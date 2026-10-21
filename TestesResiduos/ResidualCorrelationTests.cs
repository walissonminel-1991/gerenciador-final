using GerenciadorAplicativos;

internal static class ResidualCorrelationTests
{
    internal static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-Correlation");
        string location = Path.Combine(root, "Editor");
        var evidence = new[]
        {
            new BroadResidualEvidence("NomeAntigo", location, "Perfil", "AppData"),
            new BroadResidualEvidence("NomeAntigo", "ChaveFicticia", "Preferências", "Registry")
        };
        var active = BroadResidualDiscovery.Classify(evidence, [new ProgramEntry { Name = "ProdutoRenomeado", InstallLocation = location }]);
        check(active.Single().Findings.All(f => f.Disposition == ResidualDisposition.InstallationCheck && !f.CanRemove),
            "Reinstalação/renomeação no mesmo caminho impede conclusão de resíduo");
        var paths = new ResidualPathIndex([location]);
        check(paths.Overlaps(Path.Combine(location, "Plugins")) && paths.Overlaps(root) && !paths.Overlaps(location + "Outro"),
            "Índice de caminhos protege ancestrais/descendentes com fronteira exata");
        var ambiguous = BroadResidualDiscovery.Classify(new[]
        {
            evidence[0] with { Container = "Fabricante A" }, evidence[1] with { Container = "Fabricante B" }
        }, []);
        check(ambiguous.Single().Findings.All(f => f.Disposition == ResidualDisposition.Unknown && f.BasisDisposition == ResidualDisposition.PreserveShared),
            "Fabricantes homônimos conservam identidade ambígua e preservam todos os locais");
        var partial = BroadResidualDiscovery.Classify(evidence, [], inventoryIncomplete: true);
        var index = new ResidualResultIndex(partial);
        check(index.Query(filter: ResidualFilter.Data).Matching == 1 && index.Query(source: ResidualSource.Registry).Matching == 1 &&
            partial.Single().Findings.All(f => f.Disposition == ResidualDisposition.Unknown && !f.CanRemove),
            "Busca parcial mantém preservação de dados e origem nos filtros sem autorizar remoção");
        var alias = ApplicationIdentityCatalog.Rules.First(r => r.Id == "windows-terminal");
        var renamed = new[]
        {
            new BroadResidualEvidence("Terminal do Windows", "Perfil", "Perfil", "AppData"),
            new BroadResidualEvidence("Terminal do Windows", "Registro", "Preferências", "Registry")
        };
        check(BroadResidualDiscovery.Classify(renamed, [new ProgramEntry { Name = "Windows Terminal", Publisher = alias.Publishers[0] }]).Count == 0,
            "Aliases oficiais protegem programa instalado sob outro nome sem ampliar catálogo executável");
        var combined = ResidualReportMerger.Merge(new[]
        {
            new ResidualAuditReport("App", false, [new("Local", "Perfil", 1, "A", ResidualDisposition.PreserveData, ResidualSource.UserData)], []),
            new ResidualAuditReport("App", false, [new("Local", "Serviço", 1, "B", ResidualDisposition.PreserveShared, ResidualSource.Integration)], [])
        });
        check(combined.Single().Findings.Single().Sources == (ResidualSource.UserData | ResidualSource.Integration) &&
            combined.Single().Findings.Single().Disposition == ResidualDisposition.PreserveShared,
            "União mantém todas as origens e a decisão mais conservadora do mesmo local");
    }
}
