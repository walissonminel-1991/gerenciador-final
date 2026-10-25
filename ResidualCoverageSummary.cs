using Microsoft.Win32;

namespace GerenciadorAplicativos;

// Presentation only: observations never grant authority to delete.
internal static class ResidualCoverageSummary
{
    internal static string Describe(IntegratedResidualPlan plan)
    {
        var scope = IntegratedResidualPolicy.Build(plan.Context.App);
        var lines = new List<string>
        {
            $"Escopo previsto: {scope.Roots.Count} pasta(s), {scope.Keys.Count(k => k.Hive == RegistryHive.CurrentUser)} local(is) HKCU e {scope.Keys.Count(k => k.Hive == RegistryHive.LocalMachine)} local(is) HKLM.",
            $"Pastas com identidade anterior comprovada: {plan.Context.Roots.Count}/{scope.Roots.Count}.",
            "Locais fora da política não foram verificados. Isso não comprova ausência de outros vestígios."
        };
        foreach (var phase in new[] { "prepare", "capture" })
        {
            var checks = (phase == "prepare" ? plan.Context.RegistryChecks : plan.RegistryChecks) ?? [];
            if (checks.Count == 0) continue;
            foreach (var hive in checks.GroupBy(c => (c.Hive, c.View)))
            {
                string label = hive.Key.Hive == RegistryHive.CurrentUser ? "HKCU (usuário atual)" : "HKLM (máquina)";
                lines.Add($"Registro {(phase == "prepare" ? "antes" : "depois")}: {label}, {(hive.Key.View == RegistryView.Registry32 ? 32 : 64)} bits — " +
                    $"{hive.Count(c => c.State == "verified")} lido(s), {hive.Count(c => c.State == "absent")} ausente(s), " +
                    $"{hive.Count(c => c.State == "preserved")} preservado(s), {hive.Count(c => c.State == "unreadable")} inacessível(is).");
            }
        }
        lines.Add($"Exclusões propostas: {plan.Eligible}. Itens preservados: {plan.Preserved}. A limpeza exige sua confirmação e nova validação.");
        return string.Join(Environment.NewLine, lines);
    }

    internal static string RegistryProblem(ResidualProblem problem)
    {
        string scope = problem.Stage.Contains("hklm", StringComparison.Ordinal) ? "HKLM (máquina)" :
            problem.Stage.Contains("hkcu", StringComparison.Ordinal) ? "HKCU (usuário atual)" : "Registro";
        string step = problem.Detail switch
        {
            "open_parent" => "abertura de uma chave pai", "open_leaf" => "abertura da chave final",
            "link_check" => "verificação de vínculo", "enumerate_values" => "listagem de valores",
            "read_value" => "leitura de valor", "enumerate_subkeys" => "listagem de subchaves",
            "open_child" => "abertura de subchave", "fingerprint_budget" => "limite de leitura",
            _ => "leitura"
        };
        return $"{scope}: falha na {step}" + (problem.Depth >= 0 ? $" (nível {problem.Depth})" : "") +
            (problem.Error == 5 ? ": acesso negado pelo Windows (erro 5)." : ".") +
            " Local preservado; ausência não comprovada.";
    }
}
