using KillerPdf.Engine.Authoring;

namespace KillerPdf.Engine.Documents;

/// <summary>Describes one item in a PDF document's bookmark hierarchy.</summary>
public sealed record PdfBookmarkInfo
{
    /// <summary>Gets the bookmark's stable indirect-object number.</summary>
    public required int ObjectNumber { get; init; }
    /// <summary>Gets the bookmark's stable indirect-object generation.</summary>
    public required int Generation { get; init; }
    /// <summary>Gets the decoded bookmark title.</summary>
    public required string Title { get; init; }
    /// <summary>Gets whether the bookmark's children are initially expanded.</summary>
    public required bool IsOpen { get; init; }
    /// <summary>Gets the title emphasis flags.</summary>
    public required PdfBookmarkStyle Style { get; init; }
    /// <summary>Gets the optional bookmark-title color.</summary>
    public PdfRgbColor? Color { get; init; }
    /// <summary>Gets the zero-based destination page, when it can be resolved locally.</summary>
    public int? DestinationPageIndex { get; init; }
    /// <summary>Gets the decoded named destination, when the bookmark uses one.</summary>
    public string? NamedDestination { get; init; }
    /// <summary>Gets the destination view, when it can be decoded.</summary>
    public PdfDestination? Destination { get; init; }
    /// <summary>Gets the bookmark's child items in document order.</summary>
    public required IReadOnlyList<PdfBookmarkInfo> Children { get; init; }
}
