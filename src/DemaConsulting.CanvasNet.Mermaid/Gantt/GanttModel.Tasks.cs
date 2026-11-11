// Portions derived from mermaid (MIT) and dayjs (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     A task with its dates, as Mermaid's <c>getTasks</c> gives it to the renderer; one render's own copy.
/// </summary>
internal sealed class GanttTask
{
    /// <summary>
    ///     Initializes a task from its raw task.
    /// </summary>
    /// <param name="raw">The raw task.</param>
    public GanttTask(GanttRawTask raw)
    {
        Raw = raw;
        Order = raw.Order;
    }

    /// <summary>Gets the raw task.</summary>
    public GanttRawTask Raw { get; }

    /// <summary>Gets the id (null when the data has more than three fields).</summary>
    public string? Id => Raw.Id;

    /// <summary>Gets the name as written.</summary>
    public string Task => Raw.Task;

    /// <summary>Gets the section (Mermaid's <c>type</c>).</summary>
    public string Section => Raw.Section;

    /// <summary>Gets or sets the start, or null while it is undefined.</summary>
    public JsDate? StartTime { get; set; }

    /// <summary>Gets or sets the end, or null while it is undefined.</summary>
    public JsDate? EndTime { get; set; }

    /// <summary>Gets or sets the end of the drawn bar when excluded days moved the end, or null.</summary>
    public JsDate? RenderEndTime { get; set; }

    /// <summary>Gets or sets a value indicating whether the dates were resolved once.</summary>
    public bool Processed { get; set; }

    /// <summary>Gets or sets a value indicating whether the end is a date in <c>YYYY-MM-DD</c> (excluded days do not move it).</summary>
    public bool ManualEndTime { get; set; }

    /// <summary>Gets or sets the row (the compact mode assigns rows again).</summary>
    public double Order { get; set; }
}

/// <content>
///     The compilation of the task dates: Mermaid's <c>getTasks</c> (<c>compileTasks</c> up to eleven passes,
///     <c>getStartDate</c>, <c>getEndDate</c>, <c>parseDuration</c>, <c>checkTaskDates</c>, <c>fixTaskDates</c> and
///     <c>isInvalidDate</c>), with today from the render options.
/// </content>
internal sealed partial class GanttModel
{
    /// <summary>The first day of the weekend, as an ISO weekday (Mermaid's <c>WEEKEND_START_DAY</c>).</summary>
    private static readonly Dictionary<string, double> WeekendStartDay = new(StringComparer.Ordinal)
    {
        ["friday"] = 5,
        ["saturday"] = 6,
    };

    /// <summary>
    ///     Compiles the task dates of a render as Mermaid's <c>getTasks</c> does: up to eleven passes over the tasks, so that
    ///     a task may refer to tasks after it.
    /// </summary>
    /// <param name="clock">The clock of the render.</param>
    /// <param name="meter">The work meter of the render.</param>
    /// <returns>The tasks in the order of the text, each with its dates.</returns>
    /// <exception cref="MermaidRenderException">
    ///     Thrown, located at the task, where Mermaid fails (an invalid date, the first task without a start, a task with
    ///     more than three fields, excluded days that never end, a date format that needs a missing plugin), when a task's
    ///     dates are still unresolved after the passes (a circular reference) or invalid, and when the work limit is
    ///     exceeded.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public List<GanttTask> Compile(GanttClock clock, LayoutWorkMeter meter)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(meter);
        var tasks = _tasks.Select(raw => new GanttTask(raw)).ToList();
        var compiler = new Compiler(this, tasks, clock, meter);
        var all = compiler.CompileTasks();
        for (var pass = 0; !all && pass < 10; pass++)
        {
            all = compiler.CompileTasks();
        }

        // Mermaid draws NaN geometry for what is still unresolved or invalid; the port fails there instead
        foreach (var task in tasks)
        {
            if (!task.Processed || task.StartTime is not { IsValid: true } || task.EndTime is not { IsValid: true })
            {
                throw new MermaidRenderException(
                    MermaidDiagnosticCodes.RenderFailed,
                    task.Processed
                        ? $"the task '{Preprocessor.TrimJs(task.Task)}' has an invalid date"
                        : $"the start or end of the task '{Preprocessor.TrimJs(task.Task)}' cannot be resolved",
                    task.Raw.Location,
                    task.Processed
                        ? "check the task's dates against the dateFormat"
                        : "remove the circular after or until references, or give the task a date");
            }
        }

