// cspell:ignore multmatrix
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     Builds the transform modules: <c>translate</c>, <c>rotate</c>, <c>scale</c>, <c>mirror</c>,
///     <c>multmatrix</c> and <c>resize</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Dialect.</b> A transform applies to the implicit union of its children. <c>translate(v)</c>,
///         <c>scale(v)</c> and <c>mirror(v)</c> take a number (only <c>scale</c>) or a vector of one to three numbers
///         (missing components are 0 for <c>translate</c> and <c>mirror</c>, 1 for <c>scale</c>);
///         <c>rotate(a)</c> with a number rotates about Z, with a vector of one to three angles in degrees rotates
///         about X, then Y, then Z (angles applied in that order), and <c>rotate(a, v)</c> with a number rotates
///         about the axis <c>v</c> (right-hand rule); a zero <c>v</c> or a zero <c>mirror</c> normal warns and leaves
///         the children as they are. <c>multmatrix(m)</c> takes a 4x4 matrix in column-vector convention (or its
///         first three rows); a bottom row other than <c>[0, 0, 0, 1]</c> is projective, which cannot be rendered,
///         and is reported as the unsupported feature <c>scad-multmatrix-projective</c>. <c>resize</c> scales about
///         the origin so that the bounding box of the children gets the requested size (see
///         <c>ScadTransforms.Resize</c>).
///     </para>
///     <para>
///         <b>Invalid input.</b> A supplied argument of the wrong type, a non-finite number or a transform that
///         collapses the children (a singular matrix, such as a zero scale) is reported as a warning at the instance
///         and yields nothing for it, so the problem is visible instead of producing degenerate geometry. An absent
///         argument is the identity.
///     </para>
///     <para>
///         <b>Representation.</b> A solid under a transform is wrapped in a <see cref="GroupNode"/> carrying the
///         matrix (Render3D composes the chain child-first and flips the winding of mirrored instances itself); a
///         2D shape gets a planar transform node (the in-plane part of the matrix, warning when the matrix couples
///         Z, see <see cref="ScadShape2D"/>). The identity adds no wrapper. The methods are stateless and
///         thread-safe.
///     </para>
/// </remarks>
internal static partial class ScadTransforms
{
    /// <summary>The relative size of the determinant below which a transform is treated as singular.</summary>
    private const double SingularTolerance = 1e-12;

    /// <summary>The parameter names of <c>translate</c>, <c>scale</c> and <c>mirror</c>.</summary>
    private static readonly string[] VectorParameters = ["v"];

    /// <summary>The parameter names of <c>rotate</c> in positional order.</summary>
    private static readonly string[] RotateParameters = ["a", "v"];

    /// <summary>The parameter names of <c>multmatrix</c>.</summary>
    private static readonly string[] MultmatrixParameters = ["m"];

    /// <summary>
    ///     The outcome of reading an optional vector argument.
    /// </summary>
    internal enum ReadState
    {
        /// <summary>The argument was not supplied (or was <c>undef</c>).</summary>
        Absent,

        /// <summary>The argument was supplied and is valid.</summary>
        Valid,

        /// <summary>The argument was supplied but is not valid.</summary>
        Invalid,
    }

    /// <summary>
    ///     Builds <c>translate(v)</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The translated union of the children.</returns>
    public static ScadGeo Translate(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, VectorParameters, context);
        var state = ReadVector(args, "v", 0.0, out var v);
        if (state == ReadState.Invalid)
        {
            return Invalid(instance, context, "v must be a vector of one to three numbers");
        }

