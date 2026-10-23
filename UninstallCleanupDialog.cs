using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed class UninstallCleanupDialog : Form
{
    internal UninstallCleanupDialog(IReadOnlyList<IntegratedResidualPlan> plans)
    {
        Text = "Concluir desinstalação";
        Name = "UninstallCleanupConfirmation";
        BackColor = AppServices.Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 460);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(18), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.Controls.Add(new Label { Text = "Desinstalação concluída.", Dock = DockStyle.Fill,
            ForeColor = AppServices.Blue, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        root.Controls.Add(new Label { Text = "Foram encontrados resíduos do aplicativo removido.\nDeseja excluir permanentemente os resíduos identificados?",
            Dock = DockStyle.Fill, ForeColor = Color.White }, 0, 1);
        var summary = new DarkLogView { Name = "UninstallCleanupSummary", Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 8) };
        summary.SetText(Describe(plans));
        root.Controls.Add(summary, 0, 2);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Padding = new Padding(0, 10, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        var no = new ModernButton { Name = "UninstallCleanupNo", Text = "NÃO", Dock = DockStyle.Fill,
            DialogResult = DialogResult.No, BackColor = AppServices.Gray, ForeColor = Color.White,
            Margin = new Padding(6, 0, 0, 0), AllowMultilineText = false };
        var yes = new ModernButton { Name = "UninstallCleanupYes", Text = "SIM", Dock = DockStyle.Fill,
            DialogResult = DialogResult.Yes, BackColor = AppServices.Blue, ForeColor = Color.White,
            Margin = new Padding(6, 0, 0, 0), AllowMultilineText = false };
        buttons.Controls.Add(no, 1, 0); buttons.Controls.Add(yes, 2, 0);
        root.Controls.Add(buttons, 0, 3);
        Controls.Add(root);
        AcceptButton = no;
        CancelButton = no;
        Shown += (_, _) => no.Focus();
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int dark = 1; if (NativeMethods.DwmSetWindowAttribute(Handle, 20, ref dark, 4) != 0)
                _ = NativeMethods.DwmSetWindowAttribute(Handle, 19, ref dark, 4); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Bounds = FitBounds(Bounds, Screen.FromControl(Owner ?? this).WorkingArea);
    }
    internal static Rectangle FitBounds(Rectangle desired, Rectangle area)
    {
        int width = Math.Min(desired.Width, Math.Max(1, area.Width - 24));
        int height = Math.Min(desired.Height, Math.Max(1, area.Height - 24));
        return new Rectangle(Math.Clamp(desired.X, area.Left + 12, Math.Max(area.Left + 12, area.Right - width - 12)),
            Math.Clamp(desired.Y, area.Top + 12, Math.Max(area.Top + 12, area.Bottom - height - 12)), width, height);
    }
    internal static string Describe(IReadOnlyList<IntegratedResidualPlan> plans)
    {
        var text = new StringBuilder();
        foreach (var plan in plans)
        {
            text.AppendLine(plan.Context.App.Name.Replace('\r', ' ').Replace('\n', ' '));
            text.AppendLine($"{plan.Files.Count} arquivo(s), {plan.Directories.Count} pasta(s) e {plan.Keys.Count} chave(s) do Registro.");
            foreach (var file in plan.Files) text.AppendLine("  Arquivo: " + file.Path);
            foreach (var folder in plan.Directories) text.AppendLine("  Pasta: " + folder.Path);
            foreach (var key in plan.Keys) text.AppendLine("  Registro: " +
                (key.Hive == Microsoft.Win32.RegistryHive.LocalMachine ? "HKLM" : "HKCU") +
                (key.View == Microsoft.Win32.RegistryView.Registry32 ? " (32 bits)\\" : " (64 bits)\\") + key.Subkey);
            text.AppendLine($"Preservados: {plan.Preserved} local(is) com dados pessoais, compartilhados ou não reconhecidos.");
            text.AppendLine();
        }
        text.Append("SIM exclui os itens acima permanentemente. NÃO mantém todos os resíduos.\n" +
            "Documentos pessoais, objetos compartilhados e itens não verificáveis serão preservados.");
        return text.ToString();
    }
    internal static bool Confirm(Form owner, IReadOnlyList<IntegratedResidualPlan> plans)
    {
        using var dialog = new UninstallCleanupDialog(plans);
        return dialog.ShowDialog(owner) == DialogResult.Yes;
    }
}
