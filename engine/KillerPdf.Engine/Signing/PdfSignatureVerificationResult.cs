namespace KillerPdf.Engine.Signing;

/// <summary>Cryptographic verification outcome for one structurally inspected signature.</summary>
public sealed record PdfSignatureVerificationResult
{
    /// <summary>Gets whether the signature dictionary and byte ranges are structurally valid.</summary>
    public bool IsStructurallyValid { get; init; }
    /// <summary>Gets whether the cryptographic signature matches the signed bytes.</summary>
    public bool IsCryptographicallyValid { get; init; }
    /// <summary>Gets whether certificate-chain trust was evaluated.</summary>
    public bool CertificateTrustWasChecked { get; init; }
    /// <summary>Gets whether the signing certificate chains to a trusted root.</summary>
    public bool IsCertificateTrusted { get; init; }
    /// <summary>Gets the verification failure message, if any.</summary>
    public string? Error { get; init; }
}
