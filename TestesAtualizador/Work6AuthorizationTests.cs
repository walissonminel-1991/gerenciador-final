using GerenciadorAplicativos;
internal static class Work6AuthorizationTests
{
    internal static void Run(Action<bool,string> verify)
    {
        ProgramEntry Obs(string publisher = "OBS Project", bool readOnly = false) => new() {
            Name="OBS Studio", Publisher=publisher, Version="31.1.2", AvailableVersion="32.2.2",
            InventoryId="HKCU/64/OBS", Source="GitHub oficial", IsUpdate=true, InventoryOnly=readOnly };
        var approved = VerifiedUpdaters.Resolve(Obs());
        verify(approved is not null, "WORK6 controle positivo: OBS legítimo tem receita");
        verify(VerifiedUpdaters.Resolve(Obs(readOnly:true)) is null,
            "WORK6 somente inventário bloqueia autorização no endpoint");
        verify(VerifiedUpdaters.Resolve(Obs("Malicious OBS Project Tools")) is null,
            "WORK6 fabricante parcial não autoriza receita OBS");
        verify(VerifiedUpdaters.Resolve(new ProgramEntry { Name="Microsoft GameInput",
            Publisher="Microsoft Corporation", Version="3.5.270.0", AvailableVersion="3.5.283.0",
            IsUpdate=true, Source="Catálogo WinGet" }) is null,
            "WORK6 GameInput protegido mesmo sem flag de inventário");
        verify(!VerifiedUpdaters.ConfirmInstallation(approved! with { Version="31.1.2" }, Obs(), [Obs()]),
            "WORK6 receita com alvo adulterado não confirma atualização inexistente");
    }
}
