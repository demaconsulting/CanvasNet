namespace DemaConsulting.CanvasNet.Tests.TestSupport;

/// <summary>
///     Hand-rolled builder for constructing minimal, well-formed synthetic SFNT/TrueType font
///     byte arrays (and the individual table byte arrays they are built from) for the
///     <c>Fonts</c> namespace's tests, without any third-party font fixture and therefore no font
///     licensing consideration. Also exposes the shared big-endian primitive-writing helpers used
///     to hand-construct deliberately malformed variants directly in each test file, mirroring
///     <see cref="BoundedReadStream"/>/<see cref="NonSeekableStream"/>'s role as shared test
///     infrastructure for the <c>Codecs</c> tests.
/// </summary>
internal sealed class SyntheticFontBuilder
{
    private readonly List<(string Tag, byte[] Data)> _tables = [];
    private uint _sfntVersion = 0x00010000;

    /// <summary>
    ///     Overrides the <c>sfntVersion</c> written to the offset table (defaults to
    ///     <c>0x00010000</c>, the standard TrueType value).
    /// </summary>
    public SyntheticFontBuilder WithSfntVersion(uint version)
    {
        _sfntVersion = version;
        return this;
    }

    /// <summary>
    ///     Adds (or replaces) a table's raw bytes.
    /// </summary>
    public SyntheticFontBuilder AddTable(string tag, byte[] data)
    {
        _tables.RemoveAll(t => t.Tag == tag);
        _tables.Add((tag, data));
        return this;
    }

    /// <summary>
    ///     Assembles the offset table, table directory, and every added table's bytes into a
    ///     complete, well-formed SFNT byte array.
    /// </summary>
    public byte[] Build()
    {
        var buf = new List<byte>();
        WriteUInt32(buf, _sfntVersion);
        WriteUInt16(buf, (ushort)_tables.Count);
        WriteUInt16(buf, 0); // searchRange
        WriteUInt16(buf, 0); // entrySelector
        WriteUInt16(buf, 0); // rangeShift

        var directoryStart = buf.Count;
        var directorySize = _tables.Count * 16;
        var dataStart = directoryStart + directorySize;

        // Reserve directory space, filled in below once every table's absolute offset is known
        for (var i = 0; i < directorySize; i++)
        {
            buf.Add(0);
        }

        var offsets = new int[_tables.Count];
        var cursor = dataStart;
        for (var i = 0; i < _tables.Count; i++)
        {
            offsets[i] = cursor;
            buf.AddRange(_tables[i].Data);
            cursor += _tables[i].Data.Length;
        }

        for (var i = 0; i < _tables.Count; i++)
        {
            var entryOffset = directoryStart + i * 16;
            var tag = _tables[i].Tag;
            for (var c = 0; c < 4; c++)
            {
                buf[entryOffset + c] = (byte)(c < tag.Length ? tag[c] : ' ');
            }

            WriteUInt32At(buf, entryOffset + 4, 0); // checksum (never validated)
            WriteUInt32At(buf, entryOffset + 8, (uint)offsets[i]);
            WriteUInt32At(buf, entryOffset + 12, (uint)_tables[i].Data.Length);
        }

        return [.. buf];
    }

    /// <summary>
    ///     Appends a big-endian, unsigned 16-bit integer.
    /// </summary>
    public static void WriteUInt16(List<byte> buf, int value)
    {
        buf.Add((byte)((value >> 8) & 0xFF));
        buf.Add((byte)(value & 0xFF));
    }

    /// <summary>
    ///     Appends a big-endian, signed 16-bit integer.
    /// </summary>
    public static void WriteInt16(List<byte> buf, int value) => WriteUInt16(buf, value & 0xFFFF);

    /// <summary>
    ///     Appends a big-endian, unsigned 32-bit integer.
    /// </summary>
    public static void WriteUInt32(List<byte> buf, uint value)
    {
        buf.Add((byte)((value >> 24) & 0xFF));
        buf.Add((byte)((value >> 16) & 0xFF));
        buf.Add((byte)((value >> 8) & 0xFF));
        buf.Add((byte)(value & 0xFF));
    }

    /// <summary>
    ///     Appends a big-endian F2Dot14 fixed-point value.
    /// </summary>
    public static void WriteF2Dot14(List<byte> buf, float value) => WriteInt16(buf, (int)Math.Round(value * 16384f));

