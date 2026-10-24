using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GerenciadorAplicativos;

internal enum DriverStoreState { Mapped, Unavailable, Inconsistent, NotConsulted }
internal enum DriverStoreStage { None, Platform, StoreLookup, PublishedLookup, Mapping, Attributes, NativeUnavailable, PathValidation, PublishedMismatch, OutsideRepository, InfInvalid, StorePathValidation, PublishedPathValidation }
internal sealed record DriverStoreEvidence(string InfName, DriverStoreState State,
    string PublishedPath = "", string StorePath = "", DriverStoreStage FailureStage = DriverStoreStage.None, int NativeError = 0)
{
    internal string Label => State switch
    {
        DriverStoreState.Mapped => "Vínculo de pacote confirmado pela API do Windows",
        DriverStoreState.Inconsistent => "Vínculo de pacote inconsistente; preservar",
        DriverStoreState.NotConsulted => "Pacote não consultado; preservar",
        _ => "Vínculo de pacote indisponível; preservar"
    };
    // Localização não demonstra compatibilidade, conteúdo, assinatura ou remoção segura.
    internal bool CanMaintain => false;
}
internal sealed record DriverStoreAuditResult(IReadOnlyList<DriverStoreEvidence> Packages,
    bool Partial, int WithoutInf)
{
    internal string Summary => $"Driver Store: {Packages.Count(x => x.State == DriverStoreState.Mapped)}/{Packages.Count} vínculos consultivos confirmados" +
        (Partial ? " (PARCIAL)." : ".") + " Não enumera pacotes sem dispositivo nem verifica assinatura criptográfica.";
}

// Somente APIs de consulta SetupAPI. Sem comandos, enumeração recursiva,
// leitura de conteúdo, elevação, download ou qualquer operação de manutenção.
internal static class DriverStoreAudit
{
    internal static DriverInventoryResult CaptureWithPackages(CancellationToken token)
    {
        var inventory = DriverInventory.Capture(token);
        var store = Capture(inventory, token);
        var byInf = store.Packages.ToDictionary(x => x.InfName, StringComparer.OrdinalIgnoreCase);
        return inventory with
        {
            Store = store,
            Items = inventory.Items.Select(x => x with
                { PackageEvidence = byInf.GetValueOrDefault(x.InfName) }).ToArray()
        };
    }

    internal static DriverStoreAuditResult Capture(DriverInventoryResult inventory, CancellationToken token,
        Func<string, DriverStoreEvidence>? probe = null, TimeSpan? timeout = null, int maximumPackages = 256)
    {
        var links = DriverPackageLinks.Analyze(inventory);
        var clock = Stopwatch.StartNew();
        var found = new List<DriverStoreEvidence>();
        bool partial = inventory.Partial || links.WithoutInf > 0 || inventory.Items.Count == 0;
        int limit = Math.Clamp(maximumPackages, 0, 512);
        foreach (var package in links.Packages)
        {
            if (token.IsCancellationRequested || clock.Elapsed >= (timeout ?? TimeSpan.FromSeconds(6)) || found.Count >= limit)
            {
                // Registrar cada pacote ainda não consultado, sem fingir ausência.
                found.AddRange(links.Packages.Skip(found.Count).Select(p =>
                    new DriverStoreEvidence(p.InfName, DriverStoreState.NotConsulted)));
                partial = true;
                break;
            }
            DriverStoreEvidence result;
            try { result = (probe ?? Resolve)(package.InfName); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException or System.Security.SecurityException or
                DllNotFoundException or EntryPointNotFoundException)
            { result = new(package.InfName, DriverStoreState.Unavailable, FailureStage: DriverStoreStage.NativeUnavailable, NativeError: ex.HResult); }
            if (!result.InfName.Equals(package.InfName, StringComparison.OrdinalIgnoreCase))
                result = new(package.InfName, DriverStoreState.Inconsistent);
            if (result.State != DriverStoreState.Mapped) partial = true;
            found.Add(result);
        }
        return new(found.ToArray(), partial, links.WithoutInf);
    }

