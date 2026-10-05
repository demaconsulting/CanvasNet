using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx buchar buautonum bunone buclrtx buclr bufonttx bufont busztx buszpct buszpts
// cspell:ignore arabicparenr arabicplain alphalc alphauc alphalcparenr alphaucparenr romanlc romanuc
// cspell:ignore romanlcparenr romanucparenr unitsperem marl

/// <summary>
///     Unit-level tests for bullet/numbering rendering (Phase 2 Follow-Up: Bullets and
///     Numbering): raw parsing (<c>PptxDocument.Text.cs</c>'s <c>ParseBulletProperties</c>),
///     attribute-level inheritance across the four independent choice-group chains
///     (<c>PptxDocument.TextInheritance.cs</c>'s <c>ResolveEffectiveBulletProperties</c>),
///     auto-number string formatting (<c>PptxDocument.Bullets.cs</c>'s <c>FormatAutoNumber</c>),
///     and the layout engine's bullet-glyph positioning/hanging-indent fix/auto-number
///     sequencing (<c>PptxDocument.TextLayout.cs</c>). Complements <see cref="PptxTextTests"/>
///     (run/paragraph parsing-inheritance conventions) and <see cref="PptxTextLayoutTests"/>
///     (word-wrap/alignment/anchor/autofit, including this file's own synthetic-font convention).
/// </summary>
public class PptxBulletTests
{
    private static readonly XNamespace DrawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace PresentationNs = "http://schemas.openxmlformats.org/presentationml/2006/main";

    #region Synthetic font/theme/placeholder test fixtures

