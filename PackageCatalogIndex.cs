using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace GerenciadorAplicativos;

internal sealed record CatalogManifestReference(string Id, string Name, string Version, string Path, string Hash);

// Catalogo publico como dados. Nao instala source.msix, nao ativa COM nem inicia winget.exe.
internal sealed class PackageCatalogIndex : IDisposable
{
    internal const string SourceUrl = "https://cdn.winget.microsoft.com/cache/source.msix";
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(35) };
    private readonly string folder;
    private readonly FileStream pinned;
    private IntPtr database;
    internal string Digest { get; }
    private PackageCatalogIndex(string folder, FileStream pinned, string digest)
    {
        this.folder = folder; this.pinned = pinned; Digest = digest;
        try
        {
        if (sqlite3_open_v2(Path.Combine(folder, "index.db"), out database, 1, IntPtr.Zero) != 0) throw new InvalidDataException("Indice SQLite indisponivel.");
        if (Rows("SELECT value FROM metadata WHERE name='majorVersion'", [], 2).SingleOrDefault()?.SingleOrDefault() != "1")
            throw new InvalidDataException("Formato de catalogo nao homologado.");
        var written=Rows("SELECT value FROM metadata WHERE name='lastwritetime'",[],2).SingleOrDefault()?.SingleOrDefault();
        if(!long.TryParse(written,out long seconds) || DateTimeOffset.FromUnixTimeSeconds(seconds)<DateTimeOffset.UtcNow.AddDays(-7) ||
            DateTimeOffset.FromUnixTimeSeconds(seconds)>DateTimeOffset.UtcNow.AddDays(1))throw new InvalidDataException("Catalogo fora da validade.");
        }
        catch{if(database!=IntPtr.Zero){sqlite3_close(database);database=IntPtr.Zero;}throw;}
    }
    internal static async Task<PackageCatalogIndex> DownloadAsync(CancellationToken token)
    {
        string folder = Path.Combine(Path.GetTempPath(), "GF-Catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string source = Path.Combine(folder, "source.msix");
            using (var response = await Http.GetAsync(SourceUrl, HttpCompletionOption.ResponseHeadersRead, token))
            await using (var output = new FileStream(source, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
                await UpdateDownloadTransfer.CopyAsync(response, output, 100000, 64L * 1024 * 1024, token);
            using var held = InstallerFileGuard.OpenRead(source, allowDirectoryWrites: true);
            if (!TrustedFileSignature.TryGetSigner(source, out var signer, out _) || signer != "Microsoft Corporation")
                throw new InvalidDataException("Assinatura do catalogo Microsoft nao confirmada.");
            using var archive = new ZipArchive(held, ZipArchiveMode.Read, true);
            if (archive.Entries.Count > 150 || archive.Entries.Select(e => e.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != archive.Entries.Count)
                throw new InvalidDataException("Estrutura do catalogo invalida.");
            var manifest = archive.GetEntry("AppxManifest.xml") ?? throw new InvalidDataException("Identidade do catalogo ausente.");
            if (manifest.Length > 65536) throw new InvalidDataException("Manifesto do catalogo excede limite.");
            using (var input = manifest.Open())
            using (var xml = System.Xml.XmlReader.Create(input, new() { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var doc = XDocument.Load(xml); var identity = doc.Root?.Elements().SingleOrDefault(e => e.Name.LocalName == "Identity");
                if ((string?)identity?.Attribute("Name") != "Microsoft.Winget.Source" ||
                    (string?)identity?.Attribute("Publisher") != "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US")
                    throw new InvalidDataException("Produto MSIX nao e o catalogo de pacotes.");
            }
            var entry = archive.GetEntry("Public/index.db") ?? throw new InvalidDataException("Indice ausente.");
            if (entry.Length is < 4096 or > 256L * 1024 * 1024) throw new InvalidDataException("Indice excede limite.");
            string path = Path.Combine(folder, "index.db");
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var input = entry.Open())
            { await input.CopyToAsync(output, token); if (output.Length != entry.Length) throw new InvalidDataException("Indice truncado."); }
            var pinned = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
            try { return new(folder, pinned, await InstallerFileGuard.DigestAsync(pinned, token)); }
            catch { pinned.Dispose(); throw; }
        }
        catch { try { Directory.Delete(folder, true); } catch (IOException) { } throw; }
    }
    internal IReadOnlyList<CatalogManifestReference> Find(string name, string publisher, string productCode, CancellationToken token)
    {
        string baseName = InstallerEngineEvidence.FamilyName(name);
        const string sql = "SELECT DISTINCT i.id,n.name,p.norm_publisher,v.version,m.pathpart,hex(m.hash) FROM manifest m " +
            "JOIN ids i ON i.rowid=m.id JOIN names n ON n.rowid=m.name JOIN versions v ON v.rowid=m.version " +
            "JOIN channels c ON c.rowid=m.channel JOIN norm_publishers_map pm ON pm.manifest=m.rowid " +
            "JOIN norm_publishers p ON p.rowid=pm.norm_publisher LEFT JOIN productcodes_map pc ON pc.manifest=m.rowid " +
            "LEFT JOIN productcodes code ON code.rowid=pc.productcode WHERE c.channel='' AND " +
            "(n.name=? COLLATE NOCASE OR (code.productcode=? COLLATE NOCASE AND ?<>'')) LIMIT 101";
        var rows = Rows(sql, [baseName, productCode, productCode], 101, token);
        if (rows.Count > 100) return [];
        string key = InstallerEngineEvidence.ProductKey(Matching.NormalizePublisher(publisher));
        var candidates = rows.Where(r => InstallerEngineEvidence.ProductKey(Matching.NormalizePublisher(r[2])) == key &&
            InstallerEngineEvidence.SameProduct(r[1], name) && Regex.IsMatch(r[5], "^[A-Fa-f0-9]{64}$"))
            .GroupBy(r => r[0], StringComparer.OrdinalIgnoreCase).ToArray();
        if (key.Length == 0 || candidates.Length != 1) return [];
        return candidates[0].GroupBy(r => r[3]).Select(g => g.First()).Select(r =>
            new CatalogManifestReference(r[0], r[1], r[3], RelativePath(r[4], token), r[5])).ToArray();
    }
    // Busca consultiva baseada em ProductCode MSI: identidade de pacote unica
    // no indice Microsoft assinado + fornecedor exato. A correspondencia de
    // ProductCode NUNCA cria plano, flags, download executavel ou permissao.
    // Mantemos Find estrito como unica porta da descoberta executavel.
    internal IReadOnlyList<CatalogManifestReference> FindForReviewByProductCode(
        string publisher, string productCode, CancellationToken token)
    {
        if (!Guid.TryParse(productCode, out var parsed) || parsed == Guid.Empty) return [];
        string key = InstallerEngineEvidence.ProductKey(Matching.NormalizePublisher(publisher));
        if (key.Length == 0) return [];
        const string sql = "SELECT DISTINCT i.id,n.name,p.norm_publisher,v.version,m.pathpart,hex(m.hash) FROM manifest m " +
            "JOIN ids i ON i.rowid=m.id JOIN names n ON n.rowid=m.name JOIN versions v ON v.rowid=m.version " +
            "JOIN channels c ON c.rowid=m.channel JOIN norm_publishers_map pm ON pm.manifest=m.rowid " +
            "JOIN norm_publishers p ON p.rowid=pm.norm_publisher JOIN productcodes_map pc ON pc.manifest=m.rowid " +
            "JOIN productcodes code ON code.rowid=pc.productcode WHERE c.channel='' AND " +
            "code.productcode=? COLLATE NOCASE LIMIT 101";
        var rows = Rows(sql, [parsed.ToString("B").ToUpperInvariant()], 101, token);
        var matches = rows.Where(r =>
            InstallerEngineEvidence.ProductKey(Matching.NormalizePublisher(r[2])) == key &&
            Regex.IsMatch(r[5], "^[A-Fa-f0-9]{64}$"))
            .GroupBy(r => r[0], StringComparer.OrdinalIgnoreCase).ToArray();
        if (matches.Length != 1) return [];
        return matches[0].GroupBy(r => r[3]).Select(g => g.First()).Select(r =>
            new CatalogManifestReference(r[0], r[1], r[3], RelativePath(r[4], token), r[5])).ToArray();
    }

    private string RelativePath(string node, CancellationToken token)
    {
        var parts = new List<string>(); var visited = new HashSet<string>();
        while (node.Length > 0)
        {
            if (!visited.Add(node) || parts.Count > 16) throw new InvalidDataException("Caminho de manifesto invalido.");
            var row = Rows("SELECT parent,pathpart FROM pathparts WHERE rowid=?", [node], 2, token).Single();
            if (row[1] is "." or ".." || row[1].Length == 0 || row[1].IndexOfAny(['/', '\\', ':', '?', '#', '%']) >= 0)
                throw new InvalidDataException("Caminho do catalogo fora do escopo.");
            parts.Add(row[1]); node = row[0];
        }
        parts.Reverse(); string path = string.Join('/', parts);
        if (!path.StartsWith("manifests/", StringComparison.Ordinal)) throw new InvalidDataException("Manifesto fora do escopo.");
        return path;
    }
    internal static async Task<byte[]> ReadManifestAsync(CatalogManifestReference reference, CancellationToken token)
    {
        var uri = new Uri("https://cdn.winget.microsoft.com/cache/" + reference.Path);
        if (uri.Host != "cdn.winget.microsoft.com" || !uri.AbsolutePath.StartsWith("/cache/manifests/", StringComparison.Ordinal))
            throw new InvalidDataException("Origem de manifesto invalida.");
        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        using var output = new MemoryStream();
        await UpdateDownloadTransfer.CopyAsync(response, output, 20, 2 * 1024 * 1024, token);
        byte[] bytes = output.ToArray();
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(reference.Hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Manifesto alterado ou catalogo desatualizado.");
        return bytes;
    }
    private List<string[]> Rows(string sql, string[] parameters, int maximum, CancellationToken token = default)
    {
        if (sqlite3_prepare_v2(database, sql, -1, out var statement, IntPtr.Zero) != 0)
            throw new InvalidDataException("Esquema de indice nao suportado.");
        try
        {
            for (int i = 0; i < parameters.Length; i++)
                if (sqlite3_bind_text(statement, i + 1, parameters[i], -1, new IntPtr(-1)) != 0) throw new InvalidDataException("Parametro de consulta invalido.");
            var result = new List<string[]>(); int status;
            while ((status = sqlite3_step(statement)) == 100)
            {
                token.ThrowIfCancellationRequested(); if (result.Count >= maximum) throw new InvalidDataException("Consulta excede limite.");
                int count = sqlite3_column_count(statement); if (count > 16) throw new InvalidDataException("Colunas excedem limite.");
                var row = new string[count];
                for (int i = 0; i < count; i++)
                { if (sqlite3_column_bytes(statement, i) > 8192) throw new InvalidDataException("Campo excede limite."); row[i] = Marshal.PtrToStringUTF8(sqlite3_column_text(statement, i)) ?? ""; }
                result.Add(row);
            }
            if (status != 101) throw new InvalidDataException("Indice incompleto ou ilegivel.");
            return result;
        }
        finally { sqlite3_finalize(statement); }
    }
#if GF_PROGRESSIVE
    internal static PackageCatalogIndex OpenSynthetic(string folder)
    {
        var held = InstallerFileGuard.OpenRead(Path.Combine(folder, "index.db"), allowDirectoryWrites: true);
        try { return new(folder, held, Convert.ToHexString(SHA256.HashData(held))); }
        catch { held.Dispose(); throw; }
    }
#endif
    public void Dispose()
    {
        if (database != IntPtr.Zero) { sqlite3_close(database); database = IntPtr.Zero; }
        pinned.Dispose(); try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string file, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr stmt, IntPtr tail);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_text(IntPtr stmt, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int bytes, IntPtr destructor);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr stmt);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr stmt);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_count(IntPtr stmt);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr stmt, int column);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(IntPtr stmt, int column);
}
