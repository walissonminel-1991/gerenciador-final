using System.Drawing;
using System.Text.Json;

namespace GerenciadorAplicativos;

// Preferências somente do monitor. O host escolhe o caminho local; nenhum
// serviço, chave Run, tarefa, rede, ID de hardware ou habilitação automática.
internal sealed record GfResourceMonitorPreferences(
    int Version, bool Enabled, bool Expanded, bool AlwaysOnTop,
    int OpacityPercent, bool SnapToEdges, int? X, int? Y,
    bool Fixed = true, int ScalePercent = 100, V269Theme MonitorTheme = V269Theme.AzulGf)
{
    internal const int MinScale = 100, MaxScale = 150;
    internal static GfResourceMonitorPreferences Default => new(2, false, false, false, 100, true, null, null);

    internal GfResourceMonitorPreferences Normalize()
    {
        if (Version is not (1 or 2)) return Default;
        bool validLocation = X is >= -1_000_000 and <= 1_000_000 && Y is >= -1_000_000 and <= 1_000_000;
        return this with { Version = 2, OpacityPercent = Math.Clamp(OpacityPercent, 40, 100),
            Fixed = Version == 1 || Fixed, ScalePercent = Version == 1 ? 100 : Math.Clamp(ScalePercent, MinScale, MaxScale),
            MonitorTheme = Version == 1 || !Enum.IsDefined(MonitorTheme) ? V269Theme.AzulGf : MonitorTheme,
            X = validLocation ? X : null, Y = validLocation ? Y : null };
    }

    // Escala solicitada não é sobrescrita pela área útil momentânea.
    // Usar o modo expandido como referência para manter a mesma escala nos
    // dois modos. Não reduzir abaixo do mínimo autorizado no componente.
    internal static int? FitScale(int requested, int dpi, Size available)
    {
        if(dpi<=0 || available.Width<=0 || available.Height<=0)return null;
        for(int scale=Math.Clamp(requested,MinScale,MaxScale);scale>=MinScale;scale--)
        {
            int width=Math.Max(1,(int)Math.Round(552d*dpi*scale/9600d));
            int height=Math.Max(1,(int)Math.Round(186d*dpi*scale/9600d));
            if(width<=available.Width && height<=available.Height)return scale;
        }
        return null;
    }

    internal static Point Fit(Point location, Size size, Rectangle workArea, int snap)
    {
        // Coordenadas negativas são válidas em múltiplos monitores.
        if (workArea.Width <= 0 || workArea.Height <= 0) return location;
        int maxX = workArea.Left + Math.Max(0, workArea.Width - Math.Max(0, size.Width));
        int maxY = workArea.Top + Math.Max(0, workArea.Height - Math.Max(0, size.Height));
        int x = Math.Clamp(location.X, workArea.Left, maxX);
        int y = Math.Clamp(location.Y, workArea.Top, maxY);
        int threshold = Math.Clamp(snap, 0, 100);
        if (x - workArea.Left <= threshold) x = workArea.Left;
        else if (maxX - x <= threshold) x = maxX;
        if (y - workArea.Top <= threshold) y = workArea.Top;
        else if (maxY - y <= threshold) y = maxY;
        return new(x, y);
    }
}

// Armazenamento pequeno e versionado. Sem alterar o arquivo de Configurações
// da PR paralela. O consumidor deve tratar retorno false e continuar em memória.
internal static class GfResourceMonitorPreferencesStore
{
    private const int MaxBytes = 4096;

    private static bool HasCompleteSchema(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        int schema = 0;
        foreach (var property in root.EnumerateObject())
        {
            if (!keys.Add(property.Name)) return false;
            var value = property.Value;
            bool valid = property.Name switch
            {
                "Version" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out schema) && schema is 1 or 2,
                "Enabled" or "Expanded" or "AlwaysOnTop" or "SnapToEdges" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "OpacityPercent" or "ScalePercent" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
                "Fixed" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "MonitorTheme" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int theme) &&
                    Enum.IsDefined(typeof(V269Theme), theme),
                "X" or "Y" => value.ValueKind == JsonValueKind.Null || (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _)),
                _ => false
            };
            if (!valid) return false;
        }
        string[] original = ["Version","Enabled","Expanded","AlwaysOnTop","OpacityPercent","SnapToEdges","X","Y"];
        if (!original.All(keys.Contains)) return false;
        return schema == 1 ? keys.Count == 8 :
            schema == 2 && keys.Count == 11 && keys.Contains("Fixed") && keys.Contains("ScalePercent") && keys.Contains("MonitorTheme");
    }

    internal static GfResourceMonitorPreferences Load(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length <= 0 || stream.Length > MaxBytes) return GfResourceMonitorPreferences.Default;
            var bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) return GfResourceMonitorPreferences.Default;
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (!HasCompleteSchema(root))
                return GfResourceMonitorPreferences.Default;
            return (root.Deserialize<GfResourceMonitorPreferences>() ?? GfResourceMonitorPreferences.Default).Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        { return GfResourceMonitorPreferences.Default; }
    }

    internal static bool TrySave(string path, GfResourceMonitorPreferences preferences)
    {
        string? temporary = null;
        bool created = false;
        try
        {
            string full = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(full);
            if (directory is null || !Directory.Exists(directory)) return false;
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(preferences.Normalize());
            if (bytes.Length > MaxBytes) return false;
            temporary = Path.Combine(directory, ".gf-monitor-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            File.Move(temporary, full, overwrite: true);
            created = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return false; }
        finally
        {
            if (created && temporary is not null)
                try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
