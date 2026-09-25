using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using GeoPath = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Geometry;

/// <summary>Unit tests for the <see cref="CornerRoundEffect"/> class.</summary>
public class CornerRoundEffectTests
{
    private static GeoPath Square() => new PathBuilder()
        .MoveTo(new Vector2(0, 0))
        .LineTo(new Vector2(10, 0))
        .LineTo(new Vector2(10, 10))
        .LineTo(new Vector2(0, 10))
        .Close()
        .Build();

    /// <summary>CornerRoundEffect_Apply_ZeroRadius_ReturnsSourcePath.</summary>
    [Fact]
    public void CornerRoundEffect_Apply_ZeroRadius_ReturnsSourcePath()
    {
        var square = Square();
        Assert.Same(square, CornerRoundEffect.Apply(square, 0f));
    }

    /// <summary>CornerRoundEffect_Apply_SquareRectangle_ProducesCornerArcs.</summary>
    [Fact]
    public void CornerRoundEffect_Apply_SquareRectangle_ProducesCornerArcs()
    {
        var rounded = CornerRoundEffect.Apply(Square(), 2f);
        var sub = Assert.Single(rounded.Subpaths);
        Assert.Contains(sub.Commands, c => c.Type == PathCommandType.CubicBezierTo);
    }

    /// <summary>
    ///     CornerRoundEffect_Apply_ClosedSquare_RoundsAllFourCornersIncludingWrapAroundCorner.
    /// </summary>
    /// <remarks>
    ///     Regression test: the square's fourth (wrap-around) corner - between the closing edge
    ///     (last LineTo back to Start, implied by Close) and the first LineTo out of Start - must
    ///     also be rounded, not just the three interior LineTo-meets-LineTo corners.
    /// </remarks>
    [Fact]
    public void CornerRoundEffect_Apply_ClosedSquare_RoundsAllFourCornersIncludingWrapAroundCorner()
    {
        var rounded = CornerRoundEffect.Apply(Square(), 2f);
        var sub = Assert.Single(rounded.Subpaths);

        // Exactly four rounded corners means exactly four CubicBezierTo commands: one per
        // corner of the square, including the wrap-around corner at (0, 0).
        var cubics = sub.Commands.Count(c => c.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(4, cubics);

        // No command may pass exactly through any of the four original sharp corners - if any
        // corner had been left un-rounded, at least one LineTo would still terminate exactly at
        // that corner.
        Vector2[] sharpCorners = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        foreach (var corner in sharpCorners)
        {
            Assert.DoesNotContain(sub.Commands, c => c.Type == PathCommandType.LineTo && c.EndPoint == corner);
        }

        // The rounded shape must still start and end (via Close) at the same point, closing the
        // loop exactly - the wrap-around arc's tangent-out point must match the subpath's
        // recorded Start (the point the builder actually moved to).
        Assert.Equal(PathCommandType.Close, sub.Commands[^1].Type);
    }

    /// <summary>CornerRoundEffect_Apply_LeavesCurvedCornersUnchanged.</summary>
    [Fact]
    public void CornerRoundEffect_Apply_LeavesCurvedCornersUnchanged()
    {
        // A cubic bezier followed by a LineTo — the vertex where they meet must NOT be rounded.
        var source = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .CubicBezierTo(new Vector2(1, 1), new Vector2(2, 1), new Vector2(3, 0))
            .LineTo(new Vector2(6, 0))
            .Build();
        var rounded = CornerRoundEffect.Apply(source, 1f);
        // Expected: same command types, same endpoints.
        Assert.Equal(source.Subpaths[0].Commands.Count, rounded.Subpaths[0].Commands.Count);
        for (var i = 0; i < source.Subpaths[0].Commands.Count; i++)
        {
            Assert.Equal(source.Subpaths[0].Commands[i].Type, rounded.Subpaths[0].Commands[i].Type);
        }
    }

    /// <summary>
    ///     CornerRoundEffect_Apply_Size10SquareRadius5_AllFourCornersGetSameUnclampedRadius.
    /// </summary>
    /// <remarks>
    ///     Regression test for a clamp-cascade bug introduced alongside the wrap-around corner
    ///     fix: when computing the clamp for the first real corner (i == 0), the code used the
    ///     already-tangent-adjusted <c>moveToPoint</c> (the wrap-around corner's tangent-out
    ///     point) as the incoming-edge start, which shortens the apparent edge length and
    ///     over-clamps that corner's radius. For a size-10 square with corner radius 5, every
    ///     original adjacent segment is length 10, so <c>min(incoming, outgoing) / 2 == 5</c> for
    ///     every corner: radius 5 should NOT be clamped anywhere, and every corner's actual
    ///     tangent length (the distance from each original sharp corner to its rounded arc's
    ///     tangent-in point) must come out identical (5), not just "some cubic exists" - the
    ///     previously buggy code instead clamped the first corner to a tangent length of 2.5
    ///     while leaving the other three at 5.
    /// </remarks>
    [Fact]
    public void CornerRoundEffect_Apply_Size10SquareRadius5_AllFourCornersGetSameUnclampedRadius()
    {
        var rounded = CornerRoundEffect.Apply(Square(), 5f);
        var sub = Assert.Single(rounded.Subpaths);
        var commands = sub.Commands;

        Vector2[] corners = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        var tangentLengths = new List<float>();

        for (var i = 0; i + 1 < commands.Count; i++)
        {
            if (commands[i].Type != PathCommandType.LineTo || commands[i + 1].Type != PathCommandType.CubicBezierTo)
            {
                continue;
            }

            // The LineTo immediately preceding a CubicBezierTo is the arc's tangent-in point -
            // its distance to the nearest original sharp corner is that corner's actual
            // (possibly clamped) tangent length.
            var tangentIn = commands[i].EndPoint;
            var nearestCorner = corners.OrderBy(c => Vector2.Distance(c, tangentIn)).First();
            tangentLengths.Add(Vector2.Distance(nearestCorner, tangentIn));
        }

        // All four corners of the square must have been rounded (four tangent-in points found),
        // and every one of them must have the SAME, unclamped tangent length (radius 5, since
        // min(10, 10) / 2 == 5 for every original adjacent segment).
        Assert.Equal(4, tangentLengths.Count);
        Assert.All(tangentLengths, length => Assert.Equal(5f, length, 0.001f));
    }

    /// <summary>CornerRoundEffect_Apply_ClampsRadiusToHalfShorterAdjacentSegment.</summary>
    [Fact]
    public void CornerRoundEffect_Apply_ClampsRadiusToHalfShorterAdjacentSegment()
    {
        // Small polyline with a short segment (length 2) meeting a long one (length 10). Radius
        // 100 must clamp so the arc fits inside the short segment.
        var source = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(2, 0)) // 2 long
            .LineTo(new Vector2(2, 10)) // 10 long, right-angle corner at (2, 0)
            .Build();
        var rounded = CornerRoundEffect.Apply(source, 100f);
        // The rounded output must contain a CubicBezier (the arc), which means the clamp
        // succeeded rather than degenerating to a LineTo.
        Assert.Contains(rounded.Subpaths[0].Commands, c => c.Type == PathCommandType.CubicBezierTo);
    }

    /// <summary>CornerRoundEffect_Apply_NullSource_ThrowsArgumentNullException.</summary>
    [Fact]
    public void CornerRoundEffect_Apply_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CornerRoundEffect.Apply(null!, 1f));
    }

    /// <summary>CornerRoundEffect_Apply_NegativeRadius_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void CornerRoundEffect_Apply_NegativeRadius_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CornerRoundEffect.Apply(Square(), -1f));
    }

    /// <summary>CornerRoundEffect_Apply_NonFiniteRadius_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void CornerRoundEffect_Apply_NonFiniteRadius_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CornerRoundEffect.Apply(Square(), float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => CornerRoundEffect.Apply(Square(), float.PositiveInfinity));
    }
}
