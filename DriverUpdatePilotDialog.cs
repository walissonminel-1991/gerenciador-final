using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

// G3.2: WUA nativo só após consentimento físico explícito, OEM seguro,
// backup exportado e conferido, diário válido e revalidação transacional.
// A consulta G3.1 continua somente leitura; fixtures não usam WUA real.
// Fixtures sinteticas mantem o contrato historico do piloto completo.
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
        bool nativeWua = DriverG31Wiring.IsNativeBackend(backendForTest);
        var nativeReadGate = new DriverG31ReadGate();

        var form = new Form
        {
            Name = "DriverPilotReviewForm",
            Text = nativeWua ? "Drivers — atualização WUA controlada (G3.2)" :
                "Drivers — piloto WUA experimental (revisão)",
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
            Text = nativeWua ? "G3.2 — somente pacote OEM elegível com backup comprovado" :
                "PILOTO EXPERIMENTAL — seleção por dispositivo e UpdateID",
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
            Text = nativeWua
                ? "Autorizo instalar exatamente a oferta e o dispositivo selecionados"
                : "Confirmo a oferta selecionada (sem atualização automática)",
            Enabled = true,
            AccessibleName = "Confirmar explicitamente UpdateID, revisão e dispositivo"
        };
        var isolated = new CheckBox
        {
            Name = "DriverPilotDisposable", Dock = DockStyle.Fill,
            Text = nativeWua
                ? "Aceito o risco no PC físico e exijo backup OEM verificado antes do download"
                : "Confirmo que este é um Windows descartável de laboratório",
            Enabled = true,
            AccessibleName = nativeWua ? "Autorizar operação física com recuperação OEM"
                : "Confirmar computador Windows descartável"
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
            Text = nativeWua ? "Instalar com backup OEM" :
                backendForTest is not null && journalForTest is not null
                    ? "Executar atualização simulada" : "Instalação bloqueada",
            AccessibleName = nativeWua ? "Iniciar instalação WUA controlada"
                : "Iniciar piloto em ambiente descartável",
            AccessibleDescription = nativeWua
                ? "Exige administrador, OEM único não crítico, consentimento, backup e diário. Sem reinício automático."
                : "Execução nativa indisponível nesta compilação normal."
        };
        var simulate = new Button
        {
            Name = "DriverPilotSimulatePlan", Dock = DockStyle.Fill, Enabled = false,
            Text = nativeWua ? "Revalidar via WUA" : "Simular etapas",
            AccessibleName = nativeWua ? "Revalidar oferta no Windows Update sem instalar" :
                "Exibir plano textual sem executar driver",
            AccessibleDescription = nativeWua
                ? "Consulta real ao Windows Update; não baixa, instala ou modifica drivers."
                : "Somente texto local; não chama backend, journal, rede ou instalação."
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
            if (nativeWua && nativeReadGate.HasPendingWork)
                simulate.Enabled = false;
            simulate.AccessibleDescription = invalidated
                ? nativeWua ? "Fotografia revogada; consulta WUA indisponível."
                    : "Fotografia revogada; prévia indisponível."
                : simulate.Enabled
                    ? nativeWua
                        ? "Revalida a oferta no Windows Update real, sem download ou instalação."
                        : "Mostra somente um plano textual; nenhuma etapa é executada."
                    : nativeWua
                        ? nativeReadGate.HasPendingWork
                            ? "Consulta WUA anterior ainda ativa; aguarde o fim da operação nativa."
                            : "Selecione uma oferta válida para consultar o WUA."
                        : "Selecione uma oferta para visualizar um plano textual sem executar.";
            bool physicalEligible = nativeWua && choices.SelectedItem is ChoiceRow selectedRow &&
                DriverG32RecoveryTicket.CanOffer(inventory, selected, selectedRow.Choice) &&
                OperatingSystem.IsWindows() && DriverSilentUninstall.IsElevated() &&
                !DriverG32RecoveryTicket.HasActiveOperation &&
                !nativeReadGate.HasPendingWork &&
                DriverUpdatePilotFileJournal.MayBeginReadOnly(
                    DriverUpdatePilotFileJournal.SuggestedPath);
            start.Enabled = !invalidated && !busy && validChoice &&
                confirm.Checked && isolated.Checked &&
                (nativeWua ? physicalEligible :
                    DriverG31Wiring.CanExecutePilot(backendForTest, journalForTest));
            start.AccessibleDescription = invalidated
                ? "Fotografia revogada; reabra a investigação."
                : nativeWua
                    ? "G3.2: administrador, OEM individual não crítico, assinatura, backup e diário obrigatórios. " +
                      DriverUpdatePilotFileJournal.ReadOnlyStatus(
                          DriverUpdatePilotFileJournal.SuggestedPath)
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
                    Environment.NewLine + (nativeWua ?
                        "G3.2: revalidação consultiva disponível. Instalação somente com backup e consentimento." :
                        "Nenhuma instalação nesta compilação normal.");
            UpdateEligibility();
        };
        confirm.CheckedChanged += (_, _) => UpdateEligibility();
        isolated.CheckedChanged += (_, _) => UpdateEligibility();
        form.Activated += (_, _) => UpdateEligibility();
        form.Enter += (_, _) => UpdateEligibility();
        // Acompanhamento passivo da Task COM apos timeout/cancelamento.
        // Nunca publica resultado tardio como resultado da consulta atual.
        async Task NotifyWhenNativeCompletes(Task<DriverUpdatePilotCheck> nativeTask)
        {
            try { await nativeTask.ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { }
            try
            {
                if (form.IsDisposed || !form.IsHandleCreated) return;
                form.BeginInvoke((Action)(() =>
                {
                    if (form.IsDisposed || busy ||
                        !nativeReadGate.IsLatestCompleted(nativeTask)) return;
                    UpdateEligibility();
                    if (CanReview())
                        SetStatus("Pesquisa WUA anterior encerrada. Revalide novamente para obter resultado atual.");
                }));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
        }
        // Se uma janela anterior foi fechada com COM ainda ativo, observar
        // passivamente a conclusao para reabilitar a UI desta nova janela.
        form.Shown += (_, _) =>
        {
            if (nativeWua && nativeReadGate.HasPendingWork &&
                nativeReadGate.LatestTask is { } workFromEarlierWindow)
            {
                UpdateEligibility();
                SetStatus("Pesquisa WUA anterior ainda ativa; consulta nesta janela bloqueada.");
                _ = NotifyWhenNativeCompletes(workFromEarlierWindow);
            }
        };
        simulate.Click += async (_, _) =>
        {
            UpdateEligibility();
            if (!simulate.Enabled || choices.SelectedItem is not ChoiceRow row ||
                !available.Contains(row) || !CanReview()) return;
            if (nativeWua)
            {
                // Chama a API WUA REAL somente por Revalidate: nunca Download/Install.
                // Cancelar a espera nao afirma interromper a operacao COM sincrona.
                busy = true;
                using var source = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                active = source;
                UpdateEligibility();
                cancel.Text = "Cancelar espera";
                SetStatus("Revalidando no Windows Update real (somente leitura)…");
                try
                {
                    // WaitAsync pode cancelar APENAS a espera. Conservar a Task
                    // nativa para impedir segunda consulta enquanto o COM termina.
                    if (!nativeReadGate.TryStart(
                        () => backendForTest!.Revalidate(row.Choice, source.Token),
                        out var nativeTask))
                    {
                        SetStatus("Consulta WUA anterior ainda em andamento; nenhuma segunda consulta iniciada.");
                        return;
                    }
                    var check = await nativeTask!.WaitAsync(source.Token);
                    if (!form.IsDisposed && !invalidated && CanReview() &&
                        Equals(choices.SelectedItem, row))
                    {
                        detail.Text = "G3.2 — RESULTADO DA CONSULTA WUA REAL" + Environment.NewLine +
                            "Esta ação não baixou nem instalou drivers." + Environment.NewLine +
                            "Oferta: " + row.Choice.UpdateId + " / revisão " + row.Choice.Revision +
                            Environment.NewLine + "Origem oficial: " + check.OfficialSource +
                            Environment.NewLine + "UpdateID exato: " + check.ExactUpdate +
                            Environment.NewLine + "Mesmo dispositivo: " + check.SameDevice +
                            Environment.NewLine + "Aplicável agora: " + check.ApplicableNow +
                            Environment.NewLine + "Sem updates adicionais: " + check.NoAdditionalUpdates +
                            Environment.NewLine + "EULA já aceita: " + check.EulaAlreadyAccepted +
                            Environment.NewLine + "Não interativo: " + check.NonInteractive +
                            Environment.NewLine + "Sem reinício pendente: " + check.RebootNotPending +
                            Environment.NewLine + "Já baixado: " + check.Downloaded +
                            Environment.NewLine + "Instalação: condicionada a autorização, backup OEM e diário G3.2.";
                        SetStatus(check.BeforeDownload
                            ? "Oferta revalidada; nenhuma instalação iniciada nesta consulta."
                            : "Oferta não passou pelos guardas consultivos; GF não solicitou alterações.");
                    }
                }
                catch (OperationCanceledException)
                {
                    if (!form.IsDisposed) SetStatus(
                        "Espera cancelada/esgotada; a chamada WUA pode ainda estar encerrando.");
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    if (!form.IsDisposed) SetStatus(
                        "Revalidação WUA indisponível (" + ex.GetType().Name + "); sem alterações.");
                }
                finally
                {
                    active = null;
                    busy = false;
                    if (!form.IsDisposed)
                    {
                        cancel.Text = "Fechar";
                        UpdateEligibility();
                        if (nativeReadGate.HasPendingWork)
                        {
                            SetStatus("A espera terminou, mas o WUA ainda executa; outra consulta segue bloqueada.");
                            _ = NotifyWhenNativeCompletes(nativeReadGate.LatestTask!);
                        }
                    }
                }
                return;
            }
            // Fixtures: plano textual sem backend, journal ou rede.
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
                backendForTest is null || journalForTest is null ||
                !(nativeWua
                    ? DriverG32RecoveryTicket.CanOffer(inventory, selected, row.Choice)
                    : DriverG31Wiring.CanExecutePilot(backendForTest, journalForTest)) ||
                !CanReview() || !available.Contains(row)) return;
            if (nativeWua && MessageBox.Show(form,
                "Autoriza instalar UM driver pelo Windows Update neste computador?" +
                Environment.NewLine + "UpdateID: " + row.Choice.UpdateId +
                " / revisão: " + row.Choice.Revision +
                Environment.NewLine + "Dispositivo: " + row.Choice.DeviceId +
                Environment.NewLine + "Classe: " + row.Choice.DriverClass +
                Environment.NewLine + "Um backup OEM verificado será obrigatório antes do download." +
                Environment.NewLine + "Não há rollback automático; o Windows pode exigir reinício.",
                "Confirmar instalação física de driver", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            bool gateHeld = false;
            IDriverUpdatePilotJournal? journal = null;
            DriverG32RecoveryTicket? recovery = null;
            busy = true;
            active = new CancellationTokenSource();
            var operation = active;
            bool CurrentChoice() => !form.IsDisposed && !invalidated &&
                CanReview() && Equals(choices.SelectedItem, row) &&
                available.Contains(row);
            UpdateEligibility();
            cancel.Text = "Cancelar espera";
            SetStatus(nativeWua
                ? "G3.2: verificando OEM e preparando backup de recuperação..."
                : "Validação inicial do dispositivo e da oferta…");
            try
            {
                if (nativeWua)
                {
                    // Não há segundo piloto nativo concorrente neste processo.
                    if (!DriverG32RecoveryTicket.TryBeginOperation())
                    {
                        SetStatus("Outra atualização G3.2 ainda está ativa. Não repetir.");
                        return;
                    }
                    gateHeld = true;
                    var prepared = await DriverG32RecoveryTicket.PrepareAsync(
                        inventory, selected, row.Choice, CurrentChoice, operation.Token);
                    if (prepared.Ticket is null)
                    {
                        SetStatus(prepared.Reason);
                        return;
                    }
                    recovery = prepared.Ticket;
                    SetStatus(prepared.Reason + " Revalidando a oferta oficial...");
                }
                try { journal = journalForTest(); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    SetStatus("Diário indisponível; nenhuma instalação autorizada.");
                    return;
                }
                var consent = new DriverUpdatePilotApproval(
                    row.Choice.UpdateId, row.Choice.Revision, row.Choice.DeviceId,
                    confirm.Checked, !nativeWua && isolated.Checked, DateTimeOffset.UtcNow)
                {
                    PhysicalWindowsApproved = nativeWua && isolated.Checked
                };
                var result = await DriverUpdatePilot.Execute(
                    row.Choice, consent, backendForTest, journal, operation.Token,
                    stillCurrent: CurrentChoice,
                    progress: message =>
                    {
                        if (form.IsDisposed) return;
                        try
                        {
                            if (form.InvokeRequired)
                                form.BeginInvoke((Action)(() => SetStatus(message)));
                            else SetStatus(message);
                        }
                        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { }
                    },
                    recovery: recovery);
                if (!form.IsDisposed)
                    SetStatus("Resultado " + result.State + ": " + result.Reason +
                        (recovery is null ? "" : " Backup OEM preservado para recuperação manual."));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (!form.IsDisposed)
                    SetStatus("Resultado não confirmado: " + ex.GetType().Name +
                        ". Consulte diário e backup antes de repetir.");
            }
            finally
            {
                if (journal is IDisposable resource) resource.Dispose();
                if (gateHeld) DriverG32RecoveryTicket.EndOperation();
                operation.Dispose();
                active = null;
                busy = false;
                if (!form.IsDisposed)
                {
                    cancel.Text = "Fechar";
                    // Mesmo com sucesso, cada atualização requer nova autorização.
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
                SetStatus(nativeWua
                    ? "Cancelamento solicitado. WUA/backup podem continuar; confira o diário antes de repetir."
                    : "Cancelamento solicitado. Search/Download/Install COM pode continuar; não repetir.");
            }
            else form.Close();
        };
        form.FormClosing += (_, _) => active?.Cancel();
        Filter();
        SetStatus(available.Length == 0
            ? "Não há oferta WUA com identidade única e classe permitida nesta fotografia."
            : nativeWua
                ? "G3.2: instalação apenas com elegibilidade, administrador, backup e consentimento. " +
                    DriverUpdatePilotFileJournal.ReadOnlyStatus(
                        DriverUpdatePilotFileJournal.SuggestedPath)
                : "Selecione uma oferta e revise sua identidade. Execução nativa bloqueada na versão pessoal.");
        return form;
    }
}
