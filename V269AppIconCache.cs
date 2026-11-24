using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace GerenciadorAplicativos;

// DisplayIcon é dado não confiável. Nunca abrir URL/UNC, interpretar comando,
// executar o binário, usar ícone remoto ou seguir redirecionamentos conhecidos.
internal sealed record V269LocalIconSource(string Path,int Index)
{
    internal static V269LocalIconSource? Parse(string? raw)
    {
        if(string.IsNullOrWhiteSpace(raw)||raw.Length>1024||raw.Any(char.IsControl))return null;
        string s=raw.Trim(),path=s;int index=0;
        if(s.StartsWith('"'))
        {
            int end=s.IndexOf('"',1);
            if(end<2)return null;
            path=s[1..end];
            string suffix=s[(end+1)..].Trim();
            if(suffix.Length>0&&(!suffix.StartsWith(',')||
                !int.TryParse(suffix[1..].Trim(),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out index)))return null;
        }
        else
        {
            int comma=s.LastIndexOf(',');
            if(comma>=0&&int.TryParse(s[(comma+1)..].Trim(),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int parsed))
            {index=parsed;path=s[..comma].Trim();}
        }
        if(path.Length<4||!char.IsAsciiLetter(path[0])||path[1]!=':'||path[2]!='\\'||
            path[3..].Contains(':')||path.Contains('"')||path.Contains('%')||path.Contains('/')||
            path.Split('\\').Any(p=>p is "." or ".." || p.EndsWith(' ')||p.EndsWith('.'))||
            index is < -65535 or > 65535)return null;
        try
        {
            if(!System.IO.Path.IsPathFullyQualified(path))return null;
            if(System.IO.Path.GetExtension(path).ToLowerInvariant() is not (".exe" or ".dll" or ".ico"))return null;
            return new(System.IO.Path.GetFullPath(path),index);
        }
        catch(Exception ex) when(ex is ArgumentException or NotSupportedException or IOException){return null;}
    }
}
internal sealed class V269AppIconCache(Control owner):IDisposable
{
    private readonly Dictionary<V269LocalIconSource,Bitmap?> _items=new();
    private readonly Queue<V269LocalIconSource> _order=new(),_queue=new();
    private readonly HashSet<V269LocalIconSource> _pending=new();
    private bool _running,_disposed;
    internal Bitmap? Get(string value)
    {
        if(_disposed||!owner.IsHandleCreated||!owner.Visible||V269LocalIconSource.Parse(value) is not {} source)return null;
        if(_items.TryGetValue(source,out var image))return image;
        if(_pending.Count<24&&_pending.Add(source)){_queue.Enqueue(source);Start();}
        return null;
    }
    private void Start()
    {
        if(_disposed||_running||_queue.Count==0)return;
        _running=true;var source=_queue.Dequeue();
        _=Task.Run(()=>
        {
            byte[]? bytes=ReadResource(source);
            try
            {
                if(!owner.IsDisposed&&owner.IsHandleCreated)
                    owner.BeginInvoke(new Action(()=>Complete(source,bytes)));
            }
            catch(InvalidOperationException){ }
        });
    }
    private void Complete(V269LocalIconSource source,byte[]? bytes)
    {
        if(_disposed)return;
        _pending.Remove(source);_running=false;
        Bitmap? image=null;
        if(bytes is not null)
        {
            try{using var stream=new MemoryStream(bytes);using var decoded=new Bitmap(stream);image=new Bitmap(decoded);}
            catch(ArgumentException){ }
        }
        while(_items.Count>=192&&_order.TryDequeue(out var first))
            if(_items.Remove(first,out var removed))removed?.Dispose();
        _items[source]=image;_order.Enqueue(source);
        owner.Invalidate();Start();
    }
    internal static byte[]? ReadResource(V269LocalIconSource source)
    {
        IntPtr small=IntPtr.Zero;
        try
        {
            if(new DriveInfo(System.IO.Path.GetPathRoot(source.Path)!).DriveType!=DriveType.Fixed)return null;
            for(string? part=source.Path;part is not null;part=System.IO.Path.GetDirectoryName(part))
            {
                var attributes=File.GetAttributes(part);
                if((attributes&FileAttributes.ReparsePoint)!=0)return null;
                if(string.Equals(part,System.IO.Path.GetPathRoot(part),StringComparison.OrdinalIgnoreCase))break;
            }
            using var lockFile=new FileStream(source.Path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(lockFile.Length<=0||lockFile.Length>256L*1024*1024)return null;
            if(ExtractIconEx(source.Path,source.Index,IntPtr.Zero,out small,1)!=1||small==IntPtr.Zero)return null;
            using var icon=Icon.FromHandle(small);
            using var bitmap=icon.ToBitmap();
            using var resized=new Bitmap(bitmap,new Size(32,32));
            using var output=new MemoryStream();resized.Save(output,ImageFormat.Png);return output.ToArray();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or ExternalException or System.Security.SecurityException){return null;}
        finally{if(small!=IntPtr.Zero)DestroyIcon(small);}
    }
    public void Dispose()
    {
        _disposed=true;
        foreach(var image in _items.Values)image?.Dispose();
        _items.Clear();_queue.Clear();_order.Clear();_pending.Clear();
    }
    [DllImport("shell32.dll",EntryPoint="ExtractIconExW",CharSet=CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file,int index,IntPtr large,out IntPtr small,uint count);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
