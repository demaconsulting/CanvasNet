// cspell:ignore coplanar collinear lexicographic lexicographically canonicalized canonicalization

using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.Tessellation;

/// <summary>
///     Exact convex hulls of point sets: <see cref="Hull3D"/> builds the closed, outward-wound triangle
///     <see cref="Mesh"/> of a three-dimensional point set and <see cref="Hull2D"/> the counter-clockwise
///     boundary polygon of a planar one.
/// </summary>
/// <remarks>
///     <para>
///         <b>Exactness and determinism.</b> Every orientation decision uses the exact predicates of
///         <see cref="SolidPredicates"/> and <see cref="PolygonPredicates"/>; no tolerance is involved. The
///         input is first reduced to the sorted set of its distinct points (negative zero is canonicalized
///         to positive zero, duplicates are dropped), so the result depends only on the point <em>set</em>:
///         any order and any repetition of the same points give a byte-identical mesh or polygon. There is
///         no randomness and no dependence on hashing order.
///     </para>
///     <para>
///         <b>Output of <see cref="Hull3D"/>.</b> The mesh is welded (every vertex is created once and shared
///         by index), closed, wound counter-clockwise when seen from outside and has a positive volume, so it
///         is a valid constructive-solid-geometry operand. Its vertices are exactly the strictly extreme points
///         of the input: interior points and points that merely lie on a face or an edge of the hull never
///         appear. Each planar facet is fan-triangulated from its lowest-sorted corner, so the mesh depends only
///         on the hull polytope (adding or removing non-extreme points changes nothing) and has exactly
///         <c>2V - 4</c> triangles. In exact arithmetic no triangle has zero area; for coordinates below about
///         1e-150 the double-precision cross product used by <c>Mesh</c> consumers can still underflow to zero,
///         which is outside the supported range.
///     </para>
///     <para>
///         <b>Work and memory.</b> Sorting is O(n log n). The incremental construction inserts the points in
///         sorted order; an insertion costs time proportional to the faces it replaces plus the faces around
///         the previously inserted vertex, which is O(n log n) in total for typical input and O(n * h) in the
///         worst case (h = number of hull vertices, for example adversarial cyclic-polytope-like input).
///         Memory is O(n) and the result has at most <c>2n - 4</c> triangles: the input is copied exactly once into
///         one array of 24 bytes per point, which is validated, sorted and de-duplicated in place (no list, no
///         second array), and the construction adds about 9 bytes per point plus face tables proportional to the
///         hull. The hull is computed in internal arrays and only then is a <see cref="Mesh"/> allocated. The operation is single-threaded and
///         polls the cancellation token regularly (sorting itself cannot be interrupted).
///     </para>
///     <para>
///         <b>Failure contract.</b> A null sequence throws <see cref="ArgumentNullException"/>; a non-finite
///         coordinate throws <see cref="ArgumentException"/>. <see cref="Hull3D"/> also throws
///         <see cref="ArgumentException"/> for degenerate input that spans no volume (fewer than four distinct
///         points, all collinear, all coplanar), whereas <see cref="Hull2D"/> returns the one or two extreme
///         points of a degenerate set and leaves the decision to the caller. Cancellation throws
///         <see cref="OperationCanceledException"/>. All members are stateless and thread-safe.
///     </para>
/// </remarks>
public static class ConvexHull
{
    /// <summary>Number of elementary steps between two cancellation polls of the copy and chain loops.</summary>
    private const int PollInterval = 4096;

    /// <summary>
    ///     Computes the convex hull of a set of points as a closed triangle mesh.
    /// </summary>
    /// <remarks>
    ///     The result is a function of the point set only (see the type remarks), contains every extreme
    ///     point, no interior point, and is a valid closed, outward-wound, positive-volume mesh. The work is
    ///     O(n log n) typically and O(n * h) in the worst case; see the type remarks.
    /// </remarks>
    /// <param name="points">The points; every coordinate must be finite. Duplicates and any order are allowed.</param>
    /// <param name="cancellationToken">A token that cancels the computation.</param>
    /// <returns>The hull mesh.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when a coordinate is not finite, or when the points do not span a volume: fewer than four
    ///     distinct points, all points on one line, or all points in one plane (the message names the case).
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the internal closure check fails, which cannot happen for valid input and is reported
    ///     instead of returning a defective mesh.
    /// </exception>
    public static Mesh Hull3D(IEnumerable<Vector3D> points, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);
        cancellationToken.ThrowIfCancellationRequested();

