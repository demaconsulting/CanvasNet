using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Tests of <see cref="EdgeBuilder"/>: the d3 curves against d3-shape's own output, the marker geometry of the
///     flowchart and class markers, the route operations of <c>edges.js</c>, the stroke and marker paint, the edge labels
///     and the terminal labels.
/// </summary>
public sealed class EdgeBuilderTests
{
    /// <summary>The curve vectors, read once.</summary>
    private static readonly Lazy<JsonArray> CurveVectors = new(() => UpstreamVectors.Load("curves.json")["cases"]!.AsArray());

    /// <summary>The paint of an uncolored marker in the default theme.</summary>
    private static readonly MarkerPaint Gray = new(new Rgba32(0x33, 0x33, 0x33, 255), new Rgba32(0x33, 0x33, 0x33, 255));

    /// <summary>
    ///     Gets the indexes of the curve vectors.
    /// </summary>
    /// <returns>The indexes.</returns>
    public static TheoryData<int> CurveCases() => UpstreamVectors.Indexes(CurveVectors.Value);

    /// <summary>
    ///     Checks every curve on every point set against the path text d3-shape writes (3 decimals, d3's command forms).
    /// </summary>
    /// <param name="index">The vector index.</param>
    [Theory]
    [MemberData(nameof(CurveCases))]
    public void EdgeBuilder_Line_UpstreamVector_MatchesD3(int index)
    {
        // Arrange
        var vector = CurveVectors.Value[index]!.AsObject();
        var points = vector["points"]!.AsArray().Select(p => new ShapePoint((double)p![0]!, (double)p[1]!)).ToList();

        // Act
        var path = EdgeBuilder.Line(points, (string)vector["curve"]!);

        // Assert
        Assert.Equal((string?)vector["expected"], path.Data);
        Assert.Equal(SceneSegmentKind.MoveTo, path.Segments[0].Kind);
    }

    /// <summary>
    ///     Checks that a curve name Mermaid does not list draws a basis curve, as <c>insertEdge</c>'s default does.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Line_UnknownCurve_DrawsBasis()
    {
        // Arrange
        List<ShapePoint> points = [new(0, 0), new(30, 40), new(60, 0), new(90, 40)];

        // Act
        var unknown = EdgeBuilder.Line(points, "wiggly");
        var basis = EdgeBuilder.Line(points, "basis");

        // Assert
        Assert.Equal(basis.Data, unknown.Data);
    }

    /// <summary>
    ///     Checks that a number that is not finite ends the drawn path, as a browser stops at <c>NaN</c> in path data.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Line_NotFiniteNumber_EndsTheDrawnPath()
    {
        // Act
        var path = EdgeBuilder.Line([new(0, 0), new(double.NaN, 5), new(10, 10)], "linear");

        // Assert
        Assert.Equal("M0,0LNaN,5L10,10", path.Data);
        Assert.Single(path.Segments);
    }

    /// <summary>
    ///     Checks the markers a flowchart defines and their start variants.
    /// </summary>
    [Fact]
    public void EdgeBuilder_MarkerOf_ArrowTypes_GiveMermaidsMarkers()
    {
        // Act / Assert
        Assert.Equal(new EdgeBuilder.MarkerGeometry("point", 10, 8, 5, 5, 1, false), EdgeBuilder.MarkerOf("arrow_point", false));
        Assert.Equal(new EdgeBuilder.MarkerGeometry("point", 10, 8, 4.5, 5, 1, true), EdgeBuilder.MarkerOf("arrow_point", true));
        Assert.Equal(new EdgeBuilder.MarkerGeometry("circle", 10, 11, 11, 5, 1, false), EdgeBuilder.MarkerOf("arrow_circle", false));
        Assert.Equal(new EdgeBuilder.MarkerGeometry("cross", 11, 11, -1, 5.2, 2, true), EdgeBuilder.MarkerOf("arrow_cross", true));
        Assert.Null(EdgeBuilder.MarkerOf("none", false));
        Assert.Null(EdgeBuilder.MarkerOf("arrow_open", true));
    }

    /// <summary>
    ///     Checks the arrowhead at the end of a path: 8 by 8 pixels, its tip 4 pixels beyond the vertex (where the line
    ///     was pulled back from the node), clipped to its view box.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawMarker_PointEnd_TipIsFourPixelsAhead()
    {
        // Act
        var right = Assert.Single(EdgeBuilder.DrawMarker(EdgeBuilder.MarkerOf("arrow_point", false)!, 100, 50, 0, Gray, "e"));
        var down = Assert.Single(EdgeBuilder.DrawMarker(EdgeBuilder.MarkerOf("arrow_point", false)!, 100, 50, Math.PI / 2, Gray, "e"));

        // Assert
        AssertBox(new SceneRect(96, 46, 8, 8), Scene.BoundsOf([right]));
        AssertBox(new SceneRect(96, 46, 8, 8), Scene.BoundsOf([down]));
        var path = Assert.IsType<ScenePath>(right);
        Assert.Equal(Gray.Fill, path.Fill);
        Assert.Null(path.Stroke);
        Assert.Equal("marker", path.Role);
    }

