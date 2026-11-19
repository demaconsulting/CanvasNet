namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Unit tests of <see cref="MermaidLayoutWorkMeter"/>: the located Mermaid failures of the shared layout work meter.
/// </summary>
public class MermaidLayoutWorkMeterTests
{
    /// <summary>
    ///     Validates that work is added up to the limit and that passing it fails with a located limit exception that
    ///     names <c>MermaidLimits.MaxLayoutWork</c> and has a hint.
    /// </summary>
    [Fact]
    public void MermaidLayoutWorkMeter_Charge_PastLimit_ThrowsLocatedLimitExceeded()
    {
        // Arrange
        var location = new MermaidLocation("d.mmd", 3, 1);
        var meter = new MermaidLayoutWorkMeter(10, location, CancellationToken.None);

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
    ///     Validates the render failure that the layout reports for a graph dagre cannot lay out.
    /// </summary>
    [Fact]
    public void MermaidLayoutWorkMeter_Failure_Description_RenderFailedAtLocation()
    {
        // Arrange
        var location = new MermaidLocation(null, 2, 5);
        var meter = new MermaidLayoutWorkMeter(1, location, CancellationToken.None);

        // Act
        var ex = meter.Failure("no route");

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.RenderFailed, location), (ex.Code, ex.Location));
        Assert.Contains("no route", ex.Message, StringComparison.Ordinal);
    }
}
