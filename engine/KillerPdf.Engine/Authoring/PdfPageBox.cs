namespace KillerPdf.Engine.Authoring;

/// <summary>A secondary page boundary that may be explicitly set or removed.</summary>
public enum PdfPageBox
{
    /// <summary>The region displayed or printed by default.</summary>
    Crop,
    /// <summary>The region to which production output should extend when trimmed.</summary>
    Bleed,
    /// <summary>The intended finished-page dimensions.</summary>
    Trim,
    /// <summary>The meaningful content region of the page.</summary>
    Art
}
