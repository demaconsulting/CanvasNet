namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     How a difference image maps the difference of a pixel to a color.
/// </summary>
/// <seealso cref="Surface.CreateDiff"/>
public enum DiffStyle
{
    /// <summary>The largest channel difference, amplified, drawn through a hot ramp: black, red, yellow, white.</summary>
    Heat,

    /// <summary>Each channel's own absolute difference, amplified.</summary>
    Abs
}
