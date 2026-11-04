using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;

namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     The viewport decisions made before any evaluation: which values the document sees in <c>$vpt</c>,
///     <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c>, which of them an explicit option fixes, and what the caller asked for.
/// </summary>
/// <remarks>
///     The plan is pure data derived from <see cref="ScadRenderOptions"/> (see <see cref="ScadViewport.Plan"/>); it
///     does not depend on the document, so it can be built before the evaluation starts.
/// </remarks>
internal sealed class ScadViewportPlan
{
    /// <summary>Gets the values the document sees when it does not assign the variables.</summary>
    public ScadViewportValues Defaults { get; init; } = ScadViewportValues.OpenScadDefaults;

    /// <summary>Gets the variables fixed by an explicit option; they replace an override and an assignment of the same name.</summary>
    public IReadOnlyDictionary<string, ScadValue> ExplicitOverrides { get; init; } = new Dictionary<string, ScadValue>();

    /// <summary>Gets the explicit camera, or <see langword="null"/>.</summary>
    public Camera? Camera { get; init; }

    /// <summary>Gets a value indicating whether the view angles were set explicitly.</summary>
    public bool AnglesExplicit { get; init; }

    /// <summary>Gets a value indicating whether the projection was set explicitly.</summary>
    public bool ProjectionExplicit { get; init; }

    /// <summary>Gets the requested (or default) azimuth in degrees.</summary>
    public double Azimuth { get; init; } = Camera.DefaultAzimuthDegrees;

    /// <summary>Gets the requested (or default) elevation in degrees.</summary>
    public double Elevation { get; init; } = Camera.DefaultElevationDegrees;

    /// <summary>Gets the requested (or default) projection.</summary>
    public CameraProjection Projection { get; init; } = CameraProjection.Perspective;
}

/// <summary>
///     The viewport variables <c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c>: the mapping between the
///     renderer's camera and the variables, and the choice of the camera of a render.
/// </summary>
/// <remarks>
///     <para>
///         <b>Orbit mapping.</b> The orbit view of <see cref="Camera.FitToBounds"/> (azimuth <c>az</c>, elevation
///         <c>el</c>) corresponds to <c>$vpr = [90 - el, 0, az]</c> with every angle wrapped into 0 to 360: the camera
///         sits at <c>Rz(az) * Rx(90 - el) * (0, 0, d)</c> above the target. In general <c>$vpr</c> is the rotation
///         that takes the camera's own axes (right = +X, up = +Y, toward the viewer = +Z) into the world, composed
///         like <c>rotate($vpr)</c>: <c>R = Rz(c) * Ry(b) * Rx(a)</c> for <c>$vpr = [a, b, c]</c>. A model that
///         places an object with <c>translate($vpt) rotate($vpr)</c> therefore lies in the plane of the screen.
///     </para>
///     <para>
///         <b>Precedence</b>, highest first and per variable: an explicit camera or explicit view angles in
///         <see cref="ScadRenderOptions"/>; an override from <see cref="ScadEvaluationOptions.Variables"/>; the
///         top-level assignment in the document; the default (<c>$vpt = [0, 0, 0]</c>, <c>$vpd = 140</c>,
///         <c>$vpf = 22.5</c>, and <c>$vpr</c> from the requested or default view). The first two are injected as
///         variable overrides, the explicit option replacing an override of the same name.
///     </para>
///     <para>
///         <b>Circularity.</b> The camera is chosen after the document was built, from the bounds of the built
///         scene, so the document can only see the defaults of the variables it does not assign; in particular a model
///         cannot see that it turned out to be entirely 2D (the document sees the default <c>$vpr</c>), and the
///         framing of the automatic fit includes whatever geometry the variables placed.
///     </para>
/// </remarks>
internal static class ScadViewport
{
    /// <summary>The fractional margin of the automatic fit (5 %).</summary>
    private const double FitMargin = 0.05;

    /// <summary>The tolerance in degrees below which an angle counts as zero.</summary>
    private const double AngleTolerance = 1e-9;

