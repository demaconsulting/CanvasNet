using DemaConsulting.CanvasNet.Mermaid.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="LayoutWorkMeter"/>.
/// </summary>
public class LayoutWorkMeterTests
{
    /// <summary>
    ///     Validates that work is added up to the limit and that passing it fails with a located limit exception.
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_Charge_PastLimit_ThrowsLocatedLimitExceeded()
    {
        // Arrange
        var location = new MermaidLocation("d.mmd", 3, 1);
        var meter = new LayoutWorkMeter(10, location, CancellationToken.None);

        // Act
        meter.Charge(4);
        meter.Charge(6);
        var ex = Assert.Throws<MermaidRenderException>(() => meter.Charge(1));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(location, ex.Location);
        Assert.Contains("MermaidLimits.MaxLayoutWork", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.Hint);
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
        var meter = new LayoutWorkMeter(100, default, source.Token);
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
        var meter = new LayoutWorkMeter(1, default, CancellationToken.None);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutWorkMeter(0, default, CancellationToken.None));
    }

    /// <summary>
    ///     Validates the render failure that the layout reports for a graph dagre cannot lay out.
    /// </summary>
    [Fact]
    public void LayoutWorkMeter_Failure_Description_RenderFailedAtLocation()
    {
        // Arrange
        var location = new MermaidLocation(null, 2, 5);
        var meter = new LayoutWorkMeter(1, location, CancellationToken.None);

        // Act
        var ex = meter.Failure("no route");

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, location), (ex.Code, ex.Location));
        Assert.Contains("no route", ex.Message, StringComparison.Ordinal);
    }
}
