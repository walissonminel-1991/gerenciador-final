using System.Diagnostics;

namespace GerenciadorAplicativos;

// Orçamento cooperativo compartilhado por todas as fontes. Não interrompe uma
// chamada nativa bloqueada; interrompe antes do próximo item/abertura.
internal sealed class ResidualScanBudget
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly CancellationToken token;
    private readonly TimeSpan timeout;
    private readonly int maxEntries;
    internal int Entries { get; private set; }
    internal bool Incomplete => Issues != ResidualScanIssue.None;
    internal ResidualScanIssue Issues { get; private set; }
    private string? currentSource;
    internal readonly HashSet<string> SourcesConsulted = new(StringComparer.Ordinal);
    internal readonly HashSet<string> SourcesUnavailable = new(StringComparer.Ordinal);
    internal ResidualScanBudget(CancellationToken token = default,
        TimeSpan? timeout = null, int maxEntries = 30000)
    {
        this.token = token;
        this.timeout = timeout ?? TimeSpan.FromSeconds(14);
        this.maxEntries = Math.Max(0, maxEntries);
    }
    internal bool CanContinue()
    {
        if (token.IsCancellationRequested) Issues |= ResidualScanIssue.Cancelled;
        if (clock.Elapsed >= timeout) Issues |= ResidualScanIssue.Timeout;
        if (Entries >= maxEntries) Issues |= ResidualScanIssue.EntryLimit;
        // Limite local de uma raiz não interrompe as demais fontes.
        return !token.IsCancellationRequested && clock.Elapsed < timeout && Entries < maxEntries;
    }
    internal bool Visit()
    {
        if (!CanContinue()) return false;
        Entries++;
        return true;
    }
    internal void MarkIncomplete(ResidualScanIssue issue = ResidualScanIssue.ReadFailure)
    {
        Issues |= issue;
        if ((issue & (ResidualScanIssue.ReadFailure | ResidualScanIssue.SourceUnavailable)) != 0 && currentSource is not null)
            SourcesUnavailable.Add(currentSource);
    }
    internal void Source(string identifier) { currentSource = identifier; SourcesConsulted.Add(identifier); }
    internal void Unavailable(string identifier)
    { SourcesUnavailable.Add(identifier); MarkIncomplete(); }
    internal ResidualCoverage Coverage => new(Issues, Entries,
        SourcesConsulted.Order(StringComparer.Ordinal).ToArray(),
        SourcesUnavailable.Order(StringComparer.Ordinal).ToArray());
}

