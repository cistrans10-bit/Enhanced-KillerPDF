using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Signing;
using KillerPDF.Services.Signing;
using Xunit;

namespace KillerPDF.Tests;

public sealed class PdfSignerTests
{
    [Fact]
    public void Sign_WritesVerifiableEngineSignatureAndMetadata()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=KillerPDF Application Signing Test",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        string directory = Path.Combine(Path.GetTempPath(), $"killerpdf-sign-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory, "input.pdf");
        string output = Path.Combine(directory, "signed.pdf");

        try
        {
            File.WriteAllBytes(input, new PdfDocumentBuilder().AddBlankPage(200, 300).Build());
            new PdfSigner().Sign(
                input,
                output,
                certificate,
                new PdfSigner.SignInfo("Approved", "Seattle", "steve@example.test"));

            PdfDocument signed = PdfDocument.Open(File.ReadAllBytes(output));
            PdfSignatureInfo signature = Assert.Single(PdfSignatureReader.Read(signed));
            Assert.True(signature.IsSigned);
            Assert.Equal("ETSI.CAdES.detached", signature.SubFilter);
            Assert.NotEmpty(signature.Cms.ToArray());
            Assert.True(PdfSignatureVerifier.VerifyIntegrity(signed, signature).IsCryptographicallyValid);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
