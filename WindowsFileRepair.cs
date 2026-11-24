namespace GerenciadorAplicativos;

internal static class WindowsFileRepair
{
    internal static async Task<int> RunAsync(
        Func<string, string[], Task<(int ExitCode, string Output)>> run, Action<string> log)
    {
        log("Etapa 1/4 — DISM: examinando e, se necessário, reparando a imagem do Windows.");
        var dism = await run("dism.exe", ["/Online", "/Cleanup-Image", "/RestoreHealth"]);
        if (dism.ExitCode != 0)
        {
            log("A etapa DISM não terminou normalmente. SFC não foi iniciado.");
            log(WindowsIntegrityAssessment.PostRepairWire(IntegrityState.Inconclusive));
            return dism.ExitCode;
        }

        log("Etapa 2/4 — SFC: tentando reparar os arquivos protegidos do Windows.");
        var repair = await run("sfc.exe", ["/scannow"]);
        if (repair.ExitCode != 0)
        {
            log("O reparo com SFC não terminou normalmente. A verificação pós-reparo não foi iniciada.");
            log(WindowsIntegrityAssessment.PostRepairWire(IntegrityState.Inconclusive));
            return repair.ExitCode;
        }

        log("Etapa 3/4 — DISM: verificando a integridade da imagem após o reparo.");
        var image = await run("dism.exe", ["/Online", "/Cleanup-Image", "/ScanHealth"]);
        log("Etapa 4/4 — SFC: verificando os arquivos protegidos após o reparo.");
        var files = await run("sfc.exe", ["/verifyonly"]);
        var verified = WindowsIntegrityAssessment.Assess(image.ExitCode, image.Output,
            files.ExitCode, files.Output);
        log("Pós-reparo consultivo: " + verified.Summary);
        log(WindowsIntegrityAssessment.PostRepairWire(verified.Overall));
        if (files.ExitCode != 0) return files.ExitCode;
        if (image.ExitCode != 0) return image.ExitCode;
        return verified.FullyVerified ? 0 : 1;
    }
}
