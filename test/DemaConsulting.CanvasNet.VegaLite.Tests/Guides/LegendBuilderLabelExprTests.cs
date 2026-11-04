using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the <c>labelExpr</c> of <see cref="LegendBuilder"/>: the labels of the entries are replaced before they are measured, with the datum Vega gives a symbol or a gradient legend.
///     Vega-Lite 6 has no <c>config.legend.labelExpr</c>, so the configuration is not read.
/// </summary>
public class LegendBuilderLabelExprTests
{
    /// <summary>
    ///     Builds a point chart whose color is a nominal field with three values and the given legend.
    /// </summary>
    /// <param name="legend">The legend JSON.</param>
    /// <returns>The specification text.</returns>
    private static string Symbols(string legend) => """
        { "data": { "values": [ { "g": "a", "x": 1 }, { "g": "b", "x": 2 }, { "g": "c", "x": 3 } ] }, "mark": "point",
          "encoding": { "x": { "field": "x", "type": "quantitative" }, "color": { "field": "g", "type": "nominal", "legend": 
        """ + legend + " } } }";

    /// <summary>
    ///     Gets the labels of a legend.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The label items in drawing order.</returns>
    private static List<SceneText> Labels(LegendResult legend) =>
        [.. legend.Children.OfType<SceneGroup>().SelectMany(g => g.Children).OfType<SceneText>().Where(t => t.Role == "legend-label")];

    /// <summary>
    ///     Validates the datum of a symbol legend: <c>index</c>, <c>value</c> and <c>label</c>.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LabelExprSymbolLegendGivesIndexValueAndLabel()
    {
        // Act
        var legend = ChartTestHelpers.Legend(Symbols("{ \"labelExpr\": \"datum.index + ':' + upper(datum.value) + ':' + datum.label\" }"));

        // Assert
        Assert.Equal(["0:A:a", "1:B:b", "2:C:c"], Labels(legend).Select(l => l.Text));
    }

    /// <summary>
    ///     Validates that the entry that stands for the entries beyond <c>symbolLimit</c> is replaced too, with the index after the last shown entry.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LabelExprAppliesToTheEllipsisEntry()
    {
        // Act: a limit of two shows one entry and the entry for the other two
        var legend = ChartTestHelpers.Legend(Symbols("{ \"symbolLimit\": 2, \"labelExpr\": \"datum.index + '/' + datum.label\" }"));

        // Assert
        Assert.Equal(["0/a", "1/\u20262 entries"], Labels(legend).Select(l => l.Text));
    }

    /// <summary>
    ///     Validates that blank labels change the width of the legend: the replacement is made before the entries are measured.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LabelExprBlankLabelsChangeTheMeasuredWidth()
    {
        // Act
        var plain = ChartTestHelpers.Legend(Symbols("{}"));
        var longer = ChartTestHelpers.Legend(Symbols("{ \"labelExpr\": \"datum.label + ' with a long text'\" }"));
        var blank = ChartTestHelpers.Legend(Symbols("{ \"labelExpr\": \"''\" }"));

        // Assert
        Assert.True(longer.Width > plain.Width + 40, "the longer labels widen the legend");
        Assert.True(blank.Width < plain.Width, "blank labels narrow it");
    }

    /// <summary>
    ///     Validates that the explicit expression of the legend is used and a <c>labelExpr</c> of <c>config.legend</c> is not read (Vega-Lite 6 does not).
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LabelExprConfigIsIgnoredAndExplicitWins()
    {
        // Arrange
        const string config = """{ "legend": { "labelExpr": "'config'" } }""";

        // Act
        var ignored = ChartTestHelpers.Legend(Symbols("{}"), callerConfig: config);
        var explicitOne = ChartTestHelpers.Legend(Symbols("{ \"labelExpr\": \"datum.label + '!'\" }"), callerConfig: config);

        // Assert
        Assert.Equal(["a", "b", "c"], Labels(ignored).Select(l => l.Text));
        Assert.Equal(["a!", "b!", "c!"], Labels(explicitOne).Select(l => l.Text));
    }

    /// <summary>
    ///     Validates the gradient legend: <c>perc</c> is the position of the tick along the bar and <c>value</c> the tick.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LabelExprGradientLegendGivesPerc()
    {
        // Arrange: a quantitative color on 0 to 100 has a gradient with the labels 0, 50, 100 for a short bar
        const string spec = """
            { "data": { "values": [ { "x": 1, "c": 0 }, { "x": 2, "c": 100 } ] }, "mark": "point",
              "encoding": { "x": { "field": "x", "type": "quantitative" },
                            "color": { "field": "c", "type": "quantitative", "legend": { "values": [0, 25, 100], "labelExpr": "round(datum.perc * 100) + '% of ' + datum.value + ' #' + datum.index" } } } }
            """;

        // Act
        var legend = ChartTestHelpers.Legend(spec);

        // Assert
        Assert.Equal(["0% of 0 #0", "25% of 25 #1", "100% of 100 #2"], Labels(legend).Select(l => l.Text));
    }

    /// <summary>
    ///     Validates the discrete gradient legend (a quantize scale): <c>perc</c> and <c>perc2</c> are the ends of the class.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LabelExprDiscreteGradientGivesPercAndPerc2()
    {
        // Arrange
        const string spec = """
            { "data": { "values": [ { "x": 1, "c": 0 }, { "x": 2, "c": 100 } ] }, "mark": "point",
              "encoding": { "x": { "field": "x", "type": "quantitative" },
                            "color": { "field": "c", "type": "quantitative", "scale": { "type": "quantize", "nice": false }, "legend": { "labelExpr": "round(datum.perc * 100) + '-' + round(datum.perc2 * 100)" } } } }
            """;

        // Act
        var legend = ChartTestHelpers.Legend(spec);

        // Assert: four classes of 20 are cut at 20, 40, 60 and 80 (five classes of the range 0 to 100)
        Assert.Equal(["20-40", "40-60", "60-80", "80-100"], Labels(legend).Select(l => l.Text));
    }

    /// <summary>
    ///     Validates that the legend of a temporal color gives a date as <c>datum.value</c>.
    /// </summary>
    [Fact]
    public void LegendBuilder_Build_LabelExprTemporalLegendGivesDate()
    {
        // Arrange
        const string spec = """
            { "data": { "values": [ { "x": 1, "d": "2024-01-01" }, { "x": 2, "d": "2025-01-01" } ] }, "mark": "point",
              "encoding": { "x": { "field": "x", "type": "quantitative" },
                            "color": { "field": "d", "type": "temporal", "legend": { "values": ["2024-01-01"], "labelExpr": "'in ' + year(datum.value)" } } } }
            """;

        // Act
        var legend = ChartTestHelpers.Legend(spec);

        // Assert
        Assert.Equal(["in 2024"], Labels(legend).Select(l => l.Text));
    }
}
