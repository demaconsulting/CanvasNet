using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for the mathematical functions of <see cref="ScadBuiltinFunctions"/>.
/// </summary>
public class ScadBuiltinMathTests
{
    /// <summary>
    ///     Validates the one-argument numeric functions at ordinary and special inputs.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="input">The argument.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("abs", -3.5, 3.5)]
    [InlineData("abs", 2, 2)]
    [InlineData("sign", -7, -1)]
    [InlineData("sign", 0, 0)]
    [InlineData("sign", 0.001, 1)]
    [InlineData("floor", -1.5, -2)]
    [InlineData("floor", 1.9, 1)]
    [InlineData("ceil", 1.1, 2)]
    [InlineData("ceil", -1.9, -1)]
    [InlineData("round", 2.5, 3)]
    [InlineData("round", -2.5, -3)]
    [InlineData("round", 2.4, 2)]
    [InlineData("round", -0.4, 0)]
    [InlineData("sqrt", 9, 3)]
    [InlineData("sqrt", 2, 1.4142135623730951)]
    [InlineData("exp", 0, 1)]
    [InlineData("exp", 1, 2.718281828459045)]
    [InlineData("ln", 1, 0)]
    [InlineData("ln", 2.718281828459045, 1)]
    [InlineData("log", 100, 2)]
    [InlineData("log", 1000, 3)]
    [InlineData("log", 0.1, -1)]
    public void ScadBuiltinMath_Unary_ReturnsExpected(string name, double input, double expected)
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call(name, N(input));

        // Assert
        Assert.Equal(expected, result.AsNumber, 9);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that the trigonometric functions are exact at multiples of 90 degrees.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="degrees">The angle.</param>
    /// <param name="expected">The exact expected result.</param>
    [Theory]
    [InlineData("sin", 0, 0)]
    [InlineData("sin", 90, 1)]
    [InlineData("sin", 180, 0)]
    [InlineData("sin", 270, -1)]
    [InlineData("sin", 360, 0)]
    [InlineData("sin", -90, -1)]
    [InlineData("sin", -270, 1)]
    [InlineData("sin", 450, 1)]
    [InlineData("sin", 36000, 0)]
    [InlineData("cos", 0, 1)]
    [InlineData("cos", 90, 0)]
    [InlineData("cos", 180, -1)]
    [InlineData("cos", 270, 0)]
    [InlineData("cos", 360, 1)]
    [InlineData("cos", -180, -1)]
    [InlineData("cos", 720, 1)]
    [InlineData("tan", 0, 0)]
    [InlineData("tan", 180, 0)]
    [InlineData("tan", 360, 0)]
    public void ScadBuiltinMath_Trigonometry_IsExactAtRightAngles(string name, double degrees, double expected)
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call(name, N(degrees));

