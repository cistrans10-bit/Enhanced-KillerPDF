using System.Globalization;
using System.Text;
using KillerPdf.Engine.Fonts;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Authoring;

public sealed partial class PdfDocumentBuilder
{
    private readonly List<FreeTextDefinition> _freeTexts = [];
    private readonly List<VisualAnnotationDefinition> _visualAnnotations = [];
    private readonly List<ImageStampDefinition> _imageStamps = [];

    /// <summary>Adds a free-text annotation with an embedded font and deterministic appearance.</summary>
    public PdfDocumentBuilder AddFreeText(
        int pageIndex, double x, double y, double width, double height,
        string contents, TrueTypeFont font, double fontSize = 12,
        PdfRgbColor? textColor = null, PdfRgbColor? fillColor = null,
        PdfRgbColor? borderColor = null, double borderWidth = 1, double opacity = 1,
        PdfAnnotationMetadata? annotationMetadata = null,
        PdfTextAlignment alignment = PdfTextAlignment.Left,
        IReadOnlyList<double>? dashPattern = null,
        PdfFreeTextIntent intent = PdfFreeTextIntent.FreeText,
        IReadOnlyList<PdfPoint>? calloutLine = null,
        PdfLineEndingStyle calloutEnding = PdfLineEndingStyle.OpenArrow)
    {
        ValidatePageIndex(pageIndex, nameof(pageIndex));
        ValidateRectangle(x, y, width, height);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(font);
        if (!double.IsFinite(fontSize) || fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        ValidateStroke(borderWidth, opacity);
        if (!Enum.IsDefined(alignment))
            throw new ArgumentOutOfRangeException(nameof(alignment));
        if (!Enum.IsDefined(intent)) throw new ArgumentOutOfRangeException(nameof(intent));
        ValidateLineEnding(calloutEnding, nameof(calloutEnding));
        if (intent == PdfFreeTextIntent.Callout && calloutLine?.Count is not (2 or 3))
            throw new ArgumentException("Callout free text requires two or three callout points.", nameof(calloutLine));
        if (intent != PdfFreeTextIntent.Callout && calloutLine is not null)
            throw new ArgumentException("Callout points require callout free-text intent.", nameof(calloutLine));
        double[]? dash = ValidateAnnotationDashPattern(dashPattern);
        ValidateDrawableText(font, contents, nameof(contents));
        _freeTexts.Add(new FreeTextDefinition(
            pageIndex, x, y, width, height, contents, font, fontSize,
            textColor ?? new PdfRgbColor(0, 0, 0), fillColor,
            borderColor ?? new PdfRgbColor(0, 0, 0), borderWidth, opacity,
            annotationMetadata, alignment, dash, intent, calloutLine?.ToArray(), calloutEnding));
        return this;
    }

    /// <summary>Adds a line annotation with optional endpoint decorations, interior color, and intent.</summary>
    public PdfDocumentBuilder AddLineAnnotation(
        int pageIndex, PdfPoint start, PdfPoint end, PdfRgbColor? color = null,
        double lineWidth = 1, double opacity = 1, string? contents = null,
        PdfLineEndingStyle startEnding = PdfLineEndingStyle.None,
        PdfLineEndingStyle endEnding = PdfLineEndingStyle.None,
        IReadOnlyList<double>? dashPattern = null,
        PdfRgbColor? interiorColor = null,
        PdfAnnotationMetadata? annotationMetadata = null,
        PdfLineAnnotationIntent? intent = null)
    {
        ValidatePageIndex(pageIndex, nameof(pageIndex));
        ValidateStroke(lineWidth, opacity);
        ValidateLineEnding(startEnding, nameof(startEnding));
        ValidateLineEnding(endEnding, nameof(endEnding));
        if (intent is not null && !Enum.IsDefined(intent.Value))
            throw new ArgumentOutOfRangeException(nameof(intent));
        double[]? dash = ValidateAnnotationDashPattern(dashPattern);
        if (start == end) throw new ArgumentException("A line must have two distinct endpoints.", nameof(end));
        _visualAnnotations.Add(new LineAnnotationDefinition(
            pageIndex, start, end, color ?? new PdfRgbColor(0, 0, 0), lineWidth, opacity, contents,
            startEnding, endEnding, dash, interiorColor, annotationMetadata, intent));
        return this;
    }

    /// <summary>Adds a rectangular annotation with optional stroke, fill, opacity, and dash pattern.</summary>
    public PdfDocumentBuilder AddRectangleAnnotation(
        int pageIndex, double x, double y, double width, double height,
        PdfRgbColor? strokeColor = null, PdfRgbColor? fillColor = null,
        double lineWidth = 1, double opacity = 1, string? contents = null,
        IReadOnlyList<double>? dashPattern = null,
        PdfAnnotationMetadata? annotationMetadata = null)
        => AddShapeAnnotation(PdfShapeAnnotationType.Square, pageIndex, x, y, width, height,
            strokeColor, fillColor, lineWidth, opacity, contents, dashPattern,
            annotationMetadata);

    /// <summary>Adds an elliptical annotation with optional stroke, fill, opacity, and dash pattern.</summary>
    public PdfDocumentBuilder AddEllipseAnnotation(
        int pageIndex, double x, double y, double width, double height,
        PdfRgbColor? strokeColor = null, PdfRgbColor? fillColor = null,
        double lineWidth = 1, double opacity = 1, string? contents = null,
        IReadOnlyList<double>? dashPattern = null,
        PdfAnnotationMetadata? annotationMetadata = null)
        => AddShapeAnnotation(PdfShapeAnnotationType.Circle, pageIndex, x, y, width, height,
            strokeColor, fillColor, lineWidth, opacity, contents, dashPattern,
            annotationMetadata);

    /// <summary>Adds an open polyline annotation from a validated vertex sequence.</summary>
    public PdfDocumentBuilder AddPolylineAnnotation(
        int pageIndex, IReadOnlyList<PdfPoint> vertices, PdfRgbColor? color = null,
        double lineWidth = 1, double opacity = 1, string? contents = null,
        PdfLineEndingStyle startEnding = PdfLineEndingStyle.None,
        PdfLineEndingStyle endEnding = PdfLineEndingStyle.None,
        IReadOnlyList<double>? dashPattern = null,
        PdfRgbColor? interiorColor = null,
        PdfAnnotationMetadata? annotationMetadata = null,
        PdfVertexAnnotationIntent? intent = null)
        => AddVertexAnnotation(
            pageIndex, vertices, closed: false, color, null, lineWidth, opacity, contents,
            startEnding, endEnding, dashPattern, interiorColor, annotationMetadata, intent);

    /// <summary>Adds a closed polygon annotation from a validated vertex sequence.</summary>
    public PdfDocumentBuilder AddPolygonAnnotation(
        int pageIndex, IReadOnlyList<PdfPoint> vertices,
        PdfRgbColor? strokeColor = null, PdfRgbColor? fillColor = null,
        double lineWidth = 1, double opacity = 1, string? contents = null,
        IReadOnlyList<double>? dashPattern = null,
        PdfAnnotationMetadata? annotationMetadata = null,
        PdfVertexAnnotationIntent? intent = null)
        => AddVertexAnnotation(
            pageIndex, vertices, closed: true, strokeColor, fillColor,
            lineWidth, opacity, contents, PdfLineEndingStyle.None, PdfLineEndingStyle.None,
            dashPattern, null, annotationMetadata, intent);

    /// <summary>Adds a single-stroke ink annotation.</summary>
    public PdfDocumentBuilder AddInkAnnotation(
        int pageIndex, IReadOnlyList<PdfPoint> points, PdfRgbColor? color = null,
        double lineWidth = 2, double opacity = 1, string? contents = null,
        IReadOnlyList<double>? dashPattern = null,
        PdfAnnotationMetadata? annotationMetadata = null)
        => AddInkAnnotation(
            pageIndex, [points], color, lineWidth, opacity, contents, dashPattern,
            annotationMetadata);

    /// <summary>Adds a multi-stroke ink annotation.</summary>
    public PdfDocumentBuilder AddInkAnnotation(
        int pageIndex, IReadOnlyList<IReadOnlyList<PdfPoint>> strokes, PdfRgbColor? color = null,
        double lineWidth = 2, double opacity = 1, string? contents = null,
        IReadOnlyList<double>? dashPattern = null,
        PdfAnnotationMetadata? annotationMetadata = null)
    {
        ValidatePageIndex(pageIndex, nameof(pageIndex));
        ArgumentNullException.ThrowIfNull(strokes);
        ValidateStroke(lineWidth, opacity);
        double[]? dash = ValidateAnnotationDashPattern(dashPattern);
        if (strokes.Count == 0 || strokes.Any(stroke => stroke is null || stroke.Count < 2))
            throw new ArgumentException("Ink requires at least one stroke containing two points.", nameof(strokes));
        _visualAnnotations.Add(new InkAnnotationDefinition(
            pageIndex, [.. strokes.Select(stroke => stroke.ToArray())],
            color ?? new PdfRgbColor(0, 0, 0), lineWidth, opacity, contents, dash,
            annotationMetadata));
        return this;
    }

    /// <summary>Adds an image-backed stamp annotation with a standard semantic icon name.</summary>
    public PdfDocumentBuilder AddImageStamp(
        int pageIndex, double x, double y, double width, double height,
        PdfImage image, string? contents = null,
        PdfAnnotationMetadata? annotationMetadata = null,
        PdfStampIcon icon = PdfStampIcon.Image)
    {
        ValidatePageIndex(pageIndex, nameof(pageIndex));
        ValidateRectangle(x, y, width, height);
        ArgumentNullException.ThrowIfNull(image);
        if (!Enum.IsDefined(icon)) throw new ArgumentOutOfRangeException(nameof(icon));
        _imageStamps.Add(new ImageStampDefinition(
            pageIndex, x, y, width, height, image, contents, annotationMetadata, icon));
        return this;
    }

    private PdfDocumentBuilder AddShapeAnnotation(
        PdfShapeAnnotationType type, int pageIndex, double x, double y, double width, double height,
        PdfRgbColor? strokeColor, PdfRgbColor? fillColor,
        double lineWidth, double opacity, string? contents,
        IReadOnlyList<double>? dashPattern, PdfAnnotationMetadata? annotationMetadata)
    {
        ValidatePageIndex(pageIndex, nameof(pageIndex));
        ValidateRectangle(x, y, width, height);
        ValidateStroke(lineWidth, opacity);
        double[]? dash = ValidateAnnotationDashPattern(dashPattern);
        _visualAnnotations.Add(new ShapeAnnotationDefinition(
            type, pageIndex, x, y, width, height, strokeColor ?? new PdfRgbColor(0, 0, 0),
            fillColor, lineWidth, opacity, contents, dash, annotationMetadata));
        return this;
    }

    private PdfDocumentBuilder AddVertexAnnotation(
        int pageIndex, IReadOnlyList<PdfPoint> vertices, bool closed,
        PdfRgbColor? strokeColor, PdfRgbColor? fillColor,
        double lineWidth, double opacity, string? contents,
        PdfLineEndingStyle startEnding, PdfLineEndingStyle endEnding,
        IReadOnlyList<double>? dashPattern, PdfRgbColor? interiorColor,
        PdfAnnotationMetadata? annotationMetadata, PdfVertexAnnotationIntent? intent)
    {
        ValidatePageIndex(pageIndex, nameof(pageIndex));
        ArgumentNullException.ThrowIfNull(vertices);
        ValidateStroke(lineWidth, opacity);
        ValidateLineEnding(startEnding, nameof(startEnding));
        ValidateLineEnding(endEnding, nameof(endEnding));
        if (intent is not null && !Enum.IsDefined(intent.Value))
            throw new ArgumentOutOfRangeException(nameof(intent));
        if (!closed && intent == PdfVertexAnnotationIntent.Cloud)
            throw new ArgumentException("Cloud intent is only valid for polygons.", nameof(intent));
        double[]? dash = ValidateAnnotationDashPattern(dashPattern);
        int minimum = closed ? 3 : 2;
        if (vertices.Count < minimum)
            throw new ArgumentException(
                $"{(closed ? "A polygon" : "A polyline")} requires at least {minimum} vertices.",
                nameof(vertices));
        if (vertices.Zip(vertices.Skip(1)).All(pair => pair.First == pair.Second))
            throw new ArgumentException("Vertex annotations require distinct points.", nameof(vertices));
        _visualAnnotations.Add(new VertexAnnotationDefinition(
            pageIndex, [.. vertices], closed,
            strokeColor ?? new PdfRgbColor(0, 0, 0), fillColor,
            lineWidth, opacity, contents, startEnding, endEnding, dash, interiorColor,
            annotationMetadata, intent));
        return this;
    }

    private static void ValidateLineEnding(PdfLineEndingStyle style, string name)
    {
        if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(name);
    }

    private static double[]? ValidateAnnotationDashPattern(IReadOnlyList<double>? pattern)
    {
        if (pattern is null) return null;
        if (pattern.Count == 0
            || pattern.Any(value => !double.IsFinite(value) || value < 0))
            throw new ArgumentOutOfRangeException(nameof(pattern));
        if (pattern.All(value => value == 0))
            throw new ArgumentException("A dash pattern cannot contain only zeros.", nameof(pattern));
        return [.. pattern];
    }

    private static void ValidateStroke(double lineWidth, double opacity)
    {
        if (!double.IsFinite(lineWidth) || lineWidth <= 0) throw new ArgumentOutOfRangeException(nameof(lineWidth));
        if (!double.IsFinite(opacity) || opacity is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(opacity));
    }

    private static void ValidateDrawableText(TrueTypeFont font, string value, string parameterName)
    {
        if (!font.EmbeddingAllowed)
            throw new ArgumentException($"Font {font.PostScriptName} prohibits PDF embedding.", parameterName);
        foreach (FontGlyphMapping mapping in font.MapText(value))
        {
            if (mapping.UnicodeSequence is "\r" or "\n") continue;
            if (mapping.Glyph == 0 && mapping.UnicodeSequence != "\0")
                throw new ArgumentException(
                    $"Font {font.PostScriptName} has no glyph for {FormatUnicodeSequence(mapping.UnicodeSequence)}.", parameterName);
        }
    }

    private static void AddFreeTextObjects(
        List<PdfIndirectObject> objects, AllocatedFreeText allocated,
        IReadOnlyList<AllocatedPage> pages, int sequence, PdfName fontResource, int fontNumber,
        EmbeddedFontUsage? fontUsage)
    {
        FreeTextDefinition value = allocated.Definition;
        Bounds bounds = FreeTextBounds(value);
        string defaultAppearance =
            $"{NameToken(fontResource)} {FormatNumber(value.FontSize)} Tf {ColorOperands(value.TextColor)} rg";
        var entries = CommonAnnotationEntries(
            "FreeText", value.PageIndex, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            pages, $"KillerPDF-FreeText-{sequence}", value.BorderColor, value.Opacity,
            value.Contents, allocated.AppearanceNumber, value.Metadata);
        entries.Add(("DA", Latin1String(defaultAppearance)));
        entries.Add(("Q", new PdfInteger((int)value.Alignment)));
        entries.Add(("IT", Name(PdfAnnotationIntentNames.Name(value.Intent))));
        if (value.CalloutLine is not null)
        {
            entries.Add(("CL", new PdfArray(value.CalloutLine.SelectMany(point =>
                new PdfObject[] { Number(point.X), Number(point.Y) }))));
            entries.Add(("LE", Name(PdfLineEndingStyleNames.Name(value.CalloutEnding))));
        }
        entries.Add(("BS", BorderStyle(value.BorderWidth, value.DashPattern)));
        if (value.FillColor.HasValue) entries.Add(("IC", ColorArray(value.FillColor.Value)));
        objects.Add(new PdfIndirectObject(allocated.AnnotationNumber, 0, Dictionary([.. entries]), 0));

        PdfDictionary resources = AnnotationResources(value.Opacity,
            (fontResource, new PdfIndirectReference(fontNumber, 0)));
        using var appearance = new MemoryStream();
        WriteAscii(appearance, $"q\n/GS1 gs\n");
        WriteAscii(appearance, DashOperator(value.DashPattern));
        if (value.CalloutLine is not null)
            WriteFreeTextCallout(appearance, value, bounds);
        WriteAscii(appearance,
            $"q\n1 0 0 1 {FormatNumber(value.X - bounds.X)} {FormatNumber(value.Y - bounds.Y)} cm\n");
        WriteBox(appearance, value.Width, value.Height, value.BorderWidth,
            value.BorderColor, value.FillColor, ellipse: false);
        WriteFreeText(appearance, value, fontResource, fontUsage);
        appearance.Write("Q\nQ\n"u8);
        objects.Add(new PdfIndirectObject(allocated.AppearanceNumber, 0,
            AnnotationAppearance(bounds.Width, bounds.Height, resources, appearance.ToArray()), 0));
    }

    private static Bounds FreeTextBounds(FreeTextDefinition value)
    {
        if (value.CalloutLine is null)
            return new Bounds(value.X, value.Y, value.Width, value.Height);
        PdfPoint[] points =
        [
            new(value.X, value.Y), new(value.X + value.Width, value.Y + value.Height),
            .. value.CalloutLine
        ];
        return PointBounds(points, Math.Max(value.BorderWidth, 1));
    }

    private static void WriteFreeTextCallout(Stream output, FreeTextDefinition value, Bounds bounds)
    {
        PdfPoint[] points =
        [
            .. value.CalloutLine!.Select(point =>
                new PdfPoint(point.X - bounds.X, point.Y - bounds.Y))
        ];
        WriteAscii(output,
            $"{ColorOperands(value.BorderColor)} RG\n{FormatNumber(value.BorderWidth)} w\n" +
            $"{FormatNumber(points[0].X)} {FormatNumber(points[0].Y)} m\n");
        foreach (PdfPoint point in points.Skip(1))
            WriteAscii(output, $"{FormatNumber(point.X)} {FormatNumber(point.Y)} l\n");
        output.Write("S\n"u8);
        PdfPoint tip = points[0];
        PdfPoint next = points[1];
        WriteLineEnding(output, tip.X, tip.Y, next.X, next.Y,
            value.CalloutEnding, value.BorderWidth, value.BorderColor, null);
    }

    private static void AddVisualAnnotationObjects(
        ICollection<PdfIndirectObject> objects, AllocatedVisualAnnotation allocated,
        IReadOnlyList<AllocatedPage> pages, int sequence)
    {
        switch (allocated.Definition)
        {
            case LineAnnotationDefinition line:
                AddLineObjects(objects, allocated, line, pages, sequence);
                break;
            case ShapeAnnotationDefinition shape:
                AddShapeObjects(objects, allocated, shape, pages, sequence);
                break;
            case InkAnnotationDefinition ink:
                AddInkObjects(objects, allocated, ink, pages, sequence);
                break;
            case VertexAnnotationDefinition vertex:
                AddVertexObjects(objects, allocated, vertex, pages, sequence);
                break;
            default:
                throw new InvalidOperationException("Unknown visual annotation type.");
        }
    }

    private static void AddImageStampObjects(
        List<PdfIndirectObject> objects, AllocatedImageStamp allocated,
        IReadOnlyList<AllocatedPage> pages, int sequence, int imageNumber)
    {
        ImageStampDefinition stamp = allocated.Definition;
        var entries = new List<(string Name, PdfObject Value)>
        {
            ("Type", Name("Annot")), ("Subtype", Name("Stamp")),
            ("Rect", new PdfArray([
                Number(stamp.X), Number(stamp.Y),
                Number(stamp.X + stamp.Width), Number(stamp.Y + stamp.Height)])),
            ("P", new PdfIndirectReference(pages[stamp.PageIndex].PageNumber, 0)),
            ("F", new PdfInteger((int)(stamp.Metadata?.Flags ?? PdfAnnotationFlags.Print))),
            ("NM", Latin1String($"KillerPDF-Image-{sequence}")),
            ("Name", Name(PdfStampIconNames.Name(stamp.Icon))),
            ("AP", Dictionary(("N", new PdfIndirectReference(allocated.AppearanceNumber, 0))))
        };
        if (!string.IsNullOrEmpty(stamp.Contents))
            entries.Add(("Contents", UnicodeString(stamp.Contents)));
        AddAnnotationMetadata(entries, stamp.Metadata);
        objects.Add(new PdfIndirectObject(
            allocated.AnnotationNumber, 0, Dictionary([.. entries]), 0));

        PdfDictionary resources = Dictionary(("XObject", new PdfDictionary([
            new KeyValuePair<PdfName, PdfObject>(Name("Im1"),
                new PdfIndirectReference(imageNumber, 0))])));
        byte[] content = Encoding.ASCII.GetBytes(
            $"q\n{FormatNumber(stamp.Width)} 0 0 {FormatNumber(stamp.Height)} 0 0 cm\n/Im1 Do\nQ\n");
        objects.Add(new PdfIndirectObject(allocated.AppearanceNumber, 0,
            AnnotationAppearance(stamp.Width, stamp.Height, resources, content), 0));
    }

    private static void AddLineObjects(
        ICollection<PdfIndirectObject> objects, AllocatedVisualAnnotation allocated,
        LineAnnotationDefinition line, IReadOnlyList<AllocatedPage> pages, int sequence)
    {
        double padding = LineEndingPadding(line.LineWidth, line.StartEnding, line.EndEnding);
        Bounds bounds = PointBounds([line.Start, line.End], padding);
        var entries = CommonAnnotationEntries("Line", line.PageIndex,
            bounds.X, bounds.Y, bounds.Width, bounds.Height, pages,
            $"KillerPDF-Line-{sequence}", line.Color, line.Opacity,
            line.Contents, allocated.AppearanceNumber, line.Metadata);
        entries.Add(("L", new PdfArray([
            Number(line.Start.X), Number(line.Start.Y), Number(line.End.X), Number(line.End.Y)])));
        if (line.Intent is not null)
            entries.Add(("IT", Name(PdfAnnotationIntentNames.Name(line.Intent.Value))));
        entries.Add(("LE", new PdfArray([
            Name(PdfLineEndingStyleNames.Name(line.StartEnding)),
            Name(PdfLineEndingStyleNames.Name(line.EndEnding))])));
        entries.Add(("BS", BorderStyle(line.LineWidth, line.DashPattern)));
        if (line.InteriorColor.HasValue)
            entries.Add(("IC", ColorArray(line.InteriorColor.Value)));
        objects.Add(new PdfIndirectObject(allocated.AnnotationNumber, 0, Dictionary([.. entries]), 0));

        using var appearance = new MemoryStream();
        WriteAscii(appearance,
            $"q\n/GS1 gs\n{ColorOperands(line.Color)} RG\n{FormatNumber(line.LineWidth)} w\n" +
            DashOperator(line.DashPattern) +
            $"{FormatNumber(line.Start.X - bounds.X)} {FormatNumber(line.Start.Y - bounds.Y)} m\n" +
            $"{FormatNumber(line.End.X - bounds.X)} {FormatNumber(line.End.Y - bounds.Y)} l\nS\n");
        WriteLineEnding(appearance,
            line.Start.X - bounds.X, line.Start.Y - bounds.Y,
            line.End.X - bounds.X, line.End.Y - bounds.Y,
            line.StartEnding, line.LineWidth, line.Color, line.InteriorColor);
        WriteLineEnding(appearance,
            line.End.X - bounds.X, line.End.Y - bounds.Y,
            line.Start.X - bounds.X, line.Start.Y - bounds.Y,
            line.EndEnding, line.LineWidth, line.Color, line.InteriorColor);
        appearance.Write("Q\n"u8);
        objects.Add(new PdfIndirectObject(allocated.AppearanceNumber, 0,
            AnnotationAppearance(bounds.Width, bounds.Height, AnnotationResources(line.Opacity), appearance.ToArray()), 0));
    }

    private static void AddShapeObjects(
        ICollection<PdfIndirectObject> objects, AllocatedVisualAnnotation allocated,
        ShapeAnnotationDefinition shape, IReadOnlyList<AllocatedPage> pages, int sequence)
    {
        string subtype = shape.Type.ToString();
        var entries = CommonAnnotationEntries(subtype, shape.PageIndex,
            shape.X, shape.Y, shape.Width, shape.Height, pages,
            $"KillerPDF-{subtype}-{sequence}", shape.StrokeColor, shape.Opacity,
            shape.Contents, allocated.AppearanceNumber, shape.Metadata);
        entries.Add(("BS", BorderStyle(shape.LineWidth, shape.DashPattern)));
        if (shape.FillColor.HasValue) entries.Add(("IC", ColorArray(shape.FillColor.Value)));
        objects.Add(new PdfIndirectObject(allocated.AnnotationNumber, 0, Dictionary([.. entries]), 0));

        using var appearance = new MemoryStream();
        WriteAscii(appearance, "q\n/GS1 gs\n");
        WriteAscii(appearance, DashOperator(shape.DashPattern));
        WriteBox(appearance, shape.Width, shape.Height, shape.LineWidth,
            shape.StrokeColor, shape.FillColor, shape.Type == PdfShapeAnnotationType.Circle);
        appearance.Write("Q\n"u8);
        objects.Add(new PdfIndirectObject(allocated.AppearanceNumber, 0,
            AnnotationAppearance(shape.Width, shape.Height, AnnotationResources(shape.Opacity), appearance.ToArray()), 0));
    }

    private static void AddInkObjects(
        ICollection<PdfIndirectObject> objects, AllocatedVisualAnnotation allocated,
        InkAnnotationDefinition ink, IReadOnlyList<AllocatedPage> pages, int sequence)
    {
        PdfPoint[] allPoints = [.. ink.Strokes.SelectMany(stroke => stroke)];
        Bounds bounds = PointBounds(allPoints, ink.LineWidth / 2);
        var entries = CommonAnnotationEntries("Ink", ink.PageIndex,
            bounds.X, bounds.Y, bounds.Width, bounds.Height, pages,
            $"KillerPDF-Ink-{sequence}", ink.Color, ink.Opacity,
            ink.Contents, allocated.AppearanceNumber, ink.Metadata);
        entries.Add(("InkList", new PdfArray(ink.Strokes.Select(stroke =>
            (PdfObject)new PdfArray(stroke.SelectMany(point => new PdfObject[]
                { Number(point.X), Number(point.Y) }))))));
        entries.Add(("BS", BorderStyle(ink.LineWidth, ink.DashPattern)));
        objects.Add(new PdfIndirectObject(allocated.AnnotationNumber, 0, Dictionary([.. entries]), 0));

        using var appearance = new MemoryStream();
        WriteAscii(appearance,
            $"q\n/GS1 gs\n{ColorOperands(ink.Color)} RG\n{FormatNumber(ink.LineWidth)} w\n1 J\n1 j\n");
        WriteAscii(appearance, DashOperator(ink.DashPattern));
        foreach (IReadOnlyList<PdfPoint> stroke in ink.Strokes)
        {
            WriteAscii(appearance,
                $"{FormatNumber(stroke[0].X - bounds.X)} {FormatNumber(stroke[0].Y - bounds.Y)} m\n");
            foreach (PdfPoint point in stroke.Skip(1))
                WriteAscii(appearance,
                    $"{FormatNumber(point.X - bounds.X)} {FormatNumber(point.Y - bounds.Y)} l\n");
            appearance.Write("S\n"u8);
        }
        appearance.Write("Q\n"u8);
        objects.Add(new PdfIndirectObject(allocated.AppearanceNumber, 0,
            AnnotationAppearance(bounds.Width, bounds.Height, AnnotationResources(ink.Opacity), appearance.ToArray()), 0));
    }

    private static void AddVertexObjects(
        ICollection<PdfIndirectObject> objects, AllocatedVisualAnnotation allocated,
        VertexAnnotationDefinition vertex, IReadOnlyList<AllocatedPage> pages, int sequence)
    {
        double padding = vertex.Closed ? vertex.LineWidth / 2
            : LineEndingPadding(vertex.LineWidth, vertex.StartEnding, vertex.EndEnding);
        Bounds bounds = PointBounds(vertex.Vertices, padding);
        string subtype = vertex.Closed ? "Polygon" : "PolyLine";
        var entries = CommonAnnotationEntries(subtype, vertex.PageIndex,
            bounds.X, bounds.Y, bounds.Width, bounds.Height, pages,
            $"KillerPDF-{subtype}-{sequence}", vertex.Color, vertex.Opacity,
            vertex.Contents, allocated.AppearanceNumber, vertex.Metadata);
        entries.Add(("Vertices", new PdfArray(vertex.Vertices.SelectMany(point =>
            new PdfObject[] { Number(point.X), Number(point.Y) }))));
        if (vertex.Intent is not null)
            entries.Add(("IT", Name(PdfAnnotationIntentNames.Name(vertex.Intent.Value, vertex.Closed))));
        entries.Add(("BS", BorderStyle(vertex.LineWidth, vertex.DashPattern)));
        if (vertex.Closed && vertex.FillColor.HasValue)
            entries.Add(("IC", ColorArray(vertex.FillColor.Value)));
        if (!vertex.Closed)
        {
            entries.Add(("LE", new PdfArray([
                Name(PdfLineEndingStyleNames.Name(vertex.StartEnding)),
                Name(PdfLineEndingStyleNames.Name(vertex.EndEnding))])));
            if (vertex.InteriorColor.HasValue)
                entries.Add(("IC", ColorArray(vertex.InteriorColor.Value)));
        }
        objects.Add(new PdfIndirectObject(
            allocated.AnnotationNumber, 0, Dictionary([.. entries]), 0));

        using var appearance = new MemoryStream();
        WriteAscii(appearance, $"q\n/GS1 gs\n");
        if (vertex.FillColor.HasValue)
            WriteAscii(appearance, $"{ColorOperands(vertex.FillColor.Value)} rg\n");
        WriteAscii(appearance,
            $"{ColorOperands(vertex.Color)} RG\n{FormatNumber(vertex.LineWidth)} w\n" +
            DashOperator(vertex.DashPattern) +
            $"{FormatNumber(vertex.Vertices[0].X - bounds.X)} " +
            $"{FormatNumber(vertex.Vertices[0].Y - bounds.Y)} m\n");
        foreach (PdfPoint point in vertex.Vertices.Skip(1))
            WriteAscii(appearance,
                $"{FormatNumber(point.X - bounds.X)} {FormatNumber(point.Y - bounds.Y)} l\n");
        if (vertex.Closed) appearance.Write("h\n"u8);
        appearance.Write(vertex.FillColor.HasValue ? "B\n"u8 : "S\n"u8);
        if (!vertex.Closed)
        {
            WriteLineEnding(appearance,
                vertex.Vertices[0].X - bounds.X, vertex.Vertices[0].Y - bounds.Y,
                vertex.Vertices[1].X - bounds.X, vertex.Vertices[1].Y - bounds.Y,
                vertex.StartEnding, vertex.LineWidth, vertex.Color, vertex.InteriorColor);
            int last = vertex.Vertices.Count - 1;
            WriteLineEnding(appearance,
                vertex.Vertices[last].X - bounds.X, vertex.Vertices[last].Y - bounds.Y,
                vertex.Vertices[last - 1].X - bounds.X, vertex.Vertices[last - 1].Y - bounds.Y,
                vertex.EndEnding, vertex.LineWidth, vertex.Color, vertex.InteriorColor);
        }
        appearance.Write("Q\n"u8);
        objects.Add(new PdfIndirectObject(allocated.AppearanceNumber, 0,
            AnnotationAppearance(bounds.Width, bounds.Height,
                AnnotationResources(vertex.Opacity), appearance.ToArray()), 0));
    }

    private static List<(string Name, PdfObject Value)> CommonAnnotationEntries(
        string subtype, int pageIndex, double x, double y, double width, double height,
        IReadOnlyList<AllocatedPage> pages, string identity, PdfRgbColor color,
        double opacity, string? contents, int appearanceNumber,
        PdfAnnotationMetadata? metadata = null)
    {
        var entries = new List<(string Name, PdfObject Value)>
        {
            ("Type", Name("Annot")),
            ("Subtype", Name(subtype)),
            ("Rect", new PdfArray([Number(x), Number(y), Number(x + width), Number(y + height)])),
            ("P", new PdfIndirectReference(pages[pageIndex].PageNumber, 0)),
            ("F", new PdfInteger((int)(metadata?.Flags ?? PdfAnnotationFlags.Print))),
            ("NM", Latin1String(identity)),
            ("C", ColorArray(color)),
            ("CA", new PdfReal(opacity)),
            ("AP", Dictionary(("N", new PdfIndirectReference(appearanceNumber, 0))))
        };
        if (!string.IsNullOrEmpty(contents)) entries.Add(("Contents", UnicodeString(contents)));
        AddAnnotationMetadata(entries, metadata);
        return entries;
    }

    private static void AddAnnotationMetadata(
        List<(string Name, PdfObject Value)> entries,
        PdfAnnotationMetadata? metadata)
    {
        if (!string.IsNullOrEmpty(metadata?.Author))
            entries.Add(("T", UnicodeString(metadata.Author)));
        if (!string.IsNullOrEmpty(metadata?.Subject))
            entries.Add(("Subj", UnicodeString(metadata.Subject)));
        if (metadata?.CreationDate is DateTimeOffset creationDate)
            entries.Add(("CreationDate", Latin1String(PdfDate(creationDate))));
        if (metadata?.ModificationDate is DateTimeOffset modificationDate)
            entries.Add(("M", Latin1String(PdfDate(modificationDate))));
    }

    private static PdfDictionary BorderStyle(
        double width, IReadOnlyList<double>? dashPattern = null) =>
        dashPattern is null
            ? Dictionary(("W", Number(width)), ("S", Name("S")))
            : Dictionary(
                ("W", Number(width)), ("S", Name("D")),
                ("D", new PdfArray(dashPattern.Select(Number))));

    private static string DashOperator(IReadOnlyList<double>? dashPattern) =>
        dashPattern is null
            ? string.Empty
            : $"[{string.Join(' ', dashPattern.Select(FormatNumber))}] 0 d\n";

    private static PdfDictionary AnnotationResources(
        double opacity, (PdfName Name, PdfObject Reference)? font = null)
    {
        var entries = new List<(string Name, PdfObject Value)>
        {
            ("ExtGState", new PdfDictionary([
                new KeyValuePair<PdfName, PdfObject>(Name("GS1"), Dictionary(
                    ("Type", Name("ExtGState")), ("ca", Number(opacity)), ("CA", Number(opacity))
                ))]))
        };
        if (font.HasValue)
            entries.Add(("Font", new PdfDictionary([
                new KeyValuePair<PdfName, PdfObject>(font.Value.Name, font.Value.Reference)])));
        return Dictionary([.. entries]);
    }

    private static double LineEndingPadding(
        double lineWidth, PdfLineEndingStyle start, PdfLineEndingStyle end) =>
        start == PdfLineEndingStyle.None && end == PdfLineEndingStyle.None
            ? lineWidth / 2 : Math.Max(6, lineWidth * 4);

    private static void WriteLineEnding(
        Stream output, double x, double y, double neighborX, double neighborY,
        PdfLineEndingStyle style, double lineWidth, PdfRgbColor _,
        PdfRgbColor? interiorColor)
    {
        if (style == PdfLineEndingStyle.None) return;
        double dx = neighborX - x;
        double dy = neighborY - y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0) return;
        dx /= length;
        dy /= length;
        double nx = -dy;
        double ny = dx;
        double size = Math.Max(6, lineWidth * 4);
        bool reverse = style is PdfLineEndingStyle.ReverseOpenArrow
            or PdfLineEndingStyle.ReverseClosedArrow;
        double direction = reverse ? -1 : 1;
        double backX = x + dx * size * direction;
        double backY = y + dy * size * direction;
        double wing = size * 0.45;
        double firstX = backX + nx * wing;
        double firstY = backY + ny * wing;
        double secondX = backX - nx * wing;
        double secondY = backY - ny * wing;
        switch (style)
        {
            case PdfLineEndingStyle.OpenArrow:
            case PdfLineEndingStyle.ReverseOpenArrow:
                WriteAscii(output,
                    $"{FormatNumber(firstX)} {FormatNumber(firstY)} m\n" +
                    $"{FormatNumber(x)} {FormatNumber(y)} l\n" +
                    $"{FormatNumber(secondX)} {FormatNumber(secondY)} l\nS\n");
                break;
            case PdfLineEndingStyle.ClosedArrow:
            case PdfLineEndingStyle.ReverseClosedArrow:
                WriteAscii(output,
                    (interiorColor.HasValue
                        ? $"{ColorOperands(interiorColor.Value)} rg\n" : string.Empty) +
                    $"{FormatNumber(x)} {FormatNumber(y)} m\n" +
                    $"{FormatNumber(firstX)} {FormatNumber(firstY)} l\n" +
                    $"{FormatNumber(secondX)} {FormatNumber(secondY)} l\nh\n" +
                    (interiorColor.HasValue ? "B\n" : "S\n"));
                break;
            case PdfLineEndingStyle.Square:
            {
                double half = size * 0.35;
                WriteAscii(output,
                    $"{FormatNumber(x - half)} {FormatNumber(y - half)} " +
                    $"{FormatNumber(half * 2)} {FormatNumber(half * 2)} re\n" +
                    (interiorColor.HasValue
                        ? $"{ColorOperands(interiorColor.Value)} rg\nB\n" : "S\n"));
                break;
            }
            case PdfLineEndingStyle.Circle:
            {
                double diameter = size * 0.7;
                WriteEllipse(output, x - diameter / 2, y - diameter / 2, diameter, diameter);
                if (interiorColor.HasValue)
                {
                    WriteAscii(output, $"{ColorOperands(interiorColor.Value)} rg\n");
                    output.Write("B\n"u8);
                }
                else output.Write("S\n"u8);
                break;
            }
            case PdfLineEndingStyle.Diamond:
            {
                double half = size * 0.45;
                WriteAscii(output,
                    $"{FormatNumber(x)} {FormatNumber(y + half)} m\n" +
                    $"{FormatNumber(x + half)} {FormatNumber(y)} l\n" +
                    $"{FormatNumber(x)} {FormatNumber(y - half)} l\n" +
                    $"{FormatNumber(x - half)} {FormatNumber(y)} l\nh\n" +
                    (interiorColor.HasValue
                        ? $"{ColorOperands(interiorColor.Value)} rg\nB\n" : "S\n"));
                break;
            }
            case PdfLineEndingStyle.Butt:
            {
                double half = size * 0.45;
                WriteAscii(output,
                    $"{FormatNumber(x + nx * half)} {FormatNumber(y + ny * half)} m\n" +
                    $"{FormatNumber(x - nx * half)} {FormatNumber(y - ny * half)} l\nS\n");
                break;
            }
            case PdfLineEndingStyle.Slash:
            {
                double half = size * 0.5;
                double slashX = nx * 0.85 + dx * 0.5;
                double slashY = ny * 0.85 + dy * 0.5;
                WriteAscii(output,
                    $"{FormatNumber(x + slashX * half)} {FormatNumber(y + slashY * half)} m\n" +
                    $"{FormatNumber(x - slashX * half)} {FormatNumber(y - slashY * half)} l\nS\n");
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(style));
        }
    }

