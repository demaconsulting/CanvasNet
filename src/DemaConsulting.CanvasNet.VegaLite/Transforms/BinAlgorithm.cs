// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     The parameters of the bin algorithm: the <c>bin</c> object of an encoding or of a <c>bin</c> transform, with the defaults filled in.
/// </summary>
/// <remarks>
///     A parameter that the specification does not give has the default of the Vega algorithm: at most 20 bins (Vega-Lite asks for 10
///     for a position channel and for a bin transform), base 10, the divisors 5 and 2, a minimum step of 0 and <c>nice</c> on.
/// </remarks>
internal sealed record BinSettings
{
    /// <summary>Gets the most bins the algorithm may use (it ignores this when <see cref="Step"/> is given).</summary>
    public double MaxBins { get; init; } = 20;

    /// <summary>Gets the base of the step sizes (a step is a power of the base divided by one of the divisors).</summary>
    public double Base { get; init; } = 10;

    /// <summary>Gets the divisors the step may be reduced by (5 and 2 when null).</summary>
    public IReadOnlyList<double>? Divide { get; init; }

    /// <summary>Gets the exact step, or null to choose one.</summary>
    public double? Step { get; init; }

    /// <summary>Gets the allowed steps to choose from, or null.</summary>
    public IReadOnlyList<double>? Steps { get; init; }

    /// <summary>Gets the smallest step the algorithm may choose.</summary>
    public double MinStep { get; init; }

    /// <summary>Gets a value indicating whether the extent is rounded out to whole steps.</summary>
    public bool Nice { get; init; } = true;

    /// <summary>Gets the value a bin boundary is shifted to align with, or null.</summary>
    public double? Anchor { get; init; }

    /// <summary>Gets the extent the bins cover instead of the extent of the data, or null.</summary>
    public (double Min, double Max)? Extent { get; init; }

    /// <summary>
    ///     Reads the parameters from the <c>bin</c> value of a specification.
    /// </summary>
    /// <param name="bin">The value: an object of bin parameters, or anything else (true) for the defaults; null is the defaults.</param>
    /// <param name="defaultMaxBins">The number of bins to ask for when the object gives neither <c>maxbins</c> nor <c>step</c>.</param>
    /// <returns>The settings. The parser has validated the shape of the value, so a parameter of the wrong kind is ignored.</returns>
    public static BinSettings Read(SpecNode? bin, double defaultMaxBins)
    {
        if (bin is not { IsObject: true })
        {
            return new BinSettings { MaxBins = defaultMaxBins };
        }

        double? step = bin["step"] is { IsNumber: true } stepNode ? stepNode.NumberValue : null;
        var maxBins = step is null ? defaultMaxBins : 20;
        if (bin["maxbins"] is { IsNumber: true } maxNode)
        {
            maxBins = maxNode.NumberValue;
        }
        (double, double)? extent = bin["extent"] is { IsArray: true, Items.Count: 2 } extentNode && extentNode.Items.All(i => i.IsNumber)
            ? (extentNode.Items[0].NumberValue, extentNode.Items[1].NumberValue)
            : null;
        return new BinSettings
        {
            MaxBins = maxBins,
            Base = bin["base"] is { IsNumber: true } baseNode ? baseNode.NumberValue : 10,
            Divide = bin["divide"] is { IsArray: true } divideNode ? [.. divideNode.Items.Where(i => i.IsNumber).Select(i => i.NumberValue)] : null,
            Step = step,
            Steps = bin["steps"] is { IsArray: true } stepsNode ? [.. stepsNode.Items.Where(i => i.IsNumber).Select(i => i.NumberValue)] : null,
            MinStep = bin["minstep"] is { IsNumber: true } minNode ? minNode.NumberValue : 0,
            Nice = bin["nice"] is not { IsBoolean: true } niceNode || niceNode.BoolValue,
            Anchor = bin["anchor"] is { IsNumber: true } anchorNode ? anchorNode.NumberValue : null,
            Extent = extent,
        };
    }
}

