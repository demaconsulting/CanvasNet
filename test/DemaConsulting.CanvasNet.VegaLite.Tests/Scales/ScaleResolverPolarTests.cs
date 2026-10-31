using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the polar scales (<c>theta</c> and <c>radius</c>) and of the orientation Vega-Lite infers for line, area, tick, trail and arc marks, with numbers
///     derived by hand from the Vega-Lite rules (a full circle is 2 * PI, a radius runs from the center to half of the smaller side).
/// </summary>
public class ScaleResolverPolarTests
{
    /// <summary>The tolerance for numbers computed with fractions.</summary>
    private const int Precision = 9;

    /// <summary>The data of the pie charts: slices of 1, 1 and 2 (a total of 4).</summary>
    private const string PieData = """
        "data": { "values": [ { "c": "a", "v": 1 }, { "c": "b", "v": 1 }, { "c": "c", "v": 2 } ] }
        """;

    /// <summary>The data of the single-channel tests: values 3, 7, 12 and 5 (extent 3 to 12).</summary>
    private const string PlainData = """
        "data": { "values": [ { "k": "b", "q": 3 }, { "k": "a", "q": 7 }, { "k": "c", "q": 12 }, { "k": "a", "q": 5 } ] }
        """;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Resolves the scales of a specification.
    /// </summary>
    /// <param name="data">The data property text.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding object text.</param>
    /// <returns>The resolved scales.</returns>
    private static ResolvedScales Resolve(string data, string mark, string encoding)
    {
        var tree = SpecParser.Parse("{" + data + ", \"mark\": \"" + mark + "\", \"encoding\": " + encoding + "}");
        var table = DataLoader.Load(tree.Root["data"], tree.SourceName, null, Ct);
        return ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), tree.SourceName, null, Ct);
    }

    /// <summary>
    ///     Creates a string category.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    private static DataValue S(string text) => DataValue.FromString(text);

    /// <summary>
    ///     Parses an encoding object that holds the given channels, all over the field <c>q</c>.
    /// </summary>
    /// <param name="x">The type of the x channel, or null when absent.</param>
    /// <param name="y">The type of the y channel, or null when absent.</param>
    /// <param name="extra">More channel properties (with a leading comma), or empty.</param>
    /// <returns>The encoding node.</returns>
    private static SpecNode Encoding(string? x, string? y, string extra = "")
    {
        var parts = new List<string>();
        if (x is not null)
        {
            parts.Add("\"x\": {\"field\":\"q\",\"type\":\"" + x + "\"}");
        }

        if (y is not null)
        {
            parts.Add("\"y\": {\"field\":\"q\",\"type\":\"" + y + "\"}");
        }

        return SpecParser.Parse("{" + string.Join(",", parts) + extra + "}").Root;
    }

    /// <summary>
    ///     Validates that a pie chart's <c>theta</c> scale maps its domain to the full circle whatever the extent: the stacked domain 0 to 4 maps 1, 2 and 4 to a quarter,
    ///     a half and the full turn.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_ThetaHasTheFullCircleRange()
    {
        // Arrange / Act: slices 1, 1, 2 of a pie; the extent that the caller passes is irrelevant for an angle
        var pie = Resolve(PieData, "arc", """{ "theta": {"field":"v","type":"quantitative"}, "color": {"field":"c","type":"nominal"} }""");
        var theta = pie["theta"]!;
        var model = theta.CreateModel(12345);

        // Assert: a linear polar scale over [0, 4] with the range [0, 2 * PI]
        Assert.Equal(ScaleKind.Linear, theta.Kind);
        Assert.True(theta.IsPolar);
        Assert.False(theta.IsPosition);
        Assert.Equal((0.0, 4.0), (model.DomainStart, model.DomainEnd));
        Assert.Equal((0.0, 2 * Math.PI), (model.RangeStart, model.RangeEnd));
        Assert.Equal(0, model.Map(0), Precision);
        Assert.Equal(Math.PI / 2, model.Map(1), Precision);
        Assert.Equal(Math.PI, model.Map(2), Precision);
        Assert.Equal(2 * Math.PI, model.Map(4), Precision);
        Assert.Equal("/encoding/theta", theta.Location.Pointer);

        // Assert: a reversed scale runs the circle the other way; rangeMin and rangeMax replace the ends of the default range
        var reversed = Resolve(PieData, "arc", """{ "theta": {"field":"v","type":"quantitative","scale":{"reverse":true}} }""")["theta"]!.CreateModel(0);
        Assert.Equal((2 * Math.PI, 0.0), (reversed.RangeStart, reversed.RangeEnd));
        var limited = Resolve(PieData, "arc", """{ "theta": {"field":"v","type":"quantitative","scale":{"rangeMin":1,"rangeMax":3}} }""")["theta"]!.CreateModel(0);
        Assert.Equal((1.0, 3.0), (limited.RangeStart, limited.RangeEnd));

        // Assert: an explicit range replaces the default, the keyword "width" stands for the extent that the caller passes
        var explicitRange = Resolve(PieData, "arc", """{ "theta": {"field":"v","type":"quantitative","scale":{"range":[0, 3]}} }""")["theta"]!.CreateModel(0);
        Assert.Equal((0.0, 3.0), (explicitRange.RangeStart, explicitRange.RangeEnd));
        var keyword = Resolve(PieData, "arc", """{ "theta": {"field":"v","type":"quantitative","scale":{"range":[0, "width"]}} }""")["theta"]!.CreateModel(40);
        Assert.Equal((0.0, 40.0), (keyword.RangeStart, keyword.RangeEnd));
    }

    /// <summary>
    ///     Validates that a <c>radius</c> scale runs from zero to the extent (half of the smaller side), that <c>rangeMin</c> moves its start, and the hand-derived square-root
    ///     radial mapping <c>20 + (R - 20) * sqrt(v / max)</c> with <c>R = 100</c>.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_RadiusRangeIsHalfTheSmallerSideWithRangeMin()
    {
        // Arrange / Act: the extent passed by the builder is min(width, height) / 2 = 100; the domain of 3..12 includes zero
        var linear = Resolve(PlainData, "arc", """{ "radius": {"field":"q","type":"quantitative","stack":false} }""")["radius"]!.CreateModel(100);

        // Assert
        Assert.Equal((0.0, 12.0), (linear.DomainStart, linear.DomainEnd));
        Assert.Equal((0.0, 100.0), (linear.RangeStart, linear.RangeEnd));
        Assert.Equal(0, linear.Map(0), Precision);
        Assert.Equal(50, linear.Map(6), Precision);
        Assert.Equal(100, linear.Map(12), Precision);

        // Act: a square-root radius with the start of the range at 20 pixels; zero is on (the default for a polar channel)
        var radial = Resolve(PlainData, "arc", """{ "radius": {"field":"q","type":"quantitative","stack":false,"scale":{"type":"sqrt","zero":true,"rangeMin":20}} }""")["radius"]!;
        var sqrt = radial.CreateModel(100);

        // Assert: domain [0, 12]; a quarter of the maximum (3) is at sqrt(0.25) = 0.5 of the way from 20 to 100, that is 60; zero maps to 20 and the maximum to 100
        Assert.Equal(ScaleKind.Linear, radial.Kind);
        Assert.Equal(ScaleTransformKind.Pow, radial.Transform.Kind);
        Assert.Equal((0.0, 12.0), (sqrt.DomainStart, sqrt.DomainEnd));
        Assert.Equal(20, sqrt.Map(0), Precision);
        Assert.Equal(60, sqrt.Map(3), Precision);
        Assert.Equal(100, sqrt.Map(12), Precision);

        // Assert: the extent follows the data rectangle: a radius of 60 pixels puts the middle of the domain (6) at 30
        var smaller = Resolve(PlainData, "arc", """{ "radius": {"field":"q","type":"quantitative","stack":false} }""")["radius"]!.CreateModel(60);
        Assert.Equal(30, smaller.Map(6), Precision);

        // Assert: text marks have a radius too
        Assert.NotNull(Resolve(PlainData, "text", """{ "radius": {"field":"q","type":"quantitative","stack":false} }""")["radius"]);
    }

    /// <summary>
    ///     Validates the defaults of a polar scale: linear (not square root), zero included, no <c>nice</c>, no padding, no step; and the discrete kinds
    ///     (band for an arc, point for a text), which spread their categories over the full circle.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_PolarDefaultsAreLinearZeroNoNice()
    {
        // Arrange / Act: values 3..12 on theta
        var theta = Resolve(PlainData, "arc", """{ "theta": {"field":"q","type":"quantitative","stack":false} }""")["theta"]!;

        // Assert: linear, zero included, no nice and no padding, a plain field (not stacked, no step)
        Assert.Equal(ScaleKind.Linear, theta.Kind);
        Assert.Equal(ScaleTransformKind.Linear, theta.Transform.Kind);
        Assert.Equal((0.0, 12.0), (theta.DomainStart, theta.DomainEnd));
        Assert.Null(theta.NiceCount);
        Assert.Equal(0, theta.PaddingPixels);
        Assert.Null(theta.Step);
        Assert.Null(theta.NaturalExtent);
        Assert.Null(theta.Stack);

        // Assert: zero false keeps the data extent, and the explicit domain keeps its ends (no nice either)
        var noZero = Resolve(PlainData, "arc", """{ "theta": {"field":"q","type":"quantitative","stack":false,"scale":{"zero":false}} }""")["theta"]!;
        Assert.Equal((3.0, 12.0), (noZero.DomainStart, noZero.DomainEnd));
        Assert.Null(noZero.NiceCount);
        var fixedDomain = Resolve(PlainData, "arc", """{ "theta": {"field":"q","type":"quantitative","stack":false,"scale":{"domain":[2, 9.5]}} }""")["theta"]!;
        Assert.Equal((2.0, 9.5), (fixedDomain.DomainStart, fixedDomain.DomainEnd));
        Assert.Null(fixedDomain.NiceCount);

        // Assert: the configuration turns the zero rule off for every polar scale
        var tree = SpecParser.Parse("""{ "config": {"scale": {"zero": false}}, "data": {"values": [{"q": 3}, {"q": 12}]}, "mark": "arc", "encoding": {"radius": {"field":"q","type":"quantitative","stack":false}} }""");
        var table = DataLoader.Load(tree.Root["data"], tree.SourceName, null, Ct);
        var configured = ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), tree.SourceName, null, Ct)["radius"]!;
        Assert.Equal((3.0, 12.0), (configured.DomainStart, configured.DomainEnd));

        // Assert: a plain polar field with its default zero rule: the field is not a bin
        Assert.Equal(0, theta.DomainStart);

        // Act: categories; an arc has bands, a text has points, both over [0, 2 * PI] without padding
        var band = Resolve(PlainData, "arc", """{ "theta": {"field":"k","type":"nominal"} }""")["theta"]!;
        var point = Resolve(PlainData, "text", """{ "theta": {"field":"k","type":"nominal"} }""")["theta"]!;
        Assert.Equal(ScaleKind.Band, band.Kind);
        Assert.Equal(ScaleKind.Point, point.Kind);
        Assert.Equal([S("a"), S("b"), S("c")], band.DiscreteDomain);
        Assert.Null(band.Step);
        Assert.Null(band.NaturalExtent);

        // Assert: three bands of 2 * PI / 3 start at 0, 2 * PI / 3 and 4 * PI / 3; three points sit at 0, PI and 2 * PI
        var bandModel = band.CreateModel(0);
        Assert.Equal(2 * Math.PI / 3, bandModel.Bandwidth, Precision);
        Assert.Equal(0, bandModel.Map(S("a"))!.Value, Precision);
        Assert.Equal(4 * Math.PI / 3, bandModel.Map(S("c"))!.Value, Precision);
        var pointModel = point.CreateModel(0);
        Assert.Equal(0, pointModel.Map(S("a"))!.Value, Precision);
        Assert.Equal(Math.PI, pointModel.Map(S("b"))!.Value, Precision);
        Assert.Equal(2 * Math.PI, pointModel.Map(S("c"))!.Value, Precision);

        // Assert: a radius is a band over [0, extent] on an arc
        var radiusBand = Resolve(PlainData, "arc", """{ "radius": {"field":"k","type":"nominal"} }""")["radius"]!.CreateModel(90);
        Assert.Equal(30, radiusBand.Bandwidth, Precision);
        Assert.Equal(60, radiusBand.Map(S("c"))!.Value, Precision);
    }

    /// <summary>
    ///     Validates that a stacked polar channel's domain spans the start and the end of the stack, that a normalized stack is the unit interval, and that the scale of the
    ///     dimension channel is not stacked.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_StackedThetaDomainSpansTheStack()
    {
        // Arrange / Act: slices 1, 1 and 2 stack to 0..1, 1..2, 2..4
        var pie = Resolve(PieData, "arc", """{ "theta": {"field":"v","type":"quantitative"}, "color": {"field":"c","type":"nominal"} }""");
        var theta = pie["theta"]!;

        // Assert: the domain is the extent of both ends of the stack: 0 to 4 (the unstacked extent would be 1 to 2), with the derived end fields
        Assert.NotNull(theta.Stack);
        Assert.Equal(StackOffset.Zero, theta.Stack);
        Assert.Equal((0.0, 4.0), (theta.DomainStart, theta.DomainEnd));
        Assert.NotNull(theta.FieldPath);
        Assert.NotNull(theta.Field2Path);
        Assert.NotEqual(theta.FieldPath, theta.Field2Path);

        // Assert: a normalized stack always spans the unit interval
        var normalized = Resolve(PieData, "arc", """{ "theta": {"field":"v","type":"quantitative","stack":"normalize"}, "color": {"field":"c","type":"nominal"} }""")["theta"]!;
        Assert.Equal(StackOffset.Normalize, normalized.Stack);
        Assert.Equal((0.0, 1.0), (normalized.DomainStart, normalized.DomainEnd));

        // Assert: the same for a stacked radius, whose dimension is theta: the categories x and y stack the layers a and b to 3 and 1 (the unstacked extent would be 0 to 2)
        const string layered = """
            "data": { "values": [ { "t": "x", "g": "a", "v": 1 }, { "t": "x", "g": "b", "v": 2 }, { "t": "y", "g": "a", "v": 1 } ] }
            """;
        var radial = Resolve(layered, "arc", """{ "radius": {"field":"v","type":"quantitative"}, "theta": {"field":"t","type":"nominal"}, "color": {"field":"g","type":"nominal"} }""");
        Assert.Equal(StackOffset.Zero, radial["radius"]!.Stack);
        Assert.Equal((0.0, 3.0), (radial["radius"]!.DomainStart, radial["radius"]!.DomainEnd));
        Assert.Equal(ScaleKind.Band, radial["theta"]!.Kind);
        Assert.Null(radial["theta"]!.Stack);

        // Assert: an unaggregated field with nothing to split it is stacked too (every row is a slice of the one stack: 3 + 7 + 12 + 5 = 27); "stack": false switches it off
        var rows = Resolve(PlainData, "arc", """{ "theta": {"field":"q","type":"quantitative"} }""")["theta"]!;
        Assert.Equal((0.0, 27.0), (rows.DomainStart, rows.DomainEnd));

        // Assert: a text mark stacks its theta the same way
        var label = Resolve(PieData, "text", """{ "theta": {"field":"v","type":"quantitative","stack":true}, "text": {"field":"c"}, "color": {"field":"c","type":"nominal"} }""")["theta"]!;
        Assert.Equal((0.0, 4.0), (label.DomainStart, label.DomainEnd));
    }

    /// <summary>
    ///     Validates that the polar channels are scales of the <c>arc</c> and <c>text</c> marks only.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_PolarChannelsAreOnlyResolvedForArcAndText()
    {
        // Arrange / Act / Assert: every other mark drops them, whatever their field type
        foreach (var mark in new[] { "point", "bar", "line", "area", "rect", "tick", "rule", "trail", "circle", "square" })
        {
            var scales = Resolve(PlainData, mark, """{ "theta": {"field":"q","type":"quantitative"}, "radius": {"field":"q","type":"quantitative"} }""");
            Assert.Null(scales["theta"]);
            Assert.Null(scales["radius"]);
        }

        // Assert: a value or a missing field has no scale, and the scales keep the channel order x, y, theta, radius, color
        var arc = Resolve(
            PlainData,
            "arc",
            """{ "color": {"field":"k","type":"nominal"}, "radius": {"value": 50}, "theta": {"field":"q","type":"quantitative"}, "x": {"datum": 1} }""");
        Assert.Equal(["x", "theta", "color"], arc.Scales.Select(s => s.Channel).ToArray());
    }

    /// <summary>
    ///     Validates the orientation of an area (a <c>y2</c> makes it vertical, an <c>x2</c> horizontal, unless the axis is already binned), the temporal rule of line, area and tick, and the
    ///     fixed vertical orientation of trail and arc.
    /// </summary>
    [Fact]
    public void ScaleResolver_InferOrient_AreaRangeAndTemporalRulesFollowVegaLite()
    {
        // Arrange: (mark, specified orient, x type, y type, extra channels, expected orient)
        var vectors = new (string Mark, string? Specified, string? X, string? Y, string Extra, string? Expected)[]
        {
            // an area with y2 is vertical, with x2 horizontal; y2 wins when both exist; the specified orient does not matter
            ("area", null, "temporal", "quantitative", ",\"y2\":{\"field\":\"q\"}", "vertical"),
            ("area", "horizontal", "temporal", "quantitative", ",\"y2\":{\"field\":\"q\"}", "vertical"),
            ("area", null, "quantitative", "temporal", ",\"x2\":{\"field\":\"q\"}", "horizontal"),
            ("area", "vertical", "quantitative", "temporal", ",\"x2\":{\"field\":\"q\"}", "horizontal"),
            ("area", null, "quantitative", "quantitative", ",\"x2\":{\"field\":\"q\"},\"y2\":{\"field\":\"q\"}", "vertical"),
            ("area", null, "quantitative", "quantitative", ",\"y2\":{\"value\":0}", "vertical"),

            // an area whose second channel is on the axis with the bins decides by that axis alone
            ("area", null, null, "nominal", ",\"y2\":{\"field\":\"q\"}", "vertical"),

            // a time axis is the dimension of a line, an area and a tick when no measure decides
            ("line", null, "temporal", "nominal", string.Empty, "vertical"),
            ("line", null, "nominal", "temporal", string.Empty, "horizontal"),
            ("area", null, "temporal", "nominal", string.Empty, "vertical"),
            ("area", null, "ordinal", "temporal", string.Empty, "horizontal"),
            ("tick", null, "temporal", "nominal", string.Empty, "vertical"),
            ("tick", null, "nominal", "temporal", string.Empty, "horizontal"),
            ("tick", null, "temporal", null, string.Empty, "vertical"),
            ("line", null, null, "temporal", string.Empty, "horizontal"),

            // two time axes, two categories or none give no orientation; so does a mark that has no such rule
            ("line", null, "temporal", "temporal", string.Empty, null),
            ("tick", null, "temporal", "temporal", string.Empty, null),
            ("line", null, "nominal", "nominal", string.Empty, null),
            ("area", null, null, null, string.Empty, null),

            // a measure decides before time does, and an explicit orient wins over time
            ("line", null, "temporal", "quantitative", string.Empty, "vertical"),
            ("tick", null, "quantitative", "temporal", string.Empty, "vertical"),
            ("line", "horizontal", "temporal", "nominal", string.Empty, "horizontal"),
            ("tick", "horizontal", "temporal", "nominal", string.Empty, "horizontal"),

            // a trail and an arc are always vertical
            ("trail", null, "quantitative", "nominal", string.Empty, "vertical"),
            ("trail", "horizontal", "nominal", "quantitative", string.Empty, "vertical"),
            ("arc", null, "nominal", "quantitative", string.Empty, "vertical"),
            ("arc", null, null, null, string.Empty, "vertical"),
        };

        // Act / Assert
        foreach (var (mark, specified, xType, yType, extra, expected) in vectors)
        {
            Assert.Equal(expected, ScaleResolver.InferOrient(mark, specified, Encoding(xType, yType, extra)));
        }

        // Assert: bins that are in the data decide the direction of a range: an area on a binned y with a y2 is horizontal, on a binned x with an x2 vertical
        var binnedY = SpecParser.Parse("""{ "y": {"field":"q","type":"quantitative","bin":"binned"}, "y2": {"field":"q"} }""").Root;
        var binnedX = SpecParser.Parse("""{ "x": {"field":"q","type":"quantitative","bin":"binned"}, "x2": {"field":"q"} }""").Root;
        var binnedBoth = SpecParser.Parse("""{ "x": {"field":"q","type":"quantitative","bin":true}, "y": {"field":"q","type":"quantitative","bin":"binned"}, "y2": {"field":"q"} }""").Root;
        Assert.Equal("horizontal", ScaleResolver.InferOrient("area", null, binnedY));
        Assert.Equal("vertical", ScaleResolver.InferOrient("area", null, binnedX));
        Assert.Equal("horizontal", ScaleResolver.InferOrient("area", "vertical", binnedBoth));

        // Assert: the orientation reaches the scales; the ticks of a time axis over categories are vertical, and horizontal when the time is on y
        Assert.Equal("vertical", Resolve(PlainData, "tick", """{ "x": {"field":"q","type":"temporal"}, "y": {"field":"k","type":"nominal"} }""").Orient);
        Assert.Equal("horizontal", Resolve(PlainData, "tick", """{ "x": {"field":"k","type":"nominal"}, "y": {"field":"q","type":"temporal"} }""").Orient);
        Assert.Equal("vertical", Resolve(PlainData, "trail", """{ "x": {"field":"q","type":"quantitative"}, "y": {"field":"k","type":"nominal"} }""").Orient);
    }

    /// <summary>
    ///     Validates the orientation of a line: it extends along the dimension, so an unbinned quantitative x next to a y that is not one is horizontal and the mirror is vertical,
    ///     two measures are vertical, and an explicit orient wins.
    /// </summary>
    [Fact]
    public void ScaleResolver_InferOrient_LineFollowsTheMeasure()
    {
        // Arrange: (specified orient, x type, y type, expected orient)
        var vectors = new (string? Specified, string? X, string? Y, string? Expected)[]
        {
            (null, "quantitative", "nominal", "horizontal"),
            (null, "nominal", "quantitative", "vertical"),
            (null, "quantitative", "quantitative", "vertical"),
            (null, "quantitative", null, "horizontal"),
            (null, null, "quantitative", "vertical"),
            ("horizontal", "quantitative", "quantitative", "horizontal"),
            ("vertical", "quantitative", "nominal", "vertical"),
            (null, "ordinal", "nominal", null),
            (null, null, null, null),
        };

        // Act / Assert
        foreach (var (specified, xType, yType, expected) in vectors)
        {
            Assert.Equal(expected, ScaleResolver.InferOrient("line", specified, Encoding(xType, yType)));
        }

        // Assert: a binned or aggregated count field next to a plain one: a bin is not a measure, a count is
        var binned = SpecParser.Parse("""{ "x": {"field":"q","type":"quantitative","bin":true}, "y": {"aggregate":"count","type":"quantitative"} }""").Root;
        Assert.Equal("vertical", ScaleResolver.InferOrient("line", null, binned));
        var counted = SpecParser.Parse("""{ "x": {"field":"q","type":"quantitative"}, "y": {"field":"k","type":"nominal"} }""").Root;
        Assert.Equal("horizontal", ScaleResolver.InferOrient("line", null, counted));

        // Assert: the orientation of the view is the one the scales report, and a line does not stack by orientation
        Assert.Equal("horizontal", Resolve(PlainData, "line", """{ "x": {"field":"q","type":"quantitative"}, "y": {"field":"k","type":"nominal"} }""").Orient);
        Assert.Equal("vertical", Resolve(PlainData, "line", """{ "x": {"field":"k","type":"nominal"}, "y": {"field":"q","type":"quantitative"} }""").Orient);
    }
}
