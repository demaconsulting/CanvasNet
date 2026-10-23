using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the internal <c>TriangleRasterizer</c>, <c>SampleProjection</c> and
///     <c>DepthTestVisitor</c> (TriangleRasterizer unit).
/// </summary>
public class TriangleRasterizerTests
{
    /// <summary>The tangent of half the 22.5 degree field of view used for perspective tests.</summary>
    private static readonly double TanHalfFov = Math.Tan(22.5 * Math.PI / 360.0);

    /// <summary>
    ///     Creates an orthographic projection mapping view X to sample X one-to-one around the centre of a
    ///     64x64 sample grid (<c>sx = 32 + x</c>, <c>sy = 32 - y</c>).
    /// </summary>
    /// <returns>The projection.</returns>
    private static SampleProjection UnitOrthographic() => new(true, 1.0 / 32.0, 1.0 / 32.0, 64, 64, 0.1);

    /// <summary>
    ///     Creates a perspective projection for a square sample grid with the 22.5 degree field of view.
    /// </summary>
    /// <param name="size">The grid width and height in samples.</param>
    /// <returns>The projection.</returns>
    private static SampleProjection Perspective(int size) =>
        new(false, 1.0 / TanHalfFov, 1.0 / TanHalfFov, size, size, 0.1);

    /// <summary>
    ///     Creates a view-space triangle with identical front/back colours.
    /// </summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    /// <returns>The triangle.</returns>
    private static ViewTriangle View(ClipVertex a, ClipVertex b, ClipVertex c) =>
        new()
        {
            A = a,
            B = b,
            C = c,
            Priority = 3,
            FrontColor = new Rgba32(10, 20, 30, 255),
            BackColor = new Rgba32(200, 0, 0, 255),
        };

    /// <summary>
    ///     Validates that a right triangle with its legs on the sample grid lines covers exactly the
    ///     analytic number of sample centres: n(n-1)/2 (the hypotenuse is not owned).
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_AxisAlignedRightTriangle_CoversAnalyticSampleCount()
    {
        // Arrange: legs of 16 samples along the top and left edges
        var triangle = Render3DTestHelpers.MakeScreenTriangle((0, 0), (16, 0), (0, 16));
        var visitor = new CountingVisitor(16, 16);

        // Act
        TriangleRasterizer.Rasterize(in triangle, new TileRect(0, 0, 16, 16), ref visitor);

        // Assert: centres with i + j <= 14 are inside; i + j == 15 lies on the (unowned) hypotenuse
        Assert.Equal(16 * 15 / 2, visitor.Total);
        Assert.Equal(1, visitor.Counts[0]);
        Assert.Equal(0, visitor.Counts[(15 * 16) + 15]);
        Assert.All(visitor.Counts, count => Assert.InRange(count, 0, 1));
    }

    /// <summary>
    ///     Validates that vertex positions round to the nearest 1/256 sample.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_SubSampleVertexOffsets_RoundToNearestEighthOf256()
    {
        // Arrange: x = 3.0019 -> 35.0019 * 256 = 8960.49 (rounds down); x = 3.0021 -> 8960.54 (rounds up)
        var projection = UnitOrthographic();
        var below = new List<ScreenTriangle>();
        var above = new List<ScreenTriangle>();

        // Act
        TriangleRasterizer.Setup(View(new ClipVertex(3.0019, 1, -5), new ClipVertex(10, 1, -5), new ClipVertex(3.0019, 8, -5)), projection, below);
        TriangleRasterizer.Setup(View(new ClipVertex(3.0021, 1, -5), new ClipVertex(10, 1, -5), new ClipVertex(3.0021, 8, -5)), projection, above);

        // Assert
        var b = Assert.Single(below);
        var a = Assert.Single(above);
        Assert.Contains(8960, new[] { b.X0, b.X1, b.X2 });
        Assert.Contains(8961, new[] { a.X0, a.X1, a.X2 });
        Assert.DoesNotContain(8961, new[] { b.X0, b.X1, b.X2 });
    }

