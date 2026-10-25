using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of <see cref="ScadEvaluatorElementCounter"/>: exact counts, the cap, saturation, the memo for shared
///     vectors and the cancellation poll.
/// </summary>
public class ScadEvaluatorElementCounterTests
{
    /// <summary>
    ///     Builds levels of vectors where every level holds a thousand references to the level below.
    /// </summary>
    /// <param name="levels">The number of levels above the one-element base vector.</param>
    /// <returns>The top vector.</returns>
    private static ScadValue SharedLevels(int levels)
    {
        var level = ScadValue.FromVector([ScadValue.FromNumber(1)]);
        for (var i = 0; i < levels; i++)
        {
            level = ScadValue.FromVector([.. Enumerable.Repeat(level, 1000)]);
        }

        return level;
    }

    /// <summary>
    ///     Validates that a flat vector counts its length and an empty vector counts one.
    /// </summary>
    [Fact]
    public void ScadEvaluatorElementCounter_Count_FlatVectors_CountTheirLength()
    {
        // Arrange
        var counter = new ScadEvaluatorElementCounter();
        var three = ScadValue.FromVector([ScadValue.FromNumber(1), ScadValue.FromNumber(2), ScadValue.FromNumber(3)]);
        var empty = ScadValue.FromVector([]);

        // Act
        var threeCount = counter.Count(three.AsVector, long.MaxValue);
        var emptyCount = counter.Count(empty.AsVector, long.MaxValue);

        // Assert
        Assert.Equal(3, threeCount);
        Assert.Equal(1, emptyCount);
    }

    /// <summary>
    ///     Validates that nested vectors add their elements to the count of the vector that holds them.
    /// </summary>
    [Fact]
    public void ScadEvaluatorElementCounter_Count_NestedVectors_AddsNestedElements()
    {
        // Arrange: [[1, 2], [3]] holds two slots, plus two in the first row and one in the second
        var row1 = ScadValue.FromVector([ScadValue.FromNumber(1), ScadValue.FromNumber(2)]);
        var row2 = ScadValue.FromVector([ScadValue.FromNumber(3)]);
        var matrix = ScadValue.FromVector([row1, row2]);

        // Act
        var count = new ScadEvaluatorElementCounter().Count(matrix.AsVector, long.MaxValue);

        // Assert
        Assert.Equal(5, count);
    }

    /// <summary>
    ///     Validates that a count above the cap stops early and reports a value above the cap.
    /// </summary>
    [Fact]
    public void ScadEvaluatorElementCounter_Count_AboveCap_StopsEarlyWithValueAboveCap()
    {
        // Arrange: two levels hold 2,001,000 slots
        var counter = new ScadEvaluatorElementCounter();
        var level2 = SharedLevels(2);

        // Act
        var capped = counter.Count(level2.AsVector, 5000);
        var exact = new ScadEvaluatorElementCounter().Count(level2.AsVector, 2_001_000);

        // Assert: above the cap the exact value is not needed; at the cap it is exact
        Assert.True(capped > 5000, $"capped={capped}");
        Assert.True(capped < 2_001_000, $"capped={capped}");
        Assert.Equal(2_001_000, exact);
    }

    /// <summary>
    ///     Validates that seven shared levels (about 10^21 logical elements) are counted at once, saturating
    ///     instead of overflowing, because the counts of shared sub-vectors are remembered.
    /// </summary>
    [Fact]
    public void ScadEvaluatorElementCounter_Count_SharedLevels_SaturatesAndUsesTheMemo()
    {
        // Arrange
        var counter = new ScadEvaluatorElementCounter();
        var top = SharedLevels(7);

        // Act
        var count = counter.Count(top.AsVector, long.MaxValue);

        // Assert: a plain sum would overflow to a negative number
        Assert.Equal(long.MaxValue, count);
        Assert.True(counter.MemoHits > 0, $"memoHits={counter.MemoHits}");
    }

    /// <summary>
    ///     Validates that a long count polls the cancellation token and a short one does not.
    /// </summary>
    [Fact]
    public void ScadEvaluatorElementCounter_Count_CanceledToken_ThrowsOnlyOnceTheCountIsLong()
    {
        // Arrange: the token is already canceled; the poll happens every 65,536 counted elements
        using var source = new CancellationTokenSource();
        source.Cancel();
        var shortVector = ScadValue.FromVector([.. Enumerable.Repeat(ScadValue.FromNumber(1), 1000)]);
        var longVector = ScadValue.FromVector([.. Enumerable.Repeat(ScadValue.FromNumber(1), (int)ScadEvaluatorElementCounter.PollInterval)]);

        // Act
        var shortCount = new ScadEvaluatorElementCounter(source.Token).Count(shortVector.AsVector, long.MaxValue);

        // Assert
        Assert.Equal(1000, shortCount);
        Assert.Throws<OperationCanceledException>(() => new ScadEvaluatorElementCounter(source.Token).Count(longVector.AsVector, long.MaxValue));
    }

    /// <summary>
    ///     Validates that a vector of many small vectors (such as a point list) never builds the memo.
    /// </summary>
    [Fact]
    public void ScadEvaluatorElementCounter_Count_ListOfSmallVectors_DoesNotUseTheMemo()
    {
        // Arrange: ten thousand points of three numbers each
        var point = ScadValue.FromVector([ScadValue.FromNumber(1), ScadValue.FromNumber(2), ScadValue.FromNumber(3)]);
        var points = ScadValue.FromVector([.. Enumerable.Repeat(point, 10_000)]);
        var counter = new ScadEvaluatorElementCounter();

        // Act
        var count = counter.Count(points.AsVector, long.MaxValue);

        // Assert: one slot per point plus three per point; the shared point is too small to be remembered
        Assert.Equal(40_000, count);
        Assert.Equal(0, counter.MemoHits);
    }
}
