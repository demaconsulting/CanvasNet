using System.Diagnostics.CodeAnalysis;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a font's OpenType <c>MATH</c> table: the global math layout constants, per-glyph
///     italic corrections, top-accent attachments, extended-shape flags and cut-in kerns, and the
///     size variants and glyph assemblies used to stretch operators and delimiters.
/// </summary>
/// <remarks>
///     <para>
///     MathML layout (the <c>DemaConsulting.CanvasNet.MathMl</c> package, through friend access)
///     needs these values in font design units, and the core already owns SFNT parsing and the
///     font bytes, so the table is read here once rather than by a second parser in the package.
///     The type is internal because the shape of a public math-font API is not settled.
///     </para>
///     <para>
///     The whole table is parsed eagerly by <see cref="Parse"/> (which <see cref="TrueTypeFont"/>
///     calls lazily, on first access) and every offset and count is bounds-checked against the
///     table end in overflow-safe <see langword="long"/> arithmetic, mirroring
///     <see cref="KernTable"/> and <see cref="CmapTable"/>. Unlike those tolerant readers, a
///     malformed <c>MATH</c> table fails closed with <see cref="InvalidDataException"/>: a math
///     layout built from partially read constants would be silently wrong, so the caller is told
///     instead. Device tables (the pixel-size adjustments of each MathValueRecord) are ignored,
///     because layout scales design units without hinting.
///     </para>
///     <para>
///     Kern, construction, and assembly subtables are cached by position so a subtable that many
///     glyphs share is read once, and the total size of the distinct subtables read is limited to
///     the table length, so overlapping offsets cannot make the work grow beyond linear in the
///     table size. Instances are immutable after <see cref="Parse"/> and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class MathTable
{
    /// <summary>The size in bytes of the <c>MATH</c> header (version and three subtable offsets).</summary>
    private const int HeaderSize = 10;

    /// <summary>
    ///     The size in bytes of the MathConstants subtable: four 16-bit values, 51 MathValueRecords
    ///     of 4 bytes, and one 16-bit value.
    /// </summary>
    private const int ConstantsSize = 2 + 2 + 2 + 2 + (51 * MathValueRecordSize) + 2;

    /// <summary>The size in bytes of the MathGlyphInfo header (four subtable offsets).</summary>
    private const int GlyphInfoHeaderSize = 8;

    /// <summary>
    ///     The size in bytes of the MathVariants header (<c>minConnectorOverlap</c>, two coverage
    ///     offsets, and two construction counts), before the construction offset arrays.
    /// </summary>
    private const int VariantsHeaderSize = 10;

    /// <summary>The size in bytes of a MathValueRecord (a signed 16-bit value and a device-table offset).</summary>
    private const int MathValueRecordSize = 4;

    /// <summary>The size in bytes of an Offset16 field.</summary>
    private const int Offset16Size = 2;

    /// <summary>The number of corners of a MathKernInfoRecord (top-right, top-left, bottom-right, bottom-left).</summary>
    private const int KernCornerCount = 4;

    /// <summary>The coverage of <see cref="_italicsValues"/>.</summary>
    private readonly OpenTypeCoverage _italicsCoverage;

    /// <summary>The italic corrections, indexed by coverage index of <see cref="_italicsCoverage"/>.</summary>
    private readonly int[] _italicsValues;

    /// <summary>The coverage of <see cref="_topAccentValues"/>.</summary>
    private readonly OpenTypeCoverage _topAccentCoverage;

    /// <summary>The top-accent attachment positions, indexed by coverage index of <see cref="_topAccentCoverage"/>.</summary>
    private readonly int[] _topAccentValues;

    /// <summary>The glyphs that are extended shapes.</summary>
    private readonly OpenTypeCoverage _extendedShapes;

    /// <summary>The coverage of <see cref="_kerns"/>.</summary>
    private readonly OpenTypeCoverage _kernCoverage;

    /// <summary>
    ///     The cut-in kerns, four per covered glyph (at coverage index × 4 + corner), each
    ///     <see langword="null"/> when the font defines no kern for that corner.
    /// </summary>
    private readonly MathKern?[] _kerns;

    /// <summary>The coverage of <see cref="_vertical"/>.</summary>
    private readonly OpenTypeCoverage _verticalCoverage;

    /// <summary>The vertical constructions, indexed by coverage index of <see cref="_verticalCoverage"/>.</summary>
    private readonly MathGlyphConstruction?[] _vertical;

    /// <summary>The coverage of <see cref="_horizontal"/>.</summary>
    private readonly OpenTypeCoverage _horizontalCoverage;

    /// <summary>The horizontal constructions, indexed by coverage index of <see cref="_horizontalCoverage"/>.</summary>
    private readonly MathGlyphConstruction?[] _horizontal;

    /// <summary>
    ///     Parses every subtable of a <c>MATH</c> table whose header <see cref="Parse"/> has
    ///     already validated.
    /// </summary>
    /// <param name="reader">The bounds-checking reader over the table.</param>
    /// <param name="tableOffset">The absolute position of the table within the font data.</param>
    /// <exception cref="InvalidDataException">Thrown when any subtable is malformed.</exception>
    private MathTable(TableReader reader, int tableOffset)
    {
        var data = reader.Data;

        // MathConstants is required: Parse has already rejected a zero offset
        var constants = reader.Resolve(tableOffset, SfntContainer.ReadUInt16(data, tableOffset + 4), ConstantsSize, "MathConstants");
        Constants = ReadConstants(data, constants);

        // MathGlyphInfo is optional; each of its four subtables is optional too
        var glyphInfo = ReadOptionalSubtable(reader, tableOffset, tableOffset + 6, GlyphInfoHeaderSize, "MathGlyphInfo");
        (_italicsCoverage, _italicsValues) = ReadValueTable(reader, glyphInfo, ReadField(data, glyphInfo, 0), "MathItalicsCorrectionInfo");
        (_topAccentCoverage, _topAccentValues) = ReadValueTable(reader, glyphInfo, ReadField(data, glyphInfo, 2), "MathTopAccentAttachment");
        var extendedShapeOffset = ReadField(data, glyphInfo, 4);
        _extendedShapes = extendedShapeOffset == 0
            ? OpenTypeCoverage.Empty
            : reader.ReadCoverage(glyphInfo, extendedShapeOffset, "ExtendedShapeCoverage");
        (_kernCoverage, _kerns) = ReadKernInfo(reader, glyphInfo, ReadField(data, glyphInfo, 6));

        // MathVariants is optional; its header is followed by the vertical then horizontal offset arrays
        var variants = ReadOptionalSubtable(reader, tableOffset, tableOffset + 8, VariantsHeaderSize, "MathVariants");
        MinConnectorOverlap = ReadField(data, variants, 0);
        var verticalCount = ReadField(data, variants, 6);
        var horizontalCount = ReadField(data, variants, 8);
        var verticalOffsets = variants + VariantsHeaderSize;
        var horizontalOffsets = verticalOffsets + verticalCount * Offset16Size;
        if (variants >= 0)
        {
            reader.RequireArray(verticalOffsets, verticalCount + horizontalCount, Offset16Size, "MathVariants construction offsets");
        }

        (_verticalCoverage, _vertical) = ReadConstructions(
            reader, variants, ReadField(data, variants, 2), verticalCount, verticalOffsets, "vertical");
        (_horizontalCoverage, _horizontal) = ReadConstructions(
            reader, variants, ReadField(data, variants, 4), horizontalCount, horizontalOffsets, "horizontal");
    }

    /// <summary>
    ///     The global math layout constants (MathConstants), in font design units except the
    ///     three percentages.
    /// </summary>
    public MathConstants Constants { get; }

    /// <summary>
    ///     The minimum overlap, in font design units, between connecting parts of a glyph assembly
    ///     (MathVariants <c>minConnectorOverlap</c>); <c>0</c> when the table has no MathVariants.
    /// </summary>
    public int MinConnectorOverlap { get; }

    /// <summary>
    ///     Parses a complete <c>MATH</c> table.
    /// </summary>
    /// <remarks>
    ///     Called once per font, lazily, by <see cref="TrueTypeFont.TryGetMathTable"/>, so a
    ///     malformed table never affects loading a font. Reads only; allocates the parsed arrays.
    /// </remarks>
    /// <param name="data">The complete font file contents. Must not be null.</param>
    /// <param name="tableOffset">The absolute position of the <c>MATH</c> table within <paramref name="data"/>.</param>
    /// <param name="tableLength">The length of the <c>MATH</c> table, from the table directory.</param>
    /// <param name="glyphCount">
    ///     The font's glyph count (<c>maxp.numGlyphs</c>); every variant and part glyph must be
    ///     smaller.
    /// </param>
    /// <returns>The parsed table.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the table lies outside <paramref name="data"/>, is shorter than its header,
    ///     declares a major version other than 1, has no MathConstants, or any offset, count,
    ///     coverage index, or glyph ID of any subtable is out of bounds. The message names the
    ///     subtable.
    /// </exception>
    public static MathTable Parse(byte[] data, int tableOffset, int tableLength, int glyphCount)
    {
        ArgumentNullException.ThrowIfNull(data);

        // Validate the table range and the fixed header before reading any subtable
        if (tableOffset < 0 || tableLength < 0 || (long)tableOffset + tableLength > data.Length)
        {
            throw new InvalidDataException("The MATH table lies outside the font data.");
        }

        if (tableLength < HeaderSize)
        {
            throw new InvalidDataException("The MATH table is shorter than its 10-byte header.");
        }

        var majorVersion = SfntContainer.ReadUInt16(data, tableOffset);
        if (majorVersion != 1)
        {
            throw new InvalidDataException($"MATH table major version {majorVersion} is not supported (only 1 is defined).");
        }

        if (SfntContainer.ReadUInt16(data, tableOffset + 4) == 0)
        {
            throw new InvalidDataException("The MATH table has no MathConstants subtable.");
        }

        // Parse the subtables under a bounds-checking reader with a work budget of one table length
        var reader = new TableReader(data, tableOffset + tableLength, glyphCount, tableLength);
        return new MathTable(reader, tableOffset);
    }

    /// <summary>
    ///     Looks up the italic correction of a glyph (MathItalicsCorrectionInfo).
    /// </summary>
    /// <param name="glyph">The glyph index. Any value is accepted.</param>
    /// <param name="value">The italic correction in font design units, or <c>0</c> when absent.</param>
    /// <returns><see langword="true"/> when the font defines an italic correction for the glyph. Never throws.</returns>
    public bool TryGetItalicsCorrection(int glyph, out int value) =>
        TryGetValue(_italicsCoverage, _italicsValues, glyph, out value);

    /// <summary>
    ///     Looks up the horizontal position, from the glyph origin, at which an accent is centered
    ///     over a glyph (MathTopAccentAttachment).
    /// </summary>
    /// <param name="glyph">The glyph index. Any value is accepted.</param>
    /// <param name="value">The attachment position in font design units, or <c>0</c> when absent.</param>
    /// <returns><see langword="true"/> when the font defines an attachment for the glyph. Never throws.</returns>
    public bool TryGetTopAccentAttachment(int glyph, out int value) =>
        TryGetValue(_topAccentCoverage, _topAccentValues, glyph, out value);

    /// <summary>
    ///     Reports whether a glyph is an extended shape (ExtendedShapeCoverage) - a glyph, such as a
    ///     large operator, whose scripts are positioned by its ink rather than its line metrics.
    /// </summary>
    /// <param name="glyph">The glyph index. Any value is accepted.</param>
    /// <returns><see langword="true"/> when the glyph is covered. Never throws.</returns>
    public bool IsExtendedShape(int glyph) => _extendedShapes.IndexOf(glyph) >= 0;

    /// <summary>
    ///     Looks up the cut-in kern of one corner of a glyph (MathKernInfo).
    /// </summary>
    /// <param name="glyph">The glyph index. Any value is accepted.</param>
    /// <param name="corner">The corner whose kern is requested.</param>
    /// <param name="kern">The kern, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the font defines a kern for that corner of the glyph. Never throws.</returns>
    public bool TryGetMathKern(int glyph, MathKernCorner corner, [NotNullWhen(true)] out MathKern? kern)
    {
        var index = _kernCoverage.IndexOf(glyph);
        var corners = (int)corner;
        kern = index >= 0 && corners is >= 0 and < KernCornerCount ? _kerns[index * KernCornerCount + corners] : null;
        return kern is not null;
    }

    /// <summary>
    ///     Gets the size variants and glyph assembly used to stretch a glyph vertically.
    /// </summary>
    /// <param name="glyph">The glyph index. Any value is accepted.</param>
    /// <returns>The construction, or <see langword="null"/> when the font defines none for the glyph. Never throws.</returns>
    public MathGlyphConstruction? GetVerticalConstruction(int glyph) => GetConstruction(_verticalCoverage, _vertical, glyph);

    /// <summary>
    ///     Gets the size variants and glyph assembly used to stretch a glyph horizontally.
    /// </summary>
    /// <param name="glyph">The glyph index. Any value is accepted.</param>
    /// <returns>The construction, or <see langword="null"/> when the font defines none for the glyph. Never throws.</returns>
    public MathGlyphConstruction? GetHorizontalConstruction(int glyph) => GetConstruction(_horizontalCoverage, _horizontal, glyph);

    /// <summary>
    ///     Looks up a per-glyph value through its coverage.
    /// </summary>
    /// <param name="coverage">The coverage of <paramref name="values"/>.</param>
    /// <param name="values">The values, indexed by coverage index.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="value">The value, or <c>0</c> when the glyph is not covered.</param>
    /// <returns><see langword="true"/> when the glyph is covered.</returns>
    private static bool TryGetValue(OpenTypeCoverage coverage, int[] values, int glyph, out int value)
    {
        var index = coverage.IndexOf(glyph);
        value = index >= 0 ? values[index] : 0;
        return index >= 0;
    }

    /// <summary>
    ///     Looks up a construction through its coverage.
    /// </summary>
    /// <param name="coverage">The coverage of <paramref name="constructions"/>.</param>
    /// <param name="constructions">The constructions, indexed by coverage index.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <returns>The construction, or <see langword="null"/>.</returns>
    private static MathGlyphConstruction? GetConstruction(
        OpenTypeCoverage coverage, MathGlyphConstruction?[] constructions, int glyph)
    {
        var index = coverage.IndexOf(glyph);
        return index >= 0 ? constructions[index] : null;
    }

    /// <summary>
    ///     Reads an unsigned 16-bit header field (an offset or a count) of a subtable that may be absent.
    /// </summary>
    /// <param name="data">The font data.</param>
    /// <param name="subtable">The absolute position of the subtable, or <c>-1</c> when absent.</param>
    /// <param name="field">The byte offset of the field within the subtable header.</param>
    /// <returns>The field value, or <c>0</c> (an absent offset or count) when the subtable is absent.</returns>
    private static int ReadField(byte[] data, int subtable, int field) =>
        subtable < 0 ? 0 : SfntContainer.ReadUInt16(data, subtable + field);

    /// <summary>
    ///     Resolves an optional subtable whose offset is stored in the <c>MATH</c> header.
    /// </summary>
    /// <param name="reader">The table reader.</param>
    /// <param name="tableOffset">The absolute position of the <c>MATH</c> table (the offset base).</param>
    /// <param name="offsetPosition">The absolute position of the Offset16 field.</param>
    /// <param name="headerSize">The fixed header size the subtable must hold.</param>
    /// <param name="what">The subtable name, for error messages.</param>
    /// <returns>The absolute position of the subtable, or <c>-1</c> when the offset is zero.</returns>
    /// <exception cref="InvalidDataException">Thrown when the header does not fit in the table.</exception>
    private static int ReadOptionalSubtable(TableReader reader, int tableOffset, int offsetPosition, int headerSize, string what)
    {
        var offset = SfntContainer.ReadUInt16(reader.Data, offsetPosition);
        return offset == 0 ? -1 : reader.Resolve(tableOffset, offset, headerSize, what);
    }

    /// <summary>
    ///     Reads the MathConstants subtable.
    /// </summary>
    /// <remarks>
    ///     Each MathValueRecord is addressed by its index in OpenType order, so the property
    ///     assignments below can be checked one by one against the specification.
    /// </remarks>
    /// <param name="data">The font data.</param>
    /// <param name="position">The absolute position of MathConstants; <see cref="ConstantsSize"/> bytes are in bounds.</param>
    /// <returns>The constants.</returns>
    private static MathConstants ReadConstants(byte[] data, int position)
    {
        var records = position + 8;
        int Value(int index) => SfntContainer.ReadInt16(data, records + index * MathValueRecordSize);

        return new MathConstants
        {
            ScriptPercentScaleDown = SfntContainer.ReadInt16(data, position),
            ScriptScriptPercentScaleDown = SfntContainer.ReadInt16(data, position + 2),
            DelimitedSubFormulaMinHeight = SfntContainer.ReadUInt16(data, position + 4),
            DisplayOperatorMinHeight = SfntContainer.ReadUInt16(data, position + 6),
            MathLeading = Value(0),
            AxisHeight = Value(1),
            AccentBaseHeight = Value(2),
            FlattenedAccentBaseHeight = Value(3),
            SubscriptShiftDown = Value(4),
            SubscriptTopMax = Value(5),
            SubscriptBaselineDropMin = Value(6),
            SuperscriptShiftUp = Value(7),
            SuperscriptShiftUpCramped = Value(8),
            SuperscriptBottomMin = Value(9),
            SuperscriptBaselineDropMax = Value(10),
            SubSuperscriptGapMin = Value(11),
            SuperscriptBottomMaxWithSubscript = Value(12),
            SpaceAfterScript = Value(13),
            UpperLimitGapMin = Value(14),
            UpperLimitBaselineRiseMin = Value(15),
            LowerLimitGapMin = Value(16),
            LowerLimitBaselineDropMin = Value(17),
            StackTopShiftUp = Value(18),
            StackTopDisplayStyleShiftUp = Value(19),
            StackBottomShiftDown = Value(20),
            StackBottomDisplayStyleShiftDown = Value(21),
            StackGapMin = Value(22),
            StackDisplayStyleGapMin = Value(23),
            StretchStackTopShiftUp = Value(24),
            StretchStackBottomShiftDown = Value(25),
            StretchStackGapAboveMin = Value(26),
            StretchStackGapBelowMin = Value(27),
            FractionNumeratorShiftUp = Value(28),
            FractionNumeratorDisplayStyleShiftUp = Value(29),
            FractionDenominatorShiftDown = Value(30),
            FractionDenominatorDisplayStyleShiftDown = Value(31),
            FractionNumeratorGapMin = Value(32),
            FractionNumDisplayStyleGapMin = Value(33),
            FractionRuleThickness = Value(34),
            FractionDenominatorGapMin = Value(35),
            FractionDenomDisplayStyleGapMin = Value(36),
            SkewedFractionHorizontalGap = Value(37),
            SkewedFractionVerticalGap = Value(38),
            OverbarVerticalGap = Value(39),
            OverbarRuleThickness = Value(40),
            OverbarExtraAscender = Value(41),
            UnderbarVerticalGap = Value(42),
            UnderbarRuleThickness = Value(43),
            UnderbarExtraDescender = Value(44),
            RadicalVerticalGap = Value(45),
            RadicalDisplayStyleVerticalGap = Value(46),
            RadicalRuleThickness = Value(47),
            RadicalExtraAscender = Value(48),
            RadicalKernBeforeDegree = Value(49),
            RadicalKernAfterDegree = Value(50),
            RadicalDegreeBottomRaisePercent = SfntContainer.ReadInt16(data, records + 51 * MathValueRecordSize)
        };
    }

    /// <summary>
    ///     Reads a coverage-keyed MathValueRecord table (MathItalicsCorrectionInfo or
    ///     MathTopAccentAttachment): a coverage offset, a count, and that many records.
    /// </summary>
    /// <param name="reader">The table reader.</param>
    /// <param name="parent">The absolute position of MathGlyphInfo, or <c>-1</c> when absent.</param>
    /// <param name="offset">The Offset16 of the subtable from <paramref name="parent"/>; <c>0</c> when absent.</param>
    /// <param name="what">The subtable name, for error messages.</param>
    /// <returns>The coverage and the values indexed by coverage index (empty when absent).</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the subtable or its records lie outside the table, its coverage is missing or
    ///     malformed, or a coverage index reaches past the record count.
    /// </exception>
    private static (OpenTypeCoverage Coverage, int[] Values) ReadValueTable(TableReader reader, int parent, int offset, string what)
    {
        if (offset == 0)
        {
            return (OpenTypeCoverage.Empty, []);
        }

        // Read the header and check the record array fits
        var data = reader.Data;
        var position = reader.Resolve(parent, offset, 4, what);
        var count = SfntContainer.ReadUInt16(data, position + 2);
        var records = position + 4;
        reader.RequireArray(records, count, MathValueRecordSize, what);

        // The coverage must not index past the records
        var coverage = reader.ReadCoverage(position, SfntContainer.ReadUInt16(data, position), what);
        RequireCoverageFits(coverage, count, what);

        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = SfntContainer.ReadInt16(data, records + i * MathValueRecordSize);
        }

        return (coverage, values);
    }

    /// <summary>
    ///     Reads the MathKernInfo subtable: a coverage offset, a count, and per covered glyph four
    ///     corner offsets to MathKern subtables.
    /// </summary>
    /// <param name="reader">The table reader.</param>
    /// <param name="parent">The absolute position of MathGlyphInfo, or <c>-1</c> when absent.</param>
    /// <param name="offset">The Offset16 of MathKernInfo from <paramref name="parent"/>; <c>0</c> when absent.</param>
    /// <returns>The coverage and the kerns, four per covered glyph (empty when absent).</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the subtable, its records, or any MathKern lie outside the table, its coverage
    ///     is missing or malformed, or a coverage index reaches past the record count.
    /// </exception>
    private static (OpenTypeCoverage Coverage, MathKern?[] Kerns) ReadKernInfo(TableReader reader, int parent, int offset)
    {
        const string what = "MathKernInfo";
        if (offset == 0)
        {
            return (OpenTypeCoverage.Empty, []);
        }

        // Read the header and check the record array (four Offset16 per glyph) fits
        var data = reader.Data;
        var position = reader.Resolve(parent, offset, 4, what);
        var count = SfntContainer.ReadUInt16(data, position + 2);
        var records = position + 4;
        reader.RequireArray(records, count, KernCornerCount * Offset16Size, what);

        var coverage = reader.ReadCoverage(position, SfntContainer.ReadUInt16(data, position), what);
        RequireCoverageFits(coverage, count, what);

        // Read each corner's MathKern; a zero offset means the corner has no kern
        var kerns = new MathKern?[count * KernCornerCount];
        for (var i = 0; i < kerns.Length; i++)
        {
            var kernOffset = SfntContainer.ReadUInt16(data, records + i * Offset16Size);
            kerns[i] = kernOffset == 0 ? null : reader.ReadKern(position, kernOffset);
        }

        return (coverage, kerns);
    }

    /// <summary>
    ///     Reads one direction (vertical or horizontal) of MathVariants: its coverage and its
    ///     array of MathGlyphConstruction offsets.
    /// </summary>
    /// <param name="reader">The table reader.</param>
    /// <param name="variants">The absolute position of MathVariants, or <c>-1</c> when absent.</param>
    /// <param name="coverageOffset">The coverage Offset16 from MathVariants; <c>0</c> when absent.</param>
    /// <param name="count">The construction count.</param>
    /// <param name="offsets">The absolute position of the construction offset array (already bounds-checked).</param>
    /// <param name="direction">"vertical" or "horizontal", for error messages.</param>
    /// <returns>The coverage and the constructions indexed by coverage index (empty when absent).</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the coverage or a construction is malformed, or a coverage index reaches
    ///     past the construction count.
    /// </exception>
    private static (OpenTypeCoverage Coverage, MathGlyphConstruction?[] Constructions) ReadConstructions(
        TableReader reader, int variants, int coverageOffset, int count, int offsets, string direction)
    {
        if (variants < 0)
        {
            return (OpenTypeCoverage.Empty, []);
        }

        // A missing coverage covers nothing, so its constructions are unreachable
        var what = $"MathVariants {direction} coverage";
        var coverage = coverageOffset == 0 ? OpenTypeCoverage.Empty : reader.ReadCoverage(variants, coverageOffset, what);
        RequireCoverageFits(coverage, count, what);

        // Read each construction; a zero offset leaves that glyph without one
        var data = reader.Data;
        var constructions = new MathGlyphConstruction?[count];
        for (var i = 0; i < count; i++)
        {
            var offset = SfntContainer.ReadUInt16(data, offsets + i * Offset16Size);
            constructions[i] = offset == 0 ? null : reader.ReadConstruction(variants, offset);
        }

        return (coverage, constructions);
    }

    /// <summary>
    ///     Checks that every coverage index of a coverage has a record in its parallel array.
    /// </summary>
    /// <param name="coverage">The coverage.</param>
    /// <param name="count">The record count of the parallel array.</param>
    /// <param name="what">The subtable name, for error messages.</param>
    /// <exception cref="InvalidDataException">Thrown when a coverage index reaches past <paramref name="count"/>.</exception>
    private static void RequireCoverageFits(OpenTypeCoverage coverage, int count, string what)
    {
        if (coverage.IndexCount > count)
        {
            throw new InvalidDataException(
                $"The MATH {what} coverage indexes {coverage.IndexCount} records, but only {count} are present.");
        }
    }

    /// <summary>
    ///     Bounds-checks and reads the repeated subtables of one <c>MATH</c> table, caching them by
    ///     position and charging their sizes against a work budget.
    /// </summary>
    /// <remarks>
    ///     Exists only for the duration of <see cref="Parse"/>; not thread-safe, and never shared.
    /// </remarks>
    private sealed class TableReader
    {
        /// <summary>The exclusive absolute end of the <c>MATH</c> table.</summary>
        private readonly int _tableEnd;

        /// <summary>The font's glyph count; variant and part glyphs must be smaller.</summary>
        private readonly int _glyphCount;

        /// <summary>The MathKern subtables read so far, by absolute position.</summary>
        private readonly Dictionary<int, MathKern> _kerns = [];

        /// <summary>The MathGlyphConstruction subtables read so far, by absolute position.</summary>
        private readonly Dictionary<int, MathGlyphConstruction> _constructions = [];

        /// <summary>
        ///     The GlyphAssembly subtables read so far, by absolute position (<see langword="null"/>
        ///     for an assembly with no parts).
        /// </summary>
        private readonly Dictionary<int, MathGlyphAssembly?> _assemblies = [];

        /// <summary>
        ///     The bytes of distinct repeated subtables that may still be read. Subtables of a
        ///     well-formed table do not overlap, so their sizes sum to at most the table length;
        ///     exceeding it means offsets were made to overlap to multiply the work.
        /// </summary>
        private long _budget;

        /// <summary>
        ///     Initializes a reader over one table.
        /// </summary>
        /// <param name="data">The font data.</param>
        /// <param name="tableEnd">The exclusive absolute end of the table.</param>
        /// <param name="glyphCount">The font's glyph count.</param>
        /// <param name="budget">The work budget in bytes (the table length).</param>
        public TableReader(byte[] data, int tableEnd, int glyphCount, long budget)
        {
            Data = data;
            _tableEnd = tableEnd;
            _glyphCount = glyphCount;
            _budget = budget;
        }

        /// <summary>The font data.</summary>
        public byte[] Data { get; }

        /// <summary>
        ///     Resolves an Offset16 from a base position and checks that a fixed-size header fits
        ///     before the table end.
        /// </summary>
        /// <param name="basePosition">The absolute position the offset is relative to.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="size">The number of bytes that must fit at the resolved position.</param>
        /// <param name="what">The subtable name, for error messages.</param>
        /// <returns>The absolute position.</returns>
        /// <exception cref="InvalidDataException">Thrown when the bytes do not fit.</exception>
        public int Resolve(int basePosition, int offset, int size, string what)
        {
            var position = (long)basePosition + offset;
            if (position + size > _tableEnd)
            {
                throw new InvalidDataException($"The MATH {what} subtable lies outside the table.");
            }

            return (int)position;
        }

        /// <summary>
        ///     Checks that an array of records fits before the table end, in overflow-safe
        ///     arithmetic.
        /// </summary>
        /// <param name="start">The absolute position of the first record.</param>
        /// <param name="count">The record count.</param>
        /// <param name="recordSize">The size in bytes of one record.</param>
        /// <param name="what">The subtable name, for error messages.</param>
        /// <exception cref="InvalidDataException">Thrown when the records do not fit.</exception>
        public void RequireArray(int start, long count, int recordSize, string what)
        {
            if (start + count * recordSize > _tableEnd)
            {
                throw new InvalidDataException($"The MATH {what} declares {count} records, more than fit in the table.");
            }
        }

        /// <summary>
        ///     Resolves and parses a required coverage.
        /// </summary>
        /// <param name="basePosition">The absolute position the offset is relative to.</param>
        /// <param name="offset">The coverage offset; zero is malformed.</param>
        /// <param name="what">The owning subtable name, for error messages.</param>
        /// <returns>The coverage.</returns>
        /// <exception cref="InvalidDataException">Thrown when the coverage is missing or malformed.</exception>
        public OpenTypeCoverage ReadCoverage(int basePosition, int offset, string what)
        {
            if (offset == 0)
            {
                throw new InvalidDataException($"The MATH {what} subtable has no coverage.");
            }

            var position = Resolve(basePosition, offset, 0, what);
            return OpenTypeCoverage.Parse(Data, position, _tableEnd);
        }

        /// <summary>
        ///     Reads (or returns the cached) MathKern subtable: a height count, the correction
        ///     heights, and one more kern value than heights.
        /// </summary>
        /// <param name="basePosition">The absolute position of MathKernInfo.</param>
        /// <param name="offset">The MathKern offset (non-zero).</param>
        /// <returns>The kern.</returns>
        /// <exception cref="InvalidDataException">Thrown when the kern does not fit or the work budget is exceeded.</exception>
        public MathKern ReadKern(int basePosition, int offset)
        {
            const string what = "MathKern";
            var position = Resolve(basePosition, offset, 2, what);
            if (_kerns.TryGetValue(position, out var cached))
            {
                return cached;
            }

            // heightCount heights are followed by heightCount + 1 kern values
            var heightCount = SfntContainer.ReadUInt16(Data, position);
            var records = position + 2;
            var recordCount = 2L * heightCount + 1;
            RequireArray(records, recordCount, MathValueRecordSize, what);
            Charge(2 + recordCount * MathValueRecordSize);

            var heights = new int[heightCount];
            var values = new int[heightCount + 1];
            for (var i = 0; i < heightCount; i++)
            {
                heights[i] = SfntContainer.ReadInt16(Data, records + i * MathValueRecordSize);
            }

            var valuesStart = records + heightCount * MathValueRecordSize;
            for (var i = 0; i <= heightCount; i++)
            {
                values[i] = SfntContainer.ReadInt16(Data, valuesStart + i * MathValueRecordSize);
            }

            var kern = new MathKern(Array.AsReadOnly(heights), Array.AsReadOnly(values));
            _kerns[position] = kern;
            return kern;
        }

        /// <summary>
        ///     Reads (or returns the cached) MathGlyphConstruction subtable: an assembly offset, a
        ///     variant count, and the variant records.
        /// </summary>
        /// <param name="basePosition">The absolute position of MathVariants.</param>
        /// <param name="offset">The construction offset (non-zero).</param>
        /// <returns>The construction.</returns>
        /// <exception cref="InvalidDataException">
        ///     Thrown when the construction or its assembly does not fit, a variant or part glyph
        ///     is not smaller than the glyph count, or the work budget is exceeded.
        /// </exception>
        public MathGlyphConstruction ReadConstruction(int basePosition, int offset)
        {
            const string what = "MathGlyphConstruction";
            const int variantRecordSize = 4;
            var position = Resolve(basePosition, offset, 4, what);
            if (_constructions.TryGetValue(position, out var cached))
            {
                return cached;
            }

            // Read the variant records, each a glyph and its advance in the stretch direction
            var assemblyOffset = SfntContainer.ReadUInt16(Data, position);
            var variantCount = SfntContainer.ReadUInt16(Data, position + 2);
            var records = position + 4;
            RequireArray(records, variantCount, variantRecordSize, what);
            Charge(4 + (long)variantCount * variantRecordSize);

            var variants = new MathGlyphVariant[variantCount];
            for (var i = 0; i < variantCount; i++)
            {
                var record = records + i * variantRecordSize;
                var glyph = RequireGlyph(SfntContainer.ReadUInt16(Data, record), what);
                variants[i] = new MathGlyphVariant(glyph, SfntContainer.ReadUInt16(Data, record + 2));
            }

            // The assembly offset is relative to the construction; zero means none
            var assembly = assemblyOffset == 0 ? null : ReadAssembly(position, assemblyOffset);
            var construction = new MathGlyphConstruction(Array.AsReadOnly(variants), assembly);
            _constructions[position] = construction;
            return construction;
        }

        /// <summary>
        ///     Reads (or returns the cached) GlyphAssembly subtable: an italic correction, a part
        ///     count, and the part records.
        /// </summary>
        /// <param name="basePosition">The absolute position of the owning construction.</param>
        /// <param name="offset">The assembly offset (non-zero).</param>
        /// <returns>The assembly, or <see langword="null"/> when it has no parts.</returns>
        /// <exception cref="InvalidDataException">
        ///     Thrown when the assembly does not fit, a part glyph is not smaller than the glyph
        ///     count, or the work budget is exceeded.
        /// </exception>
        private MathGlyphAssembly? ReadAssembly(int basePosition, int offset)
        {
            const string what = "GlyphAssembly";
            const int partRecordSize = 10;
            var position = Resolve(basePosition, offset, MathValueRecordSize + 2, what);
            if (_assemblies.TryGetValue(position, out var cached))
            {
                return cached;
            }

            // An italic-correction MathValueRecord and a part count precede the part records
            var italicsCorrection = SfntContainer.ReadInt16(Data, position);
            var partCount = SfntContainer.ReadUInt16(Data, position + MathValueRecordSize);
            var records = position + MathValueRecordSize + 2;
            RequireArray(records, partCount, partRecordSize, what);
            Charge(MathValueRecordSize + 2 + (long)partCount * partRecordSize);

            MathGlyphAssembly? assembly = null;
            if (partCount > 0)
            {
                var parts = new MathGlyphPart[partCount];
                for (var i = 0; i < partCount; i++)
                {
                    var record = records + i * partRecordSize;
                    parts[i] = new MathGlyphPart(
                        RequireGlyph(SfntContainer.ReadUInt16(Data, record), what),
                        SfntContainer.ReadUInt16(Data, record + 2),
                        SfntContainer.ReadUInt16(Data, record + 4),
                        SfntContainer.ReadUInt16(Data, record + 6),
                        (SfntContainer.ReadUInt16(Data, record + 8) & 0x0001) != 0);
                }

                assembly = new MathGlyphAssembly(italicsCorrection, Array.AsReadOnly(parts));
            }

            _assemblies[position] = assembly;
            return assembly;
        }

        /// <summary>
        ///     Checks that a glyph ID names a glyph of the font.
        /// </summary>
        /// <param name="glyph">The glyph ID.</param>
        /// <param name="what">The subtable name, for error messages.</param>
        /// <returns><paramref name="glyph"/>.</returns>
        /// <exception cref="InvalidDataException">Thrown when the glyph is not smaller than the glyph count.</exception>
        private int RequireGlyph(int glyph, string what)
        {
            if (glyph >= _glyphCount)
            {
                throw new InvalidDataException(
                    $"The MATH {what} subtable names glyph {glyph}, but the font has only {_glyphCount} glyphs.");
            }

            return glyph;
        }

        /// <summary>
        ///     Charges the size of a distinct repeated subtable against the work budget.
        /// </summary>
        /// <param name="bytes">The subtable size in bytes.</param>
        /// <exception cref="InvalidDataException">Thrown when the budget is exceeded.</exception>
        private void Charge(long bytes)
        {
            _budget -= bytes;
            if (_budget < 0)
            {
                throw new InvalidDataException(
                    "The MATH table's subtables overlap: together they are larger than the table itself.");
            }
        }
    }
}

