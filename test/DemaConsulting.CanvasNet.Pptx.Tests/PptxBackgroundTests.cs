using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore srgbclr schemeclr pptx bgpr bgref phclr srgb hlink Hlink Calibri reparents

/// <summary>
///     Unit-level tests for the Phase 2 Follow-Up slide/layout/master background-fill resolver
///     (<c>PptxDocument.Background.cs</c>'s <see cref="PptxDocument.ResolveSlideBackgroundFill"/>)
///     and the theme's new <c>&lt;a:fmtScheme&gt;/&lt;a:bgFillStyleLst&gt;</c> parsing
///     (<c>PptxDocument.Theme.cs</c>'s <see cref="PptxDocument.GetTheme"/>, exercised indirectly
///     here via directly-constructed <see cref="PptxTheme"/> instances). Every resolver under test
///     is a plain static method operating on directly-constructed <see cref="XElement"/>
///     fragments, mirroring <see cref="PptxPaintTests"/>'s own established pattern - pixel-level,
///     end-to-end coverage (including real <c>.pptx</c> fixtures) lives in
///     <see cref="PptxRenderTests"/> and <see cref="PptxFixturesCorpusTests"/> instead.
/// </summary>
public class PptxBackgroundTests
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>Builds an arbitrary, fully-populated test <see cref="PptxTheme"/>, optionally with a <c>&lt;a:bgFillStyleLst&gt;</c>.</summary>
    private static PptxTheme BuildTestTheme(IReadOnlyList<XElement>? bgFillStyleList = null) =>
        new(
            new PptxColorScheme(
                Dark1: new Rgba32(0x10, 0x10, 0x10, 255),
                Light1: new Rgba32(0xF0, 0xF0, 0xF0, 255),
                Dark2: new Rgba32(0x20, 0x20, 0x20, 255),
                Light2: new Rgba32(0xE0, 0xE0, 0xE0, 255),
                Accent1: new Rgba32(0x11, 0x22, 0x33, 255),
                Accent2: new Rgba32(0x44, 0x55, 0x66, 255),
                Accent3: new Rgba32(0x77, 0x88, 0x99, 255),
                Accent4: new Rgba32(0xAA, 0xBB, 0xCC, 255),
                Accent5: new Rgba32(0x01, 0x02, 0x03, 255),
                Accent6: new Rgba32(0x04, 0x05, 0x06, 255),
                Hyperlink: new Rgba32(0x07, 0x08, 0x09, 255),
                FollowedHyperlink: new Rgba32(0x0A, 0x0B, 0x0C, 255)),
            new PptxFontScheme(
                new PptxFontCollection("MajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("MinorLatin", "MinorEA", "MinorCS")),
            bgFillStyleList);

    /// <summary>Builds a <c>&lt;p:bg&gt;&lt;p:bgPr&gt;&lt;a:solidFill&gt;</c> element with the given solid color.</summary>
    private static XElement SolidBg(string hex) =>
        new(P + "bg", new XElement(P + "bgPr", new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", hex)))));

    // --- ResolveSlideBackgroundFill: slide -> layout -> master precedence -----------------------

    /// <summary>Resolve Slide Background Fill - All Three Tiers Null - Returns Null.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_AllThreeTiersNull_ReturnsNull()
    {
        var paint = PptxDocument.ResolveSlideBackgroundFill(null, null, null, BuildTestTheme(), 9144000, 6858000);

        Assert.Null(paint);
    }

    /// <summary>Resolve Slide Background Fill - Slide Declares One - Resolves Slide's Own Background.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_SlideDeclaresOne_ResolvesSlideOwnBackground()
    {
        var paint = PptxDocument.ResolveSlideBackgroundFill(
            SolidBg("FF0000"), SolidBg("00FF00"), SolidBg("0000FF"), BuildTestTheme(), 9144000, 6858000);

        var solidFill = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0xFF, 0x00, 0x00, 255), solidFill.Color);
    }

    /// <summary>Resolve Slide Background Fill - Slide Declares None Layout Does - Resolves Layout Own Background.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_SlideDeclaresNoneLayoutDoes_ResolvesLayoutOwnBackground()
    {
        var paint = PptxDocument.ResolveSlideBackgroundFill(
            null, SolidBg("00FF00"), SolidBg("0000FF"), BuildTestTheme(), 9144000, 6858000);

        var solidFill = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0x00, 0xFF, 0x00, 255), solidFill.Color);
    }

    /// <summary>Resolve Slide Background Fill - Slide And Layout Declare None Master Does - Resolves Master Own Background.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_SlideAndLayoutDeclareNoneMasterDoes_ResolvesMasterOwnBackground()
    {
        var paint = PptxDocument.ResolveSlideBackgroundFill(
            null, null, SolidBg("0000FF"), BuildTestTheme(), 9144000, 6858000);

        var solidFill = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0x00, 0x00, 0xFF, 255), solidFill.Color);
    }

    /// <summary>Resolve Slide Background Fill - Empty Slide Background Still Wins Over Layout - Matches Placeholder Inheritance Precedent.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_EmptySlideBackgroundStillWinsOverLayout_MatchesPlaceholderInheritancePrecedent()
    {
        // An empty-but-present <p:bg/> at a higher tier still "wins" (and, having neither
        // <p:bgPr> nor <p:bgRef>, throws) over a lower tier's own, well-formed <p:bg> - mirroring
        // the "first element present at all wins" placeholder-inheritance precedent exactly.
        var emptySlideBackground = new XElement(P + "bg");

        Assert.Throws<InvalidDataException>(() =>
            PptxDocument.ResolveSlideBackgroundFill(
                emptySlideBackground, SolidBg("00FF00"), null, BuildTestTheme(), 9144000, 6858000));
    }

    /// <summary>Resolve Slide Background Fill - Bg Element With Neither Bg Pr Nor Bg Ref - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgElementWithNeitherBgPrNorBgRef_ThrowsInvalidDataException()
    {
        var malformedBackground = new XElement(P + "bg", new XElement(P + "unknownChild"));

        Assert.Throws<InvalidDataException>(() =>
            PptxDocument.ResolveSlideBackgroundFill(malformedBackground, null, null, BuildTestTheme(), 9144000, 6858000));
    }

    // --- ResolveSlideBackgroundFill: <p:bgRef> theme-indexed references -------------------------

    /// <summary>Resolve Slide Background Fill - Bg Ref Idx Zero - Returns No Fill.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefIdxZero_ReturnsNoFill()
    {
        var background = new XElement(P + "bg", new XElement(P + "bgRef", new XAttribute("idx", "0"), new XElement(A + "srgbClr", new XAttribute("val", "FF00FF"))));

        var paint = PptxDocument.ResolveSlideBackgroundFill(background, null, null, BuildTestTheme(), 9144000, 6858000);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>Resolve Slide Background Fill - Bg Ref Idx One Thousand - Returns No Fill.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefIdxOneThousand_ReturnsNoFill()
    {
        var background = new XElement(P + "bg", new XElement(P + "bgRef", new XAttribute("idx", "1000"), new XElement(A + "srgbClr", new XAttribute("val", "FF00FF"))));

        var paint = PptxDocument.ResolveSlideBackgroundFill(background, null, null, BuildTestTheme(), 9144000, 6858000);

        Assert.Same(PptxNoFill.Instance, paint);
    }

    /// <summary>Resolve Slide Background Fill - Bg Ref Idx In Fill Style List Range - Throws Pptx Unsupported Feature Exception.</summary>
    [Theory]
    [InlineData("1")]
    [InlineData("500")]
    [InlineData("999")]
    public void ResolveSlideBackgroundFill_BgRefIdxInFillStyleListRange_ThrowsPptxUnsupportedFeatureException(string idx)
    {
        var background = new XElement(P + "bg", new XElement(P + "bgRef", new XAttribute("idx", idx), new XElement(A + "srgbClr", new XAttribute("val", "FF00FF"))));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() =>
            PptxDocument.ResolveSlideBackgroundFill(background, null, null, BuildTestTheme(), 9144000, 6858000));
        Assert.Equal("pptx-bg-fill-style-ref", ex.Feature);
    }

    /// <summary>Resolve Slide Background Fill - Bg Ref Idx Resolves Bg Fill Style List Entry With Ph Clr Substitution.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefIdxResolvesBgFillStyleListEntryWithPhClrSubstitution()
    {
        // idx 1001 is the bgFillStyleLst's first (0-based) entry.
        var bgFillStyleList = new List<XElement>
        {
            new(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "phClr"))),
        };
        var theme = BuildTestTheme(bgFillStyleList);
        var background = new XElement(
            P + "bg",
            new XElement(P + "bgRef", new XAttribute("idx", "1001"), new XElement(A + "srgbClr", new XAttribute("val", "FF00FF"))));

        var paint = PptxDocument.ResolveSlideBackgroundFill(background, null, null, theme, 9144000, 6858000);

        var solidFill = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0xFF, 0x00, 0xFF, 255), solidFill.Color);
    }

    /// <summary>Resolve Slide Background Fill - Bg Ref Idx Second Entry - Resolves Second Bg Fill Style List Entry.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefIdxSecondEntry_ResolvesSecondBgFillStyleListEntry()
    {
        // idx 1002 is the bgFillStyleLst's second (0-based index 1) entry.
        var bgFillStyleList = new List<XElement>
        {
            new(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "111111"))),
            new(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "phClr"))),
        };
        var theme = BuildTestTheme(bgFillStyleList);
        var background = new XElement(
            P + "bg",
            new XElement(P + "bgRef", new XAttribute("idx", "1002"), new XElement(A + "srgbClr", new XAttribute("val", "00FF00"))));

        var paint = PptxDocument.ResolveSlideBackgroundFill(background, null, null, theme, 9144000, 6858000);

        var solidFill = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0x00, 0xFF, 0x00, 255), solidFill.Color);
    }

    /// <summary>
    ///     Resolving a <c>&lt;p:bgRef&gt;</c>'s matched <c>&lt;a:bgFillStyleLst&gt;</c> entry must
    ///     not mutate the theme's own, cached <see cref="PptxTheme.BgFillStyleList"/> tree (the
    ///     theme is reused across every slide in a document) - this is a regression test for the
    ///     "wrap the matched entry in a synthetic parent, which clones rather than reparents it"
    ///     approach <see cref="PptxDocument.ResolveSlideBackgroundFill"/>'s own remarks document.
    ///     Mirrors <see cref="PptxDocument.GetTheme"/>'s own production parsing exactly: the
    ///     matched entry must still have its original <c>&lt;a:bgFillStyleLst&gt;</c> parent (not
    ///     <see langword="null"/>, and not the synthetic wrapper) for this regression to be
    ///     meaningful.
    /// </summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefResolution_DoesNotMutateThemeBgFillStyleList()
    {
        var originalBgFillStyleLst = new XElement(
            A + "bgFillStyleLst",
            new XElement(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "phClr"))));
        var bgFillStyleList = originalBgFillStyleLst.Elements().ToList();
        var bgFillStyleListEntry = bgFillStyleList[0];
        var theme = BuildTestTheme(bgFillStyleList);
        var background = new XElement(
            P + "bg",
            new XElement(P + "bgRef", new XAttribute("idx", "1001"), new XElement(A + "srgbClr", new XAttribute("val", "FF00FF"))));

        _ = PptxDocument.ResolveSlideBackgroundFill(background, null, null, theme, 9144000, 6858000);

        Assert.Same(originalBgFillStyleLst, bgFillStyleListEntry.Parent);
        Assert.Single(theme.BgFillStyleList);
        Assert.Same(bgFillStyleListEntry, theme.BgFillStyleList[0]);
    }

    /// <summary>Resolve Slide Background Fill - Bg Ref Idx Past End Of Bg Fill Style List - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefIdxPastEndOfBgFillStyleList_ThrowsInvalidDataException()
    {
        var background = new XElement(
            P + "bg",
            new XElement(P + "bgRef", new XAttribute("idx", "1001"), new XElement(A + "srgbClr", new XAttribute("val", "FF00FF"))));

        Assert.Throws<InvalidDataException>(() =>
            PptxDocument.ResolveSlideBackgroundFill(background, null, null, BuildTestTheme(), 9144000, 6858000));
    }

    /// <summary>Resolve Slide Background Fill - Bg Ref With No Idx Attribute - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefWithNoIdxAttribute_ThrowsInvalidDataException()
    {
        var background = new XElement(P + "bg", new XElement(P + "bgRef", new XElement(A + "srgbClr", new XAttribute("val", "FF00FF"))));

        Assert.Throws<InvalidDataException>(() =>
            PptxDocument.ResolveSlideBackgroundFill(background, null, null, BuildTestTheme(), 9144000, 6858000));
    }

    /// <summary>Resolve Slide Background Fill - Bg Ref With Non Numeric Idx Attribute - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefWithNonNumericIdxAttribute_ThrowsInvalidDataException()
    {
        var background = new XElement(P + "bg", new XElement(P + "bgRef", new XAttribute("idx", "not-a-number"), new XElement(A + "srgbClr", new XAttribute("val", "FF00FF"))));

        Assert.Throws<InvalidDataException>(() =>
            PptxDocument.ResolveSlideBackgroundFill(background, null, null, BuildTestTheme(), 9144000, 6858000));
    }

    /// <summary>Resolve Slide Background Fill - Bg Ref With No Color Child - Resolves Entry With Default Ph Clr.</summary>
    [Fact]
    public void ResolveSlideBackgroundFill_BgRefWithNoColorChild_ResolvesEntryWithDefaultPhClr()
    {
        // A <p:bgRef> with no color child at all is schema-atypical but tolerated: phClr then
        // falls back to ResolveColor's own pre-existing default (Dark1) rather than throwing.
        var bgFillStyleList = new List<XElement>
        {
            new(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "phClr"))),
        };
        var theme = BuildTestTheme(bgFillStyleList);
        var background = new XElement(P + "bg", new XElement(P + "bgRef", new XAttribute("idx", "1001")));

        var paint = PptxDocument.ResolveSlideBackgroundFill(background, null, null, theme, 9144000, 6858000);

        var solidFill = Assert.IsType<PptxSolidFill>(paint);
        Assert.Equal(new Rgba32(0x10, 0x10, 0x10, 255), solidFill.Color);
    }

    // --- GetTheme: <a:fmtScheme>/<a:bgFillStyleLst> parsing --------------------------------------

    /// <summary>Get Theme - No Fmt Scheme - Bg Fill Style List Is Empty.</summary>
    [Fact]
    public void GetTheme_NoFmtScheme_BgFillStyleListIsEmpty()
    {
        using var stream = BuildThemeOnlyPackage(fmtSchemeXml: string.Empty);
        using var document = PptxDocument.Open(stream);

        var theme = document.GetTheme("ppt/theme/theme1.xml");

        Assert.Empty(theme.BgFillStyleList);
    }

    /// <summary>Get Theme - Fmt Scheme With No Bg Fill Style Lst - Bg Fill Style List Is Empty.</summary>
    [Fact]
    public void GetTheme_FmtSchemeWithNoBgFillStyleLst_BgFillStyleListIsEmpty()
    {
        using var stream = BuildThemeOnlyPackage(fmtSchemeXml: """<a:fmtScheme name="Test"><a:fillStyleLst/></a:fmtScheme>""");
        using var document = PptxDocument.Open(stream);

        var theme = document.GetTheme("ppt/theme/theme1.xml");

        Assert.Empty(theme.BgFillStyleList);
    }

    /// <summary>Get Theme - Fmt Scheme With Bg Fill Style Lst - Parses Entries In Document Order.</summary>
    [Fact]
    public void GetTheme_FmtSchemeWithBgFillStyleLst_ParsesEntriesInDocumentOrder()
    {
        const string fmtSchemeXml =
            """
            <a:fmtScheme name="Test">
              <a:bgFillStyleLst>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"><a:lumMod val="110000"/></a:schemeClr></a:solidFill>
              </a:bgFillStyleLst>
            </a:fmtScheme>
            """;
        using var stream = BuildThemeOnlyPackage(fmtSchemeXml);
        using var document = PptxDocument.Open(stream);

        var theme = document.GetTheme("ppt/theme/theme1.xml");

        Assert.Equal(2, theme.BgFillStyleList.Count);
        Assert.Equal(A + "solidFill", theme.BgFillStyleList[0].Name);
        Assert.Equal(A + "solidFill", theme.BgFillStyleList[1].Name);
    }

    /// <summary>Builds a minimal, navigable presentation package whose single slide master's theme declares the given <c>&lt;a:fmtScheme&gt;</c> inner XML (or none, when empty).</summary>
    private static Stream BuildThemeOnlyPackage(string fmtSchemeXml)
    {
        const string contentTypesXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
            </Types>
            """;

        const string packageRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml" />
            </Relationships>
            """;

        const string presentationXml =
            """
            <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:sldSz cx="9144000" cy="6858000"/>
              <p:sldIdLst><p:sldId id="256" r:id="rId2"/></p:sldIdLst>
            </p:presentation>
            """;

        const string presentationRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml" />
            </Relationships>
            """;

        const string slideXml =
            """
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
            </p:sld>
            """;

        const string slideRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
            </Relationships>
            """;

        const string layoutXml =
            """
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
            </p:sldLayout>
            """;

        const string layoutRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml" />
            </Relationships>
            """;

        const string masterXml =
            """
            <p:sldMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
              <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2"/>
              <p:txStyles/>
            </p:sldMaster>
            """;

        const string masterRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml" />
            </Relationships>
            """;

        var themeXml =
            $"""
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="TestTheme">
              <a:themeElements>
                <a:clrScheme name="Test">
                  <a:dk1><a:srgbClr val="101010"/></a:dk1>
                  <a:lt1><a:srgbClr val="F0F0F0"/></a:lt1>
                  <a:dk2><a:srgbClr val="202020"/></a:dk2>
                  <a:lt2><a:srgbClr val="E0E0E0"/></a:lt2>
                  <a:accent1><a:srgbClr val="4472C4"/></a:accent1>
                  <a:accent2><a:srgbClr val="ED7D31"/></a:accent2>
                  <a:accent3><a:srgbClr val="A5A5A5"/></a:accent3>
                  <a:accent4><a:srgbClr val="FFC000"/></a:accent4>
                  <a:accent5><a:srgbClr val="5B9BD5"/></a:accent5>
                  <a:accent6><a:srgbClr val="70AD47"/></a:accent6>
                  <a:hlink><a:srgbClr val="0563C1"/></a:hlink>
                  <a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
                </a:clrScheme>
                <a:fontScheme name="TestFonts">
                  <a:majorFont><a:latin typeface="Calibri Light"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
                  <a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
                </a:fontScheme>
                {fmtSchemeXml}
              </a:themeElements>
            </a:theme>
            """;

        var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteTextEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteTextEntry(archive, "_rels/.rels", packageRelsXml);
            WriteTextEntry(archive, "ppt/presentation.xml", presentationXml);
            WriteTextEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
            WriteTextEntry(archive, "ppt/slides/slide1.xml", slideXml);
            WriteTextEntry(archive, "ppt/slides/_rels/slide1.xml.rels", slideRelsXml);
            WriteTextEntry(archive, "ppt/slideLayouts/slideLayout1.xml", layoutXml);
            WriteTextEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", layoutRelsXml);
            WriteTextEntry(archive, "ppt/slideMasters/slideMaster1.xml", masterXml);
            WriteTextEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", masterRelsXml);
            WriteTextEntry(archive, "ppt/theme/theme1.xml", themeXml);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteTextEntry(System.IO.Compression.ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, System.Text.Encoding.UTF8);
        writer.Write(content);
    }
}
