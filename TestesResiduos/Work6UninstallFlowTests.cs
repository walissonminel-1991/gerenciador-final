using GerenciadorAplicativos;
using System.ComponentModel;
internal static class Work6UninstallFlowTests
{
    private static ProgramEntry App(string id="A",string version="1.0",string? command=null) => new() {
        Name="Fixture "+id, Publisher="Fixture Publisher", Version=version, InventoryId="CurrentUser|Registry64|"+id,
        MsiProductCode=Work6UninstallTests.Product, UninstallString=command??"msiexec.exe /X"+Work6UninstallTests.Product };
    internal static async Task Run(Action<bool,string> check)
    {
        if (OperatingSystem.IsWindows())
        {
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var npp = new ProgramEntry { Name = "Notepad++ (64-bit x64)", Publisher = "Notepad++ Team",
                InventoryId = "LocalMachine|Registry64|npp", InstallLocation = Path.Combine(pf, "Notepad++") };
            check(LabUnsignedUninstallerPolicy.IsMatchingRegisteredLabProduct(npp,
                    Path.Combine(npp.InstallLocation, "uninstall.exe"), "uninstall.exe") &&
                !LabUnsignedUninstallerPolicy.IsMatchingRegisteredLabProduct(npp,
                    Path.Combine(npp.InstallLocation, "update.exe"), "update.exe") &&
                !LabUnsignedUninstallerPolicy.IsMatchingRegisteredLabProduct(npp,
                    Path.Combine(pf, "Microsoft", "uninstall.exe"), "uninstall.exe"),
                "LAB-U: excepcao so admite desinstalador original do produto registrado.");
            var fb = npp with { Name = "foobar2000 v2.26.1 (x64)",
                Publisher = "Peter Pawlowski", InstallLocation = Path.Combine(pf, "foobar2000") };
            check(LabUnsignedUninstallerPolicy.IsMatchingRegisteredLabProduct(fb,
                    Path.Combine(fb.InstallLocation, "uninstall.exe"), "") &&
                !LabUnsignedUninstallerPolicy.IsMatchingRegisteredLabProduct(
                    fb with { Publisher = "Editor Desconhecido" },
                    Path.Combine(fb.InstallLocation, "uninstall.exe"), ""),
                "LAB-U: foobar2000 limitado ao editor informado no inventario.");
            await NativePrepare(check);
        }
        var app=App(); var requests=UninstallBatch.Select([app,app]);
        check(requests.Length==1,"WORK6 lote: seleção duplicada resulta em uma autorização");
        bool rejected=false;
        try { UninstallBatch.Select([app,App(version:"2.0")]); } catch(InvalidDataException) { rejected=true; }
        check(rejected,"WORK6 lote: duplicata contraditória rejeitada");
        check(UninstallCommand.TryParse("MsiExec.exe /I "+Work6UninstallTests.Product+" /qn /norestart",out var msi) &&
            msi.Arguments=="/X"+Work6UninstallTests.Product+" /qn /norestart", "WORK6 parser: manutenção MSI /I converte somente GUID íntegro para /X");
        check(UninstallCommand.TryParse("msiexec.exe /I \""+Work6UninstallTests.Product+"\" /qn",out var quoted) &&
            quoted.ProductCode==Work6UninstallTests.Product && quoted.Arguments=="/X"+Work6UninstallTests.Product+" /qn",
            "W6-U-QUOTED GUID MSI entre aspas é legítimo e mantém vínculo ao produto");
        check(!UninstallBatch.BoundToProduct(msi,new ProgramEntry { MsiProductCode="{00000000-0000-0000-0000-000000000999}" }),
            "WORK6 parser: GUID de outro produto bloqueado");
        foreach(string raw in new[]{@"\\server\share\msiexec.exe /X"+Work6UninstallTests.Product,
            @".\uninstall.exe /S", @"C:\cache.exe folder\uninstall.exe /S", "\"C:\\App\\uninstall.exe\"/S", "\"C:\\App\\uninstall.exe\" /S & cmd.exe",
            "\"C:\\App\\uninstall.exe\" /LOG \"unterminated", @"C:\Windows\System32\powershell.exe -Command fixture" })
            check(!UninstallCommand.TryParse(raw,out _),"WORK6 parser: caminho/sintaxe ambígua não produz intenção executável");
        check(UninstallCommand.TryParse("\"C:\\Program Files\\Vendor\\uninstall.exe\" /LOG \"C:\\A&B.log\"",out _),
            "WORK6 parser: argumento literal citado não é confundido com shell");
        int launches=0, scans=0;
        Task<int?> Launch(UninstallRequest _,CancellationToken token) { launches++; return Task.FromResult<int?>(0); }
        Task Wait(CancellationToken _) => Task.CompletedTask;
        Task<UninstallSnapshot> Scan() { scans++; return Task.FromResult(new UninstallSnapshot([app],true)); }
        var result=await new UninstallBatch().RunAsync(requests,false,Scan,Launch,Wait);
        check(result.Single().State==UninstallState.Refused && launches==0 && scans==0,"WORK6 lote: recusa do usuário produz zero leitura e zero lançamento");
        async Task<IReadOnlyList<UninstallOutcome>> Scenario(UninstallSnapshot[] snapshots,Func<UninstallRequest,CancellationToken,Task<int?>>? launcher=null) {
            int i=0; return await new UninstallBatch().RunAsync(requests,true,()=>Task.FromResult(snapshots[Math.Min(i++,snapshots.Length-1)]),launcher??Launch,Wait);
        }
        var present=new UninstallSnapshot([app],true); var absent=new UninstallSnapshot([],true);
        int before=launches;
        result=await Scenario([present,absent,absent]);
        check(result.Single().State==UninstallState.RegistrationAbsent && launches==before+1,"WORK6 lote: uma execução e duas ausências completas confirmam somente ausência do registro");
        result=await Scenario([present,absent,present]);
        check(result.Single().State==UninstallState.Pending,"W6-U-STABLE ausência transitória seguida de presença não confirma remoção nem falha definitiva");
        before=launches;
        result=await Scenario([new([App(version:"2.0")],true)]);
        check(result.Single().State==UninstallState.Blocked && launches==before,"WORK6 lote: seleção alterada antes do lançamento bloqueia execução");
        result=await Scenario([new([app],false)]);
        check(result.Single().State==UninstallState.Blocked && launches==before,"W6-U-PARTIAL inventário parcial anterior produz zero lançamentos");
        result=await Scenario([present,present,present,present,absent,absent]);
        check(result.Single().State==UninstallState.RegistrationAbsent,
            "R7: fornecedor remove registro tardiamente dentro da janela; duas leituras completas confirmam");
        result=await Scenario([present,new([],false)]);
        check(result.Single().State==UninstallState.Pending,"WORK6 lote: inventário posterior parcial não prova ausência");
        foreach(int code in new[]{2,3,5,193,740,1223}) {
            before=launches; var batch=new UninstallBatch();
            var original=new List<UninstallRequest>(requests);
            result=await batch.RunAsync(original,true,Scan,(_,_)=>{ launches++; throw new Win32Exception(code); },Wait);
            await batch.RunAsync(original,true,Scan,Launch,Wait);
            check(launches==before+1 && result.Single().State==(code==1223 ? UninstallState.Cancelled : UninstallState.StartFailed),
                "WORK6 lote: Win32 "+code+" não dispara nova tentativa");
        }
        before=launches;
        result=await Scenario([present],(_,_)=>{launches++;return Task.FromResult<int?>(null);});
        check(result.Single().State==UninstallState.StartFailed && launches==before+1,"WORK6 lote: Process.Start null não confirma nem repete");
        result=await Scenario([present],(_,_)=>Task.FromResult<int?>(1603));
        check(result.Single().State==UninstallState.ProcessFailed,"WORK6 lote: erro do fornecedor não permite confirmar por ausência");
        var second=App("B"); // mesmo ProductCode: ambiguidade de contexto deve bloquear antes de executar
        result=await Scenario([new([app,second],true)]);
        check(result.Single().State==UninstallState.Blocked,"WORK6 lote: MSI duplicado em outro registro não escolhe contexto implicitamente");
        using var cancel=new CancellationTokenSource();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<UninstallSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate=new UninstallBatch(); before=launches;
        var pending=gate.RunAsync(requests,true,()=>{entered.SetResult();return release.Task;},Launch,Wait,cancel.Token);
        await entered.Task;
        var concurrent=await gate.RunAsync(requests,true,Scan,Launch,Wait);
        cancel.Cancel();release.SetResult(present);
        check((await pending).Single().State==UninstallState.Cancelled && launches==before && concurrent.Count==0,
            "WORK6 lote: cancelamento durante revalidação e concorrência não lançam fornecedor");
        var mutable=new List<UninstallRequest>(requests); entered=new(TaskCreationOptions.RunContinuationsAsynchronously); release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        gate=new(); pending=gate.RunAsync(mutable,true,()=>{entered.TrySetResult();return release.Task;},Launch,Wait);
        await entered.Task; mutable.Clear(); release.SetResult(present);
        result=await pending;
        check(result.Count==1 && result[0].State==UninstallState.Pending,"WORK6 lote: coleção externa modificada não muda seleção autorizada");
        before=launches;
        result=await new UninstallBatch().RunAsync(requests,true,Scan,(_,_)=>{launches++;throw new TimeoutException();},Wait);
        check(result.Single().State==UninstallState.Pending && launches==before+1,"WORK6 lote: fornecedor sem retorno fica pendente, sem sucesso ou repetição");
        await MultiItem(check);
    }
    internal static async Task MultiItem(Action<bool,string> check)
    {
        var first=App();
        const string other="{00000000-0000-0000-0000-000000000456}";
        var second=new ProgramEntry { Name="Fixture B",Publisher="Fixture Publisher",Version="1.0",
            InventoryId="CurrentUser|Registry64|B",MsiProductCode=other,UninstallString="msiexec.exe /X"+other };
        var requests=UninstallBatch.Select([first,second]);
        var both=new UninstallSnapshot([first,second],true); var onlySecond=new UninstallSnapshot([second],true); var empty=new UninstallSnapshot([],true);
        int index=0; var launched=new List<string>();
        var snapshots=new[]{both,onlySecond,onlySecond,onlySecond,empty,empty};
        var result=await new UninstallBatch().RunAsync(requests,true,
            ()=>Task.FromResult(snapshots[Math.Min(index++,snapshots.Length-1)]),
            (r,_)=>{ launched.Add(r.Original.InventoryId); return Task.FromResult<int?>(0); },_=>Task.CompletedTask);
        check(result.Count==2 && result.All(r=>r.State==UninstallState.RegistrationAbsent) &&
            launched.SequenceEqual(new[]{first.InventoryId,second.InventoryId}),
            "WORK6 lote: dois produtos explicitamente selecionados executam uma vez cada e na ordem autorizada");
        foreach(Exception error in new Exception[]{new System.ComponentModel.Win32Exception(1223),new TimeoutException()}) {
            launched.Clear();
            result=await new UninstallBatch().RunAsync(requests,true,()=>Task.FromResult(both),
                (r,_)=>{launched.Add(r.Original.InventoryId);throw error;},_=>Task.CompletedTask);
            check(result.Count==1 && launched.SequenceEqual(new[]{first.InventoryId}),
                "WORK6 lote: UAC cancelado ou timeout no primeiro produto interrompe os demais");
        }
        var changed=new ProgramEntry { Name=second.Name,Publisher=second.Publisher,Version=second.Version,
            InventoryId=second.InventoryId,MsiProductCode=other,UninstallString=second.UninstallString+" /quiet" };
        index=0;launched.Clear();snapshots=[both,onlySecond,onlySecond,new([changed],true)];
        result=await new UninstallBatch().RunAsync(requests,true,()=>Task.FromResult(snapshots[Math.Min(index++,snapshots.Length-1)]),
            (r,_)=>{launched.Add(r.Original.InventoryId);return Task.FromResult<int?>(0);},_=>Task.CompletedTask);
        check(result.Count==2 && result[1].State==UninstallState.Blocked && launched.SequenceEqual(new[]{first.InventoryId}),
            "WORK6 lote: comando do segundo item trocado durante lote não herda autorização antiga");
    }
    private static async Task NativePrepare(Action<bool,string> check)
    {
        string folder=Path.Combine(Path.GetTempPath(),"GF-W6-Uninstall-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            string file=Path.Combine(folder,"uninstall.exe");
            File.WriteAllText(file,"fixture maliciosa não executável");
            var app=new ProgramEntry { Name="Fixture", Publisher="Fixture Publisher", InventoryId="CurrentUser|Registry64|Fixture",
                InstallLocation=folder,UninstallString="\""+file+"\" /S" };
            var request=UninstallBatch.Select([app]).Single();
            bool denied=false;
            try { using var unused=RegisteredUninstaller.Prepare(request,(_,_)=>true); }
            catch(InvalidDataException) { denied=true; }
            check(denied,"WORK6 nativo: arquivo existente com nome uninstall.exe não basta para autorizar");
            bool renamedHostsDenied=true;
            foreach(string host in new[]{"cmd.exe","rundll32.exe","msiexec.exe"}) {
                File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),host),file,true);
                bool refused=false;
                try { using var unused=RegisteredUninstaller.Prepare(request,(_,_)=>true); }
                catch(InvalidDataException) { refused=true; }
                renamedHostsDenied &= refused;
            }
            check(renamedHostsDenied,"WORK6 nativo: hosts Windows renomeados como uninstall.exe continuam bloqueados");
            File.Copy(Environment.ProcessPath!,file,true); // cópia do apphost; jamais executar
            denied=false;
            try { using var unused=RegisteredUninstaller.Prepare(request,(_,_)=>false); }
            catch(System.Security.Cryptography.CryptographicException) { denied=true; }
            check(denied,"WORK6 nativo: PE existente sem identidade assinada autorizada é bloqueado");
            using var prepared=RegisteredUninstaller.Prepare(request,(_,_)=>true); // fronteira criptográfica simulada
            check(prepared.StartInfo.FileName==file && prepared.StartInfo.Arguments=="/S" &&
                prepared.StartInfo.UseShellExecute && string.IsNullOrEmpty(prepared.StartInfo.Verb),
                "WORK6 nativo: intenção contém só caminho validado, argumentos registrados e UAC normal do fornecedor");
            var external=prepared.StartInfo; external.FileName="cmd.exe";
            check(prepared.StartInfo.FileName==file,"WORK6 nativo: cópia da intenção não altera executável autorizado");
            bool pinned=false;
            try { File.Move(file,file+".replaced"); } catch(IOException) { pinned=true; }
            check(pinned,"WORK6 nativo: substituição do EXE entre revisão e lançamento é bloqueada");
            prepared.Dispose(); denied=false;
            try { await prepared.LaunchAsync(CancellationToken.None); }
            catch(InvalidOperationException) { denied=true; }
            check(denied,"WORK6 nativo: plano descartado jamais inicia processo");
        } finally { Directory.Delete(folder,true); }
    }

}
