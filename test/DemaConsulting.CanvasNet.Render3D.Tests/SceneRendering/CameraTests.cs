using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for <see cref="Camera"/> and the internal <c>ViewTransform</c> (Camera unit).
/// </summary>
public class CameraTests
{
    /// <summary>
    ///     Projects a world point to normalized device coordinates (x right, y up) with the camera's
    ///     own view matrix and projection parameters.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="world">The world-space point.</param>
    /// <param name="aspect">The output aspect ratio (width / height).</param>
    /// <returns>The NDC x and y.</returns>
    private static (double X, double Y) ProjectNdc(Camera camera, Vector3D world, double aspect)
    {
        var p = camera.GetViewMatrix().TransformPoint(world);
        if (camera.Projection == CameraProjection.Orthographic)
        {
            var halfHeight = camera.OrthographicHeight!.Value / 2.0;
            return (p.X / (halfHeight * aspect), p.Y / halfHeight);
        }

        var tanV = Math.Tan(camera.FieldOfViewDegrees * Math.PI / 360.0);
        var depth = -p.Z;
        return (p.X / (depth * tanV * aspect), p.Y / (depth * tanV));
    }

    /// <summary>
    ///     Enumerates the eight corners of a box centred on <paramref name="centre"/> with the given half extents.
    /// </summary>
    /// <param name="centre">The box centre.</param>
    /// <param name="half">The half extents.</param>
    /// <returns>The eight corners.</returns>
    private static IEnumerable<Vector3D> Corners(Vector3D centre, Vector3D half)
    {
        for (var i = 0; i < 8; i++)
        {
            yield return centre + new Vector3D(
                (i & 1) == 0 ? -half.X : half.X,
                (i & 2) == 0 ? -half.Y : half.Y,
                (i & 4) == 0 ? -half.Z : half.Z);
        }
    }

    /// <summary>
    ///     Validates that LookAt records eye, target and up.
    /// </summary>
    [Fact]
    public void Camera_LookAt_SetsEyeTargetUp()
    {
        // Arrange / Act
        var camera = Camera.LookAt(new Vector3D(1, 2, 3), new Vector3D(4, 5, 6), Vector3D.UnitZ);

        // Assert
        Assert.Equal(new Vector3D(1, 2, 3), camera.Eye);
        Assert.Equal(new Vector3D(4, 5, 6), camera.Target);
        Assert.Equal(Vector3D.UnitZ, camera.Up);
        Assert.Equal(CameraProjection.Perspective, camera.Projection);
    }

