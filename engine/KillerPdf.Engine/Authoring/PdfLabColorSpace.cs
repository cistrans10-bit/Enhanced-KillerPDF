namespace KillerPdf.Engine.Authoring;

/// <summary>A CIE L*a*b* color space with explicit white point and component ranges.</summary>
public sealed class PdfLabColorSpace
{
    /// <summary>Creates a Lab color space from white point, black point, and component ranges.</summary>
    public PdfLabColorSpace(
        double whiteX = 0.9642, double whiteY = 1, double whiteZ = 0.8249,
        double blackX = 0, double blackY = 0, double blackZ = 0,
        double minimumA = -128, double maximumA = 127,
        double minimumB = -128, double maximumB = 127)
    {
        WhiteX = Positive(whiteX, nameof(whiteX));
        if (whiteY != 1)
            throw new ArgumentOutOfRangeException(nameof(whiteY),
                "A PDF Lab white point must normalize Y to 1.0.");
        WhiteY = whiteY;
        WhiteZ = Positive(whiteZ, nameof(whiteZ));
        BlackX = Nonnegative(blackX, nameof(blackX));
        BlackY = Nonnegative(blackY, nameof(blackY));
        BlackZ = Nonnegative(blackZ, nameof(blackZ));
        if (!double.IsFinite(minimumA) || !double.IsFinite(maximumA) || minimumA >= maximumA)
            throw new ArgumentException("The Lab a* range must be finite and increasing.");
        if (!double.IsFinite(minimumB) || !double.IsFinite(maximumB) || minimumB >= maximumB)
            throw new ArgumentException("The Lab b* range must be finite and increasing.");
        MinimumA = minimumA;
        MaximumA = maximumA;
        MinimumB = minimumB;
        MaximumB = maximumB;
    }

    /// <summary>Gets the white-point X tristimulus value.</summary>
    public double WhiteX { get; }
    /// <summary>Gets the normalized white-point Y tristimulus value.</summary>
    public double WhiteY { get; }
    /// <summary>Gets the white-point Z tristimulus value.</summary>
    public double WhiteZ { get; }
    /// <summary>Gets the black-point X tristimulus value.</summary>
    public double BlackX { get; }
    /// <summary>Gets the black-point Y tristimulus value.</summary>
    public double BlackY { get; }
    /// <summary>Gets the black-point Z tristimulus value.</summary>
    public double BlackZ { get; }
    /// <summary>Gets the minimum a* component.</summary>
    public double MinimumA { get; }
    /// <summary>Gets the maximum a* component.</summary>
    public double MaximumA { get; }
    /// <summary>Gets the minimum b* component.</summary>
    public double MinimumB { get; }
    /// <summary>Gets the maximum b* component.</summary>
    public double MaximumB { get; }

    private static double Positive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(name);
        return value;
    }
    private static double Nonnegative(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
        return value;
    }
}
