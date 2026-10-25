// cspell:ignore multmatrix
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Tests.Geometry;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Tests of the coordinate range guard: a finite but extreme transform (or primitive or extrusion) must be
///     reported as the unsupported feature <c>scad-geometry-rejected</c> at the instance and omitted, so that
///     <see cref="ScadDocument.BuildScene(ScadRenderOptions?, CancellationToken)"/> and
///     <see cref="ScadDocument.Render"/> never throw (outside strict mode) and the fitted camera never fails.
/// </summary>
public class ScadDocumentCoordinateRangeTests
{
    /// <summary>
    ///     Validates that each extreme program is rejected at the instance that caused the overflow, once, and that
    ///     nothing is left in the scene.
    /// </summary>
    /// <param name="source">The program; the offending instance is at line 2, column 3.</param>
    [Theory]
    [InlineData("\n  translate([1.7e308, 0, 0]) cube(1);")]
    [InlineData("\n  translate([2e100, 0, 0]) cube(1);")]
    [InlineData("\n  scale(1e60) scale(1e60) cube(1);")]
    [InlineData("\n  translate([1.7e308, 0]) square(1);")]
    [InlineData("\n  linear_extrude(height = 1e200) square(1);")]
    [InlineData("\n  cube(1e200);")]
    [InlineData("\n  translate([-1.7e308, 0, 1.7e308]) sphere(1);")]
    [InlineData("\n  multmatrix([[1, 0, 0, 1.7e308], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]) cube(1);")]
    [InlineData("\n  translate([1.7e308, 0, 0]) rotate([10, 20, 30]) cube(1);")]
    [InlineData("\n  scale(1e60) difference() { cube(1); scale(1e60) cube(1); }")]
    public void ScadGeometryBuilder_ExtremeCoordinates_AreRejectedAtTheInstance(string source)
    {
        // Arrange / Act
        var result = ScadGeometryTestSupport.Build(source);

        // Assert: the hidden operand of a difference counts too, so the scene stays within range
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-geometry-rejected", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Contains("rejected", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the rejected instance is the only one lost: its siblings stay.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_ExtremeCoordinates_OmitOnlyTheOffendingInstance()
    {
        // Arrange / Act
        var result = ScadGeometryTestSupport.Build("cube(1);\ntranslate([1.7e308, 0, 0]) cube(1);\ntranslate([5, 0, 0]) cube(1);");

        // Assert
        Assert.Equal(2, result.Meshes.Count);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal(new ScadSourceLocation(null, 2, 1), report.Location);
        Assert.True(ScadGeo.IsSafe(result.Scene.ComputeBounds()));
    }

    /// <summary>
    ///     Validates that large but safe coordinates are not rejected.
    /// </summary>
    [Theory]
    [InlineData("translate([1e30, 0, 0]) cube(1);")]
    [InlineData("scale(1e30) cube(1);")]
    [InlineData("translate([9e99, 0, 0]) cube(1);")]
    [InlineData("translate([1e10, 0]) square(1);")]
    public void ScadGeometryBuilder_LargeButSafeCoordinates_AreKept(string source)
    {
        // Arrange / Act
        var result = ScadGeometryTestSupport.Build(source);

        // Assert
        Assert.NotEmpty(result.Meshes);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that strict mode throws for the extreme program from <c>BuildScene</c> and from
    ///     <c>Render</c> alike, and that outside strict mode neither throws and both report once.
    /// </summary>
    [Fact]
    public void ScadDocument_ExtremeTranslate_BuildSceneAndRenderAreConsistent()
    {
        // Arrange
        var document = ScadDocument.Parse("translate([1.7e308, 0, 0]) cube(1);");
        var reports = new List<ScadUnsupportedFeature>();
        var lenient = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { UnsupportedFeatureHandler = reports.Add } };
        var strict = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true } };

        // Act
        var scene = document.BuildScene(lenient, TestContext.Current.CancellationToken);
        var sceneReports = reports.ToArray();
        reports.Clear();
        using var surface = document.Render(32, 24, lenient, TestContext.Current.CancellationToken);
        var renderReports = reports.ToArray();
        var buildFailure = Assert.Throws<ScadUnsupportedFeatureException>(() => document.BuildScene(strict, TestContext.Current.CancellationToken));
        var renderFailure = Assert.Throws<ScadUnsupportedFeatureException>(() => document.Render(32, 24, strict, TestContext.Current.CancellationToken));

