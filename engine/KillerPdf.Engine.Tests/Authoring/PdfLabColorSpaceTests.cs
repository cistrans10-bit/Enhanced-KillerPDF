using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfLabColorSpaceTests
{
    [Fact]
    public void Build_WritesLabResourceAndFillStrokeComponents()
    {
        var lab = new PdfLabColorSpace(
            minimumA: -100, maximumA: 100, minimumB: -110, maximumB: 110);
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillLabColor(lab, 62, 45, -30).Rectangle(0, 0, 40, 40).Fill()
                .SetStrokeLabColor(lab, 35, -20, 60).Rectangle(50, 50, 30, 30).Stroke())
            .Build());
        PdfDictionary page = Page(document);
        PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        PdfArray colorSpace = Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(resources[Name("ColorSpace")])[Name("CS1")]);
        PdfDictionary parameters = Assert.IsType<PdfDictionary>(colorSpace[1]);
        PdfArray white = Assert.IsType<PdfArray>(parameters[Name("WhitePoint")]);
        PdfArray range = Assert.IsType<PdfArray>(parameters[Name("Range")]);
        PdfStream content = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(page[Name("Contents")])));

        Assert.Equal("Lab", Assert.IsType<PdfName>(colorSpace[0]).ValueAsLatin1());
        Assert.Equal([0.9642, 1, 0.8249], white.Select(Number));
        Assert.Equal([-100.0, 100, -110, 110], range.Select(Number));
        Assert.Equal(
            "/CS1 cs\n62 45 -30 scn\n0 0 40 40 re\nf\n/CS1 CS\n35 -20 60 SCN\n50 50 30 30 re\nS\n",
            Encoding.ASCII.GetString(content.EncodedData.Span));
    }

    [Fact]
    public void LabColor_ValidatesSpaceAndComponents()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfLabColorSpace(whiteX: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfLabColorSpace(whiteY: 0.99));
        Assert.Throws<ArgumentException>(() =>
            new PdfLabColorSpace(minimumA: 10, maximumA: 10));
        var lab = new PdfLabColorSpace(minimumA: -80, maximumA: 80);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetFillLabColor(lab, 101, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetStrokeLabColor(lab, 50, -81, 0));
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
