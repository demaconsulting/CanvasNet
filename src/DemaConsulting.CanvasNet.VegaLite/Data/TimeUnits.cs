// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     A Vega-Lite time unit (<c>yearmonth</c>, <c>utcmonthdate</c>, <c>{"unit": "month", "step": 3}</c>, ...) with the
///     floor, end and label rules that Vega gives it.
/// </summary>
/// <remarks>
///     <para>
///         The unit is the set of calendar parts it keeps (<see cref="Parts"/>, in Vega-Lite's order). <see cref="Floor"/>
///         is Vega's <c>timeFloor</c> / <c>utcFloor</c>: the parts below the unit are reset, a part the unit does not keep
///         above it takes the reference value (the year 2012, a leap year that starts on a Sunday, and January), and the
///         step applies to the last part only. <see cref="End"/> is the exclusive end of the bin: one step of the smallest
///         part after the start. <see cref="Specifier"/> is Vega's <c>timeUnitSpecifier</c> with the Vega-Lite overrides,
///         <see cref="FieldName"/> is Vega-Lite's <c>timeUnitToString</c> naming, and <see cref="ResolveMaxBins"/> is Vega's
///         <c>timeBin</c> for the <c>maxbins</c> form that names no unit.
///     </para>
///     <para>
///         The <c>binned*</c> units are parsed (<see cref="IsBinned"/>) but not applied here: the data of a binned unit is
///         already floored. A <c>utc</c> unit always uses the UTC calendar, whatever calendar is passed in.
///         <see cref="DurationMilliseconds"/> measures the smallest part from 2001-01-01 in UTC, which makes it independent
///         of the zone (Vega-Lite measures it in the local zone).
///     </para>
///     <para>
///         The type is immutable and safe for concurrent use. All methods are total: an instant that is out of range gives NaN.
///     </para>
/// </remarks>
internal sealed class TimeUnitSpec
{
    /// <summary>The year used when the unit keeps no year (a leap year that begins on a Sunday).</summary>
    private const double ReferenceYear = 2012;

    /// <summary>The milliseconds in a second, as Vega's bin table counts them.</summary>
    private const double DurationSecond = 1000;

    /// <summary>The milliseconds in a minute.</summary>
    private const double DurationMinute = DurationSecond * 60;

    /// <summary>The milliseconds in an hour.</summary>
    private const double DurationHour = DurationMinute * 60;

    /// <summary>The milliseconds in a day.</summary>
    private const double DurationDay = DurationHour * 24;

    /// <summary>The milliseconds in a week.</summary>
    private const double DurationWeek = DurationDay * 7;

    /// <summary>The milliseconds in a month of Vega's bin table (30 days).</summary>
    private const double DurationMonth = DurationDay * 30;

    /// <summary>The milliseconds in a year of Vega's bin table (365 days).</summary>
    private const double DurationYear = DurationDay * 365;

    /// <summary>The parts in Vega-Lite's order (<c>TIMEUNIT_PARTS</c>).</summary>
    private static readonly string[] PartOrder =
        ["year", "quarter", "month", "week", "day", "dayofyear", "date", "hours", "minutes", "seconds", "milliseconds"];

    /// <summary>The parts in Vega's order (<c>TIME_UNITS</c> without the ISO week), which decides the last part.</summary>
    private static readonly string[] VegaOrder =
        ["year", "quarter", "month", "week", "date", "day", "dayofyear", "hours", "minutes", "seconds", "milliseconds"];

