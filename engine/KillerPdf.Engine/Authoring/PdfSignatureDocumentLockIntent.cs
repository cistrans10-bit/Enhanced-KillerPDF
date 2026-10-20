namespace KillerPdf.Engine.Authoring;

/// <summary>Whether the signing interface should lock the document after signing.</summary>
public enum PdfSignatureDocumentLockIntent
{
    /// <summary>Lets the signing handler determine whether to lock the document.</summary>
    Automatic,
    /// <summary>Requests document locking after signing.</summary>
    Lock,
    /// <summary>Requests that the document remain unlocked after signing.</summary>
    DoNotLock
}
