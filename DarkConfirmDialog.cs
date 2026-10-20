using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Confirmações com a mesma paleta do programa, sem caixas brancas do MessageBox.
// UAC do Windows e diálogos de terceiros continuam sendo exibidos normalmente.
internal sealed class DarkConfirmDialog : Form
{
    private readonly Label _message;

    private DarkConfirmDialog(string question, string caption)
    {
        Text = caption;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 10F);
        BackColor = AppServices.Background;
        ForeColor = Color.FromArgb(234, 242, 249);
        ClientSize = new Size(548, 214);
        MinimumSize = Size;
        MaximumSize = new Size(600, 650);
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Padding = new Padding(19, 19, 19, 15),
            BackColor = AppServices.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            BackColor = Color.FromArgb(30, 39, 50),
            Padding = new Padding(14, 13, 14, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 7));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Panel
        {
            Dock = DockStyle.Fill, BackColor = AppServices.Blue,
            Margin = new Padding(0, 4, 0, 4)
        }, 0, 0);

        _message = new Label
        {
            Text = question, Dock = DockStyle.Fill, AutoSize = false,
            ForeColor = Color.FromArgb(234, 242, 249),
            Font = new Font("Segoe UI", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 4, 0)
        };
        panel.Controls.Add(_message, 1, 0);
        root.Controls.Add(panel, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = AppServices.Background,
            Padding = new Padding(0, 10, 0, 0)
        };
        var yes = new ModernButton
        {
            Text = "Sim", DialogResult = DialogResult.Yes,
            Size = new Size(117, 34),
            BackColor = AppServices.Blue, ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.3F, FontStyle.Bold),
            Margin = new Padding(8, 0, 0, 0)
        };
        var no = new ModernButton
        {
            Text = "Não", DialogResult = DialogResult.No,
            Size = new Size(117, 34),
            BackColor = AppServices.Gray, ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.3F, FontStyle.Bold),
            Margin = new Padding(8, 0, 0, 0)
        };
        actions.Controls.Add(yes);
        actions.Controls.Add(no);
        root.Controls.Add(actions, 0, 1);
        Controls.Add(root);
        AcceptButton = yes;
        CancelButton = no;

        int wrappedHeight = TextRenderer.MeasureText(question, _message.Font, new Size(440, int.MaxValue),
            TextFormatFlags.WordBreak).Height;
        ClientSize = new Size(548, Math.Clamp(wrappedHeight + 126, 214, 570));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int enabled = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref enabled, 4) != 0)
                DwmSetWindowAttribute(Handle, 19, ref enabled, 4);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    internal static DialogResult Confirm(IWin32Window owner, string question, string title = "Confirmar ação")
    {
        using var dialog = new DarkConfirmDialog(question, title);
        return dialog.ShowDialog(owner);
    }
}
