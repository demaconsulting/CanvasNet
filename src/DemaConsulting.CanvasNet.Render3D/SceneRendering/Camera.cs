using DemaConsulting.CanvasNet.Render3D.SceneModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     An immutable camera: eye, target, up vector and projection parameters.
/// </summary>
/// <remarks>
///     <para>
///         <b>Conventions.</b> World space is right-handed with +Z up. View space is right-handed
///         with the camera looking down -Z, +X to the right and +Y up. Screen pixels have x to the
///         right, y downward and pixel centres at +0.5.
///     </para>
///     <para>
///         <b>Orbit convention</b> (used by <see cref="FitToBounds"/>): azimuth 0 and elevation 0
///         place the eye on the -Y axis looking toward +Y (a front view); positive elevation raises
///         the eye toward +Z; positive azimuth moves the eye counter-clockwise as seen from +Z
///         (from -Y toward +X): <c>eyeDirection = (sin(az) cos(el), -cos(az) cos(el), sin(el))</c>.
///     </para>
///     <para>
///         All properties are <see langword="init"/>-only and validated; a camera never changes
///         after creation and is thread-safe. Cross-property rules (eye differs from target, an
///         orthographic camera has a height, near is below far) are checked when the camera is
///         rendered with.
///     </para>
/// </remarks>
public sealed class Camera
{
    /// <summary>The default vertical field of view in degrees (a modest, CAD-style telephoto).</summary>
    public const double DefaultFieldOfViewDegrees = 22.5;

    /// <summary>The default azimuth of the fitted view in degrees (true isometric).</summary>
    public const double DefaultAzimuthDegrees = 45.0;

    /// <summary>The default elevation of the fitted view in degrees (true isometric, <c>atan(1/sqrt(2))</c>).</summary>
    public static readonly double DefaultElevationDegrees = Math.Atan(1.0 / Math.Sqrt(2.0)) * 180.0 / Math.PI;

    /// <summary>Tolerance on the sine of the angle between forward and up below which they count as parallel.</summary>
    private const double ParallelTolerance = 1e-9;

    /// <summary>Backing field for <see cref="Eye"/>.</summary>
    private readonly Vector3D _eye = new(0.0, -1.0, 0.0);

    /// <summary>Backing field for <see cref="Target"/>.</summary>
    private readonly Vector3D _target = Vector3D.Zero;

    /// <summary>Backing field for <see cref="Up"/>.</summary>
    private readonly Vector3D _up = Vector3D.UnitZ;

    /// <summary>Backing field for <see cref="FieldOfViewDegrees"/>.</summary>
    private readonly double _fieldOfViewDegrees = DefaultFieldOfViewDegrees;

    /// <summary>Backing field for <see cref="OrthographicHeight"/>.</summary>
    private readonly double? _orthographicHeight;

    /// <summary>Backing field for <see cref="NearPlane"/>.</summary>
    private readonly double? _nearPlane;

    /// <summary>Backing field for <see cref="FarPlane"/>.</summary>
    private readonly double? _farPlane;

    /// <summary>
    ///     Gets the eye (camera) position in world space; must be finite. Defaults to <c>(0, -1, 0)</c>
    ///     so that a default camera is valid; set <see cref="Eye"/> and <see cref="Target"/> (or use
    ///     <see cref="LookAt"/>) for a meaningful view.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown (from the <see langword="init"/> accessor) when a component is not finite.</exception>
    public Vector3D Eye
    {
        get => _eye;
        init => _eye = RequireFinite(value, nameof(Eye));
    }

    /// <summary>Gets the world-space point the camera looks at; must be finite. Defaults to the origin.</summary>
    /// <exception cref="ArgumentException">Thrown (from the <see langword="init"/> accessor) when a component is not finite.</exception>
    public Vector3D Target
    {
        get => _target;
        init => _target = RequireFinite(value, nameof(Target));
    }

    /// <summary>
    ///     Gets the world-space up direction (need not be unit length or perpendicular to the view
    ///     direction); must be finite. Defaults to +Z. When it is parallel to the view direction an
    ///     alternate up (+Y when looking down, -Y when looking up) is substituted automatically.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown (from the <see langword="init"/> accessor) when a component is not finite.</exception>
    public Vector3D Up
    {
        get => _up;
        init => _up = RequireFinite(value, nameof(Up));
    }

