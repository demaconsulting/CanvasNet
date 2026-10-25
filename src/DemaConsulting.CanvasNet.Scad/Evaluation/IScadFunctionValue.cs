namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The reference payload of a function <see cref="ScadValue"/>.
/// </summary>
/// <remarks>
///     The value model only needs a function's parameter names (for its text form) and reference identity (for
///     equality), so it depends on this abstraction rather than on scopes or syntax nodes. The evaluator supplies
///     the concrete closure class that also carries the body and the captured scope; two function values are equal
///     only when they wrap the same object.
/// </remarks>
internal interface IScadFunctionValue
{
    /// <summary>
    ///     Gets the names of the formal parameters in declaration order.
    /// </summary>
    IReadOnlyList<string> ParameterNames { get; }
}
