using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the <c>labelExpr</c> of <see cref="DemaConsulting.CanvasNet.VegaLite.Guides.AxisBuilder"/>: the labels are replaced before they are measured and thinned, with the datum Vega gives an axis label.
/// </summary>
public class AxisBuilderLabelExprTests
{
    /// <summary>A scatter whose x domain is 0 to 100 (ticks 0, 10, ..., 100 over 300 pixels).</summary>
    private const string Linear = """
        { "data": { "values": [ { "v": 0, "w": 0 }, { "v": 100, "w": 50 } ] }, "mark": "point",
          "encoding": { "x": { "field": "v", "type": "quantitative" }, "y": { "field": "w", "type": "quantitative" } } }
        """;

    /// <summary>
    ///     Builds the linear chart with an <c>axis</c> object on x.
    /// </summary>
    /// <param name="axis">The axis JSON.</param>
    /// <returns>The specification text.</returns>
    private static string WithAxis(string axis) =>
        Linear.Replace("\"x\": { \"field\": \"v\", \"type\": \"quantitative\" }", "\"x\": { \"field\": \"v\", \"type\": \"quantitative\", \"axis\": " + axis + " }", StringComparison.Ordinal);

    /// <summary>
    ///     Gets the texts of the labels of an axis.
    /// </summary>
    /// <param name="axis">The axis.</param>
    /// <returns>The texts in axis order (the labels that were not removed).</returns>
    private static List<string> Labels(DemaConsulting.CanvasNet.VegaLite.Guides.AxisResult axis) =>
        [.. ChartTestHelpers.Of<SceneText>(axis.Group, "axis-label").Select(l => l.Text)];

