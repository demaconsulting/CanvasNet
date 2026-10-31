using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the legends of temporal fields in <see cref="LegendBuilder"/>: the time ticks and date labels of a gradient legend of a temporal color, the labels of
///     the dates of a field with a time unit, a size over dates, explicit <c>values</c>, the time zone, the limits and the determinism. The expected ticks and labels are
///     derived by hand from the d3 interval table (a count of 4 over ten days targets 2.5 days, which is nearest to the two days of the table) and the label format
///     <c>%b %d, %Y</c> of <c>config.timeFormat</c>.
/// </summary>
public class LegendBuilderTimeTests
{
    /// <summary>Three points whose color is the date (January 1, 6 and 11, 2024); the legend is a gradient 200 pixels long.</summary>
    private const string ColorDates = """
        { "data": { "values": [ { "d": "2024-01-01", "x": 1 }, { "d": "2024-01-06", "x": 2 }, { "d": "2024-01-11", "x": 3 } ] },
          "mark": "point",
          "encoding": { "x": { "field": "x", "type": "quantitative" },
                        "color": { "field": "d", "type": "temporal"EXTRA } } }
        """;

    /// <summary>Three months of data (January 15, February 10 and March 20, 2024), whose color field is the month.</summary>
    private const string ColorMonths = """
        { "data": { "values": [ { "d": "2024-01-15", "x": 1 }, { "d": "2024-02-10", "x": 2 }, { "d": "2024-03-20", "x": 3 } ] },
          "mark": "point",
          "encoding": { "x": { "field": "x", "type": "quantitative" },
                        "color": { "field": "d", "type": "TYPE", "timeUnit": "UNIT"EXTRA } } }
        """;

    /// <summary>
    ///     Builds the date color specification with extra members in the color definition.
    /// </summary>
    /// <param name="extra">The members, each starting with a comma, or an empty text.</param>
    /// <returns>The specification text.</returns>
    private static string Dates(string extra = "") => ColorDates.Replace("EXTRA", extra, StringComparison.Ordinal);

    /// <summary>
    ///     Builds the month color specification.
    /// </summary>
    /// <param name="type">The field type.</param>
    /// <param name="unit">The time unit.</param>
    /// <param name="extra">The members, each starting with a comma, or an empty text.</param>
    /// <returns>The specification text.</returns>
    private static string Months(string type, string unit, string extra = "") =>
        ColorMonths.Replace("TYPE", type, StringComparison.Ordinal).Replace("UNIT", unit, StringComparison.Ordinal).Replace("EXTRA", extra, StringComparison.Ordinal);

    /// <summary>
    ///     Gets the label texts of a legend in drawing order.
    /// </summary>
    /// <param name="legend">The legend.</param>
    /// <returns>The texts.</returns>
    private static List<string> Labels(LegendResult legend) =>
        [.. Assert.IsType<SceneGroup>(legend.Children[0]).Children.OfType<SceneText>().Where(t => t.Role == "legend-label").Select(t => t.Text)];

    /// <summary>
    ///     Gets the January 2024 days that a two-day tick sequence has from the 1st to the 11th, as legend labels.
    /// </summary>
    /// <param name="days">The days of January.</param>
    /// <returns>The labels in <c>%b %d, %Y</c> form.</returns>
    private static string[] January(params int[] days) => [.. days.Select(d => $"Jan {d:00}, 2024")];

    /// <summary>
    ///     Validates the gradient legend of a temporal color: the labels are the two-day ticks of the ten days in the format of <c>config.timeFormat</c>
    ///     (<c>%b %d, %Y</c>), and the color ramp is sampled at the time ticks for a count of 15 (every 12 hours: 21 stops) instead of at numeric ticks of the milliseconds.
    /// </summary>
    [Fact]
    public void LegendBuilder_Time_TemporalGradient_LabelsUseConfigTimeFormat()
    {
        // Arrange: the bar is 200 pixels (a vertical legend: the height of the data rectangle is clamped to 200), so the tick count is max(2, 2 * floor(200 / 100)) = 4. The target
        // is 10 days / 4 = 2.5 days, nearest to the two days of the table (2.5 / 2 is below 7 / 2.5): the days of the month 1, 3, 5, 7, 9, 11. The count of 15 for the ramp
        // targets 16 hours, nearest to 12 hours (16 / 12 = 1.33 is below 24 / 16 = 1.5): 00:00 and 12:00 of each day, 21 stops from the 1st to the 11th at 0, 0.05, ... 1.

        // Act
        var legend = ChartTestHelpers.Legend(Dates());

        // Assert
        Assert.Equal(January(1, 3, 5, 7, 9, 11), Labels(legend));
        var bar = Assert.IsType<SceneGroup>(legend.Children[0]).Children.OfType<SceneRect>().Single();
        Assert.NotNull(bar.FillGradient);
        Assert.Equal(21, bar.FillGradient.Stops.Count);
        Assert.Equal(Enumerable.Range(0, 21).Select(i => i / 20.0), bar.FillGradient.Stops.Select(s => s.Offset).Select(o => Math.Round(o, 9)));
        Assert.Equal(["d"], Assert.IsType<SceneText>(legend.Children[1]).Lines);
    }

