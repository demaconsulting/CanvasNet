// Portions derived from lodash (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The JavaScript and lodash collection semantics that dagre's results depend on: V8's
///     <c>Array.prototype.sort</c>, and lodash's <c>_.sortBy</c>, <c>_.minBy</c>, <c>_.min</c> and <c>_.max</c>.
/// </summary>
/// <remarks>
///     <para>
///         Numbers that JavaScript may leave <c>undefined</c> are <see cref="double"/>? values here (null is
///         <c>undefined</c>). lodash's extremum functions skip <c>undefined</c> and NaN, take the first of equal values,
///         and give <c>undefined</c> for an empty list. <c>_.sortBy</c> is stable and orders numbers ascending, then
///         <c>undefined</c>, then NaN.
///     </para>
///     <para>
///         V8 sorts with TimSort. For a comparator that is a consistent order every stable sort gives the same result,
///         but dagre's barycenter comparator is not consistent when a barycenter is NaN, so the port follows V8's steps:
///         a natural run (a descending run is reversed) extended by binary insertion to the minimum run length, which for
///         fewer than 64 items is the whole list, so short lists are sorted exactly as V8 sorts them; longer runs are
///         merged stably in V8's merge order (without galloping, which only matters for inconsistent comparators).
///     </para>
/// </remarks>
internal static class Lodash
{
    /// <summary>The list length from which V8 sorts in several runs.</summary>
    private const int MinMerge = 64;

    /// <summary>
    ///     Sorts a list in place as V8's <c>Array.prototype.sort</c> does with a comparator.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The list.</param>
    /// <param name="compare">The comparator.</param>
    public static void Sort<T>(List<T> items, Comparison<T> compare)
    {
        var n = items.Count;
        if (n < 2)
        {
            return;
        }

        // Cut the list into runs of at least the minimum run length, merging as V8's run stack does
        var minRun = MinRunLength(n);
        var runs = new List<(int Base, int Length)>();
        var low = 0;
        var remaining = n;
        while (remaining != 0)
        {
            var runLength = CountAndMakeRun(items, low, low + remaining, compare);
            if (runLength < minRun)
            {
                var forced = Math.Min(minRun, remaining);
                BinaryInsertionSort(items, low, low + runLength, low + forced, compare);
                runLength = forced;
            }

            runs.Add((low, runLength));
            MergeCollapse(items, runs, compare);
            low += runLength;
            remaining -= runLength;
        }

        // Merge what is left
        while (runs.Count > 1)
        {
            var i = runs.Count - 2;
            if (i > 0 && runs[i - 1].Length < runs[i + 1].Length)
            {
                i--;
            }

            MergeAt(items, runs, i, compare);
        }
    }

    /// <summary>
    ///     Sorts items by a numeric key, stably, as lodash's <c>_.sortBy</c> does.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="key">The key; null is <c>undefined</c>.</param>
    /// <returns>A new sorted list.</returns>
    public static List<T> SortBy<T>(IEnumerable<T> items, Func<T, double?> key)
    {
        var criteria = items.Select((item, index) => (Value: item, Index: index, Key: key(item))).ToList();
        Sort(criteria, (a, b) =>
        {
            var result = CompareAscending(a.Key, b.Key);
            return result != 0 ? result : a.Index - b.Index;
        });
        return criteria.ConvertAll(c => c.Value);
    }

    /// <summary>
    ///     Finds the first item with the least key, as lodash's <c>_.minBy</c> does; <c>undefined</c> and NaN keys are
    ///     skipped.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="key">The key; null is <c>undefined</c>.</param>
    /// <param name="found">Whether an item was found.</param>
    /// <returns>The item, or the default when none has a key.</returns>
    public static T? MinBy<T>(IEnumerable<T> items, Func<T, double?> key, out bool found)
    {
        found = false;
        T? result = default;
        double computed = 0;
        foreach (var item in items)
        {
            var current = key(item);
            if (current is not { } value)
            {
                continue;
            }

            if (found ? value < computed : !double.IsNaN(value))
            {
                computed = value;
                result = item;
                found = true;
            }
        }

        return result;
    }

    /// <summary>
    ///     Gets the least number, as lodash's <c>_.min</c> does.
    /// </summary>
    /// <param name="values">The values; null is <c>undefined</c>.</param>
    /// <returns>The least value, or null when there is none that is a number.</returns>
    public static double? Min(IEnumerable<double?> values) => Extremum(values, (a, b) => a < b);

    /// <summary>
    ///     Gets the greatest number, as lodash's <c>_.max</c> does.
    /// </summary>
    /// <param name="values">The values; null is <c>undefined</c>.</param>
    /// <returns>The greatest value, or null when there is none that is a number.</returns>
    public static double? Max(IEnumerable<double?> values) => Extremum(values, (a, b) => a > b);

    /// <summary>
    ///     Gets the value of a JavaScript number that may be <c>undefined</c>, for arithmetic: NaN for <c>undefined</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The number.</returns>
    public static double Num(double? value) => value ?? double.NaN;

    /// <summary>
    ///     Compares two keys as lodash's <c>compareAscending</c> does: numbers ascending, then <c>undefined</c>, then NaN.
    /// </summary>
    /// <param name="value">The first key.</param>
    /// <param name="other">The second key.</param>
    /// <returns>Negative, zero or positive.</returns>
    public static int CompareAscending(double? value, double? other)
    {
        // value !== other (NaN is never equal to itself, undefined is equal to undefined)
        if (value is null && other is null)
        {
            return 0;
        }

        if (value is { } a && other is { } b && DagreLayout.Same(a, b))
        {
            return 0;
        }

        var valIsDefined = value is not null;
        var valIsReflexive = value is null || !double.IsNaN(value.Value);
        var othIsDefined = other is not null;
        var othIsReflexive = other is null || !double.IsNaN(other.Value);
        if ((value is { } x && other is { } y && x > y) || (!valIsDefined && othIsReflexive) || !valIsReflexive)
        {
            return 1;
        }

        if ((value is { } p && other is { } q && p < q) || (!othIsDefined && valIsReflexive) || !othIsReflexive)
        {
            return -1;
        }

        return 0;
    }

