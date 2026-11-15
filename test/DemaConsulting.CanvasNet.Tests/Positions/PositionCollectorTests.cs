using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Tests.Positions;

/// <summary>
///     Unit tests for <see cref="PositionCollector"/>: the union of selectors, delivery order, the cap, truncation, and
///     unmatched selectors.
/// </summary>
public class PositionCollectorTests
{
    /// <summary>
    ///     Test that a position matched by several selectors is kept once and the order is the delivery order whatever
    ///     the selector order.
    /// </summary>
    [Fact]
    public void PositionCollector_Offer_SeveralSelectors_KeepsUnionInDeliveryOrder()
    {
        // Arrange
        var forward = new PositionCollector(PositionSelection.Parse(["node:a", "node", "edge"]));
        var backward = new PositionCollector(PositionSelection.Parse(["edge", "node", "node:a"]));
        ImagePosition[] candidates = [new("node", "a"), new("node", "b"), new("edge", "e"), new("note", "n")];

        // Act
        foreach (var candidate in candidates)
        {
            forward.Offer(candidate);
            backward.Offer(candidate);
        }

        // Assert
        Assert.Equal(["a", "b", "e"], forward.Positions.Select(position => position.Id));
        Assert.Equal(["a", "b", "e"], backward.Positions.Select(position => position.Id));
        Assert.Empty(forward.GetUnmatchedSelectors());
        Assert.False(forward.Truncated);
    }

    /// <summary>
    ///     Test that the cap keeps the first matches (structural items delivered first stay), that it is applied after
    ///     the filter, and that truncation is reported only when a match was cut.
    /// </summary>
    [Fact]
    public void PositionCollector_Offer_MoreMatchesThanTheCap_KeepsFirstAndTruncates()
    {
        // Arrange: a title first, then 1500 marks interleaved with 1500 other items that the selection does not want
        var collector = new PositionCollector(PositionSelection.Parse(["title", "mark"]));
        collector.Offer(new ImagePosition("title", "title"));
        for (var index = 0; index < 1500; index++)
        {
            collector.Offer(new ImagePosition("axis", $"x{index}"));
            collector.Offer(new ImagePosition("mark", $"bar-{index}"));
        }

        // Assert
        Assert.Equal(PositionCollector.MaxPositions, collector.Positions.Count);
        Assert.Equal("title", collector.Positions[0].Kind);
        Assert.Equal("bar-998", collector.Positions[^1].Id);
        Assert.True(collector.Truncated);
    }

    /// <summary>
    ///     Test that exactly reaching the cap is not truncation.
    /// </summary>
    [Fact]
    public void PositionCollector_Offer_ExactlyTheCap_IsNotTruncated()
    {
        // Arrange
        var collector = new PositionCollector(PositionSelection.All, 3);

        // Act
        for (var index = 0; index < 3; index++)
        {
            collector.Offer(new ImagePosition("node", $"n{index}"));
        }

        collector.Offer(new ImagePosition("edge", "e"));
        var exact = new PositionCollector(PositionSelection.All, 3);
        for (var index = 0; index < 3; index++)
        {
            exact.Offer(new ImagePosition("node", $"n{index}"));
        }

        // Assert
        Assert.True(collector.Truncated);
        Assert.False(exact.Truncated);
        Assert.Equal(3, exact.Positions.Count);
    }

    /// <summary>
    ///     Test that a selector whose matches were all cut by the cap is not unmatched, and one that matched nothing is.
    /// </summary>
    [Fact]
    public void PositionCollector_GetUnmatchedSelectors_MatchedButCapped_IsNotUnmatched()
    {
        // Arrange
        var collector = new PositionCollector(PositionSelection.Parse(["node", "edge", "node:Missing"]), 1);

        // Act
        collector.Offer(new ImagePosition("node", "send"));
        collector.Offer(new ImagePosition("edge", "e"));

        // Assert
        Assert.Single(collector.Positions);
        Assert.True(collector.Truncated);
        Assert.Equal(["node:Missing"], collector.GetUnmatchedSelectors().Select(selector => selector.Text));
    }

    /// <summary>
    ///     Test the argument checks and the kind gate.
    /// </summary>
    [Fact]
    public void PositionCollector_Constructor_BadArguments_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new PositionCollector(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PositionCollector(PositionSelection.All, 0));
        Assert.True(new PositionCollector(PositionSelection.Parse(["node"])).IncludesKind("node"));
        Assert.False(new PositionCollector(PositionSelection.Parse(["node"])).IncludesKind("edge"));
    }
}
