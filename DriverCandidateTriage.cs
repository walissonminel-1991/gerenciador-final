using System.Text;

namespace GerenciadorAplicativos;

// EXCLUSIVAMENTE texto consultivo em memória. Não autoriza, não inicia
// operações WUA, não lê hardware ou arquivos e não exporta identificadores.
internal static class DriverCandidateTriage
{
    internal static string ClassDisposition(string? reportedClass)
    {
        string reported = DriverInventory.NormalizeDriverClass(reportedClass);
        if (string.IsNullOrEmpty(reported))
            return "NÃO CLASSIFICADO: classe do driver não informada; exigir identificação adicional.";
        if (reported.Equals("MEDIA", StringComparison.OrdinalIgnoreCase) ||
            reported.Equals("HIDClass", StringComparison.OrdinalIgnoreCase) ||
            reported.Equals("AudioEndpoint", StringComparison.OrdinalIgnoreCase) ||
            reported.Equals("Camera", StringComparison.OrdinalIgnoreCase))
            return "SOMENTE CONSULTA: esta classe do driver não integra o piloto WUA permitido.";
        if (new[] { "System", "Firmware", "Net", "Display", "DiskDrive",
            "SCSIAdapter", "HDC", "USB", "USBDevice", "Keyboard", "Mouse",
            "Computer", "Processor", "Volume", "StorageVolume", "Battery",
            "SecurityDevices", "Monitor" }.Contains(reported, StringComparer.OrdinalIgnoreCase))
            return "EXCLUÍDO DO LABORATÓRIO: classe crítica ou dependência essencial.";
        if (DriverUpdatePilot.AllowedClass(reported))
            return "GRUPO DE ESTUDO: classe do driver da lista restrita Printer/Image/Ports; não comprova elegibilidade WUA.";
        return "SOMENTE CONSULTA: classe não classificada como grupo de estudo nesta versão.";
    }

    internal static string Format(DriverInventoryResult inventory,
        DriverInventoryEntry device, DriverOfficialResult? official = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(device);
        var text = new StringBuilder("TRIAGEM DO PC FRACO — APENAS CONSULTA LOCAL");
        text.AppendLine();
        text.AppendLine("Esta orientação NÃO é uma recomendação de instalar, remover ou reverter drivers.");
        string reportedClass = DriverInventory.NormalizeDriverClass(device.ReportedDriverClass);
        text.AppendLine("Classe do driver (WMI SignedDriver): " +
            (reportedClass.Length == 0 ? "não informada" : reportedClass) + ".");
        string disposition = ClassDisposition(reportedClass);
        text.AppendLine("Grupo de segurança consultivo: " + disposition);
        text.AppendLine("Presença PnP relatada: " + device.PresenceLabel + ".");
        text.AppendLine("Versão instalada relatada (WMI): " +
            (string.IsNullOrWhiteSpace(device.Version) ? "não informada" : device.Version) + ".");
        text.AppendLine("Fornecedor relatado (WMI): " +
            (string.IsNullOrWhiteSpace(device.Provider) ? "não informado" : device.Provider) + ".");
        text.AppendLine("Assinatura relatada (WMI): " + device.SignatureLabel +
            " (NÃO substitui verificação criptográfica do INF).");
        text.AppendLine("Associação Win32_PnPSignedDriver: " +
            (device.WithoutSignedDriverAssociation ? "não observada; NÃO comprova falta de driver." :
            "linha relatada; NÃO comprova integridade do pacote."));
        text.AppendLine("INF relatado por Win32_PnPSignedDriver: " +
            (string.IsNullOrWhiteSpace(device.InfName) ? "não informado" : device.InfName) + ".");
        text.AppendLine("Vínculo Driver Store: " +
            (device.PackageEvidence?.Label ?? "não consultado ou não informado; não comprova ausência.") + ".");

        if (inventory.PublishedPackages is { } published)
        {
            int stagedWithoutDevice = published.Packages.Count(p => p.ObservedDevices == 0);
            text.AppendLine($"INFs publicados sem dispositivo observado na enumeração: {stagedWithoutDevice}; " +
                (published.Partial || !published.EnumerationComplete ?
                    "enumeração parcial/inconclusiva." : "não significa pacote órfão."));
        }
        else text.AppendLine("INFs publicados: fonte não consultada, não inferir ausência.");

        int repeated = inventory.Items.Count(d =>
            string.Equals(d.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase));
        bool ambiguous = string.IsNullOrWhiteSpace(device.DeviceId) || repeated != 1;
        if (ambiguous)
            text.AppendLine("IDENTIDADE BLOQUEADA: DeviceId ausente ou duplicado no inventário INTEGRAL.");
        if (inventory.Partial)
            text.AppendLine("INVENTÁRIO PARCIAL: faltam evidências; nenhuma conclusão negativa é autorizada.");
        if (device.Present != true)
            text.AppendLine("PRESENÇA NÃO CONFIRMADA: pacote no Store não equivale a dispositivo instalado.");
        if (device.WithoutSignedDriverAssociation)
            text.AppendLine("VÍNCULO WMI AUSENTE: PnP 28 isolado não demonstra versão mais nova disponível.");
        if (device.PackageEvidence?.State is DriverStoreState.Inconsistent or DriverStoreState.Unavailable)
            text.AppendLine("VÍNCULO DRIVER STORE INCONCLUSIVO: preservar pacotes e investigar em leitura.");

        bool sharedHardwareId = device.HardwareIds.Any(h =>
            inventory.Items.Any(other =>
                !ReferenceEquals(other, device) && other.Present == true &&
                other.HardwareIds.Contains(h, StringComparer.OrdinalIgnoreCase)));
        if (sharedHardwareId)
            text.AppendLine("HARDWAREID COMPARTILHADO: exigir oferta/instância única; não escolher pela primeira linha.");

        if (official is null)
            text.AppendLine("WUA: consulta ainda não solicitada; nenhuma atualização foi comprovada.");
        else if (inventory.Partial || official.Partial)
            text.AppendLine("WUA: consulta ou inventário PARCIAL; ausência de oferta NÃO comprovada.");
        else if (official.SearchContext is not { Source: "Windows Update", Online: true })
            text.AppendLine("WUA: origem online Windows Update NÃO COMPROVADA; não confiar em zero ofertas.");
        else if (official.Offered == 0 && official.Matches.Count == 0)
            text.AppendLine("WUA: ZERO ofertas observadas nesta busca concluída; não significa driver atualizado.");
        else
        {
            var matches = official.Matches.Where(m =>
                string.Equals(m.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0)
                text.AppendLine($"WUA: {official.Offered} ofertas observadas; nenhuma correspondência PnP exata para esta instância.");
            else
            {
                text.AppendLine($"WUA: {matches.Length} vínculos consultivos desta instância; DriverVer ofertado NÃO COMPROVADO.");
                foreach (var match in matches.Take(5))
                {
                    string reason = ambiguous || disposition.StartsWith("EXCLUÍDO", StringComparison.Ordinal)
                        ? "BLOQUEADO: identidade PnP ambígua ou classe excluída do laboratório."
                        : DriverOfferReview.Explain(inventory, official, match);
                    text.AppendLine("Oferta WUA — " + reason);
                }
                if (matches.Length > 5) text.AppendLine("Outros vínculos omitidos no resumo; nenhum é autorizado por esta tela.");
            }
        }
        text.AppendLine("Conclusão: o Windows Update não informou versão numérica comparável da oferta, " +
            "rank ou pacote remoto autenticado. Ação real permanece BLOQUEADA.");
        return text.ToString();
    }
}
