namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     Identifies the kind of drawing operation a <see cref="PathCommand"/> represents within a
///     <see cref="Subpath"/>.
/// </summary>
/// <remarks>
///     A "MoveTo" operation is deliberately excluded from this enumeration. Every subpath always
///     begins with exactly one move (which starts a new subpath and can never appear anywhere
///     else within one), so <see cref="PathBuilder"/> captures it directly as
///     <see cref="Subpath.Start"/> instead of as a command. This makes an entire class of invalid
///     states unrepresentable: a <see cref="Subpath.Commands"/> list can never be empty-then-a-
///     move, nor contain a move anywhere but logically "before" its first entry, because there is
///     no <see cref="PathCommandType"/> value that could encode one there in the first place.
/// </remarks>
public enum PathCommandType
{
    /// <summary>
    ///     A straight line segment from the current point to <see cref="PathCommand.EndPoint"/>.
    /// </summary>
    LineTo,

    /// <summary>
    ///     A quadratic Bezier curve from the current point to <see cref="PathCommand.EndPoint"/>,
    ///     using <see cref="PathCommand.Control1"/> as its single control point.
    /// </summary>
    QuadraticBezierTo,

    /// <summary>
    ///     A cubic Bezier curve from the current point to <see cref="PathCommand.EndPoint"/>, using
    ///     <see cref="PathCommand.Control1"/> and <see cref="PathCommand.Control2"/> as its two
    ///     control points.
    /// </summary>
    CubicBezierTo,

    /// <summary>
    ///     An SVG-style elliptical arc from the current point to <see cref="PathCommand.EndPoint"/>,
    ///     parameterized by <see cref="PathCommand.Radius"/>, <see cref="PathCommand.RotationDegrees"/>,
    ///     <see cref="PathCommand.LargeArc"/>, and <see cref="PathCommand.Sweep"/>.
    /// </summary>
    ArcTo,

    /// <summary>
    ///     Closes the current subpath with a straight line back to <see cref="Subpath.Start"/>.
    /// </summary>
    Close
}
