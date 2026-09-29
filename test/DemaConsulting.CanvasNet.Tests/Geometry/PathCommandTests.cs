using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Tests.Geometry;

/// <summary>
///     Unit tests for the PathCommand type's public API: the LineTo factory method and the
///     ComputeTangents instance method.
/// </summary>
public class PathCommandTests
{
    /// <summary>
    ///     Proves that the public LineTo factory constructs a LineTo command carrying the given
    ///     end point, usable by a consumer outside this package that needs to synthesize a
    ///     standalone straight-line command (for example, an implicit closing edge that is not
    ///     itself recorded as its own PathCommand).
    /// </summary>
    [Fact]
    public void PathCommand_LineTo_GivenEndPoint_ConstructsLineToCommandWithThatEndPoint()
    {
        // Arrange
        var end = new Vector2(3, 4);

        // Act
        var command = PathCommand.LineTo(end);

        // Assert
        Assert.Equal(PathCommandType.LineTo, command.Type);
        Assert.Equal(end, command.EndPoint);
    }

    /// <summary>
    ///     Proves that a LineTo command's outgoing and incoming tangents are both the normalized
    ///     direction of travel from its start point to its end point.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_LineTo_ReturnsSameNormalizedDirectionForBothTangents()
    {
        // Arrange: a horizontal line segment from (0,0) to (5,0)
        var command = PathCommand.LineTo(new Vector2(5, 0));

        // Act
        var (outgoing, incoming) = command.ComputeTangents(new Vector2(0, 0));

        // Assert
        Assert.Equal(new Vector2(1, 0), outgoing);
        Assert.Equal(new Vector2(1, 0), incoming);
    }

    /// <summary>
    ///     Proves that a LineTo command whose start and end points coincide has no meaningful
    ///     direction, returning null for both tangents rather than an undefined normalized
    ///     zero-length vector.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_LineToWithCoincidentStartAndEnd_ReturnsNullTangents()
    {
        // Arrange
        var point = new Vector2(2, 2);
        var command = PathCommand.LineTo(point);

        // Act
        var (outgoing, incoming) = command.ComputeTangents(point);

        // Assert
        Assert.Null(outgoing);
        Assert.Null(incoming);
    }

    /// <summary>
    ///     Proves that a LineTo command with very large but finite coordinates (magnitudes around
    ///     1e20 to 1e25) still returns a valid unit tangent rather than null. Squaring such large
    ///     components in float precision overflows to positive infinity well before the
    ///     coordinates themselves overflow, which previously caused NormalizeOrNull to
    ///     misclassify a perfectly valid direction as degenerate.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_LineToWithLargeFiniteCoordinates_ReturnsValidNormalizedDirection()
    {
        // Arrange: a horizontal line segment whose end point has a huge but finite X coordinate
        var start = new Vector2(1e20f, 0f);
        var command = PathCommand.LineTo(new Vector2(1e25f, 0f));

        // Act
        var (outgoing, incoming) = command.ComputeTangents(start);

        // Assert: the direction is purely along +X, so both tangents are the unit X vector
        Assert.NotNull(outgoing);
        Assert.NotNull(incoming);
        Assert.Equal(new Vector2(1, 0), outgoing);
        Assert.Equal(new Vector2(1, 0), incoming);
    }

    /// <summary>
    ///     Proves that a QuadraticBezierTo command's outgoing tangent points toward its control
    ///     point, and its incoming tangent points away from that same control point toward its
    ///     end point.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_QuadraticBezierTo_PointsTowardThenAwayFromControlPoint()
    {
        // Arrange: a quadratic curve bowing upward from (0,0) to (4,0) via control (2,4)
        var start = new Vector2(0, 0);
        var control = new Vector2(2, 4);
        var end = new Vector2(4, 0);
        var command = PathCommand.QuadraticBezierTo(control, end);

        // Act
        var (outgoing, incoming) = command.ComputeTangents(start);

        // Assert
        Assert.Equal(Vector2.Normalize(control - start), outgoing);
        Assert.Equal(Vector2.Normalize(end - control), incoming);
    }

    /// <summary>
    ///     Proves that a QuadraticBezierTo command whose control point coincides with its start
    ///     point falls back to the straight start-to-end direction for its outgoing tangent,
    ///     rather than an undefined zero-length direction.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_QuadraticBezierToWithControlCoincidentWithStart_FallsBackToEndDirection()
    {
        // Arrange: control point equals start, so the curve is effectively a straight line
        var start = new Vector2(0, 0);
        var end = new Vector2(4, 0);
        var command = PathCommand.QuadraticBezierTo(start, end);

        // Act
        var (outgoing, incoming) = command.ComputeTangents(start);

        // Assert
        Assert.Equal(Vector2.Normalize(end - start), outgoing);
        Assert.Equal(Vector2.Normalize(end - start), incoming);
    }

    /// <summary>
    ///     Proves that a CubicBezierTo command's outgoing tangent points toward its first control
    ///     point, and its incoming tangent points away from its second control point.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_CubicBezierTo_PointsTowardFirstThenAwayFromSecondControlPoint()
    {
        // Arrange: an S-shaped cubic curve from (0,0) to (6,0)
        var start = new Vector2(0, 0);
        var control1 = new Vector2(1, 3);
        var control2 = new Vector2(5, -3);
        var end = new Vector2(6, 0);
        var command = PathCommand.CubicBezierTo(control1, control2, end);

        // Act
        var (outgoing, incoming) = command.ComputeTangents(start);

        // Assert
        Assert.Equal(Vector2.Normalize(control1 - start), outgoing);
        Assert.Equal(Vector2.Normalize(end - control2), incoming);
    }

    /// <summary>
    ///     Proves that a CubicBezierTo command whose control points both coincide with its start
    ///     and end points respectively falls back, in stages, to the straight start-to-end
    ///     direction for both tangents.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_CubicBezierToWithBothControlPointsCoincidentWithEndpoints_FallsBackToEndDirection()
    {
        // Arrange: both control points equal their nearest endpoint, so the curve is effectively
        // a straight line
        var start = new Vector2(0, 0);
        var end = new Vector2(6, 0);
        var command = PathCommand.CubicBezierTo(start, end, end);

        // Act
        var (outgoing, incoming) = command.ComputeTangents(start);

        // Assert
        Assert.Equal(Vector2.Normalize(end - start), outgoing);
        Assert.Equal(Vector2.Normalize(end - start), incoming);
    }

    /// <summary>
    ///     Proves that an ArcTo command returns null for both tangents: this method deliberately
    ///     does not compute an arc's tangent directly, since doing so correctly requires the same
    ///     Bezier conversion SvgArcConverter already performs, which this method does not
    ///     duplicate.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_ArcTo_ReturnsNullTangents()
    {
        // Arrange
        var command = PathCommand.ArcTo(new Vector2(1, 1), 0, largeArc: false, sweep: false, new Vector2(2, 2));

        // Act
        var (outgoing, incoming) = command.ComputeTangents(new Vector2(0, 0));

        // Assert
        Assert.Null(outgoing);
        Assert.Null(incoming);
    }

    /// <summary>
    ///     Proves that a Close command returns null for both tangents: it carries no EndPoint of
    ///     its own to compute a direction from.
    /// </summary>
    [Fact]
    public void PathCommand_ComputeTangents_Close_ReturnsNullTangents()
    {
        // Arrange
        var command = PathCommand.Close();

        // Act
        var (outgoing, incoming) = command.ComputeTangents(new Vector2(0, 0));

        // Assert
        Assert.Null(outgoing);
        Assert.Null(incoming);
    }
}
