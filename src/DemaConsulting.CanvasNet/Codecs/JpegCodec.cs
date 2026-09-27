using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Provides hand-rolled, dependency-free loading and saving of a common real-world subset of
///     JPEG (ITU-T T.81 / ISO/IEC 10918-1) files to and from <see cref="Surface"/> pixel buffers.
/// </summary>
/// <remarks>
///     <c>JpegCodec</c> is the sixth software unit in CanvasNet, and depends on <see cref="Surface"/>
///     exactly as <see cref="BmpCodec"/>, <see cref="PngCodec"/>, and <see cref="TiffCodec"/> do: it
///     constructs and reads <see cref="Surface"/> instances via the existing public
///     <see cref="Surface.GetRowSpanBytes"/>/indexer accessors, but adds no new public members to
///     <see cref="Surface"/> itself. It is a stateless, static utility class - there is nothing to
///     construct or configure, so an instance type would add no value over static methods.
///     <para>
///         <see cref="Load(System.IO.Stream)"/> supports both baseline sequential DCT (SOF0) and
///         progressive DCT (SOF2, spectral selection and successive approximation, both DC and AC
///         scans) frames, with 1 (grayscale) or 3 (YCbCr) components, arbitrary per-component 1x1
///         or 2x2 sampling factors (4:4:4, 4:2:2, and 4:2:0 chroma subsampling), restart markers
///         (DRI/RSTn), and both 8-bit and 16-bit precision quantization tables. Every Huffman and
///         quantization table is built from the file's own embedded DHT/DQT segments - no
///         "standard" table is assumed to be present. 4-component (CMYK/YCCK) images, arithmetic
///         coding, and any SOF marker other than SOF0/SOF2 are rejected with
///         <see cref="System.IO.InvalidDataException"/>.
///     </para>
///     <para>
///         <see cref="Save(Surface, System.IO.Stream, int)"/> writes baseline-only (SOF0), 3-component
///         YCbCr, 4:2:0 chroma-subsampled JPEG files, using the standard ITU-T Annex K example
///         quantization tables (scaled by the libjpeg-style linear formula from the requested
///         <c>quality</c>) and the standard Annex K default Huffman tables.
///     </para>
///     <para>
///         Architectural decision: because JPEG is a lossy format, this codec's tests compare
///         decoded pixels against a similarity tolerance rather than exact equality; unlike
///         <see cref="BmpCodec"/>, <see cref="PngCodec"/>, and <see cref="TiffCodec"/>, a
///         round-trip through <c>Save</c> then <c>Load</c> is not expected to reproduce the
///         original pixel values exactly.
///     </para>
///     <para>
///         Architectural decision: the 8x8 inverse/forward DCT is implemented as a separable,
///         directly-verifiable float transform (a row pass followed by a column pass, each a
///         direct 1-D type-II/type-III cosine sum against a precomputed basis matrix) rather than
///         a fast butterfly network such as AAN. A butterfly network's scaling/permutation steps
///         are easy to get subtly wrong in a way that only manifests on specific coefficient
///         patterns, which this codec's tolerance-based tests would not reliably catch; the
///         direct-sum approach trades some raw speed for an implementation whose correctness
///         follows directly from the well-known DCT-II/DCT-III formulas.
///     </para>
///     <para>
///         Architectural decision: <see cref="System.Numerics.Vector{T}"/> is used to accelerate
///         the per-row/per-column dot-product accumulation in the IDCT/FDCT passes and the
///         YCbCr&lt;-&gt;RGB color-conversion hot path, processing <see cref="Vector{T}.Count"/>
///         elements at a time with a scalar remainder loop for whatever does not evenly divide by
///         the runtime's vector width. This keeps the vectorized code correct on every hardware
///         width (including widths that do not evenly divide 8) without requiring a fixed SIMD
///         width, at the cost of falling back to scalar code for the remainder elements.
///     </para>
///     <para>
///         Architectural decision: <see cref="Save(Surface, System.IO.Stream, int)"/> always
///         encodes 3-component YCbCr with 4:2:0 chroma subsampling and never writes an APP0 (JFIF)
///         segment, since neither is required by the ITU-T T.81 base specification and this
///         codec's <c>Load</c> does not depend on APP0 being present; this mirrors
///         <see cref="TiffCodec"/>'s own precedent of writing only the segments required to
///         describe a valid, decodable file.
///     </para>
/// </remarks>
public static partial class JpegCodec
{
    // ------------------------------------------------------------------------------------------
    // Marker constants (all markers are 0xFF followed by the value below)
    // ------------------------------------------------------------------------------------------

