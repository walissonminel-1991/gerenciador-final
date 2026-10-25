using System.Text;

namespace GerenciadorAplicativos;

// Somente a prévia em memória; nunca corta o arquivo de diagnóstico ou relatórios estáticos.
internal sealed class BoundedLogText(int capacity = BoundedLogText.DefaultCapacity)
{
    internal const int DefaultCapacity = 65_536;
    internal const string Omission = "Mensagens antigas omitidas da prévia.\r\n";
    private readonly StringBuilder buffer = new();
    internal int Length => buffer.Length;
    internal bool Truncated { get; private set; }
    internal void Clear() { buffer.Clear(); Truncated = false; }
    public override string ToString() => buffer.ToString();
    internal void Append(string value)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (value.Length >= capacity)
        {
            Truncated |= buffer.Length > 0 || value.Length > capacity;
            buffer.Clear();
            int start = value.Length - capacity;
            if (start > 0 && char.IsLowSurrogate(value[start]) && char.IsHighSurrogate(value[start - 1])) start++;
            buffer.Append(value.AsSpan(start));
        }
        else
        {
            int excess = buffer.Length - (capacity - value.Length);
            if (excess > 0) { buffer.Remove(0, excess); Truncated = true; }
            // Não conservar metade de par UTF-16 cortado na fronteira da cauda.
            if (Truncated && buffer.Length > 0 && char.IsLowSurrogate(buffer[0])) buffer.Remove(0, 1);
            buffer.Append(value);
        }
    }
}
