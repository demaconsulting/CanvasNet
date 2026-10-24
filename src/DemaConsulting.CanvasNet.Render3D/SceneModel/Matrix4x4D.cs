// cspell:ignore Rodrigues

using System.Globalization;

namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An immutable 4x4 double-precision matrix for model-space affine transforms, using the same
///     row-vector convention as <see cref="System.Numerics.Matrix4x4"/>.
/// </summary>
/// <remarks>
///     <para>
///         A point is transformed as <c>v' = v * M</c> with the translation stored in
///         <see cref="M41"/>..<see cref="M43"/>. Consequently <c>a * b</c> applies <c>a</c> first
///         and <c>b</c> second, so a child-to-parent chain is composed as
///         <c>child * parent</c>. The type is intended for affine transforms; the fourth column is
///         carried and inverted but ignored by <see cref="TransformPoint"/> and
///         <see cref="TransformDirection"/>.
///     </para>
///     <para>
///         <b>Caution:</b> <c>default(Matrix4x4D)</c> is the all-zero matrix, not the identity;
///         always use <see cref="Identity"/> (every property of this assembly that holds a
///         matrix is initialized with it). Rotation factories snap sine/cosine to exact 0 and ±1
///         for multiples of 90 degrees so axis-aligned CAD rotations stay exact. All members are
///         pure and thread-safe.
///     </para>
/// </remarks>
public readonly struct Matrix4x4D : IEquatable<Matrix4x4D>
{
    /// <summary>Number of elements in a 4x4 matrix.</summary>
    private const int ElementCount = 16;

    /// <summary>Determinant magnitude below which a matrix is treated as singular.</summary>
    private const double SingularThreshold = 1e-300;

    /// <summary>Angular tolerance (in quarter turns) within which a rotation angle is snapped to an exact multiple of 90 degrees.</summary>
    private const double QuarterTurnSnapTolerance = 1e-12;

    /// <summary>
    ///     Creates a matrix from 16 row-major elements. Private so that callers go through the
    ///     named factories (and so no 16-parameter public constructor exists).
    /// </summary>
    /// <param name="m">Exactly 16 elements in row-major order.</param>
    private Matrix4x4D(ReadOnlySpan<double> m)
    {
        M11 = m[0];
        M12 = m[1];
        M13 = m[2];
        M14 = m[3];
        M21 = m[4];
        M22 = m[5];
        M23 = m[6];
        M24 = m[7];
        M31 = m[8];
        M32 = m[9];
        M33 = m[10];
        M34 = m[11];
        M41 = m[12];
        M42 = m[13];
        M43 = m[14];
        M44 = m[15];
    }

    /// <summary>Gets the identity matrix (no transformation).</summary>
    public static Matrix4x4D Identity { get; } = CreateIdentity();

    /// <summary>Gets element row 1, column 1.</summary>
    public double M11 { get; }

    /// <summary>Gets element row 1, column 2.</summary>
    public double M12 { get; }

    /// <summary>Gets element row 1, column 3.</summary>
    public double M13 { get; }

    /// <summary>Gets element row 1, column 4.</summary>
    public double M14 { get; }

    /// <summary>Gets element row 2, column 1.</summary>
    public double M21 { get; }

    /// <summary>Gets element row 2, column 2.</summary>
    public double M22 { get; }

    /// <summary>Gets element row 2, column 3.</summary>
    public double M23 { get; }

    /// <summary>Gets element row 2, column 4.</summary>
    public double M24 { get; }

    /// <summary>Gets element row 3, column 1.</summary>
    public double M31 { get; }

    /// <summary>Gets element row 3, column 2.</summary>
    public double M32 { get; }

    /// <summary>Gets element row 3, column 3.</summary>
    public double M33 { get; }

    /// <summary>Gets element row 3, column 4.</summary>
    public double M34 { get; }

    /// <summary>Gets element row 4, column 1 (X translation).</summary>
    public double M41 { get; }

    /// <summary>Gets element row 4, column 2 (Y translation).</summary>
    public double M42 { get; }

    /// <summary>Gets element row 4, column 3 (Z translation).</summary>
    public double M43 { get; }

    /// <summary>Gets element row 4, column 4.</summary>
    public double M44 { get; }

    /// <summary>
    ///     Gets the determinant of the full 4x4 matrix. For an affine transform this equals the
    ///     determinant of the upper-left 3x3 block; a negative value means the transform mirrors
    ///     space and therefore reverses triangle winding.
    /// </summary>
    public double Determinant
    {
        get
        {
            // Laplace expansion along the first row using shared 2x2 sub-determinants
            var kpLo = (M33 * M44) - (M34 * M43);
            var jpLn = (M32 * M44) - (M34 * M42);
            var joKn = (M32 * M43) - (M33 * M42);
            var ipLm = (M31 * M44) - (M34 * M41);
            var ioKm = (M31 * M43) - (M33 * M41);
            var inJm = (M31 * M42) - (M32 * M41);

            return (M11 * ((M22 * kpLo) - (M23 * jpLn) + (M24 * joKn)))
                - (M12 * ((M21 * kpLo) - (M23 * ipLm) + (M24 * ioKm)))
                + (M13 * ((M21 * jpLn) - (M22 * ipLm) + (M24 * inJm)))
                - (M14 * ((M21 * joKn) - (M22 * ioKm) + (M23 * inJm)));
        }
    }

    /// <summary>Gets a value indicating whether all 16 elements are finite (none is NaN or infinite).</summary>
    public bool IsFinite
    {
        get
        {
            Span<double> m = stackalloc double[ElementCount];
            CopyTo(m);
            foreach (var value in m)
            {
                if (!double.IsFinite(value))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    ///     Creates a matrix from 16 elements given in row-major order
    ///     (<c>M11, M12, M13, M14, M21, ... M44</c>).
    /// </summary>
    /// <param name="elements">The 16 row-major elements.</param>
    /// <returns>The matrix.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="elements"/> does not contain exactly 16 values.</exception>
    public static Matrix4x4D FromRowMajor(ReadOnlySpan<double> elements)
    {
        // Reject the wrong element count up front; the private constructor assumes 16 values
        if (elements.Length != ElementCount)
        {
            throw new ArgumentException("A 4x4 matrix requires exactly 16 elements.", nameof(elements));
        }

        return new Matrix4x4D(elements);
    }

    /// <summary>Creates a translation by <paramref name="offset"/>.</summary>
    /// <param name="offset">The translation vector.</param>
    /// <returns>The translation matrix.</returns>
    public static Matrix4x4D CreateTranslation(Vector3D offset)
    {
        Span<double> m = stackalloc double[ElementCount];
        FillIdentity(m);
        m[12] = offset.X;
        m[13] = offset.Y;
        m[14] = offset.Z;
        return new Matrix4x4D(m);
    }

    /// <summary>Creates a non-uniform scale about the origin.</summary>
    /// <param name="scale">The per-axis scale factors (negative values mirror).</param>
    /// <returns>The scale matrix.</returns>
    public static Matrix4x4D CreateScale(Vector3D scale)
    {
        Span<double> m = stackalloc double[ElementCount];
        FillIdentity(m);
        m[0] = scale.X;
        m[5] = scale.Y;
        m[10] = scale.Z;
        return new Matrix4x4D(m);
    }

    /// <summary>Creates a uniform scale about the origin.</summary>
    /// <param name="scale">The scale factor applied to all three axes.</param>
    /// <returns>The scale matrix.</returns>
    public static Matrix4x4D CreateScale(double scale) => CreateScale(new Vector3D(scale, scale, scale));

    /// <summary>Creates a right-handed rotation about the X axis (counter-clockwise looking down the axis toward the origin).</summary>
    /// <param name="degrees">The angle in degrees; multiples of 90 are exact.</param>
    /// <returns>The rotation matrix.</returns>
    public static Matrix4x4D CreateRotationX(double degrees)
    {
        var (s, c) = ExactSinCos(degrees);
        Span<double> m = stackalloc double[ElementCount];
        FillIdentity(m);
        m[5] = c;
        m[6] = s;
        m[9] = -s;
        m[10] = c;
        return new Matrix4x4D(m);
    }

    /// <summary>Creates a right-handed rotation about the Y axis.</summary>
    /// <param name="degrees">The angle in degrees; multiples of 90 are exact.</param>
    /// <returns>The rotation matrix.</returns>
    public static Matrix4x4D CreateRotationY(double degrees)
    {
        var (s, c) = ExactSinCos(degrees);
        Span<double> m = stackalloc double[ElementCount];
        FillIdentity(m);
        m[0] = c;
        m[2] = -s;
        m[8] = s;
        m[10] = c;
        return new Matrix4x4D(m);
    }

    /// <summary>Creates a right-handed rotation about the Z axis (so +90 degrees maps +X to +Y).</summary>
    /// <param name="degrees">The angle in degrees; multiples of 90 are exact.</param>
    /// <returns>The rotation matrix.</returns>
    public static Matrix4x4D CreateRotationZ(double degrees)
    {
        var (s, c) = ExactSinCos(degrees);
        Span<double> m = stackalloc double[ElementCount];
        FillIdentity(m);
        m[0] = c;
        m[1] = s;
        m[4] = -s;
        m[5] = c;
        return new Matrix4x4D(m);
    }

    /// <summary>Creates a right-handed rotation of <paramref name="degrees"/> about an arbitrary axis through the origin.</summary>
    /// <param name="axis">The rotation axis; it is normalized internally and must be finite and non-zero.</param>
    /// <param name="degrees">The angle in degrees; multiples of 90 use exact sine/cosine values.</param>
    /// <returns>The rotation matrix.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="axis"/> is zero-length or not finite.</exception>
    public static Matrix4x4D CreateFromAxisAngle(Vector3D axis, double degrees)
    {
        // A zero or non-finite axis has no direction to rotate about
        if (!axis.IsFinite || axis.Length <= 0.0)
        {
            throw new ArgumentException("The rotation axis must be finite and non-zero.", nameof(axis));
        }

        var n = axis.Normalize();
        var (s, c) = ExactSinCos(degrees);
        var t = 1.0 - c;

        // Rodrigues' formula, transposed into this type's row-vector convention
        Span<double> m = stackalloc double[ElementCount];
        FillIdentity(m);
        m[0] = c + (t * n.X * n.X);
        m[1] = (t * n.X * n.Y) + (s * n.Z);
        m[2] = (t * n.X * n.Z) - (s * n.Y);
        m[4] = (t * n.X * n.Y) - (s * n.Z);
        m[5] = c + (t * n.Y * n.Y);
        m[6] = (t * n.Y * n.Z) + (s * n.X);
        m[8] = (t * n.X * n.Z) + (s * n.Y);
        m[9] = (t * n.Y * n.Z) - (s * n.X);
        m[10] = c + (t * n.Z * n.Z);
        return new Matrix4x4D(m);
    }

    /// <summary>
    ///     Creates a reflection through the plane through the origin whose normal is
    ///     <paramref name="planeNormal"/> (<c>I - 2 n nT</c>). The result has determinant -1.
    /// </summary>
    /// <param name="planeNormal">The plane normal; normalized internally; must be finite and non-zero.</param>
    /// <returns>The mirror matrix.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="planeNormal"/> is zero-length or not finite.</exception>
    public static Matrix4x4D CreateMirror(Vector3D planeNormal)
    {
        // A zero normal does not define a plane
        if (!planeNormal.IsFinite || planeNormal.Length <= 0.0)
        {
            throw new ArgumentException("The mirror plane normal must be finite and non-zero.", nameof(planeNormal));
        }

        var n = planeNormal.Normalize();
        Span<double> m = stackalloc double[ElementCount];
        FillIdentity(m);
        m[0] -= 2.0 * n.X * n.X;
        m[1] -= 2.0 * n.X * n.Y;
        m[2] -= 2.0 * n.X * n.Z;
        m[4] -= 2.0 * n.Y * n.X;
        m[5] -= 2.0 * n.Y * n.Y;
        m[6] -= 2.0 * n.Y * n.Z;
        m[8] -= 2.0 * n.Z * n.X;
        m[9] -= 2.0 * n.Z * n.Y;
        m[10] -= 2.0 * n.Z * n.Z;
        return new Matrix4x4D(m);
    }

    /// <summary>
    ///     Multiplies two matrices. With the row-vector convention the result applies
    ///     <paramref name="left"/> first and <paramref name="right"/> second.
    /// </summary>
    /// <param name="left">The transform applied first.</param>
    /// <param name="right">The transform applied second.</param>
    /// <returns>The combined transform.</returns>
    public static Matrix4x4D operator *(Matrix4x4D left, Matrix4x4D right)
    {
        Span<double> a = stackalloc double[ElementCount];
        Span<double> b = stackalloc double[ElementCount];
        Span<double> r = stackalloc double[ElementCount];
        left.CopyTo(a);
        right.CopyTo(b);

        // Standard row-by-column product
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var sum = 0.0;
                for (var k = 0; k < 4; k++)
                {
                    sum += a[(row * 4) + k] * b[(k * 4) + col];
                }

                r[(row * 4) + col] = sum;
            }
        }

        return new Matrix4x4D(r);
    }

    /// <summary>Determines whether two matrices are element-wise equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when all elements are equal.</returns>
    public static bool operator ==(Matrix4x4D left, Matrix4x4D right) => left.Equals(right);

    /// <summary>Determines whether two matrices differ in any element.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when any element differs.</returns>
    public static bool operator !=(Matrix4x4D left, Matrix4x4D right) => !left.Equals(right);

    /// <summary>
    ///     Attempts to compute the inverse by Gauss-Jordan elimination with partial pivoting.
    /// </summary>
    /// <param name="inverse">Receives the inverse when the method returns <see langword="true"/>; otherwise the zero matrix.</param>
    /// <returns>
    ///     <see langword="false"/> when the matrix is singular (determinant magnitude below
    ///     1e-300) or the result is not finite; otherwise <see langword="true"/>.
    /// </returns>
    public bool TryInvert(out Matrix4x4D inverse)
    {
        inverse = default;

        // A (near-)zero or non-finite determinant means no usable inverse exists
        var det = Determinant;
        if (!double.IsFinite(det) || Math.Abs(det) < SingularThreshold)
        {
            return false;
        }

        Span<double> a = stackalloc double[ElementCount];
        Span<double> inv = stackalloc double[ElementCount];
        CopyTo(a);
        FillIdentity(inv);

        for (var col = 0; col < 4; col++)
        {
            // Choose the largest remaining pivot in this column for numerical stability
            var pivot = col;
            var best = Math.Abs(a[(col * 4) + col]);
            for (var row = col + 1; row < 4; row++)
            {
                var candidate = Math.Abs(a[(row * 4) + col]);
                if (candidate > best)
                {
                    best = candidate;
                    pivot = row;
                }
            }

            if (best < SingularThreshold)
            {
                return false;
            }

            if (pivot != col)
            {
                SwapRows(a, pivot, col);
                SwapRows(inv, pivot, col);
            }

            // Normalize the pivot row, then eliminate the column from every other row
            var scale = 1.0 / a[(col * 4) + col];
            for (var k = 0; k < 4; k++)
            {
                a[(col * 4) + k] *= scale;
                inv[(col * 4) + k] *= scale;
            }

            for (var row = 0; row < 4; row++)
            {
                if (row == col)
                {
                    continue;
                }

                var factor = a[(row * 4) + col];
                for (var k = 0; k < 4; k++)
                {
                    a[(row * 4) + k] -= factor * a[(col * 4) + k];
                    inv[(row * 4) + k] -= factor * inv[(col * 4) + k];
                }
            }
        }

        var result = new Matrix4x4D(inv);
        if (!result.IsFinite)
        {
            return false;
        }

        inverse = result;
        return true;
    }

    /// <summary>Transforms a point (rotation, scale and translation all apply).</summary>
    /// <param name="point">The point in the source space.</param>
    /// <returns>The point in the destination space.</returns>
    public Vector3D TransformPoint(Vector3D point) =>
        new(
            (point.X * M11) + (point.Y * M21) + (point.Z * M31) + M41,
            (point.X * M12) + (point.Y * M22) + (point.Z * M32) + M42,
            (point.X * M13) + (point.Y * M23) + (point.Z * M33) + M43);

    /// <summary>Transforms a direction vector (rotation and scale apply; translation is ignored).</summary>
    /// <param name="direction">The direction in the source space.</param>
    /// <returns>The direction in the destination space (not re-normalized).</returns>
    public Vector3D TransformDirection(Vector3D direction) =>
        new(
            (direction.X * M11) + (direction.Y * M21) + (direction.Z * M31),
            (direction.X * M12) + (direction.Y * M22) + (direction.Z * M32),
            (direction.X * M13) + (direction.Y * M23) + (direction.Z * M33));

    /// <inheritdoc/>
    public bool Equals(Matrix4x4D other)
    {
        Span<double> a = stackalloc double[ElementCount];
        Span<double> b = stackalloc double[ElementCount];
        CopyTo(a);
        other.CopyTo(b);
        return a.SequenceEqual(b);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Matrix4x4D other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        Span<double> m = stackalloc double[ElementCount];
        CopyTo(m);
        var hash = default(HashCode);
        foreach (var value in m)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture,
            $"[{M11}, {M12}, {M13}, {M14}; {M21}, {M22}, {M23}, {M24}; {M31}, {M32}, {M33}, {M34}; {M41}, {M42}, {M43}, {M44}]");

    /// <summary>Builds the identity matrix (used to initialize <see cref="Identity"/>).</summary>
    /// <returns>The identity matrix.</returns>
    private static Matrix4x4D CreateIdentity()
    {
        Span<double> m = stackalloc double[ElementCount];
        FillIdentity(m);
        return new Matrix4x4D(m);
    }

    /// <summary>Writes the identity matrix into a 16-element row-major span.</summary>
    /// <param name="m">The destination span (16 elements).</param>
    private static void FillIdentity(Span<double> m)
    {
        m.Clear();
        m[0] = 1.0;
        m[5] = 1.0;
        m[10] = 1.0;
        m[15] = 1.0;
    }

    /// <summary>Swaps two rows of a 4x4 row-major matrix span.</summary>
    /// <param name="m">The matrix elements.</param>
    /// <param name="row1">The first row index.</param>
    /// <param name="row2">The second row index.</param>
    private static void SwapRows(Span<double> m, int row1, int row2)
    {
        for (var k = 0; k < 4; k++)
        {
            (m[(row1 * 4) + k], m[(row2 * 4) + k]) = (m[(row2 * 4) + k], m[(row1 * 4) + k]);
        }
    }

    /// <summary>
    ///     Computes sine and cosine of an angle in degrees, returning exact 0 and ±1 for angles
    ///     within 1e-12 quarter turns of a multiple of 90 degrees.
    /// </summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>The sine and cosine.</returns>
    private static (double Sin, double Cos) ExactSinCos(double degrees)
    {
        // Snap near-multiples of 90 degrees so axis-aligned CAD rotations have no residual error
        var quarters = degrees / 90.0;
        var rounded = Math.Round(quarters);
        if (Math.Abs(quarters - rounded) < QuarterTurnSnapTolerance)
        {
            var q = (int)(((long)rounded % 4 + 4) % 4);
            return q switch
            {
                0 => (0.0, 1.0),
                1 => (1.0, 0.0),
                2 => (0.0, -1.0),
                _ => (-1.0, 0.0),
            };
        }

        return Math.SinCos(degrees * Math.PI / 180.0);
    }

    /// <summary>Copies the 16 elements, row-major, into <paramref name="m"/>.</summary>
    /// <param name="m">The destination span (16 elements).</param>
    private void CopyTo(Span<double> m)
    {
        m[0] = M11;
        m[1] = M12;
        m[2] = M13;
        m[3] = M14;
        m[4] = M21;
        m[5] = M22;
        m[6] = M23;
        m[7] = M24;
        m[8] = M31;
        m[9] = M32;
        m[10] = M33;
        m[11] = M34;
        m[12] = M41;
        m[13] = M42;
        m[14] = M43;
        m[15] = M44;
    }
}
