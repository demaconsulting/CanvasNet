using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;

// cspell:ignore Outliner

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="StrokeOutliner"/> type.
/// </summary>
public class StrokeOutlinerTests
{
    /// <summary>
    ///     Proves that a closed contour produces one outer ring and one inner ring wound in
    ///     opposite directions.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_ClosedSubpath_ProducesTwoCounterWoundRings()
    {
        // Arrange
        var points = new List<Vector2>
        {
            new(2, 2),
            new(6, 2),
            new(6, 6),
            new(2, 6)
        };
        var style = new StrokeStyle(2f);

        // Act
        var polygons = StrokeOutliner.Outline(points, isClosed: true, style, flattenTolerance: 0.25f);

        // Assert
        Assert.Equal(2, polygons.Count);
        Assert.True(GetSignedArea(polygons[0]) * GetSignedArea(polygons[1]) < 0f);
    }

    /// <summary>
    ///     Proves that a miter join within the configured limit emits the sharp intersection
    ///     vertex.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_MiterJoinWithinLimit_ProducesSharpVertex()
    {
        // Arrange
        var points = new List<Vector2> { new(4, 4), new(8, 4), new(8, 8) };
        var style = new StrokeStyle(4f, join: LineJoin.Miter, miterLimit: 4f);

        // Act
        var polygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, style, flattenTolerance: 0.25f));

        // Assert
        Assert.Contains(new Vector2(10, 2), polygon);
    }

    /// <summary>
    ///     Proves that a miter join exceeding the configured limit falls back to bevel vertices.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_MiterJoinExceedingLimit_FallsBackToBevelVertex()
    {
        // Arrange
        var points = new List<Vector2> { new(4, 4), new(8, 4), new(8, 8) };
        var style = new StrokeStyle(4f, join: LineJoin.Miter, miterLimit: 1f);

        // Act
        var polygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, style, flattenTolerance: 0.25f));

        // Assert
        Assert.DoesNotContain(new Vector2(10, 2), polygon);
        Assert.Contains(new Vector2(8, 2), polygon);
        Assert.Contains(new Vector2(10, 4), polygon);
    }

    private static float GetSignedArea(IReadOnlyList<Vector2> points)
    {
        var area = 0f;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            area += current.X * next.Y - current.Y * next.X;
        }

        return area / 2f;
    }
}
