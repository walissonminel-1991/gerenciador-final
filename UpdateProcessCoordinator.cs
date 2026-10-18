using System.ComponentModel;
using System.Diagnostics;

namespace GerenciadorAplicativos;

// Encerra SOMENTE processos de aplicativos explicitamente autorizados na receita.
// Nunca usa taskkill, shell, encerramento por árvore ou processos de outros usuários.
// A opção forçada é apresentada APÓS tentativa de fechamento normal e novo consentimento.
internal enum UpdateProcessTarget { WindowsTerminal, ObsStudio }

internal static class UpdateProcessCoordinator
{
    internal static string ProcessName(UpdateProcessTarget target) => target switch
    {
        UpdateProcessTarget.WindowsTerminal => "WindowsTerminal",
        UpdateProcessTarget.ObsStudio => "obs64",
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    internal static string DisplayName(UpdateProcessTarget target) => target switch
    {
        UpdateProcessTarget.WindowsTerminal => "Windows Terminal",
        UpdateProcessTarget.ObsStudio => "OBS Studio",
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    // Validação extra de identidade: nomes de processo sozinhos podem ser falsificados.
    internal static bool IsAllowedExecutablePath(UpdateProcessTarget target, string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return false;
        string path = file.Replace('/', '\\');
        return target switch
        {
            UpdateProcessTarget.WindowsTerminal =>
                path.Contains(@"\WindowsApps\Microsoft.WindowsTerminal_", StringComparison.OrdinalIgnoreCase) &&
                path.Contains(@"__8wekyb3d8bbwe\", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(@"\WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase),
            UpdateProcessTarget.ObsStudio =>
                path.Contains(@"\obs-studio\", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(@"\bin\64bit\obs64.exe", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool IsAlive(Process p)
    {
        try { return !p.HasExited; }
        catch (InvalidOperationException) { return false; }
        catch (Win32Exception) { return false; }
    }

    private static Process[] FindCurrentSession(UpdateProcessTarget target)
    {
        int session;
        using (var current = Process.GetCurrentProcess()) session = current.SessionId;
        var all = Process.GetProcessesByName(ProcessName(target));
        var found = new List<Process>();
        foreach (var process in all)
        {
            try
            {
                if (process.SessionId == session && IsAlive(process))
                    found.Add(process);
                else process.Dispose();
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                process.Dispose();
            }
        }
        return found.ToArray();
    }

    private static bool IsVerified(Process process, UpdateProcessTarget target)
    {
        try { return IsAllowedExecutablePath(target, process.MainModule?.FileName); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException
            or NotSupportedException or UnauthorizedAccessException) { return false; }
    }

    private static bool AllExited(UpdateProcessTarget target)
    {
        var running = FindCurrentSession(target);
        try { return running.Length == 0; }
        finally { foreach (var p in running) p.Dispose(); }
    }

    internal static async Task<bool> EnsureClosedAsync(
        UpdateProcessTarget target, Func<string, bool> confirm,
        Action<string> report, CancellationToken cancel = default)
    {
        string name = DisplayName(target);
        var processes = FindCurrentSession(target);
        try
        {
            if (processes.Length == 0) return true;
            if (processes.Any(p => !IsVerified(p, target)))
            {
                report("Não foi possível confirmar a identidade de todas as janelas de " + name +
                    ". Feche o aplicativo manualmente e tente novamente.");
                return false;
            }
            if (!confirm(name + " está aberto. Salve seu trabalho antes de continuar.\n\n" +
                "O Gerenciador Final pode tentar fechar o aplicativo normalmente e depois instalar a atualização.\n\n" +
                "Deseja continuar?"))
            {
                report("Atualização cancelada sem fechar " + name + ".");
                return false;
            }
            report("Solicitando encerramento normal de " + name + "...");
            foreach (var p in processes)
            {
                try { if (IsAlive(p)) p.CloseMainWindow(); }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            }
        }
        finally { foreach (var p in processes) p.Dispose(); }

        // Espera assíncrona, sem congelar a interface.
        for (int i = 0; i < 16; i++)
        {
            cancel.ThrowIfCancellationRequested();
            if (AllExited(target)) { report(name + " fechado normalmente."); return true; }
            await Task.Delay(500, cancel);
        }

        var remaining = FindCurrentSession(target);
        try
        {
            if (remaining.Length == 0) return true;
            if (remaining.Any(p => !IsVerified(p, target)))
            {
                report("Não foi possível validar o aplicativo em execução. Nenhum processo será forçado.");
                return false;
            }
            if (!confirm(name + " ainda está aberto e não respondeu ao fechamento normal.\n\n" +
                "FORÇAR O ENCERRAMENTO pode perder abas, tarefas ou informações não salvas.\n\n" +
                "Deseja forçar somente o encerramento de " + name + " para continuar a atualização?"))
            {
                report("Encerramento forçado recusado; nenhuma instalação será iniciada.");
                return false;
            }
            report("Encerrando apenas os processos verificados de " + name + "...");
            foreach (var p in remaining)
            {
                try { if (IsAlive(p) && IsVerified(p, target)) p.Kill(entireProcessTree: false); }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    report("Windows não permitiu encerrar " + name + ". Feche-o manualmente.");
                    return false;
                }
            }
        }
        finally { foreach (var p in remaining) p.Dispose(); }
        for (int i = 0; i < 12; i++)
        {
            cancel.ThrowIfCancellationRequested();
            if (AllExited(target)) { report(name + " encerrado. Preparando atualização..."); return true; }
            await Task.Delay(500, cancel);
        }
        report(name + " permanece aberto. Atualização interrompida para evitar conflitos.");
        return false;
    }
}
