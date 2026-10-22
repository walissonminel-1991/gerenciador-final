using GerenciadorAplicativos;

// Ensaio de ponta a ponta com inventario inteiramente ficticio.
// NAO baixa nem executa instaladores; nao requer administrador ou WinGet.
internal static class SimulatedUpdateScenarioTests
{
    internal static void Run(Action<bool, string> check)
    {
        var original = new ProgramEntry
        {
            Name = "OBS Studio", Version = "31.1.2", AvailableVersion = "32.2.2",
            Publisher = "OBS Project", Source = "GitHub oficial",
            InventoryId = "LocalMachine|Registry64|OBS-Fixture", IsUpdate = true
        };
        var recipe = VerifiedUpdaters.Resolve(original);
        check(recipe is { Identifier: "obs-x64-32.2.2" } &&
            VerifiedUpdaters.IsApprovedRecipe(recipe),
            "SIMULACAO ATUALIZACAO: produto/versionamento geram receita local aprovada, sem download");

        var after = new ProgramEntry
        {
            Name = original.Name, Version = "32.2.2", Publisher = original.Publisher,
            InventoryId = original.InventoryId
        };
        check(VerifiedUpdaters.ConfirmInstallation(recipe!, original, [after]),
            "SIMULACAO ATUALIZACAO: nova versao so seria confirmada pela mesma identidade no inventario posterior");
        check(!VerifiedUpdaters.ConfirmInstallation(recipe!, original,
                [new ProgramEntry { Name = "OBS Studio", Version = "32.2.2", Publisher = "OBS Project",
                    InventoryId = "CurrentUser|Registry64|OBS-Fixture" }]) &&
            !VerifiedUpdaters.ConfirmInstallation(recipe!, original,
                [new ProgramEntry { Name = "OBS Studio", Version = "32.2.2", Publisher = "Fabricante Desconhecido",
                    InventoryId = original.InventoryId }]),
            "SIMULACAO ATUALIZACAO: outro perfil ou fabricante divergente nao confirma atualizacao");
        check(!VerifiedUpdaters.ConfirmInstallation(recipe!, original, [original]) &&
            !VerifiedUpdaters.ConfirmInstallation(recipe!, original, []),
            "SIMULACAO ATUALIZACAO: versao antiga ou inventario vazio permanece sem confirmacao");

        var review = new ProgramEntry
        {
            Name = original.Name, Version = original.Version, AvailableVersion = original.AvailableVersion,
            Publisher = original.Publisher, InventoryId = original.InventoryId, IsUpdate = true,
            UpdateRequiresReview = true
        };
        check(VerifiedUpdaters.Resolve(review) is null &&
            VerifiedUpdaters.GetPrimaryAction(review) != "update",
            "SIMULACAO ATUALIZACAO: conflito de fontes impede receita direta");
        check(!VerifiedUpdaters.IsApprovedRecipe(recipe! with { Arguments = "/quiet /install" }) &&
            !VerifiedUpdaters.IsApprovedRecipe(recipe! with { Download = new Uri("https://example.invalid/installer.exe") }),
            "SIMULACAO ATUALIZACAO: parametros e origem adulterados sao recusados");
        check(VerifiedUpdaters.Resolve(new ProgramEntry { Name = "OBS Studio", Version = "31.1.2",
                AvailableVersion = "32.2.2", Publisher = "Fabricante Desconhecido", IsUpdate = true }) is null,
            "SIMULACAO ATUALIZACAO: editor diferente nao recebe instalador");
    }
}
