using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the scale resolver for color and the new channels: the default scale kinds and ranges per field type, channel and mark, the
///     sequential, diverging, quantize, quantile and threshold scales, schemes and <c>config.range</c>, the numeric ranges of the new channels, the
///     limits and the cancellation. Colors are compared with the samples of the scheme ramps of <see cref="ColorSchemes"/> at positions derived by hand
///     from the d3 formulas (for example 5 in the domain 0 to 10 is the position 0.5).
/// </summary>
public class ScaleResolverColorTests
{
    /// <summary>The tolerance for numbers computed with fractions.</summary>
    private const int Precision = 9;

    /// <summary>The data of most tests: categories a, b, c and numbers q from 3 to 12 (the sorted numbers are 3, 5, 7, 12).</summary>
    private const string SampleData = """
        "data": { "values": [
          { "k": "b", "q": 3, "r": 10 },
          { "k": "a", "q": 7, "r": 20 },
          { "k": "c", "q": 12, "r": 35 },
          { "k": "a", "q": 5, "r": 15 }
        ] }
        """;

    /// <summary>The five dash patterns of Vega-Lite.</summary>
    private static readonly double[][] FiveDashes = [[1, 0], [4, 2], [2, 1], [1, 1], [1, 2, 4, 2]];

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Builds a specification over the sample data.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding object text.</param>
    /// <param name="extra">More top-level properties, with a trailing comma, or empty.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string encoding, string extra = "") =>
        "{" + extra + SampleData + ", \"mark\": \"" + mark + "\", \"encoding\": " + encoding + "}";

    /// <summary>
    ///     Builds the <c>config</c> property that a specification text can start with.
    /// </summary>
    /// <param name="config">The configuration object text.</param>
    /// <returns>The property with a trailing comma.</returns>
    private static string Cfg(string config) => "\"config\":" + config + ",";

    /// <summary>
    ///     Builds the encoding of one channel over a field of the sample data.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="scale">The scale object text, or empty.</param>
    /// <param name="field">The field.</param>
    /// <returns>The encoding object text.</returns>
    private static string Enc(string channel, string type, string scale = "", string field = "q")
    {
        var scaleText = scale.Length > 0 ? ",\"scale\":" + scale : string.Empty;
        return "{\"" + channel + "\":{\"field\":\"" + field + "\",\"type\":\"" + type + "\"" + scaleText + "}}";
    }

    /// <summary>
    ///     Resolves a specification and returns its scales.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding object text.</param>
    /// <param name="extra">More top-level properties, with a trailing comma, or empty.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The scales.</returns>
    private static ResolvedScales Resolve(string mark, string encoding, string extra = "", VegaLiteLimits? limits = null) =>
        ChartTestHelpers.Parts(Spec(mark, encoding, extra), null, limits).Scales;

    /// <summary>
    ///     Resolves a specification and returns the scale of a channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding object text.</param>
    /// <param name="extra">More top-level properties, with a trailing comma, or empty.</param>
    /// <returns>The scale.</returns>
    private static ResolvedScale Scale(string channel, string mark, string encoding, string extra = "") =>
        Resolve(mark, encoding, extra)[channel] ?? throw new InvalidOperationException("The test specification has no scale for " + channel);

    /// <summary>
    ///     Looks up a color scheme of the catalog.
    /// </summary>
    /// <param name="name">The scheme name.</param>
    /// <returns>The scheme.</returns>
    private static ColorScheme Scheme(string name)
    {
        Assert.True(ColorSchemes.TryGetColorScheme(name, out var scheme));
        return scheme;
    }

    /// <summary>
    ///     Gets the color of a scheme at a position as the CSS text a scale gives.
    /// </summary>
    /// <param name="scheme">The scheme.</param>
    /// <param name="t">The position, 0 to 1.</param>
    /// <returns>The <c>#rrggbb</c> text.</returns>
    private static string Css(ColorScheme scheme, double t) => scheme.Ramp.At(t).ToCss();

    /// <summary>
    ///     Maps a number through a model to its text.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string? Text(ScaleModel model, double value) => model.MapText(DataValue.FromNumber(value));

    /// <summary>
    ///     Reads the channels of a CSS color text.
    /// </summary>
    /// <param name="css">The <c>#rrggbb</c> text.</param>
    /// <returns>The color.</returns>
    private static RgbColor Rgb(string? css)
    {
        Assert.True(RgbColor.TryParseHex(css.AsSpan(), out var color));
        return color;
    }

    /// <summary>
    ///     Asserts that two colors differ by at most one in every channel (the trigonometry of the polar spaces may differ in the last bit between platforms).
    /// </summary>
    /// <param name="expected">The expected color.</param>
    /// <param name="css">The actual CSS text.</param>
    private static void AssertNear(RgbColor expected, string? css)
    {
        var actual = Rgb(css);
        Assert.InRange(actual.R, expected.R - 1, expected.R + 1);
        Assert.InRange(actual.G, expected.G - 1, expected.G + 1);
        Assert.InRange(actual.B, expected.B - 1, expected.B + 1);
    }

