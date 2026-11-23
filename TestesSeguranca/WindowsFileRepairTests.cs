using GerenciadorAplicativos;

internal static class WindowsFileRepairTests
{
    private const string DismClean = "No component store corruption detected.";
    private const string DismRepairable = "The component store is repairable.";
    private const string SfcClean = "Windows Resource Protection did not find any integrity violations.";
    private const string SfcViolations = "Windows Resource Protection found integrity violations.";
    private const string SfcRepaired = "Windows Resource Protection found corrupt files and successfully repaired them.";
    private const string VerifyCommand = "sfc.exe /verifyonly";

    internal static async Task RunAsync()
    {
        int passed = 0;
        void Check(bool condition, string expectation)
        {
            if (!condition) throw new InvalidOperationException("VF03 arquivos protegidos: " + expectation);
            passed++;
            Console.WriteLine("OK VF03 arquivos protegidos: " + expectation);
        }

        Check(WindowsIntegrityAssessment.Interpret(0, DismClean, 0, SfcClean) == IntegrityState.Healthy &&
            WindowsIntegrityAssessment.Interpret(0, DismRepairable, 0, SfcViolations) == IntegrityState.Repairable &&
            WindowsIntegrityAssessment.Interpret(0, "O armazenamento de componentes pode ser reparado.", 0,
                "A Proteção de Recursos do Windows encontrou violações de integridade.") == IntegrityState.Repairable,
            "verificação inicial preserva os estados em inglês e português.");
        Check(WindowsIntegrityAssessment.Interpret(0, "Completed successfully.", 0,
                "Verification 100% complete.") == IntegrityState.Inconclusive &&
            WindowsIntegrityAssessment.Interpret(1, DismRepairable, 0, SfcViolations) == IntegrityState.Inconclusive &&
            WindowsIntegrityAssessment.Interpret(0, DismClean, 1, SfcClean) == IntegrityState.Inconclusive,
            "a verificação inicial continua bloqueando reparo sem a evidência exigida.");

        string[] expected = ["dism.exe /Online /Cleanup-Image /RestoreHealth", "sfc.exe /scannow",
            "dism.exe /Online /Cleanup-Image /ScanHealth", VerifyCommand];
        var clean = await Simulate((0, ""), (0, SfcRepaired), (0, DismClean), (0, SfcClean));
        Check(clean.Commands.SequenceEqual(expected) && clean.ExitCode == 0 &&
            clean.LastLine.Equals("WINREPAIR:HEALTHY", StringComparison.Ordinal),
            "após DISM e tentativa de reparo, /verifyonly comprova o estado dos arquivos protegidos.");
        var cleanPortuguese = await Simulate((0, ""), (0, ""), (0, DismClean),
            (0, "Verificação 100% concluída.\nA Proteção de Recursos do Windows não encontrou nenhuma violação de integridade."));
        Check(cleanPortuguese.ExitCode == 0 && cleanPortuguese.Commands.SequenceEqual(expected),
            "mensagem final explícita em português confirma a verificação pós-reparo.");

        var remains = await Simulate((0, ""), (0, SfcRepaired), (0, DismClean), (0, SfcViolations));
        Check(remains.ExitCode != 0 && remains.LastLine.Equals("WINREPAIR:REPAIRABLE", StringComparison.Ordinal),
            "violações posteriores prevalecem sobre a mensagem anterior de reparo bem-sucedido.");
        foreach (string output in new[] { "", "Verification 100% complete.", SfcRepaired, SfcClean + "\n" + SfcViolations })
        {
            var unclear = await Simulate((0, ""), (0, ""), (0, DismClean), (0, output));
            Check(unclear.ExitCode != 0 && unclear.LastLine.Equals("WINREPAIR:INCONCLUSIVE", StringComparison.Ordinal),
                "saída vazia, progresso, texto de reparo ou mensagens conflitantes não confirmam integridade.");
        }

        var dismFailed = await Simulate((3010, ""));
        Check(dismFailed.ExitCode == 3010 && dismFailed.Commands.SequenceEqual(expected.Take(1)),
            "preserva retorno DISM não zero e encerra antes do SFC.");
        var sfcFailed = await Simulate((0, ""), (23, SfcRepaired));
        Check(sfcFailed.ExitCode == 23 && sfcFailed.Commands.SequenceEqual(expected.Take(2)),
            "preserva erro do SFC de reparo e não inicia a verificação posterior.");
        var verificationFailed = await Simulate((0, ""), (0, ""), (0, DismClean), (27, SfcClean));
        Check(verificationFailed.ExitCode == 27 && verificationFailed.Commands.SequenceEqual(expected) &&
            verificationFailed.LastLine.Equals("WINREPAIR:INCONCLUSIVE", StringComparison.Ordinal),
            "erro de execução invalida texto limpo e preserva o código de saída.");

        var progress = new WindowsMaintenanceProgress();
        Check(!progress.Update("47.5%", out _) &&
            !progress.Update("Etapa 1/2: DISM", out _) &&
            progress.Update("[======== 47.5% ]", out int dism) && dism == 23 &&
            !progress.Update("Etapa 2/2: SFC", out _) &&
            progress.Update("Verificação 84% concluída", out int sfc) && sfc == 92,
            "percentuais da verificação de duas etapas continuam baseados na saída real.");
        Check(!progress.Update("Etapa 1/4 — DISM", out _) && progress.Update("100%", out int first) && first == 25 &&
            !progress.Update("Etapa 2/4 — SFC", out _) && progress.Update("100%", out int second) && second == 50 &&
            !progress.Update("Etapa 4/4 — SFC", out _) && progress.Update("50%", out int checking) && checking == 87 &&
            !progress.Update("Etapa concluída", out _) && progress.Update("100%", out int final) && final == 100,
            "a barra reserva a última parte para a verificação pós-reparo e só avança com percentuais reais.");
        Console.WriteLine($"VF03 arquivos protegidos: {passed} verificações aprovadas.");
    }

    private static async Task<(int ExitCode, string[] Commands, string LastLine)> Simulate(
        params (int ExitCode, string Output)[] responses)
    {
        var commands = new List<string>();
        var log = new List<string>();
        var pending = new Queue<(int ExitCode, string Output)>(responses);
        int exit = await WindowsFileRepair.RunAsync((exe, args) =>
        {
            commands.Add(exe + " " + string.Join(" ", args));
            if (!pending.TryDequeue(out var response))
                throw new InvalidOperationException("Comando inesperado: " + commands[^1]);
            return Task.FromResult(response);
        }, log.Add);
        if (pending.Count != 0) throw new InvalidOperationException("Alguns comandos previstos não foram executados.");
        return (exit, commands.ToArray(), log[^1]);
    }
}
