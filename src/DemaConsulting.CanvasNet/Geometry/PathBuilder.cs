using System.Numerics;

namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     A mutable, fluent, reusable builder for constructing an immutable <see cref="Path"/> from a
///     sequence of move/draw/close operations.
/// </summary>
/// <remarks>
///     Every drawing method returns <see langword="this"/> to support fluent chaining
///     (<c>builder.MoveTo(a).LineTo(b).Close()</c>). <see cref="PathBuilder"/> is the sole
///     component responsible for the invariants <see cref="Path"/>/<see cref="Subpath"/> rely on:
///     every subpath starts with a real <see cref="MoveTo"/>, no drawing command is ever recorded
///     before the first <see cref="MoveTo"/>, and no drawing command is ever recorded after
///     <see cref="Close"/> without an intervening <see cref="MoveTo"/>. Not thread-safe: a single
///     instance must not be used concurrently from multiple threads.
/// </remarks>
public sealed class PathBuilder
{
    /// <summary>
    ///     Completed subpaths accumulated so far by previous <see cref="MoveTo"/>/close cycles.
    /// </summary>
    private readonly List<Subpath> _subpaths = [];

    /// <summary>
    ///     Commands accumulated for the subpath currently being built, since the most recent
    ///     <see cref="MoveTo"/>.
    /// </summary>
    private List<PathCommand> _currentCommands = [];

    /// <summary>
    ///     The point the current subpath started at, from the most recent <see cref="MoveTo"/>.
    ///     Only meaningful when <see cref="_hasCurrentSubpath"/> is <see langword="true"/>.
    /// </summary>
    private Vector2 _currentStart;

    /// <summary>
    ///     <see langword="true"/> if a subpath has been started (via <see cref="MoveTo"/>) and not
    ///     yet closed; <see langword="false"/> before the first <see cref="MoveTo"/>, and again
    ///     immediately after <see cref="Close"/> until the next <see cref="MoveTo"/>.
    /// </summary>
    private bool _hasCurrentSubpath;

    /// <summary>
    ///     Starts a new subpath at <paramref name="point"/>.
    /// </summary>
    /// <param name="point">The point the new subpath starts at.</param>
    /// <returns>This builder, for fluent chaining.</returns>
    /// <remarks>
    ///     If a previous subpath is still open (not <see cref="Close"/>d), it is committed as-is
    ///     (open) before the new subpath begins; <see cref="MoveTo"/> never implicitly closes the
    ///     previous subpath, matching SVG path-data semantics.
    /// </remarks>
    public PathBuilder MoveTo(Vector2 point)
    {
        CommitCurrentSubpath(isClosed: false);

        _currentStart = point;
        _currentCommands = [];
        _hasCurrentSubpath = true;
        return this;
    }

    /// <summary>
    ///     Appends a straight line segment from the current point to <paramref name="point"/>.
    /// </summary>
    /// <param name="point">The point the line segment draws to.</param>
    /// <returns>This builder, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called before the first <see cref="MoveTo"/>, or after <see cref="Close"/>
    ///     without an intervening <see cref="MoveTo"/>.
    /// </exception>
    public PathBuilder LineTo(Vector2 point)
    {
        EnsureCanDraw();
        _currentCommands.Add(PathCommand.LineTo(point));
        return this;
    }

    /// <summary>
    ///     Appends a quadratic Bezier curve from the current point to <paramref name="end"/>.
    /// </summary>
    /// <param name="control">The curve's single control point.</param>
    /// <param name="end">The point the curve draws to.</param>
    /// <returns>This builder, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called before the first <see cref="MoveTo"/>, or after <see cref="Close"/>
    ///     without an intervening <see cref="MoveTo"/>.
    /// </exception>
    public PathBuilder QuadraticBezierTo(Vector2 control, Vector2 end)
    {
        EnsureCanDraw();
        _currentCommands.Add(PathCommand.QuadraticBezierTo(control, end));
        return this;
    }

