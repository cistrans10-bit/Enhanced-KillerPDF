namespace KillerPdf.Engine.Authoring;

/// <summary>Constraints on the X.509 certificate used to sign a field.</summary>
public sealed record PdfSignatureCertificateSeed
{
    /// <summary>Gets the permitted DER-encoded end-entity certificates.</summary>
    public IReadOnlyList<byte[]>? SubjectCertificates { get; init; }
    /// <summary>Gets whether the end-entity certificate constraint is mandatory.</summary>
    public bool RequireSubject { get; init; }
    /// <summary>Gets the permitted DER-encoded issuer certificates.</summary>
    public IReadOnlyList<byte[]>? IssuerCertificates { get; init; }
    /// <summary>Gets whether the issuer constraint is mandatory.</summary>
    public bool RequireIssuer { get; init; }
    /// <summary>Gets the permitted certificate-policy object identifiers.</summary>
    public IReadOnlyList<string>? CertificatePolicyObjectIdentifiers { get; init; }
    /// <summary>Gets whether the certificate-policy constraint is mandatory.</summary>
    public bool RequireCertificatePolicy { get; init; }
    /// <summary>Gets the permitted subject distinguished-name patterns.</summary>
    public IReadOnlyList<PdfCertificateDistinguishedName>? SubjectDistinguishedNames { get; init; }
    /// <summary>Gets whether a subject distinguished-name match is mandatory.</summary>
    public bool RequireSubjectDistinguishedName { get; init; }
    /// <summary>Gets the permitted X.509 key-usage combinations.</summary>
    public IReadOnlyList<PdfCertificateKeyUsage>? KeyUsages { get; init; }
    /// <summary>Gets whether a key-usage match is mandatory.</summary>
    public bool RequireKeyUsage { get; init; }
    /// <summary>Gets the certificate-enrollment or acquisition URL.</summary>
    public string? EnrollmentUrl { get; init; }
    /// <summary>Gets how the enrollment URL should be interpreted.</summary>
    public PdfCertificateEnrollmentUrlType? EnrollmentUrlType { get; init; }
    /// <summary>Gets whether the enrollment URL is mandatory.</summary>
    public bool RequireEnrollmentUrl { get; init; }
}
