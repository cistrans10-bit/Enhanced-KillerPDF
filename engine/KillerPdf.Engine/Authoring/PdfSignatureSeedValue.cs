namespace KillerPdf.Engine.Authoring;

/// <summary>Signing constraints stored with an unsigned signature field.</summary>
public sealed record PdfSignatureSeedValue
{
    /// <summary>Gets the permitted signing handler.</summary>
    public PdfSignatureHandler? Handler { get; init; }
    /// <summary>Gets whether the handler constraint is mandatory.</summary>
    public bool RequireHandler { get; init; }
    /// <summary>Gets the minimum or required seed-value parser version.</summary>
    public PdfSignatureSeedParserVersion? ParserVersion { get; init; }
    /// <summary>Gets whether the parser-version constraint is mandatory.</summary>
    public bool RequireParserVersion { get; init; }
    /// <summary>Gets the permitted signature encodings.</summary>
    public IReadOnlyList<PdfSignatureSubFilter>? SubFilters { get; init; }
    /// <summary>Gets whether a permitted signature encoding is mandatory.</summary>
    public bool RequireSubFilter { get; init; }
    /// <summary>Gets the permitted digest algorithms.</summary>
    public IReadOnlyList<PdfSignatureDigestMethod>? DigestMethods { get; init; }
    /// <summary>Gets whether a permitted digest algorithm is mandatory.</summary>
    public bool RequireDigestMethod { get; init; }
    /// <summary>Gets whether the signer should embed revocation information.</summary>
    public bool AddRevocationInformation { get; init; }
    /// <summary>Gets whether embedded revocation information is mandatory.</summary>
    public bool RequireRevocationInformation { get; init; }
    /// <summary>Gets the permitted signing reasons.</summary>
    public IReadOnlyList<string>? Reasons { get; init; }
    /// <summary>Gets whether a permitted signing reason is mandatory.</summary>
    public bool RequireReason { get; init; }
    /// <summary>Gets the permitted legal attestations.</summary>
    public IReadOnlyList<string>? LegalAttestations { get; init; }
    /// <summary>Gets whether a permitted legal attestation is mandatory.</summary>
    public bool RequireLegalAttestation { get; init; }
    /// <summary>Gets the permitted certification change level.</summary>
    public PdfSignatureCertificationPermission? CertificationPermission { get; init; }
    /// <summary>Gets the timestamp-server constraint.</summary>
    public PdfSignatureTimestamp? Timestamp { get; init; }
    /// <summary>Gets the signer-certificate constraints.</summary>
    public PdfSignatureCertificateSeed? Certificate { get; init; }
    /// <summary>Gets the permitted document-lock intent.</summary>
    public PdfSignatureDocumentLockIntent? DocumentLockIntent { get; init; }
    /// <summary>Gets whether the document-lock intent is mandatory.</summary>
    public bool RequireDocumentLockIntent { get; init; }
    /// <summary>Gets the required named signature appearance.</summary>
    public string? AppearanceName { get; init; }
    /// <summary>Gets whether the named appearance is mandatory.</summary>
    public bool RequireAppearance { get; init; }
}
