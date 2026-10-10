using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests that exercise <see cref="Jpeg2000Codec"/> against the real-encoder fixture
///     corpus in <c>Jpeg2000Fixtures</c> (see <c>Jpeg2000Fixtures\README.md</c>), generated with
///     ImageMagick/OpenJPEG from synthetic source images. These give evidence that is independent
///     of the test-only <c>Jpeg2000TestEncoder</c>: the expected pixels come from the source PNG
///     files decoded with <see cref="PngCodec"/>.
/// </summary>
public class Jpeg2000FixtureTests
{
    /// <summary>
    ///     The directory containing the JPEG 2000 fixture corpus, copied to the test output
    ///     directory by the test project's <c>Jpeg2000Fixtures\**</c> content item.
    ///     <c>Path.Join</c> is used instead of <see cref="Path.Combine(string, string)"/> to avoid
    ///     CodeQL's <c>cs/path-combine</c> rule.
    /// </summary>
    private static string AssetsPath => Path.Join(AppContext.BaseDirectory, "Jpeg2000Fixtures");

    /// <summary>
    ///     The exact-match fixtures: file name, source PNG, COD progression order, quality layers,
    ///     decomposition levels, SIZ component count and SIZ bit depth, all verified from the
    ///     fixture's own marker bytes.
    /// </summary>
    public static readonly TheoryData<string, string, int, int, int, int, int> LosslessFixtures =
    [
        ("lossless_rct.jp2", "source_rgb.png", 0, 1, 5, 3, 8),
        ("lossless_raw.j2k", "source_rgb.png", 0, 1, 5, 3, 8),
        ("progression_rlcp.jp2", "source_rgb.png", 1, 1, 5, 3, 8),
        ("progression_rpcl.jp2", "source_rgb.png", 2, 1, 5, 3, 8),
        ("progression_pcrl.jp2", "source_rgb.png", 3, 1, 5, 3, 8),
        ("progression_cprl.jp2", "source_rgb.png", 4, 1, 5, 3, 8),
        ("resolutions3.jp2", "source_rgb.png", 0, 1, 2, 3, 8),
        ("layers3.jp2", "source_rgb.png", 0, 3, 5, 3, 8),
        ("gray.jp2", "source_gray.png", 0, 1, 5, 1, 8),
        ("alpha.jp2", "source_rgba.png", 0, 1, 5, 4, 8),
        ("depth16.jp2", "source_rgb.png", 0, 1, 5, 3, 16)
    ];

    /// <summary>Resolves a fixture file name within the corpus directory.</summary>
    private static string Resolve(string fileName) => Path.Join(AssetsPath, fileName);

    /// <summary>Reads the COD and SIZ fields needed for marker assertions from a codestream or JP2 file.</summary>
    private static (int Progression, int Layers, int Levels, int Components, int Depth) ReadMarkers(byte[] data)
    {
        // Locate the SOC + SIZ pair (a JP2 file wraps the codestream in boxes)
        var pos = 0;
        while (!(data[pos] == 0xFF && data[pos + 1] == 0x4F && data[pos + 2] == 0xFF && data[pos + 3] == 0x51))
        {
            pos++;
        }

        pos += 2;
        int progression = -1, layers = 0, levels = 0, components = 0, depth = 0;
        while (data[pos] == 0xFF && data[pos + 1] != 0x90)
        {
            var marker = data[pos + 1];
            var length = (data[pos + 2] << 8) | data[pos + 3];
            var body = pos + 4;
            if (marker == 0x51)
            {
                components = (data[body + 34] << 8) | data[body + 35];
                depth = (data[body + 36] & 0x7F) + 1;
            }
            else if (marker == 0x52)
            {
                progression = data[body + 1];
                layers = (data[body + 2] << 8) | data[body + 3];
                levels = data[body + 5];
            }

            pos += 2 + length;
        }

        return (progression, layers, levels, components, depth);
    }

