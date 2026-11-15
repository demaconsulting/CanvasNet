namespace DemaConsulting.CanvasNet.Positions;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Positions"/> namespace describes where the features of a rendered picture
///     are (a node of a diagram, a bar of a chart), so that a caller can place a callout or a leader line on the picture
///     without probing its pixels.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="ImagePosition"/> is one feature: its kind, its id, a few facts that identify it (text, source line,
///         view, end points, values), its geometry box and its attachment pixel. The renderers that know features (the
///         Mermaid and Vega-Lite packages) report them through a <c>PositionHandler</c> callback of their render
///         options, in the pixels of the surface they return.
///     </para>
///     <para>
///         <see cref="PositionSelector"/> parses one selector text such as <c>node:Send</c>, <c>commit:*fix*</c> or
///         <c>mark:x=Nov+color=Americas</c>, <see cref="PositionSelection"/> is the union of several, and
///         <see cref="PositionCollector"/> keeps the positions that match, in delivery order, up to a cap, and says
///         which selectors matched nothing. The namespace has no global state; the model types are immutable and
///         thread-safe, and a collector belongs to one render.
///     </para>
/// </remarks>
/// <example>
///     Collect the positions a selection asks for:
///     <code language="csharp">
///     var selection = PositionSelection.Parse(["node:send", "edge:@from=pack"]);
///     var collector = new PositionCollector(selection);
///     collector.Offer(new ImagePosition("node", "send")
///     {
///         Box = new PositionBox(10, 20, 56, 30),
///         Pixel = new PositionPoint(38, 35)
///     });
///     // collector.Positions holds the node; collector.GetUnmatchedSelectors() returns "edge:@from=pack"
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
