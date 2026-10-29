// Portions derived from d3-array (ISC, Mike Bostock); see third-party/README.md
// Portions derived from d3-scale (ISC, Mike Bostock) and Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     Tick values for continuous axes: the "nice" numbers (1, 2, 5 times a power of ten) that d3 and Vega choose.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="GetTicks"/>, <see cref="TickIncrement"/>, <see cref="TickStep"/> and <see cref="Nice"/> are C#
///         versions of the d3-array 3.2.4 functions <c>ticks</c>, <c>tickIncrement</c> and <c>tickStep</c> and of the
///         <c>nice</c> method of a d3-scale 4 linear scale. They keep d3's arithmetic (the thresholds sqrt(50),
///         sqrt(10) and sqrt(2); integer multiples of the step, divided rather than multiplied for fractional steps so
///         that 0.1 times 3 never shows up as 0.30000000000000004), so the same inputs give the same numbers.
///     </para>
///     <para>
///         The tick-count rules of Vega and Vega-Lite live here too: <see cref="DefaultAxisTickCount"/> (the
///         Vega-Lite default of one tick per 40 pixels), <see cref="DefaultAxisTickMinStep"/> and
///         <see cref="ResolveTickCount"/> (the <c>tickCount</c> function of vega-scale, which lowers the count until
///         the step is at least the minimum step). The type is stateless and thread-safe.
///     </para>
/// </remarks>
internal static class Ticks
{
    /// <summary>The error threshold above which the step is rounded to a 10 multiple: sqrt(50).</summary>
    private static readonly double E10 = Math.Sqrt(50);

    /// <summary>The error threshold above which the step is rounded to a 5 multiple: sqrt(10).</summary>
    private static readonly double E5 = Math.Sqrt(10);

    /// <summary>The error threshold above which the step is rounded to a 2 multiple: sqrt(2).</summary>
    private static readonly double E2 = Math.Sqrt(2);

    /// <summary>
    ///     Generates about <paramref name="count"/> evenly spaced tick values in the closed interval between two numbers.
    /// </summary>
    /// <remarks>
    ///     The ticks are multiples of 1, 2 or 5 times a power of ten, in the direction from <paramref name="start"/> to
    ///     <paramref name="stop"/> (descending when stop is below start). The result is empty when the count is not
    ///     positive or is NaN, and the single value when both ends are equal. Fewer or more ticks than asked for can
    ///     come back because the step is rounded to a nice number.
    /// </remarks>
    /// <param name="start">One end of the interval.</param>
    /// <param name="stop">The other end of the interval.</param>
    /// <param name="count">The approximate number of ticks wanted.</param>
    /// <param name="maxTicks">The largest number of ticks accepted; the default is <see cref="VegaLiteLimits.MaxTicks"/>.</param>
    /// <returns>The tick values.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the result would hold more than <paramref name="maxTicks"/> values.</exception>
    public static double[] GetTicks(double start, double stop, double count, int maxTicks = 1_000)
    {
        // Mirror d3: no ticks for a non-positive (or NaN) count, one for a degenerate interval
        if (!(count > 0))
        {
            return [];
        }

        if (!double.IsNaN(start) && start.CompareTo(stop) == 0)
        {
            return double.IsFinite(start) ? [start] : [];
        }

        // A span that overflows (for example -1e308 to 1e308) has no usable step, so it has no ticks
        if (!double.IsFinite(stop - start))
        {
            return [];
        }

        var reverse = stop < start;
        var spec = reverse ? Spec(stop, start, count) : Spec(start, stop, count);
        if (!(spec.I2 >= spec.I1))
        {
            return [];
        }

        var n = spec.I2 - spec.I1 + 1;
        if (n > maxTicks)
        {
            throw new VegaLiteRenderException(
                $"The axis would have {n:0} ticks, more than the limit of {maxTicks} (MaxTicks).");
        }

        // A negative increment stands for its reciprocal: the ticks are divided by it, which is exact where multiplying
        // by 0.1 and friends is not
        var ticks = new double[(int)n];
        for (var i = 0; i < ticks.Length; i++)
        {
            var index = reverse ? spec.I2 - i : spec.I1 + i;
            ticks[i] = spec.Inc < 0 ? index / -spec.Inc : index * spec.Inc;
        }

        // A tick that overflowed (a step the interval cannot express) never reaches the layout
        return Array.TrueForAll(ticks, double.IsFinite) ? ticks : [.. ticks.Where(double.IsFinite)];
    }

