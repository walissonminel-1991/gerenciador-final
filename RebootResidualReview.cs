using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace GerenciadorAplicativos;

internal sealed record RebootReviewTarget(string Kind, string Address, string Hive = "", string View = "");
internal sealed record RebootReviewItem(string AppId, string InventoryId, long BootUtcTicks,
    bool ProviderPending, RebootReviewTarget[] Targets);

// Revisao passiva depois de nova inicializacao. Nao agenda MoveFileEx,
// nao exclui por caminho armazenado e nao confunde UAC/erro 5 com reinicio.
internal static class RebootResidualReview
{
    private const int MaximumTargets = 500;
    private const int MaximumFileBytes = 128 * 1024;
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 12 };
    internal static long BootUtcTicks(DateTimeOffset now, long uptimeMs) =>
        now.UtcTicks - TimeSpan.FromMilliseconds(Math.Clamp(uptimeMs, 0, (long)TimeSpan.FromDays(365).TotalMilliseconds)).Ticks;
    internal static bool IsLaterBoot(long stored, long current) =>
        stored > 0 && Math.Abs(current - stored) > TimeSpan.FromMinutes(2).Ticks;
    internal static bool IsRetryable(ResidualItemResult item) =>
        item.Error is 32 or 33 || item.Reason.Contains("sharing_violation", StringComparison.Ordinal);
    internal static bool ProviderRequiresRestart(UninstallOutcome outcome) =>
        outcome.State == UninstallState.Pending && outcome.Code is 3010 or 1641;

    internal static RebootReviewItem[] Select(IReadOnlyList<UninstallOutcome> outcomes, ResidualBatchResult? batch)
    {
        long boot = BootUtcTicks(DateTimeOffset.UtcNow, Environment.TickCount64);
        var items = new List<RebootReviewItem>();
        foreach (var plan in batch?.RebootPlans ?? [])
        {
            var targets = plan.Files.Select(f => new RebootReviewTarget("file", f.Path))
                .Concat(plan.Directories.Select(d => new RebootReviewTarget("directory", d.Path)))
                .Concat(plan.Keys.Select(k => new RebootReviewTarget("registry", k.Subkey,
                    k.Hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU",
                    k.View == RegistryView.Registry32 ? "32" : "64"))).ToArray();
            if (targets.Length is > 0 and <= MaximumTargets)
                items.Add(new(plan.Context.App.TechnicalId, plan.Context.App.InventoryId,
                    boot, false, targets));
        }
        foreach (var outcome in outcomes.Where(ProviderRequiresRestart))
        {
            var app = ResidualAppIdentity.From(outcome.Request.Original);
            if (!items.Any(i => i.AppId == app.TechnicalId))
                items.Add(new(app.TechnicalId, outcome.Request.Original.InventoryId,
                    boot, true, []));
        }
        return items.Take(16).ToArray();
    }

    private static string Location()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local)) throw new IOException("Perfil local indisponivel.");
        return Path.Combine(local, "GerenciadorFinal", "RevisaoReinicio.dat");
    }

    internal static void Save(IReadOnlyList<RebootReviewItem> pending)
    {
        if (pending.Count == 0) return;
        string path = Location();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var combined = new List<RebootReviewItem>();
        if (File.Exists(path)) combined.AddRange(Read());
        combined.AddRange(pending);
        // Limite estrito: nenhuma lista arbitraria sobrevive entre execucoes.
        combined = combined.DistinctBy(i => i.AppId).Take(16).ToList();
        if (combined.Sum(i => i.Targets.Length) > MaximumTargets)
            throw new InvalidDataException("Limite da revisao de reinicio excedido.");
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(combined, Json);
        if (plain.Length > MaximumFileBytes) throw new InvalidDataException("Revisao extensa demais.");
        byte[] encrypted = Protect(plain, decrypt: false);
        string temp = path + ".novo";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(encrypted);
                file.Flush(true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    internal static RebootReviewItem[] Read()
    {
        string path = Location();
        if (!File.Exists(path)) return [];
        var info = new FileInfo(path);
        if (info.Length is < 1 or > MaximumFileBytes)
            throw new InvalidDataException("Registro pendente invalido.");
        byte[] plain = Protect(File.ReadAllBytes(path), decrypt: true);
        var items = JsonSerializer.Deserialize<RebootReviewItem[]>(plain, Json)
            ?? throw new InvalidDataException("Registro pendente sem dados.");
        if (items.Length > 16 || items.Sum(x => x.Targets.Length) > MaximumTargets ||
            items.Any(x => string.IsNullOrWhiteSpace(x.AppId) || x.BootUtcTicks <= 0 ||
                x.Targets.Any(t => t.Kind is not ("file" or "directory" or "registry") ||
                    t.Address.Length is 0 or > 1024)))
            throw new InvalidDataException("Registro pendente inconsistente.");
        return items;
    }

    internal static async Task CheckAfterBootAsync(Action<string> report)
    {
        var items = Read();
        if (items.Length == 0) return;
        long currentBoot = BootUtcTicks(DateTimeOffset.UtcNow, Environment.TickCount64);
        var ready = items.Where(i => IsLaterBoot(i.BootUtcTicks, currentBoot)).ToArray();
        if (ready.Length == 0) return;
        // Nao ha exclusao automatica depois do boot. Apenas conferencia; os
        // dados preservados nunca se tornam nova autoridade de limpeza.
        var inventory = await Task.Run(AppServices.ScanAllInstalledAppsForSafety);
        foreach (var item in ready)
        {
            int absent = 0, remaining = 0, unknown = 0;
            foreach (var target in item.Targets)
            {
                switch (Probe(target))
                {
                    case 0: absent++; break;
                    case 1: remaining++; break;
                    default: unknown++; break;
                }
            }
            bool installed = inventory.Items.Any(x => x.InventoryId.Equals(item.InventoryId, StringComparison.OrdinalIgnoreCase));
            string status = inventory.Incomplete || unknown > 0 ? "indeterminate" :
                installed ? "application_present" :
                item.ProviderPending ? "provider_pending_review" :
                remaining > 0 ? "partial" : "verified_absent";
            string line = $"REINICIO.RESIDUOS app={item.AppId} estado={status} ausentes={absent} presentes={remaining} incertos={unknown} inventario_parcial={inventory.Incomplete} instalacao_presente={installed} revisao_apos_boot=1";
            if (!DiagnosticTrace.Write(line))
                throw new IOException("Diagnostico de revisao indisponivel.");
            report(status == "verified_absent"
                ? "Após reiniciar: resíduos verificados como ausentes."
                : "Após reiniciar: limpeza não comprovada integralmente. Consulte o diagnóstico.");
        }
        var remainingItems = items.Except(ready).ToArray();
        string path = Location();
        if (remainingItems.Length == 0) File.Delete(path);
        else
        {
            // Dados remanescentes sao de outra inicializacao. Nao sobrescrever
            // com registros em claro nem sem protecao de usuario.
            File.Delete(path);
            Save(remainingItems);
        }
    }

    // 0=ausente, 1=presente, 2=indeterminado. Exists() nao e prova diante de ACL.
    private static int Probe(RebootReviewTarget target)
    {
        try
        {
            if (target.Kind == "registry")
            {
                using var hive = RegistryKey.OpenBaseKey(target.Hive == "HKLM" ?
                    RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                    target.View == "32" ? RegistryView.Registry32 : RegistryView.Registry64);
                using var key = hive.OpenSubKey(target.Address, false);
                return key is null ? 0 : 1;
            }
            _ = InstallerFileGuard.ValidateLocalPath(target.Address);
            var attrs = File.GetAttributes(target.Address);
            return (attrs.HasFlag(FileAttributes.Directory) == (target.Kind == "directory")) ? 1 : 2;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.SecurityException or ArgumentException or InvalidDataException or
            NotSupportedException) { return 2; }
    }

    // DPAPI, escopo usuario atual. Nao gera segredos no repositório e nao
    // imprime os caminhos em diagnosticos ou argumentos de inicializacao.
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    private static byte[] Protect(byte[] data, bool decrypt)
    {
        if (data.Length is 0 or > MaximumFileBytes) throw new InvalidDataException("Dados de revisao invalidos.");
        var input = new Blob { Size = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
        try
        {
            Marshal.Copy(data, 0, input.Data, data.Length);
            bool ok = decrypt
                ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, 1, out var ignored)
                : CryptProtectData(ref input, "Revisao GF", IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, 1, out ignored);
            if (!ok) throw new CryptographicException("O Windows nao validou os dados de revisao.");
            try
            {
                if (ignored.Size is < 1 or > MaximumFileBytes)
                    throw new InvalidDataException("Saida de protecao fora do limite.");
                byte[] bytes = new byte[ignored.Size];
                Marshal.Copy(ignored.Data, bytes, 0, bytes.Length);
                return bytes;
            }
            finally { if (ignored.Data != IntPtr.Zero) LocalFree(ignored.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }
}
