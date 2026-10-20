namespace KillerPdf.Engine.Authoring;

/// <summary>A reusable transparency-group form used as an alpha or luminosity soft mask.</summary>
public sealed record PdfSoftMask
{
    /// <summary>Creates an alpha or luminosity mask from a transparency-group form.</summary>
    public PdfSoftMask(
        PdfFormXObject group,
        PdfSoftMaskSubtype subtype = PdfSoftMaskSubtype.Alpha,
        PdfSoftMaskBackdrop? backdrop = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!Enum.IsDefined(subtype))
            throw new ArgumentOutOfRangeException(nameof(subtype));
        if (!group.IsolatedTransparencyGroup && !group.KnockoutTransparencyGroup)
            throw new ArgumentException(
                "A soft mask must be backed by a transparency-group Form XObject.", nameof(group));
        Group = group;
        Subtype = subtype;
        if (backdrop.HasValue && !backdrop.Value.IsValid)
            throw new ArgumentException("The soft-mask backdrop is not initialized.", nameof(backdrop));
        if (backdrop.HasValue && backdrop.Value.ColorSpace != group.TransparencyGroupColorSpace)
            throw new ArgumentException(
                "A soft-mask backdrop must use its transparency group's color space.",
                nameof(backdrop));
        Backdrop = backdrop;
    }

    /// <summary>Gets the transparency-group form supplying mask values.</summary>
    public PdfFormXObject Group { get; }
    /// <summary>Gets how the group is converted into mask values.</summary>
    public PdfSoftMaskSubtype Subtype { get; }
    /// <summary>Gets the optional initial backdrop color.</summary>
    public PdfSoftMaskBackdrop? Backdrop { get; }
}

/// <summary>Methods for deriving soft-mask values from a transparency group.</summary>
public enum PdfSoftMaskSubtype
{
    /// <summary>Uses the group's alpha values directly.</summary>
    Alpha,
    /// <summary>Converts group colors to luminosity values.</summary>
    Luminosity
}

/// <summary>A device-color backdrop used while evaluating a soft mask.</summary>
public readonly record struct PdfSoftMaskBackdrop
{
    /// <summary>Creates a DeviceGray backdrop.</summary>
    public PdfSoftMaskBackdrop(double gray)
    {
        Validate(gray, nameof(gray));
        ColorSpace = PdfTransparencyGroupColorSpace.Gray;
        Gray = gray;
    }

    /// <summary>Creates a DeviceRGB backdrop.</summary>
    public PdfSoftMaskBackdrop(PdfRgbColor color)
    {
        ColorSpace = PdfTransparencyGroupColorSpace.Rgb;
        Rgb = color;
    }

    /// <summary>Creates a DeviceCMYK backdrop.</summary>
    public PdfSoftMaskBackdrop(PdfCmykColor color)
    {
        ColorSpace = PdfTransparencyGroupColorSpace.Cmyk;
        Cmyk = color;
    }

    /// <summary>Gets the backdrop device color space.</summary>
    public PdfTransparencyGroupColorSpace ColorSpace { get; }
    /// <summary>Gets the grayscale component when applicable.</summary>
    public double? Gray { get; }
    /// <summary>Gets the RGB color when applicable.</summary>
    public PdfRgbColor? Rgb { get; }
    /// <summary>Gets the CMYK color when applicable.</summary>
    public PdfCmykColor? Cmyk { get; }

    internal IReadOnlyList<double> Components => ColorSpace switch
    {
        PdfTransparencyGroupColorSpace.Gray => [Gray!.Value],
        PdfTransparencyGroupColorSpace.Rgb =>
            [Rgb!.Value.Red, Rgb.Value.Green, Rgb.Value.Blue],
        PdfTransparencyGroupColorSpace.Cmyk =>
            [Cmyk!.Value.Cyan, Cmyk.Value.Magenta, Cmyk.Value.Yellow, Cmyk.Value.Black],
        _ => throw new InvalidOperationException()
    };
    internal bool IsValid => Gray.HasValue || Rgb.HasValue || Cmyk.HasValue;

    private static void Validate(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name);
    }
}
