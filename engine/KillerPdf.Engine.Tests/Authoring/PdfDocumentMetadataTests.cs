using System.Text;
using System.Xml.Linq;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfDocumentMetadataTests
{
    [Fact]
    public void Build_WritesInformationXmpLanguageAndDeterministicIdentifiers()
    {
        var metadata = new PdfDocumentMetadata
        {
            Title = "KillerPDF – Unicode",
            Author = "Steve the Killer",
            Subject = "PDF 2.0 authoring",
            Keywords = "PDF, authoring",
            Creator = "KillerPDF",
            Producer = "The KillerPDF.Engine",
            Language = "en-US",
            CreationDate = new DateTimeOffset(2026, 8, 22, 12, 34, 56, TimeSpan.FromHours(-7))
        };
        var builder = new PdfDocumentBuilder().SetMetadata(metadata).AddBlankPage();

        byte[] first = builder.Build();
        byte[] second = builder.Build();
        PdfDocument document = PdfDocument.Open(first);
        var id = Assert.IsType<PdfArray>(document.Trailer[Name("ID")]);
        var info = ResolveDictionary(document, document.Trailer[Name("Info")]);
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var metadataStream = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Metadata")])));
        XDocument xmp = XDocument.Parse(Encoding.UTF8.GetString(metadataStream.EncodedData.Span));

        Assert.Equal(first, second);
        Assert.Equal(2, id.Count);
        Assert.Equal(16, Assert.IsType<PdfString>(id[0]).Bytes.Length);
        Assert.Equal(Assert.IsType<PdfString>(id[0]).Bytes.ToArray(),
            Assert.IsType<PdfString>(id[1]).Bytes.ToArray());
        Assert.NotNull(info[Name("Title")]);
        Assert.Equal("en-US", DecodeUnicode(Assert.IsType<PdfString>(catalog[Name("Lang")])));
        Assert.Contains(xmp.Descendants(), element =>
            element.Name.LocalName == "title" && element.Value == metadata.Title);
        Assert.Contains(xmp.Descendants(), element =>
            element.Name.LocalName == "language" && element.Value == metadata.Language);
    }

    [Fact]
    public void Build_AlwaysWritesAStableTrailerIdentifier()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        PdfDocument first = PdfDocument.Open(builder.Build());
        PdfDocument second = PdfDocument.Open(builder.Build());

        Assert.Equal(
            Assert.IsType<PdfString>(Assert.IsType<PdfArray>(first.Trailer[Name("ID")])[0]).Bytes.ToArray(),
            Assert.IsType<PdfString>(Assert.IsType<PdfArray>(second.Trailer[Name("ID")])[0]).Bytes.ToArray());
    }

    [Fact]
    public void Build_RejectsUnpairedSurrogateInPdfUnicodeMetadata()
    {
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Language = "en\uD800-US" }));
    }

    [Fact]
    public void SetMetadata_AcceptsStandardGrandfatheredAndPrivateLanguageTags()
    {
        string[] values =
        [
            "en", "en-US", "zh-Hant-TW", "de-CH-1901", "sl-rozaj-biske",
            "en-US-u-ca-gregory", "i-klingon", "x-kpdf", "en-x-kpdf"
        ];

        foreach (string value in values)
        {
            Exception? error = Record.Exception(() => new PdfDocumentBuilder().SetMetadata(
                new PdfDocumentMetadata { Language = value }));
            Assert.True(error is null, $"Expected valid language tag {value}: {error}");
        }
    }

    [Fact]
    public void SetMetadata_RejectsMalformedOrDuplicateLanguageSubtags()
    {
        string[] values =
        [
            "", "e", "en_Us", "en--US", "en-US-1901-1901",
            "en-a-one-a-two", "x", "en-x", "en-12"
        ];

        foreach (string value in values)
            Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().SetMetadata(
                new PdfDocumentMetadata { Language = value }));
    }

    [Fact]
    public void Build_WritesTypedTrappingStatusToInformationAndXmp()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Trapped = PdfTrappedStatus.True })
            .AddBlankPage()
            .Build());
        PdfDictionary info = ResolveDictionary(document, document.Trailer[Name("Info")]);
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfStream metadata = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(catalog[Name("Metadata")])));
        XDocument xmp = XDocument.Parse(Encoding.UTF8.GetString(metadata.EncodedData.Span));

        Assert.Equal("True", Assert.IsType<PdfName>(info[Name("Trapped")]).ValueAsLatin1());
        Assert.Contains(xmp.Descendants(), element =>
            element.Name.LocalName == "Trapped" && element.Value == "True");
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfDocumentMetadata
        {
            Trapped = (PdfTrappedStatus)99
        });
    }

    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
