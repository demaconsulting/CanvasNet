using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

// cspell:ignore fontmetrics nosuchfunction textmetrics

/// <summary>
///     Unit tests for the registry, argument binding, type tests and version functions of <see cref="ScadBuiltinFunctions"/>.
/// </summary>
public class ScadBuiltinRegistryTests
{
    /// <summary>The names of all implemented functions.</summary>
    private static readonly string[] ImplementedNames =
    [
        "abs", "sign", "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "floor", "ceil", "round", "ln", "log", "pow", "sqrt",
        "exp", "min", "max", "norm", "cross", "len", "concat", "lookup", "str", "chr", "ord", "search", "rands", "is_undef",
        "is_num", "is_bool", "is_string", "is_list", "is_function", "version", "version_num",
    ];

    /// <summary>
    ///     Validates that every documented function is known.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_IsKnown_ImplementedAndUnsupportedNames_AreKnown()
    {
        // Arrange / Act / Assert
        foreach (var name in ImplementedNames)
        {
            Assert.True(ScadBuiltinFunctions.IsKnown(name), name);
        }

        Assert.True(ScadBuiltinFunctions.IsKnown("parent_module"));
        Assert.True(ScadBuiltinFunctions.IsKnown("textmetrics"));
        Assert.True(ScadBuiltinFunctions.IsKnown("fontmetrics"));
    }

    /// <summary>
    ///     Validates that unknown and differently cased names are not built-ins and are not handled.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_UnknownName_IsNotHandled()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var handled = ScadBuiltinFunctions.TryInvoke("nosuchfunction", [], host, out var result);
        var cased = ScadBuiltinFunctions.TryInvoke("SIN", [(null, N(1))], host, out _);

        // Assert
        Assert.False(handled);
        Assert.False(cased);
        Assert.Equal(ScadValue.Undef, result);
        Assert.False(ScadBuiltinFunctions.IsKnown("nosuchfunction"));
        Assert.False(ScadBuiltinFunctions.IsKnown("Abs"));
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that recognized-but-unsupported functions report their feature id and yield undef.
    /// </summary>
    /// <param name="name">The function name.</param>
    [Theory]
    [InlineData("parent_module")]
    [InlineData("textmetrics")]
    [InlineData("fontmetrics")]
    public void ScadBuiltinRegistry_KnownUnsupported_ReportsFeatureAndReturnsUndef(string name)
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call(name, N(0));

        // Assert
        Assert.Equal(ScadValue.Undef, result);
        var report = Assert.Single(host.UnsupportedReports);
        Assert.Equal($"scad-function-{name}", report.Feature);
        Assert.Contains(name, report.Message, StringComparison.Ordinal);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that a named argument to a function without parameter names is ignored with a warning.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_NamedArgument_ToUnnamedFunction_IsIgnoredWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.CallNamed("abs", (null, N(-1)), ("x", N(5)));

        // Assert
        Assert.Equal(1, result.AsNumber);
        Assert.Equal(["Ignoring unknown argument 'x' of function 'abs'"], host.Warnings);
    }

    /// <summary>
    ///     Validates that a name that matches no parameter of a function with names is ignored with a warning.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_UnknownNamedArgument_IsIgnoredWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.CallNamed("pow", (null, N(2)), (null, N(3)), ("nope", N(1)));

        // Assert
        Assert.Equal(8, result.AsNumber);
        Assert.Equal(["Ignoring unknown argument 'nope' of function 'pow'"], host.Warnings);
    }

    /// <summary>
    ///     Validates that a named argument overrides the positional value of the same parameter.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_NamedArgument_OverridesPositional()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.CallNamed("pow", (null, N(2)), (null, N(3)), ("exponent", N(4)));

        // Assert
        Assert.Equal(16, result.AsNumber);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that too few arguments warn with the expected count and yield undef.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_TooFewArguments_WarnsAndReturnsUndef()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var none = host.Call("abs");
        var one = host.Call("pow", N(2));
        var range = host.Call("rands", N(0), N(1));
        var variadic = host.Call("min");

        // Assert
        Assert.All(new[] { none, one, range, variadic }, r => Assert.Equal(ScadValue.Undef, r));
        Assert.Equal(
            [
                "Function 'abs' expects 1 arguments, got 0",
                "Function 'pow' expects 2 arguments, got 1",
                "Function 'rands' expects 3 to 4 arguments, got 2",
                "Function 'min' expects at least 1 arguments, got 0",
            ],
            host.Warnings);
    }

    /// <summary>
    ///     Validates that surplus positional arguments are dropped with a warning.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_ExtraArguments_AreDroppedWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call("abs", N(-2), N(99));

        // Assert
        Assert.Equal(2, result.AsNumber);
        Assert.Equal(["Ignoring extra arguments to function 'abs'"], host.Warnings);
    }

    /// <summary>
    ///     Validates that a gap left by named arguments is undef and counts toward the arguments supplied.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_NamedArgumentGap_IsFilledWithUndef()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act: only the last parameter is given by name, so the earlier slots are undef and the call is rejected
        var result = host.CallNamed("rands", ("seed", N(1)));

        // Assert
        Assert.Equal(ScadValue.Undef, result);
        Assert.Single(host.Warnings);
    }

    /// <summary>
    ///     Validates the type tests, including that NaN is a number and a range is not a list.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_TypeTests_ClassifyValues()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();
        var function = ScadValue.FromFunction(new TestFunctionValue());

        // Act / Assert
        Assert.True(host.Call("is_undef", ScadValue.Undef).AsBoolean);
        Assert.False(host.Call("is_undef", N(0)).AsBoolean);
        Assert.True(host.Call("is_num", N(1)).AsBoolean);
        Assert.True(host.Call("is_num", N(double.NaN)).AsBoolean);
        Assert.False(host.Call("is_num", S("1")).AsBoolean);
        Assert.False(host.Call("is_num", ScadValue.True).AsBoolean);
        Assert.True(host.Call("is_bool", ScadValue.False).AsBoolean);
        Assert.False(host.Call("is_bool", N(0)).AsBoolean);
        Assert.True(host.Call("is_string", S("x")).AsBoolean);
        Assert.False(host.Call("is_string", Vec()).AsBoolean);
        Assert.True(host.Call("is_list", Vec()).AsBoolean);
        Assert.False(host.Call("is_list", S("ab")).AsBoolean);
        Assert.False(host.Call("is_list", ScadValue.FromRange(0, 1, 2)).AsBoolean);
        Assert.True(host.Call("is_function", function).AsBoolean);
        Assert.False(host.Call("is_function", N(1)).AsBoolean);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates the emulated language version.
    /// </summary>
    [Fact]
    public void ScadBuiltinRegistry_Version_ReportsEmulatedLanguageLevel()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(Nums(2021, 1, 0), host.Call("version"));
        Assert.Equal(20_210_100, host.Call("version_num").AsNumber);
    }
}
