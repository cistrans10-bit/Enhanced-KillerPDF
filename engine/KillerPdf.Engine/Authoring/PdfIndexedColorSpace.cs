namespace KillerPdf.Engine.Authoring;

/// <summary>The device color space used by an indexed palette.</summary>
public enum PdfIndexedBaseColorSpace
{
    /// <summary>One-byte grayscale palette entries.</summary>
    Gray,
    /// <summary>Three-byte red, green, and blue palette entries.</summary>
    Rgb,
    /// <summary>Four-byte cyan, magenta, yellow, and black palette entries.</summary>
    Cmyk
}

/// <summary>A compact palette whose entries use a device Gray, RGB, or CMYK base space.</summary>
public sealed class PdfIndexedColorSpace
{
    private readonly byte[] _palette;

    /// <summary>Creates a validated palette with between one and 256 complete entries.</summary>
    public PdfIndexedColorSpace(
        PdfIndexedBaseColorSpace baseColorSpace, ReadOnlyMemory<byte> palette)
    {
        if (!Enum.IsDefined(baseColorSpace))
            throw new ArgumentOutOfRangeException(nameof(baseColorSpace));
        int components = baseColorSpace switch
        {
            PdfIndexedBaseColorSpace.Gray => 1,
            PdfIndexedBaseColorSpace.Rgb => 3,
            PdfIndexedBaseColorSpace.Cmyk => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(baseColorSpace))
        };
        if (palette.Length == 0 || palette.Length % components != 0)
            throw new ArgumentException(
                $"The palette must contain complete {baseColorSpace} entries.", nameof(palette));
        int entries = palette.Length / components;
        if (entries > 256)
            throw new ArgumentException("An Indexed color space supports at most 256 entries.", nameof(palette));
        BaseColorSpace = baseColorSpace;
        ComponentCount = components;
        EntryCount = entries;
        _palette = palette.ToArray();
    }

    /// <summary>Gets the device space used by each palette entry.</summary>
    public PdfIndexedBaseColorSpace BaseColorSpace { get; }
    /// <summary>Gets the number of color components in each palette entry.</summary>
    public int ComponentCount { get; }
    /// <summary>Gets the number of palette entries.</summary>
    public int EntryCount { get; }
    /// <summary>Gets the packed palette component bytes.</summary>
    public ReadOnlyMemory<byte> Palette => _palette;
}