/// <summary>
///     The global math layout constants of an OpenType <c>MATH</c> table (the MathConstants
///     subtable), in the order the specification defines them.
/// </summary>
/// <remarks>
///     Values are in font design units, except the three percentages
///     (<see cref="ScriptPercentScaleDown"/>, <see cref="ScriptScriptPercentScaleDown"/>, and
///     <see cref="RadicalDegreeBottomRaisePercent"/>). Device-table adjustments are not applied.
///     Immutable and safe for concurrent use. See the OpenType <c>MATH</c> specification for the
///     layout meaning of each constant.
/// </remarks>
internal sealed record MathConstants
{
    /// <summary>The percentage by which a first-level script is scaled down.</summary>
    public int ScriptPercentScaleDown { get; init; }

    /// <summary>The percentage by which a second-level script is scaled down.</summary>
    public int ScriptScriptPercentScaleDown { get; init; }

    /// <summary>The minimum height of a sub-formula that uses delimiters at their natural size.</summary>
    public int DelimitedSubFormulaMinHeight { get; init; }

    /// <summary>The minimum height of an n-ary operator (such as an integral or summation) in display style.</summary>
    public int DisplayOperatorMinHeight { get; init; }

    /// <summary>The white space between consecutive lines of a multi-line formula.</summary>
    public int MathLeading { get; init; }