    /// <summary>
    ///     lodash's <c>baseExtremum</c> on numbers.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="better">Whether the first value replaces the second.</param>
    /// <returns>The extremum, or null.</returns>
    private static double? Extremum(IEnumerable<double?> values, Func<double, double, bool> better)
    {
        double? computed = null;
        foreach (var current in values)
        {
            if (current is not { } value)
            {
                continue;
            }

            if (computed is { } best ? better(value, best) : !double.IsNaN(value))
            {
                computed = value;
            }
        }

        return computed;
    }

    /// <summary>
    ///     V8's minimum run length for a list length.
    /// </summary>
    /// <param name="length">The list length.</param>
    /// <returns>The length itself below 64, else a value between 32 and 64.</returns>
    private static int MinRunLength(int length)
    {
        var n = length;
        var r = 0;
        while (n >= MinMerge)
        {
            r |= n & 1;
            n >>= 1;
        }

        return n + r;
    }

    /// <summary>
    ///     Finds the natural run at the start of a range, reversing it when it is strictly descending (V8's
    ///     <c>CountAndMakeRun</c>).
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The list.</param>
    /// <param name="lowArg">The start of the range.</param>
    /// <param name="high">The end of the range (exclusive).</param>
    /// <param name="compare">The comparator.</param>
    /// <returns>The run length.</returns>
    private static int CountAndMakeRun<T>(List<T> items, int lowArg, int high, Comparison<T> compare)
    {
        var low = lowArg + 1;
        if (low == high)
        {
            return 1;
        }

        var runLength = 2;
        var previous = items[low];
        var isDescending = compare(previous, items[low - 1]) < 0;
        for (var i = low + 1; i < high; i++)
        {
            var current = items[i];
            var order = compare(current, previous);
            if (isDescending ? order >= 0 : order < 0)
            {
                break;
            }

            previous = current;
            runLength++;
        }

        if (isDescending)
        {
            items.Reverse(lowArg, runLength);
        }

        return runLength;
    }

    /// <summary>
    ///     Extends a sorted prefix of a range by binary insertion (V8's <c>BinaryInsertionSort</c>).
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The list.</param>
    /// <param name="low">The start of the range.</param>
    /// <param name="startArg">The end of the sorted prefix.</param>
    /// <param name="high">The end of the range (exclusive).</param>
    /// <param name="compare">The comparator.</param>
    private static void BinaryInsertionSort<T>(List<T> items, int low, int startArg, int high, Comparison<T> compare)
    {
        for (var start = low == startArg ? startArg + 1 : startArg; start < high; start++)
        {
            var left = low;
            var right = start;
            var pivot = items[start];
            while (left < right)
            {
                var mid = left + ((right - left) >> 1);
                if (compare(pivot, items[mid]) < 0)
                {
                    right = mid;
                }
                else
                {
                    left = mid + 1;
                }
            }

            for (var p = start; p > left; p--)
            {
                items[p] = items[p - 1];
            }

            items[left] = pivot;
        }
    }

    /// <summary>
    ///     Merges runs until the run-stack invariants hold (V8's <c>MergeCollapse</c>).
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The list.</param>
    /// <param name="runs">The pending runs.</param>
    /// <param name="compare">The comparator.</param>
    private static void MergeCollapse<T>(List<T> items, List<(int Base, int Length)> runs, Comparison<T> compare)
    {
        while (runs.Count > 1)
        {
            var n = runs.Count - 2;
            if (!InvariantHolds(runs, n + 1) || !InvariantHolds(runs, n))
            {
                if (runs[n - 1].Length < runs[n + 1].Length)
                {
                    n--;
                }

                MergeAt(items, runs, n, compare);
            }
            else if (runs[n].Length <= runs[n + 1].Length)
            {
                MergeAt(items, runs, n, compare);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    ///     Gets whether the run before a run is longer than it and the next run together.
    /// </summary>
    /// <param name="runs">The pending runs.</param>
    /// <param name="n">The run index.</param>
    /// <returns>True when the invariant holds (always below index 2).</returns>
    private static bool InvariantHolds(List<(int Base, int Length)> runs, int n) =>
        n < 2 || runs[n - 2].Length > runs[n - 1].Length + runs[n].Length;

    /// <summary>
    ///     Merges two adjacent runs stably.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The list.</param>
    /// <param name="runs">The pending runs.</param>
    /// <param name="i">The index of the first run.</param>
    /// <param name="compare">The comparator.</param>
    private static void MergeAt<T>(List<T> items, List<(int Base, int Length)> runs, int i, Comparison<T> compare)
    {
        var (baseA, lengthA) = runs[i];
        var lengthB = runs[i + 1].Length;
        runs[i] = (baseA, lengthA + lengthB);
        runs.RemoveAt(i + 1);

        // Take from the second run only when its item is strictly less
        var left = items.GetRange(baseA, lengthA);
        var a = 0;
        var b = baseA + lengthA;
        var end = b + lengthB;
        var dest = baseA;
        while (a < lengthA && b < end)
        {
            items[dest++] = compare(items[b], left[a]) < 0 ? items[b++] : left[a++];
        }

        while (a < lengthA)
        {
            items[dest++] = left[a++];
        }
    }
}
