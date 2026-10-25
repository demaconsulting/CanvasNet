using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluationTestHelpers;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of the limits <see cref="ScadValue.Binary"/> applies to a matrix product when it runs under an evaluation
///     host: result size, charged work and cancellation polls.
/// </summary>
public class ScadValueProductLimitTests
{
    /// <summary>
    ///     Builds a matrix of ones.
    /// </summary>
    /// <param name="rows">The row count.</param>
    /// <param name="columns">The column count.</param>
    /// <returns>The matrix.</returns>
    private static ScadValue Ones(int rows, int columns)
    {
        var row = new double[columns];
        Array.Fill(row, 1.0);
        var items = new ScadValue[rows];
        for (var i = 0; i < rows; i++)
        {
            items[i] = Nums(row);
        }

        return ScadValue.FromVector(items);
    }

    /// <summary>
    ///     Multiplies two values under a host.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="host">The host.</param>
    /// <returns>The product.</returns>
    private static ScadValue Multiply(ScadValue left, ScadValue right, ScadTestBuiltinHost host) =>
        ScadValue.Binary(ScadBinaryOperator.Multiply, left, right, out _, host);

    /// <summary>
    ///     Validates that a result with more elements than the list limit is refused before anything is charged.
    /// </summary>
    [Fact]
    public void ScadValueProduct_Binary_MatrixTimesMatrix_ResultOverListLimit_ThrowsBeforeCharging()
    {
        // Arrange: 2 x 2 times 2 x 5 has 10 result elements, 2 x 2 times 2 x 6 has 12
        var atLimit = new ScadTestBuiltinHost { MaxListLength = 10 };
        var over = new ScadTestBuiltinHost { MaxListLength = 10 };

        // Act
        var product = Multiply(Ones(2, 2), Ones(2, 5), atLimit);
        var ex = Assert.Throws<ScadEvaluationException>(() => Multiply(Ones(2, 2), Ones(2, 6), over));

        // Assert
        Assert.Equal(2, product.AsVector.Count);
        Assert.Contains("List too long (limit 10)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, over.Steps);
    }

    /// <summary>
    ///     Validates the charging formula: result elements plus the multiply-add work in units of
    ///     <see cref="ScadValue.MultiplyAddsPerStep"/>, rounded up.
    /// </summary>
    /// <param name="rows">The left rows.</param>
    /// <param name="inner">The inner dimension.</param>
    /// <param name="columns">The right columns.</param>
    /// <param name="expected">The expected charge.</param>
    [Theory]
    [InlineData(2, 2, 2, 5)]
    [InlineData(3, 4, 5, 19)]
    [InlineData(1, 1, 1, 2)]
    [InlineData(10, 10, 10, 163)]
    public void ScadValueProduct_Binary_MatrixTimesMatrix_ChargesResultElementsPlusScaledWork(int rows, int inner, int columns, long expected)
    {
        // Arrange
        Assert.Equal(16, ScadValue.MultiplyAddsPerStep);
        var host = new ScadTestBuiltinHost { MaxListLength = 10_000 };

        // Act
        Multiply(Ones(rows, inner), Ones(inner, columns), host);

        // Assert
        Assert.Equal(expected, host.Steps);
        Assert.Equal(expected, ScadValue.ProductSteps(rows, inner, columns));
    }

    /// <summary>
    ///     Validates that a product that does not fit the step budget is refused before any multiplication or poll.
    /// </summary>
    [Fact]
    public void ScadValueProduct_Binary_MatrixTimesMatrix_OverStepBudget_ThrowsWithoutComputing()
    {
        // Arrange: 100 x 100 times 100 x 100 costs 10,000 + 62,500 steps
        var cost = ScadValue.ProductSteps(100, 100, 100);
        var host = new ScadTestBuiltinHost { MaxListLength = 100_000, MaxSteps = cost - 1 };

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Multiply(Ones(100, 100), Ones(100, 100), host));

        // Assert
        Assert.Equal(72_500, cost);
        Assert.Contains("Evaluation step limit exceeded", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, host.Polls);
    }

    /// <summary>
    ///     Validates that the work computation saturates instead of overflowing.
    /// </summary>
    [Fact]
    public void ScadValueProduct_ProductWork_HugeDimensions_SaturatesWithoutOverflow()
    {
        // Arrange / Act / Assert
        Assert.Equal(long.MaxValue, ScadValue.ProductWork(int.MaxValue, int.MaxValue, int.MaxValue));
        Assert.True(ScadValue.ProductSteps(int.MaxValue, int.MaxValue, int.MaxValue) > 4_000_000_000_000_000_000L);
        Assert.Equal(24, ScadValue.ProductWork(2, 3, 4));
        Assert.Equal(0, ScadValue.ProductWork(0, 3, 4));
    }

