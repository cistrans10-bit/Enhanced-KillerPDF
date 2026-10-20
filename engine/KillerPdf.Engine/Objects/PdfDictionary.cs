using System.Collections;

namespace KillerPdf.Engine.Objects;

/// <summary>An immutable PDF dictionary with unique name keys and non-null object values.</summary>
public sealed class PdfDictionary : PdfObject, IReadOnlyDictionary<PdfName, PdfObject>
{
    private readonly Dictionary<PdfName, PdfObject> _entries;

    /// <summary>Creates a dictionary from unique name and object pairs.</summary>
    public PdfDictionary(IEnumerable<KeyValuePair<PdfName, PdfObject>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [];
        foreach ((PdfName key, PdfObject value) in entries)
        {
            if (key is null)
                throw new ArgumentException(
                    "A PDF dictionary cannot contain a null key.", nameof(entries));
            if (value is null)
                throw new ArgumentException(
                    "A PDF dictionary cannot contain a null object reference; use PdfNull.Instance.",
                    nameof(entries));
            if (!_entries.TryAdd(key, value))
                throw new ArgumentException($"The dictionary contains the duplicate key {key}.", nameof(entries));
        }
    }

    /// <inheritdoc/>
    public int Count => _entries.Count;
    /// <inheritdoc/>
    public IEnumerable<PdfName> Keys => _entries.Keys;
    /// <inheritdoc/>
    public IEnumerable<PdfObject> Values => _entries.Values;
    /// <inheritdoc/>
    public PdfObject this[PdfName key] => _entries[key];

    /// <inheritdoc/>
    public bool ContainsKey(PdfName key) => _entries.ContainsKey(key);
    /// <inheritdoc/>
    public bool TryGetValue(PdfName key, out PdfObject value) => _entries.TryGetValue(key, out value!);
    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<PdfName, PdfObject>> GetEnumerator() => _entries.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
