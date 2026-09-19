using System.IO.Compression;
using CanvasNet.Canvas;

namespace CanvasNet.Codecs;

/// <summary>
///     Identifies the PNG color type used when saving a <see cref="Surface"/> to PNG format.
/// </summary>
/// <remarks>
///     Only the two 8-bit-per-channel Truecolor variants supported by <see cref="PngCodec"/> are
///     represented here, using the same numeric values as the PNG specification's color type
///     byte; grayscale, palette/indexed, and 16-bit-depth PNG variants are out of scope for this
///     codec and are never produced by <see cref="PngCodec.Save(Surface, System.IO.Stream, PngColorType)"/>.
/// </remarks>
public enum PngColorType
{
    /// <summary>
    ///     Truecolor (8 bits each for red, green, blue, no alpha channel). The alpha channel of
    ///     the source <see cref="Surface"/> is not written to the file, so a PNG saved at this
    ///     color type is always fully opaque when reloaded.
    /// </summary>
    Rgb = 2,

    /// <summary>
    ///     Truecolor with alpha (8 bits each for red, green, blue, alpha). The alpha channel of
    ///     the source <see cref="Surface"/> is preserved exactly.
    /// </summary>
    Rgba = 6
}

/// <summary>
///     Provides hand-rolled, dependency-free loading and saving of a restricted subset of PNG
///     files (8-bit-per-channel Truecolor or Truecolor-with-alpha, no interlacing) to and from
///     <see cref="Surface"/> pixel buffers.
/// </summary>
/// <remarks>
///     <c>PngCodec</c> is the fourth software unit in CanvasNet, and depends on <see cref="Surface"/>
///     exactly as <see cref="BmpCodec"/> does: it constructs and reads <see cref="Surface"/>
///     instances via the existing public <see cref="Surface.GetRowSpanBytes"/> accessor, but adds
///     no new public members to <see cref="Surface"/> itself. It is a stateless, static utility
///     class - there is nothing to construct or configure, so an instance type would add no
///     value over static methods.
///     <para>
///         Only 8-bit-per-channel color type 2 (Truecolor/RGB) and color type 6 (Truecolor with
///         alpha/RGBA) are supported, with the standard (non-interlaced) scanline order. Grayscale
///         (0), palette/indexed (3), grayscale-with-alpha (4) color types, any bit depth other than
///         8, and Adam7 interlacing (interlace method 1) are all explicitly rejected with a
///         descriptive <see cref="System.IO.InvalidDataException"/> rather than silently producing
///         incorrect pixels.
///     </para>
///     <para>
///         PNG's <c>IDAT</c> payload is a zlib stream (RFC 1950): a 2-byte header, DEFLATE-
///         compressed data, and a 4-byte big-endian Adler-32 trailer. This codec strips/prepends
///         the 2-byte header and 4-byte trailer itself and feeds the inner bytes directly to
///         <see cref="DeflateStream"/> (available, without any conditional compilation, on every
///         one of this library's target frameworks), computing and validating the Adler-32
///         checksum with a hand-rolled implementation rather than taking a new dependency. Every
///         PNG chunk's CRC-32 (also hand-rolled, the standard table-driven IEEE 802.3 algorithm)
///         is validated on load and computed on save.
///     </para>
///     <para>
///         Architectural decision: on save, every scanline is written using filter type 0 (None).
///         This is the simplest filter strategy that is always correct - it never depends on
///         neighboring pixel values - at the cost of a somewhat larger file than a heuristic
///         per-row filter choice would produce. On load, all five standard filter types
///         (None/Sub/Up/Average/Paeth) are fully reconstructed, since a loaded PNG may have been
///         produced by any PNG encoder using any filter type.
///     </para>
///     <para>
///         PNG multi-byte integers (chunk length, CRC-32, IHDR width/height) are big-endian
///         regardless of host CPU/OS endianness. All multi-byte fields are read and written by
///         explicit byte composition (not <see cref="BitConverter"/> or
///         <see cref="System.Buffers.Binary.BinaryPrimitives"/>), so the codec's behavior is
///         identical on big-endian and little-endian hosts.
///     </para>
/// </remarks>
public static class PngCodec
{
    /// <summary>
    ///     The standard 8-byte PNG file signature.
    /// </summary>
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    ///     The lookup table used by <see cref="ComputeCrc32"/>, built once using the standard
    ///     IEEE 802.3 (PNG/zlib) polynomial.
    /// </summary>
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    ///     The only bit depth this codec supports (8 bits per channel).
    /// </summary>
    private const byte BitDepth = 8;

