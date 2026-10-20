namespace KillerPdf.Engine.Objects;

/// <summary>The singleton PDF null object.</summary>
public sealed class PdfNull : PdfObject
{
    private PdfNull() { }

    /// <summary>Gets the singleton null instance.</summary>
    public static PdfNull Instance { get; } = new();
}

/// <summary>A PDF boolean object.</summary>
/// <param name="value">The boolean value.</param>
public sealed class PdfBoolean(bool value) : PdfObject
{
    /// <summary>Gets the boolean value.</summary>
    public bool Value { get; } = value;
}

/// <summary>A signed 64-bit PDF integer object.</summary>
/// <param name="value">The integer value.</param>
public sealed class PdfInteger(long value) : PdfObject
{
    /// <summary>Gets the integer value.</summary>
    public long Value { get; } = value;
}

/// <summary>A finite PDF real-number object.</summary>
public sealed class PdfReal : PdfObject
{
    /// <summary>Creates a real-number object from a finite value.</summary>
    public PdfReal(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "A PDF real number must be finite.");

        Value = value;
    }

    /// <summary>Gets the finite real value.</summary>
    public double Value { get; }
}
