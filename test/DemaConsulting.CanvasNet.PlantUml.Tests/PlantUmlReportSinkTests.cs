namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Unit tests for <see cref="PlantUmlReportSink"/>: ordered, de-duplicated, capped delivery and strict mode.
/// </summary>
public class PlantUmlReportSinkTests
{
    /// <summary>
    ///     Validates that reports are delivered in the order they were recorded, each to its handler.
    /// </summary>
    [Fact]
    public void PlantUmlReportSink_Deliver_KeepsOrderAcrossKinds()
    {
        // Arrange
        var sink = new PlantUmlReportSink(10, throwOnUnsupported: false);
        var order = new List<string>();
        sink.Warn("plantuml-a", "first", default);
        sink.Unsupported("plantuml-b", "second", default, "hint");
        sink.Warn("plantuml-c", "third", default);

        // Act
        sink.Deliver(w => order.Add(w.Code), u => order.Add(u.Feature + ":" + u.Hint));

        // Assert
        Assert.Equal(["plantuml-a", "plantuml-b:hint", "plantuml-c"], order);
    }

    /// <summary>
    ///     Validates that identical reports are recorded once.
    /// </summary>
    [Fact]
    public void PlantUmlReportSink_Warn_Duplicate_IsRecordedOnce()
    {
        // Arrange
        var sink = new PlantUmlReportSink(10, throwOnUnsupported: false);

        // Act
        sink.Warn("plantuml-a", "same", new PlantUmlLocation(null, 1, 1));
        sink.Warn("plantuml-a", "same", new PlantUmlLocation(null, 1, 1));
        sink.Warn("plantuml-a", "same", new PlantUmlLocation(null, 2, 1));

        // Assert
        Assert.Equal(2, sink.Count);
    }

    /// <summary>
    ///     Validates the cap: reports beyond the limit collapse into one suppression notice.
    /// </summary>
    [Fact]
    public void PlantUmlReportSink_BeyondCap_AddsSingleSuppressedNotice()
    {
        // Arrange
        var sink = new PlantUmlReportSink(2, throwOnUnsupported: false);
        var warnings = new List<PlantUmlDiagnostic>();

        // Act
        for (var i = 0; i < 5; i++)
        {
            sink.Warn("plantuml-a", $"w{i}", default);
        }

        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal(3, warnings.Count);
        Assert.Equal(PlantUmlDiagnosticCodes.DiagnosticsSuppressed, warnings[2].Code);
        Assert.Contains("PlantUmlLimits.MaxDiagnostics", warnings[2].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates strict mode: the first unsupported feature is recorded (even beyond the cap) and thrown.
    /// </summary>
    [Fact]
    public void PlantUmlReportSink_Unsupported_Strict_RecordsAndThrows()
    {
        // Arrange
        var sink = new PlantUmlReportSink(1, throwOnUnsupported: true);
        sink.Warn("plantuml-a", "fills the cap", default);
        var unsupported = new List<PlantUmlUnsupportedFeature>();

        // Act
        var ex = Assert.Throws<PlantUmlUnsupportedFeatureException>(() => sink.Unsupported("plantuml-b", "strict", new PlantUmlLocation(null, 3, 4), "do this"));
        sink.Deliver(null, unsupported.Add);

        // Assert
        Assert.Equal(("plantuml-b", "do this"), (ex.Feature, ex.Hint));
        Assert.Single(unsupported);
    }

    /// <summary>
    ///     Validates that a failing handler is ignored when delivery follows a failure.
    /// </summary>
    [Fact]
    public void PlantUmlReportSink_DeliverIgnoringHandlerFailures_SwallowsHandlerException()
    {
        // Arrange
        var sink = new PlantUmlReportSink(10, throwOnUnsupported: false);
        sink.Warn("plantuml-a", "x", default);

        // Act / Assert: no exception escapes
        sink.DeliverIgnoringHandlerFailures(_ => throw new InvalidOperationException("handler"), null);
        Assert.Throws<InvalidOperationException>(() => sink.Deliver(_ => throw new InvalidOperationException("handler"), null));
    }
}
