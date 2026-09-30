// cspell:ignore Liberation Dogfoods
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Dogfoods all 12 bundled Liberation Sans/Serif/Mono embedded-resource fonts (see
///     <c>Fonts\BundledFonts\README.md</c> for sourcing/licensing provenance) through
///     <see cref="TrueTypeFont"/>, proving every one is a genuine, well-formed, loadable TrueType
///     font exposing the expected family name and style metadata - both a one-time sanity check
///     performed before committing these files, and a permanent, repeatable CI gate against any
///     future accidental corruption or replacement of the bundled assets.
/// </summary>
public class BundledLiberationFontsTests
{
    /// <summary>The 12 Liberation Sans/Serif/Mono bundled variants and their expected metadata.</summary>
    public static TheoryData<string, string, bool, bool, bool> LiberationVariants => new()
    {
        { "LiberationSans-Regular.ttf", "Liberation Sans", false, false, false },
        { "LiberationSans-Bold.ttf", "Liberation Sans", true, false, false },
        { "LiberationSans-Italic.ttf", "Liberation Sans", false, true, false },
        { "LiberationSans-BoldItalic.ttf", "Liberation Sans", true, true, false },
        { "LiberationSerif-Regular.ttf", "Liberation Serif", false, false, false },
        { "LiberationSerif-Bold.ttf", "Liberation Serif", true, false, false },
        { "LiberationSerif-Italic.ttf", "Liberation Serif", false, true, false },
        { "LiberationSerif-BoldItalic.ttf", "Liberation Serif", true, true, false },
        { "LiberationMono-Regular.ttf", "Liberation Mono", false, false, true },
        { "LiberationMono-Bold.ttf", "Liberation Mono", true, false, true },
        { "LiberationMono-Italic.ttf", "Liberation Mono", false, true, true },
        { "LiberationMono-BoldItalic.ttf", "Liberation Mono", true, true, true },
    };

    /// <summary>
    ///     Proves that every one of the 12 bundled Liberation embedded-resource font files loads
    ///     successfully via <see cref="TrueTypeFont"/> (through
    ///     <see cref="SystemFontCatalog.LoadBundledFallbackCore"/>, the same embedded-resource
    ///     loading path <see cref="SystemFontCatalog.LoadBundledFallback"/> itself uses) and
    ///     exposes the expected family name and bold/italic/fixed-pitch style metadata.
    /// </summary>
    [Theory]
    [MemberData(nameof(LiberationVariants))]
    public void BundledFonts_AllTwelveLiberationVariants_LoadSuccessfullyAndExposeExpectedNameAndStyle(
        string bundledFileName,
        string expectedFamilyPrefix,
        bool expectedBold,
        bool expectedItalic,
        bool expectedFixedPitch)
    {
        // Act: load the bundled font through the same embedded-resource path production code uses
        var font = SystemFontCatalog.LoadBundledFallbackCore(bundledFileName);
        var info = font.GetNameInfo();

        // Assert: genuine, well-formed font data with the expected family/style metadata
        Assert.NotNull(info.FamilyName);
        Assert.StartsWith(expectedFamilyPrefix, info.FamilyName, StringComparison.Ordinal);
        Assert.Equal(expectedBold, font.IsBold);
        Assert.Equal(expectedItalic, font.IsItalic);
        Assert.Equal(expectedFixedPitch, font.IsFixedPitch);
        Assert.True(font.GlyphCount > 0);
    }
}
