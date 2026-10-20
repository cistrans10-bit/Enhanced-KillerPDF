namespace KillerPdf.Engine.Authoring;

/// <summary>When a push-button icon is scaled to its available rectangle.</summary>
public enum PdfPushButtonIconScaleMode
{
    /// <summary>Always scales the icon.</summary>
    Always,
    /// <summary>Never scales the icon.</summary>
    Never,
    /// <summary>Scales the icon only when it exceeds the available rectangle.</summary>
    WhenTooLarge,
    /// <summary>Scales the icon only when it is smaller than the available rectangle.</summary>
    WhenTooSmall
}
