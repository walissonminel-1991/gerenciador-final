using GerenciadorAplicativos;
using System.IO.Compression;
using System.Text.Json;

int count = 0;
void Check(bool valid, string label)
{ if (!valid) throw new Exception("FAIL " + label); count++; Console.WriteLine("PASS " + label); }
const string code = "{00000000-0000-0000-0000-000000000016}";
var local = new PackageIdentity("windows", "", "Editor++", "Vendor", "1.0.0", "x64", "user", "stable",
    @"C:\Apps\Editor", code);
var candidate = local with { Provider = "winget", PackageId = "Vendor.Editor", Name = "Official Editor++" };
Check(PackageIdentityMatcher.Match(local, candidate).Confidence == IdentityConfidence.Strong, "ProductCode + publisher + installed version tolerate a different display name");
foreach (var wrong in new[] { candidate with { ProductCode = Guid.NewGuid().ToString("B") },
    candidate with { ProductCode = "invalid" }, candidate with { Architecture = "arm64" },
    candidate with { Scope = "machine" }, candidate with { Channel = "preview" }, candidate with { Publisher = "Vendor Evil" },
    candidate with { InstalledVersion = "2.0.0" }, candidate with { Location = @"C:\Other" } })
    Check(PackageIdentityMatcher.Match(local, wrong).Confidence == IdentityConfidence.Blocked, "Contradictory evidence blocks identity instead of falling back to name");
Check(PackageIdentityMatcher.Match(local with { Signer = "Verified vendor" }, candidate with { Signer = "Other" }).Confidence == IdentityConfidence.Blocked,
    "Conflicting verified signer blocks identity");
var weak = local with { ProductCode = "", Location = "", PackageId = "" };
Check(PackageIdentityMatcher.Match(weak, weak).Confidence == IdentityConfidence.Suggestion, "Name publisher and version alone do not make a strong identity");
Check(PackageIdentityMatcher.Match(weak with { Name = "Editor++" }, weak with { Name = "Editor" }).Confidence == IdentityConfidence.Unmatched,
    "Product punctuation remains significant");
Check(PackageIdentityMatcher.Resolve(local, [candidate, candidate with { PackageId = "Vendor.OtherEdition" }]).Match.Confidence == IdentityConfidence.Ambiguous,
    "Two source identities sharing an MSI code remain ambiguous");
Check(!PackageIdentityMatcher.Match(local with { Publisher = "" }, candidate).Discovered, "MSI GUID alone does not establish an independent identity");
var msix = weak with { PackageFamilyName = "Vendor.Editor_123abc" };
Check(PackageIdentityMatcher.Match(msix, msix with { Name = "Localized title" }).Confidence == IdentityConfidence.Strong,
    "MSIX package family plus publisher and installed version are usable independently of friendly name");
var managed = weak with { Provider = "scoop", PackageId = "editor", Managed = true, Location = @"C:\Scoop\apps\editor\1.0.0" };
Check(PackageIdentityMatcher.Match(managed, managed).Confidence == IdentityConfidence.Strong, "Manager package ID requires local independent context");
Check(PackageIdentityMatcher.Match(managed, managed with { PackageId = "editor-pro" }).Confidence == IdentityConfidence.Blocked, "Manager edition mismatch blocks");
foreach (string id in new[] { "../editor", "editor;Remove-Item", "editor --all", "C:\\file", "", "--help" })
    Check(!PackageIdentityMatcher.ValidId(id), "Package ID cannot carry a path or switches");
Check(PackageIdentityMatcher.LocalPath(@"C:\Apps\..\Windows").Length == 0 && PackageIdentityMatcher.LocalPath(@"\\server\share").Length == 0,
    "Path evidence rejects traversal and UNC");
var external = ApplicationCapabilities.Evaluate(new("managed", InventoryOnly: true, Identified: true, UpdateChecked: true,
    UpdateAvailable: true, ExternalManagement: true, UpdateContract: true, SilentUninstallContract: true, ResidualAuthorized: true));
Check(external.Update.State == ApplicationCapabilityState.AvailableNotAuthorized && external.Uninstall.State == ApplicationCapabilityState.ExternalManagement &&
    external.Residual.State == ApplicationCapabilityState.ReadOnly, "Manager metadata cannot grant install uninstall or residual authority");
