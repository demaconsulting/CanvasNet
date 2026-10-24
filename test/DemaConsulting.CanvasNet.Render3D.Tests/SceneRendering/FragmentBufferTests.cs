using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the internal <c>FragmentBuffer</c> (FragmentBuffer unit).
/// </summary>
public class FragmentBufferTests
{
    /// <summary>The 16x16 sample tile used by most tests.</summary>
    private static readonly TileRect Tile = new(0, 0, 16, 16);

    /// <summary>
    ///     Creates the lower-left half of the 16x16 tile (120 covered samples).
    /// </summary>
    /// <param name="depth">The constant depth.</param>
    /// <returns>The triangle.</returns>
    private static ScreenTriangle LowerHalf(float depth) =>
        Render3DTestHelpers.MakeScreenTriangle((0, 0), (16, 0), (0, 16), depth);

    /// <summary>
    ///     Creates the upper-right half of the 16x16 tile (136 covered samples; together with <see cref="LowerHalf"/> every sample once).
    /// </summary>
    /// <param name="depth">The constant depth.</param>
    /// <returns>The triangle.</returns>
    private static ScreenTriangle UpperHalf(float depth) =>
        Render3DTestHelpers.MakeScreenTriangle((16, 0), (16, 16), (0, 16), depth);

    /// <summary>
    ///     Runs the count pass, ends it and runs the fill pass for the triangles in the given order.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="tile">The tile to collect.</param>
    /// <param name="triangles">The triangles; their indices are their positions in this list.</param>
    private static void Collect(FragmentBuffer buffer, TileRect tile, IReadOnlyList<ScreenTriangle> triangles)
    {
        buffer.Begin(tile.X0, tile.Y0, tile.Width, tile.Height);
        for (var i = 0; i < triangles.Count; i++)
        {
            buffer.CountTriangle(triangles[i]);
        }

        buffer.EndCounting();
        for (var i = 0; i < triangles.Count; i++)
        {
            buffer.AddTriangle(i, triangles[i]);
        }
    }

    /// <summary>
    ///     Validates that every sample holds exactly the fragments the rasterizer reports for each triangle.
    /// </summary>
    [Fact]
    public void FragmentBuffer_Collect_OverlappingTriangles_HoldsEveryRasterizerHitPerSample()
    {
        // Arrange: three overlapping triangles at different depths
        var triangles = new List<ScreenTriangle>
        {
            LowerHalf(3.0f),
            Render3DTestHelpers.MakeScreenTriangle((2, 2), (14, 2), (8, 14), 1.5f),
            UpperHalf(6.0f),
        };
        var expected = new List<(float Depth, int Triangle)>[256];
        for (var s = 0; s < expected.Length; s++)
        {
            expected[s] = [];
        }

        for (var i = 0; i < triangles.Count; i++)
        {
            var visitor = new CountingVisitor(16, 16);
            var triangle = triangles[i];
            TriangleRasterizer.Rasterize(in triangle, Tile, ref visitor);
            for (var s = 0; s < expected.Length; s++)
            {
                if (visitor.Counts[s] == 1)
                {
                    expected[s].Add((visitor.Depths[s], i));
                }
            }
        }

        var buffer = new FragmentBuffer(1000);

        // Act
        Collect(buffer, Tile, triangles);

        // Assert: per sample, the same (depth, triangle) pairs in insertion (triangle) order
        for (var s = 0; s < expected.Length; s++)
        {
            var actual = buffer.GetSample(s).ToArray().Select(f => (f.Depth, f.Triangle)).ToList();
            Assert.Equal(expected[s], actual);
        }

        Assert.Equal(expected.Sum(e => e.Count), buffer.FragmentCount);
        Assert.Equal(256, buffer.SampleCount);
    }