    /// <summary>The local unit names of Vega-Lite: the 11 single and the 33 multi units.</summary>
    private static readonly HashSet<string> LocalUnits = new(StringComparer.Ordinal)
    {
        "year", "quarter", "month", "week", "day", "dayofyear", "date", "hours", "minutes", "seconds", "milliseconds",
        "yearquarter", "yearquartermonth",
        "yearmonth", "yearmonthdate", "yearmonthdatehours", "yearmonthdatehoursminutes", "yearmonthdatehoursminutesseconds",
        "yearweek", "yearweekday", "yearweekdayhours", "yearweekdayhoursminutes", "yearweekdayhoursminutesseconds",
        "yeardayofyear",
        "quartermonth",
        "monthdate", "monthdatehours", "monthdatehoursminutes", "monthdatehoursminutesseconds",
        "weekday", "weekdayhours", "weekdayhoursminutes", "weekdayhoursminutesseconds",
        "dayhours", "dayhoursminutes", "dayhoursminutesseconds",
        "hoursminutes", "hoursminutesseconds",
        "minutesseconds",
        "secondsmilliseconds",
    };

    /// <summary>The local unit names that have a <c>binned</c> form.</summary>
    private static readonly HashSet<string> BinnedUnits = new(StringComparer.Ordinal)
    {
        "year", "yearquarter", "yearquartermonth",
        "yearmonth", "yearmonthdate", "yearmonthdatehours", "yearmonthdatehoursminutes", "yearmonthdatehoursminutesseconds",
        "yearweek", "yearweekday", "yearweekdayhours", "yearweekdayhoursminutes", "yearweekdayhoursminutesseconds",
        "yeardayofyear",
    };

    /// <summary>The label specifiers of <c>timeUnitSpecifier</c>: Vega's defaults with the Vega-Lite overrides.</summary>
    private static readonly Dictionary<string, string> Specifiers = new(StringComparer.Ordinal)
    {
        ["year"] = "%Y ",
        ["quarter"] = "Q%q ",
        ["month"] = "%b ",
        ["date"] = "%d ",
        ["week"] = "W%U ",
        ["day"] = "%a ",
        ["dayofyear"] = "%j ",
        ["hours"] = "%H:00",
        ["minutes"] = "00:%M",
        ["seconds"] = ":%S",
        ["milliseconds"] = ".%L",
        ["year-month"] = "%b %Y ",
        ["year-month-date"] = "%b %d, %Y ",
        ["hours-minutes"] = "%H:%M",
    };

    /// <summary>The intervals of Vega's <c>timeBin</c> table in increasing duration: the parts, the step and the duration.</summary>
    private static readonly (string[] Parts, double Step, double Duration)[] BinIntervals =
    [
        (["year", "month", "date", "hours", "minutes", "seconds"], 1, DurationSecond),
        (["year", "month", "date", "hours", "minutes", "seconds"], 5, 5 * DurationSecond),
        (["year", "month", "date", "hours", "minutes", "seconds"], 15, 15 * DurationSecond),
        (["year", "month", "date", "hours", "minutes", "seconds"], 30, 30 * DurationSecond),
        (["year", "month", "date", "hours", "minutes"], 1, DurationMinute),
        (["year", "month", "date", "hours", "minutes"], 5, 5 * DurationMinute),
        (["year", "month", "date", "hours", "minutes"], 15, 15 * DurationMinute),
        (["year", "month", "date", "hours", "minutes"], 30, 30 * DurationMinute),
        (["year", "month", "date", "hours"], 1, DurationHour),
        (["year", "month", "date", "hours"], 3, 3 * DurationHour),
        (["year", "month", "date", "hours"], 6, 6 * DurationHour),
        (["year", "month", "date", "hours"], 12, 12 * DurationHour),
        (["year", "month", "date"], 1, DurationDay),
        (["year", "week"], 1, DurationWeek),
        (["year", "month"], 1, DurationMonth),
        (["year", "month"], 3, 3 * DurationMonth),
        (["year"], 1, DurationYear),
    ];

    /// <summary>The parts in Vega-Lite's order.</summary>
    private readonly string[] _parts;

    /// <summary>The parts in Vega's order; the last one is the unit the step and the end refer to.</summary>
    private readonly string[] _vegaUnits;

    /// <summary>The name used for derived fields, without the field.</summary>
    private readonly string _name;

