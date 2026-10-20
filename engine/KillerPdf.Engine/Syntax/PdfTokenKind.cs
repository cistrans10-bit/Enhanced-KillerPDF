namespace KillerPdf.Engine.Syntax;

/// <summary>The lexical units defined by the PDF object syntax.</summary>
public enum PdfTokenKind
{
    /// <summary>No more tokens are available.</summary>
    EndOfInput,
    /// <summary>An integer number token.</summary>
    Integer,
    /// <summary>A real number token.</summary>
    Real,
    /// <summary>A true or false token.</summary>
    Boolean,
    /// <summary>The null object token.</summary>
    Null,
    /// <summary>A slash-prefixed name object.</summary>
    Name,
    /// <summary>A parenthesized literal string.</summary>
    LiteralString,
    /// <summary>An angle-bracket hexadecimal string.</summary>
    HexString,
    /// <summary>An operator or other bare keyword.</summary>
    Keyword,
    /// <summary>The opening bracket of an array.</summary>
    ArrayStart,
    /// <summary>The closing bracket of an array.</summary>
    ArrayEnd,
    /// <summary>The opening delimiter of a dictionary.</summary>
    DictionaryStart,
    /// <summary>The closing delimiter of a dictionary.</summary>
    DictionaryEnd,
    /// <summary>An opening brace token used by calculator functions.</summary>
    BraceStart,
    /// <summary>A closing brace token used by calculator functions.</summary>
    BraceEnd
}
