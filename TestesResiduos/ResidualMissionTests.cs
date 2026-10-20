using GerenciadorAplicativos;
using Microsoft.Win32;

internal static class ResidualMissionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = new ResidualScanBudget(cancelled.Token);
        check(!stopped.Visit() && stopped.Incomplete && stopped.Entries == 0,
            "Cancelamento impede examinar primeiro item");
        var expired = new ResidualScanBudget(timeout: TimeSpan.Zero);
        check(!expired.Visit() && expired.Incomplete, "Timeout zero encerra sem leitura");
        var capped = new ResidualScanBudget(maxEntries: 2);
        check(capped.Visit() && capped.Visit() && !capped.Visit() && capped.Entries == 2 && capped.Incomplete,
            "Orçamento global limita enumerações de fontes diferentes");
        check(!ResidualReadSafety.IsLocalAbsolute("relative/path") &&
            !ResidualReadSafety.IsLocalAbsolute(@"\\server\share\Folder") &&
            !ResidualReadSafety.IsLocalAbsolute(@"\\?\C:\Folder") &&
            !ResidualReadSafety.IsLocalAbsolute(Path.Combine(Path.GetTempPath(), "..", "Folder")),
            "Leitura recusa caminho relativo, UNC, dispositivo e traversal");
        if (OperatingSystem.IsWindows())
        {
            check(!ResidualReadSafety.IsLocalAbsolute(@"C:\Folder\file:stream"), "ADS recusado");
            check(BroadResidualDiscovery.ExecutableName("\"C:\\Apps\\LegacyEditor.exe\" --arg") == "LegacyEditor" &&
                BroadResidualDiscovery.ExecutableName(@"C:\Apps\LegacyEditor.exe --arg") == "LegacyEditor",
                "Inicialização correlaciona executável absoluto sem executar argumentos");
            check(BroadResidualDiscovery.ExecutableName("powershell -command anything") is null &&
                BroadResidualDiscovery.ExecutableName(@"\\server\share\LegacyEditor.exe") is null &&
                BroadResidualDiscovery.ExecutableName("\"C:\\Apps\\LegacyEditor.exe\"evil") is null,
                "Comandos de shell, rede e aspas ambíguas não viram identidade");
        }
        string temp = Path.Combine(Path.GetTempPath(), "GF-Mission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            string vendor = Path.Combine(temp, "Fabricante Desconhecido");
            string product = Path.Combine(vendor, "LegacyEditor");
            string third = Path.Combine(product, "NivelNaoVarrer");
            Directory.CreateDirectory(third);
            File.WriteAllText(Path.Combine(product, "playlist.m3u"), "dados ficticios");
            var found = new List<BroadResidualEvidence>();
            int entries = 0, restricted = 0;
            bool partial = false;
            BroadResidualDiscovery.InspectDirectory(temp, "Dados", "AppData", found,
                ref entries, ref restricted, ref partial, new ResidualScanBudget());
            check(found.Any(e => e.Name == "LegacyEditor" && e.Container == "Fabricante Desconhecido") &&
                found.All(e => e.Name != "NivelNaoVarrer") && !partial,
                "Segundo nível encontra produto sob fabricante desconhecido sem percorrer perfil");
            check(File.Exists(Path.Combine(product, "playlist.m3u")), "Descoberta preserva playlist");
            var many = Path.Combine(temp, "MuitosProdutos");
            Directory.CreateDirectory(many);
            for (int i = 0; i < 150; i++) Directory.CreateDirectory(Path.Combine(many, "Produto" + i));
            found.Clear(); entries = 0; restricted = 0; partial = false;
            var smallBudget = new ResidualScanBudget(maxEntries: 30);
            BroadResidualDiscovery.InspectDirectory(temp, "Dados", "AppData", found,
                ref entries, ref restricted, ref partial, smallBudget);
            check(partial && smallBudget.Entries <= 30 && entries <= 30,
                "Pastas extensas interrompem com resultado parcial e contagem limitada");
            found.Clear(); entries = 0; restricted = 0; partial = false;
            BroadResidualDiscovery.InspectDirectory(temp + "-ausente", "Dados", "AppData", found,
                ref entries, ref restricted, ref partial, new ResidualScanBudget());
            check(partial && restricted == 1 && found.Count == 0, "Raiz inacessível/ausente não equivale a ausência de resíduos");
            string linked = Path.Combine(temp, "VinculoFabricante");
            Directory.CreateSymbolicLink(linked, vendor);
            found.Clear(); entries = 0; restricted = 0; partial = false;
            BroadResidualDiscovery.InspectDirectory(linked, "Dados", "AppData", found,
                ref entries, ref restricted, ref partial, new ResidualScanBudget());
            check(partial && found.Count == 0 && !ResidualReadSafety.IsUnlinkedDirectory(Path.Combine(linked, "LegacyEditor")),
                "Link e ancestral redirecionado recusados na leitura");
            Directory.Delete(linked); // somente link sintético
        }
        finally { Directory.Delete(temp, true); }

        if (!OperatingSystem.IsWindows()) return;
        string keyPath = @"Software\GF_Mission_" + Guid.NewGuid().ToString("N");
        try
        {
            using (var root = Registry.CurrentUser.CreateSubKey(keyPath))
            using (var product = root.CreateSubKey(@"Fabricante Exemplo\LegacyEditor"))
                product.SetValue("Preferencias", "ficticias");
            var found = new List<BroadResidualEvidence>();
            int entries = 0, restricted = 0; bool partial = false;
            BroadResidualDiscovery.InspectRegistryHive(RegistryHive.CurrentUser, RegistryView.Default,
                "HKEY_CURRENT_USER", found, ref entries, ref restricted, ref partial,
                new ResidualScanBudget(), keyPath);
            check(found.Any(e => e.Name == "LegacyEditor" && e.Container == "Fabricante Exemplo") && !partial,
                "Registro: produto em subchave de fabricante encontrado somente leitura");
            using var stillThere = Registry.CurrentUser.OpenSubKey(keyPath + @"\Fabricante Exemplo\LegacyEditor");
            check((string?)stillThere?.GetValue("Preferencias") == "ficticias", "Valores do Registro permanecem intactos");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); }
    }
}