    /// <summary>
    ///     Initializes a unit.
    /// </summary>
    /// <param name="parts">The parts in Vega-Lite's order.</param>
    /// <param name="utc">Whether the unit is a UTC unit.</param>
    /// <param name="binned">Whether the unit is a binned unit.</param>
    /// <param name="step">The step in units of the smallest part.</param>
    /// <param name="maxBins">The <c>maxbins</c> value, or null.</param>
    /// <param name="unit">The local unit name without prefixes, or null for the <c>maxbins</c> form.</param>
    /// <param name="name">The name used for derived fields.</param>
    private TimeUnitSpec(string[] parts, bool utc, bool binned, double step, double? maxBins, string? unit, string name)
    {
        _parts = parts;
        _vegaUnits = [.. parts.OrderBy(p => Array.IndexOf(VegaOrder, p))];
        Utc = utc;
        IsBinned = binned;
        Step = step;
        MaxBins = maxBins;
        Unit = unit;
        _name = name;
    }

    /// <summary>Gets the parts the unit keeps, in Vega-Lite's order (<c>year quarter month week day dayofyear date hours minutes seconds milliseconds</c>); empty for the <c>maxbins</c> form.</summary>
    public IReadOnlyList<string> Parts => _parts;

    /// <summary>Gets a value indicating whether the unit is a UTC unit (a <c>utc</c> prefix or <c>utc: true</c>).</summary>
    public bool Utc { get; }

    /// <summary>Gets a value indicating whether the unit is a <c>binned</c> unit.</summary>
    public bool IsBinned { get; }

    /// <summary>Gets the step in units of the smallest part (1 when not given).</summary>
    public double Step { get; }

    /// <summary>Gets the <c>maxbins</c> value of the object form, or null.</summary>
    public double? MaxBins { get; }

    /// <summary>Gets the local unit name without the <c>utc</c> and <c>binned</c> prefixes, or null for the <c>maxbins</c> form.</summary>
    public string? Unit { get; }

    /// <summary>Gets a value indicating whether the unit keeps exactly one part and is a local, not binned unit (the kind a bare number can stand for).</summary>
    public bool IsSingleLocalUnit => _parts.Length == 1 && !Utc && !IsBinned;

    /// <summary>
    ///     Reads a time unit written as text.
    /// </summary>
    /// <param name="name">A Vega-Lite time unit name, optionally with the <c>utc</c> prefix, or a <c>binned</c> name.</param>
    /// <param name="spec">The unit.</param>
    /// <param name="error">The reason when the name is not a time unit.</param>
    /// <returns>True when the name is a time unit.</returns>
    public static bool TryParse(string name, [NotNullWhen(true)] out TimeUnitSpec? spec, out string? error)
    {
        spec = null;
        error = null;
        var text = name;
        var binned = text.StartsWith("binned", StringComparison.Ordinal);
        if (binned)
        {
            text = text[6..];
        }

        var utc = text.StartsWith("utc", StringComparison.Ordinal);
        if (utc)
        {
            text = text[3..];
        }

        if (!(binned ? BinnedUnits : LocalUnits).Contains(text))
        {
            error = $"'{name}' is not a Vega-Lite time unit.";
            return false;
        }

        // The field name of a binned unit carries the binned flag, as Vega-Lite names it
        var core = (utc ? "utc" : string.Empty) + VarName(text) + (binned ? VarName("_binned_true") : string.Empty);
        spec = new TimeUnitSpec(GetParts(text), utc, binned, 1, null, text, core);
        return true;
    }

