using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Contratos locais de instalacoes EXE; nao convertem MSI/portatil nem mudam escopo.
// A identidade do Registro e corroborada pelo binario real antes e depois.
internal static class UpdateInstallationContract
{
    internal static async Task<bool> ConfirmAfterAsync(Func<Task<bool>> read, CancellationToken token = default,
        TimeSpan? interval = null)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            token.ThrowIfCancellationRequested();
            if (await read()) { token.ThrowIfCancellationRequested(); return true; }
            if (attempt != 3) await Task.Delay(interval ?? TimeSpan.FromSeconds(2), token);
        }
        return false;
    }

    internal sealed record Layout(string Family, bool X64, string Root, string Binary, string Uninstaller);
#if GF_PROGRESSIVE
    internal static string? SyntheticRoot;
#endif
    internal static Layout? For(string id)
    {
        bool x86 = id.Contains("-x86-", StringComparison.Ordinal);
        string pf = Environment.GetFolderPath(x86 ? Environment.SpecialFolder.ProgramFilesX86 : Environment.SpecialFolder.ProgramFiles);
#if GF_PROGRESSIVE
        pf = SyntheticRoot ?? pf;
#endif
        return id switch
        {
            "obs-x64-32.2.2" => Make("obs", true, Path.Combine(pf, "obs-studio"), @"bin\64bit\obs64.exe", "uninstall.exe"),
            "notepadpp-x64-8.9.8.1" or "notepadpp-x86-8.9.8.1" => Make("notepadpp", !x86, Path.Combine(pf, "Notepad++"), "notepad++.exe", "uninstall.exe"),
            _ => null
        };
    }
    private static Layout Make(string family, bool x64, string root, string binary, string uninstall) =>
        new(family, x64, root, Path.Combine(root, binary), Path.Combine(root, uninstall));

    internal static bool Family(string name, string family) => family switch
    {
        "obs" => name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase),
        "notepadpp" => Regex.IsMatch(name, @"^Notepad\+\+(?: \((?:64-bit x64|32-bit x86|x64|x86)\))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        _ => false
    };

    internal static bool IsSafe(VerifiedUpdateRecipe recipe, ProgramEntry chosen, IEnumerable<ProgramEntry> inventory)
    {
        var layout = For(recipe.Identifier);
        if (layout is null) return false;
        var related = inventory.Where(p => Family(p.Name, layout.Family)).ToArray();
        if (related.Length != 1 || related[0].InventoryId != chosen.InventoryId ||
            chosen.InventoryOnly || string.IsNullOrWhiteSpace(chosen.InventoryId) ||
            Scope(chosen.InventoryId).Length == 0 ||
            !string.IsNullOrWhiteSpace(chosen.MsiProductCode) ||
            (!string.IsNullOrWhiteSpace(chosen.InstallLocation) && !SamePath(layout.Root, chosen.InstallLocation)) ||
            !UninstallCommand.TryParse(chosen.UninstallString, out var uninstall) || uninstall.ProductCode is not null ||
            !SamePath(uninstall.FileName, layout.Uninstaller)) return false;
        return BinaryMatches(layout.Binary, chosen.Version, layout.X64, exact: true);
    }

    internal static bool Confirm(VerifiedUpdateRecipe recipe, ProgramEntry original, IEnumerable<ProgramEntry> inventory)
    {
        var layout = For(recipe.Identifier);
        if (layout is null) return false;
        var snapshot = inventory.ToArray();
        var related = snapshot.Where(p => Family(p.Name, layout.Family)).ToArray();
        if (related.Length != 1) return false;
        var after = related[0];
        return after.Publisher.Equals(original.Publisher, StringComparison.OrdinalIgnoreCase) &&
            after.InventoryId.Equals(original.InventoryId, StringComparison.OrdinalIgnoreCase) &&
            after.InstallLocation.Equals(original.InstallLocation, StringComparison.OrdinalIgnoreCase) &&
            after.UninstallString.Equals(original.UninstallString, StringComparison.OrdinalIgnoreCase) &&
            IsSafe(recipe, after, snapshot) &&
            VerifiedUpdaters.TryVersion(after.Version, out var version) && VerifiedUpdaters.TryVersion(recipe.Version, out var target) &&
            version >= target && BinaryMatches(layout.Binary, recipe.Version, layout.X64, exact: false);
    }

    internal static bool SamePath(string a, string b) =>
        a.TrimEnd('\\', '/').Equals(b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    internal static bool BinaryMatches(string path, string version, bool x64, bool exact)
    {
        try
        {
            using var held = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
            return VerifiedUpdaters.GetPortableExecutableMachine(path) == (x64 ? 0x8664 : 0x14c) &&
                VerifiedUpdaters.TryVersion(FileVersionInfo.GetVersionInfo(path).FileVersion, out var actual) &&
                VerifiedUpdaters.TryVersion(version, out var expected) && (exact ? actual == expected : actual >= expected);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception) { return false; }
    }

    private static bool VcName(string name, bool x64) => Regex.IsMatch(name,
        @"^Microsoft Visual C\+\+ 2013 Redistributable \(" + (x64 ? "x64" : "x86") + @"\)(?: - \d+(?:\.\d+){1,3})?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static string Scope(string id)
    {
        var parts = id.Split('|');
        return parts.Length == 3 && parts[0] == "LocalMachine" && parts[1] is "Registry32" or "Registry64" && parts[2].Length > 0
            ? parts[0] + "|" + parts[1] : "";
    }
    internal static string VcBinary(bool x64)
    {
#if GF_PROGRESSIVE
        if (SyntheticRoot is not null) return Path.Combine(SyntheticRoot, x64 ? "System64" : "System32", "msvcr120.dll");
#endif
        return Path.Combine(Environment.GetFolderPath(x64 ? Environment.SpecialFolder.System : Environment.SpecialFolder.SystemX86), "msvcr120.dll");
    }
    internal static bool VcSafe(VerifiedUpdateRecipe recipe, ProgramEntry original, IEnumerable<ProgramEntry> inventory, bool confirming = false)
    {
        bool x64 = recipe.Identifier == "vc2013-x64";
        if (recipe.Identifier is not ("vc2013-x64" or "vc2013-x86") || !Environment.Is64BitOperatingSystem ||
            !VcName(original.Name, x64) || Scope(original.InventoryId).Length == 0) return false;
        var related = inventory.Where(p => VcName(p.Name, x64)).ToArray();
        if (related.Length != 1) return false;
        var app = related[0];
        if (app.InventoryOnly || Scope(app.InventoryId) != Scope(original.InventoryId) ||
            !app.Publisher.Equals(original.Publisher, StringComparison.OrdinalIgnoreCase) ||
            !VerifiedUpdaters.TryVersion(app.Version, out var version) ||
            !VerifiedUpdaters.TryVersion(recipe.Version, out var target) || version.Major != 12 || version.Minor != 0) return false;
        // O bundle Microsoft pode trocar o GUID ARP. A migracao so e aceita
        // com uma unica familia/arquitetura no mesmo escopo E DLL real confirmada.
        return confirming ? version >= target && BinaryMatches(VcBinary(x64), recipe.Version, x64, exact: false)
            : app.InventoryId == original.InventoryId && app.Version == original.Version && version < target &&
                BinaryMatches(VcBinary(x64), original.Version, x64, exact: true);
    }
}
