using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class GifCodec
{
    /// <summary>
    ///     Resolves one frame's decoded palette indices to RGBA pixels via
    ///     <paramref name="colorTable"/>, writing them into <paramref name="destination"/> at the
    ///     frame's declared placement, honoring the frame's transparent color index if any.
    /// </summary>
    /// <remarks>
    ///     Isolated from <see cref="Load(Stream)"/> as its own self-contained blit step - resolving
    ///     an indexed frame's pixels is a distinct, independently testable operation from the
    ///     surrounding GIF block/stream parsing that produces its inputs.
    /// </remarks>
    /// <param name="destination">The surface to write pixels into. Must already be sized to contain the frame's placement.</param>
    /// <param name="indices">The frame's decoded palette indices, one per pixel, in row-major order.</param>
    /// <param name="colorTable">The color table (local or global) to resolve each index against.</param>
    /// <param name="left">The frame's left placement offset within <paramref name="destination"/>.</param>
    /// <param name="top">The frame's top placement offset within <paramref name="destination"/>.</param>
    /// <param name="width">The frame's width, in pixels.</param>
    /// <param name="height">The frame's height, in pixels.</param>
    /// <param name="transparencyFlag">Whether <paramref name="transparentIndex"/> should be rendered fully transparent.</param>
    /// <param name="transparentIndex">The palette index treated as transparent when <paramref name="transparencyFlag"/> is set.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a decoded index has no corresponding entry in <paramref name="colorTable"/>.
    /// </exception>
    private static void BlitIndexedFrame(
        Surface destination,
        byte[] indices,
        Rgba32[] colorTable,
        int left,
        int top,
        int width,
        int height,
        bool transparencyFlag,
        byte transparentIndex)
    {
        for (var row = 0; row < height; row++)
        {
            var rowSpan = destination.GetRowSpan(top + row);
            var rowOffset = row * width;
            for (var col = 0; col < width; col++)
            {
                var index = indices[rowOffset + col];
                if (index >= colorTable.Length)
                {
                    throw new InvalidDataException(
                        $"GIF pixel index {index} is out of range for its color table " +
                        $"({colorTable.Length} entries).");
                }

                var color = colorTable[index];
                var alpha = (byte)(transparencyFlag && index == transparentIndex ? 0 : 255);
                rowSpan[left + col] = new Rgba32(color.R, color.G, color.B, alpha);
            }
        }
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
    ///     Reads a Graphic Control Extension's block data, enforcing the GIF89a specification's
    ///     requirement that it consist of exactly one 4-byte data sub-block followed immediately
    ///     by the block terminator (a zero-length sub-block) - rejecting any other declared
    ///     sub-block size, and rejecting any additional data sub-block after the first, rather
    ///     than tolerantly reassembling and accepting a total of 4 bytes reconstructed across
    ///     multiple sub-blocks (which <see cref="ReadSubBlocks"/> would otherwise permit, since it
    ///     exists to support legitimately-chained sub-blocks for image data and other extensions).
    /// </summary>
    /// <param name="stream">The stream, positioned immediately after the extension label byte.</param>
    /// <param name="remainingBudget">
    ///     The remaining total sub-block byte budget (see <see cref="MaxTotalSubBlockBytes"/>),
    ///     decremented by the 4 bytes read.
    /// </param>
    /// <returns>The Graphic Control Extension's 4 data bytes.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream ends unexpectedly, the first sub-block's declared size is not
    ///     exactly <see cref="GraphicControlExtensionSize"/>, a second, non-terminating sub-block
    ///     follows the data sub-block, or reading this sub-block would exceed
    ///     <paramref name="remainingBudget"/>.
    /// </exception>
    private static byte[] ReadGraphicControlExtensionData(Stream stream, ref long remainingBudget)
    {
        var size = stream.ReadByte();
        if (size < 0)
        {
            throw new InvalidDataException(
                "Unexpected end of stream while reading a GIF Graphic Control Extension.");
        }

        if (size != GraphicControlExtensionSize)
        {
            throw new InvalidDataException(
                $"Invalid Graphic Control Extension length {size}; expected {GraphicControlExtensionSize}.");
        }

        if (size > remainingBudget)
        {
            throw new InvalidDataException(
                $"GIF sub-block data exceeds the maximum total permitted size of " +
                $"{MaxTotalSubBlockBytes} bytes across the whole file.");
        }

        remainingBudget -= size;
        var data = ReadExactly(stream, size, "GIF Graphic Control Extension");

        var terminator = stream.ReadByte();
        if (terminator < 0)
        {
            throw new InvalidDataException(
                "Unexpected end of stream while reading a GIF Graphic Control Extension's block terminator.");
        }

        if (terminator != 0)
        {
            throw new InvalidDataException(
                "GIF Graphic Control Extension has additional sub-block data after its required 4 data " +
                "bytes; expected the block terminator.");
        }

        return data;
    }

    /// <summary>
    ///     Reads a GIF sub-block chain: a sequence of length-prefixed data blocks (each preceded
    ///     by a single size byte), terminated by a zero-length size byte, concatenating every
    ///     block's data into one buffer. This generic reader is spec-correct for every extension
    ///     type other than the Graphic Control Extension - Comment, Application, Plain Text, or
    ///     any future/unknown label (see <see cref="ReadGraphicControlExtensionData"/> for the
    ///     Graphic Control Extension's stricter single-sub-block requirement) - and for an Image
    ///     Descriptor's compressed image data, with no per-label special-casing needed to consume
    ///     the bytes.
    /// </summary>
    /// <param name="stream">The stream to read the sub-block chain from.</param>
    /// <param name="remainingBudget">
    ///     The number of sub-block data bytes still permitted across the entire file, shared
    ///     across every call for the same <see cref="Load(Stream)"/> or <see cref="GetInfo(Stream)"/>
    ///     invocation (see <see cref="MaxTotalSubBlockBytes"/>) - decremented as bytes are read,
    ///     regardless of whether the caller ultimately uses or discards this chain's data.
    /// </param>
    /// <returns>The concatenated bytes of every sub-block in the chain.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream ends before the terminating zero-length sub-block is read, or
    ///     when reading this chain would exceed <paramref name="remainingBudget"/>.
    /// </exception>
    private static byte[] ReadSubBlocks(Stream stream, ref long remainingBudget)
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

            if (size > remainingBudget)
            {
                throw new InvalidDataException(
                    $"GIF sub-block data exceeds the maximum total permitted size of " +
                    $"{MaxTotalSubBlockBytes} bytes across the whole file.");
            }

            remainingBudget -= size;

            var block = ReadExactly(stream, size, "GIF sub-block");
            buffer.Write(block, 0, block.Length);
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     Walks a GIF sub-block chain exactly as <see cref="ReadSubBlocks"/> does - the same
    ///     length-prefixed sub-blocks, the same zero-length terminator, and the same
    ///     <paramref name="remainingBudget"/> guard, decremented identically - but discards each
    ///     sub-block's bytes as they are read instead of concatenating them into a buffer. Used
    ///     by <see cref="CountFrames"/>, which never needs a sub-block chain's actual contents
    ///     (only to advance the stream past it while enforcing the same resource limits), so it
    ///     never has to pay for buffering data - up to the full <see cref="MaxTotalSubBlockBytes"/>
    ///     budget, per chain - that it would immediately discard.
    /// </summary>
    /// <param name="stream">The stream to read the sub-block chain from.</param>
    /// <param name="remainingBudget">
    ///     The number of sub-block data bytes still permitted across the entire file; see
    ///     <see cref="ReadSubBlocks"/>'s identical parameter for the shared-budget contract.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream ends before the terminating zero-length sub-block is read, or
    ///     when reading this chain would exceed <paramref name="remainingBudget"/>.
    /// </exception>
    private static void SkipSubBlocks(Stream stream, ref long remainingBudget)
    {
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

            if (size > remainingBudget)
            {
                throw new InvalidDataException(
                    $"GIF sub-block data exceeds the maximum total permitted size of " +
                    $"{MaxTotalSubBlockBytes} bytes across the whole file.");
            }

            remainingBudget -= size;

            SkipExactly(stream, size, "GIF sub-block");
        }
    }

    /// <summary>
    ///     Reads a single Image Descriptor's 9-byte fixed header, optional Local Color Table, and
    ///     LZW minimum code size byte (with its 2-8 range check), without reading the descriptor's
    ///     compressed image data sub-block chain. Used identically by <see cref="Load(Stream)"/>'s
    ///     <c>ImageSeparator</c> case and <see cref="CountFrames"/>, so both walk an Image
    ///     Descriptor's fixed-size fields in exactly the same order and with exactly the same
    ///     validation.
    /// </summary>
    /// <param name="stream">
    ///     The stream, positioned immediately after the Image Separator (<c>0x2C</c>) introducer
    ///     byte.
    /// </param>
    /// <returns>
    ///     The descriptor's placement (<c>Left</c>, <c>Top</c>), declared size (<c>Width</c>,
    ///     <c>Height</c>), packed byte (bit 7 = local color table flag, bit 6 = interlace flag,
    ///     bits 0-2 = local color table size exponent when present), any Local Color Table, and
    ///     the validated LZW minimum code size.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream ends before the 9-byte descriptor, any Local Color Table, or the
    ///     minimum code size byte has been read, or the minimum code size is outside the 2-8
    ///     range - validated here for every Image Descriptor, not merely the first, whose pixel
    ///     data is separately range-checked (again) by <c>DecodeGifLzw</c> when
    ///     <see cref="Load(Stream)"/> decodes it, because a later frame's out-of-range minimum
    ///     code size is just as structurally invalid even when that frame's pixels are never
    ///     decoded.
    /// </exception>
    private static (int Left, int Top, int Width, int Height, byte Packed, Rgba32[]? LocalColorTable, int MinCodeSize)
        ReadImageDescriptorHeader(Stream stream)
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

        if (minCodeSizeByte is < 2 or > 8)
        {
            throw new InvalidDataException($"Invalid GIF LZW minimum code size {minCodeSizeByte}.");
        }

        return (left, top, imgWidth, imgHeight, imgPacked, localColorTable, minCodeSizeByte);
    }

    /// <summary>
    ///     Validates that an Image Descriptor's declared size is positive and its placement lies
    ///     entirely within the logical screen, then resolves its active color table (its own
    ///     Local Color Table if present, otherwise the file's Global Color Table). Used
    ///     identically by <see cref="Load(Stream)"/>'s <c>ImageSeparator</c> case and
    ///     <see cref="CountFrames"/>, so both apply exactly the same per-frame structural
    ///     validation - not merely to the first frame, whose pixel data is the only one either
    ///     method ever actually decodes or counts pixels for.
    /// </summary>
    /// <param name="imgWidth">The Image Descriptor's declared width.</param>
    /// <param name="imgHeight">The Image Descriptor's declared height.</param>
    /// <param name="left">The Image Descriptor's declared left placement offset.</param>
    /// <param name="top">The Image Descriptor's declared top placement offset.</param>
    /// <param name="canvasWidth">The logical screen's declared width.</param>
    /// <param name="canvasHeight">The logical screen's declared height.</param>
    /// <param name="localColorTable">The Image Descriptor's own Local Color Table, if any.</param>
    /// <param name="globalColorTable">The file's Global Color Table, if any.</param>
    /// <returns>The resolved color table (<paramref name="localColorTable"/> if present, otherwise <paramref name="globalColorTable"/>).</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="imgWidth"/> or <paramref name="imgHeight"/> is
    ///     non-positive, the descriptor's region lies outside the logical screen bounds, or
    ///     neither a Local nor a Global Color Table is available.
    /// </exception>
    private static Rgba32[] ValidateFrameRegionAndResolveColorTable(
        int imgWidth,
        int imgHeight,
        int left,
        int top,
        int canvasWidth,
        int canvasHeight,
        Rgba32[]? localColorTable,
        Rgba32[]? globalColorTable)
    {
        if (imgWidth <= 0 || imgHeight <= 0)
        {
            throw new InvalidDataException($"Invalid GIF Image Descriptor size {imgWidth}x{imgHeight}.");
        }

        if (left + imgWidth > canvasWidth || top + imgHeight > canvasHeight)
        {
            throw new InvalidDataException(
                "GIF Image Descriptor region lies outside the logical screen bounds.");
        }

        // Every Image Descriptor - not merely the first - must have a resolvable color table to
        // be a structurally valid GIF frame, even though only the first frame's pixel data is
        // ever actually decoded by Load; validating this unconditionally ensures a later frame's
        // malformed pixel data (here, a missing color table) is never silently accepted merely
        // because the frame that mattered has already been handled.
        return localColorTable ?? globalColorTable ??
            throw new InvalidDataException(
                "GIF Image Descriptor has no local or global color table available.");
    }

    /// <summary>
    ///     Walks a GIF file's blocks from the current stream position (immediately after any
    ///     Global Color Table) through and including the Trailer, counting every Image Descriptor
    ///     encountered and applying the same per-frame structural validation
    ///     <see cref="Load(Stream)"/> applies. Every frame after the first has its compressed
    ///     image data, and every other extension's sub-block data, read only far enough to be
    ///     skipped via <see cref="SkipSubBlocks"/>, without buffering it or ever invoking the LZW
    ///     decoder. The <em>first</em> frame is different: its compressed image data is buffered
    ///     via <see cref="ReadSubBlocks"/> and then fed through the same LZW decoder
    ///     (<c>DecodeGifLzw</c>) <see cref="Load(Stream)"/> itself invokes for that frame, so that
    ///     a corrupt first-frame LZW payload can be detected and reported via the returned
    ///     <c>CanDecode</c> value rather than silently accepted; the decoded palette-index bytes
    ///     that call produces are then checked against the resolved color table's length - the
    ///     same out-of-range check <see cref="BlitIndexedFrame"/> itself performs when
    ///     <see cref="Load(Stream)"/> blits this same frame - so an out-of-range index is also
    ///     reported via <c>CanDecode</c> instead of only surfacing when <c>Load</c> later throws;
    ///     the decoded indices are discarded immediately after this check - never resolved into a
    ///     <see cref="Surface"/> - so this still performs no full pixel decode. This first-frame
    ///     LZW-decode-validation attempt is itself skipped - leaving the returned <c>CanDecode</c>
    ///     at <see langword="true"/> - whenever the first frame's declared width times height
    ///     (widened to <see langword="long"/> before multiplying, so the check itself cannot
    ///     overflow) exceeds <see cref="Surface.MaxDimension"/> squared, because this method
    ///     deliberately never bounds a frame's declared dimensions by
    ///     <see cref="Surface.MaxDimension"/> and an unbounded declared size could otherwise force
    ///     an allocation of proportional, attacker-influenced size inside <c>DecodeGifLzw</c>; see
    ///     this method's call site for the full reasoning. Also rejects any trailing data after
    ///     the Trailer, exactly as <see cref="Load(Stream)"/> does, so <c>GetInfo</c> never
    ///     succeeds on a structurally malformed stream <c>Load</c> would reject.
    /// </summary>
    /// <param name="stream">The stream, positioned immediately after the Global Color Table (or Logical Screen Descriptor, if none).</param>
    /// <param name="canvasWidth">The logical screen's declared width, from the Logical Screen Descriptor.</param>
    /// <param name="canvasHeight">The logical screen's declared height, from the Logical Screen Descriptor.</param>
    /// <param name="globalColorTable">The file's Global Color Table, if any.</param>
    /// <returns>
    ///     The total number of Image Descriptors encountered before the Trailer (always at least
    ///     1), together with whether the first Image Descriptor's compressed data successfully
    ///     LZW-decoded <em>and</em> every decoded index was within range of its resolved color
    ///     table (<see langword="false"/> when that decode attempt threw
    ///     <see cref="InvalidDataException"/>, or when any decoded index had no corresponding
    ///     entry in the resolved color table - see this method's remarks).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown for any of the same <em>structural</em> reasons <see cref="Load(Stream)"/>
    ///     throws while walking its blocks (an unexpected block introducer byte, a malformed
    ///     Graphic Control Extension, an Image Descriptor failing
    ///     <see cref="ValidateFrameRegionAndResolveColorTable"/>, the total sub-block data
    ///     exceeding <see cref="MaxTotalSubBlockBytes"/>, the stream ending unexpectedly, trailing
    ///     data remaining after the Trailer, or no Image Descriptor ever being encountered before
    ///     the Trailer) - see <see cref="MaxTotalSubBlockBytes"/>'s remarks for why this shared
    ///     budget introduces no new unbounded-loop or resource-exhaustion risk specific to frame
    ///     counting. Deliberately <em>not</em> thrown when only the first frame's compressed data
    ///     fails to LZW-decode - that well-formed-but-undecodable-payload case is instead reported
    ///     via this method's returned <c>CanDecode</c> value; see this method's remarks.
    /// </exception>
    private static (int FrameCount, bool CanDecode) CountFrames(
        Stream stream,
        int canvasWidth,
        int canvasHeight,
        Rgba32[]? globalColorTable)
    {
        var frameCount = 0;
        var canDecode = true;
        var sawTrailer = false;
        var remainingSubBlockBudget = MaxTotalSubBlockBytes;

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

                        if (labelByte == GraphicControlLabel)
                        {
                            // GetInfo never resolves transparency, but still reads (and
                            // structurally validates) the Graphic Control Extension's data,
                            // exactly as Load does, purely to advance the stream past this block.
                            ReadGraphicControlExtensionData(stream, ref remainingSubBlockBudget);
                        }
                        else
                        {
                            SkipSubBlocks(stream, ref remainingSubBlockBudget);
                        }

                        break;
                    }

                case ImageSeparator:
                    {
                        var header = ReadImageDescriptorHeader(stream);
                        var isFirstFrame = frameCount == 0;

                        // Only the first frame's compressed image data is ever actually needed:
                        // Load only ever decodes the first frame's pixels, and GetInfo mirrors
                        // that scope exactly (see this method's remarks) - so only the first
                        // frame's sub-block chain is buffered (via ReadSubBlocks) so it can be
                        // fed through the exact same LZW-decode validation attempt below; every
                        // later frame's chain is still merely skipped (via SkipSubBlocks),
                        // without buffering, exactly as before.
                        byte[]? imageData = null;
                        if (isFirstFrame)
                        {
                            imageData = ReadSubBlocks(stream, ref remainingSubBlockBudget);
                        }
                        else
                        {
                            SkipSubBlocks(stream, ref remainingSubBlockBudget);
                        }

                        var activeColorTable = ValidateFrameRegionAndResolveColorTable(
                            header.Width,
                            header.Height,
                            header.Left,
                            header.Top,
                            canvasWidth,
                            canvasHeight,
                            header.LocalColorTable,
                            globalColorTable);

                        if (isFirstFrame)
                        {
                            // Attempt the exact same LZW decode Load performs for the first
                            // frame, reusing DecodeGifLzw unchanged, but discarding its decoded
                            // palette-index output instead of resolving it against a color table
                            // and blitting it into a Surface - GetInfo must never materialize
                            // decoded pixels, only determine whether Load's identical decode
                            // attempt would succeed. A failure here means the first frame's
                            // compressed data is corrupt in a way Load's LZW decoder would reject
                            // (see DecodeGifLzw's own exception conditions); this is a
                            // well-formed-container-but-undecodable-payload case - like PngCodec's
                            // Adam7-interlacing case - so it is reported via CanDecode = false
                            // rather than making GetInfo itself throw. A structurally valid LZW
                            // stream can still decode to an index with no corresponding entry in
                            // the resolved color table - the same out-of-range condition
                            // BlitIndexedFrame itself rejects when Load blits this same frame -
                            // so every decoded index is also checked against
                            // activeColorTable.Length here, without ever allocating a Surface or
                            // blitting: only the index bytes are compared to the color table's
                            // length.
                            //
                            // header.Width and header.Height are each declared-header values that
                            // GetInfo deliberately never bounds by Surface.MaxDimension (see this
                            // method's and GetInfo's own remarks) - Load itself is not at risk
                            // here because it rejects an over-large canvas via Surface.MaxDimension
                            // long before this code path is ever reached. Multiplying two such
                            // unbounded int values with plain int arithmetic could silently
                            // overflow (e.g. 50,000 x 50,000), and even a non-overflowing but huge
                            // product would force DecodeGifLzw to allocate a `new byte[...]`
                            // proportional to that untrusted product. The product is therefore
                            // widened to long first, and this first-frame decode-validation
                            // attempt is skipped entirely - leaving canDecode at its default of
                            // true - whenever that long product exceeds
                            // Surface.MaxDimension * Surface.MaxDimension, the largest index count
                            // any frame Load could ever actually decode would require. This is a
                            // deliberate leniency, not an oversight: a pathologically large but
                            // otherwise well-formed frame is not itself malformed, so GetInfo must
                            // not throw for it; it is simply too large for this validation
                            // attempt to safely perform, exactly as MaxTotalSubBlockBytes already
                            // accepts a comparable asymmetry for a different resource (see that
                            // constant's remarks).
                            var expectedIndexCount = (long)header.Width * header.Height;
                            if (expectedIndexCount <= (long)Surface.MaxDimension * Surface.MaxDimension)
                            {
                                try
                                {
                                    var indices = DecodeGifLzw(imageData!, header.MinCodeSize, (int)expectedIndexCount);
                                    foreach (var index in indices)
                                    {
                                        if (index >= activeColorTable.Length)
                                        {
                                            canDecode = false;
                                            break;
                                        }
                                    }
                                }
                                catch (InvalidDataException)
                                {
                                    canDecode = false;
                                }
                            }
                        }

                        frameCount++;
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

        return frameCount == 0
            ? throw new InvalidDataException("GIF stream contains no Image Descriptor.")
            : (frameCount, canDecode);
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
    ///     Advances a stream past exactly <paramref name="count"/> bytes without retaining them,
    ///     using a small fixed-size reusable buffer regardless of <paramref name="count"/> - the
    ///     skipping counterpart to <see cref="ReadExactly"/>, for callers (such as
    ///     <see cref="SkipSubBlocks"/>) that need only to consume the bytes, not read their
    ///     contents.
    /// </summary>
    /// <param name="stream">The stream to advance.</param>
    /// <param name="count">The exact number of bytes to skip.</param>
    /// <param name="what">A short description of the data being skipped, used in the error message.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the stream ends before <paramref name="count"/> bytes could be skipped.
    /// </exception>
    private static void SkipExactly(Stream stream, int count, string what)
    {
        Span<byte> buffer = stackalloc byte[Math.Min(count, 4096)];
        var remaining = count;
        while (remaining > 0)
        {
            var chunkSize = Math.Min(remaining, buffer.Length);
            var read = stream.Read(buffer[..chunkSize]);
            if (read == 0)
            {
                throw new InvalidDataException($"Unexpected end of stream while reading {what}.");
            }

            remaining -= read;
        }
    }

    /// <summary>
    ///     Reads a little-endian, unsigned 16-bit integer from a byte buffer, independent of the
    ///     host CPU's native endianness.
    /// </summary>
    /// <param name="buffer">The buffer to read from.</param>
    /// <param name="offset">The zero-based offset of the first (least-significant) byte.</param>
    /// <returns>The decoded value.</returns>
    private static int ReadUInt16Le(byte[] buffer, int offset) => buffer[offset] | (buffer[offset + 1] << 8);
}
