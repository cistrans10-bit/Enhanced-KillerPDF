using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Editing;
using Xunit;

namespace KillerPdf.Engine.Tests.Documents;

public sealed class PdfFormWidgetReaderTests
{
    [Fact]
    public void ReadPage_ExposesInheritedNamesFlagsValuesGeometryAndOptions()
    {
        byte[] authored = new PdfDocumentBuilder()
            .AddBlankPage(300, 400).AddBlankPage(500, 600)
            .AddTextField(0, "customer.name", 20, 300, 180, 24, "Steve", 11,
                new PdfTextFieldOptions
                {
                    ReadOnly = true,
                    Multiline = true,
                    MaximumLength = 80
                }, appearanceStyle: new PdfFormFieldAppearanceStyle
                {
                    BackgroundColor = new PdfRgbColor(0.9, 0.8, 0.7),
                    BorderColor = new PdfRgbColor(0.1, 0.2, 0.3)
                })
            .AddCheckBox(0, "approved", 20, 250, 18, 18, true, "Accepted")
            .AddRadioGroup("priority",
            [
                new PdfRadioButtonOption(0, 20, 210, 18, 18, "High"),
                new PdfRadioButtonOption(1, 20, 510, 18, 18, "Low")
            ], "High")
            .AddComboBoxOptions(0, "region", 20, 160, 140, 24,
            [
                new PdfChoiceOption("us", "United States"),
                new PdfChoiceOption("ca", "Canada")
            ], "ca")
            .AddMultiSelectListBox(0, "formats", 20, 100, 140, 48,
                ["PDF", "PDF/A", "PDF/UA"], ["PDF/A", "PDF/UA"])
            .Build();
        byte[] source = new PdfIncrementalPageEditor(PdfDocument.Open(authored))
            .SetCropBox(0, 10, 15, 280, 370)
            .SetRotation(0, 90)
            .Build();
        PdfDocument document = PdfDocument.Open(source);

        IReadOnlyList<PdfFormWidgetInfo> widgets = PdfFormWidgetReader.ReadPage(document, 0);

        Assert.Equal(5, widgets.Count);
        PdfFormWidgetInfo text = widgets.Single(widget => widget.FieldName == "customer.name");
        Assert.Equal(PdfFormFieldKind.Text, text.FieldKind);
        Assert.Equal("Steve", text.Value);
        Assert.NotEqual(0, text.Flags & 1);
        Assert.NotEqual(0, text.Flags & 4096);
        Assert.Equal(80, text.MaximumLength);
        Assert.Contains("11", text.DefaultAppearance);
        Assert.Equal(new PdfRgbColor(0.9, 0.8, 0.7), text.BackgroundColor);
        Assert.Equal(new PdfRgbColor(0.1, 0.2, 0.3), text.BorderColor);
        Assert.Equal((20d, 300d, 200d, 324d),
            (text.Left, text.Bottom, text.Right, text.Top));
        Assert.Equal((10d, 15d, 280d, 370d, 90),
            (text.PageBoxLeft, text.PageBoxBottom, text.PageBoxWidth,
                text.PageBoxHeight, text.PageRotation));
        Assert.True(text.ObjectNumber > 0);

        PdfFormWidgetInfo check = widgets.Single(widget => widget.FieldName == "approved");
        Assert.Equal("/Accepted", check.Value);
        Assert.Equal("/Accepted", check.OnValue);

        PdfFormWidgetInfo radio = widgets.Single(widget => widget.FieldName == "priority");
        Assert.Equal("/High", radio.Value);
        Assert.Equal("/High", radio.OnValue);

        PdfFormWidgetInfo choice = widgets.Single(widget => widget.FieldName == "region");
        Assert.Equal(PdfFormFieldKind.Choice, choice.FieldKind);
        Assert.Equal("ca", choice.Value);
        Assert.Equal([("us", "United States"), ("ca", "Canada")],
            choice.Options.Select(option => (option.ExportValue, option.DisplayValue)));
        PdfFormWidgetInfo multiple = widgets.Single(widget => widget.FieldName == "formats");
        Assert.Equal("PDF/A", multiple.Value);
        Assert.Equal(["PDF/A", "PDF/UA"], multiple.Values);
        Assert.Equal([0, 1, 2, 3, 4], widgets.Select(widget => widget.AnnotationIndex));
    }

    [Fact]
    public void ReadPage_ReturnsEmptyListWithoutWidgetsAndValidatesPageIndex()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder().AddBlankPage().Build());

        Assert.Empty(PdfFormWidgetReader.ReadPage(document, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfFormWidgetReader.ReadPage(document, 1));
    }
}