    /// <summary>The height of the math axis (the center line of fractions and operators) above the baseline.</summary>
    public int AxisHeight { get; init; }

    /// <summary>The largest base height that an accent can be placed over without being lifted.</summary>
    public int AccentBaseHeight { get; init; }

    /// <summary>The largest base height that an accent can be placed over without flattening.</summary>
    public int FlattenedAccentBaseHeight { get; init; }

    /// <summary>The standard shift down of a subscript.</summary>
    public int SubscriptShiftDown { get; init; }

    /// <summary>The highest the top of a subscript may be.</summary>
    public int SubscriptTopMax { get; init; }

    /// <summary>The minimum drop of a subscript's baseline below the bottom of its base.</summary>
    public int SubscriptBaselineDropMin { get; init; }

    /// <summary>The standard shift up of a superscript.</summary>
    public int SuperscriptShiftUp { get; init; }

    /// <summary>The standard shift up of a superscript in cramped style.</summary>
    public int SuperscriptShiftUpCramped { get; init; }

    /// <summary>The lowest the bottom of a superscript may be.</summary>
    public int SuperscriptBottomMin { get; init; }

    /// <summary>The maximum drop of a superscript's baseline below the top of its base.</summary>
    public int SuperscriptBaselineDropMax { get; init; }

    /// <summary>The minimum gap between a superscript's bottom and a subscript's top.</summary>
    public int SubSuperscriptGapMin { get; init; }

