using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Round-trip tests for the documented load, crop, resize, save workflow using the BMP and PNG codecs.
/// </summary>
public class SurfaceResizeCodecRoundTripTests
{
    /// <summary>Builds a 40x30 opaque gradient.</summary>
    private static Surface Gradient()
    {
        var surface = new Surface(40, 30);
        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                surface[x, y] = new Rgba32((byte)(x * 6), (byte)(y * 8), 100, 255);
            }
        }

        return surface;
    }

    /// <summary>Asserts the cropped-then-resized result matches the expected size and gradient direction.</summary>
    private static void AssertResized(Surface reloaded)
    {
        Assert.Equal(20, reloaded.Width);
        Assert.Equal(10, reloaded.Height);
        Assert.True(reloaded[19, 5].R > reloaded[0, 5].R);
        Assert.True(reloaded[10, 9].G > reloaded[10, 0].G);
    }

    /// <summary>BMP load, crop, resize, save and reload keeps the resized content.</summary>
    [Fact]
    public void Resize_BmpCodec_RoundTrip()
    {
        using var original = Gradient();
        using var stream = new MemoryStream();
        BmpCodec.Save(original, stream);
        stream.Position = 0;

        using var loaded = BmpCodec.Load(stream);
        using var cropped = loaded.Crop(5, 5, 30, 20);
        using var resized = cropped.Resize(20, 10);
        using var output = new MemoryStream();
        BmpCodec.Save(resized, output);
        output.Position = 0;

        using var reloaded = BmpCodec.Load(output);
        AssertResized(reloaded);
    }

    /// <summary>PNG load, crop, resize, save and reload keeps the resized content.</summary>
    [Fact]
    public void Resize_PngCodec_RoundTrip()
    {
        using var original = Gradient();
        using var stream = new MemoryStream();
        PngCodec.Save(original, stream);
        stream.Position = 0;

        using var loaded = PngCodec.Load(stream);
        using var cropped = loaded.Crop(5, 5, 30, 20);
        using var fitted = cropped.ResizeToFit(20, 20, ResamplingMode.Lanczos3);
        Assert.Equal(20, fitted.Width);
        using var resized = cropped.Resize(20, 10, ResamplingMode.Lanczos3);
        using var output = new MemoryStream();
        PngCodec.Save(resized, output);
        output.Position = 0;

        using var reloaded = PngCodec.Load(output);
        AssertResized(reloaded);
        Assert.Equal(resized[7, 3], reloaded[7, 3]);
    }
}
