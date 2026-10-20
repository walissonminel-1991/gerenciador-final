namespace GerenciadorAplicativos;

// Motor reutilizável: a receita identifica quais arquivos o instalador substituirá.
// Só processos realmente reportados pelo Restart Manager são considerados.
internal static class UpdateBlockingPreflight
{
    internal static UpdateProcessTarget? MainProcessFor(string recipe) => recipe switch
    {
        "obs-x64-32.2.2" => UpdateProcessTarget.ObsStudio,
        "notepadpp-x64-8.9.8.1" => UpdateProcessTarget.NotepadPlusPlus,
        "sumatrapdf-x64-3.6.1" or "sumatrapdf-x86-3.6.1" => UpdateProcessTarget.SumatraPdf,
        _ => null
    };

    internal static bool TryKnownTarget(string processName, out UpdateProcessTarget target)
    {
        switch (processName.Trim().ToLowerInvariant())
        {
            case "msedge": target = UpdateProcessTarget.MicrosoftEdge; return true;
            case "brave": target = UpdateProcessTarget.BraveBrowser; return true;
            case "chrome": target = UpdateProcessTarget.GoogleChrome; return true;
            case "firefox": target = UpdateProcessTarget.MozillaFirefox; return true;
            case "obs64": target = UpdateProcessTarget.ObsStudio; return true;
            case "notepad++": target = UpdateProcessTarget.NotepadPlusPlus; return true;
            case "sumatrapdf": target = UpdateProcessTarget.SumatraPdf; return true;
            default: target = default; return false;
        }
    }

    private static string[] ResourcesFor(string recipe) => recipe switch
    {
        "obs-x64-32.2.2" => InstallerLockScanner.ObsResources(),
        "notepadpp-x64-8.9.8.1" => InstallerLockScanner.NotepadResources(),
        "sumatrapdf-x64-3.6.1" or "sumatrapdf-x86-3.6.1" => InstallerLockScanner.SumatraResources(),
        _ => []
    };

    internal static async Task<bool> ReadyAsync(string recipe,
        Func<string, bool> confirm, Action<string> report)
    {
        var primary = MainProcessFor(recipe);
        if (primary is null) return true; // Outros instaladores seguem a politica validada.
        if (!await UpdateProcessCoordinator.EnsureClosedAsync(primary.Value, confirm, report))
            return false;

        var files = await Task.Run(() => ResourcesFor(recipe));
        if (files.Length == 0)
        {
            report("Não foi possível identificar os arquivos da instalação. " +
                "Se o instalador pedir que outro programa seja fechado, encerre-o manualmente.");
            return true; // A validação e os avisos nativos do instalador continuam ativos.
        }

        var handled = new HashSet<UpdateProcessTarget>();
        // Cada iteração reconsulta o Windows; jamais encerra um processo apenas por estar aberto.
        for (int pass = 0; pass < 5; pass++)
        {
            var scan = await Task.Run(() => InstallerLockScanner.Inspect(files));
            if (!scan.Success)
            {
                report("Não foi possível confirmar os bloqueios (" + scan.Detail +
                    "). Feche os aplicativos que usam arquivos do programa antes de instalar.");
                return false;
            }
            if (scan.Processes.Count == 0)
            {
                report("Nenhum processo conhecido está bloqueando os arquivos examinados.");
                return true;
            }
            if (scan.Processes.Any(p => p.IsSystem))
            {
                report("O Windows identificou arquivos em uso por um serviço do sistema. " +
                    "Nenhum processo será encerrado automaticamente.");
                return false;
            }
            var unknown = scan.Processes.Where(p =>
                !TryKnownTarget(p.Name, out _)).Select(p => p.Name).Distinct().ToArray();
            if (unknown.Length > 0)
            {
                report("Há arquivos em uso por outro programa (" +
                    string.Join(", ", unknown.Take(3)) +
                    "). Feche-o manualmente e tente de novo. Nenhum encerramento indiscriminado.");
                return false;
            }
            var targets = scan.Processes
                .Select(p => { TryKnownTarget(p.Name, out var t); return t; })
                .Distinct().ToArray();
            foreach (var target in targets)
            {
                if (!handled.Add(target))
                {
                    report("Um aplicativo voltou a utilizar arquivos da instalação. " +
                        "Feche-o manualmente antes de tentar novamente.");
                    return false;
                }
                report("Arquivo em uso detectado: " + UpdateProcessCoordinator.DisplayName(target));
                if (!await UpdateProcessCoordinator.EnsureClosedAsync(target, confirm, report))
                    return false;
            }
        }
        report("Ainda há processos usando os arquivos; a instalação não será iniciada.");
        return false;
    }
}
