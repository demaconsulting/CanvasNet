using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of user functions, closures and call binding in <see cref="ScadEvaluator"/>.
/// </summary>
public class ScadEvaluatorFunctionTests
{
    /// <summary>
    ///     Validates simple, mutual and doubly recursive functions.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="expected">The expected echo text.</param>
    [Theory]
    [InlineData("function fact(n) = n <= 1 ? 1 : n * fact(n - 1); echo(fact(10));", "3.6288e+06")]
    [InlineData("function fib(n) = n < 2 ? n : fib(n - 1) + fib(n - 2); echo(fib(15));", "610")]
    [InlineData("function even(n) = n == 0 ? true : odd(n - 1); function odd(n) = n == 0 ? false : even(n - 1); echo(even(10), odd(7), even(7));", "true, true, false")]
    [InlineData("function sum(v, i = 0) = i >= len(v) ? 0 : v[i] + sum(v, i + 1); echo(sum([1, 2, 3, 4]));", "10")]
    public void ScadEvaluatorFunction_Recursion_ComputesExpectedResult(string source, string expected)
    {
        // Arrange / Act
        var text = EchoText(source);

        // Assert
        Assert.Equal(expected, text);
    }

    /// <summary>
    ///     Validates closures: capture of the defining scope, currying and immediately invoked literals.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="expected">The expected echo text.</param>
    [Theory]
    [InlineData("make = function(k) function(x) x + k; add3 = make(3); echo(add3(4));", "7")]
    [InlineData("echo((function(a, b) a * b)(3, 4));", "12")]
    [InlineData("echo((function(a, b = 5) a + b)(1));", "6")]
    [InlineData("fs = [function(x) x + 1, function(x) x * 2]; echo(fs[1](5), fs[0](5));", "10, 6")]
    [InlineData("function apply(f, v) = f(v); echo(apply(function(x) x * x, 6));", "36")]
    [InlineData("function compose(f, g) = function(x) f(g(x)); inc = function(x) x + 1; dbl = function(x) x * 2; echo(compose(inc, dbl)(5));", "11")]
    [InlineData("fact = function(n) n <= 1 ? 1 : n * fact(n - 1); echo(fact(5));", "120")]
    [InlineData("k = 10; f = function(x) x + k; k2 = f(1); echo(k2);", "11")]
    public void ScadEvaluatorFunction_Closures_BehaveLexically(string source, string expected)
    {
        // Arrange / Act
        var output = Run(source);

        // Assert
        Assert.Equal($"ECHO: {expected}", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates argument binding: positional, named, defaults that see earlier parameters, and missing values.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_ArgumentBinding_FollowsDocumentedRules()
    {
        // Arrange
        const string Source = """
            function f(a, b = 10, c = a + b) = [a, b, c];
            echo(f(1));
            echo(f(1, c = 0));
            echo(f(b = 2, a = 5));
            function g(a, b) = [a, b];
            echo(g(1));
            echo(g(b = 4));
            """;

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(
            ["ECHO: [1, 10, 11]", "ECHO: [1, 10, 0]", "ECHO: [5, 2, 7]", "ECHO: [1, undef]", "ECHO: [undef, 4]"],
            output.Echo);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that a default sees only earlier parameters; a later one falls through to the definition scope.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_Default_SeesOnlyEarlierParameters()
    {
        // Arrange
        const string Source = "b = 100;\nfunction f(a = b, b = 2) = [a, b];\necho(f());";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("[100, 2]", text);
    }

    /// <summary>
    ///     Validates the warnings for extra and unknown arguments, which are ignored.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_ExtraAndUnknownArguments_WarnAndAreIgnored()
    {
        // Arrange
        const string Source = "function f(a) = a;\necho(f(1, 2));\necho(f(1, z = 3));";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 1", "ECHO: 1"], output.Echo);
        Assert.Equal(
            ["Ignoring extra argument for function 'f'", "Ignoring unknown argument 'z' for function 'f'"],
            output.Diagnostics.Select(d => d.Message));
    }

    /// <summary>
    ///     Validates that a named argument overrides a positional binding of the same parameter.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_NamedArgument_OverridesPositional()
    {
        // Arrange
        const string Source = "function f(a, b) = [a, b];\necho(f(1, 2, a = 9));";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("[9, 2]", text);
    }

    /// <summary>
    ///     Validates the expression forms of <c>let</c>, <c>echo</c> and <c>assert</c> in a function body.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_BodyForms_LetEchoAssert_Compose()
    {
        // Arrange
        const string Source = """
            function f(x) = assert(x > 0, "positive") let(y = x * 2) echo("y", y) y + 1;
            echo(f(2));
            """;

        // Act
        var echo = Echo(Source);

        // Assert
        Assert.Equal(["ECHO: \"y\", 4", "ECHO: 5"], echo);
    }

    /// <summary>
    ///     Validates that a failing <c>assert</c> expression aborts with the message and the assert's location.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_AssertExpression_Fails_WithMessage()
    {
        // Arrange
        const string Source = "function f(x) = assert(x > 0, \"must be positive\") x;\necho(f(-1));";

        // Act
        var ex = Fails(Source);

        // Assert
        Assert.Equal("1:17: Assertion failed: must be positive", ex.Message);
        Assert.Equal(1, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that a user function shadows a built-in of the same name, and a variable holding a function does too.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_UserDefinitions_ShadowBuiltins()
    {
        // Arrange
        const string Source = "function len(x) = 99;\nmax = function(a, b) a;\necho(len([1]), max(1, 2), min(4, 3));";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("99, 1, 3", text);
    }

    /// <summary>
    ///     Validates the warnings for calling something that is not a function.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_CallingNonFunction_WarnsAndYieldsUndef()
    {
        // Arrange
        const string Source = "v = 3;\necho(v(2), (3)(2), nothing(1));";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: undef, undef, undef", Assert.Single(output.Echo));
        Assert.Equal(
            ["Ignoring unknown function 'v'", "Cannot call a value that is not a function", "Ignoring unknown function 'nothing'"],
            output.Diagnostics.Select(d => d.Message));
    }

    /// <summary>
    ///     Validates that a function sees a variable defined after it in the same scope (lazy cells).
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_FileVariable_DefinedLater_IsReadable()
    {
        // Arrange
        const string Source = "function f() = later * 2;\nlater = 21;\necho(f());";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("42", text);
    }

    /// <summary>
    ///     Validates that a function value can be stored in a vector and called through an index expression.
    /// </summary>
    [Fact]
    public void ScadEvaluatorFunction_FunctionValue_PrintsParameterNames()
    {
        // Arrange
        const string Source = "echo(function(a, b = 2) a, [function() 1]);";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("function(a, b), [function()]", text);
    }
}
