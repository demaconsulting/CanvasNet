using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the <c>show</c> invalid mode of <see cref="DemaConsulting.CanvasNet.VegaLite.Marks.MarkBuilder"/> (Phase 13), which Vega-Lite's <c>compile/mark/encode/invalid.ts</c>,
///     <c>compile/invalid/ScaleInvalidDataMode.ts</c> and <c>scaledZeroOrMinOrMax.ts</c> define: a row whose value on a continuous channel is not a number gets the output that the scale gives to zero
///     (when the domain has it) or to the first value of the domain, for position, size, opacity and color channels alike. Each expectation compares the invalid row with the row that holds that
///     value, so it does not depend on the sizes and colors of the scales.
/// </summary>
public class MarkBuilderInvalidShowTests
{
    /// <summary>
    ///     Builds a single view specification.
    /// </summary>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="encoding">The JSON text of the encoding (without the braces).</param>
    /// <param name="rows">The JSON text of the rows.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string mark, string encoding, string rows) =>
        "{ \"data\": { \"values\": " + rows + " }, \"mark\": " + mark + ", \"encoding\": { " + encoding + " } }";

    /// <summary>
    ///     Gets the symbols of a point chart.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <returns>The symbols in row order.</returns>
    private static List<SceneSymbol> Points(string json) => ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(json, 200, 100), "mark-point");

    /// <summary>
    ///     Validates the size channel: a null size is the size of the value zero (the domain of a size scale has zero), and the default mode drops the row.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_InvalidShowGivesANullSizeTheSizeOfZero()
    {
        // Arrange: the sizes of the values 0, 10, null and 20 on the domain 0 to 20
        const string encoding = """ "x": { "field": "x", "type": "quantitative" }, "size": { "field": "v", "type": "quantitative", "legend": null } """;
        const string rows = """[ { "x": 0, "v": 0 }, { "x": 1, "v": 10 }, { "x": 2, "v": null }, { "x": 3, "v": 20 } ]""";

        // Act
        var shown = Points(Chart("""{ "type": "point", "invalid": "show" }""", encoding, rows));
        var dropped = Points(Chart("\"point\"", encoding, rows));

        // Assert: four symbols; the null one has the size of the zero one
        Assert.Equal(4, shown.Count);
        Assert.Equal(shown[0].Size, shown[2].Size);
        Assert.NotEqual(shown[1].Size, shown[2].Size);
        Assert.Equal(3, dropped.Count);
    }

    /// <summary>
    ///     Validates the opacity channel the same way, and that the invalid row of a color channel gets the color of the first value of the domain (a color scale does not include zero by
    ///     default): the lowest value of the data.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_InvalidShowGivesANullColorTheColorOfTheFirstDomainValue()
    {
        // Arrange: the colors of 5, 10, null and 15: the domain of the color scale is 5 to 15
        const string encoding = """ "x": { "field": "x", "type": "quantitative" }, "color": { "field": "v", "type": "quantitative", "legend": null } """;
        const string rows = """[ { "x": 0, "v": 5 }, { "x": 1, "v": 10 }, { "x": 2, "v": null }, { "x": 3, "v": 15 } ]""";
        const string opacity = """ "x": { "field": "x", "type": "quantitative" }, "opacity": { "field": "v", "type": "quantitative", "legend": null } """;
        const string opacityRows = """[ { "x": 0, "v": 0 }, { "x": 1, "v": 10 }, { "x": 2, "v": null } ]""";

        // Act
        var color = Points(Chart("""{ "type": "point", "filled": true, "invalid": "show" }""", encoding, rows));
        var faded = Points(Chart("""{ "type": "point", "invalid": "show" }""", opacity, opacityRows));

        // Assert: the null row has the fill of the row with the lowest value
        Assert.Equal(4, color.Count);
        Assert.Equal(color[0].Fill, color[2].Fill);
        Assert.NotEqual(color[1].Fill, color[2].Fill);

        // Assert: the opacity of the null row is that of the value zero, which is the first value of the domain too
        Assert.Equal(faded[0].Opacity, faded[2].Opacity);
        Assert.NotEqual(faded[1].Opacity, faded[2].Opacity);
    }

    /// <summary>
    ///     Validates a bar: the invalid value is a bar of no height on the zero line, and the x position of an invalid row (a null on a quantitative x) is at the position of zero.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_InvalidShowDrawsABarOfNoHeightAtZero()
    {
        // Arrange: y is 1, null and 3 on the domain 0 to 3 over 150 pixels (50 per unit)
        const string rows = """[ { "k": "a", "v": 1 }, { "k": "b", "v": null }, { "k": "c", "v": 3 } ]""";
        const string encoding = """ "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } """;

        // Act
        var shown = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Chart("""{ "type": "bar", "invalid": "show" }""", encoding, rows), 300, 150), "mark-bar");
        var dropped = ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(Chart("\"bar\"", encoding, rows), 300, 150), "mark-bar");

        // Assert: the null bar has no height at the zero line (y = 150); the others are as usual
        Assert.Equal(3, shown.Count);
        Assert.Equal(150.0, shown[1].Y + shown[1].Height, 6);
        Assert.Equal(0.0, shown[1].Height, 6);
        Assert.Equal(100.0, shown[0].Y, 6);
        Assert.Equal(0.0, shown[2].Y, 6);
        Assert.Equal(2, dropped.Count);
    }

    /// <summary>
    ///     Validates the scales that never have zero: a log scale and a time scale put the invalid row at the first value of the domain (<c>domainHasZero</c> is <c>definitely-not</c>),
    ///     a linear scale whose explicit domain does not hold zero too.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_InvalidShowUsesTheFirstDomainValueOnLogAndTimeScales()
    {
        // Arrange: a log y scale over 1 to 100 (100 pixels: 1 at 100, 10 at 50, 100 at 0), and a time x scale
        const string logRows = """[ { "x": 0, "v": 1 }, { "x": 1, "v": null }, { "x": 2, "v": 100 } ]""";
        const string logEncoding = """ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "v", "type": "quantitative", "scale": { "type": "log" } } """;
        const string timeRows = """[ { "d": 1704067200000, "v": 1 }, { "d": null, "v": 2 }, { "d": 1706745600000, "v": 3 } ]""";
        const string timeEncoding = """ "x": { "field": "d", "type": "temporal" }, "y": { "field": "v", "type": "quantitative" } """;

        // Act
        var log = Points(Chart("""{ "type": "point", "invalid": "show" }""", logEncoding, logRows));
        var time = Points(Chart("""{ "type": "point", "invalid": "show" }""", timeEncoding, timeRows));

        // Assert: the log scale puts the null at the position of 1 (the bottom, 100); the time scale at the first date (x = 0)
        Assert.Equal(100.0, log[1].Y, 6);
        Assert.Equal(3, time.Count);
        Assert.Equal(time[0].X, time[1].X, 6);
    }

    /// <summary>
    ///     Validates that the modes that filter and break paths still drop the row: <c>filter</c> and the default modes are not changed by the <c>show</c> implementation, and that a row that has
    ///     a valid number is not touched by it (the same positions as in the default mode).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_OtherInvalidModesStillDropTheRowAndValidRowsAreUntouched()
    {
        // Arrange
        const string rows = """[ { "x": 0, "y": 1 }, { "x": 1, "y": null }, { "x": 2, "y": 3 } ]""";
        const string encoding = """ "x": { "field": "x", "type": "quantitative" }, "y": { "field": "y", "type": "quantitative" } """;

        // Act
        var filtered = Points(Chart("""{ "type": "point", "invalid": "filter" }""", encoding, rows));
        var shown = Points(Chart("""{ "type": "point", "invalid": "show" }""", encoding, rows));
        var plain = Points(Chart("\"point\"", encoding, rows));

        // Assert
        Assert.Equal(2, filtered.Count);
        Assert.Equal(3, shown.Count);
        Assert.Equal(plain.Select(p => (p.X, p.Y)), [(shown[0].X, shown[0].Y), (shown[2].X, shown[2].Y)]);
    }
}
