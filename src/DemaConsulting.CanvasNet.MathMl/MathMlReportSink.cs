using System.Globalization;

namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     Collects the warnings and unsupported-feature reports of one call, in the order they occur, and delivers them to
///     the caller's handlers once the work is done.
/// </summary>
/// <remarks>
///     <para>
///         Buffering instead of calling the handlers immediately keeps delivery on the thread that made the call, exactly
///         once and in a deterministic order, whatever the pipeline does internally, and lets the library decide what to do
///         when a handler throws: after a successful call the handler's exception propagates, after a failed call it is
///         ignored so that the original failure is the one the caller sees.
///     </para>
///     <para>
///         Identical reports (same code, message, location and hint) are recorded once. At most
///         <see cref="MathMlLimits.MaxDiagnostics"/> reports are kept; the first one beyond the cap is replaced by a single
///         <see cref="MathMlDiagnosticCodes.DiagnosticsSuppressed"/> notice and the rest are dropped, so a hostile equation
///         cannot grow the buffer without bound. Strict mode is unaffected by the cap: the first unsupported feature is
///         always recorded, so it is delivered to the handler before the exception that it causes is thrown. An instance is
///         used by one call on one thread.
///     </para>
/// </remarks>
internal sealed class MathMlReportSink
{
    /// <summary>The recorded reports in order; each is a warning or an unsupported feature.</summary>
    private readonly List<(MathMlDiagnostic? Warning, MathMlUnsupportedFeature? Unsupported)> _events = [];

    /// <summary>The warnings recorded so far, for de-duplication.</summary>
    private readonly HashSet<MathMlDiagnostic> _seenWarnings = [];

    /// <summary>The unsupported features recorded so far, for de-duplication.</summary>
    private readonly HashSet<MathMlUnsupportedFeature> _seenUnsupported = [];

    /// <summary>The maximum number of reports that are kept.</summary>
    private readonly int _maxReports;

    /// <summary>Whether the first unsupported feature throws.</summary>
    private readonly bool _throwOnUnsupported;

    /// <summary>Whether the suppression notice has been recorded.</summary>
    private bool _suppressed;

    /// <summary>
    ///     Initializes a sink.
    /// </summary>
    /// <param name="maxReports">The maximum number of reports to keep; at least one.</param>
    /// <param name="throwOnUnsupported">Whether the first unsupported feature throws.</param>
    public MathMlReportSink(int maxReports, bool throwOnUnsupported)
    {
        _maxReports = maxReports;
        _throwOnUnsupported = throwOnUnsupported;
    }

    /// <summary>
    ///     Gets the number of reports that are currently recorded.
    /// </summary>
    public int Count => _events.Count;

    /// <summary>
    ///     Records a warning.
    /// </summary>
    /// <param name="code">The stable <c>mathml-*</c> code.</param>
    /// <param name="message">The description of the problem.</param>
    /// <param name="location">The location it is about.</param>
    /// <param name="hint">The suggested fix, or null.</param>
    public void Warn(string code, string message, MathMlLocation location, string? hint = null)
    {
        var warning = new MathMlDiagnostic(code, message, location) { Hint = hint };
        if (_seenWarnings.Add(warning))
        {
            Add((warning, null));
        }
    }

    /// <summary>
    ///     Records an unsupported feature and, in strict mode, throws for it.
    /// </summary>
    /// <param name="feature">The stable <c>mathml-*</c> identifier.</param>
    /// <param name="message">The description.</param>
    /// <param name="location">The location of the construct.</param>
    /// <param name="hint">The suggested way forward, or null.</param>
    /// <exception cref="MathMlUnsupportedFeatureException">Thrown when strict mode is on (after the report is recorded).</exception>
    public void Unsupported(string feature, string message, MathMlLocation location, string? hint = null)
    {
        var report = new MathMlUnsupportedFeature(feature, message, location) { Hint = hint };
        if (_seenUnsupported.Add(report))
        {
            // Strict mode delivers the report that it throws for, even when the buffer is full of earlier reports
            Add((null, report), force: _throwOnUnsupported);
        }

        // Strict mode must stop at the first report even when the buffer is full
        if (_throwOnUnsupported)
        {
            throw new MathMlUnsupportedFeatureException(feature, message, location, hint);
        }
    }

