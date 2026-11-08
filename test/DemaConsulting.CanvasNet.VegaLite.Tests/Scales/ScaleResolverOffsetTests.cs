using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the nested offset scales of <c>xOffset</c> and <c>yOffset</c> (Vega-Lite 6: <c>channelHasNestedOffsetScale</c>, the offset paddings and the step that the offset domain sizes), with the numbers
///     derived by hand: a grouped bar chart of 3 categories and 2 groups has an offset band step of 20, so the position step is 20 * 2 / (1 - 0.2) = 50 and its width 50 * (3 - 0.2 + 2 * 0.2) = 160.
/// </summary>
public class ScaleResolverOffsetTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The rows of a grouped bar chart: categories A, B and C with the groups x and y (C has only x).</summary>
    private const string Rows = """
        [ { "c": "A", "g": "x", "v": 1 }, { "c": "A", "g": "y", "v": 2 }, { "c": "B", "g": "x", "v": 3 }, { "c": "B", "g": "y", "v": 4 }, { "c": "C", "g": "x", "v": 5 } ]
        """;

    /// <summary>
    ///     Builds a view over the rows.
    /// </summary>
    /// <param name="mark">The mark JSON.</param>
    /// <param name="encoding">The encoding JSON (the object text).</param>
    /// <param name="top">More top-level properties starting with a comma, or empty.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string encoding, string top = "") =>
        "{ \"data\": { \"values\": " + Rows + " }, \"mark\": " + mark + ", \"encoding\": " + encoding + top + " }";

    /// <summary>The encoding of a grouped bar chart: category on x, group on xOffset, value on y.</summary>
    private const string Grouped = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""";

    /// <summary>
    ///     Validates that the offset of a bar is a band scale without padding by default and that the position scale gets the nested paddings of 0.2.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_NominalXOffsetOfABarIsBandWithoutPaddingAndXHasNestedPaddings()
    {
        // Act
        var scales = ChartTestHelpers.Parts(Spec("\"bar\"", Grouped)).Scales;

        // Assert
        var x = scales["x"]!;
        var offset = scales["xOffset"]!;
        Assert.Equal((ScaleKind.Band, 0.2, 0.2), (x.Kind, x.PaddingInner, x.PaddingOuter));
        Assert.Equal((ScaleKind.Band, 0.0, 0.0), (offset.Kind, offset.PaddingInner, offset.PaddingOuter));
        Assert.Equal(["x", "y"], offset.DiscreteDomain.Select(d => d.Text));
        Assert.True(offset.IsOffset);
        Assert.Null(offset.Step);
    }

    /// <summary>
    ///     Validates the default step: the step of x is 50, its natural width is 160 and every bar is 20 pixels wide.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_DefaultStepIsScaledByTheOffsetCount()
    {
        // Act
        var scales = ChartTestHelpers.Parts(Spec("\"bar\"", Grouped)).Scales;
        var model = scales["x"]!.CreateModel(scales["x"]!.NaturalExtent!.Value);
        var offsetModel = scales["xOffset"]!.CreateModel(model.Bandwidth);

        // Assert: step 20 * 2 / 0.8 = 50, 50 * (3 - 0.2 + 0.4) = 160, band width 40, two bars of 20
        Assert.Equal(50, scales["x"]!.Step!.Value, 9);
        Assert.Equal(160, scales["x"]!.NaturalExtent!.Value, 9);
        Assert.Equal(40, model.Bandwidth, 9);
        Assert.Equal(20, offsetModel.Bandwidth, 9);
    }

    /// <summary>
    ///     Validates that a fixed width gives the bands the room that is left: 200 pixels have the step 62.5, the band width 50 and bars of 25.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_FixedWidthLetsTheOffsetFillTheBandwidth()
    {
        // Act
        var scales = ChartTestHelpers.Parts(Spec("\"bar\"", Grouped, ", \"width\": 200")).Scales;
        var model = scales["x"]!.CreateModel(200);
        var offsetModel = scales["xOffset"]!.CreateModel(model.Bandwidth);

        // Assert: 200 / (3 - 0.2 + 0.4) = 62.5, bandwidth 62.5 * 0.8 = 50, bars 25
        Assert.Null(scales["x"]!.Step);
        Assert.Equal(62.5, model.Step, 9);
        Assert.Equal(50, model.Bandwidth, 9);
        Assert.Equal(25, offsetModel.Bandwidth, 9);
    }

    /// <summary>
    ///     Validates that <c>for: "position"</c> keeps the step on the position scale, so the bars share its band.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_StepForPositionKeepsTheStep()
    {
        // Act
        var scales = ChartTestHelpers.Parts(Spec("\"bar\"", Grouped, ", \"width\": { \"step\": 30, \"for\": \"position\" }")).Scales;

        // Assert
        Assert.Equal(30, scales["x"]!.Step!.Value, 9);
    }

    /// <summary>
    ///     Validates that the explicit <c>for: "offset"</c> is what the default does, and that a height step sizes a horizontal chart.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_StepForOffsetAppliesToTheHeightOfAHorizontalChart()
    {
        // Arrange
        const string horizontal = """{ "y": { "field": "c", "type": "nominal" }, "yOffset": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative" } }""";

        // Act
        var explicitOffset = ChartTestHelpers.Parts(Spec("\"bar\"", horizontal, ", \"height\": { \"step\": 10, \"for\": \"offset\" }")).Scales["y"]!;
        var byDefault = ChartTestHelpers.Parts(Spec("\"bar\"", horizontal)).Scales["y"]!;

        // Assert: 10 * 2 / 0.8 = 25, and the default step of 20 gives 50
        Assert.Equal(25, explicitOffset.Step!.Value, 9);
        Assert.Equal(50, byDefault.Step!.Value, 9);
    }

    /// <summary>
    ///     Validates that a point mark gets a band for its position and a point scale with an outer padding of half a step for its offset.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_PointMarkHasBandPositionAndPointOffset()
    {
        // Act
        var scales = ChartTestHelpers.Parts(Spec("\"point\"", Grouped)).Scales;

        // Assert: the position is a band (not a point scale) whatever the mark is, the offset a point scale; its step is 20 * (2 - 1 + 1) / 0.8 = 50
        Assert.Equal(ScaleKind.Band, scales["x"]!.Kind);
        Assert.Equal((ScaleKind.Point, 0.5), (scales["xOffset"]!.Kind, scales["xOffset"]!.PaddingOuter));
        Assert.Equal(50, scales["x"]!.Step!.Value, 9);
    }

    /// <summary>
    ///     Validates that an explicit <c>point</c> type on the position of an offset is not accepted: the band is used (and the catalog warns).
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_ExplicitPointOnAPositionWithOffsetUsesBand()
    {
        // Act
        var encoding = Grouped.Replace("\"field\": \"c\", \"type\": \"nominal\"", "\"field\": \"c\", \"type\": \"nominal\", \"scale\": { \"type\": \"point\" }", StringComparison.Ordinal);
        var scales = ChartTestHelpers.Parts(Spec("\"point\"", encoding)).Scales;

        // Assert
        Assert.Equal(ScaleKind.Band, scales["x"]!.Kind);
        Assert.False(ScaleResolver.AcceptsScaleType("x", "nominal", "point", nestedOffset: true));
        Assert.True(ScaleResolver.AcceptsScaleType("x", "nominal", "point"));
        Assert.True(ScaleResolver.AcceptsScaleType("xOffset", "nominal", "point"));
        Assert.True(ScaleResolver.AcceptsScaleType("xOffset", "quantitative", "linear"));
        Assert.False(ScaleResolver.AcceptsScaleType("yOffset", "nominal", "linear"));
    }

    /// <summary>
    ///     Validates the paddings of the offset: <c>scale.padding</c> on the position drops the nested defaults, the offset takes <c>paddingInner</c>, and the configuration has the
    ///     offset band paddings and the nested offset paddings.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_PaddingsFollowTheScaleAndTheConfiguration()
    {
        // Arrange
        var padded = Grouped
            .Replace("\"field\": \"c\", \"type\": \"nominal\"", "\"field\": \"c\", \"type\": \"nominal\", \"scale\": { \"padding\": 0.1 }", StringComparison.Ordinal)
            .Replace("\"field\": \"g\", \"type\": \"nominal\"", "\"field\": \"g\", \"type\": \"nominal\", \"scale\": { \"paddingInner\": 0.25 }", StringComparison.Ordinal);
        const string config = """{ "scale": { "bandWithNestedOffsetPaddingInner": 0.3, "bandWithNestedOffsetPaddingOuter": 0.1, "offsetBandPaddingInner": 0.05, "offsetBandPaddingOuter": 0.02 } }""";

        // Act
        var own = ChartTestHelpers.Parts(Spec("\"bar\"", padded)).Scales;
        var configured = ChartTestHelpers.Parts(Spec("\"bar\"", Grouped), config).Scales;

        // Assert: padding 0.1 sets both paddings of x, the explicit inner padding of the offset stays and its outer padding is 0
        Assert.Equal((0.1, 0.1), (own["x"]!.PaddingInner, own["x"]!.PaddingOuter));
        Assert.Equal((0.25, 0.0), (own["xOffset"]!.PaddingInner, own["xOffset"]!.PaddingOuter));
        Assert.Equal((0.3, 0.1), (configured["x"]!.PaddingInner, configured["x"]!.PaddingOuter));
        Assert.Equal((0.05, 0.02), (configured["xOffset"]!.PaddingInner, configured["xOffset"]!.PaddingOuter));
    }

    /// <summary>
    ///     Validates that the offset paddings change the step: an inner padding of 0.25 of the offset gives 20 * (2 - 0.25) / 0.8 = 43.75.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OffsetPaddingChangesThePositionStep()
    {
        // Act
        var encoding = Grouped.Replace("\"field\": \"g\", \"type\": \"nominal\"", "\"field\": \"g\", \"type\": \"nominal\", \"scale\": { \"paddingInner\": 0.25 }", StringComparison.Ordinal);
        var scales = ChartTestHelpers.Parts(Spec("\"bar\"", encoding)).Scales;

        // Assert
        Assert.Equal(43.75, scales["x"]!.Step!.Value, 9);
    }

    /// <summary>
    ///     Validates the domain of the offset: the sorted values of the whole field (a group that a category lacks still has its slot), and the sort of the offset.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OffsetDomainIsTheWholeFieldAndFollowsItsSort()
    {
        // Act
        var descending = Grouped.Replace("\"field\": \"g\", \"type\": \"nominal\"", "\"field\": \"g\", \"type\": \"nominal\", \"sort\": \"descending\"", StringComparison.Ordinal);
        var ascending = ChartTestHelpers.Parts(Spec("\"bar\"", Grouped)).Scales["xOffset"]!;
        var reversed = ChartTestHelpers.Parts(Spec("\"bar\"", descending)).Scales["xOffset"]!;
        var byArray = ChartTestHelpers.Parts(Spec("\"bar\"", Grouped.Replace("\"field\": \"g\", \"type\": \"nominal\"", "\"field\": \"g\", \"type\": \"nominal\", \"sort\": [\"y\", \"x\"]", StringComparison.Ordinal))).Scales["xOffset"]!;

        // Assert
        Assert.Equal(["x", "y"], ascending.DiscreteDomain.Select(d => d.Text));
        Assert.Equal(["y", "x"], reversed.DiscreteDomain.Select(d => d.Text));
        Assert.Equal(["y", "x"], byArray.DiscreteDomain.Select(d => d.Text));
    }

    /// <summary>
    ///     Validates the continuous offset of a point-like mark: a linear scale without zero and nice, running from the bandwidth to zero on y (like y) and from zero up on x.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_QuantitativeOffsetIsLinearWithoutZeroOrNice()
    {
        // Arrange: a strip plot: the category on x and a quantitative xOffset on a point mark
        const string strip = """{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative" } }""";
        const string vertical = """{ "x": { "field": "c", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "yOffset": { "field": "v", "type": "quantitative" } }""";

        // Act
        var x = ChartTestHelpers.Parts(Spec("\"point\"", strip)).Scales["xOffset"]!;
        var parts = ChartTestHelpers.Parts(Spec("\"point\"", vertical));

        // Assert: x is nominal (the band), y is the measure; the offset has the domain of the data, 1 to 5, as it is
        Assert.Equal((ScaleKind.Linear, 1.0, 5.0, null), (x.Kind, x.DomainStart, x.DomainEnd, x.NiceCount));
        Assert.Null(parts.Scales["yOffset"]);
        Assert.Equal(0.0, x.CreateModel(40).Map(1.0), 9);
        Assert.Equal(40.0, x.CreateModel(40).Map(5.0), 9);
    }

    /// <summary>
    ///     Validates that an offset without a discrete position or a time unit has no scale: the position is missing, quantitative, or has a time unit of the day of the week.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OffsetWithoutADiscreteParentHasNoScale()
    {
        // Act
        var quantitative = ChartTestHelpers.Parts(Spec("\"point\"", """{ "x": { "field": "v", "type": "quantitative" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;
        var missing = ChartTestHelpers.Parts(Spec("\"point\"", """{ "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;
        var day = ChartTestHelpers.Parts(Spec("\"bar\"", """{ "x": { "field": "c", "type": "temporal", "timeUnit": "day" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;

        // Assert
        Assert.Null(quantitative["xOffset"]);
        Assert.Null(missing["xOffset"]);
        Assert.Null(day["xOffset"]);
        Assert.Equal(ScaleKind.Linear, quantitative["x"]!.Kind);
    }

    /// <summary>
    ///     Validates that a nominal or ordinal position with a time unit is an ordinary band parent (Vega-Lite's <c>channelHasNestedOffsetScale</c> accepts a discrete type, with or without a time unit): the
    ///     position is a band scale with the nested paddings and the offset has a scale of its own, whatever the unit and the mark.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OffsetWithADiscreteParentAndATimeUnitHasANestedOffsetScale()
    {
        // Act
        var scales = ChartTestHelpers.Parts(Spec("\"bar\"", """{ "x": { "field": "c", "type": "nominal", "timeUnit": "year" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;
        var point = ChartTestHelpers.Parts(Spec("\"point\"", """{ "x": { "field": "c", "type": "ordinal", "timeUnit": "month" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;
        var rect = ChartTestHelpers.Parts(Spec("\"rect\"", """{ "x": { "field": "c", "type": "nominal", "timeUnit": "year" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;
        var day = ChartTestHelpers.Parts(Spec("\"bar\"", """{ "x": { "field": "c", "type": "nominal", "timeUnit": "day" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;

        // Assert
        var x = scales["x"]!;
        var offset = scales["xOffset"]!;
        Assert.Equal((ScaleKind.Band, 0.2, 0.2), (x.Kind, x.PaddingInner, x.PaddingOuter));
        Assert.Equal((ScaleKind.Band, 0.0, 0.0), (offset.Kind, offset.PaddingInner, offset.PaddingOuter));
        Assert.Equal(["x", "y"], offset.DiscreteDomain.Select(d => d.Text));
        Assert.True(offset.IsOffset);
        Assert.Equal(ScaleKind.Point, point["xOffset"]!.Kind);
        Assert.Equal(ScaleKind.Band, rect["xOffset"]!.Kind);
        Assert.Equal(ScaleKind.Band, day["xOffset"]!.Kind);
    }

    /// <summary>
    ///     Validates that the offset of a temporal position with a time unit has a scale of its own while the position stays a time scale (Vega-Lite's <c>channelHasNestedOffsetScale</c> accepts a field with a time unit): a
    ///     nominal offset of a bar is a band scale without padding, and the position has no band paddings.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OffsetWithATimeUnitParentHasANominalOffsetScale()
    {
        // Act
        var scales = ChartTestHelpers.Parts(Spec("\"bar\"", """{ "x": { "field": "c", "type": "temporal", "timeUnit": "year" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;
        var point = ChartTestHelpers.Parts(Spec("\"point\"", """{ "x": { "field": "c", "type": "temporal", "timeUnit": "year" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""")).Scales;

        // Assert
        Assert.Equal(ScaleKind.Time, scales["x"]!.Kind);
        var offset = scales["xOffset"]!;
        Assert.Equal((ScaleKind.Band, 0.0, 0.0), (offset.Kind, offset.PaddingInner, offset.PaddingOuter));
        Assert.Equal(["x", "y"], offset.DiscreteDomain.Select(d => d.Text));
        Assert.Equal(ScaleKind.Point, point["xOffset"]!.Kind);
    }

    /// <summary>
    ///     Validates that the range that a caller gives replaces the default range of an offset scale (a share of a time unit) and that <c>rangeMin</c>, an explicit range and <c>reverse</c> still apply to it.
    /// </summary>
    [Fact]
    public void ScaleResolver_CreateModel_NestedRangeReplacesTheDefaultRange()
    {
        // Arrange
        const string timeParent = """{ "x": { "field": "c", "type": "temporal", "timeUnit": "year" }, "xOffset": { "field": "g", "type": "nominal"SCALE }, "y": { "field": "v", "type": "quantitative" } }""";
        ResolvedScale Offset(string scale, string mark = "\"bar\"") => ChartTestHelpers.Parts(Spec(mark, timeParent.Replace("SCALE", scale, StringComparison.Ordinal))).Scales["xOffset"]!;

        // Act
        var plain = Offset(string.Empty).CreateModel(0, (10, 50));
        var rangeMin = Offset(", \"scale\": { \"rangeMin\": 20 }").CreateModel(0, (10, 50));
        var explicitRange = Offset(", \"scale\": { \"range\": [0, 100] }").CreateModel(0, (10, 50));
        var reversed = Offset(", \"scale\": { \"reverse\": true }").CreateModel(0, (10, 50));
        var descending = Offset(string.Empty).CreateModel(0, (-10, -50));
        var point = Offset(string.Empty, "\"point\"").CreateModel(0, (10, 50));
        var unchanged = Offset(string.Empty).CreateModel(40);

        // Assert: the band offset has two bands of 20 pixels in [10, 50]
        Assert.Equal((10.0, 30.0), (plain.Map(DataValue.FromString("x")) ?? -1, plain.Map(DataValue.FromString("y")) ?? -1));
        Assert.Equal(20.0, plain.Bandwidth, 9);
        Assert.Equal((20.0, 35.0), (rangeMin.Map(DataValue.FromString("x")) ?? -1, rangeMin.Map(DataValue.FromString("y")) ?? -1));
        Assert.Equal((0.0, 50.0), (explicitRange.Map(DataValue.FromString("x")) ?? -1, explicitRange.Map(DataValue.FromString("y")) ?? -1));
        Assert.Equal((30.0, 10.0), (reversed.Map(DataValue.FromString("x")) ?? -1, reversed.Map(DataValue.FromString("y")) ?? -1));
        Assert.Equal((-30.0, -50.0), (descending.Map(DataValue.FromString("x")) ?? 1, descending.Map(DataValue.FromString("y")) ?? 1));
        Assert.True(point.Map(DataValue.FromString("x")) < point.Map(DataValue.FromString("y")));
        Assert.Equal((0.0, 20.0), (unchanged.Map(DataValue.FromString("x")) ?? -1, unchanged.Map(DataValue.FromString("y")) ?? -1));
    }

    /// <summary>
    ///     Validates the decision of <see cref="ScaleResolver.DecideOffset"/> for every case, with the marks that cannot draw a continuous offset (a ranged offset of a bar or area is drawn since Phase 14: an unbinned
    ///     quantitative offset of a discrete position without a secondary channel; <c>isRangedOffset</c> of Vega-Lite 6.5.0).
    /// </summary>
    [Theory]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "Supported")]
    [InlineData("""{ "x": { "field": "c", "type": "ordinal" }, "xOffset": { "field": "g", "type": "ordinal" } }""", "point", "Supported")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "quantitative" } }""", "point", "Supported")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "quantitative" } }""", "bar", "Ranged")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "quantitative" } }""", "area", "Ranged")]
    [InlineData("""{ "x": { "field": "v", "type": "quantitative" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "NoDiscreteParent")]
    [InlineData("""{ "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "NoDiscreteParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "year" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "TimeUnitParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "month" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "TimeUnitParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "yearmonthdate" }, "xOffset": { "field": "g", "type": "nominal" } }""", "point", "TimeUnitParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "hours" }, "xOffset": { "field": "g", "type": "nominal" } }""", "tick", "TimeUnitParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": { "unit": "month", "step": 2 } }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "TimeUnitParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "month" }, "xOffset": { "field": "v", "type": "quantitative" } }""", "point", "TimeUnitParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "month" }, "xOffset": { "field": "v", "type": "quantitative" } }""", "bar", "UnsupportedContinuous")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "temporal" } }""", "bar", "UnsupportedContinuous")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "quantitative", "bin": true } }""", "bar", "UnsupportedContinuous")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "x2": { "field": "w" }, "xOffset": { "field": "v", "type": "quantitative" } }""", "bar", "UnsupportedContinuous")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "v", "type": "quantitative", "aggregate": "sum" } }""", "area", "Ranged")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "day" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "NoDiscreteParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "yearmonthdate" }, "xOffset": { "field": "g", "type": "nominal" } }""", "rect", "NoDiscreteParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": { "maxbins": 10 } }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "NoDiscreteParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal", "timeUnit": "binnedyearmonth" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "NoDiscreteParent")]
    [InlineData("""{ "x": { "field": "c", "type": "temporal" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "NoDiscreteParent")]
    [InlineData("""{ "x": { "field": "v", "type": "quantitative", "timeUnit": "month" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "NoDiscreteParent")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal", "timeUnit": "month" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "Supported")]
    [InlineData("""{ "x": { "field": "c", "type": "ordinal", "timeUnit": "year" }, "xOffset": { "field": "g", "type": "nominal" } }""", "rect", "Supported")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal", "timeUnit": "day" }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "Supported")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal", "timeUnit": { "maxbins": 10 } }, "xOffset": { "field": "g", "type": "nominal" } }""", "bar", "Supported")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal", "timeUnit": "month" }, "xOffset": { "field": "v", "type": "quantitative" } }""", "bar", "Ranged")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal", "timeUnit": "month" }, "xOffset": { "field": "v", "type": "quantitative" } }""", "point", "Supported")]
    [InlineData("""{ "x": { "field": "c", "type": "nominal" }, "xOffset": { "value": 3 } }""", "bar", "Supported")]
    public void ScaleResolver_DecideOffset_FollowsTheRules(string encoding, string mark, string expected)
    {
        // Arrange
        var tree = SpecParser.Parse("{ \"mark\": \"" + mark + "\", \"encoding\": " + encoding + " }");

        // Act
        var decision = ScaleResolver.DecideOffset("xOffset", tree.Root["encoding"], mark);

        // Assert
        Assert.Equal(Enum.Parse<OffsetDecision>(expected), decision);
        Assert.Throws<ArgumentNullException>(() => ScaleResolver.DecideOffset(null!, null, mark));
    }

    /// <summary>
    ///     Validates the horizontal variant: <c>yOffset</c> nests in the band of y and its band runs from the top.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_YOffsetNestsInTheBandOfY()
    {
        // Arrange
        const string horizontal = """{ "y": { "field": "c", "type": "nominal" }, "yOffset": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative" } }""";

        // Act
        var scales = ChartTestHelpers.Parts(Spec("\"bar\"", horizontal)).Scales;
        var model = scales["yOffset"]!.CreateModel(40);

        // Assert
        Assert.Equal((ScaleKind.Band, 0.2), (scales["y"]!.Kind, scales["y"]!.PaddingInner));
        Assert.Equal(0.0, model.Map(DataValue.FromString("x")) ?? -1, 9);
        Assert.Equal(20.0, model.Map(DataValue.FromString("y")) ?? -1, 9);
    }

    /// <summary>
    ///     Validates an explicit range, the <c>reverse</c> and the <c>round</c> of the offset scale.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_OffsetScaleTakesRangeReverseAndRound()
    {
        // Act
        var encoding = Grouped.Replace("\"field\": \"g\", \"type\": \"nominal\"", "\"field\": \"g\", \"type\": \"nominal\", \"scale\": { \"reverse\": true, \"round\": true }", StringComparison.Ordinal);
        var scale = ChartTestHelpers.Parts(Spec("\"bar\"", encoding)).Scales["xOffset"]!;
        var model = scale.CreateModel(41);

        // Assert: reversed, so y comes first
        Assert.True(scale.Reverse);
        Assert.True(scale.Round);
        Assert.True(model.Map(DataValue.FromString("y")) < model.Map(DataValue.FromString("x")));
    }

    /// <summary>
    ///     Validates that the offsets of layers that share the position scale share the combined offset domain, so the step is computed from the merged domain, and that
    ///     <c>resolve.scale.xOffset: independent</c> keeps each layer's own.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_SharedOffsetMergesTheDomainAndIndependentKeepsItsOwn()
    {
        // Arrange: the first layer has the groups x and y, the second the group z
        const string layerOne = """{ "data": { "values": [ { "c": "A", "g": "x", "v": 1 }, { "c": "A", "g": "y", "v": 2 } ] }, "mark": "bar", "encoding": { "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }""";
        const string layerTwo = """{ "data": { "values": [ { "c": "A", "g": "z", "v": 3 } ] }, "mark": "tick", "encoding": { "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }""";

        // Act
        var shared = Resolve("{ \"layer\": [" + layerOne + ", " + layerTwo + "] }");
        var independent = Resolve("{ \"resolve\": { \"scale\": { \"xOffset\": \"independent\" } }, \"layer\": [" + layerOne + ", " + layerTwo + "] }");

        // Assert: one offset domain of three groups gives the step 20 * 3 / 0.8 = 75 in every layer; independent offsets give each layer its own count (2 and 1)
        Assert.Equal(["x", "y", "z"], shared.Merged["xOffset"]!.DiscreteDomain.Select(d => d.Text));
        Assert.Equal(75, shared.PerLayer[0]["x"]!.Step!.Value, 9);
        Assert.Equal(75, shared.PerLayer[1]["x"]!.Step!.Value, 9);
        Assert.Equal(75, shared.Merged["x"]!.Step!.Value, 9);
        Assert.Equal(["x", "y"], independent.PerLayer[0]["xOffset"]!.DiscreteDomain.Select(d => d.Text));
        Assert.Equal(["z"], independent.PerLayer[1]["xOffset"]!.DiscreteDomain.Select(d => d.Text));
        Assert.Equal(50, independent.PerLayer[0]["x"]!.Step!.Value, 9);
    }

    /// <summary>
    ///     Resolves the scales of the layers of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scales of the layers.</returns>
    private static LayeredScales Resolve(string json)
    {
        var tree = SpecParser.Parse(json);
        var config = ConfigResolver.Resolve(tree, null);
        var layers = SpecNormalizer.Layers(tree, null);
        var inputs = new List<LayerScaleInput>();
        foreach (var node in layers.Units)
        {
            var unit = node.Unit!;
            var table = DataLoader.Load(node.Data ?? tree.Root["data"], tree.SourceName, null, Ct);
            var plan = EncodingPlan.Create(unit, config);
            inputs.Add(new LayerScaleInput(node, plan, EncodingTransforms.ApplyStandalone(plan, table, unit, null, null, Ct)));
        }

        return ScaleResolver.ResolveLayers(inputs, config, null, null, Ct);
    }
}
