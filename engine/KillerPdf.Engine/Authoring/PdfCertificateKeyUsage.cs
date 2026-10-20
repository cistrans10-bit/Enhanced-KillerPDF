namespace KillerPdf.Engine.Authoring;

/// <summary>Required, forbidden, or unrestricted X.509 key-usage bits for a signing certificate.</summary>
public sealed record PdfCertificateKeyUsage
{
    /// <summary>Gets whether the digital-signature usage is required, forbidden, or unrestricted.</summary>
    public bool? DigitalSignature { get; init; }
    /// <summary>Gets whether the non-repudiation usage is required, forbidden, or unrestricted.</summary>
    public bool? NonRepudiation { get; init; }
    /// <summary>Gets whether the key-encipherment usage is required, forbidden, or unrestricted.</summary>
    public bool? KeyEncipherment { get; init; }
    /// <summary>Gets whether the data-encipherment usage is required, forbidden, or unrestricted.</summary>
    public bool? DataEncipherment { get; init; }
    /// <summary>Gets whether the key-agreement usage is required, forbidden, or unrestricted.</summary>
    public bool? KeyAgreement { get; init; }
    /// <summary>Gets whether certificate signing is required, forbidden, or unrestricted.</summary>
    public bool? KeyCertificateSigning { get; init; }
    /// <summary>Gets whether revocation-list signing is required, forbidden, or unrestricted.</summary>
    public bool? CertificateRevocationListSigning { get; init; }
    /// <summary>Gets whether encipher-only key agreement is required, forbidden, or unrestricted.</summary>
    public bool? EncipherOnly { get; init; }
    /// <summary>Gets whether decipher-only key agreement is required, forbidden, or unrestricted.</summary>
    public bool? DecipherOnly { get; init; }
}
