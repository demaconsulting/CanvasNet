// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO CFF charstring charstrings subr subrs
// cspell:ignore hintmask cntrmask rmoveto hmoveto vmoveto rlineto hlineto vlineto
// cspell:ignore rrcurveto hhcurveto vvcurveto hvcurveto vhcurveto callsubr callgsubr
// cspell:ignore endchar seac hstem vstem hstemhm vstemhm nominalWidthX defaultWidthX
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a font's <c>CFF </c> (Compact Font Format) table - header, Name/Top DICT/String/
///     Global Subr INDEXes, Private DICT, Local Subr INDEX, and CharStrings INDEX - and decodes
///     individual glyph outlines from Type 2 charstring bytecode into <see cref="Path"/> geometry
///     via <see cref="CffCharstringInterpreter"/>.
/// </summary>
/// <remarks>
///     <para>
///     Every structural offset inside a CFF table is relative to the start of the CFF table's own
///     byte range, never the overall font file - this type therefore slices the CFF table's bytes
///     into their own private array at construction time, so every offset it subsequently reads
///     or hands to <see cref="CffCharstringInterpreter"/> is relative to that array's start
///     (position <c>0</c>), matching the format's own convention exactly.
///     </para>
///     <para>
///     Only non-CID-keyed CFF fonts are supported: a Top DICT declaring the <c>ROS</c> operator
///     (CID-keyed font identification) is rejected with <see cref="InvalidDataException"/> rather
///     than attempting unsupported <c>FDArray</c>/<c>FDSelect</c> parsing - mirroring
///     <see cref="TrueTypeFont"/>'s existing "reject rather than silently mis-parse" posture for
///     unsupported <c>maxp</c> versions. A Top DICT is also required to resolve to exactly one
///     entry (the non-CID-keyed case always has exactly one).
///     </para>
///     <para>
///     Like <see cref="GlyfLocaReader"/>, no individual glyph's charstring is decoded until
///     <see cref="GetGlyphOutline"/> is called for that specific index - one corrupt glyph's
///     charstring bytecode does not prevent using every other, otherwise well-formed, glyph in
///     the font.
///     </para>
/// </remarks>
internal sealed class CffTable : IGlyphOutlineSource
{
    private readonly byte[] _cffData;
    private readonly (int Offset, int Length)[] _charStrings;
    private readonly (int Offset, int Length)[] _globalSubrs;
    private readonly (int Offset, int Length)[] _localSubrs;

    /// <summary>
    ///     The <c>ROS</c> Top DICT operator (escape operator <c>12 30</c>), identifying a
    ///     CID-keyed CFF font - explicitly rejected (see this class's remarks).
    /// </summary>
    private const int RosOperator = 1230;

    /// <summary>
    ///     The <c>CharStrings</c> Top DICT operator, giving the offset (relative to the start of
    ///     the CFF table) of the CharStrings INDEX.
    /// </summary>
    private const int CharStringsOperator = 17;

    /// <summary>
    ///     The <c>Private</c> Top DICT operator, giving the Private DICT's <c>(size, offset)</c>
    ///     pair (offset relative to the start of the CFF table).
    /// </summary>
    private const int PrivateOperator = 18;

    /// <summary>
    ///     The <c>Subrs</c> Private DICT operator, giving the Local Subr INDEX offset relative to
    ///     the start of the Private DICT itself (not the CFF table).
    /// </summary>
    private const int SubrsOperator = 19;

    /// <summary>
    ///     The total number of glyphs described by the CharStrings INDEX.
    /// </summary>
    public int GlyphCount => _charStrings.Length;

    private CffTable(
        byte[] cffData,
        (int Offset, int Length)[] charStrings,
        (int Offset, int Length)[] globalSubrs,
        (int Offset, int Length)[] localSubrs)
    {
        _cffData = cffData;
        _charStrings = charStrings;
        _globalSubrs = globalSubrs;
        _localSubrs = localSubrs;
    }

