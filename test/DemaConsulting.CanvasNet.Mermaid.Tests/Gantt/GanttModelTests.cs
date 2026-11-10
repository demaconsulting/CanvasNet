using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Gantt;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Gantt;

/// <summary>
///     Unit tests for <c>GanttModel</c> (with <c>Dayjs</c> and <c>GanttClock</c>): task dates, durations, <c>after</c> and
///     <c>until</c> with several ids, the passes, excluded and included days, inclusive end dates, today with and without
///     <see cref="MermaidRenderOptions.Now"/>, the ECMAScript date fallback, the failures, the JavaScript object
///     semantics of ids, and the pinned work charges.
/// </summary>
public sealed class GanttModelTests
{
    /// <summary>A day in milliseconds.</summary>
    private const double Day = 86_400_000;

    /// <summary>2024-01-01T00:00Z in milliseconds.</summary>
    private const double January1 = 1_704_067_200_000;

    /// <summary>
    ///     Validates durations of every unit, a fractional day rounded, a month kept at the last day of a shorter month,
    ///     and a text that is no duration (the end is the start).
    /// </summary>
    [Fact]
    public void GanttModel_Compile_Durations_AddAsDayjsDoes()
    {
        // Arrange
        var model = GanttParserTests.Parse(
            "gantt\ndateFormat YYYY-MM-DD\nA :a, 2024-01-31, 1M\nB :b, 2024-01-01, 1.5d\nC :c, 2024-01-01, 36h\nD :d, 2024-01-01, 2w\nE :e, 2024-01-01, 1 d\n");

        // Act
        var tasks = Compile(model);

        // Assert
        Assert.Equal(Date(2024, 2, 29), tasks[0].EndTime!.Value.Time);
        Assert.Equal(January1 + (2 * Day), tasks[1].EndTime!.Value.Time);
        Assert.Equal(January1 + (1.5 * Day), tasks[2].EndTime!.Value.Time);
        Assert.Equal(January1 + (14 * Day), tasks[3].EndTime!.Value.Time);
        Assert.Equal(January1, tasks[4].EndTime!.Value.Time);
    }

    /// <summary>
    ///     Validates <c>after</c> with several ids (the latest end), <c>until</c> with several ids (the earliest start),
    ///     forward references resolved in later passes, and a chain from the previous task.
    /// </summary>
    [Fact]
    public void GanttModel_Compile_AfterUntilAndForwardReferences_Resolve()
    {
        // Arrange
        var model = GanttParserTests.Parse(
            "gantt\ndateFormat YYYY-MM-DD\nC :c, after a b, 1d\nA :a, 2024-01-01, 3d\nB :b, 2024-01-02, 5d\nD :d, 2024-01-01, until b a\nE :2d\n");

        // Act
        var tasks = Compile(model);

        // Assert
        Assert.Equal(January1 + (6 * Day), tasks[0].StartTime!.Value.Time);
        Assert.Equal(January1, tasks[3].EndTime!.Value.Time);
        Assert.Equal((January1, January1 + (2 * Day)), (tasks[4].StartTime!.Value.Time, tasks[4].EndTime!.Value.Time));
    }

    /// <summary>
    ///     Validates excluded days: weekends move the end and shorten the drawn bar, an included date is a working day, a
    ///     manual end is not moved, and Friday weekends move the weekend.
    /// </summary>
    [Fact]
    public void GanttModel_Compile_ExcludesAndIncludes_MoveEnds()
    {
        // Arrange
        var weekends = GanttParserTests.Parse("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends\nA :a, 2024-01-05, 2d\nB :b, 2024-01-05, 2024-01-08\n");
        var included = GanttParserTests.Parse("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends\nincludes 2024-01-06\nA :a, 2024-01-05, 2d\n");
        var friday = GanttParserTests.Parse("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends\nweekend friday\nA :a, 2024-01-04, 2d\n");

        // Act
        var a = Compile(weekends);
        var b = Compile(included);
        var c = Compile(friday);

        // Assert
        Assert.Equal((Date(2024, 1, 9), Date(2024, 1, 9)), (a[0].EndTime!.Value.Time, a[0].RenderEndTime!.Value.Time));
        Assert.Equal((Date(2024, 1, 8), (double?)null), (a[1].EndTime!.Value.Time, a[1].RenderEndTime?.Time));
        Assert.Equal(Date(2024, 1, 8), b[0].EndTime!.Value.Time);
        Assert.Equal(Date(2024, 1, 8), c[0].EndTime!.Value.Time);
    }

