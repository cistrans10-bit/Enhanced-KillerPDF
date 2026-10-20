using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfSpotColorTests
{
    [Fact]
    public void Build_WritesReusableSeparationColorSpaceAndTintFunction()
    {
        var spot = new PdfSpotColor("Killer Orange", new PdfCmykColor(0, 0.72, 1, 0));
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillSpotColor(spot, 0.8).Rectangle(0, 0, 40, 40).Fill()
                .SetStrokeSpotColor(spot, 0.35).Rectangle(50, 50, 30, 30).Stroke())
            .Build());
        PdfDictionary page = Page(document);
        PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        PdfDictionary colorSpaces = Assert.IsType<PdfDictionary>(resources[Name("ColorSpace")]);
        PdfArray separation = Assert.IsType<PdfArray>(colorSpaces[Name("CS1")]);
        PdfDictionary function = Assert.IsType<PdfDictionary>(separation[3]);
        PdfArray alternate = Assert.IsType<PdfArray>(function[Name("C1")]);
        PdfStream content = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(page[Name("Contents")])));

        Assert.Equal("Separation", Assert.IsType<PdfName>(separation[0]).ValueAsLatin1());
        Assert.Equal("Killer Orange", Assert.IsType<PdfName>(separation[1]).ValueAsLatin1());
        Assert.Equal("DeviceCMYK", Assert.IsType<PdfName>(separation[2]).ValueAsLatin1());
        Assert.Equal(2, Assert.IsType<PdfInteger>(function[Name("FunctionType")]).Value);
        Assert.Equal([0.0, 0.72, 1.0, 0.0], alternate.Select(Number));
        Assert.Equal(
            "/CS1 cs\n0.8 scn\n0 0 40 40 re\nf\n/CS1 CS\n0.35 SCN\n50 50 30 30 re\nS\n",
            Encoding.ASCII.GetString(content.EncodedData.Span));
    }

    [Fact]
    public void SpotColor_ValidatesNameAndTint()
    {
        Assert.Throws<ArgumentException>(() =>
            new PdfSpotColor(" ", new PdfCmykColor(0, 0, 0, 0)));
        Assert.Throws<ArgumentException>(() =>
            new PdfSpotColor("bad\uD800ink", new PdfCmykColor(0, 0, 0, 0)));
        var spot = new PdfSpotColor("Ink", new PdfCmykColor(0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetFillSpotColor(spot, -0.01));
        Assert.Throws<ArgumentNullException>(() =>
            new PdfContentStreamBuilder().SetStrokeSpotColor(null!, 1));
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