    /// <summary>
    ///     The only PNG compression method (zlib/DEFLATE) this codec supports.
    /// </summary>
    private const byte CompressionMethodZlib = 0;

    /// <summary>
    ///     The only PNG filter method (adaptive per-scanline filtering) this codec supports.
    /// </summary>
    private const byte FilterMethodStandard = 0;

    /// <summary>
    ///     The only PNG interlace method (no interlacing) this codec supports.
    /// </summary>
    private const byte InterlaceNone = 0;

    /// <summary>
    ///     The modulus used by the Adler-32 checksum algorithm (the largest prime smaller than
    ///     2^16, as fixed by the zlib/Adler-32 specification).
    /// </summary>
    private const uint AdlerModulus = 65521;

    /// <summary>
    ///     The maximum size, in bytes, of a single written <c>IDAT</c> chunk's data. Larger
    ///     compressed payloads are split across multiple <c>IDAT</c> chunks rather than written
    ///     as one unbounded chunk, matching common PNG encoder practice.
    /// </summary>
    private const int MaxIdatChunkSize = 8192;

    /// <summary>
    ///     Loads a <see cref="Surface"/> from an open, readable stream containing a supported PNG
    ///     image (8-bit-per-channel Truecolor or Truecolor with alpha, non-interlaced).
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the PNG image from. Reading begins at the stream's current position
    ///     and consumes exactly the PNG signature, all chunks through <c>IEND</c>.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> containing the decoded pixels. Pixels decoded from a
    ///     Truecolor (color type 2) image always have alpha 255 (fully opaque); pixels decoded
    ///     from a Truecolor-with-alpha (color type 6) image retain the alpha value stored in the
    ///     file.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not contain a valid, supported PNG image: the 8-byte PNG
    ///     signature is missing, the <c>IHDR</c> chunk is missing, malformed, or describes an
    ///     unsupported bit depth, color type, compression method, filter method, or interlace
    ///     method; any chunk's CRC-32 does not match; the decompressed scanline data has an
    ///     unexpected length; an unsupported scanline filter type is encountered; or the stream
    ///     ends before all header, chunk, or pixel data has been read.
    /// </exception>
    /// <example>
    ///     <code>
    ///     using var stream = new MemoryStream();
    ///     var surface = new Surface(2, 2);
    ///     surface[0, 0] = new Rgba32(0, 255, 0, 128); // semi-transparent green pixel
    ///
    ///     PngCodec.Save(surface, stream); // save with alpha preserved (Rgba, the default)
    ///     stream.Position = 0;
    ///     var loaded = PngCodec.Load(stream);
    ///     Console.WriteLine(loaded[0, 0].A); // Output: 128
    ///     </code>
    /// </example>
    public static Surface Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Validate the fixed 8-byte PNG signature before attempting to interpret anything else
        // as chunk data
        var signature = ReadExactly(stream, Signature.Length, "PNG signature");
        for (var i = 0; i < Signature.Length; i++)
        {
            if (signature[i] != Signature[i])
            {
                throw new InvalidDataException("Not a PNG file (missing PNG signature).");
            }
        }

        var ihdrSeen = false;
        var iendSeen = false;
        var width = 0;
        var height = 0;
        var colorType = 0;
        using var idatStream = new MemoryStream();

