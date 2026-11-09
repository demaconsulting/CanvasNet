using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Builders shared by the dagre unit tests.
/// </summary>
internal static class DagreTestGraphs
{
    /// <summary>
    ///     Creates a meter without a practical limit.
    /// </summary>
    /// <returns>The meter.</returns>
    public static LayoutWorkMeter Meter() => new(long.MaxValue, default, CancellationToken.None);

    /// <summary>
    ///     Creates an internal layout graph whose new nodes get an empty label and whose new edges get minimum length and
    ///     weight 1, as dagre's unit tests set up their graphs.
    /// </summary>
    /// <param name="multigraph">Whether the graph is a multigraph.</param>
    /// <param name="compound">Whether the graph is compound.</param>
    /// <returns>The graph.</returns>
    public static Graph<LayoutGraphLabel, LayoutNode, LayoutEdge> Layout(bool multigraph = true, bool compound = false) =>
        new Graph<LayoutGraphLabel, LayoutNode, LayoutEdge>(multigraph: multigraph, compound: compound)
            .SetGraph(new LayoutGraphLabel())
            .SetDefaultNodeLabel(_ => new LayoutNode())
            .SetDefaultEdgeLabel((_, _, _) => new LayoutEdge { MinLen = 1, Weight = 1 });

    /// <summary>
    ///     Creates an input graph as Mermaid creates it: a compound multigraph with an empty default edge label.
    /// </summary>
    /// <param name="graph">The graph attributes, or null for none.</param>
    /// <returns>The graph.</returns>
    public static Graph<DagreGraph, DagreNode, DagreEdge> Input(DagreGraph? graph = null) =>
        new Graph<DagreGraph, DagreNode, DagreEdge>(multigraph: true, compound: true)
            .SetGraph(graph ?? new DagreGraph())
            .SetDefaultEdgeLabel((_, _, _) => new DagreEdge());

    /// <summary>
    ///     Creates a node label of a size.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The label.</returns>
    public static DagreNode Node(double width, double height) => new() { Width = width, Height = height };

    /// <summary>
    ///     Creates a layout node label with a rank and an order.
    /// </summary>
    /// <param name="rank">The rank.</param>
    /// <param name="order">The order.</param>
    /// <returns>The label.</returns>
    public static LayoutNode Ranked(double rank, int order = 0) => new() { Rank = rank, Order = order };
}
