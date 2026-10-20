namespace KillerPdf.Engine.Authoring;

/// <summary>Base type for reusable PDF axial and radial gradient shadings.</summary>
public abstract class PdfShading
{
    private readonly PdfGradientStop[] _stops;

    private protected PdfShading(
        IEnumerable<PdfGradientStop> stops, bool extendStart, bool extendEnd,
        PdfShadingBounds? bounds, bool antiAlias, PdfGradientBackground? background)
    {
        ArgumentNullException.ThrowIfNull(stops);
        _stops = [.. stops];
        if (_stops.Length < 2)
            throw new ArgumentException("A gradient requires at least two color stops.", nameof(stops));
        foreach (PdfGradientStop stop in _stops)
        {
            int expectedComponents = stop.ColorSpace switch
            {
                PdfGradientColorSpace.Gray => 1,
                PdfGradientColorSpace.Rgb => 3,
                PdfGradientColorSpace.Cmyk => 4,
                _ => 0
            };
            if (expectedComponents == 0 || stop.Components is null
                || stop.Components.Count != expectedComponents
                || stop.Components.Any(component =>
                    !double.IsFinite(component) || component is < 0 or > 1))
                throw new ArgumentException(
                    "A gradient contains an uninitialized color stop.", nameof(stops));
        }
        if (_stops[0].Offset != 0 || _stops[^1].Offset != 1)
            throw new ArgumentException(
                "A gradient must begin at offset zero and end at offset one.", nameof(stops));
        for (int index = 1; index < _stops.Length; index++)
        {
            if (_stops[index].Offset <= _stops[index - 1].Offset)
                throw new ArgumentException(
                    "Gradient-stop offsets must be strictly increasing.", nameof(stops));
            if (_stops[index].ColorSpace != _stops[0].ColorSpace)
                throw new ArgumentException(
                    "Every stop in a gradient must use the same color space.", nameof(stops));
        }
        ExtendStart = extendStart;
        ExtendEnd = extendEnd;
        if (bounds is PdfShadingBounds value
            && (!double.IsFinite(value.MinimumX)
                || !double.IsFinite(value.MinimumY)
                || !double.IsFinite(value.MaximumX)
                || !double.IsFinite(value.MaximumY)
                || value.MaximumX <= value.MinimumX
                || value.MaximumY <= value.MinimumY))
            throw new ArgumentOutOfRangeException(nameof(bounds),
                "Shading bounds must be finite and have positive width and height.");
        Bounds = bounds;
        AntiAlias = antiAlias;
        if (background is not null && background.ColorSpace != _stops[0].ColorSpace)
            throw new ArgumentException(
                "A shading background must use the same color space as its stops.",
                nameof(background));
        Background = background;
    }

    /// <summary>Gets the strictly ordered color stops from zero through one.</summary>
    public IReadOnlyList<PdfGradientStop> Stops => _stops;
    /// <summary>Gets the device color space shared by every gradient stop.</summary>
    public PdfGradientColorSpace ColorSpace => _stops[0].ColorSpace;
    /// <summary>Gets whether the start color extends beyond the gradient domain.</summary>
    public bool ExtendStart { get; }
    /// <summary>Gets whether the end color extends beyond the gradient domain.</summary>
    public bool ExtendEnd { get; }
    /// <summary>Gets the optional rectangular evaluation boundary.</summary>
    public PdfShadingBounds? Bounds { get; }
    /// <summary>Gets whether the viewer should antialias the shading.</summary>
    public bool AntiAlias { get; }
    /// <summary>Gets the optional background color outside the shading geometry.</summary>
    public PdfGradientBackground? Background { get; }

    /// <summary>Validates and returns a finite shading coordinate.</summary>
    protected static double Coordinate(double value, string name)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, "Gradient coordinates must be finite.");
        return value;
    }
}

