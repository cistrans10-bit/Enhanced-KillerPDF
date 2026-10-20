using System.Text;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Authoring;

internal static class PdfOutputIntentFactory
{
    internal static void Validate(
        PdfIccProfile profile, string outputConditionIdentifier)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(outputConditionIdentifier))
            throw new ArgumentException(
                "An output-condition identifier cannot be empty.",
                nameof(outputConditionIdentifier));
    }

    internal static PdfStream Profile(PdfIccProfile profile) =>
        new(new PdfDictionary([
            new(Name("N"), new PdfInteger(profile.ComponentCount)),
            new(Name("Alternate"), Name(profile.ComponentCount switch
            {
                1 => "DeviceGray",
                3 => "DeviceRGB",
                4 => "DeviceCMYK",
                _ => throw new NotSupportedException()
            }))
        ]), profile.Data.Span);

    internal static PdfDictionary OutputIntent(
        PdfIndirectReference profile,
        string identifier, string? condition,
        string? registryName, string? information)
    {
        var entries = new List<KeyValuePair<PdfName, PdfObject>>
        {
            new(Name("Type"), Name("OutputIntent")),
            new(Name("S"), Name("GTS_PDFA1")),
            new(Name("OutputConditionIdentifier"), TextString(identifier)),
            new(Name("DestOutputProfile"), profile)
        };
        Add("OutputCondition", condition);
        Add("RegistryName", registryName);
        Add("Info", information);
        return new PdfDictionary(entries);

        void Add(string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                entries.Add(new KeyValuePair<PdfName, PdfObject>(
                    Name(name), TextString(value)));
        }
    }

    private static PdfString TextString(string value) =>
        new([0xFE, 0xFF, .. PdfUnicodeEncoding.EncodeBigEndian(value)],
            PdfStringForm.Hexadecimal);
    private static PdfName Name(string value) =>
        new(Encoding.ASCII.GetBytes(value));
}
