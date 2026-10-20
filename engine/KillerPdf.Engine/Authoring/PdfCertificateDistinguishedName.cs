namespace KillerPdf.Engine.Authoring;

/// <summary>Required X.509 subject distinguished-name attributes.</summary>
public sealed record PdfCertificateDistinguishedName(
    IReadOnlyDictionary<string, string> Attributes);
