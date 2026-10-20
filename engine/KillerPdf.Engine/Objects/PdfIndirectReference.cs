namespace KillerPdf.Engine.Objects;

/// <summary>A reference to an indirect object number and generation.</summary>
public sealed class PdfIndirectReference : PdfObject
{
    /// <summary>Creates a validated indirect reference.</summary>
    public PdfIndirectReference(int objectNumber, int generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(objectNumber);
        if (generation is < 0 or > 65_535)
            throw new ArgumentOutOfRangeException(nameof(generation));

        ObjectNumber = objectNumber;
        Generation = generation;
    }

    /// <summary>Gets the nonnegative object number.</summary>
    public int ObjectNumber { get; }
    /// <summary>Gets the generation from zero through 65,535.</summary>
    public int Generation { get; }
}

/// <summary>A numbered top-level object and the source offset at which its declaration begins.</summary>
public sealed class PdfIndirectObject
{
    /// <summary>Creates a parsed indirect object and records its declaration offset.</summary>
    public PdfIndirectObject(int objectNumber, int generation, PdfObject value, int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(objectNumber);
        if (generation is < 0 or > 65_535)
            throw new ArgumentOutOfRangeException(nameof(generation));
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        ObjectNumber = objectNumber;
        Generation = generation;
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Offset = offset;
    }

    /// <summary>Gets the nonnegative object number.</summary>
    public int ObjectNumber { get; }
    /// <summary>Gets the generation from zero through 65,535.</summary>
    public int Generation { get; }
    /// <summary>Gets the parsed object value.</summary>
    public PdfObject Value { get; }
    /// <summary>Gets the zero-based byte offset of the indirect declaration.</summary>
    public int Offset { get; }
}
