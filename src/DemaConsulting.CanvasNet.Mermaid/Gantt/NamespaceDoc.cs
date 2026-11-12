namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     The <c>Gantt</c> namespace reads and draws gantt charts (<c>gantt</c>): a port of Mermaid's <c>gantt.jison</c>
///     grammar, of its gantt database (<c>ganttDb.js</c>, with the dayjs functions it uses) and of its renderer
///     (<c>ganttRenderer.js</c>, with the d3 time scale, time intervals, time format and axis it uses).
/// </summary>
/// <remarks>
///     <para>
///         <c>GanttParser</c> ports the jison lexer and grammar and fills a <c>GanttModel</c>, Mermaid's gantt database,
///         which compiles the task dates of each render (with <c>Dayjs</c>, the dayjs functions Mermaid bundles, and the
///         clock of <see cref="MermaidRenderOptions.Now"/>). <c>GanttRenderer</c> lays the chart out and describes it as a
///         scene with the paint of <c>GanttStyles</c>.
///     </para>
///     <para>
///         <c>TimeAxis</c> is the time arithmetic the chart is built on: <c>JsDate</c> is a JavaScript <c>Date</c> whose
///         local time zone is UTC, <c>TimeInterval</c> holds d3-time's intervals (milliseconds to years, the seven week
///         intervals and the filtered <c>every</c> intervals), and <c>TimeAxis</c> adds d3-time's tick generator,
///         d3-array's <c>tickStep</c>, d3-scale's time scale with <c>rangeRound</c>, and d3-time-format's
///         <c>timeFormat</c> with the en-US locale.
///     </para>
///     <para>
///         The namespace is internal to the package. Every type is immutable or used by one call on one thread, and the
///         static functions are safe for concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
