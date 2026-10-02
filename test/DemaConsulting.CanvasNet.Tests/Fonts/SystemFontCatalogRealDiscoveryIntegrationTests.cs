// cspell:ignore Neue Segoe Xyzzy
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Real-filesystem integration tests for <see cref="SystemFontCatalog"/>: unconditional on
///     every CI runner (Windows/Linux/macOS), never skipped, but "conditionally strengthen the
///     assertion" - each test asserts a property that is always true regardless of how many (or
///     how few) fonts happen to be installed on the machine actually running the test.
/// </summary>
public class SystemFontCatalogRealDiscoveryIntegrationTests
{
    /// <summary>
    ///     Proves that scanning this operating system's real, well-known font directories
    ///     completes without throwing, and that every discovered entry's <see cref="SystemFontInfo.FilePath"/>
    ///     genuinely exists on disk - regardless of whether the catalog turns out to be empty (for
    ///     example a minimal container image with no fonts installed) or populated.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_RealFilesystem_ScanKnownOsDirectories_DiscoversFontsOrIsGracefullyEmpty()
    {
        // Act: this is the real, unmocked SystemFontCatalog.Fonts scan
        var fonts = SystemFontCatalog.Fonts;

        // Assert: the scan completed (no exception), and every reported entry satisfies the
        // documented invariants (CanvasNet-Fonts-SystemFontCatalog-DirectoryScanDiscovery): the
        // path genuinely exists on disk, the family name is non-empty, and the path has one of
        // the three supported font-file extensions.
        Assert.NotNull(fonts);
        foreach (var font in fonts)
        {
            Assert.True(File.Exists(font.FilePath), $"Reported font path '{font.FilePath}' does not exist.");
            Assert.False(string.IsNullOrEmpty(font.FamilyName), $"Reported font '{font.FilePath}' has an empty family name.");
            var extension = Path.GetExtension(font.FilePath);
            Assert.True(
                extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ttc", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase),
                $"Reported font path '{font.FilePath}' does not end in .ttf, .ttc, or .otf.");
        }
    }

    /// <summary>
    ///     Proves the strongest property this method can assert without depending on what is
    ///     actually installed: if a documented platform-typical family (Arial on Windows,
    ///     Liberation Sans or DejaVu Sans on Linux, Helvetica Neue on macOS) is present in
    ///     <see cref="SystemFontCatalog.Fonts"/>, then <see cref="SystemFontCatalog.FindBestMatch"/>
    ///     resolves to that real system font rather than requiring the bundled fallback; if none
    ///     of those families are installed, the weaker (always-true) property is asserted instead:
    ///     the OS-only catalog match for an unrelated, made-up family name is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_RealFilesystem_KnownPlatformFontFamily_ResolvesToRealFontWhenInstalled()
    {
        // The exact same well-known generic-sans family list FindBestMatch itself searches (see
        // SystemFontCatalog's own GenericSansFamilies), so "installed" here is always consistent
        // with what a made-up-name query would actually resolve to.
        string[] platformTypicalSansFamilies =
        [
            "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans", "Nimbus Sans", "Segoe UI",
        ];

        var installedFamily = platformTypicalSansFamilies.FirstOrDefault(
            family => SystemFontCatalog.Fonts.Any(
                f => string.Equals(f.FamilyName, family, StringComparison.OrdinalIgnoreCase)));

        if (installedFamily is not null)
        {
            // Strong assertion: a real system font resolves for its own exact family name
            var match = SystemFontCatalog.FindBestMatch(
                installedFamily, bold: false, italic: false, serif: false, fixedPitch: false);

            Assert.NotNull(match);
            Assert.Equal(installedFamily, match.Value.FamilyName, ignoreCase: true);
        }
        else
        {
            // Weaker, always-true assertion: nothing in the OS-only catalog matches a made-up name
            var match = SystemFontCatalog.FindBestMatch(
                "Totally-Unlikely-Font-Name-Xyzzy", bold: false, italic: false, serif: false, fixedPitch: false);

            Assert.Null(match);
        }
    }
}
