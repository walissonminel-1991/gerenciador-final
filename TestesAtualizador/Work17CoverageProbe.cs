using System.Reflection;
using System.Text.Json;
using GerenciadorAplicativos;

// Identical probe compiled against #52 and WORK17. Inert PE files, cached MSI
// metadata and signer oracle are simulated. No installer is downloaded or run.
internal static class Work17CoverageProbe
{
    internal const string Code="{00000000-0000-0000-0000-000000000059}";
    internal const string Upgrade="{00000000-0000-0000-0000-000000000058}";
    internal const string Version="12.0.40664.0";
    internal sealed class Fixture : IDisposable
    {
        internal string Basis=Path.Combine(Path.GetTempPath(),"GF-W17-"+Guid.NewGuid().ToString("N"));
        internal MsiPackageEvidence.Metadata Metadata=new("TestesAtualizador","GF Tests",Version,Code,Upgrade,"x64",false,"machine");
        internal string? Signer="GF Tests";
        private readonly FieldInfo? cache=typeof(MsiPackageEvidence).GetField("SyntheticInstalled",BindingFlags.Static|BindingFlags.NonPublic);
        private readonly FieldInfo? signer=typeof(AutomaticAppInstallation).GetField("SyntheticSigner",BindingFlags.Static|BindingFlags.NonPublic);
        internal Fixture()
        {
            Directory.CreateDirectory(Basis);AutomaticAppInstallation.SyntheticBaseRoot=Basis;
            cache?.SetValue(null,(Func<string,string,MsiPackageEvidence.Metadata>)((_,_)=>Metadata));
            signer?.SetValue(null,(Func<string,string?>)(_=>Signer));
        }
        internal ProgramEntry App(string id,bool alias=false,string scope="machine",bool msi=true,string suffix="")
        {
            string root=Path.Combine(Basis,id);Directory.CreateDirectory(root);
            string binary=Path.Combine(root,"app.exe"),uninstaller=Path.Combine(root,"uninstall.exe");
            byte[] data=File.ReadAllBytes(typeof(Work17CoverageProbe).Assembly.Location);
            int pe=BitConverter.ToInt32(data,60);
            BitConverter.GetBytes((ushort)((BitConverter.ToUInt16(data,pe+22)|2)&~0x2000)).CopyTo(data,pe+22);
            File.WriteAllBytes(binary,data);
            if(!msi)
            {
                using var stream=File.Create(uninstaller);stream.Write(data);byte[] header=new byte[512];
                byte[] marker=[0xef,0xbe,0xad,0xde,0x4e,0x75,0x6c,0x6c,0x73,0x6f,0x66,0x74,0x49,0x6e,0x73,0x74];marker.CopyTo(header,4);
                BitConverter.GetBytes(100u).CopyTo(header,20);BitConverter.GetBytes(200u).CopyTo(header,24);stream.Write(header);
            }
            return new(){Name=(alias?"Editor localizado":"TestesAtualizador")+suffix,Publisher="GF Tests",Version=Version,
                InventoryId=(scope=="machine"?"LocalMachine|":"CurrentUser|")+"Registry64|"+id,
                InstallLocation=root,DisplayIcon=binary,UninstallString=msi?"msiexec.exe /X"+Code:"\""+uninstaller+"\"",
                MsiProductCode=msi?Code:"",IsUpdate=true,AvailableVersion="13.0.0.0"};
        }
        internal AutomaticAppInstallation? Read(ProgramEntry app,bool alias)
        {
            if(!alias)return AutomaticAppInstallation.Read(app);
            var method=typeof(AutomaticAppInstallation).GetMethod("ReadByCatalogIdentity",BindingFlags.Static|BindingFlags.NonPublic);
            return (AutomaticAppInstallation?)method?.Invoke(null,[app,"TestesAtualizador",null]);
        }
        public void Dispose(){cache?.SetValue(null,null);signer?.SetValue(null,null);AutomaticAppInstallation.SyntheticBaseRoot=null;Directory.Delete(Basis,true);}
    }
    internal static IReadOnlyList<object> Measure()
    {
        var rows=new List<object>();
        string[] cases=["msi_machine","msi_user","nsis_machine","alias_machine","alias_user","alias_icon_root",
            "wrong_product","wrong_publisher","wrong_version","wrong_scope","wrong_arch","wrong_code","wrong_upgrade",
            "services","unsigned","divergent_signer","edition","preview","shared_root","duplicate_product","stale_cache","missing_binary"];
        foreach(string id in cases)
        {
            using var f=new Fixture();bool positive=Array.IndexOf(cases,id)<6;
            bool alias=id.StartsWith("alias",StringComparison.Ordinal)||!positive;
            string scope=id is "msi_user" or "alias_user"?"user":"machine";
            var app=f.App(id,alias,scope,id!="nsis_machine",id=="wrong_arch"?" (x86)":id=="edition"?" Enterprise":id=="preview"?" Preview":"");f.Metadata=f.Metadata with{Scope=scope};
            switch(id)
            {
                case "alias_icon_root":app.InstallLocation="";break;
                case "wrong_product":f.Metadata=f.Metadata with{Name="Other product"};break;
                case "wrong_publisher":f.Metadata=f.Metadata with{Publisher="Other vendor"};break;
                case "wrong_version":f.Metadata=f.Metadata with{Version="1.0"};break;
                case "wrong_scope":f.Metadata=f.Metadata with{Scope="user"};break;
                case "wrong_code":f.Metadata=f.Metadata with{ProductCode="{00000000-0000-0000-0000-000000000060}"};break;
                case "wrong_upgrade":f.Metadata=f.Metadata with{UpgradeCode=""};break;
                case "services":f.Metadata=f.Metadata with{Services=true};break;
                case "unsigned":f.Signer=null;break;
                case "divergent_signer":f.Signer="Other vendor";break;
                case "missing_binary":File.Delete(app.DisplayIcon);break;
            }
            var local=f.Read(app,alias);var inventory=new List<ProgramEntry>{app};
            if(id=="shared_root")inventory.Add(new(){Name="Unrelated component",Publisher="Other",InventoryId="LocalMachine|Registry64|shared",InstallLocation=app.InstallLocation});
            if(id=="duplicate_product")inventory.Add(f.App("second",false));
            if(id=="stale_cache")f.Metadata=f.Metadata with{UpgradeCode="{00000000-0000-0000-0000-000000000057}"};
            bool eligible=false;
            if(local is not null && local.Current(inventory))
            {
                var target=new AutomaticInstaller("GF.Fixture","TestesAtualizador",app.Publisher,app.AvailableVersion,local.Kind,
                    local.Architecture,scope,new("https://vendor.example/setup.msi"),new string('A',64),Code,Upgrade,"/qn /norestart");
                AutomaticUpdateDiscovery.Attach(AutomaticUpdateDiscovery.SimulateIssued(app,local,target));
                var recipe=AutomaticUpdateDiscovery.Resolve(app);
                eligible=recipe is not null && VerifiedUpdaters.SingleSafeSilentInstallation(recipe,app,inventory);
            }
            rows.Add(new{id,expected_eligible=positive,identity_corroborated=local is not null,eligible_before_payload=eligible,payload_authorized=false});
        }
        return rows;
    }
    internal static void Run()=>Console.WriteLine("WORK17_IDENTITY="+JsonSerializer.Serialize(Measure()));
}
