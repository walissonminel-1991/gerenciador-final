using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Evidencia do formato no arquivo, nunca executa /? nem scripts para descobrir flags.
internal static class InstallerEngineEvidence
{
    internal static string Detect(string path)
    {
        using var held = InstallerFileGuard.OpenRead(path, allowDirectoryWrites: true);
        using var pe = new PEReader(held, PEStreamOptions.LeaveOpen);
        if (pe.PEHeaders.PEHeader is null || (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.ExecutableImage) == 0 ||
            (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) != 0) return "unknown";
        long overlay = pe.PEHeaders.SectionHeaders.Max(s => (long)s.PointerToRawData + s.SizeOfRawData);
        if (overlay < 64 || overlay >= held.Length) return "unknown";
        held.Position = overlay;
        byte[] first = new byte[(int)Math.Min(2 * 1024 * 1024, held.Length - overlay)];
        held.ReadExactly(first);
        if (HasNsisHeader(first, held.Length)) return "nullsoft";
        var tail = new byte[(int)Math.Min(512 * 1024, held.Length)];
        held.Position = held.Length - tail.Length; held.ReadExactly(tail);
        return (first.AsSpan().IndexOf("Inno Setup Setup Data ("u8)>=0 || tail.AsSpan().IndexOf("Inno Setup Setup Data ("u8)>=0) &&
            (first.AsSpan().IndexOf("rDlPtS02"u8)>=0 || tail.AsSpan().IndexOf("rDlPtS02"u8)>=0) ? "inno" : "unknown";
    }
    internal static bool HasNsisHeader(ReadOnlySpan<byte> data, long fileLength)
    {
        ReadOnlySpan<byte> marker = [0xef,0xbe,0xad,0xde,0x4e,0x75,0x6c,0x6c,0x73,0x6f,0x66,0x74,0x49,0x6e,0x73,0x74];
        for (int i = 4; i + 24 <= data.Length; i++)
            if (data.Slice(i,16).SequenceEqual(marker) &&
                (BinaryPrimitives.ReadUInt32LittleEndian(data[(i-4)..]) & ~0x7fu) == 0 &&
                BinaryPrimitives.ReadUInt32LittleEndian(data[(i+16)..]) is > 0 and < 64 * 1024 * 1024 &&
                BinaryPrimitives.ReadUInt32LittleEndian(data[(i+20)..]) is > 0 and var following && following <= fileLength)
                return true;
        return false;
    }
    internal static bool HasInnoHeader(ReadOnlySpan<byte> data) =>
        data.IndexOf("Inno Setup Setup Data ("u8) >= 0 &&
        data.IndexOf("rDlPtS02"u8) >= 0;

    internal static string ProductKey(string text) => string.Concat(text.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Where(c=>char.IsLetterOrDigit(c)||c=='+'));
    internal static string FamilyName(string text)
    {
        string value=text.Trim();
        for(int pass=0;pass<3;pass++)value=Regex.Replace(value,
            @"\s*(?:\((?:x64|x86|32-bit x86|64-bit x64|64-bit|32-bit)\)|\s+v?\d+(?:\.\d+){1,3})\s*$", "", RegexOptions.IgnoreCase).Trim();
        return value;
    }

    internal static bool GenericNsisUninstaller(ProgramEntry app, ParsedUninstallCommand command)
    {
        if (command.ProductCode is not null || command.Arguments.Length != 0 || string.IsNullOrWhiteSpace(app.Publisher)) return false;
        try
        {
            if (Detect(command.FileName) != "nullsoft") return false;
            if(!TrustedFileSignature.TryGetSigner(command.FileName,out var uninstallerSigner,out _))return false;
            string root=Path.GetDirectoryName(command.FileName)!;
            using var pinned=InstallerFileGuard.PinDirectory(root,allowLeafWrites:true);
            var files=Directory.EnumerateFiles(root,"*.exe",new EnumerationOptions{RecurseSubdirectories=true,MaxRecursionDepth=3,
                IgnoreInaccessible=false,AttributesToSkip=FileAttributes.ReparsePoint}).Take(201).ToArray();
            if(files.Length>200)return false;
            var candidates=new List<string>();
            foreach(string file in files)
            {
                if(file.Equals(command.FileName,StringComparison.OrdinalIgnoreCase))continue;
                using var held=InstallerFileGuard.OpenRead(file,allowDirectoryWrites:true);
                var info=FileVersionInfo.GetVersionInfo(file);
                if(ProductKey(FamilyName(info.ProductName??""))!=ProductKey(FamilyName(app.Name)) ||
                    ProductKey(info.CompanyName??"")!=ProductKey(app.Publisher) ||
                    !Version.TryParse(info.FileVersion,out var actual) || !Version.TryParse(app.Version,out var expected) ||
                    actual.Major!=expected.Major || actual.Minor!=expected.Minor || actual.Build!=expected.Build)continue;
                candidates.Add(file);
            }
            return candidates.Count==1 && TrustedFileSignature.TryGetSigner(candidates[0],out var binarySigner,out _) &&
                binarySigner.Equals(uninstallerSigner,StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or BadImageFormatException or
            System.ComponentModel.Win32Exception or ArgumentException) { return false; }
    }
}