    /// <summary>
    ///     Validates that the storage is allocated exactly as large as the first tile needs and that fragments beyond any far distance are kept.
    /// </summary>
    [Fact]
    public void FragmentBuffer_EndCounting_FirstTile_AllocatesExactlyTheNeededFragments()
    {
        // Arrange: 120 + 136 + 120 fragments, one of them very deep
        var triangles = new List<ScreenTriangle> { LowerHalf(1.0f), UpperHalf(2.0f), LowerHalf(1.0e9f) };
        var buffer = new FragmentBuffer(10_000);

        // Act
        Collect(buffer, Tile, triangles);

        // Assert: exact size and the deep fragments are retained (no depth filtering here)
        Assert.Equal(376, buffer.Capacity);
        Assert.Equal(376L, buffer.FragmentCount);
        Assert.Contains(buffer.GetSample(0).ToArray(), f => f.Depth > 1.0e8f);
    }

    /// <summary>
    ///     Validates that a second, smaller tile reuses the storage without stale fragments and a larger one grows it to exactly its need.
    /// </summary>
    [Fact]
    public void FragmentBuffer_Begin_ReusedAcrossTiles_ReusesStorageAndNeverLeaksStaleFragments()
    {
        // Arrange
        var buffer = new FragmentBuffer(10_000);
        Collect(buffer, Tile, [LowerHalf(1.0f), UpperHalf(2.0f), LowerHalf(3.0f)]);
        var firstCapacity = buffer.Capacity;

        // Act: a smaller tile (one half only) reuses the storage
        Collect(buffer, Tile, [UpperHalf(2.0f)]);
        var upperOnlyCounts = Enumerable.Range(0, 256).Select(s => buffer.GetSample(s).Length).ToArray();
        var reusedCapacity = buffer.Capacity;

        // Act: a larger tile grows to exactly its need
        Collect(buffer, Tile, [LowerHalf(1.0f), UpperHalf(2.0f), LowerHalf(3.0f), UpperHalf(4.0f)]);

        // Assert
        Assert.Equal(376, firstCapacity);
        Assert.Equal(376, reusedCapacity);
        Assert.Equal(136, upperOnlyCounts.Sum());
        Assert.All(upperOnlyCounts, c => Assert.InRange(c, 0, 1));
        Assert.Equal(512, buffer.Capacity);
    }

    /// <summary>
    ///     Validates that only the samples inside the tile rectangle are collected and a tile at a non-zero origin is addressed locally.
    /// </summary>
    [Fact]
    public void FragmentBuffer_Collect_TileAtOffset_CollectsOnlySamplesInsideTile()
    {
        // Arrange: a triangle covering the whole 16x16 area, but the tile is the 8x8 block at (8, 8)
        var buffer = new FragmentBuffer(10_000);
        var tile = new TileRect(8, 8, 16, 16);

        // Act
        Collect(buffer, tile, [LowerHalf(1.0f), UpperHalf(1.0f)]);

        // Assert: every one of the 64 samples is covered by exactly one half
        Assert.Equal(64L, buffer.FragmentCount);
        for (var s = 0; s < 64; s++)
        {
            Assert.Equal(1, buffer.GetSample(s).Length);
        }
    }

    /// <summary>
    ///     Validates that a tile holding exactly the budget is accepted and a tile one fragment above it throws.
    /// </summary>
    [Fact]
    public void FragmentBuffer_CountTriangle_ExactlyAtBudget_Succeeds_OneAboveBudget_Throws()
    {
        // Arrange: the two halves cover 256 samples once each
        var atBudget = new FragmentBuffer(256);
        var belowBudget = new FragmentBuffer(255);

        // Act
        Collect(atBudget, Tile, [LowerHalf(1.0f), UpperHalf(1.0f)]);
        var exception = Assert.Throws<RenderBudgetExceededException>(
            () => Collect(belowBudget, Tile, [LowerHalf(1.0f), UpperHalf(1.0f)]));

        // Assert
        Assert.Equal(256L, atBudget.FragmentCount);
        Assert.Equal(255L, exception.MaxFragmentsPerTile);
    }

