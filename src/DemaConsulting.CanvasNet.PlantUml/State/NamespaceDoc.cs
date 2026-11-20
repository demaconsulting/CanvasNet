namespace DemaConsulting.CanvasNet.PlantUml.State;

/// <summary>
///     The <c>State</c> namespace draws PlantUML's state diagrams: <c>StateParser</c> reads the statements into a
///     <c>StateModel</c>, which lowers the diagram for the shared Svek engine at every measure or render call, and
///     <c>StateRenderer</c> measures and paints the states, the pseudo-states, and the composite states.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads PlantUML's state diagram statements (states with their names, descriptions, stereotypes,
///         and colors, transitions, composite states with their concurrent regions, the start, end, and history
///         pseudo-states, choices, forks and joins, notes, <c>hide empty description</c>, page texts, the direction,
///         and the skin parameters). The renderer draws the rounded state boxes with their name band, the composite
///         states with their header, and the circles, diamonds, and bars of the pseudo-states.
///     </para>
///     <para>
///         The namespace is internal to the package; the model is immutable once parsed and the parser and the
///         renderer keep no state between calls, so every type is safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
