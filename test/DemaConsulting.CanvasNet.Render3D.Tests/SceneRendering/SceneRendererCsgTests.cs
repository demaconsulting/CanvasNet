// cspell:ignore Tessellator subtrahend subtrahends

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Acceptance tests of <see cref="SceneRenderer"/> for fragment-list constructive solid geometry
///     (SceneRenderer unit): cavities, intersections, nested operations, coincident faces, a camera
///     inside solid material, the fragment budget, mirrored operands, fast-path equivalence,
///     determinism and a box-CSG analytic oracle.
/// </summary>
public class SceneRendererCsgTests
{
    /// <summary>The opaque white default background.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>A blue surface color (the minuend).</summary>
    private static readonly Rgba32 Blue = new(30, 60, 220, 255);

    /// <summary>A green surface color (the subtrahend).</summary>
    private static readonly Rgba32 Green = new(40, 200, 70, 255);

    /// <summary>A red surface color.</summary>
    private static readonly Rgba32 Red = new(220, 40, 40, 255);

    /// <summary>An unmistakable back-face color that no test surface uses.</summary>
    private static readonly Rgba32 Magenta = new(250, 0, 250, 255);

    /// <summary>
    ///     Renders with the test's cancellation token.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The surface (to be disposed by the caller).</returns>
    private static Surface Render(Scene scene, Camera camera, int width, int height, SceneRenderOptions? options = null) =>
        SceneRenderer.Render(scene, camera, width, height, options, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Renders through the fragment-list path even for a union-only scene.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="camera">The camera.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The surface (to be disposed by the caller).</returns>
    private static Surface RenderForced(Scene scene, Camera camera, int width, int height, SceneRenderOptions? options = null) =>
        SceneRenderer.RenderWithFragmentPath(scene, camera, width, height, options, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Creates flat (ambient-only) options with the unmistakable magenta back-face color.
    /// </summary>
    /// <param name="supersampling">The supersampling factor.</param>
    /// <param name="maxDegreeOfParallelism">The tile parallelism.</param>
    /// <returns>The options.</returns>
    private static SceneRenderOptions Flat(int supersampling = 1, int maxDegreeOfParallelism = -1) =>
        new()
        {
            Lights = [new AmbientLight { Intensity = 1.0f }],
            Supersampling = supersampling,
            BackFaceColor = Magenta,
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
        };

    /// <summary>
    ///     Builds the cube (edge 10, centred) minus a sphere (radius 3) centred on the top face.
    /// </summary>
    /// <param name="operation">The operation applied to the two operands.</param>
    /// <returns>The scene.</returns>
    private static Scene CubeAndTopSphere(CsgOperation operation)
    {
        var cube = Render3DTestHelpers.ColoredBox(new Vector3D(-5, -5, -5), new Vector3D(10, 10, 10), Blue);
        var sphere = new MeshNode(PrimitiveTessellator.Sphere(3.0, 48))
        {
            Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 5)),
            Appearance = new NodeAppearance(Red),
        };
        return new Scene(new CsgNode(operation, [cube, sphere]));
    }

