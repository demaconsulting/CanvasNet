using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

// cspell:ignore vsdx Visio unitsperem

/// <summary>
///     Unit-level tests for <c>VsdxDocument.TextLayout.cs</c>'s word-wrap/line-breaking/alignment
///     engine, driven directly through its internal <c>ResolveTextLayout</c> entry point with
///     hand-built <see cref="VsdxEffectiveTextRun"/>s and a deterministic synthetic
///     <see cref="TrueTypeFont"/> (via <see cref="SyntheticFontBuilder"/>, mirroring
///     <c>PptxTextLayoutTests</c>'s own test seam) so every expected glyph coordinate can be
///     hand-computed exactly.
/// </summary>
public class VsdxTextLayoutTests
{
    // Synthetic font: UnitsPerEm 1000, ascender 800, descender -200, lineGap 0 (natural line
    // height == size, a simple multiply rather than a messier ratio).
    //   .notdef (index 0), advance 0, zero contours.
    //   'A' (codepoint 65, index 1), advance 500 units, non-empty outline.
    //   ' ' (codepoint 32, index 2), advance 200 units, zero contours (whitespace).
    private static TrueTypeFont NewFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (500, 0, true), (250, 800, true)]);
        var glyphSpace = SyntheticFontBuilder.SimpleGlyph();
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1), (32, 2)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(3))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 3))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500, 200]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length, glyphSpace.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA, .. glyphSpace])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    private static readonly Func<string, bool, bool, TrueTypeFont> ConstantFontResolver = (_, _, _) => NewFont();
    private static readonly VsdxEffectiveParagraphProperties LeftParagraph = new(VsdxHorizontalAlign.Left, 0, 0, 0, 0, 0, 0);
    private static readonly VsdxEffectiveParagraphProperties CenterParagraph = new(VsdxHorizontalAlign.Center, 0, 0, 0, 0, 0, 0);

    private static VsdxEffectiveTextRun MakeRun(string text, VsdxEffectiveParagraphProperties? paragraph = null) =>
        new(text, "Synthetic", 1.0, false, false, new Rgba32(0, 0, 0, 255), paragraph ?? LeftParagraph);

    private static VsdxTextBoxTransform MakeBox(double width, double height) =>
        new(width * 0.5, height * 0.5, width, height, width * 0.5, height * 0.5, 0);

    private static VsdxEffectiveTextBoxStyle MakeStyle(VsdxVerticalAlign verticalAlign = VsdxVerticalAlign.Top) =>
        new(verticalAlign, 0, 0, 0, 0);

    /// <summary>Proves a single word narrower than the box produces exactly one line with every non-whitespace glyph placed left-to-right with no wrap.</summary>
    [Fact]
    public void TextLayout_SingleLineFit_ProducesOneLineWithAllGlyphs()
    {
        // Arrange: "AA" at size 1 inch, advance 0.5in/glyph = 1in total, box width 10in (ample).
        var runs = new List<VsdxEffectiveTextRun> { MakeRun("AA") };

        // Act
        var layout = VsdxDocument.ResolveTextLayout(runs, MakeBox(10, 10), MakeStyle(), ConstantFontResolver);

        // Assert
        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(0.0, layout.Glyphs[0].OriginXInches, 6);
        Assert.Equal(0.5, layout.Glyphs[1].OriginXInches, 6);
    }

    /// <summary>Proves a paragraph wider than the box wraps across multiple lines at a whitespace boundary, never splitting a word.</summary>
    [Fact]
    public void TextLayout_WordWiderThanBox_WrapsAtWhitespaceBoundary()
    {
        // Arrange: "AA AA" - each "AA" is 1in wide, a space is 0.2in; box width 1.5in fits one
        // "AA" plus the following space but not a second "AA" (1 + 0.2 + 1 = 2.2 > 1.5).
        var runs = new List<VsdxEffectiveTextRun> { MakeRun("AA AA") };

        // Act
        var layout = VsdxDocument.ResolveTextLayout(runs, MakeBox(1.5, 10), MakeStyle(), ConstantFontResolver);

        // Assert: two lines' worth of glyphs (2 + 2 = 4 'A' glyphs total), and the second line's
        // first glyph restarts at X=0 (left-aligned, no leftover offset carried from line one).
        Assert.Equal(4, layout.Glyphs.Count);
        var secondLineFirstGlyphX = layout.Glyphs[2].OriginXInches;
        Assert.Equal(0.0, secondLineFirstGlyphX, 6);

        // The two lines occupy distinct Y positions (line two strictly below line one, since
        // shape-local space is y-up).
        Assert.True(layout.Glyphs[2].OriginYInches < layout.Glyphs[0].OriginYInches);
    }

    /// <summary>Proves <c>HorzAlign="1"</c> (center) shifts a short line's glyphs right of a left-aligned line's own start X, by half the leftover width.</summary>
    [Fact]
    public void TextLayout_HorzAlignCenter_ShiftsLineToBoxHorizontalCenter()
    {
        // Arrange: "AA" (1in wide) in a 5in-wide box; centered leftover is (5 - 1) / 2 = 2in.
        var centeredRuns = new List<VsdxEffectiveTextRun> { MakeRun("AA", CenterParagraph) };
        var leftRuns = new List<VsdxEffectiveTextRun> { MakeRun("AA", LeftParagraph) };

        // Act
        var centeredLayout = VsdxDocument.ResolveTextLayout(centeredRuns, MakeBox(5, 10), MakeStyle(), ConstantFontResolver);
        var leftLayout = VsdxDocument.ResolveTextLayout(leftRuns, MakeBox(5, 10), MakeStyle(), ConstantFontResolver);

        // Assert
        Assert.Equal(0.0, leftLayout.Glyphs[0].OriginXInches, 6);
        Assert.Equal(2.0, centeredLayout.Glyphs[0].OriginXInches, 6);
    }

    /// <summary>
    ///     Proves this milestone's (Milestone 12, Finding #3) symptom directly at the word-wrap
    ///     level: the same text laid out against the Master's own stale, too-narrow template-
    ///     default width wraps across two lines, while the instance's own true, corrected width
    ///     keeps every glyph on a single line - mirroring
    ///     <c>Test_Visio-Some_Random_Text.vsdx</c>'s own "View" Group header child ("Test View"
    ///     wrapping to "Test"/"View" against the pre-fix stale <c>0.5in</c> <c>TxtWidth</c>, fitting
    ///     on one line against the corrected <c>~1.1146in</c> width - see
    ///     <c>VsdxTextBoxPositioningTests.TextBox_GroupChildInhMarkedWidthDiffersFromMaster_TxtWidthReflectsInstanceWidth</c>
    ///     for the <c>TxtWidth</c>-resolution half of this same fix).
    /// </summary>
    [Fact]
    public void TextLayout_CorrectedGroupChildWidth_FitsOneLineWhereStaleMasterWidthOverWrapped()
    {
        // Arrange: "AA AA" - each "AA" is 1in wide, a space is 0.2in (total natural width 2.2in).
        // A 1.1in-wide box (simulating the Master's own stale template-default TxtWidth) fits
        // only the first "AA" before wrapping; a 2.3in-wide box (simulating the instance's own
        // corrected, resized TxtWidth) fits the whole line.
        var runs = new List<VsdxEffectiveTextRun> { MakeRun("AA AA") };

        // Act
        var staleLayout = VsdxDocument.ResolveTextLayout(runs, MakeBox(1.1, 10), MakeStyle(), ConstantFontResolver);
        var correctedLayout = VsdxDocument.ResolveTextLayout(runs, MakeBox(2.3, 10), MakeStyle(), ConstantFontResolver);

        // Assert: the stale, too-narrow width wraps across two distinct Y lines (the pre-fix
        // symptom).
        Assert.True(staleLayout.Glyphs[2].OriginYInches < staleLayout.Glyphs[0].OriginYInches);

        // Assert: the corrected width keeps every glyph on a single line (no over-wrap).
        var firstLineY = correctedLayout.Glyphs[0].OriginYInches;
        Assert.All(correctedLayout.Glyphs, glyph => Assert.Equal(firstLineY, glyph.OriginYInches, 6));
    }

    /// <summary>
    ///     Proves <c>VerticalAlign</c> top/middle/bottom each anchor a single short line's
    ///     baseline at the expected Y position within a box taller than the line's own natural
    ///     height.
    /// </summary>
    [Theory]
    [InlineData("Top", 10.0 - 0.8)]
    [InlineData("Middle", 5.5 - 0.8)]
    [InlineData("Bottom", 1.0 - 0.8)]
    public void TextLayout_VerticalAlign_AnchorsLineAtExpectedBaselineY(string verticalAlignName, double expectedBaselineY)
    {
        // Arrange: a single "A" (natural line height == size == 1in) in a 10in-tall box. The
        // vertical-align value is passed as a string (rather than the internal
        // VsdxVerticalAlign enum directly) because xUnit's [Theory]/[InlineData] discovery
        // requires every public test method parameter to be at least as accessible as the
        // method itself, and VsdxVerticalAlign is internal.
        var verticalAlign = Enum.Parse<VsdxVerticalAlign>(verticalAlignName);
        var runs = new List<VsdxEffectiveTextRun> { MakeRun("A") };

        // Act
        var layout = VsdxDocument.ResolveTextLayout(runs, MakeBox(10, 10), MakeStyle(verticalAlign), ConstantFontResolver);

        // Assert: baseline = box top - ascent (Top), or shifted down by half/all of the leftover
        // vertical space (Middle/Bottom) - see VsdxDocument.TextLayout.cs's own PositionLines.
        Assert.Equal(expectedBaselineY, Assert.Single(layout.Glyphs).OriginYInches, 6);
    }

    /// <summary>
    ///     Proves a negative <c>SpLine</c> value scales the natural line height by its own
    ///     magnitude (format reference's "multiple of single-spacing" encoding), verified by the
    ///     increased vertical gap between two lines' own baselines.
    /// </summary>
    [Fact]
    public void TextLayout_NegativeSpLine_ScalesLineHeightByMagnitudeMultiplier()
    {
        // Arrange: two forced lines (one run per line, each its own paragraph via a literal '\n'),
        // first with SpLine=0 (natural, 1in height), then with SpLine=-2 (200% spacing, 2in).
        var naturalParagraph = new VsdxEffectiveParagraphProperties(VsdxHorizontalAlign.Left, 0, 0, 0, 0, 0, 0);
        var doubleSpacedParagraph = new VsdxEffectiveParagraphProperties(VsdxHorizontalAlign.Left, -2, 0, 0, 0, 0, 0);

        var naturalRuns = new List<VsdxEffectiveTextRun> { MakeRun("A\nA", naturalParagraph) };
        var doubleSpacedRuns = new List<VsdxEffectiveTextRun> { MakeRun("A\nA", doubleSpacedParagraph) };

        // Act
        var naturalLayout = VsdxDocument.ResolveTextLayout(naturalRuns, MakeBox(10, 10), MakeStyle(), ConstantFontResolver);
        var doubleSpacedLayout = VsdxDocument.ResolveTextLayout(doubleSpacedRuns, MakeBox(10, 10), MakeStyle(), ConstantFontResolver);

        // Assert: the gap between line one's and line two's baselines is exactly 1in (natural)
        // vs exactly 2in (double-spaced per the -2 multiplier).
        var naturalGap = naturalLayout.Glyphs[0].OriginYInches - naturalLayout.Glyphs[1].OriginYInches;
        var doubleSpacedGap = doubleSpacedLayout.Glyphs[0].OriginYInches - doubleSpacedLayout.Glyphs[1].OriginYInches;
        Assert.Equal(1.0, naturalGap, 6);
        Assert.Equal(2.0, doubleSpacedGap, 6);
    }
}
