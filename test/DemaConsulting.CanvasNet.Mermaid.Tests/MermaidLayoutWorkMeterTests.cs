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

    /// <summary>
    ///     Validates that a Mermaid render failure is recognized as the consumer's own failure, so the shared layout lets
    ///     it pass unchanged instead of wrapping it as a new layout failure.
    /// </summary>
    [Fact]
    public void MermaidLayoutWorkMeter_IsFailure_MermaidRenderException_ReturnsTrue()
    {
        // Arrange
        var meter = new MermaidLayoutWorkMeter(1, new MermaidLocation(null, 1, 1), CancellationToken.None);
        var failure = new MermaidRenderException(MermaidDiagnosticCodes.LimitExceeded, "too much", default);

        // Act
        var isFailure = meter.IsFailure(failure);

        // Assert
        Assert.True(isFailure);
    }

    /// <summary>
    ///     Validates that an invalid operation that is not a Mermaid failure is not recognized as one, so the shared layout
    ///     reports it as a located render failure.
    /// </summary>
    [Fact]
    public void MermaidLayoutWorkMeter_IsFailure_InvalidOperationException_ReturnsFalse()
    {
        // Arrange
        var meter = new MermaidLayoutWorkMeter(1, new MermaidLocation(null, 1, 1), CancellationToken.None);

        // Act
        var isFailure = meter.IsFailure(new InvalidOperationException("dagre failed"));

        // Assert
        Assert.False(isFailure);
    }
}
