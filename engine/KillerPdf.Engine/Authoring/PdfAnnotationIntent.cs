namespace KillerPdf.Engine.Authoring;

/// <summary>Describes the intended use of a free-text annotation.</summary>
public enum PdfFreeTextIntent
{
    /// <summary>A general free-text annotation.</summary>
    FreeText,
    /// <summary>A free-text annotation whose callout line points to related content.</summary>
    Callout,
    /// <summary>A typewriter-style free-text annotation.</summary>
    TypeWriter
}

/// <summary>Describes the intended use of a line annotation.</summary>
public enum PdfLineAnnotationIntent
{
    /// <summary>A line that functions as an arrow.</summary>
    Arrow,
    /// <summary>A line that records a dimension.</summary>
    Dimension
}

/// <summary>Describes the intended use of a polyline or polygon annotation.</summary>
public enum PdfVertexAnnotationIntent
{
    /// <summary>A polygon with a cloud-style border.</summary>
    Cloud,
    /// <summary>A polyline or polygon that records a dimension.</summary>
    Dimension
}

internal static class PdfAnnotationIntentNames
{
    internal static string Name(PdfFreeTextIntent value) => value switch
    {
        PdfFreeTextIntent.FreeText => "FreeText",
        PdfFreeTextIntent.Callout => "FreeTextCallout",
        PdfFreeTextIntent.TypeWriter => "FreeTextTypeWriter",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    internal static string Name(PdfLineAnnotationIntent value) => value switch
    {
        PdfLineAnnotationIntent.Arrow => "LineArrow",
        PdfLineAnnotationIntent.Dimension => "LineDimension",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    internal static string Name(PdfVertexAnnotationIntent value, bool closed) => value switch
    {
        PdfVertexAnnotationIntent.Cloud when closed => "PolygonCloud",
        PdfVertexAnnotationIntent.Dimension when closed => "PolygonDimension",
        PdfVertexAnnotationIntent.Dimension => "PolyLineDimension",
        PdfVertexAnnotationIntent.Cloud => throw new ArgumentException(
            "Cloud intent is only valid for polygon annotations.", nameof(value)),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
