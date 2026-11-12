using DemaConsulting.CanvasNet.Mermaid.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="Lodash"/>: the JavaScript and lodash semantics the layout depends on.
/// </summary>
public class LodashTests
{
    /// <summary>
    ///     Validates that the V8-style sort is stable and agrees with an ordinary stable sort for a consistent comparator,
    ///     for short lists and for lists long enough to be sorted in several runs.
    /// </summary>
    /// <param name="count">The list length.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(500)]
    [InlineData(2049)]
    public void Lodash_Sort_ConsistentComparator_MatchesStableSort(int count)
    {
        // Arrange: keys with many ties, plus descending and ascending runs
        var random = new Random(count);
        var items = Enumerable.Range(0, count).Select(i => (Key: i % 7 == 0 ? count - i : random.Next(20), Index: i)).ToList();
        var expected = items.OrderBy(item => item.Key).ToList();

        // Act
        Lodash.Sort(items, (a, b) => a.Key.CompareTo(b.Key));

        // Assert
        Assert.Equal(expected, items);
    }

    /// <summary>
    ///     Validates that a short list with an inconsistent comparator (a NaN barycenter) is sorted with V8's steps:
    ///     a natural run, then binary insertion.
    /// </summary>
    [Fact]
    public void Lodash_Sort_NaNKeys_FollowsBinaryInsertion()
    {
        // Arrange: the comparator of dagre's sort, where a NaN barycenter compares by index only
        var items = new List<(double Barycenter, int I)> { (2, 0), (double.NaN, 1), (1, 2), (0, 3) };

        // Act
        Lodash.Sort(items, (v, w) =>
        {
            if (v.Barycenter < w.Barycenter)
            {
                return -1;
            }

            return v.Barycenter > w.Barycenter ? 1 : v.I - w.I;
        });

        // Assert: [2, NaN, 1] is a natural run (NaN compares by index only); 0 is inserted by binary search after NaN
        Assert.Equal([0, 1, 3, 2], items.Select(item => item.I));
    }

    /// <summary>
    ///     Validates lodash's ascending order: numbers, then undefined, then NaN, stable among equal keys.
    /// </summary>
    [Fact]
    public void Lodash_SortBy_MixedKeys_NumbersUndefinedNaN()
    {
        // Arrange
        double?[] keys = [3, null, double.NaN, 1, 3, null];

        // Act
        var sorted = Lodash.SortBy(Enumerable.Range(0, keys.Length), i => keys[i]);

        // Assert
        Assert.Equal([3, 0, 4, 1, 5, 2], sorted);
    }

    /// <summary>
    ///     Validates lodash's extremum functions: undefined and NaN are skipped, the first of equal values wins, and an
    ///     empty list gives undefined.
    /// </summary>
    [Fact]
    public void Lodash_MinMaxMinBy_SkipUndefinedAndNaN()
    {
        // Arrange
        double?[] values = [double.NaN, null, 4, 2, 2, 9];

        // Act
        var min = Lodash.Min(values);
        var max = Lodash.Max(values);
        var minBy = Lodash.MinBy(Enumerable.Range(0, values.Length), i => values[i], out var found);
        var none = Lodash.MinBy(new[] { 0 }, _ => null, out var foundNone);

        // Assert
        Assert.Equal((2.0, 9.0), (min, max));
        Assert.True(found);
        Assert.Equal(3, minBy);
        Assert.False(foundNone);
        Assert.Equal(0, none);
        Assert.Null(Lodash.Min([]));
        Assert.True(double.IsNaN(Lodash.Num(null)));
    }

    /// <summary>
    ///     Validates lodash's comparison of keys, including the inconsistent NaN-to-NaN case.
    /// </summary>
    /// <param name="a">The first key; NaN is written as NaN, undefined as null.</param>
    /// <param name="b">The second key.</param>
    /// <param name="expected">The expected sign.</param>
    [Theory]
    [InlineData(1.0, 2.0, -1)]
    [InlineData(2.0, 1.0, 1)]
    [InlineData(1.0, 1.0, 0)]
    [InlineData(null, 1.0, 1)]
    [InlineData(1.0, null, -1)]
    [InlineData(null, null, 0)]
    [InlineData(double.NaN, 1.0, 1)]
    [InlineData(null, double.NaN, -1)]
    [InlineData(double.NaN, double.NaN, 1)]
    public void Lodash_CompareAscending_Keys_MatchesLodash(double? a, double? b, int expected)
    {
        // Act
        var actual = Lodash.CompareAscending(a, b);

        // Assert
        Assert.Equal(expected, Math.Sign(actual));
    }
}
