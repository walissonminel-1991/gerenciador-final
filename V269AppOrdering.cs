using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace GerenciadorAplicativos;

// Apresentação: não é evidência de elegibilidade ou autorização.
internal enum V269AppSort { NameAscending, NameDescending, RecentlyInstalled, UpdatesFirst, Manual }
internal static class V269AppOrdering
{
    internal static DateOnly? ParseRegistryDate(object? value) =>
        value is string s && s.Length == 8 &&
        DateOnly.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) && date.Year>=1980 ? date : null;

    internal static string Key(ProgramEntry entry) =>
        string.IsNullOrWhiteSpace(entry.InventoryId) ? "" :
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.InventoryId.ToUpperInvariant())));
    // Preservar o texto composto e a comparação cultural da pesquisa original,
    // inclusive ocorrências que atravessam os separadores entre campos.
    // Buffer pequeno na pilha; campos muito extensos seguem o caminho original.
    internal static bool MatchesSearch(ProgramEntry entry, string query)
    {
        if (query.Length == 0) return true;
        ReadOnlySpan<string?> fields = [entry.Name, entry.Version, entry.Publisher,
            entry.PackageId, entry.AvailableVersion, entry.CatalogIdentity,
            entry.CatalogDisplayName, entry.InventorySource, entry.MsiProductCode];
        Span<char> buffer = stackalloc char[512];
        int length = 0;
        for (int i = 0; i < fields.Length; i++)
        {
            if (i != 0)
            {
                if (length == buffer.Length) return SearchLong();
                buffer[length++] = ' ';
            }
            var field = fields[i].AsSpan();
            if (field.Length > buffer.Length - length) return SearchLong();
            field.CopyTo(buffer[length..]);
            length += field.Length;
        }
        return MemoryExtensions.Contains((ReadOnlySpan<char>)buffer[..length],
            query.AsSpan(), StringComparison.CurrentCultureIgnoreCase);

        bool SearchLong() => string.Join(" ", entry.Name, entry.Version, entry.Publisher,
            entry.PackageId, entry.AvailableVersion, entry.CatalogIdentity,
            entry.CatalogDisplayName, entry.InventorySource, entry.MsiProductCode)
            .Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    internal static ProgramEntry[] Sort(IEnumerable<ProgramEntry> source, V269AppSort sort,
        IReadOnlyList<string> manual)
    {
        var comparer=StringComparer.CurrentCultureIgnoreCase;
        // A ordem persistida só é necessária no modo manual. Nos demais,
        // OrderBy já materializa a origem uma vez: não duplicar o buffer.
        Dictionary<string,int>? order=null;
        if(sort==V269AppSort.Manual)
        {
            order=new(StringComparer.Ordinal);
            int index=0;
            foreach(string key in manual)order.TryAdd(key,index++);
        }
        IOrderedEnumerable<ProgramEntry> ordered=sort switch
        {
            V269AppSort.NameDescending=>source.OrderByDescending(x=>x.Name,comparer),
            V269AppSort.RecentlyInstalled=>source.OrderByDescending(x=>x.InstallDate.HasValue)
                .ThenByDescending(x=>x.InstallDate).ThenBy(x=>x.Name,comparer),
            V269AppSort.UpdatesFirst=>source.OrderByDescending(x=>x.IsUpdate).ThenBy(x=>x.Name,comparer),
            V269AppSort.Manual=>source.OrderBy(x=>order!.TryGetValue(Key(x),out int rank)?rank:int.MaxValue)
                .ThenBy(x=>x.Name,comparer),
            _=>source.OrderBy(x=>x.Name,comparer)
        };
        return ordered.ThenBy(x=>x.Version,comparer).ThenBy(x=>x.InventoryId,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    internal static string[]? Move(IEnumerable<ProgramEntry> all, IReadOnlyList<string> manual,
        ProgramEntry item, ProgramEntry target)
    {
        var items=Sort(all,V269AppSort.Manual,manual);
        string from=Key(item),to=Key(target);
        if(from.Length==0||to.Length==0||from==to||
            items.Count(x=>Key(x)==from)!=1||items.Count(x=>Key(x)==to)!=1)return null;
        var keys=items.Select(Key).Where(k=>k.Length==64).Distinct(StringComparer.Ordinal).ToList();
        int original=keys.IndexOf(from),destination=keys.IndexOf(to);
        if(original<0||destination<0)return null;
        keys.RemoveAt(original);keys.Insert(destination,from);
        return keys.ToArray();
    }
}
internal sealed record V269AppOrderPreferences(int Schema,V269AppSort Sort,string[] Keys);
internal static class V269AppOrderStore
{
    internal static string PathForUser=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GerenciadorFinal","ordem-aplicativos-v269.json");
    private const int MaxEntries=10000, MaxBytes=800000;
    private static bool Valid(string? key)=>key is {Length:64}&&key.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static V269AppOrderPreferences Default()=>new(1,V269AppSort.NameAscending,[]);
    private static bool ValidKeys(string[]? keys)=>keys is not null&&keys.Length<=MaxEntries&&keys.All(Valid)&&
        keys.Distinct(StringComparer.Ordinal).Count()==keys.Length;
    internal static string[] Read(string path)=>ReadPreferences(path).Keys;
    internal static V269AppOrderPreferences ReadPreferences(string path)
    {
        try
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length>MaxBytes)return Default();
            using var json=JsonDocument.Parse(stream);
            if(json.RootElement.ValueKind==JsonValueKind.Array)
            {
                var keys=json.RootElement.Deserialize<string[]>();
                return ValidKeys(keys)?new(1,keys!.Length>0?V269AppSort.Manual:V269AppSort.NameAscending,keys!):Default();
            }
            if(json.RootElement.ValueKind!=JsonValueKind.Object||
                !json.RootElement.TryGetProperty("Sort",out _))return Default();
            var saved=json.RootElement.Deserialize<V269AppOrderPreferences>();
            return saved is {Schema:1}&&Enum.IsDefined(saved.Sort)&&ValidKeys(saved.Keys)?saved:Default();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException)
        {return Default();}
    }
    internal static void Write(string path,string[] keys)=>WritePreferences(path,V269AppSort.Manual,keys);
    internal static void WritePreferences(string path,V269AppSort sort,string[] keys)
    {
        if(!Enum.IsDefined(sort)||!ValidKeys(keys))throw new InvalidDataException("Ordem inválida.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {JsonSerializer.Serialize(stream,new V269AppOrderPreferences(1,sort,keys));stream.Flush(true);}
            File.Move(temporary,path,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
