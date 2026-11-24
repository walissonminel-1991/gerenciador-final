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
            DateTimeStyles.None, out var date) ? date : null;
    internal static string Key(ProgramEntry entry) =>
        string.IsNullOrWhiteSpace(entry.InventoryId) ? "" :
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.InventoryId.ToUpperInvariant())));
    internal static ProgramEntry[] Sort(IEnumerable<ProgramEntry> source, V269AppSort sort,
        IReadOnlyList<string> manual)
    {
        var items=source.ToArray();
        var comparer=StringComparer.CurrentCultureIgnoreCase;
        var order=manual.Select((key,index)=>(key,index)).GroupBy(x=>x.key,StringComparer.Ordinal)
            .ToDictionary(g=>g.Key,g=>g.First().index,StringComparer.Ordinal);
        IOrderedEnumerable<ProgramEntry> ordered=sort switch
        {
            V269AppSort.NameDescending=>items.OrderByDescending(x=>x.Name,comparer),
            V269AppSort.RecentlyInstalled=>items.OrderByDescending(x=>x.InstallDate.HasValue)
                .ThenByDescending(x=>x.InstallDate).ThenBy(x=>x.Name,comparer),
            V269AppSort.UpdatesFirst=>items.OrderByDescending(x=>x.IsUpdate).ThenBy(x=>x.Name,comparer),
            V269AppSort.Manual=>items.OrderBy(x=>order.TryGetValue(Key(x),out int rank)?rank:int.MaxValue)
                .ThenBy(x=>x.Name,comparer),
            _=>items.OrderBy(x=>x.Name,comparer)
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
internal static class V269AppOrderStore
{
    internal static string PathForUser=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GerenciadorFinal","ordem-aplicativos-v269.json");
    private const int MaxEntries=10000, MaxBytes=800000;
    private static bool Valid(string? key)=>key is {Length:64}&&key.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    internal static string[] Read(string path)
    {
        try
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(stream.Length>MaxBytes)return [];
            var keys=JsonSerializer.Deserialize<string[]>(stream);
            return keys is not null&&keys.Length<=MaxEntries&&keys.All(Valid)&&
                keys.Distinct(StringComparer.Ordinal).Count()==keys.Length?keys:[];
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException)
        {return [];}
    }
    internal static void Write(string path,string[] keys)
    {
        if(keys.Length>MaxEntries||!keys.All(Valid)||keys.Distinct(StringComparer.Ordinal).Count()!=keys.Length)
            throw new InvalidDataException("Ordem inválida.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {JsonSerializer.Serialize(stream,keys);stream.Flush(true);}
            File.Move(temporary,path,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
