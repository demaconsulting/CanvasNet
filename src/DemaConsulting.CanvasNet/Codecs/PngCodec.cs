using System.IO.Compression;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

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
///     Provides hand-rolled, dependency-free loading and saving of PNG files to and from
///     <see cref="Surface"/> pixel buffers. <see cref="Save(Surface, System.IO.Stream, PngColorType)"/>
///     writes only the two 8-bit-per-channel Truecolor variants named by <see cref="PngColorType"/>,
///     but <see cref="Load(System.IO.Stream)"/> decodes every non-interlaced, spec-valid PNG
///     color-type/bit-depth combination (grayscale, Truecolor, palette/indexed, grayscale-with-
///     alpha, and Truecolor-with-alpha, at bit depths 1, 2, 4, 8, and 16 as permitted for each
///     color type), and <see cref="GetInfo(System.IO.Stream)"/> reports declared dimensions for
///     every well-formed PNG, including Adam7-interlaced files that <c>Load</c> cannot decode.
/// </summary>
/// <remarks>
///     <c>PngCodec</c> is the fourth software unit in CanvasNet, and depends on <see cref="Surface"/>
///     exactly as <see cref="BmpCodec"/> does: it constructs and reads <see cref="Surface"/>
///     instances via the existing public <see cref="Surface.GetRowSpanBytes"/> accessor, but adds
///     no new public members to <see cref="Surface"/> itself. It is a stateless, static utility
///     class - there is nothing to construct or configure, so an instance type would add no
///     value over static methods.
///     <para>
///         <c>Load</c> decodes every color type defined by the PNG specification - grayscale (0),
///         Truecolor (2), palette/indexed (3), grayscale-with-alpha (4), and Truecolor-with-alpha
///         (6) - at every bit depth the specification permits for that color type (1, 2, 4, 8, or
///         16 for grayscale and palette at depths up to 8 only; 8 or 16 for the other three), with
///         the standard (non-interlaced) scanline order. Only two things remain hard refusals for
///         <c>Load</c> that are not themselves well-formedness defects: Adam7 interlacing
///         (interlace method 1), which this codec does not implement, and a bit-depth/color-type
///         combination that is itself invalid per the PNG specification (for example palette at
///         16-bit depth). Both are rejected with a descriptive
///         <see cref="System.IO.InvalidDataException"/> rather than silently producing incorrect
///         pixels. <c>Save</c>'s output scope is unchanged - only the two 8-bit Truecolor variants
///         named by <see cref="PngColorType"/>.
///     </para>
///     <para>
///         Design decision - refusing chunks the PNG specification itself forbids, not merely
///         chunks this codec does not implement: beyond well-formedness and decode-capability
///         refusals, <c>Load</c> also rejects several chunk combinations the PNG specification
///         declares invalid regardless of decode capability, because silently tolerating them
///         would mean accepting non-conforming files that a correct encoder never produces: an
///         unrecognized <em>critical</em> chunk (uppercase first type byte) that is not one of
///         <c>IHDR</c>/<c>PLTE</c>/<c>tRNS</c>/<c>IDAT</c>/<c>IEND</c>, since it may change how
///         pixel data must be interpreted and this codec has no logic for it; a <c>PLTE</c> chunk
///         on a grayscale or grayscale-with-alpha file, since grayscale samples are never resolved
///         through a palette; a <c>tRNS</c> chunk on a grayscale-with-alpha or Truecolor-with-alpha
///         file, since those color types already carry a full per-pixel alpha channel that leaves
///         nothing for a single-key-color transparency chunk to add; a <c>tRNS</c> chunk that
///         precedes the <c>PLTE</c> chunk on a palette file, or a <c>PLTE</c> chunk that appears
///         after a <c>tRNS</c> chunk has already been accepted (for any color type that can
///         legally carry both), since its per-palette-entry alpha values are meaningless before
///         the palette they index into has been read; a non-consecutive run of <c>IDAT</c>
///         chunks, since the specification requires every <c>IDAT</c> chunk to be consecutive;
///         and any chunk of any type (including an otherwise-safe-to-skip ancillary chunk)
///         encountered before the mandatory <c>IHDR</c> chunk, since <c>IHDR</c> is always
///         required to be first. An unrecognized <em>ancillary</em> chunk (lowercase first type
///         byte, for example <c>tEXt</c>, <c>pHYs</c>, or <c>gAMA</c>) that appears after
///         <c>IHDR</c> remains safe to skip, exactly as before.
///     </para>
///     <para>
///         Design decision - palette (color type 3) tRNS/PLTE-to-RGBA mapping: a palette-indexed
///         pixel's raw file byte is a palette index, not a sample magnitude, so it is never scaled
///         the way grayscale samples are; it is used directly to look up the pixel's RGB triple in
///         the <c>PLTE</c> chunk (mandatory for this color type) and, if present, its alpha byte in
///         the <c>tRNS</c> chunk (missing entries default to fully opaque). This is a decode-time
///         (post-<c>Load</c>) concern; see <see cref="GetInfo(System.IO.Stream)"/>'s remarks for
///         the distinct, and deliberately different, design decision about what <c>GetInfo</c>
///         reports for palette images without decoding them.
///     </para>
///     <para>
///         Design decision - sub-byte grayscale sample scaling: at bit depths 1, 2, and 4, a
///         grayscale sample is scaled to the full 0-255 output range via
///         <c>sample * 255 / ((1 &lt;&lt; bitDepth) - 1)</c> (integer division) rather than bit
///         replication (for example repeating a 1-bit sample as <c>0x00</c> or <c>0xFF</c>, or a
///         2-bit sample's top bits into its bottom bits). The two techniques are numerically
///         identical for every value at these bit depths (both map the sample's legal range evenly
///         onto 0-255), so the multiply/divide form was chosen simply because it requires no
///         per-bit-depth special-casing beyond the divisor.
///     </para>
///     <para>
///         Design decision - two independent boolean flags gate distinct concerns while parsing
///         <c>IHDR</c>: <c>enforceMaxDimension</c> (unchanged from before this decode-widening)
///         controls only whether a width/height above <see cref="Surface.MaxDimension"/> is
///         rejected, and the newer <c>validateDecodability</c> controls only whether Adam7
///         interlacing is rejected. Every other <c>IHDR</c> validation (bit depth in range, color
///         type in range, the bit-depth/color-type combination being legal per the specification,
///         compression/filter method, interlace method in range) is a well-formedness check that
///         is always enforced by both <c>GetInfo</c> and <c>Load</c>, since a file that fails one
///         of those checks is not a well-formed PNG at all, regardless of whether the caller only
///         wants its declared size. This is why <c>GetInfo</c> succeeds for every well-formed,
///         non-interlaced-or-not PNG of any legal color-type/bit-depth combination, yet still
///         rejects the same malformed inputs <c>Load</c> rejects (bad signature, wrong IHDR
///         length, non-positive dimensions, IHDR CRC-32 mismatch, out-of-range bit depth/color
///         type, or an illegal bit-depth/color-type pairing).
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
    ///     The only bit depth <see cref="Save(Surface, System.IO.Stream, PngColorType)"/> writes
    ///     (8 bits per channel). <see cref="Load(System.IO.Stream)"/> decodes bit depths 1, 2, 4,
    ///     8, and 16, as permitted per color type by the PNG specification.
    /// </summary>
    private const byte SaveBitDepth = 8;

    /// <summary>PNG color type byte for grayscale (one sample per pixel, no alpha).</summary>
    private const int ColorTypeGrayscale = 0;

    /// <summary>PNG color type byte for Truecolor (three samples per pixel, no alpha).</summary>
    private const int ColorTypeTruecolor = 2;

    /// <summary>PNG color type byte for palette/indexed (one palette-index sample per pixel).</summary>
    private const int ColorTypePalette = 3;

    /// <summary>PNG color type byte for grayscale with alpha (two samples per pixel).</summary>
    private const int ColorTypeGrayscaleAlpha = 4;

    /// <summary>PNG color type byte for Truecolor with alpha (four samples per pixel).</summary>
    private const int ColorTypeTruecolorAlpha = 6;

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
    ///     The PNG interlace method byte for Adam7 interlacing, which this codec's <c>Load</c>
    ///     does not implement (though <c>GetInfo</c> still reports dimensions for such a file).
    /// </summary>
    private const byte InterlaceAdam7 = 1;

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
    ///     The maximum number of entries a PNG <c>PLTE</c> chunk may declare (one byte's worth of
    ///     palette index values), per the PNG specification. Used both by the full post-read
    ///     <c>PLTE</c> validation and by <see cref="ValidateChunkLengthBeforeAllocation"/>'s
    ///     pre-allocation length check.
    /// </summary>
    private const int MaxPaletteEntries = 256;

    /// <summary>
    ///     The maximum byte length of a PNG <c>PLTE</c> chunk's data (<see cref="MaxPaletteEntries"/>
    ///     three-byte RGB entries), used by <see cref="ValidateChunkLengthBeforeAllocation"/> to
    ///     reject an oversized declared length before the payload buffer is allocated.
    /// </summary>
    private const int MaxPlteDataLength = MaxPaletteEntries * 3;

    /// <summary>
    ///     Loads a <see cref="Surface"/> from an open, readable stream containing a well-formed,
    ///     non-interlaced PNG image of any color type and bit depth combination the PNG
    ///     specification permits.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the PNG image from. Reading begins at the stream's current position
    ///     and consumes exactly the PNG signature, all chunks through <c>IEND</c>.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> containing the decoded pixels, always as RGBA regardless of
    ///     the source PNG's color type. Pixels decoded from a color type without an alpha channel
    ///     (grayscale, Truecolor, or palette) have alpha 255 (fully opaque) unless a <c>tRNS</c>
    ///     chunk marks specific pixels as fully transparent (alpha 0); pixels decoded from a color
    ///     type with an alpha channel (grayscale-with-alpha or Truecolor-with-alpha) retain the
    ///     alpha value stored in the file. Samples narrower than 8 bits are scaled to the full
    ///     0-255 range; 16-bit samples are downshifted to 8 bits by discarding the low byte.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not contain a valid PNG image: the 8-byte PNG
    ///     signature is missing, the <c>IHDR</c> chunk is missing, malformed, describes
    ///     non-positive or oversized (exceeding <see cref="Surface.MaxDimension"/>) dimensions,
    ///     or describes an unsupported bit depth, color type, compression method, filter method,
    ///     or interlace method; an unrecognized critical chunk (uppercase first type byte) is
    ///     encountered; a <c>PLTE</c> chunk appears on a grayscale or grayscale-with-alpha file;
    ///     a <c>tRNS</c> chunk appears on a grayscale-with-alpha or Truecolor-with-alpha file, or
    ///     precedes the <c>PLTE</c> chunk on a palette file; a <c>PLTE</c> chunk appears after a
    ///     <c>tRNS</c> chunk has already been accepted; the <c>IDAT</c> chunks are not
    ///     consecutive; any chunk appears before the mandatory <c>IHDR</c> chunk; any chunk's
    ///     CRC-32 does not match; the decompressed scanline data
    ///     has an unexpected length; an unsupported scanline filter type is encountered; or the
    ///     stream ends before all header, chunk, or pixel data has been read.
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
        ValidateSignature(stream);

        var header = ReadChunks(stream, out var idatData, out var plteData, out var trnsData);

        if (header.ColorType == ColorTypePalette && plteData == null)
        {
            throw new InvalidDataException("PNG palette color type (3) requires a PLTE chunk.");
        }

        var trns = ValidateAndNormalizeTrns(header.ColorType, plteData, trnsData);

        var samplesPerPixel = SamplesPerPixel(header.ColorType);
        var bitsPerPixel = samplesPerPixel * header.BitDepth;
        var rowBytes = (header.Width * bitsPerPixel + 7) / 8;
        var bpp = Math.Max(1, (bitsPerPixel + 7) / 8);

        var rawData = ZlibDecompress(idatData);
        var expectedRawLength = (long)(rowBytes + 1) * header.Height;
        if (rawData.LongLength != expectedRawLength)
        {
            throw new InvalidDataException(
                "PNG scanline data has an unexpected length (corrupt or truncated image data).");
        }

        return DecodeScanlines(
            rawData,
            header.Width,
            header.Height,
            header.ColorType,
            header.BitDepth,
            rowBytes,
            bpp,
            samplesPerPixel,
            plteData,
            trns);
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
    ///     Reads a PNG file's signature and <c>IHDR</c> chunk and reports its declared dimensions
    ///     and pixel format, without reading any further chunks (in particular, without reading
    ///     any <c>PLTE</c>, <c>tRNS</c>, or <c>IDAT</c> data), and without regard to whether
    ///     <see cref="Load(Stream)"/> can actually decode the file's color type, bit depth, or
    ///     interlace method.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the PNG signature and <c>IHDR</c> chunk from. Reading begins at the
    ///     stream's current position and consumes exactly the 8-byte signature plus the first
    ///     chunk frame (4-byte length + 4-byte type + 13-byte IHDR data + 4-byte CRC = 33 bytes);
    ///     the stream is left positioned immediately after the <c>IHDR</c> chunk.
    /// </param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file's declared width and height, plus the
    ///     channel count and alpha flag that decoding this file's color type would produce:
    ///     grayscale (0) reports 1 channel, no alpha; Truecolor (2) reports 3 channels, no alpha;
    ///     palette/indexed (3) reports 1 channel, no alpha - this is the raw file encoding (one
    ///     palette-index sample per pixel, packed at sub-byte bit depths), <em>not</em> the
    ///     4-channel RGBA result <c>Load</c>
    ///     produces after resolving each index through the <c>PLTE</c>/<c>tRNS</c> chunks, since
    ///     <c>GetInfo</c> deliberately never reads those chunks; grayscale-with-alpha (4) reports
    ///     2 channels, has alpha; Truecolor-with-alpha (6) reports 4 channels, has alpha.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the signature does not match, the first chunk is not <c>IHDR</c>, the
    ///     <c>IHDR</c> chunk's declared length is not exactly 13 (validated before any
    ///     length-dependent allocation, so a crafted huge declared length cannot force a large
    ///     allocation), the <c>IHDR</c> chunk's CRC-32 does not match, it describes non-positive
    ///     dimensions, or it describes a bit depth, color type, or bit-depth/color-type
    ///     combination that is not defined by the PNG specification at all. Unlike
    ///     <see cref="Load(Stream)"/>, a width or height above <see cref="Surface.MaxDimension"/>
    ///     is <em>not</em> rejected (see <see cref="ImageInfo"/> for why), and Adam7 interlacing
    ///     is <em>not</em> rejected (this is the one feature <c>Load</c> refuses that is still a
    ///     well-formed PNG). Any corruption in a subsequent chunk (including <c>PLTE</c>,
    ///     <c>tRNS</c>, <c>IDAT</c>, or <c>IEND</c>) is never encountered by <c>GetInfo</c>.
    /// </exception>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = ReadIhdrOnly(stream, enforceMaxDimension: false, validateDecodability: false);
        var (channels, hasAlpha) = header.ColorType switch
        {
            ColorTypeGrayscale => (1, false),
            ColorTypeTruecolor => (3, false),
            ColorTypePalette => (1, false),
            ColorTypeGrayscaleAlpha => (2, true),
            ColorTypeTruecolorAlpha => (4, true),
            _ => throw new InvalidDataException($"Unsupported PNG color type {header.ColorType}.")
        };
        return new ImageInfo(header.Width, header.Height, channels, hasAlpha);
    }


    /// <summary>
    ///     Reads a PNG file's header at the specified path and reports its declared dimensions
    ///     and pixel format, without reading any pixel data.
    /// </summary>
    /// <param name="path">The path of the PNG file to inspect. Must not be null or empty.</param>
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
    ///     Validates that <paramref name="stream"/> begins with the fixed 8-byte PNG signature,
    ///     consuming exactly those 8 bytes.
    /// </summary>
    private static void ValidateSignature(Stream stream)
    {
        var signature = ReadExactly(stream, Signature.Length, "PNG signature");
        for (var i = 0; i < Signature.Length; i++)
        {
            if (signature[i] != Signature[i])
            {
                throw new InvalidDataException("Not a PNG file (missing PNG signature).");
            }
        }
    }

    /// <summary>
    ///     The width, height, color type, and bit depth parsed from a PNG's <c>IHDR</c> chunk,
    ///     produced by <see cref="ReadChunks"/> once the chunk stream has been fully consumed
    ///     through <c>IEND</c> (or by <see cref="ReadIhdrOnly"/>, which never reads past IHDR).
    /// </summary>
    private readonly record struct PngHeader(int Width, int Height, int ColorType, int BitDepth);

    /// <summary>
    ///     Tracks whether the mandatory <c>IHDR</c> chunk has been seen and accumulates the
    ///     header's field values, plus the optional <c>PLTE</c> and <c>tRNS</c> chunk payloads,
    ///     as chunks are read, threaded through <see cref="ProcessChunk"/> while
    ///     <see cref="ReadChunks"/> walks the chunk stream.
    /// </summary>
    private sealed class ChunkReadState
    {
        /// <summary>Whether the mandatory IHDR chunk has been seen yet.</summary>
        public bool IhdrSeen { get; set; }

        /// <summary>Whether the mandatory IEND chunk has been seen yet.</summary>
        public bool IendSeen { get; set; }

        /// <summary>The image width in pixels, populated by the IHDR chunk.</summary>
        public int Width { get; set; }

        /// <summary>The image height in pixels, populated by the IHDR chunk.</summary>
        public int Height { get; set; }

        /// <summary>The PNG color type, populated by the IHDR chunk.</summary>
        public int ColorType { get; set; }

        /// <summary>The PNG bit depth, populated by the IHDR chunk.</summary>
        public int BitDepth { get; set; }

        /// <summary>The raw PLTE chunk data (RGB triples), or null if no PLTE chunk was present.</summary>
        public byte[]? PlteData { get; set; }

        /// <summary>The raw tRNS chunk data, or null if no tRNS chunk was present.</summary>
        public byte[]? TrnsData { get; set; }

        /// <summary>Whether a PLTE chunk has already been seen.</summary>
        public bool PlteSeen { get; set; }

        /// <summary>Whether a tRNS chunk has already been seen.</summary>
        public bool TrnsSeen { get; set; }

        /// <summary>Whether an IDAT chunk has already been seen.</summary>
        public bool IdatSeen { get; set; }

        /// <summary>
        ///     Whether the run of consecutive IDAT chunks has already ended - set the moment a
        ///     non-IDAT chunk is processed after at least one IDAT chunk has been seen. The PNG
        ///     specification requires every IDAT chunk to be consecutive, so a further IDAT chunk
        ///     encountered once this flag is set indicates a non-conforming file.
        /// </summary>
        public bool IdatRunEnded { get; set; }
    }

    /// <summary>
    ///     Reads and validates every chunk from <paramref name="stream"/> until (and including)
    ///     <c>IEND</c>, accumulating <c>IDAT</c> payload bytes into <paramref name="idatData"/>
    ///     and returning the parsed <c>IHDR</c> fields plus any <c>PLTE</c>/<c>tRNS</c> payloads.
    /// </summary>
    private static PngHeader ReadChunks(Stream stream, out byte[] idatData, out byte[]? plteData, out byte[]? trnsData)
    {
        var state = new ChunkReadState();
        using var idatStream = new MemoryStream();

        // Read chunks until IEND is encountered; ReadExactly throws InvalidDataException if the
        // stream ends before IEND is found, which correctly rejects a truncated stream
        while (!state.IendSeen)
        {
            ProcessChunk(stream, idatStream, state);
        }

        idatData = idatStream.ToArray();
        plteData = state.PlteData;
        trnsData = state.TrnsData;
        return new PngHeader(state.Width, state.Height, state.ColorType, state.BitDepth);
    }

    /// <summary>
    ///     Reads, CRC-validates, and dispatches a single chunk: <c>IHDR</c> populates
    ///     <paramref name="state"/>'s header fields, <c>PLTE</c>/<c>tRNS</c> data is stored for
    ///     later use by the decode step, <c>IDAT</c> data is appended to
    ///     <paramref name="idatStream"/>, <c>IEND</c> marks the chunk stream complete, and any
    ///     other recognized-ancillary chunk type is validated but otherwise skipped. An
    ///     unrecognized <em>critical</em> chunk (uppercase first type byte, per the PNG
    ///     specification's chunk-naming convention) is rejected outright - see the "any other
    ///     chunk type" handling at the end of this method.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown, among the other per-chunk-type conditions documented inline below, when the
    ///     chunk type is not one of <c>IHDR</c>/<c>PLTE</c>/<c>tRNS</c>/<c>IDAT</c>/<c>IEND</c>
    ///     and its first byte is an uppercase ASCII letter, marking it critical: an unrecognized
    ///     critical chunk may change how pixel data must be interpreted, so a conforming decoder
    ///     that does not understand it must refuse to decode rather than risk producing incorrect
    ///     pixels.
    /// </exception>
    private static void ProcessChunk(Stream stream, MemoryStream idatStream, ChunkReadState state)
    {
        var (typeBytes, data) = ReadChunkFrame(
            stream,
            (chunkTypeBytes, declaredLength) => ValidateChunkLengthBeforeAllocation(chunkTypeBytes, declaredLength, state));

        // The PNG specification requires every IDAT chunk to be consecutive: the moment a
        // non-IDAT chunk is processed after at least one IDAT chunk has been seen, the IDAT run
        // has ended, so any further IDAT chunk encountered later is non-conforming
        if (!ChunkTypeIs(typeBytes, "IDAT") && state.IdatSeen)
        {
            state.IdatRunEnded = true;
        }

        if (ChunkTypeIs(typeBytes, "IHDR"))
        {
            if (state.IhdrSeen)
            {
                throw new InvalidDataException("Duplicate IHDR chunk.");
            }

            (state.Width, state.Height, state.ColorType, state.BitDepth) =
                ParseIhdr(data, enforceMaxDimension: true, validateDecodability: true);
            state.IhdrSeen = true;
        }
        else if (ChunkTypeIs(typeBytes, "PLTE"))
        {
            if (!state.IhdrSeen)
            {
                throw new InvalidDataException("PLTE chunk encountered before IHDR.");
            }

            if (state.PlteSeen)
            {
                throw new InvalidDataException("Duplicate PLTE chunk.");
            }

            if (state.IdatSeen)
            {
                throw new InvalidDataException("PLTE chunk encountered after the first IDAT chunk.");
            }

            // The PNG specification requires PLTE to precede tRNS whenever both are present,
            // regardless of color type: a tRNS chunk that has already been accepted means a PLTE
            // chunk arriving afterward is out of order, even for color types (2 and 6) where
            // PLTE is merely an optional suggested palette rather than mandatory
            if (state.TrnsSeen)
            {
                throw new InvalidDataException("PLTE chunk must precede tRNS chunk.");
            }

            if (data.Length % 3 != 0 || data.Length == 0)
            {
                throw new InvalidDataException(
                    $"Invalid PNG PLTE chunk length {data.Length}; expected a positive multiple of 3.");
            }

            if (data.Length > 256 * 3)
            {
                throw new InvalidDataException(
                    $"PNG PLTE chunk declares {data.Length / 3} palette entries; at most 256 are permitted.");
            }

            // The PNG specification forbids a PLTE chunk for the two grayscale color types (0 and
            // 4): grayscale samples are never resolved through a palette, so a PLTE chunk on such
            // a file cannot represent anything a conforming decoder is permitted to use
            if (state.ColorType is ColorTypeGrayscale or ColorTypeGrayscaleAlpha)
            {
                throw new InvalidDataException(
                    $"PLTE chunk is not permitted for grayscale PNG color type {state.ColorType}.");
            }

            if (state.ColorType == ColorTypePalette)
            {
                var maxEntries = 1 << state.BitDepth;
                var entryCount = data.Length / 3;
                if (entryCount > maxEntries)
                {
                    throw new InvalidDataException(
                        $"PNG PLTE chunk declares {entryCount} palette entries, which exceeds the maximum of " +
                        $"{maxEntries} entries permitted for a palette (color type 3) image at bit depth " +
                        $"{state.BitDepth}.");
                }
            }

            state.PlteData = data;
            state.PlteSeen = true;
        }
        else if (ChunkTypeIs(typeBytes, "tRNS"))
        {
            if (!state.IhdrSeen)
            {
                throw new InvalidDataException("tRNS chunk encountered before IHDR.");
            }

            if (state.TrnsSeen)
            {
                throw new InvalidDataException("Duplicate tRNS chunk.");
            }

            if (state.IdatSeen)
            {
                throw new InvalidDataException("tRNS chunk encountered after the first IDAT chunk.");
            }

            // The PNG specification forbids a tRNS chunk for the two color types that already
            // carry a full per-pixel alpha channel (grayscale-with-alpha and Truecolor-with-alpha):
            // there is nothing for a single-key-color transparency chunk to add for those formats,
            // so its presence indicates a malformed, non-conforming file rather than a feature to
            // silently ignore
            if (state.ColorType is ColorTypeGrayscaleAlpha or ColorTypeTruecolorAlpha)
            {
                throw new InvalidDataException(
                    $"tRNS chunk is not permitted for PNG color type {state.ColorType}, which already " +
                    "carries a full per-pixel alpha channel.");
            }

            // For indexed-color (palette) images, tRNS's per-palette-entry alpha values are
            // meaningless without the PLTE chunk they index into, so the specification requires
            // tRNS to appear after PLTE, not merely after IHDR and before the first IDAT
            if (state.ColorType == ColorTypePalette && !state.PlteSeen)
            {
                throw new InvalidDataException("tRNS chunk for indexed-color PNG must follow PLTE.");
            }

            state.TrnsData = data;
            state.TrnsSeen = true;
        }
        else if (ChunkTypeIs(typeBytes, "IDAT"))
        {
            if (!state.IhdrSeen)
            {
                throw new InvalidDataException("IDAT chunk encountered before IHDR.");
            }

            if (state.IdatRunEnded)
            {
                throw new InvalidDataException("IDAT chunks must be consecutive.");
            }

            state.IdatSeen = true;
            idatStream.Write(data, 0, data.Length);
        }
        else if (ChunkTypeIs(typeBytes, "IEND"))
        {
            if (!state.IhdrSeen)
            {
                throw new InvalidDataException("IEND chunk encountered before IHDR.");
            }

            state.IendSeen = true;
        }
        else if (typeBytes[0] is >= (byte)'A' and <= (byte)'Z')
        {
            // The PNG specification uses a chunk type's first byte's case to mark it critical
            // (uppercase) or ancillary (lowercase). This chunk type is not one of the five
            // chunks this codec explicitly recognizes above, yet its first byte is uppercase, so
            // it is an unrecognized critical chunk: it may change how pixel data must be
            // interpreted, so a conforming decoder that does not understand it must refuse to
            // decode rather than risk silently producing incorrect pixels.
            var typeName = System.Text.Encoding.ASCII.GetString(typeBytes);
            throw new InvalidDataException($"Unrecognized critical PNG chunk '{typeName}'.");
        }

        // Any other chunk type (for example "tEXt", "pHYs", "gAMA") is an unrecognized ancillary
        // chunk this codec does not need; its CRC-32 has already been validated above, and its
        // data is simply not accumulated anywhere, effectively skipping it - except that, like
        // every other chunk type, it must still not appear before the mandatory IHDR chunk, which
        // the PNG specification requires to always be first
        else if (!state.IhdrSeen)
        {
            throw new InvalidDataException("Chunk encountered before IHDR.");
        }
    }

    /// <summary>
    ///     Rejects a declared <c>PLTE</c> or <c>tRNS</c> chunk length that already exceeds the
    ///     largest length that type can legitimately have, using only the 4-byte declared length
    ///     from the chunk header - <em>before</em> <see cref="ReadChunkFrame"/> allocates and
    ///     reads a payload buffer of that declared size. Without this pre-allocation check, a
    ///     crafted PNG could declare a <c>PLTE</c> or <c>tRNS</c> length that is large (but still
    ///     below <see cref="uint.MaxValue"/>'s already-enforced <see cref="int.MaxValue"/> ceiling)
    ///     purely to force a large allocation before the full post-read checks in
    ///     <see cref="ProcessChunk"/> (multiple-of-3, exact bit-depth-derived entry cap, and the
    ///     precise <c>ValidateAndNormalizeTrns</c> per-color-type/per-PLTE-entry-count checks) get
    ///     a chance to reject it - this method exists purely to close that memory-exhaustion
    ///     attack vector, not to duplicate those precise correctness checks, so its bounds are
    ///     deliberately loose (the largest a spec-valid chunk of that type could ever be).
    /// </summary>
    /// <param name="typeBytes">The chunk's 4-byte type field.</param>
    /// <param name="length">The chunk's declared data length, read from the chunk header.</param>
    /// <param name="state">
    ///     The in-progress chunk-read state, used to narrow the <c>tRNS</c> bound by color type
    ///     once <c>IHDR</c> has been parsed; before that, only the loosest (palette-sized) bound
    ///     is available, which is still small enough to rule out a memory-exhaustion attempt.
    /// </param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when a <c>PLTE</c> or <c>tRNS</c> chunk declares a length beyond the largest
    ///     value that type can legitimately have.
    /// </exception>
    private static void ValidateChunkLengthBeforeAllocation(byte[] typeBytes, uint length, ChunkReadState state)
    {
        if (ChunkTypeIs(typeBytes, "PLTE"))
        {
            if (length > MaxPlteDataLength)
            {
                throw new InvalidDataException(
                    $"PNG PLTE chunk declares a {length}-byte payload, which exceeds the maximum of " +
                    $"{MaxPlteDataLength} bytes ({MaxPaletteEntries} entries) permitted by the PNG " +
                    "specification, regardless of color type or bit depth.");
            }
        }
        else if (ChunkTypeIs(typeBytes, "tRNS"))
        {
            // Once IHDR has been parsed, the color type pins the exact tRNS length for grayscale
            // (2 bytes) and Truecolor (6 bytes); every other case - palette, an as-yet-unparsed
            // IHDR, or a color type for which tRNS is not even permitted - falls back to the
            // one-byte-per-palette-entry ceiling, which is still small enough to rule out a
            // large allocation while leaving the precise rejection to the checks noted above
            var maxLength = state switch
            {
                { IhdrSeen: true, ColorType: ColorTypeGrayscale } => 2,
                { IhdrSeen: true, ColorType: ColorTypeTruecolor } => 6,
                _ => MaxPaletteEntries
            };

            if (length > maxLength)
            {
                throw new InvalidDataException(
                    $"PNG tRNS chunk declares a {length}-byte payload, which exceeds the maximum of " +
                    $"{maxLength} bytes permitted for this file.");
            }
        }
    }

    /// <summary>
    ///     Reads and CRC-validates a single chunk frame (length, type, data, and CRC-32) from
    ///     <paramref name="stream"/>. The chunk type's four bytes are validated against the PNG
    ///     specification's letter/reserved-bit rules (see <see cref="ValidateChunkTypeCode"/>)
    ///     before this method otherwise interprets the type in any way, and
    ///     <paramref name="validateLengthBeforeAllocation"/>, when supplied, is invoked with the
    ///     (now type-validated) type bytes and the declared length immediately afterward - both
    ///     checks run <em>before</em> the length-dependent payload buffer below is allocated and
    ///     read, so a crafted chunk with a malformed type code, or a declared length that already
    ///     violates a type-specific maximum, is rejected without first forcing a large allocation.
    /// </summary>
    /// <param name="stream">The stream to read the chunk frame from.</param>
    /// <param name="validateLengthBeforeAllocation">
    ///     Optional callback invoked with the chunk's type bytes and declared length before the
    ///     data payload is allocated or read, allowing a caller to reject a declared length that
    ///     already exceeds a type-specific maximum (for example <c>PLTE</c> or <c>tRNS</c>)
    ///     without first allocating a buffer of that size. Should throw
    ///     <see cref="System.IO.InvalidDataException"/> to reject; a non-throwing return accepts
    ///     the declared length.
    /// </param>
    /// <returns>The chunk's 4-byte type field and its data payload.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the declared chunk length exceeds the supported range, the chunk type is
    ///     not four ASCII letters or violates the reserved-bit rule (see
    ///     <see cref="ValidateChunkTypeCode"/>), <paramref name="validateLengthBeforeAllocation"/>
    ///     rejects the declared length, the stream ends before the full chunk frame has been
    ///     read, or the chunk's CRC-32 does not match its type and data.
    /// </exception>
    private static (byte[] TypeBytes, byte[] Data) ReadChunkFrame(
        Stream stream,
        Action<byte[], uint>? validateLengthBeforeAllocation = null)
    {
        var lengthBytes = ReadExactly(stream, 4, "chunk length");
        var length = ReadUInt32Be(lengthBytes, 0);
        if (length > int.MaxValue)
        {
            throw new InvalidDataException("Chunk length exceeds the supported range.");
        }

        var typeBytes = ReadExactly(stream, 4, "chunk type");
        ValidateChunkTypeCode(typeBytes);
        validateLengthBeforeAllocation?.Invoke(typeBytes, length);

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

        return (typeBytes, data);
    }

    /// <summary>
    ///     Validates the 8-byte PNG signature and reads exactly the first chunk frame, requiring
    ///     it to be <c>IHDR</c>, without reading any further chunks.
    /// </summary>
    /// <param name="stream">The stream to read the signature and IHDR chunk from.</param>
    /// <param name="enforceMaxDimension">
    ///     Forwarded to <see cref="ParseIhdr"/>; see its documentation.
    /// </param>
    /// <param name="validateDecodability">
    ///     Forwarded to <see cref="ParseIhdr"/>; see its documentation.
    /// </param>
    /// <returns>The parsed <c>IHDR</c> fields.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the signature does not match, the first chunk is not <c>IHDR</c>, or the
    ///     <c>IHDR</c> chunk itself is malformed (see <see cref="ParseIhdr"/>).
    /// </exception>
    private static PngHeader ReadIhdrOnly(Stream stream, bool enforceMaxDimension, bool validateDecodability)
    {
        ValidateSignature(stream);

        var (_, data) = ReadIhdrChunkFrame(stream);
        var (width, height, colorType, bitDepth) = ParseIhdr(data, enforceMaxDimension, validateDecodability);
        return new PngHeader(width, height, colorType, bitDepth);
    }


    /// <summary>
    ///     Reads and CRC-validates the first chunk frame from <paramref name="stream"/>,
    ///     requiring it to be an <c>IHDR</c> chunk with the exact 13-byte length mandated by the
    ///     PNG specification. Unlike <see cref="ReadChunkFrame(Stream, Action{byte[], uint})"/>, the chunk type and
    ///     declared length are both validated <em>before</em> the (fixed-size) data payload is
    ///     allocated or read, so a crafted non-<c>IHDR</c> or wrong-length first chunk with a huge
    ///     declared length can never force a large allocation.
    /// </summary>
    /// <param name="stream">The stream to read the first chunk frame from.</param>
    /// <returns>The chunk's 4-byte type field (always <c>IHDR</c>) and its 13-byte data payload.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream ends before the chunk length and type fields have been read,
    ///     the first chunk is not <c>IHDR</c>, the <c>IHDR</c> chunk's declared length is not
    ///     exactly 13, the stream ends before the full chunk frame has been read, or the chunk's
    ///     CRC-32 does not match its type and data.
    /// </exception>
    private static (byte[] TypeBytes, byte[] Data) ReadIhdrChunkFrame(Stream stream)
    {
        const int ihdrDataLength = 13;

        var lengthBytes = ReadExactly(stream, 4, "chunk length");
        var length = ReadUInt32Be(lengthBytes, 0);

        var typeBytes = ReadExactly(stream, 4, "chunk type");
        if (!ChunkTypeIs(typeBytes, "IHDR"))
        {
            throw new InvalidDataException("First PNG chunk is not IHDR.");
        }

        if (length != ihdrDataLength)
        {
            throw new InvalidDataException("IHDR chunk does not have the required length of 13 bytes.");
        }

        var data = ReadExactly(stream, ihdrDataLength, "chunk data");
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

        return (typeBytes, data);
    }

    /// <summary>
    ///     Defilters and decodes every scanline of decompressed PNG raw data into a new
    ///     <see cref="Surface"/>, reconstructing each row from the previous row per the PNG
    ///     filtering specification, then mapping each row's samples to RGBA per
    ///     <paramref name="colorType"/>.
    /// </summary>
    /// <param name="rawData">The decompressed, filtered scanline bytes (one filter-type byte plus <paramref name="rowBytes"/> per row).</param>
    /// <param name="width">The image width, in pixels.</param>
    /// <param name="height">The image height, in pixels.</param>
    /// <param name="colorType">The PNG color type.</param>
    /// <param name="bitDepth">The PNG bit depth (1, 2, 4, 8, or 16).</param>
    /// <param name="rowBytes">The number of packed pixel bytes per row (excluding the filter-type byte).</param>
    /// <param name="bpp">The number of whole bytes per pixel, used by the defilter algorithms (at least 1).</param>
    /// <param name="samplesPerPixel">The number of samples per pixel for <paramref name="colorType"/>.</param>
    /// <param name="palette">The raw PLTE chunk data (RGB triples), or null if absent.</param>
    /// <param name="trns">The validated tRNS chunk data for this color type, or null if absent/not applicable.</param>
    private static Surface DecodeScanlines(
        byte[] rawData,
        int width,
        int height,
        int colorType,
        int bitDepth,
        int rowBytes,
        int bpp,
        int samplesPerPixel,
        byte[]? palette,
        byte[]? trns)
    {
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
        var maxSample = (1 << bitDepth) - 1;

        switch (colorType)
        {
            case ColorTypeGrayscale:
                {
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

                    break;
                }

            case ColorTypeTruecolor:
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

                    break;
                }

            case ColorTypePalette:
                {
                    var entries = palette!.Length / 3;
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

                    break;
                }

            case ColorTypeGrayscaleAlpha:
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

                    break;
                }

            case ColorTypeTruecolorAlpha:
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

                    break;
                }
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
    ///     Validates a raw <c>tRNS</c> chunk payload against the file's color type and (for
    ///     palette) its <c>PLTE</c> chunk, returning the chunk unchanged when applicable or null
    ///     when absent. A <c>tRNS</c> chunk can never reach this method for the
    ///     grayscale-with-alpha or Truecolor-with-alpha color types - <c>ProcessChunk</c> rejects
    ///     such a chunk outright as soon as it is encountered, since neither color type is
    ///     spec-defined for <c>tRNS</c> - so the <c>default</c> case below exists only as a
    ///     defensive fallback for any other, already-rejected-earlier color type.
    /// </summary>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when a grayscale or Truecolor <c>tRNS</c> chunk does not have its mandatory
    ///     fixed length, or a palette <c>tRNS</c> chunk has more entries than the PLTE chunk
    ///     defines.
    /// </exception>
    private static byte[]? ValidateAndNormalizeTrns(int colorType, byte[]? plteData, byte[]? trnsData)
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

                return trnsData;

            case ColorTypeTruecolor:
                if (trnsData.Length != 6)
                {
                    throw new InvalidDataException(
                        $"Invalid PNG tRNS chunk length {trnsData.Length} for Truecolor; expected 6 bytes.");
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
        ihdrData[8] = SaveBitDepth;
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
    /// <param name="enforceMaxDimension">
    ///     When <see langword="true"/>, rejects a width or height above
    ///     <see cref="Surface.MaxDimension"/> with an <see cref="InvalidDataException"/>, as
    ///     <see cref="Load(Stream)"/> requires. When <see langword="false"/>, the raw
    ///     header-declared width and height are returned without comparison, as
    ///     <see cref="GetInfo(Stream)"/> requires. This flag gates only this one check.
    /// </param>
    /// <param name="validateDecodability">
    ///     When <see langword="true"/>, additionally rejects Adam7 interlacing (interlace method
    ///     1) with an <see cref="InvalidDataException"/>, as <see cref="Load(Stream)"/> requires,
    ///     since this codec does not implement Adam7 decoding. When <see langword="false"/>, an
    ///     Adam7-interlaced <c>IHDR</c> is accepted, as <see cref="GetInfo(Stream)"/> requires,
    ///     since Adam7 interlacing does not affect the declared width/height it reports. This
    ///     flag gates only this one check - every other check below (bit depth in range, color
    ///     type in range, their combination being legal per the PNG specification, compression/
    ///     filter method, interlace method in range) is a well-formedness check always enforced
    ///     regardless of this flag's value, since this codec's decodable color-type/bit-depth
    ///     space is now the PNG specification's entire legal space (see the type-level remarks).
    /// </param>
    /// <returns>The parsed image width, height, PNG color type byte, and bit depth.</returns>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when <paramref name="data"/> is not 13 bytes, describes non-positive
    ///     dimensions (or, when <paramref name="enforceMaxDimension"/> is <see langword="true"/>,
    ///     oversized dimensions exceeding <see cref="Surface.MaxDimension"/>), describes a bit
    ///     depth or color type not defined by the PNG specification, describes a bit-depth/color-
    ///     type combination that is itself invalid per the specification (for example palette at
    ///     16-bit depth), describes an unsupported compression or filter method, describes an
    ///     interlace method not defined by the specification, or (only when
    ///     <paramref name="validateDecodability"/> is <see langword="true"/>) declares Adam7
    ///     interlacing.
    /// </exception>
    private static (int Width, int Height, int ColorType, int BitDepth) ParseIhdr(
        byte[] data,
        bool enforceMaxDimension,
        bool validateDecodability)
    {
        if (data.Length != 13)
        {
            throw new InvalidDataException($"Invalid IHDR chunk length {data.Length}; expected 13 bytes.");
        }

        var width = (int)ReadUInt32Be(data, 0);
        var height = (int)ReadUInt32Be(data, 4);
        int bitDepth = data[8];
        int colorType = data[9];
        var compressionMethod = data[10];
        var filterMethod = data[11];
        var interlaceMethod = data[12];

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"Invalid PNG dimensions {width}x{height}.");
        }

        // Reject dimensions above Surface.MaxDimension here, before ParseIhdr returns and before
        // any width/height arithmetic performed later in Load, so an oversized value surfaces as
        // the documented InvalidDataException rather than an ArgumentOutOfRangeException escaping
        // from deep inside Surface's constructor. Skipped entirely when enforceMaxDimension is
        // false, so GetInfo can report the raw header dimensions even when they exceed the bound.
        if (enforceMaxDimension && (width > Surface.MaxDimension || height > Surface.MaxDimension))
        {
            throw new InvalidDataException(
                $"PNG dimensions {width}x{height} exceed the maximum supported size of " +
                $"{Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        if (bitDepth is not (1 or 2 or 4 or 8 or 16))
        {
            throw new InvalidDataException(
                $"Invalid PNG bit depth {bitDepth}; only 1, 2, 4, 8, or 16 bits per sample are " +
                "defined by the PNG specification.");
        }

        if (colorType is not (ColorTypeGrayscale or ColorTypeTruecolor or ColorTypePalette
            or ColorTypeGrayscaleAlpha or ColorTypeTruecolorAlpha))
        {
            throw new InvalidDataException(
                $"Invalid PNG color type {colorType}; only grayscale (0), Truecolor (2), " +
                "palette (3), grayscale-with-alpha (4), and Truecolor-with-alpha (6) are " +
                "defined by the PNG specification.");
        }

        if (!IsValidBitDepthForColorType(colorType, bitDepth))
        {
            throw new InvalidDataException(
                $"Bit depth {bitDepth} is not valid for PNG color type {colorType} per the PNG " +
                "specification's color-type/bit-depth combination table.");
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

        if (interlaceMethod is not (InterlaceNone or InterlaceAdam7))
        {
            throw new InvalidDataException(
                $"Invalid PNG interlace method {interlaceMethod}; only 0 (none) and 1 (Adam7) " +
                "are defined by the PNG specification.");
        }

        if (validateDecodability && interlaceMethod == InterlaceAdam7)
        {
            throw new InvalidDataException(
                "Adam7 interlacing is not supported by Load; use GetInfo to obtain this file's " +
                "declared dimensions without decoding its pixel data.");
        }

        return (width, height, colorType, bitDepth);
    }


    /// <summary>
    ///     Validates a 4-byte PNG chunk type field against the PNG specification's chunk-naming
    ///     rules, before <see cref="ProcessChunk"/> uses the first byte's case to classify an
    ///     otherwise-unrecognized chunk as critical or ancillary. Each of the four bytes must be
    ///     an ASCII letter (<c>A</c>-<c>Z</c> or <c>a</c>-<c>z</c>), and the third byte (the
    ///     specification's "reserved" bit position) must always be uppercase, since the
    ///     specification currently reserves a lowercase third byte for future definition. Without
    ///     this check, a malformed type such as <c>"a!cd"</c> (a non-letter byte) or <c>"aBcd"</c>
    ///     (lowercase reserved byte) would reach the ancillary/critical classification below and
    ///     be silently accepted as a spec-valid ancillary chunk, contrary to this codec's
    ///     spec-valid/malformed-input contract.
    /// </summary>
    /// <param name="typeBytes">The 4-byte chunk type field read from the stream.</param>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when any byte is not an ASCII letter, or the third byte is a lowercase letter.
    /// </exception>
    private static void ValidateChunkTypeCode(byte[] typeBytes)
    {
        for (var i = 0; i < 4; i++)
        {
            var b = typeBytes[i];
            if (b is not (>= (byte)'A' and <= (byte)'Z') and not (>= (byte)'a' and <= (byte)'z'))
            {
                throw new InvalidDataException(
                    $"Invalid PNG chunk type byte {i + 1} (0x{b:X2}); every chunk type byte must be an ASCII letter.");
            }
        }

        // The PNG specification's reserved-bit rule requires the third chunk-type byte to
        // always be uppercase; a lowercase third byte is reserved for future definition and is
        // never spec-valid for a chunk produced today, so this codec rejects it outright rather
        // than silently classifying it as ancillary (or critical) based on the first byte alone
        if (typeBytes[2] is >= (byte)'a' and <= (byte)'z')
        {
            var typeName = System.Text.Encoding.ASCII.GetString(typeBytes);
            throw new InvalidDataException(
                $"Invalid PNG chunk type '{typeName}'; the third byte must be uppercase (reserved-bit rule).");
        }
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
                DefilterSub(filtered, output, bpp);
                break;

            case 2: // Up
                DefilterUp(filtered, previousRow, output);
                break;

            case 3: // Average
                DefilterAverage(filtered, previousRow, output, bpp);
                break;

            case 4: // Paeth
                DefilterPaeth(filtered, previousRow, output, bpp);
                break;

            default:
                throw new InvalidDataException(
                    $"Unsupported PNG filter type {filterType}; only filter types 0-4 are supported.");
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 1 (Sub).</summary>
    private static void DefilterSub(ReadOnlySpan<byte> filtered, Span<byte> output, int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            output[i] = (byte)(filtered[i] + left);
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 2 (Up).</summary>
    private static void DefilterUp(ReadOnlySpan<byte> filtered, ReadOnlySpan<byte> previousRow, Span<byte> output)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            output[i] = (byte)(filtered[i] + previousRow[i]);
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 3 (Average).</summary>
    private static void DefilterAverage(
        ReadOnlySpan<byte> filtered,
        ReadOnlySpan<byte> previousRow,
        Span<byte> output,
        int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            int up = previousRow[i];
            output[i] = (byte)(filtered[i] + (left + up) / 2);
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 4 (Paeth).</summary>
    private static void DefilterPaeth(
        ReadOnlySpan<byte> filtered,
        ReadOnlySpan<byte> previousRow,
        Span<byte> output,
        int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            int up = previousRow[i];
            int upperLeft = i >= bpp ? previousRow[i - bpp] : 0;
            output[i] = (byte)(filtered[i] + PaethPredictor(left, up, upperLeft));
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
