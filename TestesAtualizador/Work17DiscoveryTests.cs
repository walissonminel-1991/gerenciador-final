using GerenciadorAplicativos;

internal static class Work17DiscoveryTests
{
    private sealed class Source : IAutomaticCatalogSource
    {
        internal Func<ProgramEntry,CancellationToken,IReadOnlyList<CatalogManifestReference>> Lookup=(a,_)=>
            [new("GF.Fixture",a.Name,"13.0.0.0","manifests/fixture",new string('A',64))];
        internal Func<CatalogManifestReference,CancellationToken,Task<byte[]>> ReadManifest=(_,_)=>throw new InvalidDataException("fixture");
        public IReadOnlyList<CatalogManifestReference> Find(ProgramEntry app,CancellationToken token)=>Lookup(app,token);
        public IReadOnlyList<CatalogManifestReference> ByProductCode(ProgramEntry app,CancellationToken token)=>[];
        public IReadOnlyList<CatalogManifestReference> ByPackageFamily(ProgramEntry app,CancellationToken token)=>[];
        public IReadOnlyList<CatalogManifestReference> ByPackageId(ProgramEntry app,string id,CancellationToken token)=>[];
        public Task<byte[]> ManifestAsync(CatalogManifestReference r,CancellationToken t)=>ReadManifest(r,t);
    }
    private static InstalledApp[] Identities(ProgramEntry[] apps)=>apps.Select(a=>new InstalledApp(a.Name,a.Version,a.Publisher,InventoryId:a.InventoryId)).ToArray();
    internal static async Task RunAsync(Action<bool,string> check)
    {
        ProgramEntry App(int i)=>new(){Name="Fixture "+i,Version="1.0",Publisher="GF Tests",InventoryId="CurrentUser|Registry64|"+i};
        var apps=Enumerable.Range(0,3).Select(App).ToArray();
        foreach(Exception failure in new Exception[]{new InvalidDataException(),new HttpRequestException(),new IOException(),new TimeoutException(),new OperationCanceledException()})
        {
            var source=new Source();var ordinary=source.Lookup;
            source.Lookup=(a,t)=>a==apps[1]?throw failure:ordinary(a,t);
            var result=await AutomaticDiscoveryPipeline.RunAsync(apps,Identities(apps),source,CancellationToken.None,CancellationToken.None);
            check(result.Findings.Count==2 &&
                result.Findings[0].Installed.Name==apps[0].Name && result.Findings[1].Installed.Name==apps[2].Name &&
                result.Diagnostic.Status=="parcial" && result.Outcomes!.GetValueOrDefault("item_unavailable")==1,
                "W17 falha individual preserva descobertas anteriores e posteriores "+failure.GetType().Name);
            check(result.Plans.Count==0,"W17 fonte de testes sem instalacao comprovada nao emite plano");
        }
        using(var budget=new CancellationTokenSource())
        {
            var source=new Source();var ordinary=source.Lookup;source.Lookup=(a,t)=>{budget.Cancel();return ordinary(a,t);};
            var partial=await AutomaticDiscoveryPipeline.RunAsync(apps,Identities(apps),source,CancellationToken.None,budget.Token);
            check(partial.Findings.Count==1 && partial.Diagnostic.Status=="parcial" && partial.Plans.Count==0,
                "W17 prazo preserva evidencias concluidas e impede planos depois do limite");
        }
        using(var user=new CancellationTokenSource())
        {
            var source=new Source();source.Lookup=(_,_)=>{user.Cancel();throw new OperationCanceledException(user.Token);};
            bool canceled=false;try{await AutomaticDiscoveryPipeline.RunAsync(apps,Identities(apps),source,user.Token,user.Token);}catch(OperationCanceledException){canceled=true;}
            check(canceled,"W17 cancelamento explicito nao retorna scan autorizavel");
        }
        var large=Enumerable.Range(0,514).Select(App).ToArray();
        var bounded=await AutomaticDiscoveryPipeline.RunAsync(large,Identities(large),new Source(),CancellationToken.None,CancellationToken.None);
        check(bounded.Findings.Count==512 && bounded.Diagnostic.Status=="parcial" && bounded.Outcomes!.GetValueOrDefault("inventory_limit")==1,
            "W17 limite preserva 512 resultados em vez de zerar a consulta");
        bool badBinding=false;try{await AutomaticDiscoveryPipeline.RunAsync(apps,Identities(apps.Reverse().ToArray()),new Source(),CancellationToken.None,CancellationToken.None);}catch(ArgumentException){badBinding=true;}
        check(badBinding,"W17 recusa vetor de identidades desalinhado");
        var installed=new MsiPackageEvidence.Metadata("GF Synthetic MSI","GF Tests","2.0.0",Work17CoverageProbe.Code,Work17CoverageProbe.Upgrade,"x64",false,"machine","1033");
        var rule=new MsiMajorUpgradeEvidence.Rule(Work17CoverageProbe.Upgrade,"1.0.0","3.0.0","",0x100,"","GF_OLD_PRODUCTS");
        check(MsiMajorUpgradeEvidence.Covers(rule,installed),"W17 major upgrade cobre versao instalada sem executar MSI");
        foreach(var bad in new[]{rule with{Code=Work17CoverageProbe.Code},rule with{Minimum="2.1.0"},rule with{Maximum="2.0.0"},
            rule with{Attributes=2},rule with{Attributes=4},rule with{Languages="1046"},rule with{Languages="invalid"},
            rule with{Remove="SomeFeature"},rule with{Property="lowercase"},rule with{Minimum="",Maximum=""}})
            check(!MsiMajorUpgradeEvidence.Covers(bad,installed),"W17 major upgrade sem substituicao verificavel recusado");
        check(MsiMajorUpgradeEvidence.Covers(rule with{Maximum="2.0.0",Attributes=0x300},installed),"W17 major upgrade inclui limite superior explicito");
        check(MsiMajorUpgradeEvidence.Covers(rule with{Languages="1046",Attributes=0x500},installed),"W17 major upgrade respeita exclusao de idioma");
        if(!OperatingSystem.IsWindows())return;
        string folder=Path.Combine(Path.GetTempPath(),"GF-W17-Major-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            foreach(bool condition in new[]{false,true})
            {
                string file=Path.Combine(folder,condition?"conditional.msi":"valid.msi");MsiPackageEvidence.CreateSynthetic(file);
                bool missing=false;try{MsiMajorUpgradeEvidence.Require(file,installed);}catch(InvalidDataException){missing=true;}
                check(missing,"W17 MSI real inerte sem Upgrade Table permanece bloqueado");
                MsiMajorUpgradeEvidence.AddSyntheticContract(file,condition);
                bool proven=true;try{MsiMajorUpgradeEvidence.Require(file,installed);}catch(InvalidDataException){proven=false;}
                check(proven!=condition,"W17 leitor MSI nativo confere tabela e sequencia "+condition);
            }
        }
        finally{Directory.Delete(folder,true);}
        foreach(var row in Work17CoverageProbe.Measure())
        {
            var type=row.GetType();bool expected=(bool)type.GetProperty("expected_eligible")!.GetValue(row)!;
            bool actual=(bool)type.GetProperty("eligible_before_payload")!.GetValue(row)!;
            check(expected==actual,"W17 identidade e revalidacao "+type.GetProperty("id")!.GetValue(row));
        }
        using(var fixture=new Work17CoverageProbe.Fixture())
        {
            var app=fixture.App("manifest_fault");var inventory=new[]{app,App(4)};int downloads=0;
            var source=new Source{ReadManifest=(_,_)=>{downloads++;throw new InvalidDataException("individual manifest");}};
            var result=await AutomaticDiscoveryPipeline.RunAsync(inventory,Identities(inventory),source,CancellationToken.None,CancellationToken.None);
            check(downloads==1 && result.Findings.Count==2 && result.Plans.Count==0 && result.Diagnostic.Status=="parcial",
                "W17 falha de manifesto conserva versao consultiva e continua proximo aplicativo");
            var canonical=fixture.App("pipeline_alias",true);
            var aliasSource=new AliasSource();
            var ready=await AutomaticDiscoveryPipeline.RunAsync([canonical],Identities([canonical]),aliasSource,CancellationToken.None,CancellationToken.None);
            check(ready.Plans.Count==1 && ready.Outcomes!.GetValueOrDefault("msi_catalog_identity_plan")==1,"W17 pipeline promove MSI corroborado para plano pendente de validar payload");
            var local=ready.Plans.Single().Installation;
            var installedTarget=ready.Plans.Single().Installer with{Version=Work17CoverageProbe.Version};
            var canonicalAfter=new ProgramEntry{Name="TestesAtualizador",Publisher=canonical.Publisher,Version=canonical.Version,
                InventoryId=canonical.InventoryId,InstallLocation=canonical.InstallLocation,DisplayIcon=canonical.DisplayIcon,
                UninstallString=canonical.UninstallString,MsiProductCode=canonical.MsiProductCode};
            check(local.Confirm(installedTarget,[canonicalAfter]),"W17 confirmacao admite ARP canonico com mesma instalacao e familia");
            var other=fixture.App("other_family_member");
            check(!local.Current([canonical,other]) && !local.Confirm(installedTarget,[canonicalAfter,other]),
                "W17 segunda instalacao na mesma familia impede execucao e confirmacao");
            fixture.Metadata=fixture.Metadata with{UpgradeCode="{00000000-0000-0000-0000-000000000057}"};
            check(!local.Confirm(installedTarget,[canonicalAfter]),"W17 familia alterada apos operacao nao produz confirmacao positiva");
            AutomaticUpdateDiscovery.Attach(ready);
            check(AutomaticUpdateDiscovery.Resolve(canonical) is null,"W17 resultado de provedor externo nao forja selo privado de scan autenticado");
        }
    }
    private sealed class AliasSource : IAutomaticCatalogSource
    {
        public IReadOnlyList<CatalogManifestReference> Find(ProgramEntry a,CancellationToken t)=>[];
        public IReadOnlyList<CatalogManifestReference> ByProductCode(ProgramEntry a,CancellationToken t)=>[new("GF.Fixture","TestesAtualizador","13.0.0.0","",new string('A',64))];
        public IReadOnlyList<CatalogManifestReference> ByPackageFamily(ProgramEntry a,CancellationToken t)=>[];
        public IReadOnlyList<CatalogManifestReference> ByPackageId(ProgramEntry a,string id,CancellationToken t)=>[];
        public Task<byte[]> ManifestAsync(CatalogManifestReference r,CancellationToken t)=>Task.FromResult(System.Text.Encoding.UTF8.GetBytes(
            "PackageIdentifier: GF.Fixture\nPackageName: TestesAtualizador\nPublisher: GF Tests\nPackageVersion: 13.0.0.0\nManifestType: merged\nManifestVersion: 1.10.0\nInstallers:\n- InstallerType: msi\n  Architecture: x64\n  Scope: machine\n  ProductCode: '"+Work17CoverageProbe.Code+"'\n  InstallerUrl: https://vendor.example/setup.msi\n  InstallerSha256: "+new string('A',64)+"\n"));
    }
}
