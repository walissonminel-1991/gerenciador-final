using System.Runtime.InteropServices;
using System.Text;
namespace GerenciadorAplicativos;

internal static class MsiPackageEvidence
{
    internal sealed record Metadata(string Name,string Publisher,string Version,string ProductCode,string UpgradeCode,string Architecture,bool Services,string Scope="unknown");
    internal static Metadata Read(string file)
    {
        using var pinned=InstallerFileGuard.OpenRead(file,allowDirectoryWrites:true);
        if(MsiOpenDatabaseW(file,IntPtr.Zero,out uint database)!=0)throw new InvalidDataException("MSI nao pode ser lido.");
        try
        {
            string Property(string key)
            {
                // Nomes de propriedades sao constantes locais, nao entradas remotas.
                if(MsiDatabaseOpenViewW(database,"SELECT `Value` FROM `Property` WHERE `Property`='"+key+"'",out uint view)!=0)throw new InvalidDataException("Propriedade MSI indisponivel.");
                try
                {
                    if(MsiViewExecute(view,0)!=0)throw new InvalidDataException("Consulta MSI recusada.");
                    if(MsiViewFetch(view,out uint row)!=0)return "";
                    try{var text=new StringBuilder(4096);uint length=4095;if(MsiRecordGetStringW(row,1,text,ref length)!=0)throw new InvalidDataException("Propriedade MSI excede limite.");return text.ToString();}
                    finally{MsiCloseHandle(row);}
                }
                finally{MsiCloseHandle(view);}
            }
            bool services=false;
            foreach(string table in new[]{"ServiceInstall","MsiDriverPackages"})
                if(MsiDatabaseIsTablePersistentW(database,table)==1)services=true;
            if(MsiGetSummaryInformationW(database,null,0,out uint summary)!=0)throw new InvalidDataException("Arquitetura MSI indisponivel.");
            string architecture;
            try
            {
                var text=new StringBuilder(1024);uint length=1023;
                if(MsiSummaryInfoGetPropertyW(summary,7,out _,out _,IntPtr.Zero,text,ref length)!=0)throw new InvalidDataException("Template MSI invalido.");
                architecture=text.ToString().Split(';')[0] switch{"x64"=>"x64","Intel"=>"x86",_=>"unknown"};
            }
            finally{MsiCloseHandle(summary);}
            string allUsers=Property("ALLUSERS");
            string scope=allUsers=="1"?"machine":allUsers.Length==0?"user":"unknown";
            return new(Property("ProductName"),Property("Manufacturer"),Property("ProductVersion"),Property("ProductCode"),Property("UpgradeCode"),architecture,services,scope);
        }
        finally{MsiCloseHandle(database);}
    }
    internal static Metadata Installed(string code)
    {
        if(!Guid.TryParse(code,out _))throw new InvalidDataException("ProductCode MSI invalido.");
        var path=new StringBuilder(4096);uint length=4095;
        if(MsiGetProductInfoW(code,"LocalPackage",path,ref length)!=0)throw new InvalidDataException("Cache MSI instalado nao pode ser confirmado.");
        return Read(path.ToString());
    }
    internal static void RequireUpgrade(AutomaticInstaller installer,AutomaticAppInstallation app,string file)
    {
        Validate(Installed(app.ProductCode),Read(file),installer,app);
    }
    internal static void Validate(Metadata before,Metadata after,AutomaticInstaller installer,AutomaticAppInstallation app)
    {
        if(!before.ProductCode.Equals(app.ProductCode,StringComparison.OrdinalIgnoreCase) ||
            Matching.NormalizeName(before.Name)!=Matching.NormalizeName(app.Name) ||
            Matching.NormalizePublisher(before.Publisher)!=Matching.NormalizePublisher(app.Publisher) ||
            !VerifiedUpdaters.TryVersion(before.Version,out var previousVersion) || !VerifiedUpdaters.TryVersion(app.Version,out var recordedVersion) ||previousVersion!=recordedVersion ||
            before.Scope!=app.Scope || after.Scope!=app.Scope || before.Services || after.Services || before.Architecture!=app.Architecture || after.Architecture!=app.Architecture ||
            !Guid.TryParse(before.UpgradeCode,out var oldCode) || oldCode==Guid.Empty || !Guid.TryParse(after.UpgradeCode,out var newCode) || oldCode!=newCode ||
            !after.ProductCode.Equals(installer.ProductCode,StringComparison.OrdinalIgnoreCase) ||
            Matching.NormalizeName(after.Name)!=Matching.NormalizeName(app.Name) ||
            Matching.NormalizePublisher(after.Publisher)!=Matching.NormalizePublisher(app.Publisher) ||
            !VerifiedUpdaters.TryVersion(after.Version,out var version) || !VerifiedUpdaters.TryVersion(installer.Version,out var target) ||version!=target)
            throw new InvalidDataException("MSI diverge do produto, familia, arquitetura ou servicos autorizados.");
    }
#if GF_PROGRESSIVE
    internal static void CreateSynthetic(string file)
    {
        if(MsiOpenDatabaseW(file,new IntPtr(3),out uint database)!=0)throw new Exception("Fixture MSI create failed");
        try
        {
            void Execute(string sql)
            {
                if(MsiDatabaseOpenViewW(database,sql,out uint view)!=0)throw new Exception("Fixture MSI view failed");
                try{if(MsiViewExecute(view,0)!=0)throw new Exception("Fixture MSI execute failed");}finally{MsiCloseHandle(view);}
            }
            Execute("CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`)");
            foreach(var pair in new[]{("ProductName","GF Synthetic MSI"),("Manufacturer","GF Tests"),("ProductVersion","2.0.0"),
                ("ProductCode","{00000000-0000-0000-0000-000000000059}"),("UpgradeCode","{00000000-0000-0000-0000-000000000058}"),("ALLUSERS","1")})
                Execute("INSERT INTO `Property` (`Property`,`Value`) VALUES ('"+pair.Item1+"','"+pair.Item2+"')");
            if(MsiGetSummaryInformationW(database,null,20,out uint summary)!=0)throw new Exception("Fixture MSI summary failed");
            try{if(MsiSummaryInfoSetPropertyW(summary,7,30,0,IntPtr.Zero,"x64;1033")!=0 || MsiSummaryInfoPersist(summary)!=0)throw new Exception("Fixture MSI template failed");}
            finally{MsiCloseHandle(summary);}
            if(MsiDatabaseCommit(database)!=0)throw new Exception("Fixture MSI commit failed");
        }
        finally{MsiCloseHandle(database);}
    }
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiSummaryInfoSetPropertyW(uint summary,uint property,uint type,int value,IntPtr time,string text);
    [DllImport("msi.dll")]private static extern uint MsiSummaryInfoPersist(uint summary);
    [DllImport("msi.dll")]private static extern uint MsiDatabaseCommit(uint database);
#endif
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiOpenDatabaseW(string path,IntPtr persist,out uint database);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiDatabaseOpenViewW(uint database,string query,out uint view);
    [DllImport("msi.dll")]private static extern uint MsiViewExecute(uint view,uint record);
    [DllImport("msi.dll")]private static extern uint MsiViewFetch(uint view,out uint record);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiRecordGetStringW(uint record,uint field,StringBuilder value,ref uint length);
    [DllImport("msi.dll")]private static extern uint MsiCloseHandle(uint handle);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern int MsiDatabaseIsTablePersistentW(uint database,string table);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiGetProductInfoW(string product,string property,StringBuilder value,ref uint length);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiGetSummaryInformationW(uint database,string? path,uint updateCount,out uint summary);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiSummaryInfoGetPropertyW(uint summary,uint property,out uint type,out int value,IntPtr time,StringBuilder text,ref uint length);
}
