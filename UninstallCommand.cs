using System.Text.RegularExpressions;
namespace GerenciadorAplicativos;

internal sealed record ParsedUninstallCommand(string FileName, string Arguments, string? ProductCode);

// Mesma gramática para diagnóstico e execução. Nunca chama shell para interpretar.
internal static class UninstallCommand
{
    private const string GuidPattern = @"\{[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\}";
    private static readonly Regex Msi = new(@"^/[ix]\s*(?:""(?<product>"+GuidPattern+@")""|(?<product>"+GuidPattern+@"))(?<options>(?:\s+(?:/quiet|/passive|/qn|/qb[!+\-]?|/norestart|REBOOT=ReallySuppress))*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
    { "cmd", "powershell", "pwsh", "wscript", "cscript", "mshta", "rundll32", "regsvr32", "regasm", "reg", "sc", "schtasks", "wmic", "forfiles", "msbuild", "installutil", "conhost", "explorer", "control", "python", "pythonw", "python3", "node", "nodejs", "java", "javaw", "bash", "sh", "wsl", "dotnet", "ruby", "perl", "php", "csi", "certutil", "bitsadmin", "curl", "ftp", "hh", "odbcconf", "iexpress" };
    internal static bool IsHost(string filename) => Hosts.Contains(Path.GetFileNameWithoutExtension(filename.Replace('\\','/'))) ||
        Regex.IsMatch(Path.GetFileNameWithoutExtension(filename.Replace('\\','/')), @"^python(?:w|[0-9]+(?:\.[0-9]+)*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static bool TryParse(string? command, out ParsedUninstallCommand parsed)
    {
        parsed = new("","",null);
        string raw=command?.Trim() ?? "";
        if(raw.Length is 0 or >32767 || raw.Any(char.IsControl)) return false;
        string file, arguments;
        if(raw[0]=='"') {
            int end=raw.IndexOf('"',1);
            if(end<=1 || (end+1<raw.Length && !char.IsWhiteSpace(raw[end+1]))) return false;
            file=raw[1..end]; arguments=raw[(end+1)..].Trim();
        } else {
            var match=Regex.Match(raw,@"^.+?\.exe(?=\s|$)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            int end=match.Success ? match.Length : raw.IndexOf(' ');
            if(end<0) end=raw.Length;
            file=raw[..end]; arguments=raw[end..].Trim();
            if (!arguments.StartsWith('/') && !arguments.StartsWith('-') &&
                !arguments.Contains('"') && Regex.IsMatch(arguments,@"\.exe(?=\s|$)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
                return false; // múltiplos términos possíveis em caminho sem aspas

        }
        file=Environment.ExpandEnvironmentVariables(file);
        if(file.Contains('"') || IsHost(file) || !SafeArguments(arguments)) return false;
        string name=Path.GetFileName(file.Replace('\\','/'));
        bool msi=name.Equals("msiexec.exe",StringComparison.OrdinalIgnoreCase) || name.Equals("msiexec",StringComparison.OrdinalIgnoreCase);
        bool bareMsi = msi && file.Equals(name,StringComparison.OrdinalIgnoreCase);
        if (!bareMsi && (!Regex.IsMatch(file,@"^[a-z]:[\\/]",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant) ||
            file.AsSpan(2).Contains(':') || file.Contains('%') || file.Contains('*') || file.Contains('?') ||
            !file.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) ||
            file.Split('\\','/').Any(p=>p is "." or ".."))) return false;
        if (msi && !bareMsi && (!OperatingSystem.IsWindows() ||
            !Path.GetFullPath(file).Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe"),StringComparison.OrdinalIgnoreCase))) return false;
        if(msi) {
            var match=Msi.Match(arguments);
            if(!match.Success || !Guid.TryParse(match.Groups["product"].Value,out var guid) || guid==Guid.Empty) return false;
            string options=match.Groups["options"].Value;
            // /quiet e /passive podem reiniciar sem perguntar quando falta
            // uma politica de reboot. O GF nunca autoriza esse reinicio.
            if(!options.Contains("/norestart",StringComparison.OrdinalIgnoreCase)) options+=" /norestart";
            parsed=new(file,"/X"+guid.ToString("B").ToUpperInvariant()+options,guid.ToString("B").ToUpperInvariant());
            return true;
        }
        parsed=new(file,arguments,null); return true;
    }
    private static bool SafeArguments(string arguments)
    {
        bool quoted=false; int slashes=0;
        foreach(char ch in arguments) {
            if(ch=='"' && slashes%2==0) quoted=!quoted;
            if(!quoted && ch is '&' or '|' or '<' or '>') return false;
            slashes=ch=='\\' ? slashes+1 : 0;
        }
        return !quoted;
    }
}
