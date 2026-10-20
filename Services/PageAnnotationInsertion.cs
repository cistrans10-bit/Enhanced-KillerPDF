using System;
using System.Collections.Generic;
using System.Linq;

namespace KillerPDF.Services;

internal static class PageAnnotationInsertion
{
    internal static void Shift(
        Dictionary<int, List<PageAnnotation>> annotations, int insertionIndex, int pageCount)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentOutOfRangeException.ThrowIfNegative(insertionIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        if (pageCount == 0) return;

        var shifted = new Dictionary<int, List<PageAnnotation>>();
        foreach (var pair in annotations)
        {
            int page = pair.Key >= insertionIndex ? pair.Key + pageCount : pair.Key;
            foreach (PageAnnotation annotation in pair.Value) annotation.PageIndex = page;
            shifted[page] = pair.Value;
        }
        annotations.Clear();
        foreach (var pair in shifted) annotations[pair.Key] = pair.Value;
    }

    /// <summary>Moves annotations with their pages. <paramref name="order"/> lists, for each new
    /// page position, the position that page held before the move.</summary>
    internal static void Reorder(
        Dictionary<int, List<PageAnnotation>> annotations, IReadOnlyList<int> order)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(order);
        if (order.Distinct().Count() != order.Count
            || order.Any(page => page < 0 || page >= order.Count))
            throw new ArgumentException("The page order must list every page exactly once.", nameof(order));

        var moved = new Dictionary<int, List<PageAnnotation>>();
        for (int page = 0; page < order.Count; page++)
        {
            if (!annotations.TryGetValue(order[page], out var list)) continue;
            foreach (PageAnnotation annotation in list) annotation.PageIndex = page;
            moved[page] = list;
        }
        foreach (var pair in annotations)
            if (pair.Key >= order.Count) moved[pair.Key] = pair.Value;
        annotations.Clear();
        foreach (var pair in moved) annotations[pair.Key] = pair.Value;
    }

    internal static void RemovePages(
        Dictionary<int, List<PageAnnotation>> annotations, IEnumerable<int> removedPages)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(removedPages);
        int[] removed = [.. removedPages.Distinct().OrderBy(page => page)];
        if (removed.Any(page => page < 0))
            throw new ArgumentOutOfRangeException(nameof(removedPages));
        if (removed.Length == 0) return;

        var removedSet = removed.ToHashSet();
        var shifted = new Dictionary<int, List<PageAnnotation>>();
        foreach (var pair in annotations)
        {
            if (removedSet.Contains(pair.Key)) continue;
            int page = pair.Key - removed.Count(removedPage => removedPage < pair.Key);
            foreach (PageAnnotation annotation in pair.Value) annotation.PageIndex = page;
            shifted[page] = pair.Value;
        }
        annotations.Clear();
        foreach (var pair in shifted) annotations[pair.Key] = pair.Value;
    }
}