    /// <summary>
    ///     Validates inclusive end dates: a date end is a day later, a duration is not.
    /// </summary>
    [Fact]
    public void GanttModel_Compile_InclusiveEndDates_AddADayToDates()
    {
        // Arrange
        var model = GanttParserTests.Parse("gantt\ndateFormat YYYY-MM-DD\ninclusiveEndDates\nA :a, 2024-01-01, 2024-01-03\nB :b, 2024-01-01, 2d\n");

        // Act
        var tasks = Compile(model);

        // Assert
        Assert.Equal(Date(2024, 1, 4), tasks[0].EndTime!.Value.Time);
        Assert.True(tasks[0].ManualEndTime);
        Assert.Equal(Date(2024, 1, 3), tasks[1].EndTime!.Value.Time);
    }

    /// <summary>
    ///     Validates today: with a clock, an unknown <c>after</c> id starts at its midnight UTC and a date format without a
    ///     day takes its date; without one, 1970-01-01 is taken and the warning is reported once.
    /// </summary>
    [Fact]
    public void GanttModel_Compile_Today_FromClockOrEpochWithWarning()
    {
        // Arrange
        const string Text = "gantt\ndateFormat HH:mm\nA :a, 10:30, 2h\nB :b, after nothing, 1h\n";
        var model = GanttParserTests.Parse(Text);
        var sink = new MermaidReportSink(100, false);
        var reports = new List<MermaidDiagnostic>();

        // Act
        var known = model.Compile(new GanttClock(new DateTimeOffset(2024, 6, 12, 23, 30, 0, TimeSpan.FromHours(2)), sink), Meter(model));
        sink.Deliver(reports.Add, null);
        var unknown = model.Compile(new GanttClock(null, sink), Meter(model));
        sink.Deliver(reports.Add, null);

        // Assert
        Assert.Equal(Date(2024, 6, 12) + (10.5 * 3_600_000), known[0].StartTime!.Value.Time);
        Assert.Equal(Date(2024, 6, 12), known[1].StartTime!.Value.Time);
        Assert.Equal(10.5 * 3_600_000, unknown[0].StartTime!.Value.Time);
        Assert.Equal(0, unknown[1].StartTime!.Value.Time);
        var warning = Assert.Single(reports);
        Assert.Equal(MermaidDiagnosticCodes.GanttTodayUnavailable, warning.Code);
    }

    /// <summary>
    ///     Validates the date fallback without a date format: the ECMAScript forms are read (a date alone in UTC, an offset
    ///     applied), and other texts fail with Mermaid's <c>Invalid date</c> message at the task.
    /// </summary>
    [Fact]
    public void GanttModel_Compile_DateFallback_ReadsEcmaScriptFormsOnly()
    {
        // Arrange
        var model = GanttParserTests.Parse("gantt\nA :a, 2024-01-05, 1d\nB :b, 2024-01-05T10:00:00+02:00, 1d\nC :c, 2024-02-30, 1d\n");
        var legacy = GanttParserTests.Parse("gantt\nA :a, 2024-01-05, 1d\n  B :b, 2024-1-5, 1d\n");

        // Act
        var tasks = Compile(model);
        var exception = Assert.Throws<MermaidRenderException>(() => Compile(legacy));

        // Assert
        Assert.Equal(Date(2024, 1, 5), tasks[0].StartTime!.Value.Time);
        Assert.Equal(Date(2024, 1, 5) + (8 * 3_600_000), tasks[1].StartTime!.Value.Time);
        Assert.Equal(Date(2024, 3, 1), tasks[2].StartTime!.Value.Time);
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Contains("Invalid date:2024-1-5", exception.Message, StringComparison.Ordinal);
        Assert.Equal((3, 3), (exception.Location.Line, exception.Location.Column));
    }

