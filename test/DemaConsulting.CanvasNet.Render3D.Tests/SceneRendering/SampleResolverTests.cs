using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the internal <c>SampleResolver</c> (SceneRenderer unit).
/// </summary>
public class SampleResolverTests
{
    /// <summary>
    ///     Creates a 2x2-sample workspace (one output pixel at supersampling 2) in which the listed samples are covered by <paramref name="color"/>.
    /// </summary>
    /// <param name="covered">The indices (0..3, row-major) of covered samples.</param>
    /// <param name="color">The color of covered samples.</param>
    /// <param name="originX">The global sample column of the tile origin.</param>
    /// <returns>The workspace.</returns>
    private static TileWorkspace TwoByTwo(int[] covered, Rgba32 color, int originX = 0)
    {
        var workspace = new TileWorkspace(4);
        workspace.Begin(originX, 0, 2, 2);
        foreach (var index in covered)
        {
            workspace.Depth[index] = 5.0f;
            workspace.Colors[index] = color;
        }

        return workspace;
    }

    /// <summary>
    ///     Validates that a half-covered opaque pixel averages the surface and background colors.
    /// </summary>
    [Fact]
    public void SampleResolver_Resolve_HalfCoveredPixelTwoByTwo_AveragesColors()
    {
        // Arrange: two of four samples are the surface color, two are the opaque black background
        var workspace = TwoByTwo([0, 1], new Rgba32(200, 100, 50, 255));
        using var surface = new Surface(1, 1);

        // Act
        SampleResolver.Resolve(workspace, 2, new Rgba32(0, 0, 0, 255), surface, new TileRect(0, 0, 1, 1));

        // Assert: round((2 * c + 2 * 0) / 4) per channel
        Assert.Equal(new Rgba32(100, 50, 25, 255), surface[0, 0]);
    }

    /// <summary>
    ///     Validates the premultiplied average over a transparent background: alpha 128 and the color stays the shaded color.
    /// </summary>
    [Fact]
    public void SampleResolver_Resolve_TransparentBackgroundHalfCovered_PremultipliedAverageKeepsColor()
    {
        // Arrange
        var workspace = TwoByTwo([0, 3], new Rgba32(200, 100, 50, 255));
        using var surface = new Surface(1, 1);

        // Act
        SampleResolver.Resolve(workspace, 2, new Rgba32(0, 0, 0, 0), surface, new TileRect(0, 0, 1, 1));

        // Assert: half the samples are opaque => A = (510 + 2) / 4 = 128, color unchanged (no dark fringe)
        Assert.Equal(new Rgba32(200, 100, 50, 128), surface[0, 0]);
    }

    /// <summary>
    ///     Validates that a fully transparent pixel resolves to (0, 0, 0, 0) and a fully covered pixel to the surface color.
    /// </summary>
    [Fact]
    public void SampleResolver_Resolve_FullyTransparentAndFullyCovered_AreExact()
    {
        // Arrange
        var empty = TwoByTwo([], new Rgba32(1, 2, 3, 255));
        var full = TwoByTwo([0, 1, 2, 3], new Rgba32(10, 20, 30, 255), originX: 2);
        using var surface = new Surface(2, 1);

        // Act
        SampleResolver.Resolve(empty, 2, new Rgba32(9, 9, 9, 0), surface, new TileRect(0, 0, 1, 1));
        SampleResolver.Resolve(full, 2, new Rgba32(9, 9, 9, 0), surface, new TileRect(1, 0, 2, 1));

        // Assert: the second workspace starts at sample column 2, i.e. output pixel 1
        Assert.Equal(new Rgba32(0, 0, 0, 0), surface[0, 0]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[1, 0]);
    }

    /// <summary>
    ///     Validates that opaque resolve equals the plain rounded average for supersampling 3 (nine samples).
    /// </summary>
    [Fact]
    public void SampleResolver_Resolve_OpaqueThreeByThree_EqualsRoundedAverage()
    {
        // Arrange: 4 of 9 samples are 200, five are background 100
        var workspace = new TileWorkspace(9);
        workspace.Begin(0, 0, 3, 3);
        for (var i = 0; i < 4; i++)
        {
            workspace.Depth[i] = 1.0f;
            workspace.Colors[i] = new Rgba32(200, 200, 200, 255);
        }

        using var surface = new Surface(1, 1);

        // Act
        SampleResolver.Resolve(workspace, 3, new Rgba32(100, 100, 100, 255), surface, new TileRect(0, 0, 1, 1));

        // Assert: (4 * 200 + 5 * 100 + 4) / 9 = 144
        Assert.Equal(new Rgba32(144, 144, 144, 255), surface[0, 0]);
    }
}
