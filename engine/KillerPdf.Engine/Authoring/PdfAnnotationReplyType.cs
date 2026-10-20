namespace KillerPdf.Engine.Authoring;

/// <summary>How a text-note annotation relates to the annotation it references.</summary>
public enum PdfAnnotationReplyType
{
    /// <summary>The annotation is a direct reply in a discussion thread.</summary>
    Reply,
    /// <summary>The annotation is grouped with the referenced annotation.</summary>
    Group
}

internal static class PdfAnnotationReplyTypeNames
{
    internal static string Name(PdfAnnotationReplyType value) => value switch
    {
        PdfAnnotationReplyType.Reply => "R",
        PdfAnnotationReplyType.Group => "Group",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
