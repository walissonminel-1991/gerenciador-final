using GerenciadorAplicativos;

internal static class Work7UpdateTests
{
    internal static void Run(Action<bool, string> check)
    {
        var old = new InstalledApp("7-Zip 26.04 (x64)", "26.04", "Igor Pavlov", Scope: "machine",
            InventoryId: "LocalMachine|Registry64|old", InstallLocation: @"C:\Program Files\7-Zip");
        var newer = old with { Name = "7-Zip 26.05 (x64 edition)", Version = "26.05", InventoryId = "LocalMachine|Registry64|new" };
        InstalledApp[] apps = [old, newer];
        var rz = Matching.Evaluate(apps, [new("7-Zip", "7-Zip 26.05 (x64)", "26.05", "Igor Pavlov")]);
        var merged = MultiSourceMatcher.Merge(apps, rz, [new(old, "26.06", "GitHub oficial", "", "sevenzip", "sevenzip")]);
        check(merged[0].State == MatchState.Candidate && merged[0].Catalog?.ProductVersion == "26.06",
            "W7-MERGE registro antigo não suprime uma versão oficial ainda não instalada");
        var ordinary = new InstalledApp("OBS Studio", "31.0", "OBS Project");
        var release = new OfficialRelease(ordinary, "32.0", "GitHub oficial", "", "obs", "obs");
        check(!LiveSourceValidation.Accepts(new([release], [new("a", "ok", 1), new("b", "TaskCanceledException", 1)]), 2),
            "W7-LIVE uma fonte indisponível reprova a consulta completa");
        check(!LiveSourceValidation.Accepts(new([release], [new("a", "ok", 1)]), 2),
            "W7-LIVE diagnóstico ausente não vira aprovação");
        check(!LiveSourceValidation.Accepts(new([release], [new("a", "ok", 1), new("a", "ok", 1)]), 2),
            "W7-LIVE fonte repetida não completa cobertura faltante");
        check(LiveSourceValidation.Accepts(new([release], [new("a", "ok", 1), new("b", "ok", 1)]), 2),
            "W7-LIVE cobertura completa continua aprovada");
        var reviewed = new UpdateMatch(ordinary, new("OBS", "OBS Studio", "32.0", "OBS Project"), MatchState.Candidate)
            { RequiresReview = true };
        check(MultiSourceMatcher.Merge([ordinary], [reviewed], []).Single().RequiresReview,
            "W7-MERGE fusão não apaga bloqueio de revisão anterior");
        check(OfficialReleaseSources.ApplicableSourceCount([newer]) == 1,
            "W7-SOURCE nome real x64 edition também consulta a fonte oficial");
        var contradictory = old with { Architecture = "arm64" };
        var unsafeMerge = MultiSourceMatcher.Merge([contradictory],
            Matching.Evaluate([contradictory], [new("7-Zip", "7-Zip 26.06 (x64)", "26.06", "Igor Pavlov")]),
            [new(contradictory, "26.06", "GitHub oficial", "", "sevenzip", "sevenzip")]);
        check(unsafeMerge.Single().State == MatchState.Ambiguous && unsafeMerge.Single().RequiresReview,
            "W7-MERGE fonte sem arquitetura não contorna conflito de identidade do inventário");
        foreach (int code in new[] { 0, 1, 3, 4, 5, -1, 99 })
        {
            bool blocked = false;
            try { WindowsUpdateSearchPolicy.RequireSuccessfulSearch(code); }
            catch (InvalidDataException) { blocked = true; }
            check(blocked, "W7-WU consulta não concluída é inconclusiva, código " + code);
        }
        WindowsUpdateSearchPolicy.RequireSuccessfulSearch(2);
        check(WindowsUpdateSearchPolicy.ValidateOffers([]).Count == 0,
            "W7-WU consulta completa vazia continua válida");
        const string update = "00000000-0000-0000-0000-000000000771";
        var offer = new WinUpdateChoice(update + "@12", "Fixture", "KB-Fixture", false, true);
        check(WindowsUpdateSearchPolicy.ValidateOffers([offer, offer]).Count == 1,
            "W7-WU mesma oferta duplicada não vira duas instalações");
        foreach (var other in new[] { offer with { Id = update + "@13" }, offer with { Id = "invalid" },
            offer with { EulaAccepted = false } })
        {
            bool blocked = false;
            try { WindowsUpdateSearchPolicy.ValidateOffers([offer, other]); }
            catch (InvalidDataException) { blocked = true; }
            check(blocked, "W7-WU identidade, revisão ou licença contraditória exige nova consulta");
        }
    }
}
