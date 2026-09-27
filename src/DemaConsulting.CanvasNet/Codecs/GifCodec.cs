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
public static partial class GifCodec
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
    ///         same bytes would succeed. Because the first frame's declared width and height
    ///         are not themselves bounded by <see cref="Surface.MaxDimension"/> here (see
    ///         above), this first-frame decode-validation attempt is only ever made when their
    ///         product does not exceed <see cref="Surface.MaxDimension"/> squared - the widest
    ///         index count any frame <c>Load</c> could ever actually decode; a pathologically
    ///         large first frame beyond that bound is skipped entirely, leaving
    ///         <see cref="ImageInfo.CanDecode"/> at its default of <see langword="true"/> for
    ///         that file rather than risking an <see langword="int"/> overflow or an
    ///         allocation proportional to an untrusted, unbounded declared size - a deliberate
    ///         leniency carve-out, not an oversight: such a frame is not itself malformed, so
    ///         <c>GetInfo</c> must not throw for it, it is merely too large for this
    ///         particular validation attempt to safely perform. When the decode attempt throws
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
    ///     <para>
    ///         This first-frame decode-validation attempt is a deliberate, accepted cost -
    ///         not an oversight - and is an explicit part of <c>GetInfo</c>'s documented
    ///         contract for GIF specifically (see this type's and <see cref="ImageInfo"/>'s
    ///         own remarks, <c>README.md</c>, and the user guide, all of which call out this
    ///         same GIF exception to the other four codecs' pure header-only probing). A
    ///         caller who only needs <see cref="ImageInfo.FrameCount"/> or the declared
    ///         dimensions, and does not care whether <see cref="ImageInfo.CanDecode"/> is
    ///         accurate, pays this cost regardless, because <c>GetInfo</c> returns a single
    ///         <see cref="ImageInfo"/> value covering all of its properties at once - there is
    ///         no lighter-weight overload that reports frame count without also attempting this
    ///         validation. This was a considered trade-off, not an accidental regression:
    ///         an earlier revision of this method never decoded the first frame at all and
    ///         consequently reported <see cref="ImageInfo.CanDecode"/> as
    ///         <see langword="true"/> even for a GIF whose first frame's compressed data was
    ///         corrupt in a way that made <see cref="Load(Stream)"/> itself throw - directly
    ///         contradicting <see cref="ImageInfo.CanDecode"/>'s documented contract. Between
    ///         that contract violation and this bounded, documented decode cost (capped by the
    ///         same <see cref="Surface.MaxDimension"/>-squared ceiling described above), this
    ///         method deliberately accepts the cost.
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
}
