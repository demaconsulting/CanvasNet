using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the color schemes: the catalog, the lookup, the interpolation of the continuous ramps and the
///     configuration of scale ranges (counts, extent, reversal, limits).
/// </summary>
public class ColorSchemesTests
{
    /// <summary>
    ///     Gets the published schemes: name, whether the scheme is continuous, the number of colors, the first and the
    ///     last color. The rows were read from the published palette tables (Vega's <c>palettes.js</c> and the
    ///     categorical files of d3-scale-chromatic), not from the source of this package.
    /// </summary>
    public static TheoryData<string, bool, int, string, string> Published => new()
    {
        { "accent", false, 8, "#7fc97f", "#666666" },
        { "category10", false, 10, "#1f77b4", "#17becf" },
        { "category20", false, 20, "#1f77b4", "#9edae5" },
        { "category20b", false, 20, "#393b79", "#de9ed6" },
        { "category20c", false, 20, "#3182bd", "#d9d9d9" },
        { "dark2", false, 8, "#1b9e77", "#666666" },
        { "observable10", false, 10, "#4269d0", "#9498a0" },
        { "paired", false, 12, "#a6cee3", "#b15928" },
        { "pastel1", false, 9, "#fbb4ae", "#f2f2f2" },
        { "pastel2", false, 8, "#b3e2cd", "#cccccc" },
        { "set1", false, 9, "#e41a1c", "#999999" },
        { "set2", false, 8, "#66c2a5", "#b3b3b3" },
        { "set3", false, 12, "#8dd3c7", "#ffed6f" },
        { "tableau10", false, 10, "#4c78a8", "#bab0ac" },
        { "tableau20", false, 20, "#4c78a8", "#d8b5a5" },
        { "blues", true, 11, "#cfe1f2", "#0a4a90" },
        { "greens", true, 11, "#d3eecd", "#036429" },
        { "greys", true, 11, "#e2e2e2", "#1e1e1e" },
        { "oranges", true, 11, "#fdd8b3", "#9f3303" },
        { "purples", true, 11, "#e2e1ef", "#501f8c" },
        { "reds", true, 11, "#fdc9b4", "#970b13" },
        { "blueGreen", true, 11, "#d5efed", "#036429" },
        { "bluePurple", true, 11, "#ccddec", "#730f71" },
        { "greenBlue", true, 11, "#d3eece", "#0b60a1" },
        { "orangeRed", true, 11, "#fddcaf", "#a70403" },
        { "purpleBlue", true, 11, "#dbdaeb", "#045281" },
        { "purpleBlueGreen", true, 11, "#dbd8ea", "#016353" },
        { "purpleRed", true, 11, "#dcc9e2", "#8f023a" },
        { "redPurple", true, 11, "#fccfcc", "#700174" },
        { "yellowGreen", true, 11, "#e4f4ac", "#036034" },
        { "yellowOrangeBrown", true, 11, "#feeaa1", "#8f3204" },
        { "yellowOrangeRed", true, 11, "#fee087", "#af0225" },
        { "blueOrange", true, 11, "#134b85", "#994a07" },
        { "brownBlueGreen", true, 11, "#704108", "#025147" },
        { "purpleGreen", true, 11, "#5b1667", "#0e5e29" },
        { "purpleOrange", true, 11, "#411469", "#994a07" },
        { "redBlue", true, 11, "#8c0d25", "#134b85" },
        { "redGrey", true, 11, "#8c0d25", "#343434" },
        { "yellowGreenBlue", true, 11, "#eff9bd", "#1c3185" },
        { "redYellowBlue", true, 11, "#a50026", "#313695" },
        { "redYellowGreen", true, 11, "#a50026", "#006837" },
        { "pinkYellowGreen", true, 11, "#8e0152", "#276419" },
        { "spectral", true, 11, "#9e0142", "#5e4fa2" },
        { "viridis", true, 31, "#440154", "#fde725" },
        { "magma", true, 31, "#000004", "#fcfdbf" },
        { "inferno", true, 31, "#000004", "#fcffa4" },
        { "plasma", true, 31, "#0d0887", "#f0f921" },
        { "cividis", true, 64, "#002051", "#fce647" },
        { "rainbow", true, 31, "#6e40aa", "#6e40aa" },
        { "sinebow", true, 31, "#ff4040", "#ff4040" },
        { "turbo", true, 64, "#23171b", "#910b00" },
        { "browns", true, 11, "#eedbbd", "#9f3632" },
        { "tealBlues", true, 9, "#bce4d8", "#2c5985" },
        { "teals", true, 11, "#bbdfdf", "#006667" },
        { "warmGreys", true, 11, "#dcd4d0", "#59504e" },
        { "goldGreen", true, 11, "#f4d166", "#146c36" },
        { "goldOrange", true, 11, "#f4d166", "#9e3a26" },
        { "goldRed", true, 11, "#f4d166", "#b71d3e" },
        { "lightGreyRed", true, 10, "#efe9e6", "#dc000b" },
        { "lightGreyTeal", true, 10, "#e4eaea", "#1876bc" },
        { "lightMulti", true, 9, "#e0f1f2", "#ef4a3c" },
        { "lightOrange", true, 10, "#f2e7da", "#d43d5b" },
        { "lightTealBlue", true, 10, "#e3e9e0", "#255988" },
        { "darkBlue", true, 10, "#323232", "#ffffff" },
        { "darkGold", true, 10, "#3c3c3c", "#ffffff" },
        { "darkGreen", true, 10, "#3a3a3a", "#ffffaa" },
        { "darkMulti", true, 7, "#373737", "#ffffff" },
        { "darkRed", true, 9, "#343434", "#ffeb2c" },
    };

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Gets a scheme of the catalog or fails the test.
    /// </summary>
    /// <param name="name">The scheme name.</param>
    /// <returns>The scheme.</returns>
    private static ColorScheme Get(string name)
    {
        Assert.True(ColorSchemes.TryGetColorScheme(name, out var scheme), $"The catalog has no scheme '{name}'.");
        return scheme;
    }

