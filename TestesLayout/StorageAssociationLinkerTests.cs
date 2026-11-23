using GerenciadorAplicativos;

internal static class StorageAssociationLinkerTests
{
    internal static void Run()
    {
        static void Check(bool ok, string name)
        { if (!ok) throw new InvalidOperationException("Vínculo físico Storage: " + name); }

        const string physicalA = "MSFT_PhysicalDisk.DeviceId=\"disk-A\"";
        const string physicalB = "MSFT_PhysicalDisk.DeviceId=\"disk-B\"";
        const string counterA = "MSFT_StorageReliabilityCounter.DeviceId=\"disk-A\"";
        const string counterB = "MSFT_StorageReliabilityCounter.DeviceId=\"disk-B\"";
        var physical = new[]
        {
            new StorageReferencedPhysical(new StorageIdentity("disk-A", "SSD físico A"), physicalA),
            new StorageReferencedPhysical(new StorageIdentity("disk-B", "SSD físico B"), physicalB)
        };
        var sensors = new[]
        {
            new StorageReferencedReliability(
                new StorageReliabilityRaw("disk-B", (byte)45, (byte)13, (ushort)230), counterB),
            new StorageReferencedReliability(
                new StorageReliabilityRaw("disk-A", (byte)36, (byte)25, (ushort)90), counterA)
        };
        var associations = new[]
        {
            new StorageAssociationReference(physicalA, counterA),
            new StorageAssociationReference(physicalB, counterB)
        };

        Check(StorageAssociationLinker.CanonicalReference(physicalA, "MSFT_PhysicalDisk") == physicalA &&
              StorageAssociationLinker.CanonicalReference(
                @"\\LOCALHOST\root\Microsoft\Windows\Storage:" + physicalA, "MSFT_PhysicalDisk") == physicalA,
            "caminho local absoluto e relativo devem indicar a mesma instância WMI");
        Check(StorageAssociationLinker.CanonicalReference(physicalA, "MSFT_StorageReliabilityCounter") is null &&
              StorageAssociationLinker.CanonicalReference(
                @"\\REMOTE-UNTRUSTED\root\Microsoft\Windows\Storage:" + physicalA,
                "MSFT_PhysicalDisk") is null &&
              StorageAssociationLinker.CanonicalReference(
                @"\\LOCALHOST\root\cimv2:" + physicalA, "MSFT_PhysicalDisk") is null &&
              StorageAssociationLinker.CanonicalReference(
                "MSFT_Disk.Number=\"1\"", "MSFT_PhysicalDisk") is null &&
              StorageAssociationLinker.CanonicalReference(
                "MSFT_PhysicalDisk.DeviceId=\"\n\"", "MSFT_PhysicalDisk") is null &&
              StorageAssociationLinker.CanonicalReference(null, "MSFT_PhysicalDisk") is null,
            "classe/namespace inválido não pode habilitar vínculo WMI");

        var linked = StorageAssociationLinker.Join(physical, sensors,
            [associations[1], associations[0]]);
        Check(linked.State == HardwareDiscoveryState.Complete &&
              linked.Disks.Count == 2 &&
              linked.Disks[0].DeviceId == "disk-A" &&
              linked.Disks[0].TemperatureC.Value == 36 &&
              linked.Disks[1].DeviceId == "disk-B" &&
              linked.Disks[1].TemperatureC.Value == 45,
            "vínculo oficial deve seguir referência WMI, não a ordem ou o nome");

        var noRelation = StorageAssociationLinker.Join(physical, sensors, []);
        Check(noRelation.Disks.Count == 2 &&
              noRelation.Disks.All(d => d.TemperatureC.Value is null &&
                  d.WearPercent.Value is null && d.PowerOnHours.Value is null),
            "mesmo DeviceId sem vínculo formal não autoriza sensor");
        var wrong = StorageAssociationLinker.Join(physical, sensors,
            [new StorageAssociationReference(physicalA, counterB)]);
        Check(wrong.State == HardwareDiscoveryState.Partial &&
              wrong.Disks.All(d => d.TemperatureC.Value is null),
            "associação com DeviceId contraditório deve ser recusada");

        var duplicatedEdge = StorageAssociationLinker.Join(physical, sensors,
            [associations[0], associations[0], associations[1]]);
        Check(duplicatedEdge.State == HardwareDiscoveryState.Partial &&
              duplicatedEdge.Disks[0].TemperatureC.Value is null &&
              duplicatedEdge.Disks[1].TemperatureC.Value == 45,
            "duas referências para o mesmo disco devem invalidar apenas seu sensor");

        var duplicatedTarget = StorageAssociationLinker.Join(physical, sensors,
            [associations[0], new StorageAssociationReference(physicalB, counterA)]);
        Check(duplicatedTarget.State == HardwareDiscoveryState.Partial &&
              duplicatedTarget.Disks.All(d => d.TemperatureC.Value is null),
            "contador ligado a dois discos não autoriza temperatura em nenhum");

        var missingReference = StorageAssociationLinker.Join(physical, sensors,
            [associations[1], new StorageAssociationReference(physicalA, "MSFT_StorageReliabilityCounter.DeviceId=\"C\"")]);
        Check(missingReference.State == HardwareDiscoveryState.Partial &&
              missingReference.Disks[0].TemperatureC.Value is null &&
              missingReference.Disks[1].TemperatureC.Value == 45,
            "referência de sensor ausente não é motivo para usar DeviceId equivalente");

        var duplicateWmiPath = StorageAssociationLinker.Join(
            physical.Append(physical[0] with
            {
                Disk = new StorageIdentity("disk-C", "SSD diferente no mesmo path")
            }),
            sensors, associations);
        Check(duplicateWmiPath.State == HardwareDiscoveryState.Partial &&
              duplicateWmiPath.Disks.All(d => d.DeviceId != "disk-A" ||
                  d.TemperatureC.Value is null) &&
              duplicateWmiPath.Disks.Single(d => d.DeviceId == "disk-B").TemperatureC.Value == 45,
            "WMI object path duplicado deve impedir correspondência arbitrária");

        var missingObjectPath = StorageAssociationLinker.Join(
            physical.Select(x => x.Disk.DeviceId == "disk-A" ? x with { WmiPath = null } : x),
            sensors, associations);
        Check(missingObjectPath.State == HardwareDiscoveryState.Partial &&
              missingObjectPath.Disks[0].TemperatureC.Value is null,
            "WMI sem caminho deve manter disco reconhecido e sensor inconclusivo");

        var failedClass = StorageAssociationLinker.Join(physical, sensors, [],
            sourcePartial: true);
        Check(failedClass.State == HardwareDiscoveryState.Partial &&
              failedClass.Disks.All(d => d.TemperatureC.Value is null),
            "classe de associação ausente não pode recorrer ao DeviceId antigo");

        Console.WriteLine("OK monitoramento Storage: associação formal WMI, duplicatas e anti-fallback.");
    }
}
