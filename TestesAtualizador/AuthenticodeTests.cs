using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GerenciadorAplicativos;

internal static class AuthenticodeTests
{
    internal static async Task RunArchiveAsync(Action<bool, string> verify)
    {
        // Simula o intervalo entre a extracao e a abertura protegida, sem MSI
        // executavel: um pacote trocado deve falhar mesmo que tenha outro editor.
        using var archiveBytes = new MemoryStream();
        byte[] original = System.Text.Encoding.UTF8.GetBytes("conteudo esperado do pacote autenticado");
        using (var writing = new ZipArchive(archiveBytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = writing.CreateEntry("redist/GameInputRedist.msi").Open();
            entry.Write(original);
        }
        archiveBytes.Position = 0;
        using var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Read);
        var source = archive.GetEntry("redist/GameInputRedist.msi")!;
        string file = Path.Combine(Path.GetTempPath(), "GF-Archive-" + Guid.NewGuid().ToString("N") + ".fixture");
        try
        {
            source.ExtractToFile(file);
            using (var extracted = File.OpenRead(file))
            {
                string digest = await VerifiedUpdaters.RequireArchiveDigestAsync(extracted, source);
                verify(digest == Convert.ToHexString(SHA256.HashData(original)),
                    "recibo do arquivo extraido corresponde ao conteudo do ZIP autenticado");
            }
            File.WriteAllText(file, "outro pacote colocado antes da abertura protegida");
            bool replaced = false;
            using (var extracted = File.OpenRead(file))
            {
                try { await VerifiedUpdaters.RequireArchiveDigestAsync(extracted, source); }
                catch (CryptographicException) { replaced = true; }
            }
            verify(replaced, "substituicao apos extracao nao emite recibo de pacote autenticado");
            bool canceled = false;
            using (var extracted = File.OpenRead(file))
            {
                try { await VerifiedUpdaters.RequireArchiveDigestAsync(extracted, source, new CancellationToken(true)); }
                catch (OperationCanceledException) { canceled = true; }
            }
            verify(canceled, "cancelamento da comparacao de pacote nao vira aprovacao");
        }
        finally { File.Delete(file); }
    }

    internal static void Run(Action<bool, string> verify)
    {
        // Certificado efemero apenas em memoria: nao altera repositorios de
        // confianca do Windows e nao assina/executa nenhum instalador.
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Editor de teste GF", key,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var synthetic = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1));
        using (var decoded = VerifiedUpdaters.LoadSignerCertificate(synthetic.Handle))
            verify(decoded.Thumbprint == synthetic.Thumbprint && !decoded.HasPrivateKey &&
                decoded.GetNameInfo(X509NameType.SimpleName, false) == "Editor de teste GF",
                "DER do contexto nativo preserva identidade sem importar chave privada");
        bool refused = false;
        try { using var invalid = VerifiedUpdaters.LoadSignerCertificate(IntPtr.Zero); }
        catch (CryptographicException) { refused = true; }
        verify(refused, "contexto ausente nao produz assinante aprovado");

        IntPtr oversized = Marshal.AllocHGlobal(Marshal.SizeOf<VerifiedUpdaters.CertificateContext>());
        try
        {
            Marshal.StructureToPtr(new VerifiedUpdaters.CertificateContext
            {
                Encoded = new IntPtr(1), Length = 1024 * 1024 + 1
            }, oversized, false);
            refused = false;
            try { using var invalid = VerifiedUpdaters.LoadSignerCertificate(oversized); }
            catch (CryptographicException) { refused = true; }
            verify(refused, "certificado acima do limite e recusado antes de copiar bytes");
        }
        finally { Marshal.FreeHGlobal(oversized); }

        string fixture = Path.Combine(Path.GetTempPath(), "GF-Unsigned-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            File.WriteAllText(fixture, "fixture nao executavel e sem assinatura");
            verify(!VerifiedUpdaters.VerifyAuthenticode(fixture, "Microsoft Windows", out var issue) &&
                issue.StartsWith("WinVerifyTrust=", StringComparison.Ordinal),
                "arquivo sem assinatura e recusado pelo Windows antes da leitura de editor");
        }
        finally { File.Delete(fixture); }

        // Exercita o caminho real do provedor de confianca em arquivo do sistema,
        // somente leitura. Falhas de revogacao/confianca devem falhar o teste.
        string signedSystemFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        verify(VerifiedUpdaters.VerifyAuthenticode(signedSystemFile, "Microsoft Windows", out var trustedIssue),
            "assinatura Windows valida fornece seu proprio editor: " + trustedIssue);
        verify(!VerifiedUpdaters.VerifyAuthenticode(signedSystemFile, "Editor de teste GF", out var mismatch) &&
            mismatch == "editor=Microsoft Windows",
            "assinatura confiavel nao autoriza editor diferente do exigido pela receita");
    }
}