    /// <summary>
    ///     Gets the increment between the ticks of an interval, in d3's signed encoding.
    /// </summary>
    /// <remarks>
    ///     A non-negative result is the step itself (1, 2 or 5 times a power of ten that is at least 1). A negative
    ///     result is the negated reciprocal of the step for steps below 1: -10 means a step of 0.1. The encoding keeps
    ///     the arithmetic exact. The interval is taken in the order given (a reversed interval is not swapped; use
    ///     <see cref="TickStep"/> for a signed step).
    /// </remarks>
    /// <param name="first">The start of the interval.</param>
    /// <param name="second">The end of the interval.</param>
    /// <param name="count">The approximate number of ticks wanted.</param>
    /// <returns>The encoded increment; NaN when the inputs are not numbers.</returns>
    public static double TickIncrement(double first, double second, double count) => Spec(first, second, count).Inc;

    /// <summary>
    ///     Gets the step between the ticks of an interval.
    /// </summary>
    /// <remarks>
    ///     The step is positive when <paramref name="stop"/> is at or above <paramref name="start"/> and negative
    ///     otherwise; its magnitude is 1, 2 or 5 times a power of ten (also below 1).
    /// </remarks>
    /// <param name="start">The start of the interval.</param>
    /// <param name="stop">The end of the interval.</param>
    /// <param name="count">The approximate number of ticks wanted.</param>
    /// <returns>The step.</returns>
    public static double TickStep(double start, double stop, double count)
    {
        var reverse = stop < start;
        var inc = reverse ? TickIncrement(stop, start, count) : TickIncrement(start, stop, count);
        return (reverse ? -1 : 1) * (inc < 0 ? 1 / -inc : inc);
    }

    /// <summary>
    ///     Extends a domain to the nearest tick values so that it starts and ends on "nice" numbers.
    /// </summary>
    /// <remarks>
    ///     This is the <c>nice</c> method of a d3-scale linear scale: the interval is rounded outward to multiples of
    ///     the tick step for the given count, and the rounding is repeated (at most ten times) until the step settles,
    ///     because a wider interval can need a larger step. A reversed domain (stop below start) keeps its direction.
    ///     When the step is not a number or does not settle (for example for an interval of zero width, whose step is
    ///     not finite) the domain is returned unchanged.
    /// </remarks>
    /// <param name="start">The first end of the domain.</param>
    /// <param name="stop">The second end of the domain.</param>
    /// <param name="count">The approximate tick count; d3's default is 10.</param>
    /// <returns>The extended domain, in the direction of the input.</returns>
    public static (double Start, double Stop) Nice(double start, double stop, double count = 10)
    {
        // Work on an ascending interval and restore the direction at the end
        var reversed = stop < start;
        var low = reversed ? stop : start;
        var high = reversed ? start : stop;
        double? previous = null;
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var step = TickIncrement(low, high, count);
            if (previous.HasValue && step.CompareTo(previous.Value) == 0)
            {
                return reversed ? (high, low) : (low, high);
            }

            if (step > 0)
            {
                low = Math.Floor(low / step) * step;
                high = Math.Ceiling(high / step) * step;
            }
            else if (step < 0)
            {
                low = Math.Ceiling(low * step) / step;
                high = Math.Floor(high * step) / step;
            }
            else
            {
                break;
            }

            previous = step;
        }

