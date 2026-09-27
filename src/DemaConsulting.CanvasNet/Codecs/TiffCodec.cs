using System.IO.Compression;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Identifies the compression method used when saving a <see cref="Surface"/> to TIFF format,
///     using the same numeric values as the TIFF specification's Compression tag.
/// </summary>
public enum TiffCompression
{
    /// <summary>
    ///     No compression; pixel data is stored uncompressed.
    /// </summary>
    None = 1,

    /// <summary>
    ///     LZW compression (TIFF 6.0 Section 13): variable-width (9-12 bit), MSB-first packed
    ///     codes, patent-free since the underlying LZW patent expired in 2003.
    /// </summary>
    Lzw = 5,

    /// <summary>
    ///     Deflate compression: a zlib-wrapped DEFLATE stream, identical in structure to PNG's
    ///     <c>IDAT</c> payload.
    /// </summary>
    Deflate = 8,

    /// <summary>
    ///     PackBits compression (TIFF 6.0 Section 9): a simple byte-oriented run-length encoding.
    /// </summary>
    PackBits = 32773
}

/// <summary>
///     Provides hand-rolled, dependency-free loading and saving of a common real-world subset of
///     TIFF 6.0 files (8-bit-per-sample RGB or Grayscale, chunky planar configuration, single
///     page, strip-based) to and from <see cref="Surface"/> pixel buffers.
/// </summary>
/// <remarks>
///     <c>TiffCodec</c> is the fifth software unit in CanvasNet, and depends on <see cref="Surface"/>
///     exactly as <see cref="BmpCodec"/> and <see cref="PngCodec"/> do: it constructs and reads
///     <see cref="Surface"/> instances via the existing public <see cref="Surface.GetRowSpanBytes"/>
///     accessor, but adds no new public members to <see cref="Surface"/> itself. It is a stateless,
///     static utility class - there is nothing to construct or configure, so an instance type
///     would add no value over static methods.
///     <para>
///         Only 8-bit-per-sample images are supported: RGB (photometric interpretation 2, with an
///         implicit fully opaque alpha channel unless a 4th sample is present with an
///         <c>ExtraSamples</c> tag value of 2, indicating unassociated alpha) and Grayscale
///         (photometric interpretation 1, BlackIsZero, loaded as R=G=B=gray with fully opaque
///         alpha). Palette (3), CMYK (5), YCbCr (6), and any other photometric interpretation are
///         explicitly rejected, as are bit depths other than 8, planar configuration 2
///         (Planar/Separate), and tiled TIFF images (identified by the presence of a
///         <c>TileWidth</c> or <c>TileLength</c> tag) - all with a descriptive
///         <see cref="System.IO.InvalidDataException"/> rather than silently producing incorrect
///         pixels. Only the first Image File Directory (IFD) is read; any subsequent IFD offset is
///         ignored, so only the first page of a multi-page TIFF is loaded.
///     </para>
///     <para>
///         Both TIFF byte-order marks are supported on load: <c>"II"</c> (little-endian) and
///         <c>"MM"</c> (big-endian), detected from the first two bytes of the file. Every
///         multi-byte field is read using the byte order determined from this mark - never
///         <see cref="BitConverter"/> or <see cref="System.Buffers.Binary.BinaryPrimitives"/>, so
///         behavior does not depend on host CPU endianness. <see cref="Save(Surface, System.IO.Stream, TiffCompression)"/>
///         always writes little-endian (<c>"II"</c>) files.
///     </para>
///     <para>
///         Four compression methods are supported on both load and save: None (1), LZW (5),
///         Deflate (8), and PackBits (32773). LZW is implemented as the TIFF-flavor variant
///         described in TIFF 6.0 Section 13 (variable-width 9-12 bit codes, MSB-first bit packing,
///         clear code 256, end-of-information code 257) - not the GIF LZW variant, which packs
///         bits LSB-first and is otherwise similar. PackBits is the standard Apple/TIFF run-length
///         encoding (TIFF 6.0 Section 9). Deflate uses the same zlib-wrapped DEFLATE container
///         (2-byte header, <see cref="DeflateStream"/>, 4-byte big-endian Adler-32 trailer) that
///         <see cref="PngCodec"/> uses for its own <c>IDAT</c> payload, reimplemented privately
///         here since each codec unit in this codebase is fully self-contained.
///     </para>
///     <para>
///         Architectural decision: because TIFF's Image File Directory stores an arbitrary byte
///         offset for its first entry and for any tag value that does not fit inline, correctly
///         parsing a TIFF file requires random access to the whole file, unlike <see cref="BmpCodec"/>
///         and <see cref="PngCodec"/>'s purely sequential parsing. <see cref="Load(System.IO.Stream)"/>
///         therefore buffers the entire input stream into memory before parsing, rather than
///         reading incrementally.
///     </para>
///     <para>
///         Architectural decision: <see cref="Save(Surface, System.IO.Stream, TiffCompression)"/>
///         always writes 8-bit RGBA (4 samples per pixel, with an <c>ExtraSamples</c> tag value of
///         2 marking the 4th sample as unassociated alpha), chunky planar configuration, as a
///         single strip containing the whole image. This preserves full round-trip fidelity
///         (including alpha) with no extra caller effort, matching <see cref="BmpCodec"/> and
///         <see cref="PngCodec"/>'s own precedent of defaulting to the alpha-preserving
///         representation; there is no separate color-type parameter in this initial version.
///     </para>
///     <para>
///         Architectural decision: when <c>compression</c> is <see cref="TiffCompression.Lzw"/>
///         or <see cref="TiffCompression.Deflate"/>, <see cref="Save(Surface, System.IO.Stream, TiffCompression)"/>
///         automatically applies the horizontal-differencing predictor (TIFF 6.0 Section 14,
///         Predictor tag value 2) before compressing, and writes the Predictor tag accordingly,
///         because it materially improves the compression ratio for typical photographic or
///         rendered content at negligible extra cost. The Predictor tag is omitted entirely (which
///         is equivalent to a value of 1, meaning no prediction) when compression is
///         <see cref="TiffCompression.None"/> or <see cref="TiffCompression.PackBits"/>, since the
///         predictor provides no benefit to those methods. <see cref="Load(System.IO.Stream)"/>
///         reverses the predictor whenever the Predictor tag is present and equal to 2, regardless
///         of which compression method is in use.
///     </para>
/// </remarks>
public static partial class TiffCodec
{
    private const ushort TagImageWidth = 256;
    private const ushort TagImageLength = 257;
    private const ushort TagBitsPerSample = 258;
    private const ushort TagCompression = 259;
    private const ushort TagPhotometricInterpretation = 262;
    private const ushort TagStripOffsets = 273;
    private const ushort TagSamplesPerPixel = 277;
    private const ushort TagRowsPerStrip = 278;
    private const ushort TagStripByteCounts = 279;
    private const ushort TagPlanarConfiguration = 284;
    private const ushort TagTileWidth = 322;
    private const ushort TagTileLength = 323;
    private const ushort TagPredictor = 317;
    private const ushort TagExtraSamples = 338;

