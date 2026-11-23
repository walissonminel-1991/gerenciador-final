namespace GerenciadorAplicativos;

// Interpretacao conservadora: codigos zero nao comprovam ausencia/corrupcao.
// Idioma nao reconhecido deixa reparo bloqueado, nunca presume integridade.
internal enum IntegrityState { Inconclusive, Healthy, Repairable }

internal enum WindowsRepairRebootState { Clear, Pending, Inconclusive }

// Diagnóstico separado de cada componente: um subsistema íntegro não
// comprova o outro e saídas não reconhecidas continuam inconclusivas.
internal sealed record WindowsIntegrityDetails(
    IntegrityState ComponentStore, IntegrityState ProtectedFiles,
    IntegrityState Overall)
{
    internal bool FullyVerified => Overall == IntegrityState.Healthy &&
        ComponentStore == IntegrityState.Healthy && ProtectedFiles == IntegrityState.Healthy;
    internal bool HasConfirmedDamage =>
        ComponentStore == IntegrityState.Repairable ||
        ProtectedFiles == IntegrityState.Repairable;
    internal string Summary =>
        "DISM/Component Store: " + ComponentStore + "; SFC/arquivos protegidos: " +
        ProtectedFiles + "; resultado conjunto: " + Overall +
        ". Nenhuma conclusão deriva apenas do código de saída.";
}
internal static class WindowsIntegrityAssessment
{
    // Duas fontes oficiais de reinício; uma exceção de leitura nunca vira
    // "liberado". Não interpreta o estado de serviços ou drivers.
    internal static WindowsRepairRebootState AssessReboot(bool? servicing, bool? windowsUpdate) =>
        servicing == true || windowsUpdate == true
            ? WindowsRepairRebootState.Pending
            : servicing == false && windowsUpdate == false
                ? WindowsRepairRebootState.Clear
                : WindowsRepairRebootState.Inconclusive;

    internal static WindowsRepairRebootState InspectRebootReadOnly()
    {
        if (!OperatingSystem.IsWindows()) return WindowsRepairRebootState.Inconclusive;
        try
        {
            using var cbs = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            using var wu = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            return AssessReboot(cbs is not null, wu is not null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return WindowsRepairRebootState.Inconclusive;
        }
    }

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
    internal static WindowsIntegrityDetails Assess(int dismExit, string dismOutput,
        int sfcExit, string sfcOutput)
    {
        string d = Normalize(dismOutput), s = Normalize(sfcOutput);
        bool dBad = HasAny(d, "component store is repairable",
            "armazenamento de componentes pode ser reparado",
            "armazenamento de componentes e reparavel");
        bool dGood = HasAny(d, "no component store corruption detected",
            "nenhuma corrupcao do armazenamento de componentes detectada",
            "nenhuma corrupcao do repositorio de componentes detectada");
        bool sBad = HasAny(s, "windows resource protection found integrity violations",
            "protecao de recursos do windows encontrou violacoes de integridade");
        bool sGood = HasAny(s, "windows resource protection did not find any integrity violations",
            "protecao de recursos do windows nao encontrou nenhuma violacao de integridade",
            "protecao de recursos do windows nao encontrou violacoes de integridade");
        IntegrityState One(int exit, bool good, bool bad) =>
            exit != 0 || good == bad ? IntegrityState.Inconclusive :
            bad ? IntegrityState.Repairable : IntegrityState.Healthy;
        return new(One(dismExit, dGood, dBad), One(sfcExit, sGood, sBad),
            Interpret(dismExit, dismOutput, sfcExit, sfcOutput));
    }

    // Resultado de verificacao POS-REPARO, distinto de exit code do DISM/SFC.
    // Apenas Healthy permite relatar integridade comprovada pela dupla consulta.
    internal static string PostRepairWire(IntegrityState state) => state switch
    {
        IntegrityState.Healthy => "WINREPAIR:HEALTHY",
        IntegrityState.Repairable => "WINREPAIR:REPAIRABLE",
        _ => "WINREPAIR:INCONCLUSIVE"
    };

    internal static bool TryReadPostRepairWire(string? value, out IntegrityState state)
    {
        state = value switch
        {
            "WINREPAIR:HEALTHY" => IntegrityState.Healthy,
            "WINREPAIR:REPAIRABLE" => IntegrityState.Repairable,
            _ => IntegrityState.Inconclusive
        };
        return value is "WINREPAIR:HEALTHY" or "WINREPAIR:REPAIRABLE" or "WINREPAIR:INCONCLUSIVE";
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
