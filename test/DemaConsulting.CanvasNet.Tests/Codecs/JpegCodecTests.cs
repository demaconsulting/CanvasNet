using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the <see cref="JpegCodec"/> class.
/// </summary>
/// <remarks>
///     Several tests build raw JPEG-like byte streams by hand, using independent test-only marker,
///     segment, and entropy-data helpers rather than <see cref="JpegCodec"/>'s own encoder, so
///     that malformed-stream rejection paths are exercised against the JPEG marker structure
///     itself rather than only by round-tripping production output.
/// </remarks>
public class JpegCodecTests
{
    private const byte MarkerPrefix = 0xFF;
    private const byte MarkerSoi = 0xD8;
    private const byte MarkerEoi = 0xD9;
    private const byte MarkerSof0 = 0xC0;
    private const byte MarkerDht = 0xC4;
    private const byte MarkerDqt = 0xDB;
    private const byte MarkerSos = 0xDA;
    private const byte MarkerDri = 0xDD;
    private const byte MarkerRst0 = 0xD0;

    /// <summary>
    ///     Builds a synthetic surface with gradients and distinct flat-color regions so JPEG
    ///     subsampling, edge padding, and block-boundary behavior are all exercised by the same
    ///     image.
    /// </summary>
    private static Surface BuildTestCanvas(int width, int height)
    {
        var surface = new Surface(width, height);
        var maxX = Math.Max(1, width - 1);
        var maxY = Math.Max(1, height - 1);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var baseValue = ((x * 160) / maxX) + ((y * 80) / maxY);
                var r = (byte)Math.Min(255, baseValue + 10);
                var g = (byte)Math.Min(255, baseValue + 5);
                var b = (byte)Math.Min(255, baseValue);

                if (x >= width / 8 && x < (width / 8) + Math.Max(4, width / 6) &&
                    y >= height / 2 && y < (height / 2) + Math.Max(4, height / 5))
                {
                    r = 96;
                    g = 96;
                    b = 96;
                }
                else if (x >= width / 2 && x < (width / 2) + Math.Max(4, width / 6) &&
                         y >= height / 2 && y < (height / 2) + Math.Max(4, height / 5))
                {
                    r = 144;
                    g = 144;
                    b = 144;
                }
                else if (x >= Math.Max(0, width - Math.Max(4, width / 5)) &&
                         y >= Math.Max(0, height - Math.Max(4, height / 4)))
                {
                    r = 192;
                    g = 192;
                    b = 192;
                }

                surface[x, y] = new Rgba32(r, g, b, (byte)(180 - ((x + y) % 80)));
            }
        }

        return surface;
    }

    /// <summary>
    ///     Verifies that two canvases match within a per-channel RGB tolerance, while also
    ///     asserting the JPEG decode contract that the loaded image is fully opaque.
    /// </summary>
    private static void AssertPixelsApproximatelyEqual(Surface expected, Surface actual, int tolerancePerChannel)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var expectedPixel = expected[x, y];
                var actualPixel = actual[x, y];

                Assert.True(
                    Math.Abs(expectedPixel.R - actualPixel.R) <= tolerancePerChannel,
                    $"Pixel ({x}, {y}) red channel differed by more than {tolerancePerChannel}: expected {expectedPixel.R}, actual {actualPixel.R}.");
                Assert.True(
                    Math.Abs(expectedPixel.G - actualPixel.G) <= tolerancePerChannel,
                    $"Pixel ({x}, {y}) green channel differed by more than {tolerancePerChannel}: expected {expectedPixel.G}, actual {actualPixel.G}.");
                Assert.True(
                    Math.Abs(expectedPixel.B - actualPixel.B) <= tolerancePerChannel,
                    $"Pixel ({x}, {y}) blue channel differed by more than {tolerancePerChannel}: expected {expectedPixel.B}, actual {actualPixel.B}.");
                Assert.Equal(255, actualPixel.A);
            }
        }
    }

    /// <summary>
    ///     Builds a complete JPEG segment from a marker byte and payload bytes, independent of
    ///     <see cref="JpegCodec"/>'s own writer.
    /// </summary>
    private static byte[] BuildSegment(byte marker, params byte[] payload)
    {
        var segment = new byte[4 + payload.Length];
        segment[0] = MarkerPrefix;
        segment[1] = marker;
        WriteUInt16Be(segment, 2, payload.Length + 2);
        payload.CopyTo(segment, 4);
        return segment;
    }

    /// <summary>
    ///     Builds a minimal DQT segment containing a single 8-bit quantization table whose entries
    ///     are all 1.
    /// </summary>
    private static byte[] BuildMinimalDqtSegment()
    {
        var payload = new byte[65];
        payload[0] = 0x00;
        for (var i = 1; i < payload.Length; i++)
        {
            payload[i] = 1;
        }

        return BuildSegment(MarkerDqt, payload);
    }

    /// <summary>
    ///     Builds a minimal DHT segment containing a DC table whose sole symbol is category 0 and
    ///     an AC table whose sole symbol is EOB, sufficient for a single all-zero block.
    /// </summary>
    private static byte[] BuildMinimalDhtSegment()
    {
        var payload = new List<byte>
        {
            0x00,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0x00,
            0x10,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0x00
        };

        return BuildSegment(MarkerDht, [.. payload]);
    }

    /// <summary>
    ///     Builds a SOF segment with the supplied marker and component descriptors.
    /// </summary>
    private static byte[] BuildSofSegment(byte marker, int width, int height, params (byte Id, byte Sampling, byte Quant)[] components)
    {
        var payload = new byte[6 + (components.Length * 3)];
        payload[0] = 8;
        WriteUInt16Be(payload, 1, height);
        WriteUInt16Be(payload, 3, width);
        payload[5] = (byte)components.Length;

        var offset = 6;
        foreach (var component in components)
        {
            payload[offset++] = component.Id;
            payload[offset++] = component.Sampling;
            payload[offset++] = component.Quant;
        }

        return BuildSegment(marker, payload);
    }

    /// <summary>
    ///     Builds an SOS segment for the supplied components.
    /// </summary>
    private static byte[] BuildSosSegment(params (byte Id, byte Tables)[] components)
    {
        var payload = new byte[1 + (components.Length * 2) + 3];
        payload[0] = (byte)components.Length;

        var offset = 1;
        foreach (var component in components)
        {
            payload[offset++] = component.Id;
            payload[offset++] = component.Tables;
        }

        payload[offset++] = 0;
        payload[offset++] = 63;
        payload[offset] = 0;

        return BuildSegment(MarkerSos, payload);
    }

    /// <summary>
    ///     Builds a complete JPEG byte stream from already-formed segments and optional entropy
    ///     data, independent of <see cref="JpegCodec"/>'s own encoder.
    /// </summary>
    private static byte[] BuildJpeg(byte[][] segments, byte[]? entropyData = null, bool includeEoi = true)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(MarkerPrefix);
        stream.WriteByte(MarkerSoi);

        foreach (var segment in segments)
        {
            stream.Write(segment, 0, segment.Length);
        }

        if (entropyData != null)
        {
            stream.Write(entropyData, 0, entropyData.Length);
        }

        if (includeEoi)
        {
            stream.WriteByte(MarkerPrefix);
            stream.WriteByte(MarkerEoi);
        }

        return stream.ToArray();
    }

    /// <summary>
    ///     Writes a big-endian unsigned 16-bit integer into a byte buffer.
    /// </summary>
    private static void WriteUInt16Be(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    // ------------------------------------------------------------------------------------------
    // Argument validation
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Verifies that Load(Stream) rejects a null stream argument.
    /// </summary>
    [Fact]
    public void JpegCodec_LoadStream_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JpegCodec.Load((Stream)null!));
    }

    /// <summary>
    ///     Verifies that Load(string) rejects a null path argument.
    /// </summary>
    [Fact]
    public void JpegCodec_LoadPath_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JpegCodec.Load((string)null!));
    }

    /// <summary>
    ///     Verifies that Load(string) rejects an empty path argument.
    /// </summary>
    [Fact]
    public void JpegCodec_LoadPath_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => JpegCodec.Load(string.Empty));
    }

    /// <summary>
    ///     Verifies that Save(Surface, Stream, int) rejects a null surface argument.
    /// </summary>
    [Fact]
    public void JpegCodec_SaveStream_NullCanvas_ThrowsArgumentNullException()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentNullException>(() => JpegCodec.Save(null!, stream));
    }

    /// <summary>
    ///     Verifies that Save(Surface, Stream, int) rejects a null stream argument.
    /// </summary>
    [Fact]
    public void JpegCodec_SaveStream_NullStream_ThrowsArgumentNullException()
    {
        var surface = new Surface(1, 1);
        Assert.Throws<ArgumentNullException>(() => JpegCodec.Save(surface, (Stream)null!));
    }

    /// <summary>
    ///     Verifies that Save(Surface, string, int) rejects a null path argument.
    /// </summary>
    [Fact]
    public void JpegCodec_SavePath_NullPath_ThrowsArgumentNullException()
    {
        var surface = new Surface(1, 1);
        Assert.Throws<ArgumentNullException>(() => JpegCodec.Save(surface, (string)null!));
    }

    /// <summary>
    ///     Verifies that Save(Surface, string, int) rejects an empty path argument.
    /// </summary>
    [Fact]
    public void JpegCodec_SavePath_EmptyPath_ThrowsArgumentException()
    {
        var surface = new Surface(1, 1);
        Assert.Throws<ArgumentException>(() => JpegCodec.Save(surface, string.Empty));
    }

    /// <summary>
    ///     Verifies that Save(Surface, Stream, int) rejects quality values less than 1.
    /// </summary>
    [Fact]
    public void JpegCodec_SaveStream_QualityBelowRange_ThrowsArgumentOutOfRangeException()
    {
        var surface = new Surface(1, 1);
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => JpegCodec.Save(surface, stream, 0));
    }

    /// <summary>
    ///     Verifies that Save(Surface, Stream, int) rejects quality values greater than 100.
    /// </summary>
    [Fact]
    public void JpegCodec_SaveStream_QualityAboveRange_ThrowsArgumentOutOfRangeException()
    {
        var surface = new Surface(1, 1);
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => JpegCodec.Save(surface, stream, 101));
    }

    // ------------------------------------------------------------------------------------------
    // Malformed-data rejection
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Verifies that Load rejects a stream that does not begin with the JPEG SOI marker.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_MissingSoiMarker_ThrowsInvalidDataException()
    {
        var bytes = new byte[] { 0x00, 0x00, MarkerPrefix, MarkerEoi };

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(bytes)));
    }

    /// <summary>
    ///     Verifies that Load rejects unsupported SOF markers other than baseline SOF0 and
    ///     progressive SOF2.
    /// </summary>
    [Theory]
    [InlineData(0xC1)]
    [InlineData(0xC3)]
    [InlineData(0xC9)]
    public void JpegCodec_Load_UnsupportedSofMarker_ThrowsInvalidDataException(byte marker)
    {
        var jpeg = BuildJpeg(
        [
            BuildSofSegment(marker, 1, 1, (1, 0x11, 0))
        ]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Regression test for finding #5: <see cref="JpegCodec.GetInfo(Stream)"/>'s marker scan
    ///     used to treat an unsupported SOF/frame marker (e.g. SOF1/SOF3) exactly like any other
    ///     unrecognized segment - skipping over it via its length prefix - and would then happily
    ///     report dimensions from a subsequent, valid SOF0 segment, silently disagreeing with
    ///     <see cref="JpegCodec.Load(Stream)"/>, which has always rejected the same bytes.
    ///     Builds a file with an unsupported SOF marker followed by a valid SOF0 segment and
    ///     proves both Load and GetInfo now reject it identically with
    ///     <see cref="InvalidDataException"/>.
    /// </summary>
    [Theory]
    [InlineData(0xC1)]
    [InlineData(0xC3)]
    public void JpegCodec_UnsupportedSofMarkerFollowedByValidSof0_BothLoadAndGetInfoThrow(byte marker)
    {
        var jpeg = BuildJpeg(
        [
            BuildSofSegment(marker, 2, 2, (1, 0x11, 0)),
            BuildSofSegment(MarkerSof0, 4, 3, (1, 0x11, 0))
        ]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
        Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a baseline SOF0 frame declaring four components.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_FourComponentSof0_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg(
        [
            BuildSofSegment(
                MarkerSof0,
                1,
                1,
                (1, 0x11, 0),
                (2, 0x11, 0),
                (3, 0x11, 0),
                (4, 0x11, 0))
        ]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a frame width exceeding Surface.MaxDimension (8192) with
    ///     InvalidDataException rather than an ArgumentOutOfRangeException escaping from the
    ///     Surface constructor, and before any MCU-grid width/height arithmetic performed while
    ///     decoding the scan is ever reached.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_WidthExceedsMaxDimension_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg(
        [
            BuildSofSegment(MarkerSof0, Surface.MaxDimension + 1, 1, (1, 0x11, 0))
        ]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a frame height exceeding Surface.MaxDimension (8192) with
    ///     InvalidDataException rather than an ArgumentOutOfRangeException escaping from the
    ///     Surface constructor.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_HeightExceedsMaxDimension_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg(
        [
            BuildSofSegment(MarkerSof0, 1, Surface.MaxDimension + 1, (1, 0x11, 0))
        ]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a stream whose SOS segment appears before any SOF marker.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_MissingSofSegment_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg(
        [
            BuildMinimalDqtSegment(),
            BuildMinimalDhtSegment(),
            BuildSosSegment((1, 0x00))
        ]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a stream missing any DHT segment.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_MissingDhtSegment_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg(
            [
                BuildMinimalDqtSegment(),
                BuildSofSegment(MarkerSof0, 1, 1, (1, 0x11, 0)),
                BuildSosSegment((1, 0x00))
            ],
            entropyData: [0x00]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a stream missing any DQT segment.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_MissingDqtSegment_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg(
            [
                BuildMinimalDhtSegment(),
                BuildSofSegment(MarkerSof0, 1, 1, (1, 0x11, 0)),
                BuildSosSegment((1, 0x00))
            ],
            entropyData: [0x00]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a stream missing any SOS segment.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_MissingSosSegment_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg(
        [
            BuildMinimalDqtSegment(),
            BuildMinimalDhtSegment(),
            BuildSofSegment(MarkerSof0, 1, 1, (1, 0x11, 0))
        ]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a stream truncated while still reading the marker/segment
    ///     header area.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_TruncatedHeader_ThrowsInvalidDataException()
    {
        var jpeg = new byte[] { MarkerPrefix, MarkerSoi, MarkerPrefix, MarkerSof0, 0x00 };

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that Load rejects a stream truncated while entropy-coded scan data is still
    ///     being consumed.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_TruncatedEntropyData_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg(
            [
                BuildMinimalDqtSegment(),
                BuildMinimalDhtSegment(),
                BuildSofSegment(MarkerSof0, 1, 1, (1, 0x11, 0)),
                BuildSosSegment((1, 0x00))
            ],
            entropyData: [],
            includeEoi: false);

        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Provides truncated-segment-payload JPEG byte streams for
    ///     <see cref="JpegCodec_Load_TruncatedSegmentPayload_ThrowsInvalidDataException"/>, each of
    ///     which declares a segment length claiming more payload bytes than are actually present.
    /// </summary>
    public static TheoryData<byte[]> TruncatedSegmentPayloadCases()
    {
        var cases = new TheoryData<byte[]>
        {
            // DQT segment claims 0x45 payload bytes but supplies none.
            new byte[] { MarkerPrefix, MarkerSoi, MarkerPrefix, MarkerDqt, 0x00, 0x45 },

            // DHT segment claims 0x45 payload bytes but supplies none.
            new byte[] { MarkerPrefix, MarkerSoi, MarkerPrefix, MarkerDht, 0x00, 0x45 },

            // SOF0 segment claims 0x20 payload bytes but supplies none.
            new byte[] { MarkerPrefix, MarkerSoi, MarkerPrefix, MarkerSof0, 0x00, 0x20 },

            // SOS segment (preceded by well-formed DQT/DHT/SOF0 segments) claims 0x20 payload
            // bytes but supplies none.
            BuildJpeg(
                [
                    BuildMinimalDqtSegment(),
                    BuildMinimalDhtSegment(),
                    BuildSofSegment(MarkerSof0, 1, 1, (1, 0x11, 0)),
                    new byte[] { MarkerPrefix, MarkerSos, 0x00, 0x20 }
                ],
                entropyData: null,
                includeEoi: false)
        };

        return cases;
    }

    /// <summary>
    ///     Verifies that Load rejects streams whose DQT, DHT, SOF0, or SOS segment length claims
    ///     more payload bytes than are actually present, throwing
    ///     <see cref="InvalidDataException"/> rather than an unhandled
    ///     <see cref="IndexOutOfRangeException"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(TruncatedSegmentPayloadCases))]
    public void JpegCodec_Load_TruncatedSegmentPayload_ThrowsInvalidDataException(byte[] jpeg)
    {
        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }

    // ------------------------------------------------------------------------------------------
    // Round-trip save/load
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Proves that a quality-90 round-trip preserves a larger, MCU-aligned synthetic surface to
    ///     within the empirically observed RGB similarity bound.
    /// </summary>
    [Fact]
    public void JpegCodec_SaveThenLoad_AlignedCanvas_RoundTripsWithinTolerance()
    {
        var surface = BuildTestCanvas(64, 48);

        using var stream = new MemoryStream();
        JpegCodec.Save(surface, stream, quality: 90);
        stream.Position = 0;
        var loaded = JpegCodec.Load(stream);

        // A tolerance of 15 per channel leaves safety margin above the empirically measured
        // quality-90 maxima for this codec: 6 on a 64x48 synthetic surface and 11 on a 33x17 one.
        AssertPixelsApproximatelyEqual(surface, loaded, 15);
    }

    /// <summary>
    ///     Proves that a quality-90 round-trip preserves an odd-sized synthetic surface to within
    ///     the empirically observed RGB similarity bound.
    /// </summary>
    [Fact]
    public void JpegCodec_SaveThenLoad_OddSizedCanvas_RoundTripsWithinTolerance()
    {
        var surface = BuildTestCanvas(33, 17);

        using var stream = new MemoryStream();
        JpegCodec.Save(surface, stream, quality: 90);
        stream.Position = 0;
        var loaded = JpegCodec.Load(stream);

        AssertPixelsApproximatelyEqual(surface, loaded, 15);
    }

    /// <summary>
    ///     Proves that the documented quality endpoints 1 and 100 both encode and decode
    ///     successfully.
    /// </summary>
    [Fact]
    public void JpegCodec_SaveThenLoad_QualityEndpoints_Succeed()
    {
        var surface = BuildTestCanvas(17, 19);

        foreach (var quality in new[] { 1, 100 })
        {
            using var stream = new MemoryStream();
            JpegCodec.Save(surface, stream, quality);
            stream.Position = 0;
            var loaded = JpegCodec.Load(stream);

            Assert.Equal(surface.Width, loaded.Width);
            Assert.Equal(surface.Height, loaded.Height);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Restart-marker handling
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Proves that a JPEG stream using a DRI-declared restart interval of one MCU, with an
    ///     RST0 marker inserted between MCUs in the entropy-coded data, decodes to exactly the
    ///     same pixels as the equivalent stream without a DRI segment or restart marker. Both
    ///     streams encode a 16x8 single-component (grayscale) image as two all-zero-coefficient
    ///     8x8 blocks, using the same minimal DC-category-0/AC-EOB Huffman tables used elsewhere
    ///     in this file, so each block is exactly one entropy byte (0x00) and the restart marker
    ///     is the only difference between the two streams.
    /// </summary>
    [Fact]
    public void JpegCodec_Load_WithRestartMarkers_DecodesIdenticallyToWithoutRestartMarkers()
    {
        var withoutRestartMarkers = BuildJpeg(
            [
                BuildMinimalDqtSegment(),
                BuildMinimalDhtSegment(),
                BuildSofSegment(MarkerSof0, 16, 8, (1, 0x11, 0)),
                BuildSosSegment((1, 0x00))
            ],
            entropyData: [0x00, 0x00]);

        var withRestartMarkers = BuildJpeg(
            [
                BuildMinimalDqtSegment(),
                BuildMinimalDhtSegment(),
                BuildSofSegment(MarkerSof0, 16, 8, (1, 0x11, 0)),
                BuildSegment(MarkerDri, 0x00, 0x01),
                BuildSosSegment((1, 0x00))
            ],
            entropyData: [0x00, MarkerPrefix, MarkerRst0, 0x00]);

        var canvasWithoutRestartMarkers = JpegCodec.Load(new MemoryStream(withoutRestartMarkers));
        var canvasWithRestartMarkers = JpegCodec.Load(new MemoryStream(withRestartMarkers));

        AssertPixelsApproximatelyEqual(canvasWithoutRestartMarkers, canvasWithRestartMarkers, 0);
    }

    // ------------------------------------------------------------------------------------------
    // SIMD-vs-scalar cross-check
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Proves that the vectorized YCbCr-to-RGB row conversion matches its scalar reference
    ///     implementation for both exact-vector and vector-plus-remainder lengths.
    /// </summary>
    [Fact]
    public void JpegCodec_ConvertYCbCrRowToRgb_VectorAndScalarRemainder_MatchesScalarReference()
    {
        var lengths = new[]
        {
            Vector<float>.Count * 2,
            (Vector<float>.Count * 2) + 3
        };

        foreach (var length in lengths)
        {
            var y = new byte[length];
            var cb = new byte[length];
            var cr = new byte[length];
            var expectedR = new byte[length];
            var expectedG = new byte[length];
            var expectedB = new byte[length];
            var actualR = new byte[length];
            var actualG = new byte[length];
            var actualB = new byte[length];

            for (var i = 0; i < length; i++)
            {
                y[i] = (byte)((i * 19) + 7);
                cb[i] = (byte)((i * 23) + 11);
                cr[i] = (byte)((i * 29) + 13);
            }

            JpegCodec.ConvertYCbCrRowToRgbScalar(y, cb, cr, expectedR, expectedG, expectedB, length);
            JpegCodec.ConvertYCbCrRowToRgb(y, cb, cr, actualR, actualG, actualB, length);

            Assert.Equal(expectedR, actualR);
            Assert.Equal(expectedG, actualG);
            Assert.Equal(expectedB, actualB);
        }
    }

    // ------------------------------------------------------------------------------------------
    // GetInfo
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Verifies that GetInfo reports the correct width, height, and single-channel/no-alpha
    ///     result for a grayscale (1-component) SOF0 frame, without requiring an SOS segment or
    ///     any entropy-coded scan data.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_Grayscale_ReturnsExpectedInfoWithoutSosOrEntropyData()
    {
        var jpeg = BuildJpeg(
        [
            BuildMinimalDqtSegment(),
            BuildMinimalDhtSegment(),
            BuildSofSegment(MarkerSof0, 4, 3, (1, 0x11, 0))
        ], includeEoi: false);

        var info = JpegCodec.GetInfo(new MemoryStream(jpeg));

        Assert.Equal(new ImageInfo(4, 3, 1, false), info);
    }

    /// <summary>
    ///     Verifies that GetInfo reports the correct width, height, and 3-channel/no-alpha result
    ///     for a color (3-component) SOF0 frame. JPEG has no alpha channel, so HasAlpha is always
    ///     false regardless of component count.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_Color_ReturnsExpectedInfo()
    {
        var jpeg = BuildJpeg(
        [
            BuildMinimalDqtSegment(),
            BuildMinimalDhtSegment(),
            BuildSofSegment(MarkerSof0, 6, 4, (1, 0x22, 0), (2, 0x11, 1), (3, 0x11, 1))
        ]);

        var info = JpegCodec.GetInfo(new MemoryStream(jpeg));

        Assert.Equal(new ImageInfo(6, 4, 3, false), info);
    }

    /// <summary>
    ///     Verifies that GetInfo(string) round-trips through a real file exactly like
    ///     GetInfo(Stream).
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfoPath_ReturnsExpectedInfo()
    {
        var jpeg = BuildJpeg(
        [
            BuildMinimalDqtSegment(),
            BuildMinimalDhtSegment(),
            BuildSofSegment(MarkerSof0, 5, 2, (1, 0x11, 0))
        ]);

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, jpeg);

            var info = JpegCodec.GetInfo(path);

            Assert.Equal(new ImageInfo(5, 2, 1, false), info);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Verifies that GetInfo(Stream) rejects a null stream argument.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfoStream_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JpegCodec.GetInfo((Stream)null!));
    }

    /// <summary>
    ///     Verifies that GetInfo(string) rejects a null path argument.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfoPath_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JpegCodec.GetInfo((string)null!));
    }

    /// <summary>
    ///     Verifies that GetInfo(string) rejects an empty path argument.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfoPath_EmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => JpegCodec.GetInfo(string.Empty));
    }

    /// <summary>
    ///     Verifies that GetInfo rejects a stream missing the SOI marker with the same
    ///     InvalidDataException contract as Load.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_MissingSoiMarker_ThrowsInvalidDataException()
    {
        var jpeg = new byte[] { 0x00, 0x00, 0x00, 0x00 };

        Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that GetInfo throws InvalidDataException when the stream ends before any
    ///     SOF0/SOF2 marker is found (distinct from the probe-limit-exceeded case).
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_TruncatedBeforeSofFound_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg([BuildMinimalDqtSegment()], includeEoi: false);

        Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that GetInfo throws InvalidDataException when an SOS segment is encountered
    ///     before any SOF0/SOF2 marker.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_SosBeforeSof_ThrowsInvalidDataException()
    {
        var jpeg = BuildJpeg([BuildSosSegment((1, 0x00))]);

        Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));
    }

    /// <summary>
    ///     Verifies that GetInfo still throws InvalidDataException for a stream that never
    ///     contains a SOF0/SOF2 marker anywhere, even well beyond the MaxProbeHeaderBytes soft
    ///     cap: the soft cap's bulk-read fallback buffers the remainder of the stream and
    ///     continues scanning, but a genuinely SOF-less stream is still correctly rejected once
    ///     end-of-stream is reached, matching <see cref="JpegCodec.Load(Stream)"/>'s own
    ///     rejection of the same bytes.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_NoSofAnywhereEvenBeyondProbeCap_ThrowsInvalidDataException()
    {
        // Filler bytes containing no 0xFF marker byte at all, long enough to exceed the 1 MiB
        // soft cap and trigger the bulk-read fallback before any SOF0/SOF2 marker could ever be
        // found.
        var filler = new byte[1_100_000];

        var jpeg = BuildJpeg([], entropyData: filler, includeEoi: false);

        var exception = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));
        Assert.Contains("SOF0/SOF2 marker", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Regression test: when the SOF0/SOF2 marker itself is found within the
    ///     MaxProbeHeaderBytes soft cap, but its declared segment length would require reading
    ///     past that cap, GetInfo used to throw a "probe limit" InvalidDataException even though
    ///     plenty more of the stream remained (not a genuine truncation). Now the soft cap
    ///     triggers a one-time bulk-read fallback instead, so this exact byte sequence is scanned
    ///     to completion and rejected only for a genuine format-validation reason (the filler
    ///     bytes used as the SOF payload do not encode a valid 8-bit sample precision) - proving
    ///     GetInfo no longer fails merely because the SOF segment's declared length crosses the
    ///     soft cap.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_SofSegmentExtendsBeyondProbeCap_FailsForFormatReasonNotProbeCap()
    {
        // Filler APP0 segments (each just under the 64 KiB segment-length limit) to advance the
        // scan position to just under the 1 MiB soft cap without ever encountering a SOF marker.
        const int fillerSegmentPayloadLength = 65_000;
        var segments = new List<byte[]>();
        for (var total = 0; total < 1_000_000; total += fillerSegmentPayloadLength + 2)
        {
            segments.Add(BuildSegment(0xE0, new byte[fillerSegmentPayloadLength]));
        }

        // An SOF0 marker whose declared segment length (65535, the maximum a ushort can express)
        // pushes the required end position well past the 1 MiB soft cap, even though the
        // underlying stream continues for a long time afterwards (so this is not a genuine
        // truncation) - the bulk-read fallback buffers the rest of the stream and this segment's
        // (all-zero filler) payload is then parsed as an actual SOF payload.
        var sofMarker = new byte[] { MarkerPrefix, MarkerSof0, 0xFF, 0xFF };
        segments.Add(sofMarker);

        var jpeg = BuildJpeg([.. segments], entropyData: new byte[200_000], includeEoi: false);

        var exception = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));
        Assert.DoesNotContain("probe limit", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sample precision", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves the core GetInfo/Load parity fix in this bundle: a genuinely valid, decodable
    ///     JPEG (the same minimal 16x8 single-component fixture used by
    ///     <see cref="JpegCodec_Load_WithRestartMarkers_DecodesIdenticallyToWithoutRestartMarkers"/>)
    ///     preceded by more than <see cref="JpegCodec.MaxProbeHeaderBytes"/> of leading APP0
    ///     filler segments - which used to make GetInfo throw a "probe limit" InvalidDataException
    ///     even though <see cref="JpegCodec.Load(Stream)"/> would successfully decode the exact
    ///     same bytes - now succeeds via the soft-cap bulk-read fallback and matches Load's own
    ///     decoded dimensions and component count exactly.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_LargeLeadingAppSegments_SucceedsAndMatchesLoadResult()
    {
        // Arrange: enough leading APP0 filler segments to exceed MaxProbeHeaderBytes before the
        // SOF0 marker is ever seen, followed by the minimal valid, decodable JPEG fixture.
        const int fillerSegmentPayloadLength = 65_000;
        var segments = new List<byte[]>();
        var totalFillerBytes = 0;
        while (totalFillerBytes <= JpegCodec.MaxProbeHeaderBytes)
        {
            segments.Add(BuildSegment(0xE0, new byte[fillerSegmentPayloadLength]));
            totalFillerBytes += fillerSegmentPayloadLength + 4;
        }

        segments.Add(BuildMinimalDqtSegment());
        segments.Add(BuildMinimalDhtSegment());
        segments.Add(BuildSofSegment(MarkerSof0, 16, 8, (1, 0x11, 0)));
        segments.Add(BuildSosSegment((1, 0x00)));

        var jpeg = BuildJpeg([.. segments], entropyData: [0x00, 0x00]);

        // Act
        var info = JpegCodec.GetInfo(new MemoryStream(jpeg));
        var surface = JpegCodec.Load(new MemoryStream(jpeg));

        // Assert: GetInfo succeeds (rather than throwing merely because the soft cap was
        // reached) and matches Load's own decoded dimensions/channel count exactly.
        Assert.Equal(new ImageInfo(16, 8, 1, false), info);
        Assert.Equal(16, surface.Width);
        Assert.Equal(8, surface.Height);
    }

    /// <summary>
    ///     Proves that GetInfo does not decode the full file: for a stream much larger than the
    ///     MaxProbeHeaderBytes cap, the stream position after GetInfo returns is capped at
    ///     MaxProbeHeaderBytes even though the underlying stream is several times longer.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_LargeStream_NeverReadsPastProbeLimit()
    {
        var filler = new byte[3_000_000];

        var jpeg = BuildJpeg(
        [
            BuildMinimalDqtSegment(),
            BuildMinimalDhtSegment(),
            BuildSofSegment(MarkerSof0, 4, 3, (1, 0x11, 0))
        ], entropyData: filler);

        using var stream = new MemoryStream(jpeg);

        var info = JpegCodec.GetInfo(stream);

        Assert.Equal(new ImageInfo(4, 3, 1, false), info);
        Assert.True(stream.Length > 1_048_576, "Test fixture must exceed the probe cap.");
        Assert.True(stream.Position <= 1_048_576, "GetInfo must not read past the probe cap.");
    }

    /// <summary>
    ///     Regression test for finding #6: <see cref="JpegCodec.GetInfo(Stream)"/> used to
    ///     eagerly read a full <c>MaxProbeHeaderBytes</c> (1 MiB) buffer from the stream up front
    ///     before scanning any markers at all, so even a file whose SOF segment appears within
    ///     the first few dozen bytes still forced up to 1 MiB of stream reads. Wraps a JPEG whose
    ///     SOF0 segment appears near the start (followed by several MB of filler entropy data) in
    ///     a <see cref="BoundedReadStream"/> configured to throw if more than a small, generous
    ///     budget is ever read, and proves GetInfo still succeeds - i.e. it now parses
    ///     incrementally and stops once the SOF has been fully read, rather than always reading
    ///     up to the full probe cap regardless of where the SOF actually is.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_SofNearStart_DoesNotReadFarBeyondWhatIsNeeded()
    {
        var filler = new byte[5_000_000];

        var jpeg = BuildJpeg(
        [
            BuildMinimalDqtSegment(),
            BuildMinimalDhtSegment(),
            BuildSofSegment(MarkerSof0, 4, 3, (1, 0x11, 0))
        ], entropyData: filler);

        // The SOF segment finishes well within the first few hundred bytes of the file; a
        // generous 16 KiB budget is far below both the file's total length and the 1 MiB probe
        // cap, so this only passes if GetInfo stops scanning once the SOF has been fully parsed
        // rather than always reading up to the full probe cap (or the whole file).
        using var bounded = new BoundedReadStream(new MemoryStream(jpeg), maxBytes: 16 * 1024);

        var info = JpegCodec.GetInfo(bounded);

        Assert.Equal(new ImageInfo(4, 3, 1, false), info);
    }

    /// <summary>
    ///     Independent re-verification of finding #4 (originally reported as a JPEG zero-length
    ///     segment infinite loop). The planning agent's investigation found this to already be a
    ///     false positive - a zero-length length-prefixed segment causes
    ///     <c>SkipLengthPrefixedSegment</c> to return the same position it started from (pointing
    ///     just past the marker code, at the 2-byte length field itself), after which the next
    ///     iteration's marker scan advances forward looking for the next real <c>0xFF</c> marker
    ///     byte, so the position always strictly advances rather than looping forever. This test
    ///     independently reproduces that scenario by calling <c>GetInfo</c> directly and
    ///     synchronously (no <c>Task.Run</c>/<c>Task.WhenAny</c>/<c>Task.Delay</c> race): the
    ///     position always strictly advancing is a deterministic, hardware-independent guarantee,
    ///     so the correct regression signal is that the call returns at all with the documented
    ///     <see cref="InvalidDataException"/> - not how long it takes to do so on any given
    ///     machine. No wall-clock timing assertion is used: this project never uses timing-based
    ///     test criteria, since CI hardware speed is outside our control and unreliable as a
    ///     signal. Algorithmic termination is a structural guarantee (position strictly advancing
    ///     each iteration), verified by code review, not by measuring elapsed time here.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_ZeroLengthSegment_TerminatesPromptlyWithInvalidDataException()
    {
        // SOI, then an APP0 marker whose 2-byte length field declares a length of 0 (invalid:
        // the length field must include itself, so the minimum valid value is 2), then EOI.
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00, 0xFF, 0xD9 };

        // Act: call directly and synchronously - no Task.Run/WhenAny/Delay race, and no
        // Stopwatch/timing assertion. The position always strictly advancing deterministically
        // bounds the work, so this either returns with the documented exception (fixed) or the
        // process itself would need to be killed by the CI job's own timeout (regressed to truly
        // unbounded) - there is no ambiguous "slow but fine" middle ground that timing would add
        // value in distinguishing.
        var caught = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));

        // Assert: the call returned (did not hang) with the documented exception.
        Assert.NotNull(caught);
    }

    /// <summary>
    ///     Verifies that GetInfo does not reject frame dimensions exceeding Surface.MaxDimension
    ///     (it reports the raw header value), while Load on the same bytes still throws
    ///     InvalidDataException.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_OversizedDimensions_NotRejected_ButLoadThrows()
    {
        var jpeg = BuildJpeg(
        [
            BuildSofSegment(MarkerSof0, Surface.MaxDimension + 1, 1, (1, 0x11, 0))
        ]);

        var info = JpegCodec.GetInfo(new MemoryStream(jpeg));

        Assert.Equal(Surface.MaxDimension + 1, info.Width);
        Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
    }
}
