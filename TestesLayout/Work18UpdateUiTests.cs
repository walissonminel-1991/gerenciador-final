using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class Work18UpdateUiTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run()
    {
        foreach(string method in new[]{"SealSynthetic","ExpireSynthetic","SimulateIssued"})
            if(typeof(AutomaticUpdateDiscovery).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic) is not null)
                throw new Exception("WORK18 produto distribui mecanismo sintetico: "+method);
        using var form=new MainForm(startServices:false);form.Show();Application.DoEvents();
        var inventory=(List<ProgramEntry>)typeof(MainForm).GetField("_installed",Private)!.GetValue(form)!;
        var list=(DarkProgramList)typeof(MainForm).GetField("_programList",Private)!.GetValue(form)!;
        ProgramEntry App(string name,string publisher="GF Tests")=>new(){Name=name,Publisher=publisher,
            Version="1.0.0",AvailableVersion="2.0.0",IsUpdate=true,InventoryId="CurrentUser|Registry64|"+name};
        var site=App("EA app");var store=App("Outlook for Windows","Microsoft Corporation");var guide=App("Fixture consultiva");
        var review=App("Fixture revisao");review.UpdateRequiresReview=true;
        var arch=App("Fixture arquitetura");
        arch.Capabilities=ApplicationCapabilities.Evaluate(new(arch.InventoryId,UpdateChecked:true,UpdateAvailable:true,
            UpdateReason:"manifest_architecture_mismatch"));
        inventory.Clear();inventory.AddRange([site,store,guide,review,arch]);
        var managed=new List<ProgramEntry>();
        EcosystemInventory.AppendManaged(managed,[new("scoop","ok",[new(new("scoop","fixture","Fixture gerenciada","GF Tests","1.0.0",Managed:true),"fixture")])]);
        managed[0].IsUpdate=true;managed[0].AvailableVersion="2.0.0";inventory.Add(managed[0]);
        if(VerifiedUpdaters.PrimaryActionLabel(site)!="Site oficial" || VerifiedUpdaters.PrimaryActionLabel(store)!="Ver na Store" ||
            VerifiedUpdaters.PrimaryActionLabel(guide)!="Como fazer" || VerifiedUpdaters.PrimaryActionLabel(review)!="Revisar" ||
            VerifiedUpdaters.PrimaryActionLabel(managed[0])!="Gerenciar" ||
            !VerifiedUpdaters.UpdateAvailabilityText(arch,arch.Capabilities.Update.Reason).Contains("arquitetura"))
            throw new Exception("WORK18 botoes ou explicacoes divergem das evidencias");
        typeof(MainForm).GetMethod("RenderPrograms",Private)!.Invoke(form,[]);Application.DoEvents();
        if(list.Items.Count!=6)throw new Exception("WORK18 inventario consultivo nao aparece na lista");
        int combinations=0;
        foreach(var screen in new[]{new Size(1440,900),new Size(2560,1440)})
        foreach(float dpi in new[]{1f,1.25f,1.5f,2f})
        {
            var window=StartupWindowSizing.Fit(new Rectangle(Point.Empty,screen));int width=Math.Max(600,window.Width-48);
            using var bitmap=new Bitmap(width,67*list.Items.Count);bitmap.SetResolution(96*dpi,96*dpi);
            using var graphics=Graphics.FromImage(bitmap);
            for(int i=0;i<list.Items.Count;i++)
            {
                var bounds=new Rectangle(0,i*67,width,67);
                if(!bounds.Contains(AppRowGeometry.UpdateButton(bounds)) ||
                    AppRowGeometry.UpdateButton(bounds).IntersectsWith(AppRowGeometry.UninstallButton(bounds)))
                    throw new Exception("WORK18 botoes sobrepostos ou fora do cartao");
                typeof(MainForm).GetMethod("DrawProgramCard",Private)!.Invoke(form,[list,new DrawItemEventArgs(graphics,list.Font,bounds,i,DrawItemState.Default)]);
            }
            Directory.CreateDirectory("LayoutCaptures");bitmap.Save($"LayoutCaptures/WORK18-{screen.Width}x{screen.Height}-{dpi:0.00}.png");combinations++;
        }
        Console.WriteLine($"WORK18 UI: {combinations} combinacoes, consulta/Store/revisao/gerenciamento e motivos visiveis; nenhuma operacao real");
    }
}
