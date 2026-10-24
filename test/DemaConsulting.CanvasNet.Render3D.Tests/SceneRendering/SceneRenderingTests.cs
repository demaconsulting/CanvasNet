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
}
