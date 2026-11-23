namespace GerenciadorAplicativos;

// Referências WMI da associação oficial MSFT_PhysicalDiskToStorageReliabilityCounter.
// Não converter por nome, ordem ou semelhança de DeviceId. O vínculo só é aceito
// quando ambos os caminhos WMI são únicos e o DeviceId concorda com a API.
internal sealed record StorageReferencedPhysical(StorageIdentity Disk, string? WmiPath);
internal sealed record StorageReferencedReliability(StorageReliabilityRaw Counter, string? WmiPath);
internal sealed record StorageAssociationReference(string? PhysicalDisk, string? StorageReliabilityCounter);

internal static class StorageAssociationLinker
{
    private const int MaxRows = 256;
    private const string PhysicalClass = "MSFT_PhysicalDisk";
    private const string ReliabilityClass = "MSFT_StorageReliabilityCounter";
    private const string StorageNamespace = @"\root\Microsoft\Windows\Storage";

    // WMI associa objetos por REF, não necessariamente por DeviceId. Aceitar
    // __RELPATH ou caminho absoluto do MESMO namespace; não interpretar IDs.
    internal static string? CanonicalReference(string? path, string expectedClass)
    {
        if (expectedClass is not (PhysicalClass or ReliabilityClass) ||
            string.IsNullOrWhiteSpace(path) || path.Length > 8192 ||
            path.Any(char.IsControl))
            return null;
        string value = path.Trim();
        string classPrefix = expectedClass + ".";
        if (value.StartsWith(classPrefix, StringComparison.OrdinalIgnoreCase))
            return value;
        string marker = ":" + classPrefix;
        int separator = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (separator <= 0 || !value.StartsWith(@"\\", StringComparison.Ordinal))
            return null;
        string prefix = value[..separator];
        if (!prefix.EndsWith(StorageNamespace, StringComparison.OrdinalIgnoreCase))
            return null;
        string serverPart = prefix[..^StorageNamespace.Length];
        if (!serverPart.StartsWith(@"\\", StringComparison.Ordinal))
            return null;
        string host = serverPart[2..];
        if (host.Length == 0 || host.Contains('\\') ||
            !(host.Equals(".", StringComparison.OrdinalIgnoreCase) ||
              host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)))
            return null;
        return value[(separator + 1)..];
    }

    internal static StorageSensorsSnapshot Join(
        IEnumerable<StorageReferencedPhysical> physical,
        IEnumerable<StorageReferencedReliability> reliability,
        IEnumerable<StorageAssociationReference> associations,
        bool sourcePartial = false)
    {
        var disks = physical.Take(MaxRows + 1).ToArray();
        var counters = reliability.Take(MaxRows + 1).ToArray();
        var links = associations.Take(MaxRows + 1).ToArray();
        bool partial = sourcePartial || disks.Length > MaxRows ||
            counters.Length > MaxRows || links.Length > MaxRows;

        // Rejeitar referências inválidas sem impedir o reconhecimento do disco.
        var physicalPaths = disks.Take(MaxRows).Select(x =>
            (Row: x, Ref: CanonicalReference(x.WmiPath, PhysicalClass))).ToArray();
        var counterPaths = counters.Take(MaxRows).Select(x =>
            (Row: x, Ref: CanonicalReference(x.WmiPath, ReliabilityClass))).ToArray();
        if (physicalPaths.Any(x => x.Ref is null) ||
            counterPaths.Any(x => x.Ref is null))
            partial = true;

        var physicalByRef = physicalPaths.Where(x => x.Ref is not null)
            .GroupBy(x => x.Ref!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        var counterByRef = counterPaths.Where(x => x.Ref is not null)
            .GroupBy(x => x.Ref!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);

        var validEdges = new List<(string Physical, string Counter)>();
        foreach (var row in links.Take(MaxRows))
        {
            var p = CanonicalReference(row.PhysicalDisk, PhysicalClass);
            var c = CanonicalReference(row.StorageReliabilityCounter, ReliabilityClass);
            if (p is null || c is null ||
                !physicalByRef.ContainsKey(p) || !counterByRef.ContainsKey(c))
            {
                partial = true;
                continue;
            }
            validEdges.Add((p, c));
        }
        var usedPhysical = validEdges.GroupBy(x => x.Physical, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedCounter = validEdges.GroupBy(x => x.Counter, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (usedPhysical.Count > 0 || usedCounter.Count > 0)
            partial = true;

        var matched = new List<StorageReliabilityRaw>();
        foreach (var edge in validEdges)
        {
            if (usedPhysical.Contains(edge.Physical) || usedCounter.Contains(edge.Counter))
                continue;
            var physicalCandidates = physicalByRef[edge.Physical];
            var counterCandidates = counterByRef[edge.Counter];
            if (physicalCandidates.Length != 1 || counterCandidates.Length != 1)
            {
                partial = true;
                continue;
            }
            string diskId = physicalCandidates[0].Row.Disk.DeviceId;
            string sensorId = counterCandidates[0].Row.Counter.DeviceId;
            if (string.IsNullOrWhiteSpace(diskId) ||
                !string.Equals(diskId, sensorId, StringComparison.OrdinalIgnoreCase))
            {
                partial = true;
                continue;
            }
            matched.Add(counterCandidates[0].Row.Counter);
        }
        // O Join já recusa DeviceId físico e contador duplicados. Contadores
        // sem associação NÃO são usados mesmo se os seus IDs forem iguais.
        return WindowsStorageSensorProvider.Join(
            disks.Take(MaxRows).Select(x => x.Disk), matched, partial);
    }
}
