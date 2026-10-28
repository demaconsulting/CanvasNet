namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     A data source named by a Vega-Lite specification, as written, before any data is loaded.
/// </summary>
/// <remarks>
///     <para>
///         Data references let tools describe a chart (for example the <c>info</c> command of a command-line tool)
///         and decide whether data files must be supplied, without reading or evaluating anything. They are listed in
///         document order by <see cref="VegaLiteDocument.DataReferences"/>.
///     </para>
///     <para>
///         The record is immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Kind">How the data is named.</param>
/// <param name="Url">The <c>url</c> exactly as written for <see cref="VegaLiteDataKind.Url"/>; otherwise <see langword="null"/>.</param>
/// <param name="RowCount">
///     The number of rows when it is known from the text alone (the length of an inline <c>values</c> array, or the
///     length of a <c>sequence</c> with numeric bounds); otherwise <see langword="null"/>.
/// </param>
/// <param name="Location">The location of the <c>data</c> object in the specification.</param>
public sealed record VegaLiteDataReference(VegaLiteDataKind Kind, string? Url, long? RowCount, VegaLiteLocation Location);
