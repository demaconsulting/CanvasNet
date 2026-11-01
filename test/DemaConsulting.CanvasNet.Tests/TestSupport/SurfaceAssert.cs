using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.TestSupport;

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
}
