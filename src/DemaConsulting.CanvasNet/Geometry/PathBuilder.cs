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
        _currentCommands = [];
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