        // Read chunks until IEND is encountered; ReadExactly throws InvalidDataException if the
        // stream ends before IEND is found, which correctly rejects a truncated stream
        while (!iendSeen)
        {
            var lengthBytes = ReadExactly(stream, 4, "chunk length");
            var length = ReadUInt32Be(lengthBytes, 0);
            if (length > int.MaxValue)
            {
                throw new InvalidDataException("Chunk length exceeds the supported range.");
            }

            var typeBytes = ReadExactly(stream, 4, "chunk type");
            var data = length == 0 ? [] : ReadExactly(stream, (int)length, "chunk data");
            var crcBytes = ReadExactly(stream, 4, "chunk CRC");
            var expectedCrc = ReadUInt32Be(crcBytes, 0);

            // The CRC-32 covers the chunk type and data, but not the length field itself
            var crcInput = new byte[4 + data.Length];
            typeBytes.CopyTo(crcInput, 0);
            data.CopyTo(crcInput, 4);
            var actualCrc = ComputeCrc32(crcInput);
            if (actualCrc != expectedCrc)
            {
                throw new InvalidDataException("Corrupt PNG chunk (CRC-32 mismatch).");
            }

            if (ChunkTypeIs(typeBytes, "IHDR"))
            {
                if (ihdrSeen)
                {
                    throw new InvalidDataException("Duplicate IHDR chunk.");
                }

                (width, height, colorType) = ParseIhdr(data);
                ihdrSeen = true;
            }
            else if (ChunkTypeIs(typeBytes, "IDAT"))
            {
                if (!ihdrSeen)
                {
                    throw new InvalidDataException("IDAT chunk encountered before IHDR.");
                }

                idatStream.Write(data, 0, data.Length);
            }
            else if (ChunkTypeIs(typeBytes, "IEND"))
            {
                if (!ihdrSeen)
                {
                    throw new InvalidDataException("IEND chunk encountered before IHDR.");
                }

                iendSeen = true;
            }

            // Any other chunk type (for example "tEXt", "pHYs", "gAMA") is an ancillary chunk
            // this codec does not need; its CRC-32 has already been validated above, and its
            // data is simply not accumulated anywhere, effectively skipping it
        }

        if (!ihdrSeen)
        {
            throw new InvalidDataException("Missing IHDR chunk.");
        }

        var channels = colorType == (int)PngColorType.Rgba ? 4 : 3;
        var rowBytes = width * channels;
        var rawData = ZlibDecompress(idatStream.ToArray());
        var expectedRawLength = (long)(rowBytes + 1) * height;
        if (rawData.LongLength != expectedRawLength)
        {
            throw new InvalidDataException(
                "PNG scanline data has an unexpected length (corrupt or truncated image data).");
        }

        var surface = new Surface(width, height);
        var previousRow = new byte[rowBytes];
        var currentRow = new byte[rowBytes];
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            var filterType = rawData[offset];
            offset++;
            var filtered = rawData.AsSpan(offset, rowBytes);
            offset += rowBytes;

            DefilterRow(filterType, filtered, previousRow, currentRow, channels);
            UnpackRow(currentRow, surface.GetRowSpanBytes(y), width, channels);