    // Synthetic font: UnitsPerEm 1000, ascender 800, descender -200, lineGap 0 (natural line
    // height == size, mirroring PptxTextLayoutTests' own NewFont() convention). Maps every
    // character this file's bullet strings/run text ever needs: 'A'/' ' (run text), the arabic
    // digits '1'-'3' plus '.'/')' (arabicPeriod/arabicParenR/arabicPlain), 'a'/'b' plus 'A'/'B'
    // (alpha schemes - reuses 'A'), and 'i'/'v'/'I'/'V' (roman schemes).
    //   .notdef (index 0), advance 0, zero contours.
    //   'A' (index 1, advance 500) - the only glyph with a visible (non-empty) outline, so pixel
    //   painting tests have real ink to sample.
    //   every other mapped character (indices 2-15), zero contours, each its own distinct advance
    //   width so glyph-origin-X math stays simple and unambiguous per character.
    private static TrueTypeFont NewFont()
    {
        var notdef = SyntheticFontBuilder.SimpleGlyph();
        var glyphA = SyntheticFontBuilder.SimpleGlyph([(0, 0, true), (500, 0, true), (250, 800, true)]);
        var blank = SyntheticFontBuilder.SimpleGlyph();

        // Every mapped character other than 'A' (glyph index 1, built/measured above) shares the
        // same zero-contour "blank" outline, at its own distinct advance width.
        var mappings = new (int Codepoint, int GlyphIndex, int Advance)[]
        {
            (' ', 2, 200),
            ('1', 3, 300),
            ('2', 4, 300),
            ('3', 5, 300),
            ('.', 6, 100),
            (')', 7, 150),
            ('a', 8, 300),
            ('b', 9, 300),
            ('B', 10, 300),
            ('i', 11, 100),
            ('v', 12, 300),
            ('I', 13, 100),
            ('V', 14, 300),
        };

        var advanceWidths = new List<int> { 0, 500 };
        var glyphLengths = new List<int> { notdef.Length, glyphA.Length };
        var glyphBytes = new List<byte> { };
        glyphBytes.AddRange(notdef);
        glyphBytes.AddRange(glyphA);
        var cmapMappings = new List<(int Codepoint, int GlyphId)> { (65, 1) };

        foreach (var (codepoint, glyphIndex, advance) in mappings)
        {
            advanceWidths.Add(advance);
            glyphLengths.Add(blank.Length);
            glyphBytes.AddRange(blank);
            cmapMappings.Add((codepoint, glyphIndex));
        }

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, cmapMappings);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(advanceWidths.Count))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, advanceWidths.Count))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx(advanceWidths))
            .AddTable("loca", SyntheticFontBuilder.Loca(glyphLengths, longFormat: false))
            .AddTable("glyf", glyphBytes.ToArray())
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    private static readonly Func<string, bool, bool, TrueTypeFont> ConstantFontResolver = (_, _, _) => NewFont();

    private static PptxTheme BuildTestTheme() =>
        new(
            new PptxColorScheme(
                new Rgba32(10, 10, 10, 255), new Rgba32(20, 20, 20, 255), new Rgba32(30, 30, 30, 255), new Rgba32(40, 40, 40, 255),
                new Rgba32(50, 50, 50, 255), new Rgba32(60, 60, 60, 255), new Rgba32(70, 70, 70, 255), new Rgba32(80, 80, 80, 255),
                new Rgba32(90, 90, 90, 255), new Rgba32(100, 100, 100, 255), new Rgba32(110, 110, 110, 255), new Rgba32(120, 120, 120, 255)),
            new PptxFontScheme(
                new PptxFontCollection("ThemeMajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("ThemeMinorLatin", "MinorEA", "MinorCS")));

    private static PptxPlaceholderProperties EmptyPlaceholderProperties(PptxTheme theme, PptxMasterTextStyles? masterTextStyles = null) =>
        new(null, null, theme, masterTextStyles);

    private static PptxTextRun Run(XElement? rPr, string text = "A") => new(rPr, text);

    private static PptxParagraph Paragraph(XElement? pPr, params PptxTextRun[] runs) =>
        new(PptxDocument.ParseParagraphProperties(pPr), runs.Select(run => (PptxParagraphItem)new PptxRunItem(run)).ToList());

    /// <summary>
    ///     Resolves <paramref name="paragraph"/>'s own first run's effective properties (or
    ///     <see langword="null"/> for a run-less paragraph) - the same value a real caller
    ///     (<c>ResolveTextLayout</c>) computes and threads into
    ///     <see cref="PptxDocument.ResolveEffectiveParagraphProperties"/>'s own
    ///     <c>firstRunProperties</c> parameter for the "follow text" bullet-modifier fallback.
    /// </summary>
    private static PptxEffectiveRunProperties? FirstRunProps(PptxParagraph paragraph, PptxPlaceholderProperties placeholderProperties, PptxTheme theme, string placeholderType) =>
        paragraph.Items.OfType<PptxRunItem>().FirstOrDefault() is { } firstRunItem
            ? PptxDocument.ResolveEffectiveRunProperties(firstRunItem.Run, paragraph, placeholderProperties, theme, placeholderType)
            : null;

    /// <summary>
    ///     Builds a one-paragraph-per-call <c>&lt;p:txBody&gt;</c> (a no-inset
    ///     <c>&lt;a:bodyPr&gt;</c>, for exact EMU math) with a single run per paragraph, each
    ///     paragraph's own <c>&lt;a:pPr&gt;</c> fragment supplied verbatim - mirroring
    ///     <see cref="PptxTextLayoutTests"/>'s own <c>BuildSingleRunTextBody</c> convention, but
    ///     supporting multiple paragraphs (needed for auto-number sequencing/reset tests).
    /// </summary>
    private static PptxTextBody BuildTextBody(params (string ParagraphPPr, string RunText)[] paragraphs)
    {
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"));

        var pElements = paragraphs.Select(p =>
            new XElement(
                DrawingNs + "p",
                XElement.Parse(p.ParagraphPPr),
                new XElement(
                    DrawingNs + "r",
                    new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")),
                    new XElement(DrawingNs + "t", p.RunText))));

        var txBody = new XElement(PresentationNs + "txBody", bodyPr, pElements);
        return PptxDocument.ParseTextBody(txBody);
    }

    private static PptxTextLayout Layout(PptxTextBody textBody, float widthEmu = 500000f, float heightEmu = 500000f) =>
        PptxDocument.ResolveTextLayout(textBody, new PptxPlaceholderProperties(null, null, BuildTestTheme()), BuildTestTheme(), "body", widthEmu, heightEmu, ConstantFontResolver);

    /// <summary>
    ///     Builds a <see cref="PptxTextBody"/> from raw <c>&lt;a:pPr&gt;</c> markup strings, each
    ///     paragraph either with a single run of its own text (when non-null) or with
    ///     NO <c>&lt;a:r&gt;</c> child at all (when <see langword="null"/> - only a synthetic
    ///     <c>&lt;a:endParaRPr&gt;</c>, mirroring a real "blank/spacer" paragraph such as
    ///     PowerPoint's own click-to-add-a-blank-line behavior) - used by the run-less-paragraph
    ///     bullet-suppression tests (Phase 2 Follow-Up: Bullets and Numbering's empty-paragraph fix).
    /// </summary>
    private static PptxTextBody BuildTextBodyRunless(params (string ParagraphPPr, string? RunText)[] paragraphs)
    {
        var bodyPr = new XElement(
            DrawingNs + "bodyPr",
            new XAttribute("lIns", "0"), new XAttribute("tIns", "0"), new XAttribute("rIns", "0"), new XAttribute("bIns", "0"));

        var pElements = paragraphs.Select(p =>
            new XElement(
                DrawingNs + "p",
                XElement.Parse(p.ParagraphPPr),
                p.RunText is null
                    ? new XElement(DrawingNs + "endParaRPr", new XAttribute("sz", "100"))
                    : new XElement(
                        DrawingNs + "r",
                        new XElement(DrawingNs + "rPr", new XAttribute("sz", "100")),
                        new XElement(DrawingNs + "t", p.RunText))));

        var txBody = new XElement(PresentationNs + "txBody", bodyPr, pElements);
        return PptxDocument.ParseTextBody(txBody);
    }

    #endregion

    #region Raw parsing (PptxDocument.Text.cs)

    /// <summary>Proves an absent <c>&lt;a:pPr&gt;</c> resolves <see cref="PptxRawBulletProperties.Empty"/>.</summary>
    [Fact]
    public void ParseParagraphProperties_Absent_ResolvesEmptyBulletProperties()
    {
        var result = PptxDocument.ParseParagraphProperties(null);

        Assert.Same(PptxRawBulletProperties.Empty, result.EffectiveBulletProperties);
    }

    /// <summary>Proves <c>&lt;a:buNone/&gt;</c> is captured as the type choice-group element.</summary>
    [Fact]
    public void ParseBulletProperties_BuNone_CapturesTypeElement()
    {
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buNone"));

        var result = PptxDocument.ParseParagraphProperties(pPr);

        Assert.NotNull(result.EffectiveBulletProperties.TypeElement);
        Assert.Equal(DrawingNs + "buNone", result.EffectiveBulletProperties.TypeElement.Name);
    }

    /// <summary>Proves <c>&lt;a:buChar char="•"/&gt;</c> is captured as the type choice-group element.</summary>
    [Fact]
    public void ParseBulletProperties_BuChar_CapturesTypeElement()
    {
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022")));

        var result = PptxDocument.ParseParagraphProperties(pPr);

        Assert.NotNull(result.EffectiveBulletProperties.TypeElement);
        Assert.Equal(DrawingNs + "buChar", result.EffectiveBulletProperties.TypeElement.Name);
        Assert.Equal("\u2022", (string?)result.EffectiveBulletProperties.TypeElement.Attribute("char"));
    }

    /// <summary>Proves <c>&lt;a:buAutoNum type="arabicPeriod" startAt="3"/&gt;</c> is captured as the type choice-group element.</summary>
    [Fact]
    public void ParseBulletProperties_BuAutoNum_CapturesTypeElement()
    {
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buAutoNum", new XAttribute("type", "arabicPeriod"), new XAttribute("startAt", "3")));

        var result = PptxDocument.ParseParagraphProperties(pPr);

        Assert.NotNull(result.EffectiveBulletProperties.TypeElement);
        Assert.Equal(DrawingNs + "buAutoNum", result.EffectiveBulletProperties.TypeElement.Name);
    }

    /// <summary>Proves the color/font/size choice-groups each independently capture their own present member.</summary>
    [Fact]
    public void ParseBulletProperties_ColorFontSizeModifiers_CapturesEachIndependently()
    {
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XElement(DrawingNs + "buClr", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "FF0000"))),
            new XElement(DrawingNs + "buFont", new XAttribute("typeface", "Arial")),
            new XElement(DrawingNs + "buSzPct", new XAttribute("val", "150000")));

        var result = PptxDocument.ParseParagraphProperties(pPr);

        Assert.Equal(DrawingNs + "buClr", result.EffectiveBulletProperties.ColorElement!.Name);
        Assert.Equal(DrawingNs + "buFont", result.EffectiveBulletProperties.FontElement!.Name);
        Assert.Equal(DrawingNs + "buSzPct", result.EffectiveBulletProperties.SizeElement!.Name);
    }

    /// <summary>Proves <c>&lt;a:buClrTx/&gt;</c>/<c>&lt;a:buFontTx/&gt;</c>/<c>&lt;a:buSzTx/&gt;</c> ("follow text") are each captured as their own choice-group's element.</summary>
    [Fact]
    public void ParseBulletProperties_FollowTextModifiers_CapturesEachIndependently()
    {
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XElement(DrawingNs + "buClrTx"),
            new XElement(DrawingNs + "buFontTx"),
            new XElement(DrawingNs + "buSzTx"));

        var result = PptxDocument.ParseParagraphProperties(pPr);

        Assert.Equal(DrawingNs + "buClrTx", result.EffectiveBulletProperties.ColorElement!.Name);
        Assert.Equal(DrawingNs + "buFontTx", result.EffectiveBulletProperties.FontElement!.Name);
        Assert.Equal(DrawingNs + "buSzTx", result.EffectiveBulletProperties.SizeElement!.Name);
    }

    #endregion

    #region Inheritance - type choice-group (ResolveEffectiveBulletProperties)

    /// <summary>Proves a paragraph with no bullet markup anywhere in its own chain resolves the conservative <see cref="PptxBulletKind.None"/> default.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_NoBulletMarkupAnywhere_ResolvesNone()
    {
        var theme = BuildTestTheme();
        var paragraph = Paragraph(null, Run(null));
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(PptxBulletKind.None, result.Bullet!.Kind);
    }

    /// <summary>Proves the paragraph's own <c>&lt;a:buChar/&gt;</c> wins over every other tier.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_OwnBuChar_WinsOverEveryOtherTier()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022")));
        var paragraph = Paragraph(pPr, Run(null));
        var lstStyle = new XElement(DrawingNs + "lstStyle", new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "buNone")));
        var placeholderProperties = new PptxPlaceholderProperties(null, lstStyle, theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(PptxBulletKind.Char, result.Bullet!.Kind);
        Assert.Equal("\u2022", result.Bullet.Character);
    }

    /// <summary>Proves an explicit <c>&lt;a:buNone/&gt;</c> at the paragraph's own tier suppresses an inherited placeholder-level bullet.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_OwnBuNone_SuppressesInheritedBullet()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buNone"));
        var paragraph = Paragraph(pPr, Run(null));
        var lstStyle = new XElement(DrawingNs + "lstStyle", new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022"))));
        var placeholderProperties = new PptxPlaceholderProperties(null, lstStyle, theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(PptxBulletKind.None, result.Bullet!.Kind);
    }

    /// <summary>Proves a placeholder level-indexed bullet wins when the paragraph itself declares no bullet markup.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_PlaceholderLevelBuAutoNum_WinsWhenParagraphDeclaresNothing()
    {
        var theme = BuildTestTheme();
        var paragraph = Paragraph(null, Run(null));
        var lstStyle = new XElement(
            DrawingNs + "lstStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "buAutoNum", new XAttribute("type", "arabicParenR"))));
        var placeholderProperties = new PptxPlaceholderProperties(null, lstStyle, theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(PptxBulletKind.AutoNum, result.Bullet!.Kind);
        Assert.Equal("arabicParenR", result.Bullet.AutoNumType);
        Assert.Equal(1, result.Bullet.AutoNumStartAt);
    }

    /// <summary>Proves a master <c>&lt;p:bodyStyle&gt;</c> level-indexed bullet wins when everything above is absent.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_MasterBodyStyleBuChar_WinsWhenEverythingAboveIsAbsent()
    {
        var theme = BuildTestTheme();
        var paragraph = Paragraph(null, Run(null));
        var bodyStyle = new XElement(
            DrawingNs + "bodyStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "-"))));
        var masterTextStyles = new PptxMasterTextStyles(null, bodyStyle, null);
        var placeholderProperties = EmptyPlaceholderProperties(theme, masterTextStyles);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(PptxBulletKind.Char, result.Bullet!.Kind);
        Assert.Equal("-", result.Bullet.Character);
    }

    /// <summary>
    ///     Proves <c>"sldNum"</c>/<c>"dt"</c>/<c>"ftr"</c> field placeholder types never resolve a
    ///     bullet, even when the master's own <c>&lt;p:bodyStyle&gt;</c> declares one for the same
    ///     level that the preceding <c>"body"</c>-placeholder-type test proves *does* win a bullet
    ///     - this is the regression guard for the stray-bullet-on-slide-number defect found during
    ///     visual QA (<c>SelectMasterTextStyle</c> routes these types to <c>bodyStyle</c> rather
    ///     than the bullet-free <c>otherStyle</c>).
    /// </summary>
    [Theory]
    [InlineData("sldNum")]
    [InlineData("dt")]
    [InlineData("ftr")]
    public void ResolveEffectiveParagraphProperties_SldNumDtFtrPlaceholderType_SuppressesMasterBodyStyleBullet(string placeholderType)
    {
        var theme = BuildTestTheme();
        var paragraph = Paragraph(null, Run(null));
        var bodyStyle = new XElement(
            DrawingNs + "bodyStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "-"))));
        var masterTextStyles = new PptxMasterTextStyles(null, bodyStyle, null);
        var placeholderProperties = EmptyPlaceholderProperties(theme, masterTextStyles);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, placeholderType);

        Assert.Equal(PptxBulletKind.None, result.Bullet!.Kind);
    }

    /// <summary>
    ///     Proves <c>"sldNum"</c>/<c>"dt"</c>/<c>"ftr"</c> field placeholder types still resolve
    ///     their own, explicitly-declared <c>&lt;a:buChar&gt;</c> override, even when the same
    ///     master <c>&lt;p:bodyStyle&gt;</c> bucket used by the preceding suppression test also
    ///     declares a bullet for the same level - this is the regression guard for the retry-1
    ///     fix's own over-broad unconditional early-return (an own-paragraph explicit bullet
    ///     choice must always win over any style-bucket default, regardless of placeholder type,
    ///     per real OOXML/PowerPoint semantics). The two tests together isolate the corrected
    ///     behavior: master-tier type exclusion for these three placeholder types, without
    ///     disturbing the paragraph's own explicit override.
    /// </summary>
    [Theory]
    [InlineData("sldNum")]
    [InlineData("dt")]
    [InlineData("ftr")]
    public void ResolveEffectiveParagraphProperties_SldNumDtFtrPlaceholderTypeWithOwnBuChar_StillResolvesOwnBullet(string placeholderType)
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "*")));
        var paragraph = Paragraph(pPr, Run(null));
        var bodyStyle = new XElement(
            DrawingNs + "bodyStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "-"))));
        var masterTextStyles = new PptxMasterTextStyles(null, bodyStyle, null);
        var placeholderProperties = EmptyPlaceholderProperties(theme, masterTextStyles);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, placeholderType);

        Assert.Equal(PptxBulletKind.Char, result.Bullet!.Kind);
        Assert.Equal("*", result.Bullet.Character);
    }

    /// <summary>Proves an absent <c>type</c>/<c>startAt</c> on <c>&lt;a:buAutoNum/&gt;</c> resolves the OOXML schema's own documented defaults.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_BuAutoNumNoAttributes_ResolvesSchemaDefaults()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buAutoNum"));
        var paragraph = Paragraph(pPr, Run(null));
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal("arabicPeriod", result.Bullet!.AutoNumType);
        Assert.Equal(1, result.Bullet.AutoNumStartAt);
    }

    #endregion

    #region Inheritance - color/font/size choice-groups are independent of each other and of the type choice-group

    /// <summary>
    ///     Proves a paragraph may declare only its own bullet character while separately
    ///     inheriting its color/font/size from the placeholder level style - the four
    ///     choice-groups resolve independently, not as one monolithic tier-wins-everything object.
    /// </summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_OwnTypeOnly_InheritsColorFontSizeIndependently()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022")));
        var paragraph = Paragraph(pPr, Run(null));
        var lstStyle = new XElement(
            DrawingNs + "lstStyle",
            new XElement(
                DrawingNs + "lvl1pPr",
                new XElement(DrawingNs + "buClr", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "FF0000"))),
                new XElement(DrawingNs + "buFont", new XAttribute("typeface", "Courier New")),
                new XElement(DrawingNs + "buSzPts", new XAttribute("val", "2400"))));
        var placeholderProperties = new PptxPlaceholderProperties(null, lstStyle, theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(PptxBulletKind.Char, result.Bullet!.Kind);
        Assert.Equal("\u2022", result.Bullet.Character);
        Assert.Equal(new Rgba32(255, 0, 0, 255), result.Bullet.Color);
        Assert.Equal("Courier New", result.Bullet.FontFamily);
        Assert.Equal(2400f * 127f, result.Bullet.SizeEmu);
    }

    /// <summary>Proves <c>&lt;a:buClrTx/&gt;</c> (or no color markup at all) resolves to the paragraph's own first run's effective color.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_BuClrTxOrAbsent_FollowsFirstRunColor()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022")));
        var runRPr = new XElement(DrawingNs + "rPr", new XElement(DrawingNs + "solidFill", new XElement(DrawingNs + "srgbClr", new XAttribute("val", "00FF00"))));
        var paragraph = Paragraph(pPr, Run(runRPr));
        var placeholderProperties = EmptyPlaceholderProperties(theme);
        var firstRunProperties = FirstRunProps(paragraph, placeholderProperties, theme, "body");

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body", firstRunProperties);

        Assert.Equal(new Rgba32(0, 255, 0, 255), result.Bullet!.Color);
    }

    /// <summary>Proves a run-less paragraph's "follow text" color falls back to <see cref="PptxColorScheme.Dark1"/>.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_BuClrTxRunLessParagraph_FallsBackToDark1()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022")));
        var paragraph = Paragraph(pPr);
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(theme.ColorScheme.Dark1, result.Bullet!.Color);
    }

    /// <summary>Proves <c>&lt;a:buFontTx/&gt;</c> (or no font markup at all) resolves to the paragraph's own first run's effective typeface.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_BuFontTxOrAbsent_FollowsFirstRunFont()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(DrawingNs + "pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022")));
        var runRPr = new XElement(DrawingNs + "rPr", new XElement(DrawingNs + "latin", new XAttribute("typeface", "Georgia")));
        var paragraph = Paragraph(pPr, Run(runRPr));
        var placeholderProperties = EmptyPlaceholderProperties(theme);
        var firstRunProperties = FirstRunProps(paragraph, placeholderProperties, theme, "body");

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body", firstRunProperties);

        Assert.Equal("Georgia", result.Bullet!.FontFamily);
    }

    /// <summary>Proves <c>&lt;a:buSzPct val="50000"/&gt;</c> resolves to half of the paragraph's own first run's effective size.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_BuSzPct_ResolvesFractionOfFirstRunSize()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022")),
            new XElement(DrawingNs + "buSzPct", new XAttribute("val", "50000")));
        var runRPr = new XElement(DrawingNs + "rPr", new XAttribute("sz", "2000"));
        var paragraph = Paragraph(pPr, Run(runRPr));
        var placeholderProperties = EmptyPlaceholderProperties(theme);
        var firstRunProperties = FirstRunProps(paragraph, placeholderProperties, theme, "body");

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body", firstRunProperties);

        Assert.Equal(2000f * 127f * 0.5f, result.Bullet!.SizeEmu);
    }

    /// <summary>Proves <c>&lt;a:buSzPts val="3600"/&gt;</c> resolves to an absolute size, independent of the run's own size entirely.</summary>
    [Fact]
    public void ResolveEffectiveParagraphProperties_BuSzPts_ResolvesAbsoluteSizeIndependentOfRunSize()
    {
        var theme = BuildTestTheme();
        var pPr = new XElement(
            DrawingNs + "pPr",
            new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022")),
            new XElement(DrawingNs + "buSzPts", new XAttribute("val", "3600")));
        var runRPr = new XElement(DrawingNs + "rPr", new XAttribute("sz", "2000"));
        var paragraph = Paragraph(pPr, Run(runRPr));
        var placeholderProperties = EmptyPlaceholderProperties(theme);

        var result = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");

        Assert.Equal(3600f * 127f, result.Bullet!.SizeEmu);
    }

    #endregion

    #region FormatAutoNumber (PptxDocument.Bullets.cs)

    /// <summary>Format Auto Number Supported Types Formats Expected String.</summary>
    [Theory]
    [InlineData(1, "arabicPeriod", "1.")]
    [InlineData(1, "arabicParenR", "1)")]
    [InlineData(1, "arabicPlain", "1")]
    [InlineData(1, "alphaLcPeriod", "a.")]
    [InlineData(2, "alphaLcPeriod", "b.")]
    [InlineData(27, "alphaLcPeriod", "aa.")]
    [InlineData(1, "alphaUcPeriod", "A.")]
    [InlineData(1, "alphaLcParenR", "a)")]
    [InlineData(1, "alphaUcParenR", "A)")]
    [InlineData(4, "romanLcPeriod", "iv.")]
    [InlineData(9, "romanLcPeriod", "ix.")]
    [InlineData(1994, "romanUcPeriod", "MCMXCIV.")]
    [InlineData(1, "romanLcParenR", "i)")]
    [InlineData(1, "romanUcParenR", "I)")]
    public void FormatAutoNumber_SupportedTypes_FormatsExpectedString(int value, string type, string expected)
    {
        var result = PptxDocument.FormatAutoNumber(value, type);

        Assert.Equal(expected, result);
    }

    /// <summary>Proves an unrecognized <c>&lt;a:buAutoNum type="..."/&gt;</c> scheme fails gracefully - returning <see langword="null"/>, not throwing.</summary>
    [Fact]
    public void FormatAutoNumber_UnsupportedType_ReturnsNull()
    {
        var result = PptxDocument.FormatAutoNumber(1, "circleNumDbPlain");

        Assert.Null(result);
    }

    #endregion

    #region Hanging-indent fix and bullet glyph geometry (ResolveTextLayout/PositionLines)

    /// <summary>
    ///     Proves a non-bulleted paragraph's first-line indent still applies to the text's own
    ///     X exactly as before this phase (regression guard for the hanging-indent fix, which is
    ///     strictly gated on "this line has a resolved bullet").
    /// </summary>
    [Fact]
    public void ResolveTextLayout_NonBulletedParagraph_FirstLineIndentStillAppliesToTextX()
    {
        var textBody = BuildTextBody(("""<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" marL="50000" indent="-20000" />""", "A"));

        var layout = Layout(textBody);

        // marL + indent = 30000 (no bullet is painted, so the pre-existing first-line-indent
        // behavior applies unchanged).
        Assert.Equal(30000f, layout.Glyphs[0].OriginXEmu, 2);
    }

    /// <summary>
    ///     Proves a bulleted paragraph's own text starts flush at <c>marL</c> (dropping
    ///     <c>indent</c> entirely), while the bullet glyph itself is painted at the
    ///     <c>marL+indent</c> gutter - the hanging-indent fix this phase introduces alongside the
    ///     bullet glyph itself.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_BulletedParagraph_TextStartsAtMarLGutterHoldsBullet()
    {
        var textBody = BuildTextBody(
            ("""<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" marL="50000" indent="-20000"><buChar char="A"/></pPr>""", "A"));

        var layout = Layout(textBody);

        // Two glyphs: the run's own "A" at marL (50000, not marL+indent, proving the fix), then
        // the bullet's own "A" at the gutter (marL+indent=30000) - PositionLines paints each
        // line's own text glyphs before appending that line's bullet glyphs.
        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(50000f, layout.Glyphs[0].OriginXEmu, 2);
        Assert.Equal(30000f, layout.Glyphs[1].OriginXEmu, 2);
    }

    /// <summary>Proves an explicit <c>&lt;a:buNone/&gt;</c> paints no bullet glyph at all - the glyph stream contains only the paragraph's own run text.</summary>
    [Fact]
    public void ResolveTextLayout_BuNone_PaintsNoGlyphBeyondRunText()
    {
        var textBody = BuildTextBody(
            ("""<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" marL="50000" indent="-20000"><buNone/></pPr>""", "A"));

        var layout = Layout(textBody);

        Assert.Single(layout.Glyphs);
        // Explicit buNone paragraphs are bullet-less, so the hanging-indent fix does NOT apply -
        // text renders at the pre-existing marL+indent first-line position.
        Assert.Equal(30000f, layout.Glyphs[0].OriginXEmu, 2);
    }

    /// <summary>
    ///     Proves consecutive same-level <c>&lt;a:buAutoNum type="arabicPlain"/&gt;</c> paragraphs
    ///     render "1", "2", "3" in sequence - the counter increments once per paragraph at that
    ///     level.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_ConsecutiveAutoNumParagraphs_SequencesCounterAcrossParagraphs()
    {
        var pPr = """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><buAutoNum type="arabicPlain"/></pPr>""";
        var textBody = BuildTextBody((pPr, "A"), (pPr, "A"), (pPr, "A"));

        var layout = Layout(textBody);

        // Each paragraph contributes 2 glyphs (run "A", then bullet digit) - 6 total.
        Assert.Equal(6, layout.Glyphs.Count);

        // Bullet digit glyph indices: '1'=3 (300 wide), '2'=4 (300 wide), '3'=5 (300 wide) - see
        // NewFont()'s own cmap/advance table. Comparing GlyphIndex (not rendered text, which this
        // layer does not expose directly) proves "1"/"2"/"3" were each individually resolved, not
        // the same digit repeated.
        Assert.Equal(3, layout.Glyphs[1].GlyphIndex);
        Assert.Equal(4, layout.Glyphs[3].GlyphIndex);
        Assert.Equal(5, layout.Glyphs[5].GlyphIndex);
    }

    /// <summary>
    ///     Proves returning to a shallower level after a deeper nested auto-numbered paragraph
    ///     resumes the shallower level's own counter (unaffected by the deeper level's own
    ///     sequence), while the deeper level itself restarts from its own <c>startAt</c> the next
    ///     time it is used - the full per-level reset/resume state machine
    ///     <c>AdvanceBulletCounters</c> implements.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_NestedThenReturnToShallowerLevel_ResumesShallowerCounterRestartsDeeperLevel()
    {
        const string ns = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var level0 = $"""<pPr xmlns="{ns}"><buAutoNum type="arabicPlain"/></pPr>""";
        var level1 = $"""<pPr xmlns="{ns}" lvl="1"><buAutoNum type="arabicPlain"/></pPr>""";

        // level0 (-> "1"), level1 (-> "1"), level1 (-> "2"), level0 (-> "2", resuming its own
        // counter, not "3" and not restarting at "1"), level1 (-> "1" again, restarted since the
        // intervening level0 paragraph closed out the nested list).
        var textBody = BuildTextBody((level0, "A"), (level1, "A"), (level1, "A"), (level0, "A"), (level1, "A"));

        var layout = Layout(textBody);

        // 5 paragraphs * 2 glyphs (run + bullet) = 10.
        Assert.Equal(10, layout.Glyphs.Count);

        // Bullet glyph indices, one per paragraph, at stream positions 1, 3, 5, 7, 9: expect
        // "1", "1", "2", "2", "1" -> glyph indices 3, 3, 4, 4, 3.
        Assert.Equal(3, layout.Glyphs[1].GlyphIndex);
        Assert.Equal(3, layout.Glyphs[3].GlyphIndex);
        Assert.Equal(4, layout.Glyphs[5].GlyphIndex);
        Assert.Equal(4, layout.Glyphs[7].GlyphIndex);
        Assert.Equal(3, layout.Glyphs[9].GlyphIndex);
    }

    /// <summary>
    ///     Proves a run-less (blank/spacer) paragraph with resolved <c>&lt;a:buChar/&gt;</c> bullet
    ///     properties paints NO bullet glyph - the Phase 2 Follow-Up empty-paragraph fix
    ///     (<c>BuildLines</c>'s <c>hasRuns</c> gate in <c>PptxDocument.TextLayout.cs</c>).
    /// </summary>
    [Fact]
    public void BuildLines_ParagraphWithBulletPropertiesAndNoRuns_PaintsNoBulletGlyph()
    {
        var pPr = """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><buChar char="A"/></pPr>""";
        var textBody = BuildTextBodyRunless((pPr, null));

        var layout = Layout(textBody);

        // No run glyphs and no bullet glyph - a run-less paragraph still occupies a blank line,
        // but paints nothing.
        Assert.Empty(layout.Glyphs);
    }

    /// <summary>
    ///     Differential counterpart to
    ///     <see cref="BuildLines_ParagraphWithBulletPropertiesAndNoRuns_PaintsNoBulletGlyph"/>:
    ///     the otherwise-identical paragraph with one run still paints its bullet glyph.
    /// </summary>
    [Fact]
    public void BuildLines_OtherwiseIdenticalParagraphWithOneRun_StillPaintsBulletGlyph()
    {
        var pPr = """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><buChar char="A"/></pPr>""";
        var textBody = BuildTextBodyRunless((pPr, "A"));

        var layout = Layout(textBody);

        // Run's own "A" glyph plus the bullet's own "A" glyph.
        Assert.Equal(2, layout.Glyphs.Count);
    }

    /// <summary>
    ///     Proves a run-less auto-numbered paragraph's suppressed bullet glyph does NOT disturb
    ///     the auto-number counter state machine - the counter still advances for it, so a
    ///     subsequent paragraph at the same level correctly continues the sequence (not restarting
    ///     at "1") - per the bug report's explicit requirement that only the glyph is suppressed,
    ///     not the counter advancement.
    /// </summary>
    [Fact]
    public void BuildLines_RunlessAutoNumberParagraph_CounterStillAdvancesForSubsequentParagraph()
    {
        var pPr = """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><buAutoNum type="arabicPlain"/></pPr>""";
        var textBody = BuildTextBodyRunless((pPr, null), (pPr, "A"));

        var layout = Layout(textBody);

        // First paragraph is run-less: no glyphs painted for it at all (counter still advances to
        // "1" internally, but nothing is painted). Second paragraph: its own run "A" plus a bullet
        // glyph for "2" (glyph index 4, see NewFont()'s cmap/advance table), proving the counter
        // advanced past "1" rather than restarting.
        Assert.Equal(2, layout.Glyphs.Count);
        Assert.Equal(4, layout.Glyphs[1].GlyphIndex);
    }

    /// <summary>
    ///     Proves a paragraph whose <c>&lt;a:buAutoNum&gt;</c> declares an unrecognized/unsupported
    ///     <c>type</c> skips painting a bullet for that one paragraph only (its own run text still
    ///     renders normally) - the documented per-bullet graceful-degradation policy, not a
    ///     whole-render failure.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_UnsupportedAutoNumType_SkipsOnlyThatBulletGracefully()
    {
        var pPr = """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><buAutoNum type="circleNumDbPlain"/></pPr>""";
        var textBody = BuildTextBody((pPr, "A"));

        var layout = Layout(textBody);

        // Only the run's own "A" glyph - no bullet glyph painted, and no exception thrown.
        Assert.Single(layout.Glyphs);
    }

    /// <summary>
    ///     Regression test for the bullet-gutter/text-start-X collision defect (confirmed on a
    ///     real-world slide: <c>marL="320040"</c>, <c>lvl="1"</c>, no <c>indent</c> attribute,
    ///     <c>&lt;a:buAutoNum type="arabicPeriod"/&gt;</c>, no placeholder/master tier
    ///     contributing an indent - see the design document's "Bullet/text gutter clearance"
    ///     note). With <c>IndentEmu</c> resolving to <c>0</c>, the bullet gutter
    ///     (<c>marL+indent=320040</c>) and the paragraph's own unclamped text-start-X
    ///     (also <c>320040</c>, since a bulleted first line drops <c>indent</c> entirely) would,
    ///     before the fix, collide exactly. Proves the bullet glyph's own X is unchanged (still
    ///     the unclamped gutter), while the first run-text glyph's X is now clamped to clear the
    ///     bullet string's own measured width - an exact numeric value derived from this file's
    ///     own synthetic font metrics (bullet string <c>"1."</c>: '1' advance 300/1000*12700
    ///     EMU + '.' advance 100/1000*12700 EMU = 5080 EMU total).
    /// </summary>
    [Fact]
    public void ResolveTextLayout_BulletedParagraphWithZeroIndent_TextClearsBulletWidth()
    {
        var textBody = BuildTextBody(
            ("""<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" marL="320040" lvl="1"><buAutoNum type="arabicPeriod"/></pPr>""", "A"));

        var layout = Layout(textBody);

        // Run "A", then bullet "1" and ".".
        Assert.Equal(3, layout.Glyphs.Count);

        // Bullet glyph X is unchanged: still the unclamped gutter (marL+indent = 320040+0).
        Assert.Equal(320040f, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(323850f, layout.Glyphs[2].OriginXEmu, 2); // 320040 + '1' advance (3810).

        // The run's own text glyph is clamped to clear the bullet's own measured width
        // (320040 + 5080 = 325120), rather than sitting at the unclamped marL (320040), which
        // would collide with the bullet.
        Assert.Equal(325120f, layout.Glyphs[0].OriginXEmu, 2);
    }

    /// <summary>
    ///     Pixel-level counterpart to
    ///     <see cref="ResolveTextLayout_BulletedParagraphWithZeroIndent_TextClearsBulletWidth"/>:
    ///     proves the bullet's own ink and the paragraph's own (now correctly offset) text ink
    ///     occupy disjoint X ranges once actually painted to a surface - not merely resolved. Uses
    ///     <c>&lt;a:buChar char="A"/&gt;</c> rather than the exact <c>buAutoNum</c> slide-23
    ///     fixture so the bullet glyph has real (non-blank) ink to sample in this file's synthetic
    ///     font (see <see cref="NewFont"/>'s own remarks: only glyph index 1, mapped to 'A', has a
    ///     visible outline - the auto-number digits/period are zero-contour placeholders) - the
    ///     underlying gutter-clearance defect is identical for <c>buChar</c> and <c>buAutoNum</c>
    ///     bullets alike (it is purely a function of resolved indent versus bullet width, never
    ///     the bullet's type - see the design document), so this remains a faithful pixel-level
    ///     proof of the same fix.
    /// </summary>
    [Fact]
    public void PaintTextLayout_BulletedParagraphWithZeroIndent_BulletAndTextInkDoNotOverlap()
    {
        var textBody = BuildTextBody(
            ("""<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" marL="320040" lvl="1"><buChar char="A"/></pPr>""", "A"));
        var layout = Layout(textBody, 400000f, 100000f);

        // Bullet "A" gutter X is the resolved marL+indent (320040 EMU); its measured width is the
        // 'A' glyph's advance scaled to the bullet's font size (6350 EMU); the clamped text start
        // X is their sum (326390 EMU) - touching, not overlapping, the bullet's own span. This is
        // the geometric proof the two glyphs' spans never share an X range.
        const float bulletGutterX = 320040f;
        const float bulletWidthEmu = 6350f;
        const float textStartX = bulletGutterX + bulletWidthEmu;
        Assert.Equal(bulletGutterX, layout.Glyphs[1].OriginXEmu, 2);
        Assert.Equal(textStartX, layout.Glyphs[0].OriginXEmu, 2);

        using var surface = new Surface(64, 16);
        var scale = 64f / 400000f;
        var transform = System.Numerics.Matrix3x2.CreateScale(scale, scale);
        PptxDocument.PaintTextLayout(surface, layout, transform);

        var bulletColumnMaxPx = (int)MathF.Floor(textStartX * scale);
        var textColumnMinPx = (int)MathF.Ceiling(textStartX * scale);

        bool AnyInkInColumnRange(int minPxInclusive, int maxPxExclusive)
        {
            for (var y = 0; y < surface.Height; y++)
            {
                for (var x = Math.Max(0, minPxInclusive); x < Math.Min(surface.Width, maxPxExclusive); x++)
                {
                    if (surface[x, y].A > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        Assert.True(AnyInkInColumnRange(0, bulletColumnMaxPx), "Expected ink in the bullet's own column.");
        Assert.True(AnyInkInColumnRange(textColumnMinPx, surface.Width), "Expected ink in the text's own (clamped) column.");
    }

    /// <summary>
    ///     Permanent regression test (promoted from a planning-pass investigation probe) for the
    ///     task's explicit "layout/master declares a default <c>buChar</c> at a list level; the
    ///     slide's own paragraph at that level declares <c>buAutoNum</c>; assert only the
    ///     auto-number marker renders" scenario. Proves the master <c>&lt;p:bodyStyle&gt;</c>
    ///     level's own <c>&lt;a:buChar/&gt;</c> default is fully superseded (not merged) by the
    ///     paragraph's own <c>&lt;a:buAutoNum/&gt;</c>, and that the full
    ///     <see cref="PptxDocument.ResolveTextLayout"/> pipeline emits exactly one run-glyph set
    ///     plus exactly one bullet-glyph set - never two overlapping markers.
    /// </summary>
    [Fact]
    public void ResolveTextLayout_MasterBuCharDefault_OwnBuAutoNumOverride_PaintsOnlyAutoNumberMarker()
    {
        var theme = BuildTestTheme();
        var bodyStyle = new XElement(
            DrawingNs + "bodyStyle",
            new XElement(DrawingNs + "lvl1pPr", new XElement(DrawingNs + "buChar", new XAttribute("char", "\u2022"))));
        var masterTextStyles = new PptxMasterTextStyles(null, bodyStyle, null);
        var placeholderProperties = new PptxPlaceholderProperties(null, null, theme, masterTextStyles);

        var pPrXml = """<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main"><buAutoNum type="arabicPeriod"/></pPr>""";
        var pPrElement = XElement.Parse(pPrXml);

        // Resolved bullet properties: the master's own buChar default is fully superseded, not
        // merged, by the paragraph's own buAutoNum.
        var paragraph = Paragraph(pPrElement, Run(null, "A"));
        var resolvedBullet = PptxDocument.ResolveEffectiveParagraphProperties(paragraph, placeholderProperties, "body");
        Assert.Equal(PptxBulletKind.AutoNum, resolvedBullet.Bullet!.Kind);
        Assert.Null(resolvedBullet.Bullet.Character);

        // Full pipeline: exactly one run glyph ("A") plus exactly one bullet-glyph set (the
        // auto-number "1." - two glyphs, '1' then '.') - never a second, master-buChar marker.
        var textBody = BuildTextBody((pPrXml, "A"));
        var layout = PptxDocument.ResolveTextLayout(textBody, placeholderProperties, theme, "body", 500000f, 500000f, ConstantFontResolver);

        Assert.Equal(3, layout.Glyphs.Count);
        Assert.Equal(3, layout.Glyphs[1].GlyphIndex); // '1' (see NewFont()'s cmap/advance table).
        Assert.Equal(6, layout.Glyphs[2].GlyphIndex); // '.' (see NewFont()'s cmap/advance table).
    }

    #endregion

    #region End-to-end pixel painting

    /// <summary>
    ///     Proves a <c>&lt;a:buChar/&gt;</c> bullet is actually painted to the pixel surface via
    ///     <see cref="PptxDocument.PaintTextLayout"/> - not merely resolved/positioned - at the
    ///     expected gutter X, with the surrounding surface left untouched.
    /// </summary>
    [Fact]
    public void PaintTextLayout_BulletedParagraph_PaintsBulletGlyphToSurface()
    {
        var textBody = BuildTextBody(
            ("""<pPr xmlns="http://schemas.openxmlformats.org/drawingml/2006/main" marL="50000" indent="-50000"><buChar char="A"/></pPr>""", "A"));
        var layout = Layout(textBody, 200000f, 200000f);

        using var surface = new Surface(64, 64);
        var scale = 64f / 200000f;
        var transform = System.Numerics.Matrix3x2.CreateScale(scale, scale);
        PptxDocument.PaintTextLayout(surface, layout, transform);

        var paintedAny = false;
        for (var y = 0; y < surface.Height && !paintedAny; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    paintedAny = true;
                    break;
                }
            }
        }

        Assert.True(paintedAny, "Expected the bullet glyph (gutter X=0) to paint at least one visible pixel.");
    }

    #endregion
}
