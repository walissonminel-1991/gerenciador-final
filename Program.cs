using Microsoft.Win32;
using System.IO.Pipes;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GerenciadorAplicativos;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // O trabalhador elevado executa comandos sem criar janelas auxiliares.
        if (args.Length == 4 && args[0] == "--worker")
            return WorkerHost.RunAsync(args[1], args[2], args[3]).GetAwaiter().GetResult();

        if (!DiagnosticTrace.Start() || !DiagnosticTrace.Write("APP.INICIALIZACAO_INICIO"))
        {
            MessageBox.Show(
                "Não foi possível criar e confirmar o registro de diagnóstico. " +
                "O teste foi interrompido antes de iniciar a consulta de pacotes.\n\n" +
                "Motivo técnico: " + DiagnosticTrace.LastFailure,
                "Diagnóstico indisponível", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
        try
        {
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => MessageBox.Show(
                "Ocorreu um erro inesperado.\n\n" + e.Exception.Message,
                "Gerenciador Final", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Run(new MainForm());
            DiagnosticTrace.Write("APP.ENCERRAMENTO_NORMAL");
            return 0;
        }
        catch (Exception ex)
        {
            DiagnosticTrace.Failure("APP.FALHA_NAO_TRATADA", ex);
            try { MessageBox.Show("Não foi possível iniciar o gerenciador.\n\n" + ex,
                    "Falha ao abrir", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
            return 1;
        }
    }
}

internal sealed class ProgramEntry
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string UninstallString { get; init; } = "";
    public string QuietUninstallString { get; init; } = "";
    public string InstallLocation { get; init; } = "";
    public string PackageId { get; set; } = "";
    public string CatalogIdentity { get; set; } = "";
    public string CatalogDisplayName { get; set; } = "";
    public string InventorySource { get; init; } = "";
    public string InventoryId { get; init; } = "";
    public string MsiProductCode { get; init; } = "";
    public bool VerifiedMsixIdentity { get; init; }
    // Entrada MSIX, jogo da biblioteca ou componente protegido: sem desinstalacao direta.
    public bool InventoryOnly { get; init; }
    public string AvailableVersion { get; set; } = "";
    public string Source { get; set; } = "";
    public bool IsUpdate { get; set; }
    public MatchState ComparisonState { get; set; } = MatchState.Unmatched;
}

internal sealed record RegistryInventoryScan(IReadOnlyList<ProgramEntry> Items,
    int Inspected, int HiddenComponents, int UpdateEntries, int ReadFailures, int Duplicates);
internal sealed record UnifiedInventoryScan(IReadOnlyList<ProgramEntry> Items,
    RegistryInventoryScan Registry, LocalMsixResult Msix, int Added,
    GameLibraryReport Games, int GamesAdded);
internal sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
internal sealed record CleanupResult(int FilesRemoved, int DirectoriesRemoved, int Failed, int ReparseSkipped, int Skipped, IReadOnlyList<string> Examples);
internal sealed record DiskUsageInfo(string Root, string Label, long TotalBytes, long FreeBytes);

internal static class AppServices
{
    // Somente consulta: nenhuma modificação no disco e nenhum privilégio elevado.
    // DriveInfo representa volumes locais (C:, D:...), não dispositivos físicos.
    public static List<DiskUsageInfo> GetLocalDiskUsage()
    {
        var items = new List<DiskUsageInfo>();
        foreach (DriveInfo disk in DriveInfo.GetDrives())
        {
            try
            {
                if (disk.DriveType != DriveType.Fixed || !disk.IsReady) continue;
                long total = disk.TotalSize;
                long free = disk.TotalFreeSpace;
                if (total <= 0) continue;
                string label;
                try { label = disk.VolumeLabel?.Trim() ?? ""; }
                catch { label = ""; }
                items.Add(new DiskUsageInfo(disk.Name, label, total, Math.Clamp(free, 0L, total)));
            }
            catch (IOException) { /* Volume momentaneamente indisponível. */ }
            catch (UnauthorizedAccessException) { /* Volume protegido. */ }
            catch (Exception) { /* Volume sem informações consultáveis. */ }
        }
        return items.OrderBy(d => d.Root, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Cores de ações com contraste legível sobre o tema escuro.
    public static readonly Color Background = Color.FromArgb(21, 27, 35);
    public static readonly Color Blue = Color.FromArgb(20, 94, 158);
    public static readonly Color Red = Color.FromArgb(164, 62, 74);
    public static readonly Color Green = Color.FromArgb(33, 112, 72);
    public static readonly Color Orange = Color.FromArgb(154, 100, 43);
    public static readonly Color Gray = Color.FromArgb(64, 80, 100);
    public static readonly Color Dark = Color.FromArgb(30, 39, 50);

    private static Encoding GetCommandEncoding()
    {
        try
        {
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            try { return Console.OutputEncoding; } catch { return Encoding.Default; }
        }
    }

    public static async Task<CommandResult> RunCommandAsync(string fileName, IEnumerable<string> arguments, Action<string>? onLine = null, CancellationToken cancellationToken = default)
    {
        Encoding commandEncoding = GetCommandEncoding();
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = commandEncoding,
            StandardErrorEncoding = commandEncoding
        };
        foreach (string argument in arguments) psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) { stdoutClosed.TrySetResult(true); return; }
            lock (stdout) stdout.AppendLine(e.Data);
            onLine?.Invoke(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) { stderrClosed.TrySetResult(true); return; }
            lock (stderr) stderr.AppendLine(e.Data);
            onLine?.Invoke(e.Data);
        };

        try
        {
            if (!process.Start()) throw new InvalidOperationException("O Windows não conseguiu iniciar o comando.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(stdoutClosed.Task, stderrClosed.Task).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
        }
        catch (Exception)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }

    public static List<ProgramEntry> GetInstalledPrograms() =>
        ScanInstalledPrograms().Items.ToList();

    internal static UnifiedInventoryScan ScanAllInstalledApps()
    {
        var registry = ScanInstalledPrograms();
        var result = registry.Items.ToList();
        var msix = LocalMsixInventory.ReadCurrentUser();
        var present = registry.Items.Select(x =>
            new InstalledApp(x.Name, x.Version, x.Publisher)).ToArray();
        var additions = LocalMsixInventory.MergeWithRegistry(msix.Items, present);
        foreach (var app in additions)
        {
            // MSIX visíveis sem regra oficial participam do inventário,
            // mas não recebem permissão de atualização/desinstalação.
            var verified = app.RecipeEligible
                ? ApplicationIdentityCatalog.Resolve(app.Name, app.Publisher)
                : null;
            result.Add(new ProgramEntry
            {
                Name = app.Name,
                Version = app.Version,
                Publisher = app.Publisher,
                InventoryOnly = true,
                InventorySource = "Aplicativo empacotado (MSIX)",
                InventoryId = "MSIX|" + app.PackageName,
                VerifiedMsixIdentity = app.RecipeEligible,
                CatalogIdentity = verified?.Id ?? "",
                CatalogDisplayName = verified?.Name ?? ""
            });
        }
        // Jogos podem estar instalados sem registro Win32: o manifesto da
        // biblioteca e a identidade da instalacao, nunca uma receita executavel.
        var games = GameLibraryAudit.Inspect();
        var gameAdditions = GameLibraryAudit.UnregisteredGames(games, result);
        foreach (var game in gameAdditions)
            result.Add(new ProgramEntry
            {
                Name = game.DisplayName,
                InventoryOnly = true,
                InventorySource = "Biblioteca de jogos: " + game.Platform,
                InventoryId = "GAME|" + game.Platform + "|" + game.GameId
            });
        return new UnifiedInventoryScan(result, registry, msix, additions.Count,
            games, gameAdditions.Count);
    }

    // ARP x64/x86 para máquina e usuário atual. Usa nomes reais do Windows,
    // INDEPENDENTE do catálogo de atualizações. Nunca executa uninstallers.
    internal static RegistryInventoryScan ScanInstalledPrograms()
    {
        var entries = new List<ProgramEntry>();
        int inspected = 0, hidden = 0, updates = 0, failures = 0;
        var views = Environment.Is64BitOperatingSystem
            ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
            : new[] { RegistryView.Registry32 };
        var roots = new[]
        {
            (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
        };
        foreach (var (hive, subKey) in roots)
        foreach (var view in views)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(subKey, writable: false);
                if (uninstall is null) continue;
                foreach (string registryKey in uninstall.GetSubKeyNames())
                {
                    inspected++;
                    try
                    {
                        using var child = uninstall.OpenSubKey(registryKey, writable: false);
                        if (child is null) { failures++; continue; }
                        string name = Convert.ToString(child.GetValue("DisplayName"))?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(name) || name.Length > 240) continue;
                        string release = Convert.ToString(child.GetValue("ReleaseType")) ?? "";
                        if (release.Equals("Update", StringComparison.OrdinalIgnoreCase) ||
                            release.Equals("Security Update", StringComparison.OrdinalIgnoreCase) ||
                            release.Equals("Hotfix", StringComparison.OrdinalIgnoreCase) ||
                            release.Equals("Update Rollup", StringComparison.OrdinalIgnoreCase))
                        { updates++; continue; }
                        bool systemComponent = Convert.ToInt32(child.GetValue("SystemComponent") ?? 0) == 1;
                        if (systemComponent && !ProtectedApplications.IsProtectedName(name))
                        { hidden++; continue; }
                        bool protectedApp = ProtectedApplications.IsInventoryOnly(name, systemComponent);
                        string publisher = Convert.ToString(child.GetValue("Publisher"))?.Trim() ?? "";
                        UnifiedAppRule? known = ApplicationIdentityCatalog.Resolve(name, publisher);
                        string scope = hive == RegistryHive.CurrentUser ? "Usuário atual" : "Computador";
                        string architecture = view == RegistryView.Registry32 ? "32 bits" : "64 bits";
                        // O identificador inclui a visão real: não unir instalações
                        // diferentes só porque o nome e versão coincidem.
                        string registryIdentity = $"{hive}|{view}|{registryKey}";
                        entries.Add(new ProgramEntry
                        {
                            Name = name,
                            Version = Convert.ToString(child.GetValue("DisplayVersion"))?.Trim() ?? "",
                            Publisher = publisher,
                            CatalogIdentity = known?.Id ?? "",
                            CatalogDisplayName = known?.Name ?? "",
                            InventorySource = $"Registro: {scope} ({architecture})",
                            InventoryId = registryIdentity,
                            MsiProductCode = InventoryReconciler.ProductCodeFromRegistryKey(registryKey) ?? "",
                            UninstallString = protectedApp ? "" :
                                Convert.ToString(child.GetValue("UninstallString")) ?? "",
                            QuietUninstallString = protectedApp ? "" :
                                Convert.ToString(child.GetValue("QuietUninstallString")) ?? "",
                            InstallLocation = protectedApp ? "" :
                                Convert.ToString(child.GetValue("InstallLocation"))?.Trim() ?? "",
                            InventoryOnly = protectedApp
                        });
                    }
                    catch (Exception) { failures++; }
                }
            }
            catch (Exception) { failures++; }
        }
        var distinct = InventoryReconciler.DistinctRegistryEntries(entries);
        return new RegistryInventoryScan(distinct.OrderBy(x => x.Name,
            StringComparer.CurrentCultureIgnoreCase).ToArray(),
            inspected, hidden, updates, failures, entries.Count - distinct.Count);
    }

    public static CleanupResult CleanDirectorySafely(string root, Action<string>? log = null)
    {
        // Escopo limitado à pasta informada. Nunca remove a pasta raiz.
        // Não percorre junctions/symlinks; preserva arquivos recentes/em uso
        // e diretórios do antivírus. Nenhuma confirmação por arquivo.
        int files = 0, directories = 0, failed = 0, special = 0, skipped = 0;
        var examples = new List<string>();
        void Failure(string path, Exception ex)
        {
            failed++;
            if (examples.Count < 5) examples.Add(Path.GetFileName(path) + ": " + ex.Message);
        }
        string fullRoot;
        try { fullRoot = Path.GetFullPath(root); }
        catch (Exception ex) { Failure(root, ex); return new(0, 0, failed, 0, 0, examples); }
        if (!Directory.Exists(fullRoot))
            return new(0, 0, 1, 0, 0, new[] { "Pasta temporária não encontrada." });

        var stack = new Stack<(string path, bool after)>();
        stack.Push((fullRoot, false));
        while (stack.Count > 0)
        {
            var (path, after) = stack.Pop();
            if (after)
            {
                if (!path.Equals(fullRoot, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Preserva pastas que ainda têm arquivos recentes/protegidos.
                        if (!Directory.EnumerateFileSystemEntries(path).Any())
                        { Directory.Delete(path, recursive: false); directories++; }
                    }
                    catch (DirectoryNotFoundException) { }
                    catch (UnauthorizedAccessException ex) { Failure(path, ex); }
                    catch (IOException) { skipped++; }
                    catch (Exception ex) { Failure(path, ex); }
                }
                continue;
            }
            if (!Directory.Exists(path)) continue;
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                { special++; continue; }
                // Diretórios temporários do antivírus devem ficar intocados.
                if (!path.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(path).StartsWith("_avg_", StringComparison.OrdinalIgnoreCase))
                { skipped++; continue; }
                stack.Push((path, true));
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var attrs = File.GetAttributes(file);
                        if ((attrs & FileAttributes.ReparsePoint) != 0) { special++; continue; }
                        if ((attrs & FileAttributes.ReadOnly) != 0 ||
                            DateTime.Now - File.GetLastWriteTime(file) < TimeSpan.FromHours(24))
                        { skipped++; continue; }
                        File.Delete(file);
                        files++;
                    }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                    catch (IOException) { skipped++; } // Em uso, não é erro crítico.
                    catch (UnauthorizedAccessException) { skipped++; }
                    catch (Exception ex) { Failure(file, ex); }
                }
                foreach (string child in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
                    stack.Push((child, false));
            }
            catch (UnauthorizedAccessException) { skipped++; }
            catch (IOException) { skipped++; }
            catch (Exception ex) { Failure(path, ex); }
        }
        log?.Invoke($"Limpeza: {files} arquivo(s) e {directories} pasta(s) removidos; " +
                    $"{skipped} itens recentes, protegidos ou em uso preservados; {special} links ignorados; {failed} falhas inesperadas.");
        foreach (var line in examples) log?.Invoke("Detalhe: " + line);
        return new(files, directories, failed, special, skipped, examples);
    }

    public static bool TryParseUninstaller(string command, out string fileName, out string arguments)
    {
        fileName = ""; arguments = "";
        string raw = command.Trim();
        if (raw.Length == 0) return false;
        if (raw.StartsWith('"'))
        {
            int end = raw.IndexOf('"', 1);
            if (end <= 1) return false;
            fileName = raw[1..end];
            arguments = raw[(end + 1)..].Trim();
        }
        else
        {
            int exeIndex = raw.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0)
            {
                int end = exeIndex + 4;
                fileName = raw[..end].Trim().Trim('"');
                arguments = raw[end..].Trim();
            }
            else
            {
                string[] parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                fileName = parts[0];
                arguments = parts.Length > 1 ? parts[1] : "";
            }
        }
        if (Path.GetFileName(fileName).Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase))
            arguments = Regex.Replace(arguments, @"(?i)(^|\s)/I(?=\s*\{)", "$1/X");
        return fileName.Length > 0;
    }

}

