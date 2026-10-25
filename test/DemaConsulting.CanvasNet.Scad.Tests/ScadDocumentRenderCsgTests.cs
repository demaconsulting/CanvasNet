using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Render-level tests of constructive solid geometry through <see cref="ScadDocument.Render"/>: top views of
///     differences and intersections of solids and of 2D shapes with analytic pixel counts, and the same on small
///     caller stacks for deeply nested booleans.
/// </summary>
public class ScadDocumentRenderCsgTests
{
    /// <summary>The default background of the renderer.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>The default surface color of the renderer, a neutral gray.</summary>
    private static readonly Rgba32 Gray = new(180, 180, 180, 255);

    /// <summary>Flat lighting without antialiasing, so every covered pixel is exactly the surface color.</summary>
    private static readonly SceneRenderOptions FlatOptions = new()
    {
        Lights = [new AmbientLight { Intensity = 1.0f }],
        Supersampling = 1,
    };

    /// <summary>
    ///     Creates options for an orthographic view straight down onto the XY plane.
    /// </summary>
    /// <param name="centerX">The model X at the center of the image.</param>
    /// <param name="centerY">The model Y at the center of the image.</param>
    /// <param name="extent">The model height covered by the image (equal to the width for a square image).</param>
    /// <returns>The options.</returns>
    private static ScadRenderOptions TopView(double centerX, double centerY, double extent) => new()
    {
        Camera = new Camera
        {
            Eye = new Vector3D(centerX, centerY, 50),
            Target = new Vector3D(centerX, centerY, 0),
            Up = Vector3D.UnitY,
            Projection = CameraProjection.Orthographic,
            OrthographicHeight = extent,
        },
        SceneOptions = FlatOptions,
    };

    /// <summary>
    ///     Renders a program in a square top view.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="size">The image size in pixels (one pixel is one model unit when the extent equals it).</param>
    /// <param name="centerX">The model X at the center of the image.</param>
    /// <param name="centerY">The model Y at the center of the image.</param>
    /// <returns>The surface; the caller disposes it.</returns>
    private static Surface RenderTop(string source, int size, double centerX, double centerY) =>
        ScadDocument.Parse(source).Render(size, size, TopView(centerX, centerY, size), TestContext.Current.CancellationToken);

    /// <summary>
    ///     Validates that a block with a round through hole renders as its footprint minus the hole: background in
    ///     the hole, the surface color around it, and the analytic number of covered pixels.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_CubeWithCylinderHole_ShowsTheHole()
    {
        // Arrange / Act: 20 x 20 pixels, one pixel per unit, centered on the middle of the 10 x 10 block
        using var surface = RenderTop("difference() { cube([10, 10, 4]); translate([5, 5, -1]) cylinder(r = 2, h = 6, $fn = 64); }", 20, 5, 5);

        // Assert: the 100 pixel footprint loses the 12 pixels whose centers are inside the radius-2 hole
        Assert.Equal(White, surface.GetRowSpan(9)[9]);
        Assert.Equal(White, surface.GetRowSpan(10)[10]);
        Assert.Equal(Gray, surface.GetRowSpan(5)[5]);
        Assert.Equal(Gray, surface.GetRowSpan(14)[14]);
        Assert.Equal(White, surface.GetRowSpan(0)[0]);
        Assert.Equal(88, CountNon(surface, White));
    }

    /// <summary>
    ///     Validates that a 2D difference at the top level renders its hole too (the 2D boolean is distributed
    ///     over slabs).
    /// </summary>
    [Fact]
    public void ScadDocument_Render_FlatDifference_ShowsTheHole()
    {
        // Arrange / Act: the 100 pixel square loses the 4 x 4 pixel hole
        using var surface = RenderTop("difference() { square(10); translate([3, 3]) square(4); }", 20, 5, 5);

        // Assert
        Assert.Equal(White, surface.GetRowSpan(10)[10]);
        Assert.Equal(Gray, surface.GetRowSpan(6)[6]);
        Assert.Equal(84, CountNon(surface, White));
    }

    /// <summary>
    ///     Validates that a 2D intersection renders exactly the overlap.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_FlatIntersection_ShowsTheOverlapOnly()
    {
        // Arrange / Act: the squares [0, 4] and [2, 6] overlap in [2, 4] x [2, 4]
        using var surface = RenderTop("intersection() { square(4); translate([2, 2]) square(4); }", 12, 3, 3);

        // Assert: pixel centers 2.5 and 3.5 lie in the overlap
        Assert.Equal(4, CountNon(surface, White));
        Assert.Equal(Gray, surface.GetRowSpan(6)[6]);
    }

    /// <summary>
    ///     Validates that the intersection of a block with a sphere renders as the disc of the sphere's equator
    ///     clipped by the block.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_CubeSphereIntersection_IsClippedByTheCube()
    {
        // Arrange / Act: the sphere of radius 8 around (5, 5, 5) is cut to the 10 x 10 footprint of the cube
        using var surface = RenderTop("intersection() { cube(10); translate([5, 5, 5]) sphere(8, $fn = 64); }", 20, 5, 5);

        // Assert: the corner pixel (5, 5) center (0.5, 9.5) is 6.4 from the axis (inside the radius 8), so the
        // whole footprint is covered
        Assert.Equal(Gray, surface.GetRowSpan(5)[5]);
        Assert.Equal(100, CountNon(surface, White));
    }

    /// <summary>
    ///     Validates that a color applies to a boolean's result: the colored difference renders in the color.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_ColoredDifference_UsesTheColor()
    {
        // Arrange / Act
        using var surface = RenderTop("color(\"red\") difference() { cube(10); translate([5, 5, -1]) cylinder(r = 2, h = 12, $fn = 32); }", 20, 5, 5);

        // Assert
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface.GetRowSpan(5)[5]);
        Assert.Equal(White, surface.GetRowSpan(10)[10]);
    }

    /// <summary>
    ///     Validates that a deeply nested difference, written in source, renders on 64 KiB and 256 KiB caller stacks
    ///     to exactly the image a big stack gives.
    /// </summary>
    /// <param name="stackSize">The caller's stack size.</param>
    [Theory]
    [InlineData(64 * 1024)]
    [InlineData(SmallStack)]
    public void ScadDocument_Render_DeepDifferenceChain_OnSmallStack_MatchesBigStack(int stackSize)
    {
        // Arrange: 1,000 nested differences, each removing a tiny cube from the previous result
        const string source = "module r(n) { difference() { if (n > 0) r(n - 1); else cube(10); translate([0.1, 0.1, 0.1]) cube(0.01); } }\nr(1000);";
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { Limits = new ScadLimits { MaxRecursionDepth = 16_384 } },
            SceneOptions = new SceneRenderOptions { Supersampling = 1 },
        };
        var document = ScadDocument.Parse(source);

        // Act
        using var big = OnSmallStack(() => document.Render(8, 8, options), 256 * 1024 * 1024);
        using var small = OnSmallStack(() => document.Render(8, 8, options), stackSize);

        // Assert
        AssertSurfacesIdentical(big, small);
        Assert.True(CountNon(small, White) > 10);
    }
}
