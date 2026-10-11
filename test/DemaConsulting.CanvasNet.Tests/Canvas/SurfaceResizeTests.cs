using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for <see cref="Surface.Resize"/>, <see cref="Surface.ResizeToFit"/> and
///     <see cref="ResamplingMode"/>. Timing is deliberately never asserted.
/// </summary>
public class SurfaceResizeTests
{
    private static readonly ResamplingMode[] ConcreteModes =
    [
        ResamplingMode.Nearest,
        ResamplingMode.Bilinear,
        ResamplingMode.Bicubic,
        ResamplingMode.Lanczos3,
        ResamplingMode.Area,
    ];

    /// <summary>Builds an opaque gray row surface from the given levels.</summary>
    private static Surface GrayRow(params byte[] levels)
    {
        var surface = new Surface(levels.Length, 1);
        for (var x = 0; x < levels.Length; x++)
        {
            surface[x, 0] = new Rgba32(levels[x], levels[x], levels[x], 255);
        }

        return surface;
    }

    /// <summary>Returns the red channel of every pixel in row 0.</summary>
    private static int[] Reds(Surface surface) =>
        [.. Enumerable.Range(0, surface.Width).Select(x => (int)surface[x, 0].R)];

    /// <summary>Nearest enlarges by replicating pixels.</summary>
    [Fact]
    public void Resize_Nearest_Upscale_ReplicatesPixels()
    {
        using var source = GrayRow(0, 255);
        using var result = source.Resize(4, 1, ResamplingMode.Nearest);
        Assert.Equal([0, 0, 255, 255], Reds(result));
    }

    /// <summary>Bilinear enlarges by linear interpolation with clamped edges.</summary>
    [Fact]
    public void Resize_Bilinear_Upscale_InterpolatesLinearly()
    {
        using var source = GrayRow(0, 100);
        using var result = source.Resize(4, 1, ResamplingMode.Bilinear);
        var reds = Reds(result);
        Assert.InRange(reds[0], 0, 1);
        Assert.InRange(reds[1], 24, 26);
        Assert.InRange(reds[2], 74, 76);
        Assert.InRange(reds[3], 99, 100);
    }

    /// <summary>Area averages each covered source pixel exactly on an integer reduction.</summary>
    [Fact]
    public void Resize_Area_IntegerReduction_AveragesBlocks()
    {
        using var source = GrayRow(0, 100, 200, 40);
        using var result = source.Resize(2, 1, ResamplingMode.Area);
        var reds = Reds(result);
        Assert.InRange(reds[0], 49, 51);
        Assert.InRange(reds[1], 119, 121);
    }

    /// <summary>Bicubic and Lanczos3 keep the flat regions of a step edge at their exact values and never leave the byte range (overshoot is clamped).</summary>
    [Theory]
    [InlineData(ResamplingMode.Bicubic)]
    [InlineData(ResamplingMode.Lanczos3)]
    public void Resize_SharpFilters_StepEdge_ClampsToByteRange(ResamplingMode mode)
    {
        using var source = GrayRow(0, 0, 0, 255, 255, 255);
        using var result = source.Resize(24, 1, mode);
        var reds = Reds(result);
        Assert.Equal(0, reds[0]);
        Assert.Equal(255, reds[^1]);
        Assert.All(reds, v => Assert.InRange(v, 0, 255));
    }

    /// <summary>Every filter returns an exact, independent copy when the size does not change.</summary>
    [Theory]
    [InlineData(ResamplingMode.Auto)]
    [InlineData(ResamplingMode.Nearest)]
    [InlineData(ResamplingMode.Bilinear)]
    [InlineData(ResamplingMode.Bicubic)]
    [InlineData(ResamplingMode.Lanczos3)]
    [InlineData(ResamplingMode.Area)]
    public void Resize_SameSize_IsExactIndependentCopy(ResamplingMode mode)
    {
        using var source = GrayRow(1, 50, 99, 250, 7);
        source[2, 0] = new Rgba32(10, 20, 30, 128);
        using var result = source.Resize(5, 1, mode);
        Assert.NotSame(source, result);
        for (var x = 0; x < 5; x++)
        {
            Assert.Equal(source[x, 0], result[x, 0]);
        }

        result[0, 0] = new Rgba32(9, 9, 9, 9);
        Assert.Equal(new Rgba32(1, 1, 1, 255), source[0, 0]);
    }

