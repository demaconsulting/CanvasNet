using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Represents a resolved, renderable tile paint: a pre-rendered one-cell tile bitmap (a
///     <see cref="Surface"/>), a <see cref="Transform"/> mapping the tile's own defining
///     (pattern-space) coordinates into the same coordinate space a caller's
///     <see cref="Geometry.Path"/> already uses, and an <see cref="XStep"/>/<see cref="YStep"/>
///     pitch describing how far apart - in pattern-space units - successive tile repetitions are
///     spaced.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="TilePaint"/> is the tiling-pattern analogue of <see cref="Gradient"/>: just as
///     <see cref="Gradient"/> (via <see cref="GradientEvaluator"/>) is evaluated once per pixel to
///     produce a smoothly varying color, <see cref="TilePaint"/> (via
///     <see cref="TilePaintEvaluator"/>) is evaluated once per pixel to sample the repeating tile
///     bitmap at the appropriate wrapped-around pattern-space offset.
///     </para>
///     <para>
///     <see cref="TilePaint"/> does not own <see cref="Surface"/>'s lifetime: it does not dispose
///     it, and the caller that constructed this <see cref="TilePaint"/> remains responsible for
///     disposing the underlying <see cref="Surface"/> once it is no longer needed - mirroring how
///     <see cref="Gradient"/> similarly owns no disposable resource of its own.
///     </para>
///     <para>
///     <see cref="XStep"/>/<see cref="YStep"/> may be negative (a legitimate PDF tiling-pattern
///     value, per PDF 32000-1 §8.7.3.1, meaning the tile repeats in the negative pattern-space
///     axis direction) - only zero or a non-finite value is rejected by the constructor. Sampling
///     code (<see cref="TilePaintEvaluator"/>) is responsible for correctly wrapping around a
///     negative step using its absolute value.
///     </para>
/// </remarks>
public sealed class TilePaint
{
    /// <summary>
    ///     Initializes a new <see cref="TilePaint"/>.
    /// </summary>
    /// <param name="surface">
    ///     The pre-rendered one-cell tile bitmap. Must not be <see langword="null"/>. This
    ///     instance does not take ownership of <paramref name="surface"/> - see this class's
    ///     remarks.
    /// </param>
    /// <param name="transform">
    ///     The transform mapping the tile's own defining (pattern-space) coordinates into the
    ///     caller's path coordinate space. Every one of its six components must be finite.
    /// </param>
    /// <param name="xStep">
    ///     The pattern-space horizontal tile pitch. Must be a finite, non-zero value; a negative
    ///     value is accepted - see this class's remarks.
    /// </param>
    /// <param name="yStep">
    ///     The pattern-space vertical tile pitch. Must be a finite, non-zero value; a negative
    ///     value is accepted - see this class's remarks.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when any component of <paramref name="transform"/> is not finite, or when
    ///     <paramref name="xStep"/> or <paramref name="yStep"/> is not finite or is zero.
    /// </exception>
    public TilePaint(Surface surface, Matrix3x2 transform, float xStep, float yStep)
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (!IsFinite(transform))
        {
            throw new ArgumentOutOfRangeException(
                nameof(transform), transform, "Transform components must all be finite values.");
        }

        if (!float.IsFinite(xStep) || xStep == 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(xStep), xStep, "XStep must be a finite, non-zero value.");
        }

        if (!float.IsFinite(yStep) || yStep == 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(yStep), yStep, "YStep must be a finite, non-zero value.");
        }

        Surface = surface;
        Transform = transform;
        XStep = xStep;
        YStep = yStep;
    }

    /// <summary>
    ///     Gets the pre-rendered one-cell tile bitmap. This <see cref="TilePaint"/> does not own
    ///     this <see cref="Surface"/>'s lifetime - see this class's remarks.
    /// </summary>
    public Surface Surface { get; }

    /// <summary>
    ///     Gets the transform mapping the tile's own defining (pattern-space) coordinates into
    ///     the caller's path coordinate space.
    /// </summary>
    public Matrix3x2 Transform { get; }

    /// <summary>
    ///     Gets the pattern-space horizontal tile pitch. May be negative - see this class's
    ///     remarks.
    /// </summary>
    public float XStep { get; }

    /// <summary>
    ///     Gets the pattern-space vertical tile pitch. May be negative - see this class's
    ///     remarks.
    /// </summary>
    public float YStep { get; }

    /// <summary>
    ///     Returns a new <see cref="TilePaint"/> with the same <see cref="Surface"/>,
    ///     <see cref="XStep"/>, and <see cref="YStep"/>, whose <see cref="Transform"/> is this
    ///     tile paint's own <see cref="Transform"/> composed with <paramref name="transform"/>
    ///     (row-vector convention, matching <see cref="Gradient.WithTransform"/>: this tile
    ///     paint's existing <see cref="Transform"/> is applied first, then
    ///     <paramref name="transform"/> is applied on top of that).
    /// </summary>
    /// <param name="transform">The additional transform to compose after this tile paint's own <see cref="Transform"/>.</param>
    /// <returns>A new <see cref="TilePaint"/> instance with the composed transform; the original is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the composed transform has a non-finite component.
    /// </exception>
    public TilePaint WithTransform(Matrix3x2 transform) =>
        new(Surface, Transform * transform, XStep, YStep);

    /// <summary>
    ///     Determines whether every component of <paramref name="matrix"/> is a finite value.
    /// </summary>
    private static bool IsFinite(Matrix3x2 matrix) =>
        float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) &&
        float.IsFinite(matrix.M21) && float.IsFinite(matrix.M22) &&
        float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32);
}
