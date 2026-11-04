namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The inputs of one evaluation, already validated and converted.
/// </summary>
/// <remarks>
///     <see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, CancellationToken)"/> converts the public
///     <see cref="ScadEvaluationOptions"/> into these internal settings; tests construct them directly.
/// </remarks>
internal sealed class ScadEvaluationSettings
{
    /// <summary>Gets the resource limits.</summary>
    public ScadLimits Limits { get; init; } = ScadLimits.Default;

    /// <summary>Gets the value of <c>$t</c>.</summary>
    public double Time { get; init; }

    /// <summary>
    ///     Gets the variable overrides (the <c>-D</c> style definitions). Each replaces every top-level assignment
    ///     of its name in the main file and creates the variable when it is absent; names starting with <c>$</c>
    ///     become special variables.
    /// </summary>
    public IReadOnlyDictionary<string, ScadValue> Overrides { get; init; } = new Dictionary<string, ScadValue>();

    /// <summary>
    ///     Gets the values the viewport special variables (<c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c>, <c>$vpf</c>) have
    ///     when the document does not assign them, or <see langword="null"/> for the documented defaults of a fresh
    ///     viewer (<see cref="ScadViewportValues.OpenScadDefaults"/>).
    /// </summary>
    public ScadViewportValues? Viewport { get; init; }

    /// <summary>Gets a value indicating whether an unsupported feature aborts evaluation instead of being reported.</summary>
    public bool ThrowOnUnsupportedFeature { get; init; }

    /// <summary>Gets the token polled during evaluation.</summary>
    public CancellationToken Cancellation { get; init; }
}
