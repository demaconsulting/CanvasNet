using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using DemaConsulting.CanvasNet.Scad.Tests.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of cancellation in <see cref="ScadGeometryBuilder"/>: polled on a fixed schedule while walking and before
///     every tessellation, and never wrapped.
/// </summary>
public class ScadGeometryBuilderCancellationTests
{
    /// <summary>
    ///     Validates that a token canceled before the build stops it before the first tessellation.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_AlreadyCanceled_ThrowsBeforeTessellating()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => Build("cube(1);", Settings(cancellation: cts.Token)));
    }

    /// <summary>
    ///     Validates that a canceled token stops a walk over many instances at the polling schedule, even when the
    ///     instances need no tessellation.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_AlreadyCanceled_StopsWalkOverManyUnsupportedInstances()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert: 1,000 instances of a module that is not built never reach a tessellation
        Assert.Throws<OperationCanceledException>(
            () => Build("for (i = [1:1000]) translate([i, 0, 0]) cube(1);", Settings(cancellation: cts.Token)));
    }

    /// <summary>
    ///     Validates that an empty model has nothing to poll and so completes even with a canceled token, and that
    ///     an uncanceled token never interferes.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_NothingToDo_IgnoresCanceledToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var empty = Build(string.Empty, Settings(cancellation: cts.Token));
        var normal = Build("cube(1);", Settings(cancellation: CancellationToken.None));

        // Assert
        Assert.Empty(empty.Meshes);
        Assert.Single(normal.Meshes);
    }

    /// <summary>
    ///     Validates that cancellation keeps the events recorded before it (here the warning of the first instance)
    ///     in the sink, so the caller can still deliver them.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_Canceled_KeepsEarlierEventsInSink()
    {
        // Arrange: the sink already holds the geometry warning of an invalid cube when the build is canceled
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        using var cts = new CancellationTokenSource();
        var evaluation = new ScadEvaluationSettings();
        var output = ScadEvaluatorTestSupport.Run("cube(0);\nfor (i = [1:70]) translate([i, 0, 0]) cube(1);", evaluation, null, sink);
        cts.Cancel();

        // Act
        Assert.Throws<OperationCanceledException>(
            () => ScadGeometryBuilder.Build(output.Roots, Settings(cancellation: cts.Token), sink));

        // Assert: the show-only pre-pass visits about 140 instances (under the 256 of one poll interval) and the build
        // continues the count, so the first poll falls in the build, after cube(0) already warned
        Assert.Contains(sink.Diagnostics, d => d.Message.Contains("size must be positive", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the <c>!</c> pre-pass polls cancellation on the same fixed schedule as the build: a canceled
    ///     token stops the walk over a large model before the marked subtree is found and built (the marked subtree
    ///     here is an unsupported module, which would never poll by itself).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_AlreadyCanceled_StopsTheShowOnlyPrePass()
    {
        // Arrange: 1,000 unmarked instances, then a marked module that is not built
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var output = ScadEvaluatorTestSupport.Run(
            "for (i = [1:1000]) translate([i, 0, 0]) cube(1);\n!surface(\"x\");",
            new ScadEvaluationSettings(),
            null,
            sink);

        // Act
        Assert.Throws<OperationCanceledException>(
            () => ScadGeometryBuilder.Build(output.Roots, Settings(cancellation: cts.Token), sink));

        // Assert: nothing was built or reported, so the stop came from the pre-pass
        Assert.Empty(sink.UnsupportedFeatures);
    }

    /// <summary>
    ///     Validates that an uncanceled token lets the same model through the pre-pass: only the marked module is
    ///     built (and reported as unsupported).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_NotCanceled_ShowOnlyPrePassCompletes()
    {
        // Arrange / Act
        var result = Build("for (i = [1:1000]) translate([i, 0, 0]) cube(1);\n!surface(\"x\");");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Contains("scad-module-surface", result.Features);
    }
}
