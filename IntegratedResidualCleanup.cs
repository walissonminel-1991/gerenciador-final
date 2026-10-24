using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace GerenciadorAplicativos;

internal enum ResidualItemState { Deleted, Absent, Preserved, Failed, AccessDenied, Cancelled }
internal sealed record ResidualItemResult(string Id, string Kind, ResidualItemState State, string Reason, int Error = 0);
internal sealed record IntegratedCleanupResult(string AppId, IReadOnlyList<ResidualItemResult> Items,
    int IntentionallyPreserved, bool Cancelled, bool Verified, IReadOnlyList<ResidualProblem> Problems)
{
    internal int Files => Items.Count(i => i.Kind == "file" && i.State == ResidualItemState.Deleted);
    internal int Directories => Items.Count(i => i.Kind == "directory" && i.State == ResidualItemState.Deleted);
    internal int Keys => Items.Count(i => i.Kind == "registry" && i.State == ResidualItemState.Deleted);
    internal int Failed => Items.Count(i => i.State is ResidualItemState.Failed or ResidualItemState.AccessDenied);
    internal int Preserved => IntentionallyPreserved + Items.Count(i => i.State is not (ResidualItemState.Deleted or ResidualItemState.Absent));
    internal string[] ElevationIds => Items.Where(i => i.State == ResidualItemState.AccessDenied).Select(i => i.Id).ToArray();
}

