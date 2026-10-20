using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfStructureTests
{
    [Fact]
    public void Build_WritesNestedStructureTreeAndParentTreeMappings()
    {
        var content = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .BeginText().SetFont(PdfStandardFont.Helvetica, 12)
            .ShowLatin1Text("KillerPDF").EndText()
            .EndMarkedContent()
            .BeginMarkedContent(PdfStructureType.Figure, 2)
            .Rectangle(10, 10, 20, 20).Fill()
            .EndMarkedContent();
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Language = "en-US" })
            .AddPage(200, 300, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Paragraph, 0, 0, 1,
                actualText: "KillerPDF")
            .AddStructureElement(PdfStructureType.Figure, 0, 2, 1,
                alternateDescription: "A square")
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary markInfo = Assert.IsType<PdfDictionary>(catalog[Name("MarkInfo")]);
        PdfDictionary structureRoot = ResolveDictionary(document, catalog[Name("StructTreeRoot")]);
        PdfDictionary parentTree = ResolveDictionary(document, structureRoot[Name("ParentTree")]);
        PdfArray topLevel = Assert.IsType<PdfArray>(structureRoot[Name("K")]);
        PdfDictionary documentElement = ResolveDictionary(document, topLevel[0]);
        PdfArray documentKids = Assert.IsType<PdfArray>(documentElement[Name("K")]);
        PdfIndirectReference paragraphReference = Assert.IsType<PdfIndirectReference>(documentKids[0]);
        PdfIndirectReference figureReference = Assert.IsType<PdfIndirectReference>(documentKids[1]);
        PdfDictionary paragraph = ResolveDictionary(document, paragraphReference);
        PdfDictionary figure = ResolveDictionary(document, figureReference);
        PdfArray parentNumbers = Assert.IsType<PdfArray>(parentTree[Name("Nums")]);
        PdfArray mappings = Assert.IsType<PdfArray>(parentNumbers[1]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        PdfDictionary page = ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);

        Assert.True(Assert.IsType<PdfBoolean>(markInfo[Name("Marked")]).Value);
        Assert.Equal("Document", Assert.IsType<PdfName>(
            documentElement[Name("S")]).ValueAsLatin1());
        Assert.Equal(0, Assert.IsType<PdfInteger>(paragraph[Name("K")]).Value);
        Assert.Equal(2, Assert.IsType<PdfInteger>(figure[Name("K")]).Value);
        Assert.Equal("KillerPDF", DecodeUnicode(
            Assert.IsType<PdfString>(paragraph[Name("ActualText")])));
        Assert.Equal("A square", DecodeUnicode(
            Assert.IsType<PdfString>(figure[Name("Alt")])));
        Assert.Equal(0, Assert.IsType<PdfInteger>(page[Name("StructParents")]).Value);
        Assert.Equal(0, Assert.IsType<PdfInteger>(parentNumbers[0]).Value);
        Assert.Equal(3, mappings.Count);
        Assert.Equal(paragraphReference.ObjectNumber,
            Assert.IsType<PdfIndirectReference>(mappings[0]).ObjectNumber);
        Assert.IsType<PdfNull>(mappings[1]);
        Assert.Equal(figureReference.ObjectNumber,
            Assert.IsType<PdfIndirectReference>(mappings[2]).ObjectNumber);
        Assert.Equal(1, Assert.IsType<PdfInteger>(
            structureRoot[Name("ParentTreeNextKey")]).Value);
    }

    [Fact]
    public void Build_RejectsUnregisteredOrUnknownMarkedContent()
    {
        var content = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 4)
            .EndMarkedContent();
        var builder = new PdfDocumentBuilder().AddPage(100, 100, content);

        Assert.Throws<ArgumentException>(() => builder.AddStructureElement(
            PdfStructureType.Paragraph, 0, 3));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_AssignsIndependentParentTreeKeysAcrossPages()
    {
        static PdfContentStreamBuilder FirstPage() => new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .Rectangle(0, 0, 10, 10).Fill().EndMarkedContent();
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, FirstPage())
            .AddPage(100, 100, FirstPage())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Paragraph, 0, 0, 1)
            .AddStructureElement(PdfStructureType.Paragraph, 1, 0, 1)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        PdfDictionary[] pageDictionaries = [.. Assert.IsType<PdfArray>(pages[Name("Kids")]).Select(value => ResolveDictionary(document, value))];
        PdfDictionary root = ResolveDictionary(document, catalog[Name("StructTreeRoot")]);
        PdfDictionary parentTree = ResolveDictionary(document, root[Name("ParentTree")]);
        PdfArray numbers = Assert.IsType<PdfArray>(parentTree[Name("Nums")]);

        Assert.Equal([0L, 1L], pageDictionaries.Select(page =>
            Assert.IsType<PdfInteger>(page[Name("StructParents")]).Value));
        Assert.Equal([0L, 1L], new[] { numbers[0], numbers[2] }
            .Select(value => Assert.IsType<PdfInteger>(value).Value));
        Assert.All([numbers[1], numbers[3]], value =>
            Assert.Single(Assert.IsType<PdfArray>(value)));
    }

    [Fact]
    public void PdfUa2Mode_WritesIdentificationNamespaceAndViewerPreferences()
    {
        var content = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Figure, 0)
            .Rectangle(10, 10, 20, 20).Fill()
            .EndMarkedContent()
            .BeginMarkedContent(PdfStructureType.Note, 1)
            .EndMarkedContent();
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible document",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Figure, 0, 0, 1,
                alternateDescription: "A square")
            .AddStructureElement(PdfStructureType.Note, 0, 1, 1,
                actualText: "A footnote")
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary viewerPreferences = Assert.IsType<PdfDictionary>(
            catalog[Name("ViewerPreferences")]);
        PdfDictionary root = ResolveDictionary(document, catalog[Name("StructTreeRoot")]);
        PdfDictionary documentElement = ResolveDictionary(document,
            Assert.IsType<PdfArray>(root[Name("K")])[0]);
        PdfIndirectReference namespaceReference = Assert.IsType<PdfIndirectReference>(
            documentElement[Name("NS")]);
        PdfDictionary note = ResolveDictionary(document,
            Assert.IsType<PdfArray>(documentElement[Name("K")])[1]);
        PdfDictionary structureNamespace = ResolveDictionary(document, namespaceReference);
        PdfStream metadata = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Metadata")])));
        string xmp = Encoding.UTF8.GetString(metadata.EncodedData.Span);

        Assert.True(Assert.IsType<PdfBoolean>(
            viewerPreferences[Name("DisplayDocTitle")]).Value);
        Assert.Equal("http://iso.org/pdf2/ssn", Encoding.Latin1.GetString(
            Assert.IsType<PdfString>(structureNamespace[Name("NS")]).Bytes.Span));
        Assert.Equal("FENote", Assert.IsType<PdfName>(note[Name("S")]).ValueAsLatin1());
        Assert.Contains("pdfuaid:part", xmp, StringComparison.Ordinal);
        Assert.Contains(">2<", xmp, StringComparison.Ordinal);
        Assert.Contains("pdfuaid:rev", xmp, StringComparison.Ordinal);
        Assert.Contains(">2024<", xmp, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfUa2Mode_WritesArticleInPdf17Namespace()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible article",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(PdfStructureType.Article, 1)
            .AddStructureContainer(PdfStructureType.Paragraph, 2)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary root = ResolveDictionary(document, catalog[Name("StructTreeRoot")]);
        PdfDictionary documentElement = ResolveDictionary(document,
            Assert.IsType<PdfArray>(root[Name("K")])[0]);
        PdfDictionary article = ResolveDictionary(document, documentElement[Name("K")]);
        PdfDictionary articleNamespace = ResolveDictionary(document, article[Name("NS")]);

        Assert.Equal("Art", Assert.IsType<PdfName>(article[Name("S")]).ValueAsLatin1());
        Assert.Equal("http://iso.org/pdf/ssn", Encoding.Latin1.GetString(
            Assert.IsType<PdfString>(articleNamespace[Name("NS")]).Bytes.Span));
    }

    [Fact]
    public void PdfUa2Mode_RejectsIncompleteAccessibilityClaims()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PdfDocumentBuilder().EnablePdfUa2Conformance().Build());

        var untagged = new PdfContentStreamBuilder().Rectangle(0, 0, 10, 10).Fill();
        var builder = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible document",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, untagged)
            .AddStructureContainer(PdfStructureType.Document);
        Assert.Throws<InvalidOperationException>(() => builder.Build());

        var text = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .BeginText().SetFont(PdfStandardFont.Helvetica, 12)
            .ShowLatin1Text("Not embedded").EndText().EndMarkedContent();
        var legacyFont = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible document",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, text)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Paragraph, 0, 0, 1);
        Assert.Throws<InvalidOperationException>(() => legacyFont.Build());

        var formulaContent = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Formula, 0)
            .EndMarkedContent();
        var undescribedFormula = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible formula",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, formulaContent)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Formula, 0, 0, 1);
        Assert.Throws<InvalidOperationException>(() => undescribedFormula.Build());

        var quoteContent = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Quote, 0)
            .EndMarkedContent();
        var misplacedQuote = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Misplaced quote",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, quoteContent)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Quote, 0, 0, 1,
                actualText: "Quoted text");
        Assert.Throws<InvalidOperationException>(() => misplacedQuote.Build());

        var cellContent = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.TableDataCell, 0)
            .EndMarkedContent();
        var misplacedCell = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Misplaced table cell",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, cellContent)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.TableDataCell, 0, 0, 1,
                actualText: "Cell");
        Assert.Throws<InvalidOperationException>(() => misplacedCell.Build());
    }

    [Fact]
    public void Build_WritesStandardFormulaStructureType()
    {
        var content = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Formula, 0)
            .EndMarkedContent();
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Formula, 0, 0, 1,
                alternateDescription: "x squared plus y squared")
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary root = ResolveDictionary(document, catalog[Name("StructTreeRoot")]);
        PdfDictionary documentElement = ResolveDictionary(document,
            Assert.IsType<PdfArray>(root[Name("K")])[0]);
        PdfDictionary formula = ResolveDictionary(document, documentElement[Name("K")]);

        Assert.Equal("Formula", Assert.IsType<PdfName>(formula[Name("S")]).ValueAsLatin1());
    }

    [Fact]
    public void Build_PdfUaRejectsGenericHeadingButTaggedPdfAllowsIt()
    {
        var content = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Heading, 0)
            .EndMarkedContent();

        byte[] tagged = new PdfDocumentBuilder()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Heading, 0, 0, 1)
            .Build();
        Assert.NotEmpty(tagged);

        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Generic heading",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(PdfStructureType.Heading, 0, 0, 1)
            .Build());
    }

    [Theory]
    [InlineData(PdfStructureType.Document)]
    [InlineData(PdfStructureType.Article)]
    [InlineData(PdfStructureType.Section)]
    [InlineData(PdfStructureType.List)]
    [InlineData(PdfStructureType.ListItem)]
    [InlineData(PdfStructureType.Table)]
    [InlineData(PdfStructureType.TableRow)]
    public void Build_PdfUaRejectsMarkedContentDirectlyInStructuralContainers(
        PdfStructureType type)
    {
        var content = new PdfContentStreamBuilder()
            .BeginMarkedContent(type, 0)
            .EndMarkedContent();

        byte[] tagged = new PdfDocumentBuilder()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(type, 0, 0, 1)
            .Build();
        Assert.NotEmpty(tagged);

        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Invalid structure container content",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureElement(type, 0, 0, 1)
            .Build());
    }

    [Theory]
    [InlineData(PdfStructureType.Heading1, PdfStructureType.List)]
    [InlineData(PdfStructureType.Span, PdfStructureType.Table)]
    [InlineData(PdfStructureType.ListItem, PdfStructureType.List)]
    [InlineData(PdfStructureType.TableRow, PdfStructureType.Table)]
    public void Build_PdfUaRejectsListAndTableUnderRestrictedParents(
        PdfStructureType parent, PdfStructureType child)
    {
        byte[] tagged = new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(parent, 1)
            .AddStructureContainer(child, 2)
            .Build();
        Assert.NotEmpty(tagged);

        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Invalid nested structure container",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(parent, 1)
            .AddStructureContainer(child, 2)
            .Build());
    }

    [Fact]
    public void Build_PdfUaRejectsIrregularTableRowsButTaggedPdfAllowsThem()
    {
        static PdfDocumentBuilder CreateBuilder() => new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(PdfStructureType.Table, 1)
            .AddStructureContainer(PdfStructureType.TableRow, 2)
            .AddStructureContainer(PdfStructureType.TableHeaderCell, 3)
            .AddStructureContainer(PdfStructureType.TableHeaderCell, 3)
            .AddStructureContainer(PdfStructureType.TableRow, 2)
            .AddStructureContainer(PdfStructureType.TableDataCell, 3);

        Assert.NotEmpty(CreateBuilder().Build());
        Assert.Throws<InvalidOperationException>(() => CreateBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Irregular table",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .Build());
    }

    [Theory]
    [InlineData(PdfStructureType.Paragraph)]
    [InlineData(PdfStructureType.Heading2)]
    [InlineData(PdfStructureType.List)]
    [InlineData(PdfStructureType.Table)]
    public void Build_PdfUaRejectsBlockChildrenUnderNumberedHeadings(
        PdfStructureType child)
    {
        PdfDocumentBuilder CreateBuilder() => new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(PdfStructureType.Heading1, 1)
            .AddStructureContainer(child, 2);

        Assert.NotEmpty(CreateBuilder().Build());
        Assert.Throws<InvalidOperationException>(() => CreateBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Invalid heading child",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .Build());
    }

    [Fact]
    public void Build_PdfUaAllowsOneSectionUnderNumberedHeadingButRejectsTwo()
    {
        static PdfDocumentBuilder CreateBuilder(bool secondSection) => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Heading section",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(PdfStructureType.Heading1, 1)
            .AddStructureContainer(PdfStructureType.Section, 2)
            .AddStructureContainer(secondSection
                ? PdfStructureType.Section : PdfStructureType.Span, 2);

        Assert.NotEmpty(CreateBuilder(false).Build());
        Assert.Throws<InvalidOperationException>(() => CreateBuilder(true).Build());
    }

    [Fact]
    public void Build_PdfUaRejectsNestedDocumentButTaggedPdfAllowsIt()
    {
        static PdfDocumentBuilder CreateBuilder() => new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(PdfStructureType.Section, 1)
            .AddStructureContainer(PdfStructureType.Document, 2);

        Assert.NotEmpty(CreateBuilder().Build());
        Assert.Throws<InvalidOperationException>(() => CreateBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Nested document",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .Build());
    }

    [Theory]
    [InlineData(PdfStructureType.Paragraph)]
    [InlineData(PdfStructureType.Span)]
    [InlineData(PdfStructureType.List)]
    [InlineData(PdfStructureType.Form)]
    [InlineData(PdfStructureType.Note)]
    public void Build_PdfUaRejectsNumberedHeadingsUnderRestrictedParents(
        PdfStructureType parent)
    {
        PdfDocumentBuilder CreateBuilder() => new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(parent, 1)
            .AddStructureContainer(PdfStructureType.Heading2, 2);

        Assert.NotEmpty(CreateBuilder().Build());
        Assert.Throws<InvalidOperationException>(() => CreateBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Invalid heading parent",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .Build());
    }

    [Theory]
    [InlineData(PdfStructureType.Document)]
    [InlineData(PdfStructureType.Part)]
    [InlineData(PdfStructureType.Article)]
    [InlineData(PdfStructureType.Section)]
    [InlineData(PdfStructureType.Division)]
    public void Build_PdfUaRejectsSpanDirectlyUnderHighLevelGroupingRoles(
        PdfStructureType parent)
    {
        PdfDocumentBuilder CreateBuilder()
        {
            var builder = new PdfDocumentBuilder()
                .AddPage(100, 100, new PdfContentStreamBuilder())
                .AddStructureContainer(PdfStructureType.Document);
            if (parent == PdfStructureType.Document)
                return builder.AddStructureContainer(PdfStructureType.Span, 1);
            return builder.AddStructureContainer(parent, 1)
                .AddStructureContainer(PdfStructureType.Span, 2);
        }

        Assert.NotEmpty(CreateBuilder().Build());
        Assert.Throws<InvalidOperationException>(() => CreateBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Invalid grouping span",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .Build());
    }

    [Fact]
    public void Build_PdfUaAllowsSpanUnderParagraph()
    {
        byte[] pdf = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Inline span",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, new PdfContentStreamBuilder())
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(PdfStructureType.Paragraph, 1)
            .AddStructureContainer(PdfStructureType.Span, 2)
            .Build();

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public void Build_WritesListNumberingAndPdfUaRequiresItForLabels()
    {
        var content = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Label, 0)
            .EndMarkedContent()
            .BeginMarkedContent(PdfStructureType.ListBody, 1)
            .EndMarkedContent();
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(PdfStructureType.List, 1,
                listNumbering: PdfListNumbering.Decimal)
            .AddStructureContainer(PdfStructureType.ListItem, 2)
            .AddStructureElement(PdfStructureType.Label, 0, 0, 3)
            .AddStructureElement(PdfStructureType.ListBody, 0, 1, 3)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary root = ResolveDictionary(document, catalog[Name("StructTreeRoot")]);
        PdfDictionary documentElement = ResolveDictionary(document,
            Assert.IsType<PdfArray>(root[Name("K")])[0]);
        PdfDictionary list = ResolveDictionary(document, documentElement[Name("K")]);
        PdfDictionary attributes = Assert.IsType<PdfDictionary>(list[Name("A")]);
        Assert.Equal("Decimal",
            Assert.IsType<PdfName>(attributes[Name("ListNumbering")]).ValueAsLatin1());

        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Unnumbered accessible list",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .AddStructureContainer(PdfStructureType.List, 1)
            .AddStructureContainer(PdfStructureType.ListItem, 2)
            .AddStructureElement(PdfStructureType.Label, 0, 0, 3)
            .AddStructureElement(PdfStructureType.ListBody, 0, 1, 3)
            .Build());
    }

    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
