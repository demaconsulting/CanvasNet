// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Provides hand-rolled, dependency-free loading and querying of TrueType (<c>glyf</c>-based)
///     SFNT font files: metrics, character-to-glyph mapping, glyph outline extraction, advance
///     widths, and basic pairwise kerning.
/// </summary>
/// <remarks>
///     <para>
///     <c>TrueTypeFont</c> is the sole public unit of the <see cref="Fonts"/> namespace; it fronts
///     several internal, independently-parsed helpers (<see cref="SfntContainer"/>,
///     <see cref="CmapTable"/>, <see cref="GlyfLocaReader"/>, <see cref="HmtxHheaReader"/>,
///     <see cref="KernTable"/>) exactly as <see cref="Codecs.PngCodec"/> internally parses chunks
///     as one unit, and as <see cref="Drawing.PathFiller"/> fronts <see cref="Drawing.EdgeFlattener"/>/
///     <see cref="Drawing.ScanlineRasterizer"/>.
///     </para>
///     <para>
///     <c>head</c>, <c>maxp</c> (version <c>1.0</c> only - version <c>0.5</c> is CFF-oriented and
///     is rejected alongside CFF/OTTO outlines), <c>hhea</c>, <c>hmtx</c>, <c>loca</c>, and
///     <c>glyf</c> are required tables: a missing or structurally malformed one causes
///     <see cref="Load(Stream)"/>/<see cref="Load(string)"/> to throw
///     <see cref="InvalidDataException"/>. <c>cmap</c> and <c>kern</c> are optional: many
///     legitimately embedded/subsetted fonts omit <c>cmap</c> entirely, and most fonts have no
///     <c>kern</c> table at all. Absence of <c>cmap</c> means <see cref="GetGlyphIndex"/> always
///     returns <c>0</c> (<c>.notdef</c>); absence of <c>kern</c> means <see cref="GetKerning"/>
///     always returns <c>0</c>.
///     </para>
///     <para>
///     SFNT table checksums (including <c>head.checkSumAdjustment</c>) are not enforced as a
///     load-rejection condition: real-world TrueType fonts commonly carry stale or incorrect
///     checksums after ordinary subsetting/hinting/editing workflows. Structural validation
///     (bounds, overflow, required tables, internal glyph/cmap/kern consistency) protects against
///     malformed input instead.
///     </para>
///     <para>
///     <see cref="GetGlyphOutline"/> returns a <see cref="Path"/> in raw font design units
///     (y-axis increasing upward, per TrueType/OpenType convention) - it is <em>not</em> scaled to
///     a point size and <em>not</em> flipped to a y-down pixel convention. <see cref="UnitsPerEm"/>
///     is exposed so callers combine it with their own point size and orientation.
///     </para>
/// </remarks>
public sealed class TrueTypeFont
{
    private readonly HmtxHheaReader _metrics;
    private readonly GlyfLocaReader _glyphs;
    private readonly CmapTable _cmap;
    private readonly KernTable _kern;

    /// <summary>
    ///     The number of font design units per em, from <c>head.unitsPerEm</c>.
    /// </summary>
    public int UnitsPerEm { get; }

    /// <summary>
    ///     The typographic ascender, in font design units, from <c>hhea.ascender</c>.
    /// </summary>
    public int Ascender { get; }

    /// <summary>
    ///     The typographic descender, in font design units, from <c>hhea.descender</c>.
    /// </summary>
    public int Descender { get; }

    /// <summary>
    ///     The typographic line gap, in font design units, from <c>hhea.lineGap</c>.
    /// </summary>
    public int LineGap { get; }

    /// <summary>
    ///     The total number of glyphs in the font, from <c>maxp.numGlyphs</c>.
    /// </summary>
    public int GlyphCount { get; }

    private TrueTypeFont(int unitsPerEm, int glyphCount, HmtxHheaReader metrics, GlyfLocaReader glyphs, CmapTable cmap, KernTable kern)
    {
        UnitsPerEm = unitsPerEm;
        GlyphCount = glyphCount;
        Ascender = metrics.Ascender;
        Descender = metrics.Descender;
        LineGap = metrics.LineGap;
        _metrics = metrics;
        _glyphs = glyphs;
        _cmap = cmap;
        _kern = kern;
    }

