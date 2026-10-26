using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Aceite remoto especifico, versionado e bloqueado sem receptor/aviso aprovados.
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));

        var notice = new Label
        {
            Dock = DockStyle.Fill, ForeColor = ForeColor,
            TextAlign = ContentAlignment.TopLeft,
            Text = "Ajude a melhorar o GERENCIADOR FINAL.\n\n" +
                   "Se aceitar, guardaremos neste computador apenas a versão do GF " +
                   "e contagens de falhas técnicas. Não recolhemos nomes de " +
                   "programas, arquivos, senhas nem dados pessoais.\n\n" +
                   "Nada será enviado pela internet. Participar é opcional e " +
                   "não limita o uso do programa.\n\n" +
                   "Você pode simular sem aceitar a coleta."
        };
#if GF_BETA
        notice.Text = TelemetryConsentPolicy.RemoteNotice;
        // O aviso ultrapassa uma altura fixa em notebook/DPI alto: permitir rolagem e copia.
        var scrollingNotice = new TextBox
        {
            Name = "PrivacyBetaNotice", Multiline = true, ReadOnly = true,
            ScrollBars = ScrollBars.Vertical, WordWrap = true, TabStop = true,
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            BackColor = BackColor, ForeColor = ForeColor, Text = notice.Text
        };
        root.Controls.Add(scrollingNotice, 0, 0);
#else
        root.Controls.Add(notice, 0, 0);
#endif

        var preview = new DarkLogView
        {
            Name = "PrivacyLocalPreview",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 2)
        };
        preview.UseReadableFont(Font);
        preview.SetText(recorder.ExplainLocalCollection(DiagnosticTrace.VersionLabel));
        root.Controls.Add(preview, 0, 1);

        // Duas linhas: os quatro controles nunca concorrem pela mesma largura
        // em notebooks ou sob ampliacao de texto/DPI.
        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 3, 0, 1)
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        buttons.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var accept = new ModernButton
        {
            Text = "Ativar coleta local", DialogResult = DialogResult.Yes,
            Size = new Size(115, 34), BackColor = AppServices.Blue,
            ForeColor = Color.White, Margin = new Padding(6, 0, 0, 0)
        };
#if GF_BETA
        accept.Text = "Aceito envio automático";
        accept.Enabled = BetaTelemetryUploader.IsConfigured;
        if (!accept.Enabled) scrollingNotice.Text += "\n\nSem receptor configurado: aceitar e enviar ficam bloqueados.";
#endif
        var decline = new ModernButton
        {
            Text = "Não participar / Desativar", DialogResult = DialogResult.No,
            Size = new Size(165, 34), BackColor = AppServices.Gray,
            ForeColor = Color.White, Margin = new Padding(6, 0, 0, 0)
        };
        var cancel = new ModernButton
        {
            Text = "Voltar sem alterar", DialogResult = DialogResult.Cancel,
            Size = new Size(90, 34), BackColor = AppServices.Dark,
            ForeColor = Color.White, Margin = new Padding(6, 0, 0, 0)
        };
        var simulate = new ModernButton
        {
            Name = "PrivacyOfflineSimulation",
            Text = "Simular sem enviar",
            Size = new Size(155, 34),
            BackColor = AppServices.Dark,
            ForeColor = Color.White,
            Margin = new Padding(6, 0, 0, 0)
        };
        simulate.Click += (_, _) =>
        {
            // Nao chama coleta, nao altera consentimento, nao grava em disco nem usa rede.
            preview.SetText(AnonymousTelemetry.SimulateContributionWithoutSending(
                DiagnosticTrace.VersionLabel));
        };
        // Linha superior: apenas conhecer a experiencia ou sair.
        // Linha inferior: decidir sobre a coleta local, com escolhas equivalentes.
        foreach (var button in new[] { accept, decline, cancel, simulate })
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4, 2, 4, 2);
        }
        buttons.Controls.Add(simulate, 0, 0);
        buttons.Controls.Add(cancel, 1, 0);
        buttons.Controls.Add(accept, 0, 1);
        buttons.Controls.Add(decline, 1, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        CancelButton = cancel;
        // Nao definir AcceptButton. Enter nao pode aprovar coleta por acidente.
    }

    internal void FitPrivacyArea(Size available, int dpi)
    {
        float scale = Math.Max(1, dpi) / 96f;
        MinimumSize = Size.Empty;
        MaximumSize = Size.Empty;
        Size border = new(Width - ClientSize.Width, Height - ClientSize.Height);
        ClientSize = new Size(Math.Max(1, Math.Min((int)(586 * scale), available.Width - border.Width - 24)),
            Math.Max(1, Math.Min((int)(505 * scale), available.Height - border.Height - 24)));
        var root = Controls.OfType<TableLayoutPanel>().Single();
        float buttons = 90 * scale;
        root.RowStyles[2].Height = buttons;
        root.RowStyles[0].Height = Math.Min(207 * scale,
            Math.Max(48 * scale, ClientSize.Height - root.Padding.Vertical - buttons - 50 * scale));
        root.PerformLayout();
        MinimumSize = Size;
        MaximumSize = Size;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitPrivacyArea(Screen.FromControl(Owner ?? this).WorkingArea.Size, DeviceDpi);
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
    private void RefreshPrivacyStatus()
    {
        if (_privacyStatusButton is null || _privacyStatusButton.IsDisposed) return;
        var state = AnonymousTelemetryContext.Current?.Consent ?? TelemetryConsent.NotAsked;
        _privacyStatusButton.Text = PrivacyConsentPresentation.ButtonLabel(state);
        _privacyStatusButton.AccessibleName = "Privacidade: " + _privacyStatusButton.Text;
        _privacyStatusButton.AccessibleDescription = PrivacyConsentPresentation.Explanation(state);
        _toolTips.SetToolTip(_privacyStatusButton,
            _privacyStatusButton.AccessibleDescription);
        _privacyStatusButton.BackColor = state == TelemetryConsent.LocalOnly
            ? Color.FromArgb(43, 66, 89) : AppServices.Gray;
        _privacyStatusButton.Invalidate();
    }

    private void OfferAnonymousDiagnosticConsent(bool firstRun = false)
    {
        var collector = AnonymousTelemetryContext.Current;
        if (collector is null || IsDisposed ||
            (firstRun && collector.Consent is TelemetryConsent.Declined or TelemetryConsent.RemoteBeta))
            return;
        using var dialog = new TelemetryPrivacyDialog(collector);
        var decision = dialog.ShowDialog(this);
        if (decision is not (DialogResult.Yes or DialogResult.No)) return;

        var choice = decision == DialogResult.Yes
            ? TelemetryConsent.RemoteBeta : TelemetryConsent.Declined;
        bool saved = collector.SetConsent(choice);
        RefreshPrivacyStatus();
        if (!saved)
        {
            Status(choice == TelemetryConsent.Declined
                ? "Coleta interrompida nesta sessão, mas não foi possível salvar a recusa ou apagar todos os dados locais. Revise a preferência ao abrir novamente."
                : "Não foi possível salvar a preferência. A escolha anterior foi mantida.");
            return;
        }
        Status(choice == TelemetryConsent.RemoteBeta
            ? "BETA: envio de contagens autorizado ao fechar; revogável na bandeja."
            : "Diagnóstico colaborativo desativado; dados pendentes apagados.");
    }
}
