using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

internal enum AudioOutputState { DefaultAvailable, NoActiveOutput, NoDefaultOutput, Inconclusive }

internal sealed record AudioOutputDiagnosis(AudioOutputState State, uint? ActiveOutputs = null, int? ErrorCode = null)
{
    internal string Summary => State switch
    {
        AudioOutputState.DefaultAvailable => "O Windows identificou uma saída padrão para multimídia.",
        AudioOutputState.NoActiveOutput => "O Windows não encontrou saídas de áudio ativas.",
        AudioOutputState.NoDefaultOutput => "Há saídas ativas, mas nenhuma saída padrão para multimídia foi identificada.",
        _ => "A consulta das saídas de áudio ficou inconclusiva."
    };

    internal string Guidance => State switch
    {
        AudioOutputState.DefaultAvailable => "Se ainda estiver sem som, confira o volume do aplicativo e da saída selecionada em Configurações > Sistema > Som.",
        AudioOutputState.NoActiveOutput => "Confira se os fones ou alto-falantes estão conectados e habilitados em Configurações > Sistema > Som. Depois, repita o diagnóstico.",
        AudioOutputState.NoDefaultOutput => "Escolha uma saída em Configurações > Sistema > Som e execute o diagnóstico novamente.",
        _ => "Confira as saídas em Configurações > Sistema > Som e tente consultar novamente."
    };

    internal string Describe()
    {
        string count = ActiveOutputs is uint active ? $"Saídas ativas observadas: {active}.\n" : "";
        string code = ErrorCode is int error ? $"Código da consulta: 0x{error:X8}.\n" : "";
        return $"Diagnóstico de áudio\n\n{Summary}\n{count}{code}\n{Guidance}";
    }
}

internal static class AudioOutputDiagnostic
{
    internal const int EndpointNotFound = unchecked((int)0x80070490);
    private const int Render = 0;
    private const int Multimedia = 1;
    private const uint ActiveDevice = 1;

    internal static AudioOutputDiagnosis Evaluate(uint activeOutputs, int defaultStatus, bool hasEndpoint)
    {
        if (defaultStatus == 0 && hasEndpoint && activeOutputs > 0)
            return new(AudioOutputState.DefaultAvailable, activeOutputs);
        if (defaultStatus == EndpointNotFound && !hasEndpoint)
            return new(activeOutputs == 0 ? AudioOutputState.NoActiveOutput : AudioOutputState.NoDefaultOutput,
                activeOutputs);

        // A enumeração e a consulta da saída padrão são observações distintas.
        // Divergência ou falha da API indica resultado inconclusivo.
        return new(AudioOutputState.Inconclusive, activeOutputs, defaultStatus < 0 ? defaultStatus : null);
    }

    internal static AudioOutputDiagnosis Read()
    {
        if (!OperatingSystem.IsWindows()) return new(AudioOutputState.Inconclusive);

        INativeEnumerator? enumerator = null;
        INativeCollection? devices = null;
        nint endpoint = 0;
        try
        {
            enumerator = (INativeEnumerator)new NativeEnumerator();
            int status = enumerator.EnumAudioEndpoints(Render, ActiveDevice, out devices);
            if (status != 0 || devices is null)
                return Inconclusive(status);
            status = devices.GetCount(out uint activeOutputs);
            if (status != 0)
                return Inconclusive(status);
            status = enumerator.GetDefaultAudioEndpoint(Render, Multimedia, out endpoint);
            return Evaluate(activeOutputs, status, endpoint != 0);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return Inconclusive(ex.HResult);
        }
        finally
        {
            if (endpoint != 0) Marshal.Release(endpoint);
            if (devices is not null) Marshal.ReleaseComObject(devices);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    private static AudioOutputDiagnosis Inconclusive(int status) =>
        new(AudioOutputState.Inconclusive, ErrorCode: status < 0 ? status : null);

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class NativeEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INativeEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint stateMask, out INativeCollection? devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out nint endpoint);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INativeCollection
    {
        [PreserveSig] int GetCount(out uint count);
    }
}
