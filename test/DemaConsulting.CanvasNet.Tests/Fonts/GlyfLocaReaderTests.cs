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
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (100, 0, true), (100, 100, true), (0, 100, true)]
        ]);

        var reader = BuildReader([glyph]);
        var path = reader.GetGlyphOutline(0);

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
        // A four-point "diamond" contour, entirely off-curve, forces the implied-on-curve
        // midpoint convention for every segment (including the synthetic contour start).
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(50, 0, false), (100, 50, false), (50, 100, false), (0, 50, false)]
        ]);

        var reader = BuildReader([glyph]);
        var path = reader.GetGlyphOutline(0);

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
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (50, 50, false), (100, 0, true), (50, -50, false)]
        ]);

        var reader = BuildReader([glyph]);
        var path = reader.GetGlyphOutline(0);

        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new System.Numerics.Vector2(0, 0), subpath.Start);
        Assert.Contains(subpath.Commands, c => c.Type == PathCommandType.QuadraticBezierTo);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader EmptyGlyph ZeroContours ProducesEmptyPath.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_EmptyGlyph_ZeroContours_ProducesEmptyPath()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph();

        var reader = BuildReader([glyph]);
        var path = reader.GetGlyphOutline(0);

        Assert.Empty(path.Subpaths);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader ZeroLengthGlyph ProducesEmptyPath.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_ZeroLengthGlyph_ProducesEmptyPath()
    {
        // A loca entry pair with equal offsets (e.g. the classic 'space' glyph with no outline).
        var reader = BuildReader([[], SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]])]);

        var path = reader.GetGlyphOutline(0);

        Assert.Empty(path.Subpaths);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader LongLocaFormat DecodesSameAsShortFormat.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_LongLocaFormat_DecodesSameAsShortFormat()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph([[(0, 0, true), (10, 0, true), (10, 10, true)]]);

        var reader = BuildReader([glyph], longFormat: true);
        var path = reader.GetGlyphOutline(0);

        Assert.Single(path.Subpaths);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader GetGlyphOutline NegativeGlyphIndex ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_GetGlyphOutline_NegativeGlyphIndex_ThrowsArgumentOutOfRangeException()
    {
        var reader = BuildReader([SyntheticFontBuilder.SimpleGlyph()]);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetGlyphOutline(-1));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader GetGlyphOutline GlyphIndexTooLarge ThrowsArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_GetGlyphOutline_GlyphIndexTooLarge_ThrowsArgumentOutOfRangeException()
    {
        var reader = BuildReader([SyntheticFontBuilder.SimpleGlyph()]);

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetGlyphOutline(1));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph SingleComponent ProducesTranslatedOutline.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_SingleComponent_ProducesTranslatedOutline()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 100, 200)
        ]);

        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new System.Numerics.Vector2(100, 200), subpath.Start);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph ScaledComponent ScalesOutline.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_ScaledComponent_ScalesOutline()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 0, 0, HasScale: true, A: 1.5f, D: 1.5f)
        ]);

        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

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
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);

        // 90-degree counter-clockwise rotation matrix: a=0, b=1, c=-1, d=0 maps (x, y) -> (-y, x).
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 0, 0, HasTwoByTwo: true, A: 0f, B: 1f, C: -1f, D: 0f)
        ]);

        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

        var subpath = Assert.Single(path.Subpaths);
        var lineTo = subpath.Commands[0];
        // The first LineTo draws to the untransformed point (10, 0), which the rotation maps to (0, 10).
        Assert.Equal(0f, lineTo.EndPoint.X, 2);
        Assert.Equal(10f, lineTo.EndPoint.Y, 2);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph NestedComposite ResolvesRecursively.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_NestedComposite_ResolvesRecursively()
    {
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

        var reader = BuildReader([square, inner, outer]);
        var path = reader.GetGlyphOutline(2);

        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new System.Numerics.Vector2(105, 105), subpath.Start);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph MultipleComponents ProducesMultipleSubpaths.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_MultipleComponents_ProducesMultipleSubpaths()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 0, 0),
            new SyntheticFontBuilder.CompositeComponent(0, 50, 50)
        ]);

        var reader = BuildReader([square, composite]);
        var path = reader.GetGlyphOutline(1);

        Assert.Equal(2, path.Subpaths.Count);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph PointMatchedComponent ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_PointMatchedComponent_ThrowsInvalidDataException()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 0, 0, ArgsAreXyValues: false)
        ]);

        var reader = BuildReader([square, composite]);

        Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(1));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph OutOfRangeComponentGlyphIndex ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_OutOfRangeComponentGlyphIndex_ThrowsInvalidDataException()
    {
        var composite = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(99, 0, 0)
        ]);

        var reader = BuildReader([composite]);

        Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph SelfReferentialCycle ThrowsInvalidDataExceptionViaDepthCap.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_SelfReferentialCycle_ThrowsInvalidDataExceptionViaDepthCap()
    {
        // Glyph 0 is a composite whose only component is itself: recursion depth increases
        // without bound until the deterministic depth cap trips - this must never hang.
        var selfReferential = SyntheticFontBuilder.CompositeGlyph(
        [
            new SyntheticFontBuilder.CompositeComponent(0, 1, 1)
        ]);

        var reader = BuildReader([selfReferential]);

        var ex = Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader CompositeGlyph ExponentialBlowUp ThrowsInvalidDataExceptionViaComponentCap.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_CompositeGlyph_ExponentialBlowUp_ThrowsInvalidDataExceptionViaComponentCap()
    {
        // A 4-level chain, each with 10 components referencing the next level (branching factor
        // 10, depth 4 <= MaxDepth): total resolved components is on the order of 10 + 100 + 1000
        // + 10000, far exceeding the 5000 total-component cap, while nesting depth never
        // approaches the separate depth cap - proving the component-count cap (not the depth
        // cap) is what prevents the hang here.
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

        var reader = BuildReader([leaf, level3, level2, level1, level0]);

        var ex = Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(4));
        Assert.Contains("too many", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves that GlyfLocaReader Parse LocaEntryExceedsGlyfBounds ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_Parse_LocaEntryExceedsGlyfBounds_ThrowsInvalidDataException()
    {
        var loca = SyntheticFontBuilder.Loca([100], longFormat: false);
        var data = new byte[loca.Length + 10];
        loca.CopyTo(data, 0);

        Assert.Throws<InvalidDataException>(() => GlyfLocaReader.Parse(data, 0, loca.Length, loca.Length, 10, 1, false));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader Parse NonMonotonicLocaEntries ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_Parse_NonMonotonicLocaEntries_ThrowsInvalidDataException()
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // glyph 0: offset 0
        SyntheticFontBuilder.WriteUInt16(buf, 5); // glyph 1 end / glyph 2 start: offset 10
        SyntheticFontBuilder.WriteUInt16(buf, 3); // glyph 2 end: offset 6 - decreases from 10
        var loca = buf.ToArray();
        var data = new byte[loca.Length + 10];
        loca.CopyTo(data, 0);

        Assert.Throws<InvalidDataException>(() => GlyfLocaReader.Parse(data, 0, loca.Length, loca.Length, 10, 2, false));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader Parse TruncatedLoca ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_Parse_TruncatedLoca_ThrowsInvalidDataException()
    {
        var loca = new byte[2]; // needs (numGlyphs + 1) * 2 = 4 bytes for numGlyphs=1

        Assert.Throws<InvalidDataException>(() => GlyfLocaReader.Parse(loca, 0, loca.Length, 2, 0, 1, false));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader TruncatedGlyphHeader ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_TruncatedGlyphHeader_ThrowsInvalidDataException()
    {
        var reader = BuildReader([new byte[5]]); // shorter than the 10-byte glyph header

        Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
    }

    /// <summary>
    ///     Proves that GlyfLocaReader InvalidContourCount ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void GlyfLocaReader_InvalidContourCount_ThrowsInvalidDataException()
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteInt16(buf, -2); // invalid: only -1 (composite) or >=0 are valid
        for (var i = 0; i < 4; i++)
        {
            SyntheticFontBuilder.WriteInt16(buf, 0);
        }

        var glyph = buf.ToArray();
        var reader = BuildReader([glyph]);

        Assert.Throws<InvalidDataException>(() => reader.GetGlyphOutline(0));
    }
}
