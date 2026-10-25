using System.Reflection;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Runtime.InteropServices;
using GerenciadorAplicativos;

// This same file is compiled against the immutable base and candidate. New
// APIs are accessed through reflection so their absence is measured as absence.
internal static class Work16CoverageProbe
{
    private const string Product = "{00000000-0000-0000-0000-000000000016}";
    internal static string Manifest(string type, bool interactive) => $"""
PackageIdentifier: GF.Synthetic
PackageVersion: 2.0.0
PackageName: GF Synthetic App
Publisher: GF Synthetic Tests
ManifestType: merged
ManifestVersion: 1.9.0
Installers:
- Architecture: x64
  Scope: user
  InstallerType: {type}
  InstallerUrl: https://vendor.example/releases/setup.exe
  InstallerSha256: {new string('A',64)}
  ProductCode: '{Product}'
  InstallModes:
  - {(interactive ? "interactive" : "silent")}
""";
    internal static void Run()
    {
        int silent = 0, supervised = 0, rejected = 0;
        var reference = new CatalogManifestReference("GF.Synthetic", "GF Synthetic App", "2.0.0", "", new string('A',64));
        foreach (string engine in new[] { "nullsoft", "inno", "msi" })
        foreach (bool interactive in new[] { false, true })
        {
            var selected = AutomaticPackageManifest.Select(Encoding.UTF8.GetBytes(Manifest(engine, interactive)), reference, "x64", "user", engine);
            if (selected is null) continue;
            if ((bool?)typeof(AutomaticInstaller).GetProperty("Interactive")?.GetValue(selected) == true) supervised++; else silent++;
        }
        string source = Manifest("inno", true);
        var negatives = new[] { source.Replace("x64", "arm64"), source.Replace("Scope: user", "Scope: machine"),
            source.Replace("InstallerType: inno", "InstallerType: burn"), source + "  InstallerSwitches:\n    Custom: powershell.exe -Command arbitrary\n",
            source + "  UpgradeBehavior: uninstallPrevious\n", source + "  Dependencies:\n    PackageDependencies:\n    - PackageIdentifier: Unreviewed.Dependency\n" };
        foreach (string negative in negatives)
            if (AutomaticPackageManifest.Select(Encoding.UTF8.GetBytes(negative), reference, "x64", "user", "inno") is null) rejected++;

        string root = Path.Combine(Path.GetTempPath(), "GF-W16-Coverage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int managerRows = 0, managerVersions = 0;
        try
        {
            string current = Path.Combine(root, "scoop", "apps", "editor", "current"); Directory.CreateDirectory(current);
            File.WriteAllText(Path.Combine(current,"manifest.json"), "{\"version\":\"1.0.0\"}");
            File.WriteAllText(Path.Combine(current,"install.json"), "{\"architecture\":\"64bit\"}");
            string lib = Path.Combine(root,"chocolatey","lib","editor.install"); Directory.CreateDirectory(lib);
            string spec = "<package><metadata><id>editor.install</id><version>1.0.0</version></metadata></package>";
            File.WriteAllText(Path.Combine(lib,"editor.install.nuspec"),spec);
            using (var zip = ZipFile.Open(Path.Combine(lib,"editor.install.nupkg"),ZipArchiveMode.Create))
            { using var writer=new StreamWriter(zip.CreateEntry("editor.install.nuspec").Open());writer.Write(spec); }
            var assembly = typeof(AutomaticPackageManifest).Assembly;
            foreach (var (typeName, folder, arguments) in new[] {
                ("ScoopDiscoveryProvider","scoop", new object[]{Path.Combine(root,"scoop"),"user"}),
                ("ChocolateyDiscoveryProvider","chocolatey", new object[]{Path.Combine(root,"chocolatey")}) })
            {
                var type = assembly.GetType("GerenciadorAplicativos." + typeName); if (type is null) continue;
                var instance = Activator.CreateInstance(type, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic, null, arguments, null)!;
                var queryType = assembly.GetType("GerenciadorAplicativos.ProviderQuery")!;
                var query = Activator.CreateInstance(queryType, [false]);
                var task = (Task)type.GetMethod("DiscoverAsync")!.Invoke(instance, [query, CancellationToken.None])!;
                task.GetAwaiter().GetResult();
                var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
                var rows = (System.Collections.IEnumerable)result.GetType().GetProperty("Installations")!.GetValue(result)!;
                foreach(var row in rows) { managerRows++; if ((string)row!.GetType().GetProperty("AvailableVersion")!.GetValue(row)! != "")managerVersions++; }
            }
        }
        finally { Directory.Delete(root,true); }
        var catalog = CatalogCoverage();
        Console.WriteLine("WORK16_UPDATES=" + JsonSerializer.Serialize(new {
            sample="synthetic authenticated-manifest decisions, not final payload eligibility", scenarios=6,
            silent_contracts=silent, supervised_contracts=supervised, negatives=6, negatives_rejected=rejected,
            managed_metadata_universe=2, managed_entries_inventoried=managerRows, manager_updates=managerVersions,
            pfn_universe=1, pfn_version_discoveries=catalog.Pfn, tracked_package_id_universe=1, tracked_package_id_lookups=catalog.Package,
            final_payloads_authorized=0, physical_computers=0 }));
    }
    private static (int Pfn, int Package) CatalogCoverage()
    {
        string root=Path.Combine(Path.GetTempPath(),"GF-W16-Catalog-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        if(sqlite3_open_v2(Path.Combine(root,"index.db"),out var db,6,IntPtr.Zero)!=0)throw new Exception("Fixture db unavailable");
        try
        {
            string sql="CREATE TABLE metadata(name,value);CREATE TABLE ids(id);CREATE TABLE names(name);CREATE TABLE versions(version);"+
                "CREATE TABLE channels(channel);CREATE TABLE norm_publishers(norm_publisher);CREATE TABLE norm_publishers_map(manifest,norm_publisher);"+
                "CREATE TABLE manifest(id,name,version,channel,pathpart,hash);CREATE TABLE pfns_map(manifest,pfn);CREATE TABLE pfns(pfn);CREATE TABLE pathparts(parent,pathpart);"+
                "INSERT INTO metadata VALUES('majorVersion','1'),('lastwritetime','"+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+"');"+
                "INSERT INTO ids VALUES('GF.Synthetic');INSERT INTO names VALUES('GF Synthetic App');INSERT INTO versions VALUES('2.0.0');"+
                "INSERT INTO channels VALUES('');INSERT INTO norm_publishers VALUES('gf synthetic tests');"+
                "INSERT INTO pfns VALUES('GF.Synthetic_abc123');INSERT INTO pfns_map VALUES(1,1);"+
                "INSERT INTO pathparts VALUES('','manifests'),('1','synthetic'),('2','hash');"+
                "INSERT INTO manifest VALUES(1,1,1,1,3,X'"+new string('A',64)+"');INSERT INTO norm_publishers_map VALUES(1,1);";
            int error=sqlite3_exec(db,sql,IntPtr.Zero,IntPtr.Zero,out var message);if(message!=IntPtr.Zero)sqlite3_free(message);
            if(error!=0)throw new Exception("Fixture db schema failed");
        }
        finally{sqlite3_close(db);}
        using var index=PackageCatalogIndex.OpenSynthetic(root);
        int Query(string method,string value)
        {
            var member=typeof(PackageCatalogIndex).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance);
            if(member is null)return 0;
            return ((IReadOnlyList<CatalogManifestReference>)member.Invoke(index,["GF Synthetic Tests",value,CancellationToken.None])!).Count;
        }
        int pfn=Query("FindForReviewByPackageFamily","GF.Synthetic_abc123");
        int package=Query("FindForReviewByPackageId","GF.Synthetic");
        if(Query("FindForReviewByPackageFamily","Other_abc123")!=0 || Query("FindForReviewByPackageId","Other.Edition")!=0)
            throw new Exception("Wrong family or package accepted");
        return(pfn,package);
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,IntPtr callback,IntPtr arg,out IntPtr error);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern void sqlite3_free(IntPtr pointer);
}
