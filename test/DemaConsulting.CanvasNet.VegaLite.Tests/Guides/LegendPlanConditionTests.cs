using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the scales and legends of conditional encodings: a condition that holds a field gives the channel its scale and legend,
///     a condition that holds only a value creates none by itself, and the legend of another channel shows the condition value that the
///     mark's paint would use (Vega-Lite's <c>getFirstConditionValue</c>).
/// </summary>
public class LegendPlanConditionTests
{
    /// <summary>Three rows with the categories <c>a</c>, <c>b</c> and <c>c</c> in <c>g</c> and the numbers <c>q</c> (1, 2, 3) and <c>r</c> (10, 20, 30).</summary>
    private const string Rows = "[ { \"g\": \"a\", \"q\": 1, \"r\": 10 }, { \"g\": \"b\", \"q\": 2, \"r\": 20 }, { \"g\": \"c\", \"q\": 3, \"r\": 30 } ]";

    /// <summary>
    ///     Builds a specification of a circle mark with the encoding channels after x and y.
    /// </summary>
    /// <param name="channels">The JSON text of the channels (without the enclosing braces).</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string channels) =>
        "{ \"data\": { \"values\": " + Rows + " }, \"mark\": \"circle\", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"quantitative\" }, \"y\": { \"field\": \"r\", \"type\": \"quantitative\" }, " + channels + " } }";

    /// <summary>
    ///     Gets the symbols of a legend.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The symbols in drawing order.</returns>
    private static List<SceneSymbol> Symbols(LegendResult legend) =>
        [.. Assert.IsType<SceneGroup>(legend.Children[0]).Children.OfType<SceneSymbol>()];

    /// <summary>
    ///     Validates that conditions that hold only values create no scale and no legend.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_ValueConditionsCreateNoScaleAndNoLegend()
    {
        // Arrange
        var json = Spec(
            """
            "color": { "condition": { "test": "datum.q > 1", "value": "red" }, "value": "gray" },
            "opacity": { "condition": { "test": "datum.q > 1", "value": 1 } },
            "size": { "condition": { "test": "datum.q > 1", "value": 80 } }
            """);

        // Act
        var parts = ChartTestHelpers.Parts(json);

        // Assert
        Assert.Null(parts.Scales["color"]);
        Assert.Null(parts.Scales["opacity"]);
        Assert.Null(parts.Scales["size"]);
        Assert.Empty(ChartTestHelpers.Legends(json));
    }

    /// <summary>
    ///     Validates that a condition that holds a field gives the channel an ordinal scale over that field, and a legend with the field as the title.
    /// </summary>
    [Fact]
    public void LegendPlan_Create_FieldConditionCreatesTheScaleAndTheLegend()
    {
        // Arrange
        var json = Spec("""
            "color": { "condition": { "test": "datum.q > 1", "field": "g", "type": "nominal" }, "value": "gray" }
            """);

        // Act
        var scale = ChartTestHelpers.Parts(json).Scales["color"];
        var legends = ChartTestHelpers.Legends(json);
        var legend = ChartTestHelpers.Legend(json);

        // Assert: the scale reads the field of the condition; the legend is titled by it and shows one symbol per category
        Assert.NotNull(scale);
        Assert.Equal("g", scale.FieldPath);
        Assert.Equal(["a", "b", "c"], scale.DiscreteDomain.Select(d => d.Text));
        Assert.Single(legends);
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], Symbols(legend).Select(s => s.Fill));
    }

    /// <summary>
    ///     Validates that a field with value conditions keeps its scale and legend (the conditions override the mapped value in the marks only).
    /// </summary>
    [Fact]
    public void LegendPlan_Create_FieldWithValueConditionKeepsItsLegend()
    {
        // Arrange
        var json = Spec("""
            "color": { "field": "g", "type": "nominal", "condition": { "test": "datum.q > 2", "value": "red" } }
            """);

        // Act
        var legend = ChartTestHelpers.Legend(json);

        // Assert: the legend shows the scale, not the conditional value
        Assert.Equal(["#4c78a8", "#f58518", "#e45756"], Symbols(legend).Select(s => s.Fill));
    }

    /// <summary>
    ///     Validates the paint that the symbols of a size legend take from a color that no field drives: the value of the first condition,
    ///     else the value of the channel.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_SizeLegendShowsTheFirstConditionValueOfAFixedColor()
    {
        // Arrange
        var size = "\"size\": { \"field\": \"q\", \"type\": \"quantitative\" }";
        var conditional = Spec(size + ", \"color\": { \"condition\": [ { \"test\": \"datum.q > 1\", \"value\": \"red\" }, { \"test\": \"datum.q > 2\", \"value\": \"blue\" } ], \"value\": \"gray\" }");
        var plain = Spec(size + ", \"color\": { \"value\": \"gray\" }");

        // Act
        var withConditions = Symbols(ChartTestHelpers.Legend(conditional));
        var withoutConditions = Symbols(ChartTestHelpers.Legend(plain));

        // Assert: the first condition value is shown; without conditions the channel's own value is
        Assert.All(withConditions, s => Assert.Equal("red", s.Fill));
        Assert.All(withoutConditions, s => Assert.Equal("gray", s.Fill));
    }

    /// <summary>
    ///     Validates that the symbols of a legend show the largest opacity of the base value and the condition values.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_OpacityOfConditionalChannelIsTheLargestValue()
    {
        // Arrange
        var size = "\"size\": { \"field\": \"q\", \"type\": \"quantitative\" }";
        var raised = Spec(size + ", \"opacity\": { \"condition\": { \"test\": \"datum.q > 1\", \"value\": 0.9 }, \"value\": 0.2 }");
        var lowered = Spec(size + ", \"opacity\": { \"condition\": { \"test\": \"datum.q > 1\", \"value\": 0.1 }, \"value\": 0.4 }");

        // Act
        var high = Symbols(ChartTestHelpers.Legend(raised));
        var low = Symbols(ChartTestHelpers.Legend(lowered));

        // Assert
        Assert.All(high, s => Assert.Equal(0.9, s.Opacity));
        Assert.All(low, s => Assert.Equal(0.4, s.Opacity));
    }

    /// <summary>
    ///     Validates that building the same conditional legend twice gives the same symbols.
    /// </summary>
    [Fact]
    public void LegendBuilder_Symbol_ConditionalLegendIsDeterministic()
    {
        // Arrange
        var json = Spec("\"color\": { \"condition\": { \"test\": \"datum.q > 1\", \"field\": \"g\", \"type\": \"nominal\" }, \"value\": \"gray\" }");

        // Act
        var first = Symbols(ChartTestHelpers.Legend(json));
        var second = Symbols(ChartTestHelpers.Legend(json));

        // Assert
        Assert.Equal(first, second);
    }
}
