# Signing and encryption

The engine supports password security, detached CMS signatures, certification policies, field locks, signature discovery, cryptographic verification, and signed revision analysis.

## Create a password-protected document

```csharp
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Security;

var encryption = new PdfPasswordEncryptionOptions
{
    UserPassword = "reader password",
    OwnerPassword = "owner password",
    AllowContentCopying = false,
    AllowDocumentModification = false,
    AllowHighQualityPrinting = true
};

byte[] protectedPdf = new PdfDocumentBuilder()
    .SetPasswordEncryption(encryption)
    .AddBlankPage()
    .Build();
```

New documents use PDF 2.0 AES-256 protection. The engine can also read supported RC4 and AES-128 documents and preserve authenticated security during incremental updates and rewrites.

## Open encrypted input

```csharp
using KillerPdf.Engine.Documents;

PdfDocument document = PdfDocument.Open(source, password);
Console.WriteLine(document.PasswordAuthenticationRole);
Console.WriteLine(document.DeclaredPermissions?.AllowContentCopying);
```

Honor the authenticated permission set in your application workflow. An owner password grants unrestricted access. A user password may limit printing, copying, assembly, forms, annotations, or general modification.

## Sign with detached CMS

`PdfDetachedSignatureWriter` prepares the PDF signature revision and calls your application back to produce the detached CMS value. Your application owns certificate selection, private-key access, timestamp service policy, and trust configuration.

```csharp
using KillerPdf.Engine.Signing;

var options = new PdfSignatureOptions
{
    FieldName = "Signature1",
    SignerName = "Example signer",
    Reason = "Approved",
    DigestMethod = PdfSignatureDigestMethod.Sha256
};
```

Use the `Sign` overload that matches your certificate and CMS provider. Do not copy a private key into the PDF or into application logs.

## Read and verify signatures

```csharp
IReadOnlyList<PdfSignatureInfo> signatures = PdfSignatureReader.Read(document);

foreach (PdfSignatureInfo signature in signatures)
{
    PdfSignatureVerificationResult integrity =
        PdfSignatureVerifier.VerifyIntegrity(document, signature);
    Console.WriteLine(integrity.IsCryptographicallyValid);
}
```

Integrity verification answers whether the signed bytes match the signature. Trust verification additionally evaluates the certificate chain under the trust policy supplied by your application.

## Analyze later revisions

`PdfSignedRevisionAnalyzer` identifies changes after a signature and evaluates them against certification permissions and field locks. This is different from a simple cryptographic pass or fail result. A signature can remain cryptographically intact while later revisions add changes that violate the declared policy.

## Security boundaries

- Treat PDF passwords as sensitive data and avoid retaining them longer than needed.
- Keep private keys in the operating system certificate store, hardware, or another protected signing provider.
- Make trust policy explicit. Do not treat any embedded certificate as trusted by default.
- Validate signed output with an independent CMS implementation before release.