    /// <summary>
    ///     Validates that two triangles sharing a diagonal that passes exactly through sample centres
    ///     cover every sample exactly once.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_SharedEdgeOnSampleCenters_CoveredExactlyOnce()
    {
        // Arrange: an 8x8 quad whose corners are sample centres, split along the A-C diagonal
        var t1 = Render3DTestHelpers.MakeScreenTriangle((0.5, 0.5), (8.5, 0.5), (8.5, 8.5));
        var t2 = Render3DTestHelpers.MakeScreenTriangle((0.5, 0.5), (8.5, 8.5), (0.5, 8.5));
        var visitor = new CountingVisitor(12, 12);

        // Act
        TriangleRasterizer.Rasterize(in t1, new TileRect(0, 0, 12, 12), ref visitor);
        TriangleRasterizer.Rasterize(in t2, new TileRect(0, 0, 12, 12), ref visitor);

        // Assert: left/top boundary samples are owned, right/bottom are not => exactly the 8x8 block, once each
        Assert.Equal(64, visitor.Total);
        for (var y = 0; y < 12; y++)
        {
            for (var x = 0; x < 12; x++)
            {
                var expected = x < 8 && y < 8 ? 1 : 0;
                Assert.Equal(expected, visitor.Counts[(y * 12) + x]);
            }
        }
    }

    /// <summary>
    ///     Validates that a fan of triangles around a shared vertex covers each interior sample exactly once.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_TriangleFanAroundVertex_CoversEachSampleOnce()
    {
        // Arrange: an octagon around (20.3, 20.7) as an eight-triangle fan with fixed-point-exact vertices
        var centre = (X: 20.3, Y: 20.7);
        var ring = new (double X, double Y)[8];
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4.0;
            ring[i] = (Math.Round(256.0 * (centre.X + (12.0 * Math.Cos(angle)))) / 256.0, Math.Round(256.0 * (centre.Y + (12.0 * Math.Sin(angle)))) / 256.0);
        }

        var visitor = new CountingVisitor(40, 40);

        // Act
        for (var i = 0; i < 8; i++)
        {
            var triangle = Render3DTestHelpers.MakeScreenTriangle(centre, ring[i], ring[(i + 1) % 8]);
            TriangleRasterizer.Rasterize(in triangle, new TileRect(0, 0, 40, 40), ref visitor);
        }

        // Assert: no sample is hit twice, and the total equals the number of centres inside the convex octagon
        Assert.All(visitor.Counts, count => Assert.InRange(count, 0, 1));
        var expected = 0;
        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                var inside = true;
                for (var i = 0; i < 8 && inside; i++)
                {
                    var a = ring[i];
                    var b = ring[(i + 1) % 8];
                    var cross = ((b.X - a.X) * ((y + 0.5) - a.Y)) - ((b.Y - a.Y) * ((x + 0.5) - a.X));
                    Assert.NotEqual(0.0, cross);
                    inside = cross > 0.0;
                }

