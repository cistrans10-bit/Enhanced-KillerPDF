using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfAttachmentTests
{
    [Fact]
    public void PdfUa2_FileAttachmentRequiresDescriptionsAndWritesStructureParent()
    {
        static PdfDocumentBuilder AccessibleBuilder() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible attachment",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddBlankPage();

        PdfDocument document = PdfDocument.Open(AccessibleBuilder()
            .AddAttachment("evidence.txt", "evidence"u8.ToArray(),
                "text/plain", "Plain-text evidence")
            .AddFileAttachmentAnnotation(0, 20, 30, 24, "evidence.txt",
                contents: "Open the plain-text evidence")
            .AddStructureContainer(PdfStructureType.Document)
            .Build());
        PdfDictionary annotation = Annotation(document);
        Assert.True(annotation.ContainsKey(Name("StructParent")));

        Assert.Throws<InvalidOperationException>(() => AccessibleBuilder()
            .AddAttachment("evidence.txt", "evidence"u8.ToArray(), "text/plain")
            .AddStructureContainer(PdfStructureType.Document)
            .Build());
        Assert.Throws<InvalidOperationException>(() => AccessibleBuilder()
            .AddAttachment("evidence.txt", "evidence"u8.ToArray(),
                "text/plain", "Plain-text evidence")
            .AddFileAttachmentAnnotation(0, 20, 30, 24, "evidence.txt")
            .AddStructureContainer(PdfStructureType.Document)
            .Build());
    }

    [Fact]
    public void AddAttachment_WritesNamesTreeAssociatedFileAndExactPayload()
    {
        byte[] payload = Encoding.UTF8.GetBytes("KillerPDF attachment");
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddAttachment("résumé.txt", payload, "text/plain", "Test data",
                PdfAssociatedFileRelationship.Data,
                new DateTimeOffset(2026, 8, 22, 20, 0, 0, TimeSpan.FromHours(-7)))
            .Build());
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var names = Assert.IsType<PdfDictionary>(catalog[Name("Names")]);
        var embeddedFiles = Assert.IsType<PdfDictionary>(names[Name("EmbeddedFiles")]);
        var nameArray = Assert.IsType<PdfArray>(embeddedFiles[Name("Names")]);
        var fileSpecReference = Assert.IsType<PdfIndirectReference>(nameArray[1]);
        var fileSpec = ResolveDictionary(document, fileSpecReference);
        var ef = Assert.IsType<PdfDictionary>(fileSpec[Name("EF")]);
        var embedded = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(ef[Name("UF")])));
        var associated = Assert.IsType<PdfArray>(catalog[Name("AF")]);

        Assert.Equal("résumé.txt", DecodeUnicode(Assert.IsType<PdfString>(nameArray[0])));
        Assert.Equal("résumé.txt", DecodeUnicode(Assert.IsType<PdfString>(fileSpec[Name("UF")])));
        Assert.Equal("Data", Assert.IsType<PdfName>(fileSpec[Name("AFRelationship")]).ValueAsLatin1());
        Assert.Equal("text/plain", Assert.IsType<PdfName>(embedded.Dictionary[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(payload, embedded.EncodedData.ToArray());
        Assert.Equal(fileSpecReference.ObjectNumber,
            Assert.IsType<PdfIndirectReference>(associated[0]).ObjectNumber);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("folder/file.txt")]
    [InlineData("folder\\file.txt")]
    [InlineData("drive:file.txt")]
    [InlineData("trailing.")]
    [InlineData("bad\0name.txt")]
    [InlineData("bad\u007fname.txt")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("LPT9.log")]
    [InlineData("")]
    public void AddAttachment_RejectsInvalidFileNames(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            new PdfDocumentBuilder().AddAttachment(name, ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void AddAttachment_RejectsDuplicateNamesIgnoringCase()
    {
        var builder = new PdfDocumentBuilder().AddAttachment("readme.txt", ReadOnlyMemory<byte>.Empty);

        Assert.Throws<ArgumentException>(() =>
            builder.AddAttachment("README.TXT", ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void AddAttachment_ValidatesMimeTypeStructure()
    {
        string[] invalid =
        ["", "text", "/plain", "text/", "text/plain/extra", "text /plain", "text/pl@in"];
        foreach (string value in invalid)
            Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder()
                .AddAttachment("file.bin", ReadOnlyMemory<byte>.Empty, value));

        new PdfDocumentBuilder().AddAttachment(
            "file.bin", ReadOnlyMemory<byte>.Empty, "application/vnd.killerpdf+zip");
    }

    [Theory]
    [InlineData(PdfFileAttachmentIcon.Graph, "Graph")]
    [InlineData(PdfFileAttachmentIcon.Paperclip, "Paperclip")]
    [InlineData(PdfFileAttachmentIcon.PushPin, "PushPin")]
    [InlineData(PdfFileAttachmentIcon.Tag, "Tag")]
    public void AddFileAttachmentAnnotation_ReferencesEmbeddedFileAndWritesAppearance(
        PdfFileAttachmentIcon icon, string expectedName)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddAttachment("evidence.txt", "payload"u8.ToArray(), "text/plain")
            .AddFileAttachmentAnnotation(0, 20, 30, 24, "evidence.txt",
                "Open evidence", icon)
            .Build());
        PdfDictionary annotation = Annotation(document);
        PdfDictionary fileSpec = ResolveDictionary(document, annotation[Name("FS")]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(annotation[Name("AP")])[Name("N")])));

        Assert.Equal("FileAttachment",
            Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(expectedName,
            Assert.IsType<PdfName>(annotation[Name("Name")]).ValueAsLatin1());
        Assert.Equal("evidence.txt",
            DecodeUnicode(Assert.IsType<PdfString>(fileSpec[Name("UF")])));
        Assert.Contains("B\n", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void AddFileAttachmentAnnotation_RequiresAnExistingAttachment()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();

        Assert.Throws<ArgumentException>(() =>
            builder.AddFileAttachmentAnnotation(0, 20, 30, 24, "missing.txt"));
    }

    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfDictionary Annotation(PdfDocument document)
    {
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        PdfDictionary page = ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        return ResolveDictionary(document, Assert.IsType<PdfArray>(page[Name("Annots")])[0]);
    }
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