/// <summary>A linear gradient defined by a start point and an end point.</summary>
public sealed class PdfAxialGradient : PdfShading
{
    /// <summary>Creates an axial gradient with ordered device-color stops.</summary>
    public PdfAxialGradient(
        double startX, double startY, double endX, double endY,
        IEnumerable<PdfGradientStop> stops,
        bool extendStart = false, bool extendEnd = false,
        PdfShadingBounds? bounds = null, bool antiAlias = true,
        PdfGradientBackground? background = null)
        : base(stops, extendStart, extendEnd, bounds, antiAlias, background)
    {
        StartX = Coordinate(startX, nameof(startX));
        StartY = Coordinate(startY, nameof(startY));
        EndX = Coordinate(endX, nameof(endX));
        EndY = Coordinate(endY, nameof(endY));
        if (StartX == EndX && StartY == EndY)
            throw new ArgumentException("An axial gradient requires two different points.");
    }

    /// <summary>Gets the horizontal coordinate of the start point.</summary>
    public double StartX { get; }
    /// <summary>Gets the vertical coordinate of the start point.</summary>
    public double StartY { get; }
    /// <summary>Gets the horizontal coordinate of the end point.</summary>
    public double EndX { get; }
    /// <summary>Gets the vertical coordinate of the end point.</summary>
    public double EndY { get; }
}

/// <summary>A radial gradient interpolated between two circles.</summary>
public sealed class PdfRadialGradient : PdfShading
{
    /// <summary>Creates a radial gradient with ordered device-color stops.</summary>
    public PdfRadialGradient(
        double startX, double startY, double startRadius,
        double endX, double endY, double endRadius,
        IEnumerable<PdfGradientStop> stops,
        bool extendStart = false, bool extendEnd = false,
        PdfShadingBounds? bounds = null, bool antiAlias = true,
        PdfGradientBackground? background = null)
        : base(stops, extendStart, extendEnd, bounds, antiAlias, background)
    {
        StartX = Coordinate(startX, nameof(startX));
        StartY = Coordinate(startY, nameof(startY));
        EndX = Coordinate(endX, nameof(endX));
        EndY = Coordinate(endY, nameof(endY));
        StartRadius = Radius(startRadius, nameof(startRadius));
        EndRadius = Radius(endRadius, nameof(endRadius));
        if (StartX == EndX && StartY == EndY && StartRadius == EndRadius)
            throw new ArgumentException("A radial gradient requires different circles.");
    }

    /// <summary>Gets the horizontal coordinate of the start circle.</summary>
    public double StartX { get; }
    /// <summary>Gets the vertical coordinate of the start circle.</summary>
    public double StartY { get; }
    /// <summary>Gets the nonnegative radius of the start circle.</summary>
    public double StartRadius { get; }
    /// <summary>Gets the horizontal coordinate of the end circle.</summary>
    public double EndX { get; }
    /// <summary>Gets the vertical coordinate of the end circle.</summary>
    public double EndY { get; }
    /// <summary>Gets the nonnegative radius of the end circle.</summary>
    public double EndRadius { get; }

    private static double Radius(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name,
                "A radial-gradient radius must be finite and non-negative.");
        return value;
    }
}

/// <summary>An optional rectangular boundary for evaluating a shading.</summary>
public readonly record struct PdfShadingBounds
{
    /// <summary>Creates a finite rectangular boundary with positive width and height.</summary>
    public PdfShadingBounds(double minimumX, double minimumY, double maximumX, double maximumY)
    {
        if (!double.IsFinite(minimumX)) throw new ArgumentOutOfRangeException(nameof(minimumX));
        if (!double.IsFinite(minimumY)) throw new ArgumentOutOfRangeException(nameof(minimumY));
        if (!double.IsFinite(maximumX) || maximumX <= minimumX)
            throw new ArgumentOutOfRangeException(nameof(maximumX));
        if (!double.IsFinite(maximumY) || maximumY <= minimumY)
            throw new ArgumentOutOfRangeException(nameof(maximumY));
        MinimumX = minimumX;
        MinimumY = minimumY;
        MaximumX = maximumX;
        MaximumY = maximumY;
    }

    /// <summary>Gets the minimum horizontal coordinate.</summary>
    public double MinimumX { get; }
    /// <summary>Gets the minimum vertical coordinate.</summary>
    public double MinimumY { get; }
    /// <summary>Gets the maximum horizontal coordinate.</summary>
    public double MaximumX { get; }
    /// <summary>Gets the maximum vertical coordinate.</summary>
    public double MaximumY { get; }
}
