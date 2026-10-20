namespace KillerPdf.Engine.Authoring;

/// <summary>Reusable transparency, blending, overprint, and compositing settings.</summary>
public sealed record PdfGraphicsState
{
    /// <summary>Creates validated transparency, blending, overprint, and soft-mask settings.</summary>
    public PdfGraphicsState(
        double fillOpacity = 1,
        double strokeOpacity = 1,
        PdfBlendMode blendMode = PdfBlendMode.Normal,
        bool fillOverprint = false,
        bool strokeOverprint = false,
        PdfOverprintMode overprintMode = PdfOverprintMode.Zero,
        bool alphaIsShape = false,
        bool textKnockout = true,
        PdfSoftMask? softMask = null)
    {
        ValidateOpacity(fillOpacity, nameof(fillOpacity));
        ValidateOpacity(strokeOpacity, nameof(strokeOpacity));
        if (!Enum.IsDefined(blendMode))
            throw new ArgumentOutOfRangeException(nameof(blendMode));
        if (!Enum.IsDefined(overprintMode))
            throw new ArgumentOutOfRangeException(nameof(overprintMode));
        FillOpacity = fillOpacity;
        StrokeOpacity = strokeOpacity;
        BlendMode = blendMode;
        FillOverprint = fillOverprint;
        StrokeOverprint = strokeOverprint;
        OverprintMode = overprintMode;
        AlphaIsShape = alphaIsShape;
        TextKnockout = textKnockout;
        SoftMask = softMask;
    }

    /// <summary>Gets the nonstroking opacity from zero through one.</summary>
    public double FillOpacity { get; }
    /// <summary>Gets the stroking opacity from zero through one.</summary>
    public double StrokeOpacity { get; }
    /// <summary>Gets the transparency blend mode.</summary>
    public PdfBlendMode BlendMode { get; }
    /// <summary>Gets whether nonstroking operations use overprint.</summary>
    public bool FillOverprint { get; }
    /// <summary>Gets whether stroking operations use overprint.</summary>
    public bool StrokeOverprint { get; }
    /// <summary>Gets the overprint interpretation mode.</summary>
    public PdfOverprintMode OverprintMode { get; }
    /// <summary>Gets whether the alpha source is interpreted as shape rather than opacity.</summary>
    public bool AlphaIsShape { get; }
    /// <summary>Gets whether text glyphs knock out underlying glyphs in the same text object.</summary>
    public bool TextKnockout { get; }
    /// <summary>Gets the optional alpha or luminosity soft mask.</summary>
    public PdfSoftMask? SoftMask { get; }

    private static void ValidateOpacity(double value, string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(name,
                "Graphics-state opacity must be between zero and one.");
    }
}

/// <summary>Overprint-mode interpretation for zero-valued process colorants.</summary>
public enum PdfOverprintMode
{
    /// <summary>Erases zero-valued process colorants when overprinting.</summary>
    Zero = 0,
    /// <summary>Preserves zero-valued process colorants when overprinting.</summary>
    One = 1
}
