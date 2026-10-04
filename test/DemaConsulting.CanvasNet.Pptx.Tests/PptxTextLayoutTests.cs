using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx txbody bodypr lnspc normautofit noautofit spautofit fontscale lnspcreduction unitsperem

/// <summary>
///     Unit-level tests for the Phase 1d text layout engine (<c>PptxDocument.TextLayout.cs</c>):
///     word-wrap, horizontal alignment, vertical anchor, and the three-tier autofit policy. Every
///     test injects a deterministic synthetic <see cref="TrueTypeFont"/> (via a <c>fontResolver</c>
///     delegate that bypasses installed-font discovery entirely) so expected glyph coordinates can
///     be hand-computed exactly. Complements <see cref="PptxTextTests"/> (parsing/inheritance) and
///     <see cref="PptxTextRenderTests"/> (glyph painting).
/// </summary>
public class PptxTextLayoutTests
{
    private static readonly XNamespace DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace PresentationNs = "http://schemas.openxmlformats.org/presentationml/2006/main";

    // Synthetic font: UnitsPerEm 1000, ascender 800, descender -200, lineGap 0 (natural line
    // height == size, since ascender - descender + lineGap == UnitsPerEm exactly - this makes
    // every expected line-height/ascent hand-computation a simple multiply, not a messier ratio).
    //   .notdef (index 0), advance 0, zero contours.
    //   'A' (codepoint 65, index 1), advance 500 units, non-empty outline (a filled triangle).
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