    /// <summary>
    ///     Checks the circle and cross markers: the circle of radius 5.5 before the vertex, the cross stroked 2 pixels wide.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawMarker_CircleAndCross_ArePlacedBeforeTheVertex()
    {
        // Act
        var circle = Assert.IsType<ScenePath>(Assert.Single(EdgeBuilder.DrawMarker(EdgeBuilder.MarkerOf("arrow_circle", false)!, 100, 50, 0, Gray, "e")));
        var cross = Assert.IsType<ScenePath>(Assert.Single(EdgeBuilder.DrawMarker(EdgeBuilder.MarkerOf("arrow_cross", false)!, 100, 50, 0, Gray, "e")));

        // Assert
        AssertBox(new SceneRect(87.9, 44.5, 11, 11), Scene.BoundsOf([circle]));
        Assert.Equal(1.1, circle.Stroke!.Width, 9);
        AssertBox(new SceneRect(89, 45.8, 9, 9), Scene.BoundsOf([cross]));
        Assert.Equal(2, cross.Stroke!.Width, 9);
        Assert.Null(cross.Fill);
    }

    /// <summary>
    ///     Checks the direction a marker takes at each end of a path (<c>orient="auto"</c>).
    /// </summary>
    [Fact]
    public void EdgeBuilder_StartOfAndEndOf_Curve_TakeTheTangents()
    {
        // Arrange
        List<SceneSegment> segments = [SceneSegment.Move(0, 0), SceneSegment.Cubic(0, 0, 10, 0, 20, 10), SceneSegment.Cubic(20, 15, 20, 20, 20, 20)];

        // Act
        var start = EdgeBuilder.StartOf(segments);
        var end = EdgeBuilder.EndOf(segments);

        // Assert
        Assert.Equal((0.0, 0.0, 0.0), start);
        Assert.Equal((20.0, 20.0, Math.PI / 2), end);
        Assert.Null(EdgeBuilder.EndOf([]));
    }

    /// <summary>
    ///     Checks <c>cutPathAtIntersect</c>: the route stops where it enters the box.
    /// </summary>
    [Fact]
    public void EdgeBuilder_CutPathAtIntersect_RouteIntoBox_EndsOnTheBorder()
    {
        // Act
        var cut = EdgeBuilder.CutPathAtIntersect([new(0, 100), new(50, 100), new(90, 100), new(110, 100)], new NodeBox(100, 100, 50, 50));

        // Assert
        Assert.Equal([new ShapePoint(0, 100), new ShapePoint(50, 100), new ShapePoint(75, 100)], cut);
    }

    /// <summary>
    ///     Checks <c>intersection</c> on the top side, and its quirk: a vertical segment gives the outside point back.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Intersection_TopSide_KeepsMermaidsArithmetic()
    {
        // Arrange
        var box = new NodeBox(100, 100, 50, 50);

        // Act
        var diagonal = EdgeBuilder.Intersection(box, new ShapePoint(50, 0), new ShapePoint(90, 90));
        var vertical = EdgeBuilder.Intersection(box, new ShapePoint(100, 0), new ShapePoint(100, 90));

        // Assert
        Assert.Equal(83.333333333, diagonal.X, 6);
        Assert.Equal(75, diagonal.Y, 9);
        Assert.Equal(new ShapePoint(100, 0), vertical);
    }

    /// <summary>
    ///     Checks <c>fixCorners</c>: a right angle becomes three points, 5 pixels each side of the corner.
    /// </summary>
    [Fact]
    public void EdgeBuilder_FixCorners_RightAngle_IsRoundedOff()
    {
        // Act
        var fixedRoute = EdgeBuilder.FixCorners([new(0, 0), new(0, 50), new(50, 50)]);

        // Assert
        Assert.Equal(5, fixedRoute.Count);
        Assert.Equal(new ShapePoint(0, 45), fixedRoute[1]);
        Assert.Equal(5 - (Math.Sqrt(2) * 2), fixedRoute[2].X, 9);
        Assert.Equal(45 + (Math.Sqrt(2) * 2), fixedRoute[2].Y, 9);
        Assert.Equal(new ShapePoint(5, 50), fixedRoute[3]);
    }

    /// <summary>
    ///     Checks <c>calcLabelPosition</c>: the middle by length, a single point, and nothing for an empty route.
    /// </summary>
    [Fact]
    public void EdgeBuilder_CalcLabelPosition_Route_IsTheMiddleByLength()
    {
        // Act / Assert
        Assert.Equal(new ShapePoint(10, 10), EdgeBuilder.CalcLabelPosition([new(0, 0), new(10, 0), new(10, 30)]));
        Assert.Equal(new ShapePoint(3, 4), EdgeBuilder.CalcLabelPosition([new(3, 4)]));
        Assert.Equal(new ShapePoint(0.61728, 0), EdgeBuilder.CalcLabelPosition([new(0, 0), new(1.234567, 0)]));
        Assert.Null(EdgeBuilder.CalcLabelPosition([]));
    }

    /// <summary>
    ///     Checks <c>isLabelCoordinateInPath</c>, a text search of the rounded coordinates in the rounded path data,
    ///     including its quirk with negative numbers.
    /// </summary>
    [Fact]
    public void EdgeBuilder_IsLabelCoordinateInPath_RoundedText_IsSearched()
    {
        // Act / Assert
        Assert.True(EdgeBuilder.IsLabelCoordinateInPath(new ShapePoint(11, 999), "M10.6,20L30.4,40.5"));
        Assert.True(EdgeBuilder.IsLabelCoordinateInPath(new ShapePoint(999, 41), "M10.6,20L30.4,40.5"));
        Assert.False(EdgeBuilder.IsLabelCoordinateInPath(new ShapePoint(99, 98), "M10.6,20L30.4,40.5"));
        Assert.False(EdgeBuilder.IsLabelCoordinateInPath(new ShapePoint(-12.5, 1000), "M-12.5,0"));
    }

