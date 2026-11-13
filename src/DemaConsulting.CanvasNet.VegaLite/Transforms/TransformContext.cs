using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     What a row-bound expression needs when it runs: the expressions the specification parser compiled and the evaluator
///     of the call (its step budget, its cancellation token and its parameter values).
/// </summary>
/// <param name="Compiled">The compiled expressions of the specification.</param>
/// <param name="Evaluator">The evaluator of the call; one thread only.</param>
internal sealed record RowExpressions(SpecExpressions Compiled, ExpressionEvaluator Evaluator);

/// <summary>
///     Everything one run of the row transforms shares: the specification, the loader that holds the row budget and the
///     file access of the call, the evaluator and the parameter values, the limits and the token.
/// </summary>
/// <remarks>
///     The context belongs to one measure or render call and is used by one thread. The transforms append the findings
///     they can recover from (for example a pivot that skips a row) to <see cref="Warnings"/> as they find them, so that a
///     later failure of the call can still deliver them.
/// </remarks>
/// <param name="Tree">The specification (compiled expressions and the source name).</param>
/// <param name="Loader">The loader of the call: it loads lookup sources and counts the rows of every table against <see cref="VegaLiteLimits.MaxDataRows"/>.</param>
/// <param name="Evaluator">The evaluator of the call.</param>
/// <param name="Parameters">The values of the variable parameters by name.</param>
/// <param name="Limits">The limits.</param>
/// <param name="CancellationToken">The token that stops the work.</param>
/// <param name="Warnings">Receives the warnings of the transforms.</param>
internal sealed record TransformContext(
    SpecTree Tree,
    DataLoader Loader,
    ExpressionEvaluator Evaluator,
    IReadOnlyDictionary<string, DataValue> Parameters,
    VegaLiteLimits Limits,
    CancellationToken CancellationToken,
    List<FeatureReport> Warnings)
{
    /// <summary>The number of rows between two cancellation checks.</summary>
    private const int CancellationInterval = 1024;

    /// <summary>Gets the selections of the specification (Vega-Lite's selection components), whose values a bin extent can follow; the model that declares none when there are none.</summary>
    public SelectionModel Selections { get; init; } = SelectionModel.Empty;

    /// <summary>Gets the view that the transforms of the context run for (the view whose <c>transform</c> runs, or the unit whose encoding is implemented); it decides the selections they see. Null when unknown.</summary>
    public LayerNode? View { get; init; }

    /// <summary>
    ///     Reads the span that a <c>bin.extent</c> of the form <c>{"param": ...}</c> gives (Vega-Lite's <c>span(N["field"])</c> of the <c>bin</c> transform).
    /// </summary>
    /// <remarks>
    ///     The extent of the bins is not taken from the parameter: only the step follows its span, and the bins start and stop at the extent of the data. A selection gives the span of the selected
    ///     range of the field the extent names; a variable parameter is the signal itself. A parameter that holds no range has the span zero, which the bin algorithm ignores.
    /// </remarks>
    /// <param name="extent">The <c>extent</c> of the bin.</param>
    /// <returns>The span; null when the extent is not a parameter reference.</returns>
    public double? BinSpanOf(SpecNode? extent)
    {
        if (extent is not { IsObject: true } || extent["param"] is not { IsString: true } name)
        {
            return null;
        }

        var param = name.StringValue!;
        var read = Selections.ResolveExtent(View, param, extent["field"]?.StringValue, extent["encoding"]?.StringValue, Locate(extent), Warnings);
        var value = read.Value;
        if (!read.IsSelection)
        {
            Parameters.TryGetValue(param, out value);
        }

        return BinAlgorithm.SpanOf(value);
    }

    /// <summary>
    ///     Gets the location of a node of the specification.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The location.</returns>
    public VegaLiteLocation Locate(SpecNode node) => Tree.Locate(node);

    /// <summary>
    ///     Sets the value of a parameter that a transform computes (the <c>param</c> of an <c>extent</c> transform).
    /// </summary>
    /// <remarks>
    ///     <see cref="Parameters"/> is a read-only view of the one parameter table that the evaluator, the later transforms and the
    ///     scale stage of the call share; the table is a mutable dictionary owned by the call, and the transform that Vega-Lite
    ///     compiles to a signal is the only writer. The new value is seen by everything that reads the parameter after this
    ///     call, and by nothing that was fixed before it (the expression references of the static sites are evaluated before the
    ///     data stage). A context whose parameter table is not mutable ignores the value.
    /// </remarks>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    public void SetParameter(string name, DataValue value)
    {
        if (Parameters is IDictionary<string, DataValue> writable)
        {
            writable[name] = value;
        }
    }

    /// <summary>
    ///     Checks the cancellation token once per batch of rows.
    /// </summary>
    /// <param name="row">The row that is about to be processed.</param>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled and the row starts a batch.</exception>
    public void Tick(long row)
    {
        if (row % CancellationInterval == 0)
        {
            CancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Checks that a text that the specification gives a transform, and that the transform copies into the cells of its output,
    ///     is not longer than the limit of the call (at any depth).
    /// </summary>
    /// <remarks>
    ///     The limit of the call can be lower than the one the specification was parsed with, so a text of the specification
    ///     is not trusted to fit. The check is made once, before any output is allocated.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <param name="location">The location of the transform.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when a text is longer than <see cref="VegaLiteLimits.MaxStringLength"/>.</exception>
    public void CheckText(DataValue value, string what, VegaLiteLocation location)
    {
        var maxLength = Limits.MaxStringLength;
        if (value.HasTextLongerThan(maxLength))
        {
            throw new VegaLiteRenderException(
                $"{what} has a text longer than the limit of {maxLength} characters (VegaLiteLimits.MaxStringLength).",
                location);
        }
    }
}
