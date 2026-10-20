using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Documents;

/// <summary>Reads a PDF document's hierarchical bookmarks without exposing parser objects.</summary>
public static class PdfBookmarkReader
{
    private const int MaximumDepth = 256;
    private const int MaximumBookmarkCount = 1_000_000;
    private static readonly PdfName OutlinesName = Name("Outlines");
    private static readonly PdfName FirstName = Name("First");
    private static readonly PdfName NextName = Name("Next");
    private static readonly PdfName TitleName = Name("Title");
    private static readonly PdfName CountName = Name("Count");
    private static readonly PdfName StyleName = Name("F");
    private static readonly PdfName ColorName = Name("C");
    private static readonly PdfName DestinationName = Name("Dest");
    private static readonly PdfName ActionName = Name("A");
    private static readonly PdfName ActionTypeName = Name("S");
    private static readonly PdfName GoToName = Name("GoTo");
    private static readonly PdfName ActionDestinationName = Name("D");
    private static readonly PdfName NamesName = Name("Names");
    private static readonly PdfName DestsName = Name("Dests");

    /// <summary>Reads the top-level bookmarks and their descendants in document order.</summary>
    public static IReadOnlyList<PdfBookmarkInfo> Read(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        PdfPageTree tree = PdfPageTree.Read(document);
        if (!tree.Catalog.TryGetValue(OutlinesName, out PdfObject? outlinesValue))
            return [];
        PdfDictionary outlines = Resolve(document, outlinesValue, "The catalog /Outlines value")
            as PdfDictionary ?? throw new InvalidOperationException(
                "The catalog /Outlines value is not a dictionary.");
        if (!outlines.TryGetValue(FirstName, out PdfObject? first)) return [];

        var pages = tree.Pages.ToDictionary(
            page => (page.Reference.ObjectNumber, page.Reference.Generation), page => page.Index);
        Dictionary<string, PdfObject> namedDestinations = ReadNamedDestinations(document, tree.Catalog);
        var visited = new HashSet<(int, int)>();
        int count = 0;
        return ReadSiblings(first, 0);

        IReadOnlyList<PdfBookmarkInfo> ReadSiblings(PdfObject value, int depth)
        {
            if (depth >= MaximumDepth)
                throw new InvalidOperationException("The bookmark hierarchy exceeds the supported depth.");
            var result = new List<PdfBookmarkInfo>();
            PdfObject? current = value;
            while (current is not null)
            {
                if (current is not PdfIndirectReference reference)
                    throw new InvalidOperationException("A bookmark item is not an indirect reference.");
                var identity = (reference.ObjectNumber, reference.Generation);
                if (!visited.Add(identity))
                    throw new InvalidOperationException("The bookmark hierarchy contains a cycle or shared item.");
                if (++count > MaximumBookmarkCount)
                    throw new NotSupportedException("The document contains too many bookmarks.");
                PdfDictionary item = Resolve(document, reference, "A bookmark item") as PdfDictionary
                    ?? throw new InvalidOperationException("A bookmark item is not a dictionary.");
                string title = item.TryGetValue(TitleName, out PdfObject? titleValue)
                    && Resolve(document, titleValue, "A bookmark title") is PdfString titleString
                    ? PdfUnicodeEncoding.DecodeTextString(titleString.Bytes.Span, "A bookmark title")
                    : throw new InvalidOperationException("A bookmark item has no valid /Title string.");
                IReadOnlyList<PdfBookmarkInfo> children = item.TryGetValue(FirstName, out PdfObject? child)
                    ? ReadSiblings(child, depth + 1) : [];
                long outlineCount = Integer(document, item, CountName, 0, "A bookmark /Count");
                long styleValue = Integer(document, item, StyleName, 0, "A bookmark /F");
                if ((styleValue & ~3L) != 0)
                    throw new InvalidOperationException("A bookmark /F value contains unsupported flags.");
                PdfRgbColor? color = ReadColor(document, item);

                PdfObject? rawDestination = null;
                if (item.TryGetValue(DestinationName, out PdfObject? direct)) rawDestination = direct;
                else if (item.TryGetValue(ActionName, out PdfObject? actionValue)
                    && Resolve(document, actionValue, "A bookmark action") is PdfDictionary action
                    && action.TryGetValue(ActionTypeName, out PdfObject? typeValue)
                    && Resolve(document, typeValue, "A bookmark action type") is PdfName type
                    && type.Equals(GoToName)
                    && action.TryGetValue(ActionDestinationName, out PdfObject? actionDestination))
                    rawDestination = actionDestination;
                (int? pageIndex, string? named, PdfDestination? destination) =
                    ReadDestination(document, rawDestination, pages, namedDestinations);
                result.Add(new PdfBookmarkInfo
                {
                    ObjectNumber = reference.ObjectNumber,
                    Generation = reference.Generation,
                    Title = title,
                    IsOpen = children.Count > 0 && outlineCount >= 0,
                    Style = (PdfBookmarkStyle)styleValue,
                    Color = color,
                    DestinationPageIndex = pageIndex,
                    NamedDestination = named,
                    Destination = destination,
                    Children = children
                });
                current = item.TryGetValue(NextName, out PdfObject? next) ? next : null;
            }
            return result;
        }
    }

