using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Fonts;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Tests.Fonts;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfVisualAnnotationAuthoringTests
{
    [Fact]
    public void PdfUa2_FreeTextAndImageStampReceiveStructureParents()
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        PdfImage image = PdfImage.FromRgb(1, 1, new byte[] { 30, 100, 200 });
        PdfDocument document = Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible visual annotations",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddBlankPage()
            .AddFreeText(0, 40, 600, 180, 60, "AA", font)
            .AddImageStamp(0, 40, 520, 40, 40, image,
                contents: "Blue review image")
            .AddStructureContainer(PdfStructureType.Document));
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        PdfDictionary page = ResolveDictionary(document,
            Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        PdfArray annotations = Assert.IsType<PdfArray>(page[Name("Annots")]);

        Assert.Equal(2, annotations.Count);
        Assert.All(annotations, value => Assert.True(
            ResolveDictionary(document, value).ContainsKey(Name("StructParent"))));
    }

    [Fact]
    public void AddFreeText_EmbedsFontAndWritesExplicitAppearance()
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddFreeText(0, 40, 600, 180, 60, "AA", font, 14,
                fillColor: new PdfRgbColor(1, 1, 0.8)));
        PdfDictionary annotation = Annotation(document);
        PdfStream appearance = Appearance(document, annotation);
        var resources = Assert.IsType<PdfDictionary>(appearance.Dictionary[Name("Resources")]);
        var fonts = Assert.IsType<PdfDictionary>(resources[Name("Font")]);
        PdfDictionary type0 = ResolveDictionary(document, fonts[Name("FormF1")]);

        Assert.Equal("FreeText", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal("AA", DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("Contents")])));
        Assert.Equal("Type0", Assert.IsType<PdfName>(type0[Name("Subtype")]).ValueAsLatin1());
        Assert.Contains("/FormF1 14 Tf", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Theory]
    [InlineData(PdfTextAlignment.Left, 0)]
    [InlineData(PdfTextAlignment.Center, 1)]
    [InlineData(PdfTextAlignment.Right, 2)]
    public void AddFreeText_WritesAlignmentAndPositionedAppearance(
        PdfTextAlignment alignment, int expectedQuadding)
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddFreeText(0, 20, 30, 180, 60, "AA", font, alignment: alignment));
        PdfDictionary annotation = Annotation(document);
        string appearance = Encoding.ASCII.GetString(
            Appearance(document, annotation).EncodedData.Span);

        Assert.Equal(expectedQuadding,
            Assert.IsType<PdfInteger>(annotation[Name("Q")]).Value);
        Assert.Contains(" Tm\n", appearance);
    }

    [Fact]
    public void AddFreeText_WritesDashedBorderStyleAndMatchingAppearance()
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddFreeText(0, 20, 30, 180, 60, "AA", font, dashPattern: [4, 2]));
        PdfDictionary annotation = Annotation(document);
        var borderStyle = Assert.IsType<PdfDictionary>(annotation[Name("BS")]);
        string appearance = Encoding.ASCII.GetString(
            Appearance(document, annotation).EncodedData.Span);

        Assert.Equal("D", Assert.IsType<PdfName>(borderStyle[Name("S")]).ValueAsLatin1());
        Assert.Equal(2, Assert.IsType<PdfArray>(borderStyle[Name("D")]).Count);
        Assert.Contains("[4 2] 0 d", appearance);
    }

    [Theory]
    [InlineData(PdfFreeTextIntent.FreeText, "FreeText")]
    [InlineData(PdfFreeTextIntent.Callout, "FreeTextCallout")]
    [InlineData(PdfFreeTextIntent.TypeWriter, "FreeTextTypeWriter")]
    public void AddFreeText_WritesStandardIntent(PdfFreeTextIntent intent, string expectedName)
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        IReadOnlyList<PdfPoint>? callout = intent == PdfFreeTextIntent.Callout
            ? [new PdfPoint(5, 10), new PdfPoint(15, 20), new PdfPoint(20, 30)]
            : null;
        PdfDictionary annotation = Annotation(Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddFreeText(0, 20, 30, 180, 60, "AA", font,
                intent: intent, calloutLine: callout)));

        Assert.Equal(expectedName,
            Assert.IsType<PdfName>(annotation[Name("IT")]).ValueAsLatin1());
    }

    [Fact]
    public void AddFreeTextCallout_WritesGeometryEndingAndExpandedAppearance()
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddFreeText(0, 100, 100, 180, 60, "AA", font,
                intent: PdfFreeTextIntent.Callout,
                calloutLine: [new PdfPoint(20, 40), new PdfPoint(60, 80), new PdfPoint(100, 100)],
                calloutEnding: PdfLineEndingStyle.ClosedArrow));
        PdfDictionary annotation = Annotation(document);
        PdfStream appearance = Appearance(document, annotation);

        Assert.Equal(6, Assert.IsType<PdfArray>(annotation[Name("CL")]).Count);
        Assert.Equal("ClosedArrow",
            Assert.IsType<PdfName>(annotation[Name("LE")]).ValueAsLatin1());
        Assert.Contains(" l\nS\n", Encoding.ASCII.GetString(appearance.EncodedData.Span));
        Assert.Equal(4, Assert.IsType<PdfArray>(appearance.Dictionary[Name("BBox")]).Count);
    }

    [Theory]
    [InlineData("Square")]
    [InlineData("Circle")]
    public void ShapeAnnotations_WriteStandardSubtypeFillAndAppearance(string subtype)
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        _ = subtype == "Square"
            ? builder.AddRectangleAnnotation(0, 30, 40, 100, 50, fillColor: PdfRgbColor.Yellow)
            : builder.AddEllipseAnnotation(0, 30, 40, 100, 50, fillColor: PdfRgbColor.Yellow);
        PdfDocument document = Open(builder);
        PdfDictionary annotation = Annotation(document);

        Assert.Equal(subtype, Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(3, Assert.IsType<PdfArray>(annotation[Name("IC")]).Count);
        Assert.True(Appearance(document, annotation).EncodedData.Length > 0);
    }

    [Fact]
    public void LineAnnotation_WritesEndpointsAndBorderStyle()
    {
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddLineAnnotation(0, new PdfPoint(20, 30), new PdfPoint(120, 80), lineWidth: 3,
                startEnding: PdfLineEndingStyle.OpenArrow,
                endEnding: PdfLineEndingStyle.ClosedArrow,
                interiorColor: PdfRgbColor.Yellow));
        PdfDictionary annotation = Annotation(document);
        PdfArray endings = Assert.IsType<PdfArray>(annotation[Name("LE")]);
        string appearance = Encoding.ASCII.GetString(
            Appearance(document, annotation).EncodedData.Span);

        Assert.Equal("Line", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(4, Assert.IsType<PdfArray>(annotation[Name("L")]).Count);
        Assert.Equal(3, Assert.IsType<PdfInteger>(
            Assert.IsType<PdfDictionary>(annotation[Name("BS")])[Name("W")]).Value);
        Assert.Equal("OpenArrow", Assert.IsType<PdfName>(endings[0]).ValueAsLatin1());
        Assert.Equal("ClosedArrow", Assert.IsType<PdfName>(endings[1]).ValueAsLatin1());
        Assert.Equal(3, Assert.IsType<PdfArray>(annotation[Name("IC")]).Count);
        Assert.Contains("h\nB\n", appearance);
    }

    [Theory]
    [InlineData(PdfLineAnnotationIntent.Arrow, "LineArrow")]
    [InlineData(PdfLineAnnotationIntent.Dimension, "LineDimension")]
    public void LineAnnotation_WritesStandardIntent(
        PdfLineAnnotationIntent intent, string expectedName)
    {
        PdfDictionary annotation = Annotation(Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddLineAnnotation(0, new PdfPoint(20, 30), new PdfPoint(120, 80), intent: intent)));

        Assert.Equal(expectedName,
            Assert.IsType<PdfName>(annotation[Name("IT")]).ValueAsLatin1());
    }

    [Theory]
    [InlineData(false, PdfVertexAnnotationIntent.Dimension, "PolyLineDimension")]
    [InlineData(true, PdfVertexAnnotationIntent.Dimension, "PolygonDimension")]
    [InlineData(true, PdfVertexAnnotationIntent.Cloud, "PolygonCloud")]
    public void VertexAnnotation_WritesStandardIntent(
        bool closed, PdfVertexAnnotationIntent intent, string expectedName)
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        PdfPoint[] points = [new(10, 10), new(60, 40), new(100, 10)];
        _ = closed
            ? builder.AddPolygonAnnotation(0, points, intent: intent)
            : builder.AddPolylineAnnotation(0, points, intent: intent);
        PdfDictionary annotation = Annotation(Open(builder));

        Assert.Equal(expectedName,
            Assert.IsType<PdfName>(annotation[Name("IT")]).ValueAsLatin1());
    }

    [Theory]
    [InlineData(PdfLineEndingStyle.None, "None")]
    [InlineData(PdfLineEndingStyle.Square, "Square")]
    [InlineData(PdfLineEndingStyle.Circle, "Circle")]
    [InlineData(PdfLineEndingStyle.Diamond, "Diamond")]
    [InlineData(PdfLineEndingStyle.OpenArrow, "OpenArrow")]
    [InlineData(PdfLineEndingStyle.ClosedArrow, "ClosedArrow")]
    [InlineData(PdfLineEndingStyle.Butt, "Butt")]
    [InlineData(PdfLineEndingStyle.ReverseOpenArrow, "ROpenArrow")]
    [InlineData(PdfLineEndingStyle.ReverseClosedArrow, "RClosedArrow")]
    [InlineData(PdfLineEndingStyle.Slash, "Slash")]
    public void LineAnnotation_WritesEveryStandardEnding(
        PdfLineEndingStyle style, string expectedName)
    {
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddLineAnnotation(0, new PdfPoint(20, 30), new PdfPoint(120, 80),
                startEnding: style));
        PdfDictionary annotation = Annotation(document);
        PdfArray endings = Assert.IsType<PdfArray>(annotation[Name("LE")]);

        Assert.Equal(expectedName, Assert.IsType<PdfName>(endings[0]).ValueAsLatin1());
        Assert.NotEmpty(Appearance(document, annotation).EncodedData.ToArray());
    }

    [Fact]
    public void PolygonAnnotation_WritesVerticesFillAndClosedAppearance()
    {
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddPolygonAnnotation(0, [
                new PdfPoint(20, 20), new PdfPoint(100, 30), new PdfPoint(60, 100)],
                strokeColor: new PdfRgbColor(0.1, 0.2, 0.8),
                fillColor: new PdfRgbColor(0.9, 0.8, 0.2), lineWidth: 2,
                dashPattern: [6, 2],
                annotationMetadata: new PdfAnnotationMetadata
                {
                    Author = "Renée",
                    Subject = "Geometry review",
                    Flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.LockedContents,
                    CreationDate = new DateTimeOffset(2026, 8, 23, 12, 34, 56, TimeSpan.FromHours(-7)),
                    ModificationDate = new DateTimeOffset(2026, 8, 23, 13, 45, 0, TimeSpan.FromHours(-7))
                }));
        PdfDictionary annotation = Annotation(document);
        string appearance = Encoding.ASCII.GetString(
            Appearance(document, annotation).EncodedData.Span);

        Assert.Equal("Polygon",
            Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(6, Assert.IsType<PdfArray>(annotation[Name("Vertices")]).Count);
        Assert.Equal(3, Assert.IsType<PdfArray>(annotation[Name("IC")]).Count);
        PdfDictionary border = Assert.IsType<PdfDictionary>(annotation[Name("BS")]);
        Assert.Equal("D", Assert.IsType<PdfName>(border[Name("S")]).ValueAsLatin1());
        Assert.Equal(2, Assert.IsType<PdfArray>(border[Name("D")]).Count);
        Assert.Contains("[6 2] 0 d", appearance);
        Assert.Contains("h\nB\n", appearance);
        Assert.Equal("Renée", DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("T")])));
        Assert.Equal("Geometry review",
            DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("Subj")])));
        Assert.Equal("D:20260823123456-07'00'",
            Encoding.Latin1.GetString(Assert.IsType<PdfString>(
                annotation[Name("CreationDate")]).Bytes.Span));
        Assert.Equal("D:20260823134500-07'00'",
            Encoding.Latin1.GetString(Assert.IsType<PdfString>(
                annotation[Name("M")]).Bytes.Span));
        Assert.Equal(516, Assert.IsType<PdfInteger>(annotation[Name("F")]).Value);
    }

    [Fact]
    public void PolylineAnnotation_WritesVerticesAndOpenAppearance()
    {
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddPolylineAnnotation(0, [
                new PdfPoint(20, 20), new PdfPoint(100, 30), new PdfPoint(60, 100)],
                startEnding: PdfLineEndingStyle.ClosedArrow,
                endEnding: PdfLineEndingStyle.OpenArrow,
                interiorColor: PdfRgbColor.Yellow));
        PdfDictionary annotation = Annotation(document);
        string appearance = Encoding.ASCII.GetString(
            Appearance(document, annotation).EncodedData.Span);

        Assert.Equal("PolyLine",
            Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(6, Assert.IsType<PdfArray>(annotation[Name("Vertices")]).Count);
        Assert.Equal(2, Assert.IsType<PdfArray>(annotation[Name("LE")]).Count);
        Assert.Equal(3, Assert.IsType<PdfArray>(annotation[Name("IC")]).Count);
        Assert.Contains("S\nQ\n", appearance);
        Assert.Contains("h\nB\n", appearance);
    }

    [Fact]
    public void InkAnnotation_PreservesEachStrokeAndWritesAppearance()
    {
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddInkAnnotation(0,
            [
                [new PdfPoint(10, 10), new PdfPoint(20, 30), new PdfPoint(40, 20)],
                [new PdfPoint(50, 50), new PdfPoint(70, 60)]
            ]));
        PdfDictionary annotation = Annotation(document);
        var inkList = Assert.IsType<PdfArray>(annotation[Name("InkList")]);

        Assert.Equal("Ink", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(2, inkList.Count);
        Assert.Equal(6, Assert.IsType<PdfArray>(inkList[0]).Count);
        Assert.Contains("1 J", Encoding.ASCII.GetString(Appearance(document, annotation).EncodedData.Span));
    }

    [Fact]
    public void ImageStamp_PreservesRgbaTransparencyInItsAppearance()
    {
        PdfImage image = PdfImage.FromRgba(1, 1, new byte[] { 20, 40, 60, 96 });
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddImageStamp(0, 20, 30, 100, 50, image, "Signature image"));
        PdfDictionary annotation = Annotation(document);
        PdfStream appearance = Appearance(document, annotation);
        PdfDictionary xobjects = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(appearance.Dictionary[Name("Resources")])[Name("XObject")]);
        PdfStream imageStream = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(xobjects[Name("Im1")])));
        PdfStream mask = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(imageStream.Dictionary[Name("SMask")])));

        Assert.Equal("Stamp", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal("Image", Assert.IsType<PdfName>(annotation[Name("Name")]).ValueAsLatin1());
        Assert.Equal("DeviceGray", Assert.IsType<PdfName>(mask.Dictionary[Name("ColorSpace")]).ValueAsLatin1());
        Assert.Contains("/Im1 Do", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Theory]
    [InlineData(PdfStampIcon.Image, "Image")]
    [InlineData(PdfStampIcon.Approved, "Approved")]
    [InlineData(PdfStampIcon.Experimental, "Experimental")]
    [InlineData(PdfStampIcon.NotApproved, "NotApproved")]
    [InlineData(PdfStampIcon.AsIs, "AsIs")]
    [InlineData(PdfStampIcon.Expired, "Expired")]
    [InlineData(PdfStampIcon.NotForPublicRelease, "NotForPublicRelease")]
    [InlineData(PdfStampIcon.Confidential, "Confidential")]
    [InlineData(PdfStampIcon.Final, "Final")]
    [InlineData(PdfStampIcon.Sold, "Sold")]
    [InlineData(PdfStampIcon.Departmental, "Departmental")]
    [InlineData(PdfStampIcon.ForComment, "ForComment")]
    [InlineData(PdfStampIcon.TopSecret, "TopSecret")]
    [InlineData(PdfStampIcon.Draft, "Draft")]
    [InlineData(PdfStampIcon.ForPublicRelease, "ForPublicRelease")]
    public void ImageStamp_WritesStandardSemanticName(PdfStampIcon icon, string expectedName)
    {
        PdfImage image = PdfImage.FromRgb(1, 1, new byte[] { 20, 40, 60 });
        PdfDictionary annotation = Annotation(Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddImageStamp(0, 20, 30, 100, 50, image, icon: icon)));

        Assert.Equal(expectedName,
            Assert.IsType<PdfName>(annotation[Name("Name")]).ValueAsLatin1());
    }

    [Fact]
    public void VisualAnnotationArguments_AreValidated()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentException>(() => builder.AddLineAnnotation(
            0, new PdfPoint(1, 1), new PdfPoint(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddLineAnnotation(
            0, new PdfPoint(1, 1), new PdfPoint(2, 2),
            startEnding: (PdfLineEndingStyle)99));
        Assert.Throws<ArgumentException>(() => builder.AddInkAnnotation(0, Array.Empty<PdfPoint>()));
        Assert.Throws<ArgumentException>(() => builder.AddPolylineAnnotation(
            0, [new PdfPoint(1, 1)]));
        Assert.Throws<ArgumentException>(() => builder.AddPolylineAnnotation(
            0, [new PdfPoint(1, 1), new PdfPoint(2, 2)],
            intent: PdfVertexAnnotationIntent.Cloud));
        Assert.Throws<ArgumentException>(() => builder.AddPolygonAnnotation(
            0, [new PdfPoint(1, 1), new PdfPoint(2, 2)]));
        Assert.Throws<ArgumentException>(() => builder.AddPolygonAnnotation(
            0, [new PdfPoint(1, 1), new PdfPoint(1, 1), new PdfPoint(1, 1)]));
        Assert.Throws<ArgumentException>(() => builder.AddPolygonAnnotation(
            0, [new PdfPoint(1, 1), new PdfPoint(2, 1), new PdfPoint(1, 2)],
            dashPattern: [0, 0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddInkAnnotation(
            0, [new PdfPoint(1, 1), new PdfPoint(2, 2)], dashPattern: [2, -1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddRectangleAnnotation(
            0, 0, 0, 10, 10, lineWidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfPoint(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddImageStamp(
            0, 0, 0, 10, 10, PdfImage.FromRgb(1, 1, new byte[] { 0, 0, 0 }),
            icon: (PdfStampIcon)99));
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddFreeText(0, 0, 0, 100, 40, "AA", font,
                alignment: (PdfTextAlignment)3));
        Assert.Throws<ArgumentException>(() =>
            builder.AddFreeText(0, 0, 0, 100, 40, "AA", font,
                intent: PdfFreeTextIntent.Callout));
    }

    [Fact]
    public void VisualAnnotationAuthoring_IsDeterministic()
    {
        static byte[] Build()
        {
            TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
            return new PdfDocumentBuilder()
                .AddBlankPage()
                .AddFreeText(0, 20, 600, 100, 50, "A\nA", font)
                .AddRectangleAnnotation(0, 20, 500, 80, 40, fillColor: PdfRgbColor.Yellow)
                .AddInkAnnotation(0, [new PdfPoint(20, 450), new PdfPoint(80, 470)])
                .Build();
        }

        Assert.Equal(Build(), Build());
    }

    private static PdfDocument Open(PdfDocumentBuilder builder) => PdfDocument.Open(builder.Build());
    private static PdfDictionary Annotation(PdfDocument document)
    {
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        PdfDictionary page = ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        return ResolveDictionary(document, Assert.IsType<PdfArray>(page[Name("Annots")])[0]);
    }
    private static PdfStream Appearance(PdfDocument document, PdfDictionary annotation) =>
        Assert.IsType<PdfStream>(document.Resolve(Assert.IsType<PdfIndirectReference>(
            Assert.IsType<PdfDictionary>(annotation[Name("AP")])[Name("N")])));
    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