    internal static DriverStoreEvidence CheckMapping(string inf, string storePath,
        string publishedPath, string windows)
    {
        if (!DriverPackageLinks.IsValidInf(inf)) return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.Mapping);
        try
        {
            string expected = Path.Combine(windows, "INF", inf);
            string repository = Path.Combine(windows, "System32", "DriverStore", "FileRepository");
            if (!LocalAbsolute(windows))
                return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.PathValidation);
            if (!LocalAbsolute(storePath))
                return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.StorePathValidation);
            if (!LocalAbsolute(publishedPath))
                return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.PublishedPathValidation);
            if (!Path.GetFullPath(publishedPath).Equals(Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
                return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.PublishedMismatch);
            if (!Path.GetFullPath(storePath).StartsWith(Path.GetFullPath(repository) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
                return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.OutsideRepository);
            if (!DriverPackageLinks.IsValidInf(Path.GetFileName(storePath)))
                return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.InfInvalid);
            return new(inf, DriverStoreState.Mapped, Path.GetFullPath(publishedPath), Path.GetFullPath(storePath));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        { return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.Mapping); }
    }

    // Aceitar nome publicado simples somente na fronteira da API nativa e
    // somente se for exatamente o INF pedido. Nunca resolver um caminho
    // relativo arbitrário, mudar de INF ou procurar arquivos por semelhança.
    internal static DriverStoreEvidence CheckNativeMapping(string inf, string storePath,
        string published, string windows) => CheckMapping(inf, storePath,
            DriverPackageLinks.IsValidInf(published) && published.Equals(inf, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(windows, "INF", inf) : published, windows);

    private static bool LocalAbsolute(string path) => Path.IsPathFullyQualified(path) &&
        !path.StartsWith(@"\\", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal) &&
        !path.Contains('\0') && !path.Split('\\', '/').Any(p => p is "." or "..") &&
        (!OperatingSystem.IsWindows() || path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' &&
            path[2] == '\\' && !path[2..].Contains(':'));

    private static DriverStoreEvidence Resolve(string inf)
    {
        if (!OperatingSystem.IsWindows()) return new(inf, DriverStoreState.Unavailable, FailureStage: DriverStoreStage.Platform);
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!LocalAbsolute(windows) || !DriverPackageLinks.IsValidInf(inf)) return new(inf, DriverStoreState.Unavailable, FailureStage: DriverStoreStage.Platform);
        string published = Path.Combine(windows, "INF", inf);
        // Unicamente o INF publicado citado pelo WMI. Nunca procurar por nome parecido.
        var store = new StringBuilder(260);
        if (!SetupGetInfDriverStoreLocationW(published, IntPtr.Zero, null, store, (uint)store.Capacity, out _))
            return new(inf, DriverStoreState.Unavailable, FailureStage: DriverStoreStage.StoreLookup,
                NativeError: Marshal.GetLastWin32Error());
        var roundTrip = new StringBuilder(260);
        if (!SetupGetInfPublishedNameW(store.ToString(), roundTrip, (uint)roundTrip.Capacity, out _))
            return new(inf, DriverStoreState.Unavailable, FailureStage: DriverStoreStage.PublishedLookup,
                NativeError: Marshal.GetLastWin32Error());
        var result = CheckNativeMapping(inf, store.ToString(), roundTrip.ToString(), windows);
        if (result.State != DriverStoreState.Mapped) return result;
        // Apenas atributos e ancestrais. Falha/reparse nunca confirma vínculo.
        foreach (string file in new[] { result.PublishedPath, result.StorePath })
        {
            if ((File.GetAttributes(file) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.Attributes);
            for (var dir = new DirectoryInfo(Path.GetDirectoryName(file)!); dir is not null; dir = dir.Parent)
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) return new(inf, DriverStoreState.Inconsistent, FailureStage: DriverStoreStage.Attributes);
        }
        return result;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupGetInfDriverStoreLocationW(string fileName, IntPtr alternatePlatform,
        string? locale, StringBuilder buffer, uint capacity, out uint requiredSize);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupGetInfPublishedNameW(string fileName, StringBuilder buffer,
        uint capacity, out uint requiredSize);
}
