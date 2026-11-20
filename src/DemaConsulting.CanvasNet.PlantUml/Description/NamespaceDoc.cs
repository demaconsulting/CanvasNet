namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <summary>
///     The <c>Description</c> namespace draws PlantUML's description diagrams (the use case, component, and deployment
///     diagrams): <c>DescriptionParser</c> reads the statements into a <c>DescriptionModel</c>, which lowers the diagram
///     for the shared Svek engine at every measure or render call, and <c>DescriptionRenderer</c> measures and paints the
///     elements and the containers.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads every statement of PlantUML's description diagrams (the elements of every symbol with their
///         names, stereotypes, and colors, the shorthands <c>[C]</c>, <c>() I</c>, <c>(U)</c>, and <c>:A:</c>,
///         containers with braces, links, notes, page texts, the direction, and the skin parameters). The renderer
///         draws the components with their icon, the interface circles with their names below them, the actors and use
///         cases, the leaf forms of every other symbol, and the large forms of the container symbols; the action, the
///         process, and archimate elements are drawn as rectangles and reported.
///     </para>
///     <para>
///         The namespace is internal to the package; the model is immutable once parsed and the parser and the
///         renderer keep no state between calls, so every type is safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
