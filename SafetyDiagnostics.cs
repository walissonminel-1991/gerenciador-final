using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GerenciadorAplicativos;

internal enum FindingLevel { Ok, Info, Attention }
internal sealed record Finding(FindingLevel Level, string Subject, string Detail, int SuggestedTab = -1);
internal sealed record DiagnosticReport(DateTime CollectedAt, IReadOnlyList<Finding> Findings);
internal sealed record CleanupEstimate(string Category, long EligibleBytes, int EligibleFiles, bool Partial);

// Somente leitura: não exige elevação nem aplica quaisquer alterações ao computador.
internal static class SafetyDiagnostics
{
    public static Task<DiagnosticReport> CaptureAsync(int updateCount, int startupCount) => Task.Run(() =>
    {
        var items = new List<Finding>();
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToArray();
            foreach (var d in drives)
            {
                if (d.TotalSize <= 0) continue;
                double freePct = 100d * d.AvailableFreeSpace / d.TotalSize;
                string text = $"{d.AvailableFreeSpace / 1_073_741_824d:N1} GiB livres ({freePct:N0}%)";
                items.Add(new(freePct < 10 ? FindingLevel.Attention : FindingLevel.Ok,
                    "Disco " + d.Name, text, freePct < 10 ? 1 : -1));
            }
            if (drives.Length == 0) items.Add(new(FindingLevel.Info, "Discos", "Nenhuma unidade fixa acessível nesta consulta."));
        }
        catch (Exception ex) { items.Add(new(FindingLevel.Info, "Discos", "Consulta incompleta: " + ex.Message)); }

        items.Add(new(updateCount > 0 ? FindingLevel.Attention : FindingLevel.Ok, "Atualizações de aplicativos",
            updateCount > 0 ? $"{updateCount} pacote(s) sinalizado(s) pelo WinGet." : "Nenhuma atualização identificada na última consulta do WinGet.",
            updateCount > 0 ? 0 : -1));
        items.Add(new(FindingLevel.Info, "Inicialização", startupCount >= 0
            ? $"{startupCount} entrada(s) carregadas nesta sessão. Avalie as desnecessárias manualmente."
            : "Consulte a aba Inicialização para identificar aplicativos dispensáveis.", 4));

        try
        {
            bool reboot = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending") != null ||
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") != null;
            items.Add(new(reboot ? FindingLevel.Attention : FindingLevel.Info, "Reinicialização do Windows",
                reboot ? "Há indicação de reinicialização pendente em componentes do Windows." :
                "Nenhuma pendência encontrada nas duas chaves verificadas; outras fontes podem indicar reinício necessário."));
        }
        catch { items.Add(new(FindingLevel.Info, "Reinicialização", "Estado não disponível neste perfil.")); }

        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
            bool gateway = adapters.Any(n => n.GetIPProperties().GatewayAddresses.Any(g =>
                g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));
            items.Add(new(adapters.Length == 0 ? FindingLevel.Attention : FindingLevel.Ok,
                "Rede local", adapters.Length == 0 ? "Nenhum adaptador ativo identificado." :
                $"{adapters.Length} adaptador(es) ativo(s); " + (gateway ? "gateway IPv4 configurado." : "gateway IPv4 não identificado."),
                adapters.Length == 0 ? 2 : -1));
        }
        catch (Exception ex) { items.Add(new(FindingLevel.Info, "Rede local", "Consulta parcial: " + ex.Message)); }
        return new DiagnosticReport(DateTime.Now, items);
    });

    // DNS externo é um teste auxiliar: falha não prova ausência de internet.
    public static async Task<string> CheckDnsAsync()
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync("www.microsoft.com")
                .WaitAsync(TimeSpan.FromSeconds(5));
            return addresses.Length > 0 ? "DNS externo: nome resolvido corretamente." : "DNS externo: sem endereços retornados.";
        }
        catch (TimeoutException) { return "DNS externo: tempo esgotado (pode haver bloqueio por rede ou firewall)."; }
        catch (Exception ex) { return "DNS externo: indisponível nesta consulta (" + ex.GetType().Name + ")."; }
    }

    public static Task<IReadOnlyList<CleanupEstimate>> EstimateCleanupAsync() => Task.Run<IReadOnlyList<CleanupEstimate>>(() =>
    {
        var result = new List<CleanupEstimate>();
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        foreach (var (label, folder) in new[]
        {
            ("Temporários do usuário", Path.GetTempPath()),
            ("Temporários do Windows", Path.Combine(windows, "Temp")),
            ("Shaders DirectX", Path.Combine(local, "D3DSCache"))
        })
        {
            long size = 0; int count = 0; bool partial = false, truncated = false;
            int visited = 0;
            if (!Directory.Exists(folder)) { result.Add(new(label, 0, 0, false)); continue; }
            var stack = new Stack<string>(); stack.Push(folder);
            while (stack.Count > 0)
            {
                if (++visited > 12000) { truncated = true; break; }
                string current = stack.Pop();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(current))
                    {
                        if (++visited > 12000) { truncated = true; break; }
                        try
                        {
                            var info = new FileInfo(file);
                            if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.ReadOnly | FileAttributes.System)) != 0) continue;
                            if (DateTime.Now - info.LastWriteTime < TimeSpan.FromHours(24)) continue;
                            checked { size += info.Length; }
                            count++;
                        }
                        catch { partial = true; }
                    }
                    if (truncated) break;
                    foreach (string dir in Directory.EnumerateDirectories(current))
                    {
                        try
                        {
                            var attr = File.GetAttributes(dir);
                            if ((attr & FileAttributes.ReparsePoint) == 0) stack.Push(dir);
                        }
                        catch { partial = true; }
                    }
                }
                catch { partial = true; }
            }
            result.Add(new(label, size, count, partial || truncated));
        }
        return result;
    });
}
