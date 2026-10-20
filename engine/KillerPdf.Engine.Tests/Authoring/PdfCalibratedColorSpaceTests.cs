using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfCalibratedColorSpaceTests
{
    [Fact]
    public void Build_WritesCalGrayAndCalRgbResources()
    {
        var gray = new PdfCalGrayColorSpace(gamma: 2.2);
        var rgb = new PdfCalRgbColorSpace(
            gamma: [2.2, 2.2, 2.2],
            matrix: [0.4124, 0.3576, 0.1805, 0.2126, 0.7152, 0.0722, 0.0193, 0.1192, 0.9505]);
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillCalibratedColor(gray, 0.4).Rectangle(0, 0, 40, 40).Fill()
                .SetStrokeCalibratedColor(rgb, 0.1, 0.5, 0.9).Rectangle(50, 50, 30, 30).Stroke())
            .Build());
        PdfDictionary page = Page(document);
        PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        PdfDictionary spaces = Assert.IsType<PdfDictionary>(resources[Name("ColorSpace")]);
        PdfArray calGray = Assert.IsType<PdfArray>(spaces[Name("CS1")]);
        PdfArray calRgb = Assert.IsType<PdfArray>(spaces[Name("CS2")]);
        PdfDictionary grayParameters = Assert.IsType<PdfDictionary>(calGray[1]);
        PdfDictionary rgbParameters = Assert.IsType<PdfDictionary>(calRgb[1]);
        PdfStream content = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(page[Name("Contents")]))); 

        Assert.Equal("CalGray", Assert.IsType<PdfName>(calGray[0]).ValueAsLatin1());
        Assert.Equal(2.2, Number(grayParameters[Name("Gamma")]));
        Assert.Equal("CalRGB", Assert.IsType<PdfName>(calRgb[0]).ValueAsLatin1());
        Assert.Equal([2.2, 2.2, 2.2],
            Assert.IsType<PdfArray>(rgbParameters[Name("Gamma")]).Select(Number));
        Assert.Equal(9, Assert.IsType<PdfArray>(rgbParameters[Name("Matrix")]).Count);
        Assert.Equal(
            "/CS1 cs\n0.4 scn\n0 0 40 40 re\nf\n/CS2 CS\n0.1 0.5 0.9 SCN\n50 50 30 30 re\nS\n",
            Encoding.ASCII.GetString(content.EncodedData.Span));
    }

    [Fact]
    public void CalibratedColor_ValidatesDefinitionsAndComponents()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfCalGrayColorSpace(whiteY: 0.99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfCalGrayColorSpace(gamma: 0));
        Assert.Throws<ArgumentException>(() => new PdfCalRgbColorSpace(gamma: [1, 1]));
        Assert.Throws<ArgumentException>(() =>
            new PdfCalRgbColorSpace(matrix: [1, 0, 0, 0, 1, 0, 0, 0]));
        Assert.Throws<ArgumentException>(() =>
            new PdfCalRgbColorSpace(matrix: [1, 0, 0, 0, 1, 0, 0, 0, 0]));
        var rgb = new PdfCalRgbColorSpace();
        Assert.Throws<ArgumentException>(() =>
            new PdfContentStreamBuilder().SetFillCalibratedColor(rgb, 0.1, 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetStrokeCalibratedColor(rgb, 0.1, 1.1, 0.2));
    }

    [Fact]
    public void Build_UsesCalibratedColorAsUncoloredPatternBase()
    {
        var stencil = new PdfTilingPattern(6, 6,
            new PdfContentStreamBuilder().Rectangle(1, 1, 4, 4).Fill(),
            paintType: PdfTilingPatternPaintType.Uncolored);
        var gray = new PdfCalGrayColorSpace(gamma: 1.8);

        Assert.Equal("[/Pattern /CS1] cs\n0.35 /P1 scn\n",
            Encoding.ASCII.GetString(new PdfContentStreamBuilder()
                .SetFillPattern(stencil, gray, 0.35).Build()));
    }

    private static double Number(PdfObject value) => value switch
    {
        PdfInteger integer => integer.Value,
        PdfReal real => real.Value,
        _ => throw new Xunit.Sdk.XunitException("Expected number")
    };

    private static PdfDictionary Page(PdfDocument document)
    {
        PdfDictionary catalog = Resolve(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = Resolve(document, catalog[Name("Pages")]);
        return Resolve(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
    }

    private static PdfDictionary Resolve(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));

    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
