using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Apenas os comandos registrados/validados podem dar origem a um modo silencioso.
// Nunca usa argumentos silenciosos recebidos de fontes remotas ou nomes parecidos.
internal static class SilentUninstallPolicy
{
    internal static bool TrySelect(ProgramEntry app, ParsedUninstallCommand registered,
        out ParsedUninstallCommand selected)
    {
        selected = registered;
        if (app.InventoryOnly || ProtectedApplications.IsProtectedName(app.Name))
            return false;
        if (registered.ProductCode is string code)
        {
            if (Guid.TryParse(code, out var parsed) && parsed != Guid.Empty &&
                Guid.TryParse(app.MsiProductCode, out var identified) && parsed == identified &&
                UninstallCommand.TryParse("msiexec.exe /X" + code + " /qn /norestart", out var quiet) &&
                quiet.ProductCode == registered.ProductCode)
            {
                selected = quiet;
                return true;
            }
            return false;
        }
        // Notepad++ oficial (NSIS): /S e parametro documentado do fabricante.
        // Nao aplicar a programas cujo nome apenas se assemelha a Notepad++.
        if (IsExactNotepadPlusPlus(app) &&
            UninstallCommand.TryParse(app.UninstallString, out var nppOriginal) &&
            nppOriginal.ProductCode is null && nppOriginal.Arguments.Length == 0 &&
            nppOriginal.FileName.Equals(registered.FileName, StringComparison.OrdinalIgnoreCase) &&
            UninstallCommand.TryParse("\"" + nppOriginal.FileName + "\" /S", out var nppSilent))
        {
            selected = nppSilent;
            return true;
        }
        // 7-Zip EXE, arquitetura x64, registro exato, executaveis integros e
        // desinstalador original na pasta padrao: parametro /S do NSIS.
        if (!IsExactSevenZipExe(app) ||
            !UninstallCommand.TryParse(app.UninstallString, out var normal) ||
            normal.ProductCode is not null || normal.Arguments.Length != 0 ||
            !normal.FileName.Equals(registered.FileName, StringComparison.OrdinalIgnoreCase) ||
            !UninstallCommand.TryParse("\"" + normal.FileName + "\" /S", out var silent))
            return false;
        selected = silent;
        return true;
    }

    // Um campo QuietUninstallString nao autoriza trocar o desinstalador
    // por outro EXE ou mudar o MSI ProductCode. O Registro e a identidade
    // continuam sendo revalidados antes do lancamento.
    internal static bool RegisteredQuietMatchesProduct(ProgramEntry app, ParsedUninstallCommand quiet)
    {
        if (app.InventoryOnly || ProtectedApplications.IsProtectedName(app.Name) ||
            !UninstallBatch.BoundToProduct(quiet, app)) return false;
        if (!UninstallCommand.TryParse(app.UninstallString, out var normal))
            return true; // Instaladores podem registrar apenas QuietUninstallString.
        if (normal.ProductCode is not null || quiet.ProductCode is not null)
            return normal.ProductCode is not null && quiet.ProductCode is not null &&
                normal.ProductCode.Equals(quiet.ProductCode, StringComparison.OrdinalIgnoreCase);
        return normal.FileName.Equals(quiet.FileName, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsExactNotepadPlusPlus(ProgramEntry app)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Notepad++");
        if (!app.Name.Equals("Notepad++ (64-bit x64)", StringComparison.OrdinalIgnoreCase) ||
            app.Publisher is not ("Notepad++ Team" or "Don HO") ||
            !app.InventoryId.StartsWith("LocalMachine|Registry64|", StringComparison.OrdinalIgnoreCase) ||
            !app.InstallLocation.TrimEnd('\\').Equals(folder, StringComparison.OrdinalIgnoreCase) ||
            !UninstallCommand.TryParse(app.UninstallString, out var uninstall) ||
            uninstall.ProductCode is not null || uninstall.Arguments.Length != 0 ||
            !uninstall.FileName.Equals(Path.Combine(folder, "uninstall.exe"), StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            return Directory.Exists(folder) && File.Exists(Path.Combine(folder, "notepad++.exe")) &&
                File.Exists(uninstall.FileName) &&
                (File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0 &&
                (File.GetAttributes(uninstall.FileName) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException) { return false; }
    }

    internal static bool IsExactSevenZipExe(ProgramEntry app)
    {
        if (!Regex.IsMatch(app.Name, @"^7-Zip\s+\d+\.\d{1,2}\s+\(x64\)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            !app.Publisher.Equals("Igor Pavlov", StringComparison.OrdinalIgnoreCase) ||
            !app.InventoryId.Equals("LocalMachine|Registry64|7-Zip",
                StringComparison.OrdinalIgnoreCase) ||
            !app.InstallLocation.TrimEnd('\\').Equals(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip"),
                StringComparison.OrdinalIgnoreCase) ||
            !UninstallCommand.TryParse(app.UninstallString, out var original) ||
            original.ProductCode is not null || original.Arguments.Length != 0 ||
            !original.FileName.Equals(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "Uninstall.exe"),
                StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip");
            if (!Directory.Exists(folder) ||
                (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) return false;
            foreach (string name in new[] { "7zFM.exe", "7z.exe", "7z.dll", "Uninstall.exe" })
            {
                string file = Path.Combine(folder, name);
                if (!File.Exists(file) || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) return false;
            }
            string? fileVersion = FileVersionInfo.GetVersionInfo(Path.Combine(folder, "7zFM.exe")).FileVersion;
            return Version.TryParse(fileVersion, out var actual) &&
                Version.TryParse(app.Version, out var expected) &&
                actual.Major == expected.Major && actual.Minor == expected.Minor;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException) { return false; }
    }

    internal static bool MatchesAuthorized(ProgramEntry app, ParsedUninstallCommand chosen)
    {
        foreach (string raw in new[] { app.UninstallString, app.QuietUninstallString })
            if (UninstallCommand.TryParse(raw, out var registered) &&
                (chosen == registered || TrySelect(app, registered, out var approved) && chosen == approved))
                return true;
        return false;
    }
}
