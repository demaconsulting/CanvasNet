// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO ttcf
// cspell:ignore macStyle fsSelection usWeightClass isFixedPitch italicAngle nameID
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     A font's resolved <c>name</c>-table strings: the typographic (or, if absent, standard)
///     family and subfamily names, the full name, and the PostScript name.
/// </summary>
/// <remarks>
///     Every field is <see langword="null"/> when the underlying font has no <c>name</c> table,
///     or no record for that specific name identifier, in any of the platform/encoding
///     combinations this type resolves (see <see cref="TrueTypeFont.GetNameInfo"/>) - this never
///     throws, mirroring <see cref="TrueTypeFont.GetKerning"/>'s "absent optional table => benign
///     default" contract rather than a required-table throwing contract.
/// </remarks>
/// <param name="FamilyName">
///     The typographic family name (<c>nameID</c> 16) if present; otherwise the standard family
///     name (<c>nameID</c> 1); otherwise <see langword="null"/>.
/// </param>
/// <param name="SubfamilyName">
///     The typographic subfamily name (<c>nameID</c> 17) if present; otherwise the standard
///     subfamily name (<c>nameID</c> 2); otherwise <see langword="null"/>.
/// </param>
/// <param name="FullName">The full font name (<c>nameID</c> 4), or <see langword="null"/>.</param>
/// <param name="PostScriptName">
///     The PostScript name (<c>nameID</c> 6), or <see langword="null"/>.
/// </param>
public readonly record struct FontNameInfo(
    string? FamilyName,
    string? SubfamilyName,
    string? FullName,
    string? PostScriptName);