    private const byte MarkerPrefix = 0xFF;
    private const byte MarkerSoi = 0xD8;
    private const byte MarkerEoi = 0xD9;
    private const byte MarkerSof0 = 0xC0;
    private const byte MarkerSof2 = 0xC2;
    private const byte MarkerDht = 0xC4;
    private const byte MarkerDqt = 0xDB;
    private const byte MarkerDri = 0xDD;
    private const byte MarkerSos = 0xDA;
    private const byte MarkerRst0 = 0xD0;
    private const byte MarkerRst7 = 0xD7;

    /// <summary>
    ///     A soft cap, in bytes, on <see cref="GetInfo(Stream)"/>'s fast incremental chunked-read
    ///     probe path. Chosen to comfortably bound even a pathological run of maximal-length
    ///     (65,535-byte) APPn/COM segments preceding the frame header (16 such segments alone
    ///     would consume roughly 1 MiB), while remaining minuscule next to the entropy-coded body
    ///     of any real photographic image, which this probe never needs to read.
    /// </summary>
    /// <remarks>
    ///     This is a soft cap only: once reached without finding a SOF0/SOF2 marker,
    ///     <see cref="GetInfo(Stream)"/> keeps scanning segment headers past the cap - one marker
    ///     segment at a time, exactly as it does below the cap - reading only as far as each
    ///     segment boundary actually requires, until a SOF0/SOF2 marker is found or the stream
    ///     genuinely ends. It never reads entropy-coded scan data, and it never bulk-reads or
    ///     drains the remainder of the stream merely because the cap was crossed, so
    ///     <see cref="GetInfo(Stream)"/> never throws merely because a file has more than
    ///     <c>MaxProbeHeaderBytes</c> of leading marker-segment data - as long as
    ///     <see cref="Load(Stream)"/> itself would successfully parse that file up to and
    ///     including the SOF marker. A file that is genuinely truncated or malformed (no SOF
    ///     marker anywhere, even after reading to end-of-stream) still throws
    ///     <see cref="InvalidDataException"/>, matching <see cref="Load(Stream)"/>'s own rejection
    ///     of the same bytes. Declared <see langword="internal"/> (rather than
    ///     <see langword="private"/>) so the test project (which the assembly already grants
    ///     <c>InternalsVisibleTo</c>) can construct fixtures that deliberately straddle this
    ///     threshold without hard-coding its value.
    /// </remarks>
    internal const int MaxProbeHeaderBytes = 1_048_576;

