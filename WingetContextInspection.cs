using System.Security.Principal;
using Windows.Management.Deployment;

namespace GerenciadorAplicativos;

internal static class WingetContextInspection
{
    // Windows.Management.Deployment apenas inspeciona o registro MSIX do usuário atual.
    // Nunca ativa Microsoft.Management.Deployment, testa executável ou repara aliases.
    internal static WingetContextFacts Read()
    {
        bool registered = false, complete = true, elevated = false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            var manager = new PackageManager();
            int count = 0;
            foreach (var package in manager.FindPackagesForUser(""))
            {
                if (++count > 2000) { complete = false; break; }
                if (package.Id.Name == "Microsoft.DesktopAppInstaller" &&
                    package.Id.PublisherId == "8wekyb3d8bbwe") registered = true;
            }
        }
        catch (Exception) { complete = false; }
        bool alias = false, onPath = false;
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps");
            alias = File.Exists(Path.Combine(directory, "winget.exe"));
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (path.Length > 32768) complete = false;
            else onPath = path.Split(';').Any(p => p.Trim().Trim('"').TrimEnd('\\')
                .Equals(directory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception) { complete = false; }
        return new(registered, alias, onPath, elevated, complete);
    }
}

internal sealed partial class MainForm
{
    private bool _wingetContextAttempted;
    private async Task InspectWingetContextForTestAsync()
    {
        if (_wingetContextAttempted || !TestDiagnosticDetail.Enabled || IsDisposed) return;
        _wingetContextAttempted = true;
        try
        {
            // Não bloquear UI. Sem nova tentativa/elevação se a enumeração nativa não terminar.
            var facts = await Task.Run(WingetContextInspection.Read).WaitAsync(TimeSpan.FromSeconds(5));
            if (IsDisposed || !TestDiagnosticDetail.Enabled) return;
            TestDiagnosticDetail.Record("WINGET_CONTEXTO",
                ("pacote", facts.PackageRegistered ? 1 : 0), ("alias", facts.AliasPresent ? 1 : 0),
                ("path", facts.AliasDirectoryOnPath ? 1 : 0), ("elevado", facts.Elevated ? 1 : 0),
                ("parcial", facts.InventoryComplete ? 0 : 1));
            Log("Contexto WinGet (sem executar): " + WingetContextPolicy.Classify(facts) +
                ". Presença não confirma API, assinatura ou necessidade de administrador.");
            Log(WingetConsultativeProvider.ProductionStatus);
        }
        catch (TimeoutException)
        {
            if (!IsDisposed) Log("Contexto WinGet não concluído no prazo; provedor continua desativado.");
        }

    }
}
