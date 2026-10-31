using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Marks;

/// <summary>
///     Unit tests of the geometry of marks over time units in <see cref="DemaConsulting.CanvasNet.VegaLite.Marks.MarkBuilder"/>: a bar or rect spans its unit (Vega-Lite's
///     <c>rectBinPosition</c>: <c>binSpacing</c> and <c>minBandSize</c>, the half pixel of the axis translate, <c>timeUnitBandSize</c>), a tick sits in the middle of its unit, a
///     point at its start (or where <c>bandPosition</c> puts it), and the bands of an ordinal unit are those of a band scale. Every number is derived by hand from the days of
///     the months of 2024: the three months January to March are 31, 29 and 31 days, 91 days over 300 pixels.
/// </summary>
public class MarkBuilderTimeTests
{
    /// <summary>Three bars over January, February and March of 2024 (one row each).</summary>
    private const string MonthBars = """
        { "data": { "values": [ { "date": "2024-01-15" }, { "date": "2024-02-10" }, { "date": "2024-03-20" } ] },
          "mark": MARK,
          "encoding": {
            "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth"EXTRA_X },
            "y": { "aggregate": "count" }
          } }
        """;

    /// <summary>The pixels of a day on the 300 pixels of the 91 days from January 1 to April 1.</summary>
    private const double PerDay = 300.0 / 91;

    /// <summary>
    ///     Builds the month bar specification.
    /// </summary>
    /// <param name="mark">The mark: a name in quotes or an object.</param>
    /// <param name="xExtra">Members of the x definition, each starting with a comma, or an empty text.</param>
    /// <returns>The specification text.</returns>
    private static string Bars(string mark = "\"bar\"", string xExtra = "") =>
        MonthBars.Replace("MARK", mark, StringComparison.Ordinal).Replace("EXTRA_X", xExtra, StringComparison.Ordinal);

    /// <summary>
    ///     Gets the rectangles of a mark role in drawing order.
    /// </summary>
    /// <param name="group">The mark group.</param>
    /// <param name="role">The role.</param>
    /// <returns>The rectangles.</returns>
    private static List<SceneRect> Rects(SceneGroup group, string role) => ChartTestHelpers.Of<SceneRect>(group, role);

    /// <summary>
    ///     Asserts the left edge and the width of a rectangle.
    /// </summary>
    /// <param name="x">The expected left edge.</param>
    /// <param name="width">The expected width.</param>
    /// <param name="rect">The rectangle.</param>
    private static void AssertSpan(double x, double width, SceneRect rect)
    {
        Assert.Equal(x, rect.X, 6);
        Assert.Equal(width, rect.Width, 6);
    }

    /// <summary>
    ///     Validates that the bars of a <c>yearmonth</c> field span their months: each edge is half the bin spacing (1 pixel of a bar) inside the month, and the whole bar is shifted
    ///     by the half pixel of the axis translate. January 1, February 1, March 1 and April 1 are at 0, 31, 60 and 91 days.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_YearMonthBars_SpanTheirMonths()
    {
        // Arrange: January spans 0 to 31 days, February 31 to 60, March 60 to 91; left edge = start + 0.5 (spacing / 2) + 0.5 (translate), width = days * 300 / 91 - 1

        // Act
        var bars = Rects(ChartTestHelpers.Marks(Bars(), 300, 200), "mark-bar");

        // Assert
        Assert.Equal(3, bars.Count);
        AssertSpan(1, (31 * PerDay) - 1, bars[0]);
        AssertSpan((31 * PerDay) + 1, (29 * PerDay) - 1, bars[1]);
        AssertSpan((60 * PerDay) + 1, (31 * PerDay) - 1, bars[2]);
    }

    /// <summary>
    ///     Validates that a rect spans its unit without spacing (<c>binSpacing</c> is 0 for a rect), shifted by the half pixel of the axis translate.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_RectSpansItsUnitWithoutSpacing()
    {
        // Arrange: a heat map of the months (x) by a field (y)
        const string Spec = """
            { "data": { "values": [ { "date": "2024-01-15", "k": "a" }, { "date": "2024-02-10", "k": "a" }, { "date": "2024-03-20", "k": "a" } ] },
              "mark": "rect",
              "encoding": { "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth" }, "y": { "field": "k", "type": "nominal" } } }
            """;

        // Act
        var rects = Rects(ChartTestHelpers.Marks(Spec, 300, 200), "mark-rect");

        // Assert
        Assert.Equal(3, rects.Count);
        AssertSpan(0.5, 31 * PerDay, rects[0]);
        AssertSpan((31 * PerDay) + 0.5, 29 * PerDay, rects[1]);
        AssertSpan((60 * PerDay) + 0.5, 31 * PerDay, rects[2]);
    }

