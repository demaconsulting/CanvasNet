using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Scenegraph;

/// <summary>
///     Unit tests for <see cref="ScenePositions"/> on hand-built scenes: group offsets and placement, the merge of items
///     with one tag, the arc-length midpoint of lines, curves and arcs, explicit aim points, the order, the parallel
///     index and the kind filter.
/// </summary>
public class ScenePositionsTests
{
    /// <summary>
    ///     Makes a rectangle path.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The segments.</returns>
    private static SceneSegment[] Box(double x, double y, double width, double height) =>
    [
        SceneSegment.Move(x, y),
        SceneSegment.Line(x + width, y),
        SceneSegment.Line(x + width, y + height),
        SceneSegment.Line(x, y + height),
        SceneSegment.Close(),
    ];

    /// <summary>
    ///     Validates that a tagged group's box is its children's bounds translated by every enclosing group, mapped by
    ///     the placement, and that its pixel is the center of the box.
    /// </summary>
    [Fact]
    public void ScenePositions_Collect_NestedGroupAndPlacement_MapsBoxAndCenter()
    {
        // Arrange: a node at (10, 20) inside a group translated by (100, 50), drawn at scale 2 with an offset
        var node = new SceneGroup(10, 20, [new ScenePath(Box(0, 0, 40, 30)) { Role = "shape" }])
        {
            Role = "node-group",
            Position = new ScenePositionTag("node", "A") { Text = "Alpha", Location = new MermaidLocation(null, 3, 1) },
        };
        var scene = new Scene(new SceneRect(0, 0, 200, 200), [new SceneGroup(100, 50, [node]) { Role = "nodes" }]);

        // Act
        var position = Assert.Single(ScenePositions.Collect(scene, new ScenePlacement(2, 5, 7), null));

        // Assert: box (110, 70, 40, 30) scaled by 2 and moved by (5, 7)
        Assert.Equal(("node", "A", "Alpha", 3), (position.Kind, position.Id, position.Text, position.Line));
        Assert.Equal(new PositionBox(225, 147, 80, 60), position.Box);
        Assert.Equal(new PositionPoint(265, 177), position.Pixel);
        Assert.Null(position.Index);
    }

    /// <summary>
    ///     Validates the arc-length midpoint of a straight polyline, joined over the pieces of one edge.
    /// </summary>
    [Fact]
    public void ScenePositions_Collect_EdgeInPieces_MidpointOfCombinedLength()
    {
        // Arrange: one edge drawn as two pieces, 10 long and 30 long; the midpoint is 10 into the second piece
        var tag = new ScenePositionTag("edge", "e1") { From = "A", To = "A", AlongPath = true };
        var first = new ScenePath([SceneSegment.Move(0, 0), SceneSegment.Line(10, 0)]) { Role = "edge", Position = tag };
        var second = new ScenePath([SceneSegment.Move(10, 0), SceneSegment.Line(10, 30)]) { Role = "edge", Position = tag };
        var scene = new Scene(new SceneRect(0, 0, 50, 50), [first, second]);

        // Act
        var position = Assert.Single(ScenePositions.Collect(scene, new ScenePlacement(1, 0, 0), null));

        // Assert
        Assert.Equal(new PositionBox(0, 0, 10, 30), position.Box);
        Assert.Equal(10, position.Pixel!.Value.X, 3);
        Assert.Equal(10, position.Pixel.Value.Y, 3);
        Assert.Equal(0, position.Index);
    }

