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

        bool resolved = false;
        try
        {
            var addresses = await Dns.GetHostAddressesAsync("www.microsoft.com")
                .WaitAsync(TimeSpan.FromSeconds(5));
            resolved = addresses.Length > 0;
            checks.Add(new("DNS", resolved ? "OK" : "Atenção",
                resolved ? "Nome de teste resolvido corretamente." : "O servidor DNS não retornou endereços."));
        }
        catch (TimeoutException)
        {
            checks.Add(new("DNS", "Atenção", "Consulta DNS excedeu 5 segundos."));
        }
        catch (Exception ex) when (ex is SocketException or System.Security.SecurityException)
        {
            checks.Add(new("DNS", "Atenção", "Não foi possível resolver o nome de teste (rede, firewall ou DNS)."));
        }

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
        return new NetworkScan(checks, checks.Any(c => c.State == "Atenção"));
    }
}
