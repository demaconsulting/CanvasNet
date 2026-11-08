using DemaConsulting.CanvasNet.Mermaid.Dagre;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="DagreCoordinateSystem"/>, with the cases of dagre's <c>coordinate-system.test.js</c>.
/// </summary>
public class DagreCoordinateSystemTests
{
    /// <summary>
    ///     Validates that adjust swaps the sizes only for a horizontal rank direction.
    /// </summary>
    /// <param name="rankDir">The rank direction.</param>
    /// <param name="swapped">Whether the sizes are swapped.</param>
    [Theory]
    [InlineData("TB", false)]
    [InlineData("BT", false)]
    [InlineData("LR", true)]
    [InlineData("rl", true)]
    public void DagreCoordinateSystem_Adjust_RankDir_SwapsSizesWhenHorizontal(string rankDir, bool swapped)
    {
        // Arrange
        var g = Layout();
        g.GraphLabel!.RankDir = rankDir;
        g.SetNode("a", new LayoutNode { Width = 100, Height = 200 });
        g.SetEdge("a", "b", new LayoutEdge { Width = 10, Height = 20 });

        // Act
        DagreCoordinateSystem.Adjust(g);

        // Assert
        var expected = swapped ? (200.0, 100.0, 20.0, 10.0) : (100.0, 200.0, 10.0, 20.0);
        Assert.Equal(expected, (g.Node("a")!.Width, g.Node("a")!.Height, g.Edge("a", "b")!.Width, g.Edge("a", "b")!.Height));
    }

    /// <summary>
    ///     Validates that undo mirrors and transposes nodes, routes and labels for the rank direction.
    /// </summary>
    /// <param name="rankDir">The rank direction.</param>
    /// <param name="x">The expected node x.</param>
    /// <param name="y">The expected node y.</param>
    /// <param name="width">The expected node width.</param>
    [Theory]
    [InlineData("TB", 100, 200, 20)]
    [InlineData("BT", 100, -200, 20)]
    [InlineData("LR", 200, 100, 40)]
    [InlineData("RL", -200, 100, 40)]
    public void DagreCoordinateSystem_Undo_RankDir_MirrorsAndTransposes(string rankDir, double x, double y, double width)
    {
        // Arrange
        var g = Layout();
        g.GraphLabel!.RankDir = rankDir;
        g.SetNode("a", new LayoutNode { X = 100, Y = 200, Width = 20, Height = 40 });
        var edge = new LayoutEdge { X = 100, Y = 200, Points = [new DagrePoint(100, 200)] };
        g.SetEdge("a", "b", edge);
        g.Node("b")!.X = 0;
        g.Node("b")!.Y = 0;

        // Act
        DagreCoordinateSystem.Undo(g);

        // Assert
        Assert.Equal((x, y, width), (g.Node("a")!.X, g.Node("a")!.Y, g.Node("a")!.Width));
        Assert.Equal((x, y), (edge.Points[0].X, edge.Points[0].Y));
        Assert.Equal((x, y), (edge.X.Value, edge.Y.Value));
    }
}
