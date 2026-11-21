using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal sealed record CleanupTarget(string Task, string Title, bool Elevated = false)
{
    internal static readonly CleanupTarget UserTemp = new("clean-user-temp", "Temporários do usuário");
    internal static readonly CleanupTarget WindowsTemp = new("clean-windows-temp", "Temporários do Windows", true);
    internal static readonly CleanupTarget UpdateCache = new("clear-update-cache", "Cache do Windows Update", true);
    internal static readonly CleanupTarget DirectX = new("clean-directx-cache", "Shaders DirectX");
    internal static readonly CleanupTarget RecycleBin = new("empty-recycle-bin", "Lixeira");
    internal static IReadOnlyList<CleanupTarget> All { get; } = [UserTemp, WindowsTemp, UpdateCache, DirectX, RecycleBin];

    internal static CleanupTarget? FromTask(string task) => All.FirstOrDefault(target => target.Task == task);

    private string DirectoryPath()
    {
        if (this == UserTemp) return Path.GetTempPath();
        if (this == DirectX) return Child(Environment.SpecialFolder.LocalApplicationData, "D3DSCache");
        return Child(Environment.SpecialFolder.Windows, this == WindowsTemp ? "Temp" : @"SoftwareDistribution\Download");

        static string Child(Environment.SpecialFolder folder, string relative)
        {
            string root = Environment.GetFolderPath(folder);
            if (!Path.IsPathFullyQualified(root))
                throw new InvalidDataException("O caminho da categoria não está disponível neste perfil.");
            return Path.Combine(root, relative);
        }
    }

    internal TemporaryCleanupEstimate Estimate(CancellationToken token, Action<CleanupProgress>? progress)
    {
        try
        {
            if (token.IsCancellationRequested) return new(0, 0, true) { State = CleanupState.Cancelled };
            using var services = this == UpdateCache ? new UpdateCacheServiceStatus() : null;
            return SafeTemporaryCleanup.Estimate(DirectoryPath(), token, progress, services is null ? null : services.CheckState);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { return new(0, 0, true) { State = CleanupState.Unavailable }; }
    }

    internal TemporaryCleanupResult Clean(CancellationToken token, Action<CleanupProgress>? progress, Action<string>? log)
    {
        try
        {
            if (token.IsCancellationRequested) return TemporaryCleanupResult.WithoutChanges(CleanupState.Cancelled);
            using var services = this == UpdateCache ? new UpdateCacheServiceStatus() : null;
            return SafeTemporaryCleanup.Clean(DirectoryPath(), log, token, progress, services is null ? null : services.CheckState);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            log?.Invoke(ex.Message);
            return TemporaryCleanupResult.WithoutChanges(CleanupState.Unavailable);
        }
    }
}

// Consultar os serviços requer só acesso de leitura. Não parar nem iniciar
// serviços para conseguir acesso ao cache ou interromper uma atualização.
internal sealed class UpdateCacheServiceStatus : IDisposable
{
    private readonly ServiceHandle _manager = OpenSCManagerW(null, null, 1);
    private readonly ServiceHandle? _bits;
    private readonly ServiceHandle? _windowsUpdate;

    internal UpdateCacheServiceStatus()
    {
        if (_manager.IsInvalid) return;
        _bits = OpenServiceW(_manager, "BITS", 4);
        _windowsUpdate = OpenServiceW(_manager, "wuauserv", 4);
    }

    internal CleanupState CheckState()
    {
        if (_bits is not { IsInvalid: false } || _windowsUpdate is not { IsInvalid: false } ||
            !QueryServiceStatus(_bits, out var bits) || !QueryServiceStatus(_windowsUpdate, out var update))
            return CleanupState.Unavailable;
        const uint stopped = 1;
        return bits.CurrentState == stopped && update.CurrentState == stopped ? CleanupState.Complete : CleanupState.Busy;
    }

    public void Dispose()
    {
        _windowsUpdate?.Dispose();
        _bits?.Dispose();
        _manager.Dispose();
    }

    private sealed class ServiceHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        public ServiceHandle() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint Type, CurrentState, ControlsAccepted, Win32Error, ServiceError, Checkpoint, WaitHint;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern ServiceHandle OpenSCManagerW(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern ServiceHandle OpenServiceW(ServiceHandle manager, string name, uint access);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatus(ServiceHandle service, out ServiceStatus status);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr service);
}
