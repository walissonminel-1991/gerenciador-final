using System.Globalization;

namespace GerenciadorAplicativos;

internal enum WindowsUpdatePhase { Revalidate, Download, Install }
internal enum WindowsUpdateOutcome
{
    SelectionChanged, LicensePending, ExclusiveUpdate, Busy, RestartPending, PreflightInconclusive,
    Download, Installation, AuthorizationDeclined, NoResult
}

internal readonly record struct WindowsUpdateNativeResult(int Code, int Error, bool? RebootRequired)
{
    internal bool Succeeded => Code == 2 && Error >= 0;
    internal string Description => Code switch
    {
        0 => "Pendente",
        2 when Succeeded => "Concluída",
        3 => "Concluída com erros",
        4 => "Falhou",
        5 => "Cancelada",
        _ => "Resultado inconclusivo"
    };

    internal string ToWire() => FormattableString.Invariant(
        $"{Code},{Error},{(RebootRequired is true ? 1 : RebootRequired is false ? 0 : -1)}");

    internal static bool TryParse(string value, out WindowsUpdateNativeResult result)
    {
        result = default;
        string[] fields = value.Split(',');
        if (fields.Length != 3 ||
            !int.TryParse(fields[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int code) ||
            !int.TryParse(fields[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int error) ||
            !int.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int reboot) ||
            code is < -1 or > 5 || reboot is < -1 or > 1)
            return false;
        result = new(code, error, reboot == -1 ? null : reboot == 1);
        return true;
    }
}

internal sealed record WindowsUpdateOperation(WindowsUpdateOutcome Outcome,
    WindowsUpdateNativeResult Overall, IReadOnlyList<WindowsUpdateNativeResult> Items)
{
    internal const int MaxSelection = 30;
    internal const string PhasePrefix = "WU-PHASE:";
    internal const string ResultPrefix = "WU:";
    internal const string AuthorizationDeclined = "UAC-DECLINED";

    internal bool RequiresRestart => Outcome == WindowsUpdateOutcome.RestartPending ||
        Overall.RebootRequired is true || Items.Any(item => item.RebootRequired is true);

    internal bool FullySucceeded => Overall.Succeeded && Items.Count > 0 && Items.All(item => item.Succeeded);

    internal int ExitCode => Outcome == WindowsUpdateOutcome.Installation && FullySucceeded ? 0 : 1;

    internal static WindowsUpdateOperation Blocked(WindowsUpdateOutcome outcome, int error = 0) =>
        new(outcome, new(-1, error, null), []);

    internal static string PhaseText(WindowsUpdatePhase phase) => phase switch
    {
        WindowsUpdatePhase.Revalidate => "Revalidando as atualizações selecionadas…",
        WindowsUpdatePhase.Download => "Baixando as atualizações pelo Windows Update…",
        _ => "Instalando as atualizações baixadas…"
    };

    internal static string ErrorText(int error) => error == 0 ? "" : $" (0x{error:X8})";

    internal string Summary => Outcome switch
    {
        WindowsUpdateOutcome.SelectionChanged => "As ofertas selecionadas mudaram desde a última consulta.",
        WindowsUpdateOutcome.LicensePending => "Há uma licença pendente. Aceite-a no Windows Update do sistema.",
        WindowsUpdateOutcome.ExclusiveUpdate => "Uma atualização exige instalação individual. Selecione-a separadamente.",
        WindowsUpdateOutcome.Busy => "O Windows Update está ocupado. Aguarde a outra operação terminar.",
        WindowsUpdateOutcome.RestartPending => "A instalação não começou porque o Windows já precisa reiniciar.",
        WindowsUpdateOutcome.PreflightInconclusive => "Não foi possível confirmar as condições da instalação. Nenhuma instalação foi iniciada.",
        WindowsUpdateOutcome.Download => Overall.Code switch
        {
            5 => "Download cancelado. A instalação não foi iniciada.",
            4 => "Falha no download. A instalação não foi iniciada.",
            _ when Overall.Code == -1 => "Download inconclusivo. A instalação não foi iniciada.",
            _ => "Download incompleto. A instalação não foi iniciada."
        },
        WindowsUpdateOutcome.Installation when FullySucceeded => $"O Windows confirmou {Items.Count} instalação(ões).",
        WindowsUpdateOutcome.Installation when Overall.Code == 5 => "O Windows cancelou a instalação. Confira cada atualização.",
        WindowsUpdateOutcome.Installation when Overall.Code == 4 => "O Windows informou falha na instalação. Confira cada atualização.",
        WindowsUpdateOutcome.Installation when Overall.Code == 3 => "Instalação concluída com erros. Confira cada atualização.",
        WindowsUpdateOutcome.Installation => "Instalação inconclusiva. Confira o Windows Update antes de repetir.",
        WindowsUpdateOutcome.AuthorizationDeclined => "Autorização administrativa cancelada. A seleção foi mantida.",
        _ => "Resultado inconclusivo. Consulte o Windows Update antes de repetir a operação."
    };

    // O trabalhador e a interface ordenam as mesmas identidades canônicas.
    // Um único quadro mantém os resultados atômicos e cabe no limite do pipe.
    internal string ToWire() => ResultPrefix +
        string.Join('|', new[] { Outcome.ToString(), Overall.ToWire() }.Concat(Items.Select(item => item.ToWire())));

    internal static WindowsUpdateOperation? FromWire(string payload, int selectionCount)
    {
        string[] fields = payload.Split('|');
        if (selectionCount is < 1 or > MaxSelection || fields.Length < 2 ||
            !Enum.TryParse(fields[0], out WindowsUpdateOutcome outcome) ||
            outcome is < WindowsUpdateOutcome.SelectionChanged or > WindowsUpdateOutcome.Installation ||
            !WindowsUpdateNativeResult.TryParse(fields[1], out var overall))
            return null;

        int itemCount = fields.Length - 2;
        var phaseResult = outcome is WindowsUpdateOutcome.Download or WindowsUpdateOutcome.Installation;
        if (itemCount != 0 && (itemCount != selectionCount || !phaseResult) ||
            itemCount == 0 && phaseResult && overall.Code != -1)
            return null;

        var items = new WindowsUpdateNativeResult[itemCount];
        for (int i = 0; i < items.Length; i++)
            if (!WindowsUpdateNativeResult.TryParse(fields[i + 2], out items[i])) return null;

        return new(outcome, overall, items);
    }
}

internal sealed class WindowsUpdateExchange(int selectionCount, Action<WindowsUpdatePhase> onPhase)
{
    private WindowsUpdateOperation? _result;
    private WindowsUpdatePhase? _phase;
    private bool _invalid;
    private bool _finished;
    private bool _authorizationDeclined;

    internal void Receive(string message)
    {
        if (_finished || _authorizationDeclined) { _invalid = true; return; }
        if (message == WindowsUpdateOperation.AuthorizationDeclined && _phase is null)
            _authorizationDeclined = true;
        else if (message.StartsWith(WindowsUpdateOperation.PhasePrefix, StringComparison.Ordinal) &&
            _result is null &&
            Enum.TryParse(message[WindowsUpdateOperation.PhasePrefix.Length..], out WindowsUpdatePhase phase) &&
            phase <= WindowsUpdatePhase.Install &&
            (int)phase == (_phase is null ? 0 : (int)_phase.Value + 1))
        {
            _phase = phase;
            onPhase(phase);
        }
        else if (message.StartsWith(WindowsUpdateOperation.ResultPrefix, StringComparison.Ordinal) && _result is null)
        {
            _result = WindowsUpdateOperation.FromWire(message[WindowsUpdateOperation.ResultPrefix.Length..], selectionCount);
            if (_result is null) _invalid = true;
        }
        else if (message.StartsWith("DONE|", StringComparison.Ordinal) && _result is { } result &&
            message[5..] == result.ExitCode.ToString(CultureInfo.InvariantCulture) &&
            _phase == (result.Outcome switch
            {
                WindowsUpdateOutcome.Download => WindowsUpdatePhase.Download,
                WindowsUpdateOutcome.Installation => WindowsUpdatePhase.Install,
                _ when _phase == WindowsUpdatePhase.Download &&
                    result.Outcome is WindowsUpdateOutcome.Busy or WindowsUpdateOutcome.RestartPending or
                        WindowsUpdateOutcome.PreflightInconclusive
                    => WindowsUpdatePhase.Download,
                _ => WindowsUpdatePhase.Revalidate
            }))
            _finished = true;
        else
            _invalid = true;
    }

    internal WindowsUpdateOperation Result => !_invalid && _authorizationDeclined
        ? WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.AuthorizationDeclined)
        : !_invalid && _finished && _result is not null
            ? _result : WindowsUpdateOperation.Blocked(WindowsUpdateOutcome.NoResult);
}