    /// <summary>
    ///     Validates that <c>legend.format</c> (a text and a multi-format object) and <c>config.timeFormat</c> choose the labels of a temporal legend, and that the unit
    ///     specifier of a time unit is used without a format.
    /// </summary>
    [Fact]
    public void LegendBuilder_Time_LegendFormatOverridesAndUnitSpecifier()
    {
        // Arrange: the ten days of the date color have the two-day ticks of the 1st, 3rd, ... 11th; a count of 4 over three months (60 days, the unit yearmonth) targets
        // 15 days, nearest to the month of the table (15 / 7 is not below 30 / 15): the ticks Jan 1, Feb 1 and Mar 1

        // Act
        var format = ChartTestHelpers.Legend(Dates(", \"legend\": { \"format\": \"%d/%m\" }"));
        var multi = ChartTestHelpers.Legend(Dates(", \"legend\": { \"format\": { \"date\": \"%d\", \"year\": \"Y%Y\" } }"));
        var configured = ChartTestHelpers.Legend(Dates(), callerConfig: "{ \"timeFormat\": \"%Y.%m.%d\" }");
        var unit = ChartTestHelpers.Legend(Months("temporal", "yearmonth"));

        // Assert
        Assert.Equal(["01/01", "03/01", "05/01", "07/01", "09/01", "11/01"], Labels(format));
        Assert.Equal(["Y2024", "03", "05", "Jan 07", "09", "11"], Labels(multi));
        Assert.Equal(["2024.01.01", "2024.01.03", "2024.01.05", "2024.01.07", "2024.01.09", "2024.01.11"], Labels(configured));
        Assert.Equal(["Jan 2024", "Feb 2024", "Mar 2024"], Labels(unit));
    }

    /// <summary>
    ///     Validates that the symbols of an ordinal field with a time unit are labeled with the dates of the unit (the specifier of the unit, or the legend format), and
    ///     that <c>legend.values</c> can name a part of the unit.
    /// </summary>
    [Fact]
    public void LegendBuilder_Time_OrdinalTimeUnitSymbols_LabelWithTheUnitSpecifier()
    {
        // Act
        var unit = ChartTestHelpers.Legend(Months("ordinal", "month"));
        var named = ChartTestHelpers.Legend(Months("ordinal", "month", ", \"legend\": { \"format\": \"%B\" }"));
        var chosen = ChartTestHelpers.Legend(Months("ordinal", "month", ", \"legend\": { \"values\": [ \"Feb\" ] }"));

        // Assert: one symbol and label per month, in date order
        Assert.Equal(["Jan", "Feb", "Mar"], Labels(unit));
        Assert.Equal(3, Assert.IsType<SceneGroup>(unit.Children[0]).Children.OfType<SceneSymbol>().Count());
        Assert.Equal(["January", "February", "March"], Labels(named));
        Assert.Equal(["Feb"], Labels(chosen));
    }

    /// <summary>
    ///     Validates that a size over dates has a symbol legend whose entries are the time ticks of its domain, labeled as dates.
    /// </summary>
    [Fact]
    public void LegendBuilder_Time_SizeOnTimeScale_TicksAndLabels()
    {
        // Arrange: the symbol legend of a continuous scale has 5 ticks by default: a target of 2 days (the two days of the table): the days 1, 3, 5, 7, 9 and 11
        var spec = Dates().Replace("\"color\"", "\"size\"", StringComparison.Ordinal);

        // Act
        var legend = ChartTestHelpers.Legend(spec);

        // Assert
        Assert.Equal(January(1, 3, 5, 7, 9, 11), Labels(legend));
        Assert.Equal(6, Assert.IsType<SceneGroup>(legend.Children[0]).Children.OfType<SceneSymbol>().Count());
    }

