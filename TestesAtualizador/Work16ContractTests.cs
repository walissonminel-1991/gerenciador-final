using System.Text;
using GerenciadorAplicativos;

internal static class Work16ContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        var genericMsix = new ProgramEntry { Name = "Localized Reader", Publisher = "Vendor", Version = "1.0.0",
            InventoryId = "MSIX|Vendor.Reader", InventoryOnly = true, MsixPackageFamilyName = "Vendor.Reader_123abc",
            IsUpdate = true, AvailableVersion = "2.0.0", UpdateRequiresReview = true };
        check(EcosystemInventory.CanQueryVersions(genericMsix) && !genericMsix.VerifiedMsixIdentity &&
            VerifiedUpdaters.Resolve(genericMsix) is null && VerifiedUpdaters.GetPrimaryAction(genericMsix) == "windows-manage",
            "W16 MSIX generico participa da consulta por PFN sem herdar atualizador ou desinstalador");
        check(!EcosystemInventory.CanQueryVersions(new ProgramEntry { InventoryOnly = true, InventoryId = "GAME|Fixture" }),
            "W16 elegibilidade consultiva MSIX nao promove jogos ou inventario sem identidade");
        var reference = new CatalogManifestReference("GF.Synthetic", "GF Synthetic App", "2.0.0", "", new string('A',64));
        foreach(string type in new[]{"nullsoft","inno","msi"})
        {
            var plan=AutomaticPackageManifest.Select(Encoding.UTF8.GetBytes(Work16CoverageProbe.Manifest(type,true)),reference,"x64","user",type);
            check(plan is {Interactive:true} && !plan.Arguments.Contains("/qn") && !plan.Arguments.Contains("/S ") && !plan.Arguments.Contains("/VERYSILENT"),
                "W16 contrato supervisionado explicito sem promover a modo silencioso: "+type);
            var app=new ProgramEntry{Name="GF Synthetic App",Publisher="GF Synthetic Tests",Version="1.0.0",AvailableVersion="2.0.0",IsUpdate=true,
                InventoryId="CurrentUser|Registry64|GF-W16-"+type,InstallLocation=@"C:\Synthetic\"+type};
            var installation=new AutomaticAppInstallation(AutomaticAppInstallation.Binding(app),app.Name,app.Publisher,app.Version,"user","x64",app.InstallLocation,
                app.InstallLocation+@"\app.exe",app.InstallLocation+@"\uninstall.exe",type,"");
            AutomaticUpdateDiscovery.Attach(AutomaticUpdateDiscovery.SimulateIssued(app,installation,plan!));
            var recipe=AutomaticUpdateDiscovery.Resolve(app);
            check(recipe is {Interactive:true} && AutomaticUpdateDiscovery.IsApproved(recipe),"W16 plano supervisionado percorre emissao efemera existente");
            check(!AutomaticUpdateDiscovery.IsApproved(recipe! with{Interactive=false}),"W16 mudar modo na copia do contrato nao transfere autorizacao");
        }
        string arm=Work16CoverageProbe.Manifest("msi",false).Replace("Architecture: x64","Architecture: arm64");
        check(AutomaticPackageManifest.Select(Encoding.UTF8.GetBytes(arm),reference,"arm64","user","msi") is {Architecture:"arm64"} &&
            AutomaticPackageManifest.Select(Encoding.UTF8.GetBytes(arm),reference,"x64","user","msi") is null,
            "W16 manifesto ARM64 exige exatamente a arquitetura instalada");
        check(AutomaticAppInstallation.ArchitectureFor(0xaa64)=="arm64" && AutomaticAppInstallation.ArchitectureFor(0x8664)=="x64" &&
            AutomaticAppInstallation.ArchitectureFor(0x1c4)=="", "W16 PE distingue ARM64 x64 e ARM32 sem inferencia cruzada");
        check(InstallerEngineEvidence.SameProduct("Example++ 2.0.0 (ARM64)","Example++") &&
            !InstallerEngineEvidence.SameProduct("Example++ Preview (ARM64)","Example++"),"W16 decoracao ARM64 nao remove canal ou simbolos de produto");
    }
}