    /// <summary>
    ///     Plans the viewport of a document for the given render options.
    /// </summary>
    /// <param name="options">The render options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The plan.</returns>
    public static ScadViewportPlan Plan(ScadRenderOptions? options)
    {
        var effective = options ?? new ScadRenderOptions();
        var azimuth = effective.ViewAzimuthDegrees;
        var elevation = effective.ViewElevationDegrees;
        var defaults = FromOrbit(azimuth, elevation);
        var overrides = new Dictionary<string, ScadValue>(StringComparer.Ordinal);
        if (effective.Camera is { } camera)
        {
            var values = FromCamera(camera);
            overrides[ScadViewportValues.TargetName] = values.TargetValue();
            overrides[ScadViewportValues.RotationName] = values.RotationValue();
            overrides[ScadViewportValues.DistanceName] = ScadValue.FromNumber(values.Distance);
            overrides[ScadViewportValues.FieldOfViewName] = ScadValue.FromNumber(values.FieldOfView);
            defaults = values;
        }
        else if (effective.IsViewExplicit)
        {
            overrides[ScadViewportValues.RotationName] = defaults.RotationValue();
        }

        return new ScadViewportPlan
        {
            Defaults = defaults,
            ExplicitOverrides = overrides,
            Camera = effective.Camera,
            AnglesExplicit = effective.IsViewExplicit,
            ProjectionExplicit = effective.IsProjectionExplicit,
            Azimuth = azimuth,
            Elevation = elevation,
            Projection = effective.Projection,
        };
    }

    /// <summary>
    ///     Gets the plan of an evaluation that has no render options: the literal defaults of a fresh viewer.
    /// </summary>
    /// <returns>The plan.</returns>
    public static ScadViewportPlan ForEvaluation() => new();

    /// <summary>
    ///     Merges the overrides the caller supplied with the ones an explicit option fixes (the option wins).
    /// </summary>
    /// <param name="user">The overrides of the evaluation options.</param>
    /// <param name="plan">The viewport plan.</param>
    /// <returns>The merged overrides (the input itself when the plan adds none).</returns>
    public static IReadOnlyDictionary<string, ScadValue> MergeOverrides(IReadOnlyDictionary<string, ScadValue> user, ScadViewportPlan plan)
    {
        if (plan.ExplicitOverrides.Count == 0)
        {
            return user;
        }

        var merged = new Dictionary<string, ScadValue>(user, StringComparer.Ordinal);
        foreach (var (name, value) in plan.ExplicitOverrides)
        {
            merged[name] = value;
        }

        return merged;
    }

    /// <summary>
    ///     Gets the viewport values of an orbit view.
    /// </summary>
    /// <param name="azimuthDegrees">The azimuth.</param>
    /// <param name="elevationDegrees">The elevation.</param>
    /// <returns>The values: target at the origin, <c>$vpr = [90 - el, 0, az]</c>, default distance and field of view.</returns>
    public static ScadViewportValues FromOrbit(double azimuthDegrees, double elevationDegrees) => new(
        Vector3D.Zero,
        new Vector3D(Wrap(90.0 - elevationDegrees), 0.0, Wrap(azimuthDegrees)),
        ScadViewportValues.DefaultDistance,
        ScadViewportValues.DefaultFieldOfView);

    /// <summary>
    ///     Gets the viewport values that describe a camera.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <returns>The values: its target, the rotation of its axes, the distance of its eye and its field of view.</returns>
    public static ScadViewportValues FromCamera(Camera camera)
    {
        var offset = camera.Eye - camera.Target;
        var forward = (camera.Target - camera.Eye).Normalize();
        var (right, up) = Axes(forward, camera.Up);
        var back = -forward;

        // The camera-to-world rotation has the camera axes as columns; decompose it as Rz(c) * Ry(b) * Rx(a)
        var r20 = right.Z;
        var b = Math.Asin(Math.Clamp(-r20, -1.0, 1.0));
        double a;
        double c;
        if (Math.Abs(Math.Cos(b)) < 1e-9)
        {
            // Gimbal lock: only the sum or difference of the other two angles matters; put it all into a
            c = 0.0;
            a = Math.Atan2(-back.Y, up.Y);
        }
        else
        {
            a = Math.Atan2(up.Z, back.Z);
            c = Math.Atan2(right.Y, right.X);
        }

        return new ScadViewportValues(
            camera.Target,
            new Vector3D(Wrap(RadiansToDegrees(a)), Wrap(RadiansToDegrees(b)), Wrap(RadiansToDegrees(c))),
            offset.Length,
            camera.FieldOfViewDegrees);
    }

