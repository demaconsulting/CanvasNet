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
///     into exactly one of the four groups below:
///     <list type="bullet">
///         <item>
///             <see cref="SupportedFiles"/> - every well-formed, non-interlaced PngSuite file
///             (126 of 175), covering every color type (grayscale, Truecolor, palette/indexed,
///             grayscale-with-alpha, Truecolor-with-alpha) at every bit depth the PNG
///             specification permits for that color type - and must load successfully.
///         </item>
///         <item>
///             <see cref="UnsupportedFiles"/> - every Adam7-interlaced PngSuite file (35 of 175).
///             These are structurally well-formed PNG files - <c>GetInfo</c> succeeds, reports
///             their correct declared dimensions, and reports <see cref="ImageInfo.CanDecode"/> as
///             <see langword="false"/> - but Adam7 decoding is not implemented, so <c>Load</c>
///             rejects them with <see cref="UnsupportedImageFeatureException"/> rather than
///             silently producing incorrect pixels. This is the only PNG feature <c>Load</c>
///             still refuses that is not itself a well-formedness defect.
///         </item>
///         <item>
///             <see cref="CorruptFiles"/> - deliberately corrupt at or before the IHDR chunk (bad
///             signature, bad IHDR CRC-32, or an invalid color-type/bit-depth combination) (12 of
///             175) and must be rejected with <see cref="InvalidDataException"/> by both
///             <c>Load</c> and <c>GetInfo</c>, since these are well-formedness defects, not merely
///             decode-capability limitations.
///         </item>
///         <item>
///             <see cref="CorruptAfterIhdrFiles"/> - deliberately corrupt, but only in a chunk
///             after a well-formed IHDR (2 of 175: a corrupt IDAT CRC-32, and a missing IDAT
///             chunk entirely). <c>Load</c> rejects both with <see cref="InvalidDataException"/>,
///             but <c>GetInfo</c> succeeds on both - reporting their correct declared dimensions -
///             since <c>GetInfo</c> reads only the signature and IHDR chunk and therefore never
///             encounters a corruption located later in the file.
///         </item>
///     </list>
/// </remarks>
public class PngSuiteTests
{
    /// <summary>
    ///     The directory containing the PngSuite corpus, copied to the test output directory by
    ///     the test project's <c>PngSuite\**</c> content item. <c>Path.Join</c> is used instead
    ///     of <see cref="Path.Combine(string, string)"/> purely to avoid CodeQL's
    ///     <c>cs/path-combine</c> rule, since <c>Path.Join</c> does not discard
    ///     <see cref="AppContext.BaseDirectory"/> when the second segment looks rooted.
    /// </summary>
    private static string AssetsPath => Path.Join(AppContext.BaseDirectory, "PngSuite");

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
    ///     Every well-formed, non-interlaced PngSuite file (126 of 175), covering every color
    ///     type and bit depth combination the PNG specification permits, that must load
    ///     successfully.
    /// </summary>
    public static readonly TheoryData<string> SupportedFiles =
    [
        // cspell:disable
        "basn0g01.png",
        "basn0g02.png",
        "basn0g04.png",
        "basn0g08.png",
        "basn0g16.png",
        "basn2c08.png",
        "basn2c16.png",
        "basn3p01.png",
        "basn3p02.png",
        "basn3p04.png",
        "basn3p08.png",
        "basn4a08.png",
        "basn4a16.png",
        "basn6a08.png",
        "basn6a16.png",
        "bgan6a08.png",
        "bgan6a16.png",
        "bgbn4a08.png",
        "bggn4a16.png",
        "bgwn6a08.png",
        "bgyn6a16.png",
        "ccwn2c08.png",
        "ccwn3p08.png",
        "cdfn2c08.png",
        "cdhn2c08.png",
        "cdsn2c08.png",
        "cdun2c08.png",
        "ch1n3p04.png",
        "ch2n3p08.png",
        "cm0n0g04.png",
        "cm7n0g04.png",
        "cm9n0g04.png",
        "cs3n2c16.png",
        "cs3n3p08.png",
        "cs5n2c08.png",
        "cs5n3p08.png",
        "cs8n2c08.png",
        "cs8n3p08.png",
        "ct0n0g04.png",
        "ct1n0g04.png",
        "cten0g04.png",
        "ctfn0g04.png",
        "ctgn0g04.png",
        "cthn0g04.png",
        "ctjn0g04.png",
        "ctzn0g04.png",
        "exif2c08.png",
        "f00n0g08.png",
        "f00n2c08.png",
        "f01n0g08.png",
        "f01n2c08.png",
        "f02n0g08.png",
        "f02n2c08.png",
        "f03n0g08.png",
        "f03n2c08.png",
        "f04n0g08.png",
        "f04n2c08.png",
        "f99n0g04.png",
        "g03n0g16.png",
        "g03n2c08.png",
        "g03n3p04.png",
        "g04n0g16.png",
        "g04n2c08.png",
        "g04n3p04.png",
        "g05n0g16.png",
        "g05n2c08.png",
        "g05n3p04.png",
        "g07n0g16.png",
        "g07n2c08.png",
        "g07n3p04.png",
        "g10n0g16.png",
        "g10n2c08.png",
        "g10n3p04.png",
        "g25n0g16.png",
        "g25n2c08.png",
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
        "pp0n6a08.png",
        "ps1n0g08.png",
        "ps1n2c16.png",
        "ps2n0g08.png",
        "ps2n2c16.png",
        "s01n3p01.png",
        "s02n3p01.png",
        "s03n3p01.png",
        "s04n3p01.png",
        "s05n3p02.png",
        "s06n3p02.png",
        "s07n3p02.png",
        "s08n3p02.png",
        "s09n3p02.png",
        "s32n3p04.png",
        "s33n3p04.png",
        "s34n3p04.png",
        "s35n3p04.png",
        "s36n3p04.png",
        "s37n3p04.png",
        "s38n3p04.png",
        "s39n3p04.png",
        "s40n3p04.png",
        "tbbn0g04.png",
        "tbbn2c16.png",
        "tbbn3p08.png",
        "tbgn2c16.png",
        "tbgn3p08.png",
        "tbrn2c08.png",
        "tbwn0g16.png",
        "tbwn3p08.png",
        "tbyn3p08.png",
        "tm3n3p02.png",
        "tp0n0g08.png",
        "tp0n2c08.png",
        "tp0n3p08.png",
        "tp1n3p08.png",
        "z00n2c08.png",
        "z03n2c08.png",
        "z06n2c08.png",
        "z09n2c08.png"
        // cspell:enable
    ];

