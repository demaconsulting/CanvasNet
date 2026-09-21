using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Evaluates a <see cref="Gradient"/>'s color at individual points or whole pixel rows, for
///     use by <see cref="ScanlineRasterizer"/>'s gradient <see cref="ScanlineRasterizer.Fill(Surface, IReadOnlyList{List{Vector2}}, Gradient, FillRule, Geometry.Rect)"/>
///     overload.
/// </summary>
/// <remarks>
///     <para>
///     <b>Evaluation pipeline (per point).</b>
///     <list type="number">
///     <item>
///     <see cref="Gradient.Transform"/> is inverted. If it is singular (non-invertible) or
///     produces a non-finite inverse, the whole gradient is treated as the "Degenerate Transform"
///     case: it flat-fills with the last stop's color (post-sort), matching the documented
///     degenerate-case policy below.
///     </item>
///     <item>The point is mapped into gradient-defining coordinates via the inverted transform.</item>
///     <item>
///     A raw (unbounded) gradient parameter <c>t</c> is computed: for <see cref="LinearGradient"/>,
///     by projecting the gradient-space point onto the <c>End - Start</c> vector; for
///     <see cref="RadialGradient"/>, by solving the standard two-circle ("conical") gradient
///     quadratic for the largest <c>t</c> at which the interpolated circle
///     <c>(center(t), radius(t))</c> - with <c>center(t) = lerp(StartCenter, EndCenter, t)</c>,
///     <c>radius(t) = lerp(StartRadius, EndRadius, t)</c> - passes through the point, subject to
///     <c>radius(t) &gt;= 0</c>. Both computations are performed in <see cref="double"/> precision
///     (never <see cref="float"/>) so that extreme-magnitude coordinates cannot overflow a
///     squared-distance or quadratic-coefficient intermediate term even when every input
///     coordinate and the true mathematical result are both well within <see cref="float"/>'s
///     range - the same numerical safeguard <c>StrokeOutliner</c> already applies to its own
///     geometry.
///     </item>
///     <item>
///     <see cref="Gradient.Spread"/> folds the raw <c>t</c> into <c>[0, 1]</c>: <see cref="GradientSpread.Pad"/>
///     clamps; <see cref="GradientSpread.Repeat"/> floor-mods (never naively via <c>%</c>, which is
///     negative for a negative dividend in C#); <see cref="GradientSpread.Reflect"/> folds into a
///     period-2 triangle wave.
///     </item>
///     <item>
///     The folded <c>t</c> resolves a color from the sorted stop list: a single-stop gradient
///     always returns that stop's color; <c>t</c> at or before the first stop resolves to the
///     first stop's color, and at or after the last stop to the last stop's color (this is also
///     where the "Degenerate Transform" and "Zero-Length Linear Vector" fallbacks plug in - both
///     are defined as "paint with the last stop's color"); otherwise, the bracketing consecutive
///     stop pair is found and interpolated <b>in premultiplied alpha space</b> (to avoid visible
///     fringing across a partial-alpha stop - for example, a naive straight-alpha lerp from
///     opaque red to transparent white visibly passes through a washed-out pink/gray band partway
///     through the transition; premultiplied interpolation does not), then unpremultiplied back
///     to straight alpha using the same "alpha == 0 implies R = G = B = 0" convention documented
///     by <see cref="Surface.UnpremultiplyAlpha"/>. A pair of stops sharing the same offset (a
///     "hard stop") resolves to the later stop's color outright, without interpolating (avoiding
///     a <c>0/0</c> division).
///     </item>
///     </list>
///     </para>
///     <para>
///     <b>Degenerate-case policy (single unifying rule).</b> Every geometric degenerate case that
///     cannot define a meaningful gradient direction - a singular <see cref="Gradient.Transform"/>,
///     a zero-length <see cref="LinearGradient"/> vector (<see cref="LinearGradient.Start"/> equal
///     to <see cref="LinearGradient.End"/>), and a <see cref="RadialGradient"/> whose two circles
///     never change with <c>t</c> at all (<see cref="RadialGradient.StartRadius"/> and
///     <see cref="RadialGradient.EndRadius"/> both zero, and <see cref="RadialGradient.StartCenter"/>
///     equal to <see cref="RadialGradient.EndCenter"/>) - resolves to flat-filling with the last
///     stop's color. This is distinct from, and does not apply to, the two-circle radial
///     gradient's "no valid root" region (a legitimate, spatially varying part of an otherwise
///     well-defined two-circle gradient - the point lies outside every circle the gradient's
///     family of circles ever sweeps through), which is instead left <b>unpainted</b> (returned
///     with alpha zero, a true compositing no-op at that pixel) - matching the CSS/Skia convention
///     that such pixels are left unpainted rather than clamped to an edge color.
///     </para>
/// </remarks>
internal static class GradientEvaluator
{
    /// <summary>
    ///     The squared-length threshold, in gradient-defining coordinates, below which a
    ///     <see cref="LinearGradient"/>'s <c>End - Start</c> vector is treated as exactly
    ///     zero-length (the "Zero-Length Linear Vector" degenerate case).
    /// </summary>
    private const double ZeroLengthSquaredThreshold = 1e-12;

