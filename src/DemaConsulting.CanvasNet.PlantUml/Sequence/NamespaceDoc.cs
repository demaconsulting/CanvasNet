namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <summary>
///     The <c>Sequence</c> namespace draws PlantUML sequence diagrams.
/// </summary>
/// <remarks>
///     <para>
///         <c>SequenceParser</c> reads the sequence statements into a <c>SequenceModel</c> (participants, boxes, and
///         events from top to bottom); <c>SequenceLayout</c> places the participant columns and the rows as PlantUML's
///         sequence engine does; <c>SequenceRenderer</c> draws the geometry as a scene with PlantUML's shapes. The model
///         is the diagram that the document's registry returns for the <c>sequence</c> type.
///     </para>
///     <para>
///         The namespace is internal to the package; the model is immutable and the layout and renderer keep no state
///         between calls, so every type is safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
