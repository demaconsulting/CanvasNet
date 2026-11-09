// Portions derived from dagre-d3-es (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Graphlib;

/// <summary>
///     The identity of a graph edge: graphlib's frozen <c>{ v, w, name }</c> edge object.
/// </summary>
/// <remarks>
///     In an undirected graph <see cref="V"/> is the lesser of the two ends in ordinal order, as graphlib stores it. The
///     name tells parallel edges of a multigraph apart and is null for an unnamed edge.
/// </remarks>
/// <param name="V">The tail node.</param>
/// <param name="W">The head node.</param>
/// <param name="Name">The edge name, or null.</param>
internal sealed record GraphEdge(string V, string W, string? Name = null);
