namespace GerenciadorAplicativos;

// Traduz negativas do piloto; não define política de instalação.
// A única classificação de candidato utiliza TryPrepare EXISTENTE.
internal static class DriverOfferReview
{
    internal static string Summary(DriverInventoryResult inventory, DriverOfficialResult official)
    {
        if (inventory.Partial || official.Partial)
            return "Triagem WUA inconclusiva: inventário ou consulta parcial. Não inferir ausência de ofertas.";
        if (official.SearchContext is not { Source: "Windows Update", Online: true })
            return "Triagem WUA inconclusiva: origem Windows Update online não comprovada. Não inferir ausência de ofertas.";
        if (official.Offered == 0 && official.Matches.Count == 0)
            return "Consulta WUA concluída sem ofertas observadas nesta busca. Não garante ausência futura nem instalação disponível.";
        if (official.Offered > 0 && official.Matches.Count == 0)
            return $"Consulta WUA: {official.Offered} ofertas observadas; nenhum vínculo PnP comprovado nesta busca.";
        int candidates = official.Matches.Count(m =>
            DriverUpdatePilot.TryPrepare(inventory, official, m, out _));
        return $"Triagem WUA: {official.Offered} ofertas observadas; " +
            $"{official.Matches.Count} vínculos apresentados; {official.Unmatched} sem vínculo aprovado; " +
            $"{candidates} candidatos aos gates experimentais pré-download. " +
            "Candidato não significa driver mais recente, pacote verificado ou permissão de instalar.";
    }

    internal static string Explain(DriverInventoryResult inventory,
        DriverOfficialResult official, DriverOfficialMatch match)
    {
        if (inventory.Partial || official.Partial)
            return "BLOQUEADO: inventário ou consulta WUA parcial.";
        if (official.SearchContext is not { Source: "Windows Update", Online: true })
            return "BLOQUEADO: origem Windows Update online não comprovada.";
        if (match.Kind != DriverOfficialMatchKind.PrimaryHardwareId)
            return $"SOMENTE CONSULTA: {match.MatchDescription}.";
        if (!DriverUpdatePilot.AllowedClass(match.Offer.DriverClass))
            return "BLOQUEADO: classe fora das permitidas no piloto (Printer, Image, Ports).";
        if (match.Offer.EntryAudit?.State != DriverOfficialEntryState.Complete ||
            match.MatchingWuaEntry is null)
            return "BLOQUEADO: entradas WUA ou correspondência por HardwareID não comprovadas.";
        if (match.Offer.EntryAudit.Entries.Count != 1)
            return "BLOQUEADO: a oferta WUA contém mais de uma entrada.";
        if (match.Offer.Relations?.State != DriverOfficialRelationsState.Complete)
            return "BLOQUEADO: metadados de relações WUA incompletos.";
        if (inventory.Items.Count(d =>
            string.Equals(d.DeviceId, match.DeviceId, StringComparison.OrdinalIgnoreCase)) != 1)
            return "BLOQUEADO: identidade PnP não única no inventário integral.";
        if (!DriverInventory.HasExclusiveHardwareId(inventory, match.Offer.HardwareId))
            return "BLOQUEADO: identificador WUA sem alvo físico exclusivo no inventário.";
        if (inventory.Items.Any(d => string.Equals(d.DeviceId, match.DeviceId, StringComparison.OrdinalIgnoreCase) &&
            d.BindingStatus == DriverBindingStatus.Conflicting))
            return "BLOQUEADO: INF ou versão diverge entre o inventário WMI e a associação PnP atual.";
        if (match.Installed is null)
            return "BLOQUEADO: evidência do driver instalado indisponível.";
        if (!DriverUpdatePilot.TryPrepare(inventory, official, match, out _))
            return "BLOQUEADO: associação INF, fornecedor, identidade ou metadados insuficientes para os gates existentes.";
        return "CANDIDATO APENAS à revisão experimental pré-download. " +
            "Versão numérica ofertada, ranking, pacote íntegro e arquitetura de destino não comprovados; " +
            "instalação nativa indisponível nesta compilação.";
    }
}