    /// <summary>
    ///     Overwrites 4 bytes at an absolute index within an in-progress buffer with a big-endian
    ///     unsigned 32-bit integer - used to backfill table directory fields once every table's
    ///     final offset is known.
    /// </summary>
    private static void WriteUInt32At(List<byte> buf, int index, uint value)
    {
        buf[index] = (byte)((value >> 24) & 0xFF);
        buf[index + 1] = (byte)((value >> 16) & 0xFF);
        buf[index + 2] = (byte)((value >> 8) & 0xFF);
        buf[index + 3] = (byte)(value & 0xFF);
    }

    /// <summary>
    ///     Builds a 54-byte <c>head</c> table.
    /// </summary>
    public static byte[] Head(int unitsPerEm, int indexToLocFormat)
    {
        var buf = new List<byte>();
        WriteUInt32(buf, 0x00010000); // version
        WriteUInt32(buf, 0x00010000); // fontRevision
        WriteUInt32(buf, 0); // checkSumAdjustment
        WriteUInt32(buf, 0x5F0F3CF5); // magicNumber
        WriteUInt16(buf, 0); // flags
        WriteUInt16(buf, unitsPerEm);
        for (var i = 0; i < 16; i++)
        {
            buf.Add(0); // created/modified (int64 x2 = 16 bytes)
        }

        WriteInt16(buf, 0); // xMin
        WriteInt16(buf, 0); // yMin
        WriteInt16(buf, 0); // xMax
        WriteInt16(buf, 0); // yMax
        WriteUInt16(buf, 0); // macStyle
        WriteUInt16(buf, 0); // lowestRecPPEM
        WriteInt16(buf, 0); // fontDirectionHint
        WriteInt16(buf, indexToLocFormat);
        WriteInt16(buf, 0); // glyphDataFormat
        return [.. buf];
    }

    /// <summary>
    ///     Builds a 6-byte <c>maxp</c> table (version 1.0 fixed-size prefix only - sufficient for
    ///     every field <see cref="DemaConsulting.CanvasNet.Fonts.TrueTypeFont"/> reads).
    /// </summary>
    public static byte[] Maxp(int numGlyphs, uint version = 0x00010000)
    {
        var buf = new List<byte>();
        WriteUInt32(buf, version);
        WriteUInt16(buf, numGlyphs);
        return [.. buf];
    }

    /// <summary>
    ///     Builds a 36-byte <c>hhea</c> table.
    /// </summary>
    public static byte[] Hhea(int ascender, int descender, int lineGap, int numOfLongHorMetrics)
    {
        var buf = new List<byte>();
        WriteUInt32(buf, 0x00010000); // version
        WriteInt16(buf, ascender);
        WriteInt16(buf, descender);
        WriteInt16(buf, lineGap);
        WriteUInt16(buf, 0); // advanceWidthMax
        WriteInt16(buf, 0); // minLeftSideBearing
        WriteInt16(buf, 0); // minRightSideBearing
        WriteInt16(buf, 0); // xMaxExtent
        WriteInt16(buf, 0); // caretSlopeRise
        WriteInt16(buf, 0); // caretSlopeRun
        WriteInt16(buf, 0); // caretOffset
        for (var i = 0; i < 4; i++)
        {
            WriteInt16(buf, 0); // reserved x4
        }

        WriteInt16(buf, 0); // metricDataFormat
        WriteUInt16(buf, numOfLongHorMetrics);
        return [.. buf];
    }

    /// <summary>
    ///     Builds an <c>hmtx</c> table with one long <c>(advanceWidth, lsb)</c> entry per glyph in
    ///     <paramref name="advanceWidths"/>, optionally followed by <paramref name="tailLsbCount"/>
    ///     left-side-bearing-only (zero) entries for glyphs beyond <c>numOfLongHorMetrics</c>.
    /// </summary>
    public static byte[] Hmtx(IReadOnlyList<int> advanceWidths, int tailLsbCount = 0)
    {
        var buf = new List<byte>();
        foreach (var advance in advanceWidths)
        {
            WriteUInt16(buf, advance);
            WriteInt16(buf, 0); // lsb
        }

        for (var i = 0; i < tailLsbCount; i++)
        {
            WriteInt16(buf, 0); // lsb-only tail entry
        }

        return [.. buf];
    }

    /// <summary>
    ///     Builds a <c>loca</c> table from each glyph's byte length within <c>glyf</c>.
    /// </summary>
    public static byte[] Loca(IReadOnlyList<int> glyphLengths, bool longFormat)
    {
        var offsets = new int[glyphLengths.Count + 1];
        for (var i = 0; i < glyphLengths.Count; i++)
        {
            offsets[i + 1] = offsets[i] + glyphLengths[i];
        }

        var buf = new List<byte>();
        foreach (var offset in offsets)
        {
            if (longFormat)
            {
                WriteUInt32(buf, (uint)offset);
            }
            else
            {
                WriteUInt16(buf, offset / 2);
            }
        }

        return [.. buf];
    }

