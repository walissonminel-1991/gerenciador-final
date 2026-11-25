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
        if (args.Length == 1 && args[0] == WinGetComProcessCatalog.Argument)
            return Task.Run(WinGetComReader.Run).GetAwaiter().GetResult(); // COM reader owns an MTA thread
        // O trabalhador elevado executa comandos sem criar janelas auxiliares.
        if (args.Length == 4 && args[0] == "--worker")
            return WorkerHost.RunAsync(args[1], args[2], args[3]).GetAwaiter().GetResult();
        if (args.Length > 0 && args[0] == DriverElevatedUninstall.Command)
            return DriverElevatedUninstall.RunAsync(args).GetAwaiter().GetResult();
        if (args.Length == 1 && args[0] == StartupTrayRegistration.MonitorArgument)
            return GfMonitorStandalone.Run();

        if (!DiagnosticTrace.Start() || !DiagnosticTrace.Write("APP.INICIALIZACAO_INICIO"))
        {
            MessageBox.Show(
                "Não foi possível criar e confirmar o registro de diagnóstico. " +
                "O teste foi interrompido antes de iniciar a consulta de pacotes.\n\n" +
                "Motivo técnico: " + DiagnosticTrace.LastFailure,
                "Diagnóstico indisponível", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
        V269AdminLaunchTicket? reopenAdministrator = null;
        try
        {
            LocalPerformanceMetrics.Sink = sample => DiagnosticTrace.Write(sample.Format());
            LocalPerformanceMetrics.BeginStartup();
            // Telemetria voluntaria jamais pode impedir a abertura do programa.
            try { AnonymousTelemetryContext.Current = new AnonymousTelemetry(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                System.Security.SecurityException or ArgumentException)
            {
                AnonymousTelemetryContext.Current = null;
                DiagnosticTrace.Write("PRIVACIDADE.INIT_FALHA");
            }
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                DiagnosticTrace.Failure("APP.FALHA_INTERFACE", e.Exception);
                MessageBox.Show(
                    "Ocorreu um erro inesperado.\n\n" + e.Exception.Message,
                    "Gerenciador Final", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, _) =>
            {
                // Melhor esforco em falha fatal: codigo fixo, sem excecao bruta
                // e sem rede. Kill/queda de energia podem impedir esta gravacao.
                var recorder = AnonymousTelemetryContext.Current;
                recorder?.Record("APP.FALHA_NAO_TRATADA");
                recorder?.SaveOnExit(DiagnosticTrace.VersionLabel, normalExit: false);
            };
            bool startInTray = args.Length is 1 or 2 && args.Contains("--tray", StringComparer.Ordinal)
                && args.All(x => x is "--tray" or "--post-reboot");
            using var mainForm = new MainForm(startInTray: startInTray);
            Application.Run(mainForm);
            reopenAdministrator = mainForm.ConsumeV269AdminRestart();
            DiagnosticTrace.Write("APP.ENCERRAMENTO_NORMAL");
            return 0;
        }
        catch (Exception ex)
        {
            DiagnosticTrace.Failure("APP.FALHA_NAO_TRATADA", ex);
            AnonymousTelemetryContext.Current?.SaveOnExit(DiagnosticTrace.VersionLabel, normalExit: false);
            try { MessageBox.Show("Não foi possível iniciar o gerenciador.\n\n" + ex,
                    "Falha ao abrir", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }
            return 1;
        }
        finally
        {
            DiagnosticTrace.Stop();
            if (reopenAdministrator is { } ticket) V269AdminSessionLauncher.RequestOnce(ticket);
        }
    }
}

internal sealed class ProgramEntry
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string DisplayIcon { get; init; } = "";
    internal DateOnly? InstallDate { get; init; }
    // Texto consultivo da fonte local; nunca usado em receitas ou telemetria.
    internal string DeclaredDescription { get; init; } = "";
    internal string ObservedInstallLocation { get; init; } = "";
    internal string ObservedPackageArchitecture { get; init; } = "";
    public string UninstallString { get; init; } = "";
    public string QuietUninstallString { get; init; } = "";
    public string InstallLocation { get; init; } = "";
    public string PackageId { get; set; } = "";
    public string CatalogIdentity { get; set; } = "";
    public string CatalogDisplayName { get; set; } = "";
    public string InventorySource { get; init; } = "";
    public string InventoryId { get; init; } = "";
    // Marcador consultivo da entrada ARP oculta; nunca concede permissao de remocao.
    public bool HiddenFromNormalView { get; init; }
    // ARP: dependências explícitas do instalador; não são regras de desinstalação.
    public string BundleParentKeyName { get; init; } = "";
    public string BundleProviderKey { get; init; } = "";
    public bool BundleMetadataReliable { get; init; } = true;
    // Evidência consultiva capturada junto ao inventário; não autoriza execução.
    internal string QueryArchitecture { get; set; } = "unknown";
    public string MsiProductCode { get; init; } = "";
    public string MsixPackageFamilyName { get; init; } = "";
    public string MsixPackageFullName { get; init; } = "";
    public string MsixPublisherId { get; init; } = "";
    public string MsixPackageName { get; init; } = "";
    public bool? MsixSystemSigned { get; init; }
    // Somente inventário MSIX completo e atual permite remoção direta.
    public bool MsixInventoryComplete { get; init; }
    // A mesma elegibilidade rege aparencia e clique: nunca exibir acao
    // executavel que o hit-test encaminhe para o gerenciador manual.
    internal MsixRemovalRequest MsixRemovalIdentity =>
        new(MsixPackageFullName, MsixPackageFamilyName, MsixPackageName,
            MsixPublisherId, Version, Name, InventoryId, MsixSystemSigned);
    internal bool CanUninstallMsix =>
        InventoryOnly && !CanUninstall && MsixInventoryComplete &&
        InventorySource.Contains("MSIX", StringComparison.OrdinalIgnoreCase) &&
        MsixUninstall.CanOffer(MsixRemovalIdentity);
    // Contrato unico entre hit-test e desenho sem depender do controle visual.
    internal string UninstallAction =>
        CanUninstall ? "uninstall" :
        CanUninstallMsix ? "msix-uninstall" : "uninstall-manual";
    public bool VerifiedMsixIdentity { get; init; }
    // Entrada MSIX, jogo da biblioteca ou componente protegido: sem desinstalacao direta.
    private bool _inventoryOnly;
    public bool InventoryOnly { get => _inventoryOnly || ProtectedApplications.IsExcludedFromManagement(Name); init => _inventoryOnly = value; }
    // Atualizada pelo inventario local; nao fornece autorizacao para executar comandos.
    private bool _canUninstall;
    public bool CanUninstall { get => _canUninstall && !ProtectedApplications.IsExcludedFromManagement(Name); set => _canUninstall = value; }
    public string AvailableVersion { get; set; } = "";
    public string Source { get; set; } = "";
    private bool _isUpdate;
    public bool IsUpdate { get => _isUpdate && !ProtectedApplications.IsExcludedFromManagement(Name); set => _isUpdate = value; }
    public bool UpdateRequiresReview { get; set; }
    public string UpdateEvidence { get; set; } = "";
    internal ApplicationCapabilitySnapshot? Capabilities { get; set; }
    public MatchState ComparisonState { get; set; } = MatchState.Unmatched;
}

internal sealed record RegistryInventoryScan(IReadOnlyList<ProgramEntry> Items,
    int Inspected, int HiddenComponents, int UpdateEntries, int ReadFailures, int Duplicates)
{
    // Prévia de bundleware exige inclusive filhos SystemComponent ocultos.
    internal bool IncludesHiddenEntries { get; init; }
}
internal sealed record UnifiedInventoryScan(IReadOnlyList<ProgramEntry> Items,
    RegistryInventoryScan Registry, LocalMsixResult Msix, int Added,
    GameLibraryReport Games, int GamesAdded)
{
    internal bool Incomplete => Registry.ReadFailures > 0 || Msix.Failures > 0 || Msix.Status != "ok" || Games.Partial;
}
internal sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
internal sealed record DiskUsageInfo(string Root, string Label, long TotalBytes, long FreeBytes,
    string FileSystem = "");

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
                string fileSystem;
                try { fileSystem = disk.DriveFormat; }
                catch { fileSystem = ""; }
                items.Add(new DiskUsageInfo(disk.Name, label, total,
                    Math.Clamp(free, 0L, total), fileSystem));
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
            FileName = WindowsSystemTools.Resolve(fileName),
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

    // Consumido pelos fluxos que confirmam instalacao/ausencia antes de limpar.
    // Uma leitura parcial nao pode virar evidencia de desinstalacao.
    public static List<ProgramEntry> GetInstalledPrograms()
    {
        var scan = ScanInstalledPrograms(includeHidden: true);
        return InventoryReconciler.RequireComplete(scan.Items, scan.ReadFailures);
    }

    internal static UnifiedInventoryScan ScanAllInstalledApps() => ScanAllInstalledApps(includeHidden: false);

    // A lista visual pode omitir componentes ocultos. Uma verificacao de
    // presenca para executar/limpar nunca pode interpretar essa omissao como ausencia.
    internal static UnifiedInventoryScan ScanAllInstalledAppsForSafety() => ScanAllInstalledApps(includeHidden: true);

    private static UnifiedInventoryScan ScanAllInstalledApps(bool includeHidden)
    {
        using var measurement = LocalPerformanceMetrics.Measure(PerformanceStage.Inventory);
        var registry = ScanInstalledPrograms(includeHidden);
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
                InventorySource = app.Stale ? "MSIX — leitura anterior, presença não confirmada" : "Aplicativo empacotado (MSIX)",
                InventoryId = LocalMsixInventory.InventoryId(app),
                VerifiedMsixIdentity = app.RecipeEligible,
                MsixPackageFamilyName = app.PackageFamilyName,
                MsixPackageFullName = app.PackageFullName,
                MsixPublisherId = app.PublisherId,
                MsixPackageName = app.PackageName,
                MsixSystemSigned = app.SystemSigned,
                MsixInventoryComplete = msix.Status == "ok" && msix.Failures == 0 && !app.Stale,
                InstallDate=app.Stale?null:app.DeclaredDate,
                DeclaredDescription=app.Stale?"":app.DeclaredDescription,
                ObservedInstallLocation=app.Stale?"":app.ObservedLocation,
                ObservedPackageArchitecture=app.Stale?"":app.ObservedArchitecture,
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
        var managed = EcosystemInventory.ReadLocal();
        EcosystemInventory.AppendManaged(result, managed);
        foreach (var source in managed)
            DiagnosticTrace.Write($"ECOSSISTEMA.INVENTARIO fonte={source.Provider} estado={source.Status} entradas={source.Installations.Count}");
        return new UnifiedInventoryScan(result, registry, msix, additions.Count,
            games, gameAdditions.Count);
    }

    // ARP x64/x86 para máquina e usuário atual. Usa nomes reais do Windows,
    // INDEPENDENTE do catálogo de atualizações. Nunca executa uninstallers.
    internal static RegistryInventoryScan ScanInstalledPrograms() => ScanInstalledPrograms(includeHidden: false);

    // Analisa entradas ARP explicitamente ocultas, apenas para revisao visual.
    // Historico de updates, entradas de nome invalido e componentes protegidos
    // nao se tornam elegiveis a desinstalacao por aparecerem nesta lista.
    internal static RegistryInventoryScan ScanHiddenWindowsProgramsForReview()
    {
        var scan = ScanInstalledPrograms(includeHidden: true);
        return scan with { Items = scan.Items.Where(item => item.HiddenFromNormalView).ToArray() };
    }

    private static RegistryInventoryScan ScanInstalledPrograms(bool includeHidden)
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
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        bool longName = name.Length > 240;
                        if (longName && !includeHidden) continue;
                        if (longName) name = name[..240];
                        string release = Convert.ToString(child.GetValue("ReleaseType")) ?? "";
                        bool updateRecord = release.Equals("Update", StringComparison.OrdinalIgnoreCase) ||
                            release.Equals("Security Update", StringComparison.OrdinalIgnoreCase) ||
                            release.Equals("Hotfix", StringComparison.OrdinalIgnoreCase) ||
                            release.Equals("Update Rollup", StringComparison.OrdinalIgnoreCase);
                        if (updateRecord) { updates++; if (!includeHidden) continue; }
                        bool systemComponent = Convert.ToInt32(child.GetValue("SystemComponent") ?? 0) == 1;
                        if (systemComponent && !ProtectedApplications.IsProtectedName(name))
                        { hidden++; if (!includeHidden) continue; }
                        bool protectedApp = longName || updateRecord || ProtectedApplications.IsInventoryOnly(name, systemComponent);
                        string publisher = Convert.ToString(child.GetValue("Publisher"))?.Trim() ?? "";
                        // ParentKeyName identifica componentes de um bundle. Um
                        // valor inseguro jamais deve permitir desinstalação isolada.
                        object? rawParent = child.GetValue("ParentKeyName");
                        object? rawBundle = child.GetValue("BundleProviderKey");
                        static bool SafeBundleValue(object? raw) => raw is null ||
                            raw is string { Length: <= 180 } value &&
                            value == value.Trim() && !value.Any(char.IsControl);
                        string parent = rawParent as string ?? "";
                        string bundle = rawBundle as string ?? "";
                        bool bundleMetadataReliable = SafeBundleValue(rawParent) &&
                            SafeBundleValue(rawBundle) &&
                            !parent.Contains('\\') && !parent.Contains('/');
                        UnifiedAppRule? known = ApplicationIdentityCatalog.Resolve(name, publisher);
                        string scope = hive == RegistryHive.CurrentUser ? "Usuário atual" : "Computador";
                        string architecture = view == RegistryView.Registry32 ? "32 bits" : "64 bits";
                        // O identificador inclui a visão real: não unir instalações
                        // diferentes só porque o nome e versão coincidem.
                        string registryIdentity = $"{hive}|{view}|{registryKey}";
                        var entry = new ProgramEntry
                        {
                            Name = name,
                            Version = Convert.ToString(child.GetValue("DisplayVersion"))?.Trim() ?? "",
                            Publisher = publisher,
                            CatalogIdentity = known?.Id ?? "",
                            CatalogDisplayName = known?.Name ?? "",
                            InventorySource = $"Registro: {scope} ({architecture})",
                            InventoryId = registryIdentity,
                            HiddenFromNormalView = systemComponent && !updateRecord && !longName &&
                                !ProtectedApplications.IsProtectedName(name),
                            BundleParentKeyName = bundleMetadataReliable ? parent : "",
                            BundleProviderKey = bundleMetadataReliable ? bundle : "",
                            BundleMetadataReliable = bundleMetadataReliable,
                            MsiProductCode = InventoryReconciler.ProductCodeFromRegistryKey(registryKey) ?? "",
                            UninstallString = protectedApp && !includeHidden ? "" :
                                Convert.ToString(child.GetValue("UninstallString")) ?? "",
                            DisplayIcon = Convert.ToString(child.GetValue("DisplayIcon")) ?? "",
                            DeclaredDescription = ReadArpDescription(child),
                            InstallDate = V269AppOrdering.ParseRegistryDate(child.GetValue("InstallDate", null, RegistryValueOptions.DoNotExpandEnvironmentNames)),
                            QuietUninstallString = protectedApp && !includeHidden ? "" :
                                Convert.ToString(child.GetValue("QuietUninstallString")) ?? "",
                            InstallLocation = protectedApp && !includeHidden ? "" :
                                Convert.ToString(child.GetValue("InstallLocation"))?.Trim() ?? "",
                            // A remoção de um filho pode quebrar o pacote pai.
                            // Inventário e revisão continuam, ação isolada fica bloqueada.
                            InventoryOnly = protectedApp || !bundleMetadataReliable || parent.Length != 0
                        };
                        entry.QueryArchitecture = InstalledUpdateIdentity.CaptureArchitecture(entry);
                        entries.Add(entry);
                    }
                    catch (Exception) { failures++; }
                }
            }
            catch (Exception) { failures++; }
        }
        var distinct = InventoryReconciler.DistinctRegistryEntries(entries);
        return new RegistryInventoryScan(distinct.OrderBy(x => x.Name,
            StringComparer.CurrentCultureIgnoreCase).ToArray(),
            inspected, hidden, updates, failures, entries.Count - distinct.Count)
        { IncludesHiddenEntries = includeHidden };
    }

    private static string ReadArpDescription(RegistryKey key)
    {
        try
        {
            return LocalMsixInventory.NormalizeDescription(
                key.GetValue("Comments",null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string);
        }
        catch(Exception){return "";} // metadado opcional não altera cobertura
    }

    public static bool TryParseUninstaller(string? command, out string fileName, out string arguments)
    {
        fileName = ""; arguments = "";
        if (!UninstallCommand.TryParse(command, out var parsed)) return false;
        fileName = parsed.FileName; arguments = parsed.Arguments;
        return true;
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
        // Diminuir a fonte quando necessario, sem cortar palavras.
        // A segunda linha so e permitida como ultimo recurso em janelas antigas.
        Font? fitted = null;
        Font drawFont = Font;
        try
        {
            int available = Math.Max(8, content.Width - 3);
            while (!AllowMultilineText && drawFont.SizeInPoints > 7.0F &&
                TextRenderer.MeasureText(e.Graphics, Text, drawFont,
                    new Size(int.MaxValue, content.Height),
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width > available)
            {
                float nextSize = Math.Max(7.0F, drawFont.SizeInPoints - 0.4F);
                fitted?.Dispose();
                fitted = new Font(Font.FontFamily, nextSize, Font.Style);
                drawFont = fitted;
            }
            var captionFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                (AllowMultilineText ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
            TextRenderer.DrawText(e.Graphics, Text, drawFont, content,
                Enabled ? ForeColor : Color.FromArgb(164, 176, 190), captionFlags);
        }
        finally { fitted?.Dispose(); }
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
        Form? owner = FindForm();
        while (owner is not null && owner is not MainForm) owner = owner.Owner;
        if (owner is MainForm main && !main.V269CanAnimateButtons)
        { base.OnClick(e); return; }
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
    private readonly ToolTip _toolTips = new() { Active = false };
    private readonly DarkProgramList _programList = new();
    private readonly LocalReliabilityHistory _reliability = new();
    private readonly UpdateOperationStore _updateOperations;
    private readonly UninstallOperationStore _uninstallOperations;
    private readonly Label _emptyState = new();
    private readonly V269AppSearchEditor _search = new();
    private readonly DarkLogView _log = new();
#if !GF_BETA
    private Button? _privacyStatusButton;
#endif
    private readonly Label _status = new();
    private readonly Label _listTitle = new();
    private readonly Label _itemHint = new();
    private readonly ProgressBar _progress = new();
    private readonly V269Shell _nav = new();
    private readonly List<ProgramEntry> _installed = new();
    private readonly List<ProgramEntry> _available = new();
    private readonly List<Control> _actionControls = new();
    private readonly DarkScrollableFlow _diskCards = new();
    private readonly Label _diskSummary = new();
    private Button? _refreshDisksButton;
    private bool _refreshingDisks;
    private bool _busy;
    private readonly BetaCloseCoordinator _betaClose = new();
    private Button? _repairWindowsButton;
    private bool _windowsRepairable;
    private DateTimeOffset _windowsScannedAt;
    private bool _multiSelectMode;
    private bool _catalogoVerificado; // Resultado local do RuckZuck (sem instalacao)
    private bool _inventoryIncomplete;
    private CancellationTokenSource? _consultaCatalogoCts;
    private CancellationTokenSource? _installerDownloadCts;
    private int _catalogoGeneration; // Descarta resultados de consultas anteriores ao recarregamento local.
    private string _appFilter = "Todos";
    private readonly HashSet<ProgramEntry> _checkedApps = new();
    private Button? _checkUpdatesButton;
    private Button? _updateAllButton;
    private bool _batchUpdatesActive;
    private bool _batchStopRequested;
    private UpdateOperationSnapshot? _batchLastAttempt;
    private Button? _bulkUninstallButton;
    private Button? _selectManyButton;
    private Button? _clearSelectionButton;
    private Button? _allFilterButton;
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
            if (_diagnoseWindowLifecycle)
                DiagnosticTrace.Write("JANELA.HANDLE_CRIADO");
            int dark = 1;
            if (NativeMethods.DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
                NativeMethods.DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private readonly Action? _startupOnlineProbe;

    public MainForm(bool startServices = true, bool startInTray = false, Action? startupOnlineProbe = null)
    {
        _startupOnlineProbe = startupOnlineProbe;
        _updateOperations = new(DiagnosticTrace.Write, _reliability.RecordUpdate);
        _uninstallOperations = new(_reliability.RecordUninstall);
        FormClosed += (_, _) => _reliability.Dispose();
        Text = "Gerenciador Final — " + DiagnosticTrace.VersionLabel +
            (BetaRuntimePolicy.IsLocalBeta ? " BETA LOCAL TESTE — NÃO HOMOLOGADA" : " BETA");
        Font = new Font("Segoe UI", 9.5F);
        BackColor = AppServices.Background;
        ForeColor = Ink;
        // Buffering somente na janela principal: sem compositar toda a
        // hierarquia nativa de TextBox/FlowLayout (custo no PC FRACO).
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint, true);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        // Tamanho fixo por monitor; a escala já existente em Configurações
        // ajusta o tamanho sem adicionar controles nem maximização.
        if(startServices)_v269Appearance=ReadV269Appearance();
        var startupScreen=Screen.PrimaryScreen;
        var display=startupScreen?.WorkingArea??SystemInformation.WorkingArea;
        Rectangle opening=StartupWindowSizing.Fit(display,startupScreen?.Bounds.Height??display.Height,
            _v269Appearance.InterfacePercent);
        StartPosition = FormStartPosition.Manual;
        _diagnoseWindowLifecycle = startServices;
        InitializeWindowChrome();
        // Evita exibir a janela no arranque automatico antes do primeiro Hide().
        if (startInTray) Opacity = 0;
        MinimumSize = StartupWindowSizing.AllowedMinimum(opening);
        MaximumSize = opening.Size;
        Bounds = opening;

        var root = new TableLayoutPanel { Dock = DockStyle.None, ColumnCount = 1, RowCount = 2, Padding = Padding.Empty, BackColor = AppServices.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);
        // A superficie interna acompanha toda a janela calculada. O limite
        // antigo de 875x745 encolhia TAMBEM o conteudo mesmo em 2560x1440,
        // causando area vazia ou controles minimos em monitores grandes.
        // A tipografia continua no DPI do sistema; nao escala com resolucao.
        void CenterWorkspace()
        {
            if (root.IsDisposed) return;
            int caption = _nav.SelectedPage is V269Page.Inicio or V269Page.Configuracoes or V269Page.Aplicativos or V269Page.Drivers ? 0 : _caption?.Height ?? 0;
            var bounds = new Rectangle(
                0, caption, Math.Max(1, ClientSize.Width),
                Math.Max(1, ClientSize.Height - caption));
            if (root.Bounds != bounds) root.Bounds = bounds;
        }
        Resize += (_, _) => CenterWorkspace();
        DpiChanged += (_, _) => CenterWorkspace();
        CenterWorkspace();
        var brandHeader = (V269BrandHeader)BuildHeader();
        root.Controls.Add(brandHeader, 0, 0);
        bool homeChrome = true;
        if (_caption is not null) _caption.Visible = false;
        void ApplyHomeChrome()
        {
            bool home = _nav.SelectedPage is V269Page.Inicio or V269Page.Configuracoes or V269Page.Aplicativos or V269Page.Drivers;
            if (homeChrome != home)
            {
                // Nos testes de viewport a superfície interna tem largura
                // independente do Form. Não desfazer o tamanho simulado.
                bool followsWindow = root.Width == ClientSize.Width &&
                    root.Height == Math.Max(1, ClientSize.Height -
                        (homeChrome ? 0 : _caption?.Height ?? 0));
                homeChrome = home;
                if (_caption is not null) _caption.Visible = !home;
                root.Padding = home ? Padding.Empty : new Padding(8, 4, 8, 4);
                if (followsWindow) CenterWorkspace();
                else root.Top = home ? 0 : _caption?.Height ?? 0;
            }
            FitWorkspace();
            ApplyV269ContentAppearance(brandHeader);
        }

        _nav.SetPageContent(V269Page.Inicio, BuildV269Home());
        _nav.SetPageContent(V269Page.Aplicativos, BuildProgramsPanel(startServices));
        _nav.SetPageContent(V269Page.Drivers, BuildDriversPlaceholder());
        AddToolsPages();
        _nav.SetPageContent(V269Page.Inicializacao, BuildV269StartupPage());
        _nav.SetPageContent(V269Page.Configuracoes, BuildV269Settings(startServices));
        _nav.SetOperationContent(BuildOperationsPanel());
        _nav.RightPane.PresentationChanged+=(_,_)=>ApplyV269AppOperationChrome();
        _nav.RightPane.ConfigureAppHistory(()=>_v269AppPreviousResults.ToArray(),DismissV269AppResult);
        _nav.RightPane.ConfigureCleanupHistory(()=>_v269CleanupPreviousResults.ToArray(),DismissV269CleanupResult);
        _nav.NavigationChanged += async (_, e) =>
        {
            ApplyHomeChrome();
            ApplyV269AppOperationChrome();
            if(e.Page==V269Page.Aplicativos&&_programList.SelectedItem is {} app)PresentV269AppDetails(app);
            if(e.Page==V269Page.Drivers) _presentV269DriverDetails?.Invoke(false);
            if(e.Page==V269Page.Limpeza) PresentV269CleanupRetained();
            ++_systemViewGeneration;
            UpdateProcessSamplingCadence();
            if (e.Page != V269Page.Inicio) _v269HardwareQuery?.Cancel();
            if (!startServices || _v269Confirming) return;
            if (e.Page == V269Page.Limpeza) await RefreshDiskUsageAsync();
            if (e.Page == V269Page.Inicializacao) await RefreshStartupEntriesAsync();
            if (e.Page == V269Page.Sistema && _activeSystemView == "memory")
                await RefreshMemoryRamAsync();
            if (e.Page == V269Page.Sistema && _activeSystemView == "update")
                DrawWindowsUpdates();
            if (e.Page == V269Page.Inicio && !_v269HardwareLoaded)
                await RefreshV269HardwareAsync();
        };
        root.Controls.Add(_nav, 0, 1);
        ApplyV269ContentAppearance(root);
        if (startServices) StartV269LiveSampling();
        void FitWorkspace()
        {
            var rows = StartupWindowSizing.WorkspaceRows(
                root.ClientSize.Height - root.Padding.Vertical, DeviceDpi);
            bool home = _nav.SelectedPage is V269Page.Inicio or V269Page.Configuracoes or V269Page.Aplicativos or V269Page.Drivers;
            int logicalWidth = Math.Max(1, (int)Math.Round(root.ClientSize.Width * 96d / DeviceDpi));
            int headerHeight = home ? Math.Max(1, (int)Math.Round(
                V269HomeMetrics.HeaderHeight(logicalWidth, V269HomeMetrics.LogicalScreenHeight(this))
                    * DeviceDpi / 96d)) : rows.Header;
            if (Math.Abs(root.RowStyles[0].Height - headerHeight) > (home ? 0 : 1))
                root.RowStyles[0].Height = headerHeight;
            brandHeader.Configure(home, logicalWidth, _v269Appearance);
        }
        bool adjustingWindow=false;
        void RestoreWithinMonitor(bool recomputeForDisplay = false)
        {
            if(IsDisposed||Disposing||adjustingWindow||WindowState!=FormWindowState.Normal)return;
            adjustingWindow=true;
            try
            {
                var screen=Screen.FromControl(this);
                var area=screen.WorkingArea;
                var fitting=StartupWindowSizing.Fit(area,screen.Bounds.Height,_v269Appearance.InterfacePercent);
                var visible=recomputeForDisplay?fitting:StartupWindowSizing.KeepVisible(Bounds,area,
                    screen.Bounds.Height,_v269Appearance.InterfacePercent);
                if(MinimumSize!=fitting.Size||MaximumSize!=fitting.Size)
                {
                    MinimumSize=Size.Empty;MaximumSize=Size.Empty;
                    Bounds=visible;
                    MinimumSize=MaximumSize=fitting.Size;
                }
                else if(Bounds!=visible)Bounds=visible;
            }
            finally{adjustingWindow=false;}
        }
        _v269ResizeFromSettings=()=>{RestoreWithinMonitor(recomputeForDisplay:true);FitWorkspace();};
        root.SizeChanged += (_, _) => FitWorkspace();
        DpiChanged += (_, _) => { RestoreWithinMonitor(recomputeForDisplay: true); FitWorkspace(); };
        ResizeEnd += (_, _) => RestoreWithinMonitor();
        string currentMonitor=Screen.FromControl(this).DeviceName;
        LocationChanged+=(_,_)=>{
            if(IsDisposed||WindowState!=FormWindowState.Normal)return;
            string nextMonitor=Screen.FromControl(this).DeviceName;
            if(nextMonitor==currentMonitor)return;
            currentMonitor=nextMonitor;
            RestoreWithinMonitor(recomputeForDisplay:true);
        };
        // Alteracoes de resolucao podem enviar varios eventos consecutivos.
        // Consolidar para nao redesenhar a arvore da janela repetidamente.
        var displayUpdate = new System.Windows.Forms.Timer { Interval = 170 };
        displayUpdate.Tick += (_, _) =>
        {
            displayUpdate.Stop();
            if (IsDisposed) return;
            RestoreWithinMonitor(recomputeForDisplay: true);
            FitWorkspace();
        };
        EventHandler monitorChanged = (_, _) =>
        {
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed) return;
                    displayUpdate.Stop();
                    displayUpdate.Start();
                }));
            }
            catch (InvalidOperationException) { /* Janela ja encerrada. */ }
        };
        SystemEvents.DisplaySettingsChanged += monitorChanged;
        FormClosed += (_, _) =>
        {
            SystemEvents.DisplaySettingsChanged -= monitorChanged;
            displayUpdate.Stop();
            displayUpdate.Dispose();
            _v269HardwareQuery?.Cancel();
            _nav.RightPane.ResolveConfirmation(false);
            _log.FlushPending();
            AnonymousTelemetryContext.Current?.CancelRemoteSend();
            // Windows Shutdown nao inicia rede. Em fechamento normal remoto o
            // snapshot ja foi salvo antes da tentativa, sem duplica-lo aqui.
            if (_betaClose.IsShutdown || !_betaClose.IsClosing || AnonymousTelemetryContext.Current?.Consent != TelemetryConsent.RemoteBeta)
                AnonymousTelemetryContext.Current?.SaveOnExit(DiagnosticTrace.VersionLabel, normalExit: !_betaClose.IsShutdown);
        };
        FitWorkspace();
        // Abre imediatamente. Primeiro carrega a lista local e depois inicia consultas independentes,
        // em segundo plano, sem janelas auxiliares e sem executar instaladores.
        Shown += async (_, _) =>
        {
            if (startInTray)
            {
                MinimizeToTray();
                Opacity = 1;
                DiagnosticTrace.Write("INICIALIZACAO.BANDEJA_AUTO estado=oculto");
            }
            else
            {
                RestoreWithinMonitor();
                FitWorkspace();
            }
            if (!startServices) return;
            LocalPerformanceMetrics.CompleteStartup();
            Log(BetaRuntimePolicy.IsLocalBeta ? "BETA LOCAL: coleta e envio remotos desativados." :
                "BETA: somente contagens técnicas são elegíveis a envio com consentimento.");
            // Nao abrir dialogos na sessao que iniciou oculta na bandeja.
            if (!startInTray) OfferAnonymousDiagnosticConsent(firstRun: true);
            if (!startInTray && _nav.SelectedPage == V269Page.Inicio) _ = RefreshV269HardwareAsync();
            // Na abertura, a lista ainda esta vazia: nao animar barra de
            // progresso nem alternar Enabled/Visible de dezenas de controles.
            // P5: Startup termina ao abrir a janela; a primeira lista ainda
            // depende de Registro/MSIX/disco e renderizacao. Medir a tentativa
            // de carga separadamente, sem incluir o dialogo de consentimento.
            using (LocalPerformanceMetrics.Measure(PerformanceStage.InitialInventoryLoad))
                await ExecuteAsync("Carregando programas instalados...", LoadInstalledAsync,
                    quietStartup: true);
            if (!IsDisposed)
                BeginStartupChecks();
            try
            {
                var rebootReports = new List<string>();
                await RebootResidualReview.CheckAfterBootAsync(async () =>
                {
                    var scan = await Task.Run(AppServices.ScanAllInstalledAppsForSafety);
                    return new UninstallSnapshot(scan.Items, !scan.Incomplete);
                }, DiagnosticTrace.Write, message => { Log(message); Status(message); rebootReports.Add(message); });
                if (rebootReports.Count > 0) ShowUninstallResult(string.Join("\n\n", rebootReports));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                InvalidDataException or System.Security.Cryptography.CryptographicException or
                System.Text.Json.JsonException)
            {
                DiagnosticTrace.Write("REINICIO.RESIDUOS estado=review_unavailable motivo=" + ex.GetType().Name);
                Log("Não foi possível concluir a conferência de resíduos após o reinício.");
            }
        };
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.WindowsShutDown)
            {
                _betaClose.Shutdown();
                AnonymousTelemetryContext.Current?.CancelRemoteSend();
                _consultaCatalogoCts?.Cancel();
                _deploymentWaitCts?.Cancel();
                _uninstallCts?.Cancel();
                _installerDownloadCts?.Cancel();
                _cleanupCancellation?.Cancel();
                _memoryTimer.Stop();
                return; // nao cancelar o desligamento nem abrir dialogo
            }
            if (_betaClose.IsClosing) return;
            if (!_busy)
            {
                _consultaCatalogoCts?.Cancel();
                _memoryTimer.Stop();
                return;
            }
            if (_consultaCatalogoCts is not null)
            {
                e.Cancel = true;
                _consultaCatalogoCts.Cancel();
                Status("Cancelando a consulta. Tente fechar novamente em alguns instantes.");
                return;
            }
            if (_deploymentWaitCts is not null)
            {
                e.Cancel = true;
                _deploymentWaitCts.Cancel();
                Status("Parando espera do pacote. O Windows pode continuar; a fonte será preservada. Não repetir.");
                return;
            }
            if (_uninstallCts is not null)
            {
                e.Cancel = true;
                _uninstallCts.Cancel();
                Status("Cancelando espera e demais itens. O desinstalador do fabricante pode continuar; não repetir.");
                return;
            }
            if (_cleanupCancellation is not null)
            {
                e.Cancel = true;
                RequestCleanupCancellation();
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
#if GF_BETA
        FormClosing += async (_, e) =>
        {
            if (_betaClose.IsShutdown || _betaClose.IsComplete) return;
            if (_betaClose.IsWaiting) { e.Cancel = true; return; }
            var recorder = AnonymousTelemetryContext.Current;
            if (!_betaClose.TryBegin(e.Cancel,
                recorder?.Consent == TelemetryConsent.RemoteBeta && BetaTelemetryUploader.IsConfigured)) return;
            e.Cancel = true;
            // Bloquear novos trabalhos, mantendo o menu Privacidade acessivel.
            _programList.ActionsEnabled = false;
            recorder!.SaveOnNormalExit(DiagnosticTrace.VersionLabel);
            try { await BetaTelemetryUploader.TrySendAsync(recorder); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or
                System.Security.SecurityException) { /* Encerrar preservando a pendencia. */ }
            finally
            {
                if (_betaClose.Finish() && !IsDisposed && !Disposing && IsHandleCreated)
                {
                    try { BeginInvoke((Action)(() =>
                    {
                        if (_betaClose.IsComplete && !_betaClose.IsShutdown && !IsDisposed && !Disposing) Close();
                    })); }
                    catch (InvalidOperationException) { /* Handle destruido durante Shutdown. */ }
                }
                else if (!_betaClose.IsShutdown) ResumeAfterCancelledShutdown();
            }
        };
#endif
        Log("Gerenciador Final iniciado. Operações e andamento ficam integrados à janela principal.");
        Log("DIAGNÓSTICO DE TESTE: arquivo confirmado em " + DiagnosticTrace.LogFilePath);
    }

    private Control BuildHeader()
    {
        var panel = new V269BrandHeader { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Margin = Padding.Empty };
        panel.Name = "V269BrandHeader";
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        var mark = new V269BrandMark(() => _v269Appearance.Theme)
        {
            Name = "V269BrandMark", Dock = DockStyle.Fill, Margin = new Padding(11, 0, 0, 0),
            AccessibleName = "GF", AccessibleRole = AccessibleRole.Graphic
        };
        panel.Controls.Add(mark, 0, 0);
        panel.SetRowSpan(mark, 2);
        panel.BackColorChanged += (_, _) => mark.Invalidate();
        var brandTitle = new Label
        {
            Name = "V269BrandTitle", Text = "GERENCIADOR FINAL", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 15.5F, FontStyle.Bold), ForeColor = Ink,
            Margin = new Padding(13, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(brandTitle, 1, 0);
        var brandSubtitle = new Label
        {
            Name = "V269BrandSubtitle", Text = "Manutenção e otimização do Windows · V2.6.9",
            Dock = DockStyle.Fill, Margin = new Padding(13, 0, 0, 0),
            ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        panel.Controls.Add(brandSubtitle, 1, 1);
        var chrome = new FlowLayoutPanel { Name = "V269HomeChrome", Dock = DockStyle.Fill,
            WrapContents = false, FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 12, 18, 0), Margin = Padding.Empty,
            Visible = false, BackColor = Color.Transparent };
        var minimize = CaptionButton("−", "Minimizar conforme a preferência da janela", false,
            (_, _) => MinimizeByV269Preference());
        minimize.Name = "V269HomeMinimize";
        var close = CaptionButton("×", "Fechar o Gerenciador Final", true,
            (_, _) => Close());
        close.Name = "V269HomeClose";
        foreach (var button in new[] { minimize, close })
        {
            button.Size = new Size(34, 28);
            button.Margin = new Padding(0, 0, 5, 0);
            chrome.Controls.Add(button);
        }
        panel.Controls.Add(chrome, 3, 0);
        panel.SetRowSpan(chrome, 2);
        panel.InitializeBrand(mark, brandTitle, brandSubtitle, chrome);
        panel.MouseDown += MoveFromCaption;
        mark.MouseDown += MoveFromCaption;
        brandTitle.MouseDown += MoveFromCaption;
        brandSubtitle.MouseDown += MoveFromCaption;
#if !GF_BETA
        _privacyStatusButton = CreateSecondaryButton("Configurar privacidade");
        _privacyStatusButton.Name = "PrivacyStatusButton";
        _privacyStatusButton.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _privacyStatusButton.Dock = DockStyle.Fill;
        _privacyStatusButton.Margin = new Padding(5, 9, 1, 9);
        _privacyStatusButton.Click += (_, _) => OfferAnonymousDiagnosticConsent();
        panel.Controls.Add(_privacyStatusButton, 2, 0);
        panel.SetRowSpan(_privacyStatusButton, 2);
        RefreshPrivacyStatus();
#endif
        return panel;
    }

    private V269AppSort _v269AppSort=V269AppSort.NameAscending;
    private string[] _v269ManualOrder=[];
    private bool _v269OrderLive;
    private readonly V269AppSortCombo _v269AppSortChoice=new() { Name="V269AppSort", DropDownStyle=ComboBoxStyle.DropDownList };
    private Font? _v269AppSortFont;

    private Control BuildProgramsPanel(bool loadPreferences)
    {
        _v269OrderLive=loadPreferences;
        if(loadPreferences)
        {
            var saved=V269AppOrderStore.ReadPreferences(V269AppOrderStore.PathForUser);
            _v269ManualOrder=saved.Keys;_v269AppSort=saved.Sort;
        }
        var root = new TableLayoutPanel
        {
            Name="V269AppsPage",Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,
            Padding=Padding.Empty,Margin=Padding.Empty,BackColor=AppServices.Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var searchBar=new TableLayoutPanel{Name="V269AppsToolbar",Dock=DockStyle.Fill,
            ColumnCount=4,RowCount=1,Margin=Padding.Empty,BackColor=AppServices.Background};
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,78));
        _search.Name="V269AppsSearch";
        _search.BorderStyle=BorderStyle.FixedSingle;_search.BackColor=Color.FromArgb(19,26,34);
        _search.ForeColor=Ink;_search.PlaceholderText="Pesquisar aplicativo...";
        _search.AccessibleName="Pesquisar aplicativo por nome, versão ou editor";
        _search.Margin=new Padding(4,5,4,5);_search.Dock=DockStyle.Fill;
        _search.TextChanged+=(_,_)=>RenderPrograms();
        var searchFrame=new V269AppSearchBox(_search);
        searchBar.Controls.Add(searchFrame,1,0);
        _v269AppSortChoice.AccessibleName="Ordenar aplicativos";
        _toolTips.SetToolTip(_v269AppSortChoice,"Instalados recentemente usa a data declarada pela fonte: instalação, atualização ou manutenção. Datas desconhecidas ficam no fim.");
        _v269AppSortChoice.Items.AddRange(["Nome (A–Z)","Nome (Z–A)","Instalados recentemente","Atualizações primeiro","Ordem manual"]);
        _v269AppSortChoice.SelectedIndex=(int)_v269AppSort;_v269AppSortChoice.Dock=DockStyle.Fill;
        _v269AppSortChoice.SelectedIndexChanged+=(_,_)=>{
            if(_v269AppSortChoice.SelectedIndex<0)return;
            var next=(V269AppSort)_v269AppSortChoice.SelectedIndex;
            if(next==_v269AppSort)return;
            _v269AppSort=next;RenderPrograms();
            if(!_v269OrderLive)return;
            try{V269AppOrderStore.WritePreferences(V269AppOrderStore.PathForUser,next,_v269ManualOrder);}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {Status("Ordenação aplicada nesta sessão; não foi possível salvar a preferência.");}
        };
        _v269AppSortChoice.Margin=new Padding(0,1,9,1);
        _v269AppSortChoice.Disposed+=(_,_)=>_v269AppSortFont?.Dispose();
        searchBar.Controls.Add(_v269AppSortChoice,2,0);
        var details=new V269AppToolbarButton("Detalhes"){Name="V269AppsDetails"};details.Dock=DockStyle.Fill;
        details.Margin=new Padding(3,3,0,3);
        details.Click+=(_,_)=>{
            if(_programList.SelectedItem is {} app)PresentV269AppDetails(app);
            _nav.ShowV269AppPane();
        };
        searchBar.Controls.Add(details,3,0);
        _checkUpdatesButton=new V269AppToolbarButton("Verificar",true);
        _checkUpdatesButton.Name="V269AppsVerify";_checkUpdatesButton.AccessibleName="Verificar inventário e atualizações";
        _checkUpdatesButton.Dock=DockStyle.Fill;_checkUpdatesButton.Margin=new Padding(0,3,9,3);
        _checkUpdatesButton.Click+=(_,_)=>_ = CheckUpdatesFromButtonAsync();
        _toolTips.SetToolTip(_checkUpdatesButton,"Renova inventário e consulta versões por HTTPS; não instala nada. Durante consulta, clique novamente para cancelar.");
        searchBar.Controls.Add(_checkUpdatesButton,0,0);
        root.Controls.Add(searchBar,0,0);

        // HTML aprovado: somente barra superior e tabela. Nenhum comando
        // complementar, rodapé, filtro extra ou atalho alternativo.
        void FitToolbar()
        {
            int S(int px)=>V269AppTable.Scale(px,_v269Appearance.InterfacePercent,root.DeviceDpi);
            _programList.SetV269WindowWidth(_nav.LayoutWindowWidth);
            bool compact=root.ClientSize.Width<S(430);
            bool showDetails=_nav.LayoutWindowWidth<=1040;
            details.Visible=showDetails;
            searchBar.SuspendLayout();
            try
            {
                searchBar.RowStyles.Clear();searchBar.RowCount=compact?2:1;
                for(int i=0;i<searchBar.RowCount;i++)searchBar.RowStyles.Add(new RowStyle(SizeType.Percent,100f/searchBar.RowCount));
                searchBar.ColumnStyles[0].Width=S(115);
                int sortWidth=Math.Clamp(root.ClientSize.Width-S(showDetails?355:270),S(120),S(207));
                searchBar.ColumnStyles[2].Width=compact?0:sortWidth;
                searchBar.ColumnStyles[3].Width=compact||!showDetails?0:S(82);
                searchBar.SetColumnSpan(searchFrame,1);searchBar.SetColumnSpan(_v269AppSortChoice,1);
                searchBar.SetCellPosition(_checkUpdatesButton,new TableLayoutPanelCellPosition(0,0));
                searchBar.SetCellPosition(searchFrame,new TableLayoutPanelCellPosition(1,0));
                searchBar.SetCellPosition(_v269AppSortChoice,new TableLayoutPanelCellPosition(compact?1:2,compact?1:0));
                searchBar.SetCellPosition(details,new TableLayoutPanelCellPosition(compact?0:3,compact?1:0));
                root.RowStyles[0].Height=S(compact?84:44);
            }
            finally{searchBar.ResumeLayout(true);}
        }
 
        void FitAppLayout()=>FitToolbar();
        root.SizeChanged+=(_,_)=>FitAppLayout();root.DpiChangedAfterParent+=(_,_)=>FitAppLayout();
        root.Tag=(Action)FitAppLayout;

        _programList.Dock = DockStyle.Fill;
        _programList.ApplyV269Table(_v269Appearance);
        _programList.LoadLocalAppIcons=loadPreferences;
        _programList.BackColor = V269AppTable.EmptySurface(_v269Appearance.Theme);
        _programList.ForeColor = Ink;
        _programList.OperationFor = _updateOperations.Find;
        _programList.UninstallOperationFor = _uninstallOperations.Find;
        _programList.DrawItem += DrawProgramCard;
        _programList.ReorderRequested+=MoveV269App;
        _toolTips.SetToolTip(_programList,
            "Atualizar usa um instalador oficial verificado quando há integração validada. " +
            "Site abre o fabricante somente quando solicitado. Desinstalar exige confirmação.");
        _programList.ItemActionClicked += (item, action) =>
        {
            if (_busy || _batchUpdatesActive || _v269Confirming || _betaClose.IsClosing || !_installed.Contains(item)) return;
            if (action == "uninstall" && item.CanUninstall)
                _ = ExecuteAsync("Desinstalando " + item.Name + "...", () => UninstallEntriesAsync(new[] { item }));
            else if (action == "uninstall-manual" && !item.CanUninstall)
                OpenManualAppUninstall(item);
            else if (action == "msix-uninstall")
                _ = ExecuteAsync("Conferindo aplicativo MSIX...", () => UninstallMsixAsync(item));
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
            else if (action == "external-manage")
                OpenOriginalPackageManager(item);
            else if (action == "windows-manage")
                OpenWindowsInstalledApps(item);
        };
        _programList.ItemCheckboxClicked += item =>
        {
            if (_busy || _betaClose.IsClosing || !_multiSelectMode || !item.CanUninstall) return;
            if (!_checkedApps.Add(item)) _checkedApps.Remove(item);
            _programList.Invalidate();
            RenderTitle();
            UpdateBulkButtons();
        };
        _programList.SelectedIndexChanged += (_, _) =>
        {
            _itemHint.Text = _programList.SelectedItem is ProgramEntry entry
                ? "Selecionado: " + entry.Name : "Selecione um aplicativo";
            if (_programList.SelectedItem is { } selected)
                PresentV269AppDetails(selected);
        };
        var host = new Panel { Name="V269AppsTableFrame",Dock = DockStyle.Fill, BackColor = PanelBackground, Margin = new Padding(0,10,0,0),Padding=new Padding(1) };
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Font = new Font("Segoe UI", 10F);
        _emptyState.ForeColor = Muted;
        _emptyState.BackColor = PanelBackground;
        _emptyState.Padding = new Padding(25);
        _emptyState.Visible = false;
        host.Controls.Add(_programList);
        host.Controls.Add(_emptyState);
        root.Controls.Add(host, 0, 1);
        root.SizeChanged+=(_,_)=>host.Margin=new Padding(0,V269AppTable.Scale(10,_v269Appearance.InterfacePercent,root.DeviceDpi),0,0);
        FitAppLayout();
        UpdateBulkButtons();
        RenderTitle();
        return root;
    }


    private async Task ShowHiddenAppsReviewAsync()
    {
        var scan = await Task.Run(AppServices.ScanHiddenWindowsProgramsForReview);
        if (IsDisposed || _betaClose.IsClosing) return;

        using var dialog = new Form
        {
            Text = "Aplicativos ocultos — consulta",
            StartPosition = FormStartPosition.CenterParent,
            Width = 710,
            Height = 475,
            MinimumSize = new Size(480, 310),
            BackColor = PanelBackground,
            ForeColor = Ink,
            ShowInTaskbar = false
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
            BackColor = PanelBackground
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        var info = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"{scan.Items.Count} entrada(s) marcadas como ocultas no Registro. " +
                "Não são necessariamente aplicativos removíveis. Nenhuma ação é oferecida." +
                (scan.ReadFailures > 0
                    ? $" ATENÇÃO: {scan.ReadFailures} falha(s) de leitura; resultado parcial."
                    : "")
        };
        layout.Controls.Add(info, 0, 0);
        static string DisplayValue(string value) =>
            new string((value ?? "").Where(c => !char.IsControl(c)).Take(160).ToArray());
        string[] entries = scan.Items.OrderBy(item => item.Name,
            StringComparer.CurrentCultureIgnoreCase)
            .Select(item => $"{DisplayValue(item.Name)}  |  Versão: {DisplayValue(item.Version)}  |  " +
                $"Editor: {DisplayValue(item.Publisher)}")
            .ToArray();
        var result = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = AppServices.Background,
            ForeColor = Ink,
            Font = new Font("Consolas", 9F),
            Text = entries.Length > 0
                ? string.Join(Environment.NewLine + Environment.NewLine, entries)
                : "Nenhum componente oculto identificado nesta consulta."
        };
        layout.Controls.Add(result, 0, 1);
        var close = CreateSecondaryButton("Fechar");
        close.Dock = DockStyle.Right;
        close.Width = 105;
        close.Click += (_, _) => dialog.Close();
        layout.Controls.Add(close, 0, 2);
        dialog.Controls.Add(layout);
        dialog.ShowDialog(this);
    }

    private async Task CheckUpdatesFromButtonAsync()
    {
        if (_installerDownloadCts is not null)
        {
            _installerDownloadCts.Cancel();
            Status("Cancelando download...");
            return;
        }
        if (_deploymentWaitCts is not null)
        {
            _deploymentWaitCts.Cancel();
            Status("Cancelando a espera. O instalador pode continuar; não repetir a atualização.");
            return;
        }
        if (_consultaCatalogoCts is not null)
        {
            _consultaCatalogoCts.Cancel();
            Status("Cancelando consulta...");
            return;
        }
        if (_busy || _betaClose.IsClosing) return;
        bool appVerification=_nav.SelectedPage==V269Page.Aplicativos;
        bool loaded = false;
        if (_checkUpdatesButton is { IsDisposed: false } initial)
        {
            initial.Text = "Atualizando lista...";
            initial.Enabled = false; // Win32/MSIX pode entrar em chamada COM nao cancelavel.
        }
        try
        {
            await ExecuteAsync("Atualizando inventário local...", async () =>
            {
                await RefreshCatalogAsync();
                loaded = true;
            });
        }
        finally
        {
            if (_checkUpdatesButton is { IsDisposed: false } done)
            {
                done.Text = "Verificar";
                done.Enabled = true;
            }
        }
        if (!loaded || IsDisposed) return;
        _appFilter = "Todos";
        await LoadUpdatesCoreAsync(forceRefresh: true, appVerification);
        // Não selecionar automaticamente somente os candidatos: preservar
        // a lista inteira, com possíveis atualizações no topo.
        if (!IsDisposed) RenderPrograms();
    }

    private Button AddFilter(TableLayoutPanel heading, string title, int column, string? visibleTitle = null)
    {
        var button = new V269AppToolbarButton(visibleTitle ?? title);
        button.Margin = new Padding(3,3,0,3);
        button.ApplyOptions(_v269Appearance);
        button.Click += (_, _) =>
        {
            if (_busy || _betaClose.IsClosing) return;
            _appFilter = title == "Marcados" ? "Selecionados" : title;
            RenderPrograms();
        };
        heading.Controls.Add(button, column, 0);
        return button;
    }

    // Sessão visual de Aplicativos; jamais modifica o histórico persistente
    // ou atribui o resultado de uma operação à linha atualmente selecionada.
    private readonly List<V269AppPreviousResult> _v269AppPreviousResults = new();
    private bool _v269AppExecutionActive;
    private V269PanelPresentation? _v269AppExecutionResult;
    private V269PanelPresentation? _v269AppRetainedResult;

    private void RecordV269AppResult(string title,string description,V269PanelPresentation? owned=null)
    {
        if(IsDisposed||Disposing)return;
        var entry = new V269AppPreviousResult(Guid.NewGuid(), title, description, owned?.FlowResult?.Complete);
        _v269AppPreviousResults.Insert(0,entry);
        // O HTML exibe os quatro últimos; este limite não alcança o
        // registro persistente nem os estados reais de atualização/remoção.
        if(_v269AppPreviousResults.Count>4)_v269AppPreviousResults.RemoveAt(4);
        if (owned?.FlowResult is { } result && _nav.SelectedPage == V269Page.Aplicativos)
        {
            _v269AppRetainedResult = null;
            _nav.PresentPanel(owned with { FlowResult = result with { HistoryId = entry.Id } });
            return;
        }
        if(owned is not null)_v269AppRetainedResult=owned;
        _nav.RightPane.RefreshAppOperationHistory();
        if(_nav.SelectedPage==V269Page.Aplicativos&&_programList.SelectedItem is {} item)
        {
            PresentV269AppDetails(item);
            if(_nav.RightPane.Current?.Application is not null)_nav.EnsureV269AppPane();
        }
    }
    private void DismissV269AppResult(Guid id)
    {
        if(IsDisposed||Disposing||_nav.SelectedPage!=V269Page.Aplicativos)return;
        bool operational=_nav.RightPane.Current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress;
        if(!operational&&(_busy||_v269Confirming))return;
        if(_v269AppPreviousResults.RemoveAll(x=>x.Id==id)==0)return;
        // Em operação, dispensar apenas a linha da sessão. Nunca substituir
        // Current, resolver a confirmação, alterar token ou gravar histórico.
        if(operational){_nav.RightPane.RefreshAppOperationHistory();return;}
        if(_programList.SelectedItem is {} item)PresentV269AppDetails(item);
    }

    private void PresentV269AppDetails(ProgramEntry item)
    {
        var current=_nav.RightPane.Current;
        if(_busy||_v269Confirming||_nav.SelectedPage!=V269Page.Aplicativos||
            current?.Kind is V269PanelKind.Confirmation or V269PanelKind.Progress or V269PanelKind.History||
            (current?.Kind==V269PanelKind.Result&&!ReferenceEquals(current,_v269AppRetainedResult)))return;
        string Known(string? value)=>string.IsNullOrWhiteSpace(value)?"Não informado":value;
        string architecture=item.ObservedPackageArchitecture.Length>0?item.ObservedPackageArchitecture:
            item.QueryArchitecture=="unknown"?"Não confirmada":Known(item.QueryArchitecture);
        string location=Known(item.ObservedInstallLocation.Length>0?item.ObservedInstallLocation:item.InstallLocation);
        string kind=item.InventorySource.Contains("MSIX",StringComparison.OrdinalIgnoreCase)?"MSIX":
            item.MsiProductCode.Length>0?"MSI":item.InventorySource.StartsWith("Registro:",StringComparison.Ordinal)?"Win32 / registro":"Não confirmado";
        var update=_programList.OperationFor?.Invoke(item);
        string evidence=ApplicationCapabilityInspection.Describe(item,update);
        // O relatório persistente contém apenas códigos/contagens; não
        // atribuir eventos anônimos a um aplicativo. Usar só estados da
        // sessão já associados pelos próprios motores. O motivo de uma
        // operação pendente de família conserva a ressalva de identidade.
        if(update is {Active:false})
            evidence+="\r\nEstado da atualização nesta sessão: "+update.Details;
        var uninstall=_programList.UninstallOperationFor?.Invoke(item);
        if(uninstall is {Active:false})
            evidence+="\r\nDesinstalação nesta sessão: "+uninstall.Details;
        string details=$"{item.Name}\r\n{Known(item.Publisher)}\r\n\r\n"+
            $"Tipo: {kind}\r\nOrigem: {Known(item.InventorySource)}\r\nArquitetura: {architecture}\r\n"+
            $"Versão instalada: {DisplayAppVersion(item.Version)}\r\nInstalado em (declarado): {item.InstallDate?.ToString("dd/MM/yyyy")??"Não informado"}\r\n"+
             $"Local: {location}\r\nOferta: {DisplayAppVersion(item.AvailableVersion)}\r\n\r\n"+
            evidence+
            "\r\n\r\nEnter ou botão direito: ações disponíveis e orientação.";
        var fields=new (string Label,string Value)[]{
            ("Tipo",kind),("Origem",Known(item.InventorySource)),("Arquitetura",architecture),
            ("Versão",DisplayAppVersion(item.Version)),("Data declarada",item.InstallDate?.ToString("dd/MM/yyyy")??"Não informado"),
            ("Local",location),("Oferta",DisplayAppVersion(item.AvailableVersion))};
        _nav.PresentPanel(new(V269PanelKind.Details,"Detalhes do aplicativo",details,Application:
            new V269AppDetailModel(item.Name,Known(item.Publisher),fields,
                evidence,
                _programList.LoadLocalAppIcons?item.DisplayIcon:"",
                _programList.LoadLocalAppIcons?V269MsixIconSource.From(item):null,
                item.DeclaredDescription,_v269AppPreviousResults.ToArray(),DismissV269AppResult)));
    }

    private void DrawProgramCard(object? sender, DrawItemEventArgs e)
    {
        if(e.Index<0||e.Index>=_programList.Items.Count)return;
        ProgramEntry item=_programList.Items[e.Index];
        bool selected=(e.State&DrawItemState.Selected)!=0;
        var options=_v269Appearance;var g=e.Graphics;int dpi=_programList.DeviceDpi;
        int S(int n)=>V269AppTable.Scale(n,options.InterfacePercent,dpi);
        var colors=V269AppTable.Colors(options.Theme);
        var bounds=e.Bounds;
        bool compact=_programList.TableCompact;
        int minimum=V269AppTable.MinimumWidth(options.InterfacePercent,dpi,compact);
        if(bounds.Width<minimum)bounds.Width=minimum;
        int padding=V269AppTable.CellPadding(options.InterfacePercent,dpi,compact);
        var cells=V269AppTable.Columns(bounds,options.InterfacePercent,dpi,compact);
        V269AppTable.DrawRowBackground(g,bounds,options.Theme,selected,e.Index%2!=0,options.InterfacePercent,dpi);
        using var font=new Font("Segoe UI",9f*options.TextPercent/100f);
        using var bold=new Font(font,FontStyle.Bold);
        void Text(string value,Rectangle area,Font? style=null,Color? color=null)
        {
            if(area.Width<1||area.Height<1)return;
            TextRenderer.DrawText(g,value,style??font,area,color??V269AppTable.RowInk(options.Theme,selected),
                TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|
                TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.PreserveGraphicsClipping);
        }
        Rectangle Inset(int i)
        {var cell=cells[i];cell.Inflate(-padding,0);return cell;}
        var grip=V269AppTable.Grip(bounds,options.InterfacePercent,dpi);
        Text(_multiSelectMode?(item.CanUninstall?(_checkedApps.Contains(item)?"☑":"☐"):""):"⋮⋮",grip,bold);
        int iconSize=Math.Max(1,Math.Min(Math.Min(S(30),bounds.Height-6),cells[0].Width/4));
        var mark=new Rectangle(grip.Right+S(3),bounds.Y+(bounds.Height-iconSize)/2,iconSize,iconSize);
        using(var shape=ModernButton.RoundRect(mark,V269AppTable.Scale(8,100,dpi)))
        using(var fill=new SolidBrush(colors.Head))
        using(var border=new Pen(colors.Border))
        {g.FillPath(fill,shape);g.DrawPath(border,shape);}
        string initial=string.IsNullOrWhiteSpace(item.Name)?"?":item.Name.Trim()[0].ToString().ToUpperInvariant();
        if(_programList.LocalAppIcon(item) is {} icon)
        {var inset=mark;inset.Inflate(-S(3),-S(3));if(inset.Width>0&&inset.Height>0)g.DrawImage(icon,inset);}
        else Text(initial,new Rectangle(mark.X+S(7),mark.Y,mark.Width-S(7),mark.Height),bold);
        int nameX=mark.Right+S(9);
        Text(item.Name,new Rectangle(nameX,bounds.Y,Math.Max(1,cells[0].Right-nameX-padding),bounds.Height),bold);
        Text(DisplayAppVersion(item.Version),Inset(1));
        Text(string.IsNullOrWhiteSpace(item.Publisher)?"Não informado":item.Publisher,Inset(2));
        Text(item.InstallDate?.ToString("dd/MM/yyyy")??"Não informada",Inset(3));
        var update=_updateOperations.Find(item);var uninstall=_uninstallOperations.Find(item);
        string state=uninstall?.Text??update?.Text??(
            item.UpdateRequiresReview||Matching.IsUncertain(item.ComparisonState)?"Revisão necessária":
            item.IsUpdate?(_programList.AllowsUpdate(item)?"Atualização disponível":"Oferta consultiva"):
            item.InventoryOnly?"Somente inventário":_inventoryIncomplete?"Inventário parcial":
            !_catalogoVerificado?"Não verificado":"Sem oferta confirmada");
        var status=Inset(4);
        if(uninstall is not null||update is not null)
        {
            var label=status;label.Height=Math.Max(1,status.Height-S(7));Text(state,label,color:V269AppTable.StateInk(options.Theme,selected,true));
            if(update?.Progress.Percent is int percent)
            {
                using var progress=new SolidBrush(V269Palette.EnabledUpdate);
                g.FillRectangle(progress,status.X,status.Bottom-S(5),status.Width*Math.Clamp(percent,0,100)/100,S(3));
            }
            else if(uninstall?.Active==true||update?.Active==true)
            {
                // Atividade indeterminada: não converter tempo decorrido em porcentagem.
                using var track=new SolidBrush(colors.Border);
                using var activity=new SolidBrush(colors.Rail);
                int segment=Math.Max(1,status.Width/3);
                int x=CanAnimateV269?Math.Max(0,status.Width-segment)*_programList.OperationFrame/17:0;
                g.FillRectangle(track,status.X,status.Bottom-S(5),status.Width,S(3));
                g.FillRectangle(activity,status.X+x,status.Bottom-S(5),segment,S(3));
            }
        }
        else Text(state,status,color:V269AppTable.StateInk(options.Theme,selected,item.IsUpdate&&_programList.AllowsUpdate(item)));
        if(!_multiSelectMode)
        {
            var u=_programList.ActionVisual(item,VerifiedUpdaters.GetPrimaryAction(item)??"");
            var d=_programList.ActionVisual(item,item.UninstallAction);
            V269AppTable.DrawAction(g,V269AppTable.Action(bounds,options.InterfacePercent,dpi,false,compact),options.Theme,false,_programList.AllowsUpdate(item),u.Hovered,u.Pressed);
            V269AppTable.DrawAction(g,V269AppTable.Action(bounds,options.InterfacePercent,dpi,true,compact),options.Theme,true,_programList.AllowsRemoval(item),d.Hovered,d.Pressed);
        }
        if(selected&&_programList.Focused)
            ControlPaint.DrawFocusRectangle(g,new Rectangle(bounds.X+S(4),bounds.Y+1,Math.Max(1,bounds.Width-S(5)),Math.Max(1,bounds.Height-2)));
    }

    private void DrawV269RowAction(Graphics graphics, Rectangle area, string label, V269ActionKind kind,
        bool allowed, (bool Hovered, bool Pressed, float Pulse) visual)
    {
        var theme = _v269Appearance.Theme;
        var foreground = V269VisualTokens.ActionColor(theme, kind, allowed);
        if (theme == V269Theme.Claro) foreground = allowed ? Color.White : V269Palette.SecondaryText;
        DrawRowButton(graphics, area, label, V269VisualTokens.ActionBackground(theme, kind, allowed),
            allowed ? visual : default, foreground, V269VisualTokens.ActionBorder(theme, kind, allowed));
    }

    private void DrawUpdateOperation(Graphics graphics, Rectangle area, UpdateOperationSnapshot state)
    {
        Color color = state.Progress.Stage switch
        {
            UpdateStage.Confirmed => Color.FromArgb(120, 222, 165),
            UpdateStage.Failed => Color.FromArgb(255, 133, 143),
            UpdateStage.Pending or UpdateStage.Blocked or UpdateStage.AuthorizationDeclined => Color.FromArgb(245, 204, 103),
            UpdateStage.Cancelled => Muted,
            _ => Color.FromArgb(114, 196, 255)
        };
        string label = state.Text;
        // Preservar o número e o resultado essencial mesmo no DPI mais alto.
        if (TextRenderer.MeasureText(graphics, label, _rowInfo).Width > area.Width)
            label = state.Progress.Stage switch
            {
                UpdateStage.Downloading => state.Progress.Percent is int p ? $"Baixando — {p}%" : "Baixando...",
                UpdateStage.Confirmed => state.RestartRequired ? "✓ 100% · Reinicie" : "✓ 100% · Concluída",
                UpdateStage.Failed => "Falha · veja detalhes",
                UpdateStage.Pending => "Resultado pendente",
                UpdateStage.Blocked => "Atualização bloqueada",
                UpdateStage.AuthorizationDeclined => "UAC recusado",
                UpdateStage.Cancelled => "Cancelada",
                UpdateStage.VerifyingIntegrity => "Conferindo integridade...",
                UpdateStage.ValidatingIdentity => "Conferindo pacote...",
                UpdateStage.ValidatingSignature => "Conferindo assinatura...",
                UpdateStage.Confirming => "Conferindo versão...",
                _ => state.Progress.Stage == UpdateStage.Installing ? "Instalando..." : "Preparando..."
            };
        TextRenderer.DrawText(graphics, label, _rowInfo,
            new Rectangle(area.X, area.Y - 2, area.Width, area.Height - 4), color,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        var track = new Rectangle(area.X + 1, area.Bottom - 5, area.Width - 2, 4);
        if (track.Width < 4) return;
        using var background = new SolidBrush(Color.FromArgb(20, 30, 42));
        using var fill = new SolidBrush(color);
        using var rounded = ModernButton.RoundRect(track, 2);
        graphics.FillPath(background, rounded);
        var oldClip = graphics.Save();
        try
        {
            graphics.SetClip(rounded, CombineMode.Intersect);
            if (state.Progress.Percent is int percent)
                graphics.FillRectangle(fill, track.X, track.Y, track.Width * percent / 100, track.Height);
            else if (state.Active)
            {
                int segment = Math.Max(12, track.Width / 4);
                int offset = (_programList.OperationFrame * (track.Width + segment) / 18) - segment;
                graphics.FillRectangle(fill, track.X + offset, track.Y, segment, track.Height);
            }
            else graphics.FillRectangle(fill, track);
        }
        finally { graphics.Restore(oldClip); }
    }

    private static string DisplayAppVersion(string version)
        => string.IsNullOrWhiteSpace(version) || version.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? "não informada" : version;

    private static void DrawRowButton(Graphics graphics, Rectangle rect, string text, Color color,
        (bool Hovered, bool Pressed, float Pulse) visual, Color? textColor = null, Color? border = null)
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
        if (border is Color borderColor)
        {
            using var outline = new Pen(borderColor);
            graphics.DrawPath(outline, path);
        }
        if (visual.Hovered || visual.Pulse > 0)
        {
            using var outline = new Pen(Color.FromArgb(
                65 + (int)(60 * pulse), 213, 233, 255), 1.1F);
            graphics.DrawPath(outline, path);
        }
        using var font = new Font("Segoe UI", 8.4F, FontStyle.Bold);
        TextRenderer.DrawText(graphics, text, font, drawRect,
            textColor ?? Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    private void AddToolsPages()
    {
        var cleaning = new Panel { BackColor = AppServices.Background, Dock = DockStyle.Fill };
        var repair = new Panel { BackColor = AppServices.Background, Dock = DockStyle.Fill };
        var system = new Panel { BackColor = AppServices.Background, Dock = DockStyle.Fill };

        // Cores de impacto: azul = rotina, cinza = baixo impacto/inspeção,
        // âmbar = atenção, vermelho = interrupção sensível ou remoção definitiva.
        var cleaningActions = CreateV269CleanupTools(new[]
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
                "Remove downloads antigos quando os serviços de atualização estão parados.",
                AppServices.Red,
                () => ConfirmThenRun("clear-update-cache", "Limpar arquivos antigos do cache do Windows Update?")),
            Tool("Esvaziar Lixeira",
                "Apaga permanentemente os itens da Lixeira. Não é possível desfazer.",
                AppServices.Red,
                EmptyRecycleBinAsync),
            Tool("Limpeza avançada",
                "Abre a ferramenta do Windows para revisar e escolher o que será removido.",
                AppServices.Blue,
                () => ConfirmThenRun("cleanmgr-auto", "Abrir a Limpeza de Disco do Windows?")),
            Tool("Cache de shaders DirectX",
                "Remove cache gráfico antigo. Jogos podem precisar recompilar shaders.",
                AppServices.Gray,
                () => ConfirmThenRun("clean-directx-cache", "Remover arquivos antigos do cache D3DSCache? Jogos poderão recompilar os shaders e ficar temporariamente mais lentos na primeira execução.")),

        });
        var cleaningLayout = new TableLayoutPanel
        {
            Name = "V269CleanupPage", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty,
            BackColor = AppServices.Background
        };
        cleaningLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 342));
        cleaningLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 275));
        cleaningLayout.Controls.Add(cleaningActions, 0, 0);
        cleaningLayout.Controls.Add(BuildDiskPanel(), 0, 1);
        cleaning.Controls.Add(ScrollablePage(cleaningLayout, width =>
        {
            int cardsHeight = ToolsHeight(cleaningActions, width);
            int diskHeight = DiskPanelHeight(width);
            cleaningLayout.RowStyles[0].Height = cardsHeight;
            cleaningLayout.RowStyles[1].Height = diskHeight;
            return cardsHeight + diskHeight;
        }, trackCleaning: true));
        // Fluxo simples para uso cotidiano: DISM repara a imagem; em seguida SFC
        // verifica e tenta reparar os arquivos protegidos. Consultas separadas
        // continuam no diagnóstico do computador, sem alterações automáticas.
        var repairActions = CreateToolsFlow(new[]
        {
            Tool("Verificar Windows",
                "Verifica arquivos importantes do Windows, sem alterar nada. Pode demorar.",
                AppServices.Blue,
                () => ExecuteAsync("Verificando integridade do Windows...", VerifyWindowsIntegrityAsync)),
            Tool("Reparar Windows",
                "Fica disponível somente quando uma verificação encontra problemas que podem ser corrigidos. Pede confirmação.",
                AppServices.Green, RepairWindowsAfterVerificationAsync),
            Tool("Analisar rede",
                "Consulta o adaptador e o gateway e testa DNS e uma conexão TCP na porta 443.",
                AppServices.Blue,
                RunNetworkDiagnosticAsync),
            Tool("Reparar rede",
                "Oferece limpar o cache quando o teste de resolução DNS falha. Pede confirmação.",
                AppServices.Blue,
                RepairNetworkAfterDiagnosisAsync),
            Tool("Diagnosticar áudio",
                "Consulta as saídas de áudio ativas e a saída padrão para multimídia.",
                AppServices.Blue, RunAudioDiagnosticAsync),
            Tool("Diagnosticar impressora",
                "Consulta o estado da fila da impressora padrão do Windows.",
                AppServices.Blue, RunPrinterDiagnosticAsync),
            Tool("Diagnosticar USB",
                "Consulta a presença e os códigos PnP de dispositivos USB.",
                AppServices.Blue, RunUsbDiagnosticAsync),
            Tool("Diagnosticar chipset",
                "Consulta o estado PnP dos controladores SMBus e da ponte PCI–ISA.",
                AppServices.Blue, RunChipsetDiagnosticAsync),
            Tool("Diagnosticar tela azul",
                "Consulta códigos de parada e desligamentos inesperados registrados pelo Windows.",
                AppServices.Blue, RunBlueScreenDiagnosticAsync),
            Tool("Restaurar configurações da rede", "Opção avançada: restaura as configurações da conexão. A internet pode cair e o Windows pode precisar reiniciar.",
                AppServices.Orange,
                () => ConfirmThenRun("network-reset", "Restaurar as configurações da conexão? A internet pode cair. Talvez seja necessário reiniciar o Windows.")),
            Tool("Criar ponto de restauração",
                "Guarda um ponto para ajudar a desfazer alterações no Windows. Não substitui cópia dos arquivos.",
                AppServices.Green,
                () => ConfirmThenRun("create-restore-point",
                    "Solicitar um ponto de restauração ao Windows? Requer autorização administrativa e Proteção do Sistema habilitada. " +
                    "Não substitui backup e o Windows pode reutilizar um ponto recente.")),
            Tool("Escolher ponto de restauração",
                "Permite escolher um ponto anterior pela ferramenta de recuperação do Windows.",
                AppServices.Gray, ChooseRestorePointAsync)
        });
        repairActions.Name = "RepairActions";
        var repairLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Margin = Padding.Empty, BackColor = AppServices.Background };
        // Proporções responsivas: em janelas menores os cartões usam a rolagem escura.
        repairLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 342));
        repairLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        repairLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
        repairLayout.Controls.Add(repairActions, 0, 0);
        repairLayout.Controls.Add(_repairReport, 0, 1);
        repairLayout.Controls.Add(BuildNetworkResultPanel(), 0, 2);
        repair.Controls.Add(ScrollablePage(repairLayout, width =>
        {
            int cardsHeight = ToolsHeight(repairActions, width);
            int resultHeight = NetworkResultHeight();
            int diagnosticHeight = RepairDiagnosticHeight(width);
            repairLayout.RowStyles[0].Height = cardsHeight;
            repairLayout.RowStyles[1].Height = diagnosticHeight;
            repairLayout.RowStyles[2].Height = resultHeight;
            return cardsHeight + resultHeight + diagnosticHeight;
        }, trackRepair: true));
        // Painéis nativos do gerenciador: nenhum atalho para Task Manager,
        // MSConfig, Configurações, Windows Update ou MSInfo32.
        var systemContent = BuildIntegratedSystemPanel();
        system.Controls.Add(ScrollablePage(systemContent,
            _ => SystemPageMinimumHeight(systemContent), trackSystem: true));
        _nav.SetPageContent(V269Page.Limpeza, cleaning);
        _nav.SetPageContent(V269Page.Reparo, repair);
        _nav.SetPageContent(V269Page.Sistema, system);
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

    // Apresentação exclusiva de Limpeza; os seis manipuladores reais permanecem intactos.
    private Control CreateV269CleanupTools(IEnumerable<ToolInfo> tools)
    {
        var host = new DarkScrollableFlow
        {
            Name = "V269CleanupActions", Dock = DockStyle.Fill, Margin = Padding.Empty,
            EmbeddedInPage = true, BackColor = AppServices.Background
        };
        var flow = host.Content;
        flow.Padding = Padding.Empty;
        flow.BackColor = AppServices.Background;
        var cards = new List<V269CleanupCard>();
        string[] glyphs = ["folder", "monitor", "refresh", "trash", "brush", "cpu"];
        string[] risks = ["Baixo", "Médio", "Atenção", "Irreversível", "Variável", "Baixo"];
        CleanupTarget?[] targets = [CleanupTarget.UserTemp,CleanupTarget.WindowsTemp,CleanupTarget.UpdateCache,CleanupTarget.RecycleBin,null,CleanupTarget.DirectX];
        foreach (var tool in tools)
        {
            int index=cards.Count;
            var card = new V269CleanupCard(tool.Name, tool.Description, index);
            cards.Add(card);
            _actionControls.Add(card.Command);
            void Select()
            {
                SelectV269CleanupItem(card,()=>new V269AppDetailModel(tool.Name,"Ferramenta de manutenção",
                    [("Risco",risks[index]),("Estimativa",targets[index] is {} target&&_cleanupRows.TryGetValue(target,out var row)?row.State.Text:
                        index==4?"Definida na ferramenta do Windows":"Aguardando análise")],
                    "A operação real exige autorização e respeita as verificações de segurança do Windows.",
                    Description:tool.Description,CleanupGlyph:glyphs[index],CleanupDescriptionHeading:"O que faz"));
            }
            if(index==0)_v269CleanupDefault=Select;
            card.Enter += (_, _) => Select();
            card.Click += (_, _) => Select();
            card.Command.Click += async (_, _) =>
            {
                if (_busy || _betaClose.IsClosing) return;
                Select();
                try { await tool.Action(); }
                catch (Exception ex) { ReportError(ex); }
            };
            flow.Controls.Add(card);
        }
        _v269CleanupDefault?.Invoke();
        bool fitting = false;
        void Fit()
        {
            if (fitting || host.IsDisposed || host.ClientSize.Width <= 0) return;
            fitting = true;
            try
            {
                int S(int x) => V269AppTable.Scale(x, _v269Appearance.InterfacePercent, host.DeviceDpi);
                int gap = S(9);
                int width = Math.Max(1, host.ClientSize.Width - Math.Max(16, S(16)));
                int columns = _nav.LayoutWindowWidth <= 850 || width < S(540) ? 2 : 3;
                if (width < S(330)) columns = 1;
                int cardWidth = Math.Max(1, (width - gap * (columns - 1)) / columns);
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    card.Margin = new Padding(0, 0, i % columns == columns - 1 ? 0 : gap, gap);
                    card.Size = new Size(cardWidth, S(114));
                    card.ApplyOptions(_v269Appearance);
                }
                host.Reflow();
            }
            finally { fitting = false; }
        }
        host.Tag = (Action)Fit;
        host.SizeChanged += (_, _) => Fit();
        host.DpiChangedAfterParent += (_, _) => Fit();
        Fit();
        return host;
    }

    private Control CreateToolsFlow(IEnumerable<ToolInfo> tools, bool compact = false)
    {
        var host = new DarkScrollableFlow
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            EmbeddedInPage = true, BackColor = AppServices.Background
        };
        var flow = host.Content;
        var measuredCards = new List<(Panel Card, Label Description)>();
        flow.Padding = new Padding(7);
        flow.BackColor = AppServices.Background;
        foreach (ToolInfo tool in tools)
        {
            var card = new Panel
            {
                Size = new Size(244, 90),
                BackColor = PanelBackground,
                Margin = new Padding(3, 3, 5, 6),
                Padding = new Padding(6)
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var button = CreateButton(tool.Name, tool.Color, Color.White);
            if (button is ModernButton modernButton)
                modernButton.AllowMultilineText = false;
            // Um titulo curto nao precisa ocupar todo o cartao.
            // Largura proporcional ao texto, mantendo espaco clicavel.
            int preferredButtonWidth = TextRenderer.MeasureText(
                tool.Name, button.Font, new Size(int.MaxValue, 33),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + 30;
            button.Dock = DockStyle.None;
            button.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            // Cartoes de reparo largos podem acomodar o titulo completo:
            // limitar artificialmente a 215px fazia ModernButton diminuir
            // a fonte de alguns botoes, apesar do espaco livre.
            button.Size = new Size(compact
                ? Math.Clamp(preferredButtonWidth, 112, 215)
                : Math.Max(112, preferredButtonWidth), 30);
            button.Margin = Padding.Empty;
            button.MinimumSize = new Size(0, 29);
            button.AccessibleName = tool.Name;
            button.AccessibleDescription = tool.Description;
            _toolTips.SetToolTip(button, tool.Description);
            _actionControls.Add(button);
            if (tool.Name == "Reparar rede")
            {
                _repairNetworkButton = button;
                button.Enabled = false; // A correção depende de um diagnóstico anterior.
            }
            if (tool.Name == "Reparar Windows")
            {
                _repairWindowsButton = button;
                button.Enabled = false;
            }
            button.Click += async (_, _) =>
            {
                if (_busy || _betaClose.IsClosing) return;
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
                Padding = new Padding(2, 2, 2, 0),
                TextAlign = ContentAlignment.TopLeft
            };

            _toolTips.SetToolTip(description, tool.Description);
            layout.Controls.Add(button, 0, 0);
            layout.Controls.Add(description, 0, 1);
            card.Controls.Add(layout);
            flow.Controls.Add(card);
            measuredCards.Add((card, description));
        }
        // Tipografia e botoes mantem o mesmo tamanho de desenho em monitores
        // grandes; apenas a quantidade de colunas e a largura se adaptam.
        bool fittingCards = false;
        void FitCards()
        {
            if (fittingCards || host.IsDisposed || host.ClientSize.Width <= 0) return;
            fittingCards = true;
            try
            {
                double scale = Math.Max(1d, host.DeviceDpi / 96d);
                int viewport = Math.Max(1, host.ClientSize.Width - (int)Math.Ceiling(16 * scale));
                int usable = Math.Max(1, viewport - flow.Padding.Horizontal);
                int desired = (int)Math.Ceiling((compact ? 216 : 244) * scale);
                int margin = (int)Math.Ceiling(8 * scale);
                // Seis ferramentas de limpeza e os comandos de reparo usam
                // ate tres colunas, preenchendo a largura sem espacos de cards removidos.
                int columns = Math.Clamp(usable / Math.Max(1, desired + margin), 1, 3);
                if (compact && usable < (int)Math.Ceiling(520 * scale)) columns = 1;
                int inset = (int)Math.Ceiling(7 * scale);
                int cardWidth = Math.Max(1, (viewport - 2 * inset - columns * margin) / columns);
                Padding balanced = new Padding(inset, 7, inset, 7);
                if (flow.Padding != balanced) flow.Padding = balanced;
                // A altura depende do texto real e da largura atual.
                // Um card com duas linhas nao pode reservar a altura de
                // outro com quatro linhas, e nenhum texto deve ser cortado.
                int buttonRow = (int)Math.Ceiling(33 * scale);
                int minHeight = (int)Math.Ceiling(78 * scale);
                int maxHeight = (int)Math.Ceiling(160 * scale);
                int cardHeight = minHeight;
                foreach (var (card, description) in measuredCards)
                {
                    if (card.Width != cardWidth) card.Width = cardWidth;
                    // A largura inicial do botao podia ultrapassar o card
                    // em viewports estreitos (reproducao real na CI 800x600).
                    var cardButton = card.Controls.OfType<TableLayoutPanel>()
                        .SelectMany(layout => layout.Controls.OfType<Button>()).FirstOrDefault();
                    if (cardButton is not null)
                    {
                        int measured = TextRenderer.MeasureText(
                            cardButton.Text, cardButton.Font, new Size(int.MaxValue, 33),
                            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + 30;
                        int ideal = compact ? Math.Clamp(measured, 112, 215)
                            : Math.Max(112, measured);
                        int fit = Math.Min(ideal, Math.Max(90,
                            cardWidth - card.Padding.Horizontal - 6));
                        if (cardButton.Width != fit) cardButton.Width = fit;
                    }
                    int labelWidth = Math.Max(90,
                        cardWidth - card.Padding.Horizontal - description.Padding.Horizontal - 4);
                    int textHeight = TextRenderer.MeasureText(description.Text,
                        description.Font, new Size(labelWidth, 1000),
                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
                    int required = buttonRow + card.Padding.Vertical +
                        description.Padding.Vertical + textHeight +
                        (int)Math.Ceiling(5 * scale);
                    cardHeight = Math.Max(cardHeight, required);
                }
                cardHeight = Math.Clamp(cardHeight, minHeight, maxHeight);
                foreach (var (card, _) in measuredCards)
                    if (card.Height != cardHeight) card.Height = cardHeight;
                host.Reflow();
            }
            finally { fittingCards = false; }
        }
        host.SizeChanged += (_, _) => FitCards();
        host.DpiChangedAfterParent += (_, _) => FitCards();
        FitCards();
        return host;
    }

    private Control BuildDiskPanel()
    {
        var root = new V269CleanupSection
        {
            Name = "V269DiskSection", Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1,
            Margin = Padding.Empty, Padding = new Padding(13, 11, 13, 11),
            BackColor = PanelBackground
        };
        int D(int x) => V269AppTable.Scale(x,_v269Appearance.InterfacePercent,root.DeviceDpi);
        _v269CleanupDiskSection=root;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, D(30)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, D(49)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, D(32)));
        var reportRow = new RowStyle(SizeType.Absolute, D(100));
        root.RowStyles.Add(reportRow);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, D(20)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = new Label
        {
            Name = "V269DiskHeading", Text = "Espaço em disco", Dock = DockStyle.Fill,
            ForeColor = Ink, BackColor = Color.Transparent, Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(D(30), 0, 0, 0), Margin = Padding.Empty
        };
        heading.Paint += (_, e) => V269CleanupGlyph.Draw(e.Graphics, "disk", new Rectangle(D(1), Math.Max(0, (heading.Height - D(21))/2), D(21), D(21)), _v269Appearance.Theme);
        root.Controls.Add(heading, 0, 0);

        var previewRow = new TableLayoutPanel { Name = "V269CleanupToolbar", Dock = DockStyle.Fill,
            ColumnCount = 4, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        previewRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, D(137)));
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, D(169)));
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, D(137)));
        previewRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var previewButton = _cleanupPreviewButton = new V269CleanupButton("Analisar limpeza", "search", true);
        previewButton.Dock = DockStyle.Fill;
        previewButton.Margin = new Padding(0, D(4), D(9), D(15));
        previewButton.Click += async (_, _) => await ShowCleanupPreviewAsync();
        _actionControls.Add(previewButton);
        previewRow.Controls.Add(previewButton, 0, 0);
        var fullAudit = new V269CleanupButton("Diagnóstico completo", "shield");
        fullAudit.Dock = DockStyle.Fill;
        fullAudit.Margin = new Padding(0, D(4), D(9), D(15));
        fullAudit.Click += async (_, _) =>
            await ExecuteAsync("Analisando instalações, dispositivos e proteção...", RunMaintenanceAuditAsync);
        fullAudit.AccessibleDescription = "Revisa inventário, dispositivos, antivírus e firewall. Somente leitura.";
        _actionControls.Add(fullAudit);
        previewRow.Controls.Add(fullAudit, 1, 0);
        _refreshDisksButton = new V269CleanupButton("Atualizar discos", "refresh");
        _refreshDisksButton.Dock = DockStyle.Fill;
        _refreshDisksButton.Margin = new Padding(0, D(4), D(9), D(15));
        _refreshDisksButton.Click += async (_, _) => await RefreshDiskUsageAsync();
        _refreshDisksButton.AccessibleDescription = "Reconsultar o espaço livre e ocupado de cada unidade local.";
        _actionControls.Add(_refreshDisksButton);
        previewRow.Controls.Add(_refreshDisksButton, 2, 0);
        previewRow.Paint += (_, e) =>
        {
            using var pen = new Pen(V269Palette.Border);
            e.Graphics.DrawLine(pen, 0, previewRow.Height - 1, previewRow.Width - 1, previewRow.Height - 1);
        };
        root.Controls.Add(previewRow, 0, 1);
        var modePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Margin = Padding.Empty, BackColor = Color.Transparent };
        modePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        modePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        _cleanupPreview.Text = "Estimativa por categoria";
        _cleanupPreview.Dock = DockStyle.Fill;
        _cleanupPreview.ForeColor = Ink;
        _cleanupPreview.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _cleanupPreview.BackColor = Color.Transparent;
        _cleanupPreview.TextAlign = ContentAlignment.MiddleLeft;
        _cleanupPreview.AutoEllipsis = true;
        modePanel.Controls.Add(_cleanupPreview, 0, 0);
