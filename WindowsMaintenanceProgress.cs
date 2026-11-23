namespace GerenciadorAplicativos;

// Progresso refletido a partir da saida real da ferramenta (nao estimado).
// Cada etapa ocupa uma fração do total. Sem percentual, não estima avanço.
internal sealed class WindowsMaintenanceProgress
{
    private int stage;
    private int stageCount;
    internal bool Update(string line, out int percentage)
    {
        percentage = 0;
        var stageMarker = System.Text.RegularExpressions.Regex.Match(line, @"\AEtapa ([1-4])/([234])\b");
        if (stageMarker.Success)
        {
            int next = stageMarker.Groups[1].Value[0] - '0';
            int total = stageMarker.Groups[2].Value[0] - '0';
            if (next <= total)
            {
                stage = next;
                stageCount = total;
            }
            return false;
        }
        if (stage == 0) return false;
        foreach (var match in System.Text.RegularExpressions.Regex.Matches(line,
            @"(?<!\d)(\d{1,3})(?:[,.]\d{1,2})?\s*%").Cast<System.Text.RegularExpressions.Match>())
        {
            if (!int.TryParse(match.Groups[1].Value, out int value) || value > 100)
                continue;
            percentage = ((stage - 1) * 100 + value) / stageCount;
            return true;
        }
        return false;
    }
}
