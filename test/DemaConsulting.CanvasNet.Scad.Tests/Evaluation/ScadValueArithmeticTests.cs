using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="ScadValue"/> operators, indexing and member access.
/// </summary>
public class ScadValueArithmeticTests
{
    /// <summary>
    ///     Applies a binary operator and returns the result together with the error flag.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The result and the type-error flag.</returns>
    private static (ScadValue Result, bool Error) Apply(ScadBinaryOperator op, ScadValue left, ScadValue right)
    {
        var result = ScadValue.Binary(op, left, right, out var error);
        return (result, error);
    }

    /// <summary>
    ///     Validates scalar arithmetic.
    /// </summary>
    /// <param name="op">The operator name.</param>
    /// <param name="left">The left number.</param>
    /// <param name="right">The right number.</param>
    /// <param name="expected">The expected result.</param>
    [Theory]
    [InlineData("Add", 2, 3, 5)]
    [InlineData("Subtract", 2, 3, -1)]
    [InlineData("Multiply", 4, 2.5, 10)]
    [InlineData("Divide", 7, 2, 3.5)]
    [InlineData("Modulo", 7, 3, 1)]
    [InlineData("Modulo", -7, 3, -1)]
    [InlineData("Modulo", 7.5, 2, 1.5)]
    [InlineData("Power", 2, 10, 1024)]
    [InlineData("Power", 9, 0.5, 3)]
    public void ScadValue_Binary_Numbers_ComputesResult(string op, double left, double right, double expected)
    {
        // Arrange / Act
        var (result, error) = Apply(Enum.Parse<ScadBinaryOperator>(op), N(left), N(right));

        // Assert
        Assert.False(error);
        Assert.Equal(expected, result.AsNumber, 12);
    }

    /// <summary>
    ///     Validates IEEE behavior of division and modulo by zero.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_DivisionByZero_FollowsIeee()
    {
        // Arrange / Act
        var positive = Apply(ScadBinaryOperator.Divide, N(1), N(0));
        var negative = Apply(ScadBinaryOperator.Divide, N(-1), N(0));
        var zero = Apply(ScadBinaryOperator.Divide, N(0), N(0));
        var modulo = Apply(ScadBinaryOperator.Modulo, N(5), N(0));

        // Assert
        Assert.Equal(double.PositiveInfinity, positive.Result.AsNumber);
        Assert.Equal(double.NegativeInfinity, negative.Result.AsNumber);
        Assert.True(double.IsNaN(zero.Result.AsNumber));
        Assert.True(double.IsNaN(modulo.Result.AsNumber));
        Assert.False(positive.Error || negative.Error || zero.Error || modulo.Error);
    }

    /// <summary>
    ///     Validates equality, inequality and the logical operators on any operands.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_EqualityAndLogic_NeverTypeError()
    {
        // Arrange / Act
        var equal = Apply(ScadBinaryOperator.Equal, S("a"), N(1));
        var notEqual = Apply(ScadBinaryOperator.NotEqual, S("a"), N(1));
        var and = Apply(ScadBinaryOperator.And, N(1), S(string.Empty));
        var or = Apply(ScadBinaryOperator.Or, ScadValue.Undef, Nums(0));

        // Assert: yields booleans without an error
        Assert.False(equal.Result.AsBoolean);
        Assert.True(notEqual.Result.AsBoolean);
        Assert.False(and.Result.AsBoolean);
        Assert.True(or.Result.AsBoolean);
        Assert.False(equal.Error || notEqual.Error || and.Error || or.Error);
    }