    /// <summary>
    ///     Asserts that a top-down render of a box CSG tree equals the analytic oracle at every pixel.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="width">The grid width.</param>
    /// <param name="height">The grid height.</param>
    /// <param name="maxDegreeOfParallelism">The tile parallelism.</param>
    private static void AssertMatchesOracle(BoxCsgTree tree, int width, int height, int maxDegreeOfParallelism)
    {
        var options = Flat(1, maxDegreeOfParallelism);
        using var surface = Render(new Scene(tree.ToSceneNode()), Render3DTestHelpers.BoxGridCamera(width, height), width, height, options);
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var expected = tree.ExpectedColor(col + 0.5, height - row - 0.5, White);
                Assert.True(expected == surface[col, row], $"Pixel ({col}, {row}): expected {expected}, rendered {surface[col, row]}.");
            }
        }
    }

    /// <summary>
    ///     Validates that cube minus a sphere on its top face shows the cavity in the subtrahend color
    ///     with a footprint matching the tessellated sphere's cross-section.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CubeMinusSphere_ShowsCavityInSubtrahendColor()
    {
        // Arrange: top view, 10 pixels per world unit
        var scene = CubeAndTopSphere(CsgOperation.Difference);
        var camera = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, 50), Vector3D.Zero, Vector3D.UnitY, 14.0);

        // Act
        using var surface = Render(scene, camera, 140, 140, Flat());

        // Assert: the cavity centre is the cut surface, the ring around it is the cube's top face, the margin is background
        Assert.Equal(Red, surface[70, 70]);
        Assert.Equal(Blue, surface[110, 70]);
        Assert.Equal(White, surface[2, 2]);
        var cavity = Render3DTestHelpers.CountPixels(surface, Red);
        var expected = Math.PI * 3.0 * 3.0 * 100.0;
        Assert.InRange(cavity, expected * 0.94, expected * 1.01);
        Assert.Equal(0, Render3DTestHelpers.CountPixels(surface, Magenta));
    }

    /// <summary>
    ///     Validates that the cut surface of a cavity is lit with a flipped normal (differs from the union's bump
    ///     and from the back-face color) in an oblique view.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CubeMinusSphereOblique_CutSurfaceUsesFlippedNormal()
    {
        // Arrange: default lights so shading differs by normal direction
        var difference = CubeAndTopSphere(CsgOperation.Difference);
        var union = CubeAndTopSphere(CsgOperation.Union);
        var camera = Camera.LookAt(new Vector3D(14, -18, 16), new Vector3D(0, 0, 3), Vector3D.UnitZ);
        var options = new SceneRenderOptions { Supersampling = 1, BackFaceColor = Magenta };

        // Act
        using var cut = Render(difference, camera, 160, 160, options);
        using var bump = Render(union, camera, 160, 160, options);

        // Assert: no back-face colored pixel anywhere, and the cavity differs from the union with the same sphere
        Assert.Equal(0, Render3DTestHelpers.CountPixels(cut, Magenta));
        var differing = 0;
        for (var y = 0; y < 160; y++)
        {
            for (var x = 0; x < 160; x++)
            {
                if (cut[x, y] != bump[x, y])
                {
                    differing++;
                }
            }
        }

        Assert.True(differing > 200, $"Only {differing} pixels differ between the cavity and the union.");
    }

    /// <summary>
    ///     Validates that cube intersected with a sphere has the silhouette of the square clipped by the disc.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CubeIntersectSphere_SilhouetteIsSquareClippedByDisc()
    {
        // Arrange: a sphere (radius 6) larger than the half cube (5) but smaller than its half diagonal
        var cube = Render3DTestHelpers.ColoredBox(new Vector3D(-5, -5, -5), new Vector3D(10, 10, 10), Blue);
        var sphere = new MeshNode(PrimitiveTessellator.Sphere(6.0, 64)) { Appearance = new NodeAppearance(Red) };
        var scene = new Scene(new CsgNode(CsgOperation.Intersection, [cube, sphere]));
        var camera = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, 50), Vector3D.Zero, Vector3D.UnitY, 14.0);

        // Act
        using var surface = Render(scene, camera, 140, 140, Flat());

        // Assert: probes in both solids, in the cube only (corner) and the analytic area of square-intersect-disc
        Assert.Equal(Blue, surface[70, 70]);
        Assert.Equal(Red, surface[115, 70]);
        Assert.Equal(White, surface[118, 22]);
        var area = 0.0;
        const int Steps = 20000;
        for (var i = 0; i < Steps; i++)
        {
            var x = -5.0 + (10.0 * (i + 0.5) / Steps);
            area += 2.0 * Math.Min(5.0, Math.Sqrt(36.0 - (x * x))) * (10.0 / Steps);
        }

        var covered = 140 * 140 - Render3DTestHelpers.CountPixels(surface, White);
        Assert.InRange(covered, area * 100.0 * 0.97, area * 100.0 * 1.01);
    }

    /// <summary>
    ///     Validates a nested difference of differences against the analytic box oracle at every pixel.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_NestedDifferenceOfDifferences_MatchesOracle()
    {
        // Arrange: Difference(Difference(A, B), Difference(C, D)) with pairwise distinct heights
        BoxCsgTree a = new BoxCsgLeaf(2, 2, 1, 22, 20, 9, new Rgba32(10, 20, 200, 255));
        BoxCsgTree b = new BoxCsgLeaf(6, 6, 2, 16, 26, 12, new Rgba32(200, 30, 30, 255));
        BoxCsgTree c = new BoxCsgLeaf(8, 3, 3, 28, 18, 13, new Rgba32(30, 190, 40, 255));
        BoxCsgTree d = new BoxCsgLeaf(12, 8, 4, 24, 14, 10, new Rgba32(200, 190, 30, 255));
        var tree = new BoxCsgOp(
            CsgOperation.Difference,
            [new BoxCsgOp(CsgOperation.Difference, [a, b]), new BoxCsgOp(CsgOperation.Difference, [c, d])]);

        // Act / Assert
        AssertMatchesOracle(tree, 32, 32, 1);
        AssertMatchesOracle(tree, 32, 32, -1);
    }

    /// <summary>
    ///     Validates seeded random box CSG trees (depth up to four, three to eight boxes) against the analytic oracle.
    /// </summary>
    /// <param name="leafCount">The number of boxes in the tree.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public void SceneRenderer_Render_RandomBoxTrees_MatchOracle(int leafCount)
    {
        // Arrange / Act / Assert: a fixed list of seeds keeps the test deterministic
        for (var seed = 1; seed <= 12; seed++)
        {
            var tree = BoxCsgTree.CreateRandom(new Random((leafCount * 1000) + seed), leafCount, 28, 28);
            AssertMatchesOracle(tree, 28, 28, seed % 2 == 0 ? 1 : -1);
        }
    }

    /// <summary>
    ///     Validates that a difference whose faces coincide with the minuend's faces leaves no speckle from
    ///     above, from below and obliquely: only the two surface colors and the background appear.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CoincidentFaceDifference_HasNoSpeckle()
    {
        // Arrange: the box shares the cube's bottom face and four side planes
        var cube = Render3DTestHelpers.ColoredBox(Vector3D.Zero, new Vector3D(10, 10, 10), Blue);
        var box = Render3DTestHelpers.ColoredBox(Vector3D.Zero, new Vector3D(10, 10, 5), Green);
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [cube, box]));
        var target = new Vector3D(5, 5, 3);

        // Act
        using var top = Render(scene, Render3DTestHelpers.OrthoCamera(new Vector3D(5, 5, 50), target, Vector3D.UnitY, 16.0), 64, 64, Flat());
        using var below = Render(scene, Render3DTestHelpers.OrthoCamera(new Vector3D(5, 5, -50), target, Vector3D.UnitY, 16.0), 64, 64, Flat());
        using var obliqueAbove = Render(scene, Camera.LookAt(new Vector3D(18, -14, 15), target, Vector3D.UnitZ), 96, 96, Flat());
        using var obliqueBelow = Render(scene, Camera.LookAt(new Vector3D(18, -14, -12), target, Vector3D.UnitZ), 96, 96, Flat());

        // Assert: exact analytic footprints (the 40 x 40 pixel square) and a color set without surprises
        Assert.Equal(1600, Render3DTestHelpers.CountPixels(top, Blue));
        Assert.Equal(1600, Render3DTestHelpers.CountPixels(below, Green));
        Assert.Equal(64 * 64, Render3DTestHelpers.CountPixels(top, Blue) + Render3DTestHelpers.CountPixels(top, White));
        Assert.Equal(64 * 64, Render3DTestHelpers.CountPixels(below, Green) + Render3DTestHelpers.CountPixels(below, White));
        foreach (var image in new[] { top, below, obliqueAbove, obliqueBelow })
        {
            Assert.Subset(new HashSet<Rgba32> { Blue, Green, White }, Render3DTestHelpers.DistinctColors(image));
            Assert.Equal(0, Render3DTestHelpers.CountIsolatedPixels(image));
        }
    }

    /// <summary>
    ///     Validates the coincident-face difference when the coincidence is only approximate (different but
    ///     mathematically equal transform chains with a 90-degree rotation and decimal offsets).
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_NearlyCoincidentFaceDifference_HasNoSpeckle()
    {
        // Arrange: the same cube built from a unit mesh and from a literal mesh, so coordinates differ by rounding noise
        // (a product of matrices applies its left operand first)
        var placement = Matrix4x4D.CreateRotationZ(90.0) * Matrix4x4D.CreateTranslation(new Vector3D(0.1, 0.2, 0.3));
        var cube = new MeshNode(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One))
        {
            Transform = Matrix4x4D.CreateScale(10.0) * placement,
            Appearance = new NodeAppearance(Blue),
        };
        var box = new MeshNode(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(10, 10, 5)))
        {
            Transform = placement,
            Appearance = new NodeAppearance(Green),
        };
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [cube, box]));
        var target = new Vector3D(-4.9, 5.2, 3.3);

        // Act
        using var top = Render(scene, Render3DTestHelpers.OrthoCamera(new Vector3D(-4.9, 5.2, 50), target, Vector3D.UnitY, 16.0), 64, 64, Flat());
        using var below = Render(scene, Render3DTestHelpers.OrthoCamera(new Vector3D(-4.9, 5.2, -50), target, Vector3D.UnitY, 16.0), 64, 64, Flat());
        using var oblique = Render(scene, Camera.LookAt(new Vector3D(14, -14, 15), target, Vector3D.UnitZ), 96, 96, Flat());
        using var obliqueBelow = Render(scene, Camera.LookAt(new Vector3D(14, -14, -12), target, Vector3D.UnitZ), 96, 96, Flat());

        // Assert
        Assert.InRange(Render3DTestHelpers.CountPixels(top, Blue), 1560, 1640);
        Assert.InRange(Render3DTestHelpers.CountPixels(below, Green), 1560, 1640);
        foreach (var image in new[] { top, below, oblique, obliqueBelow })
        {
            Assert.Subset(new HashSet<Rgba32> { Blue, Green, White }, Render3DTestHelpers.DistinctColors(image));
            Assert.Equal(0, Render3DTestHelpers.CountIsolatedPixels(image));
        }
    }

    /// <summary>
    ///     Validates that nearly coincident faces leave no speckle over many placements (rotations and offsets whose
    ///     chains of rounding differ), from oblique views above and below.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_NearlyCoincidentFacesAcrossPlacements_HaveNoSpeckle()
    {
        for (var k = 0; k < 24; k++)
        {
            // Arrange: placements with irrational-looking offsets, a rotation about Z and a tilted second rotation
            var placement = Matrix4x4D.CreateRotationZ(90.0 * (k % 4)) * Matrix4x4D.CreateRotationX(15.0 * (k % 3)) * Matrix4x4D.CreateTranslation(new Vector3D(0.1 + (0.37 * k), 0.2 + (0.11 * k), 0.3 + (0.23 * k)));
            var cube = new MeshNode(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, Vector3D.One))
            {
                Transform = Matrix4x4D.CreateScale(10.0) * placement,
                Appearance = new NodeAppearance(Blue),
            };
            var box = new MeshNode(Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(10, 10, 5)))
            {
                Transform = placement,
                Appearance = new NodeAppearance(Green),
            };
            var scene = new Scene(new CsgNode(CsgOperation.Difference, [cube, box]));

            foreach (var elevation in new[] { 40.0, -40.0, 12.0 })
            {
                // Act
                var camera = Camera.FitToBounds(scene.ComputeBounds(), 30.0 + (17.0 * k), elevation, 1.0);
                using var image = Render(scene, camera, 80, 80, Flat());

                // Assert
                Assert.Subset(new HashSet<Rgba32> { Blue, Green, White }, Render3DTestHelpers.DistinctColors(image));
                // Faces are speckle-free; a few lone samples may remain along the edge-on silhouette of coincident walls of
                // independently tessellated meshes, whose depths disagree by more than any tolerance there (a documented limitation)
                Assert.True(Render3DTestHelpers.CountIsolatedPixels(image) <= 12, $"Placement {k}, elevation {elevation}: speckle.");
            }
        }
    }

    /// <summary>
    ///     Validates that a perspective camera inside a large cube sees only back-face shading (the viewer is
    ///     inside material), including through a cavity, and matches the plain cube where rays miss the cavity.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_PerspectiveCameraInsideSolid_ShowsBackFaceEverywhere()
    {
        // Arrange: eye at the origin inside a 20-unit cube; a small sphere cavity off to the right and ahead
        var cube = Render3DTestHelpers.ColoredBox(new Vector3D(-10, -10, -10), new Vector3D(20, 20, 20), Blue);
        var cavity = new MeshNode(PrimitiveTessellator.Sphere(2.0, 32))
        {
            Transform = Matrix4x4D.CreateTranslation(new Vector3D(3, 0, -6)),
            Appearance = new NodeAppearance(Green),
        };
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [cube, cavity]));
        var plain = new Scene(cube);
        var camera = Camera.LookAt(Vector3D.Zero, new Vector3D(0, 0, -1), Vector3D.UnitY);

        // Act
        using var flat = Render(scene, camera, 64, 64, Flat());
        var lit = new SceneRenderOptions { Supersampling = 1 };
        using var csgLit = Render(scene, camera, 64, 64, lit);
        using var plainLit = Render(plain, camera, 64, 64, lit);

        // Assert: no background, every pixel is back-face shaded, and the left half (rays that miss the cavity) equals the plain cube
        Assert.Equal(64 * 64, Render3DTestHelpers.CountPixels(flat, Magenta));
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                Assert.True(csgLit[x, y] == plainLit[x, y], $"Pixel ({x}, {y}) differs from the plain cube.");
            }
        }
    }

    /// <summary>
    ///     Validates that an orthographic camera inside a large cube also sees only back-face shading.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_OrthographicCameraInsideSolid_ShowsBackFaceEverywhere()
    {
        // Arrange
        var cube = Render3DTestHelpers.ColoredBox(new Vector3D(-10, -10, -10), new Vector3D(20, 20, 20), Blue);
        var cavity = new MeshNode(PrimitiveTessellator.Sphere(2.0, 32))
        {
            Transform = Matrix4x4D.CreateTranslation(new Vector3D(3, 0, -6)),
            Appearance = new NodeAppearance(Green),
        };
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [cube, cavity]));
        var camera = new Camera
        {
            Eye = Vector3D.Zero,
            Target = new Vector3D(0, 0, -1),
            Up = Vector3D.UnitY,
            Projection = CameraProjection.Orthographic,
            OrthographicHeight = 8.0,
            NearPlane = 0.001,
        };

        // Act
        using var surface = Render(scene, camera, 48, 48, Flat());

        // Assert: an orthographic eye has no position along its rays, so the near plane puts the viewer inside the material
        Assert.Equal(48 * 48, Render3DTestHelpers.CountPixels(surface, Magenta));
    }

    /// <summary>
    ///     Validates that a camera outside the solid (fitted to the bounds) still shows the cavity from the front.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_FittedCameraOutsideSolid_ShowsMinuendAndCavity()
    {
        // Arrange
        var scene = CubeAndTopSphere(CsgOperation.Difference);
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 55.0, 1.0);

        // Act
        using var surface = Render(scene, camera, 120, 120, Flat());

        // Assert: both colors are visible and the viewer is never inside material
        var colors = Render3DTestHelpers.DistinctColors(surface);
        Assert.Contains(Blue, colors);
        Assert.Contains(Red, colors);
        Assert.DoesNotContain(Magenta, colors);
    }

    /// <summary>
    ///     Validates that exceeding the fragment budget throws the same exception for sequential and parallel
    ///     rendering, never wrapped in an <see cref="AggregateException"/>.
    /// </summary>
    /// <param name="maxDegreeOfParallelism">The tile parallelism.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    public void SceneRenderer_Render_BudgetExceeded_ThrowsDeterministicException(int maxDegreeOfParallelism)
    {
        // Arrange: every tile of this image needs far more than 100 fragments
        var scene = CubeAndTopSphere(CsgOperation.Difference);
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 30.0, 1.0);
        var options = new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = 100, MaxDegreeOfParallelism = maxDegreeOfParallelism };
        var reference = Assert.Throws<RenderBudgetExceededException>(() => Render(scene, camera, 128, 128, new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = 100, MaxDegreeOfParallelism = 1 }));

        // Act
        var ex = Assert.Throws<RenderBudgetExceededException>(() => Render(scene, camera, 128, 128, options));

        // Assert: same type, budget and message whatever the scheduling
        Assert.Equal(100L, ex.MaxFragmentsPerTile);
        Assert.Equal(reference.MaxFragmentsPerTile, ex.MaxFragmentsPerTile);
        Assert.Equal(reference.Message, ex.Message);
    }

    /// <summary>
    ///     Validates the exact budget boundary: the smallest budget that renders succeeds (sequentially and in
    ///     parallel) with the same image as the default budget, and one fragment less throws.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_ExactBudgetBoundary_RendersAndOneLessThrows()
    {
        // Arrange
        var scene = CubeAndTopSphere(CsgOperation.Difference);
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 30.0, 1.0);

        // Act: bisect the smallest budget that renders sequentially
        long low = 1;
        long high = SceneRenderOptions.DefaultMaxFragmentsPerTile;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            try
            {
                using var attempt = Render(scene, camera, 96, 96, new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = middle, MaxDegreeOfParallelism = 1 });
                high = middle;
            }
            catch (RenderBudgetExceededException)
            {
                low = middle + 1;
            }
        }

        // Assert
        using var reference = Render(scene, camera, 96, 96, new SceneRenderOptions { Supersampling = 1 });
        using var exact = Render(scene, camera, 96, 96, new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = low, MaxDegreeOfParallelism = 1 });
        using var exactParallel = Render(scene, camera, 96, 96, new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = low, MaxDegreeOfParallelism = 4 });
        Render3DTestHelpers.AssertSurfacesIdentical(reference, exact);
        Render3DTestHelpers.AssertSurfacesIdentical(reference, exactParallel);
        Assert.True(low > 1, "The scene must need more than one fragment per tile.");
        Assert.Throws<RenderBudgetExceededException>(() => Render(scene, camera, 96, 96, new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = low - 1, MaxDegreeOfParallelism = 1 }));
        Assert.Throws<RenderBudgetExceededException>(() => Render(scene, camera, 96, 96, new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = low - 1, MaxDegreeOfParallelism = 4 }));
    }

    /// <summary>
    ///     Validates that cancellation takes precedence over the budget on the parallel path.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_CancelledAndOverBudget_ThrowsOperationCanceled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var scene = CubeAndTopSphere(CsgOperation.Difference);
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 30.0, 1.0);
        var options = new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = 10, MaxDegreeOfParallelism = 4 };

        // Act
        var ex = Assert.ThrowsAny<Exception>(() => SceneRenderer.Render(scene, camera, 128, 128, options, source.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(ex);
    }

    /// <summary>
    ///     Validates that a union-only scene takes the fast path and ignores the fragment budget.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_UnionOnlyScene_IgnoresFragmentBudget()
    {
        // Arrange
        var scene = Render3DTestHelpers.CreateMixedScene();
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 30.0, 1.0);

        // Act: budget 1 would fail any fragment-list render
        using var tight = Render(scene, camera, 96, 96, new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = 1 });
        using var reference = Render(scene, camera, 96, 96, new SceneRenderOptions { Supersampling = 1 });

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(reference, tight);
        Assert.True(Render3DTestHelpers.DistinctColors(tight).Count > 1);
    }

    /// <summary>
    ///     Validates that a mirrored asymmetric operand of a difference renders exactly like the same mesh with the
    ///     mirror baked into its vertices, and never shows a back-face color (negative scales on one, two and
    ///     three axes).
    /// </summary>
    /// <param name="sx">The X scale (plus or minus one).</param>
    /// <param name="sy">The Y scale (plus or minus one).</param>
    /// <param name="sz">The Z scale (plus or minus one).</param>
    [Theory]
    [InlineData(-1.0, 1.0, 1.0)]
    [InlineData(1.0, -1.0, 1.0)]
    [InlineData(1.0, 1.0, -1.0)]
    [InlineData(-1.0, -1.0, 1.0)]
    [InlineData(-1.0, 1.0, -1.0)]
    [InlineData(1.0, -1.0, -1.0)]
    [InlineData(-1.0, -1.0, -1.0)]
    public void SceneRenderer_Render_MirroredCsgOperand_MatchesBakedMirror(double sx, double sy, double sz)
    {
        // Arrange: the operand pokes out of a small cube on several sides, so its cut surfaces are visible
        var scale = Matrix4x4D.CreateScale(new Vector3D(sx, sy, sz));
        var solid = Render3DTestHelpers.CreateAsymmetricSolid();
        var cube = Render3DTestHelpers.ColoredBox(new Vector3D(-1.5, -1.5, -1.5), new Vector3D(3, 3, 3), Blue);
        var mirrored = new Scene(new CsgNode(CsgOperation.Difference, [cube, new MeshNode(solid) { Transform = scale, Appearance = new NodeAppearance(Green) }]));
        var baked = new Scene(new CsgNode(CsgOperation.Difference, [cube, new MeshNode(solid.Transform(scale)) { Appearance = new NodeAppearance(Green) }]));
        var camera = Camera.LookAt(new Vector3D(11, -9, 8), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        using var viaTransform = Render(mirrored, camera, 120, 120, Flat(2));
        using var viaMesh = Render(baked, camera, 120, 120, Flat(2));

        // Assert: identical, both colors present and not a single back-face pixel
        Render3DTestHelpers.AssertSurfacesIdentical(viaMesh, viaTransform);
        var colors = Render3DTestHelpers.DistinctColors(viaTransform);
        Assert.Contains(Green, colors);
        Assert.DoesNotContain(Magenta, colors);
    }

    /// <summary>
    ///     Validates that the mirror on the CSG node itself, on a grandparent group (split over two nodes) and a
    ///     double mirror all match the baked result on the fragment-list path.
    /// </summary>
    /// <param name="sx">The X scale applied by the outer group (plus or minus one).</param>
    /// <param name="sy">The Y scale applied by the CSG node (plus or minus one).</param>
    /// <param name="sz">The Z scale applied by the minuend and subtrahend nodes (plus or minus one).</param>
    [Theory]
    [InlineData(-1.0, 1.0, 1.0)]
    [InlineData(1.0, -1.0, 1.0)]
    [InlineData(1.0, 1.0, -1.0)]
    [InlineData(-1.0, -1.0, 1.0)]
    [InlineData(-1.0, -1.0, -1.0)]
    public void SceneRenderer_Render_MirrorAboveCsgNode_MatchesBakedMirror(double sx, double sy, double sz)
    {
        // Arrange: the combined transform is the product of the three negative scales
        var combined = Matrix4x4D.CreateScale(new Vector3D(sx, sy, sz));
        var solid = Render3DTestHelpers.CreateAsymmetricSolid();
        var cubeMesh = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1.5, -1.5, -1.0), new Vector3D(3, 3, 2));
        var perNode = Matrix4x4D.CreateScale(new Vector3D(1, 1, sz));
        var inner = new CsgNode(
            CsgOperation.Difference,
            [
                new MeshNode(cubeMesh) { Transform = perNode, Appearance = new NodeAppearance(Blue) },
                new MeshNode(solid) { Transform = perNode, Appearance = new NodeAppearance(Green) },
            ])
        {
            Transform = Matrix4x4D.CreateScale(new Vector3D(1, sy, 1)),
        };
        var nested = new Scene(new GroupNode([inner]) { Transform = Matrix4x4D.CreateScale(new Vector3D(sx, 1, 1)) });
        var baked = new Scene(new CsgNode(
            CsgOperation.Difference,
            [
                new MeshNode(cubeMesh.Transform(combined)) { Appearance = new NodeAppearance(Blue) },
                new MeshNode(solid.Transform(combined)) { Appearance = new NodeAppearance(Green) },
            ]));
        var camera = Camera.LookAt(new Vector3D(11, -9, 8), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        using var viaTree = Render(nested, camera, 100, 100, Flat(2));
        using var viaMesh = Render(baked, camera, 100, 100, Flat(2));

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(viaMesh, viaTree);
        Assert.DoesNotContain(Magenta, Render3DTestHelpers.DistinctColors(viaTree));
        Assert.Contains(Green, Render3DTestHelpers.DistinctColors(viaTree));
    }

    /// <summary>
    ///     Validates that a union <see cref="CsgNode"/> renders byte-identically to a <see cref="GroupNode"/>.
    /// </summary>
    [Fact]
    public void SceneRenderer_Render_UnionCsgNode_IdenticalToGroupNode()
    {
        // Arrange
        var group = Render3DTestHelpers.CreateMixedScene();
        var children = ((GroupNode)group.Root).Children;
        var csg = new Scene(new CsgNode(CsgOperation.Union, children));
        var camera = Camera.FitToBounds(group.ComputeBounds(), 35.0, 30.0, 1.0);

        // Act
        using var viaGroup = Render(group, camera, 96, 96);
        using var viaCsg = Render(csg, camera, 96, 96);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(viaGroup, viaCsg);
    }

    /// <summary>
    ///     Validates that forcing the fragment-list path on union-only scenes of closed solids gives the same image
    ///     as the z-buffer fast path (with antialiasing, in parallel and sequentially).
    /// </summary>
    /// <param name="supersampling">The supersampling factor.</param>
    /// <param name="maxDegreeOfParallelism">The tile parallelism.</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, -1)]
    [InlineData(3, 2)]
    public void SceneRenderer_RenderWithFragmentPath_UnionOnlyScenes_IdenticalToZBuffer(int supersampling, int maxDegreeOfParallelism)
    {
        // Arrange: disjoint solids, overlapping solids, and a solid with a mirrored copy
        var overlapping = new Scene(new GroupNode(
        [
            new MeshNode(PrimitiveTessellator.Sphere(2.0, 40)) { Appearance = new NodeAppearance(Red) },
            new MeshNode(PrimitiveTessellator.Sphere(2.0, 40)) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(1.5, 0.5, 0.3)), Appearance = new NodeAppearance(Green) },
            Render3DTestHelpers.ColoredBox(new Vector3D(-1, -1, 0.5), new Vector3D(2, 2, 3), Blue),
        ]));
        var mirrored = new Scene(new GroupNode(
        [
            new MeshNode(Render3DTestHelpers.CreateAsymmetricSolid()) { Appearance = new NodeAppearance(Red) },
            new MeshNode(Render3DTestHelpers.CreateAsymmetricSolid()) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 3)) * Matrix4x4D.CreateMirror(Vector3D.UnitX), Appearance = new NodeAppearance(Green) },
        ]));
        var options = new SceneRenderOptions { Supersampling = supersampling, MaxDegreeOfParallelism = maxDegreeOfParallelism };

        foreach (var scene in new[] { Render3DTestHelpers.CreateMixedScene(), overlapping, mirrored })
        {
            var camera = Camera.FitToBounds(scene.ComputeBounds(), 35.0, 30.0, 1.0);

            // Act
            using var zBuffer = Render(scene, camera, 100, 90, options);
            using var fragments = RenderForced(scene, camera, 100, 90, options);

            // Assert: identical except for at most a couple of grazing silhouette samples, where a front and a back
            // surface of one solid are closer than the depth tie tolerance and cancel as one coincident group
            var differing = 0;
            for (var y = 0; y < 90; y++)
            {
                for (var x = 0; x < 100; x++)
                {
                    if (zBuffer[x, y] != fragments[x, y])
                    {
                        differing++;
                    }
                }
            }

            Assert.True(differing <= 2, $"{differing} pixels differ between the z-buffer and the fragment-list path.");
            Assert.True(Render3DTestHelpers.DistinctColors(zBuffer).Count > 2);
        }
    }

    /// <summary>
    ///     Validates that CSG output is byte-identical across tile parallelism and repeated runs for every
    ///     supersampling factor, for a scene with coincident faces, a mirrored operand and several leaves.
    /// </summary>
    /// <param name="supersampling">The supersampling factor.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SceneRenderer_Render_CsgScene_ParallelAndSequentialAreByteIdentical(int supersampling)
    {
        // Arrange: cube minus a flush box (ties), minus a mirrored asymmetric solid, united with a separate intersected pair
        var cube = Render3DTestHelpers.ColoredBox(Vector3D.Zero, new Vector3D(10, 10, 10), Blue);
        var flush = Render3DTestHelpers.ColoredBox(Vector3D.Zero, new Vector3D(10, 10, 5), Green);
        var mirroredSolid = new MeshNode(Render3DTestHelpers.CreateAsymmetricSolid())
        {
            Transform = Matrix4x4D.CreateMirror(Vector3D.UnitX) * Matrix4x4D.CreateTranslation(new Vector3D(5, 5, 8)),
            Appearance = new NodeAppearance(Red),
        };
        var lens = new CsgNode(
            CsgOperation.Intersection,
            [
                new MeshNode(PrimitiveTessellator.Sphere(3.0, 36)) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(16, 5, 3)) },
                new MeshNode(PrimitiveTessellator.Sphere(3.0, 36)) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(18, 5, 3)), Appearance = new NodeAppearance(Green) },
            ]);
        var scene = new Scene(new GroupNode([new CsgNode(CsgOperation.Difference, [cube, flush, mirroredSolid]), lens]));
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 40.0, 35.0, 120.0 / 90.0);

        // Act
        using var sequential = Render(scene, camera, 120, 90, new SceneRenderOptions { Supersampling = supersampling, MaxDegreeOfParallelism = 1 });
        using var two = Render(scene, camera, 120, 90, new SceneRenderOptions { Supersampling = supersampling, MaxDegreeOfParallelism = 2 });
        using var all = Render(scene, camera, 120, 90, new SceneRenderOptions { Supersampling = supersampling, MaxDegreeOfParallelism = -1 });
        using var again = Render(scene, camera, 120, 90, new SceneRenderOptions { Supersampling = supersampling, MaxDegreeOfParallelism = -1 });

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(sequential, two);
        Render3DTestHelpers.AssertSurfacesIdentical(sequential, all);
        Render3DTestHelpers.AssertSurfacesIdentical(all, again);
        Assert.True(Render3DTestHelpers.DistinctColors(sequential).Count > 3);
    }
}
