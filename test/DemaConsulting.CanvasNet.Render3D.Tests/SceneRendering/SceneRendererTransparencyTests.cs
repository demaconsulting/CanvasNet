// cspell:ignore subtrahend transmittance

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Acceptance tests of <see cref="SceneRenderer"/> for transparency (SceneRenderer unit): the alpha of
///     <see cref="NodeAppearance"/> colors, <see cref="OverlayNode"/> bodies, deterministic layer compositing
///     over the background, the unchanged opaque paths, operand validation of translucent scenes,
///     the fragment budget, determinism and cancellation.
/// </summary>
public class SceneRendererTransparencyTests
{
    /// <summary>The opaque white default background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>A fully transparent background.</summary>
    private static readonly Rgba32 Clear = new(0, 0, 0, 0);

    /// <summary>A half-translucent red.</summary>
    private static readonly Rgba32 TranslucentRed = new(200, 40, 40, 128);

    /// <summary>An opaque blue.</summary>
    private static readonly Rgba32 Blue = new(30, 60, 220, 255);

    /// <summary>An opaque green.</summary>
    private static readonly Rgba32 Green = new(40, 200, 70, 255);

    /// <summary>A translucent pink used for overlays.</summary>
    private static readonly Rgba32 Pink = new(255, 64, 128, 110);

    /// <summary>An unmistakable back-face color that no test surface uses.</summary>
    private static readonly Rgba32 Magenta = new(250, 0, 250, 255);

    /// <summary>
    ///     Creates flat (ambient-only) options so surface colors can be asserted exactly.
    /// </summary>
    /// <param name="supersampling">The supersampling factor.</param>
    /// <param name="maxDegreeOfParallelism">The tile parallelism.</param>
    /// <param name="background">The background color, or <see langword="null"/> for opaque white.</param>
    /// <param name="maxFragments">The fragment budget, or <see langword="null"/> for the default.</param>
    /// <returns>The options.</returns>
    private static SceneRenderOptions Flat(int supersampling = 1, int maxDegreeOfParallelism = -1, Rgba32? background = null, long? maxFragments = null) =>
        new()
        {
            Lights = [new AmbientLight { Intensity = 1.0f }],
            Supersampling = supersampling,
            BackFaceColor = Magenta,
            BackgroundColor = background ?? White,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            MaxFragmentsPerTile = maxFragments ?? SceneRenderOptions.DefaultMaxFragmentsPerTile,
        };

    /// <summary>
    ///     Renders with the test's cancellation token.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="options">The options.</param>
    /// <returns>The surface (to be disposed by the caller).</returns>
    private static Surface Render(Scene scene, Camera camera, int width, int height, SceneRenderOptions options) =>
        SceneRenderer.Render(scene, camera, width, height, options, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Creates a closed box node.
    /// </summary>
    /// <param name="min">The minimum corner.</param>
    /// <param name="max">The maximum corner.</param>
    /// <param name="color">The appearance color.</param>
    /// <returns>The mesh node.</returns>
    private static MeshNode Box((double X, double Y, double Z) min, (double X, double Y, double Z) max, Rgba32 color) =>
        Render3DTestHelpers.ColoredBox(new Vector3D(min.X, min.Y, min.Z), new Vector3D(max.X - min.X, max.Y - min.Y, max.Z - min.Z), color);

    /// <summary>
    ///     Reads the pixel of a top-down grid render that covers the world point <c>(x, y)</c>.
    /// </summary>
    /// <param name="surface">The surface rendered with <see cref="Render3DTestHelpers.BoxGridCamera"/> at one pixel per unit.</param>
    /// <param name="x">The world X.</param>
    /// <param name="y">The world Y.</param>
    /// <returns>The pixel.</returns>
    private static Rgba32 At(Surface surface, double x, double y) => surface[(int)Math.Floor(x), surface.Height - 1 - (int)Math.Floor(y)];

    /// <summary>
    ///     Composites straight-alpha layers, nearest first, over a background with the renderer's integer arithmetic.
    /// </summary>
    /// <param name="background">The background.</param>
    /// <param name="layers">The layers, nearest first.</param>
    /// <returns>The composite.</returns>
    private static Rgba32 Composite(Rgba32 background, params Rgba32[] layers)
    {
        var compositor = default(LayerCompositor);
        foreach (var layer in layers)
        {
            compositor.Add(layer);
        }

        return compositor.Finish(background);
    }

    /// <summary>
    ///     Finds the smallest fragment budget with which a scene renders (bisection on one sequential worker).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The smallest sufficient budget.</returns>
    private static long MinimalBudget(Scene scene, Camera camera, int width, int height)
    {
        long low = 1;
        long high = 1L << 22;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            try
            {
                using var attempt = Render(scene, camera, width, height, Flat(1, 1, null, middle));
                high = middle;
            }
            catch (RenderBudgetExceededException)
            {
                low = middle + 1;
            }
        }

        return low;
    }

