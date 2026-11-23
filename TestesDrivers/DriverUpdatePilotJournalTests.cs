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
            var never = DriverUpdatePilotFileJournal.InspectReadOnly(path);
            Ensure(never.Disposition == DriverWuaJournalDisposition.NeverStarted &&
                never.LastWrittenUtc is null && !never.RequiresSupervisedReview &&
                !never.AuthorizesInstallation && !File.Exists(path),
                "consulta tipada não cria diário nem autoriza instalação");
            using (var first = new DriverUpdatePilotFileJournal(path))
            {
                Ensure(first.TryBegin(choice), "primeira intencao duravel rejeitada");
                var begun = DriverUpdatePilotFileJournal.InspectReadOnly(path);
                Ensure(begun.Disposition == DriverWuaJournalDisposition.PendingReconciliation &&
                    begun.Phase == "InstallationStarted" &&
                    begun.OtherProcessHoldsLock &&
                    begun.LastWrittenUtc.HasValue && !begun.AuthorizesInstallation &&
                    !DriverUpdatePilotFileJournal.MayBeginReadOnly(path),
                    "intenção de instalação aberta continua pendente até confirmação");
                using var concurrent = new DriverUpdatePilotFileJournal(path);
                Ensure(!concurrent.TryBegin(choice),
                    "lock interinstancia nao bloqueou intencao concorrente");
                first.Mark(choice, DriverUpdatePilotState.InstallationUncertain);
            }
            Ensure(DriverUpdatePilotFileJournal.ReadOnlyStatus(path).Contains("conciliação"),
                "a UI precisa sinalizar estado incerto sem alterar o diário");
            var pendingSnapshot = DriverUpdatePilotFileJournal.InspectReadOnly(path);
            Ensure(pendingSnapshot.Disposition == DriverWuaJournalDisposition.PendingReconciliation &&
                pendingSnapshot.RequiresSupervisedReview &&
                pendingSnapshot.Phase == nameof(DriverUpdatePilotState.InstallationUncertain) &&
                !pendingSnapshot.AuthorizesInstallation,
                "intenção incerta preserva bloqueio na API tipada");
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
            var verifiedSnapshot = DriverUpdatePilotFileJournal.InspectReadOnly(successPath);
            Ensure(!verifiedSnapshot.OtherProcessHoldsLock &&
                verifiedSnapshot.Disposition == DriverWuaJournalDisposition.Verified &&
                verifiedSnapshot.Phase == nameof(DriverUpdatePilotState.Verified) &&
                !verifiedSnapshot.RequiresSupervisedReview &&
                !verifiedSnapshot.AuthorizesInstallation,
                "verificação guardada não se converte em autorização para nova instalação");
            using (var next = new DriverUpdatePilotFileJournal(successPath))
            {
                Ensure(next.TryBegin(choice),
                    "nova operacao apos verificacao valida nao foi aceita");
                var during = DriverUpdatePilotFileJournal.InspectReadOnly(successPath);
                Ensure(during.OtherProcessHoldsLock &&
                    during.Disposition == DriverWuaJournalDisposition.PendingReconciliation &&
                    !DriverUpdatePilotFileJournal.MayBeginReadOnly(successPath),
                    "observação consultiva respeita exclusividade da intenção nativa");
                next.Mark(choice, DriverUpdatePilotState.PendingReboot);
            }
            using (var pending = new DriverUpdatePilotFileJournal(successPath))
                Ensure(!pending.TryBegin(choice),
                    "reinicio pendente nao bloqueou transacao posterior");
            Ensure(DriverUpdatePilotFileJournal.InspectReadOnly(successPath)
                is { Disposition: DriverWuaJournalDisposition.PendingReconciliation,
                     Phase: nameof(DriverUpdatePilotState.PendingReboot),
                     RequiresSupervisedReview: true },
                "reinício pendente continua pendente, não é sucesso");
            // Mesmo sem diário iniciado, uma sessão com lock exclusivo
            // bloqueia preflight e não deve criar o arquivo da intenção.
            string lockedMissing = Path.Combine(folder, "locked-missing.json");
            using (var held = new FileStream(lockedMissing + ".lock",
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                var withoutLedger = DriverUpdatePilotFileJournal.InspectReadOnly(lockedMissing);
                Ensure(withoutLedger.Disposition ==
                    DriverWuaJournalDisposition.PendingReconciliation &&
                    withoutLedger.OtherProcessHoldsLock &&
                    !DriverUpdatePilotFileJournal.MayBeginReadOnly(lockedMissing) &&
                    !File.Exists(lockedMissing),
                    "lock de outra sessão não vira diário ausente elegível");
            }
            Ensure(DriverUpdatePilotFileJournal.InspectReadOnly(lockedMissing)
                is { Disposition: DriverWuaJournalDisposition.NeverStarted,
                     OtherProcessHoldsLock: false },
                "arquivo de lock ocioso não cria falsa intenção");
            string corrupt = Path.Combine(folder, "corrupt.json");
            File.WriteAllText(corrupt, "{\"Phase\":\"Verified\",\"Sha256\":\"fake\"}");
            var untouched = File.ReadAllBytes(corrupt);
            Ensure(DriverUpdatePilotFileJournal.ReadOnlyStatus(corrupt).Contains("inválido"),
                "status de diário corrompido deve falhar fechado");
            Ensure(DriverUpdatePilotFileJournal.InspectReadOnly(corrupt)
                is { Disposition: DriverWuaJournalDisposition.InvalidOrUnsafe,
                     RequiresSupervisedReview: true, AuthorizesInstallation: false },
                "registro com hash falso nunca aceita nova transação");
            Ensure(File.ReadAllBytes(corrupt).SequenceEqual(untouched),
                "consulta tipada não reescreveu arquivo inválido");
            using (var broken = new DriverUpdatePilotFileJournal(corrupt))
                Ensure(!broken.TryBegin(choice) &&
                    File.ReadAllBytes(corrupt).SequenceEqual(untouched),
                    "journal adulterado foi aceito/reescrito");
            // Um enum numérico desconhecido continua inválido mesmo quando
            // seu digest SHA-256 corresponde ao conteúdo serializado.
            var stamped = DateTimeOffset.UtcNow;
            const string numericPhase = "98765";
            string hashedPayload = choice.UpdateId + "\n" + choice.Revision +
                "\n" + choice.DeviceId + "\n" + numericPhase + "\n" +
                stamped.ToUniversalTime().ToString("O",
                    System.Globalization.CultureInfo.InvariantCulture);
            string numericHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(hashedPayload)));
            string numericPath = Path.Combine(folder, "numeric-phase.json");
            File.WriteAllText(numericPath, System.Text.Json.JsonSerializer.Serialize(new
            {
                choice.UpdateId, choice.Revision, choice.DeviceId,
                Phase = numericPhase, Timestamp = stamped, Sha256 = numericHash
            }));
            var numericOriginal = File.ReadAllBytes(numericPath);
            Ensure(DriverUpdatePilotFileJournal.InspectReadOnly(numericPath)
                is { Disposition: DriverWuaJournalDisposition.InvalidOrUnsafe,
                     AuthorizesInstallation: false } &&
                !DriverUpdatePilotFileJournal.MayBeginReadOnly(numericPath) &&
                File.ReadAllBytes(numericPath).SequenceEqual(numericOriginal),
                "fase numérica desconhecida com hash coerente deve ser inválida");
            Console.WriteLine("OK F2: journal atomico, lock exclusivo, estado incerto/reboot e integridade fail-closed; sem COM");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new Exception("WUA JOURNAL: " + message);
    }
}