    /// <summary>
    ///     Validates the arc-length midpoint of a symmetric cubic curve and of a half-circle arc: the point at the apex.
    /// </summary>
    [Fact]
    public void ScenePositions_PathMidpoint_SymmetricCubicAndArc_IsAtTheApex()
    {
        // Arrange
        SceneSegment[] cubic = [SceneSegment.Move(0, 0), SceneSegment.Cubic(0, -40, 40, -40, 40, 0)];
        SceneSegment[] arc = [SceneSegment.Move(0, 0), SceneSegment.Arc(20, 20, 0, false, true, 40, 0)];

        // Act
        var cubicMid = ScenePositions.PathMidpoint([(cubic, 0, 0)]);
        var arcMid = ScenePositions.PathMidpoint([(arc, 0, 0)]);

        // Assert: the cubic peaks at y = -30, the arc (clockwise on screen from left to right) at y = -20
        Assert.Equal(20, cubicMid!.Value.X, 2);
        Assert.Equal(-30, cubicMid.Value.Y, 2);
        Assert.Equal(20, arcMid!.Value.X, 2);
        Assert.Equal(-20, arcMid.Value.Y, 2);
    }

    /// <summary>
    ///     Validates that an explicit aim point is used (a slice's mid-angle) and that values are reported.
    /// </summary>
    [Fact]
    public void ScenePositions_Collect_ExplicitAim_UsesTheAimAndValue()
    {
        // Arrange: a slice in a group at (100, 100) aimed at (5, -10) in its own coordinates
        var slice = new ScenePath(Box(0, -20, 20, 20))
        {
            Role = "pie-slice",
            Position = new ScenePositionTag("slice", "Dogs") { Value = 386, Aim = (5, -10) },
        };
        var scene = new Scene(new SceneRect(0, 0, 200, 200), [new SceneGroup(100, 100, [slice]) { Role = "pie" }]);

        // Act
        var position = Assert.Single(ScenePositions.Collect(scene, new ScenePlacement(1, 0, 0), null));

        // Assert
        Assert.Equal(new PositionPoint(105, 90), position.Pixel);
        Assert.True(position.TryGetValue("value", out var value));
        Assert.Equal(PositionValue.FromNumber(386), value);
    }

    /// <summary>
    ///     Validates the order (kind priority, then line, then drawing order), the parallel index and the kind filter.
    /// </summary>
    [Fact]
    public void ScenePositions_Collect_KindsLinesAndFilter_OrdersAndNumbers()
    {
        // Arrange: two parallel edges drawn before the nodes, and nodes drawn against their line order
        ScenePath Edge(string id, int line) => new([SceneSegment.Move(0, 0), SceneSegment.Line(10, 0)])
        {
            Role = "edge",
            Position = new ScenePositionTag("edge", id) { From = "A", To = "B", AlongPath = true, Location = new MermaidLocation(null, line, 1) },
        };
        ScenePath Node(string id, int line) => new(Box(0, 0, 5, 5))
        {
            Role = "node",
            Position = new ScenePositionTag("node", id) { Location = new MermaidLocation(null, line, 1) },
        };
        var scene = new Scene(new SceneRect(0, 0, 50, 50), [Edge("e2", 4), Edge("e1", 3), Node("B", 2), Node("A", 1)]);

        // Act
        var all = ScenePositions.Collect(scene, new ScenePlacement(1, 0, 0), null);
        var edgesOnly = ScenePositions.Collect(scene, new ScenePlacement(1, 0, 0), kind => kind == "edge");

        // Assert
        Assert.Equal(["A", "B", "e1", "e2"], all.Select(position => position.Id));
        Assert.Equal([null, null, 0, 1], all.Select(position => position.Index));
        Assert.Equal(["e1", "e2"], edgesOnly.Select(position => position.Id));
    }

    /// <summary>
    ///     Validates that untagged items report nothing and that the handler is not needed for collection.
    /// </summary>
    [Fact]
    public void ScenePositions_Report_NoHandler_DoesNothing()
    {
        // Arrange
        var scene = new Scene(new SceneRect(0, 0, 10, 10), [new ScenePath(Box(0, 0, 5, 5)) { Role = "node" }]);
        var reported = new List<ImagePosition>();

        // Act
        ScenePositions.Report(scene, new ScenePlacement(1, 0, 0), new MermaidRenderOptions());
        ScenePositions.Report(scene, new ScenePlacement(1, 0, 0), new MermaidRenderOptions { PositionHandler = reported.Add });

        // Assert
        Assert.Empty(reported);
    }
}
