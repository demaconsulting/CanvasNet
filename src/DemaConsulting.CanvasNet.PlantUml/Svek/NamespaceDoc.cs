namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The <c>Svek</c> namespace is the shared engine of the PlantUML diagrams that PlantUML lays out with Graphviz
///     (class, object, state, use case, component, and deployment diagrams); PlantUML calls this engine "svek".
/// </summary>
/// <remarks>
///     <para>
///         A diagram subsystem lowers its model to a <c>SvekDiagram</c> for every measure or render call: entities with
///         their size, clip shape, and painter (<c>ISvekEntityImage</c>), nested clusters with their outline and title
///         band (<c>ISvekClusterImage</c>), notes attached to entities or links or floating, and links with a
///         decoration at each end, a line style, labels, a length, and a direction hint. <c>SvekSyntax</c> holds the
///         statement readers that every such diagram shares (names, stereotypes, colors, the link arrow grammar,
///         notes, page texts, direction, scale, and hide and show); <c>SvekLayout</c> lays the diagram out with the
///         dagre layered layout of the shared Layout package under the PlantUML work meter
///         <c>PlantUmlLayoutWorkMeter</c>; <c>SvekGeometry</c> turns routes into curves, clips them to the entity
///         shapes, and builds the link decorations; and <c>SvekRenderer</c> draws the clusters, notes, and links and
///         calls the entity painters.
///     </para>
///     <para>
///         The namespace is internal to the package; the readers, the layout, and the renderer keep no state between
///         calls, so every type is safe for concurrent use; a <c>SvekDiagram</c> belongs to one call.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
