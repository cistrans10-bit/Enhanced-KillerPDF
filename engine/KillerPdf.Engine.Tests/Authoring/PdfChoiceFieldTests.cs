using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfChoiceFieldTests
{
    [Fact]
    public void ChoiceFields_WriteCustomVisualStyle()
    {
        var style = new PdfFormFieldAppearanceStyle
        {
            BackgroundColor = new PdfRgbColor(0.95, 0.9, 0.8),
            BorderColor = new PdfRgbColor(0.3, 0.2, 0.1),
            TextColor = new PdfRgbColor(0.2, 0.4, 0.6),
            BorderWidth = 2
        };
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddComboBox(0, "styled", 0, 0, 160, 24, ["Alpha", "Beta"],
                choiceOptions: new PdfChoiceFieldOptions { AppearanceStyle = style })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.True(field.ContainsKey(Name("MK")));
        Assert.True(field.ContainsKey(Name("BS")));
        Assert.Contains("0.95 0.9 0.8 rg", content);
        Assert.Contains("0.3 0.2 0.1 RG", content);
        Assert.Contains("0.2 0.4 0.6 rg", content);
        Assert.Contains("2 w", content);
    }

    [Fact]
    public void ChoiceFields_WriteIndependentDefaultSelectionsInOptionOrder()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddComboBoxOptions(0, "combo", 0, 0, 120, 20,
                [new PdfChoiceOption("a", "Alpha"), new PdfChoiceOption("b", "Beta")], "b",
                choiceOptions: new PdfChoiceFieldOptions
                {
                    DefaultSelectedExportValues = ["a"]
                })
            .AddMultiSelectListBoxOptions(0, "list", 0, 30, 120, 60,
                [new PdfChoiceOption("a", "Alpha"), new PdfChoiceOption("b", "Beta"),
                 new PdfChoiceOption("c", "Gamma")], ["a"],
                choiceOptions: new PdfChoiceFieldOptions
                {
                    DefaultSelectedExportValues = ["c", "b"]
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfArray fields = Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")]);
        PdfDictionary combo = ResolveDictionary(document, fields[0]);
        PdfDictionary list = ResolveDictionary(document, fields[1]);

        Assert.Equal("b", DecodeUnicode(Assert.IsType<PdfString>(combo[Name("V")])));
        Assert.Equal("a", DecodeUnicode(Assert.IsType<PdfString>(combo[Name("DV")])));
        Assert.Equal(["b", "c"], Assert.IsType<PdfArray>(list[Name("DV")])
            .Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
    }

    [Fact]
    public void ChoiceFields_ValidateIndependentDefaultSelections()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();

        Assert.Throws<ArgumentException>(() => builder.AddListBox(
            0, "single", 0, 0, 100, 40, ["A", "B"],
            choiceOptions: new PdfChoiceFieldOptions
            {
                DefaultSelectedExportValues = ["A", "B"]
            }));
        Assert.Throws<ArgumentException>(() => builder.AddMultiSelectListBox(
            0, "multiple", 0, 0, 100, 40, ["A", "B"],
            choiceOptions: new PdfChoiceFieldOptions
            {
                DefaultSelectedExportValues = ["Missing"]
            }));
    }

    [Fact]
    public void ChoiceFields_RejectInvalidAlignmentWhenAdded()
    {
        var invalid = new PdfChoiceFieldOptions
        {
            Alignment = (PdfTextFieldAlignment)999
        };
        var builder = new PdfDocumentBuilder().AddBlankPage();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddComboBox(
            0, "combo", 0, 0, 100, 20, ["One"], choiceOptions: invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddListBoxOptions(
            0, "list", 0, 30, 100, 40, [new PdfChoiceOption("one", "One")],
            choiceOptions: invalid));
    }

    [Theory]
    [InlineData(false, 1 << 17)]
    [InlineData(true, (1 << 17) | (1 << 18))]
    public void AddComboBox_WritesOptionsValueFlagsAndAppearance(bool editable, int expectedFlags)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddComboBox(0, "theme", 72, 650, 180, 24,
                ["Dark", "Mourning", "98SE"], "Mourning", editable)
            .Build());
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        var field = ResolveDictionary(document, Assert.IsType<PdfArray>(acroForm[Name("Fields")])[0]);
        var options = Assert.IsType<PdfArray>(field[Name("Opt")]);
        var appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));

        Assert.Equal("Ch", Assert.IsType<PdfName>(field[Name("FT")]).ValueAsLatin1());
        Assert.Equal(expectedFlags, Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
        Assert.Equal("Mourning", DecodeUnicode(Assert.IsType<PdfString>(field[Name("V")])));
        Assert.Equal("Mourning", DecodeUnicode(Assert.IsType<PdfString>(field[Name("DV")])));
        Assert.Equal(["Dark", "Mourning", "98SE"],
            options.Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
        Assert.Contains("(Mourning) Tj", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void AddComboBox_ValidatesOptionsAndNonEditableSelection()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentException>(() => builder.AddComboBox(
            0, "empty", 0, 0, 100, 20, []));
        Assert.Throws<ArgumentException>(() => builder.AddComboBox(
            0, "duplicate", 0, 0, 100, 20, ["A", "A"]));
        Assert.Throws<ArgumentException>(() => builder.AddComboBox(
            0, "selection", 0, 0, 100, 20, ["A", "B"], "C"));
    }

    [Fact]
    public void AddListBox_WritesChoiceFieldWithoutComboFlag()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddListBox(0, "theme", 72, 600, 180, 72,
                ["Dark", "Mourning", "98SE"], "98SE",
                fieldMetadata: new PdfFormFieldMetadata { Tooltip = "Theme list" },
                fieldOptions: new PdfFormFieldOptions { Required = true })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);

        Assert.Equal("Ch", Assert.IsType<PdfName>(field[Name("FT")]).ValueAsLatin1());
        Assert.Equal(1 << 1, Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
        Assert.Equal("98SE", DecodeUnicode(Assert.IsType<PdfString>(field[Name("V")])));
        Assert.Equal("Theme list", DecodeUnicode(Assert.IsType<PdfString>(field[Name("TU")])));
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);
        Assert.Contains("(Dark) Tj", content);
        Assert.Contains("(Mourning) Tj", content);
        Assert.Contains("(98SE) Tj", content);
    }

    [Fact]
    public void AddListBox_ValidatesOptionsAndSelection()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentException>(() => builder.AddListBox(0, "empty", 0, 0, 100, 40, []));
        Assert.Throws<ArgumentException>(() => builder.AddListBox(0, "duplicate", 0, 0, 100, 40, ["A", "A"]));
        Assert.Throws<ArgumentException>(() => builder.AddListBox(0, "selection", 0, 0, 100, 40, ["A", "B"], "C"));
    }

    [Fact]
    public void AddMultiSelectListBox_WritesOrderedValuesIndicesAndHighlights()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddMultiSelectListBox(0, "features", 20, 20, 160, 80,
                ["Links", "Forms", "PDF/A", "PDF/UA"], ["PDF/UA", "Forms"])
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfArray values = Assert.IsType<PdfArray>(field[Name("V")]);
        PdfArray defaultValues = Assert.IsType<PdfArray>(field[Name("DV")]);
        PdfArray indices = Assert.IsType<PdfArray>(field[Name("I")]);

        Assert.Equal(1 << 21, Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
        Assert.Equal(["Forms", "PDF/UA"],
            values.Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
        Assert.Equal(["Forms", "PDF/UA"],
            defaultValues.Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
        Assert.Equal([1L, 3L], indices.Select(value => Assert.IsType<PdfInteger>(value).Value));
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        Assert.Equal(2, Encoding.ASCII.GetString(appearance.EncodedData.Span)
            .Split("0.75 g", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void AddMultiSelectListBox_RejectsInvalidSelections()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentException>(() => builder.AddMultiSelectListBox(
            0, "duplicate", 0, 0, 100, 40, ["A", "B"], ["A", "A"]));
        Assert.Throws<ArgumentException>(() => builder.AddMultiSelectListBox(
            0, "unknown", 0, 0, 100, 40, ["A", "B"], ["C"]));
    }

    [Fact]
    public void AddListBox_WritesAndRendersFromTopIndex()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddListBox(0, "scrolled", 10, 10, 100, 40,
                ["A", "B", "C", "D"], "C", topIndex: 2)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        Assert.Equal(2, Assert.IsType<PdfInteger>(field[Name("TI")]).Value);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);
        Assert.DoesNotContain("(A) Tj", content);
        Assert.DoesNotContain("(B) Tj", content);
        Assert.Contains("(C) Tj", content);
        Assert.Contains("(D) Tj", content);
    }

    [Fact]
    public void AddListBox_RejectsTopIndexOutsideOptions()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddListBox(
            0, "negative", 0, 0, 100, 40, ["A"], topIndex: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddMultiSelectListBox(
            0, "past-end", 0, 0, 100, 40, ["A"], topIndex: 1));
    }

    [Fact]
    public void ChoiceFields_WriteTypedBehaviorAndSortedOptions()
    {
        var behavior = new PdfChoiceFieldOptions
        {
            SortOptions = true,
            DoNotSpellCheck = true,
            CommitOnSelectionChange = true
        };
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddComboBox(0, "combo", 0, 0, 100, 20, ["Zulu", "Alpha"], "Zulu",
                choiceOptions: behavior)
            .AddMultiSelectListBox(0, "list", 0, 30, 100, 60,
                ["Zulu", "Alpha", "Mike"], ["Zulu", "Alpha"], choiceOptions: behavior)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfArray fields = Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")]);
        int behaviorFlags = (1 << 19) | (1 << 22) | (1 << 26);
        PdfDictionary combo = ResolveDictionary(document, fields[0]);
        PdfDictionary list = ResolveDictionary(document, fields[1]);

        Assert.Equal((1 << 17) | behaviorFlags,
            Assert.IsType<PdfInteger>(combo[Name("Ff")]).Value);
        Assert.Equal((1 << 21) | behaviorFlags,
            Assert.IsType<PdfInteger>(list[Name("Ff")]).Value);
        Assert.Equal(["Alpha", "Zulu"], Assert.IsType<PdfArray>(combo[Name("Opt")])
            .Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
        Assert.Equal([0L, 2L], Assert.IsType<PdfArray>(list[Name("I")])
            .Select(value => Assert.IsType<PdfInteger>(value).Value));
    }

    [Fact]
    public void AddComboBox_WritesSeparateExportAndDisplayValues()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddComboBoxOptions(0, "country", 0, 0, 140, 20,
                [new PdfChoiceOption("US", "United States"),
                 new PdfChoiceOption("CA", "Canada")], "US")
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfArray options = Assert.IsType<PdfArray>(field[Name("Opt")]);
        PdfArray first = Assert.IsType<PdfArray>(options[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));

        Assert.Equal("US", DecodeUnicode(Assert.IsType<PdfString>(first[0])));
        Assert.Equal("United States", DecodeUnicode(Assert.IsType<PdfString>(first[1])));
        Assert.Equal("US", DecodeUnicode(Assert.IsType<PdfString>(field[Name("V")])));
        Assert.Contains("(United States) Tj", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void ListBoxes_WriteExportValuesAndDisplayLabels()
    {
        PdfChoiceOption[] options =
        [
            new("A", "Alpha label"),
            new("B", "Beta label"),
            new("C", "Gamma label")
        ];
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddListBoxOptions(0, "single", 0, 0, 140, 50, options, "B")
            .AddMultiSelectListBoxOptions(0, "multiple", 0, 60, 140, 60,
                options, ["A", "C"])
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfArray fields = Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")]);
        PdfDictionary single = ResolveDictionary(document, fields[0]);
        PdfDictionary multiple = ResolveDictionary(document, fields[1]);

        Assert.Equal("B", DecodeUnicode(Assert.IsType<PdfString>(single[Name("V")])));
        Assert.Equal(["A", "C"], Assert.IsType<PdfArray>(multiple[Name("V")])
            .Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
        Assert.Equal([0L, 2L], Assert.IsType<PdfArray>(multiple[Name("I")])
            .Select(value => Assert.IsType<PdfInteger>(value).Value));
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(multiple[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);
        Assert.Contains("(Alpha label) Tj", content);
        Assert.Contains("(Gamma label) Tj", content);
        Assert.DoesNotContain("(A) Tj", content);
    }

    [Theory]
    [InlineData(PdfTextFieldAlignment.Center, 1)]
    [InlineData(PdfTextFieldAlignment.Right, 2)]
    public void ChoiceFields_WriteAlignmentAndMoveDisplayLabels(
        PdfTextFieldAlignment alignment, int expectedQuadding)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddComboBoxOptions(0, "aligned", 0, 0, 180, 20,
                [new PdfChoiceOption("value", "Display label")], "value",
                choiceOptions: new PdfChoiceFieldOptions { Alignment = alignment })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document,
            Assert.IsType<PdfArray>(Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Equal(expectedQuadding, Assert.IsType<PdfInteger>(field[Name("Q")]).Value);
        Assert.Contains("(Display label) Tj", content);
        Assert.DoesNotContain("3 4 Td", content);
    }

    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
