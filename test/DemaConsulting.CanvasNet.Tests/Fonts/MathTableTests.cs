using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;
using static DemaConsulting.CanvasNet.Tests.TestSupport.SyntheticFontBuilder;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for the internal <see cref="MathTable"/> reader of the <see cref="TrueTypeFont"/>
///     unit, against synthetic <c>MATH</c> tables built by <see cref="SyntheticFontBuilder.MathTableBytes"/>.
/// </summary>
public class MathTableTests
{
    /// <summary>
    ///     The number of junk bytes placed before every synthetic table, so the reader is proven to
    ///     resolve offsets from the table start rather than from the start of the font data.
    /// </summary>
    private const int Prefix = 7;

    /// <summary>
    ///     Parses a table placed after <see cref="Prefix"/> junk bytes.
    /// </summary>
    private static MathTable ParseAtOffset(byte[] table, int glyphCount = 100)
    {
        var data = new byte[Prefix + table.Length];
        Array.Fill(data, (byte)0xEE, 0, Prefix);
        table.CopyTo(data, Prefix);
        return MathTable.Parse(data, Prefix, table.Length, glyphCount);
    }

    /// <summary>
    ///     Builds and parses a table from a spec.
    /// </summary>
    private static MathTable Parse(MathTableSpec spec, int glyphCount = 100) =>
        ParseAtOffset(SyntheticFontBuilder.MathTableBytes(spec), glyphCount);

    /// <summary>
    ///     Proves that all 56 MathConstants values are read, each into its own property, in the
    ///     OpenType order, including negative MathValueRecord values.
    /// </summary>
    [Fact]
    public void MathTable_Parse_ValidTable_ReadsEveryConstantInOrder()
    {
        // Arrange: 56 distinct values, with a negative RadicalKernAfterDegree
        var expected = Enumerable.Range(0, 56).Select(i => 10 + i * 7).ToArray();
        expected[54] = -450;

        // Act
        var c = Parse(new MathTableSpec { Constants = expected }).Constants;

        // Assert: every property holds the value at its OpenType position
        int[] actual =
        [
            c.ScriptPercentScaleDown, c.ScriptScriptPercentScaleDown, c.DelimitedSubFormulaMinHeight,
            c.DisplayOperatorMinHeight, c.MathLeading, c.AxisHeight, c.AccentBaseHeight, c.FlattenedAccentBaseHeight,
            c.SubscriptShiftDown, c.SubscriptTopMax, c.SubscriptBaselineDropMin, c.SuperscriptShiftUp,
            c.SuperscriptShiftUpCramped, c.SuperscriptBottomMin, c.SuperscriptBaselineDropMax, c.SubSuperscriptGapMin,
            c.SuperscriptBottomMaxWithSubscript, c.SpaceAfterScript, c.UpperLimitGapMin, c.UpperLimitBaselineRiseMin,
            c.LowerLimitGapMin, c.LowerLimitBaselineDropMin, c.StackTopShiftUp, c.StackTopDisplayStyleShiftUp,
            c.StackBottomShiftDown, c.StackBottomDisplayStyleShiftDown, c.StackGapMin, c.StackDisplayStyleGapMin,
            c.StretchStackTopShiftUp, c.StretchStackBottomShiftDown, c.StretchStackGapAboveMin, c.StretchStackGapBelowMin,
            c.FractionNumeratorShiftUp, c.FractionNumeratorDisplayStyleShiftUp, c.FractionDenominatorShiftDown,
            c.FractionDenominatorDisplayStyleShiftDown, c.FractionNumeratorGapMin, c.FractionNumDisplayStyleGapMin,
            c.FractionRuleThickness, c.FractionDenominatorGapMin, c.FractionDenomDisplayStyleGapMin,
            c.SkewedFractionHorizontalGap, c.SkewedFractionVerticalGap, c.OverbarVerticalGap, c.OverbarRuleThickness,
            c.OverbarExtraAscender, c.UnderbarVerticalGap, c.UnderbarRuleThickness, c.UnderbarExtraDescender,
            c.RadicalVerticalGap, c.RadicalDisplayStyleVerticalGap, c.RadicalRuleThickness, c.RadicalExtraAscender,
            c.RadicalKernBeforeDegree, c.RadicalKernAfterDegree, c.RadicalDegreeBottomRaisePercent
        ];
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Proves that a table whose major version is not 1 is rejected.
    /// </summary>
    [Fact]
    public void MathTable_Parse_UnsupportedMajorVersion_ThrowsInvalidDataException()
    {
        // Arrange: a valid table with major version 2
        var table = SyntheticFontBuilder.MathTableBytes(new MathTableSpec());
        table[1] = 2;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(table));
    }

