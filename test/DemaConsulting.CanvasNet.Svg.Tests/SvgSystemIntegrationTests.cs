// cspell:ignore Sfnt sfnt glyf cmap hhea hmtx letterboxes pillarboxes
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     System-level integration tests for SVG rasterization via the CanvasNet.Svg package.
///     Each test proves one <c>CanvasNetSvg-Lib-*</c> top-level requirement end-to-end through the
///     public <see cref="SvgCodec"/> API, complementing (never replacing) the unit-level
///     <c>SvgCodec_*</c> coverage in <see cref="SvgCodecTests"/>/<see cref="SvgFixtureTests"/>,
///     which exercises each feature's finer-grained behavioral variations.
/// </summary>
public class SvgSystemIntegrationTests
{
    /// <summary>Wraps <paramref name="svg"/> as a UTF-8 stream for <see cref="SvgCodec"/> to read.</summary>
    private static MemoryStream ToStream(string svg) => new(Encoding.UTF8.GetBytes(svg));

    /// <summary>
    ///     Builds a minimal, well-formed synthetic font with a single mapped 50x50-unit square
    ///     glyph ('A' at codepoint 65, glyph index 1), a 100-unit advance width, and a 100-unit
    ///     em-square, for controlled, predictable text-layout assertions.
    /// </summary>
    private static TrueTypeFont BuildTestFont()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (50, 0, true), (50, 50, true), (0, 50, true)]
        ]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(100, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(100, 0, 0, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 100]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, square.Length], longFormat: false))
            .AddTable("glyf", square)
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>
    ///     Builds a synthetic font identical in structure to <see cref="BuildTestFont"/> except the
    ///     'A' glyph is a wider 80x50-unit square (x equals 0 to 80, rather than 0 to 50), giving
    ///     font-weight face-selection tests a distinguishable "which face actually rendered" pixel
    ///     signature - a wide-glyph-only region between local x equals 50 and 80 - representing a
    ///     registered "bold" face.
    /// </summary>
    private static TrueTypeFont BuildBoldTestFont()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (80, 0, true), (80, 50, true), (0, 50, true)]
        ]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(100, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(2))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(100, 0, 0, 2))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 100]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, square.Length], longFormat: false))
            .AddTable("glyf", square)
            .AddTable("cmap", cmap)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>Builds a solid-color <paramref name="width"/>x<paramref name="height"/> surface.</summary>
    private static Surface BuildSolidSurface(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surface[x, y] = color;
            }
        }

        return surface;
    }

    /// <summary>Encodes <paramref name="surface"/> as a <c>data:image/png;base64,...</c> URI.</summary>
    private static string BuildPngDataUri(Surface surface)
    {
        using var pngStream = new MemoryStream();
        PngCodec.Save(surface, pngStream);
        return "data:image/png;base64," + Convert.ToBase64String(pngStream.ToArray());
    }

    /// <summary>
    ///     Proves that the system can rasterize an SVG document into a Surface through the public
    ///     API, producing the expected integrated pixel result for a shape filled with a solid
    ///     color. Unlike the BMP/PNG/TIFF/JPEG system-integration tests above, there is no "Save"
    ///     half to this round-trip: SvgCodec is decode/rasterize-only (see <c>SvgCodecTests</c>/
    ///     <c>SvgFixtureTests</c> for the full unit test coverage).
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgLoad_ReturnsExpectedPixel()
    {
        // Arrange: a minimal SVG document with a viewBox matching the requested raster exactly,
        // containing one rectangle filled with a distinct, fully opaque color
        const string svg = "<svg viewBox='0 0 10 10'><rect x='0' y='0' width='10' height='10' fill='rgb(11,22,33)'/></svg>";
        using var stream = ToStream(svg);

        // Act: rasterize the document onto a new Surface through the public API
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the system produces the expected integrated rasterized pixel value
        Assert.Equal(new Rgba32(11, 22, 33, 255), surface[5, 5]);
    }

    /// <summary>
    ///     Proves that the system can rasterize an SVG document whose color comes entirely from a
    ///     <c>&lt;style&gt;</c> element's CSS class selector - rather than a plain presentation
    ///     attribute - through the public API, integrating the whole CSS engine (style-element
    ///     parsing, selector matching, and the cascade chokepoint) with the rest of the rendering
    ///     pipeline. See <c>SvgCodecTests</c>/<c>SvgFixtureTests</c> for the full unit test
    ///     coverage of individual selectors/combinators/precedence tiers.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgLoadWithCssStyleElement_ReturnsExpectedPixel()
    {
        // Arrange: a minimal SVG document whose only source of color is a stylesheet class rule -
        // the rect itself carries no fill attribute at all
        const string svg = "<svg viewBox='0 0 10 10'><style>.solid { fill: rgb(11,22,33); }</style>" +
                            "<rect x='0' y='0' width='10' height='10' class='solid'/></svg>";
        using var stream = ToStream(svg);

        // Act: rasterize the document onto a new Surface through the public API
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the system produces the expected integrated rasterized pixel value
        Assert.Equal(new Rgba32(11, 22, 33, 255), surface[5, 5]);
    }

    /// <summary>
    ///     Proves that the system rasterizes a marker referenced from a line element's
    ///     <c>marker-end</c> attribute, placing it at the line's end vertex, through the public
    ///     API. See <c>SvgCodecTests</c> for the full unit test coverage of marker orientation,
    ///     scale, and reference-cycle rejection.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgMarkerRendering_RendersMarkerAtLineEnd()
    {
        // Arrange: a horizontal line from (0,5) to (8,5); its marker-end places a 4x4
        // "userSpaceOnUse" red square anchored at (2,2) (refX/refY), so at the (8,5) end vertex
        // the square occupies (6,3)-(10,7)
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <marker id='m' markerWidth='4' markerHeight='4' refX='2' refY='2' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='4' height='4' fill='red'/>
                </marker>
              </defs>
              <line x1='0' y1='5' x2='8' y2='5' stroke='black' stroke-width='1' marker-end='url(#m)'/>
            </svg>
            """;

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the marker's red square is visible past the line's own x2=8 end point
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[9, 5]);
    }

    /// <summary>
    ///     Proves that the system rasterizes a filter chain (feFlood/feComposite/feGaussianBlur/
    ///     feMerge) referenced from a shape's <c>filter</c> attribute, through the public API. See
    ///     <c>SvgCodecTests</c> for the full unit test coverage of the individual filter
    ///     primitives.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgFilterRendering_AppliesFilterChainToShape()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='orange' result='flood'/>
                  <feComposite in='flood' in2='SourceGraphic' operator='out' result='haloBase'/>
                  <feGaussianBlur in='haloBase' stdDeviation='2' result='halo'/>
                  <feMerge>
                    <feMergeNode in='halo'/>
                    <feMergeNode in='SourceGraphic'/>
                  </feMerge>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='green' filter='url(#f)'/>
            </svg>
            """;

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect's own fill remains fully opaque and unaffected at its center
        Assert.Equal(new Rgba32(0, 128, 0, 255), surface[50, 50]);
    }

    /// <summary>
    ///     Proves that the system rasterizes a <c>clipPath</c> element referenced from a shape's
    ///     <c>clip-path</c> attribute, hard-clipping to the referenced shape, through the public
    ///     API. See <c>SvgCodecTests</c> for the full unit test coverage of clip/mask unit systems
    ///     and effect ordering.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgClipPathRendering_ClipsContentToReferencedShape()
    {
        // Arrange: a 60x60 red rect at (20,20)-(80,80), clipped by a circle centered at (50,50)
        // with radius 20 (i.e. covering (30,30)-(70,70))
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <clipPath id='c'>
                  <circle cx='50' cy='50' r='20'/>
                </clipPath>
              </defs>
              <rect x='20' y='20' width='60' height='60' fill='red' clip-path='url(#c)'/>
            </svg>
            """;

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: inside the clip circle, the rect's own red fill is visible
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 50]);
        // Assert: outside the clip circle but inside the rect's own bounds, nothing was painted
        Assert.Equal(0, surface[22, 22].A);
    }

    /// <summary>
    ///     Proves that the system tiles a <c>pattern</c> element referenced from a shape's
    ///     <c>fill</c> attribute across the shape's own bounding box, through the public API. See
    ///     <c>SvgCodecTests</c> for the full unit test coverage of pattern unit systems, href
    ///     inheritance, and resource safety.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgPatternRendering_TilesPatternAcrossShape()
    {
        // Arrange: a 60x60 bounding box (20,20)-(80,80); the pattern's own width/height (1,1) are
        // objectBoundingBox-interpreted (the default patternUnits), so exactly one tile spans the
        // whole bounding box
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <pattern id='p' width='1' height='1'>
                  <rect x='44' y='44' width='12' height='12' fill='rgb(0,255,0)'/>
                </pattern>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='rgb(255,0,0)'/>
              <rect x='20' y='20' width='60' height='60' fill='url(#p)'/>
            </svg>
            """;

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the pattern's own tile content is visible inside the filled shape's bounding box
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[50, 50]);
    }

    /// <summary>
    ///     Proves that the system decodes and rasterizes an <c>image</c> element's base64-encoded
    ///     PNG data URI at the element's own placement rect, through the public API. See
    ///     <c>SvgCodecTests</c> for the full unit test coverage of other raster formats,
    ///     preserveAspectRatio, effects integration, and resource safety.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgImageRendering_RendersEmbeddedRasterImage()
    {
        // Arrange: a 2x2 solid blue PNG, placed at (20,20) with a 60x60 placement rect
        using var source = BuildSolidSurface(2, 2, new Rgba32(0, 0, 255, 255));
        var dataUri = BuildPngDataUri(source);
        var svg = $"""
            <svg viewBox='0 0 100 100'>
              <image href='{dataUri}' x='20' y='20' width='60' height='60' preserveAspectRatio='none'/>
            </svg>
            """;

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the decoded blue pixel is visible inside the placement rect
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[50, 50]);
        // Assert: outside the placement rect, nothing was painted
        Assert.Equal(0, surface[10, 10].A);
    }

    /// <summary>
    ///     Proves that the system fits a wide (landscape) <c>viewBox</c> into a square raster,
    ///     letterboxing with transparent bars above and below the centered content, through the
    ///     public API. See <c>SvgCodecTests</c> for the full unit test coverage of other
    ///     preserveAspectRatio align/meet-or-slice combinations.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgViewBoxFitting_LetterboxesWideViewBox()
    {
        // Arrange: 200x100 viewBox (2:1) into a 100x100 raster; scale equals 0.5, content
        // occupies y equals 25 to 75, leaving transparent bars above/below
        const string svg = "<svg viewBox='0 0 200 100'><rect x='0' y='0' width='200' height='100' fill='blue'/></svg>";

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: content band is filled
        Assert.Equal(255, surface[50, 50].A);
        // Assert: top and bottom letterbox bars are transparent
        Assert.Equal(0, surface[50, 5].A);
        Assert.Equal(0, surface[50, 95].A);
    }

    /// <summary>
    ///     Proves that the system resolves a <c>rect</c>'s <c>width</c> percentage against the
    ///     current viewport width rather than rejecting it, through the public API. See
    ///     <c>SvgCodecTests</c> for the full per-attribute-family basis matrix coverage.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgPercentageGeometryResolution_ResolvesWidthPercentage()
    {
        // Arrange: width="50%" of a 100-wide viewBox resolves to width=50; the rect spans x=[0,50)
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='50%' height='10' fill='red'/></svg>";

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the percentage-resolved width is honored
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 5]);
        Assert.Equal(0, surface[75, 5].A);
    }

    /// <summary>
    ///     Proves that the system reports an SVG document's resolved <c>viewBox</c> dimensions via
    ///     the public <see cref="SvgCodec.GetInfo(Stream)"/> entry point. See <c>SvgCodecTests</c>
    ///     for the full unit test coverage of the width/height/CSS-default fallback chain.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgGetInfo_ReturnsViewBoxDimensions()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 40 20' width='999' height='999'></svg>";

        // Act: query the document's intrinsic size through the public API
        using var stream = ToStream(svg);
        var info = SvgCodec.GetInfo(stream);

        // Assert: the viewBox dimensions win over the width/height attributes
        Assert.Equal(40, info.Width);
        Assert.Equal(20, info.Height);
        Assert.Equal(4, info.Channels);
        Assert.True(info.HasAlpha);
    }

    /// <summary>
    ///     Proves that the system renders an SVG <c>text</c> element's characters as glyph
    ///     outlines from a caller-supplied font, through the public API. See
    ///     <c>SvgCodecTests</c>/<c>SvgFixtureTests</c> for the full unit test coverage of
    ///     text-anchor, kerning, and font-family fallback.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgTextRendering_RendersGlyphAtExpectedPosition()
    {
        // Arrange: font-size equals the font's em-square (100), so scale is 1:1; the square glyph
        // for 'A' should render spanning x equals 10 to 60, y equals 10 to 60 (baseline at y=60)
        const string svg = "<svg viewBox='0 0 100 100'><text x='10' y='60' font-family='TestFont' " +
                            "font-size='100' fill='black'>A</text></svg>";
        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100, fonts);

        // Assert: inside the glyph square
        Assert.Equal(255, surface[35, 35].A);
        // Assert: outside the glyph square (above baseline extent and off to the side)
        Assert.Equal(0, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that the system selects a registered bold face over a registered normal face for
    ///     a text element with <c>font-weight="bold"</c>, through the public
    ///     <see cref="SvgCodec.LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>
    ///     entry point. See <c>SvgCodecTests</c> for the full unit test coverage of font-style
    ///     matching and legacy single-font-per-family compatibility.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgFontWeightStyleMatching_SelectsBoldFaceOverNormalFace()
    {
        // Arrange: font-size equals the shared 100-unit em-square, so scale is 1:1. The narrow
        // face fills canvas x equals 10 to 60; the wide face fills canvas x equals 10 to 90 - x
        // equals 75 is filled only by the wide (bold) face.
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' font-weight='bold' fill='black'>A</text>
              <text x='10' y='95' font-family='TestFont' font-size='100' fill='black'>A</text>
            </svg>
            """;
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildTestFont(), Weight: 400),
                new SvgFontFace(BuildBoldTestFont(), Weight: 700)
            ]
        };

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: the font-weight="bold" text selected the wide face
        Assert.Equal(255, surface[75, 35].A);
        // Assert: the sibling text with no font-weight still selected the narrow face
        Assert.Equal(0, surface[75, 70].A);
    }

    /// <summary>
    ///     Proves that the system rejects a null stream argument to
    ///     <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
    ///     with <see cref="ArgumentNullException"/> through the public API. See
    ///     <c>SvgCodecTests</c> for the full unit test coverage of the other null-argument
    ///     variants (null path, GetInfo overloads).
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgValidationNull_NullStreamThrowsArgumentNullException()
    {
        // Arrange, Act & Assert: passing a null stream through the public API is rejected
        Assert.Throws<ArgumentNullException>(() => SvgCodec.Load((Stream)null!, 10, 10));
    }

    /// <summary>
    ///     Proves that the system rejects an empty path argument to
    ///     <see cref="SvgCodec.Load(string, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>
    ///     with <see cref="ArgumentException"/> through the public API. See
    ///     <c>SvgCodecTests</c> for the full unit test coverage of the whitespace-only variant.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgValidationEmptyPath_EmptyPathThrowsArgumentException()
    {
        // Arrange, Act & Assert: passing an empty path through the public API is rejected
        Assert.Throws<ArgumentException>(() => SvgCodec.Load(string.Empty, 10, 10));
    }

    /// <summary>    ///     Proves that the system rejects malformed (non-well-formed) SVG XML with
    ///     <see cref="InvalidDataException"/> through the public API. See <c>SvgCodecTests</c> for
    ///     the full unit test coverage of the other malformed-input variants (viewBox, transform,
    ///     path data).
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgUnsupportedFormatValidation_MalformedXmlThrowsInvalidDataException()
    {
        // Arrange: an unclosed root element - not well-formed XML
        const string svg = "<svg viewBox='0 0 100 100'";

        // Act & Assert: the malformed document is rejected through the public API
        using var stream = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 100, 100));
    }

    /// <summary>
    ///     Proves that the system rejects an SVG document containing a DOCTYPE declaration with
    ///     <see cref="InvalidDataException"/> through the public API, defending against XXE
    ///     injection. See <c>SvgCodecTests</c> for the full unit test coverage of the external
    ///     entity variant and the <c>GetInfo</c> path.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgXxeHardening_DoctypeDeclarationThrowsInvalidDataException()
    {
        // Arrange: a well-formed document that is otherwise entirely valid, except for a DOCTYPE
        // declaration
        const string svg = "<!DOCTYPE svg [<!ENTITY foo \"bar\">]><svg viewBox='0 0 100 100'></svg>";

        // Act & Assert: the DOCTYPE-bearing document is rejected through the public API
        using var stream = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 100, 100));
    }

    /// <summary>
    ///     Proves that the system silently skips well-formed but out-of-scope SVG constructs,
    ///     continuing to render the rest of the document, through the public API. See
    ///     <c>SvgCodecTests</c> for the full unit test coverage of individual unsupported
    ///     construct kinds.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgTolerateUnsupportedConstructs_StillRendersRestOfDocument()
    {
        // Arrange: a nested inner <svg> element is out of scope; the sibling plain rect after it
        // should still render
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <svg x='0' y='0' width='10' height='10'><rect width='10' height='10' fill='yellow'/></svg>
              <rect x='10' y='10' width='30' height='30' fill='black'/>
            </svg>
            """;

        // Act: rasterize the document onto a new Surface through the public API
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the plain rect after the unsupported construct still rendered
        Assert.Equal(255, surface[20, 20].A);
    }

    /// <summary>
    ///     Proves that the system rejects a null stream argument to <see cref="SvgCodec.GetInfo(Stream)"/>
    ///     with the same <see cref="ArgumentNullException"/> contract as
    ///     <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>,
    ///     through the public API. See <c>SvgCodecTests</c> for the full unit test coverage of the
    ///     other GetInfo validation variants.
    /// </summary>
    [Fact]
    public void CanvasNetSvg_SystemIntegration_SvgGetInfoValidation_NullStreamThrowsArgumentNullException()
    {
        // Arrange, Act & Assert: passing a null stream to GetInfo through the public API is
        // rejected exactly as Load rejects one
        Assert.Throws<ArgumentNullException>(() => SvgCodec.GetInfo((Stream)null!));
    }
}