internal static class IntegratedResidualCleanup
{
    internal const int MaxItems = 5000;
    private const long MaxBytes = 256L * 1024 * 1024;
    internal static bool Expected(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or
        System.Security.SecurityException or InvalidOperationException or ArgumentException or
        NotSupportedException or Win32Exception or CryptographicException;
    internal static int Error(Exception ex) => ex is Win32Exception w ? w.NativeErrorCode :
        ex.InnerException is Win32Exception inner ? inner.NativeErrorCode :
        ((uint)ex.HResult & 0xFFFF0000u) == 0x80070000u ? ex.HResult & 0xFFFF : 0;
    private static string Reason(Exception ex, string fallback) => ex is InvalidDataException &&
        new[] { "registry_changed", "file_changed", "directory_changed", "directory_not_empty", "personal_boundary",
            "inventory_changed", "application_present_or_unknown", "application_running", "root_changed", "scan_budget",
            "scan_depth", "shared_or_size_limit" }.Contains(ex.Message, StringComparer.Ordinal)
            ? ex.Message : Error(ex) is 32 or 33 ? "sharing_violation" : Error(ex) == 6800 ? "registry_transaction_conflict" : fallback;
    internal static string Audit(ResidualAppIdentity app, string stage, string state,
        int files = 0, int folders = 0, int keys = 0, int kept = 0, int failed = 0,
        int error = 0, string reason = "none", long milliseconds = 0, bool cancelled = false) =>
        FormattableString.Invariant($"DESINSTALACAO.RESIDUOS app={app.TechnicalId} etapa={stage} estado={state} arquivos={files} pastas={folders} chaves={keys} preservados={kept} falhas={failed} erro_windows={error} motivo={reason} duracao_ms={milliseconds} cancelamento={(cancelled ? 1 : 0)}");

    internal static string RegistryStage(ResidualKeyEvidence key) => RegistryStage(key.Hive, key.View);
    internal static string RegistryStage(RegistryHive hive, RegistryView view) =>
        (hive == RegistryHive.LocalMachine ? "hklm" : "hkcu") +
        (view == RegistryView.Registry32 ? "32" : "64");

    internal static ResidualUninstallContext Prepare(ProgramEntry app, UninstallSnapshot inventory,
        CancellationToken token = default)
    {
        var identity = ResidualAppIdentity.From(app);
        var scope = IntegratedResidualPolicy.Build(identity);
        var roots = new List<ResidualRootEvidence>();
        var keys = new List<ResidualKeyLocation>();
        var problems = new List<ResidualProblem>();
        int preserved = 0;
        if (!inventory.Complete || inventory.Items.Count(p => UninstallBatch.SameRegistration(app, p)) != 1)
            return new(identity, scope.Policy, [], [], scope.Processes, 0, [new("prepare", "inventory_incomplete_or_changed")]);
        foreach (string path in scope.Roots)
        {
            token.ThrowIfCancellationRequested();
            if (!ResidualPersonalDataBoundary.IsOutside(path) || IntegratedResidualPolicy.Shared(path, identity, inventory.Items))
            { preserved++; problems.Add(new("prepare", "personal_or_shared_root")); continue; }
            try
            {
                if (Missing(path)) continue;
                using var held = InstallerFileGuard.PinDirectory(path, allowLeafWrites: true);
                roots.Add(new(path, held.Identities()));
            }
            catch (Exception ex) when (Expected(ex))
            { preserved++; problems.Add(new("prepare", "root_unverifiable", Error(ex))); }
        }
        foreach (var address in scope.Keys)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var hive = RegistryKey.OpenBaseKey(address.Hive, address.View);
                using var key = ResidualRegistryGuard.Open(hive, address.Subkey, address.View);
                // A politica conhecida e reconsultada apos o fornecedor: uma
                // chave ausente agora pode ser criada durante a desinstalacao.
                if (key is null)
                { if (scope.Policy != "registered-install") keys.Add(address); continue; }
                var probe = new ResidualUninstallContext(identity, scope.Policy, roots, [], scope.Processes, 0, []);
                if (scope.Policy == "registered-install" && roots.Count == 0 || !IntegratedResidualPolicy.RegistryOwned(probe, key))
                { preserved++; problems.Add(new("prepare", "ownership_not_proven")); continue; }
                keys.Add(address);
                if (!ResidualRegistryGuard.Fingerprint(key, address.View, out _, out _))
                { preserved++; problems.Add(new("prepare", "registry_unverifiable")); continue; }
            }
            catch (Exception ex) when (Expected(ex))
            {
                // Sem leitura/propriedade comprovada, nenhuma autoridade nova.
                problems.Add(new("prepare_" + RegistryStage(address.Hive, address.View), "registry_unreadable", Error(ex)));
            }
        }
        if (scope.Roots.Count + scope.Keys.Count == 0) problems.Add(new("prepare", "coverage_unsupported"));
        return new(identity, scope.Policy, roots.AsReadOnly(), keys.AsReadOnly(), scope.Processes,
            preserved, problems.AsReadOnly());
    }

    internal static IntegratedResidualPlan Capture(ResidualUninstallContext context,
        UninstallSnapshot inventory, CancellationToken token = default)
    {
        var files = new List<ResidualFileEvidence>();
        var folders = new List<ResidualDirectoryEvidence>();
        var keys = new List<ResidualKeyEvidence>();
        var problems = context.Problems.ToList();
        int preserved = context.Preserved, visited = 0;
        long bytes = 0;
        var clock = Stopwatch.StartNew();
        void Budget()
        {
            token.ThrowIfCancellationRequested();
            if (++visited > MaxItems || clock.Elapsed > TimeSpan.FromSeconds(12))
                throw new InvalidDataException("scan_budget");
        }
        IntegratedResidualPlan Block(string reason) => new(context, [], [], [], preserved, false,
            problems.Append(new ResidualProblem("capture", reason)).ToArray());
        if (!ValidContext(context) || !inventory.Complete ||
            UninstallResiduals.StillInstalled(context.App.Entry(), inventory.Items)) return Block("application_present_or_unknown");
        if (!IntegratedResidualPolicy.ProcessesClosed(context)) return Block("application_running");
        if (context.Policy == "registered-install" && IntegratedResidualPolicy.Build(context.App).Roots.Any(r =>
            IntegratedResidualPolicy.Shared(r, context.App, inventory.Items))) return Block("shared_root");
        try
        {
            bool Visit(string path, ResidualRootEvidence root, int depth)
            {
                Budget();
                if (depth > 16) throw new InvalidDataException("scan_depth");
                if (!IntegratedResidualPolicy.Within(root.Path, path) || !ResidualPersonalDataBoundary.IsOutside(path))
                    throw new InvalidDataException("personal_boundary");
                using var lease = InstallerFileGuard.PinDirectory(path, allowLeafWrites: true);
                var chain = lease.Identities();
                if (!chain.Take(root.Chain.Count).SequenceEqual(root.Chain)) throw new InvalidDataException("root_changed");
                bool emptyAfter = true;
                foreach (string child in Directory.EnumerateFileSystemEntries(path))
                {
                    Budget();
                    var attr = File.GetAttributes(child);
                    if ((attr & FileAttributes.ReparsePoint) != 0)
                    { preserved++; emptyAfter = false; problems.Add(new("capture", "link_preserved")); continue; }
                    if ((attr & FileAttributes.Directory) != 0)
                    {
                        if (IntegratedResidualPolicy.PersonalDirectory(child))
                        { preserved++; emptyAfter = false; problems.Add(new("capture", "personal_folder_preserved")); }
                        else emptyAfter &= Visit(child, root, depth + 1);
                        continue;
                    }
                    if (!IntegratedResidualPolicy.EligibleFile(child))
                    { preserved++; emptyAfter = false; problems.Add(new("capture", "personal_or_unknown_file")); continue; }
                    try
                    {
                        using var handle = InstallerFileGuard.OpenResidual(child);
                        var identity = InstallerFileGuard.ReadIdentity(handle);
                        if (identity.Links != 1 || identity.Size > (ulong)MaxBytes || bytes > MaxBytes - (long)identity.Size)
                            throw new InvalidDataException("shared_or_size_limit");
                        bytes += (long)identity.Size;
                        files.Add(new("f" + files.Count, child, (long)identity.Size,
                            Convert.ToHexString(SHA256.HashData(handle)), identity, chain));
                    }
                    catch (Exception ex) when (Expected(ex))
                    { preserved++; emptyAfter = false; problems.Add(new("capture", "file_unverifiable", Error(ex))); }
                }
                if (emptyAfter) folders.Add(new("d" + folders.Count, path, chain));
                return emptyAfter;
            }
            foreach (var root in context.Roots)
            {
                Budget();
                if (IntegratedResidualPolicy.Shared(root.Path, context.App, inventory.Items))
                { preserved++; problems.Add(new("capture", "shared_root")); continue; }
                try { if (!Missing(root.Path)) Visit(root.Path, root, 0); }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is Win32Exception { NativeErrorCode: 5 })
                { preserved++; problems.Add(new("capture", "root_unreadable", Error(ex))); }
            }
            foreach (string path in IntegratedResidualPolicy.Build(context.App).Roots.Except(context.Roots.Select(r => r.Path), IntegratedResidualPolicy.Paths))
            {
                Budget();
                if (!ResidualPersonalDataBoundary.IsOutside(path)) continue;
                try
                {
                    if (!Missing(path)) { preserved++; problems.Add(new("capture", "root_without_preuninstall_identity")); }
                }
                catch (Exception ex) when (Expected(ex))
                { problems.Add(new("capture", "root_unreadable", Error(ex))); }
            }
            foreach (var location in context.Keys)
            {
                Budget();
                try
                {
                    using var hive = RegistryKey.OpenBaseKey(location.Hive, location.View);
                    using var key = ResidualRegistryGuard.Open(hive, location.Subkey, location.View);
                    if (key is null) continue;
                    if (!IntegratedResidualPolicy.RegistryOwned(context, key))
                    { preserved++; problems.Add(new("capture", "ownership_not_proven")); continue; }
                    if (!ResidualRegistryGuard.Fingerprint(key, location.View, out string digest, out int entries))
                    { preserved++; problems.Add(new("capture", "registry_unverifiable")); continue; }
                    keys.Add(new("k" + keys.Count, location.Hive, location.View, location.Subkey, digest, entries));
                }
                catch (Exception ex) when (Expected(ex))
                { problems.Add(new("capture_" + RegistryStage(location.Hive, location.View), "registry_unreadable", Error(ex))); }
            }
            return new(context, files.AsReadOnly(), folders.AsReadOnly(), keys.AsReadOnly(), preserved,
                true, problems.Distinct().ToArray());
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (Expected(ex))
        { return new(context, [], [], [], preserved, false,
            problems.Append(new ResidualProblem("capture", Reason(ex, "scan_incomplete"), Error(ex))).ToArray()); }
    }

    internal static bool ValidContext(ResidualUninstallContext context)
    {
        var scope = IntegratedResidualPolicy.Build(context.App);
        return context.Policy == scope.Policy && context.Roots.Count <= 20 && context.Keys.Count <= 40 &&
            (context.Policy != "registered-install" || context.Keys.Count == 0 || context.Roots.Count > 0) &&
            context.Roots.All(r => scope.Roots.Contains(r.Path, IntegratedResidualPolicy.Paths) &&
                r.Chain.Count is > 0 and <= 40 && r.Chain[^1].Path.Equals(r.Path, StringComparison.OrdinalIgnoreCase)) &&
            context.Keys.All(k => scope.Keys.Contains(k)) &&
            scope.Processes.All(p => context.Processes.Contains(p, IntegratedResidualPolicy.Paths));
    }

    internal static bool ValidPlan(IntegratedResidualPlan plan)
    {
        if (!plan.Complete || !ValidContext(plan.Context) || plan.Eligible > MaxItems || plan.Preserved < 0) return false;
        bool Own(string path) => plan.Context.Roots.Any(r => IntegratedResidualPolicy.Within(r.Path, path));
        bool Digest(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
        var all = plan.Files.Select(f => (f.Id, f.Path)).Concat(plan.Directories.Select(d => (d.Id, d.Path)))
            .Concat(plan.Keys.Select(k => (k.Id, k.Hive + "|" + k.View + "|" + k.Subkey))).ToArray();
        return all.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == all.Length &&
            all.Select(x => x.Item2).Distinct(IntegratedResidualPolicy.Paths).Count() == all.Length &&
            plan.Files.All(f => f.Id.StartsWith('f') && Own(f.Path) && IntegratedResidualPolicy.EligibleFile(f.Path) &&
                Digest(f.Digest) && f.Bytes is >= 0 and <= MaxBytes && f.Identity.Links == 1 &&
                f.Parents.Count is > 0 and <= 40 && f.Parents[^1].Path.Equals(Path.GetDirectoryName(f.Path), StringComparison.OrdinalIgnoreCase)) &&
            plan.Directories.All(d => d.Id.StartsWith('d') && Own(d.Path) &&
                !IntegratedResidualPolicy.PersonalDirectory(d.Path) && d.Chain.Count is > 0 and <= 40 &&
                d.Chain[^1].Path.Equals(d.Path, StringComparison.OrdinalIgnoreCase)) &&
            plan.Keys.All(k => k.Id.StartsWith('k') && Digest(k.Digest) && k.Entries is > 0 and <= 3000 &&
                plan.Context.Keys.Contains(new(k.Hive, k.View, k.Subkey)));
    }

    internal static async Task<IntegratedCleanupResult> DeleteAsync(IntegratedResidualPlan plan, bool consent,
        Func<Task<UninstallSnapshot>> inventory, CancellationToken token = default,
        IReadOnlyCollection<string>? onlyIds = null, Action<string>? audit = null)
    {
        var clock = Stopwatch.StartNew();
        var results = new List<ResidualItemResult>();
        var problems = new List<ResidualProblem>();
        bool cancelled = false;
        var targets = plan.Files.Select(f => (f.Id, Kind: "file"))
            .Concat(plan.Keys.Select(k => (k.Id, Kind: "registry")))
            .Concat(plan.Directories.OrderByDescending(d => d.Path.Length).Select(d => (d.Id, Kind: "directory")))
            .Where(t => onlyIds is null || onlyIds.Contains(t.Id, StringComparer.Ordinal)).ToArray();
        void Set(string id, string kind, ResidualItemState state, string reason, int error = 0)
        {
            results.Add(new(id, kind, state, reason, error));
            var address = kind == "registry" ? plan.Keys.FirstOrDefault(k => k.Id == id) : null;
            string stage = address is null ? kind : RegistryStage(address);
            audit?.Invoke(Audit(plan.Context.App, stage, state.ToString(), error: error, reason: reason));
        }
        if (!consent || !ValidPlan(plan) || onlyIds?.Any(id => !targets.Any(t => t.Id == id)) == true)
        {
            foreach (var t in targets) Set(t.Id, t.Kind, ResidualItemState.Preserved, consent ? "invalid_plan" : "declined");
            return new(plan.Context.App.TechnicalId, results, plan.Preserved, false, false, []);
        }
        // Armar antes da consulta fecha a janela consulta -> inicio do monitor.
        using var monitor = new ResidualRegistrationMonitor();
        UninstallSnapshot? current = null;
        long lastInventory = 0;
        async Task Guard(bool force = false)
        {
            token.ThrowIfCancellationRequested();
            if (force || current is null || monitor.Changed || clock.ElapsedMilliseconds - lastInventory >= 1500)
            {
                monitor.Rearm();
                current = await inventory().ConfigureAwait(false);
                lastInventory = clock.ElapsedMilliseconds;
                token.ThrowIfCancellationRequested();
                if (monitor.Changed) throw new InvalidDataException("inventory_changed");
            }
            if (!current.Complete || UninstallResiduals.StillInstalled(plan.Context.App.Entry(), current.Items) ||
                plan.Context.Roots.Any(r => IntegratedResidualPolicy.Shared(r.Path, plan.Context.App, current.Items)))
                throw new InvalidDataException("application_present_or_unknown");
            if (!IntegratedResidualPolicy.ProcessesClosed(plan.Context, plan.Files))
                throw new InvalidDataException("application_running");
        }
        try
        {
            await Guard(true).ConfigureAwait(false);
            foreach (var target in targets)
            {
                await Guard().ConfigureAwait(false);
                try
                {
#if GF_PROGRESSIVE
                    BeforeTarget?.Invoke(target.Id);
#endif
                    // O teste e o worker atravessam a mesma segunda barreira.
                    await Guard().ConfigureAwait(false);
                    if (target.Kind == "file")
                    {
                        var file = plan.Files.Single(f => f.Id == target.Id);
                        if (Missing(file.Path)) { Set(target.Id, target.Kind, ResidualItemState.Absent, "already_absent"); continue; }
                        if (!ResidualPersonalDataBoundary.IsOutside(file.Path)) throw new InvalidDataException("personal_boundary");
                        using (var parents = InstallerFileGuard.PinDirectory(Path.GetDirectoryName(file.Path)!, allowLeafWrites: true))
                        using (var handle = InstallerFileGuard.OpenResidual(file.Path, deleteAccess: true))
                        {
                            if (!parents.Identities().SequenceEqual(file.Parents) ||
                                InstallerFileGuard.ReadIdentity(handle) != file.Identity ||
                                Convert.ToHexString(SHA256.HashData(handle)) != file.Digest)
                                throw new InvalidDataException("file_changed");
                            await Guard().ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            if (InstallerFileGuard.ReadIdentity(handle) != file.Identity)
                                throw new InvalidDataException("file_changed");
                            InstallerFileGuard.MarkResidualForDeletion(handle);
                        }
                        if (!Missing(file.Path)) throw new IOException("file_remains");
                    }
                    else if (target.Kind == "registry")
                    {
                        var key = plan.Keys.Single(k => k.Id == target.Id);
                        using (var hive = RegistryKey.OpenBaseKey(key.Hive, key.View))
                        using (var held = ResidualRegistryGuard.Open(hive, key.Subkey, key.View))
                        {
                            if (held is null) { Set(target.Id, target.Kind, ResidualItemState.Absent, "already_absent"); continue; }
                            if (!IntegratedResidualPolicy.RegistryOwned(plan.Context, held))
                                throw new InvalidDataException("registry_changed");
                        }
                        ResidualRegistryGuard.Delete(key, token);
                        if (!KeyMissing(key)) throw new IOException("registry_remains");
                    }
                    else
                    {
                        var directory = plan.Directories.Single(d => d.Id == target.Id);
                        if (Missing(directory.Path)) { Set(target.Id, target.Kind, ResidualItemState.Absent, "already_absent"); continue; }
                        if (!ResidualPersonalDataBoundary.IsOutside(directory.Path)) throw new InvalidDataException("personal_boundary");
                        if (Directory.EnumerateFileSystemEntries(directory.Path).Any())
                        {
                            bool needsElevation = results.Any(r => r.State == ResidualItemState.AccessDenied &&
                                (plan.Files.Any(f => f.Id == r.Id && IntegratedResidualPolicy.Within(directory.Path, f.Path)) ||
                                 plan.Directories.Any(d => d.Id == r.Id && IntegratedResidualPolicy.Within(directory.Path, d.Path))));
                            Set(target.Id, target.Kind, needsElevation ? ResidualItemState.AccessDenied : ResidualItemState.Preserved,
                                needsElevation ? "permission_dependency" : "directory_not_empty", needsElevation ? 5 : 0);
                            continue;
                        }
                        InstallerFileGuard.DeleteEmptyResidualDirectory(directory.Path, directory.Chain, token);
                        if (!Missing(directory.Path)) throw new IOException("directory_remains");
                    }
                    Set(target.Id, target.Kind, ResidualItemState.Deleted, "absence_verified");
                }
                catch (Exception ex) when (Expected(ex))
                {
                    int error = Error(ex);
                    bool access = ex is UnauthorizedAccessException or System.Security.SecurityException || error == 5;
                    string failureReason = ex is ResidualFileDeleteException deletion
                        ? deletion.Step + (access ? "_access_denied" :
                            error is 32 or 33 ? "_sharing_violation" : "_windows_error")
                        : access ? "permission_required"
                        : Reason(ex, ex is InvalidDataException ? "target_changed_or_unsafe" : "operation_failed");
                    Set(target.Id, target.Kind, access ? ResidualItemState.AccessDenied :
                        ex is InvalidDataException ? ResidualItemState.Preserved : ResidualItemState.Failed,
                        failureReason, error);
                }
            }
            await Guard(true).ConfigureAwait(false);
            // Conferencia final separada da chamada de exclusao. Exists() nao e
            // prova de ausencia quando o Windows negou acesso.
            for (int i = 0; i < results.Count; i++)
            {
                var item = results[i];
                if (item.State is not (ResidualItemState.Deleted or ResidualItemState.Absent)) continue;
                bool absent;
                try
                {
                    absent = item.Kind == "registry" ? KeyMissing(plan.Keys.Single(k => k.Id == item.Id)) :
                        Missing(item.Kind == "file" ? plan.Files.Single(f => f.Id == item.Id).Path :
                            plan.Directories.Single(d => d.Id == item.Id).Path);
                }
                catch (Exception ex) when (Expected(ex)) { absent = false; problems.Add(new("verify", "absence_unverifiable", Error(ex))); }
                if (!absent)
                {
                    results[i] = item with { State = ResidualItemState.Failed, Reason = "target_reappeared" };
                    audit?.Invoke(Audit(plan.Context.App, "verify", "failed", failed: 1, reason: "target_reappeared"));
                }
            }
        }
        catch (OperationCanceledException) { cancelled = true; problems.Add(new("delete", "cancelled")); }
        catch (Exception ex) when (Expected(ex)) { problems.Add(new("delete", Reason(ex, "installation_or_inventory_changed"), Error(ex))); }
        foreach (var target in targets.Where(t => !results.Any(r => r.Id == t.Id)))
            Set(target.Id, target.Kind, cancelled ? ResidualItemState.Cancelled : ResidualItemState.Preserved,
                cancelled ? "cancelled" : "installation_or_inventory_changed");
        bool verified = !cancelled && problems.Count == 0 && results.All(i => i.State is ResidualItemState.Deleted or ResidualItemState.Absent);
        var outcome = new IntegratedCleanupResult(plan.Context.App.TechnicalId, results.AsReadOnly(),
            plan.Preserved, cancelled, verified, problems.AsReadOnly());
        audit?.Invoke(Audit(plan.Context.App, "verify", verified ? "complete" : "partial", outcome.Files,
            outcome.Directories, outcome.Keys, outcome.Preserved, outcome.Failed,
            milliseconds: clock.ElapsedMilliseconds, cancelled: cancelled));
        foreach (var issue in problems) audit?.Invoke(Audit(plan.Context.App, issue.Stage, "blocked", error: issue.Error, reason: issue.Reason));
        return outcome;
    }

    internal static bool Missing(string path)
    {
        try { _ = File.GetAttributes(path); return false; }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return true; }
    }
    private static bool KeyMissing(ResidualKeyEvidence item)
    {
        using var hive = RegistryKey.OpenBaseKey(item.Hive, item.View);
        using var key = ResidualRegistryGuard.Open(hive, item.Subkey, item.View);
        return key is null;
    }
#if GF_PROGRESSIVE
    internal static Action<string>? BeforeTarget;
#endif
}
