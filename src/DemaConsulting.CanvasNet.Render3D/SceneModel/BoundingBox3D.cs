namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An immutable, double-precision axis-aligned bounding box in model space.
/// </summary>
/// <remarks>
///     The <see cref="Empty"/> box (minimum = +infinity, maximum = -infinity) is the identity of
///     <see cref="Union(BoundingBox3D)"/> and is what a scene without geometry reports. Note that
///     <c>default(BoundingBox3D)</c> is the degenerate point box at the origin, <b>not</b> empty.
///     All members are pure and thread-safe.
/// </remarks>
/// <param name="Min">The corner with the smallest coordinates.</param>
/// <param name="Max">The corner with the largest coordinates.</param>
public readonly record struct BoundingBox3D(Vector3D Min, Vector3D Max)
{
    /// <summary>
    ///     Gets the empty box (<c>Min = +infinity</c>, <c>Max = -infinity</c>), the identity element of
    ///     <see cref="Union(BoundingBox3D)"/> and <see cref="Union(Vector3D)"/>.
    /// </summary>
    public static BoundingBox3D Empty { get; } = new(
        new Vector3D(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
        new Vector3D(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));

    /// <summary>Gets a value indicating whether the box contains no points (any minimum coordinate exceeds its maximum).</summary>
    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    /// <summary>Gets the extent along each axis (<c>Max - Min</c>); the zero vector for an empty box.</summary>
    public Vector3D Size => IsEmpty ? Vector3D.Zero : Max - Min;

    /// <summary>Gets the midpoint of the box; the origin for an empty box.</summary>
    public Vector3D Center => IsEmpty ? Vector3D.Zero : (Min + Max) * 0.5;

    /// <summary>Gets the length of the box's space diagonal (the length of <see cref="Size"/>); 0 for an empty box.</summary>
    public double Diagonal => Size.Length;

    /// <summary>Returns the smallest box containing both this box and <paramref name="other"/>.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>The union box; an empty operand contributes nothing.</returns>
    public BoundingBox3D Union(BoundingBox3D other)
    {
        // Empty boxes are the identity: return the other operand unchanged
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return new BoundingBox3D(Vector3D.Min(Min, other.Min), Vector3D.Max(Max, other.Max));
    }

    /// <summary>Returns the smallest box containing this box and <paramref name="point"/>.</summary>
    /// <param name="point">The point to include.</param>
    /// <returns>The grown box.</returns>
    public BoundingBox3D Union(Vector3D point) =>
        IsEmpty ? new BoundingBox3D(point, point) : new BoundingBox3D(Vector3D.Min(Min, point), Vector3D.Max(Max, point));

    /// <summary>
    ///     Returns the axis-aligned box of the eight transformed corners. The result is
    ///     conservative (possibly looser than the tight bounds of the underlying geometry) when
    ///     the transform rotates by a non-multiple of 90 degrees; an empty box stays empty.
    /// </summary>
    /// <param name="matrix">The transform to apply.</param>
    /// <returns>The axis-aligned bounds of the transformed corners.</returns>
    public BoundingBox3D Transform(Matrix4x4D matrix)
    {
        // Transforming the empty box would create NaNs from infinities; it stays empty
        if (IsEmpty)
        {
            return Empty;
        }

        var result = Empty;
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3D(
                (i & 1) == 0 ? Min.X : Max.X,
                (i & 2) == 0 ? Min.Y : Max.Y,
                (i & 4) == 0 ? Min.Z : Max.Z);
            result = result.Union(matrix.TransformPoint(corner));
        }

        return result;
    }
}
