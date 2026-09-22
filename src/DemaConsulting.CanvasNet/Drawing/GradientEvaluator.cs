// cspell:ignore precomputation
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
///     <see cref="Gradient.Transform"/> is inverted <b>once per <see cref="EvaluateRow"/> call</b>
///     (or once per <see cref="EvaluatePoint"/> call), never once per pixel - see "Per-fill
///     precomputation" below. If it is singular (non-invertible) or produces a non-finite
///     inverse, the whole gradient is treated as the "Degenerate Transform" case: it flat-fills
///     with the last stop's color (post-sort), matching the documented degenerate-case policy
///     below.
///     </item>
///     <item>The point is mapped into gradient-defining coordinates via the inverted transform.</item>
///     <item>
///     A raw (unbounded) gradient parameter <c>t</c> is computed: for <see cref="LinearGradient"/>,
///     by projecting the gradient-space point onto the <c>End - Start</c> vector; for
///     <see cref="RadialGradient"/>, by solving the standard two-circle ("conical") gradient
///     quadratic for the appropriate <c>t</c> at which the interpolated circle
///     <c>(center(t), radius(t))</c> - with <c>center(t) = lerp(StartCenter, EndCenter, t)</c>,
///     <c>radius(t) = lerp(StartRadius, EndRadius, t)</c> - passes through the point, subject to
///     <c>radius(t) &gt;= 0</c>. When only one candidate root satisfies that condition, it is used
///     unconditionally; when the swept family's two circles are not nested in one another (the
///     quadratic's leading coefficient is positive) both roots can independently satisfy
///     <c>radius(t) &gt;= 0</c> for a point in a narrow self-intersecting sliver of the family
///     nearest whichever endpoint circle the point sits closest to - there, the root closer to
///     that nearer endpoint (not simply the numerically larger root) is selected, so a point
///     exactly on the start (or end) circle still resolves to that circle's own stop color
///     instead of a barely-off-boundary interpolated color from the spurious second root; see
///     <see cref="SolveRadialParameter"/>. Both computations are performed in
///     <see cref="double"/> precision (never <see cref="float"/>) so that extreme-magnitude
///     coordinates cannot overflow a squared-distance or quadratic-coefficient intermediate term
///     even when every input coordinate and the true mathematical result are both well within
///     <see cref="float"/>'s range - the same numerical safeguard <c>StrokeOutliner</c> already
///     applies to its own geometry.
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
///     <para>
///     <b>Per-fill precomputation.</b> Every quantity that depends only on the
///     <see cref="Gradient"/> itself - never on the point or pixel being evaluated - is computed
///     exactly once per <see cref="EvaluateRow"/> (or <see cref="EvaluatePoint(Gradient,Vector2)"/>)
///     call and threaded through to the per-pixel work via <see cref="GradientPlan"/>, rather than
///     being recomputed for every pixel in a fill. This covers <see cref="Gradient.Transform"/>'s
///     matrix inverse, the <see cref="LinearGradient"/> direction vector and its squared length,
///     and the <see cref="RadialGradient"/> two-circle quadratic's per-fill-invariant coefficients
///     (everything except the point-dependent terms). Only the handful of quantities that
///     genuinely depend on the point being evaluated - the transformed point itself, and the
///     quadratic's point-dependent coefficients - are recomputed per pixel.
///     </para>
/// </remarks>
internal static class GradientEvaluator
{
    /// <summary>
    ///     Holds every quantity <see cref="EvaluatePoint(Gradient,Vector2)"/>/<see cref="EvaluateRow"/>
    ///     can compute once per <see cref="Gradient"/> and reuse for every point evaluated against
    ///     it, instead of recomputing per pixel - see this class's remarks.
    /// </summary>
    private readonly record struct GradientPlan
    {
        /// <summary>The gradient this plan was built for.</summary>
        public required Gradient Gradient { get; init; }

        /// <summary>
        ///     Whether <see cref="Gradient.Transform"/> is invertible with a finite inverse; when
        ///     <see langword="false"/>, every point flat-fills with the last stop's color (the
        ///     "Degenerate Transform" case) and no other field of this plan is meaningful.
        /// </summary>
        public required bool HasInverseTransform { get; init; }

        /// <summary>The inverse of <see cref="Gradient.Transform"/>, when <see cref="HasInverseTransform"/>.</summary>
        public Matrix3x2 InverseTransform { get; init; }

        /// <summary>For a <see cref="LinearGradient"/>: the (<see cref="LinearGradient.End"/> - <see cref="LinearGradient.Start"/>) vector's X component.</summary>
        public double LinearDirX { get; init; }

        /// <summary>For a <see cref="LinearGradient"/>: the (<see cref="LinearGradient.End"/> - <see cref="LinearGradient.Start"/>) vector's Y component.</summary>
        public double LinearDirY { get; init; }

        /// <summary>For a <see cref="LinearGradient"/>: the squared length of the direction vector above.</summary>
        public double LinearLengthSquared { get; init; }

        /// <summary>For a <see cref="RadialGradient"/>: whether both radii and both centers are equal (the fully degenerate case).</summary>
        public bool RadialIsFullyDegenerate { get; init; }

        /// <summary>For a <see cref="RadialGradient"/>: <see cref="RadialGradient.EndCenter"/>.X - <see cref="RadialGradient.StartCenter"/>.X.</summary>
        public double RadialDx { get; init; }

        /// <summary>For a <see cref="RadialGradient"/>: <see cref="RadialGradient.EndCenter"/>.Y - <see cref="RadialGradient.StartCenter"/>.Y.</summary>
        public double RadialDy { get; init; }

        /// <summary>For a <see cref="RadialGradient"/>: <see cref="RadialGradient.EndRadius"/> - <see cref="RadialGradient.StartRadius"/>.</summary>
        public double RadialDr { get; init; }

        /// <summary>For a <see cref="RadialGradient"/>: the two-circle quadratic's leading coefficient, <c>RadialDx^2 + RadialDy^2 - RadialDr^2</c>.</summary>
        public double RadialA { get; init; }
    }
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