    /// <summary>
    ///     Validates that whether the budget is exceeded depends only on the set of triangles, never on their order, and the message never mentions the tile.
    /// </summary>
    [Fact]
    public void FragmentBuffer_CountTriangle_BudgetOutcomeAndMessage_IndependentOfOrderAndTile()
    {
        // Arrange: 376 fragments in total
        ScreenTriangle[] forward = [LowerHalf(1.0f), UpperHalf(2.0f), LowerHalf(3.0f)];
        ScreenTriangle[] reverse = [LowerHalf(3.0f), UpperHalf(2.0f), LowerHalf(1.0f)];

        // Act
        var first = Assert.Throws<RenderBudgetExceededException>(() => Collect(new FragmentBuffer(100), Tile, forward));
        var second = Assert.Throws<RenderBudgetExceededException>(() => Collect(new FragmentBuffer(100), Tile, reverse));
        var otherTile = Assert.Throws<RenderBudgetExceededException>(() => Collect(new FragmentBuffer(100), new TileRect(0, 0, 8, 8), forward));

        // Assert: identical, tile-independent exception content
        Assert.Equal(first.Message, second.Message);
        Assert.Equal(first.Message, otherTile.Message);
        Assert.Equal(100L, first.MaxFragmentsPerTile);
        Collect(new FragmentBuffer(376), Tile, forward);
        Collect(new FragmentBuffer(376), Tile, reverse);
    }

    /// <summary>
    ///     Validates that a buffer is usable again for the next tile after the budget was exceeded.
    /// </summary>
    [Fact]
    public void FragmentBuffer_Begin_AfterBudgetExceeded_CollectsNextTile()
    {
        // Arrange
        var buffer = new FragmentBuffer(130);
        Assert.Throws<RenderBudgetExceededException>(() => Collect(buffer, Tile, [LowerHalf(1.0f), UpperHalf(1.0f)]));

        // Act
        Collect(buffer, Tile, [LowerHalf(1.0f)]);

        // Assert
        Assert.Equal(120L, buffer.FragmentCount);
    }

    /// <summary>
    ///     Validates the budget validation of the constructor.
    /// </summary>
    /// <param name="budget">An invalid budget.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    [InlineData((long)int.MaxValue + 10L)]
    public void FragmentBuffer_Constructor_InvalidBudget_ThrowsArgumentOutOfRangeException(long budget)
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new FragmentBuffer(budget));
    }

    /// <summary>
    ///     Validates that the passes must be run in order.
    /// </summary>
    [Fact]
    public void FragmentBuffer_Passes_OutOfOrder_ThrowsInvalidOperationException()
    {
        // Arrange
        var buffer = new FragmentBuffer(1000);
        var triangle = LowerHalf(1.0f);

        // Act / Assert: nothing may be used before Begin, fill needs EndCounting, counting ends at EndCounting
        Assert.Throws<InvalidOperationException>(() => buffer.CountTriangle(triangle));
        buffer.Begin(0, 0, 16, 16);
        Assert.Throws<InvalidOperationException>(() => buffer.AddTriangle(0, triangle));
        Assert.Throws<InvalidOperationException>(() => buffer.GetSample(0));
        buffer.CountTriangle(triangle);
        buffer.EndCounting();
        Assert.Throws<InvalidOperationException>(() => buffer.CountTriangle(triangle));
        Assert.Throws<InvalidOperationException>(buffer.EndCounting);
    }

    /// <summary>
    ///     Validates that reading a sample whose fragments were not all stored is reported.
    /// </summary>
    [Fact]
    public void FragmentBuffer_GetSample_IncompleteFill_ThrowsInvalidOperationException()
    {
        // Arrange: counted but never filled
        var buffer = new FragmentBuffer(1000);
        buffer.Begin(0, 0, 16, 16);
        buffer.CountTriangle(LowerHalf(1.0f));
        buffer.EndCounting();

        // Act / Assert: sample 0 is covered, so its run is still empty
        Assert.Throws<InvalidOperationException>(() => buffer.GetSample(0));
    }
}