#if !GF_BETA
        var testMode = new ComboBox
        {
            Name = "TestDiagnosticProfile", Dock = DockStyle.Fill,
            Margin = new Padding(3, 3, 0, 3), DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(37, 48, 61), ForeColor = Ink,
            FlatStyle = FlatStyle.Flat
        };
        testMode.Items.AddRange(new object[] { "Teste desligado", "Teste PC FORTE", "Teste PC FRACO" });
        testMode.SelectedIndex = 0;
        testMode.SelectedIndexChanged += (_, _) =>
        {
            TestDiagnosticDetail.SetProfile(testMode.SelectedIndex, DiagnosticTrace.Write);
            if (TestDiagnosticDetail.Enabled)
            {
                var bounds = Screen.FromControl(this).Bounds;
                TestDiagnosticDetail.Record("AMBIENTE",
                    ("largura", bounds.Width), ("altura", bounds.Height),
                    ("dpi", DeviceDpi), ("logicos", Environment.ProcessorCount));
                Log("Coleta detalhada de testes ativa apenas nesta sessão, somente contagens locais.");
                _ = InspectWingetContextForTestAsync();
            }
            else Log("Coleta detalhada de testes desativada.");
        };
        testMode.AccessibleDescription = "Coleta local de testes, sem envio; volta ao padrão ao fechar.";
        modePanel.Controls.Add(testMode, 1, 0);
