using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scenegraph;

/// <summary>
///     Unit tests of <see cref="SceneBudget"/>: exact counting, the failure at the limit and saturation.
/// </summary>
public class SceneBudgetTests
{
    /// <summary>
    ///     Validates that the budget counts exactly, allows the limit itself, fails beyond it with the location, and ignores
    ///     negative counts.
    /// </summary>
    [Fact]
    public void SceneBudget_Add_CountsExactlyAndFailsBeyondTheLimit()
    {
        // Arrange
        var budget = new SceneBudget(3);
        var location = new VegaLiteLocation("a.vl.json", "/mark", 2, 5);

        // Act
        budget.Add(2, location);
        budget.Add(-5, location);
        budget.Add(1, location);

        // Assert: exactly at the limit is allowed, one more is not
        Assert.Equal(3, budget.Count);
        var ex = Assert.Throws<VegaLiteRenderException>(() => budget.Add(1, location));
        Assert.Equal(location, ex.Location);
        Assert.Contains("MaxSceneItems", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the count saturates at the largest long instead of wrapping to a negative number that would pass the limit.
    /// </summary>
    [Fact]
    public void SceneBudget_Add_SaturatesInsteadOfOverflowing()
    {
        // Arrange
        var budget = new SceneBudget(long.MaxValue - 1);

        // Act
        budget.Add(long.MaxValue - 1, default);

        // Assert: the sum of the next count would overflow, but it saturates and passes the limit
        Assert.Throws<VegaLiteRenderException>(() => budget.Add(long.MaxValue, default));
        Assert.Equal(long.MaxValue, budget.Count);
        Assert.Throws<VegaLiteRenderException>(() => budget.Add(1, default));
        Assert.Equal(long.MaxValue, budget.Count);
    }
}
