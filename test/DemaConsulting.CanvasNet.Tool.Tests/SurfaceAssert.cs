using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Assertions about the pixels of surfaces.
/// </summary>
internal static class SurfaceAssert
{
    /// <summary>
    ///     Asserts that two surfaces have the same size and exactly the same pixels.
    /// </summary>
    /// <param name="expected">The expected surface.</param>
    /// <param name="actual">The actual surface.</param>
    public static void Equal(Surface expected, Surface actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                if (expected[x, y] != actual[x, y])
                {
                    Assert.Fail($"Pixel ({x},{y}) is {actual[x, y]}, expected {expected[x, y]}");
                }
            }
        }
    }

    /// <summary>
    ///     Asserts that two surfaces have the same size and that no channel of any pixel differs by more than a tolerance.
    /// </summary>
    /// <param name="expected">The expected surface.</param>
    /// <param name="actual">The actual surface.</param>
    /// <param name="tolerance">The largest accepted difference of one channel.</param>
    public static void Close(Surface expected, Surface actual, int tolerance)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var (e, a) = (expected[x, y], actual[x, y]);
                var difference = new[] { Math.Abs(e.R - a.R), Math.Abs(e.G - a.G), Math.Abs(e.B - a.B), Math.Abs(e.A - a.A) }.Max();
                if (difference > tolerance)
                {
                    Assert.Fail($"Pixel ({x},{y}) is {a}, expected {e} within {tolerance}");
                }
            }
        }
    }

    /// <summary>
    ///     Asserts that the average absolute difference over all channels of two surfaces is at most a limit.
    /// </summary>
    /// <param name="expected">The expected surface.</param>
    /// <param name="actual">The actual surface.</param>
    /// <param name="limit">The largest accepted mean difference of a channel.</param>
    public static void CloseOnAverage(Surface expected, Surface actual, double limit)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        double total = 0;
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var (e, a) = (expected[x, y], actual[x, y]);
                total += Math.Abs(e.R - a.R) + Math.Abs(e.G - a.G) + Math.Abs(e.B - a.B) + Math.Abs(e.A - a.A);
            }
        }

        var mean = total / (4.0 * expected.Width * expected.Height);
        Assert.True(mean <= limit, $"The mean channel difference is {mean:0.00}, expected at most {limit}");
    }

    /// <summary>
    ///     Loads an image file with the codec that matches its extension.
    /// </summary>
    /// <param name="path">The path of a PNG, JPEG, BMP, or TIFF file.</param>
    /// <returns>The decoded surface.</returns>
    public static Surface Load(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => DemaConsulting.CanvasNet.Codecs.PngCodec.Load(path),
            ".jpg" or ".jpeg" => DemaConsulting.CanvasNet.Codecs.JpegCodec.Load(path),
            ".bmp" => DemaConsulting.CanvasNet.Codecs.BmpCodec.Load(path),
            ".tif" or ".tiff" => DemaConsulting.CanvasNet.Codecs.TiffCodec.Load(path),
            _ => throw new ArgumentException($"No codec for '{path}'", nameof(path))
        };
    }
}