    /// <summary>The highest a superscript's bottom may be raised when closing the sub/superscript gap.</summary>
    public int SuperscriptBottomMaxWithSubscript { get; init; }

    /// <summary>The extra white space after a sub- or superscript.</summary>
    public int SpaceAfterScript { get; init; }

    /// <summary>The minimum gap between an upper limit's bottom and its base's top.</summary>
    public int UpperLimitGapMin { get; init; }

    /// <summary>The minimum rise of an upper limit's baseline above its base's top.</summary>
    public int UpperLimitBaselineRiseMin { get; init; }

    /// <summary>The minimum gap between a lower limit's top and its base's bottom.</summary>
    public int LowerLimitGapMin { get; init; }

    /// <summary>The minimum drop of a lower limit's baseline below its base's bottom.</summary>
    public int LowerLimitBaselineDropMin { get; init; }

    /// <summary>The standard shift up of the top element of a stack.</summary>
    public int StackTopShiftUp { get; init; }

    /// <summary>The standard shift up of the top element of a stack in display style.</summary>
    public int StackTopDisplayStyleShiftUp { get; init; }

    /// <summary>The standard shift down of the bottom element of a stack.</summary>
    public int StackBottomShiftDown { get; init; }

    /// <summary>The standard shift down of the bottom element of a stack in display style.</summary>
    public int StackBottomDisplayStyleShiftDown { get; init; }

