using System.IO.Compression;
using CanvasNet.Canvas;

namespace CanvasNet.Codecs;

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
public static class TiffCodec
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

    /// <summary>
    ///     The maximum total number of bytes <see cref="GetInfo(Stream)"/>'s non-seekable
    ///     fallback will buffer from a stream before giving up. Bounds the memory a hostile, or
    ///     merely oversized, non-seekable stream (for example a network stream) can force this
    ///     "cheap pre-decode bomb triage" API to allocate - without this cap, buffering ran to the
    ///     stream's end unconditionally, so a stream that is very large (or never ends) could
    ///     force an unbounded allocation before any header byte was even inspected. Mirrors the
    ///     equivalent <c>JpegCodec.MaxProbeHeaderBytes</c> cap; real TIFF headers, IFDs, and the
    ///     out-of-line tag values <see cref="ReadTiffImageInfo"/> resolves (bounded further by
    ///     <see cref="MaxImageLevelTagCount"/>) are always far smaller than this.
    /// </summary>
    private const int MaxNonSeekableProbeBytes = 1_048_576;

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
    ///     Both a seekable and a non-seekable <paramref name="stream"/> resolve every tag through
    ///     the exact same validating parser <see cref="Load(Stream)"/> itself uses
    ///     (<see cref="ParseIfd"/> and <see cref="ReadTiffImageInfo"/>, with
    ///     <c>enforceMaxDimension: false</c>), so the two cases are guaranteed identical by
    ///     construction rather than by two independently maintained implementations - see the
    ///     <see cref="ITiffDataSource"/> remarks for how random-access reads are abstracted over
    ///     each stream kind. Only the source of those reads differs:
    ///     <para>
    ///         When <c>stream.CanSeek</c> is <see langword="true"/> (the common case), a
    ///         <see cref="StreamTiffDataSource"/> seeks directly to each requested position and
    ///         reads only the bytes the parser actually asks for - the 8-byte header, the IFD
    ///         entry count and entries, and any out-of-line tag value the parser needs (for
    ///         example a multi-value <c>BitsPerSample</c> tag). It never reads
    ///         <c>StripOffsets</c>/<c>RowsPerStrip</c>/<c>StripByteCounts</c> or any strip/pixel
    ///         data, since <see cref="ReadTiffImageInfo"/> never resolves those tags.
    ///     </para>
    ///     <para>
    ///         When <c>stream.CanSeek</c> is <see langword="false"/> (for example a network
    ///         stream), no seek-based reads are possible, so this method falls back to buffering
    ///         up to <see cref="MaxNonSeekableProbeBytes"/> bytes of the stream and backing the
    ///         same parser with a <see cref="ByteArrayTiffDataSource"/> instead, stopping short of
    ///         <see cref="DecodeStrips"/>. This bounded buffering is an intentional divergence
    ///         from the seekable path for one specific case: a non-seekable stream whose IFD or a
    ///         needed tag value lies beyond <see cref="MaxNonSeekableProbeBytes"/> throws
    ///         <see cref="System.IO.InvalidDataException"/>, where an equivalent seekable stream
    ///         would succeed - a deliberate trade-off so this "cheap pre-decode bomb triage" API
    ///         cannot be forced to buffer an unbounded amount of memory for a non-seekable stream.
    ///     </para>
    /// </remarks>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Captured before any bytes are read so that, for a seekable stream, every subsequent
        // absolute-position read/seek performed by StreamTiffDataSource can be expressed relative
        // to wherever the caller's stream happened to be positioned when GetInfo was invoked,
        // rather than relative to absolute byte 0 of the underlying stream.
        var basePosition = stream.CanSeek ? stream.Position : 0L;

        var headerBytes = ReadStreamHeaderBytes(stream);
        var (bigEndian, ifdOffset) = ParseTiffHeader(headerBytes);

        if (stream.CanSeek)
        {
            // Seekable fast path: back the shared parser directly with the stream, so only the
            // header, IFD entries, and any out-of-line tag value actually needed are ever read
            ITiffDataSource seekableSource = new StreamTiffDataSource(stream, basePosition);
            var seekableTags = ParseIfd(seekableSource, ifdOffset, bigEndian);
            var seekableInfo = ReadTiffImageInfo(seekableSource, seekableTags, bigEndian, enforceMaxDimension: false);
            var seekableHasAlpha = seekableInfo is { Photometric: PhotometricRgb, SamplesPerPixel: 4 };
            return new ImageInfo(seekableInfo.Width, seekableInfo.Height, seekableInfo.SamplesPerPixel, seekableHasAlpha);
        }

        // Non-seekable fallback: no random access is possible, so buffer the stream (prefixing
        // the 8 header bytes already consumed above) up to MaxNonSeekableProbeBytes total and
        // back the same shared parser with those buffered bytes instead, stopping short of
        // DecodeStrips. The cap bounds the memory a hostile or merely oversized non-seekable
        // stream can force this "cheap pre-decode bomb triage" API to allocate; if the IFD or a
        // tag value it needs lies beyond the cap, ByteArrayTiffDataSource.ReadBytes rejects the
        // out-of-range request with the usual InvalidDataException rather than buffering further.
        using var buffered = new MemoryStream();
        buffered.Write(headerBytes, 0, headerBytes.Length);
        ReadStreamBounded(stream, buffered, MaxNonSeekableProbeBytes - headerBytes.Length);
        var file = buffered.ToArray();

        ITiffDataSource bufferedSource = new ByteArrayTiffDataSource(file);
        var tags = ParseIfd(bufferedSource, ifdOffset, bigEndian);
        var info = ReadTiffImageInfo(bufferedSource, tags, bigEndian, enforceMaxDimension: false);
        var fallbackHasAlpha = info is { Photometric: PhotometricRgb, SamplesPerPixel: 4 };
        return new ImageInfo(info.Width, info.Height, info.SamplesPerPixel, fallbackHasAlpha);
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
    ///     Reads exactly <paramref name="buffer"/>'s length worth of bytes from <paramref name="stream"/>,
    ///     tolerating short reads by looping, and throwing <see cref="InvalidDataException"/> if
    ///     the stream ends before the buffer is filled.
    /// </summary>
    private static void ReadStreamExactly(Stream stream, byte[] buffer, string what)
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
    ///     Reads from <paramref name="stream"/> into <paramref name="destination"/> until either
    ///     the stream ends or <paramref name="maxBytes"/> additional bytes have been read,
    ///     whichever comes first. Unlike <see cref="Stream.CopyTo(Stream)"/>, this never buffers
    ///     more than <paramref name="maxBytes"/> bytes, so a stream that is larger than any real
    ///     TIFF header/IFD needs to be (or never ends) cannot force unbounded memory use.
    /// </summary>
    private static void ReadStreamBounded(Stream stream, MemoryStream destination, int maxBytes)
    {
        if (maxBytes <= 0)
        {
            return;
        }

        var chunk = new byte[Math.Min(81920, maxBytes)];
        var remaining = maxBytes;
        while (remaining > 0)
        {
            var read = stream.Read(chunk, 0, Math.Min(chunk.Length, remaining));
            if (read == 0)
            {
                break;
            }

            destination.Write(chunk, 0, read);
            remaining -= read;
        }
    }

    /// <summary>
    ///     Reads exactly the 8-byte TIFF file header from <paramref name="stream"/>, without
    ///     interpreting it (see <see cref="ParseTiffHeader"/> for that).
    /// </summary>
    private static byte[] ReadStreamHeaderBytes(Stream stream)
    {
        var headerBytes = new byte[8];
        ReadStreamExactly(stream, headerBytes, "TIFF header");
        return headerBytes;
    }

    /// <summary>
    ///     Abstracts "read N bytes at an absolute file position" over either a fully-buffered
    ///     <c>byte[]</c> or a seekable <see cref="Stream"/>, so that <see cref="ParseIfd"/>,
    ///     <see cref="ReadTagValues"/>, <see cref="RequireTagValues"/>,
    ///     <see cref="TryGetTagValues"/>, and <see cref="ReadTiffImageInfo"/> can be written once
    ///     against this interface and reused, byte-for-byte identically, by both
    ///     <see cref="Load(Stream)"/> (always byte-array-backed) and <see cref="GetInfo(Stream)"/>
    ///     (byte-array-backed for a non-seekable stream, stream-backed for a seekable one).
    /// </summary>
    /// <remarks>
    ///     This abstraction exists to eliminate a correctness divergence that previously existed
    ///     between <see cref="GetInfo(Stream)"/>'s seekable and non-seekable code paths: before
    ///     its introduction, the seekable path was a separate, hand-written subset scan
    ///     (<c>ProbeIfdEntriesSeekable</c>) that defaulted <c>SamplesPerPixel</c> to 1 and skipped
    ///     most of <see cref="ReadTiffImageInfo"/>'s format-support validation, while the
    ///     non-seekable path reused <see cref="ReadTiffImageInfo"/> directly - so the same file
    ///     bytes could report a different channel count, or throw only on one path, depending
    ///     solely on whether the caller's stream happened to be seekable. Routing both paths
    ///     through the same validating parser, differing only in which <see cref="ITiffDataSource"/>
    ///     backs the reads, makes that divergence structurally impossible rather than merely
    ///     patched for the one symptom that was reported.
    /// </remarks>
    private interface ITiffDataSource
    {
        /// <summary>
        ///     Reads exactly <paramref name="length"/> bytes starting at absolute position
        ///     <paramref name="position"/>, throwing <see cref="InvalidDataException"/> if the
        ///     requested range extends past the available data.
        /// </summary>
        /// <param name="position">The absolute byte position to read from.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <param name="what">
        ///     A short description of what is being read, used only to produce a descriptive
        ///     <see cref="InvalidDataException"/> message.
        /// </param>
        byte[] ReadBytes(int position, int length, string what);
    }

    /// <summary>
    ///     An <see cref="ITiffDataSource"/> backed by a fully-buffered <c>byte[]</c>, used by
    ///     <see cref="Load(Stream)"/> (always) and by <see cref="GetInfo(Stream)"/>'s
    ///     non-seekable fallback (after buffering the whole stream). Preserves the exact bounds
    ///     checking (<see cref="CheckBounds"/>) this codec has always performed for byte-array
    ///     access.
    /// </summary>
    private sealed class ByteArrayTiffDataSource(byte[] file) : ITiffDataSource
    {
        public byte[] ReadBytes(int position, int length, string what)
        {
            CheckBounds(file, position, length, what);
            return file.AsSpan(position, length).ToArray();
        }
    }

    /// <summary>
    ///     An <see cref="ITiffDataSource"/> backed directly by a seekable <see cref="Stream"/>,
    ///     used only by <see cref="GetInfo(Stream)"/>'s seekable fast path. Seeks to each
    ///     requested absolute position and reads exactly the requested number of bytes, never
    ///     buffering more of the stream than the parser actually asks for - in practice, only the
    ///     IFD entry count, the IFD entries themselves, and any out-of-line tag value that
    ///     <see cref="ReadTiffImageInfo"/> needs (for example a multi-value <c>BitsPerSample</c>
    ///     tag); strip/pixel data is never requested, since <see cref="ReadTiffImageInfo"/> never
    ///     resolves <c>StripOffsets</c>/<c>RowsPerStrip</c>/<c>StripByteCounts</c>. Validates every
    ///     requested range against <see cref="Stream.Length"/> before allocating a buffer, so a
    ///     malformed file cannot force a large allocation via a bogus out-of-line tag length.
    /// </summary>
    /// <param name="stream">The seekable stream to read from.</param>
    /// <param name="basePosition">
    ///     The absolute position <paramref name="stream"/> was at when <see cref="GetInfo(Stream)"/>
    ///     was invoked. All TIFF-file-relative positions the parser requests (the IFD offset, IFD
    ///     entry positions, and out-of-line tag value offsets - all of which are relative to the
    ///     start of the TIFF file, not necessarily the start of the underlying stream) are added
    ///     to this base before seeking or bounds-checking, so a stream that is not already
    ///     positioned at byte 0 (for example a substream within a larger container, or a stream
    ///     the caller has already partially consumed) is read correctly rather than from the
    ///     wrong absolute location.
    /// </param>
    private sealed class StreamTiffDataSource(Stream stream, long basePosition) : ITiffDataSource
    {
        public byte[] ReadBytes(int position, int length, string what)
        {
            // Validate the requested range against the stream's actual length before
            // allocating anything. Without this check, a tiny malformed TIFF could declare
            // an out-of-line tag value array with an attacker-controlled Count in the
            // billions, forcing a huge up-front allocation on GetInfo's "cheap probing" fast
            // path - precisely the resource-exhaustion attack GetInfo exists to guard against.
            var absolutePosition = basePosition + position;
            if (position < 0 || length < 0 || absolutePosition + length > stream.Length)
            {
                throw new InvalidDataException($"Unexpected end of stream while reading {what}.");
            }

            stream.Seek(absolutePosition, SeekOrigin.Begin);
            var buffer = new byte[length];
            ReadStreamExactly(stream, buffer, what);
            return buffer;
        }
    }

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
        // Shared by Load and both GetInfo paths so a tiled TIFF (which DecodeStrips cannot
        // handle) is rejected identically everywhere, rather than only when Load is used.
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

    /// <summary>
    ///     Writes a complete little-endian TIFF file: the 8-byte header, a single Image File
    ///     Directory describing an 8-bit RGBA Chunky single-strip image, the externally stored
    ///     <c>BitsPerSample</c> array, and the strip data itself.
    /// </summary>
    private static void WriteTiffFile(
        Stream stream,
        int width,
        int height,
        TiffCompression compression,
        bool usePredictor,
        byte[] stripData)
    {
        var entryCount = usePredictor ? 12 : 11;
        var ifdSize = 2 + (entryCount * 12) + 4;
        var bitsPerSampleOffset = 8 + ifdSize;
        var bitsPerSampleSize = SavedSamplesPerPixel * 2;
        var stripDataOffset = bitsPerSampleOffset + bitsPerSampleSize;

        var header = new byte[8];
        header[0] = (byte)'I';
        header[1] = (byte)'I';
        WriteUInt16Le(header, 2, 42);
        WriteUInt32Le(header, 4, 8);
        stream.Write(header, 0, header.Length);

        var ifd = new byte[ifdSize];
        WriteUInt16Le(ifd, 0, (ushort)entryCount);
        var pos = 2;
        WriteIfdEntry(ifd, ref pos, TagImageWidth, TypeLong, 1, (uint)width);
        WriteIfdEntry(ifd, ref pos, TagImageLength, TypeLong, 1, (uint)height);
        WriteIfdEntry(ifd, ref pos, TagBitsPerSample, TypeShort, SavedSamplesPerPixel, (uint)bitsPerSampleOffset);
        WriteIfdEntry(ifd, ref pos, TagCompression, TypeShort, 1, (uint)compression);
        WriteIfdEntry(ifd, ref pos, TagPhotometricInterpretation, TypeShort, 1, PhotometricRgb);
        WriteIfdEntry(ifd, ref pos, TagStripOffsets, TypeLong, 1, (uint)stripDataOffset);
        WriteIfdEntry(ifd, ref pos, TagSamplesPerPixel, TypeShort, 1, SavedSamplesPerPixel);
        WriteIfdEntry(ifd, ref pos, TagRowsPerStrip, TypeLong, 1, (uint)height);
        WriteIfdEntry(ifd, ref pos, TagStripByteCounts, TypeLong, 1, (uint)stripData.Length);
        WriteIfdEntry(ifd, ref pos, TagPlanarConfiguration, TypeShort, 1, PlanarChunky);
        if (usePredictor)
        {
            WriteIfdEntry(ifd, ref pos, TagPredictor, TypeShort, 1, PredictorHorizontal);
        }

        WriteIfdEntry(ifd, ref pos, TagExtraSamples, TypeShort, 1, ExtraSamplesUnassociatedAlpha);
        WriteUInt32Le(ifd, pos, 0); // No next IFD (single page)
        stream.Write(ifd, 0, ifd.Length);

        var bitsPerSample = new byte[bitsPerSampleSize];
        for (var i = 0; i < SavedSamplesPerPixel; i++)
        {
            WriteUInt16Le(bitsPerSample, i * 2, 8);
        }

        stream.Write(bitsPerSample, 0, bitsPerSample.Length);

        stream.Write(stripData, 0, stripData.Length);
    }

    /// <summary>
    ///     Writes one 12-byte IFD entry (tag, type, count, and inline value or offset) at
    ///     <paramref name="pos"/>, then advances <paramref name="pos"/> past it.
    /// </summary>
    private static void WriteIfdEntry(byte[] buffer, ref int pos, ushort tag, ushort type, uint count, uint value)
    {
        WriteUInt16Le(buffer, pos, tag);
        WriteUInt16Le(buffer, pos + 2, type);
        WriteUInt32Le(buffer, pos + 4, count);
        if (type == TypeShort)
        {
            WriteUInt16Le(buffer, pos + 8, (ushort)value);
        }
        else
        {
            WriteUInt32Le(buffer, pos + 8, value);
        }

        pos += 12;
    }

    /// <summary>
    ///     Represents one parsed 12-byte TIFF IFD entry: its type, count, and the raw 4-byte
    ///     value/offset field exactly as stored in the file (interpreted later using the file's
    ///     own byte order).
    /// </summary>
    private sealed class IfdEntry
    {
        public required ushort Type { get; init; }

        public required uint Count { get; init; }

        public required byte[] ValueBytes { get; init; }
    }

    /// <summary>
    ///     Parses a TIFF Image File Directory at the given file offset into a lookup of tag number
    ///     to parsed entry. Only the first IFD is read; any subsequent IFD offset is ignored.
    /// </summary>
    private static Dictionary<ushort, IfdEntry> ParseIfd(ITiffDataSource source, uint ifdOffset, bool bigEndian)
    {
        var ifdOffsetInt = ToInt32Checked(ifdOffset, "IFD offset");
        var countBytes = source.ReadBytes(ifdOffsetInt, 2, "IFD entry count");
        var entryCount = ReadUInt16(countBytes, 0, bigEndian);

        var tags = new Dictionary<ushort, IfdEntry>();
        var pos = ToInt32Checked((long)ifdOffsetInt + 2, "IFD entries offset");
        for (var i = 0; i < entryCount; i++)
        {
            var entryBytes = source.ReadBytes(pos, 12, "IFD entry");
            var tag = ReadUInt16(entryBytes, 0, bigEndian);
            var type = ReadUInt16(entryBytes, 2, bigEndian);
            var count = ReadUInt32(entryBytes, 4, bigEndian);
            var valueBytes = entryBytes.AsSpan(8, 4).ToArray();
            tags[tag] = new IfdEntry { Type = type, Count = count, ValueBytes = valueBytes };
            pos += 12;
        }

        return tags;
    }

    /// <summary>
    ///     Resolves an IFD entry's values to an array of <see cref="uint"/>, reading them either
    ///     from the entry's inline 4-byte value field or, when they do not fit inline, from the
    ///     file offset stored in that field (fetched via <paramref name="source"/>).
    /// </summary>
    private static uint[] ReadTagValues(ITiffDataSource source, IfdEntry entry, bool bigEndian, int? maxCount = null)
    {
        if (entry.Count == 0)
        {
            throw new InvalidDataException("TIFF tag has a declared value count of 0, which is not valid.");
        }

        if (maxCount is { } cap && entry.Count > cap)
        {
            throw new InvalidDataException(
                $"TIFF tag has an implausibly large declared value count {entry.Count}; expected at most {cap}.");
        }

        var typeSize = entry.Type switch
        {
            TypeByte => 1,
            TypeShort => 2,
            TypeLong => 4,
            _ => throw new InvalidDataException($"Unsupported TIFF tag value type {entry.Type}.")
        };

        var totalSize = (long)typeSize * entry.Count;
        var valueBuffer = totalSize <= 4
            ? entry.ValueBytes
            : source.ReadBytes(
                ToInt32Checked(ReadUInt32(entry.ValueBytes, 0, bigEndian), "tag value offset"),
                ToInt32Checked(totalSize, "tag value array length"),
                "tag value array");

        var result = new uint[entry.Count];
        for (var i = 0; i < entry.Count; i++)
        {
            result[i] = entry.Type switch
            {
                TypeByte => valueBuffer[i],
                TypeShort => ReadUInt16(valueBuffer, i * 2, bigEndian),
                _ => ReadUInt32(valueBuffer, i * 4, bigEndian)
            };
        }

        return result;
    }

    /// <summary>
    ///     Converts a non-negative <see cref="long"/> to an <see cref="int"/>, throwing
    ///     <see cref="InvalidDataException"/> (rather than letting an <see cref="OverflowException"/>
    ///     escape) when the value is negative or exceeds <see cref="int.MaxValue"/>. Used for every
    ///     file-position/length value derived from attacker-controlled TIFF header/tag fields.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="what">A short description of the value, used in the error message.</param>
    private static int ToInt32Checked(long value, string what)
    {
        if (value < 0 || value > int.MaxValue)
        {
            throw new InvalidDataException($"TIFF {what} value {value} exceeds the supported range.");
        }

        return (int)value;
    }

    /// <summary>
    ///     Resolves a mandatory tag's values, throwing <see cref="InvalidDataException"/> if the
    ///     tag is not present in the directory.
    /// </summary>
    /// <param name="source">The abstracted, random-access-capable TIFF byte source.</param>
    /// <param name="tags">The parsed IFD tag lookup.</param>
    /// <param name="tag">The TIFF tag number to resolve.</param>
    /// <param name="tagName">The tag's human-readable name, used in the "missing tag" error message.</param>
    /// <param name="bigEndian">The file's detected byte order.</param>
    /// <param name="maxCount">
    ///     When non-null, the maximum declared value <c>Count</c> allowed for this tag; a larger
    ///     declared count is rejected with <see cref="InvalidDataException"/> before any
    ///     count-proportional allocation or read is performed.
    /// </param>
    private static uint[] RequireTagValues(
        ITiffDataSource source, Dictionary<ushort, IfdEntry> tags, ushort tag, string tagName, bool bigEndian, int? maxCount = null)
    {
        if (!tags.TryGetValue(tag, out var entry))
        {
            throw new InvalidDataException($"Missing mandatory TIFF tag {tagName}.");
        }

        return ReadTagValues(source, entry, bigEndian, maxCount);
    }

    /// <summary>
    ///     Resolves an optional tag's values, returning null if the tag is not present in the
    ///     directory.
    /// </summary>
    /// <param name="source">The abstracted, random-access-capable TIFF byte source.</param>
    /// <param name="tags">The parsed IFD tag lookup.</param>
    /// <param name="tag">The TIFF tag number to resolve.</param>
    /// <param name="bigEndian">The file's detected byte order.</param>
    /// <param name="maxCount">
    ///     When non-null, the maximum declared value <c>Count</c> allowed for this tag; see
    ///     <see cref="RequireTagValues"/>.
    /// </param>
    private static uint[]? TryGetTagValues(
        ITiffDataSource source, Dictionary<ushort, IfdEntry> tags, ushort tag, bool bigEndian, int? maxCount = null) =>
        tags.TryGetValue(tag, out var entry) ? ReadTagValues(source, entry, bigEndian, maxCount) : null;

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

    /// <summary>
    ///     Reverses the TIFF horizontal-differencing predictor (Predictor tag value 2) on one
    ///     already-decompressed row, in place: each sample becomes the running sum of itself and
    ///     the sample <paramref name="samplesPerPixel"/> positions to its left in the same row.
    /// </summary>
    private static void RemoveHorizontalPredictor(Span<byte> row, int samplesPerPixel)
    {
        for (var i = samplesPerPixel; i < row.Length; i++)
        {
            row[i] = (byte)(row[i] + row[i - samplesPerPixel]);
        }
    }

    /// <summary>
    ///     Applies the TIFF horizontal-differencing predictor (Predictor tag value 2) to one row,
    ///     in place, before compression: each sample becomes the difference between itself and the
    ///     sample <paramref name="samplesPerPixel"/> positions to its left in the same row.
    /// </summary>
    private static void ApplyHorizontalPredictor(Span<byte> row, int samplesPerPixel)
    {
        for (var i = row.Length - 1; i >= samplesPerPixel; i--)
        {
            row[i] = (byte)(row[i] - row[i - samplesPerPixel]);
        }
    }

    /// <summary>
    ///     Decodes a PackBits (TIFF 6.0 Section 9) run-length encoded byte stream.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when a literal or repeat run's data extends past the end of <paramref name="data"/>.
    /// </exception>
    private static byte[] DecodePackBits(byte[] data)
    {
        using var output = new MemoryStream();
        var i = 0;
        while (i < data.Length)
        {
            var control = unchecked((sbyte)data[i]);
            i++;
            if (control >= 0)
            {
                var count = control + 1;
                if (i + count > data.Length)
                {
                    throw new InvalidDataException("Truncated PackBits literal run.");
                }

                output.Write(data, i, count);
                i += count;
            }
            else if (control != -128)
            {
                var count = -control + 1;
                if (i >= data.Length)
                {
                    throw new InvalidDataException("Truncated PackBits repeat run.");
                }

                var value = data[i];
                i++;
                for (var j = 0; j < count; j++)
                {
                    output.WriteByte(value);
                }
            }

            // control == -128 is a documented no-op and is simply skipped
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Encodes a byte stream using PackBits (TIFF 6.0 Section 9) run-length encoding,
    ///     preferring a repeat run for any sequence of 2 or more identical bytes and otherwise
    ///     emitting a literal run.
    /// </summary>
    private static byte[] EncodePackBits(byte[] data)
    {
        using var output = new MemoryStream();
        var i = 0;
        while (i < data.Length)
        {
            var runLength = FindPackBitsRunLength(data, i);

            if (runLength >= 2)
            {
                output.WriteByte(unchecked((byte)-(runLength - 1)));
                output.WriteByte(data[i]);
                i += runLength;
            }
            else
            {
                var start = i;
                var length = FindPackBitsLiteralLength(data, ref i);
                output.WriteByte((byte)(length - 1));
                output.Write(data, start, length);
            }
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Counts how many bytes starting at <paramref name="start"/> repeat the value at
    ///     <paramref name="start"/>, capped at the PackBits maximum run length of 128.
    /// </summary>
    private static int FindPackBitsRunLength(byte[] data, int start)
    {
        var runLength = 1;
        while (start + runLength < data.Length && runLength < 128 && data[start + runLength] == data[start])
        {
            runLength++;
        }

        return runLength;
    }

    /// <summary>
    ///     Advances <paramref name="i"/> past a run of non-repeating ("literal") bytes, stopping
    ///     as soon as a repeat run of 2 or more is found or the PackBits maximum literal length of
    ///     128 is reached, and returns the number of literal bytes found.
    /// </summary>
    private static int FindPackBitsLiteralLength(byte[] data, ref int i)
    {
        var length = 0;
        while (i < data.Length && length < 128)
        {
            var lookaheadRun = FindPackBitsRunLength(data, i);
            if (lookaheadRun >= 2)
            {
                break;
            }

            i++;
            length++;
        }

        return length;
    }

    /// <summary>
    ///     Encodes a byte stream using the TIFF-flavor LZW algorithm (TIFF 6.0 Section 13):
    ///     variable-width 9-12 bit codes, MSB-first bit packing, clear code 256, end-of-information
    ///     code 257.
    /// </summary>
    private static byte[] EncodeLzw(byte[] data)
    {
        var writer = new LzwBitWriter();
        var codeSize = 9;
        var nextCode = LzwFirstCode;
        var table = new Dictionary<(int Prefix, byte Next), int>();

        writer.WriteCode(LzwClearCode, codeSize);

        if (data.Length == 0)
        {
            writer.WriteCode(LzwEoiCode, codeSize);
            return writer.ToArray();
        }

        var prefixCode = (int)data[0];
        for (var i = 1; i < data.Length; i++)
        {
            var next = data[i];
            if (table.TryGetValue((prefixCode, next), out var existingCode))
            {
                prefixCode = existingCode;
                continue;
            }

            writer.WriteCode(prefixCode, codeSize);
            table[(prefixCode, next)] = nextCode;
            nextCode++;

            if (nextCode is 511 or 1023 or 2047)
            {
                codeSize++;
            }

            if (nextCode >= LzwMaxCode)
            {
                writer.WriteCode(LzwClearCode, codeSize);
                table.Clear();
                nextCode = LzwFirstCode;
                codeSize = 9;
            }

            prefixCode = next;
        }

        writer.WriteCode(prefixCode, codeSize);
        writer.WriteCode(LzwEoiCode, codeSize);
        return writer.ToArray();
    }

    /// <summary>
    ///     Decodes a TIFF-flavor LZW (TIFF 6.0 Section 13) byte stream, mirroring the table
    ///     construction of <see cref="EncodeLzw"/> exactly (variable-width 9-12 bit codes,
    ///     MSB-first bit packing, clear code 256, end-of-information code 257).
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not begin with a Clear code, an invalid code is
    ///     encountered, or the stream ends before an end-of-information code is read.
    /// </exception>
    private static byte[] DecodeLzw(byte[] data)
    {
        var reader = new LzwBitReader(data);
        using var output = new MemoryStream();
        var codeSize = 9;
        var table = new List<byte[]>();
        byte[]? previousEntry = null;

        var firstCode = reader.ReadCode(codeSize);
        if (firstCode != LzwClearCode)
        {
            throw new InvalidDataException("TIFF LZW stream does not start with a Clear code.");
        }

        while (true)
        {
            var code = reader.ReadCode(codeSize);
            if (code == LzwEoiCode)
            {
                break;
            }

            if (code == LzwClearCode)
            {
                table.Clear();
                codeSize = 9;
                previousEntry = null;
                continue;
            }

            var entry = ResolveLzwEntry(code, table, previousEntry);
            output.Write(entry, 0, entry.Length);

            if (previousEntry is not null)
            {
                codeSize = AddLzwTableEntry(table, previousEntry, entry, codeSize);
            }

            previousEntry = entry;
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Resolves the byte sequence for a single decoded LZW <paramref name="code"/>: a literal
    ///     byte value for codes below 256, an existing table entry for already-known codes, or
    ///     the classic LZW "KwKwK" reconstruction (previous entry plus its own first byte) for
    ///     the one code that is always exactly one past the current table end.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">Thrown when <paramref name="code"/> is invalid.</exception>
    private static byte[] ResolveLzwEntry(int code, List<byte[]> table, byte[]? previousEntry)
    {
        if (code < 256)
        {
            return [(byte)code];
        }

        if (code - LzwFirstCode < table.Count)
        {
            return table[code - LzwFirstCode];
        }

        if (code - LzwFirstCode == table.Count && previousEntry is not null)
        {
            var entry = new byte[previousEntry.Length + 1];
            previousEntry.CopyTo(entry, 0);
            entry[^1] = previousEntry[0];
            return entry;
        }

        throw new InvalidDataException("Invalid TIFF LZW code sequence.");
    }

    /// <summary>
    ///     Appends a new table entry formed from <paramref name="previousEntry"/> plus the first
    ///     byte of <paramref name="entry"/>, then widens <paramref name="codeSize"/> if the table
    ///     has just grown past a code-width boundary, returning the (possibly updated) code size.
    /// </summary>
    private static int AddLzwTableEntry(List<byte[]> table, byte[] previousEntry, byte[] entry, int codeSize)
    {
        var newEntry = new byte[previousEntry.Length + 1];
        previousEntry.CopyTo(newEntry, 0);
        newEntry[^1] = entry[0];
        table.Add(newEntry);

        var nextCode = LzwFirstCode + table.Count;
        if (nextCode is 511 or 1023 or 2047)
        {
            codeSize++;
        }

        return codeSize;
    }

    /// <summary>
    ///     Packs variable-width (9-12 bit) LZW codes into bytes MSB-first, as required by the
    ///     TIFF specification (the reverse bit order from the GIF LZW variant).
    /// </summary>
    private sealed class LzwBitWriter
    {
        private readonly List<byte> _bytes = [];
        private ulong _bitBuffer;
        private int _bitCount;

        public void WriteCode(int code, int bits)
        {
            _bitBuffer = (_bitBuffer << bits) | (uint)(code & ((1 << bits) - 1));
            _bitCount += bits;
            while (_bitCount >= 8)
            {
                _bitCount -= 8;
                _bytes.Add((byte)((_bitBuffer >> _bitCount) & 0xFF));
            }

            _bitBuffer &= (1UL << _bitCount) - 1;
        }

        public byte[] ToArray()
        {
            if (_bitCount > 0)
            {
                var pad = 8 - _bitCount;
                _bytes.Add((byte)((_bitBuffer << pad) & 0xFF));
            }

            return [.. _bytes];
        }
    }

    /// <summary>
    ///     Unpacks variable-width (9-12 bit) LZW codes from bytes packed MSB-first, as required by
    ///     the TIFF specification.
    /// </summary>
    private sealed class LzwBitReader(byte[] data)
    {
        private int _bytePos;
        private ulong _bitBuffer;
        private int _bitCount;

        public int ReadCode(int bits)
        {
            while (_bitCount < bits)
            {
                if (_bytePos >= data.Length)
                {
                    throw new InvalidDataException("Truncated TIFF LZW stream (missing end-of-information code).");
                }

                _bitBuffer = (_bitBuffer << 8) | data[_bytePos];
                _bytePos++;
                _bitCount += 8;
            }

            _bitCount -= bits;
            return (int)((_bitBuffer >> _bitCount) & ((1UL << bits) - 1));
        }
    }

    /// <summary>
    ///     Computes the Adler-32 checksum of a byte sequence, as used by the zlib stream format
    ///     wrapping Deflate-compressed TIFF strip data.
    /// </summary>
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
    ///     Compresses raw bytes into a complete zlib stream: a 2-byte zlib header,
    ///     DEFLATE-compressed data, and a 4-byte big-endian Adler-32 trailer.
    /// </summary>
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
    ///     big-endian Adler-32 trailer) into its raw bytes.
    /// </summary>
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
            throw new InvalidDataException("Zlib Adler-32 checksum mismatch (corrupt TIFF Deflate data).");
        }

        return decompressed;
    }

    /// <summary>
    ///     Reads an unsigned 16-bit integer from a byte buffer at the given offset, using the
    ///     specified byte order.
    /// </summary>
    private static ushort ReadUInt16(byte[] buffer, int offset, bool bigEndian) =>
        bigEndian
            ? (ushort)((buffer[offset] << 8) | buffer[offset + 1])
            : (ushort)(buffer[offset] | (buffer[offset + 1] << 8));

    /// <summary>
    ///     Reads an unsigned 32-bit integer from a byte buffer at the given offset, using the
    ///     specified byte order.
    /// </summary>
    private static uint ReadUInt32(byte[] buffer, int offset, bool bigEndian) =>
        bigEndian
            ? ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3]
            : buffer[offset] | ((uint)buffer[offset + 1] << 8) | ((uint)buffer[offset + 2] << 16) | ((uint)buffer[offset + 3] << 24);

    /// <summary>
    ///     Reads a big-endian, unsigned 32-bit integer from a byte buffer at the given offset (used
    ///     only by the zlib wrapper, which is always big-endian regardless of the TIFF file's own
    ///     byte order).
    /// </summary>
    private static uint ReadUInt32Be(byte[] buffer, int offset) =>
        ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];

    /// <summary>
    ///     Writes a big-endian, unsigned 32-bit integer into a byte buffer at the given offset
    ///     (used only by the zlib wrapper, which is always big-endian).
    /// </summary>
    private static void WriteUInt32Be(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    /// <summary>
    ///     Writes a little-endian, unsigned 16-bit integer into a byte buffer at the given offset
    ///     (used only by <see cref="Save(Surface, Stream, TiffCompression)"/>, which always writes
    ///     little-endian files).
    /// </summary>
    private static void WriteUInt16Le(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    /// <summary>
    ///     Writes a little-endian, unsigned 32-bit integer into a byte buffer at the given offset
    ///     (used only by <see cref="Save(Surface, Stream, TiffCompression)"/>, which always writes
    ///     little-endian files).
    /// </summary>
    private static void WriteUInt32Le(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    /// <summary>
    ///     Verifies that a byte range lies fully within a buffer, throwing
    ///     <see cref="InvalidDataException"/> otherwise (indicating a truncated stream or an
    ///     invalid offset stored in the file).
    /// </summary>
    private static void CheckBounds(byte[] file, int offset, int length, string what)
    {
        if (offset < 0 || length < 0 || offset + (long)length > file.Length)
        {
            throw new InvalidDataException($"Unexpected end of TIFF data while reading {what}.");
        }
    }

    /// <summary>
    ///     Reads an entire stream into a newly allocated byte array. TIFF's Image File Directory
    ///     offset, and any tag value not fitting inline, may point anywhere in the file, so parsing
    ///     requires random access to the whole file rather than the sequential reads used by
    ///     <see cref="BmpCodec"/> and <see cref="PngCodec"/>.
    /// </summary>
    private static byte[] ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