    /// <summary>
    ///     Validates that invalid operand combinations yield undef and set the error flag.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="name">The case name.</param>
    [Theory]
    [InlineData("Add", "number-string")]
    [InlineData("Add", "string-string")]
    [InlineData("Add", "undef-number")]
    [InlineData("Add", "number-vector")]
    [InlineData("Add", "vector-length-mismatch")]
    [InlineData("Add", "bool-number")]
    [InlineData("Subtract", "vector-number")]
    [InlineData("Multiply", "string-number")]
    [InlineData("Multiply", "dot-length-mismatch")]
    [InlineData("Multiply", "matrix-dimension-mismatch")]
    [InlineData("Multiply", "empty-vectors")]
    [InlineData("Multiply", "ragged-matrix")]
    [InlineData("Divide", "number-vector")]
    [InlineData("Divide", "vector-vector")]
    [InlineData("Modulo", "vector-number")]
    [InlineData("Power", "vector-number")]
    public void ScadValue_Binary_InvalidOperands_ReturnsUndefWithError(string op, string name)
    {
        // Arrange
        var (left, right) = name switch
        {
            "number-string" => (N(1), S("a")),
            "string-string" => (S("a"), S("b")),
            "undef-number" => (ScadValue.Undef, N(1)),
            "number-vector" => (N(1), Nums(1)),
            "vector-length-mismatch" => (Nums(1, 2), Nums(1)),
            "bool-number" => (ScadValue.True, N(1)),
            "vector-number" => (Nums(1), N(1)),
            "string-number" => (S("a"), N(2)),
            "dot-length-mismatch" => (Nums(1, 2), Nums(1, 2, 3)),
            "matrix-dimension-mismatch" => (Vec(Nums(1, 2), Nums(3, 4)), Nums(1, 2, 3)),
            "empty-vectors" => (Vec(), Vec()),
            "ragged-matrix" => (Vec(Nums(1, 2), Nums(3)), Nums(1, 2)),
            _ => (Nums(1), Nums(1)),
        };

        // Act
        var (result, error) = Apply(Enum.Parse<ScadBinaryOperator>(op), left, right);

        // Assert
        Assert.Equal(ScadValue.Undef, result);
        Assert.True(error);
    }

    /// <summary>
    ///     Validates element-wise addition and subtraction, including nested vectors.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_VectorAddSubtract_IsElementWise()
    {
        // Arrange / Act
        var sum = Apply(ScadBinaryOperator.Add, Nums(1, 2, 3), Nums(10, 20, 30));
        var difference = Apply(ScadBinaryOperator.Subtract, Vec(Nums(5, 5), Nums(1, 1)), Vec(Nums(1, 2), Nums(1, 1)));

        // Assert
        Assert.Equal(Nums(11, 22, 33), sum.Result);
        Assert.Equal(Vec(Nums(4, 3), Nums(0, 0)), difference.Result);
        Assert.False(sum.Error || difference.Error);
    }

    /// <summary>
    ///     Validates that a bad element only spoils that element but the error is still flagged.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_VectorWithBadElement_KeepsShapeAndFlagsError()
    {
        // Arrange / Act
        var (result, error) = Apply(ScadBinaryOperator.Add, Vec(N(1), S("a")), Nums(1, 1));

        // Assert
        Assert.Equal(Vec(N(2), ScadValue.Undef), result);
        Assert.True(error);
    }

    /// <summary>
    ///     Validates scaling a vector by a number from either side, recursively, and division by a number.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_ScaleAndDivide_ApplyToEveryElement()
    {
        // Arrange / Act
        var right = Apply(ScadBinaryOperator.Multiply, Nums(1, 2), N(3));
        var left = Apply(ScadBinaryOperator.Multiply, N(3), Vec(Nums(1), Nums(2, 4)));
        var divided = Apply(ScadBinaryOperator.Divide, Nums(2, 4), N(2));
        var dividedNested = Apply(ScadBinaryOperator.Divide, Vec(Nums(2), Nums(8)), N(2));

        // Assert
        Assert.Equal(Nums(3, 6), right.Result);
        Assert.Equal(Vec(Nums(3), Nums(6, 12)), left.Result);
        Assert.Equal(Nums(1, 2), divided.Result);
        Assert.Equal(Vec(Nums(1), Nums(4)), dividedNested.Result);
    }

