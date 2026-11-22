using System.Globalization;
using System.Text;

namespace GerenciadorAplicativos;

// Usa o pipe já autenticado do trabalhador elevado. O worker só começa
// depois do sinal do pai e coopera com cancelamento ou perda do canal.
internal sealed class ElevatedCleanupProtocol(CancellationToken request, Action<CleanupProgress> progress, Action<string> log)
{
    private const string Start = "START", Cancel = "CANCEL";

    internal TemporaryCleanupResult Result { get; private set; } = TemporaryCleanupResult.WithoutChanges(CleanupState.Inconclusive);

    internal void Declined() => Result = TemporaryCleanupResult.WithoutChanges(CleanupState.Cancelled);

    internal async Task ReadAsync(Stream pipe, StreamReader reader)
    {
        using var finished = new CancellationTokenSource();
        bool cancelledBeforeStart = request.IsCancellationRequested;
        await SendCommandAsync(pipe, cancelledBeforeStart ? Cancel : Start, finished.Token);
        Task relay = cancelledBeforeStart ? Task.CompletedTask : RelayAsync(pipe, finished.Token);
        try
        {
            TemporaryCleanupResult? report = null;
            bool complete = false;
            while (await reader.ReadLineAsync() is { } line)
            {
                if (complete) return;
                if (line.StartsWith("LOG|", StringComparison.Ordinal)) log(line[4..]);
                else if (line.StartsWith("PROGRESS|", StringComparison.Ordinal))
                {
                    if (TryReadProgress(line[9..], out var current)) progress(current);
                }
                else if (line.StartsWith("RESULT|", StringComparison.Ordinal))
                {
                    if (report is not null || (report = ReadResult(line[7..])) is null) return;
                }
                else if (line.StartsWith("DONE|", StringComparison.Ordinal))
                {
                    if (report is null || line[5..] != ExitCode(report).ToString(CultureInfo.InvariantCulture))
                        return;
                    complete = true;
                    finished.Cancel();
                }
                else return;
            }
            // O lado elevado ainda fecha seu escritor após o DONE. Confirmar
            // somente no fim do canal evita fechar o pipe durante o flush dele.
            if (complete && report is not null) Result = report;
        }
        finally
        {
            finished.Cancel();
            await relay;
        }
    }

    private static ValueTask SendCommandAsync(Stream pipe, string command, CancellationToken token) =>
        pipe.WriteAsync(Encoding.ASCII.GetBytes(command + "\n"), token);

    private async Task RelayAsync(Stream pipe, CancellationToken finished)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(request, finished);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, wait.Token); }
        catch (OperationCanceledException) { }
        if (!request.IsCancellationRequested || finished.IsCancellationRequested) return;
        try { await SendCommandAsync(pipe, Cancel, finished); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
    }

    internal static async Task<int> RunWorkerAsync(Stream pipe,
        Func<CancellationToken, Action<CleanupProgress>, Action<string>, TemporaryCleanupResult> clean,
        Action<string, string> send)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        using var initial = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var request = new CancellationTokenSource();
        using var finished = new CancellationTokenSource();
        string? command = await reader.ReadLineAsync(initial.Token);
        if (command is not (Start or Cancel)) return 3;
        if (command == Cancel) request.Cancel();
        Task listen = command == Start ? ListenAsync() : Task.CompletedTask;
        TemporaryCleanupResult result;
        try
        {
            result = await Task.Run(() => clean(request.Token,
                p => send("PROGRESS", FormattableString.Invariant($"{p.Examined}:{p.Removed}")),
                line => send("LOG", line)));
        }
        finally
        {
            finished.Cancel();
            await listen;
        }
        int exit = ExitCode(result);
        send("RESULT", WriteResult(result));
        send("DONE", exit.ToString(CultureInfo.InvariantCulture));
        return exit;

        async Task ListenAsync()
        {
            try
            {
                await reader.ReadLineAsync(finished.Token);
                // CANCEL, fim do pipe ou outra mensagem inesperada preservam os itens restantes.
                if (!finished.IsCancellationRequested) request.Cancel();
            }
            catch (IOException) { if (!finished.IsCancellationRequested) request.Cancel(); }
            catch (OperationCanceledException) when (finished.IsCancellationRequested) { }
        }
    }

    internal static int ExitCode(TemporaryCleanupResult result) => result.State switch
    {
        CleanupState.Complete or CleanupState.Missing => 0,
        CleanupState.Cancelled => 2,
        _ => 1
    };

    internal static string WriteResult(TemporaryCleanupResult result) => FormattableString.Invariant(
        $"{(int)result.State}:{result.FilesRemoved}:{result.DirectoriesRemoved}:{result.Failed}:{result.ReparseSkipped}:{result.Skipped}:{result.BytesRemoved}");

    internal static TemporaryCleanupResult? ReadResult(string message)
    {
        string[] fields = message.Split(':');
        if (fields.Length != 7 || !TryCount(fields[0], out int state) ||
            state > (int)CleanupState.Busy ||
            !TryCount(fields[1], out int files) || !TryCount(fields[2], out int dirs) ||
            !TryCount(fields[3], out int failed) || !TryCount(fields[4], out int links) ||
            !TryCount(fields[5], out int skipped) ||
            !long.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out long bytes) ||
            files == 0 && bytes != 0 ||
            state == (int)CleanupState.Complete && failed != 0 ||
            state == (int)CleanupState.Missing && (files != 0 || dirs != 0 || failed != 0 || links != 0 || skipped != 0))
            return null;
        return new(files, dirs, failed, links, skipped, []) { BytesRemoved = bytes, State = (CleanupState)state };
    }

    private static bool TryReadProgress(string message, out CleanupProgress progress)
    {
        progress = default;
        string[] fields = message.Split(':');
        if (fields.Length != 2 || !TryCount(fields[0], out int examined) ||
            !TryCount(fields[1], out int removed) || removed > examined)
            return false;
        progress = new(examined, removed);
        return true;
    }

    private static bool TryCount(string value, out int count) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count <= 100_001;
}
