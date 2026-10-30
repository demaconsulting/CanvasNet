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
///     sandbox of the call, the evaluator and the parameter values, the limits and the token.
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

    /// <summary>
    ///     Gets the location of a node of the specification.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The location.</returns>
    public VegaLiteLocation Locate(SpecNode node) => Tree.Locate(node);

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
