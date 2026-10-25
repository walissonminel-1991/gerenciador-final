using System.Globalization;
using System.Text;

namespace GerenciadorAplicativos;

// Histórico técnico local. Identidades brutas existem somente nesta sessão, em memória.
// O arquivo contém exclusivamente códigos fechados e contagens, jamais nomes, caminhos,
// comandos, editor, IDs do Registro ou exceções. Não fornece autorização operacional.
internal sealed record InventoryDelta(bool Comparable, int Total, int Added, int NotObserved, int Changed, string Explanation);

internal sealed class LocalReliabilityHistory : IDisposable
{
    private readonly object gate = new();
    private readonly List<string> events = new();
    private Dictionary<string, (string Name, string Version, string Publisher)>? baseline;
    private StreamWriter? writer;
    private IDisposable? directoryLock;
    private int written;
    private bool disposed;
    internal bool StorageAvailable { get; private set; }
    internal string FilePath { get; private set; } = "";
    private const int MaxEvents = 128;
    private const int MaxWrites = 256;

    internal LocalReliabilityHistory(string? folder = null)
    {
        try
        {
            folder ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GerenciadorFinal", "Historico");
            if (!Path.IsPathFullyQualified(folder)) return;
            InstallerFileGuard.ValidateLocalPath(folder);
            Directory.CreateDirectory(folder);
            directoryLock = InstallerFileGuard.PinDirectory(folder, allowLeafWrites: true);
            FilePath = Path.Combine(folder, "GF-Historico-" + Guid.NewGuid().ToString("N") + ".log");
            var file = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                4096, FileOptions.WriteThrough);
            writer = new StreamWriter(file, new UTF8Encoding(false));
            writer.WriteLine("GF-HISTORICO|1|somente-codigos-e-contagens");
            writer.Flush();
            file.Flush(flushToDisk: true);
            StorageAvailable = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or InvalidDataException or NotSupportedException)
        {
            StorageAvailable = false;
            CloseStorage();
        }
    }

    internal InventoryDelta Observe(IEnumerable<ProgramEntry> source, bool complete)
    {
        var items = source.ToArray();
        lock (gate)
        {
            if (!complete)
            {
                Add("INVENTARIO_PARCIAL", items.Length, 0, 0, 0);
                return new(false, items.Length, 0, 0, 0,
                    "Leitura parcial: ausências e remoções NÃO podem ser inferidas; último inventário completo preservado.");
            }
            // Uma identidade ausente ou repetida não prova identidade/ausência. Não
            // atualizar a base até receber uma fotografia completa e inequívoca.
            var current = new Dictionary<string, (string Name, string Version, string Publisher)>(StringComparer.OrdinalIgnoreCase);
            foreach (var app in items)
            {
                if (string.IsNullOrWhiteSpace(app.InventoryId) ||
                    !current.TryAdd(app.InventoryId, (app.Name, app.Version, app.Publisher)))
                {
                    Add("INVENTARIO_AMBIGUO", items.Length, 0, 0, 0);
                    return new(false, items.Length, 0, 0, 0,
                        "Identidades ausentes/duplicadas: comparação suspensa; nenhuma remoção foi confirmada.");
                }
            }
            if (items.Length == 0)
            {
                Add("INVENTARIO_VAZIO", 0, 0, 0, 0);
                return new(false, 0, 0, 0, 0,
                    "Inventário vazio: confirme as fontes antes de interpretar ausências.");
            }
            if (baseline is null)
            {
                baseline = current;
                Add("INVENTARIO_BASE", items.Length, 0, 0, 0);
                return new(false, items.Length, 0, 0, 0,
                    "Primeira fotografia completa desta sessão; não há comparação anterior.");
            }
            int added = current.Keys.Count(k => !baseline.ContainsKey(k));
            int missing = baseline.Keys.Count(k => !current.ContainsKey(k));
            int changed = current.Count(pair => baseline.TryGetValue(pair.Key, out var old) && old != pair.Value);
            baseline = current;
            Add("INVENTARIO_COMPARADO", items.Length, added, missing, changed);
            return new(true, items.Length, added, missing, changed,
                $"Comparação consultiva: {added} novo(s) registro(s), {missing} não observado(s), " +
                $"{changed} com metadados alterados. Registro ausente NÃO confirma desinstalação.");
        }
    }

    // Códigos gerados por enums; nunca registrar o campo Reason ou Details recebido.
    internal void RecordUpdate(UpdateOperationSnapshot item)
    {
        lock (gate)
            Add("ATUALIZACAO_" + item.Progress.Stage.ToString().ToUpperInvariant(),
                item.ExitCode is int c && c is >= 0 and <= 65535 ? c : -1,
                item.RestartRequired ? 1 : 0, item.DiagnosticFailed ? 1 : 0, 0);
    }

    internal void RecordUninstall(UninstallOperationSnapshot item)
    {
        if (item.Active) return;
        lock (gate) Add("DESINSTALACAO_" + item.Stage.ToString().ToUpperInvariant(), 0, 0, 0, 0);
    }

    internal static string BlockReason(ProgramEntry app)
    {
        if (ProtectedApplications.IsProtectedName(app.Name))
            return "Componente protegido do sistema: operação não autorizada.";
        if (app.InventoryOnly)
            return "Entrada somente leitura: identidade ou autorização operacional insuficiente.";
        if (app.UpdateRequiresReview)
            return "Fontes ou identidade da atualização exigem revisão; instalação direta bloqueada.";
        if (app.IsUpdate && VerifiedUpdaters.GetPrimaryAction(app) is not ("update" or "terminal"))
            return "Sem receita executável comprovada; consultar apenas a fonte oficial.";
        if (!app.CanUninstall && !app.IsUpdate)
            return "Desinstalador não validado ou indisponível; não executar comando.";
        return "Nenhum bloqueio adicional identificado; validações da operação continuam obrigatórias.";
    }

    internal string Report(IEnumerable<ProgramEntry> inventory, bool incomplete)
    {
        var apps = inventory.ToArray();
        lock (gate)
        {
            var report = new StringBuilder();
            report.AppendLine("GERENCIADOR FINAL — RELATÓRIO LOCAL DE CONFIABILIDADE");
            report.AppendLine("Dados consultivos; não autorizam instalar, desinstalar ou limpar.");
            report.AppendLine("Histórico desta sessão; arquivos locais não são enviados pela aplicação.");
            report.AppendLine("Inventário: " + apps.Length.ToString(CultureInfo.InvariantCulture) +
                (incomplete ? " · PARCIAL (ausências inconclusivas)." : " · última leitura informada como completa."));
            report.AppendLine("Bloqueios e restrições (somente contagens):");
            foreach (var group in apps.GroupBy(BlockReason).OrderBy(x => x.Key, StringComparer.Ordinal))
                report.AppendLine("- " + group.Count().ToString(CultureInfo.InvariantCulture) + ": " + group.Key);
            report.AppendLine("Eventos recentes (códigos e contagens, do mais antigo ao novo):");
            foreach (var entry in events) report.AppendLine(entry);
            if (!StorageAvailable) report.AppendLine("AVISO: arquivo de histórico indisponível; eventos da sessão mantidos apenas em memória.");
            if (written >= MaxWrites) report.AppendLine("AVISO: limite de 256 eventos gravados nesta sessão atingido.");
            report.AppendLine("Limites: sem identificação de programas no relatório; sem provas de remoção a partir de ausências.");
            report.AppendLine("Aprovação CI não substitui homologação nos computadores nem validação AVG.");
            return report.ToString();
        }
    }

    private void Add(string code, int a, int b, int c, int d)
    {
        if (disposed) return;
        string line = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
            "|" + code + "|" + a.ToString(CultureInfo.InvariantCulture) +
            "|" + b.ToString(CultureInfo.InvariantCulture) +
            "|" + c.ToString(CultureInfo.InvariantCulture) +
            "|" + d.ToString(CultureInfo.InvariantCulture);
        events.Add(line);
        if (events.Count > MaxEvents) events.RemoveAt(0);
        if (writer is null || written >= MaxWrites) return;
        try
        {
            writer.WriteLine(line);
            writer.Flush();
            written++;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            StorageAvailable = false;
            CloseStorage();
        }
    }

    private void CloseStorage()
    {
        try { writer?.Dispose(); } catch (IOException) { }
        writer = null;
        try { directoryLock?.Dispose(); } catch (IOException) { }
        directoryLock = null;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            CloseStorage();
        }
    }
}