var supervised = ApplicationCapabilities.Evaluate(new("interactive", UpdateChecked: true, UpdateAvailable: true, SupervisedUpdateContract: true));
Check(supervised.Update.State == ApplicationCapabilityState.SupervisedUpdate && ApplicationCapabilityMatrix.From([external, supervised]).SilentUpdateEligible == 0,
    "Supervised update has a separate denominator from silent operations");

var good = new FixtureProvider("good", (_, _) => Task.FromResult(new ApplicationProviderResult("good", "ok", [])));
var broken = new FixtureProvider("broken", (_, _) => throw new IOException("PRIVATE PATH MUST NOT BE LOGGED"));
var result = await new ApplicationProviderCoordinator([broken, good]).DiscoverAsync(new(), default);
Check(result.Single(r => r.Provider == "good").Status == "ok" && result.Single(r => r.Provider == "broken").Status == "unavailable", "Provider failure does not interrupt healthy sources");
var cacheProvider = new FixtureProvider("cache", (_, _) => Task.FromResult(new ApplicationProviderResult("cache", "ok", [])));
var coordinator = new ApplicationProviderCoordinator([cacheProvider]);
await coordinator.DiscoverAsync(new(), default);
var cached = await coordinator.DiscoverAsync(new(), default);
await coordinator.DiscoverAsync(new(true), default);
Check(cacheProvider.Calls == 2 && cached.Single().Cached, "Session cache can be explicitly bypassed and never grants authority");
var slow = new FixtureProvider("slow", async (_, token) => { await Task.Delay(10000, token); return new("slow", "ok", []); }, TimeSpan.FromMilliseconds(25));
var timed = await new ApplicationProviderCoordinator([slow, good]).DiscoverAsync(new(), default);
Check(timed.Single(r => r.Provider == "slow").Status == "timeout" && timed.Single(r => r.Provider == "good").Status == "ok", "Timeout is isolated to the slow provider");
using (var stop = new CancellationTokenSource())
{
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var untilCancelled = new FixtureProvider("cancel", async (_, token) => {
        entered.SetResult();
        await Task.Delay(Timeout.Infinite, token);
        return new("cancel", "ok", []);
    }, TimeSpan.FromSeconds(10));
    var reading = new ApplicationProviderCoordinator([untilCancelled]).DiscoverAsync(new(), stop.Token);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
    stop.Cancel();
    bool cancelled = false;
    try { await reading; } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "Caller cancellation is propagated");
}
var forged = new FixtureProvider("declared", (_, _) => Task.FromResult(new ApplicationProviderResult("other", "ok", [])));
Check((await new ApplicationProviderCoordinator([forged]).DiscoverAsync(new(), default)).Single().Status == "unavailable", "Provider provenance cannot be substituted");
Check(typeof(IApplicationDiscoveryProvider).GetMethods().All(m => !m.Name.Contains("Execute") && !m.Name.Contains("Launch")), "Discovery contract exposes no launcher");
Check((await new WinGetComDiscoveryProvider(new FixtureWinGet([new(candidate, "local-com")])).DiscoverAsync(new(), default)).Installations.Count == 1,
    "WinGet production adapter consumes typed data without parsing CLI text");

