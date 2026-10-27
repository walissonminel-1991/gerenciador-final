using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Contexto para consultar versões. Não cria receita, permissão ou processo.
internal static class InstalledUpdateIdentity
{
    internal sealed record BinaryMetadata(ushort Machine, PEMagic Magic, bool Executable,
        bool Dll, bool Managed, string Product, string Company, string Version);

    internal static InstalledApp Create(ProgramEntry app) => new(app.Name, app.Version, app.Publisher,
        EcosystemInventory.CanQueryVersions(app),
        Architecture: app.QueryArchitecture is "x86" or "x64" or "arm64" ? app.QueryArchitecture :
            Regex.IsMatch(app.Name, @"(?i)\bARM64\b") ? "arm64" :
            Regex.IsMatch(app.Name, @"(?i)\b(x64|64-bit)\b") ? "x64" :
            Regex.IsMatch(app.Name, @"(?i)\b(x86|32-bit)\b") ? "x86" : "unknown",
        Scope: app.InventoryId.StartsWith("CurrentUser|", StringComparison.Ordinal) ? "user" :
            app.InventoryId.StartsWith("LocalMachine|", StringComparison.Ordinal) ? "machine" : "unknown",
        InstallType: app.InventoryId.StartsWith("MSIX|", StringComparison.Ordinal) ? "msix" : "unknown",
        InventoryId: app.InventoryId, InstallLocation: InventoryReconciler.UpdateInstallLocation(app));

    // Apenas o executável Stable explicitamente registrado; sem varredura de
    // diretórios, inferência pelo SO/visão do Registro ou fallback para setup.exe.
    internal static string RegisteredBinary(ProgramEntry app, IReadOnlyList<string> machineRoots, string userRoot)
    {
        if (app.InventoryOnly || app.DisplayIcon.Length is 0 or > 4096 || app.InventoryId.Length > 2048 ||
            !app.Name.Equals("Google Chrome", StringComparison.OrdinalIgnoreCase) ||
            Matching.NormalizePublisher(app.Publisher) != "google" ||
            !Regex.IsMatch(app.InventoryId, @"\A(?:LocalMachine|CurrentUser)\|Registry(?:32|64)\|[^|\r\n]+\z",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))) return "";
        var icon = Regex.Match(app.DisplayIcon.Trim(), @"\A(?:""(?<path>[^""\r\n]+)""|(?<path>[^""\r\n]+?))(?:,\s*-?\d+)?\z",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        if (!icon.Success) return "";
        string path = icon.Groups["path"].Value;
        var roots = app.InventoryId.StartsWith("LocalMachine|", StringComparison.Ordinal) ? machineRoots : [userRoot];
        if (!roots.Where(r => r.Length > 0).Any(r => path.Equals(
            r.TrimEnd('\\') + @"\Google\Chrome\Application\chrome.exe", StringComparison.OrdinalIgnoreCase))) return "";
        string directory = path[..path.LastIndexOf('\\')];
        if (app.InstallLocation.Length > 0 && !app.InstallLocation.Trim().Trim('"').TrimEnd('\\')
            .Equals(directory, StringComparison.OrdinalIgnoreCase)) return "";
        return path;
    }

    internal static string CorrelateBinary(ProgramEntry app, BinaryMetadata binary)
    {
        if (!app.Name.Equals("Google Chrome", StringComparison.OrdinalIgnoreCase) ||
            Matching.NormalizePublisher(app.Publisher) != "google" ||
            !binary.Product.Equals("Google Chrome", StringComparison.OrdinalIgnoreCase) ||
            Matching.NormalizePublisher(binary.Company) != "google" || !binary.Executable || binary.Dll || binary.Managed ||
            !VerifiedUpdaters.TryVersion(app.Version, out var registered) ||
            !VerifiedUpdaters.TryVersion(binary.Version, out var actual) || actual != registered) return "unknown";
        return binary.Machine switch
        {
            0x14c when binary.Magic == PEMagic.PE32 => "x86",
            0x8664 when binary.Magic == PEMagic.PE32Plus => "x64",
            0xaa64 when binary.Magic == PEMagic.PE32Plus => "arm64",
            _ => "unknown"
        };
    }

    internal static string CaptureArchitecture(ProgramEntry app)
    {
        if (!OperatingSystem.IsWindows()) return "unknown";
        try
        {
            string path = RegisteredBinary(app,
                [Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)],
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            if (path.Length == 0) return "unknown";
            using var held = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
            using var image = new PEReader(held, PEStreamOptions.LeaveOpen);
            if (image.PEHeaders.PEHeader is not { } header) return "unknown";
            var info = FileVersionInfo.GetVersionInfo(path);
            return CorrelateBinary(app, new((ushort)image.PEHeaders.CoffHeader.Machine, header.Magic,
                (image.PEHeaders.CoffHeader.Characteristics & Characteristics.ExecutableImage) != 0,
                (image.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) != 0,
                image.HasMetadata, info.ProductName ?? "", info.CompanyName ?? "", info.FileVersion ?? ""));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
            BadImageFormatException or NotSupportedException or System.ComponentModel.Win32Exception or
            System.Security.SecurityException or RegexMatchTimeoutException)
        { return "unknown"; }
    }
}