        // Reduce to the sorted distinct point set so the result cannot depend on order or repetition
        var copy = Materialize3D(points, cancellationToken, out var length);
        return Hull3DInPlace(copy, length, cancellationToken);
    }

    /// <summary>
    ///     Computes the hull of the first <paramref name="length"/> points of an array the caller hands over: the
    ///     points are validated, canonicalized, sorted and de-duplicated in place, so no second copy of a large
    ///     point set exists, and the array content is unspecified afterwards.
    /// </summary>
    /// <param name="points">The array that holds the points as a prefix; it belongs to the call.</param>
    /// <param name="length">The number of points.</param>
    /// <param name="cancellationToken">A token that cancels the computation.</param>
    /// <returns>The hull mesh (the same mesh as <see cref="Hull3D"/> gives for these points).</returns>
    /// <exception cref="ArgumentException">Thrown when a coordinate is not finite or the points span no volume.</exception>
    internal static Mesh Hull3DInPlace(Vector3D[] points, int length, CancellationToken cancellationToken)
    {
        var distinct = SortDistinct3D(points, length, cancellationToken);
        return HullBuilder3D.Build(points, distinct, cancellationToken);
    }

    /// <summary>
    ///     Computes the convex hull of a set of planar points as a strictly convex polygon.
    /// </summary>
    /// <remarks>
    ///     The polygon has no collinear vertices, winds counter-clockwise and starts at the lexicographically
    ///     smallest point (by X, then Y). The decision at every turn is the exact orientation predicate
    ///     (Andrew's monotone chain), so nearly collinear points are classified correctly. The work is
    ///     O(n log n) in every case and the result depends only on the point set.
    /// </remarks>
    /// <param name="points">The points; every coordinate must be finite. Duplicates and any order are allowed.</param>
    /// <param name="cancellationToken">A token that cancels the computation.</param>
    /// <returns>
    ///     The hull vertices in counter-clockwise order. A degenerate set does not throw: no point gives an
    ///     empty list, one distinct point gives that point and collinear points give the two extreme points.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a coordinate is not finite.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static IReadOnlyList<Vector2D> Hull2D(IEnumerable<Vector2D> points, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);
        cancellationToken.ThrowIfCancellationRequested();

        // Reduce to the sorted distinct point set so the result cannot depend on order or repetition
        var sorted = CopySortedDistinct2D(points, cancellationToken);
        var order = new int[sorted.Length];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        var chain = ChainIndices(sorted, order, cancellationToken);
        var hull = new Vector2D[chain.Length];
        for (var i = 0; i < hull.Length; i++)
        {
            hull[i] = sorted[chain[i]];
        }

        return Array.AsReadOnly(hull);
    }

    /// <summary>
    ///     Computes the indices of the convex hull of planar points given in lexicographic order, with
    ///     Andrew's monotone chain and exact turn decisions.
    /// </summary>
    /// <param name="points">The points (finite, pairwise distinct).</param>
    /// <param name="order">
    ///     The indices of the points to hull, sorted by increasing X and then increasing Y of the point they
    ///     address.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the computation.</param>
    /// <returns>
    ///     The hull indices, strictly convex, counter-clockwise, starting at <c>order[0]</c>; fewer than three
    ///     entries for a degenerate set.
    /// </returns>
    internal static int[] ChainIndices(Vector2D[] points, int[] order, CancellationToken cancellationToken)
    {
        // At most two distinct points are their own hull
        var count = order.Length;
        if (count <= 2)
        {
            return (int[])order.Clone();
        }

        var hull = new int[2 * count];
        var size = 0;

        // Lower hull from the smallest to the largest point
        for (var i = 0; i < count; i++)
        {
            if (i % PollInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            while (size >= 2 && PolygonPredicates.Orient2D(points[hull[size - 2]], points[hull[size - 1]], points[order[i]]) <= 0)
            {
                size--;
            }

            hull[size++] = order[i];
        }

        // Upper hull back to the smallest point; the first point of the lower hull is repeated at the end
        var lowerSize = size + 1;
        for (var i = count - 2; i >= 0; i--)
        {
            if (i % PollInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            while (size >= lowerSize && PolygonPredicates.Orient2D(points[hull[size - 2]], points[hull[size - 1]], points[order[i]]) <= 0)
            {
                size--;
            }

            hull[size++] = order[i];
        }

        var result = new int[size - 1];
        Array.Copy(hull, result, result.Length);
        return result;
    }

    /// <summary>
    ///     Copies a point sequence into one array (no intermediate list).
    /// </summary>
    /// <param name="points">The input points.</param>
    /// <param name="cancellationToken">A token that cancels the copy.</param>
    /// <param name="length">Receives the number of points copied, a prefix of the returned array.</param>
    /// <returns>The one array that holds the points; entries after <paramref name="length"/> are unused.</returns>
    private static Vector3D[] Materialize3D(IEnumerable<Vector3D> points, CancellationToken cancellationToken, out int length)
    {
        // One array: sized exactly when the count is known without enumerating, grown by doubling otherwise
        var array = new Vector3D[points.TryGetNonEnumeratedCount(out var known) ? known : 4];
        length = 0;
        foreach (var point in points)
        {
            // Never write past the array, even for a sequence that grows while it is copied
            if (length == array.Length)
            {
                Array.Resize(ref array, Math.Max(4, length * 2));
            }

            array[length++] = point;
            if (length % PollInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return array;
    }

    /// <summary>
    ///     Validates, canonicalizes, sorts and de-duplicates the first points of an array, all in place.
    /// </summary>
    /// <param name="points">The array; its prefix becomes the sorted distinct points.</param>
    /// <param name="length">The number of points to process.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    /// <returns>The number of distinct points, which now form the prefix of the array in ascending lexicographic order (X, then Y, then Z).</returns>
    /// <exception cref="ArgumentException">Thrown when a coordinate is not finite.</exception>
    private static int SortDistinct3D(Vector3D[] points, int length, CancellationToken cancellationToken)
    {
        // Adding zero turns negative zero into positive zero so that equal points compare equal
        for (var i = 0; i < length; i++)
        {
            var point = points[i];
            if (!point.IsFinite)
            {
                throw new ArgumentException("Every point coordinate must be finite.", nameof(points));
            }

            points[i] = new Vector3D(point.X + 0.0, point.Y + 0.0, point.Z + 0.0);
            if ((i + 1) % PollInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Equal points are identical after canonicalization, so an unstable sort of the used prefix is deterministic
        points.AsSpan(0, length).Sort(CompareLexicographic);

        // Drop exact duplicates in place, which are adjacent after sorting
        var distinct = 0;
        for (var i = 0; i < length; i++)
        {
            if (distinct == 0 || points[distinct - 1] != points[i])
            {
                points[distinct++] = points[i];
            }
        }

        return distinct;
    }

    /// <summary>
    ///     Copies, validates and canonicalizes a planar point sequence into a sorted array of distinct points.
    /// </summary>
    /// <param name="points">The input points.</param>
    /// <param name="cancellationToken">A token that cancels the copy.</param>
    /// <returns>The distinct points in ascending lexicographic order (X, then Y).</returns>
    /// <exception cref="ArgumentException">Thrown when a coordinate is not finite.</exception>
    private static Vector2D[] CopySortedDistinct2D(IEnumerable<Vector2D> points, CancellationToken cancellationToken)
    {
        var list = new List<Vector2D>(points is ICollection<Vector2D> collection ? collection.Count : 0);
        var counter = 0;
        foreach (var point in points)
        {
            if (!point.IsFinite)
            {
                throw new ArgumentException("Every point coordinate must be finite.", nameof(points));
            }

            if (++counter % PollInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            list.Add(new Vector2D(point.X + 0.0, point.Y + 0.0));
        }

        var array = list.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        Array.Sort(array, CompareLexicographic);

        var distinct = 0;
        for (var i = 0; i < array.Length; i++)
        {
            if (distinct == 0 || array[distinct - 1] != array[i])
            {
                array[distinct++] = array[i];
            }
        }

        Array.Resize(ref array, distinct);
        return array;
    }

    /// <summary>Compares two canonicalized points lexicographically by X, then Y, then Z.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns>A negative, zero or positive value.</returns>
    private static int CompareLexicographic(Vector3D left, Vector3D right)
    {
        var result = left.X.CompareTo(right.X);
        if (result != 0)
        {
            return result;
        }

        result = left.Y.CompareTo(right.Y);
        return result != 0 ? result : left.Z.CompareTo(right.Z);
    }

    /// <summary>Compares two canonicalized planar points lexicographically by X, then Y.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns>A negative, zero or positive value.</returns>
    private static int CompareLexicographic(Vector2D left, Vector2D right)
    {
        var result = left.X.CompareTo(right.X);
        return result != 0 ? result : left.Y.CompareTo(right.Y);
    }
}
