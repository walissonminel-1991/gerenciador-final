namespace GerenciadorAplicativos;

internal sealed record InventoryActionSummary(int Total, int Actionable, int Uninstallable, int Updates)
{
    internal int WithoutAction => Total - Actionable;
}

// A aba Aplicativos mostra AÇÕES, mas o inventário do Windows continua íntegro.
// Calcular o estado apenas quando o inventário é renovado: evitar File.Exists
// repetido ao desenhar cartões e ao alternar entre os filtros da interface.
internal static class ActionableInventory
{
    internal static bool CanUninstall(ProgramEntry app)
    {
        if (app.InventoryOnly || ProtectedApplications.IsProtectedName(app.Name))
            return false;

        // Um caminho absoluto que não existe NÃO representa uma ação utilizável.
        // Examinar ambas as alternativas registradas; não executar os comandos.
        return MaintenanceAudit.CheckUninstaller(app.UninstallString, false) == "available" ||
            MaintenanceAudit.CheckUninstaller(app.QuietUninstallString, false) == "available";
    }

    internal static bool ShouldShow(ProgramEntry app) => app.CanUninstall || app.IsUpdate;

    internal static int VisibleCount(IEnumerable<ProgramEntry> entries) =>
        entries.Count(ShouldShow);

    // Somente dados em memoria: nao consultar arquivos durante a pintura da UI.
    // Um programa com duas acoes continua contando como uma unica entrada.
    internal static InventoryActionSummary Summarize(IEnumerable<ProgramEntry> entries)
    {
        int total = 0, actionable = 0, uninstallable = 0, updates = 0;
        foreach (var entry in entries)
        {
            total++;
            if (ShouldShow(entry)) actionable++;
            if (entry.CanUninstall) uninstallable++;
            if (entry.IsUpdate) updates++;
        }
        return new(total, actionable, uninstallable, updates);
    }
}
