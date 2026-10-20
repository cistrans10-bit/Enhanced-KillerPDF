namespace KillerPdf.Engine.Objects;

/// <summary>The lexical representation used for a PDF string.</summary>
public enum PdfStringForm
{
    /// <summary>A parenthesized literal string.</summary>
    Literal,
    /// <summary>An angle-bracketed hexadecimal string.</summary>
    Hexadecimal
}

/// <summary>A PDF string's decoded bytes and the lexical form used to represent it.</summary>
public sealed class PdfString : PdfObject
{
    private readonly byte[] _bytes;

    /// <summary>Creates a string from decoded bytes and a defined lexical form.</summary>
    public PdfString(ReadOnlySpan<byte> bytes, PdfStringForm form)
    {
        if (!Enum.IsDefined(form))
            throw new ArgumentOutOfRangeException(nameof(form),
                "The PDF string form is not defined.");
        _bytes = bytes.ToArray();
        Form = form;
    }

    /// <summary>Gets the decoded string bytes.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes;
    /// <summary>Gets the lexical form used to represent the string.</summary>
    public PdfStringForm Form { get; }
}
