using GerenciadorAplicativos;

var cases = new (InstalledApp app, CatalogEntry[] catalog, MatchState expected)[]
{
    (new("7-Zip 25.01 (x64)", "25.01", "Igor Pavlov"), [new("7-Zip", "7-Zip 26.04 (x64)", "26.04", "Igor Pavlov")], MatchState.Candidate),
    (new("7-Zip 26.04 (x64)", "26.04", "Igor Pavlov"), [new("7-Zip", "7-Zip 26.04 (x64)", "26.04", "Igor Pavlov")], MatchState.EqualOrNewer),
    (new("7-Zip 25.01 (x64)", "25.01", "Fornecedor Diferente"), [new("7-Zip", "7-Zip 26.04 (x64)", "26.04", "Igor Pavlov")], MatchState.Unmatched),
    (new("7-Zip 25.01 (x64)", "desconhecida", "Igor Pavlov"), [new("7-Zip", "7-Zip 26.04 (x64)", "26.04", "Igor Pavlov")], MatchState.NotComparable),
    (new("Exemplo Programa", "1.0", "Empresa Alfa"), [new("Exemplo Programa", "Exemplo Programa", "2.0", "Empresa Beta")], MatchState.Unmatched),
    (new("Exemplo Programa", "1.0", "Empresa Alfa"), [new("Exemplo Programa", "Exemplo Programa", "beta", "Empresa Alfa")], MatchState.NotComparable),
    (new("Exemplo Programa", "1.0", "Empresa Alfa"), [new("Exemplo Programa", "Exemplo Programa", "2.0", "Empresa Alfa"), new("Exemplo Programa", "Exemplo Programa", "3.0", "Empresa Alfa")], MatchState.Candidate),
    (new("Desconhecido", "", ""), [], MatchState.Unmatched),
    (new("Microsoft .NET Runtime - 9.0.21 (x64)", "9.0.21.36604", "Microsoft Corporation"),
        [new("Microsoft .NET Runtime (x64)", "Microsoft .NET Runtime 10 (x64)", "10.0.13.36604", "Microsoft Corporation")], MatchState.DifferentMajor),
    (new("Microsoft .NET Runtime - 9.0.21 (x64)", "9.0.21", "Microsoft Corporation"),
        [new("Microsoft .NET Runtime (x64)", "Microsoft .NET Runtime (x64)", "9.0.22", "Microsoft Corporation")], MatchState.Candidate),
    (new("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501", "12.0.30501", "Microsoft Corporation"),
        [new("Microsoft Visual C++ 2013 Redistributable (x86)", "Microsoft Visual C++ 2013 Redistributable (x86)", "12.0.40664", "Microsoft Corporation")], MatchState.Ambiguous),
    (new("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501", "12.0.30501", "Microsoft Corporation"),
        [new("Microsoft Visual C++ 2013 Redistributable (x64)", "Microsoft Visual C++ 2013 Redistributable (x64)", "12.0.40664", "Microsoft Corporation")], MatchState.Candidate),
    (new("Adobe Acrobat (64-bit)", "26.002.21931", "Adobe"),
        [new("Adobe Acrobat (64-bit)", "Adobe Acrobat (64-bit)", "26.002.22031", "Adobe")], MatchState.Candidate),
    (new("OBS Studio", "31.1.2", "OBS Project"),
        [new("OBS Studio", "OBS Studio", "32.2.2", "OBS Project")], MatchState.Candidate),
};
int failed = 0;
foreach (var (app, catalog, expected) in cases)
{
    var actual = Matching.Evaluate([app], catalog).Single().State;
    bool passed = actual == expected;
    Console.WriteLine($"{(passed ? "OK" : "FALHA")} {app.Name}: esperado={expected} obtido={actual}");
    if (!passed) failed++;
}
// Regressão observada: a instalação de Visual C++ registrou 12.0.40664,
// mas a entrada 12.0.30501 continuou existente. Ela não deve sugerir reinstalação.
var installedVc = new[]
{
    new InstalledApp("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501", "12.0.30501.0", "Microsoft Corporation"),
    new InstalledApp("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.40664", "12.0.40664.0", "Microsoft Corporation"),
    new InstalledApp("Microsoft Visual C++ 2013 Redistributable (x86) - 12.0.30501", "12.0.30501.0", "Microsoft Corporation")
};
var vcCatalog = new[]
{
    new CatalogEntry("Microsoft Visual C++ 2013 Redistributable (x64)", "Microsoft Visual C++ 2013 Redistributable (x64)", "12.0.40664.0", "Microsoft Corporation"),
    new CatalogEntry("Microsoft Visual C++ 2013 Redistributable (x86)", "Microsoft Visual C++ 2013 Redistributable (x86)", "12.0.40664.0", "Microsoft Corporation")
};
var checkedVc = Matching.Evaluate(installedVc, vcCatalog);
var expectedVc = new[] { MatchState.SupersededRegistration, MatchState.EqualOrNewer, MatchState.Candidate };
for (int i = 0; i < expectedVc.Length; i++)
{
    bool passed = checkedVc[i].State == expectedVc[i];
    Console.WriteLine($"{(passed ? "OK" : "FALHA")} Visual C++ arquitetura/registro item={i} esperado={expectedVc[i]} obtido={checkedVc[i].State}");
    if (!passed) failed++;
}
var newerOnly = Matching.Evaluate([installedVc[0]], vcCatalog);
if (newerOnly[0].State != MatchState.Candidate)
{
    Console.WriteLine("FALHA: registro sem versão recente deixou de oferecer atualização necessária.");
    failed++;
}
var notVerified = new InstalledApp("OBS Studio", "31.1.2", "OBS Project",
    EligibleForUpdates: false);
var blockedUpdate = Matching.Evaluate([notVerified],
    [new CatalogEntry("OBS Studio", "OBS Studio", "32.2.2", "OBS Project")]);
if (blockedUpdate.Count != 1 || blockedUpdate[0].State != MatchState.Unmatched)
{
    Console.WriteLine("FALHA: MSIX sem identidade verificada recebeu atualização");
    failed++;
}
else
    Console.WriteLine("OK Pacote não verificado continua listado sem atualização automática");
Console.WriteLine($"{cases.Length + expectedVc.Length + 1 - failed}/{cases.Length + expectedVc.Length + 1} casos aprovados");
return failed == 0 ? 0 : 1;
