using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfPushButtonTests
{
    [Fact]
    public void AddUriPushButton_WritesIndependentInteractionStateIcons()
    {
        PdfImage normalIcon = PdfImage.FromRgb(1, 1, new byte[] { 255, 0, 0 });
        PdfImage rolloverIcon = PdfImage.FromRgb(1, 1, new byte[] { 0, 255, 0 });
        PdfImage downIcon = PdfImage.FromRgb(1, 1, new byte[] { 0, 0, 255 });
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "states", 0, 0, 100, 40, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions
                {
                    Icon = normalIcon,
                    RolloverIcon = rolloverIcon,
                    DownIcon = downIcon,
                    CaptionPosition = PdfPushButtonCaptionPosition.IconOnly
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary characteristics = Assert.IsType<PdfDictionary>(field[Name("MK")]);
        PdfDictionary appearances = Assert.IsType<PdfDictionary>(field[Name("AP")]);
        var normalReference = Assert.IsType<PdfIndirectReference>(characteristics[Name("I")]);
        var rolloverReference = Assert.IsType<PdfIndirectReference>(characteristics[Name("RI")]);
        var downReference = Assert.IsType<PdfIndirectReference>(characteristics[Name("IX")]);

        Assert.Equal(3, new[]
        {
            normalReference.ObjectNumber,
            rolloverReference.ObjectNumber,
            downReference.ObjectNumber
        }.Distinct().Count());
        Assert.True(appearances.ContainsKey(Name("R")));
        Assert.True(appearances.ContainsKey(Name("D")));
        Assert.Equal(rolloverReference.ObjectNumber,
            IconReference(ResolveStateAppearance(document, appearances[Name("R")])).ObjectNumber);
        Assert.Equal(downReference.ObjectNumber,
            IconReference(ResolveStateAppearance(document, appearances[Name("D")])).ObjectNumber);
    }

    [Theory]
    [InlineData(PdfPushButtonCaptionPosition.CaptionOnly, 0, false)]
    [InlineData(PdfPushButtonCaptionPosition.IconOnly, 1, true)]
    [InlineData(PdfPushButtonCaptionPosition.CaptionBelowIcon, 2, true)]
    [InlineData(PdfPushButtonCaptionPosition.CaptionAboveIcon, 3, true)]
    [InlineData(PdfPushButtonCaptionPosition.CaptionRightOfIcon, 4, true)]
    [InlineData(PdfPushButtonCaptionPosition.CaptionLeftOfIcon, 5, true)]
    [InlineData(PdfPushButtonCaptionPosition.CaptionOverIcon, 6, true)]
    public void AddUriPushButton_WritesEveryStandardCaptionPosition(
        PdfPushButtonCaptionPosition position, int expectedValue, bool drawsIcon)
    {
        PdfImage icon = PdfImage.FromRgb(1, 1, new byte[] { 20, 40, 80 });
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "position", 0, 0, 100, 40, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions
                {
                    Icon = icon,
                    CaptionPosition = position
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary characteristics = Assert.IsType<PdfDictionary>(field[Name("MK")]);
        PdfDictionary normal = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normal[Name("Normal")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Equal(expectedValue, Assert.IsType<PdfInteger>(characteristics[Name("TP")]).Value);
        Assert.Equal(drawsIcon, content.Contains("/BtnIcon Do", StringComparison.Ordinal));
        Assert.Equal(position != PdfPushButtonCaptionPosition.IconOnly,
            content.Contains("(Open) Tj", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(PdfPushButtonIconScaleMode.Always, "A")]
    [InlineData(PdfPushButtonIconScaleMode.Never, "N")]
    [InlineData(PdfPushButtonIconScaleMode.WhenTooLarge, "B")]
    [InlineData(PdfPushButtonIconScaleMode.WhenTooSmall, "S")]
    public void AddUriPushButton_WritesEveryIconScalePolicy(
        PdfPushButtonIconScaleMode mode, string expectedName)
    {
        PdfImage icon = PdfImage.FromRgb(1, 1, new byte[] { 20, 40, 80 });
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "scale", 0, 0, 100, 40, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions
                {
                    Icon = icon,
                    CaptionPosition = PdfPushButtonCaptionPosition.CaptionOverIcon,
                    IconScaleMode = mode
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary fit = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(field[Name("MK")])[Name("IF")]);

        Assert.Equal(expectedName, Assert.IsType<PdfName>(fit[Name("SW")]).ValueAsLatin1());
    }

    [Fact]
    public void AddUriPushButton_WritesIconFitDictionaryAndGeneratedIconArtwork()
    {
        PdfImage icon = PdfImage.FromRgb(2, 1, new byte[] {
            255, 0, 0, 0, 0, 255 });
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "icon", 0, 0, 100, 40, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions
                {
                    Icon = icon,
                    CaptionPosition = PdfPushButtonCaptionPosition.CaptionRightOfIcon,
                    IconScaleMode = PdfPushButtonIconScaleMode.WhenTooLarge,
                    IconHorizontalAlignment = 0.25,
                    IconVerticalAlignment = 0.75,
                    FitIconToBounds = true
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary characteristics = Assert.IsType<PdfDictionary>(field[Name("MK")]);
        PdfDictionary fit = Assert.IsType<PdfDictionary>(characteristics[Name("IF")]);
        PdfDictionary normal = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normal[Name("Normal")])));
        PdfDictionary resources = Assert.IsType<PdfDictionary>(appearance.Dictionary[Name("Resources")]);
        PdfDictionary xObjects = Assert.IsType<PdfDictionary>(resources[Name("XObject")]);

        Assert.Equal(4, Assert.IsType<PdfInteger>(characteristics[Name("TP")]).Value);
        Assert.Equal("B", Assert.IsType<PdfName>(fit[Name("SW")]).ValueAsLatin1());
        Assert.Equal("P", Assert.IsType<PdfName>(fit[Name("S")]).ValueAsLatin1());
        Assert.True(Assert.IsType<PdfBoolean>(fit[Name("FB")]).Value);
        Assert.IsType<PdfIndirectReference>(characteristics[Name("I")]);
        Assert.IsType<PdfIndirectReference>(xObjects[Name("BtnIcon")]);
        Assert.Contains("/BtnIcon Do", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void AddUriPushButton_ValidatesIconPlacementAndAlignment()
    {
        PdfImage icon = PdfImage.FromRgb(1, 1, new byte[] { 255, 255, 255 });
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddUriPushButton(0, "missing", 0, 0, 100, 20, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions
                {
                    CaptionPosition = PdfPushButtonCaptionPosition.IconOnly
                }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddUriPushButton(0, "anchor", 0, 0, 100, 20, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions
                {
                    Icon = icon,
                    CaptionPosition = PdfPushButtonCaptionPosition.CaptionOverIcon,
                    IconHorizontalAlignment = 1.1
                }));
    }

    [Theory]
    [InlineData(PdfTextFieldAlignment.Left, "4 6 Td")]
    [InlineData(PdfTextFieldAlignment.Center, "56.8 6 Td")]
    [InlineData(PdfTextFieldAlignment.Right, "109.6 6 Td")]
    public void AddUriPushButton_MeasuresCaptionAlignment(
        PdfTextFieldAlignment alignment, string expectedPosition)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "open", 0, 0, 140, 24, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions { Alignment = alignment })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary normal = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normal[Name("Normal")])));

        Assert.Contains(expectedPosition,
            Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void AddUriPushButton_RejectsInvalidCaptionAlignment()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddUriPushButton(0, "open", 0, 0, 140, 24, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions
                {
                    Alignment = (PdfTextFieldAlignment)9
                }));
    }

    [Fact]
    public void AddUriPushButton_WritesRolloverAndDownCaptionsAndAppearances()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "open", 0, 0, 140, 24, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions
                {
                    RolloverLabel = "Open now",
                    DownLabel = "Opening"
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary characteristics = Assert.IsType<PdfDictionary>(field[Name("MK")]);
        PdfDictionary appearances = Assert.IsType<PdfDictionary>(field[Name("AP")]);
        PdfStream rollover = ResolveStateAppearance(document, appearances[Name("R")]);
        PdfStream down = ResolveStateAppearance(document, appearances[Name("D")]);

        Assert.Equal("Open now", DecodeUnicode(
            Assert.IsType<PdfString>(characteristics[Name("RC")])));
        Assert.Equal("Opening", DecodeUnicode(
            Assert.IsType<PdfString>(characteristics[Name("AC")])));
        Assert.Contains("(Open now) Tj", Encoding.ASCII.GetString(rollover.EncodedData.Span));
        Assert.Contains("(Opening) Tj", Encoding.ASCII.GetString(down.EncodedData.Span));
    }

    [Fact]
    public void AddUriPushButton_ValidatesAlternateCaptions()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddUriPushButton(0, "open", 0, 0, 140, 24, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions { RolloverLabel = " " }));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddUriPushButton(0, "open", 0, 0, 140, 24, "Open", "https://example.com",
                appearanceOptions: new PdfPushButtonAppearanceOptions { DownLabel = "打开" }));
    }

    [Fact]
    public void AddUriPushButton_WritesCustomVisualStyle()
    {
        var style = new PdfFormFieldAppearanceStyle
        {
            BackgroundColor = new PdfRgbColor(0.8, 0.9, 1),
            BorderColor = new PdfRgbColor(0.1, 0.3, 0.5),
            TextColor = new PdfRgbColor(0.2, 0.2, 0.7),
            BorderWidth = 2
        };
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "open", 0, 0, 140, 24, "Open", "https://example.com",
                appearanceStyle: style)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary normal = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normal[Name("Normal")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Contains("0.8 0.9 1 rg", content);
        Assert.Contains("0.1 0.3 0.5 RG", content);
        Assert.Contains("0.2 0.2 0.7 rg", content);
        Assert.Contains("2 w", content);
    }

    [Theory]
    [InlineData(PdfPushButtonHighlightMode.None, "N")]
    [InlineData(PdfPushButtonHighlightMode.Invert, "I")]
    [InlineData(PdfPushButtonHighlightMode.Outline, "O")]
    [InlineData(PdfPushButtonHighlightMode.Push, "P")]
    [InlineData(PdfPushButtonHighlightMode.Toggle, "T")]
    public void AddUriPushButton_WritesTypedHighlightMode(
        PdfPushButtonHighlightMode mode, string expectedName)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "button", 0, 0, 100, 20, "Open",
                "https://example.com", highlightMode: mode)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);

        Assert.Equal(expectedName, Assert.IsType<PdfName>(field[Name("H")]).ValueAsLatin1());
    }

    [Fact]
    public void AddUriPushButton_WritesActionLabelFlagsMetadataAndAppearance()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriPushButton(0, "website", 20, 30, 140, 28,
                "Open KillerPDF", "https://killerpdf.com/docs",
                fieldMetadata: new PdfFormFieldMetadata
                {
                    Tooltip = "Open the documentation",
                    MappingName = "website_action"
                },
                fieldOptions: new PdfFormFieldOptions { ReadOnly = true, NoExport = true })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary action = Assert.IsType<PdfDictionary>(field[Name("A")]);
        PdfDictionary characteristics = Assert.IsType<PdfDictionary>(field[Name("MK")]);

        Assert.Equal("Btn", Assert.IsType<PdfName>(field[Name("FT")]).ValueAsLatin1());
        Assert.Equal((1 << 16) | 5, Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
        Assert.Equal("URI", Assert.IsType<PdfName>(action[Name("S")]).ValueAsLatin1());
        Assert.Equal("https://killerpdf.com/docs", DecodeUnicode(Assert.IsType<PdfString>(action[Name("URI")])));
        Assert.Equal("Open KillerPDF", DecodeUnicode(Assert.IsType<PdfString>(characteristics[Name("CA")])));
        Assert.Equal("Open the documentation", DecodeUnicode(Assert.IsType<PdfString>(field[Name("TU")])));
        PdfDictionary normalAppearances = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(normalAppearances[Name("Normal")])));
        Assert.Contains("(Open KillerPDF) Tj", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Theory]
    [InlineData("file:///tmp/report.pdf")]
    [InlineData("javascript:alert(1)")]
    [InlineData("relative/path")]
    public void AddUriPushButton_RejectsUnsafeUri(string uri)
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddUriPushButton(0, "unsafe", 0, 0, 100, 20, "Open", uri));
    }

    [Fact]
    public void AddUriPushButton_RequiresEmbeddedFontForUnicodeLabel()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddUriPushButton(0, "unicode", 0, 0, 100, 20, "Ouvrir Ω", "https://example.com"));
    }

    [Fact]
    public void PdfA4_RejectsUriPushButtonActions()
    {
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "Buttons", Language = "en-US" })
            .EnablePdfA4Conformance()
            .AddBlankPage()
            .AddUriPushButton(0, "website", 0, 0, 100, 20, "Open", "https://example.com")
            .Build());
    }

    [Fact]
    public void AddPagePushButton_WritesPreciseGoToDestination()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage().AddBlankPage()
            .AddPagePushButton(0, "next", 10, 10, 100, 20, "Next", 1,
                PdfDestination.At(12, 700, 1.5))
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary action = Assert.IsType<PdfDictionary>(field[Name("A")]);
        PdfArray destination = Assert.IsType<PdfArray>(action[Name("D")]);

        Assert.Equal("GoTo", Assert.IsType<PdfName>(action[Name("S")]).ValueAsLatin1());
        Assert.IsType<PdfIndirectReference>(destination[0]);
        Assert.Equal("XYZ", Assert.IsType<PdfName>(destination[1]).ValueAsLatin1());
        Assert.Equal(12, Assert.IsType<PdfInteger>(destination[2]).Value);
        Assert.Equal(700, Assert.IsType<PdfInteger>(destination[3]).Value);
        Assert.Equal(1.5, Assert.IsType<PdfReal>(destination[4]).Value);
    }

    [Fact]
    public void AddNamedDestinationPushButton_WritesSharedUnicodeTarget()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage().AddBlankPage()
            .AddNamedDestination("Résumé", 1, PdfDestination.FitWidth(700))
            .AddNamedDestinationPushButton(0, "resume", 10, 10, 100, 20,
                "Résumé", "Résumé")
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary action = Assert.IsType<PdfDictionary>(field[Name("A")]);

        Assert.Equal("GoTo", Assert.IsType<PdfName>(action[Name("S")]).ValueAsLatin1());
        Assert.Equal("Résumé", DecodeUnicode(Assert.IsType<PdfString>(action[Name("D")])));
    }

    [Fact]
    public void AddNamedDestinationPushButton_RequiresExistingTarget()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddNamedDestinationPushButton(0, "missing", 0, 0, 100, 20, "Missing", "missing"));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void AddResetFormPushButton_WritesSelectedFieldsAndExclusionFlag(
        bool excludeFields, int expectedFlags)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "name", 0, 0, 100, 20)
            .AddCheckBox(0, "approved", 0, 30, 20, 20)
            .AddResetFormPushButton(0, "reset", 0, 60, 100, 20, "Reset",
                ["name", "approved"], excludeFields)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfArray fields = Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")]);
        PdfDictionary action = Assert.IsType<PdfDictionary>(ResolveDictionary(document, fields[2])[Name("A")]);

        Assert.Equal("ResetForm", Assert.IsType<PdfName>(action[Name("S")]).ValueAsLatin1());
        Assert.Equal(["name", "approved"], Assert.IsType<PdfArray>(action[Name("Fields")])
            .Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
        if (expectedFlags == 0)
            Assert.False(action.ContainsKey(Name("Flags")));
        else
            Assert.Equal(expectedFlags, Assert.IsType<PdfInteger>(action[Name("Flags")]).Value);
    }

    [Fact]
    public void AddResetFormPushButton_RejectsUndefinedField()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddResetFormPushButton(0, "reset", 0, 0, 100, 20, "Reset", ["missing"]));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddResetFormPushButton(0, "reset", 0, 0, 100, 20, "Reset", excludeFields: true));
    }

    [Fact]
    public void AddSubmitPdfPushButton_WritesUrlFileSpecFieldsAndFlags()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "name", 0, 0, 100, 20)
            .AddSubmitPdfPushButton(0, "submit", 0, 30, 100, 20, "Submit",
                "https://example.com/forms", ["name"], excludeFields: true)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfArray fields = Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")]);
        PdfDictionary action = Assert.IsType<PdfDictionary>(ResolveDictionary(document, fields[1])[Name("A")]);
        PdfDictionary file = Assert.IsType<PdfDictionary>(action[Name("F")]);

        Assert.Equal("SubmitForm", Assert.IsType<PdfName>(action[Name("S")]).ValueAsLatin1());
        Assert.Equal((1 << 8) | 1, Assert.IsType<PdfInteger>(action[Name("Flags")]).Value);
        Assert.Equal("URL", Assert.IsType<PdfName>(file[Name("FS")]).ValueAsLatin1());
        Assert.Equal("https://example.com/forms",
            DecodeUnicode(Assert.IsType<PdfString>(file[Name("F")])));
        Assert.Equal("name", DecodeUnicode(Assert.IsType<PdfString>(
            Assert.IsType<PdfArray>(action[Name("Fields")])[0])));
    }

    [Theory]
    [InlineData("file:///tmp/upload")]
    [InlineData("mailto:test@example.com")]
    [InlineData("relative")]
    public void AddSubmitPdfPushButton_RejectsUnsafeEndpoint(string uri)
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddSubmitPdfPushButton(0, "submit", 0, 0, 100, 20, "Submit", uri));
    }

    [Fact]
    public void AddSubmitPdfPushButton_RequiresFieldsForExclusionMode()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddSubmitPdfPushButton(0, "submit", 0, 0, 100, 20, "Submit",
                "https://example.com", excludeFields: true));
    }

    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfStream ResolveStateAppearance(PdfDocument document, PdfObject value)
    {
        PdfDictionary states = Assert.IsType<PdfDictionary>(value);
        return Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(states[Name("Normal")])));
    }
    private static PdfIndirectReference IconReference(PdfStream appearance) =>
        Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(appearance.Dictionary[Name("Resources")])[Name("XObject")])[
                Name("BtnIcon")]);
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
