using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="StrokePathFlattener"/> type.
/// </summary>
public class StrokePathFlattenerTests
{
    /// <summary>
    ///     Proves that an open subpath remains open after flattening.
    /// </summary>
    [Fact]
    public void StrokePathFlattener_Flatten_OpenSubpath_PreservesIsClosedFalse()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(3, 4))
            .Build();

        // Act
        var flattened = StrokePathFlattener.Flatten(path, 0.25f);

        // Assert
        var subpath = Assert.Single(flattened);
        Assert.False(subpath.IsClosed);
        Assert.Equal([new Vector2(0, 0), new Vector2(3, 4)], subpath.Points);
    }

    /// <summary>
    ///     Proves that a closed subpath remains closed without gaining an implicit duplicate start
    ///     point.
    /// </summary>
    [Fact]
    public void StrokePathFlattener_Flatten_ClosedSubpath_PreservesIsClosedTrueWithNoImplicitDuplicatePoint()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(4, 0))
            .LineTo(new Vector2(0, 4))
            .Close()
            .Build();

        // Act
        var flattened = StrokePathFlattener.Flatten(path, 0.25f);

        // Assert
        var subpath = Assert.Single(flattened);
        Assert.True(subpath.IsClosed);
        Assert.Equal(3, subpath.Points.Count);
        Assert.NotEqual(subpath.Points[0], subpath.Points[^1]);
    }

    /// <summary>
    ///     Proves that curve commands are delegated to the existing Bezier-flattening helpers.
    /// </summary>
    [Fact]
    public void StrokePathFlattener_Flatten_CurveCommands_DelegatesToBezierFlattening()
    {
        // Arrange
        const float tolerance = 0.25f;
        var start = new Vector2(0, 0);
        var quadraticControl = new Vector2(2, 4);
        var quadraticEnd = new Vector2(4, 0);
        var cubicControl1 = new Vector2(5, 2);
        var cubicControl2 = new Vector2(7, 2);
        var cubicEnd = new Vector2(8, 0);
        var path = new PathBuilder()
            .MoveTo(start)
            .QuadraticBezierTo(quadraticControl, quadraticEnd)
            .CubicBezierTo(cubicControl1, cubicControl2, cubicEnd)
            .Build();

        var expected = new List<Vector2> { start };
        BezierFlattening.FlattenQuadratic(start, quadraticControl, quadraticEnd, tolerance, expected);
        BezierFlattening.FlattenCubic(quadraticEnd, cubicControl1, cubicControl2, cubicEnd, tolerance, expected);

        // Act
        var flattened = StrokePathFlattener.Flatten(path, tolerance);

        // Assert
        var subpath = Assert.Single(flattened);
        Assert.Equal(expected, subpath.Points);
    }

    /// <summary>
    ///     Proves that multiple subpaths remain separate flattened entries.
    /// </summary>
    [Fact]
    public void StrokePathFlattener_Flatten_MultipleSubpaths_RemainSeparate()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(1, 0))
            .MoveTo(new Vector2(5, 5))
            .LineTo(new Vector2(6, 5))
            .Close()
            .Build();

        // Act
        var flattened = StrokePathFlattener.Flatten(path, 0.25f);

        // Assert
        Assert.Equal(2, flattened.Count);
        Assert.False(flattened[0].IsClosed);
        Assert.True(flattened[1].IsClosed);
    }

    /// <summary>
    ///     Proves that flattening an empty path yields no flattened subpaths.
    /// </summary>
    [Fact]
    public void StrokePathFlattener_Flatten_EmptyPath_ReturnsEmptyList()
    {
        // Act
        var flattened = StrokePathFlattener.Flatten(Path.Empty, 0.25f);

        // Assert
        Assert.Empty(flattened);
    }
}
