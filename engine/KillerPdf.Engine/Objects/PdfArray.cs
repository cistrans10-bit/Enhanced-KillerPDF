using System.Collections;

namespace KillerPdf.Engine.Objects;

/// <summary>An immutable ordered collection of non-null PDF objects.</summary>
public sealed class PdfArray : PdfObject, IReadOnlyList<PdfObject>
{
    private readonly PdfObject[] _items;

    /// <summary>Creates an array from a sequence of PDF objects.</summary>
    public PdfArray(IEnumerable<PdfObject> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = [.. items];
        if (_items.Any(item => item is null))
            throw new ArgumentException(
                "A PDF array cannot contain a null object reference; use PdfNull.Instance.",
                nameof(items));
    }

    /// <inheritdoc/>
    public int Count => _items.Length;
    /// <inheritdoc/>
    public PdfObject this[int index] => _items[index];

    /// <inheritdoc/>
    public IEnumerator<PdfObject> GetEnumerator() => ((IEnumerable<PdfObject>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
