namespace GerenciadorAplicativos;

// Interpretacao conservadora: codigos zero nao comprovam ausencia/corrupcao.
// Idioma nao reconhecido deixa reparo bloqueado, nunca presume integridade.
internal enum IntegrityState { Inconclusive, Healthy, Repairable }
internal static class WindowsIntegrityAssessment
{
    internal static IntegrityState Interpret(int dismExit, string dismOutput,
        int sfcExit, string sfcOutput)
    {
        if (dismExit != 0 || sfcExit != 0) return IntegrityState.Inconclusive;
        string d = Normalize(dismOutput), s = Normalize(sfcOutput);
        bool dismProblem = HasAny(d, "component store is repairable",
            "armazenamento de componentes pode ser reparado",
            "armazenamento de componentes e reparavel");
        bool dismGood = HasAny(d, "no component store corruption detected",
            "nenhuma corrupcao do armazenamento de componentes detectada",
            "nenhuma corrupcao do repositorio de componentes detectada");
        bool sfcProblem = HasAny(s, "windows resource protection found integrity violations",
            "protecao de recursos do windows encontrou violacoes de integridade");
        bool sfcGood = HasAny(s, "windows resource protection did not find any integrity violations",
            "protecao de recursos do windows nao encontrou nenhuma violacao de integridade",
            "protecao de recursos do windows nao encontrou violacoes de integridade");
        if ((dismProblem && !dismGood) || (sfcProblem && !sfcGood))
            return IntegrityState.Repairable;
        if (dismGood && sfcGood && !dismProblem && !sfcProblem)
            return IntegrityState.Healthy;
        return IntegrityState.Inconclusive;
    }
    private static bool HasAny(string s, params string[] a) => a.Any(s.Contains);
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var chars = new System.Text.StringBuilder();
        foreach (char c in normalized)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
                chars.Append(char.ToLowerInvariant(c));
        return chars.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
