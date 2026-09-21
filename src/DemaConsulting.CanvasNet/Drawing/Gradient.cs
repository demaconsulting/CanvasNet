using System.Collections.ObjectModel;
using System.Numerics;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Represents a color ramp defined by an ordered set of <see cref="GradientStop"/>s, a
///     <see cref="GradientSpread"/> method describing how the ramp behaves beyond its own
///     <c>[0, 1]</c> extent, and a <see cref="Transform"/> mapping the gradient's own defining
///     coordinates (see <see cref="LinearGradient"/>/<see cref="RadialGradient"/>) into the same
///     coordinate space a caller's <see cref="Geometry.Path"/> already uses.
/// </summary>
/// <remarks>
///     <para>
///     <see cref="Gradient"/> is deliberately not sealed, but has no public constructor of its
///     own - only <see cref="LinearGradient"/> and <see cref="RadialGradient"/> may derive from
///     it. This keeps the type closed to caller-authored derivation while remaining open for
///     these two library-authored subtypes.
///     </para>
///     <para>
///     <b>Stop normalization.</b> The constructor takes a defensive copy of the supplied
///     stops (via the protected constructor), stable-sorted ascending by <see cref="GradientStop.Offset"/>.
///     A stable sort (rather than <see cref="Array.Sort{T}(T[])"/>/<see cref="List{T}.Sort()"/>,
///     neither of which is guaranteed stable) preserves the caller-supplied relative order among
///     stops that share the same <see cref="GradientStop.Offset"/> - this is what makes a "hard
///     stop" well-defined: of two or more stops at the same offset, the earlier-supplied one is
///     used for gradient parameter values approaching from below, and the later-supplied one for
///     values at or beyond it.
///     </para>
///     <para>
///     <see cref="Transform"/> does not have to be invertible - see the <c>gradient-paint.md</c>
///     design document's degenerate-case policy for how a non-invertible transform is resolved
///     when actually painting a fill.
///     </para>
/// </remarks>
public abstract class Gradient
{
    /// <summary>
    ///     Initializes the shared state common to every <see cref="Gradient"/> subtype.
    /// </summary>
    /// <param name="stops">
    ///     The gradient's color stops. Must not be <see langword="null"/> and must contain at
    ///     least one entry.
    /// </param>
    /// <param name="spread">
    ///     The spread method describing how the ramp behaves beyond its own <c>[0, 1]</c> extent.
    ///     Must be a defined <see cref="GradientSpread"/> value.
    /// </param>
    /// <param name="transform">
    ///     The transform mapping the gradient's own defining coordinates into the caller's path
    ///     coordinate space. Every one of its six components must be finite.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stops"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stops"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="spread"/> is not a defined <see cref="GradientSpread"/>
    ///     value, or when any component of <paramref name="transform"/> is not finite.
    /// </exception>
    protected Gradient(IReadOnlyList<GradientStop> stops, GradientSpread spread, Matrix3x2 transform)
    {
        ArgumentNullException.ThrowIfNull(stops);

        if (stops.Count == 0)
        {
            throw new ArgumentException("Stops must contain at least one entry.", nameof(stops));
        }

        if (!Enum.IsDefined(spread))
        {
            throw new ArgumentOutOfRangeException(
                nameof(spread), spread, "Spread must be a defined GradientSpread value.");
        }

        if (!IsFinite(transform))
        {
            throw new ArgumentOutOfRangeException(
                nameof(transform), transform, "Transform components must all be finite values.");
        }

        Stops = CopyStableSorted(stops);
        Spread = spread;
        Transform = transform;
    }

    /// <summary>
    ///     Gets the gradient's color stops, defensively copied and stable-sorted ascending by
    ///     <see cref="GradientStop.Offset"/> - see this class's remarks.
    /// </summary>
    public IReadOnlyList<GradientStop> Stops { get; }

    /// <summary>
    ///     Gets the spread method describing how the ramp behaves beyond its own <c>[0, 1]</c>
    ///     extent.
    /// </summary>
    public GradientSpread Spread { get; }

    /// <summary>
    ///     Gets the transform mapping the gradient's own defining coordinates into the caller's
    ///     path coordinate space.
    /// </summary>
    public Matrix3x2 Transform { get; }

    /// <summary>
    ///     Determines whether every component of <paramref name="matrix"/> is a finite value.
    /// </summary>
    private static bool IsFinite(Matrix3x2 matrix) =>
        float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) &&
        float.IsFinite(matrix.M21) && float.IsFinite(matrix.M22) &&
        float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32);

    /// <summary>
    ///     Produces an immutable, stable-sorted-by-offset defensive copy of <paramref name="stops"/>.
    /// </summary>
    private static IReadOnlyList<GradientStop> CopyStableSorted(IReadOnlyList<GradientStop> stops)
    {
        // OrderBy is a documented-stable sort (unlike Array.Sort/List<T>.Sort), which is required
        // to preserve caller-supplied relative order among stops sharing the same Offset - see
        // this class's remarks.
        var sorted = stops.OrderBy(stop => stop.Offset).ToArray();
        return new ReadOnlyCollection<GradientStop>(sorted);
    }
}
