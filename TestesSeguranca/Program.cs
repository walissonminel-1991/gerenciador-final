using GerenciadorAplicativos;

if (args.Length == 2 && args[0] == "--probe-protected")
{
    try { using var write = new FileStream(args[1], FileMode.Open, FileAccess.Write, FileShare.Read); Environment.ExitCode = 1; }
    catch (IOException) { Environment.ExitCode = 0; }
    return;
}

static void Check(bool ok, string why)
{
    if (!ok) throw new Exception("Seguranca: " + why);
}

Check(SecurityCenterHealth.Interpret("Antivirus", 0, 0).Level == SecurityHealthLevel.Ok,
    "protecao ativa");
foreach (int state in new[] {2, 3})
    Check(SecurityCenterHealth.Interpret("Antivirus", 0, state).Level ==
        SecurityHealthLevel.Attention, "protecoes suspensas exigem atencao");
foreach (var (hr, state) in new[] {(1, 2), (unchecked((int)0x80004005), 0), (0, 1), (0, 99)})
    Check(SecurityCenterHealth.Interpret("Antivirus", hr, state).Level ==
        SecurityHealthLevel.Unknown, "resultado incerto nunca e diagnostico de malware");
var results = SecurityCenterHealth.Capture();
Check(results.Count == 2, "antivirus e firewall devem ser consultados");
Check(results[0].Category == "Protecao antivirus" &&
      results[1].Category == "Protecao de firewall", "categorias");
Check(results.All(x => Enum.IsDefined(x.Level) && x.Description.Length > 0),
    "retorno sempre seguro");
Console.WriteLine("OK: consulta agregada WSC somente leitura e mapeamento conservador.");

foreach (string invalid in new[] { "cmd.exe", "powershell.exe", @"C:\Fake\net.exe", @"..\net.exe", "net.exe /stop", "https://example.com/net.exe" })
{
    bool denied = false;
    try { WindowsSystemTools.Resolve(invalid); }
    catch (ArgumentException) { denied = true; }
    Check(denied, "comando/caminho arbitrario bloqueado: " + invalid);
}
if (OperatingSystem.IsWindows())
{
    string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
    foreach (string name in new[] { "powercfg.exe", "net.exe", "cleanmgr.exe", "dism.exe", "sfc.exe", "ipconfig.exe", "netsh.exe", "defrag.exe", "taskmgr.exe" })
    {
        string resolved = WindowsSystemTools.Resolve(name);
        Check(Path.IsPathFullyQualified(resolved) && Path.GetDirectoryName(resolved) == system &&
            Path.GetFileName(resolved) == name,
            "ferramenta vinculada a pasta do Windows sem execucao: " + name);
    }
}
Console.WriteLine("OK: ferramentas internas limitadas ao caminho de sistema, sem executar comandos administrativos.");
if (OperatingSystem.IsWindows())
{
    string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
    foreach (var (registered, expected) in new[]
    {
        ("MsiExec.exe", "msiexec.exe"), ("msiexec", "msiexec.exe"),
        ("rundll32", "rundll32.exe"), ("RUNDLL32.EXE", "rundll32.exe"), ("cmd.exe", "cmd.exe")
    })
        Check(WindowsSystemTools.ResolveRegisteredUninstaller(registered) == Path.Combine(system, expected),
            "desinstalador Windows registrado sem caminho vincula a System32: " + registered);
    foreach (string invalid in new[] { "uninstall.exe", @".\msiexec.exe", @"..\uninstall.exe", "tool.exe /run" })
    {
        bool denied = false;
        try { WindowsSystemTools.ResolveRegisteredUninstaller(invalid); }
        catch (InvalidDataException) { denied = true; }
        Check(denied, "desinstalador relativo nao pesquisa PATH ou pasta portatil: " + invalid);
    }
    string absolute = Path.Combine(Path.GetTempPath(), "GF-Vendor", "uninstall.exe");
    Check(WindowsSystemTools.ResolveRegisteredUninstaller(absolute) == absolute,
        "caminho absoluto registrado do fabricante e preservado sem executar");
}
SafeTemporaryCleanupTests.Run();

