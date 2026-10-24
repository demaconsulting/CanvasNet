namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The result of one successful evaluation pass.
/// </summary>
/// <param name="Roots">The top-level instances in order (their implicit union is the model).</param>
/// <param name="Echo">The echo lines (including the <c>ECHO: </c> prefix), in order.</param>
/// <param name="Diagnostics">The warnings, in order.</param>
/// <param name="UnsupportedFeatures">The unsupported-feature reports, in order.</param>
/// <param name="InstanceCount">The number of instances created, groups included.</param>
/// <param name="Steps">The evaluation steps charged (a test hook).</param>
/// <param name="LoopIterations">The loop iterations charged (a test hook).</param>
internal sealed record ScadEvaluationOutput(
    IReadOnlyList<ScadInstance> Roots,
    IReadOnlyList<string> Echo,
    IReadOnlyList<ScadDiagnostic> Diagnostics,
    IReadOnlyList<ScadUnsupportedFeature> UnsupportedFeatures,
    int InstanceCount,
    long Steps,
    long LoopIterations);
