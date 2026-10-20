namespace KillerPdf.Engine.Authoring;

/// <summary>The shape used at the endpoints of an open stroked path.</summary>
public enum PdfLineCap
{
    /// <summary>The stroke ends exactly at the endpoint.</summary>
    Butt = 0,
    /// <summary>A semicircular cap extends beyond the endpoint.</summary>
    Round = 1,
    /// <summary>A square cap extends beyond the endpoint by half the line width.</summary>
    ProjectingSquare = 2
}

/// <summary>The shape used where two stroked path segments meet.</summary>
public enum PdfLineJoin
{
    /// <summary>Segments meet in a pointed miter.</summary>
    Miter = 0,
    /// <summary>Segments meet with a rounded join.</summary>
    Round = 1,
    /// <summary>Segments meet with a beveled join.</summary>
    Bevel = 2
}
