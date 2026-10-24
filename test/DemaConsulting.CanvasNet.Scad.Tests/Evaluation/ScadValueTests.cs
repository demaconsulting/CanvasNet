using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="ScadValue"/> kinds, factories, truthiness, equality, ordering and immutability.
/// </summary>
public class ScadValueTests
{
    /// <summary>
    ///     Validates that the default value is <c>undef</c>.
    /// </summary>
    [Fact]
    public void ScadValue_Default_IsUndef()
    {
        // Arrange / Act
        var value = default(ScadValue);

        // Assert
        Assert.Equal(ScadValueKind.Undef, value.Kind);
        Assert.Equal(ScadValue.Undef, value);
        Assert.False(value.IsTrue);
    }

    /// <summary>
    ///     Validates that every factory produces the matching kind and payload.
    /// </summary>
    [Fact]
    public void ScadValue_Factories_ProduceMatchingKindAndPayload()
    {
        // Arrange
        var function = new TestFunctionValue("a");

        // Act
        var number = ScadValue.FromNumber(2.5);
        var text = ScadValue.FromString("hi");
        var vector = ScadValue.FromVector(new[] { N(1), N(2) });
        var fromList = ScadValue.FromVector(new List<ScadValue> { N(7) });
        var range = ScadValue.FromRange(0, 2, 10);
        var func = ScadValue.FromFunction(function);

        // Assert
        Assert.Equal(ScadValueKind.Number, number.Kind);
        Assert.Equal(2.5, number.AsNumber);
        Assert.Equal("hi", text.AsString);
        Assert.Equal(2, vector.AsVector.Count);
        Assert.Equal(N(2), vector.AsVector[1]);
        Assert.Equal(N(7), fromList.AsVector[0]);
        Assert.Equal(ScadValueKind.Range, range.Kind);
        Assert.Equal(10, range.AsRange.End);
        Assert.Same(function, func.AsFunction);
        Assert.True(ScadValue.True.AsBoolean);
        Assert.False(ScadValue.False.AsBoolean);
        Assert.Equal(ScadValue.True, ScadValue.FromBoolean(true));
    }

    /// <summary>
    ///     Validates that reading a payload of the wrong kind throws.
    /// </summary>
    [Fact]
    public void ScadValue_Payload_WrongKind_Throws()
    {
        // Arrange
        var value = S("x");

        // Act / Assert: each accessor other than the string one rejects a string
        Assert.Throws<InvalidOperationException>(() => value.AsNumber);
        Assert.Throws<InvalidOperationException>(() => value.AsBoolean);
        Assert.Throws<InvalidOperationException>(() => value.AsVector);
        Assert.Throws<InvalidOperationException>(() => value.AsRange);
        Assert.Throws<InvalidOperationException>(() => value.AsFunction);
    }

