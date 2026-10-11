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

        plan.Release();
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
        plan.Release();
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
        plan.Release();
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
        positivePlan.Release();
        negativePlan.Release();
    }

    /// <summary>A tile enlarged 4x is smoothly interpolated instead of showing hard blocks.</summary>
    [Fact]
    public void EvaluateRow_MagnifiedTile_IsSmooth()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var tile = new TilePaint(tileSurface, Matrix3x2.CreateScale(4f), 2f, 2f);
        var plan = TilePaintEvaluator.CreatePlan(tile);
        try
        {
            Assert.True(plan.Magnified);
            Span<Rgba32> row = stackalloc Rgba32[8];
            TilePaintEvaluator.EvaluateRow(in plan, 0, 0, 8, row);
            var distinct = new HashSet<byte>();
            foreach (var pixel in row)
            {
                distinct.Add(pixel.R);
            }

            Assert.True(distinct.Count > 2);
        }
        finally
        {
            plan.Release();
        }
    }

    /// <summary>Tile scale classification is shared: magnification on either axis wins (mixed case), otherwise only minified axes are reduced.</summary>
    [Fact]
    public void ClassifyTileScale_MixedAndSingleAxisCases()
    {
        // Mixed: one axis magnified, the other minified -> magnified, no prefilter
        Assert.False(SurfaceResampler.ClassifyTileScale(2.0, 0.5, 8, 8, out var magnified, out _, out _));
        Assert.True(magnified);

        // Minified on one axis only: only that axis is reduced
        Assert.True(SurfaceResampler.ClassifyTileScale(0.5, 1.0, 8, 8, out magnified, out var w, out var h));
        Assert.False(magnified);
        Assert.Equal((4, 8), (w, h));

        // Near 1:1 and non-finite/degenerate scales are left untouched
        Assert.False(SurfaceResampler.ClassifyTileScale(1.04, 0.96, 8, 8, out magnified, out _, out _));
        Assert.False(magnified);
        Assert.False(SurfaceResampler.ClassifyTileScale(double.NaN, 0.5, 8, 8, out magnified, out _, out _));
        Assert.False(magnified);
    }

    /// <summary>A mixed magnified/minified tile is sampled bilinearly without a prefiltered copy.</summary>
    [Fact]
    public void CreatePlan_MixedMagnifiedAndMinifiedTile_IsMagnifiedOnly()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var plan = TilePaintEvaluator.CreatePlan(new TilePaint(tileSurface, Matrix3x2.CreateScale(4f, 0.25f), 2f, 2f));
        try
        {
            Assert.True(plan.Magnified);
            Assert.Null(plan.Minified);
        }
        finally
        {
            plan.Release();
        }
    }

    /// <summary>A tile at exactly one device pixel per tile pixel keeps exact pixel values.</summary>
    [Fact]
    public void CreatePlan_OneToOneTile_IsNeitherMagnifiedNorMinified()
    {
        using var tileSurface = CreateCheckerboardTileSurface();
        var plan = TilePaintEvaluator.CreatePlan(new TilePaint(tileSurface, Matrix3x2.Identity, 2f, 2f));
        try
        {
            Assert.False(plan.Magnified);
            Assert.Null(plan.Minified);
        }
        finally
        {
            plan.Release();
        }
    }

    /// <summary>A tile shrunk to a quarter is replaced by an area-averaged copy that averages the checkerboard.</summary>
    [Fact]
    public void EvaluateRow_MinifiedTile_AveragesColors()
    {
        using var tileSurface = new Surface(8, 8);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                tileSurface[x, y] = ((x + y) & 1) == 0 ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255);
            }
        }

        var plan = TilePaintEvaluator.CreatePlan(new TilePaint(tileSurface, Matrix3x2.CreateScale(0.25f), 8f, 8f));
        try
        {
            Assert.NotNull(plan.Minified);
            Span<Rgba32> row = stackalloc Rgba32[2];
            TilePaintEvaluator.EvaluateRow(in plan, 0, 0, 2, row);
            Assert.InRange(row[0].R, 100, 155);
        }
        finally
        {
            plan.Release();
        }
    }
}