    /// <summary>
    ///     The maximum declared value <c>Count</c> allowed for the single-valued (or, for
    ///     <c>BitsPerSample</c>, at-most-4-valued) image-level tags <see cref="ReadTiffImageInfo"/>
    ///     resolves. Applied before any count-proportional allocation or read, so a malformed file
    ///     declaring an implausibly large count for one of these tags (for example millions of
    ///     <c>BitsPerSample</c> entries) cannot force a large allocation on <c>GetInfo</c>'s
    ///     cheap-probing path even when the declared out-of-line offset/length otherwise passes
    ///     the stream-bounds check. Deliberately not applied to <c>StripOffsets</c>,
    ///     <c>RowsPerStrip</c>, or <c>StripByteCounts</c>, which are read only by
    ///     <see cref="DecodeStrips"/> (a <see cref="Load(Stream)"/>-only path) and can legitimately
    ///     have large counts for a real, large, multi-strip image.
    /// </summary>
    private const int MaxImageLevelTagCount = 8;

    private const ushort TypeByte = 1;
    private const ushort TypeShort = 3;
    private const ushort TypeLong = 4;

    private const int PhotometricGrayscale = 1;
    private const int PhotometricRgb = 2;

    private const int PlanarChunky = 1;

    private const int PredictorNone = 1;
    private const int PredictorHorizontal = 2;

