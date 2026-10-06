namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Charts"/> namespace provides a public,
///     format-agnostic charting data model (<see cref="Chart"/>, <see cref="ChartSeries"/>,
///     <see cref="ChartAxis"/>, <see cref="ChartLegend"/>, <see cref="ChartTitle"/>, and
///     <see cref="ChartType"/>) together with an ergonomic, fluent construction API
///     (<see cref="ChartBuilder"/>). This namespace is distributed as the separate
///     <c>DemaConsulting.CanvasNet.Charts</c> NuGet package, which references only the core
///     <c>DemaConsulting.CanvasNet</c> package (specifically its
///     <see cref="DemaConsulting.CanvasNet.Canvas"/> namespace, for the
///     <see cref="DemaConsulting.CanvasNet.Canvas.Rgba32"/> color type used by series/point
///     colors and the default color palette).
/// </summary>
/// <remarks>
///     <para>
///     This library's immutable data-model types (<see cref="Chart"/>, <see cref="ChartSeries"/>,
///     <see cref="ChartAxis"/>, <see cref="ChartLegend"/>, and <see cref="ChartTitle"/>) are each a
///     <see langword="sealed"/> class with a single validating constructor and read-only
///     properties, matching the convention already established by the core
///     <c>DemaConsulting.CanvasNet</c> package's own public immutable value types (for example
///     <see cref="DemaConsulting.CanvasNet.Drawing.StrokeStyle"/> and
///     <see cref="DemaConsulting.CanvasNet.Drawing.TilePaint"/>): every constructor argument is
///     validated eagerly and rejected outright (never silently clamped or coerced), and every
///     caller-supplied collection argument is defensively copied into an immutable snapshot so the
///     constructed instance's state cannot be mutated by the caller after construction.
///     <see cref="ChartBuilder"/> is the one concession to a fluent, mutable-until-built API: it
///     performs no data-shape validation of its own, delegating every invariant to the model
///     types' own constructors, so no validation rule is ever duplicated or able to drift out of
///     sync between the builder and the model.
///     </para>
///     <para>
///     This library imposes a hard, structural constraint: it must never reference
///     <c>DemaConsulting.CanvasNet.Pptx</c>, <c>DemaConsulting.CanvasNet.Pdf</c>,
///     <c>DemaConsulting.CanvasNet.Svg</c>, or any future <c>DemaConsulting.CanvasNet.Vsdx</c>
///     package. Keeping the dependency direction one-way (those packages may depend on
///     <c>Charts</c>, but <c>Charts</c> may never depend on any of them) is what keeps this
///     chart data model and its eventual renderer and OOXML adapter genuinely format-agnostic and
///     reusable by any future document-format library that needs to render a chart, rather than
///     coupled to one specific host document format.
///     </para>
///     <para>
///     As of this release, this namespace provides only the data model and builder described
///     above; rendering a <see cref="Chart"/> onto a
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> and parsing an OOXML
///     <c>chart1.xml</c> part into a <see cref="Chart"/> are not yet implemented.
///     </para>
/// </remarks>
/// <example>
///     Building an immutable <see cref="Chart"/> describing a simple two-category bar chart with
///     a title, using <see cref="ChartBuilder"/>'s fluent API:
///     <code>
///     using DemaConsulting.CanvasNet.Charts;
///
///     var chart = new ChartBuilder()
///         .OfType(ChartType.Bar)
///         .WithCategoryAxis(["Q1", "Q2"])
///         .AddSeries("Revenue", [120.0, 150.0])
///         .WithTitle("Quarterly Revenue")
///         .Build();
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
