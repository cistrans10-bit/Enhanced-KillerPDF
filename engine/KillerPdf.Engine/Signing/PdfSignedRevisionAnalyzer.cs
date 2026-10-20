using KillerPdf.Engine.Documents;
using KillerPdf.Engine.CrossReference;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Filters;

namespace KillerPdf.Engine.Signing;

/// <summary>Locates valid incremental PDF revisions added after a signed byte range.</summary>
public static class PdfSignedRevisionAnalyzer
{
    /// <summary>Analyzes revisions and certification permissions after a selected signature.</summary>
    public static PdfSignedRevisionAnalysis Analyze(
        PdfDocument document, PdfSignatureInfo signature)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(signature);
        if (!signature.HasValidByteRange || signature.ByteRange is not { Count: 4 } range)
            throw new InvalidOperationException("The signature does not have a valid byte range.");
        long signedLength = checked(range[2] + range[3]);
        bool validRevision = false;
        PdfDocument? signedDocument = null;
        if (signedLength <= int.MaxValue)
        {
            try
            {
                signedDocument = PdfDocument.Open(
                    document.Source[..checked((int)signedLength)]);
                validRevision = true;
            }
            catch (Exception exception) when (exception is FormatException
                or ArgumentException
                or InvalidOperationException
                or NotSupportedException
                or PdfFilterException
                or OverflowException)
            {
                validRevision = false;
            }
        }
        var laterSections = document.CrossReferences.Sections
            .Where(section => section.Offset >= signedLength)
            .ToArray();
        int[] changedObjects = [.. document.CrossReferences.AllSections
            .Where(section => section.Offset >= signedLength)
            .SelectMany(section => section.Keys)
            .Where(number => number > 0).Distinct().Order()];
        int[] freedObjects = [.. changedObjects.Where(number =>
            document.CrossReferences.TryGetValue(number, out PdfCrossReferenceEntry entry)
            && entry.Type == PdfCrossReferenceEntryType.Free)];
        int[] updatedObjects = signedDocument is null ? [] : [.. changedObjects.Where(number =>
            !freedObjects.Contains(number)
            && signedDocument.CrossReferences.TryGetValue(number, out PdfCrossReferenceEntry entry)
            && entry.Type is PdfCrossReferenceEntryType.InUse
                or PdfCrossReferenceEntryType.Compressed)];
        int[] addedObjects = [.. changedObjects.Except(freedObjects).Except(updatedObjects)];
        bool hasLaterChanges = document.Source.Length > signedLength;
        PdfSignedRevisionPermissionAssessment permissionAssessment =
            signature.CertificationPermission switch
            {
                null => PdfSignedRevisionPermissionAssessment.NotCertified,
                _ when !hasLaterChanges => PdfSignedRevisionPermissionAssessment.NoLaterChanges,
                PdfSignatureCertificationPermission.NoChanges =>
                    PdfSignedRevisionPermissionAssessment.Prohibited,
                _ => PdfSignedRevisionPermissionAssessment.RequiresSemanticReview
            };
        return new PdfSignedRevisionAnalysis
        {
            SignedRevisionLength = signedLength,
            CurrentDocumentLength = document.Source.Length,
            SignedRevisionIsValidPdf = validRevision,
            LaterRevisionCount = laterSections.Length,
            ChangedObjectNumbers = changedObjects,
            AddedObjectNumbers = addedObjects,
            UpdatedObjectNumbers = updatedObjects,
            FreedObjectNumbers = freedObjects,
            PermissionAssessment = permissionAssessment
        };
    }
}
