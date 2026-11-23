using GerenciadorAplicativos;

internal static class StorageReliabilityCounterMapperTests
{
    internal static void Run()
    {
        static void Check(bool ok, string reason)
        {
            if (!ok) throw new InvalidOperationException("Storage reliability: " + reason);
        }

        Check(StorageReliabilityCounterMapper.ParseUnsigned((ulong)123) == 123 &&
              StorageReliabilityCounterMapper.ParseUnsigned("18446744073709551614") ==
                ulong.MaxValue - 1 &&
              StorageReliabilityCounterMapper.ParseUnsigned("0") == 0 &&
              StorageReliabilityCounterMapper.ParseUnsigned(-1) is null &&
              StorageReliabilityCounterMapper.ParseUnsigned("-1") is null &&
              StorageReliabilityCounterMapper.ParseUnsigned(ulong.MaxValue) is null &&
              StorageReliabilityCounterMapper.ParseUnsigned("18446744073709551615") is null &&
              StorageReliabilityCounterMapper.ParseUnsigned(double.NaN) is null &&
              StorageReliabilityCounterMapper.ParseUnsigned(null) is null,
              "conversão de UInt64/COM deve rejeitar nulo, negativo, float e sentinela");

        var rawA = new StorageReliabilityCounterRaw(
            8UL, 3UL, 2UL, 0UL, 19UL, 22UL, 4UL, 150U, 320U);
        var rawB = new StorageReliabilityCounterRaw(
            "0", null, 0UL, null, null, 0UL, 0UL, 0U, 0U);
        var mapped = StorageReliabilityCounterMapper.Map(rawA, out bool contradictory);
        Check(!contradictory && mapped is not null &&
              mapped.ReadErrorsTotal.Value == 8 &&
              mapped.ReadErrorsUncorrected.Value == 3 &&
              mapped.WriteErrorsTotal.Value == 2 &&
              mapped.WriteErrorsUncorrected.Value == 0 &&
              mapped.ReadLatencyMaxMs.Value == 19 &&
              mapped.WriteLatencyMaxMs.Value == 22 &&
              mapped.FlushLatencyMaxMs.Value == 4 &&
              mapped.StartStopCycles.Value == 150 &&
              mapped.LoadUnloadCycles.Value == 320,
            "contadores reais de erros, latência (ms) e ciclos devem conservar unidade e origem");

        var missing = StorageReliabilityCounterMapper.Map(rawB, out bool noContradiction);
        Check(!noContradiction && missing is not null &&
              missing.ReadErrorsTotal.Value == 0 &&
              missing.WriteErrorsTotal.Value == 0 &&
              missing.ReadErrorsUncorrected.Value is null &&
              missing.ReadLatencyMaxMs.Value is null &&
              missing.WriteLatencyMaxMs.Value is null &&
              missing.FlushLatencyMaxMs.Value is null &&
              missing.StartStopCycles.Value is null,
            "zero informado em erro difere de latência/ciclo sem suporte comprovado");
        Check(StorageReliabilityCounterMapper.Map(null, out _) is null &&
              StorageReliabilityCounterMapper.Latency(86_400_001UL, "ReadLatencyMax").Value is null &&
              StorageReliabilityCounterMapper.Cycles((ulong)uint.MaxValue + 1UL, "StartStopCycleCount").Value is null,
            "fonte ausente e valores inválidos não podem ser sensores reais");

        var conflicts = StorageReliabilityCounterMapper.Map(
            rawA with { ReadErrorsTotal = 1UL, ReadErrorsUncorrected = 5UL },
            out bool invalid);
        Check(invalid && conflicts is not null &&
              conflicts.ReadErrorsTotal.Value is null &&
              conflicts.ReadErrorsUncorrected.Value is null &&
              conflicts.WriteErrorsTotal.Value == 2 &&
              conflicts.ReadLatencyMaxMs.Value == 19,
            "erro não corrigido superior ao total invalida somente contadores contraditórios");

        const string physicalA = "MSFT_PhysicalDisk.DeviceId=\"disk-A\"";
        const string physicalB = "MSFT_PhysicalDisk.DeviceId=\"disk-B\"";
        const string counterA = "MSFT_StorageReliabilityCounter.DeviceId=\"disk-A\"";
        const string counterB = "MSFT_StorageReliabilityCounter.DeviceId=\"disk-B\"";
        var disks = new[]
        {
            new StorageReferencedPhysical(new StorageIdentity("disk-A", "SSD A"), physicalA),
            new StorageReferencedPhysical(new StorageIdentity("disk-B", "SSD B"), physicalB)
        };
        var rows = new[]
        {
            new StorageReferencedReliability(
                new StorageReliabilityRaw("disk-B", 40, 10, 90) { Diagnostics = rawB }, counterB),
            new StorageReferencedReliability(
                new StorageReliabilityRaw("disk-A", 43, 11, 91) { Diagnostics = rawA }, counterA)
        };
        var linkA = new StorageAssociationReference(physicalA, counterA);
        var linkB = new StorageAssociationReference(physicalB, counterB);
        var correct = StorageAssociationLinker.Join(disks, rows, [linkB, linkA]);
        Check(correct.State == HardwareDiscoveryState.Complete &&
              correct.Disks[0].Diagnostics?.ReadErrorsTotal.Value == 8 &&
              correct.Disks[1].Diagnostics?.ReadErrorsTotal.Value == 0 &&
              correct.Disks[1].Diagnostics?.ReadLatencyMaxMs.Value is null,
            "associação WMI REF deve prevalecer sobre a ordem de enumeração");

        var absentLinks = StorageAssociationLinker.Join(disks, rows, []);
        Check(absentLinks.Disks.All(x => x.Diagnostics is null),
            "ID igual sem vínculo WMI não autoriza contadores de erro");

        var duplicate = StorageAssociationLinker.Join(disks, rows, [linkA, linkA, linkB]);
        Check(duplicate.State == HardwareDiscoveryState.Partial &&
              duplicate.Disks[0].Diagnostics is null &&
              duplicate.Disks[1].Diagnostics?.ReadErrorsTotal.Value == 0,
            "duplicação de referência invalida contadores só do disco ambíguo");

        var mismatch = StorageAssociationLinker.Join(disks, rows,
            [new StorageAssociationReference(physicalA, counterB)]);
        Check(mismatch.State == HardwareDiscoveryState.Partial &&
              mismatch.Disks.All(x => x.Diagnostics is null),
            "REF contraditória não pode atribuir contadores ao outro disco");

        var inconsistentDisk = StorageAssociationLinker.Join(disks,
            rows.Select(x => x.Counter.DeviceId == "disk-A"
                ? x with { Counter = x.Counter with {
                    Diagnostics = rawA with { WriteErrorsTotal = 0, WriteErrorsUncorrected = 2 } } }
                : x),
            [linkA, linkB]);
        Check(inconsistentDisk.State == HardwareDiscoveryState.Partial &&
              inconsistentDisk.Disks[0].Diagnostics?.WriteErrorsTotal.Value is null &&
              inconsistentDisk.Disks[0].Diagnostics?.ReadErrorsTotal.Value == 8 &&
              inconsistentDisk.Disks[1].Diagnostics?.ReadErrorsTotal.Value == 0,
            "contadores contraditórios afetam somente a classe/métrica correspondente");

        Console.WriteLine("OK monitoramento Storage: erros, latências e ciclos por REF WMI sem inferir saúde.");
    }
}
