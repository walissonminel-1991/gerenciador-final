using GerenciadorFinal.Laboratorio;

using var provider = new RuckZuckCatalog();
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
try
{
    var catalog = await provider.GetCatalogAsync(cancellation.Token);
    Console.WriteLine($"CLIENTE_RUCKZUCK resultado=OK entradas={catalog.Count} sem_dados_do_usuario=sim");
    if (catalog.Count == 0) { Console.WriteLine("CLIENTE_RUCKZUCK AVISO=CATALOGO_VAZIO"); Environment.ExitCode = 1; }
}
catch (Exception ex)
{
    // Mensagens deste provedor são constantes, sem saída bruta ou dados de máquina.
    var message = ex is InvalidDataException ? ex.Message.Replace('\r', ' ').Replace('\n',' ') : "falha-de-consulta";
    Console.WriteLine($"CLIENTE_RUCKZUCK resultado=FALHA tipo={ex.GetType().Name} codigo=0x{ex.HResult:X8} motivo={message}");
    Environment.ExitCode = 1;
}
