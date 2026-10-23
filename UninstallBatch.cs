using System.ComponentModel;
namespace GerenciadorAplicativos;

internal sealed record UninstallRequest(ProgramEntry Original, ParsedUninstallCommand Command);
internal sealed record UninstallSnapshot(IReadOnlyCollection<ProgramEntry> Items, bool Complete);
internal enum UninstallState { Refused, Blocked, Cancelled, StartFailed, ProcessFailed, Pending, StillPresent, RegistrationAbsent }
internal sealed record UninstallOutcome(UninstallRequest Request, UninstallState State, int? Code = null, string Reason = "");

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
                {
                    // Preferir silêncio somente quando um contrato local estrito
                    // aprova a derivação do comando. Nunca executar flags remotas.
                    parsed = SilentUninstallPolicy.TrySelect(app, candidate, out var silent)
                        ? silent : candidate;
                    break;
                }
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
                if(request.Command.ProductCode is null && before.Items.Any(other =>
                    !other.InventoryId.Equals(request.Original.InventoryId,StringComparison.OrdinalIgnoreCase) &&
                    SharesExeIntent(request.Command,other)))
                {
                    results.Add(new(request,UninstallState.Blocked,Reason:
                        "Mais de um registro aponta para o mesmo desinstalador. Não é possível remover só a entrada selecionada com segurança; confira o aplicativo pelo Windows."));
                    continue;
                }
                token.ThrowIfCancellationRequested();
                invoked=true;
                int? exit=await launch(request,token);
                if(exit is null) { results.Add(new(request,UninstallState.StartFailed)); continue; }
                if(request.Command.ProductCode is not null && exit==1602)
                { results.Add(new(request,UninstallState.Cancelled,exit,"Cancelada pelo usuário no instalador.")); break; }
                if(request.Command.ProductCode is not null && exit is 1604 or 1618 or 1641 or 3010)
                {
                    string reason=exit==1618 ? "Outra instalação ou remoção está em andamento. Aguarde antes de tentar novamente." :
                        exit is 1641 or 3010 ? "O instalador informou reinício. Confira o estado depois; o GF não solicitou reinicialização." :
                        "O Windows suspendeu a remoção; a conclusão permanece pendente.";
                    results.Add(new(request,UninstallState.Pending,exit,reason)); break;
                }
                int absent=0;
                bool sawAbsence=false, complete=true;
                for(int attempt=0;attempt<7;attempt++) {
                    token.ThrowIfCancellationRequested();
                    var after=await scan();
                    if(!after.Complete) { complete=false; break; }
                    absent=UninstallResiduals.StillInstalled(request.Original,after.Items) ? 0 : absent+1;
                    sawAbsence |= absent > 0;
                    // O fornecedor pode atualizar o Registro segundos ou minutos depois
                    // do processo inicial. Esta janela curta nao comprova
                    // falha definitiva nem autoriza um segundo lançamento.
                    if(attempt<6) await betweenChecks(token);
                }
                // Observar a janela inteira: dois registros ausentes por um
                // instante podem reaparecer durante a manutencao do fornecedor.
                // Saida nao zero exige verificar o estado, mas nunca autoriza
                // limpeza; 3010 exige reinicio e tambem permanece pendente.
                var state = complete && exit==0 && absent>=3 ? UninstallState.RegistrationAbsent :
                    complete && request.Command.ProductCode is not null && exit is not (0 or 3010) && !sawAbsence ? UninstallState.ProcessFailed :
                    UninstallState.Pending;
                results.Add(new(request,state,exit,request.Command.ProductCode is null && exit!=0 ?
                    "O desinstalador retornou um código não padronizado. A conclusão permanece pendente; processos auxiliares ainda podem estar trabalhando." : ""));
            }
            catch(Win32Exception ex) {
                results.Add(new(request,ex.NativeErrorCode==1223 ? UninstallState.Cancelled : UninstallState.StartFailed,ex.NativeErrorCode));
                if(ex.NativeErrorCode==1223) break;
            }
            catch(TimeoutException) { results.Add(new(request,UninstallState.Pending)); break; }
            catch(OperationCanceledException) { results.Add(new(request,invoked ? UninstallState.Pending : UninstallState.Cancelled)); break; }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
            { results.Add(new(request,invoked ? UninstallState.Pending : UninstallState.Blocked)); }
            // Um processo filho pode continuar trabalhando depois do lancador.
            // Nao sobrepor outro desinstalador quando a conclusao e desconhecida.
            if(results.LastOrDefault()?.State==UninstallState.Pending) break;
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
        SilentUninstallPolicy.MatchesAuthorized(request.Original, request.Command);

    private static bool SharesExeIntent(ParsedUninstallCommand selected, ProgramEntry other) =>
        new[]{other.UninstallString,other.QuietUninstallString}.Any(raw =>
            UninstallCommand.TryParse(raw,out var command) && command.ProductCode is null &&
            command.FileName.Replace('/','\\').Equals(selected.FileName.Replace('/','\\'),StringComparison.OrdinalIgnoreCase) &&
            command.Arguments.Equals(selected.Arguments,StringComparison.Ordinal));
}
