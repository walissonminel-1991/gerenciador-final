using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

/// <summary>
/// Botão de ação exclusivamente visual. O motor que o utilizará deverá
/// calcular e revalidar a elegibilidade; a cor jamais autoriza execução.
/// Não inclui eventos de instalação/desinstalação nem dados de inventário.
/// </summary>
internal sealed class V269ActionButton : Button
{
    internal readonly V269ActionKind Kind;
    internal bool Allowed;
    internal V269Theme Theme;

    internal V269ActionButton(V269ActionKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        Name = kind == V269ActionKind.Atualizar ? "V269ActionUpdate" : "V269ActionRemove";
        Size = new Size(32, 32);
        MinimumSize = new Size(32, 32);
        Margin = Padding.Empty;
        AccessibleRole = AccessibleRole.PushButton;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Text = string.Empty;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
        ApplyState(false, V269Theme.AzulGf);
    }

    internal void ApplyState(bool allowed, V269Theme theme)
    {
        // Padrão seguro: jamais habilitar ações por conta própria.
        Allowed = allowed;
        Theme = Enum.IsDefined(theme) ? theme : V269Theme.AzulGf;
        Enabled = allowed;
        TabStop = allowed;
        string name = Kind == V269ActionKind.Atualizar ? "Atualizar" : "Desinstalar";
        AccessibleName = name + (allowed ? ": pode" : ": não pode");
        AccessibleDescription = allowed ? "Ação disponível. Requer confirmação do motor." :
            "Ação indisponível.";
        BackColor = V269VisualTokens.ActionBackground(Theme, Kind, allowed);
        ForeColor = V269VisualTokens.ActionColor(Theme, Kind, allowed);
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        // Proteção adicional se alguma chamada programática chegar ao botão.
        if (Allowed && Enabled) base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(BackColor))
            g.FillRectangle(bg, ClientRectangle);
        using (var border = new Pen(V269VisualTokens.ActionBorder(Theme, Kind, Allowed), 1f))
            g.DrawRectangle(border, 0.5f, 0.5f, Math.Max(1, Width - 1.5f),
                Math.Max(1, Height - 1.5f));

        float x = (Width - 32f) / 2f;
        float y = (Height - 32f) / 2f;
        var original = g.Save();
        g.TranslateTransform(x, y);
        using (var pen = new Pen(ForeColor, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            if (Kind == V269ActionKind.Atualizar)
            {
                g.DrawArc(pen, 9, 9, 15, 15, -75, 292);
                g.DrawLines(pen, [new PointF(22, 7), new PointF(25, 12), new PointF(19, 12)]);
            }
            else
            {
                g.DrawRectangle(pen, 12, 13, 12, 13);
                g.DrawLine(pen, 10, 10, 26, 10);
                g.DrawLine(pen, 16, 8, 20, 8);
                g.DrawLine(pen, 16, 16, 16, 23);
                g.DrawLine(pen, 20, 16, 20, 23);
            }
        }
        g.Restore(original);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(g, new Rectangle(3, 3,
                Math.Max(1, Width - 6), Math.Max(1, Height - 6)));
    }
}
