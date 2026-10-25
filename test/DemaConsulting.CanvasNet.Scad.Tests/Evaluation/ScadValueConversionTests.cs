using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="ScadValue.TryFromObject"/>.
/// </summary>
public class ScadValueConversionTests
{
    /// <summary>
    ///     Gets a sequence that never ends.
    /// </summary>
    /// <returns>An endless sequence of numbers.</returns>
    private static IEnumerable<int> Endless()
    {
        var i = 0;
        while (i < int.MaxValue)
        {
            yield return i++;
        }
    }

    /// <summary>
    ///     Validates that scalars convert to the matching kinds.
    /// </summary>
    [Fact]
    public void ScadValue_TryFromObject_Scalars_Convert()
    {
        // Arrange / Act / Assert
        Assert.True(ScadValue.TryFromObject(null, out var undef));
        Assert.Equal(ScadValue.Undef, undef);
        Assert.True(ScadValue.TryFromObject(true, out var flag));
        Assert.Equal(ScadValue.True, flag);
        Assert.True(ScadValue.TryFromObject("text", out var text));
        Assert.Equal(S("text"), text);
    }

    /// <summary>
    ///     Validates that every supported numeric type converts to a number.
    /// </summary>
    [Fact]
    public void ScadValue_TryFromObject_NumericTypes_ConvertToNumbers()
    {
        // Arrange
        object[] numbers = [(sbyte)-1, (byte)2, (short)-3, (ushort)4, 5, 6u, -7L, 8UL, 1.5f, 2.5, 3.5m];

        // Act / Assert
        double[] expected = [-1, 2, -3, 4, 5, 6, -7, 8, 1.5, 2.5, 3.5];
        for (var i = 0; i < numbers.Length; i++)
        {
            Assert.True(ScadValue.TryFromObject(numbers[i], out var value));
            Assert.Equal(N(expected[i]), value);
        }

        Assert.True(ScadValue.TryFromObject(double.NaN, out var nan));
        Assert.True(double.IsNaN(nan.AsNumber));
        Assert.True(ScadValue.TryFromObject(double.PositiveInfinity, out var infinity));
        Assert.Equal(double.PositiveInfinity, infinity.AsNumber);
    }

    /// <summary>
    ///     Validates that sequences convert to vectors, recursively.
    /// </summary>
    [Fact]
    public void ScadValue_TryFromObject_Sequences_ConvertToVectors()
    {
        // Arrange
        object?[] source = [1, "a", new[] { 2.0, 3.0 }, new List<object?> { true, null }, new int[0]];

        // Act
        var ok = ScadValue.TryFromObject(source, out var value);

        // Assert
        Assert.True(ok);
        Assert.Equal(Vec(N(1), S("a"), Nums(2, 3), Vec(ScadValue.True, ScadValue.Undef), Vec()), value);
    }

    /// <summary>
    ///     Validates that unsupported objects are rejected, also when nested.
    /// </summary>
    [Fact]
    public void ScadValue_TryFromObject_UnsupportedObjects_AreRejected()
    {
        // Arrange / Act / Assert
        Assert.False(ScadValue.TryFromObject(new object(), out var plain));
        Assert.Equal(ScadValue.Undef, plain);
        Assert.False(ScadValue.TryFromObject('c', out _));
        Assert.False(ScadValue.TryFromObject(DateTime.UnixEpoch, out _));
        Assert.False(ScadValue.TryFromObject(new object?[] { 1, new object() }, out var nested));
        Assert.Equal(ScadValue.Undef, nested);
        Assert.False(ScadValue.TryFromObject(new Dictionary<string, int> { ["a"] = 1 }, out _));
    }

    /// <summary>
    ///     Validates that an endless sequence is rejected instead of hanging.
    /// </summary>
    [Fact]
    public void ScadValue_TryFromObject_EndlessSequence_IsRejected()
    {
        // Arrange / Act
        var ok = ScadValue.TryFromObject(Endless(), out var value);

        // Assert
        Assert.False(ok);
        Assert.Equal(ScadValue.Undef, value);
    }

    /// <summary>
    ///     Validates that a self-referential list is rejected instead of overflowing the stack.
    /// </summary>
    [Fact]
    public void ScadValue_TryFromObject_SelfReferentialList_IsRejected()
    {
        // Arrange
        var list = new List<object?>();
        list.Add(list);

        // Act: also from a small stack, because the depth bound must stop the recursion
        var ok = OnSmallStack(() => ScadValue.TryFromObject(list, out _));

        // Assert
        Assert.False(ok);
    }

    /// <summary>
    ///     Validates the nesting bound: depth 256 converts, depth 257 does not.
    /// </summary>
    [Fact]
    public void ScadValue_TryFromObject_Nesting_IsBoundedAt256()
    {
        // Arrange
        object atLimit = Wrap(256);
        object overLimit = Wrap(257);

        // Act
        var okLimit = ScadValue.TryFromObject(atLimit, out var value);
        var okOver = ScadValue.TryFromObject(overLimit, out _);

        // Assert
        Assert.True(okLimit);
        Assert.Equal(ScadValue.MaxNestingDepth, value.Depth);
        Assert.False(okOver);

        static object Wrap(int depth)
        {
            object current = new List<object?>();
            for (var i = 1; i < depth; i++)
            {
                current = new List<object?> { current };
            }

            return current;
        }
    }

    /// <summary>
    ///     Validates the total element budget.
    /// </summary>
    [Fact]
    public void ScadValue_TryFromObject_TooManyElements_IsRejected()
    {
        // Arrange: exactly the budget (root counts as one) fits; one more does not
        var fits = new int[ScadValue.MaxConvertedElements - 1];
        var tooMany = new int[ScadValue.MaxConvertedElements];

        // Act
        var okFits = ScadValue.TryFromObject(fits, out var value);
        var okTooMany = ScadValue.TryFromObject(tooMany, out _);

        // Assert
        Assert.True(okFits);
        Assert.Equal(fits.Length, value.AsVector.Count);
        Assert.False(okTooMany);
    }
}