    /// <summary>
    ///     Checks the rounded curve: the ends pulled back by the arrowhead offset, and a quadratic curve at the corner.
    /// </summary>
    [Fact]
    public void EdgeBuilder_RoundedPath_Corner_IsAQuadraticCurve()
    {
        // Arrange
        var points = EdgeBuilder.ApplyMarkerOffsetsToPoints([new(0, 0), new(0, 50), new(54, 50)], "none", "arrow_point");

        // Act
        var (segments, data) = EdgeBuilder.RoundedPath(points, 5);

        // Assert
        Assert.Equal(new ShapePoint(50, 50), points[2]);
        Assert.Equal([SceneSegmentKind.MoveTo, SceneSegmentKind.LineTo, SceneSegmentKind.QuadraticTo, SceneSegmentKind.LineTo], segments.Select(s => s.Kind));
        Assert.StartsWith("M0,0L0,42.928932188134524Q0,50 7.07106781186547", data, StringComparison.Ordinal);
        Assert.EndsWith(",50L50,50", data, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Checks that an arrowhead's line ends 4 pixels before the node, so the arrowhead's tip touches it.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Draw_ArrowPoint_TipTouchesTheNode()
    {
        // Arrange
        var route = new EdgeRoute(Edge(), "e", [new(0, 0), new(0, 50), new(0, 100)]);

        // Act
        var drawing = EdgeBuilder.Draw(route, null, null, RenderingTestContext.Create());

        // Assert
        var path = Assert.IsType<ScenePath>(drawing.Items[0]);
        Assert.Equal(96, path.Segments[^1].Y, 9);
        Assert.Equal(100, Scene.BoundsOf([drawing.Items[1]])!.Value.Bottom, 9);
        Assert.Equal("M0,0L0,50L0,96", drawing.PathData);
        Assert.Null(drawing.UpdatedPath);
    }

    /// <summary>
    ///     Checks the stroke of each link stroke: <c>.edge-thickness-*</c> and <c>.edge-pattern-*</c> with the line color.
    /// </summary>
    /// <param name="stroke">The link stroke.</param>
    /// <param name="width">The expected stroke width, or 0 for none.</param>
    /// <param name="dash">The expected dash, or 0 for solid.</param>
    [Theory]
    [InlineData("normal", 1, 0)]
    [InlineData("thick", 3.5, 0)]
    [InlineData("dotted", 1, 2)]
    [InlineData("invisible", 0, 0)]
    public void EdgeBuilder_Draw_LinkStroke_FollowsTheClasses(string stroke, double width, double dash)
    {
        // Arrange
        var edge = Edge() with
        {
            Thickness = stroke,
            Pattern = stroke,
            Classes = stroke == "invisible" ? string.Empty : "edge-thickness-normal edge-pattern-solid flowchart-link",
        };

        // Act
        var drawing = EdgeBuilder.Draw(new EdgeRoute(edge, "e", [new(0, 0), new(0, 50), new(0, 100)]), null, null, RenderingTestContext.Create());

        // Assert
        var path = Assert.IsType<ScenePath>(drawing.Items[0]);
        Assert.Null(path.Fill);
        if (width == 0)
        {
            Assert.Null(path.Stroke);
            return;
        }

        Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), path.Stroke!.Color);
        Assert.Equal(width, path.Stroke.Width, 9);
        Assert.Equal(dash == 0 ? null : [dash, dash], path.Stroke.Dash);
    }

    /// <summary>
    ///     Checks that a <c>linkStyle</c> stroke colors the path and its arrowhead (Mermaid's colored marker copy).
    /// </summary>
    [Fact]
    public void EdgeBuilder_Draw_LinkStyleStroke_ColorsPathAndMarker()
    {
        // Arrange
        var edge = Edge() with { Style = ["stroke:#f00", "stroke-width:4px"] };

        // Act
        var drawing = EdgeBuilder.Draw(new EdgeRoute(edge, "e", [new(0, 0), new(0, 50), new(0, 100)]), null, null, RenderingTestContext.Create());

        // Assert
        var path = Assert.IsType<ScenePath>(drawing.Items[0]);
        var marker = Assert.IsType<ScenePath>(drawing.Items[1]);
        Assert.Equal((new Rgba32(255, 0, 0, 255), 4.0), (path.Stroke!.Color, path.Stroke.Width));
        Assert.Equal(new Rgba32(255, 0, 0, 255), marker.Fill);
    }

    /// <summary>
    ///     Checks that an animated edge is drawn as the first frame of its animation: dashed 9,5 at offset 900.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Draw_AnimatedEdge_IsDashedNineFive()
    {
        // Arrange
        var (data, context) = RenderingTestContext.Flowchart("flowchart LR\n  a e1@--> b\n  e1@{ animate: true }\n");

        // Act
        var drawing = EdgeBuilder.Draw(new EdgeRoute(data.Edges[0], "e1", [new(0, 0), new(50, 0), new(100, 0)]), null, null, context);

        // Assert
        var stroke = Assert.IsType<ScenePath>(drawing.Items[0]).Stroke!;
        Assert.Equal([9.0, 5.0], stroke.Dash);
        Assert.Equal(900, stroke.DashOffset);
    }

