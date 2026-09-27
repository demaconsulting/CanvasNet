using System.IO.Compression;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Identifies the PNG color type used when saving a <see cref="Surface"/> to PNG format.
/// </summary>
/// <remarks>
///     Only the two 8-bit-per-channel Truecolor variants supported by
///     <see cref="PngCodec.Save(Surface, System.IO.Stream, PngColorType)"/> are represented here,
///     using the same numeric values as the PNG specification's color type byte; grayscale,
///     palette/indexed, and 16-bit-depth PNG variants are out of scope for <c>Save</c> and are
///     never produced by it, but they are decoded by <see cref="PngCodec.Load(System.IO.Stream)"/>.
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
///         the standard (non-interlaced) scanline order. Only one thing remains a hard refusal for
///         <c>Load</c> that is not itself a well-formedness defect: Adam7 interlacing (interlace
///         method 1), which this codec does not implement - a well-formed, spec-conforming file
///         that <c>Load</c> simply cannot decode. A bit-depth/color-type combination that is
///         itself invalid per the PNG specification (for example palette at 16-bit depth) is, by
///         contrast, a genuine well-formedness defect: <see cref="ParseIhdr"/> rejects it
///         unconditionally for both <c>Load</c> and <c>GetInfo</c>, exactly like any other
///         malformed <c>IHDR</c> field, with a descriptive <see cref="System.IO.InvalidDataException"/>.
///         Adam7 interlacing, being a well-formed-but-unsupported feature rather than a
///         well-formedness defect, is instead rejected with the more specific
///         <see cref="UnsupportedImageFeatureException"/> (which does <em>not</em> derive from
///         <see cref="System.IO.InvalidDataException"/>, since that type is sealed in .NET - see
///         <see cref="UnsupportedImageFeatureException"/>'s own remarks for the compatibility
///         implications), letting a caller distinguish the two cases without string-matching the
///         exception message; see <see cref="ImageInfo.CanDecode"/> for how a caller can detect
///         this case before calling <c>Load</c> at all. Neither case silently produces incorrect
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
public static partial class PngCodec
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
    ///     The size, in bytes, of the reusable buffer used to stream a chunk's payload in bounded
    ///     pieces instead of buffering the whole declared length in one array - shared by two
    ///     cases handled by <see cref="StreamChunkPayload"/>: stream-discarding a recognized
    ///     ancillary chunk's payload (see <see cref="StreamDiscardChunkPayload"/>), and
    ///     stream-appending an <c>IDAT</c> chunk's payload directly into the accumulating
    ///     <c>idatStream</c> (see <see cref="ReadChunkFrame"/>'s <c>idatDestination</c>
    ///     parameter). Both a recognized ancillary chunk (for example <c>tEXt</c> or
    ///     <c>iCCP</c>) and a legitimate <c>IDAT</c> chunk may declare a large payload - for
    ///     <c>IDAT</c>, the PNG specification does not require encoders to split compressed
    ///     image data into small pieces, so a conforming encoder may legitimately emit an entire
    ///     large image as a single, very large <c>IDAT</c> chunk - so unlike <c>PLTE</c> or
    ///     <c>tRNS</c>, there is no small type-specific maximum that would let
    ///     <see cref="ValidateChunkLengthBeforeAllocation"/> reject an oversized declared length
    ///     before allocation for either case; instead, each piece is read and CRC-validated in
    ///     bounded pieces of this size, keeping peak allocation bounded regardless of how large
    ///     the declared length is.
    /// </summary>
    private const int AncillaryChunkStreamBufferSize = 8192;

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
    ///     The exact byte length the PNG specification mandates for an <c>IHDR</c> chunk's data.
    ///     Used both by <see cref="ParseIhdr"/>'s post-read validation and by the pre-allocation
    ///     first-chunk checks in <see cref="ReadIhdrChunkFrame"/> and
    ///     <see cref="ValidateChunkLengthBeforeAllocation"/>.
    /// </summary>
    private const int IhdrDataLength = 13;

    /// <summary>
    ///     Loads a <see cref="Surface"/> from an open, readable stream containing a well-formed,
    ///     non-interlaced PNG image of any color type and bit depth combination the PNG
    ///     specification permits.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the PNG image from. Reading begins at the stream's current position
    ///     and consumes exactly the PNG signature, all chunks through <c>IEND</c>, and one further
    ///     byte read to confirm the stream ends there, per the PNG specification's requirement
    ///     that <c>IEND</c> be the final chunk in the datastream.
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
    ///     precedes the <c>PLTE</c> chunk on a palette file; a grayscale or Truecolor <c>tRNS</c>
    ///     chunk encodes a key sample value that exceeds the maximum value representable at the
    ///     file's bit depth; a <c>PLTE</c> chunk appears after a
    ///     <c>tRNS</c> chunk has already been accepted; the <c>IDAT</c> chunks are not
    ///     consecutive; any chunk appears before the mandatory <c>IHDR</c> chunk; any chunk's
    ///     CRC-32 does not match; the decompressed scanline data
    ///     has an unexpected length; an unsupported scanline filter type is encountered; data is
    ///     found in the stream after the <c>IEND</c> chunk; or the
    ///     stream ends before all header, chunk, or pixel data has been read.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the file is a well-formed PNG that declares Adam7 interlacing, which this
    ///     codec does not implement; use <see cref="GetInfo(Stream)"/>'s
    ///     <see cref="ImageInfo.CanDecode"/> to detect this case beforehand without catching this
    ///     exception.
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

        var trns = ValidateAndNormalizeTrns(header.ColorType, header.BitDepth, plteData, trnsData);

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
            header,
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
    ///     Thrown for the same malformed-format conditions as <see cref="Load(Stream)"/>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown for the same Adam7-interlacing condition as <see cref="Load(Stream)"/>.
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
    ///     <see cref="ImageInfo.CanDecode"/> is <see langword="false"/> when the file declares
    ///     Adam7 interlacing (the one well-formed PNG feature <see cref="Load(Stream)"/> does not
    ///     implement) and <see langword="true"/> otherwise, letting a caller detect this case
    ///     before calling <c>Load</c> instead of having to catch
    ///     <see cref="UnsupportedImageFeatureException"/> from it.
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
    ///     is <em>not</em> rejected (see <see cref="ImageInfo.CanDecode"/> above instead). Any
    ///     corruption in a subsequent chunk (including <c>PLTE</c>, <c>tRNS</c>, <c>IDAT</c>, or
    ///     <c>IEND</c>) is never encountered by <c>GetInfo</c>.
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
        return new ImageInfo(header.Width, header.Height, channels, hasAlpha)
        {
            CanDecode = header.InterlaceMethod != InterlaceAdam7
        };
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
}