    /// <summary>
    ///     Validates the truthiness table.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="expected">The expected truthiness.</param>
    [Theory]
    [InlineData("undef", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("zero", false)]
    [InlineData("negative-zero", false)]
    [InlineData("one", true)]
    [InlineData("negative", true)]
    [InlineData("nan", true)]
    [InlineData("infinity", true)]
    [InlineData("empty-string", false)]
    [InlineData("string", true)]
    [InlineData("string-false", true)]
    [InlineData("empty-vector", false)]
    [InlineData("vector", true)]
    [InlineData("vector-of-zero", true)]
    [InlineData("range", true)]
    [InlineData("empty-range", true)]
    [InlineData("function", true)]
    public void ScadValue_IsTrue_Table(string name, bool expected)
    {
        // Arrange
        var value = name switch
        {
            "undef" => ScadValue.Undef,
            "true" => ScadValue.True,
            "false" => ScadValue.False,
            "zero" => N(0),
            "negative-zero" => N(-0.0),
            "one" => N(1),
            "negative" => N(-3),
            "nan" => N(double.NaN),
            "infinity" => N(double.PositiveInfinity),
            "empty-string" => S(string.Empty),
            "string" => S("a"),
            "string-false" => S("false"),
            "empty-vector" => Vec(),
            "vector" => Nums(1),
            "vector-of-zero" => Nums(0),
            "range" => ScadValue.FromRange(0, 1, 3),
            "empty-range" => ScadValue.FromRange(3, 1, 0),
            _ => ScadValue.FromFunction(new TestFunctionValue()),
        };

        // Act / Assert
        Assert.Equal(expected, value.IsTrue);
    }

    /// <summary>
    ///     Validates equality of equal and unequal pairs of the same kind.
    /// </summary>
    [Fact]
    public void ScadValue_Equals_SameKind_ComparesByValue()
    {
        // Arrange / Act / Assert
        Assert.Equal(N(1), N(1));
        Assert.NotEqual(N(1), N(2));
        Assert.Equal(S("a"), S("a"));
        Assert.NotEqual(S("a"), S("A"));
        Assert.Equal(ScadValue.Undef, ScadValue.Undef);
        Assert.Equal(ScadValue.True, ScadValue.True);
        Assert.NotEqual(ScadValue.True, ScadValue.False);
        Assert.Equal(Nums(1, 2), Nums(1, 2));
        Assert.NotEqual(Nums(1, 2), Nums(1, 3));
        Assert.NotEqual(Nums(1, 2), Nums(1, 2, 3));
        Assert.Equal(Vec(Nums(1), S("x")), Vec(Nums(1), S("x")));
        Assert.Equal(ScadValue.FromRange(0, 1, 5), ScadValue.FromRange(0, 1, 5));
        Assert.NotEqual(ScadValue.FromRange(0, 1, 5), ScadValue.FromRange(0, 1, 6));
        Assert.True(N(1) == N(1));
        Assert.True(N(1) != N(2));
    }

    /// <summary>
    ///     Validates that values of different kinds are never equal, including number and boolean.
    /// </summary>
    [Fact]
    public void ScadValue_Equals_DifferentKinds_AreNotEqual()
    {
        // Arrange / Act / Assert
        Assert.NotEqual(N(1), ScadValue.True);
        Assert.NotEqual(N(0), ScadValue.False);
        Assert.NotEqual(N(0), ScadValue.Undef);
        Assert.NotEqual(S("1"), N(1));
        Assert.NotEqual(Vec(), ScadValue.Undef);
        Assert.NotEqual(Vec(), S(string.Empty));
        Assert.False(N(1).Equals((object?)"1"));
        Assert.False(N(1).Equals(null));
    }

    /// <summary>
    ///     Validates the IEEE rules: NaN equals nothing, and zero equals negative zero.
    /// </summary>
    [Fact]
    public void ScadValue_Equals_NaNAndNegativeZero_FollowIeee()
    {
        // Arrange
        var nan = N(double.NaN);
        var otherNan = N(double.NaN);

        // Act / Assert
        Assert.False(nan.Equals(nan));
        Assert.True(nan != otherNan);
        Assert.False(Nums(double.NaN).Equals(Nums(double.NaN)));
        Assert.True(N(0).Equals(N(-0.0)));
        Assert.Equal(N(0).GetHashCode(), N(-0.0).GetHashCode());
        Assert.True(N(double.PositiveInfinity).Equals(N(double.PositiveInfinity)));
    }

    /// <summary>
    ///     Validates that functions are equal only by reference.
    /// </summary>
    [Fact]
    public void ScadValue_Equals_Functions_ByReference()
    {
        // Arrange
        var shared = new TestFunctionValue("a");

        // Act / Assert
        Assert.Equal(ScadValue.FromFunction(shared), ScadValue.FromFunction(shared));
        Assert.NotEqual(ScadValue.FromFunction(shared), ScadValue.FromFunction(new TestFunctionValue("a")));
    }

    /// <summary>
    ///     Validates that equal values have equal hash codes.
    /// </summary>
    [Fact]
    public void ScadValue_GetHashCode_EqualValues_HaveEqualHashCodes()
    {
        // Arrange
        var function = new TestFunctionValue();
        var pairs = new (ScadValue, ScadValue)[]
        {
            (ScadValue.Undef, default),
            (N(2), N(2)),
            (ScadValue.True, ScadValue.True),
            (S("abc"), S("abc")),
            (Vec(Nums(1), S("x")), Vec(Nums(1), S("x"))),
            (ScadValue.FromRange(0, 1, 5), ScadValue.FromRange(0, 1, 5)),
            (ScadValue.FromFunction(function), ScadValue.FromFunction(function)),
        };

        // Act / Assert
        foreach (var (left, right) in pairs)
        {
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }
    }

    /// <summary>
    ///     Validates the ordering rules for the ordered kinds.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <param name="expected">The expected sign of the comparison.</param>
    [Theory]
    [InlineData("num-less", -1)]
    [InlineData("num-equal", 0)]
    [InlineData("num-greater", 1)]
    [InlineData("zero-negative-zero", 0)]
    [InlineData("string-less", -1)]
    [InlineData("string-case", -1)]
    [InlineData("string-prefix", -1)]
    [InlineData("bool", -1)]
    [InlineData("vector-first-element", -1)]
    [InlineData("vector-prefix", -1)]
    [InlineData("vector-equal", 0)]
    [InlineData("vector-longer-greater", 1)]
    [InlineData("vector-nested", 1)]
    public void ScadValue_TryCompare_OrderedPairs_ReturnsSign(string name, int expected)
    {
        // Arrange
        var (left, right) = name switch
        {
            "num-less" => (N(1), N(2)),
            "num-equal" => (N(2), N(2)),
            "num-greater" => (N(3), N(2)),
            "zero-negative-zero" => (N(0), N(-0.0)),
            "string-less" => (S("abc"), S("abd")),
            "string-case" => (S("Z"), S("a")),
            "string-prefix" => (S("ab"), S("abc")),
            "bool" => (ScadValue.False, ScadValue.True),
            "vector-first-element" => (Nums(1, 9), Nums(2, 0)),
            "vector-prefix" => (Nums(1), Nums(1, 0)),
            "vector-equal" => (Nums(1, 2), Nums(1, 2)),
            "vector-longer-greater" => (Nums(1, 2, 3), Nums(1, 2)),
            _ => (Vec(Nums(2)), Vec(Nums(1, 5))),
        };

        // Act
        var ordered = ScadValue.TryCompare(left, right, out var result);

        // Assert
        Assert.True(ordered);
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Validates that every unordered pairing reports false for all four relational operators.
    /// </summary>
    /// <param name="name">The case name.</param>
    [Theory]
    [InlineData("nan-left")]
    [InlineData("nan-right")]
    [InlineData("mixed-kinds")]
    [InlineData("number-bool")]
    [InlineData("undef-undef")]
    [InlineData("range-range")]
    [InlineData("function-function")]
    [InlineData("vector-with-nan")]
    [InlineData("vector-mixed-elements")]
    [InlineData("string-number")]
    public void ScadValue_Relational_UnorderedPairs_AreAllFalse(string name)
    {
        // Arrange
        var function = ScadValue.FromFunction(new TestFunctionValue());
        var (left, right) = name switch
        {
            "nan-left" => (N(double.NaN), N(1)),
            "nan-right" => (N(1), N(double.NaN)),
            "mixed-kinds" => (S("a"), Nums(1)),
            "number-bool" => (N(1), ScadValue.True),
            "undef-undef" => (ScadValue.Undef, ScadValue.Undef),
            "range-range" => (ScadValue.FromRange(0, 1, 3), ScadValue.FromRange(0, 1, 3)),
            "function-function" => (function, function),
            "vector-with-nan" => (Nums(double.NaN), Nums(1)),
            "vector-mixed-elements" => (Vec(N(1), S("a")), Vec(N(1), N(2))),
            _ => (S("1"), N(1)),
        };

        // Act
        var ordered = ScadValue.TryCompare(left, right, out var result);

        // Assert: unordered and false for all four operators, never a type error
        Assert.False(ordered);
        Assert.Equal(0, result);
        foreach (var op in new[]
        {
            ScadBinaryOperator.Less, ScadBinaryOperator.LessEqual, ScadBinaryOperator.Greater, ScadBinaryOperator.GreaterEqual,
        })
        {
            var outcome = ScadValue.Binary(op, left, right, out var typeError);
            Assert.False(outcome.AsBoolean);
            Assert.False(typeError);
        }
    }

    /// <summary>
    ///     Validates the four relational operators on an ordered pair.
    /// </summary>
    [Fact]
    public void ScadValue_Binary_Relational_OrderedPair_AppliesOperators()
    {
        // Arrange / Act
        var less = ScadValue.Binary(ScadBinaryOperator.Less, N(1), N(2), out _);
        var lessEqual = ScadValue.Binary(ScadBinaryOperator.LessEqual, N(2), N(2), out _);
        var greater = ScadValue.Binary(ScadBinaryOperator.Greater, N(1), N(2), out _);
        var greaterEqual = ScadValue.Binary(ScadBinaryOperator.GreaterEqual, N(1), N(2), out _);

        // Assert
        Assert.True(less.AsBoolean);
        Assert.True(lessEqual.AsBoolean);
        Assert.False(greater.AsBoolean);
        Assert.False(greaterEqual.AsBoolean);
    }

    /// <summary>
    ///     Validates that a value is immutable: a vector built from an array is unaffected by later reads and
    ///     the element span is read-only.
    /// </summary>
    [Fact]
    public void ScadValue_Vector_IsImmutableView()
    {
        // Arrange
        var source = new List<ScadValue> { N(1), N(2) };
        var value = ScadValue.FromVector(source);

        // Act: change the source list after construction
        source[0] = N(99);
        source.Add(N(3));

        // Assert: the value keeps its own copy
        Assert.Equal(Nums(1, 2), value);
        Assert.Equal(2, value.AsVector.AsSpan().Length);
    }

    /// <summary>
    ///     Validates the depth of a value: zero for non-vectors and the nesting level for vectors.
    /// </summary>
    [Fact]
    public void ScadValue_Depth_ReflectsNesting()
    {
        // Arrange / Act / Assert
        Assert.Equal(0, N(1).Depth);
        Assert.Equal(0, S("a").Depth);
        Assert.Equal(1, Vec().Depth);
        Assert.Equal(1, Nums(1, 2).Depth);
        Assert.Equal(3, Vec(Nums(1), Vec(Vec())).Depth);
        Assert.Equal(7, Nested(7).Depth);
    }

    /// <summary>
    ///     Validates the diagnostic phrases that name each kind.
    /// </summary>
    [Fact]
    public void ScadValue_KindDescription_NamesEachKind()
    {
        // Arrange / Act / Assert
        Assert.Equal("undef", ScadValue.Undef.KindDescription);
        Assert.Equal("a boolean", ScadValue.True.KindDescription);
        Assert.Equal("a number", N(1).KindDescription);
        Assert.Equal("a string", S("").KindDescription);
        Assert.Equal("a vector", Vec().KindDescription);
        Assert.Equal("a range", ScadValue.FromRange(0, 1, 2).KindDescription);
        Assert.Equal("a function", ScadValue.FromFunction(new TestFunctionValue()).KindDescription);
    }
}
