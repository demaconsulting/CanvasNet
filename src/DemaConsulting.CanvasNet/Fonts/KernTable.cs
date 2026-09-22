// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a font's <c>kern</c> table and resolves pairwise horizontal kerning adjustments,
///     using only subtable format 0 (ordered pair list).
/// </summary>
/// <remarks>
///     Only the first subtable that is format 0 and carries horizontal-only coverage (coverage
///     bit 0 set, bits 1/2 clear) is used; every other subtable is skipped. A <c>kern</c> table
///     that is absent, empty, uses only unsupported subtable formats/coverage bits, or is
///     internally malformed (a declared pair count that does not fit the table's bounds) is
///     treated as "no kerning data" - this class never throws; <see cref="GetKerning"/> always
///     returns <c>0</c> in that case.
/// </remarks>
internal sealed class KernTable
{
    /// <summary>
    ///     A <see cref="KernTable"/> with no usable pairs: <see cref="GetKerning"/> always returns
    ///     <c>0</c>. Used when the font has no <c>kern</c> table, or none of its subtables qualify.
    /// </summary>
    public static readonly KernTable Empty = new([]);

    /// <summary>
    ///     The selected subtable's pairs, sorted ascending by <c>(Left, Right)</c> for binary search.
    /// </summary>
    private readonly (ushort Left, ushort Right, short Value)[] _pairs;

    private KernTable((ushort, ushort, short)[] pairs)
    {
        _pairs = pairs;
    }

    /// <summary>
    ///     Parses a <c>kern</c> table and selects the first qualifying format-0, horizontal-only
    ///     subtable.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="tableOffset">The offset of the <c>kern</c> table within <paramref name="data"/>.</param>
    /// <param name="tableLength">The length of the <c>kern</c> table.</param>
    /// <returns>
    ///     A <see cref="KernTable"/> wrapping the selected subtable's pairs, or <see cref="Empty"/>
    ///     if the table is malformed, or no qualifying subtable is found.
    /// </returns>
    public static KernTable Parse(byte[] data, int tableOffset, int tableLength)
    {
        if (tableLength < 4)
        {
            return Empty;
        }

        var version = SfntContainer.ReadUInt16(data, tableOffset);
        if (version != 0)
        {
            // Only the classic ('version 0 header') kern table layout is understood; the
            // OpenType 'Apple' version-1 header uses an incompatible layout and is skipped
            // entirely rather than being misinterpreted.
            return Empty;
        }

        var numSubtables = SfntContainer.ReadUInt16(data, tableOffset + 2);
        var pos = tableOffset + 4;
        var tableEnd = tableOffset + tableLength;

        for (var i = 0; i < numSubtables; i++)
        {
            if (pos + 6 > tableEnd)
            {
                return Empty;
            }

            var subtableLength = SfntContainer.ReadUInt16(data, pos + 2);
            var coverage = SfntContainer.ReadUInt16(data, pos + 4);
            var subtableEnd = pos + subtableLength;
            if (subtableLength < 6 || subtableEnd > tableEnd)
            {
                return Empty;
            }

            var format = (coverage >> 8) & 0xFF;
            var horizontal = (coverage & 0x1) != 0;
            var minimum = (coverage & 0x2) != 0;
            var crossStream = (coverage & 0x4) != 0;

            if (format == 0 && horizontal && !minimum && !crossStream)
            {
                var pairs = TryParseFormat0(data, pos + 6, subtableEnd);
                if (pairs != null)
                {
                    return new KernTable(pairs);
                }

                return Empty;
            }

            pos = subtableEnd;
        }

        return Empty;
    }

    /// <summary>
    ///     Looks up the kerning adjustment for a glyph pair.
    /// </summary>
    /// <param name="leftGlyphIndex">The left glyph index of the pair.</param>
    /// <param name="rightGlyphIndex">The right glyph index of the pair.</param>
    /// <returns>
    ///     The kerning adjustment for the pair, or <c>0</c> if no matching pair is present (or no
    ///     subtable was available at all).
    /// </returns>
    public int GetKerning(int leftGlyphIndex, int rightGlyphIndex)
    {
        if (leftGlyphIndex is < 0 or > ushort.MaxValue || rightGlyphIndex is < 0 or > ushort.MaxValue)
        {
            return 0;
        }

        var left = (ushort)leftGlyphIndex;
        var right = (ushort)rightGlyphIndex;

        var lo = 0;
        var hi = _pairs.Length - 1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            var pair = _pairs[mid];
            var comparison = pair.Left != left ? pair.Left.CompareTo(left) : pair.Right.CompareTo(right);
            if (comparison == 0)
            {
                return pair.Value;
            }

            if (comparison < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Attempts to parse a format 0 subtable body (immediately following the shared 6-byte
    ///     subtable header), returning its pairs sorted for binary search, or <see langword="null"/>
    ///     if the declared pair count does not fit the subtable's bounds.
    /// </summary>
    private static (ushort, ushort, short)[]? TryParseFormat0(byte[] data, int bodyOffset, int subtableEnd)
    {
        if (bodyOffset + 8 > subtableEnd)
        {
            return null;
        }

        var nPairs = SfntContainer.ReadUInt16(data, bodyOffset);
        var pairsOffset = bodyOffset + 8;
        var requiredBytes = checked((long)nPairs * 6);
        if (pairsOffset + requiredBytes > subtableEnd)
        {
            return null;
        }

        var pairs = new (ushort, ushort, short)[nPairs];
        for (var i = 0; i < nPairs; i++)
        {
            var entryOffset = pairsOffset + i * 6;
            var left = SfntContainer.ReadUInt16(data, entryOffset);
            var right = SfntContainer.ReadUInt16(data, entryOffset + 2);
            var value = SfntContainer.ReadInt16(data, entryOffset + 4);
            pairs[i] = (left, right, value);
        }

        Array.Sort(pairs, static (a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2));
        return pairs;
    }
}
