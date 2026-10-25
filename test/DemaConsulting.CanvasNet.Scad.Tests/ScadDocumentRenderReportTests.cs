// cspell:ignore polyhedron underflows underflowing supersampled
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Tests of the render-stage reports of <see cref="ScadDocument.Render"/>: the renderer's operand validation
///     (<c>render3d-csg-*</c>) mapped to <c>scad-csg-*</c> at the source location of the module that produced the
///     mesh, the fragment budget, cancellation during evaluation and rendering, and strict mode end to end.
/// </summary>
public class ScadDocumentRenderReportTests
{
    /// <summary>The corners of the unit cube (shared by the polyhedron programs).</summary>
    private const string CubePoints = "[[0,0,0],[1,0,0],[0,1,0],[1,1,0],[0,0,1],[1,0,1],[0,1,1],[1,1,1]]";

    /// <summary>The faces of the unit cube, clockwise seen from outside.</summary>
    private const string CubeFacesClockwise = "[[1,3,2,0],[6,7,5,4],[4,5,1,0],[3,7,6,2],[2,6,4,0],[5,7,3,1]]";

    /// <summary>The faces of the unit cube, counter-clockwise seen from outside (an inside-out shell).</summary>
    private const string CubeFacesCounterClockwise = "[[0,2,3,1],[4,5,7,6],[0,1,5,4],[2,6,7,3],[0,4,6,2],[1,3,7,5]]";

