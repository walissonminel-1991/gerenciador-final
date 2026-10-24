using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;

namespace GerenciadorAplicativos;

// Evidencia do formato no arquivo, nunca executa /? nem scripts para descobrir flags.
internal static class InstallerEngineEvidence
{
    internal static string InstalledKind(ParsedUninstallCommand command)=>command.ProductCode is not null?"msi":
        IsVerifiedInnoUninstaller(command)?"inno":Detect(command.FileName);
    internal static bool IsVerifiedInnoUninstaller(ParsedUninstallCommand command)
    {
        if(command.ProductCode is not null || command.Arguments.Length!=0 ||
            !Regex.IsMatch(Path.GetFileName(command.FileName),@"^unins[0-9]{3}\.exe$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return false;
        try
        {
            using var image=InstallerFileGuard.OpenRead(command.FileName,allowDirectoryWrites:true);
            if(!string.Equals(FileVersionInfo.GetVersionInfo(command.FileName).FileDescription,"Setup/Uninstall",StringComparison.OrdinalIgnoreCase))return false;
            using var log=InstallerFileGuard.OpenRead(Path.ChangeExtension(command.FileName,".dat"),allowDirectoryWrites:true);
            Span<byte> header=stackalloc byte[64];log.ReadExactly(header);
            string id=Encoding.ASCII.GetString(header).TrimEnd('\0');
            return id is "Inno Setup Uninstall Log (b)" or "Inno Setup Uninstall Log (b) 64-bit";
        }
        catch(Exception ex)when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException or
            InvalidOperationException or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception or System.Security.Cryptography.CryptographicException){return false;}
    }
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
        // Inno guarda a tabela no recurso do stub, antes do overlay. A tabela v2
        // usa 12 bytes binarios, nao a string historica "rDlPtS02".
        var prefix = new byte[(int)Math.Min(8 * 1024 * 1024, held.Length)];
        held.Position=0;held.ReadExactly(prefix);
        if(!TryInnoTable(prefix,held.Length,overlay,out long dataOffset))return "unknown";
        Span<byte> identity=stackalloc byte[64];held.Position=dataOffset;held.ReadExactly(identity);
        return identity.StartsWith("Inno Setup Setup Data ("u8) && identity.IndexOf((byte)0)>22 ? "inno" : "unknown";
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
    // TSetupLdrOffsetTable: formato e CRC definidos em Shared.Struct.pas / SetupLdr.dpr.
    internal static bool TryInnoTable(ReadOnlySpan<byte> data,long fileLength,long overlay,out long dataOffset)
    {
        dataOffset=0;
        ReadOnlySpan<byte> magic=[0x72,0x44,0x6c,0x50,0x74,0x53,0xcd,0xe6,0xd7,0x7b,0x0b,0x2a];
        for(int i=0;i+64<=data.Length;i++)
        {
            if(!data.Slice(i,12).SequenceEqual(magic))continue;
            var table=data.Slice(i,64);
            long total=BinaryPrimitives.ReadInt64LittleEndian(table[16..]),exe=BinaryPrimitives.ReadInt64LittleEndian(table[24..]);
            long offset0=BinaryPrimitives.ReadInt64LittleEndian(table[40..]),offset1=BinaryPrimitives.ReadInt64LittleEndian(table[48..]);
            if(BinaryPrimitives.ReadUInt32LittleEndian(table[12..])!=2 || total>fileLength || total<64 ||
                offset0<overlay || offset0>total-64 || exe<=offset0 || exe>=total || offset1<overlay || offset1>offset0 ||
                BinaryPrimitives.ReadUInt32LittleEndian(table[32..]) is 0 or >64*1024*1024 ||
                InnoTableCrc(table[..60])!=BinaryPrimitives.ReadUInt32LittleEndian(table[60..]))continue;
            dataOffset=offset0;return true;
        }
        return false;
    }
    internal static uint InnoTableCrc(ReadOnlySpan<byte> data)
    {
        uint crc=uint.MaxValue;
        foreach(byte value in data){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
        return ~crc;
    }

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
