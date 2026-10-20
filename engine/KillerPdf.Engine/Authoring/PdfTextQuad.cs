namespace KillerPdf.Engine.Authoring;

/// <summary>Four corners of one text run, in PDF QuadPoints order.</summary>
public readonly record struct PdfTextQuad
{
    /// <summary>Creates a non-collapsed quadrilateral from corners in PDF text-markup order.</summary>
    public PdfTextQuad(
        PdfPoint upperLeft,
        PdfPoint upperRight,
        PdfPoint lowerLeft,
        PdfPoint lowerRight)
    {
        double upperLength = Distance(upperLeft, upperRight);
        double lowerLength = Distance(lowerLeft, lowerRight);
        double leftLength = Distance(upperLeft, lowerLeft);
        double rightLength = Distance(upperRight, lowerRight);
        if (upperLength <= 0 || lowerLength <= 0 || leftLength <= 0 || rightLength <= 0)
            throw new ArgumentException("A text quad must have four distinct, non-collapsed edges.");

        double area = Math.Abs(Cross(upperLeft, upperRight, lowerLeft)) +
            Math.Abs(Cross(upperRight, lowerRight, lowerLeft));
        if (area <= 1e-12)
            throw new ArgumentException("A text quad must have nonzero area.");

        UpperLeft = upperLeft;
        UpperRight = upperRight;
        LowerLeft = lowerLeft;
        LowerRight = lowerRight;
    }

    /// <summary>Gets the upper-left corner.</summary>
    public PdfPoint UpperLeft { get; }
    /// <summary>Gets the upper-right corner.</summary>
    public PdfPoint UpperRight { get; }
    /// <summary>Gets the lower-left corner.</summary>
    public PdfPoint LowerLeft { get; }
    /// <summary>Gets the lower-right corner.</summary>
    public PdfPoint LowerRight { get; }

    internal void Validate() => _ = new PdfTextQuad(
        UpperLeft, UpperRight, LowerLeft, LowerRight);

    internal static PdfTextQuad FromRectangle(double x, double y, double width, double height) =>
        new(new PdfPoint(x, y + height), new PdfPoint(x + width, y + height),
            new PdfPoint(x, y), new PdfPoint(x + width, y));

    private static double Distance(PdfPoint a, PdfPoint b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    private static double Cross(PdfPoint a, PdfPoint b, PdfPoint c) =>
        ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
}
