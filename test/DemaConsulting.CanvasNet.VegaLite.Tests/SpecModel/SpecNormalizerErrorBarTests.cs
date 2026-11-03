using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the <c>errorbar</c> and <c>errorband</c> expansion of <see cref="SpecNormalizer.Expand"/> (Phase 10): the layers, the transforms and the properties of the
///     parts that Vega-Lite's <c>normalizeErrorBar</c> and <c>normalizeErrorBand</c> give, the three kinds of input (observations, ends and errors), the orientation rules, the
///     part switches and the specification errors.
/// </summary>
public class SpecNormalizerErrorBarTests
{
    /// <summary>A vertical error bar of the field <c>v</c> by the field <c>g</c>.</summary>
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
        var tree = SpecParser.Parse(json, "error.json", limits);
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
    ///     Gets the transform entries of the view that holds the parts of a composite mark.
    /// </summary>
    /// <param name="expanded">The expanded layers.</param>
    /// <returns>The transform entries.</returns>
    private static IReadOnlyList<SpecNode> Transforms(ExpandedLayers expanded) => expanded.Layers.Root.Children[0].Transform!.Items;

    /// <summary>
    ///     Gets the operations of an <c>aggregate</c> transform entry as <c>op field as</c> texts.
    /// </summary>
    /// <param name="entry">The transform entry.</param>
    /// <returns>The operations.</returns>
    private static List<string> Operations(SpecNode entry) =>
        [.. entry["aggregate"]!.Items.Select(m => $"{m["op"]!.StringValue} {m["field"]!.StringValue} {m["as"]!.StringValue}")];

    /// <summary>
    ///     Gets the <c>calculate</c> entries of the transforms as <c>as = expression</c> texts.
    /// </summary>
    /// <param name="expanded">The expanded layers.</param>
    /// <returns>The calculations.</returns>
    private static List<string> Calculations(ExpandedLayers expanded) =>
        [.. Transforms(expanded).Where(t => t["calculate"] is not null).Select(t => $"{t["as"]!.StringValue} = {t["calculate"]!.StringValue}")];

