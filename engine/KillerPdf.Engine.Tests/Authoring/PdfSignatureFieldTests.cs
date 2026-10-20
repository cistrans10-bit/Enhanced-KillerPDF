using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfSignatureFieldTests
{
    [Theory]
    [InlineData(PdfTextFieldAlignment.Left, "3 12 Td")]
    [InlineData(PdfTextFieldAlignment.Center, "50.3 12 Td")]
    [InlineData(PdfTextFieldAlignment.Right, "97.6 12 Td")]
    public void AddSignatureField_MeasuresPromptAlignment(
        PdfTextFieldAlignment alignment, string expectedPosition)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddSignatureField(0, "signature", 0, 0, 160, 36,
                appearanceText: "Sign here", appearanceAlignment: alignment)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));

        Assert.Contains(expectedPosition,
            Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void AddSignatureField_WritesRequiredSigningHandlerSeed()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddSignatureField(0, "signature", 0, 0, 140, 40,
                seedValue: new PdfSignatureSeedValue
                {
                    Handler = PdfSignatureHandler.AdobePpkLite,
                    RequireHandler = true,
                    ParserVersion = PdfSignatureSeedParserVersion.Pdf17,
                    RequireParserVersion = true,
                    AddRevocationInformation = true,
                    RequireRevocationInformation = true
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary seed = ResolveDictionary(document, field[Name("SV")]);

        Assert.Equal("Adobe.PPKLite",
            Assert.IsType<PdfName>(seed[Name("Filter")]).ValueAsLatin1());
        Assert.Equal(2, Assert.IsType<PdfReal>(seed[Name("V")]).Value);
        Assert.True(Assert.IsType<PdfBoolean>(seed[Name("AddRevInfo")]).Value);
        Assert.Equal(37, Assert.IsType<PdfInteger>(seed[Name("Ff")]).Value);
    }

    [Fact]
    public void AddSignatureField_WritesPdf20TimestampAndLegalAttestationSeeds()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddSignatureField(0, "signature", 0, 0, 140, 40,
                seedValue: new PdfSignatureSeedValue
                {
                    ParserVersion = PdfSignatureSeedParserVersion.Pdf20,
                    RequireParserVersion = true,
                    LegalAttestations = ["I have reviewed the document", "I am the author"],
                    RequireLegalAttestation = true,
                    Timestamp = new PdfSignatureTimestamp(
                        "https://timestamp.example.test/rfc3161", Required: true),
                    DocumentLockIntent = PdfSignatureDocumentLockIntent.Lock,
                    RequireDocumentLockIntent = true,
                    AppearanceName = "KillerPDF Approval",
                    RequireAppearance = true
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary seed = ResolveDictionary(document, field[Name("SV")]);
        PdfDictionary timestamp = Assert.IsType<PdfDictionary>(seed[Name("TimeStamp")]);

        Assert.Equal(3, Assert.IsType<PdfReal>(seed[Name("V")]).Value);
        Assert.Equal("SV", Assert.IsType<PdfName>(seed[Name("Type")]).ValueAsLatin1());
        Assert.Equal(404, Assert.IsType<PdfInteger>(seed[Name("Ff")]).Value);
        Assert.Equal(["I have reviewed the document", "I am the author"],
            Assert.IsType<PdfArray>(seed[Name("LegalAttestation")])
                .Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
        Assert.Equal("https://timestamp.example.test/rfc3161",
            Encoding.Latin1.GetString(Assert.IsType<PdfString>(timestamp[Name("URL")]).Bytes.Span));
        Assert.Equal(1, Assert.IsType<PdfInteger>(timestamp[Name("Ff")]).Value);
        Assert.Equal("true",
            Assert.IsType<PdfName>(seed[Name("LockDocument")]).ValueAsLatin1());
        Assert.Equal("KillerPDF Approval",
            DecodeUnicode(Assert.IsType<PdfString>(seed[Name("AppearanceFilter")])));
    }

    [Fact]
    public void AddSignatureField_WritesRequiredCertificateKeyUsageSeed()
    {
        byte[] issuerCertificate = CreateTestCertificate();
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddSignatureField(0, "signature", 0, 0, 140, 40,
                seedValue: new PdfSignatureSeedValue
                {
                    Certificate = new PdfSignatureCertificateSeed
                    {
                        SubjectCertificates = [issuerCertificate],
                        RequireSubject = true,
                        IssuerCertificates = [issuerCertificate],
                        RequireIssuer = true,
                        CertificatePolicyObjectIdentifiers = ["2.16.840.1.113733.1.7.1.1"],
                        RequireCertificatePolicy = true,
                        SubjectDistinguishedNames =
                        [
                            new PdfCertificateDistinguishedName(
                                new Dictionary<string, string>
                                {
                                    ["cn"] = "KillerPDF Signer",
                                    ["2.5.4.10"] = "Killer Tools"
                                })
                        ],
                        RequireSubjectDistinguishedName = true,
                        KeyUsages =
                        [
                            new PdfCertificateKeyUsage
                            {
                                DigitalSignature = true,
                                KeyCertificateSigning = false
                            },
                            new PdfCertificateKeyUsage
                            {
                                NonRepudiation = true,
                                KeyEncipherment = false
                            }
                        ],
                        RequireKeyUsage = true,
                        EnrollmentUrl = "https://signing.example.test/enroll",
                        EnrollmentUrlType = PdfCertificateEnrollmentUrlType.SignatureService,
                        RequireEnrollmentUrl = true
                    }
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary certificate = Assert.IsType<PdfDictionary>(
            ResolveDictionary(document, field[Name("SV")])[Name("Cert")]);

        Assert.Equal("SVCert", Assert.IsType<PdfName>(certificate[Name("Type")]).ValueAsLatin1());
        Assert.Equal(111, Assert.IsType<PdfInteger>(certificate[Name("Ff")]).Value);
        Assert.Equal(issuerCertificate, Assert.IsType<PdfString>(
            Assert.Single(Assert.IsType<PdfArray>(certificate[Name("Subject")]))).Bytes.ToArray());
        Assert.Equal(issuerCertificate, Assert.IsType<PdfString>(
            Assert.Single(Assert.IsType<PdfArray>(certificate[Name("Issuer")]))).Bytes.ToArray());
        Assert.Equal("2.16.840.1.113733.1.7.1.1", Encoding.Latin1.GetString(
            Assert.IsType<PdfString>(Assert.Single(
                Assert.IsType<PdfArray>(certificate[Name("OID")]))).Bytes.Span));
        PdfDictionary subjectDn = Assert.IsType<PdfDictionary>(Assert.Single(
            Assert.IsType<PdfArray>(certificate[Name("SubjectDN")])));
        Assert.Equal("KillerPDF Signer",
            DecodeUnicode(Assert.IsType<PdfString>(subjectDn[Name("cn")])));
        Assert.Equal("Killer Tools",
            DecodeUnicode(Assert.IsType<PdfString>(subjectDn[Name("2.5.4.10")])));
        Assert.Equal("https://signing.example.test/enroll", Encoding.Latin1.GetString(
            Assert.IsType<PdfString>(certificate[Name("URL")]).Bytes.Span));
        Assert.Equal("ASSP",
            Assert.IsType<PdfName>(certificate[Name("URLType")]).ValueAsLatin1());
        Assert.Equal(["1XXXX0XXX", "X10XXXXXX"],
            Assert.IsType<PdfArray>(certificate[Name("KeyUsage")])
                .Select(value => Encoding.Latin1.GetString(
                    Assert.IsType<PdfString>(value).Bytes.Span)));
    }

    [Fact]
    public void AddSignatureField_ValidatesHandlerAndPromptAlignment()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "handler", 0, 0, 100, 30,
            seedValue: new PdfSignatureSeedValue { RequireHandler = true }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "version", 0, 0, 100, 30,
            seedValue: new PdfSignatureSeedValue { RequireParserVersion = true }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "revocation", 0, 0, 100, 30,
            seedValue: new PdfSignatureSeedValue { RequireRevocationInformation = true }));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddSignatureField(
            0, "alignment", 0, 0, 100, 30,
            appearanceAlignment: (PdfTextFieldAlignment)9));
    }

    [Fact]
    public void AddSignatureField_WritesCustomPromptStyle()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddSignatureField(0, "signature", 0, 0, 160, 36,
                appearanceText: "Sign here", appearanceStyle: new PdfFormFieldAppearanceStyle
                {
                    BackgroundColor = new PdfRgbColor(1, 0.95, 0.8),
                    BorderColor = new PdfRgbColor(0.6, 0.35, 0.1),
                    TextColor = new PdfRgbColor(0.4, 0.15, 0.05),
                    BorderWidth = 2
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream appearance = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(field[Name("AP")])[Name("N")])));
        string content = Encoding.ASCII.GetString(appearance.EncodedData.Span);

        Assert.Contains("1 0.95 0.8 rg", content);
        Assert.Contains("0.6 0.35 0.1 RG", content);
        Assert.Contains("0.4 0.15 0.05 rg", content);
        Assert.Contains("2 w", content);
    }

    [Fact]
    public void AddSignatureField_WritesUnsignedWidgetAndAcroFormFlags()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddSignatureField(0, "approval-signature", 72, 100, 220, 60,
                new PdfFormFieldMetadata
                {
                    Tooltip = "Approval signature",
                    MappingName = "approval_signature"
                },
                new PdfFormFieldOptions { Required = true })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary acroForm = Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")]);
        PdfDictionary field = ResolveDictionary(document, Assert.IsType<PdfArray>(acroForm[Name("Fields")])[0]);

        Assert.Equal(1, Assert.IsType<PdfInteger>(acroForm[Name("SigFlags")]).Value);
        Assert.Equal("Sig", Assert.IsType<PdfName>(field[Name("FT")]).ValueAsLatin1());
        Assert.Equal(2, Assert.IsType<PdfInteger>(field[Name("Ff")]).Value);
        Assert.False(field.ContainsKey(Name("V")));
        Assert.Equal("Approval signature", DecodeUnicode(Assert.IsType<PdfString>(field[Name("TU")])));
        Assert.IsType<PdfIndirectReference>(field[Name("P")]);
    }

    [Fact]
    public void AddSignatureField_UsesSharedFieldNameValidation()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "duplicate", 0, 0, 100, 20);
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "duplicate", 0, 30, 100, 30));
        Assert.Throws<ArgumentException>(() => new PdfDocumentBuilder().AddBlankPage()
            .AddSignatureField(0, "bad..name", 0, 0, 100, 30));
    }

    [Fact]
    public void AddSignatureField_WritesVisibleUnsignedAppearanceText()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddSignatureField(0, "signature", 0, 0, 180, 40,
                appearanceText: "Sign here", fontSize: 11)
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary signature = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfStream stream = Assert.IsType<PdfStream>(document.Resolve(
            Assert.IsType<PdfIndirectReference>(
                Assert.IsType<PdfDictionary>(signature[Name("AP")])[Name("N")])));

        Assert.Contains("Sign here", Encoding.Latin1.GetString(stream.EncodedData.Span));
        Assert.True(Assert.IsType<PdfDictionary>(stream.Dictionary[Name("Resources")])
            .ContainsKey(Name("Font")));
    }

    [Fact]
    public void AddSignatureField_ValidatesAppearanceText()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();

        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "empty", 0, 0, 100, 30, appearanceText: " "));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "unicode", 0, 0, 100, 30, appearanceText: "签名"));
    }

    [Theory]
    [InlineData(PdfSignatureLockAction.Include, "Include")]
    [InlineData(PdfSignatureLockAction.Exclude, "Exclude")]
    public void AddSignatureField_WritesValidatedFieldLock(
        PdfSignatureLockAction action, string expectedAction)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextField(0, "name", 0, 0, 100, 20)
            .AddCheckBox(0, "approved", 0, 30, 20, 20)
            .AddSignatureField(0, "signature", 0, 60, 140, 40,
                fieldLock: new PdfSignatureFieldLock(action, ["name", "approved"],
                    PdfSignatureLockPermission.FormFillingAndSignatures))
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfArray fields = Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")]);
        PdfDictionary signature = ResolveDictionary(document, fields[2]);
        PdfDictionary fieldLock = ResolveDictionary(document, signature[Name("Lock")]);

        Assert.Equal("SigFieldLock", Assert.IsType<PdfName>(fieldLock[Name("Type")]).ValueAsLatin1());
        Assert.Equal(expectedAction, Assert.IsType<PdfName>(fieldLock[Name("Action")]).ValueAsLatin1());
        Assert.Equal(2, Assert.IsType<PdfInteger>(fieldLock[Name("P")]).Value);
        Assert.Equal(["name", "approved"], Assert.IsType<PdfArray>(fieldLock[Name("Fields")])
            .Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
    }

    [Fact]
    public void AddSignatureField_ValidatesLockShapeAndFieldNames()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage()
            .AddTextField(0, "name", 0, 0, 100, 20);
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "all", 0, 30, 100, 30,
            fieldLock: new PdfSignatureFieldLock(PdfSignatureLockAction.All, ["name"])));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "empty", 0, 30, 100, 30,
            fieldLock: new PdfSignatureFieldLock(PdfSignatureLockAction.Include)));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "missing", 0, 30, 100, 30,
            fieldLock: new PdfSignatureFieldLock(PdfSignatureLockAction.Exclude, ["missing"])));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddSignatureField(
            0, "permission", 0, 30, 100, 30,
            fieldLock: new PdfSignatureFieldLock(PdfSignatureLockAction.All,
                Permission: (PdfSignatureLockPermission)4)));
    }

    [Fact]
    public void AddSignatureField_WritesDigestAndReasonSeedValues()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddSignatureField(0, "signature", 0, 0, 140, 40,
                seedValue: new PdfSignatureSeedValue
                {
                    SubFilters = [
                        PdfSignatureSubFilter.AdobePkcs7Detached,
                        PdfSignatureSubFilter.EtsiCadesDetached],
                    RequireSubFilter = true,
                    DigestMethods = [PdfSignatureDigestMethod.Sha256, PdfSignatureDigestMethod.Sha512],
                    RequireDigestMethod = true,
                    Reasons = ["Approved", "Reviewed"],
                    RequireReason = true,
                    CertificationPermission =
                        PdfSignatureCertificationPermission.FormFillingAndSignatures
                })
            .Build());
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary signature = ResolveDictionary(document, Assert.IsType<PdfArray>(
            Assert.IsType<PdfDictionary>(catalog[Name("AcroForm")])[Name("Fields")])[0]);
        PdfDictionary seed = ResolveDictionary(document, signature[Name("SV")]);

        Assert.Equal(74, Assert.IsType<PdfInteger>(seed[Name("Ff")]).Value);
        Assert.Equal(["adbe.pkcs7.detached", "ETSI.CAdES.detached"],
            Assert.IsType<PdfArray>(seed[Name("SubFilter")])
                .Select(value => Assert.IsType<PdfName>(value).ValueAsLatin1()));
        Assert.Equal(["SHA256", "SHA512"], Assert.IsType<PdfArray>(seed[Name("DigestMethod")])
            .Select(value => Assert.IsType<PdfName>(value).ValueAsLatin1()));
        Assert.Equal(["Approved", "Reviewed"], Assert.IsType<PdfArray>(seed[Name("Reasons")])
            .Select(value => DecodeUnicode(Assert.IsType<PdfString>(value))));
        Assert.Equal(2, Assert.IsType<PdfInteger>(
            Assert.IsType<PdfDictionary>(seed[Name("MDP")])[Name("P")]).Value);
    }

    [Fact]
    public void AddSignatureField_ValidatesSeedValueShape()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();

        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "empty", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue()));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "digest", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                RequireDigestMethod = true
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "subfilter", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                RequireSubFilter = true
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "reasons", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                Reasons = ["Approved", "Approved"]
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "attestation", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                RequireLegalAttestation = true
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "timestamp", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                Timestamp = new PdfSignatureTimestamp("file:///timestamp")
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "key-usage", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                Certificate = new PdfSignatureCertificateSeed
                {
                    KeyUsages = [new PdfCertificateKeyUsage()]
                }
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "policy", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                Certificate = new PdfSignatureCertificateSeed
                {
                    CertificatePolicyObjectIdentifiers = ["2.5.29.32.0"]
                }
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "issuer", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                Certificate = new PdfSignatureCertificateSeed
                {
                    IssuerCertificates = [[1, 2, 3]]
                }
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "certificate", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                Certificate = new PdfSignatureCertificateSeed()
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "subject-dn", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                Certificate = new PdfSignatureCertificateSeed
                {
                    SubjectDistinguishedNames =
                    [
                        new PdfCertificateDistinguishedName(
                            new Dictionary<string, string> { ["invalid key"] = "value" })
                    ]
                }
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "url-type", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                Certificate = new PdfSignatureCertificateSeed
                {
                    EnrollmentUrlType = PdfCertificateEnrollmentUrlType.SignatureService
                }
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "document-lock", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                RequireDocumentLockIntent = true
            }));
        Assert.Throws<ArgumentException>(() => builder.AddSignatureField(
            0, "appearance", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                RequireAppearance = true
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddSignatureField(
            0, "permission", 0, 0, 100, 30, seedValue: new PdfSignatureSeedValue
            {
                CertificationPermission = (PdfSignatureCertificationPermission)9
            }));
    }

    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static byte[] CreateTestCertificate()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=KillerPDF Test Issuer", key,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0,
            critical: true));
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return certificate.Export(X509ContentType.Cert);
    }
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
