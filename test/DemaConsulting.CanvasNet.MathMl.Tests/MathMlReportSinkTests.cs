namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Unit tests for <see cref="MathMlReportSink"/> and <see cref="MathMlPendingReport"/>: ordered, de-duplicated,
///     capped delivery and strict mode.
/// </summary>
public class MathMlReportSinkTests
{
    /// <summary>
    ///     Validates that reports are delivered in the order they were recorded, each to its handler.
    /// </summary>
    [Fact]
    public void MathMlReportSink_Deliver_KeepsOrderAcrossKinds()
    {
        // Arrange
        var sink = new MathMlReportSink(10, throwOnUnsupported: false);
        var order = new List<string>();
        sink.Warn("mathml-a", "first", default);
        sink.Unsupported("mathml-b", "second", default, "hint");
        sink.Warn("mathml-c", "third", default);

        // Act
        sink.Deliver(w => order.Add(w.Code), u => order.Add(u.Feature + ":" + u.Hint));

        // Assert
        Assert.Equal(["mathml-a", "mathml-b:hint", "mathml-c"], order);
    }

    /// <summary>
    ///     Validates that identical reports are recorded once.
    /// </summary>
    [Fact]
    public void MathMlReportSink_Warn_Duplicate_IsRecordedOnce()
    {
        // Arrange
        var sink = new MathMlReportSink(10, throwOnUnsupported: false);

        // Act
        sink.Warn("mathml-a", "same", new MathMlLocation(null, 1, 1));
        sink.Warn("mathml-a", "same", new MathMlLocation(null, 1, 1));
        sink.Warn("mathml-a", "same", new MathMlLocation(null, 2, 1));

        // Assert
        Assert.Equal(2, sink.Count);
    }

    /// <summary>
    ///     Validates the cap: reports beyond the limit collapse into one suppression notice.
    /// </summary>
    [Fact]
    public void MathMlReportSink_BeyondCap_AddsSingleSuppressedNotice()
    {
        // Arrange
        var sink = new MathMlReportSink(2, throwOnUnsupported: false);
        var warnings = new List<MathMlDiagnostic>();

        // Act
        for (var i = 0; i < 5; i++)
        {
            sink.Warn("mathml-a", $"w{i}", default);
        }

        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal(3, warnings.Count);
        Assert.Equal(MathMlDiagnosticCodes.DiagnosticsSuppressed, warnings[2].Code);
        Assert.Contains("MathMlLimits.MaxDiagnostics", warnings[2].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that once the suppression notice is recorded, further distinct reports of either kind are neither
    ///     kept nor remembered, so the bookkeeping of a call stays bounded.
    /// </summary>
    [Fact]
    public void MathMlReportSink_BeyondCap_StopsRememberingReports()
    {
        // Arrange
        var sink = new MathMlReportSink(2, throwOnUnsupported: false);

        // Act: two kept reports, one that is replaced by the notice, then a thousand more of both kinds
        sink.Warn("mathml-a", "w0", default);
        sink.Unsupported("mathml-b", "u0", default);
        sink.Warn("mathml-a", "w1", default);
        var trackedAtSuppression = sink.TrackedCount;
        for (var i = 2; i < 1_002; i++)
        {
            sink.Warn("mathml-a", $"w{i}", default);
            sink.Unsupported("mathml-b", $"u{i}", default);
        }

        // Assert
        Assert.Equal((3, 3), (sink.Count, trackedAtSuppression));
        Assert.Equal(3, sink.TrackedCount);
    }

    /// <summary>
    ///     Validates strict mode: the first unsupported feature is recorded (even beyond the cap) and thrown.
    /// </summary>
    [Fact]
    public void MathMlReportSink_Unsupported_Strict_RecordsAndThrows()
    {
        // Arrange
        var sink = new MathMlReportSink(1, throwOnUnsupported: true);
        sink.Warn("mathml-a", "fills the cap", default);
        var unsupported = new List<MathMlUnsupportedFeature>();

        // Act
        var ex = Assert.Throws<MathMlUnsupportedFeatureException>(() => sink.Unsupported("mathml-b", "strict", new MathMlLocation(null, 3, 4), "do this"));
        sink.Deliver(null, unsupported.Add);

        // Assert
        Assert.Equal(("mathml-b", "do this"), (ex.Feature, ex.Hint));
        Assert.Single(unsupported);
    }

    /// <summary>
    ///     Validates that a failing handler is ignored when delivery follows a failure, and propagates otherwise.
    /// </summary>
    [Fact]
    public void MathMlReportSink_DeliverIgnoringHandlerFailures_SwallowsHandlerException()
    {
        // Arrange
        var sink = new MathMlReportSink(10, throwOnUnsupported: false);
        sink.Warn("mathml-a", "x", default);

        // Act / Assert: no exception escapes
        sink.DeliverIgnoringHandlerFailures(_ => throw new InvalidOperationException("handler"), null);
        Assert.Throws<InvalidOperationException>(() => sink.Deliver(_ => throw new InvalidOperationException("handler"), null));
    }

    /// <summary>
    ///     Validates that pending reports replay into a sink as the kind they were recorded as.
    /// </summary>
    [Fact]
    public void MathMlPendingReport_Replay_RecordsItsKind()
    {
        // Arrange
        var sink = new MathMlReportSink(10, throwOnUnsupported: false);
        var kinds = new List<string>();

        // Act
        MathMlPendingReport.Warning("mathml-a", "w", default).Replay(sink);
        MathMlPendingReport.Unsupported("mathml-b", "u", default).Replay(sink);
        sink.Deliver(w => kinds.Add("warning:" + w.Code), u => kinds.Add("unsupported:" + u.Feature));

        // Assert
        Assert.Equal(["warning:mathml-a", "unsupported:mathml-b"], kinds);
    }
}
