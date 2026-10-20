using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using KillerPdf.Engine.Documents;

namespace KillerPdf.Engine.Signing;

/// <summary>Verifies detached CMS signatures over bytes reconstructed from PDF byte ranges.</summary>
public static class PdfSignatureVerifier
{
    /// <summary>Verifies signature structure and cryptographic integrity without checking trust.</summary>
    public static PdfSignatureVerificationResult VerifyIntegrity(
        PdfDocument document, PdfSignatureInfo signature) =>
        Verify(document, signature, checkCertificateTrust: false);

    /// <summary>Verifies signature integrity and evaluates the signing certificate's system trust.</summary>
    public static PdfSignatureVerificationResult VerifyTrust(
        PdfDocument document, PdfSignatureInfo signature) =>
        Verify(document, signature, checkCertificateTrust: true);

    private static PdfSignatureVerificationResult Verify(
        PdfDocument document, PdfSignatureInfo signature, bool checkCertificateTrust)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(signature);
        bool structurallyValid = signature.IsSigned
            && signature.HasValidByteRange
            && signature.HasValidCmsEncoding;
        if (!structurallyValid)
            return new PdfSignatureVerificationResult
            {
                IsStructurallyValid = false,
                CertificateTrustWasChecked = checkCertificateTrust,
                Error = "The signature does not contain a valid byte range and CMS value."
            };
        try
        {
            byte[] content = PdfSignatureReader.GetSignedContent(document, signature);
            var cms = new SignedCms(new ContentInfo(content), detached: true);
            cms.Decode(signature.Cms.Span);
            cms.CheckSignature(verifySignatureOnly: true);
            if (checkCertificateTrust)
            {
                try
                {
                    cms.CheckSignature(verifySignatureOnly: false);
                }
                catch (CryptographicException exception)
                {
                    return new PdfSignatureVerificationResult
                    {
                        IsStructurallyValid = true,
                        IsCryptographicallyValid = true,
                        CertificateTrustWasChecked = true,
                        Error = exception.Message
                    };
                }
            }
            return new PdfSignatureVerificationResult
            {
                IsStructurallyValid = true,
                IsCryptographicallyValid = true,
                CertificateTrustWasChecked = checkCertificateTrust,
                IsCertificateTrusted = checkCertificateTrust
            };
        }
        catch (CryptographicException exception)
        {
            return new PdfSignatureVerificationResult
            {
                IsStructurallyValid = true,
                CertificateTrustWasChecked = checkCertificateTrust,
                Error = exception.Message
            };
        }
    }
}
