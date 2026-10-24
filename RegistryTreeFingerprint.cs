using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace GerenciadorAplicativos;

// Formato enquadrado por contagens/comprimentos: hierarquia, tipo e limites
// de cada string de REG_MULTI_SZ fazem parte da identidade. Somente leitura.
internal static class RegistryTreeFingerprint
{
    internal static bool TryCapture(RegistryKey root, int maxEntries,
        out string digest, out int entries, int maxBytes = 4 * 1024 * 1024,
        Func<RegistryKey, string, RegistryKey?>? openChild = null,
        Action<RegistryKey>? beforeKey = null, bool throwOnFailure = false)
    {
        digest = ""; entries = 0;
        int count = 0, bytes = 0;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            void Append(ReadOnlySpan<byte> data)
            {
                if (data.Length > maxBytes - bytes) throw new InvalidDataException("Registro excedeu limite de bytes");
                bytes += data.Length;
                hash.AppendData(data);
            }
            void Number(int number)
            {
                Span<byte> buffer = stackalloc byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(buffer, number); Append(buffer);
            }
            void Long(long number)
            {
                Span<byte> buffer = stackalloc byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(buffer, number); Append(buffer);
            }
            void Text(string text)
            {
                if (text.Length > 1024 * 1024) throw new InvalidDataException("Valor grande demais");
                Number(text.Length);
                // Preserva inclusive unidades UTF-16 nao pareadas, sem fallback
                // de Encoding que faria strings diferentes terem os mesmos bytes.
                if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException();
                Append(MemoryMarshal.AsBytes(text.AsSpan()));
            }
            void Visit(RegistryKey key, int depth)
            {
                if (depth > 12 || ++count > maxEntries) throw new InvalidDataException("Muitas chaves");
                beforeKey?.Invoke(key);
                string[] values = key.GetValueNames().Order(StringComparer.OrdinalIgnoreCase).ToArray();
                Number(values.Length);
                foreach (string name in values)
                {
                    if (++count > maxEntries) throw new InvalidDataException("Muitos valores");
                    Text(name); Number((int)key.GetValueKind(name));
                    switch (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames))
                    {
                        case string text: Text(text); break;
                        case string[] strings:
                            Number(strings.Length); foreach (string text in strings) Text(text); break;
                        case byte[] data when data.Length <= 1024 * 1024:
                            Number(data.Length); Append(data); break;
                        case int number: Number(number); break;
                        case long number: Long(number); break;
                        default: throw new InvalidDataException("Valor alterado ou tipo nao suportado");
                    }
                }
                string[] children = key.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase).ToArray();
                Number(children.Length);
                foreach (string name in children)
                {
                    Text(name);
                    using var child = openChild is null ? key.OpenSubKey(name, writable: false) : openChild(key, name);
                    if (child is null) throw new IOException("Arvore alterada");
                    Visit(child, depth + 1);
                }
            }
            Visit(root, 0);
            digest = Convert.ToHexString(hash.GetHashAndReset()); entries = count;
            return true;
        }
        catch (Exception ex) when (!throwOnFailure && (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception))
        { return false; }
    }
}
