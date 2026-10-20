namespace KillerPdf.Engine.Authoring;

/// <summary>Places one selectable widget and export value in a radio-button group.</summary>
/// <param name="PageIndex">The zero-based page index.</param>
/// <param name="X">The horizontal coordinate of the widget's lower-left corner.</param>
/// <param name="Y">The vertical coordinate of the widget's lower-left corner.</param>
/// <param name="Width">The positive widget width.</param>
/// <param name="Height">The positive widget height.</param>
/// <param name="ExportValue">The value exported when this option is selected.</param>
public sealed record PdfRadioButtonOption(
    int PageIndex,
    double X,
    double Y,
    double Width,
    double Height,
    string ExportValue);
