namespace KillerPdf.Engine.Authoring;

/// <summary>Controls how text glyphs are painted and applied to the clipping path.</summary>
public enum PdfTextRenderingMode
{
    /// <summary>Fills glyph interiors.</summary>
    Fill = 0,
    /// <summary>Strokes glyph outlines.</summary>
    Stroke = 1,
    /// <summary>Fills glyph interiors and strokes their outlines.</summary>
    FillAndStroke = 2,
    /// <summary>Does not paint glyphs.</summary>
    Invisible = 3,
    /// <summary>Fills glyphs and adds their outlines to the clipping path.</summary>
    FillAndClip = 4,
    /// <summary>Strokes glyphs and adds their outlines to the clipping path.</summary>
    StrokeAndClip = 5,
    /// <summary>Fills and strokes glyphs, then adds their outlines to the clipping path.</summary>
    FillStrokeAndClip = 6,
    /// <summary>Adds glyph outlines to the clipping path without painting them.</summary>
    Clip = 7
}
