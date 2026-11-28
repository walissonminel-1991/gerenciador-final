using GerenciadorAplicativos;

internal static class DriverStoreTests
{
    internal static void Run(Action<bool, string> check)
    {
        string windows = Path.Combine(Path.GetTempPath(), "GF-Windows-Fixture");
        string storePath = Path.Combine(windows, "System32", "DriverStore", "FileRepository", "video_amd64", "video.inf");
        string published = Path.Combine(windows, "INF", "oem1.inf");
        var mapping = DriverStoreAudit.CheckMapping("oem1.inf", storePath, published, windows);
        check(mapping.State == DriverStoreState.Mapped && !mapping.CanMaintain,
            "Driver Store: round trip explícito não habilita manutenção");
        check(DriverStoreAudit.CheckNativeMapping("oem1.inf", storePath, "OEM1.INF", windows).State == DriverStoreState.Mapped,
            "Fronteira nativa: nome publicado simples só resolve o INF exato no diretório INF do Windows");
        foreach (string wrong in new[] { "oem2.inf", "../oem1.inf", "INF/oem1.inf", "oem1.inf:stream" })
            check(DriverStoreAudit.CheckNativeMapping("oem1.inf", storePath, wrong, windows).State == DriverStoreState.Inconsistent,
                "Fronteira nativa recusa nome diferente ou caminho relativo arbitrário: " + wrong);
        foreach (string bad in new[] { "../oem1.inf", "..inf", "oem1.inf:stream", @"C:\oem1.inf", "oem1.inf ", "" })
            check(!DriverPackageLinks.IsValidInf(bad), "INF não aceita caminho, ADS ou nome inválido: " + bad);
        check(DriverStoreAudit.CheckMapping("oem1.inf", storePath,
                Path.Combine(windows, "INF", "oem2.inf"), windows).State == DriverStoreState.Inconsistent,
            "Driver Store: pacote de outro INF não confirma correspondência");
        check(DriverStoreAudit.CheckMapping("oem1.inf", storePath.Replace("FileRepository", "FileRepositoryOutro"),
            published, windows).State == DriverStoreState.Inconsistent,
            "Driver Store: prefixo parecido fora do repositório é recusado");
        check(DriverStoreAudit.CheckMapping("oem1.inf", Path.Combine(windows, "System32", "DriverStore",
            "FileRepository", "..", "externo.inf"), published, windows).State == DriverStoreState.Inconsistent,
            "Driver Store: traversal recusado antes de normalizar");
        var first = new DriverInventoryEntry("PCI\\VIDEO", "Vídeo", "Fabricante", "1.0", "oem1.inf", true, true);
        var devices = new[] { first, first with { DeviceId = "PCI\\VIDEO2" },
            first with { DeviceId = "PCI\\NET", DeviceName = "Rede", InfName = "oem2.inf" },
            first with { DeviceId = "PCI\\CHIPSET", DeviceName = "Chipset", InfName = "oem3.inf" },
            first with { DeviceId = "PCI\\STORAGE", DeviceName = "Storage", InfName = "oem4.inf" },
            first with { DeviceId = "SYSTEM\\CORE", DeviceName = "Sistema", InfName = "system.inf" } };
        var inventory = new DriverInventoryResult(devices, false, "fixture");
        int calls = 0;
        DriverStoreEvidence Probe(string inf) { calls++; return new(inf, DriverStoreState.Mapped); }
        var complete = DriverStoreAudit.Capture(inventory, default, Probe);
        check(calls == 5 && complete.Packages.Count == 5 && !complete.Partial &&
            complete.Packages.All(x => !x.CanMaintain),
            "Vídeo/rede/chipset/storage/sistema: consulta única por INF, múltiplos dispositivos preservados");
        var capped = DriverStoreAudit.Capture(inventory, default, Probe, maximumPackages: 2);
        check(capped.Partial && capped.Packages.Count(x => x.State == DriverStoreState.NotConsulted) == 3,
            "Driver Store: teto mantém pacotes não consultados como desconhecidos");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        calls = 0;
        var stopped = DriverStoreAudit.Capture(inventory, cancelled.Token, Probe);
        check(calls == 0 && stopped.Partial && stopped.Packages.All(x => x.State == DriverStoreState.NotConsulted),
            "Driver Store: cancelamento não consulta nem converte ausência em pacote removível");
        check(DriverStoreAudit.Capture(inventory, default, Probe, TimeSpan.Zero).Partial,
            "Driver Store: prazo esgotado preserva a matriz parcial");
        check(DriverStoreAudit.Capture(inventory, default, _ => throw new UnauthorizedAccessException()).Partial,
            "Driver Store: acesso negado é indisponível sem elevação ou fallback de comando");
        check(DriverStoreAudit.Capture(inventory, default,
            _ => new("outro.inf", DriverStoreState.Mapped)).Packages.All(x => x.State == DriverStoreState.Inconsistent),
            "Driver Store: resultado atribuído a INF errado é rejeitado");
        check(DriverStoreAudit.Capture(inventory with { Partial = true }, default, Probe).Partial,
            "Driver Store: API bem-sucedida não apaga parcialidade WMI");
        // Prova a instrumentação sem SetupAPI real: preserva a ABI de duas
        // chamadas, identifica a última fase iniciada e não altera o resultado.
        var trace = new List<(DriverStoreAudit.AuditPhase Phase,bool Returned)>();
        void Trace(DriverStoreAudit.AuditPhase phase,bool returned,long _) => trace.Add((phase,returned));
        string raw = "oem1.inf\0system.inf\0\0";
        var enumerated = DriverPublishedPackages.EnumerateCore(
            () => (true,(uint)raw.Length),
            buffer => { raw.CopyTo(0,buffer,0,raw.Length); return (true,(uint)raw.Length); }, Trace);
        check(enumerated.SequenceEqual(new[]{"oem1.inf","system.inf"}) &&
            trace.SequenceEqual(new[]{
                (DriverStoreAudit.AuditPhase.PublishedListSize,false),
                (DriverStoreAudit.AuditPhase.PublishedListSize,true),
                (DriverStoreAudit.AuditPhase.PublishedListRead,false),
                (DriverStoreAudit.AuditPhase.PublishedListRead,true)}),
            "Drivers: trace separa tamanho e leitura da lista INF sem alterar evidência");
        trace.Clear();
        bool readStarted=false, failedSize=false;
        try
        {
            DriverPublishedPackages.EnumerateCore(() => (false,0u),
                _ => { readStarted=true; return (true,0u); }, Trace);
        }
        catch (IOException) { failedSize=true; }
        check(failedSize && !readStarted && trace.SequenceEqual(new[]{
            (DriverStoreAudit.AuditPhase.PublishedListSize,false),
            (DriverStoreAudit.AuditPhase.PublishedListSize,true)}),
            "Drivers: falha no tamanho não inicia segunda chamada nativa");
        trace.Clear();
        var list = DriverPublishedPackages.Capture(inventory,default,() => enumerated,Probe,observe:Trace);
        check(list.Packages.Count==2 && list.Packages.All(x=>!x.CanMaintain) &&
            trace.SequenceEqual(new[]{
                (DriverStoreAudit.AuditPhase.PublishedResolve,false),
                (DriverStoreAudit.AuditPhase.PublishedResolve,true)}),
            "Drivers: resolução publicada rastreada sem mudar autorização");
        trace.Clear();
        using var cancelResolve = new CancellationTokenSource();
        bool resolveStopped=false;
        try
        {
            DriverPublishedPackages.Capture(inventory,cancelResolve.Token,() => enumerated,
                inf => { cancelResolve.Cancel(); return new(inf,DriverStoreState.Mapped); },observe:Trace);
        }
        catch (OperationCanceledException) { resolveStopped=true; }
        check(resolveStopped && trace.SequenceEqual(new[]{
            (DriverStoreAudit.AuditPhase.PublishedResolve,false),
            (DriverStoreAudit.AuditPhase.PublishedResolve,true)}),
            "Drivers: trace conserva cancelamento durante resolução sem fotografia válida");
        check(DriverInventory.Correlate([first], new Dictionary<string, bool?>
            { [first.DeviceId] = true, [first.DeviceId.ToLowerInvariant()] = false }).Single().Present is null,
            "Presenças contraditórias do mesmo ID não são resolvidas pela ordem de leitura");
    }
}
