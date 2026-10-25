using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

// cspell:ignore textmetrics fontmetrics

/// <summary>
///     Tests of warnings and unsupported-feature reports produced by <see cref="ScadEvaluator"/>: their text, location,
///     de-duplication, and how evaluation continues.
/// </summary>
public class ScadEvaluatorDiagnosticsTests
{
    /// <summary>
    ///     Validates the text and location of each warning kind.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="expected">The expected warning as <c>line:column: message</c>.</param>
    [Theory]
    [InlineData("echo(nope);", "1:6: Ignoring unknown variable 'nope'")]
    [InlineData("echo(nope(1));", "1:6: Ignoring unknown function 'nope'")]
    [InlineData("nope(1);", "1:1: Ignoring unknown module 'nope'")]
    [InlineData("x = 1;\nx = 2;", "1:1: 'x' is assigned more than once in this scope; the last assignment is used")]
    [InlineData("a = a;", "1:5: Circular reference while evaluating 'a'")]
    [InlineData("echo(1 + \"a\");", "1:8: Cannot apply operator '+' to a number and a string")]
    [InlineData("echo(-\"a\");", "1:6: Cannot apply operator '-' to a string")]
    [InlineData("echo((3)(1));", "1:9: Cannot call a value that is not a function")]
    [InlineData("function f() = 1;\necho(f(1));", "2:7: Ignoring extra argument for function 'f'")]
    [InlineData("function f() = 1;\necho(f(z = 1));", "2:7: Ignoring unknown argument 'z' for function 'f'")]
    [InlineData("children();", "1:1: children() used outside a module")]
    [InlineData("for (i = undef) cube(1);", "1:6: Cannot iterate over undef")]
    [InlineData("for (i = [0 : 0 : 3]) cube(1);", "1:6: Ignoring a range with a zero step or a non-finite bound")]
    [InlineData("echo([0 : undef]);", "1:6: Range bounds and step must be numbers")]
    [InlineData("echo(sqrt(\"x\"));", "1:10: Function 'sqrt' expects a number, got a string")]
    public void ScadEvaluatorDiagnostics_Warning_HasExpectedTextAndLocation(string source, string expected)
    {
        // Arrange / Act
        var warnings = Warnings(source);

        // Assert
        Assert.Equal(expected, warnings[0]);
    }

    /// <summary>
    ///     Validates that a warning at one location is reported once however often it is triggered.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDiagnostics_RepeatedWarningAtOneLocation_IsReportedOnce()
    {
        // Arrange
        const string Source = "for (i = [1 : 50]) echo(nope);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(50, output.Echo.Count);
        Assert.Single(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that the same problem at different locations is reported at each.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDiagnostics_SameProblemAtTwoLocations_IsReportedTwice()
    {
        // Arrange / Act
        var warnings = Warnings("echo(nope);\necho(nope);");

        // Assert
        Assert.Equal(["1:6: Ignoring unknown variable 'nope'", "2:6: Ignoring unknown variable 'nope'"], warnings);
    }

    /// <summary>
    ///     Validates that the file name of the document appears in warning locations.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDiagnostics_Location_CarriesTheSourceName()
    {
        // Arrange
        var files = new ScadVirtualFiles();

        // Act
        var output = Run("echo(nope);", null, files);

        // Assert
        Assert.Equal("main.scad", Assert.Single(output.Diagnostics).Location.File);
    }

    /// <summary>
    ///     Validates that a recognized but unsupported function is reported as an unsupported feature, yields undef and
    ///     evaluation continues.
    /// </summary>
    /// <param name="call">The call expression.</param>
    /// <param name="feature">The expected feature identifier.</param>
    [Theory]
    [InlineData("parent_module(0)", "scad-function-parent_module")]
    [InlineData("textmetrics(\"x\")", "scad-function-textmetrics")]
    [InlineData("fontmetrics()", "scad-function-fontmetrics")]
    [InlineData("search(true, [1])", "scad-search-form")]
    public void ScadEvaluatorDiagnostics_UnsupportedFunction_IsReportedAndEvaluationContinues(string call, string feature)
    {
        // Arrange
        var source = $"echo({call});\necho(\"after\");";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal(["ECHO: undef", "ECHO: \"after\""], output.Echo);
        var report = Assert.Single(output.UnsupportedFeatures);
        Assert.Equal(feature, report.Feature);
        Assert.Equal(1, report.Location.Line);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that unsupported-feature reports are de-duplicated by feature and location.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDiagnostics_UnsupportedFeature_IsDeduplicatedByLocation()
    {
        // Arrange
        const string Source = "for (i = [1 : 5]) echo(parent_module(i));\necho(parent_module(0));";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(2, output.UnsupportedFeatures.Count);
        Assert.Equal([1, 2], output.UnsupportedFeatures.Select(f => f.Location.Line));
    }

    /// <summary>
    ///     Validates that, when asked to throw, the first unsupported feature aborts with its identifier and location.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDiagnostics_ThrowOnUnsupportedFeature_AbortsWithFeatureAndLocation()
    {
        // Arrange
        var settings = new ScadEvaluationSettings { ThrowOnUnsupportedFeature = true };
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => Run("echo(\"before\");\necho(parent_module(1));\necho(\"after\");", settings, null, sink));

        // Assert
        Assert.Equal("scad-function-parent_module", ex.Feature);
        Assert.Equal(2, ex.Location.Line);
        Assert.Equal(["ECHO: \"before\""], sink.EchoLines);
        Assert.Single(sink.UnsupportedFeatures);
    }

    /// <summary>
    ///     Validates that assertion failures carry the location of the assert and the optional message.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDiagnostics_AssertFailure_HasLocationAndMessage()
    {
        // Arrange / Act
        var ex = Fails("\n\n  assert(1 == 2, \"math is broken\");");

        // Assert
        Assert.Equal("3:3: Assertion failed: math is broken", ex.Message);
        Assert.Equal((3, 3), (ex.Location.Line, ex.Location.Column));
    }

    /// <summary>
    ///     Validates that limit errors are located at the offending construct.
    /// </summary>
    [Fact]
    public void ScadEvaluatorDiagnostics_LimitError_IsLocatedAtOffendingConstruct()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxInstances = 1 });

        // Act
        var ex = Fails("cube(1);\n  sphere(2);", settings);

        // Assert
        Assert.Equal((2, 3), (ex.Location.Line, ex.Location.Column));
    }
}
