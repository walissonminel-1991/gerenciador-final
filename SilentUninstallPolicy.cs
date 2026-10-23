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
        // 7-Zip EXE, arquitetura x64, registro exato, executaveis integros e
        // desinstalador original na pasta padrao: parametro /S do NSIS.
        if (!VerifiedUpdaters.IsSevenZipExeInstallation(app) ||
            !UninstallCommand.TryParse(app.UninstallString, out var normal) ||
            normal.ProductCode is not null || normal.Arguments.Length != 0 ||
            !normal.FileName.Equals(registered.FileName, StringComparison.OrdinalIgnoreCase) ||
            !UninstallCommand.TryParse("\"" + normal.FileName + "\" /S", out var silent))
            return false;
        selected = silent;
        return true;
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