/// <summary>
///     Provides hand-rolled, dependency-free loading and querying of TrueType (<c>glyf</c>-based)
///     and CFF/OpenType (<c>CFF </c>-based) SFNT font files, including TrueType Collection
///     (<c>ttcf</c>) containers: metrics, character-to-glyph mapping, glyph outline extraction,
///     advance widths, basic pairwise kerning, and <c>name</c>-table/style metadata.
/// </summary>
/// <remarks>
///     <para>
///     <c>TrueTypeFont</c> is the sole public unit of the <see cref="Fonts"/> namespace; it fronts
///     several internal, independently-parsed helpers (<see cref="SfntContainer"/>,
///     <see cref="CmapTable"/>, <see cref="GlyfLocaReader"/>, <see cref="CffTable"/>,
///     <see cref="CffCharstringInterpreter"/>, <see cref="HmtxHheaReader"/>, <see cref="KernTable"/>)
///     exactly as <see cref="Codecs.PngCodec"/> internally parses chunks as one unit, and as
///     <see cref="Drawing.PathFiller"/> fronts <see cref="Drawing.EdgeFlattener"/>/
///     <see cref="Drawing.ScanlineRasterizer"/>. <see cref="GlyfLocaReader"/> and
///     <see cref="CffTable"/> both implement the internal <see cref="IGlyphOutlineSource"/>
///     abstraction, letting <see cref="GetGlyphOutline"/> dispatch to whichever outline flavor
///     the loaded font actually uses without a type-check.
///     </para>
///     <para>
///     <c>head</c>, <c>hhea</c>, and <c>hmtx</c> are always required. Which further tables are
///     required depends on the font's outline flavor: a TrueType-outline font (no <c>CFF </c>
///     table) requires <c>maxp</c> version <c>1.0</c> (<c>0x00010000</c>), plus <c>loca</c> and
///     <c>glyf</c>; a CFF-outline font (a <c>CFF </c> table is present) requires <c>maxp</c>
///     version <c>0.5</c> (<c>0x00005000</c>) instead, and does not require <c>loca</c>/<c>glyf</c>.
///     A missing or structurally malformed required table causes <see cref="Load(Stream)"/>/
///     <see cref="Load(string)"/> to throw <see cref="InvalidDataException"/>. <c>cmap</c> and
///     <c>kern</c> are optional: many legitimately embedded/subsetted fonts omit <c>cmap</c>
///     entirely, and most fonts have no <c>kern</c> table at all. Absence of <c>cmap</c> means
///     <see cref="GetGlyphIndex"/> always returns <c>0</c> (<c>.notdef</c>); absence of
///     <c>kern</c> means <see cref="GetKerning"/> always returns <c>0</c>.
///     </para>
///     <para>
///     A CFF-outline font whose Top DICT declares a CID-keyed font (the <c>ROS</c> operator) is
///     rejected with <see cref="InvalidDataException"/> rather than attempting unsupported
///     <c>FDArray</c>/<c>FDSelect</c> parsing; see <see cref="CffTable"/>. A glyph whose Type 2
///     charstring uses the deprecated 4-operand <c>seac</c>-style accent composition form of
///     <c>endchar</c>, or any operator outside <see cref="CffCharstringInterpreter"/>'s supported
///     set (including the two-byte escape operators, such as flex), is likewise rejected with
///     <see cref="InvalidDataException"/> from <see cref="GetGlyphOutline"/> rather than silently
///     mis-decoded.
///     </para>
///     <para>
///     A leading <c>ttcf</c> tag identifies a TrueType Collection container of one or more faces
///     rather than a single bare SFNT font. <see cref="Load(Stream)"/>/<see cref="Load(string)"/>
///     transparently select face <c>0</c> when given a <c>ttcf</c> container - this is a purely
///     additive relaxation of previously-thrown behavior (a <c>ttcf</c> container previously threw
///     <see cref="InvalidDataException"/> as an "unrecognized SFNT version"). <see cref="Load(Stream, int)"/>/
///     <see cref="Load(string, int)"/> select an explicit face by index, and
///     <see cref="GetFaceCount(Stream)"/>/<see cref="GetFaceCount(string)"/> report how many faces
///     a file holds (<c>1</c> for an ordinary, non-collection SFNT file) so a caller can enumerate
///     faces before choosing one to load.
///     </para>
///     <para>
///     SFNT table checksums (including <c>head.checkSumAdjustment</c>) are not enforced as a
///     load-rejection condition: real-world TrueType fonts commonly carry stale or incorrect
///     checksums after ordinary subsetting/hinting/editing workflows. Structural validation
///     (bounds, overflow, required tables, internal glyph/cmap/kern consistency) protects against
///     malformed input instead.
///     </para>
///     <para>
///     <see cref="GetNameInfo"/> resolves the font's <c>name</c>-table family/subfamily/full/
///     PostScript name strings, and <see cref="IsBold"/>/<see cref="IsItalic"/>/
///     <see cref="IsFixedPitch"/> derive bold/italic/fixed-pitch style metadata from the optional
///     <c>OS/2</c> and <c>post</c> tables and the required <c>head.macStyle</c> field. Both are
///     resolved per-face for a <c>ttcf</c> container, exactly as every other table this class
///     reads. <c>name</c> and <c>OS/2</c>/<c>post</c> are all optional; their absence, or the
///     absence of any specific record/field within them, never throws - see
///     <see cref="NameTable"/>/<see cref="StyleTable"/> for the exact resolution and fallback
///     rules.
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
    private readonly IGlyphOutlineSource _glyphs;
    private readonly CmapTable _cmap;
    private readonly KernTable _kern;
    private readonly NameTable _name;

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

    /// <summary>
    ///     Whether this font is a bold-weight design, derived from the <c>OS/2</c> table's
    ///     <c>fsSelection</c> BOLD bit or <c>usWeightClass</c> &gt;= 600 when <c>OS/2</c> is
    ///     present, and always OR'd with <c>head.macStyle</c> bit 0.
    /// </summary>
    public bool IsBold { get; }

    /// <summary>
    ///     Whether this font is an italic/oblique design, derived from the <c>OS/2</c> table's
    ///     <c>fsSelection</c> ITALIC bit or a nonzero <c>post.italicAngle</c> when either table is
    ///     present, and always OR'd with <c>head.macStyle</c> bit 1.
    /// </summary>
    public bool IsItalic { get; }

    /// <summary>
    ///     Whether this font is a fixed-pitch (monospaced) design, from the <c>post</c> table's
    ///     <c>isFixedPitch</c> field. <see langword="false"/> when the <c>post</c> table is
    ///     absent or too short to contain that field.
    /// </summary>
    public bool IsFixedPitch { get; }

    private TrueTypeFont(
        int unitsPerEm, int glyphCount, HmtxHheaReader metrics, IGlyphOutlineSource glyphs, CmapTable cmap, KernTable kern,
        NameTable name, bool isBold, bool isItalic, bool isFixedPitch)
    {
        UnitsPerEm = unitsPerEm;
        GlyphCount = glyphCount;
        Ascender = metrics.Ascender;
        Descender = metrics.Descender;
        LineGap = metrics.LineGap;
        IsBold = isBold;
        IsItalic = isItalic;
        IsFixedPitch = isFixedPitch;
        _metrics = metrics;
        _glyphs = glyphs;
        _cmap = cmap;
        _kern = kern;
        _name = name;
    }

    /// <summary>
    ///     Loads a <see cref="TrueTypeFont"/> from an open, readable stream containing a TrueType
    ///     or CFF/OpenType SFNT font file, or a TrueType Collection (<c>ttcf</c>) container (in
    ///     which case face <c>0</c> is loaded - see <see cref="Load(Stream, int)"/> to select a
    ///     different face).
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the font from. Reading begins at the stream's current position and
    ///     consumes the remainder of the stream.
    /// </param>
    /// <returns>A new <see cref="TrueTypeFont"/> ready to be queried.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="stream"/> does not contain a valid, supported font: the
    ///     SFNT version is not recognized, an <c>OTTO</c>-tagged font has no <c>CFF </c> table, a
    ///     required table for the font's outline flavor is missing or structurally malformed
    ///     (see this class's remarks), a table directory entry's offset/length is out of bounds
    ///     or would overflow, the stream is truncated, or (for a CFF-outline font) the CFF data or
    ///     an individual glyph's Type 2 charstring is malformed, uses an unsupported operator, or
    ///     declares a CID-keyed font.
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

        return LoadFromBytesWithFaceIndex(ReadAllBytes(stream), 0);
    }

    /// <summary>
    ///     Loads a <see cref="TrueTypeFont"/> from a font file at the specified path, exactly as
    ///     <see cref="Load(Stream)"/>.
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
    ///     Loads a specific face of a <see cref="TrueTypeFont"/> from an open, readable stream,
    ///     supporting explicit face selection within a TrueType Collection (<c>ttcf</c>)
    ///     container.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the font from. Reading begins at the stream's current position and
    ///     consumes the remainder of the stream.
    /// </param>
    /// <param name="faceIndex">
    ///     The zero-based face index to load. For a <c>ttcf</c> container, must be within
    ///     <c>[0, GetFaceCount(stream))</c>. For an ordinary (non-collection) SFNT file, only
    ///     <c>0</c> is valid, and behaves identically to <see cref="Load(Stream)"/> - a single-face
    ///     file is treated as a one-face collection for this overload's purposes.
    /// </param>
    /// <returns>A new <see cref="TrueTypeFont"/> ready to be queried.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="faceIndex"/> is negative, or at or beyond the number of
    ///     faces the font file declares.
    /// </exception>
    /// <exception cref="InvalidDataException">Thrown for the same conditions as <see cref="Load(Stream)"/>.</exception>
    public static TrueTypeFont Load(Stream stream, int faceIndex)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return LoadFromBytesWithFaceIndex(ReadAllBytes(stream), faceIndex);
    }

    /// <summary>
    ///     Loads a specific face of a font file at the specified path, exactly as
    ///     <see cref="Load(Stream, int)"/>.
    /// </summary>
    /// <param name="path">The path of the font file to load. Must not be null or empty.</param>
    /// <param name="faceIndex">See <see cref="Load(Stream, int)"/>.</param>
    /// <returns>A new <see cref="TrueTypeFont"/> ready to be queried.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for the same condition as <see cref="Load(Stream, int)"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown for the same conditions as <see cref="Load(Stream)"/>.</exception>
    public static TrueTypeFont Load(string path, int faceIndex)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Load(stream, faceIndex);
    }

    /// <summary>
    ///     Reports how many faces a font file holds, without parsing any face's own table
    ///     directory.
    /// </summary>
    /// <param name="stream">
    ///     The stream to inspect. Reading begins at the stream's current position and consumes
    ///     the remainder of the stream.
    /// </param>
    /// <returns>
    ///     The number of faces declared by a <c>ttcf</c> TrueType Collection header, or <c>1</c>
    ///     for an ordinary (non-collection) SFNT file - callers can therefore iterate
    ///     <c>0..GetFaceCount(file)</c> uniformly, without a separate is-this-a-collection branch.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="stream"/> begins with the <c>ttcf</c> tag but is too short
    ///     to contain its fixed header prefix or its declared face offset table, or declares zero
    ///     faces.
    /// </exception>
    public static int GetFaceCount(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var data = ReadAllBytes(stream);
        return SfntContainer.TryReadTtcHeader(data, out var faceOffsets) ? faceOffsets.Count : 1;
    }

    /// <summary>
    ///     Reports how many faces a font file at the specified path holds, exactly as
    ///     <see cref="GetFaceCount(Stream)"/>.
    /// </summary>
    /// <param name="path">The path of the font file to inspect. Must not be null or empty.</param>
    /// <returns>See <see cref="GetFaceCount(Stream)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="InvalidDataException">Thrown for the same condition as <see cref="GetFaceCount(Stream)"/>.</exception>
    public static int GetFaceCount(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return GetFaceCount(stream);
    }

    /// <summary>
    ///     Looks up the glyph index mapped to a Unicode codepoint via the font's <c>cmap</c> table.
    /// </summary>
    /// <param name="codepoint">The Unicode codepoint to look up.</param>
    /// <returns>
    ///     The mapped glyph index, or <c>0</c> (<c>.notdef</c>) if the codepoint is unmapped, or
    ///     the font has no <c>cmap</c> table, or no supported <c>cmap</c> subtable is present.
    ///     Always in <c>[0, GlyphCount)</c> - a malformed <c>cmap</c> subtable mapping a codepoint
    ///     to a glyph index outside that range is clamped to <c>0</c>. Never throws.
    /// </returns>
    public int GetGlyphIndex(int codepoint)
    {
        var glyphIndex = _cmap.GetGlyphIndex(codepoint);
        return glyphIndex < 0 || glyphIndex >= GlyphCount ? 0 : glyphIndex;
    }

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
    ///     glyph's nesting depth (10), total resolved component count (5000), or total resolved
    ///     point/command count (200,000) exceeds its bound, when a composite glyph contains a
    ///     point-matched component, or when a composite glyph references an out-of-range
    ///     component glyph index.
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
    ///     present, the font has no <c>kern</c> table, no qualifying <c>kern</c> subtable is
    ///     present, or either glyph index is outside <c>[0, GlyphCount)</c> for this font. Never
    ///     throws, even for an out-of-range glyph index.
    /// </returns>
    public int GetKerning(int leftGlyphIndex, int rightGlyphIndex)
    {
        // KernTable.GetKerning only rejects indices outside [0, ushort.MaxValue] - it has no
        // notion of this particular font's GlyphCount, since it is parsed and validated
        // independently of maxp.numGlyphs. Without this check, a glyph index that is a valid
        // ushort but at/beyond this font's GlyphCount could still match a stale/bogus pair
        // recorded in the kern table, returning a nonzero adjustment for a glyph that does not
        // exist in this font.
        if (leftGlyphIndex < 0 || leftGlyphIndex >= GlyphCount || rightGlyphIndex < 0 || rightGlyphIndex >= GlyphCount)
        {
            return 0;
        }

        return _kern.GetKerning(leftGlyphIndex, rightGlyphIndex);
    }

    /// <summary>
    ///     Resolves this font's family/subfamily/full/PostScript name strings from its <c>name</c>
    ///     table, preferring the typographic (16/17) family/subfamily names over the standard
    ///     (1/2) ones when both are present, and preferring a Windows/Unicode-BMP platform record
    ///     over a Macintosh platform record when both are present for the same name identifier.
    /// </summary>
    /// <returns>
    ///     A <see cref="FontNameInfo"/> with every resolvable field populated, and any field the
    ///     font's <c>name</c> table (or the font as a whole) does not provide left
    ///     <see langword="null"/>. Never throws.
    /// </returns>
    public FontNameInfo GetNameInfo() => new(_name.FamilyName, _name.SubfamilyName, _name.FullName, _name.PostScriptName);

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
    ///     Reads the entirety of an open, readable stream into an in-memory byte array, from the
    ///     stream's current position through its end.
    /// </summary>
    private static byte[] ReadAllBytes(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>
    ///     Resolves <paramref name="faceIndex"/> against the font file's declared face count
    ///     (transparently treating an ordinary, non-collection SFNT file as a one-face
    ///     collection), then parses that single face via <see cref="LoadFromBytes"/>.
    /// </summary>
    private static TrueTypeFont LoadFromBytesWithFaceIndex(byte[] data, int faceIndex)
    {
        if (faceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex), faceIndex, "Face index must not be negative.");
        }

        var offsetTableStart = 0;
        if (SfntContainer.TryReadTtcHeader(data, out var faceOffsets))
        {
            if (faceIndex >= faceOffsets.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(faceIndex), faceIndex, $"Face index must be less than the container's face count ({faceOffsets.Count}).");
            }

            offsetTableStart = faceOffsets[faceIndex];
        }
        else if (faceIndex != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(faceIndex), faceIndex, "Face index must be 0 for a font file that is not a 'ttcf' collection.");
        }

        return LoadFromBytes(data, offsetTableStart);
    }

    /// <summary>
    ///     Parses one face's SFNT container, validates and parses every required table (branching
    ///     on the font's outline flavor - see this class's remarks), and parses the optional
    ///     <c>cmap</c>/<c>kern</c> tables tolerantly.
    /// </summary>
    private static TrueTypeFont LoadFromBytes(byte[] data, int offsetTableStart)
    {
        var container = SfntContainer.Parse(data, offsetTableStart);

        var head = container.RequireTable("head");
        var maxp = container.RequireTable("maxp");
        var hhea = container.RequireTable("hhea");
        var hmtx = container.RequireTable("hmtx");

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

        var isCff = container.TryGetTable("CFF ", out var cff);

        if (maxp.Length < 6)
        {
            throw new InvalidDataException("The 'maxp' table is too short.");
        }

        var maxpVersion = SfntContainer.ReadUInt32(data, maxp.Offset);
        var numGlyphs = SfntContainer.ReadUInt16(data, maxp.Offset + 4);

        IGlyphOutlineSource glyphs;
        if (isCff)
        {
            const uint maxpVersion05 = 0x00005000;
            if (maxpVersion != maxpVersion05)
            {
                throw new InvalidDataException(
                    "The 'maxp' table version is not 0.5 (0x00005000), as required for a CFF-outline font.");
            }

            var cffTable = CffTable.Parse(data, cff.Offset, cff.Length);
            if (cffTable.GlyphCount != numGlyphs)
            {
                throw new InvalidDataException(
                    $"'CFF ' CharStrings count ({cffTable.GlyphCount}) does not match 'maxp.numGlyphs' ({numGlyphs}).");
            }

            glyphs = cffTable;
        }
        else
        {
            const uint maxpVersion10 = 0x00010000;
            if (maxpVersion != maxpVersion10)
            {
                throw new InvalidDataException(
                    "The 'maxp' table version is not 1.0 (0x00010000); CFF-oriented maxp (version 0.5) fonts require a 'CFF ' table.");
            }

            const int maxpVersion10Size = 32;
            if (maxp.Length < maxpVersion10Size)
            {
                throw new InvalidDataException("The 'maxp' table (version 1.0) is too short; expected 32 bytes.");
            }

            var indexToLocFormat = SfntContainer.ReadInt16(data, head.Offset + 50);
            if (indexToLocFormat != 0 && indexToLocFormat != 1)
            {
                throw new InvalidDataException($"'head.indexToLocFormat' has an unsupported value ({indexToLocFormat}).");
            }

            var loca = container.RequireTable("loca");
            var glyf = container.RequireTable("glyf");
            glyphs = GlyfLocaReader.Parse(data, loca.Offset, loca.Length, glyf.Offset, glyf.Length, numGlyphs, indexToLocFormat == 1);
        }

        var metrics = HmtxHheaReader.Parse(data, hhea.Offset, hhea.Length, hmtx.Offset, hmtx.Length, numGlyphs);

        var cmap = container.TryGetTable("cmap", out var cmapRange)
            ? CmapTable.Parse(data, cmapRange.Offset, cmapRange.Length)
            : CmapTable.Empty;

        var kern = container.TryGetTable("kern", out var kernRange)
            ? KernTable.Parse(data, kernRange.Offset, kernRange.Length)
            : KernTable.Empty;

        var macStyle = SfntContainer.ReadUInt16(data, head.Offset + 44);

        var name = container.TryGetTable("name", out var nameRange)
            ? NameTable.Parse(data, nameRange.Offset, nameRange.Length)
            : NameTable.Empty;

        var os2 = container.TryGetTable("OS/2", out var os2Range) ? os2Range : ((int, int)?)null;
        var post = container.TryGetTable("post", out var postRange) ? postRange : ((int, int)?)null;
        var style = StyleTable.Parse(data, macStyle, os2, post);

        return new TrueTypeFont(
            unitsPerEm, numGlyphs, metrics, glyphs, cmap, kern,
            name, style.IsBold, style.IsItalic, style.IsFixedPitch);
    }
}