    /// <summary>
    ///     A hard ceiling, in bytes, on the total amount of leading marker-segment data
    ///     <see cref="GetInfo(Stream)"/> will ever read while scanning past the
    ///     <see cref="MaxProbeHeaderBytes"/> soft cap in search of a SOF0/SOF2 marker.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Unlike <see cref="MaxProbeHeaderBytes"/> - which is only a soft cap that scanning
    ///     deliberately continues past, one bounded segment at a time, so that files with
    ///     unusually large (but legitimate) leading metadata still probe successfully - this
    ///     limit is a genuine, non-negotiable ceiling: once reached without finding a SOF0/SOF2
    ///     marker, <see cref="GetInfo(Stream)"/> throws <see cref="InvalidDataException"/> rather
    ///     than continuing to read. Without it, a malformed, adversarial, or effectively-infinite
    ///     stream that never presents a SOF0/SOF2 marker (and never itself reaches end-of-stream)
    ///     would let the post-soft-cap fallback scan grow its buffer and read from the stream
    ///     without any upper bound, reintroducing the same denial-of-service exposure the soft
    ///     cap exists to prevent.
    ///     </para>
    ///     <para>
    ///     Sized at 16 MiB - sixteen times <see cref="MaxProbeHeaderBytes"/> - to comfortably
    ///     accommodate the small set of genuine real-world files that legitimately exceed the
    ///     1 MiB soft cap, such as JPEGs carrying large embedded ICC color profiles or XMP
    ///     metadata blocks, which can themselves reach several MiB, while still keeping the
    ///     total worst-case read/buffer size for <see cref="GetInfo(Stream)"/> a small, fixed,
    ///     documented multiple of realistic header sizes rather than unbounded.
    ///     </para>
    ///     <para>
    ///     Declared <see langword="internal"/> (rather than <see langword="private"/>), matching
    ///     <see cref="MaxProbeHeaderBytes"/>, so the test project (which the assembly already
    ///     grants <c>InternalsVisibleTo</c>) can construct fixtures that exercise this hard limit
    ///     without hard-coding its value.
    ///     </para>
    ///     <para>
    ///     This byte-based ceiling bounds the total <em>data volume</em> the post-soft-cap
    ///     fallback scan can read, but does not, by itself, cheaply bound the number of loop
    ///     iterations that scan performs: a marker segment can be as small as 4 bytes (a 2-byte
    ///     marker code plus a 2-byte length field), so a malformed or adversarial stream composed
    ///     entirely of minimal-size segments could still take on the order of millions of
    ///     iterations before this byte ceiling is reached. See
    ///     <see cref="MaxProbeSegmentCount"/> for the independent, iteration-count-based ceiling
    ///     that closes that gap.
    ///     </para>
    /// </remarks>
    internal const int MaxProbeHeaderBytesHardLimit = 16 * MaxProbeHeaderBytes;

    /// <summary>
    ///     A hard ceiling, independent of <see cref="MaxProbeHeaderBytesHardLimit"/>, on the total
    ///     number of marker segments <see cref="GetInfo(Stream)"/> will scan past the SOI marker
    ///     while searching for a SOF0/SOF2 marker.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <see cref="MaxProbeHeaderBytesHardLimit"/> bounds the total number of bytes the
    ///     post-soft-cap fallback scan can read, but a JPEG marker segment can be as small as
    ///     4 bytes (a 2-byte marker code plus a 2-byte length field), so a malformed or
    ///     adversarial stream composed entirely of minimal-size segments could still force on the
    ///     order of <c>MaxProbeHeaderBytesHardLimit / 4</c> (roughly four million) loop
    ///     iterations before that byte ceiling is ever reached. This constant bounds the number of
    ///     iterations directly, independently of the byte ceiling: once this many marker segments
    ///     have been scanned without finding a SOF0/SOF2 marker,
    ///     <see cref="GetInfo(Stream)"/> throws <see cref="InvalidDataException"/> rather than
    ///     scanning further segments - whichever of the two independent ceilings (this one, or
    ///     <see cref="MaxProbeHeaderBytesHardLimit"/>) is reached first triggers the throw.
    ///     </para>
    ///     <para>
    ///     Sized at 512 to give generous headroom for real-world JPEGs, where EXIF, ICC color
    ///     profile, XMP metadata, Adobe APP14, and multiple COM/thumbnail segments can realistically
    ///     combine to reach 20-30+ segments in unusual but still legitimate files - and to
    ///     comfortably exceed the roughly 260 maximal-length (65,535-byte) segments it takes to
    ///     reach the <see cref="MaxProbeHeaderBytesHardLimit"/> byte ceiling, so the two ceilings
    ///     remain genuinely independent (a file that legitimately needs to scan that many large
    ///     segments to reach the byte ceiling is not cut off early by the segment-count ceiling
    ///     instead) - while still bounding the worst-case iteration count of the post-soft-cap
    ///     scan to a small, fixed, documented value, orders of magnitude below the millions of
    ///     iterations a minimal-segment attack would otherwise force.
    ///     </para>
    ///     <para>
    ///     Declared <see langword="internal"/> (rather than <see langword="private"/>), matching
    ///     <see cref="MaxProbeHeaderBytes"/> and <see cref="MaxProbeHeaderBytesHardLimit"/>, so the
    ///     test project (which the assembly already grants <c>InternalsVisibleTo</c>) can
    ///     construct fixtures that exercise this limit without hard-coding its value.
    ///     </para>
    /// </remarks>
    internal const int MaxProbeSegmentCount = 512;

