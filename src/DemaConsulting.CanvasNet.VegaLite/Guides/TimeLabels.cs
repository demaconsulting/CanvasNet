// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     The label formatting and the tick requests that the axes and the legends of temporal fields share: which format a label of a date gets, how the
///     <c>tickCount</c> of a time scale is read, and how an explicit list of dates is read.
/// </summary>
/// <remarks>
///     <para>
///         <b>Format.</b> A <c>format</c> that is a text is a d3 time format (in the calendar of the scale), a <c>format</c> object is Vega's multi-format with
///         its members as the overrides of the levels (<c>milliseconds</c>, <c>seconds</c>, <c>minutes</c>, <c>hours</c>, <c>date</c> or <c>day</c>, <c>week</c>,
///         <c>month</c>, <c>quarter</c>, <c>year</c>); without a <c>format</c> a field with a time unit uses the specifier of the unit (<c>%b %Y</c> for
///         <c>yearmonth</c>) and the rest use the default of the guide: Vega's multi-format for an axis, <c>config.timeFormat</c> for a legend. A <c>formatType</c> of
///         <c>number</c> formats the milliseconds as a number with the d3 number format, <c>time</c> forces a time format, and <c>utc</c> forces it in UTC.
///     </para>
///     <para>
///         <b>Limits.</b> A label is at most <see cref="VegaLiteLimits.MaxStringLength"/> characters (the formatter stops as soon as it would write more, so a
///         huge specifier cannot allocate more) and the overflow is a located <see cref="VegaLiteRenderException"/>. Nothing here reads the clock, the host
///         time zone or the host culture: names are the English d3 locale and digits are ASCII.
///     </para>
/// </remarks>
internal static class TimeLabels
{
    /// <summary>
    ///     What decides the label format of a guide.
    /// </summary>
    /// <param name="Format">The <c>format</c> of the guide (a text or an object), or null.</param>
    /// <param name="FormatType">The <c>formatType</c> of the guide, or null.</param>
    /// <param name="Unit">The time unit of the field, or null.</param>
    /// <param name="DefaultSpecifier">The d3 time format the guide uses when it has no other (a legend's <c>timeFormat</c>), or null for Vega's multi-format.</param>
    /// <param name="Calendar">The calendar of the scale.</param>
    /// <param name="MaxLength">The longest label allowed.</param>
    /// <param name="Owner">The word that names the guide in a message: <c>axis</c> or <c>legend</c>.</param>
    /// <param name="Location">The location of the guide, for a label that is too long.</param>
    /// <param name="FormatLocation">The location of the <c>format</c> property, for a number format that is not valid.</param>
    internal sealed record Options(
        ConfigNode? Format,
        string? FormatType,
        TimeUnitSpec? Unit,
        string? DefaultSpecifier,
        TimeCalendar Calendar,
        int MaxLength,
        string Owner,
        VegaLiteLocation Location,
        VegaLiteLocation FormatLocation);

    /// <summary>
    ///     Tells whether a <c>formatType</c> is one that the renderer evaluates.
    /// </summary>
    /// <param name="formatType">The format type.</param>
    /// <returns><see langword="true"/> for <c>number</c>, <c>time</c> and <c>utc</c>; any other name is a custom format type, which is not evaluated.</returns>
    public static bool IsKnownFormatType(string? formatType) => FeatureCatalog.IsEvaluatedFormatType(formatType);

    /// <summary>
    ///     Creates the function that turns an instant (milliseconds since the epoch) into its label.
    /// </summary>
    /// <param name="options">What decides the format.</param>
    /// <returns>The function; it throws <see cref="VegaLiteRenderException"/> (located at the guide) when a label would be longer than the limit.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, with the location of the <c>format</c>, when a number format (<c>formatType</c> <c>number</c>) is not a d3 format specifier.</exception>
    public static Func<double, string> Create(Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.FormatType == "number")
        {
            return NumberLabels(options);
        }

