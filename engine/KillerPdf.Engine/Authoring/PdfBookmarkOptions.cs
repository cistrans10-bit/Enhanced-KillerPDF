namespace KillerPdf.Engine.Authoring;

/// <summary>Visual emphasis applied to a bookmark title.</summary>
[Flags]
public enum PdfBookmarkStyle
{
    /// <summary>Regular text without emphasis.</summary>
    Regular = 0,
    /// <summary>Italic text.</summary>
    Italic = 1,
    /// <summary>Bold text.</summary>
    Bold = 2
}

/// <summary>Controls a bookmark's destination, appearance, and initial expansion state.</summary>
public sealed record PdfBookmarkOptions
{
    private PdfBookmarkStyle _style;

    /// <summary>Gets the title style, which may combine bold and italic emphasis.</summary>
    public PdfBookmarkStyle Style
    {
        get => _style;
        init
        {
            if ((value & ~(PdfBookmarkStyle.Italic | PdfBookmarkStyle.Bold)) != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _style = value;
        }
    }

    /// <summary>Gets the optional bookmark-title color.</summary>
    public PdfRgbColor? Color { get; init; }
    /// <summary>Gets whether the bookmark's children are initially expanded.</summary>
    public bool IsOpen { get; init; } = true;
    /// <summary>Gets the view to display after activating the bookmark.</summary>
    public PdfDestination Destination { get; init; } = PdfDestination.FitPage();
}
