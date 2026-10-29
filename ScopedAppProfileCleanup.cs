using System.Windows.Forms;

namespace GerenciadorAplicativos;

// Limpador expressamente autorizado por produto, nunca por correspondencia de palavras.
// So perfis pessoais AppData e uma chave propria conhecida. Nao toca HKLM,
// Windows, Program Files, AppCompat, UFH, drivers ou chaves de outro aplicativo.
internal static class ScopedAppProfileCleanup
{
    internal sealed record Scope(string Product, string[] Profiles, string[] Processes, string RegistrySubKey);
    internal static Scope? Recognize(string? product)
    {
        if (string.IsNullOrWhiteSpace(product)) return null;
        if (ResidualAudit.TrackedApplicationStillPresent("foobar2000", [product]))
            return new("foobar2000", ["foobar2000", "foobar2000-v2"], ["foobar2000"], "foobar2000");
        if (ResidualAudit.TrackedApplicationStillPresent("OBS Studio", [product]))
            return new("OBS Studio", ["obs-studio"], ["obs64", "obs32"], "OBS Studio");
        return null;
    }

    internal const string PreservationReason =
        "Dados preservados. A limpeza de perfis está suspensa nesta candidata: " +
        "quantidade e tamanho dos arquivos não comprovam que o conteúdo e o Registro " +
        "continuam iguais aos da prévia. Uma análise geral parcial também não autoriza exclusão. " +
        "Nenhum arquivo ou chave de Registro foi apagado.";

    internal static Task<string> ConfirmAndCleanAsync(Form owner, string product, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(Recognize(product) is null
            ? "Não há uma política segura para limpar este aplicativo. Dados preservados."
            : PreservationReason);
    }
}
