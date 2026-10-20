using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Syntax;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfPageTabOrderTests
{
    [Theory]
    [InlineData(PdfPageTabOrder.Row, "R")]
    [InlineData(PdfPageTabOrder.Column, "C")]
    [InlineData(PdfPageTabOrder.Structure, "S")]
    [InlineData(PdfPageTabOrder.AnnotationArray, "A")]
    public void SetPageTabOrder_WritesStandardPageName(
        PdfPageTabOrder tabOrder, string expected)
    {
        PdfDictionary page = FirstPage(PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .SetPageTabOrder(0, tabOrder)
            .Build()));

        Assert.Equal(expected, Assert.IsType<PdfName>(page[Name("Tabs")]).ValueAsLatin1());
    }

    [Fact]
    public void PdfUa2_DefaultsPagesToStructureTabOrderAndRejectsUndefinedValues()
    {
        var content = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .Rectangle(10, 10, 20, 20).Fill()
            .EndMarkedContent();
        PdfDictionary page = FirstPage(PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "Tab order", Language = "en-US" })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Paragraph, 0, 0, 1)
            .Build()));

        Assert.Equal("S", Assert.IsType<PdfName>(page[Name("Tabs")]).ValueAsLatin1());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfDocumentBuilder()
            .AddBlankPage()
            .SetPageTabOrder(0, (PdfPageTabOrder)99));
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "Tab order", Language = "en-US" })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, content)
            .SetPageTabOrder(0, PdfPageTabOrder.Row)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Paragraph, 0, 0, 1)
            .Build());
    }

    [Fact]
    public void Build_EnforcesTabOrderVersionRequirements()
    {
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 4))
            .AddBlankPage().SetPageTabOrder(0, PdfPageTabOrder.Row).Build());
        Assert.NotEmpty(new PdfDocumentBuilder(new PdfVersion(1, 5))
            .AddBlankPage().SetPageTabOrder(0, PdfPageTabOrder.Structure).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(PdfVersion.Pdf17)
            .AddBlankPage().SetPageTabOrder(0, PdfPageTabOrder.AnnotationArray).Build());
    }

    private static PdfDictionary FirstPage(PdfDocument document)
    {
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        return ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
    }

    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));

    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
