using System.Buffers.Binary;
using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfIccColorSpaceTests
{
    [Fact]
    public void Build_ReusesIccProfileAcrossPageFillAndStrokeResources()
    {
        PdfIccProfile profile = Profile("RGB ");
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillIccColor(profile, 0.1, 0.4, 0.8).Rectangle(0, 0, 40, 40).Fill())
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetStrokeIccColor(profile, 0.8, 0.2, 0.1).Rectangle(10, 10, 50, 50).Stroke())
            .Build());
        PdfDictionary[] pages = Pages(document);
        PdfIndirectReference first = ProfileReference(pages[0]);
        PdfIndirectReference second = ProfileReference(pages[1]);
        PdfStream stream = Assert.IsType<PdfStream>(document.Resolve(first));
        PdfStream firstContent = ResolveStream(document, pages[0][Name("Contents")]);

        Assert.Equal(first.ObjectNumber, second.ObjectNumber);
        Assert.Equal(3, Assert.IsType<PdfInteger>(stream.Dictionary[Name("N")]).Value);
        Assert.Equal("DeviceRGB", Assert.IsType<PdfName>(
            stream.Dictionary[Name("Alternate")]).ValueAsLatin1());
        Assert.Equal("/CS1 cs\n0.1 0.4 0.8 scn\n0 0 40 40 re\nf\n",
            Encoding.ASCII.GetString(firstContent.EncodedData.Span));
    }

    [Theory]
    [InlineData("GRAY", 1, "DeviceGray")]
    [InlineData("RGB ", 3, "DeviceRGB")]
    [InlineData("Lab ", 3, "DeviceRGB")]
    [InlineData("CMYK", 4, "DeviceCMYK")]
    public void Build_WritesProfileComponentCountAndAlternate(
        string signature, int components, string alternate)
    {
        PdfIccProfile profile = Profile(signature);
        PdfContentStreamBuilder content = new();
        content.SetFillIccColor(profile, [.. Enumerable.Repeat(0.5, components)]);
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(10, 10, content).Build());
        PdfStream stream = Assert.IsType<PdfStream>(document.Resolve(
            ProfileReference(Pages(document)[0])));

        Assert.Equal(components, Assert.IsType<PdfInteger>(stream.Dictionary[Name("N")]).Value);
        Assert.Equal(alternate, Assert.IsType<PdfName>(
            stream.Dictionary[Name("Alternate")]).ValueAsLatin1());
    }

    [Fact]
    public void IccColor_ValidatesComponentCountAndRange()
    {
        PdfIccProfile profile = Profile("RGB ");

        Assert.Throws<ArgumentException>(() =>
            new PdfContentStreamBuilder().SetFillIccColor(profile, 0.1, 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfContentStreamBuilder().SetStrokeIccColor(profile, 0.1, 1.1, 0.2));
        Assert.Throws<ArgumentNullException>(() =>
            new PdfContentStreamBuilder().SetFillIccColor(null!, 0.1));
    }

    [Fact]
    public void Build_SharesProfileBetweenContentColorSpaceAndOutputIntent()
    {
        PdfIccProfile profile = Profile("RGB ");
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "Shared ICC", Language = "en-US" })
            .SetOutputIntent(profile, "Shared RGB")
            .EnablePdfA4Conformance()
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillIccColor(profile, 0.2, 0.5, 0.8).Rectangle(0, 0, 20, 20).Fill())
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary outputIntent = ResolveDictionary(document,
            Assert.IsType<PdfArray>(catalog[Name("OutputIntents")])[0]);
        PdfIndirectReference outputProfile = Assert.IsType<PdfIndirectReference>(
            outputIntent[Name("DestOutputProfile")]);
        PdfIndirectReference contentProfile = ProfileReference(Pages(document)[0]);

        Assert.Equal(outputProfile.ObjectNumber, contentProfile.ObjectNumber);
    }

    [Fact]
    public void PdfA4_RejectsContentUsingIdenticalCmykOutputProfile()
    {
        PdfIccProfile profile = Profile("CMYK");
        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "CMYK", Language = "en-US" })
            .SetOutputIntent(profile, "CMYK")
            .EnablePdfA4Conformance()
            .AddPage(100, 100, new PdfContentStreamBuilder()
                .SetFillIccColor(profile, 0.1, 0.2, 0.3, 0.4)
                .Rectangle(0, 0, 20, 20).Fill())
            .Build());
    }

    [Fact]
    public void Build_WritesIccBasedUncoloredPatternBaseColor()
    {
        PdfIccProfile profile = Profile("RGB ");
        var stencil = new PdfTilingPattern(8, 8,
            new PdfContentStreamBuilder().Rectangle(1, 1, 6, 6).Fill(),
            paintType: PdfTilingPatternPaintType.Uncolored);
        byte[] content = new PdfContentStreamBuilder()
            .SetFillPattern(stencil, profile, 0.15, 0.45, 0.85)
            .Rectangle(0, 0, 20, 20).Fill().Build();

        Assert.Equal(
            "[/Pattern /CS1] cs\n0.15 0.45 0.85 /P1 scn\n0 0 20 20 re\nf\n",
            Encoding.ASCII.GetString(content));
    }

    [Fact]
    public void Build_AssignsCollisionFreeMixedColorSpaceResources()
    {
        PdfIccProfile icc = Profile("RGB ");
        var spot = new PdfSpotColor("Ink", new PdfCmykColor(0, 0.5, 1, 0));
        var lab = new PdfLabColorSpace();
        var indexed = new PdfIndexedColorSpace(
            PdfIndexedBaseColorSpace.Rgb, new byte[] { 255, 0, 0, 0, 0, 255 });
        PdfContentStreamBuilder content = new PdfContentStreamBuilder()
            .SetFillIccColor(icc, 0.1, 0.2, 0.3)
            .SetFillSpotColor(spot, 0.5)
            .SetFillLabColor(lab, 50, 10, -10)
            .SetFillIndexedColor(indexed, 1);
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(10, 10, content).Build());
        PdfDictionary resources = Assert.IsType<PdfDictionary>(Pages(document)[0][Name("Resources")]);
        PdfDictionary spaces = Assert.IsType<PdfDictionary>(resources[Name("ColorSpace")]);

        Assert.Equal(["CS1", "CS2", "CS3", "CS4"],
            [.. spaces.Keys.Select(name => name.ValueAsLatin1()).Order()]);
    }

    private static PdfIccProfile Profile(string colorSpace)
    {
        byte[] bytes = new byte[132];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 132);
        Encoding.ASCII.GetBytes(colorSpace).CopyTo(bytes, 16);
        "acsp"u8.CopyTo(bytes.AsSpan(36));
        return PdfIccProfile.Load(bytes);
    }

    private static PdfIndirectReference ProfileReference(PdfDictionary page)
    {
        PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        PdfDictionary colorSpaces = Assert.IsType<PdfDictionary>(resources[Name("ColorSpace")]);
        PdfArray icc = Assert.IsType<PdfArray>(colorSpaces[Name("CS1")]);
        Assert.Equal("ICCBased", Assert.IsType<PdfName>(icc[0]).ValueAsLatin1());
        return Assert.IsType<PdfIndirectReference>(icc[1]);
    }
    private static PdfDictionary[] Pages(PdfDocument document)
    {
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        return [.. Assert.IsType<PdfArray>(pages[Name("Kids")]).Select(value => ResolveDictionary(document, value))];
    }
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfStream ResolveStream(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfStream>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