    /// <summary>The minimum gap between the elements of a stack.</summary>
    public int StackGapMin { get; init; }

    /// <summary>The minimum gap between the elements of a stack in display style.</summary>
    public int StackDisplayStyleGapMin { get; init; }

    /// <summary>The standard shift up of the top element of a stretch stack.</summary>
    public int StretchStackTopShiftUp { get; init; }

    /// <summary>The standard shift down of the bottom element of a stretch stack.</summary>
    public int StretchStackBottomShiftDown { get; init; }

    /// <summary>The minimum gap between the stretched element and the element above it.</summary>
    public int StretchStackGapAboveMin { get; init; }

    /// <summary>The minimum gap between the stretched element and the element below it.</summary>
    public int StretchStackGapBelowMin { get; init; }

    /// <summary>The standard shift up of a fraction numerator.</summary>
    public int FractionNumeratorShiftUp { get; init; }

    /// <summary>The standard shift up of a fraction numerator in display style.</summary>
    public int FractionNumeratorDisplayStyleShiftUp { get; init; }

    /// <summary>The standard shift down of a fraction denominator.</summary>
    public int FractionDenominatorShiftDown { get; init; }

    /// <summary>The standard shift down of a fraction denominator in display style.</summary>
    public int FractionDenominatorDisplayStyleShiftDown { get; init; }

