using GerenciadorAplicativos;

// Backend de memoria: jamais instancia objetos WUA/COM, nem toca hardware.
internal static class DriverUpdatePilotTests
{
    private sealed class Fake : IDriverUpdatePilotBackend, IDriverUpdatePilotJournal
    {
        internal readonly List<string> Calls = [];
        internal DriverUpdatePilotCheck First = Ok(false);
        internal DriverUpdatePilotCheck Second = Ok(true);
        internal DriverUpdatePilotStep DownloadResult = new(true, false);
        internal DriverUpdatePilotStep InstallResult = new(true, false);
        internal DriverUpdatePilotObservation Observation = new(true, true, true, false);
        internal bool JournalAllowed = true;
        internal bool ThrowsOnInstall;
        internal Action? DuringDownload;
        internal readonly List<DriverUpdatePilotState> Marked = [];
        public Task<DriverUpdatePilotCheck> Revalidate(DriverUpdatePilotChoice choice, CancellationToken token)
        {
            Calls.Add("check");
            return Task.FromResult(Calls.Count(x => x == "check") == 1 ? First : Second);
        }
        public Task<DriverUpdatePilotStep> Download(DriverUpdatePilotChoice choice, CancellationToken token)
        {
            Calls.Add("download"); DuringDownload?.Invoke();
            return Task.FromResult(DownloadResult);
        }
        public Task<DriverUpdatePilotStep> Install(DriverUpdatePilotChoice choice)
        {
            Calls.Add("install");
            if (ThrowsOnInstall) throw new InvalidOperationException("falha sintetica");
            return Task.FromResult(InstallResult);
        }
        public Task<DriverUpdatePilotObservation> Observe(DriverUpdatePilotChoice choice)
        {
            Calls.Add("observe"); return Task.FromResult(Observation);
        }
        public bool TryBegin(DriverUpdatePilotChoice choice)
        {
            Calls.Add("journal"); return JournalAllowed;
        }
        public void Mark(DriverUpdatePilotChoice choice, DriverUpdatePilotState state)
        {
            Calls.Add("mark"); Marked.Add(state);
        }
    }
    private static DriverUpdatePilotCheck Ok(bool downloaded) =>
        new(true, true, true, true, true, true, true, true, downloaded);

    private static void Check(bool v, string message)
    {
        if (!v) throw new Exception("PILOTO WUA: " + message);
    }