    /// <summary>
    ///     Reads a time unit written as an object: <c>{"unit": ..., "maxbins": ..., "step": ..., "utc": ..., "binned": ...}</c>.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="spec">The unit.</param>
    /// <param name="error">The reason when the object is not a time unit.</param>
    /// <returns>True when the object is a valid time unit.</returns>
    public static bool TryParse(SpecNode node, [NotNullWhen(true)] out TimeUnitSpec? spec, out string? error)
    {
        spec = null;
        error = null;
        if (!node.IsObject)
        {
            error = "A time unit must be a string or an object.";
            return false;
        }

        // The keys are read in document order because Vega-Lite builds the field name in that order
        string? unit = null;
        double? step = null;
        double? maxBins = null;
        var utc = false;
        var binned = false;
        var segments = new List<(string Key, string Value)>();
        foreach (var property in node.Properties)
        {
            if (!TryReadMember(property, segments, ref unit, ref step, ref maxBins, ref utc, ref binned, out error))
            {
                return false;
            }
        }

        if (unit is null && maxBins is null)
        {
            error = "A time unit object needs a 'unit' or a 'maxbins'.";
            return false;
        }

        // The name of the field: the unit and the other keys, or the 'timeunit' prefix when no unit is named
        var core = new StringBuilder(utc ? "utc" : string.Empty);
        if (unit is null)
        {
            core.Append("timeunit");
        }

        foreach (var (key, value) in segments)
        {
            core.Append(VarName(key == "unit" ? value : $"_{key}_{value}"));
        }

        spec = new TimeUnitSpec(unit is null ? [] : GetParts(unit), utc, binned, step ?? 1, maxBins, unit, core.ToString());
        return true;
    }

    /// <summary>
    ///     Builds the name of the field a unit derives from a field (Vega-Lite's <c>vgField</c> with a time unit).
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <returns>For example <c>yearmonth_date</c>, <c>utcmonth_x</c>, <c>month_step_2_date</c> or <c>timeunit_maxbins_10_d</c>.</returns>
    public string FieldName(string field) => _name + "_" + field;

    /// <summary>
    ///     Gets the Vega label specifier of the unit (<c>timeUnitSpecifier</c> with the Vega-Lite overrides).
    /// </summary>
    /// <returns>A d3 time format such as <c>%b %Y</c>, or null when the unit keeps no part (the <c>maxbins</c> form).</returns>
    public string? Specifier()
    {
        var units = _vegaUnits;
        if (units.Length == 0)
        {
            return null;
        }

        // Each step takes the longest run of parts that has a specifier of its own, as Vega does
        var format = new StringBuilder();
        var start = 0;
        while (start < units.Length)
        {
            var matched = false;
            for (var end = units.Length; end > start && !matched; end--)
            {
                if (Specifiers.TryGetValue(string.Join('-', units[start..end]), out var specifier))
                {
                    format.Append(specifier);
                    start = end;
                    matched = true;
                }
            }

            if (!matched)
            {
                start++;
            }
        }

        return format.ToString().Trim();
    }

    /// <summary>
    ///     Floors an instant to the start of its time unit (Vega's <c>timeFloor</c> and <c>utcFloor</c>).
    /// </summary>
    /// <param name="calendar">The calendar of local units; ignored by a UTC unit.</param>
    /// <param name="ms">The instant in milliseconds since the Unix epoch.</param>
    /// <returns>The start of the unit, or NaN for an invalid instant or when the unit keeps no part (resolve <c>maxbins</c> first).</returns>
    public double Floor(TimeCalendar calendar, double ms)
    {
        if (_vegaUnits.Length == 0)
        {
            return double.NaN;
        }

        var cal = Utc ? TimeCalendar.Utc : calendar;
        var p = cal.ToParts(ms);
        if (!p.IsValid)
        {
            return double.NaN;
        }

        // Year, month and day follow Vega's rules; the time-of-day parts are copied or reset
        var year = Has("year") ? Stepped("year", p.Year, 0) : ReferenceYear;
        return cal.FromParts(
            year,
            MonthPart(p),
            DayPart(p, year),
            Has("hours") ? Stepped("hours", p.Hours, 0) : 0,
            Has("minutes") ? Stepped("minutes", p.Minutes, 0) : 0,
            Has("seconds") ? Stepped("seconds", p.Seconds, 0) : 0,
            Has("milliseconds") ? Stepped("milliseconds", p.Milliseconds, 0) : 0);
    }

