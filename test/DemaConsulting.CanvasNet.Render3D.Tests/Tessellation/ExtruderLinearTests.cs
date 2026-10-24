// cspell:ignore frustum frusta

using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests.Tessellation;

/// <summary>
///     Unit tests for <see cref="Extruder.Linear(PolygonRegion, double, bool, double, int, Vector2D?)"/>.
/// </summary>
public class ExtruderLinearTests
{
    /// <summary>The L-shaped test profile (area 12).</summary>
    private static readonly Vector2D[] LShape = [new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4)];

    /// <summary>Creates a counter-clockwise axis-aligned rectangle contour.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The four corners.</returns>
    private static Vector2D[] Rect(double x, double y, double width, double height) =>
        [new(x, y), new(x + width, y), new(x + width, y + height), new(x, y + height)];

    /// <summary>Creates a 10 x 10 square with a 4 x 4 hole centered at (5, 5).</summary>
    /// <returns>The region.</returns>
    private static PolygonRegion RingRegion() => new([Rect(0, 0, 10, 10), Rect(3, 3, 4, 4)]);

    /// <summary>Creates a five-pointed star profile.</summary>
    /// <returns>The region.</returns>
    private static PolygonRegion StarRegion() => new([Enumerable.Range(0, 10)
        .Select(i => new Vector2D(
            (i % 2 == 0 ? 3.0 : 1.2) * Math.Cos(i * Math.PI / 5.0),
            (i % 2 == 0 ? 3.0 : 1.2) * Math.Sin(i * Math.PI / 5.0)))
        .ToArray()]);

    /// <summary>
    ///     Creates the mesh of a named parameter set, used by the parameterized tests.
    /// </summary>
    /// <param name="name">The variant name.</param>
    /// <returns>The extruded mesh.</returns>
    private static Mesh Variant(string name) => name switch
    {
        "square" => Extruder.Linear(new PolygonRegion([Rect(0, 0, 2, 3)]), 4.0),
        "centered" => Extruder.Linear(new PolygonRegion([Rect(0, 0, 2, 3)]), 4.0, center: true),
        "concave" => Extruder.Linear(new PolygonRegion([LShape]), 3.0),
        "ring" => Extruder.Linear(RingRegion(), 3.0),
        "island" => Extruder.Linear(new PolygonRegion([Rect(0, 0, 30, 30), Rect(5, 5, 20, 20), Rect(12, 12, 6, 6)]), 2.0),
        "multiHole" => Extruder.Linear(new PolygonRegion([Rect(0, 0, 20, 10), Rect(2, 2, 3, 3), Rect(8, 2, 3, 6), Rect(14, 4, 4, 4)]), 2.5),
        "frustum" => Extruder.Linear(new PolygonRegion([LShape]), 3.0, scale: new Vector2D(0.5, 0.5)),
        "uniformFrustum" => Extruder.Linear(new PolygonRegion([LShape]), 3.0, false, 0.0, 2, 1.5),
        "pyramid" => Extruder.Linear(new PolygonRegion([LShape]), 3.0, slices: 3, scale: Vector2D.Zero),
        "perAxis" => Extruder.Linear(new PolygonRegion([LShape]), 3.0, slices: 8, scale: new Vector2D(2.0, 0.5)),
        "twist" => Extruder.Linear(new PolygonRegion([Rect(-1, -1, 2, 2)]), 4.0, twistDegrees: 90.0, slices: 8),
        "twistScale" => Extruder.Linear(StarRegion(), 4.0, twistDegrees: 120.0, slices: 12, scale: new Vector2D(0.5, 1.5)),
        "twist360Ring" => Extruder.Linear(RingRegion(), 4.0, twistDegrees: 360.0, slices: 24),
        "twistApex" => Extruder.Linear(StarRegion(), 4.0, twistDegrees: 90.0, slices: 8, scale: Vector2D.Zero),
        "collinear" => Extruder.Linear(new PolygonRegion([[new(0, 0), new(1, 0), new(2, 0), new(2, 1), new(2, 2), new(1, 2), new(0, 2), new(0, 1)]]), 2.0),
        "slices" => Extruder.Linear(RingRegion(), 3.0, slices: 5),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown variant."),
    };

    /// <summary>
    ///     Validates the prism of a rectangle: 8 vertices, 12 triangles and the analytic volume.
    /// </summary>
    [Fact]
    public void Extruder_Linear_Square_IsClosedPrismWithAnalyticVolume()
    {
        // Arrange / Act
        var mesh = Extruder.Linear(new PolygonRegion([Rect(0, 0, 2, 3)]), 4.0);

        // Assert
        Assert.Equal(8, mesh.VertexCount);
        Assert.Equal(12, mesh.TriangleCount);
        Assert.Equal(24.0, mesh.SignedVolume, 1e-12);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a centered extrusion spans plus and minus half the height.
    /// </summary>
    [Fact]
    public void Extruder_Linear_Centered_SpansPlusMinusHalfHeight()
    {
        // Arrange / Act
        var mesh = Extruder.Linear(new PolygonRegion([Rect(0, 0, 2, 3)]), 4.0, center: true);

        // Assert
        Assert.Equal(-2.0, mesh.Bounds.Min.Z);
        Assert.Equal(2.0, mesh.Bounds.Max.Z);
        Assert.Equal(0.0, Extruder.Linear(new PolygonRegion([Rect(0, 0, 2, 3)]), 4.0).Bounds.Min.Z);
    }

    /// <summary>
    ///     Validates that a concave polygon extrudes to area times height.
    /// </summary>
    [Fact]
    public void Extruder_Linear_ConcavePolygon_VolumeIsAreaTimesHeight()
    {
        // Arrange / Act
        var mesh = Extruder.Linear(new PolygonRegion([LShape]), 3.0);

        // Assert: area 12, closed and outward (the helper checks every triangle normal against the shell)
        Assert.Equal(36.0, mesh.SignedVolume, 1e-12);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that extruding a circle matches the cylinder primitive.
    /// </summary>
    [Fact]
    public void Extruder_Linear_CircleRegion_MatchesCylinderVolume()
    {
        // Arrange
        var cylinder = PrimitiveTessellator.Cylinder(3.0, 2.0, 2.0, 24);

        // Act
        var mesh = Extruder.Linear(PrimitiveTessellator.Circle(2.0, 24), 3.0);

        // Assert
        Assert.Equal(cylinder.SignedVolume, mesh.SignedVolume, 1e-9);
        Assert.Equal(cylinder.SurfaceArea, mesh.SurfaceArea, 1e-9);
        Assert.Equal(cylinder.Bounds, mesh.Bounds);
    }

    /// <summary>
    ///     Validates that a region with a hole is one connected closed shell with a through-hole whose walls
    ///     face the hole axis.
    /// </summary>
    [Fact]
    public void Extruder_Linear_RingWithHole_IsSingleClosedShellWithThroughHole()
    {
        // Arrange / Act
        var mesh = Extruder.Linear(RingRegion(), 3.0);

        // Assert: volume (100 - 16) * 3, one shell, not inverted
        Assert.Equal(84.0 * 3.0, mesh.SignedVolume, 1e-9);
        Assert.Equal(1, Render3DTestHelpers.CountConnectedShells(mesh));
        Assert.False(mesh.IsInverted);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);

        // Every vertical wall triangle inside the hole's footprint must face the hole center (5, 5)
        var holeWalls = 0;
        var vertices = mesh.Vertices;
        for (var t = 0; t < mesh.TriangleIndices.Count; t += 3)
        {
            var a = vertices[mesh.TriangleIndices[t]];
            var b = vertices[mesh.TriangleIndices[t + 1]];
            var c = vertices[mesh.TriangleIndices[t + 2]];
            var normal = Vector3D.Cross(b - a, c - a);
            var centroid = (a + b + c) / 3.0;
            var onHoleBoundary = centroid.X is >= 3.0 and <= 7.0 && centroid.Y is >= 3.0 and <= 7.0;
            if (!onHoleBoundary || Math.Abs(normal.Z) > 1e-12)
            {
                continue;
            }

            holeWalls++;
            Assert.True((normal.X * (centroid.X - 5.0)) + (normal.Y * (centroid.Y - 5.0)) < 0.0, "A hole wall must face the hole.");
        }

        Assert.Equal(8, holeWalls);
    }

    /// <summary>
    ///     Validates that an island inside a hole yields two outward shells that are a valid operand.
    /// </summary>
    [Fact]
    public void Extruder_Linear_IslandInsideHole_IsTwoOutwardShellsAndValidOperand()
    {
        // Arrange / Act: ring 30 / 20 and an island of 6 inside the hole
        var mesh = Extruder.Linear(new PolygonRegion([Rect(0, 0, 30, 30), Rect(5, 5, 20, 20), Rect(12, 12, 6, 6)]), 2.0);

        // Assert
        Assert.Equal(2, Render3DTestHelpers.CountConnectedShells(mesh));
        Assert.Equal((900.0 - 400.0 + 36.0) * 2.0, mesh.SignedVolume, 1e-9);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates the volume of a region with several holes.
    /// </summary>
    [Fact]
    public void Extruder_Linear_MultipleHoles_VolumeIsAnalytic()
    {
        // Arrange / Act
        var mesh = Variant("multiHole");

        // Assert: 200 - 9 - 18 - 16 = 157, height 2.5
        Assert.Equal(157.0 * 2.5, mesh.SignedVolume, 1e-9);
        Assert.Equal(1, Render3DTestHelpers.CountConnectedShells(mesh));
    }

    /// <summary>
    ///     Validates the exact volume of frusta produced by uniform scaling, for several slice counts.
    /// </summary>
    /// <param name="slices">The slice count.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Extruder_Linear_UniformScale_FrustumVolumeIsExact(int slices)
    {
        // Arrange: area 12 scaled to 0.5 at the top, so V = A h (1 + (s - 1) + (s - 1)^2 / 3)
        const double s = 0.5;
        var region = new PolygonRegion([LShape]);

        // Act
        var mesh = Extruder.Linear(region, 3.0, false, 0.0, slices, s);

        // Assert
        var expected = 12.0 * 3.0 * (1.0 + (s - 1.0) + ((s - 1.0) * (s - 1.0) / 3.0));
        Assert.Equal(expected, mesh.SignedVolume, expected * 1e-12);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a scale of zero produces a pyramid with a single apex vertex and no top cap.
    /// </summary>
    [Fact]
    public void Extruder_Linear_ScaleZero_IsPyramidWithSingleApex()
    {
        // Arrange / Act
        var mesh = Variant("pyramid");

        // Assert: volume A h / 3, one vertex at the top, no triangle lying entirely in the top plane
        Assert.Equal(12.0 * 3.0 / 3.0, mesh.SignedVolume, 1e-12);
        Assert.Single(mesh.Vertices, vertex => Math.Abs(vertex.Z - 3.0) < 1e-12);
        Assert.Equal(new Vector3D(0, 0, 3), mesh.Vertices.First(vertex => Math.Abs(vertex.Z - 3.0) < 1e-12));
        for (var t = 0; t < mesh.TriangleIndices.Count; t += 3)
        {
            var allOnTop = Enumerable.Range(0, 3).All(k => Math.Abs(mesh.Vertices[mesh.TriangleIndices[t + k]].Z - 3.0) < 1e-12);
            Assert.False(allOnTop);
        }

        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that an apex over a region with several contours (a hole, or two separate islands) is
    ///     rejected, because every contour would collapse onto one pinched vertex, while a single contour is accepted.
    /// </summary>
    [Fact]
    public void Extruder_Linear_ScaleZeroWithHoleOrIslands_Throws()
    {
        // Arrange
        var ring = RingRegion();
        var islands = new PolygonRegion([Rect(0, 0, 2, 2), Rect(5, 0, 2, 2)]);
        var single = new PolygonRegion([Rect(0, 0, 2, 2)]);

        // Act / Assert: both overloads reject, naming the scale
        var exception = Assert.Throws<ArgumentException>(() => Extruder.Linear(ring, 3.0, scale: Vector2D.Zero));
        Assert.Equal("scale", exception.ParamName);
        Assert.Throws<ArgumentException>(() => Extruder.Linear(ring, 3.0, false, 45.0, 4, Vector2D.Zero));
        Assert.Throws<ArgumentException>(() => Extruder.Linear(islands, 3.0, scale: Vector2D.Zero));
        Assert.Throws<ArgumentException>(() => Extruder.Linear(ring, 3.0, false, 0.0, 1, 0.0));

        // The same region with a positive scale and the single-contour apex still work
        Render3DTestHelpers.AssertValidCsgOperand(Extruder.Linear(ring, 3.0, scale: new Vector2D(0.5, 0.5)));
        Render3DTestHelpers.AssertValidCsgOperand(Extruder.Linear(single, 3.0, scale: Vector2D.Zero));
    }

    /// <summary>
    ///     Validates that a scale that is zero in only one axis is rejected for a region with a hole as well.
    /// </summary>
    [Fact]
    public void Extruder_Linear_ScaleWithOneZeroComponentAndHole_Throws()
    {
        // Arrange
        var ring = RingRegion();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(ring, 1.0, scale: new Vector2D(0.0, 1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(ring, 1.0, scale: new Vector2D(1.0, 0.0)));
    }

    /// <summary>
    ///     Validates that per-axis scaling converges to the analytic volume with many slices.
    /// </summary>
    [Fact]
    public void Extruder_Linear_PerAxisScale_VolumeConvergesToAnalytic()
    {
        // Arrange: area 12; scale x 1 -> 2 and y 1 -> 0.5, so the integral of (1 + t)(1 - t / 2) is 1 + 1/4 - 1/6
        var expected = 12.0 * 3.0 * (1.0 + 0.25 - (1.0 / 6.0));

        // Act
        var mesh = Extruder.Linear(new PolygonRegion([LShape]), 3.0, slices: 64, scale: new Vector2D(2.0, 0.5));

        // Assert
        Assert.Equal(expected, mesh.SignedVolume, expected * 1e-3);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a scale with exactly one zero component is rejected.
    /// </summary>
    [Fact]
    public void Extruder_Linear_ScaleWithOneZeroComponent_Throws()
    {
        // Arrange
        var region = new PolygonRegion([Rect(0, 0, 2, 2)]);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 1.0, scale: new Vector2D(0.0, 1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 1.0, scale: new Vector2D(1.0, 0.0)));
    }

    /// <summary>
    ///     Validates that negative and non-finite scales are rejected for both overloads.
    /// </summary>
    [Fact]
    public void Extruder_Linear_NegativeOrNonFiniteScale_Throws()
    {
        // Arrange
        var region = new PolygonRegion([Rect(0, 0, 2, 2)]);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 1.0, scale: new Vector2D(-1.0, 1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 1.0, scale: new Vector2D(1.0, double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 1.0, scale: new Vector2D(double.PositiveInfinity, 1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 1.0, false, 0.0, 1, -0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 1.0, false, 0.0, 1, double.NaN));
    }

    /// <summary>
    ///     Validates that twisted extrusions share ring vertices: V (slices + 1) vertices and a closed surface.
    /// </summary>
    [Fact]
    public void Extruder_Linear_Twist_SlicesShareRingVertices()
    {
        // Arrange / Act: 4 vertices, 5 slices
        var mesh = Extruder.Linear(new PolygonRegion([Rect(-1, -1, 2, 2)]), 4.0, twistDegrees: 45.0, slices: 5);

        // Assert
        Assert.Equal(4 * 6, mesh.VertexCount);
        Assert.True(mesh.IsClosed);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that a positive twist rotates the top counter-clockwise about +Z.
    /// </summary>
    [Fact]
    public void Extruder_Linear_Twist_PositiveRotatesTopCounterClockwise()
    {
        // Arrange: the first profile vertex is (1, 0)
        var region = new PolygonRegion([Rect(1, 0, 1, 1)]);

        // Act: ring 0 occupies the first four vertices, the top ring the next four
        var mesh = Extruder.Linear(region, 2.0, twistDegrees: 90.0, slices: 1);

        // Assert: (1, 0) turns to (0, 1) at the top
        Assert.Equal(new Vector3D(1, 0, 0), mesh.Vertices[0]);
        Render3DTestHelpers.AssertVector(new Vector3D(0, 1, 2), mesh.Vertices[4]);
    }

    /// <summary>
    ///     Validates the volume of a 90 degree twist against the analytic prism volume: the facet error is
    ///     first order in the twist per slice, so it is below one percent with 64 slices and shrinks as slices
    ///     are added.
    /// </summary>
    [Fact]
    public void Extruder_Linear_Twist_VolumeWithinToleranceOfAnalytic()
    {
        // Arrange: a twisted prism keeps its cross-section area, so the exact volume is A h = 16
        var region = new PolygonRegion([Rect(-1, -1, 2, 2)]);

        // Act
        var coarse = Extruder.Linear(region, 4.0, twistDegrees: 90.0, slices: 32);
        var fine = Extruder.Linear(region, 4.0, twistDegrees: 90.0, slices: 64);

        // Assert: within one percent, always below the smooth volume, and converging
        Assert.Equal(16.0, fine.SignedVolume, 16.0 * 1e-2);
        Assert.True(fine.SignedVolume < 16.0 + 1e-9, "Faceting can only remove volume of this twist.");
        Assert.True(16.0 - fine.SignedVolume < (16.0 - coarse.SignedVolume) / 1.5, "Adding slices must reduce the error.");
    }

    /// <summary>
    ///     Validates that a full 360 degree twist is closed, outward and a valid operand.
    /// </summary>
    [Fact]
    public void Extruder_Linear_Twist360_IsClosedOutwardValidOperand()
    {
        // Arrange / Act
        var mesh = Variant("twist360Ring");

        // Assert
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
        Assert.Equal(1, Render3DTestHelpers.CountConnectedShells(mesh));
    }

    /// <summary>
    ///     Validates that twist, slices and a collapse to an apex together stay closed.
    /// </summary>
    [Fact]
    public void Extruder_Linear_TwistAndScaleToApex_IsValidClosedMesh()
    {
        // Arrange / Act
        var mesh = Variant("twistApex");

        // Assert: V (S) ring vertices plus the apex
        Assert.Equal((10 * 8) + 1, mesh.VertexCount);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates the triangle count formula 2 V S + 2 (V + 2 H - 2 P).
    /// </summary>
    /// <param name="slices">The slice count.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Extruder_Linear_Slices_TriangleCountMatchesFormula(int slices)
    {
        // Arrange / Act
        var square = Extruder.Linear(new PolygonRegion([Rect(0, 0, 2, 2)]), 1.0, slices: slices);
        var ring = Extruder.Linear(RingRegion(), 1.0, slices: slices);

        // Assert: square V = 4, H = 0, P = 1; ring V = 8, H = 1, P = 1
        Assert.Equal((2 * 4 * slices) + (2 * (4 + 0 - 2)), square.TriangleCount);
        Assert.Equal((2 * 8 * slices) + (2 * (8 + 2 - 2)), ring.TriangleCount);
        Assert.Equal(4 * (slices + 1), square.VertexCount);
        Assert.Equal(8 * (slices + 1), ring.VertexCount);
    }

    /// <summary>
    ///     Validates that collinear contour points are used by wall and cap alike (no T-junction, closed).
    /// </summary>
    [Fact]
    public void Extruder_Linear_CollinearContourPoints_AreClosedWithoutTJunctions()
    {
        // Arrange / Act: a 2 x 2 square with a point in the middle of every side
        var mesh = Variant("collinear");

        // Assert
        Assert.Equal(8 * 2, mesh.VertexCount);
        Assert.Equal(8.0, mesh.SignedVolume, 1e-12);
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }

    /// <summary>
    ///     Validates that an empty region extrudes to an empty mesh.
    /// </summary>
    [Fact]
    public void Extruder_Linear_EmptyRegion_ReturnsEmptyMesh()
    {
        // Arrange / Act
        var mesh = Extruder.Linear(PolygonRegion.Empty, 5.0, twistDegrees: 30.0, slices: 4);

        // Assert
        Assert.Equal(0, mesh.VertexCount);
        Assert.Equal(0, mesh.TriangleCount);
    }

    /// <summary>
    ///     Validates that invalid arguments are rejected with the documented exceptions.
    /// </summary>
    [Fact]
    public void Extruder_Linear_InvalidArguments_Throw()
    {
        // Arrange
        var region = new PolygonRegion([Rect(0, 0, 2, 2)]);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Extruder.Linear(null!, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Extruder.Linear(region, 1.0, slices: 0));
        Assert.Throws<ArgumentException>(() => Extruder.Linear(region, 1.0, twistDegrees: double.NaN));
        Assert.Throws<ArgumentException>(() => Extruder.Linear(region, 1.0, twistDegrees: double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Extruder.Linear(region, 1.0, slices: int.MaxValue));
    }

    /// <summary>
    ///     Validates that a defective (reversed) result is rejected by the operand assertion, so the assertion
    ///     used by the other tests can really fail.
    /// </summary>
    [Fact]
    public void Extruder_Linear_ReversedResult_FailsOperandAssertion()
    {
        // Arrange
        var inverted = Extruder.Linear(new PolygonRegion([Rect(0, 0, 2, 2)]), 1.0).Reverse();

        // Act / Assert
        Assert.ThrowsAny<Exception>(() => Render3DTestHelpers.AssertValidCsgOperand(inverted));
    }

    /// <summary>
    ///     Validates that every tested parameter set yields a closed, outward, valid CSG operand.
    /// </summary>
    /// <param name="name">The variant name.</param>
    [Theory]
    [InlineData("square")]
    [InlineData("centered")]
    [InlineData("concave")]
    [InlineData("ring")]
    [InlineData("island")]
    [InlineData("multiHole")]
    [InlineData("frustum")]
    [InlineData("uniformFrustum")]
    [InlineData("pyramid")]
    [InlineData("perAxis")]
    [InlineData("twist")]
    [InlineData("twistScale")]
    [InlineData("twist360Ring")]
    [InlineData("twistApex")]
    [InlineData("collinear")]
    [InlineData("slices")]
    public void Extruder_Linear_AllVariants_AreValidCsgOperands(string name)
    {
        // Arrange / Act
        var mesh = Variant(name);

        // Assert
        Render3DTestHelpers.AssertValidCsgOperand(mesh);
    }
}
