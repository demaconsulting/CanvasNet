namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Internal PNG scanline-filter reversal (the five filter types of the PNG specification, section 9)
///     shared by <see cref="PngCodec"/> and, through the <c>InternalsVisibleTo</c> grant, by the
///     <c>DemaConsulting.CanvasNet.Pdf</c> package's PNG-predictor (<c>/Predictor 10</c>-<c>15</c>)
///     reversal, which uses exactly the same byte-level algorithm.
/// </summary>
/// <remarks>
///     This type is <c>internal</c>: it is not part of the public API surface. It is stateless and
///     therefore thread-safe.
/// </remarks>
internal static class PngFilter
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
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="filterType"/> is not a value from 0 to 4.
    /// </exception>
    internal static void DefilterRow(
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
}
