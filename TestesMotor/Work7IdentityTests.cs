using GerenciadorAplicativos;

internal static class Work7IdentityTests
{
    internal static int Run()
    {
        int failed = 0, total = 0;
        void Check(bool ok, string label)
        {
            total++;
            Console.WriteLine((ok ? "PASS " : "FAIL ") + "W7-ID " + label);
            if (!ok) failed++;
        }
        var old = new InstalledApp("7-Zip 26.04 (x64)", "26.04", "Igor Pavlov",
            Architecture: "x64", Scope: "machine", InventoryId: "LocalMachine|Registry64|old",
            InstallLocation: @"C:\Program Files\7-Zip");
        var latest = old with { Name = "7-Zip 26.05 (x64 edition)", Version = "26.05",
            InventoryId = "LocalMachine|Registry64|new" };
        CatalogEntry[] catalog = [new("7-Zip", "7-Zip 26.05 (x64)", "26.05", "Igor Pavlov")];
        MatchState State(InstalledApp a, InstalledApp b) => Matching.Evaluate([a, b], catalog)[0].State;
        Check(State(old, latest) == MatchState.SupersededRegistration, "mesma pasta e escopo mantêm ambas as linhas, sem atualização redundante");
        foreach (var (label, other) in new (string, InstalledApp)[] {
            ("diretórios distintos", latest with { InstallLocation = @"D:\Tools\7-Zip" }),
            ("escopos distintos", latest with { Scope = "user", InventoryId = "CurrentUser|Registry64|new" }),
            ("escopo desconhecido", latest with { Scope = "unknown" }),
            ("pasta desconhecida", latest with { InstallLocation = "" }),
            ("caminho relativo", latest with { InstallLocation = @"..\7-Zip" }),
            ("editor divergente", latest with { Publisher = "Outro editor" }),
            ("outra arquitetura", latest with { Name = "7-Zip 26.05 (x86)", Architecture = "x86" }),
            ("canal distinto", latest with { Channel = "preview" }),
            ("tipo de instalação distinto", latest with { InstallType = "msix" }),
            ("versão do nome contradiz cadastro", latest with { Name = "7-Zip 26.03 (x64)" }) })
            Check(State(old, other) == MatchState.Candidate, label + " não prova registro antigo");
        Check(State(old with { InstallLocation = "" }, latest with { InstallLocation = "" }) == MatchState.Candidate,
            "duas pastas ausentes não comprovam a mesma instalação");
        Check(State(old with { Architecture = "arm64" }, latest) == MatchState.Ambiguous,
            "arquitetura explícita contraditória bloqueia a correspondência");
        Check(State(old, latest with { InstallLocation = @"c:/Program Files/7-Zip/" }) == MatchState.SupersededRegistration,
            "separadores e capitalização não inventam instalações distintas");
        Console.WriteLine($"W7-ID: {total - failed}/{total}");
        return failed;
    }
}
