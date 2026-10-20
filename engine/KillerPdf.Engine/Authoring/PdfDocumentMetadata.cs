namespace KillerPdf.Engine.Authoring;

/// <summary>Descriptive metadata written to both the PDF information dictionary and XMP.</summary>
public sealed record PdfDocumentMetadata
{
    private PdfTrappedStatus? _trapped;

    /// <summary>Gets the document title.</summary>
    public string? Title { get; init; }
    /// <summary>Gets the document author.</summary>
    public string? Author { get; init; }
    /// <summary>Gets the document subject.</summary>
    public string? Subject { get; init; }
    /// <summary>Gets document-search keywords.</summary>
    public string? Keywords { get; init; }
    /// <summary>Gets the application that created the original document content.</summary>
    public string? Creator { get; init; }
    /// <summary>Gets the application that produced the PDF.</summary>
    public string? Producer { get; init; } = "The KillerPDF.Engine";
    /// <summary>Gets the document's primary natural language.</summary>
    public string? Language { get; init; }
    /// <summary>Gets the document creation date.</summary>
    public DateTimeOffset? CreationDate { get; init; }
    /// <summary>Gets the most recent document modification date.</summary>
    public DateTimeOffset? ModificationDate { get; init; }
    /// <summary>Gets whether the document has been trapped for printing.</summary>
    public PdfTrappedStatus? Trapped
    {
        get => _trapped;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
                throw new ArgumentOutOfRangeException(nameof(value));
            _trapped = value;
        }
    }
}

/// <summary>Whether a document has been modified to compensate for printing-registration errors.</summary>
public enum PdfTrappedStatus
{
    /// <summary>The document has not been trapped.</summary>
    False,
    /// <summary>The document has been trapped.</summary>
    True,
    /// <summary>The trapping status is unknown.</summary>
    Unknown
}
