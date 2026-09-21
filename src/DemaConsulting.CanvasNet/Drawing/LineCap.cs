namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Identifies the shape drawn at each exposed end of an open stroked subpath.
/// </summary>
/// <remarks>
///     Caps affect only open subpaths (including dashed "on" segments produced from an otherwise
///     closed path). Closed subpaths have no exposed ends, so they ignore this setting.
/// </remarks>
public enum LineCap
{
    /// <summary>
    ///     Ends the stroke exactly at the subpath's start and end points, with no extension
    ///     beyond those points.
    /// </summary>
    Butt,

    /// <summary>
    ///     Ends the stroke with a semicircle whose radius equals half the stroke width, centered
    ///     on the subpath's start or end point.
    /// </summary>
    Round,

    /// <summary>
    ///     Ends the stroke with a square whose center lies on the subpath's start or end point,
    ///     extending half the stroke width beyond the end in the path direction.
    /// </summary>
    Square
}
