using GerenciadorAplicativos;

internal static class ElevatedOperationCheckpointTests
{
    internal static void Run(Action<bool, string> check)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("GF-04 checkpoint requires Windows.");
        string root = Path.Combine(Path.GetTempPath(), "GF04_CHECKPOINT_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string NewFolder(string name) => Directory.CreateDirectory(Path.Combine(root, name)).FullName;
        string Marker(string folder) => Path.Combine(folder, PersistentElevatedOperationCheckpoint.MarkerName);
        ElevatedOperationAdmission Gate(string folder) => new(new PersistentElevatedOperationCheckpoint(folder));
        ElevatedObservation confirmed = new(ElevatedObservationState.Completed, 0);
        try
        {
            string normal = NewFolder("normal");
            var first = Gate(normal);
            using (var lease = first.TryAcquire() ?? throw new InvalidOperationException("fixture"))
            {
                check(File.Exists(Marker(normal)) && new FileInfo(Marker(normal)).Length == 16,
                    "GF-04 recuperação: posse confirmada somente após marcador persistido");
                var other = Gate(normal);
                check(other.TryAcquire() is null && other.IsInconclusive,
                    "GF-04 recuperação: segunda instância não adquire durante execução");
                bool denied = false;
                try { File.Delete(Marker(normal)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { denied = true; }
                check(denied && File.Exists(Marker(normal)),
                    "GF-04 recuperação: handle próprio impede remover marcador durante posse");
                lease.Complete(confirmed);
            }
            check(!first.IsBlocked && !File.Exists(Marker(normal)),
                "GF-04 recuperação: terminal confirmado libera somente marcador próprio");
            using (var lease = Gate(normal).TryAcquire() ?? throw new InvalidOperationException("fixture"))
                lease.NoLaunch();
            check(!File.Exists(Marker(normal)), "GF-04 recuperação: não lançamento comprovado permite nova sessão");

            string uncertain = NewFolder("uncertain");
            var old = Gate(uncertain);
            using (var lease = old.TryAcquire() ?? throw new InvalidOperationException("fixture"))
            {
                lease.Complete(new(ElevatedObservationState.TimedOut));
                lease.Complete(confirmed);
            }
            byte[] before = File.ReadAllBytes(Marker(uncertain));
            var restarted = Gate(uncertain);
            check(old.IsInconclusive && restarted.TryAcquire() is null &&
                restarted.IsInconclusive && File.ReadAllBytes(Marker(uncertain)).SequenceEqual(before),
                "GF-04 recuperação: timeout e retorno tardio não removem posse entre sessões");

            string abandoned = NewFolder("abandoned");
            using (Gate(abandoned).TryAcquire() ?? throw new InvalidOperationException("fixture")) { }
            check(File.Exists(Marker(abandoned)) && Gate(abandoned).TryAcquire() is null,
                "GF-04 recuperação: descarte sem terminal conserva posse");
            string invalid = NewFolder("invalid");
            File.WriteAllText(Marker(invalid), "fixture sentinela");
            check(Gate(invalid).TryAcquire() is null &&
                File.ReadAllText(Marker(invalid)) == "fixture sentinela",
                "GF-04 recuperação: marcador inválido é preservado e não autoriza");
            string absent = Path.Combine(root, "nonexistent");
            var unavailable = Gate(absent);
            check(unavailable.TryAcquire() is null && unavailable.IsInconclusive && !Directory.Exists(absent),
                "GF-04 recuperação: destino indisponível não admite nem usa fallback");
            var beginFails = new CheckpointFaultFixture(begin: false, clear: true);
            var failedAdmission = new ElevatedOperationAdmission(beginFails);
            check(failedAdmission.TryAcquire() is null && failedAdmission.IsInconclusive &&
                failedAdmission.TryAcquire() is null && beginFails.BeginCalls == 1,
                "GF-04 recuperação: falha de persistência não admite nem repete aquisição");
            var clearFails = new CheckpointFaultFixture(begin: true, clear: false);
            var failedRelease = new ElevatedOperationAdmission(clearFails);
            using (var lease = failedRelease.TryAcquire() ?? throw new InvalidOperationException("fixture"))
                lease.Complete(confirmed);
            check(failedRelease.IsInconclusive && failedRelease.TryAcquire() is null && clearFails.ClearCalls == 1,
                "GF-04 recuperação: falha de liberação bloqueia mesmo após terminal confirmado");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}

internal sealed class CheckpointFaultFixture(bool begin, bool clear) : IElevatedOperationCheckpoint
{
    internal int BeginCalls, ClearCalls;
    public bool TryBegin() { BeginCalls++; return begin; }
    public bool TryClear() { ClearCalls++; return clear; }
    public void Retain() { }
}
