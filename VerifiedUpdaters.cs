using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GerenciadorAplicativos;

// Receitas revisadas e estaticas: NUNCA executar URL, argumentos ou scripts vindos do catalogo.
internal sealed record VerifiedUpdateRecipe(
    string Identifier, string ProductName, string Version, Uri Download,
    string? Sha256, string RequiredSigner, string Arguments, bool Interactive,
    string OfficialPage);

internal static class VerifiedUpdaters
{
    private static readonly Version VcTarget = new(12, 0, 40664, 0);
    private const string ObsDigest = "c3a0b880adbe64dc4bcb68f93016916ab5b55ae43fd227115287bf80257d92dc";
    private static readonly Uri Vc64 = new("https://aka.ms/highdpimfc2013x64enu");
    private static readonly Uri Vc86 = new("https://aka.ms/highdpimfc2013x86enu");
    private static readonly Uri Obs = new("https://github.com/obsproject/obs-studio/releases/download/32.2.2/OBS-Studio-32.2.2-Windows-x64-Installer.exe");

    private static bool IsMicrosoft(string? publisher) =>
        (publisher ?? "").Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

    internal static string? GetOfficialPage(ProgramEntry entry)
    {
        if (!entry.IsUpdate) return null;
        if (entry.Name.StartsWith("Adobe Acrobat", StringComparison.OrdinalIgnoreCase))
            return "https://helpx.adobe.com/br/acrobat/desktop/get-set-up/access-the-app/update-manual.html";
        if (entry.Name.Equals("Plex", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.StartsWith("Plex Media Server", StringComparison.OrdinalIgnoreCase))
            return "https://www.plex.tv/pt-br/apps-devices/";
        if (entry.Name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase))
            return "https://obsproject.com/download";
        if (entry.Source == "Mozilla" &&
            entry.Name.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            return "https://www.firefox.com/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.StartsWith("Notepad++", StringComparison.OrdinalIgnoreCase))
            return "https://notepad-plus-plus.org/downloads/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.Contains("PowerToys", StringComparison.OrdinalIgnoreCase))
            return "https://learn.microsoft.com/windows/powertoys/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.StartsWith("PowerShell 7", StringComparison.OrdinalIgnoreCase))
            return "https://github.com/PowerShell/PowerShell/releases";
        if (entry.Source == "Microsoft" && entry.Name.Equals("Microsoft Edge", StringComparison.OrdinalIgnoreCase))
            return "https://www.microsoft.com/edge";
        if (entry.Source == "GitHub oficial" &&
            (entry.Name.Equals("Windows Terminal", StringComparison.OrdinalIgnoreCase) ||
             entry.Name.Equals("Terminal do Windows", StringComparison.OrdinalIgnoreCase)))
            return "https://github.com/microsoft/terminal/releases";
        if (entry.Source == "Proton" && entry.Name.Equals("Proton Pass", StringComparison.OrdinalIgnoreCase))
            return "https://proton.me/pass/download";
        if (entry.Source == "Mozilla" &&
            entry.Name.Contains("Thunderbird", StringComparison.OrdinalIgnoreCase))
            return "https://www.thunderbird.net/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.StartsWith("7-Zip ", StringComparison.OrdinalIgnoreCase))
            return "https://www.7-zip.org/download.html";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.Equals("GitHub Desktop", StringComparison.OrdinalIgnoreCase))
            return "https://desktop.github.com/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.StartsWith("SumatraPDF", StringComparison.OrdinalIgnoreCase))
            return "https://www.sumatrapdfreader.org/download-free-pdf-viewer";
        if (entry.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable", StringComparison.OrdinalIgnoreCase))
            return "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/";
        return null;
    }

    internal static VerifiedUpdateRecipe? Resolve(ProgramEntry entry)
    {
        if (!entry.IsUpdate || string.IsNullOrWhiteSpace(entry.AvailableVersion)) return null;

        if (IsMicrosoft(entry.Publisher) &&
            entry.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable", StringComparison.OrdinalIgnoreCase) &&
            TryVersion(entry.AvailableVersion, out var available) && available == VcTarget &&
            TryVersion(entry.Version, out var installed) && installed < VcTarget)
        {
            bool x64 = entry.Name.Contains("(x64)", StringComparison.OrdinalIgnoreCase);
            bool x86 = entry.Name.Contains("(x86)", StringComparison.OrdinalIgnoreCase);
            if (x64 == x86) return null;
            return new(x64 ? "vc2013-x64" : "vc2013-x86", "Microsoft Visual C++ 2013",
                VcTarget.ToString(), x64 ? Vc64 : Vc86, null, "Microsoft Corporation",
                "/install /quiet /norestart", false,
                "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/");
        }
        if (entry.Name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase) &&
            (entry.Publisher.Contains("OBS", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Contains("Project", StringComparison.OrdinalIgnoreCase)) &&
            TryVersion(entry.Version, out var obsInstalled) && obsInstalled < new Version(32, 2, 2, 0) &&
            TryVersion(entry.AvailableVersion, out var obsAvailable) && obsAvailable == new Version(32, 2, 2, 0))
        {
            return new("obs-x64-32.2.2", "OBS Studio", "32.2.2", Obs, ObsDigest,
                "OBS Project, LLC", "", true, "https://obsproject.com/download");
        }
        return null;
    }

    internal static bool TryVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        var parts = (value ?? "").Trim().TrimStart('v', 'V').Split('.');
        if (parts.Length < 2 || parts.Length > 4 || parts.Any(x => !int.TryParse(x, out int n) || n < 0))
            return false;
        string padded = string.Join(".", parts.Concat(Enumerable.Repeat("0", 4 - parts.Length)));
        if (!Version.TryParse(padded, out var parsed) || parsed is null) return false;
        version = parsed;
        return true;
    }