// Botões desenhados na própria interface: sem dependência de recursos externos.
internal sealed class ModernButton : Button
{
    private bool _hot;
    private bool _pressed;
    private readonly System.Windows.Forms.Timer _visualTimer = new() { Interval = 16 };
    private long _visualStart;
    private float _visualProgress = 1F;
    private bool _pendingClick;
    private bool _dispatchingClick;
    private const double FeedbackMs = 145;
    // Estas propriedades sao definidas pelo codigo da interface em tempo de execucao.
    // No .NET 10, propriedades publicas de controles precisam declarar sua
    // politica de serializacao para satisfazer o analisador WFO1000.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OutlineColor { get; set; } = Color.Transparent;

    // Somente cartões de ferramentas podem quebrar títulos em duas linhas.
    // Demais botões da interface mantêm o desenho/feedback já validado.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AllowMultilineText { get; set; }

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        TabStop = true;
        _visualTimer.Tick += (_, _) =>
        {
            _visualProgress = Math.Clamp((float)((Stopwatch.GetTimestamp() - _visualStart) * 1000.0 /
                Stopwatch.Frequency / FeedbackMs), 0F, 1F);
            Invalidate();
            if (_visualProgress < 1F) return;
            _visualTimer.Stop();
            if (!_pendingClick) return;
            _pendingClick = false;
            if (IsDisposed || !Enabled) return;
            _dispatchingClick = true;
            try { base.OnClick(EventArgs.Empty); }
            finally { _dispatchingClick = false; }
        };
    }

    internal static GraphicsPath RoundRect(Rectangle box, int radius)
    {
        var path = new GraphicsPath();
        if (box.Width <= 0 || box.Height <= 0) return path;
        int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(box.Width, box.Height)));
        path.AddArc(box.Left, box.Top, diameter, diameter, 180, 90);
        path.AddArc(box.Right - diameter, box.Top, diameter, diameter, 270, 90);
        path.AddArc(box.Right - diameter, box.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(box.Left, box.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width < 6 || ClientSize.Height < 6) return;
        // Desenha tudo em GDI+; nenhum relevo/borda legado do botão padrão.
        e.Graphics.Clear(Parent?.BackColor ?? Color.FromArgb(21, 27, 35));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = new Rectangle(2, 2, ClientSize.Width - 5, ClientSize.Height - 5);
        bool animatedPress = _pendingClick && _visualProgress < 0.64F;
        bool depressed = _pressed || animatedPress;
        if (depressed) { rect.Offset(0, 2); rect.Height = Math.Max(3, rect.Height - 2); }
        float flash = _pendingClick ? MathF.Sin(_visualProgress * MathF.PI) : 0F;
        Color fill = BackColor;
        if (!Enabled) fill = Color.FromArgb(47, 56, 66);
        else if (depressed) fill = Blend(fill, Color.Black, 0.17F);
        else if (_hot) fill = Blend(fill, Color.White, 0.09F);
        if (Enabled && flash > 0F) fill = Blend(fill, Color.White, flash * 0.10F);
        using var shape = RoundRect(rect, CornerRadius);
        using (var brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, shape);
        if (OutlineColor != Color.Transparent || _hot || _pendingClick)
        {
            var outlineColor = OutlineColor != Color.Transparent ? OutlineColor :
                Color.FromArgb(65 + (int)(70 * flash), 116, 175, 227);
            using var pen = new Pen(outlineColor, 1F);
            e.Graphics.DrawPath(pen, shape);
        }
        if (Focused && ShowFocusCues)
        {
            using var outline = new Pen(Color.FromArgb(128, 188, 253), 2F);
            e.Graphics.DrawPath(outline, shape);
        }
        var content = new Rectangle(12, 3 + (depressed ? 2 : 0),
            Math.Max(1, ClientSize.Width - 24), Math.Max(1, ClientSize.Height - 6));
        var captionFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix |
            (AllowMultilineText ? TextFormatFlags.WordBreak :
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(e.Graphics, Text, Font, content,
            Enabled ? ForeColor : Color.FromArgb(164, 176, 190), captionFlags);
    }

    private static Color Blend(Color first, Color second, float amount)
    {
        int Chan(int a, int b) => (int)(a + (b - a) * amount);
        return Color.FromArgb(Chan(first.R, second.R), Chan(first.G, second.G), Chan(first.B, second.B));
    }
    // Mesmo clique em botões de navegação e manutenção: animação ANTES do evento.
    // Evita que um navegador ou caixa modal oculta a reação ao clique.
    protected override void OnClick(EventArgs e)
    {
        if (_dispatchingClick) { base.OnClick(e); return; }
        if (!Enabled || IsDisposed || _pendingClick) return;
        _pendingClick = true;
        _visualStart = Stopwatch.GetTimestamp();
        _visualProgress = 0F;
        _visualTimer.Start();
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hot = true;
        Cursor = Cursors.Hand;
        Invalidate();
        base.OnMouseEnter(e);
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        _pressed = false;
        Cursor = Cursors.Default;
        Invalidate();
        base.OnMouseLeave(e);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled) _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }
    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            _visualTimer.Stop();
            _pendingClick = false;
            _pressed = false;
            _visualProgress = 1F;
        }
        Invalidate();
        base.OnEnabledChanged(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _visualTimer.Stop(); _visualTimer.Dispose(); }
        base.Dispose(disposing);
    }
}

internal sealed partial class MainForm : Form
{
    private static readonly Color Ink = Color.FromArgb(237, 243, 250);
    private static readonly Color Muted = Color.FromArgb(167, 181, 196);
    private static readonly Color PanelBackground = Color.FromArgb(30, 39, 50);
    private static readonly Color LightBorder = Color.FromArgb(61, 77, 93);
    private static readonly Color SelectionBackground = Color.FromArgb(42, 66, 92);
    private static readonly Color Accent = Color.FromArgb(111, 184, 255);
    private readonly ToolTip _toolTips = new() { AutomaticDelay = 350, AutoPopDelay = 8500, ReshowDelay = 120 };
    private readonly DarkProgramList _programList = new();
    private readonly Label _emptyState = new();
    private readonly TextBox _search = new();
    private readonly DarkLogView _log = new();
    private readonly Label _status = new();
    private readonly Label _listTitle = new();
    private readonly Label _itemHint = new();
    private readonly ProgressBar _progress = new();
    private readonly DarkTabHost _nav = new(210);
    private readonly List<ProgramEntry> _installed = new();
    private readonly List<ProgramEntry> _available = new();
    private readonly List<Control> _actionControls = new();
    private readonly DarkScrollableFlow _diskCards = new();
    private readonly Label _diskSummary = new();
    private Button? _refreshDisksButton;
    private bool _refreshingDisks;
    private bool _busy;
    private bool _multiSelectMode;
    private bool _catalogoVerificado; // Resultado local do RuckZuck (sem instalacao)
    private CancellationTokenSource? _consultaCatalogoCts;
    private CancellationTokenSource? _installerDownloadCts;
    private int _catalogoGeneration; // Descarta resultados de consultas anteriores ao recarregamento local.
    private string _appFilter = "Todos";
    private readonly HashSet<ProgramEntry> _checkedApps = new();
    private Button? _checkUpdatesButton;
    private Button? _bulkUninstallButton;
    private Button? _selectManyButton;
    private Button? _clearSelectionButton;
    private Button? _allFilterButton;
    private Button? _updatesFilterButton;
    private Button? _selectedFilterButton;
    private string _operationResult = "Pronto";
    private readonly Font _rowTitle = new("Segoe UI", 10F, FontStyle.Bold);
    private readonly Font _rowInfo = new("Segoe UI", 8.8F);
    private readonly Font _rowMark = new("Segoe UI", 10.5F, FontStyle.Bold);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int dark = 1;
            if (NativeMethods.DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
                NativeMethods.DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    public MainForm()
    {
        Text = "Gerenciador Final — v5.13.5 TESTE";
        Font = new Font("Segoe UI", 9.5F);
        BackColor = AppServices.Background;
        ForeColor = Ink;
        AutoScaleMode = AutoScaleMode.Dpi;
        // Abre com o dimensionamento aprovado na v5.12.21.1, sem restaurar
        // janelas pequenas de execuções anteriores e sem sair do monitor.
        var display = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Rectangle opening = StartupWindowSizing.Fit(display);
        StartPosition = FormStartPosition.Manual;
        MinimumSize = StartupWindowSizing.AllowedMinimum(opening);
        Bounds = opening;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20, 16, 20, 16), BackColor = AppServices.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
        Controls.Add(root);
        root.Controls.Add(BuildHeader(), 0, 0);

        _nav.Dock = DockStyle.Fill;
        _nav.AddTab("Aplicativos", BuildProgramsPanel());
        AddToolsPages();
        _nav.SelectedIndexChanged += async (_, _) =>
        {
            // Navegação plana: Aplicativos, Limpeza, Reparo, Desempenho, Sistema.
            if (_nav.SelectedIndex == 1)
                await RefreshDiskUsageAsync();
            if (_nav.SelectedIndex == 3)
                await RefreshEnergyInfoAsync();
            if (_nav.SelectedIndex == 4 && _activeSystemView == "processes")
                ShowCachedProcessesIfAvailable();
        };
        root.Controls.Add(_nav, 0, 1);
        root.Controls.Add(BuildOperationsPanel(), 0, 2);
        // Abre imediatamente. Primeiro carrega a lista local e depois inicia consultas independentes,
        // em segundo plano, sem janelas auxiliares e sem executar instaladores.
        Shown += async (_, _) =>
        {
            // WinForms pode redimensionar a janela automaticamente em monitores DPI
            // diferentes. Readequar apenas se necessário, sem cortar cartões.
            Rectangle visibleArea = Screen.FromControl(this).WorkingArea;
            if (!visibleArea.Contains(Bounds))
            {
                Rectangle fitted = StartupWindowSizing.Fit(visibleArea);
                MinimumSize = StartupWindowSizing.AllowedMinimum(fitted);
                Bounds = fitted;
            }
            await ExecuteAsync("Carregando programas instalados...", LoadInstalledAsync);
            if (!IsDisposed)
                BeginStartupChecks();
        };
        FormClosing += (_, e) =>
        {
            if (!_busy)
            {
                _consultaCatalogoCts?.Cancel();
                _processTimer.Stop();
                return;
            }
            if (_consultaCatalogoCts is not null)
            {
                e.Cancel = true;
                _consultaCatalogoCts.Cancel();
                Status("Cancelando a consulta. Tente fechar novamente em alguns instantes.");
                return;
            }
            if (_installerDownloadCts is not null)
            {
                e.Cancel = true;
                _installerDownloadCts.Cancel();
                Status("Cancelando download. Tente fechar novamente em instantes.");
                return;
            }
            e.Cancel = true;
            MessageBox.Show(this, "Aguarde a operação atual terminar antes de fechar o gerenciador.",
                "Operação em andamento", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        Log("Gerenciador Final iniciado. Operações e andamento ficam integrados à janela principal.");
        Log("DIAGNÓSTICO DE TESTE: arquivo confirmado em " + DiagnosticTrace.LogFilePath);
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 5));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        panel.Controls.Add(new Panel
        {
            Dock = DockStyle.Fill, BackColor = AppServices.Blue,
            Margin = new Padding(0, 7, 0, 8)
        }, 0, 0);
        panel.Controls.Add(new Label
        {
            Text = "GERENCIADOR FINAL", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold), ForeColor = Ink,
            Margin = new Padding(13, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft
        }, 1, 0);
        panel.Controls.Add(new Label
        {
            Text = "Programas, atualizações e manutenção do Windows em um só lugar.",
            Dock = DockStyle.Fill, Margin = new Padding(13, 0, 0, 0),
            ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft
        }, 1, 1);
        return panel;
    }

