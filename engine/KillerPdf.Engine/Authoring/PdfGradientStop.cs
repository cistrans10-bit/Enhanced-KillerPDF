namespace KillerPdf.Engine.Authoring;

/// <summary>A color positioned from zero to one along a gradient axis.</summary>
public readonly record struct PdfGradientStop
{
    /// <summary>Creates an RGB gradient stop at an offset from zero through one.</summary>
    public PdfGradientStop(double offset, PdfRgbColor color)
        : this(offset, PdfGradientColorSpace.Rgb, [color.Red, color.Green, color.Blue])
    {
        RgbColor = color;
    }

    /// <summary>Creates a grayscale gradient stop at an offset from zero through one.</summary>
    public PdfGradientStop(double offset, double gray)
        : this(offset, PdfGradientColorSpace.Gray, [Component(gray, nameof(gray))])
    {
        Gray = gray;
    }

    /// <summary>Creates a CMYK gradient stop at an offset from zero through one.</summary>
    public PdfGradientStop(double offset, PdfCmykColor color)
        : this(offset, PdfGradientColorSpace.Cmyk,
            [color.Cyan, color.Magenta, color.Yellow, color.Black])
    {
        CmykColor = color;
    }

    private PdfGradientStop(
        double offset, PdfGradientColorSpace colorSpace, IReadOnlyList<double> components)
    {
        if (!double.IsFinite(offset) || offset is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(offset),
                "A gradient-stop offset must be between zero and one.");
        Offset = offset;
        ColorSpace = colorSpace;
        Components = components;
    }

    /// <summary>Gets the normalized position along the gradient axis.</summary>
    public double Offset { get; }
    /// <summary>The RGB value for an RGB stop.</summary>
    /// <exception cref="InvalidOperationException">The stop uses Gray or CMYK.</exception>
    public PdfRgbColor Color => RgbColor ?? throw new InvalidOperationException(
        "This gradient stop does not use the RGB color space.");
    /// <summary>Gets the RGB value when this is an RGB stop.</summary>
    public PdfRgbColor? RgbColor { get; }
    /// <summary>Gets the gray component when this is a grayscale stop.</summary>
    public double? Gray { get; }
    /// <summary>Gets the CMYK value when this is a CMYK stop.</summary>
    public PdfCmykColor? CmykColor { get; }
    /// <summary>Gets the device color space used by the stop.</summary>
    public PdfGradientColorSpace ColorSpace { get; }
    internal IReadOnlyList<double> Components { get; }

    private static double Component(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name);
        return value;
    }
}

/// <summary>Device color spaces supported by gradient stops.</summary>
public enum PdfGradientColorSpace
{
    /// <summary>DeviceGray.</summary>
    Gray,
    /// <summary>DeviceRGB.</summary>
    Rgb,
    /// <summary>DeviceCMYK.</summary>
    Cmyk
}
