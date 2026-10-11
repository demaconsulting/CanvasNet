using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Tests the sampling of a rendered pattern tile buffer into the painted region
///     (<c>SvgCodec.SampleTileIntoRegion</c>): a magnified tile is smoothly interpolated, a
///     minified tile is area-averaged, and a tile drawn at about one device pixel per tile pixel
///     is copied exactly. Timing is never asserted.
/// </summary>
public class SvgPatternTileSmoothingTests
{
    /// <summary>Samples a one-row <paramref name="tile"/> repeated across a <paramref name="regionWidth"/>-pixel-wide row.</summary>
    /// <param name="tile">The tile buffer.</param>
    /// <param name="tileWidth">Tile width in grid space.</param>
    /// <param name="scale">Uniform grid-to-device scale.</param>
    /// <param name="regionWidth">Painted region width in device pixels.</param>
    private static Surface Sample(Surface tile, float tileWidth, float scale, int regionWidth)
    {
        using var coverage = new Surface(regionWidth, 1);
        coverage.Clear(new Rgba32(255, 255, 255, 255));
        return SvgCodec.SampleTileIntoRegion(
            tile, coverage, 0, 0, regionWidth, 1, 0f, 0f, tileWidth, 1f, Matrix3x2.CreateScale(1f / scale));
    }

    /// <summary>A tile drawn well above one device pixel per tile pixel (a scaled pattern) is smoothly interpolated, not blocky.</summary>
    [Fact]
    public void SampleTileIntoRegion_MagnifiedTile_IsSmooth()
    {
        // A 2x1 black/white tile drawn 20x larger (device scale 20)
        using var tile = new Surface(2, 1);
        tile[0, 0] = new Rgba32(0, 0, 0, 255);
        tile[1, 0] = new Rgba32(255, 255, 255, 255);

        using var result = Sample(tile, tileWidth: 2f, scale: 20f, regionWidth: 40);

        // Nearest sampling would give exactly two levels; bilinear gives a ramp
        var levels = Enumerable.Range(0, 40).Select(x => (int)result[x, 0].R).Distinct().Count();
        Assert.True(levels > 8, $"expected a smooth ramp, saw {levels} levels");

        // The boundary between the two tile pixels (device x = 20) blends them about equally
        Assert.InRange(result[19, 0].R, 110, 145);
        Assert.InRange(result[20, 0].R, 110, 145);

        // The ramp never decreases between the two pixel centers (device x = 10 .. 30)
        for (var x = 11; x <= 30; x++)
        {
            Assert.True(result[x, 0].R >= result[x - 1, 0].R, $"ramp decreased at x={x}");
        }
    }

    /// <summary>A tile drawn smaller than one device pixel per tile pixel is area-averaged so alternating thin features do not alias.</summary>
    [Fact]
    public void SampleTileIntoRegion_MinifiedTile_IsAreaAveraged()
    {
        // An 8x1 tile of alternating black/white pixels drawn at half size: each device pixel covers one black and one white tile pixel
        using var tile = new Surface(8, 1);
        for (var x = 0; x < 8; x++)
        {
            var v = (byte)(x % 2 == 0 ? 0 : 255);
            tile[x, 0] = new Rgba32(v, v, v, 255);
        }

        using var result = Sample(tile, tileWidth: 8f, scale: 0.5f, regionWidth: 16);

        // Point sampling would alias to solid black or white stripes; averaging gives uniform mid-gray
        for (var x = 0; x < 16; x++)
        {
            Assert.InRange(result[x, 0].R, 125, 130);
        }
    }

    /// <summary>A tile drawn at one device pixel per tile pixel is copied exactly (no smoothing), repeating with wrap-around.</summary>
    [Fact]
    public void SampleTileIntoRegion_OneToOneTile_IsExactCopy()
    {
        using var tile = new Surface(4, 1);
        Rgba32[] colors = [new(255, 0, 0, 255), new(0, 255, 0, 255), new(0, 0, 255, 255), new(10, 20, 30, 255)];
        for (var x = 0; x < 4; x++)
        {
            tile[x, 0] = colors[x];
        }

        using var result = Sample(tile, tileWidth: 4f, scale: 1f, regionWidth: 8);

        for (var x = 0; x < 8; x++)
        {
            Assert.Equal(colors[x % 4], result[x, 0]);
        }
    }
}