        return tasks;
    }

    /// <summary>
    ///     Gets whether a date is excluded (Mermaid's <c>isInvalidDate</c>): not an included date, and a weekend day, an
    ///     excluded weekday name or an excluded date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="clock">The clock of the render.</param>
    /// <param name="meter">The work meter of the render, charged for the formatting of the date.</param>
    /// <returns>Whether it is excluded.</returns>
    /// <exception cref="NotSupportedException">Thrown when the date format needs a plugin Mermaid does not load.</exception>
    /// <exception cref="MermaidRenderException">Thrown when the work limit is exceeded.</exception>
    internal bool IsInvalidDate(JsDate date, GanttClock clock, LayoutWorkMeter meter)
    {
        var formatted = Dayjs.Format(date, Preprocessor.TrimJs(DateFormat), clock.Today, meter);
        var dateOnly = Dayjs.Format(date, "YYYY-MM-DD", clock.Today, meter);
        if (_includes.Contains(formatted) || _includes.Contains(dateOnly))
        {
            return false;
        }

        var weekend = WeekendStartDay[Weekend];
        var isoWeekday = Dayjs.IsoWeekday(date);
        if (_excludes.Contains("weekends") && ((int)isoWeekday == (int)weekend || (int)isoWeekday == (int)weekend + 1))
        {
            return true;
        }

        return _excludes.Contains(Dayjs.Format(date, "dddd", clock.Today, meter).ToLowerInvariant())
            || _excludes.Contains(formatted)
            || _excludes.Contains(dateOnly);
    }

    /// <summary>
    ///     Gets the work of one day of the excluded-days loops besides the formatting, which <see cref="IsInvalidDate"/>
    ///     charges itself: one unit, the trim of the date format, and the searches of the excludes and includes lists,
    ///     each of which compares a text with every token of the list at most up to the token's length.
    /// </summary>
    /// <returns>The work units.</returns>
    internal long DayCost() =>
        1 + DateFormat.Length + _excludes.Sum(token => 1L + token.Length) + _includes.Sum(token => 1L + token.Length);

    /// <summary>
    ///     Parses a duration (Mermaid's <c>parseDuration</c>: <c>/^(\d+(?:\.\d+)?)([Mdhmswy]|ms)$/</c> on the trimmed text).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The amount and the unit, or NaN and <c>ms</c> for a text that is no duration.</returns>
    internal static (double Value, string Unit) ParseDuration(string text)
    {
        var s = Preprocessor.TrimJs(text);
        var i = 0;
        while (i < s.Length && char.IsAsciiDigit(s[i]))
        {
            i++;
        }

        var integer = i;
        if (integer > 0 && i + 1 < s.Length && s[i] == '.' && char.IsAsciiDigit(s[i + 1]))
        {
            i++;
            while (i < s.Length && char.IsAsciiDigit(s[i]))
            {
                i++;
            }
        }

        var unit = s[i..];
        return integer > 0 && unit is "M" or "d" or "h" or "m" or "s" or "w" or "y" or "ms"
            ? (double.Parse(s.AsSpan(0, i), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture), unit)
            : (double.NaN, "ms");
    }

    /// <summary>
    ///     Reads the ids of an <c>after</c> or <c>until</c> reference (<c>/^word\s+(?&lt;ids&gt;[\d\w- ]+)/</c>, the ids
    ///     then split on single spaces).
    /// </summary>
    /// <param name="text">The trimmed text.</param>
    /// <param name="word">The word, <c>after</c> or <c>until</c> (in lower case only).</param>
    /// <returns>The ids, or null when the text is no such reference.</returns>
    internal static string[]? ReferenceIds(string text, string word)
    {
        if (!text.StartsWith(word, StringComparison.Ordinal))
        {
            return null;
        }

        var spaces = word.Length;
        var end = spaces;
        while (end < text.Length && ScanPatterns.IsJsWhitespace(text[end]))
        {
            end++;
        }

        if (end == spaces)
        {
            return null;
        }

        static bool IsIdChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or ' ';
        var ids = end;
        while (ids < text.Length && IsIdChar(text[ids]))
        {
            ids++;
        }

        if (ids > end)
        {
            return text[end..ids].Split(' ');
        }

        // The white space gives back its last space to the ids (the regular expression backtracks)
        for (var k = end - 1; k > spaces; k--)
        {
            if (text[k] == ' ')
            {
                return text[k..(k + 1)].Split(' ');
            }
        }

        return null;
    }

    /// <summary>
    ///     The state of one compilation: the render's tasks, its clock and its work meter.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="tasks">The render's tasks, in the order of the model's.</param>
    /// <param name="clock">The clock of the render.</param>
    /// <param name="meter">The work meter of the render.</param>
    private sealed class Compiler(GanttModel model, List<GanttTask> tasks, GanttClock clock, LayoutWorkMeter meter)
    {
        /// <summary>The work of one day of the excluded-days loop besides its formatting, the same for every task.</summary>
        private readonly long _dayCost = model.DayCost();

        /// <summary>
        ///     Runs one pass over the tasks (<c>compileTasks</c>).
        /// </summary>
        /// <returns>Whether every task is processed.</returns>
        public bool CompileTasks()
        {
            var all = true;
            foreach (var task in tasks)
            {
                meter.Charge(1);
                all &= CompileTask(task);
            }

            return all;
        }

        /// <summary>
        ///     Compiles one task (<c>compileTask</c>).
        /// </summary>
        /// <param name="task">The task.</param>
        /// <returns>Whether it is processed.</returns>
        private bool CompileTask(GanttTask task)
        {
            var raw = task.Raw;
            switch (raw.StartKind)
            {
                case GanttStartKind.None:
                    throw Failure(task, "Mermaid fails on a task with more than three data fields (Cannot read properties of undefined (reading 'type'))", "write a task's data as at most an id, a start and an end or duration, separated by commas, after its tags");
                case GanttStartKind.PreviousTaskEnd:
                    {
                        var index = model.IndexOf(raw.PreviousTaskId);
                        if (index < 0)
                        {
                            throw Failure(task, "Mermaid fails on a task without a start that has no task before it (Cannot read properties of undefined (reading 'endTime'))", "give the first task a start date, such as ':a1, 2024-01-01, 3d'");
                        }

                        task.StartTime = tasks[index].EndTime;
                        break;
                    }

                default:
                    if (GetStartDate(task, raw.StartData!) is { } start)
                    {
                        task.StartTime = start;
                    }

                    break;
            }

            if (task.StartTime is { } startTime)
            {
                task.EndTime = GetEndDate(task, startTime, raw.EndData!);
                if (task.EndTime is not null)
                {
                    task.Processed = true;
                    var unused = false;
                    task.ManualEndTime = Strict(task, raw.EndData!, "YYYY-MM-DD", ref unused).IsValid;
                    CheckTaskDates(task);
                }
            }

            return task.Processed;
        }

        /// <summary>
        ///     Gets the start of a task (<c>getStartDate</c>): a timestamp, the latest end of <c>after</c> tasks, a date in
        ///     the date format, or a date in the ECMAScript format.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="data">The start data.</param>
        /// <returns>The start, or null when an <c>after</c> task has no end yet.</returns>
        private JsDate? GetStartDate(GanttTask task, string data)
        {
            var text = Preprocessor.TrimJs(data);
            var format = Preprocessor.TrimJs(model.DateFormat);

            // Mermaid's fast path: digits alone are milliseconds for x and for X alike (new Date(Number(str))), while an
            // end in X goes through dayjs, which reads seconds; the two units differ in Mermaid too
            if (format is "x" or "X" && text.Length > 0 && text.All(char.IsAsciiDigit))
            {
                meter.Charge(text.Length);
                return JsDate.FromTime(Dayjs.ToNumber(text));
            }

            if (ReferenceIds(text, "after") is { } ids)
            {
                GanttTask? latest = null;
                foreach (var id in ids)
                {
                    meter.Charge(1);
                    var index = model.IndexOf(id);
                    if (index >= 0 && (latest is null || Time(tasks[index].EndTime) > Time(latest.EndTime)))
                    {
                        latest = tasks[index];
                    }
                }

                return latest is not null ? latest.EndTime : clock.TodayAtMidnight(task.Raw.Location, $"the after reference '{text}' names no task");
            }

            var usedNow = false;
            var date = Strict(task, text, format, ref usedNow);
            if (date.IsValid)
            {
                clock.NoteTodayUsed(usedNow, task.Raw.Location, text);
                return date;
            }

            var fallback = Dayjs.ParseDateString(text);
            if (!fallback.IsValid || fallback.FullYear < -10000 || fallback.FullYear > 10000)
            {
                throw Failure(task, "Invalid date:" + text, "write the date as the dateFormat says, or as YYYY-MM-DD without a dateFormat (other date texts that browsers read are not supported)");
            }

            return fallback;
        }

        /// <summary>
        ///     Gets the end of a task (<c>getEndDate</c>): the earliest start of <c>until</c> tasks, a date in the date format
        ///     (a day later with inclusive end dates), or the start plus a duration (the start for a text that is no
        ///     duration).
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="start">The start.</param>
        /// <param name="data">The end data.</param>
        /// <returns>The end, or null when an <c>until</c> task has no start yet.</returns>
        private JsDate? GetEndDate(GanttTask task, JsDate start, string data)
        {
            var text = Preprocessor.TrimJs(data);
            if (ReferenceIds(text, "until") is { } ids)
            {
                GanttTask? earliest = null;
                foreach (var id in ids)
                {
                    meter.Charge(1);
                    var index = model.IndexOf(id);
                    if (index >= 0 && (earliest is null || Time(tasks[index].StartTime) < Time(earliest.StartTime)))
                    {
                        earliest = tasks[index];
                    }
                }

                return earliest is not null ? earliest.StartTime : clock.TodayAtMidnight(task.Raw.Location, $"the until reference '{text}' names no task");
            }

            var usedNow = false;
            var date = Strict(task, text, Preprocessor.TrimJs(model.DateFormat), ref usedNow);
            if (date.IsValid)
            {
                clock.NoteTodayUsed(usedNow, task.Raw.Location, text);
                return model.InclusiveEndDates ? Dayjs.Add(date, 1, "d") : date;
            }

            var (value, unit) = ParseDuration(text);
            if (!double.IsNaN(value))
            {
                var end = Dayjs.Add(start, value, unit);
                if (end.IsValid)
                {
                    return end;
                }
            }

            return start;
        }

        /// <summary>
        ///     Moves the end of a task past excluded days (<c>checkTaskDates</c> and <c>fixTaskDates</c>): each excluded day
        ///     from the day after the start to the end moves the end a day later, and the end of the drawn bar is the last
        ///     end before an excluded day.
        /// </summary>
        /// <param name="task">The task.</param>
        private void CheckTaskDates(GanttTask task)
        {
            if (model._excludes.Count == 0 || task.ManualEndTime)
            {
                return;
            }

            var start = Dayjs.Add(task.StartTime!.Value, 1, "d");
            var end = task.EndTime!.Value;
            var maxEnd = Dayjs.Add(end, 10000, "d");
            var invalid = false;
            JsDate? renderEnd = null;
            while (start.Time <= end.Time)
            {
                meter.Charge(_dayCost);
                if (!invalid)
                {
                    renderEnd = end;
                }

                invalid = Excluded(task, start);
                if (invalid)
                {
                    end = Dayjs.Add(end, 1, "d");
                    if (end.Time > maxEnd.Time)
                    {
                        throw Failure(task, "Failed to find a valid date that was not excluded by `excludes` after 10,000 iterations.", "leave at least one day of the week out of excludes, or shorten the task: its excluded days may move its end by at most 10,000 days");
                    }
                }

                start = Dayjs.Add(start, 1, "d");
            }

            task.EndTime = end;
            task.RenderEndTime = renderEnd;
        }

        /// <summary>
        ///     Gets whether a day is excluded, failing at the task where the date format needs a missing plugin.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="date">The day.</param>
        /// <returns>Whether it is excluded.</returns>
        private bool Excluded(GanttTask task, JsDate date)
        {
            try
            {
                return model.IsInvalidDate(date, clock, meter);
            }
            catch (NotSupportedException ex)
            {
                throw Failure(task, ex.Message, "remove the week (w, ww, wo, gggg) and zone name (z, zzz) tokens from the dateFormat");
            }
        }

        /// <summary>
        ///     Parses a date strictly, charging the parse to the meter, and failing at the task where the date format needs
        ///     a missing plugin.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="text">The text.</param>
        /// <param name="format">The format.</param>
        /// <param name="usedNow">Set when the date took a part of today.</param>
        /// <returns>The date, or the invalid date.</returns>
        private JsDate Strict(GanttTask task, string text, string format, ref bool usedNow)
        {
            try
            {
                return Dayjs.ParseStrict(text, format, clock.Today, ref usedNow, meter);
            }
            catch (NotSupportedException ex)
            {
                throw Failure(task, ex.Message, "remove the week (w, ww, wo, gggg) and zone name (z, zzz) tokens from the dateFormat");
            }
        }

        /// <summary>
        ///     Gets the time of an optional date for a JavaScript comparison (undefined is NaN).
        /// </summary>
        /// <param name="date">The date, or null.</param>
        /// <returns>The time value, or NaN.</returns>
        private static double Time(JsDate? date) => date?.Time ?? double.NaN;

        /// <summary>
        ///     Creates the failure of a task's dates.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="message">The message.</param>
        /// <param name="hint">The hint.</param>
        /// <returns>The located exception.</returns>
        private static MermaidRenderException Failure(GanttTask task, string message, string hint) =>
            new(MermaidDiagnosticCodes.RenderFailed, message, task.Raw.Location, hint);
    }
}