        var matrix = state == ReadState.Absent ? Matrix4x4D.Identity : Matrix4x4D.CreateTranslation(new Vector3D(v[0], v[1], v[2]));
        return Apply(instance, results, context, matrix);
    }

    /// <summary>
    ///     Builds <c>rotate(a, v)</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The rotated union of the children.</returns>
    public static ScadGeo Rotate(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, RotateParameters, context);
        var hasAxis = args.TryGet("v", out var axisValue);
        if (!args.TryGet("a", out var angleValue))
        {
            return Apply(instance, results, context, Matrix4x4D.Identity);
        }

        double[]? axis = null;
        if (hasAxis && !ScadArguments.TryNumbers(axisValue, 3, out axis))
        {
            return Invalid(instance, context, "v must be a vector of three numbers");
        }

        if (ScadArguments.TryNumber(angleValue, out var degrees))
        {
            return Apply(instance, results, context, RotateScalar(instance, context, degrees, axis));
        }

        if (!TryReadVector(angleValue, 0.0, out var angles))
        {
            return Invalid(instance, context, "a must be a number or a vector of one to three numbers");
        }

        // The Euler form ignores an axis; say so instead of dropping it silently
        if (hasAxis)
        {
            context.Reporter.Warn("rotate: v is ignored when a is a vector", instance.Location);
        }

        // Row-vector convention: the left factor is applied first, so this is X, then Y, then Z
        var euler = Matrix4x4D.CreateRotationX(angles[0]) * Matrix4x4D.CreateRotationY(angles[1]) * Matrix4x4D.CreateRotationZ(angles[2]);
        return Apply(instance, results, context, euler);
    }

    /// <summary>
    ///     Builds <c>scale(v)</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The scaled union of the children.</returns>
    public static ScadGeo Scale(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, VectorParameters, context);
        if (!args.TryGet("v", out var value))
        {
            return Apply(instance, results, context, Matrix4x4D.Identity);
        }

        if (ScadArguments.TryNumber(value, out var factor))
        {
            return Apply(instance, results, context, Matrix4x4D.CreateScale(factor));
        }

        if (!TryReadVector(value, 1.0, out var v))
        {
            return Invalid(instance, context, "v must be a number or a vector of one to three numbers");
        }

        return Apply(instance, results, context, Matrix4x4D.CreateScale(new Vector3D(v[0], v[1], v[2])));
    }

    /// <summary>
    ///     Builds <c>mirror(v)</c>: a reflection through the plane through the origin whose normal is <c>v</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The mirrored union of the children.</returns>
    public static ScadGeo Mirror(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, VectorParameters, context);
        var state = ReadVector(args, "v", 0.0, out var v);
        if (state == ReadState.Invalid)
        {
            return Invalid(instance, context, "v must be a vector of one to three numbers");
        }

        if (state == ReadState.Absent)
        {
            return Apply(instance, results, context, Matrix4x4D.Identity);
        }

        var normal = new Vector3D(v[0], v[1], v[2]);
        if (normal.Length <= 0.0 || !double.IsFinite(normal.Length))
        {
            context.Reporter.Warn("mirror: the normal v is zero; the children are not mirrored", instance.Location);
            return Apply(instance, results, context, Matrix4x4D.Identity);
        }

        return Apply(instance, results, context, Matrix4x4D.CreateMirror(normal));
    }

    /// <summary>
    ///     Builds <c>multmatrix(m)</c>.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <returns>The transformed union of the children, or nothing when the matrix is invalid or projective.</returns>
    public static ScadGeo Multmatrix(ScadInstance? node, List<ScadGeo> results, ScadGeometryContext context)
    {
        var instance = node!;
        var args = ScadArguments.Bind(instance, MultmatrixParameters, context);
        if (!args.TryGet("m", out var value))
        {
            return Apply(instance, results, context, Matrix4x4D.Identity);
        }

        var rows = ReadMatrixRows(value);
        if (rows is null)
        {
            return Invalid(instance, context, "m must be a 4x4 matrix (or its first three rows) of finite numbers");
        }

        // A bottom row other than [0, 0, 0, 1] is a perspective transform, which the renderer cannot represent
        if (rows.Count == 4 && !(rows[3][0] == 0.0 && rows[3][1] == 0.0 && rows[3][2] == 0.0 && ScadNumbers.AreEqual(rows[3][3], 1.0)))
        {
            context.Reporter.Unsupported(
                ScadFeatureIds.MultmatrixProjective,
                "multmatrix: a matrix whose bottom row is not [0, 0, 0, 1] is a projective transform, which is not supported; the children are omitted",
                instance.Location);
            return ScadGeo.Empty;
        }

        // The language multiplies column vectors; Render3D multiplies row vectors, so the matrix is transposed
        double[] elements =
        [
            rows[0][0], rows[1][0], rows[2][0], 0.0,
            rows[0][1], rows[1][1], rows[2][1], 0.0,
            rows[0][2], rows[1][2], rows[2][2], 0.0,
            rows[0][3], rows[1][3], rows[2][3], 1.0,
        ];
        return Apply(instance, results, context, Matrix4x4D.FromRowMajor(elements));
    }

    /// <summary>
    ///     Applies a matrix to the implicit union of the children, enforcing the invariants of a transform.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="results">The results of the children.</param>
    /// <param name="context">The build context.</param>
    /// <param name="matrix">The matrix in the row-vector convention.</param>
    /// <returns>The transformed geometry; nothing when the children are empty or the matrix collapses them.</returns>
    internal static ScadGeo Apply(ScadInstance node, List<ScadGeo> results, ScadGeometryContext context, Matrix4x4D matrix)
    {
        var union = ScadBooleans.Union(node, results, context);
        if (union.Kind == ScadGeoKind.Empty || matrix == Matrix4x4D.Identity)
        {
            return union;
        }

        return union.Kind == ScadGeoKind.Solid
            ? ApplySolid(node, context, union, matrix)
            : ApplyFlat(node, context, union, matrix);
    }

    /// <summary>
    ///     Applies a matrix to a solid.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="solid">The solid.</param>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The transformed solid, or nothing when the matrix is singular or not finite.</returns>
    private static ScadGeo ApplySolid(ScadInstance node, ScadGeometryContext context, ScadGeo solid, Matrix4x4D matrix)
    {
        if (!matrix.IsFinite || IsSingular(Determinant3(matrix), RowNorms(matrix)))
        {
            return Invalid(node, context, "the transformation is singular or not finite and collapses the children");
        }

        var wrapped = new GroupNode([solid.Node!]) { Transform = matrix };
        return ScadGeo.FromSolid(wrapped, solid.Bounds.Transform(matrix), solid.IsTight && IsAxisAligned(matrix), solid.IsApproximate, solid.Reach.Transform(matrix));
    }

    /// <summary>
    ///     Applies the in-plane part of a matrix to a 2D shape.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="flat">The 2D shape.</param>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The transformed shape, or nothing when the in-plane part is singular or not finite.</returns>
    private static ScadGeo ApplyFlat(ScadInstance node, ScadGeometryContext context, ScadGeo flat, Matrix4x4D matrix)
    {
        if (!matrix.IsFinite)
        {
            return Invalid(node, context, "the transformation is not finite");
        }

        // A matrix that moves a 2D shape out of its plane cannot be honored; use the in-plane part and say so
        if (matrix.M13 != 0.0 || matrix.M23 != 0.0 || matrix.M31 != 0.0 || matrix.M32 != 0.0 || matrix.M43 != 0.0)
        {
            context.Reporter.Warn($"{node.Name}: the Z part of the transformation is ignored for 2D objects", node.Location);
        }

        double[] elements =
        [
            matrix.M11, matrix.M12, 0.0, 0.0,
            matrix.M21, matrix.M22, 0.0, 0.0,
            0.0, 0.0, 1.0, 0.0,
            matrix.M41, matrix.M42, 0.0, 1.0,
        ];
        var planar = Matrix4x4D.FromRowMajor(elements);
        var determinant = (matrix.M11 * matrix.M22) - (matrix.M12 * matrix.M21);
        var norms = Math.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)) * Math.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22));
        if (IsSingular(determinant, norms))
        {
            return Invalid(node, context, "the transformation is singular or not finite and collapses the shape");
        }

        return ScadGeo.FromShape(
            ScadShape2D.CreateTransform(flat.Shape!, planar),
            flat.Bounds.Transform(planar),
            flat.IsTight && IsAxisAligned(planar),
            flat.IsApproximate,
            flat.FlatLocation,
            flat.Reach.Transform(planar));
    }

    /// <summary>
    ///     Builds the matrix of <c>rotate(a, v)</c> for a numeric angle.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="degrees">The angle in degrees.</param>
    /// <param name="axis">The axis, or <see langword="null"/> to rotate about Z.</param>
    /// <returns>The rotation; the identity (with a warning) for a zero axis.</returns>
    private static Matrix4x4D RotateScalar(ScadInstance node, ScadGeometryContext context, double degrees, double[]? axis)
    {
        if (axis is null)
        {
            return Matrix4x4D.CreateRotationZ(degrees);
        }

        var direction = new Vector3D(axis[0], axis[1], axis[2]);
        if (!(direction.Length > 0.0) || !double.IsFinite(direction.Length))
        {
            context.Reporter.Warn("rotate: the axis v is zero; the children are not rotated", node.Location);
            return Matrix4x4D.Identity;
        }

        return Matrix4x4D.CreateFromAxisAngle(direction, degrees);
    }

    /// <summary>
    ///     Reports an invalid argument as a warning and returns the empty result.
    /// </summary>
    /// <param name="node">The instance.</param>
    /// <param name="context">The build context.</param>
    /// <param name="problem">What is wrong, without the module name.</param>
    /// <returns><see cref="ScadGeo.Empty"/>.</returns>
    internal static ScadGeo Invalid(ScadInstance node, ScadGeometryContext context, string problem)
    {
        context.Reporter.Warn($"{node.Name}: {problem}; the instance is ignored", node.Location);
        return ScadGeo.Empty;
    }

    /// <summary>
    ///     Reads an optional vector argument of one to three numbers.
    /// </summary>
    /// <param name="args">The bound arguments.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="fill">The value of the components that are not given.</param>
    /// <param name="vector">The three components when the argument is valid.</param>
    /// <returns>Whether the argument was absent, valid or invalid.</returns>
    internal static ReadState ReadVector(ScadArguments args, string name, double fill, out double[] vector)
    {
        vector = [fill, fill, fill];
        if (!args.TryGet(name, out var value))
        {
            return ReadState.Absent;
        }

        return TryReadVector(value, fill, out vector) ? ReadState.Valid : ReadState.Invalid;
    }

    /// <summary>
    ///     Reads a vector of one to three finite numbers, padding the missing components.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="fill">The value of the components that are not given.</param>
    /// <param name="vector">The three components when the value qualifies.</param>
    /// <returns><see langword="true"/> when the value is a vector of one to three finite numbers.</returns>
    internal static bool TryReadVector(ScadValue value, double fill, out double[] vector)
    {
        vector = [fill, fill, fill];
        if (value.Kind != ScadValueKind.Vector || value.AsVector.Count is < 1 or > 3)
        {
            return false;
        }

        for (var i = 0; i < value.AsVector.Count; i++)
        {
            if (!ScadArguments.TryNumber(value.AsVector[i], out vector[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Reads the rows of a matrix argument: three or four rows of four finite numbers.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The rows, or <see langword="null"/> when the value is not such a matrix.</returns>
    private static List<double[]>? ReadMatrixRows(ScadValue value)
    {
        if (value.Kind != ScadValueKind.Vector || value.AsVector.Count is not (3 or 4))
        {
            return null;
        }

        var rows = new List<double[]>(value.AsVector.Count);
        for (var i = 0; i < value.AsVector.Count; i++)
        {
            if (!ScadArguments.TryNumbers(value.AsVector[i], 4, out var row))
            {
                return null;
            }

            rows.Add(row!);
        }

        return rows;
    }

    /// <summary>
    ///     Computes the determinant of the linear (3x3) part of a matrix.
    /// </summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The determinant.</returns>
    private static double Determinant3(Matrix4x4D m) =>
        (m.M11 * ((m.M22 * m.M33) - (m.M23 * m.M32)))
        - (m.M12 * ((m.M21 * m.M33) - (m.M23 * m.M31)))
        + (m.M13 * ((m.M21 * m.M32) - (m.M22 * m.M31)));

    /// <summary>
    ///     Computes the product of the lengths of the rows of the linear part (the largest possible determinant).
    /// </summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The product of the row lengths.</returns>
    private static double RowNorms(Matrix4x4D m) =>
        Math.Sqrt((m.M11 * m.M11) + (m.M12 * m.M12) + (m.M13 * m.M13))
        * Math.Sqrt((m.M21 * m.M21) + (m.M22 * m.M22) + (m.M23 * m.M23))
        * Math.Sqrt((m.M31 * m.M31) + (m.M32 * m.M32) + (m.M33 * m.M33));

    /// <summary>
    ///     Decides whether a determinant is zero, not finite, or negligible next to the product of the row lengths.
    /// </summary>
    /// <param name="determinant">The determinant.</param>
    /// <param name="rowNorms">The product of the row lengths.</param>
    /// <returns><see langword="true"/> when the transform collapses space.</returns>
    private static bool IsSingular(double determinant, double rowNorms)
    {
        if (!double.IsFinite(determinant) || determinant == 0.0)
        {
            return true;
        }

        return double.IsFinite(rowNorms) && Math.Abs(determinant) <= SingularTolerance * rowNorms;
    }

    /// <summary>
    ///     Decides whether a matrix maps boxes to boxes exactly: its linear part has exactly one non-zero entry
    ///     in every row and every column (a scaled permutation, as translate, scale, mirror and rotations by
    ///     multiples of 90 degrees produce).
    /// </summary>
    /// <param name="m">The matrix.</param>
    /// <returns><see langword="true"/> when the transformed box is exactly the box of the transformed geometry.</returns>
    internal static bool IsAxisAligned(Matrix4x4D m)
    {
        double[,] a =
        {
            { m.M11, m.M12, m.M13 },
            { m.M21, m.M22, m.M23 },
            { m.M31, m.M32, m.M33 },
        };
        for (var i = 0; i < 3; i++)
        {
            var inRow = 0;
            var inColumn = 0;
            for (var j = 0; j < 3; j++)
            {
                inRow += a[i, j] != 0.0 ? 1 : 0;
                inColumn += a[j, i] != 0.0 ? 1 : 0;
            }

            if (inRow != 1 || inColumn != 1)
            {
                return false;
            }
        }

        return true;
    }
}
