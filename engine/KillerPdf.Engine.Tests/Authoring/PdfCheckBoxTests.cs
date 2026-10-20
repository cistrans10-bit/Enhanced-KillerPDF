using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfCheckBoxTests
{
    [Fact]
    public void AddCheckBox_WritesCustomVisualStyleForBothStates()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddCheckBox(0, "styled", 0, 0, 20, 20, true,
                appearanceStyle: new PdfFormFieldAppearanceStyle
                {
                    BackgroundColor = new PdfRgbColor(0.9, 0.95, 1),
                    BorderColor = new PdfRgbColor(0.1, 0.3, 0.5),
                    TextColor = new PdfRgbColor(0.7, 0.1, 0.2),
                    BorderWidth = 2
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary normal = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")]);
        PdfStream on = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normal[Name("Yes")])));
        string content = Encoding.ASCII.GetString(on.EncodedData.Span);

        Assert.Contains("0.9 0.95 1 rg", content);
        Assert.Contains("0.1 0.3 0.5 RG", content);
        Assert.Contains("0.7 0.1 0.2 RG", content);
        Assert.Contains("2 w", content);
    }

    [Fact]
    public void AddCheckBox_WritesIndependentDefaultState()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddCheckBox(0, "approved", 0, 0, 20, 20,
                isChecked: true, defaultChecked: false)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary widget = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);

        Assert.Equal("Yes", Assert.IsType<PdfName>(widget[Name("V")]).ValueAsLatin1());
        Assert.Equal("Off", Assert.IsType<PdfName>(widget[Name("DV")]).ValueAsLatin1());
        Assert.Equal("Yes", Assert.IsType<PdfName>(widget[Name("AS")]).ValueAsLatin1());
    }

    [Theory]
    [InlineData(true, "Approved")]
    [InlineData(false, "Off")]
    public void AddCheckBox_WritesStateAndBothAppearances(bool isChecked, string expectedState)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddCheckBox(0, "approved", 72, 650, 18, 18, isChecked, "Approved")
            .Build());
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        var fields = Assert.IsType<PdfArray>(acroForm[Name("Fields")]);
        var widget = ResolveDictionary(document, fields[0]);
        var normal = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(widget[Name("AP")])[Name("N")]);
        var off = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normal[Name("Off")])));
        var on = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normal[Name("Approved")])));

        Assert.Equal("Btn", Assert.IsType<PdfName>(widget[Name("FT")]).ValueAsLatin1());
        Assert.Equal(expectedState, Assert.IsType<PdfName>(widget[Name("V")]).ValueAsLatin1());
        Assert.Equal(expectedState, Assert.IsType<PdfName>(widget[Name("DV")]).ValueAsLatin1());
        Assert.Equal(expectedState, Assert.IsType<PdfName>(widget[Name("AS")]).ValueAsLatin1());
        Assert.DoesNotContain(" m\n", Encoding.ASCII.GetString(off.EncodedData.Span));
        Assert.Contains(" m\n", Encoding.ASCII.GetString(on.EncodedData.Span));
        Assert.False(Assert.IsType<PdfBoolean>(acroForm[Name("NeedAppearances")]).Value);
    }

    [Fact]
    public void FormFieldNames_AreUniqueAcrossTextAndCheckboxTypes()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "shared", 0, 0, 100, 20);

        Assert.Throws<ArgumentException>(() =>
            builder.AddCheckBox(0, "shared", 0, 30, 20, 20));
        Assert.Throws<ArgumentException>(() =>
            new PdfDocumentBuilder().AddBlankPage()
                .AddCheckBox(0, "reserved", 0, 0, 20, 20, exportValue: "Off"));
    }

    [Theory]
    [InlineData(PdfCheckBoxMark.Check, "4", " l\nS")]
    [InlineData(PdfCheckBoxMark.Cross, "8", " m\n")]
    [InlineData(PdfCheckBoxMark.Circle, "l", " c\nh\nf")]
    [InlineData(PdfCheckBoxMark.Diamond, "u", " l\nh\nf")]
    [InlineData(PdfCheckBoxMark.Square, "n", " re\nf")]
    [InlineData(PdfCheckBoxMark.Star, "H", " l\nh\nf")]
    public void AddCheckBox_WritesTypedMarkCaptionAndArtwork(
        PdfCheckBoxMark mark, string caption, string artwork)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddCheckBox(0, "mark", 0, 0, 20, 20, true, mark: mark)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary characteristics = Assert.IsType<PdfDictionary>(field[Name("MK")]);
        PdfDictionary normal = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")]);
        PdfStream on = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normal[Name("Yes")])));

        Assert.Equal(caption, Encoding.Latin1.GetString(
            Assert.IsType<PdfString>(characteristics[Name("CA")]).Bytes.Span));
        Assert.Contains(artwork, Encoding.ASCII.GetString(on.EncodedData.Span));
    }

    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