internal static class ResidualReadSafety
{
    // Somente caminhos locais absolutos; sem dispositivos, ADS ou traversal.
    internal static bool IsLocalAbsolute(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\0') || path.Split('\\', '/').Any(p => p is "." or "..")) return false;
        return !OperatingSystem.IsWindows() ||
            (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' &&
             path[2] == '\\' && !path[2..].Contains(':'));
    }
    internal static bool HasExistingLocalFile(string path, ResidualScanBudget? budget)
    {
        if (!IsLocalAbsolute(path)) { budget?.MarkIncomplete(ResidualScanIssue.SourceUnavailable); return false; }
        try
        {
            // File.Exists também retorna false para erros; isso perderia uma proteção ativa.
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                !IsUnlinkedDirectory(Path.GetDirectoryName(path) ?? ""))
            { budget?.MarkIncomplete(ResidualScanIssue.LinkSkipped); return false; }
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException)
        { budget?.MarkIncomplete(); return false; }
    }

    internal static bool CanInspectDirectory(string path, ResidualScanBudget budget)
    {
        if (!IsLocalAbsolute(path)) { budget.MarkIncomplete(ResidualScanIssue.SourceUnavailable); return false; }
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == 0 || !IsUnlinkedDirectory(path))
            { budget.MarkIncomplete(ResidualScanIssue.LinkSkipped); return false; }
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException)
        { budget.MarkIncomplete(); return false; }
    }
    internal static bool PathsOverlap(string? first, string? second)
    {
        if (!IsLocalAbsolute(first) || !IsLocalAbsolute(second)) return false;
        try
        {
            string a = Path.GetFullPath(first!).TrimEnd('\\', '/');
            string b = Path.GetFullPath(second!).TrimEnd('\\', '/');
            return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { return false; }
    }
    internal static bool IsUnlinkedDirectory(string path)
    {
        if (!IsLocalAbsolute(path)) return false;
        try
        {
            if (OperatingSystem.IsWindows() &&
                new DriveInfo(Path.GetPathRoot(path)!).DriveType != DriveType.Fixed) return false;
            for (DirectoryInfo? dir = new(path); dir is not null; dir = dir.Parent)
                if (!dir.Exists || (dir.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or NotSupportedException) { return false; }
    }
}

[Flags]
internal enum ResidualScanIssue
{
    None = 0, EntryLimit = 1, Timeout = 2, Cancelled = 4, ReadFailure = 8,
    GroupLimit = 16, LocationLimit = 32, InventoryPartial = 64,
    DepthLimit = 128, LinkSkipped = 256, SourceUnavailable = 512, ContradictoryEvidence = 1024,
    ReportIncomplete = 2048
}

// Somente contagens e identificadores fixos de fontes; nunca caminhos ou nomes de produtos.
internal sealed record ResidualCoverage(ResidualScanIssue Issues = ResidualScanIssue.None,
    int EntriesExamined = 0, IReadOnlyList<string>? SourcesConsulted = null,
    IReadOnlyList<string>? SourcesUnavailable = null, int CandidatesFound = 0,
    int ReportsOmitted = 0, int LocationsOmitted = 0, int? CandidatesNotEvaluated = 0, int CandidatesExamined = 0, int ReportsReconciled = 0, int LocationsReconciled = 0, int ProtectedGroups = 0, int InsufficientGroups = 0)
{
    private IReadOnlyList<ResidualCoverage>? Components { get; init; }
    internal bool Incomplete => Issues != ResidualScanIssue.None;
    internal const string PartialMessage = "ANÁLISE PARCIAL — a busca atingiu um limite ou encontrou uma falha e pode haver outros resultados.";
    internal string Describe() => (Incomplete ? PartialMessage : "Análise concluída nos locais consultados.") +
        $"\r\nEntradas examinadas: {EntriesExamined}; grupos examinados: {CandidatesExamined}; candidatos encontrados: {CandidatesFound}; " +
        $"grupos indisponíveis: {ReportsOmitted}; locais indisponíveis: {LocationsOmitted}; " +
        $"duplicatas reconciliadas: {ReportsReconciled} grupos / {LocationsReconciled} locais; " +
        $"grupos protegidos: {ProtectedGroups}; indícios isolados: {InsufficientGroups}; " +
        "evidências não avaliadas: " + (CandidatesNotEvaluated?.ToString() ?? "quantidade desconhecida") + "." +
        (SourcesConsulted is { Count: > 0 } ? "\r\nFontes consultadas: " + string.Join(", ", SourcesConsulted.Select(SourceLabel)) : "") +
        (SourcesUnavailable is { Count: > 0 } ? "\r\nFontes indisponíveis: " + string.Join(", ", SourcesUnavailable.Select(SourceLabel)) : "") +
        (Incomplete ? "\r\nMotivos: " + DescribeIssues(Issues) + ". Confiança limitada; preservar e repetir a análise." : "") +
        "\r\nEscopo: locais comuns de aplicativos, até dois níveis; não é uma varredura completa do computador." +
        "\r\nExclusão indisponível nesta versão de teste.";

    private static string SourceLabel(string source) => source switch
    {
        "AppData" => "perfis do usuário", "ProgramData" => "dados comuns dos aplicativos",
        "ProgramFiles" => "pastas de instalação", "StartMenu" => "atalhos do menu Iniciar",
        "Processes" => "programas em execução", "AppPaths" => "executáveis registrados",
        "RunServices" => "inicialização e serviços", "HistoricalCatalog" => "regras históricas",
        "PostUninstall" => "locais após desinstalação", "WinMergeHistory" => "histórico do WinMerge",
        "Preview" => "conferência dos arquivos", "Inspection" => "inspeção sob demanda", "DeepCatalog" => "perfis de revisão avançada", _ => source.StartsWith("HKEY_", StringComparison.Ordinal)
            ? "Registro: " + source : source
    };
    private static string DescribeIssues(ResidualScanIssue issues) => string.Join(", ", new[]
    {
        (ResidualScanIssue.EntryLimit, "limite de entradas"), (ResidualScanIssue.Timeout, "tempo esgotado"),
        (ResidualScanIssue.Cancelled, "cancelamento"), (ResidualScanIssue.ReadFailure, "falha de leitura"),
        (ResidualScanIssue.GroupLimit, "limite de grupos exibidos"), (ResidualScanIssue.LocationLimit, "limite de locais exibidos"),
        (ResidualScanIssue.InventoryPartial, "inventário incompleto"), (ResidualScanIssue.DepthLimit, "limite de profundidade"),
        (ResidualScanIssue.LinkSkipped, "local redirecionado ou não validado"), (ResidualScanIssue.SourceUnavailable, "fonte indisponível"),
        (ResidualScanIssue.ContradictoryEvidence, "evidências contraditórias"),
        (ResidualScanIssue.ReportIncomplete, "relatório de origem incompleto")
    }.Where(p => (issues & p.Item1) != 0).Select(p => p.Item2));

    internal static ResidualCoverage Combine(IEnumerable<ResidualCoverage?> coverages)
    {
        var values = coverages.OfType<ResidualCoverage>().Distinct().ToArray();
        // Os componentes são observações originais, para não somar novamente
        // um resumo quando ele reaparece em relatório + coleção + união de fontes.
        var leaves = values.SelectMany(v => v.Components ?? new[] { v }).Distinct().ToArray();
        return new ResidualCoverage(values.Aggregate(ResidualScanIssue.None, (a, b) => a | b.Issues),
            leaves.Sum(v => v.EntriesExamined),
            values.SelectMany(v => v.SourcesConsulted ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            values.SelectMany(v => v.SourcesUnavailable ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            leaves.Sum(v => v.CandidatesFound), leaves.Sum(v => v.ReportsOmitted),
            leaves.Sum(v => v.LocationsOmitted), leaves.Any(v => v.CandidatesNotEvaluated is null)
                ? null : leaves.Sum(v => v.CandidatesNotEvaluated ?? 0), leaves.Sum(v => v.CandidatesExamined),
            leaves.Sum(v => v.ReportsReconciled), leaves.Sum(v => v.LocationsReconciled),
            leaves.Sum(v => v.ProtectedGroups), leaves.Sum(v => v.InsufficientGroups))
            { Components = leaves };
    }

}

// Mantém o estado da busca mesmo quando nenhum achado pôde ser lido.
internal sealed class ResidualReportSet(IReadOnlyList<ResidualAuditReport> reports, ResidualCoverage coverage)
    : IReadOnlyList<ResidualAuditReport>
{
    internal ResidualCoverage Coverage { get; } = coverage;
    internal bool Incomplete => Coverage.Incomplete || reports.Any(r => r.Incomplete);
    public int Count => reports.Count;
    public ResidualAuditReport this[int index] => reports[index];
    public IEnumerator<ResidualAuditReport> GetEnumerator() => reports.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
