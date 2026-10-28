using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Svg.Tests;

/// <summary>
///     Unit tests for <see cref="SvgCodec"/>'s default font resolution: how a <c>text</c> element is
///     drawn when no caller-registered font satisfies its <c>font-family</c>. A caller-registered font
///     takes precedence; otherwise each family in the list is tried in order (generic keywords map to
///     the bundled Liberation fonts, other names are looked up among installed fonts), and bundled
///     Liberation Sans is the last resort. Every expectation is a pixel-for-pixel comparison against
///     the same text rendered with the expected font registered explicitly by the caller, so none of
///     them depends on which fonts the host has installed unless the test says so.
/// </summary>
public class SvgDefaultFontTests
{
    /// <summary>The probe text: ascenders, descenders, and round and straight strokes.</summary>
    private const string ProbeText = "Hamburg";

    /// <summary>The loader options that never consult the host's installed fonts.</summary>
    private static readonly SvgLoadOptions NoSystemFonts = new() { UseSystemFonts = false };

    /// <summary>Wraps <paramref name="svg"/> as a UTF-8 stream for <see cref="SvgCodec"/> to read.</summary>
    private static MemoryStream ToStream(string svg) => new(Encoding.UTF8.GetBytes(svg));

    /// <summary>Builds a one-text SVG document using the probe text.</summary>
    /// <param name="fontAttributes">The font presentation attributes of the <c>text</c> element.</param>
    private static string BuildSvg(string fontAttributes) =>
        $"<svg viewBox='0 0 200 60'><text x='5' y='45' font-size='40' fill='black' {fontAttributes}>{ProbeText}</text></svg>";

    /// <summary>Rasterizes a document with the 200x60 probe geometry.</summary>
    private static Surface Render(
        string svg,
        IReadOnlyDictionary<string, IReadOnlyList<SvgFontFace>>? fonts = null,
        SvgLoadOptions? options = null)
    {
        using var stream = ToStream(svg);
        return options == null
            ? SvgCodec.LoadWithFontFaces(stream, 200, 60, fonts)
            : SvgCodec.LoadWithFontFaces(stream, 200, 60, fonts, options);
    }

    /// <summary>
    ///     Renders the probe text with <paramref name="font"/> registered by the caller as the only
    ///     face of family <c>Ref</c> - the oracle every default-resolution expectation compares to.
    /// </summary>
    private static Surface RenderReference(TrueTypeFont font, string extraAttributes = "")
    {
        var fonts = new Dictionary<string, IReadOnlyList<SvgFontFace>> { ["Ref"] = [new SvgFontFace(font)] };
        return Render(BuildSvg($"font-family='Ref' {extraAttributes}"), fonts, NoSystemFonts);
    }

    /// <summary>Gets the bundled Liberation font for the given axes.</summary>
    private static TrueTypeFont Bundled(bool serif = false, bool mono = false, bool bold = false, bool italic = false) =>
        SystemFontCatalog.LoadBundledFallback(serif, mono, bold, italic);

