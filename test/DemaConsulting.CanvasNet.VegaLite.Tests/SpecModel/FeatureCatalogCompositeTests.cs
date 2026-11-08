using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of <see cref="FeatureCatalog"/> for the composite marks <c>boxplot</c>, <c>errorbar</c> and <c>errorband</c> (Phase 10): the marks are drawn (the identifiers
///     <c>vl-mark-boxplot</c>, <c>vl-mark-errorbar</c> and <c>vl-mark-errorband</c> are retired), a <c>bin</c>, an <c>aggregate</c> and a <c>timeUnit</c> of any form on a channel that groups the rows
///     are applied (Phase 14: <c>vl-composite-grouping</c> is retired), the error channels are reported on the marks that do not use them; and of the configuration defaults of the composite marks
///     (<see cref="ConfigResolver"/>) that the expansion reads.
/// </summary>
public class FeatureCatalogCompositeTests
{
    /// <summary>
    ///     Parses a specification and lists the findings of the catalog as identifier and pointer.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The findings.</returns>
    private static List<(string Id, string Pointer)> Visit(string json) => [.. FeatureCatalog.Visit(SpecParser.Parse(json)).Select(r => (r.Id, r.Location.Pointer))];

    /// <summary>
    ///     Validates that a plain box plot is reported for nothing and that the shape and the size channels (which the outliers and the box draw) are accepted.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_BoxPlotIsSupportedWithItsChannels()
    {
        // Arrange / Act
        var reports = Visit("""
            { "mark": { "type": "boxplot", "extent": 2, "ticks": true },
              "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" },
                            "shape": { "field": "g", "type": "nominal" }, "size": { "value": 5 }, "tooltip": { "field": "v" }, "opacity": { "value": 0.5 } } }
            """);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a bin, an aggregate and a time unit of any form on a grouping channel are applied and not reported (Phase 14; the identifier <c>vl-composite-grouping</c> is retired).
    /// </summary>
    /// <param name="channel">The grouping channel definition.</param>
    [Theory]
    [InlineData("""{ "field": "g", "type": "quantitative", "bin": true }""")]
    [InlineData("""{ "field": "g", "type": "temporal", "timeUnit": { "maxbins": 4 } }""")]
    [InlineData("""{ "field": "g", "type": "temporal", "timeUnit": "binnedyearmonth" }""")]
    [InlineData("""{ "field": "g", "type": "ordinal", "bin": { "maxbins": 5 } }""")]
    [InlineData("""{ "field": "g", "type": "quantitative", "aggregate": "mean" }""")]
    public void FeatureCatalog_Visit_BinOrTimeUnitOrAggregateOfAGroupingChannelIsNotReported(string channel)
    {
        // Arrange / Act
        var reports = Visit($$"""{ "mark": "boxplot", "encoding": { "x": {{channel}}, "y": { "field": "v", "type": "quantitative" } } }""");

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a time unit on a grouping channel is applied (the expansion derives the unit's field) and is not reported, for each of the composite marks.
    /// </summary>
    /// <param name="mark">The composite mark.</param>
    [Theory]
    [InlineData("boxplot")]
    [InlineData("errorbar")]
    [InlineData("errorband")]
    public void FeatureCatalog_Visit_TimeUnitOfAGroupingChannelIsNotReported(string mark)
    {
        // Arrange / Act
        var reports = Visit($$"""{ "mark": "{{mark}}", "encoding": { "x": { "field": "d", "type": "temporal", "timeUnit": "year" }, "y": { "field": "v", "type": "quantitative" } } }""");

        // Assert
        Assert.DoesNotContain(reports, r => r.Id == "vl-composite-grouping");
    }

    /// <summary>
    ///     Validates that the grouping of the three composite marks reports nothing whatever the orientation and the layering: a bin of the measure channel, of the other position channel
    ///     of a horizontal mark, and of a channel inherited from the layers above.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_GroupingFormsAreNotReportedWhateverTheOrientationAndLayers()
    {
        // Arrange / Act
        var measure = Visit("""
            { "mark": "boxplot", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative", "bin": true },
                                               "tooltip": { "field": "v", "bin": true } } }
            """);
        var horizontal = Visit("""
            { "mark": "boxplot", "encoding": { "y": { "field": "g", "type": "ordinal", "bin": true }, "x": { "field": "v", "type": "quantitative" } } }
            """);
        var layered = Visit("""
            { "encoding": { "x": { "field": "g", "type": "ordinal", "bin": true } },
              "layer": [ { "mark": "boxplot", "encoding": { "y": { "field": "v", "type": "quantitative" } } },
                         { "mark": "bar", "encoding": { "y": { "field": "v", "type": "quantitative" } } } ] }
            """);

        // Assert: the bin of the measure and of the tooltip is the ordinary bin of the channel, which the catalog supports
        Assert.DoesNotContain(measure, r => r.Id == "vl-composite-grouping");
        Assert.Empty(horizontal);
        Assert.Empty(layered);
    }

    /// <summary>
    ///     Validates that a box plot with an orientation that cannot be decided adds no grouping report (the render fails with a specification error instead), and that the
    ///     retired identifier is never produced.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_UndecidableBoxPlotAndRetiredIdentifier()
    {
        // Arrange / Act
        var reports = Visit("""{ "mark": "boxplot", "encoding": { "x": { "field": "g", "type": "ordinal", "bin": true }, "y": { "field": "h", "type": "nominal" } } }""");

        // Assert
        Assert.DoesNotContain(reports, r => r.Id is "vl-composite-grouping" or "vl-mark-boxplot");
    }

    /// <summary>
    ///     Validates that a plain error bar and error band are reported for nothing, with the range (<c>x2</c>, <c>y2</c>) and the errors (<c>xError</c>, <c>yError2</c>...) they
    ///     use, and that the retired identifiers <c>vl-mark-errorbar</c> and <c>vl-mark-errorband</c> are never produced.
    /// </summary>
    /// <param name="mark">The mark type.</param>
    /// <param name="encoding">The encoding.</param>
    [Theory]
    [InlineData("errorbar", """{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" }, "color": { "field": "g", "type": "nominal" } }""")]
    [InlineData("errorbar", """{ "x": { "field": "g", "type": "nominal" }, "y": { "field": "lo", "type": "quantitative" }, "y2": { "field": "hi" } }""")]
    [InlineData("errorbar", """{ "y": { "field": "g", "type": "nominal" }, "x": { "field": "m", "type": "quantitative" }, "xError": { "field": "e" }, "xError2": { "field": "n" } }""")]
    [InlineData("errorband", """{ "x": { "field": "t", "type": "temporal" }, "y": { "field": "m", "type": "quantitative" }, "yError": { "field": "e" }, "yError2": { "field": "n" } }""")]
    [InlineData("errorband", """{ "y": { "field": "lo", "type": "quantitative" }, "y2": { "field": "hi" } }""")]
    public void FeatureCatalog_Visit_ErrorBarAndErrorBandAreSupportedWithTheirChannels(string mark, string encoding)
    {
        // Arrange / Act
        var reports = Visit($$"""{ "mark": { "type": "{{mark}}", "extent": "stdev", "ticks": true, "interpolate": "linear" }, "encoding": {{encoding}} }""");

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that the error channels are reported, with the stable identifiers of the plan, on a mark that does not use them, once for each channel, at the channel;
    ///     and that <c>x2</c> and <c>y2</c> are still reported for a mark that has no range.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ErrorChannelsOfOtherMarksAreReported()
    {
        // Arrange / Act
        var reports = Visit("""
            { "mark": "point", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "m", "type": "quantitative" },
                "xError": { "field": "e" }, "xError2": { "field": "e" }, "yError": { "field": "e" }, "yError2": { "field": "e" } } }
            """);

        // Assert
        Assert.Equal(
            [("vl-channel-xerror", "/encoding/xError"), ("vl-channel-xerror2", "/encoding/xError2"), ("vl-channel-yerror", "/encoding/yError"), ("vl-channel-yerror2", "/encoding/yError2")],
            reports);
    }

    /// <summary>
    ///     Validates the channels an errorbar or errorband does not draw: the size channel of an errorbar is dropped by Vega-Lite and reported, a shape or angle is reported,
    ///     the stroke dash of the rule is drawn.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ErrorBarReportsTheChannelsItDoesNotDraw()
    {
        // Arrange / Act
        var reports = Visit("""
            { "mark": "errorbar", "encoding": { "x": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" },
                "size": { "field": "v", "type": "quantitative" }, "shape": { "field": "g", "type": "nominal" }, "strokeDash": { "field": "g", "type": "nominal" } } }
            """);

        // Assert: the size and the shape; the stroke dash is drawn by the rule
        Assert.Equal([("vl-channel-size", "/encoding/size"), ("vl-channel-shape", "/encoding/shape")], reports);
    }

    /// <summary>
    ///     Validates that the grouping of an error bar and an error band reports nothing, and that the orientation comes from the range or error channels when they are given.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ErrorBarGroupingIsNotReported()
    {
        // Arrange / Act
        var raw = Visit("""{ "mark": "errorbar", "encoding": { "x": { "field": "g", "type": "ordinal", "bin": true }, "y": { "field": "v", "type": "quantitative" } } }""");
        var band = Visit("""{ "mark": "errorband", "encoding": { "x": { "field": "t", "type": "temporal", "timeUnit": { "maxbins": 4 } }, "y": { "field": "v", "type": "quantitative" } } }""");
        var ranged = Visit("""{ "mark": "errorbar", "encoding": { "y": { "field": "g", "type": "ordinal", "bin": true }, "x": { "field": "lo", "type": "quantitative" }, "x2": { "field": "hi" } } }""");

        // Assert
        Assert.Empty(raw);
        Assert.Empty(band);
        Assert.Empty(ranged);
    }

    /// <summary>
    ///     Validates the configuration defaults of the error bar and the error band that the expansion reads (Vega-Lite's <c>config.ts</c>): the mean center, the rule on and
    ///     the ticks off; the band at 30% opacity and no borders.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_CarriesTheErrorBarAndErrorBandDefaults()
    {
        // Arrange
        var tree = SpecParser.Parse("""{ "config": { "errorbar": { "ticks": true }, "errorband": { "band": { "opacity": 0.5 } } }, "mark": "errorbar" }""");

        // Act
        var defaults = ConfigResolver.Resolve(SpecParser.Parse("""{ "mark": "errorbar" }"""), null);
        var configured = ConfigResolver.Resolve(tree, null);

        // Assert
        Assert.Equal("mean", defaults.GetString("errorbar.center"));
        Assert.True(defaults.GetBool("errorbar.rule"));
        Assert.False(defaults.GetBool("errorbar.ticks"));
        Assert.Equal(0.3, defaults.GetNumber("errorband.band.opacity"));
        Assert.False(defaults.GetBool("errorband.borders"));
        Assert.True(configured.GetBool("errorbar.ticks"));
        Assert.Equal(0.5, configured.GetNumber("errorband.band.opacity"));
        Assert.True(configured.GetBool("errorbar.rule"));
    }

    /// <summary>
    ///     Validates the configuration defaults of the box plot that the expansion reads (Vega-Lite's <c>config.ts</c>): size 14, extent 1.5, the white median, and no end
    ///     ticks, which the specification's own configuration overrides.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_CarriesTheBoxPlotDefaults()
    {
        // Arrange
        var tree = SpecParser.Parse("""{ "config": { "boxplot": { "ticks": true, "median": { "strokeWidth": 2 } } }, "mark": "boxplot" }""");

        // Act
        var defaults = ConfigResolver.Resolve(SpecParser.Parse("""{ "mark": "boxplot" }"""), null);
        var configured = ConfigResolver.Resolve(tree, null);

        // Assert
        Assert.Equal(14, defaults.GetNumber("boxplot.size"));
        Assert.Equal(1.5, defaults.GetNumber("boxplot.extent"));
        Assert.Equal("white", defaults.GetString("boxplot.median.color"));
        Assert.Equal(ConfigNodeKind.Null, defaults.Get("boxplot.ticks")!.Kind);
        Assert.True(defaults.Get("boxplot.box")!.IsObject);
        Assert.True(configured.GetBool("boxplot.ticks"));
        Assert.Equal("white", configured.GetString("boxplot.median.color"));
        Assert.Equal(2, configured.GetNumber("boxplot.median.strokeWidth"));
    }
}
