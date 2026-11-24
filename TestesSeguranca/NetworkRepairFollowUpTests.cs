using GerenciadorAplicativos;

internal static class NetworkRepairFollowUpTests
{
    internal static void Run()
    {
        int passed = 0;
        void Check(bool ok, string description)
        {
            if (!ok) throw new InvalidOperationException("VF03 rede: " + description);
            passed++;
            Console.WriteLine("OK VF03 rede: " + description);
        }

        var adapter = new NetworkCheck("Adaptador", "OK", "");
        var gateway = new NetworkCheck("Gateway", "OK", "");
        var dnsOk = new NetworkCheck("DNS", "OK", "");
        var dnsFailed = new NetworkCheck("DNS", "Atenção", "Consulta sem resposta.");
        var portFailed = new NetworkCheck("Conexão TCP/443", "Atenção", "Tempo esgotado.");
        var icmpBlocked = new NetworkCheck("Resposta do gateway", "Info", "Sem resposta ICMP.");

        Check(NetworkDiagnostics.SuggestTargetedRepair(new([adapter, gateway, dnsOk, portFailed], true)) is null &&
            NetworkDiagnostics.SuggestTargetedRepair(new([adapter, gateway, dnsOk, portFailed with { Name = "Conexão HTTPS" }], true)) is null,
            "falha isolada de TCP/443 permanece consultiva com DNS e rede local detectados");
        Check(NetworkDiagnostics.SuggestTargetedRepair(new([adapter, gateway, dnsOk, icmpBlocked], false)) is null,
            "ICMP bloqueado permanece consultivo");
        Check(NetworkDiagnostics.SuggestTargetedRepair(new([adapter, gateway, dnsFailed, dnsFailed with { Name = "DNS alternativo" }, portFailed], true)) == "flush-dns",
            "falha DNS com adaptador e gateway identificados continua oferecendo apenas limpar o cache");
        Check(NetworkDiagnostics.SuggestTargetedRepair(new([gateway, dnsFailed], true)) is null,
            "sem adaptador identificado, o cache DNS permanece fora das ações guiadas");
        Check(NetworkDiagnostics.SuggestTargetedRepair(new([adapter, dnsFailed], true)) is null,
            "sem gateway identificado, o cache DNS permanece fora das ações guiadas");
        Check(NetworkDiagnostics.SuggestTargetedRepair(new([adapter, gateway, portFailed], true)) is null,
            "sem evidência do teste DNS, uma falha TCP/443 permanece consultiva");
        Check(NetworkDiagnostics.SuggestTargetedRepair(null) is null &&
            NetworkDiagnostics.SuggestTargetedRepair(new([adapter, gateway, dnsOk], false)) is null,
            "sem análise ou alerta relevante, nenhuma ação guiada é oferecida");

        var cases = new (string Task, string? ExitCode, bool Expected, string Description)[]
        {
            ("flush-dns", "0", true, "limpeza de DNS confirmada pede nova análise"),
            ("flush-dns", null, false, "DNS sem retorno da operação, incluindo UAC negado"),
            ("flush-dns", "1", false, "falha do comando DNS"),
            ("flush-dns", "", false, "retorno vazio"),
            ("flush-dns", "0 com avisos", false, "retorno fora do formato do worker"),
            ("network-repair-winsock", "0", false, "a tarefa Winsock retirada fica fora da checagem guiada"),
            ("network-reset", "0", false, "redefinição avançada fica fora da correção guiada")
        };
        foreach (var (task, exitCode, expected, description) in cases)
            Check((task == "flush-dns" && NetworkRepairOutcome.FromWire(exitCode is null ? null : "DONE|" + exitCode)
                == NetworkRepairWorkerState.CommandReturnedZero) == expected, description);
        Console.WriteLine($"VF03 rede: {passed} verificações aprovadas.");
    }
}
