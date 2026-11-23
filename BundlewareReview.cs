namespace GerenciadorAplicativos;

// Evidências ARP de instaladores de pacotes (Burn, bundles e componentes).
// Não inferir "bloatware" por marca, nome, fabricante ou pasta. O usuário
// deve escolher explicitamente o aplicativo; só o desinstalador registrado
// e revalidado do próprio pacote pode executar qualquer remoção.
internal enum BundleReviewState
{
    Independent, BundleRoot, BundleComponent, UnresolvedParent,
    Inconclusive, NotRegistryInstallation
}
internal sealed record BundleReview(
    BundleReviewState State, string Reason, string InventoryId,
    IReadOnlyList<string> RegisteredChildren)
{
    internal bool CanProposeRegisteredUninstall =>
        State is BundleReviewState.Independent or BundleReviewState.BundleRoot;
}
internal sealed record BundleUninstallPreview(
    bool Eligible, string Reason, IReadOnlyList<UninstallRequest> Requests,
    IReadOnlyList<BundleReview> Evidence);

// Facade do motor existente: nenhuma permissão ou comando de remoção novo.
internal sealed record BundleUninstallRunResult(bool SentToExecutor, string Reason,
    IReadOnlyList<UninstallOutcome> Outcomes)
{
    // Leitura de Registro independente do resultado do uninstaller.
    internal IReadOnlyList<BundlePostRemovalReview> PostRemovalReviews { get; init; } = [];
}

// Conferência pós-desinstalação somente por identidade de registro.
// Ausência ARP não comprova remoção de arquivos, serviços ou dependências.
internal enum BundlePostRemovalState
{
    NotApplicable, PendingObservation, ParentStillRegistered,
    ComponentsStillRegistered, RegistrationsAbsent, Inconclusive
}
internal sealed record BundlePostRemovalReview(BundlePostRemovalState State,
    int RelatedBefore, int RelatedRemaining, string Message)
{
    internal bool ConfirmsCleanUninstall => false;
    internal bool MayRemoveRemainingComponents => false;
}

internal static class BundlewareReview
{
    internal static bool IsSafeMetadata(ProgramEntry? app) =>
        app is not null && app.BundleMetadataReliable &&
        app.BundleParentKeyName.Length <= 180 &&
        app.BundleProviderKey.Length <= 180 &&
        app.BundleParentKeyName.All(c => !char.IsControl(c) && c is not '\\' and not '/') &&
        app.BundleProviderKey.All(c => !char.IsControl(c));

    private static bool RegistryIdentity(string? id)
    {
        if (id is null || id.Length is < 16 or > 512 ||
            id.Any(char.IsControl)) return false;
        string[] parts = id.Split('|');
        return parts.Length == 3 && (parts[0] is "LocalMachine" or "CurrentUser") &&
            (parts[1] is "Registry64" or "Registry32") &&
            parts[2].Length is > 0 and <= 180 &&
            !parts[2].Contains('\\') && !parts[2].Contains('/') &&
            !parts[2].Any(char.IsControl);
    }

