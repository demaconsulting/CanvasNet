namespace DemaConsulting.CanvasNet.Codecs;

public static partial class PngCodec
{
    /// <summary>
    ///     Reconstructs one filtered PNG scanline into its raw (unfiltered) pixel bytes.
    /// </summary>
    /// <param name="filterType">The scanline's filter-type byte (0-4).</param>
    /// <param name="filtered">The filtered scanline bytes, as read from the file.</param>
    /// <param name="previousRow">
    ///     The previous scanline's already-reconstructed raw bytes, or all zeros for the first row.
    /// </param>
    /// <param name="output">The buffer to receive this scanline's reconstructed raw bytes.</param>
    /// <param name="bpp">The number of bytes per pixel (3 for RGB, 4 for RGBA at 8-bit depth).</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when <paramref name="filterType"/> is not a value from 0 to 4.
    /// </exception>
    private static void DefilterRow(
        byte filterType,
        ReadOnlySpan<byte> filtered,
        ReadOnlySpan<byte> previousRow,
        Span<byte> output,
        int bpp)
    {
        switch (filterType)
        {
            case 0: // None
                filtered.CopyTo(output);
                break;

            case 1: // Sub
                DefilterSub(filtered, output, bpp);
                break;

            case 2: // Up
                DefilterUp(filtered, previousRow, output);
                break;

            case 3: // Average
                DefilterAverage(filtered, previousRow, output, bpp);
                break;

            case 4: // Paeth
                DefilterPaeth(filtered, previousRow, output, bpp);
                break;

            default:
                throw new InvalidDataException(
                    $"Unsupported PNG filter type {filterType}; only filter types 0-4 are supported.");
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 1 (Sub).</summary>
    private static void DefilterSub(ReadOnlySpan<byte> filtered, Span<byte> output, int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            output[i] = (byte)(filtered[i] + left);
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 2 (Up).</summary>
    private static void DefilterUp(ReadOnlySpan<byte> filtered, ReadOnlySpan<byte> previousRow, Span<byte> output)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            output[i] = (byte)(filtered[i] + previousRow[i]);
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 3 (Average).</summary>
    private static void DefilterAverage(
        ReadOnlySpan<byte> filtered,
        ReadOnlySpan<byte> previousRow,
        Span<byte> output,
        int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            int up = previousRow[i];
            output[i] = (byte)(filtered[i] + (left + up) / 2);
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 4 (Paeth).</summary>
    private static void DefilterPaeth(
        ReadOnlySpan<byte> filtered,
        ReadOnlySpan<byte> previousRow,
        Span<byte> output,
        int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            int up = previousRow[i];
            int upperLeft = i >= bpp ? previousRow[i - bpp] : 0;
            output[i] = (byte)(filtered[i] + PaethPredictor(left, up, upperLeft));
        }
    }

    /// <summary>
    ///     Computes the PNG Paeth predictor value for a pixel from its left, upper, and
    ///     upper-left neighbor byte values.
    /// </summary>
    /// <param name="a">The byte immediately to the left of the current byte (0 if none).</param>
    /// <param name="b">The byte immediately above the current byte (0 if none).</param>
    /// <param name="c">The byte diagonally above-left of the current byte (0 if none).</param>
    /// <returns>Whichever of <paramref name="a"/>, <paramref name="b"/>, or <paramref name="c"/> is the closest predictor.</returns>
    private static int PaethPredictor(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

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
