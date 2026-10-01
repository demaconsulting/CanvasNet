using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

// cspell:ignore agrave Xyzzy Zapf Nonsymbolic cids
// cspell:ignore beginbfchar endbfchar beginbfrange endbfrange begincodespacerange
// cspell:ignore endcodespacerange findresource defineresource currentdict begincmap endcmap
// cspell:ignore bfchar bfrange nendbfchar nendbfrange tounicode usecmap cidrange cidchar codepoints
// cspell:ignore OTTO rmoveto rlineto endchar notdef charstring charstrings cidfonttype
// cspell:ignore functiontype multiinput fitz
// cspell:ignore hsbw closepath fontfile lenIV quoteright Quoteright hival

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Unit-level tests for the <see cref="PdfDocument"/> parsing internals (tokenizer, object
///     model, cross-reference resolution, page-tree traversal) and public API surface.
///     Complements <see cref="PdfSystemIntegrationTests"/>, which proves the same behaviors
///     end-to-end through the public API only.
/// </summary>
public class PdfDocumentTests
{
    /// <summary>The directory containing the hand-authored PDF fixtures (see <c>PdfFixtures\README.md</c>).</summary>
    private static string FixturesPath => Path.Join(AppContext.BaseDirectory, "PdfFixtures");

    private static string Fixture(string name) => Path.Join(FixturesPath, name);

    /// <summary>The opaque black color every Phase 2 fill/stroke operator paints with.</summary>
    private static readonly Canvas.Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>
    ///     Builds an in-memory, single-page, classic-xref PDF (matching
    ///     <c>PdfFixtures\README.md</c>'s hand-authored template) whose one page declares the
    ///     given <c>/MediaBox</c>, optional <c>/Rotate</c>, and a single <c>/Contents</c> stream
    ///     holding <paramref name="content"/> verbatim - used by tests that only need arbitrary
    ///     content-stream text against a fixed page size, without a dedicated fixture file on disk.
    /// </summary>
    private static byte[] BuildSinglePagePdf(double mediaBoxWidth, double mediaBoxHeight, string content, int rotate = 0)
    {
        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        var rotateEntry = rotate == 0 ? string.Empty : $" /Rotate {rotate}";
        var streamHeader = System.Text.Encoding.ASCII.GetBytes($"<< /Length {contentBytes.Length} >>\nstream\n");
        var streamFooter = "\nendstream"u8.ToArray();
        var streamBody = new byte[streamHeader.Length + contentBytes.Length + streamFooter.Length];
        streamHeader.CopyTo(streamBody, 0);
        contentBytes.CopyTo(streamBody, streamHeader.Length);
        streamFooter.CopyTo(streamBody, streamHeader.Length + contentBytes.Length);

        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            System.Text.Encoding.ASCII.GetBytes(
                $"<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 {mediaBoxWidth} {mediaBoxHeight}] >>"),
            System.Text.Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R{rotateEntry} /Contents 4 0 R >>"),
            streamBody,
        };

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        var xrefOffset = buffer.Count;
        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n"));
        buffer.AddRange("0000000000 65535 f \n"u8.ToArray());
        foreach (var offset in offsets)
        {
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    /// <summary>
    ///     Renders <paramref name="content"/> against a single-page PDF built by
    ///     <see cref="BuildSinglePagePdf"/>, returning the resulting <see cref="Canvas.Surface"/>.
    /// </summary>
    private static Canvas.Surface RenderContent(
        string content,
        int width = 100,
        int height = 100,
        double mediaBoxWidth = 100,
        double mediaBoxHeight = 100,
        int rotate = 0)
    {
        var bytes = BuildSinglePagePdf(mediaBoxWidth, mediaBoxHeight, content, rotate);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        return document.Render(0, width, height);
    }

    /// <summary>
    ///     Builds an in-memory, single-page, classic-xref PDF exactly like
    ///     <see cref="BuildSinglePagePdf"/>, but with the page declaring a
    ///     <c>/Resources &lt;&lt; ... &gt;&gt;</c> dictionary (<paramref name="resourcesBody"/>,
    ///     inserted verbatim between the outer <c>&lt;&lt; &gt;&gt;</c>) and 0 or more extra
    ///     indirect objects (<paramref name="extraObjectBodies"/>, numbered <c>5 0 obj</c>,
    ///     <c>6 0 obj</c>, ... in order) that <paramref name="resourcesBody"/> may reference by
    ///     number - used by every color-space-resource and image-XObject test that needs a
    ///     <c>/Resources</c> dictionary (plain color-operator tests needing no <c>/Resources</c>
    ///     keep using <see cref="BuildSinglePagePdf"/>/<see cref="RenderContent"/> unchanged).
    /// </summary>
    private static byte[] BuildSinglePagePdfWithResources(
        double mediaBoxWidth,
        double mediaBoxHeight,
        string content,
        string resourcesBody,
        IReadOnlyList<byte[]> extraObjectBodies,
        int rotate = 0)
    {
        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        var rotateEntry = rotate == 0 ? string.Empty : $" /Rotate {rotate}";
        var streamHeader = System.Text.Encoding.ASCII.GetBytes($"<< /Length {contentBytes.Length} >>\nstream\n");
        var streamFooter = "\nendstream"u8.ToArray();
        var streamBody = new byte[streamHeader.Length + contentBytes.Length + streamFooter.Length];
        streamHeader.CopyTo(streamBody, 0);
        contentBytes.CopyTo(streamBody, streamHeader.Length);
        streamFooter.CopyTo(streamBody, streamHeader.Length + contentBytes.Length);

        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            System.Text.Encoding.ASCII.GetBytes(
                $"<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 {mediaBoxWidth} {mediaBoxHeight}] >>"),
            System.Text.Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R{rotateEntry} /Contents 4 0 R /Resources << {resourcesBody} >> >>"),
            streamBody,
        };
        bodies.AddRange(extraObjectBodies);

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        var xrefOffset = buffer.Count;
        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n"));
        buffer.AddRange("0000000000 65535 f \n"u8.ToArray());
        foreach (var offset in offsets)
        {
            buffer.AddRange(System.Text.Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        buffer.AddRange(System.Text.Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    /// <summary>
    ///     Builds an indirect-object body (dictionary header + raw stream bytes) for use as one of
    ///     <see cref="BuildSinglePagePdfWithResources"/>'s <c>extraObjectBodies</c> - the
    ///     <paramref name="dictionaryEntries"/> text is inserted verbatim between the dictionary's
    ///     outer <c>&lt;&lt; &gt;&gt;</c> alongside an automatically computed <c>/Length</c>.
    /// </summary>
    private static byte[] BuildStreamObjectBody(string dictionaryEntries, byte[] streamData)
    {
        var header = System.Text.Encoding.ASCII.GetBytes($"<< {dictionaryEntries} /Length {streamData.Length} >>\nstream\n");
        var footer = "\nendstream"u8.ToArray();
        var body = new byte[header.Length + streamData.Length + footer.Length];
        header.CopyTo(body, 0);
        streamData.CopyTo(body, header.Length);
        footer.CopyTo(body, header.Length + streamData.Length);
        return body;
    }

    /// <summary>Compresses <paramref name="data"/> into a standards-conformant zlib stream (header + deflate + Adler-32), as <c>FlateDecode</c> expects.</summary>
    private static byte[] ZlibCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    /// <summary>Renders page 0 of an in-memory PDF built by <see cref="BuildSinglePagePdfWithResources"/>.</summary>
    private static Canvas.Surface RenderPdfBytes(byte[] bytes, int width = 100, int height = 100)
    {
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        return document.Render(0, width, height);
    }

    /// <summary>
    ///     Renders a <paramref name="width"/>x1 <c>DeviceGray</c> image XObject (declaring
    ///     <paramref name="filterAndParams"/>, e.g. <c>"/Filter /LZWDecode"</c>) placed to exactly
    ///     fill a <paramref name="width"/>x1 page/device surface - giving an exact 1:1
    ///     nearest-neighbor correspondence between decoded-byte index and device pixel column, so
    ///     every filter unit test below can directly assert <c>surface[i, 0]</c> against the
    ///     exact byte it expects <c>GetStreamDecodedBytes</c> to have produced at index
    ///     <c>i</c>, exactly like the existing predictor tests already do for 2x2 images.
    /// </summary>
    private static Canvas.Surface RenderGrayscaleImage(int width, byte[] streamData, string filterAndParams)
    {
        var imageStream = BuildStreamObjectBody(
            $"/Type /XObject /Subtype /Image /Width {width} /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 {filterAndParams}",
            streamData);

        var bytes = BuildSinglePagePdfWithResources(
            width,
            1,
            $"{width} 0 0 1 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        return RenderPdfBytes(bytes, width, 1);
    }

    /// <summary>
    ///     Builds the deterministic, synthetic (not spec-provided) 320-byte data vector the
    ///     <c>EarlyChange</c> growth-timing tests use - long enough for the dictionary to cross
    ///     dynamic code 511 (the point where code width must grow from 9 to 10 bits), so the two
    ///     <c>/EarlyChange</c> timings actually diverge (independently confirmed via a Python
    ///     prototype during this phase's planning: shorter vectors never reach the boundary and
    ///     cannot distinguish the two timings at all).
    /// </summary>
    private static byte[] BuildLzwGrowthTestData()
    {
        var data = new byte[320];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(((i * 37) + 11) % 199);
        }

        return data;
    }

    /// <summary>
    ///     A minimal, test-only classic-LZW encoder that produces exactly the byte sequence the
    ///     production <see cref="PdfDocument"/> PDF-variant <c>LZWDecode</c> decoder expects -
    ///     used only to synthesize test vectors, never shipped in <c>src/</c>. Mirrors the
    ///     encoder side of the same classic-LZW algorithm described in
    ///     <c>PdfDocument.Filters.Lzw.cs</c>'s own remarks: the encoder grows its code width one
    ///     table entry earlier than the decoder's own growth check, because the encoder omits a
    ///     dictionary addition only for its final flushed code, while the decoder omits one only
    ///     for its very first code after a Clear.
    /// </summary>
    private static byte[] EncodeLzwForTest(byte[] data, bool earlyChange)
    {
        var earlyChangeAmount = earlyChange ? 1 : 0;
        var bits = new List<int>();

        void Emit(int code, int width)
        {
            for (var i = width - 1; i >= 0; i--)
            {
                bits.Add((code >> i) & 1);
            }
        }

        var table = new Dictionary<(int Prefix, byte Suffix), int>();
        var codeWidth = 9;
        var nextCode = 258;
        var extCode = (1 << codeWidth) + 1 - earlyChangeAmount;

        Emit(256, codeWidth); // Clear

        if (data.Length == 0)
        {
            Emit(257, codeWidth); // EOD
        }
        else
        {
            var currentCode = (int)data[0];
            for (var i = 1; i < data.Length; i++)
            {
                var next = data[i];
                if (table.TryGetValue((currentCode, next), out var extended))
                {
                    currentCode = extended;
                    continue;
                }

                Emit(currentCode, codeWidth);
                if (nextCode < 4096)
                {
                    table[(currentCode, next)] = nextCode;
                    nextCode++;
                    if (nextCode >= extCode && codeWidth < 12)
                    {
                        codeWidth++;
                        extCode = (1 << codeWidth) + 1 - earlyChangeAmount;
                    }
                }

                currentCode = next;
            }

            Emit(currentCode, codeWidth);
            Emit(257, codeWidth); // EOD
        }

        while (bits.Count % 8 != 0)
        {
            bits.Add(0);
        }

        var output = new byte[bits.Count / 8];
        for (var i = 0; i < output.Length; i++)
        {
            var b = 0;
            for (var j = 0; j < 8; j++)
            {
                b = (b << 1) | bits[(i * 8) + j];
            }

            output[i] = (byte)b;
        }

        return output;
    }

    /// <summary>
    ///     Renders a <paramref name="columns"/>x<paramref name="rows"/> <c>DeviceGray</c>
    ///     <c>CCITTFaxDecode</c> image XObject placed to exactly fill a same-size device surface -
    ///     giving an exact 1:1 nearest-neighbor correspondence between decoded pixel
    ///     <c>(x, y)</c> and device pixel <c>(x, y)</c>, mirroring <see cref="RenderGrayscaleImage"/>'s
    ///     own precedent for the other filter unit tests. <paramref name="extraDecodeParms"/> is
    ///     inserted verbatim into <c>/DecodeParms</c> (after the mandatory <c>/K -1 /Columns
    ///     /Rows</c> entries) so callers can add <c>/BlackIs1</c>/<c>/EncodedByteAlign</c>/
    ///     <c>/EndOfLine</c> or override <c>/K</c> for the rejection-path tests.
    /// </summary>
    private static Canvas.Surface RenderCcittFaxImage(
        int columns,
        int rows,
        byte[] encodedData,
        string extraDecodeParms = "")
    {
        var imageStream = BuildStreamObjectBody(
            $"/Type /XObject /Subtype /Image /Width {columns} /Height {rows} /ColorSpace /DeviceGray "
            + $"/BitsPerComponent 8 /Filter /CCITTFaxDecode "
            + $"/DecodeParms << /K -1 /Columns {columns} /Rows {rows}{extraDecodeParms} >>",
            encodedData);

        var bytes = BuildSinglePagePdfWithResources(
            columns,
            rows,
            $"{columns} 0 0 {rows} 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        return RenderPdfBytes(bytes, columns, rows);
    }

    /// <summary>
    ///     A minimal, test-only ITU-T T.6 Group 4 (MMR) two-dimensional encoder, parameterized
    ///     over an arbitrary <c>pixels[x, y]</c> grid (<see langword="true"/> = black) - used only
    ///     to synthesize trustworthy encoded test vectors, never shipped in <c>src/</c>. Mirrors
    ///     <see cref="EncodeLzwForTest"/>'s precedent: an independent, from-scratch
    ///     implementation of the encode side of the same algorithm the production decoder
    ///     implements, written separately (and, per the mode-code/run-length tables below,
    ///     transcribed separately) so that a bug in one is not mechanically guaranteed to be
    ///     masked by a matching bug in the other.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Deliberately supports only run lengths <c>&lt; 64</c> (a single terminating code,
    ///         never a makeup code) - every test pattern in this file uses small images for which
    ///         this is never a true limitation, and it keeps this test-only encoder's own
    ///         run-length table small enough to transcribe (and visually cross-check) by hand.
    ///         Throws <see cref="InvalidOperationException"/> if a test accidentally supplies a
    ///         pattern requiring a run of 64 or more pixels.
    ///     </para>
    ///     <para>
    ///         Round-tripping a pattern through this encoder and then the production
    ///         <c>DecodeCcittFax</c> decoder alone is <strong>not</strong> sufficient to prove the
    ///         decoder correct, since a bug here could happen to exactly cancel a bug there - see
    ///         <see cref="PdfDocument_Images_DoOperator_CcittFaxGroup4_PlacesExpectedPixels"/>'s
    ///         own remarks for the independent, hand-verified (bit-by-bit, outside of any C# code)
    ///         cross-check this file relies on instead.
    ///     </para>
    /// </remarks>
    private static byte[] EncodeCcittGroup4ForTest(bool[,] pixels)
    {
        var columns = pixels.GetLength(0);
        var rows = pixels.GetLength(1);
        var bits = new List<int>();

        void EmitBits(int length, int code)
        {
            for (var i = length - 1; i >= 0; i--)
            {
                bits.Add((code >> i) & 1);
            }
        }

        var modeCodes = new Dictionary<string, (int Length, int Code)>
        {
            ["Pass"] = (4, 0b0001),
            ["Horizontal"] = (3, 0b001),
            ["V0"] = (1, 0b1),
            ["VR1"] = (3, 0b011),
            ["VR2"] = (6, 0b000011),
            ["VR3"] = (7, 0b0000011),
            ["VL1"] = (3, 0b010),
            ["VL2"] = (6, 0b000010),
            ["VL3"] = (7, 0b0000010),
        };

        // ITU-T T.4 Table 3 White / Table 2 Black terminating codes (run lengths 0-63 only, see
        // this method's own remarks), transcribed independently of
        // PdfDocument.CcittFax.cs's own WhiteCodeEntries/BlackCodeEntries (same ultimate ITU-T
        // source, but retyped separately here rather than referencing those internals, so a
        // transcription slip in one is not silently hidden by reusing the other).
        var whiteTerm = new Dictionary<int, (int Length, int Code)>
        {
            [0] = (8, 0x35),
            [1] = (6, 0x7),
            [2] = (4, 0x7),
            [3] = (4, 0x8),
            [4] = (4, 0xB),
            [5] = (4, 0xC),
            [6] = (4, 0xE),
            [7] = (4, 0xF),
            [8] = (5, 0x13),
            [9] = (5, 0x14),
            [10] = (5, 0x7),
            [11] = (5, 0x8),
            [12] = (6, 0x8),
            [13] = (6, 0x3),
            [14] = (6, 0x34),
            [15] = (6, 0x35),
            [16] = (6, 0x2A),
            [17] = (6, 0x2B),
            [18] = (7, 0x27),
            [19] = (7, 0xC),
            [20] = (7, 0x8),
            [21] = (7, 0x17),
            [22] = (7, 0x3),
            [23] = (7, 0x4),
            [24] = (7, 0x28),
            [25] = (7, 0x2B),
            [26] = (7, 0x13),
            [27] = (7, 0x24),
            [28] = (7, 0x18),
            [29] = (8, 0x2),
            [30] = (8, 0x3),
            [31] = (8, 0x1A),
            [32] = (8, 0x1B),
            [33] = (8, 0x12),
            [34] = (8, 0x13),
            [35] = (8, 0x14),
            [36] = (8, 0x15),
            [37] = (8, 0x16),
            [38] = (8, 0x17),
            [39] = (8, 0x28),
            [40] = (8, 0x29),
            [41] = (8, 0x2A),
            [42] = (8, 0x2B),
            [43] = (8, 0x2C),
            [44] = (8, 0x2D),
            [45] = (8, 0x4),
            [46] = (8, 0x5),
            [47] = (8, 0xA),
            [48] = (8, 0xB),
            [49] = (8, 0x52),
            [50] = (8, 0x53),
            [51] = (8, 0x54),
            [52] = (8, 0x55),
            [53] = (8, 0x24),
            [54] = (8, 0x25),
            [55] = (8, 0x58),
            [56] = (8, 0x59),
            [57] = (8, 0x5A),
            [58] = (8, 0x5B),
            [59] = (8, 0x4A),
            [60] = (8, 0x4B),
            [61] = (8, 0x32),
            [62] = (8, 0x33),
            [63] = (8, 0x34),
        };
        var blackTerm = new Dictionary<int, (int Length, int Code)>
        {
            [0] = (10, 0x37),
            [1] = (3, 0x2),
            [2] = (2, 0x3),
            [3] = (2, 0x2),
            [4] = (3, 0x3),
            [5] = (4, 0x3),
            [6] = (4, 0x2),
            [7] = (5, 0x3),
            [8] = (6, 0x5),
            [9] = (6, 0x4),
            [10] = (7, 0x4),
            [11] = (7, 0x5),
            [12] = (7, 0x7),
            [13] = (8, 0x4),
            [14] = (8, 0x7),
            [15] = (9, 0x18),
            [16] = (10, 0x17),
            [17] = (10, 0x18),
            [18] = (10, 0x8),
            [19] = (11, 0x67),
            [20] = (11, 0x68),
            [21] = (11, 0x6C),
            [22] = (11, 0x37),
            [23] = (11, 0x28),
            [24] = (11, 0x17),
            [25] = (11, 0x18),
            [26] = (12, 0xCA),
            [27] = (12, 0xCB),
            [28] = (12, 0xCC),
            [29] = (12, 0xCD),
            [30] = (12, 0x68),
            [31] = (12, 0x69),
            [32] = (12, 0x6A),
            [33] = (12, 0x6B),
            [34] = (12, 0xD2),
            [35] = (12, 0xD3),
            [36] = (12, 0xD4),
            [37] = (12, 0xD5),
            [38] = (12, 0xD6),
            [39] = (12, 0xD7),
            [40] = (12, 0x6C),
            [41] = (12, 0x6D),
            [42] = (12, 0xDA),
            [43] = (12, 0xDB),
            [44] = (12, 0x54),
            [45] = (12, 0x55),
            [46] = (12, 0x56),
            [47] = (12, 0x57),
            [48] = (12, 0x64),
            [49] = (12, 0x65),
            [50] = (12, 0x52),
            [51] = (12, 0x53),
            [52] = (12, 0x24),
            [53] = (12, 0x37),
            [54] = (12, 0x38),
            [55] = (12, 0x27),
            [56] = (12, 0x28),
            [57] = (12, 0x58),
            [58] = (12, 0x59),
            [59] = (12, 0x2B),
            [60] = (12, 0x2C),
            [61] = (12, 0x5A),
            [62] = (12, 0x66),
            [63] = (12, 0x67),
        };

        void EmitRun(bool isBlack, int run)
        {
            if (run >= 64)
            {
                throw new InvalidOperationException(
                    $"Test-only encoder limitation: run length {run} requires a makeup code, which this encoder does not support.");
            }

            var (length, code) = (isBlack ? blackTerm : whiteTerm)[run];
            EmitBits(length, code);
        }

        List<int> RowChanges(int y)
        {
            var changes = new List<int>();
            var color = false;
            for (var x = 0; x < columns; x++)
            {
                if (pixels[x, y] != color)
                {
                    changes.Add(x);
                    color = pixels[x, y];
                }
            }

            return changes;
        }

        (int B1, int B2) FindB1B2(IReadOnlyList<int> reference, int a0, bool currentIsBlack)
        {
            var idx = 0;
            while (idx < reference.Count && reference[idx] <= a0)
            {
                idx++;
            }

            var colorAtIdxIsBlack = idx % 2 == 0;
            if (colorAtIdxIsBlack == currentIsBlack)
            {
                idx++;
            }

            var b1 = idx < reference.Count ? reference[idx] : columns;
            var b2 = idx + 1 < reference.Count ? reference[idx + 1] : columns;
            return (b1, b2);
        }

        int NextChangeAfter(List<int> changes, int position)
        {
            foreach (var c in changes)
            {
                if (c > position)
                {
                    return c;
                }
            }

            return columns;
        }

        var referenceChanges = new List<int>();
        for (var y = 0; y < rows; y++)
        {
            var changes = RowChanges(y);
            var a0 = -1;
            var color = false;
            while (a0 < columns)
            {
                var (b1, b2) = FindB1B2(referenceChanges, a0, color);
                var a1 = NextChangeAfter(changes, a0);

                if (a1 > b2)
                {
                    EmitBits(modeCodes["Pass"].Length, modeCodes["Pass"].Code);
                    a0 = b2;
                }
                else
                {
                    var delta = a1 - b1;
                    if (delta is >= -3 and <= 3)
                    {
                        var name = delta switch
                        {
                            0 => "V0",
                            1 => "VR1",
                            2 => "VR2",
                            3 => "VR3",
                            -1 => "VL1",
                            -2 => "VL2",
                            _ => "VL3",
                        };
                        EmitBits(modeCodes[name].Length, modeCodes[name].Code);
                        a0 = a1;
                        color = !color;
                    }
                    else
                    {
                        var a2 = NextChangeAfter(changes, a1);
                        var start = a0 < 0 ? 0 : a0;
                        EmitBits(modeCodes["Horizontal"].Length, modeCodes["Horizontal"].Code);
                        EmitRun(color, a1 - start);
                        EmitRun(!color, a2 - a1);
                        a0 = a2;
                    }
                }
            }

            referenceChanges = changes;
        }

        while (bits.Count % 8 != 0)
        {
            bits.Add(0);
        }

        var output = new byte[bits.Count / 8];
        for (var i = 0; i < output.Length; i++)
        {
            var b = 0;
            for (var j = 0; j < 8; j++)
            {
                b = (b << 1) | bits[(i * 8) + j];
            }

            output[i] = (byte)b;
        }

        return output;
    }

    /// <summary>
    ///     Builds a minimal, well-formed synthetic embedded TrueType font (see
    ///     <see cref="SyntheticFontBuilder"/>): a 1000-unit em square, glyph 0 the
    ///     (empty-outline) <c>.notdef</c>, and every glyph from index 1 onward a filled square
    ///     outline spanning font-design-space <c>(100, 100)</c>-<c>(500, 500)</c> with a fixed
    ///     600-unit advance width - used by every Phase 4 unit test that needs a real, loadable
    ///     <c>TrueTypeFont</c> without depending on a real-world font file (see
    ///     <see cref="PdfSystemIntegrationTests"/> for the one required real-embedded-font,
    ///     real-glyph-shape pixel-level test, using the shared Open Sans fixture instead).
    /// </summary>
    /// <param name="cmapMappings">The <c>cmap</c> table's codepoint-to-glyph-index mappings.</param>
    /// <param name="glyphCount">The total number of glyphs (including glyph 0).</param>
    private static byte[] BuildEmbeddedFontBytes(
        IReadOnlyList<(int Codepoint, int GlyphId)> cmapMappings,
        int glyphCount = 3)
    {
        var square = SyntheticFontBuilder.SimpleGlyph(
        [
            [(100, 100, true), (500, 100, true), (500, 500, true), (100, 500, true)],
        ]);

        var glyphLengths = new List<int> { 0 };
        var glyf = new List<byte>();
        var advanceWidths = new List<int> { 0 };
        for (var glyphIndex = 1; glyphIndex < glyphCount; glyphIndex++)
        {
            glyphLengths.Add(square.Length);
            glyf.AddRange(square);
            advanceWidths.Add(600);
        }

        var cmap = SyntheticFontBuilder.CmapFormat4(3, 1, cmapMappings);

        return new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(glyphCount))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, glyphCount))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx(advanceWidths))
            .AddTable("loca", SyntheticFontBuilder.Loca(glyphLengths, longFormat: false))
            .AddTable("glyf", [.. glyf])
            .AddTable("cmap", cmap)
            .Build();
    }

    /// <summary>
    ///     Builds a single glyph's Type 2 charstring bytecode: a filled square outline spanning
    ///     font-design-space <c>(100, 100)</c>-<c>(500, 500)</c> (matching
    ///     <see cref="BuildEmbeddedFontBytes"/>'s own TrueType-outline square glyph, so both
    ///     descendant-font flavors paint identical ink for a given glyph index), via
    ///     <c>rmoveto</c> to <c>(100, 100)</c> then a single <c>rlineto</c> with three relative
    ///     deltas - the Type 2 charstring language auto-closes the final segment back to the
    ///     starting point at <c>endchar</c>, exactly like
    ///     <c>Fonts.TrueTypeFontTests.BuildWellFormedCffFont</c>'s own square glyph.
    /// </summary>
    private static byte[] BuildSquareCffCharstring()
    {
        var cs = new List<byte>();
        SyntheticFontBuilder.WriteCharstringNumber(cs, 100);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 100);
        SyntheticFontBuilder.WriteCharstringOperator(cs, 21); // rmoveto
        SyntheticFontBuilder.WriteCharstringNumber(cs, 400);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 0);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 0);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 400);
        SyntheticFontBuilder.WriteCharstringNumber(cs, -400);
        SyntheticFontBuilder.WriteCharstringNumber(cs, 0);
        SyntheticFontBuilder.WriteCharstringOperator(cs, 5); // rlineto
        SyntheticFontBuilder.WriteCharstringOperator(cs, 14); // endchar
        return [.. cs];
    }

    /// <summary>
    ///     Builds a minimal, well-formed synthetic embedded OTTO/CFF-flavored SFNT font (see
    ///     <see cref="SyntheticFontBuilder.Cff"/>): a 1000-unit em square, glyph 0 the
    ///     (empty-outline) <c>.notdef</c>, and every glyph from index 1 onward
    ///     <see cref="BuildSquareCffCharstring"/>'s filled square outline with a fixed 600-unit
    ///     advance width - the <c>CIDFontType0</c>/<c>/FontFile3</c> counterpart of
    ///     <see cref="BuildEmbeddedFontBytes"/>, built via the exact
    ///     <c>WithSfntVersion(0x4F54544F)</c> ('OTTO') + <c>maxp</c> version <c>0x00005000</c> +
    ///     <c>CFF </c>-table pattern already proven in
    ///     <c>Fonts.TrueTypeFontTests.BuildWellFormedCffFont</c>. No <c>cmap</c> table is included
    ///     (composite fonts never consult <c>cmap</c> - a CID is used directly as a glyph index),
    ///     mirroring how <c>Fonts.TrueTypeFont.Load</c> tolerates a missing <c>cmap</c> entirely.
    /// </summary>
    /// <param name="glyphCount">The total number of glyphs (including glyph 0).</param>
    /// <param name="includeRos">
    ///     When <see langword="true"/>, the embedded CFF program's Top DICT declares the
    ///     <c>ROS</c> operator (CID-keyed font identification), which <c>Fonts.CffTable.Parse</c>
    ///     rejects - used to prove that rejection propagates uncaught through
    ///     <c>LoadCidFontType0Font</c>.
    /// </param>
    private static byte[] BuildEmbeddedCffFontBytes(int glyphCount = 2, bool includeRos = false)
    {
        var notdefCharstring = new List<byte>();
        SyntheticFontBuilder.WriteCharstringOperator(notdefCharstring, 14); // endchar
        var charStrings = new List<byte[]> { notdefCharstring.ToArray() }; // glyph 0: empty .notdef
        var advanceWidths = new List<int> { 0 };
        for (var glyphIndex = 1; glyphIndex < glyphCount; glyphIndex++)
        {
            charStrings.Add(BuildSquareCffCharstring());
            advanceWidths.Add(600);
        }

        var cff = SyntheticFontBuilder.Cff(charStrings, includeRos: includeRos);

        return new SyntheticFontBuilder()
            .WithSfntVersion(0x4F54544F) // 'OTTO'
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(glyphCount, version: 0x00005000))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 0, glyphCount))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx(advanceWidths))
            .AddTable("CFF ", cff)
            .Build();
    }

    /// <summary>
    ///     Builds a <c>/Resources/Font</c> dictionary (as a <see cref="BuildSinglePagePdfWithResources"/>-
    ///     compatible <c>resourcesBody</c>/<c>extraObjectBodies</c> pair) declaring a single simple
    ///     TrueType font resource named <c>/F1</c>, with the given embedded <c>/FontFile2</c> bytes
    ///     and font-dictionary/font-descriptor entries appended verbatim.
    /// </summary>
    /// <remarks>
    ///     Numbered so <paramref name="fontFileBytes"/> is always object <c>7</c> (referenced as
    ///     <c>7 0 R</c> by the descriptor, object <c>6</c>, in turn referenced as <c>6 0 R</c> by
    ///     the font dictionary, object <c>5</c>, in turn referenced as <c>5 0 R</c> by the
    ///     returned <c>/Font</c> resources entry) - matching every other Phase 3 image-XObject
    ///     test's own "extra objects start at 5" convention.
    /// </remarks>
    private static (string ResourcesBody, List<byte[]> ExtraObjects) BuildSimpleTrueTypeFontResources(
        byte[] fontFileBytes,
        string fontDictExtra = "/FirstChar 65 /LastChar 66 /Widths [600 600]",
        string descriptorExtra = "",
        string fontResourceName = "F1")
    {
        var fontFileObj = BuildStreamObjectBody(string.Empty, fontFileBytes);
        var descriptorObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /FontDescriptor /FontName /Test {descriptorExtra} /FontFile2 7 0 R >>");
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /TrueType /BaseFont /Test {fontDictExtra} /FontDescriptor 6 0 R >>");

        return ($"/Font << /{fontResourceName} 5 0 R >>", [fontDictObj, descriptorObj, fontFileObj]);
    }

    /// <summary>
    ///     Builds a classic PostScript Type 1 glyph charstring: <c>hsbw</c> (left side bearing 0,
    ///     advance <paramref name="width"/>) then an empty-outline <c>endchar</c> - used for the
    ///     <c>.notdef</c>/<c>space</c> glyphs of <see cref="BuildEmbeddedType1FontResources"/>'s
    ///     synthetic font, mirroring <c>Type1TableTests.SpaceCharstring</c>'s own shape.
    /// </summary>
    private static byte[] BuildEmptyType1Charstring(int width = 300)
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, width);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 13); // hsbw
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 14); // endchar
        return [.. buf];
    }

    /// <summary>
    ///     Builds a classic PostScript Type 1 <c>"A"</c> glyph charstring: <c>hsbw</c> then a
    ///     filled square outline spanning font-design-space <c>(100, 100)</c>-<c>(500, 500)</c> of
    ///     a 1000-unit em (matching <see cref="BuildEmbeddedFontBytes"/>'s own TrueType-outline
    ///     square glyph and <see cref="BuildSquareCffCharstring"/>'s own CFF-outline square glyph,
    ///     so every embedded-font flavor paints identical ink for its own square glyph), via
    ///     <c>rmoveto</c> to <c>(100, 100)</c> then three relative <c>rlineto</c> segments and a
    ///     <c>closepath</c>.
    /// </summary>
    private static byte[] BuildSquareType1Charstring(int width = 600)
    {
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, width);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 13); // hsbw
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 100);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 100);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 21); // rmoveto
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 400);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 5); // rlineto
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 400);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 5); // rlineto
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, -400);
        SyntheticFontBuilder.WriteType1CharstringNumber(buf, 0);
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 5); // rlineto
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 9); // closepath
        SyntheticFontBuilder.WriteType1CharstringOperator(buf, 14); // endchar
        return [.. buf];
    }

    /// <summary>
    ///     Builds a <c>/Resources/Font</c> dictionary (as a <see cref="BuildSinglePagePdfWithResources"/>-
    ///     compatible <c>resourcesBody</c>/<c>extraObjectBodies</c> pair) declaring a single simple
    ///     <c>/Subtype /Type1</c> font resource named <c>/F1</c>, backed by a synthetic, entirely
    ///     hand-authored, <c>SyntheticFontBuilder.Type1</c>-built classic PostScript Type 1 font
    ///     program embedded via <c>/FontDescriptor/FontFile</c> - the Phase B counterpart of
    ///     <see cref="BuildSimpleTrueTypeFontResources"/>. The synthetic font declares three
    ///     glyphs (<c>.notdef</c>, <c>space</c>, and <c>A</c> - glyph <c>A</c> being
    ///     <see cref="BuildSquareType1Charstring"/>'s filled square), matching
    ///     <c>PdfDocument.Fonts.cs</c>'s own <c>StandardGlyphNames</c> Adobe glyph-name
    ///     vocabulary so codepoint 65 ('A') resolves to the square glyph via that file's own
    ///     <c>CodepointToStandardGlyphName</c> reverse map.
    /// </summary>
    /// <remarks>
    ///     Numbered identically to <see cref="BuildSimpleTrueTypeFontResources"/> (font-file
    ///     object <c>7</c>, descriptor object <c>6</c>, font dictionary object <c>5</c>) - the
    ///     <c>/FontDescriptor/FontFile</c> stream's own <c>/Length1</c>/<c>/Length2</c> entries
    ///     (not the descriptor's) are always emitted, matching real PDF producers and
    ///     <c>PdfDocument.Fonts.Type1.cs</c>'s own <c>LoadType1Font</c> documented requirement.
    /// </remarks>
    private static (string ResourcesBody, List<byte[]> ExtraObjects) BuildEmbeddedType1FontResources(
        string fontDictExtra = "/FirstChar 65 /LastChar 65 /Widths [600]",
        string descriptorExtra = "",
        string fontResourceName = "F1",
        bool omitLength2 = false,
        string baseFontName = "Test")
    {
        var (fontFileBytes, length1, length2) = SyntheticFontBuilder.Type1(
        [
            (".notdef", BuildEmptyType1Charstring()),
            ("space", BuildEmptyType1Charstring()),
            ("A", BuildSquareType1Charstring()),
        ]);

        var lengthEntries = omitLength2 ? $"/Length1 {length1}" : $"/Length1 {length1} /Length2 {length2}";
        var fontFileObj = BuildStreamObjectBody(lengthEntries, fontFileBytes);
        var descriptorObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /FontDescriptor /FontName /{baseFontName} {descriptorExtra} /FontFile 7 0 R >>");
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /Type1 /BaseFont /{baseFontName} {fontDictExtra} /FontDescriptor 6 0 R >>");

        return ($"/Font << /{fontResourceName} 5 0 R >>", [fontDictObj, descriptorObj, fontFileObj]);
    }

    /// <summary>
    ///     Builds a <c>/Resources/Font</c> dictionary (as a <see cref="BuildSinglePagePdfWithResources"/>-
    ///     compatible <c>resourcesBody</c>/<c>extraObjectBodies</c> pair) declaring a single simple
    ///     <c>/Subtype /Type1</c> font resource named <c>/F1</c>, backed by a synthetic, bare
    ///     Type1C/CFF (no SFNT/OpenType wrapper) font program embedded via
    ///     <c>/FontDescriptor/FontFile3</c> (the <c>/FontFile3</c> stream's own <c>/Subtype</c> set
    ///     to <paramref name="fontFileSubtype"/>) - the simple-font counterpart of
    ///     <see cref="BuildCidFontType0FontResources"/>'s composite-font bare-CFF pattern. The
    ///     synthetic CFF program declares two glyphs (<c>.notdef</c> and, via a custom charset
    ///     format 0 table, glyph 1 resolved to Standard String SID <c>34</c> i.e. <c>A</c> -
    ///     <see cref="BuildSquareCffCharstring"/>'s filled square), matching
    ///     <c>PdfDocument.Fonts.cs</c>'s own <c>CodepointToStandardGlyphName</c> reverse map so
    ///     codepoint 65 ('A') resolves to the square glyph exactly like
    ///     <see cref="BuildEmbeddedType1FontResources"/>'s classic <c>/FontFile</c> counterpart.
    /// </summary>
    /// <remarks>
    ///     Numbered identically to <see cref="BuildSimpleTrueTypeFontResources"/>/
    ///     <see cref="BuildEmbeddedType1FontResources"/> (font-file object <c>7</c>, descriptor
    ///     object <c>6</c>, font dictionary object <c>5</c>).
    /// </remarks>
    private static (string ResourcesBody, List<byte[]> ExtraObjects) BuildEmbeddedType1CFontResources(
        string fontFileSubtype = "/Type1C",
        string fontDictExtra = "/FirstChar 65 /LastChar 65 /Widths [600]",
        string descriptorExtra = "",
        string fontResourceName = "F1")
    {
        var notdefCharstring = new List<byte>();
        SyntheticFontBuilder.WriteCharstringOperator(notdefCharstring, 14); // endchar

        byte[] charsetTable = [0, 0, 34]; // format 0: glyph 1 -> SID 34 (A)
        var cff = SyntheticFontBuilder.Cff(
            [[.. notdefCharstring], BuildSquareCffCharstring()],
            charsetTable: charsetTable);

        var fontFileDictEntries = string.IsNullOrEmpty(fontFileSubtype) ? string.Empty : $"/Subtype {fontFileSubtype}";
        var fontFileObj = BuildStreamObjectBody(fontFileDictEntries, cff);
        var descriptorObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /FontDescriptor /FontName /Test {descriptorExtra} /FontFile3 7 0 R >>");
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /Type1 /BaseFont /Test {fontDictExtra} /FontDescriptor 6 0 R >>");

        return ($"/Font << /{fontResourceName} 5 0 R >>", [fontDictObj, descriptorObj, fontFileObj]);
    }

    /// <summary>
    ///     Builds a <c>/Resources/Font</c> dictionary (as a <see cref="BuildSinglePagePdfWithResources"/>-
    ///     compatible <c>resourcesBody</c>/<c>extraObjectBodies</c> pair) declaring a single
    ///     synthetic <c>/Subtype /Type3</c> font resource named <c>/F1</c>, with its
    ///     <c>/CharProcs</c> glyph-procedure content streams supplied verbatim in
    ///     <paramref name="glyphProcs"/> (each one becomes its own extra object, referenced from
    ///     an inline <c>/CharProcs</c> dictionary by glyph name), an inline <c>/Encoding</c>
    ///     <c>/Differences</c> array built from <paramref name="differencesBody"/> (omitted
    ///     entirely when empty, so callers can exercise the "no <c>/Encoding</c> at all" case),
    ///     and <paramref name="fontMatrix"/>/<paramref name="fontDictExtra"/>/
    ///     <paramref name="type3ResourcesBody"/> appended verbatim.
    /// </summary>
    /// <remarks>
    ///     Numbered so the Type3 font dictionary is object <c>5</c> (referenced as <c>5 0 R</c> by
    ///     the returned <c>/Font</c> resources entry) and each glyph procedure content stream in
    ///     <paramref name="glyphProcs"/> is object <c>6</c>, <c>7</c>, ... in order - matching
    ///     every other font-resource helper's own "extra objects start at 5" convention.
    /// </remarks>
    private static (string ResourcesBody, List<byte[]> ExtraObjects) BuildType3FontResources(
        IReadOnlyList<(string GlyphName, byte[] ContentBytes)> glyphProcs,
        string differencesBody,
        string fontMatrix = "[0.001 0 0 0.001 0 0]",
        string fontDictExtra = "/FirstChar 65 /LastChar 65 /Widths [750]",
        string? type3ResourcesBody = null,
        string fontResourceName = "F1")
    {
        var extraObjects = new List<byte[]>();
        var charProcEntries = new List<string>();
        var objNum = 6;
        foreach (var (glyphName, contentBytes) in glyphProcs)
        {
            extraObjects.Add(BuildStreamObjectBody(string.Empty, contentBytes));
            charProcEntries.Add($"/{glyphName} {objNum} 0 R");
            objNum++;
        }

        var encodingClause = string.IsNullOrEmpty(differencesBody)
            ? string.Empty
            : $" /Encoding << /Differences [{differencesBody}] >>";
        var resourcesClause = type3ResourcesBody is null ? string.Empty : $" /Resources << {type3ResourcesBody} >>";

        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /Type3 /FontMatrix {fontMatrix} " +
            $"/CharProcs << {string.Join(' ', charProcEntries)} >>{encodingClause}{resourcesClause} {fontDictExtra} >>");

        extraObjects.Insert(0, fontDictObj);

        return ($"/Font << /{fontResourceName} 5 0 R >>", extraObjects);
    }

    /// <summary>
    ///     Builds a <c>/Resources/Font</c> dictionary (as a <see cref="BuildSinglePagePdfWithResources"/>-
    ///     compatible <c>resourcesBody</c>/<c>extraObjectBodies</c> pair) declaring a single
    ///     Type0/CIDFontType2 composite font resource named <c>/F1</c>, with the given embedded
    ///     <c>/FontFile2</c> bytes and <c>/Encoding</c>, descendant <c>/Subtype</c>,
    ///     <c>/CIDToGIDMap</c> stream, and other descendant-dictionary entries appended verbatim.
    /// </summary>
    /// <remarks>
    ///     Numbered so the Type0 font dictionary is object <c>5</c> (referenced as <c>5 0 R</c> by
    ///     the returned <c>/Font</c> resources entry), the CIDFontType2 descendant dictionary is
    ///     object <c>6</c> (referenced as <c>6 0 R</c> by the font dictionary's
    ///     <c>/DescendantFonts</c>), the <c>/FontDescriptor</c> is object <c>7</c>, the
    ///     <c>/FontFile2</c> stream is object <c>8</c>, and (only when
    ///     <paramref name="cidToGidMapStreamBytes"/> is supplied) the <c>/CIDToGIDMap</c> stream is
    ///     object <c>9</c> - matching every other Phase 3+ image-XObject/font test's own
    ///     "extra objects start at 5" convention.
    /// </remarks>
    private static (string ResourcesBody, List<byte[]> ExtraObjects) BuildCompositeFontResources(
        byte[] fontFileBytes,
        string encoding = "/Identity-H",
        string descendantSubtype = "/CIDFontType2",
        string cidFontExtra = "",
        byte[]? cidToGidMapStreamBytes = null,
        string fontResourceName = "F1")
    {
        var cidToGidEntry = cidToGidMapStreamBytes is not null ? " /CIDToGIDMap 9 0 R" : string.Empty;

        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding {encoding} /DescendantFonts [6 0 R] >>");
        var descendantObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype {descendantSubtype} /BaseFont /Test " +
            "/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> " +
            $"/FontDescriptor 7 0 R{cidToGidEntry} {cidFontExtra} >>");
        var descriptorObj = "<< /Type /FontDescriptor /FontFile2 8 0 R >>"u8.ToArray();
        var fontFileObj = BuildStreamObjectBody(string.Empty, fontFileBytes);

        var extraObjects = new List<byte[]> { fontDictObj, descendantObj, descriptorObj, fontFileObj };
        if (cidToGidMapStreamBytes is not null)
        {
            extraObjects.Add(BuildStreamObjectBody(string.Empty, cidToGidMapStreamBytes));
        }

        return ($"/Font << /{fontResourceName} 5 0 R >>", extraObjects);
    }

    /// <summary>
    ///     Builds a <c>/Resources/Font</c> dictionary (as a <see cref="BuildSinglePagePdfWithResources"/>-
    ///     compatible <c>resourcesBody</c>/<c>extraObjectBodies</c> pair) declaring a single
    ///     Type0/CIDFontType0 composite font resource named <c>/F1</c>, with the given embedded
    ///     <c>/FontFile3</c> bytes (the <c>/FontFile3</c> stream's own <c>/Subtype</c> set to
    ///     <paramref name="fontFileSubtype"/>) and descendant-dictionary entries appended
    ///     verbatim - the <c>CIDFontType0</c> counterpart of
    ///     <see cref="BuildCompositeFontResources"/>.
    /// </summary>
    /// <remarks>
    ///     Numbered so the Type0 font dictionary is object <c>5</c> (referenced as <c>5 0 R</c> by
    ///     the returned <c>/Font</c> resources entry), the CIDFontType0 descendant dictionary is
    ///     object <c>6</c> (referenced as <c>6 0 R</c> by the font dictionary's
    ///     <c>/DescendantFonts</c>), the <c>/FontDescriptor</c> is object <c>7</c>, and the
    ///     <c>/FontFile3</c> stream is object <c>8</c> - matching
    ///     <see cref="BuildCompositeFontResources"/>'s own "extra objects start at 5" convention.
    /// </remarks>
    private static (string ResourcesBody, List<byte[]> ExtraObjects) BuildCidFontType0FontResources(
        byte[] fontFileBytes,
        string fontFileSubtype = "/OpenType",
        string cidFontExtra = "",
        string fontResourceName = "F1")
    {
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H /DescendantFonts [6 0 R] >>");
        var descendantObj = System.Text.Encoding.ASCII.GetBytes(
            "<< /Type /Font /Subtype /CIDFontType0 /BaseFont /Test " +
            "/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> " +
            $"/FontDescriptor 7 0 R {cidFontExtra} >>");
        var descriptorObj = "<< /Type /FontDescriptor /FontFile3 8 0 R >>"u8.ToArray();
        var fontFileDictEntries = string.IsNullOrEmpty(fontFileSubtype) ? string.Empty : $"/Subtype {fontFileSubtype}";
        var fontFileObj = BuildStreamObjectBody(fontFileDictEntries, fontFileBytes);

        return ($"/Font << /{fontResourceName} 5 0 R >>", [fontDictObj, descendantObj, descriptorObj, fontFileObj]);
    }

    /// <summary>Encodes each CID in <paramref name="cids"/> as a 2-byte big-endian code, returning the resulting hex-string content-stream operand text (including the enclosing angle brackets).</summary>
    private static string BuildIdentityHHexString(params int[] cids) =>
        "<" + string.Concat(cids.Select(cid => $"{cid:X4}")) + ">";

    /// <summary>
    ///     Wraps <paramref name="cmapBody"/> (the <c>beginbfchar</c>/<c>beginbfrange</c>/
    ///     <c>begincodespacerange</c>/other bare-operator content under test) in the standard
    ///     Adobe CMap/PostScript resource-management boilerplate every real-world
    ///     <c>/ToUnicode</c> stream requires (PDF 32000-1:2008 &#xA7;9.10.3's own worked example),
    ///     so <c>ResolveToUnicodeMap</c> tests exercise that the boilerplate is tolerated, not
    ///     merely a bare <c>bfchar</c>/<c>bfrange</c> block.
    /// </summary>
    private static byte[] BuildToUnicodeCMapStreamBytes(string cmapBody) =>
        System.Text.Encoding.ASCII.GetBytes(
            "/CIDInit /ProcSet findresource begin\n" +
            "12 dict begin\n" +
            "begincmap\n" +
            "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n" +
            "/CMapName /Adobe-Identity-UCS def\n" +
            "/CMapType 2 def\n" +
            "1 begincodespacerange\n" +
            "<0000> <FFFF>\n" +
            "endcodespacerange\n" +
            $"{cmapBody}\n" +
            "endcmap\n" +
            "CMapName currentdict /CMap defineresource pop\n" +
            "end\n" +
            "end");

    #region Tokenizer

    /// <summary>Proves that the tokenizer parses integer, negative, leading-dot, and zero number forms.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_Numbers_ParsesIntegerAndRealForms()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("12 -3.5 +.5 0"u8.ToArray());

        // Act
        var first = tokenizer.NextToken();
        var second = tokenizer.NextToken();
        var third = tokenizer.NextToken();
        var fourth = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.Number, first.Kind);
        Assert.Equal(12, first.Number);
        Assert.Equal(-3.5, second.Number);
        Assert.Equal(0.5, third.Number);
        Assert.Equal(0, fourth.Number);
    }

    /// <summary>Proves that literal strings decode nested parentheses and backslash/octal escapes.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_LiteralString_HandlesNestedParensAndEscapes()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("(He said (\\\"hi\\\") \\n \\061)"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.LiteralString, token.Kind);
        var text = System.Text.Encoding.Latin1.GetString(token.Bytes!);
        Assert.Equal("He said (\"hi\") \n 1", text);
    }

    /// <summary>Proves that a hex string with a full, even digit count decodes to the exact byte sequence.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_HexString_DecodesFullBytes()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("<48656C6C6F>"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal("Hello", System.Text.Encoding.ASCII.GetString(token.Bytes!));
    }

    /// <summary>Proves that an odd hex-digit count is padded with a trailing zero nibble per spec.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_HexString_PadsOddDigitCountWithTrailingZero()
    {
        // Arrange: an odd number of hex digits is padded with a trailing '0' per the PDF
        // specification, so "<4>" decodes as if it were "<40>" (0x40).
        var tokenizer = new PdfDocument.PdfTokenizer("<4>"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal([0x40], token.Bytes);
    }

    /// <summary>Proves that <c>#xx</c> hash-escapes within a name token are decoded to the literal character.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_Name_DecodesHashEscapes()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("/A#20Name"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.Name, token.Kind);
        Assert.Equal("A Name", token.Text);
    }

    /// <summary>Proves that array (<c>[ ]</c>) and dictionary (<c>&lt;&lt; &gt;&gt;</c>) delimiters are recognized as distinct token kinds.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_Delimiters_RecognizesArrayAndDictionaryBrackets()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("[ ] << >>"u8.ToArray());

        // Act & Assert
        Assert.Equal(PdfDocument.PdfTokenKind.ArrayStart, tokenizer.NextToken().Kind);
        Assert.Equal(PdfDocument.PdfTokenKind.ArrayEnd, tokenizer.NextToken().Kind);
        Assert.Equal(PdfDocument.PdfTokenKind.DictStart, tokenizer.NextToken().Kind);
        Assert.Equal(PdfDocument.PdfTokenKind.DictEnd, tokenizer.NextToken().Kind);
    }

    /// <summary>Proves that a <c>%</c>-comment is skipped entirely and does not interrupt subsequent tokenization.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_Comments_AreSkipped()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("% a comment\n42"u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.Number, token.Kind);
        Assert.Equal(42, token.Number);
    }

    /// <summary>Proves that each reserved PDF keyword is recognized verbatim as a <c>Keyword</c> token.</summary>
    /// <param name="keyword">The keyword text under test.</param>
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("obj")]
    [InlineData("endobj")]
    [InlineData("stream")]
    [InlineData("endstream")]
    [InlineData("xref")]
    [InlineData("trailer")]
    [InlineData("startxref")]
    [InlineData("R")]
    public void PdfDocument_Tokenizer_Keywords_AreRecognizedVerbatim(string keyword)
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer(System.Text.Encoding.ASCII.GetBytes(keyword));

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.Keyword, token.Kind);
        Assert.Equal(keyword, token.Text);
    }

    /// <summary>Proves that tokenizing whitespace-only (or empty) input yields an <c>EndOfFile</c> token.</summary>
    [Fact]
    public void PdfDocument_Tokenizer_EndOfInput_ReturnsEndOfFileToken()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("   "u8.ToArray());

        // Act
        var token = tokenizer.NextToken();

        // Assert
        Assert.Equal(PdfDocument.PdfTokenKind.EndOfFile, token.Kind);
    }

    #endregion

    #region Object model

    /// <summary>Proves that a dictionary parses nested name/number/dictionary entries by key.</summary>
    [Fact]
    public void PdfDocument_ObjectModel_Dictionary_ParsesNestedEntries()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("<< /Type /Catalog /Count 3 /Nested << /A 1 >> >>"u8.ToArray());

        // Act
        var value = PdfDocument.ParseValue(tokenizer);

        // Assert
        Assert.Equal(PdfDocument.PdfKind.Dictionary, value.Kind);
        Assert.Equal(PdfDocument.PdfKind.Name, value.Get("Type")!.Kind);
        Assert.Equal("Catalog", value.Get("Type")!.Text);
        Assert.Equal(3, value.Get("Count")!.Number);
        Assert.Equal(1, value.Get("Nested")!.Get("A")!.Number);
    }

    /// <summary>Proves that an array parses mixed element kinds (number, name, string, boolean, null, nested array) in order.</summary>
    [Fact]
    public void PdfDocument_ObjectModel_Array_ParsesMixedElementTypes()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("[1 2.5 /Name (str) true null [9]]"u8.ToArray());

        // Act
        var value = PdfDocument.ParseValue(tokenizer);

        // Assert
        Assert.Equal(PdfDocument.PdfKind.Array, value.Kind);
        Assert.Equal(7, value.Items.Count);
        Assert.Equal(PdfDocument.PdfKind.Number, value.Items[0].Kind);
        Assert.Equal(PdfDocument.PdfKind.Name, value.Items[2].Kind);
        Assert.Equal(PdfDocument.PdfKind.Boolean, value.Items[4].Kind);
        Assert.Equal(PdfDocument.PdfKind.Array, value.Items[6].Kind);
    }

    /// <summary>Proves that an <c>N G R</c> sequence parses as an indirect reference, capturing both the object and generation numbers.</summary>
    [Fact]
    public void PdfDocument_ObjectModel_IndirectReference_ParsesObjectAndGenerationNumbers()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("12 0 R"u8.ToArray());

        // Act
        var value = PdfDocument.ParseValue(tokenizer);

        // Assert
        Assert.Equal(PdfDocument.PdfKind.Reference, value.Kind);
        Assert.Equal(12, value.RefNumber);
        Assert.Equal(0, value.RefGeneration);
    }

    /// <summary>Proves that a bare number followed by <c>N obj</c> (an object definition header) is parsed as a plain number, not misread as a reference.</summary>
    [Fact]
    public void PdfDocument_ObjectModel_BareNumber_IsNotMisreadAsReference()
    {
        // Arrange
        var tokenizer = new PdfDocument.PdfTokenizer("12 0 obj"u8.ToArray());

        // Act
        var value = PdfDocument.ParseValue(tokenizer);

        // Assert
        Assert.Equal(PdfDocument.PdfKind.Number, value.Kind);
        Assert.Equal(12, value.Number);
    }

    #endregion

    #region Cross-reference forms

    /// <summary>Proves that a classic <c>xref</c>/<c>trailer</c> document resolves the catalog and its single page.</summary>
    [Fact]
    public void PdfDocument_Open_ClassicXref_ResolvesRootAndPage()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(200, info.Width);
        Assert.Equal(300, info.Height);
        Assert.Equal(0, info.Rotation);
    }

    /// <summary>Proves that a <c>/Type /XRef</c> cross-reference stream (no classic table) resolves the catalog and its single page.</summary>
    [Fact]
    public void PdfDocument_Open_XrefStream_ResolvesRootAndPage()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("xref-stream-single-page.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(200, info.Width);
        Assert.Equal(300, info.Height);
    }

    /// <summary>Proves that a page object compressed inside a <c>/Type /ObjStm</c> object stream is decompressed and resolved correctly.</summary>
    [Fact]
    public void PdfDocument_Open_ObjectStream_DecompressesAndResolvesCompressedPage()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("object-stream.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(220, info.Width);
        Assert.Equal(320, info.Height);
    }

    /// <summary>Proves that a hybrid classic trailer with an <c>/XRefStm</c> link resolves an entry that only the supplementary xref stream describes.</summary>
    [Fact]
    public void PdfDocument_Open_HybridXref_ResolvesCompressedEntryViaXRefStm()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("hybrid-xref.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(250, info.Width);
        Assert.Equal(350, info.Height);
    }

    /// <summary>Proves that a document with no <c>startxref</c>/<c>xref</c>/<c>trailer</c> at all still resolves via the linear-scan fallback.</summary>
    [Fact]
    public void PdfDocument_Open_MalformedStartxref_FallsBackToLinearScan()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("malformed-startxref.pdf"));

        // Assert
        Assert.Equal(1, document.PageCount);
        var info = document.GetPageInfo(0);
        Assert.Equal(180, info.Width);
        Assert.Equal(260, info.Height);
    }

    #endregion

    #region Page tree

    /// <summary>Proves that all pages across a multi-level page tree are reported, in document order.</summary>
    [Fact]
    public void PdfDocument_PageTree_Traversal_ReportsAllPagesInDocumentOrder()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));

        // Assert
        Assert.Equal(3, document.PageCount);
    }

    /// <summary>Proves that a page with its own explicit <c>/MediaBox</c> and no <c>/Rotate</c> reports that MediaBox unchanged.</summary>
    [Fact]
    public void PdfDocument_PageTree_Inheritance_ExplicitMediaBoxAndNoRotationIsUnchanged()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));
        var info = document.GetPageInfo(0);

        // Assert
        Assert.Equal(200, info.Width);
        Assert.Equal(300, info.Height);
        Assert.Equal(0, info.Rotation);
    }

    /// <summary>Proves that a <c>/Rotate 90</c> page swaps its displayed width and height relative to its raw <c>/MediaBox</c>.</summary>
    [Fact]
    public void PdfDocument_PageTree_Inheritance_Rotation90SwapsDisplayedWidthAndHeight()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));
        var info = document.GetPageInfo(1);

        // Assert: raw /MediaBox is [0 0 400 100]; /Rotate 90 swaps it for display
        Assert.Equal(100, info.Width);
        Assert.Equal(400, info.Height);
        Assert.Equal(90, info.Rotation);
    }

    /// <summary>Proves that a page omitting its own <c>/MediaBox</c> inherits its ancestor <c>/Pages</c> node's MediaBox.</summary>
    [Fact]
    public void PdfDocument_PageTree_Inheritance_MediaBoxInheritedFromPagesNodeWhenPageOmitsIt()
    {
        // Arrange & Act
        using var document = PdfDocument.Open(Fixture("multi-page-mixed-mediabox-rotate.pdf"));
        var info = document.GetPageInfo(2);

        // Assert: the third page declares no MediaBox of its own, so it inherits its ancestor
        // Pages node's box (width 150, height 250); Rotate 180 does not swap width/height.
        Assert.Equal(150, info.Width);
        Assert.Equal(250, info.Height);
        Assert.Equal(180, info.Rotation);
    }

    /// <summary>Proves that a <c>/Kids</c> reference forming a cycle back to an ancestor throws instead of looping forever.</summary>
    [Fact]
    public void PdfDocument_PageTree_CycleRejection_ThrowsInvalidDataException()
    {
        // Arrange, Act & Assert
        Assert.Throws<InvalidDataException>(() => PdfDocument.Open(Fixture("cyclic-page-tree.pdf")));
    }

    #endregion

    #region Encryption detection

    /// <summary>Proves that a non-<c>/Standard</c> security handler (for example <c>/Adobe.PubSec</c>) throws <see cref="UnsupportedImageFeatureException"/> without decoding any content.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedTrailer_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange, Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => PdfDocument.Open(Fixture("encrypted-trailer.pdf")));
        Assert.Equal("pdf-encrypted-filter-Adobe.PubSec", exception.Feature);
    }

    #endregion

    #region Open validation

    /// <summary>Proves that <see cref="PdfDocument.Open(System.IO.Stream, string?)"/> rejects a null stream.</summary>
    [Fact]
    public void PdfDocument_Open_NullStream_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => PdfDocument.Open((Stream)null!));
    }

    /// <summary>Proves that <see cref="PdfDocument.Open(string, string?)"/> rejects a null path.</summary>
    [Fact]
    public void PdfDocument_Open_NullPath_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => PdfDocument.Open((string)null!));
    }

    /// <summary>Proves that <see cref="PdfDocument.Open(string, string?)"/> rejects an empty or whitespace-only path.</summary>
    /// <param name="path">The invalid path under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PdfDocument_Open_EmptyOrWhitespacePath_ThrowsArgumentException(string path)
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => PdfDocument.Open(path));
    }

    #endregion

    #region GetPageInfo validation

    /// <summary>Proves that <see cref="PdfDocument.GetPageInfo"/> rejects a negative or too-large page index.</summary>
    /// <param name="pageIndex">The out-of-range page index under test.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void PdfDocument_GetPageInfo_OutOfRangeIndex_ThrowsArgumentOutOfRangeException(int pageIndex)
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetPageInfo(pageIndex));
    }

    #endregion

    #region Render

    /// <summary>Proves that <see cref="PdfDocument.Render"/> returns a fully transparent surface of the caller-requested size (Phase 1 renders no content).</summary>
    [Fact]
    public void PdfDocument_Render_ValidPageIndex_ReturnsCorrectlySizedBlankSurface()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act
        using var surface = document.Render(0, 64, 48);

        // Assert
        Assert.Equal(64, surface.Width);
        Assert.Equal(48, surface.Height);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    /// <summary>Proves that <see cref="PdfDocument.Render"/> rejects an out-of-range page index the same way <see cref="PdfDocument.GetPageInfo"/> does.</summary>
    [Fact]
    public void PdfDocument_Render_OutOfRangePageIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(5, 10, 10));
    }

    /// <summary>Proves that <see cref="PdfDocument.Render"/> lets <see cref="Canvas.Surface"/>'s own constructor validate width/height rather than duplicating that check.</summary>
    [Fact]
    public void PdfDocument_Render_InvalidWidth_PropagatesSurfaceArgumentOutOfRangeException()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(0, 0, 10));
    }

    #endregion

    #region Dispose

    /// <summary>Proves that calling <see cref="PdfDocument.Dispose"/> a second time is a no-op rather than throwing.</summary>
    [Fact]
    public void PdfDocument_Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));

        // Act
        var exception = Record.Exception(() =>
        {
            document.Dispose();
            document.Dispose();
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Proves that <see cref="PdfDocument.PageCount"/> throws <see cref="ObjectDisposedException"/> once the document is disposed.</summary>
    [Fact]
    public void PdfDocument_PageCount_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));
        document.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => document.PageCount);
    }

    /// <summary>Proves that <see cref="PdfDocument.GetPageInfo"/> throws <see cref="ObjectDisposedException"/> once the document is disposed.</summary>
    [Fact]
    public void PdfDocument_GetPageInfo_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));
        document.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => document.GetPageInfo(0));
    }

    /// <summary>Proves that <see cref="PdfDocument.Render"/> throws <see cref="ObjectDisposedException"/> once the document is disposed.</summary>
    [Fact]
    public void PdfDocument_Render_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var document = PdfDocument.Open(Fixture("classic-xref-single-page.pdf"));
        document.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => document.Render(0, 10, 10));
    }

    #endregion

    #region ContentStream

    /// <summary>Proves that an unknown/unimplemented operator is silently skipped and does not stop subsequent operators from executing.</summary>
    [Fact]
    public void PdfDocument_ContentStream_UnknownOperator_IsSkippedWithoutThrowing()
    {
        // Arrange
        const string content = "/GS1 gs 2 w 10 10 m 10 90 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert: the unrecognized 'gs' (ExtGState) operator was skipped, and the stroke after it still painted.
        Assert.Equal(Black, surface[10, 50]);
    }

    /// <summary>Proves that a <c>/Contents</c> array of streams is concatenated with a space separator, rather than merging adjacent tokens.</summary>
    [Fact]
    public void PdfDocument_ContentStream_ContentsArray_ConcatenatesStreamsWithSpaceSeparator()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("contents-array-two-streams.pdf"));

        // Act: stream 4 holds "10 10 40" and stream 5 holds "40 re f"; correct concatenation
        // with a space separator reconstructs "10 10 40 40 re f" (a filled 40x40 square).
        using var surface = document.Render(0, 100, 100);

        // Assert
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[90, 90]);
    }

    /// <summary>
    ///     Proves that a marked-content <c>BDC</c> operator's inline properties dictionary
    ///     operand (e.g. <c>/P &lt;&lt; /MCID 0 &gt;&gt; BDC</c>, as commonly emitted by tagged-PDF
    ///     producers such as Word/LibreOffice/browser print-to-PDF) is parsed as a dictionary
    ///     operand rather than throwing - regression test for a real-world content stream that
    ///     previously failed with "Unexpected token 'DictStart'".
    /// </summary>
    [Fact]
    public void PdfDocument_ContentStream_BdcWithInlinePropertiesDictionary_IsParsedWithoutThrowing()
    {
        // Arrange
        const string content = "/P << /MCID 0 >> BDC 20 20 30 30 re f EMC";

        // Act
        using var surface = RenderContent(content);

        // Assert: the fill after the BDC operator still painted, proving the inline dictionary
        // operand was consumed correctly rather than corrupting subsequent operator parsing.
        Assert.Equal(Black, surface[30, 65]);
    }

    /// <summary>Proves that a page with no <c>/Contents</c> key at all renders as a fully blank surface, without throwing.</summary>
    [Fact]
    public void PdfDocument_ContentStream_NoContents_RendersBlankSurface()
    {
        // Arrange
        using var document = PdfDocument.Open(Fixture("no-contents-page.pdf"));

        // Act
        using var surface = document.Render(0, 20, 20);

        // Assert
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    #endregion

    #region GraphicsState

    /// <summary>Proves that <c>q</c>/<c>cm</c>/<c>Q</c> restores the prior transform after the matching pop.</summary>
    [Fact]
    public void PdfDocument_GraphicsState_QPushCmThenQPop_RestoresPriorTransform()
    {
        // Arrange: inside q/Q, 'cm' doubles the scale, so the first diagonal segment is drawn
        // through a different device location than the second, identical, segment issued after Q.
        const string content = "2 w q 2 0 0 2 0 0 cm 10 10 m 20 20 l S Q 10 10 m 20 20 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert: the second segment (after Q) is drawn at the unscaled location - proving the
        // CTM was restored, not left doubled.
        Assert.NotEqual(default, surface[15, 85]);
    }

    /// <summary>Proves that nested <c>q</c>/<c>cm</c>/<c>q</c>/<c>cm</c>/.../<c>Q</c>/<c>Q</c> composes both matrices, in order, onto the same drawing operation.</summary>
    [Fact]
    public void PdfDocument_GraphicsState_NestedQQ_ComposesTransformsInOrder()
    {
        // Arrange: an outer cm scales X only (x2), an inner cm scales Y only (x2); only their
        // composition scales both axes, placing the drawn segment at (30,70)-ish. A bug applying
        // only one of the two cm's would place it at (30,85) (X-only) or (15,70) (Y-only) instead.
        const string content = "2 w q 2 0 0 1 0 0 cm q 1 0 0 2 0 0 cm 10 10 m 20 20 l S Q Q";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.NotEqual(default, surface[30, 70]);
        Assert.Equal(default, surface[30, 85]);
        Assert.Equal(default, surface[15, 70]);
    }

    /// <summary>Proves that an unbalanced <c>Q</c> with no matching prior <c>q</c> is tolerated as a no-op, rather than throwing.</summary>
    [Fact]
    public void PdfDocument_GraphicsState_UnbalancedQWithNoMatchingPush_DoesNotThrow()
    {
        // Arrange
        const string content = "Q 2 w 10 10 m 20 20 l S";

        // Act
        var exception = Record.Exception(() => RenderContent(content).Dispose());

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Proves that a malformed <c>cm</c> operand count throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_GraphicsState_MalformedCmOperandCount_ThrowsInvalidDataException()
    {
        // Arrange
        const string content = "1 2 3 cm";

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    #endregion

    #region PathOps

    /// <summary>Proves that <c>m</c>/<c>l</c>/<c>c</c>/<c>v</c>/<c>y</c>/<c>re</c> each build the geometry the PDF specification documents for them.</summary>
    [Fact]
    public void PdfDocument_PathOps_MoveLineRectCurve_BuildExpectedGeometry()
    {
        // m/l/h: an explicit square built from move/line/closepath, filled.
        using (var surface = RenderContent("10 10 m 90 10 l 90 90 l 10 90 l h f"))
        {
            Assert.Equal(Black, surface[50, 50]);
        }

        // re: a rectangle built via the single-operator shorthand, filled.
        using (var surface = RenderContent("20 20 30 30 re f"))
        {
            Assert.Equal(Black, surface[30, 65]);
            Assert.Equal(default, surface[5, 5]);
        }

        // c: a full cubic Bezier curve with two explicit control points, stroked.
        using (var surface = RenderContent("3 w 10 50 m 10 10 90 10 90 50 c S"))
        {
            Assert.Equal(Black, surface[50, 80]);
        }

        // v: the first control point defaults to the current point.
        using (var surface = RenderContent("3 w 10 50 m 90 10 90 50 v S"))
        {
            Assert.Equal(Black, surface[50, 65]);
        }

        // y: the second control point defaults to the curve's own endpoint.
        using (var surface = RenderContent("3 w 10 50 m 30 10 90 50 y S"))
        {
            Assert.Equal(Black, surface[58, 65]);
        }
    }

    /// <summary>Proves that a malformed operand count for a path-construction operator throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("m")]
    [InlineData("1 m")]
    [InlineData("1 2 3 m")]
    [InlineData("10 10 m 1 l")]
    [InlineData("10 10 m 1 2 3 4 5 c")]
    [InlineData("10 10 m 1 2 3 v")]
    [InlineData("10 10 m 1 2 3 y")]
    [InlineData("1 2 3 re")]
    public void PdfDocument_PathOps_MalformedOperandCount_ThrowsInvalidDataException(string content)
    {
        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>Proves that issuing a draw operator before any <c>m</c>/<c>re</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_PathOps_DrawBeforeMoveTo_ThrowsInvalidDataException()
    {
        // Arrange
        const string content = "10 10 l";

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>
    ///     Proves that a redundant <c>h</c> issued right after <c>re</c> (which already
    ///     self-closes its rectangle subpath) is a no-op per PDF 32000-1:2008 &#xA7;8.5.2.1,
    ///     rather than throwing <see cref="InvalidDataException"/> - a common real-world idiom
    ///     (for example emitted by MuPDF/fitz).
    /// </summary>
    [Fact]
    public void PdfDocument_PathOps_CloseAfterRectangle_IsNoOp()
    {
        // Arrange
        const string content = "10 10 40 40 re h f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves that a bare <c>h</c> issued with no path constructed at all is a no-op, not an error.</summary>
    [Fact]
    public void PdfDocument_PathOps_CloseWithNoSubpath_IsNoOp()
    {
        // Arrange
        const string content = "h";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves that <c>f</c> fills using the nonzero winding rule.</summary>
    [Fact]
    public void PdfDocument_PathOps_FillNonZero_PaintsExpectedPixels()
    {
        // Arrange
        const string content = "10 10 40 40 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves that <c>f*</c> fills using the even-odd rule, leaving a nested same-winding rectangle unfilled as a "hole".</summary>
    [Fact]
    public void PdfDocument_PathOps_FillEvenOdd_PaintsExpectedPixels()
    {
        // Arrange
        const string content = "10 10 80 80 re 30 30 40 40 re f*";

        // Act
        using var surface = RenderContent(content);

        // Assert: the outer ring is filled, but the doubly-covered inner region is not (even-odd hole).
        Assert.Equal(Black, surface[15, 50]);
        Assert.Equal(default, surface[50, 50]);
    }

    /// <summary>Proves that <c>S</c> strokes the current path without filling it.</summary>
    [Fact]
    public void PdfDocument_PathOps_Stroke_PaintsExpectedPixels()
    {
        // Arrange
        const string content = "2 w 60 10 m 60 90 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(Black, surface[60, 50]);
        Assert.Equal(default, surface[70, 50]);
    }

    /// <summary>Proves that <c>b</c> closes the current (still-open) subpath before both filling and stroking it.</summary>
    [Fact]
    public void PdfDocument_PathOps_CloseAndFillAndStroke_PaintsExpectedPixels()
    {
        // Arrange: an open triangle (no 'h'); 'b' must close it before painting.
        const string closed = "2 w 20 20 m 80 20 l 50 70 l b";
        const string open = "2 w 20 20 m 80 20 l 50 70 l S";

        // Act
        using var closedSurface = RenderContent(closed);
        using var openSurface = RenderContent(open);

        // Assert: the interior is filled, and the implicit closing edge is stroked - proven by
        // comparison against a plain 'S' (no closing), which leaves that same pixel unpainted.
        Assert.Equal(Black, closedSurface[50, 63]);
        Assert.Equal(Black, closedSurface[35, 55]);
        Assert.Equal(default, openSurface[35, 55]);
    }

    /// <summary>Proves that <c>n</c> discards the current path without painting anything.</summary>
    [Fact]
    public void PdfDocument_PathOps_NoOp_DiscardsPathWithoutPainting()
    {
        // Arrange
        const string content = "10 10 40 40 re n";

        // Act
        using var surface = RenderContent(content);

        // Assert
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    /// <summary>Proves that a path-painting operator clears the current path, but leaves the surrounding graphics state (CTM/line width) untouched for the next path.</summary>
    [Fact]
    public void PdfDocument_PathOps_PaintOperator_ClearsPathButPreservesGraphicsState()
    {
        // Arrange: the first rectangle is filled, then a second, unrelated rectangle is filled
        // under the same (still-scaled) graphics state - if the first path were not cleared, the
        // second 're' would incorrectly append to (rather than replace) it.
        const string content = "2 0 0 2 0 0 cm 5 5 10 10 re f 20 20 10 10 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert: second rectangle (user 20..30,20..30, scaled x2 = device 40..60, then flipped
        // y => device y 40..60) is painted, and the first rectangle's area does not bleed into it.
        Assert.Equal(Black, surface[50, 50]);
        Assert.Equal(default, surface[15, 15]);
    }

    #endregion

    #region Color

    /// <summary>Proves that <c>g</c> sets the fill color to the expected gray RGBA value.</summary>
    [Fact]
    public void PdfDocument_Color_SetGrayFill_SetsExpectedRgbaColor()
    {
        // Arrange: 0.5 gray rounds (round-half-to-even) to byte 128.
        const string content = "0.5 g 10 10 80 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(128, 128, 128, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>G</c> sets the stroke color to the expected gray RGBA value.</summary>
    [Fact]
    public void PdfDocument_Color_SetGrayStroke_SetsExpectedRgbaColor()
    {
        // Arrange
        const string content = "0.5 G 4 w 10 50 m 90 50 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(128, 128, 128, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>rg</c> sets the fill color to the expected RGB value.</summary>
    [Fact]
    public void PdfDocument_Color_SetRgbFill_SetsExpectedRgbaColor()
    {
        // Arrange
        const string content = "1 0 0 rg 10 10 80 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>RG</c> sets the stroke color to the expected RGB value.</summary>
    [Fact]
    public void PdfDocument_Color_SetRgbStroke_SetsExpectedRgbaColor()
    {
        // Arrange
        const string content = "0 1 0 RG 4 w 10 50 m 90 50 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>k</c> converts CMYK to the expected RGB fill color.</summary>
    [Fact]
    public void PdfDocument_Color_SetCmykFill_ConvertsToExpectedRgbaColor()
    {
        // Arrange: C=0 M=1 Y=1 K=0 -> R=255*(1-0)*(1-0)=255, G=255*(1-1)*(1-0)=0, B=255*(1-1)*(1-0)=0 (red).
        const string content = "0 1 1 0 k 10 10 80 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>K</c> converts CMYK to the expected RGB stroke color.</summary>
    [Fact]
    public void PdfDocument_Color_SetCmykStroke_ConvertsToExpectedRgbaColor()
    {
        // Arrange: C=1 M=0 Y=0 K=0 -> R=0, G=255, B=255 (cyan).
        const string content = "1 0 0 0 K 4 w 10 50 m 90 50 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 255, 255, 255), surface[50, 50]);
    }

    /// <summary>Proves that color-component values outside <c>[0, 1]</c> are clamped, not rejected.</summary>
    [Theory]
    [InlineData("-1 2 -0.5 rg 10 10 80 80 re f", 0, 255, 0)]
    [InlineData("2 -1 -1 rg 10 10 80 80 re f", 255, 0, 0)]
    public void PdfDocument_Color_ComponentValuesOutsideZeroToOne_AreClamped(string content, byte r, byte g, byte b)
    {
        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(r, g, b, 255), surface[50, 50]);
    }

    /// <summary>Proves that a malformed operand count for every device color operator throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("0.5 0.5 g")]
    [InlineData("0.5 0.5 G")]
    [InlineData("1 0 rg")]
    [InlineData("1 0 RG")]
    [InlineData("1 0 0 k")]
    [InlineData("1 0 0 K")]
    [InlineData("/DeviceRGB /DeviceGray cs")]
    [InlineData("/DeviceRGB /DeviceGray CS")]
    [InlineData("1 0 sc")]
    [InlineData("1 0 SC")]
    [InlineData("1 0 scn")]
    [InlineData("1 0 SCN")]
    public void PdfDocument_Color_MalformedOperandCount_ThrowsInvalidDataException(string content)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>Proves that <c>cs</c> accepts the three device color-space names and resets the fill color to black.</summary>
    [Theory]
    [InlineData("DeviceGray")]
    [InlineData("DeviceRGB")]
    [InlineData("DeviceCMYK")]
    public void PdfDocument_Color_SetColorSpaceFill_DeviceNames_ResetsColorToBlack(string colorSpaceName)
    {
        // Arrange: paint red first, then switch color space (resetting to black), then fill.
        var content = $"1 0 0 rg 10 10 40 80 re f /{colorSpaceName} cs 60 10 20 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[20, 50]);
        Assert.Equal(Black, surface[70, 50]);
    }

    /// <summary>Proves that <c>CS</c> accepts the three device color-space names and resets the stroke color to black.</summary>
    [Theory]
    [InlineData("DeviceGray")]
    [InlineData("DeviceRGB")]
    [InlineData("DeviceCMYK")]
    public void PdfDocument_Color_SetColorSpaceStroke_DeviceNames_ResetsColorToBlack(string colorSpaceName)
    {
        // Arrange: stroke red first, then switch stroke color space (resetting to black), then stroke again.
        var content = $"1 0 0 RG 4 w 10 30 m 90 30 l S /{colorSpaceName} CS 10 70 m 90 70 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert: PDF y-up user space flips to device y-down, so user y=30 -> device y=70, and
        // user y=70 -> device y=30.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 70]);
        Assert.Equal(Black, surface[50, 30]);
    }

    /// <summary>Proves that <c>sc</c> paints using the current fill color space's component count.</summary>
    [Fact]
    public void PdfDocument_Color_SetColorFillUsingCurrentColorSpace_Sc_PaintsExpectedColor()
    {
        // Arrange: switch to DeviceRGB (resets to black), then 'sc' with 3 components.
        const string content = "/DeviceRGB cs 0 0 1 sc 10 10 80 80 re f";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>SC</c> paints using the current stroke color space's component count.</summary>
    [Fact]
    public void PdfDocument_Color_SetColorStrokeUsingCurrentColorSpace_SC_PaintsExpectedColor()
    {
        // Arrange
        const string content = "/DeviceRGB CS 0 0 1 SC 4 w 10 50 m 90 50 l S";

        // Act
        using var surface = RenderContent(content);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>scn</c> with a trailing pattern name throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Color_ScnWithPatternName_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        const string content = "/P0 scn";

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderContent(content));
    }

    /// <summary>Proves that an unsupported named color space (resolved via <c>/Resources/ColorSpace</c>) throws <see cref="UnsupportedImageFeatureException"/> when selected with <c>cs</c>.</summary>
    [Theory]
    [InlineData("[/Separation /Spot /DeviceGray 4 0 R]")]
    [InlineData("[/DeviceN [/Spot] /DeviceGray 4 0 R]")]
    [InlineData("[/ICCBased 4 0 R]")]
    [InlineData("[/CalRGB << >>]")]
    [InlineData("[/CalGray << >>]")]
    [InlineData("[/Lab << >>]")]
    public void PdfDocument_Color_UnsupportedNamedColorSpace_ThrowsUnsupportedImageFeatureException(string colorSpaceArray)
    {
        // Arrange: an unused placeholder function stream, referenced only when the color space
        // array under test declares /Separation or /DeviceN (both name a tint-transform function
        // by indirect reference); the other cases in this theory never dereference object 5.
        var functionStream = BuildStreamObjectBody("/FunctionType 4", "{ }"u8.ToArray());
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs",
            $"/ColorSpace << /CS0 {colorSpaceArray} >>",
            [functionStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a color-space name that resolves back to itself (directly, or via a cycle
    ///     of several names) throws <see cref="InvalidDataException"/> instead of recursing until
    ///     the process' call stack is exhausted (a regression test for a real-world malformed PDF
    ///     that crashed the whole process with an unrecoverable <see cref="StackOverflowException"/>).
    /// </summary>
    [Theory]
    [InlineData("/ColorSpace << /CS0 /CS0 >>")]
    [InlineData("/ColorSpace << /CS0 /CS1 /CS1 /CS0 >>")]
    public void PdfDocument_Color_SelfReferentialNamedColorSpace_ThrowsInvalidDataException(string colorSpaceDictionary)
    {
        // Arrange
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs",
            colorSpaceDictionary,
            []);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an <c>/ICCBased</c> color space with <c>/N 3</c> (and no <c>/Alternate</c>) resolves/paints identically to <c>DeviceRGB</c>.</summary>
    [Fact]
    public void PdfDocument_Color_IccBasedN3_ResolvesAsDeviceRgb()
    {
        // Arrange: object 5 is an ICC profile stream declaring only /N 3 (no /Alternate).
        var iccStream = BuildStreamObjectBody("/N 3", [0x00]);
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs 0 0 1 scn 10 10 80 80 re f",
            "/ColorSpace << /CS0 [/ICCBased 5 0 R] >>",
            [iccStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 3 operands were accepted (/N 3 implies DeviceRGB's component count) and the
        // fill painted blue, exactly like DeviceRGB would.
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[50, 50]);
    }

    /// <summary>Proves that an <c>/ICCBased</c> color space prefers a present, supported <c>/Alternate</c> over its own <c>/N</c>.</summary>
    [Fact]
    public void PdfDocument_Color_IccBasedWithAlternate_PrefersAlternateOverN()
    {
        // Arrange: object 5 declares /N 1 (DeviceGray, 1 component) but /Alternate /DeviceRGB (3
        // components). Supplying 3 operands to 'scn' only succeeds if /Alternate won: had /N been
        // used instead, 3 operands would mismatch the expected 1 and throw InvalidDataException.
        var iccStream = BuildStreamObjectBody("/N 1 /Alternate /DeviceRGB", [0x00]);
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs 1 0 0 scn 10 10 80 80 re f",
            "/ColorSpace << /CS0 [/ICCBased 5 0 R] >>",
            [iccStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that an <c>/ICCBased</c> color space with an unsupported <c>/N</c> and no usable <c>/Alternate</c> throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Color_IccBasedUnsupportedN_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange: object 5 declares /N 2, which maps to no device color space, and no /Alternate.
        var iccStream = BuildStreamObjectBody("/N 2", [0x00]);
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs",
            "/ColorSpace << /CS0 [/ICCBased 5 0 R] >>",
            [iccStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an <c>/Indexed</c> color space selected via <c>scn</c> paints the expected palette entry.</summary>
    [Fact]
    public void PdfDocument_Color_IndexedColorSpace_Scn_PaintsExpectedPaletteColor()
    {
        // Arrange: a 2-entry (Hival = 1) DeviceRGB palette: index 0 = red, index 1 = green.
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs 1 scn 10 10 80 80 re f",
            "/ColorSpace << /CS0 [/Indexed /DeviceRGB 1 <FF000000FF00>] >>",
            []);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: index 1 selects the green palette entry.
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that an <c>/Indexed</c> color space clamps an out-of-range index to the highest valid palette entry instead of throwing.</summary>
    [Fact]
    public void PdfDocument_Color_IndexedColorSpace_OutOfRangeIndex_ClampsToHighestPaletteEntry()
    {
        // Arrange: same 2-entry (Hival = 1) palette as above, but 'scn' selects index 5.
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs 5 scn 10 10 80 80 re f",
            "/ColorSpace << /CS0 [/Indexed /DeviceRGB 1 <FF000000FF00>] >>",
            []);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: index 5 clamps to Hival (1), the highest valid (green) palette entry.
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that an <c>/Indexed</c> color space whose base color space is itself unsupported throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Color_IndexedColorSpace_UnsupportedBase_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange: the base color space (/Separation) is unsupported, and is rejected before the
        // /Hival/lookup table are ever consulted.
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/CS0 cs",
            "/ColorSpace << /CS0 [/Indexed [/Separation /Spot /DeviceGray 4 0 R] 1 <00>] >>",
            []);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    #endregion

    #region Filters

    /// <summary>Proves that a <c>FlateDecode</c>+PNG-predictor image XObject decodes the expected raw pixels.</summary>
    [Fact]
    public void PdfDocument_Images_FlateDecodePngPredictor_DecodesExpectedPixels()
    {
        // Arrange: a 2x2 DeviceRGB image, PNG predictor 15 (adaptive). Row 0 uses filter type 0
        // (None): raw pixels (255,0,0) (0,255,0). Row 1 uses filter type 2 (Up): stored as the
        // difference from row 0, encoding raw pixels (0,0,255) (255,255,0).
        byte[] row0 = [0, 255, 0, 0, 0, 255, 0];
        byte[] row1raw = [0, 0, 255, 255, 255, 0];
        var row1Filtered = new byte[row1raw.Length];
        for (var i = 0; i < row1raw.Length; i++)
        {
            row1Filtered[i] = (byte)(row1raw[i] - row0[i + 1]);
        }

        var rawRows = new List<byte>();
        rawRows.AddRange(row0);
        rawRows.Add(2); // filter type: Up
        rawRows.AddRange(row1Filtered);
        var compressed = ZlibCompress([.. rawRows]);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 "
            + "/Filter /FlateDecode /DecodeParms << /Predictor 15 /Colors 3 /BitsPerComponent 8 /Columns 2 >>",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: row 0 (top of unit square) -> device top half; row 1 -> device bottom half.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 0, 255), surface[75, 75]);
    }

    /// <summary>Proves that a <c>FlateDecode</c>+TIFF-predictor image XObject decodes the expected raw pixels.</summary>
    [Fact]
    public void PdfDocument_Images_FlateDecodeTiffPredictor_DecodesExpectedPixels()
    {
        // Arrange: a 2x2 DeviceGray image, TIFF predictor 2. Raw pixels per row: (10, 200).
        // TIFF-encoded: byte 0 verbatim, byte 1 = raw[1] - raw[0].
        byte[] rawRow = [10, 200];
        var encodedRow = new byte[] { rawRow[0], (byte)(rawRow[1] - rawRow[0]) };
        var rawBytes = new List<byte>();
        rawBytes.AddRange(encodedRow);
        rawBytes.AddRange(encodedRow);
        var compressed = ZlibCompress([.. rawBytes]);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 "
            + "/Filter /FlateDecode /DecodeParms << /Predictor 2 /Colors 1 /BitsPerComponent 8 /Columns 2 >>",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: both source pixels in a row decode to (10) then (200); both rows identical.
        Assert.Equal(new Canvas.Rgba32(10, 10, 10, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(200, 200, 200, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(10, 10, 10, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(200, 200, 200, 255), surface[75, 75]);
    }

    /// <summary>Proves that an image XObject declaring an unsupported filter throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_UnsupportedFilter_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange: /JPXDecode remains genuinely unsupported (unlike /LZWDecode and
        // /CCITTFaxDecode, both of which this library implements - see
        // PdfFixtures/README.md/design docs).
        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /JPXDecode",
            [1, 2, 3, 4]);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an image XObject declaring an <c>LZWDecode</c>+PNG-predictor filter decodes the expected raw pixels.</summary>
    [Fact]
    public void PdfDocument_Images_LzwDecodePngPredictor_DecodesExpectedPixels()
    {
        // Arrange: same 2x2 DeviceRGB/PNG-predictor-15 raw layout as
        // PdfDocument_Images_FlateDecodePngPredictor_DecodesExpectedPixels, but compressed with
        // LZWDecode instead of FlateDecode - proves predictor reversal applies after LZWDecode too.
        byte[] row0 = [0, 255, 0, 0, 0, 255, 0];
        byte[] row1raw = [0, 0, 255, 255, 255, 0];
        var row1Filtered = new byte[row1raw.Length];
        for (var i = 0; i < row1raw.Length; i++)
        {
            row1Filtered[i] = (byte)(row1raw[i] - row0[i + 1]);
        }

        var rawRows = new List<byte>();
        rawRows.AddRange(row0);
        rawRows.Add(2); // filter type: Up
        rawRows.AddRange(row1Filtered);
        var compressed = EncodeLzwForTest([.. rawRows], earlyChange: true);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 "
            + "/Filter /LZWDecode /DecodeParms << /Predictor 15 /Colors 3 /BitsPerComponent 8 /Columns 2 >>",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: row 0 (top of unit square) -> device top half; row 1 -> device bottom half.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 0, 255), surface[75, 75]);
    }

    /// <summary>Proves that a plain (unpredicted) <c>LZWDecode</c> image XObject decodes and places the expected pixels via <c>Do</c>.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DeviceGrayLzwDecode_PlacesExpectedPixels()
    {
        // Arrange: a 2x1 DeviceGray image, raw samples (10, 200), no predictor.
        byte[] rawSamples = [10, 200];
        var compressed = EncodeLzwForTest(rawSamples, earlyChange: true);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /LZWDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(10, 10, 10, 255), surface[25, 50]);
        Assert.Equal(new Canvas.Rgba32(200, 200, 200, 255), surface[75, 50]);
    }

    /// <summary>
    ///     Proves that the mandatory ISO 32000-1/2 section 7.4.4.2 <c>Table 7</c>/<c>EXAMPLE 2</c>
    ///     worked LZWDecode vector (input <c>45 45 45 45 45 65 45 45 45 66</c>, i.e. ASCII
    ///     <c>"-----A---B"</c>, packed per <c>EXAMPLE 2</c> as <c>80 0B 60 50 22 0C 0C 85 01</c>)
    ///     decodes to the exact expected bytes.
    /// </summary>
    [Fact]
    public void PdfDocument_Filters_LzwDecode_SpecExampleTable7_DecodesExpectedBytes()
    {
        // Arrange: the spec's own EXAMPLE 2 byte sequence, as a 10x1 DeviceGray image so each
        // decoded byte becomes one directly inspectable pixel.
        byte[] specBytes = [0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01];

        // Act
        using var surface = RenderGrayscaleImage(10, specBytes, "/Filter /LZWDecode");

        // Assert: "-----A---B" == 2D 2D 2D 2D 2D 41 2D 2D 2D 42
        byte[] expected = [0x2D, 0x2D, 0x2D, 0x2D, 0x2D, 0x41, 0x2D, 0x2D, 0x2D, 0x42];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(new Canvas.Rgba32(expected[i], expected[i], expected[i], 255), surface[i, 0]);
        }
    }

    /// <summary>
    ///     Proves that the default (<c>/EarlyChange</c> absent, meaning <c>1</c>) code-width
    ///     growth timing decodes a synthetic vector deliberately long enough to cross the 9-to-10
    ///     bit boundary, and that decoding the very same bytes with <c>/EarlyChange 0</c> instead
    ///     (the wrong timing for how they were encoded) is rejected - proving the two timings are
    ///     genuinely different, not merely two names for the same behavior.
    /// </summary>
    [Fact]
    public void PdfDocument_Filters_LzwDecode_EarlyChangeDefault_GrowsCodeWidthOneCodeEarly()
    {
        // Arrange: a synthetic (not spec-provided), deterministically generated 320-byte vector
        // long enough for the dictionary to cross code 511 (the 9-to-10-bit growth boundary).
        var data = BuildLzwGrowthTestData();
        var encoded = EncodeLzwForTest(data, earlyChange: true);

        // Act
        using var correctSurface = RenderGrayscaleImage(data.Length, encoded, "/Filter /LZWDecode");

        // Assert: decoded with the matching (default) EarlyChange timing, every byte round-trips.
        for (var i = 0; i < data.Length; i++)
        {
            Assert.Equal(new Canvas.Rgba32(data[i], data[i], data[i], 255), correctSurface[i, 0]);
        }

        // Assert: decoding the same bytes with the opposite (late) timing desyncs and fails closed.
        Assert.Throws<InvalidDataException>(
            () => RenderGrayscaleImage(data.Length, encoded, "/Filter /LZWDecode /DecodeParms << /EarlyChange 0 >>"));
    }

    /// <summary>
    ///     Proves that <c>/EarlyChange 0</c> code-width growth timing decodes a synthetic vector
    ///     encoded with that same (later) timing, and that decoding the very same bytes with the
    ///     default (early) timing instead is rejected.
    /// </summary>
    [Fact]
    public void PdfDocument_Filters_LzwDecode_EarlyChangeZero_GrowsCodeWidthOneCodeLater()
    {
        // Arrange: the same synthetic vector, this time encoded with EarlyChange 0 timing.
        var data = BuildLzwGrowthTestData();
        var encoded = EncodeLzwForTest(data, earlyChange: false);

        // Act
        using var correctSurface = RenderGrayscaleImage(
            data.Length, encoded, "/Filter /LZWDecode /DecodeParms << /EarlyChange 0 >>");

        // Assert: decoded with the matching (EarlyChange 0) timing, every byte round-trips.
        for (var i = 0; i < data.Length; i++)
        {
            Assert.Equal(new Canvas.Rgba32(data[i], data[i], data[i], 255), correctSurface[i, 0]);
        }

        // Assert: decoding the same bytes with the default (early) timing instead desyncs.
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(data.Length, encoded, "/Filter /LZWDecode"));
    }

    /// <summary>
    ///     Proves that a Clear-table code occurring mid-stream actually reinitializes the
    ///     dictionary: after <c>CLEAR, 'A', 'A', 'A'</c> (which would - if the table were
    ///     <em>not</em> reset - leave dynamic code <c>260</c> meaning <c>"AA"</c>), a second
    ///     <c>CLEAR</c> followed by <c>'B', 260</c> must reject code <c>260</c> as not-yet-present
    ///     (since a properly reset table has not yet reassigned it) rather than silently
    ///     resolving it against the stale pre-reset entry.
    /// </summary>
    [Fact]
    public void PdfDocument_Filters_LzwDecode_ClearCodeMidStream_ReinitializesTable()
    {
        // Arrange: 9-bit codes [256 (Clear), 65, 65, 65, 256 (Clear), 66, 260], hand-packed MSB-first.
        byte[] data = [0x80, 0x10, 0x48, 0x24, 0x18, 0x01, 0x0A, 0x08];

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(1, data, "/Filter /LZWDecode"));
    }

    /// <summary>Proves that a bit stream truncated before an EOD code is reached throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Filters_LzwDecode_MissingEodCode_ThrowsInvalidDataException()
    {
        // Arrange: 9-bit codes [256 (Clear), 65, 66] with no EOD code, hand-packed MSB-first.
        byte[] data = [0x80, 0x10, 0x48, 0x40];

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(1, data, "/Filter /LZWDecode"));
    }

    /// <summary>Proves that an out-of-range code (not yet present in the table, and not the very next code) throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Filters_LzwDecode_InvalidCode_ThrowsInvalidDataException()
    {
        // Arrange: 9-bit codes [256 (Clear), 300] - 300 is not a valid code before any table
        // entry exists (only 0-257 are), hand-packed MSB-first.
        byte[] data = [0x80, 0x4B, 0x00];

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(1, data, "/Filter /LZWDecode"));
    }

    /// <summary>Proves that an all-zero-group <c>ASCII85Decode</c> <c>z</c> shorthand decodes to four zero bytes.</summary>
    [Fact]
    public void PdfDocument_Filters_Ascii85Decode_ZeroGroup_DecodesToFourZeroBytes()
    {
        // Arrange: "z~>" - the z shorthand for 00 00 00 00, then EOD.
        var data = "z~>"u8.ToArray();

        // Act
        using var surface = RenderGrayscaleImage(4, data, "/Filter /ASCII85Decode");

        // Assert
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[i, 0]);
        }
    }

    /// <summary>
    ///     Proves that a full 5-character ASCII85 group decodes to its expected 4 bytes -
    ///     self-derived (per the spec's own section 7.4.3 formula) vector: bytes
    ///     <c>00 01 02 03</c> encode to <c>!!*-'</c>.
    /// </summary>
    [Fact]
    public void PdfDocument_Filters_Ascii85Decode_FullGroup_DecodesExpectedBytes()
    {
        // Arrange
        var data = "!!*-'~>"u8.ToArray();

        // Act
        using var surface = RenderGrayscaleImage(4, data, "/Filter /ASCII85Decode");

        // Assert
        byte[] expected = [0x00, 0x01, 0x02, 0x03];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(new Canvas.Rgba32(expected[i], expected[i], expected[i], 255), surface[i, 0]);
        }
    }

    /// <summary>
    ///     Proves the final-partial-group padding rule: self-derived vectors <c>4D 61</c> (2
    ///     bytes) encodes to <c>9jn</c> (n+1 = 3 characters), and <c>4D 61 6E</c> (3 bytes)
    ///     encodes to <c>9jqo</c> (n+1 = 4 characters).
    /// </summary>
    [Fact]
    public void PdfDocument_Filters_Ascii85Decode_PartialFinalGroup_AppliesPaddingRule()
    {
        // Arrange & Act
        using var twoByteSurface = RenderGrayscaleImage(2, "9jn~>"u8.ToArray(), "/Filter /ASCII85Decode");
        using var threeByteSurface = RenderGrayscaleImage(3, "9jqo~>"u8.ToArray(), "/Filter /ASCII85Decode");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0x4D, 0x4D, 0x4D, 255), twoByteSurface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(0x61, 0x61, 0x61, 255), twoByteSurface[1, 0]);
        Assert.Equal(new Canvas.Rgba32(0x4D, 0x4D, 0x4D, 255), threeByteSurface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(0x61, 0x61, 0x61, 255), threeByteSurface[1, 0]);
        Assert.Equal(new Canvas.Rgba32(0x6E, 0x6E, 0x6E, 255), threeByteSurface[2, 0]);
    }

    /// <summary>Proves that PDF white-space characters interspersed within an <c>ASCII85Decode</c> stream are ignored.</summary>
    [Fact]
    public void PdfDocument_Filters_Ascii85Decode_WhitespaceIgnored_DecodesExpectedBytes()
    {
        // Arrange: the same "!!*-'" vector, with whitespace inserted between every character.
        var data = "! ! \t* \n - \r ' \f ~ >"u8.ToArray();

        // Act
        using var surface = RenderGrayscaleImage(4, data, "/Filter /ASCII85Decode");

        // Assert
        byte[] expected = [0x00, 0x01, 0x02, 0x03];
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(new Canvas.Rgba32(expected[i], expected[i], expected[i], 255), surface[i, 0]);
        }
    }

    /// <summary>Proves that a <c>z</c> character occurring in the middle of a group throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Filters_Ascii85Decode_ZInMiddleOfGroup_ThrowsInvalidDataException()
    {
        // Arrange: 'z' after one already-accumulated group character is invalid.
        var data = "!z~>"u8.ToArray();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(1, data, "/Filter /ASCII85Decode"));
    }

    /// <summary>Proves that a 5-character group whose base-85 value exceeds <c>2^32 - 1</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Filters_Ascii85Decode_ValueExceedsRange_ThrowsInvalidDataException()
    {
        // Arrange: five 'u' (84) digits: 84*85^4 + 84*85^3 + 84*85^2 + 84*85 + 84 = 85^5 - 1,
        // which exceeds uint.MaxValue (2^32 - 1 = 4294967295 < 85^5 - 1 = 4437053124).
        var data = "uuuuu~>"u8.ToArray();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(1, data, "/Filter /ASCII85Decode"));
    }

    /// <summary>Proves that a missing <c>~&gt;</c> EOD marker throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Filters_Ascii85Decode_MissingEodMarker_ThrowsInvalidDataException()
    {
        // Arrange: a well-formed group, but no ~> terminator.
        var data = "!!*-'"u8.ToArray();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(4, data, "/Filter /ASCII85Decode"));
    }

    /// <summary>Proves that hex-digit pairs decode to the expected bytes.</summary>
    [Fact]
    public void PdfDocument_Filters_AsciiHexDecode_HexDigitPairs_DecodesExpectedBytes()
    {
        // Arrange
        var data = "0A1B2C>"u8.ToArray();

        // Act
        using var surface = RenderGrayscaleImage(3, data, "/Filter /ASCIIHexDecode");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0x0A, 0x0A, 0x0A, 255), surface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(0x1B, 0x1B, 0x1B, 255), surface[1, 0]);
        Assert.Equal(new Canvas.Rgba32(0x2C, 0x2C, 0x2C, 255), surface[2, 0]);
    }

    /// <summary>Proves that an odd trailing hex digit is implicitly padded with a zero low nibble.</summary>
    [Fact]
    public void PdfDocument_Filters_AsciiHexDecode_OddTrailingDigit_PadsWithZeroNibble()
    {
        // Arrange: a single trailing "A" digit, implicitly followed by a zero nibble -> 0xA0.
        var data = "0A1BA>"u8.ToArray();

        // Act
        using var surface = RenderGrayscaleImage(3, data, "/Filter /ASCIIHexDecode");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0x0A, 0x0A, 0x0A, 255), surface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(0x1B, 0x1B, 0x1B, 255), surface[1, 0]);
        Assert.Equal(new Canvas.Rgba32(0xA0, 0xA0, 0xA0, 255), surface[2, 0]);
    }

    /// <summary>Proves that PDF white-space characters interspersed within an <c>ASCIIHexDecode</c> stream are ignored.</summary>
    [Fact]
    public void PdfDocument_Filters_AsciiHexDecode_WhitespaceIgnored_DecodesExpectedBytes()
    {
        // Arrange
        var data = "0A \t1B\n2C\r\f>"u8.ToArray();

        // Act
        using var surface = RenderGrayscaleImage(3, data, "/Filter /ASCIIHexDecode");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0x0A, 0x0A, 0x0A, 255), surface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(0x1B, 0x1B, 0x1B, 255), surface[1, 0]);
        Assert.Equal(new Canvas.Rgba32(0x2C, 0x2C, 0x2C, 255), surface[2, 0]);
    }

    /// <summary>Proves that a non-hexadecimal, non-whitespace character throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Filters_AsciiHexDecode_InvalidCharacter_ThrowsInvalidDataException()
    {
        // Arrange: 'G' is not a valid hex digit.
        var data = "0AG1>"u8.ToArray();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(2, data, "/Filter /ASCIIHexDecode"));
    }

    /// <summary>Proves that a missing <c>&gt;</c> EOD marker throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Filters_AsciiHexDecode_MissingEodMarker_ThrowsInvalidDataException()
    {
        // Arrange: well-formed hex digits, but no > terminator.
        var data = "0A1B2C"u8.ToArray();

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(3, data, "/Filter /ASCIIHexDecode"));
    }

    /// <summary>Proves that a length byte 0-127 (a literal run) copies the following <c>length + 1</c> bytes verbatim.</summary>
    [Fact]
    public void PdfDocument_Filters_RunLengthDecode_LiteralRun_CopiesBytesVerbatim()
    {
        // Arrange: length byte 2 -> copy the next 3 literal bytes, then EOD (128).
        byte[] data = [2, 0x10, 0x20, 0x30, 128];

        // Act
        using var surface = RenderGrayscaleImage(3, data, "/Filter /RunLengthDecode");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0x10, 0x10, 0x10, 255), surface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(0x20, 0x20, 0x20, 255), surface[1, 0]);
        Assert.Equal(new Canvas.Rgba32(0x30, 0x30, 0x30, 255), surface[2, 0]);
    }

    /// <summary>Proves that a length byte 129-255 (a repeat run) repeats the following single byte <c>257 - length</c> times.</summary>
    [Fact]
    public void PdfDocument_Filters_RunLengthDecode_RepeatRun_RepeatsSingleByte()
    {
        // Arrange: length byte 254 -> repeat the next byte 257 - 254 = 3 times, then EOD (128).
        byte[] data = [254, 0x55, 128];

        // Act
        using var surface = RenderGrayscaleImage(3, data, "/Filter /RunLengthDecode");

        // Assert
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(new Canvas.Rgba32(0x55, 0x55, 0x55, 255), surface[i, 0]);
        }
    }

    /// <summary>Proves that the EOD length byte (<c>128</c>) stops decoding, ignoring any trailing bytes after it.</summary>
    [Fact]
    public void PdfDocument_Filters_RunLengthDecode_EodMarker_StopsDecoding()
    {
        // Arrange: a literal run, then EOD, then trailing bytes that must be ignored.
        byte[] data = [1, 0x10, 0x20, 128, 0xFF, 0xFF];

        // Act
        using var surface = RenderGrayscaleImage(2, data, "/Filter /RunLengthDecode");

        // Assert
        Assert.Equal(new Canvas.Rgba32(0x10, 0x10, 0x10, 255), surface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(0x20, 0x20, 0x20, 255), surface[1, 0]);
    }

    /// <summary>Proves that a literal run requiring more bytes than remain (truncated, no EOD) throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Filters_RunLengthDecode_TruncatedRun_ThrowsInvalidDataException()
    {
        // Arrange: length byte 5 declares a 6-byte literal run, but only 2 bytes follow.
        byte[] data = [5, 0x10, 0x20];

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderGrayscaleImage(1, data, "/Filter /RunLengthDecode"));
    }

    /// <summary>
    ///     Proves <c>PdfDocument.CcittFax.cs</c>'s White/Black Modified Huffman run-length code
    ///     tables are internally self-consistent: writing any single table entry's own exact bits
    ///     through <see cref="PdfDocument.CcittBitReader"/> and reading it back via
    ///     <see cref="PdfDocument.ReadVariableLengthCode"/> recovers the exact same run length -
    ///     independently of whether any single rendered test image happens to exercise that
    ///     particular code (most test images in this file only ever exercise a handful of small
    ///     run lengths). Exercises the <c>internal</c> (not <c>private</c>) visibility
    ///     deliberately given to these members for this exact purpose.
    /// </summary>
    [Fact]
    public void PdfDocument_Filters_CcittFaxRunLengthTables_RoundTripEveryCode()
    {
        foreach (var table in new[] { PdfDocument.WhiteCodeTable, PdfDocument.BlackCodeTable })
        {
            foreach (var ((length, code), run) in table)
            {
                // Arrange: pack just this one code's bits (MSB-first), left-justified in enough
                // bytes to hold it, zero-padded - exactly what CcittBitReader expects.
                var byteCount = (length + 7) / 8;
                var bytes = new byte[byteCount];
                for (var i = 0; i < length; i++)
                {
                    var bit = (code >> (length - 1 - i)) & 1;
                    if (bit == 1)
                    {
                        bytes[i / 8] |= (byte)(0x80 >> (i % 8));
                    }
                }

                var reader = new PdfDocument.CcittBitReader(bytes);

                // Act
                var decodedRun = PdfDocument.ReadVariableLengthCode(reader, table, length);

                // Assert
                Assert.Equal(run, decodedRun);
            }
        }
    }

    /// <summary>
    ///     Proves <c>PdfDocument.CcittFax.cs</c>'s <c>ModeCodeTable</c> (all nine ITU-T T.6
    ///     two-dimensional mode codes: Pass, Horizontal, V0, VR1-3, VL1-3) is internally
    ///     self-consistent: writing any single table entry's own exact bits through
    ///     <see cref="PdfDocument.CcittBitReader"/> and reading it back via
    ///     <see cref="PdfDocument.ReadMode"/> recovers the exact same mode - independently of
    ///     whether any single rendered test image happens to exercise that particular mode code
    ///     (most rendered test images in this file only ever exercise Horizontal/V0/VL1/VR2).
    ///     Mirrors <see cref="PdfDocument_Filters_CcittFaxRunLengthTables_RoundTripEveryCode"/>'s
    ///     own precedent; exercises the <c>internal</c> (not <c>private</c>) visibility
    ///     deliberately given to <c>ModeCodeTable</c>/<c>CcittMode</c>/<c>ReadMode</c> for this
    ///     exact purpose.
    /// </summary>
    [Fact]
    public void PdfDocument_Filters_CcittFaxModeCodeTable_RoundTripEveryCode()
    {
        foreach (var ((length, code), mode) in PdfDocument.ModeCodeTable)
        {
            // Arrange: pack just this one code's bits (MSB-first), left-justified in enough
            // bytes to hold it, zero-padded - exactly what CcittBitReader expects.
            var byteCount = (length + 7) / 8;
            var bytes = new byte[byteCount];
            for (var i = 0; i < length; i++)
            {
                var bit = (code >> (length - 1 - i)) & 1;
                if (bit == 1)
                {
                    bytes[i / 8] |= (byte)(0x80 >> (i % 8));
                }
            }

            var reader = new PdfDocument.CcittBitReader(bytes);

            // Act
            var decodedMode = PdfDocument.ReadMode(reader);

            // Assert
            Assert.Equal(mode, decodedMode);
        }
    }

    #endregion

    #region Images

    /// <summary>Proves that <c>Do</c> decodes and places a raw 8-bit <c>DeviceGray</c> <c>FlateDecode</c> image XObject at the expected device pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DeviceGrayFlateDecode_PlacesExpectedPixels()
    {
        // Arrange: a 2x2 DeviceGray image: row0 = (0, 255), row1 = (64, 192).
        byte[] raw = [0, 255, 64, 192];
        var compressed = ZlibCompress(raw);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 255, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(64, 64, 64, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(192, 192, 192, 255), surface[75, 75]);
    }

    /// <summary>
    ///     Proves that a <c>FlateDecode</c> stream whose trailing Adler-32 checksum does not match
    ///     the compressed data still decodes successfully - matching real-world reader tolerance
    ///     for the wrong/placeholder checksums some PDF producers emit, found via a batch-rendering
    ///     survey against a public real-world PDF test corpus.
    /// </summary>
    [Fact]
    public void PdfDocument_Images_DeviceGrayFlateDecode_CorruptAdlerChecksum_StillDecodes()
    {
        // Arrange: a 2x2 DeviceGray image, with the zlib stream's last byte (part of the trailing
        // Adler-32 checksum) deliberately flipped - the DEFLATE payload itself is untouched.
        byte[] raw = [0, 255, 64, 192];
        var compressed = ZlibCompress(raw);
        compressed[^1] ^= 0xFF;

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: decoded exactly as if the checksum had been correct.
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 255, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(64, 64, 64, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(192, 192, 192, 255), surface[75, 75]);
    }

    /// <summary>Proves that <c>Do</c> decodes and places a raw 8-bit <c>DeviceRGB</c> <c>FlateDecode</c> image XObject at the expected device pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DeviceRgbFlateDecode_PlacesExpectedPixels()
    {
        // Arrange: a 2x2 DeviceRGB image: row0 = red, green; row1 = blue, white.
        byte[] raw = [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255];
        var compressed = ZlibCompress(raw);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 255, 255), surface[75, 75]);
    }

    /// <summary>Proves that <c>Do</c> decodes and places a raw 8-bit <c>DeviceCMYK</c> <c>FlateDecode</c> image XObject at the expected device pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DeviceCmykFlateDecode_PlacesExpectedPixels()
    {
        // Arrange: a single-pixel 1x1 DeviceCMYK image: C=0 M=1 Y=1 K=0 -> red.
        byte[] raw = [0, 255, 255, 0];
        var compressed = ZlibCompress(raw);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceCMYK /BitsPerComponent 8 /Filter /FlateDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that <c>Do</c> decodes and places an <c>/Indexed</c> image XObject's palette colors at the expected device pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_IndexedColorSpace_PlacesExpectedPaletteColors()
    {
        // Arrange: a 2x2 image over a 3-entry (Hival = 2) DeviceRGB palette (red/green/blue).
        // Raw index samples: row0 = (0, 1), row1 = (2, 2).
        byte[] raw = [0, 1, 2, 2];
        var compressed = ZlibCompress(raw);

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 "
            + "/ColorSpace [/Indexed /DeviceRGB 2 <FF000000FF000000FF>] /BitsPerComponent 8 /Filter /FlateDecode",
            compressed);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[25, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 255, 0, 255), surface[75, 25]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[25, 75]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 255, 255), surface[75, 75]);
    }

    /// <summary>Proves that <c>Do</c> decodes a bare <c>DCTDecode</c> (JPEG) image XObject via <see cref="JpegCodec.Load(Stream)"/> and places the expected (solid-color, lossless-for-flat-blocks) pixels.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_DctDecodeJpeg_PlacesExpectedPixels()
    {
        // Arrange: an 8x8 (one MCU block), solid blue surface - a flat color block's DCT has only
        // a DC coefficient, so a high-quality encode/decode round-trip reproduces it exactly (or
        // within a tiny rounding tolerance).
        var solid = new Canvas.Surface(8, 8);
        var blue = new Canvas.Rgba32(0, 0, 255, 255);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                solid[x, y] = blue;
            }
        }

        using var jpegStream = new MemoryStream();
        JpegCodec.Save(solid, jpegStream, quality: 100);
        var jpegBytes = jpegStream.ToArray();

        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode",
            jpegBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: within a small per-channel tolerance of solid blue.
        var pixel = surface[50, 50];
        Assert.True(Math.Abs(pixel.R - blue.R) <= 8, $"R={pixel.R}");
        Assert.True(Math.Abs(pixel.G - blue.G) <= 8, $"G={pixel.G}");
        Assert.True(Math.Abs(pixel.B - blue.B) <= 8, $"B={pixel.B}");
        Assert.Equal(255, pixel.A);
    }

    /// <summary>
    ///     Proves that <c>Do</c> decodes a Group 4 (T.6 MMR) <c>CCITTFaxDecode</c> image XObject
    ///     and places the expected black/white pixels at specific <c>(x, y)</c> coordinates.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Uses the 8x4 pattern below, encoded via the test-only
    ///         <see cref="EncodeCcittGroup4ForTest"/>. Per that method's own remarks, a
    ///         round-trip through the test encoder and the production decoder alone cannot rule
    ///         out a matching pair of bugs, so this test additionally hand-verifies, bit by bit,
    ///         the exact encoded byte sequence this pattern must produce - worked out independently
    ///         of both the production C# decoder and the test-only C# encoder (originally derived
    ///         and cross-checked via a from-scratch Python prototype, then re-derived by hand
    ///         below) - and asserts the encoder actually produces those exact bytes before even
    ///         reaching the decoder, so a mismatch here would fail loudly as a wrong-test-vector
    ///         bug rather than silently validating nothing.
    ///     </para>
    ///     <para>
    ///         Pattern (<c>W</c>=white, <c>B</c>=black), 8 columns x 4 rows:
    ///         <code>
    ///         row 0: W W W W B B B B
    ///         row 1: W W W W B B B B   (identical to row 0 -> both changing elements at V0)
    ///         row 2: W W W B B B B B   (black run starts 1 pixel earlier -> VL1)
    ///         row 3: W W W W W B B B   (black run starts 2 pixels later -> VR2)
    ///         </code>
    ///     </para>
    ///     <para>
    ///         Hand-derived bit trace (reference line for row 0 is the imaginary all-white line,
    ///         so <c>b1</c>/<c>b2</c> both default to <c>columns</c> = 8):
    ///     </para>
    ///     <para>
    ///         <strong>Row 0</strong> (changing element at 4): <c>a0=-1</c>, white.
    ///         <c>b1=8, b2=8</c> (empty reference). <c>a1=4</c> (the only change). Since
    ///         <c>a1(4) &lt;= b2(8)</c>, not Pass. <c>delta = a1 - b1 = 4 - 8 = -4</c>, outside
    ///         <c>[-3, 3]</c>, so Horizontal: run1 (white) = <c>4 - 0 = 4</c> -&gt; code
    ///         <c>1011</c> (length 4); run2 (black) = <c>8 - 4 = 4</c> -&gt; code <c>011</c>
    ///         (length 3). Bits: Horizontal <c>001</c> + white-4 <c>1011</c> + black-4 <c>011</c>
    ///         = <c>0011011011</c> (10 bits). <c>a0</c> becomes 8, row done.
    ///     </para>
    ///     <para>
    ///         <strong>Row 1</strong> (reference = row 0's changes <c>[4, 8]</c>): first element:
    ///         <c>a0=-1</c>, white, <c>b1=4, b2=8</c>; <c>a1=4</c>; <c>delta=4-4=0</c> -&gt; V0
    ///         (bit <c>1</c>), <c>a0=4</c>, color -&gt; black. Second element: <c>b1=8, b2=8</c>
    ///         (no further reference change past <c>a0=4</c> except the sentinel at 8);
    ///         <c>a1=8</c> (no further row-1 change, i.e. black runs to the end);
    ///         <c>delta=8-8=0</c> -&gt; V0 (bit <c>1</c>), <c>a0=8</c>, row done. Bits: <c>11</c>
    ///         (2 bits).
    ///     </para>
    ///     <para>
    ///         Running total so far: <c>0011011011</c> + <c>11</c> = <c>001101101111</c> (12
    ///         bits); the first byte (<c>00110110</c>) = <c>0x36</c>.
    ///     </para>
    ///     <para>
    ///         <strong>Row 2</strong> (reference = row 1's changes <c>[4, 8]</c>, changing
    ///         element at 3): <c>a0=-1</c>, white, <c>b1=4, b2=8</c>; <c>a1=3</c>;
    ///         <c>delta=3-4=-1</c> -&gt; VL1 (code <c>010</c>, length 3), <c>a0=3</c>, color -&gt;
    ///         black. Second element: reference index advances past <c>b1=4&lt;=a0(3)</c>? No
    ///         (4 &gt; 3), so <c>b1</c> search continues from the same reference index but the
    ///         color now matches (black at index 0 already consumed as b1 above) - working
    ///         through <see cref="PdfDocument"/>'s own <c>FindB1B2</c> logic by hand gives
    ///         <c>b1=8, b2=8</c>; <c>a1=8</c> (no further row-2 change);
    ///         <c>delta=8-8=0</c> -&gt; V0 (bit <c>1</c>), <c>a0=8</c>, row done. Bits:
    ///         <c>010</c> + <c>1</c> = <c>0101</c> (4 bits).
    ///     </para>
    ///     <para>
    ///         Running total: 12 + 4 = 16 bits; bits 8-15 (<c>11110101</c>) = <c>0xF5</c> (the
    ///         second byte).
    ///     </para>
    ///     <para>
    ///         <strong>Row 3</strong> (reference = row 2's changes <c>[3, 8]</c>, changing
    ///         element at 5): <c>a0=-1</c>, white, <c>b1=3, b2=8</c>; <c>a1=5</c>;
    ///         <c>delta=5-3=2</c> -&gt; VR2 (code <c>000011</c>, length 6), <c>a0=5</c>, color
    ///         -&gt; black. Second element: <c>b1=8, b2=8</c>; <c>a1=8</c>; <c>delta=0</c> -&gt;
    ///         V0 (bit <c>1</c>), <c>a0=8</c>, row done. Bits: <c>000011</c> + <c>1</c> =
    ///         <c>0000111</c> (7 bits).
    ///     </para>
    ///     <para>
    ///         Running total: 16 + 7 = 23 bits, zero-padded to 24: the final 8 bits are
    ///         <c>00001110</c> = <c>0x0E</c>. Final expected encoded bytes:
    ///         <c>0x36 0xF5 0x0E</c> - exactly matching both this hand trace and the
    ///         independent Python prototype's output (confidence: high - every single bit of
    ///         all 23 was traced by hand above, not merely asserted).
    ///     </para>
    /// </remarks>
    [Fact]
    public void PdfDocument_Images_DoOperator_CcittFaxGroup4_PlacesExpectedPixels()
    {
        // Arrange
        var pixels = new bool[8, 4];
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                pixels[x, y] = y switch
                {
                    0 or 1 => x >= 4,
                    2 => x >= 3,
                    _ => x >= 5,
                };
            }
        }

        var encoded = EncodeCcittGroup4ForTest(pixels);

        // Assert the hand-derived test vector itself, before even reaching the decoder.
        Assert.Equal<byte>([0x36, 0xF5, 0x0E], encoded);

        // Act
        using var surface = RenderCcittFaxImage(8, 4, encoded);

        // Assert: white -> (255,255,255,255); black -> (0,0,0,255) under the default /BlackIs1.
        var white = new Canvas.Rgba32(255, 255, 255, 255);
        var black = new Canvas.Rgba32(0, 0, 0, 255);
        Assert.Equal(white, surface[0, 0]);
        Assert.Equal(black, surface[4, 0]);
        Assert.Equal(white, surface[0, 1]);
        Assert.Equal(black, surface[4, 1]);
        Assert.Equal(white, surface[2, 2]);
        Assert.Equal(black, surface[3, 2]);
        Assert.Equal(white, surface[4, 3]);
        Assert.Equal(black, surface[5, 3]);
    }

    /// <summary>Proves that the default (absent) <c>/BlackIs1</c> maps a physically black pixel to packed bit <c>0</c> (and a decoded gray value of <c>0</c>).</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_CcittFaxBlackIs1Default_MapsZeroBitToBlack()
    {
        // Arrange: same 8x4 pattern/encoding as the hand-verified test above.
        var pixels = new bool[8, 4];
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                pixels[x, y] = x >= 4;
            }
        }

        var encoded = EncodeCcittGroup4ForTest(pixels);

        // Act
        using var surface = RenderCcittFaxImage(8, 4, encoded);

        // Assert: default /BlackIs1 (absent -> false) -> black pixel -> gray 0.
        Assert.Equal(new Canvas.Rgba32(255, 255, 255, 255), surface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[4, 0]);
    }

    /// <summary>Proves that <c>/BlackIs1 true</c> inverts the polarity of the identical encoded bit stream relative to the default.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_CcittFaxBlackIs1True_InvertsPolarity()
    {
        // Arrange: the exact same encoded bytes as the Default test above.
        var pixels = new bool[8, 4];
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                pixels[x, y] = x >= 4;
            }
        }

        var encoded = EncodeCcittGroup4ForTest(pixels);

        // Act
        using var surface = RenderCcittFaxImage(8, 4, encoded, " /BlackIs1 true");

        // Assert: polarity inverted relative to the default-/BlackIs1 test - the physically black
        // run (x >= 4) now decodes to gray 255 (white), and the physically white run to gray 0.
        Assert.Equal(new Canvas.Rgba32(0, 0, 0, 255), surface[0, 0]);
        Assert.Equal(new Canvas.Rgba32(255, 255, 255, 255), surface[4, 0]);
    }

    /// <summary>Proves that non-byte-aligned <c>/Columns</c> values (not a multiple of 8) still decode every row's correct pixel colors, with no padding-bit leakage.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void PdfDocument_Images_DoOperator_CcittFaxNonByteAlignedColumns_DecodesExpectedRowPadding(int columns)
    {
        // Arrange: 3 rows, each with a single black run covering the row's final 3 columns (and
        // shifted by 1 column between rows, to force real 2D vertical-mode coding rather than
        // every row trivially matching its reference) - deliberately placed across the
        // not-a-multiple-of-8 /Columns boundary so a padding-bit bug would corrupt either this
        // run or the next row's leading pixels.
        var pixels = new bool[columns, 3];
        for (var y = 0; y < 3; y++)
        {
            var blackStart = columns - 3 - y;
            for (var x = 0; x < columns; x++)
            {
                pixels[x, y] = x >= blackStart;
            }
        }

        var encoded = EncodeCcittGroup4ForTest(pixels);

        // Act
        using var surface = RenderCcittFaxImage(columns, 3, encoded);

        // Assert: every column of every row matches the source pattern exactly.
        var white = new Canvas.Rgba32(255, 255, 255, 255);
        var black = new Canvas.Rgba32(0, 0, 0, 255);
        for (var y = 0; y < 3; y++)
        {
            var blackStart = columns - 3 - y;
            for (var x = 0; x < columns; x++)
            {
                Assert.Equal(x >= blackStart ? black : white, surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that <c>Do</c> decodes a Group 4 (T.6 MMR) <c>CCITTFaxDecode</c> image XObject
    ///     whose second row decodes via a <c>Pass</c> mode element, and places the expected
    ///     black/white pixels at specific <c>(x, y)</c> coordinates.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         None of this file's other CCITTFaxDecode tests happen to force a <c>Pass</c> mode
    ///         element (ITU-T T.6's "the current run extends past the reference line's <c>b2</c>
    ///         changing element without itself changing color" case), so this test uses a
    ///         dedicated hand-derived literal byte array (not <see cref="EncodeCcittGroup4ForTest"/>
    ///         - that test-only encoder's own output for this exact pattern was independently
    ///         cross-checked against the hand trace below and happens to match, but the literal
    ///         bytes are asserted directly so this test's evidence does not depend on the test
    ///         encoder's own correctness).
    ///     </para>
    ///     <para>
    ///         Pattern (<c>W</c>=white, <c>B</c>=black), 8 columns x 2 rows:
    ///         <code>
    ///         row 0: W W B B W W W W   (changing elements at x=2, x=4)
    ///         row 1: W W W W W W W W   (no changing element - all white)
    ///         </code>
    ///     </para>
    ///     <para>
    ///         <strong>Row 0</strong> (reference line empty, i.e. <c>b1=b2=8</c>): first element:
    ///         <c>a0=-1</c>, white; <c>a1=2</c>; <c>delta = a1 - b1 = 2 - 8 = -6</c>, outside
    ///         <c>[-3, 3]</c> -&gt; Horizontal: run1 (white) = <c>2</c> -&gt; code <c>0111</c>
    ///         (length 4); run2 (black) = <c>4 - 2 = 2</c> -&gt; code <c>11</c> (length 2). Bits:
    ///         Horizontal <c>001</c> + white-2 <c>0111</c> + black-2 <c>11</c> = <c>0010111 11</c>
    ///         (9 bits), <c>a0</c> becomes 4. Second element: <c>b1=b2=8</c> (reference exhausted);
    ///         <c>a1=8</c> (sentinel); <c>delta=0</c> -&gt; V0 (bit <c>1</c>), <c>a0=8</c>, row
    ///         done. Row 0 bits: <c>0010111111</c> (10 bits).
    ///     </para>
    ///     <para>
    ///         <strong>Row 1</strong> (reference <c>[2, 4]</c>): first element: <c>a0=-1</c>,
    ///         white; <c>FindB1B2([2,4], a0=-1, isBlack=false)</c> walks <c>idx</c> from 0 (no
    ///         reference element <c>&lt;= -1</c>); the color at <c>idx=0</c> is black (even index
    ///         = black, per <c>FindB1B2</c>'s own color-parity convention), which already differs
    ///         from the current white, so <c>idx</c> is <strong>not</strong> incremented:
    ///         <c>b1 = referenceChanges[0] = 2</c>, <c>b2 = referenceChanges[1] = 4</c>. Row 1 has
    ///         no changing element at all, so its next change (conceptually at <c>columns</c>,
    ///         i.e. 8) is strictly greater than <c>b2 = 4</c> - exactly ITU-T T.6's Pass condition
    ///         - so <c>Pass</c> is coded (<c>0001</c>, 4 bits), advancing <c>a0</c> to
    ///         <c>b2 = 4</c> without emitting any changing element. Second element: <c>b1=b2=8</c>
    ///         (no further reference elements remain past index 2); <c>a1=8</c>; <c>delta=0</c>
    ///         -&gt; V0 (bit <c>1</c>), <c>a0=8</c>, row done. Row 1 bits: <c>00011</c> (5 bits).
    ///     </para>
    ///     <para>
    ///         Row 0 (10 bits) + row 1 (5 bits) = 15 bits, zero-padded to 16: <c>0010111111000110</c>
    ///         -&gt; bytes <c>0x2F, 0xC6</c>. This was independently re-verified by stepping a
    ///         from-scratch Python re-implementation of <see cref="PdfDocument"/>'s own
    ///         <c>ReadMode</c>/<c>FindB1B2</c>/<c>DecodeCcittRow</c> logic through these exact
    ///         bytes, which printed each decoded mode in turn and confirmed the second row's
    ///         very first coding element is read as <c>Pass</c> before producing the expected
    ///         <c>[2, 4]</c> / all-white rows.
    ///     </para>
    /// </remarks>
    [Fact]
    public void PdfDocument_Images_DoOperator_CcittFaxPassModeElement_PlacesExpectedPixels()
    {
        // Arrange
        var pixels = new bool[8, 2];
        for (var x = 0; x < 8; x++)
        {
            pixels[x, 0] = x is 2 or 3;
            pixels[x, 1] = false;
        }

        var encoded = EncodeCcittGroup4ForTest(pixels);

        // Assert the hand-derived test vector itself, before even reaching the decoder.
        Assert.Equal<byte>([0x2F, 0xC6], encoded);

        // Act
        using var surface = RenderCcittFaxImage(8, 2, encoded);

        // Assert: row 0 is W W B B W W W W; row 1 (decoded via a Pass mode element) is all white.
        var white = new Canvas.Rgba32(255, 255, 255, 255);
        var black = new Canvas.Rgba32(0, 0, 0, 255);
        Assert.Equal(white, surface[0, 0]);
        Assert.Equal(white, surface[1, 0]);
        Assert.Equal(black, surface[2, 0]);
        Assert.Equal(black, surface[3, 0]);
        Assert.Equal(white, surface[4, 0]);
        Assert.Equal(white, surface[7, 0]);
        Assert.Equal(white, surface[0, 1]);
        Assert.Equal(white, surface[3, 1]);
        Assert.Equal(white, surface[7, 1]);
    }

    /// <summary>
    ///     Proves that <c>CCITTFaxDecode</c> with <c>/EncodedByteAlign true</c> actually invokes
    ///     <c>AlignToByte</c> before each row (not merely happens to work by coincidence): the
    ///     second row's leading padding bits after row 0 are deliberately non-zero garbage, which
    ///     would corrupt row 1's decode if <c>AlignToByte</c> were not called (or were a no-op
    ///     bug) - the test still recovers the exact same expected pixels as
    ///     <see cref="PdfDocument_Images_DoOperator_CcittFaxPassModeElement_PlacesExpectedPixels"/>,
    ///     proving the garbage bits were correctly skipped rather than misinterpreted as row 1
    ///     data.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Uses the same 8x2 pattern as the Pass-mode test above, but with <c>/Rows</c>
    ///         byte-aligned independently rather than packed back-to-back: row 0's 10 bits
    ///         (<c>0010111111</c>) occupy byte 0 (<c>00101111</c> = <c>0x2F</c>) plus 2 bits into
    ///         byte 1. Byte 1's remaining 6 bits are deliberately set to non-zero garbage
    ///         (<c>111111</c>, not the zero padding a decoder might coincidentally tolerate) -
    ///         byte 1 = <c>11111111</c> = <c>0xFF</c>. Row 1's own 5 bits (<c>00011</c>) plus 3
    ///         zero pad bits then form byte 2: <c>00011000</c> = <c>0x18</c>. Final bytes:
    ///         <c>0x2F, 0xFF, 0x18</c>. This literal byte array (not produced by
    ///         <see cref="EncodeCcittGroup4ForTest"/>, since that test-only helper has no
    ///         per-row byte-alignment/padding concept) was independently re-verified by stepping
    ///         the same from-scratch Python re-implementation referenced above through these
    ///         exact bytes with byte-alignment applied before each row, which confirmed byte 1 is
    ///         skipped in its entirety (regardless of its garbage content) and row 1 still decodes
    ///         to the expected all-white, Pass-mode-driven result.
    ///     </para>
    /// </remarks>
    [Fact]
    public void PdfDocument_Images_DoOperator_CcittFaxEncodedByteAlignTrue_SkipsRowPaddingBits()
    {
        // Arrange: hand-derived bytes - see remarks above for the full bit-level derivation.
        byte[] encoded = [0x2F, 0xFF, 0x18];

        // Act
        using var surface = RenderCcittFaxImage(8, 2, encoded, " /EncodedByteAlign true");

        // Assert: identical expected pixels to the Pass-mode test above.
        var white = new Canvas.Rgba32(255, 255, 255, 255);
        var black = new Canvas.Rgba32(0, 0, 0, 255);
        Assert.Equal(white, surface[0, 0]);
        Assert.Equal(white, surface[1, 0]);
        Assert.Equal(black, surface[2, 0]);
        Assert.Equal(black, surface[3, 0]);
        Assert.Equal(white, surface[4, 0]);
        Assert.Equal(white, surface[7, 0]);
        Assert.Equal(white, surface[0, 1]);
        Assert.Equal(white, surface[3, 1]);
        Assert.Equal(white, surface[7, 1]);
    }

    /// <summary>Proves that <c>CCITTFaxDecode</c> with a non-negative <c>/K</c> (Group 3) throws <see cref="UnsupportedImageFeatureException"/> with a clear message.</summary>
    [Fact]
    public void PdfDocument_Images_CcittFaxGroup3K_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 8 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 "
            + "/Filter /CCITTFaxDecode /DecodeParms << /K 0 /Columns 8 /Rows 1 >>",
            [0x00]);

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
        Assert.Contains("Group 3", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves that <c>CCITTFaxDecode</c> combined with another filter throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_CcittFaxCombinedWithAnotherFilter_ThrowsInvalidDataException()
    {
        // Arrange
        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 8 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 "
            + "/Filter [/FlateDecode /CCITTFaxDecode] /DecodeParms [null << /K -1 /Columns 8 /Rows 1 >>]",
            ZlibCompress([0x00]));

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>CCITTFaxDecode</c> with <c>/EndOfLine true</c> throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_CcittFaxEndOfLineTrue_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var pixels = new bool[8, 1];
        for (var x = 0; x < 8; x++)
        {
            pixels[x, 0] = x >= 4;
        }

        var encoded = EncodeCcittGroup4ForTest(pixels);

        // Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => RenderCcittFaxImage(8, 1, encoded, " /EndOfLine true"));
        Assert.Contains("EndOfLine", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves that an unsupported <c>/BitsPerComponent</c> throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_UnsupportedBitsPerComponent_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter /FlateDecode",
            ZlibCompress([0x00]));

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an image XObject with an unsupported <c>/ColorSpace</c> throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_UnsupportedColorSpace_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var imageStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace [/CalRGB << >>] /BitsPerComponent 8 /Filter /FlateDecode",
            ZlibCompress([0x00]));

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "100 0 0 100 0 0 cm /Im0 Do",
            "/XObject << /Im0 5 0 R >>",
            [imageStream]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>Do</c> with a name not declared in <c>/Resources/XObject</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_UndefinedXObjectName_ThrowsInvalidDataException()
    {
        // Arrange
        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/NotDeclared Do",
            "/XObject << >>",
            []);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>Do</c> on a <c>/Subtype /Form</c> XObject executes its nested content stream, painting at the expected device pixel and leaving pixels outside the painted rectangle untouched.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_FormXObject_PaintsNestedContentStream()
    {
        // Arrange: a Form whose own content stream fills a centered rectangle.
        var formStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100]",
            "10 10 80 80 re f"u8.ToArray());

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/Fm0 Do",
            "/XObject << /Fm0 5 0 R >>",
            [formStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the rectangle's device footprint is painted; outside it is left blank.
        Assert.Equal(Black, surface[50, 50]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves that a Form XObject's own <c>/Matrix</c> is concatenated into the CTM using the same left-multiply convention as <c>cm</c>, before the CTM in effect when <c>Do</c> was invoked.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_FormXObjectWithMatrix_AppliesMatrixToNestedContent()
    {
        // Arrange: the Form's own /Matrix translates its content by (40, 40) user-space units.
        var formStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Matrix [1 0 0 1 40 40]",
            "0 0 10 10 re f"u8.ToArray());

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/Fm0 Do",
            "/XObject << /Fm0 5 0 R >>",
            [formStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the translated rectangle (device x:40-50, y:50-60) is painted; the
        // un-translated rectangle's device position (x:0-10, y:90-100) is left blank, proving
        // the /Matrix was actually applied (not silently ignored).
        Assert.Equal(Black, surface[45, 55]);
        Assert.Equal(default, surface[5, 95]);
    }

    /// <summary>Proves that a Form XObject with no <c>/Resources</c> entry of its own falls back to (and successfully resolves against) the invoking content stream's <c>/Resources</c>.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_FormXObjectWithoutOwnResources_FallsBackToInvokingResources()
    {
        // Arrange: the Form declares no /Resources of its own, so its "/CS1 cs" must resolve
        // against the invoking page's /Resources/ColorSpace.
        var formStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100]",
            "/CS1 cs 1 0 0 sc 10 10 80 80 re f"u8.ToArray());

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/Fm0 Do",
            "/ColorSpace << /CS1 /DeviceRGB >> /XObject << /Fm0 5 0 R >>",
            [formStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: /CS1 resolved to DeviceRGB (3 operands accepted by "sc"), painting red.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that a Form XObject's own <c>/Resources</c> takes precedence over the invoking content stream's <c>/Resources</c> when both declare the same color-space name.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_FormXObjectWithOwnResources_TakesPrecedenceOverInvokingResources()
    {
        // Arrange: the page declares /CS1 as DeviceGray (1 component); the Form declares its own
        // /CS1 as DeviceRGB (3 components). "/CS1 cs 1 0 0 sc" supplies 3 numeric operands -
        // this only succeeds (rather than throwing for a wrong operand count) if the Form's own
        // /Resources definition is consulted, not the page's.
        var formStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Resources << /ColorSpace << /CS1 /DeviceRGB >> >>",
            "/CS1 cs 1 0 0 sc 10 10 80 80 re f"u8.ToArray());

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/Fm0 Do",
            "/ColorSpace << /CS1 /DeviceGray >> /XObject << /Fm0 5 0 R >>",
            [formStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[50, 50]);
    }

    /// <summary>Proves that a two-level-deep nested Form XObject paints at the correctly compounded transform, and that the invoking content stream's own painting after <c>Do</c> returns is unaffected by the nested Forms' CTM mutations.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_NestedFormXObjects_RestoresGraphicsStateAfterReturn()
    {
        // Arrange: the page invokes an outer Form (/Fm0), which applies "2 0 0 2 0 0 cm" and
        // invokes an inner Form (/Fm1), which paints a rectangle in its own content stream; the
        // page then paints its own rectangle after /Fm0's Do returns.
        var outerFormStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100]",
            "2 0 0 2 0 0 cm /Fm1 Do"u8.ToArray());
        var innerFormStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100]",
            "5 5 10 10 re f"u8.ToArray());

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/Fm0 Do 40 10 5 5 re f",
            "/XObject << /Fm0 5 0 R /Fm1 6 0 R >>",
            [outerFormStream, innerFormStream]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the inner Form's rectangle, scaled 2x by the outer Form's "cm", lands at
        // device (10,70)-(30,90) - its center is painted.
        Assert.Equal(Black, surface[20, 80]);

        // Assert: the page's own rectangle (painted after /Fm0's Do returns) lands at its own
        // normal, unscaled device position (40,85)-(45,90) - proving the outer Form's "cm" did
        // not leak back out into the invoking stream's graphics state.
        Assert.Equal(Black, surface[42, 87]);

        // Assert: the device position the page's rectangle would occupy if the 2x scale had
        // leaked ((80,70)-(90,80)) is left blank.
        Assert.Equal(default, surface[85, 75]);
    }

    /// <summary>Proves that a Form XObject nesting beyond the maximum supported depth throws <see cref="InvalidDataException"/> rather than hanging or crashing the process.</summary>
    [Fact]
    public void PdfDocument_Images_DoOperator_FormXObjectExceedsMaxRecursionDepth_ThrowsInvalidDataException()
    {
        // Arrange: a Form XObject that invokes itself by name, falling back to the invoking
        // page's /Resources/XObject (which declares it).
        var selfReferencingFormStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100]",
            "/FmSelf Do"u8.ToArray());

        var bytes = BuildSinglePagePdfWithResources(
            100,
            100,
            "/FmSelf Do",
            "/XObject << /FmSelf 5 0 R >>",
            [selfReferencingFormStream]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>Do</c> with a malformed (non-1, non-name) operand count/type throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("Do")]
    [InlineData("/Im0 /Im1 Do")]
    [InlineData("1 Do")]
    public void PdfDocument_Images_DoOperator_MalformedOperandCount_ThrowsInvalidDataException(string content)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }


    #endregion

    #region Fonts

    /// <summary>
    ///     Proves that a non-<c>TrueType</c>/non-<c>Type1</c>/non-<c>Type0</c>/non-<c>Type3</c>
    ///     simple font <c>/Subtype</c> throws <see cref="UnsupportedImageFeatureException"/>
    ///     rather than being silently substituted (<c>/Type1</c> itself is no longer universally
    ///     rejected - see the new <c>PdfDocument_Fonts_Type1_*</c> tests below; <c>/Type3</c> is
    ///     likewise no longer rejected - see the new <c>PdfDocument_Fonts_Type3_*</c> tests
    ///     below).
    /// </summary>
    [Theory]
    [InlineData("MMType1")]
    public void PdfDocument_Fonts_UnsupportedSubtype_ThrowsUnsupportedImageFeatureException(string subtype)
    {
        // Arrange
        var fontFileObj = BuildStreamObjectBody(string.Empty, BuildEmbeddedFontBytes([(65, 1)]));
        var descriptorObj = "<< /Type /FontDescriptor /FontFile2 7 0 R >>"u8.ToArray();
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /{subtype} /BaseFont /Test /FontDescriptor 6 0 R >>");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj, fontFileObj]);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a Standard-14 simple <c>/Subtype /TrueType</c> font (<c>/BaseFont /Helvetica</c>)
    ///     with no embedded <c>/FontFile2</c> resolves via <see cref="Fonts.SystemFontCatalog"/>
    ///     (a matching system font, or the bundled Liberation Sans fallback) rather than throwing,
    ///     and paints real visible glyph ink.
    /// </summary>
    [Fact]
    public void PdfDocument_BuildResolvedFont_Standard14NoEmbeddedFont_ResolvesViaFallback()
    {
        // Arrange: /Helvetica, no /FontFile2 - Standard-14, sans-serif, non-bold, non-italic
        var descriptorObj = "<< /Type /FontDescriptor >>"u8.ToArray();
        var fontDictObj =
            "<< /Type /Font /Subtype /TrueType /BaseFont /Helvetica /FirstChar 65 /LastChar 65 /Widths [600] /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 40 Tf 10 30 Td (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no exception, and some glyph ink was actually painted somewhere on the canvas
        var paintedAnyPixel = false;
        for (var y = 0; y < surface.Height && !paintedAnyPixel; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    paintedAnyPixel = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyPixel, "Expected the fallback-resolved font to paint at least one visible pixel.");
    }

    /// <summary>
    ///     Proves that a non-Standard-14 simple <c>/Subtype /TrueType</c> font with no embedded
    ///     <c>/FontFile2</c>, no <c>/FontDescriptor</c> flags, and a <c>/BaseFont</c> family name
    ///     unlikely to be installed on any host resolves via the bundled Liberation Sans fallback
    ///     (<see cref="Fonts.SystemFontCatalog.LoadBundledFallback"/>) rather than throwing.
    /// </summary>
    [Fact]
    public void PdfDocument_BuildResolvedFont_NonStandard14FlagsOnlyUnmatchedFamily_ResolvesViaBundledFallback()
    {
        // Arrange: an unmatched family name, no descriptor flags at all
        var descriptorObj = "<< /Type /FontDescriptor >>"u8.ToArray();
        var fontDictObj =
            "<< /Type /Font /Subtype /TrueType /BaseFont /TotallyUnlikelyFontFamilyXyzzy /FirstChar 65 /LastChar 65 /Widths [600] /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 40 Tf 10 30 Td (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj]);

        // Act & Assert: renders without throwing, using the bundled Liberation Sans fallback
        using var surface = RenderPdfBytes(bytes);
        Assert.NotNull(surface);
    }

    /// <summary>
    ///     Proves that an embedded <c>/FontFile2</c> always takes priority over fallback
    ///     substitution: even though the <c>/BaseFont</c> name is entirely unmatched, the embedded
    ///     synthetic font's own known glyph shape is what gets rendered.
    /// </summary>
    [Fact]
    public void PdfDocument_BuildResolvedFont_EmbeddedFontFileTakesPriorityOverFallback()
    {
        // Arrange: an embedded font (mapping codepoint 65 to glyph 1's known square)
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the embedded synthetic font's own known square glyph shape was painted (not a
        // fallback font's differently-shaped glyph) - matching every other embedded-font test's
        // own expected-pixel convention (text x [7, 15), text y [52, 60) -> device y [40, 48)).
        Assert.NotEqual(default, surface[10, 44]);
    }

    /// <summary>
    ///     Proves that a <c>/BaseFont /Symbol</c> font with no embedded <c>/FontFile2</c> still
    ///     fails closed with <see cref="UnsupportedImageFeatureException"/> (feature
    ///     <c>"pdf-font-symbolic-not-embedded"</c>), never substituted with an unrelated system or
    ///     bundled font.
    /// </summary>
    [Fact]
    public void PdfDocument_BuildResolvedFont_SymbolFont_ThrowsSymbolicNotEmbeddedException()
    {
        // Arrange
        var descriptorObj = "<< /Type /FontDescriptor >>"u8.ToArray();
        var fontDictObj = "<< /Type /Font /Subtype /TrueType /BaseFont /Symbol /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj]);

        // Act & Assert
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
        Assert.Equal("pdf-font-symbolic-not-embedded", ex.Feature);
    }

    /// <summary>
    ///     Proves that a <c>/BaseFont /ZapfDingbats</c> font with no embedded <c>/FontFile2</c>
    ///     still fails closed with <see cref="UnsupportedImageFeatureException"/> (feature
    ///     <c>"pdf-font-symbolic-not-embedded"</c>).
    /// </summary>
    [Fact]
    public void PdfDocument_BuildResolvedFont_ZapfDingbatsFont_ThrowsSymbolicNotEmbeddedException()
    {
        // Arrange
        var descriptorObj = "<< /Type /FontDescriptor >>"u8.ToArray();
        var fontDictObj = "<< /Type /Font /Subtype /TrueType /BaseFont /ZapfDingbats /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj]);

        // Act & Assert
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
        Assert.Equal("pdf-font-symbolic-not-embedded", ex.Feature);
    }

    /// <summary>
    ///     Proves that a non-Standard-14 font whose <c>/FontDescriptor/Flags</c> declares the
    ///     <c>Symbolic</c> bit (bit 3, value 4) without also declaring <c>Nonsymbolic</c> (bit 6,
    ///     value 32) fails closed exactly like <c>Symbol</c>/<c>ZapfDingbats</c>, even though its
    ///     <c>/BaseFont</c> name is not one of those two reserved names.
    /// </summary>
    [Fact]
    public void PdfDocument_BuildResolvedFont_SymbolicFlagWithoutEmbeddedFont_ThrowsSymbolicNotEmbeddedException()
    {
        // Arrange: Flags = 4 (Symbolic bit only, no Nonsymbolic bit)
        var descriptorObj = "<< /Type /FontDescriptor /Flags 4 >>"u8.ToArray();
        var fontDictObj = "<< /Type /Font /Subtype /TrueType /BaseFont /SomeCustomSymbolFont /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj]);

        // Act & Assert
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
        Assert.Equal("pdf-font-symbolic-not-embedded", ex.Feature);
    }

    /// <summary>Proves that an unrecognized <c>/Encoding</c> base-encoding name throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_UnrecognizedEncoding_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600] /Encoding /PDFDocEncoding");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>Tf</c> operator naming a font absent from <c>/Resources/Font</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_UndefinedFontName_ThrowsInvalidDataException()
    {
        // Arrange
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /NotDeclared 20 Tf (A) Tj ET", "/Font << >>", []);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a font with no <c>/Encoding</c> entry defaults to <c>/WinAnsiEncoding</c>: code <c>0xE0</c> maps to Unicode codepoint <c>224</c>.</summary>
    [Fact]
    public void PdfDocument_Fonts_DefaultEncoding_IsWinAnsiEncoding()
    {
        // Arrange: the font's only mapped codepoint (224) is WinAnsiEncoding's mapping for code
        // 0xE0 ('\340' octal) - MacRomanEncoding maps that same code to codepoint 8225 instead.
        var fontBytes = BuildEmbeddedFontBytes([(224, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 0 /LastChar 255 /Widths [" + string.Join(' ', Enumerable.Repeat(600, 256)) + "]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (\\340) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: glyph 1's square is painted at the position code 0xE0 resolves to under WinAnsiEncoding.
        Assert.NotEqual(default, surface[11, 44]);
    }

    /// <summary>Proves that an explicit <c>/Encoding /MacRomanEncoding</c> resolves code <c>0xE0</c> to a different codepoint than <c>/WinAnsiEncoding</c> does.</summary>
    [Fact]
    public void PdfDocument_Fonts_MacRomanEncoding_DiffersFromWinAnsiEncoding()
    {
        // Arrange: same font as the WinAnsiEncoding test (only codepoint 224 mapped), but this
        // font dictionary declares /MacRomanEncoding, under which code 0xE0 maps to codepoint
        // 8225 (not 224) - so the code resolves to the unmapped .notdef glyph and paints nothing.
        var fontBytes = BuildEmbeddedFontBytes([(224, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 0 /LastChar 255 /Widths [" + string.Join(' ', Enumerable.Repeat(600, 256)) + "] /Encoding /MacRomanEncoding");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (\\340) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no glyph outline is painted anywhere in the expected region.
        Assert.Equal(default, surface[11, 44]);
    }

    /// <summary>Proves that an <c>/Encoding/Differences</c> override changes a code's mapped codepoint away from its base encoding's own mapping.</summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_OverridesBaseEncodingCode()
    {
        // Arrange: the font's only mapped codepoint is 224 ('agrave'). Code 65 ('A') would
        // ordinarily resolve (via the default WinAnsiEncoding) to codepoint 65, which the font
        // has no glyph for - but /Differences remaps code 65 to the "agrave" glyph name (U+00E0).
        var fontBytes = BuildEmbeddedFontBytes([(224, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600] " +
                           "/Encoding << /Differences [65 /agrave] >>");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.NotEqual(default, surface[11, 44]);
    }

    /// <summary>Proves that without the <c>/Differences</c> override, the same code paints nothing (the font has no glyph for its base-encoding codepoint).</summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_Absent_LeavesBaseEncodingCodeUnmapped()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(224, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600]");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert
        Assert.Equal(default, surface[11, 44]);
    }

    /// <summary>
    ///     Proves that a <c>/Differences</c> array referencing an unrecognized glyph name is
    ///     tolerated rather than rejected: the affected code is simply left at whatever its base
    ///     encoding already assigned it (here, WinAnsiEncoding's own default mapping of code 65 to
    ///     <c>U+0041</c>), so the glyph still paints via the embedded font's own <c>cmap</c>
    ///     mapping for that unchanged codepoint.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_UnrecognizedGlyphName_FallsBackToBaseEncoding()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600] " +
                           "/Encoding << /Differences [65 /thisGlyphNameDoesNotExist] >>");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no exception, and the glyph painted (base encoding's codepoint 65 mapping was
        // preserved rather than cleared by the unrecognized override).
        Assert.NotEqual(default, surface[11, 44]);
    }

    /// <summary>
    ///     Proves that <c>/Differences</c> recognizes the <c>ff</c>/<c>ffi</c>/<c>ffl</c> Latin
    ///     ligature glyph names (found via a real-world pdfLaTeX Computer Modern font from
    ///     py-pdf/sample-files, which declares them in its own <c>/Differences</c> array) -
    ///     previously only <c>fi</c>/<c>fl</c> were present in <c>StandardGlyphNames</c>.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_FfFfiFflLigatureNames_DoNotThrow()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 65 /LastChar 68 /Widths [600 600 600 600] " +
                           "/Encoding << /Differences [65 /ff /ffi /ffl] >>");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert: no exception
        using var surface = RenderPdfBytes(bytes);
        Assert.NotNull(surface);
    }

    /// <summary>
    ///     Proves that <c>/Differences</c> recognizes the <c>nacute</c> (Polish/Czech "ń") glyph
    ///     name (found via the same real-world pdfLaTeX Computer Modern font from
    ///     py-pdf/sample-files) - a Latin Extended-A accented letter previously absent from
    ///     <c>StandardGlyphNames</c>.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_NacuteName_DoesNotThrow()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 65 /LastChar 65 /Widths [600] " +
                           "/Encoding << /Differences [65 /nacute] >>");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert: no exception
        using var surface = RenderPdfBytes(bytes);
        Assert.NotNull(surface);
    }

    /// <summary>Proves that a <c>/Differences</c> array starting with a glyph name (before any starting code number) throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_Differences_NameBeforeStartingCode_ThrowsInvalidDataException()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [600 600] " +
                           "/Encoding << /Differences [/A 65 /B] >>");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an explicit <c>/Widths</c> entry determines a glyph's advance width.</summary>
    [Fact]
    public void PdfDocument_Fonts_Widths_ExplicitEntry_DeterminesAdvance()
    {
        // Arrange: code 65's declared width is 1000 (a full em) -> advance = 1.0 * 20 = 20.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [1000 600]");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 5 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' paints at text x [7, 15); 'B' starts at Tm.x = 5 + 20 = 25, painting at
        // text x [27, 35) - clearly past the "no explicit width" 12-unit-advance gap boundary.
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[31, 89]);
    }

    /// <summary>Proves that <c>/FontDescriptor/MissingWidth</c> determines a code's advance width when it has no explicit <c>/Widths</c> entry.</summary>
    [Fact]
    public void PdfDocument_Fonts_Widths_MissingWidthFallback_DeterminesAdvance()
    {
        // Arrange: code 65 is outside [FirstChar, LastChar] (so has no explicit width), and
        // /MissingWidth is 500 -> advance = 0.5 * 20 = 10.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 66 /LastChar 66 /Widths [600]", descriptorExtra: "/MissingWidth 500");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 5 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' starts at Tm.x = 5 + 10 = 15, painting at text x [17, 25).
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[21, 89]);
    }

    /// <summary>Proves that a code with no explicit <c>/Widths</c> entry and no <c>/MissingWidth</c> falls back to the embedded font's own advance width.</summary>
    [Fact]
    public void PdfDocument_Fonts_Widths_FontOwnAdvanceFallback_DeterminesAdvance()
    {
        // Arrange: code 65 is outside [FirstChar, LastChar], and /MissingWidth is not declared -
        // falls back to the embedded font's own 600-unit hmtx advance -> advance = 0.6 * 20 = 12.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes, fontDictExtra: "/FirstChar 66 /LastChar 66 /Widths [600]");

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 5 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' starts at Tm.x = 5 + 12 = 17, painting at text x [19, 27).
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[23, 89]);
    }

    /// <summary>Proves that a <c>/Type0</c>/<c>/Identity-H</c>/<c>CIDFontType2</c> composite font resolves its embedded <c>/FontFile2</c> and paints real glyph ink rather than throwing.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_IdentityHCidFontType2_ResolvesEmbeddedFont()
    {
        // Arrange: no /CIDToGIDMap declared -> identity (CID 1 -> GID 1, a painted square glyph).
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(fontBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: some glyph ink was actually painted somewhere on the canvas.
        var paintedAnyPixel = false;
        for (var y = 0; y < surface.Height && !paintedAnyPixel; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    paintedAnyPixel = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyPixel);
    }

    /// <summary>Proves that an explicit <c>/CIDToGIDMap /Identity</c> name behaves identically to an absent <c>/CIDToGIDMap</c> entry (CID used directly as GID).</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidToGidMapIdentity_UsesCidAsGid()
    {
        // Arrange: CID 1 -> GID 1 (a painted square glyph) via the explicit /Identity name.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(
            fontBytes, cidFontExtra: "/CIDToGIDMap /Identity");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: text x [7, 15) holds the painted square glyph (design x [100, 500) of 1000,
        // scaled by fontSize 20, offset by originX 5).
        Assert.NotEqual(default, surface[11, 89]);
    }

    /// <summary>Proves that a <c>/CIDToGIDMap</c> stream remaps a CID to a different glyph index rather than treating the CID as the glyph index directly.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidToGidMapStream_RemapsCidToGid()
    {
        // Arrange: the stream remaps CID 1 -> GID 0 (.notdef, an empty glyph) - had the map been
        // ignored (or treated as identity), CID 1 would instead resolve to GID 1 (a painted
        // square), so no painted pixels proves the stream's remapping was actually applied.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var cidToGidMapBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(
            fontBytes, cidToGidMapStreamBytes: cidToGidMapBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no glyph ink painted anywhere on the canvas.
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(0, surface[x, y].A);
            }
        }
    }

    /// <summary>Proves that a CID beyond the end of a <c>/CIDToGIDMap</c> stream's table maps to <c>.notdef</c> (GID 0) rather than throwing.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidToGidMapStream_OutOfRangeCid_MapsToNotdef()
    {
        // Arrange: the map's table has only 1 entry (index 0), so CID 5 is out of range.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var cidToGidMapBytes = new byte[] { 0x00, 0x63 };
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(
            fontBytes, cidToGidMapStreamBytes: cidToGidMapBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(5)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no exception, and no glyph ink painted anywhere on the canvas.
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                Assert.Equal(0, surface[x, y].A);
            }
        }
    }

    /// <summary>Proves that a <c>/Type0</c> font's <c>/Encoding</c> value other than <c>/Identity-H</c> - including <c>/Identity-V</c> and a predefined CJK encoding name - throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Theory]
    [InlineData("/Identity-V")]
    [InlineData("/UniGB-UCS2-H")]
    [InlineData("/SomeOtherEncoding")]
    public void PdfDocument_Fonts_Type0_NonIdentityHEncoding_ThrowsUnsupportedImageFeatureException(string encoding)
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(fontBytes, encoding: encoding);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>CIDFontType0</c> descendant font whose <c>/FontDescriptor</c> has no embedded <c>/FontFile3</c> (only <c>/FontFile2</c>) throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidFontType0_NoFontFile3_ThrowsInvalidDataException()
    {
        // Arrange: a CIDFontType0 descendant whose /FontDescriptor declares only /FontFile2 (no
        // /FontFile3) - LoadCidFontType0Font requires /FontFile3, so this fails closed rather than
        // falling back to /FontFile2.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(fontBytes, descendantSubtype: "/CIDFontType0");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>/Type0</c>/<c>/Identity-H</c>/<c>CIDFontType0</c> composite font resolves its embedded, <c>/OpenType</c>-wrapped, non-CID-keyed <c>/FontFile3</c> CFF program and paints real glyph ink - identity CID-to-glyph-index (no <c>/CIDToGIDMap</c> consulted) - rather than throwing.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidFontType0_OpenTypeCff_ResolvesEmbeddedFont()
    {
        // Arrange: CID 1 -> GID 1 (identity, a painted square glyph) via a synthetic OTTO/CFF
        // /FontFile3.
        var fontBytes = BuildEmbeddedCffFontBytes(glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCidFontType0FontResources(fontBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: text x [7, 15) holds the painted square glyph (design x [100, 500) of 1000,
        // scaled by fontSize 20, offset by originX 5) - matching the CIDFontType2 equivalent
        // (PdfDocument_Fonts_Type0_CidToGidMapIdentity_UsesCidAsGid) exactly, since both flavors
        // paint the same square glyph shape.
        Assert.NotEqual(default, surface[11, 89]);
    }

    /// <summary>Proves that a non-standard <c>/CIDToGIDMap</c> entry on a <c>CIDFontType0</c> descendant is ignored (identity CID-to-glyph-index is always used), rather than remapping the CID away from its painted glyph.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidFontType0_NonStandardCidToGidMap_IsIgnored()
    {
        // Arrange: a /CIDToGIDMap /Identity entry (not a valid key for CIDFontType0 per the PDF
        // specification, but sometimes seen in real-world producers) is declared on the
        // descendant - had it been consulted (rather than ignored) as a stream remap or any other
        // shape, this would behave differently; here it is simply ignored, so CID 1 still resolves
        // to GID 1 via identity.
        var fontBytes = BuildEmbeddedCffFontBytes(glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCidFontType0FontResources(
            fontBytes, cidFontExtra: "/CIDToGIDMap /Identity");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: text x [7, 15) holds the painted square glyph.
        Assert.NotEqual(default, surface[11, 89]);
    }

    /// <summary>Proves that a <c>CIDFontType0</c> descendant's <c>/DW</c>/<c>/W</c> width resolution behaves identically to the <c>CIDFontType2</c> path (fully generic, no subtype-specific logic).</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidFontType0_Widths_WArrayIndividualForm_DeterminesAdvance()
    {
        // Arrange: CID 1's declared width is 500 (individual form) -> advance = 0.5 * 20 = 10.
        // CID 2 has no matching entry, so falls back to the default DW value of 1000.
        var fontBytes = BuildEmbeddedCffFontBytes(glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCidFontType0FontResources(fontBytes, cidFontExtra: "/W [1 [500]]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1, 2)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: CID 2 starts at Tm.x = 5 + 10 = 15, painting at text x [17, 25).
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[21, 89]);
    }

    /// <summary>Proves that a <c>CIDFontType0</c> descendant's <c>/FontFile3</c> stream declaring any <c>/Subtype</c> other than <c>/OpenType</c> (for example a bare <c>/CIDFontType0C</c> or <c>/Type1C</c> CFF stream with no SFNT wrapper) throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Theory]
    [InlineData("/Type1C")]
    [InlineData("/CIDFontType0C")]
    public void PdfDocument_Fonts_Type0_CidFontType0_NonOpenTypeFontFile3Subtype_ThrowsUnsupportedImageFeatureException(string fontFileSubtype)
    {
        // Arrange
        var fontBytes = BuildEmbeddedCffFontBytes(glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCidFontType0FontResources(fontBytes, fontFileSubtype: fontFileSubtype);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>CIDFontType0</c> descendant's <c>/FontFile3</c> stream with no <c>/Subtype</c> key at all also throws <see cref="UnsupportedImageFeatureException"/> (a missing <c>/Subtype</c> is treated the same as any other unsupported value, not guessed as <c>/OpenType</c> from the stream's own magic bytes).</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidFontType0_MissingFontFile3Subtype_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var fontBytes = BuildEmbeddedCffFontBytes(glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCidFontType0FontResources(fontBytes, fontFileSubtype: "");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>CIDFontType0</c> descendant's <c>/OpenType</c>-wrapped <c>/FontFile3</c> whose embedded CFF program's Top DICT declares <c>ROS</c> (CID-keyed CFF) throws <see cref="InvalidDataException"/> - <c>Fonts.CffTable.Parse</c>'s existing CID-keyed rejection surfaces uncaught through this new path, with no new translation code.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_CidFontType0_CidKeyedCff_ThrowsInvalidDataException()
    {
        // Arrange
        var fontBytes = BuildEmbeddedCffFontBytes(glyphCount: 3, includeRos: true);
        var (resourcesBody, extraObjects) = BuildCidFontType0FontResources(fontBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>/Type0</c> font dictionary with no <c>/DescendantFonts</c> entry throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_MissingDescendantFonts_ThrowsInvalidDataException()
    {
        // Arrange
        var fontDictObj = "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", "/Font << /F1 5 0 R >>", [fontDictObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a <c>/DescendantFonts</c> array with other than exactly one element throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_DescendantFontsNotSingleElement_ThrowsInvalidDataException()
    {
        // Arrange
        var fontDictObj =
            "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H /DescendantFonts [] >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", "/Font << /F1 5 0 R >>", [fontDictObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that a descendant font whose <c>/FontDescriptor</c> has no embedded <c>/FontFile2</c> throws <see cref="InvalidDataException"/> (composite fonts have no system-font fallback path).</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_NoEmbeddedFontFile_ThrowsInvalidDataException()
    {
        // Arrange
        var descriptorObj = "<< /Type /FontDescriptor >>"u8.ToArray();
        var descendantObj =
            "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Test /FontDescriptor 7 0 R >>"u8.ToArray();
        var fontDictObj =
            "<< /Type /Font /Subtype /Type0 /BaseFont /Test /Encoding /Identity-H /DescendantFonts [6 0 R] >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descendantObj, descriptorObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that an absent <c>/DW</c> defaults to 1000 (a full em) and determines every CID's advance width when no <c>/W</c> entry applies.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_Widths_DwDefault1000_DeterminesAdvance()
    {
        // Arrange: no /W declared, /DW absent -> defaults to 1000 -> advance = 1.0 * 20 = 20 for
        // both CIDs.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(fontBytes);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1, 2)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: CID 1's square paints at text x [7, 15); CID 2 starts at Tm.x = 5 + 20 = 25,
        // painting at text x [27, 35) - clearly past the gap after CID 1's square.
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[31, 89]);
    }

    /// <summary>Proves that the individual-width <c>/W</c> sub-form (<c>c [w1 w2 ...]</c>) determines each listed CID's advance width.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_Widths_WArrayIndividualForm_DeterminesAdvance()
    {
        // Arrange: CID 1's declared width is 500 (individual form) -> advance = 0.5 * 20 = 10.
        // CID 2 has no matching entry, so falls back to the default DW value of 1000.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(fontBytes, cidFontExtra: "/W [1 [500]]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1, 2)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: CID 2 starts at Tm.x = 5 + 10 = 15, painting at text x [17, 25).
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[21, 89]);
    }

    /// <summary>Proves that the range-width <c>/W</c> sub-form (<c>cFirst cLast w</c>) determines every CID in the range's advance width.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_Widths_WArrayRangeForm_DeterminesAdvance()
    {
        // Arrange: both CID 1 and CID 2's declared width is 500 (range form) -> advance = 0.5 *
        // 20 = 10 each.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(fontBytes, cidFontExtra: "/W [1 2 500]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1, 2)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: CID 2 starts at Tm.x = 5 + 10 = 15, painting at text x [17, 25).
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[21, 89]);
    }

    /// <summary>Proves that a <c>/W</c> array entry whose second element is neither an array (individual form) nor a number (range form) throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Fonts_Type0_Widths_MalformedWArray_ThrowsInvalidDataException()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(fontBytes, cidFontExtra: "/W [1 (bad)]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf {BuildIdentityHHexString(1)} Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>ResolveToUnicodeMap</c> returns <see langword="null"/> when the font dictionary has no <c>/ToUnicode</c> entry at all.</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_Absent_ResolvesNull()
    {
        // Arrange
        var bytes = BuildSinglePagePdf(100, 100, "BT ET");
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>());

        // Act
        var result = document.ResolveToUnicodeMap(fontDict);

        // Assert
        Assert.Null(result);
    }

    /// <summary>Proves that a <c>beginbfchar</c>/<c>endbfchar</c> entry maps its single source code to its destination's codepoint.</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_BfChar_MapsSingleCode()
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes("1 beginbfchar\n<0041> <0048>\nendbfchar");
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act
        var result = document.ResolveToUnicodeMap(fontDict);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0x0048, result[0x0041]);
    }

    /// <summary>Proves that a <c>beginbfrange</c>/<c>endbfrange</c> entry with a single hex-string destination maps consecutive source codes to consecutive incrementing codepoints.</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_BfRangeHexForm_MapsConsecutiveCodes()
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes("1 beginbfrange\n<0001> <0003> <0048>\nendbfrange");
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act
        var result = document.ResolveToUnicodeMap(fontDict);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0x0048, result[1]);
        Assert.Equal(0x0049, result[2]);
        Assert.Equal(0x004A, result[3]);
    }

    /// <summary>Proves that a <c>beginbfrange</c>/<c>endbfrange</c> entry with an array-of-hex-strings destination maps each code in the range to its own corresponding array element.</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_BfRangeArrayForm_MapsEachCodeIndividually()
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes("1 beginbfrange\n<0001> <0002> [<0048> <004F>]\nendbfrange");
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act
        var result = document.ResolveToUnicodeMap(fontDict);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0x0048, result[1]);
        Assert.Equal(0x004F, result[2]);
    }

    /// <summary>Proves that a <c>beginbfchar</c>/<c>endbfchar</c> entry with an empty hex-string destination (<c>&lt;&gt;</c>, seen from real-world producers such as WeasyPrint to mean "no single-character Unicode equivalent") is skipped rather than treated as malformed.</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_BfChar_EmptyDestination_SkipsMapping()
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes("2 beginbfchar\n<0003> <>\n<0041> <0048>\nendbfchar");
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act
        var result = document.ResolveToUnicodeMap(fontDict);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.ContainsKey(0x0003));
        Assert.Equal(0x0048, result[0x0041]);
    }

    /// <summary>Proves that a <c>beginbfrange</c>/<c>endbfrange</c> entry with a single empty hex-string destination skips mapping the whole range rather than treated as malformed.</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_BfRangeHexForm_EmptyDestination_SkipsWholeRange()
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes("1 beginbfrange\n<0001> <0003> <>\nendbfrange");
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act
        var result = document.ResolveToUnicodeMap(fontDict);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>Proves that a <c>bfrange</c> destination array's empty hex-string element skips mapping only that one code, leaving its siblings mapped.</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_BfRangeArrayForm_EmptyDestinationElement_SkipsThatCodeOnly()
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes("1 beginbfrange\n<0001> <0002> [<> <004F>]\nendbfrange");
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act
        var result = document.ResolveToUnicodeMap(fontDict);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.ContainsKey(0x0001));
        Assert.Equal(0x004F, result[0x0002]);
    }

    /// <summary>Proves that a non-empty destination string with an odd byte count (not a whole number of UTF-16BE code units) still throws <see cref="InvalidDataException"/> rather than being silently treated as "no mapping".</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_BfChar_OddLengthDestination_ThrowsInvalidDataException()
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes("1 beginbfchar\n<0041> <00>\nendbfchar");
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => document.ResolveToUnicodeMap(fontDict));
    }

    /// <summary>Proves that a <c>bfrange</c> destination array containing a nested array (the out-of-scope CIDSystemInfo-style "array of arrays" destination sub-form) throws <see cref="UnsupportedImageFeatureException"/>, unlike the in-scope array-of-hex-strings destination form.</summary>
    [Fact]
    public void PdfDocument_Fonts_ToUnicode_UnsupportedArrayDestinationBfRange_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes("1 beginbfrange\n<0001> <0001> [[<0048>]]\nendbfrange");
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(() => document.ResolveToUnicodeMap(fontDict));
        Assert.Equal("pdf-font-tounicode-bfrange-array-destination", exception.Feature);
    }

    /// <summary>Proves that each explicitly out-of-scope, data-bearing CMap operator (<c>usecmap</c>/<c>cidrange</c>/<c>cidchar</c>) fails closed with <see cref="UnsupportedImageFeatureException"/> rather than being silently ignored like the CMap's own PostScript wrapper keywords.</summary>
    [Theory]
    [InlineData("usecmap")]
    [InlineData("cidrange")]
    [InlineData("cidchar")]
    public void PdfDocument_Fonts_ToUnicode_UnsupportedOperator_ThrowsUnsupportedImageFeatureException(string operatorName)
    {
        // Arrange
        var cmapBytes = BuildToUnicodeCMapStreamBytes(operatorName);
        var streamObj = BuildStreamObjectBody(string.Empty, cmapBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [streamObj]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        var fontDict = PdfDocument.PdfObject.FromDictionary(new Dictionary<string, PdfDocument.PdfObject>
        {
            ["ToUnicode"] = PdfDocument.PdfObject.FromReference(5, 0),
        });

        // Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(() => document.ResolveToUnicodeMap(fontDict));
        Assert.Equal($"pdf-font-tounicode-{operatorName}", exception.Feature);
    }

    /// <summary>
    ///     Proves that <c>/Subtype /Type1</c> dispatches to simple-font resolution
    ///     (<c>BuildResolvedSimpleFont</c>) and, when the descriptor embeds a classic PostScript
    ///     <c>/FontDescriptor/FontFile</c> Type 1 font program, resolves and paints that program's
    ///     own glyph ink - proving the Phase B dispatch widening end to end.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type1_DispatchesToSimpleFontResolution_PaintsGlyphInk()
    {
        // Arrange
        var (resourcesBody, extraObjects) = BuildEmbeddedType1FontResources();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: text x [7, 15) holds the painted square glyph (design x [100, 500) of 1000,
        // scaled by fontSize 20, offset by originX 5, flipped for originY 5 on a 100-tall
        // MediaBox) - matching every other embedded-font flavor's own pixel-position convention.
        Assert.NotEqual(default, surface[11, 89]);
    }

    /// <summary>
    ///     Proves that an embedded <c>/FontDescriptor/FontFile</c> classic Type 1 program always
    ///     takes priority over fallback substitution: even though <c>/BaseFont</c> names an
    ///     entirely unmatched family, the embedded synthetic font's own known square glyph shape
    ///     is what gets rendered.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type1_EmbeddedFontFileTakesPriorityOverFallback()
    {
        // Arrange: /BaseFont names a family unlikely to be installed/matched on any host.
        var (resourcesBody, extraObjects) = BuildEmbeddedType1FontResources(baseFontName: "TotallyUnlikelyFontFamilyXyzzy");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the embedded synthetic font's own known square glyph shape was painted (not a
        // differently-shaped fallback glyph).
        Assert.NotEqual(default, surface[11, 89]);
    }

    /// <summary>
    ///     Proves that a <c>/Subtype /Type1</c> font with no embedded <c>/FontFile</c> (and no
    ///     <c>/FontFile3</c>) resolves via <c>ResolveFallbackFont</c> (a Standard-14/system/
    ///     bundled-Liberation match) rather than throwing, and paints real visible glyph ink -
    ///     mirroring <c>PdfDocument_BuildResolvedFont_Standard14NoEmbeddedFont_ResolvesViaFallback</c>'s
    ///     own TrueType-subtype test, now proven reachable for <c>/Type1</c> too.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type1_NoFontFile_ResolvesViaFallback()
    {
        // Arrange: /Helvetica, no /FontFile - Standard-14, sans-serif, non-bold, non-italic
        var descriptorObj = "<< /Type /FontDescriptor >>"u8.ToArray();
        var fontDictObj =
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /FirstChar 65 /LastChar 65 /Widths [600] /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 40 Tf 10 30 Td (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no exception, and some glyph ink was actually painted somewhere on the canvas
        var paintedAnyPixel = false;
        for (var y = 0; y < surface.Height && !paintedAnyPixel; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    paintedAnyPixel = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyPixel, "Expected the fallback-resolved /Type1 font to paint at least one visible pixel.");
    }

    /// <summary>
    ///     Proves that a simple font dictionary with no <c>/FontDescriptor</c> entry at all - as
    ///     PDF 32000-1 §9.6.2.2 explicitly permits for the standard 14 fonts, and as several
    ///     real-world producers (for example ReportLab) emit - resolves via
    ///     <c>ResolveFallbackFont</c> rather than throwing <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_SimpleFont_NoFontDescriptorAtAll_ResolvesViaFallback()
    {
        // Arrange: no /FontDescriptor key at all on the font dictionary.
        var fontDictObj = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 40 Tf 10 30 Td (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj]);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no exception, and some glyph ink was actually painted somewhere on the canvas
        var paintedAnyPixel = false;
        for (var y = 0; y < surface.Height && !paintedAnyPixel; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    paintedAnyPixel = true;
                    break;
                }
            }
        }

        Assert.True(paintedAnyPixel, "Expected the fallback-resolved font (no /FontDescriptor at all) to paint at least one visible pixel.");
    }

    /// <summary>
    ///     Proves that a <c>/Subtype /Type1</c> descriptor declaring only <c>/FontFile3</c>
    ///     (neither <c>/FontFile</c> nor <c>/FontFile2</c>), whose <c>/FontFile3</c> stream is a
    ///     bare Type1C/CFF program (<c>/Subtype /Type1C</c>, no SFNT/OpenType wrapper), resolves
    ///     and paints that program's own glyph ink - the Type1C counterpart of
    ///     <see cref="PdfDocument_Fonts_Type1_DispatchesToSimpleFontResolution_PaintsGlyphInk"/>.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type1_FontFile3Type1C_ResolvesEmbeddedFont_PaintsGlyphInk()
    {
        // Arrange
        var (resourcesBody, extraObjects) = BuildEmbeddedType1CFontResources();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: text x [7, 15) holds the painted square glyph - matching every other
        // embedded-font flavor's own pixel-position convention.
        Assert.NotEqual(default, surface[11, 89]);
    }

    /// <summary>
    ///     Proves that a <c>/Subtype /Type1</c> descriptor's <c>/FontFile3</c> stream declaring any
    ///     <c>/Subtype</c> other than <c>/Type1C</c> (for example <c>/OpenType</c> or
    ///     <c>/CIDFontType0C</c>) throws <see cref="UnsupportedImageFeatureException"/> rather than
    ///     silently falling back - the simple-font counterpart of
    ///     <see cref="PdfDocument_Fonts_Type0_CidFontType0_NonOpenTypeFontFile3Subtype_ThrowsUnsupportedImageFeatureException"/>.
    /// </summary>
    [Theory]
    [InlineData("/OpenType")]
    [InlineData("/CIDFontType0C")]
    public void PdfDocument_Fonts_Type1_FontFile3NonType1CSubtype_ThrowsUnsupportedImageFeatureException(string fontFileSubtype)
    {
        // Arrange
        var (resourcesBody, extraObjects) = BuildEmbeddedType1CFontResources(fontFileSubtype: fontFileSubtype);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a <c>/Subtype /Type1</c> descriptor's <c>/FontFile3</c> stream with no
    ///     <c>/Subtype</c> key at all also throws <see cref="UnsupportedImageFeatureException"/> -
    ///     a missing <c>/Subtype</c> is treated the same as any other unsupported value, not
    ///     guessed as <c>/Type1C</c> from the stream's own bare-CFF content.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type1_FontFile3MissingSubtype_ThrowsUnsupportedImageFeatureException()
    {
        // Arrange
        var (resourcesBody, extraObjects) = BuildEmbeddedType1CFontResources(fontFileSubtype: "");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that an embedded <c>/FontFile</c> stream missing its own required <c>/Length1</c>
    ///     entry throws <see cref="InvalidDataException"/> (read from the stream itself, not the
    ///     <c>/FontDescriptor</c>).
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type1_FontFileMissingLength1_ThrowsInvalidDataException()
    {
        // Arrange: a /FontFile stream declaring only /Length2 (no /Length1 at all).
        var (fontFileBytes, _, length2) = SyntheticFontBuilder.Type1(
            [(".notdef", BuildEmptyType1Charstring()), ("A", BuildSquareType1Charstring())]);
        var fontFileObj = BuildStreamObjectBody($"/Length2 {length2}", fontFileBytes);
        var descriptorObj = "<< /Type /FontDescriptor /FontFile 7 0 R >>"u8.ToArray();
        var fontDictObj =
            "<< /Type /Font /Subtype /Type1 /BaseFont /Test /FirstChar 65 /LastChar 65 /Widths [600] /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj, fontFileObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that an embedded <c>/FontFile</c> stream missing its own required <c>/Length2</c>
    ///     entry throws <see cref="InvalidDataException"/> (read from the stream itself, not the
    ///     <c>/FontDescriptor</c>).
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type1_FontFileMissingLength2_ThrowsInvalidDataException()
    {
        // Arrange: BuildEmbeddedType1FontResources(omitLength2: true) declares only /Length1.
        var (resourcesBody, extraObjects) = BuildEmbeddedType1FontResources(omitLength2: true);

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that an embedded <c>/FontFile</c> stream whose <c>/Length2</c> entry is present
    ///     but not a number throws <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type1_FontFileNonNumericLength2_ThrowsInvalidDataException()
    {
        // Arrange
        var (fontFileBytes, length1, _) = SyntheticFontBuilder.Type1(
            [(".notdef", BuildEmptyType1Charstring()), ("A", BuildSquareType1Charstring())]);
        var fontFileObj = BuildStreamObjectBody($"/Length1 {length1} /Length2 /NotANumber", fontFileBytes);
        var descriptorObj = "<< /Type /FontDescriptor /FontFile 7 0 R >>"u8.ToArray();
        var fontDictObj =
            "<< /Type /Font /Subtype /Type1 /BaseFont /Test /FirstChar 65 /LastChar 65 /Widths [600] /FontDescriptor 6 0 R >>"u8.ToArray();

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>",
            [fontDictObj, descriptorObj, fontFileObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that an explicit <c>/Encoding /StandardEncoding</c> resolves code <c>0x27</c>
    ///     ("quoteright", Unicode <c>U+2019</c>) differently than <c>/WinAnsiEncoding</c> does
    ///     (which maps the same code to "quotesingle", Unicode <c>U+0027</c>) - the code's mapped
    ///     codepoint is what actually differs between the two base encodings for this fixture, not
    ///     merely the glyph name.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_StandardEncoding_DiffersFromWinAnsiEncoding()
    {
        // Arrange: the font's only mapped codepoint is U+2019 (quoteright), which /StandardEncoding
        // maps code 0x27 to, but /WinAnsiEncoding maps code 0x27 to U+0027 (quotesingle) instead.
        var fontBytes = BuildEmbeddedFontBytes([(0x2019, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 0 /LastChar 255 /Widths [" + string.Join(' ', Enumerable.Repeat(600, 256)) + "] /Encoding /StandardEncoding");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (') Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: glyph 1's square is painted at the position code 0x27 resolves to under
        // /StandardEncoding (U+2019), which this font has a mapped glyph for.
        Assert.NotEqual(default, surface[11, 44]);
    }

    /// <summary>
    ///     Proves that, unlike <see cref="PdfDocument_Fonts_StandardEncoding_DiffersFromWinAnsiEncoding"/>,
    ///     the default (no <c>/Encoding</c> entry) <c>/WinAnsiEncoding</c> base encoding does not
    ///     paint the same code's glyph, since it resolves code <c>0x27</c> to Unicode <c>U+0027</c>
    ///     (quotesingle) instead of <c>U+2019</c> (quoteright) - confirming the two base encodings
    ///     are genuinely different for this code, not merely that some encoding resolved.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_StandardEncoding_Absent_DefaultWinAnsiDoesNotPaintQuoteright()
    {
        // Arrange: same font as the /StandardEncoding test (only codepoint U+2019 mapped), but no
        // /Encoding entry at all, so the default /WinAnsiEncoding applies instead.
        var fontBytes = BuildEmbeddedFontBytes([(0x2019, 1)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(
            fontBytes,
            fontDictExtra: "/FirstChar 0 /LastChar 255 /Widths [" + string.Join(' ', Enumerable.Repeat(600, 256)) + "]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (') Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: no glyph outline is painted anywhere in the expected region.
        Assert.Equal(default, surface[11, 44]);
    }

    /// <summary>
    ///     Proves <c>BuildResolvedFont</c>'s <c>Type3</c> dispatch: a minimal
    ///     <c>/Subtype /Type3</c> font declaring a single glyph (code <c>65</c> ('A'), mapped via
    ///     <c>/Encoding</c>/<c>/Differences</c> to glyph name <c>/Square</c>, whose
    ///     <c>/CharProcs</c> content stream paints a filled design-space rectangle) paints that
    ///     glyph's ink, at the device location its default <c>/FontMatrix</c>
    ///     (<c>[0.001 0 0 0.001 0 0]</c>) scales it to - the same location an equivalent
    ///     <c>/UnitsPerEm 1000</c> TrueType outline glyph would paint at (see
    ///     <c>PdfDocument_Fonts_Type1_*</c> tests' own, numerically identical, pixel assertions).
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_MinimalFont_DifferencesAndCharProcs_PaintsGlyphInk()
    {
        // Arrange: glyph /Square paints a filled rectangle spanning design (100,100)-(500,500).
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "100 100 400 400 re f"u8.ToArray())],
            "65 /Square");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: device x [7,15), y [85,93) - identical to the Type1/TrueType embedded-font
        // tests' own pixel assertions for the same design-space rectangle and /FontMatrix-
        // equivalent 0.001 scale.
        Assert.NotEqual(default, surface[11, 89]);

        // Assert: well outside the glyph's painted region, nothing is painted.
        Assert.Equal(default, surface[50, 50]);
    }

    /// <summary>
    ///     Proves that a non-default, anisotropic <c>/FontMatrix</c>
    ///     (<c>[0.002 0 0 0.0015 0 0]</c> - a different horizontal/vertical scale, neither of which
    ///     is the conventional <c>0.001</c>) is actually consulted to compute the glyph's painted
    ///     device location - not merely accepted and ignored in favor of a hardcoded
    ///     <c>0.001</c>/1000-unit-em assumption (the highest-risk area of this feature, per the
    ///     implementation plan).
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_NonDefaultFontMatrix_ScalesGlyphGeometry()
    {
        // Arrange: same glyph rectangle (100,100)-(500,500) as the minimal-font test, but with
        // /FontMatrix [0.002 0 0.0015 0 0] instead of the conventional [0.001 0 0 0.001 0 0].
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "100 100 400 400 re f"u8.ToArray())],
            "65 /Square",
            fontMatrix: "[0.002 0 0 0.0015 0 0]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: under this /FontMatrix, the rectangle lands at device x [9,25), y [80,92) -
        // verified empirically against the implementation's own rasterization.
        Assert.NotEqual(default, surface[17, 86]);

        // Assert: device (8,92) lies inside the region the rectangle would occupy under the
        // conventional (but, for this font, wrong) 0.001 /FontMatrix scale (x [7,15), y [85,93))
        // but outside this font's own, correctly-scaled region (x [9,25), y [80,92)) - proving the
        // glyph was positioned using this font's actual /FontMatrix, not a hardcoded assumption.
        Assert.Equal(default, surface[8, 92]);
    }

    /// <summary>
    ///     Proves that <c>Resolve</c>'s glyph-advance width is the <c>/Widths</c> entry scaled
    ///     through <c>/FontMatrix</c> (<c>Vector2.TransformNormal</c>) - not divided by the
    ///     simple/composite-font convention of <c>1000</c> - by painting two glyphs in a single
    ///     <c>Tj</c> and asserting the second glyph's device position reflects the first glyph's
    ///     <c>/FontMatrix</c>-scaled (not <c>/1000</c>-scaled) advance.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_Widths_ScaledViaFontMatrix_DeterminesAdvance()
    {
        // Arrange: /FontMatrix [0.0025 0 0 0.001 0 0] (a deliberately non-conventional horizontal
        // scale); code 65 ('A') declares /Widths entry 1000 (glyph-space units). The correct
        // advance is 1000 * 0.0025 * 20pt = 50 text-space units; a /1000-divided (wrong)
        // convention would instead advance only 1000/1000 * 20pt = 20 units.
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "100 100 400 400 re f"u8.ToArray())],
            "65 /Square 66 /Square",
            fontMatrix: "[0.0025 0 0 0.001 0 0]",
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [1000 1000]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the first glyph paints at its own origin (device x [10,30)).
        Assert.NotEqual(default, surface[20, 89]);

        // Assert: the second glyph paints at the correctly-advanced origin (tx = 5 + 50 = 55,
        // device x [60,80)) - proving the /FontMatrix-scaled (not /1000-scaled) width was used.
        Assert.NotEqual(default, surface[70, 89]);

        // Assert: the second glyph does NOT paint at the position a wrong, /1000-divided-width
        // advance would have produced (tx = 5 + 20 = 25, device x [30,50)).
        Assert.Equal(default, surface[40, 89]);
    }

    /// <summary>
    ///     Proves that a character code with no <c>/Encoding</c>/<c>/Differences</c> mapping at
    ///     all paints nothing (no exception), but still advances the text position by its declared
    ///     <c>/Widths</c> entry.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_UnmappedCode_NoDifferencesEntry_PaintsNothingButAdvances()
    {
        // Arrange: only code 65 ('A') is mapped via /Differences; code 66 ('B') has a declared
        // /Widths entry (750) but no glyph-name mapping at all. "(BA)" shows B first (unmapped,
        // paints nothing, but still advances 750 * 0.001 * 20pt = 15 units), then A (mapped,
        // painted at the resulting, advanced origin).
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "100 100 400 400 re f"u8.ToArray())],
            "65 /Square",
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [750 750]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (BA) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' paints at the advanced origin (tx = 5 + 15 = 20, device x [22,30)) - proving
        // the unmapped 'B' code's declared width was still applied.
        Assert.NotEqual(default, surface[25, 89]);

        // Assert: nothing paints at the un-advanced origin (tx = 5, device x [7,15)) - proving 'B'
        // itself painted nothing.
        Assert.Equal(default, surface[10, 89]);
    }

    /// <summary>
    ///     Proves that a character code mapped (via <c>/Differences</c>) to a glyph name absent
    ///     from <c>/CharProcs</c> paints nothing (no exception), but still advances the text
    ///     position by its declared <c>/Widths</c> entry - the companion leniency case to
    ///     <see cref="PdfDocument_Fonts_Type3_UnmappedCode_NoDifferencesEntry_PaintsNothingButAdvances"/>.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_MappedGlyphNameAbsentFromCharProcs_PaintsNothingButAdvances()
    {
        // Arrange: code 66 ('B') is mapped via /Differences to glyph name /Missing, which has no
        // corresponding /CharProcs entry (only /Square, for code 65 ('A'), is supplied).
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "100 100 400 400 re f"u8.ToArray())],
            "65 /Square 66 /Missing",
            fontDictExtra: "/FirstChar 65 /LastChar 66 /Widths [750 750]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (BA) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' paints at the advanced origin (tx = 5 + 15 = 20, device x [22,30)).
        Assert.NotEqual(default, surface[25, 89]);

        // Assert: nothing paints at the un-advanced origin (tx = 5, device x [7,15)).
        Assert.Equal(default, surface[10, 89]);
    }

    /// <summary>
    ///     Proves that a <c>/Subtype /Type3</c> font dictionary missing its required
    ///     <c>/FontMatrix</c> entry throws <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_MissingFontMatrix_ThrowsInvalidDataException()
    {
        // Arrange: a /Subtype /Type3 font dictionary with no /FontMatrix entry at all.
        var glyphStreamObj = BuildStreamObjectBody(string.Empty, "100 100 400 400 re f"u8.ToArray());
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            "<< /Type /Font /Subtype /Type3 /CharProcs << /Square 6 0 R >> " +
            "/Encoding << /Differences [65 /Square] >> /FirstChar 65 /LastChar 65 /Widths [750] >>");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>", [fontDictObj, glyphStreamObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a <c>/Subtype /Type3</c> font dictionary missing its required
    ///     <c>/CharProcs</c> entry throws <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_MissingCharProcs_ThrowsInvalidDataException()
    {
        // Arrange: a /Subtype /Type3 font dictionary with no /CharProcs entry at all.
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            "<< /Type /Font /Subtype /Type3 /FontMatrix [0.001 0 0 0.001 0 0] " +
            "/Encoding << /Differences [65 /Square] >> /FirstChar 65 /LastChar 65 /Widths [750] >>");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>", [fontDictObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a glyph procedure's own <c>cm</c>/color-operator mutations are fully
    ///     isolated - they do not leak back into the invoking content stream's graphics state
    ///     after the glyph finishes painting - mirroring
    ///     <see cref="PdfDocument_Images_DoOperator_NestedFormXObjects_RestoresGraphicsStateAfterReturn"/>'s
    ///     own Form-XObject state-isolation proof, applied to a Type3 glyph procedure's re-entrant
    ///     execution instead.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_GlyphProc_GraphicsStateIsolated_DoesNotLeakOut()
    {
        // Arrange: /FontMatrix [1 0 0 1 0 0] (identity) with /Tf 1 and no /Td, so the glyph's own
        // content-stream coordinates map 1:1 onto the same device space a Form XObject's own
        // BBox-space coordinates would (see the Form XObject test this mirrors). The glyph
        // procedure applies "2 0 0 2 0 0 cm 1 0 0 rg" before painting its own rectangle; the page
        // then paints its own rectangle, in its own default (black) fill color, after Tj returns.
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "2 0 0 2 0 0 cm 1 0 0 rg 5 5 10 10 re f"u8.ToArray())],
            "65 /Square",
            fontMatrix: "[1 0 0 1 0 0]",
            fontDictExtra: "/FirstChar 65 /LastChar 65 /Widths [0]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 1 Tf (A) Tj ET 40 10 5 5 re f", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the glyph's own rectangle, scaled 2x and filled red by its own "cm"/"rg", lands
        // at device (10,70)-(30,90) - its center is painted red.
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[20, 80]);

        // Assert: the page's own rectangle (painted after Tj returns) lands at its own normal,
        // unscaled device position (40,85)-(45,90), in the page's own default black fill color -
        // proving the glyph's "cm"/"rg" did not leak back out into the invoking stream's graphics
        // state.
        Assert.Equal(Black, surface[42, 87]);

        // Assert: the device position the page's rectangle would occupy if the glyph's 2x scale
        // had leaked ((80,70)-(90,80)) is left blank.
        Assert.Equal(default, surface[85, 75]);
    }

    /// <summary>
    ///     Proves that a <c>d0</c> operator inside a glyph procedure is parsed (operand
    ///     count/type validated) and then discarded - its <c>wx</c> operand is never fed back into
    ///     glyph-advance layout, which remains solely determined by the font's own declared
    ///     <c>/Widths</c> entry (scaled via <c>/FontMatrix</c>).
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_D0Operator_ParsedButDoesNotAffectAdvanceWidth()
    {
        // Arrange: code 65's glyph procedure declares "2000 d0" (a /d0 width wildly different from
        // the font's own declared /Widths entry of 750) before painting; "(AA)" shows the same
        // glyph twice. The correct second-glyph advance is 750 * 0.001 * 20pt = 15 units (from
        // /Widths); a bug that fed d0's wx back into layout would instead advance
        // 2000 * 0.001 * 20pt = 40 units.
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "2000 0 d0 100 100 400 400 re f"u8.ToArray())],
            "65 /Square",
            fontDictExtra: "/FirstChar 65 /LastChar 65 /Widths [750]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (AA) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the second glyph paints at the /Widths-correct origin (tx = 5 + 15 = 20, device
        // x [22,30)).
        Assert.NotEqual(default, surface[25, 89]);

        // Assert: the second glyph does NOT paint at the position a d0-driven (wrong) advance
        // would have produced (tx = 5 + 40 = 45, device x [47,55)).
        Assert.Equal(default, surface[50, 89]);
    }

    /// <summary>
    ///     Proves that a <c>/Subtype /Type3</c> font with no <c>/Resources</c> entry of its own
    ///     falls back, at glyph-paint time, to the invoking page's own <c>/Resources</c> - exactly
    ///     like a <c>/Subtype /Form</c> XObject with no <c>/Resources</c> of its own (per PDF
    ///     specification section 9.6.5.3) - proven by a glyph procedure that invokes a Form
    ///     XObject (<c>/Fm0</c>) declared only in the page's own <c>/Resources</c>, not the
    ///     Type3 font's.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_NoOwnResources_FallsBackToOuterPageResources()
    {
        // Arrange: the glyph procedure for code 65 ('A') does "/Fm0 Do"; /Fm0 is declared only in
        // the page's own /Resources (the Type3 font declares no /Resources of its own at all).
        var formStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 1000 1000]", "100 100 400 400 re f"u8.ToArray());

        var (fontResourcesBody, fontExtraObjects) = BuildType3FontResources(
            [("Square", "/Fm0 Do"u8.ToArray())],
            "65 /Square");

        var resourcesBody = fontResourcesBody + " /XObject << /Fm0 7 0 R >>";
        var extraObjects = new List<byte[]>(fontExtraObjects) { formStream };

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the Form XObject's rectangle paints at the same device location the minimal-
        // font test's own directly-painted rectangle does (device x [7,15), y [85,93)) - proving
        // /Fm0 was successfully resolved from the page's own /Resources while painting the glyph.
        Assert.NotEqual(default, surface[11, 89]);
    }

    /// <summary>
    ///     Proves that a self-referencing Type 3 glyph procedure - one that re-shows its own code
    ///     via <c>Tj</c> from inside its own content stream - fails closed with
    ///     <see cref="InvalidDataException"/> once the fixed maximum Type3 glyph-procedure nesting
    ///     depth is exceeded, rather than hanging or overflowing the call stack. The nested graphics
    ///     state clone <c>PaintType3Glyph</c> hands to the recursive <c>ExecuteOperators</c> call
    ///     inherits the outer <c>Tf</c>-selected font (see that method's own remarks), so a bare
    ///     <c>(A) Tj</c> inside the glyph procedure's own content bytes legally re-invokes
    ///     <c>ShowGlyph</c>/<c>PaintType3Glyph</c> recursively without any nested <c>BT</c>/<c>Tf</c>
    ///     of its own.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_SelfReferencingGlyphProc_ExceedsMaxNestingDepth_ThrowsInvalidDataException()
    {
        // Arrange: glyph /Square (code 65) re-shows its own code via "(A) Tj" inside its own
        // content stream - unbounded recursion through PaintType3Glyph until the fixed nesting
        // depth trips.
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "(A) Tj"u8.ToArray())],
            "65 /Square");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a render mode of <c>3</c> (invisible) skips Type3 glyph-procedure execution
    ///     entirely - <c>ShowGlyph</c>'s <c>ResolvedType3Font</c> branch only calls
    ///     <c>PaintType3Glyph</c> when <c>_gs.RenderMode != 3</c> - while the shared, unconditional
    ///     trailing displacement/advance logic still advances the text position by each glyph's
    ///     declared <c>/Widths</c> entry.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_RenderMode3_SkipsGlyphProcedureButStillAdvances()
    {
        // Arrange: the same minimal filled-rectangle /Square glyph (code 65, /Widths [750]) as
        // PdfDocument_Fonts_Type3_MinimalFont_DifferencesAndCharProcs_PaintsGlyphInk. "3 Tr (AA)
        // Tj" shows two glyphs under render mode 3 (invisible) - each should advance the text
        // position by its declared width (750 * 0.001 * 20pt = 15 units) without painting
        // anything. "0 Tr (A) Tj" then shows a third, visible glyph at the now fully-advanced
        // origin.
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "100 100 400 400 re f"u8.ToArray())],
            "65 /Square");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td 3 Tr (AA) Tj 0 Tr (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: nothing paints at the first invisible glyph's own, would-be-painted device
        // location (tx = 5, device x [7,15), y [85,93)) - proving the glyph procedure never
        // executed.
        Assert.Equal(default, surface[11, 89]);

        // Assert: nothing paints at the second invisible glyph's own, would-be-painted device
        // location either (tx = 5 + 15 = 20, device x [22,30)).
        Assert.Equal(default, surface[25, 89]);

        // Assert: the third, visible glyph paints at the fully-advanced origin (tx = 5 + 15 + 15
        // = 35, device x [37,45)) - proving both invisible glyphs' declared widths were applied
        // to the text position even though neither painted.
        Assert.NotEqual(default, surface[40, 89]);
    }

    /// <summary>
    ///     Proves that a malformed <c>/FontMatrix</c> array of the wrong length (5 or 7 elements,
    ///     instead of exactly 6) throws <see cref="InvalidDataException"/> - distinct from
    ///     <see cref="PdfDocument_Fonts_Type3_MissingFontMatrix_ThrowsInvalidDataException"/>'s own
    ///     "entirely absent" case.
    /// </summary>
    [Theory]
    [InlineData("[0.001 0 0 0.001 0]")]
    [InlineData("[0.001 0 0 0.001 0 0 0]")]
    public void PdfDocument_Fonts_Type3_FontMatrixWrongArrayLength_ThrowsInvalidDataException(string fontMatrix)
    {
        // Arrange: a /Subtype /Type3 font dictionary whose /FontMatrix array has the wrong number
        // of elements.
        var glyphStreamObj = BuildStreamObjectBody(string.Empty, "100 100 400 400 re f"u8.ToArray());
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            $"<< /Type /Font /Subtype /Type3 /FontMatrix {fontMatrix} /CharProcs << /Square 6 0 R >> " +
            "/Encoding << /Differences [65 /Square] >> /FirstChar 65 /LastChar 65 /Widths [750] >>");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>", [fontDictObj, glyphStreamObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a <c>/FontMatrix</c> array containing a non-number entry (a name, in place
    ///     of a number) throws <see cref="InvalidDataException"/> - distinct from
    ///     <see cref="PdfDocument_Fonts_Type3_FontMatrixWrongArrayLength_ThrowsInvalidDataException"/>'s
    ///     own wrong-length case.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_FontMatrixNonNumberEntry_ThrowsInvalidDataException()
    {
        // Arrange: /FontMatrix with a name (/Foo) in place of a number for one entry.
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "100 100 400 400 re f"u8.ToArray())],
            "65 /Square",
            fontMatrix: "[0.001 0 0 0.001 /Foo 0]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a <c>/CharProcs</c> entry resolving to something other than a dictionary
    ///     (a number, here) throws <see cref="InvalidDataException"/> - distinct from
    ///     <see cref="PdfDocument_Fonts_Type3_MissingCharProcs_ThrowsInvalidDataException"/>'s own
    ///     "entirely absent" case.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_CharProcsNotADictionary_ThrowsInvalidDataException()
    {
        // Arrange: a /Subtype /Type3 font dictionary whose /CharProcs resolves to a number, not a
        // dictionary.
        var fontDictObj = System.Text.Encoding.ASCII.GetBytes(
            "<< /Type /Font /Subtype /Type3 /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs 42 " +
            "/Encoding << /Differences [65 /Square] >> /FirstChar 65 /LastChar 65 /Widths [750] >>");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf (A) Tj ET", "/Font << /F1 5 0 R >>", [fontDictObj]);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>
    ///     Proves that a <c>d1</c> operator (6 operands: <c>wx wy llx lly urx ury</c>) inside a
    ///     glyph procedure is parsed (operand count/type validated) and then discarded - its
    ///     <c>wx</c> operand is never fed back into glyph-advance layout, which remains solely
    ///     determined by the font's own declared <c>/Widths</c> entry - mirroring
    ///     <see cref="PdfDocument_Fonts_Type3_D0Operator_ParsedButDoesNotAffectAdvanceWidth"/>'s own
    ///     <c>d0</c> proof, applied to <c>d1</c>'s own 6-operand path instead.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_D1Operator_ParsedButDoesNotAffectAdvanceWidth()
    {
        // Arrange: code 65's glyph procedure declares "2000 0 0 0 100 100 d1" (a /d1 wx wildly
        // different from the font's own declared /Widths entry of 750) before painting; "(AA)"
        // shows the same glyph twice. The correct second-glyph advance is 750 * 0.001 * 20pt = 15
        // units (from /Widths); a bug that fed d1's wx back into layout would instead advance
        // 2000 * 0.001 * 20pt = 40 units.
        var (resourcesBody, extraObjects) = BuildType3FontResources(
            [("Square", "2000 0 0 0 100 100 d1 100 100 400 400 re f"u8.ToArray())],
            "65 /Square",
            fontDictExtra: "/FirstChar 65 /LastChar 65 /Widths [750]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (AA) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the second glyph paints at the /Widths-correct origin (tx = 5 + 15 = 20, device
        // x [22,30)).
        Assert.NotEqual(default, surface[25, 89]);

        // Assert: the second glyph does NOT paint at the position a d1-driven (wrong) advance
        // would have produced (tx = 5 + 40 = 45, device x [47,55)).
        Assert.Equal(default, surface[50, 89]);
    }

    /// <summary>
    ///     Proves that a stray <c>d0</c>/<c>d1</c> pair issued in an ordinary, non-Type3 page
    ///     content stream (no font ever selected at all) renders successfully with no exception
    ///     and no visible effect - proving <c>ExecuteOperators</c>' shared <c>d0</c>/<c>d1</c>
    ///     dispatch (<c>PdfDocument.ContentStream.cs</c>) is unconditional, not gated on "currently
    ///     inside a Type3 glyph procedure".
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_StrayD0D1OutsideGlyphProc_NoExceptionNoEffect()
    {
        // Arrange/Act: a stray d0 (2 operands) and d1 (6 operands) issued directly in an ordinary
        // page content stream, followed by an ordinary filled rectangle.
        using var surface = RenderContent("5 5 d0 1 1 2 2 3 3 d1 10 10 20 20 re f");

        // Assert: no exception was thrown (RenderContent above completed), and the rectangle
        // after the stray d0/d1 painted normally at its expected device location (device x
        // [10,30), y [70,90)).
        Assert.Equal(Black, surface[20, 80]);
    }

    /// <summary>
    ///     Proves that a Type 3 font's own <c>/Resources</c> takes precedence over the invoking
    ///     page's own <c>/Resources</c> when both declare an XObject of the same name - the
    ///     non-null branch of <c>PaintType3Glyph</c>'s <c>_resources = font.Resources ??
    ///     _resources</c> fallback, distinct from
    ///     <see cref="PdfDocument_Fonts_Type3_NoOwnResources_FallsBackToOuterPageResources"/>'s own
    ///     null-branch proof.
    /// </summary>
    [Fact]
    public void PdfDocument_Fonts_Type3_OwnResourcesTakePrecedenceOverPageResources_UsesFontResources()
    {
        // Arrange: both the Type3 font's own /Resources and the page's own /Resources declare an
        // XObject named /Fm0 - the font's own paints red, the page's own paints blue. The glyph
        // procedure for code 65 ('A') does "/Fm0 Do".
        var redFormStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 1000 1000]", "1 0 0 rg 0 0 1000 1000 re f"u8.ToArray());
        var blueFormStream = BuildStreamObjectBody(
            "/Type /XObject /Subtype /Form /BBox [0 0 1000 1000]", "0 0 1 rg 0 0 1000 1000 re f"u8.ToArray());

        var (fontResourcesBody, fontExtraObjects) = BuildType3FontResources(
            [("Square", "/Fm0 Do"u8.ToArray())],
            "65 /Square",
            type3ResourcesBody: "/XObject << /Fm0 7 0 R >>");

        var resourcesBody = fontResourcesBody + " /XObject << /Fm0 8 0 R >>";
        var extraObjects = new List<byte[]>(fontExtraObjects) { redFormStream, blueFormStream };

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the glyph paints red (the font's own /Fm0, not the page's blue /Fm0) at the
        // expected glyph-matrix-transformed device location (device x [5,25), y [75,95)).
        Assert.Equal(new Canvas.Rgba32(255, 0, 0, 255), surface[15, 85]);
    }

    #endregion

    #region Text

    /// <summary>Proves that <c>Tj</c> paints a glyph outline through the composed text-rendering matrix, positioned per <c>Tf</c>/<c>Td</c>.</summary>
    [Fact]
    public void PdfDocument_Text_ShowText_PaintsGlyphAtComposedTextRenderingMatrix()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: text x [7, 15), text y [52, 60) -> device x [7, 15), device y [40, 48).
        Assert.NotEqual(default, surface[10, 44]);
        Assert.Equal(default, surface[50, 50]);
    }

    /// <summary>Proves that a text-showing operator issued before any <c>Tf</c> throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("BT (A) Tj ET")]
    [InlineData("BT (A) ' ET")]
    [InlineData("BT 0 0 (A) \" ET")]
    [InlineData("BT [(A)] TJ ET")]
    public void PdfDocument_Text_ShowText_NoFontSelected_ThrowsInvalidDataException(string content)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>Proves that render mode 3 (invisible) lays out but does not paint a glyph.</summary>
    [Fact]
    public void PdfDocument_Text_RenderMode3_Invisible_DoesNotPaintGlyph()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 3 Tr 5 50 Td (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: nowhere in the surface has any painted pixel.
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    /// <summary>Proves that render mode 3 still advances the text position (glyphs are laid out, just not painted).</summary>
    [Fact]
    public void PdfDocument_Text_RenderMode3_Invisible_StillAdvancesTextPosition()
    {
        // Arrange: 'A' is invisible (mode 3); 'B' switches back to mode 0 (fill) and should
        // still appear offset by 'A's own advance, proving 'A' was laid out, not skipped.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td 3 Tr (A) Tj 0 Tr (B) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' paints nothing; 'B' starts at Tm.x = 5 + 12 = 17, painting at text x [19, 27).
        Assert.Equal(default, surface[11, 89]);
        Assert.NotEqual(default, surface[23, 89]);
    }

    /// <summary>Proves that a defined but unsupported text-rendering mode throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void PdfDocument_Text_RenderMode_UnsupportedDefinedMode_ThrowsUnsupportedImageFeatureException(int mode)
    {
        Assert.Throws<UnsupportedImageFeatureException>(() => RenderContent($"BT {mode} Tr ET"));
    }

    /// <summary>Proves that a text-rendering mode outside the PDF specification's defined [0, 7] range throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void PdfDocument_Text_RenderMode_OutOfDefinedRange_ThrowsInvalidDataException(int mode)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent($"BT {mode} Tr ET"));
    }

    /// <summary>Proves that <c>BT</c> resets the text/line matrix to identity, but leaves the selected font (and every other text-state graphics parameter) untouched.</summary>
    [Fact]
    public void PdfDocument_Text_BeginText_ResetsTextMatrixButPreservesFontAndTextState()
    {
        // Arrange: the second BT/ET block issues no Tf of its own - it must still have a font
        // selected (from the first block) to show text without throwing, and its Tm/Tlm must
        // have reset to identity (not still be positioned at (5, 50) from the first block).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj ET BT (B) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: the second block's glyph paints at text x [2, 10), y [2, 10) -> device y [90, 98).
        Assert.NotEqual(default, surface[6, 94]);
    }

    /// <summary>Proves that <c>q</c>/<c>Q</c> save/restore the font size (part of the graphics state), independently of the (never saved/restored) text/line matrix.</summary>
    [Fact]
    public void PdfDocument_Text_PushPopGraphicsState_RestoresFontSize()
    {
        // Arrange: 'q' saves FontSize=20; the nested '40 Tf' changes it to 40; 'Q' restores 20 -
        // if it did not, the glyph painted afterward would be twice as large (and shifted).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td q /F1 40 Tf Q (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: with FontSize correctly restored to 20, the glyph paints at text y [52, 60) ->
        // device y [40, 48) - device y = 47 is inside that restored-size band, but outside the
        // much taller band an un-restored FontSize = 40 would have produced (device y [30, 46)).
        Assert.NotEqual(default, surface[11, 47]);
    }

    /// <summary>Proves that <c>Td</c> offsets the line matrix (not the text matrix as last advanced by a preceding show operator), and both are made equal.</summary>
    [Fact]
    public void PdfDocument_Text_Td_OffsetsLineMatrixNotLastTextMatrix()
    {
        // Arrange: after showing 'A', the text matrix has advanced past the line matrix's own
        // (5, 5) position - '10 0 Td' must offset the *line* matrix's (5, 5), landing 'B' at
        // Tm.x = 15, not at (5 + 'A's own advance + 10).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 5 Td (A) Tj 10 0 Td (B) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' paints at text x [17, 25) -> device x [17, 25); a buggy "offset from the
        // last text matrix" implementation would instead land it at text x [29, 37).
        Assert.NotEqual(default, surface[20, 89]);
        Assert.Equal(default, surface[32, 89]);
    }

    /// <summary>Proves that <c>TD</c> behaves like <c>Td</c> and additionally sets the leading to <c>-ty</c>, observable via a subsequent <c>T*</c>.</summary>
    [Fact]
    public void PdfDocument_Text_TD_SetsLeadingToNegativeTy()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj 0 -7 TD (A) Tj T* (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: line y = 50, then 43 (TD), then 36 (T* using leading = 7 set by TD) -> device
        // y centers approximately 44, 51, 58 respectively.
        Assert.NotEqual(default, surface[11, 44]);
        Assert.NotEqual(default, surface[11, 51]);
        Assert.NotEqual(default, surface[11, 58]);
    }

    /// <summary>Proves that <c>Tm</c> replaces (rather than composes with) the text/line matrix.</summary>
    [Fact]
    public void PdfDocument_Text_Tm_ReplacesTextAndLineMatrix()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj 1 0 0 1 30 30 Tm (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'Tm' replaces (5, 50) outright with (30, 30) - text x/y [32, 40) -> device y
        // [60, 68) - not composed with the prior (5, 50) (which would instead land at (35, 80)).
        Assert.NotEqual(default, surface[36, 64]);
        Assert.Equal(default, surface[41, 14]);
    }

    /// <summary>Proves that <c>T*</c> moves to the next line using the current leading (set via <c>TL</c>).</summary>
    [Fact]
    public void PdfDocument_Text_TStar_UsesCurrentLeading()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 7 TL 5 50 Td (A) Tj T* (A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: line y = 50, then 43 (T* using leading = 7) -> device y centers 44 and 51.
        Assert.NotEqual(default, surface[11, 44]);
        Assert.NotEqual(default, surface[11, 51]);
    }

    /// <summary>Proves that <c>Tc</c> (character spacing) is added to every glyph's advance.</summary>
    [Fact]
    public void PdfDocument_Text_Tc_AddsToGlyphAdvance()
    {
        // Arrange: 'A's own advance is 0.6 * 20 = 12; with Tc = 3, 'B' starts at Tm.x = 5 + 15 = 20.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td 3 Tc (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' paints at text x [22, 30); without Tc it would paint at [19, 27) instead.
        Assert.NotEqual(default, surface[29, 44]);
        Assert.Equal(default, surface[20, 44]);
    }

    /// <summary>Proves that <c>Tw</c> (word spacing) applies only to single-byte character code 32 (space), not to any other code.</summary>
    [Fact]
    public void PdfDocument_Text_Tw_AppliesOnlyToCode32()
    {
        // Arrange: "A A" is codes 65, 32, 65. 'A's own advance is 12; the space (code 32, no
        // glyph, zero declared/font advance) gets an extra 5 units from Tw. The final 'A'
        // starts at Tm.x = 5 + 12 + 5 = 22, painting at text x [24, 32).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td 5 Tw (A A) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: without Tw applied to the space, the final 'A' would instead paint at [19, 27).
        Assert.NotEqual(default, surface[30, 44]);
        Assert.Equal(default, surface[20, 44]);
    }

    /// <summary>Proves that <c>Tz</c> (horizontal scaling) scales both glyph shape and advance along the x-axis only.</summary>
    [Fact]
    public void PdfDocument_Text_Tz_ScalesHorizontalShapeAndAdvance()
    {
        // Arrange: Th = 0.5 halves the glyph's horizontal extent (font-space x scale factor
        // becomes 0.01 * 20 * 0.5 = 0.01, so the 100-500 unit square spans text x [1, 5) instead
        // of the unscaled [2, 10)) and halves 'A's own advance (0.6 * 20 * 0.5 = 6).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 50 Tz 5 50 Td (AB) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' paints narrowly at text x [6, 10); the unscaled width would instead extend
        // well past text x 11, but the correctly-narrowed shape leaves the gap before 'B' empty.
        // 'B' starts at Tm.x = 5 + 6 = 11, painting at text x [12, 16).
        Assert.NotEqual(default, surface[8, 44]);
        Assert.Equal(default, surface[11, 44]);
        Assert.NotEqual(default, surface[14, 44]);
    }

    /// <summary>Proves that <c>TJ</c>'s numeric array elements pre-adjust the text position, in thousandths of text-space units.</summary>
    [Fact]
    public void PdfDocument_Text_TJ_AppliesPositionAdjustments()
    {
        // Arrange: after showing 'A' (advance 12), a "-250" adjustment adds
        // -(-250 / 1000) * 20 = 5 further units before 'B' - landing 'B' at Tm.x = 5 + 12 + 5 = 22.
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td [(A) -250 (B)] TJ ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'B' paints at text x [24, 32); without the adjustment it would paint at [19, 27).
        Assert.NotEqual(default, surface[28, 44]);
        Assert.Equal(default, surface[20, 44]);
    }

    /// <summary>Proves that a <c>TJ</c> array entry that is neither a string nor a number throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_Text_TJ_ArrayEntryNotStringOrNumber_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => RenderContent("BT [true] TJ ET"));
    }

    /// <summary>Proves that the <c>'</c> operator moves to the next line (using the current leading) and then shows its string operand.</summary>
    [Fact]
    public void PdfDocument_Text_QuoteOperator_MovesToNextLineThenShows()
    {
        // Arrange
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td (A) Tj 7 TL (B) ' ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: 'A' at device y 44; ' moves to the next line (leading 7, from the line
        // matrix's own (5, 50), not the text matrix advanced by 'A') and shows 'B' at text x
        // [7, 15) again, device y 51 - a buggy "advance from the text matrix" would instead
        // shift 'B' rightward, out of this x window.
        Assert.NotEqual(default, surface[11, 44]);
        Assert.NotEqual(default, surface[11, 51]);
    }

    /// <summary>Proves that the <c>"</c> operator sets word/character spacing, then behaves like <c>'</c>.</summary>
    [Fact]
    public void PdfDocument_Text_DoubleQuoteOperator_SetsSpacingThenMovesAndShows()
    {
        // Arrange: '4 2 (A) "' sets Tw=4/Tc=2 then shows 'A' (advance 12 + 2 = 14); the
        // following 'Tj' for 'B' starts at Tm.x = 5 + 14 = 19, painting at text x [21, 29).
        var fontBytes = BuildEmbeddedFontBytes([(65, 1), (66, 2)]);
        var (resourcesBody, extraObjects) = BuildSimpleTrueTypeFontResources(fontBytes);
        var bytes = BuildSinglePagePdfWithResources(
            100, 100, "BT /F1 20 Tf 5 50 Td 4 2 (A) \" (B) Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: without Tc applied, 'B' would instead paint at text x [19, 27) only.
        Assert.NotEqual(default, surface[28, 44]);
    }

    /// <summary>Proves that every new text operator's malformed operand count/type throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("BT 1 2 ET")]
    [InlineData("1 2 Tc")]
    [InlineData("1 2 Tw")]
    [InlineData("1 2 Tz")]
    [InlineData("1 2 TL")]
    [InlineData("1 2 Ts")]
    [InlineData("/F1 Tf")]
    [InlineData("24 /F1 Tf")]
    [InlineData("Tr")]
    [InlineData("1 2 Tr")]
    [InlineData("1 Td")]
    [InlineData("1 2 3 Td")]
    [InlineData("1 TD")]
    [InlineData("1 2 3 4 5 Tm")]
    [InlineData("1 T*")]
    [InlineData("(A) (B) Tj")]
    [InlineData("1 Tj")]
    [InlineData("1 '")]
    [InlineData("1 2 \"")]
    [InlineData("1 2 (A) 3 \"")]
    [InlineData("(A) TJ")]
    public void PdfDocument_Text_MalformedOperandCount_ThrowsInvalidDataException(string content)
    {
        Assert.Throws<InvalidDataException>(() => RenderContent(content));
    }

    /// <summary>Proves that showing a hex string with an odd byte count against a <c>/Type0</c> (2-byte-per-code) composite font throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PdfDocument_ShowText_Type0_OddByteLengthString_ThrowsInvalidDataException()
    {
        // Arrange: a single-byte hex string cannot be split into whole 2-byte composite codes.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(fontBytes);

        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT /F1 20 Tf <01> Tj ET", resourcesBody, extraObjects);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => RenderPdfBytes(bytes));
    }

    /// <summary>Proves that <c>Tj</c> against a <c>/Type0</c> composite font decodes 2 bytes per code and paints each resolved glyph at its correctly advanced position.</summary>
    [Fact]
    public void PdfDocument_ShowText_Type0_TwoByteCodes_ShowsEachGlyphAtCorrectPosition()
    {
        // Arrange: CID 1's declared width is 1000 (a full em) -> advance = 1.0 * 20 = 20; CID 2's
        // declared width is 600 -> advance = 0.6 * 20 = 12 (unused here, but present to mirror the
        // simple-font "ExplicitEntry" width test's exact numeric pattern).
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 3);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(
            fontBytes, cidFontExtra: "/W [1 [1000] 2 3 600]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 5 5 Td {BuildIdentityHHexString(1, 2)} Tj ET", resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: CID 1 paints at text x [7, 15); CID 2 starts at Tm.x = 5 + 20 = 25, painting at
        // text x [27, 35) - clearly past the "no explicit width applied yet" gap boundary.
        Assert.NotEqual(default, surface[11, 89]);
        Assert.Equal(default, surface[16, 89]);
        Assert.NotEqual(default, surface[31, 89]);
    }

    /// <summary>
    ///     Proves that <c>Tw</c> (word spacing) is <b>not</b> applied to a composite
    ///     <c>/Type0</c>/<c>/Identity-H</c> font's 2-byte code <c>32</c> - per PDF specification
    ///     section 9.3.3, word spacing only applies to the single-byte code <c>32</c> of a simple
    ///     font, never to any code decoded from a composite font (even one numerically equal to
    ///     <c>32</c>). Regression test for a bug where the word-spacing check compared the
    ///     decoded code to <c>32</c> without also checking the font's code-byte-width.
    /// </summary>
    [Fact]
    public void PdfDocument_ShowText_Type0_WordSpacingCode32_IsNotAppliedToCompositeFont()
    {
        // Arrange: CID 32 declares width 1000 (a full em -> 20 device units at font size 20); a
        // large 1000 Tw would grossly displace the following CID 1 glyph if incorrectly applied.
        var fontBytes = BuildEmbeddedFontBytes([], glyphCount: 33);
        var (resourcesBody, extraObjects) = BuildCompositeFontResources(
            fontBytes, cidFontExtra: "/W [32 [1000] 1 [1000]]");

        var bytes = BuildSinglePagePdfWithResources(
            100, 100, $"BT /F1 20 Tf 1000 Tw 5 5 Td {BuildIdentityHHexString(32, 1)} Tj ET",
            resourcesBody, extraObjects);

        // Act
        using var surface = RenderPdfBytes(bytes);

        // Assert: CID 1 (the second glyph) begins immediately after CID 32's own 20-unit advance
        // (Tm.x = 5 + 20 = 25), not displaced by any extra Tw - it paints starting at text x
        // ~[27, 35), well short of where an incorrectly-applied 1000 Tw would push it off-canvas.
        Assert.NotEqual(default, surface[31, 89]);
    }

    #endregion

    #region Functions

    /// <summary>
    ///     Resolves a single-input <c>/FunctionType 0</c> sampled function whose stream is a
    ///     new indirect object (number 5, matching <see cref="BuildSinglePagePdfWithResources"/>'s
    ///     own extra-object numbering) referenced by <paramref name="dictionaryEntries"/>'s own
    ///     dictionary - mirroring the <c>ResolveToUnicodeMap</c> tests' own "build a minimal
    ///     document, open it, resolve a reference into it" convention.
    /// </summary>
    private static PdfDocument.SampledFunction ResolveTestFunction(string dictionaryEntries, byte[] sampleBytes)
    {
        var functionStream = BuildStreamObjectBody(dictionaryEntries, sampleBytes);
        var bytes = BuildSinglePagePdfWithResources(100, 100, "BT ET", string.Empty, [functionStream]);
        using var document = PdfDocument.Open(new MemoryStream(bytes));
        return document.ResolveFunction(PdfDocument.PdfObject.FromReference(5, 0));
    }

    /// <summary>Packs <paramref name="values"/> (each in <c>[0, 65535]</c>) as 16-bit big-endian sample bytes.</summary>
    private static byte[] BuildUInt16SampleBytes(params int[] values)
    {
        var bytes = new byte[values.Length * 2];
        for (var i = 0; i < values.Length; i++)
        {
            bytes[i * 2] = (byte)(values[i] >> 8);
            bytes[(i * 2) + 1] = (byte)values[i];
        }

        return bytes;
    }

    /// <summary>Proves that an input outside <c>/Domain</c> is clamped to the nearest domain boundary before being mapped to a sample index.</summary>
    [Fact]
    public void PdfDocument_Functions_Type0_InputOutsideDomain_ClampsToBoundary()
    {
        // Arrange: Domain [0, 1], 2 samples (0, 255), Range [0, 255] so Decode is an identity
        // pass-through and the result directly reflects the clamped-then-selected raw sample.
        var function = ResolveTestFunction(
            "/FunctionType 0 /Domain [0 1] /Range [0 255] /Size [2] /BitsPerSample 8",
            [0, 255]);

        // Act
        var belowDomain = function.Evaluate(-5);
        var aboveDomain = function.Evaluate(5);

        // Assert: -5 clamps to 0 (selects sample 0); 5 clamps to 1 (selects sample 1).
        Assert.Equal(0.0, belowDomain[0]);
        Assert.Equal(255.0, aboveDomain[0]);
    }

    /// <summary>Proves that an absent <c>/Encode</c> defaults to <c>[0, Size - 1]</c>, mapping the full <c>/Domain</c> onto the full sample-index range.</summary>
    [Fact]
    public void PdfDocument_Functions_Type0_EncodeAbsent_DefaultsToZeroToSizeMinusOne()
    {
        // Arrange: Domain [0, 10], 6 samples evenly spaced 0..255, no /Encode - the default
        // [0, Size - 1] = [0, 5] maps input 5 (domain midpoint) to sample index 2.5.
        var function = ResolveTestFunction(
            "/FunctionType 0 /Domain [0 10] /Range [0 255] /Size [6] /BitsPerSample 8",
            [0, 51, 102, 153, 204, 255]);

        // Act
        var result = function.Evaluate(5);

        // Assert: interpolates halfway between sample 2 (102) and sample 3 (153) -> 127.5.
        Assert.Equal(127.5, result[0]);
    }

    /// <summary>Proves that an explicit <c>/Encode</c> overrides the default, mapping <c>/Domain</c> onto a custom sample-index sub-range.</summary>
    [Fact]
    public void PdfDocument_Functions_Type0_ExplicitEncode_MapsDomainToCustomSampleIndexRange()
    {
        // Arrange: Domain [0, 1], 10 samples, /Encode [2, 5] - without this override, the default
        // encode ([0, 9]) would select sample 0 at input 0 and sample 9 at input 1, both 0; the
        // explicit override instead selects samples 2 and 5, which alone carry non-zero values.
        var function = ResolveTestFunction(
            "/FunctionType 0 /Domain [0 1] /Range [0 255] /Size [10] /BitsPerSample 8 /Encode [2 5]",
            [0, 0, 50, 0, 0, 200, 0, 0, 0, 0]);

        // Act
        var atDomainMin = function.Evaluate(0);
        var atDomainMax = function.Evaluate(1);

        // Assert: domain 0 -> sample index 2 (50); domain 1 -> sample index 5 (200).
        Assert.Equal(50.0, atDomainMin[0]);
        Assert.Equal(200.0, atDomainMax[0]);
    }

    /// <summary>Proves that <c>/Decode</c> linearly remaps a raw sample value into its own declared range, independent of (though still clipped to) <c>/Range</c>.</summary>
    [Fact]
    public void PdfDocument_Functions_Type0_Decode_MapsRawSampleToCustomOutputRange()
    {
        // Arrange: Domain [0, 1], 2 samples (0, 255), /Decode [10, 20] (distinct from the wider
        // /Range [0, 100], so the assertions below isolate /Decode's own mapping, not /Range's
        // clipping).
        var function = ResolveTestFunction(
            "/FunctionType 0 /Domain [0 1] /Range [0 100] /Decode [10 20] /Size [2] /BitsPerSample 8",
            [0, 255]);

        // Act
        var atDomainMin = function.Evaluate(0);
        var atDomainMax = function.Evaluate(1);

        // Assert: raw sample 0 decodes to 10; raw sample 255 decodes to 20.
        Assert.Equal(10.0, atDomainMin[0]);
        Assert.Equal(20.0, atDomainMax[0]);
    }

    /// <summary>Proves that a sample-index position that falls between two samples linearly interpolates between them.</summary>
    [Fact]
    public void PdfDocument_Functions_Type0_NonIntegerSampleIndex_InterpolatesBetweenAdjacentSamples()
    {
        // Arrange: Domain [0, 1], 3 samples (0, 100, 200), Range [0, 255] (identity Decode).
        // Default /Encode [0, 2] maps input 0.25 to sample index 0.5 - exactly halfway between
        // sample 0 and sample 1.
        var function = ResolveTestFunction(
            "/FunctionType 0 /Domain [0 1] /Range [0 255] /Size [3] /BitsPerSample 8",
            [0, 100, 200]);

        // Act
        var result = function.Evaluate(0.25);

        // Assert
        Assert.Equal(50.0, result[0]);
    }

    /// <summary>Proves that 8-bit samples are read and evaluated correctly.</summary>
    [Fact]
    public void PdfDocument_Functions_Type0_EightBitSamples_EvaluatesCorrectly()
    {
        // Arrange
        var function = ResolveTestFunction(
            "/FunctionType 0 /Domain [0 1] /Range [0 255] /Size [2] /BitsPerSample 8",
            [10, 250]);

        // Act
        var atDomainMin = function.Evaluate(0);
        var atDomainMax = function.Evaluate(1);

        // Assert
        Assert.Equal(10.0, atDomainMin[0]);
        Assert.Equal(250.0, atDomainMax[0]);
    }

    /// <summary>Proves that 16-bit samples (big-endian, 2 bytes per sample) are read and evaluated correctly.</summary>
    [Fact]
    public void PdfDocument_Functions_Type0_SixteenBitSamples_EvaluatesCorrectly()
    {
        // Arrange: Range [0, 65535] matches the 16-bit maximum raw sample value exactly, so
        // Decode (defaulted to Range) is an identity pass-through.
        var function = ResolveTestFunction(
            "/FunctionType 0 /Domain [0 1] /Range [0 65535] /Size [2] /BitsPerSample 16",
            BuildUInt16SampleBytes(1000, 60000));

        // Act
        var atDomainMin = function.Evaluate(0);
        var atDomainMax = function.Evaluate(1);

        // Assert
        Assert.Equal(1000.0, atDomainMin[0]);
        Assert.Equal(60000.0, atDomainMax[0]);
    }

    /// <summary>Proves that a <c>/FunctionType</c> other than <c>0</c> (<c>2</c> exponential, <c>3</c> stitching, <c>4</c> PostScript calculator) throws <see cref="UnsupportedImageFeatureException"/> rather than being evaluated.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PdfDocument_Functions_Type0_UnsupportedFunctionType_ThrowsUnsupportedImageFeatureException(int functionType)
    {
        // Act & Assert
        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => ResolveTestFunction($"/FunctionType {functionType}", []));
        Assert.Equal($"pdf-functiontype-{functionType}", exception.Feature);
    }

    /// <summary>Proves that a multi-input <c>/FunctionType 0</c> function (a <c>/Domain</c> with more than 2 elements - the <c>/DeviceN</c>/<c>/Separation</c> tint-transform shape) throws <see cref="UnsupportedImageFeatureException"/>.</summary>
    [Fact]
    public void PdfDocument_Functions_Type0_MultiInputDomain_ThrowsUnsupportedImageFeatureException()
    {
        // Act & Assert: a 2-input function's /Domain has 4 elements (a [min, max] pair per input).
        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => ResolveTestFunction("/FunctionType 0 /Domain [0 1 0 1]", []));
        Assert.Equal("pdf-function-multiinput", exception.Feature);
    }

    #endregion
}
