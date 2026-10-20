namespace KillerPdf.Engine.Authoring;

/// <summary>Standard separable and non-separable PDF blend modes.</summary>
public enum PdfBlendMode
{
    /// <summary>Uses the source color without a special blend operation.</summary>
    Normal,
    /// <summary>Multiplies source and backdrop components.</summary>
    Multiply,
    /// <summary>Applies the inverse of Multiply.</summary>
    Screen,
    /// <summary>Combines Multiply and Screen according to the backdrop.</summary>
    Overlay,
    /// <summary>Selects the darker component.</summary>
    Darken,
    /// <summary>Selects the lighter component.</summary>
    Lighten,
    /// <summary>Brightens the backdrop to reflect the source.</summary>
    ColorDodge,
    /// <summary>Darkens the backdrop to reflect the source.</summary>
    ColorBurn,
    /// <summary>Applies Multiply or Screen according to the source.</summary>
    HardLight,
    /// <summary>Softly darkens or lightens according to the source.</summary>
    SoftLight,
    /// <summary>Uses the absolute difference between source and backdrop.</summary>
    Difference,
    /// <summary>Uses a lower-contrast difference effect.</summary>
    Exclusion,
    /// <summary>Combines source hue with backdrop saturation and luminosity.</summary>
    Hue,
    /// <summary>Combines source saturation with backdrop hue and luminosity.</summary>
    Saturation,
    /// <summary>Combines source hue and saturation with backdrop luminosity.</summary>
    Color,
    /// <summary>Combines source luminosity with backdrop hue and saturation.</summary>
    Luminosity
}

internal static class PdfBlendModeNames
{
    internal static string Name(PdfBlendMode mode) => mode switch
    {
        PdfBlendMode.Normal => "Normal",
        PdfBlendMode.Multiply => "Multiply",
        PdfBlendMode.Screen => "Screen",
        PdfBlendMode.Overlay => "Overlay",
        PdfBlendMode.Darken => "Darken",
        PdfBlendMode.Lighten => "Lighten",
        PdfBlendMode.ColorDodge => "ColorDodge",
        PdfBlendMode.ColorBurn => "ColorBurn",
        PdfBlendMode.HardLight => "HardLight",
        PdfBlendMode.SoftLight => "SoftLight",
        PdfBlendMode.Difference => "Difference",
        PdfBlendMode.Exclusion => "Exclusion",
        PdfBlendMode.Hue => "Hue",
        PdfBlendMode.Saturation => "Saturation",
        PdfBlendMode.Color => "Color",
        PdfBlendMode.Luminosity => "Luminosity",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