    /// <summary>
    ///     Loads a <see cref="TrueTypeFont"/> from an open, readable stream containing a TrueType
    ///     (<c>glyf</c>-based) SFNT font file.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the font from. Reading begins at the stream's current position and
    ///     consumes the remainder of the stream.
    /// </param>
    /// <returns>A new <see cref="TrueTypeFont"/> ready to be queried.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="stream"/> does not contain a valid, supported TrueType
    ///     font: the SFNT version is not recognized, the font declares CFF/OpenType (<c>OTTO</c>)
    ///     outlines, a required table (<c>head</c>, <c>maxp</c> version <c>1.0</c>, <c>hhea</c>,
    ///     <c>hmtx</c>, <c>loca</c>, <c>glyf</c>) is missing or structurally malformed, a table
    ///     directory entry's offset/length is out of bounds or would overflow, or the stream is
    ///     truncated.
    /// </exception>
    /// <example>
    ///     <code>
    ///     using var stream = File.OpenRead("font.ttf");
    ///     var font = TrueTypeFont.Load(stream);
    ///     var glyphIndex = font.GetGlyphIndex('A');
    ///     var outline = font.GetGlyphOutline(glyphIndex); // in raw font design units
    ///     </code>
    /// </example>
    public static TrueTypeFont Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return LoadFromBytes(memory.ToArray());
    }

    /// <summary>
    ///     Loads a <see cref="TrueTypeFont"/> from a TrueType font file at the specified path.
    /// </summary>
    /// <param name="path">The path of the font file to load. Must not be null or empty.</param>
    /// <returns>A new <see cref="TrueTypeFont"/> ready to be queried.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="InvalidDataException">Thrown for the same conditions as <see cref="Load(Stream)"/>.</exception>
    /// <remarks>
    ///     File-system exceptions (for example <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, <see cref="UnauthorizedAccessException"/>, or
    ///     <see cref="IOException"/>) raised while opening <paramref name="path"/> propagate
    ///     uncaught to the caller.
    /// </remarks>
    public static TrueTypeFont Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Load(stream);
    }

    /// <summary>
    ///     Looks up the glyph index mapped to a Unicode codepoint via the font's <c>cmap</c> table.
    /// </summary>
    /// <param name="codepoint">The Unicode codepoint to look up.</param>
    /// <returns>
    ///     The mapped glyph index, or <c>0</c> (<c>.notdef</c>) if the codepoint is unmapped, or
    ///     the font has no <c>cmap</c> table, or no supported <c>cmap</c> subtable is present.
    ///     Never throws.
    /// </returns>
    public int GetGlyphIndex(int codepoint) => _cmap.GetGlyphIndex(codepoint);

    /// <summary>
    ///     Decodes a single glyph's outline.
    /// </summary>
    /// <param name="glyphIndex">The glyph index to decode, as returned by <see cref="GetGlyphIndex"/>.</param>
    /// <returns>
    ///     The glyph's outline, in raw font design units (see this class's remarks for the
    ///     coordinate-space contract), or <see cref="Path.Empty"/> for a glyph with no contour
    ///     data (for example <c>space</c>).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="glyphIndex"/> is outside <c>[0, GlyphCount)</c>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the glyph's contour data is malformed or truncated, when a composite
    ///     glyph's nesting depth (10) or total resolved component count (5000) exceeds its bound,
    ///     when a composite glyph contains a point-matched component, or when a composite glyph
    ///     references an out-of-range component glyph index.
    /// </exception>
    public Path GetGlyphOutline(int glyphIndex)
    {
        ValidateGlyphIndex(glyphIndex);
        return _glyphs.GetGlyphOutline(glyphIndex);
    }

    /// <summary>
    ///     Looks up a glyph's advance width.
    /// </summary>
    /// <param name="glyphIndex">The glyph index to look up.</param>
    /// <returns>
    ///     The glyph's advance width, in font design units, reusing the last explicit <c>hmtx</c>
    ///     entry for any glyph index at or beyond <c>hhea.numOfLongHorMetrics</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="glyphIndex"/> is outside <c>[0, GlyphCount)</c>.
    /// </exception>
    public int GetAdvanceWidth(int glyphIndex)
    {
        ValidateGlyphIndex(glyphIndex);
        return _metrics.GetAdvanceWidth(glyphIndex);
    }

    /// <summary>
    ///     Looks up the kerning adjustment for a glyph pair via the font's <c>kern</c> table.
    /// </summary>
    /// <param name="leftGlyphIndex">The left glyph index of the pair.</param>
    /// <param name="rightGlyphIndex">The right glyph index of the pair.</param>
    /// <returns>
    ///     The kerning adjustment, in font design units, or <c>0</c> if no matching pair is
    ///     present, the font has no <c>kern</c> table, or no qualifying <c>kern</c> subtable is
    ///     present. Never throws, even for an out-of-range glyph index.
    /// </returns>
    public int GetKerning(int leftGlyphIndex, int rightGlyphIndex) => _kern.GetKerning(leftGlyphIndex, rightGlyphIndex);

    /// <summary>
    ///     Validates a glyph index argument shared by <see cref="GetGlyphOutline"/> and
    ///     <see cref="GetAdvanceWidth"/>.
    /// </summary>
    private void ValidateGlyphIndex(int glyphIndex)
    {
        if (glyphIndex < 0 || glyphIndex >= GlyphCount)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphIndex), glyphIndex, "Glyph index is out of range.");
        }
    }

    /// <summary>
    ///     Parses the SFNT container, validates and parses every required table, and parses the
    ///     optional <c>cmap</c>/<c>kern</c> tables tolerantly.
    /// </summary>
    private static TrueTypeFont LoadFromBytes(byte[] data)
    {
        var container = SfntContainer.Parse(data);

        var head = container.RequireTable("head");
        var maxp = container.RequireTable("maxp");
        var hhea = container.RequireTable("hhea");
        var hmtx = container.RequireTable("hmtx");
        var loca = container.RequireTable("loca");
        var glyf = container.RequireTable("glyf");

        const int headSize = 54;
        if (head.Length < headSize)
        {
            throw new InvalidDataException("The 'head' table is too short.");
        }

        var unitsPerEm = SfntContainer.ReadUInt16(data, head.Offset + 18);
        if (unitsPerEm == 0)
        {
            throw new InvalidDataException("'head.unitsPerEm' must not be zero.");
        }

        var indexToLocFormat = SfntContainer.ReadInt16(data, head.Offset + 50);
        if (indexToLocFormat != 0 && indexToLocFormat != 1)
        {
            throw new InvalidDataException($"'head.indexToLocFormat' has an unsupported value ({indexToLocFormat}).");
        }

        const uint maxpVersion10 = 0x00010000;
        if (maxp.Length < 6)
        {
            throw new InvalidDataException("The 'maxp' table is too short.");
        }

        var maxpVersion = SfntContainer.ReadUInt32(data, maxp.Offset);
        if (maxpVersion != maxpVersion10)
        {
            throw new InvalidDataException(
                "The 'maxp' table version is not 1.0 (0x00010000); CFF-oriented maxp (version 0.5) fonts are not supported.");
        }

        var numGlyphs = SfntContainer.ReadUInt16(data, maxp.Offset + 4);

        var metrics = HmtxHheaReader.Parse(data, hhea.Offset, hhea.Length, hmtx.Offset, hmtx.Length, numGlyphs);
        var glyphs = GlyfLocaReader.Parse(data, loca.Offset, loca.Length, glyf.Offset, glyf.Length, numGlyphs, indexToLocFormat == 1);

        var cmap = container.TryGetTable("cmap", out var cmapRange)
            ? CmapTable.Parse(data, cmapRange.Offset, cmapRange.Length)
            : CmapTable.Empty;

        var kern = container.TryGetTable("kern", out var kernRange)
            ? KernTable.Parse(data, kernRange.Offset, kernRange.Length)
            : KernTable.Empty;

        return new TrueTypeFont(unitsPerEm, numGlyphs, metrics, glyphs, cmap, kern);
    }
}