    /// <summary>
    ///     Checks that the intersect functions of the ends replace the first and last points, and that a route into a
    ///     subgraph is cut (with Mermaid's quirk: a vertical route stops at its last point outside) and moves the label
    ///     to the middle of the cut route.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Draw_EndsAndSubgraph_CutTheRoute()
    {
        // Arrange
        var end = new EdgeEnd(new NodeBox(0, 0, 20, 20), ShapeLibrary.IntersectRect);
        var target = new EdgeEnd(new NodeBox(0, 200, 20, 20), ShapeLibrary.IntersectRect);
        var route = new EdgeRoute(Edge(), "e", [new(0, 5), new(0, 100), new(0, 195)]) { ToCluster = new NodeBox(0, 200, 100, 100) };

        // Act
        var plain = EdgeBuilder.Draw(route with { ToCluster = null }, end, target, RenderingTestContext.Create());
        var cut = EdgeBuilder.Draw(route, end, target, RenderingTestContext.Create());

        // Assert
        Assert.Equal("M0,10L0,100L0,186", plain.PathData);
        Assert.Equal([new ShapePoint(0, 5), new ShapePoint(0, 100)], cut.UpdatedPath);
        Assert.Equal("M0,5L0,96", cut.PathData);
        Assert.Equal(new ShapePoint(0, 52.5), EdgeBuilder.LabelPosition("label", 0, 999, cut, 0));
    }

    /// <summary>
    ///     Checks where labels go: nowhere without text, at the layout's position plus half the title margin otherwise.
    /// </summary>
    [Fact]
    public void EdgeBuilder_LabelPosition_LayoutPosition_IsMovedByHalfTheTitleMargin()
    {
        // Arrange
        var drawing = new EdgeDrawing([], null, null, [new(0, 0), new(0, 10)], "M0,0L0,10");

        // Act / Assert
        Assert.Null(EdgeBuilder.LabelPosition(string.Empty, 5, 5, drawing, 0));
        Assert.Equal(new ShapePoint(5, 15), EdgeBuilder.LabelPosition("x", 5, 5, drawing, 20));
        Assert.Equal(new ShapePoint(0, 0), EdgeBuilder.LabelPosition("x", null, null, drawing, 20));
    }

    /// <summary>
    ///     Checks the measured edge label: empty for no text, a 24 pixel line box for a one-line HTML label.
    /// </summary>
    [Fact]
    public void EdgeBuilder_MeasureLabel_Text_IsOneLineBox()
    {
        // Arrange
        var context = RenderingTestContext.Create();

        // Act
        var empty = EdgeBuilder.MeasureLabel(Edge(), string.Empty, context);
        var yes = EdgeBuilder.MeasureLabel(Edge(), "yes", context);

        // Assert
        Assert.Equal(new SceneRect(0, 0, 0, 0), empty.Box);
        Assert.True(yes.IsHtml);
        Assert.Equal(24, yes.Height, 9);
        Assert.InRange(yes.Width, 20, 30);
    }

    /// <summary>
    ///     Checks a placed HTML edge label: two backgrounds over its box (the div's and the paragraph's) and its text,
    ///     centered on its position.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawLabel_Html_IsCenteredWithTwoBackgrounds()
    {
        // Arrange
        var context = RenderingTestContext.Create();
        var label = EdgeBuilder.MeasureLabel(Edge(), "yes", context);

        // Act
        var drawing = EdgeBuilder.DrawLabel(label, new ShapePoint(100, 50), Edge(), "e", context.Styles);

        // Assert
        var group = Assert.IsType<SceneGroup>(Assert.Single(drawing.Items));
        Assert.Equal(("edge-label", 100.0, 50.0), (group.Role, group.OffsetX, group.OffsetY));
        Assert.Equal(2, group.Children.Count(c => c.Role == "edge-label-background"));
        Assert.Contains(group.Children, c => c is SceneText);
        AssertBox(new SceneRect(100 - (label.Width / 2), 38, label.Width, 24), drawing.Bounds);
    }

    /// <summary>
    ///     Checks a placed SVG edge label: one background at half opacity, centered on the text box.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawLabel_Svg_HasAHalfOpaqueBackground()
    {
        // Arrange
        var context = RenderingTestContext.Create("---\nconfig:\n  htmlLabels: false\n---\nflowchart TD\n  A\n");
        var label = EdgeBuilder.MeasureLabel(Edge(), "yes", context);

        // Act
        var drawing = EdgeBuilder.DrawLabel(label, new ShapePoint(100, 50), Edge(), "e", context.Styles);

        // Assert
        Assert.False(label.IsHtml);
        var group = Assert.IsType<SceneGroup>(Assert.Single(drawing.Items));
        var background = Assert.IsType<ScenePath>(Assert.Single(group.Children, c => c.Role == "edge-label-background"));
        Assert.True(background.Fill!.Value.A < 255);
        Assert.Equal(100, drawing.Bounds!.Value.X + (drawing.Bounds.Value.Width / 2), 6);
        Assert.Equal(50, drawing.Bounds.Value.Y + (drawing.Bounds.Value.Height / 2), 6);
        Assert.Empty(EdgeBuilder.DrawLabel(LabelLayout.Of(true, default), new ShapePoint(0, 0), Edge(), "e", context.Styles).Items);
    }

