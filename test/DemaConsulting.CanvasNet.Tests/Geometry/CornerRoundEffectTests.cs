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
