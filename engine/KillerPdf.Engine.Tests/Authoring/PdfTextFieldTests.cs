using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Filters;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Fonts;
using KillerPdf.Engine.Tests.Fonts;
using System.Buffers.Binary;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfTextFieldTests
{
    [Theory]
    [InlineData(PdfFormFieldBorderStyle.Solid, "S", " re\nS")]
    [InlineData(PdfFormFieldBorderStyle.Dashed, "D", "[2 1] 0 d")]
    [InlineData(PdfFormFieldBorderStyle.Beveled, "B", "0.65 0.65 0.65 RG")]
    [InlineData(PdfFormFieldBorderStyle.Inset, "I", "0 0 0 RG")]
    [InlineData(PdfFormFieldBorderStyle.Underline, "U", " l\nS")]
    public void AddTextField_WritesStandardBorderStyles(
        PdfFormFieldBorderStyle borderStyle, string expectedName, string expectedArtwork)
    {
        var style = new PdfFormFieldAppearanceStyle
        {
            BorderStyle = borderStyle,
            DashPattern = borderStyle == PdfFormFieldBorderStyle.Dashed ? [2, 1] : null
        };
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "border", 0, 0, 120, 24, "Value", appearanceStyle: style)
            .Build());
        PdfDictionary field = FirstField(document);
        PdfDictionary border = Assert.IsType<PdfDictionary>(field[Name("BS")]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));

        Assert.Equal(expectedName, Assert.IsType<PdfName>(border[Name("S")]).ValueAsLatin1());
        Assert.Contains(expectedArtwork, Encoding.ASCII.GetString(appearance.EncodedData.Span));
        if (borderStyle == PdfFormFieldBorderStyle.Dashed)
            Assert.Equal(2, Assert.IsType<PdfArray>(border[Name("D")]).Count);
    }

    [Fact]
    public void AddTextField_ValidatesBorderDashPatterns()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "solid", 0, 0, 100, 20, appearanceStyle:
                new PdfFormFieldAppearanceStyle { DashPattern = [2] }));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "dashed", 0, 0, 100, 20, appearanceStyle:
                new PdfFormFieldAppearanceStyle
                {
                    BorderStyle = PdfFormFieldBorderStyle.Dashed,
                    DashPattern = [0, 0]
                }));
    }

    [Fact]
    public void AddTextField_WritesCustomVisualStyleIntoWidgetAndAppearance()
    {
        var style = new PdfFormFieldAppearanceStyle
        {
            BackgroundColor = new PdfRgbColor(0.9, 0.8, 0.7),
            BorderColor = new PdfRgbColor(0.1, 0.2, 0.3),
            TextColor = new PdfRgbColor(0.4, 0.5, 0.6),
            BorderWidth = 2
        };
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "styled", 0, 0, 160, 24, "Styled",
                appearanceStyle: style)
            .Build());
        PdfDictionary field = FirstField(document);
        PdfDictionary characteristics = Assert.IsType<PdfDictionary>(field[Name("MK")]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.True(characteristics.ContainsKey(Name("BG")));
        Assert.True(characteristics.ContainsKey(Name("BC")));
        Assert.Contains("0.9 0.8 0.7 rg", content);
        Assert.Contains("0.1 0.2 0.3 RG", content);
        Assert.Contains("0.4 0.5 0.6 rg", content);
        Assert.Contains("2 w", content);
    }

    [Fact]
    public void AddTextField_AllowsTransparentBorderlessStyleAndRejectsInvalidWidth()
    {
        byte[] pdf = new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "plain", 0, 0, 100, 20, appearanceStyle:
                new PdfFormFieldAppearanceStyle
                {
                    BackgroundColor = null,
                    BorderColor = null,
                    BorderWidth = 0
                })
            .Build();
        Assert.NotEmpty(pdf);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfDocumentBuilder()
            .AddBlankPage().AddTextField(0, "invalid", 0, 0, 100, 20,
                appearanceStyle: new PdfFormFieldAppearanceStyle { BorderWidth = -1 }));
    }

    [Fact]
    public void AddTextField_WritesIndependentDefaultValueAndFileSelectFlag()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "attachment", 0, 0, 200, 20,
                value: "C:/current.pdf",
                options: new PdfTextFieldOptions { FileSelect = true },
                defaultValue: "C:/default.pdf")
            .Build());
        PdfDictionary field = FirstField(document);

        Assert.Equal("C:/current.pdf", DecodeUnicode(Assert.IsType<PdfString>(field[Name("V")])));
        Assert.Equal("C:/default.pdf", DecodeUnicode(Assert.IsType<PdfString>(field[Name("DV")])));
        Assert.Equal(1 << 20, Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
    }

    [Fact]
    public void AddTextField_ValidatesDefaultValueAndFileSelectCombinations()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();

        Assert.Throws<ArgumentException>(() => builder.AddTextField(
            0, "long", 0, 0, 100, 20, options: new PdfTextFieldOptions { MaximumLength = 3 },
            defaultValue: "four"));
        Assert.Throws<ArgumentException>(() => builder.AddTextField(
            0, "multiline-file", 0, 0, 100, 20,
            options: new PdfTextFieldOptions { FileSelect = true, Multiline = true }));
    }

    [Fact]
    public void AddTextField_WritesValidatedRichTextValue()
    {
        const string rich =
            "<body xmlns=\"http://www.w3.org/1999/xhtml\"><p><b>Approved</b></p></body>";
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "notes", 0, 0, 180, 40, "Approved",
                options: new PdfTextFieldOptions { Multiline = true }, richTextValue: rich)
            .Build());
        PdfDictionary field = FirstField(document);

        Assert.Equal((1 << 25) | (1 << 12),
            Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
        Assert.Equal(rich, DecodeUnicode(Assert.IsType<PdfString>(field[Name("RV")])));
    }

    [Fact]
    public void AddTextField_RejectsInvalidOrIncompatibleRichText()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();

        Assert.Throws<ArgumentException>(() => builder.AddTextField(
            0, "xml", 0, 0, 100, 20, richTextValue: "<body>broken"));
        Assert.Throws<ArgumentException>(() => builder.AddTextField(
            0, "root", 0, 0, 100, 20, richTextValue: "<p xmlns=\"http://www.w3.org/1999/xhtml\">Text</p>"));
        Assert.Throws<ArgumentException>(() => builder.AddTextField(
            0, "password", 0, 0, 100, 20,
            options: new PdfTextFieldOptions { Password = true },
            richTextValue: "<body xmlns=\"http://www.w3.org/1999/xhtml\">Text</body>"));
    }

    [Fact]
    public void AddTextField_WritesAcroFormWidgetValueAndAppearance()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "customer-name", 72, 650, 240, 24, "Steve (Killer)", 12)
            .Build());
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        var fields = Assert.IsType<PdfArray>(acroForm[Name("Fields")]);
        var widgetReference = Assert.IsType<PdfIndirectReference>(fields[0]);
        var widget = ResolveDictionary(document, widgetReference);
        var appearanceDictionary = Assert.IsType<PdfDictionary>(widget[Name("AP")]);
        var appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(appearanceDictionary[Name("N")])));
        var pages = ResolveDictionary(document, catalog[Name("Pages")]);
        var page = ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        var annotations = Assert.IsType<PdfArray>(page[Name("Annots")]);

        Assert.False(Assert.IsType<PdfBoolean>(acroForm[Name("NeedAppearances")]).Value);
        Assert.Equal("Tx", Assert.IsType<PdfName>(widget[Name("FT")]).ValueAsLatin1());
        Assert.Equal("customer-name", DecodeUnicode(Assert.IsType<PdfString>(widget[Name("T")])));
        Assert.Equal("Steve (Killer)", DecodeUnicode(Assert.IsType<PdfString>(widget[Name("V")])));
        Assert.Equal("Steve (Killer)", DecodeUnicode(Assert.IsType<PdfString>(widget[Name("DV")])));
        Assert.Equal(widgetReference.ObjectNumber,
            Assert.IsType<PdfIndirectReference>(annotations[0]).ObjectNumber);
        Assert.Contains("(Steve \\(Killer\\)) Tj",
            Encoding.ASCII.GetString(appearance.EncodedData.Span));
        var resources = Assert.IsType<PdfDictionary>(appearance.Dictionary[Name("Resources")]);
        Assert.NotNull(Assert.IsType<PdfDictionary>(resources[Name("Font")])[Name("Helv")]);
    }

    [Fact]
    public void Build_WritesQualifiedFieldNamesAsAParentHierarchy()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "customer.name", 0, 0, 100, 20, "Steve")
            .AddCheckBox(0, "customer.approved", 0, 30, 20, 20, isChecked: true)
            .AddTextField(0, "billing.contact.email", 0, 60, 160, 20, "a@example.com")
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        PdfArray roots = Assert.IsType<PdfArray>(acroForm[Name("Fields")]);

        Assert.Equal(2, roots.Count);
        PdfDictionary customer = ResolveDictionary(document, roots[0]);
        Assert.Equal("customer", DecodeUnicode(Assert.IsType<PdfString>(customer[Name("T")])));
        PdfArray customerKids = Assert.IsType<PdfArray>(customer[Name("Kids")]);
        Assert.Equal(2, customerKids.Count);
        PdfDictionary name = ResolveDictionary(document, customerKids[0]);
        PdfDictionary approved = ResolveDictionary(document, customerKids[1]);
        Assert.Equal("name", DecodeUnicode(Assert.IsType<PdfString>(name[Name("T")])));
        Assert.Equal("approved", DecodeUnicode(Assert.IsType<PdfString>(approved[Name("T")])));
        Assert.Equal(Assert.IsType<PdfIndirectReference>(roots[0]).ObjectNumber,
            Assert.IsType<PdfIndirectReference>(name[Name("Parent")]).ObjectNumber);

        PdfDictionary billing = ResolveDictionary(document, roots[1]);
        PdfDictionary contact = ResolveDictionary(document,
            Assert.Single(Assert.IsType<PdfArray>(billing[Name("Kids")])));
        PdfDictionary email = ResolveDictionary(document,
            Assert.Single(Assert.IsType<PdfArray>(contact[Name("Kids")])));
        Assert.Equal("contact", DecodeUnicode(Assert.IsType<PdfString>(contact[Name("T")])));
        Assert.Equal("email", DecodeUnicode(Assert.IsType<PdfString>(email[Name("T")])));
    }

    [Fact]
    public void AddTextField_PreservesLinkAnnotationsBeforeWidgets()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddUriLink(0, 0, 0, 10, 10, "https://killerpdf.net")
            .AddTextField(0, "name", 20, 20, 100, 20)
            .Build());
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var pages = ResolveDictionary(document, catalog[Name("Pages")]);
        var page = ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        var annotations = Assert.IsType<PdfArray>(page[Name("Annots")]);

        Assert.Equal(2, annotations.Count);
        Assert.Equal("Link", Assert.IsType<PdfName>(
            ResolveDictionary(document, annotations[0])[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal("Widget", Assert.IsType<PdfName>(
            ResolveDictionary(document, annotations[1])[Name("Subtype")]).ValueAsLatin1());
    }

    [Fact]
    public void AddTextField_RejectsDuplicateNamesAndUnsupportedAppearanceText()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "name", 0, 0, 100, 20);

        Assert.Throws<ArgumentException>(() =>
            builder.AddTextField(0, "name", 0, 30, 100, 20));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "bad..name", 0, 0, 100, 20));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "account", 0, 0, 100, 20)
            .AddTextField(0, "account.name", 0, 30, 100, 20));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "unicode", 0, 0, 100, 20, "你好"));
    }

    [Fact]
    public void AddTextField_WritesCombLengthAndBehaviorFlags()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "code", 0, 0, 120, 20, "1234", 12,
                new PdfTextFieldOptions
                {
                    Required = true,
                    ReadOnly = true,
                    Comb = true,
                    MaximumLength = 8
                })
            .Build());
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        var widget = ResolveDictionary(document, Assert.IsType<PdfArray>(acroForm[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(widget[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Equal(8, Assert.IsType<PdfInteger>(widget[Name("MaxLen")]).Value);
        Assert.Equal((1 << 24) | 2 | 1, Assert.IsType<PdfInteger>(widget[Name("Ff")]).Value);
        Assert.Equal(7, content.Split(" 0.5 m\n", StringSplitOptions.None).Length - 1);
        Assert.Contains("(1) Tj", content);
        Assert.Contains("(2) Tj", content);
        Assert.Contains("(3) Tj", content);
        Assert.Contains("(4) Tj", content);
        Assert.DoesNotContain("(1234) Tj", content);
    }

    [Fact]
    public void AddTextField_RejectsInvalidCombConfigurationAndOversizedValue()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentException>(() => builder.AddTextField(
            0, "comb", 0, 0, 100, 20, options: new PdfTextFieldOptions { Comb = true }));
        Assert.Throws<ArgumentException>(() => builder.AddTextField(
            0, "short", 0, 0, 100, 20, "toolong", options:
                new PdfTextFieldOptions { MaximumLength = 3 }));
    }

    [Fact]
    public void AddTextField_WritesNoSpellCheckAndNoScrollFlags()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "fixed", 0, 0, 100, 20, options: new PdfTextFieldOptions
            {
                DoNotSpellCheck = true,
                DoNotScroll = true
            })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);

        Assert.Equal((1 << 22) | (1 << 23),
            Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
    }

    [Fact]
    public void AddTextField_NoScrollRejectsInitialContentOutsideVisibleArea()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "wide", 0, 0, 30, 20, "far too wide", options:
                new PdfTextFieldOptions { DoNotScroll = true }));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "tall", 0, 0, 80, 24, "one two three four five six", 10,
                new PdfTextFieldOptions { Multiline = true, DoNotScroll = true }));
    }

    [Fact]
    public void AddTextField_RendersMultilineValueOnSeparateClippedBaselines()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "address", 0, 0, 160, 60, "First\r\nSecond\nThird", 12,
                new PdfTextFieldOptions { Multiline = true })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Equal(1 << 12, Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
        Assert.Contains("re\nW\nn", content);
        Assert.Contains("(First) Tj", content);
        Assert.Contains("(Second) Tj", content);
        Assert.Contains("(Third) Tj", content);
        Assert.Equal(3, content.Split("BT\n", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void AddTextField_WrapsMultilineWordsToAvailableWidth()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "wrapped", 0, 0, 70, 80,
                "alpha beta extraordinarily", 10,
                new PdfTextFieldOptions { Multiline = true })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Contains("(alpha beta) Tj", content);
        Assert.DoesNotContain("(extraordinarily) Tj", content);
        Assert.True(content.Split("BT\n", StringSplitOptions.None).Length - 1 >= 3);
    }

    [Fact]
    public void AddTextField_RejectsLineBreaksUnlessMultiline()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "single", 0, 0, 100, 20, "First\nSecond"));
    }

    [Fact]
    public void AddTextField_MasksPasswordAppearanceWithoutChangingValue()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "password", 0, 0, 100, 20, "secret", options:
                new PdfTextFieldOptions { Password = true })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Equal("secret", DecodeUnicode(Assert.IsType<PdfString>(field[Name("V")])));
        Assert.DoesNotContain("secret", content);
        Assert.Contains("(******) Tj", content);
    }

    [Fact]
    public void AddTextField_RejectsMultilinePassword()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "password", 0, 0, 100, 40, "secret", options:
                new PdfTextFieldOptions { Password = true, Multiline = true }));
    }

    [Theory]
    [InlineData(PdfTextFieldAlignment.Center, 1)]
    [InlineData(PdfTextFieldAlignment.Right, 2)]
    public void AddTextField_WritesAlignmentAndMovesAppearance(
        PdfTextFieldAlignment alignment, int expectedQuadding)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "aligned", 0, 0, 200, 20, "Text", options:
                new PdfTextFieldOptions { Alignment = alignment })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Equal(expectedQuadding, Assert.IsType<PdfInteger>(field[Name("Q")]).Value);
        Assert.DoesNotContain("3 4 Td", content);
        Assert.Contains("(Text) Tj", content);
    }

    [Fact]
    public void AddTextField_EmbedsUnicodeFontInAppearanceAndAcroFormResources()
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: true));
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "emoji", 0, 0, 100, 20, "😀", 12, embeddedFont: font)
            .Build());
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        var formResources = Assert.IsType<PdfDictionary>(
            Assert.IsType<PdfDictionary>(acroForm[Name("DR")])[Name("Font")]);
        var field = ResolveDictionary(document, Assert.IsType<PdfArray>(acroForm[Name("Fields")])[0]);
        var appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        var type0 = ResolveDictionary(document, formResources[Name("FormF1")]);

        Assert.Equal("Type0", Assert.IsType<PdfName>(type0[Name("Subtype")]).ValueAsLatin1());
        Assert.Contains("/FormF1 12 Tf", Encoding.ASCII.GetString(appearance.EncodedData.Span));
        Assert.Contains("<0001> Tj", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void AddTextField_DistinguishesBaseAndVariationSequenceSharingOneGlyph()
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(
            format12: false, cmap: TrueTypeFontTests.Cmap14()));
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "variation", 0, 0, 100, 20, "AA\uFE0F", 12,
                embeddedFont: font)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        PdfDictionary type0 = ResolveDictionary(document,
            Assert.IsType<PdfDictionary>(
                Assert.IsType<PdfDictionary>(acroForm[Name("DR")])[Name("Font")])[Name("FormF1")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(acroForm[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        PdfStream toUnicode = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(type0[Name("ToUnicode")])));

        Assert.Contains("<00010002> Tj", Encoding.ASCII.GetString(appearance.EncodedData.Span));
        Assert.Contains("<0001> <0041>", Encoding.ASCII.GetString(
            PdfStreamDecoder.Decode(toUnicode)));
        Assert.Contains("<0002> <0041FE0F>", Encoding.ASCII.GetString(
            PdfStreamDecoder.Decode(toUnicode)));
    }

    [Fact]
    public void PdfA4Mode_AcceptsTextFieldWithEmbeddedFont()
    {
        TrueTypeFont font = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        byte[] icc = new byte[132];
        BinaryPrimitives.WriteUInt32BigEndian(icc, 132);
        "RGB "u8.CopyTo(icc.AsSpan(16));
        "acsp"u8.CopyTo(icc.AsSpan(36));

        byte[] pdf = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata())
            .SetOutputIntent(PdfIccProfile.Load(icc), "Test RGB")
            .EnablePdfA4Conformance()
            .AddBlankPage()
            .AddTextField(0, "letter", 0, 0, 100, 20, "A", embeddedFont: font)
            .Build();

        Assert.NotEmpty(pdf);
    }

    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfDictionary FirstField(PdfDocument document)
    {
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        return ResolveDictionary(document, Assert.IsType<PdfArray>(acroForm[Name("Fields")])[0]);
    }
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