    /// <summary>
    ///     Checks a placed SVG edge label whose own font size is smaller than the diagram's (an entity relationship role of
    ///     14 pixels in a 16 pixel diagram): the text as drawn is centered on the label's position, and its background,
    ///     measured in the diagram's size, is taller than the text with its padding.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawLabel_SvgInASmallerSize_IsCenteredOnItsDrawnText()
    {
        // Arrange
        var context = RenderingTestContext.CreateEr("---\nconfig:\n  htmlLabels: false\n---\nerDiagram\n  A ||--o{ B : has\n");
        var label = EdgeBuilder.MeasureLabel(Edge(), "has", context);

        // Act
        var drawing = EdgeBuilder.DrawLabel(label, new ShapePoint(100, 50), Edge(), "e", context.Styles);

        // Assert
        Assert.False(label.IsHtml);
        Assert.Equal(14, label.Lines[0].Runs[0].Font.Size);
        var group = Assert.IsType<SceneGroup>(Assert.Single(drawing.Items));
        var text = Scene.BoundsOf([.. group.Children.Where(c => c is SceneText)])!.Value.Offset(group.OffsetX, group.OffsetY);
        Assert.Equal(100, text.X + (text.Width / 2), 6);
        Assert.Equal(50, text.Y + (text.Height / 2), 6);
        var background = Scene.BoundsOf([Assert.Single(group.Children, c => c.Role == "edge-label-background")])!.Value;
        Assert.True(background.Height > text.Height + (2 * LabelBuilder.SvgBackgroundPadding));
    }

    /// <summary>
    ///     Checks the class diagram's edge label backgrounds: an HTML label's are the node background of the class
    ///     sheet (<c>.labelBkg</c> and <c>.edgeLabel .label span</c>), and an SVG label's rectangle is too
    ///     (<c>.edgeLabel .label rect</c>).
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawLabel_ClassDiagram_UsesTheClassBackgrounds()
    {
        // Arrange
        var html = RenderingTestContext.CreateClass();
        var svg = RenderingTestContext.CreateClass("---\nconfig:\n  htmlLabels: false\n---\nclassDiagram\n  class A\n");
        var mainBkg = new Rgba32(0xEC, 0xEC, 0xFF, 255);

        // Act
        var htmlDrawing = EdgeBuilder.DrawLabel(EdgeBuilder.MeasureLabel(Edge(), "uses", html), new ShapePoint(0, 0), Edge(), "e", html.Styles);
        var svgDrawing = EdgeBuilder.DrawLabel(EdgeBuilder.MeasureLabel(Edge(), "uses", svg), new ShapePoint(0, 0), Edge(), "e", svg.Styles);

        // Assert
        var htmlBackgrounds = Assert.IsType<SceneGroup>(Assert.Single(htmlDrawing.Items)).Children.Where(c => c.Role == "edge-label-background").ToList();
        Assert.Equal(2, htmlBackgrounds.Count);
        Assert.All(htmlBackgrounds, background => Assert.Equal(mainBkg, Assert.IsType<ScenePath>(background).Fill));
        var svgBackground = Assert.Single(Assert.IsType<SceneGroup>(Assert.Single(svgDrawing.Items)).Children, c => c.Role == "edge-label-background");
        Assert.Equal(mainBkg, Assert.IsType<ScenePath>(svgBackground).Fill);
    }

    /// <summary>
    ///     Checks the class diagram's markers: their types and reference points, and none for the flowchart's types.
    /// </summary>
    [Fact]
    public void EdgeBuilder_UserSpaceMarkerOf_ClassArrowTypes_GiveMermaidsMarkers()
    {
        // Act
        var ends = new[] { "aggregation", "extension", "composition", "dependency", "lollipop" }
            .Select(type => (EdgeBuilder.UserSpaceMarkerOf(type, true)!, EdgeBuilder.UserSpaceMarkerOf(type, false)!))
            .ToList();

        // Assert: refX and refY of markers.js, start then end
        Assert.Equal(
            [(18.0, 7.0, 1.0, 7.0), (18, 7, 1, 7), (18, 7, 1, 7), (6, 7, 13, 7), (13, 7, 1, 7)],
            ends.Select(e => (e.Item1.RefX, e.Item1.RefY, e.Item2.RefX, e.Item2.RefY)));
        Assert.Equal(["aggregation", "extension", "composition", "dependency", "lollipop"], ends.Select(e => e.Item2.Type));
        Assert.Equal("transparent", Assert.Single(ends[4].Item1.Parts).Fill);
        Assert.Null(EdgeBuilder.UserSpaceMarkerOf("none", false));
        Assert.Null(EdgeBuilder.UserSpaceMarkerOf("arrow_point", true));
    }

    /// <summary>
    ///     Checks that the extension marker is drawn in user units: the end variant's tip 17 pixels ahead of the vertex,
    ///     the start variant's tip 17 pixels behind it.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawUserSpaceMarker_Extension_TipIsSeventeenPixelsFromTheVertex()
    {
        // Act
        var end = Assert.IsType<ScenePath>(Assert.Single(EdgeBuilder.DrawUserSpaceMarker(EdgeBuilder.UserSpaceMarkerOf("extension", false)!, 100, 50, 0, Gray, "e")));
        var start = Assert.IsType<ScenePath>(Assert.Single(EdgeBuilder.DrawUserSpaceMarker(EdgeBuilder.UserSpaceMarkerOf("extension", true)!, 100, 50, 0, Gray, "e")));

        // Assert: M 1,1 V 13 L18,7 Z with refX 1, and M 1,7 L18,13 V 1 Z with refX 18
        Assert.Equal([(100.0, 44.0), (100, 56), (117, 50)], end.Segments.Take(3).Select(s => (Math.Round(s.X, 9), Math.Round(s.Y, 9))));
        Assert.Equal([(83.0, 50.0), (100, 56), (100, 44)], start.Segments.Take(3).Select(s => (Math.Round(s.X, 9), Math.Round(s.Y, 9))));
        Assert.Equal(("marker", "e"), (end.Role, end.Id));
        Assert.Equal(new SceneStroke(Gray.Stroke!.Value, 1), end.Stroke);
    }

