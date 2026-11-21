using GerenciadorAplicativos;

// F2: apenas journal local em pasta temporaria, jamais SuggestedPath,
// DriverHistory.DefaultPath nem adaptador COM real.
internal static class DriverUpdatePilotJournalTests
{
    internal static void Run()
    {
        string folder = Path.Combine(Path.GetTempPath(), "GF-WUA-F2-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var choice = new DriverUpdatePilotChoice(
                "184805db-c606-48f8-987f-4502ea4c3a36", 1, @"PCI\F2\A",
                @"PCI\VEN_0001&DEV_0002", "Fornecedor", "oem8.inf",
                "1.0", "Image", "Driver sintetico");
            string path = Path.Combine(folder, "intent.json");
            using (var first = new DriverUpdatePilotFileJournal(path))
            {
                Ensure(first.TryBegin(choice), "primeira intencao duravel rejeitada");
                using var concurrent = new DriverUpdatePilotFileJournal(path);
                Ensure(!concurrent.TryBegin(choice),
                    "lock interinstancia nao bloqueou intencao concorrente");
                first.Mark(choice, DriverUpdatePilotState.InstallationUncertain);
            }
            Ensure(DriverUpdatePilotFileJournal.ReadOnlyStatus(path).Contains("conciliação"),
                "a UI precisa sinalizar estado incerto sem alterar o diário");
            var bytes = File.ReadAllBytes(path);
            using (var recovered = new DriverUpdatePilotFileJournal(path))
                Ensure(!recovered.TryBegin(choice) &&
                    File.ReadAllBytes(path).SequenceEqual(bytes),
                    "queda/incerteza autorizou repeticao ou alterou journal");
            string successPath = Path.Combine(folder, "verified.json");
            using (var verified = new DriverUpdatePilotFileJournal(successPath))
            {
                Ensure(verified.TryBegin(choice), "journal valido nao inicia");
                verified.Mark(choice, DriverUpdatePilotState.Verified);
            }
            Ensure(DriverUpdatePilotFileJournal.ReadOnlyStatus(successPath).Contains("verificada"),
                "status somente leitura deve reconhecer conclusão válida");
            using (var next = new DriverUpdatePilotFileJournal(successPath))
            {
                Ensure(next.TryBegin(choice),
                    "nova operacao apos verificacao valida nao foi aceita");
                next.Mark(choice, DriverUpdatePilotState.PendingReboot);
            }
            using (var pending = new DriverUpdatePilotFileJournal(successPath))
                Ensure(!pending.TryBegin(choice),
                    "reinicio pendente nao bloqueou transacao posterior");
            string corrupt = Path.Combine(folder, "corrupt.json");
            File.WriteAllText(corrupt, "{\"Phase\":\"Verified\",\"Sha256\":\"fake\"}");
            var untouched = File.ReadAllBytes(corrupt);
            Ensure(DriverUpdatePilotFileJournal.ReadOnlyStatus(corrupt).Contains("inválido"),
                "status de diário corrompido deve falhar fechado");
            using (var broken = new DriverUpdatePilotFileJournal(corrupt))
                Ensure(!broken.TryBegin(choice) &&
                    File.ReadAllBytes(corrupt).SequenceEqual(untouched),
                    "journal adulterado foi aceito/reescrito");
            Console.WriteLine("OK F2: journal atomico, lock exclusivo, estado incerto/reboot e integridade fail-closed; sem COM");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new Exception("WUA JOURNAL: " + message);
    }
}
