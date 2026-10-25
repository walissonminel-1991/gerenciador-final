using System.Net;
using System.Text;
using GerenciadorAplicativos;

internal static class AutomaticDiscoveryTests
{
    private static readonly CatalogManifestReference Reference=new("Synthetic.Editor","GF Synthetic App","2.0.0","manifests/s/Synthetic/Editor/2.0.0/hash",new string('A',64));
    internal static string Manifest(string type="nullsoft",string scope="machine",string arch="x64",string extra="")=>
        "PackageIdentifier: Synthetic.Editor\nPackageVersion: 2.0.0\nPackageName: GF Synthetic App\nPublisher: GF Test Vendor\nManifestType: merged\nManifestVersion: 1.10.0\n"+
        "Installers:\n- InstallerType: "+type+"\n  Architecture: "+arch+"\n  Scope: "+scope+"\n  InstallerUrl: https://vendor.example/releases/setup.exe\n  InstallerSha256: "+new string('A',64)+"\n"+extra;
    internal static AutomaticInstaller? Select(string text,string kind="nullsoft")=>AutomaticPackageManifest.Select(Encoding.UTF8.GetBytes(text),Reference,"x64","machine",kind);
    internal static void Run(Action<bool,string> check)
    {
        var installer=Select(Manifest())!;
        check(installer is {Type:"nullsoft",Arguments:"/S"},"AUTO pacote desconhecido gera instrucao NSIS sem regra de aplicativo ou versao no codigo");
        check(Select(Manifest("inno"),"inno") is {Arguments:var flags} && flags.Contains("/NOCLOSEAPPLICATIONS") && flags.Contains("/NORESTART"),"AUTO Inno silencioso preserva fechamento e reinicio sob controle");
        check(Select(Manifest("msi",extra:"  ProductCode: '{00000000-0000-0000-0000-000000000059}'\n"),"msi") is {Arguments:"/qn /norestart"},"AUTO MSI exige ProductCode e parametros locais conhecidos");
        check(Select(Manifest("msi",extra:"  ProductCode: '{00000000-0000-0000-0000-000000000000}'\n"),"msi") is null,"AUTO ProductCode MSI vazio nao identifica produto");
        foreach(var text in new[]{Manifest(arch:"x86"),Manifest(arch:"arm64"),Manifest(scope:"user"),Manifest(type:"zip"),Manifest(type:"exe"),Manifest(type:"msix")})
            check(Select(text) is null,"AUTO arquitetura escopo ou motor diferente nao autoriza");
        foreach(string bad in new[]{"  Dependencies:\n    PackageDependencies:\n    - PackageIdentifier: Unsafe.Dependency\n","  UpgradeBehavior: uninstallPrevious\n",
            "  InstallerSwitches:\n    Custom: powershell.exe -Command arbitrary\n","  InstallerSwitches:\n    Silent: /S /DELETE\n",
            "  AppsAndFeaturesEntries:\n  - ProductCode: First\n  - ProductCode: Second\n"})
            check(Select(Manifest(extra:bad)) is null,"AUTO declaracao adicional insegura ou ambigua exige revisao");
        foreach(string url in new[]{"http://vendor.example/a","https://localhost/a","https://127.0.0.1/a","https://[::1]/a","https://user:password@vendor.example/a","https://vendor.example:444/a","https://device.local/a"})
            check(Select(Manifest().Replace("https://vendor.example/releases/setup.exe",url)) is null,"AUTO endereco nao autorizado recusado: "+url);
        foreach(string ip in new[]{"127.0.0.1","10.0.0.1","172.16.0.1","192.168.1.1","169.254.1.1","100.64.0.1","::1","fe80::1","fc00::1","::ffff:127.0.0.1"})
            check(!AutomaticUpdateDiscovery.PublicAddress(IPAddress.Parse(ip)),"AUTO DNS privado nao permite conexao: "+ip);
        check(AutomaticUpdateDiscovery.PublicAddress(IPAddress.Parse("169.229.200.70")),"AUTO espelho publico 169.229 nao pertence ao link-local 169.254/16");
        check(!AutomaticUpdateDiscovery.PublicAddress(IPAddress.Parse("169.254.255.254")),"AUTO link-local continua bloqueado apos corrigir faixa publica");
        check(AutomaticUpdateDiscovery.PublicAddress(IPAddress.Parse("1.1.1.1")),"AUTO endereco publico tem controle positivo");
        foreach(string text in new[]{Manifest().Replace("Synthetic.Editor","Another.Product"),Manifest().Replace("PackageVersion: 2.0.0","PackageVersion: 3.0.0"),
            "x: &shared value\ny: *shared\n"+Manifest(),Manifest()+"---\nfoo: bar\n",Manifest()+"PackageIdentifier: Duplicate\n"})
        {
            bool denied=false;try{Select(text);}catch(Exception ex)when(ex is InvalidDataException or YamlDotNet.Core.YamlException or ArgumentException){denied=true;}
            check(denied,"AUTO YAML identidade trocada aliases documentos e duplicatas rejeitados");
        }
        foreach(string name in new[]{"Microsoft GameInput","Contoso Server","Microsoft Visual C++ Redistributable","Windows Core","Device Driver","Runtime Shared"})
            check(!AutomaticAppInstallation.Eligible(new(){Name=name,Publisher="GF",InventoryId="LocalMachine|Registry64|Synthetic"}),"AUTO dependencia ou pacote protegido nao entra na descoberta: "+name);
        var app=new ProgramEntry{Name="GF Synthetic App",Publisher="GF Test Vendor",Version="1.0.0",AvailableVersion="2.0.0",IsUpdate=true,
            InventoryId="LocalMachine|Registry64|GF-Synthetic",InstallLocation=@"C:\GF-Synthetic",UninstallString="\"C:\\GF-Synthetic\\uninstall.exe\""};
        var installation=new AutomaticAppInstallation(AutomaticAppInstallation.Binding(app),app.Name,app.Publisher,app.Version,"machine","x64",app.InstallLocation,
            @"C:\GF-Synthetic\app.exe",@"C:\GF-Synthetic\uninstall.exe","nullsoft","");
        AutomaticUpdateDiscovery.Attach(new([],[(app,installation,installer)],new("forged","ok",0)));
        check(AutomaticUpdateDiscovery.Resolve(app) is null,"AUTO dados ou catalogo forjado nao emitem permissao de execucao");
        var issued=AutomaticUpdateDiscovery.SimulateIssued(app,installation,installer);
        AutomaticUpdateDiscovery.Attach(issued);var recipe=AutomaticUpdateDiscovery.Resolve(app)!;
        check(recipe is not null && VerifiedUpdaters.IsApprovedRecipe(recipe),"AUTO autoridade emitida pela consulta vincula receita a instalacao e alvo");
        foreach(var changed in new[]{recipe! with{},recipe! with{Arguments="/DELETE"},recipe! with{Download=new Uri("https://evil.example/setup.exe")},recipe! with{Sha256=new string('0',64)}})
            check(!VerifiedUpdaters.IsApprovedRecipe(changed),"AUTO copia ou adulteracao de receita nao transfere autoridade");
        check(AutomaticUpdateDiscovery.Safe(recipe!,app,[])==false,"AUTO inventario posterior ausente nao autoriza instalacao");
        AutomaticUpdateDiscovery.Reserve(recipe!);
        bool duplicate=false;try{AutomaticUpdateDiscovery.Reserve(recipe!);}catch(InvalidOperationException){duplicate=true;}
        check(duplicate && AutomaticUpdateDiscovery.Resolve(app) is null,"AUTO intencao ja enviada nao pode executar de novo mesmo com nova consulta");
        AutomaticUpdateDiscovery.ReleaseBeforeStart(recipe!);
        check(AutomaticUpdateDiscovery.Resolve(app) is not null,"AUTO UAC recusado antes de iniciar pode liberar preparacao posterior");
        string oldCode="{00000000-0000-0000-0000-000000000001}",newCode="{00000000-0000-0000-0000-000000000002}",upgradeCode="{00000000-0000-0000-0000-000000000003}";
        var oldMsi=new MsiPackageEvidence.Metadata(app.Name,app.Publisher,app.Version,oldCode,upgradeCode,"x64",false,"machine");
        var newMsi=oldMsi with{Version=installer.Version,ProductCode=newCode};
        var msiPlan=installer with{Type="msi",ProductCode=newCode,UpgradeCode=upgradeCode};
        var msiApp=installation with{Kind="msi",ProductCode=oldCode};
        MsiPackageEvidence.Validate(oldMsi,newMsi,msiPlan,msiApp);
        check(true,"AUTO MSI familia de produto confirmada com ProductCode migrado");
        foreach(var changed in new[]{newMsi with{Scope="user"},newMsi with{Architecture="x86"},newMsi with{Services=true},
            newMsi with{UpgradeCode=newCode},newMsi with{ProductCode=oldCode},newMsi with{Name="Another"},newMsi with{Publisher="Other"},newMsi with{Version="1.0.0"}})
        {
            bool denied=false;try{MsiPackageEvidence.Validate(oldMsi,changed,msiPlan,msiApp);}catch(InvalidDataException){denied=true;}
            check(denied,"AUTO MSI adulterado compartilhado ou divergente e recusado");
        }
        check(InstallerEngineEvidence.SameProduct("Example++ 2.0.0 (64-bit x64)","Example++"),"W14 identidade comum admite decoracoes ARP conhecidas");
        check(!InstallerEngineEvidence.SameProduct("Example++","Example") && !InstallerEngineEvidence.SameProduct("Example Preview","Example"),
            "W14 identidade comum conserva simbolo de produto e canal");
        var decoratedMsi = msiApp with { Name = app.Name + " 1.0.0 (64-bit x64)" };
        MsiPackageEvidence.Validate(oldMsi, newMsi, msiPlan, decoratedMsi);
        check(true, "W14 MSI metadados usam a mesma identidade ARP da descoberta");
        bool collisionDenied=false;
        try { MsiPackageEvidence.Validate(oldMsi,newMsi with { Name = app.Name + "++" },msiPlan,msiApp); }
        catch(InvalidDataException) { collisionDenied=true; }
        check(collisionDenied,"W14 MSI produto com simbolo distinto nao passa por normalizacao consultiva");
        if(OperatingSystem.IsWindows())NativeFiles(check);
        var header=new byte[64];byte[] marker=[0xef,0xbe,0xad,0xde,0x4e,0x75,0x6c,0x6c,0x73,0x6f,0x66,0x74,0x49,0x6e,0x73,0x74];marker.CopyTo(header,4);
        BitConverter.GetBytes(100u).CopyTo(header,20);BitConverter.GetBytes(200u).CopyTo(header,24);
        check(InstallerEngineEvidence.HasNsisHeader(header,1000),"AUTO estrutura NSIS valida reconhecida em bytes sinteticos");
        header[0]=0xff;check(!InstallerEngineEvidence.HasNsisHeader(header,1000),"AUTO string NSIS com estrutura falsa rejeitada");
        byte[] table=InnoTable(1000,700,800,600);
        check(InstallerEngineEvidence.TryInnoTable(table,1000,500,out long offset) && offset==700,"AUTO Inno tabela binaria oficial v2 e offsets reconhecidos");
        byte[] observed=Convert.FromHexString("72446c507453cde6d77b0b2a0200000067474b0800000000893f39080000000000884300ab95f2c4a5d637080000000000a40d0000000000000000003a7683af");
        check(InstallerEngineEvidence.TryInnoTable(observed,139151207,893952,out long realOffset) && realOffset==137877157,"AUTO Inno tabela observada no ShareX oficial confere CRC independente");
        table[60]^=1;check(!InstallerEngineEvidence.TryInnoTable(table,1000,500,out _),"AUTO Inno CRC adulterado nao comprova motor");
        check(!InstallerEngineEvidence.TryInnoTable(InnoTable(1000,1100,1200,600),1000,500,out _),"AUTO Inno offsets fora do arquivo recusados");
        check(!InstallerEngineEvidence.TryInnoTable("rDlPtS02 Inno Setup Setup Data (6.4.3)"u8,1000,500,out _),"AUTO Inno strings isoladas nao comprovam motor");
    }
    private static byte[] InnoTable(long total,long data,long exe,long files)
    {
        byte[] table=new byte[64];byte[] magic=[0x72,0x44,0x6c,0x50,0x74,0x53,0xcd,0xe6,0xd7,0x7b,0x0b,0x2a];magic.CopyTo(table,0);
        BitConverter.GetBytes(2u).CopyTo(table,12);BitConverter.GetBytes(total).CopyTo(table,16);BitConverter.GetBytes(exe).CopyTo(table,24);
        BitConverter.GetBytes(1000u).CopyTo(table,32);BitConverter.GetBytes(data).CopyTo(table,40);BitConverter.GetBytes(files).CopyTo(table,48);
        BitConverter.GetBytes(InstallerEngineEvidence.InnoTableCrc(table.AsSpan(0,60))).CopyTo(table,60);return table;
    }
    private static void NativeFiles(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"GF-Auto-Synthetic-"+Guid.NewGuid().ToString("N")),install=Path.Combine(root,"Application");
        Directory.CreateDirectory(install);AutomaticAppInstallation.SyntheticBaseRoot=root;
        try
        {
            string msi=Path.Combine(root,"synthetic.msi");MsiPackageEvidence.CreateSynthetic(msi);
            var metadata=MsiPackageEvidence.Read(msi);
            check(metadata is {Name:"GF Synthetic MSI",Version:"2.0.0",Scope:"machine",Architecture:"x64",Services:false},"AUTO APIs MSI leem pacote sintetico real sem instalar");
            string binary=Path.Combine(install,"program.exe"),uninstall=Path.Combine(install,"uninstall.exe");
            byte[] data=File.ReadAllBytes(typeof(AutomaticDiscoveryTests).Assembly.Location);
            int pe=BitConverter.ToInt32(data,60);ushort characteristics=BitConverter.ToUInt16(data,pe+22);
            BitConverter.GetBytes((ushort)((characteristics|2)&~0x2000)).CopyTo(data,pe+22);
            File.WriteAllBytes(binary,data);
            using(var stream=new FileStream(uninstall,FileMode.CreateNew,FileAccess.Write))
            {
                stream.Write(data);byte[] header=new byte[512];
                byte[] marker=[0xef,0xbe,0xad,0xde,0x4e,0x75,0x6c,0x6c,0x73,0x6f,0x66,0x74,0x49,0x6e,0x73,0x74];marker.CopyTo(header,4);
                BitConverter.GetBytes(100u).CopyTo(header,20);BitConverter.GetBytes(200u).CopyTo(header,24);stream.Write(header);
            }
            var app=new ProgramEntry{Name="TestesAtualizador",Publisher="GF Tests",Version="12.0.40664.0",InventoryId="LocalMachine|Registry64|Synthetic",
                InstallLocation=install,UninstallString="\""+uninstall+"\"",DisplayIcon=binary};
            var snapshot=AutomaticAppInstallation.Read(app);
            check(snapshot is {Architecture:"x64",Kind:"nullsoft"} && snapshot.Current([app]),"AUTO arquivo PE e escopo reais corroboram instalacao sintetica");
            const string registeredCode="{00000000-0000-0000-0000-000000000059}";
            ProgramEntry MsiApp(string code)=>new(){Name=app.Name,Publisher=app.Publisher,Version=app.Version,InventoryId=app.InventoryId,
                InstallLocation=install,DisplayIcon=binary,MsiProductCode=code,UninstallString="msiexec.exe /X"+registeredCode};
            var missingRoot=new ProgramEntry{Name=app.Name,Publisher=app.Publisher,Version=app.Version,InventoryId=app.InventoryId,
                DisplayIcon=binary, MsiProductCode=registeredCode,UninstallString="msiexec.exe /X"+registeredCode};
            check(AutomaticAppInstallation.Read(missingRoot) is {Kind:"msi",Root:var recovered} && recovered==install,
                "W14 MSI/WiX sem InstallLocation recupera raiz comprovada pelo executavel registrado");
            var decorated=new ProgramEntry{Name=app.Name+" 12.0.40664.0 (64-bit x64)",Publisher=app.Publisher,Version=app.Version,
                InventoryId=app.InventoryId,InstallLocation=install,UninstallString=app.UninstallString,DisplayIcon=binary};
            check(AutomaticAppInstallation.Read(decorated) is {Kind:"nullsoft"},
                "W14 NSIS com versao e arquitetura decoradas usa a identidade comum comprovada no PE");
            var badIcon=new ProgramEntry{Name=app.Name,Publisher=app.Publisher,Version=app.Version,InventoryId=app.InventoryId,
                DisplayIcon=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Installer","icon.exe"),
                MsiProductCode=registeredCode,UninstallString="msiexec.exe /X"+registeredCode};
            check(AutomaticAppInstallation.Read(badIcon) is null,"W14 MSI icone de cache Windows nao vira raiz de instalacao");
            check(AutomaticAppInstallation.Read(MsiApp(registeredCode)) is {Kind:"msi",ProductCode:registeredCode},"AUTO MSI comando e inventario identificam o mesmo produto");
            check(AutomaticAppInstallation.Read(MsiApp("{00000000-0000-0000-0000-000000000060}")) is null,"AUTO MSI comando de outro produto nao autoriza atualizacao");
            check(AutomaticAppInstallation.Read(MsiApp("")) is null,"AUTO MSI codigo ausente no inventario nao autoriza");
            var before=new ProgramEntry{Name=app.Name,Publisher=app.Publisher,Version="11.0.0",IsUpdate=true,AvailableVersion=app.Version,
                InventoryId=app.InventoryId,InstallLocation=install,DisplayIcon=binary,UninstallString=app.UninstallString};
            var after=new ProgramEntry{Name=app.Name,Publisher=app.Publisher.ToLowerInvariant(),Version=app.Version,
                InventoryId=app.InventoryId,InstallLocation=install,DisplayIcon=binary,UninstallString=app.UninstallString};
            var target=new AutomaticInstaller("Synthetic.Case",app.Name,app.Publisher,app.Version,"nullsoft","x64","machine",
                new Uri("https://vendor.example/setup.exe"),new string('A',64),"","","/S");
            var previous=snapshot! with{Key=AutomaticAppInstallation.Binding(before),Version=before.Version};
            AutomaticUpdateDiscovery.Attach(AutomaticUpdateDiscovery.SimulateIssued(before,previous,target));
            var caseRecipe=AutomaticUpdateDiscovery.Resolve(before)!;
            check(ReferenceEquals(AutomaticUpdateDiscovery.ConfirmedEntry(caseRecipe,[after]),after),"AUTO fabricante com caixa diferente preserva resultado confirmado na lista");
            var other=new ProgramEntry{Name=app.Name,Publisher=app.Publisher,Version=app.Version,InventoryId="LocalMachine|Registry64|Other"};
            check(!snapshot!.Current([app,other]),"AUTO duas instalacoes da familia nao autorizam uma escolha arbitraria");
            var shared=new ProgramEntry{Name="Other Product",Publisher="Other",InstallLocation=install,InventoryId="LocalMachine|Registry64|Shared"};
            check(!snapshot.Current([app,shared]),"AUTO pasta compartilhada com outro produto nao autoriza");
            var iconShared = new ProgramEntry { Name="Other Product", Publisher="Other", InventoryId="LocalMachine|Registry64|IconShared",
                DisplayIcon=Path.Combine(install,"other.exe") };
            check(!snapshot.Current([app,iconShared]),"W14 outro registro sem InstallLocation mas com icone na mesma pasta bloqueia atualizacao");
            byte[] x86=(byte[])data.Clone();BitConverter.GetBytes((ushort)0x14c).CopyTo(x86,pe+4);File.WriteAllBytes(binary,x86);
            check(!snapshot.Current([app]),"AUTO arquitetura alterada depois da descoberta bloqueia execucao");
            File.WriteAllBytes(binary,data);File.WriteAllBytes(uninstall,data);
            check(!snapshot.Current([app]),"AUTO desinstalador mudou de mecanismo depois da descoberta");
            using(var output=new FileStream(uninstall,FileMode.Create,FileAccess.Write))
            {
                output.Write(data);byte[] suffix=new byte[512];InnoTable(data.Length+512,data.Length+256,data.Length+320,data.Length).CopyTo(suffix,0);
                "Inno Setup Setup Data (6.7.0)"u8.CopyTo(suffix.AsSpan(256));output.Write(suffix);
            }
            check(InstallerEngineEvidence.Detect(uninstall)=="inno","AUTO Inno PE sintetico usa tabela e identidade no offset declarado");
            File.WriteAllBytes(binary,[]);
            check(AutomaticAppInstallation.Read(app) is null,"AUTO executavel ausente ou vazio nao comprova identidade");
        }
        finally{AutomaticAppInstallation.SyntheticBaseRoot=null;Directory.Delete(root,true);}
    }

}