    /// <summary>Asserts every pixel of <paramref name="actual"/> equals the same pixel of <paramref name="expected"/>.</summary>
    private static void AssertSurfacesEqual(Surface expected, Surface actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.True(expected[x, y] == actual[x, y], $"pixel ({x},{y}) expected {expected[x, y]} actual {actual[x, y]}");
            }
        }
    }

    /// <summary>Proves every lossless real-encoder fixture decodes to exactly the source pixels.</summary>
    [Theory]
    [MemberData(nameof(LosslessFixtures))]
    public void Jpeg2000Codec_Load_RealEncoderLosslessFixture_MatchesSourcePixelsExactly(
        string fixture, string source, int progression, int layers, int levels, int components, int depth)
    {
        // Arrange: the source PNG (independent of the JPEG 2000 test encoder) and the fixture bytes
        var expected = PngCodec.Load(Resolve(source));
        var data = File.ReadAllBytes(Resolve(fixture));

        // Assert (precondition): the fixture really carries the feature its name claims
        var markers = ReadMarkers(data);
        Assert.Equal((progression, layers, levels, components, depth), markers);

        // Act
        using var actual = Jpeg2000Codec.Load(Resolve(fixture));

        // Assert: exact pixel equality with the source
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>Proves the 16-bit and gray fixtures report their native channel layout through Decode.</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RealEncoderFixtures_ReportChannelLayout()
    {
        // Act
        var gray = Jpeg2000Codec.Decode(File.ReadAllBytes(Resolve("gray.jp2")));
        var alpha = Jpeg2000Codec.Decode(File.ReadAllBytes(Resolve("alpha.jp2")));
        var rgb = Jpeg2000Codec.Decode(File.ReadAllBytes(Resolve("depth16.jp2")));

        // Assert
        Assert.Equal(1, gray.ColorChannelCount);
        Assert.Equal(Jpeg2000ColorSpace.Gray, gray.ColorSpace);
        Assert.False(gray.HasAlpha);
        Assert.Equal(3, alpha.ColorChannelCount);
        Assert.True(alpha.HasAlpha);
        Assert.Equal(3, rgb.ColorChannelCount);
        Assert.Equal(Jpeg2000ColorSpace.Srgb, rgb.ColorSpace);
    }

    /// <summary>Proves GetInfo reports the dimensions of a real-encoder raw codestream and JP2 file.</summary>
    [Fact]
    public void Jpeg2000Codec_GetInfo_RealEncoderFixtures_ReportDimensions()
    {
        // Act
        var raw = Jpeg2000Codec.GetInfo(Resolve("lossless_raw.j2k"));
        var jp2 = Jpeg2000Codec.GetInfo(Resolve("alpha.jp2"));

        // Assert
        Assert.Equal(100, raw.Width);
        Assert.Equal(70, raw.Height);
        Assert.Equal(100, jp2.Width);
        Assert.Equal(70, jp2.Height);
    }

    /// <summary>Proves the lossy truncated real-encoder fixture decodes close to the source (PSNR at least 30 dB).</summary>
    [Fact]
    public void Jpeg2000Codec_Load_RealEncoderLossyFixture_HasAcceptablePsnr()
    {
        // Arrange
        var expected = PngCodec.Load(Resolve("source_rgb.png"));

        // Act
        using var actual = Jpeg2000Codec.Load(Resolve("lossy_q30.jp2"));

        // Assert: PSNR over the color channels
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        double squaredError = 0;
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var e = expected[x, y];
                var a = actual[x, y];
                squaredError += Math.Pow(e.R - a.R, 2) + Math.Pow(e.G - a.G, 2) + Math.Pow(e.B - a.B, 2);
            }
        }

        var mse = squaredError / (expected.Width * expected.Height * 3.0);
        var psnr = 10 * Math.Log10(255.0 * 255.0 / mse);
        Assert.True(psnr >= 30, $"PSNR {psnr:F1} dB below 30 dB");
    }

    /// <summary>Proves every truncation of a real-encoder fixture fails closed (never returns pixels).</summary>
    [Fact]
    public void Jpeg2000Codec_Decode_RealEncoderFixtureTruncated_FailsClosed()
    {
        // Arrange: a small real-encoder codestream
        var data = File.ReadAllBytes(Resolve("lossy_q30.jp2"));

        // Act / Assert: each prefix either throws a documented exception or is rejected
        for (var length = 0; length < data.Length; length += 7)
        {
            var truncated = data[..length];
            var ex = Record.Exception(() => Jpeg2000Codec.Decode(truncated));
            Assert.True(
                ex is InvalidDataException or UnsupportedImageFeatureException,
                $"length {length}: {ex?.GetType().Name ?? "no exception"}");
        }
    }
}
