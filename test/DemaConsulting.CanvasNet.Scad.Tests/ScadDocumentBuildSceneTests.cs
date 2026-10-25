using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using DemaConsulting.CanvasNet.Scad.Tests.Geometry;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Tests of <see cref="ScadDocument.BuildScene(ScadRenderOptions?, CancellationToken)"/>: scene content, option
///     pass-through (variables, <c>$t</c>, limits, flat thickness), the single delivery of events and cancellation.
/// </summary>
public class ScadDocumentBuildSceneTests
{
    /// <summary>
    ///     Validates that a program builds into a scene with default options, and that an empty program gives an
    ///     empty scene.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_DefaultOptions_BuildsSceneOfTheModel()
    {
        // Arrange / Act
        var scene = ScadDocument.Parse("cube(2);").BuildScene(cancellationToken: TestContext.Current.CancellationToken);
        var empty = ScadDocument.Parse(string.Empty).BuildScene(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var mesh = Assert.Single(ScadGeometryTestSupport.MeshNodes(scene)).Mesh;
        Assert.Equal(8.0, mesh.SignedVolume, 1e-12);
        Assert.Empty(ScadGeometryTestSupport.MeshNodes(empty));
        Assert.True(empty.ComputeBounds().IsEmpty);
    }

    /// <summary>
    ///     Validates that <c>-D</c> style variables, a special-variable override and <c>$t</c> reach the evaluation.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_VariablesAndTime_ChangeTheGeometry()
    {
        // Arrange
        var sized = ScadDocument.Parse("size = 1;\ncube(size);");
        var round = ScadDocument.Parse("sphere(2);");
        var timed = ScadDocument.Parse("cube($t + 1);");

        // Act
        var big = sized.BuildScene(new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["size"] = 5 } } }, TestContext.Current.CancellationToken);
        var coarse = round.BuildScene(new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { Variables = new Dictionary<string, object?> { ["$fn"] = 8 } } }, TestContext.Current.CancellationToken);
        var later = timed.BuildScene(new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { Time = 1.0 } }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(125.0, ScadGeometryTestSupport.MeshNodes(big).Single().Mesh.SignedVolume, 1e-9);
        Assert.Equal(26, ScadGeometryTestSupport.MeshNodes(coarse).Single().Mesh.VertexCount);
        Assert.Equal(8.0, ScadGeometryTestSupport.MeshNodes(later).Single().Mesh.SignedVolume, 1e-9);
    }

    /// <summary>
    ///     Validates that the flat-shape thickness of the options sets the slab height.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_FlatShapeThickness_SetsSlabHeight()
    {
        // Arrange / Act
        var scene = ScadDocument.Parse("square(2);").BuildScene(new ScadRenderOptions { FlatShapeThickness = 3.0 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3.0, scene.ComputeBounds().Max.Z, 1e-12);
    }

    /// <summary>
    ///     Validates that limits of the evaluation options apply: the instance limit stops evaluation, the triangle
    ///     limit stops the build.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_Limits_ArePassedThrough()
    {
        // Arrange
        var instances = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { Limits = new ScadLimits { MaxInstances = 2 } } };
        var triangles = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { Limits = new ScadLimits { MaxTriangles = 20 } } };

        // Act
        var byInstances = Assert.Throws<ScadEvaluationException>(() => ScadDocument.Parse("for (i = [1:5]) cube(1);").BuildScene(instances, TestContext.Current.CancellationToken));
        var byTriangles = Assert.Throws<ScadEvaluationException>(() => ScadDocument.Parse("cube(1);\ncube(2);").BuildScene(triangles, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("limit", byInstances.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("2:1: Triangle limit exceeded (limit 20)", byTriangles.Message);
    }

    /// <summary>
    ///     Validates that echo lines, warnings and unsupported features of all stages reach the handlers once, in the
    ///     order they occurred, with locations.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_Handlers_ReceiveAllStagesOnceInOrder()
    {
        // Arrange
        var events = new List<string>();
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions
            {
                EchoHandler = line => events.Add("echo " + line),
                WarningHandler = w => events.Add($"warn {w.Location} {w.Message}"),
                UnsupportedFeatureHandler = u => events.Add($"unsupported {u.Location} {u.Feature}"),
            },
        };

        // Act
        ScadDocument.Parse("echo(1);\ncube(0);\nhull() cube(1);").BuildScene(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "echo ECHO: 1",
                "warn 2:1 cube: size must be positive; the instance is ignored",
                "unsupported 3:1 scad-module-hull",
            ],
            events);
    }

    /// <summary>
    ///     Validates that strict mode throws for a geometry-stage unsupported feature, and that the events recorded
    ///     before and including it are still delivered.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_StrictMode_ThrowsAndStillDeliversEvents()
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

        // Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(
            () => ScadDocument.Parse("echo(\"before\");\nhull() cube(1);").BuildScene(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("scad-module-hull", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 1), ex.Location);
        Assert.Equal(["ECHO: \"before\""], echoes);
        Assert.Equal(["scad-module-hull"], features);
    }

    /// <summary>
    ///     Validates the handler-failure contract: a failing handler propagates from a successful build, but never
    ///     hides the failure of a failed one.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_FailingHandler_PropagatesOnlyOnSuccess()
    {
        // Arrange
        var failing = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { WarningHandler = _ => throw new InvalidOperationException("handler") },
        };
        var failingWithError = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions
            {
                WarningHandler = _ => throw new InvalidOperationException("handler"),
                Limits = new ScadLimits { MaxTriangles = 1 },
            },
        };

        // Act
        var onSuccess = Assert.Throws<InvalidOperationException>(() => ScadDocument.Parse("cube(0);").BuildScene(failing, TestContext.Current.CancellationToken));
        var onFailure = Assert.Throws<ScadEvaluationException>(() => ScadDocument.Parse("cube(0);\ncube(1);").BuildScene(failingWithError, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("handler", onSuccess.Message);
        Assert.Contains("Triangle limit exceeded", onFailure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a token canceled before the call stops it before any file is requested.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_AlreadyCanceled_ThrowsBeforeAnyResolverCall()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var files = new ScadVirtualFiles(("lib.scad", "x = 1;\n"));
        var document = ScadDocument.Parse("include <lib.scad>\ncube(1);", new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "main.scad" });

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.BuildScene(cancellationToken: cts.Token));
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that a build is deterministic and thread-safe: concurrent builds of one document give equal
    ///     scenes.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_ConcurrentBuilds_AreEqual()
    {
        // Arrange
        var document = ScadDocument.Parse("sphere(3, $fn = 12);\ncylinder(h = 2, r = 1, $fn = 9);");

        // Act
        var scenes = Enumerable.Range(0, 8)
            .AsParallel()
            .Select(_ => ScadGeometryTestSupport.MeshNodes(document.BuildScene(cancellationToken: TestContext.Current.CancellationToken)).Select(m => (m.Mesh.VertexCount, m.Mesh.SignedVolume)).ToArray())
            .ToArray();

        // Assert
        Assert.All(scenes, s => Assert.Equal(scenes[0], s));
        Assert.Equal(2, scenes[0].Length);
    }
}