    /// <summary>
    ///     Validates that <c>legend.values</c> accept date-time objects, date texts and timestamps on a temporal legend.
    /// </summary>
    [Fact]
    public void LegendBuilder_Time_ExplicitValuesAcceptDatetimeObjects()
    {
        // Act: two values are two labels (a gradient legend shows the two ends of the domain only when it has fewer than three ticks of its own)
        var legend = ChartTestHelpers.Legend(Dates(", \"legend\": { \"values\": [ { \"year\": 2024, \"month\": 1, \"date\": 3 }, \"2024-01-09\", 1704067200000 ] }"));

        // Assert
        Assert.Equal(January(3, 9, 1), Labels(legend));
    }

    /// <summary>
    ///     Validates that the labels of a temporal legend are in the time zone of the chart.
    /// </summary>
    [Fact]
    public void LegendBuilder_Time_Zone_LabelsUseLocalTime()
    {
        // Arrange: dates at 00:00Z in a zone five and a half hours ahead; the two-day ticks are the local midnights of the odd days: the 1st is before the domain starts
        // (local midnight of January 1 is 18:30Z on December 31), so the first tick is the 3rd and the last the 11th (18:30Z on the 10th)
        const string Spec = """
            { "data": { "values": [ { "d": "2024-01-01T00:00:00Z", "x": 1 }, { "d": "2024-01-11T00:00:00Z", "x": 2 } ] },
              "mark": "point",
              "encoding": { "x": { "field": "x", "type": "quantitative" }, "color": { "field": "d", "type": "temporal" } } }
            """;

        // Act
        var local = ChartTestHelpers.Legend(Spec, zone: TimeTestSupport.Fixed(5, 30));
        var utc = ChartTestHelpers.Legend(Spec);

        // Assert
        Assert.Equal(January(3, 5, 7, 9, 11), Labels(local));
        Assert.Equal(January(1, 3, 5, 7, 9, 11), Labels(utc));
    }

    /// <summary>
    ///     Validates the limits of a temporal legend: a label longer than <see cref="VegaLiteLimits.MaxStringLength"/>, more ticks than <see cref="VegaLiteLimits.MaxTicks"/>
    ///     and a canceled token are failures that name their limits and place.
    /// </summary>
    /// <returns>A task that completes when the checks are done.</returns>
    [Fact]
    public async Task LegendBuilder_Time_LimitsAndCancellation()
    {
        // Arrange: "Jan 01, 2024" has 12 characters; an hourly interval over ten days has 241 ticks
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        // Act
        var tooLong = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Legend(Dates(), guideLimits: new VegaLiteLimits { MaxStringLength = 10 }));
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Legend(Dates(", \"legend\": { \"tickCount\": \"hour\" }"), guideLimits: new VegaLiteLimits { MaxTicks = 5 }));
        var fits = ChartTestHelpers.Legend(Dates(), guideLimits: new VegaLiteLimits { MaxStringLength = 12 });

        // Assert
        Assert.True(tooLong.Location.IsKnown);
        Assert.Contains("the limit of 10", tooLong.Message, StringComparison.Ordinal);
        Assert.True(tooMany.Location.IsKnown);
        Assert.Contains("MaxTicks", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal(6, Labels(fits).Count);
        Assert.Throws<OperationCanceledException>(() => ChartTestHelpers.Legend(Dates(), cancellationToken: canceled.Token));
        Assert.Equal(6, Labels(ChartTestHelpers.Legend(Dates(), cancellationToken: TestContext.Current.CancellationToken)).Count);
    }

    /// <summary>
    ///     Validates that the labels of a temporal legend do not depend on the current culture or on the order of the calls.
    /// </summary>
    /// <param name="culture">The name of the culture.</param>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("")]
    public void LegendBuilder_Time_Culture_InvariantAndDeterministic(string culture)
    {
        // Act
        var labels = ExpressionTestSupport.UnderCulture(culture, () => (Labels(ChartTestHelpers.Legend(Dates())), Labels(ChartTestHelpers.Legend(Dates()))));

        // Assert
        Assert.Equal(January(1, 3, 5, 7, 9, 11), labels.Item1);
        Assert.Equal(labels.Item1, labels.Item2);
    }
}