    private static void WriteBox(
        Stream output, double width, double height, double lineWidth,
        PdfRgbColor stroke, PdfRgbColor? fill, bool ellipse)
    {
        double inset = lineWidth / 2;
        if (fill.HasValue) WriteAscii(output, $"{ColorOperands(fill.Value)} rg\n");
        WriteAscii(output, $"{ColorOperands(stroke)} RG\n{FormatNumber(lineWidth)} w\n");
        if (ellipse)
            WriteEllipse(output, inset, inset, Math.Max(0, width - lineWidth), Math.Max(0, height - lineWidth));
        else
            WriteAscii(output,
                $"{FormatNumber(inset)} {FormatNumber(inset)} {FormatNumber(Math.Max(0, width - lineWidth))} {FormatNumber(Math.Max(0, height - lineWidth))} re\n");
        output.Write(fill.HasValue ? "B\n"u8 : "S\n"u8);
    }

    private static void WriteEllipse(Stream output, double x, double y, double width, double height)
    {
        const double kappa = 0.5522847498307936;
        double rx = width / 2, ry = height / 2, cx = x + rx, cy = y + ry;
        WriteAscii(output, $"{FormatNumber(cx + rx)} {FormatNumber(cy)} m\n");
        WriteAscii(output, $"{FormatNumber(cx + rx)} {FormatNumber(cy + ry * kappa)} {FormatNumber(cx + rx * kappa)} {FormatNumber(cy + ry)} {FormatNumber(cx)} {FormatNumber(cy + ry)} c\n");
        WriteAscii(output, $"{FormatNumber(cx - rx * kappa)} {FormatNumber(cy + ry)} {FormatNumber(cx - rx)} {FormatNumber(cy + ry * kappa)} {FormatNumber(cx - rx)} {FormatNumber(cy)} c\n");
        WriteAscii(output, $"{FormatNumber(cx - rx)} {FormatNumber(cy - ry * kappa)} {FormatNumber(cx - rx * kappa)} {FormatNumber(cy - ry)} {FormatNumber(cx)} {FormatNumber(cy - ry)} c\n");
        WriteAscii(output, $"{FormatNumber(cx + rx * kappa)} {FormatNumber(cy - ry)} {FormatNumber(cx + rx)} {FormatNumber(cy - ry * kappa)} {FormatNumber(cx + rx)} {FormatNumber(cy)} c\nh\n");
    }

