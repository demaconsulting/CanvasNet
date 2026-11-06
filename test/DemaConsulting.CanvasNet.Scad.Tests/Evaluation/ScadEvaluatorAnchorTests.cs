using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of the anchor declaration <c>echo("anchor", "name", [x, y, z]);</c> in <see cref="ScadEvaluator"/>: the
///     instance a valid statement-form echo leaves, the echo that is still recorded, the warning of a malformed
///     declaration, and the forms that are not anchors.
/// </summary>
public class ScadEvaluatorAnchorTests
{
    /// <summary>
    ///     Validates that a valid statement-form anchor leaves an anchor instance at its position and that the echo line
    ///     is recorded exactly as for any other echo.
    /// </summary>
    [Fact]
    public void ScadEvaluatorAnchor_ValidDeclaration_LeavesAnchorInstanceAndEchoLine()
    {
        // Arrange
        const string Source = "cube(1); echo(\"anchor\", \"tip\", [1, 2, 3]); sphere(2);";

        // Act
        var output = Run(Source);

        // Assert: the anchor sits between the two solids and carries its name and vector
        Assert.Equal("cube(1);anchor<tip>([1, 2, 3]);sphere(2)", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal(ScadInstanceKind.Anchor, output.Roots[1].Kind);
        Assert.Equal("tip", output.Roots[1].Name);
        Assert.Equal(["ECHO: \"anchor\", \"tip\", [1, 2, 3]"], output.Echo);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that an anchor keeps the position and the enclosing transform of its statement, and that the
    ///     children of the echo statement stay in the same place.
    /// </summary>
    [Fact]
    public void ScadEvaluatorAnchor_InsideTransformAndWithChildren_IsKeptInOrder()
    {
        // Arrange
        const string Source = "translate([5, 0, 0]) { echo(\"anchor\", \"a\", [0, 0, 1]) cube(1); echo(\"anchor\", \"b\", [0, 0, 2]); }";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("translate([5, 0, 0]){anchor<a>([0, 0, 1]);cube(1);anchor<b>([0, 0, 2])}", tree);
    }

    /// <summary>
    ///     Validates that a malformed declaration skips the anchor with exactly one warning that starts with the stable
    ///     identifier, and that the echo line is still recorded.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("echo(\"anchor\");")]
    [InlineData("echo(\"anchor\", \"a\");")]
    [InlineData("echo(\"anchor\", \"a\", [1, 2, 3], 4);")]
    [InlineData("echo(\"anchor\", 5, [1, 2, 3]);")]
    [InlineData("echo(\"anchor\", \"\", [1, 2, 3]);")]
    [InlineData("echo(\"anchor\", \"a\", [1, 2]);")]
    [InlineData("echo(\"anchor\", \"a\", [1, 2, 3, 4]);")]
    [InlineData("echo(\"anchor\", \"a\", [1, \"2\", 3]);")]
    [InlineData("echo(\"anchor\", \"a\", 5);")]
    [InlineData("echo(\"anchor\", \"a\", [1, 2, 1 / 0]);")]
    [InlineData("echo(\"anchor\", \"a\", [1, 2, 0 / 0]);")]
    [InlineData("echo(\"anchor\", name = \"a\", [1, 2, 3]);")]
    [InlineData("echo(\"anchor\", \"a\", p = [1, 2, 3]);")]
    public void ScadEvaluatorAnchor_MalformedDeclaration_WarnsOnceAndLeavesNoInstance(string source)
    {
        // Act
        var output = Run(source);

        // Assert
        var warning = Assert.Single(output.Diagnostics);
        Assert.StartsWith("scad-anchor-invalid:", warning.Message, StringComparison.Ordinal);
        Assert.Equal(ScadDiagnosticCodes.AnchorInvalid, warning.Code);
        Assert.Equal(1, warning.Location.Line);
        Assert.Empty(output.Roots);
        Assert.Single(output.Echo);
    }

    /// <summary>
    ///     Validates that an echo that is not a statement-form anchor never becomes one: the expression form, another
    ///     tag, a computed tag that is not exactly <c>anchor</c>, and an echo inside a function.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("x = echo(\"anchor\", \"a\", [1, 2, 3]) 5;")]
    [InlineData("function f() = echo(\"anchor\", \"a\", [1, 2, 3]) 1; y = f();")]
    [InlineData("echo(\"anchors\", \"a\", [1, 2, 3]);")]
    [InlineData("echo(\"Anchor\", \"a\", [1, 2, 3]);")]
    [InlineData("echo(tag = \"anchor\", \"a\", [1, 2, 3]);")]
    [InlineData("echo(\"a\", \"anchor\", [1, 2, 3]);")]
    public void ScadEvaluatorAnchor_NotAStatementFormAnchor_IsPlainEcho(string source)
    {
        // Act
        var output = Run(source);

        // Assert
        Assert.Empty(output.Roots);
        Assert.Empty(output.Diagnostics);
        Assert.Single(output.Echo);
    }

    /// <summary>
    ///     Validates that an anchor that is not executed is not declared: the false branch of an <c>if</c> and a
    ///     statement under the <c>*</c> modifier.
    /// </summary>
    [Fact]
    public void ScadEvaluatorAnchor_NotExecuted_IsNotDeclared()
    {
        // Arrange
        const string Source = "if (false) echo(\"anchor\", \"a\", [0, 0, 0]);\n*echo(\"anchor\", \"b\", [0, 0, 0]);\nif (true) echo(\"anchor\", \"c\", [0, 0, 0]);";

        // Act
        var output = Run(Source);

        // Assert: only the executed one is declared and echoed
        Assert.Equal("anchor<c>([0, 0, 0])", ScadInstanceDumper.Dump(output.Roots));
        Assert.Single(output.Echo);
    }

    /// <summary>
    ///     Validates that duplicates are all declared, in order, and that a loop declares one per iteration, with the
    ///     name and the vector evaluated from the iteration.
    /// </summary>
    [Fact]
    public void ScadEvaluatorAnchor_DuplicatesAndLoops_AreDeclaredInOrder()
    {
        // Arrange
        const string Source = "echo(\"anchor\", \"p\", [0, 0, 0]); echo(\"anchor\", \"p\", [1, 0, 0]); for (i = [1 : 2]) echo(\"anchor\", str(\"q\", i), [0, i, 0]);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("anchor<p>([0, 0, 0]);anchor<p>([1, 0, 0]);group<for>{anchor<q1>([0, 1, 0]);anchor<q2>([0, 2, 0])}", ScadInstanceDumper.Dump(output.Roots));
    }

    /// <summary>
    ///     Validates that an anchor counts as one instance against the instance limit.
    /// </summary>
    [Fact]
    public void ScadEvaluatorAnchor_CountsAgainstTheInstanceLimit()
    {
        // Arrange
        const string Source = "for (i = [1 : 3]) echo(\"anchor\", \"a\", [i, 0, 0]);";

        // Act: the loop is one instance and every anchor one more
        var atLimit = Run(Source, With(new ScadLimits { MaxInstances = 4 }));
        var ex = Fails(Source, With(new ScadLimits { MaxInstances = 3 }));

        // Assert
        Assert.Equal(4, atLimit.InstanceCount);
        Assert.Contains("Instance limit exceeded (limit 3)", ex.Message, StringComparison.Ordinal);
    }
}