    private const int ExtraSamplesUnassociatedAlpha = 2;

    /// <summary>
    ///     The number of samples per pixel this codec always writes (red, green, blue, alpha).
    /// </summary>
    private const int SavedSamplesPerPixel = 4;

    /// <summary>
    ///     The modulus used by the Adler-32 checksum algorithm, as required by the zlib format
    ///     that wraps Deflate-compressed TIFF strip data.
    /// </summary>
    private const uint AdlerModulus = 65521;

    /// <summary>
    ///     The TIFF-flavor LZW clear code, which resets the decoder's/encoder's code table.
    /// </summary>
    private const int LzwClearCode = 256;

    /// <summary>
    ///     The TIFF-flavor LZW end-of-information code, which terminates the encoded stream.
    /// </summary>
    private const int LzwEoiCode = 257;

    /// <summary>
    ///     The first code value available for dynamic table entries.
    /// </summary>
    private const int LzwFirstCode = 258;

    /// <summary>
    ///     The code value at which the table is proactively cleared, leaving headroom below the
    ///     hard 4096-entry (12-bit) limit.
    /// </summary>
    private const int LzwMaxCode = 4094;

    /// <summary>
    ///     Loads a <see cref="Surface"/> from an open, readable stream containing a supported TIFF
    ///     image (8-bit-per-sample RGB or Grayscale, chunky, single strip or multiple strips,
    ///     first page only).
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the TIFF image from. Reading begins at the stream's current position
    ///     and consumes the entire remainder of the stream (the whole stream is buffered into
    ///     memory, since TIFF's directory and value offsets require random access).
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> containing the decoded pixels. Pixels decoded from an RGB
    ///     image with 3 samples per pixel, or from a Grayscale image, always have alpha 255 (fully
    ///     opaque); pixels decoded from an RGB image with 4 samples per pixel (and an
    ///     <c>ExtraSamples</c> tag value of 2) retain the alpha value stored in the file.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not contain a valid, supported TIFF image: the byte-order
    ///     mark is neither <c>"II"</c> nor <c>"MM"</c>, the magic number is not 42, a mandatory tag
    ///     (<c>ImageWidth</c>, <c>ImageLength</c>, <c>BitsPerSample</c>, <c>Compression</c>,
    ///     <c>PhotometricInterpretation</c>, <c>StripOffsets</c>, <c>RowsPerStrip</c>, or
    ///     <c>StripByteCounts</c>) is missing, any tag this method resolves has a declared value
    ///     <c>Count</c> of 0 or (for the image-level tags also checked by
    ///     <see cref="GetInfo(Stream)"/>) above <see cref="MaxImageLevelTagCount"/>, or its
    ///     count/offset arithmetic would overflow a 32-bit integer, the image is tiled (a
    ///     <c>TileWidth</c> or <c>TileLength</c> tag is present), the bit depth is not 8, the
    ///     photometric interpretation is not Grayscale (1) or RGB (2), the compression method is
    ///     not one of None/LZW/Deflate/PackBits, the planar configuration is not Chunky (1), the
    ///     width or height exceeds <see cref="Surface.MaxDimension"/>, an RGB image with 4 samples
    ///     per pixel is missing a valid <c>ExtraSamples</c> tag, or the stream ends before all
    ///     directory, tag value, or strip data has been read.
    /// </exception>
    /// <example>
    ///     <code>
    ///     using var stream = new MemoryStream();
    ///     var surface = new Surface(2, 2);
    ///     surface[0, 0] = new Rgba32(0, 255, 0, 128); // semi-transparent green pixel
    ///
    ///     TiffCodec.Save(surface, stream); // save with alpha preserved (RGBA, the default)
    ///     stream.Position = 0;
    ///     var loaded = TiffCodec.Load(stream);
    ///     Console.WriteLine(loaded[0, 0].A); // Output: 128
    ///     </code>
    /// </example>

    public static Surface Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var file = ReadAllBytes(stream);
        var source = new ByteArrayTiffDataSource(file);
        var (bigEndian, ifdOffset) = ParseTiffHeader(file);
        var tags = ParseIfd(source, ifdOffset, bigEndian);

