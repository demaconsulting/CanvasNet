using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
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
        ScadDocument.Parse("echo(1);\ncube(0);\nroof() cube(1);").BuildScene(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                "echo ECHO: 1",
                "warn 2:1 cube: size must be positive; the instance is ignored",
                "unsupported 3:1 scad-module-roof",
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
            () => ScadDocument.Parse("echo(\"before\");\nroof() cube(1);").BuildScene(options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("scad-module-roof", ex.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 1), ex.Location);
        Assert.Equal(["ECHO: \"before\""], echoes);
        Assert.Equal(["scad-module-roof"], features);
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

    /// <summary>
    ///     Validates that the viewport variables are defined from the render options while the scene is built: an
    ///     explicit camera and explicit view angles steer <c>$vpd</c> and <c>$vpr</c>, the camera beats an override of
    ///     the evaluation options, and without them the defaults of the default view are used.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_CameraAndViewAngles_SteerTheViewportVariables()
    {
        // Arrange: the models size a cube by a viewport variable; the camera is 50 from its target
        var distance = ScadDocument.Parse("cube($vpd);");
        var azimuth = ScadDocument.Parse("cube($vpr[2]);");
        var camera = Camera.LookAt(new Vector3D(0, -50, 0), Vector3D.Zero, Vector3D.UnitZ);
        var overridden = new Dictionary<string, object?> { ["$vpd"] = 70 };

        // Act
        var byDefault = distance.BuildScene(cancellationToken: TestContext.Current.CancellationToken);
        var byCamera = distance.BuildScene(new ScadRenderOptions { Camera = camera }, TestContext.Current.CancellationToken);
        var cameraBeatsOverride = distance.BuildScene(
            new ScadRenderOptions { Camera = camera, Evaluation = new ScadEvaluationOptions { Variables = overridden } },
            TestContext.Current.CancellationToken);
        var overrideAlone = distance.BuildScene(
            new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { Variables = overridden } },
            TestContext.Current.CancellationToken);
        var defaultAngle = azimuth.BuildScene(cancellationToken: TestContext.Current.CancellationToken);
        var byAngle = azimuth.BuildScene(new ScadRenderOptions { ViewAzimuthDegrees = 90 }, TestContext.Current.CancellationToken);

        // Assert: the default distance, the distance of the camera (not the override), the override alone, the angles
        Assert.Equal(140.0, byDefault.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(50.0, byCamera.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(50.0, cameraBeatsOverride.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(70.0, overrideAlone.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(Camera.DefaultAzimuthDegrees, defaultAngle.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(90.0, byAngle.ComputeBounds().Max.X, 1e-9);
    }

    /// <summary>
    ///     Validates that a camera the renderer would reject defines no viewport variable and does not make the build
    ///     throw: the model sees the view angles or the defaults, while <c>Render</c> still rejects the same camera.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_InvalidCamera_DefinesNoViewportVariables()
    {
        // Arrange: the eye equals the target; and an orthographic camera without its height (it has a distance of 50)
        var distance = ScadDocument.Parse("cube($vpd);");
        var azimuth = ScadDocument.Parse("cube($vpr[2]);");
        var sameEye = new Camera { Eye = new Vector3D(1, 1, 1), Target = new Vector3D(1, 1, 1), Up = Vector3D.UnitZ };
        var noHeight = new Camera { Eye = new Vector3D(0, -50, 0), Target = Vector3D.Zero, Up = Vector3D.UnitZ, Projection = CameraProjection.Orthographic };
        var warnings = new List<string>();
        var evaluation = new ScadEvaluationOptions { WarningHandler = w => warnings.Add(w.Message) };

        // Act
        var byEye = distance.BuildScene(new ScadRenderOptions { Camera = sameEye, Evaluation = evaluation }, TestContext.Current.CancellationToken);
        var byHeight = distance.BuildScene(new ScadRenderOptions { Camera = noHeight, Evaluation = evaluation }, TestContext.Current.CancellationToken);
        var withAngle = azimuth.BuildScene(new ScadRenderOptions { Camera = sameEye, ViewAzimuthDegrees = 90 }, TestContext.Current.CancellationToken);
        var withoutAngle = azimuth.BuildScene(new ScadRenderOptions { Camera = sameEye }, TestContext.Current.CancellationToken);

        // Assert: the defaults of the default view, no warning about a zero distance, the explicit angle still applies
        Assert.Equal(140.0, byEye.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(140.0, byHeight.ComputeBounds().Max.X, 1e-9);
        Assert.Empty(warnings);
        Assert.Equal(90.0, withAngle.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(Camera.DefaultAzimuthDegrees, withoutAngle.ComputeBounds().Max.X, 1e-9);

        // Render rejects the same cameras, so the pair is pinned together
        Assert.Throws<ArgumentException>(() => distance.Render(8, 8, new ScadRenderOptions { Camera = sameEye }, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => distance.Render(8, 8, new ScadRenderOptions { Camera = noHeight }, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Validates that the projection of the render options does not change the scene: it changes no viewport
    ///     variable, and a build chooses no camera.
    /// </summary>
    [Fact]
    public void ScadDocument_BuildScene_Projection_DoesNotChangeTheScene()
    {
        // Arrange
        var document = ScadDocument.Parse("cube($vpd);");

        // Act
        var byDefault = document.BuildScene(cancellationToken: TestContext.Current.CancellationToken);
        var orthographic = document.BuildScene(new ScadRenderOptions { Projection = CameraProjection.Orthographic }, TestContext.Current.CancellationToken);
        var perspective = document.BuildScene(new ScadRenderOptions { Projection = CameraProjection.Perspective }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(140.0, byDefault.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(140.0, orthographic.ComputeBounds().Max.X, 1e-9);
        Assert.Equal(140.0, perspective.ComputeBounds().Max.X, 1e-9);
    }
}
