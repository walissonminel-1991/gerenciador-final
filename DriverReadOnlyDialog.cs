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
        TimeSpan? timeoutForTest = null)
    {
        using var dialog = new Form
        {
            Text = "Drivers — investigar sem alterações", Width = 800, Height = 530,
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
            AccessibleName = "Relatório de drivers somente leitura"
        };
        body.UseReadableFont(new Font("Segoe UI", 9F));
        var status = new Label { Dock = DockStyle.Fill, ForeColor = Muted,
            AutoEllipsis = true, Text = "Consultas locais; os IDs nesta janela não são enviados ao diagnóstico." };
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        var officialButton = CreateSecondaryButton("Versões oficiais");
        officialButton.Name = "DriverOfficialReadOnly";
        var verifyButton = CreateSecondaryButton("Verificar INF");
        verifyButton.Name = "DriverVerifyInfReadOnly";
        var close = CreateSecondaryButton("Fechar");
        foreach (var button in new[] { officialButton, verifyButton, close })
        { button.Dock = DockStyle.Fill; button.Margin = new Padding(2); button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold); }
        bar.Controls.Add(officialButton, 0, 0); bar.Controls.Add(verifyButton, 1, 0); bar.Controls.Add(close, 2, 0);
        root.Controls.Add(body, 0, 0); root.Controls.Add(status, 0, 1); root.Controls.Add(bar, 0, 2);
        dialog.Controls.Add(root);
        dialog.HandleCreated += (_, _) =>
        {
            try { int dark = 1; if (NativeMethods.DwmSetWindowAttribute(dialog.Handle, 20, ref dark, 4) != 0)
                    _ = NativeMethods.DwmSetWindowAttribute(dialog.Handle, 19, ref dark, 4); }
            catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        };
        DriverOfficialResult? official = null;
        DriverInfVerificationResult? verification = null;
        CancellationTokenSource? current = null;
        bool canVerify = selected?.PackageEvidence?.State == DriverStoreState.Mapped;
        bool obsolete = false;
        bool closing = false;
        const string obsoleteText = "DRIVERS — INVESTIGAÇÃO SOMENTE LEITURA\r\n" +
            "Inventário ou seleção alterados. Feche esta janela e abra uma nova investigação. " +
            "Nenhum resultado antigo será publicado.";
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
            body.SetText(obsoleteText);
            status.Text = "Inventário ou seleção alterados; reabra a investigação.";
            officialButton.Enabled = false;
            verifyButton.Enabled = false;
        }
        void Render()
        {
            if (body.IsDisposed) return;
            CheckCurrent();
            if (!obsolete)
                body.SetText(DriverReadOnlyReport.Format(inventory, selected, official, verification));
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
            button.Text = "Cancelar";
            officialButton.Enabled = button == officialButton;
            verifyButton.Enabled = button == verifyButton;
            status.Text = "Consultando em segundo plano… Nenhuma instalação ou remoção.";
            try
            {
                bool accepted = await read(source.Token);
                if (!dialog.IsDisposed && !closing)
                {
                    Render();
                    CheckCurrent();
                    if (!obsolete)
                        status.Text = accepted
                            ? "Consulta encerrada. Resultados consultivos; nenhuma alteração em drivers."
                            : "Resultado inconsistente descartado; relatório anterior preservado.";
                }
            }
            catch (OperationCanceledException)
            {
                if (!dialog.IsDisposed && !closing)
                {
                    CheckCurrent();
                    if (!obsolete)
                        status.Text = "Consulta cancelada ou prazo esgotado; resultado anterior preservado.";
                }
            }
            catch (Exception ex)
            {
                if (!dialog.IsDisposed && !closing)
                {
                    CheckCurrent();
                    if (!obsolete)
                        status.Text = "Consulta indisponível (" + ex.GetType().Name + "); resultado anterior preservado.";
                }
            }
            finally
            {
                current = null;
                if (!dialog.IsDisposed && !closing)
                {
                    button.Text = label;
                    CheckCurrent();
                    officialButton.Enabled = !obsolete && !inventory.Partial && inventory.Items.Count > 0;
                    verifyButton.Enabled = !obsolete && canVerify;
                }
            }
        }
        officialButton.Click += async (_, _) =>
        {
            if (current is null && _driverOfficialReadTask is { IsCompleted: false })
            { CheckCurrent(); if (!obsolete) status.Text = "Consulta nativa anterior ainda encerrando; resultado preservado."; return; }
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
        };
        verifyButton.Click += async (_, _) =>
        {
            if (current is null && _driverInfReadTask is { IsCompleted: false })
            { CheckCurrent(); if (!obsolete) status.Text = "Verificação nativa anterior ainda encerrando; resultado preservado."; return; }
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
        };
        close.Click += (_, _) => dialog.Close();
        dialog.FormClosed += (_, _) =>
        {
            closing = true;
            current?.Cancel();
            official = null;
            verification = null;
        };
        Render();
        dialog.Shown += (_, _) => officialButton.Focus();
        onInvalidationChanged(CheckCurrent);
        try { dialog.ShowDialog(this); }
        finally { onInvalidationChanged(null); }
    }
}