    private Control BuildProgramsPanel()
    {
        // Uma única lista: atualização disponível no topo, outras instalações abaixo.
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(4, 11, 4, 5), Margin = Padding.Empty,
            BackColor = AppServices.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 57));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var searchBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, Margin = Padding.Empty,
            BackColor = AppServices.Background
        };
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
        searchBar.Controls.Add(new Label
        {
            Text = "Pesquisar:", Dock = DockStyle.Fill, ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.BackColor = Color.FromArgb(19, 26, 34);
        _search.ForeColor = Ink;
        _search.PlaceholderText = "Nome, versão ou fabricante";
        _search.Margin = new Padding(0, 7, 10, 7);
        _search.Dock = DockStyle.Fill;
        _search.TextChanged += (_, _) => RenderPrograms();
        searchBar.Controls.Add(_search, 1, 0);
        var clear = CreateSecondaryButton("Limpar");
        clear.Dock = DockStyle.Fill;
        clear.Margin = new Padding(0, 6, 10, 6);
        clear.Click += (_, _) => _search.Clear();
        _toolTips.SetToolTip(clear, "Apaga apenas o texto da pesquisa.");
        searchBar.Controls.Add(clear, 2, 0);
        var refresh = CreateButton("Atualizar lista", AppServices.Blue, Color.White);
        refresh.Dock = DockStyle.Fill;
        refresh.Margin = new Padding(0, 6, 0, 6);
        refresh.Click += async (_, _) => await ExecuteAsync("Atualizando lista local...", RefreshCatalogAsync);
        _toolTips.SetToolTip(refresh, "Recarrega somente os programas instalados no Windows. Não consulta a internet.");
        _actionControls.Add(refresh);
        searchBar.Controls.Add(refresh, 3, 0);
        root.Controls.Add(searchBar, 0, 0);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
            BackColor = AppServices.Background, Margin = new Padding(0, 2, 0, 3)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17));
        Button MakeAction(string title, Color color, int column, Action onClick)
        {
            var b = CreateButton(title, color, Color.White);
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(0, 4, 8, 5);
            b.Click += (_, _) => onClick();
            actions.Controls.Add(b, column, 0);
            _actionControls.Add(b);
            return b;
        }
        // A consulta é uma ação explícita; não abre janelas de confirmação.
        // Permanece clicável durante a busca para permitir cancelamento.
        _checkUpdatesButton = MakeAction("Verificar atualizações", AppServices.Blue, 0, () => _ = CheckUpdatesFromButtonAsync());
        _actionControls.Remove(_checkUpdatesButton);
        _toolTips.SetToolTip(_checkUpdatesButton,
            "Consulta HTTPS a fontes oficiais e RuckZuck. Não instala nem atualiza nada; " +
            "as fontes recebem dados normais de conexão. Os resultados são candidatos, não garantias.");
        _selectManyButton = MakeAction("Selecionar vários", AppServices.Gray, 1, ToggleMultiSelection);
        _bulkUninstallButton = MakeAction("Desinstalar selecionados", AppServices.Red, 2,
            () => _ = ExecuteAsync("Desinstalando selecionados...", UninstallCheckedAsync));
        _clearSelectionButton = MakeAction("Limpar seleção", AppServices.Gray, 3, ClearMultiSelection);
        _toolTips.SetToolTip(_bulkUninstallButton, "Solicita confirmação e desinstala os programas marcados, um por vez.");
        root.Controls.Add(actions, 0, 1);

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, Margin = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        _listTitle.Dock = DockStyle.Fill;
        _listTitle.Font = new Font("Segoe UI", 11.5F, FontStyle.Bold);
        _listTitle.ForeColor = Ink;
        _listTitle.TextAlign = ContentAlignment.MiddleLeft;
        heading.Controls.Add(_listTitle, 0, 0);
        _allFilterButton = AddFilter(heading, "Todos", 1);
        _updatesFilterButton = AddFilter(heading, "Com atualização", 2, "Possíveis");
        _selectedFilterButton = AddFilter(heading, "Marcados", 3);
        _toolTips.SetToolTip(_updatesFilterButton, "Mostra possíveis atualizações indicadas por catálogos externos. " +
            "A Microsoft Store pode não oferecer a mesma versão neste momento.");
        root.Controls.Add(heading, 0, 2);

        _programList.Dock = DockStyle.Fill;
        _programList.ItemHeight = 78;
        _programList.BackColor = PanelBackground;
        _programList.ForeColor = Ink;
        _programList.DrawItem += DrawProgramCard;
        _toolTips.SetToolTip(_programList,
            "Atualizar usa um instalador oficial verificado quando há integração validada. " +
            "Site abre o fabricante somente quando solicitado. Desinstalar exige confirmação.");
        _programList.ItemActionClicked += (item, action) =>
        {
            if (_busy) return;
            if (action == "uninstall" && !item.InventoryOnly)
                _ = ExecuteAsync("Desinstalando " + item.Name + "...", () => UninstallEntriesAsync(new[] { item }));
            else if (action == "update")
                _ = UpdateThirdPartyAsync(item);
            else if (action == "terminal")
                _ = UpdateWindowsTerminalAsync(item);
            else if (action == "site")
                OpenOfficialSite(item);
            else if (action == "guide")
                ShowUpdateGuidance(item);
            else if (action == "store")
                OpenMicrosoftStore(item);
        };
        _programList.ItemCheckboxClicked += item =>
        {
            if (_busy || !_multiSelectMode || item.InventoryOnly) return;
            if (!_checkedApps.Add(item)) _checkedApps.Remove(item);
            _programList.Invalidate();
            RenderTitle();
            UpdateBulkButtons();
        };
        _programList.SelectedIndexChanged += (_, _) =>
        {
            _itemHint.Text = _programList.SelectedItem is ProgramEntry entry
                ? "Selecionado: " + entry.Name : "Selecione um aplicativo";
        };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = PanelBackground, Margin = Padding.Empty };
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Font = new Font("Segoe UI", 10F);
        _emptyState.ForeColor = Muted;
        _emptyState.BackColor = PanelBackground;
        _emptyState.Padding = new Padding(25);
        _emptyState.Visible = false;
        host.Controls.Add(_programList);
        host.Controls.Add(_emptyState);
        root.Controls.Add(host, 0, 3);
        UpdateBulkButtons();
        RenderTitle();
        return root;
    }

    private async Task CheckUpdatesFromButtonAsync()
    {
        if (_installerDownloadCts is not null)
        {
            _installerDownloadCts.Cancel();
            Status("Cancelando download...");
            return;
        }
        if (_consultaCatalogoCts is not null)
        {
            _consultaCatalogoCts.Cancel();
            Status("Cancelando consulta...");
            return;
        }
        if (_busy) return;
        await LoadUpdatesAsync(forceRefresh: true);
        if (_catalogoVerificado && !IsDisposed)
        {
            _appFilter = "Com atualização";
            RenderPrograms();
        }
    }

    private Button AddFilter(TableLayoutPanel heading, string title, int column, string? visibleTitle = null)
    {
        var button = CreateSecondaryButton(visibleTitle ?? title);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(5, 4, 0, 5);
        button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        button.Click += (_, _) =>
        {
            if (_busy) return;
            _appFilter = title == "Marcados" ? "Selecionados" : title;
            RenderPrograms();
            if (title == "Com atualização" && !_catalogoVerificado)
                Status("Clique em Verificar atualizações para consultar o catálogo online.");
        };
        heading.Controls.Add(button, column, 0);
        return button;
    }

    private void DrawProgramCard(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _programList.Items.Count) return;
        ProgramEntry item = _programList.Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;
        var card = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 3, e.Bounds.Width - 9, e.Bounds.Height - 6);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var round = ModernButton.RoundRect(card, 8))
        using (var brush = new SolidBrush(selected ? SelectionBackground : Color.FromArgb(37, 48, 61)))
        using (var pen = new Pen(selected ? Accent : LightBorder, selected ? 1.8F : 0.9F))
        {
            e.Graphics.FillPath(brush, round);
            e.Graphics.DrawPath(pen, round);
        }
        if (selected)
        {
            using var accent = new SolidBrush(AppServices.Blue);
            e.Graphics.FillRectangle(accent, card.X + 1, card.Y + 10, 4, card.Height - 20);
        }
        bool multiselect = _multiSelectMode;
        int prefix = multiselect ? 38 : 0;
        if (multiselect)
        {
            Rectangle box = AppRowGeometry.Checkbox(e.Bounds);
            bool marked = _checkedApps.Contains(item);
            using var b = new SolidBrush(marked ? AppServices.Blue : Color.FromArgb(22, 32, 43));
            using var border = new Pen(marked ? Accent : Muted, 1.3F);
            e.Graphics.FillRectangle(b, box);
            e.Graphics.DrawRectangle(border, box);
            if (marked) TextRenderer.DrawText(e.Graphics, "✓", _rowInfo, box, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        var mark = new Rectangle(e.Bounds.X + 17 + prefix, e.Bounds.Y + 19, 36, 36);
        using (var markBrush = new SolidBrush(item.IsUpdate ? Color.FromArgb(23, 97, 151) : Color.FromArgb(54, 67, 81)))
            e.Graphics.FillEllipse(markBrush, mark);
        string initial = string.IsNullOrWhiteSpace(item.Name) ? "?" : item.Name.Trim()[0].ToString().ToUpperInvariant();
        TextRenderer.DrawText(e.Graphics, initial, _rowMark, mark, item.IsUpdate ? Accent : Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        int left = e.Bounds.Left + 65 + prefix;
        int width = Math.Max(18, e.Bounds.Width - left + e.Bounds.Left - 355);
        TextRenderer.DrawText(e.Graphics, item.Name, _rowTitle,
            new Rectangle(left, e.Bounds.Top + 8, width, 27), selected ? Accent : Ink,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
        bool checkStore = item.IsUpdate && VerifiedUpdaters.GetPrimaryAction(item) == "store";
        string details = item.IsUpdate && checkStore
            ? $"Versão em catálogo ({item.Source}); confira na Store · {DisplayAppVersion(item.Version)} → {DisplayAppVersion(item.AvailableVersion)}"
            : item.IsUpdate
            ? $"Possível atualização ({item.Source})  ·  {DisplayAppVersion(item.Version)} → {DisplayAppVersion(item.AvailableVersion)}"
            : item.ComparisonState == MatchState.SupersededRegistration
            ? $"Cadastro antigo do Visual C++ · versão {DisplayAppVersion(item.AvailableVersion)} também instalada"
            : item.ComparisonState == MatchState.DifferentMajor
            ? $"Revisar: versões principais diferentes · {DisplayAppVersion(item.Version)} → {DisplayAppVersion(item.AvailableVersion)} (coexistem)"
            : Matching.IsUncertain(item.ComparisonState)
            ? $"Resultado incerto (RuckZuck)  ·  Versão: {DisplayAppVersion(item.Version)}"
            : item.InventoryOnly && item.InventorySource.StartsWith("Biblioteca de jogos:", StringComparison.Ordinal)
            ? $"Jogo da {item.InventorySource["Biblioteca de jogos:".Length..].Trim()} (somente leitura)"
            : item.InventoryOnly
            ? $"Versão: {DisplayAppVersion(item.Version)} · Componente Windows/Store (somente leitura)"
            : $"Versão: {DisplayAppVersion(item.Version)}  ·  {(string.IsNullOrWhiteSpace(item.Publisher) ? "Fornecedor não informado" : item.Publisher)}";
        TextRenderer.DrawText(e.Graphics, details, _rowInfo,
            new Rectangle(left, e.Bounds.Top + 36, Math.Max(20, e.Bounds.Width - left + e.Bounds.Left - 350), 25),
            item.IsUpdate ? Color.FromArgb(133, 221, 176) : Muted,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

        if (!multiselect)
        {
            if (item.IsUpdate)
            {
                string? primaryAction = VerifiedUpdaters.GetPrimaryAction(item);
                if (primaryAction is not null)
                {
                    string label = primaryAction switch
                    {
                        "update" => "Atualizar",
                        "terminal" => "Atualizar",
                        "site" => "Site oficial",
                        "store" => "Ver na Store",
                        _ => "Como fazer"
                    };
                    Color color = primaryAction is "update" or "terminal" ? AppServices.Green : AppServices.Gray;
                    DrawRowButton(e.Graphics, AppRowGeometry.UpdateButton(e.Bounds), label, color,
                        _programList.ActionVisual(item, primaryAction));
                }
                if (primaryAction is "update" or "terminal" && VerifiedUpdaters.GetOfficialPage(item) is not null)
                    DrawRowButton(e.Graphics, AppRowGeometry.SiteButton(e.Bounds), "Site", AppServices.Gray,
                        _programList.ActionVisual(item, "site"));
            }
            if (!item.InventoryOnly)
                DrawRowButton(e.Graphics, AppRowGeometry.UninstallButton(e.Bounds), "Desinstalar", AppServices.Red,
                    _programList.ActionVisual(item, "uninstall"));
        }
    }

    private static string DisplayAppVersion(string version)
        => string.IsNullOrWhiteSpace(version) || version.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? "não informada" : version;

    private static void DrawRowButton(Graphics graphics, Rectangle rect, string text, Color color,
        (bool Hovered, bool Pressed, float Pulse) visual)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        // O relevo de 2 px e a mudança de tonalidade representam um clique real.
        // A animação dura 160 ms e termina antes de qualquer janela externa abrir.
        var drawRect = rect;
        if (visual.Pressed) { drawRect.Offset(0, 2); drawRect.Height = Math.Max(12, drawRect.Height - 2); }
        static Color Mix(Color a, Color b, float amount)
        {
            int Blend(int x, int y) => Math.Clamp((int)Math.Round(x + (y - x) * amount), 0, 255);
            return Color.FromArgb(Blend(a.R, b.R), Blend(a.G, b.G), Blend(a.B, b.B));
        }
        Color fill = color;
        if (visual.Pressed) fill = Mix(fill, Color.Black, 0.18F);
        else if (visual.Hovered) fill = Mix(fill, Color.White, 0.10F);
        float pulse = MathF.Sin(MathF.PI * visual.Pulse);
        if (pulse > 0) fill = Mix(fill, Color.White, pulse * 0.08F);
        using var path = ModernButton.RoundRect(drawRect, 7);
        using (var bg = new SolidBrush(fill)) graphics.FillPath(bg, path);
        if (visual.Hovered || visual.Pulse > 0)
        {
            using var outline = new Pen(Color.FromArgb(
                65 + (int)(60 * pulse), 213, 233, 255), 1.1F);
            graphics.DrawPath(outline, path);
        }
        using var font = new Font("Segoe UI", 8.4F, FontStyle.Bold);
        TextRenderer.DrawText(graphics, text, font, drawRect,
            Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    private void AddToolsPages()
    {
        var cleaning = new Panel { BackColor = AppServices.Background, Dock = DockStyle.Fill };
        var repair = new Panel { BackColor = AppServices.Background, Dock = DockStyle.Fill };
        var performance = new Panel { BackColor = AppServices.Background, Dock = DockStyle.Fill };
        var system = new Panel { BackColor = AppServices.Background, Dock = DockStyle.Fill };
        _nav.AddTab("Limpeza e desempenho", cleaning);
        _nav.AddTab("Reparo e rede", repair);
        _nav.AddTab("Planos de energia", performance);
        _nav.AddTab("Inicialização e sistema", system);
        // Cores de impacto: azul = rotina, cinza = baixo impacto/inspeção,
        // âmbar = atenção, vermelho = interrupção sensível ou remoção definitiva.
        var cleaningActions = CreateToolsFlow(new[]
        {
            Tool("Temporários do usuário",
                "Remove temporários antigos, preservando arquivos recentes, protegidos e em uso.",
                AppServices.Blue,
                () => ConfirmThenRun("clean-user-temp", "Limpar arquivos temporários antigos do usuário?")),
            Tool("Temporários do Windows",
                "Remove temporários antigos do Windows. Arquivos ativos são preservados.",
                AppServices.Orange,
                () => ConfirmThenRun("clean-windows-temp", "Limpar arquivos temporários antigos do Windows? O UAC solicitará autorização.")),
            Tool("Cache do Windows Update",
                "Remove downloads antigos de atualizações; interrompe serviços temporariamente.",
                AppServices.Red,
                () => ConfirmThenRun("clear-update-cache", "Essa operação interrompe temporariamente serviços de atualização e remove downloads antigos. Continuar?")),
            Tool("Esvaziar Lixeira",
                "Apaga permanentemente os itens da Lixeira. Não é possível desfazer.",
                AppServices.Red,
                () => ExecuteAsync("Esvaziando Lixeira...", EmptyRecycleBinAsync)),
            Tool("Limpeza de Disco automática",
                "Executa a limpeza nativa do Windows, sem abrir outra janela.",
                AppServices.Blue,
                () => ConfirmThenRun("cleanmgr-auto", "Executar a Limpeza de Disco automaticamente? O Windows pode solicitar autorização.")),
            Tool("Cache de shaders DirectX",
                "Remove cache gráfico antigo. Jogos podem precisar recompilar shaders.",
                AppServices.Gray,
                () => ConfirmThenRun("clean-directx-cache", "Remover arquivos antigos do cache D3DSCache? Jogos poderão recompilar os shaders e ficar temporariamente mais lentos na primeira execução.")),
            Tool("Resíduos de aplicativos",
                "Investiga vestígios antigos em pastas, atalhos e Registro, sem apagar dados incertos.",
                AppServices.Gray,
                () => ExecuteAsync("Verificando resíduos de aplicativos...", ReviewOldUninstallResiduesAsync)),
            Tool("Limpeza avançada",
                "Revisa dados e chaves do Registro de apps removidos. Exige confirmação.",
                AppServices.Orange,
                () => ExecuteAsync("Verificando resíduos para limpeza avançada...", ReviewDeepUninstallResiduesAsync))
        }, compact: true);
        var cleaningLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty,
            BackColor = AppServices.Background
        };
        cleaningLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 342));
        cleaningLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        cleaningLayout.Controls.Add(cleaningActions, 0, 0);
        cleaningLayout.Controls.Add(BuildDiskPanel(), 0, 1);
        cleaning.Controls.Add(cleaningLayout);
        // Antes: altura 272px fixa ocultava os dois últimos botões.
        // Quatro cartões por linha em janelas grandes e altura dinâmica em menores.
        void AdjustCleaningCards()
        {
            int available = Math.Max(0, cleaning.ClientSize.Width - 30);
            int columns = Math.Clamp(available / 280, 1, 4);
            int rows = (8 + columns - 1) / columns;
            // Cada cartão compacto tem 146 px + margens: textos não são cortados.
            // Em telas menores, a área adapta-se mantendo a barra escura interna.
            float height = Math.Min(520, Math.Max(342, 22 + rows * 160));
            if (Math.Abs(cleaningLayout.RowStyles[0].Height - height) > 1)
                cleaningLayout.RowStyles[0].Height = height;
        }
        cleaning.Resize += (_, _) => AdjustCleaningCards();
        AdjustCleaningCards();
        // Fluxo simples para uso cotidiano: DISM repara a imagem; em seguida SFC
        // verifica e tenta reparar os arquivos protegidos. Consultas separadas
        // continuam no diagnóstico do computador, sem alterações automáticas.
        var repairActions = CreateToolsFlow(new[]
        {
            Tool("Verificar e reparar Windows",
                "Repara a imagem do Windows e depois verifica e repara os arquivos protegidos. Pode demorar e precisar da Internet.",
                AppServices.Green,
                () => ConfirmThenRun("windows-repair",
                    "O Gerenciador executará DISM /RestoreHealth e, se ele concluir sem erro, SFC /scannow.\n\n" +
                    "Isso pode demorar bastante, utilizar a Internet e modificar arquivos de sistema. " +
                    "Salve seu trabalho antes de continuar.\n\nExecutar o reparo?")),
            Tool("Analisar rede",
                "Verifica adaptador, gateway, DNS e acesso HTTPS. Não altera configurações.",
                AppServices.Blue,
                RunNetworkDiagnosticAsync),
            Tool("Reparar rede",
                "Após analisar, aplica uma correção direcionada a DNS ou Winsock quando indicada. Pode exigir reinício.",
                AppServices.Blue,
                RepairNetworkAfterDiagnosisAsync),
            Tool("Redefinir rede", "Opção avançada: redefine Winsock e TCP/IP mesmo sem diagnóstico. Pode exigir reinicialização.",
                AppServices.Orange,
                () => ConfirmThenRun("network-reset", "Redefinir Winsock e TCP/IP? A conexão pode cair. Talvez seja necessário reiniciar o Windows.")),
            Tool("Criar ponto de restauração",
                "Solicita um ponto de recuperação. Exige Proteção do Sistema; não substitui backup.",
                AppServices.Green,
                () => ConfirmThenRun("create-restore-point",
                    "Solicitar um ponto de restauração ao Windows? Requer autorização administrativa e Proteção do Sistema habilitada. " +
                    "Não substitui backup e o Windows pode reutilizar um ponto recente."))
        });
        var repairLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = Padding.Empty, BackColor = AppServices.Background };
        // Proporções responsivas: em janelas menores os cartões usam a rolagem escura.
        repairLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        repairLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        repairLayout.Controls.Add(repairActions, 0, 0);
        repairLayout.Controls.Add(BuildNetworkResultPanel(), 0, 1);
        repair.Controls.Add(repairLayout);
        performance.Controls.Add(BuildEnergyPanel());
        // Painéis nativos do gerenciador: nenhum atalho para Task Manager,
        // MSConfig, Configurações, Windows Update ou MSInfo32.
        system.Controls.Add(BuildIntegratedSystemPanel());
    }

    private sealed record ToolInfo(string Name, string Description, Color Color, Func<Task> Action);
    private static ToolInfo Tool(string name, string details, Color color, Func<Task> action) =>
        new(name, details, color, action);

    private void AddTools(Panel page, IEnumerable<ToolInfo> tools)
        => page.Controls.Add(CreateToolsFlow(tools));

    private static string ToolCaption(string name, bool compact) =>
        compact && name == "Limpeza de Disco automática"
            ? "Limpeza de Disco\nautomática"
            : name;

    private Control CreateToolsFlow(IEnumerable<ToolInfo> tools, bool compact = false)
    {
        var host = new DarkScrollableFlow { Dock = DockStyle.Fill, BackColor = AppServices.Background };
        var flow = host.Content;
        flow.Padding = new Padding(12);
        flow.BackColor = AppServices.Background;
        foreach (ToolInfo tool in tools)
        {
            var card = new Panel
            {
                Size = new Size(compact ? 268 : 286, compact ? 146 : 116),
                BackColor = PanelBackground,
                Margin = compact ? new Padding(3, 4, 9, 8) : new Padding(3, 4, 13, 10),
                Padding = new Padding(8)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = PanelBackground,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, compact ? 58 : 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var button = CreateButton(ToolCaption(tool.Name, compact), tool.Color, Color.White);
            if (compact && button is ModernButton modernButton)
                modernButton.AllowMultilineText = true;
            button.Dock = DockStyle.Fill;
            button.Margin = Padding.Empty;
            button.MinimumSize = new Size(0, compact ? 54 : 44);
            button.AccessibleName = tool.Name;
            button.AccessibleDescription = tool.Description;
            _toolTips.SetToolTip(button, tool.Description);
            _actionControls.Add(button);
            if (tool.Name == "Reparar rede")
            {
                _repairNetworkButton = button;
                button.Enabled = false; // A correção depende de um diagnóstico anterior.
            }
            button.Click += async (_, _) =>
            {
                if (_busy) return;
                try { await tool.Action(); }
                catch (Exception ex) { ReportError(ex); }
            };

            var description = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = false,
                ForeColor = Muted,
                Font = new Font("Segoe UI", compact ? 8.5F : 8.6F),
                Text = tool.Description,
                Padding = new Padding(3, compact ? 6 : 8, 3, 0),
                TextAlign = ContentAlignment.TopLeft
            };

            _toolTips.SetToolTip(description, tool.Description);
            layout.Controls.Add(button, 0, 0);
            layout.Controls.Add(description, 0, 1);
            card.Controls.Add(layout);
            flow.Controls.Add(card);
        }
        host.Reflow();
        return host;
    }

    private Control BuildDiskPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1,
            Margin = new Padding(12, 0, 12, 8), Padding = new Padding(12, 8, 12, 8),
            BackColor = PanelBackground
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
        heading.Controls.Add(new Label
        {
            Text = "ESPAÇO EM DISCO", Dock = DockStyle.Fill,
            ForeColor = Ink, Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        var optimizeDisks = CreateSecondaryButton("Otimizar unidades");
        optimizeDisks.Dock = DockStyle.Fill;
        optimizeDisks.Margin = new Padding(5, 4, 5, 4);
        optimizeDisks.Click += async (_, _) => await ConfirmThenRun("optimize-drives",
            "O Windows escolherá a operação apropriada para cada unidade fixa (SSD/HD).\n\n" +
            "A análise/otimização pode demorar e solicitar autorização. Continuar?");
        _toolTips.SetToolTip(optimizeDisks, "O Windows executa a otimização apropriada a cada tipo de unidade. Pode demorar.");
        _actionControls.Add(optimizeDisks);
        heading.Controls.Add(optimizeDisks, 1, 0);

        _refreshDisksButton = CreateSecondaryButton("Atualizar discos");
        _refreshDisksButton.Dock = DockStyle.Fill;
        _refreshDisksButton.Margin = new Padding(5, 4, 0, 4);
        _refreshDisksButton.MinimumSize = new Size(150, 30);
        _refreshDisksButton.Click += async (_, _) => await RefreshDiskUsageAsync();
        _toolTips.SetToolTip(_refreshDisksButton, "Reconsultar o espaço livre e ocupado de cada unidade local.");
        _actionControls.Add(_refreshDisksButton);
        heading.Controls.Add(_refreshDisksButton, 2, 0);
        root.Controls.Add(heading, 0, 0);
        var previewRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = PanelBackground };
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248));
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var previewButton = CreateSecondaryButton("Analisar limpeza");
        previewButton.Dock = DockStyle.Fill;
        previewButton.Margin = new Padding(0, 5, 8, 5);
        previewButton.Click += async (_, _) => await ShowCleanupPreviewAsync();
        _actionControls.Add(previewButton);
        previewRow.Controls.Add(previewButton, 0, 0);
        var fullAudit = CreateSecondaryButton("Diagnóstico completo");
        fullAudit.Dock = DockStyle.Fill;
        fullAudit.Margin = new Padding(0, 5, 8, 5);
        fullAudit.Click += async (_, _) =>
            await ExecuteAsync("Analisando instalações e dispositivos...", RunMaintenanceAuditAsync);
        fullAudit.AccessibleName = "Diagnóstico completo";
        _toolTips.SetToolTip(fullAudit, "Revisa o inventário, os desinstaladores, " +
            "os registros de hardware desconectado e dispositivos com problemas. Não remove nada.");
        _actionControls.Add(fullAudit);
        previewRow.Controls.Add(fullAudit, 1, 0);
        _cleanupPreview.Text = "Analisar espaço e dispositivos sem apagar dados.";
        _cleanupPreview.Dock = DockStyle.Fill;
        _cleanupPreview.ForeColor = Muted;
        _cleanupPreview.TextAlign = ContentAlignment.MiddleLeft;
        _cleanupPreview.AutoEllipsis = true;
        previewRow.Controls.Add(_cleanupPreview, 2, 0);
        root.Controls.Add(previewRow, 0, 1);
        _diskSummary.Text = "Consulte o espaço livre e ocupado das unidades locais.";
        _diskSummary.Dock = DockStyle.Fill;
        _diskSummary.ForeColor = Muted;
        _diskSummary.Font = new Font("Segoe UI", 8.6F);
        _diskSummary.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_diskSummary, 0, 2);
        _diskCards.Dock = DockStyle.Fill;
        _diskCards.Content.Padding = new Padding(1, 8, 1, 4);
        _diskCards.Margin = Padding.Empty;
        _diskCards.BackColor = PanelBackground;
        _diskCards.Content.BackColor = PanelBackground;
        _diskCards.Resize += (_, _) => ResizeDiskCards();
        root.Controls.Add(_diskCards, 0, 3);
        return root;
    }

    private static string DisplayGigabytes(long bytes)
        => (bytes / 1_000_000_000d).ToString("N1", CultureInfo.CurrentCulture) + " GB";

    private Control CreateDiskCard(DiskUsageInfo disk)
    {
        long used = disk.TotalBytes - disk.FreeBytes;
        double fraction = Math.Clamp((double)used / disk.TotalBytes, 0d, 1d);
        int percent = (int)Math.Round(fraction * 100);
        string letter = disk.Root.TrimEnd('\\');
        string name = string.IsNullOrWhiteSpace(disk.Label) ? "Disco local" : disk.Label;
        var card = new TableLayoutPanel
        {
            Size = new Size(350, 124), RowCount = 4, ColumnCount = 1,
            BackColor = Color.FromArgb(38, 50, 63), Padding = new Padding(13, 7, 13, 9),
            Margin = new Padding(0, 0, 14, 12)
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(new Label
        {
            Text = $"{name} ({letter})", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Ink,
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
        }, 0, 0);
        card.Controls.Add(new Label
        {
            Text = $"Usado: {DisplayGigabytes(used)}    |    Livre: {DisplayGigabytes(disk.FreeBytes)}",
            Dock = DockStyle.Fill, ForeColor = Ink, TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 0, 1);
        card.Controls.Add(new Label
        {
            Text = $"Total: {DisplayGigabytes(disk.TotalBytes)}    |    Ocupação: {percent}%",
            Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 0, 2);
        var track = new Panel { Dock = DockStyle.Fill, Height = 13, BackColor = Color.FromArgb(57, 68, 81), Margin = new Padding(0, 7, 0, 0) };
        var fill = new Panel
        {
            BackColor = fraction >= .90d ? AppServices.Red : fraction >= .75d ? AppServices.Orange : AppServices.Blue
        };
        track.Controls.Add(fill);
        void UpdateFill()
        {
            fill.SetBounds(0, 0, Math.Clamp((int)Math.Round(track.ClientSize.Width * fraction), 0, track.ClientSize.Width),
                track.ClientSize.Height);
        }
        track.Resize += (_, _) => UpdateFill();
        card.Controls.Add(track, 0, 3);
        return card;
    }

    private void ResizeDiskCards()
    {
        if (_diskCards.IsDisposed || _diskCards.Content.Controls.Count == 0) return;
        int area = _diskCards.Content.ClientSize.Width - _diskCards.Content.Padding.Horizontal - 23;
        if (area <= 0) return;
        int columns = area >= 740 ? 2 : 1;
        int target = Math.Max(250, (area / columns) - 14);
        foreach (Control card in _diskCards.Content.Controls)
            if (card.Width != target) card.Width = target;
        _diskCards.Reflow();
    }

    private async Task RefreshDiskUsageAsync()
    {
        if (_refreshingDisks || IsDisposed) return;
        _refreshingDisks = true;
        if (_refreshDisksButton is { IsDisposed: false }) _refreshDisksButton.Enabled = false;
        _diskSummary.Text = "Consultando espaço das unidades...";
        try
        {
            var volumes = await Task.Run(AppServices.GetLocalDiskUsage);
            if (IsDisposed || _diskCards.IsDisposed) return;
            _diskCards.Content.SuspendLayout();
            try
            {
                var oldCards = _diskCards.Content.Controls.Cast<Control>().ToArray();
                _diskCards.Content.Controls.Clear();
                foreach (Control oldCard in oldCards) oldCard.Dispose();
                foreach (DiskUsageInfo volume in volumes)
                    _diskCards.Content.Controls.Add(CreateDiskCard(volume));
                _diskSummary.Text = volumes.Count switch
                {
                    0 => "Nenhuma unidade local disponível para consulta.",
                    1 => "1 unidade local — consulta somente de leitura.",
                    _ => $"{volumes.Count} unidades locais — consulta somente de leitura."
                };
                ResizeDiskCards();
            }
            finally { _diskCards.Content.ResumeLayout(true); }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _diskSummary.Text = "Não foi possível consultar o espaço em disco.";
                Log("Consulta de discos: " + ex.Message);
            }
        }
        finally
        {
            _refreshingDisks = false;
            if (_refreshDisksButton is { IsDisposed: false }) _refreshDisksButton.Enabled = !_busy;
        }
    }

    private Control BuildOperationsPanel()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            Padding = new Padding(0, 2, 0, 0), BackColor = AppServices.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.Controls.Add(new Label { Text = "REGISTRO", Dock = DockStyle.Fill, ForeColor = Ink,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _status.Text = "Pronto";
        _status.Dock = DockStyle.Fill;
        _status.AutoEllipsis = true;
        _status.ForeColor = Muted;
        _status.TextAlign = ContentAlignment.MiddleRight;
        top.Controls.Add(_status, 1, 0);
        root.Controls.Add(top, 0, 0);
        _progress.Dock = DockStyle.Fill;
        _progress.Margin = new Padding(0, 0, 0, 2);
        _progress.Style = ProgressBarStyle.Blocks;
        _progress.Visible = false;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Value = 0;
        root.Controls.Add(_progress, 0, 1);
        _log.Dock = DockStyle.Fill;
        root.Controls.Add(_log, 0, 2);
        return root;
    }

    private static Button CreateSecondaryButton(string text) => new ModernButton
    {
        Text = text,
        BackColor = Color.FromArgb(43, 56, 70),
        ForeColor = Color.FromArgb(221, 235, 252),
        OutlineColor = Color.FromArgb(85, 112, 137),
        Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        CornerRadius = 10
    };

    private static Button CreateButton(string text, Color background, Color foreground) => new ModernButton
    {
        Text = text, BackColor = background, ForeColor = foreground,
        Font = new Font("Segoe UI", 9.3F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        CornerRadius = 10
    };

    private void AddAction(TableLayoutPanel layout, int row, string text, Color color, Func<Task> action)
    {
        var button = CreateButton(text, color, Color.White);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(0, 3, 0, 10);
        _actionControls.Add(button);
        button.Click += async (_, _) =>
        {
            if (_busy) return;
            try { await action(); }
            catch (Exception ex) { ReportError(ex); }
        };
        layout.Controls.Add(button, 0, row);
    }

    public void Log(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { BeginInvoke(new Action<string>(Log), message); } catch (InvalidOperationException) { } return; }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void Status(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(Status), text); return; }
        _operationResult = text;
        _status.Text = text;
    }

    private async Task ExecuteAsync(string description, Func<Task> work)
    {
        if (_busy) return;
        _busy = true;
        _programList.ActionsEnabled = false;
        UpdateBulkButtons();
        // Não desabilitar todos os botões: isso causava piscadas no painel Reparo e rede.
        // O bloqueio de operações paralelas é realizado por _busy no manipulador do clique.
        _search.Enabled = false;
        _progress.Visible = true;
        _progress.Style = ProgressBarStyle.Marquee; // Sem percentual inventado para comandos externos.
        _progress.MarqueeAnimationSpeed = 24;
        _status.Text = description;
        try { await work(); }
        catch (Exception ex) { ReportError(ex); }
        finally
        {
            _progress.MarqueeAnimationSpeed = 0;
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Value = 0;
            _progress.Visible = false;
            _status.Text = _operationResult;
            _search.Enabled = true;
            _busy = false;
            _programList.ActionsEnabled = true;
            UpdateBulkButtons();
            if (_checkUpdatesButton is not null && _consultaCatalogoCts is null)
                _checkUpdatesButton.Enabled = true;
            if (_repairNetworkButton is not null)
                _repairNetworkButton.Enabled = GetSuggestedNetworkRepair(_lastNetworkScan) is not null;
            if (_installSelectedUpdates is not null)
                _installSelectedUpdates.Enabled = _activeSystemView == "update" && _updateSelection.Count > 0;
            if (_nav.SelectedIndex == 1)
                _ = RefreshDiskUsageAsync();
            if (_nav.SelectedIndex == 3)
                _ = RefreshEnergyInfoAsync();
        }
    }

    private void ReportError(Exception ex)
    {
        Log("ERRO: " + ex.Message);
        Status("Não foi possível concluir a operação");
        MessageBox.Show(this, ex.Message, "Gerenciador Final", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // O registro local independe de provedores de atualizacao.
    private async Task RefreshCatalogAsync()
    {
        DiagnosticTrace.Write("BOTAO_ATUALIZAR_LISTA.INICIO");
        try { await LoadInstalledAsync(); }
        finally { DiagnosticTrace.Write("BOTAO_ATUALIZAR_LISTA.FIM"); }
    }

    private async Task LoadInstalledAsync()
    {
        DiagnosticTrace.Write("REGISTRO_PROGRAMAS.INICIO");
        List<ProgramEntry> items;
        try
        {
            var scan = await Task.Run(AppServices.ScanAllInstalledApps);
            items = scan.Items.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            DiagnosticTrace.Write($"REGISTRO_PROGRAMAS.LIDO quantidade={items.Count} examinados={scan.Registry.Inspected} ocultos={scan.Registry.HiddenComponents} updates_arp={scan.Registry.UpdateEntries} erros={scan.Registry.ReadFailures} duplicados={scan.Registry.Duplicates}");
            DiagnosticTrace.Write($"MSIX.LEITURA status={scan.Msix.Status} enumerados={scan.Msix.Seen} " +
                $"aplicativos={scan.Msix.Items.Count} adicionados={scan.Added} filtrados={scan.Msix.Filtered} falhas={scan.Msix.Failures}");
            DiagnosticTrace.Write($"JOGOS.LEITURA steam={scan.Games.Steam} epic={scan.Games.Epic} adicionados={scan.GamesAdded} parciais={scan.Games.Partial}");
            Log($"Inventário: {scan.Registry.Items.Count} programas Win32, {scan.Added} aplicativos MSIX e {scan.GamesAdded} jogos de bibliotecas adicionais. " +
                $"{scan.Registry.HiddenComponents} componentes internos e {scan.Registry.UpdateEntries} atualizações individuais do Windows não foram incluídos.");
        }
        catch (Exception ex)
        {
            DiagnosticTrace.Failure("REGISTRO_PROGRAMAS.FALHA", ex);
            throw;
        }
        _catalogoGeneration++;
        _consultaCatalogoCts?.Cancel();
        _installed.Clear();
        _installed.AddRange(items);
        _available.Clear();
        _catalogoVerificado = false;
        _checkedApps.Clear();
        _multiSelectMode = false;
        _programList.MultiSelectMode = false;
        _appFilter = "Todos";
        DiagnosticTrace.Write("REGISTRO_PROGRAMAS.RENDER_INICIO");
        RenderPrograms();
        DiagnosticTrace.Write("REGISTRO_PROGRAMAS.RENDER_FIM");
        Log($"Lista local atualizada: {_installed.Count} programa(s). Nenhuma consulta online iniciada.");
        Status($"{_installed.Count} programas encontrados");
    }

    private async Task LoadUpdatesAsync(bool forceRefresh = false)
    {
        // Fontes HTTPS oficiais trabalham em paralelo: uma falha não apaga o resultado
        // das outras. Não executam instaladores, comandos externos, WinGet ou COM.
        if (_consultaCatalogoCts is not null || IsDisposed || _installed.Count == 0) return;
        int generation = _catalogoGeneration;
        ProgramEntry[] snapshot = _installed.ToArray();
        // Preservar o mesmo número e a mesma ordem das linhas para a UI.
        // Entradas somente leitura (jogos e MSIX nao verificados) nao
        // recebem candidatos nem receitas automáticas de atualização.
        var installed = snapshot.Select(x => new InstalledApp(x.Name, x.Version, x.Publisher,
            !x.InventoryOnly || x.VerifiedMsixIdentity)).ToArray();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(65));
        var watch = Stopwatch.StartNew();
        _consultaCatalogoCts = cancellation;
        if (!_busy)
        {
            // Consulta somente leitura: duração incerta, indicar atividade sem inventar percentual.
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.MarqueeAnimationSpeed = 28;
            _progress.Visible = true;
            Status("Verificando fontes oficiais de atualizações…");
        }
        RenderTitle();
        if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar consulta";
        DiagnosticTrace.Write("MULTIFONTES.CONSULTA_INICIO fontes=ruckzuck,github,mozilla");
        Log("Consultando fontes oficiais por HTTPS em paralelo: RuckZuck, GitHub Releases e Mozilla.");
        try
        {
            using var primary = new RuckZuckCatalog();
            using var official = new OfficialReleaseSources();
            async Task<(IReadOnlyList<UpdateMatch>? Matches, int Count, bool Cached, long NetworkMs,
                long CompareMs, string Error)> ReadRuckZuckAsync()
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    var catalog = await primary.GetCatalogAsync(cancellation.Token, forceRefresh);
                    long networkMs = timer.ElapsedMilliseconds;
                    var compute = Stopwatch.StartNew();
                    var matches = await Task.Run(() => Matching.Evaluate(installed, catalog), cancellation.Token);
                    compute.Stop();
                    return (matches, catalog.Count, primary.UsedMemoryCache, networkMs,
                        compute.ElapsedMilliseconds, "");
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or
                    System.Text.Json.JsonException or TaskCanceledException or IOException)
                {
                    return (null, 0, false, timer.ElapsedMilliseconds, 0, ex.GetType().Name);
                }
            }
            var ruckTask = ReadRuckZuckAsync();
            var officialTask = official.QueryAsync(installed, cancellation.Token, forceRefresh);
            await Task.WhenAll(ruckTask, officialTask);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != _catalogoGeneration || IsDisposed) return;
            var rz = await ruckTask;
            var others = await officialTask;
            var merged = await Task.Run(
                () => MultiSourceMatcher.Merge(installed, rz.Matches, others.Findings), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != _catalogoGeneration || IsDisposed) return;
            if (rz.Matches is null && others.Findings.Count == 0)
            {
                InvalidarAtualizacoes();
                Log("Todas as fontes consultadas estavam indisponíveis ou não cobrem os programas reconhecidos.");
                Status("Fontes indisponíveis; aplicativos preservados");
                DiagnosticTrace.Write("MULTIFONTES.SEM_RESULTADOS");
                return;
            }
            AplicarCandidatos(merged);
            _catalogoVerificado = true;
            RenderPrograms();
            int candidates = merged.Count(x => x.State == MatchState.Candidate);
            int uncertain = merged.Count(x => Matching.IsUncertain(x.State));
            int superseded = merged.Count(x => x.State == MatchState.SupersededRegistration);
            int officialCandidates = merged.Count(x => x.State == MatchState.Candidate &&
                x.Provider != "RuckZuck");
            if (superseded > 0)
                Log($"Visual C++: {superseded} registro(s) antigo(s) já atendidos por versões recentes.");
            if (rz.Matches is null)
                Log($"RuckZuck indisponível ({rz.Error}); outras fontes continuam utilizáveis.");
            else
                Log($"RuckZuck: {rz.Count} pacotes ({(rz.Cached ? "cache" : "HTTPS")}, {rz.NetworkMs} ms); comparação {rz.CompareMs} ms.");
            foreach (var diagnostic in others.Diagnostics)
                Log($"Fonte oficial {diagnostic.Provider}: {diagnostic.Status}, {diagnostic.Milliseconds} ms.");
            DiagnosticTrace.Write($"MULTIFONTES.CONSULTA_OK instalados={installed.Length} candidatos={candidates} " +
                $"fontes_oficiais={officialCandidates} reconhecidos={others.Findings.Count} " +
                $"fontes_consultadas={others.Diagnostics.Count} fontes_ok={others.Diagnostics.Count(x => x.Status == "ok")} " +
                $"incertos={uncertain} ruckzuck_entradas={rz.Count} " +
                $"ruckzuck_ms={rz.NetworkMs} comparacao_ms={rz.CompareMs} total_ms={watch.ElapsedMilliseconds}");
            Log("Instalação automática permanece limitada a instaladores oficiais com receitas verificadas.");
            Status($"Consulta concluída: {candidates} possíveis atualizações · múltiplas fontes");
        }
        catch (OperationCanceledException)
        {
            if (generation != _catalogoGeneration || IsDisposed) return;
            InvalidarAtualizacoes();
            DiagnosticTrace.Write("MULTIFONTES.CONSULTA_CANCELADA");
            Log("Consulta cancelada ou esgotada; a lista local permanece disponível.");
            Status("Consulta cancelada ou tempo esgotado");
        }
        catch (Exception ex)
        {
            DiagnosticTrace.Failure("MULTIFONTES.CONSULTA_FALHA", ex);
            if (generation != _catalogoGeneration || IsDisposed) return;
            InvalidarAtualizacoes();
            Log("Falha geral na consulta multifontes (" + ex.GetType().Name + ").");
            Status("Consulta não concluída; lista local preservada");
        }
        finally
        {
            _consultaCatalogoCts = null;
            if (!IsDisposed)
            {
                if (!_busy)
                {
                    _progress.MarqueeAnimationSpeed = 0;
                    _progress.Style = ProgressBarStyle.Blocks;
                    _progress.Value = 0;
                    _progress.Visible = false;
                }
                RenderTitle();
                if (_checkUpdatesButton is not null)
                    _checkUpdatesButton.Text = "Verificar atualizações";
            }
        }
    }

    private void BeginStartupChecks()
    {
        Log("Inicialização: consultando atualizações de aplicativos e Windows Update em segundo plano.");
        BeginBackgroundProcessSampling();
        _ = LoadUpdatesAsync();
        _ = ScanWindowsUpdatesInBackgroundAsync();
    }

    private void AplicarCandidatos(IReadOnlyList<UpdateMatch> matches)
    {
        _available.Clear();
        for (int i = 0; i < _installed.Count; i++)
        {
            var item = _installed[i];
            var match = matches[i];
            item.ComparisonState = match.State;
            item.IsUpdate = match.State == MatchState.Candidate && match.Catalog is not null;
            item.AvailableVersion = match.Catalog?.ProductVersion ?? "";
            item.PackageId = ""; // Identificador de catálogo NÃO autoriza instalação.
            item.Source = item.IsUpdate ? match.Provider : "";
            // A correspondencia remota jamais autoriza instalar pacotes.
        }
        _checkedApps.RemoveWhere(entry => !_installed.Contains(entry));
    }

    private void InvalidarAtualizacoes()
    {
        _catalogoVerificado = false;
        _available.Clear();
        foreach (var entry in _installed)
        {
            entry.IsUpdate = false;
            entry.ComparisonState = MatchState.Unmatched;
            entry.AvailableVersion = "";
            entry.PackageId = "";
            entry.Source = "";
        }
        if (_appFilter == "Com atualização") _appFilter = "Todos";
        RenderPrograms();
    }

    private void RenderPrograms()
    {
        string query = _search.Text.Trim();
        IEnumerable<ProgramEntry> filtered = _installed.Where(entry => query.Length == 0 ||
            string.Join(" ", entry.Name, entry.Version, entry.Publisher, entry.PackageId, entry.AvailableVersion,
                entry.CatalogIdentity, entry.CatalogDisplayName, entry.InventorySource,
                entry.MsiProductCode)
                .Contains(query, StringComparison.CurrentCultureIgnoreCase));
        filtered = _appFilter switch
        {
            "Com atualização" => filtered.Where(x => x.IsUpdate),
            "Selecionados" => filtered.Where(x => _checkedApps.Contains(x)),
            _ => filtered
        };
        var match = filtered.OrderByDescending(x => x.IsUpdate)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Version, StringComparer.CurrentCultureIgnoreCase).ToArray();
        _programList.SetItems(match);
        _programList.ScrollToTop();
        _emptyState.Text = query.Length > 0 ? "Nenhum aplicativo corresponde à pesquisa."
            : _appFilter == "Selecionados" ? "Nenhum aplicativo marcado."
            : _appFilter == "Com atualização" && _catalogoVerificado ? "Nenhuma possível atualização encontrada neste catálogo."
            : _appFilter == "Com atualização" ? "Possíveis atualizações ainda não verificadas."
            : "Nenhum aplicativo identificado neste filtro.";
        _emptyState.Visible = match.Length == 0;
        if (_emptyState.Visible) _emptyState.BringToFront();
        RenderTitle();
        UpdateBulkButtons();
        ApplyFilterAppearance();
    }

    private void RenderTitle()
    {
        int updates = _installed.Count(x => x.IsUpdate);
        string windowsStatus = _windowsUpdateCheckingMessage();
        _listTitle.Text = $"Aplicativos: {_installed.Count} · Possíveis atualizações: {(_catalogoVerificado ? updates.ToString() : "não verificadas")}" +
            (_consultaCatalogoCts is not null ? " · Verificando..." : "") +
            windowsStatus + (_multiSelectMode ? $" · Marcados: {_checkedApps.Count}" : "");
    }

    private string _windowsUpdateCheckingMessage()
    {
        if (_windowsUpdateScanning) return " · Windows Update: consultando";
        if (_windowsUpdateChecked) return $" · Windows Update: {_updateCatalog.Count}";
        if (_windowsUpdateScanFailure is not null) return " · Windows Update: indisponível";
        return "";
    }

    private void ApplyFilterAppearance()
    {
        foreach (var (button, name) in new[]
        {
            (_allFilterButton, "Todos"), (_updatesFilterButton, "Com atualização"),
            (_selectedFilterButton, "Selecionados")
        })
        {
            if (button is null) continue;
            button.BackColor = _appFilter == name ? Color.FromArgb(43, 66, 89) : Color.FromArgb(43, 56, 70);
            button.ForeColor = _appFilter == name ? Accent : Ink;
            button.Invalidate();
        }
    }

    private void ToggleMultiSelection()
    {
        if (_busy) return;
        _multiSelectMode = !_multiSelectMode;
        _programList.MultiSelectMode = _multiSelectMode;
        if (!_multiSelectMode)
        {
            _checkedApps.Clear();
            if (_appFilter == "Selecionados") _appFilter = "Todos";
        }
        RenderPrograms();
    }

    private void ClearMultiSelection()
    {
        if (_busy) return;
        _checkedApps.Clear();
        if (_appFilter == "Selecionados") _appFilter = "Todos";
        RenderPrograms();
    }

    private void UpdateBulkButtons()
    {
        if (_selectManyButton is not null)
        {
            _selectManyButton.Text = _multiSelectMode ? "Sair da seleção" : "Selecionar vários";
            _selectManyButton.Enabled = !_busy;
        }
        if (_bulkUninstallButton is not null)
            _bulkUninstallButton.Enabled = !_busy && _multiSelectMode && _checkedApps.Count > 0;
        if (_clearSelectionButton is not null)
            _clearSelectionButton.Enabled = !_busy && _checkedApps.Count > 0;
    }

    private void ShowUpdateGuidance(ProgramEntry entry)
    {
        if (!entry.IsUpdate || _busy) return;
        string message = entry.InventoryOnly
            ? "Componente do Windows/Store: abra Configurações ou Microsoft Store e procure atualizações oficiais."
            : "Sem instalador validado: abra o próprio aplicativo, procure Ajuda ou Configurações e use a função oficial de atualização.";
        Log("Orientação para " + entry.Name + " · " + message);
        Status("Orientações de atualização exibidas no histórico de operações.");
    }

    private void OpenMicrosoftStore(ProgramEntry entry)
    {
        if (_busy) return;
        Uri? destination = VerifiedUpdaters.GetMicrosoftStoreUri(entry);
        if (destination is null) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = destination.AbsoluteUri, UseShellExecute = true });
            bool gameInput = entry.Name.Equals("Microsoft GameInput", StringComparison.OrdinalIgnoreCase);
            Log(gameInput
                ? "Microsoft Store aberta em Atualizações e downloads. Clique em Verificar se há atualizações. O GameInput pode ser atualizado pelo Windows."
                : "Página oficial aberta. Se mostrar Abrir, não há atualização oferecida pela Store nesta página agora. O catálogo pode indicar uma versão ainda não distribuída.");
            Status(gameInput
                ? "Store aberta: clique em Verificar se há atualizações."
                : "Store aberta: Atualizar instala; Abrir indica que não há atualização oferecida nesta página.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            string? fallback = VerifiedUpdaters.GetStoreWebFallback(entry);
            try
            {
                if (fallback is null)
                {
                    Process.Start(new ProcessStartInfo { FileName = "ms-settings:windowsupdate", UseShellExecute = true });
                    Log("Microsoft Store indisponível; abrindo Windows Update para componentes do sistema.");
                    Status("Microsoft Store indisponível. Verifique as atualizações do Windows.");
                }
                else
                {
                    Process.Start(new ProcessStartInfo { FileName = fallback, UseShellExecute = true });
                    Log("Link web oficial da Microsoft Store aberto como alternativa.");
                    Status("Página oficial da Microsoft Store aberta no navegador.");
                }
            }
            catch (Exception inner) when (inner is Win32Exception or InvalidOperationException)
            {
                Log("Microsoft Store indisponível: " + inner.GetType().Name);
                Status("Não foi possível abrir a Microsoft Store.");
            }
        }
    }

    private void OpenOfficialSite(ProgramEntry entry)
    {
        if (_busy) return;
        string? address = VerifiedUpdaters.GetOfficialPage(entry);
        if (address is null) return;
        try
        {
            // Endereço é estático no código e não vem de catálogos externos.
            Process.Start(new ProcessStartInfo { FileName = address, UseShellExecute = true });
            Log("Página oficial do fabricante aberta a pedido do usuário; nenhuma instalação iniciada.");
        }
        catch (Exception ex)
        {
            Status("Não foi possível abrir o site oficial.");
            Log("Falha ao abrir navegador (" + ex.GetType().Name + ").");
        }
    }

    private async Task UpdateWindowsTerminalAsync(ProgramEntry entry)
    {
        if (_busy) return;
        TerminalUpdateRecipe? recipe = WindowsTerminalUpdater.Resolve(entry);
        if (recipe is null)
        {
            Status("Esta versao do Windows Terminal nao possui pacote GitHub validado.");
            return;
        }
        Version? before = WindowsTerminalUpdater.CurrentUserVersion();
        if (before is null)
        {
            Status("Terminal MSIX nao identificado. Confira a instalacao na Store ou no site oficial.");
            Log("Atualizacao GitHub bloqueada: identidade Microsoft.WindowsTerminal nao encontrada no inventario MSIX.");
            return;
        }
        if (!Version.TryParse(recipe.Version, out var targetVersion) || before >= targetVersion)
        {
            Status("O Windows Terminal ja possui a versao selecionada.");
            return;
        }
        string previousFilter = _appFilter;
        await ExecuteAsync("Preparando atualizacao oficial do Windows Terminal...", async () =>
        {
            string folder = Path.Combine(Path.GetTempPath(), "GerenciadorFinal-Terminal-" + Guid.NewGuid().ToString("N"));
            try
            {
                DiagnosticTrace.Write("TERMINAL_GITHUB.INICIO");
                string bundle;
                using (var downloadCts = new CancellationTokenSource(TimeSpan.FromMinutes(6)))
                {
                    _installerDownloadCts = downloadCts;
                    if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar download";
                    try
                    {
                        bundle = await WindowsTerminalUpdater.DownloadAndVerifyAsync(
                            recipe, folder, Status, downloadCts.Token);
                    }
                    finally
                    {
                        _installerDownloadCts = null;
                        if (!IsDisposed && _checkUpdatesButton is not null)
                            _checkUpdatesButton.Text = "Verificar atualizacoes";
                    }
                }
                DiagnosticTrace.Write("TERMINAL_GITHUB.MSIX_VALIDADO");
                // O fechamento e a confirmacao acontecem APOS verificar o pacote
                // e ANTES de acionar o mecanismo de instalacao do Windows.
                bool closed = await UpdateProcessCoordinator.EnsureClosedAsync(
                    UpdateProcessTarget.WindowsTerminal,
                    question => DarkConfirmDialog.Confirm(this, question, "Fechar Windows Terminal para atualizar") == DialogResult.Yes,
                    message => { Status(message); Log(message); });
                if (!closed)
                {
                    DiagnosticTrace.Write("TERMINAL_GITHUB.FECHAMENTO_RECUSADO_OU_INSEGURO");
                    Status("Atualização interrompida: Windows Terminal precisa estar fechado.");
                    return;
                }
                DiagnosticTrace.Write("TERMINAL_GITHUB.PROCESSOS_VERIFICADOS_FECHADOS");
                await WindowsTerminalUpdater.DeployAsync(bundle, Status);
                DiagnosticTrace.Write("TERMINAL_GITHUB.INSTALADOR_CONCLUIDO");
                Version? installed = WindowsTerminalUpdater.CurrentUserVersion();
                bool confirmed = installed is not null && installed >= targetVersion;
                if (!confirmed)
                {
                    Status("O Windows concluiu a operacao, mas a versao nova nao foi confirmada.");
                    Log("Windows Terminal: instalacao nao confirmada; nenhum sucesso presumido.");
                    DiagnosticTrace.Write("TERMINAL_GITHUB.VERSAO_NAO_CONFIRMADA");
                    return;
                }
                DiagnosticTrace.Write("TERMINAL_GITHUB.VERSAO_CONFIRMADA");
                Log("Windows Terminal " + installed + " confirmado pelo identificador MSIX Microsoft.WindowsTerminal.");
                await LoadInstalledAsync();
                await LoadUpdatesAsync();
                _appFilter = _catalogoVerificado ? previousFilter : "Todos";
                RenderPrograms();
                Status("Windows Terminal atualizado com sucesso. Lista de aplicativos renovada.");
            }
            catch (OperationCanceledException)
            {
                Status("Download do Terminal cancelado.");
                Log("Download cancelado; nenhum pacote foi instalado.");
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("TERMINAL_GITHUB.FALHA", ex);
                Log("Windows Terminal: falha no pacote ou instalacao " + ex.GetType().Name +
                    ". Feche o Terminal e tente novamente; se persistir, utilize a Microsoft Store.");
                Status("Nao foi possivel concluir a atualizacao do Windows Terminal.");
            }
            finally
            {
                try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
                catch (IOException) { Log("Nao foi possivel remover o pacote MSIX temporario."); }
                catch (UnauthorizedAccessException) { Log("Sem permissao para remover o pacote MSIX temporario."); }
            }
        });
    }

    private async Task UpdateThirdPartyAsync(ProgramEntry entry)
    {
        if (_busy) return;
        VerifiedUpdateRecipe? recipe = VerifiedUpdaters.Resolve(entry);
        if (recipe is null)
        {
            Status("Atualização automática indisponível para este programa.");
            return;
        }
        // O clique explícito em "Atualizar" autoriza preparar a atualização escolhida.
        // Sem caixa de diálogo redundante. UAC e janelas legítimas dos instaladores continuam.
        Status("Atualizando " + recipe.ProductName + ": preparando instalador oficial verificado...");
        Log("Atualização solicitada diretamente na lista: " + recipe.ProductName +
            " (" + recipe.Identifier + "). Origem e assinatura serão conferidas antes da execução.");
        string previousFilter = _appFilter;
        await ExecuteAsync("Preparando atualização de " + recipe.ProductName + "...", async () =>
        {
            string folder = Path.Combine(Path.GetTempPath(), "GerenciadorFinal-Update-" + Guid.NewGuid().ToString("N"));
            try
            {
                DiagnosticTrace.Write("INSTALACAO_OFICIAL.INICIO receita=" + recipe.Identifier);
                string verifiedInstaller;
                using (var downloadCts = new CancellationTokenSource(TimeSpan.FromMinutes(8)))
                {
                    _installerDownloadCts = downloadCts;
                    if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar download";
                    try
                    {
                        verifiedInstaller = await VerifiedUpdaters.DownloadAndVerifyAsync(recipe, folder, Status, downloadCts.Token);
                    }
                    finally
                    {
                        _installerDownloadCts = null;
                        if (!IsDisposed && _checkUpdatesButton is not null)
                            _checkUpdatesButton.Text = "Verificar atualizações";
                    }
                }
                DiagnosticTrace.Write("INSTALACAO_OFICIAL.ARQUIVO_VALIDADO receita=" + recipe.Identifier);
                // Fluxo compartilhado por instaladores que substituem arquivos em uso.
                // Restart Manager identifica processos pelos ARQUIVOS bloqueados,
                // nunca pelo simples fato de um navegador estar aberto.
                if (UpdateBlockingPreflight.MainProcessFor(recipe.Identifier) is not null)
                {
                    DiagnosticTrace.Write("INSTALACAO_OFICIAL.PREFLIGHT_INICIO");
                    bool ready = await UpdateBlockingPreflight.ReadyAsync(
                        recipe.Identifier,
                        question => DarkConfirmDialog.Confirm(this, question,
                            "Preparar atualização com segurança") == DialogResult.Yes,
                        message => { Status(message); Log(message); });
                    if (!ready)
                    {
                        DiagnosticTrace.Write("INSTALACAO_OFICIAL.PREFLIGHT_BLOQUEADO");
                        Status("Atualização não iniciada: há arquivos em uso ou fechamento não autorizado.");
                        return;
                    }
                    DiagnosticTrace.Write("INSTALACAO_OFICIAL.PREFLIGHT_APROVADO");
                }
                Status("Instalador verificado. Aguardando conclusão...");
                int exit = await VerifiedUpdaters.LaunchAsync(recipe, verifiedInstaller, Status);
                DiagnosticTrace.Write("INSTALACAO_OFICIAL.EXECUCAO_FIM codigo=" + exit);
                if (exit is not (0 or 3010))
                {
                    Log("Instalador não concluiu com sucesso (código " + exit + ").");
                    Status(recipe.Identifier == "obs-x64-32.2.2"
                        ? "Instalação do OBS cancelada ou bloqueada. Confira outros aplicativos usando arquivos do OBS."
                        : "Instalação não concluída (código " + exit + ").");
                    return;
                }
                await Task.Delay(1000);
                var apps = await Task.Run(AppServices.GetInstalledPrograms);
                bool confirmed = VerifiedUpdaters.IsInstalledVersionAtLeast(recipe.Identifier, recipe.Version, apps);
                if (confirmed)
                {
                    Log("Atualização confirmada no Registro do Windows: " + recipe.ProductName + " " + recipe.Version);
                    Status(exit == 3010 ? "Atualizado; reinicialização pode ser necessária." : "Atualização concluída e versão confirmada.");
                    DiagnosticTrace.Write("INSTALACAO_OFICIAL.VERSAO_CONFIRMADA");
                }
                else
                {
                    Log("Instalador finalizado, mas a nova versão não foi confirmada no Registro. Verifique o fabricante.");
                    Status("Instalador terminou; versão ainda não confirmada.");
                    DiagnosticTrace.Write("INSTALACAO_OFICIAL.VERSAO_NAO_CONFIRMADA");
                }
                await LoadInstalledAsync();
                // O catálogo é consultado novamente, sem exigir clique em Verificar.
                // A visualização anterior volta depois da consulta, evitando lista vazia.
                Log("Atualizando automaticamente a lista de versões disponíveis...");
                await LoadUpdatesAsync();
                _appFilter = _catalogoVerificado ? previousFilter : "Todos";
                RenderPrograms();
                Status(confirmed
                    ? (_catalogoVerificado ? "Atualização confirmada; lista de atualizações renovada."
                        : "Atualização confirmada; catálogo remoto temporariamente indisponível.")
                    : "Instalador finalizado; confira a versão no Registro.");
            }
            catch (OperationCanceledException)
            {
                Log("Download de atualização cancelado; nenhum instalador foi executado.");
                Status("Download cancelado.");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                Log("Autorização UAC cancelada; nenhum instalador concluído.");
                Status("Atualização cancelada pelo Windows.");
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("INSTALACAO_OFICIAL.FALHA", ex);
                Log("Falha de atualização: " + ex.GetType().Name + ". Nenhuma conclusão presumida.");
                Status("Falha ao atualizar: " + ex.Message);
            }
            finally
            {
                try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
                catch (Exception) { Log("Aviso: não foi possível limpar a pasta temporária da atualização."); }
            }
        });
    }

    private async Task UninstallCheckedAsync() => await UninstallEntriesAsync(_checkedApps.ToArray());

    private async Task UninstallSelectedAsync()
    {
        if (_programList.SelectedItem is ProgramEntry entry)
            await UninstallEntriesAsync(new[] { entry });
        else Status("Nenhum aplicativo selecionado");
    }

    private async Task UninstallEntriesAsync(IReadOnlyCollection<ProgramEntry> apps)
    {
        var entries = apps.ToArray();
        if (entries.Length == 0) { Status("Nenhum aplicativo marcado"); return; }
        if (entries.Any(x => x.InventoryOnly || ProtectedApplications.IsProtectedName(x.Name)))
        {
            Status("Componente do Windows protegido contra desinstalação");
            Log("Desinstalação impedida: item de inventário protegido.");
            return;
        }
        string preview = string.Join("\n", entries.Take(8).Select(x => "• " + x.Name));
        if (entries.Length > 8) preview += $"\n... e mais {entries.Length - 8}.";
        if (DarkConfirmDialog.Confirm(this,
            $"Deseja desinstalar {entries.Length} aplicativo(s)?\n\n{preview}\n\nOs desinstaladores podem remover dados.",
            "Confirmar desinstalação") != DialogResult.Yes)
        { Status("Desinstalação cancelada"); return; }

        int started = 0, failed = 0, confirmed = 0;
        var completed = new List<ProgramEntry>();
        foreach (ProgramEntry entry in entries)
        {
            try
            {
                string command = !string.IsNullOrWhiteSpace(entry.UninstallString)
                    ? entry.UninstallString : entry.QuietUninstallString;
                if (!AppServices.TryParseUninstaller(command, out string exe, out string args))
                {
                    failed++;
                    Log("Sem desinstalador registrado: " + entry.Name);
                    continue;
                }
                using Process? proc = Process.Start(new ProcessStartInfo
                {
                    FileName = exe, Arguments = args, UseShellExecute = true
                });
                if (proc is null)
                {
                    failed++;
                    Log("Não foi possível iniciar: " + entry.Name);
                    continue;
                }
                started++;
                Status("Aguardando desinstalação de " + entry.Name + "...");
                DiagnosticTrace.Write("DESINSTALACAO.FORNECEDOR_INICIADO");
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                try
                {
                    await proc.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    // Alguns fornecedores continuam em outros processos. Não inferir sucesso.
                    Log("O desinstalador continua trabalhando. Confira depois em Atualizar lista.");
                    DiagnosticTrace.Write("DESINSTALACAO.AGUA_RETORNO_PENDENTE");
                    continue;
                }
                // Código de saída não basta: confirmar remoção no Registro.
                bool removed = false;
                for (int retry = 0; retry < 7; retry++)
                {
                    var inventory = await Task.Run(AppServices.GetInstalledPrograms);
                    if (!UninstallResiduals.StillInstalled(entry, inventory))
                    {
                        removed = true;
                        break;
                    }
                    if (retry < 6) await Task.Delay(500);
                }
                if (removed)
                {
                    completed.Add(entry);
                    confirmed++;
                    DiagnosticTrace.Write("DESINSTALACAO.REGISTRO_AUSENTE");
                    Log("Desinstalação confirmada no Registro: " + entry.Name);
                }
                else
                {
                    failed++;
                    DiagnosticTrace.Write("DESINSTALACAO.REGISTRO_AINDA_PRESENTE");
                    Log("O aplicativo ainda aparece instalado: " + entry.Name +
                        ". Nenhuma limpeza de resíduos foi permitida.");
                }
                // A auditoria é separada das políticas que podem apagar arquivos.
                // Mesmo sem receita de limpeza, mostra locais específicos e possíveis
                // componentes relacionados, sempre SEM tocar no disco ou Registro.
                var currentInventory = await Task.Run(AppServices.GetInstalledPrograms);
                var audit = await Task.Run(() => ResidualAudit.Inspect(entry, currentInventory));
                DiagnosticTrace.Write($"DESINSTALACAO.AUDITORIA encontrados={audit.Findings.Count} relacionados={audit.RelatedRegisteredPrograms.Count} removido={audit.UninstallConfirmed}");
                if (audit.Findings.Count > 0 || audit.RelatedRegisteredPrograms.Count > 0)
                {
                    Log($"Auditoria de {entry.Name}: {audit.Findings.Count} local(is) possível(is) e " +
                        $"{audit.RelatedRegisteredPrograms.Count} componente(s) relacionado(s) ainda registrado(s).");
                    ResidualAuditDialog.ShowResults(this, audit);
                }
                else
                {
                    Log("Auditoria adicional: nenhum local específico reconhecido para " + entry.Name +
                        ". Isso não comprova ausência de outros vestígios.");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Log("Falha ao desinstalar " + entry.Name + ": " + ex.GetType().Name);
                DiagnosticTrace.Failure("DESINSTALACAO.FALHA", ex);
            }
        }

        // Executar somente depois de confirmar a remoção. Uma consulta sem
        // resíduos ou sem receita conhecida jamais deve apagar arquivos.
        foreach (var item in completed)
        {
            if (!UninstallResiduals.IsSupported(item))
            {
                Log("Registro removido; limpeza automática de resíduos não disponível para " + item.Name + ".");
                continue;
            }
            Status("Verificando resíduos de " + item.Name + "...");
            DiagnosticTrace.Write("DESINSTALACAO.RESIDUOS_CONSULTA_INICIO");
            var found = await Task.Run(() => UninstallResiduals.Inspect(item));
            DiagnosticTrace.Write("DESINSTALACAO.RESIDUOS_CONSULTA_FIM candidatos=" + found.Sum(x => x.Files.Count));
            if (found.Count == 0)
            {
                Log("Nenhum resíduo executável reconhecido encontrado para " + item.Name +
                    ". Configurações pessoais são preservadas.");
                continue;
            }
            int detected = found.Sum(x => x.Files.Count);
            int untouched = found.Sum(x => x.Preserved);
            string description = string.Join("\n", found.Take(3).Select(x =>
                "• " + x.Product + ": " + x.Files.Count + " arquivo(s) na pasta de instalação"));
            string question = $"Desinstalação de {item.Name} confirmada.\n\n" +
                $"Foram encontrados {detected} arquivo(s) residuais de instalação:\n" +
                description +
                $"\n\nDeseja limpar somente esses arquivos?\n" +
                $"Configurações, documentos, plugins e outros dados ({untouched} item(ns) não reconhecido(s)) serão preservados.";
            if (DarkConfirmDialog.Confirm(this, question, "Limpar resíduos após desinstalação") != DialogResult.Yes)
            {
                Log("Limpeza de resíduos recusada. Nenhum arquivo adicional removido.");
                DiagnosticTrace.Write("DESINSTALACAO.RESIDUOS_RECUSADO");
                continue;
            }
            // Revalidar imediatamente a remoção; caso outra instalação tenha surgido, não limpar.
            if (UninstallResiduals.StillInstalled(item, await Task.Run(AppServices.GetInstalledPrograms)))
            {
                Log("O aplicativo voltou a constar no Registro; resíduos preservados.");
                DiagnosticTrace.Write("DESINSTALACAO.RESIDUOS_ABORTADO_REINSTALACAO");
                continue;
            }
            int filesRemoved = 0, foldersRemoved = 0, errors = 0, preserved = 0;
            foreach (var candidate in found)
            {
                var outcome = await Task.Run(() => UninstallResiduals.Clean(item, candidate));
                filesRemoved += outcome.RemovedFiles;
                foldersRemoved += outcome.RemovedDirectories;
                errors += outcome.Failed;
                preserved += outcome.Preserved;
            }
            DiagnosticTrace.Write($"DESINSTALACAO.RESIDUOS_LIMPEZA arquivos={filesRemoved} pastas={foldersRemoved} falhas={errors} preservados={preserved}");
            Log($"Resíduos de {item.Name}: removidos {filesRemoved} arquivo(s) e {foldersRemoved} pasta(s) vazia(s); " +
                $"preservados {preserved} item(ns); falhas {errors}.");
            Status(errors == 0 ? "Limpeza de resíduos concluída com segurança."
                               : "Limpeza parcial: alguns arquivos requerem permissões ou estão em uso.");
        }
        await RefreshCatalogAsync();
        Status($"Desinstalação: {confirmed} confirmada(s), {failed} falha(s). " +
            "Auditoria adicional concluída; possíveis resíduos desconhecidos não foram apagados.");
    }

    private async Task ReviewDeepUninstallResiduesAsync()
    {
        // Não é a limpeza padrão, não pode ser disparada automaticamente.
        Status("Analisando catálogo com " + UninstallRuleCatalog.Count +
            " regras de aplicativos para limpeza avançada...");
        DiagnosticTrace.Write("LIMPEZA_AVANCADA.CONSULTA_INICIO");
        var installed = await Task.Run(AppServices.GetInstalledPrograms);
        var candidates = await Task.Run(() => AdvancedUninstallCleaner.Inspect(installed));
        DiagnosticTrace.Write("LIMPEZA_AVANCADA.CONSULTA_FIM programas=" + candidates.Count);
        if (candidates.Count == 0)
        {
            Log("Limpeza avançada: nenhum dado ou chave do Registro conhecido encontrado.");
            Status("Nenhum resíduo avançado reconhecido; outros vestígios não podem ser descartados.");
            return;
        }
        foreach (var item in candidates)
        {
            var targets = item.Folders.Select(p => "• Pasta: " + p)
                .Concat(item.RegistryKeys.Select(p => @"• Regedit: HKEY_CURRENT_USER\" + p));
            string preview = string.Join("\n", targets.Take(5));
            string warning =
                "MODO AVANÇADO — AÇÃO IRREVERSÍVEL\n\n" +
                item.App.Name + " não consta no Registro como instalado.\n\n" +
                "A limpeza pode apagar preferências, histórico local, documentos, backups e plugins " +
                "armazenados nas pastas exclusivas abaixo, além das chaves de Registro listadas:\n\n" +
                preview +
                $"\n\nArquivos identificados: {item.FileCount}. Itens especiais ignorados: {item.InaccessibleOrLinked}.\n" +
                "As exclusões não passam pela Lixeira. Não há garantia de apagamento forense em SSD.\n" +
                "Componentes compartilhados e outras contas serão preservados.\n\n" +
                "DESEJA EXCLUIR ESSES DADOS PERMANENTEMENTE?";
            if (DarkConfirmDialog.Confirm(this, warning,
                "Limpeza avançada — confirmar exclusão") != DialogResult.Yes)
            {
                DiagnosticTrace.Write("LIMPEZA_AVANCADA.RECUSADA");
                Log("Limpeza avançada de " + item.App.Name + " recusada. Nenhuma alteração.");
                continue;
            }
            // Reconsulta independente, a confirmação não vale para apps reinstalados.
            var fresh = await Task.Run(AppServices.GetInstalledPrograms);
            if (AdvancedUninstallCleaner.StillInstalled(item.App, fresh))
            {
                Status("O programa foi detectado novamente. Nenhum dado será apagado.");
                DiagnosticTrace.Write("LIMPEZA_AVANCADA.REINSTALADO");
                continue;
            }
            Status("Eliminando apenas dados exclusivos autorizados de " + item.App.Name + "...");
            var result = await Task.Run(() => AdvancedUninstallCleaner.Clean(item, fresh));
            DiagnosticTrace.Write($"LIMPEZA_AVANCADA.RESULTADO arquivos={result.Files} pastas={result.Directories} chaves={result.RegistryKeys} ignorados={result.Skipped} falhas={result.Failed}");
            Log($"Limpeza avançada de {item.App.Name}: {result.Files} arquivo(s), {result.Directories} pasta(s), " +
                $"{result.RegistryKeys} chave(s) HKCU removidos; {result.Skipped} ignorados; {result.Failed} falhas.");
            Status(result.Failed == 0 && result.Skipped == 0
                ? "Limpeza avançada concluída para alvos conhecidos. Outros vestígios podem permanecer."
                : "Limpeza avançada parcial. Dados protegidos ou inacessíveis foram preservados.");
        }
    }

    private async Task RunMaintenanceAuditAsync()
    {
        Status("Conferindo instalações registradas e dispositivos do Windows...");
        DiagnosticTrace.Write("DIAGNOSTICO_COMPLETO.INICIO");
        var apps = await Task.Run(() => AppServices.ScanAllInstalledApps().Items);
        var maintenanceTask = Task.Run(() => MaintenanceAudit.Inspect(apps));
        var gamesTask = Task.Run(GameLibraryAudit.Inspect);
        await Task.WhenAll(maintenanceTask, gamesTask);
        var report = await maintenanceTask;
        var games = await gamesTask;
        DiagnosticTrace.Write($"DIAGNOSTICO_COMPLETO.FIM instalados={report.Installed} reconhecidos={report.Recognized} desinstaladores_ausentes={report.MissingUninstallers} desinstaladores_indeterminados={report.UnverifiableUninstallers} pnp_consultados={report.Devices.Queried} desconectados={report.Devices.Disconnected} problemas={report.Devices.WithProblems} steam={games.Steam} epic={games.Epic} parcial={report.Devices.Partial || games.Partial}");
        Log($"DIAGNÓSTICO COMPLETO: {report.Installed} programas; " +
            $"{report.Recognized} associados ao catálogo de receitas específicas; " +
            $"{report.WithPublisher} com fabricante registrado e " +
            $"{report.WithVersion} com versão registrada; " +
            $"{report.UsableUninstallers} desinstaladores localizados; " +
            $"{report.MissingUninstallers} caminhos de desinstalação ausentes; " +
            $"{report.UnverifiableUninstallers} comandos não verificáveis.");
        Log($"BIBLIOTECAS DE JOGOS: {games.Steam} título(s) Steam e " +
            $"{games.Epic} título(s) Epic encontrados em manifestos existentes. " +
            "Não são confundidos com programas Win32 desinstaláveis.");
        if (games.Partial)
            Log("A leitura de algumas bibliotecas de jogos foi limitada ou não autorizada.");
        if (report.Devices.Supported)
        {
            Log($"DISPOSITIVOS: {report.Devices.Queried} examinados; " +
                $"{report.Devices.Disconnected} desconectados (NÃO significa driver inútil); " +
                $"{report.Devices.WithProblems} presentes com código de problema.");
            foreach (string group in report.Devices.Categories)
                Log("  Classe PnP: " + group);
            if (report.HistoricalPnP is { } oldDevices)
            {
                Log($"REGISTRO PnP: {oldDevices.Registered} instâncias PCI/USB/USBSTOR " +
                    $"enumeradas; {oldDevices.NotPresent} sem correspondência entre dispositivos presentes. " +
                    "Isso NÃO comprova drivers antigos dispensáveis.");
                foreach (string group in oldDevices.Buses)
                    Log("  Origem de registros históricos: " + group);
                if (oldDevices.Partial)
                    Log($"Acesso parcial ao Registro PnP: {oldDevices.ReadErrors} erro(s). " +
                        "Algumas instâncias podem não ter sido examinadas.");
            }
            else
                Log("A comparação histórica do Registro PnP não foi possível; " +
                    "o total de drivers antigos permanece desconhecido.");
        }
        else
            Log("Consulta PnP indisponível ou parcial. Não é possível concluir sobre drivers antigos.");
        foreach (var issue in report.Concerns)
            Log("Revisar: " + issue.Category + " — " + issue.Name + ": " + issue.Description);
        Log("Nenhum driver, arquivo de instalação ou chave de Registro foi removido pelo diagnóstico.");
        Status(report.Devices.Partial || games.Partial ||
               report.HistoricalPnP is null || report.HistoricalPnP.Partial
            ? "Diagnóstico parcial: consulte as informações e limitações no registro."
            : $"Diagnóstico: {report.MissingUninstallers} desinstaladores ausentes; " +
              $"{report.HistoricalPnP.NotPresent} registros PCI/USB históricos para revisão.");
    }

    private async Task ReviewOldUninstallResiduesAsync()
    {
        Status("Conferindo instalações removidas e arquivos residuais conhecidos...");
        var installed = await Task.Run(AppServices.GetInstalledPrograms);
        var orphans = await Task.Run(() => UninstallResiduals.FindPreviouslyUninstalled(installed));
        DiagnosticTrace.Write("DESINSTALACAO.REVISAO_ANTIGA programas=" + orphans.Count);
        if (orphans.Count == 0)
        {
            Log("Nenhum resíduo executável com receita de exclusão encontrado. Ampliando a auditoria histórica.");
            Status("Verificando vestígios históricos somente para leitura...");
        }
        foreach (var (app, candidates) in orphans)
        {
            int count = candidates.Sum(x => x.Files.Count);
            if (DarkConfirmDialog.Confirm(this,
                $"O aplicativo {app.Name} não está mais instalado.\n\n" +
                $"Há {count} arquivo(s) residuais reconhecidos nas antigas pastas de instalação.\n\n" +
                "Deseja remover somente esses arquivos?\n" +
                "Documentos, plugins e configurações pessoais permanecerão intocados.",
                "Limpeza de resíduos de aplicativos") != DialogResult.Yes)
            {
                Log("Limpeza de resíduos antigos recusada para " + app.Name);
                continue;
            }
            if (UninstallResiduals.StillInstalled(app, await Task.Run(AppServices.GetInstalledPrograms)))
            {
                Log("Registro de instalação detectado novamente; limpeza cancelada.");
                continue;
            }
            int removed = 0, failed = 0, kept = 0, emptyFolders = 0;
            foreach (var candidate in candidates)
            {
                var result = await Task.Run(() => UninstallResiduals.Clean(app, candidate));
                removed += result.RemovedFiles;
                failed += result.Failed;
                kept += result.Preserved;
                emptyFolders += result.RemovedDirectories;
            }
            DiagnosticTrace.Write($"DESINSTALACAO.REVISAO_LIMPEZA arquivos={removed} falhas={failed} preservados={kept}");
            Log($"Resíduos antigos de {app.Name}: {removed} arquivo(s) e {emptyFolders} pasta(s) vazia(s) removidos; " +
                $"{kept} item(ns) preservado(s); {failed} falha(s).");
        }
        // A análise histórica cobre produtos que já desapareceram do inventário.
        // NUNCA autoriza exclusão de chaves HKLM/HKCU, saves ou integrações.
        Status("Verificando vestígios antigos sem modificar o computador...");
        DiagnosticTrace.Write("RESIDUOS_HISTORICOS.CONSULTA_INICIO");
        var updatedInventory = await Task.Run(AppServices.GetInstalledPrograms);
        var historical = await Task.Run(() => HistoricalResidualAudit.Inspect(updatedInventory));
        DiagnosticTrace.Write($"RESIDUOS_HISTORICOS.CONSULTA_FIM programas={historical.Count} locais={historical.Sum(x => x.Findings.Count)}");
        Status("Analisando locais comuns de aplicativos e Registro, sem alterar arquivos...");
        DiagnosticTrace.Write("RESIDUOS_GERAIS.CONSULTA_INICIO");
        // Independe de ter observado a instalação ou a remoção do programa.
        // Todas as fontes são somente leitura, com tempo e quantidade limitados.
        // Jogos detectados apenas por manifestos Steam/Epic nao pertencem ao ARP.
        // Proteger seus dados ativos antes de sugerir vestigios historicos.
        var gameInventory = await Task.Run(GameLibraryAudit.Inspect);
        var broad = await Task.Run(() => BroadResidualDiscovery.Inspect(updatedInventory,
            gameInventory.ActiveNames, gameInventory.Partial));
        DiagnosticTrace.Write($"RESIDUOS_GERAIS.CONSULTA_FIM origens={broad.RootsChecked} examinados={broad.EntriesSeen} candidatos={broad.Reports.Count} jogos_ativos={gameInventory.ActiveNames.Count} inacessiveis={broad.RestrictedRoots} parcial={broad.Incomplete}");
        // Manter resultados com caminhos identificados pelas receitas específicas primeiro,
        // impedindo várias caixas de diálogo para o mesmo aplicativo histórico.
        var historicalNames = new HashSet<string>(historical.Select(r => r.Product),
            StringComparer.OrdinalIgnoreCase);
        var allReports = historical.Concat(broad.Reports.Where(r => !historicalNames.Contains(r.Product)))
            .ToArray();
        if (allReports.Length > 0)
        {
            Log($"Análise de resíduos: {allReports.Length} grupos com indícios. " +
                $"Verificados {broad.EntriesSeen} nomes em {broad.RootsChecked} origens." +
                (broad.Incomplete ? " Análise PARCIAL: limites de proteção alcançados." : ""));
            ResidualAuditDialog.ShowHistoricalResults(this, allReports);
        }
        else
        {
            Log("Nenhum indício reconhecido nos locais analisados. Isso NÃO comprova ausência total de vestígios." +
                (broad.Incomplete ? " Análise parcial devido a limites de acesso ou tempo." : ""));
        }
        Status(broad.Incomplete
            ? "Análise parcial concluída. Alguns locais não puderam ser examinados; nenhum dado incerto foi apagado."
            : "Análise concluída. Possíveis vestígios foram apresentados sem apagar dados incertos.");
    }

    private static string SimpleConfirmation(string task) => task switch
    {
        "clean-user-temp" => "Deseja limpar os arquivos temporários antigos do usuário?",
        "clean-windows-temp" => "Deseja limpar os arquivos temporários antigos do Windows?",
        "clear-update-cache" => "Deseja limpar o cache do Windows Update? A consulta de atualizações ficará temporariamente indisponível.",
        "cleanmgr-auto" => "Deseja executar a Limpeza de Disco do Windows?",
        "clean-directx-cache" => "Deseja limpar o cache antigo de shaders DirectX? Alguns jogos podem recompilar os arquivos.",
        "windows-repair" => "Deseja verificar e reparar os arquivos do Windows? Esta operação pode demorar.",
        "network-reset" => "Deseja redefinir a rede? A conexão pode ser interrompida.",
        "create-restore-point" => "Deseja criar um ponto de restauração?",
        _ => "Deseja executar esta operação?"
    };

    private async Task ConfirmThenRun(string task, string question)
    {
        if (_busy) return;
        if (DarkConfirmDialog.Confirm(this, SimpleConfirmation(task), "Confirmar operação") != DialogResult.Yes)
        { Log("Operação cancelada pelo usuário."); return; }
        if (task is "clean-user-temp" or "clean-directx-cache")
        {
            string path = task == "clean-user-temp"
                ? Path.GetTempPath()
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache");
            await ExecuteAsync(task == "clean-user-temp" ? "Limpando temporários do usuário..." : "Limpando cache antigo de shaders...", async () =>
            {
                if (!Directory.Exists(path))
                {
                    Log("A pasta da limpeza não existe neste perfil. Nenhuma alteração realizada.");
                    Status("Nenhum cache encontrado");
                    return;
                }
                Log("Pasta consultada: " + path);
                var result = await Task.Run(() => AppServices.CleanDirectorySafely(path, Log));
                Status(result.Failed == 0 ? "Limpeza concluída" : "Limpeza parcial, com avisos");
            });
            return;
        }
        await ExecuteAsync("Executando manutenção do Windows...", () => RunElevatedAsync(task));
    }

    private async Task RunElevatedAsync(string task)
    {
        // O UAC é solicitado, mas a tarefa não abre formulário/terminal próprio.
        // Mensagens trafegam apenas em memória pelo pipe nomeado desta sessão.
        string pipeName = "GA5122_" + Guid.NewGuid().ToString("N");
        string token = Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        string exe = Environment.ProcessPath ?? Application.ExecutablePath;
        var start = new ProcessStartInfo { FileName = exe, UseShellExecute = true, Verb = "runas" };
        if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "GerenciadorFinal.dll"));
        start.ArgumentList.Add("--worker");
        start.ArgumentList.Add(task);
        start.ArgumentList.Add(pipeName);
        start.ArgumentList.Add(token);
        Process? child = null;
        try { child = Process.Start(start) ?? throw new InvalidOperationException("O Windows não iniciou o processo administrativo."); }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { Status("Autorização administrativa cancelada"); Log("Operação cancelada na confirmação do UAC."); return; }
        using (child)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35)))
        {
            try { await server.WaitForConnectionAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                Log("Não foi possível conectar à tarefa administrativa. Verifique se o Windows ou antivírus bloqueou o programa.");
                Status("Falha de comunicação com operação administrativa"); return;
            }
            using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096, leaveOpen: true);
            string? handshake = await reader.ReadLineAsync();
            if (handshake != "HELLO|" + token)
            { Status("Falha de autenticação da tarefa administrativa"); return; }
            bool gotResult = false;
            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("LOG|", StringComparison.Ordinal)) Log(line[4..]);
                else if (line.StartsWith("DONE|", StringComparison.Ordinal))
                {
                    string code = line[5..];
                    Status(code == "0" ? "Operação concluída" : "Operação concluída com avisos");
                    gotResult = true;
                }
            }
            if (!gotResult) { Status("Operação interrompida ou sem retorno"); Log("A tarefa foi interrompida antes de enviar o resultado."); }
        }
    }

    private Task EmptyRecycleBinAsync()
    {
        if (DarkConfirmDialog.Confirm(this, "Deseja esvaziar a Lixeira? Os arquivos serão excluídos permanentemente.",
            "Esvaziar Lixeira") != DialogResult.Yes)
        { Status("Operação cancelada"); return Task.CompletedTask; }
        int result = NativeMethods.SHEmptyRecycleBin(Handle, null, 0x1 | 0x2 | 0x4);
        Log(result == 0 ? "Lixeira esvaziada." : "O Windows retornou código " + result + ".");
        Status(result == 0 ? "Lixeira esvaziada" : "Limpeza da Lixeira com avisos");
        return Task.CompletedTask;
    }

}

