// cspell:ignore ftyp ihdr colr jp2h jp2c ppm ppt tlm plm plt crg poc rgn coc qcc qcd sot sod eoc siz csiz xrsiz yrsiz xtsiz ytsiz xosiz xtosiz bpcc unkc ipr zppm zppt tpsot lsot isot psot
using System.Text;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Fail-closed validation tests of the <see cref="Jpeg2000Codec"/> against ISO/IEC 15444-1 (ITU-T T.800)
///     Annex A (codestream markers) and Annex I (JP2 boxes): each test hand-patches a small valid stream and asserts
///     the documented outcome, either an exception of a documented type or, for a deliberate leniency, success.
/// </summary>
public class Jpeg2000ValidationTests
{
    private static readonly byte[] Marker64 = [0xFF, 0x64];

    // ------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------

    private static J2kImage Img(int comps) => Jpeg2000TestEncoder.MakeImage(8, 8, comps, 8, 1, 24, false);

    private static byte[] Stream(int comps = 1, int levels = 2, Action<J2kOptions>? configure = null)
    {
        var options = new J2kOptions { Levels = levels };
        configure?.Invoke(options);
        return Jpeg2000TestEncoder.Encode(Img(comps), options);
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

        throw new InvalidOperationException("marker not found in test stream.");
    }

    private static byte[] Replace(byte[] data, int offset, params byte[] bytes)
    {
        var copy = (byte[])data.Clone();
        Array.Copy(bytes, 0, copy, offset, bytes.Length);
        return copy;
    }

    private static byte[] InsertAt(byte[] data, int offset, byte[] extra) =>
        [.. data[..offset], .. extra, .. data[offset..]];

    private static byte[] Segment(int marker, params byte[] payload) =>
        [(byte)(marker >> 8), (byte)marker, (byte)((payload.Length + 2) >> 8), (byte)(payload.Length + 2), .. payload];

    private static int SegmentLength(byte[] data, int at) => 2 + ((data[at + 2] << 8) | data[at + 3]);

    /// <summary>Inserts bytes directly after the SIZ segment, which places them in the main header.</summary>
    private static byte[] InsertInMainHeader(byte[] data, byte[] extra)
    {
        var siz = FindMarker(data, 0xFF51);
        return InsertAt(data, siz + SegmentLength(data, siz), extra);
    }

    /// <summary>Inserts bytes directly after the first SOT segment (in the tile-part header) and grows Psot to match.</summary>
    private static byte[] InsertInTileHeader(byte[] data, byte[] extra)
    {
        var sot = FindMarker(data, 0xFF90);
        var psot = (data[sot + 6] << 24) | (data[sot + 7] << 16) | (data[sot + 8] << 8) | data[sot + 9];
        Assert.NotEqual(0, psot);
        psot += extra.Length;
        var copy = Replace(data, sot + 6, (byte)(psot >> 24), (byte)(psot >> 16), (byte)(psot >> 8), (byte)psot);
        return InsertAt(copy, sot + 12, extra);
    }

    private static void AssertRejected<T>(byte[] data, string? cause = null)
        where T : Exception
    {
        var ex = Assert.Throws<T>(() => Jpeg2000Codec.Decode(data));
        Assert.Null(ex.InnerException);
        if (cause is not null)
        {
            Assert.Contains(cause, ex.Message);
        }
    }

    private static void AssertDecodes(byte[] data, byte[] expectedSamples) =>
        Assert.Equal(expectedSamples, Jpeg2000Codec.Decode(data).ColorSamples);

    // ------------------------------------------------------------------------------------------
    // JP2 container (ITU-T T.800 Annex I)
    // ------------------------------------------------------------------------------------------

