using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the stable merge sort that the window and stack transforms use to order rows.
/// </summary>
public class StableSorterTests
{
    /// <summary>
    ///     Validates that the sort orders by the comparison and keeps equal items in their original order, for sizes that exercise every
    ///     merge width (odd and even, a power of two and not).
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(1025)]
    public void StableSorter_Sort_OrdersAndKeepsTiesInOrder(int count)
    {
        // Arrange: the key of item i is (i * 7919) % 13, so many items tie
        var items = Enumerable.Range(0, count).ToArray();
        var expected = items.OrderBy(i => (i * 7919) % 13).ToArray();

        // Act
        StableSorter.Sort(items, 0, count, new int[count], (a, b) => ((a * 7919) % 13).CompareTo((b * 7919) % 13), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, items);
    }

    /// <summary>
    ///     Validates that only the range is sorted: the items around it are untouched.
    /// </summary>
    [Fact]
    public void StableSorter_Sort_SortsOnlyTheGivenRange()
    {
        // Arrange
        int[] items = [9, 8, 5, 4, 3, 2, 1, 0];

        // Act
        StableSorter.Sort(items, 2, 4, new int[4], (a, b) => a.CompareTo(b), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([9, 8, 2, 3, 4, 5, 1, 0], items);
    }

    /// <summary>
    ///     Validates that an empty range and a range of one item are left alone without calling the comparison.
    /// </summary>
    [Fact]
    public void StableSorter_Sort_TinyRangesNeedNoComparison()
    {
        // Arrange
        int[] items = [2, 1];

        // Act
        StableSorter.Sort(items, 0, 0, [], (_, _) => throw new InvalidOperationException(), TestContext.Current.CancellationToken);
        StableSorter.Sort(items, 1, 1, [], (_, _) => throw new InvalidOperationException(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([2, 1], items);
    }

    /// <summary>
    ///     Validates that a canceled token stops the sort with an <see cref="OperationCanceledException"/> (not wrapped), and that an
    ///     exception of the comparison is not wrapped either.
    /// </summary>
    [Fact]
    public void StableSorter_Sort_StopsOnCancellationAndPropagatesExceptions()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var items = Enumerable.Range(0, 100).ToArray();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => StableSorter.Sort(items, 0, 100, new int[100], (a, b) => a.CompareTo(b), source.Token));
        Assert.Throws<InvalidOperationException>(() => StableSorter.Sort(items, 0, 100, new int[100], (_, _) => throw new InvalidOperationException(), TestContext.Current.CancellationToken));
    }
}
