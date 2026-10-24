// cspell:ignore subtrahend

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Tests of the constructive-solid-geometry operand validation of <see cref="SceneRenderer"/>
///     (SceneRenderer unit): the feature tokens per defect kind, delivery to the handler or as a strict
///     exception, de-duplication, ordering, scope (union-only scenes are never validated),
///     mirrored operands and cancellation.
/// </summary>
public class CsgOperandValidatorTests
{
    /// <summary>A closed unit-ish cube used as the healthy minuend.</summary>
    private static readonly Mesh Cube = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, -1), new Vector3D(2, 2, 2));

    /// <summary>
    ///     Builds a mesh from the box's vertices and a modified index list.
    /// </summary>
    /// <param name="vertexCount">The number of extra vertices to append (placed away from the box).</param>
    /// <param name="edit">Edits the index list of the healthy box.</param>
    /// <returns>The defective mesh.</returns>
    private static Mesh Defective(int vertexCount, Action<List<int>> edit)
    {
        var vertices = Cube.Vertices.ToList();
        for (var i = 0; i < vertexCount; i++)
        {
            vertices.Add(new Vector3D(5 + i, 5, 5));
        }

        var indices = Cube.TriangleIndices.ToList();
        edit(indices);
        return new Mesh(vertices, indices);
    }

    /// <summary>
    ///     Creates the defective mesh of a named kind.
    /// </summary>
    /// <param name="kind">The kind of defect.</param>
    /// <returns>The mesh.</returns>
    private static Mesh CreateDefective(string kind) => kind switch
    {
        "open" => Defective(0, i => i.RemoveRange(i.Count - 6, 6)),
        "quad" => Render3DTestHelpers.CreateFloorQuad(0, 0, 1, 1),
        "winding" => Defective(0, i => (i[1], i[2]) = (i[2], i[1])),
        "non-manifold" => Defective(1, i => i.AddRange([0, 1, 8])),
        "degenerate" => Defective(0, i => i.AddRange([0, 0, 1])),
        "collinear" => new Mesh(
            [new Vector3D(0, 0, 0), new Vector3D(0.2, 0, 0), new Vector3D(0.4, 0, 0), new Vector3D(0, 0, 0.5)],
            [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3]),
        "inverted" => Cube.Reverse(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown defect kind."),
    };

    /// <summary>
    ///     Renders a difference of a healthy cube and the given operand with a recording handler.
    /// </summary>
    /// <param name="operand">The subtrahend node.</param>
    /// <param name="reports">Receives the delivered reports.</param>
    /// <param name="configure">Optional extra option configuration.</param>
    private static void RenderDifference(SceneNode operand, List<SceneRenderUnsupportedFeature> reports, Func<SceneRenderOptions, SceneRenderOptions>? configure = null)
    {
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), operand]));
        var options = new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = reports.Add };
        options = configure?.Invoke(options) ?? options;
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);
        using var surface = SceneRenderer.Render(scene, camera, 48, 48, options, TestContext.Current.CancellationToken);
        Assert.Equal(48, surface.Width);
    }

    /// <summary>
    ///     Validates that every defect kind produces its documented feature token and the render still completes.
    /// </summary>
    /// <param name="kind">The defect kind.</param>
    /// <param name="feature">The expected feature token.</param>
    [Theory]
    [InlineData("open", "render3d-csg-open-mesh")]
    [InlineData("quad", "render3d-csg-open-mesh")]
    [InlineData("winding", "render3d-csg-inconsistent-winding")]
    [InlineData("non-manifold", "render3d-csg-non-manifold-edge")]
    [InlineData("degenerate", "render3d-csg-degenerate-triangle")]
    [InlineData("collinear", "render3d-csg-degenerate-triangle")]
    [InlineData("inverted", "render3d-csg-inverted-mesh")]
    public void CsgOperandValidator_Validate_DefectKind_ReportsFeatureAndRendersAnyway(string kind, string feature)
    {
        // Arrange
        var node = new MeshNode(CreateDefective(kind));
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(node, reports);

        // Assert
        var report = Assert.Single(reports, r => r.Feature == feature);
        Assert.Same(node, report.Node);
        Assert.False(string.IsNullOrWhiteSpace(report.Message));
    }

    /// <summary>
    ///     Validates that a healthy closed scene produces no reports.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_HealthyOperands_ReportsNothing()
    {
        // Arrange
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(new MeshNode(Render3DTestHelpers.CreateBoxMesh(new Vector3D(0, 0, 0), new Vector3D(2, 2, 2))), reports);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that strict mode throws the typed exception for the first problem, carrying feature and node.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_StrictMode_ThrowsTypedException()
    {
        // Arrange
        var node = new MeshNode(CreateDefective("open"));
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        var ex = Assert.Throws<SceneRenderUnsupportedFeatureException>(() =>
            RenderDifference(node, reports, o => new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = reports.Add, ThrowOnUnsupportedFeature = true }));

        // Assert: nothing reached the handler (strict mode throws instead) and the exception identifies the problem
        Assert.Equal("render3d-csg-open-mesh", ex.Feature);
        Assert.Same(node, ex.Node);
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that strict mode works without a handler.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_StrictModeWithoutHandler_Throws()
    {
        // Arrange
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), new MeshNode(CreateDefective("inverted"))]));
        var options = new SceneRenderOptions { Supersampling = 1, ThrowOnUnsupportedFeature = true };
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        var ex = Assert.Throws<SceneRenderUnsupportedFeatureException>(() => SceneRenderer.Render(scene, camera, 32, 32, options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("render3d-csg-inverted-mesh", ex.Feature);
    }

    /// <summary>
    ///     Validates that a default (tolerant) render without a handler completes for a defective operand.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_NoHandlerNoStrict_RendersTolerantly()
    {
        // Arrange
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), new MeshNode(CreateDefective("open"))]));
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        using var first = SceneRenderer.Render(scene, camera, 32, 32, new SceneRenderOptions { Supersampling = 1 }, TestContext.Current.CancellationToken);
        using var second = SceneRenderer.Render(scene, camera, 32, 32, new SceneRenderOptions { Supersampling = 1 }, TestContext.Current.CancellationToken);

        // Assert: bounded, deterministic output
        Render3DTestHelpers.AssertSurfacesIdentical(first, second);
    }

    /// <summary>
    ///     Validates that a union-only scene is never validated: an open mesh reports nothing and renders via the z-buffer.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_UnionOnlyScene_ReportsNothing()
    {
        // Arrange
        var reports = new List<SceneRenderUnsupportedFeature>();
        var scene = new Scene(new GroupNode([new MeshNode(CreateDefective("quad")), new MeshNode(CreateDefective("inverted")), new MeshNode(Cube)]));
        var options = new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = reports.Add, ThrowOnUnsupportedFeature = true };
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 32, 32, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(reports);
        Assert.True(Render3DTestHelpers.DistinctColors(surface).Count > 1);
    }

    /// <summary>
    ///     Validates that every mesh of a CSG-path scene is validated (not only the operands of the difference),
    ///     that a shared node is reported once and that the order is the scene pre-order and repeatable.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_AllInstances_DeduplicatedInPreOrder()
    {
        // Arrange: a defective node shared twice, a second defective node and a defective union branch next to the CSG
        var shared = new MeshNode(CreateDefective("open"));
        var inverted = new MeshNode(CreateDefective("inverted"));
        var branch = new MeshNode(CreateDefective("winding"));
        var scene = new Scene(new GroupNode(
        [
            new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), shared, inverted, shared]),
            branch,
        ]));
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        List<SceneRenderUnsupportedFeature> Run()
        {
            var reports = new List<SceneRenderUnsupportedFeature>();
            using var surface = SceneRenderer.Render(scene, camera, 32, 32, new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = reports.Add }, TestContext.Current.CancellationToken);
            return reports;
        }

        var first = Run();
        var second = Run();

        // Assert
        Assert.Equal(
            ["render3d-csg-open-mesh", "render3d-csg-inverted-mesh", "render3d-csg-inconsistent-winding"],
            first.Select(r => r.Feature).ToArray());
        Assert.Same(shared, first[0].Node);
        Assert.Same(inverted, first[1].Node);
        Assert.Same(branch, first[2].Node);
        Assert.Equal(first.Select(r => r.Feature), second.Select(r => r.Feature));
    }

    /// <summary>
    ///     Validates that a mirrored outward mesh is fine while a mirrored inverted mesh is still reported as
    ///     inverted: the renderer's winding correction restores the orientation the mesh data describes.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_MirroredOperands_InversionIsIndependentOfDeterminant()
    {
        // Arrange
        var mirror = Matrix4x4D.CreateMirror(Vector3D.UnitX);
        var doubleMirror = Matrix4x4D.CreateMirror(Vector3D.UnitX) * Matrix4x4D.CreateMirror(Vector3D.UnitY);
        var outward = new List<SceneRenderUnsupportedFeature>();
        var inverted = new List<SceneRenderUnsupportedFeature>();
        var invertedTwice = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(new MeshNode(Cube) { Transform = mirror }, outward);
        RenderDifference(new MeshNode(CreateDefective("inverted")) { Transform = mirror }, inverted);
        RenderDifference(new MeshNode(CreateDefective("inverted")) { Transform = doubleMirror }, invertedTwice);

        // Assert
        Assert.Empty(outward);
        Assert.Equal(["render3d-csg-inverted-mesh"], inverted.Select(r => r.Feature).ToArray());
        Assert.Equal(["render3d-csg-inverted-mesh"], invertedTwice.Select(r => r.Feature).ToArray());
    }

    /// <summary>
    ///     Validates that a mirrored inverted operand really renders inside out (so the report is accurate):
    ///     it equals the baked inverted-and-mirrored mesh.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_MirroredInvertedOperand_RendersLikeBakedMesh()
    {
        // Arrange
        var mirror = Matrix4x4D.CreateMirror(Vector3D.UnitX);
        var inverted = CreateDefective("inverted");
        var viaTransform = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), new MeshNode(inverted) { Transform = mirror }]));
        var baked = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), new MeshNode(inverted.Transform(mirror))]));
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        using var a = SceneRenderer.Render(viaTransform, camera, 48, 48, new SceneRenderOptions { Supersampling = 1 }, TestContext.Current.CancellationToken);
        using var b = SceneRenderer.Render(baked, camera, 48, 48, new SceneRenderOptions { Supersampling = 1 }, TestContext.Current.CancellationToken);

        // Assert
        Render3DTestHelpers.AssertSurfacesIdentical(b, a);
    }

    /// <summary>
    ///     Validates that a zero-scale world transform on a CSG operand is reported as a singular transform
    ///     for that node (and the render still completes), without any mesh-level report.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_ValidateTransformed_ZeroScaleOperand_Reported()
    {
        // Arrange: the mesh itself is a healthy closed cube
        var node = new MeshNode(Cube) { Transform = Matrix4x4D.CreateScale(new Vector3D(1, 1, 0)) };
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(node, reports);

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("render3d-csg-singular-transform", report.Feature);
        Assert.Same(node, report.Node);
        Assert.False(string.IsNullOrWhiteSpace(report.Message));
    }

    /// <summary>
    ///     Validates that strict mode throws the typed exception for a singular operand transform.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_ValidateTransformed_ZeroScaleOperand_StrictModeThrows()
    {
        // Arrange
        var node = new MeshNode(Cube) { Transform = Matrix4x4D.CreateScale(0.0) };
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        var ex = Assert.Throws<SceneRenderUnsupportedFeatureException>(() =>
            RenderDifference(node, reports, _ => new SceneRenderOptions { Supersampling = 1, ThrowOnUnsupportedFeature = true }));

        // Assert
        Assert.Equal("render3d-csg-singular-transform", ex.Feature);
        Assert.Same(node, ex.Node);
    }

    /// <summary>
    ///     Validates that a node shared by two instances, one valid and one singular, is reported for the
    ///     singular instance only (the mesh-level analysis is per node, the transform check per instance).
    /// </summary>
    [Fact]
    public void CsgOperandValidator_ValidateTransformed_SharedNodeOneSingularInstance_ReportedOnce()
    {
        // Arrange
        var shared = new MeshNode(Cube);
        var valid = new GroupNode([shared]) { Transform = Matrix4x4D.CreateTranslation(new Vector3D(0.5, 0, 0)) };
        var collapsed = new GroupNode([shared]) { Transform = Matrix4x4D.CreateScale(new Vector3D(1, 0, 1)) };
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), valid, collapsed]));
        var reports = new List<SceneRenderUnsupportedFeature>();
        var options = new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = reports.Add };
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 32, 32, options, TestContext.Current.CancellationToken);

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("render3d-csg-singular-transform", report.Feature);
        Assert.Same(shared, report.Node);
    }

    /// <summary>
    ///     Validates that valid transformed operands (rotation, non-uniform scale, translation, mirroring) produce no reports.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_ValidateTransformed_ValidTransformedOperands_ReportNothing()
    {
        // Arrange
        var transform = Matrix4x4D.CreateScale(new Vector3D(0.5, 0.25, 0.75))
            * Matrix4x4D.CreateRotationZ(30)
            * Matrix4x4D.CreateTranslation(new Vector3D(0.3, -0.2, 0.1));
        var reports = new List<SceneRenderUnsupportedFeature>();
        var mirrored = Matrix4x4D.CreateMirror(Vector3D.UnitX) * transform;

        // Act
        RenderDifference(new MeshNode(Cube) { Transform = transform }, reports);
        RenderDifference(new MeshNode(Cube) { Transform = mirrored }, reports);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a union-only scene with a zero-scale instance is not validated.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_ValidateTransformed_UnionOnlyScene_ReportsNothing()
    {
        // Arrange
        var reports = new List<SceneRenderUnsupportedFeature>();
        var scene = new Scene(new GroupNode([new MeshNode(Cube) { Transform = Matrix4x4D.CreateScale(0.0) }, new MeshNode(Cube)]));
        var options = new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = reports.Add, ThrowOnUnsupportedFeature = true };
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 32, 32, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates the per-instance checks directly: a non-finite determinant is singular, triangles that
    ///     collapse in view space are reported with the renderer's own criterion, and a mesh that already
    ///     carries the degenerate defect is not reported a second time.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_ValidateTransformed_DegenerateInViewSpace_ReportedWithRendererCriterion()
    {
        // Arrange: a non-singular world transform, but view-space vertices flattened until the top and bottom faces collapse
        var node = new MeshNode(Cube);
        var flattened = Cube.Vertices.Select(v => new Vector3D(v.X * 1e-200, v.Y * 1e-200, v.Z)).ToArray();
        var overflowed = new SceneInstance(Cube, Matrix4x4D.CreateScale(1e200), null, 0, node);
        var instance = new SceneInstance(Cube, Matrix4x4D.Identity, null, 0, node);
        var alreadyDegenerate = new MeshNode(CreateDefective("degenerate"));
        var reports = new List<SceneRenderUnsupportedFeature>();
        var options = new SceneRenderOptions { UnsupportedFeatureHandler = reports.Add };
        var triangles = Cube.TriangleIndices;
        var collapsedCount = Enumerable.Range(0, triangles.Count / 3).Count(t =>
            !MeshTopology.TryGetFaceNormal(flattened[triangles[3 * t]], flattened[triangles[(3 * t) + 1]], flattened[triangles[(3 * t) + 2]], out _));

        // Act
        CsgOperandValidator.ValidateTransformed(options, instance, flattened);
        CsgOperandValidator.ValidateTransformed(options, overflowed, Cube.Vertices.ToArray());
        CsgOperandValidator.ValidateTransformed(options, new SceneInstance(alreadyDegenerate.Mesh, Matrix4x4D.Identity, null, 1, alreadyDegenerate), flattened.Take(alreadyDegenerate.Mesh.Vertices.Count).ToArray());

        // Assert
        Assert.True(collapsedCount > 0);
        Assert.Equal(
            ["render3d-csg-degenerate-triangle", "render3d-csg-singular-transform"],
            reports.Select(r => r.Feature).ToArray());
        Assert.All(reports.Take(2), r => Assert.Same(node, r.Node));
        Assert.Empty(reports.Skip(2));
    }

    /// <summary>
    ///     Validates that a mirrored operand transform stays free of transform reports while a mirrored
    ///     inverted mesh is still reported as inverted only (the winding swap cancels the mirror reversal).
    /// </summary>
    [Fact]
    public void CsgOperandValidator_ValidateTransformed_MirroredInvertedOperand_OnlyInvertedReported()
    {
        // Arrange
        var mirror = Matrix4x4D.CreateMirror(Vector3D.UnitX);
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(new MeshNode(CreateDefective("inverted")) { Transform = mirror }, reports);

        // Assert
        Assert.Equal(["render3d-csg-inverted-mesh"], reports.Select(r => r.Feature).ToArray());
    }

    /// <summary>
    ///     Validates that an explicit near plane in front of operand geometry is not a reportable problem
    ///     (the resolver supports it): nothing is delivered and strict mode does not throw.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_ExplicitNearPlaneInFrontOfOperands_ReportsNothing()
    {
        // Arrange: the nearest cube vertex is about 8 units from the eye, the near plane is at 9.5
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), new MeshNode(Render3DTestHelpers.CreateBoxMesh(new Vector3D(0, 0, 0), new Vector3D(2, 2, 2)))]));
        var camera = new Camera { Eye = new Vector3D(10, 0, 0), Target = Vector3D.Zero, Up = Vector3D.UnitZ, NearPlane = 9.5 };
        var reports = new List<SceneRenderUnsupportedFeature>();
        var options = new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = reports.Add, ThrowOnUnsupportedFeature = true };

        // Act
        using var surface = SceneRenderer.Render(scene, camera, 32, 32, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(reports);
        Assert.Equal(32, surface.Width);
    }

    /// <summary>
    ///     Validates that a valid shell plus a disjoint, non-nested inside-out shell is reported as inverted even though
    ///     the total signed volume is positive (a 4 x 4 x 4 cube and a reversed 1 x 1 x 1 cube at x = 5).
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_DisjointInvertedShell_ReportsInvertedMesh()
    {
        // Arrange
        var mesh = Render3DTestHelpers.Combine(
            Render3DTestHelpers.CreateBoxMesh(Vector3D.Zero, new Vector3D(4, 4, 4)),
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(5, 0, 0), Vector3D.One).Reverse());
        var node = new MeshNode(mesh);
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(node, reports);

        // Assert
        Assert.Equal(63.0, mesh.SignedVolume, 1e-9);
        var report = Assert.Single(reports);
        Assert.Equal("render3d-csg-inverted-mesh", report.Feature);
        Assert.Same(node, report.Node);
    }

    /// <summary>
    ///     Validates that a hollow solid (outward outer shell, inward cavity shell) is not reported.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_HollowSolid_ReportsNothing()
    {
        // Arrange
        var hollow = Render3DTestHelpers.Combine(
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(-5, -5, -5), new Vector3D(10, 10, 10)),
            Render3DTestHelpers.CreateBoxMesh(new Vector3D(-2, -2, -2), new Vector3D(4, 4, 4)).Reverse());
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(new MeshNode(hollow), reports);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates the three-level nesting: a solid inside a cavity inside a solid is valid, while wrong
    ///     orientation at any level (here the solid in the cavity wound inward) is reported.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_NestedThreeLevelShells_JudgedByNestingParity()
    {
        // Arrange: outer (depth 0, outward), cavity (depth 1, inward), core (depth 2, outward or wrongly inward)
        var outer = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-5, -5, -5), new Vector3D(10, 10, 10));
        var cavity = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-3, -3, -3), new Vector3D(6, 6, 6)).Reverse();
        var core = Render3DTestHelpers.CreateBoxMesh(new Vector3D(-1, -1, -1), new Vector3D(2, 2, 2));
        var valid = Render3DTestHelpers.Combine(outer, cavity, core);
        var wrongCore = Render3DTestHelpers.Combine(outer, cavity, core.Reverse());
        var wrongCavity = Render3DTestHelpers.Combine(outer, cavity.Reverse(), core);
        var validReports = new List<SceneRenderUnsupportedFeature>();
        var wrongCoreReports = new List<SceneRenderUnsupportedFeature>();
        var wrongCavityReports = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(new MeshNode(valid), validReports);
        RenderDifference(new MeshNode(wrongCore), wrongCoreReports);
        RenderDifference(new MeshNode(wrongCavity), wrongCavityReports);

        // Assert
        Assert.Empty(validReports);
        Assert.Equal(["render3d-csg-inverted-mesh"], wrongCoreReports.Select(r => r.Feature).ToArray());
        Assert.Equal(["render3d-csg-inverted-mesh"], wrongCavityReports.Select(r => r.Feature).ToArray());
    }

    /// <summary>
    ///     Validates that an unwelded (one-vertex-per-corner, STL-style) closed cube is not reported as open,
    ///     even in strict mode, while an unwelded reversed cube is reported as inverted.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_UnweldedClosedCube_NotOpenAndInversionDetected()
    {
        // Arrange
        var unwelded = Render3DTestHelpers.ToUnwelded(Cube);
        var unweldedReversed = Render3DTestHelpers.ToUnwelded(Cube.Reverse());
        var healthy = new List<SceneRenderUnsupportedFeature>();
        var inverted = new List<SceneRenderUnsupportedFeature>();

        // Act: strict mode must not throw for the healthy unwelded cube
        RenderDifference(
            new MeshNode(unwelded),
            healthy,
            _ => new SceneRenderOptions { Supersampling = 1, UnsupportedFeatureHandler = healthy.Add, ThrowOnUnsupportedFeature = true });
        RenderDifference(new MeshNode(unweldedReversed), inverted);

        // Assert
        Assert.Equal(36, unwelded.VertexCount);
        Assert.Empty(healthy);
        Assert.Equal(["render3d-csg-inverted-mesh"], inverted.Select(r => r.Feature).ToArray());
    }

    /// <summary>
    ///     Validates that an unwelded mesh with a genuine hole is still reported as open.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_UnweldedOpenCube_StillReportsOpenMesh()
    {
        // Arrange: drop one triangle of the unwelded cube
        var unwelded = Render3DTestHelpers.ToUnwelded(Cube);
        var open = new Mesh(unwelded.Vertices, unwelded.TriangleIndices.Take(unwelded.TriangleIndices.Count - 3));
        var reports = new List<SceneRenderUnsupportedFeature>();

        // Act
        RenderDifference(new MeshNode(open), reports);

        // Assert
        Assert.Contains(reports, r => r.Feature == "render3d-csg-open-mesh");
    }

    /// <summary>
    ///     Validates that validation runs before rendering work: a strict report wins over a tiny fragment budget.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_RunsBeforeRendering()
    {
        // Arrange
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), new MeshNode(CreateDefective("open"))]));
        var options = new SceneRenderOptions { Supersampling = 1, MaxFragmentsPerTile = 1, ThrowOnUnsupportedFeature = true };
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act / Assert
        Assert.Throws<SceneRenderUnsupportedFeatureException>(() => SceneRenderer.Render(scene, camera, 32, 32, options, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that validation observes cancellation: cancelling from the handler stops the render.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_Validate_CancelledFromHandler_ThrowsOperationCanceled()
    {
        // Arrange: two defective nodes so the loop checks the token again after the handler cancelled
        using var source = new CancellationTokenSource();
        var scene = new Scene(new CsgNode(CsgOperation.Difference, [new MeshNode(Cube), new MeshNode(CreateDefective("open")), new MeshNode(CreateDefective("inverted"))]));
        var delivered = 0;
        var options = new SceneRenderOptions
        {
            Supersampling = 1,
            UnsupportedFeatureHandler = _ =>
            {
                delivered++;
                source.Cancel();
            },
        };
        var camera = Camera.LookAt(new Vector3D(6, -7, 5), Vector3D.Zero, Vector3D.UnitZ);

        // Act
        var ex = Assert.ThrowsAny<OperationCanceledException>(() => SceneRenderer.Render(scene, camera, 32, 32, options, source.Token));

        // Assert: only the first report was delivered
        Assert.Equal(1, delivered);
        Assert.NotNull(ex);
    }

    /// <summary>
    ///     Validates the argument checks of the validator entry points.
    /// </summary>
    [Fact]
    public void CsgOperandValidator_NullArguments_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => CsgOperandValidator.IsEnabled(null!));
        Assert.Throws<ArgumentNullException>(() => CsgOperandValidator.Validate(null!, SceneRenderOptions.Default, CancellationToken.None));
        Assert.Throws<ArgumentNullException>(() => CsgOperandValidator.ValidateTransformed(null!, new SceneInstance(Cube, Matrix4x4D.Identity, null, 0, new MeshNode(Cube)), []));
        Assert.Throws<ArgumentNullException>(() => CsgOperandValidator.ValidateTransformed(SceneRenderOptions.Default, new SceneInstance(Cube, Matrix4x4D.Identity, null, 0, new MeshNode(Cube)), null!));
        Assert.False(CsgOperandValidator.IsEnabled(SceneRenderOptions.Default));
        Assert.True(CsgOperandValidator.IsEnabled(new SceneRenderOptions { ThrowOnUnsupportedFeature = true }));
    }
}
