using System.Text;

namespace KillerPdf.Engine.Syntax;

/// <summary>The version marker found near the beginning of a PDF file.</summary>
public readonly record struct PdfHeader(PdfVersion Version, int Offset)
{
    private static ReadOnlySpan<byte> Signature => "%PDF-"u8;
    /// <summary>Maximum number of leading bytes searched for a compatible PDF header.</summary>
    public const int SearchLimit = 1024;

    /// <summary>Parses a bounded PDF header and its source offset.</summary>
    public static PdfHeader Parse(ReadOnlySpan<byte> source)
    {
        int searchableLength = Math.Min(source.Length, SearchLimit);
        int offset = source[..searchableLength].IndexOf(Signature);
        if (offset < 0)
            throw new FormatException("A PDF header was not found in the first 1,024 bytes.");

        ReadOnlySpan<byte> version = source[(offset + Signature.Length)..];
        if (version.Length < 3 || version[1] != (byte)'.'
            || version[0] is < (byte)'0' or > (byte)'9'
            || version[2] is < (byte)'0' or > (byte)'9')
            throw new FormatException("The PDF header does not contain a valid major.minor version.");
        int major = version[0] - (byte)'0';
        int minor = version[2] - (byte)'0';
        if (major == 1 && minor is 8 or 9)
            return new PdfHeader(PdfVersion.Pdf20, offset);
        if (!PdfVersion.IsDefined(major, minor))
            throw new NotSupportedException($"PDF {major}.{minor} is not a defined PDF version.");

        return new PdfHeader(new PdfVersion(major, minor), offset);
    }

    /// <summary>Creates canonical header bytes for a defined PDF version.</summary>
    public static byte[] Create(PdfVersion version)
    {
        if (!PdfVersion.IsDefined(version.Major, version.Minor))
            throw new ArgumentOutOfRangeException(nameof(version),
                "The PDF header version is not defined.");
        return Encoding.ASCII.GetBytes($"%PDF-{version}\n");
    }
}
