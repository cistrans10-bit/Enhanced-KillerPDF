namespace KillerPdf.Engine.Authoring;

/// <summary>Standard text-markup annotation styles defined by PDF 2.0.</summary>
public enum PdfTextMarkupType
{
    /// <summary>Highlights the enclosed text.</summary>
    Highlight,
    /// <summary>Draws a line beneath the enclosed text.</summary>
    Underline,
    /// <summary>Draws a line through the enclosed text.</summary>
    StrikeOut,
    /// <summary>Draws a wavy line beneath the enclosed text.</summary>
    Squiggly
}
