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
    private static readonly PendingDeployment Deployment = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,byte> ProtectedFolders =
        new(StringComparer.OrdinalIgnoreCase);
    internal static bool IsFolderProtected(string folder) => ProtectedFolders.ContainsKey(Path.GetFullPath(folder));
    private sealed class SourceLease(FileStream stream, string folder) : IDisposable
    {
        public void Dispose()
        {
            try { stream.Dispose(); }
            finally { ProtectedFolders.TryRemove(folder, out _); }
        }
    }
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
        if (Deployment.IsPending || entry.UpdateRequiresReview || !entry.IsUpdate || !entry.InventoryOnly ||
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

    private static readonly object VersionGate = new();
    private static Task<Version?>? versionRead;
    internal static async Task<Version?> CurrentUserVersionAsync(CancellationToken token = default)
    {
        Task<Version?> task;
        lock (VersionGate)
        {
            if (versionRead is null || versionRead.IsCompleted) versionRead = Task.Run(CurrentUserVersion);
            task = versionRead;
        }
        return await task.WaitAsync(TimeSpan.FromSeconds(8), token);
    }

    internal static Version? CurrentUserVersion()
    {
        // O nome/publisherId da familia MSIX, e nao apenas o texto exibido no Registro.
        var scan = LocalMsixInventory.ReadCurrentUser();
        return LocalMsixInventory.ConfirmedMicrosoftVersion(scan, PackageName);
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
        Action<string> report, CancellationToken token, Action<UpdateProgress>? progress = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        token = deadline.Token;
        token.ThrowIfCancellationRequested();
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
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await UpdateDownloadTransfer.CopyAsync(response, output, 200_000, maxBytes, token, progress);
            }
            progress?.Invoke(new(UpdateStage.VerifyingIntegrity));
            report("Conferindo SHA-256 e identidade Microsoft do MSIX...");
            await using (var stream = File.OpenRead(path))
            {
                string digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
                if (!digest.Equals(AssetDigest, StringComparison.OrdinalIgnoreCase))
                    throw new CryptographicException("SHA-256 diverge do release oficial Microsoft.");
            }
            progress?.Invoke(new(UpdateStage.ValidatingIdentity));
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
        if (archive.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count ||
            archive.Entries.Any(e => e.FullName.Split('/').Any(part => part is "." or "..") || e.FullName.Contains('\\')))
            throw new InvalidDataException("Entradas MSIX duplicadas ou invalidas.");
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
        XNamespace ns = "http://schemas.microsoft.com/appx/2013/bundle";
        var identities = doc.Root?.Elements(ns + "Identity").ToArray() ?? [];
        if (doc.Root?.Name != ns + "Bundle" || identities.Length != 1)
            throw new InvalidDataException("Estrutura de identidade MSIX invalida.");
        var identity = identities[0];
        var x64 = doc.Root.Element(ns + "Packages")?.Elements(ns + "Package").Where(e =>
            (string?)e.Attribute("Type") == "application" && (string?)e.Attribute("Architecture") == "x64").ToArray() ?? [];
        if (x64.Length != 1 || (string?)x64[0].Attribute("Version") != TargetVersion ||
            archive.GetEntry((string?)x64[0].Attribute("FileName") ?? "") is null)
            throw new InvalidDataException("Pacote x64 MSIX ausente ou divergente.");
        string name = identity?.Attribute("Name")?.Value ?? "(ausente)";
        string version = identity?.Attribute("Version")?.Value ?? "(ausente)";
        string publisher = identity?.Attribute("Publisher")?.Value ?? "(ausente)";
        if (name != PackageName || version != BundleTargetVersion ||
            publisher != "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US")
            throw new InvalidDataException(
                "Manifesto do pacote oficial: name=" + name +
                " version=" + version + " publisher=" + publisher +
                ". Receita deve corresponder exatamente.");
    }

    internal static async Task DeployAsync(string verifiedBundle, Action<string> report, CancellationToken token = default, Action? started = null)
    {
        if (!Path.IsPathFullyQualified(verifiedBundle) ||
            !verifiedBundle.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Arquivo MSIX verificado nao encontrado.");
        // Nova validacao imediatamente antes de entregar o arquivo ao Windows.
        // Manter a protecao ate o Windows concluir a leitura/deployment.
        var verifiedLock = InstallerFileGuard.OpenRead(verifiedBundle);
        bool transferred = false;
        try
        {
            await InstallerFileGuard.RequireDigestAsync(verifiedLock, AssetDigest, token);
            ValidateBundleIdentity(verifiedBundle);
            report("Atualizando o Windows Terminal com o instalador nativo do Windows...");
            string folder = Path.GetDirectoryName(Path.GetFullPath(verifiedBundle))!;
            ProtectedFolders[folder] = 0;
            transferred = true;
            await Deployment.RunAsync(() => BeginNativeDeploymentAsync(verifiedBundle, started),
                new SourceLease(verifiedLock, folder), TimeSpan.FromMinutes(10), token);
        }
        finally { if (!transferred) verifiedLock.Dispose(); }
    }

    private static async Task BeginNativeDeploymentAsync(string verifiedBundle, Action? started)
    {
        var operation = new PackageManager().AddPackageAsync(
            new Uri(verifiedBundle), Array.Empty<Uri>(), DeploymentOptions.None);
        started?.Invoke();
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
                    completed.TrySetException(new UpdateInstallerFailureException(result.ExtendedErrorCode?.HResult ?? unchecked((int)0x80004005)));
                }
                catch (Exception ex) { completed.TrySetException(new UpdateInstallerFailureException(ex.HResult)); }
            }
        };
        try { await completed.Task; }
        finally { GC.KeepAlive(operation); }
        // Nao marcar como sucesso ate reconsultar a identidade do pacote instalado.
    }
}
