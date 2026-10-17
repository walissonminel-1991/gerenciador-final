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
};
int failed = 0;
foreach (var (app, catalog, expected) in cases)
{
    var actual = Matching.Evaluate([app], catalog).Single().State;
    bool passed = actual == expected;
    Console.WriteLine($"{(passed ? "OK" : "FALHA")} {app.Name}: esperado={expected} obtido={actual}");
    if (!passed) failed++;
}
Console.WriteLine($"{cases.Length - failed}/{cases.Length} casos aprovados");
return failed == 0 ? 0 : 1;