        var plan = BuildPlan(gradient);

        if (!plan.HasInverseTransform)
        {
            // Degenerate Transform case applies to the whole fill, not just this row - resolve
            // it once for the entire run instead of per pixel.
            var flatFill = LastStopColor(gradient.Stops);
            for (var i = 0; i < count; i++)
            {
                destination[i] = flatFill;
            }

            return;
        }

        for (var i = 0; i < count; i++)
        {
            var point = new Vector2(x + i + 0.5f, y + 0.5f);
            destination[i] = EvaluateWithPlan(in plan, point);
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

        var plan = BuildPlan(gradient);
        return EvaluateWithPlan(in plan, point);
    }

    /// <summary>
    ///     Computes every per-<paramref name="gradient"/> (never per-point) quantity once, per
    ///     this class's "Per-fill precomputation" remarks.
    /// </summary>
    private static GradientPlan BuildPlan(Gradient gradient)
    {
        var hasInverse = Matrix3x2.Invert(gradient.Transform, out var inverse) && IsFinite(inverse);

        var plan = new GradientPlan
        {
            Gradient = gradient,
            HasInverseTransform = hasInverse,
            InverseTransform = inverse,
        };

        switch (gradient)
        {
            case LinearGradient linear:
                {
                    double dirX = linear.End.X - linear.Start.X;
                    double dirY = linear.End.Y - linear.Start.Y;
                    plan = plan with
                    {
                        LinearDirX = dirX,
                        LinearDirY = dirY,
                        LinearLengthSquared = (dirX * dirX) + (dirY * dirY),
                    };
                    break;
                }

            case RadialGradient radial:
                {
                    double dx = radial.EndCenter.X - radial.StartCenter.X;
                    double dy = radial.EndCenter.Y - radial.StartCenter.Y;
                    double dr = radial.EndRadius - radial.StartRadius;
                    plan = plan with
                    {
                        // dr == 0.0 <=> StartRadius == EndRadius (computed as a difference so the
                        // comparison is against the literal zero, not variable-to-variable).
                        RadialIsFullyDegenerate = radial.StartCenter == radial.EndCenter && dr == 0.0,
                        RadialDx = dx,
                        RadialDy = dy,
                        RadialDr = dr,
                        RadialA = (dx * dx) + (dy * dy) - (dr * dr),
                    };
                    break;
                }
        }

        return plan;
    }