    /// <summary>
    ///     Validates that LookAt rejects an eye equal to the target, a zero up vector and non-finite input.
    /// </summary>
    [Fact]
    public void Camera_LookAt_EyeEqualsTarget_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Camera.LookAt(Vector3D.One, Vector3D.One, Vector3D.UnitZ));
        Assert.Throws<ArgumentException>(() => Camera.LookAt(Vector3D.Zero, Vector3D.One, Vector3D.Zero));
        Assert.Throws<ArgumentException>(() => Camera.LookAt(new Vector3D(double.NaN, 0, 0), Vector3D.One, Vector3D.UnitZ));
    }

    /// <summary>
    ///     Validates that the view matrix maps the eye to the origin and the target onto the -Z axis at the right distance.
    /// </summary>
    [Fact]
    public void Camera_GetViewMatrix_MapsEyeToOriginAndTargetToNegativeZ()
    {
        // Arrange
        var eye = new Vector3D(3, -4, 5);
        var target = new Vector3D(1, 2, 0);
        var camera = Camera.LookAt(eye, target, Vector3D.UnitZ);
        var view = camera.GetViewMatrix();

        // Act
        var eyeView = view.TransformPoint(eye);
        var targetView = view.TransformPoint(target);

        // Assert
        Render3DTestHelpers.AssertVector(Vector3D.Zero, eyeView, 1e-12);
        Render3DTestHelpers.AssertVector(new Vector3D(0, 0, -(target - eye).Length), targetView, 1e-12);
        Assert.Equal(1.0, view.Determinant, 1e-12);
    }

    /// <summary>
    ///     Validates that the world up direction maps to +Y for a horizontal view.
    /// </summary>
    [Fact]
    public void Camera_GetViewMatrix_UpMapsToPositiveY()
    {
        // Arrange: looking along +Y from -Y with +Z up
        var camera = Camera.LookAt(new Vector3D(0, -10, 0), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        var up = camera.GetViewMatrix().TransformDirection(Vector3D.UnitZ);
        var right = camera.GetViewMatrix().TransformDirection(Vector3D.UnitX);

        // Assert: world Z is view up; world X is view right
        Render3DTestHelpers.AssertVector(Vector3D.UnitY, up, 1e-12);
        Render3DTestHelpers.AssertVector(Vector3D.UnitX, right, 1e-12);
    }

    /// <summary>
    ///     Validates the documented defaults.
    /// </summary>
    [Fact]
    public void Camera_Defaults_PerspectiveWithDocumentedFieldOfView()
    {
        // Arrange / Act
        var camera = new Camera();

        // Assert
        Assert.Equal(CameraProjection.Perspective, camera.Projection);
        Assert.Equal(22.5, camera.FieldOfViewDegrees);
        Assert.Equal(Camera.DefaultFieldOfViewDegrees, camera.FieldOfViewDegrees);
        Assert.Null(camera.OrthographicHeight);
        Assert.Null(camera.NearPlane);
        Assert.Null(camera.FarPlane);
        Assert.Equal(Vector3D.UnitZ, camera.Up);
        Assert.Equal(45.0, Camera.DefaultAzimuthDegrees);
        Assert.Equal(35.26438968, Camera.DefaultElevationDegrees, 1e-6);
    }

    /// <summary>
    ///     Validates that an orthographic camera retains its height.
    /// </summary>
    [Fact]
    public void Camera_Orthographic_WithHeight_RetainsHeight()
    {
        // Arrange / Act
        var camera = new Camera { Projection = CameraProjection.Orthographic, OrthographicHeight = 12.5 };

        // Assert
        Assert.Equal(12.5, camera.OrthographicHeight);
        Assert.Equal(CameraProjection.Orthographic, camera.Projection);
        camera.Validate();
    }

    /// <summary>
    ///     Validates that a front view (azimuth 0, elevation 0) places the eye on the negative Y axis.
    /// </summary>
    [Fact]
    public void Camera_FitToBounds_FrontView_EyeOnNegativeYAxis()
    {
        // Arrange
        var bounds = new BoundingBox3D(new Vector3D(-1, -1, -1), new Vector3D(3, 1, 1));

        // Act
        var camera = Camera.FitToBounds(bounds, 0.0, 0.0, 1.0);

        // Assert: target is the centre; eye shares its X and Z and lies in front (-Y)
        Assert.Equal(bounds.Center, camera.Target);
        Assert.Equal(bounds.Center.X, camera.Eye.X, 1e-9);
        Assert.Equal(bounds.Center.Z, camera.Eye.Z, 1e-9);
        Assert.True(camera.Eye.Y < bounds.Min.Y);
    }

    /// <summary>
    ///     Validates that a positive azimuth moves the eye toward +X and a positive elevation raises it.
    /// </summary>
    [Fact]
    public void Camera_FitToBounds_AzimuthAndElevation_FollowOrbitConvention()
    {
        // Arrange
        var bounds = new BoundingBox3D(new Vector3D(-1, -1, -1), new Vector3D(1, 1, 1));

        // Act
        var side = Camera.FitToBounds(bounds, 90.0, 0.0, 1.0);
        var above = Camera.FitToBounds(bounds, 0.0, 30.0, 1.0);

        // Assert: azimuth 90 puts the eye on +X; elevation lifts it above the centre
        Assert.True(side.Eye.X > 1.0);
        Assert.Equal(0.0, side.Eye.Y, 1e-9);
        Assert.True(above.Eye.Z > 0.0);
        Assert.True(above.Eye.Y < 0.0);
    }

    /// <summary>
    ///     Validates that every corner of the original bounds projects inside the frame.
    /// </summary>
    [Fact]
    public void Camera_FitToBounds_Perspective_AllCornersProjectInsideFrame()
    {
        // Arrange
        var centre = new Vector3D(5, -3, 2);
        var half = new Vector3D(2, 1, 3);
        var bounds = new BoundingBox3D(centre - half, centre + half);
        const double aspect = 1.5;

        // Act
        var camera = Camera.FitToBounds(bounds, 30.0, 20.0, aspect);

        // Assert
        foreach (var corner in Corners(centre, half))
        {
            var (x, y) = ProjectNdc(camera, corner, aspect);
            Assert.InRange(Math.Abs(x), 0.0, 1.0);
            Assert.InRange(Math.Abs(y), 0.0, 1.0);
        }
    }

    /// <summary>
    ///     Validates that the most demanding inflated corner touches the frame edge exactly while the
    ///     original corners stay strictly inside (the margin).
    /// </summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(2.0)]
    public void Camera_FitToBounds_Perspective_TightestCornerTouchesMargin(double aspect)
    {
        // Arrange
        var centre = new Vector3D(1, 2, 3);
        var half = new Vector3D(1.5, 1.0, 2.0);
        var bounds = new BoundingBox3D(centre - half, centre + half);
        const double margin = 0.05;

        // Act
        var camera = Camera.FitToBounds(bounds, 40.0, 25.0, aspect, marginFraction: margin);

        // Assert: inflated corners reach |ndc| == 1 exactly once, originals stay inside
        var inflatedMax = Corners(centre, half * (1.0 + margin)).Max(c => Math.Max(Math.Abs(ProjectNdc(camera, c, aspect).X), Math.Abs(ProjectNdc(camera, c, aspect).Y)));
        var originalMax = Corners(centre, half).Max(c => Math.Max(Math.Abs(ProjectNdc(camera, c, aspect).X), Math.Abs(ProjectNdc(camera, c, aspect).Y)));
        Assert.Equal(1.0, inflatedMax, 1e-9);
        Assert.True(originalMax < 1.0);
    }

    /// <summary>
    ///     Validates that the orthographic height covers the bounds exactly (inflated corners touch the frame).
    /// </summary>
    [Fact]
    public void Camera_FitToBounds_Orthographic_HeightCoversBounds()
    {
        // Arrange
        var centre = new Vector3D(0, 0, 0);
        var half = new Vector3D(2, 3, 1);
        var bounds = new BoundingBox3D(centre - half, centre + half);
        const double aspect = 2.0;
        const double margin = 0.1;

        // Act
        var camera = Camera.FitToBounds(bounds, 20.0, 30.0, aspect, CameraProjection.Orthographic, marginFraction: margin);

        // Assert
        Assert.Equal(CameraProjection.Orthographic, camera.Projection);
        Assert.NotNull(camera.OrthographicHeight);
        var max = Corners(centre, half * (1.0 + margin)).Max(c => Math.Max(Math.Abs(ProjectNdc(camera, c, aspect).X), Math.Abs(ProjectNdc(camera, c, aspect).Y)));
        Assert.Equal(1.0, max, 1e-9);
        camera.Validate();
    }

    /// <summary>
    ///     Validates that empty bounds fall back to a unit cube around the origin.
    /// </summary>
    [Fact]
    public void Camera_FitToBounds_EmptyBounds_FallsBackToUnitCube()
    {
        // Arrange / Act
        var camera = Camera.FitToBounds(BoundingBox3D.Empty, 45.0, 30.0, 1.0);

        // Assert: looks at the origin from a finite, non-zero distance that frames the unit cube
        Assert.Equal(Vector3D.Zero, camera.Target);
        Assert.True(camera.Eye.IsFinite);
        Assert.True(camera.Eye.Length > 0.5);
        foreach (var corner in Corners(Vector3D.Zero, new Vector3D(0.5, 0.5, 0.5)))
        {
            var (x, y) = ProjectNdc(camera, corner, 1.0);
            Assert.InRange(Math.Abs(x), 0.0, 1.0);
            Assert.InRange(Math.Abs(y), 0.0, 1.0);
        }
    }

    /// <summary>
    ///     Validates that a non-positive aspect ratio is rejected.
    /// </summary>
    [Fact]
    public void Camera_FitToBounds_NonPositiveAspect_Throws()
    {
        // Arrange
        var bounds = new BoundingBox3D(Vector3D.Zero, Vector3D.One);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, 0, 0, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, 0, 0, -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, 0, 0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, 0, 0, 1.0, fieldOfViewDegrees: 180.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, 0, 0, 1.0, marginFraction: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, double.NaN, 0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, 0, double.PositiveInfinity, 1.0));
    }

    /// <summary>
    ///     Validates that the field of view must lie strictly between 0 and 180 degrees.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(180.0)]
    [InlineData(double.NaN)]
    public void Camera_FieldOfView_OutOfRange_Throws(double fov)
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera { FieldOfViewDegrees = fov });
    }

    /// <summary>
    ///     Validates near/far validation: individual values must be positive and near must be below far.
    /// </summary>
    [Fact]
    public void Camera_NearFar_Invalid_Throws()
    {
        // Arrange / Act / Assert: individual initializers
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera { NearPlane = 0.0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera { NearPlane = -1.0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera { FarPlane = double.NaN });

        // Cross-property rule: near must be below far
        Assert.Throws<ArgumentException>(() => new Camera { NearPlane = 5.0, FarPlane = 2.0 }.Validate());
        new Camera { NearPlane = 1.0, FarPlane = 2.0 }.Validate();
    }

    /// <summary>
    ///     Validates that a non-positive orthographic height is rejected and that an orthographic
    ///     camera without a height fails validation.
    /// </summary>
    [Fact]
    public void Camera_OrthographicHeight_NonPositive_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera { OrthographicHeight = 0.0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera { OrthographicHeight = -2.0 });
        Assert.Throws<ArgumentException>(() => new Camera { Projection = CameraProjection.Orthographic }.Validate());
    }

    /// <summary>
    ///     Validates that an undefined <see cref="CameraProjection"/> value is rejected by
    ///     <c>Validate</c> and by the view transform, instead of rendering as perspective.
    /// </summary>
    [Fact]
    public void Camera_Validate_UndefinedProjection_Throws()
    {
        // Arrange
        var camera = new Camera { Projection = (CameraProjection)42, OrthographicHeight = 5.0 };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => camera.Validate());
        Assert.Throws<ArgumentException>(() => new ViewTransform(camera, 1.0));
    }

    /// <summary>
    ///     Validates that <c>FitToBounds</c> rejects an undefined <see cref="CameraProjection"/>
    ///     value while still accepting both defined values.
    /// </summary>
    [Fact]
    public void Camera_FitToBounds_UndefinedProjection_Throws()
    {
        // Arrange
        var bounds = new BoundingBox3D(Vector3D.Zero, Vector3D.One);

        // Act
        var perspective = Camera.FitToBounds(bounds, 0, 0, 1.0, CameraProjection.Perspective);
        var orthographic = Camera.FitToBounds(bounds, 0, 0, 1.0, CameraProjection.Orthographic);

        // Assert
        Assert.Equal(CameraProjection.Perspective, perspective.Projection);
        Assert.Equal(CameraProjection.Orthographic, orthographic.Projection);
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, 0, 0, 1.0, (CameraProjection)42));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camera.FitToBounds(bounds, 0, 0, 1.0, (CameraProjection)(-1)));
    }

    /// <summary>
    ///     Validates that non-finite eye/target/up values are rejected at initialization.
    /// </summary>
    [Fact]
    public void Camera_Vectors_NonFinite_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new Camera { Eye = new Vector3D(double.NaN, 0, 0) });
        Assert.Throws<ArgumentException>(() => new Camera { Target = new Vector3D(0, double.PositiveInfinity, 0) });
        Assert.Throws<ArgumentException>(() => new Camera { Up = new Vector3D(0, 0, double.NaN) });
        Assert.Throws<ArgumentException>(() => new Camera { Eye = Vector3D.Zero, Target = Vector3D.Zero }.GetViewMatrix());
    }

    /// <summary>
    ///     Validates that a view direction parallel to up substitutes +Y (looking down) or -Y (looking up).
    /// </summary>
    [Fact]
    public void Camera_LookAt_ForwardParallelToUp_SubstitutesAlternateUp()
    {
        // Arrange / Act
        var down = Camera.LookAt(new Vector3D(0, 0, 10), Vector3D.Zero, Vector3D.UnitZ);
        var upward = Camera.LookAt(new Vector3D(0, 0, -10), Vector3D.Zero, Vector3D.UnitZ);

        // Assert: substituted ups, and well-formed view matrices (world +Y is screen up when looking down)
        Assert.Equal(Vector3D.UnitY, down.Up);
        Assert.Equal(-Vector3D.UnitY, upward.Up);
        Render3DTestHelpers.AssertVector(Vector3D.UnitY, down.GetViewMatrix().TransformDirection(Vector3D.UnitY), 1e-12);
        Render3DTestHelpers.AssertVector(Vector3D.UnitX, down.GetViewMatrix().TransformDirection(Vector3D.UnitX), 1e-12);
        Assert.Equal(1.0, upward.GetViewMatrix().Determinant, 1e-12);
    }

    /// <summary>
    ///     Validates that LookAt accepts a tiny non-zero view offset and up hint whose squared
    ///     lengths underflow, and still builds the correct view.
    /// </summary>
    [Fact]
    public void Camera_LookAt_TinyOffsetAndUp_IsAccepted()
    {
        // Arrange: the target is 1e-200 in front of the eye and the up hint is 1e-200 long
        var eye = Vector3D.Zero;
        var target = new Vector3D(0.0, 1e-200, 0.0);
        var up = new Vector3D(0.0, 0.0, 1e-200);

        // Act
        var camera = Camera.LookAt(eye, target, up);
        var forwardInView = camera.GetViewMatrix().TransformDirection(Vector3D.UnitY);
        var upInView = camera.GetViewMatrix().TransformDirection(Vector3D.UnitZ);

        // Assert: forward maps to -Z and the up hint maps to +Y
        Render3DTestHelpers.AssertVector(new Vector3D(0.0, 0.0, -1.0), forwardInView, 1e-12);
        Render3DTestHelpers.AssertVector(Vector3D.UnitY, upInView, 1e-12);
    }

    /// <summary>
    ///     Validates that LookAt accepts a view offset and up hint whose squared lengths overflow.
    /// </summary>
    [Fact]
    public void Camera_LookAt_HugeOffsetAndUp_IsAccepted()
    {
        // Arrange
        var eye = Vector3D.Zero;
        var target = new Vector3D(0.0, 1e200, 0.0);
        var up = new Vector3D(0.0, 0.0, 1e200);

        // Act
        var camera = Camera.LookAt(eye, target, up);
        var forwardInView = camera.GetViewMatrix().TransformDirection(Vector3D.UnitY);
        var upInView = camera.GetViewMatrix().TransformDirection(Vector3D.UnitZ);

        // Assert
        Render3DTestHelpers.AssertVector(new Vector3D(0.0, 0.0, -1.0), forwardInView, 1e-12);
        Render3DTestHelpers.AssertVector(Vector3D.UnitY, upInView, 1e-12);
    }

    /// <summary>
    ///     Validates that the parallel up/forward degeneracy check is scale invariant: a tiny up
    ///     hint parallel to a tiny view direction still has an alternate up substituted.
    /// </summary>
    [Fact]
    public void Camera_LookAt_TinyUpParallelToForward_SubstitutesAlternateUp()
    {
        // Arrange / Act
        var camera = Camera.LookAt(new Vector3D(0.0, 0.0, 1e-200), Vector3D.Zero, new Vector3D(0.0, 0.0, 1e-200));

        // Assert: looking down along -Z with +Z up substitutes +Y, as for unit-scale input
        Assert.Equal(Vector3D.UnitY, camera.Up);
    }

    /// <summary>
    ///     Validates that a camera whose eye and target differ by a tiny amount, or whose up vector
    ///     is tiny, is accepted when its view basis is computed, while exact zero is still rejected.
    /// </summary>
    [Fact]
    public void Camera_GetViewMatrix_TinyOffsetAndUp_AcceptedAndZeroRejected()
    {
        // Arrange
        var tiny = new Camera { Eye = Vector3D.Zero, Target = new Vector3D(0.0, 1e-200, 0.0), Up = new Vector3D(0.0, 0.0, 1e-200) };
        var zeroOffset = new Camera { Eye = Vector3D.One, Target = Vector3D.One, Up = Vector3D.UnitZ };
        var zeroUp = new Camera { Eye = Vector3D.Zero, Target = Vector3D.UnitY, Up = Vector3D.Zero };

        // Act
        var view = tiny.GetViewMatrix();

        // Assert
        Assert.Equal(1.0, view.Determinant, 1e-12);
        Assert.Throws<ArgumentException>(() => zeroOffset.GetViewMatrix());
        Assert.Throws<ArgumentException>(() => zeroUp.GetViewMatrix());
    }

    /// <summary>
    ///     Validates that a straight-down elevation of 90 degrees does not throw and yields a finite view.
    /// </summary>
    [Fact]
    public void Camera_FitToBounds_Elevation90_DoesNotThrow()
    {
        // Arrange
        var bounds = new BoundingBox3D(new Vector3D(-1, -1, -1), new Vector3D(1, 1, 1));

        // Act
        var top = Camera.FitToBounds(bounds, 0.0, 90.0, 1.0);
        var bottom = Camera.FitToBounds(bounds, 0.0, -90.0, 1.0);

        // Assert
        Assert.True(top.Eye.Z > 1.0);
        Assert.True(bottom.Eye.Z < -1.0);
        Assert.True(top.GetViewMatrix().IsFinite);
        Assert.True(bottom.GetViewMatrix().IsFinite);
        top.Validate();
    }

    /// <summary>
    ///     Validates that the camera-relative composition equals world-then-view composition and
    ///     stays accurate for a far-away model (precision strategy).
    /// </summary>
    [Fact]
    public void ViewTransform_ComposeModelToView_FarModel_IsCameraRelative()
    {
        // Arrange: eye and model both one million units from the origin
        var camera = Camera.LookAt(new Vector3D(1e6, -10, 0), new Vector3D(1e6, 0, 0), Vector3D.UnitZ);
        var view = new ViewTransform(camera, 1.0);
        var world = Matrix4x4D.CreateTranslation(new Vector3D(1e6 + 0.25, 0, 0));

        // Act
        var p = view.ComposeModelToView(world).TransformPoint(Vector3D.Zero);

        // Assert: 0.25 right, 10 in front of the eye
        Render3DTestHelpers.AssertVector(new Vector3D(0.25, 0, -10), p, 1e-9);
        Assert.False(view.IsOrthographic);
        Assert.Equal(Math.Tan(22.5 * Math.PI / 360.0), view.TanHalfFovY, 1e-12);
        Render3DTestHelpers.AssertMatrix(camera.GetViewMatrix(), view.WorldToView, 1e-9);
    }
}
