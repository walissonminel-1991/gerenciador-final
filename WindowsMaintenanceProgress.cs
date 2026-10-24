namespace GerenciadorAplicativos;

// Progresso refletido a partir da saida real da ferramenta (nao estimado).
// 0-50 = DISM; 50-100 = SFC. Sem percentual no log, mantem animacao.
internal sealed class WindowsMaintenanceProgress
{
    private int stage;
    internal bool Update(string line, out int percentage)
    {
        percentage = 0;
        if (line.Contains("Etapa 1/2", StringComparison.Ordinal))
        {
            stage = 1;
            return false;
        }
        if (line.Contains("Etapa 2/2", StringComparison.Ordinal))
        {
            stage = 2;
            return false;
        }
        if (stage == 0) return false;
        foreach (var match in System.Text.RegularExpressions.Regex.Matches(line,
            @"(?<!\d)(\d{1,3})(?:[,.]\d{1,2})?\s*%").Cast<System.Text.RegularExpressions.Match>())
        {
            if (!int.TryParse(match.Groups[1].Value, out int value) || value > 100)
                continue;
            percentage = stage == 1 ? value / 2 : 50 + value / 2;
            return true;
        }
        return false;
    }
}
