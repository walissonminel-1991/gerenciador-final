namespace GerenciadorAplicativos;

// Plano local puramente textual. Sem rede, backend, journal, WUA,
// downloads, EULA, alterações de drivers ou reinício.
internal static class DriverPilotPreview
{
    internal static string Format(DriverUpdatePilotChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return string.Join(Environment.NewLine,
            "SIMULAÇÃO TEXTUAL — NENHUMA ETAPA EXECUTADA",
            "Oferta de referência: " + choice.UpdateId + " / revisão " + choice.Revision,
            "Dispositivo de referência: " + choice.DeviceId,
            "Classe informada: " + choice.DriverClass,
            "1. Conferir origem WUA e identidade da oferta — NÃO EXECUTADO.",
            "2. Conferir identidade PnP, INF e classe de piloto — NÃO EXECUTADO.",
            "3. Verificar consentimento e reinício pendente — NÃO EXECUTADO.",
            "4. Validar EULA e ausência de interação — NÃO EXECUTADO.",
            "5. Validar pacote baixado e sua identidade — NÃO EXECUTADO.",
            "6. Registrar intenção durável e revalidar — NÃO EXECUTADO.",
            "7. Instalar somente em Windows descartável — NÃO EXECUTADO.",
            "8. Reconsultar PnP e reconciliar resultado — NÃO EXECUTADO.",
            "Sem versão numérica ofertada, ranking ou pacote remoto autenticado.",
            "Estes são passos hipotéticos para teste, não resultados de execução.",
            "Nenhuma rede, arquivo, journal ou alteração no Windows foi realizada.");
    }
}
