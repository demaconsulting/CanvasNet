// cspell:ignore Binner

using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the internal <c>TileBinner</c> (SceneRenderer unit).
/// </summary>
public class TileBinnerTests
{
    /// <summary>
    ///     Validates that a triangle spanning several tiles appears in every overlapped tile, with each tile's list in ascending triangle order.
    /// </summary>
    [Fact]
    public void TileBinner_Bin_TriangleSpanningTiles_AppearsInEachOverlappedTileInAscendingOrder()
    {
        // Arrange: 3 x 2 tiles of 32 samples (a 96 x 64 grid)
        var triangles = new[]
        {
            Render3DTestHelpers.MakeScreenTriangle((2, 2), (40, 2), (2, 40)),    // 0: tiles (0,0) (1,0) (0,1) (1,1)
            Render3DTestHelpers.MakeScreenTriangle((70, 5), (90, 5), (70, 25)),   // 1: tile (2,0) only
            Render3DTestHelpers.MakeScreenTriangle((30, 30), (66, 30), (30, 34)), // 2: tiles (0,0) (1,0) (2,0) (0,1) (1,1) (2,1)
        };

        // Act
        var bins = TileBinner.Bin(triangles, 3, 2, 32);

        // Assert
        Assert.Equal(3, bins.TilesX);
        Assert.Equal(2, bins.TilesY);
        Assert.Equal(new[] { 0, 2 }, bins.GetTriangles(0).ToArray());
        Assert.Equal(new[] { 0, 2 }, bins.GetTriangles(1).ToArray());
        Assert.Equal(new[] { 1, 2 }, bins.GetTriangles(2).ToArray());
        Assert.Equal(new[] { 0, 2 }, bins.GetTriangles(3).ToArray());
        Assert.Equal(new[] { 0, 2 }, bins.GetTriangles(4).ToArray());
        Assert.Equal(new[] { 2 }, bins.GetTriangles(5).ToArray());
    }

    /// <summary>
    ///     Validates that triangles entirely off the grid, or covering no sample centre, are not binned anywhere.
    /// </summary>
    [Fact]
    public void TileBinner_Bin_OffGridTriangles_AreSkipped()
    {
        // Arrange
        var triangles = new[]
        {
            Render3DTestHelpers.MakeScreenTriangle((-50, -50), (-10, -50), (-50, -10)),
            Render3DTestHelpers.MakeScreenTriangle((200, 200), (230, 200), (200, 230)),
            Render3DTestHelpers.MakeScreenTriangle((10.1, 10.1), (10.3, 10.1), (10.1, 10.3)),
        };

        // Act
        var bins = TileBinner.Bin(triangles, 2, 2, 32);

        // Assert: the third triangle lies between sample centres (10.5, 10.5), so no tile can be touched
        for (var tile = 0; tile < 4; tile++)
        {
            Assert.True(bins.GetTriangles(tile).IsEmpty);
        }
    }

    /// <summary>
    ///     Validates that a triangle partly off the grid is binned only into the tiles on the grid.
    /// </summary>
    [Fact]
    public void TileBinner_Bin_PartiallyOffGridTriangle_IsClampedToGrid()
    {
        // Arrange: extends far to the left and above the 2 x 1 tile grid
        var triangles = new[] { Render3DTestHelpers.MakeScreenTriangle((-100, -100), (50, -100), (-100, 20)) };

        // Act
        var bins = TileBinner.Bin(triangles, 2, 1, 32);

        // Assert: the hypotenuse reaches x = 50 only near y = -100, so only tile 0 sees samples; the bounding box still reaches tile 1
        Assert.Contains(0, bins.GetTriangles(0).ToArray());
        Assert.Contains(0, bins.GetTriangles(1).ToArray());
    }
}
