namespace KillerPdf.Engine.Authoring;

/// <summary>The certification behavior requested by a signature seed value.</summary>
public enum PdfSignatureCertificationPermission
{
    /// <summary>Creates an approval signature instead of a certification signature.</summary>
    ApprovalSignature = 0,
    /// <summary>Certifies that no later changes are permitted.</summary>
    NoChanges = 1,
    /// <summary>Permits form filling and approval signatures.</summary>
    FormFillingAndSignatures = 2,
    /// <summary>Also permits annotation creation, deletion, and modification.</summary>
    FormFillingSignaturesAndAnnotations = 3
}