    // Synthetic font: UnitsPerEm 1000, ascender 3000, descender -1000, lineGap 0 - a font with a
    // much taller em-box (raw natural-height metric 4000) than NewFont()'s (1000), used to prove
    // the per-token "tallest font" comparison in BuildLines scales by UnitsPerEm/SizeEmu before
    // comparing, rather than comparing raw font-design units directly: a tiny-point-size run
    // using this font must NOT out-rank a much-larger-point-size run using NewFont() merely
    // because its raw metrics happen to be numerically bigger.
    //   .notdef (index 0), advance 0, zero contours.
    //   'B' (codepoint 66, index 1), advance 400 units, non-empty outline (a filled triangle).
    private static TrueTypeFont NewTallEmBoxFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphB = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (400, 0, true), (200, 3000, true)]);
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(66, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(3000, -1000, 0, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 400]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphB.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphB])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    private static PptxTheme BuildTestTheme() =>
        new(
            new PptxColorScheme(
                new Rgba32(10, 10, 10, 255), new Rgba32(20, 20, 20, 255), new Rgba32(30, 30, 30, 255), new Rgba32(40, 40, 40, 255),
                new Rgba32(50, 50, 50, 255), new Rgba32(60, 60, 60, 255), new Rgba32(70, 70, 70, 255), new Rgba32(80, 80, 80, 255),
                new Rgba32(90, 90, 90, 255), new Rgba32(100, 100, 100, 255), new Rgba32(110, 110, 110, 255), new Rgba32(120, 120, 120, 255)),
            new PptxFontScheme(
                new PptxFontCollection("ThemeMajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("ThemeMinorLatin", "MinorEA", "MinorCS")));

    private static readonly Func<string, bool, bool, TrueTypeFont> ConstantFontResolver = (_, _, _) => NewFont();

    /// <summary>
    ///     Builds a one-paragraph, one-run <c>&lt;p:txBody&gt;</c> with a no-inset
    ///     <c>&lt;a:bodyPr&gt;</c>, for exact EMU math. <paramref name="bodyPrExtra"/>/
    ///     <paramref name="paragraphExtra"/>, when supplied, are full standalone
    ///     <c>&lt;bodyPr&gt;</c>/<c>&lt;pPr&gt;</c> element fragments whose own attributes and
    ///     child elements are merged onto the real <c>&lt;a:bodyPr&gt;</c>/<c>&lt;a:pPr&gt;</c>
    ///     element (not nested underneath it - a nested, inner <c>&lt;bodyPr&gt;</c> element would
    ///     be silently invisible to the parser, which only inspects the outer element's own
    ///     attributes/children).
    /// </summary>
    private static PptxTextBody BuildSingleRunTextBody(string text, string? bodyPrExtra = null, string? paragraphExtra = null)
    {
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"));
        if (bodyPrExtra is not null)
        {
            var extra = XElement.Parse(bodyPrExtra);
            bodyPr.Add(extra.Attributes());
            bodyPr.Add(extra.Elements());
        }

        var pPr = new XElement(DrawingNs + "pPr");
        if (paragraphExtra is not null)
        {
            var extra = XElement.Parse(paragraphExtra);
            pPr.Add(extra.Attributes());
            pPr.Add(extra.Elements());
        }

        var txBody = new XElement(
            PresentationNs + "txBody",
            bodyPr,
            new XElement(
                DrawingNs + "p",
                pPr,
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")),
                    new XElement(DrawingNs + "t", text))));

        return PptxDocument.ParseTextBody(txBody);
    }

    private static PptxTextLayout Layout(PptxTextBody textBody, float widthEmu, float heightEmu) =>
        PptxDocument.ResolveTextLayout(textBody, new PptxPlaceholderProperties(null, null, BuildTestTheme()), BuildTestTheme(), "body", widthEmu, heightEmu, ConstantFontResolver);

    #region Word wrap

    /// <summary>Proves "AA AA" wraps onto a second line once the available width can no longer hold the trailing word.</summary>
    [Fact]
    public void ResolveTextLayout_WordWrap_NarrowWidth_WrapsAtTokenBoundary()
    {
        // 'A' advance at sz=100 (SizeEmu 12700): 500/1000 * 12700 = 6350. "AA" = 12700. ' ' = 2540.
        // Available width 20000 fits "AA" + ' ' (15240) but not a further "AA" (27940) - the
        // second "AA" token wraps onto its own line.
        var textBody = BuildSingleRunTextBody("AA AA");

        var layout = Layout(textBody, 20000f, 100000f);

        Assert.Equal(4, layout.Glyphs.Count);
        // Line 1 ("AA"): ascent 800/1000*12700 = 10160, at y = 0 + 10160.
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        Assert.Equal(6350f, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[1].OriginYEmu, 2);
        // Line 2 ("AA"): line height = natural height = 12700 (ascender - descender + lineGap ==
        // UnitsPerEm, so naturalHeight == sizeEmu exactly), baseline at 12700 + 10160.
        Assert.Equal(0f, layout.Glyphs[2].OriginXEmu, 2);
        Assert.Equal(22860f, layout.Glyphs[2].OriginYEmu, 2);
        Assert.Equal(6350f, layout.Glyphs[3].OriginXEmu, 2);
        Assert.Equal(22860f, layout.Glyphs[3].OriginYEmu, 2);
    }

    /// <summary>Proves a single token wider than the available width is placed alone on its own (overflowing) line rather than looping/throwing.</summary>
    [Fact]
    public void ResolveTextLayout_WordWrap_SingleTokenWiderThanAvailableWidth_PlacedAloneOnOwnLine()
    {
        // 10 'A's at sz=100: width = 10 * 6350 = 63500, far exceeding the 20000 available width.
        var textBody = BuildSingleRunTextBody("AAAAAAAAAA");

        var layout = Layout(textBody, 20000f, 100000f);

        Assert.Equal(10, layout.Glyphs.Count);
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(9 * 6350f, layout.Glyphs[9].OriginXEmu, 2);
    }

    /// <summary>
    ///     Proves a word split across two adjacent formatting runs with no whitespace between
    ///     them (for example a bold "Hel" run immediately followed by a plain "lo" run spelling
    ///     "Hello") is never wrapped at that run boundary: <c>PackTokensIntoLines</c> must treat
    ///     the two runs' tokens as a single wrap-atomic word group, not two tokens each
    ///     independently eligible to wrap, even though <c>Tokenize</c> only ever sees one run's
    ///     text at a time.
    ///     An available width that sits strictly between the width of either run's own text and
    ///     their combined width would, under the old per-token wrap logic, incorrectly wrap
    ///     between the two runs (producing "A" / "A" on two lines) - here both glyphs must still
    ///     land on the very same (overflowing) line, since there is no whitespace anywhere in the
    ///     paragraph to legally wrap at, exactly mirroring
    ///     <see cref="ResolveTextLayout_WordWrap_SingleTokenWiderThanAvailableWidth_PlacedAloneOnOwnLine"/>'s
    ///     own single-token overflow policy, just for a word spread across two runs instead of one.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_WordWrap_WordSplitAcrossRuns_DoesNotWrapAtRunBoundary()
    {
        var pPr = new XElement(DrawingNs + "pPr");
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"));
        var txBody = new XElement(
            PresentationNs + "txBody",
            bodyPr,
            new XElement(
                DrawingNs + "p",
                pPr,
                new XElement(DrawingNs + "r", new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")), new XElement(DrawingNs + "t", "A")),
                new XElement(DrawingNs + "r", new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")), new XElement(DrawingNs + "t", "A"))));
        var textBody = PptxDocument.ParseTextBody(txBody);

        // Each run's "A" at sz=100 is 6350 EMU wide; the combined word "AA" is 12700 EMU. An
        // available width of 10000 sits strictly between the two - wide enough for either run's
        // text alone, but not their combined word - so the old per-token wrap logic would
        // incorrectly wrap between the two runs after placing the first "A".
        var layout = Layout(textBody, 10000f, 100000f);

        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        // Both glyphs on the very same line - no wrap at the run boundary.
        Assert.Equal(6350f, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[1].OriginYEmu, 2);
    }

    /// <summary>
    ///     Proves an <c>&lt;a:br/&gt;</c> between two runs forces an explicit second line even
    ///     though the available width is ample enough to fit both runs' text on a single
    ///     word-wrapped line - i.e. "A&lt;br/&gt;B" must lay out as two lines, not "AB" on one
    ///     line, which is the behavior before this fix.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_RunBreakRun_ForcesSecondLine()
    {
        var pPr = new XElement(DrawingNs + "pPr");
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"));
        var txBody = new XElement(
            PresentationNs + "txBody",
            bodyPr,
            new XElement(
                DrawingNs + "p",
                pPr,
                new XElement(DrawingNs + "r", new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")), new XElement(DrawingNs + "t", "A")),
                new XElement(DrawingNs + "br"),
                new XElement(DrawingNs + "r", new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")), new XElement(DrawingNs + "t", "B"))));
        var textBody = PptxDocument.ParseTextBody(txBody);

        // Available width 50000 easily fits "AB" (12700) on a single word-wrapped line, so two
        // lines can only come from the explicit break being honored, not from word-wrap.
        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(2, layout.Glyphs.Count);
        // Line 1 ("A"): ascent 800/1000*12700 = 10160, at x=0, y=10160.
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        // Line 2 ("B"): line height = 12700, baseline at x=0, y = 12700 + 10160 = 22860 - not on
        // the same line as "A" (which a dropped break would have produced at x=6350, y=10160).
        Assert.Equal(0f, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(22860f, layout.Glyphs[1].OriginYEmu, 2);
    }

    /// <summary>
    ///     Proves the per-token "tallest font" comparison used to pick a line's height/ascent
    ///     compares each candidate's actual rendered natural height (scaled by its own
    ///     <c>UnitsPerEm</c>/<c>SizeEmu</c>), not raw font-design units directly - a tiny-point-size
    ///     run using a font with a much taller em-box must not out-rank a far larger-point-size run
    ///     using a more modest em-box, which raw-unit comparison would incorrectly do.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_TallestFontComparison_ScalesByUnitsPerEmAndSizeEmu_NotRawFontUnits()
    {
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"));
        var txBody = new XElement(
            PresentationNs + "txBody",
            bodyPr,
            new XElement(
                DrawingNs + "p",
                new XElement(DrawingNs + "pPr"),
                // Run 1: NewFont() (ascender 800/descender -200/UnitsPerEm 1000, raw natural-height
                // metric 1000) at a large sz=1000 (SizeEmu 127000): actual natural height =
                // 1000/1000*127000 = 127000 - the correct tallest candidate.
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "1000"), new XElement(DrawingNs + "latin", new XAttribute("typeface", "BigFont"))),
                    new XElement(DrawingNs + "t", "A")),
                // Run 2: NewTallEmBoxFont() (ascender 3000/descender -1000/UnitsPerEm 1000, raw
                // natural-height metric 4000 - numerically bigger than run 1's raw 1000) at a tiny
                // sz=10 (SizeEmu 1270): actual natural height = 4000/1000*1270 = 5080, far smaller
                // than run 1's 127000. A raw-unit comparison would incorrectly rank this run
                // "tallest" (4000 > 1000) and use its own (much smaller) ascent/line-height.
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "10"), new XElement(DrawingNs + "latin", new XAttribute("typeface", "TallEmBoxFont"))),
                    new XElement(DrawingNs + "t", "B"))));
        var textBody = PptxDocument.ParseTextBody(txBody);

        var bigFont = NewFont();
        var tallEmBoxFont = NewTallEmBoxFont();
        Func<string, bool, bool, TrueTypeFont> fontResolver = (family, _, _) =>
            family == "TallEmBoxFont" ? tallEmBoxFont : bigFont;

        var layout = PptxDocument.ResolveTextLayout(
            textBody, new PptxPlaceholderProperties(null, null, BuildTestTheme()), BuildTestTheme(), "body", 500000f, 500000f, fontResolver);

        Assert.Equal(2, layout.Glyphs.Count);
        // Correct ascent comes from run 1 (the actually-tallest run): 800/1000*127000 = 101600 -
        // not run 2's 3000/1000*1270 = 3810, which the pre-fix raw-unit comparison would have used.
        Assert.Equal(101600f, layout.Glyphs[0].OriginYEmu, 2);
        Assert.Equal(101600f, layout.Glyphs[1].OriginYEmu, 2);
    }

    /// <summary>
    ///     Proves a <c>wrap="none"</c> shape's centered paragraph is still positioned against the
    ///     shape's own real, finite declared width - not against the effectively-infinite width
    ///     <see cref="PptxDocument.ResolveTextLayout"/> internally uses to suppress word-wrapping
    ///     for <c>wrap="none"</c> - which would otherwise place the line's glyphs at an
    ///     astronomical X offset, far outside the shape (and any realistic surface) entirely,
    ///     painting nothing despite centered alignment being an already-implemented, in-scope
    ///     feature.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_AlignCenter_WrapNone_StillCentersAgainstShapesDeclaredWidth()
    {
        // "AA" line width = 12700. Shape's own declared width 50000 (passed as widthEmu).
        // startX = (50000 - 12700) / 2 = 18650 - identical to the wrap="square" case, proving
        // alignment ignores the infinite wrap-suppression width entirely.
        var textBody = BuildSingleRunTextBody(
            "AA",
            bodyPrExtra: """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" wrap="none" />""",
            paragraphExtra: """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" algn="ctr" />""");

        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(18650f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(18650f + 6350f, layout.Glyphs[1].OriginXEmu, 2);
    }

    #endregion

    #region Horizontal alignment

    /// <summary>Proves <c>algn="ctr"</c> centers the line within the available width.</summary>
    [Fact]
    public void ResolveTextLayout_AlignCenter_CentersLineWithinAvailableWidth()
    {
        // "AA" line width = 12700. Available width 50000.
        // startX = (50000 - 12700) / 2 = 18650.
        var textBody = BuildSingleRunTextBody("AA", paragraphExtra: """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" algn="ctr" />""");

        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(18650f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(18650f + 6350f, layout.Glyphs[1].OriginXEmu, 2);
    }

    /// <summary>Proves <c>algn="r"</c> right-aligns the line against the available width.</summary>
    [Fact]
    public void ResolveTextLayout_AlignRight_RightAlignsLineAgainstAvailableWidth()
    {
        // "AA" line width = 12700. Available width 50000. startX = 50000 - 12700 = 37300.
        var textBody = BuildSingleRunTextBody("AA", paragraphExtra: """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" algn="r" />""");

        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(37300f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(37300f + 6350f, layout.Glyphs[1].OriginXEmu, 2);
    }

    #endregion

    #region Vertical anchor

    /// <summary>Resolve Text Layout Anchor Top Positions First Baseline At Ascent From Top.</summary>
    [Fact]
    public void ResolveTextLayout_AnchorTop_PositionsFirstBaselineAtAscentFromTop()
    {
        var textBody = BuildSingleRunTextBody("A");

        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
    }

    /// <summary>Resolve Text Layout Anchor Middle Centers Block Vertically Within Available Height.</summary>
    [Fact]
    public void ResolveTextLayout_AnchorMiddle_CentersBlockVerticallyWithinAvailableHeight()
    {
        // Total block height = 12700 (one line). Available height 100000.
        // startY = (100000 - 12700) / 2 = 43650. baseline = 43650 + 10160 = 53810.
        var textBody = BuildSingleRunTextBody("A", """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" anchor="ctr" />""");

        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(53810f, layout.Glyphs[0].OriginYEmu, 2);
    }

    /// <summary>Resolve Text Layout Anchor Bottom Positions Block At Bottom Of Available Height.</summary>
    [Fact]
    public void ResolveTextLayout_AnchorBottom_PositionsBlockAtBottomOfAvailableHeight()
    {
        // startY = 100000 - 12700 = 87300. baseline = 87300 + 10160 = 97460.
        var textBody = BuildSingleRunTextBody("A", """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" anchor="b" />""");

        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(97460f, layout.Glyphs[0].OriginYEmu, 2);
    }

    #endregion

    #region Autofit

    /// <summary>Proves <c>&lt;a:noAutofit/&gt;</c> applies no scaling even when the laid-out text overflows the shape's height.</summary>
    [Fact]
    public void ResolveTextLayout_NoAutofit_AppliesNoScalingEvenWhenOverflowing()
    {
        var textBody = BuildSingleRunTextBody("A", """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><noAutofit /></bodyPr>""");

        // Available height far too small to hold even one line - must not throw, must not scale.
        var layout = Layout(textBody, 50000f, 10f);

        Assert.Equal(1f, layout.AppliedFontScale);
    }

    /// <summary>Proves <c>&lt;a:spAutoFit/&gt;</c> is a pass-through (no scaling), per Design Decision 4's documented deferral of shape-resize behavior.</summary>
    [Fact]
    public void ResolveTextLayout_SpAutoFit_AppliesNoScaling()
    {
        var textBody = BuildSingleRunTextBody("A", """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><spAutoFit /></bodyPr>""");

        var layout = Layout(textBody, 50000f, 10f);

        Assert.Equal(1f, layout.AppliedFontScale);
    }

    /// <summary>Proves an explicit <c>&lt;a:normAutofit fontScale="..." lnSpcReduction="..."/&gt;</c> is applied verbatim, not recomputed.</summary>
    [Fact]
    public void ResolveTextLayout_NormAutofitWithExplicitAttributes_AppliesStoredFactorsVerbatim()
    {
        var textBody = BuildSingleRunTextBody(
            "A",
            """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><normAutofit fontScale="50000" lnSpcReduction="10000" /></bodyPr>""");

        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(0.5f, layout.AppliedFontScale, 3);
    }

    /// <summary>
    ///     Proves a <c>&lt;a:normAutofit fontScale="..."/&gt;</c> with only <c>fontScale</c>
    ///     present (no <c>lnSpcReduction</c>) still applies its stored <c>fontScale</c> verbatim,
    ///     rather than discarding it and falling through to the attribute-less shrink loop (which,
    ///     for this ample-height case, would instead converge on an unscaled <c>1.0</c>) - OOXML's
    ///     <c>fontScale</c>/<c>lnSpcReduction</c> are independently optional, so either one alone
    ///     must still be honored.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_NormAutofitFontScaleOnly_AppliesStoredFontScaleVerbatim()
    {
        var textBody = BuildSingleRunTextBody(
            "A",
            """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><normAutofit fontScale="50000" /></bodyPr>""");

        // Available height is ample for the single unscaled line, so an attribute-less-style
        // shrink loop would converge on 1.0 instead of honoring the stored fontScale - proving
        // the 0.5 result below comes from the persisted attribute, not the shrink loop.
        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Equal(0.5f, layout.AppliedFontScale, 3);
    }

    /// <summary>
    ///     Proves a <c>&lt;a:normAutofit lnSpcReduction="..."/&gt;</c> with only
    ///     <c>lnSpcReduction</c> present (no <c>fontScale</c>) still applies its stored line-
    ///     spacing reduction - leaving <c>fontScale</c> at its own neutral default (<c>1.0</c>,
    ///     unscaled) - rather than discarding the reduction entirely.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_NormAutofitLnSpcReductionOnly_AppliesStoredReductionWithNeutralFontScale()
    {
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"),
            new XElement(DrawingNs + "normAutofit", new XAttribute("lnSpcReduction", "50000")));

        var paragraphs = Enumerable.Range(0, 2)
            .Select(_ => new XElement(
                DrawingNs + "p",
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")),
                    new XElement(DrawingNs + "t", "A"))))
            .ToArray();

        var txBody = new XElement(PresentationNs + "txBody", bodyPr, paragraphs);
        var textBody = PptxDocument.ParseTextBody(txBody);

        var layout = Layout(textBody, 50000f, 100000f);

        // fontScale stays unscaled (1.0): sz=100 -> SizeEmu 12700, ascent 10160.
        Assert.Equal(1f, layout.AppliedFontScale, 3);
        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        // Line height reduced to 50%: naturalHeight 12700 * 0.5 = 6350; line 2 baseline =
        // 6350 + 10160 = 16510 - not 12700 + 10160 = 22860, which is what an (incorrectly)
        // discarded lnSpcReduction would have produced.
        Assert.Equal(16510f, layout.Glyphs[1].OriginYEmu, 2);
    }

    /// <summary>
    ///     Proves an attribute-less <c>&lt;a:normAutofit/&gt;</c> runs the bounded 10%-step shrink
    ///     loop, converging on the scale that first fits the available height. Five single-line
    ///     paragraphs at sz=100 (SizeEmu 12700, naturalHeight 12700 since ascender - descender +
    ///     lineGap == UnitsPerEm exactly): unscaled total height 63500. Available height 50000:
    ///     scale 1.0 -> 63500 (no); 0.9 -> 57150 (no); 0.8 -> 50800 (no); 0.7 -> 44450 (fits).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_NormAutofitAttributeLess_ShrinkLoopConvergesOnFirstFittingScale()
    {
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"),
            new XElement(DrawingNs + "normAutofit"));

        var paragraphs = Enumerable.Range(0, 5)
            .Select(_ => new XElement(
                DrawingNs + "p",
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")),
                    new XElement(DrawingNs + "t", "A"))))
            .ToArray();

        var txBody = new XElement(PresentationNs + "txBody", bodyPr, paragraphs);
        var textBody = PptxDocument.ParseTextBody(txBody);

        var layout = Layout(textBody, 50000f, 50000f);

        Assert.Equal(0.7f, layout.AppliedFontScale, 2);
    }

    #endregion
}
