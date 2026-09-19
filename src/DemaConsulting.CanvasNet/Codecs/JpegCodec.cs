using System.Numerics;
using CanvasNet.Canvas;

namespace CanvasNet.Codecs;

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
public static class JpegCodec
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
    ///     missing, the marker structure is malformed, or the stream ends before all header or
    ///     entropy-coded data has been read.
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

    // ================================================================================================
    // Shared Huffman table construction (used by both the decoder's DHT tables and the encoder's
    // Annex K default tables)
    // ================================================================================================

    /// <summary>
    ///     A canonical Huffman decode table built from 16 code-length counts and a flat symbol
    ///     list, using the min-code/max-code/val-pointer arrays described in ITU-T T.81 Annex F.
    /// </summary>
    private sealed class HuffmanTable
    {
        public readonly int[] MinCode = new int[17];
        public readonly int[] MaxCode = new int[17];
        public readonly int[] ValPtr = new int[17];
        public required byte[] Values;

        /// <summary>
        ///     Builds a canonical Huffman table from 16 code-length counts (<paramref name="bits"/>,
        ///     indices 0-15 correspond to code lengths 1-16) and the flat symbol list in code order.
        /// </summary>
        public static HuffmanTable Build(byte[] bits, byte[] values)
        {
            var table = new HuffmanTable { Values = values };
            for (var length = 1; length <= 16; length++)
            {
                table.MaxCode[length] = -1;
            }

            var code = 0;
            var pointer = 0;
            for (var length = 1; length <= 16; length++)
            {
                var count = bits[length - 1];
                if (count > 0)
                {
                    table.ValPtr[length] = pointer;
                    table.MinCode[length] = code;
                    code += count;
                    pointer += count;
                    table.MaxCode[length] = code - 1;
                }

                code <<= 1;
            }

            return table;
        }
    }

    /// <summary>
    ///     Reads bits MSB-first from a JPEG entropy-coded data region, transparently removing
    ///     byte-stuffing (<c>0xFF 0x00</c> -&gt; literal <c>0xFF</c>) and throwing
    ///     <see cref="InvalidDataException"/> if an unstuffed marker is encountered where entropy
    ///     data was expected.
    /// </summary>
    private sealed class BitReader(byte[] data, int start)
    {
        private int _bitBuffer;
        private int _bitCount;

        public int Position { get; private set; } = start;

        public int ReadBit()
        {
            if (_bitCount == 0)
            {
                if (Position >= data.Length)
                {
                    throw new InvalidDataException("Unexpected end of stream while reading JPEG entropy-coded data.");
                }

                var b = data[Position++];
                if (b == MarkerPrefix)
                {
                    if (Position >= data.Length || data[Position] != 0x00)
                    {
                        throw new InvalidDataException("Unexpected marker encountered while reading JPEG entropy-coded data.");
                    }

                    Position++;
                }

                _bitBuffer = b;
                _bitCount = 8;
            }

            _bitCount--;
            return (_bitBuffer >> _bitCount) & 1;
        }

        public int ReadBits(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++)
            {
                value = (value << 1) | ReadBit();
            }

            return value;
        }

        /// <summary>
        ///     Discards any unread bits remaining in the current byte, aligning the reader to the
        ///     next byte boundary (used before expecting a restart marker).
        /// </summary>
        public void Realign() => _bitCount = 0;

        /// <summary>
        ///     Consumes an expected restart marker (<c>0xFFD0</c>-<c>0xFFD7</c>) at the current byte
        ///     position, throwing <see cref="InvalidDataException"/> if one is not present.
        /// </summary>
        public void ExpectRestartMarker()
        {
            if (Position + 1 >= data.Length || data[Position] != MarkerPrefix ||
                data[Position + 1] < MarkerRst0 || data[Position + 1] > MarkerRst7)
            {
                throw new InvalidDataException("Expected a JPEG restart marker but did not find one.");
            }

            Position += 2;
        }

        public static int Decode(HuffmanTable table, BitReader reader)
        {
            var code = reader.ReadBit();
            var length = 1;
            while (code > table.MaxCode[length])
            {
                code = (code << 1) | reader.ReadBit();
                length++;
                if (length > 16)
                {
                    throw new InvalidDataException("Invalid JPEG Huffman code encountered.");
                }
            }

            return table.Values[table.ValPtr[length] + (code - table.MinCode[length])];
        }

        /// <summary>
        ///     Implements the standard JPEG "EXTEND" procedure (ITU-T T.81 Annex F.2.2.1): reads
        ///     <paramref name="size"/> magnitude bits and sign-extends them to a signed value.
        /// </summary>
        public int Receive(int size)
        {
            if (size == 0)
            {
                return 0;
            }

            var value = ReadBits(size);
            var threshold = 1 << (size - 1);
            return value < threshold ? value - (1 << size) + 1 : value;
        }
    }

    /// <summary>
    ///     Writes bits MSB-first to a JPEG entropy-coded data region, transparently applying
    ///     byte-stuffing (<c>0xFF</c> -&gt; <c>0xFF 0x00</c>).
    /// </summary>
    private sealed class BitWriter(Stream stream)
    {
        private int _bitBuffer;
        private int _bitCount;

        public void WriteBits(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                WriteBit((value >> i) & 1);
            }
        }

        private void WriteBit(int bit)
        {
            _bitBuffer = (_bitBuffer << 1) | bit;
            _bitCount++;
            if (_bitCount == 8)
            {
                FlushByte();
            }
        }

        private void FlushByte()
        {
            var b = (byte)_bitBuffer;
            stream.WriteByte(b);
            if (b == MarkerPrefix)
            {
                stream.WriteByte(0x00);
            }

            _bitBuffer = 0;
            _bitCount = 0;
        }

        /// <summary>
        ///     Pads the current partial byte with 1-bits and flushes it, as required before
        ///     writing a marker (ITU-T T.81 Section F.1.2.3).
        /// </summary>
        public void FlushWithPadding()
        {
            while (_bitCount != 0)
            {
                WriteBit(1);
            }
        }
    }

    // ================================================================================================
    // Shared 8x8 DCT math and color conversion (used by both Decoder and Encoder)
    // ================================================================================================

    /// <summary>
    ///     Computes the dot product of two 8-element arrays, using <see cref="Vector{T}"/> to
    ///     process <see cref="Vector{T}.Count"/> elements at a time with a scalar remainder loop
    ///     for whatever does not evenly divide 8.
    /// </summary>
    /// <remarks>
    ///     Architectural decision: this is the vectorized hot path shared by every row/column pass
    ///     of both the inverse and forward 8x8 DCT. Because a fixed vector width of 8
    ///     is not guaranteed on every runtime/hardware combination, this falls back to scalar
    ///     multiply-accumulate for the elements that do not fit a whole vector.
    /// </remarks>
    internal static double DotProduct8(double[] a, double[] b)
    {
        var count = Vector<double>.Count;
        var sum = 0.0;
        var i = 0;
        while (count > 0 && i + count <= 8)
        {
            sum += Vector.Dot(new Vector<double>(a, i), new Vector<double>(b, i));
            i += count;
        }

        for (; i < 8; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    private static double[][] NewBlock()
    {
        var block = new double[8][];
        for (var i = 0; i < 8; i++)
        {
            block[i] = new double[8];
        }

        return block;
    }

    private static readonly double[][] Basis = BuildBasis();
    private static readonly double[][] BasisT = Transpose(Basis);

    private static double[][] BuildBasis()
    {
        var basis = NewBlock();
        for (var k = 0; k < 8; k++)
        {
            var ck = k == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
            for (var n = 0; n < 8; n++)
            {
                basis[k][n] = ck * Math.Cos((2.0 * n + 1) * k * Math.PI / 16.0);
            }
        }

        return basis;
    }

    private static double[][] Transpose(double[][] source)
    {
        var result = NewBlock();
        for (var i = 0; i < 8; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                result[j][i] = source[i][j];
            }
        }

        return result;
    }

    private static byte ClampToByte(double value) => value switch
    {
        <= 0 => 0,
        >= 255 => 255,
        _ => (byte)Math.Round(value, MidpointRounding.AwayFromZero)
    };

    /// <summary>
    ///     Converts one row of Y/Cb/Cr samples to RGB using the ITU-R BT.601 coefficients, with
    ///     proper clamping to [0,255].
    /// </summary>
    /// <remarks>
    ///     Architectural decision: this is the vectorized color-conversion hot path, processing
    ///     <see cref="Vector{T}.Count"/> pixels at a time with a scalar remainder loop. It is
    ///     cross-checked against <see cref="ConvertYCbCrRowToRgbScalar"/> (a plain per-pixel
    ///     reference implementation of the same formula) by
    ///     <c>JpegCodecTests.JpegCodec_ConvertYCbCrRowToRgb_VectorAndScalarRemainder_MatchesScalarReference</c>.
    /// </remarks>
    internal static void ConvertYCbCrRowToRgb(
        byte[] y, byte[] cb, byte[] cr, byte[] r, byte[] g, byte[] b, int length)
    {
        var count = Vector<float>.Count;
        var i = 0;
        var yBuf = new float[count];
        var cbBuf = new float[count];
        var crBuf = new float[count];
        while (count > 0 && i + count <= length)
        {
            for (var k = 0; k < count; k++)
            {
                yBuf[k] = y[i + k];
                cbBuf[k] = cb[i + k] - 128f;
                crBuf[k] = cr[i + k] - 128f;
            }

            var yv = new Vector<float>(yBuf);
            var cbv = new Vector<float>(cbBuf);
            var crv = new Vector<float>(crBuf);

            var rv = yv + crv * new Vector<float>(1.402f);
            var gv = yv - cbv * new Vector<float>(0.344136f) - crv * new Vector<float>(0.714136f);
            var bv = yv + cbv * new Vector<float>(1.772f);

            for (var k = 0; k < count; k++)
            {
                r[i + k] = ClampToByte(rv[k]);
                g[i + k] = ClampToByte(gv[k]);
                b[i + k] = ClampToByte(bv[k]);
            }

            i += count;
        }

        for (; i < length; i++)
        {
            ConvertOnePixel(y[i], cb[i], cr[i], out r[i], out g[i], out b[i]);
        }
    }

    /// <summary>
    ///     A plain, per-pixel scalar reference implementation of the same YCbCr-&gt;RGB formula as
    ///     <see cref="ConvertYCbCrRowToRgb"/>, used only as the "known good" comparison target by
    ///     the SIMD-vs-scalar cross-check test.
    /// </summary>
    internal static void ConvertYCbCrRowToRgbScalar(
        byte[] y, byte[] cb, byte[] cr, byte[] r, byte[] g, byte[] b, int length)
    {
        for (var i = 0; i < length; i++)
        {
            ConvertOnePixel(y[i], cb[i], cr[i], out r[i], out g[i], out b[i]);
        }
    }

    private static void ConvertOnePixel(byte y, byte cb, byte cr, out byte r, out byte g, out byte b)
    {
        var cbf = cb - 128f;
        var crf = cr - 128f;
        r = ClampToByte(y + 1.402f * crf);
        g = ClampToByte(y - 0.344136f * cbf - 0.714136f * crf);
        b = ClampToByte(y + 1.772f * cbf);
    }

    // ================================================================================================
    // Decoder
    // ================================================================================================

    private static class Decoder
    {
        private sealed class Component
        {
            public required int Id;
            public required int H;
            public required int V;
            public required int QuantSelector;
            public int DcSelector;
            public int AcSelector;
            public int DcPredictor;

            /// <summary>
            ///     One entry per block, in row-major order over the component's full MCU-grid
            ///     block dimensions (<see cref="BlocksPerLineMcu"/> x <see cref="BlocksPerColumnMcu"/>),
            ///     each holding 64 coefficients in zigzag scan order.
            /// </summary>
            public int[][]? Blocks;

            public int BlocksPerLineMcu;
            public int BlocksPerColumnMcu;
        }

        public static Surface Decode(byte[] file)
        {
            if (file.Length < 4 || file[0] != MarkerPrefix || file[1] != MarkerSoi)
            {
                throw new InvalidDataException("Not a JPEG file (missing SOI marker).");
            }

            var state = new DecodeState();

            var pos = 2;
            while (true)
            {
                pos = SkipToMarker(file, pos);
                var marker = file[pos + 1];
                pos += 2;

                if (marker == MarkerEoi)
                {
                    break;
                }

                pos = ProcessSegment(file, pos, marker, state);
            }

            if (!state.SofSeen || !state.SosSeen || state.Components == null)
            {
                throw new InvalidDataException("JPEG stream is missing a mandatory SOF or SOS segment.");
            }

            return AssembleCanvas(state.Width, state.Height, state.Components, state.QuantTables);
        }

        /// <summary>
        ///     Mutable state threaded through <see cref="ProcessSegment"/> while <see cref="Decode"/>
        ///     walks the marker segments of a JPEG stream, accumulating the quantization/Huffman
        ///     tables and frame parameters needed once the SOS-terminated scan is fully decoded.
        /// </summary>
        private sealed class DecodeState
        {
            /// <summary>Quantization tables keyed by table selector, populated by DQT segments.</summary>
            public Dictionary<int, int[]> QuantTables { get; } = [];

            /// <summary>DC Huffman tables keyed by table selector, populated by DHT segments.</summary>
            public Dictionary<int, HuffmanTable> DcTables { get; } = [];

            /// <summary>AC Huffman tables keyed by table selector, populated by DHT segments.</summary>
            public Dictionary<int, HuffmanTable> AcTables { get; } = [];

            /// <summary>The frame's component descriptors, populated by the SOF segment.</summary>
            public Component[]? Components { get; set; }

            /// <summary>The frame width in pixels, populated by the SOF segment.</summary>
            public int Width { get; set; }

            /// <summary>The frame height in pixels, populated by the SOF segment.</summary>
            public int Height { get; set; }

            /// <summary>Whether the frame uses progressive (SOF2) rather than baseline (SOF0) encoding.</summary>
            public bool Progressive { get; set; }

            /// <summary>The restart interval in MCUs, populated by a DRI segment (0 if none seen).</summary>
            public int RestartInterval { get; set; }

            /// <summary>Whether a SOF segment has been seen yet.</summary>
            public bool SofSeen { get; set; }

            /// <summary>Whether a SOS segment has been seen yet.</summary>
            public bool SosSeen { get; set; }
        }

        /// <summary>
        ///     Processes a single marker segment encountered by <see cref="Decode"/> (DQT, DHT, DRI,
        ///     SOF0/SOF2, SOS, a stray restart marker, an unsupported SOF variant, or a generically
        ///     skipped length-prefixed segment such as APPn/COM), updating <paramref name="state"/>
        ///     in place and returning the stream position immediately following the segment.
        /// </summary>
        private static int ProcessSegment(byte[] file, int pos, int marker, DecodeState state)
        {
            switch (marker)
            {
                case MarkerDqt:
                    return ReadDqt(file, pos, state.QuantTables);

                case MarkerDht:
                    return ReadDht(file, pos, state.DcTables, state.AcTables);

                case MarkerDri:
                    var driLength = ReadUInt16Be(file, pos);
                    state.RestartInterval = ReadUInt16Be(file, pos + 2);
                    return pos + driLength;

                case MarkerSof0:
                case MarkerSof2:
                    if (state.SofSeen)
                    {
                        throw new InvalidDataException("Multiple SOF markers are not supported.");
                    }

                    state.Progressive = marker == MarkerSof2;
                    var sofPos = ReadSof(file, pos, out var width, out var height, out var components);
                    state.Width = width;
                    state.Height = height;
                    state.Components = components;
                    state.SofSeen = true;
                    return sofPos;

                case MarkerSos:
                    if (!state.SofSeen)
                    {
                        throw new InvalidDataException("SOS marker encountered before any SOF marker.");
                    }

                    var scanContext = new ScanDecodeContext(
                        state.Components!, state.DcTables, state.AcTables, state.Progressive, state.RestartInterval);
                    var sosPos = DecodeScan(file, pos, state.Width, state.Height, scanContext);
                    state.SosSeen = true;
                    return sosPos;

                case >= 0xD0 and <= 0xD7:
                    // Stray restart marker outside entropy-coded data; ignore.
                    return pos;

                case 0xC1 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or
                     0xCD or 0xCE or 0xCF:
                    throw new InvalidDataException(
                        $"Unsupported JPEG SOF marker 0x{marker:X2}; only baseline (SOF0) and progressive (SOF2) are supported.");

                case 0xC8 or 0xCC:
                    throw new InvalidDataException("Arithmetic-coded and JPG-extension JPEG variants are not supported.");

                default:
                    // APPn, COM, and any other length-prefixed segment we do not act on: skip.
                    var length = ReadUInt16Be(file, pos);
                    return pos + length;
            }
        }

        private static int SkipToMarker(byte[] file, int pos)
        {
            while (pos < file.Length && file[pos] != MarkerPrefix)
            {
                pos++;
            }

            // Skip any run of fill bytes (multiple consecutive 0xFF) before the actual marker code.
            while (pos + 1 < file.Length && file[pos + 1] == MarkerPrefix)
            {
                pos++;
            }

            if (pos + 1 >= file.Length)
            {
                throw new InvalidDataException("Unexpected end of stream while looking for a JPEG marker.");
            }

            return pos;
        }

        private static ushort ReadUInt16Be(byte[] file, int offset)
        {
            if (offset + 1 >= file.Length)
            {
                throw new InvalidDataException("Unexpected end of stream while reading a JPEG segment length.");
            }

            return (ushort)((file[offset] << 8) | file[offset + 1]);
        }

        /// <summary>
        /// Reads a single byte from the file at the given position, throwing
        /// <see cref="InvalidDataException"/> (rather than an unhandled
        /// <see cref="IndexOutOfRangeException"/>) if the position is outside the bounds
        /// of the file. This is used to bounds-check segment payload parsing where a
        /// corrupt or truncated segment length would otherwise cause an unguarded array read.
        /// </summary>
        /// <param name="file">File bytes.</param>
        /// <param name="pos">Position to read from.</param>
        /// <returns>Byte at the given position.</returns>
        /// <exception cref="InvalidDataException">Position is outside the bounds of the file.</exception>
        private static byte ReadByte(byte[] file, int pos)
        {
            if (pos < 0 || pos >= file.Length)
            {
                throw new InvalidDataException("Unexpected end of stream while reading a JPEG segment payload.");
            }

            return file[pos];
        }

        private static int ReadDqt(byte[] file, int pos, Dictionary<int, int[]> quantTables)
        {
            var length = ReadUInt16Be(file, pos);
            var end = pos + length;
            var p = pos + 2;
            while (p < end)
            {
                var precisionAndId = ReadByte(file, p++);
                var precision = precisionAndId >> 4;
                var id = precisionAndId & 0xF;
                var table = new int[64];
                for (var i = 0; i < 64; i++)
                {
                    if (precision == 0)
                    {
                        table[i] = ReadByte(file, p++);
                    }
                    else
                    {
                        table[i] = (ReadByte(file, p) << 8) | ReadByte(file, p + 1);
                        p += 2;
                    }
                }

                quantTables[id] = table;
            }

            return end;
        }

        private static int ReadDht(
            byte[] file, int pos, Dictionary<int, HuffmanTable> dcTables, Dictionary<int, HuffmanTable> acTables)
        {
            var length = ReadUInt16Be(file, pos);
            var end = pos + length;
            var p = pos + 2;
            while (p < end)
            {
                var classAndId = ReadByte(file, p++);
                var tableClass = classAndId >> 4;
                var id = classAndId & 0xF;
                var bits = new byte[16];
                var totalSymbols = 0;
                for (var i = 0; i < 16; i++)
                {
                    bits[i] = ReadByte(file, p++);
                    totalSymbols += bits[i];
                }

                if (p + totalSymbols > file.Length)
                {
                    throw new InvalidDataException("Unexpected end of stream while reading a JPEG DHT segment payload.");
                }

                var values = new byte[totalSymbols];
                Array.Copy(file, p, values, 0, totalSymbols);
                p += totalSymbols;

                var table = HuffmanTable.Build(bits, values);
                if (tableClass == 0)
                {
                    dcTables[id] = table;
                }
                else
                {
                    acTables[id] = table;
                }
            }

            return end;
        }

        private static int ReadSof(byte[] file, int pos, out int width, out int height, out Component[] components)
        {
            var length = ReadUInt16Be(file, pos);
            var end = pos + length;
            var p = pos + 2;

            var precision = ReadByte(file, p++);
            if (precision != 8)
            {
                throw new InvalidDataException($"Unsupported JPEG sample precision {precision}; only 8-bit is supported.");
            }

            height = (ReadByte(file, p) << 8) | ReadByte(file, p + 1);
            p += 2;
            width = (ReadByte(file, p) << 8) | ReadByte(file, p + 1);
            p += 2;

            if (width <= 0 || height <= 0)
            {
                throw new InvalidDataException($"Invalid JPEG dimensions {width}x{height}.");
            }

            // Reject dimensions above Surface.MaxDimension here, before ReadSof returns and
            // before any width/height arithmetic (e.g. MCU-grid block sizing performed while
            // decoding the SOS-terminated scan, well before AssembleCanvas constructs the
            // Surface) is performed, so an oversized value surfaces as the documented
            // InvalidDataException rather than an ArgumentOutOfRangeException escaping from
            // deep inside Surface's constructor
            if (width > Surface.MaxDimension || height > Surface.MaxDimension)
            {
                throw new InvalidDataException(
                    $"JPEG dimensions {width}x{height} exceed the maximum supported size of " +
                    $"{Surface.MaxDimension}x{Surface.MaxDimension}.");
            }

            var numComponents = ReadByte(file, p++);
            if (numComponents != 1 && numComponents != 3)
            {
                throw new InvalidDataException(
                    $"Unsupported JPEG component count {numComponents}; only 1 (grayscale) and 3 (YCbCr) are supported. " +
                    "4-component (CMYK/YCCK) JPEG images are not supported.");
            }

            components = new Component[numComponents];
            for (var i = 0; i < numComponents; i++)
            {
                var id = ReadByte(file, p++);
                var samplingFactors = ReadByte(file, p++);
                var h = samplingFactors >> 4;
                var v = samplingFactors & 0xF;
                var quantSelector = ReadByte(file, p++);
                if (h is not (1 or 2) || v is not (1 or 2))
                {
                    throw new InvalidDataException(
                        $"Unsupported JPEG sampling factors {h}x{v} for component {id}; only 1 and 2 are supported.");
                }

                components[i] = new Component { Id = id, H = h, V = v, QuantSelector = quantSelector };
            }

            if (p != end)
            {
                throw new InvalidDataException("Malformed JPEG SOF segment length.");
            }

            return end;
        }

        /// <summary>
        ///     The tables and frame-level parameters shared by every scan within a JPEG stream,
        ///     grouped into a single parameter so <see cref="DecodeScan"/> does not need to accept
        ///     each one individually.
        /// </summary>
        private readonly record struct ScanDecodeContext(
            Component[] Components,
            Dictionary<int, HuffmanTable> DcTables,
            Dictionary<int, HuffmanTable> AcTables,
            bool Progressive,
            int RestartInterval);

        /// <summary>
        ///     The component selectors and spectral-selection/successive-approximation parameters
        ///     parsed from a single SOS segment, produced by <see cref="ParseScanHeader"/>.
        /// </summary>
        private readonly record struct ScanHeader(Component[] ScanComponents, int Ss, int Se, int Ah, int Al);

        /// <summary>
        ///     The MCU-grid dimensions shared by every scan in the frame, derived from the maximum
        ///     component sampling factors and the frame's pixel dimensions.
        /// </summary>
        private readonly record struct McuGrid(int HMax, int VMax, int McusAcross, int McusDown);

        private static int DecodeScan(byte[] file, int pos, int frameWidth, int frameHeight, ScanDecodeContext context)
        {
            var (header, p) = ParseScanHeader(file, pos, context.Components, context.Progressive);

            // Determine (and allocate, on first use) the MCU-grid dimensions shared by every scan.
            var hMax = context.Components.Max(c => c.H);
            var vMax = context.Components.Max(c => c.V);
            var mcusAcross = (frameWidth + (8 * hMax) - 1) / (8 * hMax);
            var mcusDown = (frameHeight + (8 * vMax) - 1) / (8 * vMax);
            var grid = new McuGrid(hMax, vMax, mcusAcross, mcusDown);

            EnsureComponentBlocksAllocated(context.Components, mcusAcross, mcusDown);

            foreach (var component in header.ScanComponents)
            {
                component.DcPredictor = 0;
            }

            var reader = new BitReader(file, p);

            return DecodeScanUnits(reader, header, context, frameWidth, frameHeight, grid);
        }

        /// <summary>
        ///     Parses a SOS segment's component selectors (assigning each referenced component's
        ///     DC/AC Huffman table selectors) and spectral-selection/successive-approximation
        ///     parameters, validating the segment length and baseline spectral-range constraints.
        /// </summary>
        private static (ScanHeader Header, int Position) ParseScanHeader(
            byte[] file, int pos, Component[] components, bool progressive)
        {
            var length = ReadUInt16Be(file, pos);
            var p = pos + 2;
            var numComponentsInScan = ReadByte(file, p++);
            var scanComponents = new Component[numComponentsInScan];
            for (var i = 0; i < numComponentsInScan; i++)
            {
                var selector = ReadByte(file, p++);
                var tableSelectors = ReadByte(file, p++);
                var component = Array.Find(components, c => c.Id == selector) ??
                                 throw new InvalidDataException($"SOS references unknown component id {selector}.");
                component.DcSelector = tableSelectors >> 4;
                component.AcSelector = tableSelectors & 0xF;
                scanComponents[i] = component;
            }

            var ss = ReadByte(file, p++);
            var se = ReadByte(file, p++);
            var ahAl = ReadByte(file, p++);
            var ah = ahAl >> 4;
            var al = ahAl & 0xF;

            if (p != pos + length)
            {
                throw new InvalidDataException("Malformed JPEG SOS segment length.");
            }

            if (!progressive && (ss != 0 || se != 63 || ah != 0 || al != 0))
            {
                throw new InvalidDataException("Baseline JPEG scan must cover the full spectral range with no successive approximation.");
            }

            return (new ScanHeader(scanComponents, ss, se, ah, al), p);
        }

        /// <summary>
        ///     Allocates each component's block storage the first time it is referenced by any
        ///     scan, sized to the shared MCU grid so later scans referencing the same component
        ///     reuse the same block array.
        /// </summary>
        private static void EnsureComponentBlocksAllocated(Component[] components, int mcusAcross, int mcusDown)
        {
            foreach (var component in components.Where(component => component.Blocks == null))
            {
                component.BlocksPerLineMcu = mcusAcross * component.H;
                component.BlocksPerColumnMcu = mcusDown * component.V;
                var count = component.BlocksPerLineMcu * component.BlocksPerColumnMcu;
                component.Blocks = new int[count][];
                for (var i = 0; i < count; i++)
                {
                    component.Blocks[i] = new int[64];
                }
            }
        }

        /// <summary>
        ///     Decodes every MCU (interleaved scan) or block (non-interleaved scan) in the scan,
        ///     honoring restart markers at the configured restart interval, and returns the stream
        ///     position immediately following the last decoded unit.
        /// </summary>
        private static int DecodeScanUnits(
            BitReader reader, ScanHeader header, ScanDecodeContext context, int frameWidth, int frameHeight, McuGrid grid)
        {
            var eobRun = 0;
            var scanComponents = header.ScanComponents;
            var interleaved = scanComponents.Length > 1;
            int totalUnits;
            int nonInterleavedBlocksPerLine = 0;
            if (interleaved)
            {
                totalUnits = grid.McusAcross * grid.McusDown;
            }
            else
            {
                var comp = scanComponents[0];
                var compSamplesPerLine = ((frameWidth * comp.H) + grid.HMax - 1) / grid.HMax;
                var compSamplesPerColumn = ((frameHeight * comp.V) + grid.VMax - 1) / grid.VMax;
                nonInterleavedBlocksPerLine = (compSamplesPerLine + 7) / 8;
                var blocksPerColumn = (compSamplesPerColumn + 7) / 8;
                totalUnits = nonInterleavedBlocksPerLine * blocksPerColumn;
            }

            var unitsSinceRestart = 0;
            var blockContext = new BlockDecodeContext(
                context.DcTables, context.AcTables, context.Progressive, header.Ss, header.Se, header.Ah, header.Al);
            for (var unit = 0; unit < totalUnits; unit++)
            {
                if (context.RestartInterval > 0 && unitsSinceRestart == context.RestartInterval)
                {
                    reader.Realign();
                    reader.ExpectRestartMarker();
                    foreach (var component in scanComponents)
                    {
                        component.DcPredictor = 0;
                    }

                    eobRun = 0;
                    unitsSinceRestart = 0;
                }

                if (interleaved)
                {
                    DecodeInterleavedUnit(unit, grid.McusAcross, scanComponents, reader, blockContext, ref eobRun);
                }
                else
                {
                    var comp = scanComponents[0];
                    var blockCol = unit % nonInterleavedBlocksPerLine;
                    var blockRow = unit / nonInterleavedBlocksPerLine;
                    var block = comp.Blocks![(blockRow * comp.BlocksPerLineMcu) + blockCol];
                    DecodeBlock(block, comp, reader, blockContext, ref eobRun);
                }

                unitsSinceRestart++;
            }

            return reader.Position;
        }

        /// <summary>
        ///     Decodes every block of every scan component making up a single interleaved MCU at
        ///     the given MCU index.
        /// </summary>
        private static void DecodeInterleavedUnit(
            int unit,
            int mcusAcross,
            Component[] scanComponents,
            BitReader reader,
            BlockDecodeContext blockContext,
            ref int eobRun)
        {
            var mcuX = unit % mcusAcross;
            var mcuY = unit / mcusAcross;
            foreach (var component in scanComponents)
            {
                for (var v = 0; v < component.V; v++)
                {
                    for (var h = 0; h < component.H; h++)
                    {
                        var blockCol = (mcuX * component.H) + h;
                        var blockRow = (mcuY * component.V) + v;
                        var block = component.Blocks![(blockRow * component.BlocksPerLineMcu) + blockCol];
                        DecodeBlock(block, component, reader, blockContext, ref eobRun);
                    }
                }
            }
        }

        /// <summary>
        ///     The Huffman tables and spectral-selection/successive-approximation parameters
        ///     shared by every block decoded within a single scan, grouped into a single parameter
        ///     so <see cref="DecodeBlock"/> does not need to accept each one individually.
        /// </summary>
        private readonly record struct BlockDecodeContext(
            Dictionary<int, HuffmanTable> DcTables,
            Dictionary<int, HuffmanTable> AcTables,
            bool Progressive,
            int Ss,
            int Se,
            int Ah,
            int Al);

        private static void DecodeBlock(int[] block, Component component, BitReader reader, BlockDecodeContext context, ref int eobRun)
        {
            if (!context.Progressive)
            {
                DecodeBaselineBlock(block, component, reader, context.DcTables, context.AcTables);
                return;
            }

            if (context.Ss == 0)
            {
                DecodeProgressiveDc(block, component, reader, context.DcTables, context.Ah, context.Al);
                return;
            }

            var acTableProgressive = GetTable(context.AcTables, component.AcSelector, "AC");
            if (context.Ah == 0)
            {
                DecodeAcFirst(block, acTableProgressive, reader, context.Ss, context.Se, context.Al, ref eobRun);
            }
            else
            {
                DecodeAcRefine(block, acTableProgressive, reader, context.Ss, context.Se, context.Al, ref eobRun);
            }
        }

        /// <summary>
        ///     Decodes a single baseline (sequential DCT) block: the DC coefficient via
        ///     differential prediction, followed by all non-zero AC coefficients in zigzag order
        ///     up to the first end-of-block run or index 63.
        /// </summary>
        private static void DecodeBaselineBlock(
            int[] block,
            Component component,
            BitReader reader,
            Dictionary<int, HuffmanTable> dcTables,
            Dictionary<int, HuffmanTable> acTables)
        {
            var dcTable = GetTable(dcTables, component.DcSelector, "DC");
            var acTable = GetTable(acTables, component.AcSelector, "AC");

            var t = BitReader.Decode(dcTable, reader);
            var diff = reader.Receive(t);
            component.DcPredictor += diff;
            block[0] = component.DcPredictor;

            var k = 1;
            while (k < 64)
            {
                var rs = BitReader.Decode(acTable, reader);
                var r = rs >> 4;
                var s = rs & 0xF;
                if (s == 0)
                {
                    if (r != 15)
                    {
                        break;
                    }

                    k += 16;
                }
                else
                {
                    k += r;
                    if (k > 63)
                    {
                        throw new InvalidDataException("Malformed JPEG entropy-coded data: AC coefficient index out of range.");
                    }

                    block[k] = reader.Receive(s);
                    k++;
                }
            }
        }

        /// <summary>
        ///     Decodes the DC coefficient of a progressive-scan block: a full Huffman-coded
        ///     magnitude/diff on the first DC scan (successive approximation high bit), or a
        ///     single successive-approximation refinement bit on any later DC scan.
        /// </summary>
        private static void DecodeProgressiveDc(
            int[] block, Component component, BitReader reader, Dictionary<int, HuffmanTable> dcTables, int ah, int al)
        {
            if (ah == 0)
            {
                var dcTable = GetTable(dcTables, component.DcSelector, "DC");
                var t = BitReader.Decode(dcTable, reader);
                var diff = reader.Receive(t);
                component.DcPredictor += diff;
                block[0] = component.DcPredictor << al;
            }
            else
            {
                if (reader.ReadBit() != 0)
                {
                    block[0] |= 1 << al;
                }
            }
        }

        private static void DecodeAcFirst(int[] block, HuffmanTable acTable, BitReader reader, int ss, int se, int al, ref int eobRun)
        {
            if (eobRun > 0)
            {
                eobRun--;
                return;
            }

            var k = ss;
            while (k <= se)
            {
                var rs = BitReader.Decode(acTable, reader);
                var r = rs >> 4;
                var s = rs & 0xF;
                if (s == 0)
                {
                    if (r < 15)
                    {
                        eobRun = (1 << r) - 1;
                        if (r > 0)
                        {
                            eobRun += reader.ReadBits(r);
                        }

                        break;
                    }

                    k += 16;
                }
                else
                {
                    k += r;
                    if (k > se)
                    {
                        throw new InvalidDataException("Malformed JPEG entropy-coded data: AC coefficient index out of range.");
                    }

                    block[k] = reader.Receive(s) << al;
                    k++;
                }
            }
        }

        private static void DecodeAcRefine(int[] block, HuffmanTable acTable, BitReader reader, int ss, int se, int al, ref int eobRun)
        {
            var rp = new RefinementParams(reader, se, 1 << al, -1 << al);
            var k = ss;

            if (eobRun == 0)
            {
                DecodeAcRefineNewCoefficients(block, acTable, rp, ref k, ref eobRun);
            }

            if (eobRun > 0)
            {
                RefineRemainingCoefficients(block, ref k, rp);
                eobRun--;
            }
        }

        /// <summary>
        ///     Groups the successive-approximation refinement state (ITU-T T.81 section G.1.2.3)
        ///     shared by the AC-refinement coefficient walkers: the bit reader, the end-of-band
        ///     index, and the positive/negative refinement bit values.
        /// </summary>
        private readonly record struct RefinementParams(BitReader Reader, int Se, int P1, int M1);

        /// <summary>
        ///     Applies a successive-approximation refinement bit to a single already-nonzero
        ///     coefficient, per ITU-T T.81 section G.1.2.3: the bit is only consumed (and the
        ///     coefficient only nudged toward zero-away) when the coefficient's next refinement
        ///     bit position is still unset.
        /// </summary>
        private static void RefineNonZeroCoefficient(int[] block, int k, RefinementParams rp)
        {
            if (block[k] != 0 && rp.Reader.ReadBit() != 0 && (block[k] & rp.P1) == 0)
            {
                block[k] += block[k] >= 0 ? rp.P1 : rp.M1;
            }
        }

        /// <summary>
        ///     Refines every remaining nonzero coefficient from <paramref name="k"/> through
        ///     <paramref name="rp"/>'s end-of-band index without placing any new coefficients, used
        ///     while an end-of-band run inherited from an earlier RS pair is still being consumed.
        /// </summary>
        private static void RefineRemainingCoefficients(int[] block, ref int k, RefinementParams rp)
        {
            while (k <= rp.Se)
            {
                RefineNonZeroCoefficient(block, k, rp);
                k++;
            }
        }

        /// <summary>
        ///     Decodes RS (run-length/size) pairs from the AC refinement scan, each of which
        ///     either starts a new end-of-band run (terminating this method) or specifies how many
        ///     zero coefficients to skip before placing one new nonzero coefficient; every
        ///     already-nonzero coefficient encountered along the way is refined per
        ///     <see cref="RefineNonZeroCoefficient"/>.
        /// </summary>
        private static void DecodeAcRefineNewCoefficients(
            int[] block, HuffmanTable acTable, RefinementParams rp, ref int k, ref int eobRun)
        {
            while (k <= rp.Se)
            {
                var rs = BitReader.Decode(acTable, rp.Reader);
                var r = rs >> 4;
                var s = rs & 0xF;
                var newValue = 0;

                if (s == 0)
                {
                    if (r < 15)
                    {
                        eobRun = 1 << r;
                        if (r > 0)
                        {
                            eobRun += rp.Reader.ReadBits(r);
                        }

                        r = 64; // sentinel: skip remaining coefficients (refinement only) below
                    }
                }
                else
                {
                    newValue = rp.Reader.ReadBit() != 0 ? rp.P1 : rp.M1;
                }

                RefineOrPlaceCoefficient(block, ref k, rp, r, newValue);
            }
        }

        /// <summary>
        ///     Walks coefficients from <paramref name="k"/> through <paramref name="rp"/>'s
        ///     end-of-band index, refining every already-nonzero coefficient, and counting down
        ///     <paramref name="zeroRunLength"/> zero coefficients before placing
        ///     <paramref name="newValue"/> (if nonzero) into the next zero coefficient slot and
        ///     stopping.
        /// </summary>
        private static void RefineOrPlaceCoefficient(
            int[] block, ref int k, RefinementParams rp, int zeroRunLength, int newValue)
        {
            var r = zeroRunLength;
            while (k <= rp.Se)
            {
                if (block[k] != 0)
                {
                    RefineNonZeroCoefficient(block, k, rp);
                }
                else
                {
                    if (r == 0)
                    {
                        if (newValue != 0)
                        {
                            block[k] = newValue;
                        }

                        k++;
                        break;
                    }

                    r--;
                }

                k++;
            }
        }

        private static HuffmanTable GetTable(Dictionary<int, HuffmanTable> tables, int selector, string kind)
        {
            if (!tables.TryGetValue(selector, out var table))
            {
                throw new InvalidDataException($"JPEG scan references undefined {kind} Huffman table {selector}.");
            }

            return table;
        }

        /// <summary>
        ///     Performs the inverse 8x8 DCT (ITU-T T.81 Annex A.3.3) on a natural-order coefficient
        ///     block, returning the spatial-domain block (still level-shifted, i.e. centered on zero).
        /// </summary>
        private static double[][] Idct2D(double[][] naturalCoeffs)
        {
            var tempT = NewBlock(); // tempT[x][v]
            for (var v = 0; v < 8; v++)
            {
                for (var x = 0; x < 8; x++)
                {
                    tempT[x][v] = 0.5 * DotProduct8(naturalCoeffs[v], BasisT[x]);
                }
            }

            var output = NewBlock(); // output[y][x]
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    output[y][x] = 0.5 * DotProduct8(tempT[x], BasisT[y]);
                }
            }

            return output;
        }

        private static double[][] Dequantize(int[] zigzagCoeffs, int[] quantZigzag)
        {
            var natural = NewBlock();
            for (var z = 0; z < 64; z++)
            {
                var n = ZigZagOrder[z];
                natural[n / 8][n % 8] = (double)zigzagCoeffs[z] * quantZigzag[z];
            }

            return natural;
        }

        /// <summary>
        ///     The reconstructed spatial-domain sample plane for each frame component, alongside
        ///     each plane's row stride (which may exceed the frame width when the component's
        ///     MCU-aligned block grid is wider than the actual image).
        /// </summary>
        private readonly record struct ComponentPlanes(byte[][] Planes, int[] Strides);

        private static Surface AssembleCanvas(int width, int height, Component[] components, Dictionary<int, int[]> quantTables)
        {
            var hMax = components.Max(c => c.H);
            var vMax = components.Max(c => c.V);

            var planes = ReconstructComponentPlanes(components, quantTables);

            var surface = new Surface(width, height);

            if (components.Length == 1)
            {
                WriteGrayscaleSurface(surface, planes.Planes[0], planes.Strides[0], width, height);
                return surface;
            }

            WriteColorSurface(surface, components, planes, width, height, hMax, vMax);
            return surface;
        }

        /// <summary>
        ///     Dequantizes and inverse-DCTs every block of every frame component, assembling each
        ///     component's blocks into a single MCU-aligned spatial-domain sample plane.
        /// </summary>
        private static ComponentPlanes ReconstructComponentPlanes(Component[] components, Dictionary<int, int[]> quantTables)
        {
            var planes = new byte[components.Length][];
            var planeStrides = new int[components.Length];

            for (var ci = 0; ci < components.Length; ci++)
            {
                var component = components[ci];
                if (!quantTables.TryGetValue(component.QuantSelector, out var quant))
                {
                    throw new InvalidDataException($"JPEG frame references undefined quantization table {component.QuantSelector}.");
                }

                var planeWidth = component.BlocksPerLineMcu * 8;
                var planeHeight = component.BlocksPerColumnMcu * 8;
                var plane = new byte[planeWidth * planeHeight];
                planeStrides[ci] = planeWidth;

                ReconstructComponentBlocks(component, quant, plane, planeWidth);

                planes[ci] = plane;
            }

            return new ComponentPlanes(planes, planeStrides);
        }

        /// <summary>
        ///     Dequantizes, inverse-DCTs, and level-shifts every 8x8 block of a single component
        ///     into its destination position within the component's spatial-domain sample plane.
        /// </summary>
        private static void ReconstructComponentBlocks(Component component, int[] quant, byte[] plane, int planeWidth)
        {
            for (var blockRow = 0; blockRow < component.BlocksPerColumnMcu; blockRow++)
            {
                for (var blockCol = 0; blockCol < component.BlocksPerLineMcu; blockCol++)
                {
                    var block = component.Blocks![(blockRow * component.BlocksPerLineMcu) + blockCol];
                    var natural = Dequantize(block, quant);
                    var spatial = Idct2D(natural);

                    for (var y = 0; y < 8; y++)
                    {
                        var rowOffset = ((blockRow * 8) + y) * planeWidth + (blockCol * 8);
                        for (var x = 0; x < 8; x++)
                        {
                            plane[rowOffset + x] = ClampToByte(spatial[y][x] + 128.0);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Writes a single-component (grayscale) plane into the destination surface, expanding
        ///     each sample to an opaque R == G == B pixel.
        /// </summary>
        private static void WriteGrayscaleSurface(Surface surface, byte[] plane, int stride, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                var rowBytes = surface.GetRowSpanBytes(y);
                var srcRow = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var v = plane[srcRow + x];
                    var idx = x * 4;
                    rowBytes[idx] = v;
                    rowBytes[idx + 1] = v;
                    rowBytes[idx + 2] = v;
                    rowBytes[idx + 3] = 255;
                }
            }
        }

        /// <summary>
        ///     Writes a three-component (Y/Cb/Cr) set of planes into the destination surface,
        ///     upsampling any subsampled chroma planes to full resolution and converting each row
        ///     to opaque RGB.
        /// </summary>
        private static void WriteColorSurface(
            Surface surface, Component[] components, ComponentPlanes planes, int width, int height, int hMax, int vMax)
        {
            var cbComp = components[1];
            var crComp = components[2];
            var yPlane = planes.Planes[0];
            var cbPlane = planes.Planes[1];
            var crPlane = planes.Planes[2];
            var yStride = planes.Strides[0];
            var cbStride = planes.Strides[1];
            var crStride = planes.Strides[2];

            var yRow = new byte[width];
            var cbRow = new byte[width];
            var crRow = new byte[width];
            var rRow = new byte[width];
            var gRow = new byte[width];
            var bRow = new byte[width];

            for (var y = 0; y < height; y++)
            {
                var yPlaneRow = y * yStride;
                var cbPlaneRow = (y * cbComp.V / vMax) * cbStride;
                var crPlaneRow = (y * crComp.V / vMax) * crStride;

                for (var x = 0; x < width; x++)
                {
                    yRow[x] = yPlane[yPlaneRow + x];
                    cbRow[x] = cbPlane[cbPlaneRow + (x * cbComp.H / hMax)];
                    crRow[x] = crPlane[crPlaneRow + (x * crComp.H / hMax)];
                }

                ConvertYCbCrRowToRgb(yRow, cbRow, crRow, rRow, gRow, bRow, width);

                var rowBytes = surface.GetRowSpanBytes(y);
                for (var x = 0; x < width; x++)
                {
                    var idx = x * 4;
                    rowBytes[idx] = rRow[x];
                    rowBytes[idx + 1] = gRow[x];
                    rowBytes[idx + 2] = bRow[x];
                    rowBytes[idx + 3] = 255;
                }
            }
        }
    }

    // ================================================================================================
    // Encoder
    // ================================================================================================

    private static class Encoder
    {
        private sealed class EncodeHuffmanEntry
        {
            public int Code;
            public int Length;
        }

        /// <summary>
        ///     Performs the forward 8x8 DCT (ITU-T T.81 Annex A.3.2) on a level-shifted spatial-domain
        ///     block, returning natural-order coefficients.
        /// </summary>
        private static double[][] Fdct2D(double[][] pixel)
        {
            var tempT = NewBlock(); // tempT[u][y]
            for (var y = 0; y < 8; y++)
            {
                for (var u = 0; u < 8; u++)
                {
                    tempT[u][y] = 0.5 * DotProduct8(pixel[y], Basis[u]);
                }
            }

            var natural = NewBlock(); // natural[v][u]
            for (var u = 0; u < 8; u++)
            {
                for (var v = 0; v < 8; v++)
                {
                    natural[v][u] = 0.5 * DotProduct8(tempT[u], Basis[v]);
                }
            }

            return natural;
        }

        private static void Quantize(double[][] natural, int[] quantZigzag, int[] outCoeffs)
        {
            for (var z = 0; z < 64; z++)
            {
                var n = ZigZagOrder[z];
                var value = natural[n / 8][n % 8] / quantZigzag[z];
                outCoeffs[z] = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>
        ///     Converts one RGB pixel to YCbCr using the ITU-R BT.601 coefficients (the inverse of
        ///     the decoder's pixel conversion), used by the encoder.
        /// </summary>
        private static void ConvertRgbToYCbCr(byte r, byte g, byte b, out byte y, out byte cb, out byte cr)
        {
            var yf = (0.299 * r) + (0.587 * g) + (0.114 * b);
            var cbf = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b);
            var crf = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b);
            y = ClampToByte(yf);
            cb = ClampToByte(cbf);
            cr = ClampToByte(crf);
        }

        public static void Encode(Surface surface, Stream stream, int quality)
        {
            var width = surface.Width;
            var height = surface.Height;

            var scale = quality < 50 ? 5000 / quality : 200 - (quality * 2);
            var lumaQuant = ScaleQuantTable(StandardLuminanceQuantTable, scale);
            var chromaQuant = ScaleQuantTable(StandardChrominanceQuantTable, scale);
            var lumaQuantZigZag = ToZigZag(lumaQuant);
            var chromaQuantZigZag = ToZigZag(chromaQuant);

            var dcLumaCodes = BuildEncodeTable(StandardDcLuminanceBits, StandardDcLuminanceValues);
            var acLumaCodes = BuildEncodeTable(StandardAcLuminanceBits, StandardAcLuminanceValues);
            var dcChromaCodes = BuildEncodeTable(StandardDcChrominanceBits, StandardDcChrominanceValues);
            var acChromaCodes = BuildEncodeTable(StandardAcChrominanceBits, StandardAcChrominanceValues);

            var mcusAcross = (width + 15) / 16;
            var mcusDown = (height + 15) / 16;
            var paddedWidth = mcusAcross * 16;
            var paddedHeight = mcusDown * 16;

            var planes = BuildPlanes(surface, new PlaneDimensions(width, height, paddedWidth, paddedHeight));
            var yPlane = planes.Y;
            var cbPlane = planes.Cb;
            var crPlane = planes.Cr;
            var chromaWidth = planes.ChromaWidth;

            WriteSoi(stream);
            WriteDqt(stream, 0, lumaQuantZigZag);
            WriteDqt(stream, 1, chromaQuantZigZag);
            WriteSof0(stream, width, height);
            WriteDht(stream, 0, 0, StandardDcLuminanceBits, StandardDcLuminanceValues);
            WriteDht(stream, 1, 0, StandardAcLuminanceBits, StandardAcLuminanceValues);
            WriteDht(stream, 0, 1, StandardDcChrominanceBits, StandardDcChrominanceValues);
            WriteDht(stream, 1, 1, StandardAcChrominanceBits, StandardAcChrominanceValues);
            WriteSos(stream);

            var writer = new BitWriter(stream);
            var dcPredY = 0;
            var dcPredCb = 0;
            var dcPredCr = 0;
            var coeffs = new int[64];

            for (var mcuY = 0; mcuY < mcusDown; mcuY++)
            {
                for (var mcuX = 0; mcuX < mcusAcross; mcuX++)
                {
                    for (var v = 0; v < 2; v++)
                    {
                        for (var h = 0; h < 2; h++)
                        {
                            var block = ExtractBlock(yPlane, paddedWidth, (mcuX * 16) + (h * 8), (mcuY * 16) + (v * 8));
                            EncodeBlock(block, lumaQuantZigZag, coeffs, ref dcPredY, dcLumaCodes, acLumaCodes, writer);
                        }
                    }

                    var cbBlock = ExtractBlock(cbPlane, chromaWidth, mcuX * 8, mcuY * 8);
                    EncodeBlock(cbBlock, chromaQuantZigZag, coeffs, ref dcPredCb, dcChromaCodes, acChromaCodes, writer);

                    var crBlock = ExtractBlock(crPlane, chromaWidth, mcuX * 8, mcuY * 8);
                    EncodeBlock(crBlock, chromaQuantZigZag, coeffs, ref dcPredCr, dcChromaCodes, acChromaCodes, writer);
                }
            }

            writer.FlushWithPadding();
            WriteMarker(stream, MarkerEoi);
        }

        private static int[] ScaleQuantTable(int[] baseTable, int scale)
        {
            var result = new int[64];
            for (var i = 0; i < 64; i++)
            {
                var value = ((baseTable[i] * scale) + 50) / 100;
                result[i] = Math.Clamp(value, 1, 255);
            }

            return result;
        }

        private static int[] ToZigZag(int[] natural)
        {
            var result = new int[64];
            for (var z = 0; z < 64; z++)
            {
                result[z] = natural[ZigZagOrder[z]];
            }

            return result;
        }

        private static Dictionary<int, EncodeHuffmanEntry> BuildEncodeTable(byte[] bits, byte[] values)
        {
            var result = new Dictionary<int, EncodeHuffmanEntry>();
            var code = 0;
            var pointer = 0;
            for (var length = 1; length <= 16; length++)
            {
                var count = bits[length - 1];
                for (var i = 0; i < count; i++)
                {
                    result[values[pointer]] = new EncodeHuffmanEntry { Code = code, Length = length };
                    code++;
                    pointer++;
                }

                code <<= 1;
            }

            return result;
        }

        /// <summary>
        ///     The source image dimensions together with the MCU-padded dimensions used for
        ///     chroma subsampling, grouped into a single parameter so <see cref="BuildPlanes"/>
        ///     does not need to accept each one individually.
        /// </summary>
        private readonly record struct PlaneDimensions(int Width, int Height, int PaddedWidth, int PaddedHeight);

        /// <summary>
        ///     The Y/Cb/Cr sample planes produced by <see cref="BuildPlanes"/>: a full-resolution
        ///     luma plane and 2x2 box-downsampled, MCU-padded chroma planes, alongside the chroma
        ///     planes' shared row stride.
        /// </summary>
        private readonly record struct PlaneSet(byte[] Y, byte[] Cb, byte[] Cr, int ChromaWidth);

        private static PlaneSet BuildPlanes(Surface surface, PlaneDimensions dimensions)
        {
            var width = dimensions.Width;
            var height = dimensions.Height;
            var paddedWidth = dimensions.PaddedWidth;
            var paddedHeight = dimensions.PaddedHeight;

            var fullY = new byte[width * height];
            var fullCb = new byte[width * height];
            var fullCr = new byte[width * height];

            for (var y = 0; y < height; y++)
            {
                var rowBytes = surface.GetRowSpanBytes(y);
                var rowOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    var idx = x * 4;
                    ConvertRgbToYCbCr(rowBytes[idx], rowBytes[idx + 1], rowBytes[idx + 2], out var yy, out var cb, out var cr);
                    fullY[rowOffset + x] = yy;
                    fullCb[rowOffset + x] = cb;
                    fullCr[rowOffset + x] = cr;
                }
            }

            var yPlane = PadReplicate(fullY, width, height, paddedWidth, paddedHeight);
            var paddedCb = PadReplicate(fullCb, width, height, paddedWidth, paddedHeight);
            var paddedCr = PadReplicate(fullCr, width, height, paddedWidth, paddedHeight);

            var chromaWidth = paddedWidth / 2;
            var cbPlane = DownsampleBox2X2(paddedCb, paddedWidth, paddedHeight);
            var crPlane = DownsampleBox2X2(paddedCr, paddedWidth, paddedHeight);

            return new PlaneSet(yPlane, cbPlane, crPlane, chromaWidth);
        }

        private static byte[] PadReplicate(byte[] source, int width, int height, int paddedWidth, int paddedHeight)
        {
            var result = new byte[paddedWidth * paddedHeight];
            for (var y = 0; y < paddedHeight; y++)
            {
                var srcY = Math.Min(y, height - 1);
                var srcRow = srcY * width;
                var dstRow = y * paddedWidth;
                for (var x = 0; x < paddedWidth; x++)
                {
                    var srcX = Math.Min(x, width - 1);
                    result[dstRow + x] = source[srcRow + srcX];
                }
            }

            return result;
        }

        private static byte[] DownsampleBox2X2(byte[] source, int width, int height)
        {
            var outWidth = width / 2;
            var outHeight = height / 2;
            var result = new byte[outWidth * outHeight];
            for (var y = 0; y < outHeight; y++)
            {
                var srcRow0 = (y * 2) * width;
                var srcRow1 = ((y * 2) + 1) * width;
                var dstRow = y * outWidth;
                for (var x = 0; x < outWidth; x++)
                {
                    var srcX = x * 2;
                    var sum = source[srcRow0 + srcX] + source[srcRow0 + srcX + 1] +
                              source[srcRow1 + srcX] + source[srcRow1 + srcX + 1];
                    result[dstRow + x] = (byte)((sum + 2) / 4);
                }
            }

            return result;
        }

        private static double[][] ExtractBlock(byte[] plane, int stride, int startX, int startY)
        {
            var block = NewBlock();
            for (var y = 0; y < 8; y++)
            {
                var rowOffset = ((startY + y) * stride) + startX;
                for (var x = 0; x < 8; x++)
                {
                    block[y][x] = plane[rowOffset + x] - 128.0;
                }
            }

            return block;
        }

        private static void EncodeBlock(
            double[][] block,
            int[] quantZigzag,
            int[] coeffs,
            ref int dcPredictor,
            Dictionary<int, EncodeHuffmanEntry> dcCodes,
            Dictionary<int, EncodeHuffmanEntry> acCodes,
            BitWriter writer)
        {
            var natural = Fdct2D(block);
            Quantize(natural, quantZigzag, coeffs);

            var diff = coeffs[0] - dcPredictor;
            dcPredictor = coeffs[0];
            WriteDcValue(diff, dcCodes, writer);

            var runLength = 0;
            for (var k = 1; k < 64; k++)
            {
                if (coeffs[k] == 0)
                {
                    runLength++;
                    continue;
                }

                while (runLength >= 16)
                {
                    WriteHuffman(acCodes, 0xF0, writer); // ZRL
                    runLength -= 16;
                }

                var size = MagnitudeSize(coeffs[k]);
                WriteHuffman(acCodes, (runLength << 4) | size, writer);
                WriteMagnitudeBits(coeffs[k], size, writer);
                runLength = 0;
            }

            if (runLength > 0)
            {
                WriteHuffman(acCodes, 0x00, writer); // EOB
            }
        }

        private static void WriteDcValue(int diff, Dictionary<int, EncodeHuffmanEntry> dcCodes, BitWriter writer)
        {
            var size = MagnitudeSize(diff);
            WriteHuffman(dcCodes, size, writer);
            WriteMagnitudeBits(diff, size, writer);
        }

        private static int MagnitudeSize(int value)
        {
            var magnitude = Math.Abs(value);
            var size = 0;
            while (magnitude != 0)
            {
                size++;
                magnitude >>= 1;
            }

            return size;
        }

        private static void WriteMagnitudeBits(int value, int size, BitWriter writer)
        {
            if (size == 0)
            {
                return;
            }

            var bits = value >= 0 ? value : value + (1 << size) - 1;
            writer.WriteBits(bits, size);
        }

        private static void WriteHuffman(Dictionary<int, EncodeHuffmanEntry> table, int symbol, BitWriter writer)
        {
            var entry = table[symbol];
            writer.WriteBits(entry.Code, entry.Length);
        }

        private static void WriteMarker(Stream stream, byte marker)
        {
            stream.WriteByte(MarkerPrefix);
            stream.WriteByte(marker);
        }

        private static void WriteUInt16Be(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static void WriteSoi(Stream stream) => WriteMarker(stream, MarkerSoi);

        private static void WriteDqt(Stream stream, int id, int[] tableZigZag)
        {
            WriteMarker(stream, MarkerDqt);
            WriteUInt16Be(stream, 2 + 1 + 64);
            stream.WriteByte((byte)id);
            for (var i = 0; i < 64; i++)
            {
                stream.WriteByte((byte)tableZigZag[i]);
            }
        }

        private static void WriteSof0(Stream stream, int width, int height)
        {
            WriteMarker(stream, MarkerSof0);
            WriteUInt16Be(stream, 2 + 1 + 2 + 2 + 1 + (3 * 3));
            stream.WriteByte(8); // precision
            WriteUInt16Be(stream, height);
            WriteUInt16Be(stream, width);
            stream.WriteByte(3); // number of components

            stream.WriteByte(1); // Y id
            stream.WriteByte(0x22); // H=2, V=2
            stream.WriteByte(0); // quant table 0

            stream.WriteByte(2); // Cb id
            stream.WriteByte(0x11); // H=1, V=1
            stream.WriteByte(1); // quant table 1

            stream.WriteByte(3); // Cr id
            stream.WriteByte(0x11); // H=1, V=1
            stream.WriteByte(1); // quant table 1
        }

        private static void WriteDht(Stream stream, int tableClass, int id, byte[] bits, byte[] values)
        {
            WriteMarker(stream, MarkerDht);
            WriteUInt16Be(stream, 2 + 1 + 16 + values.Length);
            stream.WriteByte((byte)((tableClass << 4) | id));
            foreach (var b in bits)
            {
                stream.WriteByte(b);
            }

            foreach (var v in values)
            {
                stream.WriteByte(v);
            }
        }

        private static void WriteSos(Stream stream)
        {
            WriteMarker(stream, MarkerSos);
            WriteUInt16Be(stream, 2 + 1 + (3 * 2) + 3);
            stream.WriteByte(3); // number of components in scan

            stream.WriteByte(1); // Y id
            stream.WriteByte(0x00); // DC=0, AC=0

            stream.WriteByte(2); // Cb id
            stream.WriteByte(0x11); // DC=1, AC=1

            stream.WriteByte(3); // Cr id
            stream.WriteByte(0x11); // DC=1, AC=1

            stream.WriteByte(0); // Ss
            stream.WriteByte(63); // Se
            stream.WriteByte(0x00); // Ah/Al
        }
    }
}
