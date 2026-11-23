using Windows.Foundation;
using Windows.Management.Deployment;

namespace GerenciadorAplicativos;

// VF06/C1: identidade nativa de UM pacote do usuario atual. Nunca e um comando
// arbitrario, uma receita de atualizacao, nem permissao para remover pacotes
// do sistema ou de outros perfis.
internal sealed record MsixRemovalRequest(
    string PackageFullName, string PackageFamilyName, string PackageName,
    string PublisherId, string Version, string DisplayName, string InventoryId, bool? SystemSigned);

internal enum MsixRemovalState { Blocked, Confirmed, Pending }
internal sealed record MsixNativeRemoval(bool Completed, bool Started, int? ErrorCode = null);
internal sealed record MsixRemovalResult(MsixRemovalState State, string Reason, int? ErrorCode = null);
internal sealed record MsixUserFeedback(string Stage, string Explanation, bool RefreshInventory);

internal static class MsixUninstall
{
    // O escopo inicial e propositalmente restrito a apps de terceiros:
    // aplicativos Microsoft e componentes internos continuam via Windows.
    private const string MicrosoftPublisherId = "8wekyb3d8bbwe";

    internal static bool CanOffer(MsixRemovalRequest request) =>
        request.SystemSigned is false &&
        !string.IsNullOrWhiteSpace(request.PackageFullName) &&
        !string.IsNullOrWhiteSpace(request.PackageFamilyName) &&
        !string.IsNullOrWhiteSpace(request.PackageName) &&
        !string.IsNullOrWhiteSpace(request.PublisherId) &&
        !string.IsNullOrWhiteSpace(request.Version) &&
        request.PackageFullName.Length <= 512 &&
        request.PackageFamilyName.Length <= 512 &&
        request.InventoryId.Equals("MSIX|" + request.PackageFamilyName, StringComparison.Ordinal) &&
        !request.PublisherId.Equals(MicrosoftPublisherId, StringComparison.OrdinalIgnoreCase) &&
        !request.PackageName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) &&
        !request.PackageName.StartsWith("Windows.", StringComparison.OrdinalIgnoreCase) &&
        !request.PackageName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) &&
        !ProtectedApplications.IsProtectedName(request.DisplayName) &&
        !ProtectedApplications.IsExcludedFromManagement(request.DisplayName);

    private static bool Complete(LocalMsixResult scan) =>
        scan.Status == "ok" && scan.Failures == 0 &&
        scan.Items.All(item => !item.Stale);

    private static bool Matches(MsixRemovalRequest request, LocalMsixApp app) =>
        !app.Stale && app.SystemSigned is false &&
        app.PackageFullName.Equals(request.PackageFullName, StringComparison.Ordinal) &&
        app.PackageFamilyName.Equals(request.PackageFamilyName, StringComparison.Ordinal) &&
        app.PackageName.Equals(request.PackageName, StringComparison.Ordinal) &&
        app.PublisherId.Equals(request.PublisherId, StringComparison.Ordinal) &&
        app.Version.Equals(request.Version, StringComparison.Ordinal) &&
        app.Name.Equals(request.DisplayName, StringComparison.Ordinal);

    internal static bool CanStart(MsixRemovalRequest request, LocalMsixResult scan) =>
        CanOffer(request) && Complete(scan) &&
        scan.ObservedMainPackageFullNames is [var fullName] &&
        fullName.Equals(request.PackageFullName, StringComparison.Ordinal) &&
        scan.Items.Count(x => Matches(request, x)) == 1;

    internal static async Task<MsixRemovalResult> ExecuteAsync(
        MsixRemovalRequest request, Func<LocalMsixResult> freshInventory,
        Func<string, Task<MsixNativeRemoval>> nativeRemove)
    {
        // Segundos antes de chamar o Windows, refazer a observacao do usuario
        // atual; snapshots retidos ou listas parciais nao autorizam exclusao.
        LocalMsixResult before;
        try { before = freshInventory(); }
        catch { return new(MsixRemovalState.Blocked, "preflight_unavailable"); }
        if (!CanStart(request, before))
            return new(MsixRemovalState.Blocked, "identity_or_inventory_changed");

        MsixNativeRemoval native;
        try { native = await nativeRemove(request.PackageFullName); }
        catch (Exception ex)
        {
            // Um erro apos entrar no executor nao prova que o Windows nao agiu.
            native = new(false, true, ex.HResult);
        }

        LocalMsixResult after;
        try { after = freshInventory(); }
        catch { return new(MsixRemovalState.Pending, "postcheck_unavailable", native.ErrorCode); }
        if (!Complete(after) || after.ObservedMainPackageFullNames is not { } observedPackages)
            return new(MsixRemovalState.Pending, "postcheck_partial", native.ErrorCode);

        bool sameFamilyPresent = observedPackages.Count > 0;
        // Início nativo não confirmado e família ainda presente são distintos
        // de uma operação iniciada e inconclusiva; não presumir zero efeitos.
        if (!native.Started && sameFamilyPresent)
            return new(MsixRemovalState.Blocked, "native_start_not_observed_family_present", native.ErrorCode);
        if (native.Completed && native.Started && !sameFamilyPresent)
            return new(MsixRemovalState.Confirmed, "native_completed_and_family_absent");

        // Sucesso do retorno nativo sem ausencia do pacote nao e confirmacao.
        // Mesmo um erro seguido de ausencia continua inconclusivo.
        return new(MsixRemovalState.Pending,
            sameFamilyPresent ? "family_still_present" : "native_result_uncertain",
            native.ErrorCode);
    }

    // Fonte única do estado e da mensagem: falha no diário final nunca
    // anuncia uma conclusão auditável nem permite recarga automática.
    internal static MsixUserFeedback DescribeForUi(MsixRemovalResult outcome, bool resultRecorded)
    {
        if (!resultRecorded)
            return new("pending",
                "MSIX: não foi possível registrar o resultado no diário. " +
                (outcome.State == MsixRemovalState.Confirmed
                    ? "O Windows informou conclusão e a consulta não encontrou a família, porém a confirmação auditável ficou pendente. "
                    : "O resultado operacional permanece sem registro auditável. ") +
                "Confira a situação nas Configurações antes de tentar novamente.", false);
        return outcome.State switch
        {
            MsixRemovalState.Confirmed => new("finished",
                "MSIX removido. Nenhum pacote principal da família permanece no usuário atual.", true),
            MsixRemovalState.Blocked when outcome.Reason == "native_start_not_observed_family_present" =>
                new("failed",
                    "MSIX: o início da remoção não foi confirmado pelo Windows; a família continua presente. " +
                    "Confira nas Configurações antes de qualquer nova tentativa.", false),
            MsixRemovalState.Blocked => new("failed",
                "MSIX não enviado: identidade ou inventário alterado.", false),
            _ => new("pending",
                "Resultado MSIX inconclusivo; não repita até confirmar nas Configurações.", false)
        };
    }

    internal static async Task<MsixNativeRemoval> RemoveCurrentUserAsync(string fullName)
    {
        // API nativa nao elevada: somente usuario atual. Sem RemoveForAllUsers,
        // PowerShell, comandos de shell ou alteracao de dependencias a mao.
        bool started = false;
        try
        {
            var operation = new PackageManager().RemovePackageAsync(fullName);
            started = true;
            var complete = new TaskCompletionSource<MsixNativeRemoval>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            operation.Completed = (o, state) =>
            {
                try
                {
                    if (state == AsyncStatus.Completed)
                    {
                        var result = o.GetResults();
                        var error = result.ExtendedErrorCode?.HResult;
                        complete.TrySetResult(new(error is null or 0, true, error));
                    }
                    else if (state == AsyncStatus.Canceled)
                        complete.TrySetResult(new(false, true));
                    else
                    {
                        int? error = null;
                        try { error = o.GetResults().ExtendedErrorCode?.HResult; }
                        catch (Exception ex) { error = ex.HResult; }
                        complete.TrySetResult(new(false, true, error));
                    }
                }
                catch (Exception ex)
                { complete.TrySetResult(new(false, true, ex.HResult)); }
            };
            try { return await complete.Task; }
            finally { GC.KeepAlive(operation); }
        }
        catch (Exception ex) { return new(false, started, ex.HResult); }
    }
}