// A fixture usa bytes no formato da API, e nao a ordem dos campos gerenciados.
// Nenhuma chamada a SRSetRestorePoint e feita nesta bateria.
Check(System.Runtime.InteropServices.Marshal.SizeOf<RestorePointService.StatusManager>() == 12,
    "STATEMGRSTATUS segue pack(1) do cabecalho Windows SDK");
IntPtr nativeStatus = System.Runtime.InteropServices.Marshal.AllocHGlobal(12);
try
{
    System.Runtime.InteropServices.Marshal.WriteInt32(nativeStatus, 0, 1058);
    System.Runtime.InteropServices.Marshal.WriteInt64(nativeStatus, 4, 0x123456789ABC);
    var status = System.Runtime.InteropServices.Marshal.PtrToStructure<RestorePointService.StatusManager>(nativeStatus);
    Check(status.Status == 1058 && status.SequenceNumber == 0x123456789ABC,
        "STATEMGRSTATUS preserva codigo de erro e sequencia nativos independentes");
}
finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(nativeStatus); }
var restoreLog = new List<string>();
int restoreCalls = 0;
bool SyntheticRestore(ref RestorePointService.RestorePointInfo input,
    out RestorePointService.StatusManager result)
{
    restoreCalls++;
    Check(input.EventType == (restoreCalls == 1 ? 100 : 101), "restauracao respeita inicio e fim");
    Check(input.SequenceNumber == (restoreCalls == 1 ? 0 : 0x123456789ABC),
        "encerramento recebe exatamente a sequencia devolvida pela API");
    result = new() { Status = 0, SequenceNumber = 0x123456789ABC };
    return true;
}
Check(RestorePointService.Request(restoreLog.Add, SyntheticRestore) == 0 && restoreCalls == 2,
    "solicitacao simulada so conclui depois do encerramento confirmado");
restoreLog.Clear();
restoreCalls = 0;
bool FailedRestore(ref RestorePointService.RestorePointInfo input,
    out RestorePointService.StatusManager result)
{
    restoreCalls++;
    result = new() { Status = 1058, SequenceNumber = 0 };
    return false;
}
Check(RestorePointService.Request(restoreLog.Add, FailedRestore) != 0 && restoreCalls == 1 &&
    restoreLog.All(line => !line.Contains("Solicitação aceita", StringComparison.Ordinal)),
    "falha ao iniciar nao tenta encerrar ou anuncia ponto de restauracao");
restoreCalls = 0;
bool FailedEnd(ref RestorePointService.RestorePointInfo input,
    out RestorePointService.StatusManager result)
{
    restoreCalls++;
    result = new() { Status = restoreCalls == 1 ? 0 : 13, SequenceNumber = 123 };
    return true;
}
Check(RestorePointService.Request(_ => { }, FailedEnd) != 0 && restoreCalls == 2,
    "status nativo de erro no encerramento nao e convertido em sucesso");
Console.WriteLine("OK: ABI e fluxo de restauracao com bytes/retornos sinteticos, sem alterar Windows.");

