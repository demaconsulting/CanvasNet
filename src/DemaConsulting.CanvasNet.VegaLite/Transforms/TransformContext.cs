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
    List<FeatureReport> Warnings);
