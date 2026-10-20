namespace KillerPdf.Engine.Authoring;

/// <summary>A finite point in PDF user-space coordinates.</summary>
public readonly record struct PdfPoint
{
    /// <summary>Creates a point from finite horizontal and vertical coordinates.</summary>
    public PdfPoint(double x, double y)
    {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y));
        X = x;
        Y = y;
    }

    /// <summary>Gets the horizontal coordinate.</summary>
    public double X { get; }
    /// <summary>Gets the vertical coordinate.</summary>
    public double Y { get; }
}
