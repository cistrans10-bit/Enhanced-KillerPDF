namespace KillerPdf.Engine.Authoring;

/// <summary>Behavior shared by AcroForm field types.</summary>
public sealed record PdfFormFieldOptions
{
    /// <summary>Gets whether users may change the field value.</summary>
    public bool ReadOnly { get; init; }
    /// <summary>Gets whether the field must have a value when submitted.</summary>
    public bool Required { get; init; }
    /// <summary>Gets whether the field is omitted from form submission.</summary>
    public bool NoExport { get; init; }
}
