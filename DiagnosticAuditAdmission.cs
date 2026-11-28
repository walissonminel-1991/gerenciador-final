namespace GerenciadorAplicativos;

// Sessão única. Falha de auditoria é terminal para novas operações mutáveis;
// uma gravação diagnóstica posterior bem-sucedida não reautoriza manutenção.
// O chamador ainda deve preservar os gates de consentimento, identidade e recuperação.
internal sealed class DiagnosticAuditAdmission(Func<string, bool> durableWrite)
{
    internal const string MutationMarker = "AUDITORIA.OPERACAO_AUTORIZACAO_REGISTRADA";
    private const int Pending = 0, Ready = 1, Degraded = 2;
    private readonly object _gate = new();
    private int _state;

    internal bool IsReady => Volatile.Read(ref _state) == Ready;
    internal bool IsDegraded => Volatile.Read(ref _state) == Degraded;

    // Pode ser chamado de dentro do lock do escritor: não adquirir outro lock.
    internal void ObserveFailure() => Interlocked.Exchange(ref _state, Degraded);

    internal bool CompleteStartup(bool traceStarted)
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _state) != Pending) return IsReady;
            if (!traceStarted) { ObserveFailure(); return false; }
            if (!WriteRequired("APP.INICIALIZACAO_INICIO")) return false;
            // Uma falha concorrente nunca pode ser sobrescrita por Ready.
            return Interlocked.CompareExchange(ref _state, Ready, Pending) == Pending;
        }
    }

    internal bool TryAuthorizeMutation()
    {
        lock (_gate)
        {
            if (!IsReady) return false;
            // O marcador não é consentimento nem identificação de alvo. Deve
            // ser chamado imediatamente antes do efeito, após os gates existentes.
            return WriteRequired(MutationMarker) && IsReady;
        }
    }

    private bool WriteRequired(string marker)
    {
        bool written = false;
        try { return written = durableWrite(marker); }
        finally { if (!written) ObserveFailure(); }
    }
}
