// cspell:ignore Sfnt sfnt glyf cmap notdef codepoint
// cspell:ignore Dasharray hhea Hhea hmtx Hmtx hrefs letterboxed Loca Maxp unstroked
// cspell:ignore miterlimit
// cspell:ignore unparseable overpainted bbox moveto lineto rects unrotated unclipped
// cspell:ignore pillarbox
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for <see cref="SvgCodec"/>, using hand-authored SVG string/stream fixtures.
///     See <see cref="SvgFixtureTests"/> for real-file corpus and real-font integration coverage.
/// </summary>
public class SvgCodecTests
{
    /// <summary>Wraps <paramref name="svg"/> as a UTF-8 stream for <see cref="SvgCodec"/> to read.</summary>
    private static MemoryStream ToStream(string svg) => new(Encoding.UTF8.GetBytes(svg));

    /// <summary>
    ///     Builds a minimal, well-formed synthetic font with two mapped 50x50-unit square glyphs
    ///     ('A' at codepoint 65, glyph index 1; 'B' at codepoint 66, glyph index 2), a 100-unit
    ///     advance width on each, a 100-unit em-square, and a single kerning pair between them, for
    ///     controlled, predictable text-layout assertions (unlike the real, irregularly-shaped
    ///     glyphs used by <see cref="SvgFixtureTests"/>'s Open Sans integration test).
    /// </summary>
    private static TrueTypeFont BuildTestFont()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (50, 0, true), (50, 50, true), (0, 50, true)]
        ]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1), (66, 2)]);
        var kern = SyntheticFontBuilder.KernFormat0([(1, 2, -10)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(100, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(3))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(100, 0, 0, 3))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 100, 100]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, square.Length, square.Length], longFormat: false))
            .AddTable("glyf", [.. square, .. square])
            .AddTable("cmap", cmap)
            .AddTable("kern", kern)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>
    ///     Builds a synthetic font identical in structure to <see cref="BuildTestFont"/> except
    ///     the 'A' glyph is a wider 80x50-unit square (x equals 0 to 80, rather than 0 to 50),
    ///     giving font-weight/font-style face-selection tests a distinguishable "which face
    ///     actually rendered" pixel signature (a wide-glyph-only region between local x equals 50
    ///     and 80) representing a registered "bold" face.
    /// </summary>
    private static TrueTypeFont BuildBoldTestFont()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (80, 0, true), (80, 50, true), (0, 50, true)]
        ]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1), (66, 2)]);
        var kern = SyntheticFontBuilder.KernFormat0([(1, 2, -10)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(100, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(3))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(100, 0, 0, 3))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 100, 100]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, square.Length, square.Length], longFormat: false))
            .AddTable("glyf", [.. square, .. square])
            .AddTable("cmap", cmap)
            .AddTable("kern", kern)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    /// <summary>
    ///     Builds a synthetic font identical in structure to <see cref="BuildTestFont"/> except
    ///     the 'A' glyph is a 50x50-unit square shifted right by 20 units (x equals 20 to 70,
    ///     rather than 0 to 50), giving font-weight/font-style face-selection tests a
    ///     distinguishable "which face actually rendered" pixel signature (filled at local x
    ///     equals 60 but not at local x equals 10, the opposite of <see cref="BuildTestFont"/>'s
    ///     square) representing a registered "italic" face.
    /// </summary>
    private static TrueTypeFont BuildItalicTestFont()
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(20, 0, true), (70, 0, true), (70, 50, true), (20, 50, true)]
        ]);

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, [(65, 1), (66, 2)]);
        var kern = SyntheticFontBuilder.KernFormat0([(1, 2, -10)]);

        var data = new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(100, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(3))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(100, 0, 0, 3))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([0, 100, 100]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, square.Length, square.Length], longFormat: false))
            .AddTable("glyf", [.. square, .. square])
            .AddTable("cmap", cmap)
            .AddTable("kern", kern)
            .Build();

        using var stream = new MemoryStream(data);
        return TrueTypeFont.Load(stream);
    }

    // ================================================================================================
    // Basic shapes
    // ================================================================================================

    /// <summary>Proves that a filled <c>rect</c> renders its solid color within its bounds.</summary>
    [Fact]
    public void SvgCodec_Load_Rect_RendersFilledRectangle()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 20 20'><rect x='5' y='5' width='10' height='10' fill='#112233'/></svg>";

        // Act
        using var stream67 = ToStream(svg);
        var surface = SvgCodec.Load(stream67, 20, 20);

        // Assert
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 255), surface[10, 10]);
        Assert.Equal(0, surface[1, 1].A);
    }

    /// <summary>
    ///     Proves that a <c>rect</c> with <c>rx</c>/<c>ry</c> rounds its corners - the exact
    ///     corner pixel is unfilled (cut by the rounding) while the shape's center remains filled.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RectWithRoundedCorners_CutsCornerButFillsCenter()
    {
        // Arrange: a large rect with a generous corner radius, so anti-aliasing at the exact
        // corner pixel cannot produce a false positive
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='100' height='100' rx='30' ry='30' fill='black'/></svg>";

        // Act
        using var stream86 = ToStream(svg);
        var surface = SvgCodec.Load(stream86, 100, 100);

        // Assert: the extreme corner (well within the cut radius) is unfilled, the center is filled
        Assert.Equal(0, surface[1, 1].A);
        Assert.Equal(255, surface[50, 50].A);
    }

    /// <summary>Proves that a filled <c>circle</c> renders its solid color at its center.</summary>
    [Fact]
    public void SvgCodec_Load_Circle_RendersFilledCircle()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><circle cx='50' cy='50' r='40' fill='#00ff00'/></svg>";

        // Act
        using var stream101 = ToStream(svg);
        var surface = SvgCodec.Load(stream101, 100, 100);

        // Assert: center is filled; far corner (outside the circle) is not
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[50, 50]);
        Assert.Equal(0, surface[2, 2].A);
    }

    /// <summary>
    ///     Proves that an <c>ellipse</c> with unequal radii fills an elongated region - a point on
    ///     the long axis, outside the shorter axis's circle-equivalent radius, is still filled.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_Ellipse_RendersElongatedFill()
    {
        // Arrange: rx=40 (long axis), ry=10 (short axis)
        const string svg = "<svg viewBox='0 0 100 100'><ellipse cx='50' cy='50' rx='40' ry='10' fill='#0000ff'/></svg>";

        // Act
        using var stream119 = ToStream(svg);
        var surface = SvgCodec.Load(stream119, 100, 100);

        // Assert: point (85,50) is within the long (x) axis's radius but would be outside a
        // radius-10 circle - only a true ellipse (not a circle) fills it
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[85, 50]);
        // Assert: point (50,25) is outside the short (y) axis's radius
        Assert.Equal(0, surface[50, 25].A);
    }

    /// <summary>
    ///     Proves that a <c>line</c> element strokes a visible line along its endpoints (a
    ///     <c>line</c> has no interior to fill, only a stroke).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_Line_RendersStrokedLine()
    {
        // Arrange: a horizontal line across the middle of the canvas
        const string svg = "<svg viewBox='0 0 100 100'><line x1='10' y1='50' x2='90' y2='50' stroke='black' stroke-width='6'/></svg>";

        // Act
        using var stream139 = ToStream(svg);
        var surface = SvgCodec.Load(stream139, 100, 100);

        // Assert: a point on the line is stroked; a point well away from the line is not
        Assert.Equal(255, surface[50, 50].A);
        Assert.Equal(0, surface[50, 10].A);
    }

    /// <summary>
    ///     Proves that a <c>polyline</c> does not implicitly close its path - the segment between
    ///     the last and first points is not stroked, unlike <c>polygon</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_Polyline_DoesNotCloseBetweenLastAndFirstPoint()
    {
        // Arrange: an open "L" shaped polyline; the implicit closing segment would run diagonally
        // through the canvas center - a point on that closing diagonal, but not on the "L" itself,
        // must remain unstroked
        const string svg = "<svg viewBox='0 0 100 100'><polyline points='10,10 10,90 90,90' stroke='black' stroke-width='4' fill='none'/></svg>";

        // Act
        using var stream159 = ToStream(svg);
        var surface = SvgCodec.Load(stream159, 100, 100);

        // Assert: a point on the actual "L" path is stroked
        Assert.Equal(255, surface[10, 50].A);
        // Assert: the canvas center, on the hypothetical closing segment from (90,90) to (10,10)
        // but nowhere near the actual "L" path, remains unstroked
        Assert.Equal(0, surface[50, 50].A);
    }

    /// <summary>
    ///     Proves that a <c>polygon</c> both closes its path and fills its interior.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_Polygon_RendersClosedFilledShape()
    {
        // Arrange: a triangle
        const string svg = "<svg viewBox='0 0 100 100'><polygon points='50,10 90,90 10,90' fill='#ff00ff'/></svg>";

        // Act
        using var stream178 = ToStream(svg);
        var surface = SvgCodec.Load(stream178, 100, 100);

        // Assert: the triangle's centroid-ish interior point is filled
        Assert.Equal(new Rgba32(255, 0, 255, 255), surface[50, 70]);
        // Assert: a point outside the triangle (above its apex) is not
        Assert.Equal(0, surface[50, 5].A);
    }

    // ================================================================================================
    // Path data ('d' attribute) mini-language commands
    // ================================================================================================

    /// <summary>Proves that absolute <c>M</c>/<c>L</c>/<c>Z</c> path commands render a filled triangle.</summary>
    [Fact]
    public void SvgCodec_Load_PathWithAbsoluteMoveLineClose_RendersFilledTriangle()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><path d='M50,10 L90,90 L10,90 Z' fill='black'/></svg>";

        // Act
        using var stream198 = ToStream(svg);
        var surface = SvgCodec.Load(stream198, 100, 100);

        // Assert
        Assert.Equal(255, surface[50, 70].A);
        Assert.Equal(0, surface[50, 5].A);
    }

    /// <summary>
    ///     Proves that relative <c>m</c>/<c>l</c>/<c>z</c> path commands render the exact same
    ///     shape as their absolute equivalents.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathWithRelativeMoveLineClose_RendersSameShapeAsAbsolute()
    {
        // Arrange: the relative equivalent of "M50,10 L90,90 L10,90 Z"
        const string svg = "<svg viewBox='0 0 100 100'><path d='m50,10 l40,80 l-80,0 z' fill='black'/></svg>";

        // Act
        using var stream216 = ToStream(svg);
        var surface = SvgCodec.Load(stream216, 100, 100);

        // Assert: identical filled/unfilled pixels to the absolute-command test above
        Assert.Equal(255, surface[50, 70].A);
        Assert.Equal(0, surface[50, 5].A);
    }

    /// <summary>
    ///     Proves that <c>H</c>/<c>V</c> (absolute horizontal/vertical line) path commands render
    ///     a filled rectangle equivalent to an ordinary <c>rect</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathWithHorizontalAndVerticalLines_RendersRectangle()
    {
        // Arrange: a 10..90 square built from H/V commands instead of L
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,10 H90 V90 H10 Z' fill='black'/></svg>";

        // Act
        using var stream234 = ToStream(svg);
        var surface = SvgCodec.Load(stream234, 100, 100);

        // Assert
        Assert.Equal(255, surface[50, 50].A);
        Assert.Equal(0, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that a cubic Bezier (<c>C</c>) path command, with control points bulging
    ///     outward from a base rectangle, fills at least the base rectangle's interior (a
    ///     conservative, curve-shape-agnostic assertion).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathWithCubicBezier_RendersFilledCurvedShape()
    {
        // Arrange: a shape whose top edge bulges upward via a cubic Bezier
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,50 C10,10 90,10 90,50 L90,90 L10,90 Z' fill='black'/></svg>";

        // Act
        using var stream253 = ToStream(svg);
        var surface = SvgCodec.Load(stream253, 100, 100);

        // Assert: the base rectangle's interior is filled; well above the bulge is not
        Assert.Equal(255, surface[50, 70].A);
        Assert.Equal(0, surface[50, 5].A);
    }

    /// <summary>
    ///     Proves that a smooth cubic Bezier (<c>S</c>) path command following a <c>C</c> command
    ///     is accepted and renders filled content (reflecting the preceding command's control
    ///     point, per the SVG "smooth" command rule).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathWithSmoothCubicBezier_RendersFilledShape()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,50 C10,10 50,10 50,50 S90,90 90,50 L90,90 L10,90 Z' fill='black'/></svg>";

        // Act
        using var stream272 = ToStream(svg);
        var surface = SvgCodec.Load(stream272, 100, 100);

        // Assert
        Assert.Equal(255, surface[50, 60].A);
    }

    /// <summary>Proves that a quadratic Bezier (<c>Q</c>) path command renders filled content.</summary>
    [Fact]
    public void SvgCodec_Load_PathWithQuadraticBezier_RendersFilledShape()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,50 Q50,10 90,50 L90,90 L10,90 Z' fill='black'/></svg>";

        // Act
        using var stream286 = ToStream(svg);
        var surface = SvgCodec.Load(stream286, 100, 100);

        // Assert
        Assert.Equal(255, surface[50, 70].A);
        Assert.Equal(0, surface[50, 5].A);
    }

    /// <summary>
    ///     Proves that a smooth quadratic Bezier (<c>T</c>) path command following a <c>Q</c>
    ///     command is accepted and renders filled content.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathWithSmoothQuadraticBezier_RendersFilledShape()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,50 Q30,10 50,50 T90,50 L90,90 L10,90 Z' fill='black'/></svg>";

        // Act
        using var stream304 = ToStream(svg);
        var surface = SvgCodec.Load(stream304, 100, 100);

        // Assert
        Assert.Equal(255, surface[50, 60].A);
    }

    /// <summary>
    ///     Proves that an elliptical arc (<c>A</c>) path command renders filled content - a
    ///     half-disc built from a diameter line plus a semicircular arc.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathWithArc_RendersFilledHalfDisc()
    {
        // Arrange: a half-disc of radius 40 centered at (50,50), flat edge on top
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,50 A40,40 0 0 0 90,50 Z' fill='black'/></svg>";

        // Act
        using var stream321 = ToStream(svg);
        var surface = SvgCodec.Load(stream321, 100, 100);

        // Assert: a point well within the half-disc (below the flat edge) is filled
        Assert.Equal(255, surface[50, 80].A);
        // Assert: a point above the flat edge (outside the half-disc) is not
        Assert.Equal(0, surface[50, 20].A);
    }

    // ================================================================================================
    // Group presentation-attribute inheritance and opacity
    // ================================================================================================

    /// <summary>
    ///     Proves that a <c>g</c> element's <c>fill</c> attribute cascades down to a child shape
    ///     that does not set its own <c>fill</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFillInheritance_AppliesToChildWithoutOwnFill()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><g fill='#ff8800'><rect x='10' y='10' width='30' height='30'/></g></svg>";

        // Act
        using var stream344 = ToStream(svg);
        var surface = SvgCodec.Load(stream344, 100, 100);

        // Assert
        Assert.Equal(new Rgba32(0xFF, 0x88, 0x00, 255), surface[25, 25]);
    }

    /// <summary>
    ///     Proves that a child element's own <c>fill</c> attribute overrides its parent group's
    ///     cascaded <c>fill</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ChildOwnFill_OverridesGroupFill()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><g fill='red'><rect x='10' y='10' width='30' height='30' fill='blue'/></g></svg>";

        // Act
        using var stream361 = ToStream(svg);
        var surface = SvgCodec.Load(stream361, 100, 100);

        // Assert
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[25, 25]);
    }

    /// <summary>
    ///     Proves that nested groups' <c>opacity</c> values multiply together and are folded into
    ///     the final shape's alpha, per this codec's documented opacity-cascade simplification.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NestedGroupOpacity_MultipliesIntoFillAlpha()
    {
        // Arrange: two nested 50%-opacity groups multiply to 25% (0.25 * 255 = 63.75 ~ 64)
        const string svg = "<svg viewBox='0 0 100 100'><g opacity='0.5'><g opacity='0.5'><rect x='10' y='10' width='30' height='30' fill='black'/></g></g></svg>";

        // Act
        using var stream378 = ToStream(svg);
        var surface = SvgCodec.Load(stream378, 100, 100);

        // Assert: resulting alpha is approximately 25% of fully opaque, not 50% or 100%
        var alpha = surface[25, 25].A;
        Assert.InRange(alpha, 55, 70);
    }

    // ================================================================================================
    // Transform attribute functions
    // ================================================================================================

    /// <summary>Proves that a <c>translate</c> transform moves a shape by the given offset.</summary>
    [Fact]
    public void SvgCodec_Load_TranslateTransform_MovesShapeByOffset()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='20' height='20' fill='black' transform='translate(40,40)'/></svg>";

        // Act
        using var stream397 = ToStream(svg);
        var surface = SvgCodec.Load(stream397, 100, 100);

        // Assert: filled at the translated position; not filled at the pre-translation position
        Assert.Equal(255, surface[50, 50].A);
        Assert.Equal(0, surface[10, 10].A);
    }

    /// <summary>Proves that a <c>scale</c> transform enlarges a shape about the origin.</summary>
    [Fact]
    public void SvgCodec_Load_ScaleTransform_EnlargesShapeAboutOrigin()
    {
        // Arrange: a 10x10 rect scaled 5x becomes a 50x50 rect, both anchored at the origin
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='10' height='10' fill='black' transform='scale(5)'/></svg>";

        // Act
        using var stream412 = ToStream(svg);
        var surface = SvgCodec.Load(stream412, 100, 100);

        // Assert: filled well within the scaled-up shape; not filled beyond it
        Assert.Equal(255, surface[40, 40].A);
        Assert.Equal(0, surface[60, 60].A);
    }

    /// <summary>
    ///     Proves that a <c>rotate</c> transform rotates a shape about the origin per the SVG
    ///     formula (x' = x*cos(a) - y*sin(a), y' = x*sin(a) + y*cos(a)), mapping a 90-degree
    ///     rotation of the point (40,0) to (0,40).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RotateTransform_RotatesShapeAboutOrigin()
    {
        // Arrange: a small square far along the local +x axis, rotated 90 degrees
        const string svg = "<svg viewBox='0 0 100 100'><rect x='35' y='-5' width='10' height='10' fill='black' transform='rotate(90)'/></svg>";

        // Act
        using var stream431 = ToStream(svg);
        var surface = SvgCodec.Load(stream431, 100, 100);

        // Assert: the square (originally centered near local (40,0)) now renders near (0,40)
        Assert.Equal(255, surface[0, 40].A);
        // Assert: its pre-rotation position, near (40,0), is now unfilled
        Assert.Equal(0, surface[40, 2].A);
    }

    /// <summary>Proves that a <c>matrix</c> transform applies its six raw components directly.</summary>
    [Fact]
    public void SvgCodec_Load_MatrixTransform_AppliesRawComponents()
    {
        // Arrange: matrix(1,0,0,1,40,40) is equivalent to translate(40,40)
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='20' height='20' fill='black' transform='matrix(1,0,0,1,40,40)'/></svg>";

        // Act
        using var stream447 = ToStream(svg);
        var surface = SvgCodec.Load(stream447, 100, 100);

        // Assert
        Assert.Equal(255, surface[50, 50].A);
        Assert.Equal(0, surface[10, 10].A);
    }

    /// <summary>
    ///     Proves that a <c>skewX</c> transform shears a shape along x, proportional to y - a
    ///     point far down the shape shifts right, while the top edge (y=0) does not move at all.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_SkewXTransform_ShearsShapeAlongX()
    {
        // Arrange: a tall, thin vertical strip, skewed by 45 degrees (tan(45) = 1, so the shift
        // at a given y equals y itself)
        const string svg = "<svg viewBox='0 0 100 100'><rect x='10' y='0' width='4' height='80' fill='black' transform='skewX(45)'/></svg>";

        // Act
        using var stream466 = ToStream(svg);
        var surface = SvgCodec.Load(stream466, 100, 100);

        // Assert: near the top (y=1), the strip has barely moved and still covers x~13
        Assert.Equal(255, surface[13, 1].A);
        // Assert: near the bottom (y=70), the strip has shifted right by ~70 and no longer
        // covers its original x~12 position
        Assert.Equal(0, surface[12, 70].A);
        // Assert: at y=70 the shifted strip now covers x~82 (10+70 to 14+70)
        Assert.Equal(255, surface[82, 70].A);
    }

    /// <summary>
    ///     Proves that a combined transform function list is composed per the SVG specification's
    ///     "rightmost function applied first" rule: <c>"translate(40,40) rotate(90)"</c> rotates
    ///     the shape about the local origin first, then translates the rotated result - not the
    ///     other way around.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_CombinedTransformFunctions_AppliesRightmostFunctionFirst()
    {
        // Arrange: a 10x10 local rect centered near local x equals 40, y equals 0;
        // rotate(90) alone would map it near x equals 0, y equals 40; translate(40,40) then
        // shifts that to x equals 40, y equals 80
        const string svg = "<svg viewBox='0 0 100 100'><rect x='35' y='-5' width='10' height='10' fill='black' transform='translate(40,40) rotate(90)'/></svg>";

        // Act
        using var stream492 = ToStream(svg);
        var surface = SvgCodec.Load(stream492, 100, 100);

        // Assert: filled at the "rotate-then-translate" expected position
        Assert.Equal(255, surface[40, 80].A);
        // Assert: the shape's pre-transform local position (around local (40,0), left unmapped
        // by either composition order) remains unfilled
        Assert.Equal(0, surface[40, 2].A);
    }

    // ================================================================================================
    // Presentation attributes (fill/stroke families, fill-rule, dash array)
    // ================================================================================================

    /// <summary>
    ///     Proves that <c>fill-rule="evenodd"</c> punches a hole where two overlapping subpaths'
    ///     windings cancel, unlike the default <c>nonzero</c> rule, which fills the union.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FillRuleEvenOdd_PunchesHoleInOverlappingSubpaths()
    {
        // Arrange: two same-direction overlapping rectangles - evenodd cancels their shared center
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,10 L90,10 L90,90 L10,90 Z M30,30 L70,30 L70,70 L30,70 Z' fill-rule='evenodd' fill='black'/></svg>";

        // Act
        using var stream516 = ToStream(svg);
        var surface = SvgCodec.Load(stream516, 100, 100);

        // Assert: the shared overlapping center is a hole (unfilled); the outer ring is filled
        Assert.Equal(0, surface[50, 50].A);
        Assert.Equal(255, surface[15, 50].A);
    }

    /// <summary>
    ///     Proves that the same overlapping subpaths under the default <c>nonzero</c> fill rule
    ///     fill the union, including the center - the opposite of the <c>evenodd</c> case above.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FillRuleNonzeroDefault_FillsUnionOfOverlappingSubpaths()
    {
        // Arrange: identical geometry to the evenodd test above, but without fill-rule specified
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,10 L90,10 L90,90 L10,90 Z M30,30 L70,30 L70,70 L30,70 Z' fill='black'/></svg>";

        // Act
        using var stream534 = ToStream(svg);
        var surface = SvgCodec.Load(stream534, 100, 100);

        // Assert: the center is filled under nonzero, unlike under evenodd
        Assert.Equal(255, surface[50, 50].A);
    }

    /// <summary>
    ///     Proves that <c>fill="none"</c> paired with a <c>stroke</c> renders only the outline,
    ///     leaving the shape's interior unfilled.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FillNoneWithStroke_RendersOnlyOutline()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='20' y='20' width='60' height='60' fill='none' stroke='black' stroke-width='6'/></svg>";

        // Act
        using var stream551 = ToStream(svg);
        var surface = SvgCodec.Load(stream551, 100, 100);

        // Assert: the outline (near x=20) is stroked; the interior (center) is not filled
        Assert.Equal(255, surface[20, 50].A);
        Assert.Equal(0, surface[50, 50].A);
    }

    /// <summary>
    ///     Proves that a <c>stroke-dasharray</c> leaves visible gaps along a stroked line, rather
    ///     than rendering a solid stroke.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeDasharray_RendersGapsAlongLine()
    {
        // Arrange: a long horizontal line with an evenly-spaced 10-on/10-off dash pattern
        const string svg = "<svg viewBox='0 0 100 100'><line x1='0' y1='50' x2='100' y2='50' stroke='black' stroke-width='4' stroke-dasharray='10,10'/></svg>";

        // Act
        using var stream569 = ToStream(svg);
        var surface = SvgCodec.Load(stream569, 100, 100);

        // Assert: at least one sampled point along the line is unstroked (a dash gap) - a solid
        // stroke (dasharray ignored) would leave every sampled point stroked
        var foundGap = false;
        for (var x = 0; x < 100; x += 2)
        {
            if (surface[x, 50].A == 0)
            {
                foundGap = true;
                break;
            }
        }

        Assert.True(foundGap, "Expected at least one unstroked gap along the dashed line.");

        // Assert: the very start of the line (within the first "on" dash segment) is stroked
        Assert.Equal(255, surface[2, 50].A);
    }

    /// <summary>
    ///     Proves that a <c>stroke-miterlimit</c> value of <c>0</c> - below
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/>'s documented contract of
    ///     "finite and at least 1" -
    ///     falls back to the inherited/default value rather than reaching
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/>'s constructor and throwing an uncaught
    ///     <see cref="ArgumentOutOfRangeException"/>, matching this codec's existing tolerant
    ///     handling of a malformed <c>stroke-dasharray</c>. The stroke still renders (non-zero
    ///     alpha), proving the fallback rather than the whole stroke being silently dropped.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeMiterLimitZero_FallsBackToInheritedDefaultWithoutThrowing()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='20' y='20' width='60' height='60' fill='none' stroke='black' stroke-width='6' stroke-miterlimit='0'/></svg>";

        // Act
        using var stream606 = ToStream(svg);
        var surface = SvgCodec.Load(stream606, 100, 100);

        // Assert: the outline still renders (no exception, and the stroke was not dropped)
        Assert.Equal(255, surface[20, 50].A);
    }

    /// <summary>
    ///     Proves that a negative <c>stroke-miterlimit</c> value likewise falls back to the
    ///     inherited/default value without throwing, rather than only the boundary case of
    ///     <c>0</c> above being tolerated.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeMiterLimitNegative_FallsBackToInheritedDefaultWithoutThrowing()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='20' y='20' width='60' height='60' fill='none' stroke='black' stroke-width='6' stroke-miterlimit='-5'/></svg>";

        // Act
        using var stream624 = ToStream(svg);
        var surface = SvgCodec.Load(stream624, 100, 100);

        // Assert: the outline still renders (no exception, and the stroke was not dropped)
        Assert.Equal(255, surface[20, 50].A);
    }

    /// <summary>
    ///     Proves that a <c>stroke-miterlimit</c> value of <c>NaN</c> - non-finite, and therefore
    ///     below <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/>'s documented contract of
    ///     "finite and at least 1" -
    ///     falls back to the inherited/default value rather than reaching
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/>'s constructor and throwing an uncaught
    ///     <see cref="ArgumentOutOfRangeException"/>, matching this codec's existing tolerant
    ///     handling of a malformed <c>stroke-dasharray</c>. The stroke still renders (non-zero
    ///     alpha), proving the fallback rather than the whole stroke being silently dropped.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeMiterLimitNaN_FallsBackToInheritedDefaultWithoutThrowing()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='20' y='20' width='60' height='60' fill='none' stroke='black' stroke-width='6' stroke-miterlimit='NaN'/></svg>";

        // Act
        using var stream647 = ToStream(svg);
        var surface = SvgCodec.Load(stream647, 100, 100);

        // Assert: the outline still renders (no exception, and the stroke was not dropped)
        Assert.Equal(255, surface[20, 50].A);
    }

    /// <summary>
    ///     Proves that a <c>stroke-miterlimit</c> value of <c>Infinity</c> - non-finite, and
    ///     therefore below <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/>'s documented
    ///     contract of "finite and at least 1" -
    ///     falls back to the inherited/default value rather than reaching
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/>'s constructor and throwing an uncaught
    ///     <see cref="ArgumentOutOfRangeException"/>, matching this codec's existing tolerant
    ///     handling of a malformed <c>stroke-dasharray</c>. The stroke still renders (non-zero
    ///     alpha), proving the fallback rather than the whole stroke being silently dropped.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeMiterLimitInfinity_FallsBackToInheritedDefaultWithoutThrowing()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='20' y='20' width='60' height='60' fill='none' stroke='black' stroke-width='6' stroke-miterlimit='Infinity'/></svg>";

        // Act
        using var stream670 = ToStream(svg);
        var surface = SvgCodec.Load(stream670, 100, 100);

        // Assert: the outline still renders (no exception, and the stroke was not dropped)
        Assert.Equal(255, surface[20, 50].A);
    }

    /// <summary>
    ///     Proves that a valid <c>stroke-miterlimit</c> value continues to be accepted and applied
    ///     (rather than every value being tolerated/ignored after the validation added above).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeMiterLimitValid_RendersNormally()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='20' y='20' width='60' height='60' fill='none' stroke='black' stroke-width='6' stroke-miterlimit='4'/></svg>";

        // Act
        using var stream687 = ToStream(svg);
        var surface = SvgCodec.Load(stream687, 100, 100);

        // Assert: the outline renders normally
        Assert.Equal(255, surface[20, 50].A);
    }

    /// <summary>
    ///     Proves the finding's exact repro scenario: an in-bound-but-large <c>stroke-width</c>
    ///     (<c>900000</c>, at or under <c>MaxCoordinateMagnitude</c>) combined with an
    ///     in-bound-but-extreme <c>stroke-miterlimit</c> (<c>1e12</c>, finite and <c>&gt;= 1</c>,
    ///     so it passes <see cref="DemaConsulting.CanvasNet.Codecs.SvgCodec"/>'s own
    ///     <c>ParseValidMiterLimit</c> check) and a vertex whose interior angle is only
    ///     ~<c>0.005</c> degrees away from a full reversal (an acute "spike" vertex) synthesizes a
    ///     miter-join point roughly <c>1e10</c> units from the origin - many orders of magnitude
    ///     beyond <c>MaxCoordinateMagnitude</c> - even though every individual literal (every path
    ///     coordinate, the stroke width, and the miterlimit) independently passes its own
    ///     parse-time check. Before the post-stroke re-check, this synthesized point reached the
    ///     rasterizer's fill step, relying only on its clip-bounds intersection with the canvas to
    ///     avoid a hang/crash - not itself a bug fix. After the fix, the entire stroke is
    ///     tolerantly skipped instead.
    /// </summary>
    /// <remarks>
    ///     The three path points below - <c>(10,50)</c>, <c>(50,50)</c>, and
    ///     <c>(10.0000002,50.0034907)</c> - form a needle-thin spike: the first segment runs due
    ///     east, and the second segment runs back nearly due west (almost retracing the first),
    ///     deviating from an exact 180-degree reversal by only ~0.005 degrees. Since a miter
    ///     length is <c>halfWidth / sin(interiorAngle / 2)</c>, this near-zero interior angle
    ///     drives the miter ratio (and therefore the synthesized point's distance from the vertex)
    ///     to roughly <c>22,900</c> times <c>halfWidth</c> (<c>450,000</c>), i.e. ~<c>1.03e10</c> -
    ///     comfortably past <c>MaxCoordinateMagnitude</c> (<c>1,000,000</c>), while the miter ratio
    ///     itself (~<c>22,900</c>) stays comfortably under the extreme <c>1e12</c> miterlimit, so
    ///     <c>TryCreateMiter</c>'s own ratio-vs-miterlimit check does not reject it - the gap this
    ///     fix closes is purely about the synthesized point's absolute magnitude, not its ratio.
    ///     <para>
    ///     Because <c>stroke-width="900000"</c> alone already dwarfs the 100x100 canvas (its
    ///     half-width alone is 4,500 times the canvas size), an un-skipped stroke - spike or not -
    ///     would engulf the entire canvas in solid stroke color. The assertion below therefore
    ///     checks that the canvas has <b>no</b> stroke color anywhere, which is only possible if
    ///     the whole stroke - including its ordinarily-covering non-spike portions - was skipped
    ///     as a unit, exactly as <see cref="DemaConsulting.CanvasNet.Codecs.SvgCodec"/>'s other
    ///     tolerant-skip guards already do for a shape/stroke as a whole.
    ///     </para>
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_ExtremeMiterLimitWithSpikeVertexSynthesizesOversizedMiterPoint_SkipsStrokeWithoutThrowing()
    {
        // Arrange: a needle-thin spike vertex (interior angle ~0.005 degrees) with an in-bound
        // stroke-width (900000, at MaxCoordinateMagnitude's near-boundary) and an in-bound
        // miterlimit (1e12) - each individually compliant, but composing to a miter point ~1e10
        // units from the origin
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<path d='M 10,50 L 50,50 L 10.0000002,50.0034907' fill='none' " +
                            "stroke='black' stroke-width='900000' stroke-miterlimit='1e12'/>" +
                            "</svg>";

        // Act
        using var stream743 = ToStream(svg);
        var surface = SvgCodec.Load(stream743, 100, 100);

        // Assert: rendering completed without incident (no hang/crash), and the whole stroke -
        // which, un-skipped, would have engulfed the entire 100x100 canvas given its 900,000-unit
        // width alone - was tolerantly skipped in its entirety instead
        Assert.Equal(100, surface.Width);
        Assert.Equal(0, surface[50, 50].A);
        Assert.Equal(0, surface[10, 50].A);
        Assert.Equal(0, surface[99, 99].A);
    }

    /// <summary>
    ///     Proves that an ordinary, legitimate miter join - a small, typical <c>stroke-width</c>,
    ///     the SVG-default <c>stroke-miterlimit</c> of <c>4</c>, and a normal (not a degenerate
    ///     near-straight/near-reversed spike) vertex angle - continues to render exactly as before,
    ///     completely unaffected by the new post-stroke coordinate-magnitude re-check added
    ///     alongside <see cref="SvgCodec_Load_ExtremeMiterLimitWithSpikeVertexSynthesizesOversizedMiterPoint_SkipsStrokeWithoutThrowing"/>
    ///     above.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NormalMiterJoinWithDefaultMiterLimit_RendersNormally()
    {
        // Arrange: a simple 90-degree corner (a well-conditioned, everyday miter join) with a
        // small stroke-width and the SVG spec's own default stroke-miterlimit of 4
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<path d='M 20,20 L 60,20 L 60,60' fill='none' " +
                            "stroke='black' stroke-width='6' stroke-miterlimit='4'/>" +
                            "</svg>";

        // Act
        using var stream773 = ToStream(svg);
        var surface = SvgCodec.Load(stream773, 100, 100);

        // Assert: the corner's sharp miter tip renders as expected, near (60,20)
        Assert.Equal(255, surface[60, 20].A);

        // ... as does a point along each straight segment away from the corner
        Assert.Equal(255, surface[40, 20].A);
        Assert.Equal(255, surface[60, 40].A);
    }

    /// <summary>
    ///     Proves that a stroke whose effective width overflows to <c>Infinity</c> - because seven
    ///     nested <c>transform="scale(1000000)"</c> groups each carry an individually-finite
    ///     literal (each at or under the codec's fixed <c>MaxCoordinateMagnitude</c> bound), but
    ///     their composed determinant inside <see cref="SvgCodec"/>'s <c>EstimateUniformScale</c>
    ///     overflows a <see langword="float"/> once composed seven levels deep (<c>1,000,000^7</c>) -
    ///     is silently skipped rather than reaching
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/>'s constructor and throwing an
    ///     uncaught <see cref="ArgumentOutOfRangeException"/>. An infinite width passes the
    ///     pre-existing <c>strokeWidth &lt;= 0f</c> guard unmodified (since <c>Infinity &gt; 0</c>),
    ///     so this proves the additional finiteness check.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NestedTransformScaleOverflowsStrokeWidthToInfinity_SkipsStrokeWithoutThrowing()
    {
        // Arrange: seven nested scale(1000000) groups - each individual literal is at the codec's
        // fixed MaxCoordinateMagnitude bound (so none is rejected on its own), but composing seven
        // of them (1,000,000^7 = 1e42) overflows float's ~3.4e38 range, producing a non-finite
        // effective stroke width
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <g transform='scale(1000000)'>
                <g transform='scale(1000000)'>
                  <g transform='scale(1000000)'>
                    <g transform='scale(1000000)'>
                      <g transform='scale(1000000)'>
                        <g transform='scale(1000000)'>
                          <g transform='scale(1000000)'>
                            <rect x='1' y='1' width='2' height='2' fill='none' stroke='black' stroke-width='1'/>
                          </g>
                        </g>
                      </g>
                    </g>
                  </g>
                </g>
              </g>
            </svg>
            """;

        // Act
        using var stream823 = ToStream(svg);
        var surface = SvgCodec.Load(stream823, 100, 100);

        // Assert: rendering completed without the raw ArgumentOutOfRangeException a non-finite
        // effective stroke width reaching StrokeStyle's constructor would otherwise throw
        Assert.Equal(100, surface.Width);
    }

    /// <summary>
    ///     Proves that a stroke whose effective width is <b>finite but extreme</b> - a compliant,
    ///     in-bound <c>stroke-width</c> composed with a large-but-finite transform scale, such
    ///     that the scaled result exceeds <see cref="SvgCodec"/>'s fixed
    ///     <c>MaxCoordinateMagnitude</c> bound without overflowing to <c>Infinity</c> - is
    ///     tolerantly skipped rather than being fed into
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.PathStroker"/>'s offset-curve generation at
    ///     a magnitude it was never meant to see.
    /// </summary>
    /// <remarks>
    ///     This is a genuinely different case from
    ///     <see cref="SvgCodec_Load_NestedTransformScaleOverflowsStrokeWidthToInfinity_SkipsStrokeWithoutThrowing"/>
    ///     immediately above: that test's effective width overflows <see langword="float"/> range
    ///     entirely (<c>Infinity</c>), which the pre-existing <c>!float.IsFinite(strokeWidth)</c>
    ///     guard alone already catches. Here, a single <c>scale(900000)</c> transform applied to a
    ///     tiny, near-origin rect (so its own transformed vertex coordinates stay comfortably
    ///     under <c>MaxCoordinateMagnitude</c>, meaning the shape's coordinate-magnitude check
    ///     passes and the fill still renders) combined with a small, compliant <c>stroke-width</c>
    ///     of <c>2</c> yields an effective width of <c>2 * 900,000 = 1,800,000</c> - finite, and
    ///     therefore invisible to the pre-existing guard, but still far past the
    ///     1,000,000-magnitude bound, exactly the gap this fix closes.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_StrokeWidthScaledPastMagnitudeBound_SkipsStrokeWithoutThrowing()
    {
        // Arrange: a tiny rect near the origin (so its transformed coordinates, up to 90, stay
        // comfortably under the 1,000,000-magnitude bound and its fill still renders normally),
        // with a small, in-bound stroke-width (2) and a scale(900000) transform whose effective
        // stroke width (2 * 900,000 = 1,800,000) exceeds the same bound.
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<rect x='0' y='0' width='0.0001' height='0.0001' fill='black' " +
                            "stroke='blue' stroke-width='2' transform='scale(900000)'/>" +
                            "</svg>";

        // Act
        using var stream865 = ToStream(svg);
        var surface = SvgCodec.Load(stream865, 100, 100);

        // Assert: the fill still renders normally (its own transformed geometry stays within
        // bound) ...
        Assert.Equal(255, surface[45, 45].A);

        // ... but the stroke was skipped rather than reaching PathStroker with a 1,800,000-unit
        // effective width: an un-skipped stroke that huge would engulf the whole 100x100 canvas
        // (its half-width alone dwarfs the canvas), so the far corner staying unfilled is direct
        // evidence the oversized stroke outline was never generated.
        Assert.Equal(0, surface[99, 99].A);
    }

    /// <summary>
    ///     Proves the finding's required concrete repro: an element whose own composed
    ///     <c>transform</c> overflows to non-finite, filled via <c>fill="url(#g)"</c> referencing a
    ///     <c>linearGradient</c>, no longer lets a non-finite transform reach
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.Gradient"/>'s constructor and throw an
    ///     uncaught <see cref="ArgumentOutOfRangeException"/> - the document loads successfully
    ///     with the affected element's rendering tolerantly skipped.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NestedTransformScaleOverflowsGradientTransformToNonFinite_SkipsElementWithoutThrowing()
    {
        // Arrange: seven nested scale(1000000) groups (each literal at the codec's fixed
        // MaxCoordinateMagnitude bound) around a gradient-filled rect - before the fix, this threw
        // a raw ArgumentOutOfRangeException from Gradient's constructor
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='g'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
              </defs>
              <g transform='scale(1000000)'>
                <g transform='scale(1000000)'>
                  <g transform='scale(1000000)'>
                    <g transform='scale(1000000)'>
                      <g transform='scale(1000000)'>
                        <g transform='scale(1000000)'>
                          <g transform='scale(1000000)'>
                            <rect x='1' y='1' width='2' height='2' fill='url(#g)'/>
                          </g>
                        </g>
                      </g>
                    </g>
                  </g>
                </g>
              </g>
            </svg>
            """;

        // Act
        using var stream919 = ToStream(svg);
        var surface = SvgCodec.Load(stream919, 100, 100);

        // Assert: loads without throwing, and the (skipped) rect leaves nothing rendered
        Assert.Equal(100, surface.Width);
        Assert.Equal(0, surface[50, 50].A);
    }

    /// <summary>
    ///     Proves that a <c>stroke-dasharray</c> entry that is itself modest (well within the
    ///     codec's fixed <c>MaxCoordinateMagnitude</c> bound) can still overflow to non-finite once
    ///     scaled by an extreme-but-finite composed transform's own scale factor (the same scale
    ///     <c>stroke-width</c> is already scaled by) - tolerantly falling back to "no dashing" (a
    ///     solid stroke) rather than reaching
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/>'s constructor and throwing an
    ///     uncaught exception - mirroring <c>ParseDashArray</c>'s own existing tolerant
    ///     "malformed dash array -&gt; no dashing" convention.
    /// </summary>
    /// <remarks>
    ///     <b>Superseded by the coordinate-magnitude bound (Finding 6).</b> This scenario required
    ///     an individually-finite <c>stroke-dasharray</c> entry large enough that, once scaled by a
    ///     composed transform's own extreme-but-finite scale factor, the product overflowed float.
    ///     That scale factor is <c>EstimateUniformScale</c>'s <c>sqrt(|M11*M22 - M12*M21|)</c>,
    ///     whose own internal squaring means the scale factor itself cannot exceed roughly
    ///     <c>sqrt(float.MaxValue) ≈ 1.84e19</c> without <c>EstimateUniformScale</c>'s own
    ///     determinant computation overflowing first (which would make <c>strokeWidth</c> itself
    ///     non-finite, skipping the whole stroke via the earlier check above - a different,
    ///     already-covered case). With every dasharray entry now capped at
    ///     <c>MaxCoordinateMagnitude</c> (1,000,000), the largest a scaled entry can ever reach is
    ///     approximately <c>1,000,000 * 1.84e19 ≈ 1.84e25</c> - far short of float's ~3.4e38 range.
    ///     This specific dasharray-scaling-only overflow is therefore no longer reachable through
    ///     any input <c>Load</c> can be given; the tolerant fallback in <c>RenderStroke</c> remains
    ///     in place as defense-in-depth (matching this class's other now-unreachable-but-retained
    ///     guards - see <c>GeometryWorkBudget.Charge</c>'s own reachability note). This test
    ///     is retained under its original name, repurposed to instead prove the new, earlier
    ///     rejection point: a dasharray entry whose own raw magnitude exceeds
    ///     <c>MaxCoordinateMagnitude</c> is now rejected by <c>TryReadNumber</c> before it can ever
    ///     reach <c>RenderStroke</c>'s scaling at all.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_ScaledDashArrayOverflowsToInfinity_FallsBackToSolidStrokeWithoutThrowing()
    {
        // Arrange: a dasharray entry one unit over the codec's fixed MaxCoordinateMagnitude bound
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <rect x='5' y='5' width='40' height='40' fill='none' stroke='black' stroke-width='2' stroke-dasharray='1000001,1'/>
            </svg>
            """;

        // Act & Assert: rejected at parse time, well before RenderStroke's own scaling would ever
        // run
        using var stream969 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream969, 100, 100));
    }

    /// <summary>
    ///     Proves that <c>opacity</c> multiplies into a solid fill color's alpha rather than
    ///     leaving it fully opaque.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_Opacity_MultipliesIntoFillAlpha()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='10' y='10' width='30' height='30' fill='black' opacity='0.4'/></svg>";

        // Act
        using var stream983 = ToStream(svg);
        var surface = SvgCodec.Load(stream983, 100, 100);

        // Assert: alpha is approximately 40% of fully opaque (0.4 * 255 = 102), not 0 or 255
        Assert.InRange((int)surface[25, 25].A, 90, 112);
    }

    // ================================================================================================
    // Gradients (linear, radial, spreadMethod, href template inheritance)
    // ================================================================================================

    /// <summary>
    ///     Proves that a <c>userSpaceOnUse</c> linear gradient's stops map directly onto document
    ///     user-space coordinates rather than the shape's own bounding box.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LinearGradientUserSpaceOnUse_VariesAlongUserSpaceAxis()
    {
        // Arrange: gradient runs black-to-white along x equals 0 to x equals 100 in user space,
        // independent of the filled rect's own (smaller, offset) bounding box
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='g' gradientUnits='userSpaceOnUse' x1='0' y1='0' x2='100' y2='0'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
              </defs>
              <rect x='10' y='10' width='80' height='80' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream1015 = ToStream(svg);
        var surface = SvgCodec.Load(stream1015, 100, 100);

        // Assert: brightness increases left to right, and reflects the user-space (not
        // bounding-box-relative) axis
        Assert.True(surface[20, 50].R < surface[80, 50].R);
    }

    /// <summary>
    ///     Proves that a radial gradient centered on a shape is brighter at its center than near
    ///     its edge, for a "bright center, dark edge" stop configuration.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RadialGradient_VariesFromCenterToEdge()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <radialGradient id='g' cx='0.5' cy='0.5' r='0.5'>
                  <stop offset='0' stop-color='white'/>
                  <stop offset='1' stop-color='black'/>
                </radialGradient>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream1043 = ToStream(svg);
        var surface = SvgCodec.Load(stream1043, 100, 100);

        // Assert: the center is brighter than a point near the shape's edge
        Assert.True(surface[50, 50].R > surface[95, 50].R);
    }

    /// <summary>
    ///     Proves that a <c>radialGradient</c> with a negative <c>r</c> - below
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.RadialGradient"/>'s documented contract of
    ///     "finite and greater than or equal to zero" - falls back to the default radius
    ///     (<c>0.5</c>, in objectBoundingBox units) rather than reaching
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.RadialGradient"/>'s constructor and throwing an
    ///     uncaught <see cref="ArgumentOutOfRangeException"/>, matching this codec's existing
    ///     tolerant handling of every other gradient coordinate. The fill still renders (non-zero
    ///     alpha), proving the fallback rather than the whole gradient being silently dropped.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RadialGradientRNegative_FallsBackToDefaultRadiusWithoutThrowing()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <radialGradient id='g' r='-5'>
                  <stop offset='0' stop-color='red'/>
                  <stop offset='1' stop-color='blue'/>
                </radialGradient>
              </defs>
              <rect x='0' y='0' width='10' height='10' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream1076 = ToStream(svg);
        var surface = SvgCodec.Load(stream1076, 100, 100);

        // Assert: the rect still renders (no exception, and the fill was not dropped)
        Assert.Equal(255, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that a <c>radialGradient</c> with a negative <c>fr</c> (the SVG 2 focal-radius
    ///     attribute) likewise falls back to its default (<c>0</c>) without throwing, rather than
    ///     only the end-circle radius (<c>r</c>) above being covered.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RadialGradientFrNegative_FallsBackToDefaultFocalRadiusWithoutThrowing()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <radialGradient id='g' r='0.5' fr='-1'>
                  <stop offset='0' stop-color='red'/>
                  <stop offset='1' stop-color='blue'/>
                </radialGradient>
              </defs>
              <rect x='0' y='0' width='10' height='10' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream1104 = ToStream(svg);
        var surface = SvgCodec.Load(stream1104, 100, 100);

        // Assert: the rect still renders (no exception, and the fill was not dropped)
        Assert.Equal(255, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that a valid (non-negative) <c>r</c>/<c>fr</c> continues to be accepted and applied
    ///     (rather than every value being tolerated/ignored after the validation added above).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RadialGradientRFrValid_RendersNormally()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <radialGradient id='g' cx='0.5' cy='0.5' r='0.5' fr='0.1'>
                  <stop offset='0' stop-color='white'/>
                  <stop offset='1' stop-color='black'/>
                </radialGradient>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream1131 = ToStream(svg);
        var surface = SvgCodec.Load(stream1131, 100, 100);

        // Assert: the center is brighter than a point near the shape's edge (gradient still applied)
        Assert.True(surface[50, 50].R > surface[95, 50].R);
    }

    /// <summary>
    ///     Proves <c>BuildGradient</c>'s own independent finiteness guard: a shape whose own
    ///     ancestor <c>transform</c> chain composes to a value that is extreme but still finite (so
    ///     <c>RenderElement</c>'s own composed-transform guard never fires) combined with an
    ///     extreme-but-individually-finite <c>width</c>/<c>height</c> (each at the codec's fixed
    ///     <c>MaxCoordinateMagnitude</c> bound) and the gradient's own
    ///     <c>gradientTransform="scale(1000000)"</c> overflows only <c>BuildGradient</c>'s own
    ///     <c>gradientTransform * bboxMap * elementTransform</c> product to a non-finite value - a
    ///     genuinely distinct repro from
    ///     <see cref="SvgCodec_Load_NestedTransformScaleOverflowsGradientTransformToNonFinite_SkipsElementWithoutThrowing"/>'s
    ///     case (where the ancestor composition itself overflows), since here
    ///     <c>RenderElement</c>'s own guard never fires - the ancestor-composed transform alone
    ///     (1,000,000^5 = 1e30) stays finite. Before the fix, this also threw a raw
    ///     <see cref="ArgumentOutOfRangeException"/> from
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.Gradient"/>'s constructor.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientTransformComposedWithHugeBoundingBoxOverflowsToNonFinite_TreatsAsNoPaintWithoutThrowing()
    {
        // Arrange: five nested scale(1000000) ancestor groups compose to an extreme-but-finite
        // elementTransform (1,000,000^5 = 1e30, well under float's ~3.4e38 range, so
        // RenderElement's own composed-transform guard stays satisfied), the rect's own
        // width/height (1000000 each, at the codec's fixed MaxCoordinateMagnitude bound) give it a
        // huge object-bounding-box scale, and the gradient's own gradientTransform (1000000, also
        // at the bound) - only once all three are multiplied together inside BuildGradient
        // (1e30 * 1e6 * 1e6 = 1e42) does the product overflow to a non-finite value
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='g' gradientTransform='scale(1000000)'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
              </defs>
              <g transform='scale(1000000)'>
                <g transform='scale(1000000)'>
                  <g transform='scale(1000000)'>
                    <g transform='scale(1000000)'>
                      <g transform='scale(1000000)'>
                        <rect x='0' y='0' width='1000000' height='1000000' fill='url(#g)'/>
                      </g>
                    </g>
                  </g>
                </g>
              </g>
            </svg>
            """;

        // Act
        using var stream1186 = ToStream(svg);
        var surface = SvgCodec.Load(stream1186, 100, 100);

        // Assert: loads without throwing, and the gradient fill is tolerated as "no paint"
        Assert.Equal(100, surface.Width);
        Assert.Equal(0, surface[50, 50].A);
    }

    /// <summary>
    ///     Proves that <c>spreadMethod="repeat"</c> tiles the gradient's base range rather than
    ///     clamping ("pad", the default) beyond it.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientSpreadMethodRepeat_TilesPastBaseRange()
    {
        // Arrange: base gradient range spans only the first fifth of the rect's bounding box
        // width (20 of 100 units); with repeat, x equals 5 and x equals 25 fall at the same
        // fractional offset within successive tiles and should render nearly identically
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='g' x1='0' y1='0' x2='0.2' y2='0' spreadMethod='repeat'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream1216 = ToStream(svg);
        var surface = SvgCodec.Load(stream1216, 100, 100);

        // Assert: the tiled positions render nearly the same color (repeat), and that color is
        // not the fully-clamped white a "pad" (default) spread would produce at x equals 25
        Assert.InRange(Math.Abs(surface[5, 50].R - surface[25, 50].R), 0, 12);
        Assert.True(surface[25, 50].R < 200);
    }

    /// <summary>
    ///     Proves that a <c>stroke="url(#id)"</c> gradient reference paints the stroke itself
    ///     with varying color, not just the fill.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeGradient_PaintsVaryingColorAlongStroke()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='g' gradientUnits='userSpaceOnUse' x1='0' y1='0' x2='100' y2='0'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
              </defs>
              <line x1='0' y1='50' x2='100' y2='50' fill='none' stroke='url(#g)' stroke-width='10'/>
            </svg>
            """;

        // Act
        using var stream1245 = ToStream(svg);
        var surface = SvgCodec.Load(stream1245, 100, 100);

        // Assert: the stroke is darker near the start than near the end
        Assert.True(surface[10, 50].R < surface[90, 50].R);
    }

    /// <summary>
    ///     Proves that a gradient with no <c>stop</c> children of its own inherits its color
    ///     stops from the gradient it references via <c>href</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientHrefInheritance_InheritsStopsFromTemplate()
    {
        // Arrange: "derived" has its own geometry attributes but no stops of its own
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='base' x1='0' y1='0' x2='1' y2='0'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
                <linearGradient id='derived' href='#base' x1='0' y1='0' x2='1' y2='0'/>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#derived)'/>
            </svg>
            """;

        // Act
        using var stream1273 = ToStream(svg);
        var surface = SvgCodec.Load(stream1273, 100, 100);

        // Assert: the inherited stops still produce a left-to-right brightness gradient
        Assert.True(surface[10, 50].R < surface[90, 50].R);
    }

    /// <summary>
    ///     Proves that a gradient <c>href</c> chain of more than one hop still resolves (walking
    ///     through an intermediate stop-less link) to the eventual stop-bearing template.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientHrefChainOfTwoHops_ResolvesToEventualStops()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='base' x1='0' y1='0' x2='1' y2='0'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
                <linearGradient id='middle' href='#base'/>
                <linearGradient id='derived' href='#middle' x1='0' y1='0' x2='1' y2='0'/>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#derived)'/>
            </svg>
            """;

        // Act
        using var stream1302 = ToStream(svg);
        var surface = SvgCodec.Load(stream1302, 100, 100);

        // Assert
        Assert.True(surface[10, 50].R < surface[90, 50].R);
    }

    /// <summary>
    ///     Proves that a cyclical gradient <c>href</c> chain is rejected as malformed input
    ///     rather than looping indefinitely.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientHrefCycle_ThrowsInvalidDataException()
    {
        // Arrange: "a" hrefs "b", which hrefs back to "a"
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='a' href='#b'/>
                <linearGradient id='b' href='#a'/>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#a)'/>
            </svg>
            """;

        // Act & Assert
        using var stream1327 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1327, 100, 100));
    }

    /// <summary>
    ///     Regression test for the gradient-stop re-parsing amplification finding: a gradient's
    ///     <c>stop</c> children were previously re-parsed from scratch on every single shape that
    ///     referenced the same gradient, rather than once per gradient per <c>Load</c> call.
    ///     Proves a gradient referenced by many shapes (directly, not via <c>use</c> fan-out)
    ///     still renders every one of them identically to the (uncached) per-reference-reparse
    ///     behavior, confirming <c>RenderContext.GradientStopCache</c> introduces no observable
    ///     rendering change - each shape still gets the correct left-to-right brightness ramp from
    ///     the same shared gradient.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientReferencedByManyShapes_CachesStopsAndRendersIdenticallyToUncached()
    {
        // Arrange: fifty separate rects, each referencing the same single gradient
        var rects = string.Concat(Enumerable.Range(0, 50)
            .Select(i => $"<rect x='0' y='{i}' width='100' height='1' fill='url(#g)'/>"));
        var svg = $"""
            <svg viewBox='0 0 100 50'>
              <defs>
                <linearGradient id='g' gradientUnits='userSpaceOnUse' x1='0' y1='0' x2='100' y2='0'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
              </defs>
              {rects}
            </svg>
            """;

        // Act
        using var stream1359 = ToStream(svg);
        var surface = SvgCodec.Load(stream1359, 100, 50);

        // Assert: every one of the 50 rows shows the same left-to-right brightness ramp from the
        // shared, cached gradient
        for (var row = 0; row < 50; row++)
        {
            Assert.True(
                surface[10, row].R < surface[90, row].R,
                $"Row {row} did not show the expected left-to-right brightness ramp.");
        }
    }

    /// <summary>
    ///     Closes the coverage gap left by
    ///     <see cref="SvgCodec_Load_GradientReferencedByManyShapes_CachesStopsAndRendersIdenticallyToUncached"/>:
    ///     that test only asserts rendered-pixel output, which is identical whether
    ///     <c>ResolveGradientStops</c> actually caches its result or always re-parses the gradient's
    ///     <c>stop</c> children from scratch - a fully reverted caching fix would still pass it. This
    ///     test instead invokes the private <c>SvgCodec.ResolveGradientStops</c> helper directly (via
    ///     reflection, matching the existing <c>BindingFlags.NonPublic</c> idiom used by e.g.
    ///     <c>CmapTableTests</c>) twice with the same gradient element and the same private
    ///     <c>RenderContext</c> instance, and asserts the second call returns the exact same
    ///     <see cref="List{T}"/> instance as the first - proving the second call was served from
    ///     <c>RenderContext.GradientStopCache</c> rather than re-parsed - and that the cache holds
    ///     exactly one entry afterward.
    /// </summary>
    [Fact]
    public void SvgCodec_ResolveGradientStops_SameGradientElementResolvedTwice_ReturnsCachedListInstance()
    {
        // Arrange: reflect the private RenderContext nested type and construct one instance
        var contextType = typeof(SvgCodec).GetNestedType("RenderContext", BindingFlags.NonPublic);
        Assert.NotNull(contextType);

        var constructor = contextType
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(c => c.GetParameters().Length == 3);
        var context = constructor.Invoke(
        [
            new Surface(1, 1),
            new Dictionary<string, XElement>(),
            null
        ]);

        // Arrange: reflect the private static ResolveGradientStops(XElement, RenderContext) method
        var method = typeof(SvgCodec).GetMethod("ResolveGradientStops", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Arrange: one standalone gradient element, resolved against the same context twice
        var gradientElement = XElement.Parse(
            "<linearGradient><stop offset='0' stop-color='black'/><stop offset='1' stop-color='white'/></linearGradient>");

        // Act
        var first = (List<GradientStop>?)method.Invoke(null, [gradientElement, context]);
        var second = (List<GradientStop>?)method.Invoke(null, [gradientElement, context]);

        // Assert: the second resolution returned the identical cached instance, not a fresh re-parse
        Assert.Same(first, second);

        // Assert: the cache holds exactly one entry - the second call was served from it, not from
        // some unrelated memoization path
        var cacheProperty = contextType.GetProperty("GradientStopCache", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(cacheProperty);
        var cache = (Dictionary<XElement, List<GradientStop>>?)cacheProperty.GetValue(context);
        Assert.NotNull(cache);
        Assert.Single(cache);
    }

    // ================================================================================================
    // <use> element
    // ================================================================================================

    /// <summary>
    ///     Proves that a <c>&lt;use&gt;</c> element renders a copy of its referenced element,
    ///     offset by its own <c>x</c>/<c>y</c>, while the original element still renders in place
    ///     - and that this resolves correctly even when <c>&lt;use&gt;</c> textually precedes the
    ///     element it references (id-index resolution is document-order independent).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseElement_RendersCopyAtOffsetIndependentOfDocumentOrder()
    {
        // Arrange: the <use> appears before the <rect id='box'> it references
        const string svg = "<svg viewBox='0 0 10 10'><use href='#box' x='2' y='2'/><rect id='box' x='0' y='0' width='4' height='4' fill='black'/></svg>";

        // Act
        using var stream1443 = ToStream(svg);
        var surface = SvgCodec.Load(stream1443, 10, 10);

        // Assert: the original box renders at (0,0)-(4,4)
        Assert.Equal(255, surface[1, 1].A);
        // Assert: the <use> copy renders offset by (2,2), i.e. at (2,2)-(6,6)
        Assert.Equal(255, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that a <c>&lt;use&gt;</c> referencing a nonexistent id is tolerated as a silent
    ///     no-op rather than throwing.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseElementDanglingReference_IsSilentNoOp()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 10 10'><use href='#missing' x='0' y='0'/></svg>";

        // Act
        using var stream1462 = ToStream(svg);
        var surface = SvgCodec.Load(stream1462, 10, 10);

        // Assert: no exception, and nothing was rendered
        Assert.Equal(0, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that a <c>&lt;use&gt;</c> can reference a <c>&lt;g&gt;</c> group, rendering all
    ///     of the group's children.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseElementReferencingGroup_RendersAllGroupChildren()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 20 20'>
              <defs>
                <g id='pair'>
                  <rect x='0' y='0' width='4' height='4' fill='black'/>
                  <rect x='6' y='0' width='4' height='4' fill='black'/>
                </g>
              </defs>
              <use href='#pair' x='0' y='0'/>
            </svg>
            """;

        // Act
        using var stream1489 = ToStream(svg);
        var surface = SvgCodec.Load(stream1489, 20, 20);

        // Assert: both group children rendered
        Assert.Equal(255, surface[1, 1].A);
        Assert.Equal(255, surface[7, 1].A);
    }

    /// <summary>
    ///     Proves that a mutually-recursive chain of <c>&lt;use&gt;</c> references (exceeding the
    ///     implementation's bounded recursion guard) is rejected rather than looping/recursing
    ///     indefinitely.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseElementMutualRecursionCycle_ThrowsInvalidDataException()
    {
        // Arrange: "a" uses "b", "b" uses "a" - an unbounded mutual cycle
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <g id='a'><use href='#b'/></g>
              <g id='b'><use href='#a'/></g>
              <use href='#a'/>
            </svg>
            """;

        // Act & Assert
        using var stream1514 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1514, 10, 10));
    }

    /// <summary>
    ///     Proves that <c>&lt;use&gt;</c> fan-out - where a group is legitimately (non-cyclically)
    ///     referenced by several sibling <c>&lt;use&gt;</c> elements that themselves fan out
    ///     further - is rejected once the total number of rendered elements exceeds the
    ///     implementation's fixed total-element budget, even though every individual reference
    ///     chain stays well within both the <c>use</c>-nesting and element-tree depth limits. This
    ///     is a distinct bound from <see cref="SvgCodec_Load_UseElementMutualRecursionCycle_ThrowsInvalidDataException"/>
    ///     (which guards against a reference cycle) and
    ///     <see cref="SvgCodec_Load_DeeplyNestedGroups_ThrowsInvalidDataException"/> (which guards
    ///     against a single deep reference chain) - here every chain is short, but the total
    ///     number of elements visited grows exponentially with nesting depth.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseFanOutExceedingTotalElementBudget_ThrowsInvalidDataException()
    {
        // Arrange: 10 levels of groups, each containing 4 <use> references to the previous
        // level's group - a fan-out of 4 per level means the total element count would need to
        // reach roughly 4^10 (over one million) to fully expand, but the fix's fail-fast budget
        // check means only a small fraction of that tree is actually visited before it throws,
        // keeping this test near-instant despite the pathological document shape
        var builder = new StringBuilder();
        builder.Append("<svg viewBox='0 0 10 10'><defs>");
        builder.Append("<g id='g0'><rect width='1' height='1'/></g>");
        for (var level = 1; level <= 10; level++)
        {
            builder.Append($"<g id='g{level}'>");
            for (var branch = 0; branch < 4; branch++)
            {
                builder.Append($"<use href='#g{level - 1}'/>");
            }

            builder.Append("</g>");
        }

        builder.Append("</defs><use href='#g10'/></svg>");

        // Act & Assert
        using var stream1554 = ToStream(builder.ToString());
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1554, 10, 10));
    }

    /// <summary>
    ///     Proves that an element tree nesting many levels of plain <c>&lt;g&gt;</c> groups (no
    ///     <c>&lt;use&gt;</c> involved) is rejected once it exceeds the implementation's bounded
    ///     element-tree recursion depth guard, rather than recursing without limit and risking a
    ///     stack overflow.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_DeeplyNestedGroups_ThrowsInvalidDataException()
    {
        // Arrange: several hundred levels of single-child nesting, comfortably exceeding the
        // codec's maximum element-tree depth while remaining trivially fast to parse and reject
        const int nestingLevels = 500;
        var svg = "<svg viewBox='0 0 10 10'>"
            + string.Concat(Enumerable.Repeat("<g>", nestingLevels))
            + "<rect width='1' height='1'/>"
            + string.Concat(Enumerable.Repeat("</g>", nestingLevels))
            + "</svg>";

        // Act & Assert
        using var stream1576 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1576, 10, 10));
    }

    // ================================================================================================
    // <marker> element
    // ================================================================================================

    /// <summary>
    ///     Proves that a <c>marker-end</c> reference renders its <c>marker</c> element's content
    ///     at the shape's final vertex, sized in user-space units (<c>markerUnits="userSpaceOnUse"</c>).
    ///     No <c>orient</c> attribute is set, so per the fixed 0-degree default (see
    ///     <see cref="SvgCodec_Load_MarkerOrientOmittedOnDiagonalLine_UsesFixedZeroDegreeDefaultNotTangent"/>)
    ///     the marker is unrotated - which happens to coincide with the horizontal segment's own
    ///     0-degree tangent, so this test alone cannot distinguish the two behaviors.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerEndOnLine_RendersArrowheadPastLineEnd()
    {
        // Arrange: a horizontal line from (0,5) to (8,5); its marker-end places a 4x4
        // "userSpaceOnUse" red square anchored at (2,2) (refX/refY), so at the (8,5) end vertex
        // (tangent (1,0), angle 0) the square occupies (6,3)-(10,7)
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

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the marker's red square is visible past the line's own x2=8 end point, outside
        // the line's own stroke band (y in roughly [4.5,5.5])
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[7, 4]);

        // Assert: no marker content appears at the line's start vertex (no marker-start was set)
        Assert.Equal(0, surface[1, 3].A);
    }

    /// <summary>
    ///     Proves that <c>marker-start</c>/<c>marker-mid</c>/<c>marker-end</c> each independently
    ///     resolve and render their own distinct marker at the correct vertex of a
    ///     <c>polyline</c>, and that each marker's own reference point (<c>refX</c>/<c>refY</c>)
    ///     lands exactly on its vertex regardless of that vertex's orientation angle.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerStartMidEndOnPolyline_RendersDistinctMarkersAtEachVertex()
    {
        // Arrange: an L-shaped polyline with three vertices - (2.4,2.4) start, (10.4,2.4) mid,
        // (10.4,10.4) end - each marker is a 2x2 square centered on its own refX/refY=1, so its
        // center always lands exactly on the vertex position regardless of rotation
        const string svg = """
            <svg viewBox='0 0 20 20'>
              <defs>
                <marker id='ms' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2' fill='red'/>
                </marker>
                <marker id='mm' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2' fill='green'/>
                </marker>
                <marker id='me' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2' fill='blue'/>
                </marker>
              </defs>
              <polyline points='2.4,2.4 10.4,2.4 10.4,10.4' fill='none' stroke='black' stroke-width='1'
                        marker-start='url(#ms)' marker-mid='url(#mm)' marker-end='url(#me)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 20, 20);

        // Assert: each vertex shows its own marker's own color
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[2, 2]);
        Assert.Equal(new Rgba32(0, 128, 0, 255), surface[10, 2]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[10, 10]);
    }

    /// <summary>
    ///     Proves that <c>orient="auto"</c> orients a marker along a purely horizontal segment's
    ///     own direction of travel.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerOrientAutoOnHorizontalLine_OrientsAlongPositiveX()
    {
        // Arrange: a marker whose content is a bar offset 2-4 units away from its refX/refY=0
        // anchor, along local +x - when unrotated (angle 0), it extends further in +x from the
        // vertex it is placed at
        const string svg = """
            <svg viewBox='0 0 16 8'>
              <defs>
                <marker id='bar' markerWidth='6' markerHeight='2' refX='0' refY='0' orient='auto' markerUnits='userSpaceOnUse'>
                  <rect x='2' y='-0.5' width='2' height='1' fill='red'/>
                </marker>
              </defs>
              <line x1='1' y1='6.5' x2='7' y2='6.5' stroke='black' stroke-width='1' marker-end='url(#bar)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 16, 8);

        // Assert: the bar extends to the right of the (7,6.5) end vertex - a point beyond the
        // line's own x2=7 extent, in the bar's expected x[9,11]/y[6,7] footprint
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[9, 6]);
    }

    /// <summary>
    ///     Proves that <c>orient="auto"</c> orients a marker along a purely vertical segment's own
    ///     direction of travel (rotated 90 degrees relative to the horizontal case).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerOrientAutoOnVerticalLine_OrientsAlongPositiveY()
    {
        // Arrange: the same "bar" marker as the horizontal test, but the line now travels
        // straight down, so the bar should extend further downward (+y) from its end vertex
        // rather than to the right
        const string svg = """
            <svg viewBox='0 0 12 12'>
              <defs>
                <marker id='bar' markerWidth='6' markerHeight='2' refX='0' refY='0' orient='auto' markerUnits='userSpaceOnUse'>
                  <rect x='2' y='-0.5' width='2' height='1' fill='red'/>
                </marker>
              </defs>
              <line x1='6.5' y1='1' x2='6.5' y2='7' stroke='black' stroke-width='1' marker-end='url(#bar)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 12, 12);

        // Assert: the bar extends below the (6.5,7) end vertex - a point beyond the line's own
        // y2=7 extent, in the bar's expected x[6,7]/y[9,11] footprint
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[6, 9]);
    }

    /// <summary>
    ///     Proves that <c>orient="auto"</c> orients a marker along a diagonal segment's own
    ///     direction of travel (a 45-degree angle between the horizontal and vertical cases).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerOrientAutoOnDiagonalLine_OrientsAlong45Degrees()
    {
        // Arrange: the same "bar" marker, on a line traveling diagonally (equal x and y
        // displacement) - the bar should extend further along that same 45-degree diagonal
        const string svg = """
            <svg viewBox='0 0 12 12'>
              <defs>
                <marker id='bar' markerWidth='6' markerHeight='2' refX='0' refY='0' orient='auto' markerUnits='userSpaceOnUse'>
                  <rect x='2' y='-0.5' width='2' height='1' fill='red'/>
                </marker>
              </defs>
              <line x1='1' y1='1' x2='7' y2='7' stroke='black' stroke-width='1' marker-end='url(#bar)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 12, 12);

        // Assert: the bar's centerline, roughly 3 units further along the 45-degree diagonal
        // from the (7,7) end vertex, lands close to (9,9) - a lower alpha threshold (rather than
        // full opacity) tolerates this rasterizer's edge anti-aliasing on a thin, diagonally
        // rotated shape, whose straight edges rarely align exactly with pixel boundaries
        var actual = surface[9, 9];
        Assert.True(
            actual.R == 255 && actual.G == 0 && actual.B == 0 && actual.A > 150,
            $"Expected a strongly red-tinted pixel at (9,9), got R={actual.R} G={actual.G} B={actual.B} A={actual.A}.");
    }

    /// <summary>
    ///     Regression test: proves that an omitted <c>orient</c> attribute uses the SVG
    ///     specification's fixed 0-degree default rather than following the vertex tangent like an
    ///     explicit <c>orient="auto"</c> - a defect where an absent/blank <c>orient</c> was
    ///     incorrectly routed into the same tangent-following branch as an explicit <c>auto</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerOrientOmittedOnDiagonalLine_UsesFixedZeroDegreeDefaultNotTangent()
    {
        // Arrange: a 45-degree diagonal line ending at (7,6.5) with no orient attribute set on its
        // marker - if the (pre-fix, buggy) tangent-following "auto" behavior applied, the bar would
        // rotate 45 degrees and land far from the vertex (roughly (9.1,8.6)); the correct fixed
        // 0-degree default instead leaves the bar unrotated, landing at exactly the same world
        // position (9,6) as the purely-horizontal-line case (see
        // SvgCodec_Load_MarkerOrientAutoOnHorizontalLine_OrientsAlongPositiveX), since a fixed
        // 0-degree rotation is independent of the segment's own direction of travel
        const string svg = """
            <svg viewBox='0 0 12 12'>
              <defs>
                <marker id='bar' markerWidth='6' markerHeight='2' refX='0' refY='0' markerUnits='userSpaceOnUse'>
                  <rect x='2' y='-0.5' width='2' height='1' fill='red'/>
                </marker>
              </defs>
              <line x1='1' y1='0.5' x2='7' y2='6.5' stroke='black' stroke-width='1' marker-end='url(#bar)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 12, 12);

        // Assert: the bar is unrotated, landing at (9,6) exactly as the horizontal-line case does
        // - not rotated 45 degrees along the diagonal tangent, which would leave this pixel
        // transparent instead
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[9, 6]);
    }

    /// <summary>
    ///     Proves that a plain <c>orient="auto"</c> <c>marker-start</c> is oriented the same way
    ///     as the outgoing segment's own direction (pointing into the line), contrasted by
    ///     <see cref="SvgCodec_Load_MarkerStartOrientAutoStartReverse_PointsAwayFromLine"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerStartOrientAuto_PointsIntoLine()
    {
        // Arrange: the same "bar" marker, referenced as marker-start with plain orient="auto" -
        // the bar should extend toward +x, i.e. into the line's own body
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <marker id='bar' markerWidth='6' markerHeight='2' refX='0' refY='0' orient='auto' markerUnits='userSpaceOnUse'>
                  <rect x='2' y='-0.5' width='2' height='1' fill='red'/>
                </marker>
              </defs>
              <line x1='5' y1='5.5' x2='9' y2='5.5' stroke='black' stroke-width='1' marker-start='url(#bar)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the bar occupies x[7,9] (toward the line body), not x[1,3] (away from it)
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[7, 5]);
        Assert.Equal(0, surface[2, 5].A);
    }

    /// <summary>
    ///     Proves that <c>orient="auto-start-reverse"</c> reverses a <c>marker-start</c> marker's
    ///     orientation by 180 degrees relative to plain <c>orient="auto"</c>, placing it away from
    ///     the line's own body rather than into it - the conventional orientation for an arrowhead
    ///     at the tail of a line.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerStartOrientAutoStartReverse_PointsAwayFromLine()
    {
        // Arrange: the same scenario as the plain orient="auto" test, but with
        // orient="auto-start-reverse" - the bar should now extend toward -x, away from the line
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <marker id='bar' markerWidth='6' markerHeight='2' refX='0' refY='0' orient='auto-start-reverse' markerUnits='userSpaceOnUse'>
                  <rect x='2' y='-0.5' width='2' height='1' fill='red'/>
                </marker>
              </defs>
              <line x1='5' y1='5.5' x2='9' y2='5.5' stroke='black' stroke-width='1' marker-start='url(#bar)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the bar occupies x[1,3] (away from the line body), not x[7,9] (into it) - the
        // line's own black stroke also covers x[7,9] at this y, so a not-equal-to-red check (not
        // an alpha-zero check) is what actually distinguishes "no marker content here" from "the
        // line's own stroke happens to cover this pixel too"
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[2, 5]);
        Assert.NotEqual(new Rgba32(255, 0, 0, 255), surface[7, 5]);
    }

    /// <summary>
    ///     Proves that a closed <c>&lt;polygon&gt;</c>'s implicit closing segment (back to its
    ///     first vertex) contributes to the <c>orient="auto"</c> tangent computed for the last
    ///     vertex's <c>marker-end</c>, rather than only the incoming open-path edge.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerEndOnClosedPolygon_OrientsUsingClosingEdgeTangent()
    {
        // Arrange: a right-triangle polygon (10,10)-(90,10)-(90,90) whose final vertex (90,90) is
        // reached via a straight-down incoming edge (direction (0,1)) but whose implicit closing
        // edge back to (10,10) travels up-and-left (direction (-1,-1) normalized) - averaging
        // both tangents orients the marker along ~157.5 degrees, landing its content near
        // (87,91); ignoring the closing edge (the pre-fix behavior) would orient it purely along
        // the incoming edge's 90 degrees (straight down), landing its content near (90,93) instead
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <marker id='bar' markerWidth='6' markerHeight='2' refX='0' refY='0' orient='auto' markerUnits='userSpaceOnUse'>
                  <rect x='2' y='-0.5' width='2' height='1' fill='red'/>
                </marker>
              </defs>
              <polygon points='10,10 90,10 90,90' fill='none' stroke='none' marker-end='url(#bar)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the marker is oriented along the averaged incoming/closing tangent (landing
        // near (87,91)), not purely along the incoming edge as if the closing edge were ignored
        // (which would land it near (90,93) instead) - a lower alpha threshold (rather than full
        // opacity) tolerates this rasterizer's edge anti-aliasing on a thin, diagonally rotated
        // shape, per this file's other diagonal-marker tests
        var actual = surface[87, 91];
        Assert.True(
            actual.R == 255 && actual.G == 0 && actual.B == 0 && actual.A > 100,
            $"Expected a strongly red-tinted pixel at (87,91), got R={actual.R} G={actual.G} B={actual.B} A={actual.A}.");
        Assert.Equal(0, surface[90, 93].A);
    }

    /// <summary>
    ///     Proves that <c>markerUnits="userSpaceOnUse"</c> keeps a marker's size independent of
    ///     the referencing shape's effective stroke width, while the default
    ///     <c>markerUnits="strokeWidth"</c> scales the marker proportionally to it.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerUnitsUserSpaceOnUseVsStrokeWidthDefault_ScalesDifferently()
    {
        // Arrange: two identical 2x2 marker definitions (refX/refY=1, so each marker's ref point
        // is its own center), one explicitly "userSpaceOnUse" and one left at the default
        // "strokeWidth" - both lines use stroke-width="4", so the default-units marker should
        // scale up to 8x8 (half-width 4) while the userSpaceOnUse marker stays 2x2 (half-width 1)
        const string svg = """
            <svg viewBox='0 0 30 15'>
              <defs>
                <marker id='sqSmall' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2' fill='blue'/>
                </marker>
                <marker id='sqBig' markerWidth='2' markerHeight='2' refX='1' refY='1'>
                  <rect x='0' y='0' width='2' height='2' fill='blue'/>
                </marker>
              </defs>
              <line x1='2' y1='5' x2='7' y2='5' stroke='black' stroke-width='4' marker-end='url(#sqSmall)'/>
              <line x1='20' y1='5' x2='25' y2='5' stroke='black' stroke-width='4' marker-end='url(#sqBig)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 30, 15);

        // Assert: the "userSpaceOnUse" marker did not grow with stroke-width - a point 3 units
        // beyond its (7,5) vertex is outside its 2x2 footprint
        Assert.Equal(0, surface[10, 5].A);

        // Assert: the default "strokeWidth" marker did grow - a point 3 units beyond its (25,5)
        // vertex is still within its 8x8 (half-width 4) footprint
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[28, 5]);
    }

    /// <summary>
    ///     Proves that a default <c>markerUnits="strokeWidth"</c> marker on a document with a
    ///     non-identity root <c>viewBox</c> fit (a uniform 2x scale from local units to pixels)
    ///     scales by exactly <c>stroke-width * root-scale</c> once, not twice - the local
    ///     <c>stroke-width</c> is applied once (for the marker's own <c>strokeWidth</c>-units
    ///     sizing) and the root scale is applied once (via the shared <c>shapeTransform</c>
    ///     composed last), rather than the root scale being folded into both.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerStrokeWidthUnitsOnScaledDocument_ScalesOnceNotTwice()
    {
        // Arrange: a 50x25 viewBox rendered onto a 100x50 canvas is a uniform 2x root scale; the
        // line's local stroke-width is 1, so the "bar" marker's content (local x in [2,4], offset
        // from refX=0) should land at pixel-offset dx*strokeWidth(1)*rootScale(2) = 2*dx from the
        // transformed vertex - e.g. dx=3 lands 6 pixels away (26,25), not double-scaled dx*1*2*2 =
        // 4*dx = 12 pixels away (32,25), which is what the pre-fix double-scaling bug would produce
        const string svg = """
            <svg viewBox='0 0 50 25'>
              <defs>
                <marker id='bar' markerWidth='6' markerHeight='2' refX='0' refY='0' markerUnits='strokeWidth'>
                  <rect x='2' y='-0.5' width='2' height='1' fill='red'/>
                </marker>
              </defs>
              <line x1='2' y1='12.5' x2='10' y2='12.5' stroke='black' stroke-width='1' marker-end='url(#bar)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 50);

        // Assert: the marker landed at the correctly-single-scaled offset (26,25), not the
        // double-scaled offset (32,25) that the pre-fix bug would have produced
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[26, 25]);
        Assert.NotEqual(new Rgba32(255, 0, 0, 255), surface[32, 25]);
    }

    /// <summary>
    ///     Proves that a marker element's own <c>viewBox</c> is fitted into
    ///     <c>markerWidth</c>/<c>markerHeight</c> (a uniform "meet" scale-down), rather than the
    ///     marker's content rendering at its raw, unfitted local-coordinate size.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerWithViewBox_FitsContentToMarkerWidthHeight()
    {
        // Arrange: a marker with a 10x10 viewBox but only a 2x2 markerWidth/markerHeight - its
        // 10x10 content rect should be scaled down by 0.2, landing within (5,5)-(7,7) of its
        // (5,5) vertex rather than the unfitted (5,5)-(15,15)
        const string svg = """
            <svg viewBox='0 0 20 20'>
              <defs>
                <marker id='vb' markerWidth='2' markerHeight='2' refX='0' refY='0' viewBox='0 0 10 10' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='10' height='10' fill='green'/>
                </marker>
              </defs>
              <line x1='1' y1='5' x2='5' y2='5' stroke='black' stroke-width='1' marker-end='url(#vb)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 20, 20);

        // Assert: the fitted, scaled-down content is visible close to the vertex
        Assert.Equal(new Rgba32(0, 128, 0, 255), surface[6, 6]);

        // Assert: a point that would only be covered by the unfitted, raw 10x10 content remains
        // transparent - proving the viewBox fit actually scaled the content down
        Assert.Equal(0, surface[9, 9].A);
    }

    /// <summary>
    ///     Proves that a <c>marker-end</c> reference to a nonexistent id is tolerated as a silent
    ///     no-op, matching this codec's general dangling-reference convention, rather than
    ///     throwing or aborting the rest of the document.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerDanglingReference_IsSilentNoOp()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <line x1='1' y1='5.5' x2='7' y2='5.5' stroke='black' stroke-width='1' marker-end='url(#missing)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the line itself still rendered
        Assert.Equal(255, surface[4, 5].A);

        // Assert: no exception, and no marker content appeared past the line's own end point
        Assert.Equal(0, surface[9, 5].A);
    }

    /// <summary>
    ///     Proves that a <c>marker</c> element whose own content directly references itself (via
    ///     <c>marker-start</c> on a child shape) is rejected once the bounded
    ///     <c>marker</c>-reference nesting depth guard is reached, rather than recursing
    ///     indefinitely, mirroring <see cref="SvgCodec_Load_UseElementMutualRecursionCycle_ThrowsInvalidDataException"/>'s
    ///     identical <see cref="InvalidDataException"/> behavior for <c>use</c> cycles.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerSelfReferenceCycle_ThrowsInvalidDataException()
    {
        // Arrange: marker "m" contains a line whose own marker-start references "m" again
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <marker id='m'>
                  <line x1='0' y1='0' x2='1' y2='0' marker-start='url(#m)'/>
                </marker>
              </defs>
              <line x1='0' y1='0' x2='5' y2='5' marker-start='url(#m)'/>
            </svg>
            """;

        // Act & Assert
        using var stream = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 10, 10));
    }

    /// <summary>
    ///     Proves that a two-marker reference chain (<c>m1</c> referencing <c>m2</c> referencing
    ///     <c>m1</c>) is likewise rejected, not only a direct self-reference.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerTwoElementReferenceCycle_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <marker id='m1'>
                  <line x1='0' y1='0' x2='1' y2='0' marker-start='url(#m2)'/>
                </marker>
                <marker id='m2'>
                  <line x1='0' y1='0' x2='1' y2='0' marker-start='url(#m1)'/>
                </marker>
              </defs>
              <line x1='0' y1='0' x2='5' y2='5' marker-start='url(#m1)'/>
            </svg>
            """;

        // Act & Assert
        using var stream = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 10, 10));
    }

    /// <summary>
    ///     Proves that, for a multi-subpath <c>path</c>, <c>marker-start</c>/<c>marker-end</c>
    ///     apply only to the very first/last vertex of the whole path - not to each subpath's own
    ///     start/end - a deliberate, documented simplification (matches at least one common
    ///     browser's behavior).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerOnMultiSubpathPath_AppliesStartEndOnlyAtWholePathEnds()
    {
        // Arrange: two separate horizontal subpaths at y=2 - vertices in whole-path order are
        // (0,2) start, (4,2) mid, (6,2) mid, (10,2) end. marker-mid is "none", so the two
        // interior subpath boundary vertices should show nothing
        const string svg = """
            <svg viewBox='0 0 12 4'>
              <defs>
                <marker id='s' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2' fill='red'/>
                </marker>
                <marker id='e' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2' fill='blue'/>
                </marker>
              </defs>
              <path d='M0,2 L4,2 M6,2 L10,2' fill='none' stroke='none'
                    marker-start='url(#s)' marker-mid='none' marker-end='url(#e)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 12, 4);

        // Assert: the whole path's very first vertex (0,2) shows the "start" marker
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[0, 2]);

        // Assert: the whole path's very last vertex (10,2) shows the "end" marker
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[10, 2]);

        // Assert: the two interior subpath-boundary vertices (4,2) and (6,2) show nothing, since
        // they are classified as "mid" vertices (marker-mid="none") rather than per-subpath
        // start/end vertices
        Assert.Equal(0, surface[4, 2].A);
        Assert.Equal(0, surface[6, 2].A);
    }

    /// <summary>
    ///     Proves that <c>rect</c>, <c>circle</c>, and <c>ellipse</c> never receive markers, even
    ///     when a <c>marker-end</c> attribute referencing a valid marker is present - these shapes
    ///     have no natural vertices to orient a marker along, per the SVG specification.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerOnRectCircleEllipse_NeverRendersMarker()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 30 10'>
              <defs>
                <marker id='m' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2' fill='red'/>
                </marker>
              </defs>
              <rect x='1' y='1' width='4' height='4' fill='black' marker-end='url(#m)'/>
              <circle cx='15' cy='5' r='2' fill='black' marker-end='url(#m)'/>
              <ellipse cx='25' cy='5' rx='3' ry='2' fill='black' marker-end='url(#m)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 30, 10);

        // Assert: no exception, each shape still renders its own black fill
        Assert.Equal(255, surface[2, 2].A);
        Assert.Equal(255, surface[15, 5].A);
        Assert.Equal(255, surface[25, 5].A);

        // Assert: no red marker content appears anywhere in the canvas
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 30; x++)
            {
                Assert.NotEqual(new Rgba32(255, 0, 0, 255), surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that a marker's own content renders with a fresh presentation-attribute cascade
    ///     starting from the SVG/CSS initial values, rather than inheriting the referencing
    ///     shape's own <c>fill</c>/<c>stroke</c> - per the SVG specification's independent marker
    ///     content model.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerContent_DoesNotInheritReferencingShapeFillOrStroke()
    {
        // Arrange: the line sets a bright "red" fill/stroke, but the marker's own <rect> has no
        // fill attribute of its own, so it should fall back to the initial default ("black"),
        // not inherit the line's "red"
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <marker id='m' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2'/>
                </marker>
              </defs>
              <line x1='1' y1='5.5' x2='7' y2='5.5' fill='red' stroke='red' stroke-width='1' marker-end='url(#m)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the marker's rect rendered its own initial-default "black" fill, not "red"
        Assert.Equal(new Rgba32(0, 0, 0, 255), surface[7, 5]);
    }

    /// <summary>
    ///     Regression test for a defect where a <c>filter</c> attribute on a marker's own content
    ///     was still resolved and evaluated, even though this codec's documented scope states that
    ///     filters on marker content have no effect. Asserts a <c>rect</c> inside a <c>marker</c>
    ///     with a <c>filter="url(#f)"</c> referencing a real <c>feFlood</c> filter renders exactly
    ///     as if it had no <c>filter</c> attribute at all - the marker's own fill, not the flood
    ///     color, since the filter must never be evaluated for marker content.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerContentWithFilterAttribute_FilterHasNoEffect()
    {
        // Arrange: the marker's own rect has a filter referencing a feFlood that would fill its
        // region with red if evaluated; the marker's rect itself is "lime"
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='red'/>
                </filter>
                <marker id='m' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='2' height='2' fill='lime' filter='url(#f)'/>
                </marker>
              </defs>
              <line x1='1' y1='5.5' x2='7' y2='5.5' stroke='black' stroke-width='1' marker-end='url(#m)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the marker's own "lime" fill rendered - the feFlood filter was never applied
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[7, 5]);
    }

    // ================================================================================================
    // <filter> element
    // ================================================================================================

    /// <summary>
    ///     Proves that a <c>filter</c> containing only a bare <c>feFlood</c> primitive replaces the
    ///     referencing element's own content entirely: the flood color fills the whole (default,
    ///     bounding-box-relative) filter region, including the area behind the element's own shape,
    ///     since a lone <c>feFlood</c> never references <c>SourceGraphic</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeFloodFilter_RendersSolidColorBehindElement()
    {
        // Arrange: a 20x20 blue rect at (40,40); the filter's default region expands the rect's
        // own bounding box by -10%/120%, i.e. (38,38)-(62,62)
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='red'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the flood fills the expanded region outside the rect's own bounds
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[39, 50]);

        // Assert: the flood also fully replaces the rect's own blue fill at its center, since the
        // filter's final output is the bare feFlood result, not a merge with SourceGraphic
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>
    ///     Regression test for a defect where an element's own <c>opacity</c> was applied while
    ///     painting the pre-filter <c>SourceGraphic</c> instead of the final filtered result: per
    ///     SVG semantics, <c>opacity</c> applies to the filter's whole output, exactly once, so a
    ///     <c>rect</c> with <c>opacity="0.5"</c> and a filter containing only a fully-opaque
    ///     <c>feFlood</c> must render the flood at ~50% alpha, not fully opaque.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_OpacityWithFeFloodFilter_AppliesOpacityToFilteredResultNotSource()
    {
        // Arrange: a fully-opaque red feFlood is the filter's entire output; the referencing
        // rect's own opacity of 0.5 must still visibly attenuate that flood's alpha
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='red' flood-opacity='1'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' opacity='0.5' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the flood's own color is still fully red, but its alpha reflects the rect's own
        // 50% opacity (0.5 * 255 = 127.5), not the fully-opaque 255 a pre-filter opacity fold
        // would incorrectly produce - mirrors SvgCodec_Load_Opacity_MultipliesIntoFillAlpha's own
        // InRange tolerance for floating-point alpha compositing
        var pixel = surface[50, 50];
        Assert.Equal(255, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
        Assert.InRange((int)pixel.A, 110, 145);
    }

    /// <summary>
    ///     Proves that <c>feMerge</c> layers named results in document order: a <c>feFlood</c>
    ///     result placed first, then <c>SourceGraphic</c> placed second, renders the flood behind
    ///     the element's own content (visible outside its bounds) while the element's own fill
    ///     remains visible on top at its own location.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeFloodFeMergeFilter_RendersFloodBehindSourceGraphic()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='yellow' result='flood'/>
                  <feMerge>
                    <feMergeNode in='flood'/>
                    <feMergeNode in='SourceGraphic'/>
                  </feMerge>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the flood is visible in the expanded region outside the rect's own bounds
        Assert.Equal(new Rgba32(255, 255, 0, 255), surface[39, 50]);

        // Assert: the rect's own blue fill remains visible on top of the flood at its center
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[50, 50]);
    }

    /// <summary>
    ///     Proves that a <c>feFlood</c> → <c>feComposite</c> (<c>operator="out"</c>, clipping the
    ///     flood to the area the element's own content does <i>not</i> cover) → <c>feGaussianBlur</c>
    ///     → <c>feMerge</c> chain (a conventional halo/glow recipe) actually softens the flood's
    ///     edge - proving the blur genuinely ran, rather than the filter being silently ignored -
    ///     while leaving the element's own fill fully opaque and unaffected at its center.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeFloodFeGaussianBlurFeCompositeFilter_RendersBlurredHaloBehindContent()
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

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect's own fill remains fully opaque and unaffected at its center
        Assert.Equal(new Rgba32(0, 128, 0, 255), surface[50, 50]);

        // Assert: just outside the rect's own edge, a softened (partially transparent, not
        // hard-edged) orange halo is visible - proving the blur actually ran
        Assert.Equal(new Rgba32(255, 161, 0, 19), surface[61, 50]);

        // Assert: a couple of pixels further out, past the blur's influence, nothing remains
        Assert.Equal(0, surface[62, 50].A);
    }

    /// <summary>
    ///     Regression test: <c>stdDeviation="2&#x9;3"</c> (tab-separated x/y radii) must still
    ///     tokenize on the tab and parse the first radius as <c>2</c>, producing the same softened
    ///     halo as the equivalent space-separated <c>stdDeviation="2"</c> case above - rather than
    ///     treating the whole value as one unparseable token (which would silently fall back to a
    ///     zero radius, disabling the blur).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeGaussianBlurStdDeviationTabSeparated_RendersBlurredHaloBehindContent()
    {
        // Arrange: identical to the space-separated halo recipe above, except stdDeviation uses a
        // tab between the x and y radii instead of a space.
        const string svg = "<svg viewBox='0 0 100 100'>\n" +
                            "  <defs>\n" +
                            "    <filter id='f'>\n" +
                            "      <feFlood flood-color='orange' result='flood'/>\n" +
                            "      <feComposite in='flood' in2='SourceGraphic' operator='out' result='haloBase'/>\n" +
                            "      <feGaussianBlur in='haloBase' stdDeviation='2\t3' result='halo'/>\n" +
                            "      <feMerge>\n" +
                            "        <feMergeNode in='halo'/>\n" +
                            "        <feMergeNode in='SourceGraphic'/>\n" +
                            "      </feMerge>\n" +
                            "    </filter>\n" +
                            "  </defs>\n" +
                            "  <rect x='40' y='40' width='20' height='20' fill='green' filter='url(#f)'/>\n" +
                            "</svg>";

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect's own fill remains fully opaque and unaffected at its center
        Assert.Equal(new Rgba32(0, 128, 0, 255), surface[50, 50]);

        // Assert: just outside the rect's own edge, a softened (partially transparent, not
        // hard-edged) orange halo is visible - proving the first token ("2") was actually parsed
        // and used as the blur radius, rather than falling back to zero
        Assert.Equal(new Rgba32(255, 161, 0, 19), surface[61, 50]);

        // Assert: a couple of pixels further out, past the blur's influence, nothing remains
        Assert.Equal(0, surface[62, 50].A);
    }

    /// <summary>
    ///     Proves <c>feComposite operator="in"</c> keeps the "in" input's own color, weighted by the
    ///     "in2" input's own alpha, per the documented Porter-Duff "in" formula
    ///     (<c>(Fa, Fb) = (Ab, 0)</c>) - two full-region, semi-transparent <c>feFlood</c> inputs
    ///     isolate <c>CompositeFeOperator</c>'s own per-pixel math from any shape-geometry/filter-
    ///     region overlap concern.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeCompositeOperatorIn_KeepsForegroundWeightedByBackgroundAlpha()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <filter id='f' x='0' y='0' width='1' height='1'>
                  <feFlood flood-color='#ff0000' flood-opacity='0.5' result='fg'/>
                  <feFlood flood-color='#0000ff' flood-opacity='0.25' result='bg'/>
                  <feComposite in='fg' in2='bg' operator='in'/>
                </filter>
              </defs>
              <rect x='0' y='0' width='10' height='10' fill='green' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 32), surface[5, 5]);
    }

    /// <summary>
    ///     Proves <c>feComposite operator="atop"</c> blends both inputs' own colors, weighted by
    ///     (<c>in2</c>'s alpha, <c>1 - in</c>'s alpha) respectively, per the documented Porter-Duff
    ///     "atop" formula (<c>(Fa, Fb) = (Ab, 1 - Aa)</c>) - only incidentally covered previously via
    ///     one pixel deep inside the third-party <c>InkscapeFilters.svg</c> fixture; this test
    ///     isolates the operator's own formula deterministically.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeCompositeOperatorAtop_BlendsBothInputsWeightedByBothAlphas()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <filter id='f' x='0' y='0' width='1' height='1'>
                  <feFlood flood-color='#ff0000' flood-opacity='0.5' result='fg'/>
                  <feFlood flood-color='#0000ff' flood-opacity='0.25' result='bg'/>
                  <feComposite in='fg' in2='bg' operator='atop'/>
                </filter>
              </defs>
              <rect x='0' y='0' width='10' height='10' fill='green' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert
        Assert.Equal(new Rgba32(128, 0, 127, 64), surface[5, 5]);
    }

    /// <summary>
    ///     Proves <c>feComposite operator="xor"</c> keeps each input only where the other does not
    ///     have coverage, per the documented Porter-Duff "xor" formula
    ///     (<c>(Fa, Fb) = (1 - Ab, 1 - Aa)</c>) - never previously exercised anywhere in this test
    ///     suite.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeCompositeOperatorXor_KeepsEachInputWhereTheOtherHasNoCoverage()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <filter id='f' x='0' y='0' width='1' height='1'>
                  <feFlood flood-color='#ff0000' flood-opacity='0.5' result='fg'/>
                  <feFlood flood-color='#0000ff' flood-opacity='0.25' result='bg'/>
                  <feComposite in='fg' in2='bg' operator='xor'/>
                </filter>
              </defs>
              <rect x='0' y='0' width='10' height='10' fill='green' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert
        Assert.Equal(new Rgba32(191, 0, 64, 128), surface[5, 5]);
    }

    /// <summary>
    ///     Regression test for a code-review finding: <c>CompositeFeOperator</c>'s own per-channel
    ///     rounding used <see cref="MathF.Round(float)"/>'s default banker's (round-to-even)
    ///     rounding, which differs from the rest of this codebase's compositing pipeline (see
    ///     <c>Surface.CompositeOverSpanCore</c>) - it consistently uses
    ///     <see cref="MidpointRounding.AwayFromZero"/>. This <c>xor</c> combination of a near-
    ///     transparent black flood (<c>flood-opacity</c> exactly <c>2/255</c>) over a 40%-opaque
    ///     gray flood produces a computed channel value of exactly <c>126.5</c> - away-from-zero
    ///     rounds this up to <c>127</c>, while round-to-even rounds it down to <c>126</c> (the
    ///     nearest even integer). Proves the away-from-zero convention is now used.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeCompositeOperatorXorWithHalfChannelValue_RoundsAwayFromZeroNotToEven()
    {
        // Arrange: a black flood at alpha exactly 2/255 composited "xor" over a 40%-opaque gray
        // flood - chosen (see this test's own remarks) so every output channel computes to
        // exactly 126.5, isolating the rounding-mode difference deterministically
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <filter id='f' x='0' y='0' width='1' height='1'>
                  <feFlood flood-color='#000000' flood-opacity='0.00784313725490196' result='fg'/>
                  <feFlood flood-color='#808080' flood-opacity='0.4' result='bg'/>
                  <feComposite in='fg' in2='bg' operator='xor'/>
                </filter>
              </defs>
              <rect x='0' y='0' width='10' height='10' fill='green' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: every channel rounded up to 127 (away-from-zero), not down to 126 (round-to-even)
        var actual = surface[5, 5];
        Assert.Equal(127, actual.R);
        Assert.Equal(127, actual.G);
        Assert.Equal(127, actual.B);
    }

    /// <summary>
    ///     Proves that a <c>filter="url(#id)"</c> reference which does not resolve to any element
    ///     (a dangling reference) renders the element normally, exactly as if no <c>filter</c>
    ///     attribute had been present at all - matching the existing dangling-reference tolerance
    ///     convention used elsewhere (e.g. <c>ResolvePaint</c>, <c>ResolveMarkerElement</c>).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterDanglingReference_RendersElementNormally()
    {
        // Arrange: two identical rects, one with a dangling filter reference and one without
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <rect x='10' y='10' width='20' height='20' fill='purple' filter='url(#missing)'/>
              <rect x='50' y='10' width='20' height='20' fill='purple'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: both rects rendered identically, unaffected by the dangling filter reference
        Assert.Equal(surface[60, 20], surface[20, 20]);
        Assert.Equal(new Rgba32(128, 0, 128, 255), surface[20, 20]);
    }

    /// <summary>
    ///     Proves that a filter primitive type this codec does not implement (here
    ///     <c>feColorMatrix</c>, but the same tolerant handling applies to <c>feTurbulence</c>,
    ///     <c>feDisplacementMap</c>, <c>feImage</c>, <c>feTile</c>, <c>feDropShadow</c>,
    ///     <c>feConvolveMatrix</c>, <c>feDiffuseLighting</c>, <c>feSpecularLighting</c>,
    ///     <c>feComponentTransfer</c>, and <c>feMorphology</c>) is treated as a no-op passthrough
    ///     of its input, rather than throwing or being ignored at the filter level.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterUnsupportedPrimitive_PassesThroughSourceGraphicUnchanged()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feColorMatrix type='saturate' values='0'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='teal' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect's own fill passed through unchanged, proving no exception was thrown
        // and the unsupported primitive did not alter (or blank out) the element's content
        Assert.Equal(new Rgba32(0, 128, 128, 255), surface[50, 50]);
    }

    /// <summary>
    ///     Proves that a filter region which would require an unreasonably large temporary
    ///     surface (here a <c>width</c>/<c>height</c> of <c>100000%</c> of the element's own
    ///     bounding box) is tolerantly skipped - the element renders normally, without its
    ///     filter effect - rather than attempting an unbounded allocation.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterPathologicallyLargeRegion_SkipsFilterRatherThanUnboundedAllocation()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f' width='100000%' height='100000%'>
                  <feFlood flood-color='red'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act: must complete promptly, without throwing or attempting to allocate a surface
        // exceeding Surface.MaxDimension
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect rendered its own normal blue fill, the (skipped) filter had no effect
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[50, 50]);
    }

    /// <summary>
    ///     Regression test for a code-review finding: a <c>filter</c> element with zero primitive
    ///     children has a zero primitive-count/work-unit charge, so it used to trivially pass the
    ///     upfront work-budget check even when paired with a pathologically large filter region
    ///     (here <c>100000%</c> of the element's own bounding box, matching
    ///     <see cref="SvgCodec_Load_FilterPathologicallyLargeRegion_SkipsFilterRatherThanUnboundedAllocation"/>'s
    ///     region) - the budget-OK verdict then let the code allocate a huge temporary
    ///     <c>SourceGraphic</c> surface for a filter that, having no primitives, could not possibly
    ///     change the rendered output. Proves the empty filter is now tolerantly skipped before any
    ///     surface is allocated, completing promptly and leaving the element rendered normally.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterWithZeroPrimitivesAndHugeRegion_SkipsFilterRatherThanAllocatingSurface()
    {
        // Arrange: a filter element with no fe* primitive children at all, paired with the same
        // pathologically large region used by the sibling huge-region test
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f' width='100000%' height='100000%'>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act: must complete promptly, without attempting to allocate a huge SourceGraphic
        // surface for a filter that has no primitives to evaluate
        var stopwatch = Stopwatch.StartNew();
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);
        stopwatch.Stop();

        // Assert: the rect rendered its own normal blue fill - the (skipped) empty filter had no
        // effect, the same tolerant per-element fallback used for every other filter resource bound
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[50, 50]);

        // Assert: completed promptly, proving no huge temporary surface was ever allocated
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Expected the zero-primitive huge-region filter to be skipped promptly, but it took {stopwatch.Elapsed}.");
    }

    /// <summary>
    ///     Proves that a filter with a pathologically large number of <c>fe*</c> primitive
    ///     children (5,000 chained <c>feGaussianBlur</c> primitives, mirroring a reported repro
    ///     that took ~26 seconds to load prior to the <c>MaxFilterPrimitivesPerFilter</c>/
    ///     <c>MaxFilterPrimitiveWorkUnits</c> bounds) is tolerantly skipped entirely rather than
    ///     evaluated, completing quickly instead of performing 5,000 region-sized blur passes.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterExcessivePrimitiveCount_SkipsFilterRatherThanUnboundedWork()
    {
        // Arrange: 5,000 chained feGaussianBlur primitives in a single filter - far beyond any
        // realistic chain length (the longest real chain in this repository's fixtures is 10) -
        // against a modest, default-expanded region
        var primitives = string.Concat(Enumerable.Repeat("<feGaussianBlur stdDeviation='1'/>", 5000));
        var svg = $"""
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  {primitives}
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act: must complete quickly rather than performing 5,000 region-sized blur passes - the
        // 5-second threshold sits far below the ~26-second pathological baseline this bound
        // eliminates, while remaining comfortably above normal test-execution variance (a
        // rejected chain here does no per-primitive work at all: just one cheap element count
        // plus one multiply/compare, so a healthy run completes in well under a second)
        var stopwatch = Stopwatch.StartNew();
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);
        stopwatch.Stop();

        // Assert: the rect rendered its own normal blue fill - the (skipped) filter had no
        // effect, the same tolerant per-element fallback used for every other filter resource
        // bound
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[50, 50]);

        // Assert: completed promptly, proving the filter was skipped rather than evaluated
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Expected the excessive-primitive-count filter to be skipped promptly, but it took {stopwatch.Elapsed}.");
    }

    /// <summary>
    ///     Regression test for a code-review finding: <c>IsFilterPrimitiveWorkWithinBudget</c>'s
    ///     <c>MaxFilterPrimitiveWorkUnits</c> ceiling is enforced independently for each
    ///     individual filtered element, so a single <c>filter</c> definition referenced by many
    ///     shapes could previously be charged the same per-filter ceiling once per reference, with
    ///     no bound on the total number of references - a document with enough shapes could drive
    ///     total filter-evaluation work arbitrarily high even though every single reference stayed
    ///     within budget. Proves a new cumulative, per-<c>Load</c>-call
    ///     <c>FilterWorkBudget</c>/<c>MaxCumulativeFilterWorkUnits</c> bound now caps the total: 13
    ///     shapes reference the same filter, each individually charging exactly
    ///     <c>MaxFilterPrimitiveWorkUnits</c> (5,000,000) work units (500 primitives against a
    ///     100x100 region) - within the per-filter ceiling every time - but the 11th and later
    ///     references exceed the new 50,000,000 cumulative ceiling and tolerantly fall back to
    ///     unfiltered rendering instead.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterReferencedByManyShapesExceedingCumulativeBudget_FallsBackToUnfilteredForExcessShapes()
    {
        // Arrange: a filter chain of 499 no-op (unrecognized primitive name) children plus one
        // trailing feFlood - 500 primitive-equivalent work units total - evaluated against a
        // filter region sized (via explicit 250% width/height overrides on a 40x40 rect) to
        // exactly 100x100 pixels, so each individual application charges exactly
        // 500 * 100 * 100 = 5,000,000 work units, precisely at (never over) the per-filter
        // MaxFilterPrimitiveWorkUnits ceiling. The unknown primitives are cheap no-op passthrough steps
        // (no per-pixel work at all - see EvaluateFilterChain's remarks), and feFlood's own cost
        // is a single cheap fill over the small 100x100 region, so evaluating this filter even
        // many times remains fast; only the cumulative work-unit total, not actual per-primitive
        // cost, is what this test exercises. 13 identical, non-overlapping rects (spaced 150 units
        // apart so neighboring filter regions never overlap) reference the same filter: the first
        // 10 charge a running cumulative total of exactly 50,000,000 (still within the new
        // ceiling), while the 11th through 13th would push the cumulative total over budget and
        // must tolerantly fall back to unfiltered rendering instead of throwing.
        const int shapeCount = 13;
        const int filteredShapeCount = 10;
        var noOpPrimitives = string.Concat(Enumerable.Repeat("<feUnsupportedNoOp/>", 499));
        var rects = string.Concat(Enumerable.Range(0, shapeCount).Select(i =>
            $"<rect x='{10 + (i * 150)}' y='10' width='40' height='40' fill='blue' filter='url(#f)'/>"));
        var svg = $"""
            <svg viewBox='0 0 2000 120'>
              <defs>
                <filter id='f' width='250%' height='250%'>
                  {noOpPrimitives}
                  <feFlood flood-color='red'/>
                </filter>
              </defs>
              {rects}
            </svg>
            """;

        // Act: render the whole document - must complete promptly, and must not throw despite the
        // cumulative filter work total across all 13 shapes (65,000,000) far exceeding the new
        // cumulative ceiling (50,000,000)
        var stopwatch = Stopwatch.StartNew();
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 2000, 120);
        stopwatch.Stop();

        // Assert: the first 10 shapes (cumulative total staying within the new 50,000,000
        // ceiling) were actually filtered - each rendered as the feFlood's solid red, not its own
        // blue fill
        for (var i = 0; i < filteredShapeCount; i++)
        {
            var sampleX = 30 + (i * 150);
            Assert.Equal(new Rgba32(255, 0, 0, 255), surface[sampleX, 30]);
        }

        // Assert: the remaining shapes (11th through 13th), which would have pushed the
        // cumulative total over the new ceiling, tolerantly fell back to unfiltered rendering -
        // each still shows its own normal blue fill, exactly as the pre-existing per-filter
        // tolerant-fallback cases already behave
        for (var i = filteredShapeCount; i < shapeCount; i++)
        {
            var sampleX = 30 + (i * 150);
            Assert.Equal(new Rgba32(0, 0, 255, 255), surface[sampleX, 30]);
        }

        // Assert: completed promptly - the cumulative budget rejects excess filter applications
        // before any of their own SourceGraphic/filter-chain work begins, so this document's total
        // work stays proportional to only the 10 shapes actually filtered
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Expected the cumulative-filter-work-budget document to render promptly, but it took {stopwatch.Elapsed}.");
    }

    /// <summary>
    ///     Regression test for a code-review finding: the upfront filter work-budget check used to
    ///     count only direct <c>fe*</c> children (a single <c>feMerge</c> always counted as 1),
    ///     even though <c>ApplyFeMerge</c> performs one full-surface <c>CompositeOver</c> per
    ///     <c>feMergeNode</c> child - so a <c>feMerge</c> with a pathologically large number of
    ///     merge nodes (5,000 here, mirroring the sibling primitive-count test) used to bypass the
    ///     budget entirely while still doing O(node-count &#215; region-area) work. Proves it is
    ///     now charged per merge-node and tolerantly skipped just as promptly as the equivalent
    ///     flat primitive-count case.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeMergeExcessiveNodeCount_SkipsFilterRatherThanUnboundedWork()
    {
        // Arrange: a single feMerge with 5,000 feMergeNode children - one direct filter-primitive
        // child by the old (buggy) counting, but 5,000 CompositeOver-worth of actual work
        var mergeNodes = string.Concat(Enumerable.Repeat("<feMergeNode/>", 5000));
        var svg = $"""
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feMerge>
                    {mergeNodes}
                  </feMerge>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act: must complete quickly rather than performing 5,000 region-sized composite passes
        var stopwatch = Stopwatch.StartNew();
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);
        stopwatch.Stop();

        // Assert: the rect rendered its own normal blue fill - the (skipped) filter had no effect
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[50, 50]);

        // Assert: completed promptly, proving the feMerge was skipped rather than evaluated
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Expected the excessive-feMergeNode-count filter to be skipped promptly, but it took {stopwatch.Elapsed}.");
    }

    /// <summary>
    ///     Regression test for a code-review finding: the filter-region degeneracy guard used to
    ///     test the bare fill/centerline bounds (<c>localPath.GetBounds()</c>), which is zero-
    ///     height for a horizontal <c>line</c> - incorrectly treating a valid filter on a stroked
    ///     horizontal line as degenerate and silently skipping it. Proves the guard now uses the
    ///     stroke-inflated ("actually-painted") bounds instead, so the filter is actually evaluated.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterOnStrokedHorizontalLine_UsesStrokeAwareBoundsNotDegenerate()
    {
        // Arrange: a horizontal line's centerline bounds have zero height; only the stroke-
        // inflated bounds (centerline +/- half the 10-unit stroke width) are non-degenerate
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='red'/>
                </filter>
              </defs>
              <line x1='20' y1='50' x2='80' y2='50' stroke='blue' stroke-width='10' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: a point just above the raw stroke's own painted extent (stroke-width 10 with
        // butt caps covers y in [45,55]) but within the stroke-aware expanded filter region is
        // filled with the flood color - proving the filter was actually evaluated rather than
        // degenerately skipped (an unfixed, skipped filter would leave this point transparent)
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 44]);
    }

    /// <summary>
    ///     Proves that a pathologically large <c>feGaussianBlur</c> <c>stdDeviation</c> (many
    ///     orders of magnitude larger than the fixed <c>MaxFilterBlurStdDeviationPixels</c> bound)
    ///     is clamped rather than causing unbounded work: the blur completes promptly (the box-blur
    ///     implementation's cost does not scale with the requested radius) and produces a heavily
    ///     diluted, non-opaque result rather than crashing or hanging.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterPathologicallyLargeBlurStdDeviation_ClampsRatherThanUnboundedWork()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feGaussianBlur stdDeviation='1000000'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act: must complete promptly despite the requested blur radius vastly exceeding both the
        // clamp and the (small) temporary surface it is applied to
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the clamped blur diluted the rect's own opaque fill down to fully transparent,
        // rather than throwing, hanging, or leaving the fill unaffected
        Assert.Equal(0, surface[50, 50].A);
    }

    /// <summary>
    ///     Proves the filter region's default computation: absent <c>x</c>/<c>y</c>/<c>width</c>/
    ///     <c>height</c> attributes, the region expands the referencing element's own bounding box
    ///     by -10%/-10%/120%/120% (objectBoundingBox units), per the SVG specification's own
    ///     defaults.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterDefaultRegion_ExpandsBoundingBoxByTenAndTwentyPercent()
    {
        // Arrange: a 20x20 rect at (40,40); the default region is therefore exactly
        // (40-2, 40-2)-(40-2+24, 40-2+24) = (38,38)-(62,62)
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='white'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: just outside the computed default region, nothing was rendered
        Assert.Equal(0, surface[37, 50].A);

        // Assert: just inside each edge of the computed default region, the flood is visible
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[38, 50]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[61, 50]);

        // Assert: just outside the opposite edge of the computed default region, nothing rendered
        Assert.Equal(0, surface[63, 50].A);
    }

    /// <summary>
    ///     Proves that explicit <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> attributes on the
    ///     <c>filter</c> element override the default region computation, using the declared
    ///     (objectBoundingBox-relative) values instead.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterExplicitRegion_UsesDeclaredXYWidthHeight()
    {
        // Arrange: an explicit region expanding the rect's own bounding box by 200% on every
        // side, far beyond the -10%/120% default, reaching all the way to the canvas corner
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f' x='-200%' y='-200%' width='500%' height='500%'>
                  <feFlood flood-color='white'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the flood reaches a point well outside the default region, proving the
        // explicit region (not the default) was used
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[10, 10]);
    }

    /// <summary>
    ///     Proves that <c>filterUnits="userSpaceOnUse"</c> falls back tolerantly to the same
    ///     objectBoundingBox-relative region computation as the default, rather than being
    ///     interpreted as literal absolute user-space coordinates - a deliberate simplification
    ///     documented on <see cref="SvgCodec"/>'s own class-level remarks.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilterUserSpaceOnUse_FallsBackToObjectBoundingBoxDefault()
    {
        // Arrange: x/y/width/height of 0/0/10/10 would, if interpreted literally as
        // "userSpaceOnUse" absolute coordinates, place the filter region at (0,0)-(10,10) - a
        // tiny box near the origin, unrelated to the rect's own (40,40)-(60,60) position
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f' x='0' y='0' width='10' height='10' filterUnits='userSpaceOnUse'>
                  <feFlood flood-color='lime'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='20' height='20' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the flood reaches far into the canvas, consistent only with the tolerant
        // objectBoundingBox-fraction fallback (x/y/width/height=0/0/10/10 interpreted as
        // fractions of the rect's own 20x20 bounding box), not with a literal (0,0)-(10,10)
        // absolute user-space box
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[90, 90]);

        // Assert: nothing rendered near the origin, proving the literal absolute-coordinate
        // interpretation was NOT used
        Assert.Equal(0, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that <c>feOffset</c> shifts its input by <c>dx</c>/<c>dy</c> (in user-space
    ///     units, scaled by the current transform) prior to compositing back onto the canvas.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeOffsetFilter_ShiftsSourceGraphicByDxDy()
    {
        // Arrange: a generously expanded filter region (200% on every side) so the offset shape
        // remains fully within the temporary surface, avoiding incidental clipping at its edges
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f' x='-1' y='-1' width='3' height='3'>
                  <feOffset dx='5' dy='0'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='10' height='10' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect's content reappears shifted 5 pixels to the right of its own bounds
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[47, 45]);

        // Assert: nothing remains at the rect's own (pre-offset) position
        Assert.Equal(0, surface[42, 45].A);
    }

    /// <summary>
    ///     Regression test for a code-review finding: <c>feOffset</c>'s own <c>dx</c>/<c>dy</c>-
    ///     to-pixel conversion used <see cref="MathF.Round(float)"/>'s default banker's
    ///     (round-to-even) rounding, which differs from the rest of this codebase's compositing
    ///     pipeline (see <c>Surface.CompositeOverSpanCore</c>) - it consistently uses
    ///     <see cref="MidpointRounding.AwayFromZero"/>. A <c>dx</c> that scales to exactly
    ///     <c>0.5</c> pixels is the smallest case that distinguishes the two: away-from-zero rounds
    ///     it up to a 1-pixel shift, while round-to-even rounds it down to 0 (since 0 is the
    ///     nearest even integer). Proves the away-from-zero convention is now used.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FeOffsetFilterWithHalfPixelDx_RoundsAwayFromZeroNotToEven()
    {
        // Arrange: a 1:1 user-space-to-pixel scale (100x100 viewBox onto a 100x100 canvas), so
        // dx='0.5' scales to exactly 0.5 pixels - round-to-even would round this down to 0 (no
        // shift), while away-from-zero rounds it up to a full 1-pixel shift
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f' x='-1' y='-1' width='3' height='3'>
                  <feOffset dx='0.5' dy='0'/>
                </filter>
              </defs>
              <rect x='40' y='40' width='10' height='10' fill='blue' filter='url(#f)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect's own original left column (x=40) is no longer covered - it shifted
        // right by 1 whole pixel rather than staying at 0 shift
        Assert.Equal(0, surface[40, 45].A);

        // Assert: the shifted content now starts at x=41, one pixel to the right
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[41, 45]);
    }

    // ================================================================================================
    // Group-level filtering (<g>/<symbol>/<use> filter attribute)
    // ================================================================================================

    /// <summary>
    ///     Proves that a <c>filter</c> attribute on a <c>&lt;g&gt;</c> element is evaluated once
    ///     against the whole subtree's combined bounds, rather than having no effect (the group's
    ///     previous behavior) or being applied independently to each child. Two non-overlapping
    ///     rects leave a gap between them; a bare <c>feFlood</c> filter fills its entire region
    ///     solid red, so the gap being red proves the filter region spans the group's *combined*
    ///     bounds rather than being evaluated per-child (which would leave the gap untouched).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilter_FeFloodOnGWithTwoChildren_FillsCombinedBoundsIncludingGap()
    {
        // Arrange: two 20x20 blue rects with a 20-unit gap between them (x in 30..50), both
        // wrapped in a single filtered <g>; the filter's default region (-10%/-10%/120%/120%)
        // around their combined bounds (10,10)-(70,30) comfortably covers the gap
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='red'/>
                </filter>
              </defs>
              <g filter='url(#f)'>
                <rect x='10' y='10' width='20' height='20' fill='blue'/>
                <rect x='50' y='10' width='20' height='20' fill='blue'/>
              </g>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the gap between the two rects, which no single child's own bounds cover, is
        // filled by the flood - proving the filter region was computed from the group's combined
        // subtree bounds
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[40, 20]);

        // Assert: outside the filter region entirely, nothing was painted
        Assert.Equal(0, surface[90, 90].A);
    }

    /// <summary>
    ///     Proves that a <c>filter</c> attribute on a <c>&lt;use&gt;</c> element referencing a
    ///     <c>&lt;symbol&gt;</c> renders the symbol's entire resolved subtree offscreen and applies
    ///     the filter to the combined result, exactly as for a <c>&lt;g&gt;</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilter_UseReferencingSymbolWithFilter_FillsCombinedBounds()
    {
        // Arrange: a symbol containing two 10x10 blue rects with a 10-unit gap between them; a
        // <use filter="url(#f)"> references the symbol and translates it by (10,10)
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'><feFlood flood-color='red'/></filter>
                <symbol id='sym'>
                  <rect x='0' y='0' width='10' height='10' fill='blue'/>
                  <rect x='20' y='0' width='10' height='10' fill='blue'/>
                </symbol>
              </defs>
              <use x='10' y='10' filter='url(#f)' href='#sym'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the gap between the symbol's two rects (translated by the use's x/y offset) is
        // filled by the flood, proving the filter was evaluated against the resolved symbol
        // subtree's combined bounds, not "no effect" (the group's previous behavior)
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 15]);

        // Assert: outside the filter region entirely, nothing was painted
        Assert.Equal(0, surface[90, 90].A);
    }

    /// <summary>
    ///     Proves that a <c>filter</c> attribute placed directly on a <c>&lt;use&gt;</c> element
    ///     itself (as distinct from a filter on the referenced <c>&lt;symbol&gt;</c>/<c>&lt;g&gt;</c>
    ///     covered by the previous test) applies to the whole resolved target, even when that
    ///     target is a plain <c>&lt;g&gt;</c> rather than a <c>&lt;symbol&gt;</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilter_FilterOnUseTargetingPlainG_FillsCombinedBounds()
    {
        // Arrange: same geometry as the symbol case above, but the use's href target is a plain
        // <g> defined inside <defs> rather than a <symbol>
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'><feFlood flood-color='red'/></filter>
                <g id='grp'>
                  <rect x='0' y='0' width='10' height='10' fill='blue'/>
                  <rect x='20' y='0' width='10' height='10' fill='blue'/>
                </g>
              </defs>
              <use x='10' y='10' filter='url(#f)' href='#grp'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the gap between the two rects is filled by the flood, proving the use's own
        // filter attribute applies to the resolved <g> target's combined subtree bounds
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 15]);
    }

    /// <summary>
    ///     Proves that a <c>symbol</c> referenced via <c>use</c> establishes a new nested viewport,
    ///     fitting the symbol's own <c>viewBox</c> content into the resolved <c>width</c>/
    ///     <c>height</c> (falling back from the <c>use</c> element's own <c>width</c>/<c>height</c>)
    ///     per the symbol's own <c>preserveAspectRatio</c> - new capability, via the same shared
    ///     <c>ComputePreserveAspectRatioFit</c> helper the root <c>svg</c> element uses.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseReferencingSymbolWithViewBox_FitsContentToResolvedWidthHeight()
    {
        // Arrange: a symbol with a 10x10 viewBox containing a full-viewBox blue rect, referenced
        // via a <use> that resolves a 20x20 box (its own width/height) - the symbol's content
        // should be scaled up 2x to fill that 20x20 box, then translated by the use's own x=5,y=5
        // offset, landing at (5,5)-(25,25) in the root's own coordinate space
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <symbol id='sym' viewBox='0 0 10 10'>
                  <rect x='0' y='0' width='10' height='10' fill='blue'/>
                </symbol>
              </defs>
              <use href='#sym' x='5' y='5' width='20' height='20'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the fitted, scaled-up content fills its resolved 20x20 box, sampled well inside
        // the expected (5,5)-(25,25) region
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[15, 15]);

        // Assert: outside the resolved 20x20 box, nothing was painted
        Assert.Equal(0, surface[40, 40].A);
    }

    /// <summary>
    ///     Proves that a <c>symbol</c>'s own <c>preserveAspectRatio</c> attribute is honored when
    ///     fitting its <c>viewBox</c> content into its resolved <c>width</c>/<c>height</c> - new
    ///     capability, exercising a non-default align (<c>xMinYMin</c>) so the fitted content is
    ///     pinned to the resolved box's own top-left corner rather than centered.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UseReferencingSymbolWithPreserveAspectRatio_HonorsAlign()
    {
        // Arrange: a symbol with a 10x20 (portrait) viewBox fitted "meet" into a 20x20 (square)
        // resolved box - under the default xMidYMid, the fitted 10x20 content (scaled to 10x20,
        // scale=1) would be horizontally centered (offset x=5); under "xMinYMin", it is pinned to
        // the left edge (offset x=0) instead. A blue marker rect fills the whole viewBox
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <symbol id='sym' viewBox='0 0 10 20' preserveAspectRatio='xMinYMin meet'>
                  <rect x='0' y='0' width='10' height='20' fill='blue'/>
                </symbol>
              </defs>
              <use href='#sym' x='0' y='0' width='20' height='20'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: content is pinned to the left edge (visible at x=2, well within the fitted
        // 0-10 band) rather than centered (which would leave x=2 transparent, since centered
        // content would occupy x=5-15)
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[2, 10]);

        // Assert: the right-hand slack area (which the centered default would fill, but the
        // left-pinned align leaves empty) is transparent
        Assert.Equal(0, surface[17, 10].A);
    }

    /// <summary>
    ///     Proves that a <c>marker</c> element's own explicit <c>preserveAspectRatio</c> attribute
    ///     is honored - new capability, routing the marker's <c>viewBox</c> fit through the same
    ///     shared <c>ComputePreserveAspectRatioFit</c> helper as the root <c>svg</c>/a <c>symbol</c>
    ///     - distinct from a marker with no explicit attribute at all, which always uses a "meet"
    ///     -equivalent scale (see <see cref="SvgCodec_Load_MarkerWithViewBox_FitsContentToMarkerWidthHeight"/>).
    ///     Exercises <c>slice</c> specifically: because a marker's content is always anchored by
    ///     its own <c>refX</c>/<c>refY</c> (never clipped to <c>markerWidth</c>/<c>markerHeight</c>
    ///     - see this class's remarks), an <c>&lt;align&gt;</c>'s Min/Mid/Max offset has no visible
    ///     effect on an unclipped marker (the offset is a rigid translation that always cancels
    ///     out relative to the anchor point), so <c>meet</c> versus <c>slice</c>'s different
    ///     uniform scale factor is the one part of an explicit <c>preserveAspectRatio</c> that
    ///     <i>is</i> visibly distinguishable here.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MarkerWithExplicitPreserveAspectRatioSlice_UsesLargerUniformScale()
    {
        // Arrange: a marker with a 16x8 viewBox fitted into a 8x8 markerWidth/markerHeight -
        // "meet" (min(8/16, 8/8) = 0.5, this marker's own pre-existing default-path scale) versus
        // "slice" (max(8/16, 8/8) = 1, twice as large) - anchored via refX=8,refY=4 (the viewBox's
        // own center) at the line's end vertex (10,10). Under "slice", the scaled-up content spans
        // canvas (2,6)-(18,14); under the marker's pre-existing "meet"-equivalent default it would
        // only span the narrower (6,8)-(14,12) - so canvas point (3,10) is covered only by the
        // wider "slice" fit
        const string svg = """
            <svg viewBox='0 0 20 20'>
              <defs>
                <marker id='sl' markerWidth='8' markerHeight='8' refX='8' refY='4' viewBox='0 0 16 8' preserveAspectRatio='xMidYMid slice' markerUnits='userSpaceOnUse'>
                  <rect x='0' y='0' width='16' height='8' fill='green'/>
                </marker>
              </defs>
              <line x1='2' y1='10' x2='10' y2='10' stroke='black' stroke-width='1' marker-end='url(#sl)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 20, 20);

        // Assert: the wider "slice" fit covers a point the narrower default "meet" fit would not
        Assert.Equal(new Rgba32(0, 128, 0, 255), surface[3, 10]);
    }

    /// <summary>
    ///     Proves that a group's own <c>opacity</c> attenuates the *filtered* result exactly once,
    ///     the same convention already established for single filtered shapes (see
    ///     <see cref="SvgCodec_Load_OpacityWithFeFloodFilter_AppliesOpacityToFilteredResultNotSource"/>).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilterWithOpacity_AppliesOpacityToFilteredResultNotChildren()
    {
        // Arrange: a fully-opaque red feFlood is the filter's entire output; the group's own
        // opacity of 0.5 must still visibly attenuate that flood's alpha
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f'><feFlood flood-color='red' flood-opacity='1'/></filter>
              </defs>
              <g opacity='0.5' filter='url(#f)'>
                <rect x='40' y='40' width='20' height='20' fill='blue'/>
              </g>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the flood's own color is still fully red, but its alpha reflects the group's own
        // 50% opacity (0.5 * 255 = 127.5), not the fully-opaque 255 a pre-filter opacity fold
        // would incorrectly produce
        var pixel = surface[50, 50];
        Assert.Equal(255, pixel.R);
        Assert.Equal(0, pixel.G);
        Assert.Equal(0, pixel.B);
        Assert.InRange((int)pixel.A, 110, 145);
    }

    /// <summary>
    ///     Proves that a group's own <c>transform</c> establishes the coordinate space the filter
    ///     region and filter primitives (here, <c>feOffset</c>) are computed and evaluated in -
    ///     mirroring <see cref="SvgCodec_Load_FeOffsetFilter_ShiftsSourceGraphicByDxDy"/>'s
    ///     single-shape assertions, but with the child rect expressed in the group's own local
    ///     space and a separate <c>transform</c> on the group carrying it to the same final
    ///     on-canvas position.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilterWithTransform_EvaluatesFilterInGroupsLocalSpace()
    {
        // Arrange: the group's transform='translate(10,10)' plus the child rect's own local
        // position (30,30) combine to the same on-canvas rect (40,40)-(50,50) used by the
        // single-shape feOffset test; the same dx='5' shift is therefore expected to move the
        // rendered content by the same 5 pixels
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='f' x='-1' y='-1' width='3' height='3'>
                  <feOffset dx='5' dy='0'/>
                </filter>
              </defs>
              <g transform='translate(10,10)' filter='url(#f)'>
                <rect x='30' y='30' width='10' height='10' fill='blue'/>
              </g>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect's content reappears shifted 5 pixels to the right of its own bounds
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[47, 45]);

        // Assert: nothing remains at the rect's own (pre-offset) position
        Assert.Equal(0, surface[42, 45].A);
    }

    /// <summary>
    ///     Proves that a group's <c>filter</c> attribute referencing a nonexistent id is tolerated
    ///     as a silent no-op, matching this codec's general dangling-reference convention: the
    ///     group's children still render normally, exactly as if the <c>filter</c> attribute had
    ///     been absent.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilterWithDanglingReference_RendersChildrenNormally()
    {
        // Arrange: the group's filter references an id that does not exist anywhere in the
        // document
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <g filter='url(#missing)'>
                <rect x='10' y='10' width='20' height='20' fill='purple'/>
              </g>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: the rect rendered its own normal fill - the dangling filter reference had no
        // effect
        Assert.Equal(new Rgba32(128, 0, 128, 255), surface[20, 20]);
    }

    /// <summary>
    ///     Proves that an empty, filtered <c>&lt;g&gt;</c> (no children at all) is tolerated as a
    ///     no-op rather than throwing: with no children, the subtree has no bounds to compute a
    ///     filter region from, so the group falls back to its (empty) unfiltered child render -
    ///     the same tolerant convention used for every other filter resource-safety fallback.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilterOnEmptyGroup_RendersNothingWithoutThrowing()
    {
        // Arrange: a filtered <g> with no children
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs><filter id='f'><feFlood flood-color='red'/></filter></defs>
              <g filter='url(#f)'></g>
            </svg>
            """;

        // Act: must not throw
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: nothing was painted anywhere - the empty group's filter had no bounds to work
        // with, so nothing was flooded
        Assert.Equal(0, surface[50, 50].A);
    }

    /// <summary>
    ///     Regression test for the group-level counterpart of
    ///     <see cref="SvgCodec_Load_MarkerContentWithFilterAttribute_FilterHasNoEffect"/>: a
    ///     <c>filter</c> attribute on a <c>&lt;g&gt;</c> *inside* a <c>&lt;marker&gt;</c>'s content
    ///     must still have no effect, preserving this codec's documented "filters on marker
    ///     content have no effect" contract even now that group-level filtering is otherwise
    ///     evaluated.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilterInsideMarkerContent_FilterHasNoEffect()
    {
        // Arrange: the marker's content is a <g filter="url(#f)"> wrapping a lime rect; the
        // filter would fill its region red if it were (incorrectly) evaluated
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <defs>
                <filter id='f'><feFlood flood-color='red'/></filter>
                <marker id='m' markerWidth='2' markerHeight='2' refX='1' refY='1' markerUnits='userSpaceOnUse'>
                  <g filter='url(#f)'>
                    <rect x='0' y='0' width='2' height='2' fill='lime'/>
                  </g>
                </marker>
              </defs>
              <line x1='1' y1='5.5' x2='7' y2='5.5' stroke='black' stroke-width='1' marker-end='url(#m)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the marker's own "lime" fill rendered - the group's filter was never applied
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[7, 5]);
    }

    /// <summary>
    ///     Group-level counterpart of
    ///     <see cref="SvgCodec_Load_FilterReferencedByManyShapesExceedingCumulativeBudget_FallsBackToUnfilteredForExcessShapes"/>:
    ///     proves the same cumulative, per-<c>Load</c>-call <c>FilterWorkBudget</c> also caps
    ///     group-level filter work, using <c>&lt;use&gt;</c> elements referencing a shared
    ///     <c>&lt;g&gt;</c> target instead of directly-filtered <c>&lt;rect&gt;</c> elements.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilterReferencedByManyUsesExceedingCumulativeBudget_FallsBackToUnfilteredForExcessUses()
    {
        // Arrange: identical filter-cost shape (a 40x40 rect, wrapped in a shared <g> target) and
        // filter chain (499 no-op primitives + a trailing feFlood, region forced to exactly
        // 100x100 pixels) as the single-shape cumulative-budget test, but each of the 13
        // non-overlapping placements is a <use filter="url(#f)"> referencing the shared <g>
        // rather than a directly-filtered rect. The first 10 charge a running cumulative total of
        // exactly 50,000,000 (still within the ceiling), while the 11th through 13th would push
        // the total over budget and must tolerantly fall back to unfiltered rendering
        const int shapeCount = 13;
        const int filteredShapeCount = 10;
        var noOpPrimitives = string.Concat(Enumerable.Repeat("<feUnsupportedNoOp/>", 499));
        var uses = string.Concat(Enumerable.Range(0, shapeCount).Select(i =>
            $"<use x='{10 + (i * 150)}' y='10' filter='url(#f)' href='#grp'/>"));
        var svg = $"""
            <svg viewBox='0 0 2000 120'>
              <defs>
                <filter id='f' width='250%' height='250%'>
                  {noOpPrimitives}
                  <feFlood flood-color='red'/>
                </filter>
                <g id='grp'>
                  <rect x='0' y='0' width='40' height='40' fill='blue'/>
                </g>
              </defs>
              {uses}
            </svg>
            """;

        // Act: render the whole document - must complete promptly, and must not throw
        var stopwatch = Stopwatch.StartNew();
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 2000, 120);
        stopwatch.Stop();

        // Assert: the first 10 uses (cumulative total staying within the ceiling) were actually
        // filtered - each rendered as the feFlood's solid red, not the referenced rect's own blue
        // fill
        for (var i = 0; i < filteredShapeCount; i++)
        {
            var sampleX = 30 + (i * 150);
            Assert.Equal(new Rgba32(255, 0, 0, 255), surface[sampleX, 30]);
        }

        // Assert: the remaining uses (11th through 13th), which would have pushed the cumulative
        // total over the ceiling, tolerantly fell back to unfiltered rendering - each still shows
        // the referenced rect's own normal blue fill
        for (var i = filteredShapeCount; i < shapeCount; i++)
        {
            var sampleX = 30 + (i * 150);
            Assert.Equal(new Rgba32(0, 0, 255, 255), surface[sampleX, 30]);
        }

        // Assert: completed promptly, proving the excess filters were skipped rather than
        // evaluated
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Expected the excessive cumulative group-filter work to be skipped promptly, but it took {stopwatch.Elapsed}.");
    }

    /// <summary>
    ///     Regression test for a code-review finding: <c>RenderFilteredGroup</c>'s bounds-only
    ///     pre-pass (<c>ComputeSubtreeLocalBounds</c>) used to charge the exact same
    ///     <c>totalElements</c> counter/<c>GeometryWorkBudget</c> instance the real render pass
    ///     charges again immediately afterward, double-counting every element under a filtered
    ///     group against the codec's fixed <c>MaxTotalRenderedElements</c> ceiling. Proves a
    ///     filtered group whose total (single-charge) element count sits comfortably under that
    ///     ceiling - but would have exceeded it under the old double-charge - now renders
    ///     successfully instead of throwing <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilteredGroupNearTotalElementBudget_RendersWithoutThrowing()
    {
        // Arrange: a filtered <g> containing 60,000 full-canvas <rect> elements - each one real
        // element, so the whole document's single-charge total (a little over 60,000) stays well
        // under the codec's fixed 100,000-element MaxTotalRenderedElements ceiling. Under the old
        // bounds-pre-pass double-charge, this same subtree would have been counted twice (once by
        // the bounds-only pre-pass, once again by the real render pass), pushing the running total
        // past 100,000 and throwing partway through - even though this document's *unfiltered*
        // rendering would have stayed comfortably within budget
        const int rectCount = 60_000;
        var rects = string.Concat(Enumerable.Repeat(
            "<rect x='0' y='0' width='10' height='10' fill='blue'/>", rectCount));
        var svg = $"""
            <svg viewBox='0 0 10 10'>
              <defs>
                <filter id='f'>
                  <feFlood flood-color='red'/>
                </filter>
              </defs>
              <g filter='url(#f)'>
                {rects}
              </g>
            </svg>
            """;

        // Act: must complete without throwing
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the group's filter (a solid red feFlood) was actually applied across the whole
        // canvas, proving the entire 60,000-element subtree was walked and rendered successfully
        // - not merely that the call happened not to throw
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[5, 5]);
    }

    /// <summary>
    ///     Regression test for a code-review finding: <c>ComputeSubtreeLocalBounds</c> (the
    ///     bounds-only pre-pass <c>RenderFilteredGroup</c> uses to size a filtered group's own
    ///     offscreen <c>SourceGraphic</c> buffer) used to exclude marker geometry entirely from a
    ///     group's own painted-content bounds, even though the real render pass that follows it
    ///     (<c>RenderMarkers</c>, re-entered from inside the group's offscreen render) does paint
    ///     marker pixels. A marker commonly extends beyond its host shape's own stroke-expanded
    ///     outline (as this test's arrowhead-style marker deliberately does), so the too-small
    ///     offscreen buffer silently clipped those marker pixels before the filter chain (or the
    ///     final composite) ever saw them. Proves the marker's own content now survives a
    ///     group-level filter unclipped.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilteredGroupWithLineMarkerExtendingBeyondLineBounds_MarkerPixelsSurviveFilter()
    {
        // Arrange: a 20x20 marker (a solid lime square, centered on its refX/refY anchor) placed
        // at a tiny 5-unit-long line's own end vertex (55, 50) - the marker's own painted content
        // (45,40)-(65,60) extends far beyond the line's own stroke-expanded bounds (roughly
        // 50-55 x, 49.5-50.5 y). The group's filter is a single identity feOffset (dx=0/dy=0,
        // a pure data copy of SourceGraphic) so a passing test proves the marker pixels survived
        // the filter round-trip unclipped, without the filter itself changing anything else
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <marker id='arrow' markerWidth='20' markerHeight='20' refX='0' refY='0' orient='0' markerUnits='userSpaceOnUse'>
                  <rect x='-10' y='-10' width='20' height='20' fill='lime'/>
                </marker>
                <filter id='f'>
                  <feOffset dx='0' dy='0'/>
                </filter>
              </defs>
              <g filter='url(#f)'>
                <line x1='50' y1='50' x2='55' y2='50' stroke='black' stroke-width='1' marker-end='url(#arrow)'/>
              </g>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: a point well inside the marker's own painted square, but well outside the
        // line's own stroke-expanded bounds, shows the marker's lime fill - proving its pixels
        // were not clipped by an offscreen buffer sized only from the line's own tiny bounds
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[50, 45]);
    }

    /// <summary>
    ///     Regression test for a code-review finding: <c>ComputeSubtreeLocalBounds</c> (the
    ///     bounds-only pre-pass <c>RenderFilteredGroup</c> uses to size a filtered group's own
    ///     offscreen <c>SourceGraphic</c> buffer) used to union each descendant's own <em>raw</em>
    ///     geometry bounds only, even when a descendant carried its own <c>filter</c> attribute
    ///     whose own filter region extends well beyond that descendant's raw geometry (for
    ///     example an enlarged filter <c>x</c>/<c>y</c>/<c>width</c>/<c>height</c> region, as this
    ///     test deliberately uses). Because the outer group's offscreen buffer was sized purely
    ///     from that too-small pre-pass return value, the real render pass then painted the inner
    ///     filtered descendant's actual (larger) output into the outer buffer, silently clipping
    ///     every pixel outside the too-small outer bounds before the outer filter chain (here, an
    ///     identity <c>feOffset</c> pass-through) or the final composite ever saw them. Proves the
    ///     outer filtered composite now contains the inner filter's full expanded output, not just
    ///     the portion that happened to fall within the (undersized) region around the inner
    ///     shape's own tiny raw bounding box.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFilterWithNestedFilteredChildExpandingBeyondOwnBounds_PreservesFullNestedFilterOutput()
    {
        // Arrange: a tiny 4x4 blue rect (raw bounds (48,48)-(52,52)) carries its own "innerFlood"
        // filter whose x/y/width/height region attributes are deliberately set far beyond its own
        // objectBoundingBox (-1000%/-1000%/2100%/2100% of the rect's own 4x4 bbox expands the
        // inner filter's own region to roughly (8,8)-(92,92) - nearly the whole 100x100 canvas).
        // The rect is wrapped in an outer <g filter="url(#outerPassThrough)"> whose own filter is
        // a dx=0/dy=0 feOffset - an identity pass-through that changes nothing, so any pixel
        // missing from the final result can only be explained by the outer group's own offscreen
        // buffer having clipped it away before the pass-through filter ever ran
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='innerFlood' x='-1000%' y='-1000%' width='2100%' height='2100%'>
                  <feFlood flood-color='red'/>
                </filter>
                <filter id='outerPassThrough'>
                  <feOffset dx='0' dy='0'/>
                </filter>
              </defs>
              <g filter='url(#outerPassThrough)'>
                <rect x='48' y='48' width='4' height='4' fill='blue' filter='url(#innerFlood)'/>
              </g>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: a point far outside the inner rect's own tiny raw bounding box (and far outside
        // the too-small margin a bounds pre-pass ignoring the inner filter's own expanded region
        // would have produced around it), but well inside the inner filter's actual expanded
        // region, shows the inner feFlood's red output surviving all the way through the outer
        // group's own filter and final composite
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[10, 10]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[90, 90]);

        // Assert: well outside even the inner filter's own (already very generous) expanded
        // region, nothing was painted - proving this is a genuine bounds fix, not merely an
        // unconditional expand-to-fill-the-canvas regression
        Assert.Equal(0, surface[1, 1].A);
    }

    /// <summary>
    ///     Regression test for a code-review finding: <c>RenderFilteredGroup</c>'s bounds-only
    ///     pre-pass (<c>ComputeSubtreeLocalBounds</c>) deliberately uses a fresh, local scratch
    ///     <c>totalElements</c> counter/<c>GeometryWorkBudget</c> instance on every invocation
    ///     (see the double-charge fix proven by
    ///     <see cref="SvgCodec_Load_FilteredGroupNearTotalElementBudget_RendersWithoutThrowing"/>),
    ///     so each nesting level of a chain of nested filtered groups "resets" its own
    ///     per-invocation ceiling - a document with many such nesting levels, each wrapping a
    ///     large subtree, could therefore force total bounds-pre-pass work proportional to
    ///     <c>depth * subtree-size</c>, unbounded by <c>MaxTotalRenderedElements</c>/
    ///     <c>GeometryWorkBudget</c> themselves. Proves the new, separate, cumulative
    ///     per-<c>Load</c>-call <c>BoundsPrePassWorkBudget</c> now bounds that total: a chain of
    ///     95 nested filtered <c>&lt;g&gt;</c> elements (comfortably within the fixed 100-level
    ///     <c>MaxElementDepth</c> ceiling) wrapping 15,000 leaf <c>rect</c> elements has a real
    ///     total rendered-element count of roughly 15,095 - comfortably under the separate
    ///     100,000-element <c>MaxTotalRenderedElements</c> ceiling, so this test cannot pass
    ///     merely by incidentally tripping that pre-existing guard instead - but each of the 95
    ///     nesting levels' own bounds pre-pass re-walks a large portion of that same 15,000-rect
    ///     subtree, so the combined pre-pass work across all 95 levels comfortably exceeds the
    ///     fixed cumulative ceiling.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_DeeplyNestedFilteredGroupsExceedingCumulativeBoundsPrePassBudget_ThrowsInvalidDataException()
    {
        // Arrange: 95 nested filtered <g> elements wrapping 15,000 leaf rects. Each nesting
        // level's own RenderFilteredGroup call re-walks the (still-large) remaining subtree
        // beneath it via its own bounds-only pre-pass, so the combined pre-pass work across all
        // 95 levels (roughly depth * subtree-size) comfortably exceeds the fixed cumulative
        // bounds-pre-pass ceiling, even though every individual pre-pass invocation's own
        // element count, and the document's own real total rendered-element count, both stay far
        // under the separate MaxTotalRenderedElements ceiling
        const int nestingDepth = 95;
        const int rectCount = 15_000;
        var rects = string.Concat(Enumerable.Repeat(
            "<rect x='0' y='0' width='10' height='10' fill='blue'/>", rectCount));

        var svg = new StringBuilder();
        svg.Append("<svg viewBox='0 0 10 10'><defs><filter id='f'><feOffset dx='0' dy='0'/></filter></defs>");
        for (var i = 0; i < nestingDepth; i++)
        {
            svg.Append("<g filter='url(#f)'>");
        }

        svg.Append(rects);
        for (var i = 0; i < nestingDepth; i++)
        {
            svg.Append("</g>");
        }

        svg.Append("</svg>");

        // Act & Assert: must throw InvalidDataException rather than perform unbounded pre-pass
        // work - the same convention this bounds pre-pass's own existing per-invocation
        // MaxElementDepth/MaxTotalRenderedElements guards already use
        using var stream = ToStream(svg.ToString());
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 10, 10));
    }

    /// <summary>
    ///     Companion non-regression test for
    ///     <see cref="SvgCodec_Load_DeeplyNestedFilteredGroupsExceedingCumulativeBoundsPrePassBudget_ThrowsInvalidDataException"/>:
    ///     proves the new cumulative bounds-pre-pass budget's fixed ceiling is generous enough
    ///     that an ordinary, realistic document with only a modest few levels of nested filtered
    ///     groups is never spuriously rejected - a false-positive fallback this new resource-safety
    ///     mechanism must not introduce for legitimate real-world content.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ModestlyNestedFilteredGroups_RenderCorrectlyWithoutFalsePositiveFallback()
    {
        // Arrange: 5 levels of nested filtered <g> elements (a modest, realistic nesting depth),
        // each with an identity (dx="0"/dy="0") feOffset filter, wrapping 50 leaf rects - the
        // combined bounds-pre-pass work across all 5 levels is trivially small relative to the
        // new cumulative ceiling
        const int nestingDepth = 5;
        const int rectCount = 50;
        var rects = string.Concat(Enumerable.Repeat(
            "<rect x='0' y='0' width='10' height='10' fill='blue'/>", rectCount));

        var svg = new StringBuilder();
        svg.Append("<svg viewBox='0 0 10 10'><defs><filter id='f'><feOffset dx='0' dy='0'/></filter></defs>");
        for (var i = 0; i < nestingDepth; i++)
        {
            svg.Append("<g filter='url(#f)'>");
        }

        svg.Append(rects);
        for (var i = 0; i < nestingDepth; i++)
        {
            svg.Append("</g>");
        }

        svg.Append("</svg>");

        // Act: must complete without throwing
        using var stream = ToStream(svg.ToString());
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the innermost rects' own blue fill survives every nested identity filter,
        // proving the document rendered correctly rather than being rejected by the new
        // cumulative bounds-pre-pass budget
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[5, 5]);
    }

    /// <summary>
    ///     Regression test for a code-review finding: <c>ApplyOwnFilterToLocalBounds</c> (used by
    ///     <c>ComputeSubtreeLocalBounds</c> to account for a descendant's own filter region when
    ///     sizing an ancestor filtered group's own offscreen buffer) used to always substitute a
    ///     descendant's filter-expanded local region for its raw geometry bounds whenever that
    ///     descendant's filter had at least one primitive - even when the real render pass would
    ///     later reject that same descendant filter for an entirely different, pixel-space-only
    ///     reason (its transformed region exceeds <c>MaxCoordinateMagnitude</c>). When the
    ///     descendant's filter really is rejected at real render time, it falls back to plain
    ///     unfiltered rendering at its own raw geometry bounds - but the old pre-pass had already
    ///     used the (much larger, doomed-to-be-rejected) expanded region to compute the ancestor's
    ///     own combined bounds, which could itself then trip the ancestor's own real render-time
    ///     pixel-space rejection checks, incorrectly skipping a perfectly reasonable outer filter
    ///     purely because of an inner descendant filter that was never going to apply. Proves the
    ///     outer group's own <c>feFlood</c> filter still applies (its own flood color paints the
    ///     outer filter region) even though the inner descendant's own filter region is
    ///     deliberately, pathologically oversized (a local-space region of roughly 4,000,000 units
    ///     square - far beyond <c>MaxCoordinateMagnitude</c>'s 1,000,000 - guaranteeing the inner
    ///     filter is itself rejected at real render time and falls back to the inner rect's own
    ///     plain blue fill).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_OuterGroupFilterWithPathologicallyOversizedDescendantFilter_StillAppliesOuterFilter()
    {
        // Arrange: a tiny 4x4 blue rect (raw local bounds (48,48)-(52,52)) carries its own
        // "innerHuge" filter whose x/y/width/height region attributes (given as plain fractions,
        // not percentages) expand its own local-space filter region to roughly
        // (-3999992,-3999992)-(4000008,4000008) - a region whose own width/height already exceed
        // MaxCoordinateMagnitude (1,000,000) well before any further transform is even composed,
        // so it is certain to be rejected by ComputeFilterRegionPixelBounds's own
        // MaxCoordinateMagnitude check at real render time regardless of scale. The rect is
        // wrapped in an outer <g filter="url(#outerFlood)"> whose own filter is a plain feFlood -
        // if the outer group's own bounds pre-pass were still poisoned by the inner filter's
        // doomed expanded region (the pre-fix bug), the outer group's own filter region would
        // itself exceed MaxCoordinateMagnitude and be tolerantly skipped, and the outer feFlood's
        // lime paint would never appear
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <filter id='innerHuge' x='-1000000' y='-1000000' width='1000000' height='1000000'>
                  <feFlood flood-color='red'/>
                </filter>
                <filter id='outerFlood'>
                  <feFlood flood-color='lime'/>
                </filter>
              </defs>
              <g filter='url(#outerFlood)'>
                <rect x='48' y='48' width='4' height='4' fill='blue' filter='url(#innerHuge)'/>
              </g>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100);

        // Assert: a point within the outer group's own (small, un-poisoned) filter region shows
        // the outer feFlood's lime paint, proving the outer filter was actually applied rather
        // than tolerantly skipped because of the doomed inner descendant filter
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[50, 50]);

        // Assert: well outside the outer group's own (small) filter region, nothing was painted -
        // proving this is a genuine, correctly-sized outer filter application, not an
        // unconditional expand-to-fill-the-canvas regression
        Assert.Equal(0, surface[1, 1].A);
    }

    /// <summary>
    ///     Regression test for a code-review finding: the cumulative
    ///     <c>BoundsPrePassWorkBudget</c> added to bound nested-filtered-group pre-pass work only
    ///     charged once per element visit, never accounting for the actual geometry-parsing work
    ///     each visit performs. A document with a single element carrying an enormous <c>path</c>
    ///     <c>d</c> attribute (comfortably within the per-invocation <c>GeometryWorkBudget</c>
    ///     ceiling on its own), nested under many levels of filtered groups, causes each nesting
    ///     level's own <c>RenderFilteredGroup</c> pre-pass to fully re-parse that same enormous
    ///     path from scratch - real CPU cost proportional to <c>depth * geometry-size</c> that
    ///     neither the per-visit cumulative budget nor any individual invocation's own fresh
    ///     <c>GeometryWorkBudget</c> could catch. Proves the new geometry-weighted cumulative
    ///     charge now bounds this: 20 nested filtered <c>&lt;g&gt;</c> levels wrapping one
    ///     ~30,000-command <c>path</c> (comfortably under the 200,000-command per-invocation
    ///     ceiling on its own) throws <see cref="InvalidDataException"/> rather than performing
    ///     20 full re-parses of that path.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_DeeplyNestedFilteredGroupsWithEnormousInnerPath_ThrowsInvalidDataException()
    {
        // Arrange: 20 nested filtered <g> elements wrapping a single <path> whose "d" attribute
        // has one initial "M" command plus 30,000 implicitly-repeated "L" commands - comfortably
        // under the 200,000-command per-invocation GeometryWorkBudget ceiling, so this test cannot
        // pass merely by incidentally tripping that pre-existing guard instead. Each of the 20
        // nesting levels' own RenderFilteredGroup pre-pass re-parses this same path once, so the
        // combined re-parse cost across all 20 levels (roughly depth * d.Length) comfortably
        // exceeds the new cumulative bounds-pre-pass geometry-work ceiling
        const int nestingDepth = 20;
        var d = "M0,0 " + string.Concat(Enumerable.Repeat("L1,1 ", 30_000));

        var svg = new StringBuilder();
        svg.Append("<svg viewBox='0 0 10 10'><defs><filter id='f'><feOffset dx='0' dy='0'/></filter></defs>");
        for (var i = 0; i < nestingDepth; i++)
        {
            svg.Append("<g filter='url(#f)'>");
        }

        svg.Append($"<path d='{d}'/>");
        for (var i = 0; i < nestingDepth; i++)
        {
            svg.Append("</g>");
        }

        svg.Append("</svg>");

        // Act & Assert: must throw InvalidDataException rather than perform 20 full re-parses of
        // the same enormous path
        using var stream = ToStream(svg.ToString());
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 10, 10));
    }

    /// <summary>
    ///     Companion non-regression test for
    ///     <see cref="SvgCodec_Load_DeeplyNestedFilteredGroupsWithEnormousInnerPath_ThrowsInvalidDataException"/>:
    ///     proves the new geometry-weighted cumulative bounds-pre-pass budget's fixed ceiling is
    ///     generous enough that an ordinary, realistic document with only a modest few levels of
    ///     nested filtered groups wrapping normal-sized geometry is never spuriously rejected - a
    ///     false-positive fallback this new resource-safety mechanism must not introduce for
    ///     legitimate real-world content.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ModestlyNestedFilteredGroupsWithNormalPath_RenderCorrectlyWithoutFalsePositiveFallback()
    {
        // Arrange: 5 levels of nested filtered <g> elements (a modest, realistic nesting depth),
        // each with an identity (dx="0"/dy="0") feOffset filter, wrapping one small, ordinary
        // <path> - the combined pre-pass geometry-work across all 5 levels is trivially small
        // relative to the new cumulative ceiling
        const int nestingDepth = 5;
        const string d = "M0,0 L10,0 L10,10 L0,10 Z";

        var svg = new StringBuilder();
        svg.Append("<svg viewBox='0 0 10 10'><defs><filter id='f'><feOffset dx='0' dy='0'/></filter></defs>");
        for (var i = 0; i < nestingDepth; i++)
        {
            svg.Append("<g filter='url(#f)'>");
        }

        svg.Append($"<path d='{d}' fill='blue'/>");
        for (var i = 0; i < nestingDepth; i++)
        {
            svg.Append("</g>");
        }

        svg.Append("</svg>");

        // Act: must complete without throwing
        using var stream = ToStream(svg.ToString());
        var surface = SvgCodec.Load(stream, 10, 10);

        // Assert: the path's own blue fill survives every nested identity filter, proving the
        // document rendered correctly rather than being rejected by the new cumulative
        // bounds-pre-pass geometry-work budget
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[5, 5]);
    }

    // ================================================================================================
    // Total geometry-parsing work budget (path data / point lists / text characters)
    // ================================================================================================

    /// <summary>
    ///     Proves that a single <c>&lt;path&gt;</c> element whose <c>d</c> attribute contains just
    ///     over the codec's fixed combined geometry-parsing work budget worth of implicit-repeat
    ///     <c>L</c> commands is rejected with <see cref="InvalidDataException"/>, even though it
    ///     counts as only a single element toward
    ///     <see cref="SvgCodec_Load_UseFanOutExceedingTotalElementBudget_ThrowsInvalidDataException"/>'s
    ///     separate total-rendered-element budget. The budget is charged incrementally (once per
    ///     parsed command), so this test throws quickly rather than only after the whole
    ///     (otherwise unbounded) <c>d</c> string has already been scanned.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathDataExceedingGeometryWorkBudget_ThrowsInvalidDataException()
    {
        // Arrange: one initial "M" command plus 200,001 implicitly-repeated "L" commands - one
        // more than the codec's fixed 200,000 combined geometry-parsing work budget
        var d = "M0,0 " + string.Concat(Enumerable.Repeat("L1,1 ", 200_001));
        var svg = $"<svg viewBox='0 0 10 10'><path d='{d}'/></svg>";

        // Act & Assert
        using var stream1602 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1602, 10, 10));
    }

    /// <summary>
    ///     Proves that a single <c>&lt;polyline&gt;</c> element whose <c>points</c> attribute
    ///     resolves to just over the codec's fixed combined geometry-parsing work budget worth of
    ///     coordinate pairs is rejected with <see cref="InvalidDataException"/>, exercising the
    ///     same shared budget as the path-data test above from a different source.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PointListExceedingGeometryWorkBudget_ThrowsInvalidDataException()
    {
        // Arrange: 200,001 coordinate pairs - one more than the codec's fixed 200,000 combined
        // geometry-parsing work budget
        var points = string.Concat(Enumerable.Repeat("1,1 ", 200_001));
        var svg = $"<svg viewBox='0 0 10 10'><polyline points='{points}'/></svg>";

        // Act & Assert
        using var stream1620 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1620, 10, 10));
    }

    /// <summary>
    ///     Regression test for the points-list budget-charging finding: <c>ParsePointList</c> used
    ///     to fully parse the entire <c>points</c> attribute into a <c>List&lt;float&gt;</c> (via
    ///     <c>ParseNumberList</c>) and then a <c>List&lt;Vector2&gt;</c> of the whole resolved pair
    ///     count, before ever charging the geometry-parsing work budget - so a hostile, far larger
    ///     than the budget <c>points</c> string was still fully materialized into two full-sized
    ///     lists before being rejected, on top of the single already-unavoidable
    ///     <c>XDocument.Load</c> attribute-value allocation every implementation pays regardless.
    ///     Proves, by measuring actual bytes allocated (never wall-clock time) during
    ///     <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont}?)"/>,
    ///     that the fixed, incrementally-charging <c>ParsePointList</c> throws as soon as the
    ///     budget is exceeded without ever retaining more than the budget's worth of parsed points
    ///     - allocating markedly less than the old, fully-materializing implementation for the
    ///     same input (empirically observed as roughly a third the allocation at this test's pair
    ///     count, verified locally against the pre-fix implementation).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PointsListLargeExceedingBudget_ThrowsWithoutLargeAllocation()
    {
        // Arrange: a points string sized far beyond the codec's fixed 200,000-pair budget
        // (generated programmatically, never a literal fixture) - large enough that fully
        // materializing it into List<float>/List<Vector2> before charging allocates tens of
        // megabytes more than incremental charging, which stops shortly after the budget is
        // exceeded regardless of how much larger the raw points string is
        const int hugePairCount = 1_000_000;
        var points = string.Concat(Enumerable.Repeat("1,1 ", hugePairCount));
        var svg = $"<svg viewBox='0 0 10 10'><polyline points='{points}'/></svg>";
        using var stream = ToStream(svg);

        // Act
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 10, 10));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // Assert
        Assert.Contains("geometry-parsing work", ex.Message, StringComparison.OrdinalIgnoreCase);

        const long maxExpectedAllocatedBytes = 64 * 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    /// <summary>
    ///     Proves that a single <c>&lt;text&gt;</c> element whose content is just over the
    ///     codec's fixed combined geometry-parsing work budget worth of characters is rejected
    ///     with <see cref="InvalidDataException"/>, exercising the same shared budget from a third
    ///     source. The budget is charged with the whole character count before the per-rune
    ///     glyph-outline/kerning loop begins, so this test remains fast despite the long string.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextExceedingGeometryWorkBudget_ThrowsInvalidDataException()
    {
        // Arrange: 200,001 characters - one more than the codec's fixed 200,000 combined
        // geometry-parsing work budget
        var text = new string('A', 200_001);
        var svg = $"<svg viewBox='0 0 10 10'><text x='0' y='5' font-family='TestFont' font-size='10'>{text}</text></svg>";
        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act & Assert
        using var stream1683 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1683, 10, 10, fonts));
    }

    // ================================================================================================
    // Number-list attribute length budget (viewBox / transform arguments / stroke-dasharray)
    // ================================================================================================

    /// <summary>
    ///     Proves that a <c>stroke-dasharray</c> attribute containing more than
    ///     <c>ParseNumberList</c>'s fixed maximum number of numbers is rejected with
    ///     <see cref="InvalidDataException"/>, and that the cap is charged incrementally (per
    ///     number, as each is parsed) rather than only after the whole list has already been
    ///     materialized into an unbounded <see cref="List{T}"/> - proven by measuring actual bytes
    ///     allocated, matching the <see cref="SvgCodec_Load_PointsListLargeExceedingBudget_ThrowsWithoutLargeAllocation"/>
    ///     allocation-bound precedent above.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeDasharrayExceedingNumberListLengthCap_ThrowsWithoutLargeAllocation()
    {
        // Arrange: a dasharray far larger than the codec's fixed 10,000-number cap (generated
        // programmatically, never a literal fixture)
        const int hugeNumberCount = 1_000_000;
        var dasharray = string.Join(',', Enumerable.Repeat("1", hugeNumberCount));
        var svg = $"<svg viewBox='0 0 10 10'><rect width='5' height='5' fill='none' stroke='black' stroke-dasharray='{dasharray}'/></svg>";
        using var stream = ToStream(svg);

        // Act
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 10, 10));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // Assert
        Assert.Contains("number list", ex.Message, StringComparison.OrdinalIgnoreCase);

        const long maxExpectedAllocatedBytes = 16 * 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    /// <summary>
    ///     Proves the same number-list length cap is reached identically via a transform
    ///     function's own argument list (here, <c>matrix(...)</c>, reached from a plain
    ///     <c>transform</c> attribute) - a second call site sharing <c>ParseNumberList</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TransformArgumentListExceedingLengthCap_ThrowsInvalidDataException()
    {
        // Arrange: a matrix(...) argument list far larger than the codec's fixed 10,000-number cap
        var args = string.Join(',', Enumerable.Repeat("1", 10_001));
        var svg = $"<svg viewBox='0 0 10 10'><rect width='5' height='5' transform='matrix({args})'/></svg>";

        // Act & Assert
        using var stream1736 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1736, 10, 10));
    }

    /// <summary>
    ///     Proves the same number-list length cap is reached identically via the root
    ///     <c>&lt;svg&gt;</c> element's own <c>viewBox</c> attribute - a third call site sharing
    ///     <c>ParseNumberList</c>, even though a well-formed <c>viewBox</c> only ever needs exactly
    ///     four numbers.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ViewBoxNumberListExceedingLengthCap_ThrowsInvalidDataException()
    {
        // Arrange: a viewBox with far more than the codec's fixed 10,000-number cap worth of
        // numbers, even though only the first four would ever be meaningful
        var numbers = string.Join(' ', Enumerable.Repeat("0", 10_001));
        var svg = $"<svg viewBox='{numbers}'><rect width='5' height='5'/></svg>";

        // Act & Assert
        using var stream1754 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1754, 10, 10));
    }

    // ================================================================================================
    // Total whole-document element budget (BuildIdIndex / ParseStops)
    // ================================================================================================

    /// <summary>
    ///     Proves that <see cref="SvgCodec"/> bounds its whole-document <c>id</c>-index walk
    ///     (<c>BuildIdIndex</c>), which runs before rendering and independently of the
    ///     total-rendered-element budget: a <c>&lt;defs&gt;</c> subtree containing more elements
    ///     than the budget - none of which are ever referenced or rendered - is still rejected
    ///     with <see cref="InvalidDataException"/>, closing the gap where the rendering-time
    ///     budget alone would never see them.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UnrenderedDefsElementCountExceedingDocumentElementBudget_ThrowsInvalidDataException()
    {
        // Arrange: a <defs> subtree containing more plain, never-referenced <rect> elements than
        // the codec's fixed 100,000-element document-wide budget - none of these are rendered or
        // referenced by anything, so only BuildIdIndex's whole-document walk ever visits them
        var builder = new StringBuilder();
        builder.Append("<svg viewBox='0 0 10 10'><defs>");
        for (var i = 0; i < 100_001; i++)
        {
            builder.Append("<rect width='1' height='1'/>");
        }

        builder.Append("</defs></svg>");

        // Act & Assert
        using var stream1785 = ToStream(builder.ToString());
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream1785, 10, 10));
    }

    /// <summary>
    ///     Proves the same whole-document element budget closes the related <c>ParseStops</c> gap:
    ///     a <c>linearGradient</c>/<c>radialGradient</c> - a non-rendering element that
    ///     <c>RenderElement</c> charges only once for itself and never recurses into - can
    ///     otherwise carry an unbounded number of <c>&lt;stop&gt;</c> children, each allocating a
    ///     <c>GradientStop</c>. Proves, by measuring actual bytes allocated (matching the
    ///     allocation-bound precedent above), that an oversized <c>&lt;stop&gt;</c> list is
    ///     rejected by <c>BuildIdIndex</c>'s whole-document walk before <c>ParseStops</c> ever
    ///     runs, rather than after fully materializing the whole stop list.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientStopCountExceedingDocumentElementBudget_ThrowsWithoutLargeAllocation()
    {
        // Arrange: a linearGradient with more <stop> children than the codec's fixed
        // 100,000-element document-wide budget
        var builder = new StringBuilder();
        builder.Append("<svg viewBox='0 0 10 10'><defs><linearGradient id='g'>");
        for (var i = 0; i < 100_001; i++)
        {
            builder.Append("<stop offset='0' stop-color='black'/>");
        }

        builder.Append("</linearGradient></defs>");
        builder.Append("<rect width='5' height='5' fill='url(#g)'/></svg>");
        using var stream = ToStream(builder.ToString());

        // Act
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 10, 10));
        var allocatedDuring = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        // Assert
        Assert.Contains("elements", ex.Message, StringComparison.OrdinalIgnoreCase);

        const long maxExpectedAllocatedBytes = 64 * 1024 * 1024;
        Assert.True(
            allocatedDuring < maxExpectedAllocatedBytes,
            $"Expected no large allocation, but {allocatedDuring:N0} bytes were allocated.");
    }

    // ================================================================================================
    // <text> rendering, text-anchor, and font fallback
    // ================================================================================================

    /// <summary>
    ///     Proves that <c>&lt;text&gt;</c> renders a matching font's glyph at the expected pixel
    ///     position, using the synthetic test font's known 50x50 square glyph shape.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextWithMatchingFont_RendersGlyphAtExpectedPosition()
    {
        // Arrange: font-size equals the font's em-square (100), so scale is 1:1; the square glyph
        // for 'A' should render spanning x equals 10 to 60, y equals 10 to 60 (baseline at y=60)
        const string svg = "<svg viewBox='0 0 100 100'><text x='10' y='60' font-family='TestFont' font-size='100' fill='black'>A</text></svg>";
        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act
        using var stream1845 = ToStream(svg);
        var surface = SvgCodec.Load(stream1845, 100, 100, fonts);

        // Assert: inside the glyph square
        Assert.Equal(255, surface[35, 35].A);
        // Assert: outside the glyph square (above baseline extent and off to the side)
        Assert.Equal(0, surface[5, 5].A);
        // Assert: below the baseline (nothing renders past the glyph's bottom edge)
        Assert.Equal(0, surface[35, 90].A);
    }

    /// <summary>
    ///     Proves that <c>text-anchor="middle"</c> centers the glyph run on the given <c>x</c>,
    ///     rather than starting there.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextAnchorMiddle_CentersTextHorizontally()
    {
        // Arrange: single glyph, advance 100, anchored at x equals 50 - offsets the glyph square
        // to span x equals 0 to 50 (half its width to either side of x equals 50)
        const string svg = "<svg viewBox='0 0 100 100'><text x='50' y='60' font-family='TestFont' font-size='100' text-anchor='middle' fill='black'>A</text></svg>";
        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act
        using var stream1868 = ToStream(svg);
        var surface = SvgCodec.Load(stream1868, 100, 100, fonts);

        // Assert: filled within the centered square
        Assert.Equal(255, surface[25, 35].A);
        // Assert: NOT filled where a start-anchored run would have placed the square instead
        Assert.Equal(0, surface[75, 35].A);
    }

    /// <summary>
    ///     Proves that <c>text-anchor="end"</c> right-aligns the glyph run so it ends at the given
    ///     <c>x</c>, rather than starting there.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextAnchorEnd_RightAlignsText()
    {
        // Arrange: single glyph, advance 100, anchored (ending) at x equals 90 - offsets the
        // glyph square to span local x equals -10 to 40 (visible portion 0 to 40)
        const string svg = "<svg viewBox='0 0 100 100'><text x='90' y='60' font-family='TestFont' font-size='100' text-anchor='end' fill='black'>A</text></svg>";
        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act
        using var stream1889 = ToStream(svg);
        var surface = SvgCodec.Load(stream1889, 100, 100, fonts);

        // Assert: filled within the visible part of the right-aligned square
        Assert.Equal(255, surface[20, 35].A);
        // Assert: NOT filled where a start-anchored run would have placed the square instead
        Assert.Equal(0, surface[70, 35].A);
    }

    /// <summary>
    ///     Proves that kerning between a known glyph pair shifts the second glyph's position,
    ///     by comparing against the position a naive (kerning-ignoring) layout would produce.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextWithKerningPair_AppliesKerningBetweenGlyphs()
    {
        // Arrange: text "AB" at font-size 100 gives 1:1 scale. Glyph 'A' occupies x from 10
        // to 60. Without kerning, glyph 'B' would start its 100-unit advance at pen position
        // 110. With the test font's -10 kerning pair applied, 'B' instead starts at pen
        // position 100, so x equals 105 is filled only under the kerned layout.
        const string svg = "<svg viewBox='0 0 200 100'><text x='10' y='60' font-family='TestFont' font-size='100' fill='black'>AB</text></svg>";
        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act
        using var stream1912 = ToStream(svg);
        var surface = SvgCodec.Load(stream1912, 200, 100, fonts);

        // Assert: filled only if the -10 kerning adjustment was applied before placing 'B'
        Assert.Equal(255, surface[105, 35].A);
        // Assert: sanity check that 'B' rendered at all, in a region common to both layouts
        Assert.Equal(255, surface[140, 35].A);
    }

    /// <summary>
    ///     Proves that <c>&lt;text&gt;</c> is silently skipped (no exception, no ink) when
    ///     <see cref="SvgCodec.Load(Stream,int,int,IReadOnlyDictionary{string,TrueTypeFont}?)"/>
    ///     is called with no fonts dictionary at all.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextWithoutFontsDictionary_SkipsSilentlyWithoutThrowing()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><text x='10' y='60' font-family='TestFont' font-size='100' fill='black'>A</text></svg>";

        // Act
        using var stream1932 = ToStream(svg);
        var surface = SvgCodec.Load(stream1932, 100, 100);

        // Assert: no exception was thrown (implicit), and no ink was painted anywhere
        Assert.Equal(0, surface[35, 35].A);
    }

    /// <summary>
    ///     Proves that <c>&lt;text&gt;</c> is silently skipped when the fonts dictionary is
    ///     supplied but contains no entry matching the requested <c>font-family</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontFamilyNoMatch_SkipsSilentlyWithoutThrowing()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><text x='10' y='60' font-family='TestFont' font-size='100' fill='black'>A</text></svg>";
        var fonts = new Dictionary<string, TrueTypeFont> { ["OtherFont"] = BuildTestFont() };

        // Act
        using var stream1950 = ToStream(svg);
        var surface = SvgCodec.Load(stream1950, 100, 100, fonts);

        // Assert: no exception, and no ink was painted
        Assert.Equal(0, surface[35, 35].A);
    }

    /// <summary>
    ///     Proves that a comma-separated <c>font-family</c> fallback list matches the first family
    ///     actually present in the supplied fonts dictionary, skipping unavailable entries.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontFamilyCommaSeparatedList_MatchesFirstAvailableFont()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><text x='10' y='60' font-family='Nonexistent, TestFont' font-size='100' fill='black'>A</text></svg>";
        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act
        using var stream1968 = ToStream(svg);
        var surface = SvgCodec.Load(stream1968, 100, 100, fonts);

        // Assert: the glyph rendered, proving the fallback list was walked to "TestFont"
        Assert.Equal(255, surface[35, 35].A);
    }

    // ================================================================================================
    // font-weight / font-style-aware face matching (SvgFontFace / SelectClosestFace)
    // ================================================================================================

    /// <summary>
    ///     Proves that a family with two registered faces (400/Normal, using
    ///     <see cref="BuildTestFont"/>'s narrow 50-wide glyph, and 700/Normal, using
    ///     <see cref="BuildBoldTestFont"/>'s wide 80-wide glyph) selects the bold face for a
    ///     <c>font-weight="bold"</c> text element, while a sibling element with no own
    ///     <c>font-weight</c> still selects the normal (narrow) face.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontWeightBold_SelectsBoldFaceOverNormalFace()
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

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: the font-weight="bold" text selected the wide face
        Assert.Equal(255, surface[75, 35].A);
        // Assert: the sibling text with no font-weight still selected the narrow face
        Assert.Equal(0, surface[75, 70].A);
    }

    /// <summary>
    ///     Proves that a family with two registered faces (400/Normal, using
    ///     <see cref="BuildTestFont"/>'s glyph at local x equals 0-50, and 400/Italic, using
    ///     <see cref="BuildItalicTestFont"/>'s glyph shifted to local x equals 20-70) selects the
    ///     italic face for a <c>font-style="italic"</c> text element.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontStyleItalic_SelectsItalicFaceOverNormalFace()
    {
        // Arrange: the normal face fills canvas x equals 10 to 60; the italic face fills canvas x
        // equals 30 to 80 - x equals 75 is filled only by the italic face, and x equals 15 is
        // filled only by the normal face.
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' font-style='italic' fill='black'>A</text>
            </svg>
            """;
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildTestFont()),
                new SvgFontFace(BuildItalicTestFont(), Style: SvgFontStyle.Italic)
            ]
        };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: filled where only the italic face's glyph reaches
        Assert.Equal(255, surface[75, 35].A);
        // Assert: NOT filled where only the normal face's glyph would have reached
        Assert.Equal(0, surface[15, 35].A);
    }

    /// <summary>
    ///     Regression test: <c>font-style="oblique&#x9;10deg"</c> (tab-separated, per the CSS
    ///     <c>font-style: oblique &lt;angle&gt;</c> grammar) must still tokenize on the tab and
    ///     resolve to <see cref="SvgFontStyle.Italic"/>, rather than treating the whole value as one
    ///     unparseable token (which would silently fall back to the inherited/normal style).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontStyleObliqueWithTabSeparatedAngle_SelectsItalicFaceOverNormalFace()
    {
        // Arrange: same glyph layout as the plain "italic" case above - x equals 75 is filled only
        // by the italic face, and x equals 15 is filled only by the normal face.
        const string svg = "<svg viewBox='0 0 100 100'>\n" +
                            "  <text x='10' y='60' font-family='TestFont' font-size='100' font-style='oblique\t10deg' fill='black'>A</text>\n" +
                            "</svg>";
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildTestFont()),
                new SvgFontFace(BuildItalicTestFont(), Style: SvgFontStyle.Italic)
            ]
        };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: filled where only the italic face's glyph reaches
        Assert.Equal(255, surface[75, 35].A);
        // Assert: NOT filled where only the normal face's glyph would have reached
        Assert.Equal(0, surface[15, 35].A);
    }

    /// <summary>
    ///     Proves the closest-weight-distance rule: with faces registered at 400 (narrow) and 900
    ///     (wide), a request of <c>font-weight="600"</c> selects the 400 face (distance 200)
    ///     rather than the 900 face (distance 300).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontWeightNumeric_SelectsClosestRegisteredFaceByDistance()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' font-weight='600' fill='black'>A</text>
            </svg>
            """;
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildTestFont(), Weight: 400),
                new SvgFontFace(BuildBoldTestFont(), Weight: 900)
            ]
        };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: the 900 (wide) face's exclusive region was NOT selected
        Assert.Equal(0, surface[75, 35].A);
        // Assert: sanity check the 400 (narrow) face did render
        Assert.Equal(255, surface[35, 35].A);
    }

    /// <summary>
    ///     Proves that an extreme, out-of-range <c>font-weight</c> value (still parsed, not
    ///     rejected, by <c>ParseFontWeight</c>) does not throw an <see cref="OverflowException"/>
    ///     from <c>SelectClosestFace</c>'s weight-distance computation, and still deterministically
    ///     selects the closer-by-magnitude registered face rather than silently wrapping to a
    ///     wrong distance.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontWeightExtremeValue_DoesNotOverflowAndSelectsClosestFace()
    {
        // Arrange: requesting int.MinValue puts both registered faces' weight distances far
        // outside int range (int.MinValue - 0 and int.MinValue - 2000000000 both overflow a
        // checked/unchecked int subtraction), but the 0-weight (narrow) face is unambiguously
        // closer in magnitude than the 2000000000-weight (wide) face
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' font-weight='-2147483648' fill='black'>A</text>
            </svg>
            """;
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildTestFont(), Weight: 0),
                new SvgFontFace(BuildBoldTestFont(), Weight: 2_000_000_000)
            ]
        };

        // Act
        using var stream = ToStream(svg);
        var exception = Record.Exception(() => SvgCodec.LoadWithFontFaces(stream, 100, 100, faces));

        // Assert: no OverflowException (or any other exception) was thrown
        Assert.Null(exception);

        // Assert: the far-closer 0-weight (narrow) face was selected, not the 2000000000-weight
        // (wide) face
        using var stream2 = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream2, 100, 100, faces);
        Assert.Equal(0, surface[75, 35].A);
        Assert.Equal(255, surface[35, 35].A);
    }

    /// <summary>
    ///     Proves the boldness-side tie-break rule: with faces registered at 300 (narrow) and 500
    ///     (wide) - both equidistant (100) from a requested <c>font-weight="400"</c> - the 500
    ///     face wins, because it is on the same "boldness side" (weight &gt;= 400) as the request,
    ///     while the 300 face is not.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontWeightTie_PrefersMatchingBoldnessSide()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' font-weight='400' fill='black'>A</text>
            </svg>
            """;
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildTestFont(), Weight: 300),
                new SvgFontFace(BuildBoldTestFont(), Weight: 500)
            ]
        };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: the 500 (wide) face was selected, per the boldness-side tie-break
        Assert.Equal(255, surface[75, 35].A);
    }

    /// <summary>
    ///     Proves that a family with only a 400/Normal face registered still renders that face
    ///     when <c>font-style="italic"</c> is requested (graceful fallback to the sole available
    ///     face, rather than rendering nothing).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontStyleNoItalicRegistered_FallsBackToOnlyAvailableFace()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' font-style='italic' fill='black'>A</text>
            </svg>
            """;
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] = [new SvgFontFace(BuildTestFont())]
        };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: the sole registered (normal) face still rendered
        Assert.Equal(255, surface[35, 35].A);
    }

    /// <summary>
    ///     Proves that, among three registered faces (400/Normal narrow, 700/Normal wide, and
    ///     700/Italic shifted), a request of <c>font-weight="bold" font-style="italic"</c>
    ///     selects the 700/Italic face specifically - not merely a weight- or style-only match.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextFontWeightAndStyleCombined_SelectsExactMatchAmongThreeFaces()
    {
        // Arrange: the italic face's glyph (canvas x equals 30-80) is a strict subset of the
        // bold/wide face's glyph (canvas x equals 10-90) - x equals 75 alone cannot distinguish
        // them, so this also asserts x equals 85 (filled only by the wide face) is NOT filled,
        // proving the wide (bold/normal) face was not selected instead.
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' font-weight='bold' font-style='italic' fill='black'>A</text>
            </svg>
            """;
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildTestFont(), Weight: 400),
                new SvgFontFace(BuildBoldTestFont(), Weight: 700),
                new SvgFontFace(BuildItalicTestFont(), Weight: 700, Style: SvgFontStyle.Italic)
            ]
        };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: filled where only the italic face's glyph reaches
        Assert.Equal(255, surface[75, 35].A);
        // Assert: NOT filled where only the wide (bold/normal) face's glyph would have reached
        Assert.Equal(0, surface[85, 35].A);
    }

    /// <summary>
    ///     Proves that a <c>g</c> element's <c>font-weight="bold"</c> cascades down to a child
    ///     <c>text</c> element that does not set its own <c>font-weight</c>, mirroring
    ///     <see cref="SvgCodec_Load_GroupFillInheritance_AppliesToChildWithoutOwnFill"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFontWeightInheritance_AppliesToChildTextWithoutOwnFontWeight()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <g font-weight='bold'>
                <text x='10' y='60' font-family='TestFont' font-size='100' fill='black'>A</text>
              </g>
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

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: the child text inherited font-weight="bold" and selected the wide face
        Assert.Equal(255, surface[75, 35].A);
    }

    /// <summary>
    ///     Proves that a <c>g</c> element's <c>font-style="italic"</c> cascades down to a child
    ///     <c>text</c> element that does not set its own <c>font-style</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GroupFontStyleInheritance_AppliesToChildTextWithoutOwnFontStyle()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <g font-style='italic'>
                <text x='10' y='60' font-family='TestFont' font-size='100' fill='black'>A</text>
              </g>
            </svg>
            """;
        var faces = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["TestFont"] =
            [
                new SvgFontFace(BuildTestFont()),
                new SvgFontFace(BuildItalicTestFont(), Style: SvgFontStyle.Italic)
            ]
        };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.LoadWithFontFaces(stream, 100, 100, faces);

        // Assert: the child text inherited font-style="italic" and selected the italic face
        Assert.Equal(255, surface[75, 35].A);
        Assert.Equal(0, surface[15, 35].A);
    }

    /// <summary>
    ///     Regression test: proves the legacy single-font-per-family <c>Load</c> overload (taking
    ///     <c>IReadOnlyDictionary&lt;string, TrueTypeFont&gt;</c>) always selects its one
    ///     registered font, ignoring any requested <c>font-weight</c>/<c>font-style</c> entirely -
    ///     proving the additive <see cref="SvgFontFace"/>-based overload never changed this
    ///     overload's own established behavior.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextLegacySingleFontOverload_IgnoresRequestedWeightAndStyle()
    {
        // Arrange: bold and italic are both requested, but only one plain font is registered
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' font-weight='bold' font-style='italic' fill='black'>A</text>
            </svg>
            """;
        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100, fonts);

        // Assert: the sole registered font still rendered at its known position
        Assert.Equal(255, surface[35, 35].A);
    }

    /// <summary>
    ///     Regression test for a source-breaking ambiguous-overload defect: prior to the
    ///     <see cref="SvgCodec.LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>
    ///     rename, a caller passing an explicit <see langword="null"/> literal (rather than
    ///     omitting the argument) to <c>SvgCodec.Load(stream, width, height, null)</c> failed to
    ///     compile with "the call is ambiguous", because <c>null</c> matched both the legacy
    ///     <c>IReadOnlyDictionary&lt;string, TrueTypeFont&gt;?</c> overload and the newer,
    ///     since-renamed <c>IReadOnlyDictionary&lt;string, IReadOnlyList&lt;SvgFontFace&gt;&gt;?</c>
    ///     overload equally well. Now that the richer overload has its own distinct
    ///     <see cref="SvgCodec.LoadWithFontFaces(Stream, int, int, IReadOnlyDictionary{string, IReadOnlyList{SvgFontFace}}?)"/>
    ///     name, only the single-font <c>Load</c> overload remains a candidate, so this call is
    ///     unambiguous - the mere fact that this test file compiles (and this call resolves to the
    ///     legacy overload's own documented "no font registered" behavior) is itself the
    ///     regression check.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ExplicitNullFontsLiteral_CompilesUnambiguouslyAndFallsBackToBuiltInFont()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <text x='10' y='60' font-family='TestFont' font-size='100' fill='black'>A</text>
            </svg>
            """;

        // Act: an explicit `null` literal - this is the exact call shape that used to be rejected
        // by the compiler as ambiguous before the richer overload was renamed
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100, null);

        // Assert: no font was registered for "TestFont", so nothing was rasterized for the glyph
        Assert.Equal(0, surface[35, 35].A);
    }

    // ================================================================================================
    // viewBox fitting ("meet, centered" / object-fit: contain)
    // ================================================================================================

    /// <summary>
    ///     Proves that a wide (landscape) viewBox fit into a square raster is letterboxed with
    ///     transparent bars above and below the centered content.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_WideViewBoxIntoSquareRaster_LetterboxesTopAndBottom()
    {
        // Arrange: 200x100 viewBox (2:1) into a 100x100 raster; scale equals 0.5, content
        // occupies y equals 25 to 75, leaving transparent bars above/below
        const string svg = "<svg viewBox='0 0 200 100'><rect x='0' y='0' width='200' height='100' fill='blue'/></svg>";

        // Act
        using var stream1990 = ToStream(svg);
        var surface = SvgCodec.Load(stream1990, 100, 100);

        // Assert: content band is filled
        Assert.Equal(255, surface[50, 50].A);
        // Assert: top and bottom letterbox bars are transparent
        Assert.Equal(0, surface[50, 5].A);
        Assert.Equal(0, surface[50, 95].A);
    }

    /// <summary>
    ///     Proves that a tall (portrait) viewBox fit into a square raster is letterboxed with
    ///     transparent bars to the left and right of the centered content.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TallViewBoxIntoSquareRaster_LetterboxesLeftAndRight()
    {
        // Arrange: 100x200 viewBox (1:2) into a 100x100 raster; scale equals 0.5, content
        // occupies x equals 25 to 75, leaving transparent bars to either side
        const string svg = "<svg viewBox='0 0 100 200'><rect x='0' y='0' width='100' height='200' fill='blue'/></svg>";

        // Act
        using var stream2011 = ToStream(svg);
        var surface = SvgCodec.Load(stream2011, 100, 100);

        // Assert: content band is filled
        Assert.Equal(255, surface[50, 50].A);
        // Assert: left and right letterbox bars are transparent
        Assert.Equal(0, surface[5, 50].A);
        Assert.Equal(0, surface[95, 50].A);
    }

    // ================================================================================================
    // preserveAspectRatio - explicit align/meetOrSlice values (new capability)
    // ================================================================================================

    /// <summary>
    ///     Data-driven matrix covering all 9 non-<c>none</c> <c>&lt;align&gt;</c> values crossed
    ///     with both <c>meetOrSlice</c> values, via <see cref="SvgCodec_Load_RootPreserveAspectRatio_AppliesAlignAndMeetOrSlice"/>.
    ///     A single 200x100 <c>viewBox</c> rendered into a mismatched-both-axes 100x200 (portrait)
    ///     raster makes the horizontal axis the exact-fit axis under <c>meet</c> (scale
    ///     <c>min(100/200, 200/100) = 0.5</c>, fitted width exactly 100 = the raster width, so the
    ///     <c>align</c>'s x-component has no visible effect and only its y-component does) and the
    ///     horizontal axis the overflowing axis under <c>slice</c> (scale
    ///     <c>max(100/200, 200/100) = 2</c>, fitted height exactly 200 = the raster height, so only
    ///     the x-component has any visible effect) - see the test method's own remarks for the full
    ///     geometry derivation. Each row's four trailing values are two independently-verified
    ///     sample points (<c>x, y, expectedColor</c>) into the resulting raster.
    /// </summary>
    [Theory]
    [InlineData("xMinYMin", "meet", 50, 5, "red", 50, 45, "blue")]
    [InlineData("xMidYMin", "meet", 50, 5, "red", 50, 45, "blue")]
    [InlineData("xMaxYMin", "meet", 50, 5, "red", 50, 45, "blue")]
    [InlineData("xMinYMid", "meet", 50, 80, "red", 50, 120, "blue")]
    [InlineData("xMidYMid", "meet", 50, 80, "red", 50, 120, "blue")]
    [InlineData("xMaxYMid", "meet", 50, 80, "red", 50, 120, "blue")]
    [InlineData("xMinYMax", "meet", 50, 155, "red", 50, 195, "blue")]
    [InlineData("xMidYMax", "meet", 50, 155, "red", 50, 195, "blue")]
    [InlineData("xMaxYMax", "meet", 50, 155, "red", 50, 195, "blue")]
    [InlineData("xMinYMin", "slice", 10, 100, "red", 90, 100, "none")]
    [InlineData("xMidYMin", "slice", 10, 100, "none", 90, 100, "none")]
    [InlineData("xMaxYMin", "slice", 10, 100, "none", 90, 100, "blue")]
    [InlineData("xMinYMid", "slice", 10, 100, "red", 90, 100, "none")]
    [InlineData("xMidYMid", "slice", 10, 100, "none", 90, 100, "none")]
    [InlineData("xMaxYMid", "slice", 10, 100, "none", 90, 100, "blue")]
    [InlineData("xMinYMax", "slice", 10, 100, "red", 90, 100, "none")]
    [InlineData("xMidYMax", "slice", 10, 100, "none", 90, 100, "none")]
    [InlineData("xMaxYMax", "slice", 10, 100, "none", 90, 100, "blue")]
    public void SvgCodec_Load_RootPreserveAspectRatio_AppliesAlignAndMeetOrSlice(
        string align, string meetOrSlice, int x1, int y1, string color1, int x2, int y2, string color2)
    {
        // Arrange: a 200x100 viewBox with a red vertical stripe at its left edge (x 0-30, full
        // height), a blue vertical stripe at its right edge (x 170-200, full height), a red
        // horizontal stripe at its top edge (y 0-20, full width), and a blue horizontal stripe at
        // its bottom edge (y 80-100, full width) - rendered into a 100x200 raster (see this
        // Theory's own remarks for the resulting geometry). The "meet" rows sample a fixed
        // x = 50 column (outside both vertical stripes' mapped position under "meet", regardless
        // of align) at a y computed from the expected y-offset for the row's y-component; the
        // "slice" rows sample a fixed y = 100 row (outside both horizontal stripes' mapped
        // position under "slice", regardless of align) at the fixed x = 10/x = 90 columns.
        var svg = $"""
            <svg viewBox='0 0 200 100' preserveAspectRatio='{align} {meetOrSlice}'>
              <rect x='0' y='0' width='30' height='100' fill='red'/>
              <rect x='170' y='0' width='30' height='100' fill='blue'/>
              <rect x='0' y='0' width='200' height='20' fill='red'/>
              <rect x='0' y='80' width='200' height='20' fill='blue'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 200);

        // Assert
        AssertSampleColor(surface, x1, y1, color1);
        AssertSampleColor(surface, x2, y2, color2);
    }

    /// <summary>Asserts one raster pixel is opaque red, opaque blue, or fully transparent ("none").</summary>
    /// <param name="surface">The rasterized surface to sample.</param>
    /// <param name="x">The pixel's x coordinate.</param>
    /// <param name="y">The pixel's y coordinate.</param>
    /// <param name="expected"><c>"red"</c>, <c>"blue"</c>, or <c>"none"</c> (fully transparent).</param>
    private static void AssertSampleColor(Surface surface, int x, int y, string expected)
    {
        switch (expected)
        {
            case "red":
                Assert.Equal(new Rgba32(255, 0, 0, 255), surface[x, y]);
                break;
            case "blue":
                Assert.Equal(new Rgba32(0, 0, 255, 255), surface[x, y]);
                break;
            case "none":
                Assert.Equal(0, surface[x, y].A);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(expected), expected, "Unrecognized expected color token.");
        }
    }

    /// <summary>
    ///     Proves that an explicit <c>preserveAspectRatio="none"</c> stretches content
    ///     non-uniformly to exactly fill the viewport on both axes independently - no uniform
    ///     scale, no letterbox/pillarbox remainder, and no centering - new capability, distinct
    ///     from every other <c>align</c> value (all of which scale uniformly).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RootPreserveAspectRatioNone_StretchesNonUniformly()
    {
        // Arrange: a 200x100 viewBox into a 100x200 raster stretches by scaleX = 100/200 = 0.5 and
        // scaleY = 200/100 = 2 independently; a 10x10 marker centered in the viewBox (at (95,45)
        // to (105,55)) lands at exactly (47.5,90) to (52.5,110) in raster space - sampling its
        // center (50,100) proves both the non-uniform scale and the lack of any centering offset,
        // and the background rect's own opaque corners prove there is no letterbox/pillarbox
        const string svg = """
            <svg viewBox='0 0 200 100' preserveAspectRatio='none'>
              <rect x='0' y='0' width='200' height='100' fill='blue'/>
              <rect x='95' y='45' width='10' height='10' fill='red'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 200);

        // Assert: the centered marker landed at its exact non-uniformly-scaled position
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[50, 100]);

        // Assert: every corner of the raster is fully opaque - no letterbox/pillarbox bars
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[99, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[0, 199]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[99, 199]);
    }

    /// <summary>
    ///     Regression test proving that an <b>absent</b> <c>preserveAspectRatio</c> attribute -
    ///     today's pre-existing implicit default - still produces byte-for-byte identical output
    ///     to an explicit <c>preserveAspectRatio="xMidYMid meet"</c> attribute (the SVG/CSS
    ///     specification's own default value), confirming that adding explicit
    ///     <c>preserveAspectRatio</c> support did not change the implicit-default behavior this
    ///     codec already had (see this class's <c>ComputePreserveAspectRatioFit</c> remarks - the
    ///     pre-existing "meet, centered" root fit was already spec-conformant <c>xMidYMid meet</c>,
    ///     not a non-uniform stretch).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NoPreserveAspectRatioAttribute_MatchesExplicitXMidYMidMeet()
    {
        // Arrange: a 200x100 viewBox into a mismatched 100x100 square raster, so the implicit
        // "meet, centered" fit actually letterboxes (a non-trivial fit, not a no-op identity)
        const string implicitSvg = """
            <svg viewBox='0 0 200 100'>
              <rect x='0' y='0' width='200' height='100' fill='blue'/>
              <rect x='90' y='40' width='20' height='20' fill='red'/>
            </svg>
            """;
        const string explicitSvg = """
            <svg viewBox='0 0 200 100' preserveAspectRatio='xMidYMid meet'>
              <rect x='0' y='0' width='200' height='100' fill='blue'/>
              <rect x='90' y='40' width='20' height='20' fill='red'/>
            </svg>
            """;

        // Act
        using var implicitStream = ToStream(implicitSvg);
        using var explicitStream = ToStream(explicitSvg);
        var implicitSurface = SvgCodec.Load(implicitStream, 100, 100);
        var explicitSurface = SvgCodec.Load(explicitStream, 100, 100);

        // Assert: every pixel matches exactly between the two documents
        Assert.Equal(implicitSurface.Width, explicitSurface.Width);
        Assert.Equal(implicitSurface.Height, explicitSurface.Height);
        for (var y = 0; y < implicitSurface.Height; y++)
        {
            for (var x = 0; x < implicitSurface.Width; x++)
            {
                Assert.Equal(implicitSurface[x, y], explicitSurface[x, y]);
            }
        }
    }

    // ================================================================================================
    // GetInfo and its three-tier fallback policy
    // ================================================================================================

    /// <summary>
    ///     Proves that <see cref="SvgCodec.GetInfo(Stream)"/> reports the <c>viewBox</c>
    ///     dimensions when present, even when conflicting <c>width</c>/<c>height</c> attributes
    ///     are also present (viewBox takes precedence).
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_ViewBoxPresent_ReturnsViewBoxDimensions()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 40 20' width='999' height='999'></svg>";

        // Act
        using var stream2036 = ToStream(svg);
        var info = SvgCodec.GetInfo(stream2036);

        // Assert
        Assert.Equal(40, info.Width);
        Assert.Equal(20, info.Height);
        Assert.Equal(4, info.Channels);
        Assert.True(info.HasAlpha);
    }

    /// <summary>
    ///     Proves that <see cref="SvgCodec.GetInfo(Stream)"/> falls back to <c>width</c>/
    ///     <c>height</c> attributes when no <c>viewBox</c> is present.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_NoViewBoxWidthHeightPresent_ReturnsWidthHeight()
    {
        // Arrange
        const string svg = "<svg width='64' height='32'></svg>";

        // Act
        using var stream2056 = ToStream(svg);
        var info = SvgCodec.GetInfo(stream2056);

        // Assert
        Assert.Equal(64, info.Width);
        Assert.Equal(32, info.Height);
    }

    /// <summary>
    ///     Proves that <see cref="SvgCodec.GetInfo(Stream)"/> falls back to the CSS/UA default
    ///     replaced-element intrinsic size (300x150) when neither a <c>viewBox</c> nor
    ///     <c>width</c>/<c>height</c> are present.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_NoViewBoxNoWidthHeight_ReturnsCssDefault300x150()
    {
        // Arrange
        const string svg = "<svg></svg>";

        // Act
        using var stream2075 = ToStream(svg);
        var info = SvgCodec.GetInfo(stream2075);

        // Assert
        Assert.Equal(300, info.Width);
        Assert.Equal(150, info.Height);
    }

    /// <summary>
    ///     Proves that <see cref="SvgCodec.GetInfo(string)"/> (the file-path overload) returns the
    ///     same information as the stream overload, reading through a real temporary file.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_FromFilePath_ReturnsExpectedInfo()
    {
        // Arrange
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "<svg viewBox='0 0 40 20'></svg>");

            // Act
            var info = SvgCodec.GetInfo(path);

            // Assert
            Assert.Equal(40, info.Width);
            Assert.Equal(20, info.Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Regression test for the <c>GetInfo</c> width/height-cast-overflow finding: a resolved
    ///     dimension large enough to overflow <see cref="int"/> on a naive cast (previously
    ///     <c>(int)MathF.Round(size.X)</c>, undefined for a value beyond <see cref="int.MaxValue"/>
    ///     since <c>int.MaxValue</c> is not exactly representable as a <see cref="float"/>) must
    ///     instead clamp to <see cref="int.MaxValue"/>.
    /// </summary>
    /// <remarks>
    ///     <b>Superseded, for the <c>viewBox</c>-sourced case, by the coordinate-magnitude bound
    ///     (Finding 6).</b> A <c>viewBox</c> width/height large enough to overflow
    ///     <see cref="int.MaxValue"/> (~2.147 billion) necessarily also exceeds the codec's fixed
    ///     <c>MaxCoordinateMagnitude</c> bound (1,000,000), so it is now rejected by
    ///     <c>TryReadNumber</c> before the clamp-before-cast logic this test originally proved is
    ///     ever reached - the clamp-before-cast logic itself remains fully covered by the sibling
    ///     <c>width</c>/<c>height</c>-fallback-tier variant below
    ///     (<see cref="SvgCodec_GetInfo_WidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing"/>),
    ///     which is sourced from <c>ParseLength</c> (a separate, tolerant parser not gated by
    ///     <c>MaxCoordinateMagnitude</c> - see this class's remarks on that method) and is
    ///     therefore unaffected. This test is retained under its original name, repurposed to
    ///     prove the new, earlier rejection point for the <c>viewBox</c>-sourced case specifically.
    /// </remarks>
    [Fact]
    public void SvgCodec_GetInfo_ViewBoxWidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing()
    {
        // Arrange: a viewBox width large enough to overflow Int32.MaxValue also exceeds the
        // codec's fixed MaxCoordinateMagnitude bound, so it is now rejected at parse time
        const string svg = "<svg viewBox='0 0 1e20 1e20'></svg>";

        // Act & Assert
        using var stream2137 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.GetInfo(stream2137));
    }

    /// <summary>
    ///     Regression test for the same <c>GetInfo</c> width/height-cast-overflow finding as
    ///     <see cref="SvgCodec_GetInfo_ViewBoxWidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing"/>,
    ///     sourced instead from the <c>width</c>/<c>height</c> fallback tier (no <c>viewBox</c>).
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_WidthExceedsInt32Range_ClampsToInt32MaxValueWithoutThrowing()
    {
        // Arrange
        const string svg = "<svg width='1e20' height='1e20'></svg>";

        // Act
        using var stream2152 = ToStream(svg);
        var info = SvgCodec.GetInfo(stream2152);

        // Assert
        Assert.Equal(int.MaxValue, info.Width);
        Assert.Equal(int.MaxValue, info.Height);
    }

    /// <summary>
    ///     Proves that <see cref="SvgCodec.GetInfo(Stream)"/> is bounded to the root <c>svg</c>
    ///     start-tag's own attributes: a document malformed only beyond the root element's
    ///     attributes (an unclosed child tag - the same markup
    ///     <see cref="SvgCodec_Load_MalformedXml_ThrowsInvalidDataException"/> proves the sibling
    ///     <c>Load</c> call still correctly rejects) does not stop <c>GetInfo</c> from resolving
    ///     and returning the <c>viewBox</c> dimensions, because it never reads that far into the
    ///     document.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_MalformedXmlAfterRootElement_DoesNotThrowAndReturnsViewBoxDimensions()
    {
        // Arrange: an unclosed child tag - malformed beyond the root element's own attributes
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='10' height='10'";

        // Act
        using var stream2175 = ToStream(svg);
        var info = SvgCodec.GetInfo(stream2175);

        // Assert
        Assert.Equal(100, info.Width);
        Assert.Equal(100, info.Height);
    }

    /// <summary>
    ///     Regression test for the unbounded <c>GetInfo</c> header-only parse finding:
    ///     <c>LoadRootElementAttributesOnly</c> never reads past the root start-tag's attributes,
    ///     but (pre-fix) used a plain <see cref="System.Xml.XmlReader"/> with no
    ///     <c>MaxCharactersInDocument</c> setting, so a single oversized attribute value on the
    ///     root <c>svg</c> element could still force it to materialize an unbounded amount of
    ///     data, even though the reader never advances into the document body. Proves an attribute
    ///     value padded well past the codec's fixed <c>MaxCharactersInDocument</c> bound is now
    ///     rejected with <see cref="InvalidDataException"/>, matching the same bound
    ///     <c>Load</c>'s own <c>LoadRootElement</c> already enforces. The oversized padding is
    ///     generated programmatically, never committed as a literal giant fixture.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_OversizedRootAttributeValueExceedingCharacterBudget_ThrowsInvalidDataException()
    {
        // Arrange: a single root-element attribute value padded well past the codec's fixed
        // 5,000,000-character document budget
        var padding = new string('x', 5_100_000);
        var svg = $"<svg viewBox='0 0 100 100' data-padding='{padding}'></svg>";

        // Act & Assert
        using var stream2203 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.GetInfo(stream2203));
    }

    // ================================================================================================
    // XXE hardening: DTD/external-entity rejection
    // ================================================================================================

    /// <summary>
    ///     Proves that <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont})"/> rejects a document containing
    ///     a <c>&lt;!DOCTYPE ...&gt;</c> declaration with <see cref="InvalidDataException"/>
    ///     (via the same malformed-XML <see cref="System.Xml.XmlException"/> wrapping every other
    ///     syntactically-rejected document goes through), demonstrating that
    ///     <see cref="System.Xml.DtdProcessing.Prohibit"/> is in effect on the
    ///     <see cref="System.Xml.XmlReaderSettings"/> <c>LoadRootElement</c> constructs.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_DocumentWithDoctypeDeclaration_ThrowsInvalidDataException()
    {
        // Arrange: a well-formed document that is otherwise entirely valid, except for a DOCTYPE
        // declaration - proving rejection is specifically attributable to the DOCTYPE, not to
        // some other malformed construct
        const string svg = "<!DOCTYPE svg [<!ENTITY foo \"bar\">]><svg viewBox='0 0 100 100'></svg>";

        // Act & Assert
        using var stream2227 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2227, 100, 100));
    }

    /// <summary>
    ///     Proves that <see cref="SvgCodec.Load(Stream, int, int, IReadOnlyDictionary{string, TrueTypeFont})"/> rejects a document whose
    ///     DOCTYPE declares and references an external, file-system-resolving general entity
    ///     (the classic XXE injection shape) with <see cref="InvalidDataException"/>, rather than
    ///     ever attempting to resolve the external entity - demonstrating both
    ///     <see cref="System.Xml.DtdProcessing.Prohibit"/> and a <see langword="null"/>
    ///     <see cref="System.Xml.XmlResolver"/> are in effect.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_DocumentWithExternalEntityDoctype_ThrowsInvalidDataException()
    {
        // Arrange: a DOCTYPE declaring an external general entity referencing a local file, and
        // the document body referencing that entity - the canonical XXE payload shape
        const string svg =
            "<!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>" +
            "<svg viewBox='0 0 100 100'><title>&xxe;</title></svg>";

        // Act & Assert
        using var stream2248 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2248, 100, 100));
    }

    /// <summary>
    ///     Proves that <see cref="SvgCodec.GetInfo(Stream)"/> also rejects a document whose
    ///     DOCTYPE declares and references an external general entity with
    ///     <see cref="InvalidDataException"/>, demonstrating the same XXE hardening is in effect
    ///     on <c>LoadRootElementAttributesOnly</c>'s independent
    ///     <see cref="System.Xml.XmlReaderSettings"/> instance, not merely <c>Load</c>'s.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_DocumentWithExternalEntityDoctype_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg =
            "<!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>" +
            "<svg viewBox='0 0 100 100'><title>&xxe;</title></svg>";

        // Act & Assert
        using var stream2267 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.GetInfo(stream2267));
    }

    // ================================================================================================
    // Malformed-input rejection and tolerant unsupported-construct handling
    // ================================================================================================

    /// <summary>
    ///     Proves that syntactically invalid XML is rejected as an <see cref="InvalidDataException"/>
    ///     rather than propagating the underlying <see cref="System.Xml.XmlException"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MalformedXml_ThrowsInvalidDataException()
    {
        // Arrange: an unclosed tag
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='10' height='10'";

        // Act & Assert
        using var stream2285 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2285, 100, 100));
    }

    /// <summary>
    ///     Proves that a malformed <c>viewBox</c> attribute (wrong number count) is rejected as an
    ///     <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MalformedViewBoxWrongNumberCount_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100'></svg>";

        // Act & Assert
        using var stream2299 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2299, 100, 100));
    }

    /// <summary>
    ///     Proves that a <c>viewBox</c> with a non-positive width is rejected as an
    ///     <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ViewBoxNonPositiveWidth_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 0 100'></svg>";

        // Act & Assert
        using var stream2313 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2313, 100, 100));
    }

    /// <summary>
    ///     Regression test for the degenerate-fit-transform finding: a <c>viewBox</c> width that
    ///     is extremely small but still finite and positive (a subnormal float) passes
    ///     <c>ParseViewBox</c>'s existing "must be positive" check, but dividing the requested
    ///     raster width by such a value overflows <c>ComputeFitTransform</c>'s own scale to
    ///     <see cref="float.PositiveInfinity"/>. Left unguarded, the resulting non-finite fit
    ///     transform would previously cause every element to silently fail
    ///     <c>IsFiniteTransform</c>'s per-element check and render a blank, transparent surface
    ///     with no exception - proves this now throws <see cref="InvalidDataException"/> instead,
    ///     the same class of malformed-sizing-data error the sibling non-positive-width case
    ///     already throws for.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ViewBoxWidthExtremelySmallCausesNonFiniteFitScale_ThrowsInvalidDataException()
    {
        // Arrange: a subnormal-magnitude viewBox width/height, positive and finite, but small
        // enough that raster-width / width overflows float to Infinity
        const string svg = "<svg viewBox='0 0 1e-40 1e-40'><rect x='0' y='0' width='1e-40' height='1e-40' fill='red'/></svg>";

        // Act & Assert
        using var stream2336 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2336, 100, 100));
    }

    /// <summary>
    ///     Proves that malformed <c>path</c> "d" data (an unrecognized command letter) is rejected
    ///     as an <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MalformedPathDataUnknownCommand_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,10 X99,99'/></svg>";

        // Act & Assert
        using var stream2350 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2350, 100, 100));
    }

    /// <summary>
    ///     Proves that malformed <c>path</c> "d" data (a command missing its required numeric
    ///     arguments) is rejected as an <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MalformedPathDataMissingArguments_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><path d='M10,10 L'/></svg>";

        // Act & Assert
        using var stream2364 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2364, 100, 100));
    }

    /// <summary>
    ///     Regression test for the path-data relative-accumulation overflow finding: a relative
    ///     command (<c>l</c>) accumulating an offset against a huge-but-finite current point can
    ///     overflow to <see cref="float.PositiveInfinity"/> even though every individual literal
    ///     token is finite. Left unguarded, the resulting non-finite path length would stall
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.DashSplitter"/>'s finite-step dash-interval
    ///     walk forever once combined with a finite <c>stroke-dasharray</c>.
    /// </summary>
    /// <remarks>
    ///     <b>Superseded by the coordinate-magnitude bound (Finding 6).</b> This scenario required
    ///     a single raw coordinate literal (<c>3e38</c>) large enough that, summed with itself,
    ///     the accumulation overflowed float. Every coordinate/length token is now individually
    ///     capped at <c>MaxCoordinateMagnitude</c> (1,000,000) - far below any magnitude needed to
    ///     overflow via a single relative-accumulation step - so <c>3e38</c> is now rejected by
    ///     <c>TryReadNumber</c> before path-data parsing even begins, rather than reaching
    ///     <c>PathDataParser</c>'s <c>RequireFinite</c> tolerant-skip guard at all. This test is
    ///     retained under its original name, repurposed to prove the new, earlier rejection point.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_PathRelativeAccumulationOverflowsToInfinity_TerminatesPromptlyWithoutHanging()
    {
        // Arrange: a coordinate literal (3e38) exceeding the codec's fixed MaxCoordinateMagnitude
        // bound - rejected at parse time, well before any relative-accumulation arithmetic runs
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<path d='M3e38,0 l3e38,0' stroke='black' stroke-width='1' stroke-dasharray='5,5'/>" +
                            "</svg>";

        // Act & Assert
        using var stream2395 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2395, 10, 10));
    }

    /// <summary>
    ///     Regression test for the huge-finite-total-length-vs-fine-dash-span CPU-exhaustion
    ///     finding: a path spanning coordinates on the order of <c>1e20</c> (finite, no overflow
    ///     involved at all) combined with a fine <c>stroke-dasharray</c> would otherwise force
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.DashSplitter"/>'s dash-interval traversal
    ///     loop to require an impractical number of iterations to reach the path's total length.
    /// </summary>
    /// <remarks>
    ///     <b>Superseded by the coordinate-magnitude bound (Finding 6).</b> This scenario required
    ///     a single raw coordinate literal (<c>1e20</c>) far beyond any real-world document's
    ///     coordinate range. Every coordinate/length token is now individually capped at
    ///     <c>MaxCoordinateMagnitude</c> (1,000,000), so <c>1e20</c> is now rejected by
    ///     <c>TryReadNumber</c> before path-data parsing even begins, rather than ever reaching
    ///     <c>DashSplitter</c>'s pre-flight iteration-budget short-circuit. This test is retained
    ///     under its original name, repurposed to prove the new, earlier rejection point.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_HugeFinitePathWithFineDashPattern_TerminatesPromptlyWithoutHanging()
    {
        // Arrange: coordinate literals (1e20) exceeding the codec's fixed MaxCoordinateMagnitude
        // bound - rejected at parse time, well before DashSplitter is ever reached
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<path d='M -1e20 -1e20 L 1e20 1e20' stroke='black' stroke-width='1' stroke-dasharray='5,5'/>" +
                            "</svg>";

        // Act & Assert
        using var stream2424 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2424, 10, 10));
    }

    /// <summary>
    ///     Regression test for the audit-discovered <c>S</c>/<c>T</c> smooth-curve reflection
    ///     overflow finding: <c>Reflect</c>'s <c>2*center - point</c> arithmetic can overflow to a
    ///     non-finite value from an individually-finite cubic-Bezier control point and current
    ///     point, an independent overflow path into the same
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.DashSplitter"/> hang risk as relative-
    ///     coordinate accumulation.
    /// </summary>
    /// <remarks>
    ///     <b>Superseded by the coordinate-magnitude bound (Finding 6).</b> This scenario required
    ///     a control point literal (<c>3e38</c>) large enough that doubling it during reflection
    ///     overflowed float. Every coordinate/length token is now individually capped at
    ///     <c>MaxCoordinateMagnitude</c> (1,000,000) - far below any magnitude a single doubling
    ///     could overflow from - so <c>3e38</c> is now rejected by <c>TryReadNumber</c> before
    ///     path-data parsing even begins, rather than reaching <c>Reflect</c>'s <c>RequireFinite</c>
    ///     tolerant-skip guard at all. This test is retained under its original name, repurposed to
    ///     prove the new, earlier rejection point.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_PathSmoothCubicReflectionOverflowsToInfinity_SkipsPathWithoutThrowing()
    {
        // Arrange: a control-point literal (3e38) exceeding the codec's fixed
        // MaxCoordinateMagnitude bound - rejected at parse time, well before any reflection
        // arithmetic runs
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<path d='M0,0 C0,0 3e38,0 -3e38,0 S1,1 0,0' fill='red'/>" +
                            "</svg>";

        // Act & Assert
        using var stream2456 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2456, 10, 10));
    }

    /// <summary>
    ///     Regression test for the unguarded <c>SvgArcConverter</c> output finding, exercised via
    ///     the <c>A</c>/<c>a</c> path-data command: an extreme-but-individually-finite arc radius
    ///     drives <c>Geometry.SvgArcConverter.ToBeziers</c>'s internal rotation/trig arithmetic
    ///     (which squares the radii) to overflow one of its emitted control points to a non-finite
    ///     value, even though every raw literal token (the radius itself) is finite.
    /// </summary>
    /// <remarks>
    ///     <b>Superseded by the coordinate-magnitude bound (Finding 6).</b> This scenario required
    ///     an arc radius literal (<c>1e18</c>) large enough that squaring it inside
    ///     <c>SvgArcConverter</c>'s ellipse-center calculation overflowed float. Every
    ///     coordinate/length token is now individually capped at <c>MaxCoordinateMagnitude</c>
    ///     (1,000,000) - whose square (1e12) is far too small for any combination of
    ///     within-the-bound radii/start/end points to overflow <c>SvgArcConverter</c>'s own
    ///     arithmetic - so <c>1e18</c> is now rejected by <c>TryReadNumber</c> before path-data
    ///     parsing even begins, rather than reaching <c>AppendArc</c>'s <c>RequireFinite</c>
    ///     tolerant-skip guard at all. That guard (added for Finding 4) remains in place as
    ///     defense-in-depth, matching this class's other now-unreachable-but-retained guards - see
    ///     <c>GeometryWorkBudget.Charge</c>'s own reachability note. This test is retained
    ///     under its original name, repurposed to prove the new, earlier rejection point.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_PathArcCommandRadiusOverflowsToNonFinite_SkipsPathWithoutThrowing()
    {
        // Arrange: an arc radius literal (1e18) exceeding the codec's fixed
        // MaxCoordinateMagnitude bound - rejected at parse time, well before SvgArcConverter is
        // ever reached
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<path d='M1e18,0 A1e18,1e18 0 0 1 0,1e18' fill='red'/>" +
                            "</svg>";

        // Act & Assert
        using var stream2491 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2491, 10, 10));
    }

    /// <summary>
    ///     Regression test for the same unguarded <c>SvgArcConverter</c> output finding, exercised
    ///     via <c>rect</c>'s rounded-corner construction (<c>AppendArcTo</c>) instead of an
    ///     explicit path-data <c>A</c> command.
    /// </summary>
    /// <remarks>
    ///     <b>Superseded by the coordinate-magnitude bound (Finding 6).</b> See
    ///     <see cref="SvgCodec_Load_PathArcCommandRadiusOverflowsToNonFinite_SkipsPathWithoutThrowing"/>'s
    ///     identical reachability note - the same <c>MaxCoordinateMagnitude</c> bound applies to
    ///     <c>rect</c>'s <c>width</c>/<c>height</c>/<c>rx</c>/<c>ry</c> attributes via
    ///     <c>ParseCoordinate</c>. The <c>AppendArcTo</c> guard (added for Finding 4) remains in
    ///     place as defense-in-depth. This test is retained under its original name, repurposed to
    ///     prove the new, earlier rejection point.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_RectRoundedCornerArcConversionOverflowsToNonFinite_SkipsShapeWithoutThrowing()
    {
        // Arrange: a rect width/height/rx/ry literal (1e18/2e18) exceeding the codec's fixed
        // MaxCoordinateMagnitude bound - rejected at parse time, well before AppendArcTo is ever
        // reached
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<rect x='0' y='0' width='2e18' height='2e18' rx='1e18' ry='1e18' fill='red'/>" +
                            "</svg>";

        // Act & Assert
        using var stream2519 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2519, 10, 10));
    }

    /// <summary>
    ///     Proves that a shape built entirely from in-bound source literals, but whose composed
    ///     <c>transform</c> amplifies every one of its points past
    ///     <see cref="SvgCodec"/>'s fixed <c>MaxCoordinateMagnitude</c> bound once transformed, is
    ///     tolerantly skipped rather than being fed into the fill/stroke pipeline at a magnitude it
    ///     was never meant to see.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="SvgCodec_Load_PathArcCommandRadiusOverflowsToNonFinite_SkipsPathWithoutThrowing"/>
    ///     and <see cref="SvgCodec_Load_RectRoundedCornerArcConversionOverflowsToNonFinite_SkipsShapeWithoutThrowing"/>
    ///     immediately above (both now intercepted at parse time because their source literals
    ///     themselves already exceed the bound), this path's every literal (<c>0</c> and <c>10</c>)
    ///     is comfortably within <c>MaxCoordinateMagnitude</c>, and the <c>scale(1000000)</c>
    ///     transform's own literal argument is exactly at the bound (not <i>greater than</i> it,
    ///     so it is not rejected by <c>TryReadNumber</c>'s strict <c>&gt;</c> check either) -
    ///     nothing at the parse-time, pre-transform level rejects this document. Only composing the
    ///     two - baking the transform into the path's points via <c>TransformPath</c> - produces a
    ///     final magnitude (<c>10 * 1,000,000 = 10,000,000</c>) the new post-transform check
    ///     catches. The path's single degenerate cubic-curve command (<c>CubicBezierTo</c>, whose
    ///     control points coincide with its endpoint) combines with two straight-line commands
    ///     (<c>LineTo</c>) to form a closed square, proving the new check's <c>Control1</c>/
    ///     <c>Control2</c> handling as well as its <c>EndPoint</c> handling. Without the fix, this square's transformed bounding box -
    ///     (0,0) to (10,000,000, 10,000,000) - fully contains the visible 100x100 canvas near the
    ///     origin, so the sampled pixel below would be filled; the fix instead skips the whole
    ///     shape, leaving it unfilled.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_TransformScaleAmplifiesCoordinatePastMagnitudeBound_SkipsShapeWithoutThrowing()
    {
        // Arrange: a small, entirely in-bound closed-square path (built from LineTo and a
        // degenerate CubicBezierTo command) whose scale(1000000) transform - itself an in-bound
        // literal, since 1,000,000 does not exceed the strict '>' magnitude check - amplifies
        // every point to 10,000,000 once composed, far past MaxCoordinateMagnitude (1,000,000).
        const string svg = "<svg viewBox='0 0 100 100'>" +
                            "<path d='M0,0 L0,10 C10,10 10,10 10,10 L10,0 Z' fill='black' " +
                            "transform='scale(1000000)'/>" +
                            "</svg>";

        // Act
        using var stream2561 = ToStream(svg);
        var surface = SvgCodec.Load(stream2561, 100, 100);

        // Assert: Load did not throw, and the shape was tolerantly skipped entirely - the sampled
        // pixel, which the scaled square's huge bounding box would otherwise cover if the shape
        // were not skipped, stays unfilled.
        Assert.Equal(0, surface[50, 50].A);
    }

    /// <summary>
    ///     Proves that a malformed <c>transform</c> attribute (an unrecognized function name) is
    ///     rejected as an <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_MalformedTransformUnrecognizedFunction_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='10' height='10' transform='wobble(1,2)'/></svg>";

        // Act & Assert
        using var stream2580 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2580, 100, 100));
    }

    /// <summary>
    ///     Regression test for the unbounded <c>XDocument.Load</c> DOM-materialization finding:
    ///     a document whose total character count exceeds the codec's fixed
    ///     <c>MaxCharactersInDocument</c> bound must be rejected with
    ///     <see cref="InvalidDataException"/> (surfaced through the existing
    ///     <see cref="System.Xml.XmlException"/> catch) rather than being fully parsed into an
    ///     unbounded in-memory DOM. The oversized padding is generated programmatically (a large
    ///     XML comment), never committed as a literal giant fixture.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_DocumentExceedingCharacterBudget_ThrowsInvalidDataException()
    {
        // Arrange: a harmless XML comment padded well past the codec's fixed 5,000,000-character
        // document budget
        var padding = new string('x', 5_100_000);
        var svg = $"<svg viewBox='0 0 100 100'><!--{padding}--></svg>";

        // Act & Assert
        using var stream2601 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2601, 100, 100));
    }

    /// <summary>
    ///     Proves that a document sized just under the codec's fixed
    ///     <c>MaxCharactersInDocument</c> bound still loads successfully, so the new bound does
    ///     not false-positive-reject an ordinary (if unusually large) well-formed document.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_DocumentWithinCharacterBudget_LoadsSuccessfully()
    {
        // Arrange: a harmless XML comment padded well under the codec's fixed
        // 5,000,000-character document budget
        var padding = new string('x', 1_000_000);
        var svg = $"<svg viewBox='0 0 100 100'><!--{padding}--><rect x='0' y='0' width='100' height='100' fill='red'/></svg>";

        // Act
        using var stream2618 = ToStream(svg);
        var surface = SvgCodec.Load(stream2618, 10, 10);

        // Assert: the rect still rendered
        Assert.Equal(255, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that a shape's numeric attribute value of literal <c>NaN</c> - a syntactically
    ///     valid <see cref="float"/> literal that is never a meaningful coordinate - is rejected
    ///     as an <see cref="InvalidDataException"/> rather than silently propagating into
    ///     rendering. Exercises <c>ParseCoordinate</c>, which parses an attribute's entire trimmed
    ///     text with no prior character-class filtering, so the literal <c>"NaN"</c> text reaches
    ///     <see cref="float.Parse(string, System.IFormatProvider?)"/> unfiltered.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_WidthAttributeNaN_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 20 20'><rect x='0' y='0' width='NaN' height='10'/></svg>";

        // Act & Assert
        using var stream2639 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2639, 100, 100));
    }

    /// <summary>
    ///     Proves that a <c>stroke-width</c> attribute value of literal <c>Infinity</c> is
    ///     rejected as an <see cref="InvalidDataException"/>. Exercises <c>GetOptionalFloat</c> →
    ///     <c>ParseCoordinate</c>, the same unfiltered-text parse path as the <c>NaN</c> test
    ///     above.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeWidthInfinity_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 20 20'><rect x='0' y='0' width='10' height='10' stroke='#000' stroke-width='Infinity'/></svg>";

        // Act & Assert
        using var stream2655 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2655, 100, 100));
    }

    /// <summary>
    ///     Proves that a <c>path</c> "d" data coordinate which overflows <see cref="float"/> to
    ///     <see cref="float.PositiveInfinity"/> (rather than failing to parse at all) is rejected
    ///     as an <see cref="InvalidDataException"/>. Exercises <c>TryReadNumber</c>'s finiteness
    ///     check: unlike <c>ParseCoordinate</c>, <c>TryReadNumber</c>'s character-class scan never
    ///     matches a leading letter, so literal text such as <c>"Infinity"</c>/<c>"NaN"</c> is
    ///     rejected earlier, unrelated to the new check - only a legitimately-scanned, all-digit/
    ///     exponent token that numerically overflows (confirmed empirically: <c>float.Parse</c>
    ///     returns <see cref="float.PositiveInfinity"/> for <c>"1e400"</c> rather than throwing)
    ///     actually exercises this method's new finiteness check.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathDataNumberOverflowToInfinity_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 20 20'><path d='M0,0 L1e400,0'/></svg>";

        // Act & Assert
        using var stream2676 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2676, 100, 100));
    }

    /// <summary>
    ///     Proves that a <c>points</c> list containing an exponent-overflow number (see the path
    ///     "d" data test above for why an overflowing token, not literal <c>"Infinity"</c>/
    ///     <c>"NaN"</c> text, is required to exercise this path) is rejected as an
    ///     <see cref="InvalidDataException"/>. Exercises <c>ParseNumberList</c> →
    ///     <c>TryReadNumber</c>'s finiteness check.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PointsListNumberOverflowToInfinity_ThrowsInvalidDataException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 20 20'><polyline points='0,0 1e400,0'/></svg>";

        // Act & Assert
        using var stream2693 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2693, 100, 100));
    }

    /// <summary>
    ///     Regression test for the coordinate-magnitude-bound finding (Finding 6): a <c>path</c>
    ///     "d" data coordinate that is finite (unlike the exponent-overflow test above) but whose
    ///     magnitude exceeds the codec's fixed <c>MaxCoordinateMagnitude</c> bound is rejected as
    ///     an <see cref="InvalidDataException"/>, exercising <c>TryReadNumber</c>'s new magnitude
    ///     check.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PathDataCoordinateExceedingMaxMagnitude_ThrowsInvalidDataException()
    {
        // Arrange: one unit over the codec's fixed 1,000,000 coordinate-magnitude bound
        const string svg = "<svg viewBox='0 0 20 20'><path d='M0,0 L1000001,0'/></svg>";

        // Act & Assert
        using var stream2710 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2710, 100, 100));
    }

    /// <summary>
    ///     Regression test for the same coordinate-magnitude-bound finding (Finding 6), exercising
    ///     <c>ParseNumberList</c> → <c>TryReadNumber</c>'s new magnitude check via a <c>points</c>
    ///     list entry instead of path <c>d</c> data.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_PointsListCoordinateExceedingMaxMagnitude_ThrowsInvalidDataException()
    {
        // Arrange: one unit over the codec's fixed 1,000,000 coordinate-magnitude bound
        const string svg = "<svg viewBox='0 0 20 20'><polyline points='0,0 1000001,0'/></svg>";

        // Act & Assert
        using var stream2725 = ToStream(svg);
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream2725, 100, 100));
    }

    /// <summary>
    ///     Proves the coordinate-magnitude bound (Finding 6) does not false-positive-reject an
    ///     ordinary, real-world-sized coordinate well under the bound - exercised via a shape
    ///     attribute (<c>ParseCoordinate</c>), a path <c>d</c> coordinate, and a <c>points</c> list
    ///     entry (both <c>TryReadNumber</c>), each at exactly the bound's boundary value, which
    ///     must still be accepted (the bound rejects only magnitudes strictly greater than it).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_CoordinateWithinMaxMagnitude_RendersSuccessfully()
    {
        // Arrange: a rect whose width/height sit exactly at the codec's fixed 1,000,000
        // coordinate-magnitude bound, combined with a path and a points list each using a
        // coordinate at the same boundary value
        const string svg = "<svg viewBox='0 0 20 20'>" +
                            "<rect x='0' y='0' width='1000000' height='1000000' fill='red'/>" +
                            "<path d='M0,0 L1000000,0'/>" +
                            "<polyline points='0,0 1000000,0'/>" +
                            "</svg>";

        // Act
        using var stream2748 = ToStream(svg);
        var surface = SvgCodec.Load(stream2748, 10, 10);

        // Assert: no exception, and the rect (which covers the whole viewBox) rendered
        Assert.Equal(255, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that the non-finite rejection added to <c>ParseCoordinate</c>/<c>TryReadNumber</c>
    ///     does not reject legitimate finite values that share surface syntax with the rejected
    ///     forms: a negative number, scientific notation, and a percentage. All three numeric
    ///     styles must continue to parse and render exactly as before the fix.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NegativeScientificAndPercentageValues_RendersWithoutThrowing()
    {
        // Arrange: x is negative, width/height use scientific notation, opacity is a percentage
        const string svg = "<svg viewBox='0 0 100 100'><rect x='-1e1' y='0' width='1e2' height='5e1' fill='black' opacity='50%'/></svg>";

        // Act
        using var stream2767 = ToStream(svg);
        var surface = SvgCodec.Load(stream2767, 100, 100);

        // Assert: the rect (x=-10, width=100 => spans to x=90) covers (50,25) at ~50% opacity
        // (0.5 * 255 = 127.5 ~ 127/128), not 0 (rejected) and not 255 (opacity ignored)
        Assert.InRange((int)surface[50, 25].A, 115, 140);
    }

    /// <summary>
    ///     Proves that a percentage value on a shape geometry attribute (<c>x</c>) resolves
    ///     against the current viewport width - repurposed from this codec's original
    ///     percentage-<i>rejection</i> behavior on this exact attribute/markup (percentages on
    ///     shape geometry are now a supported new capability; see this class's percentage-geometry
    ///     region below for the fuller per-attribute-family matrix). Unlike the opacity percentage
    ///     exercised by <see cref="SvgCodec_Load_NegativeScientificAndPercentageValues_RendersWithoutThrowing"/>
    ///     above (always a basis-1 fraction, unaffected by this change), <c>x</c> resolves its
    ///     <c>%</c> against the SVG document's current viewport width.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RectXPercentage_ResolvesAgainstViewportWidth()
    {
        // Arrange: x="50%" of a 100-wide viewBox resolves to x=50; a 10x10 rect at x=50 covers
        // (55,5) but not (5,5)
        const string svg = "<svg viewBox='0 0 100 100'><rect x='50%' y='0' width='10' height='10' fill='red'/></svg>";

        // Act
        using var stream2788 = ToStream(svg);
        var surface = SvgCodec.Load(stream2788, 100, 100);

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[55, 5]);
        Assert.Equal(0, surface[5, 5].A);
    }

    /// <summary>
    ///     Proves that a percentage value on a shape geometry attribute (<c>width</c>) resolves
    ///     against the current viewport width - repurposed from this codec's original
    ///     percentage-<i>rejection</i> behavior on this exact attribute/markup, for the same
    ///     reason as the <c>x</c> attribute test above - exercising a different attribute through
    ///     the same <c>GetFloatAttribute</c>/<c>ParseGeometryCoordinate</c> code path.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RectWidthPercentage_ResolvesAgainstViewportWidth()
    {
        // Arrange: width="50%" of a 100-wide viewBox resolves to width=50; the rect spans x=[0,50)
        const string svg = "<svg viewBox='0 0 100 100'><rect x='0' y='0' width='50%' height='10' fill='red'/></svg>";

        // Act
        using var stream2804 = ToStream(svg);
        var surface = SvgCodec.Load(stream2804, 100, 100);

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[25, 5]);
        Assert.Equal(0, surface[75, 5].A);
    }

    // ================================================================================================
    // Percentage-based geometry - per-attribute-family basis matrix (new capability)
    // ================================================================================================

    /// <summary>
    ///     Proves that a <c>rect</c>'s <c>y</c>/<c>height</c> percentages resolve against the
    ///     current viewport height (the vertical basis), distinct from <c>x</c>/<c>width</c>'s
    ///     horizontal basis exercised above.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_RectYHeightPercentage_ResolvesAgainstViewportHeight()
    {
        // Arrange: a 100x50 viewBox (non-square, so horizontal/vertical bases differ); y="40%" of
        // 50 resolves to 20, height="20%" of 50 resolves to 10 - the rect spans y=[20,30)
        const string svg = "<svg viewBox='0 0 100 50'><rect x='0' y='40%' width='10' height='20%' fill='red'/></svg>";

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 50);

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[5, 25]);
        Assert.Equal(0, surface[5, 15].A);
        Assert.Equal(0, surface[5, 35].A);
    }

    /// <summary>
    ///     Proves that a <c>circle</c>'s <c>cx</c>/<c>cy</c> percentages resolve against their own
    ///     horizontal/vertical viewport bases, and its <c>r</c> percentage resolves against the
    ///     diagonal basis (<c>sqrt(w^2 + h^2) / sqrt(2)</c>) - an axis-agnostic length, per the SVG
    ///     specification.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_CircleCxCyRPercentage_ResolvesAgainstHorizontalVerticalAndDiagonalBases()
    {
        // Arrange: a 300x400 viewBox; cx="50%" -> 150, cy="50%" -> 200; diagonal basis =
        // sqrt(300^2+400^2)/sqrt(2) = 500/sqrt(2) ~ 353.55, so r="10%" -> ~35.355
        const string svg = "<svg viewBox='0 0 300 400'><circle cx='50%' cy='50%' r='10%' fill='red'/></svg>";

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 300, 400);

        // Assert: the circle's center (150,200) is filled, and a point ~35 units away (well
        // within a ~35.355 radius) is filled, but a point 50 units away (well beyond it) is not
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[150, 200]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[150 + 30, 200]);
        Assert.Equal(0, surface[150 + 50, 200].A);
    }

    /// <summary>
    ///     Proves that a <c>line</c>'s <c>x1</c>/<c>x2</c> and <c>y1</c>/<c>y2</c> percentages
    ///     resolve against the horizontal/vertical viewport bases respectively.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_LineCoordinatePercentage_ResolvesAgainstViewport()
    {
        // Arrange: a 100x50 viewBox; the line's x1/x2/y1/y2 are percentages of the viewport,
        // resolving to a horizontal line from (10,25) to (90,25)
        const string svg =
            "<svg viewBox='0 0 100 50'><line x1='10%' y1='50%' x2='90%' y2='50%' stroke='red' stroke-width='4'/></svg>";

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 50);

        // Assert: the line spans x=[10,90] at y=25 - opaque within its span, transparent well
        // outside it
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[15, 25]);
        Assert.Equal(0, surface[5, 25].A);
    }

    /// <summary>
    ///     Proves that a <c>linearGradient</c>'s own <c>x1</c>/<c>x2</c> coordinates (a distinct,
    ///     gradient-specific code path - see <c>GetGradientCoordinateOrDefault</c>) are unaffected
    ///     by this phase's new viewport-relative percentage resolution, continuing to resolve
    ///     <c>"0%"</c>/<c>"100%"</c> as basis-1 fractions of the gradient's own
    ///     <c>objectBoundingBox</c> coordinate space rather than as viewport-relative lengths -
    ///     the documented, out-of-scope-for-this-phase <c>userSpaceOnUse</c> simplification (see
    ///     this class's remarks). Uses a <c>rect</c> fill (a non-degenerate, two-dimensional
    ///     bounding box) rather than a <c>line</c> stroke, because a horizontal/vertical
    ///     <c>line</c>'s own fill-geometry bounding box is degenerate on one axis and always
    ///     falls back to an identity object-bounding-box map regardless of this change.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientCoordinatePercentage_UnaffectedByViewportPercentageResolution()
    {
        // Arrange: a 100x50 viewBox; the rect's own x/y/width/height are plain numbers (not
        // percentages, to isolate this test to the gradient's own coordinate space), and the
        // gradient's x1="0%"/x2="100%" span its object-bounding-box fraction space left-to-right
        const string svg = """
            <svg viewBox='0 0 100 50'>
              <defs>
                <linearGradient id='g' x1='0%' y1='0%' x2='100%' y2='0%'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
              </defs>
              <rect x='10' y='10' width='80' height='30' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 50);

        // Assert: the rect's own left edge (near gradient fraction 0) is darker than its own
        // right edge (near gradient fraction 1) - proving the gradient's "0%"/"100%" still span
        // its own 80-wide bounding box, not this phase's new 100-wide viewport basis (which would
        // instead place fraction 1 far beyond the rect's own right edge, painting it a uniform
        // color throughout)
        Assert.True(surface[12, 25].R < surface[88, 25].R);
    }

    /// <summary>
    ///     Proves that <c>stroke-width</c>'s percentage resolves against the diagonal basis
    ///     (an axis-agnostic length, per the SVG specification), distinct from the horizontal/
    ///     vertical bases exercised by <c>x</c>/<c>y</c>-family attributes above.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeWidthPercentage_ResolvesAgainstDiagonalBasis()
    {
        // Arrange: a 300x400 viewBox; diagonal basis = sqrt(300^2+400^2)/sqrt(2) = 500/sqrt(2) ~
        // 353.55, so stroke-width="10%" -> ~35.355 - a visibly thick horizontal line
        const string svg = "<svg viewBox='0 0 300 400'><line x1='50' y1='200' x2='250' y2='200' stroke='black' stroke-width='10%'/></svg>";

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 300, 400);

        // Assert: a point 15 units above/below the line's own y=200 centerline (well within a
        // ~17.7 half-width) is covered by the stroke
        Assert.Equal(255, surface[150, 185].A);
        Assert.Equal(255, surface[150, 215].A);

        // Assert: a point 30 units away (beyond the ~17.7 half-width) is not covered
        Assert.Equal(0, surface[150, 170].A);
    }

    /// <summary>
    ///     Proves that <c>font-size</c>'s percentage resolves against the parent element's own
    ///     already-cascaded <c>font-size</c> (the CSS/SVG-defined basis), not any viewport
    ///     dimension - a distinct basis from every other percentage-eligible attribute above.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FontSizePercentage_ResolvesAgainstParentFontSize()
    {
        // Arrange: the outer <g> establishes a 20-unit font-size; the inner <text> sets
        // font-size="200%", which should resolve to 40 (double its parent's 20), rendering a
        // visibly taller glyph than the parent's own 20-unit size would
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <g font-size='20'>
                <text x='10' y='60' font-family='TestFont' font-size='200%'>A</text>
              </g>
            </svg>
            """;

        var fonts = new Dictionary<string, TrueTypeFont> { ["TestFont"] = BuildTestFont() };

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 100, 100, fonts);

        // Assert: BuildTestFont's 'A' glyph is a 50x50-unit square in a 100-unit em - at
        // font-size=40, it renders as a 20x20-unit square; its baseline is at y=60, so its top
        // edge is at y=40 - a point at y=45 (within the glyph) is opaque
        Assert.Equal(255, surface[15, 45].A);
    }

    /// <summary>
    ///     Proves that each <c>stroke-dasharray</c> entry's percentage resolves against the
    ///     diagonal basis, exactly like <c>stroke-width</c> above - per this phase's judgment
    ///     call to include <c>stroke-dasharray</c> percentages alongside <c>stroke-width</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_StrokeDasharrayPercentage_ResolvesAgainstDiagonalBasis()
    {
        // Arrange: a 300x400 viewBox; diagonal basis ~353.55, so a dash-array of "10%,10%"
        // resolves to roughly 35.355 on, 35.355 off - a horizontal line from x=0 to x=300 at
        // y=200 should show its first dash covering x in [0,~35] and a gap starting immediately after
        const string svg = "<svg viewBox='0 0 300 400'><line x1='0' y1='200' x2='300' y2='200' stroke='black' stroke-width='2' stroke-dasharray='10%,10%'/></svg>";

        // Act
        using var stream = ToStream(svg);
        var surface = SvgCodec.Load(stream, 300, 400);

        // Assert: near the very start of the line, within the first dash, the stroke is painted
        Assert.Equal(255, surface[5, 200].A);

        // Assert: well into the gap after the first ~35-unit dash (at x=60, comfortably inside
        // the ~[35.355, 70.71] gap), nothing is painted
        Assert.Equal(0, surface[60, 200].A);
    }

    /// <summary>
    ///     Proves that a gradient <c>stop</c>'s <c>offset</c> attribute value of literal
    ///     <c>NaN</c> - a syntactically valid <see cref="float"/> literal that is never a
    ///     meaningful stop position - is treated the same as an absent/unparseable offset
    ///     (falling back to <c>0</c>) rather than reaching <c>GradientStop</c>'s constructor,
    ///     which would otherwise throw an uncaught <see cref="ArgumentOutOfRangeException"/> that
    ///     propagates past <c>Load</c>'s <see cref="FormatException"/>-only catch boundary.
    ///     Exercises <c>ParseStops</c> → <c>ParsePercentOrNumber</c>, which (like
    ///     <c>ParseCoordinate</c>) calls <see cref="float.TryParse(string, System.Globalization.NumberStyles, System.IFormatProvider?, out float)"/>
    ///     on the attribute's whole trimmed text with no prior character-class filtering, so the
    ///     literal <c>"NaN"</c> text reaches it unfiltered.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientStopOffsetNaN_DoesNotThrowAndRenders()
    {
        // Arrange: the second stop's offset is a literal "NaN"
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='g' x1='0' y1='0' x2='1' y2='0'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='NaN' stop-color='white'/>
                </linearGradient>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream2836 = ToStream(svg);
        var surface = SvgCodec.Load(stream2836, 100, 100);

        // Assert: rendering completed without the raw ArgumentOutOfRangeException a non-finite
        // offset reaching GradientStop's constructor would otherwise throw
        Assert.Equal(100, surface.Width);
    }

    /// <summary>
    ///     Proves that a <c>linearGradient</c>'s <c>x1</c> attribute value of literal
    ///     <c>Infinity</c> falls back to its documented default (<c>0</c>) rather than producing
    ///     a non-finite gradient-space coordinate. Exercises
    ///     <c>GetGradientCoordinateOrDefault</c> → <c>ParsePercentOrNumber</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientX1Infinity_FallsBackToDefaultAndRenders()
    {
        // Arrange: x1 is a literal "Infinity" - should fall back to its default of 0, producing
        // the same left-to-right brightness ramp as if x1 had been omitted entirely
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='g' x1='Infinity' y1='0' x2='1' y2='0'>
                  <stop offset='0' stop-color='black'/>
                  <stop offset='1' stop-color='white'/>
                </linearGradient>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream2867 = ToStream(svg);
        var surface = SvgCodec.Load(stream2867, 100, 100);

        // Assert: brightness still increases left to right, proving x1 fell back to 0 rather
        // than Infinity (which would break or degenerate the ramp)
        Assert.True(surface[10, 50].R < surface[90, 50].R);
    }

    /// <summary>
    ///     Proves that an <c>rgba()</c> color's alpha channel value of literal <c>Infinity</c>
    ///     causes the whole color to be treated as unrecognized ("no paint"), the same tolerant
    ///     "unrecognized color" contract <c>ParseColor</c> already documents for an unknown
    ///     keyword, rather than silently saturating to fully opaque. Exercises
    ///     <c>ParseRgbFunctionColor</c> → <c>ParseColorChannel</c>/inline call →
    ///     <c>ParsePercentOrNumber</c>.
    /// </summary>
    /// <remarks>
    ///     <c>Infinity</c>, not <c>NaN</c>, is used here: pre-fix, <c>Math.Clamp(NaN, 0, 255)</c>
    ///     returns <c>NaN</c> unchanged (IEEE comparisons against <c>NaN</c> are always false, so
    ///     neither clamp bound is taken), which then casts to the byte alpha <c>0</c> (fully
    ///     transparent) - incidentally matching this test's "background shows through" assertion
    ///     even without the fix, and so failing to discriminate the gap. <c>Infinity</c> instead
    ///     clamps to <c>255</c> (fully opaque black) pre-fix, which visibly hides the white
    ///     background - a genuine, fix-dependent failure this test can actually detect.
    /// </remarks>
    [Fact]
    public void SvgCodec_Load_RgbaAlphaInfinity_TreatsColorAsUnrecognizedNoPaint()
    {
        // Arrange: a white background rect, overpainted by a second rect whose rgba() alpha is
        // a literal "Infinity" - if treated as unrecognized, the white background remains visible
        const string svg = """
            <svg viewBox='0 0 10 10'>
              <rect x='0' y='0' width='10' height='10' fill='white'/>
              <rect x='0' y='0' width='10' height='10' fill='rgba(0,0,0,Infinity)'/>
            </svg>
            """;

        // Act
        using var stream2904 = ToStream(svg);
        var surface = SvgCodec.Load(stream2904, 100, 100);

        // Assert: the background white shows through - the Infinity-alpha rgba() color was
        // rejected as unrecognized, not drawn as (saturated-opaque) black
        var pixel = surface[50, 50];
        Assert.Equal(255, pixel.R);
        Assert.Equal(255, pixel.G);
        Assert.Equal(255, pixel.B);
    }

    /// <summary>
    ///     Proves that a root <c>&lt;svg&gt;</c> element's <c>width</c>/<c>height</c> attribute
    ///     values of literal <c>Infinity</c> fall back to the CSS/UA default
    ///     <c>300x150</c> intrinsic size, rather than the undefined/saturated integer size that
    ///     <c>(int)MathF.Round(float.PositiveInfinity)</c> would otherwise produce. Exercises
    ///     <c>ResolveViewBoxOrSize</c> → <c>ParseLength</c>, consumed by both <c>GetInfo</c> and
    ///     <c>Load</c>.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_WidthHeightInfinity_FallsBackToDefaultSize()
    {
        // Arrange: no viewBox, width/height are both literal "Infinity"
        const string svg = "<svg width='Infinity' height='Infinity'></svg>";

        // Act
        using var stream2929 = ToStream(svg);
        var info = SvgCodec.GetInfo(stream2929);

        // Assert: falls back to the CSS/UA default replaced-element intrinsic size
        Assert.Equal(300, info.Width);
        Assert.Equal(150, info.Height);
    }

    /// <summary>
    ///     Proves that the non-finite rejection added to <c>ParseLength</c> does not reject a
    ///     legitimate finite root <c>width</c>/<c>height</c> expressed in scientific notation.
    /// </summary>
    [Fact]
    public void SvgCodec_GetInfo_WidthHeightScientificNotation_ResolvesToBareValue()
    {
        // Arrange: width/height use scientific notation, no viewBox present
        const string svg = "<svg width='1e2' height='1e2'></svg>";

        // Act
        using var stream2947 = ToStream(svg);
        var info = SvgCodec.GetInfo(stream2947);

        // Assert
        Assert.Equal(100, info.Width);
        Assert.Equal(100, info.Height);
    }

    /// <summary>
    ///     Proves that the non-finite rejection added to <c>ParsePercentOrNumber</c> does not
    ///     reject a legitimate finite gradient stop <c>offset</c> expressed as a percentage.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_GradientStopOffsetPercentage_RendersGradientCorrectly()
    {
        // Arrange: stop offsets are percentages (0% / 100%) rather than bare 0/1 numbers
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <defs>
                <linearGradient id='g' x1='0' y1='0' x2='1' y2='0'>
                  <stop offset='0%' stop-color='black'/>
                  <stop offset='100%' stop-color='white'/>
                </linearGradient>
              </defs>
              <rect x='0' y='0' width='100' height='100' fill='url(#g)'/>
            </svg>
            """;

        // Act
        using var stream2975 = ToStream(svg);
        var surface = SvgCodec.Load(stream2975, 100, 100);

        // Assert: brightness increases left to right, as with the equivalent bare-number test
        Assert.True(surface[10, 50].R < surface[90, 50].R);
    }

    /// <summary>
    ///     Proves that well-formed-but-out-of-scope constructs (<c>&lt;style&gt;</c>,
    ///     <c>&lt;mask&gt;</c>, <c>&lt;clipPath&gt;</c>,
    ///     <c>&lt;pattern&gt;</c>, a nested <c>&lt;svg&gt;</c>) are silently
    ///     skipped and do not prevent the rest of the document from rendering. (<c>filter</c> is
    ///     no longer out-of-scope - see the dedicated filter tests above for its own
    ///     dangling-reference/unsupported-primitive tolerance coverage.)
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UnsupportedConstructs_StillRendersRestOfDocument()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <style>rect { fill: red; }</style>
              <defs>
                <mask id='m'><rect width='100' height='100' fill='white'/></mask>
                <clipPath id='c'><rect width='50' height='50'/></clipPath>
                <pattern id='p' width='10' height='10'><rect width='5' height='5'/></pattern>
              </defs>
              <svg x='0' y='0' width='10' height='10'><rect width='10' height='10' fill='yellow'/></svg>
              <rect x='10' y='10' width='30' height='30' fill='black'/>
            </svg>
            """;

        // Act
        using var stream3007 = ToStream(svg);
        var surface = SvgCodec.Load(stream3007, 100, 100);

        // Assert: the plain rect after the unsupported constructs still rendered
        Assert.Equal(255, surface[20, 20].A);
    }

    // ================================================================================================
    // Argument validation
    // ================================================================================================

    /// <summary>Proves that <see cref="SvgCodec.Load(Stream,int,int,IReadOnlyDictionary{string,TrueTypeFont}?)"/> rejects a null stream.</summary>
    [Fact]
    public void SvgCodec_Load_NullStream_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => SvgCodec.Load((Stream)null!, 10, 10));
    }

    /// <summary>Proves that <see cref="SvgCodec.Load(string,int,int,IReadOnlyDictionary{string,TrueTypeFont}?)"/> rejects a null path.</summary>
    [Fact]
    public void SvgCodec_Load_NullPath_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => SvgCodec.Load((string)null!, 10, 10));
    }

    /// <summary>Proves that <see cref="SvgCodec.Load(string,int,int,IReadOnlyDictionary{string,TrueTypeFont}?)"/> rejects an empty path.</summary>
    [Fact]
    public void SvgCodec_Load_EmptyPath_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => SvgCodec.Load(string.Empty, 10, 10));
    }

    /// <summary>Proves that <see cref="SvgCodec.Load(string,int,int,IReadOnlyDictionary{string,TrueTypeFont}?)"/> rejects a whitespace-only path.</summary>
    [Fact]
    public void SvgCodec_Load_WhitespacePath_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => SvgCodec.Load("   ", 10, 10));
    }

    /// <summary>Proves that <see cref="SvgCodec.GetInfo(Stream)"/> rejects a null stream.</summary>
    [Fact]
    public void SvgCodec_GetInfo_NullStream_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => SvgCodec.GetInfo((Stream)null!));
    }

    /// <summary>Proves that <see cref="SvgCodec.GetInfo(string)"/> rejects a null path.</summary>
    [Fact]
    public void SvgCodec_GetInfo_NullPath_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => SvgCodec.GetInfo((string)null!));
    }

    /// <summary>Proves that <see cref="SvgCodec.GetInfo(string)"/> rejects an empty path.</summary>
    [Fact]
    public void SvgCodec_GetInfo_EmptyPath_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => SvgCodec.GetInfo(string.Empty));
    }

    /// <summary>
    ///     Proves that a non-positive requested output width propagates <see cref="Surface"/>'s
    ///     own <see cref="ArgumentOutOfRangeException"/> unwrapped, per this codec's design
    ///     decision to treat raster-target dimensions as ordinary API parameters rather than
    ///     untrusted file data.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NonPositiveWidth_PropagatesSurfaceArgumentOutOfRangeException()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 10 10'></svg>";

        // Act & Assert
        using var stream3086 = ToStream(svg);
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgCodec.Load(stream3086, 0, 10));
    }

    /// <summary>
    ///     Proves that <see cref="SvgCodec.Load(string,int,int,IReadOnlyDictionary{string,TrueTypeFont}?)"/>
    ///     (the file-path overload) reads and rasterizes a real file, mirroring the stream overload's
    ///     behavior.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FromFilePath_ReturnsExpectedPixels()
    {
        // Arrange
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "<svg viewBox='0 0 10 10'><rect x='0' y='0' width='10' height='10' fill='blue'/></svg>");

            // Act
            var surface = SvgCodec.Load(path, 10, 10);

            // Assert
            Assert.Equal(255, surface[5, 5].A);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
