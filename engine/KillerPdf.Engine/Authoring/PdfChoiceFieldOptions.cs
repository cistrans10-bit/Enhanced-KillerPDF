namespace KillerPdf.Engine.Authoring;

/// <summary>Behavior shared by combo boxes and list boxes.</summary>
public sealed record PdfChoiceFieldOptions
{
    /// <summary>Gets whether options are displayed in sorted order.</summary>
    public bool SortOptions { get; init; }
    /// <summary>Gets whether viewer spell checking is disabled for editable values.</summary>
    public bool DoNotSpellCheck { get; init; }
    /// <summary>Gets whether selecting an option immediately commits the value.</summary>
    public bool CommitOnSelectionChange { get; init; }
    /// <summary>Gets the horizontal text alignment.</summary>
    public PdfTextFieldAlignment Alignment { get; init; }
    /// <summary>Gets the export values selected when the field is reset.</summary>
    public IReadOnlyList<string>? DefaultSelectedExportValues { get; init; }
    /// <summary>Gets optional widget colors and border geometry.</summary>
    public PdfFormFieldAppearanceStyle? AppearanceStyle { get; init; }
}