    /// <summary>
    ///     The standard 64-entry zigzag scan order: index <c>z</c> holds the natural (row-major)
    ///     8x8 index that zigzag position <c>z</c> maps to (ITU-T T.81 Figure A.6).
    /// </summary>
    private static readonly int[] ZigZagOrder =
    [
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    ];

    /// <summary>
    ///     The standard ITU-T T.81 Annex K example luminance quantization table, in natural
    ///     (row-major) order.
    /// </summary>
    private static readonly int[] StandardLuminanceQuantTable =
    [
        16, 11, 10, 16, 24, 40, 51, 61,
        12, 12, 14, 19, 26, 58, 60, 55,
        14, 13, 16, 24, 40, 57, 69, 56,
        14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68, 109, 103, 77,
        24, 35, 55, 64, 81, 104, 113, 92,
        49, 64, 78, 87, 103, 121, 120, 101,
        72, 92, 95, 98, 112, 100, 103, 99
    ];

    /// <summary>
    ///     The standard ITU-T T.81 Annex K example chrominance quantization table, in natural
    ///     (row-major) order.
    /// </summary>
    private static readonly int[] StandardChrominanceQuantTable =
    [
        17, 18, 24, 47, 99, 99, 99, 99,
        18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99,
        47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99
    ];

    private static readonly byte[] StandardDcLuminanceBits = [0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0];

    private static readonly byte[] StandardDcLuminanceValues = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    private static readonly byte[] StandardDcChrominanceBits = [0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0];

    private static readonly byte[] StandardDcChrominanceValues = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    private static readonly byte[] StandardAcLuminanceBits = [0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7D];

    private static readonly byte[] StandardAcLuminanceValues =
    [
        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12,
        0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
        0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08,
        0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0,
        0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16,
        0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
        0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
        0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
        0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
        0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
        0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
        0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
        0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
        0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7,
        0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6,
        0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5,
        0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4,
        0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
        0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA,
        0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA
    ];

    private static readonly byte[] StandardAcChrominanceBits = [0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77];

    private static readonly byte[] StandardAcChrominanceValues =
    [
        0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21,
        0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
        0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
        0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33, 0x52, 0xF0,
        0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34,
        0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19, 0x1A, 0x26,
        0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38,
        0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
        0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
        0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
        0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
        0x79, 0x7A, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
        0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96,
        0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5,
        0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4,
        0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3,
        0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2,
        0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
        0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9,
        0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA
    ];

