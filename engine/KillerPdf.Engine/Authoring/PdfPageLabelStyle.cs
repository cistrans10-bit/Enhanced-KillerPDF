namespace KillerPdf.Engine.Authoring;

/// <summary>The numbering style used by a PDF page-label range.</summary>
public enum PdfPageLabelStyle
{
    /// <summary>No numeric or alphabetic portion.</summary>
    None,
    /// <summary>Decimal Arabic numerals.</summary>
    Decimal,
    /// <summary>Uppercase Roman numerals.</summary>
    UpperRoman,
    /// <summary>Lowercase Roman numerals.</summary>
    LowerRoman,
    /// <summary>Uppercase alphabetic labels.</summary>
    UpperLetters,
    /// <summary>Lowercase alphabetic labels.</summary>
    LowerLetters
}
