using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
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

    /// <summary>
    ///     Builds a two-paragraph, single-run-per-paragraph <c>&lt;p:txBody&gt;</c> with a
    ///     no-inset <c>&lt;a:bodyPr&gt;</c>, each paragraph's own <c>&lt;a:pPr&gt;</c> optionally
    ///     extended with <paramref name="pPrExtra1"/>/<paramref name="pPrExtra2"/> fragments (same
    ///     merge convention as <see cref="BuildSingleRunTextBody"/>'s own <c>paragraphExtra</c>),
    ///     used by the paragraph-spacing (Phase 2 Follow-Up) tests to exercise
    ///     <c>&lt;a:spcBef&gt;</c>/<c>&lt;a:spcAft&gt;</c> declared on one or both paragraphs.
    /// </summary>
    private static PptxTextBody BuildTwoParagraphTextBody(string text1, string? pPrExtra1, string text2, string? pPrExtra2)
    {
        XElement BuildPPr(string? extra)
        {
            var pPr = new XElement(DrawingNs + "pPr");
            if (extra is not null)
            {
                var parsed = XElement.Parse(extra);
                pPr.Add(parsed.Attributes());
                pPr.Add(parsed.Elements());
            }

            return pPr;
        }

        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"));
        var txBody = new XElement(
            PresentationNs + "txBody",
            bodyPr,
            new XElement(
                DrawingNs + "p",
                BuildPPr(pPrExtra1),
                new XElement(DrawingNs + "r", new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")), new XElement(DrawingNs + "t", text1))),
            new XElement(
                DrawingNs + "p",
                BuildPPr(pPrExtra2),
                new XElement(DrawingNs + "r", new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")), new XElement(DrawingNs + "t", text2))));

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

    /// <summary>
    ///     Proves a literal U+0009 TAB run character (Phase 2 Follow-Up: Default Tab-Stop
    ///     Expansion) expands the cursor to the next default tab stop (914400 EMU, 1 inch)
    ///     rather than measuring/advancing by its (near-zero, <c>.notdef</c>-glyph) font advance:
    ///     <see cref="NewFont"/>'s synthetic <c>cmap</c> has no entry for U+0009, so the pre-fix
    ///     behavior (resolving it exactly like any other whitespace character via
    ///     <c>GetGlyphIndex</c>/<c>GetAdvanceWidth</c>) would place "B" at
    ///     <c>x = 6350 + 0 = 6350</c> (glyph index 0's <c>hmtx</c> advance is 0) - immediately
    ///     after "A", jammed together - instead of the correct, PowerPoint-matching
    ///     <c>x = 914400</c>.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_TabCharacter_ExpandsToNextDefaultTabStop()
    {
        // 'A' at sz=100 (SizeEmu 12700): 500/1000*12700 = 6350. The tab following it expands the
        // cursor from 6350 to the next multiple of 914400 strictly greater than 6350, i.e. exactly
        // 914400 (floor(6350/914400)+1 = 1) - not a near-zero glyph-advance position. The
        // available width (2,000,000 EMU) is ample enough that no word-wrap occurs, isolating the
        // tab-expansion behavior itself from the wrap-decision logic (see the next test).
        var textBody = BuildSingleRunTextBody("A\tB");

        var layout = Layout(textBody, 2_000_000f, 1_000_000f);

        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        Assert.Equal(914400f, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[1].OriginYEmu, 2);
    }

    /// <summary>
    ///     Proves <c>PackTokensIntoLines</c>' wrap/fit decision uses the tab's dynamically-
    ///     computed, position-dependent tab-stop-expanded width - not its static, near-zero
    ///     glyph-measured <c>ResolvedToken.WidthEmu</c> - when deciding where word-wrap
    ///     breaks occur.
    /// </summary>
    /// <remarks>
    ///     "AAAA" (width 25400) + tab + "AAAA" (width 25400) at an available width of 920000 EMU:
    ///     using the tab's true, expanded width (the next 914400-EMU tab stop strictly past
    ///     25400, i.e. 914400 itself, an 889000 EMU advance) places "AAAA\t" on line 1 at a total
    ///     width of exactly 914400 (&lt;= 920000, fits), but the second "AAAA" would bring the
    ///     running width to 914400 + 25400 = 939800 (&gt; 920000) - correctly wrapping it onto a
    ///     second line. Using the tab's glyph-measured (near-zero, <c>.notdef</c>-advance) width
    ///     instead - the pre-fix behavior - the combined total of both words plus the tab would
    ///     be only 25400 + 0 + 25400 = 50800, comfortably under 920000, so the second "AAAA"
    ///     would incorrectly stay on line 1 instead of wrapping. This test asserts the correct
    ///     (wrapped) outcome.
    /// </remarks>
    [Fact]
    public void ResolveTextLayout_TabCharacter_WrapDecisionUsesExpandedWidthNotGlyphWidth()
    {
        var textBody = BuildSingleRunTextBody("AAAA\tAAAA");

        var layout = Layout(textBody, 920000f, 1_000_000f);

        // 8 visible 'A' glyphs total (the tab itself never emits a glyph) - 4 on each line.
        Assert.Equal(8, layout.Glyphs.Count);

        // Line 1 ("AAAA" then tab, which wraps the second "AAAA" away): ascent 10160.
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(6350f, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(12700f, layout.Glyphs[2].OriginXEmu, 2);
        Assert.Equal(19050f, layout.Glyphs[3].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);

        // Line 2 (the second "AAAA", correctly wrapped): line height 12700, baseline at
        // 12700 + 10160 = 22860 - restarting at x=0, not continuing on line 1 at x=914400+.
        Assert.Equal(0f, layout.Glyphs[4].OriginXEmu, 2);
        Assert.Equal(6350f, layout.Glyphs[5].OriginXEmu, 2);
        Assert.Equal(12700f, layout.Glyphs[6].OriginXEmu, 2);
        Assert.Equal(19050f, layout.Glyphs[7].OriginXEmu, 2);
        Assert.Equal(22860f, layout.Glyphs[4].OriginYEmu, 2);
    }

    /// <summary>
    ///     Proves two adjacent tab characters each independently advance to their own next tab
    ///     stop, computed from the cursor position <i>after</i> the previous tab's own expansion
    ///     - not both from the pre-first-tab position - and that a tab landing exactly on an
    ///     already-aligned tab-stop boundary still advances by a full, non-zero interval (never a
    ///     zero-width result), per <see cref="PptxDocument"/>'s <c>GetNextTabStopEmu</c> contract.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_MultipleTabCharacters_EachAdvancesToItsOwnNextTabStop()
    {
        // 'A' at sz=100: 6350. First tab: next stop strictly past 6350 is 914400 (one interval).
        // Second tab starts exactly on that 914400 boundary - its own next stop must still be a
        // full interval further on (1828800), never 914400 itself (a zero-width advance). "B"
        // then lands at 1828800 + 0 = 1828800, not at a near-zero position after two collapsed
        // tabs.
        var textBody = BuildSingleRunTextBody("A\t\tB");

        var layout = Layout(textBody, 3_000_000f, 1_000_000f);

        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        Assert.Equal(1828800f, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[1].OriginYEmu, 2);
    }

    /// <summary>
    ///     Proves a whitespace token that would itself overflow the current line (because the
    ///     word preceding it already fills the line, and the word following it is also too wide
    ///     to join) is absorbed into the following line rather than emitted as its own,
    ///     visually-blank, standalone line.
    /// </summary>
    /// <remarks>
    ///     Trace for <c>"AA AAA"</c> at <c>availableWidthEmu = 12700</c> ('A' advance 6350 EMU at
    ///     <c>sz="100"</c>, ' ' advance 2540 EMU): token <c>"AA"</c> (width 12700) packs first
    ///     with an empty <c>currentLine</c> (no overflow check) -&gt; <c>currentLine = ["AA"]</c>.
    ///     Token <c>" "</c> (width 2540) packs next: <c>currentLine</c> has visible content and
    ///     <c>12700 + 2540 = 15240 &gt; 12700</c> -&gt; flush <c>["AA"]</c> as line 1,
    ///     <c>currentLine = [" "]</c>. Token <c>"AAA"</c> (width 19050) packs last: before this
    ///     fix, <c>currentLine.Count &gt; 0</c> was true (the lone whitespace token counted as
    ///     "has content"), so <c>["AAA"]</c> would wrap away from the whitespace token first,
    ///     stranding <c>[" "]</c> as its own blank line 2 and pushing <c>["AAA"]</c> onto line 3.
    ///     With the fix, <c>currentLine</c> holds only whitespace, so it is not treated as "has
    ///     content" - <c>"AAA"</c> is merged onto the same line instead, producing exactly 2 lines
    ///     (<c>["AA"]</c>, <c>[" ", "AAA"]</c>), matching standard word-processor wrap behavior
    ///     (the trailing space before a wrapped word is swallowed at the wrap point).
    /// </remarks>
    [Fact]
    public void ResolveTextLayout_WordWrap_WhitespaceTokenWouldOverflowAlone_DoesNotEmitWhitespaceOnlyLine()
    {
        var textBody = BuildSingleRunTextBody("AA AAA");

        var layout = Layout(textBody, 12700f, 100000f);

        // 5 visible glyphs total (2 for "AA", 3 for "AAA") - the whitespace token itself never
        // emits a glyph, so glyph count alone cannot distinguish 2 lines from 3; the decisive
        // signal is the Y position of "AAA"'s own glyphs.
        Assert.Equal(5, layout.Glyphs.Count);

        // Line 1 ("AA"): ascent 10160, at y = 0 + 10160.
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        Assert.Equal(6350f, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(10160f, layout.Glyphs[1].OriginYEmu, 2);

        // Line 2 (" AAA", the leading space carried forward, not its own line): line height
        // 12700, baseline at 12700 + 10160 = 22860 - exactly 2 lines' worth of vertical offset.
        // Without the fix, the spurious whitespace-only line would push "AAA" to a third line's
        // offset instead: 2 * 12700 + 10160 = 35100.
        Assert.Equal(2540f, layout.Glyphs[2].OriginXEmu, 2);
        Assert.Equal(22860f, layout.Glyphs[2].OriginYEmu, 2);
        Assert.Equal(8890f, layout.Glyphs[3].OriginXEmu, 2);
        Assert.Equal(22860f, layout.Glyphs[3].OriginYEmu, 2);
        Assert.Equal(15240f, layout.Glyphs[4].OriginXEmu, 2);
        Assert.Equal(22860f, layout.Glyphs[4].OriginYEmu, 2);
    }

    /// <summary>
    ///     Regression guard distinguishing the whitespace-only-line fix (above) from an explicit
    ///     <c>&lt;a:br/&gt;</c> break: two consecutive explicit breaks still produce a genuinely
    ///     blank middle line, since explicit breaks bypass <c>Pack</c>'s whitespace-content check
    ///     entirely (<c>PackTokensIntoLines</c> force-flushes the current line directly on an
    ///     <c>IsLineBreak</c> token, independent of word-wrap width).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_WordWrap_ExplicitBreakStillProducesBlankLine_NotAffectedByWhitespaceLineFix()
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
                new XElement(DrawingNs + "br"),
                new XElement(DrawingNs + "r", new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")), new XElement(DrawingNs + "t", "B"))));
        var textBody = PptxDocument.ParseTextBody(txBody);

        var layout = Layout(textBody, 50000f, 1_000_000f);

        // "A" on line 1, a genuinely blank line 2 (from the second <a:br/>), "B" on line 3 - 3
        // total lines, unaffected by the whitespace-only-line fix. The blank line has no tokens
        // of its own, so (pre-existing, unrelated behavior) it falls back to the nominal default
        // font size's own proportions (18pt => 228600 EMU) rather than this paragraph's own
        // resolved line height: height = 228600 * 1.2 = 274320. Line 3's baseline is therefore
        // line1Height (12700) + line2Height (274320) + ascent (10160) = 297180 - not
        // 2 * 12700 + 10160, which would (incorrectly) assume the blank line shares line 1's own
        // resolved height.
        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        Assert.Equal(12700f + 274320f + 10160f, layout.Glyphs[1].OriginYEmu, 2);
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

    #region Paragraph spacing (Phase 2 Follow-Up: Paragraph Spacing)

    /// <summary>
    ///     Proves a paragraph's own <c>&lt;a:spcAft&gt;</c> and the following paragraph's own
    ///     <c>&lt;a:spcBef&gt;</c> combine additively into the gap between them - previously
    ///     neither was ever applied during layout at all.
    /// </summary>
    /// <remarks>
    ///     "A" (paragraph 1) declares <c>spcAft</c> of 100 hundredths-of-point (12700 EMU);
    ///     "B" (paragraph 2) declares <c>spcBef</c> of 100 hundredths-of-point (12700 EMU).
    ///     Without the fix, paragraph 2's baseline would sit at
    ///     <c>lineHeight(para1) + ascent(para2) = 12700 + 10160 = 22860</c>. With the additive fix,
    ///     it sits an extra <c>12700 + 12700 = 25400</c> EMU lower:
    ///     <c>12700 + 25400 + 10160 = 48260</c>.
    /// </remarks>
    [Fact]
    public void ResolveTextLayout_TwoParagraphsWithSpcAftAndSpcBef_AddsAdditiveGapBetweenParagraphs()
    {
        var textBody = BuildTwoParagraphTextBody(
            "A",
            """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><spcAft><spcPts val="100" /></spcAft></pPr>""",
            "B",
            """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><spcBef><spcPts val="100" /></spcBef></pPr>""");

        var layout = Layout(textBody, 50000f, 1_000_000f);

        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        Assert.Equal(48260f, layout.Glyphs[1].OriginYEmu, 2);
    }

    /// <summary>
    ///     Proves <c>&lt;a:spcAft&gt;</c>/<c>&lt;a:spcBef&gt;</c> declared as a percentage
    ///     (<c>spcPct</c>) also combines additively, resolved against each boundary line's own
    ///     resolved <c>lineHeight</c> (12700 EMU for this synthetic font/size).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_TwoParagraphsWithPercentSpcAftAndSpcBef_AddsAdditiveGapBetweenParagraphs()
    {
        // 50% of lineHeight (12700) = 6350 each; combined additive gap = 12700.
        var textBody = BuildTwoParagraphTextBody(
            "A",
            """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><spcAft><spcPct val="50000" /></spcAft></pPr>""",
            "B",
            """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><spcBef><spcPct val="50000" /></spcBef></pPr>""");

        var layout = Layout(textBody, 50000f, 1_000_000f);

        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
        // 12700 (para1 line height) + 12700 (additive gap) + 10160 (para2 ascent) = 35560.
        Assert.Equal(35560f, layout.Glyphs[1].OriginYEmu, 2);
    }

    /// <summary>
    ///     Regression guard: a single paragraph declaring <c>&lt;a:spcBef&gt;</c> still positions
    ///     its own (only) line's baseline at the ordinary no-spacing Y - proving <c>spcBef</c> on
    ///     a text body's very first paragraph is correctly suppressed (the additive gap only
    ///     applies between paragraphs, never before the first).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_FirstParagraphSpcBef_DoesNotShiftFirstLineDown()
    {
        var textBody = BuildSingleRunTextBody(
            "A",
            paragraphExtra: """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><spcBef><spcPts val="100" /></spcBef></pPr>""");

        var layout = Layout(textBody, 50000f, 1_000_000f);

        Assert.Single(layout.Glyphs);
        Assert.Equal(10160f, layout.Glyphs[0].OriginYEmu, 2);
    }

    /// <summary>
    ///     Regression guard pairing with the above: a single paragraph declaring
    ///     <c>&lt;a:spcAft&gt;</c> does not change <see cref="PptxTextAnchor.Bottom"/>
    ///     positioning versus the same paragraph without <c>spcAft</c> - proving <c>spcAft</c> on
    ///     a text body's very last paragraph is correctly suppressed and does not leak into the
    ///     anchor/autofit total-height calculation.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_LastParagraphSpcAft_DoesNotAffectAnchorPositioning()
    {
        var withoutSpacing = BuildSingleRunTextBody("A", """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" anchor="b" />""");
        var withSpacing = BuildSingleRunTextBody(
            "A",
            """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" anchor="b" />""",
            """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><spcAft><spcPts val="100" /></spcAft></pPr>""");

        var layoutWithoutSpacing = Layout(withoutSpacing, 50000f, 100000f);
        var layoutWithSpacing = Layout(withSpacing, 50000f, 100000f);

        Assert.Equal(layoutWithoutSpacing.Glyphs[0].OriginYEmu, layoutWithSpacing.Glyphs[0].OriginYEmu, 2);
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

    /// <summary>
    ///     Proves a run-level <c>&lt;a:ln&gt;</c> text outline's <c>OutlineWidthEmu</c> and
    ///     <c>OutlineDashArray</c> survive <see cref="PptxDocument.ResolveTextLayout"/>'s own
    ///     autofit shrink loop (<c>BuildLines</c>) scaled by the same non-unit <c>fontScale</c>
    ///     applied to <c>SizeEmu</c> - a regression guard distinct from the direct-construction
    ///     <see cref="PptxTextRenderTests"/> (which never exercise <c>BuildLines</c> at all) and
    ///     the pre-shrink <see cref="PptxTextTests"/> inheritance tests (which stop before layout
    ///     ever runs), so neither suite would catch a regression that stopped copying
    ///     <c>OutlineWidthEmu</c>/<c>OutlineDashArray</c> into a glyph placement, or stopped
    ///     scaling either by <c>fontScale</c>, inside <c>BuildLines</c> itself. Reuses the same
    ///     five-single-line-paragraph/50000 EMU available-height shape as the shrink-loop test
    ///     above, which converges on <c>fontScale == 0.7</c>, with every run additionally
    ///     declaring <c>&lt;a:ln w="9525"&gt;</c> (a solid accent-colored stroke with a
    ///     <c>&lt;a:prstDash val="dash"/&gt;</c> pattern).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_NormAutofitShrinkWithRunOutline_ScalesOutlineWidthAndDashArrayByFontScale()
    {
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"),
            new XElement(DrawingNs + "normAutofit"));

        var ln = new XElement(
            DrawingNs + "ln",
            new XAttribute("w", "9525"),
            new XElement(DrawingNs + "solidFill", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "FF00FF"))),
            new XElement(DrawingNs + "prstDash", new XAttribute("val", "dash")));

        var paragraphs = Enumerable.Range(0, 5)
            .Select(_ => new XElement(
                DrawingNs + "p",
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100"), new XElement(ln)),
                    new XElement(DrawingNs + "t", "A"))))
            .ToArray();

        var txBody = new XElement(PresentationNs + "txBody", bodyPr, paragraphs);
        var textBody = PptxDocument.ParseTextBody(txBody);

        var layout = Layout(textBody, 50000f, 50000f);

        Assert.Equal(0.7f, layout.AppliedFontScale, 2);
        var glyph = layout.Glyphs[0];
        Assert.NotNull(glyph.OutlineWidthEmu);
        Assert.Equal(9525f * 0.7f, glyph.OutlineWidthEmu.Value, 2);
        Assert.NotNull(glyph.OutlineDashArray);
        Assert.Equal(new Rgba32(0xFF, 0x00, 0xFF, 0xFF), glyph.OutlineColor);
        Assert.Equal(2, glyph.OutlineDashArray.Count);
        Assert.Equal(9525f * 4f * 0.7f, glyph.OutlineDashArray[0], 2);
        Assert.Equal(9525f * 3f * 0.7f, glyph.OutlineDashArray[1], 2);
    }

    /// <summary>
    ///     Proves a <c>&lt;a:normAutofit fontScale="..."/&gt;</c> with a non-numeric
    ///     <c>fontScale</c> attribute is rejected with <see cref="InvalidDataException"/> rather
    ///     than letting the explicit <c>(float?)</c> cast's raw <see cref="FormatException"/>
    ///     propagate uncaught.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_NormAutofitNonNumericFontScale_ThrowsInvalidDataException()
    {
        var textBody = BuildSingleRunTextBody(
            "A",
            """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><normAutofit fontScale="not-a-number" /></bodyPr>""");

        Assert.Throws<InvalidDataException>(() => Layout(textBody, 50000f, 100000f));
    }

    /// <summary>
    ///     Proves a <c>&lt;a:normAutofit lnSpcReduction="..."/&gt;</c> with a non-numeric
    ///     <c>lnSpcReduction</c> attribute is rejected with <see cref="InvalidDataException"/>
    ///     rather than letting the explicit <c>(float?)</c> cast's raw <see cref="FormatException"/>
    ///     propagate uncaught.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_NormAutofitNonNumericLnSpcReduction_ThrowsInvalidDataException()
    {
        var textBody = BuildSingleRunTextBody(
            "A",
            """<bodyPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><normAutofit lnSpcReduction="not-a-number" /></bodyPr>""");

        Assert.Throws<InvalidDataException>(() => Layout(textBody, 50000f, 100000f));
    }

    #endregion

    #region Per-character glyph-coverage fallback

    // Synthetic fallback font: UnitsPerEm 1000, ascender 800, descender -200, lineGap 0.
    //   .notdef (index 0), advance 0, zero contours.
    //   U+00B1 PLUS-MINUS SIGN (index 1), advance 400 units, a distinct filled triangle outline.
    //   U+00B0 DEGREE SIGN (index 2), advance 300 units, a different, distinguishable filled
    //   triangle outline - every assertion below distinguishes "resolved via the fallback font"
    //   from "resolved via the primary font" purely by comparing the resolved
    //   PptxGlyphPlacement.Font reference and GlyphIndex, not by outline shape, so the two
    //   outlines only need to be non-empty (a real, paintable glyph), not visually distinct.
    private static TrueTypeFont NewFallbackFontCoveringPlusMinusAndDegree()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphPlusMinus = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (400, 0, true), (200, 800, true)]);
        var glyphDegree = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (300, 0, true), (150, 800, true)]);
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(0x00B1, 1), (0x00B0, 2)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(3))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 3))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 400, 300]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphPlusMinus.Length, glyphDegree.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphPlusMinus, .. glyphDegree])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    // Synthetic primary font: identical shape to NewFallbackFontCoveringPlusMinusAndDegree(), but
    // covering 'A'/' ' (like NewFont()) plus U+00B0 DEGREE SIGN only - deliberately NOT covering
    // U+00B1 PLUS-MINUS SIGN - used by the literal reported-symptom test (c) below, where the
    // primary font covers the *second* character of the "±°" pair but not the first.
    private static TrueTypeFont NewPrimaryFontCoveringDegreeButNotPlusMinus()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (500, 0, true), (250, 800, true)]);
        var glyphSpace = SyntheticFontBuilder.SimpleGlyph();
        var glyphDegree = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (300, 0, true), (150, 800, true)]);
        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1), (32, 2), (0x00B0, 3)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(4))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 4))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500, 200, 300]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length, glyphSpace.Length, glyphDegree.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA, .. glyphSpace, .. glyphDegree])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    private static PptxTextLayout LayoutWithFallback(
        PptxTextBody textBody,
        TrueTypeFont primaryFont,
        TrueTypeFont fallbackFont,
        float widthEmu = 500000f,
        float heightEmu = 500000f) =>
        PptxDocument.ResolveTextLayout(
            textBody,
            new PptxPlaceholderProperties(null, null, BuildTestTheme()),
            BuildTestTheme(),
            "body",
            widthEmu,
            heightEmu,
            (_, _, _) => primaryFont,
            colorMap: null,
            fallbackFontResolver: (_, _) => fallbackFont);

    /// <summary>
    ///     Proves the literal two-character tofu-box reproduction: a run's primary font covers
    ///     neither U+00B1 (PLUS-MINUS SIGN) nor U+00B0 (DEGREE SIGN) that immediately follows it,
    ///     but the injected bundled fallback font covers both. Both characters must independently
    ///     resolve to the fallback font with their own correct, non-<c>.notdef</c> glyph index -
    ///     proving the second character's (U+00B0's) resolution is driven purely by its own
    ///     coverage check against the fallback font, not "stuck" to or corrupted by the first
    ///     character's (U+00B1's) own fallback decision (every per-character
    ///     <c>CmapTable</c>/<c>GetGlyphIndex</c> lookup is already a stateless, pure function of
    ///     (font, codepoint) alone - see the planning report's refutation of the "sticky
    ///     fallback" hypothesis - this test additionally proves the new per-character fallback
    ///     layer built on top of it inherits that same independence).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_PlusMinusFollowedByDegree_BothNeitherInPrimary_BothIndependentlyResolveFallback()
    {
        var primaryFont = NewFont(); // Covers only 'A'/' ' - neither U+00B1 nor U+00B0.
        var fallbackFont = NewFallbackFontCoveringPlusMinusAndDegree();
        var textBody = BuildSingleRunTextBody("\u00B1\u00B0");

        var layout = LayoutWithFallback(textBody, primaryFont, fallbackFont);

        Assert.Equal(2, layout.Glyphs.Count);

        var plusMinusGlyph = layout.Glyphs[0];
        Assert.Same(fallbackFont, plusMinusGlyph.Font);
        Assert.NotEqual(0, plusMinusGlyph.GlyphIndex);
        Assert.Equal(fallbackFont.GetGlyphIndex('\u00B1'), plusMinusGlyph.GlyphIndex);

        var degreeGlyph = layout.Glyphs[1];
        Assert.Same(fallbackFont, degreeGlyph.Font);
        Assert.NotEqual(0, degreeGlyph.GlyphIndex);
        Assert.Equal(fallbackFont.GetGlyphIndex('\u00B0'), degreeGlyph.GlyphIndex);
    }

    /// <summary>
    ///     Baseline/control isolation test: a run containing only the fallback-triggering
    ///     character (U+00B1, with no following character) still resolves via the fallback font -
    ///     proving single-character fallback behavior in isolation, independent of the
    ///     two-character sequence case above.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_PlusMinusAlone_ResolvesFallbackGlyph()
    {
        var primaryFont = NewFont(); // Covers only 'A'/' ' - not U+00B1.
        var fallbackFont = NewFallbackFontCoveringPlusMinusAndDegree();
        var textBody = BuildSingleRunTextBody("\u00B1");

        var layout = LayoutWithFallback(textBody, primaryFont, fallbackFont);

        Assert.Single(layout.Glyphs);
        Assert.Same(fallbackFont, layout.Glyphs[0].Font);
        Assert.Equal(fallbackFont.GetGlyphIndex('\u00B1'), layout.Glyphs[0].GlyphIndex);
        Assert.NotEqual(0, layout.Glyphs[0].GlyphIndex);
    }

    /// <summary>
    ///     Proves the original bug report's exact scenario: the run's primary font lacks U+00B1
    ///     (PLUS-MINUS SIGN) but DOES cover the immediately-following U+00B0 (DEGREE SIGN). Both
    ///     characters must still resolve correctly - U+00B1 via the fallback font, U+00B0 via the
    ///     primary font - proving the primary font's own, already-correct coverage of the second
    ///     character is not corrupted or overridden by the first character's fallback
    ///     substitution. This directly refutes the "sticky fallback" hypothesis for the new
    ///     per-character fallback layer itself (not just the pre-existing single-font-per-run
    ///     resolution path the planning report already refuted it for).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_PlusMinusFollowedByDegree_PrimaryCoversOnlyDegree_DegreeStaysOnPrimaryFont()
    {
        var primaryFont = NewPrimaryFontCoveringDegreeButNotPlusMinus();
        var fallbackFont = NewFallbackFontCoveringPlusMinusAndDegree();
        var textBody = BuildSingleRunTextBody("\u00B1\u00B0");

        var layout = LayoutWithFallback(textBody, primaryFont, fallbackFont);

        Assert.Equal(2, layout.Glyphs.Count);

        // U+00B1: missing from the primary font - resolves via the fallback font.
        var plusMinusGlyph = layout.Glyphs[0];
        Assert.Same(fallbackFont, plusMinusGlyph.Font);
        Assert.Equal(fallbackFont.GetGlyphIndex('\u00B1'), plusMinusGlyph.GlyphIndex);
        Assert.NotEqual(0, plusMinusGlyph.GlyphIndex);

        // U+00B0: already covered by the primary font - stays on the primary font, proving the
        // preceding character's fallback substitution did not "stick"/leak onto this character.
        var degreeGlyph = layout.Glyphs[1];
        Assert.Same(primaryFont, degreeGlyph.Font);
        Assert.Equal(primaryFont.GetGlyphIndex('\u00B0'), degreeGlyph.GlyphIndex);
        Assert.NotEqual(0, degreeGlyph.GlyphIndex);

        // Baseline check: without any fallback font configured at all (the pre-fix/default
        // production behavior for a font lacking a fallback resolver), U+00B0 alone still
        // resolves via the primary font exactly the same way - confirming the primary font's own
        // coverage of U+00B0 is what resolves it, not an accidental fallback-font side effect.
        var degreeOnlyBody = BuildSingleRunTextBody("\u00B0");
        var degreeOnlyLayout = PptxDocument.ResolveTextLayout(
            degreeOnlyBody,
            new PptxPlaceholderProperties(null, null, BuildTestTheme()),
            BuildTestTheme(),
            "body",
            500000f,
            500000f,
            (_, _, _) => primaryFont);
        Assert.Single(degreeOnlyLayout.Glyphs);
        Assert.Same(primaryFont, degreeOnlyLayout.Glyphs[0].Font);
        Assert.Equal(primaryFont.GetGlyphIndex('\u00B0'), degreeOnlyLayout.Glyphs[0].GlyphIndex);
    }

    /// <summary>
    ///     One-off, non-assertion visual repro generator (not part of the permanent regression
    ///     suite's guarantees - see the three <c>[Fact]</c> tests above for the actual permanent
    ///     xunit assertions): renders the "±° tofu box" reproduction to an inspectable PNG file
    ///     under <c>.agent-logs/</c>, so a reviewer can visually confirm the fix. Top row: the
    ///     broken "before" behavior (primary font alone, no coverage fallback at all) - both
    ///     characters paint as empty <c>.notdef</c> tofu boxes. Bottom row: the fixed "after"
    ///     behavior (this change's per-character fallback) - both characters paint their correct,
    ///     distinct fallback-font glyph shapes.
    /// </summary>
    [Fact]
    public void GeneratePlusMinusDegreeTofuReproPng()
    {
        var primaryFont = NewFont(); // Covers only 'A'/' ' - neither U+00B1 nor U+00B0.
        var fallbackFont = NewFallbackFontCoveringPlusMinusAndDegree();

        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"));
        var txBody = new XElement(
            PresentationNs + "txBody",
            bodyPr,
            new XElement(
                DrawingNs + "p",
                new XElement(DrawingNs + "pPr"),
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "7200")),
                    new XElement(DrawingNs + "t", "\u00B1\u00B0"))));
        var textBody = PptxDocument.ParseTextBody(txBody);

        const float widthEmu = 900000f;
        const float heightEmu = 700000f;
        const float scale = 300f / widthEmu; // -> 300x~233px surface.

        // "Before" layout: the fallback resolver returns the SAME primary font - i.e. no wider-
        // coverage font is ever consulted, reproducing the pre-fix behavior where a run is
        // permanently bound to a single resolved font for every character.
        var beforeLayout = PptxDocument.ResolveTextLayout(
            textBody,
            new PptxPlaceholderProperties(null, null, BuildTestTheme()),
            BuildTestTheme(),
            "body",
            widthEmu,
            heightEmu,
            (_, _, _) => primaryFont,
            colorMap: null,
            fallbackFontResolver: (_, _) => primaryFont);

        // "After" layout: this change's real per-character glyph-coverage fallback, with a
        // fallback font that actually covers both characters.
        var afterLayout = LayoutWithFallback(textBody, primaryFont, fallbackFont, widthEmu, heightEmu);

        using var surface = new Surface((int)(widthEmu * scale), (int)(heightEmu * scale * 2));
        for (var y = 0; y < surface.Height; y++)
        {
            var row = surface.GetRowSpan(y);
            row.Fill(new Rgba32(255, 255, 255, 255));
        }

        var topTransform = Matrix3x2.CreateScale(scale);
        PptxDocument.PaintTextLayout(surface, beforeLayout, topTransform);

        var bottomTransform = Matrix3x2.CreateScale(scale) *
            Matrix3x2.CreateTranslation(0f, heightEmu * scale);
        PptxDocument.PaintTextLayout(surface, afterLayout, bottomTransform);

        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".agent-logs");
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "pptx-degree-tofu-glyph-fallback-repro.png");

        // Three target-framework test processes may run this test concurrently against the
        // same shared output path - render to a process-unique temp file first (identical
        // content regardless of which TFM wins), then best-effort copy it into place, tolerating
        // (rather than failing on) a transient sharing violation from a sibling process doing the
        // same thing at the same moment.
        var tempPath = Path.Combine(outputDirectory, $"{Guid.NewGuid():N}.png.tmp");
        PngCodec.Save(surface, tempPath);
        try
        {
            File.Copy(tempPath, outputPath, overwrite: true);
        }
        catch (IOException)
        {
            // A sibling TFM process is writing/has already written the identical content -
            // nothing further to do here.
        }
        finally
        {
            File.Delete(tempPath);
        }

        Assert.True(File.Exists(outputPath));
    }

    #endregion

    #region Supplementary-plane characters (Finding 4: surrogate pairs)

    // Synthetic font: UnitsPerEm 1000, ascender 800, descender -200, lineGap 0, using a format-12
    // cmap (not format-4, which is BMP-only) so it can map a supplementary-plane codepoint.
    //   .notdef (index 0), advance 0, zero contours.
    //   'A' (codepoint 0x41, index 1), advance 500 units, non-empty outline.
    //   U+1F600 GRINNING FACE (index 2), advance 600 units, non-empty outline - a
    //   supplementary-plane codepoint (above U+FFFF), requiring a UTF-16 surrogate pair to
    //   represent as a .NET string.
    private static TrueTypeFont NewSupplementaryPlaneFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (500, 0, true), (250, 800, true)]);
        var glyphEmoji = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (600, 0, true), (300, 800, true)]);
        var cmap = SyntheticFontBuilder.CmapFormat12(3, 10, [(0x41, 0x41, 1), (0x1F600, 0x1F600, 2)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(3))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, 3))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 500, 600]))
            .AddTable("loca", SyntheticFontBuilder.Loca([notdef.Length, glyphA.Length, glyphEmoji.Length], longFormat: false))
            .AddTable("glyf", [.. notdef, .. glyphA, .. glyphEmoji])
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>
    ///     Proves a supplementary-plane character (a 2-<c>char</c> UTF-16 surrogate pair in the
    ///     .NET string, one Unicode scalar value) is measured/placed as exactly one logical glyph
    ///     unit - before the fix, enumerating by <c>char</c> would split it into two surrogate
    ///     halves, each independently resolved (almost certainly to <c>.notdef</c>, since no real
    ///     <c>cmap</c> subtable maps a lone surrogate value), emitting 2 glyphs instead of 1.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_SupplementaryPlaneCharacter_EmitsExactlyOneGlyph()
    {
        var font = NewSupplementaryPlaneFont();
        var emoji = char.ConvertFromUtf32(0x1F600);
        var textBody = BuildSingleRunTextBody(emoji);

        var layout = PptxDocument.ResolveTextLayout(
            textBody, new PptxPlaceholderProperties(null, null, BuildTestTheme()), BuildTestTheme(), "body",
            500000f, 500000f, (_, _, _) => font);

        Assert.Single(layout.Glyphs);
        Assert.Equal(font.GetGlyphIndex(0x1F600), layout.Glyphs[0].GlyphIndex);
        Assert.NotEqual(0, layout.Glyphs[0].GlyphIndex);
    }

    /// <summary>
    ///     Proves a supplementary-plane character's word-wrap-affecting measurement
    ///     (<c>MeasureTokenWidthEmu</c>) also treats it as one logical unit: a following BMP
    ///     character's own X origin must equal exactly one emoji glyph's own advance width, not
    ///     double it (the pre-fix defect, from measuring both surrogate halves independently) and
    ///     not some near-zero <c>.notdef</c>-derived width.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_SupplementaryPlaneCharacter_MeasuresSingleAdvanceWidth()
    {
        var font = NewSupplementaryPlaneFont();
        var emoji = char.ConvertFromUtf32(0x1F600);
        var textBody = BuildSingleRunTextBody(emoji + "A");

        var layout = PptxDocument.ResolveTextLayout(
            textBody, new PptxPlaceholderProperties(null, null, BuildTestTheme()), BuildTestTheme(), "body",
            500000f, 500000f, (_, _, _) => font);

        Assert.Equal(2, layout.Glyphs.Count);
        // Emoji advance at sz=100 (SizeEmu 12700): 600/1000 * 12700 = 7620 - exactly one emoji
        // glyph's own advance, not 2x (double-counted surrogate halves) nor ~0 (notdef advance).
        Assert.Equal(0f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(7620f, layout.Glyphs[1].OriginXEmu, 2);
    }

    /// <summary>
    ///     Mirrors <see cref="ResolveTextLayout_PlusMinusAlone_ResolvesFallbackGlyph"/>, substituting
    ///     a supplementary-plane codepoint for <c>'\u00B1'</c>: proves the per-character
    ///     glyph-coverage fallback path (<c>ResolveGlyph</c>) also operates on whole Unicode scalar
    ///     values, not surrogate halves - a primary font lacking the codepoint falls back to
    ///     exactly one glyph from the bundled fallback font, not two independent (and
    ///     independently-failing) surrogate-half fallback lookups.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_SupplementaryPlaneCharacterMissingFromPrimaryFont_FallsBackCorrectly()
    {
        var primaryFont = NewFont(); // Covers only 'A'/' ' - not U+1F600.
        var fallbackFont = NewSupplementaryPlaneFont();
        var emoji = char.ConvertFromUtf32(0x1F600);
        var textBody = BuildSingleRunTextBody(emoji);

        var layout = LayoutWithFallback(textBody, primaryFont, fallbackFont);

        Assert.Single(layout.Glyphs);
        Assert.Same(fallbackFont, layout.Glyphs[0].Font);
        Assert.Equal(fallbackFont.GetGlyphIndex(0x1F600), layout.Glyphs[0].GlyphIndex);
        Assert.NotEqual(0, layout.Glyphs[0].GlyphIndex);
    }

    #endregion

    #region Underlines (Phase 2 Follow-Up: Underline Rendering)

    /// <summary>Proves an underlined run emits exactly one <see cref="PptxUnderlineSegment"/> spanning the run's own measured width, with matching color/size.</summary>
    [Fact]
    public void ResolveTextLayout_UnderlinedRun_EmitsOneSegmentSpanningRunWidth()
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
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100"), new XAttribute("u", "sng")),
                    new XElement(DrawingNs + "t", "A"))));
        var textBody = PptxDocument.ParseTextBody(txBody);

        var layout = Layout(textBody, 50000f, 100000f);

        // "A" at sz=100 -> SizeEmu 12700; width = 500/1000*12700 = 6350.
        var segment = Assert.Single(layout.Underlines);
        Assert.Equal(0f, segment.StartXEmu, 2);
        Assert.Equal(6350f, segment.EndXEmu, 2);
        Assert.Equal(12700f, segment.SizeEmu, 2);
        Assert.Equal(PptxUnderlineStyle.Single, segment.Style);
        Assert.Equal(layout.Glyphs[0].Color, segment.Color);
    }

    /// <summary>Proves a non-underlined run emits no underline segment at all.</summary>
    [Fact]
    public void ResolveTextLayout_NonUnderlinedRun_EmitsNoSegment()
    {
        var textBody = BuildSingleRunTextBody("A");

        var layout = Layout(textBody, 50000f, 100000f);

        Assert.Empty(layout.Underlines);
    }

    /// <summary>
    ///     Proves that, of two runs on one line, only the first underlined, exactly one segment
    ///     is emitted (the span flushes at the run boundary rather than extending into the
    ///     second, non-underlined run).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_TwoRunsOnlyFirstUnderlined_EmitsExactlyOneSegment()
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
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100"), new XAttribute("u", "sng")),
                    new XElement(DrawingNs + "t", "A")),
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")),
                    new XElement(DrawingNs + "t", "A"))));
        var textBody = PptxDocument.ParseTextBody(txBody);

        var layout = Layout(textBody, 50000f, 100000f);

        var segment = Assert.Single(layout.Underlines);
        Assert.Equal(0f, segment.StartXEmu, 2);
        Assert.Equal(6350f, segment.EndXEmu, 2);
    }

    /// <summary>
    ///     Proves a single underlined run with interior whitespace (for example "A A") has its
    ///     underline span cover the interior space too, rather than stopping at the first word -
    ///     the span is a property of the run, not of each individual word-wrap token.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_UnderlinedRunWithInteriorSpace_SpanCoversSpace()
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
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100"), new XAttribute("u", "sng")),
                    new XElement(DrawingNs + "t", "A A"))));
        var textBody = PptxDocument.ParseTextBody(txBody);

        // "A A" at sz=100 -> SizeEmu 12700: 'A' 6350 + ' ' 2540 + 'A' 6350 = 15240 total width.
        var layout = Layout(textBody, 50000f, 100000f);

        var segment = Assert.Single(layout.Underlines);
        Assert.Equal(0f, segment.StartXEmu, 2);
        Assert.Equal(15240f, segment.EndXEmu, 2);
    }

    #endregion
}