            // Swap buffers rather than copying: the just-defiltered row becomes the "previous
            // row" reference for the next iteration, and the old previous-row buffer is reused
            // (and fully overwritten) as the next iteration's output buffer
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return surface;
    }

    /// <summary>
    ///     Loads a <see cref="Surface"/> from a PNG file at the specified path.
    /// </summary>
    /// <param name="path">The path of the PNG file to load. Must not be null or empty.</param>
    /// <returns>
    ///     A new <see cref="Surface"/> containing the decoded pixels; see <see cref="Load(Stream)"/>
    ///     for the decoding contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown for the same malformed/unsupported-format conditions as <see cref="Load(Stream)"/>.
    /// </exception>
    /// <remarks>
    ///     File-system exceptions (for example <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, <see cref="UnauthorizedAccessException"/>,
    ///     or <see cref="IOException"/>) raised while opening <paramref name="path"/> propagate
    ///     uncaught to the caller.
    /// </remarks>
    public static Surface Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Load(stream);
    }

    /// <summary>
    ///     Saves a <see cref="Surface"/> to a stream as a PNG image.
    /// </summary>
    /// <param name="surface">The pixel buffer to save. Must not be null.</param>
    /// <param name="stream">The stream to write the PNG image to. Must not be null.</param>
    /// <param name="colorType">
    ///     The PNG color type to write. Defaults to <see cref="PngColorType.Rgba"/>, which
    ///     preserves the source surface's alpha channel exactly with no extra caller effort; pass
    ///     <see cref="PngColorType.Rgb"/> for a smaller file when alpha is not needed.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="stream"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="colorType"/> is not a defined <see cref="PngColorType"/> value.
    /// </exception>
    /// <remarks>
    ///     Saving at <see cref="PngColorType.Rgb"/> discards the source surface's alpha channel
    ///     entirely - the written file has no alpha information at all, so reloading it via
    ///     <see cref="Load(Stream)"/> always yields fully opaque (alpha 255) pixels, regardless of
    ///     the alpha values present in <paramref name="surface"/> at save time. Every scanline is
    ///     written using filter type 0 (None); see the type-level remarks for the rationale.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using var stream = new MemoryStream();
    ///     var surface = new Surface(2, 2);
    ///     surface[0, 0] = new Rgba32(0, 255, 0, 128); // semi-transparent green pixel
    ///
    ///     PngCodec.Save(surface, stream, PngColorType.Rgb); // discard alpha for a smaller file
    ///     stream.Position = 0;
    ///     var loaded = PngCodec.Load(stream);
    ///     Console.WriteLine(loaded[0, 0].A); // Output: 255
    ///     </code>
    /// </example>
    public static void Save(Surface surface, Stream stream, PngColorType colorType = PngColorType.Rgba)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(stream);
        if (colorType != PngColorType.Rgb && colorType != PngColorType.Rgba)
        {
            throw new ArgumentOutOfRangeException(nameof(colorType), colorType, "Color type must be Rgb or Rgba.");
        }

        stream.Write(Signature, 0, Signature.Length);

        var channels = colorType == PngColorType.Rgba ? 4 : 3;

        var ihdrData = new byte[13];
        WriteUInt32Be(ihdrData, 0, (uint)surface.Width);
        WriteUInt32Be(ihdrData, 4, (uint)surface.Height);
        ihdrData[8] = BitDepth;
        ihdrData[9] = (byte)colorType;
        ihdrData[10] = CompressionMethodZlib;
        ihdrData[11] = FilterMethodStandard;
        ihdrData[12] = InterlaceNone;
        WriteChunk(stream, "IHDR", ihdrData);

        // Build the complete raw scanline buffer up front: one filter-type byte (always 0/None)
        // followed by the row's packed pixel bytes, for every row
        var rowBytes = surface.Width * channels;
        var raw = new byte[(long)(rowBytes + 1) * surface.Height];
        var offset = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            raw[offset] = 0; // Filter type 0 (None)
            offset++;
            PackRow(surface.GetRowSpanBytes(y), raw.AsSpan(offset, rowBytes), surface.Width, channels);
            offset += rowBytes;
        }

        var compressed = ZlibCompress(raw);

        // Split the compressed payload across as many IDAT chunks as needed, rather than writing
        // one unbounded chunk, matching common PNG encoder practice
        var position = 0;
        while (position < compressed.Length)
        {
            var chunkLength = Math.Min(MaxIdatChunkSize, compressed.Length - position);
            WriteChunk(stream, "IDAT", compressed.AsSpan(position, chunkLength).ToArray());
            position += chunkLength;
        }

        WriteChunk(stream, "IEND", []);
    }

    /// <summary>
    ///     Saves a <see cref="Surface"/> to a file as a PNG image.
    /// </summary>
    /// <param name="surface">The pixel buffer to save. Must not be null.</param>
    /// <param name="path">The destination file path. Must not be null or empty.</param>
    /// <param name="colorType">
    ///     The PNG color type to write; see <see cref="Save(Surface, Stream, PngColorType)"/> for
    ///     the default and its rationale.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="path"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="colorType"/> is not a defined <see cref="PngColorType"/> value.
    /// </exception>
    /// <remarks>
    ///     Any existing file at <paramref name="path"/> is overwritten. File-system exceptions
    ///     (for example <see cref="UnauthorizedAccessException"/>, <see cref="DirectoryNotFoundException"/>,
    ///     or <see cref="IOException"/>) raised while creating <paramref name="path"/> propagate
    ///     uncaught to the caller. See <see cref="Save(Surface, Stream, PngColorType)"/> for the
    ///     alpha-handling contract of each <paramref name="colorType"/> value.
    /// </remarks>
    public static void Save(Surface surface, string path, PngColorType colorType = PngColorType.Rgba)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        Save(surface, stream, colorType);
    }

    /// <summary>
    ///     Parses and validates a 13-byte <c>IHDR</c> chunk payload.
    /// </summary>
    /// <param name="data">The raw <c>IHDR</c> chunk data (must be exactly 13 bytes).</param>
    /// <returns>The parsed image width, height, and PNG color type byte.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when <paramref name="data"/> is not 13 bytes, describes non-positive
    ///     dimensions, or describes an unsupported bit depth, color type, compression method,
    ///     filter method, or interlace method.
    /// </exception>
    private static (int Width, int Height, int ColorType) ParseIhdr(byte[] data)
    {
        if (data.Length != 13)
        {
            throw new InvalidDataException($"Invalid IHDR chunk length {data.Length}; expected 13 bytes.");
        }

        var width = (int)ReadUInt32Be(data, 0);
        var height = (int)ReadUInt32Be(data, 4);
        var bitDepth = data[8];
        var colorType = data[9];
        var compressionMethod = data[10];
        var filterMethod = data[11];
        var interlaceMethod = data[12];

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Invalid PNG dimensions {width}x{height}.");
        }

        if (bitDepth != BitDepth)
        {
            throw new InvalidDataException(
                $"Unsupported PNG bit depth {bitDepth}; only 8 bits per channel is supported.");
        }

        if (colorType != (int)PngColorType.Rgb && colorType != (int)PngColorType.Rgba)
        {
            throw new InvalidDataException(
                $"Unsupported PNG color type {colorType}; only Truecolor (2) and Truecolor with alpha (6) are supported.");
        }

        if (compressionMethod != CompressionMethodZlib)
        {
            throw new InvalidDataException(
                $"Unsupported PNG compression method {compressionMethod}; only zlib/DEFLATE (0) is supported.");
        }

        if (filterMethod != FilterMethodStandard)
        {
            throw new InvalidDataException(
                $"Unsupported PNG filter method {filterMethod}; only the standard adaptive filter method (0) is supported.");
        }

        if (interlaceMethod != InterlaceNone)
        {
            throw new InvalidDataException(
                $"Unsupported PNG interlace method {interlaceMethod}; only non-interlaced images (0) are supported.");
        }

        return (width, height, colorType);
    }

    /// <summary>
    ///     Determines whether a 4-byte chunk type field matches the given ASCII chunk type name.
    /// </summary>
    /// <param name="typeBytes">The 4-byte chunk type field read from the stream.</param>
    /// <param name="type">The 4-character ASCII chunk type name to compare against.</param>
    /// <returns><see langword="true"/> if every byte matches; otherwise, <see langword="false"/>.</returns>
    private static bool ChunkTypeIs(byte[] typeBytes, string type)
    {
        for (var i = 0; i < 4; i++)
        {
            if (typeBytes[i] != (byte)type[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Writes a complete PNG chunk (length, type, data, and CRC-32) to a stream.
    /// </summary>
    /// <param name="stream">The stream to write the chunk to.</param>
    /// <param name="type">The 4-character ASCII chunk type name (for example <c>"IHDR"</c>).</param>
    /// <param name="data">The chunk's data payload; may be empty.</param>
    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var lengthBytes = new byte[4];
        WriteUInt32Be(lengthBytes, 0, (uint)data.Length);
        stream.Write(lengthBytes, 0, lengthBytes.Length);

        var typeBytes = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            typeBytes[i] = (byte)type[i];
        }

        stream.Write(typeBytes, 0, typeBytes.Length);
        stream.Write(data, 0, data.Length);

        var crcInput = new byte[4 + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, 4);
        var crcBytes = new byte[4];
        WriteUInt32Be(crcBytes, 0, ComputeCrc32(crcInput));
        stream.Write(crcBytes, 0, crcBytes.Length);
    }

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
                for (var i = 0; i < filtered.Length; i++)
                {
                    int left = i >= bpp ? output[i - bpp] : 0;
                    output[i] = (byte)(filtered[i] + left);
                }

                break;

            case 2: // Up
                for (var i = 0; i < filtered.Length; i++)
                {
                    output[i] = (byte)(filtered[i] + previousRow[i]);
                }

                break;

            case 3: // Average
                for (var i = 0; i < filtered.Length; i++)
                {
                    int left = i >= bpp ? output[i - bpp] : 0;
                    int up = previousRow[i];
                    output[i] = (byte)(filtered[i] + (left + up) / 2);
                }

                break;

            case 4: // Paeth
                for (var i = 0; i < filtered.Length; i++)
                {
                    int left = i >= bpp ? output[i - bpp] : 0;
                    int up = previousRow[i];
                    int upperLeft = i >= bpp ? previousRow[i - bpp] : 0;
                    output[i] = (byte)(filtered[i] + PaethPredictor(left, up, upperLeft));
                }

                break;

            default:
                throw new InvalidDataException(
                    $"Unsupported PNG filter type {filterType}; only filter types 0-4 are supported.");
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
    ///     Converts one row of raw PNG pixel bytes (RGB or RGBA, 8-bit depth) into the surface's
    ///     RGBA byte order, forcing alpha to 255 when the source has no alpha channel.
    /// </summary>
    /// <param name="source">The row's raw pixel bytes (<paramref name="channels"/> bytes per pixel).</param>
    /// <param name="destination">The surface row to fill, as RGBA bytes (4 bytes per pixel).</param>
    /// <param name="width">The number of pixels in the row.</param>
    /// <param name="channels">The number of bytes per pixel in <paramref name="source"/> (3 or 4).</param>
    private static void UnpackRow(ReadOnlySpan<byte> source, Span<byte> destination, int width, int channels)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceOffset = x * channels;
            var destinationOffset = x * 4;
            destination[destinationOffset] = source[sourceOffset];
            destination[destinationOffset + 1] = source[sourceOffset + 1];
            destination[destinationOffset + 2] = source[sourceOffset + 2];
            destination[destinationOffset + 3] = channels == 4 ? source[sourceOffset + 3] : (byte)255;
        }
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

    /// <summary>
    ///     Builds the 256-entry CRC-32 lookup table using the standard IEEE 802.3 polynomial
    ///     (0xEDB88320, reflected), as required by the PNG specification.
    /// </summary>
    /// <returns>A 256-entry lookup table for <see cref="ComputeCrc32"/>.</returns>
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < table.Length; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>
    ///     Computes the PNG/zlib CRC-32 checksum of a byte sequence.
    /// </summary>
    /// <param name="data">The data to checksum.</param>
    /// <returns>The 32-bit CRC checksum.</returns>
    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>
    ///     Computes the Adler-32 checksum of a byte sequence, as used by the zlib stream format
    ///     wrapping PNG's <c>IDAT</c> payload.
    /// </summary>
    /// <param name="data">The data to checksum.</param>
    /// <returns>The 32-bit Adler-32 checksum.</returns>
    private static uint ComputeAdler32(ReadOnlySpan<byte> data)
    {
        var a = 1u;
        var b = 0u;
        foreach (var value in data)
        {
            a = (a + value) % AdlerModulus;
            b = (b + a) % AdlerModulus;
        }

        return (b << 16) | a;
    }

    /// <summary>
    ///     Compresses raw scanline bytes into a complete zlib stream: a 2-byte zlib header,
    ///     DEFLATE-compressed data, and a 4-byte big-endian Adler-32 trailer.
    /// </summary>
    /// <param name="rawData">The uncompressed scanline bytes to compress.</param>
    /// <returns>The complete zlib-wrapped byte sequence.</returns>
    private static byte[] ZlibCompress(byte[] rawData)
    {
        byte[] deflateData;
        using (var output = new MemoryStream())
        {
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
            {
                deflate.Write(rawData, 0, rawData.Length);
            }

            deflateData = output.ToArray();
        }

        var adler = ComputeAdler32(rawData);
        var result = new byte[2 + deflateData.Length + 4];
        result[0] = 0x78;
        result[1] = 0x9C;
        deflateData.CopyTo(result, 2);
        WriteUInt32Be(result, result.Length - 4, adler);
        return result;
    }

    /// <summary>
    ///     Decompresses a complete zlib stream (2-byte header, DEFLATE-compressed data, 4-byte
    ///     big-endian Adler-32 trailer) into its raw scanline bytes.
    /// </summary>
    /// <param name="zlibData">The complete zlib-wrapped byte sequence to decompress.</param>
    /// <returns>The decompressed raw scanline bytes.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the data is too short to be a valid zlib stream, the zlib header's
    ///     compression method is not DEFLATE, the header's FCHECK bits are invalid, the header
    ///     declares a preset dictionary (unsupported), or the decompressed data's Adler-32
    ///     checksum does not match the trailer.
    /// </exception>
    private static byte[] ZlibDecompress(byte[] zlibData)
    {
        if (zlibData.Length < 6)
        {
            throw new InvalidDataException("Zlib stream is too short to be valid.");
        }

        var cmf = zlibData[0];
        var flg = zlibData[1];
        if ((cmf & 0x0F) != 8)
        {
            throw new InvalidDataException(
                $"Unsupported zlib compression method {cmf & 0x0F}; only DEFLATE (8) is supported.");
        }

        if (((cmf << 8) | flg) % 31 != 0)
        {
            throw new InvalidDataException("Invalid zlib header (FCHECK validation failed).");
        }

        if ((flg & 0x20) != 0)
        {
            throw new InvalidDataException("Zlib preset dictionaries are not supported.");
        }

        var deflateData = zlibData.AsSpan(2, zlibData.Length - 2 - 4).ToArray();
        var expectedAdler = ReadUInt32Be(zlibData, zlibData.Length - 4);

        byte[] decompressed;
        using (var input = new MemoryStream(deflateData))
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            deflate.CopyTo(output);
            decompressed = output.ToArray();
        }

        var actualAdler = ComputeAdler32(decompressed);
        if (actualAdler != expectedAdler)
        {
            throw new InvalidDataException("Zlib Adler-32 checksum mismatch (corrupt PNG data).");
        }

        return decompressed;
    }

    /// <summary>
    ///     Reads a big-endian, unsigned 32-bit integer from a byte buffer at the given offset.
    /// </summary>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="offset">The offset of the first (most significant) byte.</param>
    /// <returns>The decoded 32-bit value.</returns>
    private static uint ReadUInt32Be(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) |
        ((uint)buffer[offset + 1] << 16) |
        ((uint)buffer[offset + 2] << 8) |
        buffer[offset + 3];

    /// <summary>
    ///     Writes a big-endian, unsigned 32-bit integer into a byte buffer at the given offset.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="offset">The offset of the first (most significant) byte to write.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteUInt32Be(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    /// <summary>
    ///     Reads exactly <paramref name="count"/> bytes from a stream into a newly allocated buffer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="count">The exact number of bytes required.</param>
    /// <param name="what">A short description of the data being read, used in the error message.</param>
    /// <returns>A newly allocated buffer of length <paramref name="count"/>.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream ends before <paramref name="count"/> bytes could be read.
    /// </exception>
    private static byte[] ReadExactly(Stream stream, int count, string what)
    {
        var buffer = new byte[count];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                throw new InvalidDataException($"Unexpected end of stream while reading {what}.");
            }

            totalRead += read;
        }

        return buffer;
    }
}
