namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Identifies the shape drawn where two consecutive stroked segments meet.
/// </summary>
/// <remarks>
///     The join style applies wherever a stroked polyline turns at a vertex. For
///     <see cref="Miter"/>, excessively sharp angles fall back to a bevel when the computed
///     miter exceeds the configured <see cref="StrokeStyle.MiterLimit"/>.
/// </remarks>
public enum LineJoin
{
    /// <summary>
    ///     Extends the two offset segment edges until they meet at a point, unless the miter
    ///     exceeds the configured limit, in which case a bevel is used instead.
    /// </summary>
    Miter,

    /// <summary>
    ///     Connects the two offset segment edges with a circular arc of radius half the stroke
    ///     width, centered on the vertex.
    /// </summary>
    Round,

    /// <summary>
    ///     Connects the two offset segment edges directly with a straight line between their
    ///     respective offset endpoints.
    /// </summary>
    Bevel
}