    internal static async Task Run()
    {
        var id = "8d4bf87a-3370-4260-936f-8e3694387c8e";
        var device = new DriverInventoryEntry(@"PCI\G2\A", "Camera ficticia", "Fornecedor A",
            "1.0.0.0", "oem5.inf", true, true)
        { HardwareIds = [@"PCI\VEN_1234&DEV_1234"] };
        var entry = new DriverOfficialEntryEvidence(device.HardwareIds[0], device.Provider,
            "Camera", "Image", "Fabricante", new DateTime(2026, 1, 1), 0, 0);
        var offer = new DriverOfficialOffer(id, 1, "Atualizacao simulada",
            device.HardwareIds[0], device.Provider, "Camera", new DateTime(2026, 1, 1))
        {
            DriverClass = "Image",
            Relations = new(DriverOfficialRelationsState.Complete, [], []),
            EntryAudit = new(DriverOfficialEntryState.Complete, [entry])
        };
        var match = new DriverOfficialMatch(offer, device.DeviceId, device.DeviceName)
        {
            MatchingWuaEntry = entry,
            Installed = new(device.InfName, device.Provider, device.Version, true, null, 0, false)
        };
        var inventory = new DriverInventoryResult([device], false, "sintetico");
        var official = new DriverOfficialResult([match], 1, 0, false, "ok")
        { SearchContext = new("Windows Update", true, "10.0", "X64", DateTimeOffset.UtcNow) };

        Check(DriverUpdatePilot.TryPrepare(inventory, official, match, out var choice),
            "oferta selecionada vincula instância, HardwareID e UpdateID");
        Check(!DriverUpdatePilot.TryPrepare(inventory with
            { Items = [device, device] }, official, match, out _) &&
            !DriverUpdatePilot.TryPrepare(inventory with { Partial = true },
                official, match, out _) &&
            !DriverUpdatePilot.TryPrepare(inventory,
                official with { Partial = true }, match, out _),
            "duplicacao global e fonte parcial bloqueiam autorizacao");
        Check(!DriverUpdatePilot.AllowedClass("Net") &&
            !DriverUpdatePilot.AllowedClass("Display") &&
            !DriverUpdatePilot.AllowedClass("System") &&
            !DriverUpdatePilot.AllowedClass("Firmware") &&
            !DriverUpdatePilot.AllowedClass("USB") &&
            !DriverUpdatePilot.AllowedClass(null),
            "classes criticas ou desconhecidas bloqueadas");
        var now = DateTimeOffset.UtcNow;
        DriverUpdatePilotApproval Approval() => new(id, 1, device.DeviceId, true, true, now);
        Check(!DriverUpdatePilot.ConsentValid(choice!, Approval() with { Confirmed = false }, now) &&
            !DriverUpdatePilot.ConsentValid(choice!, Approval() with { Revision = 2 }, now) &&
            !DriverUpdatePilot.ConsentValid(choice!, Approval() with { DisposableWindows = false }, now) &&
            !DriverUpdatePilot.ConsentValid(choice!, Approval() with
                { AcceptedAt = now.AddMinutes(-7) }, now),
            "consentimento deve ser especifico, recente e no Windows descartavel");
        async Task<(DriverUpdatePilotResult Result, Fake Fake)> Run(
            Action<Fake>? change = null, DriverUpdatePilotApproval? approval = null,
            CancellationToken token = default)
        {
            var f = new Fake(); change?.Invoke(f);
            var result = await DriverUpdatePilot.Execute(choice!, approval ?? Approval(),
                f, f, token, now);
            return (result, f);
        }
        var missingConsent = await Run(approval: Approval() with { Confirmed = false });
        Check(missingConsent.Result.State == DriverUpdatePilotState.Blocked &&
            missingConsent.Fake.Calls.Count == 0, "sem consentimento nao consulta backend");
        var invalid = await Run(f => f.First = f.First with { OfficialSource = false });
        Check(invalid.Result.State == DriverUpdatePilotState.Blocked &&
            invalid.Fake.Calls.SequenceEqual(["check"]),
            "origem nativa diferente do Windows Update nao pode baixar");
        var failedDownload = await Run(f => f.DownloadResult = new(false, false));
        Check(failedDownload.Result.State == DriverUpdatePilotState.DownloadFailed &&
            !failedDownload.Fake.Calls.Contains("install"),
            "download falho impede instalacao");
        using var cancel = new CancellationTokenSource();
        var canceled = await Run(f => f.DuringDownload = cancel.Cancel, token: cancel.Token);
        Check(canceled.Result.State == DriverUpdatePilotState.DownloadedOnly &&
            !canceled.Fake.Calls.Contains("install"),
            "cancelamento depois do download impede instalacao");
        var changed = await Run(f => f.Second = f.Second with { SameDevice = false });
        Check(changed.Result.State == DriverUpdatePilotState.DownloadedOnly &&
            !changed.Fake.Calls.Contains("install"),
            "identidade diferente antes da instalacao bloqueia");
        var noJournal = await Run(f => f.JournalAllowed = false);
        Check(noJournal.Result.State == DriverUpdatePilotState.DownloadedOnly &&
            !noJournal.Fake.Calls.Contains("install"),
            "falha de journal impede comeco da instalacao");
        var uncertain = await Run(f => f.ThrowsOnInstall = true);
        Check(uncertain.Result.State == DriverUpdatePilotState.InstallationUncertain &&
            uncertain.Fake.Marked.SequenceEqual([DriverUpdatePilotState.InstallationUncertain]) &&
            !uncertain.Fake.Calls.Contains("observe"),
            "excecao depois de Install deixa estado incerto e nao repete");
        var reboot = await Run(f => f.InstallResult = new(true, true));
        Check(reboot.Result.State == DriverUpdatePilotState.PendingReboot &&
            !reboot.Fake.Calls.Contains("observe"),
            "reinicio pendente sem comando de reboot");
        var unverified = await Run(f => f.Observation = new(true, false, true, false));
        Check(unverified.Result.State == DriverUpdatePilotState.VerificationUncertain,
            "retorno nativo positivo sem diferenca PnP nao vira sucesso");
        var success = await Run();
        Check(success.Result.State == DriverUpdatePilotState.Verified &&
            success.Fake.Calls.SequenceEqual(
                ["check", "download", "check", "journal", "install", "observe", "mark"]),
            "fluxo sintetico completo de revalidacao, download, journal, install e observacao");
        Console.WriteLine("OK G2: contrato transacional WUA de teste validado sem COM, download ou instalacao reais");
    }
}
