using DemaConsulting.CanvasNet.VegaLite.Guides;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of <see cref="LegendPlan"/>: which channels get a legend, the kind, orient and direction of each, how channels that share a
///     domain merge, the title rules and the facts of the mark that the symbols stand for.
/// </summary>
public class LegendPlanTests
{
    /// <summary>Three rows with the categories <c>a</c> and <c>b</c> and the numbers <c>q</c> and <c>r</c>.</summary>
    private const string Rows = "[ { \"a\": \"x\", \"b\": \"p\", \"q\": 1, \"r\": 10 }, { \"a\": \"y\", \"b\": \"q\", \"q\": 2, \"r\": 20 }, { \"a\": \"z\", \"b\": \"p\", \"q\": 3, \"r\": 30 } ]";

    /// <summary>
    ///     Builds a specification with a mark and the encoding channels after x and y.
    /// </summary>
    /// <param name="channels">The JSON text of the channels (without the enclosing braces).</param>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string channels, string mark = "\"point\"") =>
        "{ \"data\": { \"values\": " + Rows + " }, \"mark\": " + mark + ", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"r\", \"type\": \"quantitative\" }, " + channels + " } }";

    /// <summary>
    ///     Writes the channels of a legend as text.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The channel names joined by a plus sign.</returns>
    private static string Channels(LegendSpec legend) => string.Join("+", legend.Members.Select(m => m.Channel));

    /// <summary>
    ///     Validates that every channel with a field gets a legend in Vega-Lite's order, that channels that have scales but no legend do not,
    ///     and that channels of different fields have legends of their own.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_ChannelsInVegaLiteOrderOneLegendPerField()
    {
        // Arrange: the channels are listed in another order than the legends; fillOpacity and angle have scales but no legend
        var json = Spec(
            "\"opacity\": { \"field\": \"r\", \"type\": \"quantitative\" }, \"size\": { \"field\": \"q\", \"type\": \"quantitative\" }, "
            + "\"shape\": { \"field\": \"a\", \"type\": \"nominal\" }, \"color\": { \"field\": \"b\", \"type\": \"nominal\" }, "
            + "\"fillOpacity\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"angle\": { \"field\": \"q\", \"type\": \"quantitative\" }");

        // Act
        var legends = ChartTestHelpers.Legends(json);

        // Assert: color, size, shape, then opacity; the legends of fillOpacity and angle are not planned (they would have merged with the size)
        Assert.Equal(["color", "size", "shape", "opacity"], legends.Select(Channels));
        Assert.Equal(["b"], legends[0].Title);
        Assert.Equal(["q"], legends[1].Title);
        Assert.Equal(["a"], legends[2].Title);
        Assert.Equal(["r"], legends[3].Title);
    }

    /// <summary>
    ///     Validates that a channel has no legend when its <c>legend</c> is null, when its <c>scale</c> is null, when it is a constant
    ///     value, and when <c>config.legend.disable</c> is set.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_NoLegendForNullLegendNullScaleValueOrDisabled()
    {
        // Arrange
        var field = "\"field\": \"b\", \"type\": \"nominal\"";
        var specs = new Dictionary<string, string>
        {
            ["null"] = "\"color\": { " + field + ", \"legend\": null }",
            ["scale"] = "\"color\": { " + field + ", \"scale\": null }",
            ["value"] = "\"color\": { \"value\": \"red\" }",
        };

        // Act / Assert: none of them has a legend
        foreach (var (name, channel) in specs)
        {
            Assert.True(ChartTestHelpers.Legends(Spec(channel)).Count == 0, name);
        }

        // Act / Assert: the plain field has one, and the configuration can switch it off
        Assert.Single(ChartTestHelpers.Legends(Spec("\"color\": { " + field + " }")));
        Assert.Empty(ChartTestHelpers.Legends(Spec("\"color\": { " + field + " }"), "{ \"legend\": { \"disable\": true } }"));
    }

