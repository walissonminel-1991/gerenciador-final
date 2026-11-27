using System.Drawing;
using System.Windows.Forms;

namespace GfToolProbe;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ProbeForm());
    }
}

internal sealed class ProbeForm : Form
{
    private readonly Label _status;
    private readonly ProgressBar _progress;
    private readonly Timer _timer;
    private int _ticks;

    public ProbeForm()
    {
        Name = "GfToolProbe";
        Text = "GF Tool Probe";
        AccessibleName = "GF Tool Probe";
        AccessibleRole = AccessibleRole.Application;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 360);
        MinimumSize = ClientSize;
        MaximumSize = ClientSize;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var layout = new TableLayoutPanel
        {
            Name = "ProbeLayout",
            AccessibleName = "GF Tool Probe Layout",
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var title = new Label
        {
            Name = "ProbeTitle",
            AccessibleName = "GF Tool Probe Title",
            Dock = DockStyle.Fill,
            Text = "GF engineering tool probe",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
        };
        layout.Controls.Add(title, 0, 0);

        _progress = new ProgressBar
        {
            Name = "ProbeProgress",
            AccessibleName = "GF Tool Probe Progress",
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
        };
        layout.Controls.Add(_progress, 0, 1);

        var sample = new Button
        {
            Name = "ProbeSampleButton",
            AccessibleName = "Run GF Tool Probe Sample",
            Dock = DockStyle.Fill,
            Text = "Run sample",
        };
        sample.Click += (_, _) =>
        {
            _ticks++;
            _status.Text = $"Samples: {_ticks}";
        };
        layout.Controls.Add(sample, 0, 2);

        _status = new Label
        {
            Name = "ProbeStatus",
            AccessibleName = "GF Tool Probe Status",
            Dock = DockStyle.Fill,
            Text = "Samples: 0",
            TextAlign = ContentAlignment.MiddleLeft,
        };
        layout.Controls.Add(_status, 0, 3);

        Controls.Add(layout);

        _timer = new Timer { Interval = 250 };
        _timer.Tick += (_, _) =>
        {
            _progress.Value = (_progress.Value + 1) % 101;
        };
        _timer.Start();
        FormClosed += (_, _) => _timer.Dispose();
    }
}
