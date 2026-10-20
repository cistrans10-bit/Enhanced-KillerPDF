namespace KillerPdf.Engine.Signing;

/// <summary>Incremental revisions and object definitions added after a signature revision.</summary>
public sealed record PdfSignedRevisionAnalysis
{
    /// <summary>Gets the signed revision's byte length.</summary>
    public long SignedRevisionLength { get; init; }
    /// <summary>Gets the current document's byte length.</summary>
    public long CurrentDocumentLength { get; init; }
    /// <summary>Gets whether the signed byte prefix is independently valid PDF.</summary>
    public bool SignedRevisionIsValidPdf { get; init; }
    /// <summary>Gets the number of incremental revisions after the signed revision.</summary>
    public int LaterRevisionCount { get; init; }
    /// <summary>Gets all object numbers changed after the signed revision.</summary>
    public IReadOnlyList<int> ChangedObjectNumbers { get; init; } = [];
    /// <summary>Gets object numbers first introduced after the signed revision.</summary>
    public IReadOnlyList<int> AddedObjectNumbers { get; init; } = [];
    /// <summary>Gets existing object numbers redefined after the signed revision.</summary>
    public IReadOnlyList<int> UpdatedObjectNumbers { get; init; } = [];
    /// <summary>Gets object numbers freed after the signed revision.</summary>
    public IReadOnlyList<int> FreedObjectNumbers { get; init; } = [];
    /// <summary>Gets the conservative certification-permission assessment.</summary>
    public PdfSignedRevisionPermissionAssessment PermissionAssessment { get; init; }
    /// <summary>Gets whether bytes were appended after the signed revision.</summary>
    public bool HasLaterChanges => CurrentDocumentLength > SignedRevisionLength;
}