    /// <summary>
    ///     Validates the dot product of two numeric vectors.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_VectorTimesVector_IsDotProduct()
    {
        // Arrange / Act
        var (result, error) = Apply(ScadBinaryOperator.Multiply, Nums(1, 2, 3), Nums(4, 5, 6));

        // Assert
        Assert.Equal(N(32), result);
        Assert.False(error);
    }

    /// <summary>
    ///     Validates matrix times vector.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_MatrixTimesVector_ReturnsProduct()
    {
        // Arrange: a 2 x 3 matrix and a 3-vector
        var matrix = Vec(Nums(1, 2, 3), Nums(4, 5, 6));

        // Act
        var (result, error) = Apply(ScadBinaryOperator.Multiply, matrix, Nums(1, 0, -1));

        // Assert
        Assert.Equal(Nums(-2, -2), result);
        Assert.False(error);
    }

    /// <summary>
    ///     Validates vector times matrix.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_VectorTimesMatrix_ReturnsProduct()
    {
        // Arrange: a 2-vector and a 2 x 3 matrix
        var matrix = Vec(Nums(1, 2, 3), Nums(4, 5, 6));

        // Act
        var (result, error) = Apply(ScadBinaryOperator.Multiply, Nums(1, 1), matrix);

        // Assert
        Assert.Equal(Nums(5, 7, 9), result);
        Assert.False(error);
    }

    /// <summary>
    ///     Validates matrix times matrix, including a non-square product.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_MatrixTimesMatrix_ReturnsProduct()
    {
        // Arrange: 2 x 3 times 3 x 2
        var left = Vec(Nums(1, 2, 3), Nums(4, 5, 6));
        var right = Vec(Nums(7, 8), Nums(9, 10), Nums(11, 12));

        // Act
        var (result, error) = Apply(ScadBinaryOperator.Multiply, left, right);

        // Assert
        Assert.Equal(Vec(Nums(58, 64), Nums(139, 154)), result);
        Assert.False(error);
    }

    /// <summary>
    ///     Validates that the identity matrix leaves a vector unchanged.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_IdentityMatrixTimesVector_ReturnsSameVector()
    {
        // Arrange
        var identity = Vec(Nums(1, 0), Nums(0, 1));

        // Act
        var (result, _) = Apply(ScadBinaryOperator.Multiply, identity, Nums(3, 4));

        // Assert
        Assert.Equal(Nums(3, 4), result);
    }

    /// <summary>
    ///     Validates unary operators.
    /// </summary>
    [Fact]
    public void ScadValue_Unary_NegatePlusNot_Apply()
    {
        // Arrange / Act
        var negated = ScadValue.Unary(ScadUnaryOperator.Negate, N(2), out var e1);
        var negatedVector = ScadValue.Unary(ScadUnaryOperator.Negate, Vec(Nums(1, -2), N(3)), out var e2);
        var plus = ScadValue.Unary(ScadUnaryOperator.Plus, N(2), out var e3);
        var plusVector = ScadValue.Unary(ScadUnaryOperator.Plus, Nums(1), out var e4);
        var not = ScadValue.Unary(ScadUnaryOperator.Not, Vec(), out var e5);

        // Assert
        Assert.Equal(N(-2), negated);
        Assert.Equal(Vec(Nums(-1, 2), N(-3)), negatedVector);
        Assert.Equal(N(2), plus);
        Assert.Equal(Nums(1), plusVector);
        Assert.True(not.AsBoolean);
        Assert.False(e1 || e2 || e3 || e4 || e5);
    }

    /// <summary>
    ///     Validates that unary plus and negation reject other kinds, also inside vectors, but logical not never errors.
    /// </summary>
    [Fact]
    public void ScadValue_Unary_InvalidOperand_ReturnsUndefWithError()
    {
        // Arrange / Act
        var negateString = ScadValue.Unary(ScadUnaryOperator.Negate, S("a"), out var e1);
        var plusBool = ScadValue.Unary(ScadUnaryOperator.Plus, ScadValue.True, out var e2);
        var negateElement = ScadValue.Unary(ScadUnaryOperator.Negate, Vec(N(1), S("a")), out var e3);
        var notString = ScadValue.Unary(ScadUnaryOperator.Not, S("a"), out var e4);

        // Assert
        Assert.Equal(ScadValue.Undef, negateString);
        Assert.Equal(ScadValue.Undef, plusBool);
        Assert.Equal(Vec(N(-1), ScadValue.Undef), negateElement);
        Assert.True(e1 && e2 && e3);
        Assert.False(notString.AsBoolean);
        Assert.False(e4);
    }

