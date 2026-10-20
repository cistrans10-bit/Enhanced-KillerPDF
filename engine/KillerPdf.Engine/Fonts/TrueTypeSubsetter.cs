using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KillerPdf.Engine.Fonts;

internal static class TrueTypeSubsetter
{
    private const ushort CompositeMoreComponents = 0x0020;
    private static readonly HashSet<string> RetainedTables =
    [
        "OS/2", "cmap", "cvt ", "fpgm", "gasp", "glyf", "head", "hhea",
        "hmtx", "loca", "maxp", "name", "post", "prep"
    ];

    public static byte[] Create(byte[] source, int glyphCount, IEnumerable<ushort> glyphIds)
    {
        Dictionary<string, Table> directory = ReadDirectory(source);
        if (!directory.TryGetValue("head", out Table head)
            || !directory.TryGetValue("loca", out Table loca)
            || !directory.TryGetValue("glyf", out Table glyf))
            throw new NotSupportedException("TrueType subsetting requires head, loca, and glyf tables.");
        if (head.Length < 54)
            throw new FormatException("The TrueType head table is truncated.");

        int locaFormat = S16(source, head.Offset + 50);
        if (locaFormat is not 0 and not 1)
            throw new FormatException("The TrueType loca format is invalid.");
        uint[] offsets = ReadLocations(source, loca, glyphCount, locaFormat, glyf.Length);
        var retained = new SortedSet<ushort> { 0 };
        foreach (ushort glyph in glyphIds)
        {
            if (glyph >= glyphCount)
                throw new ArgumentOutOfRangeException(nameof(glyphIds), $"Glyph {glyph} is outside the font.");
            retained.Add(glyph);
        }
        AddCompositeDependencies(source, glyf, offsets, retained, glyphCount);

        byte[] subsetGlyf = BuildGlyphTable(source, glyf, offsets, retained, glyphCount, out byte[] subsetLoca);
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach ((string tag, Table table) in directory)
        {
            if (RetainedTables.Contains(tag))
                tables[tag] = source.AsSpan(table.Offset, table.Length).ToArray();
        }
        tables["glyf"] = subsetGlyf;
        tables["loca"] = subsetLoca;
        BinaryPrimitives.WriteInt16BigEndian(tables["head"].AsSpan(50, 2), 1);
        tables["head"].AsSpan(8, 4).Clear();
        return BuildFont(tables);
    }

