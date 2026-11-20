namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <summary>
///     The <c>Class</c> namespace draws PlantUML's class and object diagrams: <c>ClassParser</c> reads the statements
///     into a <c>ClassModel</c>, which lowers the diagram for the shared Svek engine at every measure or render call,
///     and <c>ClassRenderer</c> measures and paints the class, object, and map boxes.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads PlantUML's class diagram statements (classes and their kin, members, relationships,
///         packages and namespaces, notes, hide and show, page texts, the direction, and the skin parameters) and the
///         object diagram statements (objects, their fields, and maps), which PlantUML's class diagram also reads. The
///         renderer draws the header of a class with its circled character, stereotypes, and generic parameters, the
///         member compartments with their visibility icons and separators, and the object and map tables.
///     </para>
///     <para>
///         The namespace is internal to the package; the model is immutable and the parser and the renderer keep no
///         state between calls, so every type is safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
