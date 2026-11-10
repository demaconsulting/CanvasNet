namespace DemaConsulting.CanvasNet.Mermaid.State;

/// <summary>
///     The <c>State</c> namespace reads state diagrams (<c>stateDiagram-v2</c> and <c>stateDiagram</c>): a port of
///     Mermaid's <c>stateDiagram.jison</c> grammar, of its state database (<c>stateDb.ts</c>, with
///     <c>stateCommon.ts</c>) and of its data fetcher (<c>dataFetcher.ts</c>).
/// </summary>
/// <remarks>
///     <para>
///         <c>StateParser</c> lexes and parses the preprocessed text (with the token kinds of <c>StateTokens</c>) into
///         Mermaid's statement tree: states with descriptions and classes, transitions with labels, composite states with
///         their direction and concurrent regions, fork, join and choice states, notes, style classes, <c>style</c>,
///         <c>class</c> and <c>click</c> statements, the directions and the accessibility texts. <c>StateModel</c> then
///         applies the tree as Mermaid's <c>setRootDoc</c> does: the translation of start and end states and of
///         concurrent regions, the database of the top-level statements, and the data fetcher, which makes the nodes and
///         edges of the layout with Mermaid's ids, shapes, cluster classes, note groups and note edges.
///     </para>
///     <para>
///         <c>StateModel.GetData</c> maps the nodes and edges to the shared layout data of the unified renderer, as
///         Mermaid's <c>getData</c> does.
///     </para>
///     <para>
///         The namespace is internal to the package. A parsed model is never changed after parsing and is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