    /// <summary>Gets the projection type. Defaults to <see cref="CameraProjection.Perspective"/>.</summary>
    public CameraProjection Projection { get; init; } = CameraProjection.Perspective;

    /// <summary>
    ///     Gets the vertical field of view in degrees for a perspective camera, strictly between 0
    ///     and 180 (exclusive). Defaults to <see cref="DefaultFieldOfViewDegrees"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when the value is not in (0, 180).</exception>
    public double FieldOfViewDegrees
    {
        get => _fieldOfViewDegrees;
        init
        {
            if (!(value > 0.0 && value < 180.0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The field of view must be between 0 and 180 degrees (exclusive).");
            }

            _fieldOfViewDegrees = value;
        }
    }

    /// <summary>
    ///     Gets the number of world units visible vertically for an orthographic camera. Required
    ///     (greater than zero) when <see cref="Projection"/> is <see cref="CameraProjection.Orthographic"/>;
    ///     ignored for perspective cameras.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when a supplied value is not finite and greater than zero.</exception>
    public double? OrthographicHeight
    {
        get => _orthographicHeight;
        init => _orthographicHeight = RequirePositive(value, nameof(value));
    }

    /// <summary>
    ///     Gets the near clipping distance along the view direction; <see langword="null"/> (default)
    ///     lets the renderer choose it from the scene's depth range. Must be greater than zero when set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when a supplied value is not finite and greater than zero.</exception>
    public double? NearPlane
    {
        get => _nearPlane;
        init => _nearPlane = RequirePositive(value, nameof(value));
    }

    /// <summary>
    ///     Gets the far distance beyond which fragments are discarded; <see langword="null"/>
    ///     (default) lets the renderer choose it from the scene's depth range. Must be greater than
    ///     zero (and than <see cref="NearPlane"/>) when set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown (from the <see langword="init"/> accessor) when a supplied value is not finite and greater than zero.</exception>
    public double? FarPlane
    {
        get => _farPlane;
        init => _farPlane = RequirePositive(value, nameof(value));
    }

    /// <summary>
    ///     Creates a perspective camera at <paramref name="eye"/> looking at <paramref name="target"/>.
    /// </summary>
    /// <remarks>
    ///     When the view direction is parallel to <paramref name="up"/> (for example looking straight
    ///     down with +Z up) the returned camera carries a substituted up vector (+Y when looking
    ///     down, -Y when looking up) instead of the request throwing, so top and bottom views just work.
    /// </remarks>
    /// <param name="eye">The camera position; finite.</param>
    /// <param name="target">The point looked at; finite and different from <paramref name="eye"/>.</param>
    /// <param name="up">The up hint; finite and non-zero.</param>
    /// <returns>The camera (customize further with <see langword="with"/>-style object initializers on a new instance).</returns>
    /// <exception cref="ArgumentException">Thrown when an argument is not finite, the eye equals the target or the up vector is zero.</exception>
    public static Camera LookAt(Vector3D eye, Vector3D target, Vector3D up)
    {
        // Validate the three inputs: finite, a real view direction and a real up hint
        if (!eye.IsFinite || !target.IsFinite || !up.IsFinite)
        {
            throw new ArgumentException("The eye, target and up vectors must be finite.");
        }

        if ((target - eye).LengthSquared <= 0.0)
        {
            throw new ArgumentException("The eye and the target must be different points.", nameof(target));
        }

        if (up.LengthSquared <= 0.0)
        {
            throw new ArgumentException("The up vector must be non-zero.", nameof(up));
        }

        // Substitute an alternate up now so the stored camera is already well defined
        var forward = (target - eye).Normalize();
        return new Camera { Eye = eye, Target = target, Up = ResolveUp(forward, up) };
    }

    /// <summary>
    ///     Creates a camera orbiting the centre of <paramref name="bounds"/> that frames the
    ///     whole box tightly (plus a margin) for an output of the given aspect ratio.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The box is inflated about its centre by <c>1 + marginFraction</c>; the eye distance
    ///         (perspective) or orthographic height is then chosen so that the most demanding of the
    ///         eight inflated corners just touches the frame edge, using the final view basis, so
    ///         the framing is exact rather than bounding-sphere conservative. Empty or zero-size
    ///         bounds fall back to a unit cube around the bounds centre (the origin when empty).
    ///         For perspective views the eye is additionally kept outside the inflated box.
    ///     </para>
    ///     <para>
    ///         Elevation of exactly +/-90 degrees is allowed (the degenerate up vector is
    ///         substituted). See the type remarks for the azimuth/elevation convention.
    ///     </para>
    /// </remarks>
    /// <param name="bounds">The world-space bounds to frame.</param>
    /// <param name="azimuthDegrees">The orbit azimuth in degrees (0 = front view from -Y; finite).</param>
    /// <param name="elevationDegrees">The orbit elevation in degrees (positive raises the eye; finite).</param>
    /// <param name="aspectRatio">The output width divided by height; finite and greater than zero.</param>
    /// <param name="projection">The projection to use.</param>
    /// <param name="fieldOfViewDegrees">The vertical field of view (perspective) in degrees, in (0, 180).</param>
    /// <param name="marginFraction">The fractional margin added around the bounds; finite and not negative (0.05 = 5 %).</param>
    /// <returns>The framing camera; its <see cref="Target"/> is the bounds centre.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an angle is not finite, the aspect ratio is not positive, the field of view is outside (0, 180), the margin is negative or <paramref name="projection"/> is not a defined <see cref="CameraProjection"/> value.</exception>
    public static Camera FitToBounds(
        BoundingBox3D bounds,
        double azimuthDegrees,
        double elevationDegrees,
        double aspectRatio,
        CameraProjection projection = CameraProjection.Perspective,
        double fieldOfViewDegrees = DefaultFieldOfViewDegrees,
        double marginFraction = 0.05)
    {
        ValidateFitArguments(azimuthDegrees, elevationDegrees, aspectRatio, projection, fieldOfViewDegrees, marginFraction);

        // Resolve a usable centre and half extents, falling back to a unit cube for empty or degenerate bounds
        var centre = bounds.IsEmpty ? Vector3D.Zero : bounds.Center;
        var half = bounds.IsEmpty || bounds.Diagonal <= 0.0 || !double.IsFinite(bounds.Diagonal)
            ? new Vector3D(0.5, 0.5, 0.5)
            : bounds.Size * 0.5;
        half *= 1.0 + marginFraction;

        // Orbit direction (centre -> eye) and the resulting view basis
        var az = azimuthDegrees * Math.PI / 180.0;
        var el = elevationDegrees * Math.PI / 180.0;
        var eyeDirection = new Vector3D(Math.Sin(az) * Math.Cos(el), -Math.Cos(az) * Math.Cos(el), Math.Sin(el)).Normalize();
        var (right, up, _) = ComputeBasis(-eyeDirection, Vector3D.UnitZ);

        // Project the eight inflated corners into the view basis: lateral (x, y) and depth toward the camera (z)
        var maxX = 0.0;
        var maxY = 0.0;
        var maxZ = double.NegativeInfinity;
        var tanV = Math.Tan(fieldOfViewDegrees * Math.PI / 360.0);
        var tanH = tanV * aspectRatio;
        var distance = 0.0;
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3D(
                (i & 1) == 0 ? -half.X : half.X,
                (i & 2) == 0 ? -half.Y : half.Y,
                (i & 4) == 0 ? -half.Z : half.Z);
            var x = Math.Abs(Vector3D.Dot(corner, right));
            var y = Math.Abs(Vector3D.Dot(corner, up));
            var z = Vector3D.Dot(corner, eyeDirection);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);

            // A corner at depth offset z is visible when |x| <= (distance - z) * tanH (and likewise for y)
            distance = Math.Max(distance, Math.Max(z + (x / tanH), z + (y / tanV)));
        }

