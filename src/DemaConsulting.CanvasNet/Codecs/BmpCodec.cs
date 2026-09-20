using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Identifies the pixel bit depth used when saving a <see cref="Surface"/> to BMP format.
/// </summary>
/// <remarks>
///     Only the two uncompressed (BI_RGB) depths supported by <see cref="BmpCodec"/> are
///     represented here; palette-based and compressed BMP variants are out of scope for this
///     codec and are never produced by <see cref="BmpCodec.Save(Surface, System.IO.Stream, BmpBitDepth)"/>.
/// </remarks>
public enum BmpBitDepth
{
    /// <summary>
    ///     24 bits per pixel (8 bits each for blue, green, red). The alpha channel of the source
    ///     <see cref="Surface"/> is not written to the file, so a BMP saved at this depth is
    ///     always fully opaque when reloaded.
    /// </summary>
    Bit24 = 24,

    /// <summary>
    ///     32 bits per pixel (8 bits each for blue, green, red, alpha). The alpha channel of the
    ///     source <see cref="Surface"/> is preserved exactly.
    /// </summary>
    Bit32 = 32
}

/// <summary>
///     Provides hand-rolled, dependency-free loading and saving of uncompressed Windows BMP
///     files (BITMAPFILEHEADER + BITMAPINFOHEADER, BI_RGB, 24-bit or 32-bit) to and from
///     <see cref="Surface"/> pixel buffers.
/// </summary>
/// <remarks>
///     <c>BmpCodec</c> is the third software unit in CanvasNet, and the first to depend on
///     another unit (<see cref="Surface"/>): it constructs and reads <see cref="Surface"/>
///     instances via the row-based <see cref="Surface.GetRowSpanBytes"/> accessor, but adds no
///     new public members to <see cref="Surface"/> itself. It is a stateless, static utility
///     class - there is nothing to construct or configure, so an instance type would add no
///     value over static methods.
///     <para>
///         Only the uncompressed BITMAPINFOHEADER variant is supported: BITMAPCOREHEADER,
///         BITMAPV4HEADER/BITMAPV5HEADER, RLE compression, palette-based (1/4/8-bit) formats, and
///         top-down (negative-height) images are all explicitly rejected with a descriptive
///         <see cref="System.IO.InvalidDataException"/> rather than silently producing incorrect
///         pixels.
///     </para>
///     <para>
///         BMP header fields are little-endian regardless of host CPU/OS endianness. All
///         multi-byte header fields are read and written by explicit byte composition (not
///         <see cref="BitConverter"/> or <see cref="System.Buffers.Binary.BinaryPrimitives"/>),
///         so the codec's behavior is identical on big-endian and little-endian hosts.
///     </para>
/// </remarks>
public static class BmpCodec
{
    /// <summary>
    ///     The fixed size, in bytes, of a BITMAPFILEHEADER structure.
    /// </summary>
    private const int FileHeaderSize = 14;

    /// <summary>
    ///     The fixed size, in bytes, of a BITMAPINFOHEADER structure - the only info-header
    ///     variant this codec supports.
    /// </summary>
    private const int InfoHeaderSize = 40;

    /// <summary>
    ///     The offset, in bytes, from the start of the file to the pixel data, for the header
    ///     layout this codec always writes (BITMAPFILEHEADER immediately followed by a single
    ///     BITMAPINFOHEADER, with no color table).
    /// </summary>
    private const int PixelDataOffset = FileHeaderSize + InfoHeaderSize;

    /// <summary>
    ///     BI_RGB - the only compression method this codec supports (uncompressed pixel data).
    /// </summary>
    private const int CompressionBiRgb = 0;

