using System.Text;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Deterministic reservoir sampling of row indexes: the algorithm behind the <c>sample</c> transform.
/// </summary>
/// <remarks>
///     <para>
///         The sample is a pure function of the number of rows, the sample size and the JSON pointer of the transform, so the
///         same chart gives the same rows on every call, thread, machine and platform; there is no clock or process random
///         source. The algorithm is the classic reservoir sampling (algorithm R): the first <c>size</c> indexes fill the reservoir; for every later
///         index <c>i</c> (zero based) a number <c>r</c> is drawn uniformly from <c>0..i</c> as the next output of the
///         generator modulo <c>i + 1</c>, and when <c>r &lt; size</c> the index replaces <c>reservoir[r]</c>. The selected
///         indexes are then sorted ascending, which restores the order of the source.
///     </para>
///     <para>
///         The generator is SplitMix64 (<c>state += 0x9E3779B97F4A7C15</c>, then the finalizer with the multipliers
///         <c>0xBF58476D1CE4E5B9</c> and <c>0x94D049BB133111EB</c> and the shifts 30, 27 and 31). Its seed is the 64-bit FNV-1a
///         hash (offset basis <c>0xCBF29CE484222325</c>, prime <c>0x100000001B3</c>) of the UTF-8 bytes of the JSON pointer of the
///         transform, so two sample transforms of one chart draw independent samples and a chart that is edited elsewhere keeps
///         the sample of an unchanged transform. A size of at least the number of rows selects every row (a stable copy) and a
///         size of zero selects none. The modulo reduction has a bias of at most 2^-33 for the largest permitted tables.
///     </para>
/// </remarks>
internal static class RowSampler
{
    /// <summary>The FNV-1a 64-bit offset basis.</summary>
    private const ulong FnvOffsetBasis = 14695981039346656037UL;

    /// <summary>The FNV-1a 64-bit prime.</summary>
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>The increment of SplitMix64.</summary>
    private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15;

    /// <summary>The first multiplier of the SplitMix64 finalizer.</summary>
    private const ulong SplitMixMultiplier1 = 0xBF58476D1CE4E5B9;

    /// <summary>The second multiplier of the SplitMix64 finalizer.</summary>
    private const ulong SplitMixMultiplier2 = 0x94D049BB133111EB;

    /// <summary>The number of rows between two cancellation checks.</summary>
    private const int CancellationInterval = 1024;

    /// <summary>
    ///     Computes the 64-bit FNV-1a hash of the UTF-8 bytes of a text.
    /// </summary>
    /// <param name="text">The text (the JSON pointer of a transform).</param>
    /// <returns>The hash.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static ulong Seed(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var hash = FnvOffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash = unchecked(hash * FnvPrime);
        }

        return hash;
    }

    /// <summary>
    ///     Advances a SplitMix64 generator and returns its next output.
    /// </summary>
    /// <param name="state">The state; advanced.</param>
    /// <returns>The next 64 random bits.</returns>
    public static ulong Next(ref ulong state)
    {
        unchecked
        {
            state += SplitMixIncrement;
            var z = state;
            z = (z ^ (z >> 30)) * SplitMixMultiplier1;
            z = (z ^ (z >> 27)) * SplitMixMultiplier2;
            return z ^ (z >> 31);
        }
    }

    /// <summary>
    ///     Selects the indexes of the sample.
    /// </summary>
    /// <param name="count">The number of rows.</param>
    /// <param name="size">The sample size.</param>
    /// <param name="pointer">The JSON pointer of the transform, the seed of the sample.</param>
    /// <param name="cancellationToken">The token that stops the work.</param>
    /// <returns>The selected row indexes in ascending order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> or <paramref name="size"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public static int[] Select(int count, int size, string pointer, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        if (size >= count)
        {
            return [.. Enumerable.Range(0, count)];
        }

        var reservoir = new int[size];
        for (var i = 0; i < size; i++)
        {
            reservoir[i] = i;
        }

        var state = Seed(pointer);
        for (var i = size; i < count; i++)
        {
            if (i % CancellationInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var slot = Next(ref state) % (ulong)(i + 1L);
            if (slot < (ulong)size)
            {
                reservoir[(int)slot] = i;
            }
        }

        Array.Sort(reservoir);
        return reservoir;
    }
}
