namespace DemaConsulting.CanvasNet.Mermaid.Pie;

/// <summary>
///     The <c>Pie</c> namespace reads and lays out pie charts (<c>pie</c>): a port of the Langium pie grammar of
///     <c>@mermaid-js/parser</c>, of Mermaid's pie database (<c>pieDb.ts</c>) and of its pie renderer
///     (<c>pieRenderer.ts</c>, with d3's <c>pie</c>, <c>arc</c> and <c>scaleOrdinal</c>).
/// </summary>
/// <remarks>
///     <para>
///         <c>PieParser</c> lexes and parses the preprocessed text and fills a <c>PieDiagram</c> as Mermaid's
///         <c>populateDb</c> does: the sections in order (each label once, no negative value), <c>showData</c>, the
///         title and the accessibility texts.
///     </para>
///     <para>
///         <c>PieRenderer</c> draws the diagram as Mermaid's <c>draw</c> does: the outer circle, the slices and their
///         percentage labels, the title and the legend in its position, with the pie style sheet and the view box that
///         takes in the legend and the title.
///     </para>
///     <para>
///         The namespace is internal to the package. A parsed diagram is never changed after parsing and is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