    /// <summary>
    ///     Gets the exclusive end of the bin that starts at an instant: one step of the smallest part later.
    /// </summary>
    /// <param name="calendar">The calendar of local units; ignored by a UTC unit.</param>
    /// <param name="startMs">The start of the bin, as returned by <see cref="Floor"/>.</param>
    /// <returns>The end, or NaN when out of range (a day is 23, 24 or 25 hours across a daylight saving change).</returns>
    public double End(TimeCalendar calendar, double startMs)
    {
        if (_vegaUnits.Length == 0)
        {
            return double.NaN;
        }

        // Vega offsets by its interval of the last unit: a quarter is three months, a date, a day and a day of the year are days
        var cal = Utc ? TimeCalendar.Utc : calendar;
        var (kind, multiplier) = _vegaUnits[^1] switch
        {
            "year" => (TimeIntervalKind.Year, 1.0),
            "quarter" => (TimeIntervalKind.Month, 3.0),
            "month" => (TimeIntervalKind.Month, 1.0),
            "week" => (TimeIntervalKind.Week, 1.0),
            "date" or "day" or "dayofyear" => (TimeIntervalKind.Day, 1.0),
            "hours" => (TimeIntervalKind.Hour, 1.0),
            "minutes" => (TimeIntervalKind.Minute, 1.0),
            "seconds" => (TimeIntervalKind.Second, 1.0),
            _ => (TimeIntervalKind.Millisecond, 1.0),
        };
        return cal.Interval(kind).Offset(startMs, Math.Floor(Step) * multiplier);
    }

    /// <summary>
    ///     Gets the length of one step of the smallest part, the minimum tick step of an axis (Vega-Lite's <c>durationExpr</c>).
    /// </summary>
    /// <returns>
    ///     The milliseconds between 2001-01-01 and one step of the smallest part later, measured in UTC (a month is 31
    ///     days, a year 365 days); null when the smallest part is the day of the week or the unit keeps no part.
    /// </returns>
    public double? DurationMilliseconds()
    {
        if (_parts.Length == 0 || _parts[^1] == "day")
        {
            return null;
        }

        // The smallest part moves one step from the first of January of a non-leap year
        double year = 2001;
        double month = 0;
        double date = 1;
        double hours = 0;
        double minutes = 0;
        double seconds = 0;
        double milliseconds = 0;
        switch (_parts[^1])
        {
            case "year":
                year += Step;
                break;
            case "quarter":
                month += 3 * Step;
                break;
            case "month":
                month += Step;
                break;
            case "week":
                date += 7 * Step;
                break;
            case "dayofyear" or "date":
                date += Step;
                break;
            case "hours":
                hours += Step;
                break;
            case "minutes":
                minutes += Step;
                break;
            case "seconds":
                seconds += Step;
                break;
            default:
                milliseconds += Step;
                break;
        }

        var start = TimeCalendar.Utc.FromParts(2001, 0, 1, 0, 0, 0, 0);
        var end = TimeCalendar.Utc.FromParts(year, month, date, hours, minutes, seconds, milliseconds);
        var duration = end - start;
        return double.IsFinite(duration) ? duration : null;
    }

    /// <summary>
    ///     Chooses the unit and the step of the <c>maxbins</c> form for a data extent (Vega's <c>timeBin</c>).
    /// </summary>
    /// <param name="minMs">The smallest instant of the data.</param>
    /// <param name="maxMs">The largest instant of the data.</param>
    /// <returns>
    ///     A unit that keeps the parts of the interval nearest to the extent divided by <see cref="MaxBins"/>, with the
    ///     step of that interval, or this unit when it already names a unit. A non-finite extent gives one year.
    /// </returns>
    public TimeUnitSpec ResolveMaxBins(double minMs, double maxMs)
    {
        if (MaxBins is null || _parts.Length > 0)
        {
            return this;
        }

        // The target bin width is the span divided by the bins; the table entry nearest to it (in ratio) wins
        var max = MaxBins.Value > 0 ? MaxBins.Value : 40;
        var target = Math.Abs(maxMs - minMs) / max;
        var index = BisectRight(target);
        string[] parts;
        double step;
        if (index == BinIntervals.Length)
        {
            // Wider than a year: whole years with a tick step
            parts = ["year"];
            step = TickStep(minMs / DurationYear, maxMs / DurationYear, max);
        }
        else if (index > 0)
        {
            var nearest = target / BinIntervals[index - 1].Duration < BinIntervals[index].Duration / target ? index - 1 : index;
            parts = BinIntervals[nearest].Parts;
            step = BinIntervals[nearest].Step;
        }
        else
        {
            // Narrower than a second: milliseconds with a tick step
            parts = ["year", "month", "date", "hours", "minutes", "seconds", "milliseconds"];
            step = Math.Max(TickStep(minMs, maxMs, max), 1);
        }

        return new TimeUnitSpec(parts, Utc, false, double.IsFinite(step) && step > 0 ? step : 1, MaxBins, null, _name);
    }

