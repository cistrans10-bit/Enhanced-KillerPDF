using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Signing;
using KillerPdf.Engine.Writing;
using Xunit;

namespace KillerPdf.Engine.Tests.Signing;

public sealed class PdfSignatureInvalidationWriterTests
{
    [Fact]
    public void ClearSignatureValues_PreservesEmptyFieldAndAllowsRewrite()
    {
        byte[] signed = PdfDetachedSignatureWriter.Sign(
            PdfDocument.Open(new PdfDocumentBuilder().AddBlankPage().Build()),
            _ => [0x30, 0x00],
            new PdfSignatureOptions
            {
                CertificationPermission =
                    PdfSignatureCertificationPermission.FormFillingAndSignatures
            });

        byte[] cleared = PdfSignatureInvalidationWriter.ClearSignatureValues(
            PdfDocument.Open(signed));
        PdfDocument reopened = PdfDocument.Open(cleared);
        PdfSignatureInfo signature = Assert.Single(PdfSignatureReader.Read(reopened));

        Assert.False(signature.IsSigned);
        Assert.Null(PdfSignatureReader.ReadCertificationPermission(reopened));
        Assert.True(cleared.AsSpan(0, signed.Length).SequenceEqual(signed));
        Assert.NotEmpty(PdfDocumentWriter.Write(reopened));
    }

    [Fact]
    public void ClearSignatureValues_ReturnsUnchangedDocumentWithoutSignatureState()
    {
        byte[] source = new PdfDocumentBuilder().AddBlankPage().Build();

        Assert.Equal(source, PdfSignatureInvalidationWriter.ClearSignatureValues(
            PdfDocument.Open(source)));
    }
}