    public static string Prefix(byte[] fontData, IEnumerable<ushort> glyphIds)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(fontData);
        Span<byte> glyph = stackalloc byte[2];
        foreach (ushort value in glyphIds.Order())
        {
            BinaryPrimitives.WriteUInt16BigEndian(glyph, value);
            hash.AppendData(glyph);
        }
        byte[] digest = hash.GetHashAndReset();
        Span<char> prefix = stackalloc char[6];
        for (int index = 0; index < prefix.Length; index++)
            prefix[index] = (char)('A' + digest[index] % 26);
        return new string(prefix);
    }

    private static Dictionary<string, Table> ReadDirectory(byte[] source)
    {
        int count = U16(source, 4);
        var result = new Dictionary<string, Table>(StringComparer.Ordinal);
        for (int index = 0; index < count; index++)
        {
            int record = 12 + index * 16;
            string tag = Encoding.ASCII.GetString(source, record, 4);
            int offset = checked((int)U32(source, record + 8));
            int length = checked((int)U32(source, record + 12));
            result[tag] = new Table(offset, length);
        }
        return result;
    }

    private static uint[] ReadLocations(
        byte[] source, Table loca, int glyphCount, int format, int glyfLength)
    {
        int width = format == 0 ? 2 : 4;
        if (loca.Length < checked((glyphCount + 1) * width))
            throw new FormatException("The TrueType loca table is truncated.");
        var result = new uint[glyphCount + 1];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = format == 0
                ? (uint)U16(source, loca.Offset + index * 2) * 2
                : U32(source, loca.Offset + index * 4);
            if (result[index] > glyfLength || (index > 0 && result[index] < result[index - 1]))
                throw new FormatException("The TrueType glyph locations are invalid.");
        }
        return result;
    }

    private static void AddCompositeDependencies(
        byte[] source, Table glyf, uint[] offsets, SortedSet<ushort> retained, int glyphCount)
    {
        var pending = new Queue<ushort>(retained);
        while (pending.TryDequeue(out ushort glyph))
        {
            int start = checked(glyf.Offset + (int)offsets[glyph]);
            int length = checked((int)(offsets[glyph + 1] - offsets[glyph]));
            if (length == 0)
                continue;
            if (length < 10)
                throw new FormatException("A TrueType glyph header is truncated.");
            if (S16(source, start) >= 0)
                continue;
            int position = start + 10;
            int end = start + length;
            ushort flags;
            do
            {
                if (position + 4 > end)
                    throw new FormatException("A composite TrueType glyph is truncated.");
                flags = U16(source, position);
                ushort component = U16(source, position + 2);
                if (component >= glyphCount)
                    throw new FormatException("A composite glyph references an invalid glyph.");
                if (retained.Add(component))
                    pending.Enqueue(component);
                position += 4;
                position += (flags & 0x0001) != 0 ? 4 : 2;
                if ((flags & 0x0008) != 0) position += 2;
                else if ((flags & 0x0040) != 0) position += 4;
                else if ((flags & 0x0080) != 0) position += 8;
                if (position > end)
                    throw new FormatException("A composite TrueType glyph is truncated.");
            }
            while ((flags & CompositeMoreComponents) != 0);
        }
    }

    private static byte[] BuildGlyphTable(
        byte[] source, Table glyf, uint[] offsets, SortedSet<ushort> retained,
        int glyphCount, out byte[] loca)
    {
        using var output = new MemoryStream();
        var newOffsets = new uint[glyphCount + 1];
        for (int glyph = 0; glyph < glyphCount; glyph++)
        {
            newOffsets[glyph] = checked((uint)output.Position);
            if (retained.Contains((ushort)glyph))
            {
                int start = checked(glyf.Offset + (int)offsets[glyph]);
                int length = checked((int)(offsets[glyph + 1] - offsets[glyph]));
                output.Write(source, start, length);
                while ((output.Position & 3) != 0)
                    output.WriteByte(0);
            }
        }
        newOffsets[glyphCount] = checked((uint)output.Position);
        loca = new byte[(glyphCount + 1) * 4];
        for (int index = 0; index < newOffsets.Length; index++)
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(index * 4, 4), newOffsets[index]);
        return output.ToArray();
    }

    private static byte[] BuildFont(SortedDictionary<string, byte[]> tables)
    {
        int count = tables.Count;
        int directoryLength = 12 + count * 16;
        int length = directoryLength + tables.Values.Sum(value => Align4(value.Length));
        byte[] result = new byte[length];
        BinaryPrimitives.WriteUInt32BigEndian(result, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(4, 2), checked((ushort)count));
        int maximumPower = 1;
        int selector = 0;
        while (maximumPower * 2 <= count) { maximumPower *= 2; selector++; }
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(6, 2), checked((ushort)(maximumPower * 16)));
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(8, 2), checked((ushort)selector));
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(10, 2), checked((ushort)(count * 16 - maximumPower * 16)));
        int record = 12;
        int offset = directoryLength;
        int headOffset = -1;
        foreach ((string tag, byte[] table) in tables)
        {
            Encoding.ASCII.GetBytes(tag).CopyTo(result, record);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(record + 4, 4), Checksum(table));
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(record + 8, 4), checked((uint)offset));
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(record + 12, 4), checked((uint)table.Length));
            table.CopyTo(result, offset);
            if (tag == "head") headOffset = offset;
            record += 16;
            offset += Align4(table.Length);
        }
        if (headOffset < 0)
            throw new FormatException("The subset font has no head table.");
        uint adjustment = unchecked(0xB1B0AFBAu - Checksum(result));
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(headOffset + 8, 4), adjustment);
        return result;
    }

    private static uint Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        Span<byte> word = stackalloc byte[4];
        for (int index = 0; index < bytes.Length; index += 4)
        {
            word.Clear();
            bytes.Slice(index, Math.Min(4, bytes.Length - index)).CopyTo(word);
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(word));
        }
        return sum;
    }

    private static int Align4(int value) => (value + 3) & ~3;
    private static ushort U16(byte[] source, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(source.AsSpan(offset, 2));
    private static short S16(byte[] source, int offset) =>
        BinaryPrimitives.ReadInt16BigEndian(source.AsSpan(offset, 2));
    private static uint U32(byte[] source, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(offset, 4));
    private readonly record struct Table(int Offset, int Length);
}
