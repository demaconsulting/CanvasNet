// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO ttcf
// cspell:ignore noaccess definefont currentfile closefile nnoaccess
using DemaConsulting.CanvasNet.Fonts;

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
    public static byte[] Head(int unitsPerEm, int indexToLocFormat, int macStyle = 0)
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
        WriteUInt16(buf, macStyle);
        WriteUInt16(buf, 0); // lowestRecPPEM
        WriteInt16(buf, 0); // fontDirectionHint
        WriteInt16(buf, indexToLocFormat);
        WriteInt16(buf, 0); // glyphDataFormat
        return [.. buf];
    }

    /// <summary>
    ///     Builds a full 32-byte <c>maxp</c> table (the required length for version <c>1.0</c>
    ///     per the OpenType/TrueType spec). Every field beyond <c>version</c>/<c>numGlyphs</c> -
    ///     the only two fields <see cref="DemaConsulting.CanvasNet.Fonts.TrueTypeFont"/> reads -
    ///     is filled with a plausible placeholder value; this library never interprets them.
    /// </summary>
    public static byte[] Maxp(int numGlyphs, uint version = 0x00010000)
    {
        var buf = new List<byte>();
        WriteUInt32(buf, version); // version
        WriteUInt16(buf, numGlyphs); // numGlyphs
        WriteUInt16(buf, 0); // maxPoints
        WriteUInt16(buf, 0); // maxContours
        WriteUInt16(buf, 0); // maxCompositePoints
        WriteUInt16(buf, 0); // maxCompositeContours
        WriteUInt16(buf, 1); // maxZones
        WriteUInt16(buf, 0); // maxTwilightPoints
        WriteUInt16(buf, 0); // maxStorage
        WriteUInt16(buf, 0); // maxFunctionDefs
        WriteUInt16(buf, 0); // maxInstructionDefs
        WriteUInt16(buf, 0); // maxStackElements
        WriteUInt16(buf, 0); // maxSizeOfInstructions
        WriteUInt16(buf, 0); // maxComponentElements
        WriteUInt16(buf, 0); // maxComponentDepth
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
    ///     Builds a single-contour simple glyph with <paramref name="numPoints"/> on-curve points
    ///     compactly encoded via TrueType's flag repeat-count and "same as previous" coordinate
    ///     omission, so every point after the first shares flag byte <c>0x31</c>
    ///     (<c>ON_CURVE_POINT | X_IS_SAME_OR_POSITIVE_X_SHORT_VECTOR | Y_IS_SAME_OR_POSITIVE_Y_SHORT_VECTOR</c>)
    ///     and requires no coordinate bytes at all - used to prove that a huge point count is
    ///     representable in only a few hundred bytes, matching the exact amplification the
    ///     total-point budget defends against.
    /// </summary>
    public static byte[] LargeSimpleGlyph(int numPoints)
    {
        var buf = new List<byte>();
        WriteInt16(buf, 1); // numberOfContours
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);
        WriteInt16(buf, 0);
        WriteUInt16(buf, numPoints - 1); // endPtsOfContours[0]
        WriteUInt16(buf, 0); // instructionLength

        // Every point after the first is described purely by a flag byte, never coordinate
        // bytes: TrueType lets a flag mark a point's X and Y as unchanged from the previous
        // point, and lets one flag byte represent a run of identical flags via a repeat count.
        // This block emits the minimum number of flag/repeat-count byte pairs needed to describe
        // all requested points; a repeat count may legitimately be zero, meaning a run of one.
        const byte flag = 0x01 | 0x10 | 0x20 | 0x08; // ON_CURVE | X_SAME | Y_SAME | REPEAT_FLAG
        var remaining = numPoints;
        while (remaining > 0)
        {
            var repeatCount = Math.Min(remaining - 1, 255);
            buf.Add(flag);
            buf.Add((byte)repeatCount);
            remaining -= 1 + repeatCount;
        }

        // Deliberately no X/Y coordinate section follows: every point's flag marks its
        // coordinates as identical to the preceding point, so none are ever encoded.
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
        bool HasTwoByTwo = false,
        bool ScaledComponentOffset = false,
        bool UnscaledComponentOffset = false);

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

            if (component.ScaledComponentOffset)
            {
                flags |= 0x0800; // SCALED_COMPONENT_OFFSET
            }

            if (component.UnscaledComponentOffset)
            {
                flags |= 0x1000; // UNSCALED_COMPONENT_OFFSET
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
        WriteUInt16(subtable, 16 + segCount * 8); // length: declared subtable length, matching the encoding below
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
        WriteUInt32(subtable, (uint)(16 + groups.Count * 12)); // length: declared subtable length, matching the encoding below
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
    ///     Builds a minimal, well-formed raw CFF table byte array (Header, Name INDEX, Top DICT
    ///     INDEX, String INDEX, Global Subr INDEX, an optional Private DICT + Local Subr INDEX,
    ///     the CharStrings INDEX, and an optional custom charset table) directly from
    ///     already-encoded Type 2 charstring bytecode for each glyph - used to exercise
    ///     <see cref="CffTable"/> and <see cref="CffCharstringInterpreter"/> without any
    ///     third-party font fixture.
    /// </summary>
    /// <param name="charStrings">Every glyph's raw Type 2 charstring bytecode, glyph 0 first.</param>
    /// <param name="globalSubrs">Every global subroutine's raw bytecode, index order.</param>
    /// <param name="localSubrs">Every local subroutine's raw bytecode, index order.</param>
    /// <param name="includeRos">
    ///     When <see langword="true"/>, the Top DICT declares the <c>ROS</c> operator (CID-keyed
    ///     font identification) so <see cref="CffTable"/>'s CID-rejection path can be exercised;
    ///     <paramref name="charStrings"/> is then not required to be well-formed, since the CID
    ///     check happens first.
    /// </param>
    /// <param name="includePrivate">
    ///     When <see langword="false"/>, the Top DICT omits the <c>Private</c> operator entirely
    ///     (a legitimate CFF font may have no Private DICT at all), so no local subroutines are
    ///     reachable regardless of <paramref name="localSubrs"/>, and
    ///     <paramref name="defaultWidthX"/>/<paramref name="nominalWidthX"/> are not written.
    /// </param>
    /// <param name="stringIndexEntries">
    ///     The font's own custom String INDEX entries (SID <c>391</c> and above, in index order) -
    ///     empty (the default) when the test has no custom glyph names to resolve.
    /// </param>
    /// <param name="charsetId">
    ///     When set, the Top DICT's <c>charset</c> operator is written with this predefined
    ///     charset ID (<c>0</c>/<c>1</c>/<c>2</c>). Mutually exclusive with
    ///     <paramref name="charsetTable"/>; when both are <see langword="null"/> (the default),
    ///     the <c>charset</c> operator is omitted entirely (the ISOAdobe/Standard default).
    /// </param>
    /// <param name="charsetTable">
    ///     When set, a custom charset table's already-encoded raw bytes (format byte plus data),
    ///     appended after every other section; the Top DICT's <c>charset</c> operator is written
    ///     with this table's own computed byte offset. Mutually exclusive with
    ///     <paramref name="charsetId"/>.
    /// </param>
    /// <param name="defaultWidthX">
    ///     When set (and <paramref name="includePrivate"/> is <see langword="true"/>), the Private
    ///     DICT's <c>defaultWidthX</c> operator (<c>20</c>) is written with this value.
    /// </param>
    /// <param name="nominalWidthX">
    ///     When set (and <paramref name="includePrivate"/> is <see langword="true"/>), the Private
    ///     DICT's <c>nominalWidthX</c> operator (<c>21</c>) is written with this value.
    /// </param>
    public static byte[] Cff(
        IReadOnlyList<byte[]> charStrings,
        IReadOnlyList<byte[]>? globalSubrs = null,
        IReadOnlyList<byte[]>? localSubrs = null,
        bool includeRos = false,
        bool includePrivate = true,
        IReadOnlyList<byte[]>? stringIndexEntries = null,
        int? charsetId = null,
        byte[]? charsetTable = null,
        int? defaultWidthX = null,
        int? nominalWidthX = null)
    {
        globalSubrs ??= [];
        localSubrs ??= [];
        stringIndexEntries ??= [];

        byte[] header = [1, 0, 4, 4]; // major, minor, hdrSize, offSize (offSize is advisory only)
        var nameIndex = WriteCffIndex([System.Text.Encoding.ASCII.GetBytes("Synthetic")]);
        var stringIndex = WriteCffIndex(stringIndexEntries);
        var globalSubrIndex = WriteCffIndex(globalSubrs);
        var charStringsIndex = WriteCffIndex(charStrings);

        // The Private DICT's own content is built up operator-by-operator (every operand always
        // using the fixed 5-byte integer encoding, so each operator contributes a fixed 6 bytes),
        // so the Local Subr INDEX's offset relative to the Private DICT's own start - the 'Subrs'
        // operand - is always exactly "bytes already written", computed as each operator is added
        // rather than hardcoded, now that defaultWidthX/nominalWidthX may also be present.
        var privateDict = new List<byte>();
        if (includePrivate && defaultWidthX.HasValue)
        {
            WriteDictInt(privateDict, defaultWidthX.Value);
            WriteDictOperator(privateDict, 20); // defaultWidthX
        }

        if (includePrivate && nominalWidthX.HasValue)
        {
            WriteDictInt(privateDict, nominalWidthX.Value);
            WriteDictOperator(privateDict, 21); // nominalWidthX
        }

        if (includePrivate && localSubrs.Count > 0)
        {
            WriteDictInt(privateDict, privateDict.Count + 6);
            WriteDictOperator(privateDict, 19); // Subrs
        }

        var privateDictBytes = privateDict.ToArray();
        var localSubrIndex = WriteCffIndex(localSubrs);

        // Two-pass layout: the Top DICT's own byte length is fixed by which operators it
        // contains (every operand uses the fixed 5-byte integer encoding), independent of the
        // operand *values* - so a placeholder Top DICT (all offsets 0, but with a placeholder
        // 'charset' operand present whenever the real one will be) has the exact same length as
        // the final one, letting every other section's position be computed from it before the
        // Top DICT's real offset values are known.
        var hasCharsetOperator = charsetId.HasValue || charsetTable is not null;
        var topDictPlaceholder = BuildTopDict(0, 0, 0, hasCharsetOperator ? 0 : null, includePrivate, includeRos);
        var topDictIndexPlaceholder = WriteCffIndex([topDictPlaceholder]);

        var prefixLength = header.Length + nameIndex.Length + topDictIndexPlaceholder.Length + stringIndex.Length + globalSubrIndex.Length;
        var charStringsOffset = prefixLength;
        var privateOffset = charStringsOffset + charStringsIndex.Length;
        var privateSize = privateDictBytes.Length;
        var charsetTableOffset = privateOffset + privateSize + localSubrIndex.Length;

        var charsetOperand = charsetTable is not null ? charsetTableOffset : charsetId;
        var topDict = BuildTopDict(charStringsOffset, privateOffset, privateSize, charsetOperand, includePrivate, includeRos);
        var topDictIndex = WriteCffIndex([topDict]);

        var buf = new List<byte>();
        buf.AddRange(header);
        buf.AddRange(nameIndex);
        buf.AddRange(topDictIndex);
        buf.AddRange(stringIndex);
        buf.AddRange(globalSubrIndex);
        buf.AddRange(charStringsIndex);
        buf.AddRange(privateDictBytes);
        buf.AddRange(localSubrIndex);
        if (charsetTable is not null)
        {
            buf.AddRange(charsetTable);
        }

        return [.. buf];
    }

    /// <summary>
    ///     Builds a Top DICT's raw bytes, including the <c>CharStrings</c> operator, an optional
    ///     <c>Private</c> operator, an optional <c>charset</c> operator, and (for CID-rejection
    ///     tests) an optional <c>ROS</c> operator - every operand encoded with the fixed 5-byte
    ///     integer form, so this method's return length depends only on
    ///     <paramref name="includePrivate"/>/<paramref name="charsetOperand"/>'s null-ness/
    ///     <paramref name="includeRos"/>, never on the operand values themselves.
    /// </summary>
    private static byte[] BuildTopDict(
        int charStringsOffset, int privateOffset, int privateSize, int? charsetOperand, bool includePrivate, bool includeRos)
    {
        var buf = new List<byte>();
        if (includeRos)
        {
            WriteDictInt(buf, 0);
            WriteDictInt(buf, 0);
            WriteDictInt(buf, 0);
            WriteDictOperator(buf, 1230); // ROS
        }

        if (charsetOperand.HasValue)
        {
            WriteDictInt(buf, charsetOperand.Value);
            WriteDictOperator(buf, 15); // charset
        }

        WriteDictInt(buf, charStringsOffset);
        WriteDictOperator(buf, 17); // CharStrings

        if (includePrivate)
        {
            WriteDictInt(buf, privateSize);
            WriteDictInt(buf, privateOffset);
            WriteDictOperator(buf, 18); // Private
        }

        return [.. buf];
    }

    /// <summary>
    ///     Appends a CFF DICT integer operand using the fixed 5-byte form (<c>29</c> followed by a
    ///     big-endian <see langword="int"/>) - always exactly 5 bytes, regardless of magnitude,
    ///     which is what lets <see cref="BuildTopDict"/>'s length stay independent of its operand
    ///     values (see <see cref="Cff"/>'s remarks on its two-pass layout).
    /// </summary>
    private static void WriteDictInt(List<byte> buf, int value)
    {
        buf.Add(29);
        WriteUInt32(buf, unchecked((uint)value));
    }

    /// <summary>
    ///     Appends a CFF DICT operator: a single byte for an operator code below <c>1200</c>, or
    ///     the two-byte escape form (<c>12</c>, <c>code - 1200</c>) otherwise.
    /// </summary>
    private static void WriteDictOperator(List<byte> buf, int op)
    {
        if (op >= 1200)
        {
            buf.Add(12);
            buf.Add((byte)(op - 1200));
        }
        else
        {
            buf.Add((byte)op);
        }
    }

    /// <summary>
    ///     Appends a Type 2 charstring numeric operand using the smallest applicable encoding: the
    ///     compact single/two-byte forms for <c>-1131..1131</c>, the 3-byte <c>28</c> form for the
    ///     rest of the 16-bit signed range, or the 5-byte <c>255</c> 16.16 fixed-point form for
    ///     anything larger, or for a non-integral value.
    /// </summary>
    public static void WriteCharstringNumber(List<byte> buf, double value)
    {
        var isInteger = Math.Abs(value - Math.Round(value)) < double.Epsilon;
        if (isInteger && value is >= -32768 and <= 32767)
        {
            var intValue = (int)value;
            switch (intValue)
            {
                case >= -107 and <= 107:
                    buf.Add((byte)(intValue + 139));
                    return;
                case >= 108 and <= 1131:
                    buf.Add((byte)(((intValue - 108) / 256) + 247));
                    buf.Add((byte)((intValue - 108) % 256));
                    return;
                case >= -1131 and <= -108:
                    var positive = -intValue - 108;
                    buf.Add((byte)((positive / 256) + 251));
                    buf.Add((byte)(positive % 256));
                    return;
                default:
                    buf.Add(28);
                    WriteInt16(buf, intValue);
                    return;
            }
        }

        WriteCharstringFixed(buf, value);
    }

    /// <summary>
    ///     Appends a Type 2 charstring numeric operand using the 5-byte <c>255</c> 16.16
    ///     fixed-point form, regardless of whether <paramref name="value"/> could fit a more
    ///     compact integer encoding - used to deliberately exercise the fixed-point decode path.
    /// </summary>
    public static void WriteCharstringFixed(List<byte> buf, double value)
    {
        buf.Add(255);
        WriteUInt32(buf, unchecked((uint)(int)Math.Round(value * 65536.0)));
    }

    /// <summary>
    ///     Appends a Type 2 charstring operator: a single byte for an operator code below
    ///     <c>1200</c> (the two-byte escape range base used by this test support's own encoding,
    ///     matching <see cref="WriteDictOperator"/>'s convention), or the two-byte escape form
    ///     (<c>12</c>, <c>code - 1200</c>) otherwise.
    /// </summary>
    public static void WriteCharstringOperator(List<byte> buf, int op)
    {
        if (op >= 1200)
        {
            buf.Add(12);
            buf.Add((byte)(op - 1200));
        }
        else
        {
            buf.Add((byte)op);
        }
    }

    /// <summary>
    ///     Builds a raw CFF INDEX structure's bytes (<c>count</c>, <c>offSize</c>, the 1-based
    ///     offset array, and the concatenated entry bytes) from a list of already-encoded entries.
    ///     Writes only the 2-byte <c>count</c> field (no <c>offSize</c>/offset array/data) when
    ///     <paramref name="entries"/> is empty, per the CFF INDEX format.
    /// </summary>
    public static byte[] WriteCffIndex(IReadOnlyList<byte[]> entries)
    {
        var buf = new List<byte>();
        WriteUInt16(buf, entries.Count);
        if (entries.Count == 0)
        {
            return [.. buf];
        }

        var totalDataLength = entries.Sum(e => e.Length);
        var maxOffset = totalDataLength + 1;
        var offSize = maxOffset switch
        {
            <= 0xFF => 1,
            <= 0xFFFF => 2,
            <= 0xFFFFFF => 3,
            _ => 4,
        };

        buf.Add((byte)offSize);

        var offset = 1;
        WriteOffset(buf, offset, offSize);
        foreach (var entry in entries)
        {
            offset += entry.Length;
            WriteOffset(buf, offset, offSize);
        }

        foreach (var entry in entries)
        {
            buf.AddRange(entry);
        }

        return [.. buf];
    }

    /// <summary>
    ///     Appends a CFF INDEX offset-array entry as a big-endian, unsigned integer occupying
    ///     exactly <paramref name="offSize"/> bytes.
    /// </summary>
    private static void WriteOffset(List<byte> buf, int value, int offSize)
    {
        for (var i = offSize - 1; i >= 0; i--)
        {
            buf.Add((byte)((value >> (8 * i)) & 0xFF));
        }
    }

    /// <summary>
    ///     Assembles two or more independently-built, standalone SFNT font byte arrays (each
    ///     already internally self-consistent, with table directory offsets relative to its own
    ///     start) into a single <c>ttcf</c> TrueType Collection container, rewriting each face's
    ///     table directory entries' offsets to be absolute from the start of the container -
    ///     exactly the transformation a real multi-face <c>ttcf</c> container's table directory
    ///     entries already reflect (see <see cref="SfntContainer"/>'s remarks).
    /// </summary>
    /// <param name="faces">Every face's complete, standalone SFNT byte array, in face order.</param>
    /// <returns>A single well-formed <c>ttcf</c> container holding every given face.</returns>
    public static byte[] Ttc(IReadOnlyList<byte[]> faces)
    {
        var numFonts = faces.Count;
        var headerSize = 12 + 4 * numFonts;
        var faceStarts = new int[numFonts];
        var cursor = headerSize;
        for (var i = 0; i < numFonts; i++)
        {
            faceStarts[i] = cursor;
            cursor += faces[i].Length;
        }

        var buf = new List<byte>();
        WriteUInt32(buf, 0x74746366); // ttcTag ('ttcf')
        WriteUInt16(buf, 1); // majorVersion
        WriteUInt16(buf, 0); // minorVersion
        WriteUInt32(buf, (uint)numFonts);
        foreach (var faceStart in faceStarts)
        {
            WriteUInt32(buf, (uint)faceStart);
        }

        for (var i = 0; i < numFonts; i++)
        {
            var faceBytes = (byte[])faces[i].Clone();
            var numTables = (faceBytes[4] << 8) | faceBytes[5];
            for (var t = 0; t < numTables; t++)
            {
                var entryOffset = 12 + t * 16 + 8;
                var original =
                    ((uint)faceBytes[entryOffset] << 24) |
                    ((uint)faceBytes[entryOffset + 1] << 16) |
                    ((uint)faceBytes[entryOffset + 2] << 8) |
                    faceBytes[entryOffset + 3];
                var rewritten = original + (uint)faceStarts[i];
                faceBytes[entryOffset] = (byte)((rewritten >> 24) & 0xFF);
                faceBytes[entryOffset + 1] = (byte)((rewritten >> 16) & 0xFF);
                faceBytes[entryOffset + 2] = (byte)((rewritten >> 8) & 0xFF);
                faceBytes[entryOffset + 3] = (byte)(rewritten & 0xFF);
            }

            buf.AddRange(faceBytes);
        }

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

    /// <summary>
    ///     Describes one <c>name</c> table record to be built by <see cref="Name"/>.
    /// </summary>
    public readonly record struct NameRecord(int PlatformId, int EncodingId, int LanguageId, int NameId, string Value);

    /// <summary>
    ///     Builds a format-0 <c>name</c> table from an ordered list of records, encoding each
    ///     record's string as big-endian UTF-16 for a Windows platform record
    ///     (<see cref="NameRecord.PlatformId"/> <c>3</c>) or as ASCII for a Macintosh platform
    ///     record (<see cref="NameRecord.PlatformId"/> <c>1</c>) - sufficient for every test in
    ///     this suite, since no synthetic fixture needs a genuinely non-ASCII Mac Roman string.
    /// </summary>
    public static byte[] Name(IReadOnlyList<NameRecord> records)
    {
        var stringData = new List<byte>();
        var entries = new List<(int Offset, int Length)>();
        foreach (var record in records)
        {
            var encoded = record.PlatformId == 1
                ? System.Text.Encoding.ASCII.GetBytes(record.Value)
                : System.Text.Encoding.BigEndianUnicode.GetBytes(record.Value);
            entries.Add((stringData.Count, encoded.Length));
            stringData.AddRange(encoded);
        }

        var buf = new List<byte>();
        WriteUInt16(buf, 0); // format
        WriteUInt16(buf, records.Count);
        WriteUInt16(buf, 6 + records.Count * 12); // stringOffset

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var (offset, length) = entries[i];
            WriteUInt16(buf, record.PlatformId);
            WriteUInt16(buf, record.EncodingId);
            WriteUInt16(buf, record.LanguageId);
            WriteUInt16(buf, record.NameId);
            WriteUInt16(buf, length);
            WriteUInt16(buf, offset);
        }

        buf.AddRange(stringData);
        return [.. buf];
    }

    /// <summary>
    ///     Builds a full 64-byte version-0 <c>OS/2</c> table (long enough to include
    ///     <c>fsSelection</c>), with every field beyond <c>usWeightClass</c>/<c>fsSelection</c> -
    ///     the only two fields <see cref="StyleTable"/> reads - filled with a plausible
    ///     placeholder value.
    /// </summary>
    public static byte[] Os2(int usWeightClass, int fsSelection)
    {
        var buf = new List<byte>();
        WriteUInt16(buf, 0); // version
        WriteInt16(buf, 0); // xAvgCharWidth
        WriteUInt16(buf, usWeightClass);
        WriteUInt16(buf, 5); // usWidthClass
        WriteUInt16(buf, 0); // fsType
        WriteInt16(buf, 0); // ySubscriptXSize
        WriteInt16(buf, 0); // ySubscriptYSize
        WriteInt16(buf, 0); // ySubscriptXOffset
        WriteInt16(buf, 0); // ySubscriptYOffset
        WriteInt16(buf, 0); // ySuperscriptXSize
        WriteInt16(buf, 0); // ySuperscriptYSize
        WriteInt16(buf, 0); // ySuperscriptXOffset
        WriteInt16(buf, 0); // ySuperscriptYOffset
        WriteInt16(buf, 0); // yStrikeoutSize
        WriteInt16(buf, 0); // yStrikeoutPosition
        WriteInt16(buf, 0); // sFamilyClass
        for (var i = 0; i < 10; i++)
        {
            buf.Add(0); // panose
        }

        WriteUInt32(buf, 0); // ulUnicodeRange1
        WriteUInt32(buf, 0); // ulUnicodeRange2
        WriteUInt32(buf, 0); // ulUnicodeRange3
        WriteUInt32(buf, 0); // ulUnicodeRange4
        buf.AddRange("SYNT"u8.ToArray()); // achVendID
        WriteUInt16(buf, fsSelection);
        return [.. buf];
    }

    /// <summary>
    ///     Builds a full 32-byte version-3.0 <c>post</c> table (long enough to include
    ///     <c>isFixedPitch</c>), with every field beyond <c>italicAngle</c>/<c>isFixedPitch</c> -
    ///     the only two fields <see cref="StyleTable"/> reads - filled with a plausible
    ///     placeholder value.
    /// </summary>
    public static byte[] Post(int isFixedPitch, double italicAngle = 0)
    {
        var buf = new List<byte>();
        WriteUInt32(buf, 0x00030000); // version 3.0: no glyph name arrays follow
        WriteUInt32(buf, unchecked((uint)(int)Math.Round(italicAngle * 65536.0))); // italicAngle (Fixed)
        WriteInt16(buf, 0); // underlinePosition
        WriteInt16(buf, 0); // underlineThickness
        WriteUInt32(buf, unchecked((uint)isFixedPitch));
        WriteUInt32(buf, 0); // minMemType42
        WriteUInt32(buf, 0); // maxMemType42
        WriteUInt32(buf, 0); // minMemType1
        WriteUInt32(buf, 0); // maxMemType1
        return [.. buf];
    }

    /// <summary>
    ///     Appends a Type 1 charstring numeric operand using the smallest applicable encoding.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="WriteCharstringNumber"/> (Type 2's encoding), Type 1 has no 3-byte
    ///     <c>28</c> form - anything outside the compact single/two-byte ranges falls straight
    ///     through to the 5-byte <c>255</c> form, which for Type 1 is a plain 32-bit signed
    ///     integer (not Type 2's 16.16 fixed-point value).
    /// </remarks>
    public static void WriteType1CharstringNumber(List<byte> buf, int value)
    {
        switch (value)
        {
            case >= -107 and <= 107:
                buf.Add((byte)(value + 139));
                return;
            case >= 108 and <= 1131:
                buf.Add((byte)(((value - 108) / 256) + 247));
                buf.Add((byte)((value - 108) % 256));
                return;
            case >= -1131 and <= -108:
                var positive = -value - 108;
                buf.Add((byte)((positive / 256) + 251));
                buf.Add((byte)(positive % 256));
                return;
            default:
                buf.Add(255);
                WriteUInt32(buf, unchecked((uint)value));
                return;
        }
    }

    /// <summary>
    ///     Appends a Type 1 charstring operator: a single byte for an operator code below
    ///     <c>1200</c>, or the two-byte escape form (<c>12</c>, <c>code - 1200</c>) otherwise -
    ///     matching <see cref="WriteCharstringOperator"/>'s convention for the escape range base.
    /// </summary>
    public static void WriteType1CharstringOperator(List<byte> buf, int op)
    {
        if (op >= 1200)
        {
            buf.Add(12);
            buf.Add((byte)(op - 1200));
        }
        else
        {
            buf.Add((byte)op);
        }
    }

    /// <summary>
    ///     Builds a complete, hand-authored classic PostScript Type 1 font program from already
    ///     hand-encoded glyph charstrings and (optional) subroutines, returning the byte layout
    ///     <see cref="Type1Table.Parse"/> expects directly: <c>Length1</c> cleartext bytes
    ///     immediately followed by <c>Length2</c> <c>eexec</c>-encrypted bytes.
    /// </summary>
    /// <param name="charStrings">
    ///     Every glyph's name and already-encoded (plaintext, not yet charstring-encrypted)
    ///     charstring bytes, in <c>/CharStrings</c> dict-encounter order.
    /// </param>
    /// <param name="subrs">
    ///     Every subroutine's already-encoded (plaintext) charstring bytes, indexed by
    ///     subroutine number (<see langword="null"/> or empty for no <c>/Subrs</c> dictionary).
    /// </param>
    /// <param name="lenIv">
    ///     The <c>/lenIV</c> value to declare and to use for individual charstring/subroutine
    ///     encryption lead-in padding. Only an explicit <c>/lenIV NN def</c> declaration is
    ///     emitted when this differs from the Type 1 Font Format's documented default of <c>4</c>.
    /// </param>
    /// <param name="readToken">
    ///     The (arbitrary, font-specific) "read binary data" procedure name token to emit between
    ///     each entry's declared length and its raw bytes - varying this across test fixtures
    ///     proves <see cref="Type1Table"/>'s scanner never depends on its literal spelling.
    /// </param>
    /// <param name="defToken">
    ///     The (arbitrary, font-specific) "define" procedure name token to emit immediately after
    ///     each entry's raw bytes.
    /// </param>
    /// <param name="trailer">
    ///     Optional raw ASCII text to append immediately after the private dictionary's closing
    ///     <c>end\nend\n</c> boilerplate, before <c>eexec</c>-encryption - defaults to empty, so
    ///     every existing call site is unaffected. Lets a test fixture reproduce the trailing
    ///     font-closing PostScript boilerplate (for example <c>readonly put\nnoaccess put\ndup
    ///     /FontName get exch definefont pop\nmark currentfile closefile\n</c>) that some
    ///     real-world Type 1 producers emit after the <c>/CharStrings</c> dictionary closes.
    /// </param>
    /// <param name="charStringLengthOverrides">
    ///     Optional per-glyph overrides for the declared <c>/CharStrings</c> entry length token,
    ///     keyed by glyph name - lets a test fixture declare a length that diverges from the
    ///     entry's real encrypted byte count (for example near <see cref="int.MaxValue"/>), to
    ///     simulate a crafted malicious font, while the actual appended encrypted bytes remain
    ///     unaffected. Defaults to <c>null</c>, so every existing call site is unaffected.
    /// </param>
    /// <returns>The assembled font program bytes, and its cleartext/encrypted region lengths.</returns>
    public static (byte[] FontFileBytes, int Length1, int Length2) Type1(
        IReadOnlyList<(string Name, byte[] Charstring)> charStrings,
        IReadOnlyList<byte[]>? subrs = null,
        int lenIv = 4,
        string readToken = "RD",
        string defToken = "ND",
        string trailer = "",
        IReadOnlyDictionary<string, int>? charStringLengthOverrides = null)
    {
        subrs ??= [];

        var cleartext = System.Text.Encoding.ASCII.GetBytes(
            "%!PS-AdobeFont-1.0: Synthetic\n/FontName /Synthetic def\ncurrentfile eexec\n");

        var plaintext = new List<byte>();
        void AppendAscii(string s) => plaintext.AddRange(System.Text.Encoding.ASCII.GetBytes(s));

        AppendAscii("dup /Private 15 dict dup begin\n");
        if (lenIv != 4)
        {
            AppendAscii($"/lenIV {lenIv} def\n");
        }

        if (subrs.Count > 0)
        {
            AppendAscii($"/Subrs {subrs.Count} array\n");
            for (var i = 0; i < subrs.Count; i++)
            {
                var encrypted = EncryptType1Entry(subrs[i], lenIv);
                AppendAscii($"dup {i} {encrypted.Length} {readToken} ");
                plaintext.AddRange(encrypted);
                AppendAscii($" {defToken}\n");
            }
        }

        AppendAscii($"/CharStrings {charStrings.Count} dict dup begin\n");
        foreach (var (name, charstring) in charStrings)
        {
            var encrypted = EncryptType1Entry(charstring, lenIv);
            var declaredLength = charStringLengthOverrides != null
                && charStringLengthOverrides.TryGetValue(name, out var overrideLength)
                ? overrideLength
                : encrypted.Length;
            AppendAscii($"/{name} {declaredLength} {readToken} ");
            plaintext.AddRange(encrypted);
            AppendAscii($" {defToken}\n");
        }

        AppendAscii("end\nend\n");
        if (trailer.Length > 0)
        {
            AppendAscii(trailer);
        }

        // The eexec cipher's own leading discard count is fixed at 4 bytes, regardless of the
        // font's declared '/lenIV' (which only governs individual charstring/subroutine entries).
        var withLeadIn = new byte[4 + plaintext.Count];
        withLeadIn[0] = 0x01;
        withLeadIn[1] = 0x02;
        withLeadIn[2] = 0x03;
        withLeadIn[3] = 0x04;
        plaintext.CopyTo(withLeadIn, 4);

        var encryptedPrivateDict = EncryptType1(withLeadIn, EexecR0);

        var fontFileBytes = new byte[cleartext.Length + encryptedPrivateDict.Length];
        cleartext.CopyTo(fontFileBytes, 0);
        encryptedPrivateDict.CopyTo(fontFileBytes, cleartext.Length);

        return (fontFileBytes, cleartext.Length, encryptedPrivateDict.Length);
    }

    /// <summary>
    ///     Encrypts a single charstring/subroutine entry's plaintext bytes, prefixed with
    ///     <paramref name="lenIv"/> arbitrary lead-in bytes, using the classic PostScript Type 1
    ///     charstring cipher key - the inverse of what the production <c>Type1Table.Parse</c>
    ///     parser performs when reading the entry back. This helper avoids referencing the
    ///     internal <c>Type1CharstringDecryption</c>/<c>Type1Table</c> types directly (their own
    ///     assembly only grants <c>InternalsVisibleTo</c> to the core test project, not to the
    ///     other test projects that link this shared file), so the cipher key constant is
    ///     duplicated locally as <see cref="CharstringR0"/>.
    /// </summary>
    private static byte[] EncryptType1Entry(byte[] plainCharstring, int lenIv)
    {
        var withLeadIn = new byte[lenIv + plainCharstring.Length];
        for (var i = 0; i < lenIv; i++)
        {
            withLeadIn[i] = (byte)(0x55 + i); // arbitrary, non-zero lead-in padding
        }

        plainCharstring.CopyTo(withLeadIn, lenIv);
        return EncryptType1(withLeadIn, CharstringR0);
    }

    /// <summary>
    ///     The classic PostScript Type 1 "eexec" cipher's initial key, duplicated locally from the
    ///     published Type 1 Font Format specification (see remarks on <see cref="EncryptType1Entry"/>
    ///     for why this is not simply referenced from the internal production type).
    /// </summary>
    private const ushort EexecR0 = 55665;

    /// <summary>
    ///     The classic PostScript Type 1 individual charstring/subroutine cipher's initial key,
    ///     duplicated locally from the published Type 1 Font Format specification (see remarks on
    ///     <see cref="EncryptType1Entry"/> for why this is not simply referenced from the internal
    ///     production type).
    /// </summary>
    private const ushort CharstringR0 = 4330;

    /// <summary>
    ///     Encrypts <paramref name="plain"/> using the classic PostScript Type 1 "eexec" additive
    ///     stream cipher - the forward direction of the same published cipher that production code
    ///     only ever decrypts.
    /// </summary>
    /// <remarks>
    ///     This is deliberately not implemented by reusing the production decryption routine with
    ///     its arguments swapped: the cipher's key-update step must always feed back the
    ///     <em>ciphertext</em> byte, not the plaintext byte, so encryption and decryption apply
    ///     the same recurrence in opposite output roles rather than being literally the same
    ///     function call with its input and output swapped.
    /// </remarks>
    private static byte[] EncryptType1(byte[] plain, ushort r)
    {
        const ushort c1 = 52845;
        const ushort c2 = 22719;

        var cipher = new byte[plain.Length];
        var key = r;
        for (var i = 0; i < plain.Length; i++)
        {
            var c = (byte)(plain[i] ^ (key >> 8));
            cipher[i] = c;
            key = (ushort)(((c + key) * c1) + c2);
        }

        return cipher;
    }

    /// <summary>
    ///     Re-serializes a Type 1 font program (already in <see cref="Type1Table.Parse"/>'s
    ///     <c>Length1</c>/<c>Length2</c> byte layout - see <see cref="Type1"/>) into the classic
    ///     "PFB" binary segment framing: a cleartext segment (<c>0x80 0x01</c>), a binary segment
    ///     (<c>0x80 0x02</c>), and a terminating end-of-file marker (<c>0x80 0x03</c>).
    /// </summary>
    public static byte[] Type1Pfb(byte[] fontFileBytes, int length1, int length2)
    {
        var buf = new List<byte>();
        AppendPfbSegment(buf, 0x01, fontFileBytes, 0, length1);
        AppendPfbSegment(buf, 0x02, fontFileBytes, length1, length2);
        buf.Add(0x80);
        buf.Add(0x03);
        return [.. buf];
    }

    private static void AppendPfbSegment(List<byte> buf, byte segmentType, byte[] data, int offset, int length)
    {
        buf.Add(0x80);
        buf.Add(segmentType);
        buf.Add((byte)(length & 0xFF));
        buf.Add((byte)((length >> 8) & 0xFF));
        buf.Add((byte)((length >> 16) & 0xFF));
        buf.Add((byte)((length >> 24) & 0xFF));
        buf.AddRange(new ArraySegment<byte>(data, offset, length));
    }

    /// <summary>
    ///     Re-serializes a Type 1 font program (already in <see cref="Type1Table.Parse"/>'s
    ///     <c>Length1</c>/<c>Length2</c> byte layout - see <see cref="Type1"/>) into the plain-ASCII
    ///     "PFA" framing: the cleartext bytes verbatim (already containing the literal
    ///     <c>eexec</c> keyword - see <see cref="Type1"/>), followed by the encrypted region
    ///     hex-encoded, wrapped at <paramref name="hexLineWidth"/> characters per line to also
    ///     exercise <see cref="Type1PfaReader"/>'s embedded-whitespace tolerance.
    /// </summary>
    public static byte[] Type1Pfa(byte[] fontFileBytes, int length1, int length2, int hexLineWidth = 64)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(System.Text.Encoding.Latin1.GetString(fontFileBytes, 0, length1));

        for (var i = 0; i < length2; i++)
        {
            if (i > 0 && i % hexLineWidth == 0)
            {
                sb.Append('\n');
            }

            sb.Append(fontFileBytes[length1 + i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        sb.Append('\n');
        return System.Text.Encoding.Latin1.GetBytes(sb.ToString());
    }
}
