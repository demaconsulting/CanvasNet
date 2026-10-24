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

    /// <summary>
    ///     Validates that polygon extrusion through the public subsystem API yields closed, outward meshes that are
    ///     valid constructive-solid-geometry operands: a ring with a hole, a twisted and scaled star, and full and
    ///     partial rotations.
    /// </summary>
    [Fact]
    public void Tessellation_PolygonExtrusion_AllResults_AreClosedAndValidCsgOperands()
    {
        // Arrange: a square plate with a circular hole, a star, and an off-axis profile made from the 2D primitives
        var plate = new PolygonRegion(
        [
            PrimitiveTessellator.Rectangle(new Vector2D(20, 20), center: true).Contours[0],
            PrimitiveTessellator.Circle(5.0, 32).Contours[0],
        ]);
        var star = PrimitiveTessellator.Polygon(Enumerable.Range(0, 10)
            .Select(i => new Vector2D(
                (i % 2 == 0 ? 4.0 : 1.5) * Math.Cos(i * Math.PI / 5.0),
                (i % 2 == 0 ? 4.0 : 1.5) * Math.Sin(i * Math.PI / 5.0)))
            .ToArray());
        var profile = PrimitiveTessellator.Rectangle(new Vector2D(2, 5));
        var offAxis = new PolygonRegion([profile.Contours[0].Select(p => new Vector2D(p.X + 3.0, p.Y)).ToArray()]);

        var meshes = new[]
        {
            Extruder.Linear(plate, 4.0, center: true),
            Extruder.Linear(star, 6.0, twistDegrees: 90.0, slices: 16, scale: new Vector2D(0.5, 1.5)),
            Extruder.Linear(star, 6.0, twistDegrees: 45.0, slices: 8, scale: Vector2D.Zero),
            Extruder.Rotate(offAxis, 360.0, 24),
            Extruder.Rotate(offAxis, 120.0, 8),
            Extruder.Rotate(profile, 360.0, 24),
        };

        // Act / Assert: every result is closed, outward, positive and passes the operand validator
        foreach (var mesh in meshes)
        {
            Render3DTestHelpers.AssertValidCsgOperand(mesh);
        }

        // The plate is one shell with a through-hole, so its volume is the plate minus the 32-gon hole
        var hole = PrimitiveTessellator.Circle(5.0, 32).Area;
        Assert.Equal((400.0 - hole) * 4.0, meshes[0].SignedVolume, 1e-9);
    }

    /// <summary>
    ///     Validates that invalid polygons are rejected when the region is created, never turned into a defective mesh.
    /// </summary>
    [Fact]
    public void Tessellation_PolygonRegion_InvalidPolygon_IsRejected()
    {
        // Arrange: a bow tie, a zero-area contour, crossing contours and touching contours
        Vector2D[] bowTie = [new(0, 0), new(2, 2), new(2, 0), new(0, 2)];
        Vector2D[] line = [new(0, 0), new(1, 1), new(2, 2)];
        Vector2D[] squareA = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)];
        Vector2D[] squareB = [new(2, 2), new(6, 2), new(6, 6), new(2, 6)];
        Vector2D[] touching = [new(4, 4), new(8, 4), new(8, 8), new(4, 8)];

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new PolygonRegion([bowTie]));
        Assert.Throws<ArgumentException>(() => PrimitiveTessellator.Polygon(bowTie));
        Assert.Throws<ArgumentException>(() => new PolygonRegion([line]));
        Assert.Throws<ArgumentException>(() => new PolygonRegion([squareA, squareB]));
        Assert.Throws<ArgumentException>(() => new PolygonRegion([squareA, touching]));
    }
}