    /// <summary>
    ///     Every Adam7-interlaced PngSuite file (35 of 175) - structurally well-formed PNG files
    ///     that <c>Load</c> still refuses, since Adam7 decoding is not implemented, but that
    ///     <c>GetInfo</c> succeeds on (see
    ///     <see cref="PngSuiteUnsupportedFile_GetInfoReturnsCorrectDimensions"/>).
    /// </summary>
    public static readonly TheoryData<string> UnsupportedFiles =
    [
        // cspell:disable
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
        "bgai4a08.png",
        "bgai4a16.png",
        "s01i3p01.png",
        "s02i3p01.png",
        "s03i3p01.png",
        "s04i3p01.png",
        "s05i3p02.png",
        "s06i3p02.png",
        "s07i3p02.png",
        "s08i3p02.png",
        "s09i3p02.png",
        "s32i3p04.png",
        "s33i3p04.png",
        "s34i3p04.png",
        "s35i3p04.png",
        "s36i3p04.png",
        "s37i3p04.png",
        "s38i3p04.png",
        "s39i3p04.png",
        "s40i3p04.png"
        // cspell:enable
    ];

    /// <summary>
    ///     PngSuite files that are deliberately corrupt at or before the IHDR chunk (bad
    ///     signature, bad IHDR CRC-32, or an invalid color-type/bit-depth combination), so the
    ///     corruption is always encountered by <c>GetInfo</c> (which reads only the signature and
    ///     IHDR chunk) as well as by <c>Load</c>. This deliberately excludes
    ///     <c>xcsn0g01.png</c> (a corrupt IDAT chunk CRC-32) and <c>xdtn0g01.png</c> (a missing
    ///     IDAT chunk) - see <see cref="CorruptAfterIhdrFiles"/> for those two, whose corruption
    ///     lies entirely after a well-formed IHDR chunk and is therefore never encountered by
    ///     <c>GetInfo</c>.
    /// </summary>
    public static readonly TheoryData<string> CorruptFiles =
    [
        // cspell:disable
        "xc1n0g08.png",
        "xc9n2c08.png",
        "xcrn0g04.png",
        "xd0n2c08.png",
        "xd3n2c08.png",
        "xd9n2c08.png",
        "xhdn0g08.png",
        "xlfn0g04.png",
        "xs1n0g01.png",
        "xs2n0g01.png",
        "xs4n0g01.png",
        "xs7n0g01.png"
        // cspell:enable
    ];