    /// <summary>
    ///     Parses a font's <c>CFF </c> table.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="tableOffset">The offset of the <c>CFF </c> table within <paramref name="data"/>.</param>
    /// <param name="tableLength">The length of the <c>CFF </c> table.</param>
    /// <returns>A new <see cref="CffTable"/> ready to decode glyph outlines.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the CFF header, an INDEX, or a DICT is malformed or truncated, when the
    ///     header's major version is not <c>1</c>, when the Top DICT INDEX does not resolve to
    ///     exactly one entry, when the Top DICT declares a CID-keyed (<c>ROS</c>) font, when the
    ///     required <c>CharStrings</c> operator is missing, or when the CharStrings INDEX
    ///     contains zero glyphs.
    /// </exception>
    public static CffTable Parse(byte[] data, int tableOffset, int tableLength)
    {
        if (tableLength < 4)
        {
            throw new InvalidDataException("The 'CFF ' table is too short to contain a CFF header.");
        }

        var cffData = new byte[tableLength];
        Array.Copy(data, tableOffset, cffData, 0, tableLength);

        var majorVersion = cffData[0];
        if (majorVersion != 1)
        {
            throw new InvalidDataException($"Unsupported CFF header major version ({majorVersion}).");
        }

        var headerSize = cffData[2];
        if (headerSize > cffData.Length)
        {
            throw new InvalidDataException("The CFF header size exceeds the table bounds.");
        }

        var pos = (int)headerSize;
        ParseIndex(cffData, ref pos); // Name INDEX - parsed only to skip past it; not exposed
        var topDictIndex = ParseIndex(cffData, ref pos);
        if (topDictIndex.Length != 1)
        {
            throw new InvalidDataException("CFF fonts with other than exactly one Top DICT are not supported.");
        }

        ParseIndex(cffData, ref pos); // String INDEX - parsed only to skip past it; not exposed
        var globalSubrs = ParseIndex(cffData, ref pos);

        var topDict = ParseDict(cffData, topDictIndex[0].Offset, topDictIndex[0].Length);
        if (topDict.ContainsKey(RosOperator))
        {
            throw new InvalidDataException("CID-keyed CFF fonts are not supported.");
        }

        if (!topDict.TryGetValue(CharStringsOperator, out var charStringsOperands) || charStringsOperands.Count == 0)
        {
            throw new InvalidDataException("The CFF Top DICT is missing the required 'CharStrings' operator.");
        }

        var localSubrs = Array.Empty<(int Offset, int Length)>();
        if (topDict.TryGetValue(PrivateOperator, out var privateOperands))
        {
            if (privateOperands.Count != 2)
            {
                throw new InvalidDataException("The CFF Top DICT's 'Private' operator must have exactly two operands.");
            }

            var privateSize = (int)privateOperands[0];
            var privateOffset = (int)privateOperands[1];
            if (privateSize < 0 || privateOffset < 0 || checked((long)privateOffset + privateSize) > cffData.Length)
            {
                throw new InvalidDataException("The CFF Private DICT offset/size exceeds the table bounds.");
            }

            var privateDict = ParseDict(cffData, privateOffset, privateSize);
            if (privateDict.TryGetValue(SubrsOperator, out var subrsOperands) && subrsOperands.Count > 0)
            {
                var localSubrPos = privateOffset + (int)subrsOperands[^1];
                localSubrs = ParseIndex(cffData, ref localSubrPos);
            }
        }

        var charStringsPos = (int)charStringsOperands[^1];
        var charStrings = ParseIndex(cffData, ref charStringsPos);
        if (charStrings.Length == 0)
        {
            throw new InvalidDataException("The CFF 'CharStrings' INDEX contains no glyphs.");
        }

        return new CffTable(cffData, charStrings, globalSubrs, localSubrs);
    }

