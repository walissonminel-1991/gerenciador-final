using GerenciadorAplicativos;
using var provider = new RuckZuckCatalog();
using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(60));
try
{
    var catalog = await provider.GetCatalogAsync(cancel.Token);
    Console.WriteLine($"CATALOGO.RUCKZUCK_OK entradas={catalog.Count}");
    if (catalog.Count == 0) return 1;
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"CATALOGO.RUCKZUCK_FALHA tipo={ex.GetType().Name} hresult=0x{ex.HResult:X8}");
    return 1;
}
