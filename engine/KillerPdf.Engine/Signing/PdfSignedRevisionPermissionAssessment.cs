namespace KillerPdf.Engine.Signing;

/// <summary>Conservative DocMDP assessment for changes after a signed revision.</summary>
public enum PdfSignedRevisionPermissionAssessment
{
    /// <summary>The signature does not certify the document.</summary>
    NotCertified,
    /// <summary>No changes occur after the signed revision.</summary>
    NoLaterChanges,
    /// <summary>Later changes are prohibited by the certification policy.</summary>
    Prohibited,
    /// <summary>Later changes require semantic review to determine whether they are permitted.</summary>
    RequiresSemanticReview
}
