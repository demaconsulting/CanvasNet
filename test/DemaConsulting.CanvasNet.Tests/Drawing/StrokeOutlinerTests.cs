using System.Linq;
using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;

// cspell:ignore Outliner underflows

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

    /// <summary>
    ///     Proves that a round join at an extreme stroke half-width (large enough that
    ///     <c>flattenTolerance / radius</c> underflows float32 precision) still produces a
    ///     genuinely curved (multi-segment) outline rather than silently degrading into the same
    ///     straight chord a bevel join would produce.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_RoundJoinAtExtremeScale_ProducesCurvedNotStraightJoin()
    {
        // Arrange: a sharp (~177 degree) turn so the round join's convex side sweeps a large
        // angle, paired with a stroke half-width many orders of magnitude larger than the
        // ~1.19e-7 relative float32 precision floor (so flattenTolerance / radius underflows to
        // zero for any reasonable flattenTolerance).
        var points = new List<Vector2> { new(0, 0), new(10, 0), new(0, 0.5f) };
        const float extremeWidth = 2e13f;
        var halfWidth = extremeWidth / 2f;
        var roundStyle = new StrokeStyle(extremeWidth, join: LineJoin.Round);
        var bevelStyle = new StrokeStyle(extremeWidth, join: LineJoin.Bevel);

        // Act
        var roundPolygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, roundStyle, flattenTolerance: 0.25f));
        var bevelPolygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, bevelStyle, flattenTolerance: 0.25f));

        // Assert: the round join must contribute at least one vertex that sits substantially away
        // from every vertex the (straight-chord) bevel join produces at the same geometry and
        // scale. A genuine multi-segment arc bulges far off the previous/next tangent-point chord
        // (here, by nearly the full half-width), whereas a collapsed single-chord arc endpoint
        // would land within float32 noise (~halfWidth * 1.2e-7) of the bevel join's own vertex.
        // The threshold below sits comfortably between those two scales.
        var maxDistanceFromBevel = roundPolygon
            .Select(roundVertex => bevelPolygon.Min(bevelVertex => Vector2.Distance(roundVertex, bevelVertex)))
            .Max();
        Assert.True(
            maxDistanceFromBevel > halfWidth * 0.1f,
            $"Expected a round-join vertex more than {halfWidth * 0.1f} units from any bevel-join vertex " +
            $"(found max {maxDistanceFromBevel}): a small distance indicates the flattenTolerance/radius " +
            "underflow regression has returned and the round join collapsed to a single straight chord.");
    }

    /// <summary>
    ///     Proves that the extreme-scale safety fallback does not change tessellation at ordinary,
    ///     well-conditioned scales: a round-capped point stroke must still produce exactly the
    ///     segment count implied by the flattening-tolerance formula.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_RoundCapAtTypicalScale_MatchesExpectedSegmentCount()
    {
        // Arrange: with a half-width of 2 and a flattening tolerance of 0.25, the tolerance-based
        // formula yields a maximum chord angle of roughly 1.010465 radians, which requires
        // exactly 7 segments to sweep a full circle within tolerance.
        var style = new StrokeStyle(4f, cap: LineCap.Round);

        // Act
        var polygon = Assert.Single(StrokeOutliner.Outline([new Vector2(0, 0)], isClosed: false, style, flattenTolerance: 0.25f));

        // Assert
        Assert.Equal(7, polygon.Count);
    }

    /// <summary>
    ///     Proves that a round join with a small (~45 degree) sweep angle at an extreme stroke
    ///     half-width (large enough that <c>flattenTolerance / radius</c> underflows float32
    ///     precision) still produces a genuinely multi-segment curved outline. Prior to the fix,
    ///     the underflow fallback's angle-based minimum segment count
    ///     (<c>ceil(sweepMagnitude / (PI/2))</c>) was exactly 1 for any sweep of at most 90
    ///     degrees, which collapsed the arc right back into a single straight chord - the same
    ///     regression the fallback was supposed to eliminate, just for smaller sweep angles than
    ///     the previously covered near-180-degree case.
    /// </summary>
    [Fact]
    public void StrokeOutliner_Outline_RoundJoinSmallSweepAtExtremeScale_ProducesMultiSegmentCurve()
    {
        // Arrange: a ~45 degree turn (well under the FallbackSegmentAngle of 90 degrees, so
        // fallbackCount alone would be exactly 1 without the Math.Max(2, ...) floor), paired with
        // a stroke half-width many orders of magnitude larger than the ~1.19e-7 relative float32
        // precision floor so flattenTolerance / radius underflows to zero for any reasonable
        // flattenTolerance.
        var points = new List<Vector2>
        {
            new(0, 0),
            new(10, 0),
            new(10 + 10 * MathF.Cos(-MathF.PI / 4f), 10 * MathF.Sin(-MathF.PI / 4f))
        };
        const float extremeWidth = 2e13f;
        var roundStyle = new StrokeStyle(extremeWidth, join: LineJoin.Round);
        var bevelStyle = new StrokeStyle(extremeWidth, join: LineJoin.Bevel);

        // Act
        var roundPolygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, roundStyle, flattenTolerance: 0.25f));
        var bevelPolygon = Assert.Single(StrokeOutliner.Outline(points, isClosed: false, bevelStyle, flattenTolerance: 0.25f));

        // Assert: a genuinely curved (multi-segment) round join contributes at least one extra
        // vertex beyond the two straight-chord endpoints a bevel join produces at the same
        // geometry and scale. A collapsed single-chord round join would instead produce exactly
        // the same vertex count as the bevel join.
        Assert.True(
            roundPolygon.Count > bevelPolygon.Count,
            $"Expected the round join ({roundPolygon.Count} vertices) to contribute more vertices " +
            $"than the equivalent bevel join ({bevelPolygon.Count} vertices): an equal count " +
            "indicates the small-sweep-angle underflow fallback has collapsed back to a single " +
            "straight chord.");
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
