using System.Diagnostics;
using System.Reflection;
using GerenciadorAplicativos;

internal static class Work7SafetyTests
{
    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "GF-W7-Safety-" + Guid.NewGuid().ToString("N"));
        string profile = Path.Combine(root, "foobar2000-v2");
        Directory.CreateDirectory(profile);
        try
        {
            string file = Path.Combine(profile, "prefs.ini");
            File.WriteAllText(file, "abc");
            var authorized = new FoobarProfilePlan([profile], 1, 3, "");
            File.WriteAllText(file, "xyz"); // mesmo caminho, quantidade e tamanho; outro conteudo
            var changed = new FoobarProfilePlan([profile], 1, 3, "");
            if (!FoobarProfileCleanup.Same(authorized, changed) || authorized.CanExecute)
                throw new Exception("W7-R4 resumo igual nao deve autorizar exclusao.");
            var result = FoobarProfileCleanupFlow.DeleteConfirmedAsync(authorized, default).GetAwaiter().GetResult();
            if (result.FoldersDeleted != 0 || result.RegistryKeysDeleted != 0 || File.ReadAllText(file) != "xyz")
                throw new Exception("W7-R4 chamada direta apagou dados sem identidade fixada.");
            using var owner = new Form();
            foreach (string product in new[] { "foobar2000", "OBS Studio", "VLC media player" })
            {
                string message = ScopedAppProfileCleanup.ConfirmAndCleanAsync(owner, product, default).GetAwaiter().GetResult();
                if (!message.Contains("suspensa") || File.ReadAllText(file) != "xyz")
                    throw new Exception("W7-R4 entrada de interface nao preservou os dados.");
            }
            if (typeof(FoobarProfileCleanup).GetMethod("DeleteSyntheticFolderForTests",
                BindingFlags.Static | BindingFlags.NonPublic) is not null)
                throw new Exception("W7-R4 executor de fixture apareceu no assembly distribuido.");
            Console.WriteLine("OK W7-R4: conteúdo trocado preservado, três entradas sem exclusão, executor sintético ausente da aplicação");
        }
        finally { Directory.Delete(root, true); }

        using var process = Process.GetCurrentProcess();
        if (!SelectiveMemoryTrim.TryReadHandleIdentity(process.Handle, out int pid, out DateTime started, out string path))
            throw new Exception("W7-RAM identidade do handle nao foi lida.");
        var target = new MemoryTrimTarget(process.Id, process.StartTime.ToUniversalTime(), process.ProcessName,
            process.MainModule!.FileName, process.WorkingSet64);
        if (!SelectiveMemoryTrim.SameAuthorizedIdentity(target, pid, started, path) ||
            SelectiveMemoryTrim.SameAuthorizedIdentity(target, pid + 1, started, path) ||
            SelectiveMemoryTrim.SameAuthorizedIdentity(target, pid, started.AddSeconds(1), path) ||
            SelectiveMemoryTrim.SameAuthorizedIdentity(target, pid, started, path + ".changed"))
            throw new Exception("W7-RAM PID reciclado ou identidade trocada herdou autorizacao.");
        Console.WriteLine("OK W7-RAM: handle nativo, PID, início e executável conferidos; nenhum working set alterado no teste");
    }
}
