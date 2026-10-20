namespace KillerPdf.Engine.Authoring;

/// <summary>A standard color-rendering intent for mapping source colors to an output device.</summary>
public enum PdfRenderingIntent
{
    /// <summary>Preserves measured colors relative to the device's absolute white point.</summary>
    AbsoluteColorimetric,
    /// <summary>Preserves in-gamut colors relative to the destination white point.</summary>
    RelativeColorimetric,
    /// <summary>Prioritizes color saturation.</summary>
    Saturation,
    /// <summary>Preserves the overall visual relationship among colors.</summary>
    Perceptual
}

internal static class PdfRenderingIntentNames
{
    internal static string Name(PdfRenderingIntent intent) => intent switch
    {
        PdfRenderingIntent.AbsoluteColorimetric => "AbsoluteColorimetric",
        PdfRenderingIntent.RelativeColorimetric => "RelativeColorimetric",
        PdfRenderingIntent.Saturation => "Saturation",
        PdfRenderingIntent.Perceptual => "Perceptual",
        _ => throw new ArgumentOutOfRangeException(nameof(intent))
    };
}