    /// <summary>
    ///     The magnitude threshold below which the two-circle radial quadratic's leading
    ///     coefficient (or, in the linear-equation fallback, its only coefficient) is treated as
    ///     exactly zero.
    /// </summary>
    private const double NearZeroCoefficient = 1e-9;

    /// <summary>
    ///     Evaluates <paramref name="gradient"/> once per pixel center of a horizontal run of
    ///     <paramref name="count"/> pixels starting at column <paramref name="x"/> on row
    ///     <paramref name="y"/>, writing each pixel's color into <paramref name="destination"/>.
    /// </summary>
    /// <param name="gradient">The gradient to evaluate. Must not be <see langword="null"/>.</param>
    /// <param name="y">The zero-based row, in the same coordinate space <paramref name="gradient"/>'s <see cref="Gradient.Transform"/> maps into.</param>
    /// <param name="x">The zero-based column at which the run starts.</param>
    /// <param name="count">The number of pixels to evaluate.</param>
    /// <param name="destination">
    ///     The destination span to receive each pixel's color, one entry per pixel. Must have a
    ///     length of at least <paramref name="count"/>.
    /// </param>
    internal static void EvaluateRow(Gradient gradient, int y, int x, int count, Span<Rgba32> destination)
    {
        ArgumentNullException.ThrowIfNull(gradient);

        for (var i = 0; i < count; i++)
        {
            var point = new Vector2(x + i + 0.5f, y + 0.5f);
            destination[i] = EvaluatePoint(gradient, point);
        }
    }

    /// <summary>
    ///     Evaluates <paramref name="gradient"/>'s color at a single point, in the same coordinate
    ///     space <paramref name="gradient"/>'s <see cref="Gradient.Transform"/> maps into.
    /// </summary>
    /// <param name="gradient">The gradient to evaluate. Must not be <see langword="null"/>.</param>
    /// <param name="point">The point to evaluate.</param>
    /// <returns>
    ///     The resolved color, per this class's remarks; a fully transparent (alpha zero)
    ///     <see cref="Rgba32"/> for a point inside a <see cref="RadialGradient"/>'s "no valid root"
    ///     region.
    /// </returns>
    internal static Rgba32 EvaluatePoint(Gradient gradient, Vector2 point)
    {
        ArgumentNullException.ThrowIfNull(gradient);

        if (!Matrix3x2.Invert(gradient.Transform, out var inverse) || !IsFinite(inverse))
        {
            return LastStopColor(gradient.Stops);
        }

        var gradientPoint = Vector2.Transform(point, inverse);

        return gradient switch
        {
            LinearGradient linear => EvaluateLinear(linear, gradientPoint),
            RadialGradient radial => EvaluateRadial(radial, gradientPoint),
            _ => throw new NotSupportedException($"Unsupported gradient type '{gradient.GetType()}'."),
        };
    }