    private static Dictionary<string, PdfObject> ReadNamedDestinations(
        PdfDocument document, PdfDictionary catalog)
    {
        var result = new Dictionary<string, PdfObject>(StringComparer.Ordinal);
        if (catalog.TryGetValue(NamesName, out PdfObject? namesValue)
            && Resolve(document, namesValue, "The catalog /Names value") is PdfDictionary names
            && names.TryGetValue(DestsName, out PdfObject? destinationTree))
            foreach (PdfNameTreeEntry entry in PdfNameTree.Read(document, destinationTree))
                result.Add(PdfUnicodeEncoding.DecodeTextString(
                    entry.Key.Bytes.Span, "A named destination key"), entry.Value);
        if (catalog.TryGetValue(DestsName, out PdfObject? legacyValue)
            && Resolve(document, legacyValue, "The catalog /Dests value") is PdfDictionary legacy)
            foreach ((PdfName key, PdfObject value) in legacy)
                result.TryAdd(key.ValueAsLatin1(), value);
        return result;
    }

    private static (int? PageIndex, string? Named, PdfDestination? Destination) ReadDestination(
        PdfDocument document, PdfObject? value,
        Dictionary<(int ObjectNumber, int Generation), int> pages,
        Dictionary<string, PdfObject> namedDestinations)
    {
        if (value is null) return (null, null, null);
        PdfObject resolved = Resolve(document, value, "A bookmark destination");
        string? named = resolved switch
        {
            PdfString text => PdfUnicodeEncoding.DecodeTextString(
                text.Bytes.Span, "A bookmark named destination"),
            PdfName name => name.ValueAsLatin1(),
            _ => null
        };
        if (named is not null)
        {
            if (!namedDestinations.TryGetValue(named, out PdfObject? namedValue))
                return (null, named, null);
            resolved = Resolve(document, namedValue, "A named destination value");
            if (resolved is PdfDictionary dictionary
                && dictionary.TryGetValue(ActionDestinationName, out PdfObject? dictionaryDestination))
                resolved = Resolve(document, dictionaryDestination, "A named destination /D value");
        }
        if (resolved is not PdfArray array || array.Count < 2)
            return (null, named, null);
        int? pageIndex = array[0] is PdfIndirectReference pageReference
            && pages.TryGetValue((pageReference.ObjectNumber, pageReference.Generation), out int found)
                ? found : null;
        PdfName kind = Resolve(document, array[1], "A destination view type") as PdfName
            ?? throw new InvalidOperationException("A destination view type is not a name.");
        double?[] values = [.. array.Skip(2).Select(value => OptionalNumber(document, value))];
        PdfDestination destination = kind.ValueAsLatin1() switch
        {
            // ISO 32000 defines an /XYZ zoom of zero the same way as null: retain the
            // viewer's current zoom. Typst emits the explicit zero form. Normalize it
            // before constructing the authoring model, whose non-null zoom is positive.
            "XYZ" when values.Length == 3 => PdfDestination.At(
                values[0], values[1], values[2] == 0 ? null : values[2]),
            "Fit" when values.Length == 0 => PdfDestination.FitPage(),
            "FitH" when values.Length == 1 => PdfDestination.FitWidth(values[0]),
            "FitV" when values.Length == 1 => PdfDestination.FitHeight(values[0]),
            "FitR" when values is [double left, double bottom, double right, double top] =>
                PdfDestination.FitRectangle(left, bottom, right, top),
            "FitB" when values.Length == 0 => PdfDestination.FitBoundingBox(),
            "FitBH" when values.Length == 1 => PdfDestination.FitBoundingBoxWidth(values[0]),
            "FitBV" when values.Length == 1 => PdfDestination.FitBoundingBoxHeight(values[0]),
            _ => throw new InvalidOperationException("A bookmark destination has an invalid view array.")
        };
        return (pageIndex, named, destination);
    }

    private static PdfRgbColor? ReadColor(PdfDocument document, PdfDictionary item)
    {
        if (!item.TryGetValue(ColorName, out PdfObject? value)) return null;
        PdfArray array = Resolve(document, value, "A bookmark /C value") as PdfArray
            ?? throw new InvalidOperationException("A bookmark /C value is not an array.");
        if (array.Count != 3)
            throw new InvalidOperationException("A bookmark /C value does not contain three components.");
        return new PdfRgbColor(Number(document, array[0]), Number(document, array[1]), Number(document, array[2]));
    }

    private static long Integer(PdfDocument document, PdfDictionary dictionary, PdfName key,
        long fallback, string description) =>
        !dictionary.TryGetValue(key, out PdfObject? value) ? fallback
        : Resolve(document, value, description) is PdfInteger integer ? integer.Value
        : throw new InvalidOperationException($"{description} is not an integer.");

    private static double? OptionalNumber(PdfDocument document, PdfObject value) =>
        Resolve(document, value, "A destination coordinate") switch
        {
            PdfNull => null,
            PdfInteger integer => integer.Value,
            PdfReal real => real.Value,
            _ => throw new InvalidOperationException("A destination coordinate is not numeric or null.")
        };

    private static double Number(PdfDocument document, PdfObject value) =>
        OptionalNumber(document, value)
        ?? throw new InvalidOperationException("A required numeric value is null.");

    private static PdfObject Resolve(PdfDocument document, PdfObject value, string description)
    {
        var visited = new HashSet<(int, int)>();
        for (int depth = 0; value is PdfIndirectReference reference; depth++)
        {
            if (depth >= 32 || !visited.Add((reference.ObjectNumber, reference.Generation)))
                throw new InvalidOperationException($"{description} has an invalid reference chain.");
            value = document.Resolve(reference);
        }
        return value;
    }

    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
