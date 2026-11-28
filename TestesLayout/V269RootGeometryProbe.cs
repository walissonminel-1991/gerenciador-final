using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

// Exclusivamente TestesLayout: prova focal da candidata raiz isolada.
internal static class V269RootGeometryProbe
{
    private enum Failure { None, ColumnStyle, RootBounds, ColumnWidth, ShellBounds }

    private static Failure Inspect(TableLayoutPanel root, V269Shell shell, Size client, Rectangle shellBounds)
    {
        if (root.ColumnStyles.Count != 1 || root.ColumnStyles[0].SizeType != SizeType.Percent ||
            Math.Abs(root.ColumnStyles[0].Width - 100f) >= .01f)
            return Failure.ColumnStyle;
        if (root.Bounds != new Rectangle(Point.Empty, client)) return Failure.RootBounds;
        var columns = root.GetColumnWidths();
        var display = root.DisplayRectangle;
        if (columns.Length != 1 || columns[0] != display.Width) return Failure.ColumnWidth;
        if (!ReferenceEquals(shell.Parent, root) || !root.ClientRectangle.Contains(shellBounds) ||
            shellBounds.Width <= 0 || shellBounds.Height <= 0 ||
            shellBounds.Left != display.Left + shell.Margin.Left ||
            shellBounds.Right != display.Right - shell.Margin.Right)
            return Failure.ShellBounds;
        return Failure.None;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("L12-ROOT: " + reason);
    }

    // A mesma guarda no percurso integral, antes de alterar viewports.
    internal static void VerifyCurrent(MainForm form, V269Shell shell)
    {
        var root = form.Controls.OfType<TableLayoutPanel>().Single();
        var failure = Inspect(root, shell, form.ClientSize, shell.Bounds);
        Require(failure == Failure.None,
            $"janela inicial dpi={form.DeviceDpi} causa={failure} root={root.Bounds} display={root.DisplayRectangle} shell={shell.Bounds}");
        Console.WriteLine($"PASS L12 root initial dpi={form.DeviceDpi} column={root.GetColumnWidths()[0]} shell={shell.Bounds}");
    }

    internal static void Run()
    {
        using var form = new MainForm(startServices: false);
        form.Show();
        Application.DoEvents();
        var root = form.Controls.OfType<TableLayoutPanel>().Single();
        var shell = form.Controls.Find("V269Shell", true).OfType<V269Shell>().Single();
        shell.SelectPage(V269Page.Configuracoes);
        Application.DoEvents();
        var page = (V269SettingsPage)shell.PageContent(V269Page.Configuracoes)!;
        var range = page.Appearance.Controls.Find("V269UiScale", true).OfType<V269SettingsRange>().Single();
        int original = range.Value;
        void Positive(int scale)
        {
            var screen = Screen.FromControl(form);
            var expected = StartupWindowSizing.Fit(screen.WorkingArea, screen.Bounds.Height, scale);
            Require(shell.InterfaceScalePercent == scale && form.Size == expected.Size &&
                form.MinimumSize == expected.Size && form.MaximumSize == expected.Size &&
                screen.WorkingArea.Contains(form.Bounds) && !form.MaximizeBox,
                $"janela fixa/escala {scale}, esperado={expected.Size}, observado={form.Size}");
            var failure = Inspect(root, shell, form.ClientSize, shell.Bounds);
            Require(failure == Failure.None,
                $"escala={scale} dpi={form.DeviceDpi} causa={failure} root={root.Bounds} display={root.DisplayRectangle} shell={shell.Bounds} margin={shell.Margin}");
            Console.WriteLine($"L12-ROOT scale={scale} dpi={form.DeviceDpi} monitor={screen.Bounds.Size} work={screen.WorkingArea.Size} form={form.Size} client={form.ClientSize} column={root.GetColumnWidths()[0]} shell={shell.Bounds}");
        }
        try
        {
            foreach (int scale in new[] { 90, 100, 110 })
            {
                range.Value = scale; // binder real; nao simula tamanho de monitor
                Application.DoEvents();
                Positive(scale);
            }
            // Controle negativo real do contrato: ausencia da declaracao explicita,
            // como no HEAD anterior. Nao toca arquivos nem usa manutencao.
            var style = root.ColumnStyles[0];
            root.ColumnStyles.Clear();
            try
            {
                Require(Inspect(root, shell, form.ClientSize, shell.Bounds) == Failure.ColumnStyle,
                    "oraculo aceitou coluna sem estilo explicito");
            }
            finally { root.ColumnStyles.Add(style); }
            Application.DoEvents();
            Positive(110);
            // Controle negativo do oraculo de geometria, por observacao alterada:
            // um pixel a menos de largura nao pode passar apenas por estar contido.
            var clipped = shell.Bounds;
            clipped.Width--;
            Require(Inspect(root, shell, form.ClientSize, clipped) == Failure.ShellBounds,
                "oraculo aceitou shell contido mas estreito");
            Console.WriteLine("L12-ROOT NEGATIVE column-style=PASS shell-width=PASS");
        }
        finally
        {
            range.Value = original;
            Application.DoEvents();
        }
        Positive(original);
        Console.WriteLine("PASS L12 root geometry: candidate; actual monitor/DPI; scales 90,100,110; two negative controls; no maintenance.");
    }
}