        var info = ReadTiffImageInfo(source, tags, bigEndian, enforceMaxDimension: true);

        return DecodeStrips(file, source, tags, info, bigEndian);
    }


    /// <summary>
    ///     Loads a <see cref="Surface"/> from a TIFF file at the specified path.
    /// </summary>
    /// <param name="path">The path of the TIFF file to load. Must not be null or empty.</param>
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
    ///     Reads a TIFF file's header and first Image File Directory and reports its declared
    ///     dimensions and pixel format, without decoding any strip/pixel data.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the TIFF header and IFD from.
    /// </param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file's declared width, height, channel
    ///     count (the <c>SamplesPerPixel</c> tag's value, defaulting to <c>BitsPerSample</c>'s
    ///     entry count when absent - identically to <see cref="Load(Stream)"/>), and whether the
    ///     image has an alpha channel (an RGB image with 4 samples per pixel and a valid
    ///     <c>ExtraSamples</c> tag value of 2).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown for the same format-support reasons as <see cref="Load(Stream)"/> - a mandatory
    ///     tag (<c>ImageWidth</c>, <c>ImageLength</c>, <c>BitsPerSample</c>, <c>Compression</c>,
    ///     or <c>PhotometricInterpretation</c>) is missing, the bit depth is not 8, the
    ///     compression method is not one of None/LZW/Deflate/PackBits, the photometric
    ///     interpretation is not Grayscale (1) or RGB (2), the planar configuration is not Chunky
    ///     (1), the predictor is not None (1) or horizontal differencing (2), an RGB image with 4
    ///     samples per pixel is missing a valid <c>ExtraSamples</c> tag, the image is tiled (a
    ///     <c>TileWidth</c> or <c>TileLength</c> tag is present), any tag this method resolves has
    ///     a declared value <c>Count</c> of 0 or above <see cref="MaxImageLevelTagCount"/>, the
    ///     byte-order mark is neither <c>"II"</c> nor <c>"MM"</c>, the magic number is not 42, the
    ///     declared dimensions are non-positive, or the stream ends before the header, directory,
    ///     or an out-of-line tag value has been read - <em>except</em> for
    ///     <see cref="Surface.MaxDimension"/>, which is deliberately <em>not</em> enforced (the
    ///     raw header-declared values are always returned; see <see cref="ImageInfo"/> for why).
    /// </exception>
    /// <remarks>
    ///     Upholds the cross-codec invariant documented on <see cref="ImageInfo"/> - "GetInfo
    ///     never throws for an input that Load would successfully decode" - for both seekable and
    ///     non-seekable streams; every tag is resolved through the exact same validating parser
    ///     <see cref="Load(Stream)"/> itself uses (<see cref="ParseIfd"/> and
    ///     <see cref="ReadTiffImageInfo"/>, with <c>enforceMaxDimension: false</c>), against one
    ///     of two <see cref="ITiffDataSource"/> implementations depending on seekability - see the
    ///     <see cref="ITiffDataSource"/> remarks for how random-access reads are abstracted over
    ///     each.
    ///     <para>
    ///         When <c>stream.CanSeek</c> is <see langword="true"/>, a
    ///         <see cref="StreamTiffDataSource"/> seeks directly to each requested position and
    ///         reads only the bytes the parser actually asks for - the 8-byte header, the IFD
    ///         entry count and entries, and any out-of-line tag value the parser needs (for
    ///         example a multi-value <c>BitsPerSample</c> tag). It never reads
    ///         <c>StripOffsets</c>/<c>RowsPerStrip</c>/<c>StripByteCounts</c> or any strip/pixel
    ///         data, since <see cref="ReadTiffImageInfo"/> never resolves those tags.
    ///     </para>
    ///     <para>
    ///         When <c>stream.CanSeek</c> is <see langword="false"/> (for example a network
    ///         stream), this method falls back to the same unconditional buffering
    ///         <see cref="Load(Stream)"/> already performs: the entire stream is read into memory
    ///         via <see cref="ReadAllBytes"/>, and the resulting <see cref="byte"/> array is
    ///         wrapped in a <see cref="ByteArrayTiffDataSource"/> - the exact same
    ///         <see cref="ITiffDataSource"/> implementation <see cref="Load(Stream)"/> already
    ///         uses - so a TIFF's Image File Directory can be located anywhere in the buffered
    ///         file regardless of the source stream's seekability. This trades a cheap,
    ///         partial-read probe for the same full-buffering cost <see cref="Load(Stream)"/>
    ///         already pays on the same bytes - a caller with a very large, genuinely
    ///         non-seekable source that wants to avoid this cost can still seek-enable it (for
    ///         example by copying into a <see cref="MemoryStream"/> first), but is no longer
    ///         required to.
    ///     </para>
    /// </remarks>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Non-seekable streams (for example a network stream) cannot use the direct seek-based
        // fast path below, since a TIFF's IFD can legitimately be located anywhere in the file.
        // Buffer the whole stream into memory instead - exactly as Load(Stream) already does
        // unconditionally - and probe the buffered bytes through the same validating parser, so
        // GetInfo never throws merely because its input happens to be non-seekable.
        if (!stream.CanSeek)
        {
            var file = ReadAllBytes(stream);
            ITiffDataSource bufferedSource = new ByteArrayTiffDataSource(file);
            var (bufferedBigEndian, bufferedIfdOffset) = ParseTiffHeader(file);
            var bufferedTags = ParseIfd(bufferedSource, bufferedIfdOffset, bufferedBigEndian);
            var bufferedInfo = ReadTiffImageInfo(bufferedSource, bufferedTags, bufferedBigEndian, enforceMaxDimension: false);
            var bufferedHasAlpha = bufferedInfo is { Photometric: PhotometricRgb, SamplesPerPixel: 4 };
            return new ImageInfo(bufferedInfo.Width, bufferedInfo.Height, bufferedInfo.SamplesPerPixel, bufferedHasAlpha);
        }

        // Captured before any bytes are read so that every subsequent absolute-position
        // read/seek performed by StreamTiffDataSource can be expressed relative to wherever the
        // caller's stream happened to be positioned when GetInfo was invoked, rather than
        // relative to absolute byte 0 of the underlying stream.
        var basePosition = stream.Position;

        var headerBytes = ReadStreamHeaderBytes(stream);
        var (bigEndian, ifdOffset) = ParseTiffHeader(headerBytes);

        // Seek directly to the IFD via the shared parser, reading only the header, IFD entries,
        // and any out-of-line tag value actually needed
        ITiffDataSource source = new StreamTiffDataSource(stream, basePosition);
        var tags = ParseIfd(source, ifdOffset, bigEndian);
        var info = ReadTiffImageInfo(source, tags, bigEndian, enforceMaxDimension: false);
        var hasAlpha = info is { Photometric: PhotometricRgb, SamplesPerPixel: 4 };
        return new ImageInfo(info.Width, info.Height, info.SamplesPerPixel, hasAlpha);
    }

    /// <summary>
    ///     Reads a TIFF file's header at the specified path and reports its declared dimensions
    ///     and pixel format, without decoding any strip/pixel data.
    /// </summary>
    /// <param name="path">The path of the TIFF file to inspect. Must not be null or empty.</param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file; see <see cref="GetInfo(Stream)"/> for
    ///     the reporting contract.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown for the same malformed/unsupported-format conditions as <see cref="GetInfo(Stream)"/>.
    /// </exception>
    /// <remarks>
    ///     File-system exceptions (for example <see cref="FileNotFoundException"/>,
    ///     <see cref="DirectoryNotFoundException"/>, <see cref="UnauthorizedAccessException"/>,
    ///     or <see cref="IOException"/>) raised while opening <paramref name="path"/> propagate
    ///     uncaught to the caller. A <see cref="FileStream"/> is always seekable, so
    ///     <see cref="GetInfo(string)"/> always uses the seek-based probe path.
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
    ///     Saves a <see cref="Surface"/> to a stream as an 8-bit RGBA TIFF image.
    /// </summary>
    /// <param name="surface">The pixel buffer to save. Must not be null.</param>
    /// <param name="stream">The stream to write the TIFF image to. Must not be null.</param>
    /// <param name="compression">
    ///     The compression method to write. Defaults to <see cref="TiffCompression.None"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="stream"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="compression"/> is not a defined <see cref="TiffCompression"/> value.
    /// </exception>
    /// <remarks>
    ///     Always writes a little-endian (<c>"II"</c>) file containing a single Image File
    ///     Directory describing an 8-bit-per-sample, 4-samples-per-pixel (RGBA) RGB image
    ///     (photometric interpretation 2, with an <c>ExtraSamples</c> tag value of 2 marking the
    ///     4th sample as unassociated alpha), Chunky planar configuration, and the entire image
    ///     stored as a single strip. When <paramref name="compression"/> is
    ///     <see cref="TiffCompression.Lzw"/> or <see cref="TiffCompression.Deflate"/>, the
    ///     horizontal-differencing predictor (Predictor tag value 2) is applied automatically
    ///     before compressing, and the Predictor tag is written accordingly; the Predictor tag is
    ///     omitted for <see cref="TiffCompression.None"/> and <see cref="TiffCompression.PackBits"/>.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     using var stream = new MemoryStream();
    ///     var surface = new Surface(2, 2);
    ///     surface[0, 0] = new Rgba32(0, 255, 0, 128); // semi-transparent green pixel
    ///
    ///     TiffCodec.Save(surface, stream, TiffCompression.Lzw); // compress with LZW
    ///     stream.Position = 0;
    ///     var loaded = TiffCodec.Load(stream);
    ///     Console.WriteLine(loaded[0, 0].A); // Output: 128
    ///     </code>
    /// </example>
    public static void Save(Surface surface, Stream stream, TiffCompression compression = TiffCompression.None)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(stream);
        if (!Enum.IsDefined<TiffCompression>(compression))
        {
            throw new ArgumentOutOfRangeException(
                nameof(compression), compression, "Compression must be a defined TiffCompression value.");
        }

        var width = surface.Width;
        var height = surface.Height;
        var rowBytes = width * SavedSamplesPerPixel;

        var raw = new byte[(long)rowBytes * height];
        for (var y = 0; y < height; y++)
        {
            surface.GetRowSpanBytes(y).CopyTo(raw.AsSpan(y * rowBytes, rowBytes));
        }

        var usePredictor = compression is TiffCompression.Lzw or TiffCompression.Deflate;
        if (usePredictor)
        {
            for (var y = 0; y < height; y++)
            {
                ApplyHorizontalPredictor(raw.AsSpan(y * rowBytes, rowBytes), SavedSamplesPerPixel);
            }
        }

        var stripData = compression switch
        {
            TiffCompression.None => raw,
            TiffCompression.PackBits => EncodePackBits(raw),
            TiffCompression.Lzw => EncodeLzw(raw),
            TiffCompression.Deflate => ZlibCompress(raw),
            _ => throw new ArgumentOutOfRangeException(
                nameof(compression), compression, "Compression must be a defined TiffCompression value.")
        };

        WriteTiffFile(stream, width, height, compression, usePredictor, stripData);
    }

    /// <summary>
    ///     Saves a <see cref="Surface"/> to a file as an 8-bit RGBA TIFF image.
    /// </summary>
    /// <param name="surface">The pixel buffer to save. Must not be null.</param>
    /// <param name="path">The destination file path. Must not be null or empty.</param>
    /// <param name="compression">
    ///     The compression method to write; see <see cref="Save(Surface, Stream, TiffCompression)"/>
    ///     for the default and its rationale.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="path"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="compression"/> is not a defined <see cref="TiffCompression"/> value.
    /// </exception>
    /// <remarks>
    ///     Any existing file at <paramref name="path"/> is overwritten. File-system exceptions
    ///     (for example <see cref="UnauthorizedAccessException"/>, <see cref="DirectoryNotFoundException"/>,
    ///     or <see cref="IOException"/>) raised while creating <paramref name="path"/> propagate
    ///     uncaught to the caller. See <see cref="Save(Surface, Stream, TiffCompression)"/> for the
    ///     written file's exact contents.
    /// </remarks>
    public static void Save(Surface surface, string path, TiffCompression compression = TiffCompression.None)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        Save(surface, stream, compression);
    }
}
