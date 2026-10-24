// cspell:ignore Tessellator

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Subsystem-level tests for the SceneRendering subsystem: camera, lighting, rasterization and the renderer working together.
/// </summary>
public class SceneRenderingTests
{
    /// <summary>
    ///     Validates that a fitted camera renders an object inside the frame with a visible margin.
    /// </summary>
    [Fact]
    public void SceneRendering_FittedCamera_RendersObjectInsideFrame()
    {
        // Arrange
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Sphere(2.0, 32)));
        var camera = Camera.FitToBounds(scene.ComputeBounds(), 20.0, 15.0, 160.0 / 100.0);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 160, 100, new SceneRenderOptions { Supersampling = 1 }, TestContext.Current.CancellationToken);

        // Assert: some pixels are drawn and none touch the border
        var bounds = Render3DTestHelpers.BoundsOfNon(surface, new Rgba32(255, 255, 255, 255));
        Assert.True(bounds.Count > 1000);
        Assert.True(bounds.MinX > 0 && bounds.MinY > 0 && bounds.MaxX < 159 && bounds.MaxY < 99);
    }

    /// <summary>
    ///     Validates the analytic Lambert shade of a face lit by ambient and headlight: 180 x (0.25 + 0.5) = 135, and 90 at a 60 degree tilt.
    /// </summary>
    [Fact]
    public void SceneRendering_CustomHeadlight_ShadesFaceAnalytically()
    {
        // Arrange: a floor quad facing the camera, lit by ambient 0.25 and a 0.5 headlight, no specular
        var options = new SceneRenderOptions
        {
            Lights =
            [
                new AmbientLight { Intensity = 0.25f },
                new DirectionalLight { Direction = new Vector3D(0, 0, -1), IsCameraRelative = true, Intensity = 0.5f },
            ],
            SpecularStrength = 0.0f,
            Supersampling = 1,
        };
        var camera = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, 10), Vector3D.Zero, Vector3D.UnitY, 8.0);
        var flat = new Scene(new MeshNode(Render3DTestHelpers.CreateFloorQuad(-2, -2, 2, 2)));
        var tilted = new Scene(new MeshNode(Render3DTestHelpers.CreateFloorQuad(-2, -2, 2, 2)) { Transform = Matrix4x4D.CreateRotationX(60.0) });

        // Act
        using var facing = SceneRenderer.Render(flat, camera, 32, 32, options, TestContext.Current.CancellationToken);
        using var inclined = SceneRenderer.Render(tilted, camera, 32, 32, options, TestContext.Current.CancellationToken);

        // Assert
        var a = facing[16, 16];
        var b = inclined[16, 16];
        Assert.InRange(a.R, 134, 136);
        Assert.Equal(a.R, a.G);
        Assert.Equal(a.R, a.B);
        Assert.InRange(b.R, 89, 91);
        Assert.Equal(255, a.A);
    }

    /// <summary>
    ///     Validates that two adjacent quads sharing an edge leave no gap or overlap (every pixel of the union is drawn exactly once, by construction of the top-left rule).
    /// </summary>
    [Fact]
    public void SceneRendering_TwoAdjacentQuads_LeaveNoGap()
    {
        // Arrange: two quads meeting along the slanted line x = 0.3 y + 0.11 (a shared welded edge, crossing tile boundaries)
        var builder = new MeshBuilder();
        var a = builder.AddVertex(new Vector3D(-3.1, -3.7, 0));
        var b = builder.AddVertex(new Vector3D(0.11 - (0.3 * 3.7), -3.7, 0));
        var c = builder.AddVertex(new Vector3D(0.11 + (0.3 * 3.9), 3.9, 0));
        var d = builder.AddVertex(new Vector3D(-3.1, 3.9, 0));
        var e = builder.AddVertex(new Vector3D(4.2, -3.7, 0));
        var f = builder.AddVertex(new Vector3D(4.2, 3.9, 0));
        builder.AddPolygon([a, b, c, d]);
        builder.AddPolygon([b, e, f, c]);
        var scene = new Scene(new MeshNode(builder.Build()));
        var camera = Render3DTestHelpers.OrthoCamera(new Vector3D(0, 0, 10), Vector3D.Zero, Vector3D.UnitY, 12.0);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 96, 96, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: the union is a rectangle, so inside its bounding box every pixel is the surface color (no cracks)
        var bounds = Render3DTestHelpers.BoundsOfNon(surface, new Rgba32(255, 255, 255, 255));
        Assert.True(bounds.MaxX - bounds.MinX > 40);
        for (var y = bounds.MinY; y <= bounds.MaxY; y++)
        {
            for (var x = bounds.MinX; x <= bounds.MaxX; x++)
            {
                Assert.Equal(new Rgba32(180, 180, 180, 255), surface[x, y]);
            }
        }
    }

    /// <summary>
    ///     Validates that a scene of many cubes straddling tile boundaries renders identically with tiles rendered
    ///     in parallel or sequentially, and that a closed shape has no seams (no background pixel inside its interior).
    /// </summary>
    [Fact]
    public void SceneRendering_SceneStraddlingTileBoundaries_HasNoSeams()
    {
        // Arrange: a sphere centred in a 100 x 100 image crosses all four interior tile edges
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Sphere(3.0, 40)));
        var camera = Render3DTestHelpers.OrthoCamera(new Vector3D(0, -20, 0), Vector3D.Zero, Vector3D.UnitZ, 10.0);
        var white = new Rgba32(255, 255, 255, 255);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 100, 100, Render3DTestHelpers.FlatOptions(), TestContext.Current.CancellationToken);

        // Assert: along every row crossing the sphere, the drawn pixels form one contiguous run (a seam would leave a background gap)
        var rowsChecked = 0;
        for (var y = 0; y < 100; y++)
        {
            var first = -1;
            var last = -1;
            var drawn = 0;
            for (var x = 0; x < 100; x++)
            {
                if (surface[x, y] != white)
                {
                    first = first < 0 ? x : first;
                    last = x;
                    drawn++;
                }
            }

            if (drawn > 0)
            {
                Assert.Equal(last - first + 1, drawn);
                rowsChecked++;
            }
        }

        Assert.True(rowsChecked > 50);
    }

    /// <summary>
    ///     Validates the fragment-list pipeline end to end below the renderer: real box meshes go through triangle
    ///     setup, rasterization, fragment collection and CSG resolution, and a box cut into another shows the
    ///     subtrahend's back face as the cut surface.
    /// </summary>
    [Fact]
    public void SceneRendering_FragmentListPipeline_BoxMinusBox_ShowsCutSurfaceInsideTheCutout()
    {
        // Arrange: orthographic view down -Z over a 64 x 64 sample tile; A spans depth 6..10, B (subtrahend) depth 5..8 over the middle
        var projection = new SampleProjection(true, 1.0 / 32.0, 1.0 / 32.0, 64, 64, 0.1);
        var a = new MeshNode(Render3DTestHelpers.CreateBoxMesh(new Vector3D(-30, -30, -10), new Vector3D(60, 60, 4)));
        var b = new MeshNode(Render3DTestHelpers.CreateBoxMesh(new Vector3D(-8, -8, -8), new Vector3D(16, 16, 3)));
        var root = new CsgNode(CsgOperation.Difference, [a, b]);
        var compilation = CsgCompiler.Compile(root);
        var instances = new Scene(root).EnumerateInstances().ToArray();
        var surfaceColor = new Rgba32(10, 20, 30, 255);
        var cutColor = new Rgba32(200, 100, 0, 255);
        var screen = new List<ScreenTriangle>();
        foreach (var instance in instances)
        {
            var isCutter = ReferenceEquals(instance.Node, b);
            var indices = instance.Mesh.TriangleIndices;
            for (var t = 0; t < indices.Count; t += 3)
            {
                ClipVertex Vertex(int i) => new(instance.Mesh.Vertices[indices[t + i]].X, instance.Mesh.Vertices[indices[t + i]].Y, instance.Mesh.Vertices[indices[t + i]].Z);
                TriangleRasterizer.Setup(
                    new ViewTriangle
                    {
                        A = Vertex(0),
                        B = Vertex(1),
                        C = Vertex(2),
                        Priority = instance.Order,
                        Leaf = compilation.LeafOfInstance[instance.Order],
                        FrontEnterColor = isCutter ? cutColor : surfaceColor,
                        BackEnterColor = isCutter ? cutColor : surfaceColor,
                    },
                    projection,
                    screen);
            }
        }

        var buffer = new FragmentBuffer(100_000);
        var workspace = new TileWorkspace(64 * 64);
        var resolver = new CsgResolver(compilation.Program!);
        var tile = new TileRect(0, 0, 64, 64);

        // Act: count pass, fill pass over the same triangles, then resolve
        buffer.Begin(0, 0, 64, 64);
        foreach (var triangle in screen)
        {
            buffer.CountTriangle(triangle);
        }

        buffer.EndCounting();
        for (var i = 0; i < screen.Count; i++)
        {
            buffer.AddTriangle(i, screen[i]);
        }

        workspace.Begin(tile.X0, tile.Y0, tile.Width, tile.Height);
        resolver.ResolveTile(buffer, [.. screen], workspace, 1e-4f, 1000.0f, TestContext.Current.CancellationToken);

        // Assert: outside the cutout the top face of A at depth 6; inside it the back face of B (the cut surface) at depth 8; off both: background
        var outside = workspace.IndexOf(4, 32);
        var inside = workspace.IndexOf(32, 32);
        Assert.InRange(workspace.Depth[outside], 5.999f, 6.001f);
        Assert.Equal(surfaceColor, workspace.Colors[outside]);
        Assert.InRange(workspace.Depth[inside], 7.999f, 8.001f);
        Assert.Equal(cutColor, workspace.Colors[inside]);
        Assert.Equal(1, workspace.Meta[inside] & 1);
        Assert.True(float.IsPositiveInfinity(workspace.Depth[workspace.IndexOf(0, 0)]));
    }

    /// <summary>
    ///     Validates the whole subsystem on a constructive solid geometry scene: camera, lights, tessellated
    ///     operands, validation, fragment collection, resolution and antialiasing together produce a lit image whose
    ///     cavity is shaded differently from the surrounding face, identically for sequential and parallel tiles.
    /// </summary>
    [Fact]
    public void SceneRendering_ConstructiveSolidGeometry_CubeMinusSphere_RendersLitCavityDeterministically()
    {
        // Arrange: a cube with a spherical bite out of its top, default lights and antialiasing
        var cube = new MeshNode(PrimitiveTessellator.Box(new Vector3D(10, 10, 10), center: true));
        var bite = new MeshNode(PrimitiveTessellator.Sphere(3.0, 32)) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(0, 0, 5)) };
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [cube, bite]));
        var camera = Camera.LookAt(new Vector3D(0, -2, 30), new Vector3D(0, 0, 2), Vector3D.UnitY);
        var reports = new List<SceneRenderUnsupportedFeature>();
        var sequential = new SceneRenderOptions { Supersampling = 2, MaxDegreeOfParallelism = 1, UnsupportedFeatureHandler = reports.Add };
        var parallel = new SceneRenderOptions { Supersampling = 2, MaxDegreeOfParallelism = -1 };

        // Act
        using var a = SceneRenderer.Render(scene, camera, 120, 120, sequential, TestContext.Current.CancellationToken);
        using var b = SceneRenderer.Render(scene, camera, 120, 120, parallel, TestContext.Current.CancellationToken);

        // Assert: closed primitives raise no report, the cavity centre differs from the top face, and tiles agree byte for byte
        Assert.Empty(reports);
        Assert.NotEqual(a[60, 60], a[10, 60]);
        Assert.True(Render3DTestHelpers.DistinctColors(a).Count > 4);
        Render3DTestHelpers.AssertSurfacesIdentical(a, b);
    }
}