string root = Path.Combine(Path.GetTempPath(), "GF-W16-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string scoop = Path.Combine(root, "scoop"), current = Path.Combine(scoop, "apps", "editor", "current");
    Directory.CreateDirectory(current);
    File.WriteAllText(Path.Combine(current, "scoop-manifest.json"), "{\"version\":\"1.0.0\",\"pre_install\":\"THROW_IF_EXECUTED\",\"description\":\"Not an identity\"}");
    File.WriteAllText(Path.Combine(current, "scoop-install.json"), "{\"architecture\":\"64bit\",\"bucket\":\"main\"}");
    File.WriteAllText(Path.Combine(current, "manifest.json"), "APP_OWNED_FILE_NOT_SCOOP_METADATA");
    string bucket = Path.Combine(scoop, "buckets", "main");
    Directory.CreateDirectory(Path.Combine(bucket, ".git")); Directory.CreateDirectory(Path.Combine(bucket, "bucket"));
    File.WriteAllText(Path.Combine(bucket, ".git", "config"), "[remote \"origin\"]\nurl = https://github.com/ScoopInstaller/Main.git\n");
    File.WriteAllText(Path.Combine(bucket, "bucket", "editor.json"), "{\"version\":\"2.0.0\",\"post_install\":\"THROW_IF_EXECUTED\"}");
    var scoopProvider = new ScoopDiscoveryProvider(scoop);
    var scoopRows = await scoopProvider.DiscoverAsync(new(), default);
    Check(scoopRows.Status == "ok" && scoopRows.Installations is [{ Identity.PackageId: "editor", Identity.Architecture: "x64", AvailableVersion: "2.0.0" }],
        "Scoop prefixed metadata and a local official bucket work without scripts");
    File.WriteAllText(Path.Combine(bucket, ".git", "config"), "[remote \"origin\"]\nurl = https://evil.example/Main.git\n");
    Check((await scoopProvider.DiscoverAsync(new(), default)).Installations.Single().AvailableVersion == "", "Unreviewed bucket cannot supply a new version");
    File.SetLastWriteTimeUtc(Path.Combine(bucket, "bucket", "editor.json"), DateTime.UtcNow.AddDays(-8));
    File.WriteAllText(Path.Combine(bucket, ".git", "config"), "[remote \"origin\"]\nurl = https://github.com/ScoopInstaller/Main.git\n");
    Check((await scoopProvider.DiscoverAsync(new(), default)).Installations.Single().AvailableVersion == "", "Stale local bucket is not advertised as current");
    File.WriteAllText(Path.Combine(current, "scoop-install.json"), "{");
    Check((await scoopProvider.DiscoverAsync(new(), default)).Status == "partial", "Corrupt manager metadata is partial, never absence confirmation");
    File.Delete(Path.Combine(current, "scoop-manifest.json")); File.Delete(Path.Combine(current, "scoop-install.json"));
    File.WriteAllText(Path.Combine(current, "manifest.json"), "{\"version\":\"1.0.0\"}");
    File.WriteAllText(Path.Combine(current, "install.json"), "{\"architecture\":\"arm64\"}");
    Check((await scoopProvider.DiscoverAsync(new(), default)).Installations.Single().Identity.Architecture == "arm64", "Scoop legacy metadata remains supported");
    string choco = Path.Combine(root, "chocolatey"), lib = Path.Combine(choco, "lib", "editor.install");
    Directory.CreateDirectory(lib);
    string spec = "<package><metadata><id>editor.install</id><version>1.0.0</version><title>Editor</title></metadata></package>";
    File.WriteAllText(Path.Combine(lib, "editor.install.nuspec"), spec);
    using (var zip = ZipFile.Open(Path.Combine(lib, "editor.install.nupkg"), ZipArchiveMode.Create))
    { using var text = new StreamWriter(zip.CreateEntry("editor.install.nuspec").Open()); text.Write(spec); }
    var chocoProvider = new ChocolateyDiscoveryProvider(choco);
    Check((await chocoProvider.DiscoverAsync(new(), default)).Installations is [{ Reason: "chocolatey_package_not_product", Identity.PackageId: "editor.install" }],
        "Chocolatey recognizes package receipts without claiming underlying product presence");
    File.WriteAllText(Path.Combine(lib, "editor.install.nuspec"), spec.Replace("1.0.0", "9.0.0"));
    Check((await chocoProvider.DiscoverAsync(new(), default)).Installations.Count == 0, "Mismatched nuspec and nupkg versions are rejected");
    File.WriteAllText(Path.Combine(lib, "editor.install.nuspec"), "<!DOCTYPE p [<!ENTITY leak SYSTEM 'file:///PRIVATE'>]><package>&leak;</package>");
    Check((await chocoProvider.DiscoverAsync(new(), default)).Status == "partial", "Nuspec DTD and external entity injection are refused");
    Check(!Directory.EnumerateFiles(root, "THROW_IF_EXECUTED", SearchOption.AllDirectories).Any(), "Metadata scripts have no execution path");
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"WORK16_ECOSYSTEM_PASS={count}");

internal sealed class FixtureProvider(string id, Func<ProviderQuery, CancellationToken, Task<ApplicationProviderResult>> read,
    TimeSpan? timeout = null) : IApplicationDiscoveryProvider
{
    internal int Calls;
    public ProviderDescriptor Descriptor => new(id, 1, ProviderCapabilities.DetectInstallations, timeout ?? TimeSpan.FromSeconds(2));
    public Task<ApplicationProviderResult> DiscoverAsync(ProviderQuery query, CancellationToken token) { Calls++; return read(query, token); }
}
internal sealed class FixtureWinGet(IReadOnlyList<ProviderInstallation> rows) : IWinGetInstalledCatalog
{ public Task<IReadOnlyList<ProviderInstallation>> ReadAsync(CancellationToken token) => Task.FromResult(rows); }