    /// <summary>
    ///     Evaluates <paramref name="plan"/>'s gradient at <paramref name="point"/>, using only
    ///     the per-fill-invariant quantities already precomputed into <paramref name="plan"/>.
    /// </summary>
    private static Rgba32 EvaluateWithPlan(in GradientPlan plan, Vector2 point)
    {
        if (!plan.HasInverseTransform)
        {
            return LastStopColor(plan.Gradient.Stops);
        }

        var gradientPoint = Vector2.Transform(point, plan.InverseTransform);

        return plan.Gradient switch
        {
            LinearGradient linear => EvaluateLinear(linear, in plan, gradientPoint),
            RadialGradient radial => EvaluateRadial(radial, in plan, gradientPoint),
            _ => throw new NotSupportedException($"Unsupported gradient type '{plan.Gradient.GetType()}'."),
        };
    }

    /// <summary>
    ///     Evaluates a <see cref="LinearGradient"/> at an already gradient-space point, using
    ///     <paramref name="plan"/>'s precomputed direction vector and squared length.
    /// </summary>
    private static Rgba32 EvaluateLinear(LinearGradient linear, in GradientPlan plan, Vector2 gradientPoint)
    {
        if (plan.LinearLengthSquared <= ZeroLengthSquaredThreshold)
        {
            // Zero-Length Linear Vector degenerate case - see this class's remarks.
            return LastStopColor(linear.Stops);
        }

        double px = gradientPoint.X - linear.Start.X;
        double py = gradientPoint.Y - linear.Start.Y;
        var t = ((px * plan.LinearDirX) + (py * plan.LinearDirY)) / plan.LinearLengthSquared;

        var folded = ApplySpread(t, linear.Spread);
        return ResolveColor(linear.Stops, folded);
    }

