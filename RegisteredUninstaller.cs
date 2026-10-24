using System.Diagnostics;
using System.Reflection.PortableExecutable;
namespace GerenciadorAplicativos;

// Preparado ANTES da confirmação. Lease permanece até Process.Start retornar;
// depois é solto para permitir que o próprio fornecedor remova seus arquivos.
internal sealed class PreparedUninstaller(UninstallRequest request, ProcessStartInfo start, IDisposable? lease,
    bool unsignedWarning = false, bool publisherWarning = false, bool trustWarning = false,
    TimeSpan? waitLimit = null) : IDisposable
{
    private IDisposable? held=lease;
    private int attempted;
    internal UninstallRequest Request => request;
    internal bool UnsignedWarning => unsignedWarning;
    internal bool PublisherWarning => publisherWarning;
    internal bool TrustWarning => trustWarning;
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
            deadline.CancelAfter(waitLimit ?? TimeSpan.FromMinutes(3));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch(OperationCanceledException) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
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

// Autenticidade nao e requisito para o usuario remover programa comum.
// Falta de assinatura e advertencia; assinatura quebrada/editor divergente
// permanecem erros da INTENCAO executavel (nao bloqueio do aplicativo).
internal static class UninstallerSignaturePolicy
{
    internal static bool IsUnsigned(string? winTrustIssue) =>
        string.Equals(winTrustIssue, "WinVerifyTrust=0x800B0100",
            StringComparison.OrdinalIgnoreCase);
    // 'editor=' e produzido somente apos WinVerifyTrust devolver SUCESSO.
    internal static bool IsVerifiedPublisherMismatch(string? issue) =>
        issue is { Length: > 7 } && issue.StartsWith("editor=", StringComparison.Ordinal);
    // Erros que indicam adulteracao, revogacao ou ausencia da assinatura.
    // O usuario conserva a opcao de remover pelo Windows, nunca via EXE comprometido.
    internal static bool IsKnownCompromise(string? issue) =>
        issue is "WinVerifyTrust=0x80096010" or "WinVerifyTrust=0x80096004" or
            "WinVerifyTrust=0x800B010C" or "WinVerifyTrust=0x800B0111";
    internal static bool IsUntrustedButNotProvenTampered(string? issue) =>
        // Somente falhas de cadeia/validade conhecidas podem ser apresentadas
        // como advertencia. Erro desconhecido nao equivale a EXE integro.
        issue is "WinVerifyTrust=0x800B0109" or "WinVerifyTrust=0x800B010A" or
            "WinVerifyTrust=0x800B0101" or "WinVerifyTrust=0x800B010E" or
            "WinVerifyTrust=0x80092012" or "WinVerifyTrust=0x80092013";
}

internal static class RegisteredUninstaller
{
    internal static PreparedUninstaller Prepare(UninstallRequest request, Func<string,string,bool> trustedSigner,
        Func<string,string,string>? signatureIssue = null)
    {
        if(!UninstallBatch.BoundToProduct(request.Command,request.Original))
            throw new InvalidDataException("Comando não pertence ao produto selecionado.");
        string file=WindowsSystemTools.ResolveRegisteredUninstaller(request.Command.FileName);
        FileStream? held=null;
        bool unsignedWarning = false;
        bool publisherWarning = false;
        bool trustWarning = false;
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
                // Muitos desinstaladores EXE Win32 legitimos nao sao assinados.
                // O programa registrado continua sob revisao de caminho,
                // gramática, formato PE, hosts/atalhos proibidos e pinning.
                // Somente AUSENCIA confirmada de assinatura segue para
                // consentimento explicito; assinatura adulterada ou editor
                // divergente nunca recebe o tratamento de "sem assinatura".
                if (!authenticated)
                {
                    var signersToCheck = signers.Length > 0 ? signers : [""];
                    var issues = signersToCheck.Select(s => signatureIssue is null
                        ? "" : signatureIssue(file, s)).ToArray();
                    bool unsigned = issues.All(UninstallerSignaturePolicy.IsUnsigned);
                    bool publisherMismatch = issues.All(UninstallerSignaturePolicy.IsVerifiedPublisherMismatch);
                    bool untrusted = issues.All(UninstallerSignaturePolicy.IsUntrustedButNotProvenTampered);
                    if (!unsigned && !publisherMismatch && !untrusted)
                        throw new System.Security.Cryptography.CryptographicException(
                            "O desinstalador apresenta assinatura adulterada/revogada ou dados invalidos. " +
                            "Por seguranca, o executavel nao sera iniciado pelo GF. " +
                            "A desinstalacao ainda pode ser feita pelos Aplicativos do Windows.");
                    unsignedWarning = unsigned;
                    publisherWarning = publisherMismatch;
                    trustWarning = untrusted;
                }
            }
            var start=new ProcessStartInfo {
                FileName=file, Arguments=request.Command.Arguments, UseShellExecute=true,
                WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.System)
            };
            var result=new PreparedUninstaller(request,start,held,unsignedWarning,publisherWarning,trustWarning); held=null; return result;
        } finally { held?.Dispose(); }
    }
}
