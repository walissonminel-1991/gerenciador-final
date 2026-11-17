using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class DriverUpdatePilotDialogTests
{
    private sealed class Fake : IDriverUpdatePilotBackend
    {
        internal readonly List<string> Calls = [];
        internal TaskCompletionSource<DriverUpdatePilotStep>? SlowDownload;
        internal bool ThrowRevalidate;
        public Task<DriverUpdatePilotCheck> Revalidate(DriverUpdatePilotChoice c, CancellationToken t)
        {
            Calls.Add("check");
            if (ThrowRevalidate) throw new InvalidOperationException("falha sintetica");
            return Task.FromResult(new DriverUpdatePilotCheck(
                true, true, true, true, true, true, true, true,
                Calls.Count(x => x == "check") > 1));
        }
        public Task<DriverUpdatePilotStep> Download(DriverUpdatePilotChoice c, CancellationToken t)
        {
            Calls.Add("download");
            return SlowDownload?.Task ?? Task.FromResult(new DriverUpdatePilotStep(true, false));
        }
        public Task<DriverUpdatePilotStep> Install(DriverUpdatePilotChoice c)
        {
            Calls.Add("install");
            return Task.FromResult(new DriverUpdatePilotStep(true, false));
        }
        public Task<DriverUpdatePilotObservation> Observe(DriverUpdatePilotChoice c)
        {
            Calls.Add("observe");
            return Task.FromResult(new DriverUpdatePilotObservation(true, true, true, false));
        }
    }

    internal static void Run(MainForm owner)
    {
        var root = Path.Combine(Path.GetTempPath(), "GF-WUA-F3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string hardware = @"PCI\VEN_1234&DEV_1234";
            const string deviceId = @"PCI\G3\Camera";
            var device = new DriverInventoryEntry(deviceId, "Camera sintetica",
                "Fornecedor A", "1.0.0.0", "oem5.inf", true, true)
            { HardwareIds = [hardware] };
            var entry = new DriverOfficialEntryEvidence(hardware, "Fornecedor A",
                "Camera", "Image", "Fabricante", new DateTime(2026, 1, 1), 0, 0);
            var offer = new DriverOfficialOffer(
                "67ac7afc-1846-4ce8-8d2f-f65f73aac221", 2,
                "Pacote Camera sintético WUA", hardware, "Fornecedor A", "Camera",
                new DateTime(2026, 1, 1))
            {
                DriverClass = "Image",
                EntryAudit = new(DriverOfficialEntryState.Complete, [entry]),
                Relations = new(DriverOfficialRelationsState.Complete, [], [])
            };
            var match = new DriverOfficialMatch(offer, deviceId, "Camera sintetica")
            {
                Installed = new("oem5.inf", "Fornecedor A", "1.0.0.0", true, null, 0, false),
                MatchingWuaEntry = entry
            };
            var inventory = new DriverInventoryResult([device], false, "sintetico");
            var official = new DriverOfficialResult([match], 1, 0, false, "ok")
            { SearchContext = new("Windows Update", true, "10.0", "X64", DateTimeOffset.UtcNow) };
            bool current = true;
            using (var preview = DriverUpdatePilotDialog.Create(inventory, official, device,
                () => current))
            {
                preview.Show(owner);
                Pump(8);
                var start = Find<Button>(preview, "DriverPilotStart");
                var simulate = Find<Button>(preview, "DriverPilotSimulatePlan");
                var details = Find<TextBox>(preview, "DriverPilotDetails");
                var offers = Find<ListBox>(preview, "DriverPilotOffers");
                var confirm = Find<CheckBox>(preview, "DriverPilotConfirm");
                var vm = Find<CheckBox>(preview, "DriverPilotDisposable");
                Ensure(offers.Items.Count == 1, "oferta sintética não listada");
                offers.SelectedIndex = 0;
                Ensure(simulate.Enabled, "prévia não habilitada após selecionar oferta");
                simulate.PerformClick();
                Ensure(details.Text.Contains("SIMULAÇÃO TEXTUAL") &&
                    details.Text.Contains("NÃO EXECUTADO") &&
                    Find<Label>(preview, "DriverPilotStatus").Text.Contains("Nenhum passo executado"),
                    "prévia sem resultados inventados");
                confirm.Checked = true;
                vm.Checked = true;
                Ensure(!start.Enabled && start.Text.Contains("bloqueada", StringComparison.OrdinalIgnoreCase),
                    "instalação não foi bloqueada na compilação normal");
                preview.Close();
            }
            var fake = new Fake();
            var path = Path.Combine(root, "ledger.json");
            using (var dialog = DriverUpdatePilotDialog.Create(inventory, official, device,
                () => current, fake, () => new DriverUpdatePilotFileJournal(path)))
            {
                dialog.Show(owner);
                Pump(8);
                var search = Find<TextBox>(dialog, "DriverPilotSearch");
                var offers = Find<ListBox>(dialog, "DriverPilotOffers");
                var detail = Find<TextBox>(dialog, "DriverPilotDetails");
                var confirm = Find<CheckBox>(dialog, "DriverPilotConfirm");
                var vm = Find<CheckBox>(dialog, "DriverPilotDisposable");
                var start = Find<Button>(dialog, "DriverPilotStart");
                var status = Find<Label>(dialog, "DriverPilotStatus");
                var close = Find<Button>(dialog, "DriverPilotCancel");
                var simulate = Find<Button>(dialog, "DriverPilotSimulatePlan");
                foreach (var size in new[] { new Size(500, 340), new Size(790, 510),
                    new Size(1200, 720), new Size(1450, 820) })
                {
                    dialog.ClientSize = size;
                    Pump(8);
                    Ensure(dialog.ClientSize == size && status.AccessibleName is not null,
                        "viewport ou acessibilidade perdida " + size);
                }
                search.Text = "inexistente";
                Ensure(offers.Items.Count == 0 && !start.Enabled, "pesquisa não filtra oferta");
                search.Text = "camera";
                Ensure(offers.Items.Count == 1, "pesquisa não recuperou oferta");
                offers.SelectedIndex = 0;
                Ensure(detail.Text.Contains(offer.UpdateId) &&
                    detail.Text.Contains("NÃO COMPROVADA"), "detalhe promoveu versão ofertada");
                simulate.PerformClick();
                Ensure(fake.Calls.Count == 0 && detail.Text.Contains("SIMULAÇÃO TEXTUAL") &&
                    !start.Enabled && !File.Exists(path),
                    "prévia invocou backend, journal ou habilitou instalação");
                confirm.Checked = true;
                Ensure(!start.Enabled, "confirmação isolada habilitou instalador");
                vm.Checked = true;
                Ensure(start.Enabled, "consentimento específico não habilitou teste sintético");
                start.Focus();
                start.PerformClick();
                PumpUntil(() => status.Text.Contains("Verified"));
                Ensure(fake.Calls.SequenceEqual(
                    ["check", "download", "check", "install", "observe"]) &&
                    File.Exists(path) && !start.Enabled &&
                    status.AccessibleDescription == status.Text,
                    "fluxo WinForms sintético ou journal divergente");
                close.PerformClick();
                Pump(8);
            }
            // Segundo cenário: cancelamento após download começar, resultado
            // tardio não pode produzir Install nem alterar journal anterior.
            byte[] saved = File.ReadAllBytes(path);
            var slow = new Fake
            {
                SlowDownload = new TaskCompletionSource<DriverUpdatePilotStep>(
                    TaskCreationOptions.RunContinuationsAsynchronously)
            };
            using (var pending = DriverUpdatePilotDialog.Create(inventory, official, device,
                () => current, slow, () => new DriverUpdatePilotFileJournal(path)))
            {
                pending.Show(owner);
                Pump(8);
                Find<ListBox>(pending, "DriverPilotOffers").SelectedIndex = 0;
                Find<CheckBox>(pending, "DriverPilotConfirm").Checked = true;
                Find<CheckBox>(pending, "DriverPilotDisposable").Checked = true;
                Find<Button>(pending, "DriverPilotStart").PerformClick();
                PumpUntil(() => slow.Calls.Contains("download"));
                Find<Button>(pending, "DriverPilotCancel").PerformClick();
                slow.SlowDownload!.SetResult(new(true, false));
                Pump(30);
                Ensure(!slow.Calls.Contains("install") &&
                    File.ReadAllBytes(path).SequenceEqual(saved),
                    "cancelamento tardio alterou journal ou instalou");
                pending.Close();
            }
            // Janela descartada enquanto backend está pendente: os callbacks
            // não podem reinstalar, nem tocar a UI descartada.
            var abandoned = new Fake
            {
                SlowDownload = new TaskCompletionSource<DriverUpdatePilotStep>(
                    TaskCreationOptions.RunContinuationsAsynchronously)
            };
            using (var gone = DriverUpdatePilotDialog.Create(inventory, official, device,
                () => current, abandoned, () => new DriverUpdatePilotFileJournal(path)))
            {
                gone.Show(owner);
                Pump(8);
                Find<ListBox>(gone, "DriverPilotOffers").SelectedIndex = 0;
                Find<CheckBox>(gone, "DriverPilotConfirm").Checked = true;
                Find<CheckBox>(gone, "DriverPilotDisposable").Checked = true;
                Find<Button>(gone, "DriverPilotStart").PerformClick();
                PumpUntil(() => abandoned.Calls.Contains("download"));
                gone.Close();
                abandoned.SlowDownload!.SetResult(new(true, false));
                Pump(30);
                Ensure(!abandoned.Calls.Contains("install") &&
                    File.ReadAllBytes(path).SequenceEqual(saved),
                    "disposal/retorno tardio alterou o journal");
            }
            current = false;
            using (var revoked = DriverUpdatePilotDialog.Create(inventory, official, device,
                () => current, new Fake(), () => new DriverUpdatePilotFileJournal(path)))
            {
                revoked.Show(owner);
                Pump(8);
                Find<ListBox>(revoked, "DriverPilotOffers").SelectedIndex = 0;
                Find<CheckBox>(revoked, "DriverPilotConfirm").Checked = true;
                Find<CheckBox>(revoked, "DriverPilotDisposable").Checked = true;
                Ensure(!Find<Button>(revoked, "DriverPilotStart").Enabled &&
                    !Find<Button>(revoked, "DriverPilotSimulatePlan").Enabled &&
                    Find<TextBox>(revoked, "DriverPilotDetails").Text.Contains("revogada") &&
                    Find<Label>(revoked, "DriverPilotStatus").Text.Contains("revogada"),
                    "fotografia revogada habilitou execução");
                revoked.Close();
            }
            Console.WriteLine("OK F3: WinForms piloto real, 4 viewports, oferta, pesquisa, consentimento, simulação, cancelamento, descarte e histórico do piloto preservado");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static T Find<T>(Control parent, string name) where T : Control =>
        Descendants(parent).OfType<T>().Single(c => c.Name == name);

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Pump(int loops)
    {
        for (int i = 0; i < loops; i++) { Application.DoEvents(); Thread.Sleep(6); }
        Application.DoEvents();
    }

    private static void PumpUntil(Func<bool> condition)
    {
        int remaining = 400;
        while (!condition() && --remaining > 0) Pump(1);
        Ensure(condition(), "fluxo WinForms sintético não concluiu");
    }

    private static void Ensure(bool success, string reason)
    {
        if (!success) throw new Exception("F3: " + reason);
    }
}