    /// <summary>
    ///     Validates that every kind of large product polls for cancellation at fixed multiply-add intervals and
    ///     that a poll that throws stops the product.
    /// </summary>
    /// <param name="shape">The product shape: matrix times matrix, matrix times vector or vector times matrix.</param>
    [Theory]
    [InlineData("MatrixTimesMatrix")]
    [InlineData("MatrixTimesVector")]
    [InlineData("VectorTimesMatrix")]
    public void ScadValueProduct_Binary_LargeProducts_PollCancellationAtBoundedIntervals(string shape)
    {
        // Arrange: each shape does well over 300,000 multiply-adds
        var (left, right, work) = shape switch
        {
            "MatrixTimesMatrix" => (Ones(100, 100), Ones(100, 100), 1_000_000L),
            "MatrixTimesVector" => (Ones(2000, 200), Nums([.. Enumerable.Repeat(1.0, 200)]), 400_000L),
            _ => (Nums([.. Enumerable.Repeat(1.0, 1000)]), Ones(1000, 400), 400_000L),
        };
        var counting = new ScadTestBuiltinHost { MaxListLength = 100_000 };
        var canceling = new ScadTestBuiltinHost { MaxListLength = 100_000, CancelAfterPolls = 2 };

        // Act
        Multiply(left, right, counting);
        Assert.Throws<OperationCanceledException>(() => Multiply(left, right, canceling));

        // Assert: the poll count is exactly the work divided by the interval, so it is deterministic
        Assert.Equal(work / ScadValue.PollIntervalMultiplyAdds, counting.Polls);
        Assert.Equal(2, canceling.Polls);
    }

    /// <summary>
    ///     Validates that a product whose result is over the list limit is refused from the shapes alone: the operands
    ///     are not copied into arrays first, even when they are large.
    /// </summary>
    [Fact]
    public void ScadValueProduct_Binary_MatrixTimesMatrix_ResultOverListLimit_DoesNotCopyOperands()
    {
        // Arrange: 1000 x 1000 times 1000 x 1001 (rows shared, so building them costs almost nothing) would copy about
        // two million doubles (16 MB) before the old code looked at the result size; the result has 1,001,000 elements
        var leftRow = Nums([.. Enumerable.Repeat(1.0, 1000)]);
        var rightRow = Nums([.. Enumerable.Repeat(1.0, 1001)]);
        var left = ScadValue.FromVector([.. Enumerable.Repeat(leftRow, 1000)]);
        var right = ScadValue.FromVector([.. Enumerable.Repeat(rightRow, 1000)]);
        var host = new ScadTestBuiltinHost { MaxListLength = 1_000_000 };
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Multiply(left, right, host));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: refused before the step charge, with room for the exception and message only
        Assert.Contains("List too long (limit 1000000)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, host.Steps);
        Assert.True(allocated < 100_000, $"allocated={allocated}");
    }

    /// <summary>
    ///     Validates that a product that does not fit the step budget is also refused before the operands are copied.
    /// </summary>
    [Fact]
    public void ScadValueProduct_Binary_MatrixTimesMatrix_OverStepBudget_DoesNotCopyOperands()
    {
        // Arrange: 1000 x 1000 times 1000 x 1000 has a result at the list limit but costs far more than the budget
        var row = Nums([.. Enumerable.Repeat(1.0, 1000)]);
        var matrix = ScadValue.FromVector([.. Enumerable.Repeat(row, 1000)]);
        var host = new ScadTestBuiltinHost { MaxListLength = 1_000_000, MaxSteps = 1_000_000 };
        var before = GC.GetAllocatedBytesForCurrentThread();

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Multiply(matrix, matrix, host));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Contains("Evaluation step limit exceeded", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, host.Polls);
        Assert.True(allocated < 100_000, $"allocated={allocated}");
    }

    /// <summary>
    ///     Validates that a single long dot product polls inside itself, not only between dot products.
    /// </summary>
    [Fact]
    public void ScadValueProduct_Binary_LongInnerDimension_PollsInsideTheDotProduct()
    {
        // Arrange: 1 x 200,000 times 200,000 x 1 is one dot product of 200,000 multiply-adds
        var host = new ScadTestBuiltinHost { MaxListLength = 100_000 };

        // Act
        var product = Multiply(Ones(1, 200_000), Ones(200_000, 1), host);

        // Assert
        Assert.Equal(N(200_000), product.AsVector[0].AsVector[0]);
        Assert.Equal(200_000 / ScadValue.PollIntervalMultiplyAdds, host.Polls);
    }

    /// <summary>
    ///     Validates that, without a host, the product is unbounded and unpolled (value-level use).
    /// </summary>
    [Fact]
    public void ScadValueProduct_Binary_WithoutHost_ComputesUnbounded()
    {
        // Arrange
        var left = Vec(Nums(1, 2, 3), Nums(4, 5, 6));
        var right = Vec(Nums(7, 8), Nums(9, 10), Nums(11, 12));

        // Act
        var product = ScadValue.Binary(ScadBinaryOperator.Multiply, left, right, out var error);

        // Assert
        Assert.Equal(Vec(Nums(58, 64), Nums(139, 154)), product);
        Assert.False(error);
    }
}
