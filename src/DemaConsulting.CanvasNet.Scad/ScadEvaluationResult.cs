namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The outcome of a successful
///     <see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, CancellationToken)"/>.
/// </summary>
/// <remarks>
///     <para>
///         The result is immutable. Its lists hold exactly what was also delivered to the handlers of
///         <see cref="ScadEvaluationOptions"/>, in the order it occurred. A failed evaluation throws instead of
///         returning a result.
///     </para>
///     <para>
///         The instantiation tree the evaluation produces is internal: it is the input of the geometry builder
///         (<see cref="ScadDocument.BuildScene(ScadRenderOptions?, CancellationToken)"/>) and may change shape.
///         Only its size is reported here.
///     </para>
/// </remarks>
public sealed class ScadEvaluationResult
{
    /// <summary>
    ///     Initializes a result from the buffers of one evaluation.
    /// </summary>
    /// <param name="echo">The echo lines.</param>
    /// <param name="diagnostics">The warnings.</param>
    /// <param name="unsupportedFeatures">The unsupported-feature reports.</param>
    /// <param name="instanceCount">The number of module instances.</param>
    internal ScadEvaluationResult(
        IReadOnlyList<string> echo,
        IReadOnlyList<ScadDiagnostic> diagnostics,
        IReadOnlyList<ScadUnsupportedFeature> unsupportedFeatures,
        int instanceCount)
    {
        Echo = echo;
        Diagnostics = diagnostics;
        UnsupportedFeatures = unsupportedFeatures;
        InstanceCount = instanceCount;
    }

    /// <summary>
    ///     Gets the lines produced by <c>echo</c>, in order, each as a full line including its <c>ECHO: </c> prefix
    ///     and without a line terminator. Strings are quoted and escaped, so a line never contains a line break.
    /// </summary>
    public IReadOnlyList<string> Echo { get; }

    /// <summary>
    ///     Gets the warnings (unknown names, invalid arguments, missing files and similar problems the evaluation
    ///     survived), in order and without duplicates. When more than
    ///     <see cref="ScadLimits.MaxDiagnostics"/> would be reported the list ends with one notice that further
    ///     diagnostics were suppressed.
    /// </summary>
    public IReadOnlyList<ScadDiagnostic> Diagnostics { get; }

    /// <summary>
    ///     Gets the features the program used that are recognized but not supported, in order and without
    ///     duplicates; empty when <see cref="ScadEvaluationOptions.ThrowOnUnsupportedFeature"/> is set (the first one
    ///     is thrown instead).
    /// </summary>
    public IReadOnlyList<ScadUnsupportedFeature> UnsupportedFeatures { get; }

    /// <summary>
    ///     Gets the number of module instances the evaluation created, counting the implicit groups of user
    ///     modules, <c>for</c> loops and modified statements; instances inside <c>*</c>-disabled statements are
    ///     never created.
    /// </summary>
    public int InstanceCount { get; }
}
