using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Gantt;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Gantt;

/// <summary>
///     Unit tests for <c>TimeAxis</c> (with <see cref="JsDate"/>, <see cref="TimeInterval"/> and <see cref="TimeScale"/>):
///     JavaScript date arithmetic in UTC, the d3-time intervals and ticks, d3-array's <c>tickStep</c>, the time scale with
///     <c>rangeRound</c>, d3-time-format's <c>timeFormat</c>, and every case of <c>time-axis.json</c> recorded from the d3
///     packages that the pinned Mermaid bundles.
/// </summary>
public sealed class TimeAxisTests
{
    /// <summary>The names of the vector intervals and their d3 counterparts.</summary>
    private static readonly Dictionary<string, TimeInterval> Intervals = new(StringComparer.Ordinal)
    {
        ["millisecond"] = TimeAxis.Millisecond,
        ["second"] = TimeAxis.Second,
        ["minute"] = TimeAxis.Minute,
        ["hour"] = TimeAxis.Hour,
        ["day"] = TimeAxis.Day,
        ["sunday"] = TimeAxis.Sunday,
        ["monday"] = TimeAxis.Monday,
        ["tuesday"] = TimeAxis.Tuesday,
        ["wednesday"] = TimeAxis.Wednesday,
        ["thursday"] = TimeAxis.Thursday,
        ["friday"] = TimeAxis.Friday,
        ["saturday"] = TimeAxis.Saturday,
        ["month"] = TimeAxis.Month,
        ["year"] = TimeAxis.Year,
    };

    /// <summary>
    ///     Gets the indexes of the vector cases.
    /// </summary>
    public static TheoryData<int> Cases => UpstreamVectors.Indexes(UpstreamVectors.Times);

    /// <summary>
    ///     Checks that the file holds every kind of case, with enough of each.
    /// </summary>
    [Fact]
    public void TimeAxis_Vectors_FileHasEveryKind_EnoughCases()
    {
        // Arrange
        var cases = UpstreamVectors.Times;

        // Act
        var kinds = cases.GroupBy(c => (string)c!["kind"]!).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // Assert
        Assert.True(cases.Count >= 1000, $"only {cases.Count} cases");
        Assert.All(["date", "components", "set", "interval", "range", "ticks", "tickStep", "scale", "format"], kind => Assert.True(kinds.GetValueOrDefault(kind) >= 6, kind));
    }

    /// <summary>
    ///     Computes one vector case and compares the result with what d3 recorded.
    /// </summary>
    /// <param name="index">The case index.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TimeAxis_UpstreamVector_MatchesD3(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Times[index]!.AsObject();

        // Act
        var (expected, actual) = Compute(vector);