string guardFolder = Path.Combine(Path.GetTempPath(), "GF-File-Guard-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(guardFolder);
try
{
    // Gravacao atomica precisa permitir criar/renomear arquivos filhos,
    // mantendo a propria pasta protegida contra renomeacao.
    using (var writableDirectory = InstallerFileGuard.PinDirectory(guardFolder, allowLeafWrites: true))
    {
        string created = Path.Combine(guardFolder, "atomic.tmp");
        File.WriteAllText(created, "fixture");
        File.Move(created, Path.Combine(guardFolder, "atomic.json"));
        File.Delete(Path.Combine(guardFolder, "atomic.json"));
        bool renamed = false;
        try { Directory.Move(guardFolder, guardFolder + "-moved"); }
        catch (IOException) { renamed = true; }
        Check(renamed, "gravacao dos filhos permitida sem liberar renomeacao da pasta");
    }
    string childFolder = Path.Combine(guardFolder, "private-stage");
    Directory.CreateDirectory(childFolder);
    string childFixture = Path.Combine(childFolder, "installer.bin");
    File.WriteAllText(childFixture, "fixture");
    using (var childGuard = InstallerFileGuard.OpenRead(childFixture))
    {
        string sibling = Path.Combine(guardFolder, "unrelated.tmp");
        File.WriteAllText(sibling, "outro aplicativo");
        File.Move(sibling, Path.Combine(guardFolder, "unrelated.json"));
        File.Delete(Path.Combine(guardFolder, "unrelated.json"));
        bool ancestorBlocked = false;
        try { Directory.Move(guardFolder, guardFolder + "-moved"); }
        catch (IOException) { ancestorBlocked = true; }
        Check(ancestorBlocked, "ancestral nao renomeia sem impedir arquivos de outros aplicativos");
    }
    Directory.Delete(childFolder, true);
    string fixture = Path.Combine(guardFolder, "fixture.bin");
    await File.WriteAllTextAsync(fixture, "original");
    string digest;
    using (var initial = InstallerFileGuard.OpenRead(fixture))
        digest = await InstallerFileGuard.DigestAsync(initial);
    await File.WriteAllTextAsync(fixture, "alterado");
    bool changed = false;
    using (var replaced = InstallerFileGuard.OpenRead(fixture))
    {
        try { await InstallerFileGuard.RequireDigestAsync(replaced, digest); }
        catch (System.Security.Cryptography.CryptographicException) { changed = true; }
    }
    Check(changed, "adulteracao entre verificacoes e detectada");
    if (OperatingSystem.IsWindows())
    {
        using (var pinned = InstallerFileGuard.OpenRead(fixture))
        {
            bool writeBlocked = false, deleteBlocked = false;
            try { await File.WriteAllTextAsync(fixture, "intruso"); }
            catch (IOException) { writeBlocked = true; }
            try { File.Delete(fixture); }
            catch (IOException) { deleteBlocked = true; }
            bool renameBlocked = false;
            try { Directory.Move(guardFolder, guardFolder + "-moved"); }
            catch (IOException) { renameBlocked = true; }
            Check(renameBlocked, "pasta ancestral nao pode ser renomeada durante a verificacao");
            Check(writeBlocked && deleteBlocked, "arquivo em validacao nao compartilha escrita/exclusao");
        }
        // O teste cria somente um link para uma fixture, nunca segue/exclui dados do usuario.
        string linked = Path.Combine(guardFolder, "linked.bin");
        bool canLink = false;
        try { File.CreateSymbolicLink(linked, fixture); canLink = true; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { }
        if (canLink)
        {
            bool linkBlocked = false;
            try { using var rejected = InstallerFileGuard.OpenRead(linked); }
            catch (InvalidDataException) { linkBlocked = true; }
            Check(linkBlocked, "arquivo redirecionado rejeitado");
            File.Delete(linked);
        }
        else Console.WriteLine("SKIP: criacao de symlink indisponivel no runner; nao equivale a teste aprovado.");

        string redirect = Path.Combine(guardFolder, "redirect");
        try
        {
            Directory.CreateSymbolicLink(redirect, guardFolder);
            bool rejected = false;
            try { using var invalid = InstallerFileGuard.OpenRead(Path.Combine(redirect, "fixture.bin")); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "ancestral redirecionado rejeitado pelo handle");
            Directory.Delete(redirect);
        }
        catch (UnauthorizedAccessException) { Console.WriteLine("SKIP: symlink de pasta sem permissao."); }

        foreach (string invalid in new[] { @"\\server\share\setup.exe", @"\\?\C:\setup.exe", @"C:\setup.exe:payload", "setup.exe" })
        {
            bool rejected = false;
            try { InstallerFileGuard.ValidateLocalPath(invalid); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "namespace remoto, dispositivo, ADS ou relativo recusado");
        }
        using (var session = new InstallerFileGuard.SessionProtection())
        {
            // Simula retorno do processo pai: o proprietario da trava agora e a sessao.
            session.Retain(InstallerFileGuard.OpenRead(fixture));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            bool stillProtected = false;
            try { File.Delete(fixture); }
            catch (IOException) { stillProtected = true; }
            Check(stillProtected, "trava permanece apos transferencia para a sessao e coleta");
            var probeInfo = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
                { UseShellExecute = false, CreateNoWindow = true };
            probeInfo.ArgumentList.Add("--probe-protected");
            probeInfo.ArgumentList.Add(fixture);
            using var probe = System.Diagnostics.Process.Start(probeInfo)!;
            using var probeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await probe.WaitForExitAsync(probeTimeout.Token);
            Check(probe.ExitCode == 0, "outro processo tambem nao consegue escrever no pacote retido");
        }
        await File.WriteAllTextAsync(fixture, "liberado ao encerrar a sessao");
        // DisposeAsync tambem precisa liberar todos os ancestrais.
        await using (var asyncLease = InstallerFileGuard.OpenRead(fixture)) { }
        Directory.Move(guardFolder, guardFolder + "-moved");
        Directory.Move(guardFolder + "-moved", guardFolder);
        Console.WriteLine("OK: ancestrais presos, namespaces recusados e protecao retida pela sessao.");

        string pipeName = "GF-Peer-Test-" + Guid.NewGuid().ToString("N");
        using var pipeServer = new System.IO.Pipes.NamedPipeServerStream(
            pipeName, System.IO.Pipes.PipeDirection.In, 1,
            System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
        Check(!PipePeerVerifier.IsExpectedClient(pipeServer, Environment.ProcessId),
            "pipe desconectado nao autentica cliente");
        using var pipeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waiting = pipeServer.WaitForConnectionAsync(pipeTimeout.Token);
        using var pipeClient = new System.IO.Pipes.NamedPipeClientStream(".", pipeName,
            System.IO.Pipes.PipeDirection.Out, System.IO.Pipes.PipeOptions.Asynchronous);
        await pipeClient.ConnectAsync(pipeTimeout.Token);
        await waiting;
        Check(PipePeerVerifier.IsExpectedClient(pipeServer, Environment.ProcessId),
            "kernel confirma PID real do cliente conectado");
        Check(!PipePeerVerifier.IsExpectedClient(pipeServer, int.MaxValue),
            "outro PID nao e aceito mesmo com pipe conectado");
    }
}
finally { Directory.Delete(guardFolder, recursive: true); }
Console.WriteLine("OK: hash de arquivo alterado e bloqueios de compartilhamento.");

if (OperatingSystem.IsWindows())
{
    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
    var policy = assembly.GetCustomAttributes(
        typeof(System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute), false)
        .Cast<System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute>().Single();
    Check(policy.Paths == System.Runtime.InteropServices.DllImportSearchPath.System32,
        "P/Invoke limitado a DLLs do Windows");
    string planted = Path.Combine(AppContext.BaseDirectory, "rstrtmgr.dll");
    bool created = false;
    try
    {
        using (var fixtureDll = new FileStream(planted, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            created = true;
            fixtureDll.Write(System.Text.Encoding.ASCII.GetBytes("fixture-invalida-nao-carregar"));
        }
        IntPtr module = System.Runtime.InteropServices.NativeLibrary.Load("rstrtmgr.dll", assembly, null);
        try
        {
            string actual = NativeModuleProbe.PathOf(module);
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            Check(string.Equals(Path.GetDirectoryName(actual), system, StringComparison.OrdinalIgnoreCase),
                "DLL homonima ao lado do teste nao substitui a biblioteca do sistema");
        }
        finally { System.Runtime.InteropServices.NativeLibrary.Free(module); }
    }
    finally { if (created) File.Delete(planted); }
}
Console.WriteLine("OK: busca nativa restrita ao sistema, com fixture homonima.");