    /// <summary>
    ///     Loads a <see cref="Surface"/> from an open, readable stream containing a supported JPEG
    ///     image (baseline SOF0 or progressive SOF2, 1 or 3 components, embedded Huffman/quantization
    ///     tables, 4:4:4/4:2:2/4:2:0 chroma subsampling, optional restart markers).
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the JPEG image from. Reading begins at the stream's current position
    ///     and consumes the entire remainder of the stream (the whole stream is buffered into
    ///     memory, since progressive JPEG requires multiple passes over the same coefficient data).
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/> containing the decoded pixels, always fully opaque (alpha
    ///     255), since JPEG has no alpha channel. Grayscale (1-component) images are expanded to
    ///     R=G=B=Y.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the stream does not contain a valid, supported JPEG image: it does not
    ///     start with the SOI marker, an unsupported SOF marker is present (anything other than
    ///     SOF0/SOF2, including arithmetic-coding variants), the component count is not 1 or 3,
    ///     the frame width or height exceeds <see cref="Surface.MaxDimension"/>, a DHT/DQT table
    ///     referenced by SOF/SOS is missing, a mandatory segment (SOF, DHT, DQT, or SOS) is
    ///     missing, the marker structure is malformed, the stream ends before all header or
    ///     entropy-coded data has been read, more than <see cref="MaxProbeHeaderBytesHardLimit"/>
    ///     bytes of leading marker-segment data precede the SOF0/SOF2 marker, or more than
    ///     <see cref="MaxProbeSegmentCount"/> non-terminating marker segments precede the
    ///     SOF0/SOF2 marker (the same two ceilings, and the same exceptions,
    ///     <see cref="GetInfo(Stream)"/> enforces for these conditions - see its remarks).
    /// </exception>
    /// <example>
    ///     <code>
    ///     using var stream = new MemoryStream();
    ///     var surface = new Surface(8, 8);
    ///     surface[0, 0] = new Rgba32(200, 20, 20, 255);
    ///
    ///     JpegCodec.Save(surface, stream, quality: 90);
    ///     stream.Position = 0;
    ///     var loaded = JpegCodec.Load(stream);
    ///     Console.WriteLine(loaded.Width); // Output: 8
    ///     </code>
    /// </example>
    public static Surface Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var file = ReadAllBytes(stream);
        return Decoder.Decode(file);
    }

