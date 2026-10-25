using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of scoping in <see cref="DemaConsulting.CanvasNet.Scad.Evaluation.ScadEvaluator"/>: last assignment wins, position-independent
///     lazy cells, block scopes, <c>let</c> and warnings. Expected outputs are authored for this project and pinned here.
/// </summary>
public class ScadEvaluatorScopingTests
{
    /// <summary>
    ///     Validates that the last assignment in a scope wins and that the replaced one is reported at its own location.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_Reassignment_LastWinsAndWarnsAtEarlierAssignment()
    {
        // Arrange
        const string Source = "x = 1;\ny = 5;\nx = 2;\necho(x, y);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: 2, 5", Assert.Single(output.Echo));
        var warning = Assert.Single(output.Diagnostics);
        Assert.Equal("'x' is assigned more than once in this scope; the last assignment is used", warning.Message);
        Assert.Equal(1, warning.Location.Line);
    }

    /// <summary>
    ///     Validates that assignments are position independent: a use before the assignment sees its value.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_ForwardReference_SeesLaterAssignment()
    {
        // Arrange
        const string Source = "echo(later);\nlater = base * 2;\nbase = 21;";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("42", text);
    }

    /// <summary>
    ///     Validates that unreferenced assignments are still evaluated, in source order, with their side effects.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_Cells_AreForcedInSourceOrderWithDependenciesFirst()
    {
        // Arrange
        const string Source = """
            a = echo("a") b + 1;
            b = echo("b") 10;
            c = echo("c") 3;
            echo("statement");
            """;

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("\"a\"|\"b\"|\"c\"|\"statement\"", text);
    }

    /// <summary>
    ///     Validates that a cell is evaluated once and its value is reused.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_Cell_IsEvaluatedOnce()
    {
        // Arrange
        const string Source = "a = echo(\"once\") 1;\necho(a, a, a);";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("\"once\"|1, 1, 1", text);
    }

    /// <summary>
    ///     Validates that a block has its own scope: an inner assignment shadows without leaking out.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_Block_ShadowsWithoutLeaking()
    {
        // Arrange
        const string Source = "x = 1;\n{ x = 2; echo(x); }\necho(x);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 2", "ECHO: 1"], output.Echo);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that a self-referencing assignment is undef with a circular-reference warning.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_SelfReference_IsUndefWithWarning()
    {
        // Arrange
        const string Source = "a = a + 1;\necho(a);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: undef", Assert.Single(output.Echo));
        Assert.Contains(output.Diagnostics, d => d.Message == "Circular reference while evaluating 'a'");
    }

    /// <summary>
    ///     Validates that mutually referencing assignments both fall back to undef.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_MutualReference_IsUndefWithWarning()
    {
        // Arrange
        const string Source = "a = b;\nb = a;\necho(a, b);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: undef, undef", Assert.Single(output.Echo));
        Assert.Contains(output.Diagnostics, d => d.Message.StartsWith("Circular reference", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that <c>let</c> bindings are sequential: later ones see earlier ones, never the reverse.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_Let_BindsSequentially()
    {
        // Arrange
        const string Source = "x = 5;\necho(let(a = 1, b = a + 1, a = 10) [a, b]);\necho(let(y = x, x = 1) [y, x]);";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("[10, 2]|[5, 1]", text);
    }

    /// <summary>
    ///     Validates that a loop body gets a fresh scope per iteration, so assignments neither collide nor warn.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_LoopBody_HasScopePerIteration()
    {
        // Arrange
        const string Source = "for (i = [1 : 3]) { t = i * 2; echo(t); }";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 2", "ECHO: 4", "ECHO: 6"], output.Echo);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates the warning for an unknown variable and the value undef.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_UnknownVariable_IsUndefWithWarning()
    {
        // Arrange
        const string Source = "echo(nope);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: undef", Assert.Single(output.Echo));
        var warning = Assert.Single(output.Diagnostics);
        Assert.Equal("Ignoring unknown variable 'nope'", warning.Message);
        Assert.Equal(6, warning.Location.Column);
    }

    /// <summary>
    ///     Validates that functions and modules are hoisted: usable before their definition in the same scope.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_Definitions_AreHoisted()
    {
        // Arrange
        const string Source = "echo(twice(4));\nm();\nfunction twice(v) = v * 2;\nmodule m() { echo(\"module\"); }";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("8|\"module\"", text);
    }

    /// <summary>
    ///     Validates that a variable and a function of the same name coexist.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_VariableAndFunctionOfSameName_Coexist()
    {
        // Arrange
        const string Source = "f = 3;\nfunction f(x) = x * 2;\necho(f, f(4));";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("3, 8", text);
    }

    /// <summary>
    ///     Validates lexical scoping: a function sees the variables of its definition scope, not its caller's.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_Function_UsesDefinitionScopeNotCallerScope()
    {
        // Arrange
        const string Source = "x = 1;\nfunction f() = x;\nmodule m() { x = 2; echo(f(), x); }\nm();";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("1, 2", text);
    }

    /// <summary>
    ///     Validates duplicate function and module definitions: the last wins and the earlier one is reported.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_DuplicateDefinitions_LastWinsWithWarnings()
    {
        // Arrange
        const string Source = "function f() = 1;\nfunction f() = 2;\nmodule m() { echo(\"first\"); }\nmodule m() { echo(\"second\"); }\necho(f());\nm();";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 2", "ECHO: \"second\""], output.Echo);
        Assert.Equal(
            [
                "Function 'f' is defined more than once in this scope; the last definition is used",
                "Module 'm' is defined more than once in this scope; the last definition is used",
            ],
            output.Diagnostics.Select(d => d.Message));
        Assert.Equal([1, 3], output.Diagnostics.Select(d => d.Location.Line));
    }

    /// <summary>
    ///     Validates that nested module and function definitions are scoped to their enclosing body.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_NestedDefinitions_AreLocalToTheirBody()
    {
        // Arrange
        const string Source = "module outer() { function inner(v) = v + 1; module leaf() { echo(inner(1)); } leaf(); }\nouter();\necho(inner(1));";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 2", "ECHO: undef"], output.Echo);
        Assert.Equal("Ignoring unknown function 'inner'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates the built-in constant <c>PI</c> and that a user assignment may shadow it.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_Pi_IsDefinedAndCanBeShadowed()
    {
        // Arrange
        const string Source = "echo(PI);\n{ PI = 3; echo(PI); }";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("3.14159|3", text);
    }

    /// <summary>
    ///     Validates that the warning for a repeated assignment is not repeated for the same location, and that
    ///     assignments in different scopes do not collide.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_SameNameInDifferentScopes_DoesNotWarn()
    {
        // Arrange
        const string Source = "x = 1;\nmodule m() { x = 2; echo(x); }\nm();\n{ x = 3; echo(x); }";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 2", "ECHO: 3"], output.Echo);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates the position of the evaluation of an <c>if</c> branch's own assignments.
    /// </summary>
    [Fact]
    public void ScadEvaluatorScoping_IfBranch_HasOwnScope()
    {
        // Arrange
        const string Source = "x = 1;\nif (true) { x = 5; echo(x); } else { echo(\"no\"); }\necho(x);";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("5|1", text);
    }
}
