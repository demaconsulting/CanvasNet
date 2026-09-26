using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests that exercise <see cref="GifCodec"/> against the real-world GIF fixture corpus
///     in <c>GifFixtures</c> (see <c>GifFixtures\README.md</c>), downloaded from samplelib.com,
///     rather than hand-built GIF byte streams.
/// </summary>
/// <remarks>
///     For the solid-color fixtures, each fixture's actual Global Color Table entries are read
///     directly from the file's own bytes in test setup code (rather than assumed to be pure
///     primaries such as exactly (255, 0, 0)), since real-world GIF encoders commonly quantize
///     colors slightly. For the animated fixtures, only successful first-frame-only loading, the
///     Logical Screen Descriptor dimensions, and <see cref="GifCodec.GetInfo(string)"/> parity are
///     asserted; the codec is decode-only and intentionally decodes only the first frame.
/// </remarks>
public class GifFixtureTests
{
    /// <summary>
    ///     The directory containing the GIF fixture corpus, copied to the test output directory
    ///     by the test project's <c>GifFixtures\**</c> content item.
    /// </summary>
    private static string AssetsPath => Path.Combine(AppContext.BaseDirectory, "GifFixtures");

    /// <summary>
    ///     Resolves a fixture file within <see cref="AssetsPath"/>. The file names always
    ///     originate from this class's own <c>TheoryData</c> literals rather than external input,
    ///     so path-injection is not a concern here; <c>Path.Join</c> is used instead of
    ///     <see cref="Path.Combine(string, string)"/> purely to avoid CodeQL's
    ///     <c>cs/path-combine</c> rule, since <c>Path.Join</c> does not discard the base directory
    ///     when <paramref name="fileName"/> looks rooted.
    /// </summary>
    private static string ResolveFixturePath(string fileName) =>
        Path.Join(AssetsPath, fileName);

    /// <summary>
    ///     The solid-color fixture files, each paired with their Logical Screen Descriptor
    ///     dimensions, for tests that sample several pixels and compare against the file's own
    ///     Global Color Table.
    /// </summary>
    public static readonly TheoryData<string, int, int> SolidColorFixtureFiles = new()
    {
        { "sample-red-400x300.gif", 400, 300 },
        { "sample-red-200x200.gif", 200, 200 },
        { "sample-green-400x300.gif", 400, 300 },
        { "sample-green-200x200.gif", 200, 200 },
        { "sample-blue-400x300.gif", 400, 300 }
    };

    /// <summary>
    ///     The animated (multi-frame) fixture files, each paired with their Logical Screen
    ///     Descriptor dimensions, for tests that only prove first-frame-only decoding succeeds.
    /// </summary>
    public static readonly TheoryData<string, int, int> AnimatedFixtureFiles = new()
    {
        { "sample-animated-400x300.gif", 400, 300 },
        { "sample-animated-200x200.gif", 200, 200 },
        { "sample-animated-100x75.gif", 100, 75 }
    };

    /// <summary>
    ///     Every fixture file (solid-color and animated combined), for the single GetInfo parity
    ///     test that applies identically to both groups.
    /// </summary>
    public static readonly TheoryData<string, int, int> AllFixtureFiles = new()
    {
        { "sample-red-400x300.gif", 400, 300 },
        { "sample-red-200x200.gif", 200, 200 },
        { "sample-green-400x300.gif", 400, 300 },
        { "sample-green-200x200.gif", 200, 200 },
        { "sample-blue-400x300.gif", 400, 300 },
        { "sample-animated-400x300.gif", 400, 300 },
        { "sample-animated-200x200.gif", 200, 200 },
        { "sample-animated-100x75.gif", 100, 75 }
    };

    /// <summary>
    ///     Reads the Global Color Table entry at <paramref name="index"/> directly from the raw
    ///     bytes of a GIF file, so tests do not rely on an assumed RGB value.
    /// </summary>
    /// <param name="path">Path of the GIF file.</param>
    /// <param name="index">Zero-based color table entry index to read.</param>
    /// <returns>The (R, G, B) tuple stored at that entry.</returns>
    private static (byte R, byte G, byte B) ReadGlobalColorTableEntry(string path, int index)
    {
        var bytes = File.ReadAllBytes(path);

        // Bytes 0-5: "GIF87a"/"GIF89a" signature. Bytes 6-7: canvas width (LE).
        // Bytes 8-9: canvas height (LE). Byte 10: packed fields.
        var packed = bytes[10];
        Assert.True((packed & 0x80) != 0, "Fixture is expected to declare a Global Color Table");

        var offset = 13 + index * 3;
        return (bytes[offset], bytes[offset + 1], bytes[offset + 2]);
    }

    /// <summary>
    ///     Proves that Load decodes each solid-color fixture to a surface of the expected
    ///     dimensions, with every sampled pixel matching the fixture's own Global Color Table
    ///     background-color entry (entry 0), read directly from the file's raw bytes.
    /// </summary>
    [Theory]
    [MemberData(nameof(SolidColorFixtureFiles))]
    public void GifCodec_Load_SolidColorFixture_MatchesOwnGlobalColorTable(
        string fileName,
        int width,
        int height)
    {
        var path = ResolveFixturePath(fileName);
        var (r, g, b) = ReadGlobalColorTableEntry(path, 0);

        using var surface = GifCodec.Load(path);

        Assert.Equal(width, surface.Width);
        Assert.Equal(height, surface.Height);

        var samplePoints = new (int X, int Y)[]
        {
            (0, 0),
            (width - 1, 0),
            (0, height - 1),
            (width - 1, height - 1),
            (width / 2, height / 2)
        };

        foreach (var (x, y) in samplePoints)
        {
            var pixel = surface[x, y];
            Assert.Equal(r, pixel.R);
            Assert.Equal(g, pixel.G);
            Assert.Equal(b, pixel.B);
            Assert.Equal(255, pixel.A);
        }
    }

    /// <summary>
    ///     Proves that GetInfo reports the same dimensions and <c>CanDecode == true</c> for every
    ///     fixture (solid-color and animated) as Load actually produces.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllFixtureFiles))]
    public void GifCodec_GetInfo_Fixture_MatchesLoadDimensionsAndReportsDecodable(
        string fileName,
        int width,
        int height)
    {
        var path = ResolveFixturePath(fileName);

        var info = GifCodec.GetInfo(path);

        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
        Assert.True(info.CanDecode);
    }
    /// <summary>
    ///     Proves that Load successfully decodes an animated (multi-frame) fixture without
    ///     throwing, producing a surface sized to the Logical Screen Descriptor dimensions using
    ///     only the first frame - a multi-frame GIF is not treated as malformed.
    /// </summary>
    [Theory]
    [MemberData(nameof(AnimatedFixtureFiles))]
    public void GifCodec_Load_AnimatedFixture_DecodesFirstFrameWithoutThrowing(
        string fileName,
        int width,
        int height)
    {
        var path = ResolveFixturePath(fileName);

        using var surface = GifCodec.Load(path);

        Assert.Equal(width, surface.Width);
        Assert.Equal(height, surface.Height);
    }
}