    /// <summary>
    ///     Builds a simple glyph's contour data from one or more contours, each an ordered list of
    ///     <c>(X, Y, OnCurve)</c> points. Every coordinate delta is encoded as a full 16-bit value
    ///     (flag bits for the short-vector/same-value encodings are left clear), which is
    ///     sufficient to exercise every on-curve/off-curve contour shape the decoder supports.
    /// </summary>
    public static byte[] SimpleGlyph(params (int X, int Y, bool OnCurve)[][] contours)
    {
        var buf = new List<byte>();
        WriteInt16(buf, contours.Length);
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);

        var runningEnd = -1;
        foreach (var contour in contours)
        {
            runningEnd += contour.Length;
            WriteUInt16(buf, runningEnd);
        }

        WriteUInt16(buf, 0); // instructionLength

        var allPoints = contours.SelectMany(c => c).ToArray();
        foreach (var point in allPoints)
        {
            buf.Add((byte)(point.OnCurve ? 0x01 : 0x00));
        }

        var prevX = 0;
        foreach (var point in allPoints)
        {
            WriteInt16(buf, point.X - prevX);
            prevX = point.X;
        }

        var prevY = 0;
        foreach (var point in allPoints)
        {
            WriteInt16(buf, point.Y - prevY);
            prevY = point.Y;
        }