#endif
        root.Controls.Add(modePanel, 0, 2);
        bool fittingCommands=false;
        string? previousToolbarGeometry=null;
        void FitPreviewCommands()
        {
            if(fittingCommands||root.IsDisposed)return;
            fittingCommands=true;
            try
            {
                root.Padding=new Padding(D(13),D(11),D(13),D(11));
                var buttons=new[]{previewButton,fullAudit,_refreshDisksButton!};
                int available=Math.Max(1,root.ClientSize.Width-root.Padding.Horizontal);
                int gap=D(9);
                int[] desired=buttons.Select(button=>
                    Math.Max(D(80),TextRenderer.MeasureText(button.Text,button.Font,Size.Empty,
                        TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width+D(36))).ToArray();
                int columns=desired.Sum()+gap*2<=available?3:
                    Math.Max(desired[0],desired[1])*2+gap<=available?2:1;
                int rows=(3+columns-1)/columns;
                int height=Math.Max(D(30),buttons.Max(button=>button.Font.Height)+D(8));
                string geometry=$"{available}:{columns}:{height}:{string.Join(",",desired)}:{D(9)}:{heading.Font.Height}:{_cleanupPreview.Font.Height}:{_diskSummary.Font.Height}";
                if(previousToolbarGeometry==geometry)return;
                previousToolbarGeometry=geometry;
                previewRow.SuspendLayout();
                try
                {
                    previewRow.Controls.Clear();
                    previewRow.RowCount=rows;previewRow.ColumnCount=columns+1;
                    previewRow.ColumnStyles.Clear();previewRow.RowStyles.Clear();
                    for(int i=0;i<columns;i++)
                        previewRow.ColumnStyles.Add(columns==3?new ColumnStyle(SizeType.Absolute,desired[i]+gap):
                            new ColumnStyle(SizeType.Percent,100f/columns));
                    previewRow.ColumnStyles.Add(columns==3?new ColumnStyle(SizeType.Percent,100):new ColumnStyle(SizeType.Absolute,0));
                    for(int row=0;row<rows;row++)previewRow.RowStyles.Add(new RowStyle(SizeType.Absolute,height+D(9)+(row==rows-1?D(10):0)));
                    for(int i=0;i<buttons.Length;i++)
                    {
                        buttons[i].Margin=new Padding(0,D(4),gap,D(5)+(i/columns==rows-1?D(10):0));
                        previewRow.Controls.Add(buttons[i],i%columns,i/columns);
                    }
                    root.RowStyles[0].Height=Math.Max(D(30),heading.Font.Height+D(5));
                    root.RowStyles[1].Height=rows*(height+D(9))+D(10);
                    root.RowStyles[2].Height=Math.Max(D(32),_cleanupPreview.Font.Height+D(8));
                    root.RowStyles[4].Height=Math.Max(D(12),_diskSummary.Font.Height+2);
                }
                finally{previewRow.ResumeLayout(true);}
            }
            finally{fittingCommands=false;}
        }
        root.SizeChanged += (_, _) => FitPreviewCommands();
        root.DpiChangedAfterParent+=(_,_)=>FitPreviewCommands();
        _fitV269CleanupDiskSection=FitPreviewCommands;
        FitPreviewCommands();
        root.Controls.Add(BuildCleanupReport(reportRow), 0, 3);
        _diskSummary.Text = "Consulte o espaço livre e ocupado das unidades locais.";
        _diskSummary.Dock = DockStyle.Fill;
        _diskSummary.ForeColor = Muted;
        _diskSummary.Font = new Font("Segoe UI", 8.6F);
        _diskSummary.TextAlign = ContentAlignment.MiddleLeft;
        _diskSummary.BackColor = Color.Transparent;
        root.Controls.Add(_diskSummary, 0, 4);
        _diskCards.EmbeddedInPage = true;
        _diskCards.Dock = DockStyle.Fill;
        _diskCards.Content.Padding = new Padding(0, 4, 0, 2);
        _diskCards.Margin = Padding.Empty;
        _diskCards.BackColor = PanelBackground;
        _diskCards.Content.BackColor = PanelBackground;
        _diskCards.Resize += (_, _) => ResizeDiskCards();
        root.Controls.Add(_diskCards, 0, 5);
        return root;
    }

    private Control CreateDiskCard(DiskUsageInfo disk)
    {
        long used = disk.TotalBytes - disk.FreeBytes;
        double fraction = disk.TotalBytes > 0 ? Math.Clamp((double)used / disk.TotalBytes, 0d, 1d) : 0d;
        int percent = (int)Math.Round(fraction * 100);
        string letter = disk.Root.TrimEnd('\\');
        string name = string.IsNullOrWhiteSpace(disk.Label) ? "Disco local" : disk.Label;
        var card = new V269CleanupDiskCard($"{name} ({letter})",
            $"{DisplayGigabytes(used)} de {DisplayGigabytes(disk.TotalBytes)} ({percent}%)",
            $"{disk.FileSystem} · {DisplayGigabytes(disk.FreeBytes)} livres", fraction);
        string? task = SingleVolumeOptimization.TaskForRoot(disk.Root);
        var optimize = card.Command;
        optimize.Name = "OptimizeVolume" + letter.TrimEnd(':');
        optimize.AccessibleName = "Otimizar somente " + letter;
        optimize.Tag = task;
        optimize.Enabled = task is not null &&
            SingleVolumeOptimization.IsEligible(DriveType.Fixed, true, disk.FileSystem);
        optimize.AccessibleDescription = optimize.Enabled
            ? "Otimizar apenas " + letter + " com o método adequado ao volume (SSD/HD)."
            : "Otimização indisponível para este sistema de arquivos ou volume.";
        optimize.Click += async (_, _) =>
        {
            if (task is null || !optimize.Enabled || _busy) return;
            SelectDisk();
            await ConfirmThenRun(task,
                "Otimizar somente a unidade " + letter +
                "? O Windows escolherá o método adequado e poderá solicitar autorização.");
        };
        void SelectDisk() => SelectV269CleanupItem(card,()=>new V269AppDetailModel($"{name} ({letter})",disk.FileSystem,
            [("Capacidade total",DisplayGigabytes(disk.TotalBytes)),("Espaço utilizado",DisplayGigabytes(used)),
             ("Espaço livre",DisplayGigabytes(disk.FreeBytes)),("Sistema de arquivos",disk.FileSystem)],
            optimize.Enabled?"A otimização real afeta apenas a unidade selecionada e exige confirmação.":optimize.AccessibleDescription??"Otimização indisponível.",
            Description:"O Windows escolhe o método de otimização adequado à unidade.",CleanupGlyph:"disk",CleanupDescriptionHeading:"Otimização"));
        card.Click+=(_,_)=>SelectDisk();card.Enter+=(_,_)=>SelectDisk();
        _actionControls.Add(optimize);
        card.ApplyOptions(_v269Appearance);
        return card;
    }

    private void ResizeDiskCards()
    {
        if (_diskCards.IsDisposed || _diskCards.Content.Controls.Count == 0) return;
        int area = _diskCards.ClientSize.Width - _diskCards.Content.Padding.Horizontal;
        if (area <= 0) return;
        foreach (Control control in _diskCards.Content.Controls)
        {
            if (control is not V269CleanupDiskCard card) continue;
            card.ApplyOptions(_v269Appearance);
            card.Size = new Size(area, card.HeightForWidth(area));
            card.Margin = new Padding(0, 0, 0, V269AppTable.Scale(8, _v269Appearance.InterfacePercent, card.DeviceDpi));
        }
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
                _actionControls.RemoveAll(control => control.IsDisposed);
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
            _relayoutCleaning?.Invoke();
            if(_v269CleanupSelected?.IsDisposed==true)PresentV269CleanupSelection();
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Margin = Padding.Empty };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        _status.Text = "Pronto";
        _status.Dock = DockStyle.Fill;
        _status.AutoEllipsis = true;
        _status.ForeColor = Muted;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        top.Controls.Add(_status, 0, 1);
        AddUninstallCancelButton(top);
        top.SetColumnSpan(_status, 4);
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

    private void ShowLocalReliabilityReport()
    {
        string report = _reliability.Report(_installed, _inventoryIncomplete);
        using var dialog = new Form
        {
            Text = "Histórico local — somente leitura", Width = 770, Height = 530,
            MinimumSize = new Size(520, 360), StartPosition = FormStartPosition.CenterParent,
            BackColor = PanelBackground, ForeColor = Ink, Font = Font,
            AutoScaleMode = AutoScaleMode.Dpi, ShowInTaskbar = false,
            MinimizeBox = false, MaximizeBox = false
        };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1,
            Padding = new Padding(10), BackColor = PanelBackground };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        var body = new DarkLogView
        {
            Name = "LocalReliabilityReportText", Dock = DockStyle.Fill,
            AccessibleName = "Histórico e relatório local somente leitura"
        };
        body.UseReadableFont(new Font("Segoe UI", 9F));
        body.SetText(report);
        root.Controls.Add(body, 0, 0);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = CreateSecondaryButton("Salvar TXT");
        save.Name = "SaveLocalReliabilityReport";
        save.Size = new Size(135, 35);
        save.Click += (_, _) =>
        {
            using var picker = new SaveFileDialog
            {
                Title = "Salvar relatório local sem nomes e caminhos",
                Filter = "Texto (*.txt)|*.txt", FileName = "GF-Relatorio-Local.txt",
                AddExtension = true, OverwritePrompt = true
            };
            if (picker.ShowDialog(dialog) != DialogResult.OK) return;
            try { File.WriteAllText(picker.FileName, report, new System.Text.UTF8Encoding(false)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                MessageBox.Show(dialog, "Não foi possível salvar: " + ex.GetType().Name,
                    "Relatório não salvo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        var close = CreateSecondaryButton("Fechar");
        close.Name = "CloseLocalReliabilityReport";
        close.Size = new Size(110, 35);
        close.DialogResult = DialogResult.Cancel;
        bar.Controls.Add(close);
        bar.Controls.Add(save);
        dialog.AcceptButton = close;
        dialog.CancelButton = close;
        root.Controls.Add(bar, 0, 1);
        dialog.Controls.Add(root);
        dialog.HandleCreated += (_, _) =>
        {
            try { int dark = 1; if (NativeMethods.DwmSetWindowAttribute(dialog.Handle, 20, ref dark, 4) != 0)
                    _ = NativeMethods.DwmSetWindowAttribute(dialog.Handle, 19, ref dark, 4); }
            catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        };
        dialog.Shown += (_, _) =>
        {
            dialog.Bounds = UninstallCleanupDialog.FitBounds(dialog.Bounds, Screen.FromControl(this).WorkingArea);
            close.Focus();
        };
        dialog.ShowDialog(this);
    }

    private static Button CreateSecondaryButton(string text) => new ModernButton
    {
        Text = text,
        BackColor = Color.FromArgb(43, 56, 70),
        ForeColor = Color.FromArgb(221, 235, 252),
        OutlineColor = Color.FromArgb(85, 112, 137),
        Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
        AllowMultilineText = true,
        Cursor = Cursors.Hand,
        CornerRadius = 10
    };

    private static Button CreateButton(string text, Color background, Color foreground) => new ModernButton
    {
        Text = text, BackColor = background, ForeColor = foreground,
        Font = new Font("Segoe UI", 9.3F, FontStyle.Bold),
        AllowMultilineText = true,
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
            if (_busy || _betaClose.IsClosing) return;
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
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<string>(Status), text); }
            catch (InvalidOperationException) { }
            return;
        }
        _operationResult = text;
        _status.Text = text;
        if (_v269CleanupExecutionActive && _cleanupCancellation is { } && _nav.SelectedPage==V269Page.Limpeza &&
            _nav.RightPane.Current is not {Kind:V269PanelKind.Result})
            _nav.PresentPanel(new(V269PanelKind.Progress,"Operação em andamento",text,CancelFlow:RequestCleanupCancellation,FlowPage:V269Page.Limpeza,CancelLabel:"Cancelar limpeza"));
        else if (_busy && _nav.RightPane.Current is not ({ Kind: V269PanelKind.Result } or { CancelFlow: not null }))
            _nav.PresentPanel(new(V269PanelKind.Progress, "Operação em andamento", text));
        _toolTips.SetToolTip(_status, _v269CleanupExecutionActive || _nav.SelectedPage==V269Page.Limpeza ? null : text);
    }

    // Preservar o contrato original de três argumentos utilizado pela janela
    // BETA e pelos chamadores existentes. Apenas a operação autorizada pelo
    // coordenador do lote pode chamar a variante interna.
    private Task ExecuteAsync(string description, Func<Task> work,
        bool quietStartup = false) =>
        ExecuteBatchAwareAsync(description, work, quietStartup, allowDuringBatch: false);

    private async Task ExecuteBatchAwareAsync(string description, Func<Task> work,
        bool quietStartup, bool allowDuringBatch)
    {
        if (!AppBatchUpdateRun.MayExecute(_busy, _batchUpdatesActive,
            allowDuringBatch) || _v269Confirming || _betaClose.IsClosing) return;
        bool appExecution=!quietStartup&&_nav.SelectedPage==V269Page.Aplicativos;
        bool cleanupExecution=!quietStartup&&_nav.SelectedPage==V269Page.Limpeza;
        if(cleanupExecution){_v269CleanupExecutionActive=true;_v269CleanupOutcome=null;}
        if(appExecution){_v269AppExecutionActive=true;_v269AppExecutionResult=null;}
        _busy = true;
        if (!quietStartup)
        {
            _programList.ActionsEnabled = false;
            UpdateBulkButtons();
            // Somente operações explícitas exibem o progresso animado.
            // Pesquisa local continua acessivel durante manutencao longa.
            _progress.Visible = true;
            _progress.Style = CanAnimateV269 ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            _progress.MarqueeAnimationSpeed = CanAnimateV269 ? 24 : 0;
        }
        _operationResult = description;
        _status.Text = description;
        if (!quietStartup)
            _nav.PresentPanel(new(V269PanelKind.Progress, "Operação em andamento", description));
        try { await work(); }
        catch (Exception ex) { if (!IsDisposed && !Disposing && !_betaClose.IsShutdown) ReportError(ex); }
        finally
        {
            _busy = false;
            if(appExecution)_v269AppExecutionActive=false;
            if(cleanupExecution)_v269CleanupExecutionActive=false;
            if (!IsDisposed && !Disposing)
            {
                if (!quietStartup)
                {
                    _progress.MarqueeAnimationSpeed = 0;
                    _progress.Style = ProgressBarStyle.Blocks;
                    _progress.Value = 0;
                    _progress.Visible = false;
                    _programList.ActionsEnabled = !_batchUpdatesActive;
                }
                _status.Text = _operationResult;
                if (!quietStartup && _nav.RightPane.Current?.Kind == V269PanelKind.Progress)
                    _nav.PresentPanel(new(V269PanelKind.Details, "Último estado",
                        _operationResult == description
                            ? "Nenhum desfecho adicional foi reportado. Consulte o registro."
                            : _operationResult));
                if(appExecution)
                {
                    var result=_v269AppExecutionResult;
                    _v269AppExecutionResult=null;
                    string outcome=result?.Description??(_operationResult==description
                        ?"Nenhum desfecho adicional foi reportado. Consulte o registro."
                        :_operationResult);
                    RecordV269AppResult(result?.Title??description,outcome,result);
                }
                if(cleanupExecution)
                {
                    var outcome=_v269CleanupOutcome;
                    _v269CleanupOutcome=null;
                    RecordV269CleanupResult(outcome?.Title??description, outcome?.Description??
                        (_operationResult==description?"Nenhum desfecho adicional foi reportado. Consulte o registro.":_operationResult),
                        outcome?.Complete==true);
                }
                UpdateBulkButtons();
                if (_checkUpdatesButton is not null && _consultaCatalogoCts is null)
                    _checkUpdatesButton.Enabled = !_batchUpdatesActive;
                if (_repairNetworkButton is not null)
                    _repairNetworkButton.Enabled = NetworkDiagnostics.SuggestTargetedRepair(_lastNetworkScan) is not null;
                RefreshWindowsRepairButton();
                RefreshWindowsUpdateInstallButton();
                if (_nav.SelectedPage == V269Page.Limpeza)
                    _ = RefreshDiskUsageAsync();
                if (_nav.SelectedPage == V269Page.Sistema && _activeSystemView == "energy")
                    _ = RefreshEnergyInfoAsync();
                if (!quietStartup) NotifyV269OperationResult();
                if (!_batchUpdatesActive) ExitAfterCurrentOperation();
            }
        }
    }

    private void ReportError(Exception ex)
    {
        Log("ERRO: " + ex.Message);
        Status("Não foi possível concluir a operação");
        if(_v269CleanupExecutionActive)
        {
            _v269CleanupOutcome=new("Limpeza e Desempenho",ex.Message,false);
            return;
        }
        if(_nav.SelectedPage==V269Page.Limpeza)
        {
            RecordV269CleanupResult("Limpeza e Desempenho",ex.Message,false);
            return;
        }
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
            var scan = await Task.Run(() =>
            {
                var result = AppServices.ScanAllInstalledApps();
                // Verificar caminhos pode envolver disco lento ou indisponivel.
                // Resolver antes de entregar o snapshot a thread da interface.
                foreach (var item in result.Items)
                    item.CanUninstall = !result.Incomplete && BundlewareReview.CanLaunchIndividual(item) &&
                        ActionableInventory.CanUninstall(item);
                return result;
            });
            items = scan.Items.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            _inventoryIncomplete = scan.Incomplete;
            // Comparação entre fotografias completas e identidades inequívocas.
            // Leitura parcial nunca substitui a base e não autoriza inferir remoção.
            var delta = _reliability.Observe(items, !_inventoryIncomplete);
            if (delta.Comparable && (delta.Added != 0 || delta.NotObserved != 0 || delta.Changed != 0))
                Log(delta.Explanation);
            else if (!delta.Comparable)
                Log(delta.Explanation);
            if (_inventoryIncomplete)
                Log("ATENÇÃO: inventário PARCIAL. Uma ou mais fontes não puderam ser consultadas; aplicativos ausentes desta lista podem continuar instalados. Clique em Atualizar e verificar para tentar novamente. Nenhum aplicativo foi removido.");
            DiagnosticTrace.Write($"REGISTRO_PROGRAMAS.LIDO quantidade={items.Count} examinados={scan.Registry.Inspected} ocultos={scan.Registry.HiddenComponents} updates_arp={scan.Registry.UpdateEntries} erros={scan.Registry.ReadFailures} duplicados={scan.Registry.Duplicates}");
            TestDiagnosticDetail.Record("INVENTARIO",
                ("total", items.Count),
                ("sem_nome", items.Count(x => string.IsNullOrWhiteSpace(x.Name))),
                ("sem_editor", items.Count(x => string.IsNullOrWhiteSpace(x.Publisher))),
                ("sem_versao", items.Count(x => string.IsNullOrWhiteSpace(x.Version))),
                ("desinstalaveis", items.Count(x => x.CanUninstall)),
                ("so_inventario", items.Count(x => x.InventoryOnly)),
                ("sem_desinstalacao", items.Count(x => !x.CanUninstall)),
                ("sem_desinstalacao_fora_flag", items.Count(x => !x.CanUninstall && !x.InventoryOnly)),
                ("parcial", scan.Incomplete ? 1 : 0));
            DiagnosticTrace.Write($"MSIX.LEITURA status={scan.Msix.Status} enumerados={scan.Msix.Seen} " +
                $"aplicativos={scan.Msix.Items.Count} adicionados={scan.Added} filtrados={scan.Msix.Filtered} falhas={scan.Msix.Failures} retidos={scan.Msix.Items.Count(x => x.Stale)} repeticao={scan.Msix.Retried} fase={scan.Msix.FailureStage} apartment={scan.Msix.Apartment}");
            int retainedMsix = scan.Msix.Items.Count(x => x.Stale);
            TestDiagnosticDetail.Record("MSIX", ("observados", scan.Msix.Items.Count - retainedMsix),
                ("retidos", retainedMsix), ("falhas", scan.Msix.Failures),
                ("parcial", scan.Msix.Failures > 0 || scan.Msix.Status != "ok" ? 1 : 0));
            if (retainedMsix > 0)
                Log($"MSIX: {retainedMsix} entradas preservadas da última leitura completa desta sessão, sem ações de atualização. Presença atual não confirmada.");
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
        _updateHistory.Clear();
        _consultaCatalogoCts?.Cancel();
        // Os desinstaladores ja foram resolvidos em segundo plano.
        // Não ocultar do inventário os programas sem ação; somente da lista.
        _installed.Clear();
        _installed.AddRange(items);
        DiagnosticTrace.Write($"APLICATIVOS.ACOES inventario={items.Count} desinstalaveis={items.Count(x => x.CanUninstall)} ocultos_sem_acao={items.Count(x => !x.CanUninstall)}");
        _available.Clear();
        _catalogoVerificado = false;
        _checkedApps.Clear();
        _multiSelectMode = false;
        _programList.MultiSelectMode = false;
        _appFilter = "Todos";
        DiagnosticTrace.Write("REGISTRO_PROGRAMAS.RENDER_INICIO");
        RenderPrograms();
        DiagnosticTrace.Write("REGISTRO_PROGRAMAS.RENDER_FIM");
        await RefreshCapabilitiesAsync(updateChecked: false);
        Log($"Inventário local: {_installed.Count} registros; {ActionableInventory.VisibleCount(_installed)} com desinstalação disponível. A lista exibida mostra somente ações.");
        Status(_inventoryIncomplete
            ? "Inventário PARCIAL: fonte de aplicativos indisponível. Use Atualizar e verificar para tentar novamente."
            : $"{ActionableInventory.VisibleCount(_installed)} aplicativos com ação disponíveis antes da consulta online");
    }

    private readonly UpdateResultHistory _updateHistory = new();

    private Task LoadUpdatesAsync(bool forceRefresh = false) => LoadUpdatesCoreAsync(forceRefresh, false);

    private async Task LoadUpdatesCoreAsync(bool forceRefresh, bool appVerification)
    {
        // Fontes HTTPS oficiais trabalham em paralelo: uma falha não apaga o resultado
        // das outras. Não executam instaladores, comandos externos, WinGet ou COM.
        if (_consultaCatalogoCts is not null || IsDisposed || _installed.Count == 0) return;
        int generation = _catalogoGeneration;
        ProgramEntry[] snapshot = _installed.ToArray();
        // Preservar o mesmo número e a mesma ordem das linhas para a UI.
        // Entradas somente leitura (jogos e MSIX nao verificados) nao
        // recebem candidatos nem receitas automáticas de atualização.
        var installed = snapshot.Select(InstalledUpdateIdentity.Create).ToArray();
        using var measurement = LocalPerformanceMetrics.Measure(PerformanceStage.Updates);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(65));
        var watch = Stopwatch.StartNew();
        _consultaCatalogoCts = cancellation;
        V269PanelPresentation? verification = null;
        bool? outcome = null;
        void PresentVerificationProgress(string message, int? percent = null)
        {
            if (!appVerification || IsDisposed || Disposing || _busy || _v269Confirming) return;
            if (verification is null && _nav.SelectedPage != V269Page.Aplicativos ||
                verification is not null && !ReferenceEquals(_nav.RightPane.Current, verification)) return;
            verification = new(V269PanelKind.Progress, "Verificar aplicativos", message, percent,
                CancelFlow: () => { if (ReferenceEquals(_consultaCatalogoCts, cancellation)) cancellation.Cancel(); },
                FlowPage: V269Page.Aplicativos, CancelLabel: "Cancelar consulta");
            _nav.PresentPanel(verification);
        }
        int totalSources = Math.Max(1, OfficialReleaseSources.ApplicableSourceCount(installed) + 2);
        if (!_busy)
        {
            // Consulta somente leitura: duração incerta, indicar atividade sem inventar percentual.
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Maximum = totalSources;
            _progress.Value = 0;
            _progress.Visible = true;
            Status("Consultando fontes de versões…");
            PresentVerificationProgress("Consultando fontes de versões…");
        }
        RenderTitle();
        if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar consulta";
        DiagnosticTrace.Write("MULTIFONTES.CONSULTA_INICIO fontes=ruckzuck,github,mozilla");
        Log("Consultando fontes de versões por HTTPS: RuckZuck, fabricantes e índices comunitários.");
        Log(WingetConsultativeProvider.ProductionStatus);
        try
        {
            using var primary = new RuckZuckCatalog();
            using var official = new OfficialReleaseSources();
            int completedSources = 0;
            var sourceProgress = new Progress<ProviderDiagnostic>(diagnostic =>
            {
                if (IsDisposed || generation != _catalogoGeneration ||
                    !ReferenceEquals(_consultaCatalogoCts, cancellation) || cancellation.IsCancellationRequested) return;
                completedSources++;
                if (!_busy)
                {
                    _progress.Maximum = totalSources;
                    _progress.Value = Math.Min(completedSources, totalSources);
                    string message = $"Fontes concluídas: {completedSources}/{totalSources} · {diagnostic.Provider}: {diagnostic.Status}";
                    Status(message);
                    PresentVerificationProgress(message, Math.Min(completedSources, totalSources) * 100 / totalSources);
                }
            });
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
                finally { ((IProgress<ProviderDiagnostic>)sourceProgress).Report(new("ruckzuck", "concluida", timer.ElapsedMilliseconds)); }
            }
            async Task<AutomaticDiscoveryResult> ReadAutomaticAsync()
            {
                var results = await ecosystemProviders.DiscoverAsync(new(forceRefresh), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                EcosystemInventory.Associate(snapshot, results);
                foreach (var source in results)
                    DiagnosticTrace.Write($"ECOSSISTEMA.CONSULTA fonte={source.Provider} estado={source.Status} entradas={source.Installations.Count} ms={source.Milliseconds}");
                var discovered=await AutomaticUpdateDiscovery.QueryAsync(snapshot,installed,cancellation.Token);
                ((IProgress<ProviderDiagnostic>)sourceProgress).Report(discovered.Diagnostic);
                foreach(var outcome in discovered.Outcomes ?? new Dictionary<string,int>())
                    DiagnosticTrace.Write($"CATALOGO.ETAPA motivo={outcome.Key} quantidade={outcome.Value}");
                return discovered;
            }
            var autoTask=ReadAutomaticAsync();
            var ruckTask = ReadRuckZuckAsync();
            var officialTask = official.QueryAsync(installed, cancellation.Token, forceRefresh, sourceProgress);
            await Task.WhenAll(ruckTask, officialTask, autoTask);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != _catalogoGeneration || IsDisposed) return;
            var rz = await ruckTask;
            var others = await officialTask;
            var automatic = await autoTask;
            var freshMerged = await Task.Run(
                () => MultiSourceMatcher.Merge(installed, rz.Matches, others.Findings.Concat(automatic.Findings)
                    .Concat(EcosystemInventory.Findings(snapshot, installed)).ToArray()), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != _catalogoGeneration || IsDisposed) return;
            bool partial = rz.Matches is null || others.Diagnostics.Any(x => x.Status != "ok") || automatic.Diagnostic.Status!="ok";
            var merged = _updateHistory.Apply(installed, freshMerged, partial);
            AplicarCandidatos(merged);
            AutomaticUpdateDiscovery.Attach(automatic);
            await RefreshCapabilitiesAsync(updateChecked: true, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation != _catalogoGeneration || IsDisposed) return;
            Log("Catálogo Microsoft: "+automatic.Plans.Count(p => !p.Installer.Interactive)+" contrato(s) silencioso(s), "+
                automatic.Plans.Count(p => p.Installer.Interactive)+" supervisionado(s); "+
                (automatic.Findings.Count - automatic.Plans.Count)+" versão(ões) adicionais somente consultivas; catálogo "+automatic.Diagnostic.Status+".");
            foreach (var (sourceId, sourceCode) in new[] { ("ea-desktop", 1), ("fancontrol", 2),
                ("jellyfin-server", 3), ("klite-mega", 4), ("gameinput-winget", 5), ("plex", 6),
                ("proton-pass-desktop", 7), ("proton-pass-winget", 8), ("proton-pass-windows", 9) })
            {
                // IDs internos fixos e contagens; nenhum nome/editor do inventário no diagnóstico.
                var ruleId = sourceId is "proton-pass-winget" or "proton-pass-windows" ? "proton-pass-desktop" : sourceId;
                var indices = Enumerable.Range(0, installed.Length).Where(i =>
                    ApplicationIdentityCatalog.Resolve(installed[i].Name, installed[i].Publisher)?.Id == ruleId).ToArray();
                if (indices.Length == 0)
                    TestDiagnosticDetail.Record("COBERTURA_ATUALIZACAO", ("fonte", sourceCode), ("etapa", 0), ("quantidade", 0));
                foreach (var stages in indices.GroupBy(i => OfficialReleaseSources.CoverageStage(installed[i], sourceId, others, merged[i])))
                    TestDiagnosticDetail.Record("COBERTURA_ATUALIZACAO", ("fonte", sourceCode), ("etapa", stages.Key), ("quantidade", stages.Count()));
            }
            foreach (var group in merged.GroupBy(m => m.DiagnosticReason))
            {
                DiagnosticTrace.Write($"MULTIFONTES.COBERTURA motivo={group.Key} quantidade={group.Count()}");
                Log($"Cobertura: {group.Count()} — {group.Key}.");
            }
            Log("A consulta HTTPS e o WinGet podem ter coberturas diferentes. O diagnóstico registra apenas contagens e motivos, sem nomes de aplicativos.");
            _catalogoVerificado = true;
            RenderPrograms();
            int candidates = merged.Count(x => x.State == MatchState.Candidate);
            int uncertain = merged.Count(x => Matching.IsUncertain(x.State));
            int superseded = merged.Count(x => x.State == MatchState.SupersededRegistration);
            int officialCandidates = merged.Count(x => x.State == MatchState.Candidate &&
                x.Provider != "RuckZuck");
            TestDiagnosticDetail.Record("ATUALIZACOES",
                ("avaliados", merged.Count), ("candidatos", candidates),
                ("incertos", uncertain), ("registro_antigo", superseded),
                ("fontes", others.Diagnostics.Count), ("ruckzuck_ok", rz.Matches is null ? 0 : 1));
            if (superseded > 0)
                Log($"Inventário: {superseded} registro(s) antigo(s) correlacionados a versões recentes da mesma instalação.");
            if (rz.Matches is null)
                Log($"RuckZuck indisponível ({rz.Error}); outras fontes continuam utilizáveis.");
            else
                Log($"RuckZuck: {rz.Count} pacotes ({(rz.Cached ? "cache" : "HTTPS")}, {rz.NetworkMs} ms); comparação {rz.CompareMs} ms.");
            foreach (var diagnostic in others.Diagnostics)
                Log($"Fonte oficial {diagnostic.Provider}: {diagnostic.Status}, {diagnostic.Milliseconds} ms ({(diagnostic.UsedMemoryCache ? "cache" : "HTTPS")}).");
            DiagnosticTrace.Write($"MULTIFONTES.CONSULTA_OK instalados={installed.Length} candidatos={candidates} " +
                $"fontes_oficiais={officialCandidates} reconhecidos={others.Findings.Count} " +
                $"fontes_consultadas={others.Diagnostics.Count} fontes_ok={others.Diagnostics.Count(x => x.Status == "ok")} " +
                $"incertos={uncertain} ruckzuck_entradas={rz.Count} " +
                $"ruckzuck_ms={rz.NetworkMs} comparacao_ms={rz.CompareMs} total_ms={watch.ElapsedMilliseconds}");
            Log("Execução direta exige receita verificada ou descoberta autenticada, com conferência do arquivo e da instalação.");
            int sourceFailures = others.Diagnostics.Count(x => x.Status != "ok") + (rz.Matches is null ? 1 : 0) +
                (automatic.Diagnostic.Status != "ok" ? 1 : 0);
            int conflicts = merged.Count(m => m.HasSourceConflict);
            int requiringReview = merged.Count(m => m.State == MatchState.Candidate && m.RequiresReview);
            if (conflicts > 0) Log($"Fontes divergentes: {conflicts} resultado(s); instalação direta bloqueada, detalhes na dica da linha.");
            outcome = sourceFailures == 0;
            Status($"Consulta {(sourceFailures > 0 ? "parcial" : "concluída")}: {candidates} possíveis atualizações" +
                (requiringReview > 0 ? $" ({requiringReview} em revisão)" : "") +
                $" · {sourceFailures} fonte(s) indisponível(is)");
        }
        catch (OperationCanceledException)
        {
            if (generation != _catalogoGeneration || IsDisposed) return;
            AplicarCandidatos(_updateHistory.Apply(installed,
                installed.Select(x => new UpdateMatch(x, null, MatchState.Unmatched)).ToArray(), true));
            RenderPrograms();
            DiagnosticTrace.Write("MULTIFONTES.CONSULTA_CANCELADA");
            Log("Consulta cancelada ou esgotada; resultados anteriores preservados em revisão quando disponíveis.");
            outcome = false;
            Status("Consulta cancelada ou esgotada; resultados anteriores exigem revalidação");
        }
        catch (Exception ex)
        {
            DiagnosticTrace.Failure("MULTIFONTES.CONSULTA_FALHA", ex);
            if (generation != _catalogoGeneration || IsDisposed) return;
            AplicarCandidatos(_updateHistory.Apply(installed,
                installed.Select(x => new UpdateMatch(x, null, MatchState.Unmatched)).ToArray(), true));
            RenderPrograms();
            Log("Falha geral na consulta multifontes (" + ex.GetType().Name + ").");
            outcome = false;
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
                    _progress.Maximum = 100;
                    _progress.Visible = false;
                }
                RenderTitle();
                if (_checkUpdatesButton is not null)
                    _checkUpdatesButton.Text = "Verificar";
                if (appVerification && outcome is bool complete && !Disposing)
                {
                    var entry = new V269AppPreviousResult(Guid.NewGuid(), "Verificar aplicativos", _operationResult, complete);
                    _v269AppPreviousResults.Insert(0, entry);
                    if (_v269AppPreviousResults.Count > 4) _v269AppPreviousResults.RemoveAt(4);
                    if (verification is not null && ReferenceEquals(_nav.RightPane.Current, verification))
                    {
                        _nav.PresentPanel(new(V269PanelKind.Result, entry.Title, entry.Description,
                            FlowResult: new(V269Page.Aplicativos, complete, () =>
                            {
                                _nav.PresentPanel(null);
                                if (_programList.SelectedItem is { } item) PresentV269AppDetails(item);
                            }, entry.Id)));
                    }
                    else _nav.RightPane.RefreshAppOperationHistory();
                }
                else if (verification is not null && ReferenceEquals(_nav.RightPane.Current, verification))
                {
                    _nav.PresentPanel(null);
                    if (_programList.SelectedItem is { } item) PresentV269AppDetails(item);
                }
            }
        }
    }

    private void BeginStartupChecks()
    {
        BeginBackgroundMemoryMonitoring();
        if (!BetaRuntimePolicy.AllowsAutomaticOnlineChecks)
        {
            Log("BETA LOCAL: consultas de versões disponíveis por ação explícita.");
            return;
        }
        Log("Inicialização: consultas de aplicativos em segundo plano; Windows Update somente pelo botão Buscar atualizações.");
        if (_startupOnlineProbe is not null) _startupOnlineProbe();
        else _ = RunStaggeredStartupChecksAsync();
    }

    private async Task RunStaggeredStartupChecksAsync()
    {
        // WUA exige comando explícito; não consultar ao abrir o aplicativo.
        try
        {
            await LoadUpdatesAsync();
        }
        catch (Exception ex)
        {
            // Consultas sao auxiliares. Erros nao devem encerrar a janela.
            if (!IsDisposed && !Disposing)
            {
                DiagnosticTrace.Failure("INICIALIZACAO.CONSULTAS_FALHA", ex);
                Log("Inicialização: uma consulta não pôde ser concluída (" +
                    ex.GetType().Name + ").");
            }
        }
    }

    private void AplicarCandidatos(IReadOnlyList<UpdateMatch> matches)
    {
        _available.Clear();
        for (int i = 0; i < _installed.Count; i++)
        {
            var item = _installed[i];
            var match = matches[i];
            item.ComparisonState = match.State;
            item.UpdateRequiresReview = match.RequiresReview;
            item.UpdateEvidence = match.EvidenceLabel + "\n" + match.EvidenceDetails;
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
            entry.UpdateRequiresReview = false;
            entry.UpdateEvidence = "";
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
        IEnumerable<ProgramEntry> filtered = _installed.Where(entry =>
            ActionableInventory.ShouldShowInMainList(entry, _appFilter) && (query.Length == 0 ||
            string.Join(" ", entry.Name, entry.Version, entry.Publisher, entry.PackageId, entry.AvailableVersion,
                entry.CatalogIdentity, entry.CatalogDisplayName, entry.InventorySource,
                entry.MsiProductCode)
                .Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        filtered = _appFilter switch
        {
            "Com atualização" => filtered.Where(x => x.IsUpdate),
            "Selecionados" => filtered.Where(x => _checkedApps.Contains(x)),
            _ => filtered
        };
        var match = V269AppOrdering.Sort(filtered,_v269AppSort,_v269ManualOrder);
        _programList.SetItems(match);
        _emptyState.Text = query.Length > 0
            ? "Nenhum aplicativo corresponde à pesquisa."
            : _appFilter == "Selecionados" ? "Nenhum aplicativo marcado."
            : _consultaCatalogoCts is not null
                ? "Consulta online ativa; inventário local vazio."
                : "Nenhum aplicativo identificado. Se o inventário estiver parcial, tente novamente.";
        _emptyState.Visible = false; // a tabela conserva o cabeçalho, inclusive sem linhas
        _programList.EmptyMessage=_emptyState.Text;
        RenderTitle();
        UpdateBulkButtons();
        ApplyFilterAppearance();
    }

    private void MoveV269App(ProgramEntry item,ProgramEntry target)
    {
        if(_busy||_batchUpdatesActive||_v269Confirming||_betaClose.IsClosing||_multiSelectMode||
            _inventoryIncomplete||_consultaCatalogoCts is not null)return;
        var order=_v269AppSort==V269AppSort.Manual?_v269ManualOrder:
            V269AppOrdering.Sort(_installed,_v269AppSort,_v269ManualOrder).Select(V269AppOrdering.Key)
                .Where(k=>k.Length==64).Distinct(StringComparer.Ordinal).ToArray();
        var changed=V269AppOrdering.Move(_installed,order,item,target);
        if(changed is null)return;
        try
        {
            if(_v269OrderLive)V269AppOrderStore.Write(V269AppOrderStore.PathForUser,changed);
            _v269ManualOrder=changed;_v269AppSort=V269AppSort.Manual;
            _v269AppSortChoice.SelectedIndex=(int)V269AppSort.Manual;
            RenderPrograms();
            _programList.SelectedIndex=_programList.Items.ToList().FindIndex(x=>ReferenceEquals(x,item));
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException)
        {Status("Não foi possível salvar a ordem manual; ordem anterior preservada.");}
    }

    internal static string InventoryCoverageTitle(bool incomplete) =>
        incomplete ? "Inventário PARCIAL" : "Inventário completo";

    private void RenderTitle()
    {
        var summary = ActionableInventory.Summarize(_installed);
        int updates = summary.Updates;
        int displayed = summary.Actionable;
        string windowsStatus = _windowsUpdateCheckingMessage();
        string full = $"{InventoryCoverageTitle(_inventoryIncomplete)}: {summary.Total} · Com ação: {displayed} · Sem ação disponível: {summary.WithoutAction} · Desinstaláveis: {summary.Uninstallable} · Possíveis atualizações: {(_catalogoVerificado ? updates.ToString() : "não verificadas")}" +
            (_inventoryIncomplete ? " · Fonte do Windows indisponível: ausência na lista não comprova desinstalação. Atualizar e verificar para tentar novamente." : "") +
            (_consultaCatalogoCts is not null ? " · Verificando..." : "") +
            windowsStatus + (_multiSelectMode ? $" · Marcados: {_checkedApps.Count}" : "");
        // Cabeçalho permanece de uma linha sem sobrepor os quatro filtros.
        // Todas as informacoes podem ser lidas pelo tooltip da legenda.
        int width = _listTitle.ClientSize.Width;
        // Preferir informacao essencial completa. Os detalhes adicionais,
        // inclusive o Windows Update, ficam acessiveis na dica integral.
        string count = _catalogoVerificado ? updates.ToString() : "—";
        _listTitle.Text = _inventoryIncomplete
            ? (width < 220 ? $"Apps: {summary.Total} · PARCIAL" : $"Apps: {summary.Total} · inventário PARCIAL")
            : width < 220 ? $"Apps: {summary.Total}"
            : $"Apps: {summary.Total} · Possíveis atualizações: {count}";
        _listTitle.AccessibleDescription = full;
        _programList.AccessibleDescription = full;
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
            (_allFilterButton, "Todos"), (_selectedFilterButton, "Selecionados")
        })
        {
            if (button is null) continue;
            if(button is V269AppToolbarButton filter)filter.FilterSelected=_appFilter==name;
            else
            {
                button.BackColor = _appFilter == name ? Color.FromArgb(43, 66, 89) : Color.FromArgb(43, 56, 70);
                button.ForeColor = _appFilter == name ? Accent : Ink;
            }
            button.Invalidate();
        }
    }

    private void ToggleMultiSelection()
    {
        if (_busy || _batchUpdatesActive || _betaClose.IsClosing) return;
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
        if (_busy || _batchUpdatesActive || _betaClose.IsClosing) return;
        _checkedApps.Clear();
        if (_appFilter == "Selecionados") _appFilter = "Todos";
        RenderPrograms();
    }

    private void UpdateBulkButtons()
    {
        if (_selectManyButton is not null)
        {
            _selectManyButton.Text = _multiSelectMode ? "Sair da seleção" : "Selecionar vários";
            _selectManyButton.Enabled = !_busy && !_batchUpdatesActive;
        }
        if (_updateAllButton is not null)
        {
            _updateAllButton.Text = _batchUpdatesActive
                ? (_batchStopRequested ? "Parando lote" : "Parar lote") : "Atualizar todos";
            var eligible = _batchUpdatesActive || _busy || !_catalogoVerificado || _inventoryIncomplete
                ? 0 : AppBatchUpdatePolicy.Select(_installed, true, true).Length;
            _updateAllButton.Enabled = _batchUpdatesActive ? !_batchStopRequested :
                !_busy && !_betaClose.IsClosing && _consultaCatalogoCts is null &&
                eligible > 0;
            _updateAllButton.AccessibleDescription = _batchUpdatesActive
                ? "Interromper próximas atualizações depois da operação atual."
                : $"Atualizar todos os elegíveis: {eligible} aplicativo(s) com receita direta verificada.";
        }
        if (_bulkUninstallButton is not null)
            _bulkUninstallButton.Enabled = !_busy && !_batchUpdatesActive &&
                _multiSelectMode && _checkedApps.Count > 0;
        if (_clearSelectionButton is not null)
            _clearSelectionButton.Enabled = !_busy && !_batchUpdatesActive && _checkedApps.Count > 0;
    }

    // A fila reutiliza o instalador individual; não autoriza Store, Terminal,
    // catálogos consultivos, novos alvos ou tentativas automáticas de recuperação.
    private async Task UpdateAllApprovedAsync()
    {
        if (_batchUpdatesActive)
        {
            _batchStopRequested = true;
            UpdateBulkButtons();
            Status("A fila será interrompida após a atualização atual; nenhum novo instalador será iniciado.");
            return;
        }
        if (_busy || _betaClose.IsClosing || _consultaCatalogoCts is not null ||
            _inventoryIncomplete || !_catalogoVerificado)
        {
            Status("Atualizar todos exige inventário completo e catálogo já consultado. Use Atualizar e verificar.");
            return;
        }
        var candidates = AppBatchUpdatePolicy.Select(_installed, true, true);
        if (candidates.Length == 0)
        {
            Status("Nenhuma atualização direta elegível; outras ofertas exigem revisão individual.");
            return;
        }
        if (candidates.Length > AppBatchUpdateRun.MaxCandidates)
        {
            Status("Muitos candidatos diretos para a confirmação individualizada do lote; revise as atualizações.");
            return;
        }
        int suggested = _installed.Count(item => item.IsUpdate);
        string preview = AppBatchUpdateRun.Preview(candidates, suggested);
        if (!await ConfirmV269Async("Confirmar Atualizar todos", preview))
        {
            Log("Atualização em lote cancelada antes de qualquer download.");
            return;
        }

        if (_busy || _batchUpdatesActive || _betaClose.IsClosing || _consultaCatalogoCts is not null ||
            _inventoryIncomplete || !_catalogoVerificado) return;
        _batchUpdatesActive = true;
        _batchStopRequested = false;
        _programList.ActionsEnabled = false;
        if (_checkUpdatesButton is not null) _checkUpdatesButton.Enabled = false;
        UpdateBulkButtons();
        AppBatchUpdateSummary? summary = null;
        try
        {
            summary = await AppBatchUpdateRun.RunAsync(candidates,
                async candidate =>
                {
                    ProgramEntry? current = AppBatchUpdatePolicy.Revalidate(
                        candidate, _installed, !_inventoryIncomplete, _catalogoVerificado);
                    if (current is null)
                    {
                        Log("Lote interrompido: identidade, versão ou elegibilidade alterada antes da tentativa.");
                        return AppBatchUpdateStepResult.BlockedBeforeStart;
                    }
                    Status("Atualizar todos: " + candidate.Name + " → " + candidate.TargetVersion);
                    _batchLastAttempt = null;
                    await UpdateThirdPartyAsync(current, fromApprovedBatch: true);
                    UpdateOperationSnapshot? receipt = _batchLastAttempt;
                    if (receipt?.Progress.Stage == UpdateStage.Confirmed)
                    {
                        Log("Lote: versão confirmada para " + candidate.Name + ".");
                        return AppBatchUpdateStepResult.Confirmed;
                    }
                    Log("Lote interrompido após resultado não confirmado: " +
                        (receipt?.Details ?? "nenhum recibo confirmável") +
                        ". Nenhuma nova tentativa automática.");
                    return AppBatchUpdateStepResult.Inconclusive;
                },
                () => _batchStopRequested || IsDisposed || Disposing ||
                    _betaClose.IsClosing || _busy || _consultaCatalogoCts is not null,
                ex =>
                {
                    DiagnosticTrace.Failure("ATUALIZACAO_LOTE.FALHA", ex);
                    Log("Lote interrompido após exceção: " + ex.GetType().Name);
                });
        }
        finally
        {
            bool requestedStop = _batchStopRequested;
            _batchUpdatesActive = false;
            _batchStopRequested = false;
            _batchLastAttempt = null;
            if (!IsDisposed && !Disposing)
            {
                _programList.ActionsEnabled = !_busy;
                if (_checkUpdatesButton is not null && _consultaCatalogoCts is null)
                    _checkUpdatesButton.Enabled = !_busy;
                UpdateBulkButtons();
                string result = summary is null
                    ? "Atualizar todos: interrupção sem resumo conclusivo; nenhuma repetição automática."
                    : summary.Describe(requestedStop);
                Status(result);
                Log(result);
                ExitAfterCurrentOperation();
            }
        }
    }

    private void ShowUpdateGuidance(ProgramEntry entry)
    {
        if (!entry.IsUpdate || _busy) return;
        string message = VerifiedUpdaters.UpdateAvailabilityText(entry,entry.Capabilities?.Update.Reason??"") + " " + (entry.InventoryOnly
            ? "Componente do Windows/Store: abra Configurações ou Microsoft Store e procure atualizações oficiais."
            : "Sem instalador validado: abra o próprio aplicativo, procure Ajuda ou Configurações e use a função oficial de atualização.");
        Log("Orientação para " + entry.Name + " · " + message);
        Status("Orientações de atualização exibidas no histórico de operações.");
    }

    private void OpenOriginalPackageManager(ProgramEntry entry)
    {
        if (_busy || _betaClose.IsClosing || !ActionableInventory.CanManageExternally(entry)) return;
        var receipt = EcosystemInventory.Get(entry);
        if (receipt is null) return;
        // Open only fixed project documentation. No script, package command or
        // user-controlled path is passed to a shell, even after clicking.
        string url = receipt.Identity.Provider == "chocolatey"
            ? "https://docs.chocolatey.org/en-us/choco/commands/" : "https://scoop.sh/";
        Log(EcosystemInventory.Describe(entry));
        Status("Gerencie este pacote na ferramenta original; o GF abriu as orientações oficiais.");
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        { Log("Orientações do gerenciador indisponíveis: " + ex.GetType().Name); }
    }

    // Ação universal: não inventar UninstallString nem forçar remoção de
    // componentes protegidos. O Windows/gerenciador original decide a operação.
    private void OpenManualAppUninstall(ProgramEntry entry)
    {
        if (_busy || _betaClose.IsClosing || entry.CanUninstall) return;
        string explanation = ActionableInventory.CanManageExternally(entry)
            ? "Este pacote é administrado por outro gerenciador."
            : entry.InventorySource.StartsWith("Biblioteca de jogos:",
                StringComparison.OrdinalIgnoreCase)
            ? "Este jogo pertence a uma biblioteca externa. Remova-o pela biblioteca do launcher."
            : "O GF não possui um desinstalador validado para esta entrada.";
        string message = "Desinstalar " + entry.Name + "?" +
            Environment.NewLine + Environment.NewLine + explanation +
            Environment.NewLine + "O GF não executará uma remoção automática." +
            Environment.NewLine + "Deseja abrir o gerenciamento adequado e decidir manualmente?";
        if (MessageBox.Show(this, message, "GF — confirmar gerenciamento",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        if (ActionableInventory.CanManageExternally(entry))
        {
            OpenOriginalPackageManager(entry);
            return;
        }
        if (entry.InventorySource.StartsWith("Biblioteca de jogos:",
            StringComparison.OrdinalIgnoreCase))
        {
            Log("Desinstalação do jogo " + entry.Name +
                ": abra a biblioteca do launcher correspondente. Nenhum pacote removido.");
            Status("Desinstale este jogo pela biblioteca do launcher original.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:appsfeatures", UseShellExecute = true
            });
            Status("Configurações abertas. Selecione o aplicativo; o Windows decidirá se pode removê-lo.");
            Log("Gerenciamento manual solicitado para " + entry.Name +
                ". O GF não executou desinstalador.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log("Gerenciamento do Windows indisponível: " + ex.GetType().Name);
            Status("Abra Configurações > Aplicativos instalados manualmente.");
        }
    }

    private static MsixRemovalRequest MsixRequest(ProgramEntry entry) =>
        entry.MsixRemovalIdentity;

    private static bool CanRemoveMsix(ProgramEntry entry) =>
        entry.CanUninstallMsix;

    // C1: somente aplicativos MSIX do usuario atual, nunca Microsoft/frameworks
    // ou entradas com identidade parcial. Executar no mesmo fluxo serial do GF.
    private async Task UninstallMsixAsync(ProgramEntry entry)
    {
        // Falhas de Registro/jogos não revogam uma fonte MSIX íntegra;
        // seguem obrigatórias duas consultas frescas da família exata.
        if (_betaClose.IsClosing || !_installed.Contains(entry) ||
            !CanRemoveMsix(entry) ||
            _uninstallOperations.Find(entry)?.PreventRetry == true)
        {
            Status("MSIX: operação indisponível ou inventário incompleto.");
            return;
        }
        var request = MsixRequest(entry);
        LocalMsixResult ReadFreshInventory() =>
            LocalMsixInventory.ReadCurrentUserFamilyFresh(request.PackageFamilyName);
        SetUninstallProgress(entry, "preparing");
        var initial = await Task.Run(ReadFreshInventory);
        if (!MsixUninstall.CanStart(request, initial))
        {
            SetUninstallProgress(entry, "failed", "Identidade ou leitura alterada; remoção não iniciada.");
            Status("MSIX: pacote alterado ou consulta parcial. Atualize a lista antes de tentar novamente.");
            return;
        }
        SetUninstallProgress(entry, "awaiting_consent");
        bool consent = await ConfirmV269Async("Confirmar desinstalação MSIX",
            "Remover o aplicativo MSIX da sua conta do Windows?\n\n" +
            entry.Name + "\nVersão: " + entry.Version + "\nEditor: " + entry.Publisher +
            "\nFamília do pacote: " + request.PackageFamilyName +
            "\n\nO Windows pode remover dependências sem uso e dados do aplicativo. " +
            "Esta ação não possui desfazer automático e não pode ser interrompida " +
            "com segurança após iniciar.\n\nDeseja continuar?");
        if (!consent)
        {
            SetUninstallProgress(entry, "cancelled");
            Status("MSIX: remoção cancelada antes de iniciar.");
            return;
        }
        if (!DiagnosticTrace.Write("MSIX.REMOCAO etapa=consentimento estado=confirmado"))
        {
            SetUninstallProgress(entry, "failed", "Diário indisponível; remoção não iniciada.");
            Status("MSIX: diário indisponível. Nenhuma remoção iniciada.");
            return;
        }
        SetUninstallProgress(entry, "working");
        Status("MSIX: aguardando o Windows remover o aplicativo selecionado...");
        var outcome = await Task.Run(() => MsixUninstall.ExecuteAsync(request,
            ReadFreshInventory,
            MsixUninstall.RemoveCurrentUserAsync));
        string code = outcome.ErrorCode is int error ? " codigo=" + error : "";
        bool audited = DiagnosticTrace.Write("MSIX.REMOCAO etapa=resultado estado=" +
            outcome.State.ToString().ToLowerInvariant() +
            " motivo=" + outcome.Reason + code);
        var feedback = MsixUninstall.DescribeForUi(outcome, audited);
        SetUninstallProgress(entry, feedback.Stage, feedback.Explanation);
        Status(feedback.Explanation);
        Log(feedback.Explanation);
        if (feedback.RefreshInventory)
        {
            try { await RefreshCatalogAsync(); }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("MSIX.RECARREGAR_FALHA", ex);
                Log("MSIX: não foi possível renovar a lista após a confirmação.");
            }
        }
        ShowUninstallResult(entry.Name + "\n" + feedback.Explanation +
            "\nNenhuma limpeza residual ou remoção de outros usuários foi executada pelo GF.");
    }

    private void OpenWindowsInstalledApps(ProgramEntry entry)
    {
        if (_busy || _betaClose.IsClosing || !ActionableInventory.CanManageWithWindows(entry)) return;
        try
        {
            // URI oficial fixa; nunca interpolar PackageFamilyName nem argumentos.
            // O usuário precisa escolher o aplicativo dentro das Configurações.
            Process.Start(new ProcessStartInfo { FileName = "ms-settings:appsfeatures", UseShellExecute = true });
            Log("Configurações > Aplicativos instalados aberta para gerenciamento manual de MSIX. O GF não executou desinstalação.");
            Status("Localize o aplicativo nas Configurações do Windows e escolha se deseja removê-lo.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Log("Não foi possível abrir Aplicativos instalados: " + ex.GetType().Name);
            Status("Abra Configurações > Aplicativos instalados manualmente.");
        }
    }

    private void OpenMicrosoftStore(ProgramEntry entry)
    {
        if (_busy || _betaClose.IsClosing) return;
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
        if (_busy || _betaClose.IsClosing) return;
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

    private async Task RefreshInventoryAfterUpdateAsync(string previousFilter, UpdateOperation operation)
    {
        if (IsDisposed || Disposing) return;
        try
        {
            await LoadInstalledAsync();
            if (IsDisposed || Disposing) return;
            if (operation.Snapshot.Progress.Stage == UpdateStage.Confirmed &&
                operationOrigins.TryGetValue(operation.Snapshot.Id, out var origin) && VerifiedUpdaters.Resolve(origin) is { } proven)
                _updateOperations.BindConfirmedInstallation(proven, origin, _installed, operation);
            await LoadUpdatesAsync();
            if (IsDisposed || Disposing) return;
            _appFilter = _catalogoVerificado ? previousFilter : "Todos";
            RenderPrograms();
        }
        catch (Exception ex)
        {
            DiagnosticTrace.Failure("ATUALIZACAO.RECARGA_FALHA", ex);
            Log("Não foi possível renovar a lista; o resultado da atualização foi preservado.");
        }
        finally
        {
            if (!IsDisposed && !Disposing)
                Status(operation.Snapshot.Details + (_catalogoVerificado ? " · Lista consultada." : " · Catálogo indisponível."));
        }
    }

    private readonly Dictionary<Guid, ProgramEntry> operationOrigins = new();

    private UpdateOperation? BeginUpdateOperation(ProgramEntry entry, string target)
    {
        var operation = _updateOperations.Begin(entry, target);
        if (operation is null)
        {
            Status("Atualização já iniciada, pendente ou sem identidade suficiente. Confira a versão antes de tentar novamente.");
            return null;
        }
        if (operationOrigins.Count >= 256) operationOrigins.Remove(operationOrigins.Keys.First());
        operationOrigins[operation.Snapshot.Id] = entry;
        _programList.RefreshOperations();
        return operation;
    }

    private void ReportV269AppDownload(UpdateOperation operation, ProgramEntry entry,
        UpdateProgress progress, CancellationTokenSource source)
    {
        operation.Report(progress);
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        void Present()
        {
            if (IsDisposed || Disposing || !_busy || !_v269AppExecutionActive ||
                !ReferenceEquals(_installerDownloadCts, source) || source.IsCancellationRequested) return;
            var state = operation.Snapshot;
            if (!state.Active) return;
            _nav.PresentPanel(new(V269PanelKind.Progress, "Atualizar " + entry.Name, state.Text,
                state.Progress.Stage == UpdateStage.Downloading ? state.Progress.Percent : null,
                CancelFlow: () => { if (ReferenceEquals(_installerDownloadCts, source)) source.Cancel(); },
                FlowPage: V269Page.Aplicativos, CancelLabel: "Cancelar download"));
        }
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(Present)); }
            catch (InvalidOperationException) { }
        }
        else Present();
    }

    private void FinishV269AppDownload(ProgramEntry entry)
    {
        _installerDownloadCts = null;
        if (!IsDisposed && !Disposing && _nav.RightPane.Current is
            { Kind: V269PanelKind.Progress, FlowPage: V269Page.Aplicativos, CancelLabel: "Cancelar download" })
            _nav.PresentPanel(new(V269PanelKind.Progress, "Atualizar " + entry.Name, _operationResult));
    }

    private void RecordV269UpdateOutcome(ProgramEntry entry, UpdateOperationSnapshot state)
    {
        if (!_v269AppExecutionActive || state.Active || IsDisposed || Disposing) return;
        _v269AppExecutionResult = new(V269PanelKind.Result, "Atualizar " + entry.Name, state.Details,
            FlowResult: new(V269Page.Aplicativos, state.Progress.Stage == UpdateStage.Confirmed, () =>
            {
                _nav.PresentPanel(null);
                if (_programList.SelectedItem is { } item) PresentV269AppDetails(item);
            }));
    }

    private CancellationTokenSource? _deploymentWaitCts;

    private async Task UpdateWindowsTerminalAsync(ProgramEntry entry)
    {
        if (_busy || _betaClose.IsClosing || !_installed.Contains(entry)) return;
        TerminalUpdateRecipe? recipe = WindowsTerminalUpdater.Resolve(entry);
        if (recipe is null)
        {
            Status("Esta versao do Windows Terminal nao possui pacote GitHub validado.");
            return;
        }
        if (!await ConfirmV269Async("Atualizar Windows Terminal",
            $"Preparar Windows Terminal {recipe.Version} a partir da fonte oficial? O GF verificará a identidade do pacote e o Windows poderá solicitar autorização.") ||
            !_installed.Contains(entry) || WindowsTerminalUpdater.Resolve(entry) != recipe) return;
        string previousFilter = _appFilter;
        await ExecuteAsync("Preparando atualizacao oficial do Windows Terminal...", async () =>
        {
            var operation = BeginUpdateOperation(entry, recipe.Version);
            if (operation is null) return;
            bool installerStarted = false;
            string folder = Path.Combine(Path.GetTempPath(), "GerenciadorFinal-Terminal-" + Guid.NewGuid().ToString("N"));
            bool deploymentPhase = false;
            try
            {
                using var wait = new CancellationTokenSource();
                _deploymentWaitCts = wait;
                if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar espera";
                Version? before = await WindowsTerminalUpdater.CurrentUserVersionAsync(wait.Token);
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
                DiagnosticTrace.Write("TERMINAL_GITHUB.INICIO");
                string bundle;
                using (var downloadCts = new CancellationTokenSource(TimeSpan.FromMinutes(6)))
                {
                    _installerDownloadCts = downloadCts;
                    if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar download";
                    try
                    {
                        bundle = await Task.Run(() => WindowsTerminalUpdater.DownloadAndVerifyAsync(
                            recipe, folder, Status, downloadCts.Token, progress => ReportV269AppDownload(operation, entry, progress, downloadCts)));
                    }
                    finally
                    {
                        FinishV269AppDownload(entry);
                        if (!IsDisposed && _checkUpdatesButton is not null)
                            _checkUpdatesButton.Text = "Cancelar espera";
                    }
                }
                DiagnosticTrace.Write("TERMINAL_GITHUB.MSIX_VALIDADO");
                // O fechamento e a confirmacao acontecem APOS verificar o pacote
                // e ANTES de acionar o mecanismo de instalacao do Windows.
                operation.Report(new(UpdateStage.Revalidating));
                bool closed = await UpdateProcessCoordinator.EnsureClosedAsync(
                    UpdateProcessTarget.WindowsTerminal,
                    question => DarkConfirmDialog.Confirm(this, question, "Fechar Windows Terminal para atualizar") == DialogResult.Yes,
                    message => { Status(message); Log(message); });
                if (!closed)
                {
                    DiagnosticTrace.Write("TERMINAL_GITHUB.FECHAMENTO_RECUSADO_OU_INSEGURO");
                    operation.Finish(UpdateStage.Blocked, reason: "O Windows Terminal precisa estar fechado para atualizar.");
                    Status(operation.Snapshot.Details);
                    return;
                }
                DiagnosticTrace.Write("TERMINAL_GITHUB.PROCESSOS_VERIFICADOS_FECHADOS");
                deploymentPhase = true;
                if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar espera";
                UpdateAttemptResult result;
                try
                {
                    result = await new UpdateLaunchGate().RunAsync(
                        async () => _installed.Contains(entry) && WindowsTerminalUpdater.Resolve(entry) == recipe &&
                            await WindowsTerminalUpdater.CurrentUserVersionAsync(wait.Token) == before,
                        async () => { await Task.Run(() => WindowsTerminalUpdater.DeployAsync(bundle, Status, wait.Token, () => installerStarted = true)); return 0; },
                        async () => await UpdateInstallationContract.ConfirmAfterAsync(async () =>
                            await WindowsTerminalUpdater.CurrentUserVersionAsync(wait.Token) is Version installed && installed >= targetVersion, wait.Token), wait.Token, operation.Report, () => installerStarted);
                }
                finally { _deploymentWaitCts = null; if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Verificar"; }
                operation.Complete(result);
                _programList.RefreshOperations();
                bool confirmed = result.State == UpdateAttemptState.Confirmed;
                if (!confirmed)
                {
                    if (installerStarted) await RefreshInventoryAfterUpdateAsync(previousFilter, operation);
                    Status(operation.Snapshot.Details);
                    Log("Windows Terminal: nenhuma instalação confirmada. O provedor pode continuar após o prazo; não repetir automaticamente.");
                    DiagnosticTrace.Write("TERMINAL_GITHUB.VERSAO_NAO_CONFIRMADA");
                    return;
                }
                DiagnosticTrace.Write("TERMINAL_GITHUB.VERSAO_CONFIRMADA");
                Log("Windows Terminal " + targetVersion + " confirmado pelo identificador MSIX Microsoft.WindowsTerminal.");
                await RefreshInventoryAfterUpdateAsync(previousFilter, operation);
            }
            catch (OperationCanceledException)
            {
                operation.Finish(installerStarted ? UpdateStage.Pending : UpdateStage.Cancelled);
                Status(deploymentPhase ? "Espera ou consulta interrompida; confira a versão atual do Terminal." : "Preparação do Terminal cancelada.");
                Log(deploymentPhase ? "O Windows pode ter aplicado ou continuar aplicando o pacote. Nenhuma repetição automática." :
                    "Preparação cancelada antes de enviar pacote ao instalador do Windows.");
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("TERMINAL_GITHUB.FALHA", ex);
                Log("Windows Terminal: falha no pacote ou instalacao " + ex.GetType().Name +
                    ". Confira o estado do Windows e a versão; a operação pode ter sido iniciada. Nenhuma repetição automática.");
                operation.Fail(ex, installerStarted);
                if (installerStarted) await RefreshInventoryAfterUpdateAsync(previousFilter, operation);
                Status(operation.Snapshot.Details);
            }
            finally
            {
                _deploymentWaitCts = null;
                if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Verificar";
                if (operation.Snapshot.Active) operation.Finish(installerStarted ? UpdateStage.Pending : UpdateStage.Blocked);
                RecordV269UpdateOutcome(entry, operation.Snapshot);
                if (!_programList.IsDisposed) _programList.RefreshOperations();
                try { if (!WindowsTerminalUpdater.IsFolderProtected(folder) && Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
                catch (IOException) { Log("Nao foi possivel remover o pacote MSIX temporario."); }
                catch (UnauthorizedAccessException) { Log("Sem permissao para remover o pacote MSIX temporario."); }
            }
        });
    }

    private async Task UpdateThirdPartyAsync(ProgramEntry entry, bool fromApprovedBatch = false)
    {
        if (!AppBatchUpdateRun.MayExecute(_busy, _batchUpdatesActive, fromApprovedBatch) ||
            _betaClose.IsClosing || !_installed.Contains(entry)) return;
        VerifiedUpdateRecipe? recipe = VerifiedUpdaters.Resolve(entry);
        if (recipe is null)
        {
            Status("Atualização automática indisponível para este programa.");
            return;
        }
        if (!fromApprovedBatch && (!await ConfirmV269Async("Confirmar atualização",
            $"Atualizar {recipe.ProductName} para {recipe.Version}? O GF verificará a origem, o hash e a assinatura aplicável.") ||
            !_installed.Contains(entry) || VerifiedUpdaters.Resolve(entry)?.Version != recipe.Version))
            return;
        Status("Atualizando " + recipe.ProductName + ": preparando instalador oficial verificado...");
        Log("Atualização solicitada diretamente na lista: " + recipe.ProductName +
            " (" + recipe.Identifier + "). Origem e SHA-256 serão conferidos; assinatura digital exigida quando aplicável.");
        string previousFilter = _appFilter;
        await ExecuteBatchAwareAsync("Preparando atualização de " + recipe.ProductName + "...", async () =>
        {
            var operation = BeginUpdateOperation(entry, recipe.Version);
            if (operation is null) return;
            bool installerStarted = false;
            string folder = Path.Combine(Path.GetTempPath(), "GerenciadorFinal-Update-" + Guid.NewGuid().ToString("N"));
            try
            {
                // 7-Zip EXE e MSI podem compartilhar a pasta: o teste PC FORTE demonstrou
                // que remover uma variante deixou a outra registrada, porem sem EXE.
                // Nao baixar nem iniciar o instalador se houver coexistencia ou danos.
                if (recipe.Identifier == "sevenzip-exe-x64-26.05" &&
                    !VerifiedUpdaters.SingleSevenZipRegistration(entry,
                        await Task.Run(AppServices.GetInstalledPrograms)))
                {
                    Status("7-Zip: instalação incompleta ou outra edição também registrada. Atualização silenciosa bloqueada.");
                    Log("7-Zip: atualização não iniciada; confirme a instalação EXE x64 única e íntegra.");
                    operation.Finish(UpdateStage.Blocked, reason: "Instalação incompleta ou edições coexistentes; confira a instalação.");
                    return;
                }
                // Todas as receitas passam pelo contrato local antes de baixar.
                {
                    var current = await Task.Run(AppServices.GetInstalledPrograms);
                    if (!VerifiedUpdaters.SingleSafeSilentInstallation(recipe, entry, current))
                    {
                        Status("Instalação ambígua ou incompatível com atualização silenciosa. Dados preservados.");
                        Log("Atualização silenciosa bloqueada: edição MSI/EXE divergente, outro registro ou pasta não validada.");
                        operation.Finish(UpdateStage.Blocked, reason: "Edição, arquitetura, escopo ou pasta incompatível com a receita.");
                        return;
                    }
                }
                DiagnosticTrace.Write("INSTALACAO_OFICIAL.INICIO receita=" + recipe.Identifier);
                string verifiedInstaller;
                using (var downloadCts = new CancellationTokenSource(TimeSpan.FromMinutes(8)))
                {
                    _installerDownloadCts = downloadCts;
                    if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar download";
                    try
                    {
                        verifiedInstaller = await Task.Run(() => VerifiedUpdaters.DownloadAndVerifyAsync(recipe, folder, Status, downloadCts.Token, progress => ReportV269AppDownload(operation, entry, progress, downloadCts)));
                    }
                    finally
                    {
                        FinishV269AppDownload(entry);
                        if (!IsDisposed && _checkUpdatesButton is not null)
                            _checkUpdatesButton.Text = "Verificar";
                    }
                }
                DiagnosticTrace.Write("INSTALACAO_OFICIAL.ARQUIVO_VALIDADO receita=" + recipe.Identifier);
                if (AutomaticUpdateDiscovery.IsAutomatic(recipe))
                {
                    operation.Report(new(UpdateStage.Revalidating));
                    bool ready=await AutomaticProcessPreflight.ReadyAsync(AutomaticUpdateDiscovery.Binary(recipe),
                        question=>DarkConfirmDialog.Confirm(this,question,"Fechar aplicativo para atualizar")==DialogResult.Yes,
                        message=>{Status(message);Log(message);});
                    if(!ready){operation.Finish(UpdateStage.Blocked,reason:"Aplicativo aberto ou fechamento não autorizado.");return;}
                }
                // Fluxo compartilhado por instaladores que substituem arquivos em uso.
                // Restart Manager identifica processos pelos ARQUIVOS bloqueados,
                // nunca pelo simples fato de um navegador estar aberto.
                operation.Report(new(UpdateStage.Revalidating));
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
                        operation.Finish(UpdateStage.Blocked, reason: "Há arquivos em uso ou o fechamento não foi autorizado.");
                        Status(operation.Snapshot.Text);
                        return;
                    }
                    DiagnosticTrace.Write("INSTALACAO_OFICIAL.PREFLIGHT_APROVADO");
                }
                Status("Instalador verificado. Aguardando conclusão...");
                using var wait = new CancellationTokenSource();
                _deploymentWaitCts = wait;
                if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Cancelar espera";
                var result = await new UpdateLaunchGate().RunAsync(
                    async () =>
                    {
                        if (!_installed.Contains(entry) || VerifiedUpdaters.Resolve(entry) != recipe) return false;
                        var current = await Task.Run(AppServices.GetInstalledPrograms);
                        return VerifiedUpdaters.SameInstallationForUpdate(entry, current) &&
                            VerifiedUpdaters.SingleSafeSilentInstallation(recipe, entry, current) &&
                            (recipe.Identifier != "sevenzip-exe-x64-26.05" ||
                             VerifiedUpdaters.SingleSevenZipRegistration(entry, current));
                    },
                    () => Task.Run(() => VerifiedUpdaters.LaunchAsync(recipe, verifiedInstaller, Status, () => installerStarted = true, wait.Token)),
                    async () => await UpdateInstallationContract.ConfirmAfterAsync(async () =>
                        VerifiedUpdaters.ConfirmInstallation(recipe, entry, await Task.Run(AppServices.GetInstalledPrograms).WaitAsync(TimeSpan.FromSeconds(12), wait.Token)), wait.Token),
                    wait.Token, progress: operation.Report,
                    hasConfirmedInstallerStart: () => installerStarted);
                _deploymentWaitCts = null;
                if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Verificar";
                operation.Complete(result);
                _programList.RefreshOperations();
                DiagnosticTrace.Write("INSTALACAO_OFICIAL.EXECUCAO_FIM estado=" + result.State + " retorno=" + result.ExitCode);
                if (result.State is UpdateAttemptState.Blocked or UpdateAttemptState.Cancelled or UpdateAttemptState.Failed)
                {
                    Log("Atualização não confirmada: " + result.State + ". Não haverá nova tentativa automática.");
                    if (installerStarted) await RefreshInventoryAfterUpdateAsync(previousFilter, operation);
                    Status(operation.Snapshot.Details);
                    return;
                }
                bool confirmed = result.State == UpdateAttemptState.Confirmed;
                int? exit = result.ExitCode;
                if (confirmed)
                {
                    Log("Atualização confirmada no Registro e nos arquivos exigidos pela receita: " + recipe.ProductName + " " + recipe.Version);
                    Status(exit == 3010 ? "Atualizado; reinicialização pode ser necessária." : "Atualização concluída e versão confirmada.");
                    DiagnosticTrace.Write("INSTALACAO_OFICIAL.VERSAO_CONFIRMADA");
                }
                else
                {
                    Log("Instalador finalizado, mas versão e identidade da instalação escolhida não foram confirmadas. Outra cópia ou mudança de registro não comprova o resultado; confira o fabricante.");
                    Status("Instalador terminou; versão ainda não confirmada.");
                    DiagnosticTrace.Write("INSTALACAO_OFICIAL.VERSAO_NAO_CONFIRMADA");
                }
                await RefreshInventoryAfterUpdateAsync(previousFilter, operation);
            }
            catch (OperationCanceledException)
            {
                operation.Finish(installerStarted ? UpdateStage.Pending : UpdateStage.Cancelled,
                    reason: installerStarted ? "A espera foi cancelada; o instalador pode continuar. Não repetir." : "Preparação ou download interrompido.");
                Log(operation.Snapshot.Details);
                Status(operation.Snapshot.Details);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                operation.Finish(installerStarted ? UpdateStage.Pending : UpdateStage.AuthorizationDeclined,
                    reason: installerStarted
                        ? "O instalador foi iniciado; confira a versão antes de tentar novamente."
                        : "Autorização UAC recusada antes do início.");
                Log(installerStarted
                    ? "Windows retornou cancelamento após iniciar instalador; resultado pendente, não repetir."
                    : "Autorização UAC recusada antes da execução do instalador.");
                Status(operation.Snapshot.Details);
            }
            catch (Exception ex)
            {
                DiagnosticTrace.Failure("INSTALACAO_OFICIAL.FALHA", ex);
                Log("Falha de atualização: " + ex.GetType().Name + ". Nenhuma conclusão presumida.");
                operation.Fail(ex, installerStarted);
                if (installerStarted) await RefreshInventoryAfterUpdateAsync(previousFilter, operation);
                Status(operation.Snapshot.Details);
            }
            finally
            {
                _deploymentWaitCts = null;
                if (_checkUpdatesButton is not null) _checkUpdatesButton.Text = "Verificar";
                if (operation.Snapshot.Active) operation.Finish(installerStarted ? UpdateStage.Pending : UpdateStage.Blocked);
                RecordV269UpdateOutcome(entry, operation.Snapshot);
                if (_batchUpdatesActive) _batchLastAttempt = operation.Snapshot;
                if (!_programList.IsDisposed) _programList.RefreshOperations();
                if (VerifiedUpdaters.IsFolderProtected(folder))
                    Log("Pacote temporario protegido nesta sessao. Mantenha o GF aberto ate todos os instaladores terminarem.");
                else
                {
                    try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
                    catch (Exception) { Log("Aviso: não foi possível limpar a pasta temporária da atualização."); }
                }
            }
        }, quietStartup: false, allowDuringBatch: fromApprovedBatch);
    }

    private async Task UninstallCheckedAsync() => await UninstallEntriesAsync(_checkedApps.ToArray());

    private async Task UninstallSelectedAsync()
    {
        if (_programList.SelectedItem is ProgramEntry entry)
            await UninstallEntriesAsync(new[] { entry });
        else Status("Nenhum aplicativo selecionado");
    }

    private CancellationTokenSource? _uninstallCts;
    private async Task UninstallEntriesAsync(IReadOnlyCollection<ProgramEntry> apps)
    {
        if (apps.Count == 0) { Status("Nenhum aplicativo marcado"); return; }
        if (apps.Any(app => _uninstallOperations.Find(app)?.PreventRetry == true))
        { Status("Uma desinstalação ainda está pendente. Confira o resumo antes de tentar novamente."); return; }
        foreach (var app in apps) SetUninstallProgress(app, "preparing");
        // Entregar o primeiro frame antes de consultas e verificacao de assinatura.
        await Task.Yield();
        var prepared = new List<PreparedUninstaller>();
        using var cancellation = new CancellationTokenSource();
        int diagnosticUnavailable = 0;
        void RecordResidualEvent(string line)
        {
            if (DiagnosticTrace.Write(line)) return;
            Interlocked.Exchange(ref diagnosticUnavailable, 1);
            cancellation.Cancel();
        }
        _uninstallCts = cancellation;
        ShowUninstallCancellation(true);
        var contexts = new Dictionary<string, ResidualUninstallContext>(StringComparer.OrdinalIgnoreCase);
        async Task<UninstallSnapshot> Inventory()
        {
            var scan = await Task.Run(AppServices.ScanAllInstalledAppsForSafety);
            return new(scan.Items, !scan.Incomplete);
        }
        try
        {
            var requests = await Task.Run(() => UninstallBatch.Select(apps), cancellation.Token);
            var before = await Inventory();
            if (requests.Length == 0 || !before.Complete ||
                requests.Any(request => !BundlewareReview.RelationsStillSafe(request, before, before)))
            {
                foreach (var app in apps)
                    SetUninstallProgress(app, "failed", "Inventário ou relação de pacote sem confirmação.");
                Status("Revise o inventário antes de remover. Nenhum desinstalador foi iniciado.");
                return;
            }
            foreach (var request in requests)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var context = await Task.Run(() => IntegratedResidualCleanup.Prepare(request.Original,
                    before, cancellation.Token), cancellation.Token);
                contexts.Add(request.Original.InventoryId, context);
                RecordResidualEvent(IntegratedResidualCleanup.Audit(context.App, "prepare", "captured",
                    folders: context.Roots.Count, keys: context.Keys.Count, kept: context.Preserved));
                foreach (var problem in context.Problems)
                    RecordResidualEvent(IntegratedResidualCleanup.Audit(context.App, problem.Stage, "preserved",
                        error: problem.Error, reason: problem.Reason));
                prepared.Add(await Task.Run(() => RegisteredUninstaller.Prepare(request,
                    (path, signer) => VerifiedUpdaters.VerifyAuthenticode(path, signer, out _),
                    signatureIssue: (path, signer) =>
                    {
                        VerifiedUpdaters.VerifyAuthenticode(path, signer, out string issue);
                        return issue;
                    }), cancellation.Token));
                var proof = prepared[^1];
                if (request.Original.Capabilities is { } capability && SilentUninstallPolicy.UsesSilentMode(request) &&
                    !proof.UnsignedWarning && !proof.PublisherWarning && !proof.TrustWarning)
                    request.Original.Capabilities = capability with
                    { Uninstall = new(ApplicationCapabilityState.SilentVerified, "payload_verified") };
            }
            string preview = await Task.Run(() => string.Join("\n\n", prepared.Select(p =>
                "• " + p.Request.Original.Name + "\n" + SilentUninstallPolicy.DescribeMode(p.Request) +
                (p.UnsignedWarning ? "\nDesinstalador sem assinatura digital. Confira o aplicativo antes de continuar." :
                 p.PublisherWarning ? "\nAssinatura válida com editor diferente do cadastro. Confira o aplicativo." :
                 p.TrustWarning ? "\nO Windows não confirmou a confiança da assinatura. Confira o aplicativo." : "") +
                (BundlewareReview.ExplainPotentialBundleImpact(p.Request.Original, before)
                    is { Length: > 0 } impact ? "\n" + impact : ""))), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            foreach (var app in apps) SetUninstallProgress(app, "awaiting_consent");
            bool consent = await ConfirmV269Async("Confirmar desinstalação",
                $"Deseja desinstalar {prepared.Count} aplicativo(s)?\n\n{preview}\n\nOs desinstaladores podem remover dados.",
                cancellation.Token);
            var outcomes = await Task.Run(() => new UninstallBatch().RunAsync(requests, consent, Inventory,
                (request, token) =>
                {
                    Status("Aguardando desinstalação de " + request.Original.Name + "...");
                    RecordResidualEvent(IntegratedResidualCleanup.Audit(ResidualAppIdentity.From(request.Original),
                        "launch", SilentUninstallPolicy.UsesSilentMode(request) ? "silent" : "interactive"));
                    return prepared.Single(p => p.Request == request).LaunchAsync(token);
                }, token => Task.Delay(500, token), cancellation.Token,
                (request, stage) => SetUninstallProgress(request.Original, stage),
                preLaunchSafety: (request, current) =>
                    BundlewareReview.RelationsStillSafe(request, before, current)));
            foreach (var plan in prepared) plan.Dispose();
            prepared.Clear();
            foreach (var outcome in outcomes)
            {
                SetUninstallProgress(outcome.Request.Original, outcome.State == UninstallState.RegistrationAbsent ? "inspecting" :
                    outcome.State == UninstallState.Pending ? "pending" : outcome.State is UninstallState.Refused or UninstallState.Cancelled ? "cancelled" : "failed");
                RecordResidualEvent(IntegratedResidualCleanup.Audit(ResidualAppIdentity.From(outcome.Request.Original),
                    "uninstall", outcome.State.ToString(), error: outcome.Code ?? 0, milliseconds: outcome.DurationMilliseconds,
                    reason: outcome.DiagnosticReason.Length > 0 ? outcome.DiagnosticReason :
                        RemovalSafetyContract.AuditOutcome(outcome.State == UninstallState.RegistrationAbsent)));
            }
            foreach (var app in apps.Where(app => !outcomes.Any(o => o.Request.Original.InventoryId == app.InventoryId)))
                SetUninstallProgress(app, "cancelled");
            // Um registro principal ausente não prova que os componentes do
            // bundle OEM também saíram. Consultar uma vez após o executor,
            // sem tentar remover filhos e sem promover ausência ARP a limpeza.
            var bundleRoots = outcomes.Where(o =>
                o.State == UninstallState.RegistrationAbsent &&
                BundlewareReview.ObserveAfterUninstall(o, before, null).RelatedBefore > 0)
                .ToArray();
            var bundleObservations = new Dictionary<string, BundlePostRemovalReview>(
                StringComparer.OrdinalIgnoreCase);
            if (bundleRoots.Length != 0)
            {
                UninstallSnapshot? postBundle = null;
                try { postBundle = await Inventory(); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log("Bundleware: consulta posterior indisponível (" +
                        ex.GetType().Name + "); não confirmar remoção dos componentes.");
                }
                foreach (var outcome in bundleRoots)
                {
                    var review = BundlewareReview.ObserveAfterUninstall(
                        outcome, before, postBundle);
                    bundleObservations[outcome.Request.Original.InventoryId] = review;
                    Log("Bundleware — " + outcome.Request.Original.Name + ": " + review.Message);
                }
            }
            // A limpeza integrada usa apenas os resultados sem dependências
            // OEM remanescentes. Não altera o desfecho do desinstalador nem
            // remove silenciosamente componentes do pacote.
            var cleanupCandidates = BundlewareReview.SafeResidualCleanupCandidates(
                outcomes, bundleObservations);
            foreach (var held in outcomes.Where(o =>
                !cleanupCandidates.Contains(o)))
                RecordResidualEvent(IntegratedResidualCleanup.Audit(
                    ResidualAppIdentity.From(held.Request.Original),
                    "bundle_cleanup", "preserved",
                    reason: "bundle_components_remaining_or_inconclusive"));
            ResidualBatchResult? cleanup = null;
            if (consent)
            {
                Status("Conferindo resíduos dos aplicativos removidos...");
                cleanup = await Task.Run(() => IntegratedResidualBatch.RunAsync(cleanupCandidates, contexts,
                    Inventory, (plans, token) => OnUninstallUiAsync(() => ConfirmUninstallCleanupAsync(plans, token)),
                    (plans, token) => OnUninstallUiAsync(() => RunElevatedAsync(ResidualElevatedProtocol.TaskName, plans, token, RecordResidualEvent)),
                    RecordResidualEvent, cancellation.Token,
                    (app, stage) => SetUninstallProgress(app.Entry(), stage)));
                // A recarga visual nao apaga o resultado da operacao concluida.
                try { await RefreshCatalogAsync(); }
                catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex))
                { Log("O inventário não pôde ser atualizado. O resumo da desinstalação foi preservado."); }
            }
            string summary = UninstallResultSummary.Describe(outcomes, cleanup, requests);
            if (bundleObservations.Count != 0)
            {
                summary += Environment.NewLine + Environment.NewLine +
                    "CONFERÊNCIA DE COMPONENTES OEM — somente registros do Windows" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, bundleRoots.Select(o =>
                        o.Request.Original.Name + ": " +
                        bundleObservations[o.Request.Original.InventoryId].Message));
            }
            foreach (var outcome in outcomes.Where(o => o.State == UninstallState.RegistrationAbsent))
            {
                string appId = ResidualAppIdentity.From(outcome.Request.Original).TechnicalId;
                var inspection = cleanup?.Inspections?.SingleOrDefault(p => p.Context.App.TechnicalId == appId);
                var cleaned = cleanup?.Results.SingleOrDefault(r => r.AppId == appId);
                bundleObservations.TryGetValue(outcome.Request.Original.InventoryId,
                    out var bundleObservation);
                SetUninstallProgress(outcome.Request.Original, cleanup?.Cancelled == true ? "cancelled" :
                    bundleObservation?.MustShowPartialResult == true ||
                    inspection is null || !inspection.Conclusive || cleaned is { Verified: false } && cleanup?.Declined != true ? "partial" : "finished",
                    UninstallResultSummary.Describe([outcome], cleanup) +
                    (bundleObservation is null ? "" :
                        Environment.NewLine + "Componentes OEM: " + bundleObservation.Message));
            }
            string result = UninstallResultSummary.Status(outcomes, cleanup, consent);
            Status(result); Log(result);
            ShowUninstallResult(summary);
            if (consent && cleanup?.Declined != true && cleanup?.Cancelled != true &&
                !cancellation.IsCancellationRequested)
            {
                var review = RebootResidualReview.Select(outcomes, cleanup);
                if (review.Length > 0)
                    OfferRestartReview(review);
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var app in apps)
                RecordResidualEvent(IntegratedResidualCleanup.Audit(ResidualAppIdentity.From(app), "operation", "cancelled", cancelled: true));
            foreach (var app in apps.Where(app => _uninstallOperations.Find(app)?.Active == true))
                SetUninstallProgress(app, "cancelled");
            Status("Operação cancelada.");
            ShowUninstallResult("Operação interrompida. Uma desinstalação já iniciada pode continuar trabalhando. Nenhuma limpeza adicional foi autorizada. Consulte o diagnóstico.");
        }
        catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex))
        {
            foreach (var app in apps)
                RecordResidualEvent(IntegratedResidualCleanup.Audit(ResidualAppIdentity.From(app), "operation", "failed",
                    failed: 1, error: IntegratedResidualCleanup.Error(ex), reason: "uninstall_preparation_or_execution"));
            foreach (var app in apps.Where(app => _uninstallOperations.Find(app)?.Active == true))
                SetUninstallProgress(app, "failed");
            Log("Não foi possível concluir — consulte o diagnóstico.");
            Status("Não foi possível concluir — consulte o diagnóstico.");
            ShowUninstallResult("Não foi possível concluir a operação. A ausência de resíduos não foi comprovada. Consulte o diagnóstico.");
        }
        finally
        {
            _uninstallCts = null;
            ShowUninstallCancellation(false);
            foreach (var plan in prepared) plan.Dispose();
            if (diagnosticUnavailable != 0)
            {
                Log("Não foi possível gravar o diagnóstico. A operação foi interrompida.");
                Status("Operação interrompida — diagnóstico indisponível.");
            }
        }
    }

    private async Task RunMaintenanceAuditAsync()
    {
        Status("Conferindo instalações registradas e dispositivos do Windows...");
        DiagnosticTrace.Write("DIAGNOSTICO_COMPLETO.INICIO");
        var inventory = await Task.Run(AppServices.ScanAllInstalledAppsForSafety);
        var maintenanceTask = Task.Run(() => MaintenanceAudit.Inspect(inventory.Items,
            inventoryIncomplete: inventory.Incomplete));
        var games = inventory.Games;
        // Consulta agregada via API oficial do Windows, sem subprocessos,
        // privilegiamento ou chamadas a APIs privadas do AVG.
        var securityTask = Task.Run(SecurityCenterHealth.Capture);
        await Task.WhenAll(maintenanceTask, securityTask);
        var report = await maintenanceTask;
        var security = await securityTask;
        DiagnosticTrace.Write($"DIAGNOSTICO_COMPLETO.FIM instalados={report.Installed} reconhecidos={report.Recognized} desinstaladores_ausentes={report.MissingUninstallers} desinstaladores_indeterminados={report.UnverifiableUninstallers} pnp_consultados={report.Devices.Queried} desconectados={report.Devices.Disconnected} problemas={report.Devices.WithProblems} pnp_sem_correspondencia={report.HistoricalPnP?.NotPresent ?? 0} steam={games.Steam} epic={games.Epic} inventario_parcial={report.InventoryIncomplete} msix_status={inventory.Msix.Status} msix_fase={inventory.Msix.FailureStage} msix_apartment={inventory.Msix.Apartment} parcial={report.Incomplete || games.Partial}");
        TestDiagnosticDetail.Record("MANUTENCAO",
            ("instalados", report.Installed), ("reconhecidos", report.Recognized),
            ("sem_desinstalador", report.MissingUninstallers),
            ("desinstalador_incerto", report.UnverifiableUninstallers),
            ("pnp", report.Devices.Queried),
            ("historicos_nao_observados", report.HistoricalPnP?.NotPresent ?? 0),
            ("seguranca_atencao", security.Count(x => x.Level == SecurityHealthLevel.Attention)),
            ("seguranca_incerta", security.Count(x => x.Level != SecurityHealthLevel.Attention && x.Level != SecurityHealthLevel.Ok)),
            ("parcial", report.Incomplete || games.Partial ? 1 : 0));
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
                    $"enumeradas; {oldDevices.NotPresent} sem correspondência no retrato WMI atual. " +
                    "Essa contagem é diferente de dispositivos desconectados e NÃO " +
                    "representa quantidade de drivers antigos ou removíveis.");
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
        foreach (var health in security)
        {
            var level = health.Level switch
            {
                SecurityHealthLevel.Ok => "OK",
                SecurityHealthLevel.Attention => "ATENÇÃO",
                _ => "INDETERMINADO"
            };
            Log($"SEGURANÇA [{level}]: {health.Category} — {health.Description}");
        }
        Log("Diagnóstico de proteção: leitura do Centro de Segurança. Não substitui AVG ou Defender.");
        Log("Nenhum driver, arquivo de instalação ou chave de Registro foi removido pelo diagnóstico.");
        Status(report.Incomplete || games.Partial
            ? "Diagnóstico parcial: consulte as informações e limitações no registro."
            : $"Diagnóstico: {report.MissingUninstallers} desinstaladores ausentes; " +
              $"{report.HistoricalPnP?.NotPresent ?? 0} instâncias PnP sem confirmação (não são drivers).");
        if(_v269CleanupExecutionActive)
        {
            bool complete=!report.Incomplete&&!games.Partial&&report.Devices.Supported&&
                security.All(x=>x.Level is SecurityHealthLevel.Ok or SecurityHealthLevel.Attention);
            var lines=new List<string>{
                complete?"Diagnóstico consultivo concluído.":"Diagnóstico consultivo parcial; existem leituras não confirmadas.",
                $"Aplicativos registrados: {report.Installed}; desinstaladores ausentes: {report.MissingUninstallers}; não verificáveis: {report.UnverifiableUninstallers}.",
                report.Devices.Supported?$"Dispositivos consultados: {report.Devices.Queried}; presentes com problemas: {report.Devices.WithProblems}.":"Consulta PnP indisponível.",
                report.HistoricalPnP is {} history?$"Instâncias PnP sem correspondência observada: {history.NotPresent}. Isso não indica drivers removíveis.":"Comparação PnP histórica indisponível.",
                $"Bibliotecas: Steam {games.Steam}, Epic {games.Epic}."
            };
            lines.AddRange(security.Select(health=>$"{health.Category}: {health.Description}"));
            lines.AddRange(report.Concerns.Select(issue=>$"Revisar: {issue.Category} — {issue.Name}: {issue.Description}"));
            lines.Add("Nenhum driver, arquivo ou chave do Registro foi removido pelo diagnóstico.");
            _v269CleanupOutcome=new("Diagnóstico completo",string.Join(Environment.NewLine,lines),complete);
        }
    }

    private async Task VerifyWindowsIntegrityAsync()
    {
        _windowsRepairable = false;
        _windowsScannedAt = default;
        RefreshWindowsRepairButton();
        await RunElevatedAsync("windows-check");
    }

    private void RefreshWindowsRepairButton()
    {
        if (_repairWindowsButton is { IsDisposed: false } button)
            button.Enabled = !_busy && _windowsRepairable &&
                DateTimeOffset.UtcNow - _windowsScannedAt < TimeSpan.FromMinutes(30);
    }

    private async Task RepairWindowsAfterVerificationAsync()
    {
        if (_busy || _betaClose.IsClosing || !_windowsRepairable ||
            DateTimeOffset.UtcNow - _windowsScannedAt >= TimeSpan.FromMinutes(30))
        {
            _windowsRepairable = false;
            RefreshWindowsRepairButton();
            Status("Nova verificação necessária antes de reparar.");
            return;
        }
        // Evitar solicitar UAC se o Windows já apresenta reinício pendente.
        // O worker elevado repete essa prova para fechar a janela de tempo.
        var rebootBeforeConsent = WindowsIntegrityAssessment.InspectRebootReadOnly();
        if (rebootBeforeConsent != WindowsRepairRebootState.Clear)
        {
            _windowsRepairable = false;
            _windowsScannedAt = default;
            RefreshWindowsRepairButton();
            Status(rebootBeforeConsent == WindowsRepairRebootState.Pending
                ? "Reinicialização pendente. Conclua o reinício e refaça a verificação antes de reparar."
                : "Não foi possível conferir reinício pendente. Reparo bloqueado; refaça o diagnóstico.");
            return;
        }
        _windowsRepairable = false; // Uma autorizacao por diagnostico.
        RefreshWindowsRepairButton();
        await ConfirmThenRun("windows-repair",
            "Foram encontrados problemas em arquivos do Windows. Deseja tentar corrigir? " +
            "A operação pode demorar e alterar arquivos do sistema. Após o reparo, o SFC verificará os arquivos protegidos novamente.");
    }

    private async Task ChooseRestorePointAsync()
    {
        if (_busy || _betaClose.IsClosing) return;
        if (!await ConfirmV269Async("Escolher ponto de restauração",
            "Abrir o assistente oficial de Restauração do Sistema? Você poderá escolher e " +
            "confirmar um ponto no próprio Windows. Aplicativos e atualizações posteriores " +
            "podem ser removidos e pode ser necessário reiniciar. Não substitui backup.")) return;
        try
        {
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (string.IsNullOrWhiteSpace(system) || !Path.IsPathFullyQualified(system))
                throw new InvalidOperationException("Diretório do Windows indisponível.");
            string path = Path.Combine(system, "rstrui.exe");
            if (!File.Exists(path)) throw new FileNotFoundException("Assistente indisponível.", path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Log("Assistente oficial aberto; nenhuma restauração foi iniciada pelo Gerenciador.");
        }
        catch (Exception ex) { Log("Assistente de restauração indisponível: " + ex.Message); }
    }

    private static string SimpleConfirmation(string task)
    {
        if (SingleVolumeOptimization.TryParseTask(task, out string volume))
            return "Deseja otimizar somente a unidade " + volume +
                "? O Windows escolherá o procedimento adequado. Esta operação pode demorar.";
        return task switch
        {
        "clean-user-temp" => "Deseja limpar os arquivos temporários antigos do usuário?",
        "clean-windows-temp" => "Limpar arquivos temporários antigos do Windows? A autorização administrativa pode dar acesso a itens que a prévia não conseguiu consultar.",
        "clear-update-cache" => "Limpar downloads antigos do Windows Update? O acesso é reavaliado após a autorização. A limpeza só ocorre enquanto os serviços de atualização estiverem parados.",
        "cleanmgr-auto" => "Abrir a Limpeza de Disco do Windows para escolher as categorias na ferramenta do sistema?",
        "clean-directx-cache" => "Deseja limpar o cache antigo de shaders DirectX? Alguns jogos podem recompilar os arquivos.",
        "windows-repair" => "Deseja verificar e reparar os arquivos do Windows? Esta operação pode demorar.",
        "network-reset" => "Deseja redefinir a rede? A conexão pode ser interrompida.",
        "create-restore-point" => "Deseja criar um ponto de restauração?",
        _ => "Deseja executar esta operação?"
        };
    }

    private async Task ConfirmThenRun(string task, string question)
    {
        if (_busy || _betaClose.IsClosing) return;
        string confirmation = task == "windows-repair" ? question : SimpleConfirmation(task);
        string title = _nav.SelectedPage != V269Page.Limpeza ? "Confirmar operação" :
            CleanupTarget.FromTask(task) is { } cleanupTarget ? "Limpar " + cleanupTarget.Title :
            task=="cleanmgr-auto" ? "Limpeza avançada" : task.StartsWith("optimize-volume:",StringComparison.Ordinal) ? "Otimizar " + task["optimize-volume:".Length..] + ":" : "Confirmar operação";
        if (!await ConfirmV269Async(title, confirmation))
        {
            if (SafeMaintenanceAudit.Cancelled(task) is { } audit)
                DiagnosticTrace.Write(audit);
            Log("Operação cancelada pelo usuário.");
            return;
        }
        if (SafeMaintenanceAudit.Begin(task) is { } started)
            DiagnosticTrace.Write(started);
        if (CleanupTarget.FromTask(task) is { } target && target != CleanupTarget.RecycleBin)
        {
            await CleanTemporaryCategoryAsync(target);
            return;
        }
        await ExecuteAsync("Executando manutenção do Windows...", () => RunElevatedAsync(task));
    }

    private async Task<ResidualElevationResponse?> RunElevatedAsync(string task,
        IReadOnlyList<ResidualElevationRequest>? residualRequests = null, CancellationToken cancellation = default,
        Action<string>? residualAudit = null, Action<string>? windowsUpdateMessage = null,
        ElevatedCleanupProtocol? cleanup = null,
        Action<NetworkRepairWorkerState>? networkRepairCompletion = null)
    {
        // O UAC é solicitado, mas a tarefa não abre formulário/terminal próprio.
        // Mensagens trafegam apenas em memória pelo pipe nomeado desta sessão.
        void RecordResidual(string line)
        {
            if (residualAudit is not null) residualAudit(line);
            else DiagnosticTrace.Write(line);
        }
        ResidualElevationResponse? ResidualUacFailure(string reason, int error = 0)
        {
            if (residualRequests is null) return null;
            foreach (var request in residualRequests)
                RecordResidual(IntegratedResidualCleanup.Audit(request.Plan.Context.App, "uac", "failed", error: error, reason: reason));
            return new(reason, error, []);
        }
        if (residualRequests is not null && cancellation.IsCancellationRequested)
            return ResidualUacFailure("uac_cancelled");
        string pipeName = "GA5122_" + Guid.NewGuid().ToString("N");
        string token = Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
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
        {
            if (residualRequests is not null)
            {
                foreach (var request in residualRequests)
                    RecordResidual(IntegratedResidualCleanup.Audit(request.Plan.Context.App, "uac", "declined", error: 1223));
                return new("uac_declined", 1223, []);
            }
            if (SafeMaintenanceAudit.Cancelled(task) is { } audit)
                DiagnosticTrace.Write(audit);
            cleanup?.Declined();
            windowsUpdateMessage?.Invoke(WindowsUpdateOperation.AuthorizationDeclined);
            Status("Autorização administrativa cancelada");
            Log("Operação cancelada na confirmação do UAC.");
            return null;
        }
        catch (Exception ex) when (residualRequests is not null && IntegratedResidualCleanup.Expected(ex))
        { return ResidualUacFailure("uac_start_failed", IntegratedResidualCleanup.Error(ex)); }
        using (child)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(35));
            var windowsProgress = new WindowsMaintenanceProgress();
            try { await server.WaitForConnectionAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (residualRequests is not null)
                    foreach (var request in residualRequests)
                        RecordResidual(IntegratedResidualCleanup.Audit(request.Plan.Context.App, "uac", "failed", reason: "worker_connect_timeout"));
                Log("Não foi possível conectar à tarefa administrativa. Verifique se o Windows ou antivírus bloqueou o programa.");
                Status("Falha de comunicação com operação administrativa");
                return ResidualUacFailure(cancellation.IsCancellationRequested ? "uac_cancelled" : "uac_connect_timeout");
            }
            // Token nao substitui a identidade do cliente informada pelo kernel.
            if (!PipePeerVerifier.IsExpectedClient(server, child.Id))
            { Status("Conexao administrativa rejeitada: processo inesperado."); return ResidualUacFailure("uac_peer_rejected"); }
            using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                bufferSize: 4096, leaveOpen: true);
            string? handshake;
            using (var handshakeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                try { handshake = await reader.ReadLineAsync(handshakeTimeout.Token); }
                catch (OperationCanceledException)
                { Status("Tempo esgotado na autenticacao da tarefa administrativa."); return ResidualUacFailure("uac_handshake_timeout"); }
            }
            if (handshake != "HELLO|" + token)
            { Status("Falha de autenticação da tarefa administrativa"); return ResidualUacFailure("uac_handshake_failed"); }
            if (residualRequests is not null)
            {
                try
                {
                    foreach (var request in residualRequests)
                        RecordResidual(IntegratedResidualCleanup.Audit(request.Plan.Context.App, "uac", "authorized"));
                    var response = await Task.Run(() => ResidualElevatedProtocol.ParentAsync(server, residualRequests, cancellation));
                    await Task.Run(() =>
                    {
                    foreach (var request in residualRequests)
                        RecordResidual(IntegratedResidualCleanup.Audit(request.Plan.Context.App, "uac_result", response.State, error: response.Error));
                    foreach (var result in response.Results)
                    {
                        var app = residualRequests.Single(r => r.Plan.Context.App.TechnicalId == result.AppId).Plan.Context.App;
                        foreach (var item in result.Items)
                            RecordResidual(IntegratedResidualCleanup.Audit(app, "uac_" + (item.Kind == "registry"
                                ? IntegratedResidualCleanup.RegistryStage(residualRequests.Single(r => r.Plan.Context.App.TechnicalId == result.AppId).Plan.Keys.Single(k => k.Id == item.Id))
                                : item.Kind), item.State.ToString(),
                                error: item.Error, reason: item.Reason));
                        foreach (var issue in result.Problems)
                            RecordResidual(IntegratedResidualCleanup.Audit(app, issue.Stage, "blocked", error: issue.Error, reason: issue.Reason));
                    }
                    });
                    return response;
                }
                catch (Exception ex) when (IntegratedResidualCleanup.Expected(ex) || ex is OperationCanceledException or System.Text.Json.JsonException)
                {
                    foreach (var request in residualRequests)
                        RecordResidual(IntegratedResidualCleanup.Audit(request.Plan.Context.App, "uac", "failed",
                            error: IntegratedResidualCleanup.Error(ex), reason: "worker_communication_failed"));
                    return new("uac_communication_failed", IntegratedResidualCleanup.Error(ex), []);
                }
            }
            if (cleanup is not null)
            {
                await cleanup.ReadAsync(server, reader);
                return null;
            }
            bool gotResult = false;
            IntegrityState? postRepair = null;
            while (await reader.ReadLineAsync() is { } line)
            {
                if (task == "windows-check" &&
                    line == "RESULT|WINCHECK:REPAIRABLE")
                {
                    _windowsRepairable = true;
                    _windowsScannedAt = DateTimeOffset.UtcNow;
                    Log("Corrupção reparável identificada; reparo opcional disponível.");
                }
                else if (task == "windows-check" && line == "RESULT|WINCHECK:HEALTHY")
                {
                    _windowsRepairable = false;
                    Log("A verificação não encontrou problemas que exijam reparo.");
                }
                else if (task == "windows-check" && line == "RESULT|WINCHECK:INCONCLUSIVE")
                {
                    _windowsRepairable = false;
                    Log("Verificação inconclusiva. Reparo não liberado automaticamente.");
                }
                else if (task == "windows-repair" && line.StartsWith("RESULT|WINREPAIR:", StringComparison.Ordinal))
                {
                    if (WindowsIntegrityAssessment.TryReadPostRepairWire(
                        line["RESULT|".Length..], out var observed))
                    {
                        postRepair = observed;
                        Log(observed switch
                        {
                            IntegrityState.Healthy => "Pós-reparo: DISM e SFC não detectaram corrupção.",
                            IntegrityState.Repairable => "Pós-reparo: persistem violações ou armazenamento reparável.",
                            _ => "Pós-reparo: verificação inconclusiva; não declarar sucesso."
                        });
                    }
                }
                else if (windowsUpdateMessage is not null && line.StartsWith("RESULT|WU", StringComparison.Ordinal))
                {
                    windowsUpdateMessage(line["RESULT|".Length..]);
                }
                else if (line.StartsWith("LOG|", StringComparison.Ordinal))
                {
                    string logLine = line[4..];
                    Log(logLine);
                    if ((task is "windows-check" or "windows-repair") &&
                        windowsProgress.Update(logLine, out int percentage) &&
                        !IsDisposed)
                    {
                        _progress.MarqueeAnimationSpeed = 0;
                        _progress.Style = ProgressBarStyle.Continuous;
                        _progress.Value = percentage;
                        _nav.PresentPanel(new(V269PanelKind.Progress, "Manutenção do Windows",
                            _status.Text, percentage));
                        Status("Manutenção Windows — progresso: " +
                            percentage.ToString() + "%");
                    }
                }
                else if (line.StartsWith("DONE|", StringComparison.Ordinal))
                {
                    string code = line[5..];
                    if (networkRepairCompletion is not null &&
                        task is "flush-dns" or "network-repair-winsock")
                        networkRepairCompletion(NetworkRepairOutcome.FromWire(line));
                    if (task == "windows-check" && code != "0") _windowsRepairable = false;
                    if (SafeMaintenanceAudit.ElevatedExit(task, code) is { } audit)
                        DiagnosticTrace.Write(audit);
                    if (windowsUpdateMessage is not null)
                        windowsUpdateMessage(line);
                    else if (task == "windows-repair")
                        Status(code == "0" && postRepair == IntegrityState.Healthy
                            ? "Reparo: integridade confirmada pela verificação posterior."
                            : postRepair == IntegrityState.Repairable
                                ? "Reparo: problemas persistem; confira o diagnóstico, sem repetir automaticamente."
                                : "Reparo: estado inconclusivo; confira o log antes de nova ação.");
                    else if (task == "cleanmgr-auto" && code == "0")
                        Status("Limpeza de Disco encerrada · consulte o espaço livre e a prévia.");
                    else Status(code == "0" ? "Operação concluída" : "Operação concluída com avisos");
                    gotResult = true;
                }
            }
            if (!gotResult)
            {
                if (task == "windows-check") _windowsRepairable = false;
                if (SafeMaintenanceAudit.NoReturn(task) is { } audit)
                    DiagnosticTrace.Write(audit);
                Status("Operação interrompida ou sem retorno");
                Log("A tarefa foi interrompida antes de enviar o resultado.");
            }
        }
        return null;
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
        "clean-windows-temp", "clear-update-cache", "cleanmgr-auto", "windows-check", "windows-repair",
        "flush-dns", "network-repair-winsock", "network-reset", "hibernate-on", "hibernate-off",
        "power-plan-balanced", "power-plan-saver", "power-plan-ultimate", "create-restore-point"
    };

    public static async Task<int> RunAsync(string task, string pipeName, string token)
    {
        bool updateTask = TryParseUpdateTask(task, out var updateIds);
        bool restorePower = TryParseRestorePowerTask(task, out _);
        bool optimizeVolume = SingleVolumeOptimization.TryParseTask(task, out _);
        bool residualTask = task == ResidualElevatedProtocol.TaskName;
        if ((!Allowed.Contains(task) && !updateTask && !restorePower && !optimizeVolume && !residualTask) || !Regex.IsMatch(pipeName, @"^GA5122_[a-f0-9]{32}$") ||
            !Regex.IsMatch(token, @"^[a-f0-9]{32}$")) return 2;
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await pipe.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096) { AutoFlush = true };
            void Send(string kind, string message)
            {
                lock (writer)
                    writer.WriteLine(kind + "|" + message.Replace('\r', ' ').Replace('\n', ' ').TakeText(750));
            }
            Send("HELLO", token);
            if (residualTask)
                return await ResidualElevatedProtocol.ServeAsync(pipe, async () =>
                {
                    var inventory = await Task.Run(AppServices.ScanAllInstalledAppsForSafety);
                    return new UninstallSnapshot(inventory.Items, !inventory.Incomplete);
                });
            if (CleanupTarget.FromTask(task) is { Elevated: true } target)
                return await ElevatedCleanupProtocol.RunWorkerAsync(pipe, target.Clean, Send);
            int exit;
            try
            {
                if (updateTask)
                {
                    var result = await WindowsUpdateClient.InstallAsync(updateIds,
                        phase => Send("RESULT", WindowsUpdateOperation.PhasePrefix + phase));
                    Send("RESULT", result.ToWire());
                    exit = result.ExitCode;
                }
                else
                {
                    exit = await ExecuteAsync(task, line =>
                    {
                        if (task == "windows-check" && line.StartsWith("WINCHECK:", StringComparison.Ordinal) ||
                            task == "windows-repair" && line.StartsWith("WINREPAIR:", StringComparison.Ordinal))
                            Send("RESULT", line);
                        else Send("LOG", line);
                    });
                }
            }
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

    internal static bool TryParseUpdateTask(string task, out HashSet<string> ids)
    {
        ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!task.StartsWith("wu-install:", StringComparison.Ordinal)) return false;
        string[] parts = task["wu-install:".Length..].Split(',');
        if (parts.Length is 0 or > WindowsUpdateOperation.MaxSelection) return false;
        foreach (string part in parts)
        {
            string? id = WindowsUpdateClient.CanonicalSelectedIdentity(part);
            if (id is null) return false;
            ids.Add(id);
        }
        return ids.Count == parts.Length && ids.Select(id => id[..36]).Distinct(StringComparer.Ordinal).Count() == parts.Length;
    }

    private static async Task<int> ExecuteAsync(string task, Action<string> log)
    {
        if (TryParseRestorePowerTask(task, out var powerId))
            return await PowerPlanService.RestorePreviousAsync(powerId, log);

        async Task<CommandResult> RunWithResult(string exe, params string[] args)
        {
            log("Executando: " + exe + " " + string.Join(" ", args));
            var result = await AppServices.RunCommandAsync(exe, args,
                s => { if (!string.IsNullOrWhiteSpace(s)) log(s.TakeText(350)); });
            log("Código de saída: " + result.ExitCode);
            return result;
        }

        async Task<int> Run(string exe, params string[] args) =>
            (await RunWithResult(exe, args)).ExitCode;

        log("Permissão administrativa concedida. Progresso exibido na janela principal.");
        if (SingleVolumeOptimization.TryParseTask(task, out string volume))
        {
            // Revalidar o alvo no worker elevado: o volume pode ter mudado
            // entre o clique e o consentimento UAC. Nunca otimizar todos.
            bool eligible = false;
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!string.Equals(drive.Name, volume + "\\", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    eligible = SingleVolumeOptimization.IsEligible(
                        drive.DriveType, drive.IsReady, drive.DriveFormat);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    eligible = false;
                }
                break;
            }
            if (!eligible)
            {
                log("Volume nao esta disponivel ou nao suporta otimizacao. Nenhuma unidade alterada.");
                return 2;
            }
            log("Otimizando somente a unidade " + volume +
                " com a politica adequada ao tipo de armazenamento do Windows.");
            return await Run("defrag.exe", volume, "/O", "/U");
        }
        switch (task)
        {
            case "cleanmgr-auto":
                log("Escolha as categorias diretamente na janela da Limpeza de Disco do Windows.");
                return await Run("cleanmgr.exe");
            case "windows-check":
            {
                log("Etapa 1/2: DISM /ScanHealth (sem reparo).");
                var dism = await AppServices.RunCommandAsync("dism.exe",
                    new[] { "/Online", "/Cleanup-Image", "/ScanHealth" },
                    line => { if (!string.IsNullOrWhiteSpace(line)) log(line.TakeText(350)); });
                log("Etapa 2/2: SFC /verifyonly (sem reparo).");
                var sfc = await AppServices.RunCommandAsync("sfc.exe",
                    new[] { "/verifyonly" },
                    line => { if (!string.IsNullOrWhiteSpace(line)) log(line.TakeText(350)); });
                var detail = WindowsIntegrityAssessment.Assess(
                    dism.ExitCode, dism.StandardOutput + dism.StandardError,
                    sfc.ExitCode, sfc.StandardOutput + sfc.StandardError);
                log("Resultado consultivo: " + detail.Summary);
                log("WINCHECK:" + detail.Overall.ToString().ToUpperInvariant());
                return 0; // inconclusivo nao libera reparo.
            }
            case "windows-repair":
            {
                // O check consultivo anterior não é autoridade permanente:
                // repetir o estado de reboot no próprio processo elevado,
                // imediatamente antes de qualquer DISM /RestoreHealth.
                var reboot = WindowsIntegrityAssessment.InspectRebootReadOnly();
                if (reboot != WindowsRepairRebootState.Clear)
                {
                    log(reboot == WindowsRepairRebootState.Pending
                        ? "Há reinicialização pendente do Windows. Reparo não iniciado; conclua o reinício manual e refaça o diagnóstico."
                        : "Não foi possível verificar a ausência de reinicialização pendente. Reparo não iniciado.");
                    log(WindowsIntegrityAssessment.PostRepairWire(IntegrityState.Inconclusive));
                    return 2;
                }
                return await WindowsFileRepair.RunAsync(async (exe, args) =>
                {
                    var result = await RunWithResult(exe, args);
                    return (result.ExitCode, result.StandardOutput + result.StandardError);
                }, log);
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
    }
}

