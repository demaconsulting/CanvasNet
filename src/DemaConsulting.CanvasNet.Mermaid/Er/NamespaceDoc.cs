namespace DemaConsulting.CanvasNet.Mermaid.Er;

/// <summary>
///     The <c>Er</c> namespace reads ER diagrams (<c>erDiagram</c>): a port of Mermaid's <c>erDiagram.jison</c> grammar and
///     of its ER database (<c>erDb.ts</c>, with <c>erTypes.ts</c>).
/// </summary>
/// <remarks>
///     <para>
///         <c>ErParser</c> lexes and parses the preprocessed text (with the token kinds of <c>ErTokens</c>) and fills an
///         <c>ErModel</c> through the calls Mermaid's grammar actions make: entities with aliases, classes and attribute
///         blocks, relationships with their cardinalities, type and role, style classes, <c>class</c> and <c>style</c>
///         statements, subgraphs with their titles, directions and nodes, the direction and the accessibility texts.
///     </para>
///     <para>
///         <c>ErModel.GetData</c> maps the database to the shared layout data of the unified renderer, as Mermaid's
///         <c>getData</c> does: subgraphs as clusters (the last one first), entities as <c>erBox</c> nodes with their
///         alias and attributes, and relationships as edges with the ER markers of their cardinalities.
///     </para>
///     <para>
///         The namespace is internal to the package. A parsed model is never changed after parsing and is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
