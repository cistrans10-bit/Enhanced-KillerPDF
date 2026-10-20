namespace KillerPdf.Engine.Authoring;

/// <summary>An RFC 3161 timestamp-server constraint for a signature field.</summary>
public sealed record PdfSignatureTimestamp(string ServerUrl, bool Required = false);
