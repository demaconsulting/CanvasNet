using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class TiffCodec
{
    /// <summary>
    ///     Reads and validates the 8-byte TIFF file header (byte-order mark and magic number),
    ///     returning the detected endianness and the offset of the first Image File Directory.
    /// </summary>
    private static (bool BigEndian, uint IfdOffset) ParseTiffHeader(byte[] file)
    {
        if (file.Length < 8)
        {
            throw new InvalidDataException("Unexpected end of stream while reading the TIFF header.");
        }

        bool bigEndian;
        if (file[0] == (byte)'I' && file[1] == (byte)'I')
        {
            bigEndian = false;
        }
        else if (file[0] == (byte)'M' && file[1] == (byte)'M')
        {
            bigEndian = true;
        }
        else
        {
            throw new InvalidDataException("Not a TIFF file (byte-order mark is neither \"II\" nor \"MM\").");
        }

        var magic = ReadUInt16(file, 2, bigEndian);
        if (magic != 42)
        {
            throw new InvalidDataException($"Invalid TIFF magic number {magic}; expected 42.");
        }

        var ifdOffset = ReadUInt32(file, 4, bigEndian);
        return (bigEndian, ifdOffset);
    }

    /// <summary>
    ///     The subset of TIFF IFD tag values needed to decode strip-based pixel data, parsed and
    ///     validated up front by <see cref="ReadTiffImageInfo"/>.
    /// </summary>
    private readonly record struct TiffImageInfo(
        int Width,
        int Height,
        int SamplesPerPixel,
        TiffCompression Compression,
        int Photometric,
        int PlanarConfiguration,
        int Predictor);

    /// <summary>
    ///     Reads and validates the image-level TIFF tags (dimensions, bits per sample, samples
    ///     per pixel, compression, photometric interpretation, planar configuration, and
    ///     predictor), throwing <see cref="InvalidDataException"/> for any unsupported value.
    /// </summary>
    /// <param name="source">The abstracted, random-access-capable TIFF byte source.</param>
    /// <param name="tags">The parsed IFD tag lookup.</param>
    /// <param name="bigEndian">The file's detected byte order.</param>
    /// <param name="enforceMaxDimension">
    ///     When <see langword="true"/>, rejects a width or height above
    ///     <see cref="Surface.MaxDimension"/> with an <see cref="InvalidDataException"/>, as
    ///     <see cref="Load(Stream)"/> requires. When <see langword="false"/>, the raw
    ///     header-declared width and height are returned without comparison.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown, in addition to the format-support conditions documented on
    ///     <see cref="Load(Stream)"/>/<see cref="GetInfo(Stream)"/>, when the directory contains a
    ///     <c>TileWidth</c> or <c>TileLength</c> tag (tiled TIFF images are not supported), or when
    ///     any of the tags this method resolves has a declared value <c>Count</c> of 0 or above
    ///     <see cref="MaxImageLevelTagCount"/>.
    /// </exception>
    private static TiffImageInfo ReadTiffImageInfo(
        ITiffDataSource source,
        Dictionary<ushort, IfdEntry> tags,
        bool bigEndian,
        bool enforceMaxDimension)
    {
        // Shared by Load and GetInfo so a tiled TIFF (which DecodeStrips cannot handle) is
        // rejected identically everywhere, rather than only when Load is used.
        if (tags.ContainsKey(TagTileWidth) || tags.ContainsKey(TagTileLength))
        {
            throw new InvalidDataException("Tiled TIFF images are not supported; only strip-based images are supported.");
        }

        var width = (int)RequireTagValues(source, tags, TagImageWidth, "ImageWidth", bigEndian, MaxImageLevelTagCount)[0];
        var height = (int)RequireTagValues(source, tags, TagImageLength, "ImageLength", bigEndian, MaxImageLevelTagCount)[0];
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Invalid TIFF dimensions {width}x{height}.");
        }

        // Reject dimensions above Surface.MaxDimension here, before any width/height arithmetic
        // (e.g. DecodeStrips' info.Width * info.SamplesPerPixel row-byte-width calculation, which
        // is computed before its Surface is constructed) is performed, so an oversized value
        // surfaces as the documented InvalidDataException rather than an
        // ArgumentOutOfRangeException escaping from deep inside Surface's constructor. Skipped
        // entirely when enforceMaxDimension is false, so GetInfo can report the raw header
        // dimensions even when they exceed the bound.
        if (enforceMaxDimension && (width > Surface.MaxDimension || height > Surface.MaxDimension))
        {
            throw new InvalidDataException(
                $"TIFF dimensions {width}x{height} exceed the maximum supported size of " +
                $"{Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        var bitsPerSample = RequireTagValues(source, tags, TagBitsPerSample, "BitsPerSample", bigEndian, MaxImageLevelTagCount);
        if (Array.Exists(bitsPerSample, static bits => bits != 8))
        {
            var invalidBits = Array.Find(bitsPerSample, static bits => bits != 8);
            throw new InvalidDataException(
                $"Unsupported TIFF bits per sample {invalidBits}; only 8 bits per sample is supported.");
        }

        var samplesPerPixel = TryGetTagValues(source, tags, TagSamplesPerPixel, bigEndian, MaxImageLevelTagCount) is { } sppValues
            ? (int)sppValues[0]
            : bitsPerSample.Length;

        var compressionValue = (int)RequireTagValues(source, tags, TagCompression, "Compression", bigEndian, MaxImageLevelTagCount)[0];
        if (!Enum.IsDefined((TiffCompression)compressionValue))
        {
            throw new InvalidDataException(
                $"Unsupported TIFF compression {compressionValue}; only None (1), LZW (5), Deflate (8), and PackBits (32773) are supported.");
        }

        var compression = (TiffCompression)compressionValue;

        var photometric = (int)RequireTagValues(source, tags, TagPhotometricInterpretation, "PhotometricInterpretation", bigEndian, MaxImageLevelTagCount)[0];
        if (photometric != PhotometricGrayscale && photometric != PhotometricRgb)
        {
            throw new InvalidDataException(
                $"Unsupported TIFF photometric interpretation {photometric}; only Grayscale (1) and RGB (2) are supported.");
        }

        var planarConfiguration = TryGetTagValues(source, tags, TagPlanarConfiguration, bigEndian, MaxImageLevelTagCount) is { } planarValues
            ? (int)planarValues[0]
            : PlanarChunky;
        if (planarConfiguration != PlanarChunky)
        {
            throw new InvalidDataException(
                $"Unsupported TIFF planar configuration {planarConfiguration}; only Chunky (1) is supported.");
        }

        var predictor = TryGetTagValues(source, tags, TagPredictor, bigEndian, MaxImageLevelTagCount) is { } predictorValues
            ? (int)predictorValues[0]
            : PredictorNone;
        if (predictor != PredictorNone && predictor != PredictorHorizontal)
        {
            throw new InvalidDataException(
                $"Unsupported TIFF predictor {predictor}; only None (1) and horizontal differencing (2) are supported.");
        }

        ValidateSamplesPerPixel(source, tags, photometric, samplesPerPixel, bigEndian);

        return new TiffImageInfo(width, height, samplesPerPixel, compression, photometric, planarConfiguration, predictor);
    }

    /// <summary>
    ///     Validates that <paramref name="samplesPerPixel"/> is a supported value for the given
    ///     <paramref name="photometric"/> interpretation, including the RGBA
    ///     <c>ExtraSamples</c>-tag requirement for 4-sample RGB images.
    /// </summary>
    private static void ValidateSamplesPerPixel(
        ITiffDataSource source,
        Dictionary<ushort, IfdEntry> tags,
        int photometric,
        int samplesPerPixel,
        bool bigEndian)
    {
        if (photometric == PhotometricRgb)
        {
            if (samplesPerPixel == 4)
            {
                var extraSamples = TryGetTagValues(source, tags, TagExtraSamples, bigEndian, MaxImageLevelTagCount);
                if (extraSamples is null || extraSamples[0] != ExtraSamplesUnassociatedAlpha)
                {
                    throw new InvalidDataException(
                        "An RGB TIFF image with 4 samples per pixel requires an ExtraSamples tag value of 2 (unassociated alpha).");
                }
            }
            else if (samplesPerPixel != 3)
            {
                throw new InvalidDataException(
                    $"Unsupported TIFF samples per pixel {samplesPerPixel} for RGB photometric interpretation; only 3 (RGB) or 4 (RGBA) are supported.");
            }
        }
        else if (samplesPerPixel != 1)
        {
            throw new InvalidDataException(
                $"Unsupported TIFF samples per pixel {samplesPerPixel} for Grayscale photometric interpretation; only 1 is supported.");
        }
    }

    /// <summary>
    ///     The strip-layout tag values needed to iterate a TIFF image's strips: how many rows
    ///     each strip holds, and the byte width of one decompressed, unpacked pixel row.
    /// </summary>
    private readonly record struct StripLayout(int RowsPerStrip, int RowBytes);

    /// <summary>
    ///     Reads the <c>StripOffsets</c>/<c>RowsPerStrip</c>/<c>StripByteCounts</c> tags, then
    ///     decodes every strip in order into a new <see cref="Surface"/>, verifying afterward
    ///     that the strips cover the full declared image height.
    /// </summary>
    /// <param name="file">
    ///     The fully buffered file bytes, used only for the raw strip-data extraction that
    ///     <see cref="DecodeStrip"/> performs; <see cref="Load(Stream)"/> is the only caller of
    ///     this method, so <paramref name="file"/> and <paramref name="source"/> always wrap the
    ///     same underlying bytes.
    /// </param>
    /// <param name="source">The abstracted, random-access-capable TIFF byte source used for tag lookups.</param>
    /// <param name="tags">The parsed IFD tag lookup.</param>
    /// <param name="info">The already-validated image-level TIFF tag values.</param>
    /// <param name="bigEndian">The file's detected byte order.</param>
    private static Surface DecodeStrips(byte[] file, ITiffDataSource source, Dictionary<ushort, IfdEntry> tags, TiffImageInfo info, bool bigEndian)
    {
        var stripOffsets = RequireTagValues(source, tags, TagStripOffsets, "StripOffsets", bigEndian);
        var rowsPerStrip = (int)RequireTagValues(source, tags, TagRowsPerStrip, "RowsPerStrip", bigEndian)[0];
        var stripByteCounts = RequireTagValues(source, tags, TagStripByteCounts, "StripByteCounts", bigEndian);
        if (rowsPerStrip <= 0)
        {
            throw new InvalidDataException($"Invalid TIFF RowsPerStrip value {rowsPerStrip}.");
        }

        if (stripOffsets.Length != stripByteCounts.Length)
        {
            throw new InvalidDataException("StripOffsets and StripByteCounts entry counts do not match.");
        }

        var surface = new Surface(info.Width, info.Height);
        var layout = new StripLayout(rowsPerStrip, info.Width * info.SamplesPerPixel);
        var destinationRow = 0;

        for (var stripIndex = 0; stripIndex < stripOffsets.Length; stripIndex++)
        {
            destinationRow = DecodeStrip(
                file,
                stripOffsets[stripIndex],
                stripByteCounts[stripIndex],
                info,
                layout,
                surface,
                destinationRow);
        }

        if (destinationRow != info.Height)
        {
            throw new InvalidDataException("TIFF strips do not cover the full declared image height.");
        }

        return surface;
    }

    /// <summary>
    ///     Decompresses one TIFF strip and unpacks its rows (reversing the horizontal predictor
    ///     first, if applicable) into <paramref name="surface"/> starting at
    ///     <paramref name="destinationRow"/>, returning the updated destination row index.
    /// </summary>
    private static int DecodeStrip(
        byte[] file,
        uint stripOffsetValue,
        uint stripByteCountValue,
        TiffImageInfo info,
        StripLayout layout,
        Surface surface,
        int destinationRow)
    {
        var stripOffset = ToInt32Checked(stripOffsetValue, "strip offset");
        var stripByteCount = ToInt32Checked(stripByteCountValue, "strip byte count");
        CheckBounds(file, stripOffset, stripByteCount, "strip data");
        var stripBytes = file.AsSpan(stripOffset, stripByteCount).ToArray();

        var decompressed = info.Compression switch
        {
            TiffCompression.None => stripBytes,
            TiffCompression.PackBits => DecodePackBits(stripBytes),
            TiffCompression.Lzw => DecodeLzw(stripBytes),
            TiffCompression.Deflate => ZlibDecompress(stripBytes),
            _ => throw new InvalidDataException($"Unsupported TIFF compression {info.Compression}.")
        };

        var rowsInStrip = Math.Min(layout.RowsPerStrip, info.Height - destinationRow);
        if (rowsInStrip <= 0)
        {
            return destinationRow;
        }

        var expectedLength = (long)layout.RowBytes * rowsInStrip;
        if (decompressed.LongLength < expectedLength)
        {
            throw new InvalidDataException(
                "Decompressed TIFF strip data is shorter than expected (corrupt or truncated image data).");
        }

        for (var row = 0; row < rowsInStrip; row++)
        {
            var rowSpan = decompressed.AsSpan(row * layout.RowBytes, layout.RowBytes);
            if (info.Predictor == PredictorHorizontal)
            {
                RemoveHorizontalPredictor(rowSpan, info.SamplesPerPixel);
            }

            UnpackRow(rowSpan, surface.GetRowSpanBytes(destinationRow), info.Width, info.SamplesPerPixel, info.Photometric);
            destinationRow++;
        }

        return destinationRow;
    }

    /// <summary>
    ///     Converts one row of raw TIFF pixel bytes (RGB, RGBA, or Grayscale, 8-bit depth) into
    ///     the surface's RGBA byte order, forcing alpha to 255 when the source has no alpha
    ///     channel and expanding a single gray sample into equal R, G, and B values.
    /// </summary>
    private static void UnpackRow(
        ReadOnlySpan<byte> source, Span<byte> destination, int width, int samplesPerPixel, int photometric)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceOffset = x * samplesPerPixel;
            var destinationOffset = x * 4;
            if (photometric == PhotometricRgb)
            {
                destination[destinationOffset] = source[sourceOffset];
                destination[destinationOffset + 1] = source[sourceOffset + 1];
                destination[destinationOffset + 2] = source[sourceOffset + 2];
                destination[destinationOffset + 3] = samplesPerPixel == 4 ? source[sourceOffset + 3] : (byte)255;
            }
            else
            {
                var gray = source[sourceOffset];
                destination[destinationOffset] = gray;
                destination[destinationOffset + 1] = gray;
                destination[destinationOffset + 2] = gray;
                destination[destinationOffset + 3] = 255;
            }
        }
    }
}
