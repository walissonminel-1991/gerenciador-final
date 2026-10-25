using System.Runtime.InteropServices;
using GerenciadorAplicativos;

internal static class Work14CatalogTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (bool ambiguous in new[] { false, true })
        {
            string root = Path.Combine(Path.GetTempPath(), "GF-W14-Index-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string file = Path.Combine(root,"index.db");
            if (sqlite3_open_v2(file,out var db,6,IntPtr.Zero) != 0) throw new Exception("Synthetic SQLite unavailable");
            try
            {
                string sql = "CREATE TABLE metadata(name,value);CREATE TABLE ids(id);CREATE TABLE names(name);CREATE TABLE versions(version);" +
                    "CREATE TABLE channels(channel);CREATE TABLE norm_publishers(norm_publisher);CREATE TABLE norm_publishers_map(manifest,norm_publisher);" +
                    "CREATE TABLE manifest(id,name,version,channel,pathpart,hash);CREATE TABLE productcodes_map(manifest,productcode);CREATE TABLE productcodes(productcode);CREATE TABLE pathparts(parent,pathpart);" +
                    "INSERT INTO metadata VALUES('majorVersion','1'),('lastwritetime','" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + "');" +
                    "INSERT INTO ids VALUES('GF.Synthetic');INSERT INTO names VALUES('GF Synthetic App');INSERT INTO versions VALUES('2.0.0');" +
                    "INSERT INTO channels VALUES('');INSERT INTO norm_publishers VALUES('gf test vendor');" +
                    "INSERT INTO productcodes VALUES('{00000000-0000-0000-0000-000000000059}');" +
                    "INSERT INTO pathparts VALUES('','manifests'),('1','synthetic'),('2','hash');" +
                    "INSERT INTO manifest VALUES(1,1,1,1,3,X'" + new string('A',64) + "');INSERT INTO norm_publishers_map VALUES(1,1);INSERT INTO productcodes_map VALUES(1,1);";
                if (ambiguous) sql += "INSERT INTO ids VALUES('Other.Synthetic');INSERT INTO manifest VALUES(2,1,1,1,3,X'"+new string('A',64)+"');INSERT INTO norm_publishers_map VALUES(2,1);INSERT INTO productcodes_map VALUES(2,1);";
                int error = sqlite3_exec(db,sql,IntPtr.Zero,IntPtr.Zero,out var message);
                if (message != IntPtr.Zero) sqlite3_free(message);
                if (error != 0) throw new Exception("Synthetic schema failed: " + error);
            }
            finally { sqlite3_close(db); }
            using var index = PackageCatalogIndex.OpenSynthetic(root);
            var rows = index.Find("GF Synthetic App 1.0.0 (64-bit x64)","GF Test Vendor","",default);
            check(ambiguous ? rows.Count == 0 : rows is [{Id:"GF.Synthetic",Path:"manifests/synthetic/hash"}],
                ambiguous ? "W14 indice real recusa identificadores ambiguos" : "W14 consulta SQLite real encontra identidade ARP decorada sem permissao por nome");
            foreach (string name in new[] { "GF Synthetic App++", "GF Synthetic App Preview", "GF Synthetic App Pro", "GF Synthetic App Server" })
                check(index.Find(name,"GF Test Vendor","{00000000-0000-0000-0000-000000000059}",default).Count == 0,
                    "W14 ProductCode nao contorna divergencia de produto ou canal no indice");
            check(index.Find("GF Synthetic App","Wrong Vendor","",default).Count == 0,
                "W14 indice recusa fornecedor divergente");
            const string msi = "{00000000-0000-0000-0000-000000000059}";
            check(index.Find("Another Display Name", "GF Test Vendor", msi, default).Count == 0,
                "W15 ProductCode nunca enfraquece o contrato executavel por nome");
            var advisory = index.FindForReviewByProductCode("GF Test Vendor", msi, default);
            check(ambiguous ? advisory.Count == 0 : advisory is [{ Id:"GF.Synthetic" }],
                "W15 MSI ProductCode fornece somente correspondencia consultiva unica");
            check(index.FindForReviewByProductCode("Wrong Vendor",msi,default).Count==0 &&
                index.FindForReviewByProductCode("GF Test Vendor","not-a-guid",default).Count==0,
                "W15 fornecedor incorreto ou codigo nao MSI nao recebe correspondencia consultiva");
            check(AutomaticUpdateDiscovery.NewestCandidate(advisory,"1.0.0") is {Version:"2.0.0"} == !ambiguous &&
                AutomaticUpdateDiscovery.NewestCandidate(advisory,"3.0.0") is null &&
                AutomaticUpdateDiscovery.NewestCandidate(advisory,"not-comparable") is null,
                "W15 consulta publica oferece somente versao comprovadamente superior");
            var consultative = new OfficialRelease(
                new InstalledApp("Another Display Name","1.0.0","GF Test Vendor"), "2.0.0", "Catálogo Microsoft (consulta)",
                "https://github.com/microsoft/winget-pkgs", EvidenceKind:"catalog-version-only");
            var merged = MultiSourceMatcher.Merge([consultative.Installed],null,[consultative]);
            check(merged.Count==1 && merged[0].RequiresReview && merged[0].State==MatchState.Candidate,
                "W15 correspondencia consultiva fica em revisao, nao autorizada");
        }
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string file,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]
    private static extern int sqlite3_exec(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,IntPtr callback,IntPtr state,out IntPtr error);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] private static extern void sqlite3_free(IntPtr value);
}
