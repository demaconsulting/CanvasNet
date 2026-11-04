using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Unit tests of <see cref="ScadViewport"/>: the mapping between the renderer's camera and the viewport variables
///     <c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c>, the plan that decides what the document sees, and the
///     choice of the camera of a render.
/// </summary>
public class ScadViewportTests
{
    /// <summary>The tolerance of the numeric comparisons.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The default elevation in degrees.</summary>
    private static readonly double IsoElevation = Camera.DefaultElevationDegrees;

    /// <summary>
    ///     Validates that angles wrap into 0 to 360 and that rounding noise around a full turn snaps to zero.
    /// </summary>
    /// <param name="degrees">The angle.</param>
    /// <param name="expected">The wrapped angle.</param>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(-10.0, 350.0)]
    [InlineData(360.0, 0.0)]
    [InlineData(720.5, 0.5)]
    [InlineData(-1e-12, 0.0)]
    [InlineData(359.9999999999, 0.0)]
    [InlineData(-360.0, 0.0)]
    public void ScadViewport_Wrap_GivesTheAngleInZeroToThreeSixty(double degrees, double expected)
    {
        // Arrange / Act
        var wrapped = ScadViewport.Wrap(degrees);

        // Assert
        Assert.Equal(expected, wrapped, Tolerance);
        Assert.False(double.IsNegative(wrapped));
    }

    /// <summary>
    ///     Validates the mapping of the orbit view to <c>$vpr = [90 - elevation, 0, azimuth]</c> for the named views.
    /// </summary>
    /// <param name="azimuth">The azimuth.</param>
    /// <param name="elevation">The elevation.</param>
    /// <param name="x">The expected first component of <c>$vpr</c>.</param>
    /// <param name="z">The expected third component of <c>$vpr</c>.</param>
    [Theory]
    [InlineData(0.0, 0.0, 90.0, 0.0)]
    [InlineData(90.0, 0.0, 90.0, 90.0)]
    [InlineData(180.0, 0.0, 90.0, 180.0)]
    [InlineData(0.0, 90.0, 0.0, 0.0)]
    [InlineData(0.0, -90.0, 180.0, 0.0)]
    [InlineData(-45.0, 30.0, 60.0, 315.0)]
    [InlineData(45.0, 0.0, 90.0, 45.0)]
    public void ScadViewport_FromOrbit_MapsAzimuthAndElevationToViewportRotation(double azimuth, double elevation, double x, double z)
    {
        // Arrange / Act
        var values = ScadViewport.FromOrbit(azimuth, elevation);

        // Assert: rotation about X is 90 - elevation, never about Y, about Z the azimuth; the rest are the defaults
        Assert.Equal(x, values.Rotation.X, Tolerance);
        Assert.Equal(0.0, values.Rotation.Y);
        Assert.Equal(z, values.Rotation.Z, Tolerance);
        Assert.Equal(Vector3D.Zero, values.Target);
        Assert.Equal(140.0, values.Distance);
        Assert.Equal(22.5, values.FieldOfView);
    }

    /// <summary>
    ///     Validates that the library default view is <c>$vpr = [54.7356, 0, 45]</c> and that the viewer's own defaults are
    ///     the documented <c>[55, 0, 25]</c>.
    /// </summary>
    [Fact]
    public void ScadViewport_Defaults_AreTheIsometricViewAndTheViewerDefaults()
    {
        // Arrange / Act
        var library = ScadViewport.Plan(null).Defaults;
        var evaluation = ScadViewport.ForEvaluation().Defaults;

        // Assert
        Assert.Equal(90.0 - IsoElevation, library.Rotation.X, Tolerance);
        Assert.Equal(45.0, library.Rotation.Z, Tolerance);
        Assert.Equal(new Vector3D(55, 0, 25), evaluation.Rotation);
        Assert.Equal(new Vector3D(0, 0, 0), evaluation.Target);
        Assert.Equal(140.0, evaluation.Distance);
        Assert.Equal(22.5, evaluation.FieldOfView);
    }

