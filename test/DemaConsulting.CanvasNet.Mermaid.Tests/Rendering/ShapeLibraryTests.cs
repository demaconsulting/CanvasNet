using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Tests of <see cref="ShapeLibrary"/>: the intersect functions, the exact box of path geometry, the style split, the
///     paint cascade and the shapes whose behavior the upstream vectors do not show.
/// </summary>
public sealed class ShapeLibraryTests
{
    /// <summary>The node box the intersect tests use: 100 by 50 around the origin.</summary>
    private static readonly NodeBox Box = new(0, 0, 100, 50);

    /// <summary>
    ///     Checks <c>intersect-rect.js</c>: the side the line leaves through decides the point.
    /// </summary>
    /// <param name="px">The point's x.</param>
    /// <param name="py">The point's y.</param>
    /// <param name="x">The expected x.</param>
    /// <param name="y">The expected y.</param>
    [Theory]
    [InlineData(200, 0, 50, 0)]
    [InlineData(0, -200, 0, -25)]
    [InlineData(100, 100, 25, 25)]
    [InlineData(-200, 50, -50, 12.5)]
    public void ShapeLibrary_IntersectRect_Point_LeavesThroughTheRightSide(double px, double py, double x, double y)
    {
        // Act
        var result = ShapeLibrary.IntersectRect(Box, new ShapePoint(px, py));

        // Assert
        Assert.Equal(x, result.X, 9);
        Assert.Equal(y, result.Y, 9);
    }

    /// <summary>
    ///     Checks <c>intersect-ellipse.js</c> on the axes and a diagonal.
    /// </summary>
    [Fact]
    public void ShapeLibrary_IntersectEllipse_Points_LieOnTheEllipse()
    {
        // Act
        var right = ShapeLibrary.IntersectEllipse(Box, 50, 25, new ShapePoint(300, 0));
        var up = ShapeLibrary.IntersectEllipse(Box, 50, 25, new ShapePoint(0, -300));
        var diagonal = ShapeLibrary.IntersectEllipse(Box, 50, 25, new ShapePoint(-300, 300));

        // Assert
        Assert.Equal(new ShapePoint(50, 0), right);
        Assert.Equal(new ShapePoint(0, -25), up);
        Assert.Equal(1, ((diagonal.X * diagonal.X) / 2500) + ((diagonal.Y * diagonal.Y) / 625), 9);
        Assert.True(diagonal.X < 0 && diagonal.Y > 0);
    }

