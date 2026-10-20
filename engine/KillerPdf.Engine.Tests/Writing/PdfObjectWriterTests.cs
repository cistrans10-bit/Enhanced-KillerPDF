using System.Text;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Parsing;
using KillerPdf.Engine.Writing;
using Xunit;

namespace KillerPdf.Engine.Tests.Writing;

public sealed class PdfObjectWriterTests
{
    [Fact]
    public void Write_UsesCanonicalScalarAndReferenceSyntax()
    {
        var value = new PdfArray([
            PdfNull.Instance,
            new PdfBoolean(true),
            new PdfBoolean(false),
            new PdfInteger(-42),
            new PdfIndirectReference(12, 3)
        ]);

        Assert.Equal("[null true false -42 12 3 R]", Text(PdfObjectWriter.Write(value)));
    }

    [Fact]
    public void Write_EscapesEveryUnsafeNameByteWithUppercaseHex()
    {
        byte[] bytes = [(byte)'A', (byte)' ', (byte)'B', (byte)'#', (byte)'/', (byte)'%', 0, 255];

        byte[] written = PdfObjectWriter.Write(new PdfName(bytes));

        Assert.Equal("/A#20B#23#2F#25#00#FF", Text(written));
        Assert.Equal(bytes, Assert.IsType<PdfName>(Parse(written)).Bytes.ToArray());
    }

    [Fact]
    public void Write_PreservesLiteralStringBytesWithDeterministicEscapes()
    {
        byte[] bytes = [(byte)'A', (byte)'(', (byte)')', (byte)'\\', (byte)'\n', 0, 255];

        byte[] written = PdfObjectWriter.Write(new PdfString(bytes, PdfStringForm.Literal));

        Assert.Equal("(A\\(\\)\\\\\\n\\000\\377)", Text(written));
        Assert.Equal(bytes, Assert.IsType<PdfString>(Parse(written)).Bytes.ToArray());
    }

    [Fact]
    public void Write_PreservesHexadecimalFormAndUsesUppercaseDigits()
    {
        byte[] written = PdfObjectWriter.Write(
            new PdfString([0x00, 0xAF, 0xFF], PdfStringForm.Hexadecimal));

        Assert.Equal("<00AFFF>", Text(written));
        Assert.Equal(PdfStringForm.Hexadecimal, Assert.IsType<PdfString>(Parse(written)).Form);
    }

    [Fact]
    public void PdfString_RejectsUndefinedLexicalForms()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfString("text"u8, (PdfStringForm)17));
    }

    [Fact]
    public void Containers_RejectNullObjectReferences()
    {
        Assert.Throws<ArgumentException>(() =>
            new PdfArray([PdfNull.Instance, null!]));
        Assert.Throws<ArgumentException>(() =>
            new PdfDictionary([
                new KeyValuePair<PdfName, PdfObject>(Name("Value"), null!)
            ]));
        Assert.Throws<ArgumentException>(() =>
            new PdfDictionary([
                new KeyValuePair<PdfName, PdfObject>(null!, PdfNull.Instance)
            ]));
    }

    [Fact]
    public void Write_SortsDictionaryNamesByTheirDecodedBytes()
    {
        var dictionary = new PdfDictionary([
            Pair("Zulu", new PdfInteger(3)),
            Pair("Alpha", new PdfInteger(1)),
            Pair("Beta", new PdfInteger(2))
        ]);

        Assert.Equal("<< /Alpha 1 /Beta 2 /Zulu 3 >>", Text(PdfObjectWriter.Write(dictionary)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(0.1)]
    [InlineData(1E+20)]
    [InlineData(1E-20)]
    [InlineData(double.MaxValue)]
    [InlineData(double.Epsilon)]
    public void Write_EmitsRoundTrippableRealNumbersWithoutExponentNotation(double value)
    {
        byte[] written = PdfObjectWriter.Write(new PdfReal(value));
        string text = Text(written);

        Assert.DoesNotContain("E", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains('.', text);
        Assert.Equal(value, Assert.IsType<PdfReal>(Parse(written)).Value);
    }

    [Fact]
    public void Write_NormalizesNegativeZero()
    {
        Assert.Equal("0.0", Text(PdfObjectWriter.Write(new PdfReal(-0.0))));
    }

    [Fact]
    public void Write_StreamReplacesStaleLengthWithExactEncodedByteCount()
    {
        var dictionary = new PdfDictionary([
            Pair("Z", new PdfInteger(9)),
            Pair("Length", new PdfInteger(999))
        ]);
        byte[] payload = [0x00, 0x0A, 0xFF];
        var indirect = new PdfIndirectObject(7, 0, new PdfStream(dictionary, payload), 0);

        byte[] written = PdfObjectWriter.Write(indirect);
        PdfIndirectObject reparsed = new PdfObjectParser(written).ParseIndirectObject();

        Assert.True(written.AsSpan().StartsWith("7 0 obj\n<< /Length 3 /Z 9 >>\nstream\n"u8));
        var stream = Assert.IsType<PdfStream>(reparsed.Value);
        Assert.Equal(3, Assert.IsType<PdfInteger>(stream.Dictionary[Name("Length")]).Value);
        Assert.Equal(payload, stream.EncodedData.ToArray());
    }

    [Fact]
    public void Write_RejectsDirectAndNestedStreams()
    {
        var stream = new PdfStream(new PdfDictionary([]), []);

        InvalidOperationException direct = Assert.Throws<InvalidOperationException>(
            () => PdfObjectWriter.Write(stream));
        InvalidOperationException nested = Assert.Throws<InvalidOperationException>(
            () => PdfObjectWriter.Write(new PdfArray([stream])));

        Assert.Contains("streams must be written as indirect objects",
            direct.Message, StringComparison.Ordinal);
        Assert.Contains("streams must be written as indirect objects",
            nested.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ProducesIdenticalBytesForEquivalentDictionaryInsertionOrders()
    {
        var first = new PdfDictionary([
            Pair("B", new PdfInteger(2)),
            Pair("A", new PdfInteger(1))
        ]);
        var second = new PdfDictionary([
            Pair("A", new PdfInteger(1)),
            Pair("B", new PdfInteger(2))
        ]);

        Assert.Equal(PdfObjectWriter.Write(first), PdfObjectWriter.Write(second));
    }

    [Fact]
    public void Write_EnforcesTheWriterNestingLimit()
    {
        PdfObject value = PdfNull.Instance;
        for (int index = 0; index <= PdfObjectWriter.MaximumNestingDepth; index++)
            value = new PdfArray([value]);

        Assert.Throws<InvalidOperationException>(() => PdfObjectWriter.Write(value));
    }

    [Fact]
    public void Write_RejectsReservedObjectZeroReference()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfObjectWriter.Write(new PdfIndirectReference(0, 0)));

        Assert.Contains("object number zero is reserved", error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RejectsReservedObjectZeroDeclaration()
    {
        var value = new PdfIndirectObject(0, 65_535, PdfNull.Instance, 0);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfObjectWriter.Write(value));

        Assert.Contains("object number zero is reserved", error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RejectsRetiredGenerationDeclaration()
    {
        var value = new PdfIndirectObject(1, 65_535, PdfNull.Instance, 0);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfObjectWriter.Write(value));

        Assert.Contains("generation 65,535 is retired", error.Message,
            StringComparison.Ordinal);
    }

    private static PdfObject Parse(byte[] source) => new PdfObjectParser(source).ParseSingleObject();
    private static string Text(byte[] value) => Encoding.ASCII.GetString(value);
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
    private static KeyValuePair<PdfName, PdfObject> Pair(string name, PdfObject value) =>
        new(Name(name), value);
}
