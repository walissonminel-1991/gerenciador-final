using GerenciadorAplicativos;

static void Check(bool ok, string why)
{
    if (!ok) throw new Exception("Seguranca: " + why);
}

Check(SecurityCenterHealth.Interpret("Antivirus", 0, 0).Level == SecurityHealthLevel.Ok,
    "protecao ativa");
foreach (int state in new[] {2, 3})
    Check(SecurityCenterHealth.Interpret("Antivirus", 0, state).Level ==
        SecurityHealthLevel.Attention, "protecoes suspensas exigem atencao");
foreach (var (hr, state) in new[] {(1, 2), (unchecked((int)0x80004005), 0), (0, 1), (0, 99)})
    Check(SecurityCenterHealth.Interpret("Antivirus", hr, state).Level ==
        SecurityHealthLevel.Unknown, "resultado incerto nunca e diagnostico de malware");
var results = SecurityCenterHealth.Capture();
Check(results.Count == 2, "antivirus e firewall devem ser consultados");
Check(results[0].Category == "Protecao antivirus" &&
      results[1].Category == "Protecao de firewall", "categorias");
Check(results.All(x => Enum.IsDefined(x.Level) && x.Description.Length > 0),
    "retorno sempre seguro");
Console.WriteLine("OK: consulta agregada WSC somente leitura e mapeamento conservador.");
