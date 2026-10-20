namespace KillerPdf.Engine.Authoring;

/// <summary>A device RGB color whose components range from zero through one.</summary>
public readonly record struct PdfRgbColor
{
    /// <summary>Creates a color from normalized red, green, and blue components.</summary>
    public PdfRgbColor(double red, double green, double blue)
    {
        Red = Component(red, nameof(red));
        Green = Component(green, nameof(green));
        Blue = Component(blue, nameof(blue));
    }

    /// <summary>Gets the normalized red component.</summary>
    public double Red { get; }
    /// <summary>Gets the normalized green component.</summary>
    public double Green { get; }
    /// <summary>Gets the normalized blue component.</summary>
    public double Blue { get; }

    /// <summary>Gets the standard yellow used for text highlighting.</summary>
    public static PdfRgbColor Yellow { get; } = new(1, 0.92, 0.1);
    /// <summary>Gets the warm yellow used for text-note appearances.</summary>
    public static PdfRgbColor NoteYellow { get; } = new(1, 0.78, 0.1);

    private static double Component(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name, "RGB components must be between zero and one.");
        return value;
    }
}
