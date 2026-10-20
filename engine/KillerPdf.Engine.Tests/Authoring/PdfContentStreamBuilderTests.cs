using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Filters;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Fonts;
using KillerPdf.Engine.Tests.Fonts;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfContentStreamBuilderTests
{
    [Fact]
    public void Build_WritesDeterministicGraphicsOperators()
    {
        byte[] content = new PdfContentStreamBuilder()
            .SaveState()
            .SetLineWidth(2)
            .SetStrokeRgb(1, 0.5, 0)
            .MoveTo(10, 20)
            .LineTo(30.25, 40)
            .Stroke()
            .RestoreState()
            .Build();

        Assert.Equal(
            "q\n2 w\n1 0.5 0 RG\n10 20 m\n30.25 40 l\nS\nQ\n",
            Encoding.ASCII.GetString(content));
    }

    [Fact]
    public void Build_WritesBezierShorthandsAndCompletePathPaintingOperators()
    {
        byte[] content = new PdfContentStreamBuilder()
            .MoveTo(10, 10).CurveTo(20, 30, 40, 50).CloseAndStroke()
            .MoveTo(60, 10).CurveToFinalControl(70, 30, 90, 50).FillAndStrokeEvenOdd()
            .Rectangle(10, 60, 30, 20).CloseFillAndStroke()
            .Rectangle(50, 60, 30, 20).CloseFillAndStrokeEvenOdd()
            .Build();

        Assert.Equal(
            "10 10 m\n20 30 40 50 v\ns\n60 10 m\n70 30 90 50 y\nB*\n10 60 30 20 re\nb\n50 60 30 20 re\nb*\n",
            Encoding.ASCII.GetString(content));
    }

    [Fact]
    public void Build_WritesEllipseCircleAndRoundedRectanglePaths()
    {
        string ellipse = Encoding.ASCII.GetString(new PdfContentStreamBuilder()
            .Ellipse(10, 20, 80, 40).Fill().Build());
        string circle = Encoding.ASCII.GetString(new PdfContentStreamBuilder()
            .Circle(50, 50, 20).Stroke().Build());
        string rounded = Encoding.ASCII.GetString(new PdfContentStreamBuilder()
            .RoundedRectangle(10, 20, 80, 40, 8).FillAndStroke().Build());

        Assert.StartsWith("90 40 m\n", ellipse);
        Assert.Equal(4, ellipse.Split(" c\n", StringSplitOptions.None).Length - 1);
        Assert.EndsWith("h\nf\n", ellipse);
        Assert.StartsWith("70 50 m\n", circle);
        Assert.Equal(4, circle.Split(" c\n", StringSplitOptions.None).Length - 1);
        Assert.EndsWith("h\nS\n", circle);
        Assert.StartsWith("18 20 m\n82 20 l\n", rounded);
        Assert.Equal(4, rounded.Split(" c\n", StringSplitOptions.None).Length - 1);
        Assert.Equal(4, rounded.Split(" l\n", StringSplitOptions.None).Length - 1);
        Assert.EndsWith("h\nB\n", rounded);
    }

    [Fact]
    public void ConvenienceShapes_RejectInvalidGeometry()
    {
        var content = new PdfContentStreamBuilder();
        Assert.Throws<ArgumentOutOfRangeException>(() => content.Ellipse(0, 0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => content.Circle(0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            content.RoundedRectangle(0, 0, 20, 10, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            content.RoundedRectangle(double.NaN, 0, 20, 10, 2));
    }

    [Fact]
    public void Build_WritesCompleteStrokeStyling()
    {
        byte[] content = new PdfContentStreamBuilder()
            .SetLineWidth(3.5)
            .SetLineCap(PdfLineCap.Round)
            .SetLineJoin(PdfLineJoin.Bevel)
            .SetMiterLimit(7)
            .SetDashPattern([8, 3, 2, 3], 1.5)
            .MoveTo(10, 10).LineTo(50, 50).Stroke()
            .SetSolidStroke()
            .Build();

        Assert.Equal(
            "3.5 w\n1 J\n2 j\n7 M\n[8 3 2 3] 1.5 d\n10 10 m\n50 50 l\nS\n[] 0 d\n",
            Encoding.ASCII.GetString(content));
    }

    [Fact]
    public void StrokeStyling_RejectsInvalidValues()
    {
        var content = new PdfContentStreamBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => content.SetLineCap((PdfLineCap)3));
        Assert.Throws<ArgumentOutOfRangeException>(() => content.SetLineJoin((PdfLineJoin)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => content.SetMiterLimit(0.99));
        Assert.Throws<ArgumentOutOfRangeException>(() => content.SetDashPattern([2, -1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => content.SetDashPattern([2], double.NaN));
        Assert.Throws<ArgumentException>(() => content.SetDashPattern([0, 0]));
    }

    [Fact]
    public void Build_WritesRenderingIntentAndFlatness()
    {
        Assert.Equal("/RelativeColorimetric ri\n0.75 i\n",
            Encoding.ASCII.GetString(new PdfContentStreamBuilder()
                .SetRenderingIntent(PdfRenderingIntent.RelativeColorimetric)
                .SetFlatnessTolerance(0.75).Build()));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetRenderingIntent((PdfRenderingIntent)4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetFlatnessTolerance(100.1));
    }

    [Fact]
    public void Build_WritesDeviceCmykFillAndStroke()
    {
        byte[] content = new PdfContentStreamBuilder()
            .SetFillCmyk(0.8, 0.25, 0, 0.1).Rectangle(10, 10, 30, 20).Fill()
            .SetStrokeCmyk(0, 0.7, 0.9, 0.05).Rectangle(50, 10, 30, 20).Stroke()
            .Build();

        Assert.Equal(
            "0.8 0.25 0 0.1 k\n10 10 30 20 re\nf\n0 0.7 0.9 0.05 K\n50 10 30 20 re\nS\n",
            Encoding.ASCII.GetString(content));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.PositiveInfinity)]
    public void DeviceCmyk_RejectsInvalidComponents(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfCmykColor(0, value, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetFillCmyk(0, 0, value, 0));
    }

    [Fact]
    public void DocumentBuilder_EmbedsTypedContentAndReopensIt()
    {
        var content = new PdfContentStreamBuilder()
            .SetFillGray(0.25)
            .Rectangle(10, 20, 30, 40)
            .Fill();
        PdfDocument document = PdfDocument.Open(
            new PdfDocumentBuilder().AddPage(100, 100, content).Build());
        var catalog = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(document.Trailer[Name("Root")])));
        var pages = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Pages")])));
        var kids = Assert.IsType<PdfArray>(pages[Name("Kids")]);
        var page = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(kids[0])));
        var stream = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(page[Name("Contents")])));

        Assert.Equal("0.25 g\n10 20 30 40 re\nf\n", Encoding.ASCII.GetString(stream.EncodedData.Span));
    }

    [Fact]
    public void Build_RejectsUnbalancedGraphicsState()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PdfContentStreamBuilder().SaveState().Build());
        Assert.Throws<InvalidOperationException>(() =>
            new PdfContentStreamBuilder().RestoreState());
    }

    [Fact]
    public void MarkedContent_WritesTaggedContentAndArtifacts()
    {
        byte[] content = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .BeginText().SetFont(PdfStandardFont.Helvetica, 12)
            .ShowLatin1Text("Tagged").EndText()
            .EndMarkedContent()
            .BeginArtifact().Rectangle(0, 0, 10, 10).Stroke().EndMarkedContent()
            .Build();
        string text = Encoding.ASCII.GetString(content);

        Assert.Contains("/P << /MCID 0 >> BDC", text, StringComparison.Ordinal);
        Assert.Contains("/Artifact BMC", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split("EMC\n", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void MarkedContent_RejectsDuplicateIdsAndUnbalancedSequences()
    {
        var duplicate = new PdfContentStreamBuilder()
            .BeginMarkedContent(PdfStructureType.Paragraph, 0)
            .EndMarkedContent();

        Assert.Throws<ArgumentException>(() =>
            duplicate.BeginMarkedContent(PdfStructureType.Span, 0));
        Assert.Throws<InvalidOperationException>(() =>
            new PdfContentStreamBuilder().EndMarkedContent());
        Assert.Throws<InvalidOperationException>(() =>
            new PdfContentStreamBuilder().BeginArtifact().Build());
    }

    [Fact]
    public void TextOperators_CreateAFontResourceAndEscapedText()
    {
        var content = new PdfContentStreamBuilder()
            .BeginText()
            .SetFont(PdfStandardFont.HelveticaBold, 18)
            .MoveText(72, 700)
            .ShowLatin1Text("KillerPDF (2.0)")
            .EndText();
        PdfDocument document = PdfDocument.Open(
            new PdfDocumentBuilder().AddPage(612, 792, content).Build());
        var catalog = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(document.Trailer[Name("Root")])));
        var pages = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Pages")])));
        var page = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfArray>(pages[Name("Kids")])[0])));
        var resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        var fonts = Assert.IsType<PdfDictionary>(resources[Name("Font")]);
        var font = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(fonts[Name("F1")])));

        Assert.Equal("Helvetica-Bold", Assert.IsType<PdfName>(font[Name("BaseFont")]).ValueAsLatin1());
        var stream = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(page[Name("Contents")])));
        Assert.Contains("(KillerPDF \\(2.0\\)) Tj", Encoding.ASCII.GetString(stream.EncodedData.Span));
    }

    [Fact]
    public void TextOperators_WritePositioningSpacingAndRenderingState()
    {
        byte[] content = new PdfContentStreamBuilder()
            .BeginText()
            .SetFont(PdfStandardFont.Helvetica, 14)
            .SetTextMatrix(0.866, 0.5, -0.5, 0.866, 72, 700)
            .SetTextLeading(18)
            .SetCharacterSpacing(0.25)
            .SetWordSpacing(1.5)
            .SetHorizontalTextScale(92)
            .SetTextRise(3)
            .SetTextRenderingMode(PdfTextRenderingMode.FillAndStroke)
            .ShowLatin1Text("Raised text")
            .MoveToNextTextLine()
            .SetTextRise(0)
            .ShowLatin1Text("Next line")
            .EndText()
            .Build();

        Assert.Equal(
            "BT\n/F1 14 Tf\n0.866 0.5 -0.5 0.866 72 700 Tm\n18 TL\n0.25 Tc\n1.5 Tw\n92 Tz\n3 Ts\n2 Tr\n(Raised text) Tj\nT*\n0 Ts\n(Next line) Tj\nET\n",
            Encoding.ASCII.GetString(content));
    }

    [Fact]
    public void TextOperators_WritePositionedLatin1Array()
    {
        byte[] content = new PdfContentStreamBuilder()
            .BeginText().SetFont(PdfStandardFont.Helvetica, 12)
            .ShowPositionedLatin1Text(["A", "V", "A"], [-80, 20])
            .EndText().Build();

        Assert.Equal("BT\n/F1 12 Tf\n[(A) -80 (V) 20 (A)] TJ\nET\n",
            Encoding.ASCII.GetString(content));
    }

    [Fact]
    public void TextOperators_WritePositionedEmbeddedUnicodeArray()
    {
        TrueTypeFont embedded = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        byte[] content = new PdfContentStreamBuilder()
            .BeginText().SetFont(embedded, 12)
            .ShowPositionedUnicodeText(["A", "A"], [-35.5])
            .EndText().Build();

        Assert.Equal("BT\n/F1 12 Tf\n[<0001> -35.5 <0001>] TJ\nET\n",
            Encoding.ASCII.GetString(content));
    }

    [Fact]
    public void PositionedText_RejectsMismatchedOrInvalidItems()
    {
        var latin = new PdfContentStreamBuilder()
            .BeginText().SetFont(PdfStandardFont.Helvetica, 12);

        Assert.Throws<ArgumentException>(() =>
            latin.ShowPositionedLatin1Text([], []));
        Assert.Throws<ArgumentException>(() =>
            latin.ShowPositionedLatin1Text(["A", "V"], []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            latin.ShowPositionedLatin1Text(["A", "V"], [double.NaN]));
        Assert.Throws<ArgumentException>(() =>
            latin.ShowPositionedLatin1Text(["A", "Ω"], [0]));
    }

    [Fact]
    public void TextState_RequiresTextObjectAndValidEnumerations()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new PdfContentStreamBuilder().SetTextLeading(12));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().BeginText().SetHorizontalTextScale(0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().BeginText()
                .SetTextRenderingMode((PdfTextRenderingMode)8));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().BeginText()
                .SetTextMatrix(1, 0, 0, double.NaN, 0, 0));
    }

    [Fact]
    public void UnicodeText_EmbedsTrueTypeType0FontAndToUnicodeMap()
    {
        TrueTypeFont embedded = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        var content = new PdfContentStreamBuilder()
            .BeginText()
            .SetFont(embedded, 12)
            .MoveText(72, 700)
            .ShowUnicodeText("A")
            .EndText();
        PdfDocument document = PdfDocument.Open(
            new PdfDocumentBuilder().AddPage(612, 792, content).Build());
        var catalog = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(document.Trailer[Name("Root")])));
        var pages = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Pages")])));
        var page = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfArray>(pages[Name("Kids")])[0])));
        var resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        var fontResources = Assert.IsType<PdfDictionary>(resources[Name("Font")]);
        var type0 = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(fontResources[Name("F1")])));
        var toUnicode = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(type0[Name("ToUnicode")])));
        var descendants = Assert.IsType<PdfArray>(type0[Name("DescendantFonts")]);
        var cidFont = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(descendants[0])));
        var descriptor = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(cidFont[Name("FontDescriptor")])));
        var fontFile = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(descriptor[Name("FontFile2")])));

        Assert.Equal("Type0", Assert.IsType<PdfName>(type0[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal("CIDFontType2", Assert.IsType<PdfName>(cidFont[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal("FlateDecode", Assert.IsType<PdfName>(
            fontFile.Dictionary[Name("Filter")]).ValueAsLatin1());
        Assert.True(fontFile.EncodedData.Length < embedded.FontData.Length);
        Assert.Equal(embedded.FontData.ToArray(), PdfStreamDecoder.Decode(fontFile));
        Assert.Contains("<0001> <0041>", Encoding.ASCII.GetString(
            PdfStreamDecoder.Decode(toUnicode)));
        var contentStream = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(page[Name("Contents")])));
        Assert.Contains("<0001> Tj", Encoding.ASCII.GetString(contentStream.EncodedData.Span));
    }

    [Fact]
    public void UnicodeText_WritesSupplementaryCharactersAsUtf16InToUnicodeMap()
    {
        TrueTypeFont embedded = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: true));
        var content = new PdfContentStreamBuilder()
            .BeginText().SetFont(embedded, 12).ShowUnicodeText("😀").EndText();
        PdfDocument document = PdfDocument.Open(
            new PdfDocumentBuilder().AddPage(100, 100, content).Build());
        PdfStream toUnicode = FindToUnicode(document);

        Assert.Contains("<0001> <D83DDE00>", Encoding.ASCII.GetString(
            PdfStreamDecoder.Decode(toUnicode)));
    }

    [Fact]
    public void UnicodeText_WritesVariationSequenceAsOneGlyphAndPreservesItInToUnicodeMap()
    {
        TrueTypeFont embedded = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(
            format12: false, cmap: TrueTypeFontTests.Cmap14()));
        var content = new PdfContentStreamBuilder()
            .BeginText().SetFont(embedded, 12).ShowUnicodeText("AA\uFE0F").EndText();
        PdfDocument document = PdfDocument.Open(
            new PdfDocumentBuilder().AddPage(100, 100, content).Build());
        PdfStream toUnicode = FindToUnicode(document);

        Assert.Equal("BT\n/F1 12 Tf\n<00010002> Tj\nET\n", Encoding.ASCII.GetString(content.Build()));
        Assert.Contains("<0001> <0041>", Encoding.ASCII.GetString(
            PdfStreamDecoder.Decode(toUnicode)));
        Assert.Contains("<0002> <0041FE0F>", Encoding.ASCII.GetString(
            PdfStreamDecoder.Decode(toUnicode)));
    }

    [Fact]
    public void UnicodeText_SharesEmbeddedFontObjectsForCompatiblePageMappings()
    {
        TrueTypeFont embedded = TrueTypeFont.Load(TrueTypeFontTests.BuildTestFont(format12: false));
        PdfContentStreamBuilder FirstContent() => new PdfContentStreamBuilder()
            .BeginText().SetFont(embedded, 12).ShowUnicodeText("A").EndText();
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, FirstContent())
            .AddPage(100, 100, FirstContent())
            .Build());
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(document.Trailer[Name("Root")])));
        PdfDictionary pages = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Pages")])));
        PdfArray kids = Assert.IsType<PdfArray>(pages[Name("Kids")]);
        PdfIndirectReference FontReference(PdfObject pageReference)
        {
            PdfDictionary page = Assert.IsType<PdfDictionary>(document.Resolve(
                Assert.IsType<PdfIndirectReference>(pageReference)));
            return Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(
                Assert.IsType<PdfDictionary>(page[Name("Resources")])[Name("Font")])[Name("F1")]);
        }

        Assert.Equal(FontReference(kids[0]).ObjectNumber, FontReference(kids[1]).ObjectNumber);
    }

    [Fact]
    public void CffOpenType_UsesCidFontType0AndFontFile3()
    {
        TrueTypeFont embedded = TrueTypeFont.Load(
            TrueTypeFontTests.BuildTestFont(format12: false, cffOutlines: true));
        var content = new PdfContentStreamBuilder()
            .BeginText().SetFont(embedded, 12).ShowUnicodeText("A").EndText();
        PdfDocument document = PdfDocument.Open(
            new PdfDocumentBuilder().AddPage(100, 100, content).Build());
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(document.Trailer[Name("Root")])));
        PdfDictionary pages = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Pages")])));
        PdfDictionary page = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfArray>(pages[Name("Kids")])[0])));
        PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        PdfDictionary fonts = Assert.IsType<PdfDictionary>(resources[Name("Font")]);
        PdfDictionary type0 = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(fonts[Name("F1")])));
        PdfDictionary cidFont = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfArray>(type0[Name("DescendantFonts")])[0])));
        PdfDictionary descriptor = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(cidFont[Name("FontDescriptor")])));
        PdfStream fontFile = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(descriptor[Name("FontFile3")])));

        Assert.Equal("CIDFontType0", Assert.IsType<PdfName>(
            cidFont[Name("Subtype")]).ValueAsLatin1());
        Assert.False(cidFont.ContainsKey(Name("CIDToGIDMap")));
        Assert.Equal("OpenType", Assert.IsType<PdfName>(
            fontFile.Dictionary[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(embedded.FontData.ToArray(), PdfStreamDecoder.Decode(fontFile));
    }

    [Fact]
    public void SetFont_RejectsRestrictedTrueTypeEmbedding()
    {
        TrueTypeFont restricted = TrueTypeFont.Load(
            TrueTypeFontTests.BuildTestFont(format12: false, embeddingFlags: 0x0002));
        var content = new PdfContentStreamBuilder().BeginText();

        Assert.Throws<InvalidOperationException>(() => content.SetFont(restricted, 12));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void SetFillGray_RejectsInvalidComponents(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetFillGray(value));
    }

    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));

    private static PdfStream FindToUnicode(PdfDocument document)
    {
        var catalog = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(document.Trailer[Name("Root")])));
        var pages = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Pages")])));
        var page = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfArray>(pages[Name("Kids")])[0])));
        var resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        var fonts = Assert.IsType<PdfDictionary>(resources[Name("Font")]);
        var type0 = Assert.IsType<PdfDictionary>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(fonts[Name("F1")])));
        return Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(type0[Name("ToUnicode")])));
    }
}
