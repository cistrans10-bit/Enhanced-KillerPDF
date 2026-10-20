namespace KillerPdf.Engine.Authoring;

/// <summary>Standard border styles for link annotations.</summary>
public enum PdfLinkBorderStyle
{
    /// <summary>A solid border.</summary>
    Solid,
    /// <summary>A dashed border.</summary>
    Dashed,
    /// <summary>A simulated raised border.</summary>
    Beveled,
    /// <summary>A simulated inset border.</summary>
    Inset,
    /// <summary>An underline along the link's lower edge.</summary>
    Underline
}
/// <summary>Visual feedback shown while a link is activated.</summary>
public enum PdfLinkHighlightMode
{
    /// <summary>No activation highlight.</summary>
    None,
    /// <summary>Inverts the link region.</summary>
    Invert,
    /// <summary>Inverts the link border.</summary>
    Outline,
    /// <summary>Displays the link as if pushed into the page.</summary>
    Push
}

/// <summary>Border geometry, color, and activation feedback for a link annotation.</summary>
public sealed class PdfLinkAppearance
{
    /// <summary>Creates a validated link appearance.</summary>
    public PdfLinkAppearance(
        double borderWidth = 0,
        PdfLinkBorderStyle borderStyle = PdfLinkBorderStyle.Solid,
        IReadOnlyList<double>? dashPattern = null,
        PdfRgbColor? color = null,
        PdfLinkHighlightMode highlightMode = PdfLinkHighlightMode.Invert,
        double horizontalCornerRadius = 0,
        double verticalCornerRadius = 0)
    {
        if (!double.IsFinite(borderWidth) || borderWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(borderWidth));
        if (!Enum.IsDefined(borderStyle)) throw new ArgumentOutOfRangeException(nameof(borderStyle));
        if (!Enum.IsDefined(highlightMode)) throw new ArgumentOutOfRangeException(nameof(highlightMode));
        if (!double.IsFinite(horizontalCornerRadius) || horizontalCornerRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(horizontalCornerRadius));
        if (!double.IsFinite(verticalCornerRadius) || verticalCornerRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(verticalCornerRadius));
        dashPattern ??= [3];
        if (dashPattern.Any(value => !double.IsFinite(value) || value < 0))
            throw new ArgumentOutOfRangeException(nameof(dashPattern));
        if (dashPattern.Count == 0 || dashPattern.All(value => value == 0))
            throw new ArgumentException("A link dash pattern requires a nonzero length.", nameof(dashPattern));
        if (borderStyle != PdfLinkBorderStyle.Dashed && dashPattern is not [3])
            throw new ArgumentException(
                "A dash pattern can only be used with a dashed link border.", nameof(dashPattern));

        BorderWidth = borderWidth;
        BorderStyle = borderStyle;
        DashPattern = [.. dashPattern];
        Color = color;
        HighlightMode = highlightMode;
        HorizontalCornerRadius = horizontalCornerRadius;
        VerticalCornerRadius = verticalCornerRadius;
    }

    /// <summary>Gets the nonnegative border width.</summary>
    public double BorderWidth { get; }
    /// <summary>Gets the border style.</summary>
    public PdfLinkBorderStyle BorderStyle { get; }
    /// <summary>Gets the positive dashed-border pattern.</summary>
    public IReadOnlyList<double> DashPattern { get; }
    /// <summary>Gets the optional RGB border color.</summary>
    public PdfRgbColor? Color { get; }
    /// <summary>Gets the activation highlight mode.</summary>
    public PdfLinkHighlightMode HighlightMode { get; }
    /// <summary>Gets the horizontal border-corner radius.</summary>
    public double HorizontalCornerRadius { get; }
    /// <summary>Gets the vertical border-corner radius.</summary>
    public double VerticalCornerRadius { get; }
}
