namespace DemaConsulting.CanvasNet.Mermaid.Class;

/// <summary>
///     The <c>Class</c> namespace reads class diagrams (<c>classDiagram</c>, <c>classDiagram-v2</c>): a port of Mermaid's
///     <c>classDiagram.jison</c> grammar, of its class database (<c>classDb.ts</c>) and of its class members
///     (<c>classTypes.ts</c>).
/// </summary>
/// <remarks>
///     <para>
///         <c>ClassParser</c> lexes and parses the preprocessed text (with the token kinds of <c>ClassTokens</c>) and
///         applies each statement to <c>ClassModel</c> where Mermaid's grammar runs its database action: classes with
///         their generic types, labels, annotations, attributes and methods, relations with their ends, lines,
///         cardinalities and labels, lollipop interfaces, notes, namespaces, style classes, <c>style</c> and
///         <c>cssClass</c>, the link and callback data (kept, never drawn), the direction and the accessibility texts.
///     </para>
///     <para>
///         For each render, <c>ClassModel.GetData</c> maps the model to the shared layout data of the unified renderer,
///         as Mermaid's <c>getData</c> does: namespaces as clusters, classes as class boxes, notes, invisible interface
///         nodes, note links and relations with their markers and terminal labels.
///     </para>
///     <para>
///         The namespace is internal to the package. A parsed model is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