    /// <summary>
    ///     Validates vector indexing, including truncation and out-of-range positions.
    /// </summary>
    [Fact]
    public void ScadValue_Index_Vector_ReturnsElementOrUndef()
    {
        // Arrange
        var vector = Nums(10, 20, 30);

        // Act / Assert
        Assert.Equal(N(10), vector.Index(N(0)));
        Assert.Equal(N(30), vector.Index(N(2)));
        Assert.Equal(N(20), vector.Index(N(1.9)));
        Assert.Equal(N(10), vector.Index(N(-0.5)));
        Assert.Equal(ScadValue.Undef, vector.Index(N(3)));
        Assert.Equal(ScadValue.Undef, vector.Index(N(-1)));
        Assert.Equal(ScadValue.Undef, vector.Index(N(double.NaN)));
        Assert.Equal(ScadValue.Undef, vector.Index(N(double.PositiveInfinity)));
        Assert.Equal(ScadValue.Undef, vector.Index(N(1e300)));
        Assert.Equal(ScadValue.Undef, vector.Index(S("0")));
        Assert.Equal(ScadValue.Undef, vector.Index(ScadValue.FromRange(0, 1, 2)));
    }

    /// <summary>
    ///     Validates that other targets yield undef when indexed.
    /// </summary>
    [Fact]
    public void ScadValue_Index_NonIndexableTarget_ReturnsUndef()
    {
        // Arrange / Act / Assert
        Assert.Equal(ScadValue.Undef, N(5).Index(N(0)));
        Assert.Equal(ScadValue.Undef, ScadValue.True.Index(N(0)));
        Assert.Equal(ScadValue.Undef, ScadValue.Undef.Index(N(0)));
        Assert.Equal(ScadValue.Undef, ScadValue.FromRange(0, 1, 5).Index(N(0)));
        Assert.Equal(ScadValue.Undef, ScadValue.FromFunction(new TestFunctionValue()).Index(N(0)));
    }

    /// <summary>
    ///     Validates string indexing by Unicode scalar, including a surrogate pair.
    /// </summary>
    [Fact]
    public void ScadValue_Index_String_UsesUnicodeScalars()
    {
        // Arrange: "a", U+1F600 (a surrogate pair), "b"
        var text = S("a\U0001F600b");

        // Act / Assert
        Assert.Equal(S("a"), text.Index(N(0)));
        Assert.Equal(S("\U0001F600"), text.Index(N(1)));
        Assert.Equal(S("b"), text.Index(N(2)));
        Assert.Equal(ScadValue.Undef, text.Index(N(3)));
        Assert.Equal(S("c"), S("abc").Index(N(2.7)));
        Assert.Equal(ScadValue.Undef, S(string.Empty).Index(N(0)));
    }

    /// <summary>
    ///     Validates the x, y and z members.
    /// </summary>
    [Fact]
    public void ScadValue_Member_XyzOnVector_ReturnsElements()
    {
        // Arrange
        var vector = Nums(1, 2, 3);

        // Act / Assert
        Assert.Equal(N(1), vector.Member("x"));
        Assert.Equal(N(2), vector.Member("y"));
        Assert.Equal(N(3), vector.Member("z"));
        Assert.Equal(ScadValue.Undef, Nums(1).Member("y"));
        Assert.Equal(ScadValue.Undef, vector.Member("w"));
        Assert.Equal(ScadValue.Undef, vector.Member("length"));
        Assert.Equal(ScadValue.Undef, N(1).Member("x"));
        Assert.Equal(ScadValue.Undef, S("abc").Member("x"));
    }
}
