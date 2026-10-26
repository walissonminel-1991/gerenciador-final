using System.Reflection;
using System.Text;
using System.Text.Json;
using GerenciadorAplicativos;

// The identical probe runs against immutable .64 and WORK18. Only inert PE,
// MSI cache and signer fixtures are used. Eligibility is BEFORE payload checks.
internal static class Work18CoverageProbe
{
    internal sealed record Case(string Id,string Type,string Scope="machine",bool OmitScope=false,
        string Modes="",string Switches="",string Arch="x64",string Extra="",string Expected="none");
    internal static readonly Case[] Cases =
    [
        new("control_nsis","nullsoft",Expected:"Atualizar"),
        new("control_inno","inno",Expected:"Atualizar"),
        new("control_msi","msi",Expected:"Atualizar"),
        new("control_wix","wix",Expected:"Atualizar"),
        new("control_interactive","nullsoft",Modes:"interactive",Expected:"Acompanhar"),
        new("msi_scope_machine","msi",OmitScope:true,Expected:"Atualizar"),
        new("msi_scope_user","msi",Scope:"user",OmitScope:true,Expected:"Atualizar"),
        new("wix_scope_machine","wix",OmitScope:true,Expected:"Atualizar"),
        new("wix_scope_user","wix",Scope:"user",OmitScope:true,Expected:"Atualizar"),
        new("nsis_supervised_fallback","nullsoft",Modes:"both",Switches:"silent_vendor",Expected:"Acompanhar"),
        new("inno_supervised_fallback","inno",Modes:"both",Switches:"silent_vendor",Expected:"Acompanhar"),
        new("msi_supervised_fallback","msi",Modes:"both",Switches:"silent_vendor",Expected:"Acompanhar"),
        new("wix_supervised_fallback","wix",Modes:"both",Switches:"silent_vendor",Expected:"Acompanhar"),
        new("wrong_architecture","msi",OmitScope:true,Arch:"x86"),
        new("explicit_wrong_scope","msi",Scope:"other"),
        new("nsis_scope_missing","nullsoft",OmitScope:true),
        new("inno_scope_missing","inno",OmitScope:true),
        new("unsupported_silent_only","inno",Modes:"silent",Switches:"silent_vendor"),
        new("unsupported_silent_unspecified","nullsoft",Switches:"silent_vendor"),
        new("custom_command","nullsoft",Modes:"both",Switches:"custom"),
        new("custom_inno","inno",Modes:"both",Switches:"custom"),
        new("location_command","msi",Modes:"both",Switches:"location"),
        new("repair_command","inno",Modes:"both",Switches:"repair"),
        new("dependencies","msi",OmitScope:true,Extra:"  Dependencies:\n    PackageDependencies:\n    - PackageIdentifier: Other.Product\n"),
        new("remove_previous","msi",OmitScope:true,Extra:"  UpgradeBehavior: uninstallPrevious\n"),
        new("ambiguous_installer","msi",OmitScope:true),
        new("http","msi",OmitScope:true),
        new("hash_missing","msi",OmitScope:true)
    ];
    internal static string Manifest(Case c)
    {
        string modes=c.Modes switch {"both"=>"  InstallModes: [interactive, silent]\n",""=>"",_=>"  InstallModes: ["+c.Modes+"]\n"};
        string switches=c.Switches switch
        {
            "silent_vendor"=>"  InstallerSwitches:\n    Silent: /UNVERIFIED_VENDOR_FLAG\n",
            "custom"=>"  InstallerSwitches:\n    Custom: powershell.exe -Command arbitrary\n",
            "location"=>"  InstallerSwitches:\n    InstallLocation: /D=<INSTALLPATH>\n",
            "repair"=>"  InstallerSwitches:\n    Repair: /REPAIR\n",_=>""
        };
        string option="- InstallerType: "+c.Type+"\n  Architecture: "+c.Arch+"\n"+
            (c.OmitScope?"":"  Scope: "+c.Scope+"\n")+modes+switches+
            (c.Type is "msi" or "wix"?"  ProductCode: '"+Work17CoverageProbe.Code+"'\n":"")+
            "  InstallerUrl: https://vendor.example/setup.exe\n  InstallerSha256: "+new string('A',64)+"\n"+c.Extra;
        string text="PackageIdentifier: GF.Work18\nPackageVersion: 13.0.0.0\nPackageName: TestesAtualizador\nPublisher: GF Tests\nManifestType: merged\nManifestVersion: 1.10.0\nInstallers:\n"+option;
        if(c.Id=="ambiguous_installer")text+=option.Replace("setup.exe","other.exe");
        if(c.Id=="http")text=text.Replace("https://","http://");
        if(c.Id=="hash_missing")text=text.Replace(new string('A',64),"invalid");
        return text;
    }
    internal static AutomaticInstaller? Select(Case c)=>AutomaticPackageManifest.Select(Encoding.UTF8.GetBytes(Manifest(c)),
        new("GF.Work18","TestesAtualizador","13.0.0.0","",new string('A',64)),"x64",c.Scope=="user"?"user":"machine",c.Type=="wix"?"msi":c.Type);
    internal static void MakeInno(string file)
    {
        byte[] data=File.ReadAllBytes(file); // existing inert NSIS fixture, keep only its PE sections
        using(var pe=new System.Reflection.PortableExecutable.PEReader(new MemoryStream(data)))
            Array.Resize(ref data,(int)pe.PEHeaders.SectionHeaders.Max(s=>(long)s.PointerToRawData+s.SizeOfRawData));
        var suffix=new byte[512];
        byte[] magic=[0x72,0x44,0x6c,0x50,0x74,0x53,0xcd,0xe6,0xd7,0x7b,0x0b,0x2a];magic.CopyTo(suffix,0);
        BitConverter.GetBytes(2u).CopyTo(suffix,12);BitConverter.GetBytes((long)data.Length+512).CopyTo(suffix,16);
        BitConverter.GetBytes((long)data.Length+320).CopyTo(suffix,24);BitConverter.GetBytes(1000u).CopyTo(suffix,32);
        BitConverter.GetBytes((long)data.Length+256).CopyTo(suffix,40);BitConverter.GetBytes((long)data.Length).CopyTo(suffix,48);
        BitConverter.GetBytes(InstallerEngineEvidence.InnoTableCrc(suffix.AsSpan(0,60))).CopyTo(suffix,60);
        "Inno Setup Setup Data (6.7.0)"u8.CopyTo(suffix.AsSpan(256));
        File.WriteAllBytes(file,data.Concat(suffix).ToArray());
    }
    internal static IReadOnlyList<object> Measure()
    {
        var rows=new List<object>();
        foreach(var c in Cases)
        {
            using var f=new Work17CoverageProbe.Fixture();
            var app=f.App("work18_"+c.Id,scope:c.Scope=="user"?"user":"machine",msi:c.Type is "msi" or "wix");
            if(c.Type=="inno")MakeInno(Path.Combine(app.InstallLocation,"uninstall.exe"));
            var local=AutomaticAppInstallation.Read(app);var plan=Select(c);string action="none";
            if(plan is not null && local is not null && local.Current([app]))
            {
                AutomaticUpdateDiscovery.Attach(AutomaticUpdateDiscovery.SimulateIssued(app,local,plan));
                var recipe=AutomaticUpdateDiscovery.Resolve(app);
                if(recipe is not null && VerifiedUpdaters.GetPrimaryAction(app)=="update" &&
                    VerifiedUpdaters.SingleSafeSilentInstallation(recipe,app,[app]))
                    action=recipe.Interactive?"Acompanhar":"Atualizar";
            }
            rows.Add(new{id=c.Id,expected_action=c.Expected,action,contract_selected=plan is not null,
                installation_corroborated=local is not null,payload_authorized=false});
        }
        return rows;
    }
    internal static void Run()=>Console.WriteLine("WORK18_COVERAGE="+JsonSerializer.Serialize(Measure()));
}
