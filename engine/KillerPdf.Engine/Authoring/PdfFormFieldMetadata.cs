namespace KillerPdf.Engine.Authoring;

/// <summary>Optional user-facing and export names for an AcroForm field.</summary>
public sealed record PdfFormFieldMetadata
{
    /// <summary>Gets the user-facing field description.</summary>
    public string? Tooltip { get; init; }
    /// <summary>Gets the export mapping name.</summary>
    public string? MappingName { get; init; }
}