internal static class NativeMethods
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}




// Marca GF da especificação visual; não cria ações de janela.
// Início usa o cabeçalho único do HTML, mantendo as ações reais de
// minimizar/fechar e a política nativa que impede maximização/tela cheia.
internal sealed class V269BrandHeader : TableLayoutPanel
{
    private V269BrandMark? _mark;
    private Label? _title, _subtitle;
    private FlowLayoutPanel? _chrome;
    private Font? _legacyTitle, _legacySubtitle;
    private Color _legacyTitleInk, _legacySubtitleInk;
    internal bool IsHomePresentation => _home;
    private readonly Dictionary<(float, FontStyle), Font> _fonts = new();
    private V269Theme _theme;
    private bool _home, _compactHeight;
    private int _width;
    private V269AppearanceOptions? _options;

    internal V269BrandHeader()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    internal void InitializeBrand(V269BrandMark mark, Label title, Label subtitle, FlowLayoutPanel chrome)
    {
        _mark = mark; _title = title; _subtitle = subtitle; _chrome = chrome;
        _legacyTitle = title.Font; _legacySubtitle = subtitle.Font;
        _legacyTitleInk = title.ForeColor; _legacySubtitleInk = subtitle.ForeColor;
    }

    internal void RefreshHomeTheme(V269AppearanceOptions options)
    {
        if (_home) Configure(true, _width, options);
    }

