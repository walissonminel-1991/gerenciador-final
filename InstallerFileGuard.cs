using System.Security.Cryptography;

namespace GerenciadorAplicativos;

// Mantem o arquivo aberto sem compartilhar escrita/exclusao enquanto e validado/usado.
// Nao oferece uma fronteira contra administrador/kernel ou alteracao de toda a pasta.
internal static class InstallerFileGuard
{
    internal static FileStream OpenRead(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidDataException("Caminho do pacote precisa ser absoluto.");
        string full = Path.GetFullPath(path);
        RejectLinks(full);
        var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            RejectLinks(full);
            if (stream.Length <= 0 || stream.Length > 300L * 1024 * 1024)
                throw new InvalidDataException("Tamanho do pacote fora do limite.");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    private static void RejectLinks(string path)
    {
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new InvalidDataException("Arquivo redirecionado ou diretorio nao autorizado.");
        for (var parent = new FileInfo(path).Directory; parent is not null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Pasta do pacote redirecionada.");
    }

    internal static async Task<string> DigestAsync(FileStream stream, CancellationToken token = default)
    {
        stream.Position = 0;
        string digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
        stream.Position = 0;
        return digest;
    }

    internal static async Task RequireDigestAsync(FileStream stream, string expected,
        CancellationToken token = default)
    {
        string actual = await DigestAsync(stream, token);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new CryptographicException("Pacote alterado desde a verificacao.");
    }
}