        // Assert
        Assert.Null(UpstreamVectors.Difference(expected, actual) is { } difference ? $"{vector.ToJsonString()[..Math.Min(300, vector.ToJsonString().Length)]}: {difference}" : null);
    }

    /// <summary>
    ///     Checks the ECMAScript date arithmetic: the getters of a date before 1970, the carry of the setters, the
    ///     two-digit years of the constructor and the range limit.
    /// </summary>
    [Fact]
    public void TimeAxis_JsDate_EcmaScriptArithmetic_MatchesJavaScript()
    {
        // Arrange
        var before = JsDate.FromTime(-1);
        var leap = JsDate.FromComponents(2024, 1, 29);

        // Act
        var carried = leap.SetMonth(12);
        var twoDigit = JsDate.FromComponents(99, 0, 1);
        var beyond = JsDate.FromComponents(275760, 8, 14);

        // Assert
        Assert.Equal((1969.0, 11.0, 31.0, 3.0, 23.0, 59.0, 59.0, 999.0), (before.FullYear, before.Month, before.Date, before.Day, before.Hours, before.Minutes, before.Seconds, before.Milliseconds));
        Assert.Equal("2025-01-29T00:00:00.000Z", carried.ToString());
        Assert.Equal("1999-01-01T00:00:00.000Z", twoDigit.ToString());
        Assert.False(beyond.IsValid);
        Assert.Equal("Invalid Date", beyond.ToString());
        Assert.True(double.IsNaN(beyond.FullYear));
    }

    /// <summary>
    ///     Checks that the year setter of an invalid date starts from time zero, while the other setters keep it
    ///     invalid, as in JavaScript.
    /// </summary>
    [Fact]
    public void TimeAxis_JsDate_SettersOfInvalidDate_FollowJavaScript()
    {
        // Arrange
        var invalid = JsDate.Invalid;

        // Act
        var year = invalid.SetFullYear(2024);
        var month = invalid.SetMonth(1);
        var hours = invalid.SetHours(0, 0, 0, 0);

        // Assert
        Assert.Equal("2024-01-01T00:00:00.000Z", year.ToString());
        Assert.False(month.IsValid);
        Assert.False(hours.IsValid);
    }

    /// <summary>
    ///     Checks the interval functions: the floor and ceiling of a day, a Monday week and a month, the offset, the
    ///     range and the count, and the filtered every interval of a day.
    /// </summary>
    [Fact]
    public void TimeAxis_Intervals_FloorCeilOffsetRangeCount_FollowD3()
    {
        // Arrange
        var date = JsDate.FromComponents(2024, 5, 12, 10, 30);

        // Act
        var dayFloor = TimeAxis.Day.Floor(date);
        var dayCeil = TimeAxis.Day.Ceil(date);
        var monday = TimeAxis.Monday.Floor(date);
        var month = TimeAxis.Month.Offset(date, 2.7);
        var range = TimeAxis.Day.Every(2)!.Range(dayFloor.Time, TimeAxis.Day.Offset(dayFloor, 6).Time);
        var count = TimeAxis.Sunday.Count(JsDate.FromComponents(2024, 0, 1).Time, date.Time);

        // Assert
        Assert.Equal("2024-06-12T00:00:00.000Z", dayFloor.ToString());
        Assert.Equal("2024-06-13T00:00:00.000Z", dayCeil.ToString());
        Assert.Equal("2024-06-10T00:00:00.000Z", monday.ToString());
        Assert.Equal("2024-08-12T10:30:00.000Z", month.ToString());
        Assert.Equal(["2024-06-13T00:00:00.000Z", "2024-06-15T00:00:00.000Z", "2024-06-17T00:00:00.000Z"], range.Select(d => d.ToString()));
        Assert.Equal(23, count);
    }

    /// <summary>
    ///     Checks the special every intervals: a millisecond multiple, a year multiple without count, a step that is not
    ///     positive, and an interval without count that refuses every.
    /// </summary>
    [Fact]
    public void TimeAxis_Every_SpecialCases_FollowD3()
    {
        // Arrange
        var date = JsDate.FromComponents(2024, 5, 12, 10, 30, 0, 123);

        // Act
        var milliseconds = TimeAxis.Millisecond.Every(50)!.Floor(date);
        var decade = TimeAxis.Year.Every(10)!;

        // Assert
        Assert.Equal(100, milliseconds.Milliseconds);
        Assert.Equal("2020-01-01T00:00:00.000Z", decade.Floor(date).ToString());
        Assert.False(decade.HasCount);
        Assert.Null(TimeAxis.Day.Every(0));
        Assert.Same(TimeAxis.Day, TimeAxis.Day.Every(1.5));
        Assert.Throws<InvalidOperationException>(() => TimeAxis.Day.Every(2)!.Every(2));
        Assert.Throws<InvalidOperationException>(() => decade.Count(0, 1));
    }

    /// <summary>
    ///     Checks the default ticks of a month and the tick step, and that a domain of invalid dates gives no ticks.
    /// </summary>
    [Fact]
    public void TimeAxis_Ticks_MonthDomain_GivesTwoDayTicksAndEmptyForInvalid()
    {
        // Arrange
        var scale = new TimeScale(JsDate.FromComponents(2024, 0, 1).Time, JsDate.FromComponents(2024, 1, 1).Time, 0, 634);

        // Act
        var ticks = scale.Ticks();
        var invalid = new TimeScale(double.NaN, double.NaN, 0, 634).Ticks();

        // Assert: the 31 days ask for steps of about 3 days, and two days is the nearer table row
        Assert.Equal(17, ticks.Count);
        Assert.Equal("2024-01-03T00:00:00.000Z", ticks[1].ToString());
        Assert.Empty(invalid);
        Assert.Equal(0.1, TimeAxis.TickStep(0, 1, 10));
        Assert.Equal(-5, TimeAxis.TickStep(50, 0, 10));
    }

    /// <summary>
    ///     Checks the rounding of the scale (halves up, as JavaScript's <c>Math.round</c>), a descending domain and the
    ///     middle of a zero-length domain.
    /// </summary>
    [Fact]
    public void TimeAxis_TimeScale_RangeRound_FollowsJavaScriptRounding()
    {
        // Arrange
        var scale = new TimeScale(0, 4, 0, 10);

        // Act / Assert
        Assert.Equal(3, scale.Map(1));
        Assert.Equal(5, scale.Map(2));
        Assert.Equal(8, scale.Map(3));
        Assert.Equal(-2, TimeAxis.JsRound(-2.5));
        Assert.Equal(10, new TimeScale(4, 0, 0, 10).Map(0));
        Assert.Equal(5, new TimeScale(7, 7, 0, 10).Map(123));
        Assert.True(double.IsNaN(scale.Map(double.NaN)));
    }

    /// <summary>
    ///     Checks the format directives, the pad modifiers, the locale formats and the text of an invalid date.
    /// </summary>
    [Fact]
    public void TimeAxis_Format_Directives_FollowD3TimeFormat()
    {
        // Arrange
        var date = JsDate.FromComponents(2024, 5, 2, 9, 5, 7, 12);

        // Act / Assert
        Assert.Equal("2024-06-02", TimeAxis.Format("%Y-%m-%d", date));
        Assert.Equal("6/2/2024, 9:05:07 AM", TimeAxis.Format("%c", date));
        Assert.Equal("Sun Jun  2|2|02|_2", TimeAxis.Format("%a %b %e|%-d|%0d|_%-e", date));
        Assert.Equal("22 7 +0000 % k", TimeAxis.Format("%V %u %Z %% %k", date));
        Assert.Equal("0NaN- / 12AM", TimeAxis.Format("%Y-%b / %I%p", JsDate.Invalid));
    }

    /// <summary>
    ///     Computes the result of a vector case.
    /// </summary>
    /// <param name="vector">The case.</param>
    /// <returns>The expected and the actual JSON.</returns>
    private static (JsonNode? Expected, JsonNode? Actual) Compute(JsonObject vector)
    {
        var kind = (string)vector["kind"]!;
        switch (kind)
        {
            case "date":
                {
                    var d = JsDate.FromTime(Number(vector["time"]));
                    return (vector["expected"], Numbers([d.FullYear, d.Month, d.Date, d.Day, d.Hours, d.Minutes, d.Seconds, d.Milliseconds]));
                }

            case "components":
                {
                    var a = vector["args"]!.AsArray().Select(Number).ToArray();
                    var d = JsDate.FromComponents(a[0], a[1], At(a, 2, 1), At(a, 3, 0), At(a, 4, 0), At(a, 5, 0), At(a, 6, 0));
                    return (vector["expected"], Json(d.Time));
                }

            case "set":
                return (vector["expected"], Json(Set(vector).Time));
            case "interval":
                return Interval(vector);
            case "range":
                {
                    var interval = Every(vector);
                    var range = interval.Range(Number(vector["start"]), Number(vector["stop"]), Number(vector["step"]));
                    return (vector["expected"], Numbers(range.Select(d => d.Time)));
                }

            case "ticks":
                {
                    var domain = vector["domain"]!.AsArray().Select(Number).ToArray();
                    var scale = new TimeScale(domain[0], domain[1], 0, 1);
                    var ticks = vector["interval"] is null ? scale.Ticks(Number(vector["count"])) : scale.Ticks(Every(vector));
                    return (vector["expected"], Numbers(ticks.Select(d => d.Time)));
                }

            case "tickStep":
                return (vector["expected"], Json(TimeAxis.TickStep(Number(vector["start"]), Number(vector["stop"]), Number(vector["count"]))));
            case "scale":
                {
                    var domain = vector["domain"]!.AsArray().Select(Number).ToArray();
                    var range = vector["range"]!.AsArray().Select(Number).ToArray();
                    var scale = new TimeScale(domain[0], domain[1], range[0], range[1]);
                    return (vector["expected"], Numbers(vector["inputs"]!.AsArray().Select(v => scale.Map(JsDate.FromTime(Number(v)).Time))));
                }

            case "format":
                {
                    var d = JsDate.FromTime(Number(vector["time"]));
                    var texts = vector["specifiers"]!.AsArray().Select(s => (JsonNode?)JsonValue.Create(TimeAxis.Format((string)s!, d)));
                    return (vector["expected"], new JsonArray([.. texts]));
                }

            default:
                throw new InvalidOperationException($"unknown kind {kind}");
        }
    }

    /// <summary>
    ///     Computes an interval case: floor, ceiling, offsets and counts around a date.
    /// </summary>
    /// <param name="vector">The case.</param>
    /// <returns>The expected and the actual JSON.</returns>
    private static (JsonNode? Expected, JsonNode? Actual) Interval(JsonObject vector)
    {
        var interval = Every(vector);
        var time = Number(vector["time"]);
        var date = JsDate.FromTime(time);
        var expected = new JsonObject
        {
            ["floor"] = vector["floor"]?.DeepClone(),
            ["ceil"] = vector["ceil"]?.DeepClone(),
            ["offsets"] = vector["offsets"]!.DeepClone(),
        };
        var actual = new JsonObject
        {
            ["floor"] = Json(interval.Floor(date).Time),
            ["ceil"] = Json(interval.Ceil(date).Time),
            ["offsets"] = new JsonArray([.. vector["offsets"]!.AsArray().Select(o =>
            {
                var step = Number(o![0]);
                return (JsonNode?)new JsonArray(Json(step), Json(interval.Offset(date, step).Time));
            })]),
        };
        if (vector["counts"] is JsonArray counts)
        {
            expected["counts"] = counts.DeepClone();
            actual["counts"] = new JsonArray([.. counts.Select(c =>
            {
                var other = Number(c![0]);
                return (JsonNode?)new JsonArray(Json(other), Json(interval.Count(time, other)));
            })]);
        }

        return (expected, actual);
    }

    /// <summary>
    ///     Applies the setter of a set case.
    /// </summary>
    /// <param name="vector">The case.</param>
    /// <returns>The changed date.</returns>
    private static JsDate Set(JsonObject vector)
    {
        var d = JsDate.FromTime(Number(vector["time"]));
        var a = vector["args"]!.AsArray().Select(Number).ToArray();
        double? Optional(int i) => i < a.Length ? a[i] : null;
        return (string)vector["method"]! switch
        {
            "setFullYear" => d.SetFullYear(a[0], Optional(1), Optional(2)),
            "setMonth" => d.SetMonth(a[0], Optional(1)),
            "setDate" => d.SetDate(a[0]),
            "setHours" => d.SetHours(a[0], Optional(1), Optional(2), Optional(3)),
            "setMinutes" => d.SetMinutes(a[0], Optional(1), Optional(2)),
            "setSeconds" => d.SetSeconds(a[0], Optional(1)),
            "setMilliseconds" => d.SetMilliseconds(a[0]),
            "setTime" => JsDate.FromTime(a[0]),
            var method => throw new InvalidOperationException($"unknown setter {method}"),
        };
    }

    /// <summary>
    ///     Gets the interval of a case: the named interval, every so many.
    /// </summary>
    /// <param name="vector">The case.</param>
    /// <returns>The interval.</returns>
    private static TimeInterval Every(JsonObject vector) => Intervals[(string)vector["interval"]!].Every(Number(vector["every"]))!;

    /// <summary>
    ///     Gets an optional argument, or its default.
    /// </summary>
    /// <param name="values">The arguments.</param>
    /// <param name="index">The index.</param>
    /// <param name="fallback">The default.</param>
    /// <returns>The value.</returns>
    private static double At(double[] values, int index, double fallback) => index < values.Length ? values[index] : fallback;

    /// <summary>
    ///     Reads a JSON number, with null as NaN.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The number.</returns>
    private static double Number(JsonNode? node) => node is null ? double.NaN : node.GetValue<double>();

    /// <summary>
    ///     Writes a number as JSON, with NaN and infinities as null (as <c>JSON.stringify</c> writes them).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The node.</returns>
    private static JsonNode? Json(double value) => double.IsFinite(value) ? JsonValue.Create(value) : null;

    /// <summary>
    ///     Writes numbers as a JSON array, with NaN as null.
    /// </summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The array.</returns>
    private static JsonArray Numbers(IEnumerable<double> values) => new([.. values.Select(Json)]);
}
