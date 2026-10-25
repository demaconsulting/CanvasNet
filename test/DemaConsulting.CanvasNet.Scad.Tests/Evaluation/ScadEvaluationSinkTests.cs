using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

// cspell:ignore textmetrics

/// <summary>
///     Unit tests for <see cref="ScadEvaluationSink"/>.
/// </summary>
public class ScadEvaluationSinkTests
{
    /// <summary>A location used by the tests.</summary>
    private static readonly ScadSourceLocation Here = new("t.scad", 1, 1);

    /// <summary>
    ///     Validates that echo lines are buffered in order.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Echo_BuffersLinesInOrder()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        sink.Echo("ECHO: 1");
        sink.Echo("ECHO: 2");

        // Assert
        Assert.Equal(["ECHO: 1", "ECHO: 2"], sink.EchoLines);
    }

    /// <summary>
    ///     Validates the output limit: exactly at the limit succeeds and one character more fails with the location.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Echo_AtLimit_Succeeds_OneOver_Throws()
    {
        // Arrange
        var sink = new ScadEvaluationSink(new ScadLimits { MaxOutputLength = 10 });

        // Act
        sink.Echo("12345");
        sink.Echo("67890");
        var ex = Assert.Throws<ScadEvaluationException>(() => sink.Echo("x", Here));

        // Assert
        Assert.Equal(["12345", "67890"], sink.EchoLines);
        Assert.Equal(Here, ex.Location);
        Assert.Equal("t.scad:1:1: Echo output limit exceeded (limit 10)", ex.Message);
    }

    /// <summary>
    ///     Validates that a single line longer than the limit is rejected without being stored.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Echo_SingleLineOverLimit_Throws()
    {
        // Arrange
        var sink = new ScadEvaluationSink(new ScadLimits { MaxOutputLength = 4 });

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => sink.Echo("12345"));
        Assert.Empty(sink.EchoLines);
    }

    /// <summary>
    ///     Validates that identical warnings (message and location) are recorded once.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Warn_DuplicatesAreDropped()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        sink.Warn("w", Here);
        sink.Warn("w", Here);
        sink.Warn("w", new ScadSourceLocation("t.scad", 2, 1));
        sink.Warn("other", Here);

        // Assert
        Assert.Equal(3, sink.Diagnostics.Count);
        Assert.Equal(new ScadDiagnostic("w", Here), sink.Diagnostics[0]);
    }

    /// <summary>
    ///     Validates the diagnostics cap: the real warnings up to the cap are kept, then exactly one notice.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Warn_BeyondCap_CollapsesIntoOneNotice()
    {
        // Arrange
        var sink = new ScadEvaluationSink(new ScadLimits { MaxDiagnostics = 3 });

        // Act: ten distinct warnings, then repeats of the first and of a dropped one
        for (var i = 1; i <= 10; i++)
        {
            sink.Warn($"w{i}", new ScadSourceLocation("t.scad", i, 1));
        }

        sink.Warn("w1", new ScadSourceLocation("t.scad", 1, 1));
        sink.Warn("w9", new ScadSourceLocation("t.scad", 9, 1));

        // Assert
        Assert.Equal(4, sink.Diagnostics.Count);
        Assert.Equal("w3", sink.Diagnostics[2].Message);
        Assert.Equal("Further diagnostics suppressed (limit 3)", sink.Diagnostics[3].Message);
        Assert.Equal(4, sink.Diagnostics[3].Location.Line);
    }

    /// <summary>
    ///     Validates that exactly at the cap no notice is produced.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Warn_ExactlyAtCap_HasNoNotice()
    {
        // Arrange
        var sink = new ScadEvaluationSink(new ScadLimits { MaxDiagnostics = 2 });

        // Act
        sink.Warn("a", Here);
        sink.Warn("b", Here);
        sink.Warn("a", Here);

        // Assert
        Assert.Equal(2, sink.Diagnostics.Count);
    }

    /// <summary>
    ///     Validates that a warning loop of many distinct locations keeps memory and output bounded.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Warn_ManyDistinctWarnings_StaysBounded()
    {
        // Arrange
        var sink = new ScadEvaluationSink(new ScadLimits { MaxDiagnostics = 100 });

        // Act
        for (var i = 0; i < 50_000; i++)
        {
            sink.Warn("loop", new ScadSourceLocation(null, i + 1, 1));
        }

        // Assert
        Assert.Equal(101, sink.Diagnostics.Count);
    }

    /// <summary>
    ///     Validates unsupported-feature recording and de-duplication by feature and location.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Unsupported_RecordsAndDeduplicates()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var first = sink.Unsupported("scad-function-textmetrics", "not supported", Here);
        var duplicate = sink.Unsupported("scad-function-textmetrics", "not supported", Here);
        var otherLocation = sink.Unsupported("scad-function-textmetrics", "not supported", new ScadSourceLocation("t.scad", 5, 1));
        var otherFeature = sink.Unsupported("scad-search-form", "not supported", Here);

        // Assert
        Assert.True(first);
        Assert.False(duplicate);
        Assert.True(otherLocation);
        Assert.True(otherFeature);
        Assert.Equal(3, sink.UnsupportedFeatures.Count);
        Assert.Equal("scad-function-textmetrics", sink.UnsupportedFeatures[0].Feature);
    }

    /// <summary>
    ///     Validates that reset clears every buffer, counter and de-duplication memory.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Reset_ClearsEverything()
    {
        // Arrange
        var sink = new ScadEvaluationSink(new ScadLimits { MaxOutputLength = 8, MaxDiagnostics = 1 });
        sink.Echo("12345678");
        sink.Warn("a", Here);
        sink.Warn("b", Here);
        sink.Unsupported("f", "m", Here);

        // Act
        sink.Reset();
        sink.Echo("12345678");
        sink.Warn("a", Here);
        sink.Unsupported("f", "m", Here);
        var delivered = new List<string>();
        sink.Deliver(e => delivered.Add("echo:" + e), w => delivered.Add("warn:" + w.Message), u => delivered.Add("unsupported:" + u.Feature));

        // Assert: nothing from before the reset is left, and the limits start over
        Assert.Equal(["echo:12345678", "warn:a", "unsupported:f"], delivered);
        Assert.Single(sink.Diagnostics);
    }

    /// <summary>
    ///     Validates that delivery replays the events in the order they were recorded across categories.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Deliver_KeepsChronologicalOrder()
    {
        // Arrange
        var sink = new ScadEvaluationSink(new ScadLimits { MaxDiagnostics = 1 });
        sink.Warn("first", Here);
        sink.Echo("ECHO: a");
        sink.Unsupported("f", "m", Here);
        sink.Warn("second", Here);
        sink.Echo("ECHO: b");
        var delivered = new List<string>();

        // Act
        sink.Deliver(e => delivered.Add(e), w => delivered.Add(w.Message), u => delivered.Add(u.Feature));

        // Assert
        Assert.Equal(
            ["first", "ECHO: a", "f", "Further diagnostics suppressed (limit 1)", "ECHO: b"],
            delivered);
    }

    /// <summary>
    ///     Validates that missing handlers are skipped and that a handler exception propagates.
    /// </summary>
    [Fact]
    public void ScadEvaluationSink_Deliver_NullHandlersSkipped_HandlerExceptionPropagates()
    {
        // Arrange
        var sink = new ScadEvaluationSink(ScadLimits.Default);
        sink.Echo("ECHO: a");
        sink.Warn("w", Here);
        sink.Unsupported("f", "m", Here);

        // Act / Assert
        sink.Deliver(null, null, null);
        Assert.Throws<InvalidOperationException>(() => sink.Deliver(_ => throw new InvalidOperationException("handler"), null, null));
    }
}
