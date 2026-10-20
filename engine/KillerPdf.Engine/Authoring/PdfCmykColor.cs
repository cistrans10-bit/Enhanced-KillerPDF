namespace KillerPdf.Engine.Authoring;

/// <summary>A device CMYK color whose components range from zero through one.</summary>
public readonly record struct PdfCmykColor
{
    /// <summary>Creates a color from normalized cyan, magenta, yellow, and black components.</summary>
    public PdfCmykColor(double cyan, double magenta, double yellow, double black)
    {
        Cyan = Component(cyan, nameof(cyan));
        Magenta = Component(magenta, nameof(magenta));
        Yellow = Component(yellow, nameof(yellow));
        Black = Component(black, nameof(black));
    }

    /// <summary>Gets the normalized cyan component.</summary>
    public double Cyan { get; }
    /// <summary>Gets the normalized magenta component.</summary>
    public double Magenta { get; }
    /// <summary>Gets the normalized yellow component.</summary>
    public double Yellow { get; }
    /// <summary>Gets the normalized black component.</summary>
    public double Black { get; }

    private static double Component(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name, "A CMYK component must be between zero and one.");
        return value;
    }
}
