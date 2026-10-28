// cspell:ignore antiqua consolas lucida menlo neue palatino scenegraph tahoma vegalite verdana
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
        // Arrange: a family that is installed on this machine, if any (the families the measurer maps itself are excluded)
        var generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Arial", "Helvetica", "Liberation Sans", "DejaVu Sans", "Segoe UI", "Verdana", "Tahoma", "Trebuchet MS", "Roboto", "Open Sans",
            "Times New Roman", "Times", "Georgia", "Liberation Serif", "Palatino Linotype", "Book Antiqua", "Cambria", "Garamond", "Palatino",
            "Courier New", "Courier", "Consolas", "Liberation Mono", "DejaVu Sans Mono", "Lucida Console", "Monaco", "Menlo",
        };
        var installed = SystemFontCatalog.Fonts.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.FamilyName) && !generic.Contains(f.FamilyName));
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
}
