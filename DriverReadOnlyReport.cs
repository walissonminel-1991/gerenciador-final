using System.Text;

namespace GerenciadorAplicativos;

internal static class DriverReadOnlyReport
{
    // Relatório de tela local. Não enviar ao diagnóstico: contém identificadores
    // necessários à conferência humana. Não contém linhas brutas de setupapi.dev.log.
    internal static string Format(DriverInventoryResult inventory, DriverInventoryEntry? selected,
        DriverOfficialResult? official = null, DriverInfVerificationResult? verification = null)
    {
        var text = new StringBuilder("DRIVERS — INVESTIGAÇÃO SOMENTE LEITURA\r\n");
        text.AppendLine("Dados locais de hardware nesta janela; nenhum pacote é instalado, removido ou baixado.");
        text.AppendLine($"Dispositivos/associações: {inventory.Items.Count}; inventário parcial: {(inventory.Partial ? "sim" : "não")}.");
        text.AppendLine(inventory.PublishedPackages?.Summary ?? "INFs publicados ainda não consultados.");
        text.AppendLine(inventory.WindowsHistory?.Summary ?? "Histórico Windows ainda não consultado.");
        text.AppendLine();
        if (selected is null) text.AppendLine("Selecione um dispositivo na lista para conferir histórico e assinatura do INF.");
        else
        {
            text.AppendLine("DISPOSITIVO SELECIONADO");
            text.AppendLine(selected.DeviceName);
            text.AppendLine("ID da instância: " + selected.DeviceId);
            text.AppendLine("IDs de hardware: " + (selected.HardwareIds.Count == 0 ? "não fornecidos" : string.Join("; ", selected.HardwareIds)));
            text.AppendLine($"Versão instalada (WMI): {selected.Version}; INF: {selected.InfName}; {selected.PresenceLabel}.");
            text.AppendLine(selected.PackageEvidence?.Label ?? "Vínculo de pacote indisponível.");
            text.AppendLine("Relato de assinatura WMI: " + selected.SignatureLabel + " (não é a verificação abaixo).");
            foreach (var change in selected.WindowsEvents)
                text.AppendLine($"Log Windows {change.Started:yyyy-MM-dd HH:mm:ss} (hora local registrada): {change.InfName}; " +
                    (change.Outcome == DriverInstallOutcome.Succeeded ? "sucesso registrado" : change.Outcome == DriverInstallOutcome.Failed ? "falha registrada" : "resultado indeterminado"));
            if (selected.WindowsEvents.Count == 0) text.AppendLine("Nenhum vínculo histórico explícito encontrado no trecho lido. Não significa ausência de instalações anteriores.");
            text.AppendLine();
        }
        text.AppendLine("VERIFICAÇÃO DO INF INSTALADO");
        text.AppendLine(verification?.Label ?? "Ainda não solicitada para este dispositivo.");
        if (verification is not null)
        {
            text.AppendLine(verification.Scope);
            text.AppendLine("SHA-256 do INF: " + verification.Sha256);
            text.AppendLine("Catálogo informado pelo Windows: " + verification.Catalog);
            text.AppendLine("Signatário: " + verification.Signer);
            text.AppendLine($"Código nativo: 0x{unchecked((uint)verification.NativeError):X8}");
        }
        text.AppendLine();
        text.AppendLine("OFERTAS OFICIAIS");
        text.AppendLine(official?.Status ?? "Consulta ao Windows Update ainda não solicitada. Consulta a fonte Microsoft e respeita falhas de política; exige conexão.");
        if (official is not null)
            foreach (var match in official.Matches.Take(200))
            {
                text.AppendLine($"{match.DeviceName}: {match.Offer.Title}");
                text.AppendLine($"  Fornecedor: {match.Offer.Provider}; modelo: {match.Offer.Model}; data informada: {match.Offer.VersionDate:yyyy-MM-dd}.");
                text.AppendLine($"  ID: {match.Offer.HardwareId}; oferta: {match.Offer.UpdateId}, revisão {match.Offer.Revision}.");
                text.AppendLine("  " + match.Label + " Versão numérica não fornecida separadamente pela API; não inferida do título.");
            }
        if (official?.Matches.Count > 200) text.AppendLine("Exibição limitada a 200 vínculos; não representa a totalidade.");
        text.AppendLine();
        text.AppendLine("INFs PUBLICADOS — ASSOCIAÇÕES OBSERVADAS");
        if (inventory.PublishedPackages is { } packages)
        {
            foreach (var item in packages.Packages)
                text.AppendLine($"{item.Evidence.InfName}: {item.AssociationLabel}; {item.Evidence.Label}.");
        }
        text.AppendLine("Pacote sem dispositivo observado, versão antiga ou nome parecido não comprova resíduo. R6 exige atribuição concreta; nenhuma exclusão é autorizada.");
        return text.ToString();
    }
}
