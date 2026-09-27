using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class PngCodec
{
    /// <summary>
    ///     Defilters and decodes every scanline of decompressed PNG raw data into a new
    ///     <see cref="Surface"/>, reconstructing each row from the previous row per the PNG
    ///     filtering specification, then mapping each row's samples to RGBA per
    ///     <paramref name="header"/>'s color type.
    /// </summary>
    /// <param name="rawData">The decompressed, filtered scanline bytes (one filter-type byte plus <paramref name="rowBytes"/> per row).</param>
    /// <param name="header">The parsed IHDR fields (width, height, color type, and bit depth).</param>
    /// <param name="rowBytes">The number of packed pixel bytes per row (excluding the filter-type byte).</param>
    /// <param name="bpp">The number of whole bytes per pixel, used by the defilter algorithms (at least 1).</param>
    /// <param name="samplesPerPixel">The number of samples per pixel for <paramref name="header"/>'s color type.</param>
    /// <param name="palette">The raw PLTE chunk data (RGB triples), or null if absent.</param>
    /// <param name="trns">The validated tRNS chunk data for this color type, or null if absent/not applicable.</param>
    private static Surface DecodeScanlines(
        byte[] rawData,
        PngHeader header,
        int rowBytes,
        int bpp,
        int samplesPerPixel,
        byte[]? palette,
        byte[]? trns)
    {
        var (width, height, colorType, bitDepth, _) = header;
        var surface = new Surface(width, height);
        var previousRow = new byte[rowBytes];
        var currentRow = new byte[rowBytes];
        var samples = new int[width * samplesPerPixel];
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            var filterType = rawData[offset];
            offset++;
            var filtered = rawData.AsSpan(offset, rowBytes);
            offset += rowBytes;

            DefilterRow(filterType, filtered, previousRow, currentRow, bpp);
            ExtractSamples(currentRow, width, bitDepth, samplesPerPixel, samples);
            MapSamplesToRgba(samples, width, colorType, bitDepth, palette, trns, surface.GetRowSpanBytes(y));

            // Swap buffers rather than copying: the just-defiltered row becomes the "previous
            // row" reference for the next iteration, and the old previous-row buffer is reused
            // (and fully overwritten) as the next iteration's output buffer
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return surface;
    }

    /// <summary>
    ///     Unpacks one defiltered PNG scanline's raw bytes into one integer sample per source
    ///     channel, handling every bit depth the PNG specification defines.
    /// </summary>
    /// <param name="row">The defiltered scanline bytes.</param>
    /// <param name="width">The number of pixels in the row.</param>
    /// <param name="bitDepth">The PNG bit depth (1, 2, 4, 8, or 16).</param>
    /// <param name="samplesPerPixel">The number of samples per pixel.</param>
    /// <param name="samples">
    ///     Receives <paramref name="width"/> * <paramref name="samplesPerPixel"/> samples. For a
    ///     16-bit depth, each sample is the raw big-endian 16-bit value (0-65535), <em>not</em>
    ///     yet downshifted - see <see cref="MapSamplesToRgba"/> for why the downshift is deferred.
    /// </param>
    private static void ExtractSamples(
        ReadOnlySpan<byte> row,
        int width,
        int bitDepth,
        int samplesPerPixel,
        Span<int> samples)
    {
        var totalSamples = width * samplesPerPixel;
        switch (bitDepth)
        {
            case 8:
                for (var i = 0; i < totalSamples; i++)
                {
                    samples[i] = row[i];
                }

                break;

            case 16:
                for (var i = 0; i < totalSamples; i++)
                {
                    samples[i] = (row[i * 2] << 8) | row[i * 2 + 1];
                }

                break;

            default: // 1, 2, or 4 - only reachable for grayscale/palette (samplesPerPixel == 1)
                var mask = (1 << bitDepth) - 1;
                for (var x = 0; x < width; x++)
                {
                    var bitPos = x * bitDepth;
                    var byteIndex = bitPos / 8;
                    var shift = 8 - bitDepth - (bitPos % 8);
                    samples[x] = (row[byteIndex] >> shift) & mask;
                }

                break;
        }
    }

    /// <summary>
    ///     Maps one row's unpacked integer samples to RGBA bytes per <paramref name="colorType"/>,
    ///     applying <c>tRNS</c> key-color transparency and the 16-bit-to-8-bit downshift where
    ///     applicable.
    /// </summary>
    /// <param name="samples">This row's samples, as produced by <see cref="ExtractSamples"/>.</param>
    /// <param name="width">The number of pixels in the row.</param>
    /// <param name="colorType">The PNG color type.</param>
    /// <param name="bitDepth">The PNG bit depth.</param>
    /// <param name="palette">The raw PLTE chunk data (RGB triples), required for palette (color type 3).</param>
    /// <param name="trns">The validated tRNS chunk data for this color type, or null if absent/not applicable.</param>
    /// <param name="destination">The surface row to fill, as RGBA bytes (4 bytes per pixel).</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when a palette index sample is outside the range of <paramref name="palette"/>.
    /// </exception>
    private static void MapSamplesToRgba(
        ReadOnlySpan<int> samples,
        int width,
        int colorType,
        int bitDepth,
        byte[]? palette,
        byte[]? trns,
        Span<byte> destination)
    {
        switch (colorType)
        {
            case ColorTypeGrayscale:
                MapGrayscaleSamples(samples, width, bitDepth, trns, destination);
                break;

            case ColorTypeTruecolor:
                MapTruecolorSamples(samples, width, bitDepth, trns, destination);
                break;

            case ColorTypePalette:
                MapPaletteSamples(samples, width, palette!, trns, destination);
                break;

            case ColorTypeGrayscaleAlpha:
                MapGrayscaleAlphaSamples(samples, width, bitDepth, destination);
                break;

            case ColorTypeTruecolorAlpha:
                MapTruecolorAlphaSamples(samples, width, bitDepth, destination);
                break;
        }
    }

    /// <summary>
    ///     Maps one row's grayscale samples (color type 0) to RGBA, resolving the single-key-color
    ///     transparency <paramref name="trns"/> chunk (if any) against the raw, not-yet-downshifted
    ///     sample value.
    /// </summary>
    /// <remarks>
    ///     Isolated from <see cref="MapSamplesToRgba"/> as its own color-type-specific mapping
    ///     step - each PNG color type maps its samples to RGBA via an independent, self-contained
    ///     per-pixel formula, so extracting one per color type keeps every formula independently
    ///     nameable and testable rather than folding all five into one large switch body.
    /// </remarks>
    private static void MapGrayscaleSamples(ReadOnlySpan<int> samples, int width, int bitDepth, byte[]? trns, Span<byte> destination)
    {
        var maxSample = (1 << bitDepth) - 1;
        var trnsGray = trns != null ? ReadUInt16Be(trns, 0) : -1;
        for (var x = 0; x < width; x++)
        {
            var raw = samples[x];
            var isTransparent = raw == trnsGray;
            var gray = bitDepth == 16 ? (byte)(raw >> 8) : (byte)(raw * 255 / maxSample);
            var d = x * 4;
            destination[d] = gray;
            destination[d + 1] = gray;
            destination[d + 2] = gray;
            destination[d + 3] = (byte)(isTransparent ? 0 : 255);
        }
    }

    /// <summary>
    ///     Maps one row's Truecolor samples (color type 2) to RGBA, resolving the single-key-color
    ///     transparency <paramref name="trns"/> chunk (if any) against the raw, not-yet-downshifted
    ///     RGB sample triple.
    /// </summary>
    /// <remarks>
    ///     See <see cref="MapGrayscaleSamples"/>'s remarks for why each color type has its own
    ///     extracted mapping method.
    /// </remarks>
    private static void MapTruecolorSamples(ReadOnlySpan<int> samples, int width, int bitDepth, byte[]? trns, Span<byte> destination)
    {
        var hasTrns = trns != null;
        var trnsR = hasTrns ? ReadUInt16Be(trns!, 0) : -1;
        var trnsG = hasTrns ? ReadUInt16Be(trns!, 2) : -1;
        var trnsB = hasTrns ? ReadUInt16Be(trns!, 4) : -1;
        for (var x = 0; x < width; x++)
        {
            var s = x * 3;
            var r = samples[s];
            var g = samples[s + 1];
            var b = samples[s + 2];
            var isTransparent = r == trnsR && g == trnsG && b == trnsB;
            var d = x * 4;
            destination[d] = bitDepth == 16 ? (byte)(r >> 8) : (byte)r;
            destination[d + 1] = bitDepth == 16 ? (byte)(g >> 8) : (byte)g;
            destination[d + 2] = bitDepth == 16 ? (byte)(b >> 8) : (byte)b;
            destination[d + 3] = (byte)(isTransparent ? 0 : 255);
        }
    }

    /// <summary>
    ///     Maps one row's palette indices (color type 3) to RGBA via <paramref name="palette"/>,
    ///     resolving each index's per-entry alpha from <paramref name="trns"/> (if any).
    /// </summary>
    /// <remarks>
    ///     See <see cref="MapGrayscaleSamples"/>'s remarks for why each color type has its own
    ///     extracted mapping method.
    /// </remarks>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when a sample's palette index has no corresponding <paramref name="palette"/> entry.
    /// </exception>
    private static void MapPaletteSamples(ReadOnlySpan<int> samples, int width, byte[] palette, byte[]? trns, Span<byte> destination)
    {
        var entries = palette.Length / 3;
        for (var x = 0; x < width; x++)
        {
            var index = samples[x];
            if (index >= entries)
            {
                throw new InvalidDataException(
                    $"PNG palette index {index} is out of range for a {entries}-entry PLTE chunk.");
            }

            var p = index * 3;
            var d = x * 4;
            destination[d] = palette[p];
            destination[d + 1] = palette[p + 1];
            destination[d + 2] = palette[p + 2];
            destination[d + 3] = trns != null && index < trns.Length ? trns[index] : (byte)255;
        }
    }

    /// <summary>
    ///     Maps one row's grayscale-with-alpha samples (color type 4) to RGBA.
    /// </summary>
    /// <remarks>
    ///     See <see cref="MapGrayscaleSamples"/>'s remarks for why each color type has its own
    ///     extracted mapping method.
    /// </remarks>
    private static void MapGrayscaleAlphaSamples(ReadOnlySpan<int> samples, int width, int bitDepth, Span<byte> destination)
    {
        for (var x = 0; x < width; x++)
        {
            var s = x * 2;
            var gray = samples[s];
            var alpha = samples[s + 1];
            var d = x * 4;
            var grayByte = bitDepth == 16 ? (byte)(gray >> 8) : (byte)gray;
            destination[d] = grayByte;
            destination[d + 1] = grayByte;
            destination[d + 2] = grayByte;
            destination[d + 3] = bitDepth == 16 ? (byte)(alpha >> 8) : (byte)alpha;
        }
    }

    /// <summary>
    ///     Maps one row's Truecolor-with-alpha samples (color type 6) to RGBA.
    /// </summary>
    /// <remarks>
    ///     See <see cref="MapGrayscaleSamples"/>'s remarks for why each color type has its own
    ///     extracted mapping method.
    /// </remarks>
    private static void MapTruecolorAlphaSamples(ReadOnlySpan<int> samples, int width, int bitDepth, Span<byte> destination)
    {
        for (var x = 0; x < width; x++)
        {
            var s = x * 4;
            var d = x * 4;
            destination[d] = bitDepth == 16 ? (byte)(samples[s] >> 8) : (byte)samples[s];
            destination[d + 1] = bitDepth == 16 ? (byte)(samples[s + 1] >> 8) : (byte)samples[s + 1];
            destination[d + 2] = bitDepth == 16 ? (byte)(samples[s + 2] >> 8) : (byte)samples[s + 2];
            destination[d + 3] = bitDepth == 16 ? (byte)(samples[s + 3] >> 8) : (byte)samples[s + 3];
        }
    }

    /// <summary>
    ///     Returns the number of samples per pixel for a given PNG color type.
    /// </summary>
    private static int SamplesPerPixel(int colorType) => colorType switch
    {
        ColorTypeGrayscale => 1,
        ColorTypeTruecolor => 3,
        ColorTypePalette => 1,
        ColorTypeGrayscaleAlpha => 2,
        ColorTypeTruecolorAlpha => 4,
        _ => throw new InvalidDataException($"Unsupported PNG color type {colorType}.")
    };

    /// <summary>
    ///     Determines whether a bit depth is legal for a given PNG color type, per the PNG
    ///     specification's color-type/bit-depth combination table.
    /// </summary>
    private static bool IsValidBitDepthForColorType(int colorType, int bitDepth) => colorType switch
    {
        ColorTypeGrayscale => bitDepth is 1 or 2 or 4 or 8 or 16,
        ColorTypeTruecolor or ColorTypeGrayscaleAlpha or ColorTypeTruecolorAlpha => bitDepth is 8 or 16,
        ColorTypePalette => bitDepth is 1 or 2 or 4 or 8,
        _ => false
    };

    /// <summary>
    ///     Validates a raw <c>tRNS</c> chunk payload against the file's color type, bit depth, and
    ///     (for palette) its <c>PLTE</c> chunk, returning the chunk unchanged when applicable or
    ///     null when absent. A <c>tRNS</c> chunk can never reach this method for the
    ///     grayscale-with-alpha or Truecolor-with-alpha color types - <c>ProcessChunk</c> rejects
    ///     such a chunk outright as soon as it is encountered, since neither color type is
    ///     spec-defined for <c>tRNS</c> - so the <c>default</c> case below exists only as a
    ///     defensive fallback for any other, already-rejected-earlier color type.
    /// </summary>
    /// <param name="colorType">The PNG color type declared by the file's IHDR chunk.</param>
    /// <param name="bitDepth">
    ///     The PNG bit depth declared by the file's IHDR chunk, used to bound the maximum sample
    ///     value a grayscale or Truecolor tRNS key may legally encode.
    /// </param>
    /// <param name="plteData">The file's PLTE chunk payload, or null if absent.</param>
    /// <param name="trnsData">The raw tRNS chunk payload to validate, or null if absent.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when a grayscale or Truecolor <c>tRNS</c> chunk does not have its mandatory
    ///     fixed length, or encodes a key sample value that exceeds the maximum value
    ///     representable at the file's bit depth (<c>(1 &lt;&lt; bitDepth) - 1</c>); or a palette
    ///     <c>tRNS</c> chunk has more entries than the PLTE chunk defines.
    /// </exception>
    private static byte[]? ValidateAndNormalizeTrns(int colorType, int bitDepth, byte[]? plteData, byte[]? trnsData)
    {
        if (trnsData == null)
        {
            return null;
        }

        switch (colorType)
        {
            case ColorTypeGrayscale:
                if (trnsData.Length != 2)
                {
                    throw new InvalidDataException(
                        $"Invalid PNG tRNS chunk length {trnsData.Length} for grayscale; expected 2 bytes.");
                }

                var maxGraySample = (1 << bitDepth) - 1;
                var grayKey = ReadUInt16Be(trnsData, 0);
                if (grayKey > maxGraySample)
                {
                    throw new InvalidDataException(
                        $"PNG tRNS grayscale key {grayKey} exceeds the maximum representable value " +
                        $"{maxGraySample} for bit depth {bitDepth}.");
                }

                return trnsData;

            case ColorTypeTruecolor:
                if (trnsData.Length != 6)
                {
                    throw new InvalidDataException(
                        $"Invalid PNG tRNS chunk length {trnsData.Length} for Truecolor; expected 6 bytes.");
                }

                var maxTruecolorSample = (1 << bitDepth) - 1;
                var redKey = ReadUInt16Be(trnsData, 0);
                var greenKey = ReadUInt16Be(trnsData, 2);
                var blueKey = ReadUInt16Be(trnsData, 4);
                if (redKey > maxTruecolorSample || greenKey > maxTruecolorSample || blueKey > maxTruecolorSample)
                {
                    throw new InvalidDataException(
                        $"PNG tRNS Truecolor key (red {redKey}, green {greenKey}, blue {blueKey}) exceeds the " +
                        $"maximum representable value {maxTruecolorSample} for bit depth {bitDepth}.");
                }

                return trnsData;

            case ColorTypePalette:
                var entries = (plteData?.Length ?? 0) / 3;
                if (trnsData.Length > entries)
                {
                    throw new InvalidDataException(
                        $"PNG tRNS chunk has more entries ({trnsData.Length}) than the PLTE chunk defines ({entries}).");
                }

                return trnsData;

            default:
                // Unreachable in practice: ProcessChunk rejects a tRNS chunk outright for
                // grayscale-with-alpha (4) and Truecolor-with-alpha (6) before it is ever stored,
                // and every other color type is handled by a case above; this defensive fallback
                // simply discards a tRNS chunk for any color type not otherwise matched.
                return null;
        }
    }

    /// <summary>
    ///     Reads a big-endian, unsigned 16-bit integer from a byte buffer at the given offset.
    /// </summary>
    private static int ReadUInt16Be(byte[] buffer, int offset) => (buffer[offset] << 8) | buffer[offset + 1];
}
