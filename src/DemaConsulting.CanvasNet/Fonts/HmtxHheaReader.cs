// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a font's <c>hhea</c> (horizontal header) and <c>hmtx</c> (horizontal metrics)
///     tables, and resolves per-glyph advance widths.
/// </summary>
/// <remarks>
///     <c>hmtx</c>'s array holds <c>numOfLongHorMetrics</c> (from <c>hhea</c>)
///     <c>(advanceWidth, leftSideBearing)</c> pairs, followed by
///     <c>(numGlyphs - numOfLongHorMetrics)</c> left-side-bearing-only entries; a glyph index at
///     or beyond <c>numOfLongHorMetrics</c> reuses the <em>last</em> explicit <c>advanceWidth</c>
///     entry, per the OpenType <c>hmtx</c> "monospaced tail" convention.
/// </remarks>
internal sealed class HmtxHheaReader
{
    /// <summary>
    ///     The fixed size, in bytes, of the <c>hhea</c> table.
    /// </summary>
    private const int HheaSize = 36;

    /// <summary>
    ///     The advance widths of every explicit <c>hmtx</c> entry (length <c>numOfLongHorMetrics</c>).
    ///     A glyph index at or beyond this array's length reuses the last entry.
    /// </summary>
    private readonly int[] _advanceWidths;

    /// <summary>
    ///     The typographic ascender, from <c>hhea.ascender</c>.
    /// </summary>
    public int Ascender { get; }

    /// <summary>
    ///     The typographic descender, from <c>hhea.descender</c>.
    /// </summary>
    public int Descender { get; }

    /// <summary>
    ///     The typographic line gap, from <c>hhea.lineGap</c>.
    /// </summary>
    public int LineGap { get; }

    private HmtxHheaReader(int ascender, int descender, int lineGap, int[] advanceWidths)
    {
        Ascender = ascender;
        Descender = descender;
        LineGap = lineGap;
        _advanceWidths = advanceWidths;
    }

    /// <summary>
    ///     Parses the <c>hhea</c> and <c>hmtx</c> tables.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="hheaOffset">The offset of the <c>hhea</c> table.</param>
    /// <param name="hheaLength">The length of the <c>hhea</c> table.</param>
    /// <param name="hmtxOffset">The offset of the <c>hmtx</c> table.</param>
    /// <param name="hmtxLength">The length of the <c>hmtx</c> table.</param>
    /// <param name="numGlyphs">The font's total glyph count, from <c>maxp</c>.</param>
    /// <returns>A new <see cref="HmtxHheaReader"/> exposing the parsed metrics.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>hhea</c> is too short, when <c>numOfLongHorMetrics</c> is not in the
    ///     range <c>(0, numGlyphs]</c>, or when <c>hmtx</c> is too short to contain the declared
    ///     number of entries.
    /// </exception>
    public static HmtxHheaReader Parse(byte[] data, int hheaOffset, int hheaLength, int hmtxOffset, int hmtxLength, int numGlyphs)
    {
        if (hheaLength < HheaSize)
        {
            throw new InvalidDataException("The 'hhea' table is too short.");
        }

        var ascender = SfntContainer.ReadInt16(data, hheaOffset + 4);
        var descender = SfntContainer.ReadInt16(data, hheaOffset + 6);
        var lineGap = SfntContainer.ReadInt16(data, hheaOffset + 8);
        var numOfLongHorMetrics = SfntContainer.ReadUInt16(data, hheaOffset + 34);

        if (numOfLongHorMetrics == 0 || numOfLongHorMetrics > numGlyphs)
        {
            throw new InvalidDataException(
                $"'hhea.numOfLongHorMetrics' ({numOfLongHorMetrics}) must be in the range (0, {numGlyphs}].");
        }

        var tailCount = numGlyphs - numOfLongHorMetrics;
        var requiredBytes = checked((long)numOfLongHorMetrics * 4 + (long)tailCount * 2);
        if (requiredBytes > hmtxLength)
        {
            throw new InvalidDataException("The 'hmtx' table is too short for its declared metric counts.");
        }

        var advanceWidths = new int[numOfLongHorMetrics];
        for (var i = 0; i < numOfLongHorMetrics; i++)
        {
            advanceWidths[i] = SfntContainer.ReadUInt16(data, hmtxOffset + i * 4);
        }

        return new HmtxHheaReader(ascender, descender, lineGap, advanceWidths);
    }

    /// <summary>
    ///     Looks up a glyph's advance width.
    /// </summary>
    /// <param name="glyphIndex">
    ///     The glyph index to look up. Assumed to already be validated as within
    ///     <c>[0, numGlyphs)</c> by the caller.
    /// </param>
    /// <returns>
    ///     The glyph's advance width, reusing the last explicit <c>hmtx</c> entry for any glyph
    ///     index at or beyond <c>numOfLongHorMetrics</c>.
    /// </returns>
    public int GetAdvanceWidth(int glyphIndex) =>
        glyphIndex < _advanceWidths.Length ? _advanceWidths[glyphIndex] : _advanceWidths[^1];
}
