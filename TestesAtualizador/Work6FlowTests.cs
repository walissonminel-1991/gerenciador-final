using GerenciadorAplicativos;
using System.ComponentModel;
using System.Runtime.InteropServices;
internal static class Work6FlowTests
{
    internal static async Task RunAsync(Action<bool,string> check)
    {
        int launches=0, confirmations=0;
        Task<int> Launch() { launches++; return Task.FromResult(0); }
        Task<bool> Confirm() { confirmations++; return Task.FromResult(true); }
        var gate = new UpdateLaunchGate();
        check((await gate.RunAsync(() => Task.FromResult(false), Launch, Confirm)).State == UpdateAttemptState.Blocked && launches==0 && confirmations==0,
            "WORK6 fluxo: identidade alterada bloqueia sem lançar nem confirmar");
        using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        gate = new();
        var pending = gate.RunAsync(() => { entered.SetResult(); return release.Task; }, Launch, Confirm, cancel.Token);
        await entered.Task;
        check((await gate.RunAsync(() => Task.FromResult(true), Launch, Confirm)).State == UpdateAttemptState.AlreadyAttempted,
            "WORK6 fluxo: tentativa concorrente rejeitada");
        cancel.Cancel(); release.SetResult(true);
        check((await pending).State == UpdateAttemptState.Cancelled && launches==0,
            "WORK6 fluxo: cancelamento entre revalidação e lançamento produz zero chamadas");
        foreach (int exit in new[] { 0, 3010, 1603 })
        {
            int before = launches, beforeConfirmation=confirmations;
            var result = await new UpdateLaunchGate().RunAsync(() => Task.FromResult(true),
                () => { launches++; return Task.FromResult(exit); }, Confirm);
            check(launches==before+1 && confirmations==beforeConfirmation+(exit==1603 ? 0 : 1) &&
                result.State==(exit==1603 ? UpdateAttemptState.Failed : UpdateAttemptState.Confirmed),
                "WORK6 fluxo: código " + exit + " lança uma vez e condiciona pós-verificação");
        }
        foreach (Exception error in new Exception[] { new Win32Exception(1223), new TimeoutException() })
        {
            int before=launches, beforeConfirmation=confirmations;
            gate = new();
            var result = await gate.RunAsync(() => Task.FromResult(true), () => { launches++; throw error; }, Confirm);
            await gate.RunAsync(() => Task.FromResult(true), Launch, Confirm);
            check(launches==before+1 && confirmations==beforeConfirmation && result.State==
                (error is TimeoutException ? UpdateAttemptState.Indeterminate : UpdateAttemptState.Cancelled),
                "WORK6 fluxo: UAC/timeout não repetem lançamento nem confirmam sucesso");
        }
        check((await new UpdateLaunchGate().RunAsync(() => Task.FromResult(true), Launch,
            () => Task.FromResult(false))).State == UpdateAttemptState.Indeterminate,
            "WORK6 fluxo: exit zero sem versão nova não confirma instalação");
        if (OperatingSystem.IsWindows()) HardLink(check);
    }
    private static void HardLink(Action<bool,string> check)
    {
        string folder=Path.Combine(Path.GetTempPath(), "GF-W6-Link-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            string file=Path.Combine(folder,"package.exe"), alias=Path.Combine(folder,"alias.exe");
            File.WriteAllText(file,"fixture não executável");
            using (InstallerFileGuard.OpenRead(file)) { }
            if (!CreateHardLinkW(alias,file,IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
            bool blocked=false;
            try { using var held=InstallerFileGuard.OpenRead(file); }
            catch (InvalidDataException) { blocked=true; }
            check(blocked,"WORK6 arquivo com hardlink externo não recebe lease de instalador");
        } finally { Directory.Delete(folder,true); }
    }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern bool CreateHardLinkW(string link,string existing,IntPtr security);
}
