// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a font's <c>cmap</c> table and resolves Unicode codepoints to glyph indices, using
///     only format 4 (Unicode BMP) and format 12 (Unicode full repertoire) subtables.
/// </summary>
/// <remarks>
///     <para>
///     Subtable selection is priority-ordered, first match wins: <c>(3,10)</c> format 12,
///     <c>(0,4)</c>/<c>(0,6)</c> format 12, <c>(3,1)</c> format 4, <c>(0,3)</c> format 4, then any
///     remaining <c>(0,x)</c> format 4. Format 0/2/6, symbol encoding <c>(3,0)</c>, and any
///     non-Unicode platform/encoding pair are never selected. If no supported subtable is present,
///     or the <c>cmap</c> table itself is missing, malformed, or truncated, <see cref="GetGlyphIndex"/>
///     always returns <c>0</c> (<c>.notdef</c>) - this class never throws.
///     </para>
/// </remarks>
internal sealed class CmapTable
{
    /// <summary>
    ///     A <see cref="CmapTable"/> with no usable subtable: <see cref="GetGlyphIndex"/> always
    ///     returns <c>0</c>. Used when the font has no <c>cmap</c> table at all.
    /// </summary>
    public static readonly CmapTable Empty = new(null);

    /// <summary>
    ///     The selected subtable's lookup delegate, or <see langword="null"/> if no supported
    ///     subtable was found.
    /// </summary>
    private readonly Func<int, int>? _lookup;

    private CmapTable(Func<int, int>? lookup)
    {
        _lookup = lookup;
    }

    /// <summary>
    ///     Parses a <c>cmap</c> table and selects the highest-priority supported subtable.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="tableOffset">The offset of the <c>cmap</c> table within <paramref name="data"/>.</param>
    /// <param name="tableLength">The length of the <c>cmap</c> table.</param>
    /// <returns>
    ///     A <see cref="CmapTable"/> wrapping the selected subtable, or <see cref="Empty"/> if the
    ///     table is too short to contain a header, or no candidate subtable can be parsed
    ///     successfully.
    /// </returns>
    public static CmapTable Parse(byte[] data, int tableOffset, int tableLength)
    {
        if (tableLength < 4)
        {
            return Empty;
        }

        var numTables = SfntContainer.ReadUInt16(data, tableOffset + 2);
        var candidates = new List<(int Priority, int SubtableOffset, int Format)>();

        var recordsEnd = checked((long)8 + (long)numTables * 8);
        if (recordsEnd > tableLength)
        {
            return Empty;
        }

        for (var i = 0; i < numTables; i++)
        {
            var recordOffset = tableOffset + 4 + i * 8;
            var platformId = SfntContainer.ReadUInt16(data, recordOffset);
            var encodingId = SfntContainer.ReadUInt16(data, recordOffset + 2);
            var subtableOffset = SfntContainer.ReadInt32(data, recordOffset + 4);

            if (subtableOffset < 0 || (long)subtableOffset + 2 > tableLength)
            {
                continue;
            }

            var absoluteSubtableOffset = tableOffset + subtableOffset;
            var format = SfntContainer.ReadUInt16(data, absoluteSubtableOffset);

            var priority = ClassifyPriority(platformId, encodingId, format);
            if (priority >= 0)
            {
                candidates.Add((priority, absoluteSubtableOffset, format));
            }
        }

        candidates.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (var candidate in candidates)
        {
            var lookup = candidate.Format == 12
                ? TryParseFormat12(data, candidate.SubtableOffset, tableOffset + tableLength)
                : TryParseFormat4(data, candidate.SubtableOffset, tableOffset + tableLength);

            if (lookup != null)
            {
                return new CmapTable(lookup);
            }
        }

        return Empty;
    }

    /// <summary>
    ///     Looks up the glyph index mapped to a Unicode codepoint.
    /// </summary>
    /// <param name="codepoint">The Unicode codepoint to look up.</param>
    /// <returns>
    ///     The mapped glyph index, or <c>0</c> (<c>.notdef</c>) if the codepoint is unmapped, or
    ///     no supported subtable is available.
    /// </returns>
    public int GetGlyphIndex(int codepoint) => _lookup?.Invoke(codepoint) ?? 0;

    /// <summary>
    ///     Classifies a subtable's selection priority (lower is preferred), or <c>-1</c> if the
    ///     platform/encoding/format combination is not recognized as a supported subtable.
    /// </summary>
    private static int ClassifyPriority(int platformId, int encodingId, int format)
    {
        if (platformId == 3 && encodingId == 10 && format == 12)
        {
            return 0;
        }

        if (platformId == 0 && (encodingId == 4 || encodingId == 6) && format == 12)
        {
            return 1;
        }

        if (platformId == 3 && encodingId == 1 && format == 4)
        {
            return 2;
        }

        if (platformId == 0 && encodingId == 3 && format == 4)
        {
            return 3;
        }

        if (platformId == 0 && format == 4)
        {
            return 4;
        }

        return -1;
    }

