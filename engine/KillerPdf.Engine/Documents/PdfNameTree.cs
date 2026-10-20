using System.Text;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Documents;

internal static class PdfNameTree
{
    private static readonly PdfName NamesName = Name("Names");
    private static readonly PdfName KidsName = Name("Kids");
    private static readonly PdfName LimitsName = Name("Limits");
    private const int MaximumDepth = 256;
    internal const int MaximumEntryCount = 1_000_000;

    internal static IReadOnlyList<PdfNameTreeEntry> Read(PdfDocument document, PdfObject root)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(root);
        var result = new List<PdfNameTreeEntry>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<(int ObjectNumber, int Generation)>();
        var visited = new HashSet<(int ObjectNumber, int Generation)>();
        byte[]? previousKey = null;
        Visit(root, 0);
        return result;

        void Visit(PdfObject value, int depth)
        {
            if (depth >= MaximumDepth)
                throw new InvalidOperationException("The name tree exceeds the supported nesting depth.");
            var referenceKeys = new List<(int ObjectNumber, int Generation)>();
            for (int aliasDepth = 0; value is PdfIndirectReference reference; aliasDepth++)
            {
                if (aliasDepth >= 32)
                    throw new InvalidOperationException(
                        "A name-tree node is too deeply indirect.");
                var referenceKey = (reference.ObjectNumber, reference.Generation);
                if (!active.Add(referenceKey))
                    throw new InvalidOperationException("The name tree contains a cycle.");
                referenceKeys.Add(referenceKey);
                if (!visited.Add(referenceKey))
                {
                    foreach (var key in referenceKeys) active.Remove(key);
                    throw new InvalidOperationException(
                        "The name tree references the same node more than once.");
                }
                value = document.Resolve(reference);
            }
            try
            {
                PdfDictionary node = value as PdfDictionary
                    ?? throw new InvalidOperationException("A name-tree node is not a dictionary.");
                int firstEntryIndex = result.Count;
                bool hasNames = node.TryGetValue(NamesName, out PdfObject? namesValue);
                bool hasKids = node.TryGetValue(KidsName, out PdfObject? kidsValue);
                if (hasNames == hasKids)
                    throw new InvalidOperationException(
                        "A name-tree node must contain exactly one of /Names or /Kids.");
                if (hasNames)
                {
                    PdfArray names = Resolve(namesValue!) as PdfArray
                        ?? throw new InvalidOperationException("A name-tree /Names value is not an array.");
                    if (names.Count % 2 != 0)
                        throw new InvalidOperationException("A name-tree /Names array has an unmatched key.");
                    for (int index = 0; index < names.Count; index += 2)
                    {
                        PdfString key = Resolve(names[index]) as PdfString
                            ?? throw new InvalidOperationException("A name-tree key is not a string.");
                        if (!keys.Add(Convert.ToBase64String(key.Bytes.Span)))
                            throw new InvalidOperationException("The name tree contains a duplicate key.");
                        if (previousKey is not null
                            && previousKey.AsSpan().SequenceCompareTo(key.Bytes.Span) >= 0)
                            throw new InvalidOperationException(
                                "The name tree contains keys that are not strictly ordered.");
                        previousKey = key.Bytes.ToArray();
                        if (result.Count >= MaximumEntryCount)
                            throw new NotSupportedException("The name tree contains too many entries.");
                        result.Add(new PdfNameTreeEntry(key, names[index + 1]));
                    }
                }
                else
                {
                    PdfArray kids = Resolve(kidsValue!) as PdfArray
                        ?? throw new InvalidOperationException("A name-tree /Kids value is not an array.");
                    if (kids.Count == 0)
                        throw new InvalidOperationException("A name-tree /Kids array is empty.");
                    foreach (PdfObject kid in kids) Visit(kid, depth + 1);
                }
                bool hasLimits = node.TryGetValue(LimitsName, out PdfObject? limitsValue);
                if (depth > 0 && !hasLimits)
                    throw new InvalidOperationException(
                        "A non-root name-tree node has no /Limits value.");
                if (hasLimits)
                {
                    PdfArray limits = Resolve(limitsValue!) as PdfArray
                        ?? throw new InvalidOperationException("A name-tree /Limits value is not an array.");
                    if (limits.Count != 2 || Resolve(limits[0]) is not PdfString lower
                        || Resolve(limits[1]) is not PdfString upper)
                        throw new InvalidOperationException(
                            "A name-tree /Limits value is not a two-string array.");
                    if (result.Count == firstEntryIndex
                        || !lower.Bytes.Span.SequenceEqual(result[firstEntryIndex].Key.Bytes.Span)
                        || !upper.Bytes.Span.SequenceEqual(result[^1].Key.Bytes.Span))
                        throw new InvalidOperationException(
                            "A name-tree /Limits value does not match its descendant key range.");
                }
            }
            finally
            {
                foreach (var referenceKey in referenceKeys) active.Remove(referenceKey);
            }
        }

        PdfObject Resolve(PdfObject value)
        {
            var visitedReferences = new HashSet<(int ObjectNumber, int Generation)>();
            for (int depth = 0; value is PdfIndirectReference reference; depth++)
            {
                if (depth >= 32)
                    throw new InvalidOperationException(
                        "A name-tree structural value is too deeply indirect.");
                if (!visitedReferences.Add((reference.ObjectNumber, reference.Generation)))
                    throw new InvalidOperationException(
                        "A name-tree structural value contains an indirect-reference cycle.");
                value = document.Resolve(reference);
            }
            return value;
        }
    }

    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}

internal sealed record PdfNameTreeEntry(PdfString Key, PdfObject Value);