    /// <summary>
    ///     Appends a cubic Bezier curve from the current point to <paramref name="end"/>.
    /// </summary>
    /// <param name="control1">The curve's first control point.</param>
    /// <param name="control2">The curve's second control point.</param>
    /// <param name="end">The point the curve draws to.</param>
    /// <returns>This builder, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called before the first <see cref="MoveTo"/>, or after <see cref="Close"/>
    ///     without an intervening <see cref="MoveTo"/>.
    /// </exception>
    public PathBuilder CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 end)
    {
        EnsureCanDraw();
        _currentCommands.Add(PathCommand.CubicBezierTo(control1, control2, end));
        return this;
    }

    /// <summary>
    ///     Appends an SVG-style elliptical arc from the current point to <paramref name="end"/>.
    /// </summary>
    /// <param name="radius">The arc's x- and y-radii (rx, ry).</param>
    /// <param name="rotationDegrees">The arc's x-axis rotation, in degrees.</param>
    /// <param name="largeArc">The SVG arc "large-arc-flag".</param>
    /// <param name="sweep">The SVG arc "sweep-flag".</param>
    /// <param name="end">The point the arc draws to.</param>
    /// <returns>This builder, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called before the first <see cref="MoveTo"/>, or after <see cref="Close"/>
    ///     without an intervening <see cref="MoveTo"/>.
    /// </exception>
    /// <remarks>
    ///     The raw SVG parameters are stored exactly as supplied, with no validation or
    ///     conversion: this builder never pre-inspects them (for example, to special-case a zero
    ///     radius), leaving all SVG-spec interpretation, including degenerate cases, to
    ///     <see cref="SvgArcConverter"/> for the consumer that eventually needs Bezier segments.
    /// </remarks>
    public PathBuilder ArcTo(Vector2 radius, float rotationDegrees, bool largeArc, bool sweep, Vector2 end)
    {
        EnsureCanDraw();
        _currentCommands.Add(PathCommand.ArcTo(radius, rotationDegrees, largeArc, sweep, end));
        return this;
    }

    /// <summary>
    ///     Appends a circular corner arc of the given <paramref name="radius"/> tangent to the
    ///     ray from the current point through <paramref name="corner"/> and to the ray from
    ///     <paramref name="corner"/> through <paramref name="end"/>, matching HTML5 canvas
    ///     <c>arcTo</c> semantics.
    /// </summary>
    /// <param name="corner">The corner point (shared vertex of the two rays).</param>
    /// <param name="end">A point on the outgoing ray past the corner.</param>
    /// <param name="radius">The corner arc radius. Must be finite and non-negative.</param>
    /// <returns>This builder, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called before the first <see cref="MoveTo"/>, or after <see cref="Close"/>
    ///     without an intervening <see cref="MoveTo"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="radius"/> is not finite or is negative.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///     The arc is approximated with a single cubic Bezier using the quarter-turn tangent
    ///     constant kappa = 0.5522847498 (= 4/3 * (sqrt(2) - 1)) — exact for a 90-degree sweep,
    ///     documented approximation for other angles (from Stanislaw K. Dzik / IBM 1975 derivation).
    ///     Prior to the arc a <see cref="LineTo"/> is emitted to the tangent point on the
    ///     incoming ray, matching HTML5 canvas <c>arcTo</c> semantics.
    ///     </para>
    ///     <para>
    ///     Degenerates gracefully: when the two rays are collinear, the incoming/outgoing
    ///     vectors are near-zero-length, or <paramref name="radius"/> is zero, the method emits a
    ///     single <see cref="LineTo"/> to <paramref name="corner"/> instead of an arc.
    ///     </para>
    /// </remarks>
    public PathBuilder TangentArcTo(Vector2 corner, Vector2 end, float radius)
    {
        EnsureCanDraw();
        if (!float.IsFinite(radius) || radius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be finite and non-negative.");
        }

        // Current pen point is the endpoint of the most recent command (or the subpath start).
        Vector2 start;
        if (_currentCommands.Count == 0)
        {
            start = _currentStart;
        }
        else
        {
            var lastCommand = _currentCommands[^1];
            start = lastCommand.Type == PathCommandType.Close ? _currentStart : lastCommand.EndPoint;
        }

        var v1 = start - corner;
        var v2 = end - corner;
        var len1 = v1.Length();
        var len2 = v2.Length();

        // radius == 0f is an intentional exact-sentinel check: the caller explicitly requesting
        // zero radius (mirroring HTML5 canvas arcTo semantics) means "no arc", regardless of how
        // that value was produced. len1/len2, however, are Length() results of computed vectors,
        // so a tolerance (rather than exact zero) catches a corner that is only near-coincident
        // with start/end - avoiding an ill-conditioned near-unit-vector normalization below. The
        // tolerance is scaled to the magnitude of the three points rather than a fixed absolute
        // cutoff, so a legitimately tiny (but non-coincident) corner in a small coordinate system
        // is not mistaken for coincidence, while a corner that truly is coincident relative to
        // its own coordinate scale still degrades to a straight line.
        var coincidenceTolerance = MathF.Max(corner.Length(), MathF.Max(start.Length(), end.Length())) * 1e-6f;
        if (radius == 0f || len1 <= coincidenceTolerance || len2 <= coincidenceTolerance)
        {
            _currentCommands.Add(PathCommand.LineTo(corner));
            return this;
        }

        var n1 = v1 / len1;
        var n2 = v2 / len2;

        var dot = Vector2.Dot(n1, n2);
        // Numeric clamp: |dot| may drift very slightly above 1 for near-collinear input; treat
        // as a degenerate straight segment.
        if (dot >= 1f - 1e-6f || dot <= -1f + 1e-6f)
        {
            _currentCommands.Add(PathCommand.LineTo(corner));
            return this;
        }

        // Half-angle formula: tangent distance from corner to each tangent point along its ray.
        var half = MathF.Acos(dot) * 0.5f;
        var tan = MathF.Tan(half);
        if (tan <= 0f || !float.IsFinite(tan))
        {
            _currentCommands.Add(PathCommand.LineTo(corner));
            return this;
        }

        var distance = radius / tan;

        // Clamp so we never overshoot the shorter of the two adjacent segments.
        var maxDistance = MathF.Min(len1, len2);
        if (distance > maxDistance)
        {
            distance = maxDistance;
            // Corresponding radius when the tangent distance is clamped.
            radius = distance * tan;
        }

        var tangentIn = corner + n1 * distance;
        var tangentOut = corner + n2 * distance;

        // Cubic Bezier approximation of the tangent circular arc with kappa=0.5522847498
        // (= 4/3 * (sqrt(2)-1)); exact for a 90-degree sweep, close for other sweeps typically
        // seen at rounded corners. Control points sit along the tangent rays at offset kappa * r
        // from each tangent point, matching the quarter-circle Bezier derivation.
        const float kappa = 0.5522847498f;
        var control1 = tangentIn - n1 * (kappa * radius);
        var control2 = tangentOut - n2 * (kappa * radius);

        _currentCommands.Add(PathCommand.LineTo(tangentIn));
        _currentCommands.Add(PathCommand.CubicBezierTo(control1, control2, tangentOut));
        return this;
    }


    /// <summary>
    ///     Closes the current subpath with a straight line back to its start point.
    /// </summary>
    /// <returns>This builder, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called before the first <see cref="MoveTo"/>, or after a previous
    ///     <see cref="Close"/> without an intervening <see cref="MoveTo"/>.
    /// </exception>
    public PathBuilder Close()
    {
        EnsureCanDraw();
        _currentCommands.Add(PathCommand.Close());
        CommitCurrentSubpath(isClosed: true);
        return this;
    }

    /// <summary>
    ///     Produces an immutable <see cref="Path"/> snapshot of every subpath built so far
    ///     (including the current, possibly still-open, subpath).
    /// </summary>
    /// <returns>
    ///     A new <see cref="Path"/> instance independent of this builder's internal state; the
    ///     builder remains fully usable afterward, and further mutation of the builder never
    ///     affects a previously built <see cref="Path"/>.
    /// </returns>
    public Path Build()
    {
        var subpaths = new List<Subpath>(_subpaths);
        if (_hasCurrentSubpath)
        {
            subpaths.Add(new Subpath(_currentStart, [.. _currentCommands], isClosed: false));
        }

        return subpaths.Count == 0 ? Path.Empty : new Path(subpaths);
    }

    /// <summary>
    ///     Resets this builder to its initial, empty state, ready to build an unrelated path.
    /// </summary>
    /// <remarks>
    ///     Provided so repeated builds can reuse the same <see cref="PathBuilder"/> instance
    ///     without allocating a new one each time; the internal backing lists are cleared rather
    ///     than discarded where practical, avoiding reallocation for the common case of building
    ///     many similarly sized paths in a loop.
    /// </remarks>
    public void Clear()
    {
        _subpaths.Clear();
        _currentCommands.Clear();
        _hasCurrentSubpath = false;
        _currentStart = default;
    }

    /// <summary>
    ///     Validates that a drawing command may currently be recorded, throwing if no subpath is
    ///     open.
    /// </summary>
    private void EnsureCanDraw()
    {
        if (!_hasCurrentSubpath)
        {
            throw new InvalidOperationException(
                "A drawing command was issued before the first MoveTo, or after Close without an intervening MoveTo.");
        }
    }

    /// <summary>
    ///     Moves the in-progress subpath (if any) into <see cref="_subpaths"/> and clears the
    ///     "current subpath" state.
    /// </summary>
    private void CommitCurrentSubpath(bool isClosed)
    {
        if (!_hasCurrentSubpath)
        {
            return;
        }

        _subpaths.Add(new Subpath(_currentStart, [.. _currentCommands], isClosed));
        _hasCurrentSubpath = false;
    }
}
