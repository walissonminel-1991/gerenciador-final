using System.Drawing;
using System.Text;

namespace GerenciadorFinal.Laboratorio;

internal sealed class LabForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(20, 26, 34);
    private static readonly Color SurfaceColor = Color.FromArgb(34, 45, 59);
    private static readonly Color BorderColor = Color.FromArgb(60, 78, 98);
    private static readonly Color TextColor = Color.FromArgb(235, 242, 250);
    private static readonly Color MutedColor = Color.FromArgb(164, 184, 204);
    private static readonly Color AccentColor = Color.FromArgb(43, 125, 194);

    private readonly DataGridView _grid = new();
    private readonly Label _summary = new();
    private readonly Label _status = new();
    private readonly TextBox _log = new();
    private readonly Button _consult = new();
    private readonly Button _cancel = new();
    private readonly CheckBox _showAll = new();
    private CancellationTokenSource? _cts;
    private IReadOnlyList<InstalledApp> _installed = [];
    private IReadOnlyList<UpdateMatch> _results = [];
    private bool _demo;

    public LabForm()
    {
        Text = "Gerenciador Final — Laboratório de Provedores v0.1.1 (somente leitura)";
        MinimumSize = new Size(990, 660);
        Size = new Size(1200, 780);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        ForeColor = TextColor;
        BackColor = BackgroundColor;
        BuildLayout();
        FormClosing += (_, _) => _cts?.Cancel();
        LoadInstalled();
    }

    private static Label MakeLabel(string text, float size, bool bold = false)
    {
        return new Label
        {
            Text = text,
            ForeColor = TextColor,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 5)
        };
    }

    private static Button MakeButton(string text, Color color)
    {
        return new Button
        {
            Text = text,
            Width = 170,
            Height = 38,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundColor,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        Controls.Add(root);

        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        header.Controls.Add(MakeLabel("GERENCIADOR FINAL — LABORATÓRIO", 17, true));
        var explanation = MakeLabel("Prova de conceito: catálogo RuckZuck, comparação local e nenhuma instalação automática.", 9);
        explanation.ForeColor = MutedColor;
        header.Controls.Add(explanation);
        root.Controls.Add(header, 0, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _consult.Text = "Consultar catálogo";
        _consult.Width = 175;
        _consult.Height = 38;
        _consult.FlatStyle = FlatStyle.Flat;
        _consult.BackColor = AccentColor;
        _consult.ForeColor = Color.White;
        _consult.Click += async (_, _) => await ConsultAsync();
        actions.Controls.Add(_consult);

        _cancel.Text = "Cancelar";
        _cancel.Width = 110;
        _cancel.Height = 38;
        _cancel.FlatStyle = FlatStyle.Flat;
        _cancel.BackColor = SurfaceColor;
        _cancel.ForeColor = TextColor;
        _cancel.Enabled = false;
        _cancel.Click += (_, _) => _cts?.Cancel();
        actions.Controls.Add(_cancel);

        var demo = MakeButton("Testar com exemplos", SurfaceColor);
        demo.Width = 182;
        demo.Click += (_, _) => LoadDemo();
        actions.Controls.Add(demo);

        _showAll.Text = "Mostrar também não confirmados";
        _showAll.AutoSize = true;
        _showAll.ForeColor = TextColor;
        _showAll.Checked = false;
        _showAll.Margin = new Padding(22, 10, 0, 0);
        _showAll.CheckedChanged += (_, _) => RenderResults();
        actions.Controls.Add(_showAll);
        root.Controls.Add(actions, 0, 1);

        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _summary.Text = "Aguardando consulta.";
        _summary.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _summary.ForeColor = TextColor;
        _summary.Dock = DockStyle.Fill;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _status.Text = "MODO LOCAL";
        _status.ForeColor = MutedColor;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.Dock = DockStyle.Fill;
        info.Controls.Add(_summary, 0, 0);
        info.Controls.Add(_status, 1, 0);
        root.Controls.Add(info, 0, 2);

        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = SurfaceColor;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = BorderColor;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(43, 59, 77);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 36;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowTemplate.Height = 35;
        _grid.DefaultCellStyle.BackColor = SurfaceColor;
        _grid.DefaultCellStyle.ForeColor = TextColor;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(47, 90, 126);
        _grid.DefaultCellStyle.SelectionForeColor = TextColor;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(38, 51, 66);
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add("Nome", "Programa instalado");
        _grid.Columns.Add("Instalada", "Instalada");
        _grid.Columns.Add("Catalogo", "Catálogo");
        _grid.Columns.Add("Estado", "Resultado");
        _grid.Columns[0].FillWeight = 165;
        _grid.Columns[1].FillWeight = 68;
        _grid.Columns[2].FillWeight = 68;
        _grid.Columns[3].FillWeight = 165;
        root.Controls.Add(_grid, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var footControls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        footControls.Controls.Add(MakeLabel("DIAGNÓSTICO LOCAL (memória)", 9, true));
        var copy = MakeButton("Copiar diagnóstico", SurfaceColor);
        copy.Width = 150;
        copy.Height = 29;
        copy.Margin = new Padding(28, 0, 0, 0);
        copy.Click += (_, _) =>
        {
            if (_log.Text.Length != 0) Clipboard.SetText(_log.Text);
        };
        footControls.Controls.Add(copy);
        footer.Controls.Add(footControls, 0, 0);
        _log.Dock = DockStyle.Fill;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9F);
        _log.BackColor = Color.FromArgb(13, 19, 26);
        _log.ForeColor = TextColor;
        _log.BorderStyle = BorderStyle.FixedSingle;
        footer.Controls.Add(_log, 0, 1);
        root.Controls.Add(footer, 0, 4);
    }

    private void Log(string eventName, string detail = "")
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {eventName}{(detail.Length > 0 ? " " + detail : "")}{Environment.NewLine}");
    }

    private void LoadInstalled()
    {
        _installed = Inventory.Read();
        _demo = false;
        _results = [];
        _summary.Text = $"{_installed.Count} registros locais • atualizações não verificadas";
        _grid.Rows.Clear();
        foreach (var entry in _installed)
            _grid.Rows.Add(entry.Name, entry.Version, "—", "NÃO VERIFICADO");
        Log("REGISTRO.LIDO", $"quantidade={_installed.Count}");
    }

    private void LoadDemo()
    {
        _demo = true;
        var installed = new[]
        {
            new InstalledApp("Exemplo Editor 1.2 (x64)", "1.2", "Empresa Exemplo Ltd"),
            new InstalledApp("Exemplo Arquivador", "3.0", "Empresa Exemplo Ltd"),
            new InstalledApp("Exemplo Desconhecido", "", "Outro Fabricante")
        };
        var catalog = new[]
        {
            new CatalogEntry("Exemplo Editor", "Exemplo Editor 1.4", "1.4", "Empresa Exemplo"),
            new CatalogEntry("Exemplo Arquivador", "Exemplo Arquivador 3.0", "3.0", "Empresa Exemplo")
        };
        _results = Matching.Evaluate(installed, catalog);
        _showAll.Checked = true;
        RenderResults();
        _status.Text = "DEMONSTRAÇÃO — DADOS FICTÍCIOS";
        Log("MOTOR.TESTE_OFFLINE", "exemplos_ficticios=3");
    }

    private async Task ConsultAsync()
    {
        if (_cts is not null) return;
        var consent = MessageBox.Show(this,
            "O laboratório consultará por HTTPS o catálogo público do RuckZuck.\n\n" +
            "A lista de aplicativos instalados NÃO será enviada ao serviço; a comparação ocorre neste computador. " +
            "O provedor receberá dados normais de conexão (como endereço IP).\n\n" +
            "Nenhum programa será baixado, instalado, atualizado ou desinstalado. Continuar?",
            "Consulta somente de leitura", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (consent != DialogResult.Yes) return;

        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        _consult.Enabled = false;
        _cancel.Enabled = true;
        _status.Text = "CONSULTANDO CATÁLOGO...";
        Log("CATALOGO.CONSULTA_INICIO", "provedor=ruckzuck metodo=HTTPS_GET");
        try
        {
            using var provider = new RuckZuckCatalog();
            var catalog = await provider.GetCatalogAsync(_cts.Token);
            Log("CATALOGO.RECEBIDO", $"entradas={catalog.Count}");
            // Atualiza o Registro somente depois que a consulta remota terminar.
            _installed = Inventory.Read();
            _results = Matching.Evaluate(_installed, catalog);
            _demo = false;
            _showAll.Checked = false;
            RenderResults();
            _status.Text = "CONSULTA CONCLUÍDA — SEM INSTALAÇÕES";
            Log("CATALOGO.COMPARACAO_OK", $"instalados={_installed.Count} candidatos={_results.Count(x => x.State == MatchState.Candidate)}");
        }
        catch (OperationCanceledException)
        {
            _status.Text = "CONSULTA CANCELADA / LIMITE DE TEMPO";
            Log("CATALOGO.CANCELADA");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or System.Text.Json.JsonException or IOException)
        {
            _status.Text = "CATÁLOGO INDISPONÍVEL";
            Log("CATALOGO.FALHA", $"tipo={ex.GetType().Name} codigo={(ex is HttpRequestException h ? (int?)h.StatusCode : null)}");
            MessageBox.Show(this,
                "A consulta ao catálogo não foi concluída. Nenhum resultado foi tratado como ausência de atualizações.\n\n" +
                ex.Message, "Consulta indisponível", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _status.Text = "FALHA INESPERADA";
            Log("CATALOGO.FALHA_INESPERADA", $"tipo={ex.GetType().Name}");
            MessageBox.Show(this, "Falha inesperada na consulta. Nenhuma alteração foi feita no Windows.",
                "Falha", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            if (!IsDisposed && !Disposing)
            {
                _consult.Enabled = true;
                _cancel.Enabled = false;
            }
        }
    }

    private void RenderResults()
    {
        if (_results.Count == 0)
        {
            _grid.Rows.Clear();
            return;
        }
        _grid.Rows.Clear();
        IEnumerable<UpdateMatch> displayed = _showAll.Checked ? _results : _results.Where(r => r.State == MatchState.Candidate);
        foreach (var item in displayed)
        {
            var row = _grid.Rows[_grid.Rows.Add(item.Installed.Name,
                item.Installed.Version.Length > 0 ? item.Installed.Version : "Não informada",
                item.Catalog?.ProductVersion ?? "—", item.StateLabel)];
            if (item.State == MatchState.Candidate)
                row.Cells[3].Style.ForeColor = Color.FromArgb(128, 225, 174);
        }
        int candidates = _results.Count(x => x.State == MatchState.Candidate);
        int withoutCoverage = _results.Count(x => x.State is MatchState.Unmatched or MatchState.NotComparable or MatchState.Ambiguous);
        _summary.Text = _demo
            ? "DEMONSTRAÇÃO • dados fictícios (não representam o seu PC)"
            : $"{candidates} possíveis atualizações • {withoutCoverage} sem confirmação • {_results.Count} avaliados";
    }
}
