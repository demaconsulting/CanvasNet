using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Provides hand-rolled, dependency-free, decode-only loading of a common real-world subset
///     of GIF (GIF87a/GIF89a) images into <see cref="Surface"/> pixel buffers.
/// </summary>
/// <remarks>
///     <c>GifCodec</c> is the sixth software unit in the <c>Codecs</c> subsystem, and - like
///     <see cref="BmpCodec"/>, <see cref="PngCodec"/>, <see cref="TiffCodec"/>, and
///     <see cref="JpegCodec"/> - depends only on <see cref="Surface"/> and <see cref="ImageInfo"/>.
///     It is a stateless, static utility class - there is nothing to construct or configure, so
///     an instance type would add no value over static methods.
///     <para>
///         <c>GifCodec</c> is decode-only: it has no <c>Save</c> method. A well-formed GIF file
///         may contain multiple frames (an animation), but this codec decodes only the
///         <em>first</em> Image Descriptor's pixel data - every subsequent frame is parsed only
///         far enough to validate its structure and is then discarded. This is a deliberate,
///         documented scope limitation, not a malformed-input condition, so a multi-frame GIF
///         never causes <c>Load</c> to throw. For the same reason, <see cref="ImageInfo.CanDecode"/>
///         is always <see langword="true"/> for a well-formed GIF file reported by
///         <see cref="GetInfo(Stream)"/>: unlike <see cref="PngCodec"/>'s Adam7-interlacing case,
///         a GIF file isn't malformed - nor is a subsequent <c>Load</c> call expected to fail -
///         merely because it has more than one frame; only its first frame is ever decoded, by
///         design, and that always succeeds for a well-formed file.
///     </para>
///     <para>
///         GIF's LZW compression is a distinct variant from the one <see cref="TiffCodec"/>
///         implements: GIF packs variable-width codes least-significant-bit-first (the reverse of
///         TIFF's most-significant-bit-first packing - see <see cref="TiffCodec"/>'s own
///         <c>LzwBitReader</c> remarks, which explicitly note this), uses a variable (2-8 bit,
///         file-declared) minimum code size rather than TIFF's fixed 9-bit start, uses different
///         special code values (Clear code = <c>1 &lt;&lt; minCodeSize</c>, end-of-information
///         code = Clear code + 1, rather than TIFF's fixed 256/257), and grows its code width
///         using the standard (non-early-change) LZW convention. These differences are enough
///         that <c>GifCodec</c> implements its own private LZW decoder (<c>DecodeGifLzw</c>)
///         rather than reusing <see cref="TiffCodec"/>'s, while still mirroring its private
///         nested-helper <em>pattern</em> (a dedicated bit-reader class plus code-resolution and
///         table-growth logic).
///     </para>
///     <para>
///         Both <see cref="GetInfo(Stream)"/> and <see cref="Load(Stream)"/> call the same
///         private <c>ReadLogicalScreenDescriptor</c> method as their literal first step, so the
///         two methods can never observe a different header for the same bytes - the same parity
///         guarantee documented for the other four raster codecs' shared header helpers (see
///         <em>Codecs Unit Design</em>, <c>codecs.md</c>'s Header-Only Probing section).
///     </para>
/// </remarks>
public static class GifCodec
{
    /// <summary>
    ///     The number of bytes in a GIF signature/version field ("GIF87a" or "GIF89a").
    /// </summary>
    private const int SignatureLength = 6;

    /// <summary>
    ///     The number of bytes in a Logical Screen Descriptor, excluding the signature that
    ///     precedes it.
    /// </summary>
    private const int LogicalScreenDescriptorSize = 7;

    /// <summary>
    ///     The number of bytes in an Image Descriptor, excluding its leading Image Separator
    ///     (<c>0x2C</c>) byte.
    /// </summary>
    private const int ImageDescriptorSize = 9;

    /// <summary>
    ///     The number of bytes in a Graphic Control Extension's block data.
    /// </summary>
    private const int GraphicControlExtensionSize = 4;

    /// <summary>
    ///     Block introducer byte for an Extension block.
    /// </summary>
    private const int ExtensionIntroducer = 0x21;

    /// <summary>
    ///     Extension label byte identifying a Graphic Control Extension.
    /// </summary>
    private const int GraphicControlLabel = 0xF9;