    /// <summary>
    ///     Proves that a table shorter than its 10-byte header is rejected.
    /// </summary>
    [Fact]
    public void MathTable_Parse_TableShorterThanHeader_ThrowsInvalidDataException()
    {
        // Arrange: the first 6 bytes of a valid table
        var table = SyntheticFontBuilder.MathTableBytes(new MathTableSpec())[..6];

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(table));
    }

    /// <summary>
    ///     Proves that a table too short to hold its 214-byte MathConstants is rejected, as is a
    ///     table with no MathConstants at all.
    /// </summary>
    [Fact]
    public void MathTable_Parse_ConstantsBeyondTable_ThrowsInvalidDataException()
    {
        // Arrange: a constants-only table truncated to 100 bytes, and one with a zero constants offset
        var truncated = SyntheticFontBuilder.MathTableBytes(new MathTableSpec { OmitGlyphInfo = true, OmitVariants = true })[..100];
        var noConstants = SyntheticFontBuilder.MathTableBytes(new MathTableSpec { OmitGlyphInfo = true, OmitVariants = true });
        noConstants[4] = 0;
        noConstants[5] = 0;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(truncated));
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(noConstants));
    }

    /// <summary>
    ///     Proves that a table with no MathGlyphInfo and no MathVariants reads as having no
    ///     per-glyph information, without throwing.
    /// </summary>
    [Fact]
    public void MathTable_Parse_AbsentGlyphInfoAndVariants_ReadsEmptyInformation()
    {
        // Arrange / Act: a constants-only table
        var table = Parse(new MathTableSpec { OmitGlyphInfo = true, OmitVariants = true });

        // Assert: every per-glyph query reports nothing
        Assert.False(table.TryGetItalicsCorrection(1, out var italic));
        Assert.Equal(0, italic);
        Assert.False(table.TryGetTopAccentAttachment(1, out _));
        Assert.False(table.IsExtendedShape(1));
        Assert.False(table.TryGetMathKern(1, MathKernCorner.TopRight, out _));
        Assert.Null(table.GetVerticalConstruction(1));
        Assert.Null(table.GetHorizontalConstruction(1));
        Assert.Equal(0, table.MinConnectorOverlap);
    }

    /// <summary>
    ///     Proves that a covered glyph's italic correction is returned, including a negative one.
    /// </summary>
    [Fact]
    public void MathTable_TryGetItalicsCorrection_CoveredGlyph_ReturnsValue()
    {
        // Arrange: italic corrections in a format-1 coverage, listed out of glyph order
        var table = Parse(new MathTableSpec { ItalicsCorrections = [(7, 145), (3, -20)] });

        // Act
        var found7 = table.TryGetItalicsCorrection(7, out var value7);
        var found3 = table.TryGetItalicsCorrection(3, out var value3);

        // Assert
        Assert.True(found7);
        Assert.Equal(145, value7);
        Assert.True(found3);
        Assert.Equal(-20, value3);
    }

    /// <summary>
    ///     Proves that an uncovered glyph has no italic correction.
    /// </summary>
    [Fact]
    public void MathTable_TryGetItalicsCorrection_UncoveredGlyph_ReturnsFalse()
    {
        // Arrange
        var table = Parse(new MathTableSpec { ItalicsCorrections = [(7, 145)] });

        // Act
        var found = table.TryGetItalicsCorrection(8, out var value);

        // Assert
        Assert.False(found);
        Assert.Equal(0, value);
    }

    /// <summary>
    ///     Proves that top-accent attachments are found through a format-2 (range) coverage.
    /// </summary>
    [Fact]
    public void MathTable_TryGetTopAccentAttachment_RangeCoverage_ReturnsValue()
    {
        // Arrange: glyphs 10-12 and 20 in a format-2 coverage
        var table = Parse(new MathTableSpec
        {
            TopAccentAttachments = [(10, 100), (11, 110), (12, 120), (20, 200)],
            TopAccentCoverageFormat = 2
        });

        // Act / Assert
        Assert.True(table.TryGetTopAccentAttachment(11, out var value11));
        Assert.Equal(110, value11);
        Assert.True(table.TryGetTopAccentAttachment(20, out var value20));
        Assert.Equal(200, value20);
        Assert.False(table.TryGetTopAccentAttachment(13, out _));
    }

    /// <summary>
    ///     Proves that covered glyphs are extended shapes and uncovered glyphs are not, in both
    ///     coverage formats.
    /// </summary>
    [Fact]
    public void MathTable_IsExtendedShape_CoveredAndUncoveredGlyphs()
    {
        // Arrange: the same glyphs in a format-1 and a format-2 coverage
        var format1 = Parse(new MathTableSpec { ExtendedShapes = [4, 5, 6], ExtendedShapeCoverageFormat = 1 });
        var format2 = Parse(new MathTableSpec { ExtendedShapes = [4, 5, 6], ExtendedShapeCoverageFormat = 2 });

        // Act / Assert
        Assert.True(format1.IsExtendedShape(5));
        Assert.False(format1.IsExtendedShape(7));
        Assert.True(format2.IsExtendedShape(4));
        Assert.False(format2.IsExtendedShape(3));
    }

    /// <summary>
    ///     Proves that a corner's cut-in kern is returned with its heights and its one-longer
    ///     list of kern values.
    /// </summary>
    [Fact]
    public void MathTable_TryGetMathKern_PresentCorner_ReturnsHeightsAndValues()
    {
        // Arrange: glyph 9 with a top-right and a bottom-left kern
        var table = Parse(new MathTableSpec
        {
            Kerns =
            [
                new MathKernInfoSpec(
                    9,
                    new MathKernSpec([100, 200], [10, 20, -30]),
                    BottomLeft: new MathKernSpec([], [5]))
            ]
        });

        // Act
        var foundTopRight = table.TryGetMathKern(9, MathKernCorner.TopRight, out var topRight);
        var foundBottomLeft = table.TryGetMathKern(9, MathKernCorner.BottomLeft, out var bottomLeft);

        // Assert
        Assert.True(foundTopRight);
        Assert.Equal([100, 200], topRight!.CorrectionHeights);
        Assert.Equal([10, 20, -30], topRight.KernValues);
        Assert.True(foundBottomLeft);
        Assert.Empty(bottomLeft!.CorrectionHeights);
        Assert.Equal([5], bottomLeft.KernValues);
    }

    /// <summary>
    ///     Proves that a corner with no kern, and an uncovered glyph, report no kern.
    /// </summary>
    [Fact]
    public void MathTable_TryGetMathKern_AbsentCorner_ReturnsFalse()
    {
        // Arrange: glyph 9 with only a top-right kern
        var table = Parse(new MathTableSpec { Kerns = [new MathKernInfoSpec(9, new MathKernSpec([100], [1, 2]))] });

        // Act
        var foundTopLeft = table.TryGetMathKern(9, MathKernCorner.TopLeft, out var topLeft);
        var foundUncovered = table.TryGetMathKern(10, MathKernCorner.TopRight, out var uncovered);

        // Assert
        Assert.False(foundTopLeft);
        Assert.Null(topLeft);
        Assert.False(foundUncovered);
        Assert.Null(uncovered);
    }

    /// <summary>
    ///     Proves that a vertical construction is returned with its variants, its assembly's
    ///     italic correction, and its parts with their connector lengths and extender flags, and
    ///     that MinConnectorOverlap is read.
    /// </summary>
    [Fact]
    public void MathTable_GetVerticalConstruction_VariantsAndAssembly_ReturnsPartsWithExtenderFlags()
    {
        // Arrange: glyph 1 with two variants and a bottom/extender/top assembly
        var table = Parse(new MathTableSpec
        {
            MinConnectorOverlap = 100,
            Vertical =
            [
                new MathConstructionSpec(
                    1,
                    [(1, 900), (2, 1200)],
                    new MathAssemblySpec(15, [(3, 0, 100, 500, false), (4, 100, 100, 300, true), (5, 100, 0, 500, false)]))
            ]
        });

        // Act
        var construction = table.GetVerticalConstruction(1);

        // Assert
        Assert.NotNull(construction);
        Assert.Equal(100, table.MinConnectorOverlap);
        Assert.Equal([new MathGlyphVariant(1, 900), new MathGlyphVariant(2, 1200)], construction.Variants);
        Assert.NotNull(construction.Assembly);
        Assert.Equal(15, construction.Assembly.ItalicsCorrection);
        Assert.Equal(
            [new MathGlyphPart(3, 0, 100, 500, false), new MathGlyphPart(4, 100, 100, 300, true), new MathGlyphPart(5, 100, 0, 500, false)],
            construction.Assembly.Parts);
    }

    /// <summary>
    ///     Proves that horizontal constructions are separate from vertical ones and that an
    ///     uncovered glyph has none.
    /// </summary>
    [Fact]
    public void MathTable_GetHorizontalConstruction_UncoveredGlyph_ReturnsNull()
    {
        // Arrange: glyph 6 stretches horizontally only
        var table = Parse(new MathTableSpec { Horizontal = [new MathConstructionSpec(6, [(6, 500), (7, 800)])] });

        // Act
        var covered = table.GetHorizontalConstruction(6);
        var uncovered = table.GetHorizontalConstruction(7);
        var vertical = table.GetVerticalConstruction(6);

        // Assert
        Assert.NotNull(covered);
        Assert.Equal(2, covered.Variants.Count);
        Assert.Null(uncovered);
        Assert.Null(vertical);
    }

    /// <summary>
    ///     Proves that a construction without an assembly, and one whose assembly has no parts,
    ///     both return their variants with no assembly.
    /// </summary>
    [Fact]
    public void MathTable_GetVerticalConstruction_NoAssembly_ReturnsVariantsOnly()
    {
        // Arrange: glyph 2 has no assembly; glyph 3 has an assembly with zero parts
        var table = Parse(new MathTableSpec
        {
            Vertical =
            [
                new MathConstructionSpec(2, [(2, 700)]),
                new MathConstructionSpec(3, [(3, 750)], new MathAssemblySpec(0, []))
            ]
        });

        // Act
        var noAssembly = table.GetVerticalConstruction(2);
        var emptyAssembly = table.GetVerticalConstruction(3);

        // Assert
        Assert.NotNull(noAssembly);
        Assert.Equal([new MathGlyphVariant(2, 700)], noAssembly.Variants);
        Assert.Null(noAssembly.Assembly);
        Assert.NotNull(emptyAssembly);
        Assert.Null(emptyAssembly.Assembly);
    }

    /// <summary>
    ///     Proves that a construction subtable shared by two glyphs is read once and returned for
    ///     both.
    /// </summary>
    [Fact]
    public void MathTable_GetVerticalConstruction_SharedSubtable_ReturnsSameConstruction()
    {
        // Arrange: two vertical constructions, then point the second offset at the first construction
        var table = SyntheticFontBuilder.MathTableBytes(new MathTableSpec
        {
            OmitGlyphInfo = true,
            Vertical = [new MathConstructionSpec(1, [(1, 900)]), new MathConstructionSpec(2, [(2, 950)])]
        });
        var variants = (table[8] << 8) | table[9];
        table[variants + 12] = table[variants + 10];
        table[variants + 13] = table[variants + 11];

        // Act
        var parsed = ParseAtOffset(table);

        // Assert: both glyphs get the same construction instance
        Assert.Same(parsed.GetVerticalConstruction(1), parsed.GetVerticalConstruction(2));
    }

    /// <summary>
    ///     Proves that a coverage that indexes past its record count is rejected.
    /// </summary>
    [Fact]
    public void MathTable_Parse_CoverageIndexBeyondCount_ThrowsInvalidDataException()
    {
        // Arrange: two covered glyphs, but a declared count of one
        var table = SyntheticFontBuilder.MathTableBytes(new MathTableSpec { ItalicsCorrections = [(1, 1), (2, 2)], ItalicsCountOverride = 1 });

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(table));
    }

    /// <summary>
    ///     Proves that a construction whose variant count does not fit in the table is rejected.
    /// </summary>
    [Fact]
    public void MathTable_Parse_VariantCountBeyondTable_ThrowsInvalidDataException()
    {
        // Arrange
        var table = SyntheticFontBuilder.MathTableBytes(new MathTableSpec
        {
            Vertical = [new MathConstructionSpec(1, [(1, 900)]) { VariantCountOverride = 0xFFFF }]
        });

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(table));
    }

    /// <summary>
    ///     Proves that an assembly whose part count does not fit in the table is rejected.
    /// </summary>
    [Fact]
    public void MathTable_Parse_PartCountBeyondTable_ThrowsInvalidDataException()
    {
        // Arrange
        var table = SyntheticFontBuilder.MathTableBytes(new MathTableSpec
        {
            Vertical =
            [
                new MathConstructionSpec(1, [(1, 900)], new MathAssemblySpec(0, [(3, 0, 0, 500, false)]) { PartCountOverride = 0xFFFF })
            ]
        });

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(table));
    }

    /// <summary>
    ///     Proves that a variant or part naming a glyph the font does not have is rejected.
    /// </summary>
    [Fact]
    public void MathTable_Parse_GlyphIdBeyondGlyphCount_ThrowsInvalidDataException()
    {
        // Arrange: a variant glyph 50 and a part glyph 60, in fonts of 10 glyphs
        var variant = SyntheticFontBuilder.MathTableBytes(new MathTableSpec { Vertical = [new MathConstructionSpec(1, [(50, 900)])] });
        var part = SyntheticFontBuilder.MathTableBytes(new MathTableSpec
        {
            Vertical = [new MathConstructionSpec(1, [], new MathAssemblySpec(0, [(60, 0, 0, 500, true)]))]
        });

        // Act / Assert: rejected for 10 glyphs, accepted for 100
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(variant, glyphCount: 10));
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(part, glyphCount: 10));
        Assert.NotNull(ParseAtOffset(variant, glyphCount: 100).GetVerticalConstruction(1));
    }

    /// <summary>
    ///     Proves that maximal (0xFFFF) offsets are bounds-checked without wrapping: a header-only
    ///     table and a full table whose glyph-info and variants offsets are 0xFFFF are rejected.
    /// </summary>
    [Fact]
    public void MathTable_Parse_MaximalOffsets_DoNotOverflow()
    {
        // Arrange: every header offset 0xFFFF in a 10-byte table, and maximal subtable offsets in a full one
        byte[] headerOnly = [0, 1, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
        var full = SyntheticFontBuilder.MathTableBytes(new MathTableSpec { ItalicsCorrections = [(1, 1)], Vertical = [new MathConstructionSpec(1, [(1, 1)])] });
        full[6] = 0xFF;
        full[7] = 0xFF;
        var variantsOnly = SyntheticFontBuilder.MathTableBytes(new MathTableSpec { Vertical = [new MathConstructionSpec(1, [(1, 1)])] });
        variantsOnly[8] = 0xFF;
        variantsOnly[9] = 0xFF;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(headerOnly));
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(full));
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(variantsOnly));
        Assert.Throws<InvalidDataException>(() => MathTable.Parse(headerOnly, int.MaxValue - 4, 10, 100));
    }

    /// <summary>
    ///     Proves that a MathKern whose height count does not fit in the table is rejected.
    /// </summary>
    [Fact]
    public void MathTable_Parse_KernCountBeyondTable_ThrowsInvalidDataException()
    {
        // Arrange
        var table = SyntheticFontBuilder.MathTableBytes(new MathTableSpec
        {
            Kerns = [new MathKernInfoSpec(9, new MathKernSpec([100], [1, 2]) { HeightCountOverride = 0xFFFF })]
        });

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ParseAtOffset(table));
    }

    /// <summary>
    ///     Proves that kern offsets made to overlap - each glyph's kern starting two bytes after
    ///     the previous one, so the same bytes are read again and again - are rejected once the
    ///     subtables read add up to more than the table, keeping the work linear in its size.
    /// </summary>
    [Fact]
    public void MathTable_Parse_OverlappingKernSubtables_ThrowsInvalidDataException()
    {
        // Arrange: MathGlyphInfo (at 224) whose MathKernInfo (at 232) covers 200 glyphs, each
        // top-right offset pointing two bytes further into one region of repeated height counts
        // of 16, where every position starts a valid 134-byte MathKern
        const int glyphs = 200;
        var buf = new List<byte>();
        foreach (var value in new[] { 1, 0, 10, 224, 0 })
        {
            WriteUInt16(buf, value);
        }

        buf.AddRange(new byte[214]);
        foreach (var value in new[] { 0, 0, 0, 8 })
        {
            WriteUInt16(buf, value);
        }

        var kernInfo = buf.Count;
        var coverageOffset = 4 + glyphs * 8;
        var regionOffset = coverageOffset + 4 + glyphs * 2;
        WriteUInt16(buf, coverageOffset);
        WriteUInt16(buf, glyphs);
        for (var i = 0; i < glyphs; i++)
        {
            WriteUInt16(buf, regionOffset + i * 2);
            WriteUInt16(buf, 0);
            WriteUInt16(buf, 0);
            WriteUInt16(buf, 0);
        }

        buf.AddRange(Coverage(Enumerable.Range(1, glyphs), 1));
        Assert.Equal(kernInfo + regionOffset, buf.Count);
        for (var i = 0; i < glyphs + 70; i++)
        {
            WriteUInt16(buf, 16);
        }

        // Act / Assert: each kern alone is in bounds, but together they exceed the table
        Assert.Contains("overlap", Assert.Throws<InvalidDataException>(() => ParseAtOffset([.. buf], glyphCount: 1000)).Message, StringComparison.Ordinal);
    }
}