/// <summary>
///     The clock of one gantt render: today from <see cref="MermaidRenderOptions.Now"/> (in UTC), or 1970-01-01 when it is
///     not set, with the warning that today is not known reported once.
/// </summary>
/// <remarks>
///     An instance belongs to one render on one thread.
/// </remarks>
internal sealed class GanttClock
{
    /// <summary>The report sink of the render, or null.</summary>
    private readonly MermaidReportSink? _sink;

    /// <summary>Whether the warning was reported.</summary>
    private bool _warned;

    /// <summary>
    ///     Initializes the clock of a render.
    /// </summary>
    /// <param name="now">The time of the render options, or null when it is not set.</param>
    /// <param name="sink">The report sink, or null.</param>
    public GanttClock(DateTimeOffset? now, MermaidReportSink? sink)
    {
        _sink = sink;
        IsKnown = now is not null;
        Today = now is { } value
            ? JsDate.FromTime((value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / (double)TimeSpan.TicksPerMillisecond)
            : JsDate.FromTime(0);
    }

    /// <summary>Gets a value indicating whether today is known (the options set it).</summary>
    public bool IsKnown { get; }

    /// <summary>Gets the time taken as now (<c>new Date()</c>): the options' time, or 1970-01-01T00:00Z.</summary>
    public JsDate Today { get; }

    /// <summary>
    ///     Gets today at midnight (<c>new Date()</c> with <c>setHours(0, 0, 0, 0)</c>), reporting once when today is not
    ///     known.
    /// </summary>
    /// <param name="location">The location of what needs today.</param>
    /// <param name="what">What needs today, for the message.</param>
    /// <returns>Today at midnight.</returns>
    public JsDate TodayAtMidnight(MermaidLocation location, string what)
    {
        Warn(location, what + ", so Mermaid takes today");
        return Dayjs.StartOfDay(Today);
    }

    /// <summary>
    ///     Reports once, when today is not known, that a date took a part of today.
    /// </summary>
    /// <param name="used">Whether the date took a part of today.</param>
    /// <param name="location">The location of the date.</param>
    /// <param name="text">The date text.</param>
    public void NoteTodayUsed(bool used, MermaidLocation location, string text)
    {
        if (used)
        {
            Warn(location, $"the date '{text}' has no year, month or day, so Mermaid takes them from today");
        }
    }

    /// <summary>
    ///     Reports once that today is not known.
    /// </summary>
    /// <param name="location">The location of what needs today.</param>
    /// <param name="what">What needs today.</param>
    public void Warn(MermaidLocation location, string what)
    {
        if (IsKnown || _warned)
        {
            return;
        }

        _warned = true;
        _sink?.Warn(
            MermaidDiagnosticCodes.GanttTodayUnavailable,
            what + "; MermaidRenderOptions.Now is not set, so 1970-01-01 is taken as today",
            location,
            "set MermaidRenderOptions.Now to the time the chart is drawn for, or give the dates in full");
    }
}
