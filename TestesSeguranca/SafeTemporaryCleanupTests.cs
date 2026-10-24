using GerenciadorAplicativos;
using System.ComponentModel;
using System.Runtime.InteropServices;

internal static class SafeTemporaryCleanupTests
{
    private static int checks;
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("Limpeza temporária: " + reason);
        checks++;
    }

    internal static void Run()
    {
        checks = 0;
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            bool stopped = false;
            try { SafeTemporaryCleanup.Clean("não-consultar", cancellationToken: cancelled.Token); }
            catch (OperationCanceledException) { stopped = true; }
            Check(stopped, "cancelamento anterior à consulta não é mascarado como sucesso");
        }
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine($"OK: {checks} teste de cancelamento; SKIP limpeza por handles exige Windows.");
            return;
        }
        string sandbox = Path.Combine(Path.GetTempPath(), "GF-Temporary-Cleanup-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(sandbox, "cache"), outside = Path.Combine(sandbox, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        string Old(string relative)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "fixture temporária");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
            return path;
        }
        try
        {
            string old = Old("old.tmp"), nested = Old(Path.Combine("nested", "old.tmp"));
            string recent = Path.Combine(root, "recent.tmp");
            File.WriteAllText(recent, "recente");
            string readOnly = Old("readonly.tmp");
            File.SetAttributes(readOnly, FileAttributes.ReadOnly);
            string inUse = Old("inuse.tmp");
            string avg = Old(Path.Combine("_avg_preserve", "old.tmp"));
            string sentinel = Path.Combine(outside, "sentinel.tmp");
            File.WriteAllText(sentinel, "preservar conteúdo fora da raiz");
            File.SetLastWriteTimeUtc(sentinel, DateTime.UtcNow.AddDays(-2));
            string hardlink = Path.Combine(root, "hardlink.tmp");
            if (!CreateHardLinkW(hardlink, sentinel, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Fixture hardlink não criada.");

            string redirect = Path.Combine(root, "redirect");
            string fileRedirect = Path.Combine(root, "redirect-file.tmp");
            bool linkedDirectory = TryLink(() => Directory.CreateSymbolicLink(redirect, outside));
            bool linkedFile = TryLink(() => File.CreateSymbolicLink(fileRedirect, sentinel));
            int callbacks = 0;
            bool rootRenameBlocked = false, ancestorRenameBlocked = false;
            TemporaryCleanupResult result;
            using (var writing = new FileStream(inUse, FileMode.Open, FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete))
            {
                result = SafeTemporaryCleanup.Clean(root, _ =>
                {
                    if (callbacks++ != 0) return;
                    try { Directory.Move(root, root + "-moved"); }
                    catch (IOException) { rootRenameBlocked = true; }
                    try { Directory.Move(sandbox, sandbox + "-moved"); }
                    catch (IOException) { ancestorRenameBlocked = true; }
                    // Negar WRITE no handle de diretório não impede outros apps de
                    // criarem filhos normais. O arquivo novo deve continuar preservado.
                    File.WriteAllText(Path.Combine(root, "concurrent.tmp"), "outro aplicativo");
                });
                Check(rootRenameBlocked && ancestorRenameBlocked,
                    "raiz/ancestral não podem ser trocados enquanto a consulta usa seus caminhos");
                Check(!File.Exists(old) && !File.Exists(nested), "arquivos antigos comuns são removidos");
                Check(result.FilesRemoved == 2 && result.DirectoriesRemoved == 1 && result.Failed == 0,
                    "contagem real das fixtures removidas, sem erro de compartilhamento interno");
                Check(File.Exists(recent) && File.Exists(readOnly) && File.Exists(inUse),
                    "recentes, somente leitura e escritor ativo são preservados");
                Check(File.Exists(avg) && Directory.Exists(root), "pasta AVG e própria raiz são preservadas");
                Check(File.Exists(hardlink) && File.ReadAllText(sentinel) == "preservar conteúdo fora da raiz",
                    "hardlink preservado e conteúdo externo intacto");
                Check(File.Exists(Path.Combine(root, "concurrent.tmp")), "criação normal de filhos continua permitida");
                if (linkedDirectory || linkedFile)
                    Check(result.ReparseSkipped == (linkedDirectory ? 1 : 0) + (linkedFile ? 1 : 0),
                        "links de arquivo/diretório são identificados e não percorridos");
            }
            // Todos os handles usados no sucesso devem ter sido liberados.
            Directory.Move(root, root + "-moved");
            Directory.Move(root + "-moved", root);
            Directory.Move(sandbox, sandbox + "-moved");
            Directory.Move(sandbox + "-moved", sandbox);
            Check(Directory.Exists(root), "leases liberadas ao concluir a limpeza");

            string cancelledRoot = Path.Combine(sandbox, "cancelled");
            Directory.CreateDirectory(cancelledRoot);
            string cancelledFile = Path.Combine(cancelledRoot, "old.tmp");
            File.WriteAllText(cancelledFile, "preservar ao cancelar");
            File.SetLastWriteTimeUtc(cancelledFile, DateTime.UtcNow.AddDays(-2));
            using (var duringScan = new CancellationTokenSource())
            {
                bool stopped = false;
                try { SafeTemporaryCleanup.Clean(cancelledRoot, _ => duringScan.Cancel(), duringScan.Token); }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped && File.Exists(cancelledFile), "cancelamento durante consulta não exclui nem vira sucesso");
            }
            Directory.Move(cancelledRoot, cancelledRoot + "-released");
            Check(Directory.Exists(cancelledRoot + "-released"), "cancelamento também libera os handles");

            string alias = Path.Combine(sandbox, "root-alias");
            if (TryLink(() => Directory.CreateSymbolicLink(alias, outside)))
            {
                var denied = SafeTemporaryCleanup.Clean(alias);
                Check(denied.Failed == 1 && denied.FilesRemoved == 0 && File.Exists(sentinel),
                    "raiz redirecionada falha fechada antes de excluir dados");
                // Falha também libera ancestrais.
                Directory.Delete(alias);
            }
            string deepRoot = Path.Combine(sandbox, "deep");
            string deep = deepRoot;
            for (int i = 0; i < 66; i++) deep = Path.Combine(deep, "d");
            Directory.CreateDirectory(deep);
            string deepFile = Path.Combine(deep, "old.tmp");
            File.WriteAllText(deepFile, "fora do orçamento de profundidade");
            File.SetLastWriteTimeUtc(deepFile, DateTime.UtcNow.AddDays(-2));
            var bounded = SafeTemporaryCleanup.Clean(deepRoot);
            Check(bounded.Failed == 1 && bounded.FilesRemoved == 0 && File.Exists(deepFile),
                "limite de profundidade preserva dados e informa resultado parcial");

            foreach (string invalid in new[] { "cache", @"\\server\cache", @"\\?\C:\cache", root + ":stream", Path.GetPathRoot(root)! })
            {
                var denied = SafeTemporaryCleanup.Clean(invalid);
                Check(denied.Failed == 1 && denied.FilesRemoved == 0,
                    "escopo remoto, relativo, ADS, namespace de dispositivo ou unidade inteira recusado");
            }
            Console.WriteLine($"OK: {checks} regressões de limpeza temporária em fixtures, sem tocar em caches reais.");
        }
        finally
        {
            string readOnly = Path.Combine(root, "readonly.tmp");
            if (File.Exists(readOnly)) File.SetAttributes(readOnly, FileAttributes.Normal);
            // As fixtures de links são removidas como links antes da pasta de teste.
            foreach (string link in new[] { Path.Combine(root, "redirect-file.tmp"), Path.Combine(root, "hardlink.tmp") })
                if (File.Exists(link)) File.Delete(link);
            string linkDirectory = Path.Combine(root, "redirect");
            if (Directory.Exists(linkDirectory)) Directory.Delete(linkDirectory);
            if (Directory.Exists(sandbox)) Directory.Delete(sandbox, recursive: true);
        }
    }

    private static bool TryLink(Action create)
    {
        try { create(); return true; }
        catch (UnauthorizedAccessException)
        { Console.WriteLine("SKIP: fixture symlink exige privilégio indisponível; não equivale a teste aprovado."); return false; }
        catch (IOException ex) when (ex.HResult == unchecked((int)0x80070522))
        { Console.WriteLine("SKIP: fixture symlink sem privilégio; não equivale a teste aprovado."); return false; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string link, string existing, IntPtr security);
}
