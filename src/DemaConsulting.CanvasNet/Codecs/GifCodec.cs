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
///         never causes <c>Load</c> to throw, and <c>GetInfo</c>'s reported
///         <see cref="ImageInfo.FrameCount"/> can legitimately exceed 1 without that alone
///         affecting <see cref="ImageInfo.CanDecode"/>: unlike <see cref="PngCodec"/>'s
///         Adam7-interlacing case, a GIF file isn't malformed - nor is a subsequent <c>Load</c>
///         call expected to fail - merely because it has more than one frame; only its first
///         frame is ever decoded, by design. <see cref="GetInfo(Stream)"/> reports the file's true
///         total frame count via <see cref="ImageInfo.FrameCount"/> by walking every block in the
///         file, structurally validating every frame exactly as <c>Load</c> does, and never
///         invoking the LZW decoder for any frame after the first - a second-or-later frame's LZW
///         corruption never affects <c>CanDecode</c>, since neither <c>Load</c> nor <c>GetInfo</c>
///         ever decodes that frame's pixels. The <em>first</em> frame is different:
///         <see cref="GetInfo(Stream)"/> does attempt the same LZW decode of the first frame's
///         compressed data that <c>Load</c> performs, additionally checking every decoded index
///         against the first frame's resolved color table length - the same out-of-range check
///         <c>BlitIndexedFrame</c> performs when <c>Load</c> blits this same frame - and then
///         discarding the decoded palette indices instead of resolving and blitting them into a
///         <see cref="Surface"/>, specifically so that a first-frame LZW corruption or an
///         out-of-range decoded index, either of which would make <c>Load</c> throw, is instead
///         reflected as <see cref="ImageInfo.CanDecode"/> equal to <see langword="false"/> - a
///         third well-formed-but-undecodable case, alongside <see cref="PngCodec"/>'s
///         Adam7-interlacing case; see <see cref="GetInfo(Stream)"/>'s own remarks for the full
///         reasoning.
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
    ///     The maximum total number of sub-block data bytes <see cref="Load(Stream)"/> or
    ///     <see cref="GetInfo(Stream)"/> will accumulate across every extension and Image
    ///     Descriptor sub-block chain in a single file, including chains whose contents are
    ///     ultimately discarded (a Comment/Application/Plain Text extension, a second-or-later
    ///     frame's compressed image data for either method - <c>GetInfo</c> attempts an LZW-decode
    ///     validation of only the <em>first</em> frame's compressed data, including checking every
    ///     decoded index against that frame's resolved color table length, discarding its decoded
    ///     output rather than the sub-block bytes themselves; see <see cref="GetInfo(Stream)"/>'s
    ///     remarks). Without this bound, a GIF with tiny declared dimensions could still carry an
    ///     effectively unlimited number of 255-byte sub-blocks - each individually valid -
    ///     forcing unbounded buffering and risking an out-of-memory condition rather than a
    ///     clean, prompt <see cref="InvalidDataException"/>. 64 MiB comfortably exceeds the
    ///     compressed data size any real-world GIF encoder produces for a legitimately-sized
    ///     frame, including at <see cref="Surface.MaxDimension"/>. It is, however, a deliberate,
    ///     documented resource-safety limit rather than a mathematical guarantee: GIF LZW
    ///     compression has no enforced minimum compression ratio, so a pathological (not merely
    ///     malicious) encoder could, in principle, emit more than 64 MiB of compressed data for a
    ///     single frame whose declared dimensions are themselves well within
    ///     <see cref="Surface.MaxDimension"/> - such a file would be rejected by this bound even
    ///     though its declared dimensions alone would otherwise be decodable. This asymmetry is
    ///     accepted: prioritizing a bounded, predictable worst-case memory footprint over the
    ///     vanishingly rare legitimate file that would exceed it. Declared
    ///     <see langword="internal"/> (rather than <see langword="private"/>), matching
    ///     <see cref="JpegCodec.MaxProbeHeaderBytes"/>'s established precedent, so the test
    ///     project (which the assembly already grants <c>InternalsVisibleTo</c>) can construct a
    ///     just-over-budget fixture that exercises this limit without hard-coding its value.
    ///     <para>
    ///         Sharing this same budget (and the same <c>ReadSubBlocks</c>/<c>SkipSubBlocks</c>/
    ///         <c>ReadGraphicControlExtensionData</c> helpers) between <c>Load</c> and
    ///         <c>GetInfo</c>'s frame-counting walk is a deliberate resource-safety choice, not
    ///         merely a code-reuse convenience: it means <c>GetInfo</c> introduces no new
    ///         unbounded-loop or resource-exhaustion risk of its own. The frame-counting loop
    ///         performs no allocation proportional to the frame count beyond a single
    ///         <see langword="int"/> counter; every per-frame allocation it does perform (a Local
    ///         Color Table, or a discarded sub-block chain) is itself capped by this same 64 MiB
    ///         cumulative budget; and the loop is strictly bounded by the number of bytes actually
    ///         present in the input stream - it cannot iterate, or allocate, without consuming
    ///         input from the stream passed to <see cref="Load(Stream)"/> or
    ///         <see cref="GetInfo(Stream)"/>. A GIF's frame count is therefore never itself an
    ///         independent attack surface distinct from the one this budget already defends
    ///         against.
    ///     </para>
    /// </summary>
    internal const long MaxTotalSubBlockBytes = 64 * 1024 * 1024;

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
    ///     an Image Descriptor's region lies outside the logical screen, any Image Descriptor
    ///     (not merely the first) declares an LZW minimum code size outside the 2-8 range, an
    ///     unexpected block introducer byte is encountered, bytes remain in the stream after the
    ///     Trailer, no Image Descriptor is ever encountered before the Trailer, the compressed
    ///     image data contains an invalid or out-of-range LZW code, the total sub-block data
    ///     across the whole file exceeds <see cref="MaxTotalSubBlockBytes"/>, or the stream ends
    ///     before all header, color-table, or block data has been read.
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
                            var data = ReadGraphicControlExtensionData(stream, ref remainingSubBlockBudget);
                            pendingTransparencyFlag = (data[0] & 0x01) != 0;

                            // Byte 0 = packed fields (bit 0 = transparency flag, bits 1-3 = disposal
                            // method (ignored), bit 4 = user input flag (ignored)); bytes 1-2 = Delay
                            // Time, little-endian (ignored); byte 3 = Transparent Color Index.
                            pendingTransparentIndex = data[3];
                        }
                        else
                        {
                            // Discard any other extension's sub-block data (Application, Comment,
                            // Plain Text, or an unrecognized label); this codec only interprets the
                            // Graphic Control Extension.
                            ReadSubBlocks(stream, ref remainingSubBlockBudget);
                        }

                        break;
                    }

                case ImageSeparator:
                    {
                        var header = ReadImageDescriptorHeader(stream);
                        var imageData = ReadSubBlocks(stream, ref remainingSubBlockBudget);

                        var activeColorTable = ValidateFrameRegionAndResolveColorTable(
                            header.Width,
                            header.Height,
                            header.Left,
                            header.Top,
                            canvasWidth,
                            canvasHeight,
                            header.LocalColorTable,
                            globalColorTable);

                        if (result is null)
                        {
                            var indices = DecodeGifLzw(imageData, header.MinCodeSize, header.Width * header.Height);
                            if ((header.Packed & 0x40) != 0)
                            {
                                indices = Deinterlace(indices, header.Width, header.Height);
                            }

                            result = new Surface(canvasWidth, canvasHeight);
                            BlitIndexedFrame(
                                result,
                                indices,
                                activeColorTable,
                                header.Left,
                                header.Top,
                                header.Width,
                                header.Height,
                                pendingTransparencyFlag,
                                pendingTransparentIndex);
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
    ///     Reads a GIF file's Logical Screen Descriptor, Global Color Table (if any), and every
    ///     subsequent block up to and including the Trailer, reporting the file's declared
    ///     dimensions and total Image Descriptor (frame) count, without resolving any frame's
    ///     decoded pixels into a <see cref="Surface"/> - though it does attempt an LZW-decode
    ///     validation of the <em>first</em> frame's compressed data (see this method's remarks).
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the GIF data from. Reading begins at the stream's current position
    ///     and consumes the entire GIF data stream, through and including its Trailer, exactly as
    ///     <see cref="Load(Stream)"/> does.
    /// </param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file's declared width and height, always
    ///     with <see cref="ImageInfo.Channels"/> equal to 1 and <see cref="ImageInfo.HasAlpha"/>
    ///     equal to <see langword="false"/> (see this codec's type-level remarks and
    ///     <see cref="ImageInfo"/>'s remarks for why); <see cref="ImageInfo.CanDecode"/> is
    ///     <see langword="false"/> when the first Image Descriptor's compressed data either fails
    ///     the same LZW decode <see cref="Load(Stream)"/> itself performs for that frame, or
    ///     LZW-decodes successfully but yields an index with no corresponding entry in that
    ///     frame's resolved color table - the same out-of-range condition
    ///     <see cref="BlitIndexedFrame"/> itself rejects when <see cref="Load(Stream)"/> blits
    ///     this same frame - and <see langword="true"/> for every other well-formed GIF file (see
    ///     this codec's type-level remarks and this method's own remarks below);
    ///     <see cref="ImageInfo.FrameCount"/> is the file's true total number of Image Descriptors
    ///     (1 or more).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the signature is not "GIF87a"/"GIF89a", the declared width or height is
    ///     non-positive (matching <see cref="Load(Stream)"/>'s identical check), no color table
    ///     (global or local) is available for some Image Descriptor, a Graphic Control
    ///     Extension's data is not exactly 4 bytes, an Image Descriptor's region lies outside the
    ///     logical screen, any Image Descriptor declares an LZW minimum code size outside the 2-8
    ///     range, an unexpected block introducer byte is encountered, no Image Descriptor is ever
    ///     encountered before the Trailer, the total sub-block data across the whole file exceeds
    ///     <see cref="MaxTotalSubBlockBytes"/>, or the stream ends before all header, color-table,
    ///     or block data has been read. This is <em>structural</em> malformation only - a
    ///     well-formed container whose first frame's compressed data merely fails to LZW-decode is
    ///     reported via <see cref="ImageInfo.CanDecode"/> equal to <see langword="false"/>
    ///     instead of throwing; see this method's remarks below.
    /// </exception>
    /// <remarks>
    ///     Deliberately, <c>GetInfo</c> never enforces <see cref="Surface.MaxDimension"/> - it
    ///     always reports the raw header-declared width and height, even when they exceed that
    ///     bound; a stream whose dimensions exceed <see cref="Surface.MaxDimension"/> is otherwise
    ///     walked and validated identically to any other stream. It does, however, reject a
    ///     non-positive width or height exactly as <see cref="Load(Stream)"/> does, because a zero
    ///     (or negative, were that representable) dimension can never be decoded regardless of
    ///     the <see cref="Surface.MaxDimension"/> cap, so reporting <see cref="ImageInfo.CanDecode"/>
    ///     as <see langword="true"/> for it would be a false promise rather than a decodable
    ///     oversized image.
    ///     <para>
    ///         Counting a GIF's frames correctly requires walking every block in the file - not
    ///         merely reading the Logical Screen Descriptor, as a prior version of this method
    ///         did - because an Image Descriptor's position in the file is not otherwise
    ///         predictable (it is interleaved with an arbitrary number of extension blocks).
    ///         <c>GetInfo</c> shares its per-frame structural validation (region bounds, minimum
    ///         code size range, color table resolution) with <see cref="Load(Stream)"/> via the
    ///         same private helpers (<c>ReadImageDescriptorHeader</c>,
    ///         <c>ValidateFrameRegionAndResolveColorTable</c>), so any input <c>GetInfo</c> rejects
    ///         for a structural reason is also an input <c>Load</c> would reject - upholding
    ///         <see cref="ImageInfo"/>'s "never throws for input <c>Load</c> would accept"
    ///         invariant. <c>GetInfo</c> never invokes the LZW decoder (<c>DecodeGifLzw</c>) for
    ///         any frame after the first - <see cref="Load(Stream)"/> itself never decodes those
    ///         frames' pixels either, so this introduces no divergence. The <em>first</em> frame is
    ///         different: <c>GetInfo</c> does invoke <c>DecodeGifLzw</c> on the first frame's
    ///         compressed data, reusing the exact same decoder <see cref="Load(Stream)"/> calls,
    ///         and also checks every decoded index against the first frame's resolved color
    ///         table length (reusing the color table <c>ValidateFrameRegionAndResolveColorTable</c>
    ///         already resolved for that frame) - the same out-of-range check
    ///         <see cref="BlitIndexedFrame"/> performs when <see cref="Load(Stream)"/> blits this
    ///         frame - but discards the decoded palette-index bytes afterward instead of
    ///         resolving them through the color table and blitting them into a
    ///         <see cref="Surface"/> - so this still never materializes decoded pixels, only
    ///         determines whether <c>Load</c>'s own first-frame decode-and-blit attempt on the
    ///         same bytes would succeed. When the decode attempt throws
    ///         <see cref="InvalidDataException"/> (an invalid or out-of-range LZW code, a stream
    ///         that never starts with a Clear code, a truncated compressed stream, or one that
    ///         decodes to the wrong number of palette-index bytes - see <c>DecodeGifLzw</c>'s own
    ///         exception conditions), or when the decode succeeds but any decoded index has no
    ///         corresponding entry in the resolved color table, <c>GetInfo</c> reports
    ///         <see cref="ImageInfo.CanDecode"/> as <see langword="false"/>, rather than letting
    ///         <c>GetInfo</c> itself throw or <c>Load</c> later throw for the same input: this is a
    ///         well-formed-but-undecodable-payload case - the file's block structure is entirely
    ///         valid, only its first frame's compressed pixel data is corrupt - precisely mirroring
    ///         how <see cref="PngCodec.GetInfo(Stream)"/> reports Adam7 interlacing via
    ///         <see cref="ImageInfo.CanDecode"/> rather than throwing. This upholds both of
    ///         <see cref="ImageInfo"/>'s documented invariants at once: <c>GetInfo</c> never
    ///         throws for an input <c>Load</c> would accept (a structurally malformed input still
    ///         throws exactly as before), and <c>GetInfo</c> never silently claims a
    ///         first-frame-undecodable input can be decoded. The frame-counting walk reuses the
    ///         exact same <see cref="MaxTotalSubBlockBytes"/> cumulative budget <c>Load</c>
    ///         enforces - see
    ///         that constant's remarks for why this introduces no new unbounded-loop or
    ///         resource-exhaustion risk.
    ///     </para>
    /// </remarks>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var (width, height, packed) = ReadLogicalScreenDescriptor(stream);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Invalid GIF dimensions {width}x{height}.");
        }

        Rgba32[]? globalColorTable = null;
        if ((packed & 0x80) != 0)
        {
            var globalColorTableSize = 2 << (packed & 0x07);
            globalColorTable = ReadColorTable(stream, globalColorTableSize);
        }

        var (frameCount, canDecode) = CountFrames(stream, width, height, globalColorTable);

        return new ImageInfo(width, height, 1, false) { FrameCount = frameCount, CanDecode = canDecode };
    }

    /// <summary>
    ///     Reads a GIF file at the specified path in full - its Logical Screen Descriptor, Global
    ///     Color Table (if any), and every subsequent block through and including the Trailer -
    ///     and reports its declared dimensions together with its true total frame count via
    ///     <see cref="ImageInfo.FrameCount"/>; see <see cref="GetInfo(Stream)"/> for the complete
    ///     reporting contract.
    /// </summary>
    /// <param name="path">The path of the GIF file to inspect. Must not be null or empty.</param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file; see <see cref="GetInfo(Stream)"/> for
    ///     the reporting contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown for the same malformed-input conditions as <see cref="GetInfo(Stream)"/>.
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
    ///     <see cref="Surface"/> - so this still performs no full pixel decode. Also rejects any
    ///     trailing data after the Trailer, exactly as <see cref="Load(Stream)"/> does, so
    ///     <c>GetInfo</c> never succeeds on a structurally malformed stream <c>Load</c> would
    ///     reject.
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
                            try
                            {
                                var indices = DecodeGifLzw(imageData!, header.MinCodeSize, header.Width * header.Height);
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
    ///     with a Clear code, an invalid or out-of-range code is encountered, decoding would
    ///     produce more than <paramref name="expectedIndexCount"/> bytes, or the stream ends
    ///     before both exactly <paramref name="expectedIndexCount"/> bytes have been produced and
    ///     an end-of-information code has been read.
    /// </exception>
    /// <remarks>
    ///     Decoding always continues until an end-of-information code is read - it never stops
    ///     merely because <paramref name="expectedIndexCount"/> bytes have already been produced -
    ///     and any code that would decode past exactly that many bytes is rejected immediately,
    ///     rather than silently truncated. This ensures a compressed stream that omits the
    ///     required end-of-information code, or that decodes to a different pixel count than the
    ///     Image Descriptor declared, is always treated as malformed input.
    /// </remarks>
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

        while (true)
        {
            var code = reader.ReadCode(codeSize);
            if (code == eoiCode)
            {
                break;
            }

            if (code == clearCode)
            {
                table.Clear();
                codeSize = minCodeSize + 1;
                previousEntry = null;
                continue;
            }

            var entry = ResolveGifLzwEntry(code, clearCode, firstAvailableCode, table, previousEntry);

            if (outputCount + entry.Length > expectedIndexCount)
            {
                throw new InvalidDataException(
                    "GIF LZW stream decoded more palette-index bytes than the Image Descriptor declared.");
            }

            Array.Copy(entry, 0, output, outputCount, entry.Length);
            outputCount += entry.Length;

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

        if (outputCount != expectedIndexCount)
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