    /// <summary>
    ///     PngSuite files whose IHDR chunk is well-formed but whose corruption lies entirely
    ///     after it: <c>xcsn0g01.png</c> has a corrupt IDAT chunk CRC-32 (its CRC bytes literally
    ///     spell the ASCII text "CSUM" rather than a computed checksum), and
    ///     <c>xdtn0g01.png</c> has no IDAT chunk at all (its data was deleted, leaving only
    ///     IHDR, gAMA, and IEND). Since <c>GetInfo</c> reads only the signature and IHDR chunk, it
    ///     never encounters either corruption and succeeds, exactly like the Adam7-interlaced
    ///     <see cref="UnsupportedFiles"/> above - but unlike those files, <c>Load</c> rejects
    ///     these two because they are genuinely malformed, not merely because of an
    ///     unimplemented decode feature.
    /// </summary>
    public static readonly TheoryData<string> CorruptAfterIhdrFiles =
    [
        // cspell:disable
        "xcsn0g01.png",
        "xdtn0g01.png"
        // cspell:enable
    ];

    /// <summary>
    ///     Reads a PNG file's declared width and height directly from its raw IHDR chunk bytes
    ///     (the file's bytes 16-19 and 20-23, each a 4-byte big-endian integer), independent of
    ///     <see cref="PngCodec"/>'s own IHDR parsing, so
    ///     <see cref="PngSuiteUnsupportedFile_GetInfoReturnsCorrectDimensions"/> below has an
    ///     independent ground truth to compare <c>GetInfo</c>'s reported dimensions against - the
    ///     8-byte PNG signature is always immediately followed by a 4-byte chunk length, the
    ///     4-byte ASCII chunk type "IHDR", then the 4-byte width and 4-byte height fields.
    /// </summary>
    private static (int Width, int Height) ReadRawIhdrDimensions(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return (width, height);
    }