    /// <summary>
    ///     Checks that the lollipop is an unfilled circle of radius 6 behind the start vertex, its own transparent fill
    ///     winning over the marker's.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawUserSpaceMarker_LollipopStart_IsAnUnfilledCircleBehindTheVertex()
    {
        // Act
        var circle = Assert.IsType<ScenePath>(Assert.Single(EdgeBuilder.DrawUserSpaceMarker(EdgeBuilder.UserSpaceMarkerOf("lollipop", true)!, 100, 50, 0, Gray, "e")));

        // Assert: the center (7, 7) with refX 13 is 6 pixels behind the vertex
        Assert.Null(circle.Fill);
        AssertBox(new SceneRect(88, 44, 12, 12), Scene.BoundsOf([circle]));
    }

    /// <summary>
    ///     Checks a class relation: its line shortened by 17.25 pixels at each end, a composition at its start filled and
    ///     stroked in the line color with its tip a quarter pixel from the node, and an extension at its end stroked only.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Draw_ClassRelation_MarkersFollowTheClassRules()
    {
        // Arrange
        var gray = new Rgba32(0x33, 0x33, 0x33, 255);
        var route = new EdgeRoute(Relation("composition", "extension"), "r", [new(0, 0), new(0, 50), new(0, 100)]);

        // Act
        var drawing = EdgeBuilder.Draw(route, null, null, RenderingTestContext.CreateClass());

        // Assert
        Assert.Equal("M0,17.25L0,50L0,82.75", drawing.PathData);
        var composition = Assert.IsType<ScenePath>(drawing.Items[1]);
        var extension = Assert.IsType<ScenePath>(drawing.Items[2]);
        Assert.Equal((gray, gray, 1.0), (composition.Fill, composition.Stroke!.Color, composition.Stroke.Width));
        Assert.Equal(0.25, Scene.BoundsOf([composition])!.Value.Y, 9);
        Assert.Null(extension.Fill);
        Assert.Equal(gray, extension.Stroke!.Color);
        Assert.Equal(99.75, Scene.BoundsOf([extension])!.Value.Bottom, 9);
    }

    /// <summary>
    ///     Checks that a relation's own stroke colors its markers (Mermaid's colored marker copy): the stroke of every
    ///     marker, and the fill of the filled types only.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Draw_ClassRelationStroke_ColorsTheMarkers()
    {
        // Arrange
        var red = new Rgba32(255, 0, 0, 255);
        var edge = Relation("dependency", "aggregation") with { Style = ["stroke:#f00"] };

        // Act
        var drawing = EdgeBuilder.Draw(new EdgeRoute(edge, "r", [new(0, 0), new(0, 50), new(0, 100)]), null, null, RenderingTestContext.CreateClass());

        // Assert
        var dependency = Assert.IsType<ScenePath>(drawing.Items[1]);
        var aggregation = Assert.IsType<ScenePath>(drawing.Items[2]);
        Assert.Equal((red, red), (dependency.Fill, dependency.Stroke!.Color));
        Assert.Equal(((Rgba32?)null, red), (aggregation.Fill, aggregation.Stroke!.Color));
    }

    /// <summary>
    ///     Checks the state and entity relationship markers: the barb at a transition's end only, the four entity
    ///     relationship markers at both ends with the reference points of <c>markers.js</c>, their white rings, their units
    ///     of the stroke width, and none for an arrow type Mermaid's map does not know (<c>md_parent</c>).
    /// </summary>
    [Fact]
    public void EdgeBuilder_UserSpaceMarkerOf_StateAndErArrowTypes_GiveMermaidsMarkers()
    {
        // Act
        var barb = EdgeBuilder.UserSpaceMarkerOf("arrow_barb", false)!;
        var ends = new[] { "only_one", "zero_or_one", "one_or_more", "zero_or_more" }
            .Select(type => (EdgeBuilder.UserSpaceMarkerOf(type, true)!, EdgeBuilder.UserSpaceMarkerOf(type, false)!))
            .ToList();

        // Assert
        Assert.Equal(("barb", 19.0, 7.0, false), (barb.Type, barb.RefX, barb.RefY, barb.ScalesWithStroke));
        Assert.Null(EdgeBuilder.UserSpaceMarkerOf("arrow_barb", true));
        Assert.Equal(
            [(0.0, 9.0, 18.0, 9.0), (0, 9, 30, 9), (18, 18, 27, 18), (18, 18, 39, 18)],
            ends.Select(e => (e.Item1.RefX, e.Item1.RefY, e.Item2.RefX, e.Item2.RefY)));
        Assert.Equal(["onlyOne", "zeroOrOne", "oneOrMore", "zeroOrMore"], ends.Select(e => e.Item2.Type));
        Assert.All(ends, e => Assert.True(e.Item1.ScalesWithStroke && e.Item2.ScalesWithStroke));
        Assert.Equal(new string?[] { "white", null }, ends[3].Item2.Parts.Select(part => part.Fill));
        Assert.Null(EdgeBuilder.UserSpaceMarkerOf("md_parent", false));
    }

