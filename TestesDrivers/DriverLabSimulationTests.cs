using GerenciadorAplicativos;

internal static class DriverLabSimulationTests
{
    internal static void Run()
    {
        var cases = DriverLabSimulation.Cases;
        if (cases.Count != 17 || cases.Select(c => c.Key).Distinct().Count() != 17)
            throw new Exception("UX3: fixtures incompletas ou duplicadas.");
        foreach (var c in cases)
        {
            var result = DriverLabSimulation.Format(c);
            if (result != DriverLabSimulation.Format(c) ||
                !result.Contains("CENÁRIO INTEIRAMENTE FICTÍCIO") ||
                !result.Contains("NENHUM DRIVER FOI ALTERADO") ||
                !result.Contains("Journal fictício em memória — NÃO GRAVADO") ||
                !result.Contains("Instalação — NÃO EXECUTADA"))
                throw new Exception("UX3: fixture não determinística ou enganosa: " + c.Key);
        }
        var success = cases.Single(c => c.Key == "printer-success");
        if (!DriverLabSimulation.Format(success).Contains("VERIFICADO SOMENTE NA FICÇÃO") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "staged")).Contains("dispositivo ausente") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "nooffers")).Contains("sem ofertas") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "ambiguous")).Contains("DeviceId ambíguo") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "eula")).Contains("EULA") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "revoked")).Contains("revalidação") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "cancel")).Contains("CANCELADO") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "journal")).Contains("intenção durável") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "ports-reboot")).Contains("REINÍCIO PENDENTE") ||
            !DriverLabSimulation.Format(cases.Single(c => c.Key == "uncertain")).Contains("INCONCLUSIVO"))
            throw new Exception("UX3: cenário não distinguido em modo somente fictício.");
        foreach (string critical in new[] { "MEDIA", "HIDClass", "Display", "Net", "SCSIAdapter", "System", "Firmware" })
        {
            if (DriverUpdatePilot.AllowedClass(critical) ||
                !DriverLabSimulation.Format(success with { DriverClass = critical })
                    .Contains("BLOQUEADO: classe fora"))
                throw new Exception("UX3: classe crítica foi admitida: " + critical);
        }
        if (!DriverUpdatePilot.AllowedClass("Printer") ||
            !DriverUpdatePilot.AllowedClass("Image") ||
            !DriverUpdatePilot.AllowedClass("Ports"))
            throw new Exception("UX3: regras de piloto foram alteradas.");
        Console.WriteLine("OK DRIVERS UX3: 17 cenários fictícios; classes críticas bloqueadas; sem chamadas nativas");
    }
}
