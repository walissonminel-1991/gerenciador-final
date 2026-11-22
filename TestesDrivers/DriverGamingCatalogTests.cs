using System.Text;
using GerenciadorAplicativos;

internal static class DriverGamingCatalogTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string nvidiaId = @"PCI\VEN_10DE&DEV_2D04";
        var gpu = Device(nvidiaId, "Adaptador de vídeo", "NVIDIA", "32.0.16.2124");
        var result = Catalog(gpu);
        check(result is
        {
            Model: "NVIDIA GeForce RTX 5060 Ti",
            References: [{ Channel: "Game Ready", Version: "621.24" }, { Channel: "Studio", Version: "621.14" }]
        } && result.InstalledComparable == new Version(621, 24),
            "RTX 5060 Ti confirmada por PCI com Game Ready, Studio e conversão da versão NVIDIA");

        var report = new StringBuilder();
        DriverGamingCatalog.AppendReport(report, result!);
        check(report.ToString().Contains("Canal do driver instalado indeterminado") &&
              report.ToString().Contains("Game Ready 621.24 (mesma versão numérica)") &&
              report.ToString().Contains("Studio 621.14 (referência numericamente inferior") &&
              report.ToString().Contains(DriverGamingCatalog.ReviewedOn),
            "Relatório separa versão instalada, canal indeterminado e referências oficiais datadas");
        check(Catalog(gpu with { Provider = "Microsoft" })?.InstalledComparable is null &&
              Catalog(gpu with
              {
                  DeviceBinding = DriverBindingEvidence.FromProperties("oem7.inf", "32.0.16.2114", nvidiaId)
              })?.InstalledComparable is null,
            "Versão NVIDIA comparável exige fornecedor NVIDIA e ausência de divergência registrada");

        check(Catalog(gpu, false) is { References.Count: 0 } &&
              Catalog(gpu with { Present = false }) is null &&
              Catalog(gpu with { ReportedDriverClass = "Media" }) is null &&
              Catalog(gpu with { HardwareIds = [nvidiaId, @"PCI\VEN_10DE&DEV_2D05"] }) is null &&
              Catalog(gpu with { HardwareIds = [] }) is null &&
              Catalog(Device(@"PCI\VEN_10DE&DEV_2D05", "NVIDIA GeForce RTX 5060 Ti", "NVIDIA", gpu.Version)) is null &&
              DriverGamingCatalog.Find(new([gpu, gpu], false, ""), gpu, true) is null &&
              DriverGamingCatalog.Find(new([gpu], true, ""), gpu, true) is null,
            "Referências não são atribuídas por nome, classe incorreta, presença incerta, PCI divergente ou SO sem cobertura");

        var a = Device(@"PCI\VEN_8086&DEV_E20B", "Arc", "Intel Corporation", "32.0.101.9175");
        var b = Device(@"PCI\VEN_8086&DEV_E20C", "Arc", "Intel Corporation", "32.0.101.9175");
        check(Catalog(a) is { Model: "Intel Arc B580", References: [{ Version: "32.0.101.9175" }] } &&
              Catalog(b) is { Model: "Intel Arc B570", References: [{ Version: "32.0.101.9175" }] } &&
              Catalog(a)?.InstalledComparable == new Version(32, 0, 101, 9175),
            "Intel Arc B580 e B570 são distinguidas pelo DeviceID e usam versão nativa de quatro partes");

        const string amdId = @"PCI\VEN_1002&DEV_7550";
        var amd = Device(amdId, "AMD Radeon RX 9070 XT", "Advanced Micro Devices, Inc.", "32.0.32029.2010");
        var verifiedAmd = amd with
        {
            DeviceBinding = DriverBindingEvidence.FromProperties(amd.InfName, amd.Version, amdId)
        };
        check(Catalog(amd) is { Model: "AMD Navi 48 (modelo comercial a confirmar)", References.Count: 0 } &&
              Catalog(verifiedAmd) is { References: [{ Channel: "Adrenalin Edition", Version: "26.11.2" }], InstalledComparable: null } &&
              Catalog(verifiedAmd with { DeviceName = "Radeon Graphics" }) is { References.Count: 0 },
            "AMD preserva a ambiguidade do ID compartilhado e separa versão do pacote Adrenalin");

        var pages = new[] { result!, Catalog(a)!, Catalog(verifiedAmd)! }
            .SelectMany(info => info.References.Select(release => release.Page).Prepend(info.Support));
        check(pages.All(page => page.Scheme == "https" && page.Query.Length == 0 &&
              page.Host is "www.nvidia.com" or "www.amd.com" or "www.intel.com" or "downloadmirror.intel.com") &&
              !DriverUpdatePilot.AllowedClass("Display"),
            "Páginas gráficas consultivas usam referências oficiais e o piloto exclui Display");
    }

    private static DriverInventoryEntry Device(string id, string name, string provider, string version) =>
        new(id + @"\INSTANCE", name, provider, version, "oem7.inf", true, true)
        { HardwareIds = [id + "&REV_A1", id], ReportedDriverClass = "Display" };

    private static DriverGamingInfo? Catalog(DriverInventoryEntry device, bool windows11X64 = true) =>
        DriverGamingCatalog.Find(new([device], false, ""), device, windows11X64);
}
