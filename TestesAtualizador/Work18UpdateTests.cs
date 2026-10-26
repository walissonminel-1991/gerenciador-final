using System.Text;
using GerenciadorAplicativos;

internal static class Work18UpdateTests
{
    internal static async Task RunAsync(Action<bool,string> check)
    {
        foreach(var c in Work18CoverageProbe.Cases)
        {
            var plan=Work18CoverageProbe.Select(c);
            check((plan is not null)==(c.Expected!="none"),"W18 selecao "+c.Id);
            if(plan is null)continue;
            check(plan.Interactive==(c.Expected=="Acompanhar"),"W18 modo comprovado "+c.Id);
            check(!plan.Arguments.Contains("UNVERIFIED") && !plan.Arguments.Contains("powershell"),"W18 flags remotas descartadas "+c.Id);
            check(plan.ScopeFromPayload==c.OmitScope,"W18 contexto deve ser comprovado no payload "+c.Id);
        }
        var c1=Work18CoverageProbe.Cases.Single(c=>c.Id=="msi_scope_machine");
        var manifest=Work18CoverageProbe.Manifest(c1);
        var reference=new CatalogManifestReference("GF.Work18","TestesAtualizador","13.0.0.0","",new string('A',64));
        foreach(var altered in new[]{manifest.Replace("PackageIdentifier: GF.Work18","PackageIdentifier: Other.Product"),
            manifest.Replace("PackageVersion: 13.0.0.0","PackageVersion: 14.0.0.0"),manifest+"PackageVersion: duplicate\n"})
        {
            bool denied=false;try{AutomaticPackageManifest.Select(Encoding.UTF8.GetBytes(altered),reference,"x64","machine","msi");}
            catch(Exception ex)when(ex is InvalidDataException or YamlDotNet.Core.YamlException or ArgumentException){denied=true;}
            check(denied,"W18 manifesto adulterado nao promove novo contrato");
        }
        if(!OperatingSystem.IsWindows())return;
        foreach(var row in Work18CoverageProbe.Measure())
        {
            var type=row.GetType();
            check(string.Equals((string)type.GetProperty("action")!.GetValue(row)!,(string)type.GetProperty("expected_action")!.GetValue(row)!,StringComparison.Ordinal),
                "W18 acao com instalacao nativa "+type.GetProperty("id")!.GetValue(row));
        }
        using(var f=new Work17CoverageProbe.Fixture())
        {
            var app=f.App("security");var local=AutomaticAppInstallation.Read(app)!;var plan=Work18CoverageProbe.Select(c1)!;
            var raw=new AutomaticDiscoveryResult([],[(app,local,plan)],new("fixture","ok",0));
            AutomaticUpdateDiscovery.Attach(raw);
            check(AutomaticUpdateDiscovery.Resolve(app) is null,"W18 provedor consultivo nao emite nova autorizacao");
            AutomaticUpdateDiscovery.SealSynthetic(raw,[app]);
            AutomaticUpdateDiscovery.Attach(raw with{});
            check(AutomaticUpdateDiscovery.Resolve(app) is null,"W18 copia de scan nao herda selo");
            AutomaticUpdateDiscovery.Attach(raw);
            var recipe=AutomaticUpdateDiscovery.Resolve(app)!;
            check(recipe is not null && VerifiedUpdaters.PrimaryActionLabel(app)=="Atualizar","W18 contrato MSI incompleto no manifesto chega a UI com validacao de payload obrigatoria");
            check(!AutomaticUpdateDiscovery.IsApproved(recipe! with{Interactive=true}) && !AutomaticUpdateDiscovery.IsApproved(recipe! with{Arguments="/arbitrary"}),
                "W18 alteracao de modo ou argumentos nao transfere permissao");
            string unsigned=Path.Combine(f.Basis,"unsigned.msi");MsiPackageEvidence.CreateSynthetic(unsigned);
            Exception? failure=null;try{AutomaticUpdateDiscovery.VerifyFile(recipe!,unsigned);}catch(Exception ex){failure=ex;}
            check(failure is UpdateSignatureException,"W18 assinatura obrigatoria no novo MSI");
            var second=f.App("duplicate");
            check(!AutomaticUpdateDiscovery.Safe(recipe!,app,[app,second]),"W18 instalacao duplicada bloqueia novo contrato");
            string root=app.InstallLocation;app.InstallLocation+="-changed";
            check(AutomaticUpdateDiscovery.Resolve(app) is null && !AutomaticUpdateDiscovery.Safe(recipe!,app,[app]),"W18 mudanca depois da descoberta revoga vinculo");
            app.InstallLocation=root;
            AutomaticUpdateDiscovery.ExpireSynthetic(recipe!);
            check(AutomaticUpdateDiscovery.Resolve(app) is null && !AutomaticUpdateDiscovery.IsApproved(recipe!),"W18 permissao expirada nao autoriza");
            var expired=AutomaticUpdateDiscovery.SimulateIssued(app,local,plan);
            // Replace the test seal with an already-expired one, preserving identity of the scan.
            var expiredCopy=expired with{};AutomaticUpdateDiscovery.SealSynthetic(expiredCopy,[app],DateTimeOffset.UtcNow.AddSeconds(-1));
            AutomaticUpdateDiscovery.Attach(expiredCopy);
            check(AutomaticUpdateDiscovery.Resolve(app) is null,"W18 scan expirado nao reemite permissao");
            var advisory=new AutomaticDiscoveryResult([],[],new("fixture","ok",0),Assessments:
                new Dictionary<string,string>{{AutomaticAppInstallation.Binding(app),"manifest_architecture_mismatch"}});
            AutomaticUpdateDiscovery.SealSynthetic(advisory,[app]);AutomaticUpdateDiscovery.Attach(advisory);
            check(AutomaticUpdateDiscovery.AssessmentReason(app)=="manifest_architecture_mismatch" &&
                VerifiedUpdaters.UpdateAvailabilityText(app).Contains("arquitetura"),"W18 motivo preciso chega a linha consultiva");
            app.InstallLocation+="-new";
            check(AutomaticUpdateDiscovery.AssessmentReason(app)=="","W18 motivo antigo nao acompanha instalacao alterada");
        }
        NativeMsi(check);
        using(var f=new Work17CoverageProbe.Fixture())
        {
            var app=f.App("supervised",msi:false);var local=AutomaticAppInstallation.Read(app)!;
            var plan=Work18CoverageProbe.Select(Work18CoverageProbe.Cases.Single(c=>c.Id=="nsis_supervised_fallback"))!;
            AutomaticUpdateDiscovery.Attach(AutomaticUpdateDiscovery.SimulateIssued(app,local,plan));
            var recipe=AutomaticUpdateDiscovery.Resolve(app)!;
            check(VerifiedUpdaters.PrimaryActionLabel(app)=="Acompanhar" && recipe.Interactive && recipe.Arguments.Length==0,
                "W18 NSIS supervisionado sem flags silenciosas improvisadas");
            Exception? signature=null;try{AutomaticUpdateDiscovery.VerifyFile(recipe,Path.Combine(app.InstallLocation,"uninstall.exe"));}catch(Exception ex){signature=ex;}
            check(signature is UpdateSignatureException,"W18 assinatura obrigatoria no novo NSIS supervisionado");
            int launches=0;var blocked=await new UpdateLaunchGate().RunAsync(()=>Task.FromResult(false),()=>{launches++;return Task.FromResult(0);},()=>Task.FromResult(false));
            check(blocked.State==UpdateAttemptState.Blocked && launches==0,"W18 fechamento ou revalidacao recusado nao lanca");
            var gate=new UpdateLaunchGate();var pending=await gate.RunAsync(()=>Task.FromResult(true),()=>{launches++;return Task.FromResult(0);},()=>Task.FromResult(false));
            var repeated=await gate.RunAsync(()=>Task.FromResult(true),()=>{launches++;return Task.FromResult(0);},()=>Task.FromResult(true));
            check(pending.State==UpdateAttemptState.Indeterminate && repeated.State==UpdateAttemptState.AlreadyAttempted && launches==1,
                "W18 acompanhamento sem confirmacao permanece pendente e impede repeticao");
        }
        var store=new ProgramEntry{Name="Outlook for Windows",Publisher="Microsoft Corporation",IsUpdate=true};
        check(VerifiedUpdaters.PrimaryActionLabel(store)=="Ver na Store" && VerifiedUpdaters.Resolve(store) is null,"W18 Store nao herda execucao Win32");
        check(VerifiedUpdaters.PrimaryActionLabel(new(){IsUpdate=true,UpdateRequiresReview=true})=="Revisar","W18 revisao sem botao inativo");
        check(VerifiedUpdaters.PrimaryActionLabel(new(){IsUpdate=true})=="Como fazer","W18 consulta sem contrato conserva orientacao");
    }
    private static void NativeMsi(Action<bool,string> check)
    {
        string folder=Path.Combine(Path.GetTempPath(),"GF-W18-MSI-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            string file=Path.Combine(folder,"inert.msi");MsiPackageEvidence.CreateSynthetic(file);var payload=MsiPackageEvidence.Read(file);
            var before=payload with{Version="1.0.0"};
            var app=new AutomaticAppInstallation("",payload.Name,payload.Publisher,before.Version,"machine","x64",folder,"", "","msi",before.ProductCode);
            var installer=new AutomaticInstaller("GF.Msi",payload.Name,payload.Publisher,payload.Version,"msi","x64","machine",
                new("https://vendor.example/setup.msi"),new string('A',64),payload.ProductCode,payload.UpgradeCode,"/qn /norestart",ScopeFromPayload:true);
            MsiPackageEvidence.Validate(before,payload,installer,app);
            check(true,"W18 MSI contexto ausente no manifesto comprovado por Property e Summary nativos");
            foreach(var changed in new[]{payload with{Scope="user"},payload with{Scope="unknown"},payload with{Architecture="x86"},
                payload with{Publisher="Other vendor"},payload with{Name="Another product"},payload with{UpgradeCode=payload.ProductCode},
                payload with{ProductCode="{00000000-0000-0000-0000-000000000077}"},payload with{Version="1.0.0"},payload with{Services=true}})
            {
                bool denied=false;try{MsiPackageEvidence.Validate(before,changed,installer,app);}catch(InvalidDataException){denied=true;}
                check(denied,"W18 MSI contexto arquitetura fabricante familia produto versao ou servico divergente bloqueado");
            }
            bool family=false;try{MsiPackageEvidence.Validate(before,payload,installer with{UpgradeCode=payload.ProductCode},app);}catch(InvalidDataException){family=true;}
            check(family,"W18 familia do manifesto deve coincidir com payload");
            before=before with{ProductCode="{00000000-0000-0000-0000-000000000077}"};app=app with{ProductCode=before.ProductCode};
            MsiPackageEvidence.SyntheticInstalled=(_,_)=>before;
            bool missing=false;try{MsiPackageEvidence.RequireUpgrade(installer,app,file);}catch(InvalidDataException){missing=true;}
            check(missing,"W18 ProductCode alterado exige major upgrade tambem na rota de nome exato");
            MsiMajorUpgradeEvidence.AddSyntheticContract(file);
            MsiPackageEvidence.RequireUpgrade(installer,app,file);
            check(true,"W18 major upgrade comprovado por tabela e sequencia sem instalar");
        }
        finally{MsiPackageEvidence.SyntheticInstalled=null;Directory.Delete(folder,true);}
    }
}