        // The step did not settle: the domain stays as it was
        return (start, stop);
    }

    /// <summary>
    ///     Gets the number of ticks Vega-Lite asks for on a continuous x or y axis: one per 40 pixels, rounded up.
    /// </summary>
    /// <remarks>
    ///     Vega-Lite sets <c>tickCount</c> to <c>ceil(size / 40)</c>, where size is the width (or height) of the data
    ///     rectangle, for every axis whose scale is not discrete or logarithmic, unless the author gave
    ///     <c>tickCount</c> or <c>values</c>. (Binned and time-unit fields have other rules that are not part of this
    ///     package yet.)
    /// </remarks>
    /// <param name="size">The width or height of the data rectangle in pixels.</param>
    /// <returns>The tick count.</returns>
    public static double DefaultAxisTickCount(double size) => Math.Ceiling(size / 40);

    /// <summary>
    ///     Gets the minimum tick step Vega-Lite uses by default for an axis label format.
    /// </summary>
    /// <param name="format">The axis label format specifier, or null.</param>
    /// <returns>1 for the integer format <c>d</c> (so that integer axes never show fractional ticks); otherwise null.</returns>
    public static double? DefaultAxisTickMinStep(string? format) => format == "d" ? 1 : null;

    /// <summary>
    ///     Reduces a tick count so that the tick step is not smaller than a minimum step (the <c>tickCount</c>
    ///     function of vega-scale for linear scales).
    /// </summary>
    /// <remarks>
    ///     The count is first limited to the number of minimum steps that fit in the domain plus one, then lowered one
    ///     at a time while the d3 tick step for that count is still below the minimum (d3's steps grow monotonically as
    ///     the count shrinks, so the loop ends).
    /// </remarks>
    /// <param name="domainStart">The first end of the domain.</param>
    /// <param name="domainEnd">The second end of the domain.</param>
    /// <param name="count">The desired tick count.</param>
    /// <param name="minStep">The minimum step, or null for none.</param>
    /// <returns>The tick count to pass to <see cref="GetTicks"/>.</returns>
    public static double ResolveTickCount(double domainStart, double domainEnd, double count, double? minStep)
    {
        if (minStep is not { } min)
        {
            return count;
        }

        // At most one tick per minimum step; an empty or NaN span counts as one step like vega's "|| 1"
        var lo = Math.Min(domainStart, domainEnd);
        var hi = Math.Max(domainStart, domainEnd);
        var span = (hi - lo) / min;
        var steps = JsNumber.IsZero(span) || double.IsNaN(span) ? 1 : span;
        count = Math.Min(count, Math.Floor(steps) + 1);
        if (lo < hi)
        {
            while (count > 1 && TickStep(lo, hi, count) < min)
            {
                count--;
            }
        }

        return count;
    }

    /// <summary>
    ///     Gets the tick values of a continuous axis the way Vega does: the tick count is resolved against the minimum
    ///     step and then the d3 ticks of the domain are generated.
    /// </summary>
    /// <param name="domainStart">The first end of the domain.</param>
    /// <param name="domainEnd">The second end of the domain.</param>
    /// <param name="count">The desired tick count (for example <see cref="DefaultAxisTickCount"/>).</param>
    /// <param name="minStep">The minimum step, or null for none.</param>
    /// <param name="maxTicks">The largest number of ticks accepted.</param>
    /// <returns>The tick values.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the result would hold more than <paramref name="maxTicks"/> values.</exception>
    public static double[] AxisTicks(double domainStart, double domainEnd, double count, double? minStep = null, int maxTicks = 1_000) =>
        GetTicks(domainStart, domainEnd, ResolveTickCount(domainStart, domainEnd, count, minStep), maxTicks);

    /// <summary>
    ///     Works out the first and last tick index and the increment of an interval, doubling a count below 2 once when
    ///     it would leave no tick (d3's <c>tickSpec</c>).
    /// </summary>
    /// <param name="low">The start of the interval (not above the stop).</param>
    /// <param name="high">The end of the interval.</param>
    /// <param name="count">The approximate number of ticks wanted.</param>
    /// <returns>The first index, the last index and the signed increment.</returns>
    private static TickSpec Spec(double low, double high, double count)
    {
        while (true)
        {
            // The step is rounded to 1, 2 or 5 times a power of ten
            var step = (high - low) / Math.Max(0.0, count);
            var power = Math.Floor(Math.Log10(step));
            var error = step / Math.Pow(10, power);
            double factor;
            if (error >= E10)
            {
                factor = 10;
            }
            else if (error >= E5)
            {
                factor = 5;
            }
            else
            {
                factor = error >= E2 ? 2 : 1;
            }

            double i1;
            double i2;
            double inc;
            if (power < 0)
            {
                // Fractional steps are kept as their reciprocal (a negative increment) to stay exact
                inc = Math.Pow(10, -power) / factor;
                i1 = JsNumber.Round(low * inc);
                i2 = JsNumber.Round(high * inc);
                if (i1 / inc < low)
                {
                    i1++;
                }

                if (i2 / inc > high)
                {
                    i2--;
                }

                inc = -inc;
            }
            else
            {
                inc = Math.Pow(10, power) * factor;
                i1 = JsNumber.Round(low / inc);
                i2 = JsNumber.Round(high / inc);
                if (i1 * inc < low)
                {
                    i1++;
                }

                if (i2 * inc > high)
                {
                    i2--;
                }
            }

            // A tiny count can leave no tick inside: ask for twice as many
            if (i2 < i1 && count is >= 0.5 and < 2)
            {
                count *= 2;
                continue;
            }

            return new TickSpec(i1, i2, inc);
        }
    }

    /// <summary>
    ///     The first and last tick index and the signed increment of an interval.
    /// </summary>
    /// <param name="I1">The index of the first tick.</param>
    /// <param name="I2">The index of the last tick.</param>
    /// <param name="Inc">The increment; negative for the reciprocal of a fractional step.</param>
    private readonly record struct TickSpec(double I1, double I2, double Inc);
}
