using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using GeoPath = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Geometry;

/// <summary>
///     Unit tests dedicated to the <see cref="GeoPath"/> factories and the
///     <see cref="GeoPath.Transform"/> instance method added by the Rendering batch.
/// </summary>
public class PathTests
{
    private static GeoPath Triangle()
    {
        return new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(4, 0))
            .LineTo(new Vector2(4, 3))
            .Close()
            .Build();
    }

    /// <summary>Path_Transform_Identity_ReturnsPathWithSameVertices.</summary>
    [Fact]
    public void Path_Transform_Identity_ReturnsPathWithSameVertices()
    {
        var source = Triangle();
        var result = source.Transform(Matrix3x2.Identity);

        Assert.Equal(source.Subpaths.Count, result.Subpaths.Count);
        for (var i = 0; i < source.Subpaths.Count; i++)
        {
            Assert.Equal(source.Subpaths[i].Start, result.Subpaths[i].Start);
            Assert.Equal(source.Subpaths[i].Commands.Count, result.Subpaths[i].Commands.Count);
        }
    }

    /// <summary>Path_Transform_Translation_MovesAllControlPointsAndEndpoints.</summary>
    [Fact]
    public void Path_Transform_Translation_MovesAllControlPointsAndEndpoints()
    {
        var source = Triangle();
        var translation = Matrix3x2.CreateTranslation(10, -5);
        var result = source.Transform(translation);

        var sub = result.Subpaths[0];
        Assert.Equal(new Vector2(10, -5), sub.Start);
        Assert.Equal(new Vector2(14, -5), sub.Commands[0].EndPoint);
        Assert.Equal(new Vector2(14, -2), sub.Commands[1].EndPoint);
    }

    /// <summary>Path_Transform_Rotation_RotatesEndpointsCorrectly.</summary>
    [Fact]
    public void Path_Transform_Rotation_RotatesEndpointsCorrectly()
    {
        var source = new PathBuilder()
            .MoveTo(new Vector2(1, 0))
            .LineTo(new Vector2(0, 1))
            .Build();
        var rot = Matrix3x2.CreateRotation(MathF.PI / 2);
        var result = source.Transform(rot);

        // (1,0) -> (0,1); (0,1) -> (-1, 0) approximately.
        Assert.Equal(0f, result.Subpaths[0].Start.X, 4);
        Assert.Equal(1f, result.Subpaths[0].Start.Y, 4);
        Assert.Equal(-1f, result.Subpaths[0].Commands[0].EndPoint.X, 4);
        Assert.Equal(0f, result.Subpaths[0].Commands[0].EndPoint.Y, 4);
    }

    /// <summary>Path_Transform_PreservesPathCommandTypeSequenceForNonArcCommands.</summary>
    [Fact]
    public void Path_Transform_PreservesPathCommandTypeSequenceForNonArcCommands()
    {
        var source = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(1, 0))
            .QuadraticBezierTo(new Vector2(2, 1), new Vector2(2, 2))
            .CubicBezierTo(new Vector2(3, 2), new Vector2(3, 3), new Vector2(4, 3))
            .Close()
            .Build();
        var result = source.Transform(Matrix3x2.CreateTranslation(1, 1));
        var commands = result.Subpaths[0].Commands;
        Assert.Equal(PathCommandType.LineTo, commands[0].Type);
        Assert.Equal(PathCommandType.QuadraticBezierTo, commands[1].Type);
        Assert.Equal(PathCommandType.CubicBezierTo, commands[2].Type);
        Assert.Equal(PathCommandType.Close, commands[3].Type);
    }

    /// <summary>Path_Transform_ArcTo_ConvertsToTransformedCubicBezierSegments.</summary>
    [Fact]
    public void Path_Transform_ArcTo_ConvertsToTransformedCubicBezierSegments()
    {
        var arcStart = new Vector2(0, 0);
        var arcRadius = new Vector2(5, 5);
        var arcEnd = new Vector2(10, 0);
        var source = new PathBuilder()
            .MoveTo(arcStart)
            .ArcTo(arcRadius, 0f, false, true, arcEnd)
            .Build();

        var translation = Matrix3x2.CreateTranslation(3, 4);
        var result = source.Transform(translation);
        var commands = result.Subpaths[0].Commands;

        // The ArcTo command must have been converted to one or more CubicBezierTo commands as
        // part of the transform - never left as ArcTo, and never any other command type.
        Assert.NotEmpty(commands);
        Assert.All(commands, c => Assert.Equal(PathCommandType.CubicBezierTo, c.Type));

        // The arc's declared endpoint must land at the translated position after conversion.
        Assert.Equal(arcEnd + new Vector2(3, 4), commands[^1].EndPoint);

        // Every transformed segment's control points and endpoint must equal the untransformed
        // arc-to-Bezier conversion's own control points and endpoint plus the same translation -
        // confirming control points (not just endpoints) were genuinely transformed.
        var untransformedSegments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
        SvgArcConverter.ToBeziers(arcStart, arcRadius, 0f, false, true, arcEnd, untransformedSegments);
        Assert.Equal(untransformedSegments.Count, commands.Count);
        for (var i = 0; i < commands.Count; i++)
        {
            Assert.Equal(untransformedSegments[i].Control1 + new Vector2(3, 4), commands[i].Control1);
            Assert.Equal(untransformedSegments[i].Control2 + new Vector2(3, 4), commands[i].Control2);
            Assert.Equal(untransformedSegments[i].End + new Vector2(3, 4), commands[i].EndPoint);
        }
    }

    /// <summary>Path_Rectangle_ProducesFourLineToClosedSubpath.</summary>
    [Fact]
    public void Path_Rectangle_ProducesFourLineToClosedSubpath()
    {
        var rect = GeoPath.Rectangle(1, 2, 4, 5);
        var sub = Assert.Single(rect.Subpaths);
        Assert.Equal(new Vector2(1, 2), sub.Start);
        Assert.True(sub.IsClosed);

        // 3 LineTo + Close.
        Assert.Equal(4, sub.Commands.Count);
        Assert.All(sub.Commands.Take(3), c => Assert.Equal(PathCommandType.LineTo, c.Type));
        Assert.Equal(PathCommandType.Close, sub.Commands[3].Type);
    }

    /// <summary>Path_Rectangle_WithZeroSize_ProducesEmptyPath.</summary>
    [Fact]
    public void Path_Rectangle_WithZeroSize_ProducesEmptyPath()
    {
        Assert.Empty(GeoPath.Rectangle(0, 0, 0, 5).Subpaths);
        Assert.Empty(GeoPath.Rectangle(0, 0, 5, 0).Subpaths);
    }

    /// <summary>Path_Rectangle_WithNegativeSize_ProducesEmptyPath.</summary>
    [Fact]
    public void Path_Rectangle_WithNegativeSize_ProducesEmptyPath()
    {
        // Negative width or height must also produce an empty path (matching the documented
        // "non-positive width or height" contract), not a reversed/inverted rectangle.
        Assert.Empty(GeoPath.Rectangle(0, 0, -4, 5).Subpaths);
        Assert.Empty(GeoPath.Rectangle(0, 0, 5, -4).Subpaths);
    }

    /// <summary>Path_RoundRectangle_ProducesCornerArcsWithCorrectRadius.</summary>
    [Fact]
    public void Path_RoundRectangle_ProducesCornerArcsWithCorrectRadius()
    {
        var rr = GeoPath.RoundRectangle(0, 0, 10, 10, 2);
        var sub = Assert.Single(rr.Subpaths);
        // 4 LineTo + 4 CubicBezierTo + 1 Close
        Assert.True(sub.Commands.Count >= 8);
        Assert.Contains(sub.Commands, c => c.Type == PathCommandType.CubicBezierTo);
    }

    /// <summary>Path_RoundRectangle_RadiusClampsToHalfMinDimension.</summary>
    [Fact]
    public void Path_RoundRectangle_RadiusClampsToHalfMinDimension()
    {
        // Requesting radius 100 on a 10x10 rectangle must clamp to 5 — same result as radius=5.
        var clamped = GeoPath.RoundRectangle(0, 0, 10, 10, 100);
        var expected = GeoPath.RoundRectangle(0, 0, 10, 10, 5);
        Assert.Equal(expected.Subpaths[0].Commands.Count, clamped.Subpaths[0].Commands.Count);

        // Corner control-point positions should be identical.
        for (var i = 0; i < expected.Subpaths[0].Commands.Count; i++)
        {
            Assert.Equal(expected.Subpaths[0].Commands[i].EndPoint.X, clamped.Subpaths[0].Commands[i].EndPoint.X, 3);
            Assert.Equal(expected.Subpaths[0].Commands[i].EndPoint.Y, clamped.Subpaths[0].Commands[i].EndPoint.Y, 3);
        }
    }

    /// <summary>Path_RoundRectangle_ZeroRadius_EquivalentToRectangle.</summary>
    [Fact]
    public void Path_RoundRectangle_ZeroRadius_EquivalentToRectangle()
    {
        var rr = GeoPath.RoundRectangle(0, 0, 10, 5, 0);
        var r = GeoPath.Rectangle(0, 0, 10, 5);
        Assert.Equal(r.Subpaths[0].Commands.Count, rr.Subpaths[0].Commands.Count);
    }

    /// <summary>Path_RoundRectangle_WithNegativeSize_ProducesEmptyPath.</summary>
    [Fact]
    public void Path_RoundRectangle_WithNegativeSize_ProducesEmptyPath()
    {
        // Negative width or height must produce an empty path, matching the "non-positive width
        // or height" contract shared with Rectangle - not a reversed/inverted rounded rectangle.
        Assert.Empty(GeoPath.RoundRectangle(0, 0, -10, 5, 2).Subpaths);
        Assert.Empty(GeoPath.RoundRectangle(0, 0, 10, -5, 2).Subpaths);
    }

    /// <summary>Path_Circle_ProducesFourCubicBezierQuadrantsClosingAtStart.</summary>
    [Fact]
    public void Path_Circle_ProducesFourCubicBezierQuadrantsClosingAtStart()
    {
        var c = GeoPath.Circle(5, 5, 3);
        var sub = Assert.Single(c.Subpaths);
        Assert.True(sub.IsClosed);
        var cubics = sub.Commands.Count(cmd => cmd.Type == PathCommandType.CubicBezierTo);
        Assert.Equal(4, cubics);
    }

    /// <summary>Path_Circle_WithZeroRadius_ProducesEmptyPath.</summary>
    [Fact]
    public void Path_Circle_WithZeroRadius_ProducesEmptyPath()
    {
        Assert.Empty(GeoPath.Circle(0, 0, 0).Subpaths);
    }

    /// <summary>Path_Circle_WithNegativeRadius_ProducesEmptyPath.</summary>
    [Fact]
    public void Path_Circle_WithNegativeRadius_ProducesEmptyPath()
    {
        // A negative radius must produce an empty path (matching the documented "non-positive
        // radius" contract), not a path with inverted cardinal points.
        Assert.Empty(GeoPath.Circle(5, 5, -3).Subpaths);
    }
}
