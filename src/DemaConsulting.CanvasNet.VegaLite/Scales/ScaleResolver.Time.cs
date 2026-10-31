// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <content>
///     The scales of temporal fields and of fields with a time unit (a partial file of the scale resolver unit).
/// </content>
/// <remarks>
///     <para>
///         <b>Kinds.</b> A temporal field gets Vega-Lite's default type: <see cref="ScaleKind.Time"/> on position, size, opacity, stroke width and angle
///         (<see cref="ScaleKind.Utc"/> when its time unit is a UTC unit), <see cref="ScaleKind.Sequential"/> on a color channel (Vega-Lite's default there is a <c>time</c>
///         scale with a color range, which is drawn as the sequential scale over the instants: the same colors, with ticks and labels of dates) and
///         <see cref="ScaleKind.Ordinal"/> on shape and stroke dash. Only <c>time</c> and <c>utc</c> are accepted as an explicit type of a temporal field
///         (Vega-Lite's <c>scaleTypeSupportDataType</c>); on a color channel they stand for the same sequential scale, as <c>linear</c> does for numbers, and <c>utc</c> keeps its meaning there (the
///         UTC calendar for the ticks and labels of the legend, whatever the time zone of the chart). A nominal or ordinal
///         field takes the discrete scales, also over the dates of a time unit; <c>time</c> and <c>utc</c> are ignored for it (reported by the feature catalog).
///     </para>
///     <para>
///         <b>Domain.</b> The data domain is the extent of the dates of the field (and of its second end: the end of the time unit for a mark that spans it, or
///         <c>x2</c> and <c>y2</c>), without the zero rule. An explicit domain holds timestamps, date texts (read like <c>Date.parse</c> in the calendar of the
///         scale) or date-time objects; for a field with a single local time unit a number below 10000 or a text that is not a date names a part of the unit (Vega-Lite's
///         <c>valueExpr</c>). <c>domainMin</c> and <c>domainMax</c> take a timestamp or a date-time object. <c>nice</c> is off by default for time scales; <c>true</c>
///         uses ten ticks, a number is the count, and a text or <c>{interval, step}</c> names the interval of the boundaries.
///     </para>
///     <para>
///         <b>Limits.</b> The explicit arrays are bounded by <see cref="VegaLiteLimits.MaxScaleDomainSize"/> before they are read (the existing
///         <c>CheckSpecArray</c> rule, spec <i>and</i> configuration arrays; the time scales add no configuration array), the data pass polls the cancellation token
///         every 4,096 rows, and every conversion is constant work.
///     </para>
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>The dates, the unit and the calendar of a channel whose values are instants.</summary>
    /// <param name="Calendar">The calendar of the scale: UTC for a UTC scale or a UTC unit, otherwise the zone of the chart.</param>
    /// <param name="Unit">The time unit of the field (the resolved one for the <c>maxbins</c> form), or null.</param>
    /// <param name="IsTemporal">Whether the field type is <c>temporal</c>.</param>
    /// <param name="IsUtc">Whether the scale is a UTC scale: a <c>utc</c> scale, a UTC unit, or an explicit <c>utc</c> type on a color channel (whose kind is sequential).</param>
    private sealed record TimeFacts(TimeCalendar Calendar, TimeUnitSpec? Unit, bool IsTemporal, bool IsUtc);

    /// <summary>
    ///     Finds the time unit of a channel and the type facts that decide the kind of its scale.
    /// </summary>
    /// <param name="plan">The plan of the channel, or null.</param>
    /// <param name="type">The field type.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The unit (null when the channel has none) and whether the field is temporal.</returns>
    private static (TimeUnitSpec? Unit, bool IsTemporal) ReadTimeUnit(ChannelPlan? plan, string type, Context context)
    {
        TimeUnitSpec? unit = null;
        if (plan?.TimeUnit is { } unitPlan)
        {
            unit = context.Data.TimeUnits.TryGetValue(unitPlan.StartName, out var resolved) ? resolved : unitPlan.Spec;
        }

        return (unit, type == "temporal");
    }

    /// <summary>
    ///     Completes the time facts of a channel once its scale kind is known.
    /// </summary>
    /// <param name="unit">The unit of the field, or null.</param>
    /// <param name="isTemporal">Whether the field type is temporal.</param>
    /// <param name="kind">The scale kind.</param>
    /// <param name="utcRequested">Whether the field asked for a <c>utc</c> scale type that the kind does not show (a color channel's sequential scale).</param>
    /// <param name="context">The view facts (the calendar of the chart comes from them).</param>
    /// <returns>The facts; null for a field that is neither temporal nor a time unit.</returns>
    private static TimeFacts? MakeTimeFacts(TimeUnitSpec? unit, bool isTemporal, ScaleKind kind, bool utcRequested, Context context)
    {
        if (unit is null && !isTemporal)
        {
            return null;
        }

        // A UTC scale (also the sequential scale of a color that asked for type utc) or a UTC unit uses the UTC calendar whatever the zone of the chart is
        var utc = kind == ScaleKind.Utc || utcRequested || unit is { Utc: true };
        var calendar = utc ? TimeCalendar.Utc : context.Data.Evaluation.Calendar;
        return new TimeFacts(calendar, unit, isTemporal, utc);
    }

    /// <summary>
    ///     Reads an explicit domain of a temporal field or of a field with a time unit.
    /// </summary>
    /// <param name="domain">The <c>scale.domain</c> array.</param>
    /// <param name="time">The time facts.</param>
    /// <param name="context">The view facts (for the source name).</param>
    /// <param name="minimum">The fewest entries the scale needs.</param>
    /// <returns>The entries as milliseconds since the epoch.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown when the array has too few entries or an entry that is not a date.</exception>
    private static double[] ReadTemporalDomain(SpecNode domain, TimeFacts time, Context context, int minimum = 2)
    {
        if (domain.Items.Count < minimum)
        {
            throw new VegaLiteSpecException("The scale domain of a temporal field needs at least two dates.", domain.GetLocation(context.SourceName));
        }

        var entries = new double[domain.Items.Count];
        for (var i = 0; i < entries.Length; i++)
        {
            var ms = ToMilliseconds(domain.Items[i], time);
            if (double.IsNaN(ms))
            {
                throw new VegaLiteSpecException(
                    "The scale domain of a temporal field must contain dates: a timestamp, a date text or a date-time object.",
                    domain.Items[i].GetLocation(context.SourceName));
            }

            entries[i] = ms;
        }

        return entries;
    }

    /// <summary>
    ///     Converts one entry of a domain or a sort array of a temporal field to milliseconds.
    /// </summary>
    /// <remarks>
    ///     A number or a text follows Vega-Lite's <c>valueExpr</c> for the unit of the field (a part of the unit, or a date); a date-time object is
    ///     evaluated in the calendar of the scale unless it says <c>utc</c>.
    /// </remarks>
    /// <param name="node">The entry.</param>
    /// <param name="time">The time facts.</param>
    /// <returns>The instant; NaN when the entry is not a date.</returns>
    private static double ToMilliseconds(SpecNode node, TimeFacts time)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.Number:
                return DateTimeObject.FromScalar(node.NumberValue, time.Unit, time.Calendar);
            case SpecNodeKind.String:
                return DateTimeObject.FromScalar(node.StringValue!, time.Unit, time.Calendar, text => JsDateParser.TryParse(text, time.Calendar, out var parsed) ? parsed : double.NaN);
            case SpecNodeKind.Object:
                return DateTimeObject.TryRead(node, out var value, out _, out _) ? value.ToMilliseconds(time.Calendar) : double.NaN;
            default:
                return double.NaN;
        }
    }

    /// <summary>
    ///     Reads the <c>domainMin</c>, <c>domainMax</c> or <c>domainMid</c> of a scale as a number, or as a date when the field is temporal.
    /// </summary>
    /// <param name="node">The property value, or null.</param>
    /// <param name="time">The time facts, or null for a field that is not temporal.</param>
    /// <returns>The number or the instant; null when the property is absent or not usable.</returns>
    private static double? ReadDomainBound(SpecNode? node, TimeFacts? time)
    {
        if (node is null)
        {
            return null;
        }

        if (time is null)
        {
            return node.AsNumber();
        }

        // A timestamp is milliseconds as it is (no unit part rule); an object is a date-time
        if (node.IsNumber)
        {
            return TimeCalendar.IsValid(node.NumberValue) ? Math.Truncate(node.NumberValue) + 0.0 : null;
        }

        return node.IsObject && DateTimeObject.TryRead(node, out var value, out _, out _) && !double.IsNaN(value.ToMilliseconds(time.Calendar))
            ? value.ToMilliseconds(time.Calendar)
            : null;
    }

    /// <summary>
    ///     Reads the <c>datum</c> of a temporal channel as an instant.
    /// </summary>
    /// <param name="datum">The datum node (a number or a text).</param>
    /// <param name="time">The time facts.</param>
    /// <returns>The instant; null when the datum is not a date.</returns>
    private static double? ReadTemporalDatum(SpecNode datum, TimeFacts time)
    {
        var ms = ToMilliseconds(datum, time with { Unit = null });
        return double.IsNaN(ms) ? null : ms;
    }

    /// <summary>
    ///     Decides the <c>nice</c> request of a time or UTC scale.
    /// </summary>
    /// <remarks>
    ///     Vega-Lite gives time scales no default <c>nice</c>. <c>true</c> asks for ten ticks, a positive number for that many, and an interval name or
    ///     <c>{interval, step}</c> for the boundaries of that interval; <c>false</c>, an unknown name and a bad number switch it off.
    /// </remarks>
    /// <param name="scaleDef">The scale object, or null.</param>
    /// <returns>The request; null for no <c>nice</c>.</returns>
    private static TimeTickRequest? TimeNiceRequest(SpecNode? scaleDef)
    {
        var nice = scaleDef?["nice"];
        switch (nice)
        {
            case { IsBoolean: true }:
                return nice.BoolValue ? TimeTickRequest.Default : null;
            case { IsNumber: true }:
                return nice.NumberValue > 0 ? new TimeTickRequest(nice.NumberValue) : null;
            case { IsString: true }:
                return TimeTickRequest.TryFromInterval(nice.StringValue!, null, out var named) ? named : null;
            case { IsObject: true } when nice["interval"] is { IsString: true } interval:
                return TimeTickRequest.TryFromInterval(interval.StringValue!, nice["step"]?.AsNumber(), out var stepped) ? stepped : null;
            default:
                return null;
        }
    }
}