    /// <summary>
    ///     Decodes a single glyph's outline from Type 2 charstring bytecode.
    /// </summary>
    /// <param name="glyphIndex">The glyph index to decode.</param>
    /// <returns>
    ///     The glyph's outline in raw font design units, or <see cref="Path.Empty"/> for a glyph
    ///     with no contour data (for example <c>space</c>).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="glyphIndex"/> is outside <c>[0, GlyphCount)</c>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the glyph's charstring bytecode is malformed, truncated, uses an
    ///     unsupported operator, or exceeds an internal recursion/step bound. See
    ///     <see cref="CffCharstringInterpreter"/> for the complete set of rejection conditions.
    /// </exception>
    public Path GetGlyphOutline(int glyphIndex)
    {
        if (glyphIndex < 0 || glyphIndex >= GlyphCount)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphIndex), glyphIndex, "Glyph index is out of range.");
        }

        return CffCharstringInterpreter.Decode(_cffData, _charStrings[glyphIndex], _globalSubrs, _localSubrs);
    }

    /// <summary>
    ///     Parses a CFF INDEX structure starting at <paramref name="pos"/>, returning every
    ///     entry's absolute <c>(Offset, Length)</c> within <paramref name="data"/> and advancing
    ///     <paramref name="pos"/> to just past the INDEX.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the INDEX's count/offset-size header, offset array, or object data region
    ///     is truncated, declares an unsupported offset size, or contains non-monotonically
    ///     non-decreasing offsets.
    /// </exception>
    private static (int Offset, int Length)[] ParseIndex(byte[] data, ref int pos)
    {
        EnsureAvailable(data, pos, 2);
        var count = SfntContainer.ReadUInt16(data, pos);
        pos += 2;
        if (count == 0)
        {
            return [];
        }

        EnsureAvailable(data, pos, 1);
        var offSize = data[pos];
        pos += 1;
        if (offSize is < 1 or > 4)
        {
            throw new InvalidDataException($"CFF INDEX declares an unsupported offset size ({offSize}).");
        }

        var offsetsCount = count + 1;
        EnsureAvailable(data, pos, offsetsCount * offSize);
        var offsets = new int[offsetsCount];
        for (var i = 0; i < offsetsCount; i++)
        {
            long value = 0;
            for (var b = 0; b < offSize; b++)
            {
                value = (value << 8) | data[pos + i * offSize + b];
            }

            offsets[i] = (int)value;
        }

        pos += offsetsCount * offSize;
        var dataStart = pos;

        var result = new (int Offset, int Length)[count];
        for (var i = 0; i < count; i++)
        {
            var start = offsets[i] - 1;
            var end = offsets[i + 1] - 1;
            if (start < 0 || end < start)
            {
                throw new InvalidDataException("CFF INDEX offsets are not monotonically non-decreasing.");
            }

            var absoluteStart = dataStart + start;
            var length = end - start;
            EnsureAvailable(data, absoluteStart, length);
            result[i] = (absoluteStart, length);
        }

        pos = dataStart + (offsets[^1] - 1);
        return result;
    }

    /// <summary>
    ///     Parses a CFF DICT (Top DICT or Private DICT) into a map from operator code to its
    ///     operand list. Two-byte escape operators (<c>12 XX</c>) are keyed as <c>1200 + XX</c>.
    ///     Real-number operands are consumed (so parsing stays correctly positioned) but not
    ///     interpreted, since no operator this type reads (<c>CharStrings</c>, <c>Private</c>,
    ///     <c>Subrs</c>, <c>ROS</c>) ever carries a real-number operand.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when an operand or escape operator is truncated, or a reserved/invalid DICT byte
    ///     is encountered.
    /// </exception>
    private static Dictionary<int, List<double>> ParseDict(byte[] data, int offset, int length)
    {
        var dict = new Dictionary<int, List<double>>();
        var operands = new List<double>();
        var end = offset + length;
        if (offset < 0 || length < 0 || end > data.Length)
        {
            throw new InvalidDataException("CFF DICT data exceeds the table bounds.");
        }

        var pos = offset;
        while (pos < end)
        {
            var b0 = data[pos];
            if (b0 <= 21)
            {
                int op = b0;
                pos++;
                if (b0 == 12)
                {
                    if (pos >= end)
                    {
                        throw new InvalidDataException("CFF DICT escape operator is truncated.");
                    }

                    op = 1200 + data[pos];
                    pos++;
                }

                dict[op] = [.. operands];
                operands.Clear();
            }
            else if (b0 == 28)
            {
                if (pos + 3 > end)
                {
                    throw new InvalidDataException("CFF DICT operand is truncated.");
                }

                operands.Add(SfntContainer.ReadInt16(data, pos + 1));
                pos += 3;
            }
            else if (b0 == 29)
            {
                if (pos + 5 > end)
                {
                    throw new InvalidDataException("CFF DICT operand is truncated.");
                }

                operands.Add(SfntContainer.ReadInt32(data, pos + 1));
                pos += 5;
            }
            else if (b0 == 30)
            {
                pos++;
                var terminated = false;
                while (pos < end && !terminated)
                {
                    var nibbles = data[pos];
                    pos++;
                    if ((nibbles >> 4) == 0xF || (nibbles & 0xF) == 0xF)
                    {
                        terminated = true;
                    }
                }

                operands.Add(0); // Real-number value is never interpreted; see this method's summary
            }
            else if (b0 is >= 32 and <= 246)
            {
                operands.Add(b0 - 139);
                pos++;
            }
            else if (b0 is >= 247 and <= 250)
            {
                if (pos + 2 > end)
                {
                    throw new InvalidDataException("CFF DICT operand is truncated.");
                }

                operands.Add(((b0 - 247) * 256) + data[pos + 1] + 108);
                pos += 2;
            }
            else if (b0 is >= 251 and <= 254)
            {
                if (pos + 2 > end)
                {
                    throw new InvalidDataException("CFF DICT operand is truncated.");
                }

                operands.Add((-(b0 - 251) * 256) - data[pos + 1] - 108);
                pos += 2;
            }
            else
            {
                throw new InvalidDataException($"CFF DICT contains a reserved/invalid byte (0x{b0:X2}).");
            }
        }

        return dict;
    }

    /// <summary>
    ///     Validates that <paramref name="length"/> bytes starting at <paramref name="pos"/> lie
    ///     within <paramref name="data"/>'s bounds, throwing otherwise.
    /// </summary>
    private static void EnsureAvailable(byte[] data, int pos, int length)
    {
        if (pos < 0 || length < 0 || checked((long)pos + length) > data.Length)
        {
            throw new InvalidDataException("CFF data is truncated.");
        }
    }
}
