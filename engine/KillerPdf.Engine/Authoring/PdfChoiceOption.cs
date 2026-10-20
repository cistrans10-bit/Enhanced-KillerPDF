namespace KillerPdf.Engine.Authoring;

/// <summary>An AcroForm choice option with separate exported and displayed values.</summary>
public sealed record PdfChoiceOption(string ExportValue, string DisplayValue);
