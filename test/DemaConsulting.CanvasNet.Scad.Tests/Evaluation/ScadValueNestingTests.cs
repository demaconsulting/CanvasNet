using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests for the nesting bound of <see cref="ScadValue"/> and the stack safety of its recursive operations.
/// </summary>
public class ScadValueNestingTests
{
    /// <summary>
    ///     Validates the maximum nesting constant and that depth is computed (not limited) by the factory.
    /// </summary>
    [Fact]
    public void ScadValue_MaxNestingDepth_Is256_AndFactoryDoesNotLimit()
    {
        // Arrange / Act
        var atLimit = Nested(ScadValue.MaxNestingDepth);
        var overLimit = Nested(ScadValue.MaxNestingDepth + 1);

        // Assert: the evaluator rejects values over the limit by reading Depth
        Assert.Equal(256, ScadValue.MaxNestingDepth);
        Assert.True(atLimit.Depth <= ScadValue.MaxNestingDepth);
        Assert.True(overLimit.Depth > ScadValue.MaxNestingDepth);
    }

    /// <summary>
    ///     Validates that equality, hashing, ordering and formatting work on a depth-256 vector from a small caller stack.
    /// </summary>
    [Fact]
    public void ScadValue_ReadOnlyOperations_AtDepth256_OnSmallStack_Succeed()
    {
        // Arrange / Act
        var result = OnSmallStack(() =>
        {
            var left = Nested(ScadValue.MaxNestingDepth);
            var right = Nested(ScadValue.MaxNestingDepth);
            var ordered = ScadValue.TryCompare(left, right, out var order);
            return (Equal: left.Equals(right), Hash: left.GetHashCode() == right.GetHashCode(), Ordered: ordered, Order: order, TextLength: left.ToEchoText().Length);
        });

        // Assert
        Assert.True(result.Equal);
        Assert.True(result.Hash);
        Assert.True(result.Ordered);
        Assert.Equal(0, result.Order);
        Assert.Equal((2 * ScadValue.MaxNestingDepth) + 1, result.TextLength);
    }

    /// <summary>
    ///     Validates that vector arithmetic works on a depth-256 vector; the frames of the recursive arithmetic are
    ///     larger, so a 1 MiB caller is used (a smaller caller makes the stack guard trip, which the evaluator
    ///     answers by restarting on its large-stack thread).
    /// </summary>
    [Fact]
    public void ScadValue_Arithmetic_AtDepth256_OnModerateStack_Succeeds()
    {
        // Arrange / Act
        var result = OnSmallStack(
            () =>
            {
                var value = Nested(ScadValue.MaxNestingDepth);
                var sum = ScadValue.Binary(ScadBinaryOperator.Add, value, value, out var addError);
                var scaled = ScadValue.Binary(ScadBinaryOperator.Multiply, value, N(2), out var scaleError);
                var divided = ScadValue.Binary(ScadBinaryOperator.Divide, value, N(2), out var divError);
                var negated = ScadValue.Unary(ScadUnaryOperator.Negate, value, out var negError);
                return (Depths: new[] { sum.Depth, scaled.Depth, divided.Depth, negated.Depth }, Error: addError || scaleError || divError || negError);
            },
            1024 * 1024);

        // Assert
        Assert.All(result.Depths, depth => Assert.Equal(ScadValue.MaxNestingDepth, depth));
        Assert.False(result.Error);
    }

    /// <summary>
    ///     Validates that vector arithmetic never deepens a value.
    /// </summary>
    [Fact]
    public void ScadValue_Arithmetic_NeverIncreasesDepth()
    {
        // Arrange
        var value = Nested(5);

        // Act
        var sum = ScadValue.Binary(ScadBinaryOperator.Add, value, value, out _);
        var product = ScadValue.Binary(ScadBinaryOperator.Multiply, value, N(3), out _);

        // Assert
        Assert.Equal(5, sum.Depth);
        Assert.Equal(5, product.Depth);
    }

    /// <summary>
    ///     Validates that an absurdly deep value tripping the stack guard on a tiny stack throws the stack guard
    ///     exception (which the evaluator turns into a restart) rather than crashing the process.
    /// </summary>
    [Fact]
    public void ScadValue_Equals_ExtremeDepthOnTinyStack_ThrowsInsufficientStackNotCrash()
    {
        // Arrange: a value nested 20,000 deep (far beyond the evaluator's bound) compared on a 64 KiB stack
        var deep = Nested(20_000);
        var other = Nested(20_000);

        // Act / Assert
        Assert.Throws<InsufficientExecutionStackException>(
            () => OnSmallStack(() => deep.Equals(other), 64 * 1024));
    }
}