        // Assert: an empty scene that renders as the background, the same single report in both entry points
        Assert.Empty(ScadGeometryTestSupport.MeshNodes(scene));
        Assert.Equal(32, surface.Width);
        Assert.Equal(24, surface.Height);
        var sceneReport = Assert.Single(sceneReports);
        var renderReport = Assert.Single(renderReports);
        Assert.Equal("scad-geometry-rejected", sceneReport.Feature);
        Assert.Equal(sceneReport.Feature, renderReport.Feature);
        Assert.Equal(sceneReport.Location, renderReport.Location);
        Assert.Equal(new ScadSourceLocation(null, 1, 1), renderReport.Location);
        Assert.Equal("scad-geometry-rejected", buildFailure.Feature);
        Assert.Equal(buildFailure.Feature, renderFailure.Feature);
        Assert.Equal(buildFailure.Location, renderFailure.Location);
    }

    /// <summary>
    ///     Validates that rendering never throws for the extreme programs, for both projections, with the
    ///     rejected instance next to a visible one.
    /// </summary>
    /// <param name="projection">The projection.</param>
    [Theory]
    [InlineData(CameraProjection.Perspective)]
    [InlineData(CameraProjection.Orthographic)]
    public void ScadDocument_Render_ExtremeTransformNextToVisibleSolid_RendersTheVisibleSolid(CameraProjection projection)
    {
        // Arrange
        var document = ScadDocument.Parse("cube(10);\ntranslate([1.7e308, 0, 0]) cube(1);\nscale(1e200) sphere(1);");
        var options = new ScadRenderOptions { Projection = projection };

        // Act
        using var surface = document.Render(40, 30, options, TestContext.Current.CancellationToken);
        using var visible = ScadDocument.Parse("cube(10);").Render(40, 30, options, TestContext.Current.CancellationToken);

        // Assert: the rejected instances changed nothing
        for (var y = 0; y < surface.Height; y++)
        {
            Assert.True(visible.GetRowSpanBytes(y).SequenceEqual(surface.GetRowSpanBytes(y)));
        }
    }

    /// <summary>
    ///     Validates that models at the edge of the accepted range render without throwing, for both projections.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("translate([9e99, 0, 0]) cube(1);")]
    [InlineData("translate([-9e99, 9e99, -9e99]) cube(1);\ncube(1);")]
    [InlineData("scale(1e99) cube(1);")]
    [InlineData("scale(1e99) rotate([10, 20, 30]) cube(1);")]
    [InlineData("linear_extrude(height = 1e99) square(1);")]
    public void ScadDocument_Render_CoordinatesAtTheLimit_DoesNotThrow(string source)
    {
        // Arrange
        var document = ScadDocument.Parse(source);

        // Act / Assert
        foreach (var projection in new[] { CameraProjection.Perspective, CameraProjection.Orthographic })
        {
            using var surface = document.Render(24, 24, new ScadRenderOptions { Projection = projection }, TestContext.Current.CancellationToken);
            Assert.Equal(24, surface.Width);
        }
    }

    /// <summary>
    ///     Validates that the fitted camera falls back to the unit cube for bounds that are not finite or out of
    ///     range, instead of throwing, whatever scene is given.
    /// </summary>
    /// <param name="coordinate">The coordinate of the far vertex of the scene.</param>
    [Theory]
    [InlineData(1e150)]
    [InlineData(1e300)]
    [InlineData(1.7e308)]
    public void ScadDocument_FitCamera_UnusableBounds_FallsBackToTheUnitCube(double coordinate)
    {
        // Arrange: a mesh node that a (hypothetically unguarded) build could have produced
        var mesh = PrimitiveTessellator.Box(new Vector3D(1.0, 1.0, 1.0));
        var far = new GroupNode([new MeshNode(mesh)]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(coordinate, coordinate, coordinate)) };
        var scene = new Scene(new GroupNode([new MeshNode(mesh), far]));
        var options = new ScadRenderOptions();

        // Act
        var camera = ScadDocument.FitCamera(scene, options, 4.0 / 3.0);
        var expected = Camera.FitToBounds(BoundingBox3D.Empty, options.ViewAzimuthDegrees, options.ViewElevationDegrees, 4.0 / 3.0, options.Projection);

        // Assert
        Assert.Equal(expected.Eye, camera.Eye);
        Assert.Equal(expected.Target, camera.Target);
    }

    /// <summary>
    ///     Validates that the camera fits the bounds of a usable scene (no fallback).
    /// </summary>
    [Fact]
    public void ScadDocument_FitCamera_UsableBounds_FitsTheScene()
    {
        // Arrange
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Box(new Vector3D(2.0, 2.0, 2.0))));
        var options = new ScadRenderOptions();

        // Act
        var camera = ScadDocument.FitCamera(scene, options, 1.0);
        var expected = Camera.FitToBounds(scene.ComputeBounds(), options.ViewAzimuthDegrees, options.ViewElevationDegrees, 1.0, options.Projection);

        // Assert
        Assert.Equal(expected.Eye, camera.Eye);
        Assert.Equal(new Vector3D(1.0, 1.0, 1.0), camera.Target);
    }
}
