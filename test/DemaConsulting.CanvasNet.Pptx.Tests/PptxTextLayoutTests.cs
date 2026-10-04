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
