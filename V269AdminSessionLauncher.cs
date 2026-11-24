using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Elevação única, sem registro de inicialização, tarefa, serviço ou senha.
// Chamado exclusivamente após a confirmação explícita e fechamento normal.
internal sealed record V269AdminLaunchTicket(string Path,string Sha256);

internal static class V269AdminSessionLauncher
{
    internal static bool IsEligible(string? path) =>
        !string.IsNullOrWhiteSpace(path)&&Path.IsPathFullyQualified(path)&&
        string.Equals(Path.GetFileName(path),"GerenciadorFinal.exe",StringComparison.OrdinalIgnoreCase)&&
        !path.Contains('"')&&!path.Contains('\r')&&!path.Contains('\n')&&File.Exists(path);

    internal static ProcessStartInfo Build(string path)
    {
        if(!IsEligible(path))throw new InvalidDataException("Executável atual não elegível.");
        return new ProcessStartInfo
        {
            FileName=path,WorkingDirectory=Path.GetDirectoryName(path)!,
            UseShellExecute=true,Verb="runas"
        };
    }

    private static FileStream OpenStable(string path)
    {
        if(!IsEligible(path))throw new InvalidDataException("Executável atual não elegível.");
        // Não seguir uma troca de link no caminho que será passado ao UAC.
        for(string? item=path;item is not null;item=Path.GetDirectoryName(item))
        {
            if((File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("Caminho com redirecionamento não elegível.");
            if(string.Equals(item,Path.GetPathRoot(item),StringComparison.OrdinalIgnoreCase))break;
        }
        return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
    }
    internal static V269AdminLaunchTicket Prepare(string path)
    {
        using var stream=OpenStable(path);
        return new(Path.GetFullPath(path),Convert.ToHexString(SHA256.HashData(stream)));
    }
    internal static bool Verify(V269AdminLaunchTicket ticket)
    {
        try
        {
            using var stream=OpenStable(ticket.Path);
            return Convert.ToHexString(SHA256.HashData(stream))==ticket.Sha256;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            ArgumentException or System.Security.SecurityException){return false;}
    }
    internal static void RequestOnce(V269AdminLaunchTicket ticket)
    {
        try
        {
            if(!string.Equals(Environment.ProcessPath,ticket.Path,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Origem alterada.");
            using var stream=OpenStable(ticket.Path);
            if(Convert.ToHexString(SHA256.HashData(stream))!=ticket.Sha256)
                throw new InvalidDataException("Executável alterado após confirmação.");
            // Manter o arquivo aberto sem compartilhamento de escrita/exclusão
            // até a solicitação de ShellExecute. Não confundir hash com assinatura.
            using var process=Process.Start(Build(ticket.Path));
        }
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or IOException or
            UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Security.SecurityException)
        {
            MessageBox.Show("A sessão administrativa não foi iniciada. Abra o GF normalmente quando desejar.",
                "Modo Administrador",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
    }
}
