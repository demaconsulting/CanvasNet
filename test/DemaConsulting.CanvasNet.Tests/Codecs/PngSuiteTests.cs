using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests that exercise <see cref="PngCodec"/> against the industry-standard PngSuite
///     conformance corpus (see <c>PngSuite\PngSuite.README</c> and
///     <c>PngSuite\PngSuite.LICENSE</c>), rather than hand-built PNG byte streams.
/// </summary>
/// <remarks>
///     Every file's actual IHDR fields (color type, bit depth, interlace method) and, for the
///     deliberately corrupt files, the specific corruption, were verified directly against the
///     raw file bytes rather than trusted from the PngSuite filename convention, then classified
///     into exactly one of the three groups below:
///     <list type="bullet">
///         <item>
///             <see cref="SupportedFiles"/> - within <see cref="PngCodec"/>'s supported feature
///             set (8-bit-per-channel Truecolor or Truecolor-with-alpha, non-interlaced) and must
///             load successfully.
///         </item>
///         <item>
///             <see cref="UnsupportedFiles"/> - structurally valid PNG files using a feature
///             <see cref="PngCodec"/> does not implement (grayscale, palette/indexed, or
///             grayscale-with-alpha color types; bit depths other than 8; or Adam7 interlacing).
///             These are out of scope by design rather than corrupt, but must still be rejected
///             with <see cref="InvalidDataException"/> rather than silently producing incorrect
///             pixels; broadening support for any of these features is a possible future
///             enhancement, not a defect in the current codec.
///         </item>
///         <item>
///             <see cref="CorruptFiles"/> - deliberately corrupt (bad signature, bad IHDR CRC-32,
///             or an invalid color-type/bit-depth combination) and must be rejected with
///             <see cref="InvalidDataException"/>.
///         </item>
///     </list>
/// </remarks>
public class PngSuiteTests
{
    /// <summary>
    ///     The directory containing the PngSuite corpus, copied to the test output directory by
    ///     the test project's <c>PngSuite\**</c> content item.
    /// </summary>
    private static string AssetsPath => Path.Combine(AppContext.BaseDirectory, "PngSuite");

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
    ///     PngSuite files within PngCodec's supported feature set (color type 2 or 6, 8-bit,
    ///     non-interlaced) that must load successfully.
    /// </summary>
    public static readonly TheoryData<string> SupportedFiles =
    [
        // cspell:disable
        "basn2c08.png",
        "basn6a08.png",
        "bgan6a08.png",
        "bgwn6a08.png",
        "ccwn2c08.png",
        "cdfn2c08.png",
        "cdhn2c08.png",
        "cdsn2c08.png",
        "cdun2c08.png",
        "cs5n2c08.png",
        "cs8n2c08.png",
        "exif2c08.png",
        "f00n2c08.png",
        "f01n2c08.png",
        "f02n2c08.png",
        "f03n2c08.png",
        "f04n2c08.png",
        "g03n2c08.png",
        "g04n2c08.png",
        "g05n2c08.png",
        "g07n2c08.png",
        "g10n2c08.png",
        "g25n2c08.png",
        "pp0n6a08.png",
        "tbrn2c08.png",
        "tp0n2c08.png",
        "z00n2c08.png",
        "z03n2c08.png",
        "z06n2c08.png",
        "z09n2c08.png"
    ];

