namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Unit tests for <see cref="VegaLiteReportSink"/>.
/// </summary>
public class VegaLiteReportSinkTests
{
    /// <summary>The location used by the tests.</summary>
    private static readonly VegaLiteLocation Where = new(null, "/mark", 1, 1);

    /// <summary>
    ///     Validates that reports are delivered in the order they were recorded, to the right handlers.
    /// </summary>
    [Fact]
    public void VegaLiteReportSink_Deliver_PreservesOrderAcrossKinds()
    {
        // Arrange
        var sink = new VegaLiteReportSink(10, throwOnUnsupported: false);
        var log = new List<string>();
        sink.Warn("w1", Where);
        sink.Unsupported("f1", "u1", Where);
        sink.Warn("w2", Where);

        // Act
        sink.Deliver(w => log.Add("W:" + w.Message), u => log.Add("U:" + u.Feature));

        // Assert
        Assert.Equal(["W:w1", "U:f1", "W:w2"], log);
        Assert.Equal(3, sink.Count);
    }

    /// <summary>
    ///     Validates that identical reports are recorded once.
    /// </summary>
    [Fact]
    public void VegaLiteReportSink_Duplicates_AreRecordedOnce()
    {
        // Arrange
        var sink = new VegaLiteReportSink(10, throwOnUnsupported: false);

        // Act
        sink.Warn("w", Where);
        sink.Warn("w", Where);
        sink.Unsupported("f", "m", Where);
        sink.Unsupported("f", "m", Where);

        // Assert
        Assert.Equal(2, sink.Count);
    }

    /// <summary>
    ///     Validates that the optional de-duplication key distinguishes warnings with the same message and location,
    ///     that an equal key or no key still collapses them, and that keyed warnings count toward the cap.
    /// </summary>
    [Fact]
    public void VegaLiteReportSink_WarnWithKey_DistinctKeysAreRecordedSeparately()
    {
        // Arrange
        var sink = new VegaLiteReportSink(10, throwOnUnsupported: false);
        var keyed = new VegaLiteReportSink(10, throwOnUnsupported: false);
        var capped = new VegaLiteReportSink(2, throwOnUnsupported: false);
        var delivered = new List<string>();

        // Act
        sink.Warn("w", Where, "a");
        sink.Warn("w", Where, "b");
        sink.Warn("w", Where, "a");
        keyed.Warn("w", Where);
        keyed.Warn("w", Where);
        keyed.Warn("w", Where, "a");
        keyed.Warn("w", Where, "a");
        for (var i = 0; i < 5; i++)
        {
            capped.Warn("w", Where, "k" + i);
        }

        capped.Deliver(w => delivered.Add(w.Message), null);

        // Assert: two keys are two reports, a repeated key and a null key are one each, and the cap applies to keyed warnings
        Assert.Equal(2, sink.Count);
        Assert.Equal(2, keyed.Count);
        Assert.Equal(3, capped.Count);
        Assert.Equal(["w", "w"], delivered[..2]);
        Assert.StartsWith("vl-diagnostics-suppressed:", delivered[2], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the cap collapses the overflow into a single notice.
    /// </summary>
    [Fact]
    public void VegaLiteReportSink_Cap_CollapsesOverflowIntoOneNotice()
    {
        // Arrange
        var sink = new VegaLiteReportSink(2, throwOnUnsupported: false);
        var warnings = new List<string>();

        // Act
        for (var i = 0; i < 10; i++)
        {
            sink.Warn("w" + i, Where);
        }

        sink.Deliver(w => warnings.Add(w.Message), null);

        // Assert
        Assert.Equal(3, warnings.Count);
        Assert.Equal("w0", warnings[0]);
        Assert.StartsWith("vl-diagnostics-suppressed:", warnings[2], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that strict mode records the report and then throws, even when the buffer is full, so that the report is
    ///     delivered to the handler before the exception is seen.
    /// </summary>
    [Fact]
    public void VegaLiteReportSink_Strict_ThrowsAtFirstUnsupportedFeatureAfterRecordingIt()
    {
        // Arrange
        var sink = new VegaLiteReportSink(1, throwOnUnsupported: true);
        sink.Warn("fills the buffer", Where);
        var delivered = new List<string>();

        // Act
        var ex = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => sink.Unsupported("vl-mark-geoshape", "no maps", Where));
        sink.Deliver(w => delivered.Add(w.Message), u => delivered.Add(u.Feature));

        // Assert: the warning that filled the buffer, then the feature that the exception is about, each once
        Assert.Equal("vl-mark-geoshape", ex.Feature);
        Assert.Equal(Where, ex.Location);
        Assert.Equal(["fills the buffer", "vl-mark-geoshape"], delivered);
    }

    /// <summary>
    ///     Validates that a handler exception propagates from normal delivery but is swallowed after a failure.
    /// </summary>
    [Fact]
    public void VegaLiteReportSink_HandlerFailure_PropagatesOnlyFromNormalDelivery()
    {
        // Arrange
        var sink = new VegaLiteReportSink(10, throwOnUnsupported: false);
        sink.Warn("w1", Where);
        sink.Warn("w2", Where);
        var calls = 0;

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => sink.Deliver(_ => throw new InvalidOperationException("handler"), null));
        sink.DeliverIgnoringHandlerFailures(_ => { calls++; throw new InvalidOperationException("handler"); }, null);
        Assert.Equal(1, calls);
    }
}