        PadToEvenLength(buf);
        return [.. buf];
    }

    /// <summary>
    ///     Appends a single zero pad byte if <paramref name="buf"/>'s length is odd - real
    ///     <c>glyf</c> table entries are always padded to an even length, and the short
    ///     (<c>Offset16</c>) <c>loca</c> format can only represent even byte offsets.
    /// </summary>
    private static void PadToEvenLength(List<byte> buf)
    {
        if (buf.Count % 2 != 0)
        {
            buf.Add(0);
        }
    }

    /// <summary>
    ///     Describes one component of a synthetic composite glyph.
    /// </summary>
    public readonly record struct CompositeComponent(
        int GlyphIndex,
        int Dx,
        int Dy,
        bool ArgsAreXyValues = true,
        float A = 1f,
        float B = 0f,
        float C = 0f,
        float D = 1f,
        bool HasScale = false,
        bool HasXyScale = false,
        bool HasTwoByTwo = false);

    /// <summary>
    ///     Builds a composite glyph (<c>numberOfContours == -1</c>) from an ordered list of
    ///     components. <c>MORE_COMPONENTS</c> is set automatically on every component but the
    ///     last, and <c>ARG_1_AND_2_ARE_WORDS</c> is always set (arguments always encoded as
    ///     16-bit values).
    /// </summary>
    public static byte[] CompositeGlyph(IReadOnlyList<CompositeComponent> components)
    {
        var buf = new List<byte>();
        WriteInt16(buf, -1);
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);

        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            var flags = 0x0001; // ARG_1_AND_2_ARE_WORDS
            if (component.ArgsAreXyValues)
            {
                flags |= 0x0002;
            }

            if (component.HasScale)
            {
                flags |= 0x0008;
            }

            if (i < components.Count - 1)
            {
                flags |= 0x0020; // MORE_COMPONENTS
            }

            if (component.HasXyScale)
            {
                flags |= 0x0040;
            }

            if (component.HasTwoByTwo)
            {
                flags |= 0x0080;
            }

            WriteUInt16(buf, flags);
            WriteUInt16(buf, component.GlyphIndex);
            WriteInt16(buf, component.Dx);
            WriteInt16(buf, component.Dy);

            if (component.HasScale)
            {
                WriteF2Dot14(buf, component.A);
            }
            else if (component.HasXyScale)
            {
                WriteF2Dot14(buf, component.A);
                WriteF2Dot14(buf, component.D);
            }
            else if (component.HasTwoByTwo)
            {
                WriteF2Dot14(buf, component.A);
                WriteF2Dot14(buf, component.B);
                WriteF2Dot14(buf, component.C);
                WriteF2Dot14(buf, component.D);
            }
        }

        PadToEvenLength(buf);
        return [.. buf];
    }

    /// <summary>
    ///     Builds a <c>cmap</c> table with a single format 4 subtable under the given
    ///     platform/encoding IDs, mapping each <c>(codepoint, glyphId)</c> pair given.
    /// </summary>
    public static byte[] CmapFormat4(int platformId, int encodingId, IReadOnlyList<(int Codepoint, int GlyphId)> mappings)
    {
        var subtable = new List<byte>();
        var ordered = mappings.OrderBy(m => m.Codepoint).ToList();

        // One segment per mapped codepoint (simplest possible correct encoding), plus the
        // mandatory trailing 0xFFFF terminator segment.
        var segCount = ordered.Count + 1;
        WriteUInt16(subtable, 4); // format
        WriteUInt16(subtable, 0); // length placeholder (not validated by the decoder)
        WriteUInt16(subtable, 0); // language
        WriteUInt16(subtable, segCount * 2);
        WriteUInt16(subtable, 0); // searchRange
        WriteUInt16(subtable, 0); // entrySelector
        WriteUInt16(subtable, 0); // rangeShift

        foreach (var (codepoint, _) in ordered)
        {
            WriteUInt16(subtable, codepoint);
        }

        WriteUInt16(subtable, 0xFFFF);

        WriteUInt16(subtable, 0); // reservedPad

        foreach (var (codepoint, _) in ordered)
        {
            WriteUInt16(subtable, codepoint);
        }

        WriteUInt16(subtable, 0xFFFF);

        foreach (var (codepoint, glyphId) in ordered)
        {
            WriteInt16(subtable, glyphId - codepoint);
        }

        WriteInt16(subtable, 1); // terminator segment idDelta (endCode==startCode==0xFFFF -> glyph 0)

        for (var i = 0; i < segCount; i++)
        {
            WriteUInt16(subtable, 0); // idRangeOffset (always 0: glyph id comes from idDelta)
        }

        return WrapSingleSubtableCmap(platformId, encodingId, [.. subtable]);
    }

    /// <summary>
    ///     Builds a <c>cmap</c> table with a single format 12 subtable under the given
    ///     platform/encoding IDs, mapping each contiguous <c>(startCharCode, endCharCode,
    ///     startGlyphId)</c> group given.
    /// </summary>
    public static byte[] CmapFormat12(int platformId, int encodingId, IReadOnlyList<(uint Start, uint End, uint StartGlyphId)> groups)
    {
        var subtable = new List<byte>();
        WriteUInt16(subtable, 12); // format
        WriteUInt16(subtable, 0); // reserved
        WriteUInt32(subtable, 0); // length placeholder
        WriteUInt32(subtable, 0); // language
        WriteUInt32(subtable, (uint)groups.Count);

        foreach (var (start, end, startGlyphId) in groups)
        {
            WriteUInt32(subtable, start);
            WriteUInt32(subtable, end);
            WriteUInt32(subtable, startGlyphId);
        }

        return WrapSingleSubtableCmap(platformId, encodingId, [.. subtable]);
    }

    /// <summary>
    ///     Wraps a single already-encoded subtable's bytes in a minimal <c>cmap</c> table header
    ///     (one encoding record).
    /// </summary>
    private static byte[] WrapSingleSubtableCmap(int platformId, int encodingId, byte[] subtableBytes)
    {
        var buf = new List<byte>();
        WriteUInt16(buf, 0); // version
        WriteUInt16(buf, 1); // numTables
        WriteUInt16(buf, platformId);
        WriteUInt16(buf, encodingId);
        WriteUInt32(buf, 12); // subtable offset (immediately after this one 12-byte header)
        buf.AddRange(subtableBytes);
        return [.. buf];
    }

    /// <summary>
    ///     Builds a <c>kern</c> table with a single format 0, horizontal-only subtable containing
    ///     the given sorted <c>(left, right, value)</c> pairs.
    /// </summary>
    public static byte[] KernFormat0(IReadOnlyList<(int Left, int Right, int Value)> pairs)
    {
        var body = new List<byte>();
        WriteUInt16(body, pairs.Count);
        WriteUInt16(body, 0); // searchRange
        WriteUInt16(body, 0); // entrySelector
        WriteUInt16(body, 0); // rangeShift
        foreach (var (left, right, value) in pairs)
        {
            WriteUInt16(body, left);
            WriteUInt16(body, right);
            WriteInt16(body, value);
        }

        var subtableLength = 6 + body.Count;
        var subtable = new List<byte>();
        WriteUInt16(subtable, 0); // subtable version
        WriteUInt16(subtable, subtableLength);
        WriteUInt16(subtable, 0x0001); // coverage: format 0 (high byte), horizontal (bit0)
        subtable.AddRange(body);

        var buf = new List<byte>();
        WriteUInt16(buf, 0); // kern table version
        WriteUInt16(buf, 1); // nTables
        buf.AddRange(subtable);
        return [.. buf];
    }
}
