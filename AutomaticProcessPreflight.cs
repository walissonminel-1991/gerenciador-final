using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
namespace GerenciadorAplicativos;

internal static class AutomaticProcessPreflight
{
    private static bool Owned(Process process,string binary)
    {
        try
        {
            using var current=Process.GetCurrentProcess();
            if(process.SessionId!=current.SessionId || process.HasExited || !string.Equals(process.MainModule?.FileName,binary,StringComparison.OrdinalIgnoreCase))return false;
            if(!OpenProcessToken(process.Handle,8,out var token))return false;
            using(token)using(var owner=new WindowsIdentity(token.DangerousGetHandle()))using(var me=WindowsIdentity.GetCurrent())
                return owner.User==me.User;
        }
        catch(Exception ex)when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException){return false;}
    }
    internal static async Task<bool> ReadyAsync(string binary,Func<string,bool> consent,Action<string> report,CancellationToken token=default)
    {
        var processes=Process.GetProcessesByName(Path.GetFileNameWithoutExtension(binary));
        try
        {
            var live=processes.Where(p=>{try{return !p.HasExited;}catch(InvalidOperationException){return false;}}).ToArray();
            var relevant=live.Where(p=>{try{return p.MainModule?.FileName is not { } path || path.Equals(binary,StringComparison.OrdinalIgnoreCase);}catch(System.ComponentModel.Win32Exception){return true;}catch(InvalidOperationException){return false;}}).ToArray();
            var ours=relevant.Where(p=>Owned(p,binary)).ToArray();
            if(relevant.Length!=ours.Length){report("Feche o aplicativo manualmente: identidade ou usuario do processo nao confirmados.");return false;}
            if(ours.Length>0)
            {
                if(!consent("O aplicativo está aberto. Salve seu trabalho.\n\nPermitir que o GF solicite o fechamento normal antes de atualizar?"))return false;
                foreach(var p in ours){token.ThrowIfCancellationRequested();if(Owned(p,binary))p.CloseMainWindow();}
                for(int pass=0;pass<20;pass++)
                {if(ours.All(p=>{try{return p.HasExited;}catch(InvalidOperationException){return true;}}))break;await Task.Delay(250,token);}
                if(ours.Any(p=>{try{return !p.HasExited;}catch(InvalidOperationException){return false;}}))
                {report("O aplicativo continua aberto. Conclua ou cancele seus diálogos e feche-o antes de atualizar.");return false;}
            }
            return true;
        }
        finally{foreach(var process in processes)process.Dispose();}
    }
    [DllImport("advapi32.dll",SetLastError=true)]private static extern bool OpenProcessToken(IntPtr process,uint access,out SafeAccessTokenHandle token);
}
