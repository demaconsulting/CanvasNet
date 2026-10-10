// cspell:ignore bypass termall vcausal segsym pterm ppm ppt tlm plt crg poc cprl rpcl pcrl rlcp lrcp sop eph pclr cmap cdef bpcc colr
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

    /// <summary>Explicit work cap for fuzzed streams so every mutation is bounded by a budget rather than by wall-clock time.</summary>
    private static readonly Jpeg2000DecoderLimits FuzzLimits = new() { MaxTier1Work = 1L << 28 };

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

    /// <summary>
    ///     Asserts that decoding fails with exactly <see cref="InvalidDataException"/>, that the message names the
    ///     expected cause and that the failure comes from explicit validation (no wrapped inner exception).
    /// </summary>
    private static void AssertMalformed(byte[] data, string cause, Jpeg2000DecoderLimits? limits = null)
    {
        var ex = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(data, limits ?? Jpeg2000DecoderLimits.Default));
        Assert.Contains(cause, ex.Message);
        Assert.Null(ex.InnerException);
    }

    /// <summary>
    ///     Outcome check for randomly mutated streams only. A random mutation of an entropy-coded payload or of an
    ///     unrelated header field can still be a valid stream, so success is legitimate here; any failure must be
    ///     one of the documented exception types raised by explicit validation, never a wrapped runtime error.
    /// </summary>
    private static void AssertFuzzOutcome(byte[] data)
    {
        try
        {
            _ = Jpeg2000Codec.Decode(data, FuzzLimits);
        }
        catch (Exception ex) when (AllowedFailures.Contains(ex.GetType()))
        {
            Assert.Null(ex.InnerException);
        }
    }

    /// <summary>
    ///     Builds a hostile one-tile, no-decomposition stream of <paramref name="size"/> x <paramref name="size"/>
    ///     pixels whose every code-block (<c>2^blockExp</c> square) declares <paramref name="passes"/> (6 to 164) coding
    ///     passes in a single one-byte segment, with a bit-plane count that allows them. The stream stays tiny
    ///     compared with the entropy-decoding work it asks for.
    /// </summary>
    private static byte[] HostileTier1Stream(int size, int blockExp, int passes = 88)
    {
        var o = Rev(0);
        o.CodeBlockWidthExp = blockExp;
        o.CodeBlockHeightExp = blockExp;
        var data = Encode(Img(8, 8, 1), o);
        var siz = FindMarker(data, 0xFF51);
        var qcd = FindMarker(data, 0xFF5C);
        var sot = FindMarker(data, 0xFF90);
        var sod = FindMarker(data, 0xFF93) + 2;
        byte[] dim = [(byte)(size >> 24), (byte)(size >> 16), (byte)(size >> 8), (byte)size];

        // Two guard bits and an exponent of 29 give 30 bit-planes: 3 * 30 - 2 = 88 passes are legal.
        var header = Patch(Patch(Patch(Patch(Patch(Patch(data[..sod], siz + 6, dim), siz + 10, dim), siz + 22, dim), siz + 26, dim), qcd + 4, 0x40, 29 << 3), sot + 6, 0, 0, 0, 0);
        var blocks = size >> blockExp;
        var inclusion = new Jpeg2000TestEncoder.TagTreeEncoder(blocks, blocks, new int[blocks * blocks]);
        var zeroBits = new Jpeg2000TestEncoder.TagTreeEncoder(blocks, blocks, new int[blocks * blocks]);
        var bits = new Jpeg2000TestEncoder.HeaderBitWriter();
        bits.Bit(1);
        for (var y = 0; y < blocks; y++)
        {
            for (var x = 0; x < blocks; x++)
            {
                inclusion.Encode(bits, x, y, 1);
                zeroBits.Encode(bits, x, y, 1);
                if (passes >= 37)
                {
                    bits.Bits(0x1FF, 9);
                    bits.Bits(passes - 37, 7);
                }
                else
                {
                    bits.Bits(0xF, 4);
                    bits.Bits(passes - 6, 5);
                }

                bits.Bit(0);
                bits.Bits(1, 3 + (31 - int.LeadingZeroCount(passes)));
            }
        }

        var packet = bits.Finish();
        return [.. header, .. packet, .. new byte[blocks * blocks], 0xFF, 0xD9];
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
    // Quantization entry counts (style 0) and COD/COC, QCD/QCC precedence
    // ------------------------------------------------------------------------------------------

    private const int MarkerQcdValue = 0xFF5C;
    private const int MarkerQccValue = 0xFF5D;
    private const int MarkerCodValue = 0xFF52;

    /// <summary>Finds the offset of a marker segment in the main header or in the first tile-part header.</summary>
    private static int FindSegment(byte[] d, int marker, bool inTile)
    {
        var pos = 2;
        var tile = false;
        while (pos + 4 <= d.Length)
        {
            var m = (d[pos] << 8) | d[pos + 1];
            if (m == 0xFF93)
            {
                break;
            }

            var len = (d[pos + 2] << 8) | d[pos + 3];
            if (m == marker && tile == inTile)
            {
                return pos;
            }

            tile |= m == 0xFF90;
            pos += 2 + len;
        }

        throw new InvalidOperationException("marker segment not found.");
    }

    /// <summary>Adds (positive) or removes (negative) trailing payload bytes of a marker segment, fixing its length.</summary>
    private static byte[] ResizeSegment(byte[] d, int marker, bool inTile, int delta)
    {
        var pos = FindSegment(d, marker, inTile);
        var len = (d[pos + 2] << 8) | d[pos + 3];
        var end = pos + 2 + len;
        var list = d.ToList();
        if (delta > 0)
        {
            list.InsertRange(end, Enumerable.Repeat((byte)0x40, delta));
        }
        else
        {
            list.RemoveRange(end + delta, -delta);
        }

        list[pos + 2] = (byte)((len + delta) >> 8);
        list[pos + 3] = (byte)(len + delta);
        return [.. list];
    }

    /// <summary>Moves the QCD segment of the main header in front of the COD segment.</summary>
    private static byte[] MoveQcdBeforeCod(byte[] d)
    {
        var qcd = FindSegment(d, MarkerQcdValue, false);
        var qcdLen = 2 + ((d[qcd + 2] << 8) | d[qcd + 3]);
        var segment = d[qcd..(qcd + qcdLen)];
        var rest = d[..qcd].Concat(d[(qcd + qcdLen)..]).ToArray();
        var cod = FindSegment(rest, MarkerCodValue, false);
        return [.. rest[..cod], .. segment, .. rest[cod..]];
    }

    /// <summary>A single-tile reversible stream with a main COC/QCC (component 1 has one level) and an optional tile override.</summary>
    private static byte[] QuantStream(bool tileOverride, bool tileComponentLevels = false)
    {
        var o = Rev(2);
        o.ComponentLevels = [2, 1, 2];
        o.ZeroPsotOnLast = true;
        o.OmitEoc = true;
        if (tileOverride)
        {
            var t = o.Clone();
            t.ComponentLevels = tileComponentLevels ? [2, 1, 2] : null;
            o.ComponentLevels = [2, 2, 2];
            o.TileOverrides[0] = t;
            o.TileWidth = 0;
        }

        return Encode(Img(40, 30, 3), o);
    }

    /// <summary>Tests that a style-0 QCD or QCC with extra or missing entries is rejected in the main header.</summary>
    [Theory]
    [InlineData(MarkerQcdValue, 1)]
    [InlineData(MarkerQcdValue, 3)]
    [InlineData(MarkerQcdValue, -1)]
    [InlineData(MarkerQccValue, 1)]
    [InlineData(MarkerQccValue, -1)]
    public void Jpeg2000Codec_Decode_MainQuantEntryCountMismatch_ThrowsInvalidData(int marker, int delta)
    {
        var data = ResizeSegment(QuantStream(false), marker, false, delta);
        AssertMalformed(data, "quantization segment has");
    }

    /// <summary>Tests that a style-0 QCD or QCC with extra or missing entries is rejected in a tile-part header.</summary>
    [Theory]
    [InlineData(MarkerQcdValue, 1)]
    [InlineData(MarkerQcdValue, -1)]
    [InlineData(MarkerQccValue, 1)]
    [InlineData(MarkerQccValue, -1)]
    public void Jpeg2000Codec_Decode_TileQuantEntryCountMismatch_ThrowsInvalidData(int marker, int delta)
    {
        var data = ResizeSegment(QuantStream(true, true), marker, true, delta);
        AssertMalformed(data, "quantization segment has");
    }

    /// <summary>Tests that the unmodified streams used by the mismatch tests decode.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_QuantEntryCountMatches_RoundTripsExactly()
    {
        Assert.Equal(40, Jpeg2000Codec.Decode(QuantStream(false)).Width);
        Assert.Equal(40, Jpeg2000Codec.Decode(QuantStream(true, true)).Width);
    }

    /// <summary>Tests that a QCD placed before the COD is validated against the levels the COD later signals.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_QcdBeforeCod_ValidatedAgainstLaterLevels()
    {
        var image = Img(40, 30, 3);
        var data = MoveQcdBeforeCod(Encode(image, Rev(2)));
        Assert.Equal(0, MaxError(image, data));

        AssertMalformed(ResizeSegment(data, MarkerQcdValue, false, 1), "quantization segment has");
        AssertMalformed(ResizeSegment(data, MarkerQcdValue, false, -1), "quantization segment has");
    }

    /// <summary>T.800 A.6.2: a tile COD (and QCD) overrides a main COC (and QCC) for all components.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TileCodOverridesMainCoc_ForAllComponents()
    {
        var o = Rev(3);
        o.ComponentLevels = [3, 1, 3];
        var tile = o.Clone();
        tile.Levels = 2;
        tile.ComponentLevels = null;
        o.TileOverrides[0] = tile;
        AssertExact(Img(40, 30, 3), o);
    }

    /// <summary>T.800 A.6.2: a tile-part COC (and QCC) beats the tile COD (and QCD) for its component.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TileCocOverridesTileCod_ForItsComponent()
    {
        var o = Rev(3);
        var tile = o.Clone();
        tile.Levels = 2;
        tile.ComponentLevels = [2, 1, 2];
        o.TileOverrides[0] = tile;
        AssertExact(Img(40, 30, 3), o);
    }

    /// <summary>Tests that without a tile COD the main COC still applies to its component, in tiles that have no override.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_NoTileCod_MainCocStillApplies()
    {
        var o = Rev(3);
        o.ComponentLevels = [3, 1, 3];
        o.TileWidth = 32;
        o.TileHeight = 32;
        var tile = o.Clone();
        tile.Levels = 2;
        tile.ComponentLevels = null;
        o.TileOverrides[1] = tile;
        AssertExact(Img(64, 32, 3), o);
    }

    /// <summary>Tests the same precedence on the irreversible path (expounded QCD/QCC with per-component levels).</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TilePrecedenceIrreversible_WithinTolerance()
    {
        var o = Irrev(3);
        o.ComponentLevels = [3, 1, 3];
        o.TileWidth = 32;
        o.TileHeight = 32;
        var tile = o.Clone();
        tile.Levels = 2;
        tile.ComponentLevels = [2, 2, 1];
        o.TileOverrides[1] = tile;
        AssertClose(Img(64, 32, 3), o);
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

    /// <summary>Tests that a JP2 signature box with the wrong content bytes is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2BadSignatureContent_ThrowsInvalidData()
    {
        var image = Img(8, 8, 1);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options { EnumCs = 17 });
        _ = Jpeg2000Codec.Decode(jp2);
        AssertMalformed(Patch(jp2, 8, 0x0D, 0x0A, 0x87, 0x0B), "invalid JP2 signature box content");
        AssertMalformed(Patch(jp2, 8, 0x00, 0x00, 0x00, 0x00), "invalid JP2 signature box content");
    }

    /// <summary>Tests that a palette column declared signed is rejected as unsupported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2SignedPaletteColumn_ThrowsUnsupported()
    {
        var image = Img(12, 9, 1, 4, 1, 0);
        int[][] palette = Enumerable.Range(0, 16).Select(i => new[] { i }).ToArray();

        // A depth of 129 is written as the byte 0x80: bit depth 1 with the signed flag set.
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(1)),
            new J2kJp2Options { Palette = palette, PaletteDepths = [129], Cmap = [(0, 1, 0)] });
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(jp2));
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

    /// <summary>A stream wrapper that counts the bytes consumed (read or skipped) and can hide seekability.</summary>
    private sealed class CountingStream(byte[] data, bool seekable) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public long BytesConsumed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => seekable;

        public override bool CanWrite => false;

        public override long Length => seekable ? _inner.Length : throw new NotSupportedException();

        public override long Position
        {
            get => seekable ? _inner.Position : throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = _inner.Read(buffer, offset, count);
            BytesConsumed += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!seekable || origin != SeekOrigin.Current)
            {
                throw new NotSupportedException();
            }

            // Skipping is cheap on a seekable stream and does not count as reading the bytes.
            return _inner.Seek(offset, origin);
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private const int ProbeTrailer = 4 * 1024 * 1024;

    /// <summary>Tests that GetInfo reads only a small header prefix of a raw codestream followed by a large payload.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Jpeg2000Codec_GetInfo_RawCodestream_ReadsOnlyHeaderPrefix(bool seekable)
    {
        var data = Encode(Img(31, 17, 3), Rev(2)).Concat(new byte[ProbeTrailer]).ToArray();
        using var stream = new CountingStream(data, seekable);
        var info = Jpeg2000Codec.GetInfo(stream);
        Assert.Equal(31, info.Width);
        Assert.Equal(17, info.Height);
        Assert.InRange(stream.BytesConsumed, 1, 70000);
    }

    /// <summary>Tests that GetInfo reads only a small header prefix of a JP2 file followed by a large codestream.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Jpeg2000Codec_GetInfo_Jp2_ReadsOnlyHeaderPrefix(bool seekable)
    {
        var image = Img(11, 12, 4);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(
            image,
            Encode(image, Rev(1)).Concat(new byte[ProbeTrailer]).ToArray(),
            new J2kJp2Options { Cdef = [(0, 0, 1), (1, 0, 2), (2, 0, 3), (3, 1, 0)] });
        using var stream = new CountingStream(jp2, seekable);
        var info = Jpeg2000Codec.GetInfo(stream);
        Assert.Equal(4, info.Channels);
        Assert.True(info.HasAlpha);
        Assert.InRange(stream.BytesConsumed, 1, 70000);
    }

    /// <summary>Tests that a large box before the codestream is skipped by seeking when possible and discarded otherwise.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Jpeg2000Codec_GetInfo_LargeSkippedBox_SeekOrDiscard(bool seekable)
    {
        var image = Img(11, 12, 3);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options());
        var box = new byte[8 + ProbeTrailer];
        box[0] = (byte)((box.Length >> 24) & 0xFF);
        box[1] = (byte)((box.Length >> 16) & 0xFF);
        box[2] = (byte)((box.Length >> 8) & 0xFF);
        box[3] = (byte)(box.Length & 0xFF);
        "free"u8.CopyTo(box.AsSpan(4));
        var data = jp2[..12].Concat(box).Concat(jp2[12..]).ToArray();
        using var stream = new CountingStream(data, seekable);
        Assert.Equal(11, Jpeg2000Codec.GetInfo(stream).Width);
        if (seekable)
        {
            Assert.InRange(stream.BytesConsumed, 1, 70000);
        }
    }

    /// <summary>Tests that a seekable truncated JP2 box and a truncated non-seekable skipped box are both rejected as invalid.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Jpeg2000Codec_GetInfo_TruncatedBox_ThrowsInvalidData(bool seekable)
    {
        var image = Img(11, 12, 3);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options());
        var data = jp2[..20];
        using var stream = new CountingStream(data, seekable);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.GetInfo(stream));
    }

    /// <summary>Tests that a seekable stream above the input cap is rejected by GetInfo as before.</summary>
    [Fact]
    public void Jpeg2000Codec_GetInfo_SeekableAboveInputCap_ThrowsInvalidData()
    {
        using var stream = new SparseStream(Jpeg2000DecoderLimits.Default.MaxInputBytes + 1);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.GetInfo(stream));
    }

    /// <summary>A seekable zero-filled stream of a declared length that allocates nothing.</summary>
    private sealed class SparseStream(long length) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => Position = offset;

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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

    /// <summary>Patches the image size and the tile size of the SIZ marker segment.</summary>
    private static byte[] Resize(byte[] data, int width, int height, int tileWidth, int tileHeight)
    {
        var siz = FindMarker(data, 0xFF51);
        var copy = (byte[])data.Clone();
        foreach (var (offset, value) in new[] { (6, width), (10, height), (22, tileWidth), (26, tileHeight) })
        {
            copy[siz + offset] = (byte)(value >> 24);
            copy[siz + offset + 1] = (byte)(value >> 16);
            copy[siz + offset + 2] = (byte)(value >> 8);
            copy[siz + offset + 3] = (byte)value;
        }

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
    public void Jpeg2000Codec_Decode_TooManyComponents_ThrowsUnsupported()
    {
        var data = Encode(Img(4, 4, 17), Rev(1));
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(data));
        Assert.Equal("jpeg2000-component-count", ex.Feature);
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

    /// <summary>Tests that every possible truncation of a small stream fails closed with InvalidDataException.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_EveryTruncationOfSmallStream_ThrowsInvalidData()
    {
        var o = Rev(2);
        o.Layers = 2;
        o.TileWidth = 16;
        o.TileHeight = 16;
        o.Sop = true;
        o.Eph = true;
        var stream = Encode(Img(20, 12, 1), o);

        // Every prefix that loses more than the EOC marker is rejected.
        for (var len = 0; len < stream.Length - 2; len++)
        {
            var prefix = stream[..len];
            var ex = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(prefix));
            Assert.Null(ex.InnerException);
        }

        _ = Jpeg2000Codec.Decode(stream);
    }

    /// <summary>Tests that truncations of larger generated streams are always rejected (no partial decoding).</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TruncationsOfLargeStreams_ThrowsInvalidData()
    {
        foreach (var fixture in Fixtures())
        {
            var step = Math.Max(1, fixture.Length / 400);
            for (var len = 0; len < fixture.Length - 16; len += step)
            {
                var prefix = fixture[..len];
                var ex = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(prefix));
                Assert.Null(ex.InnerException);
            }
        }
    }

    /// <summary>Tests that bit flips fail cleanly or decode, with the work bounded by explicit decoder limits.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RandomBitFlips_FailCleanly()
    {
        var rng = new Random(1234);
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

                AssertFuzzOutcome(copy);
            }
        }
    }

    /// <summary>Tests that every single byte of the main header can be replaced by extreme values.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HeaderByteSubstitutions_FailCleanly()
    {
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
                    AssertFuzzOutcome(copy);
                }
            }
        }
    }

    /// <summary>Tests marker segments with bad lengths.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_BadMarkerLengths_ThrowsInvalidData()
    {
        var data = Encode(Img(16, 16, 3), Rev(2));
        foreach (var marker in new[] { 0xFF51, 0xFF52, 0xFF5C })
        {
            var pos = FindMarker(data, marker);
            foreach (var len in new[] { 0x0000, 0x0001, 0x0002, 0x0003, 0x0100, 0xFFFF })
            {
                var broken = Patch(data, pos + 2, (byte)(len >> 8), (byte)len);
                var ex = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(broken));
                Assert.StartsWith("Invalid JPEG 2000 data: ", ex.Message);
                Assert.Null(ex.InnerException);
            }
        }
    }

    /// <summary>Tests tile-part lengths that are inconsistent with the data.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_BadTilePartLengths_ThrowsInvalidData()
    {
        var data = Encode(Img(16, 16, 3), Rev(2));
        var sot = FindMarker(data, 0xFF90);
        foreach (var psot in new[] { 1u, 11u, 13u, 0xFFFFFFFFu, 100000u })
        {
            var broken = Patch(data, sot + 6, (byte)(psot >> 24), (byte)(psot >> 16), (byte)(psot >> 8), (byte)psot);
            var ex = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(broken));
            Assert.StartsWith("Invalid JPEG 2000 data: ", ex.Message);
            Assert.Null(ex.InnerException);
        }

        // A zero length is legal for the last tile-part (it extends to the end of the data).
        _ = Jpeg2000Codec.Decode(Patch(data, sot + 6, 0, 0, 0, 0));
    }

    /// <summary>Tests that tile-parts of a tile must come in order (TPsot) while the advisory count (TNsot) is not enforced.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TilePartIndexes_AreCheckedAndCountIsAdvisory()
    {
        var o = Rev(2);
        o.TileParts = 2;
        var data = Encode(Img(16, 16, 1), o);
        var first = FindMarker(data, 0xFF90);
        var second = first + 2;
        while (!(data[second] == 0xFF && data[second + 1] == 0x90))
        {
            second++;
        }

        // Layout of the SOT marker segment: marker, Lsot, Isot, Psot, TPsot (offset 10), TNsot (offset 11).
        AssertMalformed(Patch(Patch(data, first + 10, 1), second + 10, 0), "tile-part index is out of sequence");
        AssertMalformed(Patch(data, second + 10, 0), "tile-part index is out of sequence");
        AssertMalformed(Patch(data, first + 10, 1), "tile-part index is out of sequence");

        // The count is advisory: zero (unknown) and a wrong count decode to the same image.
        var expected = Jpeg2000Codec.Decode(data).ColorSamples;
        foreach (var count in new byte[] { 0, 1, 9 })
        {
            var counted = Patch(Patch(data, first + 11, count), second + 11, count);
            Assert.Equal(expected, Jpeg2000Codec.Decode(counted).ColorSamples);
        }
    }

    /// <summary>Tests that COD, COC, QCD, QCC and RGN are rejected in a tile-part after the first (ISO 15444-1 A.6).</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_ParamMarkersInLaterTilePart_ThrowsInvalidData()
    {
        var baseline = Rev(2);
        baseline.TileParts = 2;
        var plain = Encode(Img(16, 16, 1), baseline);
        var cod = MainSegment(plain, 0xFF52);
        var qcd = MainSegment(plain, 0xFF5C);
        var coc = Segment(0xFF53, [0, 0, .. cod.Skip(4 + 5)]);
        var qcc = Segment(0xFF5D, [0, .. qcd.Skip(4)]);
        var rgn = Segment(0xFF5E, [0, 0, 1]);
        foreach (var later in new[] { cod, qcd, coc, qcc, rgn })
        {
            var o = Rev(2);
            o.TileParts = 2;
            o.LaterPartSegments = later;
            AssertMalformed(Encode(Img(16, 16, 1), o), "first tile-part header");
        }

        // The same markers in the first tile-part remain valid; the restriction is per tile, not global.
        var tiled = Rev(2);
        tiled.TileParts = 2;
        tiled.TileWidth = 8;
        tiled.TileHeight = 16;
        var tiledData = Encode(Img(16, 16, 1), tiled);
        Assert.NotNull(Jpeg2000Codec.Decode(tiledData));
        tiled.LaterPartSegments = rgn;
        AssertMalformed(Encode(Img(16, 16, 1), tiled), "first tile-part header");
    }

    /// <summary>Tests that POC in a later tile-part extends the progression list and still decodes.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PocInLaterTilePart_RoundTripsExactly()
    {
        var o = Rev(3);
        o.Layers = 3;
        o.Poc = TestPoc(3, 3);
        o.PocInTileHeader = true;
        o.PocSplitAcrossParts = true;
        o.TileParts = 2;
        AssertExact(Img(40, 40, 3), o);
    }

    private static byte[] Segment(int marker, IEnumerable<byte> payload)
    {
        var body = payload.ToArray();
        return [(byte)(marker >> 8), (byte)marker, (byte)((body.Length + 2) >> 8), (byte)(body.Length + 2), .. body];
    }

    private static byte[] MainSegment(byte[] data, int marker)
    {
        var at = FindMarker(data, marker);
        var len = (data[at + 2] << 8) | data[at + 3];
        return data[at..(at + 2 + len)];
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

            AssertFuzzOutcome(copy);
        }
    }

    /// <summary>Appends one byte to the first marker segment of the given type and fixes its length field.</summary>
    private static byte[] GrowSegment(byte[] data, int marker)
    {
        var at = FindMarker(data, marker);
        var len = (data[at + 2] << 8) | data[at + 3];
        var grown = new byte[data.Length + 1];
        Array.Copy(data, 0, grown, 0, at + 2 + len);
        grown[at + 2 + len] = 0;
        Array.Copy(data, at + 2 + len, grown, at + 3 + len, data.Length - at - 2 - len);
        grown[at + 2] = (byte)((len + 1) >> 8);
        grown[at + 3] = (byte)(len + 1);
        return grown;
    }

    /// <summary>Removes the last payload byte of the first marker segment of the given type and fixes its length field.</summary>
    private static byte[] ShrinkSegment(byte[] data, int marker)
    {
        var at = FindMarker(data, marker);
        var len = (data[at + 2] << 8) | data[at + 3];
        var shrunk = new byte[data.Length - 1];
        Array.Copy(data, 0, shrunk, 0, at + 1 + len);
        Array.Copy(data, at + 2 + len, shrunk, at + 1 + len, data.Length - at - 2 - len);
        shrunk[at + 2] = (byte)((len - 1) >> 8);
        shrunk[at + 3] = (byte)(len - 1);
        return shrunk;
    }

    /// <summary>Appends one byte to the named JP2 header sub-box (and its jp2h parent) and fixes both lengths.</summary>
    private static byte[] GrowJp2Box(byte[] jp2, string type)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        int Find(string t, int from)
        {
            var tb = System.Text.Encoding.ASCII.GetBytes(t);
            for (var i = from; i < jp2.Length - 4; i++)
            {
                if (jp2.AsSpan(i, 4).SequenceEqual(tb))
                {
                    return i - 4;
                }
            }

            return -1;
        }

        var parent = Find("jp2h", 0);
        var box = Find(System.Text.Encoding.ASCII.GetString(typeBytes), parent + 8);
        int Length(int at) => (jp2[at] << 24) | (jp2[at + 1] << 16) | (jp2[at + 2] << 8) | jp2[at + 3];
        var boxLen = Length(box);
        var parentLen = Length(parent);
        var grown = new byte[jp2.Length + 1];
        Array.Copy(jp2, 0, grown, 0, box + boxLen);
        Array.Copy(jp2, box + boxLen, grown, box + boxLen + 1, jp2.Length - box - boxLen);
        foreach (var (at, len) in new[] { (box, boxLen + 1), (parent, parentLen + 1) })
        {
            grown[at] = (byte)(len >> 24);
            grown[at + 1] = (byte)(len >> 16);
            grown[at + 2] = (byte)(len >> 8);
            grown[at + 3] = (byte)len;
        }

        return grown;
    }

    /// <summary>Tests that an odd-length expounded QCD payload is rejected instead of silently dropping a byte.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_OddLengthExpoundedQcd_ThrowsInvalidData()
    {
        var data = Encode(Img(16, 16, 1), Irrev(2));
        Assert.Equal(2, data[FindMarker(data, 0xFF5C) + 4] & 0x1F);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(GrowSegment(data, 0xFF5C)));
    }

    /// <summary>Tests that an odd-length expounded QCC payload is rejected instead of silently dropping a byte.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_OddLengthExpoundedQcc_ThrowsInvalidData()
    {
        var o = Irrev(2);
        o.ComponentLevels = [2, 1, 2];
        var data = Encode(Img(16, 16, 3), o);
        Assert.True(FindMarker(data, 0xFF5D) > 0);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(GrowSegment(data, 0xFF5D)));
    }

    /// <summary>Tests that a derived-style QCD with a trailing byte is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_DerivedQcdTrailingByte_ThrowsInvalidData()
    {
        var o = Irrev(2);
        o.QuantStyle = 1;
        var data = Encode(Img(16, 16, 1), o);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(GrowSegment(data, 0xFF5C)));
    }

    /// <summary>Tests that SIZ, COD, RGN and COC segments with trailing or missing bytes are rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_CodestreamSegmentLengthMismatch_ThrowsInvalidData()
    {
        var o = Rev(2);
        o.RoiShift = 3;
        var plain = Encode(Img(16, 16, 1), o);
        foreach (var marker in new[] { 0xFF51, 0xFF52, 0xFF5E })
        {
            Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(GrowSegment(plain, marker)));
            Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(ShrinkSegment(plain, marker)));
        }

        var perComponent = Rev(2);
        perComponent.ComponentLevels = [2, 1, 2];
        var coc = Encode(Img(16, 16, 3), perComponent);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(GrowSegment(coc, 0xFF53)));
    }

    /// <summary>Tests that an SOP marker segment with a length other than 4 is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_SopWrongLength_ThrowsInvalidData()
    {
        var o = Rev(1);
        o.Sop = true;
        var data = Encode(Img(8, 8, 1), o);
        var sop = FindMarker(data, 0xFF91);
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(Patch(data, sop + 3, 6)));
    }

    /// <summary>Tests that JP2 colr, pclr and cdef boxes with trailing bytes are rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_Jp2BoxLengthMismatch_ThrowsInvalidData()
    {
        var gray = Img(10, 10, 1);
        var colr = Jpeg2000TestEncoder.WrapJp2(gray, Encode(gray, Rev(1)), new J2kJp2Options { EnumCs = 17 });
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(GrowJp2Box(colr, "colr")));

        var rgb = Img(10, 10, 3);
        var cdef = Jpeg2000TestEncoder.WrapJp2(rgb, Encode(rgb, Rev(1)), new J2kJp2Options { Cdef = [(0, 0, 1), (1, 0, 2), (2, 0, 3)] });
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(GrowJp2Box(cdef, "cdef")));

        var indexed = Img(12, 9, 1, 4, 1, 0);
        int[][] palette = Enumerable.Range(0, 16).Select(i => new[] { i }).ToArray();
        var pclr = Jpeg2000TestEncoder.WrapJp2(
            indexed,
            Encode(indexed, Rev(1)),
            new J2kJp2Options { Palette = palette, PaletteDepths = [8], Cmap = [(0, 1, 0)] });
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(GrowJp2Box(pclr, "pclr")));
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
            AssertFuzzOutcome(copy);
        }

        foreach (var len in new byte[][] { [0, 0, 0, 1], [0, 0, 0, 2], [0xFF, 0xFF, 0xFF, 0xFF], [0, 0, 0, 7] })
        {
            AssertFuzzOutcome(Patch(jp2, 0, len));
            AssertFuzzOutcome(Patch(jp2, 12, len));
            AssertFuzzOutcome(Patch(jp2, 32, len));
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
                AssertFuzzOutcome(Patch(data, poc + i, v));
            }
        }
    }

    // ------------------------------------------------------------------------------------------
    // Resource limits and hardening
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests that a stream declaring more progression changes than the cap is rejected when the marker is parsed.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_MoreProgressionChangesThanCap_ThrowsInvalidData()
    {
        var o = Rev(3);
        o.Layers = 4;
        o.Poc = [.. Enumerable.Repeat(new J2kPoc(0, 0, 4, 4, 3, 0), 5000)];
        AssertMalformed(Encode(Img(32, 32, 3), o), "too many progression order changes");

        // A tighter custom cap applies as well, and the default cap is exactly reached by 128 entries.
        o.Poc = [.. Enumerable.Repeat(new J2kPoc(0, 0, 4, 4, 3, 0), 5)];
        AssertMalformed(Encode(Img(32, 32, 3), o), "too many progression order changes", new Jpeg2000DecoderLimits { MaxProgressionChanges = 4 });
    }

    /// <summary>Tests that progression volumes leaving packets uncovered are rejected instead of decoding a partial image.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PocLeavingPacketsUncovered_ThrowsInvalidData()
    {
        const string cause = "the progression order does not cover every packet of the tile";

        // Only the first layer is covered.
        var layers = Rev(2);
        layers.Layers = 2;
        layers.Poc = [new J2kPoc(0, 0, 1, 3, 1, 0)];
        AssertMalformed(Encode(Img(16, 16, 1), layers), cause);

        // Only the first two of three components are covered, and the same applies to a tile-part POC with tiles.
        var comps = Rev(2);
        comps.Poc = [new J2kPoc(0, 0, 1, 3, 2, 1)];
        AssertMalformed(Encode(Img(16, 16, 3), comps), cause);
        comps.PocInTileHeader = true;
        comps.TileWidth = 8;
        comps.TileHeight = 8;
        AssertMalformed(Encode(Img(16, 16, 3), comps), cause);

        // Complementary volumes together cover everything and decode.
        var full = Rev(2);
        full.Layers = 2;
        full.Poc = [new J2kPoc(0, 0, 1, 3, 1, 0), new J2kPoc(0, 0, 2, 3, 1, 1)];
        AssertExact(Img(16, 16, 1), full);
    }

    /// <summary>Tests that a later POC volume with a larger layer end skips already-sent packets and reads only the new layers.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 4)]
    public void Jpeg2000Codec_Decode_PocLaterVolumeLargerLayerEnd_SkipsSentPackets(int firstOrder, int secondOrder)
    {
        var o = Rev(2);
        o.Layers = 4;
        o.Poc = [new J2kPoc(0, 0, 2, 3, 3, firstOrder), new J2kPoc(0, 0, 4, 3, 3, secondOrder)];
        AssertExact(Img(32, 32, 3), o);
    }

    /// <summary>Tests that a later POC volume with a larger layer end skips already-sent packets in a tile-part POC.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PocTileHeaderLaterVolumeLargerLayerEnd_SkipsSentPackets()
    {
        var o = Rev(2);
        o.Layers = 4;
        o.Poc = [new J2kPoc(0, 0, 2, 3, 3, 1), new J2kPoc(0, 0, 4, 3, 3, 0)];
        o.PocInTileHeader = true;
        o.TileWidth = 16;
        o.TileHeight = 16;
        AssertExact(Img(32, 32, 3), o);
    }

    /// <summary>Tests that a modest number of repeated progression volumes is deduplicated and still decodes.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RepeatedPocEntriesWithinCap_DecodesExactly()
    {
        var o = Rev(2);
        o.Layers = 2;
        o.Poc = [.. Enumerable.Repeat(new J2kPoc(0, 0, 2, 3, 1, 0), 20)];
        AssertExact(Img(16, 16, 1), o);
    }

    /// <summary>
    ///     Builds a stream whose many overlapping progression volumes (each one layer longer than the last) make the
    ///     decoder re-visit the same packets over and over: 1024 one-sample precincts and 128 layers.
    /// </summary>
    private static byte[] OverlappingVolumesStream()
    {
        var o = Rev(0);
        o.Layers = 128;
        o.Precincts = [(0, 0)];
        o.Poc = [.. Enumerable.Range(1, 128).Select(l => new J2kPoc(0, 0, l, 1, 1, 0))];
        return Encode(Img(32, 32, 1, noise: 0), o);
    }

    /// <summary>Tests that the cumulative progression budget rejects repeated visits of the same packets once the ceiling is tight.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_OverlappingProgressionVolumes_ThrowsInvalidData()
    {
        var data = OverlappingVolumesStream();
        AssertMalformed(data, "progression order iteration exceeds the decoder limit", new Jpeg2000DecoderLimits { MaxProgressionSteps = 1_000_000 });

        // The default allowance grows with the input (about 64 steps per byte), which a genuine stream of this size
        // never exceeds, so the stream itself is valid and the ceiling is the only cause of the rejection above.
        _ = Jpeg2000Codec.Decode(data);
    }

    /// <summary>Tests the progression ceiling with a tight custom limit on a valid stream.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TightProgressionLimit_ThrowsInvalidData()
    {
        var o = Rev(2);
        o.Layers = 2;
        var data = Encode(Img(16, 16, 1), o);
        AssertMalformed(data, "progression order iteration exceeds the decoder limit", new Jpeg2000DecoderLimits { MaxProgressionSteps = 3 });
        _ = Jpeg2000Codec.Decode(data);
    }

    /// <summary>Tests that the decode-wide budget is cumulative, scales with the input and is capped by the limits.</summary>
    [Fact]
    public void Jpeg2000Codec_DecodeBudget_IsCumulativeAndScalesWithInput()
    {
        var limits = new Jpeg2000DecoderLimits { MaxProgressionSteps = 1000, MaxTier1Work = 100 };
        var budget = new Jpeg2000Codec.DecodeBudget(limits, 0);
        budget.ChargeProgression(600);
        Assert.Throws<InvalidDataException>(() => budget.ChargeProgression(600));
        budget.ChargeTier1(100);
        Assert.Throws<InvalidDataException>(() => budget.ChargeTier1(1));

        // With the default limits a tiny input gets only the base allowance, a larger input more, and never more than the limit.
        var tiny = new Jpeg2000Codec.DecodeBudget(Jpeg2000DecoderLimits.Default, 10);
        tiny.ChargeProgression(1L << 22);
        Assert.Throws<InvalidDataException>(() => tiny.ChargeProgression(1L << 12));
        tiny.ChargeTier1(1L << 24);
        Assert.Throws<InvalidDataException>(() => tiny.ChargeTier1(1L << 20));
        var large = new Jpeg2000Codec.DecodeBudget(Jpeg2000DecoderLimits.Default, 1L << 18);
        large.ChargeTier1((1L << 32) + (1L << 24));
        Assert.Throws<InvalidDataException>(() => large.ChargeTier1(1));

        // A very large input is capped by the absolute ceiling (2^34), which covers every image the default limits allow.
        var huge = new Jpeg2000Codec.DecodeBudget(Jpeg2000DecoderLimits.Default, 1L << 24);
        huge.ChargeTier1(1L << 34);
        Assert.Throws<InvalidDataException>(() => huge.ChargeTier1(1));
        Assert.True(Jpeg2000DecoderLimits.Default.MaxTier1Work >= Jpeg2000DecoderLimits.Default.MaxTotalSamples * 88);
    }

    /// <summary>
    ///     Tests that a large valid lossless flat 16-bit image (4096 x 4096, no decomposition, 64 x 64 blocks: a tiny
    ///     stream that still needs the maximum number of coding passes per block) decodes under the default limits.
    ///     Its entropy-decoding work (about 7.2 x 10^8 sample-passes from a 158 KB stream, measured by bisecting <c>MaxTier1Work</c>) exceeded the former per-input-byte allowance.
    /// </summary>
    [Fact]
    public void Jpeg2000Codec_Decode_LargeLosslessFlatImage_DecodesUnderDefaultLimits()
    {
        const int size = 4096;
        var image = Img(size, size, 1, depth: 16);
        var samples = image.Components[0].Samples;
        Array.Fill(samples, 51_200);
        samples[0] = 3000;

        var o = Rev(0);
        o.CodeBlockWidthExp = 6;
        o.CodeBlockHeightExp = 6;
        var data = Encode(image, o);

        // The former allowance (2^24 + 2^12 per byte) would have rejected this stream.
        Assert.True((1L << 24) + ((1L << 12) * data.Length) < (long)size * size * 42);
        var decoded = Jpeg2000Codec.Decode(data);
        Assert.Equal(size, decoded.Width);
        Assert.Equal(Jpeg2000TestEncoder.ExpectedByte(3000, 16, false), decoded.ColorSamples[0]);
        Assert.Equal(Jpeg2000TestEncoder.ExpectedByte(51_200, 16, false), decoded.ColorSamples[1]);
        Assert.Equal(Jpeg2000TestEncoder.ExpectedByte(51_200, 16, false), decoded.ColorSamples[^1]);
    }

    /// <summary>Tests that the entropy-decoding work is bounded by an explicit limit.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TightTier1Limit_ThrowsInvalidData()
    {
        var data = Encode(Img(32, 32, 1), Rev(1));
        AssertMalformed(data, "too much entropy-decoding work", new Jpeg2000DecoderLimits { MaxTier1Work = 1000 });
        _ = Jpeg2000Codec.Decode(data);
    }

    /// <summary>
    ///     Tests that a tiny stream declaring huge code-blocks with the maximum number of passes each is rejected by
    ///     the entropy-decoding budget. The budget is an explicit work cap, so the outcome is deterministic and
    ///     independent of machine speed.
    /// </summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HostileTier1Work_ThrowsInvalidDataWhenOverBudget()
    {
        var limits = new Jpeg2000DecoderLimits { MaxTier1Work = 1L << 22 };
        AssertMalformed(HostileTier1Stream(2048, 6), "too much entropy-decoding work", limits);

        // Control: the same stream shape at a size whose work (128 x 128 x 88 sample-passes) fits that cap decodes.
        Assert.Equal(128, Jpeg2000Codec.Decode(HostileTier1Stream(128, 6), limits).Width);
        AssertMalformed(HostileTier1Stream(128, 6), "too much entropy-decoding work", new Jpeg2000DecoderLimits { MaxTier1Work = 1L << 20 });
    }

    /// <summary>
    ///     Tests the largest allowed image shape (8192 x 8192, 64 x 64 blocks, 88 passes per block) from about 60 KB
    ///     of input against the default budget formula: the input-scaled allowance is far below the work the stream
    ///     asks for, so it must be rejected. Only the budget arithmetic is exercised; the (CPU-expensive) decode is not.
    /// </summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HostileMaximumImageTier1Work_ExceedsDefaultBudget()
    {
        var data = HostileTier1Stream(8192, 6);
        Assert.InRange(data.Length, 60_000, 80_000);
        var budget = new Jpeg2000Codec.DecodeBudget(Jpeg2000DecoderLimits.Default, data.Length);
        budget.ChargeTier1((1L << 24) + ((1L << 14) * data.Length));
        Assert.Throws<InvalidDataException>(() => budget.ChargeTier1(1));
        Assert.True(8192L * 8192 * 88 > (1L << 24) + ((1L << 14) * data.Length));
    }
    /// <summary>Tests that the hostile stream shape decodes when the work is within the budget (so the budget is the only cause).</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_SmallHostileShape_Decodes()
    {
        var image = Jpeg2000Codec.Decode(HostileTier1Stream(128, 6, 10));
        Assert.Equal(128, image.Width);
    }

    /// <summary>Tests the image-size caps with sizes within the dimension limit so the caps themselves are reached.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_ImageBeyondDimensionLimit_ThrowsInvalidData()
    {
        var data = Encode(Img(16, 16, 1), Rev(2));
        AssertMalformed(Resize(data, 8193, 16, 8193, 16), "pixel limit");
        AssertMalformed(Resize(data, 16, 0x7FFFFFFF, 16, 0x7FFFFFFF), "pixel limit");
        AssertMalformed(Resize(data, 4096, 4096, 4096, 4096), "decoder sample limit", new Jpeg2000DecoderLimits { MaxTotalSamples = 1 << 20 });
        AssertMalformed(Resize(data, 4096, 4096, 4096, 4096), "tile exceeds the decoder sample limit", new Jpeg2000DecoderLimits { MaxTileSamples = 1 << 20 });
    }

    /// <summary>Tests the tile-count caps: the format maximum at parse time and the configured maximum.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TooManyTiles_ThrowsInvalidData()
    {
        var data = Encode(Img(16, 16, 1), Rev(2));
        AssertMalformed(Resize(data, 8192, 8192, 1, 1), "too many tiles");
        AssertMalformed(Resize(data, 256, 256, 64, 64), "too many tiles", new Jpeg2000DecoderLimits { MaxTiles = 10 });

        var tiled = Rev(2);
        tiled.TileWidth = 64;
        tiled.TileHeight = 64;
        var tiledData = Encode(Img(256, 256, 1), tiled);
        AssertMalformed(tiledData, "too many tiles", new Jpeg2000DecoderLimits { MaxTiles = 15 });
        Assert.Equal(256, Jpeg2000Codec.Decode(tiledData, new Jpeg2000DecoderLimits { MaxTiles = 16 }).Width);
    }

    /// <summary>Tests that hostile SIZ geometry (32-bit extremes, wrapping tile counts) is rejected quickly as malformed.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HostileSizTileGrid_ThrowsInvalidData()
    {
        var data = Encode(Img(16, 16, 1), Rev(2));
        const uint max = 0xFFFFFFFF;

        // Each axis alone has about 2^32 tiles (XTsiz = 1, grid origin equal to the far image offset).
        var xOnly = PatchSiz(data, (6, max), (14, 0), (22, 1), (30, 0));
        AssertMalformed(xOnly, "too many tiles");
        var yOnly = PatchSiz(data, (10, max), (18, 0), (26, 1), (34, 0));
        AssertMalformed(yOnly, "too many tiles");

        // Both axes huge: the unchecked long product (2^32-1)^2 wraps negative.
        var both = PatchSiz(data, (6, max), (10, max), (22, 1), (26, 1), (14, 0), (18, 0), (30, 0), (34, 0));
        AssertMalformed(both, "too many tiles");

        // Each axis is within the format limit but the product is not.
        AssertMalformed(Resize(data, 300, 300, 1, 1), "too many tiles");

        // Tile size at the 32-bit extreme: one tile per axis, but the image itself exceeds the pixel limit.
        var wrap = PatchSiz(data, (6, max), (10, max), (14, 0), (18, 0), (22, max), (26, max), (30, 0), (34, 0));
        AssertMalformed(wrap, "exceed");
    }

    /// <summary>Patches 32-bit SIZ fields at offsets relative to the SIZ marker.</summary>
    private static byte[] PatchSiz(byte[] data, params (int Offset, uint Value)[] fields)
    {
        var siz = FindMarker(data, 0xFF51);
        var copy = (byte[])data.Clone();
        foreach (var (offset, value) in fields)
        {
            copy[siz + offset] = (byte)(value >> 24);
            copy[siz + offset + 1] = (byte)(value >> 16);
            copy[siz + offset + 2] = (byte)(value >> 8);
            copy[siz + offset + 3] = (byte)value;
        }

        return copy;
    }

    /// <summary>Tests the code-block cap of a tile with a tight custom limit and with the default limit at the maximum image size.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TooManyCodeBlocks_ThrowsInvalidData()
    {
        var o = Rev(0);
        o.CodeBlockWidthExp = 2;
        o.CodeBlockHeightExp = 2;
        var data = Encode(Img(16, 16, 1), o);
        AssertMalformed(data, "tile has too many code-blocks", new Jpeg2000DecoderLimits { MaxTileCodeBlocks = 15 });
        _ = Jpeg2000Codec.Decode(data, new Jpeg2000DecoderLimits { MaxTileCodeBlocks = 16 });

        // 8192 x 8192 samples is within every size limit but needs 4 Mi code-blocks of 4 x 4 samples.
        AssertMalformed(Resize(data, 8192, 8192, 8192, 8192), "tile has too many code-blocks");
    }

    /// <summary>Tests the precinct cap with a size within the dimension limit.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TooManyPrecincts_ThrowsInvalidData()
    {
        var o = Rev(0);
        o.Precincts = [(0, 0)];
        var data = Encode(Img(16, 16, 1), o);

        // 4096 x 4096 one-sample precincts: 16 Mi precincts against the cap of 256 Ki.
        AssertMalformed(Resize(data, 4096, 4096, 4096, 4096), "tile has more precincts than the decoder limit");
        AssertMalformed(data, "tile has more precincts than the decoder limit", new Jpeg2000DecoderLimits { MaxTilePrecincts = 255 });
        _ = Jpeg2000Codec.Decode(data, new Jpeg2000DecoderLimits { MaxTilePrecincts = 256 });
    }

    /// <summary>Tests the packet cap (precincts times layers) and the bound given by the amount of tile data.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_TooManyPackets_ThrowsInvalidData()
    {
        var o = Rev(0);
        o.Layers = 3;
        o.Precincts = [(4, 4)];
        var data = Encode(Img(32, 32, 1), o);
        AssertMalformed(data, "tile has more packets than the decoder limit", new Jpeg2000DecoderLimits { MaxTilePackets = 11 });
        _ = Jpeg2000Codec.Decode(data, new Jpeg2000DecoderLimits { MaxTilePackets = 12 });

        // 512 x 512 one-sample precincts are within the precinct cap but far more packets than a few hundred bytes can hold.
        var tiny = Rev(0);
        tiny.Precincts = [(0, 0)];
        var hostile = Resize(Encode(Img(16, 16, 1), tiny), 512, 512, 512, 512);
        AssertMalformed(hostile, "tile has more packets than its data can carry");
    }

    /// <summary>Tests that a codestream missing a whole tile is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_MissingTile_ThrowsInvalidData()
    {
        var o = Rev(2);
        o.TileWidth = 16;
        o.TileHeight = 16;
        var data = Encode(Img(32, 16, 1), o);
        var first = FindMarker(data, 0xFF90);
        var second = first + 2;
        while (!(data[second] == 0xFF && data[second + 1] == 0x90))
        {
            second++;
        }

        byte[] cut = [.. data[..second], 0xFF, 0xD9];
        var ex = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(cut));
        Assert.Null(ex.InnerException);
        _ = Jpeg2000Codec.Decode(data);
    }

    /// <summary>Tests custom decoder limits.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_CustomLimits_AreEnforced()
    {
        var data = Encode(Img(16, 8, 3), Rev(1));
        var strict = new Jpeg2000DecoderLimits { MaxWidth = 8 };
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(data, strict));
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(new MemoryStream(data), strict));
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(data, new Jpeg2000DecoderLimits { MaxTotalSamples = 16 }));
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(data, new Jpeg2000DecoderLimits { MaxInputBytes = 10 }));
        Assert.Equal(16, Jpeg2000Codec.Decode(data, Jpeg2000DecoderLimits.Default).Width);
    }

    /// <summary>Tests that invalid limits are rejected as argument errors.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_InvalidLimits_ThrowsArgumentOutOfRange()
    {
        var data = Encode(Img(8, 8, 1), Rev(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Jpeg2000Codec.Decode(data, new Jpeg2000DecoderLimits { MaxWidth = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Jpeg2000Codec.Decode(data, new Jpeg2000DecoderLimits { MaxTotalSamples = -1 }));
        Assert.Throws<ArgumentNullException>(() => Jpeg2000Codec.Decode(data, null!));
    }

    /// <summary>Tests that hostile quantization exponents are rejected during geometry construction.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_HostileExponents_ThrowsInvalidData()
    {
        var data = Encode(Img(8, 8, 1), Rev(1));
        var qcd = FindMarker(data, 0xFF5C);
        foreach (var exponent in new[] { 31, 40 })
        {
            var ex = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(Patch(data, qcd + 5, (byte)(exponent << 3))));
            Assert.Null(ex.InnerException);
        }
    }

    /// <summary>Tests that a ROI shift pushing the bit-plane count beyond the decoder maximum is unsupported.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RoiShiftBeyondMaxBitPlanes_ThrowsUnsupported()
    {
        var o = Rev(1);
        o.RoiShift = 13;
        var data = Encode(Img(8, 8, 1), o);
        var rgn = FindMarker(data, 0xFF5E);
        Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(Patch(data, rgn + 6, 30)));
    }

    /// <summary>Tests that a segmentation symbol that does not decode as 1010 is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_WrongSegmentationSymbol_ThrowsInvalidData()
    {
        var plain = Encode(Img(16, 16, 1), Rev(1));
        var cod = FindMarker(plain, 0xFF52);
        var forced = Patch(plain, cod + 12, 0x20);
        var ex = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.Decode(forced));
        Assert.Null(ex.InnerException);
    }

    /// <summary>Tests that a valid segmentation-symbol stream still decodes exactly.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_SegmentationSymbols_RoundTripsExactly()
    {
        var o = Rev(2);
        o.CodeBlockStyle = 0x20;
        AssertExact(Img(24, 24, 1), o);
    }

    /// <summary>Tests that four channels in an unrecognized color space are rejected rather than treated as CMYK.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_FourChannelsInUnknownColorSpace_ThrowsUnsupported()
    {
        var image = Img(8, 8, 4);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options { EnumCs = 99 });
        var ex = Assert.Throws<UnsupportedImageFeatureException>(() => Jpeg2000Codec.Decode(jp2));
        Assert.Equal("jpeg2000-color-space", ex.Feature);
    }

    /// <summary>Tests that an sRGB image with an undeclared fourth channel keeps only the three color channels.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_SrgbWithExtraChannelWithoutCdef_IgnoresExtraChannel()
    {
        var image = Img(8, 8, 4);
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, Encode(image, Rev(1)), new J2kJp2Options { EnumCs = 16 });
        var decoded = Jpeg2000Codec.Decode(jp2);
        Assert.Equal(Jpeg2000ColorSpace.Srgb, decoded.ColorSpace);
        Assert.Equal(3, decoded.ColorChannelCount);
        Assert.False(decoded.HasAlpha);
    }

    /// <summary>Tests that a raw four-component codestream is treated as CMYK by the documented heuristic.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RawFourComponents_IsCmyk() =>
        Assert.Equal(Jpeg2000ColorSpace.Cmyk, Jpeg2000Codec.Decode(Encode(Img(8, 8, 4), Rev(1))).ColorSpace);
}
