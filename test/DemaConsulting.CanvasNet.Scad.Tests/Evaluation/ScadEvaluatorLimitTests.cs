using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of every resource limit enforced by <see cref="ScadEvaluator"/>: for each, a program exactly at the
///     limit succeeds and one over it fails with the documented diagnostic.
/// </summary>
public class ScadEvaluatorLimitTests
{
    /// <summary>
    ///     Validates the recursion limit for module recursion: three frames per level plus five at the bottom.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_RecursionDepth_ModuleRecursion_AtLimitSucceeds_OneOverFails()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = 65 });
        const string Module = "module r(n) { if (n > 0) r(n - 1); }\n";

        // Act
        var atLimit = Run(Module + "r(20);", settings);
        var ex = Fails(Module + "r(21);", settings);

        // Assert
        Assert.Equal(21, atLimit.InstanceCount);
        Assert.Contains("Recursion too deep (limit 65)", ex.Message, StringComparison.Ordinal);
        Assert.True(ex.Location.Line > 0);
    }

    /// <summary>
    ///     Validates the step limit: a program that costs exactly N steps passes with limit N and fails with N - 1.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_Steps_AtLimitSucceeds_OneOverFails()
    {
        // Arrange
        const string Source = "for (i = [1 : 4]) echo(i * 2);";
        var cost = Run(Source).Steps;

        // Act
        var atLimit = Run(Source, With(new ScadLimits { MaxEvaluationSteps = cost }));
        var ex = Fails(Source, With(new ScadLimits { MaxEvaluationSteps = cost - 1 }));

        // Assert
        Assert.Equal(cost, atLimit.Steps);
        Assert.Contains($"Evaluation step limit exceeded (limit {cost - 1})", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that vector operations are charged per element, so they cannot be made cheap.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_Steps_VectorArithmetic_IsChargedPerElement()
    {
        // Arrange
        const string Setup = "v = [for (i = [0 : 999]) i];\n";
        var baseline = Run(Setup + "w = v;").Steps;

        // Act
        var scaled = Run(Setup + "w = v * 2;").Steps;

        // Assert
        Assert.True(scaled - baseline >= 1000, $"scaled={scaled} baseline={baseline}");
    }

    /// <summary>
    ///     Validates that shared nested vectors cannot make an operation cheap: the charge counts nested elements and
    ///     the check stops once the budget is exceeded.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_Steps_SharedNestedVectors_AreChargedDeeply()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxEvaluationSteps = 100_000 });
        const string Source = "a = [for (i = [0 : 999]) 1];\nb = [for (i = [0 : 999]) a];\nc = b * 2;";

        // Act
        var ex = Fails(Source, settings);

        // Assert
        Assert.Contains("Evaluation step limit exceeded (limit 100000)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a long scope walk is charged extra steps, so lookup cost is bounded by the step limit.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_Steps_LongScopeWalk_IsCharged()
    {
        // Arrange
        var opening = string.Concat(Enumerable.Range(0, 100).Select(i => $"{{ v{i} = {i};\n"));
        var closing = new string('}', 100);
        var deep = Run($"{opening}echo(v99);{closing}").Steps;

        // Act
        var far = Run($"{opening}echo(v0);{closing}").Steps;

        // Assert
        Assert.True(far - deep >= 2, $"far={far} deep={deep}");
    }

    /// <summary>
    ///     Validates the loop iteration limit and that a count over the budget is rejected before looping.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_LoopIterations_AtLimitSucceeds_OneOverFails()
    {
        // Arrange
        const string Source = "for (i = [1 : 5]) echo(i);";

        // Act
        var atLimit = Run(Source, With(new ScadLimits { MaxLoopIterations = 5 }));
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var ex = Assert.Throws<ScadEvaluationException>(() => Run(Source, With(new ScadLimits { MaxLoopIterations = 4 }), null, sink));

        // Assert
        Assert.Equal(5, atLimit.LoopIterations);
        Assert.Contains("Loop iteration limit exceeded (limit 4)", ex.Message, StringComparison.Ordinal);
        Assert.Empty(sink.EchoLines);
    }

    /// <summary>
    ///     Validates that the loop budget is shared by the whole evaluation, nested loops included.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_LoopIterations_AreCountedAcrossNestedLoops()
    {
        // Arrange
        const string Source = "for (i = [1 : 3]) for (j = [1 : 3]) echo(i);";

        // Act
        var atLimit = Run(Source, With(new ScadLimits { MaxLoopIterations = 12 }));
        var ex = Fails(Source, With(new ScadLimits { MaxLoopIterations = 11 }));

        // Assert
        Assert.Equal(9, atLimit.Echo.Count);
        Assert.Contains("Loop iteration limit exceeded (limit 11)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a huge range is rejected immediately, not iterated.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_LoopIterations_HugeRange_FailsImmediately()
    {
        // Arrange / Act
        var ex = Fails("for (i = [0 : 1e12]) echo(i);");

        // Assert
        Assert.Contains("Loop iteration limit exceeded", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the list length limit for literals, <c>concat</c> and <c>rands</c>.
    /// </summary>
    /// <param name="atLimit">A program with a list of exactly three elements.</param>
    /// <param name="overLimit">A program with a list of four elements.</param>
    [Theory]
    [InlineData("echo([1, 2, 3]);", "echo([1, 2, 3, 4]);")]
    [InlineData("echo(concat([1, 2], [3]));", "echo(concat([1, 2], [3, 4]));")]
    [InlineData("echo(rands(0, 1, 3, 7));", "echo(rands(0, 1, 4, 7));")]
    [InlineData("echo([each [1 : 3]]);", "echo([each [1 : 4]]);")]
    public void ScadEvaluatorLimit_ListLength_AtLimitSucceeds_OneOverFails(string atLimit, string overLimit)
    {
        // Arrange
        var settings = With(new ScadLimits { MaxListLength = 3 });

        // Act
        var ok = Run(atLimit, settings);
        var ex = Fails(overLimit, settings);

        // Assert
        Assert.Single(ok.Echo);
        Assert.Contains("List too long (limit 3)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the string length limit for <c>str</c>, <c>chr</c> and echo output.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_StringLength_AtLimitSucceeds_OneOverFails()
    {
        // Arrange
        var strLimit = With(new ScadLimits { MaxStringLength = 3 });
        var echoLimit = With(new ScadLimits { MaxStringLength = 4 });

        // Act
        var strOk = Run("x = str(\"abc\");\ny = len(x);\necho(y);", strLimit);
        var strEx = Fails("x = str(\"abcd\");", strLimit);
        var echoOk = Run("echo(\"ab\");", echoLimit);
        var echoEx = Fails("echo(\"abc\");", echoLimit);

        // Assert
        Assert.Equal("ECHO: 3", Assert.Single(strOk.Echo));
        Assert.Contains("String too long (limit 3)", strEx.Message, StringComparison.Ordinal);
        Assert.Equal("ECHO: \"ab\"", Assert.Single(echoOk.Echo));
        Assert.Contains("String too long (limit 4)", echoEx.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the value nesting limit of 256 through recursion that wraps a value in a vector at every level.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_ListNesting_AtLimitSucceeds_OneOverFails()
    {
        // Arrange
        const string Function = "function nest(n) = n == 0 ? 0 : [nest(n - 1)];\n";

        // Act
        var atLimit = Run(Function + "echo(len(nest(256)));");
        var ex = Fails(Function + "echo(len(nest(257)));");

        // Assert
        Assert.Equal("ECHO: 1", Assert.Single(atLimit.Echo));
        Assert.Contains("List nesting too deep (limit 256)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the instance limit; groups count as instances.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_Instances_AtLimitSucceeds_OneOverFails()
    {
        // Arrange
        const string Source = "for (i = [1 : 3]) cube(i);";

        // Act
        var atLimit = Run(Source, With(new ScadLimits { MaxInstances = 4 }));
        var ex = Fails(Source, With(new ScadLimits { MaxInstances = 3 }));

        // Assert
        Assert.Equal(4, atLimit.InstanceCount);
        Assert.Contains("Instance limit exceeded (limit 3)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the cumulative echo output limit.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_OutputLength_AtLimitSucceeds_OneOverFails()
    {
        // Arrange
        const string Source = "echo(1);\necho(2);\necho(3);";
        var settings = With(new ScadLimits { MaxOutputLength = 21 });

        // Act
        var atLimit = Run(Source, settings);
        var ex = Fails(Source, With(new ScadLimits { MaxOutputLength = 20 }));

        // Assert
        Assert.Equal(3, atLimit.Echo.Count);
        Assert.Contains("Echo output limit exceeded (limit 20)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(3, ex.Location.Line);
    }

    /// <summary>
    ///     Validates the diagnostics cap: warnings beyond it collapse into one notice and evaluation continues.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_Diagnostics_AreCappedWithNotice()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxDiagnostics = 3 });

        // Act
        var output = Run("echo(a, b, c, d, e);\necho(\"still running\");", settings);

        // Assert
        Assert.Equal(2, output.Echo.Count);
        Assert.Equal(4, output.Diagnostics.Count);
        Assert.Equal("Further diagnostics suppressed (limit 3)", output.Diagnostics[3].Message);
    }

    /// <summary>
    ///     Validates that the include depth and cycle limits surface through evaluation as typed evaluation errors.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_Include_CycleAndDepth_AreEvaluationErrors()
    {
        // Arrange
        var files = new ScadVirtualFiles(("a.scad", "include <b.scad>\n"), ("b.scad", "include <a.scad>\n"));
        var chain = new ScadVirtualFiles(("c1.scad", "include <c2.scad>\n"), ("c2.scad", "include <c3.scad>\n"), ("c3.scad", "x = 1;\n"));

        // Act
        var cycle = Assert.Throws<ScadEvaluationException>(() => Run("include <a.scad>", null, files));
        var depth = Assert.Throws<ScadEvaluationException>(() => Run("include <c1.scad>", With(new ScadLimits { MaxIncludeDepth = 2 }), chain));

        // Assert
        Assert.Contains("Include cycle", cycle.Message, StringComparison.Ordinal);
        Assert.Contains("Include nesting too deep (limit 2)", depth.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a failed evaluation leaves the buffered events in the sink for the caller to deliver.
    /// </summary>
    [Fact]
    public void ScadEvaluatorLimit_FailedEvaluation_KeepsEventsRecordedBeforeTheFailure()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        Assert.Throws<ScadEvaluationException>(() => Run("echo(\"before\");\necho(missing);\nassert(false);", null, null, sink));

        // Assert
        Assert.Equal(["ECHO: \"before\"", "ECHO: undef"], sink.EchoLines);
        Assert.Single(sink.Diagnostics);
    }
}
