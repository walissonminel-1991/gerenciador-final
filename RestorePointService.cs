using System;
using System.Runtime.InteropServices;

namespace GerenciadorAplicativos;

// API oficial do Windows. O Windows pode limitar frequência, ou a proteção pode estar desativada.
// Nunca informar "ponto criado" apenas porque a chamada foi iniciada.
internal static class RestorePointService
{
    private const int BeginSystemChange = 100;
    private const int EndSystemChange = 101;
    private const int ModifySettings = 12;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RestorePointInfo
    {
        public int EventType;
        public int RestorePointType;
        public long SequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Description;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StatusManager
    {
        public long SequenceNumber;
        public int Status;
    }

    [DllImport("srclient.dll", CharSet = CharSet.Unicode, EntryPoint = "SRSetRestorePointW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SRSetRestorePoint(ref RestorePointInfo input, out StatusManager result);

    public static int Request(Action<string> log)
    {
        try
        {
            var begin = new RestorePointInfo
            {
                EventType = BeginSystemChange, RestorePointType = ModifySettings,
                SequenceNumber = 0, Description = "Gerenciador Final - antes da manutencao"
            };
            bool accepted = SRSetRestorePoint(ref begin, out var state);
            if (!accepted || state.Status != 0)
            {
                log($"Windows não confirmou a solicitação do ponto de restauração. Status {state.Status}, Win32 {Marshal.GetLastWin32Error()}.");
                log("Confira se a Proteção do Sistema está habilitada e se a política do computador permite essa operação.");
                return 1;
            }
            var end = new RestorePointInfo
            {
                EventType = EndSystemChange, RestorePointType = ModifySettings,
                SequenceNumber = state.SequenceNumber, Description = "Gerenciador Final - fim da operacao"
            };
            bool finalized = SRSetRestorePoint(ref end, out var ending);
            if (!finalized || ending.Status != 0)
            {
                log("O Windows iniciou mas não confirmou o encerramento do ponto de restauração.");
                return 1;
            }
            log("Solicitação aceita pelo Windows. Pode ter sido reutilizado um ponto recente por limite de frequência.");
            log("Um ponto de restauração não substitui backup dos arquivos pessoais.");
            return 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            log("Serviço de restauração não disponível: " + ex.Message);
            return 1;
        }
    }
}
