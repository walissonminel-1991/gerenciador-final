using System.Drawing;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal sealed partial class MainForm
{
    private Task<DriverOfficialResult>? _driverOfficialReadTask;
    private Task<DriverInfVerificationResult>? _driverInfReadTask;

    private void ShowDriverReadOnlyReport(DriverInventoryResult inventory, DriverInventoryEntry? selected,
        Func<bool> isCurrent, Func<CancellationToken, DriverOfficialResult, bool> canPublishOfficial,
        Func<CancellationToken, DriverInfVerificationResult, bool> canPublishInf,
        Action<Action?> onInvalidationChanged,
        Func<CancellationToken, Task<DriverOfficialResult>>? officialReadForTest = null,
        Func<CancellationToken, Task<DriverInfVerificationResult>>? infReadForTest = null,
        TimeSpan? timeoutForTest = null,
        Action<Action, Action>? onTestActionsReady = null,
        bool startOfficialOnShow = false)
    {
        using var dialog = new Form
        {
            Text = "Drivers — detalhes e evidências", Width = 800, Height = 530,
            MinimumSize = new Size(500, 340), StartPosition = FormStartPosition.CenterParent,
            BackColor = PanelBackground, ForeColor = Ink, Font = Font,
            ShowInTaskbar = false, MinimizeBox = false
        };
        var working = Screen.FromControl(this).WorkingArea;
        dialog.Size = new Size(Math.Min(dialog.Width, working.Width - 24), Math.Min(dialog.Height, working.Height - 36));
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(10), BackColor = PanelBackground };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        var body = new DarkLogView
        {
            Name = "DriverReadOnlyReportText", Dock = DockStyle.Fill,
            AccessibleName = "Relatório de drivers somente leitura",
            AccessibleDescription = "Texto consultivo. Use Tab para acessar as ações; nenhuma ação modifica drivers."
        };
        body.UseReadableFont(new Font("Segoe UI", 9F));
        var status = new Label { Dock = DockStyle.Fill, ForeColor = Muted,
            Name = "DriverReadOnlyStatus", AccessibleName = "Estado da investigação de drivers",
            AutoEllipsis = true };
         void SetStatus(string message)
        {
            if (status.IsDisposed) return;
            status.Text = message;
            // Mensagens extensas podem ser elididas visualmente em 500×340.
            status.AccessibleDescription = message;
            // A descrição completa está acessível, sem pop-up ao passar o mouse.
        }
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        var officialButton = CreateSecondaryButton("Versões oficiais");
        officialButton.Name = "DriverOfficialReadOnly";
        officialButton.AccessibleName = "Consultar ofertas oficiais de drivers, somente leitura";
        officialButton.AccessibleDescription = "Metadados do Windows Update; não comprovam compatibilidade nem permitem instalação.";
        var verifyButton = CreateSecondaryButton("Verificar INF");
        verifyButton.Name = "DriverVerifyInfReadOnly";
        verifyButton.AccessibleName = "Verificar assinatura do INF instalado, somente leitura";
        verifyButton.AccessibleDescription = "Verifica somente o INF local associado à seleção, sem modificar drivers.";
        var close = CreateSecondaryButton("Fechar");
        close.AccessibleName = "Fechar investigação de drivers";
        close.DialogResult = DialogResult.Cancel;
        dialog.CancelButton = close;
        foreach (var button in new[] { officialButton, verifyButton, close })
        { button.Dock = DockStyle.Fill; button.Margin = new Padding(2); button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold); }
        bar.Controls.Add(officialButton, 0, 0); bar.Controls.Add(verifyButton, 1, 0); bar.Controls.Add(close, 2, 0);
        // Link de revisão F3, preservando os TRES botões anteriores e as
        // regressões de acessibilidade G1.10–G1.23.
        var statusRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        var pilotLink = new LinkLabel
        {
            Name = "DriverPilotReviewLink", Dock = DockStyle.Fill,
            Text = "Piloto WUA (revisar)", TextAlign = ContentAlignment.MiddleRight,
            LinkColor = Accent, Enabled = false,
            AccessibleName = "Revisar piloto WUA experimental",
            AccessibleDescription = "Consulta WUA; opção de instalação G3.2 só com administrador, pacote OEM elegível, backup e consentimento."
        };
        statusRow.Controls.Add(status, 0, 0);
        statusRow.Controls.Add(pilotLink, 1, 0);
        root.Controls.Add(body, 0, 0); root.Controls.Add(statusRow, 0, 1); root.Controls.Add(bar, 0, 2);
        dialog.Controls.Add(root);
        dialog.HandleCreated += (_, _) =>
        {
            try { int dark = 1; if (NativeMethods.DwmSetWindowAttribute(dialog.Handle, 20, ref dark, 4) != 0)
                    _ = NativeMethods.DwmSetWindowAttribute(dialog.Handle, 19, ref dark, 4); }
            catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        };
        // Reaproveita somente o WUA da fotografia exata consultada na aba.
        // A assinatura histórica do método é preservada para os testes WinForms.
        DriverOfficialResult? official = officialReadForTest is null &&
            ReferenceEquals(inventory, _driverReusableInventory) &&
            _driverReusableOfficial is { Partial: false } known ? known : null;
        DriverInfVerificationResult? verification = null;
        CancellationTokenSource? current = null;
        // A identidade é julgada sobre o INVENTÁRIO COMPLETO, nunca apenas
        // sobre as linhas que um filtro deixou visíveis.
        bool duplicatedIds = inventory.Items.GroupBy(x => x.DeviceId.Trim(),
            StringComparer.OrdinalIgnoreCase).Any(group =>
                string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        bool ambiguousSelection = selected is not null &&
            (string.IsNullOrWhiteSpace(selected.DeviceId) ||
             inventory.Items.Count(x => string.Equals(x.DeviceId, selected.DeviceId,
                 StringComparison.OrdinalIgnoreCase)) != 1);
        bool validAtOpening = isCurrent();
        bool canOfficial = validAtOpening && !inventory.Partial &&
            inventory.Items.Count > 0 && !duplicatedIds;
        bool canVerify = validAtOpening && !ambiguousSelection &&
            selected is { WithoutSignedDriverAssociation: false,
                PackageEvidence.State: DriverStoreState.Mapped };
        // Nenhuma tarefa WUA/INF deve ser iniciada com identidade inicialmente
        // ambígua/incoerente; o guard G1.9 continua sendo a autoridade final.
        officialButton.Enabled = canOfficial;
        verifyButton.Enabled = canVerify;
        // Consultar drivers já procurou ofertas WUA. Não pedir o mesmo clique.
        if (official is not null && officialReadForTest is null)
        {
            officialButton.Visible = false;
            bar.ColumnStyles[0].Width = 0;
            bar.ColumnStyles[1].Width = 60;
            bar.ColumnStyles[2].Width = 40;
        }
        verifyButton.AccessibleDescription = canVerify
            ? "Verifica somente o INF local associado à seleção, sem modificar drivers."
            : !validAtOpening
                ? "Indisponível: identidade ou evidência incoerente já na abertura; nenhuma verificação INF será iniciada."
                : DriverReadOnlyReport.InfAvailability(selected);
        SetStatus(!validAtOpening
            ? ambiguousSelection
                ? "DeviceId duplicado ou ausente no inventário completo; INF/WUA bloqueados."
                : "Evidência incoerente já na abertura; INF/WUA bloqueados."
            : duplicatedIds
                ? "DeviceId duplicado no inventário completo; consulta WUA indisponível."
                : inventory.Partial
                    ? "Inventário parcial: consulta oficial indisponível; INF local depende de vínculo mapeado."
                    : inventory.Items.Count == 0
                        ? "Inventário vazio nesta leitura: nenhuma consulta oficial disponível."
                        : selected is null
                            ? "Sem dispositivo selecionado: a consulta oficial é geral; verificar INF exige seleção."
                            : "Consultas locais; os IDs nesta janela não são enviados ao diagnóstico.");
        bool obsolete = false;
        bool closing = false;
        const string obsoleteText = "DRIVERS — INVESTIGAÇÃO SOMENTE LEITURA\r\n" +
            "Inventário ou seleção alterados. Feche esta janela e abra uma nova investigação. " +
            "Nenhum resultado antigo será publicado.";
        const string invalidOpeningText = "DRIVERS — INVESTIGAÇÃO SOMENTE LEITURA\r\n" +
            "A evidência já era incoerente na abertura. A seleção ou o vínculo de INF " +
            "não podem ser confirmados; nenhuma consulta ou resultado INF/WUA será publicado.";
        const string duplicateOpeningText = "DRIVERS — INVESTIGAÇÃO SOMENTE LEITURA\r\n" +
            "DeviceId duplicado ou ausente no inventário completo ANTES da abertura. " +
            "A seleção visual não comprova qual INF pertence a esta instância. " +
            "Consulta oficial e verificação INF bloqueadas; nenhum resultado será publicado.";
        void CheckCurrent()
        {
            if (dialog.IsDisposed || closing || obsolete) return;
            // O diálogo é modal, mas eventos de inventário/seleção ainda
            // podem invalidar a fotografia; não confundir foco com validade.
            if (isCurrent()) return;
            obsolete = true;
            official = null;
            verification = null;
            current?.Cancel(); // cancela a espera, não força fim do Search COM
            body.SetText(!validAtOpening
                ? ambiguousSelection ? duplicateOpeningText : invalidOpeningText
                : obsoleteText);
            SetStatus(!validAtOpening
                ? ambiguousSelection
                    ? "Identidade DeviceId já ambígua na abertura; consultas INF/WUA bloqueadas."
                    : "Evidência local já incoerente na abertura; consultas INF/WUA bloqueadas."
                : "Inventário ou seleção alterados; reabra a investigação.");
            officialButton.Enabled = false;
            verifyButton.Enabled = false;
            pilotLink.Enabled = false;
        }
        void Render()
        {
            if (body.IsDisposed) return;
            CheckCurrent();
            if (!obsolete)
                body.SetText(DriverReadOnlyReport.Format(inventory, selected, official, verification));
            pilotLink.Enabled = !obsolete && !closing && selected is not null &&
                official is { Partial: false } result && isCurrent() &&
                result.Matches.Any(m => DriverUpdatePilot.TryPrepare(inventory, result, m, out _) &&
                    string.Equals(m.DeviceId, selected.DeviceId, StringComparison.OrdinalIgnoreCase));
        }
        // Revalidação só nos eventos de estado e foco, sem thread/timer novo.
        dialog.Activated += (_, _) => CheckCurrent();
        dialog.Enter += (_, _) => CheckCurrent();
        async Task Run(Button button, Func<CancellationToken, Task<bool>> read, TimeSpan timeout)
        {
            CheckCurrent();
            if (obsolete) return;
            if (current is not null) { current.Cancel(); return; }
            using var source = new CancellationTokenSource(timeout);
            current = source;
            string label = button.Text;
            string? accessibleLabel = button.AccessibleName;
            string? accessibleDescription = button.AccessibleDescription;
            button.Text = "Cancelar";
            button.AccessibleName = button == officialButton
                ? "Cancelar consulta oficial de drivers" : "Cancelar verificação do INF";
            button.AccessibleDescription =
                "Solicita cancelamento da espera. Uma operação nativa síncrona pode continuar em execução.";
            officialButton.Enabled = button == officialButton;
            verifyButton.Enabled = button == verifyButton;
            SetStatus("Consultando em segundo plano… Nenhuma instalação ou remoção.");
            try
            {
                bool accepted = await read(source.Token);
                if (!dialog.IsDisposed && !closing)
                {
                    Render();
                    CheckCurrent();
                    if (!obsolete)
                        SetStatus(accepted
                            ? "Consulta encerrada. Resultados consultivos; nenhuma alteração em drivers."
                            : "Resultado inconsistente descartado; relatório anterior preservado.");
                }
            }
            catch (OperationCanceledException)
            {
                if (!dialog.IsDisposed && !closing)
                {
                    CheckCurrent();
                    if (!obsolete)
                        SetStatus("Consulta cancelada ou prazo esgotado; resultado anterior preservado.");
                }
            }
            catch (Exception ex)
            {
                if (!dialog.IsDisposed && !closing)
                {
                    CheckCurrent();
                    if (!obsolete)
                        SetStatus("Consulta indisponível (" + ex.GetType().Name + "); resultado anterior preservado.");
                }
            }
            finally
            {
                current = null;
                if (!dialog.IsDisposed && !closing)
                {
                    button.Text = label;
                    button.AccessibleName = accessibleLabel;
                    button.AccessibleDescription = accessibleDescription;
                    CheckCurrent();
                    officialButton.Enabled = !obsolete && canOfficial;
                    verifyButton.Enabled = !obsolete && canVerify;
                }
            }
        }
        async Task RunOfficial()
        {
            CheckCurrent();
            if (obsolete || closing || !officialButton.Enabled) return;
            if (current is null && _driverOfficialReadTask is { IsCompleted: false })
            { CheckCurrent(); if (!obsolete) SetStatus("Consulta nativa anterior ainda encerrando; resultado preservado."); return; }
            await Run(officialButton, async token =>
            {
                _driverOfficialReadTask = officialReadForTest is null
                    ? DriverOfficialLookup.Start(inventory, token) : officialReadForTest(token);
                var result = await _driverOfficialReadTask.WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (dialog.IsDisposed || closing || !canPublishOfficial(token, result)) return false;
                official = result;
                return true;
            }, timeoutForTest ?? TimeSpan.FromSeconds(90));
        }
        async Task RunInf()
        {
            CheckCurrent();
            if (obsolete || closing || !verifyButton.Enabled) return;
            if (current is null && _driverInfReadTask is { IsCompleted: false })
            { CheckCurrent(); if (!obsolete) SetStatus("Verificação nativa anterior ainda encerrando; resultado preservado."); return; }
            if (selected?.PackageEvidence is not { } package) return;
            await Run(verifyButton, async token =>
            {
                _driverInfReadTask = infReadForTest is null
                    ? Task.Run(() => DriverInfVerification.Verify(package, token))
                    : infReadForTest(token);
                var result = await _driverInfReadTask.WaitAsync(token);
                token.ThrowIfCancellationRequested();
                if (dialog.IsDisposed || closing || !canPublishInf(token, result)) return false;
                verification = result;
                return true;
            }, timeoutForTest ?? TimeSpan.FromSeconds(25));
        }
        pilotLink.LinkClicked += (_, _) =>
        {
            CheckCurrent();
            if (obsolete || closing || !pilotLink.Enabled ||
                selected is null || official is not { Partial: false } offers || !isCurrent()) return;
            // G3.2: backend e diário reais; executar exige prova de recuperação e aceite explícito.
            using var preview = DriverUpdatePilotDialog.Create(inventory, offers, selected,
                isCurrent, DriverG31Wiring.NewProductionBackend(),
                DriverG31Wiring.NewProductionJournalFactory());
            preview.ShowDialog(dialog);
            CheckCurrent();
        };
        officialButton.Click += async (_, _) => await RunOfficial();
        verifyButton.Click += async (_, _) => await RunInf();
        // Costura exclusivamente de fixture: invoca os mesmos handlers da UI,
        // sem depender do foco/PerformClick do runner Windows.
        onTestActionsReady?.Invoke(() => _ = RunOfficial(), () => _ = RunInf());
        close.Click += (_, _) => dialog.Close();
        dialog.FormClosed += (_, _) =>
        {
            closing = true;
            current?.Cancel();
            official = null;
            verification = null;
            pilotLink.Enabled = false;
        };
        Render();
        dialog.Shown += (_, _) =>
        {
            // Não enviar o foco inicial para uma ação indisponível.
            (officialButton.Enabled ? officialButton : verifyButton.Enabled ? verifyButton : close).Focus();
            if (startOfficialOnShow && officialButton.Enabled) _ = RunOfficial();
        };
        onInvalidationChanged(CheckCurrent);
        try { dialog.ShowDialog(this); }
        finally { onInvalidationChanged(null); }
    }
}
