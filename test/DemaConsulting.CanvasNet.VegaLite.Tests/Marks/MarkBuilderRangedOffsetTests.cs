using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Tests of the ranged offsets of <see cref="DemaConsulting.CanvasNet.VegaLite.Marks.MarkBuilder"/> (Phase 14): a bar or area whose discrete position has an unbinned quantitative offset spans, inside the band of the position, from the zero of
///     the offset scale to the offset value (Vega-Lite 6.5.0 <c>isRangedOffset</c>, <c>rangePosition</c> and <c>area.ts</c>). The expected numbers are those of Vega 6.4 with Vega-Lite 6.5.0 for the same specifications (the paths of
///     <c>vega.View.toSVG</c>), read once and written out here.
/// </summary>
public class MarkBuilderRangedOffsetTests
{
    /// <summary>Three quarters of one team, one of them below zero; a data rectangle of 150 by 100.</summary>
    private const string Quarters = """
        "data": { "values": [ { "q": "Q1", "t": "A", "s": 12 }, { "q": "Q2", "t": "A", "s": -9 }, { "q": "Q3", "t": "A", "s": 4 } ] }, "width": 150, "height": 100
        """;

    /// <summary>Two teams over three quarters, with a quantitative offset; a data rectangle of 150 by 100.</summary>
    private const string Teams = """
        "data": { "values": [ { "q": "Q1", "t": "A", "s": 12 }, { "q": "Q2", "t": "A", "s": -9 }, { "q": "Q3", "t": "A", "s": 4 },
                              { "q": "Q1", "t": "B", "s": 3 }, { "q": "Q2", "t": "B", "s": 7 }, { "q": "Q3", "t": "B", "s": 8 } ] }, "width": 150, "height": 100
        """;

    /// <summary>
    ///     Gets the bars of a specification as rounded rectangles.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The x, y, width and height of each bar in drawing order.</returns>
    private static List<(double X, double Y, double Width, double Height)> Bars(string json) =>
        [.. ChartTestHelpers.Of<SceneRect>(ChartTestHelpers.Marks(json, 150, 100), "mark-bar").Select(r => (Math.Round(r.X, 9), Math.Round(r.Y, 9), Math.Round(r.Width, 9), Math.Round(r.Height, 9)))];

    /// <summary>
    ///     Validates the bars of a quantitative <c>yOffset</c> on a nominal y: the bar of a quarter has the width of its band and spans, vertically, from the zero of the offset scale (the baseline, the bottom of the band at the
    ///     bandwidth of 66.67 below the start of the band at 16.67 when the domain starts at zero... here the domain is -9 to 12) to the value; a negative value is drawn below the baseline.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_YOffsetBarsSpanFromTheBaselineToTheOffsetValue()
    {
        // Arrange: Vega's output for the same chart: the bars at x = 2.5, 52.5 and 102.5 (the band of x is 50 wide with the padding 0.1 and 0.05), the band of y starts at 16.667 and is 66.667 high, the offset scale maps -9..12 to
        // 66.667..0, so zero is at 38.095 inside the band: y = 16.667 + 0 (12), y2 = 16.667 + 38.095 (the baseline); for -9 the bar runs from the baseline to 16.667 + 66.667; for 4 from 16.667 + 21.905 to the baseline
        var json = "{ " + Quarters + ", \"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"ordinal\" }, \"y\": { \"field\": \"t\", \"type\": \"nominal\" }, \"yOffset\": { \"field\": \"s\", \"type\": \"quantitative\" } } }";

        // Act
        var bars = Bars(json);

        // Assert
        Assert.Equal(
            [
                (2.5, 16.666666667, 45.0, 38.095238095),
                (52.5, 54.761904762, 45.0, 28.571428571),
                (102.5, 42.063492063, 45.0, 12.698412698),
            ],
            bars);
    }