    internal void Configure(bool home, int width, V269AppearanceOptions options)
    {
        if (_mark is null || _title is null || _subtitle is null || _chrome is null) return;
        int screenHeight = home ? V269HomeMetrics.LogicalScreenHeight(this) : int.MaxValue;
        bool compactHeight = screenHeight <= 810;
        if (_options == options && _home == home && _width == width && _compactHeight == compactHeight) return;
        _options = options; _home = home; _width = width; _theme = options.Theme; _compactHeight = compactHeight;
        int D(int n) => Math.Max(1, (int)Math.Round(n * DeviceDpi / 96d));
        Font F(int pixels, FontStyle style)
        {
            float size = pixels * .75f * options.TextPercent / 100f;
            if (_fonts.TryGetValue((size, style), out var existing)) return existing;
            return _fonts[(size, style)] = new Font("Segoe UI", size, style);
        }
        SuspendLayout();
        try
        {
            _chrome.Visible = home;
            ColumnStyles[3].Width = home ? D((compactHeight ? 14 : 18) + 34 * 2 + 5) : 0;
            if (home)
            {
                bool compact = width <= 850;
                int logo = V269HomeMetrics.LogoSize(width, screenHeight);
                int horizontal = compactHeight ? 14 : 18;
                _mark.LogicalSize = logo;
                ColumnStyles[0].Width = D(horizontal + logo);
                _mark.Margin = new Padding(D(horizontal), 0, 0, 0);
                RowStyles[0].SizeType = SizeType.Percent; RowStyles[0].Height = 54;
                RowStyles[1].SizeType = SizeType.Percent; RowStyles[1].Height = 46;
                _title.Margin = new Padding(D(compact ? 9 : 14), 0, 0, 0);
                _subtitle.Margin = _title.Margin;
                _title.TextAlign = ContentAlignment.BottomLeft;
                _subtitle.TextAlign = ContentAlignment.TopLeft;
                _title.Font = F(compact ? 17 : width <= 1250 ? 20 : 23, FontStyle.Bold);
                _subtitle.Font = F(compact ? 10 : 12, FontStyle.Regular);
                _title.BackColor = _subtitle.BackColor = Color.Transparent;
                _title.ForeColor = ColorTranslator.FromHtml(_theme switch {
                    V269Theme.Dark => "#e0e3e6", V269Theme.Claro => "#17354e", _ => "#e4edf6" });
                _subtitle.ForeColor = ColorTranslator.FromHtml(_theme switch {
                    V269Theme.Dark => "#a5afb8", V269Theme.Claro => "#3f627a", _ => "#becddd" });
                _chrome.Padding = new Padding(0, D(compactHeight ? 8 : 12), D(horizontal), 0);
                foreach (Button button in _chrome.Controls.OfType<Button>())
                {
                    button.Size = new Size(D(34), D(28));
                    // gap de 5px somente entre os botões; a margem
                    // direita pertence ao cabeçalho, não ao último botão.
                    button.Margin = new Padding(0, 0,
                        button.Name == "V269HomeClose" ? 0 : D(5), 0);
                    button.BackColor = ColorTranslator.FromHtml(_theme switch {
                        V269Theme.Dark => "#1b1d20", V269Theme.Claro => "#d8e9f7", _ => "#102234" });
                    button.ForeColor = _title.ForeColor;
                }
            }
            else
            {
                ColumnStyles[0].Width = 66;
                RowStyles[0].SizeType = SizeType.Absolute; RowStyles[0].Height = 30;
                RowStyles[1].SizeType = SizeType.Absolute; RowStyles[1].Height = 22;
                _mark.LogicalSize = 55;
                _mark.Margin = new Padding(11, 0, 0, 0);
                _title.Margin = _subtitle.Margin = new Padding(13, 0, 0, 0);
                _title.TextAlign = _subtitle.TextAlign = ContentAlignment.MiddleLeft;
                if (_legacyTitle is not null) _title.Font = _legacyTitle;
                if (_legacySubtitle is not null) _subtitle.Font = _legacySubtitle;
                _title.ForeColor = _legacyTitleInk; _subtitle.ForeColor = _legacySubtitleInk;
            }
        }
        finally { ResumeLayout(true); Invalidate(true); }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (!_home || Width <= 1 || Height <= 1) { base.OnPaintBackground(e); return; }
        Color H(string hex) => ColorTranslator.FromHtml(hex);
        var (first, last, border, angle) = _theme switch
        {
            V269Theme.Dark => (H("#292c30"), H("#16181b"), H("#41464b"), 25f),
            V269Theme.Claro => (H("#e6f2fc"), H("#d8e9f7"), H("#a2c2d8"), 30f),
            _ => (H("#13293f"), H("#0a1826"), H("#1f3b52"), 90f)
        };
        using var gradient = new System.Drawing.Drawing2D.LinearGradientBrush(
            ClientRectangle, first, last, angle);
        e.Graphics.FillRectangle(gradient, ClientRectangle);
        using var line = new Pen(border);
        e.Graphics.DrawLine(line, 0, Height - 1, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var font in _fonts.Values) font.Dispose();
            _fonts.Clear();
        }
    }
}

internal sealed class V269BrandMark : Control
{
    private readonly Func<V269Theme> _theme;
    internal int LogicalSize = 55;
    internal V269BrandMark(Func<V269Theme> theme)
    {
        _theme = theme;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int n = Math.Min(Math.Min(Width,Height)-2,Math.Max(1,LogicalSize*DeviceDpi/96));
        if(n < 8) return;
        var r = new Rectangle((Width-n)/2,(Height-n)/2,n,n);
        Color H(string hex) => ColorTranslator.FromHtml(hex);
        var (top,bottom,border) = _theme() switch {
            V269Theme.Dark => (H("#6d747b"),H("#3a4046"),H("#a7b2ba")),
            V269Theme.Claro => (H("#2271aa"),H("#124a79"),H("#58a8d8")),
            _ => (H("#1a7bb0"),H("#0d57b0"),H("#58a8d8")) };
        using var path = V269Shell.Rounded(r,13);
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(r,top,bottom,135f);
        using var pen = new Pen(border);
        g.FillPath(brush,path); g.DrawPath(pen,path);
        using var font = new Font("Segoe UI",n*0.45f,FontStyle.Bold,GraphicsUnit.Pixel);
        TextRenderer.DrawText(g,"GF",font,r,Color.White,TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}
