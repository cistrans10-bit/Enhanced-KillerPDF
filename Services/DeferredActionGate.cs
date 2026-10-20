namespace KillerPDF.Services;

internal sealed class DeferredActionGate
{
    private int _generation;

    internal int Begin() => unchecked(++_generation);
    internal void Cancel()
    {
        unchecked { _generation++; }
    }
    internal bool IsCurrent(int generation) => generation == _generation;
}
