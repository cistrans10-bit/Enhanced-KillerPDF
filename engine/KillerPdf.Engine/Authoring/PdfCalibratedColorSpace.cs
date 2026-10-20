namespace KillerPdf.Engine.Authoring;

/// <summary>Base type for calibrated grayscale and RGB color spaces.</summary>
public abstract class PdfCalibratedColorSpace
{
    private protected PdfCalibratedColorSpace(
        int componentCount,
        double whiteX, double whiteY, double whiteZ,
        double blackX, double blackY, double blackZ)
    {
        if (!double.IsFinite(whiteX) || whiteX <= 0) throw new ArgumentOutOfRangeException(nameof(whiteX));
        if (whiteY != 1) throw new ArgumentOutOfRangeException(nameof(whiteY),
            "A calibrated PDF white point must normalize Y to 1.0.");
        if (!double.IsFinite(whiteZ) || whiteZ <= 0) throw new ArgumentOutOfRangeException(nameof(whiteZ));
        if (!double.IsFinite(blackX) || blackX < 0) throw new ArgumentOutOfRangeException(nameof(blackX));
        if (!double.IsFinite(blackY) || blackY < 0) throw new ArgumentOutOfRangeException(nameof(blackY));
        if (!double.IsFinite(blackZ) || blackZ < 0) throw new ArgumentOutOfRangeException(nameof(blackZ));
        ComponentCount = componentCount;
        WhiteX = whiteX; WhiteY = whiteY; WhiteZ = whiteZ;
        BlackX = blackX; BlackY = blackY; BlackZ = blackZ;
    }

    internal int ComponentCount { get; }
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
}

/// <summary>A calibrated one-component grayscale color space.</summary>
public sealed class PdfCalGrayColorSpace : PdfCalibratedColorSpace
{
    /// <summary>Creates a calibrated grayscale space from white point, black point, and gamma.</summary>
    public PdfCalGrayColorSpace(
        double whiteX = 0.9642, double whiteY = 1, double whiteZ = 0.8249,
        double blackX = 0, double blackY = 0, double blackZ = 0,
        double gamma = 1)
        : base(1, whiteX, whiteY, whiteZ, blackX, blackY, blackZ)
    {
        if (!double.IsFinite(gamma) || gamma <= 0)
            throw new ArgumentOutOfRangeException(nameof(gamma));
        Gamma = gamma;
    }
    /// <summary>Gets the positive gray-component gamma value.</summary>
    public double Gamma { get; }
}

/// <summary>A calibrated three-component RGB color space.</summary>
public sealed class PdfCalRgbColorSpace : PdfCalibratedColorSpace
{
    private readonly double[] _gamma;
    private readonly double[] _matrix;

    /// <summary>Creates a calibrated RGB space from white point, black point, gamma, and matrix.</summary>
    public PdfCalRgbColorSpace(
        double whiteX = 0.9642, double whiteY = 1, double whiteZ = 0.8249,
        double blackX = 0, double blackY = 0, double blackZ = 0,
        IReadOnlyList<double>? gamma = null,
        IReadOnlyList<double>? matrix = null)
        : base(3, whiteX, whiteY, whiteZ, blackX, blackY, blackZ)
    {
        gamma ??= [1, 1, 1];
        matrix ??= [1, 0, 0, 0, 1, 0, 0, 0, 1];
        if (gamma.Count != 3 || gamma.Any(value => !double.IsFinite(value) || value <= 0))
            throw new ArgumentException("CalRGB gamma requires three positive finite values.", nameof(gamma));
        if (matrix.Count != 9 || matrix.Any(value => !double.IsFinite(value)))
            throw new ArgumentException("A CalRGB matrix requires nine finite values.", nameof(matrix));
        double determinant =
            matrix[0] * (matrix[4] * matrix[8] - matrix[5] * matrix[7])
            - matrix[1] * (matrix[3] * matrix[8] - matrix[5] * matrix[6])
            + matrix[2] * (matrix[3] * matrix[7] - matrix[4] * matrix[6]);
        if (determinant == 0)
            throw new ArgumentException("A CalRGB matrix must be invertible.", nameof(matrix));
        _gamma = [.. gamma];
        _matrix = [.. matrix];
    }

    /// <summary>Gets the three positive component gamma values.</summary>
    public IReadOnlyList<double> Gamma => _gamma;
    /// <summary>Gets the finite invertible three-by-three calibration matrix.</summary>
    public IReadOnlyList<double> Matrix => _matrix;
}
