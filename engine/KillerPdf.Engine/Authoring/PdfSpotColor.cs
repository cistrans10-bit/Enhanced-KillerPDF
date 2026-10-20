using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Authoring;

/// <summary>A named Separation ink with a process-CMYK full-tint alternate.</summary>
public sealed class PdfSpotColor
{
    /// <summary>Creates a named spot color with a process-CMYK alternate.</summary>
    public PdfSpotColor(string name, PdfCmykColor alternateColor)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A spot-color name cannot be empty.", nameof(name));
        PdfUnicodeEncoding.EncodeUtf8(name);
        Name = name;
        AlternateColor = alternateColor;
    }

    /// <summary>Gets the nonempty separation name.</summary>
    public string Name { get; }
    /// <summary>Gets the full-tint process-CMYK alternate color.</summary>
    public PdfCmykColor AlternateColor { get; }
}