    /// <summary>
    ///     Block introducer byte for an Image Descriptor.
    /// </summary>
    private const int ImageSeparator = 0x2C;

    /// <summary>
    ///     Block introducer byte for the GIF Trailer, marking the end of the data stream.
    /// </summary>
    private const int Trailer = 0x3B;

    /// <summary>
    ///     Loads a <see cref="Surface"/> from an open, readable stream containing a GIF87a or
    ///     GIF89a image.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the GIF image from. Reading begins at the stream's current
    ///     position and consumes the entire GIF data stream, through and including its Trailer.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/>, sized to the file's Logical Screen Descriptor dimensions,
    ///     containing the decoded pixels of the <em>first</em> Image Descriptor only. A pixel
    ///     outside that first frame's region - for a frame smaller than the logical screen - is
    ///     fully transparent (alpha 0), matching <see cref="Surface"/>'s own documented default.
    ///     A pixel whose palette index matches an active Graphic Control Extension's transparent
    ///     color index is also fully transparent (alpha 0); every other decoded pixel is fully
    ///     opaque (alpha 255).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not contain a valid GIF image this codec supports: the
    ///     signature is not "GIF87a"/"GIF89a", the width or height is non-positive or exceeds
    ///     <see cref="Surface.MaxDimension"/>, no color table (global or local) is available for
    ///     the first Image Descriptor, a Graphic Control Extension's data is not exactly 4 bytes,
    ///     an Image Descriptor's region lies outside the logical screen, an unexpected block
    ///     introducer byte is encountered, bytes remain in the stream after the Trailer, no Image
    ///     Descriptor is ever encountered before the Trailer, the compressed image data contains
    ///     an invalid or out-of-range LZW code, or the stream ends before all header, color-table,
    ///     or block data has been read.
    /// </exception>
    /// <example>
    ///     <code>
    ///     using var stream = File.OpenRead("animation.gif");
    ///     using var surface = GifCodec.Load(stream); // decodes only the first frame
    ///     Console.WriteLine($"{surface.Width}x{surface.Height}");
    ///     </code>
    /// </example>
    public static Surface Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var (canvasWidth, canvasHeight, packed) = ReadLogicalScreenDescriptor(stream);
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            throw new InvalidDataException($"Invalid GIF dimensions {canvasWidth}x{canvasHeight}.");
        }

        if (canvasWidth > Surface.MaxDimension || canvasHeight > Surface.MaxDimension)
        {
            throw new InvalidDataException(
                $"GIF dimensions {canvasWidth}x{canvasHeight} exceed the maximum supported size of " +
                $"{Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        Rgba32[]? globalColorTable = null;
        if ((packed & 0x80) != 0)
        {
            var globalColorTableSize = 2 << (packed & 0x07);
            globalColorTable = ReadColorTable(stream, globalColorTableSize);
        }

        var pendingTransparencyFlag = false;
        var pendingTransparentIndex = (byte)0;
        Surface? result = null;
        var sawTrailer = false;

        while (!sawTrailer)
        {
            var introducer = stream.ReadByte();
            if (introducer < 0)
            {
                throw new InvalidDataException("Unexpected end of stream while reading a GIF block.");
            }

            switch (introducer)
            {
                case ExtensionIntroducer:
                    {
                        var labelByte = stream.ReadByte();
                        if (labelByte < 0)
                        {
                            throw new InvalidDataException("Unexpected end of stream while reading a GIF extension.");
                        }

                        var data = ReadSubBlocks(stream);
                        if (labelByte == GraphicControlLabel)
                        {
                            if (data.Length != GraphicControlExtensionSize)
                            {
                                throw new InvalidDataException(
                                    $"Invalid Graphic Control Extension length {data.Length}; expected " +
                                    $"{GraphicControlExtensionSize}.");
                            }

                            pendingTransparencyFlag = (data[0] & 0x01) != 0;

                            // Byte 0 = packed fields (bit 0 = transparency flag, bits 1-3 = disposal
                            // method (ignored), bit 4 = user input flag (ignored)); bytes 1-2 = Delay
                            // Time, little-endian (ignored); byte 3 = Transparent Color Index.
                            pendingTransparentIndex = data[3];
                        }

                        break;
                    }

                case ImageSeparator:
                    {
                        var descriptor = ReadExactly(stream, ImageDescriptorSize, "GIF Image Descriptor");
                        var left = ReadUInt16Le(descriptor, 0);
                        var top = ReadUInt16Le(descriptor, 2);
                        var imgWidth = ReadUInt16Le(descriptor, 4);
                        var imgHeight = ReadUInt16Le(descriptor, 6);
                        var imgPacked = descriptor[8];

                        Rgba32[]? localColorTable = null;
                        if ((imgPacked & 0x80) != 0)
                        {
                            var localColorTableSize = 2 << (imgPacked & 0x07);
                            localColorTable = ReadColorTable(stream, localColorTableSize);
                        }

                        var minCodeSizeByte = stream.ReadByte();
                        if (minCodeSizeByte < 0)
                        {
                            throw new InvalidDataException(
                                "Unexpected end of stream while reading a GIF LZW minimum code size.");
                        }

                        var imageData = ReadSubBlocks(stream);

                        if (imgWidth <= 0 || imgHeight <= 0)
                        {
                            throw new InvalidDataException($"Invalid GIF Image Descriptor size {imgWidth}x{imgHeight}.");
                        }

                        if (left + imgWidth > canvasWidth || top + imgHeight > canvasHeight)
                        {
                            throw new InvalidDataException(
                                "GIF Image Descriptor region lies outside the logical screen bounds.");
                        }

                        if (result is null)
                        {
                            var activeColorTable = localColorTable ?? globalColorTable ??
                                throw new InvalidDataException(
                                    "GIF Image Descriptor has no local or global color table available.");

                            var indices = DecodeGifLzw(imageData, minCodeSizeByte, imgWidth * imgHeight);
                            if ((imgPacked & 0x40) != 0)
                            {
                                indices = Deinterlace(indices, imgWidth, imgHeight);
                            }

                            result = new Surface(canvasWidth, canvasHeight);
                            for (var row = 0; row < imgHeight; row++)
                            {
                                var rowSpan = result.GetRowSpan(top + row);
                                var rowOffset = row * imgWidth;
                                for (var col = 0; col < imgWidth; col++)
                                {
                                    var index = indices[rowOffset + col];
                                    if (index >= activeColorTable.Length)
                                    {
                                        throw new InvalidDataException(
                                            $"GIF pixel index {index} is out of range for its color table " +
                                            $"({activeColorTable.Length} entries).");
                                    }

                                    var color = activeColorTable[index];
                                    var alpha = (byte)(pendingTransparencyFlag && index == pendingTransparentIndex
                                        ? 0
                                        : 255);
                                    rowSpan[left + col] = new Rgba32(color.R, color.G, color.B, alpha);
                                }
                            }
                        }

                        pendingTransparencyFlag = false;
                        break;
                    }

                case Trailer:
                    sawTrailer = true;
                    break;

                default:
                    throw new InvalidDataException($"Unexpected GIF block introducer byte 0x{introducer:X2}.");
            }
        }

        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Unexpected trailing data after the GIF trailer.");
        }

        return result ?? throw new InvalidDataException("GIF stream contains no Image Descriptor.");
    }

    /// <summary>
    ///     Loads a <see cref="Surface"/> from a GIF file at the specified path.
    /// </summary>
    /// <param name="path">The path of the GIF file to load. Must not be null or empty.</param>
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
    ///     Reads a GIF file's Logical Screen Descriptor and reports its declared dimensions,
    ///     without reading any color table, block, or pixel data.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the GIF header from. Reading begins at the stream's current
    ///     position and consumes exactly the 13-byte signature and Logical Screen Descriptor; the
    ///     stream is left positioned immediately after it.
    /// </param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file's declared width and height, always
    ///     with <see cref="ImageInfo.Channels"/> equal to 1 and <see cref="ImageInfo.HasAlpha"/>
    ///     equal to <see langword="false"/> (see this codec's type-level remarks and
    ///     <see cref="ImageInfo"/>'s remarks for why); <see cref="ImageInfo.CanDecode"/> is always
    ///     <see langword="true"/> for a well-formed GIF file (see this codec's type-level remarks).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the signature is not "GIF87a"/"GIF89a", or the stream ends before the
    ///     13-byte signature and Logical Screen Descriptor has been read.
    /// </exception>
    /// <remarks>
    ///     Deliberately, <c>GetInfo</c> never enforces <see cref="Surface.MaxDimension"/> - it
    ///     always reports the raw header-declared width and height, even when they exceed that
    ///     bound - and never reads the color table, extension, or image blocks that follow the
    ///     Logical Screen Descriptor, so a stream that is truncated or malformed only <em>after</em>
    ///     its first 13 bytes does not cause <c>GetInfo</c> to throw, even though the same stream
    ///     would cause <see cref="Load(Stream)"/> to throw.
    /// </remarks>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var (width, height, _) = ReadLogicalScreenDescriptor(stream);
        return new ImageInfo(width, height, 1, false);
    }

    /// <summary>
    ///     Reads a GIF file's Logical Screen Descriptor at the specified path and reports its
    ///     declared dimensions, without reading any color table, block, or pixel data.
    /// </summary>
    /// <param name="path">The path of the GIF file to inspect. Must not be null or empty.</param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file; see <see cref="GetInfo(Stream)"/> for
    ///     the reporting contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown for the same malformed-header conditions as <see cref="GetInfo(Stream)"/>.
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
    ///     Reads and validates a GIF file's 6-byte signature and 7-byte Logical Screen
    ///     Descriptor, without reading any color table, block, or pixel data. Called identically,
    ///     as the literal first step, by both <see cref="GetInfo(Stream)"/> and
    ///     <see cref="Load(Stream)"/>, guaranteeing the two methods can never observe a different
    ///     header for the same bytes.
    /// </summary>
    /// <param name="stream">The stream to read the header from. Must already be non-null.</param>
    /// <returns>
    ///     The decoded width, height, and the Logical Screen Descriptor's packed byte (bit 7 =
    ///     global color table flag, bits 4-6 = color resolution (ignored), bit 3 = sort flag
    ///     (ignored), bits 0-2 = global color table size exponent).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the signature is not "GIF87a"/"GIF89a", or the stream ends before the
    ///     13-byte signature and Logical Screen Descriptor has been read.
    /// </exception>
    private static (int Width, int Height, byte Packed) ReadLogicalScreenDescriptor(Stream stream)
    {
        var signature = ReadExactly(stream, SignatureLength, "GIF signature");
        var isGif87A = signature.AsSpan().SequenceEqual("GIF87a"u8);
        var isGif89A = signature.AsSpan().SequenceEqual("GIF89a"u8);
        if (!isGif87A && !isGif89A)
        {
            throw new InvalidDataException(
                $"Not a GIF file (expected 'GIF87a' or 'GIF89a' signature, found " +
                $"'{System.Text.Encoding.ASCII.GetString(signature)}').");
        }

        var lsd = ReadExactly(stream, LogicalScreenDescriptorSize, "GIF Logical Screen Descriptor");
        var width = ReadUInt16Le(lsd, 0);
        var height = ReadUInt16Le(lsd, 2);
        var packed = lsd[4];

        // Bytes 5 (background color index) and 6 (pixel aspect ratio) are ignored - neither
        // affects the decoded pixel data this codec produces.
        return (width, height, packed);
    }

    /// <summary>
    ///     Reads a GIF color table (a sequence of 3-byte RGB entries, each promoted to an
    ///     opaque <see cref="Rgba32"/>), used identically for both the Global Color Table and any
    ///     Image Descriptor's Local Color Table.
    /// </summary>
    /// <param name="stream">The stream to read the color table from.</param>
    /// <param name="entryCount">The number of 3-byte RGB entries to read.</param>
    /// <returns>An array of <paramref name="entryCount"/> fully opaque <see cref="Rgba32"/> colors.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream ends before all <paramref name="entryCount"/> entries have been
    ///     read.
    /// </exception>
    private static Rgba32[] ReadColorTable(Stream stream, int entryCount)
    {
        var raw = ReadExactly(stream, entryCount * 3, "GIF color table");
        var table = new Rgba32[entryCount];
        for (var i = 0; i < entryCount; i++)
        {
            var offset = i * 3;
            table[i] = new Rgba32(raw[offset], raw[offset + 1], raw[offset + 2], 255);
        }

        return table;
    }

    /// <summary>
    ///     Reads a GIF sub-block chain: a sequence of length-prefixed data blocks (each preceded
    ///     by a single size byte), terminated by a zero-length size byte, concatenating every
    ///     block's data into one buffer. This generic reader is spec-correct for every extension
    ///     type (Graphic Control, Comment, Application, Plain Text, or any future/unknown label)
    ///     and for an Image Descriptor's compressed image data, with no per-label special-casing
    ///     needed to consume the bytes.
    /// </summary>
    /// <param name="stream">The stream to read the sub-block chain from.</param>
    /// <returns>The concatenated bytes of every sub-block in the chain.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream ends before the terminating zero-length sub-block is read.
    /// </exception>
    private static byte[] ReadSubBlocks(Stream stream)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            var size = stream.ReadByte();
            if (size < 0)
            {
                throw new InvalidDataException("Unexpected end of stream while reading a GIF sub-block chain.");
            }

            if (size == 0)
            {
                break;
            }

            var block = ReadExactly(stream, size, "GIF sub-block");
            buffer.Write(block, 0, block.Length);
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     Re-orders a flat array of decoded palette-index bytes from GIF's 4-pass interlace row
    ///     order into normal top-to-bottom row order.
    /// </summary>
    /// <param name="indices">
    ///     The decoded palette-index bytes, in the order the LZW decoder produced them: every row
    ///     of Pass 1 (every 8th row starting at row 0), then every row of Pass 2 (every 8th row
    ///     starting at row 4), then every row of Pass 3 (every 4th row starting at row 2), then
    ///     every row of Pass 4 (every 2nd row starting at row 1).
    /// </param>
    /// <param name="width">The image width, in pixels.</param>
    /// <param name="height">The image height, in pixels.</param>
    /// <returns>
    ///     A new array of the same length as <paramref name="indices"/>, with rows in normal
    ///     top-to-bottom order.
    /// </returns>
    private static byte[] Deinterlace(byte[] indices, int width, int height)
    {
        var result = new byte[indices.Length];
        var sourceRow = 0;

        void CopyPass(int start, int step)
        {
            for (var row = start; row < height; row += step)
            {
                Array.Copy(indices, sourceRow * width, result, row * width, width);
                sourceRow++;
            }
        }

        CopyPass(0, 8);
        CopyPass(4, 8);
        CopyPass(2, 4);
        CopyPass(1, 2);

        return result;
    }

    /// <summary>
    ///     Decodes a GIF-flavor LZW byte stream (variable-width codes packed
    ///     least-significant-bit-first, a file-declared minimum code size, Clear code =
    ///     <c>1 &lt;&lt; minCodeSize</c>, end-of-information code = Clear code + 1, standard
    ///     non-early-change code-width growth) into exactly <paramref name="expectedIndexCount"/>
    ///     palette-index bytes.
    /// </summary>
    /// <param name="data">The concatenated compressed image data (from <see cref="ReadSubBlocks"/>).</param>
    /// <param name="minCodeSize">
    ///     The minimum LZW code size, as declared by the byte immediately preceding the image's
    ///     sub-block chain. Must be between 2 and 8 inclusive.
    /// </param>
    /// <param name="expectedIndexCount">The exact number of palette-index bytes to produce.</param>
    /// <returns>An array of exactly <paramref name="expectedIndexCount"/> palette-index bytes.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="minCodeSize"/> is out of range, the stream does not start
    ///     with a Clear code, an invalid or out-of-range code is encountered, or the stream ends
    ///     before <paramref name="expectedIndexCount"/> bytes have been produced.
    /// </exception>
    private static byte[] DecodeGifLzw(byte[] data, int minCodeSize, int expectedIndexCount)
    {
        if (minCodeSize is < 2 or > 8)
        {
            throw new InvalidDataException($"Invalid GIF LZW minimum code size {minCodeSize}.");
        }

        var clearCode = 1 << minCodeSize;
        var eoiCode = clearCode + 1;
        var firstAvailableCode = eoiCode + 1;

        var reader = new GifLzwBitReader(data);
        var output = new byte[expectedIndexCount];
        var outputCount = 0;

        var codeSize = minCodeSize + 1;
        var table = new List<byte[]>();
        byte[]? previousEntry = null;

        var firstCode = reader.ReadCode(codeSize);
        if (firstCode != clearCode)
        {
            throw new InvalidDataException("GIF LZW stream does not start with a Clear code.");
        }

        while (outputCount < expectedIndexCount)
        {
            var code = reader.ReadCode(codeSize);
            if (code == clearCode)
            {
                table.Clear();
                codeSize = minCodeSize + 1;
                previousEntry = null;
                continue;
            }

            if (code == eoiCode)
            {
                break;
            }

            var entry = ResolveGifLzwEntry(code, clearCode, firstAvailableCode, table, previousEntry);

            var copyLength = Math.Min(entry.Length, expectedIndexCount - outputCount);
            Array.Copy(entry, 0, output, outputCount, copyLength);
            outputCount += copyLength;

            if (previousEntry is not null && firstAvailableCode + table.Count < 4096)
            {
                var newEntry = new byte[previousEntry.Length + 1];
                previousEntry.CopyTo(newEntry, 0);
                newEntry[^1] = entry[0];
                table.Add(newEntry);

                var nextCode = firstAvailableCode + table.Count;
                if (nextCode == 1 << codeSize && codeSize < 12)
                {
                    codeSize++;
                }
            }

            previousEntry = entry;
        }

        if (outputCount < expectedIndexCount)
        {
            throw new InvalidDataException("Truncated GIF LZW stream (insufficient decoded pixel data).");
        }

        return output;
    }

    /// <summary>
    ///     Resolves the byte sequence for a single decoded GIF LZW <paramref name="code"/>: a
    ///     literal single-index byte for codes below <paramref name="clearCode"/>, an existing
    ///     table entry for already-known codes, or the classic LZW "KwKwK" reconstruction
    ///     (previous entry plus its own first byte) for the one code that is always exactly one
    ///     past the current table end.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="code"/> is invalid.</exception>
    private static byte[] ResolveGifLzwEntry(
        int code,
        int clearCode,
        int firstAvailableCode,
        List<byte[]> table,
        byte[]? previousEntry)
    {
        if (code < clearCode)
        {
            return [(byte)code];
        }

        if (code - firstAvailableCode < table.Count)
        {
            return table[code - firstAvailableCode];
        }

        if (code - firstAvailableCode == table.Count && previousEntry is not null)
        {
            var entry = new byte[previousEntry.Length + 1];
            previousEntry.CopyTo(entry, 0);
            entry[^1] = previousEntry[0];
            return entry;
        }

        throw new InvalidDataException("Invalid GIF LZW code sequence.");
    }

    /// <summary>
    ///     Reads exactly <paramref name="count"/> bytes from a stream into a newly allocated buffer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="count">The exact number of bytes required.</param>
    /// <param name="what">A short description of the data being read, used in the error message.</param>
    /// <returns>A newly allocated buffer of length <paramref name="count"/>.</returns>
    /// <exception cref="InvalidDataException">
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

    /// <summary>
    ///     Reads a little-endian, unsigned 16-bit integer from a byte buffer, independent of the
    ///     host CPU's native endianness.
    /// </summary>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="offset">The zero-based offset of the first (least-significant) byte.</param>
    /// <returns>The decoded value.</returns>
    private static int ReadUInt16Le(byte[] buffer, int offset) => buffer[offset] | (buffer[offset + 1] << 8);

    /// <summary>
    ///     Unpacks variable-width LZW codes from bytes packed least-significant-bit-first, as
    ///     required by the GIF specification (the reverse bit order from the TIFF LZW variant -
    ///     see <see cref="TiffCodec"/>'s own <c>LzwBitReader</c> remarks).
    /// </summary>
    private sealed class GifLzwBitReader(byte[] data)
    {
        private int _bytePos;
        private uint _bitBuffer;
        private int _bitCount;

        public int ReadCode(int bits)
        {
            while (_bitCount < bits)
            {
                if (_bytePos >= data.Length)
                {
                    throw new InvalidDataException(
                        "Truncated GIF LZW stream (missing end-of-information code).");
                }

                _bitBuffer |= (uint)data[_bytePos] << _bitCount;
                _bytePos++;
                _bitCount += 8;
            }

            var code = (int)(_bitBuffer & ((1u << bits) - 1));
            _bitBuffer >>= bits;
            _bitCount -= bits;
            return code;
        }
    }
}