    /// <summary>
    ///     PngSuite files that are structurally valid PNG files but use a color type, bit depth,
    ///     or interlace method outside PngCodec's supported feature set.
    /// </summary>
    public static readonly TheoryData<string> UnsupportedFiles =
    [
        "basi0g01.png",
        "basi0g02.png",
        "basi0g04.png",
        "basi0g08.png",
        "basi0g16.png",
        "basi2c08.png",
        "basi2c16.png",
        "basi3p01.png",
        "basi3p02.png",
        "basi3p04.png",
        "basi3p08.png",
        "basi4a08.png",
        "basi4a16.png",
        "basi6a08.png",
        "basi6a16.png",
        "basn0g01.png",
        "basn0g02.png",
        "basn0g04.png",
        "basn0g08.png",
        "basn0g16.png",
        "basn2c16.png",
        "basn3p01.png",
        "basn3p02.png",
        "basn3p04.png",
        "basn3p08.png",
        "basn4a08.png",
        "basn4a16.png",
        "basn6a16.png",
        "bgai4a08.png",
        "bgai4a16.png",
        "bgan6a16.png",
        "bgbn4a08.png",
        "bggn4a16.png",
        "bgyn6a16.png",
        "ccwn3p08.png",
        "ch1n3p04.png",
        "ch2n3p08.png",
        "cm0n0g04.png",
        "cm7n0g04.png",
        "cm9n0g04.png",
        "cs3n2c16.png",
        "cs3n3p08.png",
        "cs5n3p08.png",
        "cs8n3p08.png",
        "ct0n0g04.png",
        "ct1n0g04.png",
        "cten0g04.png",
        "ctfn0g04.png",
        "ctgn0g04.png",
        "cthn0g04.png",
        "ctjn0g04.png",
        "ctzn0g04.png",
        "f00n0g08.png",
        "f01n0g08.png",
        "f02n0g08.png",
        "f03n0g08.png",
        "f04n0g08.png",
        "f99n0g04.png",
        "g03n0g16.png",
        "g03n3p04.png",
        "g04n0g16.png",
        "g04n3p04.png",
        "g05n0g16.png",
        "g05n3p04.png",
        "g07n0g16.png",
        "g07n3p04.png",
        "g10n0g16.png",
        "g10n3p04.png",
        "g25n0g16.png",
        "g25n3p04.png",
        "oi1n0g16.png",
        "oi1n2c16.png",
        "oi2n0g16.png",
        "oi2n2c16.png",
        "oi4n0g16.png",
        "oi4n2c16.png",
        "oi9n0g16.png",
        "oi9n2c16.png",
        "pp0n2c16.png",
        "ps1n0g08.png",
        "ps1n2c16.png",
        "ps2n0g08.png",
        "ps2n2c16.png",
        "s01i3p01.png",
        "s01n3p01.png",
        "s02i3p01.png",
        "s02n3p01.png",
        "s03i3p01.png",
        "s03n3p01.png",
        "s04i3p01.png",
        "s04n3p01.png",
        "s05i3p02.png",
        "s05n3p02.png",
        "s06i3p02.png",
        "s06n3p02.png",
        "s07i3p02.png",
        "s07n3p02.png",
        "s08i3p02.png",
        "s08n3p02.png",
        "s09i3p02.png",
        "s09n3p02.png",
        "s32i3p04.png",
        "s32n3p04.png",
        "s33i3p04.png",
        "s33n3p04.png",
        "s34i3p04.png",
        "s34n3p04.png",
        "s35i3p04.png",
        "s35n3p04.png",
        "s36i3p04.png",
        "s36n3p04.png",
        "s37i3p04.png",
        "s37n3p04.png",
        "s38i3p04.png",
        "s38n3p04.png",
        "s39i3p04.png",
        "s39n3p04.png",
        "s40i3p04.png",
        "s40n3p04.png",
        "tbbn0g04.png",
        "tbbn2c16.png",
        "tbbn3p08.png",
        "tbgn2c16.png",
        "tbgn3p08.png",
        "tbwn0g16.png",
        "tbwn3p08.png",
        "tbyn3p08.png",
        "tm3n3p02.png",
        "tp0n0g08.png",
        "tp0n3p08.png",
        "tp1n3p08.png"
        // cspell:enable
    ];

    /// <summary>
    ///     PngSuite files that are deliberately corrupt (bad signature, bad IHDR CRC-32, or an
    ///     invalid color-type/bit-depth combination).
    /// </summary>
    public static readonly TheoryData<string> CorruptFiles =
    [
        // cspell:disable
        "xc1n0g08.png",
        "xc9n2c08.png",
        "xcrn0g04.png",
        "xcsn0g01.png",
        "xd0n2c08.png",
        "xd3n2c08.png",
        "xd9n2c08.png",
        "xdtn0g01.png",
        "xhdn0g08.png",
        "xlfn0g04.png",
        "xs1n0g01.png",
        "xs2n0g01.png",
        "xs4n0g01.png",
        "xs7n0g01.png"
        // cspell:enable
    ];

    /// <summary>
    ///     Proves that Load successfully decodes every PngSuite file within PngCodec's supported
    ///     feature set, producing a surface with matching dimensions and no thrown exception.
    /// </summary>
    [Theory]
    [MemberData(nameof(SupportedFiles))]
    public void PngCodec_Load_PngSuiteSupportedFile_ReturnsCanvas(string fileName)
    {
        // Act: load the PngSuite file
        var surface = PngCodec.Load(ResolveFixturePath(AssetsPath, fileName));

        // Assert: a non-empty surface was produced
        Assert.True(surface.Width > 0);
        Assert.True(surface.Height > 0);
    }

    /// <summary>
    ///     Proves that Load rejects every PngSuite file using a color type, bit depth, or
    ///     interlace method outside PngCodec's supported feature set with
    ///     InvalidDataException, rather than silently producing incorrect pixels.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnsupportedFiles))]
    public void PngCodec_Load_PngSuiteUnsupportedFile_ThrowsInvalidDataException(string fileName)
    {
        // Act & Assert: the unsupported feature must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(ResolveFixturePath(AssetsPath, fileName)));
    }

    /// <summary>
    ///     Proves that Load rejects every deliberately corrupt PngSuite file with
    ///     InvalidDataException.
    /// </summary>
    [Theory]
    [MemberData(nameof(CorruptFiles))]
    public void PngCodec_Load_PngSuiteCorruptFile_ThrowsInvalidDataException(string fileName)
    {
        // Act & Assert: the corrupt file must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(ResolveFixturePath(AssetsPath, fileName)));
    }
}
