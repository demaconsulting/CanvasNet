using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the <c>boxplot</c> expansion of <see cref="SpecNormalizer.Expand"/> (Phase 10): the layers, the transforms and the properties of the parts that
///     Vega-Lite's <c>normalizeBoxPlot</c> gives a Tukey and a min-max box plot, the orientation rules, the part switches, the inheritance of the encoding and the limits.
/// </summary>
public class SpecNormalizerBoxPlotTests
{
    /// <summary>A vertical box plot of the field <c>v</c> by the field <c>g</c>.</summary>
    private const string Vertical = """
        "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }
        """;

    /// <summary>
    ///     Parses a specification, resolves its configuration and expands its composite marks.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The expanded layers.</returns>
    private static ExpandedLayers Expand(string json, VegaLiteLimits? limits = null)
    {
        var tree = SpecParser.Parse(json, "box.json", limits);
        return SpecNormalizer.Expand(tree, ConfigResolver.Resolve(tree, null), limits);
    }

    /// <summary>
    ///     Gets the mark types of the layers that are drawn, in order.
    /// </summary>
    /// <param name="expanded">The expanded layers.</param>
    /// <returns>The mark types.</returns>
    private static List<string?> Marks(ExpandedLayers expanded) => [.. expanded.Layers.Units.Select(u => u.Unit!["mark"] is { IsString: true } text ? text.StringValue : MarkOf(u.Unit)["type"]?.StringValue)];

    /// <summary>
    ///     Gets the mark definition of a unit view.
    /// </summary>
    /// <param name="unit">The unit view.</param>
    /// <returns>The mark definition object.</returns>
    private static SpecNode MarkOf(SpecNode unit) => unit["mark"]!;

    /// <summary>
    ///     Gets the field of a channel of a unit view.
    /// </summary>
    /// <param name="unit">The unit view.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The field; null when the channel has none.</returns>
    private static string? FieldOf(SpecNode unit, string channel) => unit["encoding"]?[channel]?["field"]?.StringValue;

    /// <summary>
    ///     Gets the layers of a unit view that has the given part style, as <c>(field, end field)</c> pairs of the continuous channel.
    /// </summary>
    /// <param name="expanded">The expanded layers.</param>
    /// <param name="style">The style of the part, such as <c>boxplot-rule</c>.</param>
    /// <returns>The first and second field of the <c>y</c> channels of the units with that style.</returns>
    private static List<(string? Field, string? End)> Positions(ExpandedLayers expanded, string style) =>
        [.. expanded.Layers.Units.Where(u => MarkOf(u.Unit!)["style"]?.StringValue == style).Select(u => (FieldOf(u.Unit!, "y"), FieldOf(u.Unit!, "y2")))];

    /// <summary>
    ///     Gets the operations of an <c>aggregate</c> or <c>joinaggregate</c> transform entry as <c>op field as</c> texts.
    /// </summary>
    /// <param name="entry">The transform entry.</param>
    /// <param name="kind"><c>aggregate</c> or <c>joinaggregate</c>.</param>
    /// <returns>The operations.</returns>
    private static List<string> Operations(SpecNode entry, string kind) =>
        [.. entry[kind]!.Items.Select(m => $"{m["op"]!.StringValue} {m["field"]!.StringValue} {m["as"]!.StringValue}")];

