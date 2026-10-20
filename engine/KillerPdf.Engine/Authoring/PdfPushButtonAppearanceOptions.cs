namespace KillerPdf.Engine.Authoring;

/// <summary>Alternate captions shown while a push button is hovered or pressed.</summary>
public sealed record PdfPushButtonAppearanceOptions
{
    /// <summary>Gets the caption alignment.</summary>
    public PdfTextFieldAlignment Alignment { get; init; } = PdfTextFieldAlignment.Center;
    /// <summary>Gets the normal-state icon.</summary>
    public PdfImage? Icon { get; init; }
    /// <summary>Gets the icon displayed while the pointer is over the button.</summary>
    public PdfImage? RolloverIcon { get; init; }
    /// <summary>Gets the icon displayed while the button is pressed.</summary>
    public PdfImage? DownIcon { get; init; }
    /// <summary>Gets the relative placement of the caption and icon.</summary>
    public PdfPushButtonCaptionPosition CaptionPosition { get; init; }
    /// <summary>Gets when icons are scaled to the available rectangle.</summary>
    public PdfPushButtonIconScaleMode IconScaleMode { get; init; } =
        PdfPushButtonIconScaleMode.Always;
    /// <summary>Gets whether icon scaling preserves the source aspect ratio.</summary>
    public bool ProportionalIconScaling { get; init; } = true;
    /// <summary>Gets the normalized horizontal icon alignment from zero through one.</summary>
    public double IconHorizontalAlignment { get; init; } = 0.5;
    /// <summary>Gets the normalized vertical icon alignment from zero through one.</summary>
    public double IconVerticalAlignment { get; init; } = 0.5;
    /// <summary>Gets whether the icon may occupy the button's complete bounds.</summary>
    public bool FitIconToBounds { get; init; }
    /// <summary>Gets the caption displayed while the pointer is over the button.</summary>
    public string? RolloverLabel { get; init; }
    /// <summary>Gets the caption displayed while the button is pressed.</summary>
    public string? DownLabel { get; init; }
}
