using System.Diagnostics;
using System.Reflection.PortableExecutable;
namespace GerenciadorAplicativos;

// Preparado ANTES da confirmação. Lease permanece até Process.Start retornar;
// depois é solto para permitir que o próprio fornecedor remova seus arquivos.
internal sealed class PreparedUninstaller(UninstallRequest request, ProcessStartInfo start, IDisposable? lease,
    bool unsignedTestException = false) : IDisposable
{
    private IDisposable? held=lease;
    private int attempted;
    internal UninstallRequest Request => request;
    internal bool UnsignedTestException => unsignedTestException;
    internal ProcessStartInfo StartInfo => new() { FileName=start.FileName, Arguments=start.Arguments,
        UseShellExecute=start.UseShellExecute, WorkingDirectory=start.WorkingDirectory };
    internal async Task<int?> LaunchAsync(CancellationToken token)
    {
        if(Interlocked.Exchange(ref attempted,1)!=0) throw new InvalidOperationException("Tentativa já consumida.");
        Process? process;
        try { token.ThrowIfCancellationRequested(); process=Process.Start(start); }
        finally { Dispose(); }
        if(process is null) return null;
        using(process)
        using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)) {
            deadline.CancelAfter(TimeSpan.FromMinutes(3));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch(OperationCanceledException) { throw new TimeoutException("Fornecedor iniciado; conclusão pendente. Não repetir automaticamente."); }
            return process.ExitCode;
        }
    }
    public void Dispose()
    {
        Interlocked.CompareExchange(ref attempted,2,0);
        Interlocked.Exchange(ref held,null)?.Dispose();
    }
}

// Politica de laboratorio: nunca habilitar para qualquer outro aplicativo,
 // nem aceitar a assinatura INVALIDA como se fosse assinatura AUSENTE.
internal static class LabUnsignedUninstallerPolicy
{
    internal static bool IsMatchingRegisteredLabProduct(ProgramEntry entry, string executable,
        string originalFilename)
    {
        bool notepad = entry.Name.StartsWith("Notepad++", StringComparison.OrdinalIgnoreCase) &&
            (entry.Publisher.Equals("Notepad++ Team", StringComparison.OrdinalIgnoreCase) ||
             entry.Publisher.Equals("Don HO", StringComparison.OrdinalIgnoreCase));
        bool foobar = entry.Name.StartsWith("foobar2000", StringComparison.OrdinalIgnoreCase) &&
            entry.Publisher.Equals("Peter Pawlowski", StringComparison.OrdinalIgnoreCase);
        if (!notepad && !foobar || string.IsNullOrWhiteSpace(entry.InventoryId) ||
            string.IsNullOrWhiteSpace(entry.InstallLocation) ||
            !string.IsNullOrWhiteSpace(entry.MsiProductCode)) return false;
        try
        {
            string folder = Path.GetFullPath(entry.InstallLocation).TrimEnd('\\', '/');
            string file = Path.GetFullPath(executable);
            string expectedFolder = notepad ? "Notepad++" : "foobar2000";
            if (!Path.GetFileName(folder).Equals(expectedFolder, StringComparison.OrdinalIgnoreCase) ||
                !file.Equals(Path.Combine(folder, "uninstall.exe"), StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(originalFilename) &&
                    !originalFilename.Equals("uninstall.exe", StringComparison.OrdinalIgnoreCase))
                return false;
            // Permitir somente Program Files, nao AppData/Downloads/Temp,
            // e evitar caminhos compartilhados como a propria raiz.
            return new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
                .Where(ResidualReadSafety.IsLocalAbsolute)
                .Any(root => folder.StartsWith(
                    Path.GetFullPath(root).TrimEnd('\\') + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        { return false; }
    }
}

internal static class RegisteredUninstaller
{
    internal static PreparedUninstaller Prepare(UninstallRequest request, Func<string,string,bool> trustedSigner,
        bool allowUnsignedInLab = false, Func<string,string,string>? signatureIssue = null)
    {
        if(!UninstallBatch.BoundToProduct(request.Command,request.Original))
            throw new InvalidDataException("Comando não pertence ao produto selecionado.");
        string file=WindowsSystemTools.ResolveRegisteredUninstaller(request.Command.FileName);
        FileStream? held=null;
        bool unsignedTestException = false;
        try {
            if(request.Command.ProductCode is not null) {
                string systemMsi=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe");
                if(!file.Equals(systemMsi,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("MSI exige o serviço do Windows, nunca executável homônimo.");
            } else {
                held=InstallerFileGuard.OpenRead(file,allowDirectoryWrites:true);
                try { using(var image=new PEReader(held,PEStreamOptions.LeaveOpen)) {
                    var headers=image.PEHeaders;
                    if(headers.PEHeader is null || (headers.CoffHeader.Characteristics & System.Reflection.PortableExecutable.Characteristics.ExecutableImage)==0 ||
                        (headers.CoffHeader.Characteristics & System.Reflection.PortableExecutable.Characteristics.Dll)!=0)
                        throw new InvalidDataException("Desinstalador não é uma imagem executável PE.");
                }
                } catch (BadImageFormatException ex) { throw new InvalidDataException("Formato PE não verificável.", ex); }
                string original=FileVersionInfo.GetVersionInfo(file).OriginalFilename ?? "";
                if(UninstallCommand.IsHost(original) || original.Equals("msiexec.exe",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Interpretador renomeado não é desinstalador.");
                string publisher=request.Original.Publisher.Trim();
                var known=ApplicationIdentityCatalog.Resolve(request.Original.Name,publisher);
                var signers=(known?.Publishers ?? new[]{publisher}).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
                bool authenticated = signers.Length > 0 &&
                    signers.Any(s => trustedSigner(file, s));
                // Usuario selecionou PC FORTE no perfil de testes e confirmou
                // expressamente o app descartavel. Restrito ao caso
                // TRUST_E_NOSIGNATURE; assinatura expirada, invalida ou
                // outro editor NUNCA vira excecao.
                if (!authenticated)
                {
                    bool unsignedOnly = allowUnsignedInLab &&
                        LabUnsignedUninstallerPolicy.IsMatchingRegisteredLabProduct(request.Original, file, original) &&
                        ResidualReadSafety.IsUnlinkedDirectory(Path.GetDirectoryName(file) ?? "") &&
                        signers.Length > 0 && signatureIssue is not null &&
                        signers.All(s => signatureIssue(file, s) == "WinVerifyTrust=0x800B0100");
                    if (!unsignedOnly)
                        throw new System.Security.Cryptography.CryptographicException(
                            "Identidade do desinstalador não confirmada por assinatura confiável. " +
                            "Um produto fora da exceção explícita de laboratório segue bloqueado.");
                    unsignedTestException = true;
                }
            }
            var start=new ProcessStartInfo {
                FileName=file, Arguments=request.Command.Arguments, UseShellExecute=true,
                WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.System)
            };
            var result=new PreparedUninstaller(request,start,held,unsignedTestException); held=null; return result;
        } finally { held?.Dispose(); }
    }
}
