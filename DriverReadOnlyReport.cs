using System.Text;

namespace GerenciadorAplicativos;

internal static class DriverReadOnlyReport
{
    // Estado consultivo da leitura: não tentar inferir presença/ausência
    // de INF ou assinatura a partir de consulta não feita/incompleta.
    internal static string InfAvailability(DriverInventoryEntry? selected) =>
        selected is null
            ? "Indisponível: selecione um dispositivo com INF mapeado."
            : selected.WithoutSignedDriverAssociation
                ? "Indisponível: nenhuma associação de driver assinado observada nesta leitura; preservar."
                : selected.PackageEvidence is null
                    ? "Indisponível: vínculo com Driver Store não informado nesta leitura; não prova ausência de INF."
                    : selected.PackageEvidence.State switch
                    {
                        DriverStoreState.Mapped => "Ainda não solicitada para este dispositivo.",
                        DriverStoreState.NotConsulted =>
                            "Indisponível: Driver Store não consultado para este INF; não prova ausência do pacote.",
                        DriverStoreState.Unavailable =>
                            "Indisponível: consulta do vínculo Driver Store não disponível; preservar.",
                        _ => "Indisponível: vínculo do INF com Driver Store inconsistente; preservar."
                    };

    // Relatório de tela local. Não enviar ao diagnóstico: contém identificadores
    // necessários à conferência humana. Não contém linhas brutas de setupapi.dev.log.
    internal static string Format(DriverInventoryResult inventory, DriverInventoryEntry? selected,
        DriverOfficialResult? official = null, DriverInfVerificationResult? verification = null)
    {
        var text = new StringBuilder("DRIVERS — INVESTIGAÇÃO SOMENTE LEITURA\r\n");
        text.AppendLine("Dados locais de hardware nesta janela; nenhum pacote é instalado, removido ou baixado.");
        text.AppendLine($"Dispositivos/associações: {inventory.Items.Count}; inventário parcial: {(inventory.Partial ? "sim" : "não")}.");
        bool ambiguousIdentity = inventory.Items.GroupBy(x => x.DeviceId.Trim(),
            StringComparer.OrdinalIgnoreCase).Any(group =>
                string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (ambiguousIdentity)
            text.AppendLine("ATENÇÃO: DeviceId ausente ou duplicado no inventário completo. " +
                "Uma linha filtrada não comprova associação única de INF/WUA; " +
                "consulta oficial bloqueada até nova leitura coerente.");
        text.AppendLine(inventory.PublishedPackages?.Summary ?? "INFs publicados ainda não consultados.");
        var windowsHistory = inventory.WindowsHistory;
        text.AppendLine(windowsHistory?.Summary ?? "Histórico Windows ainda não consultado.");
        // Lacuna na leitura ou trecho rejeitado não é prova negativa da história.
        if (windowsHistory is { Unavailable: false } &&
            (windowsHistory.Partial || windowsHistory.TailOnly ||
             windowsHistory.RejectedSections > 0))
            text.AppendLine("Histórico Windows incompleto: apenas eventos observados nesta leitura; " +
                "lacunas não comprovam ausência de instalações anteriores.");
        text.AppendLine();
        if (selected is null) text.AppendLine("Selecione um dispositivo na lista para conferir histórico e assinatura do INF.");
        else
        {
            text.AppendLine("DISPOSITIVO SELECIONADO");
            text.AppendLine("Nome no Windows: " + (string.IsNullOrWhiteSpace(selected.DeviceName)
                ? "não informado" : selected.DeviceName));
            if (selected.DeviceMetadata is { BusDescription: { Length: > 0 } description })
                text.AppendLine("Descrição informada pelo dispositivo: " + description);
            var identity = DriverHardwareCatalog.Identify(selected);
            if (identity.Vendor.Length > 0)
                text.AppendLine("Fabricante do hardware (catálogo): " + identity.Vendor);
            if (identity.Model.Length > 0)
            {
                string label = identity switch
                {
                    { FromUsbParent: true } => "Modelo do dispositivo USB vinculado (catálogo): ",
                    { UsbInterface: true } => "Produto USB da interface (catálogo): ",
                    _ => "Modelo do hardware (catálogo): "
                };
                text.AppendLine(label + identity.Model);
            }
            if (identity.Subsystem.Length > 0)
                text.AppendLine("Variante PCI (catálogo): " + identity.Subsystem);
            text.AppendLine("ID da instância: " + selected.DeviceId);
            text.AppendLine("IDs de hardware: " + (selected.HardwareIds.Count == 0 ? "não fornecidos" : string.Join("; ", selected.HardwareIds)));
            text.AppendLine($"Versão instalada (WMI): {selected.Version}; INF: {selected.InfName}; {selected.PresenceLabel}.");
            text.AppendLine("Diagnóstico PnP do Windows: " + selected.PnpStatusLabel + ".");
            text.AppendLine("Orientação consultiva PnP: " +
                DriverPnpGuidance.Describe(selected.PnpProblemCode));
            if (selected.WithoutSignedDriverAssociation)
                text.AppendLine("Sem linha de driver assinado associada nesta leitura. Não significa pacote removível nem prova de versão disponível.");
            text.AppendLine(selected.PackageEvidence?.Label ?? "Vínculo de pacote indisponível.");
            text.AppendLine("Relato de assinatura WMI: " + selected.SignatureLabel + " (não é a verificação abaixo).");
            // A apresentação nunca deve alegar "trecho lido" se a fonte
            // histórica nem foi consultada ou informou indisponibilidade.
            var visibleEvents = windowsHistory?.Unavailable == true
                ? Array.Empty<DriverWindowsEvent>() : selected.WindowsEvents;
            foreach (var change in visibleEvents)
                text.AppendLine($"Log Windows {change.Started:yyyy-MM-dd HH:mm:ss} (hora local registrada): {change.InfName}; " +
                    (change.Outcome == DriverInstallOutcome.Succeeded ? "sucesso registrado" : change.Outcome == DriverInstallOutcome.Failed ? "falha registrada" : "resultado indeterminado"));
            if (visibleEvents.Count == 0)
                text.AppendLine(windowsHistory is null
                    ? "Histórico deste dispositivo não consultado nesta fotografia; não inferir ausência de instalações."
                    : windowsHistory.Unavailable
                        ? "Histórico deste dispositivo indisponível; não inferir ausência de instalações."
                        : windowsHistory.Partial || windowsHistory.TailOnly || windowsHistory.RejectedSections > 0
                            ? "Nenhum vínculo explícito observado no trecho parcial consultado; não inferir ausência de instalações."
                            : "Nenhum vínculo histórico explícito encontrado no trecho lido. Não significa ausência de instalações anteriores.");
            text.AppendLine();
        }
        text.AppendLine("VERIFICAÇÃO DO INF INSTALADO");
        bool evidenceAllowsVerification = selected is not null &&
            !selected.WithoutSignedDriverAssociation &&
            selected.PackageEvidence?.State == DriverStoreState.Mapped;
        text.AppendLine(evidenceAllowsVerification
            ? verification?.Label ?? InfAvailability(selected)
            : InfAvailability(selected));
        if (verification is not null && evidenceAllowsVerification)
        {
            text.AppendLine(verification.Scope);
            text.AppendLine("SHA-256 do INF: " + verification.Sha256);
            text.AppendLine("Catálogo informado pelo Windows: " + verification.Catalog);
            text.AppendLine("Signatário: " + verification.Signer);
            text.AppendLine($"Código nativo: 0x{unchecked((uint)verification.NativeError):X8}");
        }
        text.AppendLine();
        if (selected is not null)
        {
            text.AppendLine(DriverCandidateTriage.Format(inventory, selected, official));
            text.AppendLine();
        }
        text.AppendLine("OFERTAS OFICIAIS");
        bool canShowOfficial = !ambiguousIdentity && !inventory.Partial &&
            inventory.Items.Count > 0;
        text.AppendLine(ambiguousIdentity
            ? "Consulta ao Windows Update indisponível: DeviceId repetido ou ausente no inventário completo; nenhum vínculo WUA publicado."
            : inventory.Partial
                ? "Consulta ao Windows Update indisponível: inventário parcial; falta de dados não prova ausência de oferta."
                : inventory.Items.Count == 0
                    ? "Consulta ao Windows Update indisponível: inventário vazio nesta leitura; não prova ausência de drivers."
                    : official?.Status ?? "Consulta ao Windows Update ainda não solicitada. Consulta a fonte Microsoft e respeita falhas de política; exige conexão.");
        if (official is not null && canShowOfficial)
            text.AppendLine(DriverOfferReview.Summary(inventory, official));
        if (official is not null && canShowOfficial)
            text.AppendLine("IDENTIDADE DO PACOTE REMOTO: NÃO COMPROVADA. WUA DownloadContents expõe URLs de conteúdo e indicador de compressão delta, mas não fornece hash autenticado, INF, DriverVer ou TargetOSVersion. O GF não consulta URLs nem baixa conteúdo nesta investigação; nenhuma oferta vira atualização autorizada.");
        if (canShowOfficial && official?.SearchContext is { } context)
        {
            text.AppendLine($"Contexto da busca: {context.Source}; {(context.Online ? "online" : "cache local")}; Windows {context.HostOsVersion}; arquitetura do SO {context.HostArchitecture}; UTC {context.SearchedAtUtc:u}.");
            text.AppendLine("Estes são atributos do Windows consultado, NÃO a arquitetura ou compatibilidade certificada do pacote oferecido.");
        }
        if (canShowOfficial && official is not null)
            foreach (var match in official.Matches.Take(200))
            {
                text.AppendLine($"{match.DeviceName}: {match.Offer.Title}");
                text.AppendLine($"  Fornecedor: {match.Offer.Provider}; modelo: {match.Offer.Model}; data informada: {match.Offer.VersionDate:yyyy-MM-dd}.");
                text.AppendLine($"  Instância PnP: {match.DeviceId}; HardwareID exato: {match.Offer.HardwareId}.");
                text.AppendLine($"  Identidade da oferta WUA: {match.Offer.UpdateId}, revisão {match.Offer.Revision}.");
                if (match.Relations is { } relations)
                {
                    text.AppendLine($"  Relações WUA: {relations.State}; {relations.SupersededUpdateIds.Count} UpdateIDs substituídos declarados; {relations.Categories.Count} categorias.");
                    if (relations.State == DriverOfficialRelationsState.Complete)
                    {
                        foreach (var id in relations.SupersededUpdateIds.Take(8))
                            text.AppendLine($"    WUA informa que esta oferta substitui o UpdateID {id} (sem versão/revisão do destino; não demonstra driver melhor ou instalado).");
                        if (relations.SupersededUpdateIds.Count > 8)
                            text.AppendLine("    Referências restantes omitidas por limite de apresentação.");
                        foreach (var cat in relations.Categories.Take(8))
                            text.AppendLine($"    Categoria WUA: {cat.CategoryId}; tipo {cat.Type}; nome localizado {cat.Name}.");
                        if (relations.Categories.Count > 8)
                            text.AppendLine("    Categorias restantes omitidas por limite de apresentação.");
                    }
                    else
                        text.AppendLine("    Metadados de supersedência/categorias indisponíveis ou inconclusivos; não concluir ausência de vínculos.");
                    text.AppendLine("  Supersedência entre atualizações e categoria do serviço NÃO são ranking, versão, compatibilidade ou permissão de instalar driver.");
                }
                if (match.Offer.EntryAudit is { } audit)
                {
                    text.AppendLine($"  WUA4 — entradas da oferta: {audit.State}; {audit.Entries.Count} registros válidos observados.");
                    if (match.MatchingWuaEntry is { } specific)
                    {
                        text.AppendLine($"  Entrada WUA vinculada por identificador exato: {specific.HardwareOrCompatibleId}; classe: {specific.DriverClass}; fornecedor: {specific.Provider}; modelo: {specific.Model}.");
                        text.AppendLine($"  Estado e problema informados pela entrada WUA: {specific.DeviceStatus} / {specific.DeviceProblemNumber}; não são prova de identidade da instância física.");
                    }
                    text.AppendLine("  As entradas WUA não fornecem DriverVer numérico, arquitetura/TargetOSVersion do INF, ranking ou bytes do pacote.");
                }
                text.AppendLine($"  Classe informada: {(string.IsNullOrWhiteSpace(match.Offer.DriverClass) ? "não informada" : match.Offer.DriverClass)}; fabricante: {(string.IsNullOrWhiteSpace(match.Offer.Manufacturer) ? "não informado" : match.Offer.Manufacturer)}.");
                if (match.Installed is { } current)
                {
                    text.AppendLine($"  Driver atual: INF {(string.IsNullOrEmpty(current.InfName) ? "não associado" : current.InfName)}; fornecedor {(string.IsNullOrEmpty(current.Provider) ? "não informado" : current.Provider)}; versão {(string.IsNullOrEmpty(current.Version) ? "não instalada/informada" : current.Version)}.");
                    text.AppendLine($"  Assinatura relatada por WMI: {(current.SignedReportedByWindows is true ? "sim" : current.SignedReportedByWindows is false ? "não" : "indeterminada")}; Store: {current.StoreState?.ToString() ?? "não comprovado"}; problema PnP: {current.PnpProblemCode?.ToString() ?? "não informado"}.");
                    text.AppendLine($"  Versão numérica normalizada SOMENTE do driver instalado: {match.InstalledNumericVersion ?? "não comprovada"}; versão numérica do driver oferecido: NÃO DISPONÍVEL pela interface WUA.");
                    if (!current.WithoutSignedDriverAssociation)
                        text.AppendLine("  Mesmo fornecedor atual/oferta: " + (match.ProviderMatchesInstalled ? "sim (nome)" : "não/indeterminado; não equivale a incompatibilidade."));
                }
                text.AppendLine("  " + match.Label);
                text.AppendLine("  Triagem pré-download: " +
                    DriverOfferReview.Explain(inventory, official, match));
                text.AppendLine("  Não há versão numérica da oferta nem comparação de ranking do Windows; nenhuma atualização, instalação ou remoção foi autorizada.");
            }
        if (canShowOfficial && official?.Matches.Count > 200)
            text.AppendLine("Exibição limitada a 200 vínculos; não representa a totalidade.");
        text.AppendLine();
        text.AppendLine("INFs PUBLICADOS — ASSOCIAÇÕES OBSERVADAS");
        if (inventory.PublishedPackages is { } packages)
        {
            if (packages.Partial || !packages.EnumerationComplete)
                text.AppendLine("Relação de INFs publicados parcial ou inconclusiva: a lista abaixo " +
                    "contém somente itens observados; não representa todos os pacotes existentes.");
            if (packages.Packages.Count == 0)
                text.AppendLine(packages.Partial || !packages.EnumerationComplete
                    ? "Nenhum INF retornado na parcela consultada; não comprova ausência de pacotes publicados."
                    : "Nenhum INF observado nesta enumeração; não comprova ausência de drivers no sistema.");
            foreach (var item in packages.Packages)
                text.AppendLine($"{item.Evidence.InfName}: {item.AssociationLabel}; {item.Evidence.Label}.");
        }
        else
            text.AppendLine("Enumeração de INFs publicados não consultada; não há evidência de ausência de pacotes.");
        text.AppendLine("Pacote sem dispositivo observado, versão antiga ou nome parecido não comprova resíduo. R6 exige atribuição concreta; nenhuma exclusão é autorizada.");
        return text.ToString();
    }
}
