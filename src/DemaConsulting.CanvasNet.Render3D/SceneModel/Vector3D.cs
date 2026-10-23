using System.Numerics;

namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An immutable three-dimensional vector (or point) with double-precision components.
/// </summary>
/// <remarks>
///     This is the model-space point/direction type of the 3D renderer. Double precision is used
///     because <see cref="System.Numerics.Vector3"/> is single-precision only and loses accuracy
///     for large-offset models; narrowing happens explicitly through <see cref="ToVector3"/>,
///     after the renderer has made coordinates camera-relative. The coordinate system is
///     right-handed with +Z up. All members are pure and thread-safe.
/// </remarks>
/// <param name="X">The X component.</param>
/// <param name="Y">The Y component.</param>
/// <param name="Z">The Z component.</param>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    /// <summary>Gets the zero vector <c>(0, 0, 0)</c>.</summary>
    public static Vector3D Zero => new(0.0, 0.0, 0.0);

    /// <summary>Gets the vector <c>(1, 1, 1)</c>.</summary>
    public static Vector3D One => new(1.0, 1.0, 1.0);

    /// <summary>Gets the unit vector along the X axis, <c>(1, 0, 0)</c>.</summary>
    public static Vector3D UnitX => new(1.0, 0.0, 0.0);

    /// <summary>Gets the unit vector along the Y axis, <c>(0, 1, 0)</c>.</summary>
    public static Vector3D UnitY => new(0.0, 1.0, 0.0);

    /// <summary>Gets the unit vector along the Z axis, <c>(0, 0, 1)</c>.</summary>
    public static Vector3D UnitZ => new(0.0, 0.0, 1.0);

    /// <summary>Gets the squared Euclidean length (cheaper than <see cref="Length"/>; no square root).</summary>
    public double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    /// <summary>Gets the Euclidean length of the vector.</summary>
    public double Length => Math.Sqrt(LengthSquared);

    /// <summary>Gets a value indicating whether all components are finite (none is NaN or infinite).</summary>
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    /// <summary>Adds two vectors component-wise.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The component-wise sum.</returns>
    public static Vector3D operator +(Vector3D left, Vector3D right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    /// <summary>Subtracts <paramref name="right"/> from <paramref name="left"/> component-wise.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The component-wise difference.</returns>
    public static Vector3D operator -(Vector3D left, Vector3D right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    /// <summary>Negates all components.</summary>
    /// <param name="value">The vector to negate.</param>
    /// <returns>The negated vector.</returns>
    public static Vector3D operator -(Vector3D value) => new(-value.X, -value.Y, -value.Z);

    /// <summary>Multiplies a vector by a scalar.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="scalar">The scalar factor.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector3D operator *(Vector3D vector, double scalar) =>
        new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    /// <summary>Multiplies a vector by a scalar.</summary>
    /// <param name="scalar">The scalar factor.</param>
    /// <param name="vector">The vector.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector3D operator *(double scalar, Vector3D vector) => vector * scalar;

    /// <summary>Divides a vector by a scalar.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="scalar">The scalar divisor (division by zero yields infinities, as for <see cref="double"/>).</param>
    /// <returns>The divided vector.</returns>
    public static Vector3D operator /(Vector3D vector, double scalar) =>
        new(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);

    /// <summary>Computes the dot product of two vectors.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The scalar dot product.</returns>
    public static double Dot(Vector3D left, Vector3D right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    /// <summary>
    ///     Computes the right-handed cross product; the result follows the right-hand rule, so
    ///     <c>Cross(UnitX, UnitY)</c> is <see cref="UnitZ"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The vector perpendicular to both operands.</returns>
    public static Vector3D Cross(Vector3D left, Vector3D right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));

    /// <summary>Returns the component-wise minimum of two vectors.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A vector whose components are the smaller of each pair.</returns>
    public static Vector3D Min(Vector3D left, Vector3D right) =>
        new(Math.Min(left.X, right.X), Math.Min(left.Y, right.Y), Math.Min(left.Z, right.Z));

    /// <summary>Returns the component-wise maximum of two vectors.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>A vector whose components are the larger of each pair.</returns>
    public static Vector3D Max(Vector3D left, Vector3D right) =>
        new(Math.Max(left.X, right.X), Math.Max(left.Y, right.Y), Math.Max(left.Z, right.Z));

    /// <summary>
    ///     Returns the unit-length vector pointing in the same direction. A zero-length vector
    ///     normalizes to <see cref="Zero"/> rather than producing NaN components, so degenerate
    ///     input (for example a zero-area triangle normal) never poisons later arithmetic.
    /// </summary>
    /// <returns>The normalized vector, or <see cref="Zero"/> when the length is zero.</returns>
    public Vector3D Normalize()
    {
        // A zero (or underflowing) length has no direction; return Zero instead of dividing by 0
        var length = Length;
        return length > 0.0 ? this / length : Zero;
    }

    /// <summary>
    ///     Explicitly narrows the vector to single precision. Callers should make coordinates
    ///     camera-relative (small magnitude) in double precision first, so that the float rounding
    ///     error is relative to the view distance rather than to the world position.
    /// </summary>
    /// <returns>The nearest single-precision <see cref="System.Numerics.Vector3"/>.</returns>
    public Vector3 ToVector3() => new((float)X, (float)Y, (float)Z);
}
