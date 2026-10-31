// Portions derived from Vega (BSD-3-Clause): the timeunit transform; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     One time unit to compute: a field, the unit, and the names of the two fields that receive the start and the end of the unit.
/// </summary>
/// <param name="Field">The field path of the dates, as written in the specification.</param>
/// <param name="StartName">The literal name of the field that receives the start of the unit.</param>
/// <param name="EndName">The literal name of the field that receives the end of the unit (exclusive).</param>
/// <param name="Spec">The time unit (the <c>maxbins</c> form is resolved against the data).</param>
/// <param name="Node">The node of the specification that asked for the unit (the location of a failure when the call has none of its own).</param>
internal sealed record TimeUnitRequest(string Field, string StartName, string EndName, TimeUnitSpec Spec, SpecNode Node);

/// <summary>
///     The <c>timeUnit</c> transform: adds, for every row, the start and the end of the time unit that contains a date.
/// </summary>
/// <remarks>
///     <para>
///         <b>Behavior.</b> For a request the start is Vega's <c>timeFloor</c> (or <c>utcFloor</c>) of the date, the end is one step of the
///         smallest part of the unit later (<see cref="TimeUnitSpec.End"/>), and both are written as dates to the two fields of the request.
///         The dates are those of the calendar of the chart (the zone of <see cref="VegaLiteRenderOptions.TimeZone"/>), except for a <c>utc</c> unit.
///         The value of a row is read as a date when it is a date or a finite number (milliseconds since the epoch); anything else (null, text,
///         a boolean) and a date that the calendar cannot place (outside the years 1 to 9999) gives null in both fields. The <c>maxbins</c> form that names
///         no unit is resolved first: one pass finds the extent of the valid dates, and Vega's <c>timeBin</c> chooses the unit and the step for it
///         (a table without a valid date gets null fields). A <c>binned</c> unit does not floor: the value is the start and the end follows it.
///     </para>
///     <para>
///         <b>Limits and cancellation.</b> The fields are added through <see cref="TableLayout"/> (the field and cell limits are checked before
///         anything is allocated); the output names go through <see cref="TransformContext.CheckText"/>; <c>maxbins</c> is bounded by
///         <see cref="VegaLiteLimits.MaxScaleDomainSize"/>; every row is charged to the step budget through a <see cref="WorkMeter"/> (2 units per
///         request for a UTC calendar, 8 for a zone, whose offset lookups cost more) and the token is checked every 1,024 rows, in the extent pass as well.
///         A failure is a <see cref="VegaLiteRenderException"/> at the location of the call. The class is static and the result is deterministic.
///     </para>
/// </remarks>
internal static class TimeUnitTransform
{
    /// <summary>The work, in elements, of a row and a request in the UTC calendar.</summary>
    private const int UtcRowCost = 2;

    /// <summary>The work, in elements, of a row and a request in a time zone.</summary>
    private const int ZoneRowCost = 8;

    /// <summary>
    ///     Adds the start and the end of one or more time units to every row of a table.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="requests">The units to compute.</param>
    /// <param name="context">The context of the call (limits, evaluator and its calendar, token).</param>
    /// <param name="location">The location of a failure.</param>
    /// <param name="resolved">When not null, receives the unit of each request after the <c>maxbins</c> form was resolved (the request's own unit when it needed none, or when the table has no valid date).</param>
    /// <returns>The table with the fields added; the rows keep their order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="resolved"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, at <paramref name="location"/>, when a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    public static DataTable Apply(
        DataTable input,
        IReadOnlyList<TimeUnitRequest> requests,
        TransformContext context,
        VegaLiteLocation location,
        TimeUnitSpec[]? resolved = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(context);
        var names = new List<string>(requests.Count * 2);
        foreach (var request in requests)
        {
            context.CheckText(DataValue.FromString(request.StartName), "An output name of the timeUnit transform", location);
            context.CheckText(DataValue.FromString(request.EndName), "An output name of the timeUnit transform", location);
            names.Add(TableLayout.Canonical(request.StartName));
            names.Add(TableLayout.Canonical(request.EndName));
        }