                expected += inside ? 1 : 0;
            }
        }

        Assert.Equal(expected, visitor.Total);
    }

    /// <summary>
    ///     Validates, over 200 seeded random jittered grid triangulations, that every sample inside the
    ///     grid's (lattice-aligned) boundary is covered exactly once and every sample outside none.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_RandomTriangulatedGrid_EveryInteriorSampleCoveredOnceNoneOutside()
    {
        // Arrange
        const int cells = 8;
        const int cellSize = 5;
        const int gridSize = cells * cellSize;
        const int margin = 3;
        const int canvas = gridSize + (2 * margin);

        for (var seed = 0; seed < 200; seed++)
        {
            var random = new Random(seed);

            // Boundary vertices stay on the integer lattice; interior vertices are jittered by up to +/-1 sample in 1/256 steps
            var vertices = new (double X, double Y)[cells + 1, cells + 1];
            for (var j = 0; j <= cells; j++)
            {
                for (var i = 0; i <= cells; i++)
                {
                    var interior = i > 0 && i < cells && j > 0 && j < cells;
                    var jitterX = interior ? random.Next(-256, 257) / 256.0 : 0.0;
                    var jitterY = interior ? random.Next(-256, 257) / 256.0 : 0.0;
                    vertices[i, j] = (margin + (i * cellSize) + jitterX, margin + (j * cellSize) + jitterY);
                }
            }

            var visitor = new CountingVisitor(canvas, canvas);

            // Act: two triangles per cell with alternating diagonals
            for (var j = 0; j < cells; j++)
            {
                for (var i = 0; i < cells; i++)
                {
                    var p00 = vertices[i, j];
                    var p10 = vertices[i + 1, j];
                    var p01 = vertices[i, j + 1];
                    var p11 = vertices[i + 1, j + 1];
                    (double X, double Y)[][] triangles = ((i + j) & 1) == 0
                        ? [[p00, p10, p11], [p00, p11, p01]]
                        : [[p00, p10, p01], [p10, p11, p01]];
                    foreach (var t in triangles)
                    {
                        var triangle = Render3DTestHelpers.MakeScreenTriangle(t[0], t[1], t[2]);
                        TriangleRasterizer.Rasterize(in triangle, new TileRect(0, 0, canvas, canvas), ref visitor);
                    }
                }
            }

            // Assert: inside the lattice square exactly once, outside never
            for (var y = 0; y < canvas; y++)
            {
                for (var x = 0; x < canvas; x++)
                {
                    var inside = x >= margin && x < margin + gridSize && y >= margin && y < margin + gridSize;
                    Assert.True(
                        visitor.Counts[(y * canvas) + x] == (inside ? 1 : 0),
                        $"Seed {seed}: sample ({x},{y}) was hit {visitor.Counts[(y * canvas) + x]} times.");
                }
            }
        }
    }

    /// <summary>
    ///     Validates that a counter-clockwise (outward) quad facing the camera is front-facing and
    ///     takes the front colour.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Setup_CounterClockwiseOutwardQuad_IsFrontFacing()
    {
        // Arrange: a quad at depth 5, counter-clockwise as seen from the camera (x right, y up)
        var projection = UnitOrthographic();
        var output = new List<ScreenTriangle>();
        ClipVertex bl = new(-4, -4, -5);
        ClipVertex br = new(4, -4, -5);
        ClipVertex tr = new(4, 4, -5);
        ClipVertex tl = new(-4, 4, -5);

        // Act
        TriangleRasterizer.Setup(View(bl, br, tr), projection, output);
        TriangleRasterizer.Setup(View(bl, tr, tl), projection, output);

        // Assert
        Assert.Equal(2, output.Count);
        Assert.All(output, t =>
        {
            Assert.False(t.BackFacing);
            Assert.Equal(new Rgba32(10, 20, 30, 255), t.Color);
            Assert.True(t.Area2 > 0);
            Assert.Equal(3, t.Priority);
            Assert.False(t.PerspectiveDepth);
        });
    }

    /// <summary>
    ///     Validates that reversing the winding makes the triangle back-facing with the back colour.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Setup_ReversedWinding_IsBackFacing()
    {
        // Arrange: the same triangle with two vertices swapped
        var projection = UnitOrthographic();
        var output = new List<ScreenTriangle>();

        // Act
        TriangleRasterizer.Setup(View(new ClipVertex(-4, -4, -5), new ClipVertex(4, 4, -5), new ClipVertex(4, -4, -5)), projection, output);

        // Assert
        var triangle = Assert.Single(output);
        Assert.True(triangle.BackFacing);
        Assert.Equal(new Rgba32(200, 0, 0, 255), triangle.Color);
        Assert.True(triangle.Area2 > 0);
    }

    /// <summary>
    ///     Validates that collinear vertices yield no triangle.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Setup_CollinearVertices_ProducesNoTriangle()
    {
        // Arrange
        var output = new List<ScreenTriangle>();

        // Act
        TriangleRasterizer.Setup(View(new ClipVertex(0, 0, -5), new ClipVertex(1, 1, -5), new ClipVertex(2, 2, -5)), UnitOrthographic(), output);

        // Assert
        Assert.Empty(output);
    }

    /// <summary>
    ///     Validates that a triangle with a non-finite vertex is skipped.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Setup_NonFiniteVertex_IsSkipped()
    {
        // Arrange
        var output = new List<ScreenTriangle>();

        // Act
        TriangleRasterizer.Setup(View(new ClipVertex(double.NaN, 0, -5), new ClipVertex(1, 0, -5), new ClipVertex(0, 1, -5)), UnitOrthographic(), output);
        TriangleRasterizer.Setup(View(new ClipVertex(0, 0, -5), new ClipVertex(double.PositiveInfinity, 0, -5), new ClipVertex(0, 1, -5)), UnitOrthographic(), output);

        // Assert
        Assert.Empty(output);
    }

    /// <summary>
    ///     Validates that a triangle straddling the near plane is clipped and that one entirely behind it vanishes.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Setup_TriangleBehindEye_ProducesNothing()
    {
        // Arrange: perspective projection with near 0.1; every vertex is behind the eye (positive Z)
        var output = new List<ScreenTriangle>();

        // Act
        TriangleRasterizer.Setup(View(new ClipVertex(0, 0, 5), new ClipVertex(1, 0, 5), new ClipVertex(0, 1, 5)), Perspective(64), output);

        // Assert
        Assert.Empty(output);
    }

    /// <summary>
    ///     Validates that a huge triangle is clipped to the guard band so fixed-point values stay
    ///     within range and the edge-function arithmetic cannot overflow.
    /// </summary>
    [Fact]
    public void TriangleClipper_GuardBand_HugeTriangle_ClipsAndStaysWithinFixedPointRange()
    {
        // Arrange: view-space extents of 1e9 and a vertex barely in front of the near plane
        var output = new List<ScreenTriangle>();

        // Act
        TriangleRasterizer.Setup(View(new ClipVertex(-1e9, -1e9, -5), new ClipVertex(1e9, -1e9, -5), new ClipVertex(0, 1e9, -5)), UnitOrthographic(), output);
        TriangleRasterizer.Setup(View(new ClipVertex(-1e6, -1, -5), new ClipVertex(1e6, -1, -5), new ClipVertex(1000, 1, -0.1000001)), Perspective(64), output);

        // Assert: every coordinate within (2^20 + half the grid) samples, so |fixed| < 2^29 and products fit in 64 bits
        Assert.NotEmpty(output);
        const long limit = (1L << 28) + (1L << 22);
        foreach (var t in output)
        {
            Assert.All(new[] { t.X0, t.Y0, t.X1, t.Y1, t.X2, t.Y2 }, v => Assert.InRange((long)v, -limit, limit));
            Assert.True(t.Area2 > 0);
        }
    }

    /// <summary>
    ///     Validates that a huge triangle covering a whole tile covers every sample exactly once.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_HugeTriangleCoveringTile_CoversAllSamples()
    {
        // Arrange: a perspective triangle with extents far beyond the guard band
        var projection = Perspective(32);
        var output = new List<ScreenTriangle>();
        TriangleRasterizer.Setup(View(new ClipVertex(-1e7, -1e7, -5), new ClipVertex(1e7, -1e7, -5), new ClipVertex(0, 1e7, -5)), projection, output);
        var visitor = new CountingVisitor(32, 32);

        // Act
        foreach (var triangle in output)
        {
            TriangleRasterizer.Rasterize(in triangle, new TileRect(0, 0, 32, 32), ref visitor);
        }

        // Assert
        Assert.Equal(32 * 32, visitor.Total);
        Assert.All(visitor.Counts, count => Assert.Equal(1, count));
    }

    /// <summary>
    ///     Validates perspective-correct depth on a slanted plane against the analytic ray-plane intersection.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_PerspectivePlane_DepthIsPerspectiveCorrect()
    {
        // Arrange: the plane z = -10 + 0.2 x + 0.1 y (view space), sampled on a 64x64 grid
        const int size = 64;
        static ClipVertex OnPlane(double x, double y) => new(x, y, -10.0 + (0.2 * x) + (0.1 * y));
        var output = new List<ScreenTriangle>();
        TriangleRasterizer.Setup(View(OnPlane(-8, -8), OnPlane(8, -8), OnPlane(0, 8)), Perspective(size), output);
        var visitor = new CountingVisitor(size, size);

        // Act
        foreach (var triangle in output)
        {
            TriangleRasterizer.Rasterize(in triangle, new TileRect(0, 0, size, size), ref visitor);
        }

        // Assert: depth = 10 / (1 + 0.2 rx + 0.1 ry) with rx, ry the view-ray slopes at each sample centre
        Assert.Equal(size * size, visitor.Total);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var rx = ((x + 0.5 - (size / 2.0)) / (size / 2.0)) * TanHalfFov;
                var ry = (((size / 2.0) - (y + 0.5)) / (size / 2.0)) * TanHalfFov;
                var expected = 10.0 / (1.0 + (0.2 * rx) + (0.1 * ry));
                Assert.Equal(expected, visitor.Depths[(y * size) + x], 2e-3);
            }
        }
    }

    /// <summary>
    ///     Validates that orthographic depth varies linearly across a slanted plane.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_OrthographicSlantedPlane_DepthIsLinear()
    {
        // Arrange: depth = 20 - 0.2 x - 0.1 y; projection sx = 32 + x, sy = 32 - y
        static ClipVertex OnPlane(double x, double y) => new(x, y, -(20.0 - (0.2 * x) - (0.1 * y)));
        var output = new List<ScreenTriangle>();
        TriangleRasterizer.Setup(View(OnPlane(-300, -300), OnPlane(300, -300), OnPlane(0, 400)), UnitOrthographic(), output);
        var visitor = new CountingVisitor(64, 64);

        // Act
        foreach (var triangle in output)
        {
            TriangleRasterizer.Rasterize(in triangle, new TileRect(0, 0, 64, 64), ref visitor);
        }

        // Assert
        Assert.Equal(64 * 64, visitor.Total);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var wx = x + 0.5 - 32.0;
                var wy = 32.0 - (y + 0.5);
                Assert.Equal(20.0 - (0.2 * wx) - (0.1 * wy), visitor.Depths[(y * 64) + x], 2e-3);
            }
        }
    }

    /// <summary>
    ///     Validates that rasterization is restricted to the tile rectangle.
    /// </summary>
    [Fact]
    public void TriangleRasterizer_Rasterize_TileRectangle_RestrictsCoverage()
    {
        // Arrange: a 16x16 right triangle, rasterized only into the right half of the grid
        var triangle = Render3DTestHelpers.MakeScreenTriangle((0, 0), (16, 0), (0, 16));
        var visitor = new CountingVisitor(16, 16);

        // Act
        TriangleRasterizer.Rasterize(in triangle, new TileRect(8, 0, 16, 16), ref visitor);

        // Assert: only columns 8..15 can be hit; columns i + j <= 14 => for i >= 8, j <= 14 - i
        Assert.Equal(7 * 8 / 2, visitor.Total);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                Assert.Equal(0, visitor.Counts[(y * 16) + x]);
            }
        }
    }

    /// <summary>
    ///     Validates the sample projection of the view centre and an off-centre point.
    /// </summary>
    [Fact]
    public void SampleProjection_ToSample_CentreAndOffCentre_MatchAnalytic()
    {
        // Arrange
        var perspective = Perspective(100);
        var ortho = UnitOrthographic();

        // Act
        var centre = perspective.ToSample(new ClipVertex(0, 0, -7));
        var offset = perspective.ToSample(new ClipVertex(7 * TanHalfFov, 7 * TanHalfFov, -7));
        var orthoPoint = ortho.ToSample(new ClipVertex(5, 3, -99));

        // Assert: the view-centre maps to the grid centre; x = depth * tan(fov/2) maps to the right edge, y up maps to the top edge
        Assert.Equal(50.0, centre.X, 1e-9);
        Assert.Equal(50.0, centre.Y, 1e-9);
        Assert.Equal(100.0, offset.X, 1e-9);
        Assert.Equal(0.0, offset.Y, 1e-9);
        Assert.Equal(37.0, orthoPoint.X, 1e-9);
        Assert.Equal(29.0, orthoPoint.Y, 1e-9);
    }

    /// <summary>The metadata-bearing triangle used by the depth-test visitor tests.</summary>
    /// <param name="priority">The draw priority.</param>
    /// <param name="back">Whether the triangle is back-facing.</param>
    /// <param name="red">The red channel used to identify the winner.</param>
    /// <returns>The triangle.</returns>
    private static ScreenTriangle Tagged(int priority, bool back, byte red) =>
        new() { Priority = priority, BackFacing = back, Color = new Rgba32(red, 0, 0, 255) };

    /// <summary>
    ///     Validates that fragments within the depth tolerance are resolved by priority (lower wins).
    /// </summary>
    [Fact]
    public void DepthTestVisitor_Visit_CoincidentWithinTolerance_LowerPriorityWins()
    {
        // Arrange
        var workspace = new TileWorkspace(16);
        workspace.Begin(0, 0, 4, 4);
        const float eps = 0.01f;
        var first = Tagged(priority: 5, back: false, red: 5);
        var better = Tagged(priority: 2, back: false, red: 2);
        var worse = Tagged(priority: 9, back: false, red: 9);

        // Act
        new DepthTestVisitor(workspace, in first, eps, 100f).Visit(1, 1, 10.0f);
        new DepthTestVisitor(workspace, in worse, eps, 100f).Visit(1, 1, 10.0f);
        var afterWorse = workspace.Colors[workspace.IndexOf(1, 1)].R;
        new DepthTestVisitor(workspace, in better, eps, 100f).Visit(1, 1, 10.0f + (eps * 0.5f));

        // Assert
        Assert.Equal(5, afterWorse);
        Assert.Equal(2, workspace.Colors[workspace.IndexOf(1, 1)].R);
    }

    /// <summary>
    ///     Validates that a front-facing fragment beats a back-facing one within tolerance regardless of priority.
    /// </summary>
    [Fact]
    public void DepthTestVisitor_Visit_FrontBeatsBackWithinTolerance()
    {
        // Arrange
        var workspace = new TileWorkspace(16);
        workspace.Begin(0, 0, 4, 4);
        const float eps = 0.01f;
        var back = Tagged(priority: 1, back: true, red: 1);
        var front = Tagged(priority: 7, back: false, red: 7);

        // Act
        new DepthTestVisitor(workspace, in back, eps, 100f).Visit(2, 3, 10.0f);
        new DepthTestVisitor(workspace, in front, eps, 100f).Visit(2, 3, 10.0f + (eps * 0.5f));

        // Assert
        Assert.Equal(7, workspace.Colors[workspace.IndexOf(2, 3)].R);
    }

    /// <summary>
    ///     Validates that beyond the tolerance the nearer fragment always wins, whatever its priority.
    /// </summary>
    [Fact]
    public void DepthTestVisitor_Visit_BeyondTolerance_NearerWins()
    {
        // Arrange
        var workspace = new TileWorkspace(16);
        workspace.Begin(0, 0, 4, 4);
        const float eps = 0.01f;
        var first = Tagged(priority: 1, back: false, red: 1);
        var nearer = Tagged(priority: 9, back: false, red: 9);
        var farther = Tagged(priority: 0, back: false, red: 0);

        // Act
        new DepthTestVisitor(workspace, in first, eps, 100f).Visit(0, 0, 10.0f);
        new DepthTestVisitor(workspace, in farther, eps, 100f).Visit(0, 0, 10.0f + (eps * 2));
        var afterFarther = workspace.Colors[0].R;
        new DepthTestVisitor(workspace, in nearer, eps, 100f).Visit(0, 0, 10.0f - (eps * 2));

        // Assert
        Assert.Equal(1, afterFarther);
        Assert.Equal(9, workspace.Colors[0].R);
        Assert.Equal(10.0f - (eps * 2), workspace.Depth[0]);
    }

    /// <summary>
    ///     Validates that the stored depth after a tie is the smaller of the two depths, even when the old fragment stays.
    /// </summary>
    [Fact]
    public void DepthTestVisitor_Visit_TieWinner_StoresMinimumDepth()
    {
        // Arrange
        var workspace = new TileWorkspace(16);
        workspace.Begin(0, 0, 4, 4);
        const float eps = 0.01f;
        var first = Tagged(priority: 1, back: false, red: 1);
        var loser = Tagged(priority: 4, back: false, red: 4);

        // Act: the later fragment is slightly nearer but loses the priority tie
        new DepthTestVisitor(workspace, in first, eps, 100f).Visit(3, 3, 10.0f);
        new DepthTestVisitor(workspace, in loser, eps, 100f).Visit(3, 3, 10.0f - (eps * 0.5f));

        // Assert
        Assert.Equal(1, workspace.Colors[workspace.IndexOf(3, 3)].R);
        Assert.Equal(10.0f - (eps * 0.5f), workspace.Depth[workspace.IndexOf(3, 3)]);
    }

    /// <summary>
    ///     Validates that fragments beyond the far distance are discarded.
    /// </summary>
    [Fact]
    public void DepthTestVisitor_Visit_BeyondFar_IsDiscarded()
    {
        // Arrange
        var workspace = new TileWorkspace(16);
        workspace.Begin(0, 0, 4, 4);
        var triangle = Tagged(priority: 0, back: false, red: 8);

        // Act
        new DepthTestVisitor(workspace, in triangle, 0.01f, 50f).Visit(1, 2, 60.0f);

        // Assert: nothing stored
        Assert.True(float.IsPositiveInfinity(workspace.Depth[workspace.IndexOf(1, 2)]));
    }
}
