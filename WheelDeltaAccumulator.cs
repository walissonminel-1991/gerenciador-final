namespace GerenciadorAplicativos;

// Windows envia WHEEL_DELTA=120 por entalhe; dispositivos de alta resolucao
// podem emitir deltas parciais. Preservar o resto inclusive ao inverter direcao.
internal sealed class WheelDeltaAccumulator
{
    private const int WheelDelta = 120;
    private int _remainder;

    internal int Consume(int delta)
    {
        long total = (long)_remainder + delta;
        int notches = (int)(total / WheelDelta);
        _remainder = (int)(total % WheelDelta);
        return notches;
    }
}
