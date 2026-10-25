using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GerenciadorAplicativos;

internal static class Work16SquirrelProbe
{
    internal static void Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"GF-W16-Squirrel-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var type=typeof(SilentUninstallPolicy).Assembly.GetType("GerenciadorAplicativos.SquirrelUninstallEvidence");
        try
        {
            string appRoot=Path.Combine(root,"SyntheticSquirrel"), version=Path.Combine(appRoot,"app-3.6.1"), packages=Path.Combine(appRoot,"packages");
            Directory.CreateDirectory(version);Directory.CreateDirectory(packages);
            string updater=Path.Combine(appRoot,"Update.exe"), binary=Path.Combine(version,"SumatraPDF.exe");
            File.Copy(Path.Combine(AppContext.BaseDirectory,"SyntheticSquirrel","Update.dll"),updater);
            File.Copy(Path.Combine(AppContext.BaseDirectory,"SyntheticSilent","GF-SilentFixture.exe"),binary);
            string package=Path.Combine(packages,"SyntheticSquirrel-3.6.1-full.nupkg");
            using(var zip=ZipFile.Open(package,ZipArchiveMode.Create))
            {using var text=new StreamWriter(zip.CreateEntry("SyntheticSquirrel.nuspec").Open());text.Write("<package><metadata><id>SyntheticSquirrel</id><version>3.6.1</version><title>SumatraPDF</title></metadata></package>");}
            using(var input=File.OpenRead(package))File.WriteAllText(Path.Combine(packages,"RELEASES"),Convert.ToHexString(SHA1.HashData(input))+" "+Path.GetFileName(package)+" "+input.Length);
            type?.GetField("SyntheticRoot",BindingFlags.NonPublic|BindingFlags.Static)?.SetValue(null,root);
            Func<string,string?> signer=file=>file.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase)?"GF Synthetic Tests":null;
            type?.GetField("SyntheticSigner",BindingFlags.NonPublic|BindingFlags.Static)?.SetValue(null,signer);
            ProgramEntry App(string inventory="CurrentUser|Registry64|SyntheticSquirrel",string name="SumatraPDF",string publisher="GF Synthetic Tests",string versionNumber="3.6.1",string arguments="--uninstall") =>
                new(){Name=name,Publisher=publisher,Version=versionNumber,InventoryId=inventory,InstallLocation=appRoot,UninstallString="\""+updater+"\" "+arguments,CanUninstall=true};
            bool Silent(ProgramEntry app) { var request=UninstallBatch.Select([app]).Single();return SilentUninstallPolicy.UsesSilentMode(request); }
            bool selected=Silent(App());
            int refused=0;
            foreach(var app in new[]{App(inventory:"LocalMachine|Registry64|SyntheticSquirrel"),App(name:"SumatraPDF Pro"),
                App(publisher:"Different Vendor"),App(versionNumber:"3.6.2"),App(arguments:"--uninstall --processStart unsafe.exe")})
                if(!Silent(app))refused++;
            File.WriteAllText(Path.Combine(packages,"RELEASES"),new string('A',40)+" "+Path.GetFileName(package)+" "+new FileInfo(package).Length);
            if(!Silent(App()))refused++;
            Console.WriteLine("WORK16_UNINSTALL="+JsonSerializer.Serialize(new{sample="synthetic Squirrel layout, simulated signer, no uninstaller executed",scenarios=1,
                silent_contracts=selected?1:0,interactive_contracts=selected?0:1,negatives=6,negatives_rejected=refused,
                physical_computers=0,real_signatures=0}));
        }
        finally
        {
            type?.GetField("SyntheticRoot",BindingFlags.NonPublic|BindingFlags.Static)?.SetValue(null,null);
            type?.GetField("SyntheticSigner",BindingFlags.NonPublic|BindingFlags.Static)?.SetValue(null,null);
            Directory.Delete(root,true);
        }
    }
}
