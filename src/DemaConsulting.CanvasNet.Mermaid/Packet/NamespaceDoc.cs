namespace DemaConsulting.CanvasNet.Mermaid.Packet;

/// <summary>
///     The <c>Packet</c> namespace reads and lays out packet diagrams (<c>packet</c> and <c>packet-beta</c>): a port of the
///     Langium packet grammar of <c>@mermaid-js/parser</c>, of Mermaid's packet database (<c>db.ts</c> and the
///     <c>populate</c> and <c>getNextFittingBlock</c> of <c>parser.ts</c>) and of its packet renderer
///     (<c>renderer.ts</c>).
/// </summary>
/// <remarks>
///     <para>
///         <c>PacketParser</c> lexes and parses the preprocessed text and fills a <c>PacketDiagram</c> as Mermaid's
///         <c>populate</c> does: the blocks in order, each contiguous with the one before it, with a non-zero bit count
///         and an end not before its start, the title and the accessibility texts. <c>PacketDiagram.Rows</c> splits the
///         blocks into rows of the configured <c>bitsPerRow</c> as Mermaid does, when the diagram is drawn.
///     </para>
///     <para>
///         <c>PacketRenderer</c> draws the diagram as Mermaid's <c>draw</c> does: a row of block rectangles with their
///         labels and bit numbers for each row, and the title below them, with the packet style sheet.
///     </para>
///     <para>
///         The namespace depends on the <c>Syntax</c> (scanning, sanitizing), <c>Configuration</c> (the packet section and
///         style sheet), <c>Rendering</c> (text metrics), <c>Dagre</c> (the work meter) and <c>Scenegraph</c> namespaces. It
///         is internal to the package. A parsed diagram is never changed after parsing and is safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
