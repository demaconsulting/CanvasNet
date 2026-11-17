using System.Numerics;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Unit tests for arrows drawn on SVG connectors: the vertices and <c>orient="auto"</c> angles the codec places
///     markers at (including the closing vertex of a closed subpath), and the <c>svg-arrowhead-shape</c> authoring hint
///     reported through <see cref="SvgLoadOptions.HintHandler"/>.
/// </summary>
public class SvgArrowTests
{
    /// <summary>The tolerance, in degrees, of the angle assertions.</summary>
    private const float AngleTolerance = 0.01f;

    /// <summary>Wraps <paramref name="svg"/> as a UTF-8 stream for <see cref="SvgCodec"/> to read.</summary>
    /// <param name="svg">The SVG text.</param>
    /// <returns>The stream.</returns>
    private static MemoryStream ToStream(string svg) => new(Encoding.UTF8.GetBytes(svg));

    /// <summary>Renders <paramref name="svg"/> and returns the hints it reported.</summary>
    /// <param name="svg">The SVG text.</param>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    /// <returns>The hints, in the order they were reported.</returns>
    private static List<SvgHint> RenderHints(string svg, int width = 200, int height = 200)
    {
        var hints = new List<SvgHint>();
        using var stream = ToStream(svg);
        using var surface = SvgCodec.LoadWithFontFaces(stream, width, height, null, new SvgLoadOptions { HintHandler = hints.Add });
        return hints;
    }

    /// <summary>Asserts that a vertex is at a position.</summary>
    /// <param name="expected">The expected position.</param>
    /// <param name="vertex">The vertex.</param>
    private static void AssertAt(Vector2 expected, SvgCodec.MarkerVertex vertex)
    {
        Assert.Equal(expected.X, vertex.Position.X, 3);
        Assert.Equal(expected.Y, vertex.Position.Y, 3);
    }

    /// <summary>Asserts the <c>orient="auto"</c> angle of a vertex.</summary>
    /// <param name="expectedDegrees">The expected angle in degrees.</param>
    /// <param name="vertex">The vertex.</param>
    private static void AssertAngle(float expectedDegrees, SvgCodec.MarkerVertex vertex)
    {
        var actual = SvgCodec.ComputeVertexAngleDegrees(vertex);
        Assert.True(MathF.Abs(expectedDegrees - actual) <= AngleTolerance, $"Expected {expectedDegrees} degrees, got {actual}.");
    }

    /// <summary>Asserts that a tangent is present and points in a direction.</summary>
    /// <param name="expected">The expected unit direction.</param>
    /// <param name="actual">The tangent.</param>
    private static void AssertDirection(Vector2 expected, Vector2? actual)
    {
        Assert.True(actual.HasValue, "Expected a tangent.");
        Assert.Equal(expected.X, actual.Value.X, 3);
        Assert.Equal(expected.Y, actual.Value.Y, 3);
    }

    /// <summary>
    ///     Test that an open path has one vertex per point and no closing vertex, so its end marker stays at its last
    ///     point along its last segment.
    /// </summary>
    [Fact]
    public void SvgArrow_BuildMarkerVertices_OpenPath_EndsAtLastPoint()
    {
        // Arrange: an open L-shaped path
        var path = new PathBuilder().MoveTo(new Vector2(0, 0)).LineTo(new Vector2(10, 0)).LineTo(new Vector2(10, 10)).Build();

        // Act
        var vertices = SvgCodec.BuildMarkerVertices(path);

        // Assert: three vertices, the end along the last segment (straight down)
        Assert.Equal(3, vertices.Count);
        AssertAt(new Vector2(10, 10), vertices[^1]);
        AssertAngle(0, vertices[0]);
        AssertAngle(45, vertices[1]);
        AssertAngle(90, vertices[^1]);
    }

