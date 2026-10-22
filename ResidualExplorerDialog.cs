using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Uma revisão organizada; detalhes e inspeção ficam na mesma janela, sem ações destrutivas.
internal sealed class ResidualExplorerDialog : Form
{
    private IReadOnlyList<ResidualAuditReport> source;
    private static readonly SemaphoreSlim nativeInspection = new(1, 1);
    private ResidualResultIndex? index;
    private readonly Label summary = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly TextBox search = new() { Name = "ResidualSearch", Dock = DockStyle.Fill, PlaceholderText = "Buscar programa, fabricante ou local", MaxLength = 256 };
    private readonly ComboBox filter = new() { Name = "ResidualFilter", DropDownStyle = ComboBoxStyle.DropDownList, Width = 245 };
    private readonly ComboBox origin = new() { Name = "ResidualSource", DropDownStyle = ComboBoxStyle.DropDownList, Width = 235 };
    private readonly DarkResidualList groups = new() { Name = "ResidualGroups", Dock = DockStyle.Fill };
    private readonly DarkResidualList locations = new() { Name = "ResidualLocations", Dock = DockStyle.Fill };
    private readonly DarkLogView details = new() { Dock = DockStyle.Fill };
    private readonly SplitContainer split = new() { Dock = DockStyle.Fill, SplitterWidth = 5, Panel1MinSize = 0, Panel2MinSize = 0 };
    private readonly ModernButton previous = Button("Anterior", "ResidualPrevious", 86);
    private readonly ModernButton next = Button("Próxima", "ResidualNext", 86);
    private readonly ModernButton cancel = Button("Cancelar", "ResidualCancel", 86);
    private readonly ModernButton inspect = Button("Inspecionar local", "ResidualInspect", 150);
    private readonly ModernButton cleanFoobar = Button("Limpar perfis foobar", "ResidualCleanFoobar", 160);
    private readonly Label pageLabel = new() { Width = 110, Height = 32, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label locationLabel = new() { Width = 74, Height = 30, TextAlign = ContentAlignment.MiddleCenter };
    private readonly ModernButton locationPrevious = Button("Locais ‹", "ResidualLocationPrevious", 75);
    private readonly ModernButton locationNext = Button("Locais ›", "ResidualLocationNext", 75);
    private readonly ModernButton back = Button("Voltar", "ResidualBack", 76);
    private readonly Font readable = new("Segoe UI", 9f);
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private readonly SemaphoreSlim worker = new(1, 1);
    private CancellationTokenSource? operation;
    private readonly System.Windows.Forms.Timer debounce = new() { Interval = 220 };
    private readonly Dictionary<int, string> selections = new();
    private int generation, locationPage;
    private bool binding, resourcesDisposed;
    private string criteria = "";
    private ResidualAuditReport? selectedReport;
    private ResidualInspectionSession? inspection;
    private ResidualInspectionResult? inspectionResult;
    private readonly Func<string, CancellationToken, Task<ResidualInspectionResult>>? inspectOverride;
    internal ResidualResultPage? CurrentPage { get; private set; }
    internal bool Ready => index is not null && CurrentPage is not null;
    internal bool Busy => operation is not null;
    internal string SummaryText => summary.Text;
    internal DarkResidualList Groups => groups;
    internal DarkResidualList Locations => locations;
    internal string DetailText => details.RenderedText;
    internal ResidualExplorerDialog(IReadOnlyList<ResidualAuditReport> reports,
        Func<string, CancellationToken, Task<ResidualInspectionResult>>? inspector = null)
    {
        source = reports; inspectOverride = inspector; lifetimeToken = lifetime.Token;
        locations.EmptyMessage = "Nenhum item neste nível. Consulte o resumo; os dados foram preservados.";
        criteria = "\u001f" + ResidualFilter.All + "\u001f" + ResidualSource.None;
        Text = "Investigação de resíduos"; StartPosition = FormStartPosition.CenterParent;
        BackColor = AppServices.Background; ForeColor = Color.FromArgb(235, 242, 250); Font = new Font("Segoe UI", 9f);
        ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        ClientSize = FitClient(area); MinimumSize = new(Math.Min(640, area.Width - 16), Math.Min(460, area.Height - 24));
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12), BackColor = BackColor };
        foreach (float height in new[] { 83f, 34f, 37f }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        summary.Text = "Preparando resultados...\nExclusão restrita a perfis do foobar2000 com confirmação; outras exclusões bloqueadas.";
        root.Controls.Add(summary, 0, 0);
        search.BackColor = Color.FromArgb(30, 40, 52); search.ForeColor = ForeColor; search.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(search, 0, 1);
        filter.Items.AddRange(new object[] { "Todas as classificações", "Possível resíduo — revisar", "Dados importantes — preservar", "Compartilhado — preservar", "Instalação a conferir", "Estado desconhecido" });
        origin.Items.AddRange(new object[] { "Todas as origens", "Perfis do usuário", "Dados comuns", "Instalação", "Registro", "Atalhos", "Inicialização e serviços" });
        foreach (var combo in new[] { filter, origin }) { combo.BackColor = search.BackColor; combo.ForeColor = ForeColor; combo.FlatStyle = FlatStyle.Flat; combo.DrawMode = DrawMode.OwnerDrawFixed; combo.ItemHeight = 21;
            combo.DrawItem += (_, e) =>
            {
                using var background = new SolidBrush(e.State.HasFlag(DrawItemState.Selected) ? Color.FromArgb(42, 66, 92) : Color.FromArgb(30, 40, 52));
                e.Graphics.FillRectangle(background, e.Bounds);
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, combo.Items[e.Index]?.ToString(), combo.Font, e.Bounds, ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };
            combo.SelectedIndex = 0; }
        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        filters.Controls.Add(filter); filters.Controls.Add(origin); root.Controls.Add(filters, 0, 2);
        split.Panel1.Controls.Add(groups);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 46)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); right.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        details.UseReadableFont(readable); right.Controls.Add(details, 0, 0); right.Controls.Add(locations, 0, 1);
        var locationBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        locationBar.Controls.AddRange([locationPrevious, locationLabel, locationNext, back]); right.Controls.Add(locationBar, 0, 2);
        var inspectBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        inspectBar.Controls.Add(inspect); inspectBar.Controls.Add(cleanFoobar);
        right.Controls.Add(inspectBar, 0, 3); split.Panel2.Controls.Add(right);
        root.Controls.Add(split, 0, 3);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 6, 0, 0) };
        var coverage = Button("Resumo", "ResidualCoverage", 78); var close = Button("Fechar", "ResidualClose", 78);
        footer.Controls.AddRange([previous, pageLabel, next, coverage, cancel, close]); root.Controls.Add(footer, 0, 4);
        Controls.Add(root); CancelButton = close; close.Click += (_, _) => Close();
        previous.Click += async (_, _) => await QueryAsync(Math.Max(0, (CurrentPage?.PageIndex ?? 0) - 1));
        next.Click += async (_, _) => await QueryAsync((CurrentPage?.PageIndex ?? 0) + 1);
        cancel.Click += (_, _) => CancelCurrent();
        coverage.Click += (_, _) => details.SetText(index?.Coverage.Describe() ?? "Preparando fotografia...");
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        debounce.Tick += async (_, _) => { debounce.Stop(); await QueryAsync(0); };
        filter.SelectedIndexChanged += async (_, _) => await QueryAsync(0);
        origin.SelectedIndexChanged += async (_, _) => await QueryAsync(0);
        groups.SelectionChanged += ShowReport; locations.SelectionChanged += ShowLocation;
        locationPrevious.Click += (_, _) => { locationPage = Math.Max(0, locationPage - 1); ShowLocations(); };
        locationNext.Click += (_, _) => { locationPage++; ShowLocations(); };
        back.Click += (_, _) => { inspectionResult = null; locationPage = 0; ShowLocations(); };
        inspect.Click += async (_, _) => await InspectSelectedAsync();
        cleanFoobar.Click += async (_, _) =>
        {
            cleanFoobar.Enabled = false;
            try
            {
                string result = await FoobarProfileCleanupFlow.ConfirmAndCleanAsync(this, lifetimeToken);
                if (!IsDisposed) details.SetText(result);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            { if (!IsDisposed) details.SetText("Limpeza interrompida: " + ex.Message); }
            finally { if (!IsDisposed) UpdateActions(); }
        };
        split.SizeChanged += (_, _) => ArrangeBody();
        Shown += async (_, _) => await RunAsync(async (token, id) =>
        {
            var built = await Task.Run(() => new ResidualResultIndex(source, token), token);
            if (!Current(id, token)) return; index = built;
            var page = await Task.Run(() => built.Query(cancellationToken: token), token);
            if (Current(id, token)) Bind(page);
        });
        UpdateActions();
    }
    internal static Size FitClient(Rectangle area) => new(Math.Clamp(area.Width - 48, 480, 1080), Math.Clamp(area.Height - 80, 380, 700));
    private static ModernButton Button(string text, string name, int width) => new()
    { Text = text, Name = name, Width = width, Height = 30, BackColor = Color.FromArgb(43, 60, 78), ForeColor = Color.White, AllowMultilineText = false, Margin = new Padding(2) };
    private void ArrangeBody()
    {
        if (split.Width < 2 || split.Height < 2) return;
        var orientation = ClientSize.Width < 820 ? Orientation.Horizontal : Orientation.Vertical;
        if (split.Orientation != orientation) split.Orientation = orientation;
        int extent = orientation == Orientation.Vertical ? split.ClientSize.Width : split.ClientSize.Height;
        if (extent > 20) split.SplitterDistance = Math.Clamp((int)(extent * (orientation == Orientation.Vertical ? .46 : .38)), 1, extent - split.SplitterWidth - 1);
    }
    internal Task QueryAsync(int page = 0)
    {
        var snapshot = index;
        if (snapshot is null || IsDisposed) return Task.CompletedTask;
        string text = search.Text; var classification = (ResidualFilter)filter.SelectedIndex;
        ResidualSource sourceFilter = origin.SelectedIndex switch { 1 => ResidualSource.UserData, 2 => ResidualSource.CommonData,
            3 => ResidualSource.Installation, 4 => ResidualSource.Registry, 5 => ResidualSource.Shortcut, 6 => ResidualSource.Integration, _ => ResidualSource.None };
        string nextCriteria = text + "\u001f" + classification + "\u001f" + sourceFilter;
        if (criteria != nextCriteria) { criteria = nextCriteria; selections.Clear(); page = 0; }
        else if (CurrentPage is not null && groups.SelectedRow is not null) selections[CurrentPage.PageIndex] = groups.SelectedRow.Key;
        return RunAsync(async (token, id) =>
        {
            var result = await Task.Run(() => snapshot.Query(text, classification, page, cancellationToken: token, source: sourceFilter), token);
            if (Current(id, token)) Bind(result);
        });
    }
    private void Bind(ResidualResultPage page)
    {
        CurrentPage = page;
        summary.Text = (page.Coverage.Incomplete ? "ANÁLISE PARCIAL — " + page.Coverage.ReasonSummary + ".\n" : "Análise dos locais consultados; não é varredura completa do computador.\n") +
            $"{page.Matching} de {page.TotalAvailable} grupos; {page.Reports.Count} nesta página, {page.NotLoaded} nas demais. {page.LocationsAvailable} locais disponíveis.\nExclusão restrita a perfis do foobar2000 com confirmação; outras exclusões bloqueadas.";
        summary.ForeColor = page.Coverage.Incomplete ? Color.FromArgb(255, 209, 138) : ForeColor;
        groups.EmptyMessage = page.Matching == 0 && filter.SelectedIndex == (int)ResidualFilter.Review &&
            page.Coverage.Incomplete
            ? "Nenhum candidato revisável. A análise é parcial; preserve os dados."
            : "Nenhum grupo corresponde aos filtros escolhidos.";
        locations.EmptyMessage = page.Matching == 0
            ? "Nenhum local corresponde aos filtros escolhidos."
            : "Nenhum item neste nível. Consulte o resumo; os dados foram preservados.";
        binding = true;
        try { groups.SetRows(page.Reports.Select(r => new ResidualDisplayRow(r.Product, r.Product,
            $"{r.Findings.Count} locais · {(r.Incomplete ? "análise parcial" : "revisão necessária")}",
            string.Join(" / ", r.Findings.Select(f => f.DecisionLabel).Distinct().Take(2)), r)), selections.GetValueOrDefault(page.PageIndex)); }
        finally { binding = false; }
        pageLabel.Text = $"{page.PageIndex + 1} / {page.PageCount}";
        UpdateActions();
    }
    private void ShowReport()
    {
        if (!binding && Busy) operation?.Cancel();
        selectedReport = groups.SelectedRow?.Target as ResidualAuditReport;
        inspectionResult = null; locationPage = 0;
        inspection = selectedReport is null ? null : new ResidualInspectionSession(selectedReport);
        if (selectedReport is null) { locations.SetRows([]); details.SetText(index?.Coverage.Describe() ?? "Preparando resultados..."); return; }
        ShowLocations();
        UpdateActions();
    }
    private void ShowLocations()
    {
        if (selectedReport is null) return;
        if (inspectionResult is null)
        {
            int pages = Math.Max(1, (selectedReport.Findings.Count + 29) / 30); locationPage = Math.Clamp(locationPage, 0, pages - 1);
            locations.SetRows(ResidualResultIndex.LocationPage(selectedReport, locationPage).Select(f =>
                new ResidualDisplayRow(f.Location, f.Kind, f.Location, f.DecisionLabel, f)));
            locationLabel.Text = $"{locationPage + 1}/{pages}"; locationPrevious.Enabled = locationPage > 0; locationNext.Enabled = locationPage + 1 < pages;
        }
        else
        {
            int pages = Math.Max(1, (inspectionResult.Entries.Count + 29) / 30); locationPage = Math.Clamp(locationPage, 0, pages - 1);
            locations.SetRows(inspectionResult.Entries.Skip(locationPage * 30).Take(30).Select(e =>
                new ResidualDisplayRow(e.Location, e.Name, e.Location, "Dados importantes — preservar", e)));
            locationLabel.Text = $"{locationPage + 1}/{pages}"; locationPrevious.Enabled = locationPage > 0; locationNext.Enabled = locationPage + 1 < pages;
        }
        back.Enabled = inspectionResult is not null; ShowLocation();
    }
    private void ShowLocation()
    {
        if (selectedReport is null) return;
        string text = selectedReport.Product + "\r\n" + (selectedReport.UninstallConfirmed ? "Registro de desinstalação ausente; auxiliares ainda podem estar ativos." : "O programa ainda pode estar instalado. Vínculo exclusivo não comprovado.") + "\r\n\r\n";
        if (locations.SelectedRow?.Target is ResidualAuditFinding f) text += f.DecisionLabel + "\r\n" + f.Location + "\r\n" + f.Evidence;
        else if (locations.SelectedRow?.Target is ResidualInspectionEntry e) text += e.Location + "\r\n" +
            e.PreservationExplanation + "\r\nPista pelo nome; finalidade e vínculo não confirmados. Conteúdo não lido. " +
            (e.Container ? !e.CanInspect
                ? "Limite de profundidade ou quantidade: esta subpasta não foi examinada."
                : "Inspeção de subpastas exige nova consulta limitada."
                : "Arquivo preservado; não é aberto nesta inspeção.");
        // A finalidade do item selecionado deve aparecer antes do resumo longo.
        if (inspectionResult is not null) text += "\r\n\r\n" + inspectionResult.Explanation + "\r\n" + inspectionResult.Coverage.Describe();
        details.SetText(text + "\r\n\r\nExclusão restrita a perfis do foobar2000 com confirmação; outras exclusões bloqueadas."); UpdateActions();
    }
    internal Task InspectSelectedAsync()
    {
        string? path = locations.SelectedRow?.Key; var session = inspection;
        if (path is null || session is null) return Task.CompletedTask;
        return RunAsync(async (token, id) =>
        {
            await nativeInspection.WaitAsync(token);
            ResidualInspectionResult result;
            try
            {
                result = inspectOverride is null ? await Task.Run(() => session.Inspect(path, token), token) : await inspectOverride(path, token);
            }
            finally { nativeInspection.Release(); }
            if (!Current(id, token)) return;
            inspectionResult = result; locationPage = 0; ShowLocations();
        });
    }
    private bool Current(int id, CancellationToken token) => !IsDisposed && !token.IsCancellationRequested && id == generation;
    internal void CancelCurrent() { operation?.Cancel(); summary.Text = "Consulta cancelada. Resultados anteriores preservados; dados não apagados.\nExclusão restrita a perfis do foobar2000 com confirmação; outras exclusões bloqueadas."; }
    private async Task RunAsync(Func<CancellationToken, int, Task> work)
    {
        operation?.Cancel(); var own = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken); operation = own;
        int id = ++generation; bool entered = false; UpdateActions();
        try { await worker.WaitAsync(own.Token); entered = true; await work(own.Token, id); }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        { if (Current(id, own.Token)) summary.Text = "Análise parcial — não foi possível consultar todos os dados. Resultados anteriores preservados.\nExclusão restrita a perfis do foobar2000 com confirmação; outras exclusões bloqueadas."; }
        finally
        {
            if (entered) worker.Release();
            if (ReferenceEquals(operation, own)) operation = null;
            own.Dispose(); if (!IsDisposed) UpdateActions();
        }
    }
    private void UpdateActions()
    {
        cancel.Enabled = Busy; previous.Enabled = CurrentPage?.PageIndex > 0; next.Enabled = CurrentPage is not null && CurrentPage.PageIndex + 1 < CurrentPage.PageCount;
        inspect.Enabled = !Busy && locations.SelectedRow is not null && (locations.SelectedRow.Target is not ResidualInspectionEntry entry || entry.CanInspect);
        cleanFoobar.Visible = selectedReport?.Product.Equals("foobar2000", StringComparison.OrdinalIgnoreCase) == true;
        cleanFoobar.Enabled = !Busy && cleanFoobar.Visible;
    }
    internal static Rectangle FitBounds(Rectangle requested, Rectangle workingArea)
    {
        int width = Math.Min(Math.Max(1, requested.Width), Math.Max(1, workingArea.Width - 16));
        int height = Math.Min(Math.Max(1, requested.Height), Math.Max(1, workingArea.Height - 16));
        return new Rectangle(Math.Clamp(requested.X, workingArea.Left, workingArea.Right - width),
            Math.Clamp(requested.Y, workingArea.Top, workingArea.Bottom - height), width, height);
    }
    protected override void OnShown(EventArgs e)
    {
        // A construção ainda não conhece o monitor do owner. Conter o tamanho
        // físico DEPOIS da escala DPI, incluindo bordas e barra de título.
        var area = Screen.FromControl(Owner ?? this).WorkingArea;
        var fitted = FitBounds(Bounds, area);
        MinimumSize = new Size(Math.Min(MinimumSize.Width, fitted.Width), Math.Min(MinimumSize.Height, fitted.Height));
        Bounds = fitted;
        base.OnShown(e);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int enabled = 1; _ = DwmSetWindowAttribute(Handle, 20, ref enabled, 4); }
        catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
    protected override void Dispose(bool disposing)
    {
        bool release = disposing && !resourcesDisposed;
        if (release)
        {
            resourcesDisposed = true;
            debounce.Stop(); debounce.Dispose(); lifetime.Cancel(); operation?.Cancel(); index = null; source = []; CurrentPage = null;
            selectedReport = null; inspection = null; inspectionResult = null; selections.Clear();
        }
        base.Dispose(disposing);
        if (release) { lifetime.Dispose(); readable.Dispose(); }
    }
}
