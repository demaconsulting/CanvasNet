// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The date and time functions of the closed function table (a partial file of the functions unit).
/// </summary>
/// <remarks>
///     <para>
///         The functions are those of the Vega expression language: the constructors <c>datetime</c> and <c>utc</c>, the
///         getters (<c>year</c>, <c>month</c>, <c>date</c>, <c>day</c>, <c>hours</c>, <c>minutes</c>, <c>seconds</c>,
///         <c>milliseconds</c>, <c>time</c>, <c>timezoneoffset</c>, <c>quarter</c>, <c>week</c>, <c>isoweek</c>,
///         <c>dayofyear</c> and the <c>utc</c> forms of the getters), <c>timeOffset</c> and <c>timeSequence</c>
///         (<c>utcOffset</c>, <c>utcSequence</c>), <c>timeUnitSpecifier</c>, <c>timeFormat</c>, <c>timeParse</c>
///         (<c>utcFormat</c>, <c>utcParse</c>), the name formatters (<c>monthFormat</c>, <c>monthAbbrevFormat</c>,
///         <c>dayFormat</c>, <c>dayAbbrevFormat</c>) and, with the type functions, <c>toDate</c>. <c>now</c> is not in the
///         table: the clock is not an input of a pure expression, so a call is reported and gives null.
///     </para>
///     <para>
///         A date is an instant in whole milliseconds since the Unix epoch, <see cref="DataValue.FromDate"/> in the data
///         model. The local forms use the calendar of the evaluation (<see cref="ExpressionFunctionContext.Calendar"/>: UTC
///         unless the render was given a time zone), never the host's zone. Where JavaScript gives an <c>Invalid Date</c> the
///         functions give null for a date result and NaN for a number; the valid range is the years 1 to 9999 (see
///         <see cref="TimeCalendar"/>). An instant argument may be a date, a number of milliseconds or a text that
///         <see cref="JsDateParser"/> reads; null, booleans, arrays and objects are not instants (JavaScript would read null
///         as the epoch, which would hide a missing value).
///     </para>
///     <para>
///         Work is bounded: a date text is cut off at 128 characters, a format or parse is charged by the length of the
///         specifier and the text, a specifier is at most <see cref="TimeFormat.MaxSpecifierLength"/> characters and
///         <see cref="TimeFormat.MaxTokenCount"/> pieces (a longer one is a runtime error, as is a longer override of a
///         multi-format), a formatted text is limited by <see cref="ExpressionFunctionContext.MaxStringLength"/>,
///         and a sequence holds at most <see cref="ExpressionFunctionContext.MaxArrayLength"/> dates and costs one step for
///         each of them. All results are culture invariant (the English names of the d3 locale, ASCII digits).
///     </para>
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>The longest unit name that is looked at (the longest real name is <c>milliseconds</c>).</summary>
    private const int MaxUnitNameLength = 16;

    /// <summary>The year of the reference dates of the name formatters.</summary>
    private const double NameReferenceYear = 2000;

    /// <summary>The most unit names an array may hold before it cannot be a valid list of Vega time units.</summary>
    private const int MaxUnitNames = 12;

    /// <summary>
    ///     Adds the date and time functions.
    /// </summary>
    /// <param name="table">The table being built.</param>
    private static void AddTime(Dictionary<string, ExpressionFunction> table)
    {
        table["datetime"] = MakeDatetime;
        table["utc"] = (a, _) => DataValue.FromNumber(Components(a, TimeCalendar.Utc));
        table["time"] = (a, c) => DataValue.FromNumber(Instant(Arg(a, 0), c));
        table["timezoneoffset"] = TimezoneOffset;

        // The getters exist in a local form and a UTC form with the prefix
        (string Name, Func<DateParts, double> Select)[] parts =
        [
            ("date", p => p.Date),
            ("day", p => p.Day),
            ("year", p => p.Year),
            ("month", p => p.Month),
            ("hours", p => p.Hours),
            ("minutes", p => p.Minutes),
            ("seconds", p => p.Seconds),
            ("milliseconds", p => p.Milliseconds),
            ("quarter", p => 1 + (p.Month / 3)),
            ("week", WeekNumber),
            ("isoweek", IsoWeekNumber),
            ("dayofyear", p => p.DayOfYear),
        ];
        foreach (var (name, select) in parts)
        {
            table[name] = Part(select, utc: false);
            table["utc" + name] = Part(select, utc: true);
        }

        table["timeOffset"] = (a, c) => OffsetDate(a, c, utc: false);
        table["utcOffset"] = (a, c) => OffsetDate(a, c, utc: true);
        table["timeSequence"] = (a, c) => Sequence(a, c, utc: false);
        table["utcSequence"] = (a, c) => Sequence(a, c, utc: true);
        table["timeUnitSpecifier"] = UnitSpecifier;
        table["timeFormat"] = (a, c) => FormatTime(a, c, utc: false);
        table["utcFormat"] = (a, c) => FormatTime(a, c, utc: true);
        table["timeParse"] = (a, c) => ParseTime(a, c, utc: false);
        table["utcParse"] = (a, c) => ParseTime(a, c, utc: true);
        table["monthFormat"] = (a, c) => FormatName(a, c, "%B", weekday: false);
        table["monthAbbrevFormat"] = (a, c) => FormatName(a, c, "%b", weekday: false);
        table["dayFormat"] = (a, c) => FormatName(a, c, "%A", weekday: true);
        table["dayAbbrevFormat"] = (a, c) => FormatName(a, c, "%a", weekday: true);
    }

    /// <summary>
    ///     Reads an argument as an instant.
    /// </summary>
    /// <param name="value">The argument: a date, a number of milliseconds or a date text.</param>
    /// <param name="context">The context that supplies the calendar of a date text and takes the charge.</param>
    /// <returns>The whole milliseconds since the epoch, or NaN when the argument is not a valid instant.</returns>
    private static double Instant(DataValue value, ExpressionFunctionContext context)
    {
        switch (value.Kind)
        {
            case DataValueKind.Date:
                return value.Milliseconds;
            case DataValueKind.Number:
                return WholeInstant(value.Number);
            case DataValueKind.String:
                // The parser looks at 128 characters at most, so the charge is the cost of that look
                context.ChargeSize(Math.Min(value.Text!.Length, JsDateParser.MaxLength));
                return JsDateParser.TryParse(value.Text, context.Calendar, out var parsed) ? parsed : double.NaN;
            default:
                return double.NaN;
        }
    }

    /// <summary>
    ///     Reads a number of milliseconds as an instant, like the time clip of JavaScript with the range of this engine.
    /// </summary>
    /// <param name="milliseconds">The number.</param>
    /// <returns>The number truncated to whole milliseconds, or NaN when it is outside the range of the calendar.</returns>
    private static double WholeInstant(double milliseconds) =>
        TimeCalendar.IsValid(milliseconds) ? Math.Truncate(milliseconds) + 0.0 : double.NaN;

    /// <summary>
    ///     Wraps an instant as a date value.
    /// </summary>
    /// <param name="milliseconds">The instant, or NaN.</param>
    /// <returns>The date; null for NaN.</returns>
    private static DataValue DateOrNull(double milliseconds) =>
        TimeCalendar.IsValid(milliseconds) ? DataValue.FromDate(milliseconds) : default;

    /// <summary>
    ///     Gets a numeric argument or a default when it is not supplied.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <param name="fallback">The value of a missing argument.</param>
    /// <returns>The number.</returns>
    private static double NumOr(ReadOnlySpan<DataValue> arguments, int index, double fallback) =>
        index < arguments.Length ? ExpressionEvaluator.ToNumber(arguments[index]) : fallback;

    /// <summary>
    ///     Builds an instant from the components of <c>datetime(year, month, ...)</c> or <c>utc(year, ...)</c>.
    /// </summary>
    /// <param name="arguments">The year, the month, the day of the month, the hours, the minutes, the seconds and the milliseconds.</param>
    /// <param name="calendar">The calendar whose wall time the components are.</param>
    /// <returns>The instant, or NaN when a component is not a number or the result is outside the valid range.</returns>
    private static double Components(ReadOnlySpan<DataValue> arguments, TimeCalendar calendar)
    {
        // A year from 0 to 99 means 1900 to 1999, as for the JavaScript Date constructor
        var year = NumOr(arguments, 0, double.NaN);
        if (double.IsFinite(year) && Math.Truncate(year) is >= 0.0 and <= 99.0)
        {
            year = 1900 + Math.Truncate(year);
        }

        return calendar.FromParts(
            year,
            NumOr(arguments, 1, 0),
            NumOr(arguments, 2, 1),
            NumOr(arguments, 3, 0),
            NumOr(arguments, 4, 0),
            NumOr(arguments, 5, 0),
            NumOr(arguments, 6, 0));
    }

    /// <summary>
    ///     The <c>datetime</c> function: no argument gives null (there is no clock), one argument is an instant, two or more
    ///     are the components of a local date.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="context">The context.</param>
    /// <returns>The date, or null when it is invalid.</returns>
    private static DataValue MakeDatetime(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context) => arguments.Length switch
    {
        0 => default,
        1 => DateOrNull(Instant(arguments[0], context)),
        _ => DateOrNull(Components(arguments, context.Calendar)),
    };

    /// <summary>
    ///     Creates a getter of a calendar part of an instant.
    /// </summary>
    /// <param name="select">Reads the part from the wall-clock fields.</param>
    /// <param name="utc">True for the UTC form, false for the calendar of the evaluation.</param>
    /// <returns>The function; it gives NaN for an invalid instant.</returns>
    private static ExpressionFunction Part(Func<DateParts, double> select, bool utc) => (a, c) =>
    {
        var parts = (utc ? TimeCalendar.Utc : c.Calendar).ToParts(Instant(Arg(a, 0), c));
        return DataValue.FromNumber(parts.IsValid ? select(parts) : double.NaN);
    };

    /// <summary>
    ///     The <c>timezoneoffset</c> function.
    /// </summary>
    /// <param name="arguments">The instant.</param>
    /// <param name="context">The context.</param>
    /// <returns>The minutes to add to the local time to get UTC (positive west of Greenwich), or NaN for an invalid instant.</returns>
    private static DataValue TimezoneOffset(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var parts = context.Calendar.ToParts(Instant(Arg(arguments, 0), context));
        return DataValue.FromNumber(parts.IsValid ? 0 - (parts.OffsetMs / 60_000.0) : double.NaN);
    }

    /// <summary>
    ///     Counts the Sundays from the first of January up to a day: Vega's <c>week</c>, which is 0 until the first Sunday.
    /// </summary>
    /// <param name="p">The wall-clock fields.</param>
    /// <returns>The week number from 0.</returns>
    private static double WeekNumber(DateParts p)
    {
        // The Sunday that starts the week of the day, less the Sunday that starts the week of the last day of the previous year
        var dayZero = p.DayOfYear - 1;
        var januaryFirst = (int)TimeCalendar.FloorMod(p.Day - dayZero, 7);
        return (dayZero - p.Day + 1 + ((januaryFirst + 6) % 7)) / 7.0;
    }

    /// <summary>
    ///     Gets the ISO 8601 week number of a day.
    /// </summary>
    /// <param name="p">The wall-clock fields.</param>
    /// <returns>The week number from 1 to 53.</returns>
    private static double IsoWeekNumber(DateParts p)
    {
        var isoDay = p.Day == 0 ? 7 : p.Day;
        var week = (p.DayOfYear - isoDay + 10) / 7;
        if (week < 1)
        {
            return WeeksInIsoYear(p.Year - 1);
        }

        return week > WeeksInIsoYear(p.Year) ? 1 : week;
    }

    /// <summary>
    ///     Tells how many ISO weeks a year has: 53 when it starts on a Thursday, or on a Wednesday in a leap year.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <returns>52 or 53.</returns>
    private static int WeeksInIsoYear(int year)
    {
        var first = TimeCalendar.FirstWeekdayOfYear(year);
        var leap = (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
        return first == 4 || (leap && first == 3) ? 53 : 52;
    }

    /// <summary>
    ///     Finds the interval of a unit name for <c>timeOffset</c> and <c>timeSequence</c>.
    /// </summary>
    /// <param name="name">The argument that names the unit.</param>
    /// <param name="calendar">The calendar of the interval.</param>
    /// <returns>
    ///     The interval, or null when the name is not a unit. The Vega names are <c>year</c>, <c>quarter</c>, <c>month</c>,
    ///     <c>week</c>, <c>date</c>, <c>day</c>, <c>dayofyear</c>, <c>hours</c>, <c>minutes</c>, <c>seconds</c> and
    ///     <c>milliseconds</c>, in lower case; the singular d3 names are accepted too, the ISO week and the <c>utc</c>
    ///     prefix are not.
    /// </returns>
    private static TimeInterval? FindInterval(DataValue name, TimeCalendar calendar)
    {
        if (!name.IsString || name.Text!.Length > MaxUnitNameLength || name.Text != name.Text.ToLowerInvariant()
            || !TimeInterval.TryParseName(name.Text, out var kind, out var multiplier, out var forceUtc) || forceUtc)
        {
            return null;
        }

        // A quarter is every third month
        return calendar.Interval(kind).Every(multiplier);
    }

    /// <summary>
    ///     The <c>timeOffset</c> and <c>utcOffset</c> functions.
    /// </summary>
    /// <param name="arguments">The unit, the date and the optional number of units (1 when missing).</param>
    /// <param name="context">The context.</param>
    /// <param name="utc">True for the UTC form.</param>
    /// <returns>The moved date; null for an unknown unit or an invalid date.</returns>
    private static DataValue OffsetDate(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context, bool utc)
    {
        var interval = FindInterval(Arg(arguments, 0), utc ? TimeCalendar.Utc : context.Calendar);
        if (interval is null)
        {
            return default;
        }

        var step = IsAbsent(arguments, 2) ? 1 : Num(arguments, 2);
        return DateOrNull(interval.Offset(Instant(Arg(arguments, 1), context), step));
    }

    /// <summary>
    ///     The <c>timeSequence</c> and <c>utcSequence</c> functions.
    /// </summary>
    /// <param name="arguments">The unit, the start, the exclusive stop and the optional step (1 when missing).</param>
    /// <param name="context">The context.</param>
    /// <param name="utc">True for the UTC form.</param>
    /// <returns>The dates from the first unit boundary at or after the start; null for an unknown unit.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the sequence would hold more dates than the limit or the step budget allows.</exception>
    private static DataValue Sequence(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context, bool utc)
    {
        var interval = FindInterval(Arg(arguments, 0), utc ? TimeCalendar.Utc : context.Calendar);
        if (interval is null)
        {
            return default;
        }

        var start = Instant(Arg(arguments, 1), context);
        var stop = Instant(Arg(arguments, 2), context);
        var step = IsAbsent(arguments, 3) ? 1 : Num(arguments, 3);

        // The sequence is cut off at the array limit, and at what the step budget still pays for, so no more work is done
        var affordable = Math.Max(context.MaxSteps - context.Steps, 0);
        var allowed = (int)Math.Min(context.MaxArrayLength, affordable);
        IReadOnlyList<double> dates;
        try
        {
            dates = interval.Range(start, stop, step, allowed, context.CancellationToken);
        }
        catch (TimeRangeLimitException) when (affordable < context.MaxArrayLength)
        {
            context.ChargeSteps(context.MaxSteps + 1);
            throw;
        }
        catch (TimeRangeLimitException)
        {
            throw new ExpressionRuntimeException(
                $"The date sequence holds more than the limit of {context.MaxArrayLength} dates (VegaLiteLimits.MaxScaleDomainSize).");
        }

        context.ChargeSteps(dates.Count);
        var result = new DataValue[dates.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = DataValue.FromDate(dates[i]);
        }

        return DataValue.FromOwnedArray(result);
    }

    /// <summary>
    ///     The <c>timeUnitSpecifier(units, specifiers)</c> function.
    /// </summary>
    /// <param name="arguments">A unit name or an array of them, and an optional object of specifiers that replace the defaults.</param>
    /// <param name="context">The context.</param>
    /// <returns>The d3 time format of the units; null when the units are not valid.</returns>
    private static DataValue UnitSpecifier(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context)
    {
        var first = Arg(arguments, 0);
        var units = new List<string>();
        if (first.IsString)
        {
            units.Add(first.Text!);
        }
        else if (first.IsArray && first.Elements.Length <= MaxUnitNames)
        {
            foreach (var element in first.Elements)
            {
                if (!element.IsString)
                {
                    return default;
                }

                units.Add(element.Text!);
            }
        }
        else
        {
            return default;
        }

        context.ChargeSize(units.Sum(u => (long)u.Length));
        var overrides = ReadSpecifiers(Arg(arguments, 1), context);
        return TimeUnitSpec.TryGetUnitSpecifier(units, overrides, out var specifier) ? DataValue.FromString(specifier) : default;
    }

    /// <summary>
    ///     Reads an object of format specifiers: the members whose value is a text.
    /// </summary>
    /// <param name="value">The argument; anything other than an object has no specifiers.</param>
    /// <param name="context">The context that is charged for the members.</param>
    /// <returns>The specifiers by key, or null when there are none.</returns>
    private static Dictionary<string, string>? ReadSpecifiers(DataValue value, ExpressionFunctionContext context)
    {
        var members = value.ObjectValue;
        if (members is null)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        long length = 0;
        for (var i = 0; i < members.Count; i++)
        {
            if (members.ValueAt(i).IsString)
            {
                var text = members.ValueAt(i).Text!;
                result[members.Keys[i]] = text;
                length += text.Length + members.Keys[i].Length;
            }
        }

        context.ChargeSteps(members.Count / 8);
        context.ChargeSize(length);
        return result;
    }

    /// <summary>
    ///     The <c>timeFormat</c> and <c>utcFormat</c> functions.
    /// </summary>
    /// <param name="arguments">The value, and the specifier: a d3 time format text, or an object for a multi-format.</param>
    /// <param name="context">The context.</param>
    /// <param name="utc">True for the UTC form.</param>
    /// <returns>The text; <c>null</c> for a null value (as Vega) and <c>Invalid Date</c> for an invalid one.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the text would be longer than the string limit.</exception>
    private static DataValue FormatTime(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context, bool utc)
    {
        var value = Arg(arguments, 0);
        if (arguments.Length > 0 && value.IsNull)
        {
            return DataValue.FromString("null");
        }

        // d3 reads the value as a number (a date is its milliseconds); a text that is not a number is an invalid date
        var instant = double.NaN;
        if (arguments.Length > 0)
        {
            instant = value.IsDate ? value.Milliseconds : WholeInstant(ExpressionEvaluator.ToNumber(value, context));
        }
        var calendar = utc ? TimeCalendar.Utc : context.Calendar;
        TimeFormatter formatter;
        if (Arg(arguments, 1).IsObject)
        {
            formatter = ExpressionFunctionContext.CompileMultiFormat(calendar, ReadSpecifiers(Arg(arguments, 1), context));
        }
        else
        {
            var specifier = Text(arguments, 1, context);
            context.ChargeSize(specifier.Length);
            formatter = context.GetTimeFormatter(specifier);
        }

        return FormatWith(formatter, instant, calendar, context);
    }

    /// <summary>
    ///     Formats an instant within the string limit and charges the text.
    /// </summary>
    /// <param name="formatter">The formatter.</param>
    /// <param name="instant">The instant, or NaN.</param>
    /// <param name="calendar">The calendar of the wall time.</param>
    /// <param name="context">The context.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ExpressionRuntimeException">Thrown when the text would be longer than the string limit.</exception>
    private static DataValue FormatWith(TimeFormatter formatter, double instant, TimeCalendar calendar, ExpressionFunctionContext context)
    {
        try
        {
            var text = formatter.Format(instant, calendar, context.MaxStringLength);
            context.ChargeSize(text.Length);
            return DataValue.FromString(text);
        }
        catch (TimeFormatTooLongException)
        {
            throw context.StringLimitExceeded();
        }
    }

    /// <summary>
    ///     The <c>timeParse</c> and <c>utcParse</c> functions.
    /// </summary>
    /// <param name="arguments">The text and the d3 time format specifier.</param>
    /// <param name="context">The context.</param>
    /// <param name="utc">True for the UTC form.</param>
    /// <returns>The date; null when the text does not match the specifier completely.</returns>
    private static DataValue ParseTime(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context, bool utc)
    {
        var text = Text(arguments, 0, context);
        var specifier = Text(arguments, 1, context);
        context.ChargeSize(text.Length + (long)specifier.Length);
        if (specifier.Length > TimeFormat.MaxSpecifierLength)
        {
            throw new ExpressionRuntimeException($"The time format is longer than the limit of {TimeFormat.MaxSpecifierLength} characters.");
        }

        var calendar = utc ? TimeCalendar.Utc : context.Calendar;
        return TimeFormat.TryParse(specifier, text, calendar, out var instant) ? DateOrNull(instant) : default;
    }

    /// <summary>
    ///     The <c>monthFormat</c>, <c>monthAbbrevFormat</c>, <c>dayFormat</c> and <c>dayAbbrevFormat</c> functions.
    /// </summary>
    /// <param name="arguments">The month from 0 or the weekday from 0 (Sunday); other numbers roll over, as in a date.</param>
    /// <param name="context">The context.</param>
    /// <param name="specifier">The d3 directive of the name.</param>
    /// <param name="weekday">True for a weekday counted from Sunday, false for a month counted from January.</param>
    /// <returns>The name; the empty text when the argument is not a whole number.</returns>
    private static DataValue FormatName(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context, string specifier, bool weekday)
    {
        var value = Arg(arguments, 0);
        if (!value.IsNumber || !double.IsInteger(value.Number))
        {
            return DataValue.FromString(string.Empty);
        }

        // The month names count months from the first of January 2000, the day names count days from the second
        var instant = weekday
            ? TimeCalendar.Utc.FromParts(NameReferenceYear, 0, 2 + value.Number, 0, 0, 0, 0)
            : TimeCalendar.Utc.FromParts(NameReferenceYear, value.Number, 1, 0, 0, 0, 0);
        return FormatWith(context.GetTimeFormatter(specifier), instant, TimeCalendar.Utc, context);
    }
}
