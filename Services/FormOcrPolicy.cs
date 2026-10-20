using KillerPdf.Engine.Documents;

namespace KillerPDF.Services;

internal static class FormOcrPolicy
{
    private static readonly string[] NumericNameMarkers =
        ["amount", "total", "number", "numeric", "price", "quantity",
         "qty", "zip", "postal", "phone", "date", "currency", "tax"];

    private const long CombFlag = 1L << 24;
    internal const string NumericWhitelist = "0123456789.,-+/$%(): ";

    internal sealed record Region(
        int Left, int Top, int Right, int Bottom,
        string? CharacterWhitelist, int MaximumLength,
        IReadOnlyList<string> ChoiceValues, bool IsComb);

    internal static IReadOnlyList<Region> MapRegions(
        IReadOnlyList<PdfFormWidgetInfo> widgets, int pixelWidth, int pixelHeight,
        int additionalRotation = 0)
    {
        var regions = new List<Region>();
        foreach (PdfFormWidgetInfo widget in widgets)
        {
            if (widget.FieldKind is not (PdfFormFieldKind.Text or PdfFormFieldKind.Choice)
                || widget.PageBoxWidth <= 0 || widget.PageBoxHeight <= 0)
                continue;

            double left = (widget.Left - widget.PageBoxLeft) / widget.PageBoxWidth;
            double right = (widget.Right - widget.PageBoxLeft) / widget.PageBoxWidth;
            double top = 1 - (widget.Top - widget.PageBoxBottom) / widget.PageBoxHeight;
            double bottom = 1 - (widget.Bottom - widget.PageBoxBottom) / widget.PageBoxHeight;
            int rotation = ((widget.PageRotation + additionalRotation) % 360 + 360) % 360;
            (left, top, right, bottom) = RotateBounds(left, top, right, bottom, rotation);

            const double coordinateEpsilon = 1e-7;
            int x1 = Math.Clamp((int)Math.Floor(left * pixelWidth + coordinateEpsilon), 0, pixelWidth - 1);
            int y1 = Math.Clamp((int)Math.Floor(top * pixelHeight + coordinateEpsilon), 0, pixelHeight - 1);
            int x2 = Math.Clamp((int)Math.Ceiling(right * pixelWidth - coordinateEpsilon), x1 + 1, pixelWidth);
            int y2 = Math.Clamp((int)Math.Ceiling(bottom * pixelHeight - coordinateEpsilon), y1 + 1, pixelHeight);
            if (x2 - x1 < 3 || y2 - y1 < 3) continue;

            string? whitelist = LooksNumeric(widget.FieldName) ? NumericWhitelist : null;
            int maximumLength = widget.FieldKind == PdfFormFieldKind.Text
                ? widget.MaximumLength : 0;
            string[] choices = [.. widget.Options.Select(option => option.DisplayValue)
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()];
            regions.Add(new Region(x1, y1, x2, y2, whitelist, maximumLength, choices,
                maximumLength > 0 && x2 - x1 >= maximumLength && (widget.Flags & CombFlag) != 0));
        }
        return regions;
    }

    internal static string ClosestChoice(string text, IReadOnlyList<string> choices)
    {
        if (choices.Count == 0) return text;
        string best = choices[0];
        int bestDistance = Distance(text, best);
        foreach (string choice in choices.Skip(1))
        {
            int distance = Distance(text, choice);
            if (distance < bestDistance) { best = choice; bestDistance = distance; }
        }
        return bestDistance <= Math.Max(1, best.Length / 3) ? best : text;
    }

    private static (double left, double top, double right, double bottom) RotateBounds(
        double left, double top, double right, double bottom, int rotation)
    {
        var points = new[] { (left, top), (right, top), (right, bottom), (left, bottom) }
            .Select(point => rotation switch
            {
                90 => (1 - point.Item2, point.Item1),
                180 => (1 - point.Item1, 1 - point.Item2),
                270 => (point.Item2, 1 - point.Item1),
                _ => point
            }).ToArray();
        return (points.Min(p => p.Item1), points.Min(p => p.Item2),
            points.Max(p => p.Item1), points.Max(p => p.Item2));
    }

    private static bool LooksNumeric(string name)
    {
        string normalized = name.ToLowerInvariant();
        return NumericNameMarkers.Any(normalized.Contains);
    }

    private static int Distance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (int i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (int j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (char.ToUpperInvariant(left[i - 1]) ==
                        char.ToUpperInvariant(right[j - 1]) ? 0 : 1));
            previous = current;
        }
        return previous[right.Length];
    }
}
