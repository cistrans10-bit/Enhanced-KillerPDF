using System.Text;

namespace KillerPdf.Engine.Syntax;

/// <summary>A token and its exact location in the source PDF.</summary>
public readonly record struct PdfToken(
    PdfTokenKind Kind,
    int Offset,
    int Length,
    ReadOnlyMemory<byte> Value)
{
    /// <summary>Decodes the token value bytes using the PDF-compatible Latin-1 mapping.</summary>
    public string ValueAsLatin1() => Encoding.Latin1.GetString(Value.Span);
}
