using System.ComponentModel;
using System.Diagnostics;

namespace GerenciadorAplicativos;

// Encerra SOMENTE processos de aplicativos explicitamente autorizados na receita.
// Nunca usa taskkill, shell, encerramento por árvore ou processos de outros usuários.
// A opção forçada é apresentada APÓS tentativa de fechamento normal e novo consentimento.
internal enum UpdateProcessTarget { WindowsTerminal, ObsStudio, MicrosoftEdge, BraveBrowser, GoogleChrome, MozillaFirefox, NotepadPlusPlus }

internal static class UpdateProcessCoordinator
{
    internal static string ProcessName(UpdateProcessTarget target) => target switch
    {
        UpdateProcessTarget.WindowsTerminal => "WindowsTerminal",
        UpdateProcessTarget.ObsStudio => "obs64",
        UpdateProcessTarget.MicrosoftEdge => "msedge",
        UpdateProcessTarget.BraveBrowser => "brave",
        UpdateProcessTarget.GoogleChrome => "chrome",
        UpdateProcessTarget.MozillaFirefox => "firefox",
        UpdateProcessTarget.NotepadPlusPlus => "notepad++",
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    internal static string DisplayName(UpdateProcessTarget target) => target switch
    {
        UpdateProcessTarget.WindowsTerminal => "Windows Terminal",
        UpdateProcessTarget.ObsStudio => "OBS Studio",
        UpdateProcessTarget.MicrosoftEdge => "Microsoft Edge",
        UpdateProcessTarget.BraveBrowser => "Brave Browser",
        UpdateProcessTarget.GoogleChrome => "Google Chrome",
        UpdateProcessTarget.MozillaFirefox => "Mozilla Firefox",
        UpdateProcessTarget.NotepadPlusPlus => "Notepad++",
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    // Validação extra de identidade: nomes de processo sozinhos podem ser falsificados.
    internal static bool IsAllowedExecutablePath(UpdateProcessTarget target, string? file)
    {
        if (string.IsNullOrWhiteSpace(file)) return false;
        string path;
        try { path = Path.GetFullPath(file.Replace('/', '\\')); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return false; }
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string windowsApps = Path.Combine(programFiles, "WindowsApps") +
            Path.DirectorySeparatorChar;
        string edgePath = Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe");
        string edgeX86Path = Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe");
        string bravePath = Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe");
        string braveX86Path = Path.Combine(programFilesX86, "BraveSoftware", "Brave-Browser", "Application", "brave.exe");
        string chromePath = Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe");
        string chromeX86Path = Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe");
        string firefoxPath = Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe");
        string firefoxX86Path = Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe");
        string nppPath = Path.Combine(programFiles, "Notepad++", "notepad++.exe");
        return target switch
        {
            UpdateProcessTarget.WindowsTerminal =>
                path.StartsWith(windowsApps + "Microsoft.WindowsTerminal_", StringComparison.OrdinalIgnoreCase) &&
                path.Contains(@"__8wekyb3d8bbwe\", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(@"\WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase),
            UpdateProcessTarget.ObsStudio =>
                path.Contains(@"\obs-studio\", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(@"\bin\64bit\obs64.exe", StringComparison.OrdinalIgnoreCase),
            UpdateProcessTarget.MicrosoftEdge =>
                path.Equals(edgePath, StringComparison.OrdinalIgnoreCase) ||
                path.Equals(edgeX86Path, StringComparison.OrdinalIgnoreCase),
            UpdateProcessTarget.BraveBrowser =>
                path.Equals(bravePath, StringComparison.OrdinalIgnoreCase) ||
                path.Equals(braveX86Path, StringComparison.OrdinalIgnoreCase),
            UpdateProcessTarget.GoogleChrome =>
                path.Equals(chromePath, StringComparison.OrdinalIgnoreCase) ||
                path.Equals(chromeX86Path, StringComparison.OrdinalIgnoreCase),
            UpdateProcessTarget.MozillaFirefox =>
                path.Equals(firefoxPath, StringComparison.OrdinalIgnoreCase) ||
                path.Equals(firefoxX86Path, StringComparison.OrdinalIgnoreCase),
            UpdateProcessTarget.NotepadPlusPlus =>
                path.Equals(nppPath, StringComparison.OrdinalIgnoreCase),
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
            string firstQuestion = target is UpdateProcessTarget.MicrosoftEdge or UpdateProcessTarget.BraveBrowser
                or UpdateProcessTarget.GoogleChrome or UpdateProcessTarget.MozillaFirefox
                ? "O Windows identificou que " + name +
                  " está usando arquivos necessários para esta atualização.\n\n" +
                  "Salve suas abas, downloads e formulários antes de continuar.\n\n" +
                  "Deseja permitir que o Gerenciador Final feche " + name + " normalmente?"
                : name + " está aberto. Salve seu trabalho antes de continuar.\n\n" +
                  "O Gerenciador Final pode tentar fechar o aplicativo normalmente e depois instalar a atualização.\n\n" +
                  "Deseja continuar?";
            if (!confirm(firstQuestion))
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
