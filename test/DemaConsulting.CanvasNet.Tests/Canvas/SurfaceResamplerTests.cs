using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for the internal <c>SurfaceResampler</c> helper (box prefilter + bilinear sampler).
/// </summary>
public class SurfaceResamplerTests
{
    /// <summary>Upscaling or 1:1 mapping returns the very same surface instance (no filtering).</summary>
    [Fact]
    public void Prefilter_UpscaleOrIdentity_ReturnsSourceUnchanged()
    {
        using var source = new Surface(4, 4);
        Assert.Same(source, SurfaceResampler.Prefilter(source, Matrix3x2.Identity, out var b1));
        Assert.Same(source, SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(3f, 2f), out var b2));
        Assert.False(b1);
        Assert.False(b2);
    }

    /// <summary>Non-finite or degenerate transforms are never filtered.</summary>
    [Fact]
    public void Prefilter_NonFiniteOrDegenerateMatrix_ReturnsSourceUnchanged()
    {
        using var source = new Surface(4, 4);
        Assert.Same(source, SurfaceResampler.Prefilter(source, new Matrix3x2(float.NaN, 0, 0, 1f, 0, 0), out _));
        Assert.Same(source, SurfaceResampler.Prefilter(source, new Matrix3x2(float.PositiveInfinity, 0, 0, 1f, 0, 0), out _));
        Assert.Same(source, SurfaceResampler.Prefilter(source, new Matrix3x2(0, 0, 0, 0, 0, 0), out _));
    }

    /// <summary>An exact 2:1 reduction averages each 2x2 block.</summary>
    [Fact]
    public void Prefilter_ExactTwoToOne_AveragesBlocks()
    {
        using var source = new Surface(4, 2);
        source[0, 0] = new Rgba32(0, 0, 0, 255);
        source[1, 0] = new Rgba32(255, 255, 255, 255);
        source[0, 1] = new Rgba32(255, 255, 255, 255);
        source[1, 1] = new Rgba32(0, 0, 0, 255);
        for (var x = 2; x < 4; x++)
        {
            source[x, 0] = new Rgba32(100, 100, 100, 255);
            source[x, 1] = new Rgba32(100, 100, 100, 255);
        }

        using var reduced = SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(0.5f, 0.5f), out _);

        Assert.Equal(2, reduced.Width);
        Assert.Equal(1, reduced.Height);
        Assert.Equal(new Rgba32(128, 128, 128, 255), reduced[0, 0]);
        Assert.Equal(new Rgba32(100, 100, 100, 255), reduced[1, 0]);
    }

    /// <summary>Fractional and anisotropic ratios use the nearest integer ratio per axis; partial edge blocks average covered pixels only.</summary>
    [Fact]
    public void Prefilter_FractionalAnisotropic_UsesRoundedFactorsAndPartialEdgeBlocks()
    {
        Assert.Equal((2, 1), SurfaceResampler.ComputeFactors(10, 10, Matrix3x2.CreateScale(0.45f, 1.5f)));
        Assert.Equal((2, 3), SurfaceResampler.ComputeFactors(10, 10, Matrix3x2.CreateScale(0.45f, 0.3f)));

        using var source = new Surface(5, 1);
        for (var x = 0; x < 5; x++)
        {
            source[x, 0] = new Rgba32(100, 100, 100, 255);
        }

        source[4, 0] = new Rgba32(200, 200, 200, 255);
        using var reduced = SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(0.45f, 1f), out _);

        Assert.Equal(3, reduced.Width);
        Assert.Equal(100, reduced[0, 0].R);
        Assert.Equal(100, reduced[1, 0].R);
        Assert.Equal(200, reduced[2, 0].R); // last block covers only the final pixel
    }

    /// <summary>
    ///     A reduced surface with a partial last block is sampled by physical position: the boundary
    ///     between original pixels 2 and 3 (x = 3) lands exactly on the center of reduced pixel 1.
    /// </summary>
    [Fact]
    public void SampleBilinearReduced_PartialLastBlock_MapsByFactorNotByUniformScale()
    {
        using var source = new Surface(5, 1);
        for (var x = 0; x < 5; x++)
        {
            source[x, 0] = x < 3 ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 255);
        }

        using var reduced = SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(0.45f, 1f), out _, out var fx, out var fy);

        Assert.Equal(3, reduced.Width);
        Assert.Equal(2, fx);
        Assert.Equal(1, fy);
        Assert.Equal(0, reduced[0, 0].R);
        Assert.Equal(128, reduced[1, 0].R); // (0 + 255) / 2
        Assert.Equal(255, reduced[2, 0].R);

        // x = 3 is the center of reduced pixel 1 (covering 2..4): exactly that block's value
        Assert.Equal(128, SurfaceResampler.SampleBilinearReduced(reduced, 3f, 0.5f, fx, fy, 5, 1).R);

        // x = 1 is the center of reduced pixel 0
        Assert.Equal(0, SurfaceResampler.SampleBilinearReduced(reduced, 1f, 0.5f, fx, fy, 5, 1).R);

        // Halfway between those centers (x = 2) blends them equally
        Assert.InRange(SurfaceResampler.SampleBilinearReduced(reduced, 2f, 0.5f, fx, fy, 5, 1).R, 63, 65);

        // The last (partial, one-pixel) block's center x = 4.5 is exactly that reduced pixel
        Assert.Equal(255, SurfaceResampler.SampleBilinearReduced(reduced, 4.5f, 0.5f, fx, fy, 5, 1).R);

        // Halfway between block 1's center (3) and the last block's center (4.5) blends them equally
        Assert.InRange(SurfaceResampler.SampleBilinearReduced(reduced, 3.75f, 0.5f, fx, fy, 5, 1).R, 190, 192);

        // Uniform normalization (x / 5) would have returned about 166 at x = 3
        Assert.NotInRange(SurfaceResampler.SampleBilinear(reduced, 3f / 5f, 0.5f).R, 120, 136);
    }

    /// <summary>The vertical axis is mapped by its own factor when the last block is partial.</summary>
    [Fact]
    public void SampleBilinearReduced_PartialLastBlockVertical_MapsByFactor()
    {
        using var source = new Surface(1, 5);
        for (var y = 0; y < 5; y++)
        {
            source[0, y] = y < 3 ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 255);
        }

        using var reduced = SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(1f, 0.45f), out _, out var fx, out var fy);

        Assert.Equal(1, fx);
        Assert.Equal(2, fy);
        Assert.Equal(128, SurfaceResampler.SampleBilinearReduced(reduced, 0.5f, 3f, fx, fy, 1, 5).R);
        Assert.Equal(255, SurfaceResampler.SampleBilinearReduced(reduced, 0.5f, 4.5f, fx, fy, 1, 5).R);
    }

    /// <summary>Factor 3 over width 7 (blocks 3, 3, 1): the one-pixel last block's center and the transition map by actual block centers.</summary>
    [Fact]
    public void SampleBilinearReduced_Factor3Width7_LastBlockCenterExact()
    {
        using var source = new Surface(7, 1);
        for (var x = 0; x < 7; x++)
        {
            source[x, 0] = x < 6 ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 255);
        }

        using var reduced = SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(0.33f, 1f), out _, out var fx, out var fy);

        Assert.Equal(3, fx);
        Assert.Equal(3, reduced.Width);
        Assert.Equal(255, SurfaceResampler.SampleBilinearReduced(reduced, 6.5f, 0.5f, fx, fy, 7, 1).R);
        Assert.Equal(0, SurfaceResampler.SampleBilinearReduced(reduced, 4.5f, 0.5f, fx, fy, 7, 1).R);

        // Halfway between centers 4.5 and 6.5 (x = 5.5) blends equally
        Assert.InRange(SurfaceResampler.SampleBilinearReduced(reduced, 5.5f, 0.5f, fx, fy, 7, 1).R, 127, 129);
    }

    /// <summary>The rotated/skewed fallback path samples a partial last block at its own center rather than blending in the previous block.</summary>
    [Fact]
    public void SampleBilinearReduced_FallbackPathScenario_LastPixelDoesNotBleedPreviousBlock()
    {
        using var source = new Surface(5, 5);
        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                source[x, y] = x == 4 ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255);
            }
        }

        // Rotated and shrunk: no axis-aligned fast path applies, so callers use the prefilter + sampler
        var transform = Matrix3x2.CreateScale(0.45f) * Matrix3x2.CreateRotation(0.3f);
        using var reduced = SurfaceResampler.Prefilter(source, transform, out _, out var fx, out var fy);

        Assert.Equal(2, fx);
        Assert.Equal(2, fy);
        Assert.Equal(255, SurfaceResampler.SampleBilinearReduced(reduced, 4.5f, 2.5f, fx, fy, 5, 5).R);
    }

    /// <summary>Averaging is premultiplied: a transparent pixel contributes no color.</summary>
    [Fact]
    public void Prefilter_PremultipliedAlpha_TransparentPixelsDoNotBleedColor()
    {
        using var source = new Surface(2, 1);
        source[0, 0] = new Rgba32(255, 0, 0, 255);
        source[1, 0] = new Rgba32(0, 255, 0, 0);

        using var reduced = SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(0.5f, 1f), out _);

        var pixel = reduced[0, 0];
        Assert.InRange(pixel.A, 127, 128);
        Assert.Equal(255, pixel.R);
        Assert.Equal(0, pixel.G);
    }

    /// <summary>An extreme ratio collapses to a single pixel holding the global average.</summary>
    [Fact]
    public void Prefilter_ExtremeRatio_ReducesToOnePixel()
    {
        using var source = new Surface(64, 64);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                source[x, y] = x < 32 ? new Rgba32(0, 0, 0, 255) : new Rgba32(255, 255, 255, 255);
            }
        }

        using var reduced = SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(1e-6f, 1e-6f), out _);

        Assert.Equal(1, reduced.Width);
        Assert.Equal(1, reduced.Height);
        Assert.Equal(new Rgba32(128, 128, 128, 255), reduced[0, 0]);
    }

    /// <summary>A mild shrink (below 1.5:1) needs no block reduction but still requests bilinear sampling.</summary>
    [Fact]
    public void Prefilter_MildShrink_ReturnsSourceButRequestsBilinear()
    {
        using var source = new Surface(10, 10);
        Assert.Same(source, SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(0.8f), out var bilinear));
        Assert.True(bilinear);
    }

    /// <summary>Block factors below 1 are rejected.</summary>
    [Fact]
    public void BoxReduce_FactorBelowOne_ThrowsArgumentOutOfRange()
    {
        using var source = new Surface(4, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceResampler.BoxReduce(source, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceResampler.BoxReduce(source, 1, -1));
    }

    /// <summary>A shrink on one axis and an enlargement on the other reduces only the shrunk axis and requests bilinear sampling.</summary>
    [Fact]
    public void Prefilter_MixedAxisShrinkAndEnlarge_ReducesOnlyShrunkAxis()
    {
        using var source = new Surface(4, 2);
        for (var x = 0; x < 4; x++)
        {
            source[x, 0] = x < 2 ? new Rgba32(0, 0, 0, 255) : new Rgba32(200, 200, 200, 255);
            source[x, 1] = source[x, 0];
        }

        using var reduced = SurfaceResampler.Prefilter(source, Matrix3x2.CreateScale(0.5f, 2f), out var bilinear);

        Assert.True(bilinear);
        Assert.NotSame(source, reduced);
        Assert.Equal(2, reduced.Width);
        Assert.Equal(2, reduced.Height);
        Assert.Equal(new Rgba32(0, 0, 0, 255), reduced[0, 1]);
        Assert.Equal(new Rgba32(200, 200, 200, 255), reduced[1, 1]);
    }

    /// <summary>Rotating the mapping by 90 degrees still detects the shrink via column norms.</summary>
    [Fact]
    public void ComputeFactors_Rotated_UsesColumnNorms()
    {
        var m = Matrix3x2.CreateScale(0.25f) * Matrix3x2.CreateRotation(MathF.PI / 2);
        Assert.Equal((4, 4), SurfaceResampler.ComputeFactors(100, 100, m));
        var m30 = Matrix3x2.CreateScale(0.5f) * Matrix3x2.CreateRotation(MathF.PI / 6);
        Assert.Equal((2, 2), SurfaceResampler.ComputeFactors(100, 100, m30));
    }

    /// <summary>Bilinear sampling at a pixel centre returns that pixel and midway blends neighbors.</summary>
    [Fact]
    public void SampleBilinear_CentreAndMidpoint_InterpolatesAndClampsEdges()
    {
        using var image = new Surface(2, 1);
        image[0, 0] = new Rgba32(0, 0, 0, 255);
        image[1, 0] = new Rgba32(200, 100, 50, 255);

        Assert.Equal(new Rgba32(0, 0, 0, 255), SurfaceResampler.SampleBilinear(image, 0.25f, 0.5f));
        Assert.Equal(new Rgba32(200, 100, 50, 255), SurfaceResampler.SampleBilinear(image, 0.75f, 0.5f));
        Assert.Equal(new Rgba32(100, 50, 25, 255), SurfaceResampler.SampleBilinear(image, 0.5f, 0.5f));
        Assert.Equal(new Rgba32(0, 0, 0, 255), SurfaceResampler.SampleBilinear(image, 0f, 0f));
        Assert.Equal(new Rgba32(200, 100, 50, 255), SurfaceResampler.SampleBilinear(image, 0.999f, 0.999f));
    }

    /// <summary>Rotation or skew is rejected so callers use the inverse-mapped fallback.</summary>
    [Fact]
    public void TryResampleToDevice_RotatedMatrix_ReturnsFalse()
    {
        using var source = new Surface(4, 4);
        Assert.False(SurfaceResampler.TryResampleToDevice(source, Matrix3x2.CreateRotation(0.5f), 20, 20, ResamplingMode.Auto, out var window, out _, out _));
        Assert.Null(window);
    }

    /// <summary>An axis-aligned enlargement yields a window covering exactly the image rectangle.</summary>
    [Fact]
    public void TryResampleToDevice_AxisAligned_ReturnsWindowAtOrigin()
    {
        using var source = new Surface(2, 2);
        source.Clear(new Rgba32(10, 20, 30, 255));
        var matrix = Matrix3x2.CreateScale(5f) * Matrix3x2.CreateTranslation(3f, 4f);
        Assert.True(SurfaceResampler.TryResampleToDevice(source, matrix, 40, 40, ResamplingMode.Auto, out var window, out var ox, out var oy));
        using (window)
        {
            Assert.NotNull(window);
            Assert.Equal((3, 4), (ox, oy));
            Assert.Equal((10, 10), (window.Width, window.Height));
        }
    }

    /// <summary>An image entirely outside the visible area succeeds with no window.</summary>
    [Fact]
    public void TryResampleToDevice_OffScreen_ReturnsTrueWithNullWindow()
    {
        using var source = new Surface(2, 2);
        var matrix = Matrix3x2.CreateScale(5f) * Matrix3x2.CreateTranslation(100f, 100f);
        Assert.True(SurfaceResampler.TryResampleToDevice(source, matrix, 40, 40, ResamplingMode.Auto, out var window, out _, out _));
        Assert.Null(window);
    }

    /// <summary>BoxReduce renormalizes partial edge blocks: a 10x1 ramp reduced by 4 ends with the mean of the two covered pixels, not an edge-folded value.</summary>
    [Fact]
    public void BoxReduce_NonDivisibleSize_AveragesOnlyCoveredPixelsInPartialBlock()
    {
        using var source = new Surface(10, 1);
        for (var x = 0; x < 10; x++)
        {
            var v = (byte)(x * 20);
            source[x, 0] = new Rgba32(v, v, v, 255);
        }

        using var reduced = SurfaceResampler.BoxReduce(source, 4, 1);
        Assert.Equal(3, reduced.Width);
        Assert.Equal(30, reduced[0, 0].R);
        Assert.Equal(110, reduced[1, 0].R);
        Assert.Equal(170, reduced[2, 0].R); // (160 + 180) / 2
    }

    /// <summary>BoxReduce of a non-divisible size with alpha averages premultiplied color over the covered pixels only.</summary>
    [Fact]
    public void BoxReduce_NonDivisibleSizeWithAlpha_PartialBlockUsesCoveredPixelsOnly()
    {
        using var source = new Surface(3, 1);
        source[0, 0] = new Rgba32(0, 0, 0, 0);
        source[1, 0] = new Rgba32(0, 0, 0, 0);
        source[2, 0] = new Rgba32(200, 100, 50, 255);

        using var reduced = SurfaceResampler.BoxReduce(source, 2, 1);
        Assert.Equal(2, reduced.Width);
        Assert.Equal(new Rgba32(0, 0, 0, 0), reduced[0, 0]);
        Assert.Equal(new Rgba32(200, 100, 50, 255), reduced[1, 0]); // the partial block covers only the opaque pixel
    }

    /// <summary>A tiny intermediate budget (multi-strip path) gives output identical to the default budget.</summary>
    [Theory]
    [InlineData(ResamplingMode.Bilinear, 1)]
    [InlineData(ResamplingMode.Bilinear, 64)]
    [InlineData(ResamplingMode.Lanczos3, 1)]
    [InlineData(ResamplingMode.Lanczos3, 64)]
    [InlineData(ResamplingMode.Area, 1)]
    [InlineData(ResamplingMode.Area, 64)]
    public void Resample_TinyIntermediateBudget_MatchesDefaultBudget(ResamplingMode mode, int budget)
    {
        using var source = new Surface(23, 19);
        for (var y = 0; y < 19; y++)
        {
            for (var x = 0; x < 23; x++)
            {
                source[x, y] = new Rgba32((byte)((x * 11) + y), (byte)(y * 13), (byte)((x * y) & 255), (byte)(255 - ((x + y) % 5 * 20)));
            }
        }

        foreach (var (w, h) in new[] { (9, 7), (40, 31) })
        {
            using var expected = SurfaceResampler.Resample(source, 0, 23, 0, 19, w, h, mode);
            using var actual = SurfaceResampler.Resample(source, 0, 23, 0, 19, w, h, mode, budget);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    Assert.Equal(expected[x, y], actual[x, y]);
                }
            }
        }
    }

    /// <summary>BoxReduce matches an explicit Area resize.</summary>
    [Fact]
    public void BoxReduce_MatchesAreaResize()
    {
        using var source = new Surface(6, 4);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                source[x, y] = new Rgba32((byte)(x * 40), (byte)(y * 60), 9, 255);
            }
        }

        using var reduced = SurfaceResampler.BoxReduce(source, 2, 2);
        using var area = source.Resize(3, 2, ResamplingMode.Area);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                Assert.Equal(area[x, y], reduced[x, y]);
            }
        }
    }

    /// <summary>
    ///     Resampling is deterministic across the pooled scratch buffers: a call repeated after
    ///     differently sized calls (which leave stale data in recycled buffers) is identical to the first.
    /// </summary>
    [Theory]
    [InlineData(ResamplingMode.Area)]
    [InlineData(ResamplingMode.Bicubic)]
    [InlineData(ResamplingMode.Lanczos3)]
    public void Resample_RepeatedAfterDifferentSizes_IsIdenticalDespiteRecycledBuffers(ResamplingMode mode)
    {
        using var source = new Surface(37, 29);
        for (var y = 0; y < 29; y++)
        {
            for (var x = 0; x < 37; x++)
            {
                source[x, y] = new Rgba32((byte)((x * 7) + y), (byte)(y * 9), (byte)((x * y) & 255), (byte)(255 - ((x + y) % 6 * 15)));
            }
        }

        using var baseline = SurfaceResampler.Resample(source, 0, 37, 0, 29, 11, 8, mode);

        // Churn the pool: a much larger intermediate, then a mid-sized one
        using (var large = SurfaceResampler.Resample(source, 0, 37, 0, 29, 150, 120, mode))
        using (var mid = SurfaceResampler.Resample(source, 0, 37, 0, 29, 25, 40, mode))
        {
            Assert.Equal(150, large.Width);
            Assert.Equal(25, mid.Width);
        }

        using var repeated = SurfaceResampler.Resample(source, 0, 37, 0, 29, 11, 8, mode);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 11; x++)
            {
                Assert.Equal(baseline[x, y], repeated[x, y]);
            }
        }
    }

    /// <summary>
    ///     An extreme pixel scale (a crafted placement where one source pixel covers ~1e-9 device
    ///     pixels) must complete with work bounded by the source size and give a sane result:
    ///     the output is the (uniform) source color for every filter.
    /// </summary>
    [Theory]
    [InlineData(ResamplingMode.Area)]
    [InlineData(ResamplingMode.Bilinear)]
    [InlineData(ResamplingMode.Bicubic)]
    [InlineData(ResamplingMode.Lanczos3)]
    public void Resample_ExtremeReduction_CompletesWithSaneResult(ResamplingMode mode)
    {
        using var source = new Surface(5, 4);
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                source[x, y] = new Rgba32(90, 150, 210, 255);
            }
        }

        using var result = SurfaceResampler.Resample(source, 0, 1e9, 0, 1e9, 2, 2, mode);

        Assert.Equal(2, result.Width);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                var p = result[x, y];
                Assert.InRange(p.R, 89, 91);
                Assert.InRange(p.G, 149, 151);
                Assert.InRange(p.B, 209, 211);
            }
        }
    }

    /// <summary>A tiny image placement via <c>TryResampleToDevice</c> completes with a sane window.</summary>
    [Fact]
    public void TryResampleToDevice_TinyScale_CompletesWithSaneWindow()
    {
        using var source = new Surface(8, 8);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                source[x, y] = new Rgba32(200, 100, 50, 255);
            }
        }

        var m = new Matrix3x2(1e-9f, 0, 0, 1e-9f, 0.2f, 0.2f);
        Assert.True(SurfaceResampler.TryResampleToDevice(source, m, 10, 10, ResamplingMode.Lanczos3, out var window, out _, out _));
        window?.Dispose();
    }

    /// <summary>
    ///     A huge source footprint (tiny placement) averages the whole image for every filter,
    ///     never selecting an edge pixel or overflowing tap arithmetic.
    /// </summary>
    [Theory]
    [InlineData(ResamplingMode.Auto, 1e-19f)]
    [InlineData(ResamplingMode.Area, 1e-19f)]
    [InlineData(ResamplingMode.Bilinear, 1e-19f)]
    [InlineData(ResamplingMode.Bicubic, 1e-19f)]
    [InlineData(ResamplingMode.Lanczos3, 1e-19f)]
    [InlineData(ResamplingMode.Bilinear, 1e-12f)]
    [InlineData(ResamplingMode.Bicubic, 1e-12f)]
    [InlineData(ResamplingMode.Lanczos3, 1e-12f)]
    [InlineData(ResamplingMode.Bilinear, 1e-6f)]
    [InlineData(ResamplingMode.Bicubic, 1e-6f)]
    [InlineData(ResamplingMode.Lanczos3, 1e-6f)]
    public void TryResampleToDevice_HugeFootprintTwoTone_YieldsMean(ResamplingMode mode, float scale)
    {
        // Left half black, right half R=200 (mean 100), 8192 wide
        using var source = new Surface(8192, 2);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 8192; x++)
            {
                source[x, y] = new Rgba32((byte)(x < 4096 ? 0 : 200), 0, 0, 255);
            }
        }

        // Only x is tiny (y stays 1:1) so the 2-row image still covers device pixel centers in y
        var m = new Matrix3x2(scale, 0, 0, 1f, 0.5f, 0f);
        Assert.True(SurfaceResampler.TryResampleToDevice(source, m, 4, 4, mode, out var window, out var ox, out var oy));
        Assert.NotNull(window);
        using (window)
        {
            Assert.Equal(0, ox);
            Assert.Equal(0, oy);
            Assert.Equal(1, window.Width);
            Assert.Equal(2, window.Height);
            Assert.InRange(window[0, 0].R, 98, 102);
            Assert.Equal(255, window[0, 0].A);
        }
    }

    /// <summary>Less extreme tiny scales on a constant image keep the constant color for every filter.</summary>
    [Theory]
    [InlineData(ResamplingMode.Bilinear, 1e-4f)]
    [InlineData(ResamplingMode.Bicubic, 1e-4f)]
    [InlineData(ResamplingMode.Lanczos3, 1e-4f)]
    [InlineData(ResamplingMode.Bilinear, 1e-5f)]
    [InlineData(ResamplingMode.Bicubic, 1e-5f)]
    [InlineData(ResamplingMode.Lanczos3, 1e-5f)]
    [InlineData(ResamplingMode.Area, 1e-5f)]
    public void TryResampleToDevice_LargeFootprintConstantImage_StaysConstant(ResamplingMode mode, float scale)
    {
        using var source = new Surface(8192, 2);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 8192; x++)
            {
                source[x, y] = new Rgba32(40, 80, 120, 255);
            }
        }

        var m = new Matrix3x2(scale, 0, 0, scale, 0.5f, 0.5f);
        Assert.True(SurfaceResampler.TryResampleToDevice(source, m, 4, 4, mode, out var window, out _, out _));
        Assert.NotNull(window);
        using (window)
        {
            Assert.Equal(new Rgba32(40, 80, 120, 255), window[0, 0]);
        }
    }

    /// <summary>Folding long out-of-image tails keeps results identical to a direct sum for normal scales.</summary>
    [Fact]
    public void Resample_NormalScaleEdgeFolding_KeepsConstantImageConstant()
    {
        using var source = new Surface(6, 6);
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                source[x, y] = new Rgba32(40, 80, 120, 255);
            }
        }

        // Magnified view whose Lanczos support extends beyond both edges
        using var result = SurfaceResampler.Resample(source, -3, 12, -3, 12, 30, 30, ResamplingMode.Lanczos3);
        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x < 30; x++)
            {
                Assert.Equal(new Rgba32(40, 80, 120, 255), result[x, y]);
            }
        }
    }

    /// <summary>Wrapped bilinear sampling wraps across the tile edge.</summary>
    [Fact]
    public void SampleBilinearWrapped_AtEdge_BlendsWithOppositeSide()
    {
        using var source = new Surface(2, 1);
        source[0, 0] = new Rgba32(0, 0, 0, 255);
        source[1, 0] = new Rgba32(200, 200, 200, 255);
        var p = SurfaceResampler.SampleBilinearWrapped(source, 0.0, 0.5);
        Assert.InRange(p.R, 99, 101);
    }
}