    /// <summary>
    ///     Attempts to parse a format 4 (segment mapping to delta values) subtable, returning a
    ///     bound lookup delegate, or <see langword="null"/> if the subtable is malformed/truncated.
    /// </summary>
    private static Func<int, int>? TryParseFormat4(byte[] data, int offset, int tableEnd)
    {
        if ((long)offset + 14 > tableEnd)
        {
            return null;
        }

        var segCountX2 = SfntContainer.ReadUInt16(data, offset + 6);
        var segCount = segCountX2 / 2;
        if (segCount == 0)
        {
            return null;
        }

        var endCodeOffset = offset + 14;
        var startCodeOffset = endCodeOffset + segCountX2 + 2; // +2 skips reservedPad
        var idDeltaOffset = startCodeOffset + segCountX2;
        var idRangeOffsetOffset = idDeltaOffset + segCountX2;
        var glyphIdArrayOffset = idRangeOffsetOffset + segCountX2;

        if (glyphIdArrayOffset > tableEnd)
        {
            return null;
        }

        var endCodes = new int[segCount];
        var startCodes = new int[segCount];
        var idDeltas = new short[segCount];
        var idRangeOffsets = new ushort[segCount];
        for (var i = 0; i < segCount; i++)
        {
            endCodes[i] = SfntContainer.ReadUInt16(data, endCodeOffset + i * 2);
            startCodes[i] = SfntContainer.ReadUInt16(data, startCodeOffset + i * 2);
            idDeltas[i] = SfntContainer.ReadInt16(data, idDeltaOffset + i * 2);
            idRangeOffsets[i] = SfntContainer.ReadUInt16(data, idRangeOffsetOffset + i * 2);
        }

        return codepoint =>
        {
            if (codepoint is < 0 or > 0xFFFF)
            {
                return 0;
            }

            for (var i = 0; i < segCount; i++)
            {
                if (codepoint > endCodes[i])
                {
                    continue;
                }

                if (codepoint < startCodes[i])
                {
                    return 0;
                }

                if (idRangeOffsets[i] == 0)
                {
                    return (codepoint + idDeltas[i]) & 0xFFFF;
                }

                var glyphIndexAddress = idRangeOffsetOffset + i * 2 + idRangeOffsets[i] + 2 * (codepoint - startCodes[i]);
                if (glyphIndexAddress + 2 > tableEnd)
                {
                    return 0;
                }

                var glyphId = SfntContainer.ReadUInt16(data, glyphIndexAddress);
                return glyphId == 0 ? 0 : (glyphId + idDeltas[i]) & 0xFFFF;
            }

            return 0;
        };
    }

    /// <summary>
    ///     Attempts to parse a format 12 (segmented coverage) subtable, returning a bound lookup
    ///     delegate, or <see langword="null"/> if the subtable is malformed/truncated.
    /// </summary>
    private static Func<int, int>? TryParseFormat12(byte[] data, int offset, int tableEnd)
    {
        if ((long)offset + 16 > tableEnd)
        {
            return null;
        }

        var numGroups = SfntContainer.ReadUInt32(data, offset + 12);
        var groupsEnd = checked((long)offset + 16 + (long)numGroups * 12);
        if (groupsEnd > tableEnd)
        {
            return null;
        }

        var starts = new uint[numGroups];
        var ends = new uint[numGroups];
        var startGlyphIds = new uint[numGroups];
        for (var i = 0; i < numGroups; i++)
        {
            var groupOffset = offset + 16 + i * 12;
            starts[i] = SfntContainer.ReadUInt32(data, groupOffset);
            ends[i] = SfntContainer.ReadUInt32(data, groupOffset + 4);
            startGlyphIds[i] = SfntContainer.ReadUInt32(data, groupOffset + 8);
        }

        return codepoint =>
        {
            if (codepoint < 0)
            {
                return 0;
            }

            var code = (uint)codepoint;
            var lo = 0;
            var hi = starts.Length - 1;
            while (lo <= hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (code < starts[mid])
                {
                    hi = mid - 1;
                }
                else if (code > ends[mid])
                {
                    lo = mid + 1;
                }
                else
                {
                    return (int)(startGlyphIds[mid] + (code - starts[mid]));
                }
            }

            return 0;
        };
    }
}
