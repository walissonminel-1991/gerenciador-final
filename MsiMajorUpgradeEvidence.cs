using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
namespace GerenciadorAplicativos;

// Only required for the new MSI catalog-identity route when ProductCode changes.
// A shared UpgradeCode alone does not prove that /i replaces the old product.
internal static class MsiMajorUpgradeEvidence
{
    internal sealed record Rule(string Code,string? Minimum,string? Maximum,string Languages,int Attributes,string Remove,string Property);
    internal static bool Covers(Rule row,MsiPackageEvidence.Metadata installed)
    {
        if(!Guid.TryParse(row.Code,out var family) || !Guid.TryParse(installed.UpgradeCode,out var expected) || family!=expected ||
            (row.Attributes & ~0x701)!=0 || (row.Attributes & 6)!=0 || row.Remove is not ("" or "ALL") ||
            !Regex.IsMatch(row.Property,@"^[A-Z_][A-Z0-9_]{0,71}$") || row.Minimum is null && row.Maximum is null ||
            !Version3(installed.Version,out var version))return false;
        if(row.Minimum is not null && (!Bound(row.Minimum,out var min) || version<min || version==min && (row.Attributes & 0x100)==0))return false;
        if(row.Maximum is not null && (!Bound(row.Maximum,out var max) || version>max || version==max && (row.Attributes & 0x200)==0))return false;
        if(row.Languages.Length==0)return true;
        string[] languages=row.Languages.Split(',');
        if(languages.Any(l=>!ushort.TryParse(l,out _)) || !ushort.TryParse(installed.Language,out var language))return false;
        bool contains=languages.Any(l=>ushort.Parse(l)==language);
        return (row.Attributes & 0x400)!=0?!contains:contains;
    }
    private static bool Bound(string text,out Version version)
    {
        if(text.Length>0)return Version3(text,out version);
        version=new(0,0,0);return true; // Empty is zero; NULL is unbounded.
    }
    private static bool Version3(string text,out Version version)
    {
        version=new Version(0,0,0);
        if(!VerifiedUpdaters.TryVersion(text,out var v) || v.Major>255 || v.Minor>255 || v.Build<0 || v.Build>65535)return false;
        version=new(v.Major,v.Minor,v.Build);return true;
    }
    internal static void Require(string file,MsiPackageEvidence.Metadata installed)
    {
        using var held=InstallerFileGuard.OpenRead(file,allowDirectoryWrites:true);
        if(MsiOpenDatabaseW(file,IntPtr.Zero,out uint database)!=0)throw new InvalidDataException("MSI de upgrade ilegivel.");
        try
        {
            List<string?[]> Query(string sql,int columns,int limit,bool nullBounds=false)
            {
                if(MsiDatabaseOpenViewW(database,sql,out uint view)!=0)throw new InvalidDataException("Contrato de major upgrade ausente.");
                try
                {
                    if(MsiViewExecute(view,0)!=0)throw new InvalidDataException("Major upgrade nao consultavel.");
                    var rows=new List<string?[]>();uint result;
                    while((result=MsiViewFetch(view,out uint record))==0)
                    {
                        try
                        {
                            if(rows.Count==limit)throw new InvalidDataException("Major upgrade excede limite.");
                            var row=new string?[columns];
                            for(uint field=1;field<=columns;field++)
                            {
                                var value=new StringBuilder(4096);uint length=4095;
                                if(MsiRecordGetStringW(record,field,value,ref length)!=0)throw new InvalidDataException("Major upgrade invalido.");
                                row[field-1]=nullBounds && field is 2 or 3 && MsiRecordIsNull(record,field)!=0?null:value.ToString();
                            }
                            rows.Add(row);
                        }
                        finally{MsiCloseHandle(record);}
                    }
                    if(result!=259)throw new InvalidDataException("Major upgrade incompleto.");
                    return rows;
                }
                finally{MsiCloseHandle(view);}
            }
            var rules=Query("SELECT `UpgradeCode`,`VersionMin`,`VersionMax`,`Language`,`Attributes`,`Remove`,`ActionProperty` FROM `Upgrade`",7,64,true)
                .Select(r=>new Rule(r[0]??"",r[1],r[2],r[3]??"",int.TryParse(r[4],out int attributes)?attributes:-1,r[5]??"",r[6]??"")).ToArray();
            var applicable=rules.Where(r=>Covers(r,installed)).ToArray();
            if(applicable.Length!=1 || rules.Any(r=>(r.Attributes & 2)==0 &&
                (!Guid.TryParse(r.Code,out var family) || !Guid.TryParse(installed.UpgradeCode,out var expected) || family!=expected)))
                throw new InvalidDataException("MSI nao comprova substituicao exclusiva da familia instalada.");
            var secure=Query("SELECT `Value` FROM `Property` WHERE `Property`='SecureCustomProperties'",1,1);
            if(secure.Count!=1 || !(secure[0][0]??"").Split(';').Contains(applicable[0].Property,StringComparer.Ordinal))
                throw new InvalidDataException("Identidade de major upgrade nao preservada entre contextos.");
            string[] actions=["FindRelatedProducts","RemoveExistingProducts"];
            var sequences=actions.Select(action=>Query("SELECT `Condition`,`Sequence` FROM `InstallExecuteSequence` WHERE `Action`='"+action+"'",2,1)).ToArray();
            if(sequences.Any(rows=>rows.Count!=1 || rows[0][0] is not ("" or "NOT Installed") || !int.TryParse(rows[0][1],out int n) || n<=0) ||
                int.Parse(sequences[0][0][1]??"0")>=int.Parse(sequences[1][0][1]??"0"))
                throw new InvalidDataException("Sequencia de major upgrade nao comprovada.");
        }
        finally{MsiCloseHandle(database);}
    }
#if GF_PROGRESSIVE
    internal static void AddSyntheticContract(string file,bool conditioned=false)
    {
        if(MsiOpenDatabaseW(file,new IntPtr(1),out uint database)!=0)throw new Exception("MSI fixture update failed");
        try
        {
            void Execute(string sql)
            {
                if(MsiDatabaseOpenViewW(database,sql,out uint view)!=0)throw new Exception("MSI fixture view failed");
                try{if(MsiViewExecute(view,0)!=0)throw new Exception("MSI fixture SQL failed");}finally{MsiCloseHandle(view);}
            }
            Execute("CREATE TABLE `Upgrade` (`UpgradeCode` CHAR(38) NOT NULL, `VersionMin` CHAR(20), `VersionMax` CHAR(20), `Language` CHAR(255), `Attributes` SHORT NOT NULL, `Remove` CHAR(255), `ActionProperty` CHAR(72) NOT NULL PRIMARY KEY `UpgradeCode`)");
            Execute("INSERT INTO `Upgrade` (`UpgradeCode`,`VersionMin`,`VersionMax`,`Attributes`,`ActionProperty`) VALUES ('{00000000-0000-0000-0000-000000000058}','1.0.0','3.0.0',256,'GF_OLD_PRODUCTS')");
            Execute("INSERT INTO `Property` (`Property`,`Value`) VALUES ('SecureCustomProperties','GF_OLD_PRODUCTS')");
            Execute("CREATE TABLE `InstallExecuteSequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)");
            Execute("INSERT INTO `InstallExecuteSequence` (`Action`,`Sequence`) VALUES ('FindRelatedProducts',100)");
            Execute("INSERT INTO `InstallExecuteSequence` (`Action`,`Condition`,`Sequence`) VALUES ('RemoveExistingProducts','"+(conditioned?"UNKNOWN_PROPERTY":"NOT Installed")+"',200)");
            if(MsiDatabaseCommit(database)!=0)throw new Exception("MSI fixture commit failed");
        }
        finally{MsiCloseHandle(database);}
    }
    [DllImport("msi.dll")]private static extern uint MsiDatabaseCommit(uint database);
#endif
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiOpenDatabaseW(string path,IntPtr persist,out uint database);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiDatabaseOpenViewW(uint database,string query,out uint view);
    [DllImport("msi.dll")]private static extern uint MsiViewExecute(uint view,uint record);
    [DllImport("msi.dll")]private static extern uint MsiViewFetch(uint view,out uint record);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiRecordGetStringW(uint record,uint field,StringBuilder value,ref uint length);
    [DllImport("msi.dll")]private static extern int MsiRecordIsNull(uint record,uint field);
    [DllImport("msi.dll")]private static extern uint MsiCloseHandle(uint handle);
}
