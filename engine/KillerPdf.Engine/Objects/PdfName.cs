using System.Text;

namespace KillerPdf.Engine.Objects;

/// <summary>A PDF name, stored as its decoded bytes rather than assuming a text encoding.</summary>
public sealed class PdfName : PdfObject, IEquatable<PdfName>
{
    private readonly byte[] _bytes;

    /// <summary>Creates a name from its decoded byte representation.</summary>
    public PdfName(ReadOnlySpan<byte> bytes) => _bytes = bytes.ToArray();

    /// <summary>Gets the decoded name bytes.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes;

    /// <summary>Decodes the name bytes using the PDF-compatible Latin-1 mapping.</summary>
    public string ValueAsLatin1() => Encoding.Latin1.GetString(_bytes);

    /// <inheritdoc/>
    public bool Equals(PdfName? other) =>
        other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PdfName);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (byte value in _bytes)
            hash.Add(value);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() => "/" + ValueAsLatin1();
}
