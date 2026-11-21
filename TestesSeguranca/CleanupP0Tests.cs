using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using GerenciadorAplicativos;

internal static class CleanupP0Tests
{
    internal static async Task RunAsync()
    {
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("P0 limpeza: " + label);
            Console.WriteLine("PASS P0-LIMPEZA " + label);
        }

        const int fail = unchecked((int)0x8000FFFF);
        var full = new RecycleBinSnapshot(0, 3, 64000);
        var empty = new RecycleBinSnapshot(0, 0, 0);
        var unavailable = new RecycleBinSnapshot(fail, 0, 0);
        int calls = 0;
        int Erase() { calls++; return 0; }
        Check(RecycleBinCleanup.Empty(() => empty, Erase).State == CleanupState.Complete && calls == 0,
            "Lixeira já vazia evita chamar a remoção nativa");
        Check(RecycleBinCleanup.Empty(() => unavailable, Erase).State == CleanupState.Unavailable && calls == 0,
            "Lixeira sem leitura confirmada tem remoção bloqueada");
        var snapshots = new Queue<RecycleBinSnapshot>([full, empty]);
        Check(RecycleBinCleanup.Empty(snapshots.Dequeue, Erase).State == CleanupState.Complete && calls == 1,
            "Lixeira só confirma resultado com leitura posterior");
        snapshots = new([full, new(0, 2, 32000)]);
        var partial = RecycleBinCleanup.Empty(snapshots.Dequeue, () => fail);
        Check(partial.State == CleanupState.Partial && partial.Current.Items == 2 && partial.Error == fail,
            "falha da Lixeira conserva HRESULT e itens restantes");
        snapshots = new([full, unavailable]);
        Check(RecycleBinCleanup.Empty(snapshots.Dequeue, Erase).State == CleanupState.Inconclusive,
            "falha da segunda leitura da Lixeira não vira sucesso");
        Check(Marshal.SizeOf<RecycleBinCleanup.QueryInformation>() == 24 &&
            Marshal.OffsetOf<RecycleBinCleanup.QueryInformation>(nameof(RecycleBinCleanup.QueryInformation.Bytes)).ToInt32() == 8 &&
            Marshal.SizeOf<RecycleBinCleanup.QueryInformation32>() == 20,
            "contagem e bytes da Lixeira usam o layout nativo de 64 bits e x86");

        var report = new TemporaryCleanupResult(2, 1, 1, 1, 3, ["privado"]) { State = CleanupState.Cancelled, BytesRemoved = 65536 };
        var wire = ElevatedCleanupProtocol.WriteResult(report);
        var decoded = ElevatedCleanupProtocol.ReadResult(wire);
        Check(decoded is { FilesRemoved: 2, DirectoriesRemoved: 1, Failed: 1, Skipped: 3,
            ReparseSkipped: 1, BytesRemoved: 65536, State: CleanupState.Cancelled, Examples.Count: 0 },
            "retorno elevado conserva contagens do cancelamento sem enviar os exemplos");
        Check(ElevatedCleanupProtocol.ReadResult("0:0:0:0:0:0:1") is null &&
            ElevatedCleanupProtocol.ReadResult("1:1:0:0:0:-1:1") is null,
            "protocolo rejeita bytes sem arquivos e contagem negativa");

        if (!OperatingSystem.IsWindows()) return;

        async Task<(CleanupState State, string? Signal)> Exchange(string[] lines, bool cancel)
        {
            using var withTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string name = "GF-P0-clean-fixture-" + Guid.NewGuid().ToString("N");
            using var parent = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            using var child = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            Task wait = parent.WaitForConnectionAsync(withTimeout.Token);
            await child.ConnectAsync(withTimeout.Token);
            await wait;
            using var source = new CancellationTokenSource();
            using var reader = new StreamReader(parent, Encoding.UTF8, false, 1024, leaveOpen: true);
            var protocol = new ElevatedCleanupProtocol(source.Token, _ => { }, _ => { });
            Task read = protocol.ReadAsync(parent, reader);
            string? receivedCancellation;
            using (var input = new StreamReader(child, Encoding.UTF8, false, 1024, leaveOpen: true))
            using (var output = new StreamWriter(child, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true })
            {
                if (await input.ReadLineAsync(withTimeout.Token) != "START") throw new IOException("sinal inicial ausente");
                if (cancel) source.Cancel();
                receivedCancellation = cancel ? await input.ReadLineAsync(withTimeout.Token) : null;
                foreach (string line in lines) await output.WriteLineAsync(line);
            }
            child.Dispose();
            await read.WaitAsync(withTimeout.Token);
            return (protocol.Result.State, receivedCancellation);
        }

