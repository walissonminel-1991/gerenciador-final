using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace GerenciadorAplicativos;

// Consulta WinVerifyTrust; nao importa chaves nem executa o arquivo.
internal static class TrustedFileSignature
{
    internal static bool TryGetSigner(string path, out string signer, out string issue)
    {
        signer = "";
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
            signer = name;
            return true;
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

}