    /// <summary>
    ///     Validates <c>timeUnitBandSize</c>: a band of half the width covers the middle half of the unit, between the quarter and the three quarters of the way, with the bin
    ///     spacing taken off each edge.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_TimeUnitBandSize_Half()
    {
        // Arrange: January from 0 to 31 days: the edges at 0.25 and 0.75 of the 31 PerDay pixels, then half a pixel inside each, plus the half pixel of the translate
        var span = 31 * PerDay;

        // Act
        var viaMark = Rects(ChartTestHelpers.Marks(Bars("{ \"type\": \"bar\", \"timeUnitBandSize\": 0.5 }"), 300, 200), "mark-bar");
        var viaConfig = Rects(ChartTestHelpers.Marks(Bars(), 300, 200, "{ \"bar\": { \"timeUnitBandSize\": 0.5 } }"), "mark-bar");
        var viaSpecConfig = Rects(ChartTestHelpers.Marks(Bars().TrimEnd().TrimEnd('}') + ", \"config\": { \"mark\": { \"timeUnitBandSize\": 0.5 } } }", 300, 200), "mark-bar");

        // Assert
        AssertSpan((0.25 * span) + 1, (0.5 * span) - 1, viaMark[0]);
        AssertSpan((0.25 * span) + 1, (0.5 * span) - 1, viaConfig[0]);
        AssertSpan((0.25 * span) + 1, (0.5 * span) - 1, viaSpecConfig[0]);
    }

    /// <summary>
    ///     Validates that the band of the <c>width</c> of a bar (<c>{"band": 0.5}</c>) is the relative size of a time unit.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_WidthBand_IsTheRelativeSize()
    {
        // Act
        var bars = Rects(ChartTestHelpers.Marks(Bars("{ \"type\": \"bar\", \"width\": { \"band\": 0.5 } }"), 300, 200), "mark-bar");

        // Assert: the same as a time unit band size of one half
        AssertSpan((0.25 * 31 * PerDay) + 1, (0.5 * 31 * PerDay) - 1, bars[0]);
    }

    /// <summary>
    ///     Validates that a unit thinner than <c>minBandSize</c> is widened around its middle to that size, without spacing.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_MinBandSize_WidensAThinUnit()
    {
        // Arrange: months of the years 2000 and 2100: the domain is the 100 years and a month, about 36,556 days over 300 pixels, so January is 31 / 36,556 * 300 = 0.254 pixels wide,
        // which is below the minimum of 0.3 (the default 0.25 is only just below it); the bar is then widened around its middle to exactly 0.3 pixels
        const string Spec = """
            { "data": { "values": [ { "date": "2000-01-15" }, { "date": "2100-01-15" } ] },
              "mark": { "type": "bar", "minBandSize": 0.3 },
              "encoding": { "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth" }, "y": { "aggregate": "count" } } }
            """;

        // Act
        var bars = Rects(ChartTestHelpers.Marks(Spec, 300, 200), "mark-bar");

        // Assert: every bar is exactly 0.3 pixels wide, centered on its month (the middle of January 2000 is 15.5 days after the domain start), plus the half pixel of the translate
        var total = TimeTestSupport.Ms(2100, 2, 1) - TimeTestSupport.Ms(2000, 1, 1);
        var middle = 15.5 * 86_400_000.0 / total * 300;
        Assert.Equal(2, bars.Count);
        Assert.All(bars, b => Assert.Equal(0.3, b.Width, 9));
        Assert.Equal(middle - 0.15 + 0.5, bars[0].X, 9);
    }

    /// <summary>
    ///     Validates that a <c>timeUnitBandPosition</c> other than 0.5 on a bar changes nothing in the geometry (the feature catalog reports it; 0.5 is used).
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_RectBandPositionNotHalf_IsReported()
    {
        // Arrange
        var other = Bars("{ \"type\": \"bar\", \"timeUnitBandPosition\": 0.2 }");

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(other));
        var plain = Rects(ChartTestHelpers.Marks(Bars(), 300, 200), "mark-bar");
        var shifted = Rects(ChartTestHelpers.Marks(other, 300, 200), "mark-bar");

