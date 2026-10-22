using System.ComponentModel;
namespace GerenciadorAplicativos;

internal sealed record UninstallRequest(ProgramEntry Original, ParsedUninstallCommand Command);
internal sealed record UninstallSnapshot(IReadOnlyCollection<ProgramEntry> Items, bool Complete);
internal enum UninstallState { Refused, Blocked, Cancelled, StartFailed, ProcessFailed, Pending, StillPresent, RegistrationAbsent }
internal sealed record UninstallOutcome(UninstallRequest Request, UninstallState State, int? Code = null);

internal sealed class UninstallBatch
{
    private int attempted;
    internal static UninstallRequest[] Select(IEnumerable<ProgramEntry> selection)
    {
        var requests=new List<UninstallRequest>();
        var seen=new Dictionary<string,ProgramEntry>(StringComparer.OrdinalIgnoreCase);
        foreach(var app in selection.ToArray()) {
            if(string.IsNullOrWhiteSpace(app.InventoryId) || app.InventoryOnly || ProtectedApplications.IsProtectedName(app.Name))
                throw new InvalidDataException("Seleção sem identidade desinstalável.");
            if(seen.TryGetValue(app.InventoryId,out var previous)) {
                if(!SameRegistration(previous,app)) throw new InvalidDataException("Seleção contém identidades contraditórias.");
                continue;
            }
            seen.Add(app.InventoryId,app);
            ParsedUninstallCommand? parsed=null;
            foreach(string command in new[]{app.UninstallString,app.QuietUninstallString})
                if(MaintenanceAudit.CheckUninstaller(command,false)=="available" &&
                    UninstallCommand.TryParse(command,out var candidate) && BoundToProduct(candidate,app))
                { parsed=candidate; break; }
            if(parsed is null) throw new InvalidDataException("Comando de desinstalação não verificável para a seleção.");
            requests.Add(new(app,parsed));
        }
        return requests.ToArray();
    }
    internal static bool SameRegistration(ProgramEntry a, ProgramEntry b) =>
        !b.InventoryOnly && !ProtectedApplications.IsProtectedName(b.Name) &&
        a.InventoryId.Equals(b.InventoryId,StringComparison.OrdinalIgnoreCase) &&
        a.Name==b.Name && a.Publisher==b.Publisher && a.Version==b.Version &&
        a.InstallLocation==b.InstallLocation && a.MsiProductCode==b.MsiProductCode &&
        a.UninstallString==b.UninstallString && a.QuietUninstallString==b.QuietUninstallString;

    internal async Task<IReadOnlyList<UninstallOutcome>> RunAsync(IReadOnlyList<UninstallRequest> selected,
        bool consent, Func<Task<UninstallSnapshot>> scan,
        Func<UninstallRequest,CancellationToken,Task<int?>> launch,
        Func<CancellationToken,Task> betweenChecks, CancellationToken token=default)
    {
        var requests=selected.ToArray(); // seleção confirmada não é coleção viva da UI
        var results=new List<UninstallOutcome>();
        if(Interlocked.Exchange(ref attempted,1)!=0) return results;
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var request in requests) {
            if(!consent) { results.Add(new(request,UninstallState.Refused)); continue; }
            if(!seen.Add(request.Original.InventoryId)) continue;
            bool invoked=false;
            try {
                token.ThrowIfCancellationRequested();
                var before=await scan();
                var matching=before.Items.Where(x=>x.InventoryId.Equals(request.Original.InventoryId,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(!before.Complete || matching.Length!=1 || !SameRegistration(request.Original,matching[0]) ||
                    !BoundToProduct(request.Command,matching[0]) ||
                    !MatchesRegisteredCommand(request) ||
                    (request.Command.ProductCode is not null && before.Items.Count(x =>
                        x.MsiProductCode.Equals(request.Command.ProductCode,StringComparison.OrdinalIgnoreCase)) != 1))
                { results.Add(new(request,UninstallState.Blocked)); continue; }
                token.ThrowIfCancellationRequested();
                invoked=true;
                int? exit=await launch(request,token);
                if(exit is null) { results.Add(new(request,UninstallState.StartFailed)); continue; }
                if(exit is not (0 or 3010)) { results.Add(new(request,UninstallState.ProcessFailed,exit)); continue; }
                int absent=0;
                for(int attempt=0;attempt<7;attempt++) {
                    token.ThrowIfCancellationRequested();
                    var after=await scan();
                    if(!after.Complete) { results.Add(new(request,UninstallState.Pending,exit)); break; }
                    absent=UninstallResiduals.StillInstalled(request.Original,after.Items) ? 0 : absent+1;
                    if(absent==2) { results.Add(new(request,UninstallState.RegistrationAbsent,exit)); break; }
                    // O fornecedor pode atualizar o Registro segundos ou minutos depois
                    // do processo inicial. Esta janela curta nao comprova
                    // falha definitiva nem autoriza um segundo lançamento.
                    if(attempt==6) results.Add(new(request,UninstallState.Pending,exit));
                    else await betweenChecks(token);
                }
            }
            catch(Win32Exception ex) {
                results.Add(new(request,ex.NativeErrorCode==1223 ? UninstallState.Cancelled : UninstallState.StartFailed,ex.NativeErrorCode));
                if(ex.NativeErrorCode==1223) break;
            }
            catch(TimeoutException) { results.Add(new(request,UninstallState.Pending)); break; }
            catch(OperationCanceledException) { results.Add(new(request,invoked ? UninstallState.Pending : UninstallState.Cancelled)); break; }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
            { results.Add(new(request,UninstallState.Blocked)); }
        }
        return results;
    }
    internal static bool BoundToProduct(ParsedUninstallCommand command, ProgramEntry entry)
    {
        if(entry.InventoryOnly || ProtectedApplications.IsProtectedName(entry.Name)) return false;
        return command.ProductCode is null || Guid.TryParse(entry.MsiProductCode,out var expected) &&
            Guid.TryParse(command.ProductCode,out var actual) && expected==actual;
    }
    private static bool MatchesRegisteredCommand(UninstallRequest request) =>
        UninstallCommand.TryParse(request.Original.UninstallString,out var normal) && normal==request.Command ||
        MatchesQuiet(request);
    private static bool MatchesQuiet(UninstallRequest request) =>
        UninstallCommand.TryParse(request.Original.QuietUninstallString,out var quiet) && quiet==request.Command;
}
