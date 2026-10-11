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
}
