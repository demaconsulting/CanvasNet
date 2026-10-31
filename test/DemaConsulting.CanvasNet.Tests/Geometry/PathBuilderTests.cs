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
        var mutable = Assert.IsType<IList<Subpath>>(path.Subpaths, exactMatch: false);

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
        var mutable = Assert.IsType<IList<PathCommand>>(subpath.Commands, exactMatch: false);

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

    /// <summary>PathBuilder_TangentArcTo_QuarterTurn_ProducesLineToTangentAndCubicBezier.</summary>
    [Fact]
    public void PathBuilder_TangentArcTo_QuarterTurn_ProducesLineToTangentAndCubicBezier()
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

    /// <summary>PathBuilder_TangentArcTo_CollinearInputs_DegradesToLineTo.</summary>
    [Fact]
    public void PathBuilder_TangentArcTo_CollinearInputs_DegradesToLineTo()
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0));
        builder.TangentArcTo(new Vector2(5, 0), new Vector2(10, 0), 2f);
        var commands = builder.Build().Subpaths[0].Commands;
        var command = Assert.Single(commands);
        Assert.Equal(PathCommandType.LineTo, command.Type);
        Assert.Equal(new Vector2(5, 0), command.EndPoint);
    }

    /// <summary>PathBuilder_TangentArcTo_ZeroRadius_DegradesToLineToCorner.</summary>
    [Fact]
    public void PathBuilder_TangentArcTo_ZeroRadius_DegradesToLineToCorner()
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0));
        builder.TangentArcTo(new Vector2(10, 0), new Vector2(10, 10), 0f);
        var commands = builder.Build().Subpaths[0].Commands;
        var command = Assert.Single(commands);
        Assert.Equal(PathCommandType.LineTo, command.Type);
        Assert.Equal(new Vector2(10, 0), command.EndPoint);
    }

    /// <summary>PathBuilder_TangentArcTo_NegativeRadius_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void PathBuilder_TangentArcTo_NegativeRadius_ThrowsArgumentOutOfRangeException()
    {
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.TangentArcTo(new Vector2(10, 0), new Vector2(10, 10), -1f));
    }

    /// <summary>PathBuilder_TangentArcTo_NoCurrentPoint_ThrowsInvalidOperationException.</summary>
    [Fact]
    public void PathBuilder_TangentArcTo_NoCurrentPoint_ThrowsInvalidOperationException()
    {
        var builder = new PathBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.TangentArcTo(new Vector2(10, 0), new Vector2(10, 10), 1f));
    }

    /// <summary>
    ///     Proves that a built Path is a stable snapshot: later MoveTo/draw/Clear/reuse of the same
    ///     builder (which recycles its scratch command list) never alters it, and each subpath keeps
    ///     only its own commands.
    /// </summary>
    [Fact]
    public void PathBuilder_Build_ThenReuseBuilder_EarlierPathsAreUnchanged()
    {
        // Arrange: a first path with two subpaths, the second still open when built
        var builder = new PathBuilder();
        builder.MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).LineTo(new Vector2(1, 1)).Close();
        builder.MoveTo(new Vector2(5, 5)).LineTo(new Vector2(6, 5));
        var first = builder.Build();

        // Act: keep drawing on the same builder, then clear it and build something unrelated
        builder.LineTo(new Vector2(7, 7)).MoveTo(new Vector2(9, 9)).QuadraticBezierTo(new Vector2(10, 9), new Vector2(10, 10));
        var second = builder.Build();
        builder.Clear();
        builder.MoveTo(new Vector2(-1, -1)).LineTo(new Vector2(-2, -2));
        var third = builder.Build();

        // Assert: the first path still holds exactly what it held when built
        Assert.Equal(2, first.Subpaths.Count);
        Assert.Equal(
            [PathCommandType.LineTo, PathCommandType.LineTo, PathCommandType.Close],
            first.Subpaths[0].Commands.Select(c => c.Type));
        Assert.Equal(new Vector2(5, 5), first.Subpaths[1].Start);
        var open = Assert.Single(first.Subpaths[1].Commands);
        Assert.Equal(new Vector2(6, 5), open.EndPoint);

        // Assert: the second path has the extended second subpath and the new third subpath
        Assert.Equal(3, second.Subpaths.Count);
        Assert.Equal(2, second.Subpaths[1].Commands.Count);
        Assert.Equal(new Vector2(7, 7), second.Subpaths[1].Commands[1].EndPoint);
        Assert.Equal(PathCommandType.QuadraticBezierTo, Assert.Single(second.Subpaths[2].Commands).Type);

        // Assert: the reused builder starts from scratch
        var subpath = Assert.Single(third.Subpaths);
        Assert.Equal(new Vector2(-1, -1), subpath.Start);
        Assert.Equal(new Vector2(-2, -2), Assert.Single(subpath.Commands).EndPoint);
    }

    /// <summary>
    ///     Describes a path as one token per start point and per command, so two paths can be compared
    ///     structurally; the round-trip format keeps the sign of zero and every bit of precision.
    /// </summary>
    /// <param name="path">The path to describe.</param>
    /// <returns>The tokens, in order, with a closed subpath ending in <c>Z</c>.</returns>
    private static List<string> Describe(GeoPath path)
    {
        static string P(Vector2 v) => $"{v.X:R},{v.Y:R}";

        var tokens = new List<string>();
        foreach (var subpath in path.Subpaths)
        {
            tokens.Add($"M{P(subpath.Start)}{(subpath.IsClosed ? "!closed" : string.Empty)}");
            foreach (var c in subpath.Commands)
            {
                tokens.Add(c.Type switch
                {
                    PathCommandType.LineTo => $"L{P(c.EndPoint)}",
                    PathCommandType.QuadraticBezierTo => $"Q{P(c.Control1)} {P(c.EndPoint)}",
                    PathCommandType.CubicBezierTo => $"C{P(c.Control1)} {P(c.Control2)} {P(c.EndPoint)}",
                    PathCommandType.ArcTo => $"A{P(c.Radius)} {c.RotationDegrees:R} {c.LargeArc} {c.Sweep} {P(c.EndPoint)}",
                    _ => "Z",
                });
            }
        }

        return tokens;
    }

    /// <summary>
    ///     Test-local copy of the per-point re-issue loop that the renderer packages used before
    ///     <see cref="PathBuilder.AppendPath(GeoPath, Matrix3x2)"/> existed; the independent oracle for
    ///     every path without arcs.
    /// </summary>
    /// <param name="builder">The destination builder.</param>
    /// <param name="source">The path to re-issue.</param>
    /// <param name="transform">The transform applied to every point.</param>
    private static void LegacyAppendTransformed(PathBuilder builder, GeoPath source, Matrix3x2 transform)
    {
        foreach (var subpath in source.Subpaths)
        {
            builder.MoveTo(Vector2.Transform(subpath.Start, transform));
            foreach (var command in subpath.Commands)
            {
                switch (command.Type)
                {
                    case PathCommandType.LineTo:
                        builder.LineTo(Vector2.Transform(command.EndPoint, transform));
                        break;
                    case PathCommandType.QuadraticBezierTo:
                        builder.QuadraticBezierTo(
                            Vector2.Transform(command.Control1, transform),
                            Vector2.Transform(command.EndPoint, transform));
                        break;
                    case PathCommandType.CubicBezierTo:
                        builder.CubicBezierTo(
                            Vector2.Transform(command.Control1, transform),
                            Vector2.Transform(command.Control2, transform),
                            Vector2.Transform(command.EndPoint, transform));
                        break;
                    case PathCommandType.Close:
                        builder.Close();
                        break;
                    default:
                        throw new NotSupportedException("The oracle only knows arc-free paths.");
                }
            }
        }
    }

    /// <summary>
    ///     Builds an arc-free path with a closed polygon, an open curve subpath, and a closed curve subpath.
    /// </summary>
    /// <returns>The path.</returns>
    private static GeoPath ArcFreeSamplePath() =>
        new PathBuilder()
            .MoveTo(new Vector2(1, 2)).LineTo(new Vector2(5, 2)).LineTo(new Vector2(5, 6)).Close()
            .MoveTo(new Vector2(-3, 4)).QuadraticBezierTo(new Vector2(-1, 8), new Vector2(2, 9))
            .CubicBezierTo(new Vector2(3, 10), new Vector2(4, 7), new Vector2(6, 7.5f))
            .MoveTo(new Vector2(10, 10)).CubicBezierTo(new Vector2(11, 12), new Vector2(13, 12), new Vector2(14, 10)).Close()
            .Build();

    /// <summary>
    ///     Proves that the transformed overload maps every start, control, and end point of every
    ///     command type exactly like the per-point re-issue loop it replaced.
    /// </summary>
    [Fact]
    public void PathBuilder_AppendPath_Transformed_MatchesPerPointTransformOfEveryCommandType()
    {
        // Arrange: an arc-free sample path and a transform with scale, rotation, shear, and translation
        var path = ArcFreeSamplePath();
        var transform = Matrix3x2.CreateScale(1.7f, -0.6f) * Matrix3x2.CreateSkew(0.3f, 0f)
            * Matrix3x2.CreateRotation(0.5f) * Matrix3x2.CreateTranslation(11.25f, -4.5f);
        var oracle = new PathBuilder();
        LegacyAppendTransformed(oracle, path, transform);

        // Act
        var actual = new PathBuilder().AppendPath(path, transform).Build();

        // Assert: identical structure and identical coordinates (compared bit for bit through the round-trip format)
        Assert.Equal(Describe(oracle.Build()), Describe(actual));
    }

    /// <summary>
    ///     Proves that the untransformed overload copies every command as stored - including an arc,
    ///     which is not converted - and keeps coordinates bit-exact (negative zero stays negative zero).
    /// </summary>
    [Fact]
    public void PathBuilder_AppendPath_Untransformed_CopiesEveryCommandVerbatimIncludingArcTo()
    {
        // Arrange: a path with a negative-zero start, every command type including an arc, and a close
        var path = new PathBuilder()
            .MoveTo(new Vector2(-0f, 1))
            .LineTo(new Vector2(2, 1))
            .QuadraticBezierTo(new Vector2(3, 2), new Vector2(4, 1))
            .CubicBezierTo(new Vector2(5, 0), new Vector2(6, 2), new Vector2(7, 1))
            .ArcTo(new Vector2(2, 3), 25, largeArc: true, sweep: false, new Vector2(9, 4))
            .Close()
            .Build();

        // Act
        var copy = new PathBuilder().AppendPath(path).Build();

        // Assert: same description as the source, the arc is still an arc, and the zero kept its sign
        Assert.Equal(Describe(path), Describe(copy));
        Assert.Contains(copy.Subpaths[0].Commands, c => c.Type == PathCommandType.ArcTo);
        Assert.True(float.IsNegative(copy.Subpaths[0].Start.X));
    }

    /// <summary>
    ///     Proves that a closed source subpath stays closed and an open one stays open, for both overloads.
    /// </summary>
    [Fact]
    public void PathBuilder_AppendPath_ClosedAndOpenSubpaths_PreservesClosureFlags()
    {
        // Arrange: closed, open, closed
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).LineTo(new Vector2(1, 1)).Close()
            .MoveTo(new Vector2(5, 5)).LineTo(new Vector2(6, 5))
            .MoveTo(new Vector2(8, 8)).LineTo(new Vector2(9, 8)).LineTo(new Vector2(9, 9)).Close()
            .Build();

        // Act
        var verbatim = new PathBuilder().AppendPath(path).Build();
        var transformed = new PathBuilder().AppendPath(path, Matrix3x2.CreateTranslation(2, 3)).Build();

        // Assert: closure flags and trailing Close commands follow the source in both results
        Assert.Equal([true, false, true], verbatim.Subpaths.Select(s => s.IsClosed));
        Assert.Equal([true, false, true], transformed.Subpaths.Select(s => s.IsClosed));
        Assert.Equal(PathCommandType.Close, verbatim.Subpaths[0].Commands[^1].Type);
        Assert.Equal(PathCommandType.Close, transformed.Subpaths[2].Commands[^1].Type);
    }

    /// <summary>
    ///     Proves that repeated calls accumulate subpaths in call order and that a subpath the builder
    ///     still had open is kept as an open subpath when the next one is appended.
    /// </summary>
    [Fact]
    public void PathBuilder_AppendPath_CalledRepeatedly_AccumulatesSubpathsInOrder()
    {
        // Arrange: a builder with an open subpath of its own, and two single-subpath paths
        var first = new PathBuilder().MoveTo(new Vector2(10, 10)).LineTo(new Vector2(11, 10)).Close().Build();
        var second = new PathBuilder().MoveTo(new Vector2(20, 20)).LineTo(new Vector2(21, 20)).Build();
        var builder = new PathBuilder().MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0));

        // Act
        builder.AppendPath(first).AppendPath(second, Matrix3x2.CreateTranslation(100, 0)).AppendPath(first, Matrix3x2.CreateTranslation(0, 100));
        var result = builder.Build();

        // Assert: four subpaths in order, the builder's own one left open, the translated ones moved
        Assert.Equal(
            [new Vector2(0, 0), new Vector2(10, 10), new Vector2(120, 20), new Vector2(10, 110)],
            result.Subpaths.Select(s => s.Start));
        Assert.Equal([false, true, false, true], result.Subpaths.Select(s => s.IsClosed));
        Assert.Equal(new Vector2(121, 20), result.Subpaths[2].Commands[0].EndPoint);
    }

    /// <summary>
    ///     Proves that appending an empty path changes nothing, for both overloads, including the
    ///     subpath the builder had open.
    /// </summary>
    [Fact]
    public void PathBuilder_AppendPath_EmptyPath_LeavesBuilderUnchanged()
    {
        // Arrange: a builder holding a closed and an open subpath
        var builder = new PathBuilder()
            .MoveTo(new Vector2(0, 0)).LineTo(new Vector2(1, 0)).Close()
            .MoveTo(new Vector2(5, 5)).LineTo(new Vector2(6, 5));
        var before = Describe(builder.Build());

        // Act
        builder.AppendPath(GeoPath.Empty).AppendPath(GeoPath.Empty, Matrix3x2.CreateScale(3));
        builder.LineTo(new Vector2(7, 5));
        var after = builder.Build();

        // Assert: nothing was added, and the open subpath can still be extended afterwards
        Assert.Equal(before.Count + 1, Describe(after).Count);
        Assert.Equal(2, after.Subpaths.Count);
        Assert.Equal(2, after.Subpaths[1].Commands.Count);
        Assert.Equal(GeoPath.Empty, new PathBuilder().AppendPath(GeoPath.Empty).Build());
    }

    /// <summary>
    ///     Proves that both overloads return the same builder instance, so calls can be chained.
    /// </summary>
    [Fact]
    public void PathBuilder_AppendPath_ReturnsSameBuilderForChaining()
    {
        // Arrange
        var builder = new PathBuilder();
        var path = ArcFreeSamplePath();

        // Act
        var verbatim = builder.AppendPath(path);
        var transformed = builder.AppendPath(path, Matrix3x2.Identity);

        // Assert
        Assert.Same(builder, verbatim);
        Assert.Same(builder, transformed);
    }

    /// <summary>
    ///     Proves that an arc is replaced by the cubic Bezier segments of the arc converter, each
    ///     transformed, so a non-uniform transform of an arc stays geometrically correct.
    /// </summary>
    [Fact]
    public void PathBuilder_AppendPath_Transformed_ArcTo_ConvertsToTransformedCubicBeziers()
    {
        // Arrange: a path whose second command is an arc, and a non-uniform scale with rotation
        var radius = new Vector2(4, 2);
        var arcEnd = new Vector2(9, 3);
        var start = new Vector2(1, 1);
        var path = new PathBuilder().MoveTo(new Vector2(0, 0)).LineTo(start)
            .ArcTo(radius, 20, largeArc: false, sweep: true, arcEnd).Build();
        var transform = Matrix3x2.CreateScale(2, 0.5f) * Matrix3x2.CreateRotation(0.7f) * Matrix3x2.CreateTranslation(3, 4);
        var segments = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
        SvgArcConverter.ToBeziers(start, radius, 20, false, true, arcEnd, segments);

        // Act
        var result = new PathBuilder().AppendPath(path, transform).Build();

        // Assert: the line, then one transformed cubic per converter segment, and no arc command
        var commands = Assert.Single(result.Subpaths).Commands;
        Assert.DoesNotContain(commands, c => c.Type == PathCommandType.ArcTo);
        Assert.Equal(1 + segments.Count, commands.Count);
        Assert.NotEmpty(segments);
        for (var i = 0; i < segments.Count; i++)
        {
            var command = commands[i + 1];
            Assert.Equal(PathCommandType.CubicBezierTo, command.Type);
            Assert.Equal(Vector2.Transform(segments[i].Control1, transform), command.Control1);
            Assert.Equal(Vector2.Transform(segments[i].Control2, transform), command.Control2);
            Assert.Equal(Vector2.Transform(segments[i].End, transform), command.EndPoint);
        }

        // Assert: Path.Transform yields the same geometry
        Assert.Equal(Describe(path.Transform(transform)), Describe(result));
    }

    /// <summary>
    ///     Proves that a null path is rejected by both overloads.
    /// </summary>
    [Fact]
    public void PathBuilder_AppendPath_NullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new PathBuilder();

        // Act / Assert
        Assert.Throws<ArgumentNullException>("path", () => builder.AppendPath(null!));
        Assert.Throws<ArgumentNullException>("path", () => builder.AppendPath(null!, Matrix3x2.Identity));
    }
}
