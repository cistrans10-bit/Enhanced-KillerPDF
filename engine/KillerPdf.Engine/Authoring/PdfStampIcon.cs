namespace KillerPdf.Engine.Authoring;

/// <summary>A standard semantic identity for a stamp annotation's custom appearance.</summary>
public enum PdfStampIcon
{
    /// <summary>A custom image stamp without a standard semantic label.</summary>
    Image,
    /// <summary>An approved stamp.</summary>
    Approved,
    /// <summary>An experimental stamp.</summary>
    Experimental,
    /// <summary>A not-approved stamp.</summary>
    NotApproved,
    /// <summary>An as-is stamp.</summary>
    AsIs,
    /// <summary>An expired stamp.</summary>
    Expired,
    /// <summary>A not-for-public-release stamp.</summary>
    NotForPublicRelease,
    /// <summary>A confidential stamp.</summary>
    Confidential,
    /// <summary>A final stamp.</summary>
    Final,
    /// <summary>A sold stamp.</summary>
    Sold,
    /// <summary>A departmental stamp.</summary>
    Departmental,
    /// <summary>A for-comment stamp.</summary>
    ForComment,
    /// <summary>A top-secret stamp.</summary>
    TopSecret,
    /// <summary>A draft stamp.</summary>
    Draft,
    /// <summary>A for-public-release stamp.</summary>
    ForPublicRelease
}

internal static class PdfStampIconNames
{
    internal static string Name(PdfStampIcon value) => value switch
    {
        PdfStampIcon.Image => "Image",
        PdfStampIcon.Approved => "Approved",
        PdfStampIcon.Experimental => "Experimental",
        PdfStampIcon.NotApproved => "NotApproved",
        PdfStampIcon.AsIs => "AsIs",
        PdfStampIcon.Expired => "Expired",
        PdfStampIcon.NotForPublicRelease => "NotForPublicRelease",
        PdfStampIcon.Confidential => "Confidential",
        PdfStampIcon.Final => "Final",
        PdfStampIcon.Sold => "Sold",
        PdfStampIcon.Departmental => "Departmental",
        PdfStampIcon.ForComment => "ForComment",
        PdfStampIcon.TopSecret => "TopSecret",
        PdfStampIcon.Draft => "Draft",
        PdfStampIcon.ForPublicRelease => "ForPublicRelease",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
