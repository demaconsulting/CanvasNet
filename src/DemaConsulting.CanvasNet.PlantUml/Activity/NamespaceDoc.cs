namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     The <c>Activity</c> namespace draws PlantUML activity diagrams of the new syntax.
/// </summary>
/// <remarks>
///     <para>
///         <c>ActivityParser</c> reads the activity statements into an <c>ActivityModel</c> (a tree of instructions:
///         actions, start and stop nodes, ifs, loops, forks, splits, groups, and their swimlanes and notes);
///         <c>ActivityLayout</c> composes the instructions into tiles and places them as PlantUML's "ftile" engine
///         does, then compresses the empty space and frames the drawing; <c>ActivityRenderer</c> draws the placed
///         shapes as a scene with PlantUML's look. No graph layout is involved. The model is the diagram that the
///         document's registry returns for the <c>activity</c> type.
///     </para>
///     <para>
///         The namespace is internal to the package; the model is not changed after parsing and the layout and
///         renderer keep no state between calls, so every type is safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
