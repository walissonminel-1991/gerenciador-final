namespace GerenciadorAplicativos;

internal interface IElevatedOperationCheckpoint
{
    bool TryBegin();
    bool TryClear();
    void Retain();
}

// Posse de manutenção é distinta de histórico diagnóstico. O diretório deve
// ser o mesmo diretório local estável e validado para todas as sessões;
// nunca escolher TEMP como fallback. O chamador não cria este diretório
// a partir de entrada do usuário. Um marcador preexistente, inclusive
// vazio/inválido, bloqueia; não há reset automático por tempo, PID ou reboot.
internal sealed class PersistentElevatedOperationCheckpoint(string directory) : IElevatedOperationCheckpoint
{
    internal const string MarkerName = "GF-elevated-operation-v1.pending";
    internal const string LockName = "GF-elevated-operation-v1.lock";
    private FileStream? _coordination, _held;
    private InstallerFileGuard.FileIdentity? _identity;
    private byte[]? _nonce;
    private string? _path;

    private static bool Expected(Exception ex) => ex is IOException or UnauthorizedAccessException
        or ArgumentException or NotSupportedException or InvalidOperationException
        or System.Security.SecurityException or System.ComponentModel.Win32Exception;

    public bool TryBegin()
    {
        if (_coordination is not null) return false;
        bool retained = false;
        try
        {
            string root = InstallerFileGuard.ValidateLocalPath(directory);
            // OpenCoordinationLock fixa os ancestrais e recusa reparse/links.
            // Diretório inexistente, lock ocupado ou storage não confiável: negar.
            _coordination = InstallerFileGuard.OpenCoordinationLock(Path.Combine(root, LockName));
            _path = Path.Combine(root, MarkerName);
            _nonce = Guid.NewGuid().ToByteArray();
            using (var file = new FileStream(_path, FileMode.CreateNew, FileAccess.Write,
                FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                file.Write(_nonce);
                file.Flush(flushToDisk: true);
                _identity = InstallerFileGuard.ReadIdentity(file);
                if (_identity.Links != 1 || _identity.Size != (ulong)_nonce.Length) return false;
            }
            _held = InstallerFileGuard.OpenResidual(_path);
            if (!OwnsMarker(_held)) return false;
            retained = true;
            return true;
        }
        catch (Exception ex) when (Expected(ex)) { return false; }
        finally { if (!retained) Retain(); }
    }

    // Chamado somente pela posse original depois de prova terminal específica
    // da tarefa ou prova de que não houve lançamento. Não reconcilia marcador
    // de outra sessão. Excluir somente por handle validado, nunca por nome.
    public bool TryClear()
    {
        if (_coordination is null || _held is null || _path is null) return false;
        try
        {
            if (!OwnsMarker(_held)) return false;
            _held.Dispose();
            _held = null;
            using var delete = InstallerFileGuard.OpenResidual(_path, deleteAccess: true);
            if (!OwnsMarker(delete)) return false;
            InstallerFileGuard.MarkResidualForDeletion(delete);
            return true;
        }
        catch (Exception ex) when (Expected(ex)) { return false; }
        finally { Retain(); }
    }

    private bool OwnsMarker(FileStream file)
    {
        if (_identity is null || _nonce is null ||
            InstallerFileGuard.ReadIdentity(file) != _identity ||
            file.Length != _nonce.Length) return false;
        Span<byte> actual = stackalloc byte[16];
        file.Position = 0;
        file.ReadExactly(actual);
        return actual.SequenceEqual(_nonce) && file.ReadByte() == -1;
    }

    // Perda de observação/descarte: fechar os handles, conservar os bytes.
    // A próxima sessão não pode adquirir por CreateNew enquanto houver marcador.
    public void Retain()
    {
        try { _held?.Dispose(); }
        finally
        {
            _held = null;
            try { _coordination?.Dispose(); }
            finally { _coordination = null; _identity = null; _nonce = null; _path = null; }
        }
    }
}