        var calendar = options.FormatType == "utc" ? TimeCalendar.Utc : options.Calendar;
        var formatter = ChooseFormatter(options, calendar);
        return ms =>
        {
            try
            {
                return formatter.Format(ms, calendar, options.MaxLength);
            }
            catch (TimeFormatTooLongException)
            {
                throw LabelTooLong(options);
            }
        };
    }

    /// <summary>
    ///     Reads an interval request from the <c>tickCount</c> of a time scale: an interval name or <c>{interval, step}</c>.
    /// </summary>
    /// <param name="tickCount">The <c>tickCount</c> property, or null.</param>
    /// <returns>The request; null when the property is absent, a number (the count is read as a number) or not an interval.</returns>
    public static TimeTickRequest? IntervalRequest(SpecNode? tickCount)
    {
        if (tickCount is { IsString: true } name && TimeTickRequest.TryFromInterval(name.StringValue!, null, out var named))
        {
            return named;
        }

        if (tickCount is { IsObject: true } && tickCount["interval"] is { IsString: true } interval
            && TimeTickRequest.TryFromInterval(interval.StringValue!, tickCount["step"]?.AsNumber(), out var stepped))
        {
            return stepped;
        }

        return null;
    }

    /// <summary>
    ///     Tells whether Vega-Lite leaves the default tick count of a time axis to Vega (ten): for a field whose time unit is exactly a month, an hour, a day or a quarter.
    /// </summary>
    /// <param name="unit">The time unit of the field, or null.</param>
    /// <returns><see langword="true"/> when the default is Vega's ten instead of one tick for every 40 pixels.</returns>
    public static bool UsesVegaDefaultCount(TimeUnitSpec? unit) =>
        unit is { Parts.Count: 1 } && unit.Parts[0] is "month" or "hours" or "day" or "quarter";

    /// <summary>
    ///     Reads an entry of an explicit list of dates (<c>axis.values</c>, <c>legend.values</c>) as an instant.
    /// </summary>
    /// <remarks>
    ///     A number is milliseconds since the epoch, a text is a date text (read like <c>Date.parse</c> in the calendar), and an object is a Vega-Lite date-time object
    ///     (in the calendar unless it says <c>utc</c>).
    /// </remarks>
    /// <param name="node">The entry.</param>
    /// <param name="calendar">The calendar of the scale.</param>
    /// <returns>The instant; NaN when the entry is not a date.</returns>
    public static double ReadInstant(SpecNode node, TimeCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(calendar);
        double ms = node.Kind switch
        {
            SpecNodeKind.Number => Math.Truncate(node.NumberValue),
            SpecNodeKind.String => JsDateParser.TryParse(node.StringValue!, calendar, out var parsed) ? parsed : double.NaN,
            SpecNodeKind.Object => DateTimeObject.TryRead(node, out var value, out _, out _) ? value.ToMilliseconds(calendar) : double.NaN,
            _ => double.NaN,
        };
        return TimeCalendar.IsValid(ms) ? ms : double.NaN;
    }

    /// <summary>
    ///     Chooses the formatter of the time labels.
    /// </summary>
    /// <param name="options">What decides the format.</param>
    /// <param name="calendar">The calendar of the labels.</param>
    /// <returns>The formatter.</returns>
    private static TimeFormatter ChooseFormatter(Options options, TimeCalendar calendar)
    {
        if (options.Format is { Kind: ConfigNodeKind.String } text)
        {
            return TimeFormat.Compile(text.StringValue!);
        }

        if (options.Format is { IsObject: true } levels)
        {
            var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (name, node) in levels.Properties)
            {
                if (node.AsString() is { } specifier)
                {
                    overrides[name] = specifier;
                }
            }

            return TimeFormat.Multi(calendar, overrides);
        }

        if (options.Unit?.Specifier() is { } unitSpecifier)
        {
            return TimeFormat.Compile(unitSpecifier);
        }

        return options.DefaultSpecifier is { } fallback ? TimeFormat.Compile(fallback) : TimeFormat.Multi(calendar, null);
    }

    /// <summary>
    ///     Creates the labels of <c>formatType: "number"</c>: the milliseconds as a number.
    /// </summary>
    /// <param name="options">What decides the format.</param>
    /// <returns>The function.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the format is not a d3 number format specifier.</exception>
    private static Func<double, string> NumberLabels(Options options)
    {
        NumberFormat? formatter = null;
        if (options.Format?.AsString() is { } specifier)
        {
            try
            {
                formatter = NumberFormat.Create(specifier);
            }
            catch (FormatException ex)
            {
                throw new VegaLiteSpecException($"The {options.Owner} format is not a valid d3 format specifier: {ex.Message}", options.FormatLocation, ex);
            }
        }

        return ms =>
        {
            var text = formatter is null ? JsNumber.ToString(ms) : formatter.Format(ms);
            if (text.Length > options.MaxLength)
            {
                throw LabelTooLong(options);
            }

            return text;
        };
    }

    /// <summary>
    ///     Creates the exception for a label that is longer than the limit.
    /// </summary>
    /// <param name="options">What decides the format.</param>
    /// <returns>The exception, located at the guide.</returns>
    private static VegaLiteRenderException LabelTooLong(Options options) =>
        new($"A label of the {options.Owner} is longer than the limit of {options.MaxLength} characters (VegaLiteLimits.MaxStringLength).", options.Location);
}
