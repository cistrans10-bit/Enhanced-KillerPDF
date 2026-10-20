using KillerPdf.Engine.Syntax;
using Xunit;

namespace KillerPdf.Engine.Tests.Syntax;

public sealed class PdfHeaderTests
{
    [Theory]
    [InlineData("%PDF-1.0\n", 1, 0)]
    [InlineData("%PDF-1.7\r\n", 1, 7)]
    [InlineData("%PDF-2.0\n", 2, 0)]
    [InlineData("%PDF-2.9\n", 2, 9)]
    public void Parse_AcceptsDefinedPdfVersions(string text, int major, int minor)
    {
        PdfHeader header = PdfHeader.Parse(System.Text.Encoding.ASCII.GetBytes(text));

        Assert.Equal(new PdfVersion(major, minor), header.Version);
        Assert.Equal(0, header.Offset);
    }

    [Fact]
    public void Parse_ReportsHeaderOffsetWithinCompatibilityWindow()
    {
        byte[] prefix = new byte[32];
        byte[] marker = "%PDF-2.0\n"u8.ToArray();
        byte[] source = [.. prefix, .. marker];

        PdfHeader header = PdfHeader.Parse(source);

        Assert.Equal(PdfVersion.Pdf20, header.Version);
        Assert.Equal(prefix.Length, header.Offset);
    }

    [Fact]
    public void Parse_RejectsUndefinedVersion()
    {
        Assert.Throws<NotSupportedException>(() => PdfHeader.Parse("%PDF-3.0\n"u8));
    }

    [Theory]
    [InlineData("%PDF-2.00\n")]
    [InlineData("%PDF-2.0x")]
    [InlineData("%PDF-2.0")]
    public void Parse_RecoversVersionWithoutCanonicalLineEnding(string source)
    {
        Assert.Equal(PdfVersion.Pdf20,
            PdfHeader.Parse(System.Text.Encoding.ASCII.GetBytes(source)).Version);
    }

    [Fact]
    public void Parse_MapsUnofficialPdf19DeclarationToPdf20()
    {
        Assert.Equal(PdfVersion.Pdf20, PdfHeader.Parse("%PDF-1.9\n"u8).Version);
    }

    [Fact]
    public void Parse_DoesNotSearchPastFirstKilobyte()
    {
        byte[] prefix = new byte[PdfHeader.SearchLimit];
        byte[] source = [.. prefix, .. "%PDF-2.0\n"u8];

        Assert.Throws<FormatException>(() => PdfHeader.Parse(source));
    }

    [Fact]
    public void Create_EmitsPdf20Header()
    {
        Assert.Equal("%PDF-2.0\n", System.Text.Encoding.ASCII.GetString(PdfHeader.Create(PdfVersion.Pdf20)));
    }

    [Fact]
    public void Create_RejectsDefaultVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PdfHeader.Create(default));
    }
}
