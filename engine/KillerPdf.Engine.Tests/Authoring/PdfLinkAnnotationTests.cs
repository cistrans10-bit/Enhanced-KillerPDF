using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfLinkAnnotationTests
{
    [Fact]
    public void AddUriLink_WritesUriActionAndInvisibleBorder()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriLink(0, 10, 20, 100, 30, "https://killerpdf.net/docs?q=2",
                annotationMetadata: new PdfAnnotationMetadata
                {
                    Author = "KillerPDF",
                    Subject = "Documentation",
                    Flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.Locked
                }, contents: "Open documentation")
            .Build());
        PdfDictionary annotation = FirstAnnotation(document);
        var action = Assert.IsType<PdfDictionary>(annotation[Name("A")]);
        var rectangle = Assert.IsType<PdfArray>(annotation[Name("Rect")]);
        var border = Assert.IsType<PdfArray>(annotation[Name("Border")]);

        Assert.Equal("Link", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(132, Assert.IsType<PdfInteger>(annotation[Name("F")]).Value);
        Assert.Equal("Open documentation",
            DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("Contents")])));
        Assert.Equal("KillerPDF",
            DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("T")])));
        Assert.True(annotation.ContainsKey(Name("P")));
        Assert.Equal("URI", Assert.IsType<PdfName>(action[Name("S")]).ValueAsLatin1());
        Assert.Equal("https://killerpdf.net/docs?q=2",
            Encoding.UTF8.GetString(Assert.IsType<PdfString>(action[Name("URI")]).Bytes.Span));
        Assert.Equal([10, 20, 110, 50],
            rectangle.Select(value => Assert.IsType<PdfInteger>(value).Value));
        Assert.All(border, value => Assert.Equal(0, Assert.IsType<PdfInteger>(value).Value));
    }

    [Fact]
    public void AddPageLink_UsesTargetPageReferenceAndFitDestination()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddBlankPage()
            .AddPageLink(0, 0, 0, 50, 50, 1)
            .Build());
        PdfDictionary annotation = FirstAnnotation(document);
        var destination = Assert.IsType<PdfArray>(annotation[Name("Dest")]);
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var pages = ResolveDictionary(document, catalog[Name("Pages")]);
        var target = Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfArray>(pages[Name("Kids")])[1]);

        var actualTarget = Assert.IsType<PdfIndirectReference>(destination[0]);
        Assert.Equal(target.ObjectNumber, actualTarget.ObjectNumber);
        Assert.Equal(target.Generation, actualTarget.Generation);
        Assert.Equal("Fit", Assert.IsType<PdfName>(destination[1]).ValueAsLatin1());
    }

    [Fact]
    public void AddPageLink_WritesPreciseDestinationView()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddBlankPage()
            .AddPageLink(0, 0, 0, 50, 50, 1,
                destination: PdfDestination.FitRectangle(40, 50, 500, 700))
            .Build());
        PdfArray destination = Assert.IsType<PdfArray>(
            FirstAnnotation(document)[Name("Dest")]);

        Assert.Equal("FitR", Assert.IsType<PdfName>(destination[1]).ValueAsLatin1());
        Assert.Equal([40L, 50L, 500L, 700L], destination.Skip(2)
            .Select(value => Assert.IsType<PdfInteger>(value).Value));
    }

    [Fact]
    public void AddUriLink_WithMultipleQuads_WritesTightBoundsAndHitGeometry()
    {
        PdfTextQuad[] quads =
        [
            new(new PdfPoint(10, 40), new PdfPoint(110, 40),
                new PdfPoint(10, 25), new PdfPoint(110, 25)),
            new(new PdfPoint(20, 20), new PdfPoint(80, 22),
                new PdfPoint(20, 5), new PdfPoint(80, 7))
        ];
        PdfDictionary annotation = FirstAnnotation(PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriLink(0, quads, "https://killerpdf.net")
            .Build()));

        Assert.Equal(16, Assert.IsType<PdfArray>(annotation[Name("QuadPoints")]).Count);
        Assert.Equal([10d, 5d, 110d, 40d],
            Assert.IsType<PdfArray>(annotation[Name("Rect")]).Select(NumberValue));
    }

    [Fact]
    public void LinkQuadOverloads_RejectEmptyGeometry()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder()
            .AddBlankPage().AddUriLink(0, [], "https://killerpdf.net"));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder()
            .AddBlankPage().AddUriLink(
                0, [default], "https://killerpdf.net"));
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/secret.txt")]
    public void AddUriLink_RejectsUnsafeOrRelativeSchemes(string uri)
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder()
            .AddBlankPage().AddUriLink(0, 0, 0, 10, 10, uri));
    }

    [Fact]
    public void AddUriLink_RejectsUnpairedSurrogate()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder()
            .AddBlankPage().AddUriLink(
                0, 0, 0, 10, 10, "https://killerpdf.net/bad\uD800path"));
    }

    [Fact]
    public void PdfUa2_WritesLinkObjectReferenceAndParentTreeAssociation()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible link",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddBlankPage()
            .AddUriLink(0, 10, 10, 80, 20, "https://killerpdf.net",
                contents: "Open KillerPDF")
            .AddStructureContainer(PdfStructureType.Document)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary annotation = FirstAnnotation(document);
        long key = Assert.IsType<PdfInteger>(annotation[Name("StructParent")]).Value;
        PdfDictionary structureRoot = ResolveDictionary(document, catalog[Name("StructTreeRoot")]);
        PdfDictionary parentTree = ResolveDictionary(document, structureRoot[Name("ParentTree")]);
        PdfArray numbers = Assert.IsType<PdfArray>(parentTree[Name("Nums")]);
        PdfIndirectReference linkReference = Assert.IsType<PdfIndirectReference>(numbers[1]);
        PdfDictionary link = ResolveDictionary(document, linkReference);
        PdfDictionary objectReference = Assert.IsType<PdfDictionary>(link[Name("K")]);

        Assert.Equal(key, Assert.IsType<PdfInteger>(numbers[0]).Value);
        Assert.Equal("Link", Assert.IsType<PdfName>(link[Name("S")]).ValueAsLatin1());
        Assert.Equal("OBJR", Assert.IsType<PdfName>(objectReference[Name("Type")]).ValueAsLatin1());
        Assert.Equal(Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfArray>(FirstPage(document)[Name("Annots")])[0]).ObjectNumber,
            Assert.IsType<PdfIndirectReference>(objectReference[Name("Obj")]).ObjectNumber);

        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "Link", Language = "en-US" })
            .EnablePdfUa2Conformance()
            .AddBlankPage()
            .AddUriLink(0, 0, 0, 10, 10, "https://killerpdf.net")
            .AddStructureContainer(PdfStructureType.Document)
            .Build());
    }

    [Fact]
    public void PdfUa2_DirectPageLinkUsesStructureDestination()
    {
        var firstContent = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .EndMarkedContent();
        var secondContent = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .EndMarkedContent();
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible page link",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(612, 792, firstContent)
            .AddPage(612, 792, secondContent)
            .AddPageLink(0, 10, 10, 80, 20, 1,
                contents: "Open the second page")
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Paragraph, 0, 0, 1)
            .AddStructureElement(PdfStructureType.Paragraph, 1, 0, 1)
            .Build());
        PdfDictionary annotation = FirstAnnotation(document);
        PdfArray destination = Assert.IsType<PdfArray>(annotation[Name("Dest")]);
        PdfDictionary target = ResolveDictionary(document, destination[0]);

        Assert.Equal("StructElem",
            Assert.IsType<PdfName>(target[Name("Type")]).ValueAsLatin1());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        int secondPageNumber = Assert.IsType<PdfIndirectReference>(
            Assert.IsType<PdfArray>(pages[Name("Kids")])[1]).ObjectNumber;
        Assert.Equal(secondPageNumber,
            Assert.IsType<PdfIndirectReference>(target[Name("Pg")]).ObjectNumber);
    }

    private static PdfDictionary FirstAnnotation(PdfDocument document)
    {
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var pages = ResolveDictionary(document, catalog[Name("Pages")]);
        var page = ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        return ResolveDictionary(document, Assert.IsType<PdfArray>(page[Name("Annots")])[0]);
    }

    private static PdfDictionary FirstPage(PdfDocument document)
    {
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        return ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
    }

    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static double NumberValue(PdfObject value) => value switch
    {
        PdfInteger integer => integer.Value,
        PdfReal real => real.Value,
        _ => throw new InvalidOperationException()
    };
    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