    private static byte[] Box(string type, params byte[][] parts)
    {
        var content = parts.SelectMany(p => p).ToArray();
        var length = content.Length + 8;
        return [(byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length, .. Encoding.ASCII.GetBytes(type), .. content];
    }

    private static byte[] Content(byte[] box) => box[8..];

    /// <summary>The pieces of a valid three-component JP2 file, so each test can recombine them.</summary>
    private static byte[] Compose(params byte[][] boxes) => [.. boxes.SelectMany(b => b)];

    private static byte[] Header(params byte[][] children) => Box("jp2h", children);

    private sealed record Jp2Pieces(byte[] Signature, byte[] FileType, byte[] Ihdr, byte[] Colr, byte[] Codestream)
    {
        public byte[] CodestreamBox => Box("jp2c", Codestream);

        public byte[] Valid() => Compose(Signature, FileType, Header(Ihdr, Colr), CodestreamBox);
    }

    private static Jp2Pieces Pieces()
    {
        var image = Img(3);
        var codestream = Jpeg2000TestEncoder.Encode(image, new J2kOptions { Levels = 1 });
        var jp2 = Jpeg2000TestEncoder.WrapJp2(image, codestream, new J2kJp2Options());
        var boxes = new List<byte[]>();
        for (var pos = 0; pos < jp2.Length;)
        {
            var length = (jp2[pos] << 24) | (jp2[pos + 1] << 16) | (jp2[pos + 2] << 8) | jp2[pos + 3];
            boxes.Add(jp2[pos..(pos + length)]);
            pos += length;
        }

        var jp2h = boxes.Single(b => Encoding.ASCII.GetString(b, 4, 4) == "jp2h");
        var ihdrLength = (jp2h[8] << 24) | (jp2h[9] << 16) | (jp2h[10] << 8) | jp2h[11];
        return new Jp2Pieces(boxes[0], boxes[1], jp2h[8..(8 + ihdrLength)], jp2h[(8 + ihdrLength)..], codestream);
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }

    /// <summary>Asserts that Decode, GetInfo (seekable) and GetInfo (non-seekable) all reject the file with the same cause.</summary>
    private static void AssertJp2Rejected(byte[] file, string cause)
    {
        AssertRejected<InvalidDataException>(file, cause);
        var seekable = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.GetInfo(new MemoryStream(file)));
        Assert.Contains(cause, seekable.Message);
        var forward = Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.GetInfo(new NonSeekableStream(file)));
        Assert.Contains(cause, forward.Message);
    }

    /// <summary>Asserts that Decode and both GetInfo paths accept the file and report the 8 x 8 image.</summary>
    private static void AssertJp2Accepted(byte[] file)
    {
        var image = Jpeg2000Codec.Decode(file);
        Assert.Equal(8, image.Width);
        Assert.Equal(8, image.Height);
        Assert.Equal(8, Jpeg2000Codec.GetInfo(new MemoryStream(file)).Width);
        Assert.Equal(8, Jpeg2000Codec.GetInfo(new NonSeekableStream(file)).Height);
    }

    /// <summary>Tests that the valid baseline file used by the other container tests is accepted.</summary>
    [Fact]
    public void Jpeg2000Codec_Jp2Baseline_IsAccepted() => AssertJp2Accepted(Pieces().Valid());

    /// <summary>Tests that a JP2 file without the file type box is rejected by Decode and GetInfo (T.800 I.5.2).</summary>
    [Fact]
    public void Jpeg2000Codec_Jp2MissingFileType_IsRejected()
    {
        var p = Pieces();
        AssertJp2Rejected(Compose(p.Signature, Header(p.Ihdr, p.Colr), p.CodestreamBox), "file type box");
    }

    /// <summary>Tests that a file type box that is not the second box is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Jp2FileTypeNotSecond_IsRejected()
    {
        var p = Pieces();
        AssertJp2Rejected(Compose(p.Signature, Header(p.Ihdr, p.Colr), p.FileType, p.CodestreamBox), "file type box");
    }

    /// <summary>Tests the file type box length rule: at least 8 content bytes and a multiple of 4.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(9, false)]
    [InlineData(8, true)]
    [InlineData(12, true)]
    [InlineData(16, true)]
    public void Jpeg2000Codec_Jp2FileTypeLength_IsChecked(int contentLength, bool accepted)
    {
        var p = Pieces();
        var content = new byte[contentLength];
        Encoding.ASCII.GetBytes("jp2 ").AsSpan(0, Math.Min(4, contentLength)).CopyTo(content);
        var file = Compose(p.Signature, Box("ftyp", content), Header(p.Ihdr, p.Colr), p.CodestreamBox);
        if (accepted)
        {
            AssertJp2Accepted(file);
        }
        else
        {
            AssertJp2Rejected(file, "file type box");
        }
    }

    /// <summary>Tests that a JP2 file with no header box is rejected (T.800 I.5.3).</summary>
    [Fact]
    public void Jpeg2000Codec_Jp2MissingHeaderBox_IsRejected()
    {
        var p = Pieces();
        AssertJp2Rejected(Compose(p.Signature, p.FileType, p.CodestreamBox), "header box must precede");
    }

    /// <summary>Tests that a header box after the codestream box is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Jp2HeaderAfterCodestream_IsRejected()
    {
        var p = Pieces();
        AssertJp2Rejected(Compose(p.Signature, p.FileType, p.CodestreamBox, Header(p.Ihdr, p.Colr)), "header box must precede");
    }

    /// <summary>Tests that a header box without an image header box is rejected.</summary>
    [Fact]
    public void Jpeg2000Codec_Jp2HeaderWithoutIhdr_IsRejected()
    {
        var p = Pieces();
        AssertJp2Rejected(Compose(p.Signature, p.FileType, Header(p.Colr), p.CodestreamBox), "no image header box");
        AssertJp2Rejected(Compose(p.Signature, p.FileType, Header(), p.CodestreamBox), "no image header box");
    }

    /// <summary>Tests that an image header box must be exactly 14 bytes long.</summary>
    [Theory]
    [InlineData(13)]
    [InlineData(15)]
    public void Jpeg2000Codec_Jp2IhdrLength_IsChecked(int contentLength)
    {
        var p = Pieces();
        var content = new byte[contentLength];
        Content(p.Ihdr).AsSpan(0, Math.Min(contentLength, 14)).CopyTo(content);
        AssertJp2Rejected(Compose(p.Signature, p.FileType, Header(Box("ihdr", content), p.Colr), p.CodestreamBox), "image header box length");
    }

    /// <summary>Tests that an image header box whose height or width differs from SIZ is rejected.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void Jpeg2000Codec_Jp2IhdrSizeMismatch_IsRejected(int contentOffset)
    {
        var p = Pieces();
        var content = Content(p.Ihdr);
        content[contentOffset]++;
        AssertJp2Rejected(Compose(p.Signature, p.FileType, Header(Box("ihdr", content), p.Colr), p.CodestreamBox), "does not match");
    }

    /// <summary>Tests the documented JP2 leniencies: each of these files decodes, as it does with OpenJPEG.</summary>
    [Theory]
    [InlineData("no colr")]
    [InlineData("colr before ihdr")]
    [InlineData("other brand")]
    [InlineData("NC, BPC, C, UnkC and IPR differ")]
    [InlineData("second ihdr ignored")]
    [InlineData("second jp2h without ihdr")]
    public void Jpeg2000Codec_Jp2Leniency_IsAccepted(string variant)
    {
        var p = Pieces();
        var ihdr = Content(p.Ihdr);
        var file = variant switch
        {
            "no colr" => Compose(p.Signature, p.FileType, Header(p.Ihdr), p.CodestreamBox),
            "colr before ihdr" => Compose(p.Signature, p.FileType, Header(p.Colr, p.Ihdr), p.CodestreamBox),
            "other brand" => Compose(p.Signature, Box("ftyp", Encoding.ASCII.GetBytes("abcd"), new byte[4], Encoding.ASCII.GetBytes("abcd")), Header(p.Ihdr, p.Colr), p.CodestreamBox),
            "NC, BPC, C, UnkC and IPR differ" => Compose(
                p.Signature, p.FileType, Header(Box("ihdr", ihdr[..8], [0, 9, 3, 6, 2, 1]), p.Colr), p.CodestreamBox),
            "second ihdr ignored" => Compose(
                p.Signature, p.FileType, Header(p.Ihdr, p.Colr, Box("ihdr", new byte[14])), p.CodestreamBox),
            "second jp2h without ihdr" => Compose(p.Signature, p.FileType, p.Valid()[(p.Signature.Length + p.FileType.Length)..^p.CodestreamBox.Length], Header(), p.CodestreamBox),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        AssertJp2Accepted(file);
    }

    // ------------------------------------------------------------------------------------------
    // Codestream marker placement and ordering (T.800 A.2, A.3)
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests that SOC, SOD, EOC, a second SIZ and the tile-part-only markers PPT, PLT and SOP are rejected in the main header.</summary>
    [Theory]
    [InlineData(0xFF4F)]
    [InlineData(0xFF93)]
    [InlineData(0xFFD9)]
    [InlineData(0xFF51)]
    [InlineData(0xFF61)]
    [InlineData(0xFF58)]
    [InlineData(0xFF91)]
    public void Jpeg2000Codec_Decode_MarkerNotAllowedInMainHeader_IsRejected(int marker)
    {
        var data = Stream();
        var extra = marker is 0xFF4F or 0xFF93 or 0xFFD9 ? new[] { (byte)(marker >> 8), (byte)marker } : Segment(marker, 0);
        AssertRejected<InvalidDataException>(InsertInMainHeader(data, extra));
    }

    /// <summary>Tests that the main-header-only markers TLM, PLM and CRG, COM and an unknown marker are skipped in the main header.</summary>
    [Theory]
    [InlineData(0xFF55)]
    [InlineData(0xFF57)]
    [InlineData(0xFF63)]
    [InlineData(0xFF64)]
    [InlineData(0xFF70)]
    public void Jpeg2000Codec_Decode_SkippedMarkerInMainHeader_IsAccepted(int marker)
    {
        var data = Stream();
        AssertDecodes(InsertInMainHeader(data, Segment(marker, 0, 1, 2)), Jpeg2000Codec.Decode(data).ColorSamples);
    }

    /// <summary>Tests that the main-header-only markers and SOP are rejected in a tile-part header.</summary>
    [Theory]
    [InlineData(0xFF50)]
    [InlineData(0xFF51)]
    [InlineData(0xFF55)]
    [InlineData(0xFF57)]
    [InlineData(0xFF60)]
    [InlineData(0xFF63)]
    [InlineData(0xFF91)]
    public void Jpeg2000Codec_Decode_MarkerNotAllowedInTilePartHeader_IsRejected(int marker)
    {
        var data = Stream();
        var rejected = InsertInTileHeader(data, Segment(marker, 0, 1, 2));
        var ex = Assert.ThrowsAny<Exception>(() => Jpeg2000Codec.Decode(rejected));
        Assert.Null(ex.InnerException);
        Assert.Contains(ex.GetType(), new[] { typeof(InvalidDataException), typeof(UnsupportedImageFeatureException) });
        if (marker != 0xFF50)
        {
            Assert.IsType<InvalidDataException>(ex);
            Assert.Contains("tile-part header", ex.Message);
        }
    }

    /// <summary>Tests that PLT, COM and an unknown marker are skipped in a tile-part header.</summary>
    [Theory]
    [InlineData(0xFF58)]
    [InlineData(0xFF64)]
    [InlineData(0xFF70)]
    public void Jpeg2000Codec_Decode_SkippedMarkerInTilePartHeader_IsAccepted(int marker)
    {
        var data = Stream();
        AssertDecodes(InsertInTileHeader(data, Segment(marker, 0, 1, 2)), Jpeg2000Codec.Decode(data).ColorSamples);
    }

    /// <summary>Tests that SIZ must directly follow SOC, and that COD and QCD are required in the main header.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_MarkerOrdering_IsEnforced()
    {
        var data = Stream();
        AssertRejected<InvalidDataException>(Replace(data, FindMarker(data, 0xFF51), 0xFF, 0x64), "SIZ marker segment must follow SOC");
        AssertRejected<InvalidDataException>(Replace(data, FindMarker(data, 0xFF52), 0xFF, 0x64), "lacks a COD or QCD");
        AssertRejected<InvalidDataException>(Replace(data, FindMarker(data, 0xFF5C), 0xFF, 0x64), "lacks a COD or QCD");
        Assert.Throws<InvalidDataException>(() => Jpeg2000Codec.GetInfo(new MemoryStream(Replace(data, FindMarker(data, 0xFF51), 0xFF, 0x64))));
    }

    /// <summary>Tests that a tile-part header must end with SOD: a tile-part without it never decodes.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_MissingSod_IsRejected()
    {
        var data = Stream();
        var ex = Assert.ThrowsAny<Exception>(() => Jpeg2000Codec.Decode(Replace(data, FindMarker(data, 0xFF93), Marker64)));
        Assert.Null(ex.InnerException);
        Assert.IsType<InvalidDataException>(ex);
    }

    /// <summary>Tests the documented leniency that bytes after the EOC marker are ignored.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_BytesAfterEoc_AreIgnored()
    {
        var data = Stream();
        AssertDecodes([.. data, 0x01, 0x02, 0x03], Jpeg2000Codec.Decode(data).ColorSamples);
    }

    // ------------------------------------------------------------------------------------------
    // PPM / PPT index sequencing (T.800 A.7.4, A.7.5)
    // ------------------------------------------------------------------------------------------

    /// <summary>Tests that a repeated Zppm index is rejected while a gap in the sequence is tolerated.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PpmIndexes_RepeatRejectedGapAccepted()
    {
        var data = Stream(configure: o => o.HeaderMode = J2kHeaderMode.Ppm);
        var expected = Jpeg2000Codec.Decode(data).ColorSamples;
        var ppm = FindMarker(data, 0xFF60);
        var repeated = InsertAt(data, ppm + SegmentLength(data, ppm), data[ppm..(ppm + SegmentLength(data, ppm))]);
        AssertRejected<InvalidDataException>(repeated, "duplicate PPM");
        AssertDecodes(Replace(data, ppm + 4, 3), expected);
    }

    /// <summary>Tests that a repeated Zppt index is rejected while a gap in the sequence is tolerated.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_PptIndexes_RepeatRejectedGapAccepted()
    {
        var data = Stream(configure: o => o.HeaderMode = J2kHeaderMode.Ppt);
        var expected = Jpeg2000Codec.Decode(data).ColorSamples;
        var ppt = FindMarker(data, 0xFF61);
        var segment = data[ppt..(ppt + SegmentLength(data, ppt))];
        AssertRejected<InvalidDataException>(InsertInTileHeader(data, segment), "duplicate PPT");
        AssertDecodes(Replace(data, ppt + 4, 3), expected);
    }

    // ------------------------------------------------------------------------------------------
    // Field-level value ranges
    // ------------------------------------------------------------------------------------------

    /// <summary>
    ///     Invalid field values: the kind of stream to build, the marker, the patches ("offset=hex;offset=hex",
    ///     offsets relative to the first byte of the marker so the segment payload starts at 4) and whether the
    ///     outcome is <see cref="UnsupportedImageFeatureException"/> rather than <see cref="InvalidDataException"/>.
    /// </summary>
    public static TheoryData<string, int, string, bool> InvalidFields => new()
    {
        // SIZ: Xsiz, Ysiz, XOsiz, YOsiz, XTsiz, YTsiz, XTOsiz, YTOsiz, Csiz, XRsiz, YRsiz, Rsiz.
        { "base", 0xFF51, "6=00000000", false },
        { "base", 0xFF51, "10=00000000", false },
        { "base", 0xFF51, "14=00000008", false },
        { "base", 0xFF51, "18=00000008", false },
        { "base", 0xFF51, "22=00000000", false },
        { "base", 0xFF51, "26=00000000", false },
        { "base", 0xFF51, "30=00000001", false },
        { "base", 0xFF51, "34=00000001", false },
        { "base", 0xFF51, "14=00000003;22=00000002", false },
        { "base", 0xFF51, "38=0000", false },
        { "base", 0xFF51, "38=4001", false },
        { "base", 0xFF51, "38=0011", true },
        { "base", 0xFF51, "41=00", false },
        { "base", 0xFF51, "42=00", false },
        { "base", 0xFF51, "4=4000", true },
        { "base", 0xFF51, "4=8000", true },
        { "base", 0xFF51, "40=10", true },

        // COD: progression order, layers, decomposition levels, code-block exponents, style, transform, MCT.
        { "base", 0xFF52, "5=05", false },
        { "base", 0xFF52, "6=0000", false },
        { "base", 0xFF52, "9=21", false },
        { "base", 0xFF52, "10=09", false },
        { "base", 0xFF52, "11=09", false },
        { "base", 0xFF52, "10=05;11=05", false },
        { "base", 0xFF52, "12=40", true },
        { "base", 0xFF52, "12=80", true },
        { "base", 0xFF52, "13=02", true },
        { "base", 0xFF52, "8=02", true },

        // Precinct exponents: zero is only allowed at the lowest resolution.
        { "precinct", 0xFF52, "15=50", false },
        { "precinct", 0xFF52, "16=05", false },

        // QCD: quantization style 3 does not exist.
        { "base", 0xFF5C, "4=43", false },

        // COC / QCC component index beyond Csiz.
        { "coc", 0xFF53, "4=03", false },
        { "coc", 0xFF5D, "4=03", false },

        // RGN: component index, style, shift.
        { "roi", 0xFF5E, "4=01", false },
        { "roi", 0xFF5E, "5=01", true },
        { "roi", 0xFF5E, "6=26", false },

        // POC: RSpoc >= REpoc, CSpoc >= CEpoc, LYEpoc = 0, progression order.
        { "poc", 0xFF5F, "4=02", false },
        { "poc", 0xFF5F, "5=01", false },
        { "poc", 0xFF5F, "6=0000", false },
        { "poc", 0xFF5F, "10=05", false },

        // SOT: Lsot, Isot, Psot (too small, beyond the data), TPsot.
        { "base", 0xFF90, "2=000B", false },
        { "base", 0xFF90, "4=0009", false },
        { "base", 0xFF90, "6=0000000D", false },
        { "base", 0xFF90, "6=7FFFFFFF", false },
        { "base", 0xFF90, "10=01", false },
    };

    private static byte[] BuildForKind(string kind) => kind switch
    {
        "base" => Stream(),
        "precinct" => Stream(configure: o => o.Precincts = [(5, 5), (5, 5), (5, 5)]),
        "coc" => Stream(3, configure: o => o.ComponentLevels = [2, 1, 2]),
        "roi" => Stream(configure: o => o.RoiShift = 3),
        "poc" => Stream(configure: o => o.Poc = [new J2kPoc(0, 0, 1, 2, 1, 4), new J2kPoc(0, 0, 1, 3, 1, 1)]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static byte[] ApplyPatches(byte[] data, int marker, string patches)
    {
        var at = FindMarker(data, marker);
        var result = data;
        foreach (var patch in patches.Split(';'))
        {
            var parts = patch.Split('=');
            result = Replace(result, at + int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), Convert.FromHexString(parts[1]));
        }

        return result;
    }

    /// <summary>Tests that every invalid header field value is rejected with the documented exception type and no wrapped inner exception.</summary>
    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Jpeg2000Codec_Decode_InvalidFieldValue_IsRejected(string kind, int marker, string patches, bool unsupported)
    {
        var data = ApplyPatches(BuildForKind(kind), marker, patches);
        if (unsupported)
        {
            AssertRejected<UnsupportedImageFeatureException>(data);
        }
        else
        {
            AssertRejected<InvalidDataException>(data);
        }

        if (marker == 0xFF51)
        {
            // GetInfo parses only SIZ, so it must reject exactly the same SIZ values.
            var ex = Assert.ThrowsAny<Exception>(() => Jpeg2000Codec.GetInfo(new MemoryStream(data)));
            Assert.IsType(unsupported ? typeof(UnsupportedImageFeatureException) : typeof(InvalidDataException), ex);
        }
    }

    /// <summary>Tests that the largest legal code-block (2^6 x 2^6, xcb + ycb = 12) is accepted.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_MaximumCodeBlockArea_IsAccepted()
    {
        var data = Stream(configure: o =>
        {
            o.CodeBlockWidthExp = 6;
            o.CodeBlockHeightExp = 6;
        });
        Assert.Equal(8, Jpeg2000Codec.Decode(data).Width);
    }
}