    /// <summary>
    ///     Validates that the cameras the renderer fits for many orbit views describe the same rotation as the orbit
    ///     mapping, including the straight-down and straight-up views (where the angles are not unique).
    /// </summary>
    /// <param name="azimuth">The azimuth.</param>
    /// <param name="elevation">The elevation.</param>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(90.0, 0.0)]
    [InlineData(45.0, 35.264389682754654)]
    [InlineData(30.0, 20.0)]
    [InlineData(0.0, 90.0)]
    [InlineData(0.0, -90.0)]
    [InlineData(200.0, -30.0)]
    [InlineData(-45.0, 60.0)]
    public void ScadViewport_FromCamera_OfAnOrbitView_DescribesTheSameRotationAsTheOrbitMapping(double azimuth, double elevation)
    {
        // Arrange
        var camera = Camera.FitToBounds(new BoundingBox3D(new Vector3D(-3, -2, -1), new Vector3D(5, 6, 7)), azimuth, elevation, 1.5, CameraProjection.Perspective);

        // Act
        var fromCamera = ScadViewport.FromCamera(camera);
        var fromOrbit = ScadViewport.FromOrbit(azimuth, elevation);

        // Assert: the same rotation of the camera axes (the angles themselves are not unique at the poles)
        AssertSameRotation(fromOrbit.Rotation, fromCamera.Rotation);
        Assert.Equal(camera.Target, fromCamera.Target);
        Assert.Equal((camera.Eye - camera.Target).Length, fromCamera.Distance, 1e-9);
        Assert.Equal(camera.FieldOfViewDegrees, fromCamera.FieldOfView);
    }

    /// <summary>
    ///     Validates the round trip of a general camera: with a roll, and with the gimbal-lock orientation, the camera
    ///     rebuilt from the variables has the same eye, the same target and the same up direction.
    /// </summary>
    /// <param name="upX">The X of the up hint.</param>
    /// <param name="upY">The Y of the up hint.</param>
    /// <param name="upZ">The Z of the up hint.</param>
    /// <param name="horizontal">Whether the camera looks horizontally from (0, -5, 0) at the origin, otherwise it looks down at (1, 2, 3).</param>
    [Theory]
    [InlineData(0.3, 0.2, 1.0, false)]
    [InlineData(1.0, 0.0, 0.0, true)]
    [InlineData(-1.0, 0.0, 0.0, true)]
    [InlineData(0.0, 0.0, 1.0, false)]
    [InlineData(0.0, 1.0, 0.2, false)]
    public void ScadViewport_ToCamera_RoundTripsAGeneralCamera(double upX, double upY, double upZ, bool horizontal)
    {
        // Arrange
        var eye = horizontal ? new Vector3D(0, -5, 0) : new Vector3D(10, -20, 15);
        var camera = Camera.LookAt(eye, horizontal ? Vector3D.Zero : new Vector3D(1, 2, 3), new Vector3D(upX, upY, upZ));

        // Act
        var values = ScadViewport.FromCamera(camera);
        var rebuilt = ScadViewport.ToCamera(values, CameraProjection.Perspective);

        // Assert: the eye and the target, and the true up (the component of the up hint across the view direction)
        AssertVector(camera.Eye, rebuilt.Eye);
        AssertVector(camera.Target, rebuilt.Target);
        var forward = (camera.Target - camera.Eye).Normalize();
        var expectedUp = (camera.Up - (forward * Vector3D.Dot(camera.Up, forward))).Normalize();
        AssertVector(expectedUp, rebuilt.Up.Normalize());
    }

    /// <summary>
    ///     Validates the camera that <c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c> describe: the eye at the distance
    ///     from the target along the rotated +Z axis, and the orthographic height from the distance and the field of view.
    /// </summary>
    [Fact]
    public void ScadViewport_ToCamera_PlacesTheEyeAlongTheRotatedZAxis()
    {
        // Arrange: rotation [90, 0, 0] turns +Z into -Y and +Y into +Z
        var values = new ScadViewportValues(new Vector3D(1, 2, 3), new Vector3D(90, 0, 0), 200.0, 30.0);

        // Act
        var perspective = ScadViewport.ToCamera(values, CameraProjection.Perspective);
        var orthographic = ScadViewport.ToCamera(values, CameraProjection.Orthographic);

        // Assert
        AssertVector(new Vector3D(1, -198, 3), perspective.Eye);
        AssertVector(new Vector3D(1, 2, 3), perspective.Target);
        AssertVector(Vector3D.UnitZ, perspective.Up);
        Assert.Equal(30.0, perspective.FieldOfViewDegrees);
        Assert.Null(perspective.OrthographicHeight);
        Assert.Equal(2.0 * 200.0 * Math.Tan(15.0 * Math.PI / 180.0), orthographic.OrthographicHeight!.Value, 1e-9);
    }

