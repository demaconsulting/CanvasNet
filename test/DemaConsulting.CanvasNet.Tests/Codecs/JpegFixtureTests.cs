using CanvasNet.Canvas;
using CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests that exercise <see cref="JpegCodec"/> against the real-world JPEG fixture
///     corpus in <c>JpegFixtures</c> (see <c>JpegFixtures\README.md</c>), generated with
///     ImageMagick from the PngSuite <c>basn2c08.png</c> source image rather than hand-built JPEG
///     byte streams.
/// </summary>
/// <remarks>
///     Since JPEG is lossy, the color fixtures are compared against the source PNG using
///     per-channel similarity tolerances derived from empirical measurements on these exact
///     fixtures, while the grayscale fixture is only checked for successful loading, correct
///     dimensions, and exact R == G == B expansion.
/// </remarks>
public class JpegFixtureTests
{
    /// <summary>
    ///     The directory containing the JPEG fixture corpus, copied to the test output directory
    ///     by the test project's <c>JpegFixtures\**</c> content item.
    /// </summary>
    private static string AssetsPath => Path.Combine(AppContext.BaseDirectory, "JpegFixtures");

    /// <summary>
    ///     The directory containing the PngSuite corpus, copied to the test output directory by
    ///     the test project's <c>PngSuite\**</c> content item.
    /// </summary>
    private static string PngSuitePath => Path.Combine(AppContext.BaseDirectory, "PngSuite");

    /// <summary>
    ///     Resolves a fixture file within <paramref name="baseDirectory"/>, stripping any
    ///     directory component from <paramref name="fileName"/> first. The file names always
    ///     originate from this class's own <c>TheoryData</c> literals rather than external input,
    ///     so this is a static-analysis hardening (guards against path traversal via
    ///     <see cref="Path.Combine(string, string)"/>) rather than a behavior change.
    /// </summary>
    private static string ResolveFixturePath(string baseDirectory, string fileName) =>
        Path.Combine(baseDirectory, Path.GetFileName(fileName));

    /// <summary>
    ///     Every JPEG fixture file name, for tests that only need to prove successful loading with
    ///     the expected dimensions.
    /// </summary>
    public static readonly TheoryData<string> AllFixtureFiles =
    [
        "baseline_420.jpg",
        "baseline_422.jpg",
        "baseline_444.jpg",
        "progressive_420.jpg",
        "grayscale_baseline.jpg"
    ];

    /// <summary>
    ///     The lossy color JPEG fixtures paired with the per-channel similarity tolerance measured
    ///     against <c>basn2c08.png</c>: max delta 25 for the 4:2:0 baseline/progressive files and
    ///     max delta 6 for the 4:2:2 and 4:4:4 baseline files.
    /// </summary>
    public static readonly TheoryData<string, int> ColorFixtureFiles =
    [
        ("baseline_420.jpg", 30),
        ("baseline_422.jpg", 15),
        ("baseline_444.jpg", 15),
        ("progressive_420.jpg", 30)
    ];

    /// <summary>
    ///     The grayscale fixture files, which are only checked for successful loading, correct
    ///     dimensions, and R == G == B.
    /// </summary>
    public static readonly TheoryData<string> GrayscaleFixtureFiles =
    [
        "grayscale_baseline.jpg"
    ];

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
    ///     Confirms the expected pixel dimensions of the PngSuite source image the JPEG fixtures
    ///     were generated from, so the other tests in this class do not rely on an unverified
    ///     assumption about its size.
    /// </summary>
    [Fact]
    public void JpegFixtures_SourcePngDimensions_Are32x32()
    {
        var source = PngCodec.Load(Path.Combine(PngSuitePath, "basn2c08.png"));

        Assert.Equal(32, source.Width);
        Assert.Equal(32, source.Height);
    }

    /// <summary>
    ///     Proves that Load successfully decodes every JPEG fixture file, producing a surface of
    ///     the expected 32x32 dimensions.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFixtureFiles))]
    public void JpegCodec_Load_Fixture_ReturnsCanvasWithExpectedDimensions(string fileName)
    {
        var surface = JpegCodec.Load(ResolveFixturePath(AssetsPath, fileName));

        Assert.Equal(32, surface.Width);
        Assert.Equal(32, surface.Height);
    }

    /// <summary>
    ///     Proves that every color JPEG fixture decodes to pixels sufficiently close to its
    ///     <c>basn2c08.png</c> source, using the empirically measured tolerance for that fixture
    ///     family plus modest safety margin.
    /// </summary>
    [Theory]
    [MemberData(nameof(ColorFixtureFiles))]
    public void JpegCodec_Load_ColorFixture_MatchesSourcePngWithinTolerance(string fileName, int tolerancePerChannel)
    {
        var expected = PngCodec.Load(Path.Combine(PngSuitePath, "basn2c08.png"));
        var actual = JpegCodec.Load(ResolveFixturePath(AssetsPath, fileName));

        AssertPixelsApproximatelyEqual(expected, actual, tolerancePerChannel);
    }

    /// <summary>
    ///     Proves that every grayscale JPEG fixture loads successfully with the expected
    ///     dimensions and R == G == B for every pixel.
    /// </summary>
    [Theory]
    [MemberData(nameof(GrayscaleFixtureFiles))]
    public void JpegCodec_Load_GrayscaleFixture_HasEqualRgbChannels(string fileName)
    {
        var surface = JpegCodec.Load(ResolveFixturePath(AssetsPath, fileName));

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
