using System.IO.Compression;
using System.Text;
using KillerPdf.Engine.Filters;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Filters;

public sealed class PdfStreamDecoderTests
{
    [Fact]
    public void Decode_AllowsBoundedPngPredictorRowBytesBeforeReconstruction()
    {
        byte[] predicted = [0, 1, 2, 3, 4, 0, 5, 6, 7, 8];
        byte[] compressed = Compress(predicted);
        var stream = new PdfStream(new PdfDictionary([
            Pair("Filter", Name("FlateDecode")),
            Pair("DecodeParms", new PdfDictionary([
                Pair("Predictor", new PdfInteger(15)),
                Pair("Columns", new PdfInteger(4))]))]), compressed);

        byte[] decoded = PdfStreamDecoder.Decode(stream, maximumDecodedBytes: 8);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, decoded);
    }

    [Fact]
    public void Decode_AllowsBoundedIntermediateBytesInMultiFilterPipeline()
    {
        byte[] expected = [0x41];
        byte[] compressed = Compress(expected);
        byte[] hexadecimal = Encoding.ASCII.GetBytes(
            Convert.ToHexString(compressed) + ">");
        var stream = new PdfStream(new PdfDictionary([
            Pair("Filter", new PdfArray([
                Name("ASCIIHexDecode"), Name("FlateDecode")]))]), hexadecimal);

        byte[] decoded = PdfStreamDecoder.Decode(stream, maximumDecodedBytes: 1);

        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void Decode_EnforcesFinalLimitAfterBoundedMultiFilterIntermediate()
    {
        byte[] compressed = Compress([0x41, 0x42]);
        byte[] hexadecimal = Encoding.ASCII.GetBytes(
            Convert.ToHexString(compressed) + ">");
        var stream = new PdfStream(new PdfDictionary([
            Pair("Filter", new PdfArray([
                Name("ASCIIHexDecode"), Name("FlateDecode")]))]), hexadecimal);

        PdfFilterException error = Assert.Throws<PdfFilterException>(() =>
            PdfStreamDecoder.Decode(stream, maximumDecodedBytes: 1));

        Assert.Contains("safety limit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_ReturnsUnfilteredBytes()
    {
        byte[] source = [0x00, 0xFF, 0x41];

        Assert.Equal(source, PdfStreamDecoder.Decode(Stream(source)));
    }

    [Fact]
    public void Decode_EnforcesOutputLimitForUnfilteredBytes()
    {
        PdfStream stream = Stream(new byte[101]);

        PdfFilterException error = Assert.Throws<PdfFilterException>(
            () => PdfStreamDecoder.Decode(stream, 100));

        Assert.Contains("safety limit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_InflatesZlibData()
    {
        byte[] expected = Encoding.ASCII.GetBytes("PDF 2.0 object stream");
        PdfStream stream = Stream(Compress(expected), Pair("Filter", Name("FlateDecode")));

        Assert.Equal(expected, PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_ResolvesIndirectFilterParametersAndScalarValues()
    {
        byte[] expected = Encoding.ASCII.GetBytes("indirect stream metadata");
        var objects = new Dictionary<int, PdfObject>
        {
            [1] = new PdfIndirectReference(2, 0),
            [2] = Name("FlateDecode"),
            [3] = new PdfIndirectReference(4, 0),
            [4] = Dictionary(Pair("Predictor", new PdfIndirectReference(5, 0))),
            [5] = new PdfInteger(1)
        };
        PdfStream stream = Stream(Compress(expected),
            Pair("Filter", new PdfIndirectReference(1, 0)),
            Pair("DecodeParms", new PdfIndirectReference(3, 0)));

        Assert.Equal(expected, PdfStreamDecoder.Decode(stream, reference => objects[reference.ObjectNumber]));
    }

    [Fact]
    public void Decode_RejectsIndirectFilterCycles()
    {
        var objects = new Dictionary<int, PdfObject>
        {
            [1] = new PdfIndirectReference(2, 0),
            [2] = new PdfIndirectReference(1, 0)
        };
        PdfStream stream = Stream([], Pair("Filter", new PdfIndirectReference(1, 0)));

        PdfFilterException error = Assert.Throws<PdfFilterException>(() =>
            PdfStreamDecoder.Decode(stream, reference => objects[reference.ObjectNumber]));

        Assert.Contains("cycle", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_DecodesAsciiHexIncludingOddFinalNibble()
    {
        PdfStream stream = Stream("61 62 6>"u8.ToArray(),
            Pair("Filter", Name("ASCIIHexDecode")));

        Assert.Equal("ab`"u8.ToArray(), PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_DecodesAscii85ZeroTuple()
    {
        PdfStream stream = Stream("z~>"u8.ToArray(),
            Pair("Filter", Name("ASCII85Decode")));

        Assert.Equal(new byte[4], PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_DecodesRunLengthLiteralAndRepeatRuns()
    {
        PdfStream stream = Stream([2, (byte)'A', (byte)'B', (byte)'C', 254, (byte)'Z', 128],
            Pair("Filter", Name("RunLengthDecode")));

        Assert.Equal("ABCZZZ", Encoding.ASCII.GetString(PdfStreamDecoder.Decode(stream)));
    }

    [Fact]
    public void Decode_DecodesLzwCodesWithClearAndEndMarkers()
    {
        PdfStream stream = Stream(PackNineBitCodes(256, 'A', 'B', 'C', 257),
            Pair("Filter", Name("LZWDecode")));

        Assert.Equal("ABC", Encoding.ASCII.GetString(PdfStreamDecoder.Decode(stream)));
    }

    [Fact]
    public void Decode_AppliesLosslessFilterPipelineInDeclaredOrder()
    {
        byte[] expected = "chained PDF filters"u8.ToArray();
        byte[] compressed = Compress(expected);
        byte[] hexadecimal = Encoding.ASCII.GetBytes(
            Convert.ToHexString(compressed) + ">");
        PdfStream stream = Stream(hexadecimal,
            Pair("Filter", new PdfArray([Name("ASCIIHexDecode"), Name("FlateDecode")])));

        Assert.Equal(expected, PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_DoesNotApplyPredictorsToFiltersThatDoNotDefineThem()
    {
        var parameters = new PdfDictionary([
            Pair("Predictor", new PdfInteger(2)),
            Pair("Columns", new PdfInteger(3))
        ]);
        PdfStream stream = Stream("010101>"u8.ToArray(),
            Pair("Filter", Name("ASCIIHexDecode")),
            Pair("DecodeParms", parameters));

        Assert.Equal(new byte[] { 1, 1, 1 }, PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_ReversesPngUpPrediction()
    {
        byte[] predicted = [2, 10, 20, 30, 2, 5, 5, 5];
        PdfDictionary parameters = Dictionary(
            Pair("Predictor", new PdfInteger(12)),
            Pair("Columns", new PdfInteger(3)));
        PdfStream stream = Stream(
            Compress(predicted),
            Pair("Filter", Name("FlateDecode")),
            Pair("DecodeParms", parameters));

        Assert.Equal(new byte[] { 10, 20, 30, 15, 25, 35 }, PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_RejectsPngRowFilterThatContradictsFixedPredictor()
    {
        PdfDictionary parameters = Dictionary(
            Pair("Predictor", new PdfInteger(12)),
            Pair("Columns", new PdfInteger(3)));
        PdfStream stream = Stream(
            Compress([1, 10, 20, 30]),
            Pair("Filter", Name("FlateDecode")),
            Pair("DecodeParms", parameters));

        Assert.Throws<PdfFilterException>(() => PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_AllowsMixedRowFiltersForOptimumPngPredictor()
    {
        PdfDictionary parameters = Dictionary(
            Pair("Predictor", new PdfInteger(15)),
            Pair("Columns", new PdfInteger(3)));
        PdfStream stream = Stream(
            Compress([0, 1, 2, 3, 2, 1, 1, 1]),
            Pair("Filter", Name("FlateDecode")),
            Pair("DecodeParms", parameters));

        Assert.Equal([1, 2, 3, 2, 3, 4], PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_ReversesTiffPrediction()
    {
        byte[] predicted = [10, 10, 10, 40, 10, 10];
        PdfDictionary parameters = Dictionary(
            Pair("Predictor", new PdfInteger(2)),
            Pair("Columns", new PdfInteger(3)));
        PdfStream stream = Stream(
            Compress(predicted),
            Pair("Filter", Name("FlateDecode")),
            Pair("DecodeParms", parameters));

        Assert.Equal(new byte[] { 10, 20, 30, 40, 50, 60 }, PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_ReversesPackedFourBitTiffPrediction()
    {
        PdfDictionary parameters = Dictionary(
            Pair("Predictor", new PdfInteger(2)),
            Pair("BitsPerComponent", new PdfInteger(4)),
            Pair("Columns", new PdfInteger(4)));
        PdfStream stream = Stream(Compress([0x12, 0x48]),
            Pair("Filter", Name("FlateDecode")), Pair("DecodeParms", parameters));

        Assert.Equal(new byte[] { 0x13, 0x7F }, PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_EnforcesOutputLimitWhileInflating()
    {
        PdfStream stream = Stream(Compress(new byte[10_000]), Pair("Filter", Name("FlateDecode")));

        PdfFilterException error = Assert.Throws<PdfFilterException>(() => PdfStreamDecoder.Decode(stream, 100));
        Assert.Contains("safety limit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_EnforcesOutputLimitForCryptPassThrough()
    {
        PdfStream stream = Stream(new byte[101], Pair("Filter", Name("Crypt")));

        PdfFilterException error = Assert.Throws<PdfFilterException>(
            () => PdfStreamDecoder.Decode(stream, 100));

        Assert.Contains("safety limit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_AllowsCryptPassThroughAtOutputLimit()
    {
        byte[] source = new byte[100];
        PdfStream stream = Stream(source, Pair("Filter", Name("Crypt")));

        Assert.Equal(source, PdfStreamDecoder.Decode(stream, 100));
    }

    [Theory]
    [InlineData("DCTDecode")]
    public void Decode_ReportsUnsupportedFilters(string filter)
    {
        PdfStream stream = Stream([], Pair("Filter", Name(filter)));

        Assert.Throws<PdfFilterException>(() => PdfStreamDecoder.Decode(stream));
    }

    [Fact]
    public void Decode_RejectsInvalidZlibData()
    {
        PdfStream stream = Stream("not zlib"u8.ToArray(), Pair("Filter", Name("FlateDecode")));

        Assert.Throws<PdfFilterException>(() => PdfStreamDecoder.Decode(stream));
    }

    private static byte[] Compress(byte[] source)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(source);
        return output.ToArray();
    }

    private static byte[] PackNineBitCodes(params int[] codes)
    {
        byte[] result = new byte[(codes.Length * 9 + 7) / 8];
        int bitOffset = 0;
        foreach (int code in codes)
            for (int bit = 8; bit >= 0; bit--, bitOffset++)
                result[bitOffset / 8] |= (byte)(((code >> bit) & 1) << (7 - bitOffset % 8));
        return result;
    }

    private static PdfStream Stream(byte[] data, params KeyValuePair<PdfName, PdfObject>[] entries) =>
        new(Dictionary(entries), data);

    private static PdfDictionary Dictionary(params KeyValuePair<PdfName, PdfObject>[] entries) => new(entries);
    private static KeyValuePair<PdfName, PdfObject> Pair(string name, PdfObject value) => new(Name(name), value);
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