internal static class TextExtensions
{
    public static string TakeText(this string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
internal static class WorkerHost
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "clean-windows-temp", "clear-update-cache", "cleanmgr-auto", "windows-repair",
        "flush-dns", "network-repair-winsock", "network-reset", "optimize-drives", "hibernate-on", "hibernate-off",
        "power-plan-balanced", "power-plan-saver", "power-plan-ultimate", "create-restore-point"
    };

    public static async Task<int> RunAsync(string task, string pipeName, string token)
    {
        bool updateTask = TryParseUpdateTask(task, out _);
        bool restorePower = TryParseRestorePowerTask(task, out _);
        if ((!Allowed.Contains(task) && !updateTask && !restorePower) || !Regex.IsMatch(pipeName, @"^GA5122_[a-f0-9]{32}$") ||
            !Regex.IsMatch(token, @"^[a-f0-9]{32}$")) return 2;
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await pipe.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096) { AutoFlush = true };
            void Send(string kind, string message)
            {
                lock (writer)
                    writer.WriteLine(kind + "|" + message.Replace('\r', ' ').Replace('\n', ' ').TakeText(750));
            }
            Send("HELLO", token);
            int exit;
            try { exit = await ExecuteAsync(task, line => Send("LOG", line)); }
            catch (Exception ex) { Send("LOG", "ERRO: " + ex.Message); exit = 1; }
            Send("DONE", exit.ToString());
            return exit;
        }
        catch { return 3; } // Processo elevado não exibe janelas próprias.
    }

    private static bool TryParseRestorePowerTask(string task, out Guid id)
    {
        id = Guid.Empty;
        const string prefix = "power-plan-restore:";
        return task.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(task[prefix.Length..], "D", out id) && id != Guid.Empty;
    }

    private static bool TryParseUpdateTask(string task, out HashSet<string> ids)
    {
        ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!task.StartsWith("wu-install:", StringComparison.Ordinal)) return false;
        string[] parts = task["wu-install:".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 0 or > 30) return false;
        foreach (string part in parts)
        {
            if (!Guid.TryParseExact(part, "D", out Guid id)) return false;
            ids.Add(id.ToString("D"));
        }
        return ids.Count == parts.Length;
    }

    private static async Task<int> ExecuteAsync(string task, Action<string> log)
    {
        if (TryParseUpdateTask(task, out var updateIds))
            return await WindowsUpdateClient.InstallAsync(updateIds, log);
        if (TryParseRestorePowerTask(task, out var powerId))
            return await PowerPlanService.RestorePreviousAsync(powerId, log);

        async Task<int> Run(string exe, params string[] args)
        {
            log("Executando: " + exe + " " + string.Join(" ", args));
            var result = await AppServices.RunCommandAsync(exe, args,
                s => { if (!string.IsNullOrWhiteSpace(s)) log(s.TakeText(350)); });
            log("Código de saída: " + result.ExitCode);
            return result.ExitCode;
        }

        log("Permissão administrativa concedida. Progresso exibido na janela principal.");
        switch (task)
        {
            case "clean-windows-temp":
                return await Clean(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
            case "clear-update-cache":
            {
                log("Interrompendo os serviços relacionados ao Windows Update...");
                await Run("net.exe", "stop", "bits");
                await Run("net.exe", "stop", "wuauserv");
                try
                {
                    return await Clean(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "SoftwareDistribution", "Download"));
                }
                finally
                {
                    // Mesmo se houver exceção na limpeza, tenta recuperar os serviços.
                    log("Restabelecendo os serviços de atualização...");
                    await Run("net.exe", "start", "wuauserv");
                    await Run("net.exe", "start", "bits");
                }
            }
            case "cleanmgr-auto":
                log("Executando a Limpeza de Disco automática do Windows...");
                log("A limpeza de arquivos do sistema será tentada no mesmo processo elevado, sem abrir a janela do utilitário.");
                return await Run("cleanmgr.exe", "/VERYLOWDISK");
            case "windows-repair":
            {
                log("Etapa 1/2 — DISM: examinando e, se necessário, reparando a imagem do Windows.");
                int dismCode = await Run("dism.exe", "/Online", "/Cleanup-Image", "/RestoreHealth");
                if (dismCode != 0)
                {
                    log("A etapa DISM não terminou normalmente. SFC não foi iniciado; confira os detalhes acima.");
                    return dismCode;
                }
                log("Etapa 2/2 — SFC: verificando e tentando reparar os arquivos protegidos do Windows.");
                int sfcCode = await Run("sfc.exe", "/scannow");
                log(sfcCode == 0
                    ? "SFC terminou sem erro de execução. Confira o resultado textual acima para saber se houve reparos."
                    : "SFC terminou com avisos/erro. Consulte o resultado acima; pode ser necessária outra ação.");
                return sfcCode;
            }
            case "flush-dns": return await Run("ipconfig.exe", "/flushdns");
            case "network-repair-winsock":
            {
                int code = await Run("netsh.exe", "winsock", "reset");
                log("A redefinição do catálogo Winsock pode exigir reinicialização. O Windows não será reiniciado automaticamente.");
                return code;
            }
            case "network-reset":
            {
                int one = await Run("netsh.exe", "winsock", "reset");
                int two = await Run("netsh.exe", "int", "ip", "reset");
                log("Reinicie o computador para completar algumas alterações de rede.");
                return one == 0 && two == 0 ? 0 : 1;
            }
            case "optimize-drives":
            {
                int errors = 0;
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    log("Otimizando " + drive.Name);
                    if (await Run("defrag.exe", drive.Name.TrimEnd('\\'), "/O") != 0) errors++;
                }
                return errors == 0 ? 0 : 1;
            }
            case "create-restore-point":
                log("Solicitando ponto de restauração ao Windows...");
                return await Task.Run(() => RestorePointService.Request(log));
            case "power-plan-balanced": return await PowerPlanService.ApplyAsync("balanced", log);
            case "power-plan-saver": return await PowerPlanService.ApplyAsync("saver", log);
            case "power-plan-ultimate": return await PowerPlanService.ApplyAsync("ultimate", log);
            case "hibernate-on": return await Run("powercfg.exe", "-h", "on");
            case "hibernate-off": return await Run("powercfg.exe", "-h", "off");
            default: return 2;
        }

        async Task<int> Clean(string path)
        {
            var result = await Task.Run(() => AppServices.CleanDirectorySafely(path, log));
            return result.Failed == 0 ? 0 : 1;
        }
    }
}

internal static class NativeMethods
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
