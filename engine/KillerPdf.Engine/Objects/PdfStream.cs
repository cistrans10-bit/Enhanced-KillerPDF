namespace KillerPdf.Engine.Objects;

/// <summary>A PDF stream dictionary and its encoded, undecoded payload bytes.</summary>
public sealed class PdfStream : PdfObject
{
    private readonly byte[] _encodedData;

    /// <summary>Creates a stream from its dictionary and encoded payload bytes.</summary>
    public PdfStream(PdfDictionary dictionary, ReadOnlySpan<byte> encodedData)
    {
        Dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        _encodedData = encodedData.ToArray();
    }

    /// <summary>Gets the stream dictionary.</summary>
    public PdfDictionary Dictionary { get; }
    /// <summary>Gets the encoded stream payload.</summary>
    public ReadOnlyMemory<byte> EncodedData => _encodedData;
}
