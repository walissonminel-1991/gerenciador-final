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
// Identidade consultiva, derivada do inventário observado; não aceita caminhos,
 // comandos, AUMIDs arbitrários ou identificação apenas por nome de exibição.
internal sealed record V269MsixIconSource(string Family,string FullName)
{
    internal static V269MsixIconSource? From(ProgramEntry entry)
    {
        if(!entry.MsixInventoryComplete)return null;
        string family=entry.MsixPackageFamilyName,full=entry.MsixPackageFullName;
        if(family.Length is < 3 or > 255||full.Length is < 5 or > 255||
            !family.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '-' or '_')||
            !full.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '-' or '_'))return null;
        var f=family.Split('_');var p=full.Split('_');
        if(f.Length!=2||p.Length!=5||f.Any(string.IsNullOrEmpty)||
            !Version.TryParse(p[1],out _)||p[2].Length==0||
            !string.Equals(f[0],p[0],StringComparison.OrdinalIgnoreCase)||
            !string.Equals(f[1],p[4],StringComparison.OrdinalIgnoreCase)||
            !string.Equals(f[0],entry.MsixPackageName,StringComparison.OrdinalIgnoreCase)||
            !string.Equals(f[1],entry.MsixPublisherId,StringComparison.OrdinalIgnoreCase)||
            !string.Equals(entry.InventoryId,"MSIX|"+family,StringComparison.OrdinalIgnoreCase))return null;
        return new(family,full);
    }
}
internal sealed record V269IconRequest(V269LocalIconSource? Local,V269MsixIconSource? Msix);
internal sealed class V269AppIconCache : IDisposable
{
    private readonly Control owner;
    private readonly Func<V269IconRequest,Task<byte[]?>> _read;

    internal V269AppIconCache(Control owner,Func<V269IconRequest,Task<byte[]?>>? read=null)
    {
        this.owner=owner;
        _read=read??ReadAsync;
        owner.VisibleChanged+=OwnerVisibilityChanged;
    }

    private static async Task<byte[]?> ReadAsync(V269IconRequest source) =>
        source.Msix is {} msix?await ReadMsixAsync(msix):
        source.Local is {} local?ReadResource(local):null;

    internal int PendingCount=>_pending.Count;

    private void OwnerVisibilityChanged(object? sender,EventArgs e)
    {
        if(_disposed)return;
        if(!owner.Visible)
        {
            // A leitura já iniciada pode terminar; descartar apenas a fila ainda
            // não executada. Na volta, a pintura solicita os ícones visíveis.
            while(_queue.TryDequeue(out var source))_pending.Remove(source);
            return;
        }
        owner.Invalidate();Start();
    }
    private readonly Dictionary<V269IconRequest,Bitmap?> _items=new();
    private readonly Queue<V269IconRequest> _order=new(),_queue=new();
    private readonly HashSet<V269IconRequest> _pending=new();
    private bool _running,_disposed;
    internal Bitmap? Get(string value,V269MsixIconSource? msix=null)
    {
        if(_disposed||!owner.IsHandleCreated||!owner.Visible)return null;
        var local=msix is null?V269LocalIconSource.Parse(value):null;
        if(local is null&&msix is null)return null;
        var source=new V269IconRequest(local,msix);
        if(_items.TryGetValue(source,out var image))return image;
        if(_pending.Count<24&&_pending.Add(source)){_queue.Enqueue(source);Start();}
        return null;
    }
    private void Start()
    {
        if(_disposed||_running||_queue.Count==0||!owner.Visible||!owner.IsHandleCreated)return;
        _running=true;var source=_queue.Dequeue();
        _=Task.Run(async ()=>
        {
            byte[]? bytes=await _read(source);
            try
            {
                if(!owner.IsDisposed&&owner.IsHandleCreated)
                    owner.BeginInvoke(new Action(()=>Complete(source,bytes)));
            }
            catch(InvalidOperationException){ }
        });
    }
    private void Complete(V269IconRequest source,byte[]? bytes)
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
        if(owner.Visible){owner.Invalidate();Start();}
    }
    // O logotipo vem da API oficial de recursos do pacote instalado no usuário
    // corrente. Um pacote com múltiplas entradas não tem um logotipo único
    // comprovado para esta linha agregada; nesse caso manter a abreviação.
    internal static async Task<byte[]?> ReadMsixAsync(V269MsixIconSource source)
    {
        try
        {
            var manager=new Windows.Management.Deployment.PackageManager();
            var package=manager.FindPackageForUser(string.Empty,source.FullName);
            if(package is null||package.IsFramework||package.IsResourcePackage||
                !string.Equals(package.Id.FullName,source.FullName,StringComparison.OrdinalIgnoreCase)||
                !string.Equals(package.Id.FamilyName,source.Family,StringComparison.OrdinalIgnoreCase))return null;
            var entries=package.GetAppListEntries().Take(2).ToArray();
            if(entries.Length!=1)return null;
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var stream=await entries[0].DisplayInfo.GetLogo(new Windows.Foundation.Size(48,48))
                .OpenReadAsync().AsTask(deadline.Token);
            if(stream.Size is < 33 or > 524288)return null;
            uint count=(uint)stream.Size;
            using var input=stream.GetInputStreamAt(0);
            using var reader=new Windows.Storage.Streams.DataReader(input);
            if(await reader.LoadAsync(count).AsTask(deadline.Token)!=count)return null;
            byte[] bytes=new byte[(int)count];reader.ReadBytes(bytes);
            return NormalizeLogo(bytes);
        }
        catch(Exception){return null;} // somente apresentação; não alterar inventário
    }
    // Limitar também o bitmap decodificado retido no cache (48 x 48);
    // decodificação e redimensionamento acontecem fora da thread de UI.
    internal static byte[]? NormalizeLogo(byte[] bytes)
    {
        if(!IsBoundedPng(bytes))return null;
        try
        {
            using var input=new MemoryStream(bytes,false);
            using var decoded=Image.FromStream(input,false,true);
            if(decoded.Width is <1 or >512||decoded.Height is <1 or >512||
                decoded.RawFormat.Guid!=ImageFormat.Png.Guid)return null;
            using var normalized=new Bitmap(48,48,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(normalized))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                float factor=Math.Min(48f/decoded.Width,48f/decoded.Height);
                int w=Math.Max(1,(int)Math.Round(decoded.Width*factor)),h=Math.Max(1,(int)Math.Round(decoded.Height*factor));
                g.DrawImage(decoded,new Rectangle((48-w)/2,(48-h)/2,w,h));
            }
            using var output=new MemoryStream();normalized.Save(output,ImageFormat.Png);
            return output.ToArray();
        }
        catch(Exception ex) when(ex is ArgumentException or ExternalException or OutOfMemoryException or IOException)
        {return null;}
    }
    internal static bool IsBoundedPng(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is < 33 or > 524288||
            !bytes[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||
            !bytes.Slice(8,8).SequenceEqual(new byte[]{0,0,0,13,73,72,68,82}))return false;
        uint w=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16,4));
        uint h=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20,4));
        return w is >0 and <=512 && h is >0 and <=512;
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
        owner.VisibleChanged-=OwnerVisibilityChanged;
        foreach(var image in _items.Values)image?.Dispose();
        _items.Clear();_queue.Clear();_order.Clear();_pending.Clear();
    }
    [DllImport("shell32.dll",EntryPoint="ExtractIconExW",CharSet=CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file,int index,IntPtr large,out IntPtr small,uint count);
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
