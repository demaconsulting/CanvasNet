namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Identifies the rule used to resolve which regions of a self-intersecting or multi-subpath
///     <see cref="Geometry.Path"/> are considered "inside" for fill purposes.
/// </summary>
/// <remarks>
///     Mirrors the SVG/CSS <c>fill-rule</c> property exactly, both in the two values offered and
///     in which one is conventionally treated as the default (<see cref="NonZero"/>): SVG's own
///     <c>fill-rule</c> defaults to <c>nonzero</c>. Declared as its own top-level public enum
///     (rather than documented inline within <see cref="PathFiller"/>, the way
///     <see cref="Geometry.PathCommandType"/> is documented inline within
///     <see cref="Geometry.Path"/>) because, unlike an internal implementation tag, this is a
///     value a caller of <see cref="PathFiller.Fill"/> chooses directly and explicitly - it is
///     part of the public contract of the fill operation, not an implementation detail hidden
///     behind it.
/// </remarks>
public enum FillRule
{
    /// <summary>
    ///     A point is "inside" the path if the signed count of edges crossing a ray from that
    ///     point to infinity (incrementing for one winding direction, decrementing for the other)
    ///     is nonzero. This is the SVG/CSS default (<c>fill-rule: nonzero</c>): two overlapping
    ///     subpaths wound in the <em>same</em> direction still fill their union solidly, while a
    ///     subpath wound in the <em>opposite</em> direction from an enclosing one (for example,
    ///     the inner contour of a letter "O") cuts a hole out of it.
    /// </summary>
    NonZero,

    /// <summary>
    ///     A point is "inside" the path if a ray from that point to infinity crosses the path's
    ///     edges an odd number of times, regardless of winding direction (<c>fill-rule:
    ///     evenodd</c>). Two overlapping subpaths - regardless of winding direction - "cancel
    ///     out" in their region of overlap, alternating inside/outside with every additional
    ///     nested contour crossed.
    /// </summary>
    EvenOdd
}
