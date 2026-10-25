using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of the entry points, settings and small support types of the evaluator unit.
/// </summary>
public class ScadEvaluatorEntryPointTests
{
    /// <summary>
    ///     Validates the argument checks of the document entry point.
    /// </summary>
    [Fact]
    public void ScadEvaluatorEntryPoint_Evaluate_NullArguments_Throw()
    {
        // Arrange
        var document = ScadDocument.Parse("cube(1);");
        var settings = new ScadEvaluationSettings();
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ScadEvaluator.Evaluate((ScadDocument)null!, settings, sink));
        Assert.Throws<ArgumentNullException>(() => ScadEvaluator.Evaluate(document, null!, sink));
        Assert.Throws<ArgumentNullException>(() => ScadEvaluator.Evaluate(document, settings, null!));
    }

    /// <summary>
    ///     Validates that an already resolved unit can be evaluated directly.
    /// </summary>
    [Fact]
    public void ScadEvaluatorEntryPoint_Evaluate_ResolvedUnit_ProducesInstances()
    {
        // Arrange
        var program = ScadParser.Parse("cube(2); echo(\"unit\");", "unit.scad");
        var unit = ScadSourceUnit.FromProgram(program, "unit.scad");

        // Act
        var output = ScadEvaluator.Evaluate(unit, new ScadEvaluationSettings(), new ScadEvaluationSink(ScadLimits.Default));

        // Assert
        Assert.Equal("cube(2)", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal("ECHO: \"unit\"", Assert.Single(output.Echo));
    }

    /// <summary>
    ///     Validates the defaults of the settings.
    /// </summary>
    [Fact]
    public void ScadEvaluatorEntryPoint_Settings_HaveSafeDefaults()
    {
        // Arrange / Act
        var settings = new ScadEvaluationSettings();

        // Assert
        Assert.Same(ScadLimits.Default, settings.Limits);
        Assert.Equal(0, settings.Time);
        Assert.Empty(settings.Overrides);
        Assert.False(settings.ThrowOnUnsupportedFeature);
        Assert.False(settings.Cancellation.IsCancellationRequested);
    }

    /// <summary>
    ///     Validates the set of recognized built-in module names.
    /// </summary>
    [Fact]
    public void ScadEvaluatorEntryPoint_BuiltinModules_RecognizeGeometryNamesOnly()
    {
        // Arrange / Act / Assert
        Assert.True(ScadBuiltinModules.IsBuiltin("cube"));
        Assert.True(ScadBuiltinModules.IsBuiltin("linear_extrude"));
        Assert.False(ScadBuiltinModules.IsBuiltin("Cube"));
        Assert.False(ScadBuiltinModules.IsBuiltin("children"));
        Assert.False(ScadBuiltinModules.IsBuiltin("for"));
        Assert.Equal(28, ScadBuiltinModules.Names.Count);
    }

    /// <summary>
    ///     Validates the limits hooks: the output reports the steps, loop iterations and instances it charged.
    /// </summary>
    [Fact]
    public void ScadEvaluatorEntryPoint_Output_ReportsAccounting()
    {
        // Arrange / Act
        var output = Run("for (i = [1 : 4]) cube(i);");

        // Assert
        Assert.Equal(4, output.LoopIterations);
        Assert.Equal(5, output.InstanceCount);
        Assert.True(output.Steps > 4);
        Assert.Empty(output.UnsupportedFeatures);
    }

    /// <summary>
    ///     Validates that the output lists are snapshots: later use of the sink does not change them.
    /// </summary>
    [Fact]
    public void ScadEvaluatorEntryPoint_Output_IsASnapshotOfTheSink()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var output = Run("echo(1);", null, null, sink);

        // Act
        sink.Reset();

        // Assert
        Assert.Equal("ECHO: 1", Assert.Single(output.Echo));
    }

    /// <summary>
    ///     Validates that an include fails when no base directory, library path or resolver is available.
    /// </summary>
    [Fact]
    public void ScadEvaluatorEntryPoint_Evaluate_IncludeWithoutAnySource_IsAnEvaluationError()
    {
        // Arrange / Act
        var ex = Fails("include <anything.scad>\ncube(1);");

        // Assert
        Assert.Contains("no base directory, library path or file resolver", ex.Message, StringComparison.Ordinal);
    }
}
