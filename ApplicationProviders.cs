using System.Diagnostics;

namespace GerenciadorAplicativos;

[Flags]
internal enum ProviderCapabilities
{
    None = 0, DetectInstallations = 1, QueryVersions = 2, AssociateIdentities = 4,
    IdentifyInstaller = 8, AssessUpdate = 16, PrepareUpdate = 32,
    AssessUninstall = 64, PrepareUninstall = 128, ConfirmResult = 256
}
internal enum ManagementMode { Unavailable, External, Supervised, Silent }
internal sealed record ProviderDescriptor(string Id, int Priority, ProviderCapabilities Capabilities, TimeSpan Timeout);
internal sealed record ProviderInstallation(PackageIdentity Identity, string Receipt, string AvailableVersion = "",
    string Reason = "managed_metadata_only");
internal sealed record ApplicationProviderResult(string Provider, string Status, IReadOnlyList<ProviderInstallation> Installations,
    long Milliseconds = 0, bool Cached = false);
internal sealed record ProviderQuery(bool ForceRefresh = false);

// No process launcher, shell command, authorization token or deletion delegate
// belongs to this interface. Discovery implementations cannot act as executors.
internal interface IApplicationDiscoveryProvider
{
    ProviderDescriptor Descriptor { get; }
    Task<ApplicationProviderResult> DiscoverAsync(ProviderQuery query, CancellationToken token);
}

// Operation contracts describe a plan; they do not carry executable commands.
// Existing VerifiedUpdateRecipe/PreparedUninstaller are the only launch permits.
internal sealed record ProviderOperationAssessment(string Provider, string PackageId, ManagementMode Mode,
    IdentityMatch Identity, string Reason, DateTimeOffset ObservedAt)
{
    internal bool NeedsExecutionValidation => Mode is ManagementMode.Supervised or ManagementMode.Silent;
}

internal sealed class ApplicationProviderCoordinator(IEnumerable<IApplicationDiscoveryProvider> providers)
{
    private readonly IApplicationDiscoveryProvider[] sources = providers.OrderBy(p => p.Descriptor.Priority).ToArray();
    private readonly Dictionary<string, (long At, ApplicationProviderResult Result)> cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim gate = new(1, 1);
    internal async Task<IReadOnlyList<ApplicationProviderResult>> DiscoverAsync(ProviderQuery query, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { return await Task.WhenAll(sources.Select(p => QueryAsync(p, query, token))); }
        finally { gate.Release(); }
    }
    private async Task<ApplicationProviderResult> QueryAsync(IApplicationDiscoveryProvider source, ProviderQuery query, CancellationToken token)
    {
        string id = source.Descriptor.Id;
        // Cache never outlives the coordinator/session; force bypasses it. No
        // cache is used to authorize operations or confirm removal.
        lock (cache)
            if (!query.ForceRefresh && cache.TryGetValue(id, out var old) &&
                Stopwatch.GetElapsedTime(old.At) < TimeSpan.FromMinutes(2)) return old.Result with { Cached = true };
        var timer = Stopwatch.StartNew();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        stop.CancelAfter(source.Descriptor.Timeout);
        try
        {
            // Task.Run also isolates synchronous IO/COM startup from the UI.
            var task = Task.Run(() => source.DiscoverAsync(query, stop.Token), CancellationToken.None);
            _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var result = await task.WaitAsync(source.Descriptor.Timeout + TimeSpan.FromSeconds(1), token);
            stop.Token.ThrowIfCancellationRequested();
            if (result.Provider != id || result.Installations.Count > 2000 || result.Installations.Any(i =>
                i.Identity.Provider != id || i.Identity.Name.Length > 512 || i.Identity.PackageId.Length > 256 ||
                i.Identity.InstalledVersion.Length > 128 || i.Receipt.Length > 4096))
                throw new InvalidDataException("Provider result outside contract.");
            result = result with { Milliseconds = timer.ElapsedMilliseconds };
            if (result.Status == "ok") lock (cache) cache[id] = (Stopwatch.GetTimestamp(), result);
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return new(id, "timeout", [], timer.ElapsedMilliseconds); }
        catch (TimeoutException) { stop.Cancel(); return new(id, "timeout", [], timer.ElapsedMilliseconds); }
        catch (Exception) { return new(id, "unavailable", [], timer.ElapsedMilliseconds); }
    }
}
