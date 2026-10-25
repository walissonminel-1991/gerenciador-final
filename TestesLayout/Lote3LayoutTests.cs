using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class Lote3LayoutTests
{
    internal static void Run()
    {
        using var form = new Form { Width = 650, Height = 350 };
        var log = new DarkLogView { Dock = DockStyle.Fill };
        form.Controls.Add(log);
        form.Show();
        for (int i = 0; i < 6000; i++) log.AppendText($"Mensagem {i:0000} " + new string('x', 110) + "\r\n");
        string text = log.RenderedText;
        if (text.Length > BoundedLogText.DefaultCapacity || !text.StartsWith(BoundedLogText.Omission) ||
            !text.Contains("Mensagem 5999") || text.Contains("Mensagem 0000"))
            throw new Exception("Prévia de log cresce sem teto ou perdeu as mensagens recentes.");
        Console.WriteLine($"L3_UI_LOG retidos_caracteres={text.Length} teto={BoundedLogText.DefaultCapacity} mensagens_entrada=6000");
        for (int i = 6000; i < 9000; i++) { log.AppendText($"Mensagem {i:0000}\r\n"); if (i % 100 == 0) _ = log.RenderedText; }
        if (log.RenderedText.Length > BoundedLogText.DefaultCapacity || !log.RenderedText.Contains("Mensagem 8999"))
            throw new Exception("Atualizações sucessivas ultrapassam o limite da prévia.");
        string report = new('A', 2 * BoundedLogText.DefaultCapacity);
        log.SetText(report);
        if (log.RenderedText != report) throw new Exception("Relatório estático não pode ser truncado pela política do log.");
        var tail = new BoundedLogText(5);
        tail.Append("😀😀ab");
        if (tail.Length > 5 || tail.ToString().Any(char.IsControl) || tail.ToString().EnumerateRunes().Any(r => r.Value == 0xFFFD))
            throw new Exception("Corte da prévia partiu um caractere Unicode.");
        Console.WriteLine("PASS L3 prévia em memória limitada, cauda recente e relatórios estáticos completos");
    }
}
