using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for <see cref="PathStroker"/>.
/// </summary>
public class PathStrokerTests
{
    /// <summary>
    ///     Proves that a butt-capped horizontal line becomes an exact rectangle with no extension.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_HorizontalLineButtCap_FillsExactRectangleNoExtension()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act
        var surface = RenderStroke(path, style, 8, 5);

        // Assert
        Assert.Equal((byte)255, surface[2, 1].A);
        Assert.Equal((byte)255, surface[5, 2].A);
        Assert.Equal((byte)0, surface[1, 1].A);
        Assert.Equal((byte)0, surface[6, 1].A);
    }

    /// <summary>
    ///     Proves that a round-capped horizontal line renders semicircular end caps.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_HorizontalLineRoundCap_FillsRectanglePlusSemicircularEnds()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Round);

        // Act
        var surface = RenderStroke(path, style, 8, 5);

        // Assert
        Assert.Equal((byte)255, surface[2, 1].A);
        Assert.Equal((byte)166, surface[1, 1].A);
        Assert.Equal((byte)166, surface[6, 1].A);
        Assert.Equal((byte)0, surface[0, 1].A);
    }

    /// <summary>
    ///     Proves that a square-capped horizontal line extends by half the stroke width at each
    ///     end.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_HorizontalLineSquareCap_FillsRectanglePlusHalfWidthExtension()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Square);

        // Act
        var surface = RenderStroke(path, style, 9, 5);

        // Assert
        Assert.Equal((byte)255, surface[1, 1].A);
        Assert.Equal((byte)255, surface[6, 2].A);
        Assert.Equal((byte)0, surface[0, 1].A);
        Assert.Equal((byte)0, surface[7, 1].A);
    }

    /// <summary>
    ///     Proves that a right-angle corner with a miter join renders the miter-only corner pixel.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_RightAngleCornerMiterJoin_FillsSharpMiteredCorner()
    {
        // Arrange
        var path = BuildRightAnglePath();
        var style = new StrokeStyle(4f, cap: LineCap.Butt, join: LineJoin.Miter, miterLimit: 4f);

        // Act
        var surface = RenderStroke(path, style, 14, 12);

        // Assert
        Assert.Equal((byte)255, surface[9, 2].A);
        Assert.Equal((byte)255, surface[8, 2].A);
    }

    /// <summary>
    ///     Proves that a right-angle corner with a round join renders the quarter-circle corner
    ///     coverage.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_RightAngleCornerRoundJoin_FillsRoundedCorner()
    {
        // Arrange
        var path = BuildRightAnglePath();
        var style = new StrokeStyle(4f, cap: LineCap.Butt, join: LineJoin.Round);

        // Act
        var surface = RenderStroke(path, style, 14, 12);

        // Assert
        Assert.Equal((byte)202, surface[8, 2].A);
        Assert.Equal((byte)62, surface[9, 2].A);
    }

    /// <summary>
    ///     Proves that a right-angle corner with a bevel join omits the miter-only corner pixel.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_RightAngleCornerBevelJoin_FillsFlatBeveledCorner()
    {
        // Arrange
        var path = BuildRightAnglePath();
        var style = new StrokeStyle(4f, cap: LineCap.Butt, join: LineJoin.Bevel);

        // Act
        var surface = RenderStroke(path, style, 14, 12);

        // Assert
        Assert.Equal((byte)0, surface[9, 2].A);
        Assert.Equal((byte)128, surface[8, 2].A);
    }

    /// <summary>
    ///     Proves that stroking a closed rectangle produces a ring with an unfilled center and an
    ///     unfilled exterior.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ClosedRectangle_FillsRingLeavingInteriorAndExteriorUnfilled()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .LineTo(new Vector2(6, 6))
            .LineTo(new Vector2(2, 6))
            .Close()
            .Build();
        var style = new StrokeStyle(2f);

        // Act
        var surface = RenderStroke(path, style, 8, 8);

        // Assert
        Assert.Equal((byte)255, surface[1, 3].A);
        Assert.Equal((byte)0, surface[3, 3].A);
        Assert.Equal((byte)0, surface[0, 0].A);
    }

    /// <summary>
    ///     Proves that a closed subpath reduced to exactly two distinct points (a zero-area,
    ///     degenerate closed contour that immediately doubles back over the same segment) still
    ///     renders as a stroked line segment, rather than vanishing entirely because its coincident
    ///     "outer" and "inner" offset rings would otherwise be forced into opposite winding and
    ///     cancel under FillRule.NonZero. The expected pixel coverage matches the equivalent
    ///     open, butt-capped, two-point stroke exactly (see
    ///     <see cref="PathStroker_Stroke_HorizontalLineButtCap_FillsExactRectangleNoExtension"/>).
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ClosedTwoPointSubpath_FillsStrokedSegmentAreaNotEmpty()
    {
        // Arrange: a closed subpath (M ... L ... Z) with only two distinct points
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .Close()
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act: rasterize and sample actual pixel coverage, not just outline generation
        var surface = RenderStroke(path, style, 8, 5);

        // Assert: coverage matches the equivalent open two-point butt-capped stroke
        Assert.Equal((byte)255, surface[2, 1].A);
        Assert.Equal((byte)255, surface[5, 2].A);
        Assert.Equal((byte)0, surface[1, 1].A);
        Assert.Equal((byte)0, surface[6, 1].A);
    }

    /// <summary>
    ///     Proves that a dash array whose entries individually are finite but whose summed total
    ///     pattern length would overflow a naive float32 accumulation does not hang the public
    ///     <see cref="PathStroker.Stroke(Path, StrokeStyle, float)"/> entry point, when combined
    ///     with a negative dash offset. This test intentionally makes no timing assertion: it
    ///     relies only on xUnit's normal test execution completing to prove there is no infinite
    ///     loop reachable through the public stroking API.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_OverflowProneDashArrayWithNegativeOffset_CompletesWithoutHanging()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(10, 0))
            .Build();
        var style = new StrokeStyle(2f, dashArray: [float.MaxValue, float.MaxValue], dashOffset: -1f);

        // Act
        var stroked = PathStroker.Stroke(path, style);

        // Assert: the call returned (did not hang) with a well-formed result
        Assert.NotNull(stroked);
    }

    /// <summary>
    ///     Proves that a sharp corner exceeding the miter limit falls back to bevel geometry.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_SharpAngleExceedingMiterLimit_FallsBackToBevel()
    {
        // Arrange
        var path = BuildRightAnglePath();
        var style = new StrokeStyle(4f, cap: LineCap.Butt, join: LineJoin.Miter, miterLimit: 1f);

        // Act
        var surface = RenderStroke(path, style, 14, 12);

        // Assert
        Assert.Equal((byte)0, surface[9, 2].A);
        Assert.Equal((byte)128, surface[8, 2].A);
    }

    /// <summary>
    ///     Proves that dashing leaves gaps unfilled between visible stroke segments.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_DashedLine_FillsOnlyOnSegmentsLeavingGapsUnfilled()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 2))
            .LineTo(new Vector2(10, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt, dashArray: [2f, 2f]);

        // Act
        var surface = RenderStroke(path, style, 11, 5);

        // Assert
        Assert.Equal((byte)255, surface[0, 1].A);
        Assert.Equal((byte)0, surface[2, 1].A);
        Assert.Equal((byte)255, surface[4, 1].A);
        Assert.Equal((byte)0, surface[6, 1].A);
    }

    /// <summary>
    ///     Proves that a zero-length subpath with a round cap becomes a full circle.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ZeroLengthSubpathRoundCap_FillsCircleOfRadiusHalfWidth()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(2, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Round);

        // Act
        var surface = RenderStroke(path, style, 5, 5);

        // Assert
        Assert.Equal((byte)148, surface[1, 1].A);
        Assert.Equal((byte)0, surface[0, 0].A);
    }

    /// <summary>
    ///     Proves that a zero-length subpath with a square cap becomes a width-by-width square.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ZeroLengthSubpathSquareCap_FillsSquareOfSideWidth()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(2, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Square);

        // Act
        var surface = RenderStroke(path, style, 5, 5);

        // Assert
        Assert.Equal((byte)255, surface[1, 1].A);
        Assert.Equal((byte)255, surface[2, 2].A);
        Assert.Equal((byte)0, surface[0, 0].A);
    }

    /// <summary>
    ///     Proves that a zero-length subpath with a butt cap contributes no fillable area.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_ZeroLengthSubpathButtCap_ProducesNoFilledPixels()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(2, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Butt);

        // Act
        var surface = RenderStroke(path, style, 5, 5);

        // Assert
        Assert.Equal((byte)0, surface[1, 1].A);
        Assert.Equal((byte)0, surface[2, 2].A);
    }

    /// <summary>
    ///     Proves that a single-point subpath matches the zero-length-subpath cap behavior.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_SinglePointSubpath_MatchesZeroLengthSubpathBehavior()
    {
        // Arrange
        var singlePointPath = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .Build();
        var zeroLengthPath = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(2, 2))
            .Build();
        var style = new StrokeStyle(2f, cap: LineCap.Round);

        // Act
        var singlePoint = RenderStroke(singlePointPath, style, 5, 5);
        var zeroLength = RenderStroke(zeroLengthPath, style, 5, 5);

        // Assert
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.Equal(zeroLength[x, y], singlePoint[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that stroking an empty path returns <see cref="Path.Empty"/> and renders no
    ///     pixels when subsequently filled.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_EmptyPath_ReturnsEmptyPathNoOp()
    {
        // Arrange
        var style = new StrokeStyle(2f);

        // Act
        var stroked = PathStroker.Stroke(Path.Empty, style);
        var surface = new Surface(3, 3);
        PathFiller.Fill(surface, stroked, new Rgba32(255, 0, 0, 255));

        // Assert
        Assert.Same(Path.Empty, stroked);
        Assert.Equal((byte)0, surface[1, 1].A);
    }

    /// <summary>
    ///     Proves that a null path is rejected.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_NullPath_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => PathStroker.Stroke(null!, new StrokeStyle(1f)));
    }

    /// <summary>
    ///     Proves that a null stroke style is rejected.
    /// </summary>
    [Fact]
    public void PathStroker_Stroke_NullStrokeStyle_ThrowsArgumentNullException()
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(1, 0))
            .Build();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PathStroker.Stroke(path, null!));
    }

    /// <summary>
    ///     Proves that a non-positive or non-finite flatten tolerance is rejected.
    /// </summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void PathStroker_Stroke_NonPositiveFlattenTolerance_ThrowsArgumentOutOfRangeException(float tolerance)
    {
        // Arrange
        var path = new PathBuilder()
            .MoveTo(new Vector2(0, 0))
            .LineTo(new Vector2(1, 0))
            .Build();
        var style = new StrokeStyle(1f);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => PathStroker.Stroke(path, style, tolerance));
    }

    private static Path BuildRightAnglePath() => new PathBuilder()
        .MoveTo(new Vector2(4, 4))
        .LineTo(new Vector2(8, 4))
        .LineTo(new Vector2(8, 8))
        .Build();

    private static Surface RenderStroke(Path path, StrokeStyle style, int width, int height, float flattenTolerance = 0.25f)
    {
        var stroked = PathStroker.Stroke(path, style, flattenTolerance);
        var surface = new Surface(width, height);
        PathFiller.Fill(surface, stroked, new Rgba32(255, 0, 0, 255), FillRule.NonZero, flattenTolerance);
        return surface;
    }
}