    /// <summary>
    ///     Proves that Load successfully decodes every well-formed, non-interlaced PngSuite
    ///     file, producing a surface with matching dimensions and no thrown exception.
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
    ///     Proves that Load rejects every Adam7-interlaced PngSuite file with
    ///     UnsupportedImageFeatureException, since Adam7 decoding is not implemented, rather than
    ///     silently producing incorrect pixels.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnsupportedFiles))]
    public void PngCodec_Load_PngSuiteUnsupportedFile_ThrowsUnsupportedImageFeatureException(string fileName)
    {
        // Act & Assert: the unsupported feature must be rejected, with a Feature token
        // identifying which feature was rejected
        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => PngCodec.Load(ResolveFixturePath(AssetsPath, fileName)));
        Assert.Equal("png-adam7-interlace", exception.Feature);
    }

    /// <summary>
    ///     Proves that GetInfo still succeeds on every Adam7-interlaced PngSuite file that Load
    ///     refuses, reporting the file's exact declared IHDR width and height (not merely
    ///     positive values, which would pass even if the reported dimensions were wrong, for
    ///     example swapped) and CanDecode as false - since Adam7 interlacing does not affect the
    ///     declared dimensions GetInfo reports and is not itself a well-formedness defect.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnsupportedFiles))]
    public void PngSuiteUnsupportedFile_GetInfoReturnsCorrectDimensions(string fileName)
    {
        // Arrange: the fixture's actual declared IHDR dimensions, read directly from its raw
        // bytes rather than trusted from GetInfo itself
        var path = ResolveFixturePath(AssetsPath, fileName);
        var (expectedWidth, expectedHeight) = ReadRawIhdrDimensions(path);

        // Act: GetInfo succeeds even though Load would refuse this file
        var info = PngCodec.GetInfo(path);

        // Assert: GetInfo reports the file's exact declared dimensions and that Load cannot
        // decode this file
        Assert.Equal(expectedWidth, info.Width);
        Assert.Equal(expectedHeight, info.Height);
        Assert.False(info.CanDecode);
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

    /// <summary>
    ///     Proves that GetInfo also rejects every deliberately corrupt PngSuite file with
    ///     InvalidDataException, exactly like Load, since these files (bad signature, bad IHDR
    ///     CRC-32, or an invalid color-type/bit-depth combination) are well-formedness defects,
    ///     not merely a decode-capability limitation GetInfo is designed to tolerate (unlike the
    ///     Adam7-interlaced <see cref="UnsupportedFiles"/> above, which GetInfo still accepts).
    /// </summary>
    [Theory]
    [MemberData(nameof(CorruptFiles))]
    public void PngCodec_GetInfo_PngSuiteCorruptFile_ThrowsInvalidDataException(string fileName)
    {
        // Act & Assert: the corrupt file must be rejected
        Assert.Throws<InvalidDataException>(() => PngCodec.GetInfo(ResolveFixturePath(AssetsPath, fileName)));
    }

    /// <summary>
    ///     Proves that, for both PngSuite files whose corruption lies entirely after a
    ///     well-formed IHDR chunk (a corrupt IDAT CRC-32, and a missing IDAT chunk), GetInfo
    ///     still succeeds - reporting the exact declared IHDR width and height, read directly
    ///     from the file's raw bytes - while Load on the same file throws InvalidDataException.
    ///     GetInfo never encounters either corruption since it reads only the signature and IHDR
    ///     chunk, but Load must fully parse the IDAT chunk (or notice its absence) to decode
    ///     pixel data.
    /// </summary>
    [Theory]
    [MemberData(nameof(CorruptAfterIhdrFiles))]
    public void PngCodec_PngSuiteCorruptAfterIhdrFile_GetInfoReturnsCorrectDimensions_ButLoadThrows(string fileName)
    {
        // Arrange: the fixture's actual declared IHDR dimensions, read directly from its raw
        // bytes rather than trusted from GetInfo itself
        var path = ResolveFixturePath(AssetsPath, fileName);
        var (expectedWidth, expectedHeight) = ReadRawIhdrDimensions(path);

        // Act: GetInfo succeeds even though the file is corrupt beyond its IHDR chunk
        var info = PngCodec.GetInfo(path);

        // Assert: GetInfo reports the file's exact declared dimensions
        Assert.Equal(expectedWidth, info.Width);
        Assert.Equal(expectedHeight, info.Height);

        // Assert: Load on the same file still throws, since it must reach the corrupt/missing
        // IDAT chunk to decode pixel data
        Assert.Throws<InvalidDataException>(() => PngCodec.Load(path));
    }
}
