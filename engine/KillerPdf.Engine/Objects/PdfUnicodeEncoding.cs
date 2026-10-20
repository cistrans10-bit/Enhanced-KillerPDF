using System.Text;

namespace KillerPdf.Engine.Objects;

internal static class PdfUnicodeEncoding
{
    private static readonly UnicodeEncoding BigEndian = new(
        bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8 = new(
        encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static byte[] EncodeBigEndian(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        try
        {
            return BigEndian.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "PDF Unicode text cannot contain an unpaired UTF-16 surrogate.",
                nameof(value), exception);
        }
    }

    internal static byte[] EncodeUtf8(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        try
        {
            return Utf8.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "PDF UTF-8 text cannot contain an unpaired UTF-16 surrogate.",
                nameof(value), exception);
        }
    }

    internal static string DecodeBigEndian(ReadOnlySpan<byte> value, string description)
    {
        try
        {
            return BigEndian.GetString(value);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                $"{description} contains malformed UTF-16BE text.", exception);
        }
    }

    internal static string DecodeUtf8(ReadOnlySpan<byte> value, string description)
    {
        try
        {
            return Utf8.GetString(value);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                $"{description} contains malformed UTF-8 text.", exception);
        }
    }

    internal static string DecodeTextString(
        ReadOnlySpan<byte> value, string description) =>
        value.Length >= 2 && value[0] == 0xFE && value[1] == 0xFF
            ? DecodeBigEndian(value[2..], description)
            : value.Length >= 3 && value[0] == 0xEF && value[1] == 0xBB
                && value[2] == 0xBF
                ? DecodeUtf8(value[3..], description)
                : DecodePdfDocEncoding(value, description);

    private static string DecodePdfDocEncoding(
        ReadOnlySpan<byte> value, string description)
    {
        var characters = new char[value.Length];
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] is 0x7F or 0x9F or 0xAD)
                throw new InvalidOperationException(
                    $"{description} contains undefined PDFDocEncoding byte 0x{value[index]:X2}.");
            characters[index] = value[index] switch
            {
                0x18 => '\u02D8', 0x19 => '\u02C7', 0x1A => '\u02C6',
                0x1B => '\u02D9', 0x1C => '\u02DD', 0x1D => '\u02DB',
                0x1E => '\u02DA', 0x1F => '\u02DC',
                0x80 => '\u2022', 0x81 => '\u2020', 0x82 => '\u2021',
                0x83 => '\u2026', 0x84 => '\u2014', 0x85 => '\u2013',
                0x86 => '\u0192', 0x87 => '\u2044', 0x88 => '\u2039',
                0x89 => '\u203A', 0x8A => '\u2212', 0x8B => '\u2030',
                0x8C => '\u201E', 0x8D => '\u201C', 0x8E => '\u201D',
                0x8F => '\u2018', 0x90 => '\u2019', 0x91 => '\u201A',
                0x92 => '\u2122', 0x93 => '\uFB01', 0x94 => '\uFB02',
                0x95 => '\u0141', 0x96 => '\u0152', 0x97 => '\u0160',
                0x98 => '\u0178', 0x99 => '\u017D', 0x9A => '\u0131',
                0x9B => '\u0142', 0x9C => '\u0153', 0x9D => '\u0161',
                0x9E => '\u017E', 0xA0 => '\u20AC',
                byte item => (char)item
            };
        }
        return new string(characters);
    }
}