    /// <summary>The minimum gap between a fraction numerator's bottom and the fraction bar.</summary>
    public int FractionNumeratorGapMin { get; init; }

    /// <summary>The minimum gap between a fraction numerator and the fraction bar in display style.</summary>
    public int FractionNumDisplayStyleGapMin { get; init; }

    /// <summary>The thickness of the fraction bar.</summary>
    public int FractionRuleThickness { get; init; }

    /// <summary>The minimum gap between the fraction bar and a fraction denominator's top.</summary>
    public int FractionDenominatorGapMin { get; init; }

    /// <summary>The minimum gap between the fraction bar and a fraction denominator in display style.</summary>
    public int FractionDenomDisplayStyleGapMin { get; init; }

    /// <summary>The horizontal gap between the elements of a skewed fraction.</summary>
    public int SkewedFractionHorizontalGap { get; init; }

    /// <summary>The vertical shift between the elements of a skewed fraction.</summary>
    public int SkewedFractionVerticalGap { get; init; }

    /// <summary>The gap between an overbar and the top of its base.</summary>
    public int OverbarVerticalGap { get; init; }

    /// <summary>The thickness of an overbar.</summary>
    public int OverbarRuleThickness { get; init; }

    /// <summary>The extra white space reserved above an overbar.</summary>
    public int OverbarExtraAscender { get; init; }

