using System.Globalization;

namespace KillerPdf.Engine.Syntax;

/// <summary>A PDF specification version that KillerPdf.Engine can read or write.</summary>
public readonly record struct PdfVersion : IComparable<PdfVersion>
{
    /// <summary>PDF 1.0.</summary>
    public static readonly PdfVersion Pdf10 = new(1, 0);
    /// <summary>PDF 1.7, the final ISO 32000-1 version.</summary>
    public static readonly PdfVersion Pdf17 = new(1, 7);
    /// <summary>PDF 2.0, the base ISO 32000-2 version.</summary>
    public static readonly PdfVersion Pdf20 = new(2, 0);

    /// <summary>Creates a defined PDF version from major and minor components.</summary>
    public PdfVersion(int major, int minor)
    {
        if (!IsDefined(major, minor))
            throw new ArgumentOutOfRangeException(nameof(minor), $"PDF {major}.{minor} is not a defined PDF version.");

        Major = major;
        Minor = minor;
    }

    /// <summary>Gets the major version component.</summary>
    public int Major { get; }
    /// <summary>Gets the minor version component.</summary>
    public int Minor { get; }

    // ISO 32000-2 reserves the 2.x header range for PDF 2.0-compatible declarations.
    /// <summary>Returns whether a major and minor pair is defined for a PDF header.</summary>
    public static bool IsDefined(int major, int minor) =>
        (major == 1 && minor is >= 0 and <= 7) || (major == 2 && minor is >= 0 and <= 9);

    /// <summary>Compares versions by major component and then minor component.</summary>
    public int CompareTo(PdfVersion other)
    {
        int major = Major.CompareTo(other.Major);
        return major != 0 ? major : Minor.CompareTo(other.Minor);
    }

    /// <summary>Formats the version as an invariant major.minor string.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture, $"{Major}.{Minor}");
}