    /// <summary>
    ///     Gets the rotation that takes the camera axes into the world for a <c>$vpr</c> (composed like <c>rotate()</c>).
    /// </summary>
    /// <param name="rotation">The <c>$vpr</c> value in degrees.</param>
    /// <returns>The matrix (row-vector convention); <c>TransformDirection</c> maps a camera-space direction into the world.</returns>
    public static Matrix4x4D CameraToWorld(Vector3D rotation) =>
        Matrix4x4D.CreateRotationX(rotation.X) * Matrix4x4D.CreateRotationY(rotation.Y) * Matrix4x4D.CreateRotationZ(rotation.Z);

    /// <summary>
    ///     Builds the camera that the viewport variables describe.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="projection">The projection.</param>
    /// <returns>The camera: the eye at <c>$vpd</c> from <c>$vpt</c> along the rotated +Z axis.</returns>
    public static Camera ToCamera(ScadViewportValues values, CameraProjection projection)
    {
        var rotation = CameraToWorld(values.Rotation);
        var toViewer = rotation.TransformDirection(Vector3D.UnitZ);
        var up = rotation.TransformDirection(Vector3D.UnitY);
        return new Camera
        {
            Eye = values.Target + (toViewer * values.Distance),
            Target = values.Target,
            Up = up,
            Projection = projection,
            FieldOfViewDegrees = values.FieldOfView,
            OrthographicHeight = projection == CameraProjection.Orthographic
                ? 2.0 * values.Distance * Math.Tan(values.FieldOfView * Math.PI / 360.0)
                : null,
        };
    }

    /// <summary>
    ///     Chooses the camera of a render and reports the view it describes.
    /// </summary>
    /// <remarks>
    ///     The explicit camera is used as given. Otherwise, when the document or an override defined
    ///     <c>$vpt</c>, <c>$vpd</c> or <c>$vpf</c>, the camera is the manual one those variables describe (the others
    ///     keep their defaults, <c>$vpr</c> falls back to the requested view, or to a top view for a flat model).
    ///     Otherwise the view is fitted to the model from the explicit view angles, the defined <c>$vpr</c>, the flat
    ///     top view of a model that is entirely 2D, or the requested view, in that order.
    /// </remarks>
    /// <param name="plan">The plan.</param>
    /// <param name="document">The viewport variables defined by the document or an override.</param>
    /// <param name="isFlatModel">Whether the model is entirely 2D.</param>
    /// <param name="scene">The built scene (its bounds are fitted).</param>
    /// <param name="aspectRatio">The width divided by the height.</param>
    /// <param name="view">The view that was chosen.</param>
    /// <returns>The camera; never throws for a scene the build returned.</returns>
    public static Camera ChooseCamera(
        ScadViewportPlan plan,
        ScadDocumentViewport document,
        bool isFlatModel,
        Scene scene,
        double aspectRatio,
        out ScadResolvedView view)
    {
        if (plan.Camera is { } explicitCamera)
        {
            var (az, el) = Orbit(explicitCamera.Eye - explicitCamera.Target);
            view = new ScadResolvedView(az, el, explicitCamera.Projection, isFlatModel, ScadViewSource.Camera);
            return explicitCamera;
        }

        var bounds = SafeBounds(scene);

        // Which orientation is in force: the explicit angles, then the document's $vpr, then the flat top, then the default
        var flatDefault = isFlatModel && !plan.AnglesExplicit && document.Rotation is null;
        var projection = flatDefault && !plan.ProjectionExplicit ? CameraProjection.Orthographic : plan.Projection;
        var source = SourceOf(plan, document, flatDefault);
        if (document.IsManual)
        {
            var defaults = plan.Defaults;
            var rotation = document.Rotation ?? (flatDefault ? Vector3D.Zero : defaults.Rotation);
            var values = new ScadViewportValues(
                document.Target ?? defaults.Target,
                rotation,
                document.Distance ?? defaults.Distance,
                document.FieldOfView ?? defaults.FieldOfView);
            var manual = ToCamera(values, projection);
            var (maz, mel) = Orbit(manual.Eye - manual.Target);
            view = new ScadResolvedView(maz, mel, projection, isFlatModel, source);
            return manual;
        }

        double azimuth;
        double elevation;
        if (plan.AnglesExplicit)
        {
            (azimuth, elevation) = (plan.Azimuth, plan.Elevation);
        }
        else if (document.Rotation is { } vpr)
        {
            if (Math.Abs(vpr.Y) < AngleTolerance && vpr.X >= 0.0 && vpr.X <= 180.0)
            {
                (azimuth, elevation) = (vpr.Z, 90.0 - vpr.X);
            }
            else
            {
                var rotated = FitRotated(bounds, CameraToWorld(vpr), aspectRatio, projection);
                var (raz, rel) = Orbit(rotated.Eye - rotated.Target);
                view = new ScadResolvedView(raz, rel, projection, isFlatModel, source);
                return rotated;
            }
        }
        else if (flatDefault)
        {
            (azimuth, elevation) = (0.0, 90.0);
        }
        else
        {
            (azimuth, elevation) = (plan.Azimuth, plan.Elevation);
        }

        view = new ScadResolvedView(azimuth, elevation, projection, isFlatModel, source);
        return FitOrbit(bounds, azimuth, elevation, aspectRatio, projection);
    }

