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

// Superfícies V2.6.9 recebem pixels proporcionais a cada evento, inclusive
// deltas inferiores a 120. Mantém a distância total de um entalhe e só
// acumula a fração de pixel. Uma instância por eixo evita misturar resíduos.
internal sealed class PixelWheelDeltaAccumulator
{
    private const int WheelDelta = 120;
    private long _remainder;
    private int _lastScale;

    internal int Move(int position, int maximum, int delta, int pixelsPerNotch)
    {
        if (maximum <= 0 || pixelsPerNotch <= 0)
        {
            _remainder = 0;
            _lastScale = 0;
            return 0;
        }
        if (_lastScale != pixelsPerNotch) _remainder = 0;
        _lastScale = pixelsPerNotch;
        long total = _remainder + (long)delta * pixelsPerNotch;
        long next = (long)position - total / WheelDelta;
        _remainder = total % WheelDelta;
        // Descartar pressão para fora do intervalo: inverter no limite não
        // exige compensar movimento que o usuário nunca viu.
        if ((next <= 0 && total > 0) || (next >= maximum && total < 0))
            _remainder = 0;
        return (int)System.Math.Clamp(next, 0L, (long)maximum);
    }
}
