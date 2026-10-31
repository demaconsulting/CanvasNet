using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Tests of font resolution and text measurement.
/// </summary>
public class TextMeasurerTests
{
    /// <summary>
    ///     Validates that every default and generic family resolves to the bundled Liberation fonts, whatever is installed.
    /// </summary>
    [Fact]
    public void TextMeasurer_Resolve_DefaultUsesBundledFonts()
    {
        // Arrange: the bundled fonts, and families that must map to each of them
        var sans = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);
        var serif = SystemFontCatalog.LoadBundledFallback(serif: true, fixedPitch: false, bold: false, italic: false);
        var mono = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: true, bold: false, italic: false);
        string?[] sansFamilies =
        [
            null, string.Empty, "  ", "sans-serif", "system-ui", "Helvetica", "Arial", "Helvetica Neue", "Verdana", "ARIAL",
            "\"Helvetica Neue\", Arial, sans-serif", "'Segoe UI', sans-serif", "No Such Family 7", "No Such Family 7, Other Family",
        ];
        string[] serifFamilies = ["serif", "Times", "Times New Roman", "Georgia", "'Times New Roman', serif", "No Such Family, serif"];
        string[] monoFamilies = ["monospace", "Courier", "Courier New", "Menlo", "Consolas", "ui-monospace, monospace"];

        // Act
        var measurers = new[] { new TextMeasurer(), new TextMeasurer(new VegaLiteFontOptions { UseSystemFonts = true }) };

        // Assert: both measurers (system fonts off and on) use the very same bundled font objects for generic families
        foreach (var measurer in measurers)
        {
            foreach (var family in sansFamilies)
            {
                var resolved = measurer.Resolve(family, null, null);
                Assert.Equal(FontSource.Bundled, resolved.Source);
                Assert.Equal("Liberation Sans", resolved.Family);
                Assert.Same(sans, resolved.Font);
            }

            foreach (var family in serifFamilies)
            {
                var resolved = measurer.Resolve(family, null, null);
                Assert.Equal((FontSource.Bundled, "Liberation Serif"), (resolved.Source, resolved.Family));
                Assert.Same(serif, resolved.Font);
            }

            foreach (var family in monoFamilies)
            {
                var resolved = measurer.Resolve(family, null, null);
                Assert.Equal((FontSource.Bundled, "Liberation Mono"), (resolved.Source, resolved.Family));
                Assert.Same(mono, resolved.Font);
            }
        }
    }

    /// <summary>
    ///     Validates the weight and style rules: 600 and above or bold selects the bold face, italic and oblique the italic face.
    /// </summary>
    [Fact]
    public void TextMeasurer_Resolve_WeightAndStyleSelectTheFace()
    {
        // Arrange
        var measurer = new TextMeasurer();

        // Act
        var regular = measurer.Resolve("sans-serif", "normal", "normal");
        var bold = measurer.Resolve("sans-serif", "bold", null);
        var heavy = measurer.Resolve("sans-serif", "700", null);
        var semi = measurer.Resolve("sans-serif", "600", null);
        var medium = measurer.Resolve("sans-serif", "599", null);
        var italic = measurer.Resolve("sans-serif", null, "italic");
        var oblique = measurer.Resolve("sans-serif", null, "oblique");
        var boldItalicSerif = measurer.Resolve("serif", "bold", "italic");

        // Assert
        Assert.Same(SystemFontCatalog.LoadBundledFallback(false, false, false, false), regular.Font);
        Assert.Same(SystemFontCatalog.LoadBundledFallback(false, false, true, false), bold.Font);
        Assert.Same(bold.Font, heavy.Font);
        Assert.Same(bold.Font, semi.Font);
        Assert.Same(regular.Font, medium.Font);
        Assert.Same(SystemFontCatalog.LoadBundledFallback(false, false, false, true), italic.Font);
        Assert.Same(italic.Font, oblique.Font);
        Assert.Same(SystemFontCatalog.LoadBundledFallback(true, false, true, true), boldItalicSerif.Font);
        Assert.Equal((true, true), (boldItalicSerif.Bold, boldItalicSerif.Italic));
        Assert.True(TextMeasurer.IsBold("bolder"));
        Assert.False(TextMeasurer.IsBold("lighter"));
        Assert.False(TextMeasurer.IsBold(null));
        Assert.False(TextMeasurer.IsItalic("normal"));
        Assert.Same(regular, measurer.Resolve("sans-serif", "normal", "normal"));
    }

    /// <summary>
    ///     Validates that a named family that is not generic is ignored unless system fonts or directories are enabled.
    /// </summary>
    [Fact]
    public void TextMeasurer_Resolve_NamedFamilyNeedsSystemFontsOrDirectories()
    {
        // Arrange: a family that is installed on this machine, if any. The families the measurer maps itself are excluded (it
        // owns that list, which also holds names such as "Helvetica Neue" that macOS installs), as are names that a CSS family
        // list cannot carry unchanged and faces that cannot be loaded as fonts (the host may install formats the loader rejects).
        var installed = SystemFontCatalog.Fonts.FirstOrDefault(f =>
            !string.IsNullOrWhiteSpace(f.FamilyName)
            && !TextMeasurer.IsMappedFamily(f.FamilyName)
            && TextMeasurer.SplitFamilies(f.FamilyName).SequenceEqual([f.FamilyName])
            && CanLoad(f.FilePath, f.FaceIndex));
        var bundledSans = SystemFontCatalog.LoadBundledFallback(false, false, false, false);

        // Act / Assert: without options, a family that does not exist resolves to the bundled sans-serif font
        Assert.Same(bundledSans, new TextMeasurer().Resolve("Surely Not Installed Family", null, null).Font);
        Assert.Same(bundledSans, new TextMeasurer(new VegaLiteFontOptions { UseSystemFonts = true }).Resolve("Surely Not Installed Family", null, null).Font);

        if (installed.FamilyName is null)
        {
            Assert.Skip("No font that the measurer does not map itself is installed on this machine.");
        }

        // Assert: the installed family is ignored by default and by an explicit UseSystemFonts = false
        var ignored = new TextMeasurer(new VegaLiteFontOptions { UseSystemFonts = false }).Resolve(installed.FamilyName, null, null);
        Assert.Equal(FontSource.Bundled, ignored.Source);
        Assert.Same(bundledSans, ignored.Font);
        Assert.Same(bundledSans, new TextMeasurer().Resolve(installed.FamilyName, null, null).Font);

        // Assert: with UseSystemFonts the same family is loaded from the host (case-insensitive), and a missing first family falls through
        var installedFamily = installed.FamilyName;
        var used = new TextMeasurer(new VegaLiteFontOptions { UseSystemFonts = true }).Resolve(installedFamily.ToUpperInvariant(), null, null);
        var fallthrough = new TextMeasurer(new VegaLiteFontOptions { UseSystemFonts = true }).Resolve("Surely Not Installed, '" + installedFamily + "'", null, null);
        Assert.Equal(FontSource.System, used.Source);
        Assert.Equal(installedFamily, used.Family, ignoreCase: true);
        Assert.Equal(FontSource.System, fallthrough.Source);
    }

    /// <summary>
    ///     Validates fonts found in additional directories: by family name, ignoring unusable files and missing folders.
    /// </summary>
    [Fact]
    public void TextMeasurer_Resolve_AdditionalDirectoryFontIsFoundByFamilyName()
    {
        // Arrange: a font file in a temporary directory (one of the bundled Noto fonts, whose family no generic name maps to),
        // next to a file that is not a font
        var folder = Path.Combine(Path.GetTempPath(), "vegalite-fonts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using (var resource = typeof(Surface).Assembly.GetManifestResourceStream("DemaConsulting.CanvasNet.Fonts.BundledFonts.NotoSans-Regular.ttf"))
            {
                Assert.NotNull(resource);
                using var file = File.Create(Path.Combine(folder, "NotoSans-Regular.ttf"));
                resource.CopyTo(file);
            }

            File.WriteAllText(Path.Combine(folder, "broken.ttf"), "this is not a font");
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "ignored");
            var family = TrueTypeFont.Load(Path.Combine(folder, "NotoSans-Regular.ttf")).GetNameInfo().FamilyName;
            Assert.NotNull(family);
            var options = new VegaLiteFontOptions { AdditionalDirectories = [Path.Combine(folder, "missing"), folder] };
            var bundledSans = SystemFontCatalog.LoadBundledFallback(false, false, false, false);

            // Act
            var measurer = new TextMeasurer(options);
            var found = measurer.Resolve(family, null, null);
            var upper = measurer.Resolve(family.ToUpperInvariant(), "bold", null);
            var listed = measurer.Resolve("Missing One, \"" + family + "\", serif", null, null);
            var generic = measurer.Resolve("serif", null, null);
            var without = new TextMeasurer().Resolve(family, null, null);

            // Assert: found by name (case-insensitive), a face without the requested style is used as it is
            Assert.Equal(FontSource.AdditionalDirectory, found.Source);
            Assert.Equal(family, found.Family);
            Assert.Equal(FontSource.AdditionalDirectory, upper.Source);
            Assert.Equal(FontSource.AdditionalDirectory, listed.Source);

            // Assert: generic families never come from the directories, and without the option the family is not found
            Assert.Equal(FontSource.Bundled, generic.Source);
            Assert.Equal(FontSource.Bundled, without.Source);
            Assert.Same(bundledSans, without.Font);

            // Assert: the directory font measures differently from the fallback, so the option really changes the layout
            var text = "Quarterly report";
            Assert.NotEqual(
                TextRenderer.MeasureText(text, bundledSans, 20).Width,
                measurer.MeasureWidth(text, new TextFont(family, 20)),
                3);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that the bound on font files is applied while a directory is enumerated, and that files that are
    ///     not fonts do not count against it.
    /// </summary>
    [Fact]
    public void TextMeasurer_LoadDirectoryFonts_StopsAtTheBound()
    {
        // Arrange: five font files (and three files that are not fonts) in a directory and a subdirectory
        var folder = Path.Combine(Path.GetTempPath(), "vegalite-bound-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        try
        {
            using (var resource = typeof(Surface).Assembly.GetManifestResourceStream("DemaConsulting.CanvasNet.Fonts.BundledFonts.NotoSans-Regular.ttf"))
            {
                Assert.NotNull(resource);
                var bytes = new byte[resource.Length];
                resource.ReadExactly(bytes);
                foreach (var name in new[] { "a.ttf", "b.TTF", "c.otf", Path.Combine("sub", "d.ttf"), Path.Combine("sub", "e.ttf") })
                {
                    File.WriteAllBytes(Path.Combine(folder, name), bytes);
                }
            }

            foreach (var name in new[] { "x.txt", "y.png", Path.Combine("sub", "z.json") })
            {
                File.WriteAllText(Path.Combine(folder, name), "not a font");
            }

            // Act
            var all = TextMeasurer.LoadDirectoryFonts([folder], TextMeasurer.MaxDirectoryFonts);
            var two = TextMeasurer.LoadDirectoryFonts([folder], 2);
            var one = TextMeasurer.LoadDirectoryFonts([folder], 1);
            var spread = TextMeasurer.LoadDirectoryFonts([Path.Combine(folder, "sub"), folder], 3);

            // Assert: the bound limits the font files read, over all directories, and the default is 4,096
            Assert.Equal(4096, TextMeasurer.MaxDirectoryFonts);
            Assert.Equal(5, all.Count);
            Assert.Equal(2, two.Count);
            Assert.Single(one);
            Assert.Equal(3, spread.Count);
            Assert.Empty(TextMeasurer.LoadDirectoryFonts([folder], 0));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    ///     Determines whether a face of an installed font file can be loaded.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="faceIndex">The face index.</param>
    /// <returns>True when the face loads.</returns>
    private static bool CanLoad(string path, int faceIndex)
    {
        try
        {
            return TrueTypeFont.Load(path, faceIndex) is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException
            or NotSupportedException or OverflowException or FormatException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Validates widths, line splitting, truncation with the ellipsis and the extent of line blocks.
    /// </summary>
    [Fact]
    public void TextMeasurer_Measure_TruncatesAndSplitsLines()
    {
        // Arrange
        var measurer = new TextMeasurer();
        var sans = new TextFont("sans-serif", 10);
        var bundled = SystemFontCatalog.LoadBundledFallback(false, false, false, false);

        // Act / Assert: lines split at line feeds, carriage returns and both
        Assert.Equal(["a", "b", "c", "d"], TextMeasurer.SplitLines("a\nb\r\nc\rd"));
        Assert.Equal(["", ""], TextMeasurer.SplitLines("\n"));
        Assert.Empty(TextMeasurer.SplitLines(null));
        Assert.Empty(TextMeasurer.SplitLines(string.Empty));

        // Assert: the width is the advance width of the bundled font at the size, and scales linearly with it
        var width = measurer.MeasureWidth("Hello, world", sans);
        Assert.Equal(TextRenderer.MeasureText("Hello, world", bundled, 10).Width, width, 4);
        Assert.Equal(2 * width, measurer.MeasureWidth("Hello, world", sans with { Size = 20 }), 3);
        Assert.Equal(0, measurer.MeasureWidth(string.Empty, sans));
        Assert.Equal(0, measurer.MeasureWidth("x", sans with { Size = 0 }));
        Assert.True(measurer.MeasureWidth("Hello", sans with { Weight = "bold" }) > measurer.MeasureWidth("Hello", sans));
        Assert.Equal(measurer.MeasureWidth("iiii", new TextFont("monospace", 10)), measurer.MeasureWidth("WWWW", new TextFont("monospace", 10)), 4);

        // Arrange: a limit that a long line does not fit in
        var text = "The quick brown fox jumps over the lazy dog";
        var limit = 100.0;

        // Act
        var cut = measurer.Truncate(text, sans, limit);
        var longer = text[..(cut.Length)] + "\u2026";

        // Assert: Vega's rule: the result ends with the ellipsis, is narrower than the limit, and one more character would not fit
        Assert.EndsWith("\u2026", cut, StringComparison.Ordinal);
        Assert.StartsWith(cut[..^1], text, StringComparison.Ordinal);
        Assert.True(cut.Length < text.Length);
        Assert.True(measurer.MeasureWidth(cut, sans) < limit);
        Assert.True(measurer.MeasureWidth(longer, sans) >= limit);

        // Assert: a text that fits is unchanged, a tiny limit leaves only the ellipsis, and cutting at the start keeps the end
        Assert.Equal("short", measurer.Truncate("short", sans, 100));
        Assert.Equal("\u2026", measurer.Truncate(text, sans, 1));
        Assert.Equal("...", measurer.Truncate("abcdefghij", sans, 12, "...")[^3..]);
        var cutAtStart = measurer.Truncate(text, sans, limit, null, rightToLeft: true);
        Assert.StartsWith("\u2026", cutAtStart, StringComparison.Ordinal);
        Assert.EndsWith(cutAtStart[1..], text, StringComparison.Ordinal);
        Assert.True(measurer.MeasureWidth(cutAtStart, sans) <= limit);

        // Assert: the drawn text is trimmed, truncated only when a limit is set
        Assert.Equal("padded", measurer.TextValue("  padded \t", sans));
        Assert.Equal(string.Empty, measurer.TextValue(null, sans, 50));
        Assert.Equal(cut, measurer.TextValue(text, sans, limit));
        Assert.Equal(text, measurer.TextValue(text, sans, 0));

        // Assert: three lines of a 10 pixel font are 10 + 2 * 12 = 34 high (line height defaults to size + 2), 50 with 20
        var block = measurer.Measure(["one", "a longer second line", "3"], sans);
        Assert.Equal(34, block.Height);
        Assert.Equal(12, block.LineHeight);
        Assert.Equal(measurer.MeasureWidth("a longer second line", sans), block.Width, 6);
        Assert.Equal(50, measurer.Measure(["one", "two", "three"], sans, lineHeight: 20).Height);
        Assert.Equal(10, measurer.Measure(["one"], sans).Height);
        Assert.Equal(10, measurer.Measure([], sans).Height);
        Assert.Equal(0, measurer.Measure([], sans).Width);

        // Assert: truncation is part of the measurement: the widest line is the limited one
        var limited = measurer.Measure([text, "tiny"], sans, limit: limit);
        Assert.Equal(cut, limited.Lines[0]);
        Assert.Equal(measurer.MeasureWidth(cut, sans), limited.Width, 6);
        Assert.True(limited.Width < limit);
    }

    /// <summary>
    ///     Validates Vega's baseline offsets (rounded half up, as JavaScript does), the line height and the family list parser.
    /// </summary>
    [Fact]
    public void TextMeasurer_BaselineOffset_FollowsVegaConstants()
    {
        // Act / Assert: 10 pixel font: top 7.9, middle 3, bottom -2.1, line-top 2.9 + 6, line-bottom 2.9 - 6
        Assert.Equal(8, TextMeasurer.BaselineOffset("top", 10));
        Assert.Equal(3, TextMeasurer.BaselineOffset("middle", 10));
        Assert.Equal(-2, TextMeasurer.BaselineOffset("bottom", 10));
        Assert.Equal(9, TextMeasurer.BaselineOffset("line-top", 10));
        Assert.Equal(-3, TextMeasurer.BaselineOffset("line-bottom", 10));
        Assert.Equal(0, TextMeasurer.BaselineOffset("alphabetic", 10));
        Assert.Equal(0, TextMeasurer.BaselineOffset(null, 10));

        // Assert: a 13 pixel font (the title): 10.27, 3.9 and -2.73
        Assert.Equal(10, TextMeasurer.BaselineOffset("top", 13));
        Assert.Equal(4, TextMeasurer.BaselineOffset("middle", 13));
        Assert.Equal(-3, TextMeasurer.BaselineOffset("bottom", 13));

        // Assert: an explicit line height changes the line-based baselines: 2.9 + 10 and 2.9 - 10
        Assert.Equal(13, TextMeasurer.BaselineOffset("line-top", 10, 20));
        Assert.Equal(-7, TextMeasurer.BaselineOffset("line-bottom", 10, 20));

        // Assert: halves round up like JavaScript's Math.round: 0.3 * 50 is not a half, but -0.21 * 50 = -10.5 gives -10
        Assert.Equal(-10, TextMeasurer.BaselineOffset("bottom", 50));
        Assert.Equal(-10, TextMeasurer.RoundHalfUp(-10.5));
        Assert.Equal(3, TextMeasurer.RoundHalfUp(2.5));
        Assert.Equal(12, TextMeasurer.LineHeight(10));
        Assert.Equal(15, TextMeasurer.LineHeight(10, 15));

        // Assert: family lists honor quotes and commas inside them
        Assert.Equal(["Helvetica Neue", "Arial", "sans-serif"], TextMeasurer.SplitFamilies("\"Helvetica Neue\", Arial ,  sans-serif"));
        Assert.Equal(["A, B", "serif"], TextMeasurer.SplitFamilies("'A, B', serif"));
        Assert.Empty(TextMeasurer.SplitFamilies(" , ,"));
    }

    /// <summary>
    ///     Validates the bounds of text the way Vega computes them, including rotation.
    /// </summary>
    [Fact]
    public void TextMeasurer_GetBounds_FollowsVegaTextBounds()
    {
        // Arrange
        var measurer = new TextMeasurer();
        var font = new TextFont("sans-serif", 13, "bold");
        var width = measurer.MeasureWidth("Title", font);

        // Act
        var left = measurer.GetBounds(new SceneText("t", 10, 20, ["Title"]) { FontSize = 13, FontWeight = "bold", Baseline = "top" });
        var centered = measurer.GetBounds(new SceneText("t", 0, 0, ["Title"]) { FontSize = 13, FontWeight = "bold", Baseline = "top", Align = "center" });
        var rotated = measurer.GetBounds(new SceneText("t", 0, 0, ["Title"]) { FontSize = 13, FontWeight = "bold", Baseline = "top", Align = "center", Angle = -90 });
        var multi = measurer.GetBounds(new SceneText("t", 0, 0, ["a", "b", "c"]) { FontSize = 10, Baseline = "top" });
        var alphabetic = measurer.GetBounds(new SceneText("t", 0, 0, ["a"]) { FontSize = 10 });

        // Assert: the box starts at the anchor and is as wide as the text and as high as the font (the baseline offset 10 minus 80% = 10)
        Assert.Equal(10, left.X1, 6);
        Assert.Equal(20, left.Y1, 6);
        Assert.Equal(10 + width, left.X2, 4);
        Assert.Equal(33, left.Y2, 6);

        // Assert: centering moves the box by half its width; rotating by -90 degrees turns width into height
        Assert.Equal(-width / 2, centered.X1, 4);
        Assert.Equal(width / 2, centered.X2, 4);
        Assert.Equal((0.0, 13.0), (rotated.X1, rotated.X2));
        Assert.Equal(-width / 2, rotated.Y1, 4);
        Assert.Equal(width / 2, rotated.Y2, 4);

        // Assert: three lines are 10 + 2 * 12 high; an alphabetic baseline sits at the box's 80% line (the box starts 8 above it)
        Assert.Equal(34, multi.Height, 6);
        Assert.Equal(-8, alphabetic.Y1, 6);
        Assert.Equal(2, alphabetic.Y2, 6);
    }

    /// <summary>
    ///     Validates that a font size that is finite as a double but not usable as a single (it overflows to infinity or underflows to zero) is unmeasurable instead of throwing
    ///     the raw exception of the core text renderer, for the width, the metrics, the truncation and the bounds of a text.
    /// </summary>
    [Fact]
    public void TextMeasurer_UnrepresentableFontSizeIsUnmeasurableAndNeverThrows()
    {
        // Arrange: 1e300 is finite as a double and infinity as a single; 1e-300 is a positive double and zero as a single; the others are not usable at all
        var measurer = new TextMeasurer();
        var sizes = new[] { 1e300, 1e39, double.MaxValue, 1e-300, 1e-46, 0, -5, double.NaN, double.PositiveInfinity };

        foreach (var size in sizes)
        {
            var font = new TextFont("sans-serif", size);

            // Act / Assert: no width and no metrics, and a limit cannot make the truncation throw (a text of zero width is below any positive limit and is returned whole)
            Assert.Equal(0, measurer.MeasureWidth("Hello", font));
            Assert.Equal(new FontMetrics(0, 0), measurer.GetMetrics(font));
            Assert.Equal("Hello", measurer.Truncate("Hello", font, 10));
            Assert.Equal("Hello", measurer.TextValue("  Hello ", font, 10));
            Assert.Equal(0, measurer.Measure(["Hello"], font, 10).Width);

            // Assert: the height, the line height and the bounds are zero too, never huge or non-finite, with one line or several
            var extent = measurer.Measure(["Hello", "World"], font, lineHeight: 5);
            Assert.Equal((0.0, 0.0, 0.0), (extent.Width, extent.Height, extent.LineHeight));
            Assert.Equal(2, extent.Lines.Count);
            var unmeasured = measurer.GetBounds(new SceneText("t", 7, 9, ["Hello", "World"]) { FontSize = size, Limit = 50, Baseline = "top", Align = "center", Angle = 30 });
            Assert.Equal((7.0, 9.0, 7.0, 9.0), (unmeasured.X1, unmeasured.Y1, unmeasured.X2, unmeasured.Y2));
        }

        // Assert: float.MaxValue * 2 is a finite double that is infinity as a single
        var twiceMax = new TextFont("sans-serif", (double)float.MaxValue * 2);
        Assert.Equal(0, measurer.Measure(["Hello"], twiceMax).Height);
        Assert.Equal(0, measurer.GetBounds(new SceneText("t", 0, 0, ["Hello"]) { FontSize = twiceMax.Size }).Height);

        // Assert: a huge but representable size is still measured, and every output stays finite
        var huge = measurer.Measure(["Hi", "There"], new TextFont("sans-serif", 1e38));
        Assert.All([huge.Width, huge.Height, huge.LineHeight], value => Assert.True(double.IsFinite(value) && value > 0));
        var hugeBounds = measurer.GetBounds(new SceneText("t", 0, 0, ["Hi"]) { FontSize = 1e38, Baseline = "top" });
        Assert.All([hugeBounds.X1, hugeBounds.Y1, hugeBounds.X2, hugeBounds.Y2], value => Assert.True(double.IsFinite(value)));
        Assert.True(hugeBounds.Height > 0);
    }

    /// <summary>
    ///     Validates that the largest size that is a finite single is measured, and that a text of an unmeasurable size does not poison a union of bounds.
    /// </summary>
    [Fact]
    public void TextMeasurer_LargestSingleSizeIsMeasuredAndUnmeasurableBoundsAreEmpty()
    {
        // Arrange
        var measurer = new TextMeasurer();

        // Assert: the largest size that is a finite single is measured (a width that is not a number would not be acceptable either)
        var largest = measurer.MeasureWidth("Hi", new TextFont("sans-serif", 3e38));
        Assert.False(double.IsNaN(largest));

        // Assert: a union with the empty box of an unmeasurable text stays finite
        var visible = measurer.GetBounds(new SceneText("t", 0, 0, ["Hi"]) { FontSize = 10, Baseline = "top" });
        var union = visible.Union(measurer.GetBounds(new SceneText("t", 0, 0, ["Hi"]) { FontSize = 1e300 }));
        Assert.All([union.X1, union.Y1, union.X2, union.Y2], value => Assert.True(double.IsFinite(value)));
        Assert.Equal(visible.X2, union.X2, 6);
        Assert.Equal(visible.Y2, union.Y2, 6);
    }
}
