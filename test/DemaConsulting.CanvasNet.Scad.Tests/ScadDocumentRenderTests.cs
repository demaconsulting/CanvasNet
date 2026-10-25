using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Tests of <see cref="ScadDocument.Render(int, int, ScadRenderOptions?, CancellationToken)"/>: argument
///     validation before any work, the fitted and explicit views, the look options, determinism, event delivery,
///     cancellation and stack safety.
/// </summary>
public class ScadDocumentRenderTests
{
    /// <summary>The default background of the renderer.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>The default surface color of the renderer, a neutral gray.</summary>
    private static readonly Rgba32 Gray = new(180, 180, 180, 255);

    /// <summary>The options that render flat colors without antialiasing, for exact pixel assertions.</summary>
    private static readonly SceneRenderOptions FlatOptions = new()
    {
        Lights = [new AmbientLight { Intensity = 1.0f }],
        Supersampling = 1,
    };

    /// <summary>
    ///     Validates that a size outside the surface limits is rejected before the program is evaluated.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="parameter">The expected parameter name.</param>
    [Theory]
    [InlineData(0, 10, "width")]
    [InlineData(-5, 10, "width")]
    [InlineData(Surface.MaxDimension + 1, 10, "width")]
    [InlineData(10, 0, "height")]
    [InlineData(10, -1, "height")]
    [InlineData(10, Surface.MaxDimension + 1, "height")]
    public void ScadDocument_Render_InvalidSize_ThrowsBeforeEvaluating(int width, int height, string parameter)
    {
        // Arrange
        var files = new ScadVirtualFiles(("lib.scad", "x = 1;\n"));
        var document = ScadDocument.Parse("include <lib.scad>\ncube(1);", new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "main.scad" });

