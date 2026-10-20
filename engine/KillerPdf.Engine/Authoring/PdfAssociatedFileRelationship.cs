namespace KillerPdf.Engine.Authoring;

/// <summary>Describes how an associated file relates to the PDF or content it accompanies.</summary>
public enum PdfAssociatedFileRelationship
{
    /// <summary>The file is the source material from which the content was created.</summary>
    Source,
    /// <summary>The file contains data used to derive or represent the content.</summary>
    Data,
    /// <summary>The file is an alternative representation of the content.</summary>
    Alternative,
    /// <summary>The file supplements the content.</summary>
    Supplement,
    /// <summary>The file contains an encrypted payload associated with the content.</summary>
    EncryptedPayload,
    /// <summary>The file contains form data.</summary>
    FormData,
    /// <summary>The file defines a schema used by the content.</summary>
    Schema,
    /// <summary>The relationship is unspecified.</summary>
    Unspecified
}
