using System.Globalization;
using KillerPdf.Engine.Objects;

namespace KillerPdf.Engine.Writing;

/// <summary>Writes the engine object model as deterministic, ASCII-safe PDF syntax.</summary>
public static class PdfObjectWriter
{
    /// <summary>Maximum nested array and dictionary depth accepted during serialization.</summary>
    public const int MaximumNestingDepth = 256;

    private static readonly PdfName LengthName = new("Length"u8);
    private static ReadOnlySpan<byte> HexDigits => "0123456789ABCDEF"u8;

    /// <summary>Serializes one direct PDF object to canonical bytes.</summary>
    public static byte[] Write(PdfObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var output = new MemoryStream();
        Write(output, value);
        return output.ToArray();
    }

    /// <summary>Serializes one direct PDF object to a writable stream.</summary>
    public static void Write(Stream destination, PdfObject value)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(value);
        if (!destination.CanWrite)
            throw new ArgumentException("The destination stream is not writable.", nameof(destination));
        WriteObject(destination, value, 0);
    }

    /// <summary>Serializes one complete indirect-object declaration to canonical bytes.</summary>
    public static byte[] Write(PdfIndirectObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var output = new MemoryStream();
        Write(output, value);
        return output.ToArray();
    }

    /// <summary>Serializes one complete indirect-object declaration to a writable stream.</summary>
    public static void Write(Stream destination, PdfIndirectObject value)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(value);
        if (!destination.CanWrite)
            throw new ArgumentException("The destination stream is not writable.", nameof(destination));
        if (value.ObjectNumber == 0)
            throw new InvalidOperationException(
                "PDF object number zero is reserved and cannot be written as an indirect object.");
        if (value.Generation == 65_535)
            throw new InvalidOperationException(
                "PDF generation 65,535 is retired and cannot be written as an indirect object.");

        WriteAscii(destination, value.ObjectNumber.ToString(CultureInfo.InvariantCulture));
        destination.WriteByte((byte)' ');
        WriteAscii(destination, value.Generation.ToString(CultureInfo.InvariantCulture));
        destination.Write(" obj\n"u8);
        if (value.Value is PdfStream stream)
            WriteStream(destination, stream, 1);
        else
            WriteObject(destination, value.Value, 0);
        destination.Write("\nendobj\n"u8);
    }

    private static void WriteObject(Stream output, PdfObject value, int depth)
    {
        if (depth >= MaximumNestingDepth)
            throw new InvalidOperationException("The PDF object nesting limit was exceeded while writing.");

        switch (value)
        {
            case PdfNull:
                output.Write("null"u8);
                break;
            case PdfBoolean boolean:
                output.Write(boolean.Value ? "true"u8 : "false"u8);
                break;
            case PdfInteger integer:
                WriteAscii(output, integer.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case PdfReal real:
                WriteAscii(output, FormatReal(real.Value));
                break;
            case PdfName name:
                WriteName(output, name);
                break;
            case PdfString text:
                WriteString(output, text);
                break;
            case PdfIndirectReference reference:
                WriteReference(output, reference);
                break;
            case PdfArray array:
                WriteArray(output, array, depth + 1);
                break;
            case PdfDictionary dictionary:
                WriteDictionary(output, dictionary, depth + 1, streamLength: null);
                break;
            case PdfStream:
                throw new InvalidOperationException(
                    "PDF streams must be written as indirect objects.");
            default:
                throw new NotSupportedException($"PDF object type {value.GetType().FullName} cannot be written.");
        }
    }

    private static void WriteArray(Stream output, PdfArray array, int depth)
    {
        output.WriteByte((byte)'[');
        for (int index = 0; index < array.Count; index++)
        {
            if (index > 0)
                output.WriteByte((byte)' ');
            WriteObject(output, array[index], depth);
        }
        output.WriteByte((byte)']');
    }

    private static void WriteDictionary(
        Stream output,
        PdfDictionary dictionary,
        int depth,
        int? streamLength)
    {
        IEnumerable<KeyValuePair<PdfName, PdfObject>> entries = dictionary;
        if (streamLength.HasValue)
        {
            entries = dictionary
                .Where(entry => !entry.Key.Equals(LengthName))
                .Append(new KeyValuePair<PdfName, PdfObject>(LengthName, new PdfInteger(streamLength.Value)));
        }

        output.Write("<<"u8);
        foreach (KeyValuePair<PdfName, PdfObject> entry in entries.OrderBy(
                     entry => entry.Key,
                     PdfNameByteComparer.Instance))
        {
            output.WriteByte((byte)' ');
            WriteName(output, entry.Key);
            output.WriteByte((byte)' ');
            WriteObject(output, entry.Value, depth);
        }
        output.Write(" >>"u8);
    }

    private static void WriteStream(Stream output, PdfStream stream, int depth)
    {
        WriteDictionary(output, stream.Dictionary, depth, stream.EncodedData.Length);
        output.Write("\nstream\n"u8);
        output.Write(stream.EncodedData.Span);
        output.Write("\nendstream"u8);
    }

    private static void WriteReference(Stream output, PdfIndirectReference reference)
    {
        if (reference.ObjectNumber == 0)
            throw new InvalidOperationException(
                "PDF object number zero is reserved and cannot be written as an indirect reference.");
        WriteAscii(output, reference.ObjectNumber.ToString(CultureInfo.InvariantCulture));
        output.WriteByte((byte)' ');
        WriteAscii(output, reference.Generation.ToString(CultureInfo.InvariantCulture));
        output.Write(" R"u8);
    }

    private static void WriteName(Stream output, PdfName name)
    {
        output.WriteByte((byte)'/');
        foreach (byte value in name.Bytes.Span)
        {
            if (IsRegularNameByte(value))
            {
                output.WriteByte(value);
                continue;
            }

            output.WriteByte((byte)'#');
            output.WriteByte(HexDigits[value >> 4]);
            output.WriteByte(HexDigits[value & 0x0F]);
        }
    }

    private static void WriteString(Stream output, PdfString value)
    {
        if (value.Form == PdfStringForm.Hexadecimal)
        {
            output.WriteByte((byte)'<');
            foreach (byte item in value.Bytes.Span)
            {
                output.WriteByte(HexDigits[item >> 4]);
                output.WriteByte(HexDigits[item & 0x0F]);
            }
            output.WriteByte((byte)'>');
            return;
        }

        output.WriteByte((byte)'(');
        foreach (byte item in value.Bytes.Span)
        {
            switch (item)
            {
                case (byte)'(':
                case (byte)')':
                case (byte)'\\':
                    output.WriteByte((byte)'\\');
                    output.WriteByte(item);
                    break;
                case (byte)'\n': output.Write("\\n"u8); break;
                case (byte)'\r': output.Write("\\r"u8); break;
                case (byte)'\t': output.Write("\\t"u8); break;
                case (byte)'\b': output.Write("\\b"u8); break;
                case (byte)'\f': output.Write("\\f"u8); break;
                case >= 0x20 and <= 0x7E:
                    output.WriteByte(item);
                    break;
                default:
                    output.WriteByte((byte)'\\');
                    output.WriteByte((byte)('0' + ((item >> 6) & 0x07)));
                    output.WriteByte((byte)('0' + ((item >> 3) & 0x07)));
                    output.WriteByte((byte)('0' + (item & 0x07)));
                    break;
            }
        }
        output.WriteByte((byte)')');
    }

    private static string FormatReal(double value)
    {
        if (value == 0)
            return "0.0";

        string roundTrip = value.ToString("R", CultureInfo.InvariantCulture);
        int exponentMarker = roundTrip.IndexOfAny(['E', 'e']);
        string expanded = exponentMarker < 0
            ? roundTrip
            : ExpandExponent(roundTrip, exponentMarker);

        if (expanded.Contains('.'))
        {
            expanded = expanded.TrimEnd('0');
            if (expanded.EndsWith('.'))
                expanded += "0";
            return expanded;
        }
        return expanded + ".0";
    }

    private static string ExpandExponent(string value, int exponentMarker)
    {
        int exponent = int.Parse(value.AsSpan(exponentMarker + 1), CultureInfo.InvariantCulture);
        string mantissa = value[..exponentMarker];
        bool negative = mantissa.StartsWith('-');
        if (negative)
            mantissa = mantissa[1..];

        int decimalPoint = mantissa.IndexOf('.');
        int originalDecimalPosition = decimalPoint < 0 ? mantissa.Length : decimalPoint;
        string digits = decimalPoint < 0 ? mantissa : mantissa.Remove(decimalPoint, 1);
        int decimalPosition = checked(originalDecimalPosition + exponent);

        string result;
        if (decimalPosition <= 0)
            result = "0." + new string('0', -decimalPosition) + digits;
        else if (decimalPosition >= digits.Length)
            result = digits + new string('0', decimalPosition - digits.Length);
        else
            result = digits.Insert(decimalPosition, ".");
        return negative ? "-" + result : result;
    }

    private static bool IsRegularNameByte(byte value) =>
        value is >= 0x21 and <= 0x7E
        && value is not (byte)'#'
        && value is not (byte)'%'
        && value is not (byte)'('
        && value is not (byte)')'
        && value is not (byte)'<'
        && value is not (byte)'>'
        && value is not (byte)'['
        && value is not (byte)']'
        && value is not (byte)'{'
        && value is not (byte)'}'
        && value is not (byte)'/';

    private static void WriteAscii(Stream output, string value)
    {
        foreach (char character in value)
        {
            if (character > 0x7F)
                throw new InvalidOperationException("Canonical PDF syntax must be ASCII.");
            output.WriteByte((byte)character);
        }
    }

    private sealed class PdfNameByteComparer : IComparer<PdfName>
    {
        public static PdfNameByteComparer Instance { get; } = new();

        public int Compare(PdfName? left, PdfName? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            return left.Bytes.Span.SequenceCompareTo(right.Bytes.Span);
        }
    }
}
