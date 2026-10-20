using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.CrossReference;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Security;
using KillerPdf.Engine.Writing;
using KillerPdf.Engine.Syntax;
using KillerPdf.Engine.Signing;
using Xunit;

namespace KillerPdf.Engine.Tests.Writing;

public sealed class PdfDocumentWriterTests
{
    [Fact]
    public void Write_RejectsUndefinedPolicyValues()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf());

        Assert.Throws<ArgumentOutOfRangeException>(() => PdfDocumentWriter.Write(
            document, new PdfDocumentWriteOptions
            {
                MetadataPolicy = (PdfMetadataPolicy)int.MaxValue
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfDocumentWriter.Write(
            document, new PdfDocumentWriteOptions
            {
                CrossReferenceFormat = (PdfCrossReferenceFormat)int.MaxValue
            }));
    }

    [Fact]
    public void Write_RejectsDefaultTargetVersion()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf());

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PdfDocumentWriter.Write(document, new PdfDocumentWriteOptions
            {
                TargetVersion = default(PdfVersion)
            }));

        Assert.Contains("target PDF version", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ProducesACompletePdfThatTheEngineCanReopen()
    {
        PdfDocument original = PdfDocument.Open(SourcePdf());

        byte[] rewrittenBytes = PdfDocumentWriter.Write(original);
        PdfDocument rewritten = PdfDocument.Open(rewrittenBytes);

        Assert.Equal(original.Header.Version, rewritten.Header.Version);
        var catalog = Assert.IsType<PdfDictionary>(rewritten.Resolve(new PdfIndirectReference(1, 0)));
        Assert.Equal("Catalog", Assert.IsType<PdfName>(catalog[Name("Type")]).ValueAsLatin1());
        var stream = Assert.IsType<PdfStream>(rewritten.Resolve(2));
        Assert.Equal("Hello", Encoding.ASCII.GetString(stream.EncodedData.Span));
        Assert.Contains("\nxref\n", Encoding.Latin1.GetString(rewrittenBytes), StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", Encoding.Latin1.GetString(rewrittenBytes), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RequiresExplicitConsentToInvalidateExistingSignatures()
    {
        byte[] signedBytes = PdfDetachedSignatureWriter.Sign(
            PdfDocument.Open(new PdfDocumentBuilder().AddBlankPage().Build()),
            _ => [0x30, 0x00], new PdfSignatureOptions { ReservedSignatureSize = 16 });
        PdfDocument signed = PdfDocument.Open(signedBytes);

        Assert.Throws<InvalidOperationException>(() => PdfDocumentWriter.Write(signed));

        PdfDocument rewritten = PdfDocument.Open(PdfDocumentWriter.Write(signed,
            new PdfDocumentWriteOptions { AllowSignatureInvalidation = true }));
        PdfSignatureInfo signature = Assert.Single(PdfSignatureReader.Read(rewritten));
        Assert.False(signature.IsSigned);
        Assert.Null(PdfSignatureReader.ReadCertificationPermission(rewritten));
    }

    [Fact]
    public void Write_RejectsCatalogCertificationEvenWithoutRegisteredField()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder().AddBlankPage().Build());
        PdfIndirectReference catalogReference = Assert.IsType<PdfIndirectReference>(
            document.Trailer[Name("Root")]);
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(document.Resolve(catalogReference));
        var update = new PdfIncrementalUpdateBuilder(document);
        PdfIndirectReference parameters = update.AddObject(new PdfDictionary([
            new(Name("P"), new PdfInteger(1))
        ]));
        PdfIndirectReference transform = update.AddObject(new PdfDictionary([
            new(Name("TransformMethod"), Name("DocMDP")),
            new(Name("TransformParams"), parameters)
        ]));
        PdfIndirectReference signature = update.AddObject(new PdfDictionary([
            new(Name("Type"), Name("Sig")),
            new(Name("Reference"), new PdfArray([transform]))
        ]));
        update.ReplaceObject(catalogReference.ObjectNumber, new PdfDictionary(catalog.Append(
            new KeyValuePair<PdfName, PdfObject>(Name("Perms"), new PdfDictionary([
                new(Name("DocMDP"), signature)
            ])))));
        PdfDocument certified = PdfDocument.Open(update.Build());

        Assert.Throws<InvalidOperationException>(() => PdfDocumentWriter.Write(certified));
        Assert.NotEmpty(PdfDocumentWriter.Write(certified,
            new PdfDocumentWriteOptions { AllowSignatureInvalidation = true }));
    }

    [Fact]
    public void Write_IsByteStableAcrossRepeatedFullRewrites()
    {
        byte[] first = PdfDocumentWriter.Write(PdfDocument.Open(SourcePdf()));
        byte[] second = PdfDocumentWriter.Write(PdfDocument.Open(first));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Write_CanProduceAByteStableCrossReferenceStream()
    {
        var options = new PdfDocumentWriteOptions
        {
            CrossReferenceFormat = PdfCrossReferenceFormat.Stream
        };
        byte[] first = PdfDocumentWriter.Write(PdfDocument.Open(SourcePdf()), options);
        PdfDocument reopened = PdfDocument.Open(first);
        byte[] second = PdfDocumentWriter.Write(reopened, options);

        Assert.Equal(first, second);
        Assert.DoesNotContain("\nxref\n", Encoding.Latin1.GetString(first), StringComparison.Ordinal);
        Assert.Equal(PdfCrossReferenceEntryType.InUse,
            reopened.CrossReferences[3].Type);
        Assert.Equal("XRef", Assert.IsType<PdfName>(
            Assert.IsType<PdfStream>(reopened.Resolve(3)).Dictionary[Name("Type")]).ValueAsLatin1());
    }

    [Fact]
    public void Write_RejectsCrossReferenceStreamBeforePdf15()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf(version: "1.4"));

        Assert.Throws<InvalidOperationException>(() => PdfDocumentWriter.Write(document,
            new PdfDocumentWriteOptions
            {
                CrossReferenceFormat = PdfCrossReferenceFormat.Stream
            }));
    }

    [Fact]
    public void Write_HonorsCatalogVersionOverrideForCrossReferenceStreams()
    {
        PdfDocument document = PdfDocument.Open(
            SourcePdf(version: "1.4", catalogVersion: "1.5"));

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(document,
            new PdfDocumentWriteOptions
            {
                CrossReferenceFormat = PdfCrossReferenceFormat.Stream
            }));

        Assert.Equal(new PdfVersion(1, 4), reopened.Header.Version);
        Assert.True(reopened.CrossReferences.Sections[0].IsStream);
    }

    [Fact]
    public void Write_ResolvesIndirectCatalogVersionNames()
    {
        PdfDocument source = PdfDocument.Open(SourcePdf(version: "1.4"));
        PdfIndirectReference catalogReference = Assert.IsType<PdfIndirectReference>(
            source.Trailer[Name("Root")]);
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(source.Resolve(catalogReference));
        var update = new PdfIncrementalUpdateBuilder(source);
        PdfIndirectReference catalogType = update.AddObject(Name("Catalog"));
        PdfIndirectReference version = update.AddObject(Name("1.5"));
        update.ReplaceObject(catalogReference.ObjectNumber, new PdfDictionary(catalog
            .Where(entry => !entry.Key.Equals(Name("Type")))
            .Append(new KeyValuePair<PdfName, PdfObject>(Name("Type"), catalogType))
            .Append(new KeyValuePair<PdfName, PdfObject>(Name("Version"), version))));
        source = PdfDocument.Open(update.Build());

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(source,
            new PdfDocumentWriteOptions
            {
                CrossReferenceFormat = PdfCrossReferenceFormat.Stream
            }));

        Assert.True(reopened.CrossReferences.Sections[0].IsStream);
    }

    [Fact]
    public void Write_IgnoresInvalidCatalogVersionForClassicTables()
    {
        PdfDocument source = PdfDocument.Open(
            SourcePdf(catalogVersion: "Future"));

        PdfDocument rewritten = PdfDocument.Open(PdfDocumentWriter.Write(source));

        Assert.Equal(source.Header.Version, rewritten.Header.Version);
    }

    [Fact]
    public void Write_CanPackEligibleObjectsIntoAnObjectStream()
    {
        var options = new PdfDocumentWriteOptions
        {
            CrossReferenceFormat = PdfCrossReferenceFormat.Stream,
            UseObjectStreams = true,
            CompressStructuralStreams = true
        };
        byte[] first = PdfDocumentWriter.Write(PdfDocument.Open(SourcePdf()), options);
        PdfDocument reopened = PdfDocument.Open(first);
        byte[] second = PdfDocumentWriter.Write(reopened, options);

        Assert.Equal(first, second);
        Assert.Equal(PdfCrossReferenceEntryType.Compressed,
            reopened.CrossReferences[1].Type);
        Assert.Equal("Catalog", Assert.IsType<PdfName>(
            Assert.IsType<PdfDictionary>(reopened.Resolve(1))[Name("Type")]).ValueAsLatin1());
        Assert.Equal("Hello", Encoding.ASCII.GetString(
            Assert.IsType<PdfStream>(reopened.Resolve(2)).EncodedData.Span));
        Assert.Equal("FlateDecode", Assert.IsType<PdfName>(
            Assert.IsType<PdfStream>(reopened.Resolve(3)).Dictionary[Name("Filter")]).ValueAsLatin1());
        Assert.Equal("FlateDecode", Assert.IsType<PdfName>(
            Assert.IsType<PdfStream>(reopened.Resolve(4)).Dictionary[Name("Filter")]).ValueAsLatin1());
    }

    [Fact]
    public void Write_RequiresCrossReferenceStreamWhenPackingObjects()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf());

        Assert.Throws<InvalidOperationException>(() => PdfDocumentWriter.Write(document,
            new PdfDocumentWriteOptions { UseObjectStreams = true }));
    }

    [Fact]
    public void Write_RequiresCrossReferenceStreamWhenCompressingStructuralStreams()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf());

        Assert.Throws<InvalidOperationException>(() => PdfDocumentWriter.Write(document,
            new PdfDocumentWriteOptions { CompressStructuralStreams = true }));
    }

    [Fact]
    public void Write_BoundsObjectStreamsToOneHundredObjects()
    {
        PdfDocument source = PdfDocument.Open(SourcePdf());
        var update = new PdfIncrementalUpdateBuilder(source);
        PdfIndirectReference[] added = [.. Enumerable.Range(0, 101)
            .Select(index => update.AddObject(new PdfDictionary([
                new(Name("Value"), new PdfInteger(index))
            ])))];
        PdfDocument expanded = PdfDocument.Open(update.Build());

        byte[] rewritten = PdfDocumentWriter.Write(expanded, new PdfDocumentWriteOptions
        {
            CrossReferenceFormat = PdfCrossReferenceFormat.Stream,
            UseObjectStreams = true,
            CompressStructuralStreams = true
        });
        PdfDocument reopened = PdfDocument.Open(rewritten);
        long[] streamNumbers = [.. added.Select(reference =>
                reopened.CrossReferences[reference.ObjectNumber].Field1)
            .Distinct()];

        Assert.Equal(2, streamNumbers.Length);
        Assert.All(streamNumbers, number => Assert.Equal("ObjStm", Assert.IsType<PdfName>(
            Assert.IsType<PdfStream>(reopened.Resolve(checked((int)number)))
                .Dictionary[Name("Type")]).ValueAsLatin1()));
        Assert.Equal(100, Assert.IsType<PdfInteger>(
            Assert.IsType<PdfStream>(reopened.Resolve(checked((int)streamNumbers[0])))
                .Dictionary[Name("N")]).Value);
    }

    [Fact]
    public void Write_LinksFreeCrossReferenceStreamEntries()
    {
        PdfDocument source = PdfDocument.Open(SourcePdfWithObjectNumberGap());

        byte[] rewritten = PdfDocumentWriter.Write(source, new PdfDocumentWriteOptions
        {
            CrossReferenceFormat = PdfCrossReferenceFormat.Stream
        });
        PdfDocument reopened = PdfDocument.Open(rewritten);

        Assert.Equal(PdfCrossReferenceEntryType.Free, reopened.CrossReferences[0].Type);
        Assert.Equal(3, reopened.CrossReferences[0].Field1);
        for (int number = 3; number <= 8; number++)
            Assert.Equal(number + 1, reopened.CrossReferences[number].Field1);
        Assert.Equal(11, reopened.CrossReferences[9].Field1);
        Assert.Equal(0, reopened.CrossReferences[11].Field1);
        Assert.Equal(7, reopened.CrossReferences[5].Field2);
        Assert.Equal(65_535, reopened.CrossReferences[11].Field2);
        Assert.Equal(13, new PdfIncrementalUpdateBuilder(reopened).ReserveObject().ObjectNumber);
    }

    [Fact]
    public void Write_LinksFreeClassicCrossReferenceEntries()
    {
        PdfDocument source = PdfDocument.Open(SourcePdfWithObjectNumberGap());

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(source));

        Assert.Equal(PdfCrossReferenceEntryType.Free, reopened.CrossReferences[0].Type);
        Assert.Equal(3, reopened.CrossReferences[0].Field1);
        for (int number = 3; number <= 8; number++)
            Assert.Equal(number + 1, reopened.CrossReferences[number].Field1);
        Assert.Equal(11, reopened.CrossReferences[9].Field1);
        Assert.Equal(0, reopened.CrossReferences[11].Field1);
        Assert.Equal(7, reopened.CrossReferences[5].Field2);
        Assert.Equal(65_535, reopened.CrossReferences[11].Field2);
        Assert.Equal(12, new PdfIncrementalUpdateBuilder(reopened).ReserveObject().ObjectNumber);
    }

    [Fact]
    public void Write_RequiresPasswordBeforeEncryptedRewrite()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf(" /Encrypt 9 0 R"));

        Assert.Throws<InvalidOperationException>(() => PdfDocumentWriter.Write(document));
    }

    [Fact]
    public void Write_RemoveEncryption_EmitsDecryptedDocumentAndOmitsEncryptionObject()
    {
        byte[] encrypted = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "Decrypted title" })
            .SetPasswordEncryption(new PdfPasswordEncryptionOptions
            {
                UserPassword = "user",
                OwnerPassword = "owner"
            })
            .AddBlankPage()
            .Build();
        PdfDocument authenticated = PdfDocument.Open(encrypted, "owner");
        PdfIndirectReference encryptionReference = Assert.IsType<PdfIndirectReference>(
            authenticated.Trailer[Name("Encrypt")]);

        byte[] rewritten = PdfDocumentWriter.Write(authenticated,
            new PdfDocumentWriteOptions { RemoveEncryption = true });
        PdfDocument reopened = PdfDocument.Open(rewritten);

        Assert.False(reopened.IsEncrypted);
        Assert.False(reopened.Trailer.ContainsKey(Name("Encrypt")));
        Assert.False(reopened.CrossReferences.TryGetValue(
            encryptionReference.ObjectNumber, out PdfCrossReferenceEntry entry)
            && entry.Type != PdfCrossReferenceEntryType.Free);
        Assert.Equal("Decrypted title", PdfDocumentInformation.Read(reopened).Title);
    }

    [Fact]
    public void Write_AllowsVersionUpgradesButRefusesBlindDowngrades()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf(version: "1.7"));

        byte[] upgraded = PdfDocumentWriter.Write(document, new PdfDocumentWriteOptions
        {
            TargetVersion = PdfVersion.Pdf20
        });

        Assert.Equal(PdfVersion.Pdf20, PdfDocument.Open(upgraded).Header.Version);
        Assert.Throws<NotSupportedException>(() => PdfDocumentWriter.Write(
            PdfDocument.Open(SourcePdf()),
            new PdfDocumentWriteOptions { TargetVersion = PdfVersion.Pdf17 }));
    }

    [Fact]
    public void Write_CanRemoveDocumentInformationAndIdentifiersIndependently()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf(" /Info 1 0 R /ID [<01> <02>]"));
        byte[] rewritten = PdfDocumentWriter.Write(document, new PdfDocumentWriteOptions
        {
            MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformation,
            PreserveDocumentIdentifiers = false
        });
        PdfDictionary trailer = PdfDocument.Open(rewritten).Trailer;

        Assert.False(trailer.ContainsKey(Name("Info")));
        Assert.False(trailer.ContainsKey(Name("ID")));
    }

    [Fact]
    public void Write_PhysicallyRemovesDedicatedDocumentInformationObject()
    {
        PdfDocument source = PdfDocument.Open(SourcePdfWithDocumentInformation());

        byte[] rewritten = PdfDocumentWriter.Write(source, new PdfDocumentWriteOptions
        {
            MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformation
        });
        PdfDocument reopened = PdfDocument.Open(rewritten);

        Assert.False(reopened.Trailer.ContainsKey(Name("Info")));
        Assert.Equal(PdfCrossReferenceEntryType.Free, reopened.CrossReferences[3].Type);
        Assert.Equal(1, reopened.CrossReferences[3].Field2);
        Assert.DoesNotContain("private metadata", Encoding.Latin1.GetString(rewritten),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RejectsPhysicalInformationRemovalWhenObjectIsShared()
    {
        PdfDocument source = PdfDocument.Open(SourcePdfWithDocumentInformation(shared: true));

        Assert.Throws<NotSupportedException>(() => PdfDocumentWriter.Write(source,
            new PdfDocumentWriteOptions
            {
                MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformation
            }));
    }

    [Fact]
    public void Write_DoesNotDeleteActiveObjectBehindStaleInformationReference()
    {
        string sourceText = Encoding.Latin1.GetString(SourcePdfWithDocumentInformation())
            .Replace("/Info 3 0 R", "/Info 3 1 R", StringComparison.Ordinal);
        PdfDocument source = PdfDocument.Open(Encoding.Latin1.GetBytes(sourceText));

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(source,
            new PdfDocumentWriteOptions
            {
                MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformation
            }));

        Assert.False(reopened.Trailer.ContainsKey(Name("Info")));
        Assert.Equal(PdfCrossReferenceEntryType.InUse, reopened.CrossReferences[3].Type);
        PdfDictionary retained = Assert.IsType<PdfDictionary>(reopened.Resolve(3));
        Assert.Equal("private metadata", Encoding.Latin1.GetString(
            Assert.IsType<PdfString>(retained[Name("Title")]).Bytes.Span));
    }

    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table)]
    [InlineData(PdfCrossReferenceFormat.Stream)]
    public void Write_CanPhysicallyRemoveDocumentInformationAndCatalogXmp(
        PdfCrossReferenceFormat format)
    {
        const string privateMarker = "private xmp metadata marker";
        PdfDocument source = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = privateMarker,
                Author = "Private Author"
            })
            .AddBlankPage()
            .Build());
        PdfIndirectReference sourceCatalogReference = Assert.IsType<PdfIndirectReference>(
            source.Trailer[Name("Root")]);
        PdfDictionary sourceCatalog = Assert.IsType<PdfDictionary>(
            source.Resolve(sourceCatalogReference));
        PdfIndirectReference metadataReference = Assert.IsType<PdfIndirectReference>(
            sourceCatalog[Name("Metadata")]);

        byte[] rewritten = PdfDocumentWriter.Write(source, new PdfDocumentWriteOptions
        {
            MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformationAndXmp,
            CrossReferenceFormat = format,
            UseObjectStreams = format == PdfCrossReferenceFormat.Stream,
            CompressStructuralStreams = format == PdfCrossReferenceFormat.Stream
        });
        PdfDocument reopened = PdfDocument.Open(rewritten);
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(reopened.Resolve(
            Assert.IsType<PdfIndirectReference>(reopened.Trailer[Name("Root")])));

        Assert.False(reopened.Trailer.ContainsKey(Name("Info")));
        Assert.False(catalog.ContainsKey(Name("Metadata")));
        Assert.Equal(PdfCrossReferenceEntryType.Free,
            reopened.CrossReferences[metadataReference.ObjectNumber].Type);
        Assert.Equal(1, reopened.CrossReferences[metadataReference.ObjectNumber].Field2);
        Assert.DoesNotContain(privateMarker, Encoding.UTF8.GetString(rewritten),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RemovesAliasedInformationAndXmpFromAliasedCatalog()
    {
        const string privateMarker = "aliased private metadata marker";
        byte[] authoredBytes = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = privateMarker })
            .AddBlankPage()
            .Build();
        PdfDocument authored = PdfDocument.Open(authoredBytes);
        PdfIndirectReference catalogReference = Assert.IsType<PdfIndirectReference>(
            authored.Trailer[Name("Root")]);
        PdfIndirectReference infoReference = Assert.IsType<PdfIndirectReference>(
            authored.Trailer[Name("Info")]);
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(
            authored.Resolve(catalogReference));
        PdfIndirectReference metadataReference = Assert.IsType<PdfIndirectReference>(
            catalog[Name("Metadata")]);
        var update = new PdfIncrementalUpdateBuilder(authored);
        PdfIndirectReference infoAlias = update.AddObject(infoReference);
        PdfIndirectReference infoSecondAlias = update.AddObject(infoAlias);
        PdfIndirectReference metadataAlias = update.AddObject(metadataReference);
        PdfIndirectReference metadataSecondAlias = update.AddObject(metadataAlias);
        update.SetDocumentInformation(infoSecondAlias);
        update.ReplaceObject(catalogReference.ObjectNumber,
            new PdfDictionary(catalog.Select(entry => entry.Key.Equals(Name("Metadata"))
                ? new KeyValuePair<PdfName, PdfObject>(entry.Key, metadataSecondAlias)
                : entry)));
        byte[] aliasedBytes = AppendRootAliases(update.Build(), catalogReference);

        byte[] rewritten = PdfDocumentWriter.Write(PdfDocument.Open(aliasedBytes),
            new PdfDocumentWriteOptions
            {
                MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformationAndXmp,
                CrossReferenceFormat = PdfCrossReferenceFormat.Stream,
                UseObjectStreams = true,
                CompressStructuralStreams = true
            });
        PdfDocument reopened = PdfDocument.Open(rewritten);
        PdfDictionary rewrittenCatalog = Assert.IsType<PdfDictionary>(
            ResolveChain(reopened, reopened.Trailer[Name("Root")]));

        Assert.False(reopened.Trailer.ContainsKey(Name("Info")));
        Assert.False(rewrittenCatalog.ContainsKey(Name("Metadata")));
        foreach (PdfIndirectReference removed in new[]
                 {
                     infoReference, infoAlias, infoSecondAlias,
                     metadataReference, metadataAlias, metadataSecondAlias
                 })
            Assert.Equal(PdfCrossReferenceEntryType.Free,
                reopened.CrossReferences[removed.ObjectNumber].Type);
        Assert.DoesNotContain(privateMarker, Encoding.UTF8.GetString(rewritten),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RejectsCatalogXmpRemovalWhenMetadataObjectIsShared()
    {
        PdfDocument authored = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "shared metadata" })
            .AddBlankPage()
            .Build());
        PdfIndirectReference catalogReference = Assert.IsType<PdfIndirectReference>(
            authored.Trailer[Name("Root")]);
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(
            authored.Resolve(catalogReference));
        PdfObject metadata = catalog[Name("Metadata")];
        var sharedCatalog = new PdfDictionary(catalog.Append(
            new KeyValuePair<PdfName, PdfObject>(Name("PrivateMetadata"), metadata)));
        PdfDocument shared = PdfDocument.Open(new PdfIncrementalUpdateBuilder(authored)
            .ReplaceObject(catalogReference.ObjectNumber, sharedCatalog)
            .Build());

        Assert.Throws<NotSupportedException>(() => PdfDocumentWriter.Write(
            shared, new PdfDocumentWriteOptions
            {
                MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformationAndXmp
            }));
    }

    [Fact]
    public void Write_DoesNotDeleteActiveObjectBehindStaleCatalogMetadataReference()
    {
        PdfDocument authored = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "active metadata object" })
            .AddBlankPage()
            .Build());
        PdfIndirectReference catalogReference = Assert.IsType<PdfIndirectReference>(
            authored.Trailer[Name("Root")]);
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(authored.Resolve(catalogReference));
        PdfIndirectReference metadataReference = Assert.IsType<PdfIndirectReference>(
            catalog[Name("Metadata")]);
        PdfDictionary staleCatalog = new(catalog
            .Where(entry => !entry.Key.Equals(Name("Metadata")))
            .Append(new KeyValuePair<PdfName, PdfObject>(Name("Metadata"),
                new PdfIndirectReference(
                    metadataReference.ObjectNumber, metadataReference.Generation + 1))));
        PdfDocument stale = PdfDocument.Open(new PdfIncrementalUpdateBuilder(authored)
            .ReplaceObject(catalogReference.ObjectNumber, staleCatalog)
            .Build());

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(stale,
            new PdfDocumentWriteOptions
            {
                MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformationAndXmp
            }));
        PdfDictionary rewrittenCatalog = Assert.IsType<PdfDictionary>(reopened.Resolve(
            Assert.IsType<PdfIndirectReference>(reopened.Trailer[Name("Root")])));

        Assert.False(rewrittenCatalog.ContainsKey(Name("Metadata")));
        Assert.Equal(PdfCrossReferenceEntryType.InUse,
            reopened.CrossReferences[metadataReference.ObjectNumber].Type);
        Assert.IsType<PdfStream>(reopened.Resolve(metadataReference.ObjectNumber));
    }

    [Fact]
    public void Write_RemovesAllMetadataWithoutRemovingAes256Protection()
    {
        byte[] source = new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata { Title = "encrypted private metadata" })
            .SetPasswordEncryption(new PdfPasswordEncryptionOptions
            {
                UserPassword = "user",
                OwnerPassword = "owner"
            })
            .AddBlankPage()
            .Build();

        byte[] rewritten = PdfDocumentWriter.Write(
            PdfDocument.Open(source, "owner"), new PdfDocumentWriteOptions
            {
                MetadataPolicy = PdfMetadataPolicy.RemoveDocumentInformationAndXmp,
                CrossReferenceFormat = PdfCrossReferenceFormat.Stream,
                UseObjectStreams = true,
                CompressStructuralStreams = true
            });
        PdfDocument reopened = PdfDocument.Open(rewritten, "user");
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(reopened.Resolve(
            Assert.IsType<PdfIndirectReference>(reopened.Trailer[Name("Root")])));

        Assert.True(reopened.IsEncrypted);
        Assert.True(reopened.IsDecrypted);
        Assert.False(reopened.Trailer.ContainsKey(Name("Info")));
        Assert.False(catalog.ContainsKey(Name("Metadata")));
    }

    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table)]
    [InlineData(PdfCrossReferenceFormat.Stream)]
    public void Write_PreservesNonStructuralTrailerEntriesAndDropsStaleChecksum(
        PdfCrossReferenceFormat format)
    {
        PdfDocument source = PdfDocument.Open(SourcePdf(
            " /PrivateState << /Enabled true >> /DocChecksum <01020304>"));

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(source,
            new PdfDocumentWriteOptions { CrossReferenceFormat = format }));

        PdfDictionary state = Assert.IsType<PdfDictionary>(
            reopened.Trailer[Name("PrivateState")]);
        Assert.True(Assert.IsType<PdfBoolean>(state[Name("Enabled")]).Value);
        Assert.False(reopened.Trailer.ContainsKey(Name("DocChecksum")));
        Assert.False(reopened.Trailer.ContainsKey(Name("Prev")));
        Assert.False(reopened.Trailer.ContainsKey(Name("XRefStm")));
    }

    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table, 20)]
    [InlineData(PdfCrossReferenceFormat.Stream, 21)]
    public void Write_PreservesSparseTrailerSizeHighWaterMark(
        PdfCrossReferenceFormat format, int nextObjectNumber)
    {
        PdfDocument source = PdfDocument.Open(SourcePdf(declaredSize: 20));

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(source,
            new PdfDocumentWriteOptions { CrossReferenceFormat = format }));

        Assert.Equal(PdfCrossReferenceEntryType.Free, reopened.CrossReferences[19].Type);
        Assert.Equal(nextObjectNumber,
            new PdfIncrementalUpdateBuilder(reopened).ReserveObject().ObjectNumber);
    }

    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table, 1_000_005)]
    [InlineData(PdfCrossReferenceFormat.Stream, 1_000_006)]
    public void Write_UsesSparseCrossReferenceRangesBeyondReaderSectionLimit(
        PdfCrossReferenceFormat format, int nextObjectNumber)
    {
        PdfDocument source = PdfDocument.Open(SourcePdf(declaredSize: 1_000_005));

        byte[] rewritten = PdfDocumentWriter.Write(source,
            new PdfDocumentWriteOptions { CrossReferenceFormat = format });
        PdfDocument reopened = PdfDocument.Open(rewritten);

        Assert.True(rewritten.Length < 100_000);
        Assert.Equal(PdfCrossReferenceEntryType.Free,
            reopened.CrossReferences[1_000_004].Type);
        Assert.Equal(nextObjectNumber,
            new PdfIncrementalUpdateBuilder(reopened).ReserveObject().ObjectNumber);
    }

    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table)]
    [InlineData(PdfCrossReferenceFormat.Stream)]
    public void Write_SparseRangesPreserveKnownFreeObjectGenerations(
        PdfCrossReferenceFormat format)
    {
        PdfDocument source = PdfDocument.Open(SourcePdfWithSparseFreeEntry());

        var options = new PdfDocumentWriteOptions { CrossReferenceFormat = format };
        byte[] firstRewrite = PdfDocumentWriter.Write(source, options);
        PdfDocument reopened = PdfDocument.Open(firstRewrite);
        byte[] secondRewrite = PdfDocumentWriter.Write(reopened, options);

        PdfCrossReferenceEntry zero = reopened.CrossReferences[0];
        PdfCrossReferenceEntry inherited = reopened.CrossReferences[500_000];
        Assert.Equal(500_000, zero.Field1);
        Assert.Equal(PdfCrossReferenceEntryType.Free, inherited.Type);
        Assert.Equal(7, inherited.Field2);
        Assert.Equal(firstRewrite, secondRewrite);
    }

    [Fact]
    public void Write_RemovesObsoleteLinearizationDictionary()
    {
        PdfDocument document = PdfDocument.Open(SourcePdfWithLinearizationDictionary());

        byte[] rewritten = PdfDocumentWriter.Write(document);
        PdfDocument reopened = PdfDocument.Open(rewritten);

        Assert.False(reopened.CrossReferences.ContainsKey(3));
        Assert.DoesNotContain("/Linearized", Encoding.Latin1.GetString(rewritten),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("direct", "trailer /Root to be an indirect reference")]
    [InlineData("stale", "trailer /Root to resolve to a catalog dictionary")]
    [InlineData("wrong-type", "trailer /Root to resolve to a catalog dictionary")]
    public void Write_RejectsInvalidTrailerRoots(string kind, string expectedMessage)
    {
        string source = Encoding.ASCII.GetString(SourcePdf());
        source = kind switch
        {
            "direct" => source.Replace("/Root 1 0 R", "/Root null ",
                StringComparison.Ordinal),
            "stale" => source.Replace("/Root 1 0 R", "/Root 1 1 R",
                StringComparison.Ordinal),
            _ => source.Replace("/Type /Catalog", "/Type /Pages  ",
                StringComparison.Ordinal)
        };
        PdfDocument document = PdfDocument.Open(Encoding.ASCII.GetBytes(source));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(document));

        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RejectsStaleReferencesReachableFromCatalog()
    {
        string source = Encoding.ASCII.GetString(SourcePdf())
            .Replace("/Data 2 0 R", "/Data 2 1 R", StringComparison.Ordinal);
        PdfDocument document = PdfDocument.Open(Encoding.ASCII.GetBytes(source));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(document));

        Assert.Contains("Trailer /Root value contains a stale indirect reference",
            error.Message, StringComparison.Ordinal);

        PdfDocument authored = PdfDocument.Open(
            new PdfDocumentBuilder().AddBlankPage().Build());
        PdfIndirectReference rootReference = Assert.IsType<PdfIndirectReference>(
            authored.Trailer[Name("Root")]);
        PdfDictionary catalog = Assert.IsType<PdfDictionary>(
            authored.Resolve(rootReference));
        var update = new PdfIncrementalUpdateBuilder(authored);
        PdfIndirectReference xrefType = update.AddObject(Name("XRef"));
        PdfIndirectReference xrefTypeAlias = update.AddObject(xrefType);
        PdfIndirectReference obsolete = update.AddObject(new PdfStream(
            new PdfDictionary([new(Name("Type"), xrefTypeAlias)]), []));
        PdfDocument referencesObsolete = PdfDocument.Open(update
            .ReplaceObject(rootReference.ObjectNumber,
                new PdfDictionary(catalog.Append(
                    new KeyValuePair<PdfName, PdfObject>(Name("Private"), obsolete))))
            .Build());

        InvalidOperationException obsoleteError = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(referencesObsolete));
        Assert.Contains("Trailer /Root value contains a stale indirect reference",
            obsoleteError.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" /Info << /Title (direct) >>", "trailer /Info to be an indirect reference")]
    public void Write_RejectsInvalidPreservedInformationReferences(
        string extraTrailer, string expectedMessage)
    {
        PdfDocument document = PdfDocument.Open(SourcePdf(extraTrailer));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(document));

        Assert.Contains(expectedMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_OmitsInformationReferenceThatDoesNotResolveToDictionary()
    {
        PdfDocument document = PdfDocument.Open(SourcePdf(" /Info 2 1 R"));

        PdfDocument rewritten = PdfDocument.Open(PdfDocumentWriter.Write(document));

        Assert.False(rewritten.CrossReferences.TryGetTrailerValue(Name("Info"), out _));
    }

    [Fact]
    public void Write_PreservesInvalidStandardDocumentInformationFieldsForRecovery()
    {
        PdfDocument document = PdfDocument.Open(
            SourcePdfWithDocumentInformation(title: "17"));

        Assert.NotEmpty(PdfDocumentWriter.Write(document));
    }

    [Theory]
    [InlineData("D:20251301000000Z")]
    [InlineData("D:2026Z")]
    [InlineData("D:20260824120000+0700")]
    public void Write_PreservesInvalidDocumentInformationDatesForRecovery(string date)
    {
        PdfDocument document = PdfDocument.Open(SourcePdfWithDocumentInformation(
            extraInformation: $" /CreationDate ({date})"));

        Assert.NotEmpty(PdfDocumentWriter.Write(document));
    }

    [Theory]
    [InlineData("D:2026")]
    [InlineData("D:202608")]
    [InlineData("D:20260824")]
    [InlineData("D:2026082412")]
    [InlineData("D:202608241234")]
    [InlineData("D:20240229123456")]
    [InlineData("D:20260824123456Z")]
    [InlineData("D:20260824123456-07'00'")]
    [InlineData("D:20260824123456+05'30")]
    public void Write_AcceptsValidDocumentInformationDates(string date)
    {
        PdfDocument document = PdfDocument.Open(SourcePdfWithDocumentInformation(
            extraInformation: $" /CreationDate ({date})"));

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(document));

        Assert.NotNull(reopened.Trailer[Name("Info")]);
    }

    [Fact]
    public void Write_ResolvesIndirectDocumentInformationFields()
    {
        PdfDocument source = PdfDocument.Open(SourcePdf());
        var update = new PdfIncrementalUpdateBuilder(source);
        PdfIndirectReference title = update.AddObject(
            new PdfString("Indirect title"u8, PdfStringForm.Literal));
        PdfIndirectReference trapped = update.AddObject(Name("Unknown"));
        PdfIndirectReference information = update.AddObject(new PdfDictionary([
            new(Name("Title"), title),
            new(Name("Trapped"), trapped)
        ]));
        source = PdfDocument.Open(update.SetDocumentInformation(information).Build());

        PdfDocument reopened = PdfDocument.Open(PdfDocumentWriter.Write(source));

        PdfDictionary rewrittenInformation = Assert.IsType<PdfDictionary>(reopened.Resolve(
            Assert.IsType<PdfIndirectReference>(reopened.Trailer[Name("Info")])));
        PdfString rewrittenTitle = Assert.IsType<PdfString>(reopened.Resolve(
            Assert.IsType<PdfIndirectReference>(rewrittenInformation[Name("Title")])));
        Assert.Equal("Indirect title", Encoding.Latin1.GetString(rewrittenTitle.Bytes.Span));
    }

    [Fact]
    public void Write_RejectsStaleCustomDocumentInformationGraphs()
    {
        PdfDocument document = PdfDocument.Open(SourcePdfWithDocumentInformation(
            extraInformation: " /Private [2 1 R]"));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(document));

        Assert.Contains("Trailer /Info value contains a stale indirect reference",
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Open_RejectsMalformedDocumentIdentifiersBeforeRewrite()
    {
        PdfSyntaxException error = Assert.Throws<PdfSyntaxException>(() =>
            PdfDocument.Open(SourcePdf(" /ID [<01>]")));

        Assert.Contains("Trailer /ID must be an array of two strings",
            error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" /Private 2 1 R")]
    [InlineData(" /Private [2 1 R]")]
    public void Write_RejectsStaleApplicationTrailerReferences(string extraTrailer)
    {
        PdfDocument document = PdfDocument.Open(SourcePdf(extraTrailer));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(document));

        Assert.Contains("Trailer /Private value contains a stale indirect reference",
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RejectsApplicationTrailerReferencesToOmittedStructuralObjects()
    {
        PdfDocument document = PdfDocument.Open(
            SourcePdfWithApplicationReferenceToXrefStream());

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(document));

        Assert.Contains(
            "Trailer /Private value contains a reference to an object omitted from the full rewrite",
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RejectsInformationReferencesToOmittedStructuralObjects()
    {
        PdfDocument document = PdfDocument.Open(
            SourcePdfWithInformationReferenceToXrefStream());

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(document));

        Assert.Contains(
            "Trailer /Info value contains a reference to an object omitted from the full rewrite",
            error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_RejectsDanglingReferencesInUnreachableWritableObjects()
    {
        PdfDocument document = PdfDocument.Open(SourcePdfWithUnreachableDanglingReference());

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => PdfDocumentWriter.Write(document));

        Assert.Contains(
            "Object 3 0 contains a reference to an object omitted from the full rewrite",
            error.Message, StringComparison.Ordinal);
    }

    private static byte[] SourcePdf(
        string extraTrailer = "", string version = "2.0", string? catalogVersion = null,
        int declaredSize = 3)
    {
        var source = new StringBuilder($"%PDF-{version}\n");
        int catalogOffset = source.Length;
        source.Append($"1 0 obj << /Type /Catalog /Data 2 0 R{(catalogVersion is null ? "" : $" /Version /{catalogVersion}")} >> endobj\n");
        int streamOffset = source.Length;
        source.Append("2 0 obj << /Length 5 >> stream\nHello\nendstream endobj\n");
        int xrefOffset = source.Length;
        source.Append("xref\n0 3\n0000000000 65535 f\n");
        source.Append($"{catalogOffset:0000000000} 00000 n\n");
        source.Append($"{streamOffset:0000000000} 00000 n\n");
        source.Append($"trailer << /Size {declaredSize} /Root 1 0 R{extraTrailer} >>\n");
        source.Append($"startxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private static byte[] SourcePdfWithSparseFreeEntry()
    {
        var source = new StringBuilder("%PDF-2.0\n");
        int catalogOffset = source.Length;
        source.Append("1 0 obj << /Type /Catalog /Data 2 0 R >> endobj\n");
        int streamOffset = source.Length;
        source.Append("2 0 obj << /Length 5 >> stream\nHello\nendstream endobj\n");
        int xrefOffset = source.Length;
        source.Append("xref\n0 3\n0000000000 65535 f\n");
        source.Append($"{catalogOffset:0000000000} 00000 n\n");
        source.Append($"{streamOffset:0000000000} 00000 n\n");
        source.Append("500000 1\n0000000000 00007 f\n");
        source.Append("trailer << /Size 1000005 /Root 1 0 R >>\n");
        source.Append($"startxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private static byte[] SourcePdfWithApplicationReferenceToXrefStream()
    {
        var source = new StringBuilder("%PDF-2.0\n");
        int catalogOffset = source.Length;
        source.Append("1 0 obj << /Type /Catalog >> endobj\n");
        int structuralOffset = source.Length;
        source.Append("2 0 obj << /Type /XRef /Length 0 >> stream\n\nendstream endobj\n");
        int xrefOffset = source.Length;
        source.Append("xref\n0 3\n0000000000 65535 f\n");
        source.Append($"{catalogOffset:0000000000} 00000 n\n");
        source.Append($"{structuralOffset:0000000000} 00000 n\n");
        source.Append("trailer << /Size 3 /Root 1 0 R /Private 2 0 R >>\n");
        source.Append($"startxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private static byte[] SourcePdfWithInformationReferenceToXrefStream()
    {
        var source = new StringBuilder("%PDF-2.0\n");
        int catalogOffset = source.Length;
        source.Append("1 0 obj << /Type /Catalog >> endobj\n");
        int structuralOffset = source.Length;
        source.Append("2 0 obj << /Type /XRef /Length 0 >> stream\n\nendstream endobj\n");
        int informationOffset = source.Length;
        source.Append("3 0 obj << /Private 2 0 R >> endobj\n");
        int xrefOffset = source.Length;
        source.Append("xref\n0 4\n0000000000 65535 f\n");
        source.Append($"{catalogOffset:0000000000} 00000 n\n");
        source.Append($"{structuralOffset:0000000000} 00000 n\n");
        source.Append($"{informationOffset:0000000000} 00000 n\n");
        source.Append("trailer << /Size 4 /Root 1 0 R /Info 3 0 R >>\n");
        source.Append($"startxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private static byte[] SourcePdfWithUnreachableDanglingReference()
    {
        var source = new StringBuilder("%PDF-2.0\n");
        int catalogOffset = source.Length;
        source.Append("1 0 obj << /Type /Catalog >> endobj\n");
        int dataOffset = source.Length;
        source.Append("2 0 obj << /Length 5 >> stream\nHello\nendstream endobj\n");
        int unreachableOffset = source.Length;
        source.Append("3 0 obj << /Missing 9 0 R >> endobj\n");
        int xrefOffset = source.Length;
        source.Append("xref\n0 4\n0000000000 65535 f\n");
        source.Append($"{catalogOffset:0000000000} 00000 n\n");
        source.Append($"{dataOffset:0000000000} 00000 n\n");
        source.Append($"{unreachableOffset:0000000000} 00000 n\n");
        source.Append("trailer << /Size 4 /Root 1 0 R >>\n");
        source.Append($"startxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private static byte[] SourcePdfWithLinearizationDictionary()
    {
        var source = new StringBuilder("%PDF-2.0\n");
        int catalogOffset = source.Length;
        source.Append("1 0 obj << /Type /Catalog /Data 2 0 R >> endobj\n");
        int streamOffset = source.Length;
        source.Append("2 0 obj << /Length 5 >> stream\nHello\nendstream endobj\n");
        int linearizationOffset = source.Length;
        source.Append("3 0 obj << /Linearized 1 /L 999 /H [0 0] /O 1 /E 0 /N 1 /T 0 >> endobj\n");
        int xrefOffset = source.Length;
        source.Append("xref\n0 4\n0000000000 65535 f\n");
        source.Append($"{catalogOffset:0000000000} 00000 n\n");
        source.Append($"{streamOffset:0000000000} 00000 n\n");
        source.Append($"{linearizationOffset:0000000000} 00000 n\n");
        source.Append($"trailer << /Size 4 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private static byte[] SourcePdfWithDocumentInformation(
        bool shared = false, string title = "(private metadata)",
        string extraInformation = "")
    {
        var source = new StringBuilder("%PDF-2.0\n");
        int catalogOffset = source.Length;
        source.Append($"1 0 obj << /Type /Catalog{(shared ? " /SharedInfo 3 0 R" : "")} >> endobj\n");
        int pageDataOffset = source.Length;
        source.Append("2 0 obj << /Value 2 >> endobj\n");
        int infoOffset = source.Length;
        source.Append($"3 0 obj << /Title {title}{extraInformation} >> endobj\n");
        int xrefOffset = source.Length;
        source.Append("xref\n0 4\n0000000000 65535 f\n");
        source.Append($"{catalogOffset:0000000000} 00000 n\n");
        source.Append($"{pageDataOffset:0000000000} 00000 n\n");
        source.Append($"{infoOffset:0000000000} 00000 n\n");
        source.Append($"trailer << /Size 4 /Root 1 0 R /Info 3 0 R >>\n" +
            $"startxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private static byte[] SourcePdfWithObjectNumberGap()
    {
        var source = new StringBuilder("%PDF-2.0\n");
        int catalogOffset = source.Length;
        source.Append("1 0 obj << /Type /Catalog /Data 2 0 R /Extra 10 0 R >> endobj\n");
        int streamOffset = source.Length;
        source.Append("2 0 obj << /Length 5 >> stream\nHello\nendstream endobj\n");
        int extraOffset = source.Length;
        source.Append("10 0 obj << /Value 10 >> endobj\n");
        int xrefOffset = source.Length;
        source.Append("xref\n0 3\n0000000000 65535 f\n");
        source.Append($"{catalogOffset:0000000000} 00000 n\n");
        source.Append($"{streamOffset:0000000000} 00000 n\n");
        source.Append("5 1\n0000000000 00007 f\n");
        source.Append("10 1\n");
        source.Append($"{extraOffset:0000000000} 00000 n\n");
        source.Append("11 1\n0000000000 65535 f\n");
        source.Append($"trailer << /Size 12 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private static byte[] AppendRootAliases(
        byte[] source, PdfIndirectReference catalogReference)
    {
        PdfDocument document = PdfDocument.Open(source);
        Assert.True(document.CrossReferences.TryGetTrailerValue(
            Name("Size"), out PdfObject sizeValue));
        int firstObjectNumber = checked((int)Assert.IsType<PdfInteger>(sizeValue).Value);
        int secondObjectNumber = checked(firstObjectNumber + 1);
        long previousXref = PdfStartXref.Find(source).Offset;
        using var output = new MemoryStream();
        output.Write(source);
        int firstOffset = checked((int)output.Position);
        Write($"{firstObjectNumber} 0 obj {secondObjectNumber} 0 R endobj\n");
        int secondOffset = checked((int)output.Position);
        Write($"{secondObjectNumber} 0 obj {catalogReference.ObjectNumber} "
            + $"{catalogReference.Generation} R endobj\n");
        int xrefOffset = checked((int)output.Position);
        Write($"xref\n{firstObjectNumber} 2\n");
        Write($"{firstOffset:0000000000} 00000 n \n");
        Write($"{secondOffset:0000000000} 00000 n \n");
        Write($"trailer << /Size {secondObjectNumber + 1} /Prev {previousXref} "
            + $"/Root {firstObjectNumber} 0 R >>\n");
        Write($"startxref\n{xrefOffset}\n%%EOF\n");
        return output.ToArray();

        void Write(string value) => output.Write(Encoding.ASCII.GetBytes(value));
    }

    private static PdfObject ResolveChain(PdfDocument document, PdfObject value)
    {
        while (value is PdfIndirectReference reference)
            value = document.Resolve(reference);
        return value;
    }

    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}
