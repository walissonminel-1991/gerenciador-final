using GerenciadorAplicativos;

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
