namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An immutable two-dimensional vector (or point) with double-precision components.
/// </summary>
/// <remarks>
///     Used for 2D profile data (polygons, extrusion cross-sections) and screen-space helper math
///     where single precision would lose accuracy. As a <see langword="readonly"/>
///     <see langword="record struct"/> it is value-comparable and allocation-free. All members are
///     pure and thread-safe.
/// </remarks>
/// <param name="X">The horizontal component.</param>
/// <param name="Y">The vertical component.</param>
public readonly record struct Vector2D(double X, double Y)
{
    /// <summary>Gets the zero vector <c>(0, 0)</c>.</summary>
    public static Vector2D Zero => new(0.0, 0.0);

    /// <summary>Gets the unit vector along the X axis, <c>(1, 0)</c>.</summary>
    public static Vector2D UnitX => new(1.0, 0.0);

    /// <summary>Gets the unit vector along the Y axis, <c>(0, 1)</c>.</summary>
    public static Vector2D UnitY => new(0.0, 1.0);

    /// <summary>Gets the squared Euclidean length (cheaper than <see cref="Length"/>; no square root).</summary>
    public double LengthSquared => (X * X) + (Y * Y);

    /// <summary>Gets the Euclidean length of the vector.</summary>
    /// <remarks>
    ///     The length is computed by scaling with the largest absolute component first, so a finite
    ///     vector never overflows (for example <c>(1e200, 0)</c> has length <c>1e200</c>) or
    ///     becomes zero (for example <c>(1e-200, 0)</c> has length <c>1e-200</c>) merely because
    ///     its components are squared. A zero vector has length zero; a NaN component yields NaN
    ///     and an infinite component yields infinity, as for <see cref="double"/>.
    /// </remarks>
    public double Length
    {
        get
        {
            // The largest absolute component is the scale; NaN propagates through Math.Max
            var scale = MaxAbsComponent();
            if (scale == 0.0 || !double.IsFinite(scale))
            {
                return scale;
            }

            return scale * ScaledLength(scale);
        }
    }

    /// <summary>Adds two vectors component-wise.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The component-wise sum.</returns>
    public static Vector2D operator +(Vector2D left, Vector2D right) => new(left.X + right.X, left.Y + right.Y);

    /// <summary>Subtracts <paramref name="right"/> from <paramref name="left"/> component-wise.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The component-wise difference.</returns>
    public static Vector2D operator -(Vector2D left, Vector2D right) => new(left.X - right.X, left.Y - right.Y);

    /// <summary>Negates both components.</summary>
    /// <param name="value">The vector to negate.</param>
    /// <returns>The negated vector.</returns>
    public static Vector2D operator -(Vector2D value) => new(-value.X, -value.Y);

    /// <summary>Multiplies a vector by a scalar.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="scalar">The scalar factor.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector2D operator *(Vector2D vector, double scalar) => new(vector.X * scalar, vector.Y * scalar);

    /// <summary>Multiplies a vector by a scalar.</summary>
    /// <param name="scalar">The scalar factor.</param>
    /// <param name="vector">The vector.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector2D operator *(double scalar, Vector2D vector) => vector * scalar;

    /// <summary>Divides a vector by a scalar.</summary>
    /// <param name="vector">The vector.</param>
    /// <param name="scalar">The scalar divisor (division by zero yields infinities, as for <see cref="double"/>).</param>
    /// <returns>The divided vector.</returns>
    public static Vector2D operator /(Vector2D vector, double scalar) => new(vector.X / scalar, vector.Y / scalar);

    /// <summary>Computes the dot product of two vectors.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>left.X * right.X + left.Y * right.Y</c>.</returns>
    public static double Dot(Vector2D left, Vector2D right) => (left.X * right.X) + (left.Y * right.Y);

    /// <summary>
    ///     Computes the scalar 2D cross product (the Z component of the 3D cross product of the
    ///     two vectors embedded at Z = 0). It is positive when <paramref name="right"/> lies
    ///     counter-clockwise from <paramref name="left"/>.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>left.X * right.Y - left.Y * right.X</c>.</returns>
    public static double Cross(Vector2D left, Vector2D right) => (left.X * right.Y) - (left.Y * right.X);

    /// <summary>
    ///     Returns the unit-length vector pointing in the same direction. A zero-length vector
    ///     normalizes to <see cref="Zero"/> rather than producing NaN components, so degenerate
    ///     input never poisons later arithmetic.
    /// </summary>
    /// <remarks>
    ///     The components are divided by the largest absolute component before the length is
    ///     taken, so a finite non-zero vector of any magnitude (including <c>(1e200, 0)</c> and
    ///     <c>(1e-200, 0)</c>) normalizes to a true unit vector instead of overflowing or
    ///     collapsing to <see cref="Zero"/>.
    /// </remarks>
    /// <returns>The normalized vector, or <see cref="Zero"/> when the length is zero.</returns>
    public Vector2D Normalize()
    {
        // A zero (or NaN) length has no direction; return Zero instead of dividing by 0
        var scale = MaxAbsComponent();
        if (!(scale > 0.0))
        {
            return Zero;
        }

        // An infinite component cannot be scaled meaningfully; keep the plain division behavior
        if (double.IsInfinity(scale))
        {
            return this / Length;
        }

        // Scale so the largest component has magnitude 1, then divide by the (safe) scaled length
        var scaled = this / scale;
        return scaled / Math.Sqrt(scaled.LengthSquared);
    }

    /// <summary>Gets a value indicating whether both components are finite (neither NaN nor infinite).</summary>
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

    /// <summary>Returns the largest absolute component value (NaN if any component is NaN).</summary>
    /// <returns>The maximum of <c>|X|</c> and <c>|Y|</c>.</returns>
    private double MaxAbsComponent() => Math.Max(Math.Abs(X), Math.Abs(Y));

    /// <summary>
    ///     Computes the length of this vector divided by <paramref name="scale"/>, which is safe
    ///     from overflow and underflow because every scaled component has magnitude at most one.
    /// </summary>
    /// <param name="scale">The largest absolute component; finite and greater than zero.</param>
    /// <returns>The length of the scaled vector (between 1 and the square root of 2).</returns>
    private double ScaledLength(double scale)
    {
        var x = X / scale;
        var y = Y / scale;
        return Math.Sqrt((x * x) + (y * y));
    }
}
