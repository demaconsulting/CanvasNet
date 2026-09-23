// cspell:ignore Sfnt sfnt glyf cmap notdef codepoint
// cspell:ignore Dasharray hhea Hhea hmtx Hmtx hrefs letterboxed Loca Maxp unstroked
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
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

        return TrueTypeFont.Load(new MemoryStream(data));
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
        var surface = SvgCodec.Load(ToStream(svg), 20, 20);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
    ///     Proves that <c>opacity</c> multiplies into a solid fill color's alpha rather than
    ///     leaving it fully opaque.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_Opacity_MultipliesIntoFillAlpha()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 100 100'><rect x='10' y='10' width='30' height='30' fill='black' opacity='0.4'/></svg>";

        // Act
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

        // Assert: the center is brighter than a point near the shape's edge
        Assert.True(surface[50, 50].R > surface[95, 50].R);
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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 100, 100));
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
        var surface = SvgCodec.Load(ToStream(svg), 10, 10);

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
        var surface = SvgCodec.Load(ToStream(svg), 10, 10);

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
        var surface = SvgCodec.Load(ToStream(svg), 20, 20);

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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 10, 10));
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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(builder.ToString()), 10, 10));
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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 10, 10));
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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100, fonts);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100, fonts);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100, fonts);

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
        var surface = SvgCodec.Load(ToStream(svg), 200, 100, fonts);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100, fonts);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100, fonts);

        // Assert: the glyph rendered, proving the fallback list was walked to "TestFont"
        Assert.Equal(255, surface[35, 35].A);
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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

        // Assert: content band is filled
        Assert.Equal(255, surface[50, 50].A);
        // Assert: left and right letterbox bars are transparent
        Assert.Equal(0, surface[5, 50].A);
        Assert.Equal(0, surface[95, 50].A);
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
        var info = SvgCodec.GetInfo(ToStream(svg));

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
        var info = SvgCodec.GetInfo(ToStream(svg));

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
        var info = SvgCodec.GetInfo(ToStream(svg));

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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 100, 100));
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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 100, 100));
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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 100, 100));
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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 100, 100));
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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 100, 100));
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
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(ToStream(svg), 100, 100));
    }

    /// <summary>
    ///     Proves that well-formed-but-out-of-scope constructs (<c>&lt;style&gt;</c>,
    ///     <c>&lt;filter&gt;</c>, <c>&lt;mask&gt;</c>, <c>&lt;clipPath&gt;</c>,
    ///     <c>&lt;pattern&gt;</c>, <c>&lt;marker&gt;</c>, a nested <c>&lt;svg&gt;</c>) are silently
    ///     skipped and do not prevent the rest of the document from rendering.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_UnsupportedConstructs_StillRendersRestOfDocument()
    {
        // Arrange
        const string svg = """
            <svg viewBox='0 0 100 100'>
              <style>rect { fill: red; }</style>
              <defs>
                <filter id='f'><feGaussianBlur stdDeviation='2'/></filter>
                <mask id='m'><rect width='100' height='100' fill='white'/></mask>
                <clipPath id='c'><rect width='50' height='50'/></clipPath>
                <pattern id='p' width='10' height='10'><rect width='5' height='5'/></pattern>
                <marker id='mk'><circle r='2'/></marker>
              </defs>
              <svg x='0' y='0' width='10' height='10'><rect width='10' height='10' fill='yellow'/></svg>
              <rect x='10' y='10' width='30' height='30' fill='black'/>
            </svg>
            """;

        // Act
        var surface = SvgCodec.Load(ToStream(svg), 100, 100);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => SvgCodec.Load(ToStream(svg), 0, 10));
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
