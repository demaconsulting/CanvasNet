namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The <c>Transforms</c> namespace turns the rows a chart loads into the rows it draws: the row transforms of a
///     specification (<c>RowTransforms</c>: calculate, filter, fold, flatten, sample, pivot and lookup), the statistical
///     transforms (<c>StatTransforms</c>) and the static
///     parameter environment that its expressions read (<c>ParameterEnvironment</c>).
/// </summary>
/// <remarks>
///     <para>
///         The transforms run eagerly, one after the other in the order of the specification, after the data loader and
///         before the scales: every transform reads an immutable <c>DataTable</c> and produces a new one, so no stage ever
///         sees a table change. The row budget of the call (<c>VegaLiteLimits.MaxDataRows</c>) is shared with the loader:
///         a transform that can produce more rows than it reads (fold, flatten) reserves its output before it allocates it,
///         the input is released once the output is complete, and a lookup source is loaded through the same loader, so it
///         has the same file-access policy, file cache and limits as the main data. Field counts, string lengths and the evaluation
///         steps of expressions are bounded by their limits, cancellation is checked at least every 1,024 rows and between
///         transforms, and a failure that depends on the data is a located <c>VegaLiteRenderException</c>.
///     </para>
///     <para>
///         Every transform of Vega-Lite is executed: the row transforms here, the aggregating transforms by
///         <c>AggregateTransforms</c>, time units by <c>TimeUnitTransform</c> and the statistical transforms (regression, loess,
///         density, quantile and extent) by <c>StatTransforms</c>; the feature catalog reports only the parts of them that are not
///         supported. The namespace is internal to the package, uses no clock or random
///         source (<c>sample</c> is seeded from the position of the transform in the specification), and gives the same
///         result on every call, thread, culture and platform.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