    /// <summary>
    ///     Validates that rotating a point with <c>rotate($vpr)</c> faces the camera: the rotated +Z axis points to the
    ///     viewer and the rotated +Y axis is the up direction of the picture, for the orbit views.
    /// </summary>
    /// <param name="azimuth">The azimuth.</param>
    /// <param name="elevation">The elevation.</param>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(45.0, 35.264389682754654)]
    [InlineData(30.0, 20.0)]
    [InlineData(300.0, -50.0)]
    public void ScadViewport_CameraToWorld_ToViewerAxisPointsFromTheTargetToTheEye(double azimuth, double elevation)
    {
        // Arrange
        var values = ScadViewport.FromOrbit(azimuth, elevation);
        var az = azimuth * Math.PI / 180.0;
        var el = elevation * Math.PI / 180.0;
        var toEye = new Vector3D(Math.Sin(az) * Math.Cos(el), -Math.Cos(az) * Math.Cos(el), Math.Sin(el));

        // Act
        var rotation = ScadViewport.CameraToWorld(values.Rotation);

        // Assert: +Z of the camera is the direction to the eye, and +Y of the camera is up across it
        AssertVector(toEye, rotation.TransformDirection(Vector3D.UnitZ));
        var up = rotation.TransformDirection(Vector3D.UnitY);
        Assert.Equal(0.0, Vector3D.Dot(up, toEye), Tolerance);
        Assert.True(up.Z >= -Tolerance);
    }

    /// <summary>
    ///     Validates the plan of default options: the library default view, nothing explicit, nothing overridden.
    /// </summary>
    [Fact]
    public void ScadViewport_Plan_DefaultOptions_HasNothingExplicit()
    {
        // Arrange / Act
        var plan = ScadViewport.Plan(new ScadRenderOptions());

        // Assert
        Assert.Empty(plan.ExplicitOverrides);
        Assert.False(plan.AnglesExplicit);
        Assert.False(plan.ProjectionExplicit);
        Assert.Null(plan.Camera);
        Assert.Equal(CameraProjection.Perspective, plan.Projection);
    }

    /// <summary>
    ///     Validates that explicit view angles, even equal to the defaults, become an override of <c>$vpr</c> only, and an
    ///     explicit projection only changes the projection.
    /// </summary>
    [Fact]
    public void ScadViewport_Plan_ExplicitAnglesAndProjection_AreRecorded()
    {
        // Arrange / Act
        var angles = ScadViewport.Plan(new ScadRenderOptions { ViewAzimuthDegrees = Camera.DefaultAzimuthDegrees });
        var elevation = ScadViewport.Plan(new ScadRenderOptions { ViewElevationDegrees = 20, ViewAzimuthDegrees = 30 });
        var projection = ScadViewport.Plan(new ScadRenderOptions { Projection = CameraProjection.Perspective });

        // Assert
        Assert.True(angles.AnglesExplicit);
        Assert.Equal(["$vpr"], angles.ExplicitOverrides.Keys.ToArray());
        Assert.Equal(70.0, elevation.Defaults.Rotation.X, Tolerance);
        Assert.Equal(30.0, elevation.Defaults.Rotation.Z, Tolerance);
        Assert.False(projection.AnglesExplicit);
        Assert.True(projection.ProjectionExplicit);
        Assert.Empty(projection.ExplicitOverrides);
    }

    /// <summary>
    ///     Validates that an explicit camera supplies all four variables, as an override that also becomes the default.
    /// </summary>
    [Fact]
    public void ScadViewport_Plan_ExplicitCamera_SuppliesAllFourVariables()
    {
        // Arrange
        var camera = Camera.LookAt(new Vector3D(0, -50, 0), new Vector3D(1, 2, 3), Vector3D.UnitZ);

        // Act
        var plan = ScadViewport.Plan(new ScadRenderOptions { Camera = camera });

        // Assert
        Assert.Equal(["$vpd", "$vpf", "$vpr", "$vpt"], plan.ExplicitOverrides.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new Vector3D(1, 2, 3), plan.Defaults.Target);
        Assert.Same(camera, plan.Camera);
    }

