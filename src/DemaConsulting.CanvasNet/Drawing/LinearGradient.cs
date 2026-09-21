using System.Numerics;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     A <see cref="Gradient"/> whose ramp varies linearly along a straight vector between two
///     points, expressed in the gradient's own defining coordinates (mapped into path space by
///     <see cref="Gradient.Transform"/>).
/// </summary>
public sealed class LinearGradient : Gradient
{
    /// <summary>
    ///     Initializes a new linear gradient.
    /// </summary>
    /// <param name="start">
    ///     The point, in gradient-defining coordinates, at which the ramp reaches its first
    ///     stop's offset (<c>0</c>). Must have finite <see cref="Vector2.X"/>/<see cref="Vector2.Y"/>
    ///     components.
    /// </param>
    /// <param name="end">
    ///     The point, in gradient-defining coordinates, at which the ramp reaches its last stop's
    ///     offset (<c>1</c>). Must have finite components. <paramref name="end"/> equal to
    ///     <paramref name="start"/> (a zero-length gradient vector) is accepted - see the
    ///     <c>gradient-paint.md</c> design document's degenerate-case policy for how it is
    ///     resolved when painting a fill.
    /// </param>
    /// <param name="stops">The gradient's color stops. See <see cref="Gradient"/>'s remarks.</param>
    /// <param name="spread">The spread method. Defaults to <see cref="GradientSpread.Pad"/>.</param>
    /// <param name="transform">
    ///     The transform mapping gradient-defining coordinates into path space. Because
    ///     <see cref="Matrix3x2.Identity"/> is not a compile-time constant, the default value here
    ///     is <see langword="default"/>(<see cref="Matrix3x2"/>) (the all-zero matrix, never a
    ///     legitimate caller transform); the constructor substitutes <see cref="Matrix3x2.Identity"/>
    ///     whenever the caller passes (or omits) that value, before running validation.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stops"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stops"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="start"/> or <paramref name="end"/> has a non-finite
    ///     component, when <paramref name="spread"/> is not a defined <see cref="GradientSpread"/>
    ///     value, or when any component of <paramref name="transform"/> is not finite.
    /// </exception>
    public LinearGradient(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<GradientStop> stops,
        GradientSpread spread = GradientSpread.Pad,
        Matrix3x2 transform = default)
        : base(stops, spread, transform == default ? Matrix3x2.Identity : transform)
    {
        if (!float.IsFinite(start.X) || !float.IsFinite(start.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Start must have finite components.");
        }

        if (!float.IsFinite(end.X) || !float.IsFinite(end.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "End must have finite components.");
        }

        Start = start;
        End = end;
    }

    /// <summary>
    ///     Gets the point, in gradient-defining coordinates, at which the ramp reaches its first
    ///     stop's offset (<c>0</c>).
    /// </summary>
    public Vector2 Start { get; }

    /// <summary>
    ///     Gets the point, in gradient-defining coordinates, at which the ramp reaches its last
    ///     stop's offset (<c>1</c>).
    /// </summary>
    public Vector2 End { get; }
}
