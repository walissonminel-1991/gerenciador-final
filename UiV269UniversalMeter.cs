namespace GerenciadorAplicativos;

// Contrato para adoção pelo WORK, sem alterar telas nem iniciar operações.
// Bytes reais e progresso confirmado; não deduz sucesso do percentual.
internal static class GfUniversalMeter
{
    internal static GfMeterFrame Disk(ulong? usedBytes, ulong? capacityBytes, V269Theme theme)
    {
        double? percent = usedBytes is ulong used && capacityBytes is ulong capacity && capacity > 0 && used <= capacity
            ? used * 100d / capacity : null;
        return GfMeterScale.Frame(GfMeterKind.Usage, percent, theme);
    }

    internal static GfMeterFrame Update(UpdateProgress progress, V269Theme theme)
    {
        var state = progress.Stage switch
        {
            UpdateStage.Confirmed => GfMeterState.Success,
            UpdateStage.Failed => GfMeterState.Failed,
            UpdateStage.Cancelled or UpdateStage.AuthorizationDeclined => GfMeterState.Cancelled,
            UpdateStage.Pending or UpdateStage.Blocked => GfMeterState.Unavailable,
            _ => GfMeterState.Running
        };
        return Operation(progress.Percent, state, true, theme);
    }

    internal static GfMeterFrame Uninstall(UninstallStage stage, V269Theme theme)
    {
        var state = stage switch
        {
            UninstallStage.Finished => GfMeterState.Success,
            UninstallStage.Failed => GfMeterState.Failed,
            UninstallStage.Cancelled => GfMeterState.Cancelled,
            UninstallStage.Partial or UninstallStage.Pending => GfMeterState.Unavailable,
            _ => GfMeterState.Indeterminate
        };
        return Operation(null, state, true, theme);
    }

    internal static GfMeterFrame Operation(double? reportedPercent, GfMeterState state, bool inverted, V269Theme theme)
    {
        var kind = inverted ? GfMeterKind.InvertedProgress : GfMeterKind.Progress;
        if (state == GfMeterState.Running && !GfMeterScale.Valid(reportedPercent, kind))
            state = GfMeterState.Indeterminate;
        return GfMeterScale.Frame(kind, reportedPercent, theme, state: state);
    }
}
