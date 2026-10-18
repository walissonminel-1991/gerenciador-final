using GerenciadorAplicativos;

int checks = 0;
void Check(bool ok, string what)
{
    if (!ok) throw new InvalidOperationException("FALHA: " + what);
    Console.WriteLine("OK " + what);
    checks++;
}
string apps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
string terminal = Path.Combine(apps,
    "Microsoft.WindowsTerminal_1.24.12741.0_x64__8wekyb3d8bbwe", "WindowsTerminal.exe");
Check(UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.WindowsTerminal, terminal),
    "Windows Terminal Stable Microsoft x64 permitido");
Check(UpdateProcessCoordinator.ProcessName(UpdateProcessTarget.WindowsTerminal) == "WindowsTerminal",
    "Fechar so WindowsTerminal.exe");
Check(UpdateProcessCoordinator.ProcessName(UpdateProcessTarget.ObsStudio) == "obs64",
    "Fechar so obs64.exe");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.WindowsTerminal,
    Path.Combine(Path.GetTempPath(), "WindowsTerminal.exe")), "Nao encerrar nome Terminal falso em Temp");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.WindowsTerminal,
    Path.Combine(Path.GetTempPath(), "WindowsApps", "Microsoft.WindowsTerminal_1.24.12741.0_x64__8wekyb3d8bbwe", "WindowsTerminal.exe")),
    "Nao confiar em pasta WindowsApps falsa no perfil");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.WindowsTerminal,
    Path.Combine(apps, "Microsoft.WindowsTerminalPreview_1.24.12741.0_x64__8wekyb3d8bbwe", "WindowsTerminal.exe")),
    "Nao encerrar Terminal Preview");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.WindowsTerminal,
    Path.Combine(apps, "Microsoft.WindowsTerminal_1.24.12741.0_x64__8wekyb3d8bbwe", "Explorer.exe")),
    "Nao encerrar Explorer com diretorio parecido");
Check(UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.ObsStudio,
    @"C:\Program Files\obs-studio\bin\64bit\obs64.exe"), "OBS Studio de 64 bits permitido");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.ObsStudio,
    @"C:\Program Files\OBS Studio\Chrome.exe"), "Nao encerrar outros processos");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.ObsStudio,
    @"C:\Users\Public\obs64.exe"), "Nao encerrar OBS falso sem diretorio validado");
Check(UpdateProcessCoordinator.ProcessName(UpdateProcessTarget.MicrosoftEdge) == "msedge",
    "O único navegador tratado é Microsoft Edge");
string edgeX86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
    "Microsoft", "Edge", "Application", "msedge.exe");
string edgeX64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
    "Microsoft", "Edge", "Application", "msedge.exe");
Check(UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.MicrosoftEdge, edgeX86) &&
    UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.MicrosoftEdge, edgeX64),
    "Identificar somente o executável oficial Edge nas instalações padrão");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.MicrosoftEdge,
    Path.Combine(Path.GetTempPath(), "msedge.exe")), "Não fechar Edge falso na pasta temporária");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.MicrosoftEdge,
    @"C:\Program Files\Microsoft\Edge\Application\other.exe"), "Não encerrar outros aplicativos Microsoft");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.MicrosoftEdge,
    @"C:\Windows\System32\msedge.exe"), "Não confiar apenas no nome do processo");
Check(!UpdateProcessCoordinator.IsAllowedExecutablePath(UpdateProcessTarget.WindowsTerminal, null),
    "Caminho sem informacao nao concede permissao");
Console.WriteLine($"{checks}/{checks} testes de encerramento seguro aprovados; nenhum processo foi encerrado.");