    /// <summary>
    ///     Checks that <c>intersect-line.js</c> keeps the half-denominator rounding of its integer original: the crossing
    ///     of two lines at the origin comes out half a unit off.
    /// </summary>
    [Fact]
    public void ShapeLibrary_IntersectLine_Crossing_KeepsTheRoundingOffset()
    {
        // Act
        var result = ShapeLibrary.IntersectLine(new ShapePoint(-10, 0), new ShapePoint(10, 0), new ShapePoint(0, -10), new ShapePoint(0, 10));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.5, Math.Abs(result.Value.X), 9);
        Assert.Equal(0.5, Math.Abs(result.Value.Y), 9);
    }

    /// <summary>
    ///     Checks that segments on one side of each other, and parallel segments, do not intersect.
    /// </summary>
    [Fact]
    public void ShapeLibrary_IntersectLine_NoCrossing_GivesNull()
    {
        // Act
        var apart = ShapeLibrary.IntersectLine(new ShapePoint(0, 0), new ShapePoint(10, 0), new ShapePoint(0, 5), new ShapePoint(10, 6));
        var parallel = ShapeLibrary.IntersectLine(new ShapePoint(0, 0), new ShapePoint(10, 0), new ShapePoint(0, 0), new ShapePoint(5, 0));

        // Assert
        Assert.Null(apart);
        Assert.Null(parallel);
    }

    /// <summary>
    ///     Checks that the polygon intersection places the polygon in the node's box and returns the crossing nearest
    ///     the point; Mermaid's line test never rejects a crossing beyond the point, so a point inside still gives the
    ///     crossing in its direction.
    /// </summary>
    [Fact]
    public void ShapeLibrary_IntersectPolygon_Square_GivesNearestCrossing()
    {
        // Arrange: a square of 100 by 50 written from 0, placed by its smallest x and y
        List<ShapePoint> square = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)];

        // Act
        var right = ShapeLibrary.IntersectPolygon(Box, square, new ShapePoint(200, 0));
        var inside = ShapeLibrary.IntersectPolygon(Box, square, new ShapePoint(-10, 0));

        // Assert
        Assert.Equal(50.5, right.X, 9);
        Assert.Equal(-50.5, inside.X, 9);
        Assert.Equal(0.5, inside.Y, 9);
    }

    /// <summary>
    ///     Checks that the box of an arc includes its extremes, as <c>getBBox</c> does, and a radius too small for the
    ///     arc is scaled up as SVG requires.
    /// </summary>
    [Fact]
    public void ShapeLibrary_SegmentBounds_Arcs_IncludeTheirExtremes()
    {
        // Arrange: a half circle from (-10, 0) to (10, 0) over the top, and an arc whose radius is too small
        IReadOnlyList<SceneSegment> half = [SceneSegment.Move(-10, 0), SceneSegment.Arc(10, 10, 0, false, true, 10, 0)];
        IReadOnlyList<SceneSegment> small = [SceneSegment.Move(0, 0), SceneSegment.Arc(1, 1, 0, false, false, 20, 0)];

        // Act
        var top = ShapeLibrary.SegmentBounds(half);
        var scaled = ShapeLibrary.SegmentBounds(small);

        // Assert
        Assert.NotNull(top);
        Assert.Equal(-10, top.Value.Y, 9);
        Assert.Equal(10, top.Value.Height, 9);
        Assert.NotNull(scaled);
        Assert.Equal(10, scaled.Value.Height, 9);
    }

    /// <summary>
    ///     Checks <c>styles2String</c>'s split: class styles, then inline styles, a later key wins in its first place, and
    ///     text properties go to the label.
    /// </summary>
    [Fact]
    public void ShapeLibrary_SplitStyles_Styles_SplitsLabelFromNode()
    {
        // Arrange
        var node = new LayoutNode
        {
            Id = "A",
            Shape = "rect",
            CssCompiledStyles = ["fill:#f00", "color:blue"],
            CssStyles = [" stroke: #333", "fill:#0f0"],
        };

        // Act
        var (label, nodeStyles) = ShapeLibrary.SplitStyles(node);

        // Assert
        Assert.Equal(["color:blue"], label);
        Assert.Equal(["fill:#0f0", "stroke:#333"], nodeStyles);
    }

    /// <summary>
    ///     Checks that the node's own styles win over the style sheet on the shape.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_UserStyles_PaintTheShape()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "rect", Label = "A", Padding = 15, CssStyles = ["fill:#ff0000", "stroke-width:4px", "stroke-dasharray:5 5"] };

        // Act
        var result = ShapeLibrary.Draw(node, Context());

        // Assert
        var path = Assert.IsType<ScenePath>(result.Items[0]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), path.Fill);
        Assert.Equal(4, path.Stroke!.Width);
        Assert.Equal([5.0, 5.0], path.Stroke.Dash);
        Assert.Equal(new Rgba32(147, 112, 219, 255), path.Stroke.Color);
    }

    /// <summary>
    ///     Checks the documented choice for the legacy <c>ellipse</c> (<c>A(-text-)</c>): an ellipse around the padded
    ///     label with an elliptical intersect function.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_LegacyEllipse_IsAnEllipse()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "ellipse", Padding = 15 };

        // Act
        var result = ShapeLibrary.Draw(node, Context(fixedLabel: new SceneRect(0, 0, 40, 24)));

        // Assert
        Assert.Equal("ellipse", result.Handler);
        Assert.Equal(70, result.Width, 9);
        Assert.Equal(39, result.Height, 9);
        Assert.Equal(new ShapePoint(35, 0), result.Intersect(new NodeBox(0, 0, 70, 39), new ShapePoint(300, 0)));
    }

    /// <summary>
    ///     Checks that the <c>state</c> shape, whose rectangle Mermaid sizes as not a number, draws and measures only its
    ///     label.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_State_SizesToTheLabel()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "state", Padding = 15 };

        // Act
        var result = ShapeLibrary.Draw(node, Context(fixedLabel: new SceneRect(0, 0, 40, 24)));

        // Assert
        Assert.Equal(new SceneRect(-20, -12, 40, 24), result.Bounds);
        Assert.DoesNotContain(result.Items, item => item is ScenePath);
    }

    /// <summary>
    ///     Checks that a fork turns upright in a subgraph of direction <c>LR</c>.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ForkInLeftToRight_IsUpright()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "fork", Dir = "LR" };

        // Act
        var result = ShapeLibrary.Draw(node, Context());

        // Assert
        Assert.Equal(10, result.Width, 9);
        Assert.Equal(70, result.Height, 9);
    }

    /// <summary>
    ///     Checks that an image node without an image resolver is reported, keeps its frame and draws no picture.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_ImageWithoutResolver_ReportsAndDrawsNoPicture()
    {
        // Arrange
        var sink = new MermaidReportSink(100, false);
        var node = new LayoutNode { Id = "A", Shape = "imageSquare", Img = "pic.png", Label = string.Empty, AssetWidth = 60, AssetHeight = 40, Location = new MermaidLocation(null, 2, 5) };
        var context = Context(sink: sink, images: new NodeImages(null, 10, sink));
        var warnings = new List<MermaidDiagnostic>();

        // Act
        var result = ShapeLibrary.Draw(node, context);
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.DoesNotContain(result.Items, item => item is SceneImage);
        Assert.Equal(60, result.Width, 9);
        Assert.Equal(40, result.Height, 9);
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.ImageUnresolved, warning.Code);
        Assert.Equal(2, warning.Location.Line);
    }

    /// <summary>
    ///     Checks that a shape this version does not draw fails, located, with a suggestion.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_UndrawnShape_ThrowsRenderFailed()
    {
        // Arrange
        var node = new LayoutNode { Id = "A", Shape = "classBox", Location = new MermaidLocation(null, 3, 1) };

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => ShapeLibrary.Draw(node, Context()));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Equal(3, exception.Location.Line);
    }

    /// <summary>
    ///     Checks that every shape name a flowchart accepts is drawn.
    /// </summary>
    [Fact]
    public void ShapeLibrary_Draw_EveryDocumentedName_IsDrawn()
    {
        // Arrange
        var context = Context(fixedLabel: new SceneRect(0, 0, 40, 24));
        var names = ShapeNames.ShortNames.Concat(["state", "choice", "note", "composite", "anchor", "icon", "imageSquare", "iconSquare", "iconCircle", "iconRounded", "collapsedGroup", "ellipse"]);

        // Act
        var results = names.Select(name => ShapeLibrary.Draw(new LayoutNode { Id = "A", Shape = name, Label = "x", Padding = 15 }, context)).ToList();

        // Assert
        Assert.All(results, result => Assert.True(result.Width > 0 && result.Height > 0, result.Handler));
    }

    /// <summary>
    ///     Creates the context of a default flowchart.
    /// </summary>
    /// <param name="fixedLabel">A fixed label box instead of a measured one, or null.</param>
    /// <param name="sink">The sink, or null for a new one.</param>
    /// <param name="images">The pictures, or null.</param>
    /// <returns>The context.</returns>
    private static ShapeContext Context(SceneRect? fixedLabel = null, MermaidReportSink? sink = null, NodeImages? images = null)
    {
        var reports = sink ?? new MermaidReportSink(100, false);
        var config = MermaidConfigResolverVectorTests.ResolveLikeMermaid(new JsonObject(), "flowchart TD\n  A\n");
        var labels = new LabelBuilder(ScenePainter.CreateFontResolver(), reports, images);
        return new ShapeContext
        {
            Config = config,
            Styles = FlowchartStyles.Create(config, reports),
            MeasureLabel = fixedLabel is { } box ? request => LabelLayout.Of(request.HtmlLabels, box) : labels.Build,
            Images = images,
            Sink = reports,
        };
    }
}
