using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace GerenciadorAplicativos;

// Apenas consultas. Nenhuma redefinição, mudança de DNS ou de adaptador.
internal sealed record NetworkCheck(string Name, string State, string Detail);
internal sealed record NetworkScan(IReadOnlyList<NetworkCheck> Checks, bool HasWarnings);

internal static class NetworkDiagnostics
{
    // Falha de um único endereço HTTPS não identifica defeito Winsock.
    // A decisão é somente uma sugestão e exige autorização posterior.
    internal static string? SuggestTargetedRepair(NetworkScan? scan)
    {
        if (scan is null) return null;
        // Uma linha OK não comprova unicidade se existir outra leitura
        // contraditória. Dois nomes DNS distintos são necessários para
        // evitar que um domínio isolado dispare limpeza do cache.
        var adapters = scan.Checks.Where(c => c.Name == "Adaptador").ToArray();
        var gateways = scan.Checks.Where(c => c.Name == "Gateway").ToArray();
        if (adapters.Length != 1 || adapters[0].State != "OK" ||
            gateways.Length != 1 || gateways[0].State != "OK")
            return null;
        var dns = scan.Checks.Where(c => c.Name == "DNS").ToArray();
        var alternateDns = scan.Checks.Where(c =>
            c.Name == "DNS alternativo").ToArray();
        if (dns.Length != 1 || alternateDns.Length != 1) return null;
        if (dns[0].State == "Atenção" && alternateDns[0].State == "Atenção")
            return "flush-dns";
        if (dns[0].State != "OK" || alternateDns[0].State != "OK") return null;
        var primary = scan.Checks.Where(c => c.Name == "Conexão HTTPS").ToArray();
        var secondary = scan.Checks.Where(c =>
            c.Name == "Conexão HTTPS alternativa").ToArray();
        return primary.Length == 1 && secondary.Length == 1 &&
            primary[0].State == "Atenção" && secondary[0].State == "Atenção"
            ? "network-repair-winsock" : null;
    }

    // Resolver somente nomes oficiais de teste; nenhuma alteração em DNS,
    // adaptadores ou cache. Dois nomes não equivalem a dois servidores DNS.
    private static async Task<NetworkCheck> CheckDnsReadOnlyAsync(
        string label, string host)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host)
                .WaitAsync(TimeSpan.FromSeconds(5));
            return addresses.Length > 0
                ? new(label, "OK", "Nome de teste resolvido: " + host + ".")
                : new(label, "Atenção", "Nenhum endereço retornado para " + host +
                    "; não comprova falha do cache DNS.");
        }
        catch (TimeoutException)
        {
            return new(label, "Atenção", "Consulta de " + host +
                " excedeu 5 segundos; rede ou servidor DNS podem estar indisponíveis.");
        }
        catch (SocketException)
        {
            return new(label, "Atenção", "O nome " + host +
                " não resolveu; falha pode ser de rede, firewall ou DNS.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(label, "Info", "Consulta DNS inconclusiva (" +
                ex.GetType().Name + "); nenhuma manutenção indicada.");
        }
    }

    public static async Task<NetworkScan> AnalyzeAsync()
    {
        var checks = new List<NetworkCheck>();
        IPAddress? gateway = null;
        NetworkInterface? mainAdapter = null;
        try
        {
            var available = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToArray();
            mainAdapter = available.FirstOrDefault(n => n.GetIPProperties().GatewayAddresses.Any(
                g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                ?? available.FirstOrDefault();
            if (mainAdapter is null)
            {
                checks.Add(new("Adaptador", "Atenção", "Nenhuma conexão ativa identificada."));
            }
            else
            {
                var props = mainAdapter.GetIPProperties();
                var localIP = props.UnicastAddresses.FirstOrDefault(a =>
                    a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
                gateway = props.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
                checks.Add(new("Adaptador", "OK", mainAdapter.Name +
                    (localIP is null ? " · sem IPv4 identificado" : " · IPv4 " + localIP)));
                checks.Add(new("Gateway", gateway is null ? "Atenção" : "OK",
                    gateway is null ? "Gateway IPv4 não identificado." : "Gateway configurado: " + gateway));
            }
        }
        catch (Exception ex)
        {
            checks.Add(new("Rede local", "Info", "Consulta incompleta: " + ex.GetType().Name));
        }

        if (gateway is not null)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(gateway, 1200);
                checks.Add(new("Resposta do gateway", reply.Status == IPStatus.Success ? "OK" : "Info",
                    reply.Status == IPStatus.Success
                        ? $"Resposta ICMP em {reply.RoundtripTime} ms."
                        : "Sem resposta ICMP (pode ser bloqueada pelo roteador)."));
            }
            catch (Exception ex) when (ex is PingException or InvalidOperationException)
            {
                checks.Add(new("Resposta do gateway", "Info", "Teste ICMP indisponível ou bloqueado."));
            }
        }
        else
        {
            checks.Add(new("Resposta do gateway", "Info", "Teste não realizado: gateway não identificado."));
        }

        checks.Add(await CheckDnsReadOnlyAsync("DNS", "www.microsoft.com"));
        checks.Add(await CheckDnsReadOnlyAsync("DNS alternativo", "www.cloudflare.com"));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("www.microsoft.com", 443)
                .WaitAsync(TimeSpan.FromSeconds(5));
            checks.Add(new("Conexão HTTPS", "OK", "Conexão TCP à porta 443 realizada."));
        }
        catch (TimeoutException)
        {
            checks.Add(new("Conexão HTTPS", "Atenção", "Tempo esgotado. Pode haver bloqueio ou exigência de proxy."));
        }
        catch (Exception ex) when (ex is SocketException or System.Security.SecurityException)
        {
            checks.Add(new("Conexão HTTPS", "Atenção", "Não foi possível estabelecer TCP/443; isso não comprova indisponibilidade geral da Internet."));
        }
        // Um segundo domínio independente impede que bloqueio isolado de
        // www.microsoft.com seja confundido com falha de catálogo Winsock.
        // TCP/443 não faz handshake TLS e não comprova Internet saudável.
        try
        {
            using var alternate = new TcpClient();
            await alternate.ConnectAsync("www.cloudflare.com", 443)
                .WaitAsync(TimeSpan.FromSeconds(5));
            checks.Add(new("Conexão HTTPS alternativa", "OK",
                "TCP/443 do segundo domínio respondeu; um primeiro destino isolado pode estar bloqueado."));
        }
        catch (TimeoutException)
        {
            checks.Add(new("Conexão HTTPS alternativa", "Atenção",
                "Segundo destino TCP/443 sem resposta; pode haver firewall, proxy ou indisponibilidade externa."));
        }
        catch (Exception ex) when (ex is SocketException or System.Security.SecurityException)
        {
            checks.Add(new("Conexão HTTPS alternativa", "Atenção",
                "Segundo destino TCP/443 indisponível; não comprova falha Winsock."));
        }
        return new NetworkScan(checks, checks.Any(c => c.State == "Atenção"));
    }
}
