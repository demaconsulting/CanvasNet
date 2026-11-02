// cspell:ignore exif transversed
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for ApplyExifOrientation of the Surface class (part of the SurfaceTransforms unit).
/// </summary>
public class SurfaceExifOrientationTests
{
    /// <summary>
    ///     Creates the 3x2 image whose pixels are numbered <c>a</c> (0) to <c>f</c> (5) in reading order.
    /// </summary>
    /// <returns>A new surface.</returns>
    private static Surface CreateNumbered()
    {
        var surface = new Surface(3, 2);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                surface[x, y] = new Rgba32((byte)(((y * 3) + x) * 10), (byte)(100 + (y * 3) + x), 7, (byte)(200 + (y * 3) + x));
            }
        }

        return surface;
    }

    /// <summary>
    ///     Renders a surface as the letters of its numbered pixels, one line per row, separated by slashes.
    /// </summary>
    /// <param name="surface">The surface made of the numbered pixels.</param>
    /// <returns>The text, for example <c>abc/def</c>.</returns>
    private static string Letters(Surface surface)
    {
        var rows = new List<string>();
        for (var y = 0; y < surface.Height; y++)
        {
            var letters = new char[surface.Width];
            for (var x = 0; x < surface.Width; x++)
            {
                letters[x] = (char)('a' + (surface[x, y].R / 10));
            }

            rows.Add(new string(letters));
        }

        return string.Join('/', rows);
    }

    /// <summary>
    ///     Test that each orientation turns the numbered image into the expected upright image.
    /// </summary>
    /// <param name="orientation">The orientation to apply.</param>
    /// <param name="expected">The expected letters, row by row.</param>
    [Theory]
    [InlineData(1, "abc/def")]
    [InlineData(2, "cba/fed")]
    [InlineData(3, "fed/cba")]
    [InlineData(4, "def/abc")]
    [InlineData(5, "ad/be/cf")]
    [InlineData(6, "da/eb/fc")]
    [InlineData(7, "fc/eb/da")]
    [InlineData(8, "cf/be/ad")]
    public void Surface_ApplyExifOrientation_Orientation_MapsPixels(int orientation, string expected)
    {
        // Arrange: the numbered 3x2 image
        using var source = CreateNumbered();

        // Act: apply the orientation
        using var result = source.ApplyExifOrientation(orientation);

        // Assert: the pixels are where the EXIF specification puts them, and the source is unchanged
        Assert.Equal(expected, Letters(result));
        Assert.Equal("abc/def", Letters(source));
    }

    /// <summary>
    ///     Test that the moved pixels are exact copies, alpha included, and that orientation 1 is an independent copy.
    /// </summary>
    [Fact]
    public void Surface_ApplyExifOrientation_Pixels_AreExactCopiesIncludingAlpha()
    {
        // Arrange: a numbered image whose pixels have different alpha
        using var source = CreateNumbered();

        // Act
        using var upright = source.ApplyExifOrientation(1);
        using var turned = source.ApplyExifOrientation(6);

        // Assert: orientation 1 equals the source but is another surface; 6 moves (x, y) to (Height - 1 - y, x)
        Assert.NotSame(source, upright);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                Assert.Equal(source[x, y], upright[x, y]);
                Assert.Equal(source[x, y], turned[source.Height - 1 - y, x]);
            }
        }

        upright[0, 0] = default;
        Assert.NotEqual(default, source[0, 0]);
    }

    /// <summary>
    ///     Test that an orientation outside 1 to 8 is rejected and that a disposed surface is refused.
    /// </summary>
    [Fact]
    public void Surface_ApplyExifOrientation_InvalidArguments_Throw()
    {
        // Arrange: a surface
        var source = CreateNumbered();

        // Act / Assert: the range checks, then the lifetime check
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ApplyExifOrientation(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ApplyExifOrientation(9));
        source.Dispose();
        Assert.Throws<ObjectDisposedException>(() => source.ApplyExifOrientation(1));
    }
}
