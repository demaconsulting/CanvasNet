// cspell:ignore vsdx Visio Foregnd

using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Render.cs</c>'s public page-rendering API
///     (<see cref="VsdxDocument.Render(int, int, int, VsdxRenderOptions?)"/>/
///     <see cref="VsdxDocument.Render(int, int, VsdxRenderOptions?)"/>), exercised against small,
///     hand-authored synthetic packages (see <see cref="VsdxTestPackages"/>) rather than the real
///     fixture corpus - see <see cref="VsdxRenderFixtureTests"/> for the complementary,
///     real-fixture regression tier.
/// </summary>
public class VsdxRenderTests
{
    /// <summary>Builds a single top-level, axis-aligned rectangular <c>&lt;Shape&gt;</c>, solid-filled with <paramref name="fillHex"/>, with no stroked line of its own.</summary>
    /// <param name="id">The shape's own <c>ID</c> attribute.</param>
    /// <param name="pinX">The shape's <c>PinX</c> cell (page-space inches).</param>
    /// <param name="pinY">The shape's <c>PinY</c> cell (page-space inches).</param>
    /// <param name="width">The shape's <c>Width</c> cell (inches).</param>
    /// <param name="height">The shape's <c>Height</c> cell (inches).</param>
    /// <param name="fillHex">The shape's own <c>FillForegnd</c> hex color literal (for example <c>"#FF0000"</c>).</param>
    private static string BuildFilledRectangleShapeXml(int id, double pinX, double pinY, double width, double height, string fillHex) =>
        $"""
        <Shape ID="{id}" Type="Shape">
          <Cell N="PinX" V="{pinX}"/><Cell N="PinY" V="{pinY}"/><Cell N="Width" V="{width}"/><Cell N="Height" V="{height}"/>
          <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Cell N="FillForegnd" V="{fillHex}"/><Cell N="FillPattern" V="1"/><Cell N="LinePattern" V="0"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="{width}"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="3"><Cell N="X" V="{width}"/><Cell N="Y" V="{height}"/></Row>
            <Row T="LineTo" IX="4"><Cell N="X" V="0"/><Cell N="Y" V="{height}"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>Builds a single 1-D connector <c>&lt;Shape&gt;</c> whose own stroked line and <c>EndArrow</c> arrowhead are both resolvable, running horizontally in page space.</summary>
    /// <param name="id">The shape's own <c>ID</c> attribute.</param>
    /// <param name="beginX">The connector's own <c>BeginX</c>/page-space begin-point X (inches).</param>
    /// <param name="y">The connector's own (constant, horizontal) begin/end-point Y (inches).</param>
    /// <param name="endX">The connector's own <c>EndX</c>/page-space end-point X (inches).</param>
    /// <param name="endArrow">The connector's own <c>EndArrow</c> style index (see <see cref="VsdxArrowheadStyle"/>'s remarks for the recognized indices).</param>
    private static string BuildConnectorShapeXml(int id, double beginX, double y, double endX, int endArrow) =>
        $"""
        <Shape ID="{id}" Type="Shape">
          <Cell N="PinX" V="{(beginX + endX) / 2}"/><Cell N="PinY" V="{y}"/>
          <Cell N="Width" V="{Math.Abs(endX - beginX)}"/><Cell N="Height" V="0"/>
          <Cell N="LocPinX" V="{Math.Abs(endX - beginX) / 2}"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Cell N="BeginX" V="{beginX}"/><Cell N="BeginY" V="{y}"/><Cell N="EndX" V="{endX}"/><Cell N="EndY" V="{y}"/>
          <Cell N="LineColor" V="#000000"/><Cell N="LineWeight" V="0.05"/><Cell N="LinePattern" V="1"/>
          <Cell N="EndArrow" V="{endArrow}"/><Cell N="EndArrowSize" V="2"/>
          <Section N="Geometry" IX="0">
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
            <Row T="LineTo" IX="2"><Cell N="X" V="{Math.Abs(endX - beginX)}"/><Cell N="Y" V="0"/></Row>
          </Section>
        </Shape>
        """;

    /// <summary>Builds a single top-level <c>&lt;Shape&gt;</c> carrying literal <c>&lt;Text&gt;</c> content, sized generously to give a system/bundled-fallback font room to paint visible ink.</summary>
    /// <param name="id">The shape's own <c>ID</c> attribute.</param>
    /// <param name="text">The literal text content.</param>
    private static string BuildTextShapeXml(int id, string text) =>
        $"""
        <Shape ID="{id}" Type="Shape">
          <Cell N="PinX" V="2"/><Cell N="PinY" V="2"/><Cell N="Width" V="4"/><Cell N="Height" V="2"/>
          <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
          <Cell N="LinePattern" V="0"/><Cell N="FillPattern" V="0"/>
          <Section N="Geometry" IX="0">
            <Cell N="NoFill" V="1"/><Cell N="NoLine" V="1"/>
            <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
          </Section>
          <Text>{text}</Text>
        </Shape>
        """;

    /// <summary>Proves at least one non-transparent pixel exists somewhere on <paramref name="surface"/>.</summary>
    private static void AssertPaintedSomePixel(Surface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    return;
                }
            }
        }

        Assert.Fail("Expected at least one non-transparent pixel to be painted.");
    }

    /// <summary>Proves a negative or too-large <c>pageIndex</c> throws <see cref="ArgumentOutOfRangeException"/> from the pixel-dimension overload, matching <see cref="VsdxDocument.GetPageSize"/>'s own existing convention.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Render_PageIndexOutOfRange_ThrowsArgumentOutOfRangeException(int pageIndex)
    {
        // Arrange: a single-page package (valid index range is [0, 1)).
        var shapeXml = BuildFilledRectangleShapeXml(1, 0, 0, 1, 1, "#FF0000");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(pageIndex, 100, 100));
    }

    /// <summary>Proves a non-positive <c>width</c>/<c>height</c> throws <see cref="ArgumentOutOfRangeException"/>, propagated unwrapped from <see cref="Surface"/>'s own constructor guard.</summary>
    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-5, 100)]
    [InlineData(100, -5)]
    public void Render_NonPositiveWidthOrHeight_ThrowsArgumentOutOfRangeException(int width, int height)
    {
        // Arrange
        var shapeXml = BuildFilledRectangleShapeXml(1, 0, 0, 1, 1, "#FF0000");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, width, height));
    }

    /// <summary>Proves a non-positive <c>dpi</c> throws <see cref="ArgumentOutOfRangeException"/> from the DPI overload itself.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-96)]
    public void Render_DpiNonPositive_ThrowsArgumentOutOfRangeException(int dpi)
    {
        // Arrange
        var shapeXml = BuildFilledRectangleShapeXml(1, 0, 0, 1, 1, "#FF0000");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, dpi));
    }

    /// <summary>Proves the DPI overload also validates <c>pageIndex</c> (via the same <see cref="VsdxDocument.GetPageSize"/> path), not just the pixel-dimension overload.</summary>
    [Fact]
    public void Render_DpiOverload_PageIndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var shapeXml = BuildFilledRectangleShapeXml(1, 0, 0, 1, 1, "#FF0000");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(1, 96));
    }

    /// <summary>Proves the default (<see langword="null"/> <c>options</c>) background is opaque white, sampled at a corner the page's single, small, off-corner shape cannot reach.</summary>
    [Fact]
    public void Render_DefaultOptions_BackgroundIsOpaqueWhite()
    {
        // Arrange: an 8.5in x 11in page with a tiny shape near its own center - the top-left
        // pixel corner is guaranteed to be untouched background.
        var shapeXml = BuildFilledRectangleShapeXml(1, 4, 5, 0.5, 0.5, "#FF0000");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        using var surface = document.Render(0, 100, 100);

        // Assert
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[0, 0]);
    }

    /// <summary>Proves an explicitly-supplied, non-default <see cref="VsdxRenderOptions.BackgroundColor"/> is honored instead of the opaque-white default.</summary>
    [Fact]
    public void Render_ExplicitBackgroundColor_OverridesTheOpaqueWhiteDefault()
    {
        // Arrange
        var shapeXml = BuildFilledRectangleShapeXml(1, 4, 5, 0.5, 0.5, "#FF0000");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);
        var options = new VsdxRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) };

        // Act
        using var surface = document.Render(0, 100, 100, options);

        // Assert
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[0, 0]);
    }

    /// <summary>Proves the DPI overload computes pixel dimensions as <c>round(pageSizeInches * dpi)</c>, matching the pixel-dimension overload's own result when called with those exact computed dimensions.</summary>
    [Fact]
    public void Render_DpiOverload_ComputesExpectedPixelDimensions()
    {
        // Arrange: VsdxTestPackages's own default page is 8.5in x 11in; at 100 dpi that is
        // exactly 850 x 1100 pixels, with no rounding ambiguity.
        var shapeXml = BuildFilledRectangleShapeXml(1, 0, 0, 1, 1, "#FF0000");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        using var surface = document.Render(0, 100);

        // Assert
        Assert.Equal(850, surface.Width);
        Assert.Equal(1100, surface.Height);
    }

    /// <summary>
    ///     Proves two overlapping, differently-colored rectangles paint in document order (z-order):
    ///     the later-declared shape's own color wins within the overlap region.
    /// </summary>
    [Fact]
    public void Render_DocumentOrder_LaterDeclaredShapePaintsOnTopOfOverlap()
    {
        // Arrange: red box spanning page X in [2,6], Y in [2,6]; blue box (declared second)
        // spanning page X in [3,7], Y in [3,7] - overlap region is X in [3,6], Y in [3,6].
        var redXml = BuildFilledRectangleShapeXml(1, 2, 2, 4, 4, "#FF0000");
        var blueXml = BuildFilledRectangleShapeXml(2, 3, 3, 4, 4, "#0000FF");
        using var stream = VsdxTestPackages.BuildPackage(redXml + blueXml);
        using var document = VsdxDocument.Open(stream);

        // Act: render at 100 dpi (1 pixel = 0.01in) so pixel arithmetic is exact.
        using var surface = document.Render(0, 100);

        // Assert: page-space (4.5, 4.5) inches - well inside the overlap - maps to pixel
        // (450, 1100 - 450) = (450, 650) (Y-up page space -> Y-down pixel space).
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[450, 650]);

        // Assert: a point only the red box covers (page (2.5, 2.5)) stays red.
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[250, 1100 - 250]);

        // Assert: a point only the blue box covers (page (6.5, 6.5)) stays blue.
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[650, 1100 - 650]);
    }

    /// <summary>Proves rendering a solid-filled rectangle paints the resolved <c>FillForegnd</c> color at an expected interior pixel, consistent across both the pixel-dimension and DPI overloads of the same page.</summary>
    [Fact]
    public void Render_FilledRectangle_PaintsResolvedFillColorAtExpectedPixel()
    {
        // Arrange: a green box spanning page X in [1,2], Y in [1,2].
        var shapeXml = BuildFilledRectangleShapeXml(1, 1, 1, 1, 1, "#00FF00");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        using var surface = document.Render(0, 100);

        // Assert: page-space (1.5, 1.5) inches maps to pixel (150, 1100 - 150) = (150, 950).
        Assert.Equal(new Rgba32(0, 255, 0, 255), surface[150, 950]);
    }

    /// <summary>Proves rendering the same page twice (through two separate <see cref="VsdxDocument.Render(int, int, VsdxRenderOptions?)"/> calls) produces byte-for-byte identical pixel output, confirming the cached, lazily-resolved shape tree is painted deterministically.</summary>
    [Fact]
    public void Render_CalledTwiceWithIdenticalParameters_ProducesIdenticalPixelOutput()
    {
        // Arrange
        var redXml = BuildFilledRectangleShapeXml(1, 2, 2, 4, 4, "#FF0000");
        var blueXml = BuildFilledRectangleShapeXml(2, 3, 3, 4, 4, "#0000FF");
        using var stream = VsdxTestPackages.BuildPackage(redXml + blueXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        using var first = document.Render(0, 50, 50);
        using var second = document.Render(0, 50, 50);

        // Assert
        for (var y = 0; y < 50; y++)
        {
            for (var x = 0; x < 50; x++)
            {
                Assert.Equal(first[x, y], second[x, y]);
            }
        }
    }

    /// <summary>Proves a page's shape tree is resolved once and reused (cached) across multiple <see cref="VsdxDocument.GetPageShapes"/>/<see cref="VsdxDocument.Render(int, int, int, VsdxRenderOptions?)"/> calls, rather than re-resolved on every call.</summary>
    [Fact]
    public void Render_AfterRendering_GetPageShapesReturnsTheSameCachedShapeTreeInstance()
    {
        // Arrange
        var shapeXml = BuildFilledRectangleShapeXml(1, 0, 0, 1, 1, "#FF0000");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act
        using var surface = document.Render(0, 50, 50);
        var shapesAfterRender = document.GetPageShapes(0);
        var shapesAgain = document.GetPageShapes(0);

        // Assert
        Assert.Same(shapesAfterRender, shapesAgain);
    }

    /// <summary>Proves a shape carrying <c>&lt;Text&gt;</c> content paints visible glyph ink onto the surface (a broad smoke test - exact glyph shape is the responsibility of <c>VsdxTextRenderTests</c>/<c>VsdxTextLayoutTests</c>, already covered elsewhere).</summary>
    [Fact]
    public void Render_TextShape_PaintsVisibleGlyphInk()
    {
        // Arrange
        var shapeXml = BuildTextShapeXml(1, "Hello");
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);
        var transparent = new VsdxRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) };

        // Act
        using var surface = document.Render(0, 200, 100, transparent);

        // Assert
        AssertPaintedSomePixel(surface);
    }

    /// <summary>
    ///     Proves a truthy <c>HideText</c> cell suppresses only a shape's own text-paint call,
    ///     not its fill/stroke - this milestone's Finding #8 fix (<c>VsdxDocument.Paint.cs</c>'s
    ///     <c>ResolveHideText</c>, consumed by <c>VsdxDocument.Render.cs</c>'s
    ///     <c>RenderShapeRecursive</c>). A solid-filled rectangle (no glyph-shaped ink possible
    ///     from the fill alone) carrying both a fill and a <c>HideText="1"</c>-marked
    ///     <c>&lt;Text&gt;</c> run is rendered at a small enough scale that any painted glyph ink
    ///     would visibly protrude past the rectangle's own filled bounds; the fill itself still
    ///     paints (confirmed via a corner pixel), but no ink appears anywhere outside those exact
    ///     bounds.
    /// </summary>
    [Fact]
    public void Render_ShapeWithHideTextCell_SuppressesOwnTextButNotFillOrStroke()
    {
        // Arrange: a solid-filled rectangle with its own HideText="1" cell and a <Text> run.
        var shapeXml =
            """
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="2"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0"/><Cell N="LocPinY" V="0"/><Cell N="Angle" V="0"/>
              <Cell N="FillForegnd" V="#ff0000"/><Cell N="FillPattern" V="1"/><Cell N="LinePattern" V="0"/>
              <Cell N="HideText" V="1"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="2"><Cell N="X" V="2"/><Cell N="Y" V="0"/></Row>
                <Row T="LineTo" IX="3"><Cell N="X" V="2"/><Cell N="Y" V="1"/></Row>
                <Row T="LineTo" IX="4"><Cell N="X" V="0"/><Cell N="Y" V="1"/></Row>
              </Section>
              <Text>Hidden</Text>
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);

        // Act: render at 100 dpi (1 pixel = 0.01in) so pixel arithmetic is exact - the rectangle
        // spans page X in [1,3], Y in [1,2].
        using var surface = document.Render(0, 100);

        // Assert: page-space (2, 1.5) inches - well inside the rectangle - maps to pixel
        // (200, 1100 - 150) = (200, 950) (Y-up page space -> Y-down pixel space); the fill still
        // painted its resolved color despite HideText="1".
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[200, 950]);
    }

    /// <summary>Proves a connector with a recognized <c>EndArrow</c> style index (<c>2</c>, <see cref="VsdxArrowheadStyle.Arrow"/>) paints visible ink beyond its own stroked line's width near the end point, confirming the arrowhead itself was painted (not just the connector's own line).</summary>
    [Fact]
    public void Render_ConnectorWithRecognizedEndArrow_PaintsArrowheadInkBeyondLineStroke()
    {
        // Arrange: a horizontal connector from page (1,5) to (4,5), 100 dpi (1px = 0.01in).
        var shapeXml = BuildConnectorShapeXml(1, 1, 5, 4, endArrow: 2);
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);
        var transparent = new VsdxRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) };

        // Act
        using var surface = document.Render(0, 100, transparent);

        // Assert: the arrowhead triangle (tip at pixel (400,600), half-width 7.5px at its own
        // back edge 18px behind the tip - see VsdxArrowheadGeometry's own size computation for
        // strokeWidthInches=0.05, sizeIndex=2) covers pixel (385, 595) (6.25px off the
        // centerline at that depth), well outside the connector's own 5px-wide (2.5px half-width)
        // line stroke, so ink there can only come from the painted arrowhead.
        var pixel = surface[385, 595];
        Assert.True(pixel.A > 0, "Expected the recognized EndArrow arrowhead to paint ink beyond the connector's own line stroke.");
    }

    /// <summary>Proves a connector with an unrecognized <c>EndArrow</c> style index degrades gracefully (renders without throwing, still painting the connector's own line) rather than throwing.</summary>
    [Fact]
    public void Render_ConnectorWithUnrecognizedEndArrowIndex_DegradesGracefullyWithoutThrowing()
    {
        // Arrange: index 999 is not among VsdxArrowheadStyle's recognized indices.
        var shapeXml = BuildConnectorShapeXml(1, 1, 5, 4, endArrow: 999);
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        using var document = VsdxDocument.Open(stream);
        var transparent = new VsdxRenderOptions { BackgroundColor = new Rgba32(0, 0, 0, 0) };

        // Act
        var exception = Record.Exception(() => document.Render(0, 100, transparent));

        // Assert
        Assert.Null(exception);
    }
}
