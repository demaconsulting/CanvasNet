// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause): legends of temporal fields; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <content>
///     The legends of temporal fields: the ticks and labels of dates (a gradient legend of a temporal color, a symbol legend of a size or an opacity on a time scale) and the
///     labels of the dates of a field with a time unit (a symbol legend over the months of a year, say).
/// </content>
/// <remarks>
///     <para>
///         <b>Labels.</b> A legend labels a date with its <c>format</c> (a text, or the multi-format levels of an object), else the specifier of the time unit of the field, else
///         <c>config.timeFormat</c> (<c>%b %d, %Y</c> by default), in the calendar of the scale (UTC for a UTC scale or unit, else the time zone of the chart); the
///         <c>formatType</c> <c>number</c> formats the milliseconds as a number. <b>Ticks.</b> A temporal color is drawn by a sequential scale over the milliseconds, so its legend takes the
///         time ticks of the domain (the calendar interval nearest to the span over the tick count, or the interval named by <c>tickCount</c>), and the gradient samples its color at the
///         time ticks for a count of 15. <c>values</c> are timestamps, date texts or date-time objects.
///     </para>
///     <para>
///         <b>Limits.</b> The ticks are bounded by <see cref="VegaLiteLimits.MaxTicks"/>, every label by <see cref="VegaLiteLimits.MaxStringLength"/> and its length is charged to the
///         evaluation of the build (<see cref="VegaLiteLimits.MaxEvaluationSteps"/>) as it is made; a failure is a located <see cref="VegaLiteRenderException"/> that names the limit.
///     </para>
/// </remarks>
internal sealed partial class LegendBuilder
{
    /// <summary>
    ///     Tells whether the values of the first channel of a legend are instants (the field is temporal or has a time unit).
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns><see langword="true"/> for a legend of a temporal field.</returns>
    private static bool IsTemporal(Context c) => c.Spec.Canonical.Scale.IsTemporalField;

    /// <summary>
    ///     Gets the calendar of the dates of a legend.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The calendar of the scale; UTC when the scale has none.</returns>
    private static TimeCalendar CalendarOf(Context c) => c.Spec.Canonical.Scale.Calendar ?? TimeCalendar.Utc;

    /// <summary>
    ///     Reads an entry of <c>legend.values</c> of a temporal field.
    /// </summary>
    /// <remarks>
    ///     A discrete scale (the dates of a time unit) takes the date of the entry by the rule of its domain (a number or a text may name a part of the unit); a continuous
    ///     scale takes the instant as a number of milliseconds.
    /// </remarks>
    /// <param name="c">The build state.</param>
    /// <param name="item">The entry (a number, a text or a date-time object).</param>
    /// <returns>A date for a discrete scale, a number of milliseconds for a continuous scale; null for an entry that is not a date.</returns>
    private static DataValue? TemporalValue(Context c, SpecNode item)
    {
        var scale = c.Spec.Canonical.Scale;
        var calendar = CalendarOf(c);
        var discrete = c.Spec.Canonical.Model.Kind is ScaleKind.Ordinal or ScaleKind.Band or ScaleKind.Point;
        double ms;
        if (!discrete)
        {
            ms = TimeLabels.ReadInstant(item, calendar);
        }
        else
        {
            ms = item.Kind switch
            {
                SpecNodeKind.Number => DateTimeObject.FromScalar(item.NumberValue, scale.TimeUnit, calendar),
                SpecNodeKind.String => DateTimeObject.FromScalar(item.StringValue!, scale.TimeUnit, calendar, text => JsDateParser.TryParse(text, calendar, out var parsed) ? parsed : double.NaN),
                _ => TimeLabels.ReadInstant(item, calendar),
            };
        }

        if (!TimeCalendar.IsValid(ms))
        {
            return null;
        }

        return discrete ? DataValue.FromDate(ms) : DataValue.FromNumber(ms);
    }

    /// <summary>
    ///     Creates the function that labels an instant of a temporal legend: the format of the legend, the specifier of the time unit or <c>config.timeFormat</c>.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <returns>The function; its text is charged to the evaluation of the build.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, with the location of the <c>format</c>, when a number format (<c>formatType</c> <c>number</c>) is not a format specifier.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, when the function is called, with the location of the legend, when a label is too long or the evaluation budget is exhausted.</exception>
    private Func<double, string> TimeLabelFunction(Context c)
    {
        var s = c.S;
        var scale = c.Spec.Canonical.Scale;
        var formatLocation = s.ExplicitNode("format")?.GetLocation(_sourceName) ?? c.Spec.Location;
        var formatType = s.Effective("formatType")?.AsString();
        var label = TimeLabels.Create(new TimeLabels.Options(
            s.Effective("format"),
            TimeLabels.IsKnownFormatType(formatType) ? formatType : null,
            scale.TimeUnit,
            s.Config.GetString("timeFormat"),
            CalendarOf(c),
            _limits.MaxStringLength,
            "legend",
            c.Spec.Location,
            formatLocation));
        return ms =>
        {
            var text = label(ms);
            try
            {
                c.Evaluation.ChargeSize(text.Length);
            }
            catch (ExpressionRuntimeException ex)
            {
                throw new VegaLiteRenderException(ex.Message, c.Spec.Location);
            }

            return text;
        };
    }

    /// <summary>
    ///     Gets the time ticks of the domain of a temporal legend for a tick count, after the minimum step of the legend.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="model">The scale model (a time scale or the sequential scale of a temporal color); its domain is in milliseconds.</param>
    /// <param name="requested">The requested tick count when the legend does not give one.</param>
    /// <returns>The ticks.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the legend, when the ticks are more than <see cref="VegaLiteLimits.MaxTicks"/>.</exception>
    private TickSet TimeTickSet(Context c, ScaleModel model, double requested)
    {
        var minStep = c.S.ExplicitNode("tickMinStep")?.AsNumber();
        var count = Ticks.ResolveTickCount(model.DomainStart, model.DomainEnd, requested, minStep, temporal: true);
        var request = TimeLabels.IntervalRequest(c.S.ExplicitNode("tickCount")) ?? new TimeTickRequest(count);
        return new TickSet(TimeTicksOf(c, model, request), count, null);
    }

    /// <summary>
    ///     Gets the time ticks of the domain of a temporal legend for a request.
    /// </summary>
    /// <param name="c">The build state.</param>
    /// <param name="model">The scale model.</param>
    /// <param name="request">The request: a count or an interval.</param>
    /// <returns>The ticks in milliseconds.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the legend, when the ticks are more than <see cref="VegaLiteLimits.MaxTicks"/>.</exception>
    private double[] TimeTicksOf(Context c, ScaleModel model, TimeTickRequest request)
    {
        var scale = c.Spec.Canonical.Scale;
        var utc = scale.IsUtcTime;
        try
        {
            return Ticks.TimeTicks(model.DomainStart, model.DomainEnd, request, CalendarOf(c), utc, _limits.MaxTicks, c.Token);
        }
        catch (VegaLiteRenderException ex) when (!ex.Location.IsKnown)
        {
            throw new VegaLiteRenderException(ex.Message, c.Spec.Location);
        }
    }
}