    /// <summary>
    ///     Evaluates a <see cref="LinearGradient"/> at an already gradient-space point.
    /// </summary>
    private static Rgba32 EvaluateLinear(LinearGradient linear, Vector2 gradientPoint)
    {
        double dirX = linear.End.X - linear.Start.X;
        double dirY = linear.End.Y - linear.Start.Y;
        var lengthSquared = dirX * dirX + dirY * dirY;

        if (lengthSquared <= ZeroLengthSquaredThreshold)
        {
            // Zero-Length Linear Vector degenerate case - see this class's remarks.
            return LastStopColor(linear.Stops);
        }

        double px = gradientPoint.X - linear.Start.X;
        double py = gradientPoint.Y - linear.Start.Y;
        var t = (px * dirX + py * dirY) / lengthSquared;

        var folded = ApplySpread(t, linear.Spread);
        return ResolveColor(linear.Stops, folded);
    }

    /// <summary>
    ///     Evaluates a <see cref="RadialGradient"/> at an already gradient-space point.
    /// </summary>
    private static Rgba32 EvaluateRadial(RadialGradient radial, Vector2 gradientPoint)
    {
        if (radial.StartRadius == 0f && radial.EndRadius == 0f && radial.StartCenter == radial.EndCenter)
        {
            // Both radii zero and both centers equal: the family of circles never changes with
            // t at all - see this class's remarks.
            return LastStopColor(radial.Stops);
        }

        var t = SolveRadialParameter(radial, gradientPoint);
        if (t is null)
        {
            // No valid root: outside every circle in the gradient's swept family - left
            // unpainted, per this class's remarks.
            return new Rgba32(0, 0, 0, 0);
        }

        var folded = ApplySpread(t.Value, radial.Spread);
        return ResolveColor(radial.Stops, folded);
    }

    /// <summary>
    ///     Solves the standard two-circle ("conical") gradient quadratic for the largest valid
    ///     <c>t</c> (subject to <c>radius(t) &gt;= 0</c>) at which the interpolated circle
    ///     <c>(center(t), radius(t))</c> passes through <paramref name="gradientPoint"/>, entirely
    ///     in <see cref="double"/> precision.
    /// </summary>
    /// <returns>The largest valid <c>t</c>, or <see langword="null"/> if no valid root exists.</returns>
    private static double? SolveRadialParameter(RadialGradient radial, Vector2 gradientPoint)
    {
        double dx = radial.EndCenter.X - radial.StartCenter.X;
        double dy = radial.EndCenter.Y - radial.StartCenter.Y;
        double dr = radial.EndRadius - radial.StartRadius;

        double pdx = gradientPoint.X - radial.StartCenter.X;
        double pdy = gradientPoint.Y - radial.StartCenter.Y;

        var a = (dx * dx) + (dy * dy) - (dr * dr);
        var b = -2.0 * ((pdx * dx) + (pdy * dy) + (radial.StartRadius * dr));
        var c = (pdx * pdx) + (pdy * pdy) - ((double)radial.StartRadius * radial.StartRadius);

        if (Math.Abs(a) <= NearZeroCoefficient)
        {
            if (Math.Abs(b) <= NearZeroCoefficient)
            {
                return null;
            }

            var t = -c / b;
            return IsValidRoot(radial, t) ? t : null;
        }

        var discriminant = (b * b) - (4.0 * a * c);
        if (discriminant < 0.0)
        {
            return null;
        }

        var sqrtDiscriminant = Math.Sqrt(discriminant);
        var t0 = (-b + sqrtDiscriminant) / (2.0 * a);
        var t1 = (-b - sqrtDiscriminant) / (2.0 * a);

        var high = Math.Max(t0, t1);
        var low = Math.Min(t0, t1);

        if (IsValidRoot(radial, high))
        {
            return high;
        }

        return IsValidRoot(radial, low) ? low : null;
    }

    /// <summary>
    ///     Determines whether <paramref name="t"/> is finite and produces a non-negative
    ///     interpolated radius - the validity condition every candidate root of the two-circle
    ///     quadratic must satisfy.
    /// </summary>
    private static bool IsValidRoot(RadialGradient radial, double t)
    {
        if (!double.IsFinite(t))
        {
            return false;
        }

        var radius = radial.StartRadius + (t * (radial.EndRadius - radial.StartRadius));
        return radius >= 0.0;
    }