    /// <summary>
    ///     Checks that an entity relationship marker is drawn in units of the path's stroke width: a zero-or-more end on a
    ///     path 2 wide is twice its size, its ring 12 pixels in radius 60 pixels behind the vertex, stroked 2 wide.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawUserSpaceMarker_ErMarker_ScalesWithTheStrokeWidth()
    {
        // Act
        var items = EdgeBuilder.DrawUserSpaceMarker(EdgeBuilder.UserSpaceMarkerOf("zero_or_more", false)!, 100, 50, 0, Gray, "e", 2);

        // Assert: the ring (9, 18, r 6) with refX 39 and refY 18; the crow's foot from x 21 to x 57
        var ring = Assert.IsType<ScenePath>(items[0]);
        var foot = Assert.IsType<ScenePath>(items[1]);
        AssertBox(new SceneRect(28, 38, 24, 24), Scene.BoundsOf([ring]));
        Assert.Equal(new Rgba32(255, 255, 255, 255), ring.Fill);
        Assert.Equal(2, ring.Stroke!.Width);
        AssertBox(new SceneRect(64, 32, 72, 36), Scene.BoundsOf([foot]));
    }

    /// <summary>
    ///     Checks a state transition: its barb at the end, its tip on the route's end (no shortening), filled and stroked
    ///     by the more specific <c>defs [id$="-barbEnd"]</c> rule in the transition color, not the line color.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Draw_StateTransition_BarbTakesTheTransitionColor()
    {
        // Arrange
        const string Text = "---\nconfig:\n  themeVariables:\n    transitionColor: '#00aa00'\n    lineColor: '#aa0000'\n---\nstateDiagram-v2\n  A --> B\n";
        var green = new Rgba32(0, 0xaa, 0, 255);
        var edge = Relation("none", "arrow_barb") with { Classes = "transition" };

        // Act
        var drawing = EdgeBuilder.Draw(new EdgeRoute(edge, "t", [new(0, 0), new(0, 50), new(0, 100)]), null, null, RenderingTestContext.CreateState(Text));

        // Assert
        Assert.Equal("M0,0L0,50L0,100", drawing.PathData);
        var path = Assert.IsType<ScenePath>(drawing.Items[0]);
        var barb = Assert.IsType<ScenePath>(drawing.Items[1]);
        Assert.Equal(green, path.Stroke!.Color);
        Assert.Equal((green, green, 1.0), (barb.Fill, barb.Stroke!.Color, barb.Stroke.Width));
        Assert.Equal(100, Scene.BoundsOf([barb])!.Value.Bottom, 9);
    }

    /// <summary>
    ///     Checks an entity relationship: its route not shortened, its markers unfilled by <c>.marker</c>'s important
    ///     <c>none</c> and stroked in the line color, the zero ring keeping its own white fill.
    /// </summary>
    [Fact]
    public void EdgeBuilder_Draw_ErRelationship_MarkersAreUnfilledWithWhiteRings()
    {
        // Arrange
        var gray = new Rgba32(0x33, 0x33, 0x33, 255);
        var edge = Relation("only_one", "zero_or_more") with { Classes = "relationshipLine" };

        // Act
        var drawing = EdgeBuilder.Draw(new EdgeRoute(edge, "r", [new(0, 0), new(0, 50), new(0, 100)]), null, null, RenderingTestContext.CreateEr());

        // Assert
        Assert.Equal("M0,0L0,50L0,100", drawing.PathData);
        var markers = drawing.Items.Skip(1).Cast<ScenePath>().ToList();
        Assert.Equal(3, markers.Count);
        Assert.All(markers, marker => Assert.Equal((gray, 1.0), (marker.Stroke!.Color, marker.Stroke.Width)));
        Assert.Equal(new Rgba32?[] { null, new Rgba32(255, 255, 255, 255), null }, markers.Select(marker => marker.Fill));
    }

    /// <summary>
    ///     Checks where terminal labels go on a straight route: beside the middle of the first 35 pixels (25 plus the
    ///     marker size), 15 pixels to the right of the walking direction, the end label moved 5 pixels up and left.
    /// </summary>
    [Fact]
    public void EdgeBuilder_CalcTerminalLabelPosition_StraightRoute_IsBesideTheWalkedPart()
    {
        // Arrange: a route straight down
        IReadOnlyList<ShapePoint> route = [new(0, 0), new(0, 100)];

        // Act
        var start = EdgeBuilder.CalcTerminalLabelPosition(10, false, route)!.Value;
        var end = EdgeBuilder.CalcTerminalLabelPosition(10, true, route)!.Value;
        var bare = EdgeBuilder.CalcTerminalLabelPosition(0, false, route)!.Value;

        // Assert
        Assert.Equal((-15, 17.5), (Math.Round(start.X, 9), Math.Round(start.Y, 9)));
        Assert.Equal((10, 77.5), (Math.Round(end.X, 9), Math.Round(end.Y, 9)));
        Assert.Equal((-10, 12.5), (Math.Round(bare.X, 9), Math.Round(bare.Y, 9)));
    }

