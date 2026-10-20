using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfListBoxOverflowTests
{
#pragma warning disable SYSLIB1045 // VS IntelliSense does not materialize GeneratedRegex implementations in this project.
    private static readonly Regex SelectionHighlightPattern =
        new(@"0\.75 g\s+1 (-?[\d.]+) ", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TextOriginPattern =
        new(@"(-?[\d.]+) (-?[\d.]+) Td", RegexOptions.Compiled | RegexOptions.CultureInvariant);
#pragma warning restore SYSLIB1045

    // Four 12pt options need roughly 58 points of rows. In a 48 point box the last
    // rows fall below the field, and the appearance stream's clip path is what keeps
    // them out of the drawn result. Rows must stay evenly spaced: clamping an
    // overflowing row up to the bottom edge leaves it a few points from the row above
    // and their glyphs overlap.
    [Fact]
    public void RowsStayEvenlySpacedWhenOptionsOverflowTheField()
    {
        double[] origins = TextOrigins(ListBoxAppearance(height: 48));

        Assert.True(origins.Length >= 3, "Expected at least three rows to be written.");
        double spacing = origins[0] - origins[1];
        for (int index = 1; index < origins.Length; index++)
        {
            double gap = origins[index - 1] - origins[index];
            Assert.True(Math.Abs(gap - spacing) < 0.01,
                $"Row {index} sits {gap} below row {index - 1}, but the rows above it are " +
                $"{spacing} apart. An overflowing row must keep its true position so the clip " +
                "path can discard it, rather than being pinned to the bottom of the field.");
        }
    }

    // The selection highlight is subject to the same clamp and must move with its row.
    [Fact]
    public void SelectionHighlightTracksItsOwnRow()
    {
        string content = ListBoxAppearance(height: 48, selected: "Four");
        Match highlight = SelectionHighlightPattern.Match(content);

        Assert.True(highlight.Success, "No selection highlight was written.");
        Assert.True(Number(highlight.Groups[1].Value) < 1,
            "The highlight for the last of four options in a 48 point box should sit below " +
            "the field, where the clip removes it, rather than being pinned to the bottom edge.");
    }

    // A field whose options all fit must be unaffected.
    [Fact]
    public void FieldsThatFitAreUnchanged()
    {
        double[] origins = TextOrigins(ListBoxAppearance(height: 120));

        Assert.Equal(4, origins.Length);
        Assert.All(origins, origin => Assert.True(origin > 0));
    }

    private static string ListBoxAppearance(double height, string? selected = "Two")
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddListBox(0, "rows", 0, 0, 200, height,
                ["One", "Two", "Three", "Four"], selectedValue: selected)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        return Encoding.ASCII.GetString(appearance.EncodedData.Span);
    }

    private static double[] TextOrigins(string content) =>
        [.. TextOriginPattern.Matches(content)
            .Select(match => Number(match.Groups[2].Value))];

    private static double Number(string value) =>
        double.Parse(value, CultureInfo.InvariantCulture);

    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