    /// <summary>
    ///     Replaces every character that is not an ASCII letter, digit or underscore (Vega-Lite's <c>varName</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The name, with a leading underscore when the text starts with a digit.</returns>
    internal static string VarName(string text)
    {
        var builder = new StringBuilder(text.Length + 1);
        if (text.Length > 0 && text[0] is >= '0' and <= '9')
        {
            builder.Append('_');
        }

        foreach (var c in text)
        {
            builder.Append(c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' ? c : '_');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Tells whether a full unit name contains a part (Vega-Lite's <c>containsTimeUnit</c>).
    /// </summary>
    /// <param name="full">The full unit name.</param>
    /// <param name="part">The part name.</param>
    /// <returns>True when the part is in the name; <c>seconds</c> inside <c>milliseconds</c>, <c>day</c> at the start of <c>dayofyear</c> and <c>year</c> at the end of <c>dayofyear</c> do not count.</returns>
    private static bool ContainsTimeUnit(string full, string part)
    {
        var index = full.IndexOf(part, StringComparison.Ordinal);
        if (index < 0)
        {
            return false;
        }

        // The three names that appear inside other names
        if (index > 0 && part == "seconds" && full[index - 1] == 'i')
        {
            return false;
        }

        if (full.Length > index + 3 && part == "day" && full[index + 3] == 'o')
        {
            return false;
        }

        return !(index > 0 && part == "year" && full[index - 1] == 'f');
    }

    /// <summary>
    ///     Lists the parts of a local unit name in Vega-Lite's order (<c>getTimeUnitParts</c>).
    /// </summary>
    /// <param name="unit">The local unit name.</param>
    /// <returns>The parts.</returns>
    private static string[] GetParts(string unit) => [.. PartOrder.Where(part => ContainsTimeUnit(unit, part))];

    /// <summary>
    ///     Reads one member of a time unit object.
    /// </summary>
    /// <param name="property">The member.</param>
    /// <param name="segments">The name segments in document order; extended by the members that name the field.</param>
    /// <param name="unit">The local unit name; set by <c>unit</c>.</param>
    /// <param name="step">The step; set by <c>step</c>.</param>
    /// <param name="maxBins">The bin count; set by <c>maxbins</c>.</param>
    /// <param name="utc">The UTC flag; set by <c>utc</c> or by a <c>utc</c> prefix of the unit.</param>
    /// <param name="binned">The binned flag; set by <c>binned</c>.</param>
    /// <param name="error">The reason when the member is invalid.</param>
    /// <returns>True when the member is valid.</returns>
    private static bool TryReadMember(
        SpecNode property,
        List<(string Key, string Value)> segments,
        ref string? unit,
        ref double? step,
        ref double? maxBins,
        ref bool utc,
        ref bool binned,
        out string? error)
    {
        error = null;
        switch (property.Name)
        {
            case "unit":
                if (!property.IsString)
                {
                    error = "The 'unit' of a time unit must be a string.";
                    return false;
                }

                var text = property.StringValue!;
                if (text.StartsWith("utc", StringComparison.Ordinal))
                {
                    utc = true;
                    text = text[3..];
                }

                if (!LocalUnits.Contains(text))
                {
                    error = $"'{property.StringValue}' is not a Vega-Lite time unit.";
                    return false;
                }

                unit = text;
                segments.Add(("unit", text));
                return true;
            case "step":
            case "maxbins":
                if (!property.IsNumber || !(property.NumberValue > 0))
                {
                    error = $"The '{property.Name}' of a time unit must be a positive number.";
                    return false;
                }

                if (property.Name == "step")
                {
                    step = property.NumberValue;
                }
                else
                {
                    maxBins = property.NumberValue;
                }

                segments.Add((property.Name, JsNumber.ToString(property.NumberValue)));
                return true;
            case "utc":
            case "binned":
                if (!property.IsBoolean)
                {
                    error = $"The '{property.Name}' of a time unit must be true or false.";
                    return false;
                }

                if (property.Name == "utc")
                {
                    utc |= property.BoolValue;
                }
                else
                {
                    binned = property.BoolValue;
                    segments.Add(("binned", property.BoolValue ? "true" : "false"));
                }

                return true;
            default:
                error = $"'{property.Name}' is not a property of a time unit.";
                return false;
        }
    }

    /// <summary>
    ///     Gets the first index of the bin table whose duration is greater than a target (a right bisection).
    /// </summary>
    /// <param name="target">The target duration; NaN gives the table length.</param>
    /// <returns>The index.</returns>
    private static int BisectRight(double target)
    {
        var low = 0;
        var high = BinIntervals.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (BinIntervals[middle].Duration > target)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }

    /// <summary>
    ///     The d3-array <c>tickStep</c>: a step of 1, 2 or 5 times a power of ten that gives about the requested number of ticks.
    /// </summary>
    /// <param name="start">The start of the extent.</param>
    /// <param name="stop">The end of the extent.</param>
    /// <param name="count">The requested number of ticks.</param>
    /// <returns>The step; negative when the extent is reversed.</returns>
    /// <remarks>
    ///     A private copy keeps the Data namespace free of a dependency on the Scales namespace, which owns the same
    ///     function for axes (<c>Ticks.TickStep</c>).
    /// </remarks>
    private static double TickStep(double start, double stop, double count)
    {
        var reverse = stop < start;
        var increment = reverse ? TickIncrement(stop, start, count) : TickIncrement(start, stop, count);
        return (reverse ? -1 : 1) * (increment < 0 ? 1 / -increment : increment);
    }

    /// <summary>
    ///     The d3-array <c>tickIncrement</c>: the step, or the negative reciprocal of it for a fractional step.
    /// </summary>
    /// <param name="low">The start of the extent.</param>
    /// <param name="high">The end of the extent, not before the start.</param>
    /// <param name="count">The requested number of ticks.</param>
    /// <returns>The increment.</returns>
    private static double TickIncrement(double low, double high, double count)
    {
        while (true)
        {
            var step = (high - low) / Math.Max(0, count);
            var power = Math.Floor(Math.Log10(step));
            var error = step / Math.Pow(10, power);
            double factor;
            if (error >= Math.Sqrt(50))
            {
                factor = 10;
            }
            else if (error >= Math.Sqrt(10))
            {
                factor = 5;
            }
            else
            {
                factor = error >= Math.Sqrt(2) ? 2 : 1;
            }

            double first;
            double last;
            double increment;
            if (power < 0)
            {
                increment = Math.Pow(10, -power) / factor;
                first = JsNumber.Round(low * increment);
                last = JsNumber.Round(high * increment);
                if (first / increment < low)
                {
                    first++;
                }

                if (last / increment > high)
                {
                    last--;
                }

                increment = -increment;
            }
            else
            {
                increment = Math.Pow(10, power) * factor;
                first = JsNumber.Round(low / increment);
                last = JsNumber.Round(high / increment);
                if (first * increment < low)
                {
                    first++;
                }

                if (last * increment > high)
                {
                    last--;
                }
            }

            // A request for less than two ticks that finds none is retried with twice the count
            if (last < first && count >= 0.5 && count < 2)
            {
                count *= 2;
                continue;
            }

            return increment;
        }
    }

    /// <summary>
    ///     Tells whether the unit keeps a part, in Vega's naming.
    /// </summary>
    /// <param name="unit">The part name.</param>
    /// <returns>True when the part is kept.</returns>
    private bool Has(string unit) => Array.IndexOf(_vegaUnits, unit) >= 0;

    /// <summary>
    ///     Applies the step to a part when it is the last part of the unit (Vega's <c>getUnit</c>).
    /// </summary>
    /// <param name="unit">The part the value belongs to.</param>
    /// <param name="value">The value of the part.</param>
    /// <param name="phase">The first value of the part: 1 for the day parts, 0 otherwise.</param>
    /// <returns>The value floored to a multiple of the step counted from the phase, or the value itself for any other part or a step of 1 or less.</returns>
    private double Stepped(string unit, double value, double phase)
    {
        if (!(Step > 1) || unit != _vegaUnits[^1])
        {
            return value;
        }

        return phase != 0
            ? phase + (Step * Math.Floor((value - phase) / Step))
            : Step * Math.Floor(value / Step);
    }

    /// <summary>
    ///     Computes the month the floor starts in: the month (or three times the quarter), or January.
    /// </summary>
    /// <param name="p">The parts of the instant.</param>
    /// <returns>The month from 0.</returns>
    private double MonthPart(DateParts p)
    {
        if (Has("month"))
        {
            return Stepped("month", p.Month, 0);
        }

        return Has("quarter") ? 3 * Stepped("quarter", Math.Floor(p.Month / 3.0), 0) : 0;
    }

    /// <summary>
    ///     Computes the day of the month the floor starts in: from the week and day of the week, the date, the day of the year, or the first.
    /// </summary>
    /// <param name="p">The parts of the instant.</param>
    /// <param name="year">The year of the floor, which decides the weekday the weeks are counted from.</param>
    /// <returns>The day of the month, possibly outside 1 to 31 (the calendar rolls it into the neighboring month).</returns>
    private double DayPart(DateParts p, double year)
    {
        var first = TimeCalendar.FirstWeekdayOfYear(year);
        if (Has("week") && Has("day"))
        {
            return Stepped("day", Weekday(WeekNumber(p), p.Day, first), 1);
        }

        if (Has("week"))
        {
            return Weekday(Stepped("week", WeekNumber(p), 1), 0, first);
        }

        if (Has("day"))
        {
            return Stepped("day", Weekday(1, p.Day, first), 1);
        }

        if (Has("date"))
        {
            return Stepped("date", p.Date, 1);
        }

        return Has("dayofyear") ? Stepped("dayofyear", p.DayOfYear, 1) : 1;
    }

    /// <summary>
    ///     Gets Vega's week number: the number of Sundays from January 1 up to and including the date.
    /// </summary>
    /// <param name="p">The parts of the instant.</param>
    /// <returns>0 before the first Sunday of the year, 1 from the first Sunday on, and so on.</returns>
    private static double WeekNumber(DateParts p)
    {
        // The weekday of January 1 follows from the weekday of the date and the days since
        var firstWeekday = ((p.Day - (p.DayOfYear - 1)) % 7 + 7) % 7;
        return ((p.DayOfYear - 1 + firstWeekday) / 7) + (firstWeekday == 0 ? 1 : 0);
    }

    /// <summary>
    ///     Computes the day of the month of a day of a week (Vega's <c>weekday</c>).
    /// </summary>
    /// <param name="week">The week number.</param>
    /// <param name="day">The day of the week, 0 for Sunday.</param>
    /// <param name="firstDay">The weekday of January 1.</param>
    /// <returns>The day of January that the weekday of the week falls on; zero or negative values lie in the previous December.</returns>
    private static double Weekday(double week, double day, int firstDay) => day + (week * 7) - ((firstDay + 6) % 7);
}
