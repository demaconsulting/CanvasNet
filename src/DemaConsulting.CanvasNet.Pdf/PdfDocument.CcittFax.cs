// cspell:ignore CCITT Makeup ccittfax EOFB
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The ITU-T T.6 two-dimensional mode codes (section 4.2.1): how the next changing
    ///     element on the current coding line relates to the reference (previous) line's own
    ///     changing elements.
    /// </summary>
    internal enum CcittMode
    {
        /// <summary>The current color run extends past the reference line's <c>b2</c> changing element without itself changing color.</summary>
        Pass,

        /// <summary>Two explicit run-length codes (current color, then the opposite color) describe the next two changing elements.</summary>
        Horizontal,

        /// <summary>The next changing element is at the reference line's <c>b1</c> position (0 offset).</summary>
        V0,

        /// <summary>The next changing element is 1 pixel to the right of <c>b1</c>.</summary>
        VR1,

        /// <summary>The next changing element is 2 pixels to the right of <c>b1</c>.</summary>
        VR2,

        /// <summary>The next changing element is 3 pixels to the right of <c>b1</c>.</summary>
        VR3,

        /// <summary>The next changing element is 1 pixel to the left of <c>b1</c>.</summary>
        VL1,

        /// <summary>The next changing element is 2 pixels to the left of <c>b1</c>.</summary>
        VL2,

        /// <summary>The next changing element is 3 pixels to the left of <c>b1</c>.</summary>
        VL3,
    }

    /// <summary>
    ///     The ITU-T T.6 two-dimensional mode code table: each entry's bit length/value maps to
    ///     one <see cref="CcittMode"/> member. Prefix-free, so <see cref="ReadMode"/> can read one
    ///     bit at a time and stop at the first match. Deliberately <see langword="internal"/>
    ///     (not <see langword="private"/>), together with <see cref="CcittMode"/> and
    ///     <see cref="ReadMode"/>, so a defensive unit test can feed every table entry's own exact
    ///     bits back through the same decoder the production code path uses - mirroring
    ///     <see cref="WhiteCodeTable"/>'s own identical visibility rationale.
    /// </summary>
    internal static readonly Dictionary<(int Length, int Code), CcittMode> ModeCodeTable = new()
    {
        [(1, 0b1)] = CcittMode.V0,
        [(3, 0b001)] = CcittMode.Horizontal,
        [(3, 0b011)] = CcittMode.VR1,
        [(3, 0b010)] = CcittMode.VL1,
        [(4, 0b0001)] = CcittMode.Pass,
        [(6, 0b000011)] = CcittMode.VR2,
        [(6, 0b000010)] = CcittMode.VL2,
        [(7, 0b0000011)] = CcittMode.VR3,
        [(7, 0b0000010)] = CcittMode.VL3,
    };

    /// <summary>The longest bit length any <see cref="ModeCodeTable"/> entry uses.</summary>
    private const int MaxModeCodeLength = 7;

    /// <summary>The longest bit length any White/Black run-length code table entry uses (ITU-T T.4 Table 3b's 13-bit Black codes).</summary>
    private const int MaxRunLengthCodeLength = 13;

    /// <summary>
    ///     The ITU-T T.4 Table 3 White terminating (run lengths <c>0</c>-<c>63</c>) and makeup
    ///     (<c>64</c>-<c>1728</c>) Modified Huffman run-length codes, as <c>(bit length, code
    ///     value, run length)</c> triples. Transcribed from libtiff's independently published,
    ///     widely-used <c>TIFFFaxWhiteCodes</c> table (<c>libtiff/t4.h</c>), itself a direct
    ///     encoding of the ITU-T T.4 recommendation's own published table - used here only as a
    ///     cross-check source, not copied code (these are the recommendation's own numeric
    ///     values, not copyrightable expression).
    /// </summary>
    private static readonly (int Length, int Code, int Run)[] WhiteCodeEntries =
    [
        (8, 0x35, 0), (6, 0x7, 1), (4, 0x7, 2), (4, 0x8, 3), (4, 0xB, 4), (4, 0xC, 5), (4, 0xE, 6), (4, 0xF, 7),
        (5, 0x13, 8), (5, 0x14, 9), (5, 0x7, 10), (5, 0x8, 11), (6, 0x8, 12), (6, 0x3, 13), (6, 0x34, 14), (6, 0x35, 15),
        (6, 0x2A, 16), (6, 0x2B, 17), (7, 0x27, 18), (7, 0xC, 19), (7, 0x8, 20), (7, 0x17, 21), (7, 0x3, 22), (7, 0x4, 23),
        (7, 0x28, 24), (7, 0x2B, 25), (7, 0x13, 26), (7, 0x24, 27), (7, 0x18, 28),
        (8, 0x2, 29), (8, 0x3, 30), (8, 0x1A, 31), (8, 0x1B, 32), (8, 0x12, 33), (8, 0x13, 34), (8, 0x14, 35), (8, 0x15, 36),
        (8, 0x16, 37), (8, 0x17, 38), (8, 0x28, 39), (8, 0x29, 40), (8, 0x2A, 41), (8, 0x2B, 42), (8, 0x2C, 43), (8, 0x2D, 44),
        (8, 0x4, 45), (8, 0x5, 46), (8, 0xA, 47), (8, 0xB, 48), (8, 0x52, 49), (8, 0x53, 50), (8, 0x54, 51), (8, 0x55, 52),
        (8, 0x24, 53), (8, 0x25, 54), (8, 0x58, 55), (8, 0x59, 56), (8, 0x5A, 57), (8, 0x5B, 58), (8, 0x4A, 59), (8, 0x4B, 60),
        (8, 0x32, 61), (8, 0x33, 62), (8, 0x34, 63),
        (5, 0x1B, 64), (5, 0x12, 128), (6, 0x17, 192), (7, 0x37, 256), (8, 0x36, 320), (8, 0x37, 384), (8, 0x64, 448), (8, 0x65, 512),
        (8, 0x68, 576), (8, 0x67, 640), (9, 0xCC, 704), (9, 0xCD, 768), (9, 0xD2, 832), (9, 0xD3, 896), (9, 0xD4, 960), (9, 0xD5, 1024),
        (9, 0xD6, 1088), (9, 0xD7, 1152), (9, 0xD8, 1216), (9, 0xD9, 1280), (9, 0xDA, 1344), (9, 0xDB, 1408), (9, 0x98, 1472), (9, 0x99, 1536),
        (9, 0x9A, 1600), (6, 0x18, 1664), (9, 0x9B, 1728),
    ];

    /// <summary>
    ///     The ITU-T T.4 Table 2 Black terminating (run lengths <c>0</c>-<c>63</c>) and makeup
    ///     (<c>64</c>-<c>1728</c>) Modified Huffman run-length codes - see
    ///     <see cref="WhiteCodeEntries"/>'s remarks for the transcription source/provenance.
    /// </summary>
    private static readonly (int Length, int Code, int Run)[] BlackCodeEntries =
    [
        (10, 0x37, 0), (3, 0x2, 1), (2, 0x3, 2), (2, 0x2, 3), (3, 0x3, 4), (4, 0x3, 5), (4, 0x2, 6), (5, 0x3, 7),
        (6, 0x5, 8), (6, 0x4, 9), (7, 0x4, 10), (7, 0x5, 11), (7, 0x7, 12), (8, 0x4, 13), (8, 0x7, 14), (9, 0x18, 15),
        (10, 0x17, 16), (10, 0x18, 17), (10, 0x8, 18), (11, 0x67, 19), (11, 0x68, 20), (11, 0x6C, 21), (11, 0x37, 22), (11, 0x28, 23),
        (11, 0x17, 24), (11, 0x18, 25), (12, 0xCA, 26), (12, 0xCB, 27), (12, 0xCC, 28), (12, 0xCD, 29), (12, 0x68, 30), (12, 0x69, 31),
        (12, 0x6A, 32), (12, 0x6B, 33), (12, 0xD2, 34), (12, 0xD3, 35), (12, 0xD4, 36), (12, 0xD5, 37), (12, 0xD6, 38), (12, 0xD7, 39),
        (12, 0x6C, 40), (12, 0x6D, 41), (12, 0xDA, 42), (12, 0xDB, 43), (12, 0x54, 44), (12, 0x55, 45), (12, 0x56, 46), (12, 0x57, 47),
        (12, 0x64, 48), (12, 0x65, 49), (12, 0x52, 50), (12, 0x53, 51), (12, 0x24, 52), (12, 0x37, 53), (12, 0x38, 54), (12, 0x27, 55),
        (12, 0x28, 56), (12, 0x58, 57), (12, 0x59, 58), (12, 0x2B, 59), (12, 0x2C, 60), (12, 0x5A, 61), (12, 0x66, 62), (12, 0x67, 63),
        (10, 0xF, 64), (12, 0xC8, 128), (12, 0xC9, 192), (12, 0x5B, 256), (12, 0x33, 320), (12, 0x34, 384), (12, 0x35, 448),
        (13, 0x6C, 512), (13, 0x6D, 576), (13, 0x4A, 640), (13, 0x4B, 704), (13, 0x4C, 768), (13, 0x4D, 832), (13, 0x72, 896),
        (13, 0x73, 960), (13, 0x74, 1024), (13, 0x75, 1088), (13, 0x76, 1152), (13, 0x77, 1216), (13, 0x52, 1280), (13, 0x53, 1344),
        (13, 0x54, 1408), (13, 0x55, 1472), (13, 0x5A, 1536), (13, 0x5B, 1600), (13, 0x64, 1664), (13, 0x65, 1728),
    ];

    /// <summary>
    ///     The ITU-T T.4 Table 3a/3b extended makeup codes (run lengths <c>1792</c>-<c>2560</c>),
    ///     shared verbatim between the White and Black code tables (identical bit patterns for
    ///     both colors) - see <see cref="WhiteCodeEntries"/>'s remarks for the transcription
    ///     source/provenance.
    /// </summary>
    private static readonly (int Length, int Code, int Run)[] ExtendedMakeupCodeEntries =
    [
        (11, 0x8, 1792), (11, 0xC, 1856), (11, 0xD, 1920), (12, 0x12, 1984), (12, 0x13, 2048), (12, 0x14, 2112),
        (12, 0x15, 2176), (12, 0x16, 2240), (12, 0x17, 2304), (12, 0x1C, 2368), (12, 0x1D, 2432), (12, 0x1E, 2496),
        (12, 0x1F, 2560),
    ];

    /// <summary>
    ///     The White run-length code table (<see cref="WhiteCodeEntries"/> merged with the shared
    ///     <see cref="ExtendedMakeupCodeEntries"/>), keyed by <c>(bit length, code value)</c>.
    ///     Deliberately <see langword="internal"/> (not <see langword="private"/>), together with
    ///     <see cref="BlackCodeTable"/>, <see cref="CcittBitReader"/>, and
    ///     <see cref="ReadVariableLengthCode"/>, so a defensive unit test can feed every table
    ///     entry's own exact bits back through the same generic decoder the production code path
    ///     uses, independently of whether a given run length happens to appear in any rendered
    ///     test image.
    /// </summary>
    internal static readonly Dictionary<(int Length, int Code), int> WhiteCodeTable =
        BuildRunLengthCodeTable(WhiteCodeEntries, ExtendedMakeupCodeEntries);

    /// <summary>The Black run-length code table - see <see cref="WhiteCodeTable"/>'s remarks.</summary>
    internal static readonly Dictionary<(int Length, int Code), int> BlackCodeTable =
        BuildRunLengthCodeTable(BlackCodeEntries, ExtendedMakeupCodeEntries);

    /// <summary>Merges a color's own terminating/makeup entries with the shared extended makeup entries into a lookup dictionary.</summary>
    private static Dictionary<(int Length, int Code), int> BuildRunLengthCodeTable(
        (int Length, int Code, int Run)[] colorEntries,
        (int Length, int Code, int Run)[] sharedExtendedEntries)
    {
        var table = new Dictionary<(int Length, int Code), int>(colorEntries.Length + sharedExtendedEntries.Length);
        foreach (var entry in colorEntries)
        {
            table[(entry.Length, entry.Code)] = entry.Run;
        }

        foreach (var entry in sharedExtendedEntries)
        {
            table[(entry.Length, entry.Code)] = entry.Run;
        }

        return table;
    }

    /// <summary>
    ///     Decodes a <c>CCITTFaxDecode</c>-filtered image stream: ITU-T T.6 Group 4 (<c>/K</c>
    ///     negative) Modified Modified READ (MMR) two-dimensional coding only.
    /// </summary>
    /// <param name="data">The raw (pre-<c>CCITTFaxDecode</c>) stream bytes.</param>
    /// <param name="k">The resolved <c>/DecodeParms /K</c> value.</param>
    /// <param name="columns">The resolved <c>/DecodeParms /Columns</c> value (pixels per row).</param>
    /// <param name="rows">
    ///     The resolved row count to decode (the already-defaulted-to-the-image's-own-<c>/Height</c>
    ///     <c>/DecodeParms /Rows</c> value) - authoritative: decoding stops after exactly this
    ///     many rows regardless of any trailing <c>EOFB</c>/<c>RTC</c> marker bits that may follow
    ///     in <paramref name="data"/>.
    /// </param>
    /// <param name="blackIs1">The resolved <c>/DecodeParms /BlackIs1</c> value.</param>
    /// <param name="encodedByteAlign">The resolved <c>/DecodeParms /EncodedByteAlign</c> value.</param>
    /// <param name="endOfLine">The resolved <c>/DecodeParms /EndOfLine</c> value.</param>
    /// <returns>
    ///     <paramref name="columns"/> x <paramref name="rows"/> 8-bit gray samples (one byte per
    ///     pixel, each either <c>0</c> or <c>255</c>), row-major, ready for the shared
    ///     <c>/ColorSpace</c> pipeline (<c>PdfDocument.Images.cs</c>'s <c>SamplesToColor</c>).
    /// </returns>
    /// <remarks>
    ///     Each row is decoded two-dimensionally (<see cref="DecodeCcittRow"/>) against the
    ///     previous row's changing elements (the first row's reference line is the imaginary,
    ///     entirely-white line ITU-T T.6 itself specifies), packed into
    ///     <c>(Columns + 7) / 8</c> bytes per row (matching <see cref="ApplyTiffPredictor"/>/
    ///     <see cref="ApplyPngPredictor"/>'s own <c>rowBytes</c> convention) with
    ///     <paramref name="blackIs1"/> polarity already applied via <see cref="PackRow"/>, then
    ///     expanded into one gray byte per pixel via <see cref="ExpandPackedBitsToGrayBytes"/>.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the bit stream is truncated or contains an invalid/unrecognized mode or
    ///     run-length code.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="k"/> is <c>0</c> or greater (Group 3, one- or
    ///     two-dimensional, is not supported - only Group 4 is), or when
    ///     <paramref name="endOfLine"/> is <see langword="true"/> (explicit in-stream <c>EOL</c>
    ///     codes before every row are not supported).
    /// </exception>
    private static byte[] DecodeCcittFax(
        byte[] data,
        int k,
        int columns,
        int rows,
        bool blackIs1,
        bool encodedByteAlign,
        bool endOfLine)
    {
        if (k >= 0)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-ccittfax-group3",
                $"CCITTFaxDecode with /K {k} (Group 3, one- or two-dimensional) is not supported; only Group 4 (/K < 0) is supported.");
        }

        if (endOfLine)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-ccittfax-endofline",
                "CCITTFaxDecode with /EndOfLine true is not supported.");
        }

        var reader = new CcittBitReader(data);
        var rowBytes = (columns + 7) / 8;
        var packedRows = new byte[rowBytes * rows];
        IReadOnlyList<int> referenceChanges = [];

        for (var row = 0; row < rows; row++)
        {
            if (encodedByteAlign)
            {
                reader.AlignToByte();
            }

            var currentChanges = DecodeCcittRow(reader, referenceChanges, columns);
            var packedRow = PackRow(currentChanges, columns, blackIs1);
            Array.Copy(packedRow, 0, packedRows, row * rowBytes, rowBytes);
            referenceChanges = currentChanges;
        }

        return ExpandPackedBitsToGrayBytes(packedRows, columns, rows);
    }

    /// <summary>
    ///     Decodes a single ITU-T T.6 two-dimensional coding line against
    ///     <paramref name="referenceChanges"/> (the previous line's changing elements, ascending,
    ///     or empty for the imaginary all-white first reference line).
    /// </summary>
    /// <returns>The decoded line's own changing-element positions, ascending.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the bit stream is truncated, contains an invalid mode/run-length code, or
    ///     a single coding line produces an implausible number of changing elements (a malformed-
    ///     stream safety guard, not a real ITU-T T.6 limit).
    /// </exception>
    private static List<int> DecodeCcittRow(CcittBitReader reader, IReadOnlyList<int> referenceChanges, int columns)
    {
        var changes = new List<int>();
        var a0 = -1;
        var isBlack = false;
        var maxChanges = (columns * 2) + 16;

        while (a0 < columns)
        {
            if (changes.Count > maxChanges)
            {
                throw new InvalidDataException("Malformed CCITTFaxDecode stream: a coding line produced too many changing elements.");
            }

            var mode = ReadMode(reader);
            var (b1, b2) = FindB1B2(referenceChanges, a0, isBlack, columns);

            switch (mode)
            {
                case CcittMode.Pass:
                    a0 = b2;
                    break;

                case CcittMode.Horizontal:
                    var start = a0 < 0 ? 0 : a0;
                    var run1 = ReadRun(reader, isBlack);
                    var run2 = ReadRun(reader, !isBlack);
                    var a1 = start + run1;
                    var a2 = a1 + run2;
                    changes.Add(a1);
                    changes.Add(a2);
                    a0 = a2;
                    break;

                default:
                    var vertical = b1 + VerticalDelta(mode);
                    changes.Add(vertical);
                    a0 = vertical;
                    isBlack = !isBlack;
                    break;
            }
        }

        return changes;
    }

    /// <summary>Maps a vertical <see cref="CcittMode"/> member to its <c>b1</c> pixel offset.</summary>
    private static int VerticalDelta(CcittMode mode) => mode switch
    {
        CcittMode.V0 => 0,
        CcittMode.VR1 => 1,
        CcittMode.VR2 => 2,
        CcittMode.VR3 => 3,
        CcittMode.VL1 => -1,
        CcittMode.VL2 => -2,
        CcittMode.VL3 => -3,
        _ => throw new InvalidOperationException($"Unreachable: {mode} is not a vertical mode."),
    };

    /// <summary>
    ///     Finds the <c>b1</c>/<c>b2</c> changing elements on the reference line relative to
    ///     <paramref name="a0"/>/<paramref name="currentIsBlack"/>, per ITU-T T.6 section 4.2.1:
    ///     <c>b1</c> is the first changing element on the reference line to the right of
    ///     <paramref name="a0"/> and of opposite color to <paramref name="currentIsBlack"/>;
    ///     <c>b2</c> is the next changing element after <c>b1</c>. Either/both default to
    ///     <paramref name="columns"/> when no such element exists.
    /// </summary>
    private static (int B1, int B2) FindB1B2(
        IReadOnlyList<int> referenceChanges,
        int a0,
        bool currentIsBlack,
        int columns)
    {
        var idx = 0;
        while (idx < referenceChanges.Count && referenceChanges[idx] <= a0)
        {
            idx++;
        }

        // Changing element 0 is where the (imaginary, all-white-before-it) reference line
        // transitions from white to black, so even indices are black, odd indices are white.
        var colorAtIdxIsBlack = idx % 2 == 0;
        if (colorAtIdxIsBlack == currentIsBlack)
        {
            idx++;
        }

        var b1 = idx < referenceChanges.Count ? referenceChanges[idx] : columns;
        var b2 = idx + 1 < referenceChanges.Count ? referenceChanges[idx + 1] : columns;
        return (b1, b2);
    }

    /// <summary>Reads one ITU-T T.6 two-dimensional mode code (<see cref="ModeCodeTable"/>).</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the bit stream is truncated or no <see cref="MaxModeCodeLength"/>-bit
    ///     prefix matches a known mode code.
    /// </exception>
    internal static CcittMode ReadMode(CcittBitReader reader)
    {
        var code = 0;
        for (var length = 1; length <= MaxModeCodeLength; length++)
        {
            code = (code << 1) | reader.ReadBit();
            if (ModeCodeTable.TryGetValue((length, code), out var mode))
            {
                return mode;
            }
        }

        throw new InvalidDataException("Invalid CCITTFaxDecode two-dimensional mode code.");
    }

    /// <summary>
    ///     Reads one run-length code's worth of run (a terminating code <c>0</c>-<c>63</c>, a
    ///     makeup code <c>64</c>-<c>1728</c>, or an extended makeup code <c>1792</c>-<c>2560</c>)
    ///     from <paramref name="table"/>, returning as soon as the accumulated bits match a known
    ///     code - valid since every entry in both tables is prefix-free.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the bit stream is truncated or no <paramref name="maxLength"/>-bit prefix
    ///     matches a known code.
    /// </exception>
    internal static int ReadVariableLengthCode(
        CcittBitReader reader,
        IReadOnlyDictionary<(int Length, int Code), int> table,
        int maxLength)
    {
        var code = 0;
        for (var length = 1; length <= maxLength; length++)
        {
            code = (code << 1) | reader.ReadBit();
            if (table.TryGetValue((length, code), out var run))
            {
                return run;
            }
        }

        throw new InvalidDataException("Invalid CCITTFaxDecode run-length code.");
    }

    /// <summary>
    ///     Reads a complete run length for <paramref name="isBlack"/>'s color: a chain of 0 or
    ///     more makeup codes (each <c>&gt;= 64</c>), terminated by exactly one terminating code
    ///     (<c>&lt; 64</c>), summing every code's run length.
    /// </summary>
    private static int ReadRun(CcittBitReader reader, bool isBlack)
    {
        var table = isBlack ? BlackCodeTable : WhiteCodeTable;
        var total = 0;
        int run;
        do
        {
            run = ReadVariableLengthCode(reader, table, MaxRunLengthCodeLength);
            total += run;
        }
        while (run >= 64);

        return total;
    }

    /// <summary>
    ///     Packs one decoded coding line's changing elements into <c>(columns + 7) / 8</c> bytes,
    ///     MSB-first, applying <paramref name="blackIs1"/> polarity: per ISO 32000-1/2 Table 11,
    ///     the default (<see langword="false"/>) maps a physically black pixel to bit <c>0</c>
    ///     and white to bit <c>1</c>; <see langword="true"/> reverses this.
    /// </summary>
    private static byte[] PackRow(IReadOnlyList<int> changes, int columns, bool blackIs1)
    {
        var packed = new byte[(columns + 7) / 8];
        var pos = 0;
        var isBlack = false;

        foreach (var changeRaw in changes)
        {
            var change = Math.Min(changeRaw, columns);
            if (change > pos)
            {
                if (isBlack == blackIs1)
                {
                    SetBitRange(packed, pos, change);
                }

                pos = change;
            }

            isBlack = !isBlack;
            if (pos >= columns)
            {
                break;
            }
        }

        if (pos < columns && isBlack == blackIs1)
        {
            SetBitRange(packed, pos, columns);
        }

        return packed;
    }

    /// <summary>Sets every bit in <c>[start, end)</c> (MSB-first pixel-to-bit mapping) to <c>1</c>.</summary>
    private static void SetBitRange(byte[] packed, int start, int end)
    {
        for (var x = start; x < end; x++)
        {
            packed[x / 8] |= (byte)(0x80 >> (x % 8));
        }
    }

    /// <summary>
    ///     Expands <paramref name="packedRows"/> (<paramref name="rows"/> rows of
    ///     <c>(columns + 7) / 8</c> packed bits each, <see cref="PackRow"/>'s own output format)
    ///     into one 8-bit gray sample per pixel (bit <c>1</c> -&gt; <c>255</c>, bit <c>0</c> -&gt;
    ///     <c>0</c>), row-major.
    /// </summary>
    private static byte[] ExpandPackedBitsToGrayBytes(byte[] packedRows, int columns, int rows)
    {
        var rowBytes = (columns + 7) / 8;
        var output = new byte[columns * rows];
        for (var row = 0; row < rows; row++)
        {
            var rowOffset = row * rowBytes;
            for (var x = 0; x < columns; x++)
            {
                var packedByte = packedRows[rowOffset + (x / 8)];
                var bit = (packedByte >> (7 - (x % 8))) & 1;
                output[(row * columns) + x] = bit == 1 ? (byte)255 : (byte)0;
            }
        }

        return output;
    }

    /// <summary>
    ///     An MSB-first (high-order bit first, per ITU-T T.6's own bit-packing convention), single
    ///     bit-at-a-time reader over a byte array, with optional byte-boundary alignment for
    ///     <c>/EncodedByteAlign</c> support. Deliberately <see langword="internal"/> (see
    ///     <see cref="WhiteCodeTable"/>'s remarks).
    /// </summary>
    internal sealed class CcittBitReader(byte[] data)
    {
        private int _bytePosition;
        private int _bitPosition;

        /// <summary>Reads and returns the next single bit (<c>0</c> or <c>1</c>).</summary>
        /// <exception cref="InvalidDataException">Thrown when the stream is exhausted.</exception>
        internal int ReadBit()
        {
            if (_bytePosition >= data.Length)
            {
                throw new InvalidDataException("Truncated CCITTFaxDecode stream.");
            }

            var bit = (data[_bytePosition] >> (7 - _bitPosition)) & 1;
            _bitPosition++;
            if (_bitPosition == 8)
            {
                _bitPosition = 0;
                _bytePosition++;
            }

            return bit;
        }

        /// <summary>Advances to the start of the next byte when not already at one (a no-op if already byte-aligned).</summary>
        internal void AlignToByte()
        {
            if (_bitPosition != 0)
            {
                _bitPosition = 0;
                _bytePosition++;
            }
        }
    }
}
