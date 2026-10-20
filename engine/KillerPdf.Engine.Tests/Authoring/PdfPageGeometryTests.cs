using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfPageGeometryTests
{
    [Fact]
    public void Build_WritesRotationUserUnitAndProductionBoxes()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage(600, 800)
            .SetPageRotation(0, 90)
            .SetPageUserUnit(0, 2.5)
            .SetPageBox(0, PdfPageBox.Crop, 20, 30, 560, 740)
            .SetPageBox(0, PdfPageBox.Bleed, 15, 25, 570, 750)
            .SetPageBox(0, PdfPageBox.Trim, 25, 35, 550, 730)
            .SetPageBox(0, PdfPageBox.Art, 40, 50, 520, 700)
            .Build());
        PdfDictionary page = FirstPage(document);

        Assert.Equal(90, Assert.IsType<PdfInteger>(page[Name("Rotate")]).Value);
        Assert.Equal(2.5, Assert.IsType<PdfReal>(page[Name("UserUnit")]).Value);
        AssertBox(page, "CropBox", 20, 30, 580, 770);
        AssertBox(page, "BleedBox", 15, 25, 585, 775);
        AssertBox(page, "TrimBox", 25, 35, 575, 765);
        AssertBox(page, "ArtBox", 40, 50, 560, 750);
    }

    [Fact]
    public void PageGeometry_RejectsInvalidValuesAndOutOfBoundsBoxes()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage(100, 200);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetPageRotation(0, 45));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetPageUserUnit(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetPageUserUnit(0, 75_001));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.SetPageBox(0, PdfPageBox.Crop, -1, 0, 50, 50));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.SetPageBox(0, PdfPageBox.Trim, 60, 0, 50, 50));
    }

    [Fact]
    public void PageGeometry_OmitsDefaultRotationAndUserUnit()
    {
        PdfDictionary page = FirstPage(PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage().SetPageRotation(0, 0).SetPageUserUnit(0, 1).Build()));

        Assert.False(page.ContainsKey(Name("Rotate")));
        Assert.False(page.ContainsKey(Name("UserUnit")));
    }

    private static void AssertBox(
        PdfDictionary page, string name, params double[] expected)
    {
        PdfArray box = Assert.IsType<PdfArray>(page[Name(name)]);
        Assert.Equal(expected, box.Select(value => value switch
        {
            PdfInteger integer => (double)integer.Value,
            PdfReal real => real.Value,
            _ => throw new Xunit.Sdk.XunitException("Expected a numeric page-box coordinate.")
        }));
    }

    private static PdfDictionary FirstPage(PdfDocument document)
    {
        PdfDictionary catalog = Resolve(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = Resolve(document, catalog[Name("Pages")]);
        return Resolve(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
    }

    private static PdfDictionary Resolve(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