    /// <summary>
    ///     Validates the layers of a default (Tukey) box plot: a view of the outliers and of the whiskers whose rows have their quartiles, then a view of the box, in the
    ///     order of Vega-Lite's <c>normalizeBoxPlot</c>, and that the composite mark is a layered view that does not inherit its own encoding again.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_TukeyBoxPlotIsOutlierWhiskerAndBoxViews()
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": "boxplot", {{Vertical}} }""");
        var root = expanded.Layers.Root;

        // Assert: the composite mark is a layered view; the outliers, the two rules, the box and the median tick are the units, in drawing order
        Assert.True(root.IsLayer);
        Assert.True(root.IsExpansion);
        Assert.Null(root.Unit);
        Assert.Equal(["point", "rule", "rule", "bar", "tick"], Marks(expanded));

        // Assert: views are the composite, the two groups of layers, the whisker view and the five units
        Assert.Equal(9, expanded.Layers.ViewCount);
        Assert.Equal(2, root.Children.Count);
        Assert.Equal(2, root.Children[0].Children.Count);
        Assert.Equal(2, root.Children[1].Children.Count);
        Assert.All(expanded.Layers.Units, u => Assert.True(u.IsSynthetic));
        Assert.All(expanded.Layers.Units, u => Assert.Equal("/mark", u.Unit!.Pointer));
    }

    /// <summary>
    ///     Validates the transforms of a Tukey box plot: the quartiles by a <c>joinaggregate</c> grouped by the grouping field, the whisker view that keeps the rows inside the
    ///     fences of 1.5 interquartile ranges and takes their extremes, and the box view that aggregates the three quartiles; all the expressions are compiled.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_TukeyTransformsFollowVegaLite()
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": "boxplot", {{Vertical}} }""");
        var quartiles = expanded.Layers.Root.Children[0];
        var whiskers = quartiles.Children[1];
        var box = expanded.Layers.Root.Children[1];

        // Assert: the quartiles of each group go onto the rows
        var joinAggregate = Assert.Single(quartiles.Transform!.Items);
        Assert.Equal(["q1 v lower_box_v", "q3 v upper_box_v"], Operations(joinAggregate, "joinaggregate"));
        Assert.Equal(["g"], joinAggregate["groupby"]!.Items.Select(g => g.StringValue));

        // Assert: the whisker view keeps the rows within the fences (written as Vega-Lite writes them) and takes the extremes, keeping the box bounds
        Assert.Equal(2, whiskers.Transform!.Items.Count);
        var filter = whiskers.Transform.Items[0]["filter"]!;
        Assert.Equal(
            "(datum['lower_box_v'] - 1.5 * (datum['upper_box_v'] - datum['lower_box_v']) <= datum['v']) && (datum['v'] <= datum['upper_box_v'] + 1.5 * (datum['upper_box_v'] - datum['lower_box_v']))",
            filter.StringValue);
        Assert.NotNull(expanded.Tree.Expressions.Get(filter));
        Assert.Equal(
            ["min v lower_whisker_v", "max v upper_whisker_v", "min lower_box_v lower_box_v", "max upper_box_v upper_box_v"],
            Operations(whiskers.Transform.Items[1], "aggregate"));

        // Assert: the box view aggregates the quartiles by group
        var aggregate = Assert.Single(box.Transform!.Items);
        Assert.Equal(["q1 v lower_box_v", "q3 v upper_box_v", "median v mid_box_v"], Operations(aggregate, "aggregate"));
        Assert.Equal(["g"], aggregate["groupby"]!.Items.Select(g => g.StringValue));

        // Assert: the outliers are the rows outside the fences, of the raw field
        var outliers = quartiles.Children[0];
        var test = outliers.Transform!.Items[0]["filter"]!;
        Assert.Equal(
            "(datum['v'] < datum['lower_box_v'] - 1.5 * (datum['upper_box_v'] - datum['lower_box_v'])) || (datum['v'] > datum['upper_box_v'] + 1.5 * (datum['upper_box_v'] - datum['lower_box_v']))",
            test.StringValue);
        Assert.NotNull(expanded.Tree.Expressions.Get(test));
        Assert.Equal("v", FieldOf(outliers.Unit!, "y"));
    }

    /// <summary>
    ///     Validates that the expansion does not touch the parsed tree: the expressions of the expansion are only in the derived tree, and a specification without composite
    ///     mark gives back the very tree that was parsed.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ExpressionsAreInTheDerivedTreeOnly()
    {
        // Arrange
        var tree = SpecParser.Parse($$"""{ "mark": "boxplot", {{Vertical}} }""");
        var plain = SpecParser.Parse("""{ "mark": "point" }""");
        var config = ConfigResolver.Resolve(tree, null);

        // Act
        var expanded = SpecNormalizer.Expand(tree, config);
        var untouched = SpecNormalizer.Expand(plain, config);

        // Assert
        Assert.NotSame(tree, expanded.Tree);
        Assert.Same(tree.Root, expanded.Tree.Root);
        Assert.Equal(0, tree.Expressions.Count);
        Assert.Equal(3, expanded.Tree.Expressions.Count);
        Assert.Same(plain, untouched.Tree);
        Assert.Equal(["point"], untouched.Layers.Units.Select(u => u.Unit!["mark"]!.StringValue));
    }

    /// <summary>
    ///     Validates the min-max box plot: one view whose single aggregate gives the quartiles, the minimum and the maximum, and whose layers are the whiskers, the box and the
    ///     median (no outliers).
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_MinMaxIsOneAggregateView()
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": { "type": "boxplot", "extent": "min-max" }, {{Vertical}} }""");
        var view = Assert.Single(expanded.Layers.Root.Children);

        // Assert
        Assert.Equal(["rule", "rule", "bar", "tick"], Marks(expanded));
        Assert.Equal(
            ["q1 v lower_box_v", "q3 v upper_box_v", "median v mid_box_v", "min v lower_whisker_v", "max v upper_whisker_v"],
            Operations(Assert.Single(view.Transform!.Items), "aggregate"));
        Assert.Equal(1, expanded.Tree.Expressions.Count);
        Assert.Equal([("lower_whisker_v", "lower_box_v"), ("upper_box_v", "upper_whisker_v")], Positions(expanded, "boxplot-rule"));
    }

    /// <summary>
    ///     Validates that the extent is a multiple of the interquartile range in the fences of the expressions, and that it comes from the mark, else from the configuration
    ///     (<c>1.5</c> by default).
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ExtentComesFromTheMarkThenTheConfiguration()
    {
        // Arrange / Act
        var own = Expand($$"""{ "mark": { "type": "boxplot", "extent": 3 }, {{Vertical}} }""");
        var configured = Expand($$"""{ "config": { "boxplot": { "extent": 2.5 } }, "mark": "boxplot", {{Vertical}} }""");

        // Assert
        Assert.Contains("- 3 * (", own.Layers.Root.Children[0].Children[1].Transform!.Items[0]["filter"]!.StringValue, StringComparison.Ordinal);
        Assert.Contains("- 2.5 * (", configured.Layers.Root.Children[0].Children[1].Transform!.Items[0]["filter"]!.StringValue, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the fields that the parts draw: the whiskers run between the lower whisker and the lower box and between the upper box and the upper whisker, the box
    ///     between the quartiles, the median at the middle, and every part names the style <c>boxplot-&lt;part&gt;</c>.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_PartsDrawTheComputedFields()
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": "boxplot", {{Vertical}} }""");

        // Assert
        Assert.Equal([("lower_whisker_v", "lower_box_v"), ("upper_box_v", "upper_whisker_v")], Positions(expanded, "boxplot-rule"));
        Assert.Equal([("lower_box_v", "upper_box_v")], Positions(expanded, "boxplot-box"));
        Assert.Equal([("mid_box_v", null)], Positions(expanded, "boxplot-median"));
        Assert.Equal([("v", null)], Positions(expanded, "boxplot-outliers"));
        Assert.Empty(Positions(expanded, "boxplot-ticks"));
    }

    /// <summary>
    ///     Validates the properties of the marks of the parts: the box and the median are 14 wide, the box is a bar along the orientation and the median a white tick across it,
    ///     the whiskers are rules and the outliers points.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_PartMarksHaveTheDefaults()
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": "boxplot", {{Vertical}} }""");
        var bar = MarkOf(expanded.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "bar").Unit!);
        var tick = MarkOf(expanded.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "tick").Unit!);

        // Assert
        Assert.Equal(14, bar["size"]!.NumberValue);
        Assert.Equal("vertical", bar["orient"]!.StringValue);
        Assert.Equal("boxplot-box", bar["style"]!.StringValue);
        Assert.Equal(14, tick["size"]!.NumberValue);
        Assert.Equal("horizontal", tick["orient"]!.StringValue);
        Assert.Equal("white", tick["color"]!.StringValue);
        Assert.Equal("boxplot-median", tick["style"]!.StringValue);
    }

    /// <summary>
    ///     Validates that the median takes the color of the box where the box has no height: its <c>color</c> is a condition on the quartiles whose value is the default
    ///     box color, or the written color channel.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_MedianColorFallsBackToTheBoxColorOnAFlatBox()
    {
        // Arrange / Act
        var plain = Expand($$"""{ "mark": "boxplot", {{Vertical}} }""");
        var colored = Expand("""
            { "mark": "boxplot", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } } }
            """);
        var plainMedian = plain.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "tick").Unit!["encoding"]!["color"]!["condition"]!;
        var coloredMedian = colored.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "tick").Unit!["encoding"]!["color"]!["condition"]!;

        // Assert: the test is compiled; the fallback is the default color or the field that the color channel writes
        Assert.Equal("datum['lower_box_v'] >= datum['upper_box_v']", plainMedian["test"]!.StringValue);
        Assert.NotNull(plain.Tree.Expressions.Get(plainMedian["test"]));
        Assert.Equal("#4c78a8", plainMedian["value"]!.StringValue);
        Assert.Equal("g", coloredMedian["field"]!.StringValue);
        Assert.Null(coloredMedian["value"]);
    }

    /// <summary>
    ///     Validates the switches of the parts: each can be turned off in the mark and the end ticks turned on, in the mark and in the configuration.
    /// </summary>
    /// <param name="mark">The mark definition.</param>
    /// <param name="config">The configuration, or an empty object.</param>
    /// <param name="expected">The mark types of the layers.</param>
    [Theory]
    [InlineData("""{ "type": "boxplot", "outliers": false }""", "{}", "rule rule bar tick")]
    [InlineData("""{ "type": "boxplot", "box": false }""", "{}", "point rule rule tick")]
    [InlineData("""{ "type": "boxplot", "median": false }""", "{}", "point rule rule bar")]
    [InlineData("""{ "type": "boxplot", "rule": false }""", "{}", "point bar tick")]
    [InlineData("""{ "type": "boxplot", "ticks": true }""", "{}", "point rule rule tick tick bar tick")]
    [InlineData("""{ "type": "boxplot", "ticks": {} }""", "{}", "point rule rule tick tick bar tick")]
    [InlineData("""{ "type": "boxplot", "rule": false, "box": false, "median": false, "outliers": false }""", "{}", "")]
    [InlineData("\"boxplot\"", """{ "boxplot": { "ticks": true, "outliers": false } }""", "rule rule tick tick bar tick")]
    [InlineData("""{ "type": "boxplot", "outliers": true }""", """{ "boxplot": { "outliers": false } }""", "point rule rule bar tick")]
    public void SpecNormalizer_Expand_PartsCanBeSwitched(string mark, string config, string expected)
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "config": {{config}}, "mark": {{mark}}, {{Vertical}} }""");

        // Assert
        Assert.Equal(expected.Split(' ', StringSplitOptions.RemoveEmptyEntries), Marks(expanded));
    }

    /// <summary>
    ///     Validates that the end ticks are black, opaque and across the box, and that the box takes its size from the mark, the configuration or the default of 14.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_EndTicksAndSizes()
    {
        // Arrange / Act
        var ticks = Expand($$"""{ "mark": { "type": "boxplot", "ticks": true, "size": 20 }, {{Vertical}} }""");
        var configured = Expand($$"""{ "config": { "boxplot": { "size": 8 } }, "mark": "boxplot", {{Vertical}} }""");
        var tick = MarkOf(ticks.Layers.Units.First(u => MarkOf(u.Unit!)["style"]!.StringValue == "boxplot-ticks").Unit!);
        var bar = MarkOf(ticks.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "bar").Unit!);

        // Assert
        Assert.Equal("black", tick["color"]!.StringValue);
        Assert.Equal(1, tick["opacity"]!.NumberValue);
        Assert.Equal("horizontal", tick["orient"]!.StringValue);
        Assert.Equal(20, bar["size"]!.NumberValue);
        Assert.Equal(8, MarkOf(configured.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "bar").Unit!)["size"]!.NumberValue);
    }

    /// <summary>
    ///     Validates how the mark's own properties reach the parts: <c>color</c>, <c>opacity</c> and <c>clip</c> go to every part, a part's own object overrides them, and the
    ///     configuration of a part (here the median's color) comes first.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_PartPropertiesOverrideInOrder()
    {
        // Arrange / Act
        var expanded = Expand($$"""
            { "config": { "boxplot": { "box": { "stroke": "gray" } } },
              "mark": { "type": "boxplot", "color": "firebrick", "opacity": 0.5, "clip": true, "median": { "color": "yellow" }, "rule": { "strokeWidth": 3 } }, {{Vertical}} }
            """);
        SpecNode Mark(string style) => MarkOf(expanded.Layers.Units.First(u => MarkOf(u.Unit!)["style"]!.StringValue == style).Unit!);

        // Assert
        Assert.Equal("firebrick", Mark("boxplot-box")["color"]!.StringValue);
        Assert.Equal("gray", Mark("boxplot-box")["stroke"]!.StringValue);
        Assert.Equal(0.5, Mark("boxplot-box")["opacity"]!.NumberValue);
        Assert.True(Mark("boxplot-box")["clip"]!.BoolValue);
        Assert.Equal("yellow", Mark("boxplot-median")["color"]!.StringValue);
        Assert.Equal(3, Mark("boxplot-rule")["strokeWidth"]!.NumberValue);
        Assert.Equal("firebrick", Mark("boxplot-rule")["color"]!.StringValue);
        Assert.Equal("firebrick", Mark("boxplot-outliers")["color"]!.StringValue);
    }

    /// <summary>
    ///     Validates the orientation: the measure is the continuous channel (a horizontal box plot has it on <c>x</c>), an <c>orient</c> decides, and two continuous channels
    ///     are vertical unless only <c>y</c> is temporal.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="x">The <c>x</c> channel definition.</param>
    /// <param name="y">The <c>y</c> channel definition.</param>
    /// <param name="barOrient">The orientation of the box.</param>
    [Theory]
    [InlineData("\"boxplot\"", """{ "field": "v", "type": "quantitative" }""", """{ "field": "g", "type": "nominal" }""", "horizontal")]
    [InlineData("\"boxplot\"", """{ "field": "g", "type": "ordinal" }""", """{ "field": "v", "type": "quantitative" }""", "vertical")]
    [InlineData("\"boxplot\"", """{ "field": "a", "type": "quantitative" }""", """{ "field": "v", "type": "quantitative" }""", "vertical")]
    [InlineData("\"boxplot\"", """{ "field": "a", "type": "quantitative" }""", """{ "field": "t", "type": "temporal" }""", "horizontal")]
    [InlineData("\"boxplot\"", """{ "field": "t", "type": "temporal" }""", """{ "field": "a", "type": "quantitative" }""", "vertical")]
    [InlineData("""{ "type": "boxplot", "orient": "horizontal" }""", """{ "field": "a", "type": "quantitative" }""", """{ "field": "v", "type": "quantitative" }""", "horizontal")]
    public void SpecNormalizer_Expand_OrientFollowsTheContinuousChannel(string mark, string x, string y, string barOrient)
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": {{mark}}, "encoding": { "x": {{x}}, "y": {{y}} } }""");
        var bar = MarkOf(expanded.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "bar").Unit!);

        // Assert
        Assert.Equal(barOrient, bar["orient"]!.StringValue);
    }

    /// <summary>
    ///     Validates the channels of a horizontal box plot: the measure is on <c>x</c> and <c>x2</c> and the ticks across the box are vertical.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_HorizontalBoxPlotUsesXAndX2()
    {
        // Arrange / Act
        var expanded = Expand("""
            { "mark": "boxplot", "encoding": { "y": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative", "title": "Value", "scale": { "zero": false } } } }
            """);
        var bar = expanded.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "bar").Unit!;
        var tick = expanded.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "tick").Unit!;

        // Assert: the box spans x to x2, the grouping channel is kept, the title and the scale of the measure reach every part
        Assert.Equal("lower_box_v", FieldOf(bar, "x"));
        Assert.Equal("upper_box_v", FieldOf(bar, "x2"));
        Assert.Equal("g", FieldOf(bar, "y"));
        Assert.Equal("Value", bar["encoding"]!["x"]!["title"]!.StringValue);
        Assert.False(bar["encoding"]!["x"]!["scale"]!["zero"]!.BoolValue);
        Assert.Equal("vertical", MarkOf(tick)["orient"]!.StringValue);
        Assert.Equal("mid_box_v", FieldOf(tick, "x"));
    }

    /// <summary>
    ///     Validates that the title of the measure is the field when none is written, on every part.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_TitleOfTheMeasureDefaultsToTheField()
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": "boxplot", {{Vertical}} }""");

        // Assert
        Assert.All(expanded.Layers.Units, u => Assert.Equal("v", u.Unit!["encoding"]!["y"]!["title"]!.StringValue));
    }

    /// <summary>
    ///     Validates the channels that the parts share: the color and the size go to the box (the median takes the size and a conditional color), the outliers get the color,
    ///     the whiskers neither, a grouping channel adds to the <c>groupby</c> and the tooltip is left out.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_SharedChannelsFollowVegaLite()
    {
        // Arrange / Act
        var expanded = Expand("""
            { "mark": "boxplot",
              "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" },
                            "color": { "field": "c", "type": "nominal" }, "size": { "value": 5 }, "opacity": { "value": 0.9 }, "tooltip": { "field": "v" } } }
            """);
        SpecNode Unit(string style) => expanded.Layers.Units.First(u => MarkOf(u.Unit!)["style"]!.StringValue == style).Unit!;

        // Assert: groups by every field channel
        Assert.Equal(["g", "c"], expanded.Layers.Root.Children[1].Transform!.Items[0]["groupby"]!.Items.Select(g => g.StringValue));
        Assert.Equal(["y", "y2", "x", "color", "size", "opacity"], Unit("boxplot-box")["encoding"]!.Properties.Select(p => p.Name));
        Assert.Equal(["y", "x", "opacity", "color"], Unit("boxplot-outliers")["encoding"]!.Properties.Select(p => p.Name));
        Assert.Equal(["y", "y2", "x", "opacity"], Unit("boxplot-rule")["encoding"]!.Properties.Select(p => p.Name));
        Assert.Equal(["y", "x", "opacity", "size", "color"], Unit("boxplot-median")["encoding"]!.Properties.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that a bin or an aggregate on a grouping channel is not applied (the raw field groups the rows, the bin is not in the encoding of the parts and a channel that
    ///     aggregates is left out), and that a time unit is applied (Vega-Lite's <c>extractTransformsFromEncoding</c>): a <c>timeUnit</c> transform runs before the aggregate, the unit's field
    ///     groups the rows and the channel reads it with the default title.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_GroupingChannelsDropBinAndAggregateAndApplyTimeUnit()
    {
        // Arrange / Act
        var expanded = Expand("""
            { "mark": "boxplot",
              "encoding": { "x": { "field": "g", "type": "ordinal", "bin": true }, "y": { "field": "v", "type": "quantitative" },
                            "color": { "field": "t", "type": "temporal", "timeUnit": "year" }, "size": { "field": "z", "aggregate": "mean", "type": "quantitative" } } }
            """);
        var box = expanded.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "bar").Unit!["encoding"]!;
        var boxTransform = expanded.Layers.Root.Children[1].Transform!.Items;
        var quartileTransform = expanded.Layers.Root.Children[0].Transform!.Items;

        // Assert: the time unit transform comes first in both views, then the aggregate groups by the unit's field
        Assert.Equal("year", boxTransform[0]["timeUnit"]!.StringValue);
        Assert.Equal("t", boxTransform[0]["field"]!.StringValue);
        Assert.Equal("year_t", boxTransform[0]["as"]!.StringValue);
        Assert.Equal("year", quartileTransform[0]["timeUnit"]!.StringValue);
        Assert.Equal(["g", "year_t"], boxTransform[1]["groupby"]!.Items.Select(g => g.StringValue));
        Assert.Null(box["x"]!["bin"]);
        Assert.Equal("g", box["x"]!["field"]!.StringValue);
        Assert.Equal("ordinal", box["x"]!["type"]!.StringValue);

        // Assert: the channel reads the derived field, has no time unit and keeps a default title; the aggregated channel is left out
        Assert.Null(box["color"]!["timeUnit"]);
        Assert.Equal("year_t", box["color"]!["field"]!.StringValue);
        Assert.Equal("t (year)", box["color"]!["title"]!.StringValue);
        Assert.Null(box["size"]);
    }

    /// <summary>
    ///     Validates that a time unit on a channel that is not temporal gets the format type <c>time</c> for its axis (a position channel), its legend or its text, keeping the properties
    ///     and the title that the definition has, and that a binned time unit is left as it was.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_TimeUnitOfAnOrdinalGroupingChannelFormatsAsTime()
    {
        // Arrange / Act
        var expanded = Expand("""
            { "mark": "boxplot",
              "encoding": { "x": { "field": "d", "type": "ordinal", "timeUnit": "month", "axis": { "labelAngle": 0 } }, "y": { "field": "v", "type": "quantitative" },
                            "color": { "field": "d", "type": "nominal", "timeUnit": "year", "title": "Own" } } }
            """);
        var box = expanded.Layers.Units.Single(u => MarkOf(u.Unit!)["type"]!.StringValue == "bar").Unit!["encoding"]!;

        // Assert: the axis has the format type first and keeps its angle; the legend is created for the color; a written title wins over the default
        Assert.Equal("month_d", box["x"]!["field"]!.StringValue);
        Assert.Equal("time", box["x"]!["axis"]!["formatType"]!.StringValue);
        Assert.Equal(0, box["x"]!["axis"]!["labelAngle"]!.NumberValue);
        Assert.Equal("d (month)", box["x"]!["title"]!.StringValue);
        Assert.Equal("time", box["color"]!["legend"]!["formatType"]!.StringValue);
        Assert.Equal("Own", box["color"]!["title"]!.StringValue);
    }

    /// <summary>
    ///     Validates that a box plot without a grouping channel groups by nothing (one box), and that the group fields are not repeated.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_SingleBoxHasNoGroupBy()
    {
        // Arrange / Act
        var expanded = Expand("""{ "mark": "boxplot", "encoding": { "y": { "field": "v", "type": "quantitative" } } }""");

        // Assert
        Assert.Empty(expanded.Layers.Root.Children[1].Transform!.Items[0]["groupby"]!.Items);
        Assert.Equal(["point", "rule", "rule", "bar", "tick"], Marks(expanded));
    }

    /// <summary>
    ///     Validates the inheritance: a box plot layer takes the encoding of the layered views above it (the expanded parts do not inherit the encodings again, so a channel
    ///     of the parent that the parts leave out does not come back), and the other layers are untouched.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_BoxPlotLayerMergesTheEncodingOfItsParents()
    {
        // Arrange / Act
        var expanded = Expand("""
            { "encoding": { "x": { "field": "g", "type": "nominal" }, "color": { "field": "c", "type": "nominal" } },
              "layer": [
                { "mark": "boxplot", "encoding": { "y": { "field": "v", "type": "quantitative" } } },
                { "mark": "point", "encoding": { "y": { "field": "w", "type": "quantitative" } } } ] }
            """);
        var rule = expanded.Layers.Units.First(u => MarkOf(u.Unit!)["style"]?.StringValue == "boxplot-rule").Unit!;

        // Assert: the box plot got x from the parent, and its whiskers do not carry the parent's color back in; the point layer inherits both
        Assert.Equal("g", FieldOf(rule, "x"));
        Assert.Null(rule["encoding"]!["color"]);
        Assert.Equal(["point", "rule", "rule", "bar", "tick", "point"], Marks(expanded));
        Assert.Equal(["x", "color", "y"], expanded.Layers.Units[^1].Unit!["encoding"]!.Properties.Select(p => p.Name));
        Assert.True(expanded.Layers.Root.Children[0].IsExpansion);
        Assert.False(expanded.Layers.Root.Children[1].IsExpansion);
    }

    /// <summary>
    ///     Validates that the views of the expansion are counted as views, so that <see cref="VegaLiteLimits.MaxViews"/> bounds them, and that they are not counted as
    ///     nesting levels of layers.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ExpandedViewsCountAgainstMaxViewsNotTheDepth()
    {
        // Arrange: a box plot has nine views
        var json = $$"""{ "mark": "boxplot", {{Vertical}} }""";
        var nested = $$"""{ "layer": [ { "layer": [ { "mark": "boxplot", {{Vertical}} } ] } ] }""";

        // Act
        var fits = Expand(json, new VegaLiteLimits { MaxViews = 9 });
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => Expand(json, new VegaLiteLimits { MaxViews = 8 }));
        var shallow = Expand(nested, new VegaLiteLimits { MaxCompositionDepth = 2 });

        // Assert: located at the view that exceeded the limit, i.e. a view written by the expansion (the mark)
        Assert.Equal(9, fits.Layers.ViewCount);
        Assert.Contains("MaxViews", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal("/mark", tooMany.Location.Pointer);
        Assert.Equal(5, shallow.Layers.Units.Count);
    }

    /// <summary>
    ///     Validates the specification errors of a box plot: no continuous channel, only one channel that is not continuous, and a measure that is not a field.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding.</param>
    [Theory]
    [InlineData("\"boxplot\"", """{ "x": { "field": "a", "type": "nominal" }, "y": { "field": "b", "type": "ordinal" } }""")]
    [InlineData("\"boxplot\"", """{ "x": { "field": "a", "type": "nominal" } }""")]
    [InlineData("""{ "type": "boxplot", "orient": "vertical" }""", """{ "x": { "field": "a", "type": "quantitative" }, "y": { "datum": 3 } }""")]
    public void SpecNormalizer_Expand_UndecidableBoxPlotIsASpecificationError(string mark, string encoding)
    {
        // Arrange
        var json = $$"""{ "mark": {{mark}}, "encoding": {{encoding}} }""";

        // Act / Assert
        var error = Assert.Throws<VegaLiteSpecException>(() => Expand(json));
        Assert.Equal("/mark", error.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the marks that are not composite are left as unit views, that a view without a composite mark is unchanged, and that the arguments are
    ///     checked.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_OtherMarksAreUnchangedAndArgumentsChecked()
    {
        // Arrange
        var tree = SpecParser.Parse("""{ "layer": [ { "mark": "geoshape" }, { "mark": "bar" } ] }""");
        var config = ConfigResolver.Resolve(tree, null);

        // Act
        var expanded = SpecNormalizer.Expand(tree, config);

        // Assert
        Assert.Equal(["geoshape", "bar"], expanded.Layers.Units.Select(u => u.Unit!["mark"]!.StringValue));
        Assert.Equal("boxplot", SpecNormalizer.ExpandedCompositeType(SpecParser.Parse("""{ "mark": { "type": "boxplot" } }""").Root));
        Assert.Equal("errorbar", SpecNormalizer.ExpandedCompositeType(SpecParser.Parse("""{ "mark": "errorbar" }""").Root));
        Assert.Equal("errorband", SpecNormalizer.ExpandedCompositeType(SpecParser.Parse("""{ "mark": { "type": "errorband" } }""").Root));
        Assert.Null(SpecNormalizer.ExpandedCompositeType(tree.Root));
        Assert.Throws<ArgumentNullException>(() => SpecNormalizer.Expand(null!, config));
        Assert.Throws<ArgumentNullException>(() => SpecNormalizer.Expand(tree, null!));
    }
}