    internal static bool IsInstalledVersionAtLeast(string identifier, string version, IEnumerable<ProgramEntry> apps)
    {
        if (!TryVersion(version, out var target)) return false;
        foreach (var entry in apps)
        {
            bool belongs = identifier switch
            {
                "vc2013-x64" => entry.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable (x64)", StringComparison.OrdinalIgnoreCase),
                "vc2013-x86" => entry.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable (x86)", StringComparison.OrdinalIgnoreCase),
                "obs-x64-32.2.2" => entry.Name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
            if (belongs && TryVersion(entry.Version, out var installed) && installed >= target)
                return true;
        }
        return false;
    }

    private static bool IsAllowedRemoteUri(Uri uri, string id)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsAbsoluteUri || !string.IsNullOrEmpty(uri.UserInfo) || uri.Port != 443)
            return false;
        string host = uri.Host.ToLowerInvariant();
        if (id.StartsWith("vc2013-", StringComparison.Ordinal))
            return host is "aka.ms" or "download.microsoft.com" or "download.visualstudio.microsoft.com";
        if (id.StartsWith("obs-", StringComparison.Ordinal))
            return host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
        return false;
    }

    internal static async Task<string> DownloadAndVerifyAsync(VerifiedUpdateRecipe recipe,
        string directory, Action<string> report, CancellationToken cancel)
    {
        if (!IsAllowedRemoteUri(recipe.Download, recipe.Identifier)) throw new InvalidDataException("Endereço não autorizado.");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, recipe.Identifier + ".exe");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GerenciadorFinal/5.12.7");
        Uri target = recipe.Download;
        const long maxBytes = 260L * 1024 * 1024;
        for (int redirect = 0; redirect <= 7; redirect++)
        {
            cancel.ThrowIfCancellationRequested();
            if (!IsAllowedRemoteUri(target, recipe.Identifier)) throw new InvalidDataException("Redirecionamento fora do domínio autorizado.");
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (response.Headers.Location is null) throw new InvalidDataException("Redirecionamento incompleto.");
                target = new Uri(target, response.Headers.Location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > maxBytes)
                throw new InvalidDataException("Arquivo excede o limite de segurança.");
            report("Baixando instalador do fabricante...");
            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            await using (var dest = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[128 * 1024];
                long total = 0;
                long reported = 0;
                int n;
                while ((n = await source.ReadAsync(buffer, cancel)) > 0)
                {
                    total += n;
                    if (total > maxBytes) throw new InvalidDataException("Arquivo excede o limite de segurança.");
                    await dest.WriteAsync(buffer.AsMemory(0, n), cancel);
                    if (total - reported >= 10 * 1024 * 1024)
                    {
                        reported = total;
                        report("Baixando instalador oficial: " + (total / (1024 * 1024)).ToString("N0") + " MB");
                    }
                }
                if (total < 50_000) throw new InvalidDataException("Arquivo recebido está incompleto.");
            }
            report("Verificando integridade e assinatura digital...");
            if (recipe.Sha256 is string digest)
            {
                using var file = File.OpenRead(path);
                string actual = Convert.ToHexString(await SHA256.HashDataAsync(file, cancel));
                if (!actual.Equals(digest, StringComparison.OrdinalIgnoreCase))
                    throw new CryptographicException("SHA-256 não corresponde ao arquivo oficial.");
            }
            if (!VerifyAuthenticode(path, recipe.RequiredSigner, out string issue))
                throw new CryptographicException("Assinatura Authenticode inválida ou editor não esperado (" + issue + ").");
            return path;
        }
        throw new InvalidDataException("Excesso de redirecionamentos.");
    }

    private static bool VerifyAuthenticode(string path, string signer, out string issue)
    {
        issue = "";
        // A verificação de cadeia e assinatura cabe ao Windows (WinVerifyTrust).
        var info = new WinTrustFileInfo { Size = (uint)Marshal.SizeOf<WinTrustFileInfo>(), FilePath = path };
        IntPtr pInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(info, pInfo, false);
            var data = new WinTrustData
            {
                Size = (uint)Marshal.SizeOf<WinTrustData>(), UiChoice = 2,
                RevocationChecks = 1, UnionChoice = 1, FileInfo = pInfo,
                StateAction = 0, ProviderFlags = 0x80
            };
            Guid action = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            int status = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            if (status != 0)
            {
                issue = "WinVerifyTrust=0x" + status.ToString("X8");
                return false;
            }
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            string name = cert.GetNameInfo(X509NameType.SimpleName, false);
            issue = "editor=" + name;
            return name.Equals(signer, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            issue = "validacao=" + ex.GetType().Name;
            return false;
        }
        finally { Marshal.DestroyStructure<WinTrustFileInfo>(pInfo); Marshal.FreeHGlobal(pInfo); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr Handle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint Size;
        public IntPtr PolicyCallback;
        public IntPtr ClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, ref WinTrustData data);

    internal static async Task<int> LaunchAsync(VerifiedUpdateRecipe recipe, string verifiedFile, Action<string> report)
    {
        if (recipe.Identifier == "obs-x64-32.2.2" &&
            Process.GetProcessesByName("obs64").Any())
            throw new InvalidOperationException("Feche o OBS Studio antes de instalar a atualização.");
        report(recipe.Interactive
            ? "Abrindo instalador oficial para você concluir as opções necessárias..."
            : "Executando atualização oficial. O Windows pode solicitar autorização UAC...");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = verifiedFile, Arguments = recipe.Arguments,
            UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(verifiedFile)!
        }) ?? throw new InvalidOperationException("Instalador não foi iniciado.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
