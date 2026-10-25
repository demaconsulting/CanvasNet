namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The services the evaluator provides to a built-in function while it runs: reporting, limit checks and the
///     evaluation-wide random stream.
/// </summary>
/// <remarks>
///     Built-in functions are pure apart from these services, which keeps them independent of the evaluator's
///     scopes and frames. Every method may throw <see cref="ScadEvaluationException"/> (limit checks and
///     <see cref="Charge"/>); a built-in lets that propagate.
/// </remarks>
internal interface IScadBuiltinHost
{
    /// <summary>
    ///     Gets the location of the call being evaluated, used for warnings and errors.
    /// </summary>
    ScadSourceLocation Location { get; }

    /// <summary>
    ///     Gets the maximum string length in UTF-16 characters (see <see cref="ScadLimits.MaxStringLength"/>).
    /// </summary>
    int MaxStringLength { get; }

    /// <summary>
    ///     Gets the random stream shared by all <c>rands</c> calls without a seed within one evaluation.
    /// </summary>
    /// <remarks>
    ///     It starts from a fixed state, so an evaluation is reproducible and a restarted evaluation pass sees the
    ///     same numbers.
    /// </remarks>
    ScadRandom UnseededRandom { get; }

    /// <summary>
    ///     Reports a warning at <see cref="Location"/>.
    /// </summary>
    /// <param name="message">The warning text.</param>
    void Warn(string message);

    /// <summary>
    ///     Reports an unsupported feature at <see cref="Location"/>.
    /// </summary>
    /// <param name="feature">The stable feature identifier.</param>
    /// <param name="message">The description.</param>
    void Unsupported(string feature, string message);

    /// <summary>
    ///     Verifies that a list of the given length is permitted.
    /// </summary>
    /// <param name="length">The prospective length.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the length exceeds <see cref="ScadLimits.MaxListLength"/>.</exception>
    void CheckListLength(long length);

    /// <summary>
    ///     Verifies that a string of the given length is permitted.
    /// </summary>
    /// <param name="length">The prospective length in UTF-16 characters.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the length exceeds <see cref="MaxStringLength"/>.</exception>
    void CheckStringLength(long length);

    /// <summary>
    ///     Charges evaluation steps for work done by a built-in.
    /// </summary>
    /// <param name="steps">The number of steps; at least one.</param>
    /// <exception cref="ScadEvaluationException">Thrown when the step limit is exceeded.</exception>
    void Charge(long steps);

    /// <summary>
    ///     Polls for cancellation; long-running work calls it at bounded, deterministic intervals.
    /// </summary>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested.</exception>
    void Poll();
}
