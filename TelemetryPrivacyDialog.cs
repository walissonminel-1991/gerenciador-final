using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// A primeira fase nao oferece envio; somente registra preferencia e relatorio
// local resumido. Uma futura ativacao de rede exigira novo consentimento.
internal sealed class TelemetryPrivacyDialog : Form
{
    internal TelemetryPrivacyDialog(AnonymousTelemetry recorder)
    {
        Text = "Privacidade e diagnóstico colaborativo";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(586, 505);
        MinimumSize = Size;
        MaximumSize = Size;
        BackColor = AppServices.Background;
        ForeColor = Color.FromArgb(235, 240, 246);
        Font = new Font("Segoe UI", 9.5F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(16, 13, 16, 12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 207));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var notice = new Label
        {
            Dock = DockStyle.Fill, ForeColor = ForeColor,
            TextAlign = ContentAlignment.TopLeft,
            Text = "O GERENCIADOR FINAL está em desenvolvimento. Você pode colaborar " +
                   "com o diagnóstico de bugs sem compartilhar dados pessoais.\n\n" +
                   "Esta etapa registra somente a versão do GF, o encerramento e " +
                   "contagens por categoria de erro. Não recolhe nomes de programas, " +
                   "arquivos, comandos, IP, caminhos, IDs de dispositivos ou dados de uso pessoal.\n\n" +
                   "IMPORTANTE: ainda não há envio automático. Tudo ficará local; " +
                   "quando o envio seguro estiver disponível, solicitaremos novo consentimento."
        };
        root.Controls.Add(notice, 0, 0);

        var preview = new DarkLogView { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 2) };
        preview.SetText("PRÉVIA EXATA DOS CAMPOS ARMAZENADOS LOCALMENTE:\n" +
            recorder.Preview(DiagnosticTrace.VersionLabel, normalExit: true));
        root.Controls.Add(preview, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Padding = new Padding(0, 5, 0, 0)
        };
        var accept = new ModernButton
        {
            Text = "Aceitar", DialogResult = DialogResult.Yes,
            Size = new Size(125, 34), BackColor = AppServices.Blue,
            ForeColor = Color.White, Margin = new Padding(6, 0, 0, 0)
        };
        var decline = new ModernButton
        {
            Text = "Recusar / Desativar", DialogResult = DialogResult.No,
            Size = new Size(169, 34), BackColor = AppServices.Gray,
            ForeColor = Color.White, Margin = new Padding(6, 0, 0, 0)
        };
        var cancel = new ModernButton
        {
            Text = "Voltar", DialogResult = DialogResult.Cancel,
            Size = new Size(110, 34), BackColor = AppServices.Dark,
            ForeColor = Color.White, Margin = new Padding(6, 0, 0, 0)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(decline);
        buttons.Controls.Add(accept);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        CancelButton = cancel;
        // Nao definir AcceptButton. Enter nao pode aprovar coleta por acidente.
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int dark = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref dark, 4) != 0)
                DwmSetWindowAttribute(Handle, 19, ref dark, 4);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);
}

internal sealed partial class MainForm
{
    // Piloto GitHub-only: envio PUBLICO exige duas acoes conscientes:
    // confirmar no GF e clicar em "Submit new issue" no proprio GitHub.
    private void ShareAnonymousDiagnosticToGitHub()
    {
        var collector = AnonymousTelemetryContext.Current;
        if (collector is null || IsDisposed) return;
        if (collector.Consent != TelemetryConsent.LocalOnly)
        {
            OfferAnonymousDiagnosticConsent();
            if (collector.Consent != TelemetryConsent.LocalOnly) return;
        }

        string preview = collector.Preview(DiagnosticTrace.VersionLabel, normalExit: false);
        Uri? url = GitHubDiagnosticShare.CreateReviewUrl(preview);
        if (url is null)
        {
            Status("Ainda não há ocorrências técnicas elegíveis para compartilhar com o GitHub.");
            return;
        }

        if (DarkConfirmDialog.Confirm(this,
            "O GitHub abrirá um formulário com os dados técnicos resumidos. " +
            "Se você publicar, a Issue será PÚBLICA e vinculada ao seu perfil GitHub.\n\n" +
            "Nenhum arquivo completo de diagnóstico será anexado. Você poderá " +
            "revisar ou desistir antes de publicar. Deseja abrir o formulário?",
            "Revisar diagnóstico público no GitHub") != DialogResult.Yes)
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri)
            {
                UseShellExecute = true
            });
            Status("Formulário GitHub aberto. Nada foi enviado automaticamente.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or InvalidOperationException or System.Security.SecurityException)
        {
            Status("Não foi possível abrir o navegador. O diagnóstico não foi enviado.");
        }
    }

    private void OfferAnonymousDiagnosticConsent(bool firstRun = false)
    {
        var collector = AnonymousTelemetryContext.Current;
        if (collector is null || IsDisposed ||
            (firstRun && collector.Consent != TelemetryConsent.NotAsked))
            return;
        using var dialog = new TelemetryPrivacyDialog(collector);
        var decision = dialog.ShowDialog(this);
        if (decision is not (DialogResult.Yes or DialogResult.No)) return;

        var choice = decision == DialogResult.Yes
            ? TelemetryConsent.LocalOnly : TelemetryConsent.Declined;
        bool saved = collector.SetConsent(choice);
        if (!saved)
        {
            Log("Privacidade: não foi possível registrar a preferência. A coleta continua desativada.");
            return;
        }
        Status(choice == TelemetryConsent.LocalOnly
            ? "Diagnóstico local voluntário ativo. Nenhum dado é enviado pela internet."
            : "Diagnóstico colaborativo desativado; dados pendentes apagados.");
    }
}
