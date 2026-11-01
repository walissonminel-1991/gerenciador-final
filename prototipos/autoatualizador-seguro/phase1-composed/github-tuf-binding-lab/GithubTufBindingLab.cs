// Windows CI-only proof of the EXISTING GitHub parser/downloader source.
// No network sockets, production installation, update worker, TUF signing, or L18.
// The Python-TUF verifier subsequently authenticates the downloaded file set.
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;

namespace GerenciadorAplicativos;

// Stubs make unsafe product execution impossible in this standalone project.
// In particular PrepareOfficialReleaseAsync can NEVER start an updater.
internal static class GfPrivateFunctionalUpdate
{
    internal static bool LabEnabled => false;
    internal static Task PrepareOfficialReleaseAsync(
        GfGithubWeeklyUpdate.Release release,
        Func<GfGithubWeeklyUpdate.Release, string, Task<string>> acquire)
        => throw new InvalidOperationException("GF_GITHUB_TUF_NO_INSTALL_AUTHORITY");
}

internal static class Program
{
    private const string Repo = "walissonminel-1991/gerenciador-final";

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("GF_TUF_BINDING_" + reason);
    }

    private static string Scoped(string path, string runner)
    {
        string candidate = Path.GetFullPath(path);
        Require(candidate.StartsWith(runner + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "RUNNER_SCOPE");
        return candidate;
    }

    private static async Task ExecuteAsync(string archive, string destination)
    {
        string checksum;
        using (var archiveStream = File.OpenRead(archive))
            checksum = Convert.ToHexString(SHA256.HashData(archiveStream)).ToLowerInvariant();

        // Instantiate the EXISTING, private, socketless GitHub protocol fixture.
        // Reflect only to invoke the exact existing parser and downloader; do
        // not copy those methods into a second code path.
        Type owner = typeof(GfGithubWeeklyUpdate);
        Type handlerType = owner.GetNestedType("CiGithubProtocolHandler",
            BindingFlags.NonPublic) ?? throw new InvalidDataException("HANDLER_ABSENT");
        var handler = Activator.CreateInstance(handlerType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: new object[] { archive, checksum }, culture: null)
            as HttpMessageHandler;
        Require(handler is not null, "HANDLER_NOT_CREATED");
        using var http = new HttpClient(handler!, disposeHandler: true)
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
        MethodInfo find = owner.GetMethod("FindReleaseAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidDataException("PARSER_ABSENT");
        MethodInfo download = owner.GetMethod("DownloadAssetWithClientAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidDataException("DOWNLOADER_ABSENT");
        var parsed = find.Invoke(null, new object[] { http }) as
            Task<GfGithubWeeklyUpdate.Release>;
        Require(parsed is not null, "PARSER_RETURN_INVALID");
        GfGithubWeeklyUpdate.Release? candidate = await parsed!;
        Require(candidate is not null &&
            candidate.Version == new Version(5, 13, 11, 66) &&
            candidate.AssetLength == new FileInfo(archive).Length &&
            string.Equals(candidate.Sha256, checksum, StringComparison.OrdinalIgnoreCase),
            "GITHUB_METADATA_NOT_BOUND");

        // Negative: same bytes, wrong declared digest, must be rejected before
        // any TUF handoff. This writes only a disposable file in RUNNER_TEMP.
        var bad = candidate! with { Sha256 = new string('0', 64) };
        string rejectedPath = destination + ".wrong-digest";
        bool rejected = false;
        try
        {
            var wrong = download.Invoke(null,
                new object[] { bad, rejectedPath, http }) as Task<string>;
            Require(wrong is not null, "NEGATIVE_TASK_MISSING");
            await wrong!;
        }
        catch (InvalidDataException e) when (
            e.Message.Contains("RELEASE_ASSET_DIGEST_MISMATCH",
                StringComparison.Ordinal))
        {
            rejected = true;
        }
        Require(rejected, "WRONG_DIGEST_NOT_REJECTED");
        if (File.Exists(rejectedPath)) File.Delete(rejectedPath);

        var acquired = download.Invoke(null,
            new object[] { candidate!, destination, http }) as Task<string>;
        Require(acquired is not null, "DOWNLOAD_TASK_MISSING");
        string returned = await acquired!;
        Require(returned == destination && File.Exists(destination),
            "DOWNLOADED_PATH_MISMATCH");
        string actual;
        using (var stream = File.OpenRead(destination))
            actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        Require(actual == checksum &&
                new FileInfo(destination).Length == candidate!.AssetLength,
                "DOWNLOADED_BYTES_DIFFER");

        int ReadCounter(string field) =>
            (int)(handlerType.GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(handler) ?? -1);
        Require(ReadCounter("MetadataRequests") == 1 &&
                ReadCounter("AssetRequests") == 2 &&
                ReadCounter("RedirectedDownloads") == 2, "NOT_SAME_GITHUB_PROTOCOL");
        Console.WriteLine("GF_GITHUB_TUF_DOWNLOADER_PASS " +
            "parser=EXISTING download=EXISTING redirect=302 " +
            "wrong_digest=REJECTED zip_bytes_identical=true " +
            "network=false handoff=false worker=false READY=false");
    }

    private static async Task<int> Main(string[] args)
    {
        try
        {
            Require(OperatingSystem.IsWindows() &&
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" &&
                Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") == Repo &&
                Environment.GetEnvironmentVariable("GF_GITHUB_TUF_ONLY") == "1",
                "DISPOSABLE_CI_ONLY");
            Require(args.Length == 2, "ARGS");
            string runner = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                Environment.GetEnvironmentVariable("RUNNER_TEMP") ??
                    throw new InvalidDataException("RUNNER_ABSENT")));
            Require(Directory.Exists(runner), "RUNNER_ABSENT");
            string archive = Scoped(args[0], runner);
            string destination = Scoped(args[1], runner);
            Require(archive != destination && File.Exists(archive) &&
                !File.Exists(destination) && !Directory.Exists(destination) &&
                new FileInfo(archive).Length is > 10000 and <= 250_000_000,
                "SOURCE_OR_DESTINATION_INVALID");
            await ExecuteAsync(archive, destination);
            return 0;
        }
        catch (Exception e)
        {
            // No user paths, environment secrets, signed CDN URLs or raw JSON.
            Console.Error.WriteLine("GF_GITHUB_TUF_BINDING_BLOCKED " +
                e.GetType().Name + " READY=false");
            return 17;
        }
    }
}
