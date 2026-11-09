using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Scenegraph;

/// <summary>
///     Unit tests for the scene model: rectangles, segments, bounds and the conversion to core paths.
/// </summary>
public class SceneModelTests
{
    /// <summary>
    ///     Validates the rectangle helpers.
    /// </summary>
    [Fact]
    public void SceneRect_UnionAndOffset_ComputeEdges()
    {
        // Arrange
        var a = new SceneRect(0, 0, 10, 5);
        var b = new SceneRect(-2, 3, 4, 10);

        // Act
        var union = a.Union(b);
        var moved = a.Offset(1, 2);

        // Assert
        Assert.Equal(new SceneRect(-2, 0, 12, 13), union);
        Assert.Equal((11.0, 7.0), (moved.Right, moved.Bottom));
    }

    /// <summary>
    ///     Validates the bounds of a scene: groups translate their children, text and pictures count their boxes, and the
    ///     bounds of a curve are those of the curve, not of its control points (as SVG's <c>getBBox</c>).
    /// </summary>
    [Fact]
    public void Scene_BoundsOf_ItemsAndCurves_AreGeometryBounds()
    {
        // Arrange: a cubic whose control points reach y = -40 but whose curve reaches y = -30
        var curve = new ScenePath([SceneSegment.Move(0, 0), SceneSegment.Cubic(0, -40, 40, -40, 40, 0)]) { Role = "edge" };
        var text = new SceneText(0, 0, SceneTextAnchor.Start, [], new SceneRect(50, 0, 10, 10)) { Role = "label" };
        var group = new SceneGroup(100, 100, [new SceneImage(new SceneRect(0, 0, 5, 5), "a.png") { Role = "image" }]) { Role = "node" };

        // Act
        var bounds = Scene.BoundsOf([curve, text, group]);

        // Assert
        var b = Assert.IsType<SceneRect>(bounds);
        Assert.Equal(0, b.X, 3);
        Assert.Equal(-30, b.Y, 1);
        Assert.Equal(105, b.Right, 3);
        Assert.Equal(105, b.Bottom, 3);
        Assert.Null(Scene.BoundsOf([]));
        Assert.Null(Scene.BoundsOf([new ScenePath([]) { Role = "edge" }]));
    }

    /// <summary>
    ///     Validates the conversion to a core path: scale and offset are applied, an arc is converted, and a path that does
    ///     not start with a move starts at the origin.
    /// </summary>
    [Fact]
    public void Scene_ToPath_AppliesPlacementAndArcs()
    {
        // Arrange: a half circle of radius 10 from (0, 0) to (20, 0), drawn above the line
        IReadOnlyList<SceneSegment> segments = [SceneSegment.Arc(10, 10, 0, false, true, 20, 0), SceneSegment.Close()];

        // Act
        var bounds = Scene.ToPath(segments, 2, 5, 50).GetBounds(0.01f);

        // Assert: scaled by 2 and moved by (5, 50); the arc reaches 20 device pixels above the chord
        Assert.Equal(5, bounds.X, 1);
        Assert.Equal(45, bounds.Right, 1);
        Assert.Equal(30, bounds.Y, 1);
        Assert.Equal(50, bounds.Bottom, 1);
    }

    /// <summary>
    ///     Validates the segment factories.
    /// </summary>
    [Fact]
    public void SceneSegment_Factories_FillTheDocumentedFields()
    {
        // Arrange / Act
        var quad = SceneSegment.Quadratic(1, 2, 3, 4);
        var cubic = SceneSegment.Cubic(1, 2, 3, 4, 5, 6);
        var arc = SceneSegment.Arc(7, 8, 30, true, false, 9, 10);

        // Assert
        Assert.Equal((SceneSegmentKind.QuadraticTo, 3.0, 4.0, 1.0, 2.0), (quad.Kind, quad.X, quad.Y, quad.X1, quad.Y1));
        Assert.Equal((5.0, 6.0, 3.0, 4.0), (cubic.X, cubic.Y, cubic.X2, cubic.Y2));
        Assert.Equal((7.0, 8.0, 30.0, true, false), (arc.X1, arc.Y1, arc.X2, arc.LargeArc, arc.Sweep));
        Assert.Equal(SceneSegmentKind.Close, SceneSegment.Close().Kind);
    }
}
