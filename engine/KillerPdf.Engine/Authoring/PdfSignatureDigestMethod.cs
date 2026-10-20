namespace KillerPdf.Engine.Authoring;

/// <summary>A digest algorithm permitted by a signature field seed value.</summary>
public enum PdfSignatureDigestMethod
{
    /// <summary>SHA-256.</summary>
    Sha256,
    /// <summary>SHA-384.</summary>
    Sha384,
    /// <summary>SHA-512.</summary>
    Sha512
}
