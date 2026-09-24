using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using GeoPath = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Geometry;

/// <summary>
///     Unit tests for the PathBuilder class, and the Path/Subpath/PathCommand types it produces.
/// </summary>
public class PathBuilderTests
{
    /// <summary>
    ///     Proves that each drawing command type is recorded in order, and that the subpath's
    ///     Start reflects the MoveTo call that opened it.
    /// </summary>
    [Fact]
    public void PathBuilder_Build_EveryCommandType_RecordsCommandsInOrder()
    {
        // Arrange: a builder exercising every non-close command type in sequence
        var builder = new PathBuilder();
        var start = new Vector2(0, 0);
        var lineEnd = new Vector2(1, 0);
        var quadControl = new Vector2(2, 1);
        var quadEnd = new Vector2(2, 2);
        var cubicControl1 = new Vector2(3, 2);
        var cubicControl2 = new Vector2(3, 3);
        var cubicEnd = new Vector2(4, 3);
        var arcRadius = new Vector2(1, 1);
        var arcEnd = new Vector2(5, 4);

        // Act
        builder.MoveTo(start)
            .LineTo(lineEnd)
            .QuadraticBezierTo(quadControl, quadEnd)
            .CubicBezierTo(cubicControl1, cubicControl2, cubicEnd)
            .ArcTo(arcRadius, 30, largeArc: true, sweep: false, arcEnd);
        var path = builder.Build();

        // Assert: one subpath, starting at the MoveTo point, with four commands in order
        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(start, subpath.Start);
        Assert.False(subpath.IsClosed);
        Assert.Equal(4, subpath.Commands.Count);

        Assert.Equal(PathCommandType.LineTo, subpath.Commands[0].Type);
        Assert.Equal(lineEnd, subpath.Commands[0].EndPoint);

        Assert.Equal(PathCommandType.QuadraticBezierTo, subpath.Commands[1].Type);
        Assert.Equal(quadControl, subpath.Commands[1].Control1);
        Assert.Equal(quadEnd, subpath.Commands[1].EndPoint);

        Assert.Equal(PathCommandType.CubicBezierTo, subpath.Commands[2].Type);
        Assert.Equal(cubicControl1, subpath.Commands[2].Control1);
        Assert.Equal(cubicControl2, subpath.Commands[2].Control2);
        Assert.Equal(cubicEnd, subpath.Commands[2].EndPoint);

        Assert.Equal(PathCommandType.ArcTo, subpath.Commands[3].Type);
        Assert.Equal(arcRadius, subpath.Commands[3].Radius);
        Assert.Equal(30, subpath.Commands[3].RotationDegrees);
        Assert.True(subpath.Commands[3].LargeArc);
        Assert.False(subpath.Commands[3].Sweep);
        Assert.Equal(arcEnd, subpath.Commands[3].EndPoint);
    }

