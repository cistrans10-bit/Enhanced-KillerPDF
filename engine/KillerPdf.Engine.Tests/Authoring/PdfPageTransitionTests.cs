using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfPageTransitionTests
{
    [Fact]
    public void Build_WritesTimedFlyTransition()
    {
        PdfDictionary page = FirstPage(PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .SetPageDisplayDuration(0, 8)
            .SetPageTransition(0, PdfPageTransition.Fly(
                90, PdfTransitionMotion.Outward, 0.6, opaque: true, duration: 1.5))
            .Build()));
        PdfDictionary transition = Assert.IsType<PdfDictionary>(page[Name("Trans")]);

        Assert.Equal(8, Assert.IsType<PdfInteger>(page[Name("Dur")]).Value);
        Assert.Equal("Fly", Assert.IsType<PdfName>(transition[Name("S")]).ValueAsLatin1());
        Assert.Equal(1.5, Assert.IsType<PdfReal>(transition[Name("D")]).Value);
        Assert.Equal("O", Assert.IsType<PdfName>(transition[Name("M")]).ValueAsLatin1());
        Assert.Equal(90, Assert.IsType<PdfInteger>(transition[Name("Di")]).Value);
        Assert.Equal(0.6, Assert.IsType<PdfReal>(transition[Name("SS")]).Value);
        Assert.True(Assert.IsType<PdfBoolean>(transition[Name("B")]).Value);
    }

    [Fact]
    public void Build_WritesSplitAndGlitterParameters()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage().AddBlankPage()
            .SetPageTransition(0, PdfPageTransition.Split(
                PdfTransitionDimension.Vertical, PdfTransitionMotion.Inward))
            .SetPageTransition(1, PdfPageTransition.Glitter(315, 2))
            .Build());
        PdfDictionary[] pages = Pages(document);
        PdfDictionary split = Assert.IsType<PdfDictionary>(pages[0][Name("Trans")]);
        PdfDictionary glitter = Assert.IsType<PdfDictionary>(pages[1][Name("Trans")]);

        Assert.Equal("V", Assert.IsType<PdfName>(split[Name("Dm")]).ValueAsLatin1());
        Assert.Equal("I", Assert.IsType<PdfName>(split[Name("M")]).ValueAsLatin1());
        Assert.Equal(315, Assert.IsType<PdfInteger>(glitter[Name("Di")]).Value);
    }

    [Fact]
    public void PageTransitions_RejectInvalidTimingDirectionAndScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPageTransition.Fade(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPageTransition.Wipe(45));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPageTransition.Glitter(90));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PdfPageTransition.Fly(0, PdfTransitionMotion.Inward, 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfDocumentBuilder().AddBlankPage().SetPageDisplayDuration(0, double.NaN));
    }

    private static PdfDictionary FirstPage(PdfDocument document) => Pages(document)[0];
    private static PdfDictionary[] Pages(PdfDocument document)
    {
        PdfDictionary catalog = Resolve(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = Resolve(document, catalog[Name("Pages")]);
        return [.. Assert.IsType<PdfArray>(pages[Name("Kids")]).Select(value => Resolve(document, value))];
    }
    private static PdfDictionary Resolve(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