    /// <summary>
    ///     Delivers the recorded reports, in order, to the handlers; an exception of a handler propagates.
    /// </summary>
    /// <param name="warningHandler">The warning handler, or null.</param>
    /// <param name="unsupportedHandler">The unsupported-feature handler, or null.</param>
    public void Deliver(Action<MathMlDiagnostic>? warningHandler, Action<MathMlUnsupportedFeature>? unsupportedHandler)
    {
        foreach (var (warning, unsupported) in _events)
        {
            if (warning is not null)
            {
                warningHandler?.Invoke(warning);
            }
            else if (unsupported is not null)
            {
                unsupportedHandler?.Invoke(unsupported);
            }
        }
    }

    /// <summary>
    ///     Delivers the recorded reports after a failure: each handler call is protected so that a failing handler cannot
    ///     hide the failure that is being reported.
    /// </summary>
    /// <param name="warningHandler">The warning handler, or null.</param>
    /// <param name="unsupportedHandler">The unsupported-feature handler, or null.</param>
    public void DeliverIgnoringHandlerFailures(Action<MathMlDiagnostic>? warningHandler, Action<MathMlUnsupportedFeature>? unsupportedHandler)
    {
        try
        {
            Deliver(warningHandler, unsupportedHandler);
        }
#pragma warning disable CA1031 // A handler's failure must not mask the failure that is already being reported
        catch (Exception)
#pragma warning restore CA1031
        {
            // Intentionally ignored: see the method documentation
        }
    }

    /// <summary>
    ///     Adds a report, applying the cap.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="force">Whether to keep the report beyond the cap (the report that strict mode throws for).</param>
    private void Add((MathMlDiagnostic?, MathMlUnsupportedFeature?) report, bool force = false)
    {
        if (force || _events.Count < _maxReports)
        {
            _events.Add(report);
            return;
        }

        // The first overflow leaves a single notice; everything after it is dropped
        if (!_suppressed)
        {
            _suppressed = true;
            _events.Add((
                new MathMlDiagnostic(
                    MathMlDiagnosticCodes.DiagnosticsSuppressed,
                    string.Create(CultureInfo.InvariantCulture, $"further diagnostics were suppressed after the limit of {_maxReports} (MathMlLimits.MaxDiagnostics)."),
                    default),
                null));
        }
    }
}

/// <summary>
///     A report found while the text is read (an element of another namespace, a link, text outside a token element),
///     kept by the document and recorded again into the sink of every measure and render call.
/// </summary>
/// <remarks>
///     Reading has no handlers to deliver to (the handlers belong to a render call), so the reader keeps what it finds as
///     pending reports, in text order; every call replays them first, so measuring and rendering deliver the same reports
///     and strict mode throws for an unsupported construct whatever stage found it. The record is immutable and safe for
///     concurrent use.
/// </remarks>
/// <param name="IsWarning">True for a warning, false for an unsupported feature.</param>
/// <param name="Code">The stable <c>mathml-*</c> code.</param>
/// <param name="Message">The description.</param>
/// <param name="Location">The location in the original text.</param>
/// <param name="Hint">The suggested fix or way forward, or null.</param>
internal sealed record MathMlPendingReport(bool IsWarning, string Code, string Message, MathMlLocation Location, string? Hint)
{
    /// <summary>
    ///     Creates a pending warning.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="message">The description.</param>
    /// <param name="location">The location.</param>
    /// <param name="hint">The hint, or null.</param>
    /// <returns>The report.</returns>
    public static MathMlPendingReport Warning(string code, string message, MathMlLocation location, string? hint = null) =>
        new(true, code, message, location, hint);

    /// <summary>
    ///     Creates a pending unsupported feature.
    /// </summary>
    /// <param name="code">The feature identifier.</param>
    /// <param name="message">The description.</param>
    /// <param name="location">The location.</param>
    /// <param name="hint">The hint, or null.</param>
    /// <returns>The report.</returns>
    public static MathMlPendingReport Unsupported(string code, string message, MathMlLocation location, string? hint = null) =>
        new(false, code, message, location, hint);

    /// <summary>
    ///     Records the report into a sink.
    /// </summary>
    /// <param name="sink">The sink of the call.</param>
    /// <exception cref="MathMlUnsupportedFeatureException">Thrown, for an unsupported feature, in strict mode.</exception>
    public void Replay(MathMlReportSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (IsWarning)
        {
            sink.Warn(Code, Message, Location, Hint);
        }
        else
        {
            sink.Unsupported(Code, Message, Location, Hint);
        }
    }
}
