using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Fonts;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Tests.Fonts;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfEditorialAnnotationAuthoringTests
{
    [Theory]
    [InlineData(PdfCaretSymbol.None, false)]
    [InlineData(PdfCaretSymbol.Paragraph, true)]
    public void AddCaretAnnotation_WritesSymbolMetadataAndAppearance(
        PdfCaretSymbol symbol, bool expectsSymbol)
    {
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddCaretAnnotation(0, 20, 30, 24, 30, "Insert here",
                symbol: symbol,
                annotationMetadata: new PdfAnnotationMetadata { Author = "Editor" }));
        PdfDictionary annotation = Annotation(document, 0);
        string appearance = Encoding.ASCII.GetString(
            Appearance(document, annotation).EncodedData.Span);

        Assert.Equal("Caret", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(expectsSymbol, annotation.ContainsKey(Name("Sy")));
        Assert.Equal("Editor", DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("T")])));
        Assert.Contains(" l\n", appearance);
    }

    [Fact]
    public void AddRedactionMark_WritesMultipleQuadsUnionBoundsAndReviewAppearance()
    {
        PdfTextQuad[] quads =
        [
            new(new PdfPoint(10, 40), new PdfPoint(110, 40),
                new PdfPoint(10, 25), new PdfPoint(110, 25)),
            new(new PdfPoint(20, 20), new PdfPoint(80, 22),
                new PdfPoint(20, 5), new PdfPoint(80, 7))
        ];
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddRedactionMark(0, quads, "Remove personal data",
                overlayText: "REDACTED", repeatOverlayText: true,
                overlayAlignment: PdfTextAlignment.Right));
        PdfDictionary annotation = Annotation(document, 0);
        PdfStream appearance = Appearance(document, annotation);

        Assert.Equal("Redact", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(16, Assert.IsType<PdfArray>(annotation[Name("QuadPoints")]).Count);
        Assert.Equal([10d, 5d, 110d, 40d],
            Assert.IsType<PdfArray>(annotation[Name("Rect")]).Select(NumberValue));
        Assert.Equal(3, Assert.IsType<PdfArray>(annotation[Name("IC")]).Count);
        Assert.True(Assert.IsType<PdfBoolean>(annotation[Name("Repeat")]).Value);
        Assert.Equal(2, Assert.IsType<PdfInteger>(annotation[Name("Q")]).Value);
        Assert.Equal("REDACTED",
            DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("OverlayText")])));
        Assert.Equal(10, Encoding.ASCII.GetString(appearance.EncodedData.Span)
            .Split(" l\n").Length - 1);
        Assert.Equal(2, Encoding.ASCII.GetString(appearance.EncodedData.Span)
            .Split("BT\n").Length - 1);
    }

    [Fact]
    public void EditorialAnnotationArguments_AreValidated()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddCaretAnnotation(0, 0, 0, 10, 10, symbol: (PdfCaretSymbol)99));
        Assert.Throws<ArgumentException>(() => builder.AddRedactionMark(0, []));
        Assert.Throws<ArgumentException>(() =>
            builder.AddRedactionMark(0, [default]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddRedactionMark(0,
                [new PdfTextQuad(new PdfPoint(0, 10), new PdfPoint(10, 10),
                    new PdfPoint(0, 0), new PdfPoint(10, 0))], opacity: -0.1));
        Assert.Throws<ArgumentException>(() =>
            builder.AddRedactionMark(0,
                [new PdfTextQuad(new PdfPoint(0, 10), new PdfPoint(10, 10),
                    new PdfPoint(0, 0), new PdfPoint(10, 0))], overlayText: "Privé"));
    }

    [Fact]
    public void AddRedactionMark_EmbedsOverlayFontInAppearanceResources()
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        PdfDocument document = Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddRedactionMark(0,
                [new PdfTextQuad(new PdfPoint(0, 20), new PdfPoint(100, 20),
                    new PdfPoint(0, 0), new PdfPoint(100, 0))],
                overlayText: "AA", overlayFont: font));
        PdfStream appearance = Appearance(document, Annotation(document, 0));
        PdfDictionary fonts = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(appearance.Dictionary[Name("Resources")])[Name("Font")]);
        PdfDictionary type0 = ResolveDictionary(document, fonts[Name("FormF1")]);

        Assert.Equal("Type0", Assert.IsType<PdfName>(type0[Name("Subtype")]).ValueAsLatin1());
        Assert.Contains("<00010001> Tj",
            Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    private static PdfDocument Open(PdfDocumentBuilder builder) => PdfDocument.Open(builder.Build());
    private static PdfDictionary Annotation(PdfDocument document, int index)
    {
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        PdfDictionary page = ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        return ResolveDictionary(document, Assert.IsType<PdfArray>(page[Name("Annots")])[index]);
    }
    private static PdfStream Appearance(PdfDocument document, PdfDictionary annotation) =>
        Assert.IsType<PdfStream>(document.Resolve(Assert.IsType<PdfIndirectReference>(
            Assert.IsType<PdfDictionary>(annotation[Name("AP")])[Name("N")])));
    private static double NumberValue(PdfObject value) => value switch
    {
        PdfInteger integer => integer.Value,
        PdfReal real => real.Value,
        _ => throw new InvalidOperationException()
    };
    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
