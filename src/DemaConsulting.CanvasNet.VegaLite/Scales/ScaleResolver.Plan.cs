using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <content>
///     The domains that follow from the encoding plan: binned, stacked, ranged and unaggregated (a partial file of the scale resolver unit).
/// </content>
/// <remarks>
///     The domain of a continuous channel is read from the fields the plan names (<see cref="ChannelPlan.FieldPath"/> and
///     <see cref="ChannelPlan.Field2Path"/>): the extent over both for a stack (its lower and upper ends), a range (<c>x</c> and <c>x2</c>) and
///     a field that is already binned (<c>bin: "binned"</c>); <c>[0, 1]</c> for a normalized stack; <c>[start, stop]</c> of the bins the
///     encoding computed for a binned field (no zero, no nice); and, for <c>scale.domain: "unaggregated"</c> on a channel that aggregates with
///     <c>mean</c>, <c>median</c>, <c>q1</c>, <c>q3</c>, <c>min</c> or <c>max</c>, the extent of the field in the Raw table (any other
///     <c>unaggregated</c> domain falls back to the normal one, as in Vega-Lite). The zero rule, <c>nice</c> and the padding are applied by
///     the caller to the result.
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>
    ///     Finds the domain of a continuous scale before the zero rule.
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="context">The view facts.</param>
    /// <param name="binSpec">Receives the bins when the domain is that of a binned field; otherwise null.</param>
    /// <returns>The minimum and maximum; (0, 1) when there is no valid value.</returns>
    private static (double Min, double Max) LinearExtent(CommonInputs input, Context context, out BinSpec? binSpec)
    {
        binSpec = null;
        var plan = input.Plan;
        if (plan is null)
        {
            return ExtentOfFields(context.Data.Main, [], input.Datum, context);
        }

        // The bins of a field that the encoding bins: the domain runs from the start of the first bin to the stop of the last
        if (plan is { Bin: BinKind.Binning, BinPlan: { } bins, Field2Path: not null })
        {
            if (context.Data.Bins.TryGetValue(bins.StartName, out var spec))
            {
                binSpec = spec;
                return (spec.Start, spec.Stop);
            }

            return (0, 1);
        }

        // A normalized stack always spans the unit interval
        if (plan.IsStacked && context.Plan.Stack is { Offset: StackOffset.Normalize })
        {
            return (0, 1);
        }

        if (IsUnaggregatedDomain(input, plan))
        {
            return ExtentOfFields(context.Data.Raw, [plan.RawField], null, context);
        }

        var extent = ExtentOfFields(context.Data.Main, [plan.FieldPath, plan.Field2Path], input.Datum, context);

        // A field that is binned already and has no end field gives the width of its bins as a step: the last bin ends one step after its start
        if (plan is { Bin: BinKind.Binned, Field2Path: null } && plan.Definition["bin"]?["step"] is { IsNumber: true } step && step.NumberValue > 0)
        {
            return (extent.Min, extent.Max + step.NumberValue);
        }

        return extent;
    }

    /// <summary>
    ///     Determines whether a scale asks for the <c>unaggregated</c> domain and can have it: the channel aggregates with an operation that
    ///     stays in the range of the data (mean, median, quartiles, minimum, maximum).
    /// </summary>
    /// <param name="input">The channel facts.</param>
    /// <param name="plan">The plan of the channel.</param>
    /// <returns><see langword="true"/> when the domain is the extent of the field before the aggregate.</returns>
    private static bool IsUnaggregatedDomain(CommonInputs input, ChannelPlan plan) =>
        input.ScaleDef?["domain"]?.AsString() == "unaggregated"
        && plan is { Measure: { } measure, RawField: not null }
        && SharedDomainOperations.Contains(measure.Op);

    /// <summary>
    ///     Finds the extent of the valid numbers of fields of a table.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="paths">The field paths (null entries and fields the table lacks are skipped).</param>
    /// <param name="datum">The datum that gives the extent when no field is read, or null.</param>
    /// <param name="context">The view facts.</param>
    /// <returns>The minimum and maximum; (0, 1) when there is no valid value.</returns>
    private static (double Min, double Max) ExtentOfFields(DataTable table, IReadOnlyList<string?> paths, SpecNode? datum, Context context)
    {
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        if (paths.Any(p => p is not null))
        {
            foreach (var path in paths)
            {
                var field = path is null ? -1 : table.FindField(path);
                if (field < 0)
                {
                    continue;
                }

                var row = 0;
                foreach (var value in table.Column(field))
                {
                    if (++row % CancellationInterval == 0)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                    }

                    if (value.TryGetQuantitative(out var number))
                    {
                        min = Math.Min(min, number);
                        max = Math.Max(max, number);
                    }
                }
            }
        }
        else if (datum is not null && ToDataValue(datum).TryGetQuantitative(out var datumNumber))
        {
            min = max = datumNumber;
        }

        return double.IsFinite(min) ? (min, max) : (0, 1);
    }
}
