// cspell:ignore extrude
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Render-level tests of the extrusions through <see cref="ScadDocument.Render"/>: analytic pixel counts of a
///     ring and of a vase silhouette, and the equivalence of an extruded 2D difference with the difference of the
///     hand-built extrusions.
/// </summary>
public class ScadDocumentRenderExtrusionTests
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
    ///     Counts the pixel centers of a one-unit-per-pixel square image, centered on the origin, that lie strictly
    ///     inside a circle: the centers are the half-integers, so with odd integers a and b the squared distance
    ///     is (a squared plus b squared) over four.
    /// </summary>
    /// <param name="size">The image size in pixels (even).</param>
    /// <param name="radius">The integer radius of the circle.</param>
    /// <returns>The number of centers inside.</returns>
    private static int CentersInside(int size, int radius)
    {
        var count = 0;
        for (var a = -(size - 1); a <= size - 1; a += 2)
        {
            for (var b = -(size - 1); b <= size - 1; b += 2)
            {
                if ((a * a) + (b * b) < 4 * radius * radius)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Validates that an extruded ring renders as the annulus: the analytic number of pixel centers between
    ///     the two circles, background in the hole and outside, and the surface color in the ring.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_ExtrudedRing_ShowsTheAnnulus()
    {
        // Arrange: 14 x 14 pixels, one pixel per unit, looking straight down at the origin
        var options = new ScadRenderOptions
        {
            Camera = new Camera
            {
                Eye = new Vector3D(0, 0, 50),
                Target = Vector3D.Zero,
                Up = Vector3D.UnitY,
                Projection = CameraProjection.Orthographic,
                OrthographicHeight = 14,
            },
            SceneOptions = FlatOptions,
        };
        var document = ScadDocument.Parse("linear_extrude(10) difference() { circle(5, $fn = 48); circle(2, $fn = 48); }");

        // Act
        using var surface = document.Render(14, 14, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CentersInside(14, 5) - CentersInside(14, 2), CountNon(surface, White));
        Assert.Equal(White, surface.GetRowSpan(6)[6]);
        Assert.Equal(White, surface.GetRowSpan(7)[7]);
        Assert.Equal(Gray, surface.GetRowSpan(6)[2]);
        Assert.Equal(White, surface.GetRowSpan(0)[0]);
    }

    /// <summary>
    ///     Validates that a revolved vase renders with the profile's silhouette: row by row, the width of the body
    ///     and of the neck at the sampled heights, and nothing above the lip.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_RevolvedVase_HasTheProfileSilhouette()
    {
        // Arrange: a front view, 12 x 12 pixels, one pixel per unit, centered on Z = 4
        var options = new ScadRenderOptions
        {
            Camera = new Camera
            {
                Eye = new Vector3D(0, -50, 4),
                Target = new Vector3D(0, 0, 4),
                Up = Vector3D.UnitZ,
                Projection = CameraProjection.Orthographic,
                OrthographicHeight = 12,
            },
            SceneOptions = FlatOptions,
        };
        var document = ScadDocument.Parse("rotate_extrude($fn = 64) polygon([[0, 0], [4, 0], [4, 6], [2, 6], [2, 8], [0, 8]]);");

        // Act
        using var surface = document.Render(12, 12, options, TestContext.Current.CancellationToken);

        // Assert: row j shows the height 9.5 - j
        static int Width(Surface s, int row) => s.GetRowSpan(row).ToArray().Count(p => p != new Rgba32(255, 255, 255, 255));
        Assert.Equal(0, Width(surface, 0));
        Assert.Equal(4, Width(surface, 2));
        Assert.Equal(4, Width(surface, 3));
        Assert.Equal(8, Width(surface, 5));
        Assert.Equal(8, Width(surface, 6));
        Assert.Equal(8, Width(surface, 9));
        Assert.Equal(0, Width(surface, 10));
        Assert.Equal(0, Width(surface, 11));
    }

    /// <summary>
    ///     Validates that the extrusion of a 2D difference renders pixel for pixel like the difference of the two
    ///     extrusions built by hand with the renderer's own tessellators.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_ExtrudedDifference_EqualsTheHandBuiltDifferenceOfExtrusions()
    {
        // Arrange
        var document = ScadDocument.Parse("linear_extrude(10) difference() { square(10); translate([3, 3]) square(4); }");
        var outer = Extruder.Linear(PrimitiveTessellator.Rectangle(new Vector2D(10, 10)), 10.0);
        var hole = Extruder.Linear(new PolygonRegion([new[] { new Vector2D(3, 3), new Vector2D(7, 3), new Vector2D(7, 7), new Vector2D(3, 7) }]), 10.0);
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(outer), new MeshNode(hole)]));

        // Act
        using var rendered = document.Render(64, 64, cancellationToken: TestContext.Current.CancellationToken);
        using var expected = SceneRenderer.RenderFitted(scene, 64, 64, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        AssertSurfacesIdentical(expected, rendered);
        Assert.True(CountNon(rendered, White) > 500);
    }

    /// <summary>
    ///     Validates the same equivalence for a revolved difference.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_RevolvedDifference_EqualsTheHandBuiltDifferenceOfRevolutions()
    {
        // Arrange: a tube [2, 5] x [0, 4] with a groove [4, 5] x [1, 2] removed, 24 segments
        var document = ScadDocument.Parse("rotate_extrude($fn = 24) difference() { translate([2, 0]) square([3, 4]); translate([4, 1]) square([1, 1]); }");
        var tube = Extruder.Rotate(new PolygonRegion([new[] { new Vector2D(2, 0), new Vector2D(5, 0), new Vector2D(5, 4), new Vector2D(2, 4) }]), 360.0, 24);
        var groove = Extruder.Rotate(new PolygonRegion([new[] { new Vector2D(4, 1), new Vector2D(5, 1), new Vector2D(5, 2), new Vector2D(4, 2) }]), 360.0, 24);
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(tube), new MeshNode(groove)]));

        // Act
        using var rendered = document.Render(64, 64, cancellationToken: TestContext.Current.CancellationToken);
        using var expected = SceneRenderer.RenderFitted(scene, 64, 64, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        AssertSurfacesIdentical(expected, rendered);
    }

    /// <summary>
    ///     Validates that a colored extrusion renders in its color and a twisted extrusion renders a different image
    ///     from the untwisted one (the twist is visible).
    /// </summary>
    [Fact]
    public void ScadDocument_Render_ColoredAndTwistedExtrusions_AreVisible()
    {
        // Arrange
        var colored = ScadDocument.Parse("color(\"green\") linear_extrude(4) square(4);");
        var plain = ScadDocument.Parse("linear_extrude(height = 8, twist = 0) translate([-2, -1]) square([4, 2]);");
        var twisted = ScadDocument.Parse("linear_extrude(height = 8, twist = 90, slices = 16) translate([-2, -1]) square([4, 2]);");
        var top = new ScadRenderOptions
        {
            Camera = new Camera
            {
                Eye = new Vector3D(0, 0, 50),
                Target = Vector3D.Zero,
                Up = Vector3D.UnitY,
                Projection = CameraProjection.Orthographic,
                OrthographicHeight = 8,
            },
            SceneOptions = FlatOptions,
        };

        // Act
        using var green = colored.Render(8, 8, top, TestContext.Current.CancellationToken);
        using var straight = plain.Render(8, 8, top, TestContext.Current.CancellationToken);
        using var turned = twisted.Render(8, 8, top, TestContext.Current.CancellationToken);

        // Assert: viewed from above, the bar's footprint is 4 x 2 pixels, and the twisted one sweeps a larger footprint
        Assert.Equal(new Rgba32(0, 128, 0, 255), green.GetRowSpan(2)[5]);
        Assert.Equal(8, CountNon(straight, White));
        Assert.True(CountNon(turned, White) > 8);
    }
}