    /// <summary>
    ///     Validates the layers of a default error bar: one view whose transforms aggregate the standard error and the mean per group and compute the ends, and whose only
    ///     layer is the rule between them; the ticks are off.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBarDefaultIsTheRuleOfTheMeanAndStandardError()
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": "errorbar", {{Vertical}} }""");
        var root = expanded.Layers.Root;

        // Assert: a composite with one view that holds the rule; the views are the composite, the view and the rule
        Assert.True(root.IsExpansion);
        Assert.Equal(["rule"], Marks(expanded));
        Assert.Equal(3, expanded.Layers.ViewCount);
        Assert.Single(root.Children);
        Assert.All(expanded.Layers.Units, u => Assert.Equal("/mark", u.Unit!.Pointer));

        // Assert: the transforms are the aggregate of the extent and the center by group, then the ends computed from them (the expressions are compiled)
        var transforms = Transforms(expanded);
        Assert.Equal(["stderr v extent_v", "mean v center_v"], Operations(transforms[0]));
        Assert.Equal(["g"], transforms[0]["groupby"]!.Items.Select(g => g.StringValue));
        Assert.Equal(["upper_v = datum['center_v'] + datum['extent_v']", "lower_v = datum['center_v'] - datum['extent_v']"], Calculations(expanded));
        Assert.All(transforms.Skip(1), t => Assert.NotNull(expanded.Tree.Expressions.Get(t["calculate"]!)));

        // Assert: the rule goes from the lower to the upper end of the measure, titled with the field, styled as the part of an errorbar
        var rule = expanded.Layers.Units[0].Unit!;
        Assert.Equal(("lower_v", "upper_v", "v"), (FieldOf(rule, "y"), FieldOf(rule, "y2"), rule["encoding"]!["y"]!["title"]!.StringValue));
        Assert.Equal("quantitative", rule["encoding"]!["y"]!["type"]!.StringValue);
        Assert.Equal("g", FieldOf(rule, "x"));
        Assert.Equal("errorbar-rule", MarkOf(rule)["style"]!.StringValue);
    }

    /// <summary>
    ///     Validates the aggregates of every extent and center, as Vega-Lite chooses them: the center follows the extent when only that is given, and the extent follows the
    ///     center.
    /// </summary>
    /// <param name="mark">The mark definition.</param>
    /// <param name="aggregate">The expected operations.</param>
    /// <param name="calculations">The expected number of computed ends (zero when the aggregate gives them).</param>
    [Theory]
    [InlineData("""{ "type": "errorbar" }""", "stderr v extent_v|mean v center_v", 2)]
    [InlineData("""{ "type": "errorbar", "extent": "stdev" }""", "stdev v extent_v|mean v center_v", 2)]
    [InlineData("""{ "type": "errorbar", "extent": "stdev", "center": "median" }""", "stdev v extent_v|median v center_v", 2)]
    [InlineData("""{ "type": "errorbar", "extent": "ci" }""", "ci0 v lower_v|ci1 v upper_v|mean v center_v", 0)]
    [InlineData("""{ "type": "errorbar", "extent": "iqr" }""", "q1 v lower_v|q3 v upper_v|median v center_v", 0)]
    [InlineData("""{ "type": "errorbar", "center": "median" }""", "q1 v lower_v|q3 v upper_v|median v center_v", 0)]
    [InlineData("""{ "type": "errorband", "extent": "ci" }""", "ci0 v lower_v|ci1 v upper_v|mean v center_v", 0)]
    public void SpecNormalizer_Expand_ErrorBarExtentAndCenterChooseTheAggregates(string mark, string aggregate, int calculations)
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "mark": {{mark}}, {{Vertical}} }""");

        // Assert
        Assert.Equal(aggregate.Split('|'), Operations(Transforms(expanded)[0]));
        Assert.Equal(calculations, Calculations(expanded).Count);
    }

    /// <summary>
    ///     Validates that the center of the configuration, which Vega-Lite reads for an errorband too, applies when the mark gives neither a center nor an extent.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ConfiguredCenterApplies()
    {
        // Arrange / Act
        var expanded = Expand($$"""{ "config": { "errorbar": { "center": "median" } }, "mark": "errorband", {{Vertical}} }""");

        // Assert: a median center brings the interquartile extent
        Assert.Equal(["q1 v lower_v", "q3 v upper_v", "median v center_v"], Operations(Transforms(expanded)[0]));
    }

    /// <summary>
    ///     Validates the pre-aggregated range (<c>y2</c> or <c>x2</c>): the ends are copied from the fields by <c>calculate</c> and the rows are not aggregated.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_RangeChannelsAreCopiedWithoutAggregate()
    {
        // Arrange / Act
        var vertical = Expand("""{ "mark": "errorbar", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "lo", "type": "quantitative" }, "y2": { "field": "hi" } } }""");
        var horizontal = Expand("""{ "mark": "errorbar", "encoding": { "y": { "field": "g", "type": "nominal" }, "x": { "field": "lo", "type": "quantitative" }, "x2": { "field": "hi" } } }""");

        // Assert
        Assert.Equal(["upper_lo = datum['hi']", "lower_lo = datum['lo']"], Calculations(vertical));
        Assert.Equal(["upper_lo = datum['hi']", "lower_lo = datum['lo']"], Calculations(horizontal));
        Assert.All(Transforms(vertical), t => Assert.Null(t["aggregate"]));
        Assert.Equal(("lower_lo", "upper_lo"), (FieldOf(vertical.Layers.Units[0].Unit!, "y"), FieldOf(vertical.Layers.Units[0].Unit!, "y2")));
        Assert.Equal(("lower_lo", "upper_lo"), (FieldOf(horizontal.Layers.Units[0].Unit!, "x"), FieldOf(horizontal.Layers.Units[0].Unit!, "x2")));
    }

    /// <summary>
    ///     Validates the pre-aggregated errors: the value plus the error for the upper end, and minus it (or plus the second error) for the lower end.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorChannelsAreAddedToTheValue()
    {
        // Arrange / Act
        var symmetric = Expand("""{ "mark": "errorbar", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative" }, "yError": { "field": "e" } } }""");
        var asymmetric = Expand("""{ "mark": "errorband", "encoding": { "y": { "field": "g", "type": "nominal" }, "x": { "field": "m", "type": "quantitative" }, "xError": { "field": "e" }, "xError2": { "field": "n" } } }""");

        // Assert
        Assert.Equal(["upper_m = datum['m'] + datum['e']", "lower_m = datum['m'] - datum['e']"], Calculations(symmetric));
        Assert.Equal(["upper_m = datum['m'] + datum['e']", "lower_m = datum['m'] + datum['n']"], Calculations(asymmetric));
        Assert.Equal(["area"], Marks(asymmetric));
    }

    /// <summary>
    ///     Validates the ticks that are switched on: a tick at the lower and one at the upper end across the rule, drawn before the rule, with the size and thickness of the mark
    ///     (the thickness is the size of the rule).
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBarTicksComeBeforeTheRule()
    {
        // Arrange / Act
        var vertical = Expand($$"""{ "mark": { "type": "errorbar", "ticks": true, "size": 8, "thickness": 3 }, {{Vertical}} }""");
        var horizontal = Expand("""{ "mark": { "type": "errorbar", "ticks": { "color": "red" } }, "encoding": { "y": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative" } } }""");

        // Assert: the ticks lie across a vertical rule, so they are horizontal; the rule is as thick as the thickness
        Assert.Equal(["tick", "tick", "rule"], Marks(vertical));
        var units = vertical.Layers.Units.Select(u => u.Unit!).ToList();
        Assert.Equal(["lower_v", "upper_v", "lower_v"], units.Select(u => FieldOf(u, "y")));
        Assert.Equal(["horizontal", "horizontal"], units.Take(2).Select(u => MarkOf(u)["orient"]!.StringValue));
        Assert.All(units.Take(2), u => Assert.Equal((8.0, 3.0, "errorbar-ticks"), (MarkOf(u)["size"]!.NumberValue, MarkOf(u)["thickness"]!.NumberValue, MarkOf(u)["style"]!.StringValue)));
        Assert.Equal(3.0, MarkOf(units[2])["size"]!.NumberValue);
        Assert.Null(units[0]["encoding"]!["y2"]);

        // Assert: across a horizontal rule the ticks are vertical and take the properties of the part
        Assert.Equal(["tick", "tick", "rule"], Marks(horizontal));
        Assert.All(horizontal.Layers.Units.Take(2), u => Assert.Equal(("vertical", "red"), (MarkOf(u.Unit!)["orient"]!.StringValue, MarkOf(u.Unit!)["color"]!.StringValue)));
    }

    /// <summary>
    ///     Validates the part switches: the mark's own setting beats the configuration, the configuration's object or <c>true</c> switches a part on, and a mark whose
    ///     parts are all off has no layer; the mark's <c>color</c> and <c>opacity</c> reach every part, a part's own properties win.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBarPartsFollowMarkAndConfiguration()
    {
        // Arrange / Act
        var none = Expand($$"""{ "mark": { "type": "errorbar", "rule": false }, {{Vertical}} }""");
        var fromConfig = Expand($$"""{ "config": { "errorbar": { "ticks": true } }, "mark": "errorbar", {{Vertical}} }""");
        var switchedOff = Expand($$"""{ "config": { "errorbar": { "ticks": true } }, "mark": { "type": "errorbar", "ticks": false }, {{Vertical}} }""");
        var colored = Expand($$"""{ "mark": { "type": "errorbar", "color": "green", "opacity": 0.5, "rule": { "color": "red" } }, {{Vertical}} }""");

        // Assert
        Assert.Empty(none.Layers.Units);
        Assert.Equal(["tick", "tick", "rule"], Marks(fromConfig));
        Assert.Equal(["rule"], Marks(switchedOff));
        var rule = MarkOf(colored.Layers.Units[0].Unit!);
        Assert.Equal(("red", 0.5), (rule["color"]!.StringValue, rule["opacity"]!.NumberValue));
    }

    /// <summary>
    ///     Validates the channels the rule shares: the grouping channels (with their fields grouped), a color, a detail; the size channel is dropped from an errorbar and the
    ///     tooltip from every part; the written <c>title</c>, <c>scale</c> and <c>axis</c> of the measure go to the parts.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBarSharesTheOtherChannels()
    {
        // Arrange / Act
        var expanded = Expand(
            """
            { "mark": "errorbar", "encoding": {
                "x": { "field": "g", "type": "nominal" },
                "y": { "field": "v", "type": "quantitative", "title": "Value", "scale": { "zero": false }, "axis": { "grid": false } },
                "color": { "field": "c", "type": "nominal" }, "size": { "field": "s", "type": "quantitative" }, "tooltip": { "field": "t" }, "opacity": { "value": 0.5 } } }
            """);

        // Assert: grouped by every channel with a field, in the order they are written
        Assert.Equal(["g", "c", "s"], Transforms(expanded)[0]["groupby"]!.Items.Select(g => g.StringValue));
        var rule = expanded.Layers.Units[0].Unit!;
        Assert.Equal("c", FieldOf(rule, "color"));
        Assert.Equal(0.5, rule["encoding"]!["opacity"]!["value"]!.NumberValue);
        Assert.Null(rule["encoding"]!["size"]);
        Assert.Null(rule["encoding"]!["tooltip"]);
        var y = rule["encoding"]!["y"]!;
        Assert.Equal(("Value", false), (y["title"]!.StringValue, y["scale"]!["zero"]!.BoolValue));
        Assert.False(y["axis"]!["grid"]!.BoolValue);
    }

    /// <summary>
    ///     Validates the band of an errorband when only the measure is encoded: a <c>rect</c> between the ends at 30% opacity, no borders; the borders that are switched on are
    ///     <c>rule</c> marks at the lower and upper ends, drawn after the band.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBandOneDimensionalIsARectWithRuleBorders()
    {
        // Arrange / Act
        var plain = Expand("""{ "mark": "errorband", "encoding": { "y": { "field": "v", "type": "quantitative" } } }""");
        var bordered = Expand("""{ "mark": { "type": "errorband", "borders": true, "interpolate": "monotone" }, "encoding": { "y": { "field": "v", "type": "quantitative" } } }""");

        // Assert: the default band is a 30% opaque rect between the ends, the groups of rows are the whole data
        Assert.Equal(["rect"], Marks(plain));
        var band = plain.Layers.Units[0].Unit!;
        Assert.Equal(("lower_v", "upper_v"), (FieldOf(band, "y"), FieldOf(band, "y2")));
        Assert.Equal(("errorband-band", 0.3), (MarkOf(band)["style"]!.StringValue, MarkOf(band)["opacity"]!.NumberValue));
        Assert.Empty(Transforms(plain)[0]["groupby"]!.Items);

        // Assert: the borders are rules; the interpolation of a one-dimensional band is ignored
        Assert.Equal(["rect", "rule", "rule"], Marks(bordered));
        Assert.Equal(["lower_v", "upper_v"], bordered.Layers.Units.Skip(1).Select(u => FieldOf(u.Unit!, "y")));
        Assert.All(bordered.Layers.Units, u => Assert.Null(MarkOf(u.Unit!)["interpolate"]));
        Assert.Equal("errorband-borders", MarkOf(bordered.Layers.Units[1].Unit!)["style"]!.StringValue);
        Assert.Null(bordered.Layers.Units[1].Unit!["encoding"]!["y2"]);
    }

    /// <summary>
    ///     Validates the two-dimensional errorband (both <c>x</c> and <c>y</c>): an <c>area</c> and <c>line</c> borders, which take the interpolation and, only with an
    ///     interpolation, the tension.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBandTwoDimensionalIsAnAreaWithLineBorders()
    {
        // Arrange / Act
        const string encoding = """
            "encoding": { "x": { "field": "t", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative" } }
            """;
        var interpolated = Expand($$"""{ "mark": { "type": "errorband", "borders": true, "interpolate": "step", "tension": 0.5 }, {{encoding}} }""");
        var tensionOnly = Expand($$"""{ "mark": { "type": "errorband", "borders": true, "tension": 0.5 }, {{encoding}} }""");

        // Assert
        Assert.Equal(["area", "line", "line"], Marks(interpolated));
        Assert.All(interpolated.Layers.Units, u => Assert.Equal(("step", 0.5), (MarkOf(u.Unit!)["interpolate"]!.StringValue, MarkOf(u.Unit!)["tension"]!.NumberValue)));
        Assert.Equal(["t"], Transforms(interpolated)[0]["groupby"]!.Items.Select(g => g.StringValue));
        Assert.All(tensionOnly.Layers.Units, u =>
        {
            Assert.Null(MarkOf(u.Unit!)["interpolate"]);
            Assert.Null(MarkOf(u.Unit!)["tension"]);
        });
    }

    /// <summary>
    ///     Validates the part switches and properties of an errorband from the mark and from the configuration, and that the size channel is kept (an errorbar drops it).
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBandPartsAndConfiguration()
    {
        // Arrange / Act
        var noBand = Expand($$"""{ "mark": { "type": "errorband", "band": false, "borders": { "color": "red" } }, {{Vertical}} }""");
        var configured = Expand($$"""{ "config": { "errorband": { "band": { "opacity": 0.6 }, "borders": true } }, "mark": "errorband", {{Vertical}} }""");
        var sized = Expand("""{ "mark": "errorband", "encoding": { "y": { "field": "v", "type": "quantitative" }, "size": { "field": "s", "type": "quantitative" } } }""");

        // Assert
        Assert.Equal(["line", "line"], Marks(noBand));
        Assert.All(noBand.Layers.Units, u => Assert.Equal("red", MarkOf(u.Unit!)["color"]!.StringValue));
        Assert.Equal(["area", "line", "line"], Marks(configured));
        Assert.Equal(0.6, MarkOf(configured.Layers.Units[0].Unit!)["opacity"]!.NumberValue);
        Assert.Equal("s", FieldOf(sized.Layers.Units[0].Unit!, "size"));
    }

    /// <summary>
    ///     Validates the inheritance: a composite mark in a layer takes the encoding of the layered view, and the specification's own transform is not touched by the
    ///     expansion.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBarInALayerInheritsTheEncoding()
    {
        // Arrange / Act
        var expanded = Expand(
            """
            { "encoding": { "x": { "field": "g", "type": "nominal" } },
              "layer": [ { "mark": "point", "encoding": { "y": { "field": "v", "type": "quantitative" } } },
                         { "mark": "errorbar", "encoding": { "y": { "field": "v", "type": "quantitative" } }, "transform": [ { "filter": "datum.v > 0" } ] } ] }
            """);

        // Assert: the point and the rule, which has the inherited x; the layer's own filter is a transform of the composite, which is the second layer of the root
        Assert.Equal(["point", "rule"], Marks(expanded));
        Assert.Equal("g", FieldOf(expanded.Layers.Units[1].Unit!, "x"));
        Assert.Equal("g", expanded.Layers.Root.Children[1].Children[0].Transform!.Items[0]["groupby"]!.Items[0].StringValue);
    }

    /// <summary>
    ///     Validates the orientation: the range or error channels decide, then (for observations) the continuous channel, and a mark that cannot be decided is a
    ///     specification error at the mark.
    /// </summary>
    /// <param name="mark">The mark type.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="orient">The expected orientation, or null when the encoding is invalid.</param>
    [Theory]
    [InlineData("errorbar", """{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }""", "vertical")]
    [InlineData("errorbar", """{ "y": { "field": "g", "type": "nominal" }, "x": { "field": "v", "type": "quantitative" } }""", "horizontal")]
    [InlineData("errorband", """{ "x": { "field": "t", "type": "temporal" }, "y": { "field": "v", "type": "quantitative" } }""", "vertical")]
    [InlineData("errorband", """{ "x": { "field": "v", "type": "quantitative" }, "y": { "field": "t", "type": "temporal" } }""", "horizontal")]
    [InlineData("errorbar", """{ "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } }""", "vertical")]
    [InlineData("errorbar", """{ "x": { "field": "a", "type": "quantitative" }, "x2": { "field": "b" }, "y": { "field": "g", "type": "nominal" } }""", "horizontal")]
    [InlineData("errorbar", """{ "y": { "field": "a", "type": "quantitative" }, "y2": { "field": "b" }, "x": { "field": "a", "type": "quantitative" } }""", "vertical")]
    [InlineData("errorbar", """{ "x": { "field": "a", "type": "quantitative" }, "xError": { "field": "b" }, "y": { "field": "g", "type": "nominal" } }""", "horizontal")]
    [InlineData("errorband", """{ "x": { "datum": "all" }, "y": { "field": "t", "type": "temporal" } }""", "vertical")]
    [InlineData("errorbar", """{ "x": { "datum": 5 }, "y": { "field": "g", "type": "nominal" } }""", "horizontal")]
    [InlineData("errorbar", """{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "g", "type": "nominal" } }""", null)]
    [InlineData("errorbar", """{ "x": { "field": "g", "type": "nominal" } }""", null)]
    [InlineData("errorbar", """{ "x": { "field": "a", "type": "quantitative" }, "x2": { "field": "b" }, "y2": { "field": "b" } }""", null)]
    [InlineData("errorbar", """{ "x": { "field": "a", "type": "quantitative" }, "x2": { "field": "b" }, "yError": { "field": "b" } }""", null)]
    [InlineData("errorbar", """{ "x": { "field": "a", "type": "quantitative" }, "xError": { "field": "b" }, "yError": { "field": "b" } }""", null)]
    [InlineData("errorbar", """{ "x": { "field": "a", "type": "quantitative" }, "xError2": { "field": "b" } }""", null)]
    [InlineData("errorbar", """{ "x": { "field": "a", "type": "nominal" }, "xError": { "field": "b" } }""", null)]
    [InlineData("errorbar", """{ "y": { "field": "a", "type": "nominal" }, "y2": { "field": "b" } }""", null)]
    public void SpecNormalizer_TryOrientation_DecidesFromTheChannels(string mark, string encoding, string? orient)
    {
        // Arrange
        var tree = SpecParser.Parse($$"""{ "mark": "{{mark}}", "encoding": {{encoding}} }""");

        // Act
        var decided = SpecNormalizer.TryOrientation(mark, null, tree.Root["encoding"], out var actual, out var problem);

        // Assert
        Assert.Equal(orient is not null, decided);
        if (orient is null)
        {
            Assert.False(string.IsNullOrEmpty(problem));
            Assert.Throws<VegaLiteSpecException>(() => Expand($$"""{ "mark": "{{mark}}", "encoding": {{encoding}} }"""));
        }
        else
        {
            Assert.Equal(orient, actual);
            Assert.Null(problem);
        }
    }

    /// <summary>
    ///     Validates that an explicit <c>orient</c> decides observations whose channels are both continuous, but not a range or errors.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ExplicitOrientDecidesObservationsOnly()
    {
        // Arrange / Act
        var horizontal = Expand("""{ "mark": { "type": "errorbar", "orient": "horizontal" }, "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } }""");
        var ranged = Expand("""{ "mark": { "type": "errorbar", "orient": "horizontal" }, "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "a", "type": "quantitative" }, "y2": { "field": "b" } } }""");

        // Assert: the explicit orientation makes x the measure and y the group; the range channel makes y the measure whatever the orient says
        Assert.Equal(("lower_a", "b"), (FieldOf(horizontal.Layers.Units[0].Unit!, "x"), Transforms(horizontal)[0]["groupby"]!.Items[0].StringValue));
        Assert.Equal(("lower_a", "upper_a"), (FieldOf(ranged.Layers.Units[0].Unit!, "y"), FieldOf(ranged.Layers.Units[0].Unit!, "y2")));
    }

    /// <summary>
    ///     Validates the specification error of a range channel that is not a field (the error is at the mark).
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorChannelWithoutFieldIsASpecificationError()
    {
        // Arrange
        const string json = """{ "mark": "errorbar", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative" }, "yError": { "datum": 3 } } }""";

        // Act
        var error = Assert.Throws<VegaLiteSpecException>(() => Expand(json));

        // Assert
        Assert.Equal("/mark", error.Location.Pointer);
        Assert.Contains("yError", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the views of the expansion count against <see cref="VegaLiteLimits.MaxViews"/> (three for an error bar: the composite, its view and the rule; five with
    ///     the ticks).
    /// </summary>
    [Fact]
    public void SpecNormalizer_Expand_ErrorBarViewsAreCounted()
    {
        // Arrange
        var json = $$"""{ "mark": { "type": "errorbar", "ticks": true }, {{Vertical}} }""";

        // Act
        var allowed = Expand(json, new VegaLiteLimits { MaxViews = 5 });

        // Assert
        Assert.Equal(5, allowed.Layers.ViewCount);
        var error = Assert.Throws<VegaLiteRenderException>(() => Expand(json, new VegaLiteLimits { MaxViews = 4 }));
        Assert.Contains("MaxViews", error.Message, StringComparison.Ordinal);
    }
}
