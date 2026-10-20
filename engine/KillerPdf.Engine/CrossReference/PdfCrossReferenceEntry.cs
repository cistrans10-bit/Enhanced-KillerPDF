namespace KillerPdf.Engine.CrossReference;

/// <summary>The storage state represented by a cross-reference entry.</summary>
public enum PdfCrossReferenceEntryType
{
    /// <summary>A free object-number entry.</summary>
    Free,
    /// <summary>An uncompressed indirect object at a byte offset.</summary>
    InUse,
    /// <summary>An object stored as a member of an object stream.</summary>
    Compressed,
    /// <summary>An unknown future xref-stream type, which PDF requires readers to treat as null.</summary>
    Null
}

/// <summary>
/// One cross-reference entry. Field1 is the next free object, byte offset, or object-stream
/// number; Field2 is the generation or object-stream index, according to <see cref="Type"/>.
/// </summary>
public readonly record struct PdfCrossReferenceEntry(
    int ObjectNumber,
    PdfCrossReferenceEntryType Type,
    long Field1,
    int Field2);
