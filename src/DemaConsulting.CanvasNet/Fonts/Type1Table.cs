// cspell:ignore eexec lenIV charstring charstrings subr subrs notdef hsbw sbw
using System.Globalization;
using System.Text;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a classic PostScript Type 1 font program - a cleartext segment (skipped entirely,
///     never inspected) followed by an <c>eexec</c>-encrypted private-dictionary segment holding
///     <c>/Subrs</c> and <c>/CharStrings</c> - and decodes individual glyph outlines from Type 1
///     charstring bytecode into <see cref="Path"/> geometry via
///     <see cref="Type1CharstringInterpreter"/>.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="Parse"/> skips the cleartext segment's <c>length1</c> bytes entirely - their
///     content (the font's <c>/FontMatrix</c>, its own built-in <c>/Encoding</c>, etc.) is never
///     parsed, a deliberate scope decision documented on <see cref="TrueTypeFont.LoadType1"/> -
///     then <c>eexec</c>-decrypts the following <c>length2</c> bytes
///     (<see cref="Type1CharstringDecryption.EexecR0"/>, discarding the fixed leading 4 bytes) into
///     the private dictionary's plaintext. This exact "skip <c>length1</c>, decrypt <c>length2</c>"
///     byte-layout contract is also what lets <see cref="Type1PfbReader"/>/<see cref="Type1PfaReader"/>
///     feed a standalone <c>.pfb</c>/<c>.pfa</c> font program to this same method unchanged.
///     </para>
///     <para>
///     Within that plaintext, <c>/Subrs</c> and <c>/CharStrings</c> entries are located by an ad
///     hoc, procedure-name-agnostic binary-blob scanner: entries have the structural shape
///     <c>&lt;integer&gt; &lt;word-token&gt; &lt;exactly-that-many-raw-bytes&gt;</c> (preceded by
///     <c>dup &lt;index&gt;</c> for <c>/Subrs</c>, or <c>/&lt;name&gt;</c> for <c>/CharStrings</c>).
///     The word token between the length and the raw bytes is the font's own aliased "read binary
///     data" procedure name (conventionally <c>RD</c>/<c>-|</c> or similar) - its literal spelling
///     varies per font and is never inspected, only skipped, since the entry's boundaries are
///     already fully determined by the declared length. <c>/Subrs</c> and <c>/CharStrings</c>
///     themselves are not aliased - they are the private dictionary's own fixed key names - so
///     locating those two keywords directly is reliable regardless of which font produced the
///     file.
///     </para>
///     <para>
///     Each entry's raw bytes are then individually decrypted
///     (<see cref="Type1CharstringDecryption.CharstringR0"/>, discarding the font's declared
///     <c>/lenIV</c> leading bytes - default <c>4</c> if no <c>/lenIV</c> declaration is present)
///     into a private byte array, copied once at parse time (the same "copy once, offset/length
///     tuples into that array" convention <see cref="CffTable"/> uses for its own CFF table
///     bytes). <c>.notdef</c>, if present, is forced to glyph index <c>0</c>; every other glyph
///     name gets the next sequential glyph index in <c>/CharStrings</c> dict-encounter order.
///     </para>
///     <para>
///     Like <see cref="CffTable"/>, no individual glyph's charstring is decoded until
///     <see cref="GetGlyphOutline"/> is called for that specific index. Each glyph's advance width
///     is instead resolved eagerly at parse time via a cheap <c>hsbw</c>/<c>sbw</c>-only peek
///     (that operator is spec-guaranteed to be the very first in every glyph's charstring, with
///     only literal numeric operands), since a Type 1 font has no <c>hmtx</c>-equivalent table to
///     supply advance widths independently.
///     </para>
/// </remarks>
internal sealed class Type1Table : IGlyphOutlineSource
{
    private readonly byte[] _data;
    private readonly (int Offset, int Length)[] _charStrings;
    private readonly (int Offset, int Length)[] _subrs;
    private readonly int[] _advanceWidths;
    private readonly IReadOnlyDictionary<string, int> _nameToGlyphIndex;

    /// <summary>
    ///     The total number of glyphs described by the <c>/CharStrings</c> dictionary.
    /// </summary>
    public int GlyphCount => _charStrings.Length;

    private Type1Table(
        byte[] data,
        (int Offset, int Length)[] charStrings,
        (int Offset, int Length)[] subrs,
        int[] advanceWidths,
        IReadOnlyDictionary<string, int> nameToGlyphIndex)
    {
        _data = data;
        _charStrings = charStrings;
        _subrs = subrs;
        _advanceWidths = advanceWidths;
        _nameToGlyphIndex = nameToGlyphIndex;
    }

    /// <summary>
    ///     Parses a Type 1 font program from its cleartext/encrypted segment byte counts.
    /// </summary>
    /// <param name="fontFileBytes">
    ///     The complete font program bytes: <paramref name="length1"/> cleartext bytes (skipped,
    ///     never parsed) immediately followed by <paramref name="length2"/> <c>eexec</c>-encrypted
    ///     bytes.
    /// </param>
    /// <param name="length1">The cleartext segment's length, in bytes.</param>
    /// <param name="length2">The encrypted segment's length, in bytes.</param>
    /// <returns>A new <see cref="Type1Table"/> ready to decode glyph outlines.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="length1"/>/<paramref name="length2"/> are negative or
    ///     exceed <paramref name="fontFileBytes"/>'s bounds, when the decrypted private dictionary
    ///     is missing the required <c>/CharStrings</c> keyword, when a <c>/Subrs</c>/
    ///     <c>/CharStrings</c> entry or the optional <c>/lenIV</c> declaration is malformed or
    ///     truncated, or when the <c>/CharStrings</c> dictionary contains no glyphs.
    /// </exception>
    public static Type1Table Parse(byte[] fontFileBytes, int length1, int length2)
    {
        ArgumentNullException.ThrowIfNull(fontFileBytes);
        if (length1 < 0 || length2 < 0)
        {
            throw new InvalidDataException("Type 1 'Length1'/'Length2' must not be negative.");
        }

        if (checked((long)length1 + length2) > fontFileBytes.Length)
        {
            throw new InvalidDataException("Type 1 font program is shorter than its declared 'Length1' + 'Length2'.");
        }

        var plaintext = Type1CharstringDecryption.Decrypt(fontFileBytes, length1, length2, Type1CharstringDecryption.EexecR0, discardCount: 4);
        var text = Encoding.Latin1.GetString(plaintext);

        var lenIv = ParseLenIv(text);

        const string charStringsKeyword = "/CharStrings";
        var charStringsKeywordStart = text.IndexOf(charStringsKeyword, StringComparison.Ordinal);
        if (charStringsKeywordStart < 0)
        {
            throw new InvalidDataException("Type 1 font program is missing the required '/CharStrings' dictionary.");
        }

        const string subrsKeyword = "/Subrs";
        var subrsKeywordStart = text.IndexOf(subrsKeyword, StringComparison.Ordinal);

        var rawSubrs = new Dictionary<int, (int Offset, int Length)>();
        if (subrsKeywordStart >= 0 && subrsKeywordStart < charStringsKeywordStart)
        {
            ScanSubrs(text, subrsKeywordStart + subrsKeyword.Length, charStringsKeywordStart, rawSubrs);
        }

        var rawCharStrings = new List<(string Name, int Offset, int Length)>();
        ScanCharStrings(text, charStringsKeywordStart + charStringsKeyword.Length, text.Length, rawCharStrings);
        if (rawCharStrings.Count == 0)
        {
            throw new InvalidDataException("Type 1 font program's '/CharStrings' dictionary contains no glyphs.");
        }

        // Force '.notdef' (if present) to glyph index 0, preserving encounter order otherwise.
        var notdefEntryIndex = rawCharStrings.FindIndex(e => e.Name == ".notdef");
        if (notdefEntryIndex > 0)
        {
            var notdefEntry = rawCharStrings[notdefEntryIndex];
            rawCharStrings.RemoveAt(notdefEntryIndex);
            rawCharStrings.Insert(0, notdefEntry);
        }

        var combined = new List<byte>();

        var maxSubrIndex = rawSubrs.Count == 0 ? -1 : rawSubrs.Keys.Max();
        var subrs = new (int Offset, int Length)[maxSubrIndex + 1];
        foreach (var (index, entry) in rawSubrs)
        {
            var decrypted = Type1CharstringDecryption.Decrypt(plaintext, entry.Offset, entry.Length, Type1CharstringDecryption.CharstringR0, lenIv);
            subrs[index] = (combined.Count, decrypted.Length);
            combined.AddRange(decrypted);
        }

        var charStrings = new (int Offset, int Length)[rawCharStrings.Count];
        var nameToGlyphIndex = new Dictionary<string, int>();
        for (var i = 0; i < rawCharStrings.Count; i++)
        {
            var entry = rawCharStrings[i];
            var decrypted = Type1CharstringDecryption.Decrypt(plaintext, entry.Offset, entry.Length, Type1CharstringDecryption.CharstringR0, lenIv);
            charStrings[i] = (combined.Count, decrypted.Length);
            combined.AddRange(decrypted);
            nameToGlyphIndex.TryAdd(entry.Name, i); // first-wins on a duplicate glyph name
        }

        var data = combined.ToArray();
        var advanceWidths = new int[charStrings.Length];
        for (var i = 0; i < charStrings.Length; i++)
        {
            advanceWidths[i] = PeekAdvanceWidth(data, charStrings[i]);
        }

        return new Type1Table(data, charStrings, subrs, advanceWidths, nameToGlyphIndex);
    }

    /// <summary>
    ///     Decodes a single glyph's outline from Type 1 charstring bytecode.
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
    ///     unsupported operator (including <c>seac</c>), or exceeds an internal recursion/step
    ///     bound. See <see cref="Type1CharstringInterpreter"/> for the complete set of rejection
    ///     conditions.
    /// </exception>
    public Path GetGlyphOutline(int glyphIndex)
    {
        ValidateGlyphIndex(glyphIndex);
        var (outline, _) = Type1CharstringInterpreter.Decode(_data, _charStrings[glyphIndex], _subrs);
        return outline;
    }

    /// <summary>
    ///     Looks up a glyph's advance width, resolved eagerly at parse time from that glyph's own
    ///     <c>hsbw</c>/<c>sbw</c> operator.
    /// </summary>
    /// <param name="glyphIndex">The glyph index to look up.</param>
    /// <returns>The glyph's advance width, in font design units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="glyphIndex"/> is outside <c>[0, GlyphCount)</c>.
    /// </exception>
    public int GetAdvanceWidth(int glyphIndex)
    {
        ValidateGlyphIndex(glyphIndex);
        return _advanceWidths[glyphIndex];
    }

    /// <summary>
    ///     Looks up a glyph's index by its <c>/CharStrings</c> glyph name.
    /// </summary>
    /// <param name="glyphName">The glyph name to look up.</param>
    /// <param name="glyphIndex">The resolved glyph index, if found.</param>
    /// <returns><see langword="true"/> if <paramref name="glyphName"/> is a known glyph name.</returns>
    public bool TryGetGlyphIndex(string glyphName, out int glyphIndex) => _nameToGlyphIndex.TryGetValue(glyphName, out glyphIndex);

    private void ValidateGlyphIndex(int glyphIndex)
    {
        if (glyphIndex < 0 || glyphIndex >= GlyphCount)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphIndex), glyphIndex, "Glyph index is out of range.");
        }
    }

    /// <summary>
    ///     Parses an optional <c>/lenIV &lt;n&gt; def</c> declaration from the decrypted private
    ///     dictionary plaintext, defaulting to <c>4</c> (the Type 1 Font Format's documented
    ///     default) when no such declaration is present.
    /// </summary>
    private static int ParseLenIv(string text)
    {
        const string keyword = "/lenIV";
        var idx = text.IndexOf(keyword, StringComparison.Ordinal);
        if (idx < 0)
        {
            return 4;
        }

        var pos = idx + keyword.Length;
        if (!NextToken(text, ref pos, text.Length, out var tokenStart, out var tokenEnd))
        {
            throw new InvalidDataException("Type 1 font program's '/lenIV' declaration is truncated.");
        }

        if (!int.TryParse(text.AsSpan(tokenStart, tokenEnd - tokenStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value < 0)
        {
            throw new InvalidDataException("Type 1 font program's '/lenIV' declaration is malformed.");
        }

        return value;
    }

    /// <summary>
    ///     Scans the <c>/Subrs</c> region (from just after the <c>/Subrs</c> keyword through the
    ///     start of the <c>/CharStrings</c> keyword) for <c>dup &lt;index&gt; &lt;length&gt;
    ///     &lt;token&gt; &lt;raw bytes&gt;</c> entries, agnostic to the literal spelling of
    ///     <c>&lt;token&gt;</c> (the font's own aliased "read binary data" procedure name).
    /// </summary>
    private static void ScanSubrs(string text, int start, int end, Dictionary<int, (int Offset, int Length)> result)
    {
        var pos = start;
        while (pos < end)
        {
            if (!NextToken(text, ref pos, end, out var tokenStart, out var tokenEnd))
            {
                break;
            }

            if (tokenEnd - tokenStart != 3 || text[tokenStart] != 'd' || text[tokenStart + 1] != 'u' || text[tokenStart + 2] != 'p')
            {
                continue; // not the 'dup' keyword - skip unrelated tokens between entries
            }

            if (!NextToken(text, ref pos, end, out var idxStart, out var idxEnd))
            {
                throw new InvalidDataException("Type 1 font program's '/Subrs' entry is truncated.");
            }

            if (!int.TryParse(text.AsSpan(idxStart, idxEnd - idxStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                || index < 0)
            {
                throw new InvalidDataException("Type 1 font program's '/Subrs' entry has a malformed index.");
            }

            var length = ReadLengthToken(text, ref pos, end, "'/Subrs'");
            SkipProcNameToken(text, ref pos, end, "'/Subrs'");

            if (pos >= end || !IsWhitespace(text[pos]))
            {
                throw new InvalidDataException("Type 1 font program's '/Subrs' entry is missing its single separator byte before the raw data.");
            }

            pos++;

            if (checked(pos + length) > end)
            {
                throw new InvalidDataException("Type 1 font program's '/Subrs' raw charstring data is truncated.");
            }

            result[index] = (pos, length);
            pos += length;
        }
    }

    /// <summary>
    ///     Scans the <c>/CharStrings</c> region (from just after the <c>/CharStrings</c> keyword
    ///     through the end of the decrypted plaintext) for <c>/&lt;name&gt; &lt;length&gt;
    ///     &lt;token&gt; &lt;raw bytes&gt;</c> entries, agnostic to the literal spelling of
    ///     <c>&lt;token&gt;</c>.
    /// </summary>
    private static void ScanCharStrings(string text, int start, int end, List<(string Name, int Offset, int Length)> result)
    {
        var pos = start;
        while (pos < end)
        {
            if (!NextToken(text, ref pos, end, out var tokenStart, out var tokenEnd))
            {
                break;
            }

            if (text[tokenStart] != '/')
            {
                continue; // not a glyph-name token - skip unrelated tokens between entries
            }

            var name = text[(tokenStart + 1)..tokenEnd];
            if (name.Length == 0)
            {
                throw new InvalidDataException("Type 1 font program's '/CharStrings' dictionary contains an empty glyph name.");
            }

            var length = ReadLengthToken(text, ref pos, end, "'/CharStrings'");
            SkipProcNameToken(text, ref pos, end, "'/CharStrings'");

            if (pos >= end || !IsWhitespace(text[pos]))
            {
                throw new InvalidDataException(
                    "Type 1 font program's '/CharStrings' entry is missing its single separator byte before the raw data.");
            }

            pos++;

            if (checked(pos + length) > end)
            {
                throw new InvalidDataException("Type 1 font program's '/CharStrings' raw charstring data is truncated.");
            }

            result.Add((name, pos, length));
            pos += length;
        }
    }

    private static int ReadLengthToken(string text, ref int pos, int end, string entryKind)
    {
        if (!NextToken(text, ref pos, end, out var lenStart, out var lenEnd))
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"Type 1 font program's {entryKind} entry is truncated."));
        }

        if (!int.TryParse(text.AsSpan(lenStart, lenEnd - lenStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out var length)
            || length < 0)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"Type 1 font program's {entryKind} entry has a malformed length."));
        }

        return length;
    }

    private static void SkipProcNameToken(string text, ref int pos, int end, string entryKind)
    {
        if (!NextToken(text, ref pos, end, out _, out _))
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"Type 1 font program's {entryKind} entry is truncated."));
        }
    }

    /// <summary>
    ///     Skips leading Type 1/PostScript whitespace, then returns the next contiguous run of
    ///     non-whitespace characters as a token.
    /// </summary>
    private static bool NextToken(string text, ref int pos, int end, out int tokenStart, out int tokenEnd)
    {
        while (pos < end && IsWhitespace(text[pos]))
        {
            pos++;
        }

        if (pos >= end)
        {
            tokenStart = pos;
            tokenEnd = pos;
            return false;
        }

        tokenStart = pos;
        while (pos < end && !IsWhitespace(text[pos]))
        {
            pos++;
        }

        tokenEnd = pos;
        return true;
    }

    /// <summary>
    ///     Classifies a byte (reinterpreted as a Latin-1 character) as PostScript whitespace:
    ///     space, tab, carriage return, line feed, form feed, or NUL.
    /// </summary>
    private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n' or '\f' or '\0';

    /// <summary>
    ///     Peeks a single glyph's charstring for only its leading <c>hsbw</c>/<c>sbw</c> operator
    ///     to resolve its advance width, without running the full charstring interpreter.
    /// </summary>
    private static int PeekAdvanceWidth(byte[] data, (int Offset, int Length) charstring)
    {
        var pos = charstring.Offset;
        var end = charstring.Offset + charstring.Length;
        var stack = new List<double>();
        while (pos < end)
        {
            var b0 = data[pos];
            if (b0 >= 32)
            {
                pos = Type1CharstringInterpreter.ReadNumber(data, pos, end, stack);
                continue;
            }

            pos++;
            if (b0 == 13) // hsbw
            {
                if (stack.Count != 2)
                {
                    throw new InvalidDataException("Type 1 charstring 'hsbw' requires exactly two operands.");
                }

                return (int)Math.Round(stack[1], MidpointRounding.AwayFromZero);
            }

            if (b0 == 12)
            {
                if (pos >= end)
                {
                    throw new InvalidDataException("Type 1 charstring escape operator is truncated.");
                }

                var escOp = data[pos];
                if (escOp == 7) // sbw
                {
                    if (stack.Count != 4)
                    {
                        throw new InvalidDataException("Type 1 charstring 'sbw' requires exactly four operands.");
                    }

                    return (int)Math.Round(stack[2], MidpointRounding.AwayFromZero);
                }
            }

            throw new InvalidDataException("Type 1 charstring does not begin with 'hsbw'/'sbw'.");
        }

        throw new InvalidDataException("Type 1 charstring is truncated before 'hsbw'/'sbw'.");
    }
}
