using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Syntax;
using KillerPdf.Engine.Security;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfDocumentBuilderTests
{
    [Fact]
    public void Constructor_RejectsDefaultVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfDocumentBuilder(default(PdfVersion)));
    }

    [Fact]
    public void Build_RequiresPdf20ForPdfA4AndPdfUa2Claims()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PdfDocumentBuilder(PdfVersion.Pdf17).EnablePdfA4Conformance().Build());
        Assert.Throws<InvalidOperationException>(() =>
            new PdfDocumentBuilder(PdfVersion.Pdf17).EnablePdfUa2Conformance().Build());
    }

    [Fact]
    public void Build_EnforcesFeatureMinimumVersionsWithoutExtensions()
    {
        Assert.StartsWith("%PDF-1.0", Encoding.ASCII.GetString(
            new PdfDocumentBuilder(PdfVersion.Pdf10).AddBlankPage().Build()));
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 3))
            .SetMetadata(new PdfDocumentMetadata()).Build());
        var tagged = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .Rectangle(0, 0, 10, 10).Fill()
            .EndMarkedContent();
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 2))
            .AddPage(100, 100, tagged)
            .AddStructureElement(PdfStructureType.Paragraph, 0, 0)
            .Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 5))
            .AddBlankPage().SetPageUserUnit(0, 2).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(PdfVersion.Pdf17)
            .AddAttachment("data.txt", "data"u8.ToArray(), "text/plain").Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(PdfVersion.Pdf17)
            .SetPasswordEncryption(new PdfPasswordEncryptionOptions
            {
                UserPassword = "user",
                OwnerPassword = "owner"
            }).Build());

        var layer = new PdfOptionalContentGroup("Layer");
        var layered = new PdfContentStreamBuilder()
            .BeginOptionalContent(layer).Rectangle(0, 0, 10, 10).Fill().EndMarkedContent();
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 4))
            .AddPage(100, 100, layered).Build());
        var transparent = new PdfContentStreamBuilder()
            .SetGraphicsState(new PdfGraphicsState(fillOpacity: 0.5))
            .Rectangle(0, 0, 10, 10).Fill();
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 3))
            .AddPage(100, 100, transparent).Build());
    }

    [Fact]
    public void Build_EnforcesFormAndAnnotationMinimumVersions()
    {
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 1))
            .AddBlankPage().AddTextField(0, "name", 0, 0, 100, 20).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 2))
            .AddBlankPage().AddSignatureField(0, "signature", 0, 0, 100, 20).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 3))
            .AddBlankPage().AddRectangleAnnotation(0, 0, 0, 20, 20).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 4))
            .AddBlankPage().AddCaretAnnotation(0, 0, 0, 20, 20).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 6))
            .AddBlankPage().AddRedactionMark(0,
                [new PdfTextQuad(new PdfPoint(0, 10), new PdfPoint(20, 10),
                    new PdfPoint(0, 0), new PdfPoint(20, 0))]).Build());
    }

    [Fact]
    public void Build_EnforcesResourceMinimumVersions()
    {
        var form = new PdfFormXObject(10, 10,
            new PdfContentStreamBuilder().Rectangle(0, 0, 10, 10).Fill());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 1))
            .AddPage(100, 100, new PdfContentStreamBuilder().DrawForm(form, 0, 0)).Build());

        var shading = new PdfAxialGradient(0, 0, 10, 0,
        [
            new PdfGradientStop(0, new PdfRgbColor(0, 0, 0)),
            new PdfGradientStop(1, new PdfRgbColor(1, 1, 1))
        ]);
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 2))
            .AddPage(100, 100, new PdfContentStreamBuilder().PaintShading(shading)).Build());

        PdfImage alpha = PdfImage.FromRgba(1, 1, new byte[] { 10, 20, 30, 128 });
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 3))
            .AddPage(100, 100,
                new PdfContentStreamBuilder().DrawImage(alpha, 0, 0, 10, 10)).Build());
    }

    [Fact]
    public void Build_EnforcesPagePresentationAndProductionMinimumVersions()
    {
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(PdfVersion.Pdf10)
            .AddBlankPage().SetPageDisplayDuration(0, 2).Build());
        Assert.NotEmpty(new PdfDocumentBuilder(new PdfVersion(1, 1))
            .AddBlankPage().SetPageTransition(0, PdfPageTransition.Dissolve()).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 4))
            .AddBlankPage().SetPageTransition(0, PdfPageTransition.Fade()).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 2))
            .AddBlankPage().AddPageLabelRange(0, PdfPageLabelStyle.Decimal).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 2))
            .AddBlankPage(100, 100)
            .SetPageBox(0, PdfPageBox.Trim, 5, 5, 90, 90).Build());
    }

    [Fact]
    public void Build_EnforcesNavigationAndViewerStateMinimumVersions()
    {
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(PdfVersion.Pdf10)
            .AddBlankPage().AddUriLink(0, 0, 0, 20, 20, "https://killerpdf.net").Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 1))
            .AddBlankPage().AddNamedDestination("top", 0).Build());
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 1))
            .SetViewerPreferences(new PdfViewerPreferences { FitWindow = true })
            .AddBlankPage().Build());
    }

    [Fact]
    public void Build_EnforcesColorSpaceMinimumVersions()
    {
        var lab = new PdfLabColorSpace();
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(PdfVersion.Pdf10)
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillLabColor(lab, 50, 0, 0).Rectangle(0, 0, 10, 10).Fill())
            .Build());

        var spot = new PdfSpotColor("Killer Orange", new PdfCmykColor(0, 0.7, 1, 0));
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(new PdfVersion(1, 1))
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillSpotColor(spot, 0.5).Rectangle(0, 0, 10, 10).Fill())
            .Build());
    }

    [Fact]
    public void Build_CreatesAReopenableCatalogAndPageTree()
    {
        byte[] bytes = new PdfDocumentBuilder()
            .AddBlankPage(612, 792)
            .AddPage(300.5, 400.25, "0 0 m 100 100 l S\n"u8.ToArray())
            .Build();
        PdfDocument document = PdfDocument.Open(bytes);

        var rootReference = Assert.IsType<PdfIndirectReference>(document.Trailer[Name("Root")]);
        var catalog = Assert.IsType<PdfDictionary>(document.Resolve(rootReference));
        var pagesReference = Assert.IsType<PdfIndirectReference>(catalog[Name("Pages")]);
        var pages = Assert.IsType<PdfDictionary>(document.Resolve(pagesReference));
        Assert.Equal(2, Assert.IsType<PdfInteger>(pages[Name("Count")]).Value);

        var kids = Assert.IsType<PdfArray>(pages[Name("Kids")]);
        var secondPage = Assert.IsType<PdfDictionary>(
            document.Resolve(Assert.IsType<PdfIndirectReference>(kids[1])));
        var mediaBox = Assert.IsType<PdfArray>(secondPage[Name("MediaBox")]);
        Assert.Equal(300.5, Assert.IsType<PdfReal>(mediaBox[2]).Value);
        var content = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(secondPage[Name("Contents")])));
        Assert.Equal("0 0 m 100 100 l S\n", Encoding.ASCII.GetString(content.EncodedData.Span));
    }

    [Fact]
    public void Build_IsDeterministicAndAcceptsAnEmptyPageTree()
    {
        byte[] first = new PdfDocumentBuilder().Build();
        byte[] second = new PdfDocumentBuilder().Build();

        Assert.Equal(first, second);
        Assert.True(KillerPdf.Engine.Diagnostics.PdfDocumentInspector.Inspect(first).IsStructurallyValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddBlankPage_RejectsInvalidDimensions(double width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfDocumentBuilder().AddBlankPage(width, 100));
    }

    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