    /// <summary>A constant opaque image stays constant for every filter and ratio.</summary>
    [Theory]
    [InlineData(ResamplingMode.Auto, 7, 5)]
    [InlineData(ResamplingMode.Nearest, 33, 3)]
    [InlineData(ResamplingMode.Bilinear, 3, 40)]
    [InlineData(ResamplingMode.Bicubic, 90, 90)]
    [InlineData(ResamplingMode.Lanczos3, 13, 2)]
    [InlineData(ResamplingMode.Area, 5, 61)]
    public void Resize_ConstantImage_StaysConstant(ResamplingMode mode, int width, int height)
    {
        using var source = new Surface(20, 17);
        source.Clear(new Rgba32(77, 130, 201, 255));
        using var result = source.Resize(width, height, mode);
        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var p = result[x, y];
                Assert.InRange(p.R, 76, 78);
                Assert.InRange(p.G, 129, 131);
                Assert.InRange(p.B, 200, 202);
                Assert.Equal(255, p.A);
            }
        }
    }

    /// <summary>A one-pixel line is never lost at many reduction ratios, and Area conserves its ink.</summary>
    [Theory]
    [InlineData(ResamplingMode.Auto)]
    [InlineData(ResamplingMode.Bilinear)]
    [InlineData(ResamplingMode.Bicubic)]
    [InlineData(ResamplingMode.Lanczos3)]
    [InlineData(ResamplingMode.Area)]
    public void Resize_Downscale_PreservesOnePixelLine(ResamplingMode mode)
    {
        const int sourceWidth = 1000;
        foreach (var width in new[] { 999, 800, 765, 500, 333, 250, 100, 37, 10 })
        {
            using var source = new Surface(sourceWidth, 2);
            source.Clear(new Rgba32(255, 255, 255, 255));
            for (var y = 0; y < 2; y++)
            {
                source[417, y] = new Rgba32(0, 0, 0, 255);
            }

            using var result = source.Resize(width, 2, mode);
            double ink = 0;
            for (var x = 0; x < width; x++)
            {
                ink += 255 - result[x, 0].R;
            }

            Assert.True(ink > 0, $"line lost at width {width} with {mode}");
            if (mode == ResamplingMode.Area)
            {
                var expected = 255.0 * width / sourceWidth;
                Assert.InRange(ink, expected * 0.9, expected * 1.1 + 1);
            }
        }
    }

    /// <summary>An enlargement with a smooth filter has no blocky jumps between neighbors.</summary>
    [Theory]
    [InlineData(ResamplingMode.Auto)]
    [InlineData(ResamplingMode.Bilinear)]
    [InlineData(ResamplingMode.Bicubic)]
    public void Resize_Upscale_IsSmooth(ResamplingMode mode)
    {
        using var source = GrayRow(0, 80, 160, 240);
        using var result = source.Resize(400, 1, mode);
        var reds = Reds(result);
        for (var x = 1; x < reds.Length; x++)
        {
            Assert.InRange(Math.Abs(reds[x] - reds[x - 1]), 0, 3);
        }
    }

    /// <summary>Transparent pixels contribute no color, so there is no dark fringe at an alpha edge.</summary>
    [Theory]
    [InlineData(ResamplingMode.Bilinear)]
    [InlineData(ResamplingMode.Bicubic)]
    [InlineData(ResamplingMode.Area)]
    public void Resize_AlphaEdge_HasNoColorFringe(ResamplingMode mode)
    {
        using var source = new Surface(4, 1);
        source[0, 0] = new Rgba32(255, 0, 0, 255);
        source[1, 0] = new Rgba32(255, 0, 0, 255);
        source[2, 0] = new Rgba32(0, 0, 0, 0);
        source[3, 0] = new Rgba32(0, 0, 0, 0);
        using var result = source.Resize(9, 1, mode);
        for (var x = 0; x < 9; x++)
        {
            var p = result[x, 0];
            if (p.A > 0)
            {
                Assert.InRange(p.R, 250, 255);
                Assert.InRange(p.G, 0, 5);
                Assert.InRange(p.B, 0, 5);
            }
        }
    }

    /// <summary>Auto picks Area when shrinking one axis and Bicubic when enlarging the other.</summary>
    [Fact]
    public void Resize_Auto_ChoosesPerAxis()
    {
        using var source = new Surface(4, 2);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                source[x, y] = new Rgba32((byte)(x * 60), (byte)(y * 100), (byte)(x * 60), 255);
            }
        }

        using var auto = source.Resize(2, 4);
        using var explicitArea = source.Resize(2, 2, ResamplingMode.Area);
        Assert.Equal(2, auto.Width);
        Assert.Equal(4, auto.Height);

        // The enlarged (y) axis interpolates the two source rows smoothly: strictly between them, increasing
        Assert.True(auto[0, 0].G < auto[0, 1].G && auto[0, 1].G < auto[0, 2].G && auto[0, 2].G < auto[0, 3].G);
        Assert.InRange(auto[0, 0].G, 0, 40);
        Assert.InRange(auto[0, 3].G, 60, 120);

        // The shrunken axis averages pairs: (0+60)/2 and (120+180)/2
        Assert.InRange(auto[0, 1].R, 29, 31);
        Assert.InRange(auto[1, 1].R, 149, 151);
        using var autoSame = source.Resize(2, 2);
        Assert.Equal(explicitArea[0, 0], autoSame[0, 0]);
    }

    /// <summary>Resizing an extremely wide 8192x1 image to 1x8192 yields the expected shape and content.</summary>
    [Fact]
    public void Resize_ExtremeAspectChange_ProducesExpectedShape()
    {
        using var wide = new Surface(8192, 1);
        wide.Clear(new Rgba32(10, 20, 30, 255));
        using var tall = wide.Resize(1, 8192);
        Assert.Equal(1, tall.Width);
        Assert.Equal(8192, tall.Height);
        Assert.InRange(tall[0, 4000].R, 9, 11);
        Assert.InRange(tall[0, 4000].B, 29, 31);
    }

    /// <summary>Invalid sizes report the offending parameter.</summary>
    [Theory]
    [InlineData(0, 1, "width")]
    [InlineData(-3, 1, "width")]
    [InlineData(Surface.MaxDimension + 1, 1, "width")]
    [InlineData(1, 0, "height")]
    [InlineData(1, -1, "height")]
    [InlineData(1, Surface.MaxDimension + 1, "height")]
    public void Resize_InvalidDimensions_Throws(int width, int height, string parameter)
    {
        using var source = new Surface(4, 4);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => source.Resize(width, height));
        Assert.Equal(parameter, ex.ParamName);
    }

    /// <summary>An undefined mode is rejected for both entry points.</summary>
    [Fact]
    public void Resize_UndefinedMode_Throws()
    {
        using var source = new Surface(4, 4);
        Assert.Equal("mode", Assert.Throws<ArgumentOutOfRangeException>(() => source.Resize(2, 2, (ResamplingMode)99)).ParamName);
        Assert.Equal("mode", Assert.Throws<ArgumentOutOfRangeException>(() => source.ResizeToFit(2, 2, (ResamplingMode)(-1))).ParamName);
    }

    /// <summary>A disposed surface cannot be resized.</summary>
    [Fact]
    public void Resize_Disposed_Throws()
    {
        var source = new Surface(4, 4);
        source.Dispose();
        Assert.Throws<ObjectDisposedException>(() => source.Resize(2, 2));
        Assert.Throws<ObjectDisposedException>(() => source.ResizeToFit(2, 2));
    }

    /// <summary>ResizeToFit keeps the aspect ratio, fits the box, and works up and down.</summary>
    [Theory]
    [InlineData(100, 50, 20, 20, 20, 10)]
    [InlineData(50, 100, 20, 20, 10, 20)]
    [InlineData(10, 10, 40, 30, 30, 30)]
    [InlineData(4, 2, 100, 100, 100, 50)]
    [InlineData(300, 300, 7, 7, 7, 7)]
    public void ResizeToFit_KeepsAspectAndFitsBox(int w, int h, int boxW, int boxH, int expectedW, int expectedH)
    {
        using var source = new Surface(w, h);
        using var result = source.ResizeToFit(boxW, boxH);
        Assert.Equal(expectedW, result.Width);
        Assert.Equal(expectedH, result.Height);
    }

    /// <summary>Extremely elongated images clamp the short axis to one pixel.</summary>
    [Fact]
    public void ResizeToFit_VeryElongated_ClampsToOnePixel()
    {
        using var source = new Surface(8000, 2);
        using var result = source.ResizeToFit(10, 10);
        Assert.Equal(10, result.Width);
        Assert.Equal(1, result.Height);
    }

    /// <summary>ResizeToFit validates the box.</summary>
    [Theory]
    [InlineData(0, 5, "maxWidth")]
    [InlineData(Surface.MaxDimension + 1, 5, "maxWidth")]
    [InlineData(5, 0, "maxHeight")]
    [InlineData(5, Surface.MaxDimension + 1, "maxHeight")]
    public void ResizeToFit_InvalidBox_Throws(int maxW, int maxH, string parameter)
    {
        using var source = new Surface(4, 4);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => source.ResizeToFit(maxW, maxH));
        Assert.Equal(parameter, ex.ParamName);
    }

    /// <summary>Every concrete mode produces the requested size.</summary>
    [Fact]
    public void Resize_AllModes_ProduceRequestedSize()
    {
        using var source = GrayRow(0, 64, 128, 192, 255);
        foreach (var mode in ConcreteModes)
        {
            using var result = source.Resize(7, 3, mode);
            Assert.Equal(7, result.Width);
            Assert.Equal(3, result.Height);
        }
    }
}