    /// <summary>
    ///     Validates that an override from the plan replaces one of the same name from the caller and that the others stay.
    /// </summary>
    [Fact]
    public void ScadViewport_MergeOverrides_TheOptionWinsOverTheCallerOverride()
    {
        // Arrange
        var user = new Dictionary<string, ScadValue> { ["$vpr"] = ScadValue.FromNumber(1), ["width"] = ScadValue.FromNumber(5) };
        var plan = ScadViewport.Plan(new ScadRenderOptions { ViewAzimuthDegrees = 10 });
        var none = ScadViewport.Plan(new ScadRenderOptions());

        // Act
        var merged = ScadViewport.MergeOverrides(user, plan);
        var untouched = ScadViewport.MergeOverrides(user, none);

        // Assert
        Assert.Equal(ScadValueKind.Vector, merged["$vpr"].Kind);
        Assert.Equal(5.0, merged["width"].AsNumber);
        Assert.Same(user, untouched);
    }

    /// <summary>
    ///     Validates that an explicit camera is used as given, whatever the document says.
    /// </summary>
    [Fact]
    public void ScadViewport_ChooseCamera_ExplicitCamera_IsUsedAsGiven()
    {
        // Arrange
        var camera = Camera.LookAt(new Vector3D(0, -50, 0), Vector3D.Zero, Vector3D.UnitZ);
        var plan = ScadViewport.Plan(new ScadRenderOptions { Camera = camera });
        var document = new ScadDocumentViewport(null, new Vector3D(1, 2, 3), 9.0, null);

        // Act
        var chosen = ScadViewport.ChooseCamera(plan, document, false, ThreeD(), 1.0, out var view);

        // Assert
        Assert.Same(camera, chosen);
        Assert.Equal(ScadViewSource.Camera, view.Source);
        Assert.Equal(0.0, view.AzimuthDegrees, Tolerance);
        Assert.Equal(0.0, view.ElevationDegrees, Tolerance);
    }

    /// <summary>
    ///     Validates that without anything chosen a model with 3D objects is fitted isometrically and a model that is
    ///     entirely 2D from the top, orthographically unless the projection was set.
    /// </summary>
    [Fact]
    public void ScadViewport_ChooseCamera_DefaultAndFlat_FitTheModel()
    {
        // Arrange
        var plan = ScadViewport.Plan(new ScadRenderOptions());
        var perspectivePlan = ScadViewport.Plan(new ScadRenderOptions { Projection = CameraProjection.Perspective });
        var scene = ThreeD();
        var flat = Flat();

        // Act
        var solid = ScadViewport.ChooseCamera(plan, ScadDocumentViewport.None, false, scene, 1.0, out var solidView);
        var drawing = ScadViewport.ChooseCamera(plan, ScadDocumentViewport.None, true, flat, 1.0, out var drawingView);
        var explicitPerspective = ScadViewport.ChooseCamera(perspectivePlan, ScadDocumentViewport.None, true, flat, 1.0, out var perspectiveView);

        // Assert
        AssertCamera(Camera.FitToBounds(scene.ComputeBounds(), 45.0, IsoElevation, 1.0, CameraProjection.Perspective), solid);
        Assert.Equal(ScadViewSource.Default, solidView.Source);
        Assert.False(solidView.IsFlatModel);
        AssertCamera(Camera.FitToBounds(flat.ComputeBounds(), 0.0, 90.0, 1.0, CameraProjection.Orthographic), drawing);
        Assert.Equal(new ScadResolvedView(0.0, 90.0, CameraProjection.Orthographic, true, ScadViewSource.FlatDefault), drawingView);
        AssertCamera(Camera.FitToBounds(flat.ComputeBounds(), 0.0, 90.0, 1.0, CameraProjection.Perspective), explicitPerspective);
        Assert.Equal(CameraProjection.Perspective, perspectiveView.Projection);
        Assert.Equal(ScadViewSource.FlatDefault, perspectiveView.Source);
    }