    /// <summary>The gap between an underbar and the bottom of its base.</summary>
    public int UnderbarVerticalGap { get; init; }

    /// <summary>The thickness of an underbar.</summary>
    public int UnderbarRuleThickness { get; init; }

    /// <summary>The extra white space reserved below an underbar.</summary>
    public int UnderbarExtraDescender { get; init; }

    /// <summary>The gap between a radical's overbar and its radicand.</summary>
    public int RadicalVerticalGap { get; init; }

    /// <summary>The gap between a radical's overbar and its radicand in display style.</summary>
    public int RadicalDisplayStyleVerticalGap { get; init; }

    /// <summary>The thickness of a radical's overbar.</summary>
    public int RadicalRuleThickness { get; init; }

    /// <summary>The extra white space reserved above a radical's overbar.</summary>
    public int RadicalExtraAscender { get; init; }

    /// <summary>The extra horizontal kern before a radical's degree.</summary>
    public int RadicalKernBeforeDegree { get; init; }

    /// <summary>The horizontal kern after a radical's degree (usually negative, so the degree overlaps the sign).</summary>
    public int RadicalKernAfterDegree { get; init; }

    /// <summary>The height of a radical degree's bottom, as a percentage of the radical sign's height.</summary>
    public int RadicalDegreeBottomRaisePercent { get; init; }
}

