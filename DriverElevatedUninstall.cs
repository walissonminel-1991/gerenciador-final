using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// A remoção em processo elevado NÃO confia em inventários passados por CLI.
// O Windows solicita UAC e o próprio trabalhador recaptura, revalida,
// confirma o alvo e executa o motor OEM existente com backup obrigatório.
internal static class DriverElevatedUninstall
{
    internal const string Command = "--gf-driver-uninstall";
    internal sealed record LaunchResult(bool Started, bool Declined, string Message);

    private static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static bool TryDecode(string encoded, int limit, out string value)
    {
        value = "";
        try
        {
            if (encoded.Length is < 4 or > 6000) return false;
            byte[] bytes = Convert.FromBase64String(encoded);
            value = new UTF8Encoding(false, true).GetString(bytes);
            return value.Length is > 0 and <= 1024 && value.Length <= limit &&
                value == value.Trim() && !value.Any(char.IsControl);
        }
        catch (Exception ex) when (ex is FormatException or DecoderFallbackException)
        {
            return false;
        }
    }

    internal static async Task<LaunchResult> RequestAsync(
        string deviceId, string infName)
    {
        try
        {
            string exe = Environment.ProcessPath ?? "";
            if (!OperatingSystem.IsWindows() ||
                !Path.GetFileName(exe).Equals("GerenciadorFinal.exe",
                    StringComparison.OrdinalIgnoreCase) || !File.Exists(exe))
                return new(false, false, "Executável do GF indisponível para solicitar UAC.");
            using var child = Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = true, Verb = "runas",
                Arguments = "", // ArgumentList mantém identidade e INF como dados.
                ArgumentList = { Command, Encode(deviceId), Encode(infName) }
            });
            if (child is null)
                return new(false, false, "O Windows não iniciou a operação administrativa.");
            await child.WaitForExitAsync();
            // O filho apresenta seu resultado; o inventário da UI será relido.
            return new(true, false,
                "Operação administrativa encerrada. Consultando os drivers novamente.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(false, true, "Autorização UAC cancelada. Nenhum comando de remoção iniciado.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(false, false,
                "Não foi possível solicitar UAC (" + ex.GetType().Name + "). Nenhuma remoção solicitada.");
        }
    }

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 3 ||
            !TryDecode(args[1], 1024, out var deviceId) ||
            !TryDecode(args[2], 80, out var infName) ||
            !System.Text.RegularExpressions.Regex.IsMatch(infName,
                @"^oem[0-9]{1,7}\.inf$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            return 2;
        if (!OperatingSystem.IsWindows() || !DriverSilentUninstall.IsElevated())
            return 3;
        ApplicationConfiguration.Initialize();
        DriverInventoryResult snapshot;
        try
        {
            snapshot = await Task.Run(() =>
                DriverStoreAudit.CaptureWithPackages(CancellationToken.None));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MessageBox.Show("Não foi possível consultar o dispositivo novamente. Nada removido.",
                "GF — remoção bloqueada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 4;
        }
        var matches = snapshot.Items.Where(item =>
            string.Equals(item.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.InfName, infName, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1 ||
            snapshot.Items.Count(item => string.Equals(item.DeviceId, deviceId,
                StringComparison.OrdinalIgnoreCase)) != 1)
        {
            MessageBox.Show("Dispositivo ou INF mudou após solicitar UAC. Nada removido.",
                "GF — remoção bloqueada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return 5;
        }
        var item = matches[0];
        var policy = DriverSilentUninstall.Evaluate(snapshot, item);
        if (!policy.Allowed)
        {
            MessageBox.Show(policy.Reason, "GF — driver protegido",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 6;
        }
        var question = "Confirme a remoção de UM driver OEM pelo Windows:" +
            Environment.NewLine + Environment.NewLine +
            "Dispositivo: " + item.DeviceName + Environment.NewLine +
            "ID: " + item.DeviceId + Environment.NewLine +
            "INF: " + item.InfName + Environment.NewLine +
            "Fornecedor: " + item.Provider + Environment.NewLine + Environment.NewLine +
            "O backup verificável é obrigatório. Nenhum pacote crítico ou compartilhado será removido.";
        if (MessageBox.Show(question, "GF — confirmar remoção administrativa",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2)
            != DialogResult.Yes)
            return 0;

        // Janela de progresso simples, sem operações concorrentes ou ToolTips.
        using var form = new Form
        {
            Text = "GF — desinstalando driver OEM", Width = 590, Height = 155,
            StartPosition = FormStartPosition.CenterScreen, ShowInTaskbar = true,
            ControlBox = false, MinimizeBox = false, MaximizeBox = false,
            BackColor = System.Drawing.Color.FromArgb(26, 31, 39),
            ForeColor = System.Drawing.Color.White
        };
        var status = new Label
        {
            Dock = DockStyle.Fill, Padding = new Padding(18),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Text = "Revalidando dispositivo e exportando backup..."
        };
        form.Controls.Add(status);
        DriverSilentUninstallOutcome? outcome = null;
        form.Shown += async (_, _) =>
        {
            try
            {
                outcome = await DriverSilentUninstall.Execute(snapshot, item, true,
                    () => !form.IsDisposed,
                    message =>
                    {
                        try
                        {
                            if (!form.IsDisposed)
                                form.BeginInvoke((Action)(() => status.Text = message));
                        }
                        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
                    });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                outcome = new(DriverSilentUninstallState.VerificationUncertain,
                    "Resultado não confirmado (" + ex.GetType().Name +
                    "). Verifique o estado do INF antes de qualquer repetição.");
            }
            finally { if (!form.IsDisposed) form.Close(); }
        };
        form.ShowDialog();
        if (outcome is null) return 7;
        MessageBox.Show(outcome.Message + (outcome.BackupPath is null ? "" :
            Environment.NewLine + Environment.NewLine + "Backup: " + outcome.BackupPath),
            "GF — " + outcome.State, MessageBoxButtons.OK,
            outcome.State == DriverSilentUninstallState.Verified
                ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        return outcome.State == DriverSilentUninstallState.Verified ? 0 : 8;
    }
}
