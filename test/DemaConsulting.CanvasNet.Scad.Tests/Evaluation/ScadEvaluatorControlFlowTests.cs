using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of control flow in <see cref="DemaConsulting.CanvasNet.Scad.Evaluation.ScadEvaluator"/>: <c>if</c>, <c>for</c>,
///     <c>intersection_for</c>, <c>let</c>, <c>echo</c> and <c>assert</c> statements and range iteration.
/// </summary>
public class ScadEvaluatorControlFlowTests
{
    /// <summary>
    ///     Validates <c>if</c>, <c>else</c> and <c>else if</c> selection by truthiness.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="expected">The expected echo text.</param>
    [Theory]
    [InlineData("if (1 > 2) echo(\"a\"); else echo(\"b\");", "\"b\"")]
    [InlineData("if (2 > 1) echo(\"a\"); else echo(\"b\");", "\"a\"")]
    [InlineData("if (false) echo(\"a\"); else if (true) echo(\"b\"); else echo(\"c\");", "\"b\"")]
    [InlineData("if (false) echo(\"a\"); else if (0) echo(\"b\"); else echo(\"c\");", "\"c\"")]
    [InlineData("if (undef) echo(\"a\"); else echo(\"none\");", "\"none\"")]
    [InlineData("if ([]) echo(\"a\"); else echo(\"empty\");", "\"empty\"")]
    [InlineData("if (\"x\") echo(\"yes\");", "\"yes\"")]
    [InlineData("if (false) echo(\"never\");", "")]
    public void ScadEvaluatorControlFlow_If_SelectsBranchByTruthiness(string source, string expected)
    {
        // Arrange / Act
        var text = EchoText(source);

        // Assert
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Validates which values a <c>for</c> iterator walks.
    /// </summary>
    /// <param name="iterable">The iterated expression.</param>
    /// <param name="expected">The expected echo text of the iterator values.</param>
    [Theory]
    [InlineData("[1, 2, 3]", "1|2|3")]
    [InlineData("[1 : 3]", "1|2|3")]
    [InlineData("[0 : 2 : 6]", "0|2|4|6")]
    [InlineData("[0 : 2 : 5]", "0|2|4")]
    [InlineData("[10 : -3 : 0]", "10|7|4|1")]
    [InlineData("[0 : 0.1 : 0.5]", "0|0.1|0.2|0.3|0.4|0.5")]
    [InlineData("[5 : 1]", "")]
    [InlineData("[1 : -1 : 5]", "")]
    [InlineData("[[1, 2], [3, 4]]", "[1, 2]|[3, 4]")]
    [InlineData("7", "7")]
    [InlineData("\"ab\"", "\"ab\"")]
    [InlineData("[]", "")]
    public void ScadEvaluatorControlFlow_For_IteratesExpectedValues(string iterable, string expected)
    {
        // Arrange
        var source = $"for (i = {iterable}) echo(i);";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal(expected, string.Join('|', output.Echo.Select(l => l["ECHO: ".Length..])));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that iterating undef is zero iterations with a warning, and a zero step likewise.
    /// </summary>
    /// <param name="iterable">The iterated expression.</param>
    /// <param name="message">The expected warning.</param>
    [Theory]
    [InlineData("undef", "Cannot iterate over undef")]
    [InlineData("[0 : 0 : 5]", "Ignoring a range with a zero step or a non-finite bound")]
    [InlineData("[0 : 1 : 1 / 0]", "Ignoring a range with a zero step or a non-finite bound")]
    public void ScadEvaluatorControlFlow_For_UndefOrInvalidRange_WarnsWithoutIterating(string iterable, string message)
    {
        // Arrange
        var source = $"for (i = {iterable}) echo(i);";

        // Act
        var output = Run(source);

        // Assert
        Assert.Empty(output.Echo);
        Assert.Equal(message, Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that several iterators nest with the first as the outermost loop.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_For_MultipleIterators_FirstIsOutermost()
    {
        // Arrange
        const string Source = "for (i = [1 : 2], j = [10, 20]) echo(i, j);";

        // Act
        var echo = Echo(Source);

        // Assert
        Assert.Equal(["ECHO: 1, 10", "ECHO: 1, 20", "ECHO: 2, 10", "ECHO: 2, 20"], echo);
    }

    /// <summary>
    ///     Validates that a later iterator can use an earlier one.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_For_LaterIteratorSeesEarlierOne()
    {
        // Arrange
        const string Source = "for (i = [1 : 3], j = [i : 3]) echo(i, j);";

        // Act
        var echo = Echo(Source);

        // Assert
        Assert.Equal(6, echo.Length);
        Assert.Equal("ECHO: 3, 3", echo[^1]);
    }

    /// <summary>
    ///     Validates that <c>for</c> always produces a group, even for zero iterations, holding all iterations' instances.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_For_ProducesGroupEvenWhenEmpty()
    {
        // Arrange
        const string Source = "for (i = [1 : 2]) cube(i);\nfor (i = []) cube(i);";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("group<for>{cube(1);cube(2)};group<for>", tree);
    }

    /// <summary>
    ///     Validates the shape of <c>intersection_for</c>: an <c>intersection</c> of one group per iteration.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_IntersectionFor_ProducesIntersectionOfGroups()
    {
        // Arrange
        const string Source = "intersection_for (i = [1 : 2]) { cube(i); sphere(i); }\nintersection_for (i = []) cube(1);";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal(
            "intersection(){group<intersection_for>{cube(1);sphere(1)};group<intersection_for>{cube(2);sphere(2)}};intersection()",
            tree);
    }

    /// <summary>
    ///     Validates that a <c>let</c> statement scopes its bindings over its body and splices the instances.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_LetStatement_ScopesBindingsAndSplices()
    {
        // Arrange
        const string Source = "let(a = 2, b = a * 2) { cube(a); sphere(b); }\ncube(7);";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("cube(2);sphere(4);cube(7)", tree);
    }

    /// <summary>
    ///     Validates that an <c>echo</c> statement echoes and then places its children, and an <c>assert</c> statement checks first.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_EchoAndAssertStatements_PlaceTheirChildren()
    {
        // Arrange
        const string Source = "echo(\"hi\") cube(1);\nassert(true, \"fine\") sphere(2);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: \"hi\"", Assert.Single(output.Echo));
        Assert.Equal("cube(1);sphere(2)", ScadInstanceDumper.Dump(output.Roots));
    }

    /// <summary>
    ///     Validates the failure of an <c>assert</c> statement, with and without a message, including named arguments.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="expected">The expected exception message after the location.</param>
    [Theory]
    [InlineData("assert(false);", "Assertion failed")]
    [InlineData("assert(1 > 2, \"one is not greater\");", "Assertion failed: one is not greater")]
    [InlineData("assert(condition = 0, message = \"named\");", "Assertion failed: named")]
    [InlineData("assert(undef);", "Assertion failed")]
    [InlineData("assert(false, 42);", "Assertion failed: 42")]
    public void ScadEvaluatorControlFlow_AssertStatement_FailureAbortsWithMessage(string statement, string expected)
    {
        // Arrange / Act
        var ex = Fails(statement);

        // Assert
        Assert.Equal($"1:1: {expected}", ex.Message);
    }

    /// <summary>
    ///     Validates that a passing assertion has no effect.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_AssertStatement_PassingHasNoEffect()
    {
        // Arrange / Act
        var output = Run("assert(true); assert(1, \"ok\"); cube(1);");

        // Assert
        Assert.Equal("cube(1)", ScadInstanceDumper.Dump(output.Roots));
        Assert.Empty(output.Echo);
    }

    /// <summary>
    ///     Validates that an <c>assert</c> without a condition fails, in statement and expression form, with a clear
    ///     message and the location of the assert.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="expected">The expected exception message.</param>
    [Theory]
    [InlineData("assert();", "1:1: Assertion failed: the condition argument is missing")]
    [InlineData("assert() cube(1);", "1:1: Assertion failed: the condition argument is missing")]
    [InlineData("assert(message = \"no condition\");", "1:1: Assertion failed: no condition")]
    [InlineData("\n x = assert() 1;", "2:6: Assertion failed: the condition argument is missing")]
    [InlineData("function f() = assert() 1;\necho(f());", "1:16: Assertion failed: the condition argument is missing")]
    public void ScadEvaluatorControlFlow_AssertWithoutCondition_Fails(string source, string expected)
    {
        // Arrange / Act
        var ex = Fails(source);

        // Assert
        Assert.Equal(expected, ex.Message);
    }

    /// <summary>
    ///     Validates that surplus positional and unknown named arguments of <c>assert</c> are ignored with a warning
    ///     (still evaluated), and do not change the outcome.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_AssertExtraAndUnknownArguments_WarnAndAreIgnored()
    {
        // Arrange
        const string Source = "function f() = echo(\"third\") 3;\nassert(true, \"ok\", f(), 4);\nassert(true, bogus = 1);\ncube(1);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("cube(1)", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal("ECHO: \"third\"", Assert.Single(output.Echo));
        Assert.Equal(
            ["2:1: Ignoring extra arguments to assert", "3:1: Ignoring unknown argument 'bogus' of assert"],
            output.Diagnostics.Select(d => $"{d.Location}: {d.Message}"));
    }

    /// <summary>
    ///     Validates that surplus arguments do not mask a failing condition, and that the message is still the
    ///     second positional argument.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_AssertExtraArguments_StillFailOnFalseCondition()
    {
        // Arrange / Act
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        var ex = Assert.Throws<ScadEvaluationException>(() => Run("assert(false, \"msg\", 3);", null, null, sink));

        // Assert
        Assert.Equal("1:1: Assertion failed: msg", ex.Message);
        Assert.Equal("Ignoring extra arguments to assert", Assert.Single(sink.Diagnostics).Message);
    }
    /// <summary>
    ///     Validates that a loop variable starting with <c>$</c> becomes a dynamic special variable for the body.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_For_SpecialLoopVariable_IsDynamicForBody()
    {
        // Arrange
        const string Source = "module m() { echo($detail); }\nfor ($detail = [8, 16]) m();";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("8|16", text);
    }

    /// <summary>
    ///     Validates that loop turns charge one loop iteration each, outer and inner levels included.
    /// </summary>
    [Fact]
    public void ScadEvaluatorControlFlow_For_ChargesLoopIterations()
    {
        // Arrange / Act
        var output = Run("for (i = [1 : 3], j = [1 : 2]) echo(i);");

        // Assert
        Assert.Equal(3 + 6, output.LoopIterations);
    }
}
