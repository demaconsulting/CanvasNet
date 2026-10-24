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

    /// <summary>
    ///     Validates that the subsystem reproduces a caller-supplied polyhedron as given: an open, inverted
    ///     face set is neither closed nor reoriented.
    /// </summary>
    [Fact]
    public void Tessellation_Polyhedron_OpenFaceSet_IsPreservedWithoutClosureOrReorientation()
    {
        // Arrange: three faces of a tetrahedron, all listed clockwise from outside
        Vector3D[] points = [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ];
        int[][] faces = [[0, 1, 2], [0, 3, 1], [0, 2, 3]];

        // Act
        var mesh = PrimitiveTessellator.Polyhedron(points, faces);

        // Assert: exactly the supplied triangles in the supplied order, still open
        Assert.False(mesh.IsClosed);
        Assert.Equal([0, 1, 2, 0, 3, 1, 0, 2, 3], mesh.TriangleIndices);
    }
}
