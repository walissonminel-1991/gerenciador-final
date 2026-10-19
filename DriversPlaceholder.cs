using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Somente apresentacao. Nenhum inventario, alteracao, instalacao,
// desinstalacao ou consulta de drivers e disparado por esta pagina.
internal sealed partial class MainForm
{
    private Control BuildDriversPlaceholder()
    {
        var host = new Panel
        {
            Name = "DriversPlaceholder",
            Dock = DockStyle.Fill,
            BackColor = AppServices.Background,
            Margin = Padding.Empty
        };
        var viewport = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = AppServices.Background
        };
        host.Controls.Add(viewport);

        var title = new Label
        {
            Text = "DRIVERS",
            AutoSize = false,
            ForeColor = Ink,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var intro = new Label
        {
            Text = "Área reservada para gerenciamento de drivers em uma versão futura.",
            AutoSize = false,
            ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var warning = new Label
        {
            Text = "Prévia visual: nenhuma instalação, remoção ou alteração de drivers está disponível nesta aba.",
            AutoSize = false,
            ForeColor = Color.FromArgb(170, 205, 229),
            TextAlign = ContentAlignment.MiddleLeft
        };
        viewport.Controls.Add(title);
        viewport.Controls.Add(intro);
        viewport.Controls.Add(warning);

        var grid = new TableLayoutPanel
        {
            Name = "DriversPreviewCards",
            ColumnCount = 3,
            RowCount = 1,
            BackColor = AppServices.Background,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        viewport.Controls.Add(grid);

        var entries = new[]
        {
            (Title: "Inventário de drivers",
             Description: "Planejado: identificar fabricantes, versões e dispositivos com segurança."),
            (Title: "Atualização e instalação",
             Description: "Planejado: verificar pacotes oficiais antes de qualquer instalação."),
            (Title: "Histórico e reversão",
             Description: "Planejado: examinar alterações e opções de recuperação de drivers.")
        };
        var cards = new Panel[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var card = new Panel
            {
                Name = "DriversPreviewCard" + i,
                BackColor = PanelBackground,
                Padding = new Padding(11, 9, 11, 8),
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 3, 4, 4)
            };
            card.Controls.Add(new Label
            {
                Text = entries[i].Description,
                Dock = DockStyle.Fill,
                ForeColor = Muted,
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Segoe UI", 9F),
                Margin = Padding.Empty
            });
            card.Controls.Add(new Label
            {
                Text = entries[i].Title,
                Dock = DockStyle.Top,
                Height = 27,
                ForeColor = Ink,
                Font = new Font("Segoe UI", 9.4F, FontStyle.Bold),
                Margin = Padding.Empty,
                TextAlign = ContentAlignment.MiddleLeft
            });
            cards[i] = card;
            grid.Controls.Add(card);
        }

        int lastWidth = -1;
        int lastDpi = -1;
        void Reflow()
        {
            if (viewport.IsDisposed || viewport.ClientSize.Width < 1) return;
            int scaleDpi = Math.Max(96, DeviceDpi);
            int available = Math.Max(120, viewport.ClientSize.Width - 18);
            if (lastWidth == available && lastDpi == scaleDpi) return;
            lastWidth = available;
            lastDpi = scaleDpi;
            double scale = scaleDpi / 96d;
            int columns = available >= (int)(850 * scale) ? 3 :
                available >= (int)(520 * scale) ? 2 : 1;
            int cardHeight = (int)Math.Ceiling(112 * scale);
            int rows = (cards.Length + columns - 1) / columns;
            int top = (int)Math.Ceiling(91 * scale);

            grid.SuspendLayout();
            try
            {
                grid.ColumnStyles.Clear();
                grid.RowStyles.Clear();
                grid.ColumnCount = columns;
                grid.RowCount = rows;
                for (int i = 0; i < columns; i++)
                    grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
                for (int i = 0; i < rows; i++)
                    grid.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
                for (int i = 0; i < cards.Length; i++)
                    grid.SetCellPosition(cards[i],
                        new TableLayoutPanelCellPosition(i % columns, i / columns));
                grid.Bounds = new Rectangle(9, top, available, rows * cardHeight);
            }
            finally { grid.ResumeLayout(true); }

            title.Bounds = new Rectangle(13, 5, Math.Max(1, available - 12), (int)(30 * scale));
            intro.Bounds = new Rectangle(13, (int)(37 * scale),
                Math.Max(1, available - 12), (int)(42 * scale));
            warning.Bounds = new Rectangle(13, grid.Bottom + (int)(12 * scale),
                Math.Max(1, available - 12), (int)(45 * scale));
            viewport.AutoScrollMinSize = new Size(0, warning.Bottom + (int)(12 * scale));
        }
        viewport.SizeChanged += (_, _) => Reflow();
        host.HandleCreated += (_, _) => Reflow();
        return host;
    }
}