    /// <summary>
    ///     Validates that explicit view angles win over the flat view and over the document's <c>$vpr</c>.
    /// </summary>
    [Fact]
    public void ScadViewport_ChooseCamera_ExplicitAngles_WinOverTheFlatViewAndTheDocument()
    {
        // Arrange
        var plan = ScadViewport.Plan(new ScadRenderOptions { ViewAzimuthDegrees = 30, ViewElevationDegrees = 20 });
        var flat = Flat();
        var document = new ScadDocumentViewport(null, new Vector3D(10, 0, 10), null, null);

        // Act
        var camera = ScadViewport.ChooseCamera(plan, document, true, flat, 1.0, out var view);

        // Assert
        AssertCamera(Camera.FitToBounds(flat.ComputeBounds(), 30.0, 20.0, 1.0, CameraProjection.Perspective), camera);
        Assert.Equal(ScadViewSource.Options, view.Source);
        Assert.Equal(30.0, view.AzimuthDegrees);
        Assert.Equal(20.0, view.ElevationDegrees);
    }

    /// <summary>
    ///     Validates that a <c>$vpr</c> that the document defined steers the fitted view, also for a flat model, and that a
    ///     rotation that is not an orbit (a roll) is honored with its own up direction.
    /// </summary>
    [Fact]
    public void ScadViewport_ChooseCamera_DocumentRotation_SteersTheFit()
    {
        // Arrange
        var plan = ScadViewport.Plan(new ScadRenderOptions());
        var flat = Flat();
        var orbit = new ScadDocumentViewport(null, new Vector3D(90, 0, 0), null, null);
        var rolled = new ScadDocumentViewport(null, new Vector3D(60, 20, 10), null, null);

        // Act
        var front = ScadViewport.ChooseCamera(plan, orbit, true, flat, 1.0, out var frontView);
        var camera = ScadViewport.ChooseCamera(plan, rolled, false, ThreeD(), 1.0, out var rolledView);

        // Assert: the front view is the orbit fit, the roll gives the axes of the rotation
        AssertCamera(Camera.FitToBounds(flat.ComputeBounds(), 10.0 * 0.0, 0.0, 1.0, CameraProjection.Perspective), front);
        Assert.Equal(ScadViewSource.Document, frontView.Source);
        var rotation = ScadViewport.CameraToWorld(new Vector3D(60, 20, 10));
        AssertVector(rotation.TransformDirection(Vector3D.UnitZ), (camera.Eye - camera.Target).Normalize());
        AssertVector(rotation.TransformDirection(Vector3D.UnitY), camera.Up.Normalize());
        Assert.Equal(ScadViewSource.Document, rolledView.Source);
    }

    /// <summary>
    ///     Validates the manual camera: assigning <c>$vpt</c>, <c>$vpd</c> or <c>$vpf</c> switches the framing off and the
    ///     eye is placed from the variables, with the defaults for the ones that were not assigned.
    /// </summary>
    [Fact]
    public void ScadViewport_ChooseCamera_AssignedTargetDistanceOrFieldOfView_GiveAManualCamera()
    {
        // Arrange
        var plan = ScadViewport.Plan(new ScadRenderOptions { Projection = CameraProjection.Orthographic });
        var document = new ScadDocumentViewport(new Vector3D(1, 2, 3), new Vector3D(90, 0, 0), 200.0, 30.0);
        var onlyDistance = new ScadDocumentViewport(null, null, 50.0, null);

        // Act
        var manual = ScadViewport.ChooseCamera(plan, document, false, ThreeD(), 1.0, out var view);
        var partial = ScadViewport.ChooseCamera(plan, onlyDistance, false, ThreeD(), 1.0, out var partialView);

        // Assert: eye 200 in front of the target, an orthographic height from the distance and the field of view
        AssertVector(new Vector3D(1, -198, 3), manual.Eye);
        AssertVector(new Vector3D(1, 2, 3), manual.Target);
        Assert.Equal(CameraProjection.Orthographic, manual.Projection);
        Assert.Equal(2.0 * 200.0 * Math.Tan(15.0 * Math.PI / 180.0), manual.OrthographicHeight!.Value, 1e-9);
        Assert.Equal(ScadViewSource.Document, view.Source);
        Assert.Equal(0.0, view.AzimuthDegrees, Tolerance);
        Assert.Equal(0.0, view.ElevationDegrees, Tolerance);

        // The target keeps its default, so the camera looks at the origin from 50 units, along the default view direction
        AssertVector(Vector3D.Zero, partial.Target);
        Assert.Equal(50.0, (partial.Eye - partial.Target).Length, 1e-9);
        Assert.Equal(ScadViewSource.Document, partialView.Source);
    }

