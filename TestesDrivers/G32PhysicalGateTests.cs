using GerenciadorAplicativos;

// G3.2: apenas contratos sintéticos. Zero COM, pnputil, download ou instalação.
internal static class G32PhysicalGateTests
{
    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException("G3.2: " + description);
    }

    internal static async Task Run()
    {
        string id = @"USB\VID_1234&PID_0012\A";
        string hardware = @"USB\VID_1234&PID_0012";
        var evidence = new DriverStoreEvidence("oem33.inf", DriverStoreState.Mapped,
            @"C:\Windows\INF\oem33.inf",
            @"C:\Windows\System32\DriverStore\FileRepository\mock\driver.inf");
        var device = new DriverInventoryEntry(id, "Câmera de laboratório",
            "Fornecedor OEM", "1.0.0.0", "oem33.inf", true, true)
        {
            ReportedDriverClass = "Image", PnpProblemCode = 0,
            PackageEvidence = evidence, HardwareIds = [hardware]
        };
        DriverInventoryResult Inventory(params DriverInventoryEntry[] rows) =>
            new(rows, false, "fixture")
            {
                Store = new DriverStoreAuditResult([evidence], false, 0)
            };
        var choice = new DriverUpdatePilotChoice(
            "9c18e379-4769-4a8c-8af8-6b02416bf071", 2, id, hardware,
            device.Provider, device.InfName, device.Version, "Image", "Oferta fictícia");
        Check(DriverG32RecoveryTicket.CanOffer(Inventory(device), device, choice),
            "OEM Image único, assinado e PnP zero pode ser submetido a preflight");
        Check(!DriverG32RecoveryTicket.CanOffer(Inventory(device) with { Partial = true },
                device, choice) &&
              !DriverG32RecoveryTicket.CanOffer(Inventory(device, device), device, choice) &&
              !DriverG32RecoveryTicket.CanOffer(Inventory(device with { PnpProblemCode = null }),
                  device with { PnpProblemCode = null }, choice) &&
              !DriverG32RecoveryTicket.CanOffer(Inventory(device), device,
                  choice with { DriverClass = "Display" }) &&
              !DriverG32RecoveryTicket.CanOffer(Inventory(device with { Provider = "Microsoft" }),
                  device with { Provider = "Microsoft" }, choice with { Provider = "Microsoft" }),
            "inventário parcial, duplicação, PnP incerto, classe crítica e Microsoft bloqueados");
        Check(!DriverG32RecoveryTicket.HasActiveOperation &&
              DriverG32RecoveryTicket.TryBeginOperation() &&
              DriverG32RecoveryTicket.HasActiveOperation &&
              !DriverG32RecoveryTicket.TryBeginOperation(),
            "no máximo uma operação física por processo");
        DriverG32RecoveryTicket.EndOperation();
        Check(!DriverG32RecoveryTicket.HasActiveOperation,
            "trava local liberada após terminar");

        var now = DateTimeOffset.UtcNow;
        var physical = new DriverUpdatePilotApproval(
            choice.UpdateId, choice.Revision, choice.DeviceId,
            true, false, now) { PhysicalWindowsApproved = true };
        Check(DriverUpdatePilot.ConsentValid(choice, physical, now) &&
              !DriverUpdatePilot.ConsentValid(choice,
                  physical with { DisposableWindows = true }, now) &&
              !DriverUpdatePilot.ConsentValid(choice,
                  physical with { PhysicalWindowsApproved = false }, now) &&
              !DriverUpdatePilot.ConsentValid(choice,
                  physical with { AcceptedAt = now.AddMinutes(-6) }, now),
            "consentimento físico exclusivo, identificado e recente");

        string folder = Path.Combine(Path.GetTempPath(), "GF-G32-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "journal.json");
            Check(DriverUpdatePilotFileJournal.MayBeginReadOnly(path),
                "primeira instalação pode avaliar diário ausente sem criá-lo");
            using (var pending = new DriverUpdatePilotFileJournal(path))
            {
                Check(pending.TryBegin(choice), "journal de fixture inicia");
                pending.Mark(choice, DriverUpdatePilotState.InstallationUncertain);
            }
            Check(!DriverUpdatePilotFileJournal.MayBeginReadOnly(path),
                "resultado incerto bloqueia operação posterior antes do download");
            File.WriteAllText(path, "arquivo corrompido");
            Check(!DriverUpdatePilotFileJournal.MayBeginReadOnly(path),
                "corrupção bloqueia preflight sem reparar nem sobrescrever");
            File.Delete(path);
            using (var verified = new DriverUpdatePilotFileJournal(path))
            {
                Check(verified.TryBegin(choice), "journal após remover fixture");
                verified.Mark(choice, DriverUpdatePilotState.Verified);
            }
            Check(DriverUpdatePilotFileJournal.MayBeginReadOnly(path),
                "somente operação verificada libera novo preflight");
            // O backend nativo é instanciado, mas não recebe UMA chamada COM:
            // sem ticket de recuperação privado, o coordenador retorna Blocked.
            using var realJournal = new DriverUpdatePilotFileJournal(path);
            var blocked = await DriverUpdatePilot.Execute(choice, physical,
                new DriverWuaNativeBackend(), realJournal, CancellationToken.None,
                nowForTest: now, stillCurrent: () => true);
            Check(blocked.State == DriverUpdatePilotState.Blocked &&
                  DriverUpdatePilotFileJournal.MayBeginReadOnly(path),
                "WUA real sem ticket não consulta nem altera journal");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("OK G3.2: gates físicos, consentimento, lock e diário sem operações nativas");
    }
}