/// <summary>
///     The corner of a glyph whose cut-in kern (MathKernInfoRecord) is requested, in the order of
///     the record's four offsets.
/// </summary>
internal enum MathKernCorner
{
    /// <summary>The top-right corner, used for a superscript.</summary>
    TopRight,

    /// <summary>The top-left corner, used for a prescript superscript.</summary>
    TopLeft,

    /// <summary>The bottom-right corner, used for a subscript.</summary>
    BottomRight,

    /// <summary>The bottom-left corner, used for a prescript subscript.</summary>
    BottomLeft
}

/// <summary>
///     A cut-in kern of one corner of a glyph (a MathKern subtable): a step function of kern
///     values over correction heights.
/// </summary>
/// <remarks>
///     <see cref="KernValues"/> always has one more entry than <see cref="CorrectionHeights"/>:
///     value <c>i</c> applies below height <c>i</c>, and the last value above the last height.
///     Values are in font design units. Immutable.
/// </remarks>
/// <param name="CorrectionHeights">The correction heights, in font design units.</param>
/// <param name="KernValues">The kern values, one more than the heights.</param>
internal sealed record MathKern(IReadOnlyList<int> CorrectionHeights, IReadOnlyList<int> KernValues);

/// <summary>
///     How a glyph is stretched in one direction (a MathGlyphConstruction subtable): ready-made
///     size variants, and optionally an assembly of parts for sizes beyond the largest variant.
/// </summary>
/// <remarks>Immutable.</remarks>
/// <param name="Variants">The size variants, in increasing size (may be empty).</param>
/// <param name="Assembly">The glyph assembly, or <see langword="null"/> when the glyph has none.</param>
internal sealed record MathGlyphConstruction(IReadOnlyList<MathGlyphVariant> Variants, MathGlyphAssembly? Assembly);

/// <summary>
///     One size variant of a stretchable glyph (a MathGlyphVariantRecord).
/// </summary>
/// <param name="Glyph">The variant glyph index (smaller than the font's glyph count).</param>
/// <param name="AdvanceMeasurement">The variant's size in the stretch direction, in font design units.</param>
internal readonly record struct MathGlyphVariant(int Glyph, int AdvanceMeasurement);

/// <summary>
///     The parts from which a stretched glyph of any size is assembled (a GlyphAssembly subtable).
/// </summary>
/// <remarks>Never empty: an assembly with no parts is reported as no assembly. Immutable.</remarks>
/// <param name="ItalicsCorrection">The italic correction of the assembled glyph, in font design units.</param>
/// <param name="Parts">The parts, from bottom to top (vertical) or left to right (horizontal).</param>
internal sealed record MathGlyphAssembly(int ItalicsCorrection, IReadOnlyList<MathGlyphPart> Parts);

/// <summary>
///     One part of a glyph assembly (a GlyphPart record).
/// </summary>
/// <param name="Glyph">The part glyph index (smaller than the font's glyph count).</param>
/// <param name="StartConnectorLength">The length of the connector at the start of the part, in font design units.</param>
/// <param name="EndConnectorLength">The length of the connector at the end of the part, in font design units.</param>
/// <param name="FullAdvance">The full size of the part in the stretch direction, in font design units.</param>
/// <param name="IsExtender">Whether the part may be repeated (or omitted) to reach the requested size.</param>
internal readonly record struct MathGlyphPart(
    int Glyph,
    int StartConnectorLength,
    int EndConnectorLength,
    int FullAdvance,
    bool IsExtender);
