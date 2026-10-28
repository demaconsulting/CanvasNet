namespace DemaConsulting.CanvasNet.VegaLite;

// cspell:ignore hconcat vconcat

/// <summary>
///     The <c>VegaLite</c> namespace renders charts written in the Vega-Lite specification language onto a CanvasNet
///     surface. It reads a specification from a string, a stream or a file and produces a validated document
///     (<c>VegaLiteDocument</c>) that describes the chart (<c>SchemaUrl</c>, <c>Title</c>, <c>MarkTypes</c>,
///     <c>DataReferences</c>, <c>ParameterNames</c>) or fails with a <c>VegaLiteSpecException</c> carrying a precise
///     <c>VegaLiteLocation</c> (file, JSON pointer, line and column). The document can be measured
///     (<c>VegaLiteDocument.Measure</c>) and drawn onto a surface (<c>VegaLiteDocument.Render</c>) under the options
///     of <c>VegaLiteRenderOptions</c>, with warnings (<c>VegaLiteDiagnostic</c>) and unsupported features
///     (<c>VegaLiteUnsupportedFeature</c>) reported through handlers or, in strict mode, a
///     <c>VegaLiteUnsupportedFeatureException</c>.
/// </summary>
/// <remarks>
///     <para>
///         Open behavior and the data sandbox are configured through <c>VegaLiteOpenOptions</c>, rendering through
///         <c>VegaLiteRenderOptions</c> and resource budgets through <c>VegaLiteLimits</c>. The specification model,
///         data, expression, scale, guide, layout, mark and scene-graph stages are internal; only the document surface
///         is public so that the internals can evolve.
///     </para>
///     <para>
///         Rendering is static, offline and deterministic: nothing is fetched from the network, no script runs, fonts
///         come from the bundled Liberation families unless the caller opts in to more, and two renders of the same
///         document and options are byte-identical.
///     </para>
///     <para>
///         Third-party provenance and attribution: this package implements the public Vega-Lite specification
///         language and is not affiliated with or endorsed by the Vega project or the University of Washington
///         Interactive Data Lab. The specification model, normalization, layout structure and marks are original
///         code written from the public documentation; a small number of default values, layout constants and numeric
///         algorithms are ported from Vega and Vega-Lite (BSD-3-Clause) and d3 (ISC) with attribution in the
///         <c>third-party</c> folder, whose license texts ship in the package under <c>licenses\</c>.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
