// cspell:ignore Tessellator

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Subsystem-level tests for the Tessellation subsystem.
/// </summary>
public class TessellationTests
{
    /// <summary>
    ///     Validates that every primitive produced by the subsystem is a closed, outward-oriented
    ///     mesh usable directly by the scene model.
    /// </summary>
    [Fact]
    public void Tessellation_AllPrimitives_AreClosedAndOutwardOriented()
    {
        // Arrange: one of each primitive kind
        var meshes = new[]
        {
            PrimitiveTessellator.Box(new Vector3D(2, 3, 4), center: true),
            PrimitiveTessellator.Sphere(1.5, 20),
            PrimitiveTessellator.Cylinder(3.0, 1.0, 1.0, 20),
            PrimitiveTessellator.Cylinder(3.0, 1.0, 0.0, 20),
        };

        // Act / Assert: closed, positive volume, normals away from the centroid
        foreach (var mesh in meshes)
        {
            Render3DTestHelpers.AssertClosedAndOutward(mesh);
        }
    }
}