    /// <summary>
    ///     Tells where the view of a render came from when no explicit camera was given.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <param name="document">The viewport variables defined by the document or an override.</param>
    /// <param name="flatDefault">Whether the flat top view of a model that is entirely 2D is in force.</param>
    /// <returns>The source.</returns>
    private static ScadViewSource SourceOf(ScadViewportPlan plan, ScadDocumentViewport document, bool flatDefault)
    {
        if (plan.AnglesExplicit)
        {
            return ScadViewSource.Options;
        }

        if (document.Rotation is not null || document.IsManual)
        {
            return ScadViewSource.Document;
        }

        return flatDefault ? ScadViewSource.FlatDefault : ScadViewSource.Default;
    }

    /// <summary>
    ///     Wraps an angle into 0 (inclusive) to 360 (exclusive), snapping values within a billionth of a degree of a
    ///     full turn to zero so that rounding noise does not show up as 359.999999999.
    /// </summary>
    /// <param name="degrees">The angle.</param>
    /// <returns>The wrapped angle; never negative zero.</returns>
    public static double Wrap(double degrees)
    {
        var wrapped = degrees % 360.0;
        if (wrapped < 0.0)
        {
            wrapped += 360.0;
        }

        return wrapped < AngleTolerance || wrapped > 360.0 - AngleTolerance ? 0.0 : wrapped;
    }

    /// <summary>
    ///     Fits an orbit view to the scene; it never throws for a scene the build returned.
    /// </summary>
    /// <remarks>
    ///     The build keeps every coordinate finite and within <see cref="ScadGeo.MaxCoordinate"/>, so the fit
    ///     normally succeeds. Should the bounds still be unusable (not finite or out of range) or the fit be
    ///     rejected, the camera frames the unit cube around the origin instead of failing the render.
    /// </remarks>
    /// <param name="scene">The scene.</param>
    /// <param name="azimuth">The azimuth in degrees.</param>
    /// <param name="elevation">The elevation in degrees.</param>
    /// <param name="aspectRatio">The width divided by the height.</param>
    /// <param name="projection">The projection.</param>
    /// <returns>The camera.</returns>
    internal static Camera FitScene(Scene scene, double azimuth, double elevation, double aspectRatio, CameraProjection projection) =>
        FitOrbit(SafeBounds(scene), azimuth, elevation, aspectRatio, projection);

    /// <summary>
    ///     Gets the bounds of a scene, or an empty box when they are not usable (not finite or out of range).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The bounds.</returns>
    private static BoundingBox3D SafeBounds(Scene scene)
    {
        var bounds = scene.ComputeBounds();
        return ScadGeo.IsSafe(bounds) ? bounds : BoundingBox3D.Empty;
    }

    /// <summary>
    ///     Fits an orbit view to the bounds, framing the unit cube instead should the bounds be unusable.
    /// </summary>
    /// <param name="bounds">The scene bounds (empty when unusable).</param>
    /// <param name="azimuth">The azimuth.</param>
    /// <param name="elevation">The elevation.</param>
    /// <param name="aspectRatio">The aspect ratio.</param>
    /// <param name="projection">The projection.</param>
    /// <returns>The camera.</returns>
    private static Camera FitOrbit(BoundingBox3D bounds, double azimuth, double elevation, double aspectRatio, CameraProjection projection)
    {
        try
        {
            return Camera.FitToBounds(bounds, azimuth, elevation, aspectRatio, projection);
        }
        catch (ArgumentException)
        {
            return Camera.FitToBounds(BoundingBox3D.Empty, azimuth, elevation, aspectRatio, projection);
        }
    }

