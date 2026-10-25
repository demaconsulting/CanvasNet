// cspell:ignore Pappus

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for <see cref="Extruder.Rotate(PolygonRegion, double, int)"/>.
/// </summary>
public class ExtruderRotateTests
{
    /// <summary>Creates a counter-clockwise axis-aligned rectangle contour.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The four corners.</returns>
    private static Vector2D[] Rect(double x, double y, double width, double height) =>
        [new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)];

    /// <summary>
    ///     Computes the integral of the X coordinate over a region with Green's theorem (exact for polygons;
    ///     holes, wound clockwise, subtract).
    /// </summary>
    /// <param name="region">The region.</param>
    /// <returns>The integral of x dA.</returns>
    private static double IntegralX(PolygonRegion region)
    {
        var sum = 0.0;
        foreach (var contour in region.Contours)
        {
            for (var i = 0; i < contour.Count; i++)
            {
                var p = contour[i];
                var q = contour[(i + 1) % contour.Count];
                sum += (q.Y - p.Y) * ((p.X * p.X) + (p.X * q.X) + (q.X * q.X)) / 6.0;
            }
        }

        return sum;
    }

    /// <summary>
    ///     Computes the exact volume of the faceted solid of revolution: <c>n sin(delta) |integral of x dA|</c>.
    /// </summary>
    /// <param name="region">The profile.</param>
    /// <param name="angleDegrees">The sweep angle in degrees.</param>
    /// <param name="segments">The number of angular steps.</param>
    /// <returns>The facet volume.</returns>
    private static double FacetVolume(PolygonRegion region, double angleDegrees, int segments) =>
        segments * Math.Sin(angleDegrees * Math.PI / 180.0 / segments) * Math.Abs(IntegralX(region));

    /// <summary>Creates a torus profile: a 16-gon of radius 1 centered at x = 3.</summary>
    /// <returns>The region.</returns>
    private static PolygonRegion TorusProfile() => new([PrimitiveTessellator.Circle(1.0, 16).Contours[0].Select(p => new Vector2D(p.X + 3.0, p.Y)).ToArray()]);

    /// <summary>Creates the region of a named profile, used by the parameterized tests.</summary>
    /// <param name="name">The profile name.</param>
    /// <returns>The region.</returns>
    private static PolygonRegion Profile(string name) => name switch
    {
        "offAxis" => new PolygonRegion([Rect(2, 0, 3, 3)]),
        "touchingAxis" => new PolygonRegion([Rect(0, 0, 3, 4)]),
        "cone" => new PolygonRegion([[new(0, 0), new(3, 0), new(0, 4)]]),
        "torus" => TorusProfile(),
        "hole" => new PolygonRegion([Rect(2, 0, 8, 8), Rect(4, 2, 4, 4)]),
        "negative" => new PolygonRegion([Rect(-5, 0, 3, 3)]),
        "negativeTouching" => new PolygonRegion([Rect(-3, 0, 3, 4)]),
        "negativeHole" => new PolygonRegion([Rect(-10, 0, 8, 8), Rect(-8, 2, 4, 4)]),
        "lens" => new PolygonRegion([[new(0, 0), new(2, 2), new(0, 4)]]),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown profile."),
    };

    /// <summary>
    ///     Validates the exact facet volume of an off-axis rectangle revolved fully.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_RectangleOffAxis_FullRevolution_ExactFacetVolume()
    {
        // Arrange
        var region = Profile("offAxis");

        // Act
        var mesh = Extruder.Rotate(region, 360.0, 12);

        // Assert: 4 profile vertices on 12 rings, 4 edges with two triangles per segment
        Assert.Equal(4 * 12, mesh.VertexCount);
        Assert.Equal(4 * 2 * 12, mesh.TriangleCount);
        var expected = FacetVolume(region, 360.0, 12);
        Assert.Equal(expected, mesh.SignedVolume, expected * 1e-9);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a rectangle touching the axis equals a cylinder and welds its axis vertices.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_RectangleTouchingAxis_MatchesCylinderAndWeldsAxisVertices()
    {
        // Arrange
        var cylinder = PrimitiveTessellator.Cylinder(4.0, 3.0, 3.0, 16);

        // Act
        var mesh = Extruder.Rotate(Profile("touchingAxis"), 360.0, 16);

        // Assert: two axis vertices plus two vertices per azimuth
        Assert.Equal(2 + (2 * 16), mesh.VertexCount);
        Assert.Equal(2, mesh.Vertices.Count(vertex => Math.Abs(vertex.X) < 1e-12 && Math.Abs(vertex.Y) < 1e-12));
        Assert.Equal(cylinder.SignedVolume, mesh.SignedVolume, cylinder.SignedVolume * 1e-12);
        Assert.Equal(cylinder.SurfaceArea, mesh.SurfaceArea, cylinder.SurfaceArea * 1e-12);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a triangle with one edge on the axis revolves to a cone with a single apex vertex.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_TriangleOnAxis_IsConeWithSingleApex()
    {
        // Arrange
        var region = Profile("cone");

        // Act
        var mesh = Extruder.Rotate(region, 360.0, 12);

        // Assert: exactly one vertex at the apex (0, 0, 4) and one at the base center
        Assert.Single(mesh.Vertices, vertex => Math.Abs(vertex.X) < 1e-12 && Math.Abs(vertex.Y) < 1e-12 && Math.Abs(vertex.Z - 4.0) < 1e-12);
        Assert.Equal(2 + 12, mesh.VertexCount);
        var expected = FacetVolume(region, 360.0, 12);
        Assert.Equal(expected, mesh.SignedVolume, expected * 1e-9);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that an edge lying on the axis sweeps to nothing and yields no degenerate triangles.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_ProfileEdgeAlongAxis_SkipsDegenerateWall()
    {
        // Arrange: rectangle (0,0)-(2,2); edges: bottom (one axis end), right (off axis), top (one axis end), left (on axis)
        var region = new PolygonRegion([Rect(0, 0, 2, 2)]);

        // Act
        var mesh = Extruder.Rotate(region, 360.0, 10);

        // Assert: 1 + 2 + 1 + 0 triangles per segment
        Assert.Equal(4 * 10, mesh.TriangleCount);
        Assert.Equal(MeshTopologyDefects.None, mesh.OperandDefects);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates a torus: an off-axis circle revolved fully has the exact facet volume and is one closed shell.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_CircleProfileOffAxis_IsTorusWithExactFacetVolume()
    {
        // Arrange
        var region = TorusProfile();

        // Act
        var mesh = Extruder.Rotate(region, 360.0, 24);

        // Assert
        var expected = FacetVolume(region, 360.0, 24);
        Assert.Equal(expected, mesh.SignedVolume, expected * 1e-9);
        Assert.Equal(16 * 24, mesh.VertexCount);
        Assert.Equal(1, Render3DTestHelpers.CountConnectedShells(mesh));

        // The faceted volume approaches Pappus' theorem (2 pi x A) from below within the n-gon factor
        var pappus = 2.0 * Math.PI * 3.0 * region.Area;
        Assert.InRange(mesh.SignedVolume / pappus, 0.98, 1.0);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a profile with a hole revolves to a closed hollow solid.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_ProfileWithHole_FullRevolution_IsClosed()
    {
        // Arrange
        var region = Profile("hole");

        // Act
        var mesh = Extruder.Rotate(region, 360.0, 16);

        // Assert: outer shell outward, cavity shell inward, volume from the closed form
        Assert.Equal(2, Render3DTestHelpers.CountConnectedShells(mesh));
        var expected = FacetVolume(region, 360.0, 16);
        Assert.Equal(expected, mesh.SignedVolume, expected * 1e-9);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a partial sweep has two end caps, is closed and has the exact facet volume.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_PartialAngle_HasEndCapsClosedAndExactVolume()
    {
        // Arrange
        var region = Profile("offAxis");

        // Act
        var mesh = Extruder.Rotate(region, 90.0, 8);

        // Assert: nine rings of four vertices; 4 edges * 2 triangles * 8 segments plus two caps of two triangles
        Assert.Equal(4 * 9, mesh.VertexCount);
        Assert.Equal((4 * 2 * 8) + 4, mesh.TriangleCount);
        var expected = FacetVolume(region, 90.0, 8);
        Assert.Equal(expected, mesh.SignedVolume, expected * 1e-9);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates a partial sweep of a profile that touches the axis.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_PartialAngleTouchingAxis_IsClosed()
    {
        // Arrange / Act
        var mesh = Extruder.Rotate(Profile("touchingAxis"), 90.0, 4);

        // Assert: two axis vertices plus two per azimuth ring, five rings
        Assert.Equal(2 + (2 * 5), mesh.VertexCount);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a full revolution has no duplicated seam vertices.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_Angle360_SeamHasNoDuplicateVertices()
    {
        // Arrange / Act
        var mesh = Extruder.Rotate(Profile("offAxis"), 360.0, 9);

        // Assert
        Assert.Equal(4 * 9, mesh.VertexCount);
        Render3DTestHelpers.AssertNoDuplicateVerticesAndAllReferenced(mesh);
    }

    /// <summary>
    ///     Validates that a profile on the negative side of the axis is outward with the same volume as its mirror.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_NegativeSideProfile_IsOutwardWithSameVolume()
    {
        // Arrange: Rect(-5, 0, 3, 3) mirrors Rect(2, 0, 3, 3)
        var positive = Extruder.Rotate(Profile("offAxis"), 360.0, 12);
        var partialPositive = Extruder.Rotate(Profile("offAxis"), 90.0, 6);

        // Act
        var negative = Extruder.Rotate(new PolygonRegion([Rect(-5, 0, 3, 3)]), 360.0, 12);
        var partialNegative = Extruder.Rotate(new PolygonRegion([Rect(-5, 0, 3, 3)]), 90.0, 6);

        // Assert: valid, outward (positive volume) and equal to the mirror image's volume
        Assert.Equal(positive.SignedVolume, negative.SignedVolume, positive.SignedVolume * 1e-12);
        Assert.Equal(partialPositive.SignedVolume, partialNegative.SignedVolume, partialPositive.SignedVolume * 1e-12);
        Render3DTestHelpers.AssertValidCsgOperand(negative);
        Render3DTestHelpers.AssertValidCsgOperand(partialNegative);
    }

    /// <summary>
    ///     Validates that a profile touching the axis at an isolated vertex (both adjacent edges leave the axis)
    ///     is rejected, on either side of the axis and also next to an edge lying on the axis.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_IsolatedAxisVertex_Throws()
    {
        // Arrange: triangles whose single vertex (0, 0) touches the axis, and a notched profile whose vertex (0, 1)
        // touches the axis in isolation although its edge from (0, 6) to (0, 4) lies on the axis
        var positive = new PolygonRegion([[new Vector2D(0, 0), new Vector2D(2, -1), new Vector2D(2, 1)]]);
        var negative = new PolygonRegion([[new Vector2D(0, 0), new Vector2D(-2, 1), new Vector2D(-2, -1)]]);
        var notched = new PolygonRegion([[new Vector2D(0, 1), new Vector2D(3, 0), new Vector2D(3, 6), new Vector2D(0, 6), new Vector2D(0, 4), new Vector2D(2, 3)]]);

        // Act / Assert
        var exception = Assert.Throws<ArgumentException>(() => Extruder.Rotate(positive, 360.0, 12));
        Assert.Contains("isolated", exception.Message, StringComparison.Ordinal);
        Assert.Equal("region", exception.ParamName);
        Assert.Throws<ArgumentException>(() => Extruder.Rotate(positive, 90.0, 4));
        Assert.Throws<ArgumentException>(() => Extruder.Rotate(negative, 360.0, 12));
        Assert.Throws<ArgumentException>(() => Extruder.Rotate(notched, 360.0, 12));
    }

    /// <summary>
    ///     Validates that a profile whose axis vertices all end an edge lying on the axis (poles) is still accepted.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_PoleVerticesWithAxisEdge_AreAccepted()
    {
        // Arrange: a lens-like profile with one edge along the axis from (0, 0) to (0, 4)
        var region = new PolygonRegion([[new Vector2D(0, 0), new Vector2D(2, 2), new Vector2D(0, 4)]]);

        // Act
        var mesh = Extruder.Rotate(region, 360.0, 12);

        // Assert
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a profile on both sides of the axis is rejected.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_ProfileCrossingAxis_Throws()
    {
        // Arrange
        var region = new PolygonRegion([Rect(-1, 0, 3, 2)]);

        // Act / Assert
        var exception = Assert.Throws<ArgumentException>(() => Extruder.Rotate(region, 360.0, 12));
        Assert.Contains("axis", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a profile lying entirely on the axis cannot be built: it has no area, so the region
    ///     constructor rejects it before a rotation can be attempted.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_ProfileOnlyOnAxis_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Extruder.Rotate(new PolygonRegion([[new Vector2D(0, 0), new Vector2D(0, 1), new Vector2D(0, 2)]]), 360.0, 12));
    }

    /// <summary>
    ///     Validates that an angle whose radian value rounds to zero is rejected instead of welding the end
    ///     ring onto the start ring and returning an open mesh.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_TinyAngleRoundingToZeroRadians_Throws()
    {
        // Arrange
        var region = Profile("offAxis");

        // Act / Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, double.Epsilon, 1));
        Assert.Equal("angleDegrees", exception.ParamName);
    }

    /// <summary>
    ///     Validates that a sweep step that cannot be resolved at the profile's radius (the ring vertices would
    ///     coincide) is rejected rather than producing an open mesh.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_StepUnresolvableAtRadius_Throws()
    {
        // Arrange: a profile at radius 1e-300 swept by 1e-300 degrees has a ring offset (y) that rounds to zero
        var region = new PolygonRegion([Rect(1e-300, 0, 1e-300, 1)]);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => Extruder.Rotate(region, 1e-300, 1));
    }

    /// <summary>
    ///     Validates that invalid arguments are rejected with the documented exceptions.
    /// </summary>
    [Fact]
    public void Extruder_Rotate_InvalidArguments_Throw()
    {
        // Arrange
        var region = Profile("offAxis");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Extruder.Rotate(null!, 360.0, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, 0.0, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, -10.0, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, 360.5, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, double.NaN, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, double.PositiveInfinity, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, 360.0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, 90.0, 0));
        Assert.Throws<ArgumentException>(() => Extruder.Rotate(region, 360.0, int.MaxValue));
        Assert.Equal(0, Extruder.Rotate(PolygonRegion.Empty, 90.0, 4).TriangleCount);

        // A partial sweep may use a single segment: 4 edges * 2 triangles plus two caps of two triangles
        Assert.Equal((4 * 2) + 4, Extruder.Rotate(region, 45.0, 1).TriangleCount);
    }

    /// <summary>
    ///     Regression: a partial sweep whose single angular step reaches 180 degrees or more produced flat
    ///     (180), inside-out (200, 270, 359) or near-flat (359 in two segments) meshes; the segment count must
    ///     now keep every step at most 120 degrees, the step of the coarsest full revolution.
    /// </summary>
    /// <param name="angle">The sweep angle in degrees.</param>
    /// <param name="segments">The (too small) number of angular steps.</param>
    [Theory]
    [InlineData(180.0, 1)]
    [InlineData(200.0, 1)]
    [InlineData(270.0, 1)]
    [InlineData(359.0, 1)]
    [InlineData(359.0, 2)]
    [InlineData(240.5, 2)]
    [InlineData(121.0, 1)]
    public void Extruder_Rotate_StepWiderThanMaximum_ThrowsArgumentOutOfRangeOnSegments(double angle, int segments)
    {
        // Arrange: the profile of the reported defect, the square (1,0)-(2,1)
        var region = new PolygonRegion([Rect(1, 0, 1, 1)]);

        // Act / Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Rotate(region, angle, segments));
        Assert.Equal("segments", exception.ParamName);
    }

    /// <summary>
    ///     Validates that sweeps whose step is exactly at or below the 120 degree maximum are still accepted and
    ///     yield closed, outward, positive-volume operands with the exact facet volume.
    /// </summary>
    /// <param name="angle">The sweep angle in degrees.</param>
    /// <param name="segments">The number of angular steps.</param>
    [Theory]
    [InlineData(90.0, 1)]
    [InlineData(119.0, 1)]
    [InlineData(120.0, 1)]
    [InlineData(180.0, 2)]
    [InlineData(240.0, 2)]
    [InlineData(359.0, 3)]
    [InlineData(360.0, 3)]
    public void Extruder_Rotate_StepAtOrBelowMaximum_IsValidPositiveVolume(double angle, int segments)
    {
        // Arrange
        var region = new PolygonRegion([Rect(1, 0, 1, 1)]);

        // Act
        var mesh = Extruder.Rotate(region, angle, segments);

        // Assert
        Assert.True(mesh.SignedVolume > 0.0);
        var expected = FacetVolume(region, angle, segments);
        Assert.Equal(expected, mesh.SignedVolume, expected * 1e-9);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that every tested profile and sweep yields a closed, outward, valid CSG operand.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <param name="angle">The sweep angle in degrees.</param>
    /// <param name="segments">The number of angular steps.</param>
    [Theory]
    [InlineData("offAxis", 360.0, 12)]
    [InlineData("touchingAxis", 360.0, 16)]
    [InlineData("cone", 360.0, 12)]
    [InlineData("torus", 360.0, 24)]
    [InlineData("hole", 360.0, 16)]
    [InlineData("offAxis", 90.0, 8)]
    [InlineData("touchingAxis", 90.0, 4)]
    [InlineData("cone", 270.0, 9)]
    [InlineData("hole", 180.0, 10)]
    [InlineData("negative", 360.0, 12)]
    [InlineData("negative", 120.0, 5)]
    [InlineData("negativeTouching", 200.0, 7)]
    [InlineData("negativeHole", 360.0, 12)]
    [InlineData("lens", 360.0, 12)]
    [InlineData("lens", 90.0, 3)]
    public void Extruder_Rotate_AllVariants_AreValidCsgOperands(string name, double angle, int segments)
    {
        // Arrange / Act
        var mesh = Extruder.Rotate(Profile(name), angle, segments);

        // Assert
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
        Assert.Equal(FacetVolume(Profile(name), angle, segments), mesh.SignedVolume, FacetVolume(Profile(name), angle, segments) * 1e-9);
    }
}
