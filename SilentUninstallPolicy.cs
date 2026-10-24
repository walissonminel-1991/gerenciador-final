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
        if (UninstallCommand.TryParse(app.QuietUninstallString, out var registeredQuiet) &&
            registered == registeredQuiet && RegisteredQuietMatchesProduct(app, registeredQuiet))
            return true; // preservar exatamente os argumentos registrados
        // SumatraPDF 3.6.1: contrato conferido no fonte 3.6.1rel/Uninstaller.cpp.
        if (IsVerifiedSumatra(app, registered) &&
            UninstallCommand.TryParse("\"" + registered.FileName + "\" " + registered.Arguments + " -silent", out var sumatra))
        { selected = sumatra; return true; }
        // Inno: identidade do EXE + cabeçalho oficial do log adjacente.
        // Nunca inferir o motor apenas por um nome unins000.exe.
        if (IsVerifiedInno(registered) &&
            UninstallCommand.TryParse("\"" + registered.FileName + "\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART", out var inno))
        { selected = inno; return true; }
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
        bool x86 = app.Name.Equals("Notepad++ (32-bit x86)", StringComparison.OrdinalIgnoreCase);
        bool x64 = app.Name.Equals("Notepad++ (64-bit x64)", StringComparison.OrdinalIgnoreCase);
        string folder = Path.Combine(Environment.GetFolderPath(x86
            ? Environment.SpecialFolder.ProgramFilesX86 : Environment.SpecialFolder.ProgramFiles), "Notepad++");
        if ((!x64 && !x86) || app.Publisher is not ("Notepad++ Team" or "Don HO") ||
            !app.InventoryId.StartsWith(x86 ? "LocalMachine|Registry32|" : "LocalMachine|Registry64|", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(app.InstallLocation) && !app.InstallLocation.TrimEnd('\\').Equals(folder, StringComparison.OrdinalIgnoreCase)) ||
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

    internal static bool UsesSilentMode(UninstallRequest request)
    {
        if (UninstallCommand.TryParse(request.Original.QuietUninstallString, out var quiet) &&
            request.Command == quiet && RegisteredQuietMatchesProduct(request.Original, quiet)) return true;
        return new[] { request.Original.UninstallString, request.Original.QuietUninstallString }.Any(raw =>
            UninstallCommand.TryParse(raw, out var command) && TrySelect(request.Original, command, out var selected) && selected == request.Command);
    }
    internal static string DescribeMode(UninstallRequest request) => UsesSilentMode(request)
        ? "Modo silencioso disponível. O Windows ainda pode solicitar autorização."
        : "Modo interativo: acompanhe a janela do fabricante para concluir a desinstalação.";

    internal static bool IsVerifiedSumatra(ProgramEntry app, ParsedUninstallCommand command)
    {
        if (ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher)?.Id != "sumatrapdf" ||
            app.Version != "3.6.1" || command.ProductCode is not null ||
            !Regex.IsMatch(command.Arguments, @"^-uninstall(?: -all-users)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return false;
        string? root = IntegratedResidualPolicy.DedicatedInstallRoot(ResidualAppIdentity.From(app),
            ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher));
        if (root is null || !command.FileName.Equals(Path.Combine(root, "SumatraPDF.exe"), StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            using var held = InstallerFileGuard.OpenRead(command.FileName, allowDirectoryWrites: true);
            return Version.TryParse(FileVersionInfo.GetVersionInfo(command.FileName).FileVersion, out var version) &&
                version.Major == 3 && version.Minor == 6 && version.Build == 1;
        }
        catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex)) { return false; }
    }

    internal static bool IsVerifiedInno(ParsedUninstallCommand command)
    {
        if (command.ProductCode is not null || command.Arguments.Length != 0 ||
            !Regex.IsMatch(Path.GetFileName(command.FileName), @"^unins[0-9]{3}\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return false;
        try
        {
            using var image = InstallerFileGuard.OpenRead(command.FileName, allowDirectoryWrites: true);
            if (!string.Equals(FileVersionInfo.GetVersionInfo(command.FileName).FileDescription,
                    "Setup/Uninstall", StringComparison.OrdinalIgnoreCase)) return false;
            using var log = InstallerFileGuard.OpenRead(Path.ChangeExtension(command.FileName, ".dat"), allowDirectoryWrites: true);
            Span<byte> header = stackalloc byte[64];
            log.ReadExactly(header);
            string id = System.Text.Encoding.ASCII.GetString(header).TrimEnd('\0');
            return id is "Inno Setup Uninstall Log (b)" or "Inno Setup Uninstall Log (b) 64-bit";
        }
        catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex)) { return false; }
    }

    internal static bool MatchesAuthorized(ProgramEntry app, ParsedUninstallCommand chosen)
    {
        foreach (string raw in new[] { app.UninstallString, app.QuietUninstallString })
            if (UninstallCommand.TryParse(raw, out var registered) &&
                (!string.Equals(raw, app.QuietUninstallString, StringComparison.Ordinal) || RegisteredQuietMatchesProduct(app, registered)) &&
                (chosen == registered || TrySelect(app, registered, out var approved) && chosen == approved))
                return true;
        return false;
    }
}
