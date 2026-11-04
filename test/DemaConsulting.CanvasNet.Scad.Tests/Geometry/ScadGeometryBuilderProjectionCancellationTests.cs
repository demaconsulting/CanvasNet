using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of cancellation in the long loops of the <c>projection</c> module: they poll on the counted schedule of
///     the geometry context. The projection is called directly with a token that is canceled up front, because the
///     tessellation of a primitive would poll first when the whole build is used.
/// </summary>
public class ScadGeometryBuilderProjectionCancellationTests
{
    /// <summary>
    ///     Validates that a canceled token stops the projection of a mesh with many triangles, with and without the cut.
    /// </summary>
    /// <param name="cut">Whether the projection is the cross-section.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScadGeometryBuilderProjectionCancellation_PreCanceledLargeMesh_Throws(bool cut)
    {
        // Arrange: a prism whose two caps are single faces of thousands of triangles (few face groups, so the per-group polls cannot stop it), built with a live token
        var (instance, solid) = Prepare(cut, "linear_extrude(1) circle(r = 5, $fn = 5000);");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = Context(cts.Token);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => ScadProjections.Projection(instance, [solid], context));
    }

    /// <summary>
    ///     Validates the counted interval: a mesh of a dozen triangles costs fewer polls than one interval, so the
    ///     shadow of it completes even with a canceled token (like a model with nothing to do). The cut is not used here
    ///     because it polls before every slice.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderProjectionCancellation_PreCanceledSmallMesh_CompletesBelowTheInterval()
    {
        // Arrange
        var (instance, solid) = Prepare(false, "cube(1);");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var context = Context(cts.Token);

        // Act
        var result = ScadProjections.Projection(instance, [solid], context);

        // Assert
        Assert.Equal(ScadGeoKind.Flat, result.Kind);
    }

    /// <summary>
    ///     Validates that the same large mesh projects normally with a live token (the polls do not interfere).
    /// </summary>
    /// <param name="cut">Whether the projection is the cross-section.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScadGeometryBuilderProjectionCancellation_LargeMeshNotCanceled_Completes(bool cut)
    {
        // Arrange
        var (instance, solid) = Prepare(cut, "linear_extrude(1) circle(r = 5, $fn = 5000);");
        var context = Context(CancellationToken.None);

        // Act
        var result = ScadProjections.Projection(instance, [solid], context);

        // Assert
        Assert.Equal(ScadGeoKind.Flat, result.Kind);
    }

    /// <summary>
    ///     Creates the projection instance and its solid child from a program text.
    /// </summary>
    /// <param name="cut">Whether the projection is the cross-section.</param>
    /// <param name="solidSource">The program text of the solid.</param>
    /// <returns>The instance of the projection and the solid to project.</returns>
    private static (ScadInstance Instance, ScadGeo Solid) Prepare(bool cut, string solidSource)
    {
        var instance = ScadEvaluatorTestSupport.Run(cut ? "projection(cut = true) cube(1);" : "projection() cube(1);").Roots[0];
        var mesh = Build(solidSource).Meshes[0].Mesh;
        return (instance, ScadGeo.FromMesh(new MeshNode(mesh)));
    }

    /// <summary>
    ///     Creates a geometry context with the given token.
    /// </summary>
    /// <param name="cancellation">The token.</param>
    /// <returns>The context.</returns>
    private static ScadGeometryContext Context(CancellationToken cancellation) =>
        new(Settings(cancellation: cancellation), new ScadReporter(new ScadEvaluationSink(ScadLimits.Default), false));
}
