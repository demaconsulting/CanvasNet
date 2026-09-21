using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="DashSplitter"/> type.
/// </summary>
public class DashSplitterTests
{
    /// <summary>
    ///     Proves that a null or empty dash array leaves the input polyline unchanged.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_NullOrEmptyDashArray_ReturnsWholeSegmentUnchanged()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var nullDash = DashSplitter.Split(points, isClosed: false, dashArray: null, dashOffset: 0f);
        var emptyDash = DashSplitter.Split(points, isClosed: false, dashArray: Array.Empty<float>(), dashOffset: 0f);

        // Assert
        Assert.Equal(points, Assert.Single(nullDash).Points);
        Assert.Equal(points, Assert.Single(emptyDash).Points);
    }

    /// <summary>
    ///     Proves that an odd-length dash array is conceptually duplicated.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_OddLengthDashArray_DuplicatesArrayConceptually()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [2f, 1f, 3f], dashOffset: 0f);

        // Assert
        Assert.Equal(3, segments.Count);
        Assert.Equal([new Vector2(0, 0), new Vector2(2, 0)], segments[0].Points);
        Assert.Equal([new Vector2(3, 0), new Vector2(6, 0)], segments[1].Points);
        Assert.Equal([new Vector2(8, 0), new Vector2(9, 0)], segments[2].Points);
    }

    /// <summary>
    ///     Proves that dash offset phase-shifts the first visible segment.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_WithDashOffset_ShiftsPhaseOfFirstSegment()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [4f, 2f], dashOffset: 1f);

        // Assert
        Assert.Equal(2, segments.Count);
        Assert.Equal([new Vector2(0, 0), new Vector2(3, 0)], segments[0].Points);
        Assert.Equal([new Vector2(5, 0), new Vector2(9, 0)], segments[1].Points);
    }

    /// <summary>
    ///     Proves that a visible closed-path dash segment wrapping across the seam is stitched
    ///     into one emitted segment.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_ClosedPolylineDashWrappingSeam_StitchesSegmentAcrossStartPoint()
    {
        // Arrange
        var points = new List<Vector2>
        {
            new(0, 0),
            new(2, 0),
            new(2, 2),
            new(0, 2)
        };

        // Act
        var segments = DashSplitter.Split(points, isClosed: true, dashArray: [3f, 5f], dashOffset: 2f);

        // Assert
        var segment = Assert.Single(segments);
        Assert.False(segment.IsClosed);
        Assert.Equal([new Vector2(0, 2), new Vector2(0, 0), new Vector2(1, 0)], segment.Points);
    }

    /// <summary>
    ///     Proves that a dash pattern longer than the polyline emits one partial "on" segment
    ///     when the path ends mid-dash.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_PatternLongerThanPolyline_ReturnsSinglePartialOnSegment()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(4, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [10f, 5f], dashOffset: 0f);

        // Assert
        Assert.Equal([new Vector2(0, 0), new Vector2(4, 0)], Assert.Single(segments).Points);
    }

    /// <summary>
    ///     Proves that a dash array whose entries are each individually finite but whose summed
    ///     total pattern length overflows a naive float32 accumulation (e.g. two
    ///     <see cref="float.MaxValue"/> entries) completes without hanging, even when combined
    ///     with a negative dash offset that forces phase normalization to traverse the pattern.
    ///     Prior to the fix, the overflowed (<see cref="float.PositiveInfinity"/>) pattern length
    ///     made the phase-traversal loop subtract finite dash entries from infinity forever - an
    ///     effectively infinite loop. This test intentionally makes no timing assertion: it simply
    ///     relies on xUnit's normal test execution completing at all to prove the loop terminates.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_OverflowProneDashArrayWithNegativeOffset_CompletesWithoutHanging()
    {
        // Arrange: a dash pattern whose two entries individually are finite but whose float32 sum
        // overflows to +Infinity, paired with a negative dash offset (both independently legal
        // per StrokeStyle's validation) so phase normalization must traverse the pattern.
        var points = new List<Vector2> { new(0, 0), new(10, 0) };

        // Act
        var segments = DashSplitter.Split(
            points,
            isClosed: false,
            dashArray: [float.MaxValue, float.MaxValue],
            dashOffset: -1f);

        // Assert: the call returned (did not hang) with a well-formed result
        Assert.NotNull(segments);
    }

    /// <summary>
    ///     Proves that an all-zero dash array is treated as a solid stroke.
    /// </summary>
    [Fact]
    public void DashSplitter_Split_AllZeroDashArray_TreatedAsSolid()
    {
        // Arrange
        var points = new List<Vector2> { new(0, 0), new(6, 0) };

        // Act
        var segments = DashSplitter.Split(points, isClosed: false, dashArray: [0f, 0f, 0f], dashOffset: 5f);

        // Assert
        Assert.Equal(points, Assert.Single(segments).Points);
    }
}