    /// <summary>
    ///     Test that a closed triangle (case 9 of the arrow reference drawing) gets a closing vertex at its first point:
    ///     the end marker is there, oriented by bisecting the closing and the first segment, as in SVG 2 and browsers.
    /// </summary>
    [Fact]
    public void SvgArrow_BuildMarkerVertices_ClosedTriangle_EndsAtClosingVertex()
    {
        // Arrange: M30,420 L190,420 L110,520 Z
        var path = new PathBuilder()
            .MoveTo(new Vector2(30, 420)).LineTo(new Vector2(190, 420)).LineTo(new Vector2(110, 520)).Close().Build();

        // Act
        var vertices = SvgCodec.BuildMarkerVertices(path);

        // Assert: start, two mids, and the closing vertex at the first point
        Assert.Equal(4, vertices.Count);
        AssertAt(new Vector2(30, 420), vertices[0]);
        AssertAt(new Vector2(190, 420), vertices[1]);
        AssertAt(new Vector2(110, 520), vertices[2]);
        AssertAt(new Vector2(30, 420), vertices[3]);

        // Assert: the closing segment (-0.6247,-0.7809) and the first segment (1,0) bisect to -64.33 degrees at both ends
        AssertAngle(-64.33f, vertices[0]);
        AssertAngle(64.33f, vertices[1]);
        AssertAngle(180, vertices[2]);
        AssertAngle(-64.33f, vertices[3]);

        // Assert: auto-start-reverse turns the start half a turn, to 115.67 degrees
        var reversed = SvgCodec.ParseMarkerOrient("auto-start-reverse", SvgCodec.ComputeVertexAngleDegrees(vertices[0]), isStartVertex: true);
        Assert.True(MathF.Abs(115.67f - reversed) <= AngleTolerance, $"Expected 115.67 degrees, got {reversed}.");
    }

    /// <summary>
    ///     Test that a closed subpath whose last point already equals its first (a zero-length closing segment) takes the
    ///     direction of the segment before it at the closing vertex.
    /// </summary>
    [Fact]
    public void SvgArrow_BuildMarkerVertices_ZeroLengthClose_UsesThePreviousSegment()
    {
        // Arrange: M0,0 L10,0 L0,10 L0,0 Z - the last LineTo already returns to the start
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(10, 0)).LineTo(new Vector2(0, 10)).LineTo(new Vector2(0, 0)).Close().Build();

        // Act
        var vertices = SvgCodec.BuildMarkerVertices(path);

