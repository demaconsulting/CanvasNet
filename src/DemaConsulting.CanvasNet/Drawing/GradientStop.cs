using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Represents a single color stop within a <see cref="Gradient"/>'s ramp: a position along the
///     gradient's <c>[0, 1]</c> parameter and the color the ramp reaches at that position.
/// </summary>
/// <remarks>
///     <see cref="GradientStop"/> is deliberately not required to be part of a sorted or
///     duplicate-free sequence - a caller may supply stops in any order, and may supply two or
///     more stops that share the same <see cref="Offset"/> (a "hard stop", producing an
///     instantaneous color step rather than a smooth ramp at that position) - both are legal,
///     well-defined configurations. See <see cref="Gradient"/>'s remarks for how a list of stops
///     is normalized and resolved.
/// </remarks>
public readonly struct GradientStop
{
    /// <summary>
    ///     Initializes a new gradient stop.
    /// </summary>
    /// <param name="offset">
    ///     The position along the gradient's <c>[0, 1]</c> parameter at which the ramp reaches
    ///     <paramref name="color"/>. Must be finite and within <c>[0, 1]</c> inclusive.
    /// </param>
    /// <param name="color">The color the ramp reaches at <paramref name="offset"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="offset"/> is not finite, or is outside <c>[0, 1]</c>.
    /// </exception>
    public GradientStop(float offset, Rgba32 color)
    {
        if (!float.IsFinite(offset) || offset < 0f || offset > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset), offset, "Offset must be a finite value within [0, 1].");
        }

        Offset = offset;
        Color = color;
    }

    /// <summary>
    ///     Gets the position along the gradient's <c>[0, 1]</c> parameter at which the ramp
    ///     reaches <see cref="Color"/>.
    /// </summary>
    public float Offset { get; }

    /// <summary>
    ///     Gets the color the ramp reaches at <see cref="Offset"/>.
    /// </summary>
    public Rgba32 Color { get; }
}