    /// <summary>
    ///     Checks that a route shorter than the walk has no terminal label position, and that drawing a terminal label
    ///     there fails located at the link, as Mermaid's render fails.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawTerminals_ShortRoute_FailsLocatedAtTheLink()
    {
        // Arrange
        IReadOnlyList<ShapePoint> route = [new(0, 0), new(0, 20)];
        var drawing = new EdgeDrawing([], null, null, route, "M0,0L0,20");
        var edge = Relation("none", "none") with { StartLabelRight = "1", Location = new MermaidLocation("t.mmd", 3, 2) };

        // Act
        var position = EdgeBuilder.CalcTerminalLabelPosition(10, false, route);
        var exception = Assert.Throws<MermaidRenderException>(() =>
            EdgeBuilder.DrawTerminals(edge, "r", "1", string.Empty, "none", "none", drawing, RenderingTestContext.CreateClass()));

        // Assert
        Assert.Null(position);
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, new MermaidLocation("t.mmd", 3, 2)), (exception.Code, exception.Location));
        Assert.NotNull(exception.Hint);
    }

    /// <summary>
    ///     Checks HTML terminal labels: drawn in the 11 pixel <c>.edgeTerminals</c> font, the start label centered on its
    ///     position, the end label starting at its position, each with the box of a foreignObject 9 pixels per character
    ///     wide and 12 pixels high.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawTerminals_HtmlLabels_StartCenteredEndNot()
    {
        // Arrange
        var context = RenderingTestContext.CreateClass();
        var edge = Relation("none", "extension");
        var drawing = new EdgeDrawing([], null, null, [new(0, 0), new(0, 100)], "M0,0L0,100");
        var one = EdgeBuilder.MeasureTerminal(edge, "1", context);

        // Act
        var terminals = EdgeBuilder.DrawTerminals(edge, "r", "1", "many", edge.ArrowTypeStart, edge.ArrowTypeEnd, drawing, context);

        // Assert
        Assert.True(one.IsHtml);
        Assert.Equal(16.5, one.Height, 9);
        var groups = terminals.Items.Cast<SceneGroup>().ToList();
        Assert.Equal([("edge-terminal", "r", -15.0, 17.5), ("edge-terminal", "r", 10, 77.5)], groups.Select(g => (g.Role, g.Id!, Math.Round(g.OffsetX, 9), Math.Round(g.OffsetY, 9))));
        Assert.All(groups, g => Assert.Equal(11, Assert.IsType<SceneText>(Assert.Single(g.Children)).Runs[0].Font.Size));
        var startBox = new SceneRect(-15 - (one.Width / 2), 17.5 - 8.25, 9, 12);
        AssertBox(startBox.Union(new SceneRect(10, 77.5, 36, 12)), terminals.Bounds);
    }

    /// <summary>
    ///     Checks SVG terminal labels: the start label centered on its text box, the end label's box starting at its
    ///     position, and no labels for empty texts.
    /// </summary>
    [Fact]
    public void EdgeBuilder_DrawTerminals_SvgLabels_StartCenteredOnItsTextBox()
    {
        // Arrange
        var context = RenderingTestContext.CreateClass("---\nconfig:\n  htmlLabels: false\n---\nclassDiagram\n  class A\n");
        var edge = Relation("none", "none");
        var drawing = new EdgeDrawing([], null, null, [new(0, 0), new(0, 100)], "M0,0L0,100");
        var label = EdgeBuilder.MeasureTerminal(edge, "0..1", context);

        // Act
        var start = EdgeBuilder.DrawTerminals(edge, "r", "0..1", string.Empty, "none", "none", drawing, context);
        var end = EdgeBuilder.DrawTerminals(edge, "r", string.Empty, "0..1", "none", "none", drawing, context);
        var none = EdgeBuilder.DrawTerminals(edge, "r", string.Empty, string.Empty, "none", "none", drawing, context);

        // Assert
        Assert.False(label.IsHtml);
        Assert.Equal(-15, start.Bounds!.Value.X + (start.Bounds.Value.Width / 2), 6);
        Assert.Equal(17.5, start.Bounds.Value.Y + (start.Bounds.Value.Height / 2), 6);
        AssertBox(label.Box.Offset(10, 77.5), end.Bounds);
        Assert.Empty(none.Items);
        Assert.Null(none.Bounds);
    }

    /// <summary>
    ///     Makes a visible edge with an arrowhead at its end and a linear curve.
    /// </summary>
    /// <returns>The edge.</returns>
    private static LayoutEdge Edge() => new()
    {
        Id = "e",
        Start = "a",
        End = "b",
        Type = "arrow_point",
        Thickness = "normal",
        Pattern = "normal",
        Classes = "edge-thickness-normal edge-pattern-solid flowchart-link",
        ArrowTypeEnd = "arrow_point",
        Curve = "linear",
    };

    /// <summary>
    ///     Makes a class relation with a linear curve.
    /// </summary>
    /// <param name="start">The arrow type at the start.</param>
    /// <param name="end">The arrow type at the end.</param>
    /// <returns>The edge.</returns>
    private static LayoutEdge Relation(string start, string end) => new()
    {
        Id = "r",
        Start = "A",
        End = "B",
        Type = "normal",
        Thickness = "normal",
        Pattern = "solid",
        Classes = "relation",
        ArrowTypeStart = start,
        ArrowTypeEnd = end,
        Curve = "linear",
    };

    /// <summary>
    ///     Asserts that a box is the expected one within 1e-6.
    /// </summary>
    /// <param name="expected">The expected box.</param>
    /// <param name="actual">The actual box.</param>
    private static void AssertBox(SceneRect expected, SceneRect? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.X, actual.Value.X, 4);
        Assert.Equal(expected.Y, actual.Value.Y, 4);
        Assert.Equal(expected.Width, actual.Value.Width, 4);
        Assert.Equal(expected.Height, actual.Value.Height, 4);
    }
}
