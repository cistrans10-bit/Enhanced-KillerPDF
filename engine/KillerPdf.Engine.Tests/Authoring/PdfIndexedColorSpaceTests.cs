using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfIndexedColorSpaceTests
{
    [Fact]
    public void Build_WritesRgbPaletteAndIndexedPainting()
    {
        var palette = new PdfIndexedColorSpace(PdfIndexedBaseColorSpace.Rgb, new byte[]
        {
            255, 60, 40,
            30, 120, 230,
            245, 210, 50
        });
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillIndexedColor(palette, 2).Rectangle(0, 0, 40, 40).Fill()
                .SetStrokeIndexedColor(palette, 1).Rectangle(50, 50, 30, 30).Stroke())
            .Build());
        PdfDictionary page = Page(document);
        PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        PdfArray indexed = Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(resources[Name("ColorSpace")])[Name("CS1")]);
        PdfString lookup = Assert.IsType<PdfString>(indexed[3]);
        PdfStream content = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(page[Name("Contents")])));

        Assert.Equal("Indexed", Assert.IsType<PdfName>(indexed[0]).ValueAsLatin1());
        Assert.Equal("DeviceRGB", Assert.IsType<PdfName>(indexed[1]).ValueAsLatin1());
        Assert.Equal(2, Assert.IsType<PdfInteger>(indexed[2]).Value);
        Assert.Equal(palette.Palette.ToArray(), lookup.Bytes.ToArray());
        Assert.Equal(
            "/CS1 cs\n2 scn\n0 0 40 40 re\nf\n/CS1 CS\n1 SCN\n50 50 30 30 re\nS\n",
            Encoding.ASCII.GetString(content.EncodedData.Span));
    }

    [Fact]
    public void IndexedColor_ValidatesPaletteAndIndex()
    {
        Assert.Throws<ArgumentException>(() =>
            new PdfIndexedColorSpace(PdfIndexedBaseColorSpace.Rgb, new byte[] { 1, 2 }));
        Assert.Throws<ArgumentException>(() =>
            new PdfIndexedColorSpace(PdfIndexedBaseColorSpace.Gray, new byte[257]));
        var palette = new PdfIndexedColorSpace(
            PdfIndexedBaseColorSpace.Cmyk, new byte[] { 0, 0, 0, 0, 0, 0, 0, 255 });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetFillIndexedColor(palette, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetStrokeIndexedColor(palette, -1));
    }

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