    /// <summary>
    ///     Validates the failures where Mermaid fails or would draw unresolved dates: the first task without a start, a
    ///     task with four fields, excluded days that never end, and a circular reference.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="message">A part of the message.</param>
    [Theory]
    [InlineData("gantt\nA :3d\n", "(reading 'endTime')")]
    [InlineData("gantt\nA :a, b, 2024-01-01, 1d\n", "(reading 'type')")]
    [InlineData("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends monday tuesday wednesday thursday friday\nA :a, 2024-01-01, 2d\n", "Failed to find a valid date")]
    [InlineData("gantt\nA :a, after b, 1d\nB :b, after a, 1d\n", "cannot be resolved")]
    [InlineData("gantt\ndateFormat YYYY-MM-DD w\nexcludes monday\nA :a, 2024-01-01 1, 1d\n", "Invalid date")]
    public void GanttModel_Compile_Failures_AreLocatedRenderFailures(string text, string message)
    {
        // Arrange
        var model = GanttParserTests.Parse(text);

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => Compile(model));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, exception.Code);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
        Assert.True(exception.Location.IsKnown);
    }

    /// <summary>
    ///     Validates the JavaScript object semantics of ids: a repeated id finds the last task, <c>__proto__</c> is never
    ///     found (no link, no class), and a missing id is the key <c>undefined</c>.
    /// </summary>
    [Fact]
    public void GanttModel_FindTask_JavaScriptObjectSemantics()
    {
        // Arrange
        var model = GanttParserTests.Parse(
            "gantt\nA :x, 2024-01-01, 1d\nB :x, 2024-02-01, 1d\nP :__proto__, 2024-01-01, 1d\nU :undefined, 2024-01-01, 1d\nclick __proto__ href \"https://example.com\"\n");

        // Act
        var x = model.FindTask("x");
        var proto = model.FindTask("__proto__");
        var undefinedKey = model.FindTask(null);

        // Assert
        Assert.Equal("B ", x!.Task);
        Assert.Null(proto);
        Assert.Empty(model.Tasks[2].Classes);
        Assert.Empty(model.Links);
        Assert.Equal("U ", undefinedKey!.Task);
    }

    /// <summary>
    ///     Validates the pinned work charges: one unit per task per pass, one per <c>after</c> or <c>until</c> id, and one day
    ///     cost (1 + the date format's length + the excludes and includes) per day of the excluded-days loop.
    /// </summary>
    [Fact]
    public void GanttModel_Compile_WorkCharges_ArePinned()
    {
        // Arrange
        var plain = GanttParserTests.Parse("gantt\ndateFormat YYYY-MM-DD\nA :a, 2024-01-01, 3d\nB :b, after a, 2d\n");
        var excluded = GanttParserTests.Parse("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends\nA :a, 2024-01-01, 3d\nB :b, after a, 2d\n");
        var plainMeter = Meter(plain);
        var excludedMeter = Meter(excluded);

        // Act
        plain.Compile(new GanttClock(null, null), plainMeter);
        excluded.Compile(new GanttClock(null, null), excludedMeter);

        // Assert
        Assert.Equal(3, plainMeter.Used);
        Assert.Equal(87, excludedMeter.Used);
    }

    /// <summary>
    ///     Validates that a long excluded-days loop is bounded by the work limit with a located limit failure.
    /// </summary>
    [Fact]
    public void GanttModel_Compile_LongExcludedSpan_ExceedsWorkLimit()
    {
        // Arrange
        var model = GanttParserTests.Parse("gantt\ndateFormat YYYY-MM-DD\nexcludes weekends\nA :a, 2024-01-01, 2000y\n");
        var meter = new LayoutWorkMeter(100_000, model.KeywordLocation, CancellationToken.None);

        // Act
        var exception = Assert.Throws<MermaidRenderException>(() => model.Compile(new GanttClock(null, null), meter));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MaxLayoutWork", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates dayjs formatting: core and advanced tokens, brackets, padding, ordinals and ISO weeks, and the tokens
    ///     whose plugins Mermaid does not load.
    /// </summary>
    [Fact]
    public void Dayjs_Format_TokensAsTheBundledDayjs()
    {
        // Arrange
        var date = JsDate.FromComponents(2024, 0, 5, 9, 7, 3, 45);

        // Act
        var text = Dayjs.Format(date, "YYYY-MM-DD HH:mm:ss.SSS [at] dddd ddd dd MMMM MMM Do Q W kk h a A Z ZZ X", date);
        var empty = Dayjs.Format(date, string.Empty, date);

        // Assert
        Assert.Equal("2024-01-05 09:07:03.045 at Friday Fri Fr January Jan 5th 1 1 09 9 am AM +00:00 +0000 1704445623", text);
        Assert.Equal("2024-01-05T09:07:03+00:00", empty);
        Assert.Throws<NotSupportedException>(() => Dayjs.Format(date, "w", date));
        Assert.Equal("Invalid Date", Dayjs.Format(JsDate.Invalid, "w", date));
    }

    /// <summary>
    ///     Validates dayjs's strict parse: formats that match, the two-digit years, the meridiem, the fields taken from
    ///     today, and texts that do not format back.
    /// </summary>
    [Fact]
    public void Dayjs_ParseStrict_MatchesTheCustomParseFormatPlugin()
    {
        // Arrange
        var now = JsDate.FromComponents(2024, 5, 12, 10, 30);
        var usedNow = false;

        // Act
        var full = Dayjs.ParseStrict("05-01-2024", "DD-MM-YYYY", now, ref usedNow);
        var twoDigit = Dayjs.ParseStrict("69-01-08", "YY-MM-DD", now, ref usedNow);
        var meridiem = Dayjs.ParseStrict("04:30 pm", "hh:mm a", now, ref usedNow);
        var loose = Dayjs.ParseStrict("2024-1-5", "YYYY-MM-DD", now, ref usedNow);
        var longDate = Dayjs.ParseStrict("01/05/2024", "L", now, ref usedNow);

        // Assert
        Assert.Equal(Date(2024, 1, 5), full.Time);
        Assert.Equal(Date(1969, 1, 8), twoDigit.Time);
        Assert.Equal(Date(2024, 6, 12) + (16.5 * 3_600_000), meridiem.Time);
        Assert.True(usedNow);
        Assert.False(loose.IsValid);
        Assert.False(longDate.IsValid);
    }

    /// <summary>
    ///     Validates JavaScript's <c>Number(text)</c> and the ECMAScript date strings.
    /// </summary>
    [Fact]
    public void Dayjs_NumberAndDateStrings_FollowJavaScript()
    {
        // Act and Assert
        Assert.Equal(0, Dayjs.ToNumber("  "));
        Assert.Equal(16, Dayjs.ToNumber("0x10"));
        Assert.Equal(1000, Dayjs.ToNumber(" 1e3 "));
        Assert.True(double.IsNaN(Dayjs.ToNumber("12px")));
        Assert.Equal(Date(2024, 1, 1), Dayjs.ParseDateString("2024").Time);
        Assert.Equal(Date(2024, 1, 6), Dayjs.ParseDateString("2024-01-05T24:00").Time);
        Assert.False(Dayjs.ParseDateString("2024-01-05T10").IsValid);
        Assert.False(Dayjs.ParseDateString("-000000-01-01").IsValid);
        Assert.Equal(-1, Dayjs.DiffYears(JsDate.FromTime(Date(2023, 1, 1)), JsDate.FromTime(Date(2024, 6, 1))));
    }

    /// <summary>
    ///     Gets a UTC date's time in milliseconds.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month, from 1.</param>
    /// <param name="day">The day.</param>
    /// <returns>The time.</returns>
    private static double Date(int year, int month, int day) =>
        (new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero) - DateTimeOffset.UnixEpoch).TotalMilliseconds;

    /// <summary>
    ///     Compiles a model with the vectors' clock.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The tasks.</returns>
    private static List<GanttTask> Compile(GanttModel model) => GanttVectorTests.Compile(model);

    /// <summary>
    ///     Creates a work meter with the default limit.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The meter.</returns>
    private static LayoutWorkMeter Meter(GanttModel model) =>
        new(MermaidLimits.Default.MaxLayoutWork, model.KeywordLocation, CancellationToken.None);
}