        var returned = await Exchange(["RESULT|" + wire, "DONE|2"], true);
        Check(returned == (CleanupState.Cancelled, "CANCEL"), "cancelamento percorre o pipe sem perder o resultado final");
        Check((await Exchange(["RESULT|" + wire], false)).State == CleanupState.Inconclusive &&
            (await Exchange(["RESULT|" + wire, "DONE|0"], false)).State == CleanupState.Inconclusive,
            "pipe interrompido ou código contraditório permanece inconclusivo");

        await VerifyWorkerCancellationAsync(Check);
        string root = Path.Combine(Path.GetTempPath(), "GF-P0-clean-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (string name in new[] { "first.tmp", "second.tmp", "third.tmp" })
            {
                string path = Path.Combine(root, name);
                File.WriteAllBytes(path, [1, 2, 3, 4]);
                File.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-2));
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
            }
            Check(SafeTemporaryCleanup.Estimate(root, checkScope: () => CleanupState.Busy).State == CleanupState.Busy,
                "a mesma política impede estimar cache com serviços ativos");
            var expected = SafeTemporaryCleanup.Estimate(root);
            int checks = 0;
            using var cts = new CancellationTokenSource();
            var cancelled = SafeTemporaryCleanup.Clean(root, cancellationToken: cts.Token, checkScope: () =>
            {
                if (++checks == 3) cts.Cancel();
                return CleanupState.Complete;
            });
            Check(expected.EligibleFiles == 3 && cancelled is { State: CleanupState.Cancelled, FilesRemoved: 1, BytesRemoved: 4 } &&
                Directory.EnumerateFiles(root).Count() == 2, "cancelamento após a primeira remoção conserva o resultado já confirmado");
            checks = 0;
            var blocked = SafeTemporaryCleanup.Clean(root,
                checkScope: () => ++checks < 3 ? CleanupState.Complete : CleanupState.Busy);
            Check(blocked is { State: CleanupState.Busy, FilesRemoved: 1, BytesRemoved: 4 } &&
                Directory.EnumerateFiles(root).Count() == 1, "mudança de estado dos serviços impede a próxima remoção");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static async Task VerifyWorkerCancellationAsync(Action<bool, string> check)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var cancellation = new CancellationTokenSource();
        string name = "GF-P0-worker-fixture-" + Guid.NewGuid().ToString("N");
        using var parent = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var worker = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        Task connected = parent.WaitForConnectionAsync(timeout.Token);
        await worker.ConnectAsync(timeout.Token);
        await connected;
        using var reader = new StreamReader(parent, Encoding.UTF8, false, 1024, leaveOpen: true);
        using var writer = new StreamWriter(worker, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        var exchange = new ElevatedCleanupProtocol(cancellation.Token, _ => cancellation.Cancel(), _ => { });
        Task<int> running = ElevatedCleanupProtocol.RunWorkerAsync(worker, (token, progress, _) =>
        {
            progress(new(7, 1));
            if (!token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            return new TemporaryCleanupResult(1, 0, 0, 0, 0, [])
                { State = CleanupState.Cancelled, BytesRemoved = 22 };
        }, (kind, message) => writer.WriteLine(kind + "|" + message));
        await exchange.ReadAsync(parent, reader).WaitAsync(timeout.Token);
        int exit = await running.WaitAsync(timeout.Token);
        check(exit == 2 && exchange.Result is { State: CleanupState.Cancelled, FilesRemoved: 1, BytesRemoved: 22 },
            "worker cooperativo recebe cancelamento durante a operação e confirma os contadores");
    }

}