    /// <summary>
    ///     Renders a program with a handler that records every unsupported-feature report.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="files">The virtual files, or <see langword="null"/>.</param>
    /// <returns>The reports in order.</returns>
    private static List<ScadUnsupportedFeature> RenderReports(string source, ScadVirtualFiles? files = null)
    {
        var reports = new List<ScadUnsupportedFeature>();
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { UnsupportedFeatureHandler = reports.Add },
            SceneOptions = new SceneRenderOptions { Supersampling = 1 },
        };
        var document = files is null
            ? ScadDocument.Parse(source)
            : ScadDocument.Parse(source, new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "main.scad" });
        using var surface = document.Render(16, 16, options, TestContext.Current.CancellationToken);
        return reports;
    }

    /// <summary>
    ///     Validates that an open polyhedron used as a difference operand is reported as
    ///     <c>scad-csg-open-mesh</c> at the polyhedron's own line and column, once.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_OpenPolyhedronInDifference_IsReportedAtThePolyhedron()
    {
        // Arrange / Act
        var reports = RenderReports("cube(10);\ndifference() {\n    cube(10);\n    translate([1, 1, 1]) polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]);\n}");

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("scad-csg-open-mesh", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 4, 26), report.Location);
        Assert.Contains("open", report.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates the other mesh defects, each mapped with its own feature identifier and the polyhedron's location:
    ///     an edge shared by three faces, two faces wound against each other, a zero-area face, and a shell wound
    ///     inside out next to a correct one.
    /// </summary>
    /// <param name="polyhedron">The polyhedron call.</param>
    /// <param name="feature">The expected feature among the reports.</param>
    [Theory]
    [InlineData("polyhedron([[0,0,0],[1,0,0],[0,1,0],[0,0,1],[0,-1,0]], [[0,1,2],[0,1,3],[0,1,4]])", "scad-csg-non-manifold-edge")]
    [InlineData("polyhedron([[0,0,0],[1,0,0],[0,1,0],[0,0,1]], [[0,1,2],[0,1,3]])", "scad-csg-inconsistent-winding")]
    [InlineData("polyhedron([[0,0,0],[1,0,0],[2,0,0]], [[0,1,2]])", "scad-csg-degenerate-triangle")]
    public void ScadDocumentRenderReport_MeshDefects_AreMappedWithTheirFeature(string polyhedron, string feature)
    {
        // Arrange / Act
        var reports = RenderReports($"difference() {{\n  cube(10);\n  {polyhedron};\n}}");

        // Assert: the defect is reported at the polyhedron (line 3, column 3), once per feature
        Assert.Contains(reports, r => r.Feature == feature);
        Assert.All(reports, r => Assert.Equal(new ScadSourceLocation(null, 3, 3), r.Location));
        Assert.Equal(reports.Count, reports.Select(r => r.Feature).Distinct().Count());
        Assert.StartsWith("scad-csg-", reports[0].Feature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the inverted-shell defect: a polyhedron with a correct big cube and a disjoint inside-out small
    ///     cube has a positive total volume (so it is kept as is) and is reported when it is a boolean operand.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_InvertedShellNextToCorrectShell_IsReported()
    {
        // Arrange: the second cube is moved away and wound counter-clockwise
        var source = $$"""
            cube_points = {{CubePoints}};
            cube_faces = {{CubeFacesClockwise}};
            inside_out = {{CubeFacesCounterClockwise}};
            points = concat([for (p = cube_points) p * 10], [for (p = cube_points) p + [20, 0, 0]]);
            faces = concat(cube_faces, [for (f = inside_out) [for (i = f) i + 8]]);
            difference() {
                cube(10);
                polyhedron(points, faces);
            }
            """;

        // Act
        var reports = RenderReports(source);

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("scad-csg-inverted-mesh", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 8, 5), report.Location);
    }

    /// <summary>
    ///     Validates the singular-transform report: three nested stretches that are valid on their own multiply to a
    ///     world transform whose determinant underflows to zero, which the renderer reports per operand and which is
    ///     mapped to each operand's own location.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_UnderflowingWorldTransform_IsReportedPerOperand()
    {
        // Arrange
        const string source = "scale([1e-110, 1, 1]) scale([1e-110, 1, 1]) scale([1e-110, 1, 1])\ndifference() {\n  cube(1);\n  translate([0.5, 0.5, 0.5]) cube(1);\n}";

        // Act
        var reports = RenderReports(source);

        // Assert
        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.Equal("scad-csg-singular-transform", r.Feature));
        Assert.Equal(
            [new ScadSourceLocation(null, 3, 3), new ScadSourceLocation(null, 4, 30)],
            reports.Select(r => r.Location).OrderBy(l => l.Line).ToArray());
    }

    /// <summary>
    ///     Validates that the same defective operand instantiated many times is reported once (the sink
    ///     de-duplicates by feature and location).
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_RepeatedOperand_IsReportedOnce()
    {
        // Arrange / Act
        var reports = RenderReports("difference() {\n  cube(20);\n  for (i = [0:3]) translate([i * 3, 0, 0]) polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]);\n}");

        // Assert
        Assert.Equal(["scad-csg-open-mesh"], reports.Select(r => r.Feature).ToArray());
    }

    /// <summary>
    ///     Validates that a defect in a used file is reported with the file in the location.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_DefectInUsedFile_CarriesTheFile()
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "\nmodule sheet() { polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]); }\n"));

        // Act
        var reports = RenderReports("use <lib.scad>\ndifference() { cube(5); sheet(); }", files);

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("scad-csg-open-mesh", report.Feature);
        Assert.Equal(new ScadSourceLocation("lib.scad", 2, 18), report.Location);
    }

    /// <summary>
    ///     Validates that the renderer only checks operands of a real boolean: an open mesh in a plain union is
    ///     never reported, and neither is anything without a handler.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_NoBooleanOrNoHandler_ReportsNothing()
    {
        // Arrange
        const string open = "polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]);";

        // Act
        var union = RenderReports($"cube(5);\n{open}");
        var single = RenderReports($"difference() {open}");
        using var withoutHandler = ScadDocument.Parse($"difference() {{ cube(5); {open} }}").Render(16, 16, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(union);
        Assert.Empty(single);
        Assert.True(CountNon(withoutHandler, new Rgba32(255, 255, 255, 255)) > 0);
    }

    /// <summary>
    ///     Validates that building a scene (no rendering) does not run the renderer's checks.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_BuildScene_DoesNotValidateOperands()
    {
        // Arrange
        var reports = new List<ScadUnsupportedFeature>();
        var options = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { UnsupportedFeatureHandler = reports.Add } };

        // Act
        ScadDocument.Parse("difference() { cube(5); polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]); }").BuildScene(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates the order of events across the three stages: evaluation (echo, warning), geometry (unsupported
    ///     module) and renderer (operand report), delivered once each on the calling thread.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_AllStages_AreDeliveredInOrderOnTheCallingThread()
    {
        // Arrange
        var events = new List<string>();
        var threads = new HashSet<int>();
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions
            {
                EchoHandler = e => { events.Add("echo"); threads.Add(Environment.CurrentManagedThreadId); },
                WarningHandler = w => { events.Add("warn"); threads.Add(Environment.CurrentManagedThreadId); },
                UnsupportedFeatureHandler = u => { events.Add(u.Feature); threads.Add(Environment.CurrentManagedThreadId); },
            },
            SceneOptions = new SceneRenderOptions { Supersampling = 1 },
        };
        const string source = "echo(1);\ncube(0);\nhull() cube(1);\ndifference() { cube(5); polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]); }";

        // Act
        using var surface = ScadDocument.Parse(source).Render(16, 16, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["echo", "warn", "scad-module-hull", "scad-csg-open-mesh"], events);
        Assert.Equal([Environment.CurrentManagedThreadId], threads.ToArray());
    }

    /// <summary>
    ///     Validates strict mode at the renderer stage: the operand report throws the mapped feature with its
    ///     location, the earlier events were delivered first, and no surface is returned.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_StrictMode_ThrowsTheMappedOperandReport()
    {
        // Arrange
        var echoes = new List<string>();
        var features = new List<string>();
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions
            {
                ThrowOnUnsupportedFeature = true,
                EchoHandler = echoes.Add,
                UnsupportedFeatureHandler = u => features.Add(u.Feature),
            },
        };
        var document = ScadDocument.Parse("echo(\"first\");\ndifference() {\n  cube(5);\n  polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]);\n}");

        // Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => document.Render(16, 16, options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("scad-csg-open-mesh", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 4, 3), ex.Location);
        Assert.Equal(["ECHO: \"first\""], echoes);
        Assert.Equal(["scad-csg-open-mesh"], features);
    }

    /// <summary>
    ///     Validates that strict mode with nothing unsupported renders normally, and that the strict flag of the
    ///     nested scene options never throws on its own (reporting and strictness belong to the evaluation options).
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_StrictModeWithNothingUnsupported_Renders()
    {
        // Arrange
        var strict = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true } };
        var sceneStrict = new ScadRenderOptions { SceneOptions = new SceneRenderOptions { ThrowOnUnsupportedFeature = true } };

        // Act
        using var clean = ScadDocument.Parse("difference() { cube(5); translate([1, 1, -1]) cube([2, 2, 9]); }").Render(16, 16, strict, TestContext.Current.CancellationToken);
        using var ignored = ScadDocument.Parse("difference() { cube(5); polyhedron([[0,0,0],[1,0,0],[0,1,0],[5,5,5]], [[0,1,2]]); }").Render(16, 16, sceneStrict, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(CountNon(clean, new Rgba32(255, 255, 255, 255)) > 0);
        Assert.True(CountNon(ignored, new Rgba32(255, 255, 255, 255)) > 0);
    }

    /// <summary>
    ///     Validates that a fragment budget overrun of the renderer is a <see cref="ScadEvaluationException"/> that
    ///     names the option to change, carries the renderer's exception, and still delivers the earlier events.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_FragmentBudgetExceeded_IsAnEvaluationException()
    {
        // Arrange
        var echoes = new List<string>();
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { EchoHandler = echoes.Add },
            SceneOptions = new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = 100 },
        };
        var document = ScadDocument.Parse("echo(1);\ndifference() { cube(10); translate([5, 5, 5]) sphere(6, $fn = 32); }");

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => document.Render(128, 128, options, TestContext.Current.CancellationToken));

        // Assert
        var inner = Assert.IsType<RenderBudgetExceededException>(ex.InnerException);
        Assert.Equal(100L, inner.MaxFragmentsPerTile);
        Assert.Contains("MaxFragmentsPerTile", ex.Message, StringComparison.Ordinal);
        Assert.Equal(["ECHO: 1"], echoes);
    }

    /// <summary>
    ///     Validates that a token canceled while the program is being evaluated (here by the file resolver) stops
    ///     <c>BuildScene</c> and <c>Render</c> with <see cref="OperationCanceledException"/> and no surface.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_CanceledDuringEvaluation_StopsBuildAndRender()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var files = new ScadVirtualFiles(("lib.scad", "x = 1;\n")) { OnRequest = _ => cts.Cancel() };
        var document = ScadDocument.Parse("include <lib.scad>\ncube(1);", new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "main.scad" });

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.BuildScene(cancellationToken: cts.Token));
        Assert.Throws<OperationCanceledException>(() => document.Render(8, 8, cancellationToken: cts.Token));
    }

    /// <summary>
    ///     Validates that cancelling a long render stops it promptly with <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public async Task ScadDocumentRenderReport_CanceledDuringRendering_StopsTheRender()
    {
        // Arrange: a large, finely supersampled image takes far longer than the delay before the cancellation
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var options = new ScadRenderOptions { SceneOptions = new SceneRenderOptions { Supersampling = 4 } };
        var document = ScadDocument.Parse("difference() { sphere(10, $fn = 64); translate([0, 0, 5]) sphere(8, $fn = 64); }");

        // Act
        var render = Task.Run(() => document.Render(3000, 3000, options, cts.Token), TestContext.Current.CancellationToken);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => render);
    }

    /// <summary>
    ///     Validates that every Render3D operand token has a mapped identifier of this package.
    /// </summary>
    [Fact]
    public void ScadDocumentRenderReport_MapFeature_MapsEveryRenderToken()
    {
        // Arrange / Act / Assert
        Assert.Equal(ScadFeatureIds.CsgOpenMesh, ScadSceneOptions.MapFeature("render3d-csg-open-mesh"));
        Assert.Equal(ScadFeatureIds.CsgNonManifoldEdge, ScadSceneOptions.MapFeature("render3d-csg-non-manifold-edge"));
        Assert.Equal(ScadFeatureIds.CsgInconsistentWinding, ScadSceneOptions.MapFeature("render3d-csg-inconsistent-winding"));
        Assert.Equal(ScadFeatureIds.CsgDegenerateTriangle, ScadSceneOptions.MapFeature("render3d-csg-degenerate-triangle"));
        Assert.Equal(ScadFeatureIds.CsgInvertedMesh, ScadSceneOptions.MapFeature("render3d-csg-inverted-mesh"));
        Assert.Equal(ScadFeatureIds.CsgSingularTransform, ScadSceneOptions.MapFeature("render3d-csg-singular-transform"));
        Assert.Equal("scad-other", ScadSceneOptions.MapFeature("other"));
    }
}
