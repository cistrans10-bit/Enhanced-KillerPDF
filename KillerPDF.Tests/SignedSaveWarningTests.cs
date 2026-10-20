using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KillerPdf.Engine.Authoring;
using KillerPDF.Services;
using KillerPDF.Services.Signing;
using Xunit;

namespace KillerPDF.Tests;

public sealed class SignedSaveWarningTests
{
    [Fact]
    public void HasSignedSignatures_IsTrueOnlyWhileTheFileCarriesASignature()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=KillerPDF Signed Save Test",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        string directory = Path.Combine(Path.GetTempPath(), $"killerpdf-signed-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string unsigned = Path.Combine(directory, "unsigned.pdf");
        string signed = Path.Combine(directory, "signed.pdf");

        try
        {
            File.WriteAllBytes(unsigned, new PdfDocumentBuilder().AddBlankPage(200, 300).Build());
            new PdfSigner().Sign(
                unsigned,
                signed,
                certificate,
                new PdfSigner.SignInfo("Approved", "Seattle", "steve@example.test"));

            Assert.False(PdfEngineIntegration.HasSignedSignatures(unsigned));
            Assert.True(PdfEngineIntegration.HasSignedSignatures(signed));

            PdfEngineIntegration.ClearInvalidatedSignatures(signed);

            Assert.False(PdfEngineIntegration.HasSignedSignatures(signed));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