    /// <summary>
    ///     Validates that a flat model with a manual camera and no <c>$vpr</c> looks straight down.
    /// </summary>
    [Fact]
    public void ScadViewport_ChooseCamera_ManualCameraOnAFlatModel_LooksDown()
    {
        // Arrange
        var plan = ScadViewport.Plan(new ScadRenderOptions());
        var document = new ScadDocumentViewport(null, null, 80.0, null);

        // Act
        var camera = ScadViewport.ChooseCamera(plan, document, true, Flat(), 1.0, out var view);

        // Assert: the eye is 80 above the origin and the view is a top view
        AssertVector(new Vector3D(0, 0, 80), camera.Eye);
        Assert.Equal(90.0, view.ElevationDegrees, Tolerance);
        Assert.Equal(CameraProjection.Orthographic, view.Projection);
    }

    /// <summary>
    ///     Validates the circularity of the automatic framing: a document that assigns only <c>$vpr</c> keeps the fitted
    ///     camera, framed on the center of the scene.
    /// </summary>
    [Fact]
    public void ScadViewport_ChooseCamera_OnlyRotationAssigned_StaysFitted()
    {
        // Arrange
        var plan = ScadViewport.Plan(new ScadRenderOptions());
        var scene = ThreeD();
        var document = new ScadDocumentViewport(null, new Vector3D(70, 0, 20), null, null);

        // Act
        var camera = ScadViewport.ChooseCamera(plan, document, false, scene, 1.0, out _);

        // Assert: it looks at the center of the bounds, not at the default $vpt
        AssertVector(scene.ComputeBounds().Center, camera.Target);
        Assert.NotEqual(Vector3D.Zero, camera.Target);
    }

    /// <summary>
    ///     Builds a scene with a box away from the origin.
    /// </summary>
    /// <returns>The scene.</returns>
    private static Scene ThreeD() =>
        new(new MeshNode(PrimitiveTessellator.Box(new Vector3D(4, 6, 8))) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(10, 20, 30)) });

    /// <summary>
    ///     Builds a flat scene (a thin slab) away from the origin.
    /// </summary>
    /// <returns>The scene.</returns>
    private static Scene Flat() =>
        new(new MeshNode(PrimitiveTessellator.Box(new Vector3D(10, 6, 0.1))) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(5, 3, 0)) });

    /// <summary>
    ///     Asserts that two rotations (<c>$vpr</c> values) turn the camera axes the same way.
    /// </summary>
    /// <param name="expected">The expected rotation in degrees.</param>
    /// <param name="actual">The actual rotation in degrees.</param>
    private static void AssertSameRotation(Vector3D expected, Vector3D actual)
    {
        var a = ScadViewport.CameraToWorld(expected);
        var b = ScadViewport.CameraToWorld(actual);
        foreach (var axis in new[] { Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ })
        {
            AssertVector(a.TransformDirection(axis), b.TransformDirection(axis));
        }
    }

    /// <summary>
    ///     Asserts that two vectors are equal within the tolerance.
    /// </summary>
    /// <param name="expected">The expected vector.</param>
    /// <param name="actual">The actual vector.</param>
    private static void AssertVector(Vector3D expected, Vector3D actual)
    {
        Assert.Equal(expected.X, actual.X, 1e-8);
        Assert.Equal(expected.Y, actual.Y, 1e-8);
        Assert.Equal(expected.Z, actual.Z, 1e-8);
    }

    /// <summary>
    ///     Asserts that two cameras have the same eye, target, up and projection.
    /// </summary>
    /// <param name="expected">The expected camera.</param>
    /// <param name="actual">The actual camera.</param>
    private static void AssertCamera(Camera expected, Camera actual)
    {
        AssertVector(expected.Eye, actual.Eye);
        AssertVector(expected.Target, actual.Target);
        AssertVector(expected.Up, actual.Up);
        Assert.Equal(expected.Projection, actual.Projection);
        Assert.Equal(expected.OrthographicHeight, actual.OrthographicHeight);
    }
}