    /// <summary>
    ///     Validates that channels of the same field merge into one legend (the first channel is the canonical one), that the legend of
    ///     channels with a different field, a different explicit domain or a different sort stays apart.
    /// </summary>
    [Fact]
    public void LegendPlan_Merge_ChannelsOfTheSameDomainShareOneLegend()
    {
        // Arrange
        const string b = "\"field\": \"b\", \"type\": \"nominal\"";

        // Act
        var same = ChartTestHelpers.Legends(Spec("\"shape\": { " + b + " }, \"color\": { " + b + " }, \"size\": { " + b + " }"));
        var otherField = ChartTestHelpers.Legends(Spec("\"color\": { " + b + " }, \"shape\": { \"field\": \"a\", \"type\": \"nominal\" }"));
        var otherSort = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"sort\": \"descending\" }, \"shape\": { " + b + " }"));
        var otherDomain = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"scale\": { \"domain\": [\"q\", \"p\"] } }, \"shape\": { " + b + " }"));
        var sameDomain = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"scale\": { \"domain\": [\"q\", \"p\"] } }, \"shape\": { " + b + ", \"scale\": { \"domain\": [\"q\", \"p\"] } }"));

        // Assert: one legend with the channels in Vega-Lite's order, a single title, and the kind symbol
        Assert.Single(same);
        Assert.Equal("color+size+shape", Channels(same[0]));
        Assert.Equal(["b"], same[0].Title);
        Assert.Equal(LegendKind.Symbol, same[0].Kind);
        Assert.Equal("color", same[0].Canonical.Channel);

        // Assert: a different field, sort or explicit domain keeps the legends apart; the same explicit domain merges
        Assert.Equal(["color", "shape"], otherField.Select(Channels));
        Assert.Equal(["color", "shape"], otherSort.Select(Channels));
        Assert.Equal(["color", "shape"], otherDomain.Select(Channels));
        Assert.Equal(["color+shape"], sameDomain.Select(Channels));
    }

    /// <summary>
    ///     Validates that two legends with explicit orients that differ are not merged, that the same orient merges, and that the orient of one
    ///     legend object applies to the merged legend.
    /// </summary>
    [Fact]
    public void LegendPlan_Merge_ExplicitOrientsThatDifferKeepLegendsApart()
    {
        // Arrange
        const string b = "\"field\": \"b\", \"type\": \"nominal\"";

        // Act
        var different = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"legend\": { \"orient\": \"left\" } }, \"shape\": { " + b + ", \"legend\": { \"orient\": \"top\" } }"));
        var same = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"legend\": { \"orient\": \"left\" } }, \"shape\": { " + b + ", \"legend\": { \"orient\": \"left\" } }"));
        var one = ChartTestHelpers.Legends(Spec("\"color\": { " + b + " }, \"shape\": { " + b + ", \"legend\": { \"orient\": \"bottom\" } }"));

        // Assert
        Assert.Equal(["color", "shape"], different.Select(Channels));
        Assert.Equal(["left", "top"], different.Select(l => l.Orient));
        Assert.Equal(["color+shape"], same.Select(Channels));
        Assert.Equal("left", same[0].Orient);
        Assert.Equal(["color+shape"], one.Select(Channels));
        Assert.Equal("bottom", one[0].Orient);
        Assert.True(one[0].ExplicitOrient);
    }

    /// <summary>
    ///     Validates the kind of a legend: a continuous color scale is a gradient, a discretizing color scale a discrete gradient, everything
    ///     else symbols; <c>legend.type</c> can force symbols; channels that share a legend with different kinds make it a symbol legend.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_KindFollowsTheScaleAndTheTypeProperty()
    {
        // Arrange
        const string q = "\"field\": \"q\", \"type\": \"quantitative\"";

        // Act
        var continuous = ChartTestHelpers.Legends(Spec("\"color\": { " + q + " }"));
        var discretizing = ChartTestHelpers.Legends(Spec("\"color\": { " + q + ", \"scale\": { \"type\": \"quantize\" } }"));
        var threshold = ChartTestHelpers.Legends(Spec("\"color\": { " + q + ", \"scale\": { \"type\": \"threshold\", \"domain\": [2] } }"));
        var ordinal = ChartTestHelpers.Legends(Spec("\"color\": { \"field\": \"b\", \"type\": \"nominal\" }"));
        var size = ChartTestHelpers.Legends(Spec("\"size\": { " + q + " }"));
        var forced = ChartTestHelpers.Legends(Spec("\"color\": { " + q + ", \"legend\": { \"type\": \"symbol\" } }"));
        var ignoredGradient = ChartTestHelpers.Legends(Spec("\"size\": { " + q + ", \"legend\": { \"type\": \"gradient\" } }"));
        var mixed = ChartTestHelpers.Legends(Spec("\"color\": { " + q + " }, \"size\": { " + q + " }"));
        var fillAndStroke = ChartTestHelpers.Legends(Spec("\"fill\": { " + q + " }, \"stroke\": { " + q + " }"));

        // Assert
        Assert.Equal(LegendKind.Gradient, continuous[0].Kind);
        Assert.Equal(LegendKind.DiscreteGradient, discretizing[0].Kind);
        Assert.Equal(LegendKind.DiscreteGradient, threshold[0].Kind);
        Assert.Equal(LegendKind.Symbol, ordinal[0].Kind);
        Assert.Equal(LegendKind.Symbol, size[0].Kind);
        Assert.Equal(LegendKind.Symbol, forced[0].Kind);
        Assert.Equal(LegendKind.Symbol, ignoredGradient[0].Kind);

        // Assert: the size and the color of one field make one symbol legend, a fill and a stroke of one field one gradient
        Assert.Equal("color+size", Channels(mixed.Single()));
        Assert.Equal(LegendKind.Symbol, mixed[0].Kind);
        Assert.Equal("fill+stroke", Channels(fillAndStroke.Single()));
        Assert.Equal(LegendKind.Gradient, fillAndStroke[0].Kind);
    }

    /// <summary>
    ///     Validates the orient and the direction: the orient is <c>right</c> by default, a legend object and the configuration change it, the
    ///     direction of a top or bottom legend is horizontal, the inner corners are horizontal for gradients only.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_OrientAndDirectionFollowVegaLiteDefaults()
    {
        // Arrange
        const string b = "\"field\": \"b\", \"type\": \"nominal\"";
        const string q = "\"field\": \"q\", \"type\": \"quantitative\"";
        static string WithLegend(string field, string legend) => "\"color\": { " + field + ", \"legend\": " + legend + " }";

        // Act
        var plain = ChartTestHelpers.Legends(Spec("\"color\": { " + b + " }"))[0];
        var top = ChartTestHelpers.Legends(Spec(WithLegend(b, "{ \"orient\": \"top\" }")))[0];
        var corner = ChartTestHelpers.Legends(Spec(WithLegend(b, "{ \"orient\": \"top-left\" }")))[0];
        var cornerGradient = ChartTestHelpers.Legends(Spec(WithLegend(q, "{ \"orient\": \"bottom-right\" }")))[0];
        var left = ChartTestHelpers.Legends(Spec(WithLegend(b, "{ \"orient\": \"left\" }")))[0];
        var explicitDirection = ChartTestHelpers.Legends(Spec(WithLegend(b, "{ \"direction\": \"horizontal\" }")))[0];
        var configured = ChartTestHelpers.Legends(Spec("\"color\": { " + b + " }"), "{ \"legend\": { \"orient\": \"bottom\", \"symbolDirection\": \"horizontal\" } }")[0];
        var gradientConfigured = ChartTestHelpers.Legends(Spec("\"color\": { " + q + " }"), "{ \"legend\": { \"gradientDirection\": \"horizontal\" } }")[0];

        // Assert
        Assert.Equal(("right", true, false), (plain.Orient, plain.Vertical, plain.ExplicitOrient));
        Assert.Equal(("top", false), (top.Orient, top.Vertical));
        Assert.Equal(("top-left", true), (corner.Orient, corner.Vertical));
        Assert.Equal(("bottom-right", false), (cornerGradient.Orient, cornerGradient.Vertical));
        Assert.Equal(("left", true), (left.Orient, left.Vertical));
        Assert.False(explicitDirection.Vertical);
        Assert.Equal(("bottom", false), (configured.Orient, configured.Vertical));
        Assert.False(gradientConfigured.Vertical);
    }

    /// <summary>
    ///     Validates the title: the legend's <c>title</c> first, then the title of the channel definition, then the default title of the field
    ///     (the aggregate and bin titles like an axis); null means no title.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_TitleFollowsTheLegendTheChannelAndTheFieldTitle()
    {
        // Arrange
        const string b = "\"field\": \"b\", \"type\": \"nominal\"";
        var aggregated = "\"color\": { \"field\": \"r\", \"type\": \"quantitative\", \"aggregate\": \"sum\" }";

        // Act
        var legendTitle = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"title\": \"channel\", \"legend\": { \"title\": \"legend\" } }"))[0];
        var channelTitle = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"title\": \"channel\" }"))[0];
        var lines = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"legend\": { \"title\": [\"one\", \"two\"] } }"))[0];
        var none = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"legend\": { \"title\": null } }"))[0];
        var empty = ChartTestHelpers.Legends(Spec("\"color\": { " + b + ", \"legend\": { \"title\": \"\" } }"))[0];
        var field = ChartTestHelpers.Legends(Spec("\"color\": { " + b + " }"))[0];
        var sum = ChartTestHelpers.Legends(Spec(aggregated));

        // Assert
        Assert.Equal(["legend"], legendTitle.Title);
        Assert.Equal(["channel"], channelTitle.Title);
        Assert.Equal(["one", "two"], lines.Title);
        Assert.Null(none.Title);
        Assert.Null(empty.Title);
        Assert.Equal(["b"], field.Title);
        Assert.Equal(["Sum of r"], sum.Single().Title);
    }

    /// <summary>
    ///     Validates the facts of the mark that the symbols stand for: filled or not, the fixed paints, the opacity, the shape and the
    ///     paints that a field drives.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_MarkFactsDescribeFilledFixedPaintsAndOpacity()
    {
        // Arrange
        const string b = "\"color\": { \"field\": \"b\", \"type\": \"nominal\" }";
        const string size = "\"size\": { \"field\": \"q\", \"type\": \"quantitative\" }";

        // Act
        var point = ChartTestHelpers.Legends(Spec(size))[0].Mark;
        var circle = ChartTestHelpers.Legends(Spec(size, "{ \"type\": \"circle\", \"color\": \"red\", \"opacity\": 0.4 }"))[0].Mark;
        var shaped = ChartTestHelpers.Legends(Spec(size, "{ \"type\": \"point\", \"shape\": \"square\", \"strokeWidth\": 3, \"strokeDash\": [2, 1] }"))[0].Mark;
        var fieldFill = ChartTestHelpers.Legends(Spec(size + ", \"fill\": { \"field\": \"b\", \"type\": \"nominal\" }", "\"circle\""))[0].Mark;
        var fieldStroke = ChartTestHelpers.Legends(Spec(size + ", " + b))[0].Mark;
        var constantOpacity = ChartTestHelpers.Legends(Spec(size + ", \"opacity\": { \"value\": 0.25 }"))[0].Mark;

        // Assert: an unfilled point is drawn at 0.7 opacity with a transparent fill and the default blue stroke
        Assert.False(point.Filled);
        Assert.Equal("transparent", point.Fill);
        Assert.Equal("#4c78a8", point.Stroke);
        Assert.Equal(0.7, point.Opacity);
        Assert.Null(point.Shape);

        // Assert: a filled circle has the fixed color as its fill and the opacity of the mark
        Assert.True(circle.Filled);
        Assert.Equal("red", circle.Fill);
        Assert.Equal(0.4, circle.Opacity);

        // Assert: the mark's shape, stroke width and dash
        Assert.Equal("square", shaped.Shape);
        Assert.Equal(3, shaped.StrokeWidth);
        Assert.Equal([2.0, 1.0], shaped.StrokeDash);

        // Assert: a field that drives a paint is not a fixed paint
        Assert.True(fieldFill.FillIsField);
        Assert.False(fieldFill.StrokeIsField);
        Assert.True(fieldStroke.StrokeIsField);
        Assert.False(fieldStroke.FillIsField);

        // Assert: a constant opacity channel is the opacity of the marks
        Assert.Equal(0.25, constantOpacity.Opacity);
    }
}
