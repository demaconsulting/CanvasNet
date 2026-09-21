using System.Numerics;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="EdgeFlattener"/> type.
/// </summary>
public class EdgeFlattenerTests
{
    /// <summary>
    ///     Proves that a LineTo command converts directly into its expected polygon point, with
    ///     no intermediate points introduced.
    /// </summary>
    [Fact]
    public void EdgeFlattener_Flatten_LineTo_ConvertsToExpectedPoint()
    {
        // Arrange: a single line segment from (0,0) to (3,4), left open so the implicit close is
        // the only extra point appended
        var path = new PathBuilder().MoveTo(new Vector2(0, 0)).LineTo(new Vector2(3, 4)).Build();

        // Act
        var polygons = EdgeFlattener.Flatten(path, 0.25f);

        // Assert: start point, the line's end point, then the implicit closing point back to start
        var polygon = Assert.Single(polygons);
        Assert.Equal([new Vector2(0, 0), new Vector2(3, 4), new Vector2(0, 0)], polygon);
    }

    /// <summary>
    ///     Proves that a QuadraticBezierTo command delegates to BezierFlattening, producing
    ///     exactly the same points BezierFlattening.FlattenQuadratic itself would produce for the
    ///     same control points and tolerance.
    /// </summary>
    [Fact]
    public void EdgeFlattener_Flatten_QuadraticBezierTo_DelegatesToBezierFlattening()
    {
        // Arrange
        var start = new Vector2(0, 0);
        var control = new Vector2(2, 4);
        var end = new Vector2(4, 0);
        const float tolerance = 0.25f;
        var path = new PathBuilder().MoveTo(start).QuadraticBezierTo(control, end).Build();

        var expected = new List<Vector2> { start };
        BezierFlattening.FlattenQuadratic(start, control, end, tolerance, expected);
        expected.Add(start); // implicit close

        // Act
        var polygons = EdgeFlattener.Flatten(path, tolerance);

        // Assert
        var polygon = Assert.Single(polygons);
        Assert.Equal(expected, polygon);
    }

    /// <summary>
    ///     Proves that a CubicBezierTo command delegates to BezierFlattening, producing exactly
    ///     the same points BezierFlattening.FlattenCubic itself would produce for the same control
    ///     points and tolerance.
    /// </summary>
    [Fact]
    public void EdgeFlattener_Flatten_CubicBezierTo_DelegatesToBezierFlattening()
    {
        // Arrange
        var start = new Vector2(0, 0);
        var control1 = new Vector2(0, 4);
        var control2 = new Vector2(4, 4);
        var end = new Vector2(4, 0);
        const float tolerance = 0.25f;
        var path = new PathBuilder().MoveTo(start).CubicBezierTo(control1, control2, end).Build();

        var expected = new List<Vector2> { start };
        BezierFlattening.FlattenCubic(start, control1, control2, end, tolerance, expected);
        expected.Add(start); // implicit close

        // Act
        var polygons = EdgeFlattener.Flatten(path, tolerance);

        // Assert
        var polygon = Assert.Single(polygons);
        Assert.Equal(expected, polygon);
    }

    /// <summary>
    ///     Proves that an ArcTo command delegates to SvgArcConverter.ToBeziers followed by
    ///     BezierFlattening.FlattenCubic for each resulting segment, producing exactly the same
    ///     points that pipeline would produce directly.
    /// </summary>
    [Fact]
    public void EdgeFlattener_Flatten_ArcTo_DelegatesToSvgArcConverterAndBezierFlattening()
    {
        // Arrange
        var start = new Vector2(0, 0);
        var radius = new Vector2(5, 5);
        var end = new Vector2(10, 0);
        const float tolerance = 0.25f;
        var path = new PathBuilder().MoveTo(start).ArcTo(radius, 0, largeArc: false, sweep: true, end).Build();

        var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
        SvgArcConverter.ToBeziers(start, radius, 0, false, true, end, segments);
        var expected = new List<Vector2> { start };
        var segmentStart = start;
        foreach (var segment in segments)
        {
            BezierFlattening.FlattenCubic(segmentStart, segment.Control1, segment.Control2, segment.End, tolerance, expected);
            segmentStart = segment.End;
        }

        expected.Add(start); // implicit close

        // Act
        var polygons = EdgeFlattener.Flatten(path, tolerance);

        // Assert
        var polygon = Assert.Single(polygons);
        Assert.Equal(expected, polygon);
    }

