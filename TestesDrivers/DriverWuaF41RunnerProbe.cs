using GerenciadorAplicativos;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// O modo do Actions termina aqui. O executor interativo F4 conserva seus gates.
internal static class DriverWuaF41RunnerProbe
{
    private const string Repository = "walissonminel-1991/gerenciador-final";
    private const string BranchRef = "refs/heads/experimento-atualizador-drivers-wua-20261114";
    private sealed record MatchEvidence(Guid UpdateId, int Revision, string DeviceToken,
        string? PilotClass, string Status, DriverUpdatePilotCheck? NativeCheck);

    internal static bool IsHostedDispatch(bool windows, Func<string, string?> env) =>
        windows && env("GITHUB_ACTIONS") == "true" &&
        env("RUNNER_ENVIRONMENT") == "github-hosted" && env("RUNNER_OS") == "Windows" &&
        env("GITHUB_REPOSITORY") == Repository && env("GITHUB_REF") == BranchRef &&
        env("GITHUB_EVENT_NAME") == "workflow_dispatch" &&
        env("GF_F41_IMAGE") is "windows-2022" or "windows-2025";

    internal static string DeviceToken(byte[] key, string deviceId) =>
        Convert.ToHexString(HMACSHA256.HashData(key,
            Encoding.UTF8.GetBytes(deviceId.Trim().ToUpperInvariant())));

    internal static string Verdict(bool complete, int offers, int eligible) =>
        !complete ? "QUERY_FAILED" :
        offers == 0 ? "NO_OFFERS" :
        eligible == 0 ? "NO_ELIGIBLE_OFFERS" : "ELIGIBLE_OFFER";

    private static string? HResult(string status)
    {
        // O texto de diagnóstico fica local. Só encaminhar o código hexadecimal.
        var match = Regex.Match(status, @"\b0x[0-9A-Fa-f]{8}\b");
        return match.Success ? match.Value : null;
    }

    internal static async Task<int> Run()
    {
        if (!IsHostedDispatch(OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable))
        {
            Console.Error.WriteLine("F4.1: ambiente de consulta não reconhecido.");
            return 4;
        }

        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        var token = budget.Token;
        var key = RandomNumberGenerator.GetBytes(32);
        var matches = new List<MatchEvidence>();
        DriverInventoryResult? inventory = null;
        DriverOfficialResult? official = null;
        var stage = "PNP";
        string? failure = null;
        string? hresult = null;

        try
        {
            inventory = await Task.Run(() => DriverInventory.Capture(token), token).WaitAsync(token);
            if (inventory.Partial || inventory.Items.Count == 0)
            {
                failure = "PNP_INCOMPLETE";
                hresult = HResult(inventory.Status);
            }
            else
            {
                stage = "WUA_SEARCH";
                official = await DriverOfficialLookup.Start(inventory, token).WaitAsync(token);
                if (official.Partial ||
                    official.SearchContext is not { Source: "Windows Update", Online: true })
                {
                    failure = "WUA_INCOMPLETE";
                    hresult = HResult(official.Status);
                }
                else
                {
                    stage = "WUA_F4_REVALIDATION";
                    var backend = new DriverWuaNativeBackend();
                    foreach (var match in official.Matches)
                    {
                        DriverUpdatePilotCheck? native = null;
                        var offer = match.Offer;
                        string? pilotClass = DriverUpdatePilot.AllowedClass(offer.DriverClass)
                            ? offer.DriverClass.ToUpperInvariant() : null;
                        var status = "REJECTED_PILOT_GATE";
                        if (DriverUpdatePilot.TryPrepare(inventory, official, match, out var choice) &&
                            choice is not null && offer.EntryAudit is { Entries.Count: 1 } audit &&
                            DriverWuaF4Gates.EntryMatches(choice, audit.Entries[0].HardwareOrCompatibleId,
                                audit.Entries[0].Provider, audit.Entries[0].DriverClass))
                        {
                            // ReadNative só consulta. Nenhuma aprovação, journal, download ou instalação.
                            native = await backend.Revalidate(choice, token).WaitAsync(token);
                            status = !native.OfficialSource ? "REVALIDATION_INCONCLUSIVE" :
                                native.BeforeDownload ? "ELIGIBLE" : "REJECTED_NATIVE_GATE";
                        }

                        matches.Add(new(Guid.ParseExact(offer.UpdateId, "D"), offer.Revision,
                            DeviceToken(key, match.DeviceId), pilotClass, status, native));
                    }

                    if (matches.Any(match => match.Status == "REVALIDATION_INCONCLUSIVE"))
                        failure = "WUA_REVALIDATION_INCOMPLETE";
                    else
                        stage = "COMPLETE";
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            failure = "TIMEOUT";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            failure = "QUERY_EXCEPTION";
            hresult = $"0x{ex.HResult:X8}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        int eligible = matches.Count(match => match.Status == "ELIGIBLE");
        var verdict = Verdict(failure is null, official?.Offered ?? 0, eligible);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Schema = 1,
            TimeUtc = DateTimeOffset.UtcNow,
            OsVersion = Environment.OSVersion.Version.ToString(),
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            Stage = stage,
            Verdict = verdict,
            FailureCode = failure,
            HResult = hresult,
            PnpEntities = inventory?.PnpEntities,
            SignedRows = inventory?.SignedRows,
            InventoryItems = inventory?.Items.Count,
            PresentItems = inventory?.Items.Count(item => item.Present == true),
            SearchSource = official?.SearchContext?.Source,
            SearchOnline = official?.SearchContext?.Online,
            WuaOffered = official is { Partial: false } ? (int?)official.Offered : null,
            WuaUnmatched = official is { Partial: false } ? (int?)official.Unmatched : null,
            WuaMatches = official is { Partial: false } ? (int?)official.Matches.Count : null,
            Eligible = eligible,
            Matches = matches
        }));
        return verdict == "QUERY_FAILED" ? 5 : 0;
    }
}
