namespace KillerPdf.Engine.Authoring;

internal static class PdfLanguageTag
{
    private static readonly HashSet<string> Grandfathered = new(StringComparer.OrdinalIgnoreCase)
    {
        "art-lojban", "cel-gaulish", "en-GB-oed", "i-ami", "i-bnn", "i-default",
        "i-enochian", "i-hak", "i-klingon", "i-lux", "i-mingo", "i-navajo",
        "i-pwn", "i-tao", "i-tay", "i-tsu", "no-bok", "no-nyn", "sgn-BE-FR",
        "sgn-BE-NL", "sgn-CH-DE", "zh-guoyu", "zh-hakka", "zh-min",
        "zh-min-nan", "zh-xiang"
    };

    internal static bool IsValid(string value)
    {
        if (string.IsNullOrEmpty(value) || Grandfathered.Contains(value))
            return Grandfathered.Contains(value);
        string[] parts = value.Split('-');
        if (parts.Any(part => part.Length == 0 || part.Length > 8 || !IsAlphaNumeric(part)))
            return false;
        if (parts[0].Equals("x", StringComparison.OrdinalIgnoreCase))
            return parts.Length > 1 && parts.Skip(1).All(part => part.Length is >= 1 and <= 8);

        int index = 0;
        string language = parts[index++];
        if (!IsAlpha(language) || language.Length is < 2 or > 8)
            return false;
        if (language.Length is 2 or 3)
        {
            int extlangs = 0;
            while (index < parts.Length && extlangs < 3
                && parts[index].Length == 3 && IsAlpha(parts[index]))
            {
                index++;
                extlangs++;
            }
        }
        if (index < parts.Length && parts[index].Length == 4 && IsAlpha(parts[index]))
            index++;
        if (index < parts.Length
            && (parts[index].Length == 2 && IsAlpha(parts[index])
                || parts[index].Length == 3 && IsNumeric(parts[index])))
            index++;

        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (index < parts.Length && IsVariant(parts[index]))
            if (!variants.Add(parts[index++])) return false;

        var extensions = new HashSet<char>();
        while (index < parts.Length && IsExtensionSingleton(parts[index]))
        {
            char singleton = char.ToLowerInvariant(parts[index++][0]);
            if (!extensions.Add(singleton)) return false;
            int start = index;
            while (index < parts.Length && parts[index].Length is >= 2 and <= 8)
                index++;
            if (index == start) return false;
        }

        if (index < parts.Length && parts[index].Equals("x", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            int start = index;
            while (index < parts.Length && parts[index].Length is >= 1 and <= 8)
                index++;
            if (index == start) return false;
        }
        return index == parts.Length;
    }

    private static bool IsVariant(string value) =>
        value.Length is >= 5 and <= 8
            || value.Length == 4 && value[0] is >= '0' and <= '9';

    private static bool IsExtensionSingleton(string value) =>
        value.Length == 1 && IsAlphaNumeric(value)
        && !value.Equals("x", StringComparison.OrdinalIgnoreCase);

    private static bool IsAlphaNumeric(string value) =>
        value.All(character => character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z' or >= '0' and <= '9');

    private static bool IsAlpha(string value) =>
        value.All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    private static bool IsNumeric(string value) =>
        value.All(character => character is >= '0' and <= '9');
}
