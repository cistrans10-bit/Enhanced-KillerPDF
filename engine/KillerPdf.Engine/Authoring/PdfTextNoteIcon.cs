namespace KillerPdf.Engine.Authoring;

/// <summary>A standard icon displayed for a closed text-note annotation.</summary>
public enum PdfTextNoteIcon
{
    /// <summary>A note icon.</summary>
    Note,
    /// <summary>A comment icon.</summary>
    Comment,
    /// <summary>A key icon.</summary>
    Key,
    /// <summary>A help icon.</summary>
    Help,
    /// <summary>A new-paragraph icon.</summary>
    NewParagraph,
    /// <summary>A paragraph icon.</summary>
    Paragraph,
    /// <summary>An insert icon.</summary>
    Insert
}

internal static class PdfTextNoteIconNames
{
    public static string Name(PdfTextNoteIcon icon) => icon switch
    {
        PdfTextNoteIcon.Note => "Note",
        PdfTextNoteIcon.Comment => "Comment",
        PdfTextNoteIcon.Key => "Key",
        PdfTextNoteIcon.Help => "Help",
        PdfTextNoteIcon.NewParagraph => "NewParagraph",
        PdfTextNoteIcon.Paragraph => "Paragraph",
        PdfTextNoteIcon.Insert => "Insert",
        _ => throw new ArgumentOutOfRangeException(nameof(icon))
    };
}
