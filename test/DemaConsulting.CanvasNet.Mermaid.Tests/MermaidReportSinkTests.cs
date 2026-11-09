namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Unit tests for <see cref="MermaidReportSink"/>: ordered, de-duplicated, capped delivery and strict mode.
/// </summary>
public class MermaidReportSinkTests
{
    /// <summary>
    ///     Validates that reports are delivered in the order they were recorded, each to its handler.
    /// </summary>
    [Fact]
    public void MermaidReportSink_Deliver_KeepsOrderAcrossKinds()
    {
        // Arrange
        var sink = new MermaidReportSink(10, throwOnUnsupported: false);
        var order = new List<string>();
        sink.Warn("mermaid-a", "first", default);
        sink.Unsupported("mermaid-b", "second", default, "hint");
        sink.Warn("mermaid-c", "third", default);

        // Act
        sink.Deliver(w => order.Add(w.Code), u => order.Add(u.Feature + ":" + u.Hint));

        // Assert
        Assert.Equal(["mermaid-a", "mermaid-b:hint", "mermaid-c"], order);
    }

    /// <summary>
    ///     Validates that identical reports are recorded once.
    /// </summary>
    [Fact]
    public void MermaidReportSink_Warn_Duplicate_IsRecordedOnce()
    {
        // Arrange
        var sink = new MermaidReportSink(10, throwOnUnsupported: false);

        // Act
        sink.Warn("mermaid-a", "same", new MermaidLocation(null, 1, 1));
        sink.Warn("mermaid-a", "same", new MermaidLocation(null, 1, 1));
        sink.Warn("mermaid-a", "same", new MermaidLocation(null, 2, 1));

        // Assert
        Assert.Equal(2, sink.Count);
    }

    /// <summary>
    ///     Validates the cap: reports beyond the limit collapse into one suppression notice.
    /// </summary>
    [Fact]
    public void MermaidReportSink_BeyondCap_AddsSingleSuppressedNotice()
    {
        // Arrange
        var sink = new MermaidReportSink(2, throwOnUnsupported: false);
        var warnings = new List<MermaidDiagnostic>();

        // Act
        for (var i = 0; i < 5; i++)
        {
            sink.Warn("mermaid-a", $"w{i}", default);
        }

        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal(3, warnings.Count);
        Assert.Equal(MermaidDiagnosticCodes.DiagnosticsSuppressed, warnings[2].Code);
        Assert.Contains("MermaidLimits.MaxDiagnostics", warnings[2].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates strict mode: the first unsupported feature is recorded (even beyond the cap) and thrown.
    /// </summary>
    [Fact]
    public void MermaidReportSink_Unsupported_Strict_RecordsAndThrows()
    {
        // Arrange
        var sink = new MermaidReportSink(1, throwOnUnsupported: true);
        sink.Warn("mermaid-a", "fills the cap", default);
        var unsupported = new List<MermaidUnsupportedFeature>();

        // Act
        var ex = Assert.Throws<MermaidUnsupportedFeatureException>(() => sink.Unsupported("mermaid-b", "strict", new MermaidLocation(null, 3, 4), "do this"));
        sink.Deliver(null, unsupported.Add);

        // Assert
        Assert.Equal(("mermaid-b", "do this"), (ex.Feature, ex.Hint));
        Assert.Single(unsupported);
    }

    /// <summary>
    ///     Validates that a failing handler is ignored when delivery follows a failure.
    /// </summary>
    [Fact]
    public void MermaidReportSink_DeliverIgnoringHandlerFailures_SwallowsHandlerException()
    {
        // Arrange
        var sink = new MermaidReportSink(10, throwOnUnsupported: false);
        sink.Warn("mermaid-a", "x", default);

        // Act / Assert: no exception escapes
        sink.DeliverIgnoringHandlerFailures(_ => throw new InvalidOperationException("handler"), null);
        Assert.Throws<InvalidOperationException>(() => sink.Deliver(_ => throw new InvalidOperationException("handler"), null));
    }
}
