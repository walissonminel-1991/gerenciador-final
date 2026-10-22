using System.Diagnostics;
using System.Reflection.PortableExecutable;
namespace GerenciadorAplicativos;

// Preparado ANTES da confirmação. Lease permanece até Process.Start retornar;
// depois é solto para permitir que o próprio fornecedor remova seus arquivos.
internal sealed class PreparedUninstaller(UninstallRequest request, ProcessStartInfo start, IDisposable? lease) : IDisposable
{
    private IDisposable? held=lease;
    private int attempted;
    internal UninstallRequest Request => request;
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

internal static class RegisteredUninstaller
{
    internal static PreparedUninstaller Prepare(UninstallRequest request, Func<string,string,bool> trustedSigner)
    {
        if(!UninstallBatch.BoundToProduct(request.Command,request.Original))
            throw new InvalidDataException("Comando não pertence ao produto selecionado.");
        string file=WindowsSystemTools.ResolveRegisteredUninstaller(request.Command.FileName);
        FileStream? held=null;
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
                if(signers.Length==0 || !signers.Any(s=>trustedSigner(file,s)))
                    throw new System.Security.Cryptography.CryptographicException(
                        "Identidade do desinstalador não confirmada por assinatura confiável do fabricante. Use o fabricante ou Aplicativos do Windows; arquivo sem assinatura não é prova de malware.");
            }
            var start=new ProcessStartInfo {
                FileName=file, Arguments=request.Command.Arguments, UseShellExecute=true,
                WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.System)
            };
            var result=new PreparedUninstaller(request,start,held); held=null; return result;
        } finally { held?.Dispose(); }
    }
}
