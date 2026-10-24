using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using GerenciadorAplicativos;

internal static class Work11ProgressLayoutTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Require(bool ok,string label){if(!ok)throw new Exception("W11 UI: "+label);}
    private static ProgramEntry App(string id,string name="GF Fixture",string version="1.0")=>new()
    {InventoryId=id,Name=name,Version=version,Publisher="GF Tests",InstallLocation=@"C:\GF-Synthetic"};
    internal static void Run()
    {
        Require(typeof(UpdateInstallationContract).GetField("SyntheticRoot", BindingFlags.Static|BindingFlags.NonPublic) is null,
            "produto distribuido nao contem redirecionamento sintetico dos testes WORK13");
        using var form=new MainForm(startServices:false);
        form.Show();Application.DoEvents();
        var list=(DarkProgramList)typeof(MainForm).GetField("_programList",Private)!.GetValue(form)!;
        var inventory=(List<ProgramEntry>)typeof(MainForm).GetField("_installed",Private)!.GetValue(form)!;
        var store=(UpdateOperationStore)typeof(MainForm).GetField("_updateOperations",Private)!.GetValue(form)!;
        var render=typeof(MainForm).GetMethod("RenderPrograms",Private)!;
        var search=(TextBox)typeof(MainForm).GetField("_search",Private)!.GetValue(form)!;
        var nav=(DarkTabHost)typeof(MainForm).GetField("_nav",Private)!.GetValue(form)!;
        var app=App("HKLM|Registry64|Fixture","Primeiro aplicativo");
        var second=App("HKCU|Registry64|Fixture","Segundo aplicativo");
        inventory.Clear();inventory.AddRange([app,second]);
        var operation=store.Begin(app,"2.0")!;operation.Report(new(UpdateStage.Downloading,74,100));
        render.Invoke(form,[]);Application.DoEvents();
        Require(list.OperationFor!(list.Items[0])!.Progress.Percent==74 && list.OperationTimerRunning,"lista mostra progresso e possui um unico timer ativo");
        inventory.Clear();var reconstructed=App(app.InventoryId,app.Name,"2.0");inventory.AddRange([second,reconstructed]);
        render.Invoke(form,[]);search.Text="Segundo";Application.DoEvents();
        Require(list.Items.Count==1 && list.OperationFor(list.Items[0]) is null,"pesquisa nao transfere progresso para outro aplicativo");
        search.Text="Primeiro";nav.SelectTab(5);Application.DoEvents();nav.SelectTab(0);Application.DoEvents();
        Require(list.Items.Count==1 && list.OperationFor(list.Items[0])!.Id==operation.Snapshot.Id,"reconstrucao ordenacao pesquisa e navegacao preservam identidade");
        operation.Complete(new(UpdateAttemptState.Confirmed));list.RefreshOperations();
        Require(!list.OperationTimerRunning && list.OperationFor(reconstructed)!.Progress.Percent==100,"conclusao rapida persiste sem timer ocioso");
        search.Text="";
        var states=new[]{UpdateStage.Downloading,UpdateStage.Installing,UpdateStage.Confirmed,UpdateStage.Failed,
            UpdateStage.Pending,UpdateStage.Blocked,UpdateStage.Cancelled,UpdateStage.AuthorizationDeclined};
        inventory.Clear();
        foreach(var stage in states)
        {
            var entry=App("Fixture|"+stage,stage.ToString());inventory.Add(entry);
            var op=store.Begin(entry,"2.0")!;
            if(stage<UpdateStage.Confirmed)op.Report(new(stage,74,100));else op.Finish(stage);
        }
        render.Invoke(form,[]);Application.DoEvents();
        // A geometria de desenho recebe pixels; simular todas as larguras de janela/DPI sem depender do monitor do runner.
        int combinations=0;
        foreach(var screen in new[]{new Size(1280,720),new Size(1366,768),new Size(1440,900),new Size(1600,900),
            new Size(1920,1080),new Size(2560,1440),new Size(3840,2160),new Size(3440,1440),new Size(5120,1440)})
        foreach(float dpi in new[]{1f,1.25f,1.5f,2f})
        {
            var window=StartupWindowSizing.Fit(new Rectangle(Point.Empty,screen));
            int rowWidth=Math.Max(600,window.Width-48);
            using var bitmap=new Bitmap(rowWidth,67*states.Length);
            bitmap.SetResolution(96*dpi,96*dpi);
            using var graphics=Graphics.FromImage(bitmap);
            for(int i=0;i<states.Length;i++)
            {
                var bounds=new Rectangle(0,i*67,rowWidth,67);
                var area=AppRowGeometry.OperationArea(bounds);
                Require(bounds.Contains(area) && area.Right<AppRowGeometry.UpdateButton(bounds).Left,
                    "barra sobrepoe botao ou sai do cartao");
                var draw=typeof(MainForm).GetMethod("DrawProgramCard",Private)!;
                draw.Invoke(form,[list,new DrawItemEventArgs(graphics,list.Font,bounds,i,DrawItemState.Default)]);
            }
            if(dpi==1 || screen.Width==1440 && dpi==2)
            {
                Directory.CreateDirectory("LayoutCaptures");
                bitmap.Save($"LayoutCaptures/Atualizacoes-W11-{screen.Width}x{screen.Height}-{dpi:0.00}.png");
            }
            combinations++;
        }
        // Controle descartado e callback em outro thread nao devem acessar handles WinForms.
        var disposable=new DarkProgramList {OperationFor=store.Find};disposable.SetItems(inventory);disposable.Dispose();
        Task.Run(()=>{operation.Report(new(UpdateStage.Downloading,1,10));disposable.RefreshOperations();}).GetAwaiter().GetResult();
        Require(disposable.IsDisposed,"controle descartado suporta conclusao tardia");
        operation.Fail(new IOException("fixture"),false);
        Require(operation.Snapshot.Progress.Stage==UpdateStage.Confirmed,"erro tardio de recarga nao apaga sucesso");
        form.Close();
        Console.WriteLine($"W11 UI: {combinations} combinacoes, oito estados, reconstrucao, filtro, navegacao e descarte aprovados");
    }
}
