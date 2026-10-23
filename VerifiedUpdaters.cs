using System.ComponentModel;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GerenciadorAplicativos;

// Receitas revisadas e estaticas: NUNCA executar URL, argumentos ou scripts vindos do catalogo.
internal sealed record VerifiedUpdateRecipe(
    string Identifier, string ProductName, string Version, Uri Download,
    string? Sha256, string RequiredSigner, string Arguments, bool Interactive,
    string OfficialPage);

internal static class VerifiedUpdaters
{
    private static readonly InstallerFileGuard.SessionProtection SessionLocks = new();
    private static readonly ConcurrentDictionary<string, byte> ProtectedFolders =
        new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsFolderProtected(string folder) =>
        ProtectedFolders.ContainsKey(Path.GetFullPath(folder));

    internal static void ValidateInstallerDirectory(VerifiedUpdateRecipe recipe, string file)
    {
        string folder = Path.GetDirectoryName(file)!;
        string[] allowed = recipe.Identifier == "gameinput-x64-3.5.283"
            ? new[] { recipe.Identifier + ".nupkg", recipe.Identifier + ".msi" }
            : new[] { recipe.Identifier + ".exe" };
        foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
            if (!allowed.Contains(Path.GetFileName(entry), StringComparer.OrdinalIgnoreCase) ||
                (File.GetAttributes(entry) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidDataException("Pasta do instalador contem arquivos inesperados. Baixe novamente.");
    }

    private sealed record DownloadReceipt(VerifiedUpdateRecipe Recipe, string Digest);
    private static readonly ConcurrentDictionary<string, DownloadReceipt> Receipts =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> ReceiptOrder = new();

    private static void RememberDownload(string path, VerifiedUpdateRecipe recipe, string digest)
    {
        string full = Path.GetFullPath(path);
        Receipts[full] = new(recipe, digest);
        ReceiptOrder.Enqueue(full);
        while (ReceiptOrder.Count > 32 && ReceiptOrder.TryDequeue(out string? oldest))
            Receipts.TryRemove(oldest, out _);
    }

    // A receita completa precisa ser uma das constantes locais, nao apenas ter ID conhecido.
    internal static bool IsApprovedRecipe(VerifiedUpdateRecipe recipe)
    {
        VerifiedUpdateRecipe? approved = recipe.Identifier switch
        {
            "vc2013-x64" or "vc2013-x86" => new(recipe.Identifier, "Microsoft Visual C++ 2013",
                VcTarget.ToString(), recipe.Identifier == "vc2013-x64" ? Vc64 : Vc86,
                null, "Microsoft Corporation", "/install /quiet /norestart", false,
                "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/"),
            "obs-x64-32.2.2" => new(recipe.Identifier, "OBS Studio", "32.2.2",
                Obs, ObsDigest, "OBS Project, LLC", "", true, "https://obsproject.com/download"),
            "notepadpp-x64-8.9.8.1" => new(recipe.Identifier, "Notepad++", "8.9.8.1",
                Npp64, NppDigest, "Notepad++", "/S", false, "https://notepad-plus-plus.org/downloads/"),
            "sevenzip-exe-x64-26.05" => CreateSevenZipRecipe(),
            "sumatrapdf-x64-3.6.1" or "sumatrapdf-x86-3.6.1" =>
                CreateSumatraRecipe(new ProgramEntry(), recipe.Identifier == "sumatrapdf-x64-3.6.1",
                    recipe.Arguments == "-install -silent -all-users"),
            _ => null
        };
        return approved is not null && recipe == approved &&
            // Nenhum EXE/MSI e executado sem SHA-256 fixado na receita revisada.
            approved.Sha256 is { Length: 64 } &&
            approved.Sha256.All(Uri.IsHexDigit);
    }

    internal static async Task<FileStream> OpenVerifiedForLaunchAsync(
        VerifiedUpdateRecipe recipe, string path)
    {
        if (!IsApprovedRecipe(recipe) || !Path.IsPathFullyQualified(path) ||
            !Receipts.TryRemove(Path.GetFullPath(path), out var receipt) || receipt.Recipe != recipe)
            throw new InvalidDataException("Instalador sem comprovante valido desta sessao.");
        var locked = InstallerFileGuard.OpenRead(path);
        try
        {
            await InstallerFileGuard.RequireDigestAsync(locked, receipt.Digest);
            if (!IsPinnedUnsignedSevenZip(recipe) &&
                !VerifyAuthenticode(path, recipe.RequiredSigner, out string issue))
                throw new CryptographicException("Assinatura alterada antes da execucao (" + issue + ").");
            return locked;
        }
        catch { locked.Dispose(); throw; }
    }

    // SHA-256 do instalador EXE x64 26.05, conforme asset oficial ip7z/7zip.
    // 7-Zip distribui EXE sem Authenticode: excecao EXCLUSIVA por hash fixo,
    // sem permitir excecoes genericas a instaladores unsigned.
    private const string SevenZipDigest = "FD99E86F57D8F429EF5ED045BF98315AF962E2309B0F1BA87A147A44C71647D0";
    private const string SevenZipId = "sevenzip-exe-x64-26.05";
    private static readonly Version SevenZipTarget = new(26, 5, 0, 0);
    private static readonly Uri SevenZipExe = new("https://github.com/ip7z/7zip/releases/download/26.05/7z2605-x64.exe");
    internal static VerifiedUpdateRecipe CreateSevenZipRecipe() =>
        new(SevenZipId, "7-Zip", "26.05", SevenZipExe, SevenZipDigest,
            "", "/S", false, "https://www.7-zip.org/download.html");

    private static bool IsPinnedUnsignedSevenZip(VerifiedUpdateRecipe recipe) =>
        recipe.Identifier == SevenZipId && recipe == CreateSevenZipRecipe();

    private static readonly Version VcTarget = new(12, 0, 40664, 0);
    private const string ObsDigest = "c3a0b880adbe64dc4bcb68f93016916ab5b55ae43fd227115287bf80257d92dc";
    private const string NppDigest = "26f3bcced788fadbac4e7e577b430ea2ce6060cf3bcad993ef4612ecd7d743da";
    private const string SumatraX64Digest = "1EEE71CCCD2EA6E94D5BCEA54EE2F759844DA3E1A0EE2F6045035B1D17B94381";
    private const string SumatraX86Digest = "E232308A77E2293BAFE4AC4079DDB8EF00CA10DE43B9477CA55F17A03419770B";
    private static readonly Version SumatraTarget = new(3, 6, 1, 0);
    private static readonly Uri SumatraX64 = new("https://www.sumatrapdfreader.org/dl/rel/3.6.1/SumatraPDF-3.6.1-64-install.exe");
    private static readonly Uri SumatraX86 = new("https://www.sumatrapdfreader.org/dl/rel/3.6.1/SumatraPDF-3.6.1-install.exe");
    private static readonly Version NppVersion = new(8, 9, 8, 1);
    private static readonly Uri Npp64 = new("https://github.com/notepad-plus-plus/notepad-plus-plus/releases/download/v8.9.8.1/npp.8.9.8.1.Installer.x64.exe");
    // Integridade fixada conforme manifesto Microsoft/winget-pkgs da versão 3.5.283.
    private const string GameInputDigest = "B5988CB8FF9D7009B6DDF6AD4E3FF87E91B00CC17FFCD5D628DABC21C208F100";
    private static readonly Version GameInputTarget = new(3, 5, 283, 0);
    private static readonly Uri GameInputPackage =
        new("https://globalcdn.nuget.org/packages/microsoft.gameinput.3.5.283.nupkg");
    private static readonly Uri Vc64 = new("https://aka.ms/highdpimfc2013x64enu");
    private static readonly Uri Vc86 = new("https://aka.ms/highdpimfc2013x86enu");
    private static readonly Uri Obs = new("https://github.com/obsproject/obs-studio/releases/download/32.2.2/OBS-Studio-32.2.2-Windows-x64-Installer.exe");

    private static bool IsMicrosoft(string? publisher) =>
        new[] { "Microsoft Corporation", "Microsoft" }.Contains((publisher ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

    // IDs oficiais conhecidos; GameInput nao tem pagina individual confirmada.
    internal static Uri? GetMicrosoftStoreUri(ProgramEntry entry)
    {
        if (!entry.IsUpdate || !IsMicrosoft(entry.Publisher)) return null;
        // Microsoft GameInput NAO possui pagina de aplicativo na Store.
        if (entry.Name.Equals("Outlook for Windows", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.Equals("Outlook para Windows", StringComparison.OrdinalIgnoreCase))
            return new Uri("ms-windows-store://pdp/?ProductId=9NRX63209R7B");
        if (entry.Name.Equals("Windows Terminal", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.Equals("Terminal do Windows", StringComparison.OrdinalIgnoreCase))
            return new Uri("ms-windows-store://pdp/?ProductId=9N0DX20HK701");
        return null;
    }

    internal static string? GetStoreWebFallback(ProgramEntry entry)
    {
        if (GetMicrosoftStoreUri(entry) is null) return null;
        if (entry.Name.StartsWith("Outlook", StringComparison.OrdinalIgnoreCase))
            return "https://apps.microsoft.com/detail/9NRX63209R7B";
        if (entry.Name.Contains("Terminal", StringComparison.OrdinalIgnoreCase))
            return "https://apps.microsoft.com/detail/9N0DX20HK701";
        return null;
    }

    internal static string? GetOfficialPage(ProgramEntry entry)
    {
        if (!entry.IsUpdate) return null;
        if (entry.Name == "EA app") return "https://www.ea.com/ea-app";
        if (entry.Name == "FanControl") return "https://getfancontrol.com/";
        if (entry.Name == "Jellyfin Server") return "https://jellyfin.org/downloads/windows/";
        if (entry.Name.StartsWith("K-Lite Mega Codec Pack", StringComparison.OrdinalIgnoreCase))
            return "https://codecguide.com/download_k-lite_codec_pack_mega.htm";
        if (entry.Name.StartsWith("Adobe Acrobat", StringComparison.OrdinalIgnoreCase))
            return "https://helpx.adobe.com/br/acrobat/desktop/get-set-up/access-the-app/update-manual.html";
        if (entry.Name.Equals("Plex", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.StartsWith("Plex Media Server", StringComparison.OrdinalIgnoreCase))
            return "https://www.plex.tv/pt-br/apps-devices/";
        if (entry.Name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase))
            return "https://obsproject.com/download";
        if (entry.Source == "Mozilla" &&
            entry.Name.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            return "https://www.firefox.com/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.StartsWith("Notepad++", StringComparison.OrdinalIgnoreCase))
            return "https://notepad-plus-plus.org/downloads/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.Contains("PowerToys", StringComparison.OrdinalIgnoreCase))
            return "https://learn.microsoft.com/windows/powertoys/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.StartsWith("PowerShell 7", StringComparison.OrdinalIgnoreCase))
            return "https://github.com/PowerShell/PowerShell/releases";
        if (entry.Source == "Microsoft" && entry.Name.Equals("Microsoft Edge", StringComparison.OrdinalIgnoreCase))
            return "https://www.microsoft.com/edge";
        if (entry.Source == "GitHub oficial" &&
            (entry.Name.Equals("Windows Terminal", StringComparison.OrdinalIgnoreCase) ||
             entry.Name.Equals("Terminal do Windows", StringComparison.OrdinalIgnoreCase)))
            return "https://github.com/microsoft/terminal/releases";
        if (entry.Source == "Proton" && entry.Name.Equals("Proton Pass", StringComparison.OrdinalIgnoreCase))
            return "https://proton.me/pass/download";
        if (entry.Source == "Mozilla" &&
            entry.Name.Contains("Thunderbird", StringComparison.OrdinalIgnoreCase))
            return "https://www.thunderbird.net/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.StartsWith("7-Zip ", StringComparison.OrdinalIgnoreCase))
            return "https://www.7-zip.org/download.html";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.Equals("GitHub Desktop", StringComparison.OrdinalIgnoreCase))
            return "https://desktop.github.com/";
        if (entry.Source == "GitHub oficial" &&
            entry.Name.StartsWith("SumatraPDF", StringComparison.OrdinalIgnoreCase))
            return "https://www.sumatrapdfreader.org/download-free-pdf-viewer";
        if (entry.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable", StringComparison.OrdinalIgnoreCase))
            return "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/";
        return null;
    }

    internal static VerifiedUpdateRecipe? Resolve(ProgramEntry entry)
    {
        if (entry.InventoryOnly || ProtectedApplications.IsProtectedName(entry.Name) || entry.UpdateRequiresReview || !entry.IsUpdate || string.IsNullOrWhiteSpace(entry.AvailableVersion)) return null;

        if (IsMicrosoft(entry.Publisher) &&
            entry.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable", StringComparison.OrdinalIgnoreCase) &&
            TryVersion(entry.AvailableVersion, out var available) && available == VcTarget &&
            TryVersion(entry.Version, out var installed) && installed < VcTarget)
        {
            bool x64 = entry.Name.Contains("(x64)", StringComparison.OrdinalIgnoreCase);
            bool x86 = entry.Name.Contains("(x86)", StringComparison.OrdinalIgnoreCase);
            if (x64 == x86) return null;
            return new(x64 ? "vc2013-x64" : "vc2013-x86", "Microsoft Visual C++ 2013",
                VcTarget.ToString(), x64 ? Vc64 : Vc86, null, "Microsoft Corporation",
                "/install /quiet /norestart", false,
                "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/");
        }
        if (entry.Source == "GitHub oficial" && IsSevenZipExeInstallation(entry) &&
            TryVersion(entry.AvailableVersion, out var zipAvailable) && zipAvailable == SevenZipTarget &&
            TryVersion(entry.Version, out var zipInstalled) && zipInstalled < SevenZipTarget)
            return CreateSevenZipRecipe();

        if (entry.Name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase) &&
            MatchesInstalledPublisher("obs-x64-32.2.2", entry.Publisher) &&
            TryVersion(entry.Version, out var obsInstalled) && obsInstalled < new Version(32, 2, 2, 0) &&
            TryVersion(entry.AvailableVersion, out var obsAvailable) && obsAvailable == new Version(32, 2, 2, 0))
        {
            return new("obs-x64-32.2.2", "OBS Studio", "32.2.2", Obs, ObsDigest,
                "OBS Project, LLC", "", true, "https://obsproject.com/download");
        }
        if (entry.Source == "GitHub oficial" &&
            (entry.Name.Equals("Notepad++ (64-bit x64)", StringComparison.OrdinalIgnoreCase) ||
             entry.Name.Equals("Notepad++ (x64)", StringComparison.OrdinalIgnoreCase)) &&
            (entry.Publisher.Equals("Don HO", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("Notepad++", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("Notepad Team", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("Notepad++ Team", StringComparison.OrdinalIgnoreCase)) &&
            TryVersion(entry.Version, out var nppInstalled) && nppInstalled < NppVersion &&
            TryVersion(entry.AvailableVersion, out var nppAvailable) && nppAvailable == NppVersion)
        {
            return new("notepadpp-x64-8.9.8.1", "Notepad++", "8.9.8.1", Npp64, NppDigest,
                "Notepad++", "/S", false, "https://notepad-plus-plus.org/downloads/");
        }
        if (entry.Name.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase) &&
            entry.Source == "GitHub oficial" &&
            (entry.Publisher.Equals("Krzysztof Kowalczyk", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("SumatraPDF Reader", StringComparison.OrdinalIgnoreCase)) &&
            TryVersion(entry.Version, out var sumInstalled) && sumInstalled < SumatraTarget &&
            TryVersion(entry.AvailableVersion, out var sumAvailable) && sumAvailable == SumatraTarget)
        {
            var detected = DetectSumatraInstallation(entry);
            if (detected is not null)
                return CreateSumatraRecipe(entry, detected.Value.IsX64, detected.Value.AllUsers);
        }
        return null;
    }

    // Distingue a instalacao EXE x64 padrao do pacote MSI visto no PC FORTE.
    // Nome/editor/versao ou apenas o Registro nunca autorizam substituicao silenciosa.
    internal static bool IsSevenZipRegistryCandidate(ProgramEntry entry)
    {
        var match = System.Text.RegularExpressions.Regex.Match(entry.Name,
            @"^7-Zip\s+(?<version>\d+(?:\.\d+){1,3})\s+\(x64\)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success || !entry.Publisher.Equals("Igor Pavlov", StringComparison.OrdinalIgnoreCase) ||
            !entry.InventoryId.Equals("LocalMachine|Registry64|7-Zip", StringComparison.OrdinalIgnoreCase) ||
            !TryVersion(match.Groups["version"].Value, out var named) ||
            !TryVersion(entry.Version, out var installed) || installed != named)
            return false;
        string canonical = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip");
        string registered = entry.InstallLocation.Trim().TrimEnd(Path.DirectorySeparatorChar);
        string uninstaller = entry.UninstallString.Trim().Trim('"');
        return registered.Equals(canonical, StringComparison.OrdinalIgnoreCase) &&
            uninstaller.Equals(Path.Combine(canonical, "Uninstall.exe"), StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSevenZipExeInstallation(ProgramEntry entry)
    {
        if (!IsSevenZipRegistryCandidate(entry)) return false;
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip");
        try
        {
            if (!Directory.Exists(directory) ||
                (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return false;
            foreach (string name in new[] { "7zFM.exe", "7z.exe", "7z.dll", "Uninstall.exe" })
            {
                string file = Path.Combine(directory, name);
                if (!File.Exists(file) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) return false;
            }
            // EXE sem arquivo funcional corresponde ao incidente real de instalacao quebrada.
            var information = FileVersionInfo.GetVersionInfo(Path.Combine(directory, "7zFM.exe"));
            return TryVersion(information.FileVersion, out var actual) &&
                TryVersion(entry.Version, out var expected) && actual == expected;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException) { return false; }
    }

    internal static bool SingleSevenZipRegistration(ProgramEntry chosen, IEnumerable<ProgramEntry> inventory)
    {
        if (!IsSevenZipExeInstallation(chosen)) return false;
        var family = inventory.Where(x => x.Name.StartsWith("7-Zip", StringComparison.OrdinalIgnoreCase) &&
            (x.Name.Length == 5 || !char.IsLetterOrDigit(x.Name[5]))).ToArray();
        return family.Length == 1 &&
            family[0].InventoryId.Equals(chosen.InventoryId, StringComparison.OrdinalIgnoreCase);
    }

    // Valida local e arquitetura PE real: nunca converte uma instalação x86 em x64
    // nem altera a instalação por usuário para instalação global.
    internal static (bool IsX64, bool AllUsers)? DetectSumatraInstallation(ProgramEntry entry)
    {
        string? executable = FindSumatraExecutable(entry);
        if (executable is null) return null;
        ushort machine = GetPortableExecutableMachine(executable);
        if (machine is not (0x8664 or 0x014C)) return null;
        string directory = Path.GetDirectoryName(executable)!;
        string global = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SumatraPDF");
        string global32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SumatraPDF");
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF");
        bool allUsers = directory.Equals(global, StringComparison.OrdinalIgnoreCase) ||
                        directory.Equals(global32, StringComparison.OrdinalIgnoreCase);
        if (!allUsers && !directory.Equals(local, StringComparison.OrdinalIgnoreCase)) return null;
        return (machine == 0x8664, allUsers);
    }

    private static string? FindSumatraExecutable(ProgramEntry entry)
    {
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SumatraPDF");
        string global = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SumatraPDF");
        string global32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SumatraPDF");
        var allowed = new[] { local, global, global32 }
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string registryRoot = !string.IsNullOrWhiteSpace(entry.InstallLocation)
            ? entry.InstallLocation.Trim().Trim('"')
            : ExtractExecutableDirectory(entry.UninstallString);
        if (!string.IsNullOrWhiteSpace(registryRoot))
        {
            // Se o registro indica um caminho personalizado, não cair silenciosamente
            // em outro SumatraPDF por coincidência.
            try
            {
                string normalized = Path.GetFullPath(registryRoot).TrimEnd(Path.DirectorySeparatorChar);
                if (!allowed.Any(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                    return null;
                return VerifiedSumatraPath(normalized);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return null;
            }
        }
        // Alguns instaladores omitem InstallLocation: só aceitar quando houver
        // exatamente uma instalação padrão, nunca decidir entre duas variantes.
        var located = allowed.Select(VerifiedSumatraPath).Where(x => x is not null).ToArray();
        return located.Length == 1 ? located[0] : null;
    }

    private static string? VerifiedSumatraPath(string directory)
    {
        try
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return null;
            string file = Path.Combine(directory, "SumatraPDF.exe");
            return File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0 ? file : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return null; }
    }

    private static string ExtractExecutableDirectory(string command)
    {
        var value = command.Trim();
        if (value.StartsWith('"'))
        {
            int closing = value.IndexOf('"', 1);
            if (closing > 1) value = value.Substring(1, closing - 1);
        }
        else
        {
            int exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe >= 0) value = value.Substring(0, exe + 4);
        }
        if (!value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return "";
        return Path.GetDirectoryName(value) ?? "";
    }

    private static ushort GetPortableExecutableMachine(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 256 || reader.ReadUInt16() != 0x5A4D) return 0;
            stream.Position = 0x3C;
            int offset = reader.ReadInt32();
            if (offset < 64 || offset > stream.Length - 6) return 0;
            stream.Position = offset;
            return reader.ReadUInt32() == 0x00004550 ? reader.ReadUInt16() : (ushort)0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return 0; }
    }

    // Testável sem precisar escrever no Registro do Windows.
    internal static VerifiedUpdateRecipe CreateSumatraRecipe(ProgramEntry entry, bool x64, bool allUsers)
        => new(x64 ? "sumatrapdf-x64-3.6.1" : "sumatrapdf-x86-3.6.1",
            "SumatraPDF", "3.6.1", x64 ? SumatraX64 : SumatraX86,
            x64 ? SumatraX64Digest : SumatraX86Digest,
            "Krzysztof Kowalczyk", allUsers ? "-install -silent -all-users" : "-install -silent",
            false, "https://www.sumatrapdfreader.org/download-free-pdf-viewer");

    // Um candidato sempre recebe uma acao explicita, mas apenas receitas locais
    // assinadas e verificadas autorizam iniciar um instalador.
    internal static string? GetPrimaryAction(ProgramEntry entry)
    {
        if (!entry.IsUpdate) return null;
        if (entry.UpdateRequiresReview)
            return GetMicrosoftStoreUri(entry) is not null ? "store" : GetOfficialPage(entry) is not null ? "site" : "guide";
        if (WindowsTerminalUpdater.Resolve(entry) is not null) return "terminal";
        if (GetMicrosoftStoreUri(entry) is not null) return "store";
        if (Resolve(entry) is { } recipe && IsApprovedRecipe(recipe)) return "update";
        if (GetOfficialPage(entry) is not null) return "site";
        return "guide";
    }

    internal static bool TryVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        var parts = (value ?? "").Trim().TrimStart('v', 'V').Split('.');
        if (parts.Length < 2 || parts.Length > 4 || parts.Any(x => !int.TryParse(x, out int n) || n < 0))
            return false;
        string padded = string.Join(".", parts.Concat(Enumerable.Repeat("0", 4 - parts.Length)));
        if (!Version.TryParse(padded, out var parsed) || parsed is null) return false;
        version = parsed;
        return true;
    }

    internal static bool IsInstalledVersionAtLeast(string identifier, string version, IEnumerable<ProgramEntry> apps)
    {
        if (!TryVersion(version, out var target)) return false;
        foreach (var entry in apps)
        {
            // A confirmacao posterior nao pode aceitar apenas nome/versao:
            // outro editor ou uma entrada somente leitura nao comprova instalacao.
            if (entry.InventoryOnly || ProtectedApplications.IsProtectedName(entry.Name) || !MatchesInstalledPublisher(identifier, entry.Publisher))
                continue;
            bool belongs = identifier switch
            {
                "vc2013-x64" => entry.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable (x64)", StringComparison.OrdinalIgnoreCase),
                "vc2013-x86" => entry.Name.StartsWith("Microsoft Visual C++ 2013 Redistributable (x86)", StringComparison.OrdinalIgnoreCase),
                "obs-x64-32.2.2" => entry.Name.Equals("OBS Studio", StringComparison.OrdinalIgnoreCase),
                "notepadpp-x64-8.9.8.1" => entry.Name.Equals("Notepad++ (64-bit x64)", StringComparison.OrdinalIgnoreCase) ||
                                               entry.Name.Equals("Notepad++ (x64)", StringComparison.OrdinalIgnoreCase),
                "sumatrapdf-x64-3.6.1" or "sumatrapdf-x86-3.6.1" =>
                    entry.Name.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase),
                "gameinput-x64-3.5.283" => entry.Name.Equals("Microsoft GameInput", StringComparison.OrdinalIgnoreCase),
                SevenZipId => IsSevenZipRegistryCandidate(entry),
                _ => false
            };
            if (belongs && TryVersion(entry.Version, out var installed) && installed >= target)
                return true;
        }
        return false;
    }

    internal static bool ConfirmInstallation(VerifiedUpdateRecipe recipe, ProgramEntry original,
        IEnumerable<ProgramEntry> current)
    {
        // Confirma a instalacao escolhida, nao outra copia do mesmo produto.
        // Se o instalador trocar a identidade, manter resultado indeterminado.
        if (!IsApprovedRecipe(recipe) || original.InventoryOnly ||
            ProtectedApplications.IsProtectedName(original.Name) ||
            string.IsNullOrWhiteSpace(original.InventoryId) ||
            !IsInstalledVersionAtLeast(recipe.Identifier, "0.0", [original])) return false;
        var snapshot = current.ToArray();
        if (snapshot.Count(x => x.InventoryId.Equals(original.InventoryId, StringComparison.OrdinalIgnoreCase)) != 1) return false;
        foreach (var entry in snapshot)
        {
            if (!original.InventoryId.Equals(entry.InventoryId, StringComparison.OrdinalIgnoreCase) ||
                !IsInstalledVersionAtLeast(recipe.Identifier, recipe.Version, new[] { entry })) continue;
            if (recipe.Identifier == SevenZipId)
            {
                if (!IsSevenZipExeInstallation(entry) || !SingleSevenZipRegistration(entry, snapshot))
                    continue;
            }
            if (recipe.Identifier.StartsWith("sumatrapdf-", StringComparison.Ordinal))
            {
                var installation = DetectSumatraInstallation(entry);
                bool expectedX64 = recipe.Identifier == "sumatrapdf-x64-3.6.1";
                bool expectedAllUsers = recipe.Arguments.Contains("-all-users", StringComparison.Ordinal);
                if (installation is null || installation.Value.IsX64 != expectedX64 ||
                    installation.Value.AllUsers != expectedAllUsers) continue;
            }
            return true;
        }
        return false;
    }

    internal static bool SameInstallationForUpdate(ProgramEntry original, IEnumerable<ProgramEntry> current)
    {
        if (string.IsNullOrWhiteSpace(original.InventoryId) || original.InventoryOnly ||
            ProtectedApplications.IsProtectedName(original.Name)) return false;
        var matches = current.Where(x => x.InventoryId.Equals(original.InventoryId, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 && !matches[0].InventoryOnly &&
            !ProtectedApplications.IsProtectedName(matches[0].Name) &&
            matches[0].Name.Equals(original.Name, StringComparison.OrdinalIgnoreCase) &&
            matches[0].Publisher.Equals(original.Publisher, StringComparison.OrdinalIgnoreCase) &&
            matches[0].Version == original.Version && matches[0].InstallLocation == original.InstallLocation &&
            matches[0].UninstallString == original.UninstallString;
    }

    private static bool MatchesInstalledPublisher(string identifier, string publisher)
    {
        string[] allowed = identifier switch
        {
            "vc2013-x64" or "vc2013-x86" or "gameinput-x64-3.5.283" =>
                ["Microsoft Corporation", "Microsoft"],
            "obs-x64-32.2.2" => ["OBS Project", "OBS Project, LLC"],
            "notepadpp-x64-8.9.8.1" => ["Don HO", "Notepad++", "Notepad Team", "Notepad++ Team"],
            "sumatrapdf-x64-3.6.1" or "sumatrapdf-x86-3.6.1" =>
                ["Krzysztof Kowalczyk", "SumatraPDF", "SumatraPDF Reader"],
            SevenZipId => ["Igor Pavlov"],
            _ => []
        };
        return allowed.Contains(publisher.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAllowedRemoteUri(Uri uri, string id)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsAbsoluteUri || !string.IsNullOrEmpty(uri.UserInfo) || uri.Port != 443)
            return false;
        string host = uri.Host.ToLowerInvariant();
        if (id.StartsWith("vc2013-", StringComparison.Ordinal))
            return host is "aka.ms" or "download.microsoft.com" or "download.visualstudio.microsoft.com";
        if (id == "gameinput-x64-3.5.283")
            return host == "globalcdn.nuget.org" &&
                   uri.AbsolutePath.Equals("/packages/microsoft.gameinput.3.5.283.nupkg",
                       StringComparison.OrdinalIgnoreCase);
        if (id.StartsWith("sumatrapdf-", StringComparison.Ordinal))
        {
            // O site oficial redireciona somente para seu subdominio de arquivos.
            // Ambos os destinos precisam corresponder ao executavel esperado.
            bool x64 = id == "sumatrapdf-x64-3.6.1";
            bool x86 = id == "sumatrapdf-x86-3.6.1";
            string suffix = x64 ? "SumatraPDF-3.6.1-64-install.exe" : "SumatraPDF-3.6.1-install.exe";
            return (x64 || x86) &&
                   ((host == "www.sumatrapdfreader.org" &&
                     uri.AbsolutePath.Equals("/dl/rel/3.6.1/" + suffix, StringComparison.Ordinal)) ||
                    (host == "files.sumatrapdfreader.org" &&
                     uri.AbsolutePath.Equals("/software/sumatrapdf/rel/3.6.1/" + suffix, StringComparison.Ordinal)));
        }
        if (id == SevenZipId)
            return (host == "github.com" &&
                    uri.AbsolutePath.Equals("/ip7z/7zip/releases/download/26.05/7z2605-x64.exe",
                        StringComparison.Ordinal)) ||
                   host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
        if (id.StartsWith("obs-", StringComparison.Ordinal) ||
            id.StartsWith("notepadpp-", StringComparison.Ordinal))
            return host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";
        return false;
    }

    internal static async Task<string> DownloadAndVerifyAsync(VerifiedUpdateRecipe recipe,
        string directory, Action<string> report, CancellationToken cancel)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        deadline.CancelAfter(TimeSpan.FromMinutes(8));
        cancel = deadline.Token;
        cancel.ThrowIfCancellationRequested();
        if (!IsApprovedRecipe(recipe) || !IsAllowedRemoteUri(recipe.Download, recipe.Identifier))
            throw new InvalidDataException("Receita ou endereco nao autorizado.");
        directory = InstallerFileGuard.ValidateLocalPath(directory);
        using var parentLock = InstallerFileGuard.PinDirectory(Path.GetDirectoryName(directory)!, allowLeafWrites: true);
        Directory.CreateDirectory(directory);
        using var directoryLock = InstallerFileGuard.PinDirectory(directory, allowLeafWrites: true);
        bool gameInput = recipe.Identifier == "gameinput-x64-3.5.283";
        string path = Path.Combine(directory, recipe.Identifier + (gameInput ? ".nupkg" : ".exe"));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GerenciadorFinal/5.12.18.1");
        Uri target = recipe.Download;
        const long maxBytes = 260L * 1024 * 1024;
        for (int redirect = 0; redirect <= 7; redirect++)
        {
            cancel.ThrowIfCancellationRequested();
            if (!IsAllowedRemoteUri(target, recipe.Identifier)) throw new InvalidDataException("Redirecionamento fora do domínio autorizado.");
            using var request = new HttpRequestMessage(HttpMethod.Get, target);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (response.Headers.Location is null) throw new InvalidDataException("Redirecionamento incompleto.");
                target = new Uri(target, response.Headers.Location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length > maxBytes)
                throw new InvalidDataException("Arquivo excede o limite de segurança.");
            report("Baixando instalador do fabricante...");
            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            await using (var dest = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[128 * 1024];
                long total = 0;
                long reported = 0;
                int n;
                while ((n = await source.ReadAsync(buffer, cancel)) > 0)
                {
                    total += n;
                    if (total > maxBytes) throw new InvalidDataException("Arquivo excede o limite de segurança.");
                    await dest.WriteAsync(buffer.AsMemory(0, n), cancel);
                    if (total - reported >= 10 * 1024 * 1024)
                    {
                        reported = total;
                        report("Baixando instalador oficial: " + (total / (1024 * 1024)).ToString("N0") + " MB");
                    }
                }
                if (total < 50_000) throw new InvalidDataException("Arquivo recebido está incompleto.");
            }
            report("Verificando integridade e assinatura digital...");
            using var downloaded = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
            string actualDigest = await InstallerFileGuard.DigestAsync(downloaded, cancel);
            if (recipe.Sha256 is string digest &&
                !actualDigest.Equals(digest, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException("SHA-256 nao corresponde ao arquivo oficial.");
            if (gameInput)
            {
                // Nunca executar nupkg/ZIP diretamente. Extrair APENAS o MSI no
                // caminho conhecido, apos validar o SHA publicado no winget-pkgs.
                string msi = Path.Combine(directory, recipe.Identifier + ".msi");
                using (var archive = new ZipArchive(downloaded, ZipArchiveMode.Read, leaveOpen: true))
                {
                    if (archive.Entries.Count is < 1 or > 300)
                        throw new InvalidDataException("Pacote NuGet com estrutura inesperada.");
                    var entries = archive.Entries.Where(entry =>
                        entry.FullName.Replace('\\', '/')
                            .Equals("redist/GameInputRedist.msi", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (entries.Length != 1 || entries[0].Length < 50_000 || entries[0].Length > 80L * 1024 * 1024)
                        throw new InvalidDataException("Redistribuível oficial GameInput ausente ou inválido.");
                    entries[0].ExtractToFile(msi);
                    using var lockedMsi = InstallerFileGuard.OpenRead(msi);
                    // A identidade Microsoft sozinha nao prova que este e o MSI
                    // daquele NUPKG. Releia o conteudo autenticado e compare por
                    // handle apos fechar a extracao e prender o arquivo no disco.
                    string msiDigest = await RequireArchiveDigestAsync(lockedMsi, entries[0], cancel);
                    if (!VerifyAuthenticode(msi, recipe.RequiredSigner, out string msiIssue))
                        throw new CryptographicException("MSI Microsoft sem assinatura válida (" + msiIssue + ").");
                    RememberDownload(msi, recipe, msiDigest);
                    return msi;
                }
            }
            if (!IsPinnedUnsignedSevenZip(recipe) &&
                !VerifyAuthenticode(path, recipe.RequiredSigner, out string issue))
                throw new CryptographicException("Assinatura Authenticode inválida ou editor não esperado (" + issue + ").");
            RememberDownload(path, recipe, actualDigest);
            return path;
        }
        throw new InvalidDataException("Excesso de redirecionamentos.");
    }

    internal static async Task<string> RequireArchiveDigestAsync(FileStream extracted,
        ZipArchiveEntry authenticatedSource, CancellationToken cancel = default)
    {
        await using var source = authenticatedSource.Open();
        string expected = Convert.ToHexString(await SHA256.HashDataAsync(source, cancel));
        await InstallerFileGuard.RequireDigestAsync(extracted, expected, cancel);
        return expected;
    }

    internal static bool VerifyAuthenticode(string path, string signer, out string issue)
    {
        issue = "";
        // A verificação de cadeia e assinatura cabe ao Windows (WinVerifyTrust).
        var info = new WinTrustFileInfo { Size = (uint)Marshal.SizeOf<WinTrustFileInfo>(), FilePath = path };
        IntPtr pInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        Guid action = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        var data = new WinTrustData
        {
            Size = (uint)Marshal.SizeOf<WinTrustData>(), UiChoice = 2,
            RevocationChecks = 1, UnionChoice = 1, FileInfo = pInfo,
            StateAction = 1, ProviderFlags = 0x80
        };
        bool verifyCalled = false;
        bool infoInitialized = false;
        try
        {
            Marshal.StructureToPtr(info, pInfo, false);
            infoInitialized = true;
            int status = WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            verifyCalled = true;
            if (status != 0)
            {
                issue = "WinVerifyTrust=0x" + status.ToString("X8");
                return false;
            }
            // O certificado deve pertencer a assinatura efetivamente validada pelo
            // Windows, nao a uma segunda interpretacao independente do arquivo.
            IntPtr provider = WTHelperProvDataFromStateData(data.StateData);
            IntPtr providerSigner = provider == IntPtr.Zero ? IntPtr.Zero :
                WTHelperGetProvSignerFromChain(provider, 0, false, 0);
            IntPtr providerCert = providerSigner == IntPtr.Zero ? IntPtr.Zero :
                WTHelperGetProvCertFromChain(providerSigner, 0);
            if (providerCert == IntPtr.Zero)
                throw new CryptographicException("Certificado do assinante validado indisponivel.");
            var trusted = Marshal.PtrToStructure<ProviderCertificatePrefix>(providerCert);
            if (trusted.Size < Marshal.SizeOf<ProviderCertificatePrefix>() ||
                trusted.Certificate == IntPtr.Zero)
                throw new CryptographicException("Estrutura do assinante validado invalida.");
            using var cert = LoadSignerCertificate(trusted.Certificate);
            string name = cert.GetNameInfo(X509NameType.SimpleName, false);
            issue = "editor=" + name;
            return name.Equals(signer, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException
            or DllNotFoundException or EntryPointNotFoundException)
        {
            issue = "validacao=" + ex.GetType().Name;
            return false;
        }
        finally
        {
            // WTD_STATEACTION_CLOSE deve acompanhar toda tentativa VERIFY,
            // inclusive assinatura recusada e excecao ao interpretar certificado.
            try
            {
                if (verifyCalled)
                {
                    data.StateAction = 2;
                    WinVerifyTrust(IntPtr.Zero, ref action, ref data);
                }
            }
            finally
            {
                if (infoInitialized) Marshal.DestroyStructure<WinTrustFileInfo>(pInfo);
                Marshal.FreeHGlobal(pInfo);
            }
        }
    }

    // Entrada exclusiva de CERT_CONTEXT obtido do provedor de confianca Windows.
    // O loader recebe somente DER; nao interpreta EXE/MSI/PFX nem importa chaves.
    internal static X509Certificate2 LoadSignerCertificate(IntPtr certificateContext)
    {
        if (certificateContext == IntPtr.Zero)
            throw new CryptographicException("Contexto do certificado ausente.");
        var context = Marshal.PtrToStructure<CertificateContext>(certificateContext);
        if (context.Encoded == IntPtr.Zero || context.Length is 0 or > 1024 * 1024)
            throw new CryptographicException("Certificado do assinante fora dos limites.");
        var der = new byte[context.Length];
        Marshal.Copy(context.Encoded, der, 0, der.Length);
        return X509CertificateLoader.LoadCertificate(der);
    }

    // Apenas o prefixo documentado de CRYPT_PROVIDER_CERT e necessario.
    [StructLayout(LayoutKind.Sequential)]
    private struct ProviderCertificatePrefix
    {
        public uint Size;
        public IntPtr Certificate;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CertificateContext
    {
        public uint Encoding;
        public IntPtr Encoded;
        public uint Length;
        public IntPtr Information;
        public IntPtr Store;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
        public IntPtr Handle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint Size;
        public IntPtr PolicyCallback;
        public IntPtr ClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid actionId, ref WinTrustData data);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr state);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr provider, uint index,
        [MarshalAs(UnmanagedType.Bool)] bool counterSigner, uint counterIndex);
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvCertFromChain(IntPtr signer, uint index);

    internal static async Task<int> LaunchAsync(VerifiedUpdateRecipe recipe, string verifiedFile, Action<string> report)
    {
        // Preserva arquivo e ancestrais ate o GF encerrar, inclusive apos a saida do bootstrapper.
        // Recibo e hash sao locais a esta sessao; nao sao lidos de arquivo adulteravel.
        var verifiedLock = await OpenVerifiedForLaunchAsync(recipe, verifiedFile);
        bool retained = false;
        try
        {
        ValidateInstallerDirectory(recipe, verifiedFile);
        if (recipe.Identifier == SevenZipId &&
            new[] { "7zFM", "7zG", "7z", "7za" }
                .Any(name => Process.GetProcessesByName(name).Length != 0))
            throw new InvalidOperationException("Feche os processos do 7-Zip antes de atualizar. Nenhum processo será encerrado automaticamente.");
        if (recipe.Identifier == "obs-x64-32.2.2" &&
            Process.GetProcessesByName("obs64").Any())
            throw new InvalidOperationException("Feche o OBS Studio antes de instalar a atualização.");
        bool isMsi = recipe.Identifier == "gameinput-x64-3.5.283";
        if (isMsi && !verifiedFile.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Instalador GameInput não é um MSI verificado.");
        report(recipe.Interactive
            ? "Abrindo instalador oficial para você concluir as opções necessárias..."
            : "Executando atualização oficial. O Windows pode solicitar autorização UAC...");
        SessionLocks.Retain(verifiedLock);
        retained = true;
        ProtectedFolders[Path.GetDirectoryName(Path.GetFullPath(verifiedFile))!] = 0;
        using var process = Process.Start(new ProcessStartInfo
        {
            // Caminho System32 absoluto: nenhum executável homônimo vindo do pacote ou PATH.
            FileName = isMsi
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe")
                : verifiedFile,
            Arguments = isMsi ? $"/i \"{verifiedFile}\" {recipe.Arguments}" : recipe.Arguments,
            UseShellExecute = true, Verb = "runas",
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
        }) ?? throw new InvalidOperationException("Instalador não foi iniciado.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        { throw new TimeoutException("Instalador iniciado; conclusão indeterminada. Não repetir automaticamente. Confira a versão após concluir o fabricante."); }
        return process.ExitCode;
        }
        finally { if (!retained) verifiedLock.Dispose(); }
    }
}
