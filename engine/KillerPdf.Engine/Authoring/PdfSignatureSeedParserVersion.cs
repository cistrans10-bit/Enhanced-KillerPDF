namespace KillerPdf.Engine.Authoring;

/// <summary>The minimum signature seed-value feature set a signing handler must recognize.</summary>
public enum PdfSignatureSeedParserVersion
{
    /// <summary>The PDF 1.5 seed-value feature set.</summary>
    Pdf15 = 1,
    /// <summary>The PDF 1.7 seed-value feature set.</summary>
    Pdf17 = 2,
    /// <summary>The PDF 2.0 seed-value feature set.</summary>
    Pdf20 = 3
}