        // Assert
        Assert.Contains(reports, r => r.Id == "vl-encoding-bandposition-rect");
        Assert.Equal(plain.Select(b => (b.X, b.Width)), shifted.Select(b => (b.X, b.Width)));
    }

    /// <summary>
    ///     Validates that points sit at the start of their unit (Vega-Lite gives a point no time unit band position): the domain is the starts only.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_PointsAtTheStartOfTheUnit()
    {
        // Arrange: the starts Jan 1, Feb 1 and Mar 1 are 0, 31 and 60 days apart: 0, 31 / 60 and 1 of the 300 pixels
        const string Spec = """
            { "data": { "values": [ { "date": "2024-01-15" }, { "date": "2024-02-10" }, { "date": "2024-03-20" } ] },
              "mark": "point",
              "encoding": { "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth" } } }
            """;

        // Act
        var xs = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec, 300, 200), "mark-point").Select(s => s.X).ToList();

        // Assert
        Assert.Equal(3, xs.Count);
        Assert.Equal(0, xs[0], 6);
        Assert.Equal(300.0 * 31 / 60, xs[1], 6);
        Assert.Equal(300, xs[2], 6);
    }

    /// <summary>
    ///     Validates that a tick sits in the middle of its unit (the <c>timeUnitBandPosition</c> of a tick is 0.5 by default): the domain includes the ends of the units.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_TicksAtTheMiddle()
    {
        // Arrange: a strip of ticks along the months (the y axis is nominal): the middle of January is 15.5 days, of February 31 + 14.5, of March 60 + 15.5
        const string Spec = """
            { "data": { "values": [ { "date": "2024-01-15", "k": "a" }, { "date": "2024-02-10", "k": "a" }, { "date": "2024-03-20", "k": "a" } ] },
              "mark": "tick",
              "encoding": { "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth" }, "y": { "field": "k", "type": "nominal" } } }
            """;

        // Act
        var ticks = Rects(ChartTestHelpers.Marks(Spec, 300, 200), "mark-tick");

        // Assert: a one pixel wide tick is centered on the middle of its unit
        Assert.Equal(3, ticks.Count);
        Assert.Equal([15.5 * PerDay, 45.5 * PerDay, 75.5 * PerDay], ticks.Select(t => t.X + (t.Width / 2)), new TolerantComparer());
        Assert.All(ticks, t => Assert.Equal(1, t.Width, 9));
    }

    /// <summary>
    ///     Validates the <c>bandPosition</c> of a field with a time unit on a point: 0.5 is the middle of the unit and 0.25 a quarter of the way.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_BandPositionOnAPoint()
    {
        // Arrange: with a band position the domain includes the end of the units: January 1 to April 1, 91 days
        const string Spec = """
            { "data": { "values": [ { "date": "2024-01-15" }, { "date": "2024-02-10" }, { "date": "2024-03-20" } ] },
              "mark": "point",
              "encoding": { "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth", "bandPosition": POSITION } } }
            """;

        // Act
        var middle = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec.Replace("POSITION", "0.5", StringComparison.Ordinal), 300, 200), "mark-point").Select(s => s.X);
        var quarter = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec.Replace("POSITION", "0.25", StringComparison.Ordinal), 300, 200), "mark-point").Select(s => s.X);
        var start = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec.Replace("POSITION", "0", StringComparison.Ordinal), 300, 200), "mark-point").Select(s => s.X);

        // Assert
        Assert.Equal([15.5 * PerDay, 45.5 * PerDay, 75.5 * PerDay], middle, new TolerantComparer());
        Assert.Equal([(7.75) * PerDay, (31 + 7.25) * PerDay, (60 + 7.75) * PerDay], quarter, new TolerantComparer());
        Assert.Equal([0, 31 * PerDay, 60 * PerDay], start, new TolerantComparer());
    }

    /// <summary>
    ///     Validates that points of a time scale without a unit sit at their dates, and that a bar on a time scale without a unit has the continuous band size.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_TimeScalePoints_AtTheirDates_AndBarsUseTheContinuousBandSize()
    {
        // Arrange: January 1 to 11, 2024 over 300 pixels (30 pixels a day)
        const string Spec = """
            { "data": { "values": [ { "d": "2024-01-01", "v": 1 }, { "d": "2024-01-06", "v": 2 }, { "d": "2024-01-11", "v": 3 } ] },
              "mark": MARK,
              "encoding": { "x": { "field": "d", "type": "temporal" }, "y": { "field": "v", "type": "quantitative" } } }
            """;

        // Act
        var points = ChartTestHelpers.Of<SceneSymbol>(ChartTestHelpers.Marks(Spec.Replace("MARK", "\"point\"", StringComparison.Ordinal), 300, 200), "mark-point").Select(s => s.X).ToList();
        var bars = Rects(ChartTestHelpers.Marks(Spec.Replace("MARK", "\"bar\"", StringComparison.Ordinal), 300, 200), "mark-bar");

        // Assert: points at 0, 150 and 300; bars 5 pixels wide (continuousBandSize); a bar on a continuous axis pads the scale by that size (5 pixels at each end, as for a
        // linear scale), so the three bars are centered at 5, 150 and 295
        Assert.Equal([0.0, 150, 300], points, new TolerantComparer());
        Assert.Equal([2.5, 147.5, 292.5], bars.Select(b => b.X), new TolerantComparer());
        Assert.All(bars, b => Assert.Equal(5, b.Width, 9));
    }

    /// <summary>
    ///     Validates the bands of an ordinal field with a time unit: they are those of a band scale over the months (a step of 100 and a band of 90 pixels over 300).
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_OrdinalTimeUnit_BandsOfABandScale()
    {
        // Arrange: three bands with the bar padding (inner 0.1, outer 0.05): step 300 / (3 - 0.1 + 0.1) = 100, band 90, first band at (300 - 100 * 2.9) / 2 = 5
        const string Spec = """
            { "data": { "values": [ { "date": "2024-01-15" }, { "date": "2024-02-10" }, { "date": "2024-03-20" } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "date", "type": "ordinal", "timeUnit": "month" }, "y": { "aggregate": "count" } } }
            """;

        // Act
        var bars = Rects(ChartTestHelpers.Marks(Spec, 300, 200), "mark-bar");

        // Assert
        Assert.Equal(3, bars.Count);
        AssertSpan(5, 90, bars[0]);
        AssertSpan(105, 90, bars[1]);
        AssertSpan(205, 90, bars[2]);
    }

    /// <summary>
    ///     Validates that the bars of a time unit in a time zone span their local months.
    /// </summary>
    [Fact]
    public void MarkBuilder_Time_Zone_BarsSpanLocalMonths()
    {
        // Arrange: two instants late in the evening UTC of March 31 and February 29 in a zone five and a half hours ahead: they are in April and March locally
        const string Spec = """
            { "data": { "values": [ { "date": "2024-02-29T20:00:00Z" }, { "date": "2024-03-31T20:00:00Z" } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "date", "type": "temporal", "timeUnit": "yearmonth" }, "y": { "aggregate": "count" } } }
            """;

        // Act
        var local = Rects(ChartTestHelpers.Marks(Spec, 300, 200, zone: TimeTestSupport.Fixed(5, 30)), "mark-bar");
        var utc = Rects(ChartTestHelpers.Marks(Spec, 300, 200), "mark-bar");

        // Assert: locally March (31 days) and April (30 days) share the 61 days of the domain (the bar width is days * 300 / 61 less the spacing pixel); in UTC they are February (29)
        // and March (31): 60 days
        Assert.Equal(2, local.Count);
        Assert.Equal((31 * 300.0 / 61) - 1, local[0].Width, 6);
        Assert.Equal((30 * 300.0 / 61) - 1, local[1].Width, 6);
        Assert.Equal((29 * 300.0 / 60) - 1, utc[0].Width, 6);
        Assert.Equal((31 * 300.0 / 60) - 1, utc[1].Width, 6);
    }

    /// <summary>
    ///     Compares sequences of numbers with a tolerance of a millionth, for positions that are fractions of a day.
    /// </summary>
    private sealed class TolerantComparer : IEqualityComparer<double>
    {
        /// <inheritdoc/>
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-6;

        /// <inheritdoc/>
        public int GetHashCode(double obj) => 0;
    }
}
