namespace KillerPdf.Engine.Authoring;

/// <summary>The page-space window used to display an annotation's text.</summary>
public sealed record PdfAnnotationPopup
{
    /// <summary>Creates a popup window with a positive size and optional initial open state.</summary>
    public PdfAnnotationPopup(double x, double y, double width, double height, bool open = false)
    {
        if (!double.IsFinite(x)) throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(y));
        if (!double.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Open = open;
    }

    /// <summary>Gets the horizontal coordinate of the popup's lower-left corner.</summary>
    public double X { get; }
    /// <summary>Gets the vertical coordinate of the popup's lower-left corner.</summary>
    public double Y { get; }
    /// <summary>Gets the positive popup width.</summary>
    public double Width { get; }
    /// <summary>Gets the positive popup height.</summary>
    public double Height { get; }
    /// <summary>Gets whether the popup is initially open.</summary>
    public bool Open { get; }
}
