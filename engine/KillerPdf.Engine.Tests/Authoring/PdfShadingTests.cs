using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfShadingTests
{
    [Fact]
    public void Build_WritesClippedTwoColorAxialShading()
    {
        var gradient = new PdfAxialGradient(10, 20, 90, 80, [
            new PdfGradientStop(0, new PdfRgbColor(1, 0, 0)),
            new PdfGradientStop(1, new PdfRgbColor(0, 0, 1))],
            extendStart: true, extendEnd: false);
        var content = new PdfContentStreamBuilder()
            .Rectangle(10, 20, 80, 60).Clip()
            .PaintShading(gradient);

        PdfDocument document = PdfDocument.Open(
            new PdfDocumentBuilder().AddPage(100, 100, content).Build());
        PdfDictionary page = FirstPage(document);
        PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
        PdfDictionary shadings = Assert.IsType<PdfDictionary>(resources[Name("Shading")]);
        PdfDictionary shading = ResolveDictionary(document, shadings[Name("Sh1")]);
        PdfDictionary function = Assert.IsType<PdfDictionary>(shading[Name("Function")]);
        PdfStream stream = ResolveStream(document, page[Name("Contents")]);

        Assert.Equal(2, Assert.IsType<PdfInteger>(shading[Name("ShadingType")]).Value);
        Assert.Equal([10d, 20d, 90d, 80d],
            Assert.IsType<PdfArray>(shading[Name("Coords")]).Select(Number));
        Assert.Equal([true, false], Assert.IsType<PdfArray>(shading[Name("Extend")])
            .Select(value => Assert.IsType<PdfBoolean>(value).Value));
        Assert.Equal(2, Assert.IsType<PdfInteger>(function[Name("FunctionType")]).Value);
        Assert.Equal([1d, 0d, 0d], Assert.IsType<PdfArray>(function[Name("C0")]).Select(Number));
        Assert.Equal([0d, 0d, 1d], Assert.IsType<PdfArray>(function[Name("C1")]).Select(Number));
        Assert.Equal("10 20 80 60 re\nW\nn\n/Sh1 sh\n",
            Encoding.ASCII.GetString(stream.EncodedData.Span));
    }

    [Fact]
    public void Build_WritesMultiStopRadialShadingWithStitchingFunction()
    {
        var gradient = new PdfRadialGradient(50, 50, 0, 50, 50, 50, [
            new PdfGradientStop(0, new PdfRgbColor(1, 1, 1)),
            new PdfGradientStop(0.25, new PdfRgbColor(1, 0.5, 0)),
            new PdfGradientStop(0.75, new PdfRgbColor(0.5, 0, 0.8)),
            new PdfGradientStop(1, new PdfRgbColor(0, 0, 0))],
            extendEnd: true);
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder().PaintShading(gradient))
            .Build());
        PdfDictionary resources = Assert.IsType<PdfDictionary>(
            FirstPage(document)[Name("Resources")]);
        PdfDictionary shading = ResolveDictionary(document,
            Assert.IsType<PdfDictionary>(resources[Name("Shading")])[Name("Sh1")]);
        PdfDictionary function = Assert.IsType<PdfDictionary>(shading[Name("Function")]);
        PdfArray functions = Assert.IsType<PdfArray>(function[Name("Functions")]);

        Assert.Equal(3, Assert.IsType<PdfInteger>(shading[Name("ShadingType")]).Value);
        Assert.Equal([50d, 50d, 0d, 50d, 50d, 50d],
            Assert.IsType<PdfArray>(shading[Name("Coords")]).Select(Number));
        Assert.Equal(3, Assert.IsType<PdfInteger>(function[Name("FunctionType")]).Value);
        Assert.Equal([0.25, 0.75],
            Assert.IsType<PdfArray>(function[Name("Bounds")]).Select(Number));
        Assert.Equal([0d, 1d, 0d, 1d, 0d, 1d],
            Assert.IsType<PdfArray>(function[Name("Encode")]).Select(Number));
        Assert.Equal(3, functions.Count);
        Assert.All(functions, value => Assert.Equal(2,
            Assert.IsType<PdfInteger>(
                Assert.IsType<PdfDictionary>(value)[Name("FunctionType")]).Value));
    }

    [Fact]
    public void Build_SharesAReusedShadingAcrossPages()
    {
        var gradient = new PdfAxialGradient(0, 0, 100, 0, [
            new PdfGradientStop(0, new PdfRgbColor(0, 0, 0)),
            new PdfGradientStop(1, new PdfRgbColor(1, 1, 1))]);
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder().PaintShading(gradient))
            .AddPage(100, 100, new PdfContentStreamBuilder().PaintShading(gradient))
            .Build());
        int[] references = [.. Pages(document).Select(page =>
        {
            PdfDictionary resources = Assert.IsType<PdfDictionary>(page[Name("Resources")]);
            PdfDictionary shadings = Assert.IsType<PdfDictionary>(resources[Name("Shading")]);
            return Assert.IsType<PdfIndirectReference>(shadings[Name("Sh1")]).ObjectNumber;
        })];

        Assert.Equal(references[0], references[1]);
    }

    [Fact]
    public void Build_WritesDeviceGrayGradient()
    {
        var gradient = new PdfAxialGradient(0, 0, 100, 0, [
            new PdfGradientStop(0, 0.15),
            new PdfGradientStop(1, 0.85)]);
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder().PaintShading(gradient))
            .Build());
        PdfDictionary resources = Assert.IsType<PdfDictionary>(
            FirstPage(document)[Name("Resources")]);
        PdfDictionary shading = ResolveDictionary(document,
            Assert.IsType<PdfDictionary>(resources[Name("Shading")])[Name("Sh1")]);
        PdfDictionary function = Assert.IsType<PdfDictionary>(shading[Name("Function")]);

        Assert.Equal(PdfGradientColorSpace.Gray, gradient.ColorSpace);
        Assert.Equal("DeviceGray", Assert.IsType<PdfName>(
            shading[Name("ColorSpace")]).ValueAsLatin1());
        Assert.Equal([0.15], Assert.IsType<PdfArray>(function[Name("C0")]).Select(Number));
        Assert.Equal([0.85], Assert.IsType<PdfArray>(function[Name("C1")]).Select(Number));
    }

    [Fact]
    public void Build_WritesDeviceCmykGradient()
    {
        var gradient = new PdfRadialGradient(50, 50, 0, 50, 50, 50, [
            new PdfGradientStop(0, new PdfCmykColor(1, 0.2, 0, 0)),
            new PdfGradientStop(1, new PdfCmykColor(0, 0.1, 0.8, 0.15))]);
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder().PaintShading(gradient))
            .Build());
        PdfDictionary resources = Assert.IsType<PdfDictionary>(
            FirstPage(document)[Name("Resources")]);
        PdfDictionary shading = ResolveDictionary(document,
            Assert.IsType<PdfDictionary>(resources[Name("Shading")])[Name("Sh1")]);
        PdfDictionary function = Assert.IsType<PdfDictionary>(shading[Name("Function")]);

        Assert.Equal(PdfGradientColorSpace.Cmyk, gradient.ColorSpace);
        Assert.Equal("DeviceCMYK", Assert.IsType<PdfName>(
            shading[Name("ColorSpace")]).ValueAsLatin1());
        Assert.Equal([1d, 0.2, 0, 0],
            Assert.IsType<PdfArray>(function[Name("C0")]).Select(Number));
        Assert.Equal([0d, 0.1, 0.8, 0.15],
            Assert.IsType<PdfArray>(function[Name("C1")]).Select(Number));
    }

    [Fact]
    public void Build_WritesShadingBoundsAndAntialiasingPreference()
    {
        var gradient = new PdfAxialGradient(0, 0, 100, 0, [
            new PdfGradientStop(0, 0.1),
            new PdfGradientStop(1, 0.9)],
            bounds: new PdfShadingBounds(10, 20, 90, 80),
            antiAlias: false,
            background: new PdfGradientBackground(0.25));
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddPage(100, 100, new PdfContentStreamBuilder().PaintShading(gradient))
            .Build());
        PdfDictionary resources = Assert.IsType<PdfDictionary>(
            FirstPage(document)[Name("Resources")]);
        PdfDictionary shading = ResolveDictionary(document,
            Assert.IsType<PdfDictionary>(resources[Name("Shading")])[Name("Sh1")]);

        Assert.Equal([10d, 20d, 90d, 80d],
            Assert.IsType<PdfArray>(shading[Name("BBox")]).Select(Number));
        Assert.False(Assert.IsType<PdfBoolean>(shading[Name("AntiAlias")]).Value);
        Assert.Equal([0.25],
            Assert.IsType<PdfArray>(shading[Name("Background")]).Select(Number));
    }

    [Fact]
    public void Gradients_RejectInvalidStopsCoordinatesAndCircles()
    {
        PdfGradientStop black = new(0, new PdfRgbColor(0, 0, 0));
        PdfGradientStop white = new(1, new PdfRgbColor(1, 1, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfGradientStop(double.NaN, new PdfRgbColor(0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfGradientStop(0, 1.01));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfShadingBounds(0, 0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfShadingBounds(0, 0, 10, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfAxialGradient(0, 0, 10, 10, [black, white],
                bounds: default(PdfShadingBounds)));
        Assert.Throws<ArgumentException>(() =>
            new PdfAxialGradient(0, 0, 10, 10,
                [default, new PdfGradientStop(1, 1)]));
        Assert.Throws<ArgumentException>(() =>
            new PdfAxialGradient(0, 0, 10, 10, [
                new PdfGradientStop(0, 0.1),
                new PdfGradientStop(1, new PdfRgbColor(1, 1, 1))]));
        Assert.Throws<ArgumentException>(() =>
            new PdfAxialGradient(0, 0, 10, 10, [black, white],
                background: new PdfGradientBackground(0.5)));
        Assert.Throws<ArgumentException>(() =>
            new PdfAxialGradient(0, 0, 10, 10, [white, black]));
        Assert.Throws<ArgumentException>(() =>
            new PdfAxialGradient(0, 0, 10, 10, [black, black, white]));
        Assert.Throws<ArgumentException>(() =>
            new PdfAxialGradient(0, 0, 0, 0, [black, white]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfRadialGradient(0, 0, -1, 10, 10, 5, [black, white]));
        Assert.Throws<ArgumentException>(() =>
            new PdfRadialGradient(0, 0, 5, 0, 0, 5, [black, white]));
    }

    [Fact]
    public void PdfUa2Mode_RequiresShadingPaintToBeTagged()
    {
        var gradient = new PdfAxialGradient(0, 0, 100, 0, [
            new PdfGradientStop(0, new PdfRgbColor(0, 0, 0)),
            new PdfGradientStop(1, new PdfRgbColor(1, 1, 1))]);
        static PdfDocumentBuilder Ready(PdfContentStreamBuilder content) => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "Gradient", Language = "en-US" })
            .EnablePdfUa2Conformance()
            .AddPage(100, 100, content)
            .AddStructureContainer(PdfStructureType.Document);

        Assert.Throws<InvalidOperationException>(() =>
            Ready(new PdfContentStreamBuilder().PaintShading(gradient)).Build());

        byte[] tagged = Ready(new PdfContentStreamBuilder()
                .BeginMarkedContent(PdfStructureType.Figure, 0)
                .PaintShading(gradient)
                .EndMarkedContent())
            .AddStructureElement(PdfStructureType.Figure, 0, 0, 1,
                alternateDescription: "A gradient")
            .Build();
        Assert.NotEmpty(tagged);
    }

    private static PdfDictionary FirstPage(PdfDocument document) => Pages(document)[0];
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
    private static double Number(PdfObject value) => value switch
    {
        PdfInteger integer => integer.Value,
        PdfReal real => real.Value,
        _ => throw new Xunit.Sdk.XunitException("Expected a PDF number.")
    };
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