    /// <summary>
    ///     Loads a <see cref="Surface"/> from an open, readable stream containing an uncompressed
    ///     24-bit or 32-bit Windows BMP image.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the BMP image from. Reading begins at the stream's current
    ///     position and consumes exactly the BMP file's header and pixel data.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> containing the decoded pixels, top row first. Pixels
    ///     decoded from a 24-bit BMP always have alpha 255 (fully opaque); pixels decoded from a
    ///     32-bit BMP retain the alpha value stored in the file.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not contain a valid, supported BMP image: the
    ///     BITMAPFILEHEADER signature is not "BM", the info header is not a 40-byte
    ///     BITMAPINFOHEADER, the compression method is not BI_RGB, the bit depth is not 24 or 32,
    ///     the height is negative (top-down), the width or height exceeds
    ///     <see cref="Surface.MaxDimension"/>, or the stream ends before all header or pixel data
    ///     has been read.
    /// </exception>
    /// <example>
    ///     <code>
    ///     using var stream = new MemoryStream();
    ///     var surface = new Surface(2, 2);
    ///     surface[0, 0] = new Rgba32(255, 0, 0, 128); // semi-transparent red pixel
    ///
    ///     BmpCodec.Save(surface, stream); // save with alpha preserved (Bit32, the default)
    ///     stream.Position = 0;
    ///     var loaded = BmpCodec.Load(stream);
    ///     Console.WriteLine(loaded[0, 0].A); // Output: 128
    ///     </code>
    /// </example>
    public static Surface Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = ParseHeader(stream, enforceMaxDimension: true);

        // Skip forward to the pixel data, tolerating any gap left by a color table or extra
        // header bytes that a nonstandard-but-otherwise-valid file might include
        var alreadyRead = FileHeaderSize + InfoHeaderSize;
        if (header.BfOffBits > alreadyRead)
        {
            SkipExactly(stream, header.BfOffBits - alreadyRead);
        }

        var width = header.Width;
        var height = header.Height;
        var biBitCount = header.BiBitCount;
        var surface = new Surface(width, height);
        var bytesPerPixel = biBitCount / 8;
        var rowDataBytes = width * bytesPerPixel;
        var paddedRowBytes = RoundUpToMultipleOfFour(rowDataBytes);
        var rowBuffer = new byte[paddedRowBytes];

        // BMP pixel rows are stored bottom-up: the first row read from the file is the bottom
        // row of the image, so it is unpacked into the last row of the surface, and so on
        for (var fileRow = 0; fileRow < height; fileRow++)
        {
            ReadExactly(stream, rowBuffer, "BMP pixel data");
            var destination = surface.GetRowSpanBytes(height - 1 - fileRow);
            UnpackRow(rowBuffer, destination, width, biBitCount == (int)BmpBitDepth.Bit32);
        }

