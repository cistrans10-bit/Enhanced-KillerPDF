namespace KillerPdf.Engine.Authoring;

/// <summary>A standard marked or review workflow state for a text-note annotation.</summary>
public enum PdfTextNoteState
{
    /// <summary>The annotation has been marked.</summary>
    Marked,
    /// <summary>The annotation has not been marked.</summary>
    Unmarked,
    /// <summary>The annotation has been accepted during review.</summary>
    Accepted,
    /// <summary>The annotation has been rejected during review.</summary>
    Rejected,
    /// <summary>The annotation review has been cancelled.</summary>
    Cancelled,
    /// <summary>The annotation review has been completed.</summary>
    Completed,
    /// <summary>The annotation has no review state.</summary>
    NoReviewState
}

internal static class PdfTextNoteStateNames
{
    internal static string State(PdfTextNoteState value) => value switch
    {
        PdfTextNoteState.Marked => "Marked",
        PdfTextNoteState.Unmarked => "Unmarked",
        PdfTextNoteState.Accepted => "Accepted",
        PdfTextNoteState.Rejected => "Rejected",
        PdfTextNoteState.Cancelled => "Cancelled",
        PdfTextNoteState.Completed => "Completed",
        PdfTextNoteState.NoReviewState => "None",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    internal static string Model(PdfTextNoteState value) => value switch
    {
        PdfTextNoteState.Marked or PdfTextNoteState.Unmarked => "Marked",
        PdfTextNoteState.Accepted or PdfTextNoteState.Rejected or PdfTextNoteState.Cancelled
            or PdfTextNoteState.Completed or PdfTextNoteState.NoReviewState => "Review",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
