using System.Text;
using KillerPdf.Engine.Fonts;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Authoring;

public sealed partial class PdfDocumentBuilder
{
    private readonly List<CaretAnnotationDefinition> _caretAnnotations = [];
    private readonly List<RedactionAnnotationDefinition> _redactionAnnotations = [];

    /// <summary>Adds a caret annotation that marks an insertion point or paragraph replacement.</summary>
    public PdfDocumentBuilder AddCaretAnnotation(
        int pageIndex, double x, double y, double width, double height,
        string? contents = null, PdfRgbColor? color = null, double opacity = 1,
        PdfCaretSymbol symbol = PdfCaretSymbol.None,
        PdfAnnotationMetadata? annotationMetadata = null)
    {
        ValidatePageIndex(pageIndex, nameof(pageIndex));
        ValidateRectangle(x, y, width, height);
        if (!double.IsFinite(opacity) || opacity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity));
        if (!Enum.IsDefined(symbol)) throw new ArgumentOutOfRangeException(nameof(symbol));
        _caretAnnotations.Add(new CaretAnnotationDefinition(
            pageIndex, x, y, width, height, contents,
            color ?? new PdfRgbColor(0.1, 0.35, 0.9), opacity, symbol, annotationMetadata));
        return this;
    }

    /// <summary>Adds a review-time redaction mark with optional overlay text and post-redaction fill.</summary>
    public PdfDocumentBuilder AddRedactionMark(
        int pageIndex, IReadOnlyList<PdfTextQuad> quads,
        string? contents = null, PdfRgbColor? fillColor = null,
        PdfRgbColor? markColor = null, double opacity = 0.25,
        PdfAnnotationMetadata? annotationMetadata = null,
        string? overlayText = null, bool repeatOverlayText = false,
        PdfTextAlignment overlayAlignment = PdfTextAlignment.Center,
        double overlayFontSize = 10, TrueTypeFont? overlayFont = null)
    {
        ValidatePageIndex(pageIndex, nameof(pageIndex));
        ArgumentNullException.ThrowIfNull(quads);
        if (quads.Count == 0)
            throw new ArgumentException("At least one redaction quad is required.", nameof(quads));
        foreach (PdfTextQuad quad in quads) quad.Validate();
        if (!double.IsFinite(opacity) || opacity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(opacity));
        if (overlayFont is null && overlayText is not null && overlayText.Any(character => character > 0x7F))
            throw new ArgumentException("Baseline redaction overlay text supports ASCII characters.", nameof(overlayText));
        if (overlayFont is not null && overlayText is not null)
            ValidateDrawableText(overlayFont, overlayText, nameof(overlayText));
        if (!Enum.IsDefined(overlayAlignment))
            throw new ArgumentOutOfRangeException(nameof(overlayAlignment));
        if (!double.IsFinite(overlayFontSize) || overlayFontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(overlayFontSize));
        _redactionAnnotations.Add(new RedactionAnnotationDefinition(
            pageIndex, [.. quads], contents, fillColor ?? new PdfRgbColor(0, 0, 0),
            markColor ?? new PdfRgbColor(0.85, 0.1, 0.1), opacity, annotationMetadata,
            overlayText, repeatOverlayText, overlayAlignment, overlayFontSize, overlayFont));
        return this;
    }

    private static void AddCaretAnnotationObjects(
        List<PdfIndirectObject> objects, AllocatedCaretAnnotation allocated,
        IReadOnlyList<AllocatedPage> pages, int sequence)
    {
        CaretAnnotationDefinition value = allocated.Definition;
        var entries = EditorialAnnotationEntries(
            "Caret", value.PageIndex, value.X, value.Y, value.Width, value.Height,
            pages, $"KillerPDF-Caret-{sequence}", value.Color, value.Opacity,
            value.Contents, allocated.AppearanceNumber, value.Metadata);
        if (value.Symbol == PdfCaretSymbol.Paragraph) entries.Add(("Sy", Name("P")));
        objects.Add(new PdfIndirectObject(
            allocated.AnnotationNumber, 0, Dictionary([.. entries]), 0));

        PdfDictionary resources = AnnotationResources(value.Opacity);
        double center = value.Width / 2;
        double inset = Math.Max(1, Math.Min(value.Width, value.Height) * 0.12);
        byte[] appearance = Encoding.ASCII.GetBytes(
            $"q\n/GS1 gs\n{ColorOperands(value.Color)} RG\n" +
            $"{FormatNumber(Math.Max(1, Math.Min(value.Width, value.Height) * 0.1))} w\n" +
            $"{FormatNumber(inset)} {FormatNumber(value.Height - inset)} m\n" +
            $"{FormatNumber(center)} {FormatNumber(inset)} l\n" +
            $"{FormatNumber(value.Width - inset)} {FormatNumber(value.Height - inset)} l\nS\nQ\n");
        objects.Add(new PdfIndirectObject(allocated.AppearanceNumber, 0,
            AnnotationAppearance(value.Width, value.Height, resources, appearance), 0));
    }

    private static void AddRedactionAnnotationObjects(
        List<PdfIndirectObject> objects, AllocatedRedactionAnnotation allocated,
        IReadOnlyList<AllocatedPage> pages, int sequence, PdfName? fontResource, int? fontNumber,
        EmbeddedFontUsage? fontUsage)
    {
        RedactionAnnotationDefinition value = allocated.Definition;
        (double minX, double minY, double maxX, double maxY) = EditorialQuadBounds(value.Quads);
        var quadPoints = new List<PdfObject>(value.Quads.Count * 8);
        foreach (PdfTextQuad quad in value.Quads)
            quadPoints.AddRange([
                Number(quad.UpperLeft.X), Number(quad.UpperLeft.Y),
                Number(quad.UpperRight.X), Number(quad.UpperRight.Y),
                Number(quad.LowerLeft.X), Number(quad.LowerLeft.Y),
                Number(quad.LowerRight.X), Number(quad.LowerRight.Y)]);
        var entries = EditorialAnnotationEntries(
            "Redact", value.PageIndex, minX, minY, maxX - minX, maxY - minY,
            pages, $"KillerPDF-Redact-{sequence}", value.MarkColor, value.Opacity,
            value.Contents, allocated.AppearanceNumber, value.Metadata);
        entries.Add(("QuadPoints", new PdfArray(quadPoints)));
        entries.Add(("IC", ColorArray(value.FillColor)));
        if (value.OverlayText is not null)
        {
            entries.Add(("OverlayText", UnicodeString(value.OverlayText)));
            entries.Add(("Repeat", new PdfBoolean(value.RepeatOverlayText)));
            entries.Add(("Q", new PdfInteger((int)value.OverlayAlignment)));
            entries.Add(("DA", Latin1String(
                $"{NameToken(fontResource!)} {FormatNumber(value.OverlayFontSize)} Tf 1 1 1 rg")));
        }
        objects.Add(new PdfIndirectObject(
            allocated.AnnotationNumber, 0, Dictionary([.. entries]), 0));

        PdfDictionary resources = value.OverlayText is null
            ? AnnotationResources(value.Opacity)
            : AnnotationResources(value.Opacity,
                (fontResource!, new PdfIndirectReference(fontNumber!.Value, 0)));
        var drawing = new StringBuilder($"q\n/GS1 gs\n{ColorOperands(value.MarkColor)} RG\n1 w\n");
        foreach (PdfTextQuad source in value.Quads)
        {
            PdfPoint ul = new(source.UpperLeft.X - minX, source.UpperLeft.Y - minY);
            PdfPoint ur = new(source.UpperRight.X - minX, source.UpperRight.Y - minY);
            PdfPoint ll = new(source.LowerLeft.X - minX, source.LowerLeft.Y - minY);
            PdfPoint lr = new(source.LowerRight.X - minX, source.LowerRight.Y - minY);
            drawing.Append($"{FormatNumber(ll.X)} {FormatNumber(ll.Y)} m\n")
                .Append($"{FormatNumber(lr.X)} {FormatNumber(lr.Y)} l\n")
                .Append($"{FormatNumber(ur.X)} {FormatNumber(ur.Y)} l\n")
                .Append($"{FormatNumber(ul.X)} {FormatNumber(ul.Y)} l\nh\n")
                .Append($"{FormatNumber(ll.X)} {FormatNumber(ll.Y)} m\n")
                .Append($"{FormatNumber(ur.X)} {FormatNumber(ur.Y)} l\n")
                .Append($"{FormatNumber(ul.X)} {FormatNumber(ul.Y)} m\n")
                .Append($"{FormatNumber(lr.X)} {FormatNumber(lr.Y)} l\nS\n");
        }
        if (value.OverlayText is not null)
        {
            IEnumerable<PdfTextQuad> textQuads = value.RepeatOverlayText
                ? value.Quads : value.Quads.Take(1);
            foreach (PdfTextQuad quad in textQuads)
            {
                double left = Math.Min(quad.LowerLeft.X, quad.UpperLeft.X) - minX;
                double right = Math.Max(quad.LowerRight.X, quad.UpperRight.X) - minX;
                double bottom = Math.Min(quad.LowerLeft.Y, quad.LowerRight.Y) - minY;
                double top = Math.Max(quad.UpperLeft.Y, quad.UpperRight.Y) - minY;
                double textWidth = value.OverlayText.Length * value.OverlayFontSize * 0.5;
                double textX = value.OverlayAlignment switch
                {
                    PdfTextAlignment.Left => left + 2,
                    PdfTextAlignment.Center => left + Math.Max(0, (right - left - textWidth) / 2),
                    PdfTextAlignment.Right => Math.Max(left, right - textWidth - 2),
                    _ => throw new InvalidOperationException(
                        $"Unsupported overlay alignment: {value.OverlayAlignment}.")
                };
                double textY = bottom + Math.Max(1, (top - bottom - value.OverlayFontSize) / 2);
                drawing.Append("BT\n").Append(NameToken(fontResource!)).Append(' ')
                    .Append(FormatNumber(value.OverlayFontSize))
                    .Append(" Tf\n1 1 1 rg\n1 0 0 1 ").Append(FormatNumber(textX)).Append(' ')
                    .Append(FormatNumber(textY)).Append(" Tm\n");
                using var shown = new MemoryStream();
                WriteShownText(shown, value.OverlayText, value.OverlayFont, fontUsage);
                drawing.Append(Encoding.ASCII.GetString(shown.ToArray())).Append("ET\n");
            }
        }
        drawing.Append("Q\n");
        objects.Add(new PdfIndirectObject(allocated.AppearanceNumber, 0,
            AnnotationAppearance(maxX - minX, maxY - minY, resources,
                Encoding.ASCII.GetBytes(drawing.ToString())), 0));
    }

    private static List<(string Name, PdfObject Value)> EditorialAnnotationEntries(
        string subtype, int pageIndex, double x, double y, double width, double height,
        IReadOnlyList<AllocatedPage> pages, string name, PdfRgbColor color, double opacity,
        string? contents, int appearanceNumber, PdfAnnotationMetadata? metadata)
    {
        var entries = new List<(string Name, PdfObject Value)>
        {
            ("Type", Name("Annot")), ("Subtype", Name(subtype)),
            ("Rect", new PdfArray([Number(x), Number(y), Number(x + width), Number(y + height)])),
            ("P", new PdfIndirectReference(pages[pageIndex].PageNumber, 0)),
            ("F", new PdfInteger((int)(metadata?.Flags ?? PdfAnnotationFlags.Print))),
            ("NM", Latin1String(name)), ("C", ColorArray(color)), ("CA", Number(opacity)),
            ("AP", Dictionary(("N", new PdfIndirectReference(appearanceNumber, 0))))
        };
        if (!string.IsNullOrEmpty(contents)) entries.Add(("Contents", UnicodeString(contents)));
        AddAnnotationMetadata(entries, metadata);
        return entries;
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) EditorialQuadBounds(
        IReadOnlyList<PdfTextQuad> quads)
    {
        PdfPoint[] points = [.. quads.SelectMany(quad => new[]
            { quad.UpperLeft, quad.UpperRight, quad.LowerLeft, quad.LowerRight })];
        return (points.Min(point => point.X), points.Min(point => point.Y),
            points.Max(point => point.X), points.Max(point => point.Y));
    }

    private sealed record CaretAnnotationDefinition(
        int PageIndex, double X, double Y, double Width, double Height, string? Contents,
        PdfRgbColor Color, double Opacity, PdfCaretSymbol Symbol, PdfAnnotationMetadata? Metadata);
    private sealed record AllocatedCaretAnnotation(
        CaretAnnotationDefinition Definition, int AnnotationNumber, int AppearanceNumber);
    private sealed record RedactionAnnotationDefinition(
        int PageIndex, IReadOnlyList<PdfTextQuad> Quads, string? Contents,
        PdfRgbColor FillColor, PdfRgbColor MarkColor, double Opacity,
        PdfAnnotationMetadata? Metadata, string? OverlayText, bool RepeatOverlayText,
        PdfTextAlignment OverlayAlignment, double OverlayFontSize, TrueTypeFont? OverlayFont);
    private sealed record AllocatedRedactionAnnotation(
        RedactionAnnotationDefinition Definition, int AnnotationNumber, int AppearanceNumber);
}
