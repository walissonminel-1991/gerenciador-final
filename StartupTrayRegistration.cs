using Microsoft.Win32;

namespace GerenciadorAplicativos;

// Opt-in por usuario. Nenhuma tarefa, servico, UAC ou instalador e criado.
internal static class StartupTrayRegistration
{
    internal const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string RunOncePath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    internal const string ValueName = "GerenciadorFinal";
    internal const string RecoveryValueName = "GerenciadorFinal-Revisao";
    internal const string MonitorValueName = "GerenciadorFinal-Monitor";
    internal const string TrayArgument = "--tray";
    internal const string MonitorArgument = "--monitor";
    internal static string Command(string executable, bool recovery = false) =>
        BuildCommand(executable, TrayArgument + (recovery ? " --post-reboot" : ""));
    internal static string MonitorCommand(string executable) => BuildCommand(executable, MonitorArgument);

    private static string BuildCommand(string executable, string arguments)
    {
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
            !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            executable.Contains('"') || executable.Contains('\n') ||
            executable.Contains('\r') || executable.Length > 220)
            throw new InvalidDataException("Executavel nao elegivel para inicializacao.");
        return "\"" + executable + "\" " + arguments;
    }

    // A chave Run é o registro solicitado pelo GF. A configuração efetiva
    // de Aplicativos de inicialização do Windows pode impedir o lançamento.
    internal static bool IsMonitorStartupRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunPath, false);
        return string.Equals(key?.GetValue(MonitorValueName) as string,
            MonitorCommand(Environment.ProcessPath ?? throw new InvalidOperationException("Executavel nao identificado.")),
            StringComparison.OrdinalIgnoreCase);
    }

    internal static void SetMonitorStartupRegistered(bool enabled)
    {
        if (!enabled)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(RunPath, true);
            existing?.DeleteValue(MonitorValueName, false);
            return;
        }
        string command = MonitorCommand(Environment.ProcessPath ?? throw new InvalidOperationException("Executavel nao identificado."));
        using var key = Registry.CurrentUser.CreateSubKey(RunPath, true)
            ?? throw new IOException("Nao foi possivel configurar a inicializacao do monitor.");
        key.SetValue(MonitorValueName, command, RegistryValueKind.String);
    }
    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunPath, false);
        return string.Equals(key?.GetValue(ValueName) as string,
            Command((Environment.ProcessPath ?? throw new InvalidOperationException("Executavel nao identificado."))), StringComparison.OrdinalIgnoreCase);
    }
    internal static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            using var existing = Registry.CurrentUser.OpenSubKey(RunPath, true);
            existing?.DeleteValue(ValueName, false);
            return;
        }
        using var key = Registry.CurrentUser.CreateSubKey(RunPath, true)
            ?? throw new IOException("Nao foi possivel configurar inicializacao.");
        key.SetValue(ValueName, Command((Environment.ProcessPath ?? throw new InvalidOperationException("Executavel nao identificado."))), RegistryValueKind.String);
    }
    // Somente com a autorizacao especifica para reiniciar, e somente se
    // o inicio permanente nao estiver habilitado. Executa apos proximo login.
    internal static void QueueOneTimeRecovery()
    {
        if (IsEnabled()) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunOncePath, true)
            ?? throw new IOException("Nao foi possivel configurar retorno apos reinicio.");
        key.SetValue(RecoveryValueName, Command((Environment.ProcessPath ?? throw new InvalidOperationException("Executavel nao identificado.")), recovery: true),
            RegistryValueKind.String);
    }
    internal static void ClearOneTimeRecovery()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunOncePath, true);
        key?.DeleteValue(RecoveryValueName, false);
    }
}
