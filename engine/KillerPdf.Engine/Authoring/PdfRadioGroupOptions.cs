namespace KillerPdf.Engine.Authoring;

/// <summary>Behavior specific to an AcroForm radio-button group.</summary>
public sealed record PdfRadioGroupOptions
{
    /// <summary>Gets whether a selected radio option cannot be cleared by user interaction.</summary>
    public bool NoToggleToOff { get; init; }
    /// <summary>Gets whether widgets sharing an export value toggle together.</summary>
    public bool RadiosInUnison { get; init; }
    /// <summary>Gets optional widget colors and border geometry.</summary>
    public PdfFormFieldAppearanceStyle? AppearanceStyle { get; init; }
}
