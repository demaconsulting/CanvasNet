// cspell:ignore Nimbus Dejavu Consolas canvasnet
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="SystemFontCatalog"/>. The scoring tests below (tier-1 exact
///     family match, tier-2 generic-bucket fallback, and no-match cases) exercise
///     <see cref="SystemFontCatalog.FindBestMatchCore"/> over a fully synthetic, injected
///     candidate list, so they never depend on what happens to be installed on the machine
///     running these tests. The scan-tolerance tests exercise
///     <see cref="SystemFontCatalog.BuildCatalogFromRoots"/> against controlled temporary
///     directories, likewise never depending on the real host font directories.
/// </summary>
public class SystemFontCatalogTests
{
    /// <summary>
    ///     Proves that a subdirectory symlink (or junction) that cycles back to an ancestor
    ///     directory does not cause font scanning to hang, and that a sibling real font file is
    ///     still discovered. Skips itself (rather than failing) when the current process lacks
    ///     the privilege to create a directory symlink, since that privilege is environment-
    ///     dependent (for example, Windows requires Developer Mode or an elevated process).
    /// </summary>
    [Fact]
    public void SystemFontCatalog_Fonts_SymlinkCycleInScanDirectory_DoesNotHangAndSiblingFontStillFound()
    {
        // Arrange: root/Valid.ttf plus root/loop -> root (a symlinked subdirectory cycling back
        // to its own ancestor).
        var root = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "canvasnet-test-symlink-cycle-" + Guid.NewGuid())).FullName;

        try
        {
            File.WriteAllBytes(Path.Combine(root, "Valid.ttf"), BuildMinimalValidFont());

            var loopPath = Path.Combine(root, "loop");
            try
            {
                Directory.CreateSymbolicLink(loopPath, root);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // This process/environment cannot create directory symlinks (for example,
                // Windows without Developer Mode or elevation) - nothing more to verify here.
                return;
            }

            // Act
            var result = SystemFontCatalog.BuildCatalogFromRoots([root]);

            // Assert: the scan terminated (it did not hang following the cycle forever) and the
            // real font sibling to the symlink was still discovered.
            var entry = Assert.Single(result);
            Assert.Equal(Path.Combine(root, "Valid.ttf"), entry.FilePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that an exact (case-insensitive) family-name match is found and preferred over
    ///     any generic-bucket candidate, even when the case of the query and the catalog entry
    ///     differ.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_FindBestMatch_ExactFamilyNameCaseInsensitive_ReturnsMatch()
    {
        // Arrange: a synthetic catalog with one exact-family match and one unrelated entry
        SystemFontInfo[] candidates =
        [
            new("Some Other Family", "Regular", false, false, false, "/other.ttf", 0),
            new("My Custom Font", "Bold", true, false, false, "/custom-bold.ttf", 0),
        ];

        // Act: query using a different case than the catalog entry
        var match = SystemFontCatalog.FindBestMatchCore(
            candidates, "my custom font", bold: true, italic: false, serif: false, fixedPitch: false);

        // Assert: the exact family match (case-insensitive) is returned
        Assert.NotNull(match);
        Assert.Equal("/custom-bold.ttf", match.Value.FilePath);
    }

    /// <summary>
    ///     Proves that, among multiple style variants of the same exactly-matched family, the
    ///     variant minimizing mismatched bold/italic flags is preferred, even when no variant is
    ///     an exact style match.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_FindBestMatch_ExactFamilyStyleMismatch_PrefersClosestStyleVariant()
    {
        // Arrange: only Regular and Bold variants exist; BoldItalic is requested
        SystemFontInfo[] candidates =
        [
            new("My Custom Font", "Regular", false, false, false, "/custom-regular.ttf", 0),
            new("My Custom Font", "Bold", true, false, false, "/custom-bold.ttf", 0),
        ];

        // Act: request Bold+Italic (Bold mismatches by 1 flag, Regular mismatches by 2)
        var match = SystemFontCatalog.FindBestMatchCore(
            candidates, "My Custom Font", bold: true, italic: true, serif: false, fixedPitch: false);

        // Assert: the closer (Bold, 1-flag-mismatch) variant wins over Regular (2-flag-mismatch)
        Assert.NotNull(match);
        Assert.Equal("/custom-bold.ttf", match.Value.FilePath);
    }

    /// <summary>
    ///     Proves that, when no exact family match exists, the generic sans-serif bucket's
    ///     well-known family list is searched and the first one actually present is returned.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_FindBestMatch_NoExactFamily_FallsBackToGenericSansBucket()
    {
        // Arrange: no family matches the hint, but "Arial" (first in the generic sans list) exists
        SystemFontInfo[] candidates =
        [
            new("Some Unrelated Family", "Regular", false, false, false, "/unrelated.ttf", 0),
            new("Arial", "Regular", false, false, false, "/arial.ttf", 0),
        ];

        // Act
        var match = SystemFontCatalog.FindBestMatchCore(
            candidates, "Totally Unknown Font", bold: false, italic: false, serif: false, fixedPitch: false);

        // Assert
        Assert.NotNull(match);
        Assert.Equal("/arial.ttf", match.Value.FilePath);
    }

    /// <summary>
    ///     Proves that, when <c>serif</c> is requested and no exact family match exists, the
    ///     generic serif bucket's well-known family list is searched.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_FindBestMatch_NoExactFamily_FallsBackToGenericSerifBucket()
    {
        // Arrange
        SystemFontInfo[] candidates =
        [
            new("Some Unrelated Family", "Regular", false, false, false, "/unrelated.ttf", 0),
            new("Times New Roman", "Regular", false, false, false, "/times.ttf", 0),
        ];

        // Act
        var match = SystemFontCatalog.FindBestMatchCore(
            candidates, "Totally Unknown Font", bold: false, italic: false, serif: true, fixedPitch: false);

        // Assert
        Assert.NotNull(match);
        Assert.Equal("/times.ttf", match.Value.FilePath);
    }

    /// <summary>
    ///     Proves that, when <c>fixedPitch</c> is requested and no exact family match exists, the
    ///     generic monospace bucket's well-known family list is searched.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_FindBestMatch_NoExactFamily_FallsBackToGenericFixedPitchBucket()
    {
        // Arrange
        SystemFontInfo[] candidates =
        [
            new("Some Unrelated Family", "Regular", false, false, false, "/unrelated.ttf", 0),
            new("Courier New", "Regular", false, false, true, "/courier.ttf", 0),
        ];

        // Act
        var match = SystemFontCatalog.FindBestMatchCore(
            candidates, "Totally Unknown Font", bold: false, italic: false, serif: false, fixedPitch: true);

        // Assert
        Assert.NotNull(match);
        Assert.Equal("/courier.ttf", match.Value.FilePath);
    }

    /// <summary>Proves that an empty candidate list always returns <see langword="null"/>.</summary>
    [Fact]
    public void SystemFontCatalog_FindBestMatch_EmptyCandidateList_ReturnsNull()
    {
        // Act
        var match = SystemFontCatalog.FindBestMatchCore(
            [], "Anything", bold: false, italic: false, serif: false, fixedPitch: false);

        // Assert
        Assert.Null(match);
    }

    /// <summary>
    ///     Proves that a non-empty candidate list with no exact family match and no generic-bucket
    ///     match either (none of the well-known bucket family names are present) returns
    ///     <see langword="null"/>.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_FindBestMatch_NoFamilyAndNoBucketMatch_ReturnsNull()
    {
        // Arrange: an entry whose family matches neither the hint nor any well-known bucket name
        SystemFontInfo[] candidates =
        [
            new("Some Wholly Unrelated Family", "Regular", false, false, false, "/unrelated.ttf", 0),
        ];

        // Act
        var match = SystemFontCatalog.FindBestMatchCore(
            candidates, "Totally Unknown Font", bold: false, italic: false, serif: false, fixedPitch: false);

        // Assert
        Assert.Null(match);
    }

    /// <summary>
    ///     Proves that scanning a set of roots including a nonexistent directory does not throw,
    ///     and that a sibling, existing root's fonts are still discovered normally.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_Fonts_MissingScanDirectory_SkippedWithoutThrowing()
    {
        // Arrange: one root that does not exist, and one real temp directory with a valid font
        var missingRoot = Path.Combine(Path.GetTempPath(), "canvasnet-test-missing-" + Guid.NewGuid());
        var validRoot = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "canvasnet-test-valid-" + Guid.NewGuid())).FullName;

        try
        {
            File.WriteAllBytes(Path.Combine(validRoot, "Valid.ttf"), BuildMinimalValidFont());

            // Act
            var result = SystemFontCatalog.BuildCatalogFromRoots([missingRoot, validRoot]);

            // Assert: no exception, and the valid root's font was still discovered
            Assert.Contains(result, f => f.FilePath == Path.Combine(validRoot, "Valid.ttf"));
        }
        finally
        {
            Directory.Delete(validRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that an unparseable candidate file (garbage bytes with a recognized font
    ///     extension) is silently skipped, while a sibling valid font file in the same directory
    ///     is still discovered.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_Fonts_UnparseableCandidateFile_SkippedWithoutThrowing()
    {
        // Arrange: a directory containing one garbage ".ttf" and one genuinely valid ".ttf"
        var root = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "canvasnet-test-unparseable-" + Guid.NewGuid())).FullName;

        try
        {
            File.WriteAllBytes(Path.Combine(root, "Garbage.ttf"), [0x00, 0x01, 0x02, 0x03, 0x04]);
            File.WriteAllBytes(Path.Combine(root, "Valid.ttf"), BuildMinimalValidFont());

            // Act
            var result = SystemFontCatalog.BuildCatalogFromRoots([root]);

            // Assert: only the valid font was discovered; the garbage file was silently skipped
            var entry = Assert.Single(result);
            Assert.Equal(Path.Combine(root, "Valid.ttf"), entry.FilePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Proves that <see cref="SystemFontCatalog.Fonts"/> is built exactly once per process and
    ///     the same cached instance is returned on every subsequent access.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_Fonts_IsLazilyBuiltOnceAndCachedForProcessLifetime()
    {
        // Act: access Fonts twice
        var first = SystemFontCatalog.Fonts;
        var second = SystemFontCatalog.Fonts;

        // Assert: the exact same cached instance is returned both times
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Proves that <see cref="SystemFontCatalog.LoadBundledFallbackCore"/> throws
    ///     <see cref="InvalidOperationException"/> for a deliberately-wrong bundled file name,
    ///     exercising the defensive packaging-integrity guard without deleting any real embedded
    ///     resource.
    /// </summary>
    [Fact]
    public void SystemFontCatalog_LoadBundledFallback_MissingEmbeddedResource_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => SystemFontCatalog.LoadBundledFallbackCore("NoSuchBundledFont-Regular.ttf"));
    }

    /// <summary>
    ///     Builds a minimal, complete, well-formed synthetic font's raw bytes (mirroring
    ///     <c>TrueTypeFontTests.BuildWellFormedFont</c>), suitable for writing to a temporary file
    ///     and loading via <see cref="TrueTypeFont"/>-backed catalog-scanning code.
    /// </summary>
    private static byte[] BuildMinimalValidFont()
    {
        var glyph = SyntheticFontBuilder.SimpleGlyph(
        [
            [(0, 0, true), (10, 0, true), (10, 10, true), (0, 10, true)]
        ]);

        return new SyntheticFontBuilder()
            .AddTable("head", SyntheticFontBuilder.Head(1000, 0))
            .AddTable("maxp", SyntheticFontBuilder.Maxp(1))
            .AddTable("hhea", SyntheticFontBuilder.Hhea(800, -200, 50, 1))
            .AddTable("hmtx", SyntheticFontBuilder.Hmtx([500]))
            .AddTable("loca", SyntheticFontBuilder.Loca([0, glyph.Length], longFormat: false))
            .AddTable("glyf", glyph)
            .Build();
    }
}