        // Act
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => document.Render(width, height, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(parameter, ex.ParamName);
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that an invalid camera is rejected by the renderer's own rules before the program is evaluated.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_InvalidCamera_ThrowsBeforeEvaluating()
    {
        // Arrange: the eye coincides with the target
        var files = new ScadVirtualFiles(("lib.scad", "x = 1;\n"));
        var document = ScadDocument.Parse("include <lib.scad>\ncube(1);", new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "main.scad" });
        var camera = new Camera { Eye = new Vector3D(1, 1, 1), Target = new Vector3D(1, 1, 1), Up = Vector3D.UnitZ };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => document.Render(8, 8, new ScadRenderOptions { Camera = camera }, TestContext.Current.CancellationToken));
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that an empty model renders the background only, at the requested size, and that the
    ///     background of the scene options is honored.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_EmptyModel_IsBackgroundOnlyAtRequestedSize()
    {
        // Arrange
        var document = ScadDocument.Parse(string.Empty);
        var background = new Rgba32(10, 20, 30, 255);

        // Act
        using var plain = document.Render(13, 7, cancellationToken: TestContext.Current.CancellationToken);
        using var colored = document.Render(13, 7, new ScadRenderOptions { SceneOptions = new SceneRenderOptions { BackgroundColor = background } }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((13, 7), (plain.Width, plain.Height));
        Assert.Equal(0, CountNon(plain, White));
        Assert.Equal(0, CountNon(colored, background));
    }

    /// <summary>
    ///     Validates that, with default options, the fitted render of a cube equals the renderer's fitted render of
    ///     the same mesh, and that the model is visible.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_DefaultFittedView_EqualsRenderFittedOfSameMesh()
    {
        // Arrange
        var document = ScadDocument.Parse("cube(10);");
        var scene = new Scene(new MeshNode(PrimitiveTessellator.Box(new Vector3D(10, 10, 10))));

        // Act
        using var rendered = document.Render(64, 48, cancellationToken: TestContext.Current.CancellationToken);
        using var expected = SceneRenderer.RenderFitted(scene, 64, 48, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        AssertSurfacesIdentical(expected, rendered);
        Assert.True(CountNon(rendered, White) > 200);
    }

    /// <summary>
    ///     Validates that an explicit orthographic camera and flat lighting render a cube as exactly its analytic
    ///     footprint in the default gray, and that the scene options are honored.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_ExplicitOrthographicTopCamera_CoversExactFootprint()
    {
        // Arrange: 20 x 20 pixels show 20 x 20 units around (5, 5), so the cube covers the 10 x 10 pixel center
        var camera = new Camera
        {
            Eye = new Vector3D(5, 5, 50),
            Target = new Vector3D(5, 5, 0),
            Up = Vector3D.UnitY,
            Projection = CameraProjection.Orthographic,
            OrthographicHeight = 20,
        };
        var options = new ScadRenderOptions { Camera = camera, SceneOptions = FlatOptions };

        // Act
        using var surface = ScadDocument.Parse("cube(10);").Render(20, 20, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(100, CountNon(surface, White));
        Assert.Equal(Gray, surface.GetRowSpan(10)[10]);
        Assert.Equal(White, surface.GetRowSpan(2)[2]);
        Assert.Equal(Gray, surface.GetRowSpan(5)[5]);
        Assert.Equal(White, surface.GetRowSpan(4)[4]);
        Assert.Equal(Gray, surface.GetRowSpan(14)[14]);
        Assert.Equal(White, surface.GetRowSpan(15)[15]);
    }

    /// <summary>
    ///     Validates that the fitted view options (azimuth, elevation, projection) change the image.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_FittedViewOptions_ChangeTheImage()
    {
        // Arrange
        var document = ScadDocument.Parse("cube([10, 4, 2]);");

        // Act
        using var isometric = document.Render(40, 40, cancellationToken: TestContext.Current.CancellationToken);
        using var azimuth = document.Render(40, 40, new ScadRenderOptions { ViewAzimuthDegrees = 100 }, TestContext.Current.CancellationToken);
        using var elevation = document.Render(40, 40, new ScadRenderOptions { ViewElevationDegrees = 80 }, TestContext.Current.CancellationToken);
        using var orthographic = document.Render(40, 40, new ScadRenderOptions { Projection = CameraProjection.Orthographic }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(AreIdentical(isometric, azimuth));
        Assert.False(AreIdentical(isometric, elevation));
        Assert.False(AreIdentical(isometric, orthographic));
    }

    /// <summary>
    ///     Validates that a model color-free render uses the default gray and a changed default color is honored.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_DefaultColorOption_IsHonored()
    {
        // Arrange
        var camera = new Camera
        {
            Eye = new Vector3D(0.5, 0.5, 10),
            Target = new Vector3D(0.5, 0.5, 0),
            Up = Vector3D.UnitY,
            Projection = CameraProjection.Orthographic,
            OrthographicHeight = 1,
        };
        var red = new Rgba32(200, 10, 10, 255);
        var scene = new SceneRenderOptions { Lights = [new AmbientLight { Intensity = 1.0f }], Supersampling = 1, DefaultColor = red };

        // Act
        using var surface = ScadDocument.Parse("cube(1);").Render(4, 4, new ScadRenderOptions { Camera = camera, SceneOptions = scene }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(16, CountPixels(surface, red));
    }

    /// <summary>
    ///     Validates that two renders are byte-identical, and that the degree of parallelism does not change them.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_IsDeterministicForEveryParallelism()
    {
        // Arrange
        var document = ScadDocument.Parse("sphere(5, $fn = 24);\ncylinder(h = 8, r = 2, $fn = 16);");
        var serial = new ScadRenderOptions { SceneOptions = new SceneRenderOptions { MaxDegreeOfParallelism = 1 } };
        var parallel = new ScadRenderOptions { SceneOptions = new SceneRenderOptions { MaxDegreeOfParallelism = -1 } };

        // Act
        using var first = document.Render(70, 50, cancellationToken: TestContext.Current.CancellationToken);
        using var second = document.Render(70, 50, cancellationToken: TestContext.Current.CancellationToken);
        using var one = document.Render(70, 50, serial, TestContext.Current.CancellationToken);
        using var many = document.Render(70, 50, parallel, TestContext.Current.CancellationToken);

        // Assert
        AssertSurfacesIdentical(first, second);
        AssertSurfacesIdentical(first, one);
        AssertSurfacesIdentical(one, many);
    }

    /// <summary>
    ///     Validates that the reports of evaluation and geometry go through the evaluation handlers exactly once, and
    ///     that the handler and strict flag inside the scene options are ignored.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_ReportsThroughEvaluationHandlersOnly()
    {
        // Arrange
        var features = new List<string>();
        var sceneHandlerCalls = 0;
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { UnsupportedFeatureHandler = u => features.Add(u.Feature) },
            SceneOptions = new SceneRenderOptions
            {
                UnsupportedFeatureHandler = _ => sceneHandlerCalls++,
                ThrowOnUnsupportedFeature = true,
                Supersampling = 1,
            },
        };

        // Act
        using var surface = ScadDocument.Parse("cube(1);\nhull() cube(1);").Render(16, 16, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["scad-module-hull"], features);
        Assert.Equal(0, sceneHandlerCalls);
        Assert.True(CountNon(surface, White) > 0);
    }

    /// <summary>
    ///     Validates that strict mode throws from the geometry stage and still delivers the events.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_StrictMode_ThrowsForUnsupportedFeature()
    {
        // Arrange
        var features = new List<string>();
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true, UnsupportedFeatureHandler = u => features.Add(u.Feature) },
        };

        // Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => ScadDocument.Parse("hull() cube(1);").Render(8, 8, options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("scad-module-hull", ex.Feature);
        Assert.Equal(["scad-module-hull"], features);
    }

    /// <summary>
    ///     Validates that an evaluation failure propagates from <c>Render</c> after the earlier events were delivered.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_EvaluationFailure_PropagatesAfterDeliveringEvents()
    {
        // Arrange
        var echoes = new List<string>();
        var options = new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { EchoHandler = echoes.Add } };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => ScadDocument.Parse("echo(1);\nassert(false);").Render(8, 8, options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(2, ex.Location.Line);
        Assert.Equal(["ECHO: 1"], echoes);
    }

    /// <summary>
    ///     Validates that a failing handler on a successful render propagates (and the surface does not leak).
    /// </summary>
    [Fact]
    public void ScadDocument_Render_FailingHandlerOnSuccess_Propagates()
    {
        // Arrange
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { EchoHandler = _ => throw new InvalidOperationException("handler") },
        };

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => ScadDocument.Parse("echo(1);\ncube(1);").Render(8, 8, options, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("handler", ex.Message);
    }

    /// <summary>
    ///     Validates that a token canceled before the call stops it before any file is requested.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_AlreadyCanceled_ThrowsBeforeAnyResolverCall()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var files = new ScadVirtualFiles(("lib.scad", "x = 1;\n"));
        var document = ScadDocument.Parse("include <lib.scad>\ncube(1);", new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "main.scad" });

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => document.Render(8, 8, cancellationToken: cts.Token));
        Assert.Empty(files.Requests);
    }

    /// <summary>
    ///     Validates that a deep module chain renders from small caller stacks (64 KiB and 256 KiB) to the same image
    ///     as from a big stack: neither the evaluator, the builder nor the renderer needs the caller's stack.
    /// </summary>
    /// <param name="stackSize">The caller's stack size in bytes.</param>
    [Theory]
    [InlineData(64 * 1024)]
    [InlineData(SmallStack)]
    public void ScadDocument_Render_DeepModuleChain_OnSmallStack_MatchesBigStack(int stackSize)
    {
        // Arrange
        var document = ScadDocument.Parse("module r(n) { cube(1); if (n > 0) r(n - 1); }\nr(2000);");
        var options = new ScadRenderOptions
        {
            Evaluation = new ScadEvaluationOptions { Limits = new ScadLimits { MaxRecursionDepth = 16_384 } },
            SceneOptions = new SceneRenderOptions { Supersampling = 1 },
        };

        // Act
        using var big = OnSmallStack(() => document.Render(8, 8, options, TestContext.Current.CancellationToken), 256 * 1024 * 1024);
        using var small = OnSmallStack(() => document.Render(8, 8, options, TestContext.Current.CancellationToken), stackSize);

        // Assert
        AssertSurfacesIdentical(big, small);
        Assert.True(CountNon(small, White) > 0);
    }

    /// <summary>
    ///     Validates that concurrent renders of one document produce identical images.
    /// </summary>
    [Fact]
    public void ScadDocument_Render_ConcurrentRenders_AreIdentical()
    {
        // Arrange
        var document = ScadDocument.Parse("sphere(4, $fn = 16);");
        using var reference = document.Render(32, 32, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var token = TestContext.Current.CancellationToken;
        var surfaces = Enumerable.Range(0, 6).AsParallel().Select(_ => document.Render(32, 32, cancellationToken: token)).ToArray();

        // Assert
        try
        {
            Assert.All(surfaces, s => AssertSurfacesIdentical(reference, s));
        }
        finally
        {
            foreach (var surface in surfaces)
            {
                surface.Dispose();
            }
        }
    }

    /// <summary>
    ///     Determines whether two surfaces of the same size are byte-identical.
    /// </summary>
    /// <param name="left">The first surface.</param>
    /// <param name="right">The second surface.</param>
    /// <returns><see langword="true"/> when every row matches.</returns>
    private static bool AreIdentical(Surface left, Surface right)
    {
        for (var y = 0; y < left.Height; y++)
        {
            if (!left.GetRowSpanBytes(y).SequenceEqual(right.GetRowSpanBytes(y)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Counts the pixels of a surface that equal a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="color">The color.</param>
    /// <returns>The number of matching pixels.</returns>
    private static int CountPixels(Surface surface, Rgba32 color)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel == color)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
