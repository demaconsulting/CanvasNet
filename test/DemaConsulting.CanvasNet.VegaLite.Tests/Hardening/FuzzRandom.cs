namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     A small seeded random number generator (SplitMix64) for the fuzz tests. It is written here instead of using <see cref="Random"/> so that a seed
///     gives the same sequence on every runtime and every operating system: a failing case that is reported with its seed and index can be rebuilt anywhere.
/// </summary>
internal sealed class FuzzRandom
{
    /// <summary>The state of the generator.</summary>
    private ulong _state;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FuzzRandom"/> class.
    /// </summary>
    /// <param name="seed">The seed.</param>
    public FuzzRandom(ulong seed)
    {
        _state = seed;
    }

    /// <summary>
    ///     Creates the generator of one case: the seed and the index of the case are mixed so that neighboring cases are unrelated.
    /// </summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="index">The index of the case.</param>
    /// <returns>The generator.</returns>
    public static FuzzRandom ForCase(ulong seed, int index)
    {
        var mixer = new FuzzRandom(unchecked(seed + (0x9E3779B97F4A7C15UL * (ulong)(index + 1L))));
        return new FuzzRandom(mixer.NextULong());
    }

    /// <summary>
    ///     Gets the next 64 random bits.
    /// </summary>
    /// <returns>The bits.</returns>
    public ulong NextULong()
    {
        unchecked
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; // cspell:disable-line
            return z ^ (z >> 31);
        }
    }

    /// <summary>
    ///     Gets a random integer below a bound.
    /// </summary>
    /// <param name="bound">The exclusive upper bound; at least one.</param>
    /// <returns>An integer from zero to <paramref name="bound"/> minus one.</returns>
    public int Next(int bound) => (int)(NextULong() % (ulong)bound);

    /// <summary>
    ///     Gets a random integer in a range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>The integer.</returns>
    public int Next(int min, int max) => min + Next(max - min + 1);

    /// <summary>
    ///     Gets a random number from zero (inclusive) to one (exclusive).
    /// </summary>
    /// <returns>The number.</returns>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    ///     Decides at random.
    /// </summary>
    /// <param name="probability">The probability of <see langword="true"/>, from zero to one.</param>
    /// <returns>The decision.</returns>
    public bool Chance(double probability) => NextDouble() < probability;

    /// <summary>
    ///     Picks an element of a list at random.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="items">The list; not empty.</param>
    /// <returns>The element.</returns>
    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];
}
