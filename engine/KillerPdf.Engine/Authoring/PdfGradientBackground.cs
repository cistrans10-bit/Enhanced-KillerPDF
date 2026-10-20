namespace KillerPdf.Engine.Authoring;

/// <summary>A background color used where a bounded shading has no defined value.</summary>
public sealed class PdfGradientBackground
{
    /// <summary>Creates a normalized device-gray background.</summary>
    public PdfGradientBackground(double gray)
        : this(PdfGradientColorSpace.Gray, [Component(gray, nameof(gray))])
    {
    }

    /// <summary>Creates a device-RGB background.</summary>
    public PdfGradientBackground(PdfRgbColor color)
        : this(PdfGradientColorSpace.Rgb, [color.Red, color.Green, color.Blue])
    {
    }

    /// <summary>Creates a device-CMYK background.</summary>
    public PdfGradientBackground(PdfCmykColor color)
        : this(PdfGradientColorSpace.Cmyk,
            [color.Cyan, color.Magenta, color.Yellow, color.Black])
    {
    }

    private PdfGradientBackground(
        PdfGradientColorSpace colorSpace, IReadOnlyList<double> components)
    {
        ColorSpace = colorSpace;
        Components = components;
    }

    /// <summary>Gets the device color space used by the background.</summary>
    public PdfGradientColorSpace ColorSpace { get; }
    internal IReadOnlyList<double> Components { get; }

    private static double Component(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name);
        return value;
    }
}
