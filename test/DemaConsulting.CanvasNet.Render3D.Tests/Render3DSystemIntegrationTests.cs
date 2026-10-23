// cspell:ignore Tessellator

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests;

/// <summary>
///     System-level integration tests for the <c>DemaConsulting.CanvasNet.Render3D</c> package, exercising
///     only its public API end to end (one test method per top-level system requirement declared in
///     <c>docs/reqstream/canvas-net-render3d.yaml</c> and its platform requirements). Unlike the unit and
///     subsystem tests, every test here builds scenes with the public model types and renders through
///     <see cref="SceneRenderer"/>, proving the whole pipeline composes correctly for an external caller.
/// </summary>
public class Render3DSystemIntegrationTests
{
    /// <summary>The opaque white default background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>The default neutral gray.</summary>
    private static readonly Rgba32 Gray = new(180, 180, 180, 255);

    /// <summary>A view from +Z straight down, orthographic, 16 world units tall.</summary>
    private static readonly Camera TopDown = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, 10), Vector3D.Zero, Vector3D.UnitY, 16.0);

    /// <summary>
    ///     Projects a world point to pixel coordinates for a camera and image size.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="world">The world point.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The pixel x and y.</returns>
    private static (int X, int Y) ToPixel(Camera camera, Vector3D world, int width, int height)
    {
        var p = camera.GetViewMatrix().TransformPoint(world);
        var aspect = width / (double)height;
        double ndcX;
        double ndcY;
        if (camera.Projection == CameraProjection.Orthographic)
        {
            var half = camera.OrthographicHeight!.Value / 2.0;
            ndcX = p.X / (half * aspect);
            ndcY = p.Y / half;
        }
        else
        {
            var tanV = Math.Tan(camera.FieldOfViewDegrees * Math.PI / 360.0);
            ndcX = p.X / (-p.Z * tanV * aspect);
            ndcY = p.Y / (-p.Z * tanV);
        }

        return ((int)Math.Floor((0.5 + (ndcX * 0.5)) * width), (int)Math.Floor((0.5 - (ndcY * 0.5)) * height));
    }

    /// <summary>
    ///     Validates scene assembly: a transformed group's bounds equal the analytic transformed box.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Scene_TransformedGroupBoundsMatchAnalytic()
    {
        // Arrange: a unit-cube mesh scaled by 2, rotated 90 degrees about Z and moved by (10, 20, 30), through two group levels
        var cube = PrimitiveTessellator.Box(Vector3D.One);
        var leaf = new MeshNode(cube) { Transform = Matrix4x4D.CreateScale(2.0) * Matrix4x4D.CreateRotationZ(90.0) };
        var scene = new Scene(new GroupNode([new GroupNode([leaf])]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(10, 20, 30)) });

        // Act
        var bounds = scene.ComputeBounds();

        // Assert: x in [0,2] -> y in [0,2]; y in [0,2] -> x in [-2,0]; then translate
        Render3DTestHelpers.AssertVector(new Vector3D(8, 20, 30), bounds.Min);
        Render3DTestHelpers.AssertVector(new Vector3D(10, 22, 32), bounds.Max);
    }

    /// <summary>
    ///     Validates that invalid scene input is rejected at construction.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Scene_InvalidInputIsRejected()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new Scene(null!));
        Assert.Throws<ArgumentNullException>(() => new MeshNode(null!));
        Assert.Throws<ArgumentException>(() => new GroupNode([null!]));
        Assert.Throws<ArgumentException>(() => new MeshNode(PrimitiveTessellator.Box(Vector3D.One)) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(double.NaN, 0, 0)) });
        Assert.Throws<ArgumentException>(() => new Mesh([Vector3D.Zero], [0, 0]));
    }

    /// <summary>
    ///     Validates the primitive meshes: closed, outward oriented and with analytic volumes.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_PrimitiveMeshes_BoxSphereCylinderAreClosedWithAnalyticVolume()
    {
        // Arrange / Act
        var box = PrimitiveTessellator.Box(new Vector3D(2, 3, 4));
        var sphere = PrimitiveTessellator.Sphere(1.0, 48);
        var cylinder = PrimitiveTessellator.Cylinder(2.0, 1.0, 1.0, 64);
        var cone = PrimitiveTessellator.Cylinder(3.0, 1.0, 0.0, 64);

        // Assert
        Assert.All(new[] { box, sphere, cylinder, cone }, mesh => Assert.True(mesh.IsClosed));
        Assert.Equal(24.0, box.SignedVolume, 1e-9);
        Assert.InRange(sphere.SignedVolume, (4.0 / 3.0 * Math.PI) * 0.99, 4.0 / 3.0 * Math.PI);
        Assert.InRange(cylinder.SignedVolume, Math.PI * 2.0 * 0.998, Math.PI * 2.0);
        Assert.InRange(cone.SignedVolume, Math.PI * 0.998, Math.PI);
    }

    /// <summary>
    ///     Validates the headline render: a perspective cube's silhouette matches the analytic projected area (and is the platform requirements' shared test).
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Render_CubeSilhouetteMatchesAnalyticCoverage()
    {
        // Arrange: a 2-unit cube viewed head-on from 10 units; its front face (at distance 9) is the whole silhouette
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true)));
        var camera = Camera.LookAt(new Vector3D(0, -10, 0), Vector3D.Zero, Vector3D.UnitZ);
        const int size = 100;

        // Act
        using var surface = SceneRenderer.Render(scene, camera, size, size, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: projected half-extent = (1 / (9 tan(fov/2))) * size / 2 pixels
        var halfPixels = 1.0 / (9.0 * Math.Tan(22.5 * Math.PI / 360.0)) * (size / 2.0);
        var expected = 4.0 * halfPixels * halfPixels;
        var covered = size * size - Render3DTestHelpers.CountPixels(surface, White);
        Assert.InRange(covered, expected * 0.97, expected * 1.03);
        Assert.Equal(Gray, surface[50, 50]);
        Assert.Equal(White, surface[2, 2]);
    }

    /// <summary>
    ///     Validates that an empty scene renders only the background color.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Render_EmptySceneProducesBackgroundOnly()
    {
        // Arrange
        var scene = new Scene(new GroupNode([]));
        var background = new Rgba32(30, 60, 90, 255);

        // Act
        using var surface = SceneRenderer.RenderFitted(scene, 50, 40, new SceneRenderOptions { BackgroundColor = background }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(50, surface.Width);
        Assert.Equal(40, surface.Height);
        Assert.Equal(2000, Render3DTestHelpers.CountPixels(surface, background));
    }

    /// <summary>
    ///     Validates that invalid render arguments are rejected.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Render_InvalidArgumentsThrow()
    {
        // Arrange
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Box(Vector3D.One)));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SceneRenderer.Render(null!, TopDown, 10, 10, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => SceneRenderer.Render(scene, null!, 10, 10, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Render(scene, TopDown, 0, 10, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Render(scene, TopDown, 10, Surface.MaxDimension + 1, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => SceneRenderer.Render(scene, new Camera { Projection = CameraProjection.Orthographic }, 10, 10, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that a fitted camera frames a cube within the image with roughly the requested margin.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Camera_FitToBoundsFramesCubeWithinMargin()
    {
        // Arrange
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true)));
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 30.0, 25.0, 1.0);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 200, 200, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: nothing touches the border, and the farthest silhouette point from the centre sits at about
        // 1 / (1 + margin) = 95 % of the half-frame (the inflated bounds touch the frame; the real corners do not)
        var bounds = Render3DTestHelpers.BoundsOfNon(surface, White);
        Assert.True(bounds.MinX > 0 && bounds.MinY > 0 && bounds.MaxX < 199 && bounds.MaxY < 199);
        var reach = new[] { 99.5 - bounds.MinX, bounds.MaxX - 99.5, 99.5 - bounds.MinY, bounds.MaxY - 99.5 }.Max() / 100.0;
        Assert.InRange(reach, 0.92, 0.98);
    }

    /// <summary>
    ///     Validates that orthographic and perspective cameras both place the target at the image centre.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Camera_OrthographicAndPerspectiveCenterRay()
    {
        // Arrange: a small cube at the origin; one camera of each kind looking at it
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Box(new Vector3D(1, 1, 1), center: true)) { Appearance = new NodeAppearance(new Rgba32(0, 200, 0, 255)) });
        var perspective = Camera.LookAt(new Vector3D(5, -5, 4), Vector3D.Zero, Vector3D.UnitZ);
        var ortho = Render3DTestHelpers.OrthoCamera(new Vector3D(5, -5, 4), Vector3D.Zero, Vector3D.UnitZ, 6.0);
        var options = Render3DTestHelpers.FlatOptions(defaultColor: new Rgba32(0, 200, 0, 255));

        // Act
        using var a = SceneRenderer.Render(scene, perspective, 61, 61, options, TestContext.Current.CancellationToken);
        using var b = SceneRenderer.Render(scene, ortho, 61, 61, options, TestContext.Current.CancellationToken);

        // Assert: the centre pixel shows the cube in both, the corners show background, and the centre maps to the middle pixel
        Assert.Equal(new Rgba32(0, 200, 0, 255), a[30, 30]);
        Assert.Equal(new Rgba32(0, 200, 0, 255), b[30, 30]);
        Assert.Equal(White, a[0, 0]);
        Assert.Equal(White, b[60, 60]);
        Assert.Equal((30, 30), ToPixel(perspective, Vector3D.Zero, 61, 61));
        Assert.Equal((30, 30), ToPixel(ortho, Vector3D.Zero, 61, 61));
    }

    /// <summary>
    ///     Validates user-adjustable lights: ambient 0.25 plus a 0.5 headlight lights a camera-facing face to 135, a custom color tints it.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Lighting_CustomLightsProduceAnalyticShade()
    {
        // Arrange
        var scene = new Scene(new MeshNode(Render3DTestHelpers.CreateFloorQuad(-2, -2, 2, 2)));
        var headlight = new DirectionalLight { Direction = new Vector3D(0, 0, -1), IsCameraRelative = true, Intensity = 0.5f };
        var white = new SceneRenderOptions { Lights = [new AmbientLight { Intensity = 0.25f }, headlight], SpecularStrength = 0.0f, Supersampling = 1 };
        var red = new SceneRenderOptions { Lights = [new AmbientLight { Color = new Rgba32(255, 0, 0, 255), Intensity = 1.0f }], Supersampling = 1 };
        var worldLight = new SceneRenderOptions { Lights = [new DirectionalLight { Direction = new Vector3D(0, 0, 1) }], Supersampling = 1, SpecularStrength = 0.0f };

        // Act
        using var lit = SceneRenderer.Render(scene, TopDown, 32, 32, white, TestContext.Current.CancellationToken);
        using var tinted = SceneRenderer.Render(scene, TopDown, 32, 32, red, TestContext.Current.CancellationToken);
        using var awayFromLight = SceneRenderer.Render(scene, TopDown, 32, 32, worldLight, TestContext.Current.CancellationToken);

        // Assert: 180 * 0.75 = 135; pure red ambient keeps only red; a world light traveling +Z lights the +Z face not at all
        Assert.InRange(lit[16, 16].R, 134, 136);
        Assert.Equal(lit[16, 16].R, lit[16, 16].G);
        Assert.Equal(new Rgba32(180, 0, 0, 255), tinted[16, 16]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), awayFromLight[16, 16]);
    }

    /// <summary>
    ///     Validates that the default rig shades the three visible faces of an isometric cube in the analytic order: top, then left, then right.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Lighting_DefaultRigShadesFacesInExpectedOrder()
    {
        // Arrange: an isometric view of a cube; the key light comes from the upper left
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true)));
        var camera = Camera.FitToBounds(scene.ComputeBounds(), Camera.DefaultAzimuthDegrees, Camera.DefaultElevationDegrees, 1.0);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 200, 200, new SceneRenderOptions { Supersampling = 1 }, TestContext.Current.CancellationToken);

        // Assert: sample the centres of the +Z (top), -Y (left) and +X (right) faces
        var topPixel = ToPixel(camera, new Vector3D(0, 0, 1), 200, 200);
        var leftPixel = ToPixel(camera, new Vector3D(0, -1, 0), 200, 200);
        var rightPixel = ToPixel(camera, new Vector3D(1, 0, 0), 200, 200);
        var top = surface[topPixel.X, topPixel.Y];
        var left = surface[leftPixel.X, leftPixel.Y];
        var right = surface[rightPixel.X, rightPixel.Y];
        Assert.True(top.R > left.R, $"top {top} should be brighter than left {left}");
        Assert.True(left.R > right.R, $"left {left} should be brighter than right {right}");
        Assert.True(right.R > 0);
    }

    /// <summary>
    ///     Validates default node colors, per-node color overrides and the back-face color.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Appearance_DefaultNodeAndBackFaceColorsApplied()
    {
        // Arrange: left cube without an appearance, right cube red; an open quad viewed from behind below them
        var red = new Rgba32(220, 20, 20, 255);
        var cube = PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true);
        var scene = new Scene(new GroupNode(
        [
            new MeshNode(cube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(-4, 0, 0)) },
            new MeshNode(cube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(4, 0, 0)), Appearance = new NodeAppearance(red) },
        ]));
        var quadScene = new Scene(new MeshNode(Render3DTestHelpers.CreateFloorQuad(-2, -2, 2, 2)));
        var fromBelow = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, -10), Vector3D.Zero, Vector3D.UnitY, 8.0);

        // Act
        using var surface = SceneRenderer.Render(scene, TopDown, 64, 32, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);
        using var back = SceneRenderer.Render(quadScene, fromBelow, 32, 32, Render3DTestHelpers.FlatOptions(backFace: new Rgba32(1, 2, 3, 255)), TestContext.Current.CancellationToken);

        // Assert: pixel x of world x: 32 + x * 2 (16 units over 32 rows => 2 pixels per unit); left cube default gray, right cube red
        Assert.Equal(Gray, surface[32 - 8, 16]);
        Assert.Equal(red, surface[32 + 8, 16]);
        Assert.Equal(new Rgba32(1, 2, 3, 255), back[16, 16]);
    }

    /// <summary>
    ///     Validates that a transparent background is preserved: untouched pixels stay (0, 0, 0, 0) and edges blend by alpha without darkening.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Appearance_TransparentBackgroundPreserved()
    {
        // Arrange
        var scene = new Scene(new MeshNode(Render3DTestHelpers.CreateFloorQuad(-2.3, -2.3, 2.3, 2.3)));
        var options = Render3DTestHelpers.FlatOptions(supersampling: 2, background: new Rgba32(0, 0, 0, 0));

        // Act
        using var surface = SceneRenderer.Render(scene, TopDown, 32, 32, options, TestContext.Current.CancellationToken);

        // Assert: corners fully transparent, centre opaque gray, edge pixels partially transparent with the surface color intact
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[0, 0]);
        Assert.Equal(Gray, surface[16, 16]);
        var edge = Render3DTestHelpers.DistinctColors(surface).Where(c => c.A > 0 && c.A < 255).ToList();
        Assert.NotEmpty(edge);
        Assert.All(edge, c =>
        {
            Assert.InRange(c.R, 179, 181);
            Assert.InRange(c.G, 179, 181);
        });
    }

    /// <summary>
    ///     Validates hidden-surface removal: the nearer surface wins regardless of scene order.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Visibility_NearerSurfaceOccludesRegardlessOfOrder()
    {
        // Arrange: a near red quad and a far blue quad, listed in both orders
        var near = new MeshNode(Render3DTestHelpers.CreateFloorQuad(-4, -4, 1, 1, 3.0)) { Appearance = new NodeAppearance(new Rgba32(255, 0, 0, 255)) };
        var far = new MeshNode(Render3DTestHelpers.CreateFloorQuad(-1, -1, 4, 4, 0.0)) { Appearance = new NodeAppearance(new Rgba32(0, 0, 255, 255)) };

        // Act
        using var a = SceneRenderer.Render(new Scene(new GroupNode([near, far])), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);
        using var b = SceneRenderer.Render(new Scene(new GroupNode([far, near])), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(a, b);
        Assert.Equal(new Rgba32(255, 0, 0, 255), a[32, 32]);
    }

    /// <summary>
    ///     Validates that exactly coincident faces resolve by traversal order, deterministically.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Visibility_CoincidentFacesResolveByTraversalOrder()
    {
        // Arrange: two overlapping coplanar quads of different colors
        var first = new MeshNode(Render3DTestHelpers.CreateFloorQuad(-4, -4, 4, 4)) { Appearance = new NodeAppearance(new Rgba32(255, 0, 0, 255)) };
        var second = new MeshNode(Render3DTestHelpers.CreateFloorQuad(-3, -3, 5, 5)) { Appearance = new NodeAppearance(new Rgba32(0, 0, 255, 255)) };

        // Act
        using var redWins = SceneRenderer.Render(new Scene(new GroupNode([first, second])), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);
        using var blueWins = SceneRenderer.Render(new Scene(new GroupNode([second, first])), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: the overlap shows the first listed quad's color, with no speckle
        Assert.Equal(new Rgba32(255, 0, 0, 255), redWins[32, 32]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), blueWins[32, 32]);
        Assert.Single(Enumerable.Range(24, 16).SelectMany(y => Enumerable.Range(24, 16).Select(x => redWins[x, y])).Distinct());
    }

    /// <summary>
    ///     Validates that a mirrored cube renders identically to the original (winding is compensated for mirrored transforms).
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Visibility_MirroredCubeRendersIdenticallyToOriginal()
    {
        // Arrange
        var cube = PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true);
        var original = new Scene(new MeshNode(cube));
        var mirrored = new Scene(new MeshNode(cube) { Transform = Matrix4x4D.CreateMirror(new Vector3D(1, 0, 0)) });
        var camera = Camera.FitToBounds(original.ComputeBounds(), 25.0, 30.0, 1.0);

        // Act
        using var a = SceneRenderer.Render(original, camera, 100, 100, null, TestContext.Current.CancellationToken);
        using var b = SceneRenderer.Render(mirrored, camera, 100, 100, null, TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(a, b);
    }

    /// <summary>
    ///     Validates the near-plane clipping case: a large ground plane that passes behind the camera renders a clean horizon.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Visibility_GroundPlaneCrossingNearPlaneRendersHorizon()
    {
        // Arrange: the camera is one unit above a ground plane that extends 100 units in front of and behind it
        const int size = 90;
        var scene = new Scene(new MeshNode(Render3DTestHelpers.CreateFloorQuad(-100, -100, 100, 100)));
        var camera = Camera.LookAt(new Vector3D(0, 0, 1), new Vector3D(0, 1, 1), Vector3D.UnitZ);
        var options = Render3DTestHelpers.FlatOptions();

        // Act
        using var surface = SceneRenderer.Render(scene, camera, size, size, options, TestContext.Current.CancellationToken);

        // Assert: rows below the far edge of the plane are all ground; rows above the horizon are all background; nothing else appears
        var firstGroundRow = (size / 2) + (int)Math.Ceiling(0.05 * size / 2);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = surface[x, y];
                if (y >= firstGroundRow)
                {
                    Assert.Equal(Gray, pixel);
                }
                else if (y < size / 2)
                {
                    Assert.Equal(White, pixel);
                }
                else
                {
                    Assert.True(pixel == White || pixel == Gray, $"Unexpected color {pixel} at ({x},{y}).");
                }
            }
        }
    }

    /// <summary>
    ///     Validates antialiasing: a pixel half covered by a surface blends the surface and background colors at the midpoint.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Antialiasing_HalfCoveredPixelBlendsToMidColor()
    {
        // Arrange: 16 x 16 pixels of 1 world unit; the quad's left edge x = -2.5 is the exact middle of pixel column 5
        var scene = new Scene(new MeshNode(Render3DTestHelpers.CreateFloorQuad(-2.5, -4, 4, 4)));
        var camera = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, 10), Vector3D.Zero, Vector3D.UnitY, 16.0);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 16, 16, Render3DTestHelpers.FlatOptions(supersampling: 2), TestContext.Current.CancellationToken);

        // Assert: two of four samples are gray(180), two are white(255) => (2 * 180 + 2 * 255 + 2) / 4 = 218
        Assert.Equal(new Rgba32(218, 218, 218, 255), surface[5, 8]);
        Assert.Equal(Gray, surface[6, 8]);
        Assert.Equal(White, surface[4, 8]);
    }

    /// <summary>
    ///     Validates numeric precision: an object a million units from the origin renders byte-identically to the same object at the origin.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_NumericPrecision_ObjectOneMillionUnitsAwayRendersIdentically()
    {
        // Arrange: the same cube and camera, shifted by 1e6 along X
        const double offset = 1e6;
        var cube = PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true);
        var home = new Scene(new MeshNode(cube));
        var away = new Scene(new MeshNode(cube) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(offset, 0, 0)) });
        var homeCamera = Camera.LookAt(new Vector3D(0, -8, 0), Vector3D.Zero, Vector3D.UnitZ);
        var awayCamera = Camera.LookAt(new Vector3D(offset, -8, 0), new Vector3D(offset, 0, 0), Vector3D.UnitZ);

        // Act
        using var a = SceneRenderer.Render(home, homeCamera, 90, 90, null, TestContext.Current.CancellationToken);
        using var b = SceneRenderer.Render(away, awayCamera, 90, 90, null, TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(a, b);
        Assert.True(Render3DTestHelpers.CountPixels(a, White) < 90 * 90);
    }

    /// <summary>
    ///     Validates determinism: sequential and parallel renders of a mixed scene are byte-identical.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Determinism_ParallelAndSequentialRendersAreByteIdentical()
    {
        // Arrange
        var scene = Render3DTestHelpers.CreateMixedScene();
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 30.0, 20.0, 200.0 / 150.0);

        // Act
        using var sequential = SceneRenderer.Render(scene, camera, 200, 150, new SceneRenderOptions { Supersampling = 2, MaxDegreeOfParallelism = 1 }, TestContext.Current.CancellationToken);
        using var two = SceneRenderer.Render(scene, camera, 200, 150, new SceneRenderOptions { Supersampling = 2, MaxDegreeOfParallelism = 2 }, TestContext.Current.CancellationToken);
        using var automatic = SceneRenderer.Render(scene, camera, 200, 150, new SceneRenderOptions { Supersampling = 2, MaxDegreeOfParallelism = -1 }, TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(sequential, two);
        Render3DTestHelpers.AssertSurfacesIdentical(sequential, automatic);
    }

    /// <summary>
    ///     Validates determinism: rendering twice gives byte-identical output.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Determinism_RepeatedRenderIsByteIdentical()
    {
        // Arrange
        var scene = Render3DTestHelpers.CreateMixedScene();

        // Act
        using var first = SceneRenderer.RenderFitted(scene, 160, 120, null, TestContext.Current.CancellationToken);
        using var second = SceneRenderer.RenderFitted(scene, 160, 120, null, TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(first, second);
    }

    /// <summary>
    ///     Validates cooperative cancellation: a pre-cancelled token throws OperationCanceledException.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Cancellation_PreCancelledTokenThrowsOperationCanceled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var scene = Render3DTestHelpers.CreateMixedScene();

        // Act
        var ex = Assert.ThrowsAny<OperationCanceledException>(() => SceneRenderer.RenderFitted(scene, 64, 64, null, source.Token));

        // Assert
        Assert.IsNotType<AggregateException>(ex);
        Assert.Equal(source.Token, ex.CancellationToken);
    }

    /// <summary>
    ///     Validates that an invalid degree of parallelism is rejected.
    /// </summary>
    [Fact]
    public void CanvasNetRender3D_SystemIntegration_Cancellation_InvalidParallelismIsRejected()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { MaxDegreeOfParallelism = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneRenderOptions { MaxDegreeOfParallelism = -5 });
    }
}
