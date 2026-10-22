using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using GerenciadorAplicativos;
internal static class ConfirmReviewTests
{
    internal static void Run()
    {
        string content=string.Join("\n\n",Enumerable.Range(1,24).Select(i=>
            "• Aplicativo "+i+"\nC:\\Program Files\\Fornecedor\\Aplicativo "+i+"\\uninstall.exe /S"));
        using var form=(Form)Activator.CreateInstance(typeof(DarkConfirmDialog),
            BindingFlags.Instance|BindingFlags.NonPublic,null,[content,"Confirmar desinstalação"],null)!;
        form.Show();form.PerformLayout();Application.DoEvents();
        var label=(Label)typeof(DarkConfirmDialog).GetField("_message",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        if(label.Text!=content || label.Parent is not Panel panel || !panel.AutoScroll || label.Height<=panel.ClientSize.Height)
            throw new Exception("Confirmação longa ocultou seleção sem permitir rolagem.");
        panel.ScrollControlIntoView(label);
        Directory.CreateDirectory("LayoutCaptures");
        using var bitmap=new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
        bitmap.Save("LayoutCaptures/Residuos-Confirmacao-Lote.png");
        form.Close();
        Console.WriteLine("OK confirmação dark mantém lista integral e rolagem para lote longo, sem executar ações");
    }
}
