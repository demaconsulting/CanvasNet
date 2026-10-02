// cspell:ignore Noto Dogfoods ZapfDingbats
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Dogfoods all 3 bundled Noto embedded-resource substitute fonts (see
///     <c>Fonts\BundledFonts\README.md</c>'s "Noto Substitute Fonts" section for
///     sourcing/licensing provenance) through <see cref="TrueTypeFont"/>, proving every one is a
///     genuine, well-formed, loadable TrueType font exposing a sane family name - both a
///     one-time sanity check performed before committing these files, and a permanent,
///     repeatable CI gate against any future accidental corruption or replacement of the bundled
///     assets. These 3 fonts are used by <c>DemaConsulting.CanvasNet.Pdf</c> as the
///     <c>Symbol</c>/<c>ZapfDingbats</c> font-fallback substitutes - see
///     <c>PdfDocument.FontFallback.cs</c>'s <c>ResolveSymbolicNotoFallback</c>.
/// </summary>
public class BundledNotoFontsTests
{
    /// <summary>The 3 bundled Noto substitute font files and their expected family-name prefixes.</summary>
    public static TheoryData<string, string> NotoVariants => new()
    {
        { "NotoSans-Regular.ttf", "Noto Sans" },
        { "NotoSansMath-Regular.ttf", "Noto Sans Math" },
        { "NotoSansSymbols2-Regular.ttf", "Noto Sans Symbols" },
    };

    /// <summary>
    ///     Proves that every one of the 3 bundled Noto embedded-resource font files loads
    ///     successfully via <see cref="TrueTypeFont"/> (through
    ///     <see cref="SystemFontCatalog.LoadBundledFallbackCore"/>, the same embedded-resource
    ///     loading path <c>PdfDocument.FontFallback.cs</c>'s <c>ResolveSymbolicNotoFallback</c>
    ///     itself uses) and exposes a sane, expected family name.
    /// </summary>
    [Theory]
    [MemberData(nameof(NotoVariants))]
    public void BundledFonts_AllThreeNotoVariants_LoadSuccessfullyAndExposeExpectedName(
        string bundledFileName,
        string expectedFamilyPrefix)
    {
        // Act: load the bundled font through the same embedded-resource path production code uses
        var font = SystemFontCatalog.LoadBundledFallbackCore(bundledFileName);
        var info = font.GetNameInfo();

        // Assert: genuine, well-formed font data with a sane, expected family name
        Assert.NotNull(info.FamilyName);
        Assert.StartsWith(expectedFamilyPrefix, info.FamilyName, StringComparison.Ordinal);
        Assert.True(font.GlyphCount > 0);
    }
}