    /// <summary>
    ///     Loads a <see cref="Surface"/> from a JPEG file at the specified path.
    /// </summary>
    /// <param name="path">The path of the JPEG file to load. Must not be null or empty.</param>
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
    ///     Reads only as much of a JPEG file's leading marker segments as necessary to find the
    ///     first SOF0/SOF2 (frame header) marker, and reports its declared dimensions and
    ///     component count, without ever reaching the entropy-coded scan data.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read the JPEG marker segments from. Reading begins at the stream's
    ///     current position; in the common case where the SOF segment appears within the first
    ///     <see cref="MaxProbeHeaderBytes"/> bytes (as it does for essentially all real-world
    ///     JPEG files), only that much is read incrementally. If that soft cap is reached without
    ///     finding a SOF0/SOF2 marker, scanning continues past it - one marker segment at a time,
    ///     the same way it does below the cap, never reading into entropy-coded scan data - so
    ///     <see cref="GetInfo(Stream)"/> never throws merely because a file has an unusually
    ///     large amount of leading marker-segment data, up to two independent ceilings: the
    ///     <see cref="MaxProbeHeaderBytesHardLimit"/> hard byte ceiling, and the
    ///     <see cref="MaxProbeSegmentCount"/> segment-count ceiling. A malformed or adversarial
    ///     stream that never presents a SOF0/SOF2 marker within either limit causes
    ///     <see cref="InvalidDataException"/> rather than unbounded reading.
    ///     </param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> describing the file's declared width, height, and component
    ///     count (1 for grayscale, 3 for YCbCr). <see cref="ImageInfo.HasAlpha"/> is always
    ///     <see langword="false"/>, since JPEG has no alpha channel.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="System.IO.InvalidDataException">
    ///     Thrown when the SOI marker is missing, an SOS marker or end-of-image is reached before
    ///     any SOF0/SOF2 marker is found, an unsupported SOF/frame marker (for example SOF1/SOF3,
    ///     the same markers <see cref="Load(Stream)"/> rejects) is encountered, no SOF0/SOF2
    ///     marker is found anywhere in the stream (a genuinely truncated or non-JPEG input) - the
    ///     same condition <see cref="Load(Stream)"/> itself would reject on the same bytes - or
    ///     either the <see cref="MaxProbeHeaderBytesHardLimit"/> byte ceiling or the
    ///     <see cref="MaxProbeSegmentCount"/> segment-count ceiling is reached without a SOF0/SOF2
    ///     marker ever being found.
    ///     <see cref="Surface.MaxDimension"/> is <em>not</em> enforced - the raw header-declared
    ///     values are always returned; see <see cref="ImageInfo"/> for why.
    /// </exception>
    /// <remarks>
    ///     <see cref="ImageInfo"/>'s type-level remarks document the general cross-codec
    ///     invariant that <c>GetInfo</c> never fails on an input <see cref="Load(Stream)"/> would
    ///     accept. For JPEG this is a genuine, unconditional agreement, not merely a best effort:
    ///     <see cref="Load(Stream)"/> enforces the exact same two independent ceilings on its own
    ///     pre-SOF marker-segment walk that this method enforces - <see cref="MaxProbeHeaderBytesHardLimit"/>
    ///     (16 MiB of leading marker-segment data) and <see cref="MaxProbeSegmentCount"/> (512
    ///     non-terminating marker segments) - throwing the identical <see cref="InvalidDataException"/>
    ///     this method throws for the same condition. A pathological JPEG whose leading
    ///     marker-segment data before any SOF0/SOF2 marker exceeds either ceiling is therefore
    ///     rejected consistently by both methods; there is no input this method rejects that
    ///     <see cref="Load(Stream)"/> would otherwise have accepted.
    /// </remarks>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return Decoder.ProbeDimensions(stream);
    }

    /// <summary>
    ///     Reads a JPEG file's leading marker segments at the specified path and reports its
    ///     declared dimensions and component count, without decoding any entropy-coded scan data.
    /// </summary>
    /// <param name="path">The path of the JPEG file to inspect. Must not be null or empty.</param>
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
    ///     Saves a <see cref="Surface"/> to a stream as a baseline, 3-component YCbCr, 4:2:0
    ///     chroma-subsampled JPEG image at the specified quality.
    /// </summary>
    /// <param name="surface">The pixel buffer to save. Must not be null.</param>
    /// <param name="stream">The stream to write the JPEG image to. Must not be null.</param>
    /// <param name="quality">
    ///     The JPEG quality, on the standard 1-100 scale (higher is better quality and larger file
    ///     size). Defaults to 90.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="stream"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="quality"/> is less than 1 or greater than 100.
    /// </exception>
    /// <example>
    ///     <code>
    ///     using var stream = new MemoryStream();
    ///     var surface = new Surface(8, 8);
    ///     surface[0, 0] = new Rgba32(20, 200, 20, 255);
    ///
    ///     JpegCodec.Save(surface, stream, quality: 90);
    ///     stream.Position = 0;
    ///     var loaded = JpegCodec.Load(stream);
    ///     Console.WriteLine(loaded.Width); // Output: 8
    ///     </code>
    /// </example>
    public static void Save(Surface surface, Stream stream, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(stream);
        if (quality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Quality must be between 1 and 100 inclusive.");
        }

        Encoder.Encode(surface, stream, quality);
    }

    /// <summary>
    ///     Saves a <see cref="Surface"/> to a file as a baseline, 3-component YCbCr, 4:2:0
    ///     chroma-subsampled JPEG image at the specified quality.
    /// </summary>
    /// <param name="surface">The pixel buffer to save. Must not be null.</param>
    /// <param name="path">The destination file path. Must not be null or empty.</param>
    /// <param name="quality">
    ///     The JPEG quality; see <see cref="Save(Surface, Stream, int)"/> for the default and
    ///     range.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="surface"/> or <paramref name="path"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="quality"/> is less than 1 or greater than 100.
    /// </exception>
    /// <remarks>
    ///     Any existing file at <paramref name="path"/> is overwritten. File-system exceptions
    ///     (for example <see cref="UnauthorizedAccessException"/>, <see cref="DirectoryNotFoundException"/>,
    ///     or <see cref="IOException"/>) raised while creating <paramref name="path"/> propagate
    ///     uncaught to the caller. See <see cref="Save(Surface, Stream, int)"/> for the written
    ///     file's exact contents.
    /// </remarks>
    public static void Save(Surface surface, string path, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        Save(surface, stream, quality);
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

}
