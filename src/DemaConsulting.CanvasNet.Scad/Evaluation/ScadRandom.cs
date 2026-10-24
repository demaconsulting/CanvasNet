namespace DemaConsulting.CanvasNet.Scad.Evaluation;

// cspell:ignore CADUL EBUL xorshift

/// <summary>
///     A small deterministic pseudo-random generator (SplitMix64) used by the <c>rands</c> built-in function.
/// </summary>
/// <remarks>
///     <para>
///         The generator is implemented here, rather than taken from the platform, so that the same seed produces
///         the same numbers on every operating system, architecture and runtime version: rendered models must be
///         reproducible. SplitMix64 is a public-domain algorithm: the state advances by a fixed odd constant and is
///         scrambled with two multiply-xorshift rounds.
///     </para>
///     <para>
///         <see cref="NextDouble"/> maps the top 53 bits of an output to a double in [0, 1) by multiplication with
///         2<sup>-53</sup>, which is exact. Instances are not thread-safe.
///     </para>
/// </remarks>
internal sealed class ScadRandom
{
    /// <summary>The fixed initial state of the evaluation-wide generator that is used when <c>rands</c> has no seed.</summary>
    internal const ulong DefaultState = 0x5CAD_5CAD_5CAD_5CADUL;

    /// <summary>The state increment (the 64-bit golden ratio, an odd constant).</summary>
    private const ulong Increment = 0x9E3779B97F4A7C15UL;

    /// <summary>The first scrambling multiplier.</summary>
    private const ulong MultiplierA = 0xBF58476D1CE4E5B9UL;

    /// <summary>The second scrambling multiplier.</summary>
    private const ulong MultiplierB = 0x94D049BB133111EBUL;

    /// <summary>The scale that maps 53 random bits to [0, 1): 2<sup>-53</sup>.</summary>
    private const double UnitScale = 1.0 / (1UL << 53);

    /// <summary>The generator state.</summary>
    private ulong _state;

    /// <summary>
    ///     Initializes a generator.
    /// </summary>
    /// <param name="state">The initial state (any value, including zero).</param>
    public ScadRandom(ulong state)
    {
        _state = state;
    }

    /// <summary>
    ///     Creates a generator from a numeric seed.
    /// </summary>
    /// <param name="seed">The seed number; its IEEE-754 bit pattern becomes the initial state, so every distinct double is a distinct seed.</param>
    /// <returns>A new generator.</returns>
    public static ScadRandom FromSeed(double seed) => new(unchecked((ulong)BitConverter.DoubleToInt64Bits(seed)));

    /// <summary>
    ///     Produces the next 64-bit output.
    /// </summary>
    /// <returns>A pseudo-random 64-bit value.</returns>
    public ulong NextUInt64()
    {
        // Advance the state, then scramble a copy of it with two multiply-xorshift rounds
        unchecked
        {
            _state += Increment;
            var z = _state;
            z = (z ^ (z >> 30)) * MultiplierA;
            z = (z ^ (z >> 27)) * MultiplierB;
            return z ^ (z >> 31);
        }
    }

    /// <summary>
    ///     Produces the next number in [0, 1).
    /// </summary>
    /// <returns>A pseudo-random double that is at least zero and less than one.</returns>
    public double NextDouble() => (NextUInt64() >> 11) * UnitScale;
}