    /// <summary>
    ///     Validates the common <c>datum.label + '%'</c>: every label gets a percent sign.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprAppendsPercent()
    {
        // Act: the drawn labels need room, so the labels that overlap are left out with the default parity rule unless the overlap removal is off
        var spec = WithAxis("{ \"labelOverlap\": false, \"labelExpr\": \"datum.label + '%'\" }");
        var axis = ChartTestHelpers.Axis(spec, "x", 300, 200)!;
        var thinned = ChartTestHelpers.Axis(WithAxis("{ \"tickCount\": 5, \"labelExpr\": \"datum.label + '%'\" }"), "x", 300, 200)!;

        // Assert
        Assert.Equal(["0%", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%"], Labels(axis));
        Assert.Equal(["0%", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%", "100%"], axis.Ticks.Select(t => t.Label));
        Assert.Equal(["0%", "20%", "40%", "60%", "80%", "100%"], Labels(thinned));
    }

    /// <summary>
    ///     Validates a conditional expression that blanks some labels: the blank ones are not drawn and take no part in the overlap removal.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprConditionalBlanksTicks()
    {
        // Act
        var axis = ChartTestHelpers.Axis(WithAxis("{ \"labelExpr\": \"datum.value % 20 ? '' : datum.label\" }"), "x", 300, 200)!;

        // Assert
        Assert.Equal(["0", "20", "40", "60", "80", "100"], Labels(axis).Where(t => t.Length > 0));
        Assert.Equal(11, axis.Ticks.Count);
    }

    /// <summary>
    ///     Validates the datum of an axis: <c>index</c> is a fraction of the way along the ticks and <c>tickIndex</c> a whole number.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprIndexIsFractionAndTickIndexIsInteger()
    {
        // Act: five ticks on 0 to 40 (counts of 40 px over 120 px: 3 -> 0, 20, 40 with a step of 20)
        var spec = Linear.Replace("\"v\": 100", "\"v\": 40", StringComparison.Ordinal);
        var axis = ChartTestHelpers.Axis(
            spec.Replace("\"x\": { \"field\": \"v\", \"type\": \"quantitative\" }", "\"x\": { \"field\": \"v\", \"type\": \"quantitative\", \"axis\": { \"values\": [0, 10, 20, 30, 40], \"labelExpr\": \"datum.tickIndex + '@' + datum.index\" } }", StringComparison.Ordinal),
            "x",
            300,
            200)!;

        // Assert: index = i / (n - 1)
        Assert.Equal(["0@0", "1@0.25", "2@0.5", "3@0.75", "4@1"], Labels(axis));
    }

    /// <summary>
    ///     Validates that the temporal axis gives a date as <c>datum.value</c>, so the time functions work on it.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprTemporalAxisUsesDateValue()
    {
        // Arrange
        const string spec = """
            { "data": { "values": [ { "d": "2024-01-01", "n": 1 }, { "d": "2024-03-01", "n": 2 }, { "d": "2024-05-01", "n": 3 } ] }, "mark": "line",
              "encoding": { "x": { "field": "d", "type": "temporal", "axis": { "values": ["2024-01-01", "2024-03-01"], "labelExpr": "timeFormat(datum.value, '%b %Y') + ' / ' + year(datum.value)" } },
                            "y": { "field": "n", "type": "quantitative" } } }
            """;

        // Act
        var axis = ChartTestHelpers.Axis(spec, "x", 300, 200)!;

        // Assert
        Assert.Equal(["Jan 2024 / 2024", "Mar 2024 / 2024"], Labels(axis));
    }

    /// <summary>
    ///     Validates the band axis: the value is the category and the label its text.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprBandAxisGivesCategoryAsValueAndLabel()
    {
        // Arrange
        const string spec = """
            { "data": { "values": [ { "k": "a", "n": 1 }, { "k": "b", "n": 2 } ] }, "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal", "axis": { "labelExpr": "upper(datum.value) + datum.label" } }, "y": { "field": "n", "type": "quantitative" } } }
            """;

        // Act
        var axis = ChartTestHelpers.Axis(spec, "x", 300, 200)!;

        // Assert
        Assert.Equal(["Aa", "Bb"], Labels(axis));
    }

    /// <summary>
    ///     Validates that labels are replaced before they are measured: a longer label makes the axis extent grow.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprLongerLabelWidensTheAxisExtent()
    {
        // Act: the y axis of a plain chart and of one whose labels are longer
        var plain = ChartTestHelpers.Axis(Linear, "y", 300, 200)!;
        var longer = ChartTestHelpers.Axis(Linear.Replace("\"y\": { \"field\": \"w\", \"type\": \"quantitative\" }", "\"y\": { \"field\": \"w\", \"type\": \"quantitative\", \"axis\": { \"labelExpr\": \"'value ' + datum.label\" } }", StringComparison.Ordinal), "y", 300, 200)!;

        // Assert: the left extent follows the replaced text
        Assert.True(longer.Bounds.X1 < plain.Bounds.X1 - 20, $"{longer.Bounds.X1} should be well left of {plain.Bounds.X1}");
    }

    /// <summary>
    ///     Validates that the overlap removal sees the replaced text: labels that become blank free the room for their neighbors.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprOverlapRemovalUsesTheReplacedText()
    {
        // Arrange: ticks every 10 with very long labels would overlap; with blanks between them nothing needs removal
        var longLabels = WithAxis("{ \"values\": [0, 10, 20, 30, 40, 50], \"labelOverlap\": \"parity\", \"labelExpr\": \"datum.label + ' kilograms'\" }");
        var blanked = WithAxis("{ \"values\": [0, 10, 20, 30, 40, 50], \"labelOverlap\": \"parity\", \"labelExpr\": \"datum.value % 30 ? '' : datum.label + ' kilograms'\" }");

        // Act
        var all = Labels(ChartTestHelpers.Axis(longLabels, "x", 300, 200)!);
        var some = Labels(ChartTestHelpers.Axis(blanked, "x", 300, 200)!).Where(t => t.Length > 0).ToList();

        // Assert: the long labels at 60 px per step overlap and are thinned; the two that remain after blanking (at 0 and 180 px) fit
        Assert.True(all.Count < 6, "overlapping labels are removed");
        Assert.Equal(["0 kilograms", "30 kilograms"], some);
    }

    /// <summary>
    ///     Validates that <c>labelLimit</c> truncates the replaced text when the label is drawn.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprLabelLimitAppliesToReplacedText()
    {
        // Act
        var axis = ChartTestHelpers.Axis(WithAxis("{ \"labelLimit\": 30, \"labelExpr\": \"'abcdefghijklmnop'\" }"), "x", 300, 200)!;
        var label = ChartTestHelpers.Of<SceneText>(axis.Group, "axis-label")[0];

        // Assert: the scene keeps the full text and carries the limit
        Assert.Equal("abcdefghijklmnop", label.Text);
        Assert.Equal(30, label.Limit);
    }

    /// <summary>
    ///     Validates that an axis without labels does not use its expression: a configured text that is not valid is not even compiled.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprIsNotUsedWhenLabelsAreOff()
    {
        // Act: the configured expression is invalid, and the axis has no labels
        var axis = ChartTestHelpers.Axis(WithAxis("{ \"labels\": false }"), "x", 300, 200, """{ "axis": { "labelExpr": "datum.label +" } }""")!;

        // Assert
        Assert.Empty(ChartTestHelpers.Of<SceneText>(axis.Group, "axis-label"));
    }

    /// <summary>
    ///     Validates the configuration chain: <c>config.axis</c> applies to every axis, <c>axisY</c> beats it for y, <c>axisQuantitative</c> (Vega-Lite's own) beats both, and the axis object beats all.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprConfigPrecedenceFollowsTheAxisChain()
    {
        // Arrange
        const string general = """{ "axis": { "labelExpr": "datum.label + 'A'" }, "axisY": { "labelExpr": "datum.label + 'Y'" } }""";
        const string typed = """{ "axis": { "labelExpr": "datum.label + 'A'" }, "axisY": { "labelExpr": "datum.label + 'Y'" }, "axisQuantitative": { "labelExpr": "datum.label + 'Q'" } }""";

        // Act
        var x = ChartTestHelpers.Axis(Linear, "x", 300, 200, general)!;
        var y = ChartTestHelpers.Axis(Linear, "y", 300, 200, general)!;
        var typedX = ChartTestHelpers.Axis(Linear, "x", 300, 200, typed)!;
        var own = ChartTestHelpers.Axis(WithAxis("{ \"labelExpr\": \"datum.label + 'X'\" }"), "x", 300, 200, typed)!;

        // Assert
        Assert.Equal("0A", Labels(x)[0]);
        Assert.Equal("0Y", Labels(y)[0]);
        Assert.Equal("0Q", Labels(typedX)[0]);
        Assert.Equal("0X", Labels(own)[0]);
    }

    /// <summary>
    ///     Validates that an array result is a multi-line label.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprArrayResultIsMultiLine()
    {
        // Act
        var axis = ChartTestHelpers.Axis(WithAxis("{ \"values\": [0], \"labelExpr\": \"['top', datum.label]\" }"), "x", 300, 200)!;
        var label = ChartTestHelpers.Of<SceneText>(axis.Group, "axis-label")[0];

        // Assert
        Assert.Equal(["top", "0"], label.Lines);
    }

    /// <summary>
    ///     Validates that an expression of the caller's configuration that is not valid is a specification error.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_LabelExprInvalidConfiguredTextIsSpecException()
    {
        // Act / Assert
        Assert.Throws<VegaLiteSpecException>(() => ChartTestHelpers.Axis(Linear, "x", 300, 200, """{ "axis": { "labelExpr": "datum.label +" } }"""));
    }
}
