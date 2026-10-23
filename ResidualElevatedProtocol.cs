using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Extensao do WorkerHost e do mesmo RunElevatedAsync usado pela manutencao.
// Plano somente em memoria, tamanho limitado e sem caminhos/comandos na CLI.
internal static class ResidualElevatedProtocol
{
    internal const string TaskName = "uninstall-residual-clean";
    private const int MaximumFrame = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    { MaxDepth = 40, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    internal static async Task WriteFrameAsync<T>(Stream pipe, T value, CancellationToken token)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length is < 1 or > MaximumFrame) throw new InvalidDataException("protocol_size");
        byte[] header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await pipe.WriteAsync(header, token).ConfigureAwait(false);
        await pipe.WriteAsync(bytes, token).ConfigureAwait(false);
        await pipe.FlushAsync(token).ConfigureAwait(false);
    }
    internal static async Task<T> ReadFrameAsync<T>(Stream pipe, CancellationToken token)
    {
        byte[] header = new byte[4]; await pipe.ReadExactlyAsync(header, token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > MaximumFrame) throw new InvalidDataException("protocol_size");
        byte[] bytes = new byte[length]; await pipe.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException("protocol_empty");
    }

    internal static bool ValidRequests(IReadOnlyList<ResidualElevationRequest> requests)
    {
        try
        {
            return requests.Count is > 0 and <= 100 && requests.Sum(r => r.Plan.Eligible) <= IntegratedResidualCleanup.MaxItems &&
                requests.Select(r => r.Plan.Context.App.TechnicalId).Distinct(StringComparer.Ordinal).Count() == requests.Count &&
                requests.All(r => IntegratedResidualCleanup.ValidPlan(r.Plan) && r.TargetIds.Count > 0 &&
                    r.TargetIds.Distinct(StringComparer.Ordinal).Count() == r.TargetIds.Count &&
                    r.TargetIds.All(id => r.Plan.Files.Any(f => f.Id == id) ||
                        r.Plan.Directories.Any(d => d.Id == id) || r.Plan.Keys.Any(k => k.Id == id)));
        }
        catch (Exception ex) when (ex is NullReferenceException || IntegratedResidualCleanup.Expected(ex)) { return false; }
    }

    internal static bool ValidResponse(ResidualElevationResponse response, IReadOnlyList<ResidualElevationRequest> requests)
    {
        try
        {
            if (response.State is "uac_peer_rejected" or "uac_account_changed" or "uac_not_elevated" or "uac_invalid_request")
                return response.Results.Count == 0;
            return response.State == "uac_authorized" && response.Error == 0 && response.Results.Count == requests.Count &&
                response.Results.Select(r => r.AppId).Distinct().Count() == requests.Count &&
                requests.All(request => response.Results.SingleOrDefault(r => r.AppId == request.Plan.Context.App.TechnicalId) is { } result &&
                    result.Items.Count == request.TargetIds.Count && result.Items.Select(i => i.Id).Distinct().Count() == result.Items.Count &&
                    result.Items.All(i => request.TargetIds.Contains(i.Id, StringComparer.Ordinal) && Enum.IsDefined(i.State) &&
                        i.Kind == (request.Plan.Files.Any(f => f.Id == i.Id) ? "file" :
                            request.Plan.Keys.Any(k => k.Id == i.Id) ? "registry" : "directory") && SafeCode(i.Reason)) &&
                    result.Problems.All(p => SafeCode(p.Stage) && SafeCode(p.Reason)));
        }
        catch (Exception ex) when (ex is NullReferenceException || IntegratedResidualCleanup.Expected(ex)) { return false; }
    }
    private static bool SafeCode(string value) => value is not null &&
        Regex.IsMatch(value, "^[a-z_]{1,80}$", RegexOptions.CultureInvariant);

    internal static async Task<ResidualElevationResponse> ParentAsync(NamedPipeServerStream pipe,
        IReadOnlyList<ResidualElevationRequest> requests, CancellationToken token)
    {
        if (!ValidRequests(requests)) return new("uac_invalid_request", 0, []);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await WriteFrameAsync(pipe, requests.ToArray(), deadline.Token).ConfigureAwait(false);
        using var cancelled = token.Register(() => { try { pipe.WriteByte(1); } catch (IOException) { } catch (ObjectDisposedException) { } });
        var response = await ReadFrameAsync<ResidualElevationResponse>(pipe, deadline.Token).ConfigureAwait(false);
        return ValidResponse(response, requests) ? response : new("uac_invalid_response", 0, []);
    }

    internal static async Task<int> ServeAsync(NamedPipeClientStream pipe,
        Func<Task<UninstallSnapshot>> inventory)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var requests = await ReadFrameAsync<ResidualElevationRequest[]>(pipe, deadline.Token).ConfigureAwait(false);
        string? refusal = !PipePeerVerifier.IsSameApplicationServer(pipe, out string peerReason) ? peerReason :
            !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) ? "uac_not_elevated" :
            !ValidRequests(requests) ? "uac_invalid_request" : null;
        if (refusal is not null)
        {
            await WriteFrameAsync(pipe, new ResidualElevationResponse(refusal, 5, []), deadline.Token).ConfigureAwait(false);
            return 4;
        }
        var results = new List<IntegratedCleanupResult>();
        var cancellationWatch = WatchCancellationAsync(pipe, deadline);
        try
        {
            foreach (var request in requests)
                results.Add(await IntegratedResidualCleanup.DeleteAsync(request.Plan, true, inventory,
                    deadline.Token, request.TargetIds).ConfigureAwait(false));
            // Enviar tambem resultado parcial apos cancelamento, sem abortar o frame.
            using var sendTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WriteFrameAsync(pipe, new ResidualElevationResponse("uac_authorized", 0, results), sendTimeout.Token).ConfigureAwait(false);
            return 0;
        }
        finally { deadline.Cancel(); await cancellationWatch.ConfigureAwait(false); }
    }
    private static async Task WatchCancellationAsync(Stream pipe, CancellationTokenSource cancellation)
    {
        try
        {
            byte[] signal = new byte[1];
            _ = await pipe.ReadAsync(signal, cancellation.Token).ConfigureAwait(false);
            cancellation.Cancel(); // comando de cancelar ou desconexao do processo principal
        }
        catch (OperationCanceledException) { }
        catch (IOException) { cancellation.Cancel(); }
    }
}
