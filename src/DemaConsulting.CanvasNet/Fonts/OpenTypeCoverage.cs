namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses an OpenType Coverage table (format 1 or 2) and maps a glyph index to its coverage
///     index - the position of the glyph's entry in the parallel record array of the table that
///     owns the coverage.
/// </summary>
/// <remarks>
///     <para>
///     Every OpenType layout table that keys per-glyph data by glyph (the <c>MATH</c> table's
///     italic corrections, top-accent attachments, extended shapes, kerns, and constructions, and
///     later the glyph substitution and positioning tables) uses the same two Coverage formats, so
///     the reader is its own helper instead of being repeated inside each table reader. Format 1
///     lists the covered glyphs one by one (the coverage index is the array position); format 2
///     lists ranges of consecutive glyphs, each with the coverage index of its first glyph.
///     </para>
///     <para>
///     Both formats are normalized to a range list sorted by first glyph, so a lookup is a binary
///     search whichever format the font uses. The specification requires sorted entries; an
///     unsorted table is sorted here rather than rejected, so a lookup never depends on the order
///     the font happened to store. Instances are immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class OpenTypeCoverage
{
    /// <summary>
    ///     The size in bytes of the Coverage header (<c>coverageFormat</c> and the glyph or range
    ///     count).
    /// </summary>
    private const int HeaderSize = 4;

    /// <summary>The size in bytes of one format-1 glyph entry (a glyph ID).</summary>
    private const int GlyphRecordSize = 2;

    /// <summary>
    ///     The size in bytes of one format-2 range record (<c>startGlyphID</c>, <c>endGlyphID</c>,
    ///     <c>startCoverageIndex</c>).
    /// </summary>
    private const int RangeRecordSize = 6;

    /// <summary>
    ///     A coverage that covers no glyph: <see cref="IndexOf"/> always returns <c>-1</c> and
    ///     <see cref="IndexCount"/> is <c>0</c>. Used for a coverage the owning table omits.
    /// </summary>
    public static readonly OpenTypeCoverage Empty = new([]);

    /// <summary>
    ///     The covered glyph ranges sorted ascending by <see cref="CoverageRange.Start"/>, so that
    ///     <see cref="IndexOf"/> can binary-search them.
    /// </summary>
    private readonly CoverageRange[] _ranges;

    /// <summary>
    ///     Initializes a coverage from its already-sorted ranges and computes
    ///     <see cref="IndexCount"/> from them.
    /// </summary>
    /// <param name="ranges">The ranges, sorted ascending by first glyph.</param>
    private OpenTypeCoverage(CoverageRange[] ranges)
    {
        _ranges = ranges;

        // The owning table sizes its parallel record array by its own count field, so it needs
        // the largest index this coverage can return to check that the two agree
        var indexCount = 0;
        foreach (var range in ranges)
        {
            indexCount = Math.Max(indexCount, range.StartIndex + (range.End - range.Start) + 1);
        }

        IndexCount = indexCount;
    }

    /// <summary>
    ///     One more than the largest coverage index this coverage can return (<c>0</c> when it
    ///     covers no glyph). An owning table whose record count is smaller than this value is
    ///     malformed, because a covered glyph would index past its records.
    /// </summary>
    public int IndexCount { get; }

    /// <summary>
    ///     Parses a Coverage table of format 1 or 2.
    /// </summary>
    /// <remarks>
    ///     All position arithmetic is widened to <see langword="long"/> before it is compared with
    ///     <paramref name="end"/>, so a maximal count cannot wrap a 32-bit sum past the bounds
    ///     check. Reads only; no state is shared between calls.
    /// </remarks>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="offset">The absolute position of the Coverage table within <paramref name="data"/>.</param>
    /// <param name="end">
    ///     The exclusive absolute end of the enclosing table; the Coverage table must lie entirely
    ///     before it. Must not exceed <c>data.Length</c>.
    /// </param>
    /// <returns>The parsed coverage.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the table does not fit before <paramref name="end"/>, its format is neither 1
    ///     nor 2, or a format-2 range ends before it starts.
    /// </exception>
    public static OpenTypeCoverage Parse(byte[] data, int offset, int end)
    {
        // Read the header, which must fit before anything else is read
        if (offset < 0 || (long)offset + HeaderSize > end)
        {
            throw new InvalidDataException("An OpenType Coverage table lies outside its enclosing table.");
        }

        var format = SfntContainer.ReadUInt16(data, offset);
        var count = SfntContainer.ReadUInt16(data, offset + 2);
        var recordsStart = offset + HeaderSize;

        // Decode the records of the declared format into ranges
        var ranges = format switch
        {
            1 => ReadFormat1(data, recordsStart, count, end),
            2 => ReadFormat2(data, recordsStart, count, end),
            _ => throw new InvalidDataException($"OpenType Coverage format {format} is not supported (only 1 and 2 are defined).")
        };

        // Sort by first glyph so lookups can binary-search regardless of the stored order
        Array.Sort(ranges, static (a, b) => a.Start.CompareTo(b.Start));
        return new OpenTypeCoverage(ranges);
    }

    /// <summary>
    ///     Maps a glyph index to its coverage index.
    /// </summary>
    /// <param name="glyph">The glyph index to look up. Any value is accepted.</param>
    /// <returns>
    ///     The coverage index of <paramref name="glyph"/> (in <c>[0, IndexCount)</c>), or <c>-1</c>
    ///     when the glyph is not covered (including any value outside the 16-bit glyph ID range).
    ///     Never throws.
    /// </returns>
    public int IndexOf(int glyph)
    {
        if (glyph is < 0 or > ushort.MaxValue)
        {
            return -1;
        }

        // Find the last range whose first glyph is at or before the requested glyph
        var lo = 0;
        var hi = _ranges.Length - 1;
        var candidate = -1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (_ranges[mid].Start <= glyph)
            {
                candidate = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // The glyph is covered only when that range also extends to it
        if (candidate < 0 || glyph > _ranges[candidate].End)
        {
            return -1;
        }

        var range = _ranges[candidate];
        return range.StartIndex + (glyph - range.Start);
    }

    /// <summary>
    ///     Reads a format-1 glyph array as single-glyph ranges whose coverage index is the array
    ///     position.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="recordsStart">The absolute position of the first glyph ID.</param>
    /// <param name="count">The declared glyph count.</param>
    /// <param name="end">The exclusive absolute end of the enclosing table.</param>
    /// <returns>One range per listed glyph.</returns>
    /// <exception cref="InvalidDataException">Thrown when the glyph array does not fit before <paramref name="end"/>.</exception>
    private static CoverageRange[] ReadFormat1(byte[] data, int recordsStart, int count, int end)
    {
        RequireRecords(recordsStart, count, GlyphRecordSize, end);

        var ranges = new CoverageRange[count];
        for (var i = 0; i < count; i++)
        {
            var glyph = SfntContainer.ReadUInt16(data, recordsStart + i * GlyphRecordSize);
            ranges[i] = new CoverageRange(glyph, glyph, i);
        }

        return ranges;
    }

    /// <summary>
    ///     Reads a format-2 range array.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="recordsStart">The absolute position of the first range record.</param>
    /// <param name="count">The declared range count.</param>
    /// <param name="end">The exclusive absolute end of the enclosing table.</param>
    /// <returns>The ranges, in stored order.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the range array does not fit before <paramref name="end"/>, or a range ends
    ///     before it starts.
    /// </exception>
    private static CoverageRange[] ReadFormat2(byte[] data, int recordsStart, int count, int end)
    {
        RequireRecords(recordsStart, count, RangeRecordSize, end);

        var ranges = new CoverageRange[count];
        for (var i = 0; i < count; i++)
        {
            var position = recordsStart + i * RangeRecordSize;
            var start = SfntContainer.ReadUInt16(data, position);
            var last = SfntContainer.ReadUInt16(data, position + 2);
            var startIndex = SfntContainer.ReadUInt16(data, position + 4);
            if (last < start)
            {
                throw new InvalidDataException(
                    $"An OpenType Coverage range ends (glyph {last}) before it starts (glyph {start}).");
            }

            ranges[i] = new CoverageRange(start, last, startIndex);
        }

        return ranges;
    }

    /// <summary>
    ///     Checks that <paramref name="count"/> records of <paramref name="recordSize"/> bytes
    ///     starting at <paramref name="recordsStart"/> fit before <paramref name="end"/>, in
    ///     overflow-safe <see langword="long"/> arithmetic.
    /// </summary>
    /// <param name="recordsStart">The absolute position of the first record.</param>
    /// <param name="count">The declared record count.</param>
    /// <param name="recordSize">The size in bytes of one record.</param>
    /// <param name="end">The exclusive absolute end of the enclosing table.</param>
    /// <exception cref="InvalidDataException">Thrown when the records do not fit.</exception>
    private static void RequireRecords(int recordsStart, int count, int recordSize, int end)
    {
        if (recordsStart + (long)count * recordSize > end)
        {
            throw new InvalidDataException(
                $"An OpenType Coverage table declares {count} records, more than fit in its enclosing table.");
        }
    }

    /// <summary>
    ///     One run of consecutive covered glyphs.
    /// </summary>
    /// <param name="Start">The first covered glyph of the run.</param>
    /// <param name="End">The last covered glyph of the run (inclusive, never before <paramref name="Start"/>).</param>
    /// <param name="StartIndex">The coverage index of <paramref name="Start"/>.</param>
    private readonly record struct CoverageRange(int Start, int End, int StartIndex);
}
