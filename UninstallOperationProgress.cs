namespace GerenciadorAplicativos;

internal enum UninstallStage { Preparing, AwaitingConsent, Working, Confirming, Inspecting, AwaitingCleanup, Cleaning, Finished, Partial, Pending, Failed, Cancelled }
internal sealed record UninstallOperationSnapshot(UninstallStage Stage, string Text, string Details)
{
    internal bool Active => Stage < UninstallStage.Finished;
    internal bool PreventRetry => Active || Stage == UninstallStage.Pending;
}

// A desinstalacao nao fornece uma porcentagem confiavel. Todos os estados
// ativos usam animacao indeterminada; nenhum deles fabrica progresso numerico.
internal sealed class UninstallOperationStore
{
    private readonly object gate = new();
    private readonly Action<UninstallOperationSnapshot>? onFinished;
    internal UninstallOperationStore(Action<UninstallOperationSnapshot>? onFinished = null)
        => this.onFinished = onFinished;
    private readonly Dictionary<string, UninstallOperationSnapshot> operations = new(StringComparer.Ordinal);
    private sealed record KeyHolder(string Value);
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ProgramEntry, KeyHolder> identities = new();
    private string Key(ProgramEntry app) => identities.GetValue(app, p => new(string.Concat(new[] {
        p.InventoryId, p.Name, p.Publisher, p.InstallLocation, p.MsiProductCode,
        p.UninstallString, p.QuietUninstallString }.Select(s => s.ToUpperInvariant()).Select(s => s.Length + ":" + s)))).Value;
    internal UninstallOperationSnapshot? Find(ProgramEntry app)
    { lock (gate) return operations.GetValueOrDefault(Key(app)); }
    internal void Set(ProgramEntry app, UninstallStage stage, string? details = null)
    {
        string text = stage switch
        {
            UninstallStage.Preparing => "Preparando desinstalação...",
            UninstallStage.AwaitingConsent => "Aguardando sua confirmação...",
            UninstallStage.Working => "Desinstalador trabalhando...",
            UninstallStage.Confirming => "Conferindo remoção...",
            UninstallStage.Inspecting => "Verificando resíduos...",
            UninstallStage.AwaitingCleanup => "Aguardando decisão sobre resíduos...",
            UninstallStage.Cleaning => "Limpando resíduos autorizados...",
            UninstallStage.Finished => "Concluída — veja o resumo",
            UninstallStage.Partial => "Desinstalação concluída · limpeza parcial",
            UninstallStage.Pending => "Conclusão pendente — confira o resumo",
            UninstallStage.Failed => "Falha — confira o resumo",
            _ => "Operação cancelada"
        };
        lock (gate)
        {
            string key = Key(app);
            if (!operations.ContainsKey(key) && operations.Count >= 256)
            {
                string? retired = operations.FirstOrDefault(p => !p.Value.PreventRetry).Key;
                if (retired is null) return;
                operations.Remove(retired);
            }
            var snapshot = new UninstallOperationSnapshot(stage, text, details ?? text);
            operations[key] = snapshot;
            if (!snapshot.Active) { try { onFinished?.Invoke(snapshot); } catch { /* histórico opcional */ } }
        }
    }
}

