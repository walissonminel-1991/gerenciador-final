using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class CleanupP0LayoutTests
{
    internal static void Run(MainForm form, V269Shell navigation)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var set = typeof(MainForm).GetMethod("SetCleanupRow", flags)!;
        string[] states =
        [
            "Prévia · 210 arquivos · 638,5 MiB",
            "Parcial · 8 arquivos · 145,4 MiB removidos · 3 preservados · 2 falhas/limites",
            "Serviços de atualização ativos",
            "Cancelada · 3 arquivos · 82,6 KiB removidos · 4 preservados",
            "Resultado inconclusivo (0x8000FFFF)"
        ];
        for (int i = 0; i < states.Length; i++) set.Invoke(form, [CleanupTarget.All[i], states[i]]);
        navigation.SelectPage(V269Page.Limpeza);
        form.MinimumSize = Size.Empty;
        form.MaximumSize = Size.Empty;
        Directory.CreateDirectory("LayoutCaptures");
        var report = (TableLayoutPanel)form.Controls.Find("CleanupCategoryReport", true).Single();
        foreach (var (name, width) in new[] { ("normal", 1280), ("compacta", 820) })
        {
            form.ClientSize = new Size(width, 590);
            Application.DoEvents();
            if (!report.Visible || report.ColumnCount != 2 || report.RowCount != 5 || report.Width < Math.Min(480, navigation.PageContent(V269Page.Limpeza)!.ClientSize.Width - 55))
                throw new InvalidOperationException($"P0 limpeza {name}: painel de categorias indisponível.");
            for (int row = 0; row < states.Length; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    var label = (Label)report.GetControlFromPosition(col, row)!;
                    int required = TextRenderer.MeasureText(label.Text, label.Font,
                        new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak |
                        TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height;
                    if (required > label.Height || !report.ClientRectangle.Contains(label.Bounds))
                        throw new InvalidOperationException($"P0 limpeza {name}: texto cortado, linha {row}, coluna {col}.");
                }
            }
            using var bitmap = new Bitmap(report.Width, report.Height);
            report.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save($"LayoutCaptures/Limpeza-P0-{name}.png");
            Console.WriteLine($"PASS P0-LIMPEZA-UI {name} {report.Width}x{report.Height}, 5 categorias legíveis");
        }
    }
}
