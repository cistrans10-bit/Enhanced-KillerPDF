namespace KillerPdf.Engine.Authoring;

/// <summary>A standard endpoint decoration for line-based annotations.</summary>
public enum PdfLineEndingStyle
{
    /// <summary>No endpoint decoration.</summary>
    None,
    /// <summary>A square centered on the endpoint.</summary>
    Square,
    /// <summary>A circle centered on the endpoint.</summary>
    Circle,
    /// <summary>A diamond centered on the endpoint.</summary>
    Diamond,
    /// <summary>An open arrowhead pointing outward from the line.</summary>
    OpenArrow,
    /// <summary>A closed arrowhead pointing outward from the line.</summary>
    ClosedArrow,
    /// <summary>A short perpendicular line at the endpoint.</summary>
    Butt,
    /// <summary>An open arrowhead pointing inward along the line.</summary>
    ReverseOpenArrow,
    /// <summary>A closed arrowhead pointing inward along the line.</summary>
    ReverseClosedArrow,
    /// <summary>A short diagonal slash at the endpoint.</summary>
    Slash
}

internal static class PdfLineEndingStyleNames
{
    public static string Name(PdfLineEndingStyle style) => style switch
    {
        PdfLineEndingStyle.None => "None",
        PdfLineEndingStyle.Square => "Square",
        PdfLineEndingStyle.Circle => "Circle",
        PdfLineEndingStyle.Diamond => "Diamond",
        PdfLineEndingStyle.OpenArrow => "OpenArrow",
        PdfLineEndingStyle.ClosedArrow => "ClosedArrow",
        PdfLineEndingStyle.Butt => "Butt",
        PdfLineEndingStyle.ReverseOpenArrow => "ROpenArrow",
        PdfLineEndingStyle.ReverseClosedArrow => "RClosedArrow",
        PdfLineEndingStyle.Slash => "Slash",
        _ => throw new ArgumentOutOfRangeException(nameof(style))
    };
}
