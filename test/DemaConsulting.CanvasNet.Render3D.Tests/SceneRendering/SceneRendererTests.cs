// cspell:ignore Tessellator

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for <see cref="SceneRenderer"/> (SceneRenderer unit).
/// </summary>
public class SceneRendererTests
{
    /// <summary>The opaque white default background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>The default neutral gray surface color.</summary>
    private static readonly Rgba32 Gray = new(180, 180, 180, 255);

    /// <summary>An orthographic camera looking down -Z from +Z with +Y up, showing 16 world units vertically.</summary>
    private static readonly Camera TopDown = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, 10), Vector3D.Zero, Vector3D.UnitY, 16.0);

    /// <summary>
    ///     Creates a one-mesh scene.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The scene.</returns>
    private static Scene SceneOf(Mesh mesh) => new(new MeshNode(mesh));

    /// <summary>
    ///     Creates a scene whose node transform overflows to infinity when applied to its vertices, so
    ///     that any scene processing (bounds computation, flattening) would fail with an exception.
    /// </summary>
    /// <returns>The scene.</returns>
    private static Scene CreateOverflowingScene() => new(new MeshNode(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(1e300, 1e300, 1e300)))
    {
        Transform = Matrix4x4D.CreateScale(1e300),
    });

    /// <summary>
    ///     Validates that null scene or camera arguments are rejected.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_NullSceneOrCamera_Throws()
    {
        // Arrange
        var scene = SceneOf(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SceneRenderer.Render(null!, TopDown, 8, 8, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => SceneRenderer.Render(scene, null!, 8, 8, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => SceneRenderer.RenderFitted(null!, 8, 8, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that invalid output sizes are rejected.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_InvalidDimensions_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var scene = SceneOf(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One));

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Render(scene, TopDown, 0, 8, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Render(scene, TopDown, 8, -1, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Render(scene, TopDown, Surface.MaxDimension + 1, 8, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.RenderFitted(scene, 8, 0, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that an orthographic camera without a height, a camera with eye equal to target
    ///     and a camera with near above far are rejected.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OrthographicWithoutHeight_ThrowsArgumentException()
    {
        // Arrange
        var scene = SceneOf(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One));
        var noHeight = new Camera { Eye = new Vector3D(0, 0, 10), Target = Vector3D.Zero, Projection = CameraProjection.Orthographic };
        var degenerate = new Camera { Eye = Vector3D.Zero, Target = Vector3D.Zero };
        var badRange = new Camera { Eye = new Vector3D(0, 0, 10), Target = Vector3D.Zero, NearPlane = 5.0, FarPlane = 2.0 };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => SceneRenderer.Render(scene, noHeight, 8, 8, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => SceneRenderer.Render(scene, degenerate, 8, 8, null, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => SceneRenderer.Render(scene, badRange, 8, 8, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that a camera with an undefined projection value is rejected by the renderer.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_UndefinedProjection_ThrowsArgumentException()
    {
        // Arrange
        var scene = SceneOf(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One));
        var camera = new Camera { Eye = new Vector3D(0, 0, 10), Target = Vector3D.Zero, Projection = (CameraProjection)42 };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => SceneRenderer.Render(scene, camera, 8, 8, null, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that an invalid supersampling factor cannot reach the renderer.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_InvalidSupersampling_Throws()
    {
        // Arrange
        var scene = SceneOf(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One));

        // Act / Assert: the options type rejects the value at construction
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Render(scene, TopDown, 8, 8, new SceneRenderOptions { Supersampling = 0 }, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderer.Render(scene, TopDown, 8, 8, new SceneRenderOptions { Supersampling = 5 }, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that the result has the requested size.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_ReturnsSurfaceOfRequestedSize()
    {
        // Arrange
        var scene = SceneOf(Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, -1), new Vector3D(2, 2, 2)));

        // Act
        using var surface = SceneRenderer.Render(scene, TopDown, 37, 23, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(37, surface.Width);
        Assert.Equal(23, surface.Height);
    }

    /// <summary>
    ///     Validates that images whose size is not a multiple of the tile size are fully rendered, including the partial edge tiles.
    /// </summary>
    [Theory]
    [InlineData(70, 45, 1)]
    [InlineData(33, 65, 3)]
    [InlineData(1, 1, 2)]
    public void SceneRenderer_Render_NonTileMultipleSize_RendersPartialTiles(int width, int height, int supersampling)
    {
        // Arrange: a floor quad much larger than the view covers every pixel
        var scene = SceneOf(Render3DTestHelpers.CreateFloorQuad(-50, -50, 50, 50));

        // Act
        using var surface = SceneRenderer.Render(scene, TopDown, width, height, Render3DTestHelpers.FlatOptions(supersampling), TestContext.Current.CancellationToken);

        // Assert: every pixel, including the last partial row and column, has the surface color
        Assert.Equal(width * height, Render3DTestHelpers.CountPixels(surface, Gray));
    }

    /// <summary>
    ///     Validates that an empty scene fills the surface with the background color.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_EmptyScene_FillsBackgroundColor()
    {
        // Arrange
        var scene = new Scene(new GroupNode([]));
        var options = new SceneRenderOptions { BackgroundColor = new Rgba32(10, 20, 30, 255) };

        // Act
        using var surface = SceneRenderer.Render(scene, TopDown, 40, 40, options, TestContext.Current.CancellationToken);
        using var fitted = SceneRenderer.RenderFitted(scene, 40, 40, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1600, Render3DTestHelpers.CountPixels(surface, new Rgba32(10, 20, 30, 255)));
        Assert.Equal(1600, Render3DTestHelpers.CountPixels(fitted, new Rgba32(10, 20, 30, 255)));
    }

    /// <summary>
    ///     Validates that pixels outside the silhouette keep the background color.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_PixelsOutsideSilhouette_AreBackground()
    {
        // Arrange: a 4x4 floor quad in a 16-unit view
        var scene = SceneOf(Render3DTestHelpers.CreateFloorQuad(-2, -2, 2, 2));

        // Act
        using var surface = SceneRenderer.Render(scene, TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: corners are background, the centre is the surface color
        Assert.Equal(White, surface[0, 0]);
        Assert.Equal(White, surface[63, 63]);
        Assert.Equal(White, surface[0, 63]);
        Assert.Equal(Gray, surface[32, 32]);
    }

    /// <summary>
    ///     Validates the exact dyadic coverage of an axis-aligned cube: 32 x 32 pixels, honouring the top-left rule.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OrthoCubeOnDyadicGrid_CoversExactly32By32Pixels()
    {
        // Arrange: pixel i has its centre at x = 0.25 i - 7.875; the box edges lie exactly on sample centres
        var box = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-3.875, -3.875, -1), new Vector3D(8, 8, 2));

        // Act
        using var surface = SceneRenderer.Render(SceneOf(box), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: left/top edges are owned, right/bottom are not => columns 16..47, rows 15..46
        var bounds = Render3DTestHelpers.BoundsOfNon(surface, White);
        Assert.Equal(1024, bounds.Count);
        Assert.Equal(16, bounds.MinX);
        Assert.Equal(47, bounds.MaxX);
        Assert.Equal(15, bounds.MinY);
        Assert.Equal(46, bounds.MaxY);
        Assert.Equal(1024, Render3DTestHelpers.CountPixels(surface, Gray));
    }

    /// <summary>
    ///     Validates that a quad straddling several tile boundaries renders as a perfect rectangle (no seams or holes).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void SceneRenderer_Render_QuadStraddlingTileBoundaries_HasNoSeams(int supersampling)
    {
        // Arrange: two triangles over a region crossing the 32 and 64 pixel tile boundaries of a 96 x 96 image
        var scene = SceneOf(Render3DTestHelpers.CreateFloorQuad(-3.7, -2.9, 3.9, 4.1));

        // Act
        using var surface = SceneRenderer.Render(scene, Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, 10), Vector3D.Zero, Vector3D.UnitY, 12.0), 96, 96, Render3DTestHelpers.FlatOptions(supersampling), TestContext.Current.CancellationToken);

        // Assert: inside the bounding box of the shape, every pixel is exactly the surface color at k = 1 (the rectangle
        // edges fall inside pixels at k = 3, so interior pixels are checked instead)
        var bounds = Render3DTestHelpers.BoundsOfNon(surface, White);
        Assert.True(bounds.MinX < 32 && bounds.MaxX > 64);
        Assert.True(bounds.MinY < 32 && bounds.MaxY > 64);
        var solid = 0;
        for (var y = bounds.MinY + 1; y < bounds.MaxY; y++)
        {
            for (var x = bounds.MinX + 1; x < bounds.MaxX; x++)
            {
                Assert.Equal(Gray, surface[x, y]);
                solid++;
            }
        }

        Assert.True(solid > 3000);
    }

    /// <summary>
    ///     Validates that without supersampling only the background and the surface color occur.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_Supersampling1_HasNoIntermediateEdgeColors()
    {
        // Arrange: a rotated box has slanted edges
        var scene = new Scene(new MeshNode(Render3DTestHelpers.CreateBoxMesh(new Vector3D(-2, -2, -2), new Vector3D(4, 4, 4))) { Transform = Matrix4x4D.CreateRotationZ(30.0) });

        // Act
        using var surface = SceneRenderer.Render(scene, TopDown, 64, 64, Render3DTestHelpers.FlatOptions(1), TestContext.Current.CancellationToken);

        // Assert
        var colors = Render3DTestHelpers.DistinctColors(surface);
        Assert.Equal(2, colors.Count);
        Assert.Contains(White, colors);
        Assert.Contains(Gray, colors);
    }

    /// <summary>
    ///     Validates that 3x3 supersampling produces blended edge colors between background and surface.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_Supersampling3_ProducesIntermediateEdgeColors()
    {
        // Arrange
        var scene = new Scene(new MeshNode(Render3DTestHelpers.CreateBoxMesh(new Vector3D(-2, -2, -2), new Vector3D(4, 4, 4))) { Transform = Matrix4x4D.CreateRotationZ(30.0) });

        // Act
        using var surface = SceneRenderer.Render(scene, TopDown, 64, 64, Render3DTestHelpers.FlatOptions(3), TestContext.Current.CancellationToken);

        // Assert: blended pixels lie strictly between 180 and 255 on every channel
        var blended = Render3DTestHelpers.DistinctColors(surface).Where(c => c.R > 180 && c.R < 255).ToList();
        Assert.True(blended.Count >= 3);
        Assert.All(blended, c => Assert.Equal(255, c.A));
    }

    /// <summary>
    ///     Validates that sequential and parallel renders are byte-identical.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_ParallelismOneVersusMany_ByteIdentical()
    {
        // Arrange
        var scene = Render3DTestHelpers.CreateMixedScene();
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 40.0, 25.0, 200.0 / 150.0);

        // Act
        using var sequential = SceneRenderer.Render(scene, camera, 200, 150, new SceneRenderOptions { Supersampling = 2, MaxDegreeOfParallelism = 1 }, TestContext.Current.CancellationToken);
        using var two = SceneRenderer.Render(scene, camera, 200, 150, new SceneRenderOptions { Supersampling = 2, MaxDegreeOfParallelism = 2 }, TestContext.Current.CancellationToken);
        using var many = SceneRenderer.Render(scene, camera, 200, 150, new SceneRenderOptions { Supersampling = 2, MaxDegreeOfParallelism = -1 }, TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(sequential, two);
        Render3DTestHelpers.AssertSurfacesIdentical(sequential, many);
        Assert.True(Render3DTestHelpers.DistinctColors(sequential).Count > 10);
    }

    /// <summary>
    ///     Validates that rendering the same input twice gives identical bytes.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_RepeatedRender_ByteIdentical()
    {
        // Arrange
        var scene = Render3DTestHelpers.CreateMixedScene();

        // Act
        using var first = SceneRenderer.RenderFitted(scene, 120, 90, null, TestContext.Current.CancellationToken);
        using var second = SceneRenderer.RenderFitted(scene, 120, 90, null, TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(first, second);
    }

    /// <summary>
    ///     Validates that a pre-cancelled token throws OperationCanceledException (sequential path).
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_PreCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var scene = Render3DTestHelpers.CreateMixedScene();

        // Act
        var ex = Assert.ThrowsAny<OperationCanceledException>(() => SceneRenderer.RenderFitted(scene, 64, 64, new SceneRenderOptions { MaxDegreeOfParallelism = 1 }, source.Token));

        // Assert
        Assert.Equal(source.Token, ex.CancellationToken);
    }

    /// <summary>
    ///     Validates that Render honors a pre-cancelled token before doing any work, even for a scene whose
    ///     processing would otherwise fail.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_PreCancelledToken_ThrowsBeforeProcessingScene()
    {
        // Arrange: a scene whose transform overflows when applied, so any processing would throw a different exception
        using var source = new CancellationTokenSource();
        source.Cancel();
        var scene = CreateOverflowingScene();

        // Act
        var ex = Assert.ThrowsAny<OperationCanceledException>(() => SceneRenderer.Render(scene, TopDown, 16, 16, null, source.Token));

        // Assert
        Assert.Equal(source.Token, ex.CancellationToken);
    }

    /// <summary>
    ///     Validates that RenderFitted honors a pre-cancelled token before computing the fitted scene bounds.
    /// </summary>
    [Fact]
    public void SceneRenderer_RenderFitted_PreCancelledToken_ThrowsBeforeComputingBounds()
    {
        // Arrange: a scene whose transform overflows when bounds are computed, so any bounds work would throw a different exception
        using var source = new CancellationTokenSource();
        source.Cancel();
        var scene = CreateOverflowingScene();

        // Act
        var ex = Assert.ThrowsAny<OperationCanceledException>(() => SceneRenderer.RenderFitted(scene, 16, 16, null, source.Token));

        // Assert
        Assert.Equal(source.Token, ex.CancellationToken);
    }

    /// <summary>
    ///     Validates that cancellation on the parallel path surfaces as OperationCanceledException, not AggregateException.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_PreCancelledToken_Parallel_ThrowsOperationCanceledNotAggregate()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var scene = Render3DTestHelpers.CreateMixedScene();

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => SceneRenderer.RenderFitted(scene, 64, 64, new SceneRenderOptions { MaxDegreeOfParallelism = 4 }, source.Token));

        // Assert
        Assert.IsNotType<AggregateException>(ex);
        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(ex);
        Assert.Equal(source.Token, cancelled.CancellationToken);
    }

    /// <summary>
    ///     Validates that cancelling while a large render is running stops it with OperationCanceledException.
    /// </summary>
    [Fact]
    public async Task SceneRenderer_Render_CancelledDuringRender_ThrowsOperationCanceledException()
    {
        // Arrange: a render large enough to still be running when it is cancelled
        using var source = new CancellationTokenSource();
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Sphere(1.0, 120)));
        var token = source.Token;
        var task = Task.Run(() => SceneRenderer.RenderFitted(scene, 4096, 4096, new SceneRenderOptions { Supersampling = 4 }, token), CancellationToken.None);

        // Act
        await Task.Delay(30, CancellationToken.None);
        await source.CancelAsync();

        // Assert
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => task);
        Assert.IsNotType<AggregateException>(ex);
        Assert.IsAssignableFrom<OperationCanceledException>(ex);
    }

    /// <summary>
    ///     Validates that a cube one million units away renders identically to the same cube at the origin (orthographic and perspective).
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CubeOneMillionUnitsAway_IdenticalToCubeAtOrigin()
    {
        // Arrange: the same rotated-free cube and axis-aligned camera at the origin, 1e6 away via mesh vertices, and 1e6 away via a node transform
        const double far = 1e6;
        var near = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, -1), new Vector3D(2, 2, 2));
        var moved = Render3DTestHelpers.CreateBoxMesh(new Vector3D(far - 1, -1, -1), new Vector3D(2, 2, 2));
        var originScene = SceneOf(near);
        var farScene = SceneOf(moved);
        var nodeScene = new Scene(new MeshNode(near) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(far, 0, 0)) });
        var options = new SceneRenderOptions { Supersampling = 2 };

        var originPerspective = Camera.LookAt(new Vector3D(0, -10, 0), Vector3D.Zero, Vector3D.UnitZ);
        var farPerspective = Camera.LookAt(new Vector3D(far, -10, 0), new Vector3D(far, 0, 0), Vector3D.UnitZ);
        var originOrtho = Render3DTestHelpers.OrthoCamera(new Vector3D(0, -10, 0), Vector3D.Zero, Vector3D.UnitZ, 6.0);
        var farOrtho = Render3DTestHelpers.OrthoCamera(new Vector3D(far, -10, 0), new Vector3D(far, 0, 0), Vector3D.UnitZ, 6.0);

        // Act
        using var reference = SceneRenderer.Render(originScene, originPerspective, 80, 60, options, TestContext.Current.CancellationToken);
        using var perspectiveVertices = SceneRenderer.Render(farScene, farPerspective, 80, 60, options, TestContext.Current.CancellationToken);
        using var perspectiveNode = SceneRenderer.Render(nodeScene, farPerspective, 80, 60, options, TestContext.Current.CancellationToken);
        using var orthoReference = SceneRenderer.Render(originScene, originOrtho, 80, 60, options, TestContext.Current.CancellationToken);
        using var orthoFar = SceneRenderer.Render(farScene, farOrtho, 80, 60, options, TestContext.Current.CancellationToken);

        // Assert: byte-identical (a single-precision pipeline cannot pass: one float ULP at 1e6 is 0.06)
        Render3DTestHelpers.AssertSurfacesIdentical(reference, perspectiveVertices);
        Render3DTestHelpers.AssertSurfacesIdentical(reference, perspectiveNode);
        Render3DTestHelpers.AssertSurfacesIdentical(orthoReference, orthoFar);
        Assert.True(Render3DTestHelpers.DistinctColors(reference).Count > 1);
    }

    /// <summary>
    ///     Validates that a cube mirrored through its own centre plane renders identically to the original.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_MirroredSymmetricCube_IdenticalToOriginal()
    {
        // Arrange: a cube centred on the origin is symmetric under an X mirror; without the winding flip every face would be a back face
        var cube = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, -1), new Vector3D(2, 2, 2));
        var original = SceneOf(cube);
        var mirrored = new Scene(new MeshNode(cube) { Transform = Matrix4x4D.CreateMirror(Vector3D.UnitX) });
        var camera = Camera.FitToBounds(original.ComputeBounds(), 35.0, 30.0, 1.0);

        // Act
        using var a = SceneRenderer.Render(original, camera, 100, 100, null, TestContext.Current.CancellationToken);
        using var b = SceneRenderer.Render(mirrored, camera, 100, 100, null, TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(a, b);
    }

    /// <summary>
    ///     Validates that a negative scale inside a group still produces front-facing (not back-face coloured) surfaces.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_NegativeScaleInGroup_FacesAreFrontFacing()
    {
        // Arrange: unmistakable colors; flat lighting keeps them exact
        var green = new Rgba32(10, 200, 10, 255);
        var magenta = new Rgba32(255, 0, 255, 255);
        var cube = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, -1), new Vector3D(2, 2, 2));
        var scene = new Scene(new GroupNode([new MeshNode(cube)]) { Transform = Matrix4x4D.CreateScale(new Vector3D(-1, 1, 1)) });
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 30.0, 1.0);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 80, 80, Render3DTestHelpers.FlatOptions(1, defaultColor: green, backFace: magenta), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Render3DTestHelpers.CountPixels(surface, green) > 500);
        Assert.Equal(0, Render3DTestHelpers.CountPixels(surface, magenta));
    }

    /// <summary>
    ///     Validates that an open quad seen from behind takes the back-face color.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OpenQuadSeenFromBehind_UsesBackFaceColor()
    {
        // Arrange: a floor quad facing +Z, viewed from below
        var scene = SceneOf(Render3DTestHelpers.CreateFloorQuad(-2, -2, 2, 2));
        var bottom = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, -10), Vector3D.Zero, Vector3D.UnitY, 16.0);
        var back = new Rgba32(200, 70, 70, 255);

        // Act
        using var fromBelow = SceneRenderer.Render(scene, bottom, 32, 32, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);
        using var fromAbove = SceneRenderer.Render(scene, TopDown, 32, 32, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(back, fromBelow[16, 16]);
        Assert.Equal(Gray, fromAbove[16, 16]);
    }

    /// <summary>
    ///     Validates that the nearer of two overlapping surfaces wins independent of scene order.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_NearerSurfaceWinsRegardlessOfOrder()
    {
        // Arrange: a red quad above a blue quad, overlapping partly
        var red = new NodeAppearance(new Rgba32(255, 0, 0, 255));
        var blue = new NodeAppearance(new Rgba32(0, 0, 255, 255));
        var nearQuad = new MeshNode(Render3DTestHelpers.CreateFloorQuad(-4, -4, 1, 1, 2.0)) { Appearance = red };
        var farQuad = new MeshNode(Render3DTestHelpers.CreateFloorQuad(-1, -1, 4, 4, 0.0)) { Appearance = blue };

        // Act
        using var a = SceneRenderer.Render(new Scene(new GroupNode([nearQuad, farQuad])), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);
        using var b = SceneRenderer.Render(new Scene(new GroupNode([farQuad, nearQuad])), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: images match; the overlap (world 0,0 = pixel 32,32) is red; far-only area is blue
        Render3DTestHelpers.AssertSurfacesIdentical(a, b);
        Assert.Equal(new Rgba32(255, 0, 0, 255), a[32, 32]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), a[44, 20]);
    }

    /// <summary>
    ///     Validates that coplanar quads resolve by traversal order (earlier node wins).
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CoplanarQuads_EarlierNodeWins()
    {
        // Arrange
        var red = new Rgba32(255, 0, 0, 255);
        var blue = new Rgba32(0, 0, 255, 255);
        var first = new MeshNode(Render3DTestHelpers.CreateFloorQuad(-4, -4, 4, 4)) { Appearance = new NodeAppearance(red) };
        var second = new MeshNode(Render3DTestHelpers.CreateFloorQuad(-2, -6, 6, 2)) { Appearance = new NodeAppearance(blue) };

        // Act
        using var redFirst = SceneRenderer.Render(new Scene(new GroupNode([first, second])), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);
        using var blueFirst = SceneRenderer.Render(new Scene(new GroupNode([second, first])), TopDown, 64, 64, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: inside the overlap (world 0,0) the earlier node's color shows, deterministically and without speckle
        Assert.Equal(red, redFirst[32, 32]);
        Assert.Equal(blue, blueFirst[32, 32]);
        Assert.Equal(0, Render3DTestHelpers.CountPixels(redFirst, new Rgba32(0, 0, 0, 255)));
        var overlapColors = new HashSet<Rgba32>();
        for (var y = 34; y < 48; y++)
        {
            for (var x = 24; x < 38; x++)
            {
                overlapColors.Add(redFirst[x, y]);
            }
        }

        Assert.Single(overlapColors);
    }

    /// <summary>
    ///     Validates that a camera inside a closed box sees only back faces (near-plane clipping of the walls behind and around it).
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CameraInsideClosedBox_ShowsBackFaceColorEverywhere()
    {
        // Arrange: a 10-unit box around the eye at the origin
        var box = PrimitiveTessellator.Box(new Vector3D(10, 10, 10), center: true);
        var camera = Camera.LookAt(Vector3D.Zero, new Vector3D(0, 1, 0), Vector3D.UnitZ);
        var back = new Rgba32(200, 70, 70, 255);

        // Act
        using var surface = SceneRenderer.Render(SceneOf(box), camera, 48, 48, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(48 * 48, Render3DTestHelpers.CountPixels(surface, back));
    }

    /// <summary>
    ///     Validates that a user-specified far plane discards more distant surfaces.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_FarPlaneBelowSurface_DiscardsSurface()
    {
        // Arrange: a quad 10 units from the eye, with the far plane at 5 and at 20
        var scene = SceneOf(Render3DTestHelpers.CreateFloorQuad(-50, -50, 50, 50));
        var cut = new Camera { Eye = new Vector3D(0, 0, 10), Target = Vector3D.Zero, Up = Vector3D.UnitY, FarPlane = 5.0 };
        var kept = new Camera { Eye = new Vector3D(0, 0, 10), Target = Vector3D.Zero, Up = Vector3D.UnitY, FarPlane = 20.0 };

        // Act
        using var discarded = SceneRenderer.Render(scene, cut, 16, 16, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);
        using var visible = SceneRenderer.Render(scene, kept, 16, 16, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(16 * 16, Render3DTestHelpers.CountPixels(discarded, White));
        Assert.Equal(16 * 16, Render3DTestHelpers.CountPixels(visible, Gray));
    }

    /// <summary>
    ///     Validates that a user-specified near plane clips nearer geometry.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_NearPlaneBeyondSurface_ClipsSurface()
    {
        // Arrange: the quad is 10 units away; a near plane at 12 removes it
        var scene = SceneOf(Render3DTestHelpers.CreateFloorQuad(-50, -50, 50, 50));
        var camera = new Camera { Eye = new Vector3D(0, 0, 10), Target = Vector3D.Zero, Up = Vector3D.UnitY, NearPlane = 12.0, FarPlane = 30.0 };

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 16, 16, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(16 * 16, Render3DTestHelpers.CountPixels(surface, White));
    }

    /// <summary>
    ///     Validates that RenderFitted frames an object inside the image with a margin and uses the options' colors.
    /// </summary>
    [Fact]
    public void SceneRenderer_RenderFitted_Cube_IsCenteredAndInsideFrame()
    {
        // Arrange
        var scene = SceneOf(PrimitiveTessellator.Box(new Vector3D(2, 2, 2), center: true));

        // Act
        using var surface = SceneRenderer.RenderFitted(scene, 120, 120, new SceneRenderOptions { Supersampling = 1 }, TestContext.Current.CancellationToken);

        // Assert: the silhouette does not touch the border and is roughly centred (perspective makes the near corners larger, so allow a few pixels)
        var bounds = Render3DTestHelpers.BoundsOfNon(surface, White);
        Assert.True(bounds.MinX > 0 && bounds.MinY > 0 && bounds.MaxX < 119 && bounds.MaxY < 119);
        Assert.InRange(((bounds.MinX + bounds.MaxX) / 2.0) - 59.5, -8.0, 8.0);
        Assert.InRange(((bounds.MinY + bounds.MaxY) / 2.0) - 59.5, -8.0, 8.0);
    }
}
