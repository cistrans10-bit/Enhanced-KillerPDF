using System.Text;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Authoring;

/// <summary>Controls how a destination page is positioned in a conforming viewer.</summary>
public sealed class PdfDestination
{
    private PdfDestination(PdfDestinationKind kind, params double?[] values)
    {
        Kind = kind;
        Values = values;
    }

    /// <summary>Creates a destination that fits the complete page in the viewer.</summary>
    public static PdfDestination FitPage() => new(PdfDestinationKind.Fit);
    /// <summary>Creates a destination that fits the page bounding box in the viewer.</summary>
    public static PdfDestination FitBoundingBox() => new(PdfDestinationKind.FitB);
    /// <summary>Creates a destination that fits the page width at an optional top coordinate.</summary>
    public static PdfDestination FitWidth(double? top = null) =>
        new(PdfDestinationKind.FitH, Optional(top, nameof(top)));
    /// <summary>Creates a destination that fits the page height at an optional left coordinate.</summary>
    public static PdfDestination FitHeight(double? left = null) =>
        new(PdfDestinationKind.FitV, Optional(left, nameof(left)));
    /// <summary>Creates a destination that fits the bounding-box width at an optional top coordinate.</summary>
    public static PdfDestination FitBoundingBoxWidth(double? top = null) =>
        new(PdfDestinationKind.FitBH, Optional(top, nameof(top)));
    /// <summary>Creates a destination that fits the bounding-box height at an optional left coordinate.</summary>
    public static PdfDestination FitBoundingBoxHeight(double? left = null) =>
        new(PdfDestinationKind.FitBV, Optional(left, nameof(left)));
    /// <summary>Creates a destination at optional left and top coordinates and an optional positive zoom factor.</summary>
    public static PdfDestination At(double? left = null, double? top = null, double? zoom = null)
    {
        if (zoom.HasValue && (!double.IsFinite(zoom.Value) || zoom.Value <= 0))
            throw new ArgumentOutOfRangeException(nameof(zoom));
        return new(PdfDestinationKind.Xyz,
            Optional(left, nameof(left)), Optional(top, nameof(top)), zoom);
    }
    /// <summary>Creates a destination that fits the specified nonempty rectangle.</summary>
    public static PdfDestination FitRectangle(
        double left, double bottom, double right, double top)
    {
        if (!double.IsFinite(left)) throw new ArgumentOutOfRangeException(nameof(left));
        if (!double.IsFinite(bottom)) throw new ArgumentOutOfRangeException(nameof(bottom));
        if (!double.IsFinite(right) || right <= left) throw new ArgumentOutOfRangeException(nameof(right));
        if (!double.IsFinite(top) || top <= bottom) throw new ArgumentOutOfRangeException(nameof(top));
        return new(PdfDestinationKind.FitR, left, bottom, right, top);
    }

    /// <summary>Gets the destination view mode.</summary>
    public PdfDestinationKind Kind { get; }
    /// <summary>Gets the coordinates used by the destination view mode.</summary>
    public IReadOnlyList<double?> Values { get; }

    internal PdfArray ToArray(PdfIndirectReference page)
    {
        var values = new List<PdfObject> { page, Name(Kind switch
        {
            PdfDestinationKind.Xyz => "XYZ",
            PdfDestinationKind.Fit => "Fit",
            PdfDestinationKind.FitH => "FitH",
            PdfDestinationKind.FitV => "FitV",
            PdfDestinationKind.FitR => "FitR",
            PdfDestinationKind.FitB => "FitB",
            PdfDestinationKind.FitBH => "FitBH",
            PdfDestinationKind.FitBV => "FitBV",
            _ => throw new InvalidOperationException($"Unsupported destination kind: {Kind}.")
        }) };
        values.AddRange(Values.Select(value => value switch
        {
            null => (PdfObject)PdfNull.Instance,
            double number when number == Math.Truncate(number)
                && number is >= long.MinValue and <= long.MaxValue =>
                new PdfInteger((long)number),
            double number => new PdfReal(number)
        }));
        return new PdfArray(values);
    }

    private static double? Optional(double? value, string name)
    {
        if (value.HasValue && !double.IsFinite(value.Value))
            throw new ArgumentOutOfRangeException(name);
        return value;
    }

    private static PdfName Name(string value) =>
        new(Encoding.ASCII.GetBytes(value));
}

/// <summary>Identifies how a destination page is positioned in a conforming viewer.</summary>
public enum PdfDestinationKind
{
    /// <summary>Uses optional left, top, and zoom values.</summary>
    Xyz,
    /// <summary>Fits the complete page.</summary>
    Fit,
    /// <summary>Fits the page width.</summary>
    FitH,
    /// <summary>Fits the page height.</summary>
    FitV,
    /// <summary>Fits a rectangle.</summary>
    FitR,
    /// <summary>Fits the page bounding box.</summary>
    FitB,
    /// <summary>Fits the bounding-box width.</summary>
    FitBH,
    /// <summary>Fits the bounding-box height.</summary>
    FitBV
}
