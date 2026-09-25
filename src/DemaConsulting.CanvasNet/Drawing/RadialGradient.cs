using System.Numerics;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     A <see cref="Gradient"/> whose ramp varies radially between two circles (the general
///     "two-circle"/"focal" radial gradient model used by SVG/CSS radial gradients, Direct2D's
///     radial-brush-with-origin, and Skia's <c>TwoPointConical</c> shader), expressed in the
///     gradient's own defining coordinates (mapped into path space by <see cref="Gradient.Transform"/>).
/// </summary>
/// <remarks>
///     <para>
///     This general two-circle model subsumes the simpler single-circle case
///     (<see cref="StartRadius"/> zero, <see cref="StartCenter"/> equal to <see cref="EndCenter"/>)
///     and the "focal point" case (<see cref="StartRadius"/> zero,
///     <see cref="StartCenter"/> different from <see cref="EndCenter"/>) without a second public
///     type.
///     </para>
///     <para>
///     Every combination of equal/different centers and equal/zero/different radii is accepted at
///     construction; each is a defined, evaluation-time case - see the <c>gradient-paint.md</c>
///     design document's degenerate-case policy, and <see cref="GradientEvaluator"/>'s remarks, for
///     exactly how each configuration resolves.
///     </para>
/// </remarks>
public sealed class RadialGradient : Gradient
{
    /// <summary>
    ///     Initializes a new radial gradient.
    /// </summary>
    /// <param name="startCenter">
    ///     The center, in gradient-defining coordinates, of the circle at which the ramp reaches
    ///     its first stop's offset (<c>0</c>). Must have finite components.
    /// </param>
    /// <param name="startRadius">
    ///     The radius, in gradient-defining coordinates, of the circle at which the ramp reaches
    ///     its first stop's offset (<c>0</c>). Must be finite and greater than or equal to zero.
    /// </param>
    /// <param name="endCenter">
    ///     The center, in gradient-defining coordinates, of the circle at which the ramp reaches
    ///     its last stop's offset (<c>1</c>). Must have finite components.
    /// </param>
    /// <param name="endRadius">
    ///     The radius, in gradient-defining coordinates, of the circle at which the ramp reaches
    ///     its last stop's offset (<c>1</c>). Must be finite and greater than or equal to zero.
    /// </param>
    /// <param name="stops">The gradient's color stops. See <see cref="Gradient"/>'s remarks.</param>
    /// <param name="spread">The spread method. Defaults to <see cref="GradientSpread.Pad"/>.</param>
    /// <param name="transform">
    ///     The transform mapping gradient-defining coordinates into path space. See
    ///     <see cref="LinearGradient"/>'s constructor remarks: a <see langword="null"/> value (the
    ///     default, meaning "omitted") is resolved to <see cref="Matrix3x2.Identity"/>, while any
    ///     caller-supplied <see cref="Matrix3x2"/> value - including the all-zero
    ///     <see langword="default"/>(<see cref="Matrix3x2"/>) - is preserved exactly as given.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stops"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stops"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="startCenter"/>/<paramref name="endCenter"/> has a
    ///     non-finite component; when <paramref name="startRadius"/>/<paramref name="endRadius"/>
    ///     is not finite or is negative; when <paramref name="spread"/> is not a defined
    ///     <see cref="GradientSpread"/> value; or when any component of <paramref name="transform"/>
    ///     is not finite.
    /// </exception>
    public RadialGradient(
        Vector2 startCenter,
        float startRadius,
        Vector2 endCenter,
        float endRadius,
        IReadOnlyList<GradientStop> stops,
        GradientSpread spread = GradientSpread.Pad,
        Matrix3x2? transform = null)
        : base(stops, spread, transform ?? Matrix3x2.Identity)
    {
        if (!float.IsFinite(startCenter.X) || !float.IsFinite(startCenter.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(startCenter), startCenter, "Start center must have finite components.");
        }

        if (!float.IsFinite(endCenter.X) || !float.IsFinite(endCenter.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(endCenter), endCenter, "End center must have finite components.");
        }

        if (!float.IsFinite(startRadius) || startRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startRadius), startRadius, "Start radius must be a finite value greater than or equal to zero.");
        }

        if (!float.IsFinite(endRadius) || endRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endRadius), endRadius, "End radius must be a finite value greater than or equal to zero.");
        }

        StartCenter = startCenter;
        StartRadius = startRadius;
        EndCenter = endCenter;
        EndRadius = endRadius;
    }

    /// <summary>
    ///     Gets the center, in gradient-defining coordinates, of the circle at which the ramp
    ///     reaches its first stop's offset (<c>0</c>).
    /// </summary>
    public Vector2 StartCenter { get; }

    /// <summary>
    ///     Gets the radius, in gradient-defining coordinates, of the circle at which the ramp
    ///     reaches its first stop's offset (<c>0</c>).
    /// </summary>
    public float StartRadius { get; }

    /// <summary>
    ///     Gets the center, in gradient-defining coordinates, of the circle at which the ramp
    ///     reaches its last stop's offset (<c>1</c>).
    /// </summary>
    public Vector2 EndCenter { get; }

    /// <summary>
    ///     Gets the radius, in gradient-defining coordinates, of the circle at which the ramp
    ///     reaches its last stop's offset (<c>1</c>).
    /// </summary>
    public float EndRadius { get; }

    /// <inheritdoc/>
    public override Gradient WithTransform(Matrix3x2 transform) =>
        new RadialGradient(StartCenter, StartRadius, EndCenter, EndRadius, Stops, Spread, Transform * transform);
}
