using System.Text;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Parsing;
using KillerPdf.Engine.Syntax;
using Xunit;

namespace KillerPdf.Engine.Tests.Parsing;

public sealed class PdfStreamParserTests
{
    [Fact]
    public void ParseIndirectObject_CapturesBinaryStreamPayloadByDeclaredLength()
    {
        byte[] prefix = "8 0 obj << /Length 5 >> stream\r\n"u8.ToArray();
        byte[] payload = [0x00, 0x25, 0xFF, 0x0A, 0x41];
        byte[] suffix = "\r\nendstream endobj"u8.ToArray();
        byte[] source = [.. prefix, .. payload, .. suffix];
        var parser = new PdfObjectParser(source);

        PdfIndirectObject indirect = parser.ParseIndirectObject();

        var stream = Assert.IsType<PdfStream>(indirect.Value);
        Assert.Equal(payload, stream.EncodedData.ToArray());
        Assert.Equal(5, Assert.IsType<PdfInteger>(stream.Dictionary[Name("Length")]).Value);
    }

    [Fact]
    public void ParseIndirectObject_UsesResolverForIndirectStreamLength()
    {
        var parser = Parser(
            "8 0 obj << /Length 9 0 R >> stream\nHello\nendstream endobj",
            reference =>
            {
                Assert.Equal(9, reference.ObjectNumber);
                Assert.Equal(0, reference.Generation);
                return 5;
            });

        var stream = Assert.IsType<PdfStream>(parser.ParseIndirectObject().Value);

        Assert.Equal("Hello", Encoding.Latin1.GetString(stream.EncodedData.Span));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void ParseIndirectObject_AcceptsPdfLineEndingsAfterStreamKeyword(
        string openingLineEnding)
    {
        string source = $"1 0 obj << /Length 5 >> stream{openingLineEnding}Hello\nendstream endobj";

        var stream = Assert.IsType<PdfStream>(Parser(source).ParseIndirectObject().Value);

        Assert.Equal("Hello", Encoding.Latin1.GetString(stream.EncodedData.Span));
    }

    [Theory]
    [InlineData("1 0 obj << >> stream\n\nendstream endobj")]
    [InlineData("1 0 obj << /Length -1 >> stream\n\nendstream endobj")]
    [InlineData("1 0 obj << /Length /Five >> stream\nHello\nendstream endobj")]
    [InlineData("1 0 obj << /Length 5 >> stream\nHello\nendobj")]
    public void ParseIndirectObject_RejectsMalformedStreamBoundaries(string source)
    {
        Assert.Throws<PdfSyntaxException>(() => Parser(source).ParseIndirectObject());
    }

    [Theory]
    [InlineData("1 0 obj << /Length 5 >> stream Hello\nendstream endobj")]
    [InlineData("1 0 obj << /Length 8 >> stream\nHello\nendstream endobj")]
    public void ParseIndirectObject_RecoversMalformedStreamBoundaries(string source)
    {
        var stream = Assert.IsType<PdfStream>(Parser(source).ParseIndirectObject().Value);

        Assert.Equal("Hello", Encoding.Latin1.GetString(stream.EncodedData.Span));
    }

    [Fact]
    public void ParseIndirectObject_RequiresResolverForIndirectLength()
    {
        var parser = Parser("1 0 obj << /Length 9 0 R >> stream\nHello\nendstream endobj");

        PdfSyntaxException error = Assert.Throws<PdfSyntaxException>(() => parser.ParseIndirectObject());
        Assert.Contains("no length resolver", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void ParseIndirectObject_AcceptsPdfLineEndingsBeforeEndstream(string closingLineEnding)
    {
        string source = $"1 0 obj << /Length 1 >> stream\nX{closingLineEnding}endstream endobj";

        var stream = Assert.IsType<PdfStream>(Parser(source).ParseIndirectObject().Value);

        Assert.Equal("X", Encoding.Latin1.GetString(stream.EncodedData.Span));
    }

    [Fact]
    public void ParseIndirectObject_AcceptsQpdfStreamWithClosingLineEndingIncludedInLength()
    {
        var stream = Assert.IsType<PdfStream>(Parser(
            "1 0 obj << /Length 5 >> stream\nHelloendstream endobj").ParseIndirectObject().Value);

        Assert.Equal("Hello", Encoding.Latin1.GetString(stream.EncodedData.Span));
    }

    private static PdfObjectParser Parser(
        string source,
        Func<PdfIndirectReference, long>? streamLengthResolver = null) =>
        new(Encoding.Latin1.GetBytes(source), streamLengthResolver);

    private static PdfName Name(string value) => new(Encoding.Latin1.GetBytes(value));
}