    /// <summary>
    ///     Fits a view with an arbitrary orientation (including a roll) to the bounds, with the same framing rule as
    ///     <see cref="Camera.FitToBounds"/>: the box is inflated by the fit margin and the eye distance (or the
    ///     orthographic height) is chosen so that the most demanding corner touches the frame.
    /// </summary>
    /// <param name="bounds">The scene bounds (empty when unusable).</param>
    /// <param name="rotation">The camera-to-world rotation.</param>
    /// <param name="aspectRatio">The aspect ratio.</param>
    /// <param name="projection">The projection.</param>
    /// <returns>The camera.</returns>
    private static Camera FitRotated(BoundingBox3D bounds, Matrix4x4D rotation, double aspectRatio, CameraProjection projection)
    {
        var center = bounds.IsEmpty ? Vector3D.Zero : bounds.Center;
        var half = bounds.IsEmpty || bounds.Diagonal <= 0.0 || !double.IsFinite(bounds.Diagonal)
            ? new Vector3D(0.5, 0.5, 0.5)
            : bounds.Size * 0.5;
        half *= 1.0 + FitMargin;

        var toViewer = rotation.TransformDirection(Vector3D.UnitZ).Normalize();
        var right = rotation.TransformDirection(Vector3D.UnitX).Normalize();
        var up = rotation.TransformDirection(Vector3D.UnitY).Normalize();
        var tanV = Math.Tan(Camera.DefaultFieldOfViewDegrees * Math.PI / 360.0);
        var tanH = tanV * aspectRatio;
        var maxX = 0.0;
        var maxY = 0.0;
        var maxZ = double.NegativeInfinity;
        var distance = 0.0;
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3D(
                (i & 1) == 0 ? -half.X : half.X,
                (i & 2) == 0 ? -half.Y : half.Y,
                (i & 4) == 0 ? -half.Z : half.Z);
            var x = Math.Abs(Vector3D.Dot(corner, right));
            var y = Math.Abs(Vector3D.Dot(corner, up));
            var z = Vector3D.Dot(corner, toViewer);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);
            distance = Math.Max(distance, Math.Max(z + (x / tanH), z + (y / tanV)));
        }

        var radius = half.Length;
        if (projection == CameraProjection.Perspective)
        {
            distance = Math.Max(distance, maxZ + (0.1 * radius));
            return new Camera
            {
                Eye = center + (toViewer * distance),
                Target = center,
                Up = up,
                Projection = CameraProjection.Perspective,
            };
        }

        return new Camera
        {
            Eye = center + (toViewer * (2.0 * radius)),
            Target = center,
            Up = up,
            Projection = CameraProjection.Orthographic,
            OrthographicHeight = 2.0 * Math.Max(maxY, maxX / aspectRatio),
        };
    }

    /// <summary>
    ///     Computes the right and the true up axes of a camera, substituting an alternate up hint when the view
    ///     direction is parallel to the hint (the same rule the renderer applies).
    /// </summary>
    /// <param name="forward">The unit view direction.</param>
    /// <param name="upHint">The camera's up vector.</param>
    /// <returns>The right and the true up unit vectors in world space.</returns>
    private static (Vector3D Right, Vector3D Up) Axes(Vector3D forward, Vector3D upHint)
    {
        var hint = upHint;
        var unitUp = hint.Normalize();
        if (Vector3D.Cross(forward, unitUp).Length <= 1e-9)
        {
            var alternate = Vector3D.Dot(forward, unitUp) < 0.0 ? Vector3D.UnitY : -Vector3D.UnitY;
            hint = Vector3D.Cross(forward, alternate).Length > 1e-9 ? alternate : Vector3D.UnitZ;
        }

        var right = Vector3D.Cross(forward, hint).Normalize();
        var up = Vector3D.Cross(right, forward).Normalize();
        return (right, up);
    }

    /// <summary>
    ///     Gets the orbit azimuth and elevation of a direction from the target to the eye.
    /// </summary>
    /// <param name="toEye">The direction (not necessarily unit length; not zero).</param>
    /// <returns>The azimuth in degrees (0 when looking straight down or up) and the elevation in degrees.</returns>
    private static (double Azimuth, double Elevation) Orbit(Vector3D toEye)
    {
        var direction = toEye.Normalize();
        var elevation = RadiansToDegrees(Math.Asin(Math.Clamp(direction.Z, -1.0, 1.0)));
        var azimuth = Math.Abs(direction.X) < 1e-12 && Math.Abs(direction.Y) < 1e-12
            ? 0.0
            : RadiansToDegrees(Math.Atan2(direction.X, -direction.Y));
        return (azimuth, elevation);
    }

    /// <summary>
    ///     Converts radians to degrees.
    /// </summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The angle in degrees.</returns>
    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}
