using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Tests of the axis of a position that holds a ranged offset (Phase 14): Vega-Lite 6.5.0's <c>defaultBandPosition</c> (0 for x and 1 for y) moves the ticks and the grid lines to the baseline of the offset scale, and
///     <c>labelsPositionSpec</c> moves the labels there too (Vega puts the labels of a band scale in the middle whatever the band position). The expected numbers are those of Vega 6.4 with Vega-Lite 6.5.0 for the same
///     specifications (the SVG of <c>vega.View.toSVG</c>), read once and written out here.
/// </summary>
public class AxisBuilderRangedOffsetTests
{
    /// <summary>A bar chart whose x is the quarter and whose y is the team, with a quantitative <c>yOffset</c>; a data rectangle of 150 by 100.</summary>
    private const string YOffset = """
        { "data": { "values": [ { "q": "Q1", "t": "A", "s": 12 }, { "q": "Q2", "t": "A", "s": -9 }, { "q": "Q3", "t": "A", "s": 4 } ] }, "width": 150, "height": 100,
          "mark": "bar",
          "encoding": { "x": { "field": "q", "type": "ordinal" }, "y": { "field": "t", "type": "nominal", "AXIS": 0 }, "yOffset": { "field": "s", "type": "quantitative" } } }
        """;

    /// <summary>A bar chart whose y is the quarter and whose x is the team, with a quantitative <c>xOffset</c>; a data rectangle of 150 by 100.</summary>
    private const string XOffset = """
        { "data": { "values": [ { "q": "Q1", "t": "A", "s": 12 }, { "q": "Q2", "t": "A", "s": -9 }, { "q": "Q3", "t": "A", "s": 4 },
                                { "q": "Q1", "t": "B", "s": 3 }, { "q": "Q2", "t": "B", "s": 7 }, { "q": "Q3", "t": "B", "s": 8 } ] }, "width": 150, "height": 100,
          "mark": "bar",
          "encoding": { "y": { "field": "q", "type": "ordinal" }, "x": { "field": "t", "type": "nominal", "AXIS": 0 }, "xOffset": { "field": "s", "type": "quantitative" } } }
        """;

    /// <summary>
    ///     Builds an axis, replacing the placeholder <c>"AXIS": 0</c> by the axis properties.
    /// </summary>
    /// <param name="template">The specification with the placeholder.</param>
    /// <param name="channel">The channel of the axis.</param>
    /// <param name="axis">The properties of the axis object, with the braces (or <c>null</c>); an empty text for no axis property.</param>
    /// <returns>The axis.</returns>
    private static AxisResult Build(string template, string channel, string axis) =>
        ChartTestHelpers.Axis(template.Replace("\"AXIS\": 0", axis.Length == 0 ? "\"axis\": {}" : "\"axis\": " + axis, StringComparison.Ordinal), channel, 150, 100) ?? throw new InvalidOperationException("no axis");

    /// <summary>
    ///     Gets the labels of an axis.
    /// </summary>
    /// <param name="axis">The axis.</param>
    /// <returns>The label items in order.</returns>
    private static List<SceneText> Labels(AxisResult axis) => [.. axis.Group.Children.OfType<SceneText>().Where(t => t.Role == "axis-label")];

    /// <summary>
    ///     Validates the y axis of a ranged <c>yOffset</c>: one tick and label at the end of the band (the baseline of the offset scale, 83.33; Vega's tick at 83 with the half-pixel translate of the axis, the label at 83.33 with the
    ///     baseline in the middle), where the band of the nominal y starts at 16.67 and is 66.67 high.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_RangedYOffsetPutsTicksAndLabelsAtTheBandEnd()
    {
        // Arrange / Act
        var axis = Build(YOffset, "y", string.Empty);
        var label = Assert.Single(Labels(axis));

        // Assert
        Assert.Equal("A", label.Text);
        Assert.Equal(83.333333333, label.Y, 9);
        Assert.Equal([83.0], axis.Ticks.Select(t => t.Position));
    }

    /// <summary>
    ///     Validates the x axis of a ranged <c>xOffset</c>: the ticks and labels at the start of the bands (13.64 and 81.82), which Vega draws at 13 and 81 (round of the position less the half-pixel translate).
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_RangedXOffsetPutsTicksAndLabelsAtTheBandStart()
    {
        // Arrange / Act
        var axis = Build(XOffset, "x", string.Empty);
        var labels = Labels(axis);

        // Assert
        Assert.Equal(["A", "B"], labels.Select(l => l.Text));
        Assert.Equal([13.636363636, 81.818181818], labels.Select(l => Math.Round(l.X, 9)));
        Assert.Equal([13.0, 81.0], axis.Ticks.Select(t => t.Position));
    }

    /// <summary>
    ///     Validates that an explicit <c>bandPosition</c> of the axis wins (Vega-Lite: <c>axis.bandPosition ?? defaultBandPosition</c>), for the ticks and for the labels (<c>labelsPositionSpec</c>), and that 0.5 leaves the labels in
    ///     the middle of the band.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_ExplicitBandPositionWinsOverTheRangedDefault()
    {
        // Arrange / Act
        var middle = Build(YOffset, "y", """{ "bandPosition": 0.5 }""");
        var start = Build(YOffset, "y", """{ "bandPosition": 0 }""");

        // Assert: the middle of the band is 16.67 + 33.33 = 50 (the labels of the default position are 0.5 before it: the tick offset of a band axis), the start 16.67 (the override has no offset)
        Assert.Equal(49.5, Assert.Single(Labels(middle)).Y, 9);
        Assert.Equal(16.666666667, Assert.Single(Labels(start)).Y, 9);
        Assert.Equal([50.0], middle.Ticks.Select(t => t.Position));
    }

    /// <summary>
    ///     Validates that the axis of the other position (the one without the ranged offset) and the axis of a position with an ordinary offset are not moved.
    /// </summary>
    [Fact]
    public void AxisBuilder_Build_OtherAxesStayInTheMiddleOfTheBand()
    {
        // Arrange
        const string grouped = """
            { "data": { "values": [ { "c": "A", "g": "x", "v": 1 }, { "c": "A", "g": "y", "v": 2 } ] }, "width": 150, "height": 100, "mark": "bar",
              "encoding": { "x": { "field": "c", "type": "nominal" }, "xOffset": { "field": "g", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } }
            """;

        // Act: the x axis of the ranged chart is the quarters (the bars of the quarters span their whole bands), and the grouped chart is not ranged
        var quarters = ChartTestHelpers.Axis(YOffset.Replace("\"AXIS\": 0", "\"axis\": {}", StringComparison.Ordinal), "x", 150, 100)!;
        var plain = ChartTestHelpers.Axis(grouped, "x", 150, 100)!;

        // Assert: the labels are in the middle of the bands (Vega: 25, 75 and 125 for the three quarters of 50; the labels of the band are not shifted by the half-pixel translate of the axis)
        Assert.Equal([24.5, 74.5, 124.5], Labels(quarters).Select(l => Math.Round(l.X, 9)));
        Assert.Equal(74.5, Assert.Single(Labels(plain)).X, 9);
    }
}
