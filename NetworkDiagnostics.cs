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
    // Estar conectado não basta: adaptador sem IPv4 utilizável não pode
    // autorizar recomendação de flush DNS nem reset Winsock baseado em IPv4.
    // Uma rede IPv6-only pode continuar funcionando, mas este diagnóstico
    // não deve fingir que sua rota IPv4 foi comprovada.
    internal static bool HasUsableIpv4(IPAddress? address)
    {
        if (address is null || address.AddressFamily != AddressFamily.InterNetwork ||
            IPAddress.IsLoopback(address)) return false;
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] is > 0 and < 224 &&
            !(bytes[0] == 169 && bytes[1] == 254);
    }

    // Um gateway informado pelo Windows só comprova configuração IPv4
    // utilizável quando não é zero, loopback, multicast ou APIPA.
    // Nenhuma classificação autoriza correção automática.
    internal static NetworkCheck ClassifyIpv4Gateway(IPAddress? address) =>
        HasUsableIpv4(address)
            ? new("Gateway", "OK", "Gateway configurado: " + address)
            : new("Gateway", "Atenção",
                "Gateway IPv4 utilizável não identificado; não inferir rota válida.");

    // Escolher a rota só quando existe UMA interface com IPv4 local e
    // gateway válidos. Em máquinas multihomed sem métrica de rota consultada,
    // a primeira interface enumerada não prova ser o caminho da Internet.
    // -1 = nenhuma, -2 = múltiplas (ambíguo), >= 0 = índice único.
    internal static int FindUniqueIpv4Route(
        IReadOnlyList<(IPAddress? Local, IPAddress? Gateway)> interfaces)
    {
        int selected = -1;
        for (int i = 0; i < interfaces.Count; i++)
        {
            if (!HasUsableIpv4(interfaces[i].Local) ||
                !HasUsableIpv4(interfaces[i].Gateway)) continue;
            if (selected >= 0) return -2;
            selected = i;
        }
        return selected;
    }

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
        try
        {
            var configured = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Select(n =>
                {
                    var props = n.GetIPProperties();
                    return (Adapter: n,
                        Local: props.UnicastAddresses.Select(a => a.Address)
                            .FirstOrDefault(HasUsableIpv4),
                        Gateway: props.GatewayAddresses.Select(g => g.Address)
                            .FirstOrDefault(HasUsableIpv4));
                })
                .ToArray();
            int route = FindUniqueIpv4Route(configured.Select(x =>
                (x.Local, x.Gateway)).ToArray());
            if (route == -2)
            {
                // DNS/TCP permanecem observáveis, mas não inferem uma rota
                // local única nem habilitam os comandos de reparo.
                checks.Add(new("Adaptador", "Atenção",
                    "Múltiplas interfaces IPv4 com gateway; origem da rota não determinada."));
                checks.Add(new("Gateway", "Atenção",
                    "Mais de uma rota local elegível; nenhuma escolhida por ordem de enumeração."));
            }
            else
            {
                var current = route >= 0 ? configured[route] :
                    configured.FirstOrDefault(x => HasUsableIpv4(x.Gateway));
                if (current.Adapter is null) current = configured.FirstOrDefault();
                if (current.Adapter is null)
                {
                    checks.Add(new("Adaptador", "Atenção",
                        "Nenhuma conexão ativa identificada."));
                }
                else
                {
                    gateway = current.Gateway;
                    checks.Add(new("Adaptador", current.Local is null ? "Atenção" : "OK",
                        current.Adapter.Name + (current.Local is null
                            ? " · sem IPv4 utilizável; uma rede IPv6 pode funcionar"
                            : " · IPv4 " + current.Local)));
                    checks.Add(ClassifyIpv4Gateway(gateway));
                }
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