    /// <summary>
    ///     Proves that multiple subpaths (started via separate MoveTo calls) are preserved
    ///     independently, each with its own Start point and commands.
    /// </summary>
    [Fact]
    public void PathBuilder_Build_MultipleSubpaths_PreservesEachIndependently()
    {
        // Arrange
        var builder = new PathBuilder();

        // Act: build two independent, unconnected subpaths
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0));
        builder.MoveTo(new Vector2(10, 10)).LineTo(new Vector2(11, 10)).LineTo(new Vector2(11, 11));
        var path = builder.Build();

        // Assert
        Assert.Equal(2, path.Subpaths.Count);
        Assert.Equal(new Vector2(0, 0), path.Subpaths[0].Start);
        Assert.Single(path.Subpaths[0].Commands);
        Assert.Equal(new Vector2(10, 10), path.Subpaths[1].Start);
        Assert.Equal(2, path.Subpaths[1].Commands.Count);
    }

    /// <summary>
    ///     Proves that Close appends a Close command and marks the subpath as closed.
    /// </summary>
    [Fact]
    public void PathBuilder_Close_OpenSubpath_MarksSubpathClosedWithCloseCommand()
    {
        // Arrange
        var builder = new PathBuilder();

        // Act
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).Close();
        var path = builder.Build();

        // Assert
        var subpath = Assert.Single(path.Subpaths);
        Assert.True(subpath.IsClosed);
        Assert.Equal(PathCommandType.Close, subpath.Commands[^1].Type);
    }

    /// <summary>
    ///     Proves that a drawing command issued before the first MoveTo throws
    ///     InvalidOperationException.
    /// </summary>
    [Fact]
    public void PathBuilder_LineTo_BeforeFirstMoveTo_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new PathBuilder();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.LineTo(new Vector2(1, 1)));
    }

    /// <summary>
    ///     Proves that every drawing command type issued before the first MoveTo throws
    ///     InvalidOperationException.
    /// </summary>
    [Fact]
    public void PathBuilder_EveryDrawingCommand_BeforeFirstMoveTo_ThrowsInvalidOperationException()
    {
        // Arrange & Act & Assert
        Assert.Throws<InvalidOperationException>(() => new PathBuilder().QuadraticBezierTo(new Vector2(1, 1), new Vector2(2, 2)));
        Assert.Throws<InvalidOperationException>(() => new PathBuilder().CubicBezierTo(new Vector2(1, 1), new Vector2(2, 2), new Vector2(3, 3)));
        Assert.Throws<InvalidOperationException>(() => new PathBuilder().ArcTo(new Vector2(1, 1), 0, false, false, new Vector2(1, 1)));
        Assert.Throws<InvalidOperationException>(() => new PathBuilder().Close());
    }

    /// <summary>
    ///     Proves that a drawing command issued after Close without an intervening MoveTo throws
    ///     InvalidOperationException.
    /// </summary>
    [Fact]
    public void PathBuilder_LineTo_AfterCloseWithoutMoveTo_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).Close();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.LineTo(new Vector2(2, 2)));
    }

    /// <summary>
    ///     Proves that a MoveTo issued after Close is valid and starts a fresh subpath.
    /// </summary>
    [Fact]
    public void PathBuilder_MoveTo_AfterClose_StartsNewSubpath()
    {
        // Arrange
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).Close();

        // Act
        builder.MoveTo(new Vector2(5, 5)).LineTo(new Vector2(6, 5));
        var path = builder.Build();

        // Assert
        Assert.Equal(2, path.Subpaths.Count);
        Assert.True(path.Subpaths[0].IsClosed);
        Assert.False(path.Subpaths[1].IsClosed);
    }

    /// <summary>
    ///     Proves that MoveTo issued while a previous subpath is still open commits that subpath
    ///     as open (not implicitly closed) before starting the new one.
    /// </summary>
    [Fact]
    public void PathBuilder_MoveTo_WhilePreviousSubpathOpen_CommitsPreviousSubpathAsOpen()
    {
        // Arrange
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0));

        // Act: start a new subpath without closing the first
        builder.MoveTo(new Vector2(5, 5));
        var path = builder.Build();

        // Assert
        Assert.Equal(2, path.Subpaths.Count);
        Assert.False(path.Subpaths[0].IsClosed);
    }

    /// <summary>
    ///     Proves that Build() produces an independent snapshot: mutating the builder after
    ///     Build() does not affect the previously built Path.
    /// </summary>
    [Fact]
    public void PathBuilder_Build_ThenMutateBuilder_DoesNotAffectPreviouslyBuiltPath()
    {
        // Arrange
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0));
        var firstPath = builder.Build();

        // Act: continue mutating the same builder after the first Build()
        builder.LineTo(new Vector2(2, 0));
        var secondPath = builder.Build();

        // Assert: the first snapshot is unaffected by the later mutation
        Assert.Single(firstPath.Subpaths[0].Commands);
        Assert.Equal(2, secondPath.Subpaths[0].Commands.Count);
    }

    /// <summary>
    ///     Proves that a builder with no MoveTo calls at all produces the empty path.
    /// </summary>
    [Fact]
    public void PathBuilder_Build_NoCommandsIssued_ReturnsPathWithNoSubpaths()
    {
        // Arrange
        var builder = new PathBuilder();

        // Act
        var path = builder.Build();

        // Assert
        Assert.Empty(path.Subpaths);
        Assert.Equal(Rect.Empty, path.GetBounds());
    }

    /// <summary>
    ///     Proves that Clear() resets the builder to its initial state, allowing it to build an
    ///     unrelated path with no leftover subpaths from a previous build.
    /// </summary>
    [Fact]
    public void PathBuilder_Clear_AfterBuildingAPath_ResetsBuilderToEmptyState()
    {
        // Arrange
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).Close();

        // Act
        builder.Clear();
        builder.MoveTo(new Vector2(9, 9)).LineTo(new Vector2(10, 9));
        var path = builder.Build();

        // Assert: only the post-Clear subpath is present
        var subpath = Assert.Single(path.Subpaths);
        Assert.Equal(new Vector2(9, 9), subpath.Start);
        Assert.Single(subpath.Commands);
    }

    /// <summary>
    ///     Proves that Path is truly immutable: even though Subpaths is typed as
    ///     IReadOnlyList&lt;Subpath&gt;, a caller cannot bypass that by casting it down to
    ///     IList&lt;Subpath&gt; and mutating in place - the underlying collection throws
    ///     NotSupportedException regardless of how it is cast. Regression test for a bug where
    ///     Subpaths was backed by a plain List&lt;Subpath&gt;, which was mutable via exactly that
    ///     downcast.
    /// </summary>
    [Fact]
    public void Path_Subpaths_DowncastToIList_ThrowsNotSupportedExceptionOnMutation()
    {
        // Arrange
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0));
        var path = builder.Build();

        // Act: cast the read-only view back down to a mutable interface
        var mutable = Assert.IsAssignableFrom<IList<Subpath>>(path.Subpaths);

        // Assert: every mutating member throws, regardless of the compile-time IReadOnlyList<T> type
        Assert.Throws<NotSupportedException>(() => mutable.Add(default));
        Assert.Throws<NotSupportedException>(() => mutable.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => mutable.Clear());
    }

    /// <summary>
    ///     Proves that Subpath is truly immutable in the same way as <see cref="GeoPath"/>: Commands
    ///     cannot be mutated via a downcast to IList&lt;PathCommand&gt;. Regression test for a bug
    ///     where Commands was backed by a plain List&lt;PathCommand&gt;.
    /// </summary>
    [Fact]
    public void Subpath_Commands_DowncastToIList_ThrowsNotSupportedExceptionOnMutation()
    {
        // Arrange
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).LineTo(new Vector2(2, 0));
        var path = builder.Build();
        var subpath = path.Subpaths[0];

        // Act
        var mutable = Assert.IsAssignableFrom<IList<PathCommand>>(subpath.Commands);

        // Assert
        Assert.Throws<NotSupportedException>(() => mutable.Add(default));
        Assert.Throws<NotSupportedException>(() => mutable.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => mutable.Clear());
    }

    /// <summary>
    ///     Proves that Path.Empty has zero subpaths and its GetBounds() returns Rect.Empty.
    /// </summary>
    [Fact]
    public void Path_Empty_HasNoSubpathsAndEmptyBounds()
    {
        // Arrange & Act & Assert
        Assert.Empty(GeoPath.Empty.Subpaths);
        Assert.Equal(Rect.Empty, GeoPath.Empty.GetBounds());
    }

    /// <summary>
    ///     Proves that GetBounds(), with the default (non-flattening) conservative mode, returns
    ///     the convex hull of every command's control points and endpoints, matching a
    ///     hand-computed expected rectangle for a cubic Bezier whose control points extend beyond
    ///     its endpoints.
    /// </summary>
    [Fact]
    public void Path_GetBounds_ConservativeMode_CubicWithControlPointsOutsideChord_ReturnsControlPointConvexHull()
    {
        // Arrange: a cubic Bezier whose control points reach further up/down than either endpoint
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0))
            .CubicBezierTo(new Vector2(0, -5), new Vector2(10, 5), new Vector2(10, 0));
        var path = builder.Build();

        // Act
        var bounds = path.GetBounds();

        // Assert: hand-computed hull of (0,0),(0,-5),(10,5),(10,0) is X=0,Y=-5,Width=10,Height=10
        Assert.Equal(new Rect(0, -5, 10, 10), bounds);
    }

    /// <summary>
    ///     Proves that GetBounds() with a positive flattenTolerance produces a tighter bound than
    ///     the conservative control-point convex hull, for a curve whose control points lie well
    ///     outside the true curve's extent.
    /// </summary>
    [Fact]
    public void Path_GetBounds_FlattenMode_TighterThanConservativeMode_ForCurveWithWideControlPolygon()
    {
        // Arrange: a quadratic Bezier whose single control point is far above the curve itself
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).QuadraticBezierTo(new Vector2(5, 100), new Vector2(10, 0));
        var path = builder.Build();

        // Act
        var conservative = path.GetBounds();
        var flattened = path.GetBounds(0.01f);

        // Assert: the true curve's highest point is at t=0.5, y = 0.25*100*2 = 50 (quadratic
        // Bezier: (1-t)^2*p0 + 2t(1-t)*p1 + t^2*p2, at t=0.5 => 0.5*p1 + 0.25*(p0+p2)); the
        // flattened bound must be close to 50, while the conservative bound must reach the full
        // control point height of 100
        Assert.Equal(100, conservative.Height, 3);
        Assert.True(flattened.Height < 60, $"Expected a tighter flattened height, got {flattened.Height}");
    }

    /// <summary>
    ///     Proves that GetBounds() folds every subpath together into one overall bounding box.
    /// </summary>
    [Fact]
    public void Path_GetBounds_MultipleSubpaths_UnionsAllSubpaths()
    {
        // Arrange: two disjoint subpaths
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 1));
        builder.MoveTo(new Vector2(10, 10)).LineTo(new Vector2(11, 11));
        var path = builder.Build();

        // Act
        var bounds = path.GetBounds();

        // Assert: hand-computed union spans (0,0) to (11,11)
        Assert.Equal(new Rect(0, 0, 11, 11), bounds);
    }

    /// <summary>TangentArcTo_QuarterTurn_ProducesLineToTangentAndCubicBezier.</summary>
    [Fact]
    public void TangentArcTo_QuarterTurn_ProducesLineToTangentAndCubicBezier()
    {
        // Right angle at (10, 0): incoming ray from (0,0), outgoing ray to (10, 10). Radius 5.
        // Tangent distance = 5 / tan(45deg) = 5. Tangent-in = (5, 0), tangent-out = (10, 5).
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0));
        builder.TangentArcTo(new Vector2(10, 0), new Vector2(10, 10), 5f);
        var path = builder.Build();
        var commands = path.Subpaths[0].Commands;

        Assert.Equal(2, commands.Count);
        Assert.Equal(PathCommandType.LineTo, commands[0].Type);
        Assert.Equal(new Vector2(5, 0), commands[0].EndPoint);
        Assert.Equal(PathCommandType.CubicBezierTo, commands[1].Type);
        Assert.Equal(10f, commands[1].EndPoint.X, 3);
        Assert.Equal(5f, commands[1].EndPoint.Y, 3);
    }

    /// <summary>TangentArcTo_CollinearInputs_DegradesToLineTo.</summary>
    [Fact]
    public void TangentArcTo_CollinearInputs_DegradesToLineTo()
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0));
        builder.TangentArcTo(new Vector2(5, 0), new Vector2(10, 0), 2f);
        var commands = builder.Build().Subpaths[0].Commands;
        Assert.Single(commands);
        Assert.Equal(PathCommandType.LineTo, commands[0].Type);
        Assert.Equal(new Vector2(5, 0), commands[0].EndPoint);
    }

    /// <summary>TangentArcTo_ZeroRadius_DegradesToLineToCorner.</summary>
    [Fact]
    public void TangentArcTo_ZeroRadius_DegradesToLineToCorner()
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0));
        builder.TangentArcTo(new Vector2(10, 0), new Vector2(10, 10), 0f);
        var commands = builder.Build().Subpaths[0].Commands;
        Assert.Single(commands);
        Assert.Equal(PathCommandType.LineTo, commands[0].Type);
        Assert.Equal(new Vector2(10, 0), commands[0].EndPoint);
    }

    /// <summary>TangentArcTo_NegativeRadius_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void TangentArcTo_NegativeRadius_ThrowsArgumentOutOfRangeException()
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.TangentArcTo(new Vector2(10, 0), new Vector2(10, 10), -1f));
    }

    /// <summary>TangentArcTo_NoCurrentPoint_ThrowsInvalidOperationException.</summary>
    [Fact]
    public void TangentArcTo_NoCurrentPoint_ThrowsInvalidOperationException()
    {
        var builder = new PathBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.TangentArcTo(new Vector2(10, 0), new Vector2(10, 10), 1f));
    }
}