    /// <summary>
    ///     Validates that a translucent box shows the two-layer composite (front and back wall) over the background.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TranslucentBox_BlendsAppearanceColorWithBackground()
    {
        // Arrange
        var scene = new Scene(Box((2, 2, 0), (8, 8, 2), TranslucentRed));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert: 128 + 64 of the box color and the remaining 63 of the white background
        Assert.Equal(new Rgba32(214, 93, 93, 255), At(surface, 5.5, 5.5));
        Assert.Equal(Composite(White, TranslucentRed, TranslucentRed), At(surface, 5.5, 5.5));
        Assert.Equal(White, At(surface, 0.5, 0.5));
    }

    /// <summary>
    ///     Validates that a translucent box above an opaque box blends with it and the opaque box stays exact elsewhere.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TranslucentBoxOverOpaqueBox_ShowsBlend()
    {
        // Arrange: an opaque floor box and, above it, a separate translucent box (one union)
        var scene = new Scene(new GroupNode([Box((0, 0, 0), (10, 10, 1), Blue), Box((2, 2, 2), (8, 8, 3), TranslucentRed)]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert
        Assert.Equal(new Rgba32(158, 45, 84, 255), At(surface, 5.5, 5.5));
        Assert.Equal(Composite(White, TranslucentRed, TranslucentRed, Blue), At(surface, 5.5, 5.5));
        Assert.Equal(Blue, At(surface, 0.5, 0.5));
    }

    /// <summary>
    ///     Validates that, in an oblique view, every ray through a translucent box passes the front and the back wall.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TranslucentBox_ShowsBackWallThroughFront()
    {
        // Arrange
        var scene = new Scene(Box((0, 0, 0), (4, 4, 4), TranslucentRed));
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 30.0, 1.0);

        // Act
        using var surface = Render(scene, camera, 64, 64, Flat());

        // Assert: apart from silhouette samples the image holds only the background and the two-layer composite
        var twoLayers = Composite(White, TranslucentRed, TranslucentRed);
        Assert.NotEqual(Composite(White, TranslucentRed), twoLayers);
        Assert.True(Render3DTestHelpers.CountPixels(surface, twoLayers) > 500);
        Assert.True(Render3DTestHelpers.DistinctColors(surface).Count <= 4);
        Assert.Equal(0, Render3DTestHelpers.CountPixels(surface, Composite(White, TranslucentRed)));
    }

    /// <summary>
    ///     Validates that overlapping translucent solids of one union merge: the interior faces are not drawn.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TranslucentUnion_MergesOverlappingSolids_NoInteriorFaces()
    {
        // Arrange: two overlapping boxes of the same translucent color
        var scene = new Scene(new GroupNode([Box((0, 0, 0), (6, 6, 2), TranslucentRed), Box((3, 3, 1), (9, 9, 3), TranslucentRed)]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert: the overlap and both single-box regions show the same two-layer composite
        var expected = Composite(White, TranslucentRed, TranslucentRed);
        Assert.Equal(expected, At(surface, 4.5, 4.5));
        Assert.Equal(expected, At(surface, 1.5, 1.5));
        Assert.Equal(expected, At(surface, 7.5, 7.5));
    }

    /// <summary>
    ///     Validates that the cut surface of a translucent difference uses the subtrahend's own color and alpha.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TranslucentDifference_ShowsCutSurfaceWithSubtrahendColor()
    {
        // Arrange: a translucent red slab with a translucent blue pocket cut from its top
        var blue = new Rgba32(Blue.R, Blue.G, Blue.B, 128);
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [Box((0, 0, 0), (10, 10, 4), TranslucentRed), Box((3, 3, 1), (7, 7, 5), blue)]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert: the pocket shows its floor (the blue cut surface) and then the slab's bottom; elsewhere top and bottom of the slab
        Assert.Equal(Composite(White, blue, TranslucentRed), At(surface, 5.5, 5.5));
        Assert.Equal(Composite(White, TranslucentRed, TranslucentRed), At(surface, 1.5, 1.5));
    }

    /// <summary>
    ///     Validates that an alpha-0 appearance is invisible but its body still takes part in the boolean operation.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_AlphaZeroAppearance_IsInvisibleButSolid()
    {
        // Arrange: a translucent slab with a pocket cut by an alpha-0 box
        var invisible = new Rgba32(0, 255, 0, 0);
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [Box((0, 0, 0), (10, 10, 4), TranslucentRed), Box((3, 3, 1), (7, 7, 5), invisible)]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert: the pocket is still cut (one layer instead of two), and the cut surface adds no color
        Assert.Equal(Composite(White, TranslucentRed), At(surface, 5.5, 5.5));
        Assert.Equal(Composite(White, TranslucentRed, TranslucentRed), At(surface, 1.5, 1.5));
        Assert.NotEqual(Composite(White, TranslucentRed), Composite(White, TranslucentRed, TranslucentRed));
    }

    /// <summary>
    ///     Validates that an overlay in front of an opaque body tints it.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OverlayInFrontOfOpaque_Tints()
    {
        // Arrange: an opaque floor box and a translucent overlay box above it
        var scene = new Scene(new GroupNode([Box((0, 0, 0), (10, 10, 2), Blue), new OverlayNode(Pink, [Box((2, 2, 3), (8, 8, 5), Green)])]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert: the overlay color replaces the green appearance; its two walls are in front of the blue top
        Assert.Equal(Composite(White, Pink, Pink, Blue), At(surface, 5.5, 5.5));
        Assert.Equal(Blue, At(surface, 0.5, 0.5));
    }

    /// <summary>
    ///     Validates that an overlay behind an opaque body is hidden.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OverlayBehindOpaque_Hidden()
    {
        // Arrange: the overlay box lies below the opaque box
        var scene = new Scene(new GroupNode([Box((0, 0, 4), (10, 10, 6), Blue), new OverlayNode(Pink, [Box((2, 2, 0), (8, 8, 2), Green)])]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert
        Assert.Equal(Blue, At(surface, 5.5, 5.5));
        Assert.Equal(100, Render3DTestHelpers.CountPixels(surface, Blue));
    }

    /// <summary>
    ///     Validates that an overlay inside a difference is neither subtracted nor intersected.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OverlayDoesNotParticipateInEnclosingDifference()
    {
        // Arrange: a translucent slab minus an overlay box that lies inside it
        var slab = new Rgba32(Green.R, Green.G, Green.B, 128);
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [Box((0, 0, 0), (10, 10, 4), slab), new OverlayNode(Pink, [Box((3, 3, 1), (7, 7, 3), Blue)])]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert: slab top, overlay top and bottom, slab bottom: nothing is cut out
        Assert.Equal(Composite(White, slab, Pink, Pink, slab), At(surface, 5.5, 5.5));
        Assert.Equal(Composite(White, slab, slab), At(surface, 1.5, 1.5));
    }

    /// <summary>
    ///     Validates the highlight pattern: a subtrahend that is also drawn as an overlay shows through the cut.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_HighlightPattern_SubtractedBodyShowsAsOverlayThroughCut()
    {
        // Arrange: an opaque slab minus a cutter that is shared by the subtraction and an overlay
        var cutter = Box((3, 3, 1), (7, 7, 5), Green);
        var highlighted = new Scene(new CsgNode(CsgOperation.Difference, [Box((0, 0, 0), (10, 10, 4), Blue), new GroupNode([cutter, new OverlayNode(Pink, [cutter])])]));
        var plain = new Scene(new CsgNode(CsgOperation.Difference, [Box((0, 0, 0), (10, 10, 4), Blue), cutter]));
        var camera = Render3DTestHelpers.BoxGridCamera(10, 10);

        // Act
        using var withOverlay = Render(highlighted, camera, 10, 10, Flat());
        using var without = Render(plain, camera, 10, 10, Flat());

        // Assert: the cutter still cuts a pocket (green floor), and the pink body is drawn in front of it
        Assert.Equal(Green, At(without, 5.5, 5.5));
        Assert.Equal(Composite(White, Pink, Pink, Green), At(withOverlay, 5.5, 5.5));
        Assert.Equal(Blue, At(withOverlay, 1.5, 1.5));
        Assert.Equal(Blue, At(without, 1.5, 1.5));
    }

    /// <summary>
    ///     Validates that an opaque overlay is an independent opaque body.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OverlayOpaqueColor_ActsAsIndependentBody()
    {
        // Arrange: a blue box and an opaque red overlay box that pokes into it
        var red = new Rgba32(220, 40, 40, 255);
        var scene = new Scene(new GroupNode([Box((0, 0, 0), (10, 10, 3), Blue), new OverlayNode(red, [Box((3, 3, 2), (7, 7, 6), Green)])]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat());

        // Assert
        Assert.Equal(red, At(surface, 5.5, 5.5));
        Assert.Equal(Blue, At(surface, 1.5, 1.5));
        Assert.Equal(16, Render3DTestHelpers.CountPixels(surface, red));
    }

    /// <summary>
    ///     Validates that over a transparent background the pixel keeps the layer color with the accumulated alpha.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TransparentBackground_ResolvesToPartialAlphaWithLayerColor()
    {
        // Arrange
        var scene = new Scene(Box((2, 2, 0), (8, 8, 2), TranslucentRed));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat(1, -1, Clear));

        // Assert
        Assert.Equal(new Rgba32(200, 40, 40, 192), At(surface, 5.5, 5.5));
        Assert.Equal(Clear, At(surface, 0.5, 0.5));
    }

    /// <summary>
    ///     Validates that the silhouette of a translucent body is antialiased with intermediate blends.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TranslucentEdges_AreAntialiased()
    {
        // Arrange: a box whose edges fall inside pixels
        var scene = new Scene(Box((2.3, 2.3, 0), (7.6, 7.6, 2), TranslucentRed));
        var interior = Composite(White, TranslucentRed, TranslucentRed);

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, Flat(3));

        // Assert: the interior is exact, edge pixels lie strictly between the background and the interior
        Assert.Equal(interior, At(surface, 5.5, 5.5));
        var edge = At(surface, 2.5, 5.5);
        Assert.NotEqual(White, edge);
        Assert.NotEqual(interior, edge);
        Assert.InRange(edge.G, interior.G, White.G);
        Assert.Equal(255, edge.A);
    }

    /// <summary>
    ///     Validates that an opaque union-only scene keeps the z-buffer path (the fragment budget is never consulted)
    ///     and agrees with the fragment-list path.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OpaqueUnionScene_StillUsesZBufferAndIsByteIdenticalToFragmentPath()
    {
        // Arrange: a budget of one fragment would fail any fragment-list render
        var scene = new Scene(new GroupNode([Box((0, 0, 0), (10, 10, 1), Blue), Box((2, 2, 2), (8, 8, 3), Green), Box((4, 4, 4), (6, 6, 5), new Rgba32(220, 40, 40, 255))]));
        var camera = Render3DTestHelpers.BoxGridCamera(10, 10);

        // Act
        using var zBuffer = Render(scene, camera, 10, 10, Flat(1, -1, null, 1));
        using var fragments = SceneRenderer.RenderWithFragmentPath(scene, camera, 10, 10, Flat(), TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(zBuffer, fragments);
        Assert.Equal(new Rgba32(220, 40, 40, 255), At(zBuffer, 5.5, 5.5));
    }

    /// <summary>
    ///     Validates that an opaque CSG scene renders exactly its analytic pure colors, unchanged by transparency support.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OpaqueCsgScene_IsUnchangedByTransparencySupport()
    {
        // Arrange: a blue slab with a green pocket, on a 12 x 12 grid
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [Box((1, 1, 0), (11, 11, 4), Blue), Box((4, 4, 1), (8, 8, 5), Green)]));

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(12, 12), 12, 12, Flat());

        // Assert: only the three pure colors, no alpha below 255, exact counts
        Assert.Equal(84, Render3DTestHelpers.CountPixels(surface, Blue));
        Assert.Equal(16, Render3DTestHelpers.CountPixels(surface, Green));
        Assert.Equal(44, Render3DTestHelpers.CountPixels(surface, White));
        Assert.Equal(3, Render3DTestHelpers.DistinctColors(surface).Count);
    }

    /// <summary>
    ///     Validates that the alpha of the default and back-face colors is ignored.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_DefaultColorAndBackFaceColorAlpha_AreStillIgnored()
    {
        // Arrange: appearance-free CSG scene viewed from outside and from inside the minuend
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Render3DTestHelpers.CreateBoxMesh(new Vector3D(-5, -5, -5), new Vector3D(10, 10, 10))), new MeshNode(Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, 2), new Vector3D(2, 2, 6)))]));
        var outside = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 30.0, 1.0);
        var inside = Camera.LookAt(Vector3D.Zero, new Vector3D(1, 0.2, -1), Vector3D.UnitZ);
        var translucent = new SceneRenderOptions { Supersampling = 1, DefaultColor = new Rgba32(200, 100, 50, 40), BackFaceColor = new Rgba32(180, 20, 20, 30) };
        var opaque = new SceneRenderOptions { Supersampling = 1, DefaultColor = new Rgba32(200, 100, 50, 255), BackFaceColor = new Rgba32(180, 20, 20, 255) };

        foreach (var camera in new[] { outside, inside })
        {
            // Act
            using var withAlpha = Render(scene, camera, 48, 48, translucent);
            using var withoutAlpha = Render(scene, camera, 48, 48, opaque);

            // Assert
            Render3DTestHelpers.AssertSurfacesIdentical(withoutAlpha, withAlpha);
            Assert.Equal(48 * 48, Enumerable.Range(0, 48).Sum(y => Enumerable.Range(0, 48).Count(x => withAlpha[x, y].A == 255)));
        }
    }

    /// <summary>
    ///     Builds the scene of the determinism tests: translucent solids, a translucent difference and an overlay.
    /// </summary>
    /// <returns>The scene.</returns>
    private static Scene DeterminismScene()
    {
        var sphere = new MeshNode(PrimitiveTessellator.Sphere(3.0, 40)) { Appearance = new NodeAppearance(new Rgba32(220, 60, 60, 120)) };
        var cube = Render3DTestHelpers.ColoredBox(new Vector3D(-4, -4, -4), new Vector3D(8, 8, 8), new Rgba32(60, 80, 220, 170));
        var cut = new CsgNode(CsgOperation.Difference, [cube, sphere]);
        var ghost = new OverlayNode(new Rgba32(128, 128, 128, 80), [new MeshNode(PrimitiveTessellator.Sphere(2.0, 32)) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(5, 1, 1)) }]);
        var opaque = Render3DTestHelpers.ColoredBox(new Vector3D(-2, -9, -2), new Vector3D(4, 3, 4), Green);
        return new Scene(new GroupNode([cut, ghost, opaque]));
    }

    /// <summary>
    ///     Validates that a translucent scene renders byte-identically sequentially and in parallel.
    /// </summary>
    /// <param name="supersampling">The supersampling factor.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SceneRenderer_Render_TranslucentScene_ParallelAndSequentialAreByteIdentical(int supersampling)
    {
        // Arrange
        var scene = DeterminismScene();
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 40.0, 35.0, 120.0 / 90.0);

        // Act
        using var sequential = Render(scene, camera, 120, 90, new SceneRenderOptions { Supersampling = supersampling, MaxDegreeOfParallelism = 1 });
        using var two = Render(scene, camera, 120, 90, new SceneRenderOptions { Supersampling = supersampling, MaxDegreeOfParallelism = 2 });
        using var all = Render(scene, camera, 120, 90, new SceneRenderOptions { Supersampling = supersampling, MaxDegreeOfParallelism = -1 });

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(sequential, two);
        Render3DTestHelpers.AssertSurfacesIdentical(sequential, all);
        Assert.True(Render3DTestHelpers.DistinctColors(sequential).Count > 10);
    }

    /// <summary>
    ///     Validates that repeated renders of a translucent scene are byte-identical.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TranslucentScene_RepeatedRendersAreByteIdentical()
    {
        // Arrange
        var scene = DeterminismScene();
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 40.0, 35.0, 1.0);
        var options = new SceneRenderOptions { Supersampling = 2 };

        // Act
        using var first = Render(scene, camera, 80, 80, options);
        using var second = Render(scene, camera, 80, 80, options);
        using var third = Render(scene, camera, 80, 80, options);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(first, second);
        Render3DTestHelpers.AssertSurfacesIdentical(first, third);
    }

    /// <summary>
    ///     Validates that a translucent scene without any boolean node is subject to the fragment budget.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">The tile parallelism.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void SceneRenderer_Render_TranslucentScene_ExceedingFragmentBudget_ThrowsRenderBudgetExceeded(int maxDegreeOfParallelism)
    {
        // Arrange: a translucent union-only scene whose every tile needs far more than 10 fragments
        var scene = new Scene(new GroupNode([Box((0, 0, 0), (40, 40, 2), TranslucentRed)]));
        var camera = Render3DTestHelpers.BoxGridCamera(64, 64);
        var options = Flat(1, maxDegreeOfParallelism, null, 10);

        // Act
        var ex = Assert.Throws<RenderBudgetExceededException>(() => Render(scene, camera, 64, 64, options));

        // Assert
        Assert.Equal(10L, ex.MaxFragmentsPerTile);
    }

    /// <summary>
    ///     Validates that overlay fragments are counted toward the budget like any other fragment.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OverlayFragments_CountTowardTheBudget()
    {
        // Arrange: the same translucent box alone and with an identical overlay box
        var camera = Render3DTestHelpers.BoxGridCamera(32, 32);
        var alone = new Scene(Box((0, 0, 0), (32, 32, 2), TranslucentRed));
        var withOverlay = new Scene(new GroupNode([Box((0, 0, 0), (32, 32, 2), TranslucentRed), new OverlayNode(Pink, [Box((0, 0, 0), (32, 32, 2), Green)])]));

        // Act
        var aloneBudget = MinimalBudget(alone, camera, 32, 32);
        var overlayBudget = MinimalBudget(withOverlay, camera, 32, 32);

        // Assert: the overlay's top and bottom faces double the fragments of the tile
        Assert.True(aloneBudget > 1);
        Assert.Equal(2 * aloneBudget, overlayBudget);
        Assert.Throws<RenderBudgetExceededException>(() => Render(withOverlay, camera, 32, 32, Flat(1, 1, null, aloneBudget)));
    }

    /// <summary>
    ///     Validates that an open translucent mesh is reported (and thrown in strict mode) like an open CSG operand.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_TranslucentOpenMesh_IsReportedAndStrictThrows()
    {
        // Arrange
        var open = new MeshNode(Render3DTestHelpers.CreateFloorQuad(0, 0, 10, 10)) { Appearance = new NodeAppearance(TranslucentRed) };
        var scene = new Scene(new GroupNode([open]));
        var camera = Render3DTestHelpers.BoxGridCamera(10, 10);
        var reports = new List<SceneRenderUnsupportedFeature>();
        var tolerant = new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = reports.Add };
        var strict = new SceneRenderOptions { Supersampling = 1, ThrowOnUnsupportedFeature = true };

        // Act
        using var surface = Render(scene, camera, 10, 10, tolerant);
        var ex = Assert.Throws<SceneRenderUnsupportedFeatureException>(() => Render(scene, camera, 10, 10, strict));

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("render3d-csg-open-mesh", report.Feature);
        Assert.Same(open, report.Node);
        Assert.Equal("render3d-csg-open-mesh", ex.Feature);
    }

    /// <summary>
    ///     Validates that an opaque union scene with an open mesh is still never validated.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OpaqueOpenMeshUnion_IsStillNotValidated()
    {
        // Arrange
        var scene = new Scene(new GroupNode([new MeshNode(Render3DTestHelpers.CreateFloorQuad(0, 0, 10, 10)) { Appearance = new NodeAppearance(Blue) }]));
        var reports = new List<SceneRenderUnsupportedFeature>();
        var options = new SceneRenderOptions
        {
            Supersampling = 1,
            Lights = [new AmbientLight { Intensity = 1.0f }],
            UnsupportedFeatureHandler = reports.Add,
            ThrowOnUnsupportedFeature = true,
        };

        // Act
        using var surface = Render(scene, Render3DTestHelpers.BoxGridCamera(10, 10), 10, 10, options);

        // Assert
        Assert.Empty(reports);
        Assert.Equal(100, Render3DTestHelpers.CountPixels(surface, Blue));
    }

    /// <summary>
    ///     Validates that a cancelled token stops a translucent render with <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">The tile parallelism.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void SceneRenderer_Render_TranslucentScene_CancellationThrowsOperationCanceled(int maxDegreeOfParallelism)
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var scene = DeterminismScene();
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 40.0, 35.0, 1.0);
        var options = new SceneRenderOptions { Supersampling = 1, MaxDegreeOfParallelism = maxDegreeOfParallelism };

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => SceneRenderer.Render(scene, camera, 64, 64, options, source.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(ex);
    }

    /// <summary>
    ///     Validates that a camera inside a translucent solid renders the exit surface with the back-face color and the appearance alpha.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CameraInsideTranslucentSolid_RendersWithoutFailure()
    {
        // Arrange: eye at the center of a translucent cube
        var scene = new Scene(Box((-10, -10, -10), (10, 10, 10), new Rgba32(30, 60, 220, 128)));
        var camera = Camera.LookAt(Vector3D.Zero, new Vector3D(0, 0, -1), Vector3D.UnitY);

        // Act
        using var surface = Render(scene, camera, 32, 32, Flat());

        // Assert: every ray leaves through the far wall, shown in the back-face color at the appearance alpha
        var expected = Composite(White, new Rgba32(Magenta.R, Magenta.G, Magenta.B, 128));
        Assert.Equal(32 * 32, Render3DTestHelpers.CountPixels(surface, expected));
    }

    /// <summary>
    ///     Validates that a camera inside an overlay body sees the overlay's exit with the back-face color at the overlay alpha,
    ///     like a camera inside a translucent main solid.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CameraInsideOverlay_ShowsBackFaceColorAtOverlayAlpha()
    {
        // Arrange: eye at the center of an overlay cube
        var scene = new Scene(new OverlayNode(Pink, [Box((-10, -10, -10), (10, 10, 10), Green)]));
        var camera = Camera.LookAt(Vector3D.Zero, new Vector3D(0, 0, -1), Vector3D.UnitY);

        // Act
        using var surface = Render(scene, camera, 32, 32, Flat());

        // Assert: every ray leaves the overlay through a wall: the back-face color at the overlay alpha over the background
        var expected = Composite(White, new Rgba32(Magenta.R, Magenta.G, Magenta.B, Pink.A));
        Assert.Equal(32 * 32, Render3DTestHelpers.CountPixels(surface, expected));
    }

    /// <summary>
    ///     Validates that a camera inside an overlay that lies inside a translucent solid composites both exits, the overlay first.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CameraInsideOverlayInsideTranslucentSolid_CompositesBothExits()
    {
        // Arrange: eye at the center of a translucent cube that contains an overlay cube
        var alpha = new Rgba32(30, 60, 220, 128);
        var scene = new Scene(new GroupNode([Box((-10, -10, -10), (10, 10, 10), alpha), new OverlayNode(Pink, [Box((-5, -5, -5), (5, 5, 5), Green)])]));
        var camera = Camera.LookAt(Vector3D.Zero, new Vector3D(0, 0, -1), Vector3D.UnitY);

        // Act
        using var surface = Render(scene, camera, 32, 32, Flat());

        // Assert: the overlay exit (first) and then the main exit, each in the back-face color at its own alpha
        var expected = Composite(White, new Rgba32(Magenta.R, Magenta.G, Magenta.B, Pink.A), new Rgba32(Magenta.R, Magenta.G, Magenta.B, alpha.A));
        Assert.Equal(32 * 32, Render3DTestHelpers.CountPixels(surface, expected));
    }

    /// <summary>
    ///     Validates that an overlay entered from outside after the camera has left it keeps the own-color shades (exit shade only for a ray start).
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CameraOutsideOverlay_KeepsOwnColorFarWall()
    {
        // Arrange: eye in front of an overlay cube
        var scene = new Scene(new OverlayNode(Pink, [Box((-10, -10, -10), (10, 10, 10), Green)]));
        var camera = Camera.LookAt(new Vector3D(0, 0, 40), new Vector3D(0, 0, 0), Vector3D.UnitY);

        // Act
        using var surface = Render(scene, camera, 32, 32, Flat());

        // Assert: the center ray sees the near wall and the far wall in the overlay color
        var expected = Composite(White, Pink, Pink);
        Assert.Equal(expected, surface[16, 16]);
    }
}