    /// <summary>
    ///     Evaluates a <see cref="RadialGradient"/> at an already gradient-space point, using
    ///     <paramref name="plan"/>'s precomputed two-circle quadratic coefficients.
    /// </summary>
    private static Rgba32 EvaluateRadial(RadialGradient radial, in GradientPlan plan, Vector2 gradientPoint)
    {
        if (plan.RadialIsFullyDegenerate)
        {
            // Both radii equal and both centers equal: the family of circles never changes with
            // t at all - see this class's remarks.
            return LastStopColor(radial.Stops);
        }

        var t = SolveRadialParameter(radial, in plan, gradientPoint);
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
    ///     Solves the standard two-circle ("conical") gradient quadratic for the correct valid
    ///     <c>t</c> (subject to <c>radius(t) &gt;= 0</c>) at which the interpolated circle
    ///     <c>(center(t), radius(t))</c> passes through <paramref name="gradientPoint"/>, entirely
    ///     in <see cref="double"/> precision, using <paramref name="plan"/>'s precomputed
    ///     per-fill-invariant coefficients.
    /// </summary>
    /// <remarks>
    ///     When both roots of the quadratic are independently valid (both give
    ///     <c>radius(t) &gt;= 0</c>) - which can only happen when the leading coefficient
    ///     <c>a</c> is positive, i.e. the start and end circles are not nested one inside the
    ///     other - the point lies in a narrow sliver where the swept family's circle boundary
    ///     self-intersects near one of the two endpoint circles (see the design document's
    ///     two-circle root-selection policy). Unconditionally preferring the numerically larger
    ///     root there (as a naive reading of "prefer the bigger valid root" suggests) can select
    ///     an interpolated-color root instead of the correct boundary-conforming one - for
    ///     example, a point exactly on the start circle is already a valid root at <c>t = 0</c>,
    ///     and should resolve to the first stop's color, not to a second, barely-larger root a
    ///     hair's breadth away. The correct root is instead selected relative to
    ///     <see cref="GradientPlan.RadialDr"/>'s sign: when the radius is growing from start to
    ///     end (<c>dr &gt; 0</c>), the sliver sits just after the start circle, so the smaller
    ///     root is the boundary-conforming one; when shrinking (<c>dr &lt; 0</c>), the sliver sits
    ///     just before the end circle, so the larger root is boundary-conforming. When the radius
    ///     never changes (<c>dr == 0</c>, the "cylindrical" equal-radii case), there is no such
    ///     endpoint asymmetry, so the larger root is kept for consistency with the general
    ///     (single-valid-root) case.
    /// </remarks>
    /// <returns>The selected valid <c>t</c>, or <see langword="null"/> if no valid root exists.</returns>
    private static double? SolveRadialParameter(RadialGradient radial, in GradientPlan plan, Vector2 gradientPoint)
    {
        var dx = plan.RadialDx;
        var dy = plan.RadialDy;
        var dr = plan.RadialDr;
        var a = plan.RadialA;

        double pdx = gradientPoint.X - radial.StartCenter.X;
        double pdy = gradientPoint.Y - radial.StartCenter.Y;

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

        // a > 0 guarantees t0 >= t1 (t0 uses "+sqrtDiscriminant", t1 uses "-sqrtDiscriminant",
        // and sqrtDiscriminant >= 0).
        var high = t0;
        var low = t1;

        var validHigh = IsValidRoot(radial, high);
        var validLow = IsValidRoot(radial, low);

        if (validHigh && validLow)
        {
            // Both roots are valid: the ambiguous "self-intersecting sliver" case - see this
            // method's remarks for the boundary-conforming selection rule.
            return dr > 0.0 ? low : high;
        }

        if (validHigh)
        {
            return high;
        }

        return validLow ? low : null;
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

        var last = stops[^1];
        if (t >= last.Offset)
        {
            return last.Color;
        }

        var first = stops[0];
        if (t < first.Offset)
        {
            return first.Color;
        }

        // Scan backward (highest index first) so that, at an exact tie between two or more stops
        // sharing the same Offset - including a tie at the gradient's very first offset - the
        // later-supplied stop (the higher array index; Gradient stable-sorts by Offset, so a higher
        // index among equal-offset stops always means "supplied later") is matched first and wins.
        // Scanning forward instead matches the *earlier* tied stop first, which is the bug this
        // fixes: it previously returned via a spurious fraction == 1.0 interpolation against the
        // preceding pair before ever reaching the intended tied-pair hard-stop branch.
        for (var i = stops.Count - 2; i >= 0; i--)
        {
            var a = stops[i];
            if (t < a.Offset)
            {
                continue;
            }

            var b = stops[i + 1];
            if (b.Offset - a.Offset <= float.Epsilon)
            {
                // Hard stop: two (or more) stops sharing the same offset - resolve to the later
                // stop's color outright, avoiding a 0/0 division.
                return b.Color;
            }

            var fraction = (t - a.Offset) / (b.Offset - a.Offset);
            return LerpPremultiplied(a.Color, b.Color, fraction);
        }

        // Unreachable given the bracketing checks above (every t in [first.Offset, last.Offset) is
        // covered by some consecutive pair), but guards against ever falling through without a
        // defined result.
        return first.Color;
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
