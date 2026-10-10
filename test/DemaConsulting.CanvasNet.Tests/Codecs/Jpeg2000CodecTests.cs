// cspell:ignore bypass termall vcausal segsym pterm ppm ppt tlm plt crg poc cprl rpcl pcrl rlcp lrcp sop eph pclr cmap cdef bpcc colr
using System.Diagnostics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the <see cref="Jpeg2000Codec"/> class.
/// </summary>
/// <remarks>
///     The tests drive the decoder with streams produced by the independent, test-only
///     <see cref="Jpeg2000TestEncoder"/> so that every coding feature of ITU-T T.800 Part 1 is exercised,
///     plus a hand-verified MQ coder vector from ITU-T T.88 that guards against symmetric
///     encoder/decoder defects.
/// </remarks>
public class Jpeg2000CodecTests
{
    /// <summary>The T.88 Annex H.2 test sequence (256 decisions, MSB first).</summary>
    private static readonly byte[] MqTestInput =
        [0x00, 0x02, 0x00, 0x51, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x52, 0x87, 0x2A, 0xAA, 0xAA, 0xAA, 0xAA,
         0x82, 0xC0, 0x20, 0x00, 0xFC, 0xD7, 0x9E, 0xF6, 0xBF, 0x7F, 0xED, 0x90, 0x4F, 0x46, 0xA3, 0xBF];

    /// <summary>The T.88 Annex H.2 encoded sequence (without the terminating marker).</summary>
    private static readonly byte[] MqTestOutput =
        [0x84, 0xC7, 0x3B, 0xFC, 0xE1, 0xA1, 0x43, 0x04, 0x02, 0x20, 0x00, 0x00, 0x41, 0x0D, 0xBB, 0x86,
         0xF4, 0x31, 0x7F, 0xFF, 0x88, 0xFF, 0x37, 0x47, 0x1A, 0xDB, 0x6A, 0xDF];

    private static readonly Type[] AllowedFailures = [typeof(InvalidDataException), typeof(UnsupportedImageFeatureException)];

    // ------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------

    private static J2kOptions Rev(int levels = 3) => new() { Levels = levels };

    private static J2kOptions Irrev(int levels = 3) => new() { Levels = levels, Reversible = false };

    private static byte[] Encode(J2kImage image, J2kOptions options) => Jpeg2000TestEncoder.Encode(image, options);

    private static int Floor(int a, int b) => (int)Math.Floor(a / (double)b);

    /// <summary>Computes the byte the decoder is expected to produce for pixel (x, y) of component c.</summary>
    private static int Expected(J2kImage image, int c, int x, int y)
    {
        var comp = image.Components[c];
        var cx = Math.Clamp(Floor(image.XOsiz + x, comp.Dx) - image.CompX0(c), 0, image.CompWidth(c) - 1);
        var cy = Math.Clamp(Floor(image.YOsiz + y, comp.Dy) - image.CompY0(c), 0, image.CompHeight(c) - 1);
        return Jpeg2000TestEncoder.ExpectedByte(comp.Samples[(cy * image.CompWidth(c)) + cx], comp.Depth, comp.Signed);
    }

