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
        if (scan is null || selected is null || scan.ReadFailures != 0 ||
            scan.Duplicates != 0)
            return Invalid("Inventário ARP parcial ou ambíguo: não planejar remoção em grupo.");
        if (!RegistryIdentity(id))
            return new(BundleReviewState.NotRegistryInstallation,
                "Esta instalação não é uma entrada ARP examinada.", id, []);
        if (!IsSafeMetadata(selected) || scan.Items.Any(app => !IsSafeMetadata(app)))
            return Invalid("Metadados de instalador inválidos ou incompletos.");
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
        var children = scan.Items.Where(app => app.BundleParentKeyName.Length != 0 &&
                string.Equals(app.BundleParentKeyName, ownKey, StringComparison.OrdinalIgnoreCase) &&
                app.InventoryId.StartsWith(linked[0] + "|" + linked[1] + "|",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(app.InventoryId, id, StringComparison.OrdinalIgnoreCase))
            .Select(app => app.InventoryId)
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
        if (scan is null || scan.ReadFailures != 0 || scan.Duplicates != 0 ||
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

    // Gate por fotografia *atual* para o executor, antes do lançamento.
    // Evita remover componente aninhado mesmo por chamada direta antiga.
    internal static bool CanLaunchIndividual(ProgramEntry? app) =>
        app is not null && IsSafeMetadata(app) &&
        string.IsNullOrEmpty(app.BundleParentKeyName);
}
