namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The random-number built-in function <c>rands</c>.
/// </summary>
internal static partial class ScadBuiltinFunctions
{
    /// <summary>
    ///     Gets the descriptors of the random functions.
    /// </summary>
    /// <returns>The descriptors.</returns>
    private static IEnumerable<Descriptor> RandomFunctions()
    {
        yield return Define("rands", 3, 4, Rands, "min_value", "max_value", "value_count", "seed");
    }

    /// <summary>
    ///     Implements <c>rands</c>: a vector of deterministic pseudo-random numbers in [min, max).
    /// </summary>
    /// <remarks>
    ///     With a numeric seed every call starts a fresh generator from that seed, so the same call yields the same
    ///     numbers on every platform. Without a seed the numbers come from the evaluation-wide generator, which starts
    ///     from a fixed state: results are deterministic per evaluation (deliberately not random) so renders are
    ///     reproducible. Each value is <c>min + (max - min) * u</c> with <c>u</c> in [0, 1).
    /// </remarks>
    /// <param name="args">The bound arguments: minimum, maximum, count and the optional seed.</param>
    /// <param name="host">The evaluator services.</param>
    /// <returns>The vector, or <c>undef</c> with a warning for invalid arguments.</returns>
    /// <exception cref="ScadEvaluationException">Thrown when the count exceeds the list length limit.</exception>
    private static ScadValue Rands(ReadOnlySpan<ScadValue> args, IScadBuiltinHost host)
    {
        // Validate the operands first: two numbers, a non-negative count and an optional numeric seed
        for (var i = 0; i < 3; i++)
        {
            if (args[i].Kind != ScadValueKind.Number)
            {
                return BadArgument(host, "rands", "numbers for the minimum, maximum and count", args[i]);
            }
        }

        var count = args[2].AsNumber;
        if (!(count >= 0))
        {
            return BadArgument(host, "rands", "a non-negative count", args[2]);
        }

        var seed = args.Length > 3 ? args[3] : ScadValue.Undef;
        if (seed.Kind is not (ScadValueKind.Undef or ScadValueKind.Number))
        {
            return BadArgument(host, "rands", "a number as the seed", seed);
        }

        // The count is bounded by the list limit before any element is produced
        var length = (long)Math.Min(Math.Truncate(count), long.MaxValue / 2.0);
        host.CheckListLength(length);
        host.Charge(Math.Max(1, length));
        var generator = seed.Kind == ScadValueKind.Number ? ScadRandom.FromSeed(seed.AsNumber) : host.UnseededRandom;
        var minimum = args[0].AsNumber;
        var span = args[1].AsNumber - minimum;
        var items = new ScadValue[length];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = ScadValue.FromNumber(minimum + (span * generator.NextDouble()));
        }

        return ScadValue.FromVector(items);
    }
}