    /// <summary>
    ///     Folds a raw (unbounded) gradient parameter into <c>[0, 1]</c> per <paramref name="spread"/>.
    /// </summary>
    private static double ApplySpread(double t, GradientSpread spread)
    {
        switch (spread)
        {
            case GradientSpread.Repeat:
                // Floor-mod, not "t % 1.0" - the CLR's "%" is negative for a negative dividend,
                // which would wrap a negative t to the wrong side of the ramp.
                return t - Math.Floor(t);

            case GradientSpread.Reflect:
            {
                var u = t - (2.0 * Math.Floor(t / 2.0));
                return u <= 1.0 ? u : 2.0 - u;
            }

            case GradientSpread.Pad:
            default:
                return Math.Clamp(t, 0.0, 1.0);
        }
    }

    /// <summary>
    ///     Resolves a color from the sorted, non-empty <paramref name="stops"/> list at gradient
    ///     parameter <paramref name="t"/> (already folded into <c>[0, 1]</c> by <see cref="ApplySpread"/>).
    /// </summary>
    private static Rgba32 ResolveColor(IReadOnlyList<GradientStop> stops, double t)
    {
        if (stops.Count == 1)
        {
            return stops[0].Color;
        }

        var first = stops[0];
        if (t <= first.Offset)
        {
            return first.Color;
        }

        var last = stops[^1];
        if (t >= last.Offset)
        {
            return last.Color;
        }

        for (var i = 0; i < stops.Count - 1; i++)
        {
            var a = stops[i];
            var b = stops[i + 1];

            if (t < a.Offset || t > b.Offset)
            {
                continue;
            }

            if (b.Offset - a.Offset <= float.Epsilon)
            {
                // Hard stop: two (or more) stops sharing the same offset - resolve to the later
                // stop's color outright, avoiding a 0/0 division.
                return b.Color;
            }

            var fraction = (t - a.Offset) / (b.Offset - a.Offset);
            return LerpPremultiplied(a.Color, b.Color, fraction);
        }

        // Unreachable given the bracketing checks above (every t between first.Offset and
        // last.Offset is covered by some consecutive pair), but guards against ever falling
        // through without a defined result.
        return last.Color;
    }

    /// <summary>
    ///     Linearly interpolates between <paramref name="a"/> and <paramref name="b"/> in
    ///     premultiplied alpha space, then unpremultiplies the result back to straight alpha -
    ///     using the same "alpha == 0 implies R = G = B = 0" convention documented by
    ///     <see cref="Surface.UnpremultiplyAlpha"/> - avoiding the visible fringing a naive
    ///     straight-alpha lerp produces across a partial-alpha stop.
    /// </summary>
    private static Rgba32 LerpPremultiplied(Rgba32 a, Rgba32 b, double fraction)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);

        var aAlpha = a.A / 255.0;
        var aPr = a.R * aAlpha;
        var aPg = a.G * aAlpha;
        var aPb = a.B * aAlpha;

        var bAlpha = b.A / 255.0;
        var bPr = b.R * bAlpha;
        var bPg = b.G * bAlpha;
        var bPb = b.B * bAlpha;

        var outPa = a.A + ((b.A - a.A) * fraction);
        var outPr = aPr + ((bPr - aPr) * fraction);
        var outPg = aPg + ((bPg - aPg) * fraction);
        var outPb = aPb + ((bPb - aPb) * fraction);

        if (outPa <= 0.0)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        var outA = ClampToByte(outPa);
        var outR = ClampToByte(outPr * 255.0 / outPa);
        var outG = ClampToByte(outPg * 255.0 / outPa);
        var outB = ClampToByte(outPb * 255.0 / outPa);

        return new Rgba32(outR, outG, outB, outA);
    }

    /// <summary>
    ///     Rounds (half-away-from-zero) and clamps <paramref name="value"/> to a valid
    ///     <see cref="byte"/> channel value.
    /// </summary>
    private static byte ClampToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0.0, 255.0);

    /// <summary>
    ///     Returns the last (post-sort) stop's color - the flat-fill result used by every
    ///     degenerate case documented in this class's remarks.
    /// </summary>
    private static Rgba32 LastStopColor(IReadOnlyList<GradientStop> stops) => stops[^1].Color;

    /// <summary>
    ///     Determines whether every component of <paramref name="matrix"/> is a finite value.
    /// </summary>
    private static bool IsFinite(Matrix3x2 matrix) =>
        float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) &&
        float.IsFinite(matrix.M21) && float.IsFinite(matrix.M22) &&
        float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32);
}
