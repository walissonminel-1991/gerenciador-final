using System.ComponentModel;
using System.Diagnostics;
namespace GerenciadorAplicativos;

internal sealed record UninstallRequest(ProgramEntry Original, ParsedUninstallCommand Command);
internal sealed record UninstallSnapshot(IReadOnlyCollection<ProgramEntry> Items, bool Complete);
internal enum UninstallState { Refused, Blocked, Cancelled, StartFailed, ProcessFailed, Pending, StillPresent, RegistrationAbsent }
internal sealed record UninstallOutcome(UninstallRequest Request, UninstallState State, int? Code = null, string Reason = "",
    string DiagnosticReason = "", long DurationMilliseconds = 0);

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
            // O valor QuietUninstallString do Registro deve preceder UninstallString:
            // a ordem antiga abria a GUI do Notepad++ mesmo com /S registrado.
            // Rejeitar comandos quiet que apontam para outro executavel/produto.
            foreach(string command in new[]{app.QuietUninstallString,app.UninstallString})
                if(MaintenanceAudit.CheckUninstaller(command,false)=="available" &&
                    UninstallCommand.TryParse(command,out var candidate) && BoundToProduct(candidate,app) &&
                    (!string.Equals(command, app.QuietUninstallString, StringComparison.Ordinal) ||
                        SilentUninstallPolicy.RegisteredQuietMatchesProduct(app, candidate)))
                {
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
        Func<CancellationToken,Task> betweenChecks, CancellationToken token=default,
        Action<UninstallRequest, string>? progress = null)
    {
        var requests=selected.ToArray(); // seleção confirmada não é coleção viva da UI
        var results=new List<UninstallOutcome>();
        if(Interlocked.Exchange(ref attempted,1)!=0) return results;
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var request in requests) {
            if(!consent) { results.Add(new(request,UninstallState.Refused)); continue; }
            if(!seen.Add(request.Original.InventoryId)) continue;
            bool invoked=false, installerReturned=false;
            var duration = Stopwatch.StartNew();
            try {
                token.ThrowIfCancellationRequested();
                progress?.Invoke(request, "preparing");
                var before=await scan();
                token.ThrowIfCancellationRequested();
                var matching=before.Items.Where(x=>x.InventoryId.Equals(request.Original.InventoryId,StringComparison.OrdinalIgnoreCase)).ToArray();
                if(!before.Complete || matching.Length!=1 || !SameRegistration(request.Original,matching[0]) ||
                    !BoundToProduct(request.Command,matching[0]) ||
                    !MatchesRegisteredCommand(request) ||
                    (request.Command.ProductCode is not null && before.Items.Count(x =>
                        x.MsiProductCode.Equals(request.Command.ProductCode,StringComparison.OrdinalIgnoreCase)) != 1))
                { results.Add(new(request,UninstallState.Blocked, DiagnosticReason:
                    !before.Complete ? "inventory_incomplete" : matching.Length != 1 || !SameRegistration(request.Original, matching[0])
                    ? "registration_changed" : "command_or_product_mismatch")); continue; }
                // PC FORTE: 7-Zip EXE e MSI compartilharam arquivos e a remoção
                // da variante EXE comprometeu a instalação MSI ainda registrada.
                // Não executar nenhuma delas isoladamente enquanto houver outra
                // edição registrada. Sem tentativa de limpeza ou correção forçada.
                if (request.Original.Name.StartsWith("7-Zip", StringComparison.OrdinalIgnoreCase) &&
                    before.Items.Count(other => other.Name.StartsWith("7-Zip", StringComparison.OrdinalIgnoreCase)) > 1)
                {
                    results.Add(new(request, UninstallState.Blocked, DiagnosticReason: "shared_editions", Reason:
                        "Há mais de uma edição do 7-Zip registrada. As instalações podem compartilhar arquivos; " +
                        "não é seguro desinstalar uma isoladamente pelo GF."));
                    continue;
                }
                if(request.Command.ProductCode is null && before.Items.Any(other =>
                    !other.InventoryId.Equals(request.Original.InventoryId,StringComparison.OrdinalIgnoreCase) &&
                    SharesExeIntent(request,other)))
                {
                    results.Add(new(request,UninstallState.Blocked,DiagnosticReason: "shared_uninstaller", Reason:
                        "Mais de um registro aponta para o mesmo desinstalador. Não é possível remover só a entrada selecionada com segurança; confira o aplicativo pelo Windows."));
                    continue;
                }
                token.ThrowIfCancellationRequested();
                invoked=true;
                progress?.Invoke(request, "working");
                int? exit=await launch(request,token);
                if(exit is null) { results.Add(new(request,UninstallState.StartFailed)); continue; }
                installerReturned=true;
                if(request.Command.ProductCode is not null && exit==1602)
                { results.Add(new(request,UninstallState.Cancelled,exit,"Cancelada pelo usuário no instalador.")); break; }
                if(request.Command.ProductCode is not null && exit is 1604 or 1618 or 1641 or 3010)
                {
                    string reason=exit==1618 ? "Outra instalação ou remoção está em andamento. Aguarde antes de tentar novamente." :
                        exit is 1641 or 3010 ? "O instalador informou reinício. Confira o estado depois; o GF não solicitou reinicialização." :
                        "O Windows suspendeu a remoção; a conclusão permanece pendente.";
                    results.Add(new(request,UninstallState.Pending,exit,reason)); break;
                }
                progress?.Invoke(request, "confirming");
                int absent=0;
                bool sawAbsence=false, complete=true;
                for(int attempt=0;attempt<7;attempt++) {
                    token.ThrowIfCancellationRequested();
                    var after=await scan();
                    token.ThrowIfCancellationRequested();
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
                // Falha posterior não autoriza repetir nem oferecer limpeza.
                results.Add(new(request,installerReturned ? UninstallState.Pending :
                    ex.NativeErrorCode==1223 ? UninstallState.Cancelled : UninstallState.StartFailed,
                    ex.NativeErrorCode, Reason: installerReturned ? "A conferência posterior falhou; não repita a operação antes de confirmar o resultado." :
                        WindowsExecutionFailure.Describe(ex.NativeErrorCode) ?? "O Windows impediu o início do desinstalador.",
                    DiagnosticReason: installerReturned ? "post_launch_inventory_failure" :
                        WindowsExecutionFailure.IsAntivirus(ex.NativeErrorCode) ? "antivirus_block" : "launch_win32_failure"));
                if(installerReturned || ex.NativeErrorCode==1223) break;
            }
            catch(TimeoutException) { results.Add(new(request,UninstallState.Pending, DiagnosticReason: "provider_timeout")); break; }
            catch(OperationCanceledException) { results.Add(new(request,invoked ? UninstallState.Pending : UninstallState.Cancelled,
                DiagnosticReason: "cancelled")); break; }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
            { results.Add(new(request,invoked ? UninstallState.Pending : UninstallState.Blocked,
                ((uint)ex.HResult & 0xFFFF0000u) == 0x80070000u ? ex.HResult & 0xFFFF : 0,
                DiagnosticReason: "uninstall_validation_or_inventory_failure")); }
            finally
            {
                if (results.LastOrDefault() is { } outcome && outcome.Request == request)
                    results[^1] = outcome with { DurationMilliseconds = duration.ElapsedMilliseconds };
            }
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

    private static bool SharesExeIntent(UninstallRequest selected, ProgramEntry other)
    {
        var intents = new List<string> { SilentUninstallPolicy.ArgumentIntent(selected.Command.Arguments) };
        if (UninstallCommand.TryParse(selected.Original.UninstallString, out var original) &&
            original.FileName.Equals(selected.Command.FileName, StringComparison.OrdinalIgnoreCase))
            intents.Add(SilentUninstallPolicy.ArgumentIntent(original.Arguments));
        return new[] { other.UninstallString, other.QuietUninstallString }.Any(raw =>
            UninstallCommand.TryParse(raw, out var command) && command.ProductCode is null &&
            command.FileName.Replace('/', '\\').Equals(selected.Command.FileName.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase) &&
            intents.Contains(SilentUninstallPolicy.ArgumentIntent(command.Arguments), StringComparer.Ordinal));
    }
}
