using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Revisão WinForms do piloto de drivers. Na distribuição padrão, backend e
// journal NÃO são fornecidos: zero caminho de download ou instalação nativos.
// Fixtures injetam SOMENTE um backend sintético e journal temporário.
internal static class DriverUpdatePilotDialog
{
    private sealed record ChoiceRow(DriverUpdatePilotChoice Choice)
    {
        public override string ToString() =>
            Choice.Title + "  |  " + Choice.DriverClass +
            "  |  revisão " + Choice.Revision;
    }

    internal static Form Create(
        DriverInventoryResult inventory, DriverOfficialResult official,
        DriverInventoryEntry selected, Func<bool> isCurrent,
        IDriverUpdatePilotBackend? backendForTest = null,
        Func<IDriverUpdatePilotJournal>? journalForTest = null)
    {
        var available = official.Matches
            .Where(m => string.Equals(m.DeviceId, selected.DeviceId,
                StringComparison.OrdinalIgnoreCase))
            .Where(m => DriverUpdatePilot.TryPrepare(inventory, official, m, out _))
            .Select(m =>
            {
                _ = DriverUpdatePilot.TryPrepare(inventory, official, m, out var choice);
                return new ChoiceRow(choice!);
            })
            .OrderBy(m => m.Choice.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Choice.UpdateId, StringComparer.Ordinal)
            .ToArray();

        var form = new Form
        {
            Name = "DriverPilotReviewForm",
            Text = "Drivers — piloto WUA experimental (revisão)",
            Width = 760, Height = 550, MinimumSize = new Size(500, 340),
            StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            Font = new Font("Segoe UI", 9F), BackColor = Color.FromArgb(26, 31, 39),
            ForeColor = Color.Gainsboro
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(9),
            ColumnCount = 1, RowCount = 8
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var (kind, size) in new[]
        {
            (SizeType.Absolute, 38F), (SizeType.Absolute, 32F),
            (SizeType.Percent, 48F), (SizeType.Percent, 52F),
            (SizeType.Absolute, 30F), (SizeType.Absolute, 30F),
            (SizeType.Absolute, 43F), (SizeType.Absolute, 43F)
        }) root.RowStyles.Add(new RowStyle(kind, size));
        var headline = new Label
        {
            Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
            Text = "PILOTO EXPERIMENTAL — seleção por dispositivo e UpdateID",
            AccessibleName = "Identificação do piloto experimental"
        };
        var search = new TextBox
        {
            Name = "DriverPilotSearch", Dock = DockStyle.Fill,
            PlaceholderText = "Pesquisar ofertas oficiais", AccessibleName = "Pesquisar ofertas de drivers"
        };
        var choices = new ListBox
        {
            Name = "DriverPilotOffers", Dock = DockStyle.Fill,
            IntegralHeight = false, AccessibleName = "Ofertas WUA para o dispositivo selecionado",
            AccessibleDescription = "Selecione UMA oferta. Título ou data não provam versão superior."
        };
        var detail = new TextBox
        {
            Name = "DriverPilotDetails", Dock = DockStyle.Fill, Multiline = true,
            ScrollBars = ScrollBars.Vertical, ReadOnly = true,
            AccessibleName = "Identidade e limites da atualização",
            BackColor = Color.FromArgb(33, 39, 49), ForeColor = Color.Gainsboro,
            BorderStyle = BorderStyle.FixedSingle
        };
        var confirm = new CheckBox
        {
            Name = "DriverPilotConfirm", Dock = DockStyle.Fill,
            Text = "Confirmo a oferta selecionada (sem atualização automática)",
            AccessibleName = "Confirmar explicitamente UpdateID, revisão e dispositivo"
        };
        var isolated = new CheckBox
        {
            Name = "DriverPilotDisposable", Dock = DockStyle.Fill,
            Text = "Confirmo que este é um Windows descartável de laboratório",
            AccessibleName = "Confirmar computador Windows descartável"
        };
        var status = new Label
        {
            Name = "DriverPilotStatus", Dock = DockStyle.Fill,
            AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Estado da atualização piloto WUA"
        };
        var nonOwningHint = new ToolTip();
        form.Disposed += (_, _) => nonOwningHint.Dispose();
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        var start = new Button
        {
            Name = "DriverPilotStart", Dock = DockStyle.Fill, Enabled = false,
            Text = backendForTest is not null && journalForTest is not null
                ? "Executar atualização simulada" : "Instalação bloqueada",
            AccessibleName = "Iniciar piloto em ambiente descartável",
            AccessibleDescription = "Execução nativa indisponível nesta compilação normal."
        };
        var simulate = new Button
        {
            Name = "DriverPilotSimulatePlan", Dock = DockStyle.Fill, Enabled = false,
            Text = "Simular etapas", AccessibleName = "Exibir plano textual sem executar driver",
            AccessibleDescription = "Somente texto local; não chama backend, journal, rede ou instalação."
        };
        var cancel = new Button
        {
            Name = "DriverPilotCancel", Dock = DockStyle.Fill,
            Text = "Fechar", AccessibleName = "Fechar ou cancelar a espera"
        };
        actions.Controls.Add(start, 0, 0);
        actions.Controls.Add(simulate, 1, 0);
        actions.Controls.Add(cancel, 2, 0);
        root.Controls.Add(headline, 0, 0);
        root.Controls.Add(search, 0, 1);
        root.Controls.Add(choices, 0, 2);
        root.Controls.Add(detail, 0, 3);
        root.Controls.Add(confirm, 0, 4);
        root.Controls.Add(isolated, 0, 5);
        root.Controls.Add(status, 0, 6);
        root.Controls.Add(actions, 0, 7);
        form.Controls.Add(root);

        // ToolTip não participa da identidade do snapshot.
        void SetStatus(string message)
        {
            if (form.IsDisposed) return;
            status.Text = message;
            status.AccessibleDescription = message;
            nonOwningHint.SetToolTip(status, message);
        }
        bool CanReview()
        {
            try { return !form.IsDisposed && isCurrent(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
        }
        CancellationTokenSource? active = null;
        bool busy = false;
        bool invalidated = false;
        void UpdateEligibility()
        {
            if (form.IsDisposed) return;
            if (!CanReview()) invalidated = true;
            bool validChoice = choices.SelectedItem is ChoiceRow;
            simulate.Enabled = !invalidated && !busy && validChoice;
            simulate.AccessibleDescription = invalidated
                ? "Fotografia revogada; prévia indisponível."
                : simulate.Enabled
                    ? "Mostra somente um plano textual; nenhuma etapa é executada."
                    : "Selecione uma oferta para visualizar um plano textual sem executar.";
            start.Enabled = !invalidated && !busy && validChoice &&
                confirm.Checked && isolated.Checked &&
                backendForTest is not null && journalForTest is not null;
            start.AccessibleDescription = invalidated
                ? "Fotografia revogada; reabra a investigação."
                : backendForTest is null || journalForTest is null
                    ? "Nenhuma instalação habilitada na versão pessoal."
                    : "Ação sintética de laboratório. Não chama o Windows Update real.";
            if (invalidated)
            {
                confirm.Checked = false;
                isolated.Checked = false;
                detail.Text = "Fotografia ou seleção revogada. Feche e reabra a investigação.";
                SetStatus("Fotografia ou seleção revogada: nenhuma operação permitida.");
            }
        }
        void Filter()
        {
            if (form.IsDisposed) return;
            string query = search.Text.Trim();
            choices.BeginUpdate();
            try
            {
                choices.Items.Clear();
                foreach (var row in available.Where(r =>
                    query.Length == 0 || r.Choice.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.Choice.Provider.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    r.Choice.UpdateId.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    choices.Items.Add(row);
            }
            finally { choices.EndUpdate(); }
            confirm.Checked = false;
            isolated.Checked = false;
            UpdateEligibility();
        }
        search.TextChanged += (_, _) => Filter();
        choices.SelectedIndexChanged += (_, _) =>
        {
            confirm.Checked = false;
            isolated.Checked = false;
            detail.Text = choices.SelectedItem is not ChoiceRow row
                ? "Nenhuma oferta selecionada."
                : "Título: " + row.Choice.Title + Environment.NewLine +
                    "UpdateID: " + row.Choice.UpdateId + " / revisão: " + row.Choice.Revision +
                    Environment.NewLine + "Dispositivo: " + row.Choice.DeviceId +
                    Environment.NewLine + "HardwareID: " + row.Choice.HardwareId +
                    Environment.NewLine + "Fornecedor: " + row.Choice.Provider +
                    Environment.NewLine + "INF instalado: " + row.Choice.InfName +
                    Environment.NewLine + "Versão instalada: " + row.Choice.InstalledVersion +
                    Environment.NewLine + "VERSÃO OFERTADA: NÃO COMPROVADA. " +
                    "Esta oferta consultiva não prova atualização numericamente superior." +
                    Environment.NewLine + "Nenhuma instalação nesta compilação normal.";
            UpdateEligibility();
        };
        confirm.CheckedChanged += (_, _) => UpdateEligibility();
        isolated.CheckedChanged += (_, _) => UpdateEligibility();
        form.Activated += (_, _) => UpdateEligibility();
        form.Enter += (_, _) => UpdateEligibility();
        simulate.Click += (_, _) =>
        {
            UpdateEligibility();
            if (!simulate.Enabled || choices.SelectedItem is not ChoiceRow row ||
                !available.Contains(row) || !CanReview()) return;
            // Apenas StringBuilder/string.Join; nenhum backend ou journal é criado.
            detail.Text = DriverPilotPreview.Format(row.Choice);
            confirm.Checked = false;
            isolated.Checked = false;
            SetStatus("Simulação textual exibida. Nenhum passo executado; nenhuma autorização concedida.");
            UpdateEligibility();
        };
        start.Click += async (_, _) =>
        {
            UpdateEligibility();
            if (!start.Enabled || busy || choices.SelectedItem is not ChoiceRow row ||
                backendForTest is null || journalForTest is null) return;
            if (!CanReview() || !available.Contains(row)) return;
            var consent = new DriverUpdatePilotApproval(row.Choice.UpdateId, row.Choice.Revision,
                row.Choice.DeviceId, confirm.Checked, isolated.Checked, DateTimeOffset.UtcNow);
            IDriverUpdatePilotJournal journal;
            try { journal = journalForTest(); }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                SetStatus("Journal indisponível; execução bloqueada.");
                return;
            }
            busy = true;
            active = new CancellationTokenSource();
            UpdateEligibility();
            cancel.Text = "Cancelar espera";
            SetStatus("Validação inicial do dispositivo e da oferta…");
            try
            {
                var result = await DriverUpdatePilot.Execute(
                    row.Choice, consent, backendForTest, journal, active.Token,
                    stillCurrent: () => !form.IsDisposed && !invalidated && CanReview(),
                    progress: message =>
                    {
                        if (form.IsDisposed) return;
                        if (form.InvokeRequired)
                            form.BeginInvoke((Action)(() => SetStatus(message)));
                        else SetStatus(message);
                    });
                if (!form.IsDisposed)
                    SetStatus("Resultado " + result.State + ": " + result.Reason);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (!form.IsDisposed)
                    SetStatus("Falha do piloto: estado não confirmado. " + ex.GetType().Name);
            }
            finally
            {
                if (journal is IDisposable resource) resource.Dispose();
                active.Dispose();
                active = null;
                busy = false;
                if (!form.IsDisposed)
                {
                    cancel.Text = "Fechar";
                    // Proíbe nova tentativa automática, mesmo após resultado positivo.
                    confirm.Checked = false;
                    isolated.Checked = false;
                    UpdateEligibility();
                }
            }
        };
        cancel.Click += (_, _) =>
        {
            if (busy)
            {
                active?.Cancel();
                SetStatus("Cancelamento solicitado. Search/Download/Install COM pode continuar; não repetir.");
            }
            else form.Close();
        };
        form.FormClosing += (_, _) => active?.Cancel();
        Filter();
        SetStatus(available.Length == 0
            ? "Não há oferta WUA com identidade única e classe permitida nesta fotografia."
            : "Selecione uma oferta e revise sua identidade. Execução nativa bloqueada na versão pessoal.");
        return form;
    }
}
