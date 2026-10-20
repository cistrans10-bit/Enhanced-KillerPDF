namespace KillerPdf.Engine.Syntax;

/// <summary>A malformed lexical construct at a known byte offset.</summary>
/// <remarks>Creates a syntax error associated with an exact byte offset.</remarks>
public sealed class PdfSyntaxException(string message, int offset) : FormatException($"{message} (byte offset {offset}).")
{

    /// <summary>Gets the zero-based byte offset at which parsing failed.</summary>
    public int Offset { get; } = offset;
}
