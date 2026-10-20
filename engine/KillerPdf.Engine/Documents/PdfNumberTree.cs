using System.Text;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Documents;

internal static class PdfNumberTree
{
    private static readonly PdfName NumsName = Name("Nums");
    private static readonly PdfName KidsName = Name("Kids");
    private static readonly PdfName LimitsName = Name("Limits");
    private const int MaximumDepth = 256;
    internal const int MaximumEntryCount = 1_000_000;

    internal static IReadOnlyList<PdfNumberTreeEntry> Read(PdfDocument document, PdfObject root)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(root);
        var result = new List<PdfNumberTreeEntry>();
        var keys = new HashSet<long>();
        var active = new HashSet<(int ObjectNumber, int Generation)>();
        var visited = new HashSet<(int ObjectNumber, int Generation)>();
        long? previousKey = null;
        Visit(root, 0);
        return result;

        void Visit(PdfObject value, int depth)
        {
            if (depth >= MaximumDepth)
                throw new InvalidOperationException("The number tree exceeds the supported nesting depth.");
            var referenceKeys = new List<(int ObjectNumber, int Generation)>();
            for (int aliasDepth = 0; value is PdfIndirectReference reference; aliasDepth++)
            {
                if (aliasDepth >= 32)
                    throw new InvalidOperationException(
                        "A number-tree node is too deeply indirect.");
                var referenceKey = (reference.ObjectNumber, reference.Generation);
                if (!active.Add(referenceKey))
                    throw new InvalidOperationException("The number tree contains a cycle.");
                referenceKeys.Add(referenceKey);
                if (!visited.Add(referenceKey))
                {
                    foreach (var key in referenceKeys) active.Remove(key);
                    throw new InvalidOperationException(
                        "The number tree references the same node more than once.");
                }
                value = document.Resolve(reference);
            }
            try
            {
                PdfDictionary node = value as PdfDictionary
                    ?? throw new InvalidOperationException("A number-tree node is not a dictionary.");
                int firstEntryIndex = result.Count;
                bool hasNumbers = node.TryGetValue(NumsName, out PdfObject? numbersValue);
                bool hasKids = node.TryGetValue(KidsName, out PdfObject? kidsValue);
                if (hasNumbers == hasKids)
                    throw new InvalidOperationException(
                        "A number-tree node must contain exactly one of /Nums or /Kids.");
                if (hasNumbers)
                {
                    PdfArray numbers = Resolve(numbersValue!) as PdfArray
                        ?? throw new InvalidOperationException("A number-tree /Nums value is not an array.");
                    if (numbers.Count % 2 != 0)
                        throw new InvalidOperationException("A number-tree /Nums array has an unmatched key.");
                    for (int index = 0; index < numbers.Count; index += 2)
                    {
                        PdfInteger key = Resolve(numbers[index]) as PdfInteger
                            ?? throw new InvalidOperationException("A number-tree key is not an integer.");
                        if (!keys.Add(key.Value))
                            throw new InvalidOperationException("The number tree contains a duplicate key.");
                        if (previousKey.HasValue && previousKey.Value >= key.Value)
                            throw new InvalidOperationException(
                                "The number tree contains keys that are not strictly ordered.");
                        previousKey = key.Value;
                        if (result.Count >= MaximumEntryCount)
                            throw new NotSupportedException("The number tree contains too many entries.");
                        result.Add(new PdfNumberTreeEntry(key.Value, numbers[index + 1]));
                    }
                }
                else
                {
                    PdfArray kids = Resolve(kidsValue!) as PdfArray
                        ?? throw new InvalidOperationException("A number-tree /Kids value is not an array.");
                    if (kids.Count == 0)
                        throw new InvalidOperationException("A number-tree /Kids array is empty.");
                    foreach (PdfObject kid in kids) Visit(kid, depth + 1);
                }
                bool hasLimits = node.TryGetValue(LimitsName, out PdfObject? limitsValue);
                if (depth > 0 && !hasLimits)
                    throw new InvalidOperationException(
                        "A non-root number-tree node has no /Limits value.");
                if (hasLimits)
                {
                    PdfArray limits = Resolve(limitsValue!) as PdfArray
                        ?? throw new InvalidOperationException("A number-tree /Limits value is not an array.");
                    if (limits.Count != 2 || Resolve(limits[0]) is not PdfInteger lower
                        || Resolve(limits[1]) is not PdfInteger upper)
                        throw new InvalidOperationException(
                            "A number-tree /Limits value is not a two-integer array.");
                    if (result.Count == firstEntryIndex
                        || lower.Value != result[firstEntryIndex].Key
                        || upper.Value != result[^1].Key)
                        throw new InvalidOperationException(
                            "A number-tree /Limits value does not match its descendant key range.");
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
                        "A number-tree structural value is too deeply indirect.");
                if (!visitedReferences.Add((reference.ObjectNumber, reference.Generation)))
                    throw new InvalidOperationException(
                        "A number-tree structural value contains an indirect-reference cycle.");
                value = document.Resolve(reference);
            }
            return value;
        }
    }

    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}

internal sealed record PdfNumberTreeEntry(long Key, PdfObject Value);
