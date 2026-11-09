namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     The <c>Sequence</c> namespace reads and draws sequence diagrams (<c>sequenceDiagram</c>): a port of Mermaid's
///     <c>sequenceDiagram.jison</c> grammar, of its sequence database (<c>sequenceDb.ts</c>), of its sequence renderer
///     (<c>sequenceRenderer.ts</c>) and of its drawing functions (<c>svgDraw.js</c>).
/// </summary>
/// <remarks>
///     <para>
///         <c>SequenceParser</c> lexes and parses the preprocessed text (with the token kinds of <c>SequenceTokens</c>)
///         into the list of statements that Mermaid's grammar hands to its database, and <c>SequenceModel</c> applies
///         them as <c>sequenceDb.ts</c> does: participants with their types, aliases, boxes, links and properties,
///         messages with Mermaid's line types, notes, blocks, activations, created and destroyed participants, the
///         autonumber statements, the title and the accessibility texts. The model keeps every text with its explicit
///         <c>wrap:</c> or <c>nowrap:</c> prefix, so the configuration of each render decides the rest.
///     </para>
///     <para>
///         For each render, <c>SequenceLayout</c> sizes and places the participants, messages, notes, activations,
///         blocks, boxes and menus from top to bottom as <c>sequenceRenderer.ts</c> does, and <c>SequenceRenderer</c>
///         turns them into scene items with the paint of the theme's sequence style sheet, in Mermaid's paint order.
///     </para>
///     <para>
///         The namespace is internal to the package. A parsed model is immutable and safe for concurrent use; a layout and
///         a renderer serve one render on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
