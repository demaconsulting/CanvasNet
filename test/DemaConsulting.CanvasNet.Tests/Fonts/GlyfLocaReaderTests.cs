// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="GlyfLocaReader"/>.
/// </summary>
public class GlyfLocaReaderTests
{
    /// <summary>
    ///     Packs a <c>loca</c> byte array and a set of already-encoded glyph byte arrays into a
    ///     single combined buffer, returning a ready-to-parse <see cref="GlyfLocaReader"/>.
    /// </summary>
    private static GlyfLocaReader BuildReader(IReadOnlyList<byte[]> glyphs, bool longFormat = false)
    {
        var glyphLengths = glyphs.Select(g => g.Length).ToArray();
        var loca = SyntheticFontBuilder.Loca(glyphLengths, longFormat);

        var buf = new List<byte>();
        buf.AddRange(loca);
        var glyfOffset = buf.Count;
        foreach (var glyph in glyphs)
        {
            buf.AddRange(glyph);
        }

        var data = buf.ToArray();
        return GlyfLocaReader.Parse(data, 0, loca.Length, glyfOffset, data.Length - glyfOffset, glyphs.Count, longFormat);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader SimpleGlyph AllOnCurve ProducesLineSegments.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_SimpleGlyph_AllOnCurve_ProducesLineSegments()
    {
        // Arrange: build a simple glyph with a single all-on-curve square contour
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (100, 0, true), (100, 100, true), (0, 100, true)]
        ]);

        // Act: build the reader and get the glyph outline
        var reader = BuildReader([glyph]);
        var path = reader.GetGlyphOutline(0);

        // Assert: a single subpath of straight line segments is produced
        Assert.Single(path.Subpaths);
        var subpath = path.Subpaths[0];
        Assert.Equal(new System.Numerics.Vector2(0, 0), subpath.Start);
        Assert.All(subpath.Commands, c => Assert.True(c.Type is PathCommandType.LineTo or PathCommandType.Close));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader SimpleGlyph ConsecutiveOffCurvePoints ProducesImpliedMidpointQuadratics.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_SimpleGlyph_ConsecutiveOffCurvePoints_ProducesImpliedMidpointQuadratics()
    {
        // Arrange: a four-point "diamond" contour, entirely off-curve, forces the
        // implied-on-curve midpoint convention for every segment (including the synthetic
        // contour start)
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(50, 0, false), (100, 50, false), (50, 100, false), (0, 50, false)]
        ]);

        // Act: build the reader and get the glyph outline
        var reader = BuildReader([glyph]);
        var path = reader.GetGlyphOutline(0);

        // Assert: a single subpath entirely made of quadratic bezier segments is produced
        var subpath = Assert.Single(path.Subpaths);
        Assert.Contains(subpath.Commands, c => c.Type == PathCommandType.QuadraticBezierTo);
        Assert.All(subpath.Commands, c => Assert.True(c.Type is PathCommandType.QuadraticBezierTo or PathCommandType.Close));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader SimpleGlyph MixedOnAndOffCurve ProducesQuadraticToRealOnCurvePoint.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_SimpleGlyph_MixedOnAndOffCurve_ProducesQuadraticToRealOnCurvePoint()
    {
        // Arrange: build a simple glyph alternating on-curve and off-curve points
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (50, 50, false), (100, 0, true), (50, -50, false)]
        ]);

        // Act: build the reader and get the glyph outline
        var reader = BuildReader([glyph]);
        var path = reader.GetGlyphOutline(0);

        // Assert: the subpath starts at the real on-curve point and contains quadratic segments
        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new System.Numerics.Vector2(0, 0), subpath.Start);
        Assert.Contains(subpath.Commands, c => c.Type == PathCommandType.QuadraticBezierTo);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader SimpleGlyph FlagRepeatCountExceedsRemainingPoints
    ///     ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_SimpleGlyph_FlagRepeatCountExceedsRemainingPoints_ThrowsInvalidDataException()
    {
        // Arrange: hand-build a simple glyph with a single 4-point contour whose first flag byte
        // declares a repeat count (10) far exceeding the number of remaining point slots (3) -
        // malformed input that must be rejected rather than silently clipped. Sufficient valid
        // x/y coordinate bytes for all 4 points are appended so that, on the old/pre-fix code
        // (which silently clips the repeat loop to `numPoints`), decoding would fully and
        // successfully complete with no truncation - ensuring any throw can only originate from
        // the repeat-count validation itself, not from running out of coordinate data.
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteInt16(buf, 1); // numberOfContours
        SyntheticFontBuilder.WriteInt16(buf, 0); // xMin
        SyntheticFontBuilder.WriteInt16(buf, 0); // yMin
        SyntheticFontBuilder.WriteInt16(buf, 0); // xMax
        SyntheticFontBuilder.WriteInt16(buf, 0); // yMax
        SyntheticFontBuilder.WriteUInt16(buf, 3); // endPts[0] -> numPoints = 4
        SyntheticFontBuilder.WriteUInt16(buf, 0); // instructionLength
        buf.Add(0x08); // flag: REPEAT_FLAG set (neither X/Y-short nor X/Y-same bits set)
        buf.Add(10); // repeatCount: far exceeds the 3 remaining point slots

        // Trailing coordinate data: 4 points, each needing a 2-byte x delta followed (after all
        // x deltas) by a 2-byte y delta, per the flag byte above - enough for the old/pre-fix
        // code to decode the whole glyph without hitting a truncation-caused throw.
        for (var p = 0; p < 4; p++)
        {
            SyntheticFontBuilder.WriteInt16(buf, 10); // x delta
        }

        for (var p = 0; p < 4; p++)
        {
            SyntheticFontBuilder.WriteInt16(buf, 10); // y delta
        }

        var glyph = buf.ToArray();

        // Act: build the reader over the malformed glyph
        var reader = BuildReader([glyph]);

        // Assert: decoding the malformed glyph throws specifically because of the repeat-count
        // validation - not merely any InvalidDataException, which could also arise from
        // truncated coordinate data
        var exception = Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
        Assert.Contains("repeat count", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader EmptyGlyph ZeroContours ProducesEmptyPath.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_EmptyGlyph_ZeroContours_ProducesEmptyPath()
    {
        // Arrange: build a simple glyph with zero contours
        var glyph = SyntheticFontBuilder.SimpleGlyph();

        // Act: build the reader and get the glyph outline
        var reader = BuildReader([glyph]);
        var path = reader.GetGlyphOutline(0);

        // Assert: the resulting path has no subpaths
        Assert.Empty(path.Subpaths);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader ZeroLengthGlyph ProducesEmptyPath.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_ZeroLengthGlyph_ProducesEmptyPath()
    {
        // Arrange: a loca entry pair with equal offsets (e.g. the classic 'space' glyph with no
        // outline) alongside a normal glyph
        var reader = BuildReader([[], SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]])]);

        // Act: get the outline of the zero-length glyph
        var path = reader.GetGlyphOutline(0);

        // Assert: the resulting path has no subpaths
        Assert.Empty(path.Subpaths);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader LongLocaFormat DecodesSameAsShortFormat.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_LongLocaFormat_DecodesSameAsShortFormat()
    {
        // Arrange: build a simple glyph and a reader using the long loca format
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);

        // Act: build the reader with long-format loca and get the glyph outline
        var reader = BuildReader([glyph], longFormat: true);
        var path = reader.GetGlyphOutline(0);

        // Assert: the outline decodes correctly, same as with the short format
        Assert.Single(path.Subpaths);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader GetGlyphOutline NegativeGlyphIndex ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_GetGlyphOutline_NegativeGlyphIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: build a reader over a single glyph
        var reader = BuildReader([SyntheticFontBuilder.SimpleGlyph()]);

        // Act/Assert: requesting a negative glyph index throws
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetGlyphOutline(-1));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader GetGlyphOutline GlyphIndexTooLarge ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_GetGlyphOutline_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: build a reader over a single glyph
        var reader = BuildReader([SyntheticFontBuilder.SimpleGlyph()]);

        // Act/Assert: requesting a glyph index at/beyond the glyph count throws
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetGlyphOutline(1));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph SingleComponent ProducesTranslatedOutline.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_SingleComponent_ProducesTranslatedOutline()
    {
        // Arrange: build a square glyph and a composite referencing it with a translation
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 100, 200)
        ]);

        // Act: build the reader and get the composite glyph outline
        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

        // Assert: the outline is translated by the component's dx/dy offset
        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new System.Numerics.Vector2(100, 200), subpath.Start);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph ScaledComponent ScalesOutline.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_ScaledComponent_ScalesOutline()
    {
        // Arrange: build a square glyph and a composite referencing it with a uniform scale
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 0, 0, HasScale: true, A: 1.5f, D: 1.5f)
        ]);

        // Act: build the reader and get the composite glyph outline
        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

        // Assert: the outline coordinates are scaled by the component's scale factor
        var subpath = Assert.Single(path.Subpaths);
        var lineTo = subpath.Commands[0];
        Assert.Equal(PathCommandType.LineTo, lineTo.Type);
        Assert.Equal(15f, lineTo.EndPoint.X, 3);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph TwoByTwoTransform TransformsOutline.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_TwoByTwoTransform_TransformsOutline()
    {
        // Arrange: build a square glyph and a composite applying a 90-degree counter-clockwise
        // rotation matrix: a=0, b=1, c=-1, d=0 maps (x, y) -> (-y, x)
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 0, 0, HasTwoByTwo: true, A: 0f, B: 1f, C: -1f, D: 0f)
        ]);

        // Act: build the reader and get the composite glyph outline
        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

        // Assert: the first LineTo draws to the untransformed point (10, 0), which the rotation maps to (0, 10)
        var subpath = Assert.Single(path.Subpaths);
        var lineTo = subpath.Commands[0];
        Assert.Equal(0f, lineTo.EndPoint.X, 2);
        Assert.Equal(10f, lineTo.EndPoint.Y, 2);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph ScaledComponentOffset TransformsTranslation.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_ScaledComponentOffset_TransformsTranslation()
    {
        // Arrange: build a square glyph and a composite applying a 90-degree counter-clockwise
        // rotation matrix (a=0, b=1, c=-1, d=0) together with SCALED_COMPONENT_OFFSET and a
        // dx/dy translation of (10, 0). Per the OpenType/TrueType spec, SCALED_COMPONENT_OFFSET
        // requires the translation itself to be transformed through the component's own matrix
        // before being applied: (dx', dy') = (a*dx + c*dy, b*dx + d*dy) = (0*10 + -1*0, 1*10 + 0*0)
        // = (0, 10) - not the untransformed (10, 0) that unscaled-offset semantics would apply.
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(
                0, 10, 0, HasTwoByTwo: true, A: 0f, B: 1f, C: -1f, D: 0f, ScaledComponentOffset: true)
        ]);

        // Act: build the reader and get the composite glyph outline
        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

        // Assert: the outline's start point is the component's own matrix applied to the
        // translation - (0, 10) - rather than the unscaled (10, 0) offset
        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(0f, subpath.Start.X, 2);
        Assert.Equal(10f, subpath.Start.Y, 2);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph NestedComposite ResolvesRecursively.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_NestedComposite_ResolvesRecursively()
    {
        // Arrange: build a square glyph, an inner composite referencing it, and an outer
        // composite referencing the inner composite
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var inner = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 5, 5)
        ]);
        var outer = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(1, 100, 100)
        ]);

        // Act: build the reader and get the outer composite glyph outline
        var reader = BuildReader([square, inner, outer]);
        var path = reader.GetGlyphOutline(2);

        // Assert: both levels of translation are combined into the final outline
        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new System.Numerics.Vector2(105, 105), subpath.Start);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph MultipleComponents ProducesMultipleSubpaths.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_MultipleComponents_ProducesMultipleSubpaths()
    {
        // Arrange: build a square glyph and a composite referencing it twice at different offsets
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 0, 0),
            new SyntheticFontBuilder.CompositeComponent(0, 50, 50)
        ]);

        // Act: build the reader and get the composite glyph outline
        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

        // Assert: each component contributes its own subpath
        Assert.Equal(2, path.Subpaths.Count);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph PointMatchedComponent ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_PointMatchedComponent_ThrowsInvalidDataException()
    {
        // Arrange: build a square glyph and a composite component using point-matching
        // (unsupported) instead of x/y offsets
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 0, 0, ArgsAreXyValues: false)
        ]);

        // Act
        var reader = BuildReader([square, composite]);

        // Assert: resolving the point-matched component throws
        Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(1));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph OutOfRangeComponentGlyphIndex ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_OutOfRangeComponentGlyphIndex_ThrowsInvalidDataException()
    {
        // Arrange: build a composite whose sole component references a nonexistent glyph index
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(99, 0, 0)
        ]);

        // Act
        var reader = BuildReader([composite]);

        // Assert: resolving the out-of-range component glyph index throws
        Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph SelfReferentialCycle ThrowsInvalidDataExceptionViaDepthCap.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_SelfReferentialCycle_ThrowsInvalidDataExceptionViaDepthCap()
    {
        // Arrange: glyph 0 is a composite whose only component is itself: recursion depth
        // increases without bound until the deterministic depth cap trips - this must never hang
        var selfReferential = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 1, 1)
        ]);

        // Act
        var reader = BuildReader([selfReferential]);

        // Assert: the depth cap trips and the message identifies the depth-cap failure
        var ex = Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph ExponentialBlowUp ThrowsInvalidDataExceptionViaComponentCap.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_ExponentialBlowUp_ThrowsInvalidDataExceptionViaComponentCap()
    {
        // Arrange: a 4-level chain, each with 10 components referencing the next level
        // (branching factor 10, depth 4 <= MaxDepth): total resolved components is on the order
        // of 10 + 100 + 1000 + 10000, far exceeding the 5000 total-component cap, while nesting
        // depth never approaches the separate depth cap - proving the component-count cap (not
        // the depth cap) is what prevents the hang here
        var leaf = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (1, 0, true), (1, 1, true)]]);

        byte[] MakeLevel(int referencedGlyphIndex) =>
            SyntheticFontBuilder.CompositeGlyph(
                Enumerable.Range(0, 10)
                    .Select(i => new SyntheticFontBuilder.CompositeComponent(referencedGlyphIndex, i, i))
                    .ToArray());

        var level3 = MakeLevel(0); // glyph 1: 10 refs to leaf (glyph 0)
        var level2 = MakeLevel(1); // glyph 2: 10 refs to glyph 1
        var level1 = MakeLevel(2); // glyph 3: 10 refs to glyph 2
        var level0 = MakeLevel(3); // glyph 4: 10 refs to glyph 3 (the top-level glyph under test)

        // Act
        var reader = BuildReader([leaf, level3, level2, level1, level0]);

        // Assert: the component-count cap trips and the message identifies the count-cap failure
        var ex = Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(4));
        Assert.Contains("too many", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader Parse LocaEntryExceedsGlyfBounds ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_Parse_LocaEntryExceedsGlyfBounds_ThrowsInvalidDataException()
    {
        // Arrange: build a loca table whose single glyph length exceeds the supplied glyf region
        var loca = SyntheticFontBuilder.Loca([100], longFormat: false);
        var data = new byte[loca.Length + 10];
        loca.CopyTo(data, 0);

        // Act/Assert: parsing with the out-of-bounds loca entry throws
        Assert.Throws<InvalidDataException>(() => GlyfLocaReader.Parse(data, 0, loca.Length, loca.Length, 10, 1, false));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader Parse NonMonotonicLocaEntries ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_Parse_NonMonotonicLocaEntries_ThrowsInvalidDataException()
    {
        // Arrange: build a loca table whose entries decrease instead of monotonically increasing
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // glyph 0: offset 0
        SyntheticFontBuilder.WriteUInt16(buf, 5); // glyph 1 end / glyph 2 start: offset 10
        SyntheticFontBuilder.WriteUInt16(buf, 3); // glyph 2 end: offset 6 - decreases from 10
        var loca = buf.ToArray();
        var data = new byte[loca.Length + 10];
        loca.CopyTo(data, 0);

        // Act/Assert: parsing the non-monotonic loca table throws
        Assert.Throws<InvalidDataException>(() => GlyfLocaReader.Parse(data, 0, loca.Length, loca.Length, 10, 2, false));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader Parse TruncatedLoca ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_Parse_TruncatedLoca_ThrowsInvalidDataException()
    {
        // Arrange: build a loca buffer shorter than the (numGlyphs + 1) * 2 = 4 bytes required
        var loca = new byte[2]; // needs (numGlyphs + 1) * 2 = 4 bytes for numGlyphs=1

        // Act/Assert: parsing the truncated loca table throws
        Assert.Throws<InvalidDataException>(() => GlyfLocaReader.Parse(loca, 0, loca.Length, 2, 0, 1, false));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader TruncatedGlyphHeader ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_TruncatedGlyphHeader_ThrowsInvalidDataException()
    {
        // Arrange: build a reader over a glyph shorter than the 10-byte glyph header
        var reader = BuildReader([new byte[5]]); // shorter than the 10-byte glyph header

        // Act/Assert: reading the truncated glyph header throws
        Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader InvalidContourCount ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_InvalidContourCount_ThrowsInvalidDataException()
    {
        // Arrange: build a glyph header with an invalid contour count (only -1 or >=0 are valid)
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteInt16(buf, -2); // invalid: only -1 (composite) or >=0 are valid
        for (var i = 0; i < 4; i++)
        {
            SyntheticFontBuilder.WriteInt16(buf, 0);
        }

        var glyph = buf.ToArray();
        var reader = BuildReader([glyph]);

        // Act/Assert: reading the glyph with the invalid contour count throws
        Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
    }
}
