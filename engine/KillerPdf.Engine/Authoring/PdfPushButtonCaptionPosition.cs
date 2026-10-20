namespace KillerPdf.Engine.Authoring;

/// <summary>The relative placement of a push-button caption and icon.</summary>
public enum PdfPushButtonCaptionPosition
{
    /// <summary>Displays only the caption.</summary>
    CaptionOnly = 0,
    /// <summary>Displays only the icon.</summary>
    IconOnly = 1,
    /// <summary>Displays the caption below the icon.</summary>
    CaptionBelowIcon = 2,
    /// <summary>Displays the caption above the icon.</summary>
    CaptionAboveIcon = 3,
    /// <summary>Displays the caption to the right of the icon.</summary>
    CaptionRightOfIcon = 4,
    /// <summary>Displays the caption to the left of the icon.</summary>
    CaptionLeftOfIcon = 5,
    /// <summary>Displays the caption over the icon.</summary>
    CaptionOverIcon = 6
}
