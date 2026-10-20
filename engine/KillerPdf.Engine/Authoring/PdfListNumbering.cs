namespace KillerPdf.Engine.Authoring;

/// <summary>Standard numbering styles for tagged list structure elements.</summary>
public enum PdfListNumbering
{
    /// <summary>No numbering or marker.</summary>
    None,
    /// <summary>A filled circular bullet.</summary>
    Disc,
    /// <summary>An open circular bullet.</summary>
    Circle,
    /// <summary>A square bullet.</summary>
    Square,
    /// <summary>Decimal numbers.</summary>
    Decimal,
    /// <summary>Uppercase Roman numerals.</summary>
    UpperRoman,
    /// <summary>Lowercase Roman numerals.</summary>
    LowerRoman,
    /// <summary>Uppercase alphabetic numbering.</summary>
    UpperAlpha,
    /// <summary>Lowercase alphabetic numbering.</summary>
    LowerAlpha
}

internal static class PdfListNumberingNames
{
    internal static string Name(PdfListNumbering value) => value switch
    {
        PdfListNumbering.None => "None",
        PdfListNumbering.Disc => "Disc",
        PdfListNumbering.Circle => "Circle",
        PdfListNumbering.Square => "Square",
        PdfListNumbering.Decimal => "Decimal",
        PdfListNumbering.UpperRoman => "UpperRoman",
        PdfListNumbering.LowerRoman => "LowerRoman",
        PdfListNumbering.UpperAlpha => "UpperAlpha",
        PdfListNumbering.LowerAlpha => "LowerAlpha",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
