using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Canvas;

/// <summary>
///     Unit tests for <see cref="Surface.Clone"/> and <see cref="Surface.CopyFrom"/>.
/// </summary>
public class SurfaceCopyTests
{
    /// <summary>
    ///     Creates a surface with a distinct, opaque pixel at every coordinate.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The surface.</returns>
    private static Surface CreatePattern(int width, int height)
    {
        var surface = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                surface[x, y] = new Rgba32((byte)(x + 1), (byte)(y + 1), 7, 200);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Proves that Clone returns a surface of the same size with identical pixels.
    /// </summary>
    [Fact]
    public void Surface_Clone_ValidSurface_ReturnsIdenticalPixels()
    {
        // Arrange: a surface with a distinct pixel everywhere
        using var source = CreatePattern(5, 3);

        // Act
        using var clone = source.Clone();

        // Assert: same size, same pixels, different instance
        Assert.NotSame(source, clone);
        Assert.Equal(5, clone.Width);
        Assert.Equal(3, clone.Height);
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.Equal(source[x, y], clone[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that the buffers of a surface and its clone are independent.
    /// </summary>
    [Fact]
    public void Surface_Clone_ModifyEither_DoesNotAffectTheOther()
    {
        // Arrange
        using var source = CreatePattern(2, 2);
        using var clone = source.Clone();
        var original = source[0, 0];

        // Act: change the clone, then the source at another pixel
        clone[0, 0] = new Rgba32(9, 9, 9, 9);
        source[1, 1] = new Rgba32(8, 8, 8, 8);

        // Assert
        Assert.Equal(original, source[0, 0]);
        Assert.NotEqual(new Rgba32(8, 8, 8, 8), clone[1, 1]);
    }

    /// <summary>
    ///     Proves that Clone on a disposed surface throws ObjectDisposedException.
    /// </summary>
    [Fact]
    public void Surface_Clone_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var surface = new Surface(2, 2);
        surface.Dispose();

        // Act / Assert
        Assert.Throws<ObjectDisposedException>(() => surface.Clone());
    }

    /// <summary>
    ///     Proves that CopyFrom replaces the destination pixels, alpha included, without blending.
    /// </summary>
    [Fact]
    public void Surface_CopyFrom_InsideDestination_ReplacesPixelsWithoutBlending()
    {
        // Arrange: an opaque destination and a half-transparent source
        using var destination = new Surface(6, 5);
        destination.Clear(new Rgba32(10, 20, 30, 255));
        using var source = new Surface(2, 2);
        source.Clear(new Rgba32(200, 100, 50, 128));

        // Act
        destination.CopyFrom(source, 3, 2);

        // Assert: the four target pixels are the source value (not a blend), the rest untouched
        var untouched = new Rgba32(10, 20, 30, 255);
        Assert.Equal(new Rgba32(200, 100, 50, 128), destination[3, 2]);
        Assert.Equal(new Rgba32(200, 100, 50, 128), destination[4, 3]);
        Assert.Equal(untouched, destination[2, 2]);
        Assert.Equal(untouched, destination[5, 4]);
        Assert.Equal(untouched, destination[3, 1]);
    }

    /// <summary>
    ///     Proves that CopyFrom ignores the parts of the source outside the destination on every side.
    /// </summary>
    [Theory]
    [InlineData(-2, -1)]
    [InlineData(3, 2)]
    [InlineData(-2, 2)]
    [InlineData(3, -1)]
    public void Surface_CopyFrom_PartlyOutside_ClipsToDestination(int destX, int destY)
    {
        // Arrange: a 4x3 destination and a 4x3 source overlapping it partly
        using var destination = new Surface(4, 3);
        using var source = CreatePattern(4, 3);

        // Act
        destination.CopyFrom(source, destX, destY);

        // Assert: every destination pixel inside the overlap equals the matching source pixel; all others stay clear
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                var sx = x - destX;
                var sy = y - destY;
                var inside = sx >= 0 && sx < 4 && sy >= 0 && sy < 3;
                Assert.Equal(inside ? source[sx, sy] : default, destination[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that a source entirely outside the destination, or at extreme positions, changes nothing.
    /// </summary>
    [Theory]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    [InlineData(-3, 0)]
    [InlineData(0, -3)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(int.MinValue, int.MinValue)]
    public void Surface_CopyFrom_EntirelyOutside_LeavesDestinationUnchanged(int destX, int destY)
    {
        // Arrange
        using var destination = new Surface(4, 3);
        using var source = CreatePattern(3, 3);

        // Act
        destination.CopyFrom(source, destX, destY);

        // Assert
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                Assert.Equal(default, destination[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that CopyFrom does not modify the source.
    /// </summary>
    [Fact]
    public void Surface_CopyFrom_Source_IsNotModified()
    {
        // Arrange
        using var destination = new Surface(3, 3);
        using var source = CreatePattern(2, 2);
        using var expected = source.Clone();

        // Act
        destination.CopyFrom(source, 1, 1);

        // Assert
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                Assert.Equal(expected[x, y], source[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that CopyFrom rejects a null source, the surface itself, and disposed surfaces.
    /// </summary>
    [Fact]
    public void Surface_CopyFrom_InvalidArguments_Throw()
    {
        // Arrange
        var destination = new Surface(2, 2);
        var source = new Surface(2, 2);
        var disposed = new Surface(2, 2);
        disposed.Dispose();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => destination.CopyFrom(null!, 0, 0));
        Assert.Throws<ArgumentException>(() => destination.CopyFrom(destination, 0, 0));
        Assert.Throws<ObjectDisposedException>(() => destination.CopyFrom(disposed, 0, 0));
        Assert.Throws<ObjectDisposedException>(() => disposed.CopyFrom(source, 0, 0));
    }
}
