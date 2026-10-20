namespace KillerPdf.Engine.Authoring;

/// <summary>Optional authorship and lifecycle metadata for an authored annotation.</summary>
public sealed record PdfAnnotationMetadata
{
    private PdfAnnotationFlags _flags = PdfAnnotationFlags.Print;

    /// <summary>Gets the optional annotation author.</summary>
    public string? Author { get; init; }
    /// <summary>Gets the optional annotation subject.</summary>
    public string? Subject { get; init; }
    /// <summary>Gets the optional creation timestamp.</summary>
    public DateTimeOffset? CreationDate { get; init; }
    /// <summary>Gets the optional last-modification timestamp.</summary>
    public DateTimeOffset? ModificationDate { get; init; }
    /// <summary>Gets the validated annotation behavior flags.</summary>
    public PdfAnnotationFlags Flags
    {
        get => _flags;
        init
        {
            const PdfAnnotationFlags all =
                PdfAnnotationFlags.Invisible | PdfAnnotationFlags.Hidden
                | PdfAnnotationFlags.Print | PdfAnnotationFlags.NoZoom
                | PdfAnnotationFlags.NoRotate | PdfAnnotationFlags.NoView
                | PdfAnnotationFlags.ReadOnly | PdfAnnotationFlags.Locked
                | PdfAnnotationFlags.ToggleNoView | PdfAnnotationFlags.LockedContents;
            if ((value & ~all) != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _flags = value;
        }
    }
}

/// <summary>Standard bit flags controlling annotation visibility and interaction.</summary>
[Flags]
public enum PdfAnnotationFlags
{
    /// <summary>No annotation flags.</summary>
    None = 0,
    /// <summary>Do not display an annotation whose appearance is unknown.</summary>
    Invisible = 1,
    /// <summary>Do not display, print, or interact with the annotation.</summary>
    Hidden = 2,
    /// <summary>Print the annotation with the page.</summary>
    Print = 4,
    /// <summary>Keep the annotation size fixed while the page is zoomed.</summary>
    NoZoom = 8,
    /// <summary>Do not rotate the annotation with the page.</summary>
    NoRotate = 16,
    /// <summary>Do not display or interact with the annotation on screen.</summary>
    NoView = 32,
    /// <summary>Do not permit interaction with the annotation.</summary>
    ReadOnly = 64,
    /// <summary>Do not permit the annotation itself to be deleted or repositioned.</summary>
    Locked = 128,
    /// <summary>Invert the meaning of NoView for specified viewer events.</summary>
    ToggleNoView = 256,
    /// <summary>Do not permit the annotation contents to be changed.</summary>
    LockedContents = 512
}