        var radius = half.Length;
        if (projection == CameraProjection.Perspective)
        {
            // Keep the eye outside the inflated box even for extreme fields of view
            distance = Math.Max(distance, maxZ + (0.1 * radius));
            return new Camera
            {
                Eye = centre + (eyeDirection * distance),
                Target = centre,
                Up = Vector3D.UnitZ,
                Projection = CameraProjection.Perspective,
                FieldOfViewDegrees = fieldOfViewDegrees,
            };
        }

        // Orthographic: the visible height must cover the tallest corner and the widest corner (width = height * aspect)
        return new Camera
        {
            Eye = centre + (eyeDirection * (2.0 * radius)),
            Target = centre,
            Up = Vector3D.UnitZ,
            Projection = CameraProjection.Orthographic,
            FieldOfViewDegrees = fieldOfViewDegrees,
            OrthographicHeight = 2.0 * Math.Max(maxY, maxX / aspectRatio),
        };
    }

    /// <summary>
    ///     Builds the world-to-view matrix in the row-vector convention: it maps the eye to the
    ///     origin, the target to <c>(0, 0, -distance)</c> and the (resolved) up direction to +Y.
    /// </summary>
    /// <remarks>
    ///     The matrix is the translation by <c>-Eye</c> followed by the view rotation, evaluated in
    ///     double precision. The renderer applies the same two steps separately per mesh instance so
    ///     the (large) world translation is subtracted before any rotation, keeping precision
    ///     relative to the view distance.
    /// </remarks>
    /// <returns>The view matrix.</returns>
    /// <exception cref="ArgumentException">Thrown when the eye equals the target or the up vector is zero.</exception>
    public Matrix4x4D GetViewMatrix() =>
        Matrix4x4D.CreateTranslation(-Eye) * CreateViewRotation();

    /// <summary>
    ///     Checks the cross-property rules that cannot be enforced by the individual initializers.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when the eye equals the target, the up vector is zero, the projection is not a
    ///     defined <see cref="CameraProjection"/> value, an orthographic camera has no height, or
    ///     the near plane is not below the far plane.
    /// </exception>
    internal void Validate()
    {
        RequireViewDirection();

        // An undefined enum value would render as perspective but be treated as orthographic elsewhere
        if (!Enum.IsDefined(Projection))
        {
            throw new ArgumentException($"The camera projection {(int)Projection} is not a defined CameraProjection value.");
        }

        if (Projection == CameraProjection.Orthographic && OrthographicHeight is null)
        {
            throw new ArgumentException("An orthographic camera requires OrthographicHeight.");
        }

        if (NearPlane is { } near && FarPlane is { } far && !(near < far))
        {
            throw new ArgumentException("The camera near plane must be closer than the far plane.");
        }
    }

    /// <summary>
    ///     Computes the orthonormal view basis (right, up, forward) of this camera, resolving a
    ///     degenerate up vector.
    /// </summary>
    /// <returns>The right, up and forward unit vectors in world space.</returns>
    internal (Vector3D Right, Vector3D Up, Vector3D Forward) GetBasis()
    {
        RequireViewDirection();
        return ComputeBasis((Target - Eye).Normalize(), Up);
    }

    /// <summary>Ensures the eye differs from the target and the up vector is non-zero.</summary>
    /// <exception cref="ArgumentException">Thrown when the eye equals the target or the up vector is zero.</exception>
    private void RequireViewDirection()
    {
        if ((Target - Eye).LengthSquared <= 0.0)
        {
            throw new ArgumentException("The camera eye and target must be different points.");
        }

        if (Up.LengthSquared <= 0.0)
        {
            throw new ArgumentException("The camera up vector must be non-zero.");
        }
    }

    /// <summary>
    ///     Builds the pure rotation part of the view matrix (columns are the right, up and back axes).
    /// </summary>
    /// <returns>The rotation matrix in row-vector convention.</returns>
    internal Matrix4x4D CreateViewRotation()
    {
        var (right, up, forward) = GetBasis();
        var back = -forward;
        ReadOnlySpan<double> m =
        [
            right.X, up.X, back.X, 0.0,
            right.Y, up.Y, back.Y, 0.0,
            right.Z, up.Z, back.Z, 0.0,
            0.0, 0.0, 0.0, 1.0,
        ];
        return Matrix4x4D.FromRowMajor(m);
    }

    /// <summary>Computes the orthonormal basis for a forward direction and an up hint.</summary>
    /// <param name="forward">The unit view direction.</param>
    /// <param name="upHint">The up hint (non-zero).</param>
    /// <returns>The right, true up and forward unit vectors.</returns>
    private static (Vector3D Right, Vector3D Up, Vector3D Forward) ComputeBasis(Vector3D forward, Vector3D upHint)
    {
        var up = ResolveUp(forward, upHint);
        var right = Vector3D.Cross(forward, up).Normalize();
        var trueUp = Vector3D.Cross(right, forward).Normalize();
        return (right, trueUp, forward);
    }

    /// <summary>
    ///     Returns <paramref name="up"/> unless it is parallel to <paramref name="forward"/>, in
    ///     which case an alternate is chosen: +Y when looking down, -Y when looking up (and +Z if
    ///     the view direction is itself along Y).
    /// </summary>
    /// <param name="forward">The unit view direction.</param>
    /// <param name="up">The requested up hint.</param>
    /// <returns>A usable up hint that is not parallel to <paramref name="forward"/>.</returns>
    private static Vector3D ResolveUp(Vector3D forward, Vector3D up)
    {
        var unitUp = up.Normalize();
        if (Vector3D.Cross(forward, unitUp).Length > ParallelTolerance)
        {
            return up;
        }

        // Looking along the up direction: pick +Y for a downward view and -Y for an upward one
        var alternate = Vector3D.Dot(forward, unitUp) < 0.0 ? Vector3D.UnitY : -Vector3D.UnitY;
        if (Vector3D.Cross(forward, alternate).Length > ParallelTolerance)
        {
            return alternate;
        }

        return Vector3D.UnitZ;
    }

    /// <summary>Validates the numeric arguments of <see cref="FitToBounds"/>.</summary>
    /// <param name="azimuthDegrees">The azimuth.</param>
    /// <param name="elevationDegrees">The elevation.</param>
    /// <param name="aspectRatio">The aspect ratio.</param>
    /// <param name="projection">The projection.</param>
    /// <param name="fieldOfViewDegrees">The field of view.</param>
    /// <param name="marginFraction">The margin.</param>
    private static void ValidateFitArguments(
        double azimuthDegrees,
        double elevationDegrees,
        double aspectRatio,
        CameraProjection projection,
        double fieldOfViewDegrees,
        double marginFraction)
    {
        if (!double.IsFinite(azimuthDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(azimuthDegrees), azimuthDegrees, "The azimuth must be finite.");
        }

        if (!double.IsFinite(elevationDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(elevationDegrees), elevationDegrees, "The elevation must be finite.");
        }

        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), aspectRatio, "The aspect ratio must be finite and greater than zero.");
        }

        if (!Enum.IsDefined(projection))
        {
            throw new ArgumentOutOfRangeException(nameof(projection), projection, "The projection must be a defined CameraProjection value.");
        }

        if (!(fieldOfViewDegrees > 0.0 && fieldOfViewDegrees < 180.0))
        {
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees), fieldOfViewDegrees, "The field of view must be between 0 and 180 degrees (exclusive).");
        }

        if (!double.IsFinite(marginFraction) || marginFraction < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(marginFraction), marginFraction, "The margin must be finite and not negative.");
        }
    }

    /// <summary>Rejects vectors that are not finite.</summary>
    /// <param name="value">The vector.</param>
    /// <param name="paramName">The property name for the exception.</param>
    /// <returns>The same vector.</returns>
    private static Vector3D RequireFinite(Vector3D value, string paramName)
    {
        if (!value.IsFinite)
        {
            throw new ArgumentException("The vector must be finite.", paramName);
        }

        return value;
    }

    /// <summary>Rejects optional values that are not finite and greater than zero.</summary>
    /// <param name="value">The value.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>The same value.</returns>
    private static double? RequirePositive(double? value, string paramName)
    {
        if (value is { } v && (!double.IsFinite(v) || v <= 0.0))
        {
            throw new ArgumentOutOfRangeException(paramName, v, "The value must be finite and greater than zero.");
        }

        return value;
    }
}
