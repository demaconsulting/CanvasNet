using System.Numerics;

namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     Provides adaptive flattening of quadratic and cubic Bezier curves into polylines within a
///     caller-specified tolerance.
/// </summary>
/// <remarks>
///     Both methods write into a caller-supplied <see cref="IList{T}"/> rather than allocating and
///     returning a fresh list, because a future rasterizer/stroker will call these in hot loops
///     across many path segments, and repeated per-call allocation would be wasteful; the caller
///     owns buffer reuse. Neither method writes the curve's start point (<c>p0</c>) to the output
///     list - each writes only the interior-and-final points of the curve, always writing the
///     final point last - so that a caller building a continuous polyline for an entire subpath
///     can chain repeated calls (each curve's start point is implicitly the previous call's last
///     written point) without duplicating vertices at segment boundaries.
/// </remarks>
public static class BezierFlattening
{
    /// <summary>
    ///     The maximum recursive subdivision depth for both flattening methods.
    /// </summary>
    /// <remarks>
    ///     Bounds worst-case recursion for pathological inputs (coincident control points,
    ///     near-cusp curves) that might otherwise converge very slowly under the flatness test.
    ///     At depth 20, a single input curve can produce at most 2^20 (a little over one million)
    ///     output segments - already far beyond any practical flattening need - so this guard
    ///     never affects a well-behaved curve's output, only pathological ones.
    /// </remarks>
    private const int MaxRecursionDepth = 20;

    /// <summary>
    ///     Flattens a cubic Bezier curve into a polyline within <paramref name="tolerance"/>,
    ///     appending the resulting points to <paramref name="output"/>.
    /// </summary>
    /// <param name="p0">The curve's start point. Never written to <paramref name="output"/>.</param>
    /// <param name="p1">The curve's first control point.</param>
    /// <param name="p2">The curve's second control point.</param>
    /// <param name="p3">The curve's end point. Always the last point written to <paramref name="output"/>.</param>
    /// <param name="tolerance">
    ///     The maximum allowed perpendicular deviation, in the same units as the input points,
    ///     between the flattened polyline and the true curve. Must be greater than zero.
    /// </param>
    /// <param name="output">The list to append the flattened points to.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="tolerance"/> is less than or equal to zero.
    /// </exception>
    /// <remarks>
    ///     Every combination of control points is mathematically valid, including coincident or
    ///     collinear ones, so no input other than a non-positive <paramref name="tolerance"/>
    ///     causes an exception; degenerate curves are simply accepted as "flat enough" immediately
    ///     by the same flatness test used for well-formed curves.
    /// </remarks>
    public static void FlattenCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float tolerance, IList<Vector2> output)
    {
        if (tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be greater than zero.");
        }

        FlattenCubicRecursive(p0, p1, p2, p3, tolerance, output, 0);
    }

    /// <summary>
    ///     Flattens a quadratic Bezier curve into a polyline within <paramref name="tolerance"/>,
    ///     appending the resulting points to <paramref name="output"/>.
    /// </summary>
    /// <param name="p0">The curve's start point. Never written to <paramref name="output"/>.</param>
    /// <param name="p1">The curve's single control point.</param>
    /// <param name="p2">The curve's end point. Always the last point written to <paramref name="output"/>.</param>
    /// <param name="tolerance">
    ///     The maximum allowed perpendicular deviation, in the same units as the input points,
    ///     between the flattened polyline and the true curve. Must be greater than zero.
    /// </param>
    /// <param name="output">The list to append the flattened points to.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="tolerance"/> is less than or equal to zero.
    /// </exception>
    /// <remarks>
    ///     Every combination of control points is mathematically valid, including coincident or
    ///     collinear ones, so no input other than a non-positive <paramref name="tolerance"/>
    ///     causes an exception; degenerate curves are simply accepted as "flat enough" immediately
    ///     by the same flatness test used for well-formed curves.
    /// </remarks>
    public static void FlattenQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float tolerance, IList<Vector2> output)
    {
        if (tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be greater than zero.");
        }

        FlattenQuadraticRecursive(p0, p1, p2, tolerance, output, 0);
    }

    /// <summary>
    ///     Recursively subdivides a cubic Bezier curve, appending flattened points to <paramref name="output"/>.
    /// </summary>
    private static void FlattenCubicRecursive(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float tolerance, IList<Vector2> output, int depth)
    {
        // Flatness test: distance of each interior control point from the finite chord segment
        // p0-p3 (not the infinite line through it) must be within tolerance
        if (depth >= MaxRecursionDepth || IsFlatEnough(p0, p1, p3, tolerance) && IsFlatEnough(p0, p2, p3, tolerance))
        {
            output.Add(p3);
            return;
        }

        // De Casteljau midpoint split at t = 0.5: repeatedly average adjacent points to obtain
        // the two control polygons for the left and right halves of the curve
        var p01 = (p0 + p1) / 2;
        var p12 = (p1 + p2) / 2;
        var p23 = (p2 + p3) / 2;
        var p012 = (p01 + p12) / 2;
        var p123 = (p12 + p23) / 2;
        var mid = (p012 + p123) / 2;

        FlattenCubicRecursive(p0, p01, p012, mid, tolerance, output, depth + 1);
        FlattenCubicRecursive(mid, p123, p23, p3, tolerance, output, depth + 1);
    }

    /// <summary>
    ///     Recursively subdivides a quadratic Bezier curve, appending flattened points to <paramref name="output"/>.
    /// </summary>
    private static void FlattenQuadraticRecursive(Vector2 p0, Vector2 p1, Vector2 p2, float tolerance, IList<Vector2> output, int depth)
    {
        if (depth >= MaxRecursionDepth || IsFlatEnough(p0, p1, p2, tolerance))
        {
            output.Add(p2);
            return;
        }

        // De Casteljau midpoint split at t = 0.5
        var p01 = (p0 + p1) / 2;
        var p12 = (p1 + p2) / 2;
        var mid = (p01 + p12) / 2;

        FlattenQuadraticRecursive(p0, p01, mid, tolerance, output, depth + 1);
        FlattenQuadraticRecursive(mid, p12, p2, tolerance, output, depth + 1);
    }

    /// <summary>
    ///     Tests whether control point <paramref name="control"/> lies within
    ///     <paramref name="tolerance"/> of the finite chord segment from <paramref name="chordStart"/>
    ///     to <paramref name="chordEnd"/>.
    /// </summary>
    private static bool IsFlatEnough(Vector2 chordStart, Vector2 control, Vector2 chordEnd, float tolerance)
    {
        var chord = chordEnd - chordStart;
        var toControl = control - chordStart;

        var chordLengthSquared = chord.LengthSquared();
        if (chordLengthSquared <= float.Epsilon)
        {
            // A zero-length chord (p0 == pEnd) has no well-defined projection - fall back to the
            // straight-line distance from the chord point to the control point
            return toControl.Length() <= tolerance;
        }

        // Distance to the finite chord *segment*, not the infinite line through it: project
        // control onto the chord and clamp the parameter to [0, 1] before measuring distance to
        // that clamped point. Measuring against the infinite line alone would accept a control
        // point whose projection falls beyond chordEnd as "flat" even though the curve it belongs
        // to travels well outside the [chordStart, chordEnd] segment.
        var t = Math.Clamp(Vector2.Dot(toControl, chord) / chordLengthSquared, 0f, 1f);
        var closest = chordStart + t * chord;
        return Vector2.Distance(control, closest) <= tolerance;
    }
}