    /// <summary>
    ///     Validates that a quantitative field on a color channel gets a sequential scale over the extent of the data, with the <c>ramp</c> range
    ///     (the blues scheme) and no zero or nice, mapping through the interpolator as d3's <c>scaleSequential</c> does, and that fill and stroke
    ///     are separate scales.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Default_QuantitativeColorIsSequentialWithTheRampScheme()
    {
        // Arrange / Act: the data extent is 3 to 12 with no zero and no nice
        var scale = Scale("color", "point", Enc("color", "quantitative"));
        var model = scale.CreateModel(0);
        var blues = Scheme("blues");

        // Assert: a sequential scale over the extent; the ends map to the ends of the scheme
        Assert.Equal(ScaleKind.Sequential, scale.Kind);
        Assert.Equal("quantitative", scale.DataType);
        Assert.Equal((3.0, 12.0), (scale.DomainStart, scale.DomainEnd));
        Assert.Null(scale.Domain);
        Assert.Null(scale.NiceCount);
        Assert.NotNull(scale.Interpolator);
        Assert.Equal(ScaleKind.Sequential, model.Kind);
        Assert.Equal(Css(blues, 0), Text(model, 3));
        Assert.Equal(Css(blues, 1), Text(model, 12));

        // Assert: the position is the fraction of the domain: with the domain 0 to 10, 2.5 is 0.25, 5 is 0.5 and 10 is 1 (20 extrapolates, 20 / 10 = 2)
        var tenth = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10]}""")).CreateModel(0);
        Assert.Equal(Css(blues, 0.25), Text(tenth, 2.5));
        Assert.Equal(Css(blues, 0.5), Text(tenth, 5));
        Assert.Equal(Css(blues, 1), Text(tenth, 10));
        Assert.Equal(Css(blues, 2), Text(tenth, 20));
        Assert.Null(tenth.MapText(DataValue.FromString("n/a")));
        Assert.Null(tenth.MapText(DataValue.Null));

        // Assert: fill and stroke have their own sequential scales
        var paints = Resolve("point", """{ "fill": {"field":"q","type":"quantitative"}, "stroke": {"field":"r","type":"quantitative"} }""");
        Assert.Equal([ScaleKind.Sequential, ScaleKind.Sequential], new[] { paints["fill"]!.Kind, paints["stroke"]!.Kind });
        Assert.Equal((10.0, 35.0), (paints["stroke"]!.DomainStart, paints["stroke"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that the <c>rect</c> mark uses the <c>heatmap</c> range (the yellowgreenblue scheme) for a continuous color, and that a
    ///     <c>domainMid</c> makes the default range <c>diverging</c> (the blueorange scheme, reversed by its extent <c>[1, 0]</c>).
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Default_RectUsesTheHeatmapRangeAndDomainMidTheDivergingRange()
    {
        // Arrange / Act
        var heat = Scale("color", "rect", Enc("color", "quantitative", """{"domain":[0,10]}""")).CreateModel(0);
        var diverging = Scale("color", "point", Enc("color", "quantitative", """{"domainMid":7}"""));
        var model = diverging.CreateModel(0);
        var orange = Scheme("blueorange");

        // Assert: rect: yellowgreenblue
        var heatmap = Scheme("yellowgreenblue");
        Assert.Equal(Css(heatmap, 0), Text(heat, 0));
        Assert.Equal(Css(heatmap, 1), Text(heat, 10));

        // Assert: domainMid inserts the middle value; the ends (3 and 12) are the ends of the scheme read backwards (extent [1, 0]) and 7 is its middle
        Assert.Equal(ScaleKind.Diverging, diverging.Kind);
        Assert.Equal([3.0, 7, 12], diverging.Domain);
        Assert.Equal((3.0, 12.0), (diverging.DomainStart, diverging.DomainEnd));
        Assert.Equal(ScaleKind.Diverging, model.Kind);
        Assert.Equal(7, model.DomainMid, Precision);
        Assert.Equal(Css(orange, 1), Text(model, 3));
        Assert.Equal(Css(orange, 0.5), Text(model, 7));
        Assert.Equal(Css(orange, 0), Text(model, 12));

        // Assert: a middle value outside the domain goes before the first or after the last entry (Vega's configureDomain)
        Assert.Equal([3.0, 12, 20], Scale("color", "point", Enc("color", "quantitative", """{"domainMid":20}""")).Domain);
        Assert.Equal([-5.0, 3, 12], Scale("color", "point", Enc("color", "quantitative", """{"domainMid":-5}""")).Domain);

        // Assert: domainMid belongs to the color channels: a size scale ignores it
        var size = Scale("size", "point", Enc("size", "quantitative", """{"domainMid":7}"""));
        Assert.Equal(ScaleKind.Linear, size.Kind);
        Assert.Null(size.Domain);
    }

    /// <summary>
    ///     Validates that an explicit domain of three entries makes a diverging scale even without <c>domainMid</c> (with the <c>ramp</c> range, since only
    ///     <c>domainMid</c> selects <c>diverging</c>), that the two slopes follow d3, and that a longer domain is a located specification error.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Domain_ThreeEntriesAreDivergingAndMoreAreRefused()
    {
        // Arrange / Act: the domain 0, 5, 10 with the blues range
        var scale = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,5,10]}"""));
        var model = scale.CreateModel(0);
        var blues = Scheme("blues");

        // Assert: 5 is the middle, 2.5 a quarter, 10 the end (0.5 + 5 * 0.1 = 1)
        Assert.Equal(ScaleKind.Diverging, scale.Kind);
        Assert.Equal(Css(blues, 0.5), Text(model, 5));
        Assert.Equal(Css(blues, 0.25), Text(model, 2.5));
        Assert.Equal(Css(blues, 1), Text(model, 10));

        // Assert: an asymmetric domain 0, 10, 100: 5 is 0.5 + (5 - 10) * 0.05 = 0.25
        var asymmetric = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10,100]}""")).CreateModel(0);
        Assert.Equal(Css(blues, 0.25), Text(asymmetric, 5));

        // Assert: four entries would be a piecewise color scale, which is a specification error at the domain
        var ex = Assert.Throws<VegaLiteSpecException>(() => Resolve("point", Enc("color", "quantitative", """{"domain":[0,1,2,3]}""")));
        Assert.Equal("/encoding/color/scale/domain", ex.Location.Pointer);
        var withMid = Assert.Throws<VegaLiteSpecException>(() => Resolve("point", Enc("color", "quantitative", """{"domain":[0,1,2],"domainMid":1}""")));
        Assert.Equal("/encoding/color/scale/domain", withMid.Location.Pointer);
    }

    /// <summary>
    ///     Validates the named schemes and their <c>extent</c>: the colors at the two ends and in the middle of the domain are the samples of the
    ///     scheme at the ends and the middle of the extent, and the name's case does not matter.
    /// </summary>
    /// <param name="scheme">The scheme text (a string or an object).</param>
    /// <param name="name">The scheme name for the expected colors.</param>
    /// <param name="start">The position of the scheme at the start of the domain.</param>
    /// <param name="end">The position of the scheme at the end of the domain.</param>
    [Theory]
    [InlineData("\"viridis\"", "viridis", 0.0, 1.0)]
    [InlineData("\"VIRIDIS\"", "viridis", 0.0, 1.0)]
    [InlineData("{\"name\":\"magma\"}", "magma", 0.0, 1.0)]
    [InlineData("{\"name\":\"magma\",\"extent\":[0.2,0.8]}", "magma", 0.2, 0.8)]
    [InlineData("{\"name\":\"plasma\",\"extent\":[1,0]}", "plasma", 1.0, 0.0)]
    [InlineData("{\"name\":\"turbo\",\"count\":9}", "turbo", 0.0, 1.0)]
    public void ScaleResolverColor_Scheme_NamesObjectsAndExtents(string scheme, string name, double start, double end)
    {
        // Arrange / Act: the domain 0 to 10
        var model = Scale("color", "point", Enc("color", "quantitative", "{\"domain\":[0,10],\"scheme\":" + scheme + "}")).CreateModel(0);
        var expected = Scheme(name);

        // Assert
        Assert.Equal(Css(expected, start), Text(model, 0));
        Assert.Equal(Css(expected, start + (0.5 * (end - start))), Text(model, 5));
        Assert.Equal(Css(expected, end), Text(model, 10));
    }

    /// <summary>
    ///     Validates <c>reverse</c> (and the sort <c>descending</c>, which sets it for a scale with a continuous domain): the ramp runs backwards.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Reverse_FlipsTheRamp()
    {
        // Arrange
        var blues = Scheme("blues");

        // Act
        var reversed = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"reverse":true}""")).CreateModel(0);
        var byScheme = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"scheme":{"name":"blues","extent":[0.25,0.75]},"reverse":true}""")).CreateModel(0);
        var byColors = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"range":["#000000","#ffffff"],"reverse":true}""")).CreateModel(0);
        var bySort = Scale("color", "point", """{"color":{"field":"q","type":"quantitative","sort":"descending","scale":{"domain":[0,10]}}}""").CreateModel(0);
        var overridden = Scale("color", "point", """{"color":{"field":"q","type":"quantitative","sort":"descending","scale":{"domain":[0,10],"reverse":false}}}""").CreateModel(0);

        // Assert
        Assert.Equal(Css(blues, 1), Text(reversed, 0));
        Assert.Equal(Css(blues, 0), Text(reversed, 10));
        Assert.Equal(Css(blues, 0.75), Text(byScheme, 0));
        Assert.Equal(Css(blues, 0.25), Text(byScheme, 10));
        Assert.Equal("#ffffff", Text(byColors, 0));
        Assert.Equal("#000000", Text(byColors, 10));
        Assert.Equal(Css(blues, 1), Text(bySort, 0));
        Assert.Equal(Css(blues, 0), Text(overridden, 0));
    }

    /// <summary>
    ///     Validates an explicit range of colors: the colors are interpolated piecewise in sRGB, evenly spaced over the domain; a single color is constant; the range wins over a
    ///     scheme; an invalid color is a located specification error.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Range_ExplicitColorsInterpolatePiecewise()
    {
        // Arrange / Act: black to white over 0 to 10; red, lime, blue over 0 to 10
        var gray = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"range":["#000000","#ffffff"]}""")).CreateModel(0);
        var three = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"range":["red","lime","blue"]}""")).CreateModel(0);
        var single = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"range":["#123456"]}""")).CreateModel(0);
        var both = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"range":["#000000","#ffffff"],"scheme":"viridis"}""")).CreateModel(0);

        // Assert: the middle of black and white is 127.5, which rounds half away from zero to 128 (0x80)
        Assert.Equal("#000000", Text(gray, 0));
        Assert.Equal("#808080", Text(gray, 5));
        Assert.Equal("#ffffff", Text(gray, 10));

        // Assert: three colors: the middle is lime, a quarter is halfway from red to lime (127.5, 127.5, 0 -> 128, 128, 0), the end blue
        Assert.Equal("#ff0000", Text(three, 0));
        Assert.Equal("#808000", Text(three, 2.5));
        Assert.Equal("#00ff00", Text(three, 5));
        Assert.Equal("#0000ff", Text(three, 10));

        // Assert: one color is the same everywhere; the range wins over the scheme
        Assert.Equal("#123456", Text(single, 0));
        Assert.Equal("#123456", Text(single, 7));
        Assert.Equal("#808080", Text(both, 5));

        // Assert: an invalid color names the entry
        var ex = Assert.Throws<VegaLiteSpecException>(() => Resolve("point", Enc("color", "quantitative", """{"range":["#000000","not a color"]}""")));
        Assert.Equal("/encoding/color/scale/range/1", ex.Location.Pointer);
        Assert.Contains("not a color", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates <c>interpolate</c>: the color space of a list of colors (a name, or an object with a type and a gamma) changes the middle color, matching the
    ///     interpolators of <see cref="ColorInterpolator"/> (within one in each channel); a named scheme ignores it; cubehelix, which is not drawn, interpolates in sRGB.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Interpolate_ChoosesTheColorSpaceOfAList()
    {
        // Arrange
        var red = RgbColor.FromBytes(255, 0, 0);
        var blue = RgbColor.FromBytes(0, 0, 255);
        string Middle(string interpolate) => Text(
            Scale("color", "point", Enc("color", "quantitative", "{\"domain\":[0,10],\"range\":[\"red\",\"blue\"]" + interpolate + "}")).CreateModel(0),
            5)!;

        // Assert: the default is sRGB; the other spaces differ from it and equal the interpolators within one per channel
        Assert.Equal("#800080", Middle(string.Empty));
        Assert.Equal("#800080", Middle(",\"interpolate\":\"rgb\""));
        AssertNear(ColorInterpolator.Interpolate(InterpolationSpace.Hsl, red, blue, 0.5), Middle(",\"interpolate\":\"hsl\""));
        AssertNear(ColorInterpolator.Interpolate(InterpolationSpace.HslLong, red, blue, 0.5), Middle(",\"interpolate\":\"hsl-long\""));
        AssertNear(ColorInterpolator.Interpolate(InterpolationSpace.Lab, red, blue, 0.5), Middle(",\"interpolate\":\"lab\""));
        AssertNear(ColorInterpolator.Interpolate(InterpolationSpace.Hcl, red, blue, 0.5), Middle(",\"interpolate\":{\"type\":\"hcl\"}"));
        AssertNear(ColorInterpolator.Interpolate(InterpolationSpace.HclLong, red, blue, 0.5), Middle(",\"interpolate\":\"hcl-long\""));
        AssertNear(ColorInterpolator.Interpolate(InterpolationSpace.Rgb, red, blue, 0.5, 2), Middle(",\"interpolate\":{\"type\":\"rgb\",\"gamma\":2}"));
        Assert.NotEqual(Middle(string.Empty), Middle(",\"interpolate\":\"lab\""));
        Assert.NotEqual(Middle(string.Empty), Middle(",\"interpolate\":{\"type\":\"rgb\",\"gamma\":2}"));

        // Assert: cubehelix is accepted but interpolates in sRGB (the feature catalog reports it)
        Assert.Equal("#800080", Middle(",\"interpolate\":\"cubehelix\""));

        // Assert: a named scheme ignores interpolate, as Vega does
        var plain = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"scheme":"viridis"}""")).CreateModel(0);
        var withLab = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"scheme":"viridis","interpolate":"lab"}""")).CreateModel(0);
        Assert.Equal(Text(plain, 5), Text(withLab, 5));
    }

    /// <summary>
    ///     Validates that <c>config.range</c> names the default ranges: a list of colors, a scheme object (with <c>extent</c> and <c>count</c>), and
    ///     names of the configuration that the scale's own <c>range</c> refers to; and that a name the configuration lacks is a located specification error.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_ConfigRange_NamedRangesAreListsOrSchemes()
    {
        // Arrange / Act: ramp as a list of colors; ramp as a scheme with an extent; a scale range naming a configured list; rect with a configured heatmap
        var list = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10]}"""), Cfg("""{"range":{"ramp":["#000000","#ffffff"]}}""")).CreateModel(0);
        var extent = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10]}"""), Cfg("""{"range":{"ramp":{"scheme":"viridis","extent":[0,0.5]}}}""")).CreateModel(0);
        var named = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"range":"warm"}"""), Cfg("""{"range":{"warm":["#ff0000","#0000ff"]}}""")).CreateModel(0);
        var heat = Scale("color", "rect", Enc("color", "quantitative", """{"domain":[0,10]}"""), Cfg("""{"range":{"heatmap":["#00ff00","#000000"]}}""")).CreateModel(0);
        var scheme = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"range":"cool"}"""), Cfg("""{"range":{"cool":{"scheme":"magma","extent":[0.5,1]}}}""")).CreateModel(0);
        var diverging = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"domainMid":5}"""), Cfg("""{"range":{"diverging":["#ff0000","#ffffff","#0000ff"]}}""")).CreateModel(0);

        // Assert
        Assert.Equal("#808080", Text(list, 5));
        Assert.Equal(Css(Scheme("viridis"), 0), Text(extent, 0));
        Assert.Equal(Css(Scheme("viridis"), 0.5), Text(extent, 10));
        Assert.Equal("#ff0000", Text(named, 0));
        Assert.Equal("#0000ff", Text(named, 10));
        Assert.Equal("#00ff00", Text(heat, 0));
        Assert.Equal(Css(Scheme("magma"), 0.5), Text(scheme, 0));
        Assert.Equal(Css(Scheme("magma"), 1), Text(scheme, 10));
        Assert.Equal("#ffffff", Text(diverging, 5));

        // Assert: the configured categorical range of a nominal field follows the same lookup, and an unknown range name is an error at the range
        var ex = Assert.Throws<VegaLiteSpecException>(() => Resolve("point", Enc("color", "quantitative", """{"range":"nope"}""")));
        Assert.Equal("/encoding/color/scale/range", ex.Location.Pointer);
        Assert.Contains("'nope'", ex.Message, StringComparison.Ordinal);

        // Assert: Vega's built-in lookups are used when a configuration removes the defaults
        var removed = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10]}"""), Cfg("""{"range":{"ramp":null}}""")).CreateModel(0);
        Assert.Equal(Css(Scheme("blues"), 0), Text(removed, 0));
    }

    /// <summary>
    ///     Validates that an unknown scheme name is a located specification error (as Vega's "Unrecognized scheme name"): at the scale's scheme, at the name of a
    ///     scheme object, or, for a configured range, at the channel.
    /// </summary>
    /// <param name="channelScale">The scale object text, or empty.</param>
    /// <param name="extra">The configuration text, or empty.</param>
    /// <param name="type">The field type.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("{\"scheme\":\"nope\"}", "", "quantitative", "/encoding/color/scale/scheme")]
    [InlineData("{\"scheme\":{\"name\":\"nope\"}}", "", "quantitative", "/encoding/color/scale/scheme/name")]
    [InlineData("{\"scheme\":\"nope\"}", "", "nominal", "/encoding/color/scale/scheme")]
    [InlineData("{\"type\":\"quantize\",\"scheme\":\"nope\"}", "", "quantitative", "/encoding/color/scale/scheme")]
    [InlineData("", "\"config\":{\"range\":{\"ramp\":{\"scheme\":\"nope\"}}},", "quantitative", "/encoding/color")]
    [InlineData("{\"range\":\"mine\"}", "\"config\":{\"range\":{\"mine\":{\"scheme\":\"nope\"}}},", "quantitative", "/encoding/color/scale/range")]
    public void ScaleResolverColor_Scheme_UnknownNameIsASpecificationError(string channelScale, string extra, string type, string pointer)
    {
        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => Resolve("point", Enc("color", type, channelScale), extra));

        // Assert
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Contains("Unrecognized scheme name 'nope'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the order of precedence of the color sources: the scale's <c>range</c>, then its <c>scheme</c>, then the configured named range; and that a range
    ///     that is not a list of colors is skipped.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Precedence_RangeThenSchemeThenTheDefault()
    {
        // Arrange / Act
        var range = Scale("color", "point", Enc("color", "nominal", """{"range":["red","blue"],"scheme":"set1"}"""));
        var scheme = Scale("color", "point", Enc("color", "nominal", """{"scheme":"set1"}"""));
        var numbers = Scale("color", "point", Enc("color", "nominal", """{"range":[1,2],"scheme":"set1"}"""));
        var empty = Scale("color", "point", Enc("color", "nominal", """{"range":[]}"""));

        // Assert
        Assert.Equal(["red", "blue"], range.ColorRange);
        Assert.Equal(Scheme("set1").CssColors, scheme.ColorRange);
        Assert.Equal(Scheme("set1").CssColors, numbers.ColorRange);
        Assert.Equal(ColorSchemes.Tableau10, empty.ColorRange);
    }

    /// <summary>
    ///     Validates the colors of an ordinal scale from schemes: a categorical scheme is used whole (its <c>count</c> is ignored), a continuous one is sampled at <c>i / (n + 1)</c> for
    ///     the size of the domain (or the <c>count</c>), and <c>reverse</c> reverses the list.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Ordinal_SchemesAreWholeListsOrSamples()
    {
        // Arrange: three categories a, b, c
        var blues = Scheme("blues");

        // Act
        var whole = Scale("color", "point", Enc("color", "nominal", """{"scheme":"category10"}""", "k"));
        var counted = Scale("color", "point", Enc("color", "nominal", """{"scheme":{"name":"blues","count":5}}""", "k"));
        var sampled = Scale("color", "point", Enc("color", "nominal", """{"scheme":"blues"}""", "k"));
        var ignored = Scale("color", "point", Enc("color", "nominal", """{"scheme":{"name":"tableau10","count":2}}""", "k"));
        var reversed = Scale("color", "point", Enc("color", "nominal", """{"scheme":"set1","reverse":true}""", "k"));
        var model = sampled.CreateModel(0);

        // Assert
        Assert.Equal(Scheme("category10").CssColors, whole.ColorRange);
        Assert.Equal(ColorInterpolator.Quantize(blues.Ramp.At, 5, Ct).Select(c => c.ToCss()), counted.ColorRange);
        Assert.Equal(ColorInterpolator.Quantize(blues.Ramp.At, 3, Ct).Select(c => c.ToCss()), sampled.ColorRange);
        Assert.Equal(Css(blues, 0.25), model.MapOrdinal(DataValue.FromString("a")));
        Assert.Equal(Css(blues, 0.75), model.MapOrdinal(DataValue.FromString("c")));
        Assert.Equal(10, ignored.ColorRange.Count);
        Assert.Equal(Scheme("set1").CssColors.Reverse(), reversed.ColorRange);
    }

    /// <summary>
    ///     Validates the quantize scale: equal-width intervals over the data extent (the thresholds 3 + 9i/5 for five colors), five scheme colors by default (Vega's default
    ///     count, not <c>quantizeCount</c>), <c>scheme.count</c>, an explicit color range, and that <c>zero</c> applies while <c>domainMin</c> does not (Vega-Lite's scaleTypeSupportProperty).
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Quantize_UsesFiveSchemeColorsOverEqualIntervals()
    {
        // Arrange / Act
        var scale = Scale("color", "point", Enc("color", "quantitative", """{"type":"quantize"}"""));
        var model = scale.CreateModel(0);
        var three = Scale("color", "point", Enc("color", "quantitative", """{"type":"quantize","scheme":{"name":"blues","count":3}}""")).CreateModel(0);
        var explicitColors = Scale("color", "point", Enc("color", "quantitative", """{"type":"quantize","range":["red","lime"],"zero":true,"domainMin":1}""")).CreateModel(0);

        // Assert: five colors sampled from blues at i / 6; thresholds 4.8, 6.6, 8.4, 10.2 over the extent 3 to 12
        Assert.Equal(ScaleKind.Quantize, scale.Kind);
        Assert.Equal(ColorInterpolator.Quantize(Scheme("blues").Ramp.At, 5, Ct).Select(c => c.ToCss()), scale.ColorRange);
        Assert.Equal(4, model.Thresholds.Count);
        Assert.Equal(4.8, model.Thresholds[0], Precision);
        Assert.Equal(10.2, model.Thresholds[3], Precision);
        Assert.Equal(scale.ColorRange[0], Text(model, 3));
        Assert.Equal(scale.ColorRange[1], Text(model, 5));
        Assert.Equal(scale.ColorRange[4], Text(model, 12));

        // Assert: three colors cut the extent at 6 and 9; a value on a threshold takes the color above it
        var colors = Scale("color", "point", Enc("color", "quantitative", """{"type":"quantize","scheme":{"name":"blues","count":3}}""")).ColorRange;
        Assert.Equal([6.0, 9.0], three.Thresholds);
        Assert.Equal([colors[0], colors[1], colors[2]], new[] { 5.9, 6.0, 9.0 }.Select(v => Text(three, v)!).ToArray());

        // Assert: an explicit range uses its two colors; zero applies to a quantize scale (the extent starts at 0), domainMin does not (it is for continuous-to-continuous scales)
        Assert.Equal((0.0, 12.0), (explicitColors.DomainStart, explicitColors.DomainEnd));
        Assert.Equal([6.0], explicitColors.Thresholds);
        Assert.Equal(["red", "lime"], new[] { 3.0, 12.0 }.Select(v => Text(explicitColors, v)!).ToArray());
    }

    /// <summary>
    ///     Validates the quantile scale: the domain is all the valid values in ascending order (3, 5, 7, 12), the thresholds are the R-7 quantiles (4.5, 6 and 8.25 for four colors),
    ///     an explicit domain is sorted, and the scale has no <c>zero</c>.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Quantile_ThresholdsComeFromTheSortedValues()
    {
        // Arrange / Act
        var scale = Scale("color", "point", Enc("color", "quantitative", """{"type":"quantile","scheme":{"name":"blues","count":4}}"""));
        var model = scale.CreateModel(0);
        var five = Scale("color", "point", Enc("color", "quantitative", """{"type":"quantile"}""")).CreateModel(0);
        var explicitDomain = Scale("color", "point", Enc("color", "quantitative", """{"type":"quantile","domain":[40,10,30,20],"scheme":{"name":"blues","count":2},"zero":true}"""));

        // Assert: four groups of the values 3, 5, 7, 12: the quantiles at 0.25 (3 + 2 * 0.75), 0.5 (5 + 2 * 0.5) and 0.75 (7 + 5 * 0.25)
        Assert.Equal(ScaleKind.Quantile, scale.Kind);
        Assert.Equal([3.0, 5, 7, 12], scale.Domain);
        Assert.Equal((3.0, 12.0), (scale.DomainStart, scale.DomainEnd));
        Assert.Null(scale.NiceCount);
        Assert.Equal([4.5, 6.0, 8.25], model.Thresholds);
        Assert.Equal(4, scale.ColorRange.Count);
        Assert.Equal(
            [scale.ColorRange[0], scale.ColorRange[1], scale.ColorRange[2], scale.ColorRange[3]],
            new[] { 4.4, 4.5, 8.2, 8.25 }.Select(v => Text(model, v)!).ToArray());
        Assert.Equal(scale.ColorRange[0], Text(model, 3));
        Assert.Equal(scale.ColorRange[3], Text(model, 12));

        // Assert: five groups by default (the scheme count default), the quintiles 4.2, 5.4, 6.6 and 9
        var thresholds = five.Thresholds;
        Assert.Equal(4, thresholds.Count);
        Assert.Equal(4.2, thresholds[0], Precision);
        Assert.Equal(5.4, thresholds[1], Precision);
        Assert.Equal(6.6, thresholds[2], Precision);
        Assert.Equal(9, thresholds[3], Precision);

        // Assert: an explicit domain is sorted and kept as it is (no zero)
        Assert.Equal([10.0, 20, 30, 40], explicitDomain.Domain);
        Assert.Equal([25.0], explicitDomain.CreateModel(0).Thresholds);
    }

    /// <summary>
    ///     Validates the threshold scale: the explicit thresholds make one more color than thresholds, without a domain the extent of the data gives two thresholds (three colors),
    ///     and the scale has no <c>zero</c> and no <c>nice</c>.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Threshold_ExplicitThresholdsOrTheExtent()
    {
        // Arrange / Act
        var explicitScale = Scale("color", "point", Enc("color", "quantitative", """{"type":"threshold","domain":[5,10],"zero":true,"nice":true}"""));
        var extent = Scale("color", "point", Enc("color", "quantitative", """{"type":"threshold"}"""));
        var single = Scale("color", "point", Enc("color", "quantitative", """{"type":"threshold","domain":[7],"range":["red","blue"]}"""));

        // Assert: three colors sampled from blues (one more than the two thresholds); 5 is on the first threshold and goes up
        var colors = ColorInterpolator.Quantize(Scheme("blues").Ramp.At, 3, Ct).Select(c => c.ToCss()).ToArray();
        var model = explicitScale.CreateModel(0);
        Assert.Equal(ScaleKind.Threshold, explicitScale.Kind);
        Assert.Equal([5.0, 10], explicitScale.Domain);
        Assert.Null(explicitScale.NiceCount);
        Assert.Equal(colors, explicitScale.ColorRange);
        Assert.Equal([colors[0], colors[1], colors[1], colors[2]], new[] { 4.9, 5.0, 9.9, 10.0 }.Select(v => Text(model, v)!).ToArray());

        // Assert: without a domain the thresholds are the ends of the data (3 and 12)
        Assert.Equal([3.0, 12], extent.Domain);
        Assert.Equal(colors, extent.ColorRange);

        // Assert: one threshold needs two colors
        var one = single.CreateModel(0);
        Assert.Equal([7.0], single.Domain);
        Assert.Equal(["red", "blue"], new[] { 6.0, 7.0 }.Select(v => Text(one, v)!).ToArray());
    }

    /// <summary>
    ///     Validates <c>zero</c>, <c>nice</c>, <c>domainMin</c>, <c>domainMax</c> and <c>clamp</c> on a sequential scale: the zero rule is off for color unless asked for, an explicit
    ///     <c>nice</c> rounds the domain, the minimum and maximum replace the ends, and clamping keeps values outside the domain on the end colors.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Domain_ZeroNiceMinMaxAndClamp()
    {
        // Arrange / Act
        var zero = Scale("color", "point", Enc("color", "quantitative", """{"zero":true}"""));
        var nice = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0.2,9.7],"nice":true}""")).CreateModel(0);
        var limits = Scale("color", "point", Enc("color", "quantitative", """{"domainMin":1,"domainMax":20}"""));
        var clamped = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10],"clamp":true}""")).CreateModel(0);
        var free = Scale("color", "point", Enc("color", "quantitative", """{"domain":[0,10]}""")).CreateModel(0);

        // Assert: the data extent 3..12 becomes 0..12; nice rounds 0.2..9.7 to 0..10; minimum and maximum replace the ends (the data are 3..12)
        Assert.Equal((0.0, 12.0), (zero.DomainStart, zero.DomainEnd));
        Assert.Equal((0.0, 10.0), (nice.DomainStart, nice.DomainEnd));
        Assert.Equal((1.0, 20.0), (limits.DomainStart, limits.DomainEnd));

        // Assert: clamping keeps 20 on the end color; without it the color extrapolates
        Assert.Equal(Text(clamped, 10), Text(clamped, 20));
        Assert.Equal(Text(clamped, 0), Text(clamped, -10));
        Assert.NotEqual(Text(free, 10), Text(free, 20));
    }

    /// <summary>
    ///     Validates the kind of scale for every channel group, field type and explicit scale type against Vega-Lite's <c>defaultType</c>, <c>channelSupportScaleType</c> and
    ///     <c>scaleTypeSupportDataType</c>: an accepted type is used, a rejected one gives the default.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="scaleType">The explicit scale type, or empty.</param>
    /// <param name="expected">The expected kind.</param>
    [Theory]
    [InlineData("color", "quantitative", "", ScaleKind.Sequential)]
    [InlineData("color", "quantitative", "linear", ScaleKind.Sequential)]
    [InlineData("color", "quantitative", "sequential", ScaleKind.Sequential)]
    [InlineData("color", "quantitative", "quantize", ScaleKind.Quantize)]
    [InlineData("color", "quantitative", "quantile", ScaleKind.Quantile)]
    [InlineData("color", "quantitative", "threshold", ScaleKind.Threshold)]
    [InlineData("color", "quantitative", "ordinal", ScaleKind.Sequential)]
    [InlineData("color", "quantitative", "bin-ordinal", ScaleKind.Sequential)]
    [InlineData("color", "nominal", "", ScaleKind.Ordinal)]
    [InlineData("color", "nominal", "quantize", ScaleKind.Ordinal)]
    [InlineData("color", "nominal", "sequential", ScaleKind.Ordinal)]
    [InlineData("color", "nominal", "band", ScaleKind.Ordinal)]
    [InlineData("fill", "ordinal", "ordinal", ScaleKind.Ordinal)]
    [InlineData("stroke", "quantitative", "quantize", ScaleKind.Quantize)]
    [InlineData("size", "quantitative", "", ScaleKind.Linear)]
    [InlineData("size", "quantitative", "quantize", ScaleKind.Quantize)]
    [InlineData("size", "quantitative", "sequential", ScaleKind.Linear)]
    [InlineData("size", "nominal", "", ScaleKind.Point)]
    [InlineData("size", "nominal", "ordinal", ScaleKind.Ordinal)]
    [InlineData("size", "nominal", "band", ScaleKind.Band)]
    [InlineData("opacity", "quantitative", "threshold", ScaleKind.Threshold)]
    [InlineData("opacity", "ordinal", "", ScaleKind.Point)]
    [InlineData("strokeWidth", "quantitative", "", ScaleKind.Linear)]
    [InlineData("strokeWidth", "nominal", "", ScaleKind.Point)]
    [InlineData("fillOpacity", "quantitative", "quantile", ScaleKind.Quantile)]
    [InlineData("strokeOpacity", "quantitative", "", ScaleKind.Linear)]
    [InlineData("angle", "quantitative", "", ScaleKind.Linear)]
    [InlineData("shape", "nominal", "", ScaleKind.Ordinal)]
    [InlineData("shape", "ordinal", "", ScaleKind.Ordinal)]
    [InlineData("shape", "quantitative", "", ScaleKind.Ordinal)]
    [InlineData("shape", "quantitative", "quantize", ScaleKind.Quantize)]
    [InlineData("shape", "nominal", "point", ScaleKind.Ordinal)]
    [InlineData("strokeDash", "ordinal", "", ScaleKind.Ordinal)]
    [InlineData("strokeDash", "quantitative", "threshold", ScaleKind.Threshold)]
    [InlineData("strokeDash", "quantitative", "linear", ScaleKind.Ordinal)]
    public void ScaleResolverColor_Kind_FollowsTheVegaLiteTypeRules(string channel, string type, string scaleType, object expected)
    {
        // Arrange: a nominal or ordinal field reads k, a quantitative one q
        var field = type == "quantitative" ? "q" : "k";
        var scale = scaleType.Length > 0 ? "{\"type\":\"" + scaleType + "\"}" : string.Empty;

        // Act
        var resolved = Scale(channel, "point", Enc(channel, type, scale, field));

        // Assert
        Assert.Equal((ScaleKind)expected, resolved.Kind);
        Assert.Equal(channel, resolved.Channel);
    }

    /// <summary>
    ///     Validates the answer that the feature catalog uses to tell whether Vega-Lite accepts an explicit scale type for a channel and a field type.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="scaleType">The scale type.</param>
    /// <param name="expected">Whether the type is accepted.</param>
    [Theory]
    [InlineData("x", "quantitative", "linear", true)]
    [InlineData("x", "quantitative", "quantize", false)]
    [InlineData("x", "nominal", "band", true)]
    [InlineData("x", "nominal", "ordinal", false)]
    [InlineData("color", "quantitative", "quantize", true)]
    [InlineData("color", "quantitative", "sequential", true)]
    [InlineData("color", "nominal", "quantize", false)]
    [InlineData("color", "nominal", "ordinal", true)]
    [InlineData("size", "nominal", "ordinal", true)]
    [InlineData("size", "quantitative", "ordinal", false)]
    [InlineData("shape", "quantitative", "threshold", true)]
    [InlineData("shape", "nominal", "point", false)]
    [InlineData("strokeDash", "nominal", "ordinal", true)]
    [InlineData("color", "quantitative", "log", true)]
    [InlineData("color", "nominal", "log", false)]
    [InlineData("x", "quantitative", "symlog", true)]
    [InlineData("size", "quantitative", "sqrt", true)]
    [InlineData("shape", "quantitative", "pow", false)]
    [InlineData("color", "quantitative", "bin-ordinal", false)]
    public void ScaleResolverColor_AcceptsScaleType_AppliesChannelAndFieldRules(string channel, string type, string scaleType, bool expected)
    {
        // Act / Assert
        Assert.Equal(expected, ScaleResolver.AcceptsScaleType(channel, type, scaleType));
    }

    /// <summary>
    ///     Validates that the bin-ordinal scale type is accepted for a binned quantitative field of a color channel only: not without a bin (there are no bins to
    ///     place values in), not for a nominal field (which reads the text of its bin), and not for the other channels, which Vega-Lite does not give it.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="binned">Whether the encoding bins the field.</param>
    /// <param name="expected">Whether the type is accepted.</param>
    [Theory]
    [InlineData("color", "quantitative", true, true)]
    [InlineData("fill", "quantitative", true, true)]
    [InlineData("stroke", "quantitative", true, true)]
    [InlineData("color", "quantitative", false, false)]
    [InlineData("color", "nominal", true, false)]
    [InlineData("color", "ordinal", true, false)]
    [InlineData("size", "quantitative", true, false)]
    [InlineData("opacity", "quantitative", true, false)]
    [InlineData("shape", "quantitative", true, false)]
    [InlineData("x", "quantitative", true, false)]
    public void ScaleResolverColor_AcceptsScaleType_BinOrdinalNeedsABinnedQuantitativeColor(string channel, string type, bool binned, bool expected)
    {
        // Act / Assert
        Assert.Equal(expected, ScaleResolver.AcceptsScaleType(channel, type, "bin-ordinal", binned));
    }

    /// <summary>
    ///     Validates the argument checks of the scale type question and of the per-mark channel rule.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_AcceptsScaleType_RejectsNullArguments()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.AcceptsScaleType(null!, "quantitative", "linear"));
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.AcceptsScaleType("x", null!, "linear"));
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.AcceptsScaleType("x", "quantitative", null!));
    }

    /// <summary>
    ///     Validates that a channel has a scale only for the marks that use it (Vega-Lite's <c>supportMark</c>): <c>shape</c> on <c>point</c>, <c>angle</c> on
    ///     <c>point</c>, <c>circle</c> and <c>square</c>, <c>strokeDash</c> on every mark but <c>rect</c>, <c>size</c> on every mark but <c>rect</c>, and the
    ///     opacities and <c>strokeWidth</c> on all.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="hasScale">Whether the channel has a scale.</param>
    [Theory]
    [InlineData("point", "shape", true)]
    [InlineData("circle", "shape", false)]
    [InlineData("square", "shape", false)]
    [InlineData("bar", "shape", false)]
    [InlineData("point", "angle", true)]
    [InlineData("circle", "angle", true)]
    [InlineData("square", "angle", true)]
    [InlineData("bar", "angle", false)]
    [InlineData("tick", "angle", false)]
    [InlineData("rect", "angle", false)]
    [InlineData("rule", "angle", false)]
    [InlineData("point", "strokeDash", true)]
    [InlineData("bar", "strokeDash", true)]
    [InlineData("tick", "strokeDash", true)]
    [InlineData("rule", "strokeDash", true)]
    [InlineData("circle", "strokeDash", true)]
    [InlineData("square", "strokeDash", true)]
    [InlineData("rect", "strokeDash", false)]
    [InlineData("rect", "size", false)]
    [InlineData("bar", "size", true)]
    [InlineData("rect", "strokeWidth", true)]
    [InlineData("rect", "fillOpacity", true)]
    [InlineData("rule", "strokeOpacity", true)]
    public void ScaleResolverColor_Channels_AreDroppedForMarksThatDoNotUseThem(string mark, string channel, bool hasScale)
    {
        // Arrange: shape and strokeDash read the category field, the others the numbers
        var isDiscrete = channel is "shape" or "strokeDash";

        // Act
        var scales = Resolve(mark, Enc(channel, isDiscrete ? "nominal" : "quantitative", string.Empty, isDiscrete ? "k" : "q"));

        // Assert
        Assert.Equal(hasScale, scales[channel] is not null);
        Assert.Equal(hasScale, ScaleResolver.ChannelIsDrawn(channel, mark));
    }

    /// <summary>
    ///     Validates the default numeric ranges of the new channels over the data extent 3 to 12 (no zero): <c>strokeWidth</c> from 1 to 4, the opacities from 0.3 to 0.8 and
    ///     <c>angle</c> from 0 to 360 degrees, their configuration (<c>minStrokeWidth</c>, <c>maxStrokeWidth</c>, <c>minOpacity</c>, <c>maxOpacity</c>), and a nominal field as points.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_NewChannels_UseTheirDefaultRanges()
    {
        // Arrange / Act
        var all = Resolve(
            "point",
            """{ "strokeWidth": {"field":"q","type":"quantitative"}, "fillOpacity": {"field":"q","type":"quantitative"}, "strokeOpacity": {"field":"q","type":"quantitative"}, "angle": {"field":"q","type":"quantitative"} }""");
        var configured = Resolve(
            "point",
            """{ "strokeWidth": {"field":"q","type":"quantitative"}, "fillOpacity": {"field":"q","type":"quantitative"} }""",
            Cfg("""{"scale":{"minStrokeWidth":2,"maxStrokeWidth":8,"minOpacity":0.1,"maxOpacity":0.9}}"""));
        var points = Resolve("point", Enc("strokeWidth", "nominal", string.Empty, "k"));

        // Assert: strokeWidth 1 to 4: the middle of 3..12 (7.5) is 2.5
        var width = all["strokeWidth"]!.CreateModel(0);
        Assert.Equal(1, width.Map(3), Precision);
        Assert.Equal(2.5, width.Map(7.5), Precision);
        Assert.Equal(4, width.Map(12), Precision);
        Assert.Equal((3.0, 12.0), (all["strokeWidth"]!.DomainStart, all["strokeWidth"]!.DomainEnd));

        // Assert: opacities 0.3 to 0.8 (the middle is 0.55), angle 0 to 360 (the middle is 180)
        Assert.Equal(0.3, all["fillOpacity"]!.CreateModel(0).Map(3), Precision);
        Assert.Equal(0.55, all["strokeOpacity"]!.CreateModel(0).Map(7.5), Precision);
        Assert.Equal(0.8, all["strokeOpacity"]!.CreateModel(0).Map(12), Precision);
        Assert.Equal(0, all["angle"]!.CreateModel(0).Map(3), Precision);
        Assert.Equal(180, all["angle"]!.CreateModel(0).Map(7.5), Precision);
        Assert.Equal(360, all["angle"]!.CreateModel(0).Map(12), Precision);

        // Assert: the configuration changes the ends
        Assert.Equal(2, configured["strokeWidth"]!.CreateModel(0).Map(3), Precision);
        Assert.Equal(8, configured["strokeWidth"]!.CreateModel(0).Map(12), Precision);
        Assert.Equal(0.1, configured["fillOpacity"]!.CreateModel(0).Map(3), Precision);
        Assert.Equal(0.9, configured["fillOpacity"]!.CreateModel(0).Map(12), Precision);

        // Assert: three categories are points over 1 to 4 (1, 2.5, 4)
        var categories = points["strokeWidth"]!.CreateModel(0);
        Assert.Equal(ScaleKind.Point, categories.Kind);
        Assert.Equal(
            [1.0, 2.5, 4.0],
            new[] { "a", "b", "c" }.Select(c => categories.Map(DataValue.FromString(c))!.Value).ToArray());
    }

    /// <summary>
    ///     Validates the shape range: the eight symbols of <c>config.range.symbol</c> in order for the categories (cycling), an explicit range of names or path strings, a configured
    ///     symbol list, <c>reverse</c>, a discretizing scale on a quantitative field, and a located error for a range that is not a list of strings.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Shape_RangeIsTheSymbolList()
    {
        // Arrange / Act
        var plain = Scale("shape", "point", Enc("shape", "nominal", string.Empty, "k"));
        var explicitRange = Scale("shape", "point", Enc("shape", "nominal", """{"range":["cross","M0,0L1,1"]}""", "k"));
        var configured = Scale("shape", "point", Enc("shape", "nominal", string.Empty, "k"), Cfg("""{"range":{"symbol":["diamond","square"]}}"""));
        var reversed = Scale("shape", "point", Enc("shape", "nominal", """{"range":["cross","diamond","circle"],"reverse":true}""", "k"));
        var quantized = Scale("shape", "point", Enc("shape", "quantitative", """{"type":"quantize"}"""));
        var model = plain.CreateModel(0);

        // Assert: the eight symbols; the categories a, b, c get circle, square, triangle-up
        Assert.Equal(["circle", "square", "triangle-up", "cross", "diamond", "triangle-right", "triangle-down", "triangle-left"], plain.ShapeRange);
        Assert.Equal(["circle", "square", "triangle-up"], new[] { "a", "b", "c" }.Select(c => model.MapOrdinal(DataValue.FromString(c))).ToArray());
        Assert.Equal(["cross", "M0,0L1,1"], explicitRange.ShapeRange);
        Assert.Equal(["cross", "M0,0L1,1", "cross"], new[] { "a", "b", "c" }.Select(c => explicitRange.CreateModel(0).MapOrdinal(DataValue.FromString(c))).ToArray());
        Assert.Equal(["diamond", "square"], configured.ShapeRange);
        Assert.Equal(["circle", "diamond", "cross"], reversed.ShapeRange);

        // Assert: a quantitative field with quantize cuts 3 to 12 into eight intervals (seven thresholds), one symbol each
        var quantizedModel = quantized.CreateModel(0);
        Assert.Equal(ScaleKind.Quantize, quantized.Kind);
        Assert.Equal(7, quantizedModel.Thresholds.Count);
        Assert.Equal("circle", Text(quantizedModel, 3));
        Assert.Equal("triangle-left", Text(quantizedModel, 12));

        // Assert: a range that holds something other than a string
        var ex = Assert.Throws<VegaLiteSpecException>(() => Resolve("point", Enc("shape", "nominal", """{"range":["circle",5]}""", "k")));
        Assert.Equal("/encoding/shape/scale/range/1", ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates the stroke dash range: Vega-Lite's five patterns, the index of each category, an explicit list of lists of numbers, and a located error for a range that is not one.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_StrokeDash_RangeIsFivePatterns()
    {
        // Arrange / Act
        var plain = Scale("strokeDash", "point", Enc("strokeDash", "nominal", string.Empty, "k"));
        var custom = Scale("strokeDash", "point", Enc("strokeDash", "nominal", """{"range":[[1,2],[3,4,5]]}""", "k"));
        var reversed = Scale("strokeDash", "point", Enc("strokeDash", "nominal", """{"range":[[1,2],[3,4,5]],"reverse":true}""", "k"));
        var model = custom.CreateModel(0);

        // Assert
        Assert.Equal(FiveDashes, plain.DashRange.Select(d => d.ToArray()).ToArray());
        Assert.Equal([0, 1, 2], new[] { "a", "b", "c" }.Select(c => plain.CreateModel(0).MapIndex(DataValue.FromString(c))).ToArray());
        Assert.Equal([[1.0, 2], [3, 4, 5]], custom.DashRange.Select(d => d.ToArray()).ToArray());
        Assert.Equal([0, 1, 0], new[] { "a", "b", "c" }.Select(c => model.MapIndex(DataValue.FromString(c))).ToArray());
        Assert.Equal(-1, model.MapIndex(DataValue.FromString("zzz")));
        Assert.Equal([[3.0, 4, 5], [1, 2]], reversed.DashRange.Select(d => d.ToArray()).ToArray());

        // Assert: an entry that is not a list of numbers
        var ex = Assert.Throws<VegaLiteSpecException>(() => Resolve("point", Enc("strokeDash", "nominal", """{"range":[[1,2],"solid"]}""", "k")));
        Assert.Equal("/encoding/strokeDash/scale/range/1", ex.Location.Pointer);
        Assert.Throws<VegaLiteSpecException>(() => Resolve("point", Enc("strokeDash", "nominal", """{"range":[[1,"x"]]}""", "k")));
    }

    /// <summary>
    ///     Validates the numbers of a discretizing scale on <c>size</c> (Vega-Lite's <c>interpolateRange</c>): four evenly spaced sizes by default (<c>quantizeCount</c>), the count
    ///     from <c>quantileCount</c>, one more than the thresholds for a threshold scale (three without a domain), the two-entry range of the other numeric channels, an explicit
    ///     range, <c>reverse</c>, and an ordinal scale cycling through the two sizes.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_DiscretizingNumbers_SpreadTheRangeEvenly()
    {
        // Arrange / Act: sizes 4 to 361 (0.95 * 20 = 19, squared)
        var quantize = Scale("size", "point", Enc("size", "quantitative", """{"type":"quantize"}"""));
        var quantile = Scale("size", "point", Enc("size", "quantitative", """{"type":"quantile"}"""), Cfg("""{"scale":{"quantileCount":3}}"""));
        var threshold = Scale("size", "point", Enc("size", "quantitative", """{"type":"threshold","domain":[5,10]}"""));
        var open = Scale("size", "point", Enc("size", "quantitative", """{"type":"threshold"}"""));
        var opacity = Scale("opacity", "point", Enc("opacity", "quantitative", """{"type":"quantize"}"""));
        var explicitRange = Scale("size", "point", Enc("size", "quantitative", """{"type":"quantize","range":[1,2,3]}"""));
        var reversed = Scale("size", "point", Enc("size", "quantitative", """{"type":"quantize","range":[1,2,3],"reverse":true}"""));
        var ordinal = Scale("size", "point", Enc("size", "nominal", """{"type":"ordinal"}""", "k"));

        // Assert: four sizes 4, 123, 242, 361 (step 119); the extent 3..12 has no zero for a discretizing scale
        Assert.Equal(4, quantize.NumberRange.Count);
        Assert.Equal(4, quantize.NumberRange[0], Precision);
        Assert.Equal(123, quantize.NumberRange[1], Precision);
        Assert.Equal(242, quantize.NumberRange[2], Precision);
        Assert.Equal(361, quantize.NumberRange[3], Precision);
        Assert.Equal((3.0, 12.0), (quantize.DomainStart, quantize.DomainEnd));
        Assert.Equal(123, quantize.CreateModel(0).Map(DataValue.FromNumber(6))!.Value, Precision);

        // Assert: three sizes 4, 182.5, 361 for quantileCount 3, for two thresholds, and for the extent as thresholds
        Assert.Equal([4.0, 182.5, 361], quantile.NumberRange.Select(n => Math.Round(n, Precision)).ToArray());
        Assert.Equal([4.0, 182.5, 361], threshold.NumberRange.Select(n => Math.Round(n, Precision)).ToArray());
        Assert.Equal(3, open.NumberRange.Count);

        // Assert: only size is spread: opacity keeps its two values; an explicit range is used as it is, reversed on request
        Assert.Equal([0.3, 0.8], opacity.NumberRange);
        Assert.Equal([1.0, 2, 3], explicitRange.NumberRange);
        Assert.Equal([3.0, 2, 1], reversed.NumberRange);

        // Assert: an ordinal scale gives the two sizes by turns
        var model = ordinal.CreateModel(0);
        Assert.Equal(ScaleKind.Ordinal, ordinal.Kind);
        Assert.Equal(
            [4.0, 361, 4],
            new[] { "a", "b", "c" }.Select(c => Math.Round(model.Map(DataValue.FromString(c))!.Value, Precision)).ToArray());
    }

    /// <summary>
    ///     Validates <c>rangeMin</c> and <c>rangeMax</c>: they replace the first and the second entry of a default two-entry range (a size range, a position range), not of an explicit
    ///     range, not of a range that a step sizes, and not on a discretizing scale.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_RangeMinAndMax_ReplaceTheEntriesOfADefaultRange()
    {
        // Arrange / Act: sizes 4..361 with the domain 0..10 and the minimum 10 and the maximum 50
        var size = Scale("size", "point", Enc("size", "quantitative", """{"domain":[0,10],"rangeMin":10,"rangeMax":50}""")).CreateModel(0);
        var x = Scale("x", "point", Enc("x", "quantitative", """{"domain":[0,10],"nice":false,"rangeMin":20,"rangeMax":120}""")).CreateModel(300);
        var y = Scale("y", "point", Enc("y", "quantitative", """{"domain":[0,10],"nice":false,"rangeMax":50}""")).CreateModel(300);
        var explicitRange = Scale("size", "point", Enc("size", "quantitative", """{"domain":[0,10],"range":[1,2],"rangeMin":10}""")).CreateModel(0);
        var band = Scale("x", "bar", Enc("x", "nominal", """{"rangeMin":10}""", "k"), "\"width\": 100,");
        var stepped = Scale("x", "bar", Enc("x", "nominal", """{"rangeMin":10}""", "k"));
        var discretizing = Scale("size", "point", Enc("size", "quantitative", """{"type":"quantize","rangeMin":10}"""));

        // Assert
        Assert.Equal(10, size.Map(0), Precision);
        Assert.Equal(50, size.Map(10), Precision);
        Assert.Equal(20, x.Map(0), Precision);
        Assert.Equal(120, x.Map(10), Precision);

        // Assert: y runs from the bottom (300) up to the maximum 50: the first entry stays the height, the second is replaced by rangeMax
        Assert.Equal(300, y.Map(0), Precision);
        Assert.Equal(50, y.Map(10), Precision);

        // Assert: an explicit range is not changed; a band scale of a fixed width starts at the minimum; a step-sized one ignores it; a discretizing scale has none
        Assert.Equal(1, explicitRange.Map(0), Precision);
        Assert.Equal(2, explicitRange.Map(10), Precision);
        Assert.Equal(10, band.CreateModel(100).RangeStart, Precision);
        Assert.Equal(0, stepped.CreateModel(60).RangeStart, Precision);
        Assert.Equal(4, discretizing.NumberRange[0], Precision);
    }

    /// <summary>
    ///     Validates the properties that a scale type or channel does not support: <c>scheme</c> and <c>interpolate</c> on a non-color channel, and <c>domainMin</c> and
    ///     <c>domainMax</c> on a quantize scale, are ignored.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_UnsupportedProperties_AreIgnored()
    {
        // Arrange / Act
        var size = Scale("size", "point", Enc("size", "quantitative", """{"scheme":"blues","interpolate":"lab"}"""));
        var quantize = Scale("color", "point", Enc("color", "quantitative", """{"type":"quantize","domainMin":0,"domainMax":100,"clamp":true,"nice":true}"""));

        // Assert: the size scale is plain linear; the quantize domain is the extent
        Assert.Equal(ScaleKind.Linear, size.Kind);
        Assert.Empty(size.ColorRange);
        Assert.Null(size.Interpolator);
        Assert.Equal((3.0, 12.0), (quantize.DomainStart, quantize.DomainEnd));
        Assert.False(quantize.Clamp);
        Assert.NotNull(quantize.NiceCount);
    }

    /// <summary>
    ///     Validates the limits of the color and range work: a scheme count above <c>MaxScaleDomainSize</c>, a range, a domain or a generated range longer than it, each a located
    ///     <see cref="VegaLiteRenderException"/> whose message names the limit.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_Limits_AreEnforcedBeforeAnythingIsBuilt()
    {
        // Arrange
        var small = new VegaLiteLimits { MaxScaleDomainSize = 10 };

        // Act
        var ordinal = Assert.Throws<VegaLiteRenderException>(() => Resolve("point", Enc("color", "nominal", """{"scheme":{"name":"blues","count":11}}""", "k"), string.Empty, small));
        var quantize = Assert.Throws<VegaLiteRenderException>(() => Resolve("point", Enc("color", "quantitative", """{"type":"quantize","scheme":{"name":"blues","count":11}}"""), string.Empty, small));
        var range = Assert.Throws<VegaLiteRenderException>(() => Resolve("point", Enc("color", "quantitative", """{"range":["red","blue","lime"]}"""), string.Empty, new VegaLiteLimits { MaxScaleDomainSize = 2 }));
        var domain = Assert.Throws<VegaLiteRenderException>(() => Resolve("point", Enc("x", "quantitative", """{"domain":[1,2,3]}"""), string.Empty, new VegaLiteLimits { MaxScaleDomainSize = 2 }));
        var count = Assert.Throws<VegaLiteRenderException>(() => Resolve("point", Enc("size", "quantitative", """{"type":"quantize"}"""), Cfg("""{"scale":{"quantizeCount":1000}}"""), small));
        var thresholds = Assert.Throws<VegaLiteRenderException>(() => Resolve("point", Enc("color", "quantitative", """{"type":"threshold","domain":[1,2,3]}"""), string.Empty, new VegaLiteLimits { MaxScaleDomainSize = 2 }));
        var fine = Resolve("point", Enc("color", "nominal", """{"scheme":{"name":"blues","count":10}}""", "k"), string.Empty, small);

        // Assert: the pointers and the limit names
        Assert.Equal("/encoding/color/scale/scheme/name", ordinal.Location.Pointer);
        Assert.Equal("/encoding/color/scale/scheme/name", quantize.Location.Pointer);
        Assert.Equal("/encoding/color/scale/range", range.Location.Pointer);
        Assert.Equal("/encoding/x/scale/domain", domain.Location.Pointer);
        Assert.Equal("/encoding/size", count.Location.Pointer);
        Assert.Equal("/encoding/color/scale/domain", thresholds.Location.Pointer);
        foreach (var failure in new[] { ordinal, quantize, range, domain, count, thresholds })
        {
            Assert.Contains("MaxScaleDomainSize", failure.Message, StringComparison.Ordinal);
            Assert.Contains("the limit of", failure.Message, StringComparison.Ordinal);
        }

        Assert.Equal(10, fine["color"]!.ColorRange.Count);
    }

    /// <summary>
    ///     Validates the quantile domain on many values given in descending order: the sort is ascending and complete (the median of 1 to 5000 is 2500.5), the work is charged to the evaluation-step
    ///     budget (a failure names <c>MaxEvaluationSteps</c> and the channel), and a canceled token stops the read.
    /// </summary>
    [Fact]
    public void ScaleResolverColor_QuantileDomain_IsSortedChargedAndCancellable()
    {
        // Arrange: the values 5000 down to 1
        const string Source = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":5000,\"as\":\"id\"}},\"transform\":[{\"calculate\":\"5000 - datum.id\",\"as\":\"v\"}],\"mark\":\"point\",\"encoding\":{\"color\":{\"field\":\"v\",\"type\":\"quantitative\",\"scale\":{\"type\":\"quantile\",\"scheme\":{\"name\":\"blues\",\"count\":2}}}}}";

        // The same read over the sequence itself, which costs the transforms nothing
        const string Plain = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":5000,\"as\":\"id\"}},\"mark\":\"point\",\"encoding\":{\"color\":{\"field\":\"id\",\"type\":\"quantitative\",\"scale\":{\"type\":\"quantile\"}}}}";

        // Act
        var parts = ChartTestHelpers.ResolveFull(Source);
        var charged = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.ResolveFull(Plain, new VegaLiteLimits { MaxEvaluationSteps = 40 }));

        // Assert: complete and ascending; the median of 1..5000 by R-7 is 2500 + 0.5
        var scale = parts.Scales["color"]!;
        Assert.Equal(5000, scale.Domain!.Count);
        Assert.Equal((1.0, 5000.0), (scale.DomainStart, scale.DomainEnd));
        for (var i = 1; i < scale.Domain.Count; i++)
        {
            Assert.True(scale.Domain[i - 1] < scale.Domain[i]);
        }

        Assert.Equal(2500.5, scale.CreateModel(0).Thresholds[0], Precision);
        Assert.Equal("/encoding/color", charged.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", charged.Message, StringComparison.Ordinal);

        // Assert: a token that is already canceled stops the read of the values (they are polled every 4,096 rows)
        var rows = string.Join(",", Enumerable.Range(0, 9000).Select(i => "{\"v\":" + i + "}"));
        var big = SpecParser.Parse("{\"data\":{\"values\":[" + rows + "]},\"mark\":\"point\",\"encoding\":{\"color\":{\"field\":\"v\",\"type\":\"quantitative\",\"scale\":{\"type\":\"quantile\"}}}}");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            ScaleResolver.Resolve(big.Root, DataLoader.Load(big.Root["data"], null, null, Ct), ConfigResolver.Resolve(big, null), null, null, canceled.Token));
    }

    /// <summary>
    ///     Validates that resolving the scales of color specifications is deterministic: the same specification gives the same colors, thresholds and numbers on repeated and concurrent
    ///     runs.
    /// </summary>
    /// <returns>A task that completes when the concurrent resolutions are done.</returns>
    [Fact]
    public async Task ScaleResolverColor_Resolve_IsDeterministicAcrossRepeatedAndConcurrentRuns()
    {
        // Arrange
        string Summarize()
        {
            var scales = Resolve(
                "point",
                """{ "color": {"field":"q","type":"quantitative","scale":{"type":"quantile","scheme":{"name":"viridis","count":5}}}, "fill": {"field":"q","type":"quantitative","scale":{"scheme":"magma","interpolate":"lab"}}, "size": {"field":"q","type":"quantitative","scale":{"type":"quantize"}} }""");
            var color = scales["color"]!;
            var fill = scales["fill"]!.CreateModel(0);
            return string.Join(
                "|",
                string.Join(",", color.ColorRange),
                string.Join(",", color.CreateModel(0).Thresholds.Select(t => t.ToString("R", System.Globalization.CultureInfo.InvariantCulture))),
                string.Join(",", scales["size"]!.NumberRange.Select(t => t.ToString("R", System.Globalization.CultureInfo.InvariantCulture))),
                string.Join(",", new[] { 3.0, 4.5, 8, 12 }.Select(v => Text(fill, v))));
        }

        // Act
        var first = Summarize();
        var again = Summarize();
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Summarize, Ct)));

        // Assert
        Assert.Equal(first, again);
        Assert.All(concurrent, summary => Assert.Equal(first, summary));
    }
}
