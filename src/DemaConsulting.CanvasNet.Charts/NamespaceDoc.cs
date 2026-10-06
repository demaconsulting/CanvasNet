namespace DemaConsulting.CanvasNet.Charts;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Charts"/> namespace provides a public,
///     format-agnostic charting data model (<see cref="Chart"/>, <see cref="ChartSeries"/>,
///     <see cref="ChartAxis"/>, <see cref="ChartLegend"/>, <see cref="ChartTitle"/>, and
///     <see cref="ChartType"/>), an ergonomic, fluent construction API
///     (<see cref="ChartBuilder"/>), and a pixel-rendering engine
///     (<see cref="ChartRenderer"/>, <see cref="ChartRenderOptions"/>, and
///     <see cref="ChartColorPalette"/>) that paints a <see cref="Chart"/> onto a core
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/>. This namespace is distributed as the
///     separate <c>DemaConsulting.CanvasNet.Charts</c> NuGet package, which references only the
///     core <c>DemaConsulting.CanvasNet</c> package - specifically its
///     <see cref="DemaConsulting.CanvasNet.Canvas"/> namespace (for the
///     <see cref="DemaConsulting.CanvasNet.Canvas.Rgba32"/> color type and the
///     <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> render target), and, for rendering
///     specifically, its <c>Drawing</c> (path building/filling/stroking), <c>Geometry</c>
///     (transforms/rectangles), <c>Fonts</c> (TrueType text layout/metrics), and <c>Rendering</c>
///     (the bundled fallback font) namespaces.
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
///     chart data model and its renderer and eventual OOXML adapter genuinely format-agnostic and
///     reusable by any future document-format library that needs to render a chart, rather than
///     coupled to one specific host document format.
///     </para>
///     <para>
///     As of this release, this namespace provides the data model, builder, and renderer
///     described above; parsing an OOXML <c>chart1.xml</c> part into a <see cref="Chart"/>, and
///     integrating chart rendering into a host document format (such as
///     <c>DemaConsulting.CanvasNet.Pptx</c>), are not yet implemented.
///     </para>
/// </remarks>
/// <example>
///     Building an immutable <see cref="Chart"/> describing a simple two-category bar chart with
///     a title, using <see cref="ChartBuilder"/>'s fluent API, then rendering it onto a new
///     400x300 <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/> using
///     <see cref="ChartRenderer"/>:
///     <code>
///     using DemaConsulting.CanvasNet.Charts;
///
///     var chart = new ChartBuilder()
///         .OfType(ChartType.Bar)
///         .WithCategoryAxis(["Q1", "Q2"])
///         .AddSeries("Revenue", [120.0, 150.0])
///         .WithTitle("Quarterly Revenue")
///         .Build();
///
///     using var surface = ChartRenderer.Render(chart, 400, 300);
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
