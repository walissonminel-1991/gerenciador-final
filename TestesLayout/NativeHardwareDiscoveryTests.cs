using GerenciadorAplicativos;

internal static class NativeHardwareDiscoveryTests
{
    internal static void Run(bool includeRealWindowsScan = false)
    {
        static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("Hardware universal: " + message);
        }

        foreach (var (pnp, kind) in new (string, HardwareDeviceKind)[]
        {
            ("Processor", HardwareDeviceKind.Processor),
            ("Display", HardwareDeviceKind.Graphics),
            ("DiskDrive", HardwareDeviceKind.Storage),
            ("SCSIAdapter", HardwareDeviceKind.Storage),
            ("Net", HardwareDeviceKind.Network),
            ("Bluetooth", HardwareDeviceKind.Bluetooth),
            ("Media", HardwareDeviceKind.Audio),
            ("AudioEndpoint", HardwareDeviceKind.Audio),
            ("USB", HardwareDeviceKind.Usb),
            ("USBDevice", HardwareDeviceKind.Usb),
            ("HIDClass", HardwareDeviceKind.Input),
            ("Keyboard", HardwareDeviceKind.Input),
            ("Mouse", HardwareDeviceKind.Input),
            ("Monitor", HardwareDeviceKind.Monitor),
            ("Battery", HardwareDeviceKind.Battery),
            ("Sensor", HardwareDeviceKind.Sensor),
            ("Camera", HardwareDeviceKind.Camera),
            ("Printer", HardwareDeviceKind.Printer),
            ("Firmware", HardwareDeviceKind.Firmware),
            ("SecurityDevices", HardwareDeviceKind.Security),
            ("System", HardwareDeviceKind.System),
            ("VendorFutureClass", HardwareDeviceKind.Other)
        })
            Check(HardwareDeviceClassifier.Classify(pnp.ToUpperInvariant()) == kind &&
                  HardwareDeviceClassifier.Classify(" " + pnp + " ") == kind,
                "classe PnP foi perdida: " + pnp);

        Check(HardwareDeviceClassifier.Classify(null) == HardwareDeviceKind.Other,
            "classe ausente não pode ser inventada");
        foreach (string instance in new[]
        {
            @"PCI\\VEN_10DE&DEV_0001", @"USB\\VID_1234&PID_5678",
            @"HID\\VID_0000&PID_0000", @"ACPI\\PNP0C02",
            @"BTHENUM\\DEV_1234", @"ROOT\\UNKNOWN", @"SWD\\PRINTENUM"
        })
        {
            string expected = instance.Split('\\')[0];
            Check(HardwareDeviceClassifier.BusOf(instance) == expected,
                "prefixo de barramento incorreto: " + instance);
        }
        Check(HardwareDeviceClassifier.BusOf(null) == "Não informado" &&
              HardwareDeviceClassifier.BusOf("SEM_BARRAMENTO") == "Não informado",
            "barramento ausente deve permanecer inconclusivo");

        if (includeRealWindowsScan)
        {
            var sample = NativeHardwareDiscovery.Capture(maxDevices: 512);
            Check(sample.Source == "Windows SetupAPI (dispositivos presentes)" ||
                  sample.State == HardwareDiscoveryState.Unavailable,
                "origem da consulta não foi preservada");
            Check(sample.DeviceCount <= 512 && sample.CapturedAtUtc.Offset == TimeSpan.Zero,
                "limites ou timestamp inconsistentes");
            Check(sample.Devices.All(d => !string.IsNullOrWhiteSpace(d.Name)),
                "algum dispositivo foi perdido por não possuir nome amigável");
            Check(sample.State != HardwareDiscoveryState.Complete || sample.Limitation is null,
                "inventário completo não pode carregar aviso de parcialidade");
            Check(sample.State != HardwareDiscoveryState.Partial ||
                  !string.IsNullOrWhiteSpace(sample.Limitation),
                "inventário parcial deve explicar o limite");
        }

        Console.WriteLine("OK hardware universal: classificação genérica PnP e buses sem catálogo fixo.");
    }
}
