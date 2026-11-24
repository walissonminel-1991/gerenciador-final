using System.Diagnostics;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Elevação única, sem registro de inicialização, tarefa, serviço ou senha.
// Chamado exclusivamente após a confirmação explícita e fechamento normal.
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

    internal static void RequestOnce()
    {
        try
        {
            using var process=Process.Start(Build(Environment.ProcessPath??""));
        }
        catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or IOException or
            UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            MessageBox.Show("A sessão administrativa não foi iniciada. Abra o GF normalmente quando desejar.",
                "Modo Administrador",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
    }
}