        return surface;
    }

    /// <summary>
    ///     Loads a <see cref="Surface"/> from a BMP file at the specified path.
    /// </summary>
    /// <param name="path">The path of the BMP file to load. Must not be null or empty.</param>
    /// <returns>
    ///     A new <see cref="Surface"/> containing the decoded pixels; see
    ///     <see cref="Load(Stream)"/> for the decoding contract.
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
    ///     Reads and validates a BMP file's 54-byte BITMAPFILEHEADER + BITMAPINFOHEADER, without
    ///     reading any pixel data, returning the fields both <see cref="Load(Stream)"/> and
    ///     <see cref="GetInfo(Stream)"/> need.
    /// </summary>
    /// <param name="stream">The stream to read the header from. Must already be non-null.</param>
    /// <param name="enforceMaxDimension">
    ///     When <see langword="true"/>, rejects a width or height above
    ///     <see cref="Surface.MaxDimension"/> with an <see cref="InvalidDataException"/>, as
    ///     <see cref="Load(Stream)"/> requires. When <see langword="false"/>, the raw
    ///     header-declared width and height are returned without comparison, as
    ///     <see cref="GetInfo(Stream)"/> requires.
    /// </param>
    /// <returns>
    ///     The decoded width, height, bit depth, and pixel-data offset (<c>bfOffBits</c>).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown for the same malformed/unsupported-header conditions documented on
    ///     <see cref="Load(Stream)"/>, other than the <see cref="Surface.MaxDimension"/> check
    ///     when <paramref name="enforceMaxDimension"/> is <see langword="false"/>.
    /// </exception>
    private static (int Width, int Height, int BiBitCount, int BfOffBits) ParseHeader(
        Stream stream,
        bool enforceMaxDimension)
    {
        // Read and validate the 14-byte BITMAPFILEHEADER; only the "BM" signature is checked -
        // bfSize/bfOffBits are not trusted for anything other than skipping to the pixel data
        var fileHeader = ReadExactly(stream, FileHeaderSize, "BITMAPFILEHEADER");
        if (fileHeader[0] != (byte)'B' || fileHeader[1] != (byte)'M')
        {
            throw new InvalidDataException("Not a BMP file (missing 'BM' signature).");
        }

        var bfOffBits = ReadInt32Le(fileHeader, 10);

        // Read and validate the info header. A single biSize check rejects both
        // BITMAPCOREHEADER (12 bytes) and the larger V4/V5 headers (108/124 bytes) in one place,
        // since this codec only understands the 40-byte BITMAPINFOHEADER layout
        var infoHeader = ReadExactly(stream, InfoHeaderSize, "BITMAPINFOHEADER");
        var biSize = ReadInt32Le(infoHeader, 0);
        if (biSize != InfoHeaderSize)
        {
            throw new InvalidDataException(
                $"Unsupported BMP header size {biSize}; only BITMAPINFOHEADER (40 bytes) is supported.");
        }

        var width = ReadInt32Le(infoHeader, 4);
        var height = ReadInt32Le(infoHeader, 8);
        var biBitCount = ReadUInt16Le(infoHeader, 14);
        var biCompression = ReadInt32Le(infoHeader, 16);

        if (biCompression != CompressionBiRgb)
        {
            throw new InvalidDataException(
                $"Unsupported BMP compression {biCompression}; only BI_RGB (uncompressed) is supported.");
        }

        if (biBitCount != (int)BmpBitDepth.Bit24 && biBitCount != (int)BmpBitDepth.Bit32)
        {
            throw new InvalidDataException(
                $"Unsupported BMP bit depth {biBitCount}; only 24-bit and 32-bit are supported.");
        }

        if (height < 0)
        {
            throw new InvalidDataException("Top-down BMP (negative height) is not supported.");
        }

        if (width <= 0 || height == 0)
        {
            throw new InvalidDataException($"Invalid BMP dimensions {width}x{height}.");
        }

        // Reject dimensions above Surface.MaxDimension before any width/height arithmetic
        // (row/stride sizing below, or the Surface constructor itself) is performed, so an
        // oversized value surfaces as the documented InvalidDataException rather than an
        // ArgumentOutOfRangeException escaping from deep inside Surface's constructor. Skipped
        // entirely when enforceMaxDimension is false, so GetInfo can report the raw header
        // dimensions even when they exceed the bound.
        if (enforceMaxDimension && (width > Surface.MaxDimension || height > Surface.MaxDimension))
        {
            throw new InvalidDataException(
                $"BMP dimensions {width}x{height} exceed the maximum supported size of " +
                $"{Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        return (width, height, biBitCount, bfOffBits);
    }

    /// <summary>
    ///     Reads a BMP file's header and reports its declared dimensions and pixel format,
    ///     without reading any pixel data.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the BMP header from. Reading begins at the stream's current
    ///     position and consumes exactly the 54-byte BITMAPFILEHEADER + BITMAPINFOHEADER; no
    ///     pixel data is read, and the stream is left positioned immediately after the header.
    /// </param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file's declared width, height, channel
    ///     count (3 for 24-bit, 4 for 32-bit), and whether it has an alpha channel (32-bit only).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown for the same malformed/unsupported-format conditions as
    ///     <see cref="Load(Stream)"/>, except that a width or height above
    ///     <see cref="Surface.MaxDimension"/> is <em>not</em> rejected - the raw header-declared
    ///     values are always returned; see <see cref="ImageInfo"/> for why.
    /// </exception>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = ParseHeader(stream, enforceMaxDimension: false);
        return new ImageInfo(
            header.Width,
            header.Height,
            header.BiBitCount / 8,
            header.BiBitCount == (int)BmpBitDepth.Bit32);
    }

    /// <summary>
    ///     Reads a BMP file's header at the specified path and reports its declared dimensions
    ///     and pixel format, without reading any pixel data.
    /// </summary>
    /// <param name="path">The path of the BMP file to inspect. Must not be null or empty.</param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file; see <see cref="GetInfo(Stream)"/> for
    ///     the reporting contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown for the same malformed/unsupported-format conditions as <see cref="GetInfo(Stream)"/>.
    /// </exception>
    /// <remarks>
    ///     File-system exceptions (for example <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, <see cref="UnauthorizedAccessException"/>,
    ///     or <see cref="IOException"/>) raised while opening <paramref name="path"/> propagate
    ///     uncaught to the caller.
    /// </remarks>
    public static ImageInfo GetInfo(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return GetInfo(stream);
    }

    /// <summary>
    ///     Saves a <see cref="Surface"/> to a stream as an uncompressed Windows BMP image.
    /// </summary>
    /// <param name="surface">The pixel buffer to save. Must not be null.</param>
    /// <param name="stream">The stream to write the BMP image to. Must not be null.</param>
    /// <param name="bitDepth">
    ///     The pixel bit depth to write. Defaults to <see cref="BmpBitDepth.Bit32"/>, which
    ///     preserves the source surface's alpha channel exactly with no extra caller effort;
    ///     pass <see cref="BmpBitDepth.Bit24"/> for a smaller file when alpha is not needed.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="stream"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="bitDepth"/> is not a defined <see cref="BmpBitDepth"/> value.
    /// </exception>
    /// <remarks>
    ///     Saving at <see cref="BmpBitDepth.Bit24"/> discards the source surface's alpha channel
    ///     entirely - the written file has no alpha information at all, so reloading it via
    ///     <see cref="Load(Stream)"/> always yields fully opaque (alpha 255) pixels, regardless of
    ///     the alpha values present in <paramref name="surface"/> at save time.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using var stream = new MemoryStream();
    ///     var surface = new Surface(2, 2);
    ///     surface[0, 0] = new Rgba32(255, 0, 0, 128); // semi-transparent red pixel
    ///
    ///     BmpCodec.Save(surface, stream, BmpBitDepth.Bit24); // discard alpha for a smaller file
    ///     stream.Position = 0;
    ///     var loaded = BmpCodec.Load(stream);
    ///     Console.WriteLine(loaded[0, 0].A); // Output: 255
    ///     </code>
    /// </example>
    public static void Save(Surface surface, Stream stream, BmpBitDepth bitDepth = BmpBitDepth.Bit32)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(stream);
        if (bitDepth != BmpBitDepth.Bit24 && bitDepth != BmpBitDepth.Bit32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitDepth), bitDepth, "Bit depth must be Bit24 or Bit32.");
        }

        var bytesPerPixel = (int)bitDepth / 8;
        var rowDataBytes = surface.Width * bytesPerPixel;
        var paddedRowBytes = RoundUpToMultipleOfFour(rowDataBytes);
        var pixelDataSize = paddedRowBytes * surface.Height;
        var fileSize = PixelDataOffset + pixelDataSize;

        WriteFileHeader(stream, fileSize);
        WriteInfoHeader(stream, surface.Width, surface.Height, (int)bitDepth);

        // One scratch row buffer is allocated up front and reused for every row, rather than
        // allocating per row or per pixel, matching Surface.Crop's bulk-row-copy convention
        var rowBuffer = new byte[paddedRowBytes];
        var includeAlpha = bitDepth == BmpBitDepth.Bit32;

        // BMP pixel rows are stored bottom-up, so the surface's last row is written to the file
        // first and its first row is written last
        for (var y = surface.Height - 1; y >= 0; y--)
        {
            PackRow(surface.GetRowSpanBytes(y), rowBuffer, surface.Width, includeAlpha);
            stream.Write(rowBuffer, 0, rowBuffer.Length);
        }
    }

    /// <summary>
    ///     Saves a <see cref="Surface"/> to a file as an uncompressed Windows BMP image.
    /// </summary>
    /// <param name="surface">The pixel buffer to save. Must not be null.</param>
    /// <param name="path">The destination file path. Must not be null or empty.</param>
    /// <param name="bitDepth">
    ///     The pixel bit depth to write; see <see cref="Save(Surface, Stream, BmpBitDepth)"/> for
    ///     the default and its rationale.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="path"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="bitDepth"/> is not a defined <see cref="BmpBitDepth"/> value.
    /// </exception>
    /// <remarks>
    ///     Any existing file at <paramref name="path"/> is overwritten. File-system exceptions
    ///     (for example <see cref="UnauthorizedAccessException"/>, <see cref="DirectoryNotFoundException"/>,
    ///     or <see cref="IOException"/>) raised while creating <paramref name="path"/> propagate
    ///     uncaught to the caller. See <see cref="Save(Surface, Stream, BmpBitDepth)"/> for the
    ///     alpha-handling contract of each <paramref name="bitDepth"/> value.
    /// </remarks>
    public static void Save(Surface surface, string path, BmpBitDepth bitDepth = BmpBitDepth.Bit32)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        Save(surface, stream, bitDepth);
    }

    /// <summary>
    ///     Rounds a byte count up to the next multiple of four, matching BMP's mandatory 4-byte
    ///     row alignment.
    /// </summary>
    /// <param name="value">The un-padded byte count for one row.</param>
    /// <returns>The smallest multiple of four that is greater than or equal to <paramref name="value"/>.</returns>
    private static int RoundUpToMultipleOfFour(int value) => (value + 3) & ~3;

    /// <summary>
    ///     Writes the 14-byte BITMAPFILEHEADER for a file with no color table, whose pixel data
    ///     immediately follows a single BITMAPINFOHEADER.
    /// </summary>
    /// <param name="stream">The stream to write the header to.</param>
    /// <param name="fileSize">The total size of the file, in bytes.</param>
    private static void WriteFileHeader(Stream stream, int fileSize)
    {
        var header = new byte[FileHeaderSize];
        header[0] = (byte)'B';
        header[1] = (byte)'M';
        WriteInt32Le(header, 2, fileSize);

        // bfReserved1/bfReserved2 (offsets 6 and 8) are left zero
        WriteInt32Le(header, 10, PixelDataOffset);
        stream.Write(header, 0, header.Length);
    }

    /// <summary>
    ///     Writes the 40-byte BITMAPINFOHEADER describing an uncompressed image.
    /// </summary>
    /// <param name="stream">The stream to write the header to.</param>
    /// <param name="width">The image width, in pixels.</param>
    /// <param name="height">The image height, in pixels (always written as positive/bottom-up).</param>
    /// <param name="bitCount">The bit depth, in bits per pixel (24 or 32).</param>
    private static void WriteInfoHeader(Stream stream, int width, int height, int bitCount)
    {
        var header = new byte[InfoHeaderSize];
        WriteInt32Le(header, 0, InfoHeaderSize); // biSize
        WriteInt32Le(header, 4, width); // biWidth
        WriteInt32Le(header, 8, height); // biHeight (positive => bottom-up)
        WriteUInt16Le(header, 12, 1); // biPlanes
        WriteUInt16Le(header, 14, (ushort)bitCount); // biBitCount

        // biCompression (offset 16) is left zero (BI_RGB); biSizeImage (offset 20) is written
        // with the actual pixel data size so downstream readers do not need to derive it
        var bytesPerPixel = bitCount / 8;
        var paddedRowBytes = RoundUpToMultipleOfFour(width * bytesPerPixel);
        WriteInt32Le(header, 20, paddedRowBytes * height);

        // biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant (offsets 24, 28, 32, 36)
        // are all left zero - this codec does not track physical resolution or color tables
        stream.Write(header, 0, header.Length);
    }

    /// <summary>
    ///     Converts one row of RGBA surface bytes into BMP's on-disk BGR/BGRA channel order,
    ///     dropping alpha and zero-filling any trailing row-padding bytes.
    /// </summary>
    /// <param name="source">The surface row, as RGBA bytes (4 bytes per pixel).</param>
    /// <param name="destination">
    ///     The scratch buffer to fill; its length is the full padded row size and any bytes
    ///     beyond the pixel data (the padding) are set to zero.
    /// </param>
    /// <param name="width">The number of pixels in the row.</param>
    /// <param name="includeAlpha">
    ///     <see langword="true"/> to write 4 bytes per pixel (BGRA); <see langword="false"/> to
    ///     write 3 bytes per pixel (BGR), dropping alpha entirely.
    /// </param>
    private static void PackRow(ReadOnlySpan<byte> source, Span<byte> destination, int width, bool includeAlpha)
    {
        var bytesPerPixel = includeAlpha ? 4 : 3;
        for (var x = 0; x < width; x++)
        {
            var sourceOffset = x * 4;
            var destinationOffset = x * bytesPerPixel;
            var r = source[sourceOffset];
            var g = source[sourceOffset + 1];
            var b = source[sourceOffset + 2];

            // BMP stores channels in blue, green, red order - the reverse of the surface's
            // red, green, blue, alpha order
            destination[destinationOffset] = b;
            destination[destinationOffset + 1] = g;
            destination[destinationOffset + 2] = r;
            if (includeAlpha)
            {
                destination[destinationOffset + 3] = source[sourceOffset + 3];
            }
        }

        // Zero any trailing padding bytes so the file never contains uninitialized data
        destination[(width * bytesPerPixel)..].Clear();
    }

    /// <summary>
    ///     Converts one row of BMP BGR/BGRA bytes into the surface's RGBA channel order, ignoring
    ///     any trailing row-padding bytes.
    /// </summary>
    /// <param name="source">
    ///     The raw row read from the file, including any trailing padding bytes.
    /// </param>
    /// <param name="destination">The surface row to fill, as RGBA bytes (4 bytes per pixel).</param>
    /// <param name="width">The number of pixels in the row.</param>
    /// <param name="includeAlpha">
    ///     <see langword="true"/> to read 4 bytes per pixel (BGRA) and copy the alpha byte
    ///     directly; <see langword="false"/> to read 3 bytes per pixel (BGR) and set alpha to
    ///     255 (fully opaque) for every pixel.
    /// </param>
    private static void UnpackRow(ReadOnlySpan<byte> source, Span<byte> destination, int width, bool includeAlpha)
    {
        var bytesPerPixel = includeAlpha ? 4 : 3;
        for (var x = 0; x < width; x++)
        {
            var sourceOffset = x * bytesPerPixel;
            var destinationOffset = x * 4;
            var b = source[sourceOffset];
            var g = source[sourceOffset + 1];
            var r = source[sourceOffset + 2];

            destination[destinationOffset] = r;
            destination[destinationOffset + 1] = g;
            destination[destinationOffset + 2] = b;
            destination[destinationOffset + 3] = includeAlpha ? source[sourceOffset + 3] : (byte)255;
        }
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
        ReadExactly(stream, buffer, what);
        return buffer;
    }

    /// <summary>
    ///     Reads exactly enough bytes to fill <paramref name="buffer"/> from a stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to fill completely.</param>
    /// <param name="what">A short description of the data being read, used in the error message.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream ends before <paramref name="buffer"/> could be filled.
    /// </exception>
    private static void ReadExactly(Stream stream, byte[] buffer, string what)
    {
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
    }

    /// <summary>
    ///     Advances a stream by exactly <paramref name="count"/> bytes, using <see cref="Stream.Seek"/>
    ///     when the stream supports it and reading-and-discarding otherwise.
    /// </summary>
    /// <param name="stream">The stream to advance.</param>
    /// <param name="count">The number of bytes to skip.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream ends before <paramref name="count"/> bytes could be skipped.
    /// </exception>
    private static void SkipExactly(Stream stream, int count)
    {
        if (stream.CanSeek)
        {
            stream.Seek(count, SeekOrigin.Current);
            return;
        }

        ReadExactly(stream, count, "header padding");
    }

    /// <summary>
    ///     Reads a little-endian, unsigned 16-bit integer from a byte buffer, independent of the
    ///     host CPU's native endianness.
    /// </summary>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="offset">The zero-based offset of the first (least-significant) byte.</param>
    /// <returns>The decoded value.</returns>
    private static ushort ReadUInt16Le(byte[] buffer, int offset) =>
        (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    /// <summary>
    ///     Reads a little-endian, signed 32-bit integer from a byte buffer, independent of the
    ///     host CPU's native endianness.
    /// </summary>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="offset">The zero-based offset of the first (least-significant) byte.</param>
    /// <returns>The decoded value.</returns>
    private static int ReadInt32Le(byte[] buffer, int offset) =>
        buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);

    /// <summary>
    ///     Writes a little-endian, unsigned 16-bit integer into a byte buffer, independent of the
    ///     host CPU's native endianness.
    /// </summary>
    /// <param name="buffer">The buffer to write into.</param>
    /// <param name="offset">The zero-based offset of the first (least-significant) byte.</param>
    /// <param name="value">The value to encode.</param>
    private static void WriteUInt16Le(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    /// <summary>
    ///     Writes a little-endian, signed 32-bit integer into a byte buffer, independent of the
    ///     host CPU's native endianness.
    /// </summary>
    /// <param name="buffer">The buffer to write into.</param>
    /// <param name="offset">The zero-based offset of the first (least-significant) byte.</param>
    /// <param name="value">The value to encode.</param>
    private static void WriteInt32Le(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}
