namespace KillerPDF.Services;

internal static class CombFieldLayout
{
    internal static double CellLeft(double width, int cellCount, int index)
    {
        if (!double.IsFinite(width) || width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellCount);
        if ((uint)index >= (uint)cellCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        return width / cellCount * index;
    }

    internal static int CellIndexAt(double x, double width, int cellCount)
    {
        if (!double.IsFinite(width) || width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellCount);
        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x));
        return Math.Clamp((int)Math.Floor(x / (width / cellCount)), 0, cellCount - 1);
    }
}
