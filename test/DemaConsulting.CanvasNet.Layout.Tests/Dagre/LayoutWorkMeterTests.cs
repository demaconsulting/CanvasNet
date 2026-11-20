using DemaConsulting.CanvasNet.Layout.Dagre;
using DemaConsulting.CanvasNet.Layout.Graphlib;

namespace DemaConsulting.CanvasNet.Layout.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="LayoutWorkMeter"/>, through the test consumer <see cref="TestLayoutWorkMeter"/>.
/// </summary>
public class LayoutWorkMeterTests
{
    /// <summary>
    ///     Validates that work is added up to the limit and that passing it fails with the consumer's limit failure.
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_Charge_PastLimit_ThrowsConsumerLimitFailure()
    {
        // Arrange
        var meter = new TestLayoutWorkMeter(10, CancellationToken.None);

        // Act
        meter.Charge(4);
        meter.Charge(6);
        var ex = Assert.Throws<LayoutTestException>(() => meter.Charge(1));

        // Assert
        Assert.True(ex.IsLimit);
        Assert.Equal(10, ex.MaxWork);
        Assert.Equal(10, meter.Used);
    }

    /// <summary>
    ///     Validates that a canceled call stops at the next charge.
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_Charge_Canceled_ThrowsOperationCanceled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var meter = new TestLayoutWorkMeter(100, source.Token);
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => meter.Charge(1));
    }

    /// <summary>
    ///     Validates lodash's unique ids: one counter shared by every prefix.
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_UniqueId_Prefixes_ShareOneCounter()
    {
        // Arrange
        var meter = new TestLayoutWorkMeter(1, CancellationToken.None);

        // Act
        var ids = new[] { meter.UniqueId("_d"), meter.UniqueId("rev"), meter.UniqueId("_d") };

        // Assert
        Assert.Equal(["_d1", "rev2", "_d3"], ids);
    }

    /// <summary>
    ///     Validates that a limit below one is refused.
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_Constructor_ZeroLimit_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestLayoutWorkMeter(0, CancellationToken.None));
    }

    /// <summary>
    ///     Validates that the render failure the layout reports for a graph dagre cannot lay out is the one the consumer
    ///     creates.
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_Failure_Description_CreatedByConsumer()
    {
        // Arrange
        LayoutWorkMeter meter = new TestLayoutWorkMeter(1, CancellationToken.None);

        // Act
        var ex = Assert.IsType<LayoutTestException>(meter.Failure("no route"));

        // Assert
        Assert.False(ex.IsLimit);
        Assert.Contains("no route", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the consumer's limit failure, raised inside a layout by the meter, passes through the layout
    ///     unchanged instead of being wrapped as a render failure (although it is an invalid operation).
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_IsFailure_ConsumerFailureInLayout_PassesUnchanged()
    {
        // Arrange: a two-node graph and a budget of one step, which the first charge of the layout passes
        var g = new Graph<DagreGraph, DagreNode, DagreEdge>(multigraph: true, compound: true)
            .SetGraph(new DagreGraph())
            .SetDefaultEdgeLabel((_, _, _) => new DagreEdge());
        g.SetNode("a", new DagreNode { Width = 10, Height = 10 });
        g.SetNode("b", new DagreNode { Width = 10, Height = 10 });
        g.SetEdge("a", "b");
        var meter = new TestLayoutWorkMeter(1, CancellationToken.None);

        // Act
        var ex = Assert.Throws<LayoutTestException>(() => DagreLayout.Run(g, meter));

        // Assert: the limit failure itself, not a render failure wrapping its message
        Assert.True(ex.IsLimit);
        Assert.DoesNotContain("the layout failed", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the meter recognizes the consumer's failures and nothing else, so that other invalid operations
    ///     of a layout are wrapped as the consumer's render failure.
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_IsFailure_ConsumerAndOtherExceptions_Classified()
    {
        // Arrange
        LayoutWorkMeter meter = new TestLayoutWorkMeter(1, CancellationToken.None);

        // Act
        var consumer = meter.IsFailure(new LayoutTestException());
        var other = meter.IsFailure(new InvalidOperationException("dagre"));

        // Assert
        Assert.True(consumer);
        Assert.False(other);
    }
}
