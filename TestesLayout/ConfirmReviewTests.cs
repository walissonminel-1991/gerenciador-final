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

        // VF06/C1: Enter e Esc devem recusar por padrao a remocao de MSIX,
        // enquanto os demais dialogos conservam o comportamento legado.
        using var msixConfirm = (Form)Activator.CreateInstance(typeof(DarkConfirmDialog),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            ["Leitor de teste\\nFamília do pacote: Contoso.Reader_pub",
             "Confirmar desinstalação MSIX", true], null)!;
        msixConfirm.Show(); msixConfirm.PerformLayout(); Application.DoEvents();
        if (msixConfirm.AcceptButton?.DialogResult != DialogResult.No ||
            msixConfirm.CancelButton?.DialogResult != DialogResult.No ||
            !msixConfirm.Controls.OfType<TableLayoutPanel>().Any())
            throw new Exception("VF06/C1: confirmação MSIX deve negar por Enter/Esc.");
        var msixLabel = (Label)typeof(DarkConfirmDialog)
            .GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(msixConfirm)!;
        if (!msixLabel.Text.Contains("Contoso.Reader_pub", StringComparison.Ordinal))
            throw new Exception("VF06/C1: confirmação não exibiu identidade da família MSIX.");
        msixConfirm.Close();
        if (form.AcceptButton?.DialogResult != DialogResult.Yes ||
            form.CancelButton?.DialogResult != DialogResult.No)
            throw new Exception("VF06/C1: rota legada de confirmação foi alterada.");
        Console.WriteLine("OK confirmação MSIX nega Enter/Esc, preserva família e contrato legado.");
        Console.WriteLine("OK confirmação dark mantém lista integral e rolagem para lote longo, sem executar ações");
    }
}
