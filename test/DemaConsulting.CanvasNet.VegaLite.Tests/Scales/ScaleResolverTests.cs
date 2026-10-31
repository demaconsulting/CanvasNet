using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the Vega-Lite scale inference: scale kinds, domains, zero, nice, padding, ranges, steps and colors, with
///     numbers derived by hand from the Vega-Lite 6 rules and the d3 formulas.
/// </summary>
public class ScaleResolverTests
{
    /// <summary>The tolerance for numbers computed with fractions.</summary>
    private const int Precision = 9;

    /// <summary>The data of most tests: three categories (with a repeat) and numbers.</summary>
    private const string SampleData = """
        "data": { "values": [
          { "k": "b", "q": 3, "r": 10 },
          { "k": "a", "q": 7, "r": 20 },
          { "k": "c", "q": 12, "r": 35 },
          { "k": "a", "q": 5, "r": 15 }
        ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Resolves the scales of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The resolved scales.</returns>
    private static ResolvedScales Resolve(string json, VegaLiteLimits? limits = null)
    {
        var tree = SpecParser.Parse(json);
        var table = DataLoader.Load(tree.Root["data"], tree.SourceName, limits, Ct);
        return ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), tree.SourceName, limits, Ct);
    }

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
    ///     Creates a string category.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    private static DataValue S(string text) => DataValue.FromString(text);

    /// <summary>
    ///     Validates the scale kind that Vega-Lite infers for quantitative, nominal and ordinal fields on every channel
    ///     and mark, including explicit scale types, channels without a scale and fields that are not drawn.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_InfersQuantitativeNominalOrdinal()
    {
        // Arrange / Act: a bar chart with a nominal x, a quantitative y and a nominal color
        var bar = Resolve(Spec(
            "bar",
            """{ "x": {"field":"k","type":"nominal"}, "y": {"field":"q","type":"quantitative"}, "color": {"field":"k","type":"nominal"} }"""));

        // Assert: nominal x of a bar is a band, quantitative y is linear, nominal color is ordinal
        Assert.Equal("bar", bar.MarkType);
        Assert.Equal(ScaleKind.Band, bar["x"]!.Kind);
        Assert.Equal("k", bar["x"]!.FieldPath);
        Assert.Equal("nominal", bar["x"]!.DataType);
        Assert.Equal(ScaleKind.Linear, bar["y"]!.Kind);
        Assert.Equal("quantitative", bar["y"]!.DataType);
        Assert.Equal(ScaleKind.Ordinal, bar["color"]!.Kind);
        Assert.Equal("/encoding/x", bar["x"]!.Location.Pointer);
        Assert.Equal(["x", "y", "color"], bar.Scales.Select(s => s.Channel).ToArray());
        Assert.Null(bar["size"]);

        // Assert: a discrete x is a band for the marks that must fit a band (rect, bar, rule, tick) and a point otherwise
        var kinds = new[] { "bar", "rect", "rule", "tick", "point", "circle", "square" }
            .Select(mark => (mark, Resolve(Spec(mark, """{ "x": {"field":"k","type":"nominal"} }"""))["x"]!.Kind))
            .ToArray();
        Assert.Equal(
            [
                ("bar", ScaleKind.Band),
                ("rect", ScaleKind.Band),
                ("rule", ScaleKind.Band),
                ("tick", ScaleKind.Band),
                ("point", ScaleKind.Point),
                ("circle", ScaleKind.Point),
                ("square", ScaleKind.Point),
            ],
            kinds);

        // Assert: an ordinal field is discrete like a nominal one, and it keeps its type for the color range
        var ordinal = Resolve(Spec("point", """{ "x": {"field":"r","type":"ordinal"} }"""));
        Assert.Equal(ScaleKind.Point, ordinal["x"]!.Kind);
        Assert.Equal("ordinal", ordinal["x"]!.DataType);

        // Assert: size and opacity follow the field type: linear for numbers, point for categories
        var sizes = Resolve(Spec("point", """{ "size": {"field":"q","type":"quantitative"}, "opacity": {"field":"q","type":"quantitative"} }"""));
        Assert.Equal(ScaleKind.Linear, sizes["size"]!.Kind);
        Assert.Equal(ScaleKind.Linear, sizes["opacity"]!.Kind);
        var categorySizes = Resolve(Spec("point", """{ "size": {"field":"k","type":"nominal"}, "opacity": {"field":"k","type":"ordinal"} }"""));
        Assert.Equal(ScaleKind.Point, categorySizes["size"]!.Kind);
        Assert.Equal(ScaleKind.Point, categorySizes["opacity"]!.Kind);

        // Assert: fill and stroke are separate color scales
        var paints = Resolve(Spec("point", """{ "fill": {"field":"k","type":"nominal"}, "stroke": {"field":"k","type":"nominal"} }"""));
        Assert.Equal(["fill", "stroke"], paints.Scales.Select(s => s.Channel).ToArray());

        // Assert: an explicit type counts when the channel and the field type accept it, otherwise the default applies
        Assert.Equal(ScaleKind.Point, Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal","scale":{"type":"point"}} }"""))["x"]!.Kind);
        Assert.Equal(ScaleKind.Band, Resolve(Spec("point", """{ "x": {"field":"k","type":"nominal","scale":{"type":"band"}} }"""))["x"]!.Kind);
        Assert.Equal(ScaleKind.Linear, Resolve(Spec("bar", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"band"}} }"""))["x"]!.Kind);
        Assert.Equal(ScaleKind.Band, Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal","scale":{"type":"linear"}} }"""))["x"]!.Kind);
        Assert.Equal(ScaleKind.Band, Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal","scale":{"type":"ordinal"}} }"""))["x"]!.Kind);
        Assert.Equal(ScaleKind.Linear, Resolve(Spec("bar", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log"}} }"""))["x"]!.Kind);

        // Assert: no scale for a temporal field, a value, a datum-less definition, "scale": null
        var none = Resolve(Spec(
            "point",
            """
            { "x": {"field":"q","type":"temporal"},
              "y": {"value": 20},
              "size": {"field":"q","type":"quantitative","scale":null},
              "opacity": {"type":"quantitative"} }
            """));
        Assert.Empty(none.Scales);

        // Assert: a quantitative color is a sequential scale (a continuous color ramp), its linear scale type too
        Assert.Equal(ScaleKind.Sequential, Resolve(Spec("point", """{ "color": {"field":"q","type":"quantitative"} }"""))["color"]!.Kind);
        Assert.Equal(ScaleKind.Sequential, Resolve(Spec("point", """{ "color": {"field":"q","type":"quantitative","scale":{"type":"linear"}} }"""))["color"]!.Kind);

        // Assert: an aggregate count has no field but is a field definition, so it has a scale (and the title Vega-Lite gives it)
        var counted = Resolve(Spec("point", """{ "opacity": {"aggregate":"count","type":"quantitative"} }"""));
        Assert.Equal(ScaleKind.Linear, counted["opacity"]!.Kind);
        Assert.Equal("__count", counted["opacity"]!.FieldPath);
        Assert.Equal("Count of Records", counted["opacity"]!.Title);

        // Assert: a field without a type is read as quantitative, a datum takes the type of its value
        var missingType = Resolve(Spec("point", """{ "x": {"field":"q"}, "y": {"datum": "label"} }"""));
        Assert.Equal("quantitative", missingType["x"]!.DataType);
        Assert.Equal("nominal", missingType["y"]!.DataType);
        Assert.Null(missingType["y"]!.FieldPath);

        // Assert: a view without a mark or encoding has no scales and no orientation
        Assert.Empty(Resolve("{ " + SampleData + " }").Scales);
        Assert.Empty(Resolve("{ \"mark\": \"bar\" }").Scales);
        Assert.Equal(string.Empty, Resolve("{ \"encoding\": {} }").MarkType);
    }

    /// <summary>
    ///     Validates the orientation Vega-Lite infers for bars, ticks and rules, which decides the zero rule and the
    ///     padding of continuous scales.
    /// </summary>
    [Fact]
    public void ScaleResolver_InferOrient_FollowsVegaLite()
    {
        // Arrange: (mark, specified orient, x type, y type, expected orient); null types mean the channel is absent
        var vectors = new (string Mark, string? Specified, string? X, string? Y, string? Expected)[]
        {
            // a bar extends along the measure
            ("bar", null, "nominal", "quantitative", "vertical"),
            ("bar", null, "quantitative", "nominal", "horizontal"),
            ("bar", null, "quantitative", "quantitative", "vertical"),
            ("bar", null, "nominal", "nominal", null),
            ("bar", "horizontal", "quantitative", "quantitative", "horizontal"),

            // a tick is the opposite
            ("tick", null, "quantitative", "nominal", "vertical"),
            ("tick", null, "nominal", "quantitative", "horizontal"),

            // a rule with one position channel runs across the view
            ("rule", null, "quantitative", null, "vertical"),
            ("rule", null, null, "quantitative", "horizontal"),

            // marks without a direction
            ("point", null, "quantitative", "quantitative", null),
            ("circle", null, "nominal", "quantitative", null),
            ("square", null, "nominal", "quantitative", null),
            ("rect", null, "nominal", "nominal", null),
        };

        // Act / Assert
        foreach (var (mark, specified, xType, yType, expected) in vectors)
        {
            var x = xType is null ? string.Empty : "\"x\": {\"field\":\"q\",\"type\":\"" + xType + "\"}";
            var y = yType is null ? string.Empty : "\"y\": {\"field\":\"q\",\"type\":\"" + yType + "\"}";
            var encoding = SpecParser.Parse("{" + string.Join(",", new[] { x, y }.Where(p => p.Length > 0)) + "}").Root;
            Assert.Equal(expected, ScaleResolver.InferOrient(mark, specified, encoding));
        }

        Assert.Null(ScaleResolver.InferOrient("bar", null, null));
        Assert.Null(ScaleResolver.InferOrient(string.Empty, null, null));
        Assert.Equal("vertical", Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal"}, "y": {"field":"q","type":"quantitative"} }""")).Orient);
    }

    /// <summary>
    ///     Validates zero, nice, explicit domains and ranges, pixel padding, reversal and step sizing against hand-derived
    ///     domains and positions.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_AppliesZeroNiceDomainRangeAndPadding()
    {
        // Arrange / Act: a bar chart; y is q in 3..12 (zero true for bars) and x has three categories
        var bars = Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal"}, "y": {"field":"q","type":"quantitative"} }"""));
        var y = bars["y"]!;
        var yModel = y.CreateModel(200);

        // Assert: zero pulls the start to 0, nice (count 10, step 1) keeps 12, the range runs from the bottom up
        Assert.Equal(0, y.DomainStart);
        Assert.Equal(12, y.DomainEnd);
        Assert.Equal(10, y.NiceCount);
        Assert.Equal(0, yModel.DomainStart);
        Assert.Equal(12, yModel.DomainEnd);
        Assert.Equal(100, yModel.Map(6), Precision);
        Assert.Equal(0, yModel.Map(12), Precision);
        Assert.Equal(200, yModel.Map(0), Precision);

        // Assert: the x band scale has a step of 20, three categories in order a, b, c, bar padding 0.1 / 0.05 and so
        // 20 * 3 = 60 pixels; the first band starts at (60 - 20 * 2.9) / 2 = 1 and is 18 wide
        var x = bars["x"]!;
        Assert.Equal(60, x.NaturalExtent!.Value, Precision);
        Assert.Equal(60, bars.DiscreteWidth!.Value, Precision);
        Assert.Null(bars.DiscreteHeight);
        Assert.Equal([S("a"), S("b"), S("c")], x.DiscreteDomain);
        var xModel = x.CreateModel(60);
        Assert.Equal(18, xModel.Bandwidth, Precision);
        Assert.Equal(1, xModel.Map(S("a"))!.Value, Precision);
        Assert.Equal(21, xModel.Map(S("b"))!.Value, Precision);
        Assert.Equal(41, xModel.Map(S("c"))!.Value, Precision);

        // Assert: a scatter plot includes zero by the configuration; 10..35 is nice as 0..35 (step 5) and 5..20 as 0..16 (step 2)
        var scatter = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative"}, "y": {"field":"q","type":"quantitative"} }"""));
        var scatterX = scatter["x"]!.CreateModel(300);
        var scatterY = scatter["y"]!.CreateModel(300);
        Assert.Equal((0.0, 35.0), (scatterX.DomainStart, scatterX.DomainEnd));
        Assert.Equal((0.0, 12.0), (scatterY.DomainStart, scatterY.DomainEnd));
        Assert.Equal(0, scatterX.Map(0));
        Assert.Equal(300, scatterX.Map(35), Precision);
        Assert.Equal(300, scatterY.Map(0));

        // Assert: 5..15 is nice as 0..16 (step 2, because 15 / 10 = 1.5 is above sqrt(2))
        var fifteen = Resolve("""{ "data": {"values":[{"v":5},{"v":15}]}, "mark": "point", "encoding": { "y": {"field":"v","type":"quantitative"} } }""");
        var fifteenModel = fifteen["y"]!.CreateModel(160);
        Assert.Equal((0.0, 16.0), (fifteenModel.DomainStart, fifteenModel.DomainEnd));
        Assert.Equal(60, fifteenModel.Map(10), Precision);

        // Assert: zero false keeps the data extent and nice rounds it: 10..35 has step 2, so 10..36; q 3..12 has step 1
        var noZero = Resolve(Spec(
            "point",
            """{ "x": {"field":"r","type":"quantitative","scale":{"zero":false}}, "y": {"field":"q","type":"quantitative","scale":{"zero":false}} }"""));
        var noZeroX = noZero["x"]!.CreateModel(100);
        Assert.Equal((10.0, 36.0), (noZeroX.DomainStart, noZeroX.DomainEnd));
        var noZeroY = noZero["y"]!.CreateModel(100);
        Assert.Equal((3.0, 12.0), (noZeroY.DomainStart, noZeroY.DomainEnd));
        Assert.Equal(100, noZeroY.Map(3), Precision);
        Assert.Equal(0, noZeroY.Map(12), Precision);

        // Assert: an explicit domain is used as given: no nice, and zero only when it already spans zero
        var explicitDomain = Resolve(Spec(
            "point",
            """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 100]}}, "y": {"field":"q","type":"quantitative","scale":{"domain":[2, 50]}} }"""));
        Assert.Null(explicitDomain["x"]!.NiceCount);
        Assert.Equal(150, explicitDomain["x"]!.CreateModel(300).Map(50), Precision);
        var narrow = explicitDomain["y"]!.CreateModel(100);
        Assert.Equal((2.0, 50.0), (narrow.DomainStart, narrow.DomainEnd));

        // Assert: an explicit zero overrides the rule even with a domain; 5..15 as zero true gives 0..15
        var forcedZero = Resolve(Spec("point", """{ "y": {"field":"q","type":"quantitative","scale":{"domain":[5, 15],"zero":true}} }"""));
        Assert.Equal((0.0, 15.0), (forcedZero["y"]!.DomainStart, forcedZero["y"]!.DomainEnd));

        // Assert: nice may be a tick count (0..97 with 5 ticks has step 20: 0..100), or false
        var niceCount = Resolve(Spec(
            "point",
            """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 97],"nice":5}}, "y": {"field":"q","type":"quantitative","scale":{"nice":false}} }"""));
        var withNice = niceCount["x"]!.CreateModel(100);
        Assert.Equal((0.0, 100.0), (withNice.DomainStart, withNice.DomainEnd));
        Assert.Null(niceCount["y"]!.NiceCount);
        Assert.Equal((0.0, 12.0), (niceCount["y"]!.DomainStart, niceCount["y"]!.DomainEnd));

        // Assert: pixel padding widens the domain around its middle: 0..100 in 200 pixels with 10 pixels of padding is
        // -5.56..105.56, so 0 lands on pixel 10 and 100 on pixel 190
        var padded = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 100],"padding":10}} }"""));
        var paddedModel = padded["x"]!.CreateModel(200);
        Assert.Equal(10, paddedModel.Map(0), Precision);
        Assert.Equal(190, paddedModel.Map(100), Precision);

        // Assert: a vertical bar on a continuous x has 5 pixels of padding (the configured continuous band size), no zero on
        // x, and the padded domain 0.89..3.11 is extended by nice to 0.8..3.2 (step 0.2 at 10 ticks)
        var continuousBars = Resolve("""
            { "data": { "values": [ {"a":1,"b":1}, {"a":2,"b":2}, {"a":3,"b":3} ] }, "mark": "bar",
              "encoding": { "x": {"field":"a","type":"quantitative"}, "y": {"field":"b","type":"quantitative"} } }
            """);
        Assert.Equal(5, continuousBars["x"]!.PaddingPixels);
        Assert.Equal(0, continuousBars["y"]!.PaddingPixels);
        var continuousX = continuousBars["x"]!.CreateModel(100);
        Assert.Equal(0.8, continuousX.DomainStart, Precision);
        Assert.Equal(3.2, continuousX.DomainEnd, Precision);
        var continuousY = continuousBars["y"]!.CreateModel(100);
        Assert.Equal((0.0, 3.0), (continuousY.DomainStart, continuousY.DomainEnd));

        // Assert: a horizontal bar has the measure on x (zero true) and no padding there
        var horizontal = Resolve(Spec("bar", """{ "x": {"field":"q","type":"quantitative"}, "y": {"field":"k","type":"nominal"} }"""));
        Assert.Equal(0, horizontal["x"]!.PaddingPixels);
        Assert.Equal(0, horizontal["x"]!.DomainStart);
        Assert.Equal("horizontal", horizontal.Orient);
        Assert.Equal(ScaleKind.Band, horizontal["y"]!.Kind);
        Assert.Equal(60, horizontal.DiscreteHeight!.Value, Precision);

        // Assert: a reversed scale swaps the range ends, and so does "sort": "descending" on a continuous scale
        var reversed = Resolve(Spec("point", """{ "y": {"field":"q","type":"quantitative","scale":{"reverse":true,"nice":false,"zero":false}} }"""));
        var reversedModel = reversed["y"]!.CreateModel(90);
        Assert.Equal(0, reversedModel.Map(3), Precision);
        Assert.Equal(90, reversedModel.Map(12), Precision);
        var descending = Resolve(Spec("point", """{ "y": {"field":"q","type":"quantitative","sort":"descending","scale":{"nice":false,"zero":false}} }"""));
        Assert.Equal(0, descending["y"]!.CreateModel(90).Map(3), Precision);

        // Assert: an explicit range replaces the default, and "width" stands for the extent
        var explicitRange = Resolve(Spec(
            "point",
            """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 100],"range":[20, "width"]}}, "y": {"field":"q","type":"quantitative","scale":{"domain":[0,10],"range":[0, 50]}} }"""));
        Assert.Equal(20, explicitRange["x"]!.CreateModel(300).Map(0), Precision);
        Assert.Equal(300, explicitRange["x"]!.CreateModel(300).Map(100), Precision);
        Assert.Equal(25, explicitRange["y"]!.CreateModel(999).Map(5), Precision);

        // Assert: a datum has a one-value domain; zero extends it to 0..5
        var datum = Resolve("""{ "mark": "rule", "encoding": { "x": {"datum": 5, "type": "quantitative"} } }""");
        Assert.Equal((0.0, 5.0), (datum["x"]!.DomainStart, datum["x"]!.DomainEnd));

        // Assert: no data gives the default domain 0..1
        var empty = Resolve("""{ "data": {"values": []}, "mark": "point", "encoding": { "y": {"field":"q","type":"quantitative"} } }""");
        var emptyModel = empty["y"]!.CreateModel(100);
        Assert.Equal((0.0, 1.0), (emptyModel.DomainStart, emptyModel.DomainEnd));

        // Assert: invalid values (null, text that is not a number) are ignored, numeric text is read, booleans are 1 and 0
        var dirty = Resolve("""
            { "data": { "values": [ {"v": null}, {"v": "7"}, {"v": "x"}, {"v": 2}, {"v": true}, {} ] }, "mark": "point",
              "encoding": { "y": {"field":"v","type":"quantitative","scale":{"nice":false}} } }
            """);
        Assert.Equal((0.0, 7.0), (dirty["y"]!.DomainStart, dirty["y"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that an explicit domain and range of more than two entries map piecewise as d3's <c>polymap</c> does,
    ///     with <c>zero</c>, pixel padding, <c>nice</c>, <c>clamp</c> and <c>reverse</c> acting on the end entries only, and that
    ///     a band or point scale takes the first two entries of its range.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_PiecewiseDomainAndRangeFollowD3()
    {
        // Arrange / Act: domain 0..50..100 onto range 0..200..300 (the keyword is not needed)
        var piecewise = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 50, 100],"range":[0, 200, 300]}} }"""))["x"]!;
        var model = piecewise.CreateModel(999);

        // Assert: 25 is halfway through the first segment (100), 75 halfway through the second (250), and nice does not change it
        Assert.Null(piecewise.NiceCount);
        Assert.Equal(100, model.Map(25), Precision);
        Assert.Equal(200, model.Map(50), Precision);
        Assert.Equal(250, model.Map(75), Precision);
        Assert.Equal((0.0, 100.0, 0.0, 300.0), (model.DomainStart, model.DomainEnd, model.RangeStart, model.RangeEnd));

        // Assert: with a range keyword the entries follow the data rectangle: [0, "width", 600] on a width of 300 maps the
        // second domain entry to 300 and the third to 600
        var keyword = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 50, 100],"range":[0, "width", 600]}} }"""))["x"]!.CreateModel(300);
        Assert.Equal(300, keyword.Map(50), Precision);
        Assert.Equal(450, keyword.Map(75), Precision);

        // Assert: a domain with three entries on the default two-entry range uses the first two entries only (d3's bimap):
        // 0..50 onto 0..300, so 25 is 150 and 100 is 600
        var truncated = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 50, 100]}} }"""))["x"]!.CreateModel(300);
        Assert.Equal(150, truncated.Map(25), Precision);
        Assert.Equal(600, truncated.Map(100), Precision);
        Assert.Equal(100, truncated.DomainEnd);

        // Assert: clamp stops at the first and last entry: -10 is 0 and 500 is 300
        var clamped = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 50, 100],"range":[0, 200, 300],"clamp":true}} }"""))["x"]!.CreateModel(999);
        Assert.Equal(0, clamped.Map(-10), Precision);
        Assert.Equal(300, clamped.Map(500), Precision);

        // Assert: reverse flips the whole range: [300, 200, 0], so 0 is 300, 25 is 250 and 100 is 0
        var reversed = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 50, 100],"range":[0, 200, 300],"reverse":true}} }"""))["x"]!.CreateModel(999);
        Assert.Equal(300, reversed.Map(0), Precision);
        Assert.Equal(250, reversed.Map(25), Precision);
        Assert.Equal(0, reversed.Map(100), Precision);

        // Assert: zero true changes only the first entry: 5..10..15 becomes 0..10..15 over 0..50..100, so 5 is 25
        var zero = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[5, 10, 15],"range":[0, 50, 100],"zero":true}} }"""))["x"]!.CreateModel(100);
        Assert.Equal(25, zero.Map(5), Precision);
        Assert.Equal(50, zero.Map(10), Precision);
        Assert.Equal(100, zero.Map(15), Precision);
        Assert.Equal(0, zero.DomainStart);

        // Assert: nice with 5 ticks extends 0..97 to 0..100 and leaves the middle entry: 3 -> 100, 100 -> 200
        var nice = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 3, 97],"range":[0, 100, 200],"nice":5}} }"""))["x"]!.CreateModel(100);
        Assert.Equal((0.0, 100.0), (nice.DomainStart, nice.DomainEnd));
        Assert.Equal(100, nice.Map(3), Precision);
        Assert.Equal(200, nice.Map(100), Precision);

        // Assert: 10 pixels of padding over a range of 300 widens the end entries around their middle (50) by 300/280:
        // -3.5714..103.5714; the middle entry stays at 50 (pixel 200) and 0 lands 1/15 of the way into the first segment
        var padded = Resolve(Spec("point", """{ "x": {"field":"r","type":"quantitative","scale":{"domain":[0, 50, 100],"range":[0, 200, 300],"padding":10}} }"""))["x"]!.CreateModel(999);
        Assert.Equal(-25.0 / 7, padded.DomainStart, Precision);
        Assert.Equal(725.0 / 7, padded.DomainEnd, Precision);
        Assert.Equal(200, padded.Map(50), Precision);
        Assert.Equal(200.0 / 15, padded.Map(0), Precision);

        // Assert: a band scale uses the first two entries of its range (d3's [r0, r1] = range), here 0..100 for three bands
        // with Vega-Lite's bar padding: a step of 100 / 3
        var band = Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal","scale":{"range":[0, 100, 500]}} }"""))["x"]!.CreateModel(999);
        Assert.Equal((0.0, 100.0), (band.RangeStart, band.RangeEnd));
        Assert.Equal(100.0 / 3, band.Step, Precision);
    }

    /// <summary>
    ///     Validates that the paddings of band and point scales follow the mark, the configuration and the scale
    ///     properties.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_PaddingsFollowMarkAndScaleProperties()
    {
        // Arrange / Act / Assert: defaults by mark; the outer padding is half the inner padding
        var vectors = new (string Mark, string Scale, double Inner, double Outer)[]
        {
            ("bar", "{}", 0.1, 0.05),
            ("tick", "{}", 0.25, 0.125),
            ("rect", "{}", 0, 0),
            ("rule", "{}", 0, 0),

            // "padding" sets both; the specific properties refine it
            ("bar", """{"padding": 0.3}""", 0.3, 0.3),
            ("bar", """{"padding": 3}""", 1, 3),
            ("bar", """{"padding": 0.3, "paddingInner": 0.2}""", 0.2, 0.3),
            ("bar", """{"padding": 0.3, "paddingOuter": 0.1}""", 0.3, 0.1),

            // an explicit inner padding makes the outer half of it
            ("bar", """{"paddingInner": 0.4}""", 0.4, 0.2),
            ("bar", """{"paddingInner": 0.4, "paddingOuter": 0.5}""", 0.4, 0.5),

            // a point scale: half a step outside by default, "padding" or "paddingOuter" otherwise; the inner padding is 1
            ("point", "{}", 1, 0.5),
            ("point", """{"padding": 0.25}""", 1, 0.25),
            ("point", """{"paddingOuter": 0.75}""", 1, 0.75),
        };

        foreach (var (mark, scale, inner, outer) in vectors)
        {
            var resolved = Resolve(Spec(mark, "{ \"x\": {\"field\":\"k\",\"type\":\"nominal\",\"scale\":" + scale + "} }"))["x"]!;
            Assert.Equal((inner, outer), (resolved.PaddingInner, resolved.PaddingOuter));
        }

        // Assert: the configuration overrides the mark defaults
        var tree = SpecParser.Parse(Spec(
            "bar",
            """{ "x": {"field":"k","type":"nominal"} }""",
            "\"config\": {\"scale\": {\"bandPaddingInner\": 0.6, \"bandPaddingOuter\": 0.1, \"pointPadding\": 0.25}},"));
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
        var configured = ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), null, null, Ct)["x"]!;
        Assert.Equal((0.6, 0.1), (configured.PaddingInner, configured.PaddingOuter));
        var pointTree = SpecParser.Parse(Spec("point", """{ "x": {"field":"k","type":"nominal"} }""", "\"config\": {\"scale\": {\"pointPadding\": 0.25}},"));
        var configuredPoint = ScaleResolver.Resolve(pointTree.Root, table, ConfigResolver.Resolve(pointTree, null), null, null, Ct)["x"]!;
        Assert.Equal(0.25, configuredPoint.PaddingOuter);

        // Assert: alignment and rounding are passed on
        var aligned = Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal","scale":{"align":0.25,"round":true}} }"""))["x"]!;
        Assert.Equal(0.25, aligned.Align);
        Assert.True(aligned.Round);
    }

    /// <summary>
    ///     Validates the order of discrete domains: sorted ascending by default, data order, descending, an explicit
    ///     array, numbers numerically and nulls first.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_DiscreteDomainsFollowTheSortProperty()
    {
        // Arrange / Act / Assert: the sample has categories b, a, c, a in data order
        string Domain(string definitionExtras) =>
            string.Join(",", Resolve(Spec("bar", "{ \"x\": {\"field\":\"k\",\"type\":\"nominal\"" + definitionExtras + "} }"))["x"]!.DiscreteDomain.Select(v => v.ToText()));
        Assert.Equal("a,b,c", Domain(string.Empty));
        Assert.Equal("a,b,c", Domain(",\"sort\":\"ascending\""));
        Assert.Equal("b,a,c", Domain(",\"sort\":null"));
        Assert.Equal("c,b,a", Domain(",\"sort\":\"descending\""));

        // Assert: values missing from a sort array come first (Vega-Lite sorts by the index, which is -1 for them)
        Assert.Equal("a,c,b", Domain(",\"sort\":[\"c\",\"b\"]"));
        Assert.Equal("c,a,b", Domain(",\"sort\":[\"c\",\"a\",\"b\"]"));

        // Assert: an explicit scale domain is used as given, including values the data does not have
        Assert.Equal("z,b,a", string.Join(",", Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal","scale":{"domain":["z","b","a","b"]}} }"""))["x"]!.DiscreteDomain.Select(v => v.ToText())));

        // Assert: numbers sort numerically and numeric text sorts as text; null sorts first
        var numbers = Resolve("""{ "data": {"values":[{"n":10},{"n":9},{"n":100}]}, "mark": "point", "encoding": { "x": {"field":"n","type":"ordinal"} } }""")["x"]!;
        Assert.Equal("9,10,100", string.Join(",", numbers.DiscreteDomain.Select(v => v.ToText())));
        var text = Resolve("""{ "data": {"values":[{"n":"10"},{"n":"9"},{"n":"100"}]}, "mark": "point", "encoding": { "x": {"field":"n","type":"ordinal"} } }""")["x"]!;
        Assert.Equal("10,100,9", string.Join(",", text.DiscreteDomain.Select(v => v.ToText())));
        var nulls = Resolve("""{ "data": {"values":[{"n":"a"},{"n":null},{},{"n":"b"}]}, "mark": "point", "encoding": { "x": {"field":"n","type":"nominal"} } }""")["x"]!;
        Assert.Equal("null,a,b", string.Join(",", nulls.DiscreteDomain.Select(v => v.ToText())));

        // Assert: a field the data does not have is one null category
        var missing = Resolve(Spec("point", """{ "x": {"field":"nope","type":"nominal"} }"""))["x"]!;
        Assert.Equal([DataValue.Null], missing.DiscreteDomain);
    }

    /// <summary>
    ///     Validates that an explicit sort array uses the first position of a value that is listed more than once, puts
    ///     the values it does not list first, and stays correct for a long list (the positions are looked up, not searched).
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_ExplicitSortArrayUsesTheFirstIndexOfEachValue()
    {
        // Arrange: the sample has categories b, a, c, a in data order
        string Domain(string sort) =>
            string.Join(",", Resolve(Spec("bar", "{ \"x\": {\"field\":\"k\",\"type\":\"nominal\",\"sort\":" + sort + "} }"))["x"]!.DiscreteDomain.Select(v => v.ToText()));

        // Act / Assert: a repeated value counts at its first position
        Assert.Equal("c,b,a", Domain("[\"c\",\"b\",\"c\",\"a\"]"));
        Assert.Equal("a,b,c", Domain("[\"b\",\"c\",\"b\"]"));

        // Assert: a value of another kind is not the same value, so it does not position the text
        Assert.Equal("a,b,c", Domain("[1,\"b\",true,\"c\"]"));

        // Assert: a long list gives the same order as a short one, with the unlisted value first
        var padding = string.Join(",", Enumerable.Range(0, 20_000).Select(i => "\"pad" + i + "\""));
        Assert.Equal("a,c,b", Domain("[" + padding + ",\"c\",\"b\",\"c\"]"));
    }

    /// <summary>
    ///     Validates the size and opacity ranges against the Vega-Lite defaults.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_SizeAndOpacityUseTheDefaultRanges()
    {
        // Arrange / Act: a point mark: size from 4 to (0.95 * 20)^2 = 361 with zero in the domain (sizes 1, 2, 4 -> 0..4)
        var data = "\"data\": { \"values\": [ {\"s\": 1, \"o\": 10}, {\"s\": 2, \"o\": 20}, {\"s\": 4, \"o\": 15} ] }";
        var point = Resolve("{" + data + """, "mark": "point", "encoding": { "size": {"field":"s","type":"quantitative"}, "opacity": {"field":"o","type":"quantitative"} } }""");
        var size = point["size"]!.CreateModel(0);
        var opacity = point["opacity"]!.CreateModel(0);

        // Assert: size
        Assert.Equal((0.0, 4.0), (size.DomainStart, size.DomainEnd));
        Assert.Equal(4, size.Map(0), Precision);
        Assert.Equal(361, size.Map(4), Precision);
        Assert.Equal(182.5, size.Map(2), Precision);
        Assert.Null(point["size"]!.NiceCount);

        // Assert: opacity from 0.3 to 0.8 over the data extent (no zero): 15 is in the middle
        Assert.Equal((10.0, 20.0), (opacity.DomainStart, opacity.DomainEnd));
        Assert.Equal(0.3, opacity.Map(10), Precision);
        Assert.Equal(0.55, opacity.Map(15), Precision);
        Assert.Equal(0.8, opacity.Map(20), Precision);

        // Assert: the size range depends on the mark: bars and ticks 2..step-1, rules 1..4, circles like points
        string Range(string mark, string extra = "")
        {
            var resolved = Resolve("{" + extra + data + ", \"mark\": \"" + mark + "\", \"encoding\": { \"size\": {\"field\":\"s\",\"type\":\"quantitative\"} } }")["size"]!;
            var model = resolved.CreateModel(0);
            return $"{model.Map(0):0.##}..{model.Map(4):0.##}";
        }

        Assert.Equal("2..19", Range("bar"));
        Assert.Equal("2..19", Range("tick"));
        Assert.Equal("1..4", Range("rule"));
        Assert.Equal("4..361", Range("circle"));
        Assert.Equal("4..361", Range("square"));

        // Assert: a smaller step limits the largest size: (0.95 * 10)^2 = 90.25 and 10 - 1 = 9
        Assert.Equal("4..90.25", Range("point", "\"height\": {\"step\": 10},"));
        Assert.Equal("2..9", Range("bar", "\"width\": {\"step\": 10},"));

        // Assert: a mark without sizes (rect) and an explicit range
        Assert.Null(Resolve("{" + data + """, "mark": "rect", "encoding": { "size": {"field":"s","type":"quantitative"} } }""")["size"]);
        var custom = Resolve("{" + data + """, "mark": "point", "encoding": { "size": {"field":"s","type":"quantitative","scale":{"range":[10, 100]}} } }""")["size"]!.CreateModel(0);
        Assert.Equal(10, custom.Map(0), Precision);
        Assert.Equal(100, custom.Map(4), Precision);

        // Assert: categories are spread over the range as points with no padding: three categories 4, 182.5, 361
        var categories = Resolve("{" + data + """, "mark": "point", "encoding": { "size": {"field":"s","type":"ordinal"} } }""")["size"]!.CreateModel(0);
        Assert.Equal(ScaleKind.Point, categories.Kind);
        Assert.Equal(4, categories.Map(DataValue.FromNumber(1))!.Value, Precision);
        Assert.Equal(182.5, categories.Map(DataValue.FromNumber(2))!.Value, Precision);
        Assert.Equal(361, categories.Map(DataValue.FromNumber(4))!.Value, Precision);
    }

    /// <summary>
    ///     Validates the colors of ordinal color scales: the tableau10 default, schemes, explicit ranges, the configured named ranges
    ///     and the Vega-Lite <c>ordinal</c> range of an ordinal field.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OrdinalColorsUseTheConfiguredRanges()
    {
        // Arrange / Act: the default for a nominal field; the domain is a, b, c
        var plain = Resolve(Spec("point", """{ "color": {"field":"k","type":"nominal"} }"""))["color"]!;
        var model = plain.CreateModel(0);

        // Assert: the tableau10 colors in domain order
        Assert.Equal(ColorSchemes.Tableau10, plain.ColorRange);
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], new[] { "a", "b", "c" }.Select(c => model.MapOrdinal(S(c))).ToArray());
        Assert.Null(model.MapOrdinal(S("zzz")));

        // Assert: the named scheme tableau10, a scheme object, another categorical scheme and an explicit range
        Assert.Equal(ColorSchemes.Tableau10, Resolve(Spec("point", """{ "color": {"field":"k","type":"nominal","scale":{"scheme":"tableau10"}} }"""))["color"]!.ColorRange);
        Assert.Equal(ColorSchemes.Tableau10, Resolve(Spec("point", """{ "color": {"field":"k","type":"nominal","scale":{"scheme":{"name":"tableau10"}}} }"""))["color"]!.ColorRange);
        Assert.True(ColorSchemes.TryGetScheme("set1", out var set1));
        Assert.Equal(set1, Resolve(Spec("point", """{ "color": {"field":"k","type":"nominal","scale":{"scheme":"set1"}} }"""))["color"]!.ColorRange);
        var custom = Resolve(Spec("point", """{ "color": {"field":"k","type":"nominal","scale":{"range":["red","blue"]}} }"""))["color"]!;
        Assert.Equal(["red", "blue"], custom.ColorRange);
        Assert.Equal("red", custom.CreateModel(0).MapOrdinal(S("a")));
        Assert.Equal("red", custom.CreateModel(0).MapOrdinal(S("c")));

        // Assert: an ordinal field uses Vega-Lite's "ordinal" range (the blues ramp sampled at i / (n + 1) for n = 3 categories)
        var ordinal = Resolve(Spec("point", """{ "color": {"field":"k","type":"ordinal"} }"""))["color"]!;
        Assert.True(ColorSchemes.TryGetColorScheme("blues", out var blues));
        Assert.Equal(
            ColorInterpolator.Quantize(blues.Ramp.At, 3, Ct).Select(c => c.ToCss()).ToArray(),
            ordinal.ColorRange);

        // Assert: the configured category range (a list of colors, or a scheme) wins over the default
        var tree = SpecParser.Parse(Spec("point", """{ "color": {"field":"k","type":"nominal"} }""", "\"config\": {\"range\": {\"category\": [\"#111111\", \"#222222\"]}},"));
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
        var configured = ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), null, null, Ct)["color"]!;
        Assert.Equal(["#111111", "#222222"], configured.ColorRange);
    }

    /// <summary>
    ///     Validates the step that sizes a discrete position scale: the default step, a step in the specification, a fixed
    ///     size, an explicit range and the configuration.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_DiscreteSizesComeFromTheStep()
    {
        // Arrange / Act / Assert: three bars: 20 * (3 - 0.1 + 0.1) = 60; a step in the specification: 30 * 3 = 90
        var encoding = """{ "x": {"field":"k","type":"nominal"} }""";
        Assert.Equal(60, Resolve(Spec("bar", encoding)).DiscreteWidth!.Value, Precision);
        Assert.Equal(20, Resolve(Spec("bar", encoding))["x"]!.Step);
        Assert.Equal(90, Resolve(Spec("bar", encoding, "\"width\": {\"step\": 30},")).DiscreteWidth!.Value, Precision);

        // Assert: a fixed width is not step-driven, and neither is an explicit range or "container"
        Assert.Null(Resolve(Spec("bar", encoding, "\"width\": 400,")).DiscreteWidth);
        Assert.Null(Resolve(Spec("bar", encoding, "\"width\": 400,"))["x"]!.Step);
        Assert.Null(Resolve(Spec("bar", encoding, "\"width\": \"container\",")).DiscreteWidth);
        Assert.Null(Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal","scale":{"range":[0, 200]}} }""")).DiscreteWidth);

        // Assert: four points with half a step of padding take 20 * 4 = 80; five bars on y take 20 * 5 = 100 (rows a..e)
        Assert.Equal(60, Resolve(Spec("point", encoding)).DiscreteWidth!.Value, Precision);
        var five = Resolve("""
            { "data": {"values":[{"k":"a"},{"k":"b"},{"k":"c"},{"k":"d"},{"k":"e"}]}, "mark": "bar", "encoding": { "y": {"field":"k","type":"nominal"} } }
            """);
        Assert.Equal(100, five.DiscreteHeight!.Value, Precision);
        Assert.Null(five.DiscreteWidth);
        var fourPoints = Resolve("""
            { "data": {"values":[{"k":"a"},{"k":"b"},{"k":"c"},{"k":"d"}]}, "mark": "point", "encoding": { "x": {"field":"k","type":"nominal"} } }
            """);
        Assert.Equal(80, fourPoints.DiscreteWidth!.Value, Precision);

        // Assert: a continuous scale has no step size
        Assert.Null(Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative"} }""")).DiscreteWidth);

        // Assert: the configured step applies when the view has no size: 25 * 3 = 75
        Assert.Equal(75, Resolve(Spec("bar", encoding, "\"config\": {\"view\": {\"step\": 25}},")).DiscreteWidth!.Value, Precision);

        // Assert: the model of a step-sized scale fills the extent from the layout
        Assert.Equal(60, Resolve(Spec("bar", encoding))["x"]!.CreateModel(60).Step * 3, Precision);
    }

    /// <summary>
    ///     Validates the failures of the resolver: a domain over the limit and invalid scale properties.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_InvalidInputsFailWithLocation()
    {
        // Arrange / Act / Assert: three categories over a limit of two
        var ex = Assert.Throws<VegaLiteRenderException>(() =>
            Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal"} }"""), new VegaLiteLimits { MaxScaleDomainSize = 2 }));
        Assert.Equal("/encoding/x", ex.Location.Pointer);
        Assert.Contains("MaxScaleDomainSize", ex.Message, StringComparison.Ordinal);

        // Assert: an explicit domain over the limit too
        Assert.Throws<VegaLiteRenderException>(() =>
            Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal","scale":{"domain":["a","b","c"]}} }"""), new VegaLiteLimits { MaxScaleDomainSize = 2 }));

        // Assert: bad ranges and domains name the offending value
        var badRange = Assert.Throws<VegaLiteSpecException>(() =>
            Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"range":[0, "left"]}} }""")));
        Assert.Equal("/encoding/x/scale/range/1", badRange.Location.Pointer);
        var shortRange = Assert.Throws<VegaLiteSpecException>(() =>
            Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"range":[0]}} }""")));
        Assert.Equal("/encoding/x/scale/range", shortRange.Location.Pointer);
        var badDomain = Assert.Throws<VegaLiteSpecException>(() =>
            Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"domain":[0, "a"]}} }""")));
        Assert.Equal("/encoding/x/scale/domain/1", badDomain.Location.Pointer);
        Assert.Throws<VegaLiteSpecException>(() =>
            Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"domain":[1]}} }""")));

        // Assert: argument checks and cancellation
        var tree = SpecParser.Parse(Spec("bar", """{ "x": {"field":"k","type":"nominal"} }"""));
        var config = ConfigResolver.Resolve(tree, null);
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.Resolve(null!, DataTable.Empty, config, null, null, Ct));
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.Resolve(tree.Root, null!, config, null, null, Ct));
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.Resolve(tree.Root, DataTable.Empty, null!, null, null, Ct));
        using var source = new CancellationTokenSource();
        source.Cancel();
        var rows = string.Join(",", Enumerable.Range(0, 9000).Select(i => "{\"k\":\"c" + i + "\"}"));
        var big = SpecParser.Parse("{\"data\":{\"values\":[" + rows + "]},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"}}}");
        Assert.Throws<OperationCanceledException>(() =>
            ScaleResolver.Resolve(big.Root, DataLoader.Load(big.Root["data"], null, null, Ct), ConfigResolver.Resolve(big, null), null, null, source.Token));
    }

    /// <summary>
    ///     Validates that the log, pow, sqrt and symlog scale types resolve to a continuous scale with the matching transform and its
    ///     parameter (the <c>base</c>, <c>exponent</c> or <c>constant</c>, with Vega's defaults of 10, 1 and 1, sqrt being the power 0.5
    ///     whatever <c>exponent</c> says), on position, size and color channels, with positions derived by hand from the d3 formulas.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_LogPowSqrtSymlogCarryTheirTransform()
    {
        // Arrange / Act: q is 3, 7, 12, 5. A log x gets the base 10, no zero, and nice to powers of ten: 3..12 becomes 1..100
        var log = Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log"}} }"""))["x"]!;

        // Assert: the kind stays linear and the transform is the log of base 10 over the domain of the data
        Assert.Equal(ScaleKind.Linear, log.Kind);
        Assert.Equal(ScaleTransformKind.Log, log.Transform.Kind);
        Assert.Equal(10, log.Transform.Parameter);
        Assert.Equal((3.0, 12.0), (log.DomainStart, log.DomainEnd));
        Assert.Equal(10, log.NiceCount);

        // Assert: the model is nice to the powers 1..100 (two decades over 300 pixels: 150 per decade), so 10 is the middle
        var model = log.CreateModel(300);
        Assert.Equal((1.0, 100.0), (model.DomainStart, model.DomainEnd));
        Assert.Equal(150, model.Map(10), Precision);
        Assert.Equal(0, model.Map(1), Precision);

        // Assert: base 2 rounds out to 2..16 (floor of log2 3 is 1, ceil of log2 12 is 4)
        var base2 = Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log","base":2}} }"""))["x"]!;
        Assert.Equal(2, base2.Transform.Parameter);
        Assert.Equal((2.0, 16.0), (base2.CreateModel(300).DomainStart, base2.CreateModel(300).DomainEnd));

        // Assert: pow takes the exponent (default 1), keeps the zero rule of a point's x (0..12), and 6 is 36 of 144 along the way (75 of 300)
        var pow = Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"pow","exponent":2}} }"""))["x"]!;
        Assert.Equal(ScaleTransformKind.Pow, pow.Transform.Kind);
        Assert.Equal(2, pow.Transform.Parameter);
        Assert.Equal((0.0, 12.0), (pow.DomainStart, pow.DomainEnd));
        Assert.Equal(75, pow.CreateModel(300).Map(6), Precision);
        Assert.Equal(1, Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"pow"}} }"""))["x"]!.Transform.Parameter);

        // Assert: sqrt is the power 0.5 and ignores the exponent; 3 is sqrt(3) of sqrt(12) of the way
        var sqrt = Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"sqrt","exponent":3}} }"""))["x"]!;
        Assert.Equal(ScaleTransformKind.Pow, sqrt.Transform.Kind);
        Assert.Equal(0.5, sqrt.Transform.Parameter);
        Assert.Equal(300 * Math.Sqrt(3) / Math.Sqrt(12), sqrt.CreateModel(300).Map(3), Precision);

        // Assert: symlog takes the constant (default 1); with 10 the number 90 is ln 10 along and 9 is ln(1.9) / ln(10) of it
        var symlog = Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"symlog","constant":10,"domain":[0,90]}} }"""))["x"]!;
        Assert.Equal(ScaleTransformKind.Symlog, symlog.Transform.Kind);
        Assert.Equal(10, symlog.Transform.Parameter);
        Assert.Equal(300 * Math.Log(1.9) / Math.Log(10), symlog.CreateModel(300).Map(9), Precision);
        Assert.Equal(1, Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"symlog"}} }"""))["x"]!.Transform.Parameter);

        // Assert: a size scale takes the transform too, and a log scale has no zero (3..12, not 0..12)
        var size = Resolve(Spec("point", """{ "size": {"field":"q","type":"quantitative","scale":{"type":"log"}} }"""))["size"]!;
        Assert.Equal(ScaleTransformKind.Log, size.Transform.Kind);
        Assert.Equal((3.0, 12.0), (size.DomainStart, size.DomainEnd));

        // Assert: a color scale becomes the sequential scale of the transform: 6 is the geometric mean of 3 and 12, so the middle of the colors
        var color = Resolve(Spec("point", """{ "color": {"field":"q","type":"quantitative","scale":{"type":"log"}} }"""))["color"]!;
        Assert.Equal(ScaleKind.Sequential, color.Kind);
        Assert.Equal(ScaleTransformKind.Log, color.Transform.Kind);
        Assert.Equal(color.Interpolator!(0.5).ToCss(), color.CreateModel(0).MapColor(6)!.Value.ToCss());

        // Assert: other scales keep the identity transform, also when they carry the properties of a transform
        Assert.True(Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"base":2,"exponent":3,"constant":4}} }"""))["x"]!.Transform.IsLinear);
        Assert.Equal(ScaleTransformKind.Linear, Resolve(Spec("point", """{ "x": {"field":"k","type":"nominal"} }"""))["x"]!.Transform.Kind);
    }

    /// <summary>
    ///     Validates the Vega-Lite defaults of a log scale: <c>zero</c> is ignored (a log domain cannot reach zero), <c>nice</c> follows the
    ///     usual rules (on by default for x and y without a domain, off for an explicit domain or <c>nice: false</c>), <c>clamp</c> and
    ///     <c>domain</c> carry over, a domain below zero is reflected, the pixel padding works on the logarithm, and a bar chart on a log
    ///     axis does not pull its domain to zero.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_LogScaleDefaultsFollowVegaLite()
    {
        // Assert: an explicit zero: true does not pull the domain to zero (Vega-Lite does not support zero on a log scale)
        var zero = Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log","zero":true,"nice":false}} }"""))["x"]!;
        Assert.Equal((3.0, 12.0), (zero.DomainStart, zero.DomainEnd));
        Assert.Null(zero.NiceCount);
        Assert.Equal((3.0, 12.0), (zero.CreateModel(300).DomainStart, zero.CreateModel(300).DomainEnd));

        // Assert: an explicit domain is used as it is (no nice), and clamp and reverse carry over to the model
        var explicitDomain = Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log","domain":[10,1000],"clamp":true}} }"""))["x"]!;
        Assert.Null(explicitDomain.NiceCount);
        Assert.True(explicitDomain.Clamp);
        var clamped = explicitDomain.CreateModel(300);
        Assert.Equal(300, clamped.Map(100000), Precision);
        Assert.Equal(150, clamped.Map(100), Precision);

        // Assert: the measure of a bar does not start at zero on a log scale; with the default nice it spans 1..100
        var bar = Resolve(Spec("bar", """{ "x": {"field":"k","type":"nominal"}, "y": {"field":"q","type":"quantitative","scale":{"type":"log"}} }"""))["y"]!;
        Assert.Equal((3.0, 12.0), (bar.DomainStart, bar.DomainEnd));
        Assert.Equal((1.0, 100.0), (bar.CreateModel(200).DomainStart, bar.CreateModel(200).DomainEnd));

        // Assert: a domain below zero is reflected: -100, -10 and -1 make the domain -100..-1, and -10 is the middle
        var negative = Resolve("""
            { "data": {"values":[{"v":-100},{"v":-10},{"v":-1}]}, "mark": "point", "encoding": { "y": {"field":"v","type":"quantitative","scale":{"type":"log"}} } }
            """)["y"]!;
        Assert.Equal((-100.0, -1.0), (negative.DomainStart, negative.DomainEnd));
        Assert.Equal(150, negative.CreateModel(300).Map(-10), Precision);

        // Assert: pixel padding widens the domain by the same factor in the logarithm: 3..12 has the geometric middle 6 and the half-width
        // ln 2; with 10 pixels of padding on 300 the factor is 300 / 280 = 15 / 14, so the domain is 6 / 2^(15/14) .. 6 * 2^(15/14)
        var padded = Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log","padding":10,"nice":false}} }"""))["x"]!.CreateModel(300);
        Assert.Equal(6 / Math.Pow(2, 15.0 / 14), padded.DomainStart, Precision);
        Assert.Equal(6 * Math.Pow(2, 15.0 / 14), padded.DomainEnd, Precision);

        // Assert: without a valid value a log scale has one decade (1..10) instead of the default 0..1 that it cannot map
        var empty = Resolve("""
            { "data": {"values":[{"v":null},{"v":"a"}]}, "mark": "point", "encoding": { "y": {"field":"v","type":"quantitative","scale":{"type":"log"}} } }
            """)["y"]!;
        Assert.Equal((1.0, 10.0), (empty.DomainStart, empty.DomainEnd));
    }

    /// <summary>
    ///     Validates the domain errors of a log scale: a domain that includes zero or crosses it cannot be mapped, so it is a located
    ///     error (a specification error at the <c>domain</c> for an explicit domain, a render error at the channel for the domain of the
    ///     data), where Vega only warns and draws nothing; a symlog scale takes such domains.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_LogDomainThatIncludesZeroFailsWithALocatedError()
    {
        // Arrange: data with a zero, and data with both signs
        const string withZero = """{"data":{"values":[{"v":0},{"v":10}]},"mark":"point","encoding":{"y":{"field":"v","type":"quantitative","scale":{"type":"log"}}}}""";
        const string bothSigns = """{"data":{"values":[{"v":-5},{"v":5}]},"mark":"point","encoding":{"y":{"field":"v","type":"quantitative","scale":{"type":"log"}}}}""";

        // Assert: the domain of the data includes zero: a render error at the channel that tells what to do
        var zero = Assert.Throws<VegaLiteRenderException>(() => Resolve(withZero));
        Assert.Equal("/encoding/y", zero.Location.Pointer);
        Assert.Contains("includes zero or crosses it ([0, 10])", zero.Message, StringComparison.Ordinal);
        Assert.Contains("symlog", zero.Message, StringComparison.Ordinal);
        var crossing = Assert.Throws<VegaLiteRenderException>(() => Resolve(bothSigns));
        Assert.Contains("([-5, 5])", crossing.Message, StringComparison.Ordinal);

        // Assert: an explicit domain with zero is a specification error at the domain, also for a later entry and for domainMin
        var explicitZero = Assert.Throws<VegaLiteSpecException>(() =>
            Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log","domain":[0,100]}} }""")));
        Assert.Equal("/encoding/x/scale/domain", explicitZero.Location.Pointer);
        Assert.Throws<VegaLiteSpecException>(() =>
            Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log","domain":[1,10,0]}} }""")));
        var minimum = Assert.Throws<VegaLiteRenderException>(() =>
            Resolve(Spec("point", """{ "x": {"field":"q","type":"quantitative","scale":{"type":"log","domainMin":0}} }""")));
        Assert.Equal("/encoding/x", minimum.Location.Pointer);

        // Assert: symlog takes zero and both signs, and a pow scale too
        var symlogZero = Resolve(withZero.Replace("\"log\"", "\"symlog\"", StringComparison.Ordinal))["y"]!;
        Assert.Equal((0.0, 10.0), (symlogZero.DomainStart, symlogZero.DomainEnd));
        var symlogBoth = Resolve(bothSigns.Replace("\"log\"", "\"symlog\"", StringComparison.Ordinal))["y"]!;
        Assert.Equal((-5.0, 5.0), (symlogBoth.DomainStart, symlogBoth.DomainEnd));
        Assert.NotNull(Resolve(bothSigns.Replace("\"log\"", "\"pow\"", StringComparison.Ordinal))["y"]);
    }

    /// <summary>
    ///     Validates the cases in which an explicit log, pow or symlog type is not used: a nominal field takes the discrete default and a
    ///     shape channel the ordinal default; a stacked channel with such a scale type is not stacked at all (Vega-Lite does not stack on
    ///     a scale that is not linear), so its domain is that of the values and its bars start at the baseline.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_TransformTypesNeedAChannelAndFieldThatAcceptThem()
    {
        // Assert: a measure on a log scale is not stacked (Vega-Lite warns and draws from the baseline), so it keeps its scale and the
        // domain of the plain values; the same measure on the default scale is stacked, and its domain adds up the layers
        const string encoding = """{ "x": {"field":"k","type":"nominal"}, "y": {"field":"q","type":"quantitative","stack":"zero","scale":{"type":"log"}}, "color": {"field":"r","type":"nominal"} }""";
        var log = Resolve(Spec("bar", encoding))["y"]!;
        Assert.Null(log.Stack);
        Assert.Equal(ScaleTransformKind.Log, log.Transform.Kind);
        Assert.Equal("q", log.FieldPath);
        Assert.Equal((3.0, 12.0), (log.DomainStart, log.DomainEnd));
        var linear = Resolve(Spec("bar", encoding.Replace(",\"scale\":{\"type\":\"log\"}", string.Empty, StringComparison.Ordinal)))["y"]!;
        Assert.NotNull(linear.Stack);
        Assert.True(linear.Transform.IsLinear);

        // Assert: a nominal field has no log scale: the point or band default applies
        Assert.Equal(ScaleKind.Point, Resolve(Spec("point", """{ "x": {"field":"k","type":"nominal","scale":{"type":"log"}} }"""))["x"]!.Kind);
        Assert.Equal(ScaleKind.Ordinal, Resolve(Spec("point", """{ "color": {"field":"k","type":"nominal","scale":{"type":"symlog"}} }"""))["color"]!.Kind);

        // Assert: a shape channel is ordinal only
        var shape = Resolve(Spec("point", """{ "shape": {"field":"q","type":"quantitative","scale":{"type":"pow"}} }"""))["shape"]!;
        Assert.Equal(ScaleKind.Ordinal, shape.Kind);
        Assert.True(shape.Transform.IsLinear);
    }
}
