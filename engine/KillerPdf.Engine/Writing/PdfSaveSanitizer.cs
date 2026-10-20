using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Writing;

/// <summary>Repairs harmless structural artifacts before application-authored output is finalized.</summary>
public static class PdfSaveSanitizer
{
    private static readonly PdfName OutlinesName = Name("Outlines");
    private static readonly PdfName FirstName = Name("First");
    private static readonly PdfName CropBoxName = Name("CropBox");
    private static readonly PdfName MediaBoxName = Name("MediaBox");

    /// <summary>
    /// Removes an empty or dangling outline root and direct crop boxes that are degenerate or
    /// outside the effective media box. Returns the original bytes when no repair is required.
    /// </summary>
    public static byte[] RepairHarmlessArtifacts(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        PdfPageTree tree = PdfPageTree.Read(document);
        var update = new PdfIncrementalUpdateBuilder(document);
        bool changed = false;

        if (tree.Catalog.TryGetValue(OutlinesName, out PdfObject? outlinesValue))
        {
            PdfObject outlines = Resolve(document, outlinesValue);
            if (outlines is not PdfDictionary dictionary || !dictionary.ContainsKey(FirstName))
            {
                update.ReplaceObject(tree.CatalogReference.ObjectNumber,
                    new PdfDictionary(tree.Catalog.Where(entry => !entry.Key.Equals(OutlinesName))));
                changed = true;
            }
        }

        foreach (PdfPageTreeEntry page in tree.Pages)
        {
            if (!page.Dictionary.TryGetValue(CropBoxName, out PdfObject? cropValue)
                || !TryBox(document, cropValue, out Box crop)) continue;
            if (!page.InheritedValues.TryGetValue(MediaBoxName, out PdfObject? mediaValue)
                || !TryBox(document, mediaValue, out Box media)) continue;
            bool invalid = crop.Width < 1 || crop.Height < 1
                || crop.Left < media.Left - .01 || crop.Bottom < media.Bottom - .01
                || crop.Right > media.Right + .01 || crop.Top > media.Top + .01;
            if (!invalid) continue;
            update.ReplaceObject(page.Reference.ObjectNumber,
                new PdfDictionary(page.Dictionary.Where(entry => !entry.Key.Equals(CropBoxName))));
            changed = true;
        }

        return changed ? update.Build() : document.Source.ToArray();
    }

    private static bool TryBox(PdfDocument document, PdfObject value, out Box box)
    {
        value = Resolve(document, value);
        if (value is not PdfArray { Count: 4 } array
            || !TryNumber(document, array[0], out double x1)
            || !TryNumber(document, array[1], out double y1)
            || !TryNumber(document, array[2], out double x2)
            || !TryNumber(document, array[3], out double y2))
        { box = default; return false; }
        box = new Box(Math.Min(x1, x2), Math.Min(y1, y2),
            Math.Max(x1, x2), Math.Max(y1, y2));
        return true;
    }

    private static bool TryNumber(PdfDocument document, PdfObject value, out double number)
    {
        value = Resolve(document, value);
        if (value is PdfInteger integer) { number = integer.Value; return true; }
        if (value is PdfReal real && double.IsFinite(real.Value)) { number = real.Value; return true; }
        number = 0;
        return false;
    }

    private static PdfObject Resolve(PdfDocument document, PdfObject value)
    {
        var visited = new HashSet<(int, int)>();
        for (int depth = 0; value is PdfIndirectReference reference; depth++)
        {
            if (depth >= 32 || !visited.Add((reference.ObjectNumber, reference.Generation)))
                return PdfNull.Instance;
            value = document.Resolve(reference);
        }
        return value;
    }

    private static PdfName Name(string value) => new(System.Text.Encoding.ASCII.GetBytes(value));
    private readonly record struct Box(double Left, double Bottom, double Right, double Top)
    {
        internal double Width => Right - Left;
        internal double Height => Top - Bottom;
    }
}
