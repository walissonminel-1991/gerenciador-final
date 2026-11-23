using GerenciadorAplicativos;

internal static class ChipsetPnpDiagnosticTests
{
    internal static void Run()
    {
        int passed = 0;
        void Check(bool condition, string expectation)
        {
            if (!condition) throw new InvalidOperationException("VF03 chipset: " + expectation);
            passed++;
            Console.WriteLine("OK VF03 chipset: " + expectation);
        }

        static ChipsetPnpDiagnosis Diagnose(params DriverInventoryEntry[] devices) =>
            ChipsetPnpDiagnostic.Evaluate(new(devices, false, ""));

        const string amdId = @"PCI\VEN_1022&DEV_790B";
        var smbus = new DriverInventoryEntry(amdId + @"\LOCAL", "Nome local", "", "", "", null, true)
        {
            HardwareIds = [amdId, amdId + "&CC_0C0500"],
            CompatibleIds = [@"pci\ven_1022&cc_0c05", @"PCI\CC_0C0500", @"PCI\CC_0C05"],
            PnpProblemCode = 0
        };
        const string intelId = @"PCI\VEN_8086&DEV_A308";
        var isa = smbus with
        {
            DeviceId = intelId + @"\LOCAL",
            HardwareIds = [intelId],
            CompatibleIds = [@"PCI\CC_060100&DT_0000", @"PCI\CC_0601"],
            PnpProblemCode = 22
        };

        var identified = Diagnose(smbus, isa);
        Check(!identified.Partial && identified.Controllers is
            [{ Role: "SMBus", CatalogModel: "FCH SMBus Controller" },
             { Role: "Ponte PCI–ISA", CatalogModel: "B360 Chipset LPC/eSPI Controller" }],
            "classe PCI e VEN/DEV confirmam funções e nomes do catálogo existente");
        Check(identified.Controllers[0].PnpStatus == smbus.PnpStatusLabel &&
            identified.Describe().Contains("PnP 22", StringComparison.Ordinal),
            "estado PnP 0 e dispositivo desativado mantêm os rótulos do inventário");
        var withoutDriver = DriverInventory.IncludeUnboundProblemDevices([],
            new Dictionary<string, int?> { [smbus.DeviceId] = 28 },
            new Dictionary<string, string>(),
            new Dictionary<string, string[]> { [smbus.DeviceId] = [amdId] },
            new Dictionary<string, string[]> { [smbus.DeviceId] = [@"PCI\CC_0C05"] });
        var presentWithoutDriver = DriverInventory.Correlate(withoutDriver,
            new Dictionary<string, bool?> { [smbus.DeviceId] = true });
        Check(ChipsetPnpDiagnostic.Evaluate(new(presentWithoutDriver, false, ""))
                .Describe().Contains("Driver não instalado (PnP 28)", StringComparison.Ordinal) &&
            Diagnose(isa with { PnpProblemCode = 43 }).Describe().Contains("PnP 43", StringComparison.Ordinal),
            "os códigos 28 sem associação assinada e 43 aparecem como relatos do Windows");
        var unknownStatus = Diagnose(smbus with { PnpProblemCode = null });
        Check(unknownStatus.Partial && unknownStatus.Controllers is [{ PnpStatus: "Estado PnP não informado" }],
            "identidade confirmada permanece visível com estado ausente");
        Check(Diagnose(smbus with { Present = false, PnpProblemCode = 45 }) is
                { Partial: false, Controllers.Count: 0 } &&
            Diagnose(smbus with { Present = null }) is { Partial: true, Controllers.Count: 0 } &&
            Diagnose(smbus with { Present = true, PnpProblemCode = 45 }) is { Partial: true, Controllers.Count: 0 },
            "ignora histórico e sinaliza presença incerta ou contraditória");

        Check(Diagnose(smbus with
            {
                HardwareIds = [amdId],
                CompatibleIds = [@"PCI\CC_0600", @"PCI\CC_0200"],
                DeviceName = "Chipset SMBus", ReportedPnpClass = "System"
            }, smbus with { DeviceId = @"ACPI\PNP0000\LOCAL" }) is { Partial: false, Controllers.Count: 0 },
            "processadores e outros dispositivos de sistema ficam fora do recorte por classe");
        Check(Diagnose(smbus with
            {
                HardwareIds = [amdId],
                CompatibleIds = [@"PCI\CC_0C050", @"PCI\CC_0C050X", "PCI\\CC_0C0500\n", @"PCI\CC_0C05&DT_1"]
            }).Controllers.Count == 0,
            "identificadores de classe malformados não servem de evidência");
        Check(Diagnose(smbus with { HardwareIds = [] }) is { Partial: true, Controllers.Count: 0 } &&
            Diagnose(smbus with { HardwareIds = [intelId] }) is { Partial: true, Controllers.Count: 0 } &&
            Diagnose(smbus with { CompatibleIds = [@"PCI\VEN_8086&CC_0C05"] }) is { Partial: true, Controllers.Count: 0 },
            "rejeita classe ou identidade sem vínculo consistente com o mesmo dispositivo");
        Check(Diagnose(smbus with { CompatibleIds = [@"PCI\CC_0200"] }) is { Partial: true, Controllers.Count: 0 } &&
            Diagnose(smbus, smbus with { DeviceId = smbus.DeviceId.ToLowerInvariant(), PnpProblemCode = 28 }) is
                { Partial: true, Controllers.Count: 0 },
            "classes ou associações conflitantes ficam inconclusivas");

        var incomplete = ChipsetPnpDiagnostic.Evaluate(new([smbus], true, "consulta interrompida"));
        var unassociated = ChipsetPnpDiagnostic.Evaluate(new([], false, "")
            { OtherPnpTargetIds = [@"PCI\CC_0601", intelId] });
        Check(incomplete is { Partial: true, Controllers.Count: 1 } &&
            unassociated is { Partial: true, Controllers.Count: 0 } &&
            !unassociated.Describe().Contains("nenhum", StringComparison.OrdinalIgnoreCase),
            "leituras parciais preservam observações válidas e não declaram ausência");

        const string uncatalogued = @"PCI\VEN_ABCD&DEV_ABCD";
        var unknownModel = Diagnose(smbus with
        {
            DeviceId = uncatalogued + @"\LOCAL",
            HardwareIds = [uncatalogued],
            CompatibleIds = [@"PCI\CC_0C05"]
        });
        Check(unknownModel is { Partial: false, Controllers: [{ Role: "SMBus", CatalogModel: "" }] } &&
            !unknownModel.Describe().Contains(uncatalogued, StringComparison.Ordinal) &&
            !identified.Describe().Contains("Nome local", StringComparison.Ordinal),
            "sem nome no catálogo, exibe apenas a função e omite identificadores e nomes locais");

        Console.WriteLine($"VF03 chipset: {passed} verificações aprovadas.");
    }
}
