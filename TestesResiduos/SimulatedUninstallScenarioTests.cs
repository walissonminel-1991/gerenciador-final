using GerenciadorAplicativos;

// Fluxo 2: dados inventados; nenhum desinstalador/Process.Start e invocado.
// A ausencia de um item no snapshot simulado nao comprova que Windows o removeu.
internal static class SimulatedUninstallScenarioTests
{
    internal static void Run(Action<bool, string> check)
    {
        var installed = new ProgramEntry
        {
            Name = "SumatraPDF", Publisher = "Krzysztof Kowalczyk",
            InventoryId = "LocalMachine|Registry64|Sumatra-Fixture",
            UninstallString = "msiexec.exe /X{00000000-0000-0000-0000-000000000123}"
        };
        var protectedComponent = new ProgramEntry
        {
            Name = "Microsoft GameInput", Publisher = "Microsoft Corporation",
            UninstallString = "msiexec.exe /X{00000000-0000-0000-0000-000000000321}"
        };
        var stale = new ProgramEntry
        {
            Name = "Programa Ficticio Desatualizado",
            UninstallString = "\"Z:\\GF-Nao-Existe\\uninstall.exe\""
        };
        check(ActionableInventory.CanUninstall(installed) &&
            !ActionableInventory.CanUninstall(protectedComponent) &&
            !ActionableInventory.CanUninstall(stale),
            "SIMULACAO DESINSTALACAO: acao valida so para software desinstalavel; protegido/ausente bloqueados");

        check(UninstallLaunchDiagnostics.Explain(1223).Contains("cancelada", StringComparison.OrdinalIgnoreCase) &&
            UninstallLaunchDiagnostics.Explain(5).Contains("negado", StringComparison.OrdinalIgnoreCase) &&
            UninstallLaunchDiagnostics.Explain(740).Contains("eleva", StringComparison.OrdinalIgnoreCase),
            "SIMULACAO DESINSTALACAO: cancelamento, acesso negado e UAC nao disparam repeticao");

        // O desinstalador simulado falhou/cancelou; snapshot continua igual.
        check(UninstallResiduals.StillInstalled(installed, [installed]),
            "SIMULACAO DESINSTALACAO: item presente depois de cancelamento impede residuos");
        var parallel = new ProgramEntry
        {
            Name = "SumatraPDF", Publisher = "SumatraPDF Reader",
            InventoryId = "CurrentUser|Registry64|Sumatra-SegundaInstalacao"
        };
        check(UninstallResiduals.StillInstalled(installed, [parallel]),
            "SIMULACAO DESINSTALACAO: versao paralela preserva residuos compartilhados");

        // Somente um inventario completo e especifico permitiria investigar,
        // nunca excluir: a hipotese de ausencia nao equivale a sucesso real.
        check(!UninstallResiduals.StillInstalled(installed, []),
            "SIMULACAO DESINSTALACAO: ausencia em snapshot ficticio habilita apenas investigacao");
        var fakeCandidate = new ResidualCandidate("SumatraPDF", @"C:\GF-Fixture-Inexistente", [@"C:\GF-Fixture-Inexistente\uninstall.exe"], 0);
        var denied = UninstallResiduals.Clean(installed, fakeCandidate);
        check(denied.RemovedFiles == 0 && denied.RemovedDirectories == 0 && denied.Failed > 0,
            "SIMULACAO DESINSTALACAO: ausencia nao autoriza apagar arquivo ou pasta");
    }
}
