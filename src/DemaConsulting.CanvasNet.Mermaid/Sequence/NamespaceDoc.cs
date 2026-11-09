namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     The <c>Sequence</c> namespace reads sequence diagrams (<c>sequenceDiagram</c>): a port of Mermaid's
///     <c>sequenceDiagram.jison</c> grammar and of its sequence database (<c>sequenceDb.ts</c>).
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
///         The namespace is internal to the package. A parsed model is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
