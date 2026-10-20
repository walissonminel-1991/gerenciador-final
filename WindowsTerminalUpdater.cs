using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace GerenciadorAplicativos;

// Receita local, imutavel e limitada ao pacote STABLE da Microsoft.
// Novas versoes exigem nova validacao de release e SHA-256 antes de autorizar cliques.
internal sealed record TerminalUpdateRecipe(string Version, Uri Download, string Sha256);

internal static class WindowsTerminalUpdater
{
    // O bundle publicado pela Microsoft usa 3001.x; a versao do aplicativo e 1.x.
    private const string TargetVersion = "1.25.2733.0";
    private const string BundleTargetVersion = "3001.25.2733.0";
    private const string AssetDigest = "CCE6E9CC3C8457FA11CED1D2A7354888C7D6DB83C5ACE5D5891A52C95E3B21DA";
    private const string PackageName = "Microsoft.WindowsTerminal";
    private const string MicrosoftPublisherId = "8wekyb3d8bbwe";
    private static readonly Uri PackageUri = new(
        "https://github.com/microsoft/terminal/releases/download/v1.25.2733.0/Microsoft.WindowsTerminal_1.25.2733.0_8wekyb3d8bbwe.msixbundle");

    internal static TerminalUpdateRecipe? Resolve(ProgramEntry entry)
    {
        if (!entry.IsUpdate || !entry.InventoryOnly ||
            !entry.Publisher.Trim().Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase) ||
            !entry.Source.Equals("GitHub oficial", StringComparison.Ordinal) ||
            (entry.Name != "Windows Terminal" && entry.Name != "Terminal do Windows"))
            return null;
        if (!Version.TryParse(entry.AvailableVersion, out var latest) ||
            !Version.TryParse(entry.Version, out var installed) ||
            latest != new Version(1, 25, 2733, 0) || installed >= latest)
            return null;
        return new TerminalUpdateRecipe(TargetVersion, PackageUri, AssetDigest);
    }

    internal static Version? CurrentUserVersion()
    {
        // O nome/publisherId da familia MSIX, e nao apenas o texto exibido no Registro.
        var scan = LocalMsixInventory.ReadCurrentUser();
        if (scan.Status != "ok") return null;
        var item = scan.Items.FirstOrDefault(x =>
            x.PackageName.Equals(PackageName, StringComparison.OrdinalIgnoreCase) &&
            x.Publisher.Equals("Microsoft Corporation", StringComparison.Ordinal));
        return item is not null && Version.TryParse(item.Version, out var result) ? result : null;
    }

    private static bool AllowedDownloadUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.Port != 443) return false;
        return uri.Host.ToLowerInvariant() switch
        {
            "github.com" => uri.AbsolutePath.Equals(
                "/microsoft/terminal/releases/download/v1.25.2733.0/Microsoft.WindowsTerminal_1.25.2733.0_8wekyb3d8bbwe.msixbundle",
                StringComparison.Ordinal),
            "release-assets.githubusercontent.com" => true,
            "objects.githubusercontent.com" => true,
            _ => false
        };
    }

    internal static async Task<string> DownloadAndVerifyAsync(TerminalUpdateRecipe recipe, string folder,
        Action<string> report, CancellationToken token)
    {
        if (recipe.Version != TargetVersion ||
            !recipe.Download.Equals(PackageUri) ||
            !recipe.Sha256.Equals(AssetDigest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Receita Windows Terminal nao autorizada.");
        folder = InstallerFileGuard.ValidateLocalPath(folder);
        using var parentLock = InstallerFileGuard.PinDirectory(Path.GetDirectoryName(folder)!, allowLeafWrites: true);
        Directory.CreateDirectory(folder);
        using var directoryLock = InstallerFileGuard.PinDirectory(folder, allowLeafWrites: true);
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GerenciadorFinal", "5.12.17"));
        Uri target = recipe.Download;
        const long maxBytes = 90L * 1024 * 1024;
        for (int redirect = 0; redirect <= 7; redirect++)
        {
            token.ThrowIfCancellationRequested();
            if (!AllowedDownloadUri(target)) throw new InvalidDataException("Endereco fora dos dominios GitHub autorizados.");
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (response.Headers.Location is null) throw new InvalidDataException("Redirecionamento incompleto.");
                target = new Uri(target, response.Headers.Location);
                continue;
            }
            if (response.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException("GitHub nao disponibilizou o pacote selecionado.");
            if (response.Content.Headers.ContentLength is long length && (length > maxBytes || length < 200_000))
                throw new InvalidDataException("Tamanho inesperado do pacote MSIX.");
            string path = Path.Combine(folder, "Microsoft.WindowsTerminal-" + TargetVersion + ".msixbundle");
            report("Baixando pacote oficial do Windows Terminal...");
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[128 * 1024];
                long bytes = 0;
                int n;
                while ((n = await input.ReadAsync(buffer, token)) > 0)
                {
                    bytes += n;
                    if (bytes > maxBytes) throw new InvalidDataException("Pacote acima do limite.");
                    await output.WriteAsync(buffer.AsMemory(0, n), token);
                }
                if (bytes < 200_000) throw new InvalidDataException("Pacote incompleto.");
            }
            report("Conferindo SHA-256 e identidade Microsoft do MSIX...");
            await using (var stream = File.OpenRead(path))
            {
                string digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
                if (!digest.Equals(AssetDigest, StringComparison.OrdinalIgnoreCase))
                    throw new CryptographicException("SHA-256 diverge do release oficial Microsoft.");
            }
            ValidateBundleIdentity(path);
            // Integridade foi conferida por SHA. Na instalacao, o Windows tambem
            // exige assinatura e confianca da familia MSIX Microsoft.WindowsTerminal.
            return path;
        }
        throw new InvalidDataException("Redirecionamentos excessivos.");
    }

    internal static void ValidateBundleIdentity(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count is < 1 or > 150)
            throw new InvalidDataException("Estrutura do MSIXBundle invalida.");
        var manifest = archive.GetEntry("AppxMetadata/AppxBundleManifest.xml");
        var signature = archive.GetEntry("AppxSignature.p7x");
        if (manifest is null || signature is null || manifest.Length is < 80 or > 512_000)
            throw new InvalidDataException("Manifesto ou assinatura MSIX ausente.");
        using var content = manifest.Open();
        using var xml = XmlReader.Create(content, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        XDocument doc = XDocument.Load(xml);
        var identity = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "Identity");
        string name = identity?.Attribute("Name")?.Value ?? "(ausente)";
        string version = identity?.Attribute("Version")?.Value ?? "(ausente)";
        string publisher = identity?.Attribute("Publisher")?.Value ?? "(ausente)";
        if (name != PackageName || version != BundleTargetVersion ||
            !publisher.StartsWith("CN=Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Manifesto do pacote oficial: name=" + name +
                " version=" + version + " publisher=" + publisher +
                ". Receita deve corresponder exatamente.");
    }

    internal static async Task DeployAsync(string verifiedBundle, Action<string> report)
    {
        if (!Path.IsPathFullyQualified(verifiedBundle) ||
            !verifiedBundle.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Arquivo MSIX verificado nao encontrado.");
        // Nova validacao imediatamente antes de entregar o arquivo ao Windows.
        // Manter a protecao ate o Windows concluir a leitura/deployment.
        using var verifiedLock = InstallerFileGuard.OpenRead(verifiedBundle);
        await InstallerFileGuard.RequireDigestAsync(verifiedLock, AssetDigest);
        ValidateBundleIdentity(verifiedBundle);
        report("Atualizando o Windows Terminal com o instalador nativo do Windows...");
        var operation = new PackageManager().AddPackageAsync(
            new Uri(verifiedBundle), Array.Empty<Uri>(), DeploymentOptions.None);
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        operation.Completed = (o, status) =>
        {
            if (status == AsyncStatus.Completed) completed.TrySetResult(true);
            else if (status == AsyncStatus.Canceled) completed.TrySetCanceled();
            else
            {
                try
                {
                    var result = o.GetResults();
                    completed.TrySetException(new InvalidOperationException(
                        "O Windows recusou o pacote MSIX: " + result.ErrorText));
                }
                catch (Exception ex) { completed.TrySetException(ex); }
            }
        };
        await completed.Task;
        // Nao marcar como sucesso ate reconsultar a identidade do pacote instalado.
    }
}