internal static class UninstallResultSummary
{
    internal static string Problem(ResidualProblem problem) => problem.Reason switch
    {
        "registry_unreadable" or "registry_unverifiable" => problem.Error == 5
            ? "Parte do Registro não pôde ser lida: acesso negado pelo Windows (erro 5)."
            : "Não foi possível conferir parte do Registro.",
        "personal_or_shared_root" or "shared_root" => "Pasta pessoal ou compartilhada preservada.",
        "personal_folder_preserved" => "Pastas de dados pessoais preservadas.",
        "personal_or_unknown_file" => "Arquivos pessoais ou de tipo não reconhecido preservados.",
        "link_preserved" => "Atalhos e vínculos de arquivos preservados por segurança.",
        "ownership_not_proven" => "Local encontrado sem comprovação de pertencer exclusivamente ao aplicativo; preservado.",
        "root_without_preuninstall_identity" => "Pasta encontrada sem identidade anterior comprovada; preservada.",
        "coverage_unsupported" => "Este aplicativo ainda não tem locais de resíduos comprovados. A ausência de resíduos não foi confirmada.",
        "application_running" => "O aplicativo ou um processo auxiliar ainda está aberto.",
        "application_present_or_unknown" => "A remoção ou o inventário ainda não pôde ser confirmado.",
        "scan_budget" or "scan_depth" => "A verificação atingiu seu limite e ficou parcial.",
        _ => problem.Error is 32 or 33 ? "Um arquivo ainda está em uso pelo Windows ou por outro programa."
            : problem.Error == 5 ? "Um local não pôde ser acessado (erro 5). As permissões foram preservadas."
            : "Um local não pôde ser conferido com segurança; verificação parcial."
    };

    internal static string Describe(IReadOnlyList<UninstallOutcome> outcomes, ResidualBatchResult? batch)
    {
        var lines = new List<string>();
        foreach (var outcome in outcomes)
        {
            var app = ResidualAppIdentity.From(outcome.Request.Original);
            lines.Add(app.Name);
            if (outcome.State != UninstallState.RegistrationAbsent)
            {
                lines.Add(outcome.State switch
                {
                    UninstallState.Refused or UninstallState.Cancelled => "Desinstalação cancelada. Nenhuma limpeza foi autorizada.",
                    UninstallState.Pending => "Conclusão da desinstalação pendente. O fornecedor pode continuar trabalhando. Não repita a operação agora.",
                    _ => "A desinstalação não foi concluída. Nenhuma limpeza foi autorizada."
                });
                if (outcome.Reason.Length > 0) lines.Add(outcome.Reason);
                if (outcome.Code is int code) lines.Add("Código informado: " + code);
                lines.Add(""); continue;
            }
            lines.Add("Desinstalação confirmada.");
            var plan = batch?.Inspections?.SingleOrDefault(p => p.Context.App.TechnicalId == app.TechnicalId);
            var result = batch?.Results.SingleOrDefault(r => r.AppId == app.TechnicalId);
            if (result is not null)
            {
                lines.Add(batch!.Declined ? "Você escolheu NÃO. Os resíduos foram preservados."
                    : $"Limpeza: {result.Files} arquivo(s), {result.Directories} pasta(s) e {result.Keys} chave(s) removidos.");
                if (!batch.Declined && !result.Verified) lines.Add("Limpeza parcial ou interrompida. Alguns alvos não foram removidos.");
                if (result.Preserved > 0) lines.Add($"Itens preservados: {result.Preserved}.");
                lines.AddRange(result.Problems.Select(Problem).Distinct());
                foreach (var failure in result.Items.Where(i => i.Error != 0).DistinctBy(i => i.Error))
                    lines.Add(Problem(new("delete", failure.Reason, failure.Error)));
            }
            if (plan is null) lines.Add("Verificação de resíduos indisponível ou interrompida; ausência não comprovada.");
            else
            {
                if (!plan.Conclusive) lines.Add("Verificação parcial ou inconclusiva. Isso não significa ausência de resíduos.");
                else if (plan.Assessment == ResidualAssessment.None) lines.Add("Nenhum resíduo encontrado nos locais verificados.");
                else if (result is null && plan.Assessment == ResidualAssessment.Preserved)
                    lines.Add($"Resíduos encontrados e preservados por segurança: {plan.Preserved}.");
                else if (result is null) lines.Add($"Resíduos removíveis encontrados: {plan.Eligible}. Limpeza não executada.");
                lines.AddRange(plan.Problems.Select(Problem).Distinct());
            }
            lines.Add("");
        }
        if (batch?.Cancelled == true) lines.Add("Verificação ou limpeza interrompida pelo cancelamento.");
        lines.Add("Detalhes técnicos registrados no diagnóstico.");
        return string.Join(Environment.NewLine, lines);
    }
}