        // Assert: the closing vertex arrives along (0,-1) (the previous segment) and leaves along (1,0): -45 degrees
        Assert.Equal(5, vertices.Count);
        AssertAt(new Vector2(0, 0), vertices[^1]);
        AssertAngle(-45, vertices[^1]);
        AssertAngle(-45, vertices[0]);
    }

    /// <summary>
    ///     Test that a path of two closed subpaths gets a closing vertex per subpath, and that only the first vertex of
    ///     the whole path is the start and only its last (the second subpath's closing vertex) the end.
    /// </summary>
    [Fact]
    public void SvgArrow_BuildMarkerVertices_TwoClosedSubpaths_CloseEachSubpath()
    {
        // Arrange: M0,0 L10,0 L10,10 Z M20,0 L30,0 L30,10 Z
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(10, 0)).LineTo(new Vector2(10, 10)).Close()
            .MoveTo(new Vector2(20, 0)).LineTo(new Vector2(30, 0)).LineTo(new Vector2(30, 10)).Close()
            .Build();

        // Act
        var vertices = SvgCodec.BuildMarkerVertices(path);

        // Assert: four vertices per subpath; the first subpath's closing vertex is a mid vertex at its own start
        Assert.Equal(8, vertices.Count);
        AssertAt(new Vector2(0, 0), vertices[3]);
        AssertAngle(-67.5f, vertices[3]);
        AssertAt(new Vector2(20, 0), vertices[4]);
        AssertAt(new Vector2(20, 0), vertices[7]);
        AssertAngle(-67.5f, vertices[7]);
    }

    /// <summary>
    ///     Test that a drawing command after closepath without a moveto (a new subpath starting at the closed subpath's
    ///     start) shares the closing vertex: one vertex there, arriving along the closing segment and leaving along the
    ///     new segment.
    /// </summary>
    [Fact]
    public void SvgArrow_BuildMarkerVertices_DrawAfterCloseWithoutMoveTo_SharesTheClosingVertex()
    {
        // Arrange: M10,10 L80,10 L80,60 Z L150,90 - the parser issues the implicit MoveTo(10,10) after Z
        var path = new PathBuilder()
            .MoveTo(new Vector2(10, 10)).LineTo(new Vector2(80, 10)).LineTo(new Vector2(80, 60)).Close()
            .MoveTo(new Vector2(10, 10)).LineTo(new Vector2(150, 90))
            .Build();

        // Act
        var vertices = SvgCodec.BuildMarkerVertices(path);

        // Assert: five vertices - no second vertex at (10,10) for the new subpath's start
        Assert.Equal(5, vertices.Count);
        AssertAt(new Vector2(10, 10), vertices[0]);
        AssertAt(new Vector2(80, 10), vertices[1]);
        AssertAt(new Vector2(80, 60), vertices[2]);
        AssertAt(new Vector2(10, 10), vertices[3]);
        AssertAt(new Vector2(150, 90), vertices[4]);

        // Assert: the shared vertex arrives along the closing segment and leaves along the new segment, and the end
        // arrives along the new segment
        var closing = Vector2.Normalize(new Vector2(10, 10) - new Vector2(80, 60));
        var drawn = Vector2.Normalize(new Vector2(150, 90) - new Vector2(10, 10));
        AssertDirection(closing, vertices[3].IncomingTangent);
        AssertDirection(drawn, vertices[3].OutgoingTangent);
        AssertDirection(drawn, vertices[4].IncomingTangent);
    }

    /// <summary>
    ///     Test that a path drawing after closepath without a moveto renders its new segment from the closed subpath's
    ///     start and puts its end marker at the last point.
    /// </summary>
    [Fact]
    public void SvgArrow_Load_DrawAfterCloseWithoutMoveTo_PlacesMarkerEndAtTheLastPoint()
    {
        // Arrange: a stroked triangle closed with Z, then a line drawn without a moveto, with a red end dot
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 100' width='200' height='100'>
              <defs>
                <marker id='dot' viewBox='0 0 10 10' refX='5' refY='5' markerWidth='4' markerHeight='4'>
                  <circle cx='5' cy='5' r='5' fill='#ff0000'/>
                </marker>
              </defs>
              <path d='M10,10 L80,10 L80,60 Z L150,90' fill='none' stroke='#0000ff' stroke-width='2' marker-end='url(#dot)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        using var surface = SvgCodec.Load(stream, 200, 100);

        // Assert: the new segment from (10,10) to (150,90) is stroked at (115,70), and the end dot is at (150,90)
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[115, 70]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[150, 90]);
    }

    /// <summary>
    ///     Test that case 9 of the arrow reference drawing renders like a browser: the end head (red) sits at the first
    ///     point along the bisector, the reversed start head (blue) is unchanged, and nothing is drawn where the end head
    ///     used to be (the last explicit point).
    /// </summary>
    [Fact]
    public void SvgArrow_Load_ClosedTriangleMarkers_MatchTheBrowserPlacement()
    {
        // Arrange: the closed triangle with an auto end head and an auto-start-reverse start head
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 220 560' width='220' height='560'>
              <defs>
                <marker id='a' viewBox='0 0 10 10' refX='9' refY='5' markerWidth='8' markerHeight='8' orient='auto'>
                  <path d='M0,0 L10,5 L0,10 z' fill='#ff0000'/>
                </marker>
                <marker id='s' viewBox='0 0 10 10' refX='9' refY='5' markerWidth='8' markerHeight='8' orient='auto-start-reverse'>
                  <path d='M0,0 L10,5 L0,10 z' fill='#0000ff'/>
                </marker>
              </defs>
              <path d='M30,420 L190,420 L110,520 Z' fill='none' stroke='#808080' stroke-width='2' marker-start='url(#s)' marker-end='url(#a)'/>
            </svg>
            """;

        // Act
        using var stream = ToStream(svg);
        using var surface = SvgCodec.Load(stream, 220, 560);

        // Assert: the end head's centroid (26,428) is red, the start head's (34,412) blue, and (119,520) is not red
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[26, 428]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[34, 412]);
        Assert.NotEqual(255, surface[119, 520].R);
    }

    /// <summary>
    ///     Test that a filled triangle drawn by hand at the end of a curved connector is reported once, naming the shape.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_TriangleAtCurveEnd_ReportsArrowheadShape()
    {
        // Arrange: an S-curve ending at (100,100) and a triangle on its end
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <path d='M20,50 C60,50 60,100 100,100' fill='none' stroke='#000000' stroke-width='2'/>
              <polygon id='head' points='100,100 90,95 90,105' fill='#000000'/>
            </svg>
            """;

        // Act
        var hints = RenderHints(svg);

        // Assert: one hint, for the polygon
        var hint = Assert.Single(hints);
        Assert.Equal(SvgHint.ArrowheadShapeCode, hint.Code);
        Assert.Equal(1, hint.Count);
        Assert.Contains("<polygon id=\"head\">", hint.Message, StringComparison.Ordinal);
        Assert.Contains("marker", hint.Message, StringComparison.Ordinal);
        Assert.Contains("marker-end with orient=\"auto\"", hint.Message, StringComparison.Ordinal);
        Assert.Contains("marker-start with orient=\"auto-start-reverse\"", hint.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that visibility follows the resolved paint: a connector stroked with a dangling <c>url(#...)</c> is not
    ///     drawn, so a triangle at its end reports nothing, while a connector whose fill is a dangling <c>url(#...)</c> is
    ///     unfilled, so a triangle at its end is reported.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_DanglingPaintReferences_FollowTheResolvedPaint()
    {
        // Arrange: the same connector and head, once with an invisible stroke and once with an invisible fill
        const string invisibleStroke = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <path d='M20,50 C60,50 60,100 100,100' fill='none' stroke='url(#missing)' stroke-width='2'/>
              <polygon points='100,100 90,95 90,105' fill='#000000'/>
            </svg>
            """;
        const string invisibleFill = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <path d='M20,50 C60,50 60,100 100,100' fill='url(#missing)' stroke='#000000' stroke-width='2'/>
              <polygon points='100,100 90,95 90,105' fill='#000000'/>
            </svg>
            """;

        // Act
        var strokeHints = RenderHints(invisibleStroke);
        var fillHints = RenderHints(invisibleFill);

        // Assert
        Assert.Empty(strokeHints);
        Assert.Equal(SvgHint.ArrowheadShapeCode, Assert.Single(fillHints).Code);
    }

    /// <summary>
    ///     Test that a connector stroked with a pattern is visible (a hand-drawn head at its end is reported), while a
    ///     connector stroked with a gradient whose stops are all transparent draws nothing (no hint).
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_PatternAndTransparentGradientStrokes_FollowThePaintPath()
    {
        // Arrange
        const string patterned = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <defs><pattern id='p' width='4' height='4' patternUnits='userSpaceOnUse'><rect width='2' height='4' fill='#000000'/></pattern></defs>
              <path d='M20,50 C60,50 60,100 100,100' fill='none' stroke='url(#p)' stroke-width='2'/>
              <polygon points='100,100 90,95 90,105' fill='#000000'/>
            </svg>
            """;
        const string transparentGradient = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <defs><linearGradient id='g'><stop offset='0' stop-color='#000000' stop-opacity='0'/><stop offset='1' stop-color='#000000' stop-opacity='0'/></linearGradient></defs>
              <path d='M20,50 C60,50 60,100 100,100' fill='none' stroke='url(#g)' stroke-width='2'/>
              <polygon points='100,100 90,95 90,105' fill='#000000'/>
            </svg>
            """;

        // Act
        var patternHints = RenderHints(patterned);
        var gradientHints = RenderHints(transparentGradient);

        // Assert
        Assert.Equal(SvgHint.ArrowheadShapeCode, Assert.Single(patternHints).Code);
        Assert.Empty(gradientHints);
    }

    /// <summary>
    ///     Test that a notched four-point head drawn as a path at the end of a straight line is reported.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_NotchedHeadAtLineEnd_ReportsArrowheadShape()
    {
        // Arrange: a line ending at (100,10) and a notched head on its end
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <line x1='10' y1='10' x2='100' y2='10' stroke='#000000' stroke-width='2'/>
              <path d='M100,10 L88,4 L91,10 L88,16 Z' fill='#000000'/>
            </svg>
            """;

        // Act
        var hints = RenderHints(svg);

        // Assert: one hint, for the path
        var hint = Assert.Single(hints);
        Assert.Equal(SvgHint.ArrowheadShapeCode, hint.Code);
        Assert.Contains("<path>", hint.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that several hand-drawn heads, styled by a CSS class and inside a transformed group, are reported as one
    ///     hint that counts them.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_SeveralStyledHeads_ReportsOneHintWithTheCount()
    {
        // Arrange: two connectors in a scaled group, each with a head colored by a class
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <style>.head { fill: #1e3a8a; } .wire { fill: none; stroke: #1e3a8a; stroke-width: 1; }</style>
              <g transform='translate(10,10) scale(2)'>
                <polyline class='wire' points='0,0 40,0 40,30'/>
                <polygon class='head' points='40,30 37,24 43,24'/>
                <line class='wire' x1='0' y1='60' x2='60' y2='60'/>
                <polygon class='head' points='60,60 54,57 54,63'/>
              </g>
            </svg>
            """;

        // Act
        var hints = RenderHints(svg);

        // Assert: one hint counting both heads, naming the first at its pixel position
        var hint = Assert.Single(hints);
        Assert.Equal(2, hint.Count);
        Assert.Contains("2 filled shapes", hint.Message, StringComparison.Ordinal);
        Assert.Contains("at pixel 90,62", hint.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that connectors with marker heads report nothing: the connector end has a marker, and the marker's own
    ///     triangle is marker content.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_MarkerArrows_ReportNothing()
    {
        // Arrange: a marker head on a curve and on a line
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <defs>
                <marker id='arrow' viewBox='0 0 10 10' refX='10' refY='5' markerWidth='6' markerHeight='6' orient='auto'>
                  <path d='M0,0 L10,5 L0,10 z' fill='#000000'/>
                </marker>
              </defs>
              <path d='M20,50 C60,50 60,100 100,100' fill='none' stroke='#000000' stroke-width='2' marker-end='url(#arrow)'/>
              <line x1='10' y1='150' x2='100' y2='150' stroke='#000000' stroke-width='2' marker-end='url(#arrow)'/>
            </svg>
            """;

        // Act / Assert
        Assert.Empty(RenderHints(svg));
    }

    /// <summary>
    ///     Test that triangles that are not on a connector end report nothing: an icon far from any end, a legend whose
    ///     square swatch sits on the end of its sample line, and a triangle too large for the stroke of the line it
    ///     touches.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_TrianglesAwayFromConnectorEnds_ReportNothing()
    {
        // Arrange: a warning icon away from the line, a legend with a square swatch, and a large triangle by a thin line
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <line x1='10' y1='10' x2='50' y2='10' stroke='#000000' stroke-width='2'/>
              <polygon points='150,140 160,160 140,160' fill='#f59e0b'/>
              <line x1='10' y1='60' x2='30' y2='60' stroke='#ea580c' stroke-width='2'/>
              <polygon points='30,55 40,55 40,65 30,65' fill='#ea580c'/>
              <line x1='10' y1='120' x2='60' y2='120' stroke='#000000' stroke-width='1'/>
              <polygon points='60,100 120,120 60,140' fill='#cbd5e1'/>
            </svg>
            """;

        // Act / Assert
        Assert.Empty(RenderHints(svg));
    }

    /// <summary>
    ///     Test that chart symbols centered on the ends of a data line (a triangle whose centroid is the data point)
    ///     report nothing: an arrowhead's centroid lies back along the connector from its end.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_ChartSymbolsCenteredOnLineEnds_ReportNothing()
    {
        // Arrange: a data polyline with triangle symbols centered on its first and last points
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 200 200'>
              <polyline points='20,80 60,40 100,60' fill='none' stroke='#000000' stroke-width='2'/>
              <polygon points='20,74 25.2,83 14.8,83' fill='#000000'/>
              <polygon points='100,54 105.2,63 94.8,63' fill='#000000'/>
            </svg>
            """;

        // Act / Assert
        Assert.Empty(RenderHints(svg));
    }

    /// <summary>
    ///     Test that a connector and head that are only templates - in <c>defs</c> drawn through <c>use</c>, in a
    ///     <c>symbol</c>, or in a <c>pattern</c> - report nothing.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_TemplatesAndPatternContent_ReportNothing()
    {
        // Arrange: the same hand-drawn arrow as a defs group drawn by use, as a symbol, and as pattern content
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' viewBox='0 0 200 200'>
              <defs>
                <g id='drawn'>
                  <line x1='10' y1='10' x2='100' y2='10' stroke='#000000' stroke-width='2'/>
                  <polygon points='100,10 90,5 90,15' fill='#000000'/>
                </g>
                <pattern id='tile' width='120' height='20' patternUnits='userSpaceOnUse'>
                  <line x1='0' y1='10' x2='100' y2='10' stroke='#000000' stroke-width='2'/>
                  <polygon points='100,10 90,5 90,15' fill='#000000'/>
                </pattern>
              </defs>
              <use href='#drawn' y='20'/>
              <symbol id='icon'>
                <line x1='10' y1='60' x2='100' y2='60' stroke='#000000' stroke-width='2'/>
                <polygon points='100,60 90,55 90,65' fill='#000000'/>
              </symbol>
              <rect x='0' y='100' width='200' height='60' fill='url(#tile)'/>
            </svg>
            """;

        // Act / Assert
        Assert.Empty(RenderHints(svg));
    }

    /// <summary>
    ///     Test that the hint analysis never changes the pixels: a document with a hand-drawn head renders the same with
    ///     and without a hint handler.
    /// </summary>
    [Fact]
    public void SvgArrow_HintHandler_DoesNotChangeThePixels()
    {
        // Arrange
        const string svg = """
            <svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 40'>
              <line x1='10' y1='20' x2='100' y2='20' stroke='#000000' stroke-width='2'/>
              <polygon points='100,20 90,15 90,25' fill='#000000'/>
            </svg>
            """;

        // Act: render without a handler and with one
        using var plainStream = ToStream(svg);
        using var plain = SvgCodec.Load(plainStream, 120, 40);
        var hints = new List<SvgHint>();
        using var hintedStream = ToStream(svg);
        using var hinted = SvgCodec.LoadWithFontFaces(hintedStream, 120, 40, null, new SvgLoadOptions { HintHandler = hints.Add });

        // Assert: a hint was reported, and every pixel is the same
        Assert.Single(hints);
        for (var y = 0; y < plain.Height; y++)
        {
            for (var x = 0; x < plain.Width; x++)
            {
                Assert.Equal(plain[x, y], hinted[x, y]);
            }
        }
    }
}