    /// <summary>Counts the pixels that received any ink.</summary>
    private static int CountInk(Surface surface)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y].A > 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Asserts that two same-size surfaces contain identical pixels.</summary>
    private static void AssertSurfacesEqual(Surface expected, Surface actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }

    /// <summary>Asserts that two same-size surfaces differ in at least one pixel.</summary>
    private static void AssertSurfacesDiffer(Surface first, Surface second) =>
        Assert.True(SurfacesDiffer(first, second), "The two surfaces are pixel-identical but were expected to differ.");

    /// <summary>
    ///     Proves a generic font-family keyword resolves to the matching bundled Liberation family with
    ///     no font registered by the caller, and with installed-font lookup left at its default.
    /// </summary>
    /// <param name="family">The CSS <c>font-family</c> value.</param>
    /// <param name="serif">Whether Liberation Serif is expected.</param>
    /// <param name="mono">Whether Liberation Mono is expected.</param>
    [Theory]
    [InlineData("sans-serif", false, false)]
    [InlineData("system-ui", false, false)]
    [InlineData("ui-sans-serif", false, false)]
    [InlineData("cursive", false, false)]
    [InlineData("fantasy", false, false)]
    [InlineData("SANS-SERIF", false, false)]
    [InlineData("serif", true, false)]
    [InlineData("ui-serif", true, false)]
    [InlineData("monospace", false, true)]
    [InlineData("ui-monospace", false, true)]
    [InlineData("'monospace'", false, true)]
    public void SvgCodec_Load_TextGenericFamilyWithoutRegisteredFonts_RendersBundledLiberationFont(
        string family, bool serif, bool mono)
    {
        // Arrange
        var svg = BuildSvg($"font-family=\"{family}\"");
        var expected = RenderReference(Bundled(serif, mono));

        // Act: no fonts registered, installed-font lookup at its default
        using var stream = ToStream(svg);
        var actual = SvgCodec.Load(stream, 200, 60);

        // Assert
        Assert.True(CountInk(actual) > 0);
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>
    ///     Proves the three Liberation families are genuinely distinct renderings, so the equality
    ///     assertions in the other tests cannot pass vacuously by every keyword resolving to one font.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextGenericFamilies_SansSerifAndMonospaceRenderDifferently()
    {
        // Arrange / Act
        var sans = Render(BuildSvg("font-family='sans-serif'"));
        var serif = Render(BuildSvg("font-family='serif'"));
        var mono = Render(BuildSvg("font-family='monospace'"));

        // Assert
        AssertSurfacesDiffer(sans, serif);
        AssertSurfacesDiffer(sans, mono);
        AssertSurfacesDiffer(serif, mono);
    }

    /// <summary>
    ///     Proves <c>font-weight</c> and <c>font-style</c> select the matching bundled style variant
    ///     for a generic family (a weight of 600 or more is bold; <c>oblique</c> is italic).
    /// </summary>
    /// <param name="attributes">The font presentation attributes.</param>
    /// <param name="bold">Whether the bold variant is expected.</param>
    /// <param name="italic">Whether the italic variant is expected.</param>
    [Theory]
    [InlineData("font-weight='bold'", true, false)]
    [InlineData("font-weight='700'", true, false)]
    [InlineData("font-weight='600'", true, false)]
    [InlineData("font-weight='500'", false, false)]
    [InlineData("font-weight='normal'", false, false)]
    [InlineData("font-style='italic'", false, true)]
    [InlineData("font-style='oblique'", false, true)]
    [InlineData("font-weight='bold' font-style='italic'", true, true)]
    public void SvgCodec_Load_TextGenericFamilyWithWeightAndStyle_RendersBundledStyleVariant(
        string attributes, bool bold, bool italic)
    {
        // Arrange
        var expected = RenderReference(Bundled(bold: bold, italic: italic));

        // Act
        var actual = Render(BuildSvg($"font-family='sans-serif' {attributes}"));

        // Assert
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>
    ///     Proves the style variant is also applied for a serif and a monospace generic family.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextGenericSerifAndMonospaceBoldItalic_RendersBundledStyleVariants()
    {
        // Arrange
        var expectedSerif = RenderReference(Bundled(serif: true, bold: true));
        var expectedMono = RenderReference(Bundled(mono: true, italic: true));

        // Act
        var actualSerif = Render(BuildSvg("font-family='serif' font-weight='bold'"));
        var actualMono = Render(BuildSvg("font-family='monospace' font-style='italic'"));

        // Assert
        AssertSurfacesEqual(expectedSerif, actualSerif);
        AssertSurfacesEqual(expectedMono, actualMono);
    }

    /// <summary>
    ///     Proves a family no font matches resolves to bundled Liberation Sans in the requested
    ///     weight and style when installed fonts are not consulted - the deterministic mode.
    /// </summary>
    /// <param name="attributes">The font presentation attributes (besides the unknown family).</param>
    /// <param name="bold">Whether the bold variant is expected.</param>
    /// <param name="italic">Whether the italic variant is expected.</param>
    [Theory]
    [InlineData("", false, false)]
    [InlineData("font-weight='bold'", true, false)]
    [InlineData("font-style='italic'", false, true)]
    public void SvgCodec_Load_TextUnknownFamilySystemFontsDisabled_RendersBundledLiberationSans(
        string attributes, bool bold, bool italic)
    {
        // Arrange
        var expected = RenderReference(Bundled(bold: bold, italic: italic));

        // Act
        var actual = Render(BuildSvg($"font-family='No Such Family 7f3a' {attributes}"), options: NoSystemFonts);

        // Assert
        Assert.True(CountInk(actual) > 0);
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>
    ///     Proves a <c>text</c> element with no <c>font-family</c> at all still renders, with bundled
    ///     Liberation Sans.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextWithoutFontFamilyAttribute_RendersBundledLiberationSans()
    {
        // Arrange
        var expected = RenderReference(Bundled());

        // Act
        var actual = Render(BuildSvg(string.Empty), options: NoSystemFonts);

        // Assert
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>
    ///     Proves a family list is walked in order: an unknown name is skipped, and the first generic
    ///     keyword that follows decides the font (a later keyword is never reached).
    /// </summary>
    /// <param name="family">The <c>font-family</c> list.</param>
    /// <param name="serif">Whether Liberation Serif is expected.</param>
    /// <param name="mono">Whether Liberation Mono is expected.</param>
    [Theory]
    [InlineData("No Such Family 7f3a, serif, monospace", true, false)]
    [InlineData("'No Such Family 7f3a', monospace, serif", false, true)]
    [InlineData("serif, monospace", true, false)]
    [InlineData("monospace, serif", false, true)]
    [InlineData("No Such Family 7f3a, Another Missing 91cc", false, false)]
    public void SvgCodec_Load_TextFamilyList_UsesFirstResolvableFamilyInOrder(string family, bool serif, bool mono)
    {
        // Arrange
        var expected = RenderReference(Bundled(serif, mono));

        // Act
        var actual = Render(BuildSvg($"font-family=\"{family}\""), options: NoSystemFonts);

        // Assert
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>
    ///     Proves a caller-registered font keeps precedence over every default: it wins over an
    ///     earlier generic keyword in the same list, and over a registered entry named like a
    ///     generic keyword's bundled substitute.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextRegisteredFontLaterInList_TakesPrecedenceOverEarlierGenericKeyword()
    {
        // Arrange: "Mine" is registered as Liberation Mono; the list names serif first
        var fonts = new Dictionary<string, IReadOnlyList<SvgFontFace>> { ["Mine"] = [new SvgFontFace(Bundled(mono: true))] };
        var expected = RenderReference(Bundled(mono: true));

        // Act
        var actual = Render(BuildSvg("font-family='serif, Mine'"), fonts);

        // Assert
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>
    ///     Proves a caller may register a font under a generic keyword and have it honored instead of
    ///     the bundled substitute.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextRegisteredFontNamedLikeGenericKeyword_TakesPrecedenceOverBundledFont()
    {
        // Arrange: "sans-serif" is registered as Liberation Serif
        var fonts = new Dictionary<string, IReadOnlyList<SvgFontFace>> { ["sans-serif"] = [new SvgFontFace(Bundled(serif: true))] };
        var expected = RenderReference(Bundled(serif: true));

        // Act
        var actual = Render(BuildSvg("font-family='sans-serif'"), fonts);

        // Assert
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>
    ///     Proves the legacy single-font-per-family <c>Load</c> overload still wins when it supplies a
    ///     matching font, and that an unmatched family then falls through to the default font.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextLegacyFontsOverload_RegisteredFontWinsAndUnmatchedFamilyFallsThrough()
    {
        // Arrange
        var fonts = new Dictionary<string, TrueTypeFont> { ["Mine"] = Bundled(mono: true) };
        var expectedMine = RenderReference(Bundled(mono: true));
        var expectedDefault = RenderReference(Bundled());

        // Act
        using var mineStream = ToStream(BuildSvg("font-family='Mine'"));
        var mine = SvgCodec.Load(mineStream, 200, 60, fonts, NoSystemFonts);
        using var otherStream = ToStream(BuildSvg("font-family='Other'"));
        var other = SvgCodec.Load(otherStream, 200, 60, fonts, NoSystemFonts);

        // Assert
        AssertSurfacesEqual(expectedMine, mine);
        AssertSurfacesEqual(expectedDefault, other);
    }

    /// <summary>
    ///     Proves that two <c>text</c> elements of one family but different weights each resolve to their
    ///     own bundled variant within a single render (the per-render cache keys on weight and style).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextMixedWeightsInOneDocument_ResolvesEachToItsOwnBundledVariant()
    {
        // Arrange: the oracle registers regular and bold Liberation Sans faces under one family
        const string template = "<svg viewBox='0 0 200 60'>" +
            "<text x='5' y='25' font-size='20' fill='black' font-family='{0}'>Hamburg</text>" +
            "<text x='5' y='50' font-size='20' fill='black' font-family='{0}' font-weight='bold'>Hamburg</text></svg>";
        var fonts = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["Ref"] = [new SvgFontFace(Bundled(), Weight: 400), new SvgFontFace(Bundled(bold: true), Weight: 700)]
        };
        var expected = Render(string.Format(template, "Ref"), fonts, NoSystemFonts);

        // Act
        var actual = Render(string.Format(template, "No Such Family 7f3a"), options: NoSystemFonts);

        // Assert
        AssertSurfacesEqual(expected, actual);
    }

    /// <summary>
    ///     Proves that, with installed-font lookup enabled, a family installed on the host is rendered
    ///     with that installed font, and that disabling the lookup renders bundled Liberation Sans
    ///     instead. On a host with no installed font that renders differently from the bundled one,
    ///     the test degrades to checking that an unknown family renders the bundled font.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextInstalledFamily_UsesInstalledFontOnlyWhenSystemFontsEnabled()
    {
        // Arrange: find an installed, non-generic family whose rendering differs from bundled Sans
        var bundledSans = RenderReference(Bundled());
        string? family = null;
        Surface? expected = null;
        foreach (var candidate in SystemFontCatalog.Fonts)
        {
            var name = candidate.FamilyName;
            if (name.Contains(',') || name.Contains('\'') || name.Contains('"') || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var match = SystemFontCatalog.FindBestMatch(name, bold: false, italic: false, serif: false, fixedPitch: false);
            if (match is not { } info || !string.Equals(info.FamilyName, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TrueTypeFont font;
            try
            {
                font = TrueTypeFont.Load(info.FilePath, info.FaceIndex);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                continue;
            }

            var reference = RenderReference(font);
            if (CountInk(reference) == 0 || !SurfacesDiffer(bundledSans, reference))
            {
                continue;
            }

            family = name;
            expected = reference;
            break;
        }

        if (family == null || expected == null)
        {
            // Nothing installed renders differently from the bundled font (for example a minimal
            // container), so installed-font lookup cannot be told apart from the bundled fallback;
            // the contract that remains observable is that an unknown family still renders Sans
            family = "No Such Family 7f3a";
            expected = bundledSans;
        }

        // Act
        var enabled = Render(BuildSvg($"font-family=\"{family}\""), options: new SvgLoadOptions { UseSystemFonts = true });
        var defaulted = Render(BuildSvg($"font-family=\"{family}\""));
        var disabled = Render(BuildSvg($"font-family=\"{family}\""), options: NoSystemFonts);

        // Assert
        AssertSurfacesEqual(expected, enabled);
        AssertSurfacesEqual(expected, defaulted);
        AssertSurfacesEqual(bundledSans, disabled);
    }

    /// <summary>
    ///     Proves a <c>text</c> child of a <c>clipPath</c> clips with the default font's glyph outlines
    ///     when no font is registered, rather than clipping everything away.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ClipPathTextWithoutRegisteredFonts_ClipsToDefaultFontGlyphs()
    {
        // Arrange: a red rectangle clipped by text
        const string svg = "<svg viewBox='0 0 200 60'><defs><clipPath id='c'>" +
            "<text x='5' y='45' font-size='40' font-family='sans-serif'>Hamburg</text></clipPath></defs>" +
            "<rect width='200' height='60' fill='red' clip-path='url(#c)'/></svg>";

        // Act
        var surface = Render(svg, options: NoSystemFonts);

        // Assert: some, but not all, of the rectangle survives the clip
        var ink = CountInk(surface);
        Assert.True(ink > 0);
        Assert.True(ink < 200 * 60 / 2);
    }

    /// <summary>
    ///     Proves a registered family with an empty face list counts as unavailable, so a later
    ///     registered family in the list is still used (and an empty-only list falls to the default).
    /// </summary>
    [Fact]
    public void SvgCodec_Load_TextRegisteredFamilyWithEmptyFaceList_FallsThroughToLaterRegisteredFamily()
    {
        // Arrange: "Empty" has no faces; "Mine" is Liberation Mono
        var fonts = new Dictionary<string, IReadOnlyList<SvgFontFace>>
        {
            ["Empty"] = [],
            ["Mine"] = [new SvgFontFace(Bundled(mono: true))]
        };
        var expectedMine = RenderReference(Bundled(mono: true));
        var expectedDefault = RenderReference(Bundled());

        // Act
        var later = Render(BuildSvg("font-family='Empty, Mine'"), fonts, NoSystemFonts);
        var emptyOnly = Render(BuildSvg("font-family='Empty'"), fonts, NoSystemFonts);

        // Assert
        AssertSurfacesEqual(expectedMine, later);
        AssertSurfacesEqual(expectedDefault, emptyOnly);
    }

    /// <summary>
    ///     Proves a text run inside a <c>clipPath</c> is charged against the geometry work budget, so
    ///     an oversized run is rejected before glyph layout begins, with no registered font needed.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_ClipPathTextExceedingGeometryWorkBudget_ThrowsInvalidDataException()
    {
        // Arrange: one more character than the codec's fixed 200,000 geometry work budget
        var text = new string('A', 200_001);
        var svg = "<svg viewBox='0 0 10 10'><defs><clipPath id='c'>" +
            $"<text x='0' y='5' font-size='10' font-family='sans-serif'>{text}</text></clipPath></defs>" +
            "<rect width='10' height='10' clip-path='url(#c)'/></svg>";
        using var stream = ToStream(svg);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => SvgCodec.Load(stream, 10, 10));
    }

    /// <summary>
    ///     Proves a filtered group containing <c>text</c> measures and renders that text with the
    ///     default font when no font is registered.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_FilteredGroupTextWithoutRegisteredFonts_RendersDefaultFontGlyphs()
    {
        // Arrange
        const string svg = "<svg viewBox='0 0 200 60'><defs><filter id='f'><feGaussianBlur stdDeviation='0.5'/></filter></defs>" +
            "<g filter='url(#f)'><text x='5' y='45' font-size='40' font-family='sans-serif' fill='black'>Hamburg</text></g></svg>";

        // Act
        var surface = Render(svg, options: NoSystemFonts);

        // Assert
        Assert.True(CountInk(surface) > 0);
    }

    /// <summary>
    ///     Proves the options overloads render identically to the options-free overloads for default
    ///     options, and that the file-path overload accepts options.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_DefaultOptionsOverloads_MatchOptionsFreeOverloads()
    {
        // Arrange: generic keyword, so the expectation is host-independent
        var svg = BuildSvg("font-family='serif'");
        var path = Path.Combine(Path.GetTempPath(), $"svg-default-fonts-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, svg);
        try
        {
            using var stream = ToStream(svg);
            var optionsFree = SvgCodec.Load(stream, 200, 60);

            // Act
            using var optionsStream = ToStream(svg);
            var withOptions = SvgCodec.Load(optionsStream, 200, 60, null, new SvgLoadOptions());
            var fromPath = SvgCodec.Load(path, 200, 60, null, new SvgLoadOptions());
            var facesFromPath = SvgCodec.LoadWithFontFaces(path, 200, 60, null, NoSystemFonts);

            // Assert
            AssertSurfacesEqual(optionsFree, withOptions);
            AssertSurfacesEqual(optionsFree, fromPath);
            AssertSurfacesEqual(optionsFree, facesFromPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Proves a null <see cref="SvgLoadOptions"/> is rejected by every options overload.
    /// </summary>
    [Fact]
    public void SvgCodec_Load_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        using var stream = ToStream(BuildSvg("font-family='serif'"));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SvgCodec.Load(stream, 200, 60, null, null!));
        Assert.Throws<ArgumentNullException>(() => SvgCodec.Load("x.svg", 200, 60, null, null!));
        Assert.Throws<ArgumentNullException>(() => SvgCodec.LoadWithFontFaces(stream, 200, 60, null, null!));
        Assert.Throws<ArgumentNullException>(() => SvgCodec.LoadWithFontFaces("x.svg", 200, 60, null, null!));
    }

    /// <summary>Proves <see cref="SvgLoadOptions.UseSystemFonts"/> defaults to enabled.</summary>
    [Fact]
    public void SvgLoadOptions_Default_UsesSystemFonts()
    {
        // Arrange / Act
        var options = new SvgLoadOptions();

        // Assert
        Assert.True(options.UseSystemFonts);
    }

    /// <summary>Returns whether any pixel differs between two same-size surfaces.</summary>
    private static bool SurfacesDiffer(Surface first, Surface second)
    {
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                if (first[x, y] != second[x, y])
                {
                    return true;
                }
            }
        }

        return false;
    }
}
