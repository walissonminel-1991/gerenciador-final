using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace GerenciadorAplicativos;

internal enum DriverInfTrust { VerifiedInf, PublisherNotEstablished, Rejected, Unavailable }
internal sealed record DriverInfVerificationResult(DriverInfTrust Trust, string Sha256 = "",
    string Catalog = "", string Signer = "", int NativeError = 0)
{
    internal bool CanInstall => false;
    internal bool CanMaintain => false;
    internal string Label => Trust switch
    {
        DriverInfTrust.VerifiedInf => "Assinatura do INF confirmada pelo Windows usando catálogo",
        DriverInfTrust.PublisherNotEstablished => "Confiança no editor não estabelecida; preservar",
        DriverInfTrust.Rejected => "INF não aprovado na verificação; preservar",
        _ => "Verificação do INF indisponível; preservar"
    };
    internal string Scope => "Verifica somente o INF instalado e seu catálogo. Não verifica todos os arquivos do pacote, origem de download, compatibilidade ou garantia de revogação online. Não autoriza instalação/remoção.";
}

internal static class DriverInfVerification
{
    // Erros publicados em winerror.h. Uma assinatura Authenticode com confiança
    // não estabelecida nunca é promovida a aprovação automática.
    internal static DriverInfTrust Interpret(bool verified, int error, bool hasCatalog, bool hasSigner) =>
        (verified || unchecked((uint)error) == 0xE0000241) && hasCatalog && hasSigner ? DriverInfTrust.VerifiedInf
        : unchecked((uint)error) == 0xE0000242 ? DriverInfTrust.PublisherNotEstablished : DriverInfTrust.Rejected;

    internal static DriverInfVerificationResult Verify(DriverStoreEvidence package, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return new(DriverInfTrust.Unavailable);
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (package.State != DriverStoreState.Mapped || DriverStoreAudit.CheckMapping(package.InfName,
            package.StorePath, package.PublishedPath, windows).State != DriverStoreState.Mapped)
            return new(DriverInfTrust.Rejected);
        try
        {
            token.ThrowIfCancellationRequested();
            RequireOrdinaryPath(package.PublishedPath);
            RequireOrdinaryPath(package.StorePath);
            // Handles sem compartilhamento de escrita/exclusão durante a consulta.
            using var published = new FileStream(package.PublishedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var stored = new FileStream(package.StorePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (published.Length is <= 0 or > 8_388_608 || stored.Length != published.Length)
                return new(DriverInfTrust.Rejected);
            string digest = Convert.ToHexString(SHA256.HashData(stored));
            if (digest != Convert.ToHexString(SHA256.HashData(published))) return new(DriverInfTrust.Rejected);
            var signature = InspectInf(package.StorePath, token);
            RequireOrdinaryPath(package.PublishedPath);
            RequireOrdinaryPath(package.StorePath);
            return signature with { Sha256 = digest };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException or
            DllNotFoundException or EntryPointNotFoundException or CryptographicException)
        { return new(DriverInfTrust.Unavailable); }
    }

    // Reutilizável para inspecionar um INF local; não produz autorização de
    // execução. O produto exige também o round trip do pacote instalado.
    internal static DriverInfVerificationResult InspectInf(string path, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return new(DriverInfTrust.Unavailable);
        try
        {
            if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
                path.StartsWith("//", StringComparison.Ordinal) || path.Split('\\', '/').Any(p => p is "." or "..") ||
                path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path[2..].Contains(':') ||
                !DriverPackageLinks.IsValidInf(Path.GetFileName(path))) return new(DriverInfTrust.Rejected);
            token.ThrowIfCancellationRequested();
            RequireOrdinaryPath(path);
            using var inf = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (inf.Length is <= 0 or > 8_388_608) return new(DriverInfTrust.Rejected);
            string digest = Convert.ToHexString(SHA256.HashData(inf));
            var info = new InfSignerInfo { Size = (uint)Marshal.SizeOf<InfSignerInfo>(), Catalog = "", Signer = "", Version = "" };
            bool verified = SetupVerifyInfFileW(path, IntPtr.Zero, ref info);
            int error = verified ? 0 : Marshal.GetLastWin32Error();
            token.ThrowIfCancellationRequested();
            var trust = Interpret(verified, error, !string.IsNullOrWhiteSpace(info.Catalog), !string.IsNullOrWhiteSpace(info.Signer));
            if (trust == DriverInfTrust.VerifiedInf)
            {
                if (!Path.IsPathFullyQualified(info.Catalog) || info.Catalog.StartsWith(@"\\", StringComparison.Ordinal))
                    return new(DriverInfTrust.Rejected);
                RequireOrdinaryPath(info.Catalog);
                using var catalog = new FileStream(info.Catalog, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (catalog.Length is <= 0 or > 67_108_864) return new(DriverInfTrust.Rejected);
            }
            RequireOrdinaryPath(path);
            return new(trust, digest, info.Catalog, info.Signer, error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException or
            DllNotFoundException or EntryPointNotFoundException or CryptographicException)
        { return new(DriverInfTrust.Unavailable); }
    }

    private static void RequireOrdinaryPath(string path)
    {
        for (FileSystemInfo? item = new FileInfo(path); item is not null;
            item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException();
        if ((File.GetAttributes(path) & FileAttributes.Directory) != 0) throw new IOException();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InfSignerInfo
    {
        internal uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Catalog;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Signer;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Version;
        internal uint SignerScore;
    }
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupVerifyInfFileW(string infName, IntPtr platform, ref InfSignerInfo info);
}
