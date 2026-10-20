using System.Buffers.Binary;
using System.Text;

namespace KillerPdf.Engine.Authoring;

/// <summary>A bounded ICC profile suitable for an ICCBased colour space or output intent.</summary>
public sealed class PdfIccProfile
{
    private readonly byte[] _data;

    private PdfIccProfile(byte[] data, int componentCount, string colorSpace)
    {
        _data = data;
        ComponentCount = componentCount;
        ColorSpace = colorSpace;
    }

    /// <summary>Gets the validated ICC profile bytes.</summary>
    public ReadOnlyMemory<byte> Data => _data;
    /// <summary>Gets the number of color components described by the profile.</summary>
    public int ComponentCount { get; }
    /// <summary>Gets the ICC header color-space signature.</summary>
    public string ColorSpace { get; }

    /// <summary>Loads and validates a bounded ICC profile.</summary>
    public static PdfIccProfile Load(ReadOnlyMemory<byte> source)
    {
        byte[] data = source.ToArray();
        if (data.Length < 132)
            throw new FormatException(
                "An ICC profile must contain its 128-byte header and tag-table count.");
        uint declaredSize = BinaryPrimitives.ReadUInt32BigEndian(data);
        if (declaredSize < 128 || declaredSize > data.Length)
            throw new FormatException("The ICC profile's declared size points outside the supplied data.");
        if (!data.AsSpan(36, 4).SequenceEqual("acsp"u8))
            throw new FormatException("The ICC profile signature is missing.");
        uint tagCount = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(128, 4));
        long tagTableEnd = 132L + tagCount * 12L;
        if (tagTableEnd > declaredSize)
            throw new FormatException("The ICC profile tag table is truncated.");
        var tagSignatures = new HashSet<uint>();
        for (uint tagIndex = 0; tagIndex < tagCount; tagIndex++)
        {
            int entryOffset = checked(132 + (int)tagIndex * 12);
            uint signature = BinaryPrimitives.ReadUInt32BigEndian(
                data.AsSpan(entryOffset, 4));
            uint offset = BinaryPrimitives.ReadUInt32BigEndian(
                data.AsSpan(entryOffset + 4, 4));
            uint size = BinaryPrimitives.ReadUInt32BigEndian(
                data.AsSpan(entryOffset + 8, 4));
            if (!tagSignatures.Add(signature))
                throw new FormatException("The ICC profile tag table contains a duplicate signature.");
            if ((offset & 3) != 0 || size < 8
                || offset < tagTableEnd || offset > declaredSize
                || size > declaredSize - offset)
                throw new FormatException(
                    "An ICC profile tag has an invalid alignment, size, or data range.");
        }
        string colorSpace = Encoding.ASCII.GetString(data, 16, 4);
        int components = colorSpace switch
        {
            "GRAY" => 1,
            "RGB " or "XYZ " or "Lab " or "Luv " or "YCbr" or "Yxy "
                or "HSV " or "HLS " or "CMY " or "3CLR" => 3,
            "CMYK" or "4CLR" => 4,
            _ => throw new NotSupportedException(
                $"ICC colour space '{colorSpace.Trim()}' is not yet supported for PDF output intents.")
        };
        if (declaredSize < data.Length)
            Array.Resize(ref data, checked((int)declaredSize));
        return new PdfIccProfile(data, components, colorSpace.TrimEnd());
    }
}