    /// <summary>
    ///     Proves that multiple subpaths within a single Path remain separate polygons in the
    ///     returned list, in the same order as Path.Subpaths.
    /// </summary>
    [Fact]
    public void EdgeFlattener_Flatten_MultipleSubpaths_RemainSeparatePolygons()
    {
        // Arrange: two independent triangles
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).LineTo(new Vector2(0, 1)).Close()
            .MoveTo(new Vector2(5, 5)).LineTo(new Vector2(6, 5)).LineTo(new Vector2(5, 6)).Close()
            .Build();

        // Act
        var polygons = EdgeFlattener.Flatten(path, 0.25f);

        // Assert: exactly two polygons, each a closed loop starting/ending at its own subpath's start
        Assert.Equal(2, polygons.Count);
        Assert.Equal(new Vector2(0, 0), polygons[0][0]);
        Assert.Equal(new Vector2(0, 0), polygons[0][^1]);
        Assert.Equal(new Vector2(5, 5), polygons[1][0]);
        Assert.Equal(new Vector2(5, 5), polygons[1][^1]);
    }

    /// <summary>
    ///     Proves that an explicitly open subpath (no Close call, so its last drawn point does
    ///     not coincide with Start) has an implicit closing point appended back to Start.
    /// </summary>
    [Fact]
    public void EdgeFlattener_Flatten_OpenSubpath_AppendsImplicitClosingPoint()
    {
        // Arrange: an open triangle - no Close call
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(4, 0))
            .LineTo(new Vector2(0, 4))
            .Build();
        Assert.False(path.Subpaths[0].IsClosed);

        // Act
        var polygons = EdgeFlattener.Flatten(path, 0.25f);

        // Assert: the polygon has 4 points - the 3 drawn points plus one implicit closing point
        // back to Start - not merely 3
        var polygon = Assert.Single(polygons);
        Assert.Equal(4, polygon.Count);
        Assert.Equal(new Vector2(0, 0), polygon[0]);
        Assert.Equal(new Vector2(0, 0), polygon[^1]);
    }

    /// <summary>
    ///     Proves that a subpath whose last drawn point already coincides with Start (via an
    ///     explicit LineTo back to the start point, followed by Close) does not get a duplicate
    ///     closing point appended.
    /// </summary>
    [Fact]
    public void EdgeFlattener_Flatten_AlreadyClosedSubpath_DoesNotDuplicateClosingPoint()
    {
        // Arrange: a triangle whose final LineTo already returns to Start, then an explicit Close
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(4, 0))
            .LineTo(new Vector2(0, 4))
            .LineTo(new Vector2(0, 0))
            .Close()
            .Build();

        // Act
        var polygons = EdgeFlattener.Flatten(path, 0.25f);

        // Assert: exactly 4 points (start + 3 drawn points, the last of which already is Start) -
        // not 5, which would indicate a duplicate closing point was appended
        var polygon = Assert.Single(polygons);
        Assert.Equal(4, polygon.Count);
        Assert.Equal(new Vector2(0, 0), polygon[^1]);
    }

    /// <summary>
    ///     Proves that flattening an empty Path yields an empty polygon list.
    /// </summary>
    [Fact]
    public void EdgeFlattener_Flatten_EmptyPath_ReturnsEmptyList()
    {
        // Act
        var polygons = EdgeFlattener.Flatten(Path.Empty, 0.25f);

        // Assert
        Assert.Empty(polygons);
    }
}
