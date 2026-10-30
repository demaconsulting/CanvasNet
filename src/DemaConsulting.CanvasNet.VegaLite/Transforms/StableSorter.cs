namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     Sorts a range of row numbers with a comparison, keeping the rows that compare equal in their original order and polling the
///     cancellation token while it works.
/// </summary>
/// <remarks>
///     <see cref="Array.Sort{T}(T[], Comparison{T})"/> is not stable, and it wraps an exception that the comparison throws, so it can
///     neither give ties their input order nor be canceled. This is a bottom-up merge sort: stable by construction (the left run wins
///     a tie), deterministic, <c>O(n log n)</c> in the comparisons and bounded in memory by one buffer of the size of the range.
/// </remarks>
internal static class StableSorter
{
    /// <summary>
    ///     Sorts <c>items[start..start+count)</c> in place.
    /// </summary>
    /// <param name="items">The array that holds the range.</param>
    /// <param name="start">The first position of the range.</param>
    /// <param name="count">The number of items in the range.</param>
    /// <param name="buffer">Scratch space of at least <paramref name="count"/> items.</param>
    /// <param name="compare">Compares two items; negative when the first sorts first.</param>
    /// <param name="cancellationToken">The token that stops the sort.</param>
    /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
    public static void Sort(int[] items, int start, int count, int[] buffer, Func<int, int, int> compare, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(compare);
        if (count < 2)
        {
            return;
        }

        var source = items;
        var sourceOffset = start;
        var target = buffer;
        var targetOffset = 0;
        for (var width = 1; width < count; width = checked(width * 2))
        {
            for (var low = 0; low < count; low += 2 * width)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var middle = Math.Min(low + width, count);
                var high = Math.Min(low + (2 * width), count);
                Merge(source, sourceOffset, target, targetOffset, low, middle, high, compare);
            }

            (source, target) = (target, source);
            (sourceOffset, targetOffset) = (targetOffset, sourceOffset);
        }

        if (!ReferenceEquals(source, items))
        {
            Array.Copy(source, sourceOffset, items, start, count);
        }
    }

    /// <summary>
    ///     Merges two adjacent sorted runs of the source into the target.
    /// </summary>
    /// <param name="source">The array that holds the runs.</param>
    /// <param name="sourceOffset">The position of the range in the source.</param>
    /// <param name="target">The array that receives the merged run.</param>
    /// <param name="targetOffset">The position of the range in the target.</param>
    /// <param name="low">The first position of the left run (relative to the range).</param>
    /// <param name="middle">The first position of the right run (relative to the range).</param>
    /// <param name="high">The position after the right run (relative to the range).</param>
    /// <param name="compare">The comparison.</param>
    private static void Merge(int[] source, int sourceOffset, int[] target, int targetOffset, int low, int middle, int high, Func<int, int, int> compare)
    {
        int left = low, right = middle, k = low;
        while (left < middle && right < high)
        {
            // The right item goes first only when it is strictly smaller, so equal items keep their order
            target[targetOffset + k++] = compare(source[sourceOffset + right], source[sourceOffset + left]) < 0
                ? source[sourceOffset + right++]
                : source[sourceOffset + left++];
        }

        while (left < middle)
        {
            target[targetOffset + k++] = source[sourceOffset + left++];
        }

        while (right < high)
        {
            target[targetOffset + k++] = source[sourceOffset + right++];
        }
    }
}