    /// <summary>Decodes and returns the largest absolute sample error against the source image.</summary>
    private static int MaxError(J2kImage image, byte[] data)
    {
        var decoded = Jpeg2000Codec.Decode(data);
        Assert.Equal(image.Width, decoded.Width);
        Assert.Equal(image.Height, decoded.Height);
        Assert.Equal(image.Components.Length, decoded.ColorChannelCount);
        var max = 0;
        var n = decoded.ColorChannelCount;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                for (var c = 0; c < n; c++)
                {
                    var got = decoded.ColorSamples[((((y * image.Width) + x) * n) + c)];
                    max = Math.Max(max, Math.Abs(got - Expected(image, c, x, y)));
                }
            }
        }

        return max;
    }

    private static void AssertExact(J2kImage image, J2kOptions options)
    {
        var data = Encode(image, options);
        Assert.Equal(0, MaxError(image, data));
    }

    private static void AssertClose(J2kImage image, J2kOptions options, int tolerance = 3)
    {
        var data = Encode(image, options);
        Assert.True(MaxError(image, data) <= tolerance);
    }

    private static J2kImage Img(int w, int h, int comps = 3, int depth = 8, int seed = 1, int noise = 24, bool signed = false) =>
        Jpeg2000TestEncoder.MakeImage(w, h, comps, depth, seed, noise, signed);

    private static void AssertRejectedOrDecoded(byte[] data)
    {
        try
        {
            _ = Jpeg2000Codec.Decode(data);
        }
        catch (Exception ex) when (AllowedFailures.Contains(ex.GetType()))
        {
            // Expected failure type.
        }
    }

    // ------------------------------------------------------------------------------------------
    // MQ coder (T.88)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Tests that the test encoder's MQ coder reproduces the T.88 Annex H.2 reference code stream.
    /// </summary>
    [Fact]
    public void Jpeg2000Codec_MqEncoder_T88TestSequence_MatchesReferenceCodeStream()
    {
        var enc = new Jpeg2000TestEncoder.MqEncoder();
        enc.ResetContexts(false);
        enc.Start();
        foreach (var b in MqTestInput)
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                enc.Encode((b >> bit) & 1, 1);
            }
        }

        Assert.Equal(MqTestOutput, enc.Flush());
    }

    /// <summary>
    ///     Tests that the production MQ decoder recovers the T.88 Annex H.2 test sequence from the reference code stream.
    /// </summary>
    [Fact]
    public void Jpeg2000Codec_MqDecoder_T88ReferenceCodeStream_RecoversTestSequence()
    {
        var dec = new Jpeg2000Codec.MqDecoder();
        dec.ResetContexts();
        dec.Init(MqTestOutput, 0, MqTestOutput.Length);
        var decoded = new byte[MqTestInput.Length];
        for (var i = 0; i < decoded.Length * 8; i++)
        {
            decoded[i / 8] = (byte)((decoded[i / 8] << 1) | dec.DecodeBit(1));
        }

        Assert.Equal(MqTestInput, decoded);
    }

    // ------------------------------------------------------------------------------------------
    // Reversible (5/3) round trips
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests lossless coding of a single-component image with no decomposition.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_ReversibleNoDecomposition_RoundTripsExactly() =>
        AssertExact(Img(23, 17, 1), Rev(0));

    /// <summary>Tests lossless coding with different decomposition levels.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Jpeg2000Codec_Decode_ReversibleLevels_RoundTripsExactly(int levels) =>
        AssertExact(Img(61, 47, 1), Rev(levels));

    /// <summary>Tests lossless coding of odd and tiny image sizes.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 9)]
    [InlineData(9, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    [InlineData(65, 33)]
    public void Jpeg2000Codec_Decode_ReversibleOddSizes_RoundTripsExactly(int w, int h) =>
        AssertExact(Img(w, h, 1), Rev(4));

    /// <summary>Tests lossless coding with the reversible component transform.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_ReversibleRct_RoundTripsExactly()
    {
        var o = Rev();
        o.Mct = true;
        AssertExact(Img(40, 30), o);
    }

    /// <summary>Tests lossless coding of a four-component image.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_FourComponents_RoundTripsExactly() =>
        AssertExact(Img(33, 21, 4), Rev());

    /// <summary>Tests lossless coding with large non-square code-blocks.</summary>
    [Theory]
    [InlineData(2, 2)]
    [InlineData(6, 4)]
    [InlineData(4, 6)]
    [InlineData(10, 2)]
    public void Jpeg2000Codec_Decode_CodeBlockSizes_RoundTripsExactly(int xcb, int ycb)
    {
        var o = Rev();
        o.CodeBlockWidthExp = xcb;
        o.CodeBlockHeightExp = ycb;
        AssertExact(Img(90, 70, 1), o);
    }

    /// <summary>Tests lossless coding at other bit depths and signedness.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(10, false)]
    [InlineData(12, true)]
    [InlineData(16, false)]
    [InlineData(16, true)]
    [InlineData(8, true)]
    public void Jpeg2000Codec_Decode_DepthAndSign_ScalesToEightBits(int depth, bool signed)
    {
        var o = Rev();
        AssertExact(Img(37, 29, 1, depth, 5, depth > 2 ? 3 : 0, signed), o);
    }

    /// <summary>Tests that a signed multi-component image with RCT round trips.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_SignedRct_RoundTripsExactly()
    {
        var o = Rev();
        o.Mct = true;
        AssertExact(Img(21, 19, 3, 8, 9, 10, true), o);
    }

    // ------------------------------------------------------------------------------------------
    // Irreversible (9/7)
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests the 9/7 path with expounded quantization.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Irreversible_WithinTolerance() =>
        AssertClose(Img(64, 48, 1), Irrev());

    /// <summary>Tests the 9/7 path with the ICT and non-zero step size mantissas.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_IrreversibleIctMantissa_WithinTolerance()
    {
        var o = Irrev();
        o.Mct = true;
        o.QuantMantissa = true;
        AssertClose(Img(50, 41), o, 4);
    }

    /// <summary>Tests the 9/7 path with derived quantization.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_IrreversibleDerivedQuantization_WithinTolerance()
    {
        var o = Irrev(4);
        o.QuantStyle = 1;
        o.QuantMantissa = true;
        AssertClose(Img(70, 50, 1), o, 6);
    }

    /// <summary>Tests the 9/7 path at 12 bits.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_IrreversibleTwelveBit_WithinTolerance() =>
        AssertClose(Img(33, 33, 1, 12, 2, 20), Irrev(), 2);

    // ------------------------------------------------------------------------------------------
    // Code-block styles
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests every code-block style flag and several combinations.</summary>
    [Theory]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x04)]
    [InlineData(0x08)]
    [InlineData(0x10)]
    [InlineData(0x20)]
    [InlineData(0x01 | 0x04)]
    [InlineData(0x01 | 0x02)]
    [InlineData(0x01 | 0x08)]
    [InlineData(0x04 | 0x08 | 0x20)]
    [InlineData(0x3F)]
    public void Jpeg2000Codec_Decode_CodeBlockStyle_RoundTripsExactly(int style)
    {
        var o = Rev();
        o.CodeBlockStyle = style;
        AssertExact(Img(48, 40, 1, 8, 3, 40), o);
    }

    /// <summary>Tests the code-block styles with multiple layers (segment splitting across packets).</summary>
    [Theory]
    [InlineData(0x01)]
    [InlineData(0x04)]
    [InlineData(0x05)]
    [InlineData(0x08)]
    public void Jpeg2000Codec_Decode_CodeBlockStyleWithLayers_RoundTripsExactly(int style)
    {
        var o = Rev();
        o.CodeBlockStyle = style;
        o.Layers = 5;
        AssertExact(Img(48, 40, 1, 8, 4, 40), o);
    }

    /// <summary>Tests the code-block styles on the irreversible path.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_IrreversibleBypassTermall_WithinTolerance()
    {
        var o = Irrev();
        o.CodeBlockStyle = 0x01 | 0x04 | 0x08;
        AssertClose(Img(40, 40, 1), o);
    }

    // ------------------------------------------------------------------------------------------
    // Tiles, precincts, layers, progressions
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests tiling.</summary>
    [Theory]
    [InlineData(32, 32)]
    [InlineData(17, 13)]
    [InlineData(1, 1)]
    [InlineData(100, 20)]
    public void Jpeg2000Codec_Decode_Tiles_RoundTripsExactly(int tw, int th)
    {
        var o = Rev(2);
        o.TileWidth = tw;
        o.TileHeight = th;
        AssertExact(Img(70, 45, 3), o);
    }

    /// <summary>Tests tiling with a tile grid offset and an image offset.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TileAndImageOffsets_RoundTripsExactly()
    {
        var image = Img(50, 37, 3);
        image.XOsiz = 5;
        image.YOsiz = 3;
        image.Xsiz += 5;
        image.Ysiz += 3;
        var o = Rev(2);
        o.TileWidth = 20;
        o.TileHeight = 16;
        o.TileXOffset = 4;
        o.TileYOffset = 2;
        AssertExact(image, o);
    }

    /// <summary>Tests precincts.</summary>
    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 3)]
    [InlineData(6, 6)]
    public void Jpeg2000Codec_Decode_Precincts_RoundTripsExactly(int ppx, int ppy)
    {
        var o = Rev(3);
        o.Precincts = [(ppx, ppy)];
        o.CodeBlockWidthExp = 3;
        o.CodeBlockHeightExp = 3;
        AssertExact(Img(80, 70, 2 + 1), o);
    }

    /// <summary>Tests per-resolution precinct sizes.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PerResolutionPrecincts_RoundTripsExactly()
    {
        var o = Rev(3);
        o.Precincts = [(3, 3), (4, 4), (5, 5), (6, 6)];
        o.CodeBlockWidthExp = 3;
        o.CodeBlockHeightExp = 3;
        o.Layers = 2;
        AssertExact(Img(100, 90, 1), o);
    }

    /// <summary>Tests multiple quality layers.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Jpeg2000Codec_Decode_Layers_RoundTripsExactly(int layers)
    {
        var o = Rev();
        o.Layers = layers;
        AssertExact(Img(45, 38, 3), o);
    }

    /// <summary>Tests every progression order with layers, precincts, tiles and subsampled components.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Jpeg2000Codec_Decode_ProgressionOrders_RoundTripsExactly(int order)
    {
        var o = Rev(3);
        o.Progression = order;
        o.Layers = 3;
        o.Precincts = [(4, 4), (5, 4), (5, 5), (6, 5)];
        o.CodeBlockWidthExp = 3;
        o.CodeBlockHeightExp = 3;
        o.TileWidth = 50;
        o.TileHeight = 41;
        var image = Img(83, 77, 3);
        image.Components[1].Dx = 2;
        image.Components[1].Dy = 2;
        image.Components[2].Dx = 3;
        image.Components[2].Dy = 1;
        Resample(image);
        AssertExact(image, o);
    }

    /// <summary>Tests every progression order with the image origin offset from zero.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Jpeg2000Codec_Decode_PositionProgressionWithOffsets_RoundTripsExactly(int order)
    {
        var o = Rev(2);
        o.Progression = order;
        o.Precincts = [(4, 4)];
        o.CodeBlockWidthExp = 3;
        o.CodeBlockHeightExp = 3;
        o.TileWidth = 40;
        o.TileHeight = 40;
        o.TileXOffset = 3;
        o.TileYOffset = 5;
        var image = Img(60, 55, 3);
        image.XOsiz = 7;
        image.YOsiz = 9;
        image.Xsiz += 7;
        image.Ysiz += 9;
        image.Components[1].Dx = 2;
        image.Components[2].Dy = 2;
        Resample(image);
        AssertExact(image, o);
    }

    /// <summary>Replaces the samples of a subsampled image by a grid of the right size.</summary>
    private static void Resample(J2kImage image)
    {
        var rng = new Random(11);
        for (var c = 0; c < image.Components.Length; c++)
        {
            var n = image.CompWidth(c) * image.CompHeight(c);
            var s = new int[n];
            for (var i = 0; i < n; i++)
            {
                s[i] = Math.Clamp(((i * 5) % 200) + rng.Next(0, 40), 0, 255);
            }

            image.Components[c].Samples = s;
        }
    }

    /// <summary>Tests subsampled components.</summary>
    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 2)]
    [InlineData(4, 4)]
    [InlineData(3, 5)]
    public void Jpeg2000Codec_Decode_SubsampledComponents_UpsamplesToImageGrid(int dx, int dy)
    {
        var image = Img(47, 39, 3);
        image.Components[1].Dx = dx;
        image.Components[1].Dy = dy;
        image.Components[2].Dx = dx;
        image.Components[2].Dy = dy;
        Resample(image);
        AssertExact(image, Rev(2));
    }

    /// <summary>Tests per-component decomposition levels (COC/QCC).</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PerComponentLevels_RoundTripsExactly()
    {
        var o = Rev(3);
        o.ComponentLevels = [3, 1, 2];
        AssertExact(Img(50, 40, 3), o);
    }

    /// <summary>Tests per-component levels on the irreversible path (QCC with differing steps).</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PerComponentLevelsIrreversible_WithinTolerance()
    {
        var o = Irrev(3);
        o.ComponentLevels = [3, 1, 2];
        AssertClose(Img(50, 40, 3), o);
    }

    /// <summary>Tests per-tile coding overrides.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TileHeaderOverrides_RoundTripsExactly()
    {
        var o = Rev(2);
        o.TileWidth = 32;
        o.TileHeight = 32;
        var t1 = o.Clone();
        t1.Levels = 1;
        t1.CodeBlockStyle = 0x04;
        t1.Progression = 3;
        t1.Layers = 2;
        o.TileOverrides[1] = t1;
        var t2 = o.Clone();
        t2.Mct = false;
        t2.Levels = 0;
        o.TileOverrides[2] = t2;
        AssertExact(Img(70, 40, 3), o);
    }

    // ------------------------------------------------------------------------------------------
    // POC, SOP/EPH, PPM/PPT, tile-parts, misc markers
    // ------------------------------------------------------------------------------------------

    private static List<J2kPoc> TestPoc(int layers, int comps) =>
    [
        new(0, 0, layers, 2, comps, 4),
        new(0, 0, Math.Max(1, layers - 1), 4, comps, 1),
        new(0, 0, layers, 4, comps, 0),
    ];

    /// <summary>Tests progression order changes in the main header.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PocMainHeader_RoundTripsExactly()
    {
        var o = Rev(3);
        o.Layers = 3;
        o.Poc = TestPoc(3, 3);
        AssertExact(Img(40, 40, 3), o);
    }

    /// <summary>Tests progression order changes in the main header with several tiles.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PocMainHeaderWithTiles_RoundTripsExactly()
    {
        var o = Rev(3);
        o.Layers = 3;
        o.Poc = TestPoc(3, 3);
        o.TileWidth = 24;
        o.TileHeight = 24;
        AssertExact(Img(50, 40, 3), o);
    }

    /// <summary>Tests progression order changes in the tile-part header.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PocTileHeader_RoundTripsExactly()
    {
        var o = Rev(3);
        o.Layers = 3;
        o.Poc = TestPoc(3, 3);
        o.PocInTileHeader = true;
        o.TileWidth = 24;
        o.TileHeight = 24;
        AssertExact(Img(50, 40, 3), o);
    }

    /// <summary>Tests that a tile-header POC replaces the main-header POC.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PocTileOverridesMain_RoundTripsExactly()
    {
        var o = Rev(3);
        o.Layers = 2;
        o.Poc = TestPoc(2, 3);
        o.TileWidth = 24;
        o.TileHeight = 40;
        var t = o.Clone();
        t.Poc = [new J2kPoc(0, 0, 2, 4, 3, 3)];
        o.TileOverrides[1] = t;
        AssertExact(Img(48, 40, 3), o);
    }

    /// <summary>Tests SOP and EPH markers.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Jpeg2000Codec_Decode_SopEph_RoundTripsExactly(bool sop, bool eph)
    {
        var o = Rev(2);
        o.Sop = sop;
        o.Eph = eph;
        o.Layers = 2;
        o.TileWidth = 30;
        o.TileHeight = 30;
        AssertExact(Img(50, 40, 3), o);
    }

    /// <summary>Tests packed packet headers in PPM marker segments.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(7, 1)]
    [InlineData(40, 3)]
    public void Jpeg2000Codec_Decode_Ppm_RoundTripsExactly(int chunk, int tileParts)
    {
        var o = Rev(2);
        o.HeaderMode = J2kHeaderMode.Ppm;
        o.PackedChunkSize = chunk;
        o.TileParts = tileParts;
        o.Layers = 2;
        o.Eph = true;
        o.TileWidth = 30;
        o.TileHeight = 30;
        AssertExact(Img(50, 40, 3), o);
    }

    /// <summary>Tests packed packet headers in PPT marker segments.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(30, 4)]
    public void Jpeg2000Codec_Decode_Ppt_RoundTripsExactly(int chunk, int tileParts)
    {
        var o = Rev(2);
        o.HeaderMode = J2kHeaderMode.Ppt;
        o.PackedChunkSize = chunk;
        o.TileParts = tileParts;
        o.Layers = 2;
        o.Sop = true;
        o.TileWidth = 30;
        o.TileHeight = 30;
        AssertExact(Img(50, 40, 3), o);
    }

    /// <summary>Tests multiple tile-parts per tile.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TileParts_RoundTripsExactly()
    {
        var o = Rev(3);
        o.TileParts = 4;
        o.Layers = 3;
        o.TileWidth = 40;
        o.TileHeight = 40;
        AssertExact(Img(70, 50, 3), o);
    }

    /// <summary>Tests TLM, PLT, CRG, COM and reserved markers.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_ExtraMarkers_AreSkipped()
    {
        var o = Rev(2);
        o.ExtraMarkers = true;
        o.TileParts = 2;
        o.Layers = 2;
        o.TileWidth = 40;
        o.TileHeight = 40;
        AssertExact(Img(70, 50, 3), o);
    }

    /// <summary>Tests a final tile-part with Psot = 0 and no EOC marker.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Jpeg2000Codec_Decode_ZeroPsotMissingEoc_RoundTripsExactly(bool zeroPsot, bool omitEoc)
    {
        var o = Rev(2);
        o.ZeroPsotOnLast = zeroPsot;
        o.OmitEoc = omitEoc;
        o.TileWidth = 40;
        o.TileHeight = 40;
        AssertExact(Img(70, 50, 3), o);
    }

    // ------------------------------------------------------------------------------------------
    // ROI
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests maximum-shift region of interest coding.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Jpeg2000Codec_Decode_RoiMaxShift_RoundTripsExactly(bool layers)
    {
        var o = Rev(3);
        o.RoiShift = 13;
        o.Layers = layers ? 3 : 1;
        AssertExact(Img(50, 44, 1), o);
    }

    /// <summary>Tests ROI on a subset of the components.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RoiOnSubsetOfComponents_RoundTripsExactly()
    {
        var o = Rev(2);
        o.RoiShift = 13;
        o.RoiComponents = [1];
        AssertExact(Img(40, 40, 3), o);
    }

    /// <summary>Tests ROI on the irreversible path.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RoiIrreversible_WithinTolerance()
    {
        var o = Irrev(3);
        o.RoiShift = 14;
        AssertClose(Img(40, 40, 1), o);
    }

    // ------------------------------------------------------------------------------------------
    // JP2 container
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests a JP2 wrapper with an sRGB color specification.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2Srgb_ReportsColorSpace()
    {
        var image = Img(20, 15, 3);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(2)), new J2kJp2Options { WriteBpcc = true });
        var decoded = Jpeg2000Codec.Decode(jp2);
        Assert.Equal(Jpeg2000ColorSpace.Srgb, decoded.ColorSpace);
        Assert.False(decoded.HasAlpha);
        Assert.Equal(0, MaxError(image, jp2));
    }

    /// <summary>Tests a grayscale JP2 and a CMYK JP2.</summary>
    [Theory]
    [InlineData(17, 1, Jpeg2000ColorSpace.Gray)]
    [InlineData(12, 4, Jpeg2000ColorSpace.Cmyk)]
    public void Jpeg2000Codec_Decode_Jp2EnumeratedColorSpace_ReportsColorSpace(int enumCs, int comps, Jpeg2000ColorSpace expected)
    {
        var image = Img(20, 15, comps);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(2)), new J2kJp2Options { EnumCs = enumCs });
        Assert.Equal(expected, Jpeg2000Codec.Decode(jp2).ColorSpace);
        Assert.Equal(0, MaxError(image, jp2));
    }

    /// <summary>Tests that sYCC data is converted to sRGB.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2Sycc_ConvertsToSrgb()
    {
        var image = Img(8, 8, 3, 8, 1, 0);
        foreach (var c in image.Components)
        {
            Array.Fill(c.Samples, 128);
        }

        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options { EnumCs = 18 });
        var decoded = Jpeg2000Codec.Decode(jp2);
        Assert.Equal(Jpeg2000ColorSpace.Srgb, decoded.ColorSpace);
        Assert.All(decoded.ColorSamples, v => Assert.InRange((int)v, 126, 130));
    }

    /// <summary>Tests that an ICC profile is reported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2IccProfile_IsReported()
    {
        var image = Img(10, 10, 3);
        byte[] icc = [1, 2, 3, 4, 5, 6, 7, 8];
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options { Icc = icc });
        Assert.Equal(icc, Jpeg2000Codec.Decode(jp2).IccProfile);
    }

    /// <summary>Tests extended-length and to-end-of-file codestream boxes.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Jpeg2000Codec_Decode_Jp2BoxLengthForms_AreHandled(bool extended, bool toEnd)
    {
        var image = Img(10, 10, 1);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options { EnumCs = 17, ExtendedLength = extended, ZeroLength = toEnd });
        Assert.Equal(0, MaxError(image, jp2));
    }

    /// <summary>Tests palette mapping.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2Palette_MapsIndicesToColors()
    {
        var image = Img(12, 9, 1, 4, 1, 0);
        int[][] palette = Enumerable.Range(0, 16).Select(i => new[] { i * 16, 255 - (i * 16), i * 8 }).ToArray();
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(1)),
            new J2kJp2Options
            {
                Palette = palette,
                PaletteDepths = [8, 8, 8],
                Cmap = [(0, 1, 0), (0, 1, 1), (0, 1, 2)],
            });
        var decoded = Jpeg2000Codec.Decode(jp2);
        Assert.Equal(3, decoded.ColorChannelCount);
        for (var i = 0; i < image.Width * image.Height; i++)
        {
            var e = palette[image.Components[0].Samples[i]];
            Assert.Equal(e[0], decoded.ColorSamples[i * 3]);
            Assert.Equal(e[1], decoded.ColorSamples[(i * 3) + 1]);
            Assert.Equal(e[2], decoded.ColorSamples[(i * 3) + 2]);
        }
    }

    /// <summary>Tests that <c>BitDepth</c> reports the source depth and <c>HasPalette</c> reports palette expansion.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_BitDepthAndHasPalette_ReportSourceFormat()
    {
        var plain = Img(6, 5, 1, 4, 1, 0);
        var plainDecoded = Jpeg2000Codec.Decode(Jpeg2000TestEncoder.WrapJp2(plain, Encode(plain, Rev(1)), new J2kJp2Options { EnumCs = 17 }));
        Assert.Equal(4, plainDecoded.BitDepth);
        Assert.False(plainDecoded.HasPalette);

        var image = Img(6, 5, 1, 4, 1, 0);
        int[][] palette = Enumerable.Range(0, 16).Select(i => new[] { i, i, i }).ToArray();
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(1)),
            new J2kJp2Options { Palette = palette, PaletteDepths = [8, 8, 8], Cmap = [(0, 1, 0), (0, 1, 1), (0, 1, 2)] });
        var decoded = Jpeg2000Codec.Decode(jp2);
        Assert.Equal(4, decoded.BitDepth);
        Assert.True(decoded.HasPalette);
    }

    /// <summary>Tests a mapping that mixes direct and palette channels, with a 16-bit palette column.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2PaletteMixedDirectChannel_Maps()
    {
        var image = Img(12, 9, 2, 4, 1, 0);
        int[][] palette = Enumerable.Range(0, 16).Select(i => new[] { i * 4096, i * 16 }).ToArray();
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(1)),
            new J2kJp2Options
            {
                Palette = palette,
                PaletteDepths = [16, 8],
                Cmap = [(1, 0, 0), (0, 1, 0), (0, 1, 1)],
            });
        var decoded = Jpeg2000Codec.Decode(jp2);
        Assert.Equal(3, decoded.ColorChannelCount);
        var i0 = image.Components[0].Samples[0];
        Assert.Equal(Jpeg2000TestEncoder.ExpectedByte(image.Components[1].Samples[0], 4, false), decoded.ColorSamples[0]);
        Assert.Equal((byte)Math.Min(255, ((palette[i0][0] * 255) + 32767) / 65535), decoded.ColorSamples[1]);
        Assert.Equal(palette[i0][1], decoded.ColorSamples[2]);
    }

    /// <summary>Tests an alpha channel declared by cdef.</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void Jpeg2000Codec_Decode_Jp2Cdef_ProducesAlpha(int type, bool premultiplied)
    {
        var image = Img(14, 11, 4);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(2)),
            new J2kJp2Options { Cdef = [(0, 0, 1), (1, 0, 2), (2, 0, 3), (3, type, 0)] });
        var decoded = Jpeg2000Codec.Decode(jp2);
        Assert.Equal(3, decoded.ColorChannelCount);
        Assert.True(decoded.HasAlpha);
        Assert.Equal(premultiplied, decoded.AlphaPremultiplied);
        for (var i = 0; i < image.Width * image.Height; i++)
        {
            Assert.Equal(image.Components[3].Samples[i], decoded.AlphaSamples![i]);
        }
    }

    /// <summary>Tests that cdef can reorder color channels.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2CdefReorder_SwapsChannels()
    {
        var image = Img(9, 9, 3);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(1)),
            new J2kJp2Options { Cdef = [(0, 0, 3), (1, 0, 2), (2, 0, 1)] });
        var decoded = Jpeg2000Codec.Decode(jp2);
        for (var i = 0; i < 81; i++)
        {
            Assert.Equal(image.Components[0].Samples[i], decoded.ColorSamples[(i * 3) + 2]);
            Assert.Equal(image.Components[2].Samples[i], decoded.ColorSamples[i * 3]);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests that Load produces a surface with the decoded pixels.</summary>
    [Fact]
    public void Jpeg2000Codec_Load_Stream_ReturnsPixels()
    {
        var image = Img(13, 9, 3);
        using var stream = new MemoryStream(Encode(image, Rev(2)));
        using var surface = Jpeg2000Codec.Load(stream);
        Assert.Equal(13, surface.Width);
        Assert.Equal(9, surface.Height);
        var p = surface.GetRowSpan(4)[5];
        Assert.Equal(Expected(image, 0, 5, 4), p.R);
        Assert.Equal(Expected(image, 1, 5, 4), p.G);
        Assert.Equal(Expected(image, 2, 5, 4), p.B);
        Assert.Equal(255, p.A);
    }

    /// <summary>Tests Load of a grayscale image.</summary>
    [Fact]
    public void Jpeg2000Codec_Load_Grey_ExpandsToRgb()
    {
        var image = Img(7, 7, 1);
        using var surface = Jpeg2000Codec.Load(new MemoryStream(Encode(image, Rev(1))));
        var p = surface.GetRowSpan(2)[3];
        Assert.Equal(p.R, p.G);
        Assert.Equal(p.G, p.B);
        Assert.Equal(Expected(image, 0, 3, 2), p.R);
    }

    /// <summary>Tests Load of an image with alpha.</summary>
    [Fact]
    public void Jpeg2000Codec_Load_Alpha_PopulatesAlpha()
    {
        var image = Img(7, 7, 4);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(1)),
            new J2kJp2Options { Cdef = [(0, 0, 1), (1, 0, 2), (2, 0, 3), (3, 1, 0)] });
        using var surface = Jpeg2000Codec.Load(new MemoryStream(jp2));
        Assert.Equal(Expected(image, 3, 1, 1), surface.GetRowSpan(1)[1].A);
    }

    /// <summary>Tests Load from a file path.</summary>
    [Fact]
    public void Jpeg2000Codec_Load_Path_ReturnsPixels()
    {
        var path = Path.Combine(Path.GetTempPath(), "j2k-" + Guid.NewGuid().ToString("N") + ".j2k");
        try
        {
            File.WriteAllBytes(path, Encode(Img(8, 8, 1), Rev(1)));
            using var surface = Jpeg2000Codec.Load(path);
            Assert.Equal(8, surface.Width);
            Assert.Equal(8, Jpeg2000Codec.GetInfo(path).Height);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Tests GetInfo.</summary>
    [Fact]
    public void Jpeg2000Codec_GetInfo_RawCodestream_ReportsDimensionsAndChannels()
    {
        var info = Jpeg2000Codec.GetInfo(new MemoryStream(Encode(Img(31, 17, 3), Rev(2))));
        Assert.Equal(31, info.Width);
        Assert.Equal(17, info.Height);
        Assert.Equal(3, info.Channels);
        Assert.False(info.HasAlpha);
    }

    /// <summary>Tests GetInfo on JP2 with alpha.</summary>
    [Fact]
    public void Jpeg2000Codec_GetInfo_Jp2WithAlpha_CountsAlphaChannel()
    {
        var image = Img(11, 12, 4);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(1)),
            new J2kJp2Options { Cdef = [(0, 0, 1), (1, 0, 2), (2, 0, 3), (3, 1, 0)] });
        var info = Jpeg2000Codec.GetInfo(new MemoryStream(jp2));
        Assert.Equal(4, info.Channels);
        Assert.True(info.HasAlpha);
    }

    /// <summary>Tests the image offset is reflected in GetInfo dimensions.</summary>
    [Fact]
    public void Jpeg2000Codec_GetInfo_ImageOffset_ReportsWidthMinusOffset()
    {
        var image = Img(20, 10, 1);
        image.XOsiz = 5;
        image.Xsiz += 5;
        var info = Jpeg2000Codec.GetInfo(new MemoryStream(Encode(image, Rev(1))));
        Assert.Equal(20, info.Width);
    }

    /// <summary>Tests null and empty arguments.</summary>
    [Fact]
    public void Jpeg2000Codec_NullOrEmptyArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Jpeg2000Codec.Load((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => Jpeg2000Codec.Load((string)null!));
        Assert.Throws<ArgumentException>(() => Jpeg2000Codec.Load(string.Empty));
        Assert.Throws<ArgumentNullException>(() => Jpeg2000Codec.GetInfo((Stream)null!));
        Assert.Throws<ArgumentNullException>(() => Jpeg2000Codec.GetInfo((string)null!));
        Assert.Throws<ArgumentNullException>(() => Jpeg2000Codec.Decode((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => Jpeg2000Codec.Decode((Stream)null!));
    }

    /// <summary>Tests a missing file.</summary>
    [Fact]
    public void Jpeg2000Codec_Load_MissingFile_ThrowsFileNotFound() =>
        Assert.Throws<FileNotFoundException>(() => Jpeg2000Codec.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".jp2")));

    /// <summary>Tests data that is not JPEG 2000.</summary>
    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1 })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 })]
    [InlineData(new byte[] { 0xFF, 0x4F })]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 })]
    public void Jpeg2000Codec_Load_NotJpeg2000_ThrowsInvalidData(byte[] data)
    {
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Load(new MemoryStream(data)));
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.GetInfo(new MemoryStream(data)));
    }

    // ------------------------------------------------------------------------------------------
    // Unsupported features
    // ------------------------------------------------------------------------------------------

    private static byte[] Patch(byte[] data, int offset, params byte[] bytes)
    {
        var copy = (byte[])data.Clone();
        Array.Copy(bytes, 0, copy, offset, bytes.Length);
        return copy;
    }

    private static int FindMarker(byte[] data, int marker)
    {
        for (var i = 2; i < data.Length - 1; i++)
        {
            if (data[i] == (marker >> 8) && data[i + 1] == (marker & 0xFF))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Tests that Part 2 extensions are rejected as unsupported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Part2Capabilities_ThrowsUnsupported()
    {
        var data = Encode(Img(8, 8, 1), Rev(1));
        var siz = FindMarker(data, 0xFF51);
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Patch(data, siz + 4, 0x80, 0x00)));
    }

    /// <summary>Tests that high-throughput JPEG 2000 is rejected as unsupported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HighThroughput_ThrowsUnsupported()
    {
        var data = Encode(Img(8, 8, 1), Rev(1));
        var cod = FindMarker(data, 0xFF52);
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Patch(data, cod + 12, 0x40)));
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Patch(data, cod + 12, 0x40 | 0x80)));
    }

    /// <summary>Tests that unknown wavelet transforms and component transforms are rejected as unsupported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_UnknownTransforms_ThrowsUnsupported()
    {
        var data = Encode(Img(8, 8, 3), Rev(1));
        var cod = FindMarker(data, 0xFF52);
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Patch(data, cod + 13, 5)));
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Patch(data, cod + 8, 3)));
    }

    /// <summary>Tests that an unsupported ROI style is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_UnknownRoiStyle_ThrowsUnsupported()
    {
        var o = Rev(1);
        o.RoiShift = 13;
        var data = Encode(Img(8, 8, 1), o);
        var rgn = FindMarker(data, 0xFF5E);
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Patch(data, rgn + 5, 1)));
    }

    /// <summary>Tests that more than sixteen components are rejected as unsupported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TooManyComponents_ThrowsUnsupportedOrInvalid()
    {
        var image = Img(4, 4, 17);
        var data = Encode(image, Rev(1));
        var ex = Record.Exception(() => Jpeg2000Codec.Decode(data));
        Assert.True(ex is UnsupportedImageFeatureException or InvalidDataException);
    }

    /// <summary>Tests that a two-component codestream without a channel definition decodes as grey.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TwoComponentsWithoutCdef_DecodesAsGrey() =>
        Assert.Equal(1, Jpeg2000Codec.Decode(Encode(Img(8, 8, 2), Rev(1))).ColorChannelCount);

    /// <summary>Tests that five channels without a channel definition are rejected as unsupported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_FiveComponentsWithoutCdef_ThrowsUnsupported() =>
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Encode(Img(8, 8, 5), Rev(1))));

    /// <summary>Tests that depths above sixteen bits are rejected as unsupported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_DepthAbove16_ThrowsUnsupported()
    {
        var data = Encode(Img(8, 8, 1), Rev(1));
        var siz = FindMarker(data, 0xFF51);
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Patch(data, siz + 40, 0x10)));
    }

    /// <summary>Tests that images wider than the surface limit are rejected by Load but reported by GetInfo.</summary>
    [Fact]
    public void Jpeg2000Codec_Load_ExceedsMaxDimension_ThrowsInvalidData()
    {
        var data = Encode(Img(8, 8, 1), Rev(1));
        var siz = FindMarker(data, 0xFF51);
        var huge = Patch(data, siz + 6, 0x00, 0x01, 0x00, 0x00);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Load(new MemoryStream(huge)));
        Assert.Equal(65536, Jpeg2000Codec.GetInfo(new MemoryStream(huge)).Width);
    }

    // ------------------------------------------------------------------------------------------
    // Malformed, truncated and hostile input
    // ------------------------------------------------------------------------------------------

    private static IEnumerable<byte[]> Fixtures()
    {
        var multi = Rev(3);
        multi.Layers = 3;
        multi.TileWidth = 24;
        multi.TileHeight = 24;
        multi.Progression = 2;
        multi.Precincts = [(4, 4), (5, 5)];
        multi.Sop = true;
        multi.Eph = true;
        multi.CodeBlockStyle = 0x3F & ~0x10;
        multi.Mct = true;
        yield return Encode(Img(40, 30, 3), multi);

        var ppm = Rev(2);
        ppm.HeaderMode = J2kHeaderMode.Ppm;
        ppm.TileWidth = 20;
        ppm.TileHeight = 20;
        ppm.TileParts = 2;
        ppm.Poc = TestPoc(1, 1);
        yield return Encode(Img(30, 30, 1), ppm);

        var irr = Irrev(3);
        irr.Mct = true;
        irr.RoiShift = 14;
        yield return Jpeg2000TestEncoder.WrapJp2(
            Img(24, 20, 3),
            Encode(Img(24, 20, 3), irr),
            new J2kJp2Options { WriteBpcc = true });

        var ppt = Rev(2);
        ppt.HeaderMode = J2kHeaderMode.Ppt;
        ppt.ExtraMarkers = true;
        ppt.Layers = 2;
        yield return Encode(Img(25, 19, 1), ppt);
    }

    /// <summary>Tests that every truncation of a generated stream fails cleanly or decodes.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_EveryTruncation_FailsCleanly()
    {
        foreach (var fixture in Fixtures())
        {
            var step = Math.Max(1, fixture.Length / 400);
            for (var len = 0; len < fixture.Length; len += step)
            {
                AssertRejectedOrDecoded(fixture[..len]);
            }
        }
    }

    /// <summary>Tests that bit flips fail cleanly or decode, within bounded time.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RandomBitFlips_FailCleanly()
    {
        var rng = new Random(1234);
        var watch = Stopwatch.StartNew();
        foreach (var fixture in Fixtures())
        {
            for (var i = 0; i < 400; i++)
            {
                var copy = (byte[])fixture.Clone();
                var flips = rng.Next(1, 4);
                for (var f = 0; f < flips; f++)
                {
                    // Concentrate on the headers, where structural fields live.
                    var pos = rng.Next(3) == 0 ? rng.Next(copy.Length) : rng.Next(Math.Min(copy.Length, 200));
                    copy[pos] ^= (byte)(1 << rng.Next(8));
                }

                AssertRejectedOrDecoded(copy);
            }
        }

        Assert.True(watch.Elapsed < TimeSpan.FromMinutes(3), "fuzzing took too long: " + watch.Elapsed);
    }

    /// <summary>Tests that every single byte of the main header can be replaced by extreme values.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HeaderByteSubstitutions_FailCleanly()
    {
        var watch = Stopwatch.StartNew();
        foreach (var fixture in Fixtures())
        {
            var sot = FindMarker(fixture, 0xFF90);
            var limit = sot > 0 ? Math.Min(sot + 14, fixture.Length) : Math.Min(300, fixture.Length);
            for (var pos = 0; pos < limit; pos++)
            {
                foreach (var value in new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFF })
                {
                    var copy = (byte[])fixture.Clone();
                    copy[pos] = value;
                    AssertRejectedOrDecoded(copy);
                }
            }
        }

        Assert.True(watch.Elapsed < TimeSpan.FromMinutes(3), "fuzzing took too long: " + watch.Elapsed);
    }

    /// <summary>Tests huge declared dimensions, tile counts and precinct counts.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HugeCounts_FailQuickly()
    {
        var data = Encode(Img(16, 16, 1), Rev(2));
        var siz = FindMarker(data, 0xFF51);
        var watch = Stopwatch.StartNew();
        byte[] ff4 = [0xFF, 0xFF, 0xFF, 0xFF];
        byte[] big = [0x7F, 0xFF, 0xFF, 0xFF];
        byte[] one = [0, 0, 0, 1];
        foreach (var field in new[] { 6, 10 })
        {
            AssertRejectedOrDecoded(Patch(data, siz + field, ff4));
            AssertRejectedOrDecoded(Patch(data, siz + field, big));
        }

        // Huge image with tiny tiles: more than the permitted number of tiles.
        var tiny = Patch(Patch(Patch(data, siz + 6, 0x00, 0x10, 0x00, 0x00), siz + 10, 0x00, 0x10, 0x00, 0x00), siz + 22, one);
        tiny = Patch(tiny, siz + 26, one);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(tiny));

        // Huge image, one tile, tiny precincts and code-blocks: more than the permitted number of precincts or blocks.
        var o = Rev(0);
        o.Precincts = [(0, 0)];
        o.CodeBlockWidthExp = 2;
        o.CodeBlockHeightExp = 2;
        var p = Encode(Img(16, 16, 1), o);
        var siz2 = FindMarker(p, 0xFF51);
        var bigImage = Patch(Patch(p, siz2 + 6, 0x00, 0x40, 0x00, 0x00), siz2 + 10, 0x00, 0x40, 0x00, 0x00);
        bigImage = Patch(Patch(bigImage, siz2 + 22, 0x00, 0x40, 0x00, 0x00), siz2 + 26, 0x00, 0x40, 0x00, 0x00);
        AssertRejectedOrDecoded(bigImage);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), "hostile input took too long: " + watch.Elapsed);
    }

    /// <summary>Tests marker segments with bad lengths.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_BadMarkerLengths_FailCleanly()
    {
        var data = Encode(Img(16, 16, 3), Rev(2));
        foreach (var marker in new[] { 0xFF51, 0xFF52, 0xFF5C })
        {
            var pos = FindMarker(data, marker);
            foreach (var len in new[] { 0x0000, 0x0001, 0x0002, 0x0003, 0x0100, 0xFFFF })
            {
                AssertRejectedOrDecoded(Patch(data, pos + 2, (byte)(len >> 8), (byte)len));
            }
        }

        var sot = FindMarker(data, 0xFF90);
        foreach (var psot in new[] { 0u, 1u, 11u, 13u, 0xFFFFFFFFu, 100000u })
        {
            AssertRejectedOrDecoded(Patch(data, sot + 6, (byte)(psot >> 24), (byte)(psot >> 16), (byte)(psot >> 8), (byte)psot));
        }
    }

    /// <summary>Tests a stream that is only a header, or whose tile data are garbage.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_GarbageTileData_FailsCleanly()
    {
        var data = Encode(Img(24, 24, 3), Rev(2));
        var sod = FindMarker(data, 0xFF93) + 2;
        var rng = new Random(77);
        for (var i = 0; i < 100; i++)
        {
            var copy = (byte[])data.Clone();
            for (var j = sod; j < copy.Length - 2; j++)
            {
                copy[j] = (byte)rng.Next(256);
            }

            AssertRejectedOrDecoded(copy);
        }
    }

    /// <summary>Tests hostile JP2 box structures.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HostileBoxes_FailCleanly()
    {
        var image = Img(10, 10, 3);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options { Cdef = [(0, 0, 1), (1, 0, 2), (2, 0, 3)] });
        var rng = new Random(5);
        for (var i = 0; i < 600; i++)
        {
            var copy = (byte[])jp2.Clone();
            copy[rng.Next(Math.Min(copy.Length, 220))] = (byte)rng.Next(256);
            AssertRejectedOrDecoded(copy);
        }

        foreach (var len in new byte[][] { [0, 0, 0, 1], [0, 0, 0, 2], [0xFF, 0xFF, 0xFF, 0xFF], [0, 0, 0, 7] })
        {
            AssertRejectedOrDecoded(Patch(jp2, 0, len));
            AssertRejectedOrDecoded(Patch(jp2, 12, len));
            AssertRejectedOrDecoded(Patch(jp2, 32, len));
        }
    }

    /// <summary>Tests a progression-order-change marker that loops or has out-of-range indexes.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HostilePoc_FailsCleanly()
    {
        var o = Rev(2);
        o.Layers = 2;
        o.Poc = [new J2kPoc(0, 0, 2, 3, 1, 0)];
        var data = Encode(Img(16, 16, 1), o);
        var poc = FindMarker(data, 0xFF5F);
        for (var i = 4; i < 13; i++)
        {
            foreach (var v in new byte[] { 0, 1, 0x7F, 0xFF })
            {
                AssertRejectedOrDecoded(Patch(data, poc + i, v));
            }
        }
    }
}
