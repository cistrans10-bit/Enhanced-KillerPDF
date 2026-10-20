using System.Text;
using KillerPdf.Engine.Syntax;
using Xunit;

namespace KillerPdf.Engine.Tests.Syntax;

public sealed class PdfTokenizerTests
{
    [Fact]
    public void Read_RecognizesObjectSyntaxAndExactOffsets()
    {
        var tokenizer = Tokenizer("12 0 obj << /Type /Example true false null [3 -.5] >> endobj");

        AssertToken(tokenizer.Read(), PdfTokenKind.Integer, 0, 2, "12");
        AssertToken(tokenizer.Read(), PdfTokenKind.Integer, 3, 1, "0");
        AssertToken(tokenizer.Read(), PdfTokenKind.Keyword, 5, 3, "obj");
        AssertToken(tokenizer.Read(), PdfTokenKind.DictionaryStart, 9, 2);
        AssertToken(tokenizer.Read(), PdfTokenKind.Name, 12, 5, "Type");
        AssertToken(tokenizer.Read(), PdfTokenKind.Name, 18, 8, "Example");
        AssertToken(tokenizer.Read(), PdfTokenKind.Boolean, 27, 4, "true");
        AssertToken(tokenizer.Read(), PdfTokenKind.Boolean, 32, 5, "false");
        AssertToken(tokenizer.Read(), PdfTokenKind.Null, 38, 4);
        AssertToken(tokenizer.Read(), PdfTokenKind.ArrayStart, 43, 1);
        AssertToken(tokenizer.Read(), PdfTokenKind.Integer, 44, 1, "3");
        AssertToken(tokenizer.Read(), PdfTokenKind.Real, 46, 3, "-.5");
        AssertToken(tokenizer.Read(), PdfTokenKind.ArrayEnd, 49, 1);
        AssertToken(tokenizer.Read(), PdfTokenKind.DictionaryEnd, 51, 2);
        AssertToken(tokenizer.Read(), PdfTokenKind.Keyword, 54, 6, "endobj");
        AssertToken(tokenizer.Read(), PdfTokenKind.EndOfInput, 60, 0);
    }

    [Fact]
    public void Read_SkipsEveryPdfWhitespaceByteAndComments()
    {
        byte[] source = [0x00, 0x09, 0x0A, 0x0C, 0x0D, 0x20, .. "% ignored\r\n/Name"u8];
        var tokenizer = new PdfTokenizer(source);

        PdfToken token = tokenizer.Read();

        Assert.Equal(PdfTokenKind.Name, token.Kind);
        Assert.Equal("Name", token.ValueAsLatin1());
    }

    [Fact]
    public void Read_DecodesEscapedNameBytes()
    {
        PdfToken token = Tokenizer("/A#20Name#23Tag").Read();

        AssertToken(token, PdfTokenKind.Name, 0, 15, "A Name#Tag");
    }

    [Fact]
    public void Read_DecodesNestedLiteralStringsEscapesAndLineEndings()
    {
        PdfToken token = Tokenizer("(A\\nB\\053(C\\)D)\\\r\nE\r\nF)").Read();

        Assert.Equal(PdfTokenKind.LiteralString, token.Kind);
        Assert.Equal("A\nB+(C)D)E\nF", token.ValueAsLatin1());
    }

    [Theory]
    [InlineData("<48656c6c6f>", "Hello")]
    [InlineData("<48 65 6C 6C 6F>", "Hello")]
    [InlineData("<48656c6c6f2>", "Hello ")]
    public void Read_DecodesHexadecimalStrings(string source, string expected)
    {
        PdfToken token = Tokenizer(source).Read();

        Assert.Equal(PdfTokenKind.HexString, token.Kind);
        Assert.Equal(expected, token.ValueAsLatin1());
    }

    [Theory]
    [InlineData("0", PdfTokenKind.Integer)]
    [InlineData("+17", PdfTokenKind.Integer)]
    [InlineData("-3", PdfTokenKind.Integer)]
    [InlineData("34.", PdfTokenKind.Real)]
    [InlineData("-.002", PdfTokenKind.Real)]
    [InlineData("0.0", PdfTokenKind.Real)]
    public void Read_ClassifiesValidNumbers(string source, PdfTokenKind expected)
    {
        Assert.Equal(expected, Tokenizer(source).Read().Kind);
    }

    [Theory]
    [InlineData("+")]
    [InlineData(".")]
    [InlineData("1.2.3")]
    public void Read_RejectsMalformedNumbers(string source)
    {
        Assert.Throws<PdfSyntaxException>(() => Tokenizer(source).Read());
    }

    [Theory]
    [InlineData("(unterminated")]
    [InlineData("<ABCZ>")]
    [InlineData("/Bad#2GName")]
    [InlineData(">")]
    public void Read_RejectsMalformedLexicalConstructs(string source)
    {
        Assert.Throws<PdfSyntaxException>(() => Tokenizer(source).Read());
    }

    private static PdfTokenizer Tokenizer(string source) =>
        new(Encoding.Latin1.GetBytes(source));

    private static void AssertToken(
        PdfToken actual,
        PdfTokenKind kind,
        int offset,
        int length,
        string? value = null)
    {
        Assert.Equal(kind, actual.Kind);
        Assert.Equal(offset, actual.Offset);
        Assert.Equal(length, actual.Length);
        if (value != null)
            Assert.Equal(value, actual.ValueAsLatin1());
    }
}
