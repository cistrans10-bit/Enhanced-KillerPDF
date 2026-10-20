using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Syntax;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfViewerPreferencesTests
{
    [Fact]
    public void Build_WritesLayoutModeAndViewerPreferences()
    {
        PdfDictionary catalog = Catalog(PdfDocument.Open(new PdfDocumentBuilder()
            .SetPageLayout(PdfPageLayout.TwoPageRight)
            .SetPageMode(PdfPageMode.UseThumbs)
            .SetViewerPreferences(new PdfViewerPreferences
            {
                HideToolbar = true,
                FitWindow = true,
                CenterWindow = true,
                DisplayDocumentTitle = true,
                ReadingDirection = PdfReadingDirection.RightToLeft,
                PrintScaling = PdfPrintScaling.None,
                Duplex = PdfDuplexMode.DuplexFlipLongEdge,
                PickTrayByPdfSize = true
            })
            .AddBlankPage().Build()));
        PdfDictionary preferences = Assert.IsType<PdfDictionary>(catalog[Name("ViewerPreferences")]);

        AssertName(catalog, "PageLayout", "TwoPageRight");
        AssertName(catalog, "PageMode", "UseThumbs");
        Assert.True(Assert.IsType<PdfBoolean>(preferences[Name("HideToolbar")]).Value);
        Assert.True(Assert.IsType<PdfBoolean>(preferences[Name("FitWindow")]).Value);
        Assert.True(Assert.IsType<PdfBoolean>(preferences[Name("CenterWindow")]).Value);
        Assert.True(Assert.IsType<PdfBoolean>(preferences[Name("DisplayDocTitle")]).Value);
        Assert.True(Assert.IsType<PdfBoolean>(preferences[Name("PickTrayByPDFSize")]).Value);
        AssertName(preferences, "Direction", "R2L");
        AssertName(preferences, "PrintScaling", "None");
        AssertName(preferences, "Duplex", "DuplexFlipLongEdge");
        Assert.False(preferences.ContainsKey(Name("HideMenubar")));
    }

    [Fact]
    public void Build_ExplicitPageModeOverridesAutomaticOutlineMode()
    {
        PdfDictionary catalog = Catalog(PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage().AddBookmark("Start", 0)
            .SetPageMode(PdfPageMode.UseNone).Build()));

        Assert.True(catalog.ContainsKey(Name("Outlines")));
        AssertName(catalog, "PageMode", "UseNone");
    }

    [Fact]
    public void PdfUa2_ForcesDocumentTitlePreferenceWithoutDiscardingOthers()
    {
        var content = new PdfContentStreamBuilder().BeginArtifact()
            .Rectangle(0, 0, 1, 1).Fill().EndMarkedContent();
        PdfDictionary catalog = Catalog(PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "UA", Language = "en-US" })
            .EnablePdfUa2Conformance()
            .SetViewerPreferences(new PdfViewerPreferences { HideMenuBar = true })
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document)
            .Build()));
        PdfDictionary preferences = Assert.IsType<PdfDictionary>(catalog[Name("ViewerPreferences")]);

        Assert.True(Assert.IsType<PdfBoolean>(preferences[Name("DisplayDocTitle")]).Value);
        Assert.True(Assert.IsType<PdfBoolean>(preferences[Name("HideMenubar")]).Value);
    }

    [Fact]
    public void PresentationOptions_RejectInvalidEnums()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfDocumentBuilder().SetPageLayout((PdfPageLayout)6));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfDocumentBuilder().SetPageMode((PdfPageMode)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfDocumentBuilder().SetViewerPreferences(new PdfViewerPreferences
            {
                Duplex = (PdfDuplexMode)4
            }));
    }

    [Theory]
    [InlineData(1, 4, PdfPageLayout.TwoPageLeft, PdfPageMode.UseNone)]
    [InlineData(1, 4, PdfPageLayout.SinglePage, PdfPageMode.UseOptionalContent)]
    [InlineData(1, 5, PdfPageLayout.SinglePage, PdfPageMode.UseAttachments)]
    public void Build_EnforcesLayoutAndModeVersionRequirements(
        int major, int minor, PdfPageLayout layout, PdfPageMode mode)
    {
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder(
                new PdfVersion(major, minor))
            .SetPageLayout(layout)
            .SetPageMode(mode)
            .AddBlankPage()
            .Build());
    }

    [Fact]
    public void Build_EnforcesViewerPreferenceVersionRequirements()
    {
        Assert.Throws<InvalidOperationException>(() => Build(
            new PdfVersion(1, 1), new PdfViewerPreferences()));
        Assert.Throws<InvalidOperationException>(() => Build(
            new PdfVersion(1, 2), new PdfViewerPreferences
            {
                ReadingDirection = PdfReadingDirection.RightToLeft
            }));
        Assert.Throws<InvalidOperationException>(() => Build(
            new PdfVersion(1, 3), new PdfViewerPreferences
            {
                DisplayDocumentTitle = true
            }));
        Assert.Throws<InvalidOperationException>(() => Build(
            new PdfVersion(1, 5), new PdfViewerPreferences
            {
                PrintScaling = PdfPrintScaling.None
            }));
        Assert.Throws<InvalidOperationException>(() => Build(
            new PdfVersion(1, 6), new PdfViewerPreferences
            {
                Duplex = PdfDuplexMode.Simplex
            }));

        static byte[] Build(PdfVersion version, PdfViewerPreferences preferences) =>
            new PdfDocumentBuilder(version)
                .SetViewerPreferences(preferences)
                .AddBlankPage()
                .Build();
    }

    private static PdfDictionary Catalog(PdfDocument document) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(
            document.Trailer[Name("Root")])));
    private static void AssertName(PdfDictionary dictionary, string key, string expected) =>
        Assert.Equal(expected, Assert.IsType<PdfName>(dictionary[Name(key)]).ValueAsLatin1());
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
