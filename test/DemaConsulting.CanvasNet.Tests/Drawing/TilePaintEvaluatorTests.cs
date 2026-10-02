using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Tests.Drawing;

/// <summary>
///     Unit tests for the internal <see cref="TilePaintEvaluator"/> class.
/// </summary>
public class TilePaintEvaluatorTests
{
    /// <summary>
    ///     Builds a 2x2 tile surface split into a red/blue 2-color checkerboard (red at (0,0)
    ///     and (1,1), blue at (1,0) and (0,1)).
    /// </summary>
    private static Surface CreateCheckerboardTileSurface()
    {
        var surface = new Surface(2, 2);
        surface[0, 0] = new Rgba32(255, 0, 0, 255);
        surface[1, 0] = new Rgba32(0, 0, 255, 255);
        surface[0, 1] = new Rgba32(0, 0, 255, 255);
        surface[1, 1] = new Rgba32(255, 0, 0, 255);
        return surface;
    }

    /// <summary>
    ///     Proves that CreatePlan throws ArgumentNullException when the tile is null.
    /// </summary>
    [Fact]
    public void CreatePlan_NullTile_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TilePaintEvaluator.CreatePlan(null!));
    }

    /// <summary>
    ///     Proves that a non-invertible (all-zero) Transform causes EvaluateRow to write fully
    ///     transparent pixels for the whole row - this is the documented, deliberate
    ///     "paint nothing" Degenerate Transform policy, which intentionally diverges from
    ///     Gradient's own "flat-fill with the last stop color" policy (see
    ///     TilePaintEvaluator's remarks).
    /// </summary>
    [Fact]
    public void EvaluateRow_NonInvertibleTransform_WritesFullyTransparentRow()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var tile = new TilePaint(tileSurface, default, 2f, 2f);
        var plan = TilePaintEvaluator.CreatePlan(tile);

        Span<Rgba32> destination = stackalloc Rgba32[4];
        TilePaintEvaluator.EvaluateRow(in plan, 0, 0, 4, destination);

        foreach (var pixel in destination)
        {
            Assert.Equal(default, pixel);
        }
    }

    /// <summary>
    ///     Proves that an in-bounds sample against an identity-transform tile produces the exact
    ///     expected tile pixel at each device point within the first tile repetition.
    /// </summary>
    [Fact]
    public void EvaluateRow_IdentityTransform_SamplesExpectedTilePixels()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var tile = new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f);
        var plan = TilePaintEvaluator.CreatePlan(tile);

        Span<Rgba32> destination = stackalloc Rgba32[2];
        TilePaintEvaluator.EvaluateRow(in plan, 0, 0, 2, destination);

        Assert.Equal(new Rgba32(255, 0, 0, 255), destination[0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), destination[1]);
    }

    /// <summary>
    ///     Proves that sampling beyond one tile repetition wraps around via modulo - sampling at
    ///     x=2.5 (one full XStep=2 beyond x=0.5) produces the same color as sampling at x=0.5.
    /// </summary>
    [Fact]
    public void EvaluateRow_WrapAroundBeyondOneTile_MatchesFirstTileRepetition()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var tile = new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f);
        var plan = TilePaintEvaluator.CreatePlan(tile);

        Span<Rgba32> destination = stackalloc Rgba32[4];
        TilePaintEvaluator.EvaluateRow(in plan, 0, 0, 4, destination);

        // Columns 0 and 2 both sample pattern-space x in [0,1) -> same wrapped tile pixel.
        Assert.Equal(destination[0], destination[2]);
        // Columns 1 and 3 both sample pattern-space x in [1,2) -> same wrapped tile pixel.
        Assert.Equal(destination[1], destination[3]);
    }

    /// <summary>
    ///     Proves that a negative XStep/YStep still samples correctly - wraparound uses the
    ///     absolute value of the step, not the signed value.
    /// </summary>
    [Fact]
    public void EvaluateRow_NegativeSteps_SamplesUsingAbsoluteValueWraparound()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var positiveTile = new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f);
        var negativeTile = new TilePaint(tileSurface, Matrix3x2.Identity, -2f, -2f);

        var positivePlan = TilePaintEvaluator.CreatePlan(positiveTile);
        var negativePlan = TilePaintEvaluator.CreatePlan(negativeTile);

        Span<Rgba32> positiveRow = stackalloc Rgba32[2];
        Span<Rgba32> negativeRow = stackalloc Rgba32[2];
        TilePaintEvaluator.EvaluateRow(in positivePlan, 0, 0, 2, positiveRow);
        TilePaintEvaluator.EvaluateRow(in negativePlan, 0, 0, 2, negativeRow);

        Assert.Equal(positiveRow[0], negativeRow[0]);
        Assert.Equal(positiveRow[1], negativeRow[1]);
    }
}
