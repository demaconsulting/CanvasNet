using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     A recording <see cref="IScadBuiltinHost"/> for unit-testing built-in functions without an evaluator.
/// </summary>
internal sealed class ScadTestBuiltinHost : IScadBuiltinHost
{
    /// <summary>Gets the warnings reported, in order.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Gets the unsupported-feature reports, in order.</summary>
    public List<(string Feature, string Message)> UnsupportedReports { get; } = [];

    /// <summary>Gets the total number of steps charged.</summary>
    public long Steps { get; private set; }

    /// <summary>Gets the maximum number of steps that may be charged, or sets it.</summary>
    public long MaxSteps { get; init; } = long.MaxValue;

    /// <summary>Gets the maximum list length, or sets it.</summary>
    public int MaxListLength { get; init; } = 1000;

    /// <inheritdoc />
    public int MaxStringLength { get; init; } = 1000;

    /// <inheritdoc />
    public ScadSourceLocation Location { get; } = new("t.scad", 1, 1);

    /// <inheritdoc />
    public ScadRandom UnseededRandom { get; } = new(ScadRandom.DefaultState);

    /// <inheritdoc />
    public void Warn(string message) => Warnings.Add(message);

    /// <inheritdoc />
    public void Unsupported(string feature, string message) => UnsupportedReports.Add((feature, message));

    /// <inheritdoc />
    public void CheckListLength(long length)
    {
        if (length > MaxListLength)
        {
            throw new ScadEvaluationException($"List too long (limit {MaxListLength})", Location);
        }
    }

    /// <inheritdoc />
    public void CheckStringLength(long length)
    {
        if (length > MaxStringLength)
        {
            throw new ScadEvaluationException($"String too long (limit {MaxStringLength})", Location);
        }
    }

    /// <inheritdoc />
    public void Charge(long steps)
    {
        Steps += steps;
        if (Steps > MaxSteps)
        {
            throw new ScadEvaluationException($"Evaluation step limit exceeded (limit {MaxSteps})", Location);
        }
    }

    /// <summary>
    ///     Calls a built-in function with positional arguments and asserts that the name is a built-in.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="args">The positional arguments.</param>
    /// <returns>The result.</returns>
    public ScadValue Call(string name, params ScadValue[] args)
    {
        var named = args.Select(a => ((string?)null, a)).ToArray();
        return CallNamed(name, named);
    }

    /// <summary>
    ///     Calls a built-in function with positional and named arguments and asserts that the name is a built-in.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="args">The arguments in call order.</param>
    /// <returns>The result.</returns>
    public ScadValue CallNamed(string name, params (string? Name, ScadValue Value)[] args)
    {
        var handled = ScadBuiltinFunctions.TryInvoke(name, args, this, out var result);
        Assert.True(handled, $"'{name}' should be a built-in function");
        return result;
    }
}
