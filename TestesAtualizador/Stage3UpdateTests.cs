using GerenciadorAplicativos;
using System.ComponentModel;
using System.Reflection.PortableExecutable;

internal static class Stage3UpdateTests
{
    private sealed class Clock : TimeProvider
    {
        internal long Seconds;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }
    internal static async Task RunAsync(Action<bool, string> check)
    {
        const string binary = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        ProgramEntry App(string name = "Google Chrome", string publisher = "Google LLC", string icon = binary,
            string id = "LocalMachine|Registry32|fixture", string location = "", bool inventoryOnly = false) =>
            new() { Name = name, Publisher = publisher, Version = "130.0.1.2", DisplayIcon = icon,
                InventoryId = id, InstallLocation = location, InventoryOnly = inventoryOnly };
        var app = App();
        string Registered(ProgramEntry entry) => InstalledUpdateIdentity.RegisteredBinary(entry,
            [@"C:\Program Files", @"C:\Program Files (x86)"], @"C:\Users\Fixture\AppData\Local");
        var metadata = new InstalledUpdateIdentity.BinaryMetadata(0x8664, PEMagic.PE32Plus, true, false, false,
            "Google Chrome", "Google LLC", app.Version);
        foreach (var native in new[] { metadata, metadata with { Machine = 0x14c, Magic = PEMagic.PE32 }, metadata with { Machine = 0xaa64 } })
        {
            var entry = App();
            entry.QueryArchitecture = InstalledUpdateIdentity.CorrelateBinary(entry, native);
            string expected = native.Machine == 0x14c ? "x86" : native.Machine == 0xaa64 ? "arm64" : "x64";
            var identity = InstalledUpdateIdentity.Create(entry);
            check(identity.Architecture == expected && identity.InventoryId == entry.InventoryId && identity.Scope == "machine",
                "ETAPA3: adaptador real conserva PE " + expected + " sem inferir pela visão Registry32");
        }
        check(InstalledUpdateIdentity.Create(App()).Architecture == "unknown",
            "ETAPA3: nome exato, caminho, visão do Registro e SO sem prova PE não inventam arquitetura");
        check(Registered(app) == binary && Registered(App(icon: '"' + binary + "\",0")) == binary,
            "ETAPA3: ícone registrado simples ou com índice identifica somente chrome.exe Stable");
        foreach (var invalid in new[]
        {
            App(name: "Google Chrome Beta"), App(publisher: "Outro editor"), App(inventoryOnly: true),
            App(icon: binary + " --argumento"), App(icon: binary + ":stream"), App(icon: binary.Replace("Chrome", "Chrome Beta")),
            App(icon: @"\\server\Google\Chrome\Application\chrome.exe"), App(icon: @"chrome.exe"),
            App(id: "MSIX|fixture"), App(id: "CurrentUser|Registry64|fixture"), App(location: @"C:\Outra pasta")
        })
            check(Registered(invalid).Length == 0, "ETAPA3: identidade, escopo, canal ou caminho incompatível não fornece prova local");
        check(Registered(App(icon: @"C:\Users\Fixture\AppData\Local\Google\Chrome\Application\chrome.exe",
            id: "CurrentUser|Registry64|fixture")) == @"C:\Users\Fixture\AppData\Local\Google\Chrome\Application\chrome.exe",
            "ETAPA3: instalação por usuário exige a raiz do mesmo escopo");
        foreach (var invalid in new[]
        {
            metadata with { Machine = 0 }, metadata with { Machine = 0x14c }, metadata with { Magic = PEMagic.PE32 },
            metadata with { Product = "Outro produto" }, metadata with { Company = "Outro editor" },
            metadata with { Version = "131.0.1.2" }, metadata with { Version = "inválida" },
            metadata with { Executable = false }, metadata with { Dll = true }, metadata with { Managed = true }
        })
            check(InstalledUpdateIdentity.CorrelateBinary(app, invalid) == "unknown",
                "ETAPA3: PE, produto, fabricante ou versão conflitante não aprova arquitetura");
        check(InstalledUpdateIdentity.CaptureArchitecture(App(icon: @"C:\Ausente\fixture.exe")) == "unknown",
            "ETAPA3: prova local indisponível conserva inventário e consulta sem arquitetura");

        var clock = new Clock();
        var history = new UpdateResultHistory(clock);
        var installed = new[] { new InstalledApp("Fixture", "1.0", "Editor", InventoryId: "fixture") };
        UpdateMatch Candidate(string version = "2.0") => new(installed[0], new("fixture", "Fixture", version, "Editor"), MatchState.Candidate);
        UpdateMatch[] none = [new(installed[0], null, MatchState.Unmatched)];
        history.Apply(installed, [Candidate()], incomplete: true);
        var cancelled = history.Apply(installed, none, incomplete: true).Single();
        check(cancelled is { State: MatchState.Candidate, RequiresReview: true, IsStale: true },
            "ETAPA3: primeira consulta parcial seguida de cancelamento preserva candidato em revisão");
        clock.Seconds = 10;
        history.Apply(installed, [Candidate("3.0")], incomplete: true);
        check(history.Apply(installed, none, incomplete: true).Single().Catalog?.ProductVersion == "3.0",
            "ETAPA3: cancelamento preserva a última versão publicada em consulta parcial");
        clock.Seconds = 1801;
        check(history.Apply(installed, none, incomplete: true).Single().State == MatchState.Unmatched,
            "ETAPA3: consultas parciais e cancelamentos sucessivos não renovam validade da evidência antiga");
        history.Apply(installed, [Candidate()], incomplete: true);
        var changed = new[] { installed[0] with { InventoryId = "outro registro" } };
        check(!history.Apply(changed, [new(changed[0], null, MatchState.Unmatched)], true).Single().IsStale,
            "ETAPA3: troca de instalação invalida candidato consultivo preservado");
        history.Apply(installed, [Candidate()], incomplete: true);
        history.Apply(installed, none, incomplete: false);
        check(history.Apply(installed, none, incomplete: true).Single().State == MatchState.Unmatched,
            "ETAPA3: consulta completa sem candidato substitui evidência parcial anterior");
        var bounded = new UpdateResultHistory(clock);
        IReadOnlyList<UpdateMatch> last = [];
        for (int i = 0; i < 100; i++)
            last = bounded.Apply(installed, [Candidate() with { Observations =
                [new("fixture", "fixture", "Fonte", "2.0", "https://example.invalid/", DateTimeOffset.UnixEpoch.AddSeconds(i))] }], true);
        check(last.Single().Observations.Count <= 16 && last.Single().IsStale &&
            last.Single().Observations[0].ObservedAt == DateTimeOffset.UnixEpoch.AddSeconds(99),
            "ETAPA3: falhas sucessivas retêm evidência recente com memória limitada por aplicativo");

        foreach (var scenario in new[] { "bloqueado", "assinatura recusada", "UAC recusado", "falha", "confirmado", "reinício", "cancelamento após início", "timeout" })
        {
            using var cancellation = new CancellationTokenSource();
            var gate = new UpdateLaunchGate(); int launches = 0;
            var result = await gate.RunAsync(() => Task.FromResult(scenario is not ("bloqueado" or "assinatura recusada")), () =>
            {
                launches++;
                if (scenario == "UAC recusado") throw new Win32Exception(1223);
                if (scenario == "cancelamento após início") cancellation.Cancel();
                return Task.FromResult(scenario == "falha" ? 1603 : scenario == "reinício" ? 3010 : 0);
            }, () => scenario == "timeout" ? Task.FromException<bool>(new TimeoutException()) : Task.FromResult(true), cancellation.Token);
            var expected = scenario switch
            {
                "bloqueado" or "assinatura recusada" => UpdateAttemptState.Blocked,
                "UAC recusado" => UpdateAttemptState.Cancelled,
                "falha" => UpdateAttemptState.Failed,
                "cancelamento após início" or "timeout" => UpdateAttemptState.Indeterminate,
                _ => UpdateAttemptState.Confirmed
            };
            var repeated = await gate.RunAsync(() => Task.FromResult(true), () => { launches++; return Task.FromResult(0); }, () => Task.FromResult(true));
            check(result.State == expected && repeated.State == UpdateAttemptState.AlreadyAttempted && launches ==
                (expected == UpdateAttemptState.Blocked ? 0 : 1) && (scenario != "UAC recusado" || result.AuthorizationDeclined),
                "ETAPA3: fluxo inerte " + scenario + " respeita pré-verificação, estado final e tentativa única");
        }
    }
}