    internal static BundleReview Evaluate(RegistryInventoryScan? scan, ProgramEntry? selected)
    {
        string id = selected?.InventoryId ?? "";
        BundleReview Invalid(string reason) =>
            new(BundleReviewState.Inconclusive, reason, id, []);
        if (scan is null || selected is null || !scan.IncludesHiddenEntries ||
            scan.ReadFailures != 0 || scan.Duplicates != 0)
            return Invalid("Inventário ARP parcial ou ambíguo: não planejar remoção em grupo.");
        if (!RegistryIdentity(id))
            return new(BundleReviewState.NotRegistryInstallation,
                "Esta instalação não é uma entrada ARP examinada.", id, []);
        // Um Registro de terceiro com metadados inválidos não invalida uma
        // instalação independente. Falhar fechado para o próprio item
        // escolhido e para componentes com ParentKeyName ligado a ele.
        if (!IsSafeMetadata(selected))
            return Invalid("Metadados do instalador selecionado inválidos.");
        var rows = scan.Items.Where(app => string.Equals(app.InventoryId, id,
            StringComparison.OrdinalIgnoreCase)).ToArray();
        if (rows.Length != 1 || !ReferenceEquals(rows[0], selected) ||
            scan.Items.Any(app => !RegistryIdentity(app.InventoryId) &&
                app.InventorySource.StartsWith("Registro:", StringComparison.OrdinalIgnoreCase)))
            return Invalid("Identidade de registro duplicada ou fotografia inconsistente.");

        string parent = selected.BundleParentKeyName;
        if (parent.Length != 0)
        {
            // Mesmo hive e visão; jamais vincular um pai por nome comercial.
            string[] parts = id.Split('|');
            string parentId = parts[0] + "|" + parts[1] + "|" + parent;
            var parents = scan.Items.Where(app =>
                string.Equals(app.InventoryId, parentId, StringComparison.OrdinalIgnoreCase)).ToArray();
            return parents.Length == 1 && !string.Equals(parentId, id, StringComparison.OrdinalIgnoreCase)
                ? new(BundleReviewState.BundleComponent,
                    "Componente registrado de outro instalador. Não desinstalar isoladamente.", id, [])
                : new(BundleReviewState.UnresolvedParent,
                    "ParentKeyName informado, mas vínculo do pai não é único/verificável.", id, []);
        }

        string[] linked = id.Split('|');
        string ownKey = linked[2];
        var related = scan.Items.Where(app => app.BundleParentKeyName.Length != 0 &&
                string.Equals(app.BundleParentKeyName, ownKey, StringComparison.OrdinalIgnoreCase) &&
                app.InventoryId.StartsWith(linked[0] + "|" + linked[1] + "|",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(app.InventoryId, id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (related.Any(app => !IsSafeMetadata(app)))
            return Invalid("Componente relacionado possui metadados inconsistentes.");
        var children = related.Select(app => app.InventoryId)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        bool bundled = children.Length != 0 || selected.BundleProviderKey.Length != 0;
        return new(bundled ? BundleReviewState.BundleRoot : BundleReviewState.Independent,
            children.Length > 0 ? "Instalador raiz com componentes relacionados; apenas o desinstalador OEM registrado pode remover o conjunto." :
                bundled ? "Metadado de pacote presente; dependências externas não presumidas." :
                "Nenhum metadado de relação de pacote observado.",
            id, children);
    }

    // Revisão de bloatware INDICADO PELO USUÁRIO ou raiz de bundleware.
    // Só constrói pedidos para o executor existente, não executa remoção.
    internal static BundleUninstallPreview PrepareUserSelection(
        RegistryInventoryScan? scan, IReadOnlyCollection<string>? selectedIds)
    {
        if (scan is null || !scan.IncludesHiddenEntries ||
            scan.ReadFailures != 0 || scan.Duplicates != 0 ||
            selectedIds is null || selectedIds.Count is < 1 or > 50 ||
            selectedIds.Any(id => !RegistryIdentity(id)))
            return new(false, "Seleção ou inventário incompleto.", [], []);
        var ids = new HashSet<string>(selectedIds, StringComparer.OrdinalIgnoreCase);
        if (ids.Count != selectedIds.Count)
            return new(false, "Uma instalação foi selecionada mais de uma vez.", [], []);
        var matches = scan.Items.Where(item => ids.Contains(item.InventoryId)).ToArray();
        if (matches.Length != ids.Count)
            return new(false, "A seleção mudou ou não corresponde ao Registro.", [], []);
        var reviews = matches.Select(app => Evaluate(scan, app)).ToArray();
        if (reviews.Any(e => !e.CanProposeRegisteredUninstall) ||
            matches.Any(app => app.InventoryOnly || !app.CanUninstall ||
                ProtectedApplications.IsProtectedName(app.Name)))
            return new(false, "Componente, item protegido ou vínculo inconclusivo; nenhuma execução.", [], reviews);
        try
        {
            // Reutilizar os comandos nativos de desinstalação já validados,
            // sem scripts genéricos nem heurísticas por nome.
            var requests = UninstallBatch.Select(matches);
            return requests.Length == matches.Length
                ? new(true, "Prévia apta a solicitar confirmação; revalidar no momento da remoção.", requests, reviews)
                : new(false, "Desinstalador incompleto.", [], reviews);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            return new(false, "Registro não possui desinstalador validado; nenhuma execução.", [], reviews);
        }
    }

    // Integração funcional: a escolha deve permanecer idêntica APÓS nova
    // leitura ARP completa; a execução fica a cargo de UninstallBatch,
    // que faz sua própria recaptura antes de lançar o uninstaller registrado.
    // Nenhum script de "debloat" ou exclusão por fabricante é permitido.
    internal static async Task<BundleUninstallRunResult> RunConfirmedSelectionAsync(
        RegistryInventoryScan initial, IReadOnlyCollection<string> selectedIds,
        bool confirmed, Func<Task<RegistryInventoryScan>> freshRegistryScan,
        Func<Task<UninstallSnapshot>> freshFullInventory,
        Func<UninstallRequest, CancellationToken, Task<int?>> registeredUninstaller,
        Func<CancellationToken, Task> waitBetweenChecks,
        CancellationToken token = default)
    {
        if (!confirmed || token.IsCancellationRequested)
            return new(false, "Sem consentimento atual; nenhuma remoção.", []);
        var before = PrepareUserSelection(initial, selectedIds);
        if (!before.Eligible)
            return new(false, before.Reason, []);
        bool dispatched = false;
        try
        {
            var fresh = await freshRegistryScan();
            token.ThrowIfCancellationRequested();
            var current = PrepareUserSelection(fresh, selectedIds);
            if (!current.Eligible || before.Requests.Count != current.Requests.Count ||
                before.Evidence.Count != current.Evidence.Count)
                return new(false, "Inventário ou relações do pacote mudaram; refazer a prévia.", []);
            for (int i = 0; i < before.Requests.Count; i++)
            {
                var old = before.Requests[i];
                var now = current.Requests[i];
                var oldReview = before.Evidence[i];
                var currentReview = current.Evidence[i];
                if (!UninstallBatch.SameRegistration(old.Original, now.Original) ||
                    !string.Equals(old.Original.BundleProviderKey,
                        now.Original.BundleProviderKey, StringComparison.Ordinal) ||
                    !string.Equals(old.Original.BundleParentKeyName,
                        now.Original.BundleParentKeyName, StringComparison.Ordinal) ||
                    old.Original.BundleMetadataReliable != now.Original.BundleMetadataReliable ||
                    oldReview.State != currentReview.State ||
                    !oldReview.RegisteredChildren.SequenceEqual(currentReview.RegisteredChildren,
                        StringComparer.OrdinalIgnoreCase) ||
                    !Equals(old.Command, now.Command))
                    return new(false, "Pacote, dependências ou desinstalador mudaram; não executar.", []);
            }
            token.ThrowIfCancellationRequested();
            // O executor também revisa relações do bundle no snapshot mais
            // próximo do lançamento. Essa checagem cobre a janela entre a
            // prévia ARP e a chamada ao desinstalador registrado.
            bool BeforeNativeLaunch(UninstallRequest request, UninstallSnapshot snapshot)
            {
                if (!snapshot.Complete) return false;
                var arp = snapshot.Items.Where(app =>
                    app.InventorySource.StartsWith("Registro:", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (arp.Length == 0 ||
                    arp.GroupBy(app => app.InventoryId, StringComparer.OrdinalIgnoreCase)
                        .Any(g => g.Count() != 1))
                    return false;
                var scanAtLaunch = new RegistryInventoryScan(arp, arp.Length, 0, 0, 0, 0)
                { IncludesHiddenEntries = true };
                var matches = arp.Where(app => string.Equals(app.InventoryId,
                    request.Original.InventoryId, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length != 1 || !UninstallBatch.SameRegistration(
                    request.Original, matches[0]) ||
                    !matches[0].BundleMetadataReliable ||
                    !string.Equals(matches[0].BundleParentKeyName,
                        request.Original.BundleParentKeyName, StringComparison.Ordinal) ||
                    !string.Equals(matches[0].BundleProviderKey,
                        request.Original.BundleProviderKey, StringComparison.Ordinal))
                    return false;
                var atLaunch = Evaluate(scanAtLaunch, matches[0]);
                var expected = current.Evidence.SingleOrDefault(e =>
                    string.Equals(e.InventoryId, request.Original.InventoryId,
                        StringComparison.OrdinalIgnoreCase));
                return expected is not null && atLaunch.State == expected.State &&
                    atLaunch.CanProposeRegisteredUninstall &&
                    atLaunch.RegisteredChildren.SequenceEqual(expected.RegisteredChildren,
                        StringComparer.OrdinalIgnoreCase);
            }
            var executor = new UninstallBatch();
            // Uma falha/cancelamento DEPOIS do handoff pode esconder efeitos
            // do instalador; não relatar que nenhuma remoção foi iniciada.
            dispatched = true;
            var outcomes = await executor.RunAsync(current.Requests, true,
                freshFullInventory, registeredUninstaller, waitBetweenChecks,
                token, preLaunchSafety: BeforeNativeLaunch);
            // O resultado do executor é preservado mesmo se a observação
            // posterior falhar; jamais repetir remoção por erro de consulta.
            var initialSnapshot = new UninstallSnapshot(fresh.Items,
                fresh.IncludesHiddenEntries && fresh.ReadFailures == 0 &&
                fresh.Duplicates == 0);
            var candidates = outcomes.Where(o =>
                o.State == UninstallState.RegistrationAbsent &&
                ObserveAfterUninstall(o, initialSnapshot, null).RelatedBefore > 0).ToArray();
            var postReviews = new List<BundlePostRemovalReview>();
            if (candidates.Length > 0)
            {
                UninstallSnapshot? after = null;
                try { after = await freshFullInventory(); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { }
                foreach (var outcome in candidates)
                    postReviews.Add(ObserveAfterUninstall(outcome, initialSnapshot, after));
            }
            return new(true, "Solicitação encaminhada ao executor registrado com reconferência de identidade.",
                outcomes) { PostRemovalReviews = postReviews };
        }
        catch (OperationCanceledException)
        {
            return new(dispatched, dispatched
                ? "Cancelamento depois de encaminhar ao executor; resultado desconhecido, não repetir."
                : "Solicitação cancelada antes do executor; nenhuma remoção iniciada.", []);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(dispatched, dispatched
                ? "Falha após encaminhar ao executor (" + ex.GetType().Name +
                  "); resultado incerto, conciliar antes de tentar novamente."
                : "Revalidação indisponível (" + ex.GetType().Name +
                  "); nenhuma remoção iniciada.", []);
        }
    }

    // Uma segunda fotografia completa após o executor valida separadamente
    // o destino dos filhos vinculados ao pai. O executor não chama remoção
    // adicional e o resultado nunca certifica limpeza de arquivos/serviços.
    internal static BundlePostRemovalReview ObserveAfterUninstall(
        UninstallOutcome? outcome, UninstallSnapshot? before,
        UninstallSnapshot? after)
    {
        BundlePostRemovalReview Inconclusive(string reason) =>
            new(BundlePostRemovalState.Inconclusive, 0, 0, reason);
        if (outcome is null || before is null || !before.Complete)
            return Inconclusive("Inventário anterior indisponível; não inferir componentes removidos.");
        var original = outcome.Request.Original;
        if (!RegistryIdentity(original.InventoryId))
            return new(BundlePostRemovalState.NotApplicable, 0, 0,
                "O pedido não corresponde a uma raiz ARP verificável.");
        var oldArp = before.Items.Where(app =>
            app.InventorySource.StartsWith("Registro:", StringComparison.OrdinalIgnoreCase)).ToArray();
        var previous = oldArp.Where(app => string.Equals(app.InventoryId,
            original.InventoryId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (previous.Length != 1 || !UninstallBatch.SameRegistration(original, previous[0]))
            return Inconclusive("Identidade do registro inicial não confirmada.");
        var scanBefore = new RegistryInventoryScan(oldArp, oldArp.Length, 0, 0, 0, 0)
        { IncludesHiddenEntries = true };
        var relationship = Evaluate(scanBefore, previous[0]);
        if (relationship.State == BundleReviewState.Independent ||
            relationship.State == BundleReviewState.NotRegistryInstallation ||
            relationship.State == BundleReviewState.BundleRoot &&
                relationship.RegisteredChildren.Count == 0)
            return new(BundlePostRemovalState.NotApplicable, 0, 0,
                "Nenhum componente relacionado foi confirmado no inventário inicial.");
        if (relationship.State != BundleReviewState.BundleRoot ||
            relationship.RegisteredChildren.Count == 0)
            return Inconclusive("Vínculo inicial incompleto; não confirmar remoção de componentes.");
        int total = relationship.RegisteredChildren.Count;
        if (outcome.State != UninstallState.RegistrationAbsent)
            return new(BundlePostRemovalState.PendingObservation, total, total,
                "Execução não comprovou ausência do registro principal; preservar pendência.");
        if (after is null || !after.Complete)
            return new(BundlePostRemovalState.Inconclusive, total, total,
                "Consulta posterior parcial; preservar resultado sem declarar componentes removidos.");
        var present = after.Items.Where(app =>
            app.InventorySource.StartsWith("Registro:", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (present.GroupBy(x => x.InventoryId, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() != 1))
            return new(BundlePostRemovalState.Inconclusive, total, total,
                "Registro posterior ambíguo; comparação não confiável.");
        if (present.Any(x => string.Equals(x.InventoryId,
            original.InventoryId, StringComparison.OrdinalIgnoreCase)))
            return new(BundlePostRemovalState.ParentStillRegistered, total, total,
                "O programa principal voltou a aparecer no Registro; nenhuma conclusão de conjunto.");
        int remaining = relationship.RegisteredChildren.Count(id =>
            present.Any(app => string.Equals(app.InventoryId, id,
                StringComparison.OrdinalIgnoreCase)));
        return remaining > 0
            ? new(BundlePostRemovalState.ComponentsStillRegistered, total, remaining,
                "Registro principal ausente, mas " + remaining + " de " + total +
                " componente(s) relacionado(s) continuam registrados. Não remover automaticamente.")
            : new(BundlePostRemovalState.RegistrationsAbsent, total, 0,
                "Registro principal e " + total +
                " componente(s) relacionado(s) não foram encontrados na nova consulta ARP. " +
                "Arquivos e serviços não foram verificados.");
    }

    // Aviso textual limitado no diálogo de consentimento existente. A
    // presença de filhos ARP não prova sua remoção efetiva pelo instalador;
    // não altera o comando, a seleção ou os gates de segurança.
    internal static string ExplainPotentialBundleImpact(ProgramEntry? selected,
        UninstallSnapshot? snapshot)
    {
        if (selected is null || !RegistryIdentity(selected.InventoryId))
            return "";
        if (snapshot is null || !snapshot.Complete)
            return "Relações de instalador não puderam ser conferidas nesta consulta.";
        var arp = snapshot.Items.Where(app =>
            app.InventorySource.StartsWith("Registro:", StringComparison.OrdinalIgnoreCase)).ToArray();
        var matching = arp.Where(app => string.Equals(app.InventoryId,
            selected.InventoryId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matching.Length != 1 ||
            !UninstallBatch.SameRegistration(selected, matching[0]))
            return "Registro do pacote alterado: não presumir o alcance da desinstalação.";
        var scan = new RegistryInventoryScan(arp, arp.Length, 0, 0, 0, 0)
        { IncludesHiddenEntries = true };
        var evidence = Evaluate(scan, matching[0]);
        return evidence.State switch
        {
            BundleReviewState.BundleRoot when evidence.RegisteredChildren.Count > 0 =>
                "Pacote OEM: " + evidence.RegisteredChildren.Count +
                " componente(s) relacionado(s) no Registro. O desinstalador oficial pode afetá-los.",
            BundleReviewState.BundleRoot =>
                "Pacote OEM com metadados de bundle; outras dependências não foram comprovadas.",
            BundleReviewState.Independent => "",
            _ => "Relação de pacote incompleta ou componente protegido; nenhuma remoção isolada é segura."
        };
    }

    // Ligação com o fluxo de desinstalação já existente, sem comandos novos.
    // snapshots provêm de ScanAllInstalledAppsForSafety (inclusive ARP ocultos).
    // Verifica o vínculo antes de cada execução OEM, não só na prévia.
    internal static bool RelationsStillSafe(UninstallRequest request,
        UninstallSnapshot preview, UninstallSnapshot current)
    {
        if (request is null || preview is null || current is null ||
            !preview.Complete || !current.Complete)
            return false;
        string id = request.Original.InventoryId;
        // Não interferir nos fluxos não ARP (MSIX segue seu próprio executor).
        // Entrada marcada "Registro:" com identidade malformada NÃO pode ser
        // tratada como não ARP para contornar a revisão de dependências.
        if (!RegistryIdentity(id))
            return !request.Original.InventorySource.StartsWith("Registro:",
                StringComparison.OrdinalIgnoreCase);
        bool Registered(ProgramEntry app) =>
            app.InventorySource.StartsWith("Registro:", StringComparison.OrdinalIgnoreCase);
        var firstRows = preview.Items.Where(Registered).ToArray();
        var latestRows = current.Items.Where(Registered).ToArray();
        if (firstRows.GroupBy(app => app.InventoryId, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() != 1) ||
            latestRows.GroupBy(app => app.InventoryId, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() != 1))
            return false;
        var old = firstRows.Where(app => string.Equals(app.InventoryId,
            id, StringComparison.OrdinalIgnoreCase)).ToArray();
        var now = latestRows.Where(app => string.Equals(app.InventoryId,
            id, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (old.Length != 1 || now.Length != 1 ||
            !UninstallBatch.SameRegistration(request.Original, old[0]) ||
            !UninstallBatch.SameRegistration(old[0], now[0]) ||
            !IsSafeMetadata(old[0]) || !IsSafeMetadata(now[0]) ||
            !string.Equals(old[0].BundleParentKeyName,
                now[0].BundleParentKeyName, StringComparison.Ordinal) ||
            !string.Equals(old[0].BundleProviderKey,
                now[0].BundleProviderKey, StringComparison.Ordinal) ||
            !string.Equals(old[0].BundleParentKeyName,
                request.Original.BundleParentKeyName, StringComparison.Ordinal) ||
            !string.Equals(old[0].BundleProviderKey,
                request.Original.BundleProviderKey, StringComparison.Ordinal))
            return false;
        var first = new RegistryInventoryScan(firstRows, firstRows.Length, 0, 0, 0, 0)
        { IncludesHiddenEntries = true };
        var latest = new RegistryInventoryScan(latestRows, latestRows.Length, 0, 0, 0, 0)
        { IncludesHiddenEntries = true };
        var initialReview = Evaluate(first, old[0]);
        var latestReview = Evaluate(latest, now[0]);
        return initialReview.CanProposeRegisteredUninstall &&
            latestReview.CanProposeRegisteredUninstall &&
            initialReview.State == latestReview.State &&
            initialReview.RegisteredChildren.SequenceEqual(latestReview.RegisteredChildren,
                StringComparer.OrdinalIgnoreCase);
    }

    // Gate por fotografia *atual* para o executor, antes do lançamento.
    // Evita remover componente aninhado mesmo por chamada direta antiga.
    internal static bool CanLaunchIndividual(ProgramEntry? app) =>
        app is not null && IsSafeMetadata(app) &&
        string.IsNullOrEmpty(app.BundleParentKeyName);
}
