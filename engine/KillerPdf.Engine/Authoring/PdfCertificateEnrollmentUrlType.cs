namespace KillerPdf.Engine.Authoring;

/// <summary>The purpose of a certificate enrollment URL.</summary>
public enum PdfCertificateEnrollmentUrlType
{
    /// <summary>An interactive HTML enrollment page.</summary>
    Html,
    /// <summary>A web-service endpoint used by the signing handler.</summary>
    SignatureService
}