    private static void WriteFreeText(
        Stream output, FreeTextDefinition value, PdfName fontResource,
        EmbeddedFontUsage? fontUsage)
    {
        double padding = Math.Max(3, value.BorderWidth + 2);
        double lineHeight = value.FontSize * 1.2;
        List<string> lines = WrapText(value.Contents, value.Font, value.FontSize,
            Math.Max(1, value.Width - padding * 2));
        WriteAscii(output,
            $"BT\n{NameToken(fontResource)} {FormatNumber(value.FontSize)} Tf\n" +
            $"{ColorOperands(value.TextColor)} rg\n");
        for (int index = 0; index < lines.Count; index++)
        {
            double lineWidth = TextWidth(lines[index], value.Font, value.FontSize);
            double x = value.Alignment switch
            {
                PdfTextAlignment.Left => padding,
                PdfTextAlignment.Center => Math.Max(padding, (value.Width - lineWidth) / 2),
                PdfTextAlignment.Right => Math.Max(padding, value.Width - padding - lineWidth),
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
            double y = Math.Max(
                padding, value.Height - padding - value.FontSize - index * lineHeight);
            WriteAscii(output,
                $"1 0 0 1 {FormatNumber(x)} {FormatNumber(y)} Tm\n");
            WriteShownText(output, lines[index], value.Font, fontUsage);
            if ((index + 2) * lineHeight > value.Height - padding) break;
        }
        output.Write("ET\n"u8);
    }

    private static List<string> WrapText(
        string text, TrueTypeFont font, double fontSize, double maximumWidth)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Split('\n'))
        {
            if (paragraph.Length == 0) { lines.Add(string.Empty); continue; }
            var current = new StringBuilder();
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = current.Length == 0 ? word : $"{current} {word}";
                if (current.Length > 0 && TextWidth(candidate, font, fontSize) > maximumWidth)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    current.Append(word);
                }
                else
                {
                    if (current.Length > 0) current.Append(' ');
                    current.Append(word);
                }
            }
            lines.Add(current.ToString());
        }
        return lines;
    }

    private static double TextWidth(string value, TrueTypeFont font, double fontSize) =>
        font.MapText(value).Sum(mapping => font.GetPdfAdvanceWidth(mapping.Glyph))
            * fontSize / 1000;

    private static Bounds PointBounds(IEnumerable<PdfPoint> points, double padding)
    {
        PdfPoint[] values = [.. points];
        double minX = values.Min(point => point.X) - padding;
        double minY = values.Min(point => point.Y) - padding;
        double maxX = values.Max(point => point.X) + padding;
        double maxY = values.Max(point => point.Y) + padding;
        return new Bounds(minX, minY, maxX - minX, maxY - minY);
    }

    private sealed record FreeTextDefinition(
        int PageIndex, double X, double Y, double Width, double Height, string Contents,
        TrueTypeFont Font, double FontSize, PdfRgbColor TextColor, PdfRgbColor? FillColor,
        PdfRgbColor BorderColor, double BorderWidth, double Opacity,
        PdfAnnotationMetadata? Metadata, PdfTextAlignment Alignment,
        IReadOnlyList<double>? DashPattern, PdfFreeTextIntent Intent,
        IReadOnlyList<PdfPoint>? CalloutLine, PdfLineEndingStyle CalloutEnding);
    private sealed record AllocatedFreeText(
        FreeTextDefinition Definition, int AnnotationNumber, int AppearanceNumber);
    private abstract record VisualAnnotationDefinition(
        int PageIndex, PdfRgbColor Color, double LineWidth, double Opacity, string? Contents,
        IReadOnlyList<double>? DashPattern, PdfAnnotationMetadata? Metadata);
    private sealed record LineAnnotationDefinition(
        int PageIndex, PdfPoint Start, PdfPoint End, PdfRgbColor Color,
        double LineWidth, double Opacity, string? Contents,
        PdfLineEndingStyle StartEnding, PdfLineEndingStyle EndEnding,
        IReadOnlyList<double>? DashPattern, PdfRgbColor? InteriorColor,
        PdfAnnotationMetadata? Metadata, PdfLineAnnotationIntent? Intent)
        : VisualAnnotationDefinition(
            PageIndex, Color, LineWidth, Opacity, Contents, DashPattern, Metadata);
    private sealed record ShapeAnnotationDefinition(
        PdfShapeAnnotationType Type, int PageIndex, double X, double Y, double Width, double Height,
        PdfRgbColor StrokeColor, PdfRgbColor? FillColor, double LineWidth, double Opacity,
        string? Contents, IReadOnlyList<double>? DashPattern, PdfAnnotationMetadata? Metadata)
        : VisualAnnotationDefinition(
            PageIndex, StrokeColor, LineWidth, Opacity, Contents, DashPattern, Metadata);
    private sealed record InkAnnotationDefinition(
        int PageIndex, IReadOnlyList<IReadOnlyList<PdfPoint>> Strokes, PdfRgbColor Color,
        double LineWidth, double Opacity, string? Contents, IReadOnlyList<double>? DashPattern,
        PdfAnnotationMetadata? Metadata)
        : VisualAnnotationDefinition(
            PageIndex, Color, LineWidth, Opacity, Contents, DashPattern, Metadata);
    private sealed record VertexAnnotationDefinition(
        int PageIndex, IReadOnlyList<PdfPoint> Vertices, bool Closed,
        PdfRgbColor Color, PdfRgbColor? FillColor,
        double LineWidth, double Opacity, string? Contents,
        PdfLineEndingStyle StartEnding, PdfLineEndingStyle EndEnding,
        IReadOnlyList<double>? DashPattern, PdfRgbColor? InteriorColor,
        PdfAnnotationMetadata? Metadata, PdfVertexAnnotationIntent? Intent)
        : VisualAnnotationDefinition(
            PageIndex, Color, LineWidth, Opacity, Contents, DashPattern, Metadata);
    private sealed record AllocatedVisualAnnotation(
        VisualAnnotationDefinition Definition, int AnnotationNumber, int AppearanceNumber);
    private sealed record ImageStampDefinition(
        int PageIndex, double X, double Y, double Width, double Height,
        PdfImage Image, string? Contents, PdfAnnotationMetadata? Metadata, PdfStampIcon Icon);
    private sealed record AllocatedImageStamp(
        ImageStampDefinition Definition, int AnnotationNumber, int AppearanceNumber);
    private sealed record Bounds(double X, double Y, double Width, double Height);
    private enum PdfShapeAnnotationType { Square, Circle }
}
