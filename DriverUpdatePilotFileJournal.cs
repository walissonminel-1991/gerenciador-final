using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GerenciadorAplicativos;

// Consulta sem efeito colateral para coordenação WUA com os demais motores.
// O estado consultivo nunca concede permissão de instalação, nem reabre
// uma intenção pendente. O caminho físico usa os gates existentes.
internal enum DriverWuaJournalDisposition
{
    NeverStarted, Verified, PendingReconciliation, InvalidOrUnsafe
}
internal sealed record DriverWuaJournalSnapshot(
    DriverWuaJournalDisposition Disposition, string Phase,
    DateTimeOffset? LastWrittenUtc, string Message)
{
    internal bool RequiresSupervisedReview =>
        Disposition is DriverWuaJournalDisposition.PendingReconciliation or
            DriverWuaJournalDisposition.InvalidOrUnsafe;
    internal bool AuthorizesInstallation => false;
}

// Journal experimental para impedir repeticao apos queda, timeout ou resultado
// incerto. Arquivo local separado do historico v1 de consultas.
internal sealed class DriverUpdatePilotFileJournal(string path) :
    IDriverUpdatePilotJournal, IDisposable
{
    private sealed record Ledger(string UpdateId, int Revision, string DeviceId,
        string Phase, DateTimeOffset Timestamp, string Sha256);
    private FileStream? heldLock;
    private DriverUpdatePilotChoice? activeChoice;

    // O GF elevado não pode escrever diário ou lock através de junções,
    // symlinks e caminhos UNC. Verificar antes/depois de criar diretórios.
    private static bool SafeStorage(string journalPath)
    {
        try
        {
            string full = Path.GetFullPath(journalPath);
            if (!Path.IsPathFullyQualified(full) ||
                full.StartsWith(@"\\", StringComparison.Ordinal) ||
                full.StartsWith("//", StringComparison.Ordinal)) return false;
            for (DirectoryInfo? dir = new FileInfo(full).Directory;
                dir is not null; dir = dir.Parent)
                if (dir.Exists && dir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return false;
            foreach (string file in new[] { full, full + ".lock" })
                if (File.Exists(file) &&
                    File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))
                    return false;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    // Novo contrato consultivo: snapshot tipado do diário local. Nunca
    // lista IDs de dispositivo, caminhos ou metadados de identificação.
    // Cuidado: "Verified" atesta só o registro guardado, não a situação
    // PnP atual nem uma nova autorização de manutenção.
    internal static DriverWuaJournalSnapshot InspectReadOnly(string journalPath)
    {
        DriverWuaJournalSnapshot Invalid(string reason) => new(
            DriverWuaJournalDisposition.InvalidOrUnsafe, "",
            null, reason);
        try
        {
            if (!SafeStorage(journalPath))
                return Invalid("Diário WUA inseguro ou inacessível; operação bloqueada.");
            if (!File.Exists(journalPath))
                return new(DriverWuaJournalDisposition.NeverStarted, "", null,
                    "Nenhuma intenção WUA observada nesta localização.");
            using var input = new FileStream(journalPath, FileMode.Open, FileAccess.Read,
                FileShare.Read);
            if (input.Length is <= 0 or > 4096)
                return Invalid("Diário WUA vazio ou fora do limite; preservar.");
            var bytes = new byte[(int)input.Length];
            input.ReadExactly(bytes);
            if (input.ReadByte() != -1)
                return Invalid("Diário WUA alterado durante leitura; preservar.");
            var ledger = JsonSerializer.Deserialize<Ledger>(bytes);
            if (ledger is null || !Valid(ledger) ||
                ledger.Timestamp == default ||
                ledger.Timestamp > DateTimeOffset.UtcNow.AddMinutes(5))
                return Invalid("Diário WUA inválido; não sobrescrever.");
            bool verified = ledger.Phase == nameof(DriverUpdatePilotState.Verified);
            return new(verified ? DriverWuaJournalDisposition.Verified :
                DriverWuaJournalDisposition.PendingReconciliation,
                ledger.Phase, ledger.Timestamp, verified
                    ? "Última operação registrada como verificada; nova operação ainda exige consulta e consentimento."
                    : "Intenção WUA não verificada; conciliação supervisionada necessária.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Invalid("Diário WUA indisponível (" + ex.GetType().Name +
                "); nenhuma operação liberada.");
        }
    }

    // G3.1: inspeção passiva e limitada a 4 KiB. Nunca cria, limpa ou
    // sobrescreve diário existente (inclusive estado incerto ou corrupto).
    internal static string ReadOnlyStatus(string journalPath)
    {
        try
        {
            if (!SafeStorage(journalPath)) return "Diário WUA em local inseguro: instalação bloqueada.";
            if (!File.Exists(journalPath)) return "Diário WUA: nenhuma instalação registrada.";
            using var file = new FileStream(journalPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (file.Length is <= 0 or > 4096)
                return "Diário WUA inválido: instalação bloqueada.";
            var bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            if (file.ReadByte() != -1)
                return "Diário WUA inválido: instalação bloqueada.";
            var last = JsonSerializer.Deserialize<Ledger>(bytes);
            if (last is null || !Valid(last))
                return "Diário WUA inválido: instalação bloqueada.";
            return last.Phase == nameof(DriverUpdatePilotState.Verified)
                ? "Diário WUA: última operação verificada."
                : "Diário WUA: resultado pendente de conciliação; não repetir instalação.";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return "Diário WUA indisponível: instalação bloqueada.";
        }
    }

    // Preflight sem escrita: a confirmação jamais substitui um diário pendente.
    // TryBegin continua sendo a autoridade transacional após o download.
    internal static bool MayBeginReadOnly(string journalPath)
    {
        try
        {
            if (!SafeStorage(journalPath)) return false;
            if (!File.Exists(journalPath)) return true;
            var info = new FileInfo(journalPath);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                info.Length is <= 0 or > 4096) return false;
            using var stream = new FileStream(journalPath, FileMode.Open,
                FileAccess.Read, FileShare.Read);
            var bytes = new byte[(int)info.Length];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) return false;
            var last = JsonSerializer.Deserialize<Ledger>(bytes);
            return last is not null && Valid(last) &&
                last.Phase == nameof(DriverUpdatePilotState.Verified);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    internal static string SuggestedPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GerenciadorFinal", "drivers-update-pilot-v1.json");

    private static string Hash(string id, int revision, string device,
        string phase, DateTimeOffset at) => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(id + "\n" + revision + "\n" + device +
                "\n" + phase + "\n" + at.ToUniversalTime().ToString("O",
                    System.Globalization.CultureInfo.InvariantCulture))));

    private static bool Valid(Ledger value) =>
        value is not null && value.Revision >= 1 &&
        (value.Phase == "InstallationStarted" ||
         Enum.TryParse<DriverUpdatePilotState>(value.Phase, out _)) &&
        Guid.TryParseExact(value.UpdateId, "D", out _) &&
        !string.IsNullOrWhiteSpace(value.DeviceId) &&
        value.Sha256 == Hash(value.UpdateId, value.Revision, value.DeviceId,
            value.Phase, value.Timestamp);

    private void Write(DriverUpdatePilotChoice choice, string phase)
    {
        var at = DateTimeOffset.UtcNow;
        var state = new Ledger(choice.UpdateId, choice.Revision, choice.DeviceId,
            phase, at, Hash(choice.UpdateId, choice.Revision, choice.DeviceId, phase, at));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state);
        if (bytes.Length > 4096) throw new InvalidDataException("Journal excedeu o limite.");
        string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(tmp, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, overwrite: true);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public bool TryBegin(DriverUpdatePilotChoice choice)
    {
        if (heldLock is not null || choice is null || !SafeStorage(path)) return false;
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) return false;
            Directory.CreateDirectory(directory);
            if (!SafeStorage(path)) return false;
            heldLock = new FileStream(path + ".lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length is <= 0 or > 4096)
                { Dispose(); return false; }
                var previous = JsonSerializer.Deserialize<Ledger>(File.ReadAllBytes(path));
                // PendingReboot e Inconclusive tambem bloqueiam a proxima
                // transacao ate reconciliacao supervisionada.
                if (previous is null || !Valid(previous) ||
                    previous.Phase != nameof(DriverUpdatePilotState.Verified))
                { Dispose(); return false; }
            }
            Write(choice, "InstallationStarted");
            activeChoice = choice;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Dispose();
            return false;
        }
    }

    public void Mark(DriverUpdatePilotChoice choice, DriverUpdatePilotState state)
    {
        if (heldLock is null || activeChoice is null ||
            !Equals(activeChoice, choice))
            throw new InvalidOperationException("Nenhuma intencao ativa ou identidade mudou.");
        try { Write(choice, state.ToString()); }
        finally { Dispose(); }
    }

    public void Dispose()
    {
        activeChoice = null;
        heldLock?.Dispose();
        heldLock = null;
    }
}