/// <summary>
///     The bins of a field: where they start and stop and how wide each is.
/// </summary>
/// <param name="Start">The lower boundary of the first bin.</param>
/// <param name="Stop">The upper boundary of the last bin.</param>
/// <param name="Step">The width of a bin; greater than zero.</param>
internal readonly record struct BinSpec(double Start, double Stop, double Step)
{
    /// <summary>Gets the number of bins.</summary>
    public double BinCount => Math.Ceiling((Stop - Start) / Step);
}

/// <summary>
///     The bin algorithm of Vega: chooses a bin width for an extent and assigns values to bins.
/// </summary>
/// <remarks>
///     <para>
///         <b>Choosing the step</b> (<see cref="Compute"/>) is a port of Vega's <c>bin</c> of <c>vega-statistics</c>. With neither an
///         exact <c>step</c> nor a list of <c>steps</c> the step starts at <c>max(minstep, base^(round(log_base(span)) - level))</c>,
///         where <c>span</c> is the extent (its magnitude when the extent is a single value, 1 for zero) and <c>level</c> is
///         <c>ceil(log_base(maxbins))</c>; it is multiplied by the base while the span needs more than <c>maxbins</c> bins, and
///         then divided by each divisor in turn when that still fits. A list of steps chooses the largest listed step that is
///         smaller than <c>span / maxbins</c> (the first one when none is). With <c>nice</c> the extent is rounded out to whole
///         steps; the result is the boundaries and the step.
///     </para>
///     <para>
///         <b>Assigning</b> (<see cref="Assign"/> and <see cref="End"/>) follows Vega's <c>bin</c> transform: <see cref="Align"/> first rounds the
///         stop up to a whole step and, with an <c>anchor</c>, shifts the boundaries so that one aligns with it. A value below
///         the start is in the bin at negative infinity, a value above the stop in the one at positive infinity, the last bin
///         includes its upper boundary, and a small epsilon keeps a value such as 0.3 out of the bin below it despite rounding error.
///     </para>
///     <para>
///         <b>Differences from Vega.</b> Vega can loop for ever when the step underflows to zero or the base is so close to 1 that
///         the step cannot grow; the growth loop here is bounded by <see cref="MaxGrowthSteps"/> iterations and the algorithm fails
///         with a located <see cref="VegaLiteRenderException"/> when no finite positive step exists. The number of bins is bounded by
///         <see cref="VegaLiteLimits.MaxScaleDomainSize"/> (also for an exact <c>step</c>), because the bins of a discrete axis become
///         the categories of a scale. JavaScript's <c>Math.round</c> (half toward positive infinity) is used, not banker's rounding.
///     </para>
/// </remarks>
internal static class BinAlgorithm
{
    /// <summary>The tolerance that keeps a value on a bin boundary in the bin above it (Vega's <c>EPSILON</c>).</summary>
    private const double Epsilon = 1e-14;

    /// <summary>The most times the step may be multiplied by the base while looking for a step that fits the bin count.</summary>
    internal const int MaxGrowthSteps = 2048;

    /// <summary>The divisors used when the settings give none.</summary>
    private static readonly double[] DefaultDivide = [5, 2];

