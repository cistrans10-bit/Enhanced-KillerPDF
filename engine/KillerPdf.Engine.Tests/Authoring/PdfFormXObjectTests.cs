using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfFormXObjectTests
{
    [Fact]
    public void Build_ReusesOneFormObjectAcrossPagesAndPlacements()
    {
        var form = new PdfFormXObject(20, 10, new PdfContentStreamBuilder()
            .SetFillRgb(0.2, 0.4, 0.8).Rectangle(0, 0, 20, 10).Fill());
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(200, 100, new PdfContentStreamBuilder()
                .DrawForm(form, 10, 20)
                .DrawForm(form, 50, 20, 40, 20))
            .AddPage(200, 100, new PdfContentStreamBuilder().DrawForm(form, 5, 6))
            .Build());
        PdfDictionary[] pages = Pages(document);
        PdfIndirectReference first = FormReference(pages[0], "Fm1");
        PdfIndirectReference second = FormReference(pages[1], "Fm1");
        PdfStream formStream = Assert.IsType<PdfStream>(document.Resolve(first));
        PdfStream pageStream = ResolveStream(document, pages[0][Name("Contents")]);

        Assert.Equal(first.ObjectNumber, second.ObjectNumber);
        Assert.Equal("0.2 0.4 0.8 rg\n0 0 20 10 re\nf\n",
            Encoding.ASCII.GetString(formStream.EncodedData.Span));
        Assert.Equal(
            "q\n1 0 0 1 10 20 cm\n/Fm1 Do\nQ\nq\n2 0 0 2 50 20 cm\n/Fm1 Do\nQ\n",
            Encoding.ASCII.GetString(pageStream.EncodedData.Span));
    }

    [Fact]
    public void Build_WritesNestedFormsWithScopedResourcesAndTransparencyGroup()
    {
        var gradient = new PdfAxialGradient(0, 0, 20, 0, [
            new PdfGradientStop(0, new PdfRgbColor(1, 0, 0)),
            new PdfGradientStop(1, new PdfRgbColor(0, 0, 1))]);
        var inner = new PdfFormXObject(20, 10, new PdfContentStreamBuilder()
            .SetOpacity(0.75)
            .Rectangle(0, 0, 20, 10).Clip()
            .PaintShading(gradient), isolatedTransparencyGroup: true);
        var outer = new PdfFormXObject(40, 20,
            new PdfContentStreamBuilder().DrawForm(inner, 10, 5));
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder().DrawForm(outer, 0, 0))
            .Build());

        PdfStream outerStream = ResolveStream(document, FormReference(Pages(document)[0], "Fm1"));
        PdfDictionary outerResources = Assert.IsType<PdfDictionary>(outerStream.Dictionary[Name("Resources")]);
        PdfDictionary outerXObjects = Assert.IsType<PdfDictionary>(outerResources[Name("XObject")]);
        PdfStream innerStream = ResolveStream(document, outerXObjects[Name("Fm1")]);
        PdfDictionary innerResources = Assert.IsType<PdfDictionary>(innerStream.Dictionary[Name("Resources")]);
        PdfDictionary group = Assert.IsType<PdfDictionary>(innerStream.Dictionary[Name("Group")]);

        Assert.True(innerResources.ContainsKey(Name("ExtGState")));
        Assert.True(innerResources.ContainsKey(Name("Shading")));
        Assert.Equal("Transparency", Assert.IsType<PdfName>(group[Name("S")]).ValueAsLatin1());
        Assert.True(Assert.IsType<PdfBoolean>(group[Name("I")]).Value);
        Assert.False(Assert.IsType<PdfBoolean>(group[Name("K")]).Value);
    }

    [Fact]
    public void PdfUa2Mode_RequiresFormPlacementToBeTagged()
    {
        var form = new PdfFormXObject(10, 10,
            new PdfContentStreamBuilder().Rectangle(0, 0, 10, 10).Fill());
        static PdfDocumentBuilder Ready(PdfContentStreamBuilder content) => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "Form", Language = "en-US" })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document);

        Assert.Throws<InvalidOperationException>(() =>
            Ready(new PdfContentStreamBuilder().DrawForm(form, 0, 0)).Build());

        byte[] tagged = Ready(new PdfContentStreamBuilder()
                .BeginMarkedContent(PdfStructureType.Figure, 0)
                .DrawForm(form, 0, 0)
                .EndMarkedContent())
            .AddStructureElement(PdfStructureType.Figure, 0, 0, 1,
                alternateDescription: "A reusable vector figure")
            .Build();
        Assert.NotEmpty(tagged);
    }

    [Fact]
    public void Form_RejectsInvalidDimensionsAndPageTaggedContent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfFormXObject(0, 10, new PdfContentStreamBuilder()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfFormXObject(10, double.NaN, new PdfContentStreamBuilder()));
        var tagged = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Figure, 0)
            .Rectangle(0, 0, 1, 1).Fill()
            .EndMarkedContent();
        Assert.Throws<ArgumentException>(() => new PdfFormXObject(10, 10, tagged));
    }

    private static PdfIndirectReference FormReference(PdfDictionary page, string name)
    {
        PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        PdfDictionary xObjects = Assert.IsType<PdfDictionary>(resources[Name("XObject")]);
        return Assert.IsType<PdfIndirectReference>(xObjects[Name(name)]);
    }

    private static PdfDictionary[] Pages(PdfDocument document)
    {
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        return [.. Assert.IsType<PdfArray>(pages[Name("Kids")]).Select(value => ResolveDictionary(document, value))];
    }

    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfStream ResolveStream(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfStream>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