    /// <summary>
    ///     Validates the bars of a quantitative <c>xOffset</c> on a nominal x (the bars run horizontally within the bands of x, the offset scale runs from the start of the band to its end), from Vega's output.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_XOffsetBarsSpanFromTheBaselineToTheOffsetValue()
    {
        // Arrange
        var json = "{ " + Teams + ", \"mark\": \"bar\", \"encoding\": { \"y\": { \"field\": \"q\", \"type\": \"ordinal\" }, \"x\": { \"field\": \"t\", \"type\": \"nominal\" }, \"xOffset\": { \"field\": \"s\", \"type\": \"quantitative\" } } }";

        // Act
        var bars = Bars(json);

        // Assert: the bars of the team A start at the baseline (37.013) and run to the value, a negative one to the left; those of B start at 105.195
        Assert.Equal(
            [
                (37.012987013, 1.666666667, 31.168831169, 30.0),
                (13.636363636, 35.0, 23.376623377, 30.0),
                (37.012987013, 68.333333333, 10.38961039, 30.0),
                (105.194805195, 1.666666667, 7.792207792, 30.0),
                (105.194805195, 35.0, 18.181818182, 30.0),
                (105.194805195, 68.333333333, 20.779220779, 30.0),
            ],
            bars);
    }

    /// <summary>
    ///     Validates the ranged area of a quantitative <c>yOffset</c>: the vertices run along the points of x, the top edge follows the offset values and the baseline is the zero of the offset scale inside the band, as in Vega.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_YOffsetAreaSpansFromTheBaselineToTheOffsetValue()
    {
        // Arrange
        var json = "{ " + Quarters + ", \"mark\": \"area\", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"ordinal\" }, \"y\": { \"field\": \"t\", \"type\": \"nominal\" }, \"yOffset\": { \"field\": \"s\", \"type\": \"quantitative\" } } }";

        // Act
        var area = Assert.Single(ChartTestHelpers.Of<ScenePath>(ChartTestHelpers.Marks(json, 150, 100), "mark-area"));

        // Assert: Vega: M25,16.667L75,83.333L125,42.063L125,54.762L75,54.762L25,54.762Z
        Assert.Equal("M25,16.666667 L75,83.333333 L125,42.063492 L125,54.761905 L75,54.761905 L25,54.761905 Z", PathText.Of(area));
    }

    /// <summary>
    ///     Validates that a point mark with the same encoding is not ranged: it sits at the offset value inside the band (the center of the offset band), so the ranged rules apply to bars and areas only.
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_PointMarksAreNotRanged()
    {
        // Arrange
        var json = "{ " + Quarters + ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"ordinal\" }, \"y\": { \"field\": \"t\", \"type\": \"nominal\" }, \"yOffset\": { \"field\": \"s\", \"type\": \"quantitative\" } } }";

        // Act
        var symbols = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(json, 150, 100), "mark-point");

        // Assert: three symbols, one per row; the offset scale of a point mark has no zero (its domain is -9 to 12 only because of the data), so the symbols sit at the centers of the offset values inside the band
        Assert.Equal(3, symbols.Count);
        Assert.Equal(3, symbols.Select(s => s.Y).Distinct().Count());
        Assert.All(symbols, s => Assert.InRange(s.Y, 16.666666667 - 1e-6, 83.333333334 + 1e-6));
    }

    /// <summary>
    ///     Validates the ranged offset when the bar has a secondary channel (<c>y2</c>): Vega-Lite does not treat it as ranged, so the offset is not drawn (and is reported).
    /// </summary>
    [Fact]
    public void MarkBuilder_Build_ASecondaryChannelIsNotARangedOffset()
    {
        // Arrange
        var json = "{ " + Quarters + ", \"mark\": \"bar\", \"encoding\": { \"x\": { \"field\": \"q\", \"type\": \"ordinal\" }, \"y\": { \"field\": \"t\", \"type\": \"nominal\" }, \"y2\": { \"field\": \"s\" }, \"yOffset\": { \"field\": \"s\", \"type\": \"quantitative\" } } }";

        // Act
        var decision = ScaleResolver.DecideOffset("yOffset", ChartTestHelpers.Parts(json).Tree.Root["encoding"], "bar");

        // Assert
        Assert.Equal(OffsetDecision.UnsupportedContinuous, decision);
    }
}