    /// <summary>
    ///     Chooses the bins for an extent (Vega's <c>bin</c>).
    /// </summary>
    /// <param name="min">The smallest value; finite.</param>
    /// <param name="max">The largest value; finite and not less than <paramref name="min"/>.</param>
    /// <param name="settings">The bin parameters.</param>
    /// <param name="maxBinsLimit">The most bins allowed (<see cref="VegaLiteLimits.MaxScaleDomainSize"/>).</param>
    /// <param name="location">The location reported when the bins cannot be computed.</param>
    /// <returns>The boundaries and the step, before <see cref="Align"/>.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when no finite step exists or the bins would exceed <paramref name="maxBinsLimit"/>.</exception>
    public static BinSpec Compute(double min, double max, BinSettings settings, int maxBinsLimit, VegaLiteLocation location)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!double.IsFinite(min) || !double.IsFinite(max) || min > max)
        {
            throw Cannot(min, max, "the extent must be two finite numbers in ascending order", location);
        }

        var maxBins = settings.MaxBins;
        if (settings.Step is null && maxBins > maxBinsLimit)
        {
            throw TooManyBins(maxBins, maxBinsLimit, location);
        }

        var logBase = Math.Log(settings.Base);
        var span = max - min;
        if (JsNumber.IsZero(span))
        {
            span = Math.Abs(min);
        }

        if (JsNumber.IsZero(span))
        {
            span = 1;
        }

        if (!double.IsFinite(span))
        {
            throw Cannot(min, max, "the extent is too wide", location);
        }

        var step = ChooseStep(span, settings, logBase, min, max, location);
        if (!double.IsFinite(step) || step <= 0)
        {
            throw Cannot(min, max, "the bin step is not a positive number", location);
        }

        // The precision of the step decides the tolerance of the rounding below
        var logStep = Math.Log(step);
        var precision = logStep >= 0 ? 0 : (int)(-logStep / logBase) + 1;
        var epsilon = Math.Pow(settings.Base, -precision - 1);
        if (settings.Nice)
        {
            var floor = Math.Floor((min / step) + epsilon) * step;
            min = min < floor ? floor - step : floor;
            max = Math.Ceiling(max / step) * step;
        }

        var stop = max.CompareTo(min) == 0 ? min + step : max;
        if (!double.IsFinite(min) || !double.IsFinite(stop))
        {
            throw Cannot(min, max, "the bin boundaries are not finite numbers", location);
        }

        var spec = new BinSpec(min, stop, step);
        if (spec.BinCount > maxBinsLimit)
        {
            throw TooManyBins(spec.BinCount, maxBinsLimit, location);
        }

        return spec;
    }

    /// <summary>
    ///     Chooses the bins for an extent and aligns them (the <c>bin</c> transform of Vega: <see cref="Compute"/> then <see cref="Align"/>).
    /// </summary>
    /// <param name="min">The smallest value; finite.</param>
    /// <param name="max">The largest value; finite and not less than <paramref name="min"/>.</param>
    /// <param name="settings">The bin parameters.</param>
    /// <param name="maxBinsLimit">The most bins allowed.</param>
    /// <param name="location">The location reported when the bins cannot be computed.</param>
    /// <returns>The bins.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when no finite bins exist or they would exceed <paramref name="maxBinsLimit"/>.</exception>
    public static BinSpec Resolve(double min, double max, BinSettings settings, int maxBinsLimit, VegaLiteLocation location)
    {
        var spec = Align(Compute(min, max, settings, maxBinsLimit, location), settings.Anchor);
        if (!double.IsFinite(spec.Start) || !double.IsFinite(spec.Stop))
        {
            throw Cannot(min, max, "the anchor moves the bin boundaries out of range", location);
        }

        return spec;
    }

    /// <summary>
    ///     Rounds the stop of the bins up to a whole number of steps and shifts the boundaries to align with an anchor.
    /// </summary>
    /// <param name="spec">The bins from <see cref="Compute"/>.</param>
    /// <param name="anchor">The value a boundary must align with, or null.</param>
    /// <returns>The aligned bins.</returns>
    public static BinSpec Align(BinSpec spec, double? anchor)
    {
        var start = spec.Start;
        var stop = start + (Math.Ceiling((spec.Stop - start) / spec.Step) * spec.Step);
        if (anchor is { } a)
        {
            var shift = a - (start + (spec.Step * Math.Floor((a - start) / spec.Step)));
            start += shift;
            stop += shift;
        }

        return new BinSpec(start, stop, spec.Step);
    }

    /// <summary>
    ///     Gets the lower boundary of the bin a value falls in.
    /// </summary>
    /// <param name="value">The value; not NaN.</param>
    /// <param name="spec">The aligned bins.</param>
    /// <returns>The lower boundary; negative infinity below the bins and positive infinity above them.</returns>
    public static double Assign(double value, BinSpec spec)
    {
        if (value < spec.Start)
        {
            return double.NegativeInfinity;
        }

        if (value > spec.Stop)
        {
            return double.PositiveInfinity;
        }

        // The last bin is closed: the stop itself belongs to the bin below it
        var clamped = Math.Max(spec.Start, Math.Min(value, spec.Stop - spec.Step));
        return spec.Start + (spec.Step * Math.Floor(Epsilon + ((clamped - spec.Start) / spec.Step)));
    }

    /// <summary>
    ///     Gets the upper boundary of a bin.
    /// </summary>
    /// <param name="binStart">The lower boundary from <see cref="Assign"/>.</param>
    /// <param name="spec">The aligned bins.</param>
    /// <returns>The upper boundary (infinite for the bins at infinity).</returns>
    public static double End(double binStart, BinSpec spec) =>
        spec.Start + (spec.Step * (1 + ((binStart - spec.Start) / spec.Step)));

    /// <summary>
    ///     Chooses the step before the extent is rounded.
    /// </summary>
    /// <param name="span">The span of the extent.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="logBase">The natural logarithm of the base.</param>
    /// <param name="min">The smallest value, for the message.</param>
    /// <param name="max">The largest value, for the message.</param>
    /// <param name="location">The location of the failure.</param>
    /// <returns>The step.</returns>
    private static double ChooseStep(double span, BinSettings settings, double logBase, double min, double max, VegaLiteLocation location)
    {
        if (settings.Step is { } exact)
        {
            return exact;
        }

        var maxBins = settings.MaxBins;
        if (settings.Steps is { Count: > 0 } steps)
        {
            var target = span / maxBins;
            var i = 0;
            while (i < steps.Count && steps[i] < target)
            {
                i++;
            }

            return steps[Math.Max(0, i - 1)];
        }

        var level = Math.Ceiling(Math.Log(maxBins) / logBase);
        var step = Math.Max(settings.MinStep, Math.Pow(settings.Base, JsNumber.Round(Math.Log(span) / logBase) - level));
        if (!double.IsFinite(step) || step <= 0)
        {
            throw Cannot(min, max, "the bin step is not a positive number", location);
        }

        // Increase the step while there are too many bins; the loop is bounded because a base close to 1 may never get there
        var growth = 0;
        while (Math.Ceiling(span / step) > maxBins)
        {
            growth++;
            step *= settings.Base;
            if (growth > MaxGrowthSteps || !double.IsFinite(step))
            {
                throw Cannot(min, max, $"no step was found within {MaxGrowthSteps} growth steps of the base", location);
            }
        }

        // Decrease the step again where that is allowed
        foreach (var divisor in settings.Divide ?? DefaultDivide)
        {
            var smaller = step / divisor;
            if (smaller >= settings.MinStep && span / smaller <= maxBins)
            {
                step = smaller;
            }
        }

        return step;
    }

    /// <summary>
    ///     Creates the exception for an extent that cannot be binned.
    /// </summary>
    /// <param name="min">The smallest value.</param>
    /// <param name="max">The largest value.</param>
    /// <param name="reason">Why.</param>
    /// <param name="location">The location.</param>
    /// <returns>The exception.</returns>
    private static VegaLiteRenderException Cannot(double min, double max, string reason, VegaLiteLocation location) =>
        new($"The values cannot be binned: {reason} (extent [{JsNumber.ToString(min)}, {JsNumber.ToString(max)}]).", location);

    /// <summary>
    ///     Creates the exception for a bin count above the limit.
    /// </summary>
    /// <param name="count">The number of bins.</param>
    /// <param name="limit">The limit.</param>
    /// <param name="location">The location.</param>
    /// <returns>The exception.</returns>
    private static VegaLiteRenderException TooManyBins(double count, int limit, VegaLiteLocation location) =>
        new($"The bin has {JsNumber.ToString(count)} bins, more than the limit of {limit} (VegaLiteLimits.MaxScaleDomainSize).", location);
}
