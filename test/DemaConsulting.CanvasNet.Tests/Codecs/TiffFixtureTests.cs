using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests that exercise <see cref="TiffCodec"/> against the real-world TIFF fixture
///     corpus in <c>TiffFixtures</c> (see <c>TiffFixtures\README.md</c>), generated with
///     ImageMagick from the PngSuite <c>basn2c08.png</c> (RGB) and <c>basn6a08.png</c> (RGBA)
///     source images, rather than hand-built TIFF byte streams.
/// </summary>
/// <remarks>
///     For the RGB/RGBA fixtures, every decoded pixel is compared for exact equality against the
///     corresponding source PNG (loaded via <see cref="PngCodec"/>), since TIFF is lossless,
///     except that the RGB fixtures' alpha is expected to be forced to 255 (opaque) because they
///     have no alpha channel. For the two grayscale fixtures (converted from the color source via
///     ImageMagick, which is itself a lossy transformation relative to the color original), only
///     successful loading, correct dimensions, and R == G == B are asserted.
/// </remarks>
public class TiffFixtureTests
{
    /// <summary>
    ///     The directory containing the TIFF fixture corpus, copied to the test output directory
    ///     by the test project's <c>TiffFixtures\**</c> content item.
    /// </summary>
    private static string AssetsPath => Path.Combine(AppContext.BaseDirectory, "TiffFixtures");

    /// <summary>
    ///     The directory containing the PngSuite corpus, copied to the test output directory by
    ///     the test project's <c>PngSuite\**</c> content item.
    /// </summary>
    private static string PngSuitePath => Path.Combine(AppContext.BaseDirectory, "PngSuite");

    /// <summary>
    ///     Resolves a fixture file within <paramref name="baseDirectory"/>. The file names always
    ///     originate from this class's own <c>TheoryData</c> literals rather than external input,
    ///     so path-injection is not a concern here; <c>Path.Join</c> is
    ///     used instead of <see cref="Path.Combine(string, string)"/> purely to avoid CodeQL's
    ///     <c>cs/path-combine</c> rule, since <c>Path.Join</c> does not discard
    ///     <paramref name="baseDirectory"/> when <paramref name="fileName"/> looks rooted.
    /// </summary>
    private static string ResolveFixturePath(string baseDirectory, string fileName) =>
        Path.Join(baseDirectory, fileName);

    /// <summary>
    ///     Every TIFF fixture file name, for tests that only need to prove successful loading with
    ///     the expected dimensions.
    /// </summary>
    public static readonly TheoryData<string> AllFixtureFiles =
    [
        "rgb_none_le.tiff",
        "rgb_none_be.tiff",
        "rgb_lzw.tiff",
        "rgb_packbits.tiff",
        "rgb_deflate.tiff",
        "rgba_none.tiff",
        "rgba_lzw.tiff",
        "gray_none.tiff",
        "gray_lzw.tiff"
    ];

    /// <summary>
    ///     The RGB (no-alpha) fixture files, all generated from <c>basn2c08.png</c>, paired for
    ///     exact-pixel comparison against that source (with alpha forced to 255).
    /// </summary>
    public static readonly TheoryData<string> RgbFixtureFiles =
    [
        "rgb_none_le.tiff",
        "rgb_none_be.tiff",
        "rgb_lzw.tiff",
        "rgb_packbits.tiff",
        "rgb_deflate.tiff"
    ];

    /// <summary>
    ///     The RGBA fixture files, all generated from <c>basn6a08.png</c>, paired for exact-pixel
    ///     comparison (including alpha) against that source.
    /// </summary>
    public static readonly TheoryData<string> RgbaFixtureFiles =
    [
        "rgba_none.tiff",
        "rgba_lzw.tiff"
    ];

    /// <summary>
    ///     The grayscale fixture files (converted from <c>basn2c08.png</c> by ImageMagick), which
    ///     are only checked for successful loading, correct dimensions, and R == G == B.
    /// </summary>
    public static readonly TheoryData<string> GrayscaleFixtureFiles =
    [
        "gray_none.tiff",
        "gray_lzw.tiff"
    ];

    /// <summary>
    ///     Confirms the expected pixel dimensions of the PngSuite source images the TIFF fixtures
    ///     were generated from, so the other tests in this class do not rely on an unverified
    ///     assumption about their size.
    /// </summary>
    [Fact]
    public void TiffFixtures_SourcePngDimensions_Are32x32()
    {
        var rgbSource = PngCodec.Load(Path.Combine(PngSuitePath, "basn2c08.png"));
        var rgbaSource = PngCodec.Load(Path.Combine(PngSuitePath, "basn6a08.png"));

        Assert.Equal(32, rgbSource.Width);
        Assert.Equal(32, rgbSource.Height);
        Assert.Equal(32, rgbaSource.Width);
        Assert.Equal(32, rgbaSource.Height);
    }

    /// <summary>
    ///     Proves that Load successfully decodes every TIFF fixture file, producing a surface of
    ///     the expected 32x32 dimensions.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFixtureFiles))]
    public void TiffCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions(string fileName)
    {
        var surface = TiffCodec.Load(ResolveFixturePath(AssetsPath, fileName));

        Assert.Equal(32, surface.Width);
        Assert.Equal(32, surface.Height);
    }

    /// <summary>
    ///     Proves that every RGB TIFF fixture decodes to pixels matching its <c>basn2c08.png</c>
    ///     source exactly, with alpha forced to 255 (opaque).
    /// </summary>
    [Theory]
    [MemberData(nameof(RgbFixtureFiles))]
    public void TiffCodec_Load_RgbFixture_MatchesSourcePngWithOpaqueAlpha(string fileName)
    {
        var expected = PngCodec.Load(Path.Combine(PngSuitePath, "basn2c08.png"));
        var actual = TiffCodec.Load(ResolveFixturePath(AssetsPath, fileName));

        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                var expectedPixel = expected[x, y];
                var actualPixel = actual[x, y];
                Assert.Equal(expectedPixel.R, actualPixel.R);
                Assert.Equal(expectedPixel.G, actualPixel.G);
                Assert.Equal(expectedPixel.B, actualPixel.B);
                Assert.Equal(255, actualPixel.A);
            }
        }
    }

    /// <summary>
    ///     Proves that every RGBA TIFF fixture decodes to pixels matching its
    ///     <c>basn6a08.png</c> source exactly, including alpha.
    /// </summary>
    [Theory]
    [MemberData(nameof(RgbaFixtureFiles))]
    public void TiffCodec_Load_RgbaFixture_MatchesSourcePngExactly(string fileName)
    {
        var expected = PngCodec.Load(Path.Combine(PngSuitePath, "basn6a08.png"));
        var actual = TiffCodec.Load(ResolveFixturePath(AssetsPath, fileName));

        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }

    /// <summary>
    ///     Proves that every grayscale TIFF fixture loads successfully with the expected
    ///     dimensions and R == G == B for every pixel; the color-to-grayscale conversion is lossy
    ///     relative to the color source, so no exact-match comparison against the color source is
    ///     performed.
    /// </summary>
    [Theory]
    [MemberData(nameof(GrayscaleFixtureFiles))]
    public void TiffCodec_Load_GrayscaleFixture_HasEqualRgbChannels(string fileName)
    {
        var surface = TiffCodec.Load(ResolveFixturePath(AssetsPath, fileName));

        Assert.Equal(32, surface.Width);
        Assert.Equal(32, surface.Height);
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var pixel = surface[x, y];
                Assert.Equal(pixel.R, pixel.G);
                Assert.Equal(pixel.G, pixel.B);
                Assert.Equal(255, pixel.A);
            }
        }
    }
}