    /// <summary>
    ///     Configures a range with the cancellation token of the running test.
    /// </summary>
    /// <param name="scheme">The scheme.</param>
    /// <param name="use">The kind of scale.</param>
    /// <param name="schemeCount">The explicit color count.</param>
    /// <param name="domainSize">The domain size.</param>
    /// <param name="extent">The extent of a continuous scheme.</param>
    /// <param name="reverse">Whether to reverse the colors.</param>
    /// <param name="maxColors">The color limit.</param>
    /// <returns>The range.</returns>
    private static SchemeRange Configure(
        ColorScheme scheme,
        SchemeUse use,
        int? schemeCount = null,
        int domainSize = 0,
        (double Start, double End)? extent = null,
        bool reverse = false,
        int maxColors = int.MaxValue) =>
        ColorSchemes.Configure(scheme, use, schemeCount, domainSize, extent, reverse, maxColors, Ct);

    /// <summary>
    ///     Validates that tableau10 holds the ten colors of the Vega-Lite default category palette in order, and that
    ///     categories cycle through them.
    /// </summary>
    [Fact]
    public void ColorSchemes_Tableau10_ReturnsDocumentedCategoryColors()
    {
        // Arrange: the documented palette
        string[] documented =
        [
            "#4c78a8", "#f58518", "#e45756", "#72b7b2", "#54a24b",
            "#eeca3b", "#b279a2", "#ff9da6", "#9d755d", "#bab0ac",
        ];

        // Act / Assert: the palette in category order
        Assert.Equal(documented, ColorSchemes.Tableau10);
        Assert.Equal("tableau10", ColorSchemes.Tableau10Name);
        for (var i = 0; i < documented.Length; i++)
        {
            Assert.Equal(documented[i], ColorSchemes.Tableau10Color(i));
        }

        // Assert: category i gets color i mod 10
        Assert.Equal("#4c78a8", ColorSchemes.Tableau10Color(10));
        Assert.Equal("#54a24b", ColorSchemes.Tableau10Color(24));
        Assert.Equal("#ff9da6", ColorSchemes.CategoryColor(ColorSchemes.Tableau10, 107));
        Assert.Equal("b", ColorSchemes.CategoryColor(["a", "b", "c"], 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorSchemes.Tableau10Color(-1));
        Assert.Throws<ArgumentException>(() => ColorSchemes.CategoryColor([], 0));
        Assert.Throws<ArgumentNullException>(() => ColorSchemes.CategoryColor(null!, 0));
    }

    /// <summary>
    ///     Validates the catalog as a whole: 68 schemes, 15 categorical and 53 continuous, with unique names.
    /// </summary>
    [Fact]
    public void ColorSchemes_Catalog_HasSixtyEightSchemesWithUniqueNames()
    {
        // Arrange / Act
        var all = ColorSchemes.All;
        var published = Published.Select(row => row.Data.Item1).OrderBy(name => name, StringComparer.Ordinal).ToArray();

        // Assert
        Assert.Equal(68, all.Count);
        Assert.Equal(15, all.Count(s => !s.IsContinuous));
        Assert.Equal(53, all.Count(s => s.IsContinuous));
        Assert.Equal(68, all.Select(s => s.Name.ToLowerInvariant()).Distinct().Count());
        Assert.Equal(68, published.Length);
        Assert.Equal(published, all.Select(s => s.Name).OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Validates the kind and the number of colors of every scheme against the published tables.
    /// </summary>
    /// <param name="name">The scheme name.</param>
    /// <param name="continuous">Whether the scheme is a continuous ramp.</param>
    /// <param name="count">The published number of colors.</param>
    /// <param name="first">The published first color.</param>
    /// <param name="last">The published last color.</param>
    [Theory]
    [MemberData(nameof(Published))]
    public void ColorSchemes_Catalog_HasEverySchemeWithItsStopCount(string name, bool continuous, int count, string first, string last)
    {
        // Arrange / Act
        var scheme = Get(name);

        // Assert: the kind and the number of colors, which every access path reports the same
        Assert.Equal(name, scheme.Name);
        Assert.Equal(continuous, scheme.IsContinuous);
        Assert.Equal(count, scheme.Count);
        Assert.Equal(count, scheme.Colors.Count);
        Assert.Equal(count, scheme.CssColors.Count);
        Assert.Equal(count, scheme.Ramp.StopCount);
        Assert.StartsWith("#", first, StringComparison.Ordinal);
        Assert.StartsWith("#", last, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the first and the last color of every scheme, and that the ramp of a scheme starts and ends on them.
    /// </summary>
    /// <param name="name">The scheme name.</param>
    /// <param name="continuous">Whether the scheme is a continuous ramp.</param>
    /// <param name="count">The published number of colors.</param>
    /// <param name="first">The published first color.</param>
    /// <param name="last">The published last color.</param>
    [Theory]
    [MemberData(nameof(Published))]
    public void ColorSchemes_Catalog_FirstAndLastStopsMatchTheTable(string name, bool continuous, int count, string first, string last)
    {
        // Arrange / Act
        var scheme = Get(name);

        // Assert: the colors of the table
        Assert.Equal(continuous, scheme.IsContinuous);
        Assert.Equal(first, scheme.CssColors[0]);
        Assert.Equal(last, scheme.CssColors[count - 1]);
        Assert.Equal(first, scheme.Colors[0].ToCss());
        Assert.Equal(last, scheme.Colors[count - 1].ToCss());

        // Assert: every color is opaque, and the ramp reaches the first and the last color exactly
        Assert.All(scheme.Colors, color => Assert.Equal(1, color.Opacity));
        Assert.Equal(first, scheme.Ramp.CssAt(0));
        Assert.Equal(last, scheme.Ramp.CssAt(1));
    }

    /// <summary>
    ///     Validates spot values inside the tables: ColorBrewer qualitative sets and ramps, d3 and Vega sets, matplotlib,
    ///     cividis and Turbo stops.
    /// </summary>
    [Fact]
    public void ColorSchemes_Catalog_InteriorStopsMatchThePublishedValues()
    {
        // Assert: the ColorBrewer qualitative sets (Set1 is red, blue, green, purple, orange, yellow, brown, pink, grey)
        Assert.Equal(
            ["#e41a1c", "#377eb8", "#4daf4a", "#984ea3", "#ff7f00", "#ffff33", "#a65628", "#f781bf", "#999999"],
            Get("set1").CssColors);
        Assert.Equal("#fb8072", Get("set3").CssColors[3]);
        Assert.Equal("#fdc086", Get("accent").CssColors[2]);
        Assert.Equal("#e7298a", Get("dark2").CssColors[3]);
        Assert.Equal("#33a02c", Get("paired").CssColors[3]);
        Assert.Equal("#fed9a6", Get("pastel1").CssColors[4]);
        Assert.Equal("#cbd5e8", Get("pastel2").CssColors[2]);
        Assert.Equal("#8da0cb", Get("set2").CssColors[2]);

        // Assert: the d3 and Vega categorical sets
        Assert.Equal("#ff7f0e", Get("category10").CssColors[1]);
        Assert.Equal("#aec7e8", Get("category20").CssColors[1]);
        Assert.Equal("#5254a3", Get("category20b").CssColors[1]);
        Assert.Equal("#6baed6", Get("category20c").CssColors[1]);
        Assert.Equal("#efb118", Get("observable10").CssColors[1]);
        Assert.Equal("#9ecae9", Get("tableau20").CssColors[1]);

        // Assert: single-hue, multi-hue and diverging ramps (the sixth of eleven stops is the middle of the ramp)
        Assert.Equal("#5ba3cf", Get("blues").CssColors[5]);
        Assert.Equal("#f87f2c", Get("oranges").CssColors[5]);
        Assert.Equal("#f9f7ae", Get("redYellowGreen").CssColors[5]);
        Assert.Equal("#fedd8d", Get("spectral").CssColors[4]);
        Assert.Equal("#f2f0eb", Get("blueOrange").CssColors[5]);

        // Assert: the matplotlib colormaps at the middle stop, and the middle of cividis and Turbo
        Assert.Equal("#21918d", Get("viridis").CssColors[15]);
        Assert.Equal("#b6377a", Get("magma").CssColors[15]);
        Assert.Equal("#bb3755", Get("inferno").CssColors[15]);
        Assert.Equal("#cb4779", Get("plasma").CssColors[15]);
        Assert.Equal("#7f7c75", Get("cividis").CssColors[32]);
        Assert.Equal("#95fb51", Get("turbo").CssColors[32]);
    }

    /// <summary>
    ///     Validates the lookup: case does not matter, unknown names are not found, and the categorical lookup used by
    ///     the resolver finds only categorical schemes.
    /// </summary>
    [Fact]
    public void ColorSchemes_TryGetScheme_IgnoresCaseAndRejectsUnknownNames()
    {
        // Assert: any letter case finds the scheme, in the full and the categorical lookup
        Assert.True(ColorSchemes.TryGetScheme("tableau10", out var colors));
        Assert.Equal(10, colors.Count);
        Assert.True(ColorSchemes.TryGetScheme("TABLEAU10", out _));
        Assert.True(ColorSchemes.TryGetColorScheme("BlueGreen", out var blueGreen));
        Assert.Equal("blueGreen", blueGreen.Name);
        Assert.True(ColorSchemes.TryGetColorScheme("BLUEGREEN", out var shouted));
        Assert.Same(blueGreen, shouted);
        Assert.True(ColorSchemes.TryGetColorScheme("bluegreen", out var lower));
        Assert.Same(blueGreen, lower);

        // Assert: a categorical scheme other than tableau10 is found now
        Assert.True(ColorSchemes.TryGetScheme("Category20", out var twenty));
        Assert.Equal(20, twenty.Count);

        // Assert: a continuous scheme is a scheme but not a categorical list
        Assert.True(ColorSchemes.TryGetColorScheme("blues", out _));
        Assert.False(ColorSchemes.TryGetScheme("blues", out var none));
        Assert.Empty(none);

        // Assert: unknown, empty and missing names are rejected
        Assert.False(ColorSchemes.TryGetColorScheme("notAScheme", out _));
        Assert.False(ColorSchemes.TryGetColorScheme(string.Empty, out _));
        Assert.False(ColorSchemes.TryGetColorScheme(null, out _));
        Assert.False(ColorSchemes.TryGetScheme(null, out var missing));
        Assert.Empty(missing);
        Assert.False(ColorSchemes.TryGetColorScheme("cubehelix", out _));
    }

    /// <summary>
    ///     Validates that a continuous scheme interpolates piecewise linearly in sRGB between its stops.
    /// </summary>
    [Fact]
    public void ColorSchemes_Interpolate_IsPiecewiseLinearInSrgb()
    {
        // Arrange: blues has eleven stops, so ten segments of width 0.1
        var blues = Get("blues").Ramp;

        // Assert: a stop position gives the stop (the sixth stop is #5ba3cf)
        Assert.Equal("#5ba3cf", blues.CssAt(0.5));

        // Assert: the middle of the first segment is the mean of #cfe1f2 (207, 225, 242) and #bed8ec (190, 216, 236):
        // 198.5 rounds to 199, 220.5 to 221 and 239 stays
        Assert.Equal("#c7ddef", blues.CssAt(0.05));

        // Assert: viridis starts and ends on its published colors
        var viridis = Get("viridis").Ramp;
        Assert.Equal("#440154", viridis.CssAt(0));
        Assert.Equal("#fde725", viridis.CssAt(1));

        // Assert: a scheme made from colors interpolates in the space asked for (red to blue in HSL passes through magenta)
        var custom = ColorScheme.FromColors([RgbColor.FromBytes(255, 0, 0), RgbColor.FromBytes(0, 0, 255)], InterpolationSpace.Hsl);
        Assert.True(custom.IsContinuous);
        Assert.Equal("custom", custom.Name);
        Assert.Equal(2, custom.Count);
        Assert.Equal("#ff00ff", custom.Ramp.CssAt(0.5));
        Assert.Equal(["#ff0000", "#0000ff"], custom.CssColors);
        Assert.Throws<ArgumentException>(() => ColorScheme.FromColors([]));
    }

    /// <summary>
    ///     Validates that a discrete range from a continuous scheme is sampled at i / (count + 1), without the end points.
    /// </summary>
    [Fact]
    public void ColorSchemes_Sample_UsesQuantizeInterpolatorAtIOverCountPlusOne()
    {
        // Arrange / Act: three colors from blues at 0.25, 0.5 and 0.75 (stop positions 2.5, 5 and 7.5 of ten)
        var range = Configure(Get("blues"), SchemeUse.Quantize, schemeCount: 3);

        // Assert: between the stops #a8cee5 and #8fc1de (155.5, 199.5, 225.5 round up), on the stop #5ba3cf, and
        // between #3181bd and #206fb2 (40.5, 120, 183.5 round up)
        Assert.False(range.IsContinuous);
        Assert.Equal(["#9cc8e2", "#5ba3cf", "#2978b8"], range.ToCssList());

        // Assert: neither end color is used
        Assert.DoesNotContain("#cfe1f2", range.ToCssList());
        Assert.DoesNotContain("#0a4a90", range.ToCssList());
    }

    /// <summary>
    ///     Validates the number of colors for each use: threshold, bin-ordinal, ordinal, quantile and quantize.
    /// </summary>
    [Fact]
    public void ColorSchemes_Configure_ThresholdBinOrdinalOrdinalQuantileCounts()
    {
        var blues = Get("blues");
        var category10 = Get("category10");

        // Assert: threshold: one more color than the thresholds; bin-ordinal: one less than the bin edges
        Assert.Equal(4, Configure(blues, SchemeUse.Threshold, domainSize: 3).ToCssList().Count);
        Assert.Equal(4, Configure(blues, SchemeUse.BinOrdinal, domainSize: 5).ToCssList().Count);
        Assert.Empty(Configure(blues, SchemeUse.BinOrdinal, domainSize: 1).ToCssList());
        Assert.Empty(Configure(blues, SchemeUse.BinOrdinal, domainSize: 0).ToCssList());

        // Assert: quantile and quantize: the explicit count, else five
        Assert.Equal(5, Configure(blues, SchemeUse.Quantile).ToCssList().Count);
        Assert.Equal(5, Configure(blues, SchemeUse.Quantize, domainSize: 9).ToCssList().Count);
        Assert.Equal(7, Configure(blues, SchemeUse.Quantile, schemeCount: 7).ToCssList().Count);
        Assert.Equal(5, Configure(blues, SchemeUse.Quantize, schemeCount: 0).ToCssList().Count);

        // Assert: ordinal over a continuous scheme: the explicit count, else the domain size, else five
        Assert.Equal(4, Configure(blues, SchemeUse.Ordinal, domainSize: 4).ToCssList().Count);
        Assert.Equal(5, Configure(blues, SchemeUse.Ordinal, domainSize: 0).ToCssList().Count);
        Assert.Equal(6, Configure(blues, SchemeUse.Ordinal, schemeCount: 6, domainSize: 4).ToCssList().Count);

        // Assert: a categorical scheme is used whole for an ordinal scale, however large the domain, and is cut to
        // the count by the discretizing scales
        Assert.Equal(category10.CssColors, Configure(category10, SchemeUse.Ordinal, domainSize: 3).ToCssList());
        Assert.Equal(category10.CssColors, Configure(category10, SchemeUse.Ordinal, domainSize: 1_000_000, maxColors: 100).ToCssList());
        Assert.Equal(category10.CssColors.Take(3), Configure(category10, SchemeUse.Quantize, schemeCount: 3).ToCssList());
        Assert.Equal(category10.CssColors.Take(5), Configure(category10, SchemeUse.Quantile).ToCssList());
        Assert.Equal(category10.CssColors.Take(4), Configure(category10, SchemeUse.Threshold, domainSize: 3).ToCssList());
        Assert.Equal(category10.CssColors, Configure(category10, SchemeUse.Threshold, domainSize: 20).ToCssList());
        Assert.Equal(category10.CssColors.Take(4), Configure(category10, SchemeUse.BinOrdinal, domainSize: 5).ToCssList());
    }

    /// <summary>
    ///     Validates the extent (a sub-range, either direction) and the reversal for continuous and discrete ranges.
    /// </summary>
    [Fact]
    public void ColorSchemes_Configure_ExtentAndReverse()
    {
        var blues = Get("blues");

        // Assert: a continuous range maps 0 and 1 to the ends of the scheme; the extent restricts it
        var whole = Configure(blues, SchemeUse.Continuous);
        Assert.True(whole.IsContinuous);
        Assert.Null(whole.Colors);
        Assert.Empty(whole.ToCssList());
        Assert.Equal("#cfe1f2", whole.Interpolator!(0).ToCss());
        Assert.Equal("#0a4a90", whole.Interpolator(1).ToCss());
        var upper = Configure(blues, SchemeUse.Continuous, extent: (0.5, 1));
        Assert.Equal("#5ba3cf", upper.Interpolator!(0).ToCss());
        Assert.Equal("#0a4a90", upper.Interpolator(1).ToCss());

        // Assert: an extent that starts above its end reverses; so does the reverse flag; both together cancel out
        var descending = Configure(blues, SchemeUse.Continuous, extent: (1, 0));
        Assert.Equal("#0a4a90", descending.Interpolator!(0).ToCss());
        Assert.Equal("#cfe1f2", descending.Interpolator(1).ToCss());
        var reversed = Configure(blues, SchemeUse.Continuous, reverse: true);
        Assert.Equal("#0a4a90", reversed.Interpolator!(0).ToCss());
        Assert.Equal("#cfe1f2", reversed.Interpolator(1).ToCss());
        var both = Configure(blues, SchemeUse.Continuous, extent: (1, 0), reverse: true);
        Assert.Equal("#cfe1f2", both.Interpolator!(0).ToCss());
        var reversedSubRange = Configure(blues, SchemeUse.Continuous, extent: (0.2, 0.6), reverse: true);
        Assert.Equal(blues.Ramp.CssAt(0.6), reversedSubRange.Interpolator!(0).ToCss());
        Assert.Equal(blues.Ramp.CssAt(0.2), reversedSubRange.Interpolator(1).ToCss());

        // Assert: a discrete range from a continuous scheme samples the extent, and reversing reverses the list
        var forward = Configure(blues, SchemeUse.Quantize, schemeCount: 4, extent: (0.5, 1)).ToCssList();
        var backward = Configure(blues, SchemeUse.Quantize, schemeCount: 4, extent: (0.5, 1), reverse: true).ToCssList();
        Assert.Equal(4, forward.Count);
        Assert.Equal(forward.Reverse(), backward);
        Assert.Equal(upper.Interpolator(0.2).ToCss(), forward[0]);

        // Assert: a categorical scheme ignores the extent and reverses its list
        var category10 = Get("category10");
        Assert.Equal(category10.CssColors, Configure(category10, SchemeUse.Ordinal, extent: (0.5, 1)).ToCssList());
        Assert.Equal(category10.CssColors.Reverse(), Configure(category10, SchemeUse.Ordinal, reverse: true).ToCssList());
    }

    /// <summary>
    ///     Validates the limit on the number of colors, the cancellation of a long list and the argument checks.
    /// </summary>
    [Fact]
    public void ColorSchemes_Configure_EnforcesTheColorLimitAndCancellation()
    {
        var blues = Get("blues");

        // Assert: a count over the limit is a render exception whose message names the limit
        var ex = Assert.Throws<VegaLiteRenderException>(() => Configure(blues, SchemeUse.Quantize, schemeCount: 10, maxColors: 5));
        Assert.Contains("the limit of 5", ex.Message, StringComparison.Ordinal);
        Assert.Contains("VegaLiteLimits.MaxScaleDomainSize", ex.Message, StringComparison.Ordinal);
        Assert.Contains("blues", ex.Message, StringComparison.Ordinal);

        // Assert: exactly the limit is fine; a threshold domain at the integer limit does not overflow
        Assert.Equal(5, Configure(blues, SchemeUse.Quantize, schemeCount: 5, maxColors: 5).ToCssList().Count);
        Assert.Throws<VegaLiteRenderException>(() => Configure(blues, SchemeUse.Threshold, domainSize: int.MaxValue, maxColors: 100));
        Assert.Throws<VegaLiteRenderException>(() => Configure(blues, SchemeUse.Ordinal, domainSize: 1_000_000, maxColors: 1_000));

        // Assert: a long list stops when the token is cancelled
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            ColorSchemes.Configure(blues, SchemeUse.Quantize, schemeCount: 20_000, cancellationToken: cancelled.Token));

        // Assert: a large but permitted list is produced in full, in order, ascending along the ramp
        var big = Configure(blues, SchemeUse.Quantize, schemeCount: 20_000).ToCssList();
        Assert.Equal(20_000, big.Count);
        Assert.Equal(blues.Ramp.CssAt(1d / 20_001), big[0]);
        Assert.Equal(blues.Ramp.CssAt(20_000d / 20_001), big[^1]);
        Assert.Throws<ArgumentNullException>(() => Configure(null!, SchemeUse.Continuous));
    }

    /// <summary>
    ///     Validates that the license notices of the color schemes exist, are strict UTF-8, carry the text of their
    ///     licenses, and that the provenance record names every scheme and every notice file.
    /// </summary>
    [Fact]
    public void ColorSchemes_Notices_ShipWithTheTablesAndNameEveryLicense()
    {
        // Arrange: the notice files copied next to the test assembly
        var strict = new System.Text.UTF8Encoding(false, true);
        string Read(string name) => strict.GetString(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Notices", name)));

        // Assert: ColorBrewer (Apache-2.0 notice), the Apache License text, Turbo, cividis and the d3 and Vega licenses
        var brewer = Read("LICENSE-colorbrewer.txt");
        Assert.Contains("Copyright 2002 Cynthia Brewer, Mark Harrower, and The Pennsylvania State University", brewer, StringComparison.Ordinal);
        Assert.Contains("Licensed under the Apache License, Version 2.0", brewer, StringComparison.Ordinal);
        var apache = Read("LICENSE-apache-2.0.txt");
        Assert.Contains("Apache License", apache, StringComparison.Ordinal);
        Assert.Contains("Version 2.0, January 2004", apache, StringComparison.Ordinal);
        Assert.Contains("END OF TERMS AND CONDITIONS", apache, StringComparison.Ordinal);
        var turbo = Read("LICENSE-turbo.txt");
        Assert.Contains("Copyright 2019 Google LLC.", turbo, StringComparison.Ordinal);
        Assert.Contains("SPDX-License-Identifier: Apache-2.0", turbo, StringComparison.Ordinal);
        Assert.Contains("Anton Mikhailov", turbo, StringComparison.Ordinal);
        var cividis = Read("LICENSE-cividis.txt");
        Assert.Contains("Copyright (c) 2017, Battelle Memorial Institute", cividis, StringComparison.Ordinal);
        Assert.Contains("Redistributions in binary form must reproduce the above copyright notice", cividis, StringComparison.Ordinal);
        var d3 = Read("LICENSE-d3.txt");
        Assert.Contains("Copyright 2010-2021 Mike Bostock", d3, StringComparison.Ordinal);
        Assert.Contains("Copyright 2010-2022 Mike Bostock", d3, StringComparison.Ordinal);
        Assert.Contains("Copyright 2010-2024 Mike Bostock", d3, StringComparison.Ordinal);
        Assert.Contains("BSD-3-Clause", Read("third-party-provenance.txt"), StringComparison.Ordinal);
        Assert.NotEmpty(Read("LICENSE-vega.txt"));

        // Assert: the provenance record names every notice file and every scheme of the catalog
        var readme = Read("third-party-provenance.txt");
        foreach (var file in new[] { "LICENSE-colorbrewer.txt", "LICENSE-apache-2.0.txt", "LICENSE-turbo.txt", "LICENSE-cividis.txt", "LICENSE-d3.txt", "LICENSE-vega.txt" })
        {
            Assert.Contains(file, readme, StringComparison.Ordinal);
        }

        foreach (var scheme in ColorSchemes.All)
        {
            Assert.Contains($"`{scheme.Name}`", readme, StringComparison.Ordinal);
        }

        // Assert: the CC0 matplotlib colormaps are credited by name
        Assert.Contains("CC0", readme, StringComparison.Ordinal);
        Assert.Contains("Nathaniel J. Smith", readme, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the catalog and the configuration are deterministic, including concurrent first use.
    /// </summary>
    [Fact]
    public void ColorSchemes_Catalog_IsDeterministicUnderConcurrentUse()
    {
        // Arrange / Act: read every scheme and sample a range from several threads at once
        var names = Published.Select(row => row.Data.Item1).ToArray();
        var texts = new string[names.Length];
        var ranges = new string[names.Length];
        Parallel.For(0, names.Length, i =>
        {
            var scheme = Get(names[i]);
            texts[i] = string.Join(',', scheme.CssColors);
            ranges[i] = string.Join(',', Configure(scheme, SchemeUse.Quantize, schemeCount: 7).ToCssList());
        });

        // Assert: a second, sequential pass gives the same text
        for (var i = 0; i < names.Length; i++)
        {
            var scheme = Get(names[i]);
            Assert.Equal(texts[i], string.Join(',', scheme.CssColors));
            Assert.Equal(ranges[i], string.Join(',', Configure(scheme, SchemeUse.Quantize, schemeCount: 7).ToCssList()));
        }
    }
}