        // Assert: exact equality, not a tolerance
        Assert.Equal(expected, result.AsNumber);
    }

    /// <summary>
    ///     Validates the infinite tangent at 90 and 270 degrees.
    /// </summary>
    [Fact]
    public void ScadBuiltinMath_Tan_AtOddRightAngles_IsInfinite()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(double.PositiveInfinity, host.Call("tan", N(90)).AsNumber);
        Assert.Equal(double.NegativeInfinity, host.Call("tan", N(270)).AsNumber);
        Assert.Equal(double.PositiveInfinity, host.Call("tan", N(-270)).AsNumber);
    }

    /// <summary>
    ///     Validates the trigonometric functions at other angles to six significant digits.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="degrees">The angle.</param>
    /// <param name="expectedText">The expected text.</param>
    [Theory]
    [InlineData("sin", 30, "0.5")]
    [InlineData("sin", 45, "0.707107")]
    [InlineData("cos", 60, "0.5")]
    [InlineData("cos", 45, "0.707107")]
    [InlineData("tan", 45, "1")]
    [InlineData("sin", -30, "-0.5")]
    [InlineData("tan", -45, "-1")]
    [InlineData("sin", 390, "0.5")]
    public void ScadBuiltinMath_Trigonometry_OtherAngles_MatchesToSixDigits(string name, double degrees, string expectedText)
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call(name, N(degrees));

        // Assert
        Assert.Equal(expectedText, result.ToStr());
    }

    /// <summary>
    ///     Validates the inverse trigonometric functions, which return degrees.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="input">The argument.</param>
    /// <param name="expected">The expected angle in degrees.</param>
    [Theory]
    [InlineData("asin", 0, 0)]
    [InlineData("asin", 1, 90)]
    [InlineData("asin", -1, -90)]
    [InlineData("asin", 0.5, 30)]
    [InlineData("acos", 1, 0)]
    [InlineData("acos", 0, 90)]
    [InlineData("acos", -1, 180)]
    [InlineData("acos", 0.5, 60)]
    [InlineData("atan", 0, 0)]
    [InlineData("atan", 1, 45)]
    [InlineData("atan", -1, -45)]
    [InlineData("atan", double.PositiveInfinity, 90)]
    public void ScadBuiltinMath_InverseTrigonometry_ReturnsDegrees(string name, double input, double expected)
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call(name, N(input));

        // Assert
        Assert.Equal(expected, result.AsNumber, 9);
    }

    /// <summary>
    ///     Validates atan2 in every quadrant and its named parameters.
    /// </summary>
    [Fact]
    public void ScadBuiltinMath_Atan2_CoversQuadrantsAndNamedArguments()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(0, host.Call("atan2", N(0), N(1)).AsNumber, 9);
        Assert.Equal(90, host.Call("atan2", N(1), N(0)).AsNumber, 9);
        Assert.Equal(180, host.Call("atan2", N(0), N(-1)).AsNumber, 9);
        Assert.Equal(-90, host.Call("atan2", N(-1), N(0)).AsNumber, 9);
        Assert.Equal(45, host.Call("atan2", N(1), N(1)).AsNumber, 9);
        Assert.Equal(135, host.Call("atan2", N(1), N(-1)).AsNumber, 9);
        Assert.Equal(-135, host.Call("atan2", N(-1), N(-1)).AsNumber, 9);
        Assert.Equal(-45, host.Call("atan2", N(-1), N(1)).AsNumber, 9);
        Assert.Equal(90, host.CallNamed("atan2", ("x", N(0)), ("y", N(5))).AsNumber, 9);
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates pow, including named arguments.
    /// </summary>
    [Fact]
    public void ScadBuiltinMath_Pow_ComputesPowers()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(8, host.Call("pow", N(2), N(3)).AsNumber);
        Assert.Equal(0.5, host.Call("pow", N(2), N(-1)).AsNumber);
        Assert.Equal(1024, host.CallNamed("pow", ("exponent", N(10)), ("base", N(2))).AsNumber);
        Assert.Equal(3, host.Call("pow", N(9), N(0.5)).AsNumber, 12);
    }

    /// <summary>
    ///     Validates the special numeric results: square root of a negative number and logarithm of zero.
    /// </summary>
    [Fact]
    public void ScadBuiltinMath_SpecialValues_FollowIeee()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.True(double.IsNaN(host.Call("sqrt", N(-1)).AsNumber));
        Assert.Equal(double.NegativeInfinity, host.Call("ln", N(0)).AsNumber);
        Assert.True(double.IsNaN(host.Call("log", N(-1)).AsNumber));
        Assert.True(double.IsNaN(host.Call("sin", N(double.PositiveInfinity)).AsNumber));
        Assert.True(double.IsNaN(host.Call("cos", N(double.NaN)).AsNumber));
        Assert.True(double.IsNaN(host.Call("sign", N(double.NaN)).AsNumber));
        Assert.True(double.IsNaN(host.Call("asin", N(2)).AsNumber));
        Assert.Empty(host.Warnings);
    }

    /// <summary>
    ///     Validates that every scalar function warns and yields undef for a non-number.
    /// </summary>
    /// <param name="name">The function name.</param>
    [Theory]
    [InlineData("abs")]
    [InlineData("sign")]
    [InlineData("sin")]
    [InlineData("cos")]
    [InlineData("tan")]
    [InlineData("asin")]
    [InlineData("acos")]
    [InlineData("atan")]
    [InlineData("floor")]
    [InlineData("ceil")]
    [InlineData("round")]
    [InlineData("ln")]
    [InlineData("log")]
    [InlineData("sqrt")]
    [InlineData("exp")]
    public void ScadBuiltinMath_Unary_WrongType_ReturnsUndefWithWarning(string name)
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var result = host.Call(name, S("x"));

        // Assert
        Assert.Equal(ScadValue.Undef, result);
        Assert.Equal([$"Function '{name}' expects a number, got a string"], host.Warnings);
    }

    /// <summary>
    ///     Validates that two-argument functions warn for a bad first or second argument.
    /// </summary>
    [Fact]
    public void ScadBuiltinMath_Binary_WrongType_ReturnsUndefWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var first = host.Call("pow", S("a"), N(1));
        var second = host.Call("atan2", N(1), ScadValue.Undef);

        // Assert
        Assert.Equal(ScadValue.Undef, first);
        Assert.Equal(ScadValue.Undef, second);
        Assert.Equal(
            ["Function 'pow' expects a number, got a string", "Function 'atan2' expects a number, got undef"],
            host.Warnings);
    }

    /// <summary>
    ///     Validates min and max with several numbers and with one vector.
    /// </summary>
    [Fact]
    public void ScadBuiltinMath_MinMax_AcceptNumbersOrOneVector()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act / Assert
        Assert.Equal(1, host.Call("min", N(3), N(1), N(2)).AsNumber);
        Assert.Equal(3, host.Call("max", N(3), N(1), N(2)).AsNumber);
        Assert.Equal(-5, host.Call("min", Nums(4, -5, 0)).AsNumber);
        Assert.Equal(4, host.Call("max", Nums(4, -5, 0)).AsNumber);
        Assert.Equal(7, host.Call("min", N(7)).AsNumber);
        Assert.Equal(7, host.Call("max", Nums(7)).AsNumber);
        Assert.True(double.IsNaN(host.Call("max", N(1), N(double.NaN), N(2)).AsNumber));
        Assert.Empty(host.Warnings);
        Assert.True(host.Steps > 0);
    }

    /// <summary>
    ///     Validates that min and max warn for an empty vector, mixed kinds and nested vectors.
    /// </summary>
    [Fact]
    public void ScadBuiltinMath_MinMax_InvalidOperands_ReturnUndefWithWarning()
    {
        // Arrange
        var host = new ScadTestBuiltinHost();

        // Act
        var empty = host.Call("min", Vec());
        var mixed = host.Call("max", N(1), S("a"));
        var nested = host.Call("max", Vec(Nums(1)));
        var notNumbers = host.Call("min", Vec(N(1), S("a")));

        // Assert
        Assert.Equal(ScadValue.Undef, empty);
        Assert.Equal(ScadValue.Undef, mixed);
        Assert.Equal(ScadValue.Undef, nested);
        Assert.Equal(ScadValue.Undef, notNumbers);
        Assert.Equal(4, host.Warnings.Count);
    }
}
