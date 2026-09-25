using System.Globalization;
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
    ///     cap: past the soft cap, scanning continues segment-by-segment (never draining to
    ///     end-of-stream in one shot), but a genuinely SOF-less stream is still correctly
    ///     rejected once end-of-stream is reached, matching <see cref="JpegCodec.Load(Stream)"/>'s
    ///     own rejection of the same bytes.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_NoSofAnywhereEvenBeyondProbeCap_ThrowsInvalidDataException()
    {
        // Filler bytes containing no 0xFF marker byte at all, long enough to exceed the 1 MiB
        // soft cap before any SOF0/SOF2 marker could ever be found.
        var filler = new byte[1_100_000];

        var jpeg = BuildJpeg([], entropyData: filler, includeEoi: false);

        var exception = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));
        Assert.Contains("SOF0/SOF2 marker", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Regression test: when the SOF0/SOF2 marker itself is found within the
    ///     MaxProbeHeaderBytes soft cap, but its declared segment length would require reading
    ///     past that cap, GetInfo used to throw a "probe limit" InvalidDataException even though
    ///     plenty more of the stream remained (not a genuine truncation). Now scanning simply
    ///     continues past the soft cap, so this exact byte sequence is scanned to completion and
    ///     rejected only for a genuine format-validation reason (the filler
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
        // truncation) - scanning continues past the soft cap to read exactly that much of the
        // stream, and this segment's (all-zero filler) payload is then parsed as an actual SOF
        // payload.
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
    ///     same bytes - now succeeds by continuing to scan segment headers past the soft cap and
    ///     matches Load's own decoded dimensions and component count exactly.
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
    ///     Regression test: once <see cref="JpegCodec.MaxProbeHeaderBytes"/>'s soft cap is
    ///     crossed without finding a SOF0/SOF2 marker, GetInfo used to fall back to draining the
    ///     stream all the way to end-of-stream, even after the SOF marker - and the documented
    ///     "never reads scan data" contract - would have been satisfied moments later. Builds a
    ///     JPEG whose leading APP0 filler segments cross the 1 MiB soft cap, immediately followed
    ///     by a normal SOF0 segment, and then an <see cref="InfiniteTailStream"/> "entropy" tail
    ///     that never reaches end-of-stream (wrapped in a <see cref="BoundedReadStream"/> so that
    ///     any attempt to read into that tail fails fast rather than hanging the test). Proves
    ///     GetInfo returns the correct dimensions without ever reading past the end of the SOF
    ///     segment - i.e. it keeps scanning segment-by-segment past the soft cap only until SOF is
    ///     found, rather than unconditionally draining the remainder of the stream.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_SofShortlyAfterProbeCap_StopsAtSofWithoutDrainingStream()
    {
        // Arrange: enough leading APP0 filler segments to cross the 1 MiB soft cap, then a
        // normal-sized SOF0 segment immediately afterward.
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

        // The known, finite prefix ends at the last byte of the SOF segment - no SOS or entropy
        // data is included here at all, since a correct GetInfo must never read that far.
        var knownPrefix = BuildJpeg([.. segments], entropyData: null, includeEoi: false);

        // A small amount of slack above the known prefix length tolerates any incidental extra
        // buffering, while remaining far below the effectively unbounded "entropy" tail that
        // follows, so a stream that reads even moderately past the SOF segment - let alone all
        // the way to end-of-stream - is caught immediately rather than hanging.
        const int slack = 64;
        using var stream = new BoundedReadStream(
            new InfiniteTailStream(knownPrefix),
            maxBytes: knownPrefix.Length + slack);

        // Act
        var info = JpegCodec.GetInfo(stream);

        // Assert: GetInfo returns the correct dimensions, having stopped at the SOF segment
        // rather than draining the never-ending tail.
        Assert.Equal(new ImageInfo(16, 8, 1, false), info);
    }

    /// <summary>
    ///     Regression test for the DoS-reintroduction finding: after the previous round's fix
    ///     made the post-soft-cap fallback scan continue segment-by-segment (rather than
    ///     bulk-draining the stream), that scan had no upper bound at all besides genuine
    ///     end-of-stream. Builds a known (but deliberately huge) prefix of well-formed APP0
    ///     marker segments - comfortably exceeding
    ///     <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/> - that never contains a SOF0/SOF2
    ///     marker anywhere, followed by an endless zero-byte tail
    ///     (<see cref="InfiniteTailStream"/>) that a correctly bounded GetInfo must never reach.
    ///     Wraps the whole thing in a <see cref="BoundedReadStream"/> configured to throw a
    ///     distinct <see cref="InvalidOperationException"/> the instant more than
    ///     <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/> (plus a small slack) bytes are
    ///     read, so this test fails loudly - rather than reading unboundedly or hanging - if the
    ///     hard-limit ceiling were ever removed again (in which case scanning would consume the
    ///     entire oversized known prefix and then run into the never-ending tail). Proves GetInfo
    ///     instead throws <see cref="InvalidDataException"/> referencing the hard limit once that
    ///     ceiling is reached, exactly as it does for a genuinely truncated stream, rather than
    ///     continuing to read indefinitely.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_NoSofEverFound_StopsAtHardLimitWithInvalidDataException()
    {
        // Arrange: enough well-formed, non-SOF APP0 filler segments to comfortably exceed the
        // hard limit (with margin), so the hard limit is reached while scanning through
        // legitimate segment structure (efficient large jumps) rather than an unstructured byte
        // stream, followed by a never-ending zero-byte tail that a correct implementation must
        // never reach.
        const int fillerSegmentPayloadLength = 65_000;
        const int margin = 200_000;
        var segments = new List<byte[]>();
        var totalFillerBytes = 0;
        while (totalFillerBytes <= JpegCodec.MaxProbeHeaderBytesHardLimit + margin)
        {
            segments.Add(BuildSegment(0xE0, new byte[fillerSegmentPayloadLength]));
            totalFillerBytes += fillerSegmentPayloadLength + 4;
        }

        var knownPrefix = BuildJpeg([.. segments], entropyData: null, includeEoi: false);

        const int slack = 4096;
        using var stream = new BoundedReadStream(
            new InfiniteTailStream(knownPrefix),
            maxBytes: JpegCodec.MaxProbeHeaderBytesHardLimit + slack);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(stream));

        // Assert: GetInfo throws referencing the hard limit, having stopped well before the
        // BoundedReadStream's budget (and therefore never reached the never-ending tail), rather
        // than reading or buffering data without bound.
        Assert.Contains("hard limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves genuine, unconditional GetInfo/Load parity for the byte-based hard ceiling (see
    ///     the type-level remarks on <see cref="ImageInfo"/>): a JPEG whose leading marker-segment
    ///     data before the SOF0/SOF2 marker exceeds <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/>
    ///     is now rejected consistently by both <see cref="JpegCodec.Load(Stream)"/> (which
    ///     enforces the exact same ceiling on its own pre-SOF marker-segment walk) and
    ///     <see cref="JpegCodec.GetInfo(Stream)"/> - there is no accepted divergence between the
    ///     two for this input shape. Builds well-formed, non-SOF APP0 filler segments comfortably
    ///     exceeding <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/> (well under
    ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> in number, so only the byte-based ceiling
    ///     is exercised) followed by a would-be-valid, would-be-decodable DQT/DHT/SOF0/SOS-plus-
    ///     entropy-data tail identical in shape to the minimal fixture used elsewhere in this
    ///     file. Confirms both <see cref="JpegCodec.Load(Stream)"/> and
    ///     <see cref="JpegCodec.GetInfo(Stream)"/>, called on the exact same byte array, throw
    ///     <see cref="InvalidDataException"/> referencing the hard limit - proving true parity,
    ///     not an accepted exception.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfoVsLoad_LeadingMetadataExceedsHardLimit_BothThrowConsistently()
    {
        // Arrange: enough well-formed, non-SOF APP0 filler segments to comfortably exceed
        // MaxProbeHeaderBytesHardLimit (with margin, and well under MaxProbeSegmentCount in
        // count) before the SOF0 marker is ever seen, followed by an otherwise fully decodable
        // minimal JPEG tail (DQT/DHT/SOF0/SOS plus matching entropy data) - so the only reason
        // either method would reject this file is the hard byte ceiling itself, not any other
        // malformation.
        const int fillerSegmentPayloadLength = 65_000;
        const int margin = 200_000;
        var segments = new List<byte[]>();
        var totalFillerBytes = 0;
        while (totalFillerBytes <= JpegCodec.MaxProbeHeaderBytesHardLimit + margin)
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
        var loadException = Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
        var getInfoException = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));

        // Assert: both Load and GetInfo, on the exact same bytes, throw InvalidDataException
        // referencing the hard limit - true parity, not a documented exception.
        Assert.Contains("hard limit", loadException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hard limit", getInfoException.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Proves genuine, unconditional GetInfo/Load parity for the segment-count-based ceiling
    ///     too, not just the byte-based ceiling above: a JPEG with more than
    ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> non-terminating marker segments before the
    ///     SOF0/SOF2 marker - but comfortably under <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/>
    ///     in total bytes, so the byte-based ceiling could never be the reason either method
    ///     rejects it - is now rejected consistently by both <see cref="JpegCodec.Load(Stream)"/>
    ///     (which enforces the exact same segment-count ceiling on its own pre-SOF marker-segment
    ///     walk) and <see cref="JpegCodec.GetInfo(Stream)"/>. Builds more than
    ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> minimal (4-byte) APP0 filler segments -
    ///     totalling only a few kilobytes - followed by an otherwise fully decodable minimal JPEG
    ///     tail, and confirms both methods, called on the exact same bytes, throw
    ///     <see cref="InvalidDataException"/> referencing the segment limit.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfoVsLoad_LeadingSegmentCountExceedsLimit_BothThrowConsistently()
    {
        // Arrange: more than MaxProbeSegmentCount minimal 4-byte APP0 filler segments (well under
        // the byte-based hard limit in total), followed by an otherwise fully decodable minimal
        // JPEG tail.
        const int segmentCount = JpegCodec.MaxProbeSegmentCount + 50;
        var segments = new List<byte[]>();
        for (var i = 0; i < segmentCount; i++)
        {
            segments.Add(BuildSegment(0xE0));
        }

        segments.Add(BuildMinimalDqtSegment());
        segments.Add(BuildMinimalDhtSegment());
        segments.Add(BuildSofSegment(MarkerSof0, 16, 8, (1, 0x11, 0)));
        segments.Add(BuildSosSegment((1, 0x00)));

        var jpeg = BuildJpeg([.. segments], entropyData: [0x00, 0x00]);

        Assert.True(
            jpeg.Length < JpegCodec.MaxProbeHeaderBytes,
            "Test fixture must stay well below the byte-based soft/hard caps so only the " +
            "segment-count cap can plausibly trigger the failure for either method.");

        // Act
        var loadException = Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
        var getInfoException = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));

        // Assert: both Load and GetInfo, on the exact same bytes, throw InvalidDataException
        // referencing the segment limit - true parity, not a documented exception.
        Assert.Contains("segment", loadException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("segment", getInfoException.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Regression test proving the byte-based hard ceiling applies unconditionally to every
    ///     byte read, including the SOF0/SOF2 segment's own bytes - not merely the leading filler
    ///     segments that precede it - so a file whose SOF segment itself straddles
    ///     <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/> is rejected by both
    ///     <see cref="JpegCodec.Load(Stream)"/> and <see cref="JpegCodec.GetInfo(Stream)"/> rather
    ///     than being silently exempted by either. This guards against an off-by-condition bug
    ///     where the check is gated on "has a SOF0/SOF2 marker been found yet" using the state
    ///     <em>after</em> processing the current segment: since processing the SOF segment itself
    ///     is what sets that flag, such a bug would skip the check specifically for the one
    ///     segment whose own bytes push the cumulative count over the ceiling - accepting exactly
    ///     the input shape this test constructs. Builds leading APP0 filler segments sized so that
    ///     the cumulative byte count immediately before the SOF0 segment starts is just a few
    ///     bytes under <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/>, so the ceiling is not
    ///     yet exceeded when the SOF0 segment begins, but consuming the SOF0 segment's own bytes
    ///     (marker, length, and payload) pushes the cumulative count past the ceiling.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfoVsLoad_SofSegmentStraddlesHardLimit_BothThrowConsistently()
    {
        // Arrange: a SOF0 segment for a single component is exactly 13 bytes (2-byte marker,
        // 2-byte length, 1-byte precision, 2-byte height, 2-byte width, 1-byte component count,
        // 3-byte component descriptor). Build leading APP0 filler so that the cumulative byte
        // count immediately before the SOF0 segment is (MaxProbeHeaderBytesHardLimit - 5) - just
        // under the ceiling - so the SOF0 segment's own 13 bytes push the cumulative count to
        // (MaxProbeHeaderBytesHardLimit + 8), straddling the ceiling within the SOF segment itself.
        const int sofSegmentLength = 13;
        var desiredPreSofPos = JpegCodec.MaxProbeHeaderBytesHardLimit - 5;

        var segments = new List<byte[]>();
        var pos = 2; // Bytes consumed so far, starting immediately after the SOI marker.
        const int chunkPayloadLength = 65_000;
        while (pos + chunkPayloadLength + 4 < desiredPreSofPos - 4)
        {
            segments.Add(BuildSegment(0xE0, new byte[chunkPayloadLength]));
            pos += chunkPayloadLength + 4;
        }

        // Top up with one final, precisely sized filler segment to land exactly at
        // desiredPreSofPos before the SOF0 segment begins.
        var finalFillerPayloadLength = desiredPreSofPos - pos - 4;
        segments.Add(BuildSegment(0xE0, new byte[finalFillerPayloadLength]));
        pos += finalFillerPayloadLength + 4;

        Assert.Equal(desiredPreSofPos, pos);
        Assert.True(
            segments.Count < JpegCodec.MaxProbeSegmentCount,
            "Test fixture must stay well under the segment-count cap so only the byte-based hard limit can plausibly trigger the failure.");

        segments.Add(BuildSofSegment(MarkerSof0, 16, 8, (1, 0x11, 0)));

        var jpeg = BuildJpeg([.. segments], entropyData: null, includeEoi: false);
        Assert.True(
            pos + sofSegmentLength > JpegCodec.MaxProbeHeaderBytesHardLimit &&
            pos < JpegCodec.MaxProbeHeaderBytesHardLimit,
            "Test fixture must place the SOF0 segment so it straddles MaxProbeHeaderBytesHardLimit.");

        // Act
        var loadException = Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
        var getInfoException = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));

        // Assert: both Load and GetInfo, on the exact same bytes, throw InvalidDataException
        // referencing the hard limit even though the SOF0 segment - not merely the leading filler
        // - is what crosses the ceiling.
        Assert.Contains("hard limit", loadException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hard limit", getInfoException.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Regression test proving <see cref="JpegCodec.MaxProbeSegmentCount"/> counting excludes
    ///     not just the terminating SOF0/SOF2 marker but also an SOS-before-SOF marker, exactly
    ///     mirroring which marker kinds <c>ProbeDimensions</c>' segment-count check counts. Without
    ///     this exact alignment, a stream crafted so an SOS-before-SOF marker lands exactly on what
    ///     would otherwise be the segment-count-exceeding segment could make
    ///     <see cref="JpegCodec.Load(Stream)"/> throw the generic segment-count-limit message while
    ///     <see cref="JpegCodec.GetInfo(Stream)"/> throws its own, more specific "SOS marker
    ///     encountered before SOF" message for the same bytes - both still
    ///     <see cref="InvalidDataException"/>, but with diverging text. Builds exactly
    ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> minimal (4-byte) APP0 filler segments -
    ///     which, if the SOS marker below were also counted, would push the very next segment past
    ///     the ceiling - followed by an SOS marker with no SOF marker ever having appeared, and
    ///     asserts both <see cref="JpegCodec.Load(Stream)"/> and <see cref="JpegCodec.GetInfo(Stream)"/>
    ///     throw <see cref="InvalidDataException"/> referencing "SOS" rather than the segment-count
    ///     limit message, proving the SOS marker is excluded from the count identically by both.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfoVsLoad_SosBeforeSofAtSegmentCountBoundary_BothThrowSosMessageNotSegmentLimit()
    {
        // Arrange: exactly MaxProbeSegmentCount minimal 4-byte APP0 filler segments (well under
        // the byte-based hard limit in total), followed by an SOS marker with no SOF marker
        // ever having appeared.
        var segments = new List<byte[]>();
        for (var i = 0; i < JpegCodec.MaxProbeSegmentCount; i++)
        {
            segments.Add(BuildSegment(0xE0));
        }

        segments.Add(BuildSosSegment((1, 0x00)));

        var jpeg = BuildJpeg([.. segments], entropyData: null, includeEoi: false);

        // Act
        var loadException = Assert.Throws<InvalidDataException>(() => JpegCodec.Load(new MemoryStream(jpeg)));
        var getInfoException = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(new MemoryStream(jpeg)));

        // Assert: both Load and GetInfo, on the exact same bytes, throw InvalidDataException
        // referencing the SOS-before-SOF condition, not the segment-count limit - proving the SOS
        // marker itself is never counted toward MaxProbeSegmentCount by either method.
        Assert.Contains("SOS", loadException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SOS", getInfoException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("segment", loadException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("segment", getInfoException.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Regression test for the segment-count defense-in-depth cap: the byte-based
    ///     <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/> bounds total bytes read, but a
    ///     JPEG marker segment can be as small as 4 bytes (a 2-byte marker plus a 2-byte length
    ///     field), so a malformed stream built entirely from minimal-size segments could still
    ///     take millions of iterations before that byte ceiling is ever reached. Builds a known
    ///     prefix of many well-formed, minimal (4-byte) APP0 segments - more than
    ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> of them, but totalling only a few kilobytes,
    ///     nowhere close to either the soft cap or the byte-based hard limit - that never contains
    ///     a SOF0/SOF2 marker, followed by an endless zero-byte tail
    ///     (<see cref="InfiniteTailStream"/>). Wraps the whole thing in a
    ///     <see cref="BoundedReadStream"/> configured to throw if more than a small, generous
    ///     budget (comfortably covering both the known prefix and the probe buffer's eager
    ///     internal chunk reads, but still a tiny fraction of the byte-based soft cap and hard
    ///     limit) is ever read, so this test fails loudly - rather than reading unboundedly or
    ///     hanging - if the segment-count cap were removed and only the byte-based hard limit
    ///     remained (in which case scanning would continue, one minimal segment at a time, until
    ///     the byte-based hard limit was reached - many megabytes and well past this test's bound
    ///     - or, absent that limit too, forever).
    ///     Proves GetInfo instead throws <see cref="InvalidDataException"/> referencing the
    ///     segment limit almost immediately, well before the byte-based hard limit could ever be
    ///     approached.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_ManyMinimalSegmentsNoSof_StopsAtSegmentCountLimitBeforeByteLimit()
    {
        // Arrange: more than MaxProbeSegmentCount well-formed, minimal 4-byte APP0 segments
        // (marker + zero-length payload), none of which is a SOF0/SOF2 marker. The total byte
        // count stays tiny - nowhere near the soft cap, let alone the byte-based hard limit -
        // proving that the segment-count cap, not the byte cap, is what catches this attack shape.
        const int segmentCount = JpegCodec.MaxProbeSegmentCount + 50;
        var segments = new List<byte[]>();
        for (var i = 0; i < segmentCount; i++)
        {
            segments.Add(BuildSegment(0xE0));
        }

        var knownPrefix = BuildJpeg([.. segments], entropyData: null, includeEoi: false);

        Assert.True(
            knownPrefix.Length < JpegCodec.MaxProbeHeaderBytes,
            "Test fixture must stay well below the byte-based soft/hard caps so only the " +
            "segment-count cap can plausibly trigger the failure.");

        // The probe buffer's fast path always attempts a single, eager, fixed-size (4096-byte)
        // chunk read even when far fewer bytes are actually required, so the budget must
        // comfortably exceed that chunk size (not just the known prefix length) - while still
        // remaining a tiny fraction of the 1 MiB soft cap, so a stream that reads even
        // moderately further than one such chunk past the known prefix is still caught.
        const int budget = 32_768;
        using var stream = new BoundedReadStream(new InfiniteTailStream(knownPrefix), maxBytes: budget);

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => JpegCodec.GetInfo(stream));

        // Assert: GetInfo throws referencing the segment-count limit, having stopped within the
        // small BoundedReadStream budget (far below the byte-based hard limit, and even the soft
        // cap) rather than continuing to scan segment-by-segment without bound.
        Assert.Contains("segment", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            JpegCodec.MaxProbeSegmentCount.ToString(CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Boundary regression test: the segment-count ceiling must never reject a well-formed
    ///     file that <see cref="JpegCodec.Load(Stream)"/> would successfully decode merely
    ///     because its terminating SOF0/SOF2 marker happens to be exactly the segment that would
    ///     otherwise exceed <see cref="JpegCodec.MaxProbeSegmentCount"/>. Builds
    ///     <see cref="JpegCodec.MaxProbeSegmentCount"/> minimal (4-byte) non-SOF filler segments
    ///     followed immediately by a normal SOF0 segment (so the SOF marker is the
    ///     <c>MaxProbeSegmentCount + 1</c>-th marker segment scanned) and confirms GetInfo still
    ///     returns the correct dimensions, matching <see cref="JpegCodec.Load(Stream)"/>'s own
    ///     decoded result exactly - proving the segment-count check only ever applies to
    ///     non-terminating segments, never to the SOF0/SOF2 marker itself.
    /// </summary>
    [Fact]
    public void JpegCodec_GetInfo_SofAsSegmentImmediatelyAfterSegmentCountLimit_StillSucceeds()
    {
        // Arrange: exactly MaxProbeSegmentCount non-SOF marker segments (minimal APP0 filler,
        // plus the DQT/DHT segments Load itself needs), then a normal SOF0 segment as the very
        // next (MaxProbeSegmentCount + 1-th) marker segment, followed by SOS/entropy data so
        // Load can fully decode the same bytes.
        var segments = new List<byte[]>();
        for (var i = 0; i < JpegCodec.MaxProbeSegmentCount - 2; i++)
        {
            segments.Add(BuildSegment(0xE0));
        }

        segments.Add(BuildMinimalDqtSegment());
        segments.Add(BuildMinimalDhtSegment());
        segments.Add(BuildSofSegment(MarkerSof0, 16, 8, (1, 0x11, 0)));
        segments.Add(BuildSosSegment((1, 0x00)));

        var jpeg = BuildJpeg([.. segments], entropyData: [0x00, 0x00]);

        // Act
        var info = JpegCodec.GetInfo(new MemoryStream(jpeg));
        var surface = JpegCodec.Load(new MemoryStream(jpeg));

        // Assert: GetInfo succeeds (rather than throwing merely because the SOF marker landed
        // immediately past the segment-count ceiling) and matches Load's own decoded
        // dimensions/channel count exactly.
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
