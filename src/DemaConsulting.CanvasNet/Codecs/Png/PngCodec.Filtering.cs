namespace DemaConsulting.CanvasNet.Codecs;

public static partial class PngCodec
{
    /// <summary>
    ///     Converts one row of RGBA surface bytes into raw PNG pixel bytes (RGB or RGBA, 8-bit
    ///     depth), dropping alpha entirely when writing RGB.
    /// </summary>
    /// <param name="source">The surface row, as RGBA bytes (4 bytes per pixel).</param>
    /// <param name="destination">The buffer to fill (<paramref name="channels"/> bytes per pixel).</param>
    /// <param name="width">The number of pixels in the row.</param>
    /// <param name="channels">The number of bytes per pixel to write to <paramref name="destination"/> (3 or 4).</param>
    private static void PackRow(ReadOnlySpan<byte> source, Span<byte> destination, int width, int channels)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceOffset = x * 4;
            var destinationOffset = x * channels;
            destination[destinationOffset] = source[sourceOffset];
            destination[destinationOffset + 1] = source[sourceOffset + 1];
            destination[destinationOffset + 2] = source[sourceOffset + 2];
            if (channels == 4)
            {
                destination[destinationOffset + 3] = source[sourceOffset + 3];
            }
        }
    }
}
