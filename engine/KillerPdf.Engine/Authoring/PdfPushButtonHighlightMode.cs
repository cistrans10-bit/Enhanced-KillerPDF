namespace KillerPdf.Engine.Authoring;

/// <summary>The visual feedback displayed while a push button is activated.</summary>
public enum PdfPushButtonHighlightMode
{
    /// <summary>Displays no activation feedback.</summary>
    None,
    /// <summary>Inverts the button contents.</summary>
    Invert,
    /// <summary>Inverts the button border.</summary>
    Outline,
    /// <summary>Displays the button's pressed-state appearance.</summary>
    Push,
    /// <summary>Uses the button's rollover appearance while activated.</summary>
    Toggle
}
