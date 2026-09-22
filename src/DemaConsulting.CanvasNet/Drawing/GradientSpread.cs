namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Describes how a <see cref="Gradient"/> paints the region beyond its own defined
///     <c>[0, 1]</c> ramp - the region before its first stop and beyond its last stop.
/// </summary>
/// <remarks>
///     These are the three universally recognized gradient spread methods (SVG's
///     <c>spreadMethod</c>, CSS's plain/<c>repeating-</c> gradients, Direct2D's
///     <c>D2D1_EXTEND_MODE</c>, and Skia's <c>SkTileMode</c>); no fourth mode is defined.
/// </remarks>
public enum GradientSpread
{
    /// <summary>
    ///     Clamps the gradient parameter to <c>[0, 1]</c>: every point beyond an end is painted
    ///     with that end's own stop color, exactly as if the ramp extended flatly forever.
    /// </summary>
    Pad,

    /// <summary>
    ///     Mirrors the gradient parameter back and forth across <c>[0, 1]</c> in a period-2
    ///     triangle wave, so the ramp appears to bounce between its two ends indefinitely.
    /// </summary>
    Reflect,

    /// <summary>
    ///     Wraps the gradient parameter back into <c>[0, 1]</c> in a sawtooth pattern, so the ramp
    ///     appears to repeat from its first stop immediately after its last stop, indefinitely.
    /// </summary>
    Repeat,
}