        var evaluation = context.Evaluator.Context;
        var calendar = evaluation.Calendar;
        var meter = new WorkMeter(evaluation);
        try
        {
            var readers = requests.Select(r => new FieldReader(input, r.Field)).ToArray();
            var specs = new TimeUnitSpec?[requests.Count];
            for (var r = 0; r < requests.Count; r++)
            {
                specs[r] = Resolve(requests[r], readers[r], input.RowCount, context, evaluation, calendar, meter, location);
                if (resolved is not null)
                {
                    resolved[r] = specs[r] ?? requests[r].Spec;
                }
            }

            var layout = new TableLayout(input, names, keepInput: true, context.Limits, location);
            var cells = layout.Allocate(input.RowCount);
            var width = layout.Fields.Length;
            var cost = requests.Select(r => (r.Spec.Utc || calendar.IsUtc ? UtcRowCost : ZoneRowCost)).Sum();
            for (var row = 0; row < input.RowCount; row++)
            {
                context.Tick(row);
                meter.Add(cost);
                layout.CopyRow(input, row, cells, row);
                var offset = (long)row * width;
                for (var r = 0; r < requests.Count; r++)
                {
                    if (specs[r] is not { } spec || !TryReadInstant(readers[r].Read(row, evaluation), out var ms))
                    {
                        continue;
                    }

                    var start = spec.IsBinned ? ms : spec.Floor(calendar, ms);
                    if (!TimeCalendar.IsValid(start))
                    {
                        continue;
                    }

                    cells[offset + layout.Added[2 * r]] = DataValue.FromDate(start);
                    var end = spec.End(calendar, start);
                    if (TimeCalendar.IsValid(end))
                    {
                        cells[offset + layout.Added[(2 * r) + 1]] = DataValue.FromDate(end);
                    }
                }
            }

            return layout.Build(cells, input.RowCount);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw new VegaLiteRenderException(ex.Message, location);
        }
    }

    /// <summary>
    ///     Reads a value as an instant: a date, or a finite number of milliseconds since the epoch.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="ms">Receives the instant.</param>
    /// <returns><see langword="true"/> for a date or a finite number that is inside the years 1 to 9999.</returns>
    private static bool TryReadInstant(DataValue value, out double ms)
    {
        ms = 0;
        if (value.IsDate)
        {
            ms = value.Milliseconds;
            return true;
        }

        if (value.IsNumber && double.IsFinite(value.Number))
        {
            ms = Math.Truncate(value.Number);
            return TimeCalendar.IsValid(ms);
        }

        return false;
    }

    /// <summary>
    ///     Chooses the unit of a request: the <c>maxbins</c> form is resolved against the extent of the dates of the field.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="reader">The reader of the field.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="context">The context of the call.</param>
    /// <param name="evaluation">The evaluation to charge.</param>
    /// <param name="calendar">The calendar of the chart.</param>
    /// <param name="meter">The meter that charges the work.</param>
    /// <param name="location">The location of a failure.</param>
    /// <returns>The unit; null when the table has no valid date to resolve a <c>maxbins</c> unit with.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when <c>maxbins</c> is above <see cref="VegaLiteLimits.MaxScaleDomainSize"/>.</exception>
    private static TimeUnitSpec? Resolve(
        TimeUnitRequest request,
        FieldReader reader,
        int rows,
        TransformContext context,
        ExpressionFunctionContext evaluation,
        TimeCalendar calendar,
        WorkMeter meter,
        VegaLiteLocation location)
    {
        var spec = request.Spec;
        if (spec.MaxBins is not { } maxBins || spec.Parts.Count > 0)
        {
            return spec;
        }

        if (maxBins > context.Limits.MaxScaleDomainSize)
        {
            throw new VegaLiteRenderException(
                $"The timeUnit maxbins of {JsNumber.ToString(maxBins)} is more than the limit of {context.Limits.MaxScaleDomainSize} (VegaLiteLimits.MaxScaleDomainSize).",
                location);
        }

        // One pass over the valid dates decides the unit; a table without one has nothing to bin
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        var cost = spec.Utc || calendar.IsUtc ? 1 : 2;
        for (var row = 0; row < rows; row++)
        {
            context.Tick(row);
            meter.Add(cost);
            if (TryReadInstant(reader.Read(row, evaluation), out var ms))
            {
                min = Math.Min(min, ms);
                max = Math.Max(max, ms);
            }
        }

        return min <= max ? spec.ResolveMaxBins(min, max) : null;
    }
}
